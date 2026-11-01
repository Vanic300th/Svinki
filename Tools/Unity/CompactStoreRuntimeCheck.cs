var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByName("SampleScene");
if(!UnityEngine.Application.isPlaying||!scene.IsValid()||!scene.isLoaded)throw new System.Exception("Start an offline factory run first");
foreach(var brain in UnityEngine.Object.FindObjectsByType<MannequinBrain>())brain.enabled=false;
foreach(var brain in UnityEngine.Object.FindObjectsByType<ThiefBrain>())brain.enabled=false;
foreach(var agent in UnityEngine.Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>())if(agent.isOnNavMesh)agent.isStopped=true;
var world=UnityEngine.Object.FindAnyObjectByType<RoundWorldLayout>();
var reports=new System.Collections.Generic.List<string>();
foreach(int seed in new[]{231,517,1039,2027})
{
    world.Shuffle(seed);
    if(world.Placements.Count<80)throw new System.Exception("Missing round population");
    reports.Add(seed+": "+world.Placements.Count+" reachable, clear round spawns");
}
var clothes=UnityEngine.Object.FindAnyObjectByType<RoundClothingLayout>();
foreach(int seed in new[]{812,1613,2417,730,731,732,733,734,735,736,737,738})
{
    clothes.Shuffle(seed);
    var failures=StoreRoomDressingSetup.Validate();
    if(failures.Length>0)throw new System.Exception(seed+": "+string.Join("; ",failures));
    var map=UnityEngine.Object.FindAnyObjectByType<StoreMap>();
    if(map.Floors.Length!=30)throw new System.Exception("Expected thirty small rooms");
    reports.Add(seed+": 30 connected small rooms, all 96 garments accessible to pickup ray");
}
System.IO.File.WriteAllLines("ArtSource/SmallStore30/runtime-validation.txt",reports);
return reports.ToArray();
