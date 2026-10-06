#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PlayerBodySetup
{
    [MenuItem("Svinki/Configure first person body")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        if (LayerMask.NameToLayer("FirstPersonBody") < 0)
        {
            int free = -1;
            for (int i = 10; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { free = i; break; }
            if (free < 0) throw new System.InvalidOperationException("No free layer for first person body.");
            layers.GetArrayElementAtIndex(free).stringValue = "FirstPersonBody";
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
        const string modelPath = "Assets/Models/UAL1_Standard.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        bool wasReadable = importer.isReadable;
        if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
            var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var mesh = Object.Instantiate(skin.sharedMesh);
            mesh.name = "Player first person body";
            var weights = mesh.boneWeights;
            var hiddenBones = new HashSet<int>();
            for (int i = 0; i < skin.bones.Length; i++)
                if (skin.bones[i].name.ToLowerInvariant().Contains("head") || skin.bones[i].name.ToLowerInvariant().Contains("neck")) hiddenBones.Add(i);
            var hidden = new bool[mesh.vertexCount];
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                float weight = (hiddenBones.Contains(w.boneIndex0) ? w.weight0 : 0f) +
                    (hiddenBones.Contains(w.boneIndex1) ? w.weight1 : 0f) +
                    (hiddenBones.Contains(w.boneIndex2) ? w.weight2 : 0f) +
                    (hiddenBones.Contains(w.boneIndex3) ? w.weight3 : 0f);
                hidden[i] = weight > .25f;
            }
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var original = mesh.GetTriangles(submesh);
                var triangles = new List<int>();
                for (int i = 0; i < original.Length; i += 3)
                    if (!hidden[original[i]] && !hidden[original[i + 1]] && !hidden[original[i + 2]])
                    { triangles.Add(original[i]); triangles.Add(original[i + 1]); triangles.Add(original[i + 2]); }
                mesh.SetTriangles(triangles, submesh);
            }
            const string meshPath = "Assets/Models/PlayerFirstPersonBody.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            foreach (string path in new[] { "Assets/Prefabs/NetworkPlayer.prefab", "Assets/Prefabs/OfflinePlayer.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var body = root.GetComponent<FirstPersonBody>() ?? root.AddComponent<FirstPersonBody>();
                    var settings = new SerializedObject(body);
                    settings.FindProperty("source").objectReferenceValue = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    settings.FindProperty("bodyMesh").objectReferenceValue = mesh;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }
        finally { if (!wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); } }
    }
}
#endif
