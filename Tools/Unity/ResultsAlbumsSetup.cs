using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ResultsAlbumsSetup
{
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode is required");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Single);
        var brains=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MannequinBrain>(true))
            .OrderBy(b=>b.name,StringComparer.Ordinal).ToArray();
        if(brains.Length<8)throw new Exception("Expected at least eight aggressive mannequins");
        foreach(var brain in brains.Skip(8))UnityEngine.Object.DestroyImmediate(brain.gameObject);
        var roots=scene.GetRootGameObjects();
        int count=roots.Sum(r=>r.GetComponentsInChildren<MannequinBrain>(true).Length);
        var idField=typeof(FishNet.Object.NetworkObject).GetField("SceneId",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var ids=roots.SelectMany(r=>r.GetComponentsInChildren<FishNet.Object.NetworkObject>(true)).Select(n=>(ulong)idField.GetValue(n)).ToArray();
        if(count!=8 || ids.Any(id=>id==0) || ids.Distinct().Count()!=ids.Length)throw new Exception("Invalid mannequin count or scene network IDs");
        if(roots.Sum(r=>r.GetComponentsInChildren<ShoppingCart>(true).Length)!=3)throw new Exception("Cart count changed");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("ArtSource/ResultsAlbums");
        File.WriteAllText("ArtSource/ResultsAlbums/scene-validation.txt","PASS: 8 aggressive mannequins, 3 carts, "+ids.Length+" unique nonzero network IDs.\n");
        var result=ExpandedRoundBuild.MacImmediate();
        File.WriteAllText("ArtSource/ResultsAlbums/build-status.txt",result);
        if(!result.StartsWith("Succeeded"))throw new Exception(result);
    }
}
