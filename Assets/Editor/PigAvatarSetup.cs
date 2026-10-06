using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Idempotent installation through Unity's asset/prefab APIs, never raw YAML edits.</summary>
public static class PigAvatarSetup
{
    private const string Model = "Assets/Art/Characters/Pigs/Pig_Modular.fbx";
    private const string Prefab = "Assets/Resources/Pigs/PigAvatar.prefab";
    private const string Headless = "Assets/Art/Characters/Pigs/Pig_FirstPersonBody.asset";

    [MenuItem("Svinki/Свиньи/Подключить модульного игрока")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        string layer = LayerMask.LayerToName(31);
        if (!string.IsNullOrEmpty(layer) && layer != "PigPreview")
            throw new InvalidOperationException("Preview layer 31 is occupied: " + layer);
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        tags.FindProperty("layers").GetArrayElementAtIndex(31).stringValue = "PigPreview";
        tags.ApplyModifiedPropertiesWithoutUndo();
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false; importer.isReadable = true; importer.importAnimation = false;
        importer.SaveAndReimport();
        Directory.CreateDirectory("Assets/Resources/Pigs");
        AssetDatabase.Refresh();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        if (model == null) throw new InvalidOperationException("Pig model is missing.");
        var root = new GameObject("PigAvatar");
        try
        {
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Pig Model";
            InstallSkinMaterials(visual.transform);
            foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
            { animator.runtimeAnimatorController = null; animator.enabled = false; }
            foreach (SkinnedMeshRenderer renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.updateWhenOffscreen = true;
            var appearance = root.AddComponent<PigAppearance>(); appearance.Bind(visual.transform);
            appearance.Apply(PigFace.Preset(0).Encode());
            root.AddComponent<PigMotion>();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            MakeHeadlessMesh(visual.transform);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        foreach (string path in new[] { "Assets/Prefabs/NetworkPlayer.prefab", "Assets/Prefabs/OfflinePlayer.prefab" })
            InstallPlayer(path);
        AssetDatabase.SaveAssets();
        Debug.Log("Pig avatars installed: independent faces, preview prefab and first-person body.");
    }

    private static void InstallSkinMaterials(Transform visual)
    {
        Shader shader = Shader.Find("Svinki/Pig Skin");
        if (shader == null) throw new InvalidOperationException("Pig Skin shader is missing.");
        const string folder = "Assets/Art/Characters/Pigs/Materials";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material original = materials[i];
                if (original == null || !original.name.StartsWith("Pig_Skin", StringComparison.Ordinal)) continue;
                string path = folder + "/Pig_Skin.mat";
                Material skin = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (skin == null) { skin = new Material(shader) { name = "Pig_Skin" }; AssetDatabase.CreateAsset(skin, path); }
                skin.shader = shader; skin.SetColor("_BaseColor", PigAppearance.SkinColors[0]);
                skin.SetFloat("_Smoothness", .25f); EditorUtility.SetDirty(skin);
                materials[i] = skin;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static void MakeHeadlessMesh(Transform visual)
    {
        var source = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Body");
        Mesh mesh = UnityEngine.Object.Instantiate(source.sharedMesh); mesh.name = "Pig First Person Body";
        Vector3[] vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var kept = Enumerable.Range(0, triangles.Length / 3).Where(t =>
            Enumerable.Range(0, 3).All(i => visual.InverseTransformPoint(source.transform.TransformPoint(vertices[triangles[t * 3 + i]])).y < 1.05f))
            .SelectMany(t => new[] { triangles[t * 3], triangles[t * 3 + 1], triangles[t * 3 + 2] }).ToArray();
        if (kept.Length == 0) throw new InvalidOperationException("Empty first-person pig mesh.");
        mesh.triangles = kept; mesh.RecalculateBounds();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Headless);
        if (existing == null) AssetDatabase.CreateAsset(mesh, Headless);
        else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); }
    }

    public static void RefreshPlayerAnimation()
    {
        foreach (string path in new[] { "Assets/Prefabs/NetworkPlayer.prefab", "Assets/Prefabs/OfflinePlayer.prefab" }) InstallPlayer(path);
        AssetDatabase.SaveAssets();
    }

    private static void InstallLegacyAnimation(GameObject root, Transform pig, PlayerAnimation animation)
    {
        Transform driver = root.transform.Find("Legacy Animation Driver");
        if (driver == null)
        {
            Transform backup = root.transform.Find("Character Visual Human Backup");
            if (backup == null) throw new InvalidOperationException("Missing previous animation rig.");
            driver = UnityEngine.Object.Instantiate(backup.gameObject, root.transform).transform;
            driver.name = "Legacy Animation Driver";
        }
        driver.gameObject.SetActive(true);
        foreach (Renderer r in driver.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (Collider c in driver.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (Light l in driver.GetComponentsInChildren<Light>(true)) l.enabled = false;
        Animator animator = driver.GetComponentInChildren<Animator>(true);
        animator.enabled = true; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player.controller");
        AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/UAL1_Standard.fbx").OfType<AnimationClip>().First(c => c.name.EndsWith("|Idle_Loop", StringComparison.Ordinal));
        idle.SampleAnimation(animator.gameObject, 0);
        pig.GetComponent<PigMotion>().BindLegacyAnimator(animator);
        var data = new SerializedObject(animation); data.FindProperty("animator").objectReferenceValue = animator; data.ApplyModifiedPropertiesWithoutUndo();
        animation.enabled = true;
    }

    private static void InstallPlayer(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform pig = root.transform.Find("Character Visual Pig");
            if (pig == null)
            {
                var original = root.transform.Find("Character Visual");
                if (original != null) { original.name = "Character Visual Human Backup"; original.gameObject.SetActive(false); }
                var avatar = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
                pig = ((GameObject)PrefabUtility.InstantiatePrefab(avatar, root.transform)).transform;
                pig.name = "Character Visual Pig";
            }
            pig.SetSiblingIndex(0); pig.gameObject.SetActive(true);
            pig.localPosition = Vector3.zero; pig.localRotation = Quaternion.identity; pig.localScale = Vector3.one;
            var outfit = root.GetComponent<WorldOutfitRenderer>();
            if (outfit != null)
            {
                var data = new SerializedObject(outfit); data.FindProperty("characterVisual").objectReferenceValue = pig;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var animation = root.GetComponent<PlayerAnimation>();
            if (animation != null) InstallLegacyAnimation(root, pig, animation);
            var fps = root.GetComponent<FirstPersonBody>();
            if (fps == null) fps = root.AddComponent<FirstPersonBody>();
            var renderers = pig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var firstPerson = new SerializedObject(fps);
            firstPerson.FindProperty("source").objectReferenceValue = renderers.First(r => r.name == "Body");
            firstPerson.FindProperty("bodyMesh").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Mesh>(Headless);
            var parts = renderers.Where(r => r.name.StartsWith("Hoof_", StringComparison.Ordinal)).ToArray();
            var extras = firstPerson.FindProperty("additionalSources"); extras.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++) extras.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
            firstPerson.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
