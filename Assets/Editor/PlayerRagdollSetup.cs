using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PlayerRagdollSetup
{
    [MenuItem("Svinki/Подогнать регдолл свинок")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Сначала остановите Play Mode.");
        foreach (string path in new[] { "Assets/Prefabs/OfflinePlayer.prefab", "Assets/Prefabs/NetworkPlayer.prefab" })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ragdoll = root.GetComponent<PlayerRagdoll>() ?? root.AddComponent<PlayerRagdoll>();
                var appearance = root.GetComponentInChildren<PigAppearance>(true);
                var skin = appearance.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == "Body");
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh);
                    Vector3[] vertices = mesh.vertices;
                    BoneWeight[] weights = skin.sharedMesh.boneWeights;
                    var data = new SerializedObject(ragdoll); var shapes = data.FindProperty("bodyShapes"); shapes.arraySize = 3;
                    string[] names = { "Hips", "Spine", "Head" };
                    for (int b = 0; b < names.Length; b++)
                    {
                        Transform bone = skin.bones.First(t => t.name == names[b]);
                        Bounds bounds = default; bool found = false;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            BoneWeight w = weights[i];
                            bool uses = w.weight0 > .5f && skin.bones[w.boneIndex0] == bone ||
                                w.weight1 > .5f && skin.bones[w.boneIndex1] == bone ||
                                w.weight2 > .5f && skin.bones[w.boneIndex2] == bone ||
                                w.weight3 > .5f && skin.bones[w.boneIndex3] == bone;
                            if (!uses) continue;
                            Vector3 local = bone.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));
                            if (!found) { bounds = new Bounds(local, Vector3.zero); found = true; } else bounds.Encapsulate(local);
                        }
                        if (!found) throw new InvalidOperationException("Нет вершин для " + names[b]);
                        // Include the blend area and give cloth a small clearance from the floor.
                        Vector3 size = bounds.size * 1.04f + Vector3.one * .02f;
                        var item = shapes.GetArrayElementAtIndex(b);
                        item.FindPropertyRelative("Bone").stringValue = names[b];
                        item.FindPropertyRelative("Center").vector3Value = bounds.center;
                        item.FindPropertyRelative("Size").vector3Value = size;
                        Debug.Log($"{path} {names[b]}: center {bounds.center}, size {size}");
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }
}
