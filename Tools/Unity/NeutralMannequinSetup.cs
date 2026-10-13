using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Run in edit mode with SampleScene loaded. Adds displays without rebuilding the level.</summary>
public static class NeutralMannequinSetup
{
    private static T[] Items<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static readonly FieldInfo Id = typeof(NetworkObject).GetField("SceneId", BindingFlags.Instance | BindingFlags.NonPublic);

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit mode required");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        if (!scene.IsValid() || !scene.isLoaded) throw new Exception("Load SampleScene first");
        if (scene.isDirty) throw new Exception("Save pending scene edits first");
        var originals = Items<NetworkObject>(scene).ToDictionary(n => n, n => (ulong)Id.GetValue(n));
        var displays = Items<ThrowableMannequin>(scene);
        if (displays.Length > 40 || Items<MannequinBrain>(scene).Length != 8 || Items<ShoppingCart>(scene).Length != 3)
            throw new Exception("Unexpected existing population");
        var source = displays.First(m => m.name == "Store display expansion 1");
        var parent = scene.GetRootGameObjects().Single(g => g.name == "Graybox Level").transform;
        Transform group = parent.Find("Additional neutral displays");
        if (group == null) { group = new GameObject("Additional neutral displays").transform; group.SetParent(parent, false); }
        for (int i = displays.Length; i < 40; i++)
        {
            var copy = UnityEngine.Object.Instantiate(source.gameObject, group);
            copy.name = "Neutral display " + (i + 1).ToString("00");
            foreach (var n in copy.GetComponentsInChildren<NetworkObject>(true)) n.SetSceneId(0);
            if (copy.GetComponent<MannequinBrain>() != null) throw new Exception("Neutral source attacks");
        }
        var create = typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic);
        var serialize = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var n in (List<NetworkObject>)create.Invoke(null, new object[] { scene, false, 0 }))
            serialize.Invoke(n, new object[] { true, false });
        if (originals.Any(p => (ulong)Id.GetValue(p.Key) != p.Value)) throw new Exception("An existing network ID changed");
        var ids = Items<NetworkObject>(scene).Select(n => (ulong)Id.GetValue(n)).ToArray();
        if (ids.Any(id => id == 0) || ids.Distinct().Count() != ids.Length) throw new Exception("Invalid scene IDs");
        var surfaces = Items<NavMeshSurface>(scene); foreach (var surface in surfaces) surface.AddData();
        GameObject test = new GameObject("Neutral population spawn check"); SceneManager.MoveGameObjectToScene(test, scene);
        try { Physics.SyncTransforms(); test.AddComponent<RoundWorldLayout>().Shuffle(4017); }
        finally { UnityEngine.Object.DestroyImmediate(test); }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
        string report = "PASS: 40 neutral mannequins, 8 aggressive mannequins, 3 carts; " + ids.Length +
            " unique nonzero network IDs; all " + originals.Count + " existing IDs preserved.\n";
        Directory.CreateDirectory("ArtSource/NeutralMannequins");
        File.WriteAllText("ArtSource/NeutralMannequins/scene-validation.txt", report);
        return report;
    }
}
