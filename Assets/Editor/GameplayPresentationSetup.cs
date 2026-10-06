using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class GameplayPresentationSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ControllerPath = "Assets/Animations/Player.controller";
    private const string ProfilePath = "Assets/Settings/StoreAtmosphere.asset";

    [MenuItem("Svinki/Настроить одежду, анимации и атмосферу")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Сначала остановите Play Mode.");
        AnimatorController controller = CreateController();
        foreach (string path in new[] { "Assets/Prefabs/NetworkPlayer.prefab", "Assets/Prefabs/OfflinePlayer.prefab" })
            ConfigurePlayer(path, controller);
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var pickups = InScene<ClothingPickup>(scene).OrderBy(p => p.name, StringComparer.Ordinal).ToList();
            var kept = new HashSet<ClothingPickup>();
            foreach (ClothingSlot slot in Enum.GetValues(typeof(ClothingSlot)))
            {
                var candidates = pickups.Where(p => p.Clothing != null && p.Clothing.Slot == slot).ToList();
                if (candidates.Count == 0) continue;
                // Keep variety, then spread the remaining items across the level.
                foreach (var group in candidates.GroupBy(p => p.Clothing).Take(6)) kept.Add(group.First());
                while (kept.Count(p => p.Clothing.Slot == slot) < 6 && candidates.Any(p => !kept.Contains(p)))
                {
                    var selected = candidates.Where(p => !kept.Contains(p)).OrderByDescending(p =>
                        kept.Where(k => k.Clothing.Slot == slot).Min(k => (p.transform.position - k.transform.position).sqrMagnitude)).First();
                    kept.Add(selected);
                }
            }
            foreach (ClothingPickup pickup in pickups)
            {
                if (!kept.Contains(pickup)) { UnityEngine.Object.DestroyImmediate(pickup.gameObject); continue; }
                RepairCollider(pickup);
                var data = new SerializedObject(pickup.GetComponent<PickupItem>());
                data.FindProperty("afterPickup").enumValueIndex = 0;
                data.FindProperty("sparkleRate").floatValue = 0;
                data.FindProperty("glowStrength").floatValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var decoration = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("Display clothing - ", StringComparison.Ordinal)).ToArray();
            foreach (Transform display in decoration)
            {
                // Stable selection makes rerunning the setup safe for the remaining displays.
                string key = display.name + display.position.ToString("F2");
                uint hash = 2166136261;
                foreach (char letter in key) hash = (hash ^ letter) * 16777619;
                if (hash % 3 != 0) UnityEngine.Object.DestroyImmediate(display.gameObject);
            }
            ConfigureLighting(scene);
            ConfigureDropOffs(scene);
            foreach (var player in InScene<GrayboxPlayerController>(scene))
                if (player.GetComponent<NetworkPlayer>() == null)
                {
                    var animator = player.GetComponentInChildren<Animator>(true);
                    if (animator != null) animator.runtimeAnimatorController = controller;
                    if (player.GetComponent<PlayerAnimation>() == null) player.gameObject.AddComponent<PlayerAnimation>();
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Gameplay setup: " + kept.Count + " pickups; player animation, six thief destinations and dark lighting saved.");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ConfigurePlayer(string path, AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new InvalidOperationException("Нет Animator в " + path);
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (root.GetComponent<PlayerAnimation>() == null) root.AddComponent<PlayerAnimation>();
            var player = root.GetComponent<NetworkPlayer>();
            if (player != null)
            {
                var definitions = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/ClothingItems", "Assets/ClothingLibrary" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<ClothingDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(d => d != null).OrderBy(d => d.name, StringComparer.Ordinal).ToArray();
                var data = new SerializedObject(player); var catalog = data.FindProperty("clothingCatalog");
                catalog.arraySize = definitions.Length;
                for (int i = 0; i < definitions.Length; i++) catalog.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static AnimatorController CreateController()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null)
        {
            var existingMask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Animations/FlashlightArm.mask");
            if (existingMask != null) ConfigureTorchMask(existingMask);
            return existing;
        }
        var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/UAL1_Standard.fbx").OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToDictionary(c => c.name);
        AnimationClip Clip(string name) => clips["Armature|" + name];
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Crouch", AnimatorControllerParameterType.Float);
        controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        var machine = controller.layers[0].stateMachine;
        var move = machine.AddState("Locomotion");
        move.motion = Tree("Move", new[] { Clip("Idle_Loop"), Clip("Walk_Loop"), Clip("Jog_Fwd_Loop"), Clip("Sprint_Loop") }, new[] { 0f, 2.5f, 5f, 10f });
        machine.defaultState = move;
        var crouch = machine.AddState("Crouch");
        crouch.motion = Tree("Crouch", new[] { Clip("Crouch_Idle_Loop"), Clip("Crouch_Fwd_Loop") }, new[] { 0f, 2.5f });
        var start = machine.AddState("Jump Start"); start.motion = Clip("Jump_Start"); start.speed = 4;
        var air = machine.AddState("Jump Air"); air.motion = Clip("Jump_Loop");
        var land = machine.AddState("Jump Land"); land.motion = Clip("Jump_Land"); land.speed = 4;
        Transition(move, crouch).AddCondition(AnimatorConditionMode.Greater, .3f, "Crouch");
        Transition(crouch, move).AddCondition(AnimatorConditionMode.Less, .3f, "Crouch");
        foreach (var ground in new[] { move, crouch })
        {
            var jump = Transition(ground, start); jump.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            jump.AddCondition(AnimatorConditionMode.Greater, .1f, "VerticalSpeed");
            var fall = Transition(ground, air); fall.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            fall.AddCondition(AnimatorConditionMode.Less, .1f, "VerticalSpeed");
        }
        var lift = Transition(start, air); lift.hasExitTime = true; lift.exitTime = .85f;
        Transition(start, land).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        Transition(air, land).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
        var stand = Transition(land, move); stand.hasExitTime = true; stand.exitTime = .85f;
        stand.AddCondition(AnimatorConditionMode.Less, .3f, "Crouch");
        var duck = Transition(land, crouch); duck.hasExitTime = true; duck.exitTime = .85f;
        duck.AddCondition(AnimatorConditionMode.Greater, .3f, "Crouch");
        controller.AddLayer("Flashlight Arm");
        var layers = controller.layers;
        var mask = new AvatarMask { name = "Flashlight left arm" };
        ConfigureTorchMask(mask);
        AssetDatabase.CreateAsset(mask, "Assets/Animations/FlashlightArm.mask");
        layers[1].avatarMask = mask; layers[1].defaultWeight = 0;
        var hold = layers[1].stateMachine.AddState("Hold flashlight"); hold.motion = Clip("Idle_Torch_Loop");
        layers[1].stateMachine.defaultState = hold; controller.layers = layers;
        EditorUtility.SetDirty(controller);
        return controller;

        BlendTree Tree(string name, AnimationClip[] animations, float[] thresholds)
        {
            var tree = new BlendTree { name = name, blendParameter = "Speed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            for (int i = 0; i < animations.Length; i++) tree.AddChild(animations[i], thresholds[i]);
            return tree;
        }
    }

    private static void ConfigureTorchMask(AvatarMask mask)
    {
        mask.name = "Flashlight left arm";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/UAL1_Standard.fbx");
        Transform[] bones = model.GetComponentsInChildren<Transform>(true);
        Transform shoulder = bones.First(t => t.name == "clavicle_l");
        mask.transformCount = bones.Length;
        for (int i = 0; i < bones.Length; i++)
        {
            mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(bones[i], model.transform));
            mask.SetTransformActive(i, bones[i] == shoulder || bones[i].IsChildOf(shoulder));
        }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        EditorUtility.SetDirty(mask);
    }

    private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to)
    {
        var transition = from.AddTransition(to); transition.duration = .1f; transition.hasFixedDuration = true;
        transition.hasExitTime = false; transition.canTransitionToSelf = false;
        return transition;
    }

    private static void RepairCollider(ClothingPickup pickup)
    {
        var renderers = pickup.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("У вещи нет модели: " + pickup.name);
        var local = new Bounds(); bool first = true;
        foreach (Renderer renderer in renderers)
        {
            Bounds b = renderer.bounds;
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
            {
                Vector3 point = pickup.transform.InverseTransformPoint(new Vector3(x == 0 ? b.min.x : b.max.x,
                    y == 0 ? b.min.y : b.max.y, z == 0 ? b.min.z : b.max.z));
                if (first) { local = new Bounds(point, Vector3.zero); first = false; } else local.Encapsulate(point);
            }
        }
        foreach (Collider col in pickup.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        var box = pickup.GetComponent<BoxCollider>();
        if (box == null) box = pickup.gameObject.AddComponent<BoxCollider>();
        box.enabled = true; box.isTrigger = true; box.center = local.center;
        box.size = Vector3.Max(local.size + Vector3.one * .06f, Vector3.one * .22f);
        pickup.gameObject.layer = 0;
    }

    [MenuItem("Svinki/Обновить освещение магазина")]
    public static void RefreshLighting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Сначала остановите Play Mode.");
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            ConfigureLighting(scene);
            foreach (PickupItem item in InScene<PickupItem>(scene))
            {
                var data = new SerializedObject(item);
                data.FindProperty("glowStrength").floatValue = 0;
                data.FindProperty("sparkleRate").floatValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ConfigureLighting(Scene scene)
    {
        RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.075f, .085f, .11f);
        RenderSettings.ambientIntensity = 1; RenderSettings.reflectionIntensity = .08f;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(.009f, .013f, .018f); RenderSettings.fogDensity = .018f;
        foreach (Light light in InScene<Light>(scene))
        {
            if (light.GetComponent<FlashlightController>() != null) { FlashlightController.ConfigureLight(light); continue; }
            if (light.type == LightType.Directional) { light.gameObject.SetActive(false); continue; }
            if (light.name != "Light source") continue;
            light.intensity = light.type == LightType.Spot ? 10f : 4f;
            light.range = light.type == LightType.Spot ? 14 : 11;
        }
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
        T Effect<T>() where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var effect)) return effect;
            effect = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(effect, profile); return effect;
        }
        var color = Effect<ColorAdjustments>(); color.postExposure.Override(-.15f); color.contrast.Override(5); color.saturation.Override(-10);
        var tone = Effect<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
        var vignette = Effect<Vignette>(); vignette.intensity.Override(.20f); vignette.smoothness.Override(.4f);
        var bloom = Effect<Bloom>(); bloom.intensity.Override(.08f); bloom.threshold.Override(2.5f);
        foreach (var volume in InScene<Volume>(scene))
        { volume.sharedProfile = profile; volume.isGlobal = true; volume.weight = 1; volume.gameObject.layer = 0; }
        foreach (Camera camera in InScene<Camera>(scene)) if (camera.CompareTag("MainCamera"))
        {
            camera.allowHDR = true;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true; data.volumeLayerMask = 1;
        }
        foreach (PlanarMirror mirror in InScene<PlanarMirror>(scene))
        {
            Transform fixture = mirror.transform.Find("Mirror warm light");
            if (fixture == null) { fixture = new GameObject("Mirror warm light").transform; fixture.SetParent(mirror.transform, false); }
            fixture.localPosition = new Vector3(0, .8f, 1);
            var lamp = fixture.GetComponent<Light>();
            if (lamp == null) lamp = fixture.gameObject.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.color = new Color(1, .85f, .65f); lamp.intensity = 4; lamp.range = 4;
            lamp.shadows = LightShadows.Soft;
        }
        EditorUtility.SetDirty(profile);
    }

    private static void ConfigureDropOffs(Scene scene)
    {
        var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Thief drop-off points");
        if (root == null) { root = new GameObject("Thief drop-off points"); SceneManager.MoveGameObjectToScene(root, scene); }
        Vector3[] positions = { new Vector3(-18, .05f, 5), new Vector3(18, .05f, 5), new Vector3(-16, .05f, 23),
            new Vector3(20, .05f, 20), new Vector3(-12, .05f, 32), new Vector3(17, .05f, 32) };
        for (int i = root.transform.childCount; i < positions.Length; i++) new GameObject("Stash " + (i + 1)).transform.SetParent(root.transform);
        var points = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++) { points[i] = root.transform.GetChild(i); points[i].position = positions[i]; }
        foreach (ThiefBrain thief in InScene<ThiefBrain>(scene))
        {
            var data = new SerializedObject(thief); var destinations = data.FindProperty("dropOffPoints");
            destinations.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) destinations.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            data.FindProperty("showStateLabel").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static IEnumerable<T> InScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
}
