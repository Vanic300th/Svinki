using System;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class PhotoRoundCheck
{
    public static void Spawns()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);
        var roots=scene.GetRootGameObjects();var transforms=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var targets=transforms.Where(t=>t.GetComponent<ShoppingCart>()!=null||t.GetComponent<ThrowableMannequin>()!=null||t.GetComponent<MannequinBrain>()!=null||t.GetComponent<ThiefBrain>()!=null).ToArray();
        var poses=targets.Select(t=>(t,t.position,t.rotation)).ToArray();GameObject go=null;
        try
        {
            Physics.SyncTransforms();var surface=roots.SelectMany(r=>r.GetComponentsInChildren<NavMeshSurface>(true)).Single();surface.AddData();
            go=new GameObject("Random spawn validation");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);var layout=go.AddComponent<RoundWorldLayout>();
            int total=0;Vector3[] first=null;
            for(int seed=1200;seed<1264;seed++)
            {
                layout.Shuffle(seed);
                for(int i=0;i<6;i++)if(!layout.TryTakePoint("Test monkey "+i,.65f,out _)||!layout.TryTakePoint("Test camera "+i,.45f,out _))throw new Exception("Missing player equipment spawn");
                var points=layout.Placements.Select(p=>p.Position).ToArray();if(seed==1200)first=points;
                if(seed==1201&&points.SequenceEqual(first))throw new Exception("Two round seeds produced the same positions");
                total+=points.Length;
            }
            layout.Shuffle(1200);for(int i=0;i<6;i++){layout.TryTakePoint("Test monkey "+i,.65f,out _);layout.TryTakePoint("Test camera "+i,.45f,out _);}
            if(!layout.Placements.Select(p=>p.Position).SequenceEqual(first))throw new Exception("Seed is not reproducible");
            Directory.CreateDirectory("ArtSource/PhotoRound");
            File.WriteAllText("ArtSource/PhotoRound/spawn-validation.txt","PASS: 64 distinct round seeds, "+total+" clear, reachable placements; deterministic seed replay.\nCarts="+targets.Count(t=>t.GetComponent<ShoppingCart>()!=null)+"; aggressive="+targets.Count(t=>t.GetComponent<MannequinBrain>()!=null)+"; ordinary="+targets.Count(t=>t.GetComponent<ThrowableMannequin>()!=null)+"; thieves="+targets.Count(t=>t.GetComponent<ThiefBrain>()!=null)+"; max 6 monkeys + 6 cameras.\n");
        }
        finally
        {
            foreach(var pose in poses){pose.t.SetPositionAndRotation(pose.position,pose.rotation);if(pose.t.TryGetComponent<Rigidbody>(out var body)){body.position=pose.position;body.rotation=pose.rotation;}}
            if(go!=null)UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.CloseScene(scene,true);
        }
    }
}
