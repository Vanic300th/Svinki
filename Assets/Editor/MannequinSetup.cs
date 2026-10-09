using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tools > Mannequin:
///  • Build Prefab            — настройки импорта, Animator Controller, префаб Assets/Prefabs/Mannequin.prefab
///  • Setup NavMesh In Scene  — объект NavMesh с NavMeshSurface (+ автосборка при Play)
///  • Place Mannequin         — всё выше + ставит манекена в открытую сцену
///  • Build Watcher Prefab    — Mannequin_Watcher: самостоятельный префаб без мозга, стоит и только поворачивает голову
///  • Place Watcher           — ставит подглядывающего манекена в сцену (префаб собирается, если его нет)
///  • Build Thief Prefab / Place Thief — Воришка, см. ThiefSetup
///  • Online > Build Online Prefabs / Place In Level — сетевые копии и расстановка в уровне, см. MannequinNetworkSetup
/// </summary>
public static class MannequinSetup
{
    private const string ModelPath = "Assets/Models/UAL1_Standard.fbx";
    private const string MaterialPath = "Assets/Materials/Animated Character White.mat";
    private const string ControllerPath = "Assets/Animations/Mannequin.controller";
    private const string PrefabPath = "Assets/Prefabs/Mannequin.prefab";
    private const string WatcherPrefabPath = "Assets/Prefabs/Mannequin_Watcher.prefab";
    private const float Height = 1.8f;

    // Скорости (м/с), при которых включается анимация в блендтри
    private static readonly (string clip, float speed)[] Locomotion =
    {
        ("Idle_Loop", 0f), ("Walk_Loop", 1.4f), ("Jog_Fwd_Loop", 3.2f), ("Sprint_Loop", 5.5f)
    };

    // Клипы, которые будут отдельными состояниями (позы для замирания + атака)
    private static readonly string[] PoseClips =
    {
        "Punch_Jab", "Punch_Cross", "Sword_Attack", "Spell_Simple_Shoot", "Interact",
        "PickUp_Table", "Hit_Head", "Hit_Chest", "Death01", "Walk_Loop", "Sprint_Loop"
    };

    // Кости, по которым проверяется видимость
    private static readonly string[] SightBones =
        { "Head", "spine_03", "pelvis", "hand_l", "hand_r", "foot_l", "foot_r", "lowerarm_l", "lowerarm_r" };

    // ------------------------------------------------------------------

    [MenuItem("Tools/Mannequin/Place Mannequin")]
    public static void PlaceMannequin()
    {
        if (!NotPlaying()) return;
        // Готовый префаб не пересобираем — иначе сбросятся настройки из Inspector (скорость и т.п.)
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = BuildPrefab();
        if (prefab == null) return;
        SetupNavMesh();

        Vector3 pos = PlaceInFrontOfPlayer(prefab, "Place Mannequin", 8f, 0f);
        Debug.Log($"[Mannequin] Манекен поставлен в {pos}. Жми Play.");
    }

    [MenuItem("Tools/Mannequin/Place Watcher")]
    public static void PlaceWatcher()
    {
        if (!NotPlaying()) return;
        GameObject prefab = LoadOrBuildWatcher();
        if (prefab == null) return;

        // Ставим боком к игроку, чтобы сразу было видно, как голова поворачивается
        Vector3 pos = PlaceInFrontOfPlayer(prefab, "Place Watcher", 6f, 90f);
        Debug.Log($"[Mannequin] Подглядывающий поставлен в {pos} боком к тебе. Жми Play, отвернись и посмотри снова.");
    }

    [MenuItem("Tools/Mannequin/Build Watcher Prefab")]
    public static void BuildWatcherPrefabMenu() => BuildWatcherPrefab();

    /// <summary>
    /// Mannequin_Watcher — самостоятельный префаб (не вариант): копия обычного манекена с той же моделью,
    /// материалом и позой покоя, но без мозга и NavMeshAgent. Стоит на месте и только поворачивает голову.
    /// С Mannequin.prefab не связан: правки обычного манекена сюда не попадают, и наоборот.
    /// Build Watcher Prefab собирает копию заново (настройки Watcher в Inspector при этом сбросятся).
    /// </summary>
    public static GameObject BuildWatcherPrefab()
    {
        if (!NotPlaying()) return null;
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (basePrefab == null) basePrefab = BuildPrefab();
        if (basePrefab == null) return null;

        // Собираем во временной сцене, чтобы не трогать открытую
        var temp = EditorSceneManager.NewPreviewScene();
        GameObject prefab = null;
        try
        {
            // Копия обычного манекена, отвязанная от его префаба
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, temp);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            instance.name = "Mannequin_Watcher";

            // Ходить не нужно: сначала мозг (он требует агента), потом агент
            var brain = instance.GetComponent<MannequinBrain>();
            if (brain != null) Object.DestroyImmediate(brain);
            var agent = instance.GetComponent<NavMeshAgent>();
            if (agent != null) Object.DestroyImmediate(agent);

            // Живые манекены обходят его, а не проходят насквозь
            var obstacle = instance.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.center = new Vector3(0f, Height / 2f, 0f);
            obstacle.radius = 0.3f;
            obstacle.height = Height;
            obstacle.carving = true;

            var watcher = instance.AddComponent<MannequinHeadWatcher>();
            var wso = new SerializedObject(watcher);
            wso.FindProperty("headBone").objectReferenceValue = FindDeep(instance.transform, "Head");
            wso.FindProperty("neckBone").objectReferenceValue = FindDeep(instance.transform, "neck_01");
            wso.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder("Assets/Prefabs");
            prefab = PrefabUtility.SaveAsPrefabAsset(instance, WatcherPrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(temp);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Mannequin] Подглядывающий манекен готов (самостоятельный префаб): {WatcherPrefabPath}", prefab);
        return prefab;
    }

    private static GameObject LoadOrBuildWatcher()
    {
        // Собираем, только если префаба нет (или остался старый вариант), чтобы не сбросить настройки из Inspector
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WatcherPrefabPath);
        if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant)
            prefab = BuildWatcherPrefab();
        return prefab;
    }

    // Раньше Watcher собирался как вариант Mannequin — при загрузке редактора переделываем его в самостоятельный
    [InitializeOnLoadMethod]
    private static void AutoConvertWatcherVariant()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WatcherPrefabPath);
            if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Variant) return;
            if (BuildWatcherPrefab() != null)
                Debug.Log("[Mannequin] Mannequin_Watcher переделан из варианта в самостоятельный префаб.");
        };
    }

    /// <summary>
    /// BuildController пересоздаёт Mannequin.controller заново, а самостоятельный Watcher ссылается на него напрямую.
    /// Обновляем ссылку, чтобы подглядывающий не остался без анимации (и позы покоя).
    /// </summary>
    private static void RelinkWatcherController(AnimatorController controller)
    {
        // Самостоятельные префабы (Подглядывающий, Воришка) ссылаются на контроллер напрямую
        foreach (string path in new[] { WatcherPrefabPath, ThiefSetup.ThiefPrefabPath })
        {
            var standalone = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (standalone == null || PrefabUtility.GetPrefabAssetType(standalone) == PrefabAssetType.Variant) continue;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Animator a in contents.GetComponentsInChildren<Animator>(true))
                    a.runtimeAnimatorController = controller;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }

    private static Vector3 PlaceInFrontOfPlayer(GameObject prefab, string undoName, float distance, float extraYaw)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, undoName);

        Vector3 pos = new Vector3(0f, 0f, 7f);
        var playerController = Object.FindAnyObjectByType<GrayboxPlayerController>();
        Transform player = playerController != null ? playerController.transform : null;
        if (player != null) pos = player.position + player.forward * distance;
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 10f, NavMesh.AllAreas)) pos = hit.position;
        instance.transform.position = pos;
        if (player != null)
        {
            Vector3 look = player.position - pos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                instance.transform.rotation = Quaternion.LookRotation(look) * Quaternion.Euler(0f, extraYaw, 0f);
        }

        Selection.activeGameObject = instance;
        SaveScene();
        return pos;
    }

    [MenuItem("Tools/Mannequin/Build Prefab")]
    public static void BuildPrefabMenu() => BuildPrefab();

    public static GameObject BuildPrefab()
    {
        if (!NotPlaying()) return null;

        ConfigureImport();
        AnimatorController controller = BuildController();
        if (controller == null) return null;
        RelinkWatcherController(controller);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Material white = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (white == null)
        {
            white = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Animated Character White" };
            white.SetColor("_BaseColor", Color.white);
            white.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(white, MaterialPath);
        }

        // --- Корень ---
        var root = new GameObject("Mannequin");
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.height = Height;
        capsule.radius = 0.3f;
        capsule.center = new Vector3(0f, Height / 2f, 0f);

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f;
        agent.height = Height;
        agent.speed = 4f;
        agent.angularSpeed = 540f;
        agent.acceleration = 30f;
        agent.autoBraking = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

        // --- Модель ---
        var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        modelInstance.name = "Model";
        modelInstance.transform.SetParent(root.transform, false);

        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = white;
            r.sharedMaterials = mats;
        }
        FaceForward(modelInstance);
        FitHeight(modelInstance, renderers);

        var animator = modelInstance.GetComponent<Animator>();
        if (animator == null) animator = modelInstance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // --- Скрипты ---
        var visibility = root.AddComponent<MannequinVisibility>();
        var anim = root.AddComponent<MannequinAnimator>();
        root.AddComponent<MannequinBrain>();

        var bones = new List<Transform>();
        foreach (string boneName in SightBones)
        {
            Transform bone = FindDeep(modelInstance.transform, boneName);
            if (bone != null) bones.Add(bone);
            else Debug.LogWarning($"[Mannequin] Кость {boneName} не найдена");
        }
        var so = new SerializedObject(visibility);
        var arr = so.FindProperty("sightPoints");
        arr.arraySize = bones.Count;
        for (int i = 0; i < bones.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = bones[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        var aso = new SerializedObject(anim);
        aso.FindProperty("animator").objectReferenceValue = animator;
        aso.FindProperty("headBone").objectReferenceValue = FindDeep(modelInstance.transform, "Head");
        aso.ApplyModifiedPropertiesWithoutUndo();

        // --- Сохранение ---
        EnsureFolder("Assets/Prefabs");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Mannequin] Префаб готов: {PrefabPath}", prefab);
        return prefab;
    }

    [MenuItem("Tools/Mannequin/Setup NavMesh In Scene")]
    public static void SetupNavMesh()
    {
        if (!NotPlaying()) return;
        var surface = Object.FindAnyObjectByType<NavMeshSurface>();
        if (surface == null)
        {
            var go = new GameObject("NavMesh");
            Undo.RegisterCreatedObjectUndo(go, "Create NavMesh");
            surface = go.AddComponent<NavMeshSurface>();
        }
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = ~(1 << LayerMask.NameToLayer("Ignore Raycast")); // игрок не дырявит NavMesh
        if (surface.GetComponent<NavMeshAutoBake>() == null) surface.gameObject.AddComponent<NavMeshAutoBake>();

        surface.BuildNavMesh();
        SaveNavMeshAsset(surface);
        SaveScene();
        Debug.Log("[Mannequin] NavMesh собран. При Play он пересобирается сам (NavMeshAutoBake).");
    }

    // ------------------------------------------------------------------

    private static void ConfigureImport()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (importer == null) { Debug.LogError("Нет модели " + ModelPath); return; }

        bool changed = false;
        if (importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            bool loop = clip.name.Contains("_Loop") || clip.name.EndsWith("Sword_Idle");
            if (clip.loopTime != loop) { clip.loopTime = loop; changed = true; }
        }
        if (importer.clipAnimations == null || importer.clipAnimations.Length == 0) changed = true;

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log("[Mannequin] Импорт модели обновлён: зацикленные клипы + Avatar.");
        }
    }

    private static AnimatorController BuildController()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToList();
        AnimationClip Find(string n) => clips.FirstOrDefault(c => c.name == n || c.name.EndsWith("|" + n));

        EnsureFolder("Assets/Animations");
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        foreach (var (clipName, speed) in Locomotion)
        {
            AnimationClip clip = Find(clipName);
            if (clip != null) tree.AddChild(clip, speed);
            else Debug.LogWarning($"[Mannequin] Клип {clipName} не найден");
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        sm.defaultState = locomotion;
        int row = 0;
        foreach (string clipName in PoseClips)
        {
            AnimationClip clip = Find(clipName);
            if (clip == null) { Debug.LogWarning($"[Mannequin] Клип {clipName} не найден"); continue; }
            AnimatorState state = sm.AddState(clipName, new Vector3(550f, row++ * 55f, 0f));
            state.motion = clip;
        }

        AddRestPoseState(controller, clips);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // ------------------------------------------------------------------
    // Поза покоя: первый кадр Idle_Loop, стоящий на месте (скорость состояния = 0).
    // Добавляется в существующий контроллер без пересборки префаба, чтобы не сбить настройки в Inspector.

    private const string RestPoseState = "RestPose";

    [InitializeOnLoadMethod]
    private static void AutoAddRestPose()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || HasRestPose(controller)) return;
            AddRestPoseMenu();
        };
    }

    [MenuItem("Tools/Mannequin/Add Rest Pose")]
    public static void AddRestPoseMenu()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) { Debug.LogError("[Mannequin] Нет контроллера " + ControllerPath); return; }
        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToList();
        if (AddRestPoseState(controller, clips))
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[Mannequin] В Animator добавлено состояние RestPose (первый кадр Idle_Loop).");
        }
    }

    private static bool HasRestPose(AnimatorController controller)
    {
        return controller.layers[0].stateMachine.states.Any(s => s.state.name == RestPoseState);
    }

    private static bool AddRestPoseState(AnimatorController controller, List<AnimationClip> clips)
    {
        if (HasRestPose(controller)) return false;
        AnimationClip idle = clips.FirstOrDefault(c => c.name == "Idle_Loop" || c.name.EndsWith("|Idle_Loop"));
        if (idle == null) { Debug.LogWarning("[Mannequin] Клип Idle_Loop не найден"); return false; }

        AnimatorState rest = controller.layers[0].stateMachine.AddState(RestPoseState, new Vector3(300f, -80f, 0f));
        rest.motion = idle;
        rest.speed = 0f; // стоит на одном кадре
        return true;
    }

    /// <summary>
    /// Модель в FBX может смотреть назад (-Z), а NavMeshAgent ведёт объект вперёд по +Z.
    /// Определяем, куда смотрят носки стоп, и если назад — разворачиваем модель на 180°.
    /// </summary>
    private static void FaceForward(GameObject model)
    {
        Transform foot = FindDeep(model.transform, "foot_l");
        Transform toe = FindDeep(model.transform, "ball_l");
        if (foot == null || toe == null)
        {
            Debug.LogWarning("[Mannequin] Не нашёл кости стопы — проверь направление модели вручную");
            return;
        }
        Vector3 toeDir = toe.position - foot.position;
        toeDir.y = 0f;
        if (Vector3.Dot(toeDir, Vector3.forward) < 0f)
        {
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
            Debug.Log("[Mannequin] Модель смотрела назад — развернул на 180°.");
        }
    }

    private static void FitHeight(GameObject model, Renderer[] renderers)
    {
        Bounds b = GetBounds(renderers);
        if (b.size.y < 0.001f) return;
        model.transform.localScale *= Height / b.size.y;
        b = GetBounds(renderers);
        model.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
    }

    private static Bounds GetBounds(Renderer[] renderers)
    {
        var b = new Bounds();
        bool has = false;
        foreach (Renderer r in renderers)
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static void SaveNavMeshAsset(NavMeshSurface surface)
    {
        if (surface.navMeshData == null || AssetDatabase.Contains(surface.navMeshData)) return;
        var scene = surface.gameObject.scene;
        string dir = string.IsNullOrEmpty(scene.path) ? "Assets" : System.IO.Path.GetDirectoryName(scene.path).Replace('\\', '/');
        string path = $"{dir}/{(string.IsNullOrEmpty(scene.name) ? "Scene" : scene.name)}_NavMesh.asset";
        AssetDatabase.DeleteAsset(path);
        surface.navMeshData.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(surface);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    private static void SaveScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static bool NotPlaying()
    {
        if (!EditorApplication.isPlaying) return true;
        Debug.LogError("[Mannequin] Сначала останови Play Mode.");
        return false;
    }
}
