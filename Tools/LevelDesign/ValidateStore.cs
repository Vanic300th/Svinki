using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using FishNet.Object;
using Newtonsoft.Json.Linq;

public static class ValidateStore
{
    public static string Validate()
    {
        var root=GameObject.Find("Graybox Level");
        if(root==null)throw new Exception("Level not loaded");
        NavMesh.SamplePosition(new Vector3(0,0,-3),out var start,1,NavMesh.AllAreas);
        var spec=JObject.Parse(File.ReadAllText("Docs/LevelDesign/PeachStoreLayout.json"));
        var paths=((JArray)spec["pickups"]).Select((p,i)=>
        {
            var pos=new Vector3((float)p["x"],(float)p["y"],(float)p["z"]);
            bool sampled=NavMesh.SamplePosition(pos,out var end,1.7f,NavMesh.AllAreas);
            var path=new NavMeshPath();
            bool found=sampled&&NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path);
            return new {index=i+1,kind=(string)p["kind"],position=pos.ToString(),sampled,complete=found&&path.status==NavMeshPathStatus.PathComplete};
        }).ToArray();
        var agents=root.GetComponentsInChildren<NavMeshAgent>(true).Select(a=>new {a.name,sampled=NavMesh.SamplePosition(a.transform.position,out var hit,.6f,NavMesh.AllAreas)}).ToArray();
        var ids=root.GetComponentsInChildren<NetworkObject>(true).Select(n=>new SerializedObject(n).FindProperty("SceneId").ulongValue).ToArray();
        var result=new {
            scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path,
            pickups=root.GetComponentsInChildren<ClothingPickup>(true).Length,
            stalkers=root.GetComponentsInChildren<MannequinBrain>(true).Length,
            watchers=root.GetComponentsInChildren<MannequinHeadWatcher>(true).Length,
            thieves=root.GetComponentsInChildren<ThiefBrain>(true).Length,
            displayMannequins=root.GetComponentsInChildren<Animator>(true).Count(a=>a.name=="White Animated Character"||a.name.StartsWith("Store display")),
            networkObjects=ids.Length,uniqueNetworkIds=ids.Distinct().Count(),unsetNetworkIds=ids.Count(i=>i==0),
            missingScripts=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
            reachablePickupLocations=paths.Count(p=>p.complete),pickupLocations=paths.Length,failedLocations=paths.Where(p=>!p.complete).ToArray(),
            agentStarts=agents,posterExists=GameObject.Find("The Peach - original generated illustration")!=null
        };
        string json=Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("Docs/LevelDesign/Validation.json",json+"\n");
        return json;
    }
}
