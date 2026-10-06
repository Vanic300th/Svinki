using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FishNet.Component.Transforming;
using FishNet.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ThrowableMannequinSetup
{
    [MenuItem("Svinki/Настроить бросаемые манекены")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Сначала остановите Play Mode.");
        foreach (string path in new[] { "Assets/Prefabs/OfflinePlayer.prefab", "Assets/Prefabs/NetworkPlayer.prefab" })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<PlayerMannequinCarry>() == null) root.AddComponent<PlayerMannequinCarry>();
                var fall = root.GetComponent<PlayerKnockdown>() ?? root.AddComponent<PlayerKnockdown>();
                var outfit = root.GetComponent<WorldOutfitRenderer>();
                var data = new SerializedObject(fall);
                data.FindProperty("characterVisual").objectReferenceValue = new SerializedObject(outfit).FindProperty("characterVisual").objectReferenceValue;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        try
        {
            var displays = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Animator>(true))
                .Select(a => a.gameObject).Where(g => g.name == "White Animated Character" || g.name.StartsWith("Store display ", StringComparison.Ordinal))
                .Where(g => g.GetComponent<MannequinBrain>() == null && g.GetComponent<ThiefBrain>() == null && g.GetComponent<MannequinHeadWatcher>() == null).ToArray();
            foreach (GameObject display in displays) Configure(display);
            EditorSceneManager.MarkSceneDirty(scene);
            var create = typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic);
            var serialize = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", BindingFlags.Instance | BindingFlags.NonPublic);
            object[] args = { scene, true, 0 };
            foreach (NetworkObject item in (List<NetworkObject>)create.Invoke(null, args))
                serialize.Invoke(item, new object[] { true, false });
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Throwable mannequins: {displays.Length} displays configured; player prefabs and scene IDs saved.");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    public static void Configure(GameObject root)
    {
        if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
        if (root.GetComponent<ThrowableMannequin>() == null) root.AddComponent<ThrowableMannequin>();
        if (root.GetComponent<NetworkThrowableMannequin>() == null) root.AddComponent<NetworkThrowableMannequin>();
        var nt = root.GetComponent<NetworkTransform>() ?? root.AddComponent<NetworkTransform>();
        var data = new SerializedObject(nt);
        data.FindProperty("_clientAuthoritative").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        GameObjectUtility.SetStaticEditorFlags(root, 0);
        root.layer = 0;
        var body = root.GetComponent<Rigidbody>();
        body.isKinematic = true; body.useGravity = true; body.mass = 8;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var capsule = root.GetComponent<CapsuleCollider>();
        capsule.isTrigger = false; capsule.enabled = true; capsule.direction = 1;
        // The body renderer excludes clothing and gives stable foot/head limits for this rig.
        var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin != null)
        {
            Bounds bounds = skin.localBounds;
            var local = new Bounds(root.transform.InverseTransformPoint(skin.transform.TransformPoint(bounds.center)), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                local.Encapsulate(root.transform.InverseTransformPoint(skin.transform.TransformPoint(corner)));
            }
            capsule.height = Mathf.Max(.6f, local.size.y);
            capsule.center = new Vector3(0, local.center.y, 0);
        }
        else { capsule.height = 1.9f; capsule.center = new Vector3(0, .95f, 0); }
        capsule.radius = .3f;
        var modifier = root.GetComponent<NavMeshModifier>() ?? root.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
    }
}
