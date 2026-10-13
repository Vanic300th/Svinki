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
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class PhotoPersistenceSetup
{
 static T[] Items<T>(Scene s) where T:Component=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static string Plugins()
 {
  AssetDatabase.Refresh(); const string path="Assets/Plugins/SQLite/Windows/x86_64/sqlite3.dll";
  var importer=AssetImporter.GetAtPath(path) as PluginImporter;Need(importer!=null,"Missing native SQLite plugin");
  importer.SetCompatibleWithAnyPlatform(false);importer.SetCompatibleWithEditor(true);
  importer.SetEditorData("OS","Windows");importer.SetEditorData("CPU","x86_64");
  importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64,true);
  importer.SetPlatformData(BuildTarget.StandaloneWindows64,"CPU","x86_64");
  importer.SaveAndReimport();
  return "PASS SQLite DLL limited to Windows x64; macOS uses system SQLite; no game build";
 }
 public static string Mannequins()
 {
  Need(!EditorApplication.isPlayingOrWillChangePlaymode,"Edit mode required");
  var scene=SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
  if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);
  Need(!scene.isDirty,"Save existing scene edits before placing displays");
  var id=typeof(NetworkObject).GetField("SceneId",BindingFlags.Instance|BindingFlags.NonPublic);
  var originals=Items<NetworkObject>(scene).ToDictionary(n=>n,n=>(ulong)id.GetValue(n));
  var source=Items<ThrowableMannequin>(scene).First(m=>m.GetComponent<FixedFactoryDisplay>()==null);
  var level=scene.GetRootGameObjects().Single(g=>g.name=="Graybox Level").transform;
  var group=level.Find("Arrival room AFK displays");if(group==null){group=new GameObject("Arrival room AFK displays").transform;group.SetParent(level,false);}
  Vector3[] points={new Vector3(-6,0,1.5f),new Vector3(6,0,1.5f),new Vector3(-8,0,5),new Vector3(8,0,5),new Vector3(-6,0,7.5f),new Vector3(6,0,7.5f)};
  foreach(var surface in Items<NavMeshSurface>(scene))surface.AddData();Physics.SyncTransforms();
  for(int i=0;i<points.Length;i++)
  {
   string name="Arrival AFK mannequin "+(i+1);var target=group.Find(name);
   if(target==null)
   {
    var copy=UnityEngine.Object.Instantiate(source.gameObject,group);copy.name=name;target=copy.transform;copy.AddComponent<FixedFactoryDisplay>();
    foreach(var n in copy.GetComponentsInChildren<NetworkObject>(true))n.SetSceneId(0);
   }
   Need(NavMesh.SamplePosition(points[i],out var ground,.6f,NavMesh.AllAreas),"Arrival position outside NavMesh: "+points[i]);
   var p=ground.position+Vector3.up*.05f;var r=Quaternion.LookRotation(new Vector3(-p.x,0,-2-p.z));target.SetPositionAndRotation(p,r);
   var body=target.GetComponent<Rigidbody>();body.isKinematic=true;body.position=p;body.rotation=r;
   Need(target.GetComponent<MannequinBrain>()==null&&target.GetComponent<ThiefBrain>()==null&&target.GetComponent<MannequinHeadWatcher>()==null,"AFK source has AI");
  }
  Physics.SyncTransforms();ValidateArrival(scene);
  var create=typeof(NetworkObject).GetMethod("CreateSceneId",BindingFlags.Static|BindingFlags.NonPublic);
  var serialize=typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",BindingFlags.Instance|BindingFlags.NonPublic);
  foreach(var n in (List<NetworkObject>)create.Invoke(null,new object[]{scene,false,0}))serialize.Invoke(n,new object[]{true,false});
  Need(originals.All(p=>(ulong)id.GetValue(p.Key)==p.Value),"Existing network ID changed");
  var ids=Items<NetworkObject>(scene).Select(n=>(ulong)id.GetValue(n)).ToArray();Need(ids.All(n=>n!=0)&&ids.Distinct().Count()==ids.Length,"Invalid scene IDs");
  EditorSceneManager.MarkSceneDirty(scene);Need(EditorSceneManager.SaveScene(scene),"Scene save failed");
  Directory.CreateDirectory("ArtSource/PhotoPersistence");var report="PASS six fixed, carryable AFK displays in the factory arrival room; no hostile AI; clear entrance and lift routes; "+originals.Count+" existing scene IDs preserved.";
  File.WriteAllText("ArtSource/PhotoPersistence/arrival-validation.txt",report);return report;
 }
 public static void ValidateArrival(Scene scene)
 {
  var displays=Items<FixedFactoryDisplay>(scene);Need(displays.Length==6,"Expected six arrival displays");
  foreach(var display in displays)
  {
   var shape=display.GetComponent<CapsuleCollider>();var c=shape.bounds;
   foreach(var hit in Physics.OverlapBox(c.center,c.extents*.85f,Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
    if(hit.gameObject.scene==scene&&!hit.transform.IsChildOf(display.transform))Need(hit.bounds.max.y<.3f,"Display overlaps "+hit.name);
   var path=new NavMeshPath();Need(NavMesh.CalculatePath(new Vector3(0,0,-3),display.transform.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Unreachable arrival display");
  }
  foreach(var route in new[]{new[]{new Vector3(0,1,-3),new Vector3(0,1,-7.7f)},new[]{new Vector3(0,1,-3),new Vector3(0,1,8)}})
  {var d=route[1]-route[0];Need(!scene.GetPhysicsScene().SphereCast(route[0],.45f,d.normalized,out var hit,d.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),"Blocked arrival route: "+hit.collider?.name);}
 }
}
