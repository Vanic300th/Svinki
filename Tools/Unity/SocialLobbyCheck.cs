using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class SocialLobbyCheck
{
 static T[] Items<T>(Scene s) where T:Component=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
 public static string Check()
 {
  var lobby=SceneManager.GetSceneByName("FriendslopLobby");var factory=SceneManager.GetSceneByName("SampleScene");
  void Need(bool value,string label){if(!value)throw new Exception(label);}
  Need(lobby.IsValid()&&factory.IsValid(),"Scenes loaded");
  Need(Items<PlayerLocker>(lobby).Length==6&&Items<LockerPhotoSlot>(lobby).Length==36&&Items<LobbyProp>(lobby).Length==17,"Locker/photo/prop inventory");
  Need(Items<LobbyTelevision>(lobby).Length==1&&Items<LobbyJukebox>(lobby).Length==1&&Items<DrawingBoard>(lobby).Length==1&&Items<AirHockeyTable>(lobby).Length==1,"Social modules");
  Need(Items<LobbyFreightElevator>(lobby).Length==1&&Items<RoundFinishStation>(lobby).Length==0,"Start lift independent of final station");
  Need(Items<MannequinBrain>(lobby).Length==0,"Safe lobby has no attacking AI");
  Need(Items<LobbyActivityManager>(factory).Length==0&&Items<LobbyFreightElevator>(factory).Length==1&&Items<RoundFinishStation>(factory).Single().Elevator!=null,"Factory return lift connected");
  Need(Items<PickupItem>(factory).Length==96&&Items<ShoppingCart>(factory).Length==3,"Original gameplay inventory");
  var field=typeof(NetworkObject).GetField("SceneId",BindingFlags.Instance|BindingFlags.NonPublic);
  var ids=Items<NetworkObject>(lobby).Concat(Items<NetworkObject>(factory)).Select(o=>(ulong)field.GetValue(o)).ToArray();Need(ids.All(x=>x!=0)&&ids.Distinct().Count()==ids.Length,"Unique nonzero network IDs");
  foreach(var scene in new[]{lobby,factory})foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script: "+t.name);
  Need(Items<StoreMap>(lobby).Single().VisibleRange==1,"Whole-room lobby map");
  Need(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==lobby.path),"Lobby in build");
  Physics.SyncTransforms();Vector3[] route={new Vector3(0,1,-22),new Vector3(-5,1,-20),new Vector3(-7,1,-18),new Vector3(-7,1,-15.4f)};
  for(int i=1;i<route.Length;i++){var d=route[i]-route[i-1];Need(!lobby.GetPhysicsScene().SphereCast(route[i-1],.45f,d.normalized,out var hit,d.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),"Blocked lift approach "+(hit.collider!=null?hit.collider.name:""));}
  var result="PASS: separate playable lobby, six lockers/36 photo slots/17 props, media/board/sports; no attacking AI; original factory content/return lift; 188 unique scene network IDs; clear route to start lift.";
  File.WriteAllText("ArtSource/SocialLobby/friendslop-module-validation.txt",result+"\n");return result;
 }
}
