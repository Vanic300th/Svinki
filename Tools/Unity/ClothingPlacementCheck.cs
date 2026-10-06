using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class ClothingPlacementCheck
{
    public static object Main()
    {
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        var clothes=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ClothingPickup>(true)).ToArray();
        Physics.SyncTransforms();
        var physics=scene.GetPhysicsScene();
        var results=clothes.Select(c=>
        {
            var collider=c.GetComponent<BoxCollider>();
            bool available=c.GetComponent<PickupItem>().IsAvailable;
            bool reachable=false;
            Vector3 target=collider.bounds.center;
            if(available)
                for(int direction=0;direction<16&&!reachable;direction++)
                foreach(float distance in new[]{1.1f,1.6f,2.2f})
                {
                    float angle=direction*Mathf.PI/8;
                    var desired=c.transform.position+new Vector3(Mathf.Cos(angle)*distance,0,Mathf.Sin(angle)*distance);
                    if(!NavMesh.SamplePosition(desired,out NavMeshHit floor,.45f,NavMesh.AllAreas))continue;
                    foreach(float eyeHeight in new[]{.72f,1.55f})
                    {
                        Vector3 eye=floor.position+Vector3.up*eyeHeight;
                        Vector3 ray=target-eye;
                        if(ray.magnitude>3.5f)continue;
                        if(physics.Raycast(eye,ray.normalized,out RaycastHit hit,ray.magnitude+.02f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide)
                            &&hit.collider==collider){reachable=true;break;}
                    }
                }
            return new {c.name,available,reachable,rigged=c.Clothing.PigRigged,
                sceneId=(ulong)typeof(FishNet.Object.NetworkObject).GetField("SceneId",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(c.GetComponent<FishNet.Object.NetworkObject>()),
                meshes=c.GetComponentsInChildren<MeshRenderer>(true).Length,sprites=c.GetComponentsInChildren<SpriteRenderer>(true).Length};
        }).ToArray();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var catalog=new SerializedObject(prefab.GetComponent<NetworkPlayer>()).FindProperty("clothingCatalog");
        var definitions=clothes.Select(c=>c.Clothing).Distinct().ToArray();
        bool registered=definitions.All(d=>Enumerable.Range(0,catalog.arraySize).Any(i=>catalog.GetArrayElementAtIndex(i).objectReferenceValue==d));
        var report=new {count=clothes.Length,variants=definitions.Length,registered,
            old=results.Count(r=>!r.rigged),unreachable=results.Where(r=>r.available&&!r.reachable).Select(r=>r.name).ToArray(),
            uniqueSceneIds=results.Select(r=>r.sceneId).Distinct().Count(),zeroSceneIds=results.Count(r=>r.sceneId==0),results};
        Directory.CreateDirectory("ArtSource/Clothing3D");
        File.WriteAllText("ArtSource/Clothing3D/reachability.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
