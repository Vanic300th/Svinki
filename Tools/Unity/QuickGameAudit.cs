using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FishNet.Managing;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Editor regression only: normal offline flow and real local FishNet host.
// Extra server avatar checks consensus; it is not a separate remote client test.
public static class QuickGameAudit
{
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static string report="";
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static void Pass(string text){report+="PASS "+text+"\n";Directory.CreateDirectory("ArtSource/BugAudit");File.AppendAllText("ArtSource/BugAudit/quick-game-validation.txt","PASS "+text+"\n");}
 static async Task Until(Func<bool> test,string message,int timeout=45000)
 {var end=DateTime.UtcNow.AddMilliseconds(timeout);while(!test()&&DateTime.UtcNow<end)await Task.Delay(100);Need(test(),message);}
 static PlayerAvatar Local()=>PlayerRegistry.Players.FirstOrDefault(p=>p!=null&&p.IsLocal);
 static void Move(PlayerAvatar p,Vector3 position)
 {var c=p.GetComponent<CharacterController>();bool active=c!=null&&c.enabled;if(c!=null)c.enabled=false;p.transform.position=position;Physics.SyncTransforms();if(c!=null)c.enabled=active;}
 static LobbyFreightElevator Lift()=>Local().gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LobbyFreightElevator>(true)).Single();
 static void Calm(){foreach(var b in UnityEngine.Object.FindObjectsByType<MannequinBrain>())b.enabled=false;foreach(var b in UnityEngine.Object.FindObjectsByType<ThiefBrain>())b.enabled=false;}
 static void CheckRuntime()
 {
  Need(UnityEngine.Object.FindObjectsByType<EventSystem>().Count(e=>e.isActiveAndEnabled)==1,"Multiple active EventSystems");
  foreach(var s in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
   foreach(var root in s.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
    Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing component: "+t.name);
 }
 public static string ClothingReferences()
 {
  Need(!EditorApplication.isPlaying,"Edit mode required");
  int definitions=0,prefabs=0;
  foreach(var guid in AssetDatabase.FindAssets("t:ClothingDefinition",new[]{SvinkiAssetPaths.ClothingDefinitions}))
  {
   string path=AssetDatabase.GUIDToAssetPath(guid);var item=AssetDatabase.LoadAssetAtPath<ClothingDefinition>(path);
   Need(item!=null&&item.Model!=null,"Missing clothing model: "+path);definitions++;
  }
  foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{SvinkiAssetPaths.ClothingPickups}))
  {
   string path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
   try{foreach(var t in root.GetComponentsInChildren<Transform>(true))Need(PrefabUtility.GetPrefabInstanceStatus(t.gameObject)!=PrefabInstanceStatus.MissingAsset,"Missing nested clothing prefab: "+path);prefabs++;}
   finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  var scene=SceneManager.GetSceneByName("SampleScene");Need(scene.IsValid(),"Open factory scene first");
  foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
   Need(PrefabUtility.GetPrefabInstanceStatus(t.gameObject)!=PrefabInstanceStatus.MissingAsset,"Missing scene prefab: "+t.name);
  Pass("factory has no missing prefab sources; "+definitions+" clothing definitions have models; "+prefabs+" clothing pickup prefabs have no missing nested sources");return report;
 }
 public static async Task<string> OfflineReturn()
 {
  var lobby=NetworkLobby.Instance;Need(EditorApplication.isPlaying&&lobby?.Offline==true&&Local()?.gameObject.scene.name=="SampleScene","Run after factory arrival check");
  report="";Calm();int saved=PhotoAlbum.Instance.SavedCount;CheckRuntime();
  Need(UnityEngine.Object.FindObjectsByType<PickupItem>().Length==96,"Factory pickup inventory changed");
  Need(UnityEngine.Object.FindObjectsByType<ShoppingCart>().Length==3,"Factory carts changed");
  var player=Local();var lift=Lift();var outside=player.Position;
  Move(player,lift.transform.TransformPoint(new Vector3(0,.05f,0)));await Task.Delay(1100);Need(lift.State.Closing&&lobby.Results==null,"Return countdown did not start");
  Move(player,outside);await Task.Delay(3200);Need(!lift.State.Closing&&lobby.Results==null,"Exit did not cancel return lift");
  Move(player,lift.transform.TransformPoint(new Vector3(0,.05f,0)));await Until(()=>lobby.Results!=null,"Return lift did not show results",15000);
  Need(!lobby.InputAllowed&&lobby.Results.entries.Length==1,"Results input or participants wrong");Pass("offline factory inventory, return-lift cancellation, final show and input lock");
  lobby.ContinueAfterResults();await Until(()=>!lobby.Busy&&Local()!=null&&Local().gameObject.scene.name==NetworkLobby.FriendslopSceneName,"Results did not return to lobby");
  CheckRuntime();Need(lobby.Results==null&&lobby.InputAllowed&&PhotoAlbum.Instance.SavedCount==saved,"Return lost album or input remained locked");
  Need(!SceneManager.GetSceneByName("SampleScene").IsValid(),"Factory scene leaked after return");Pass("offline return restores playable social lobby, retains permanent album, unloads factory, one EventSystem and no missing scripts");
  return report;
 }
 public static async Task<string> HockeyLease()
 {
  var lobby=NetworkLobby.Instance;var p=Local();Need(EditorApplication.isPlaying&&p?.gameObject.scene.name==NetworkLobby.FriendslopSceneName,"Social lobby required");
  var table=UnityEngine.Object.FindAnyObjectByType<AirHockeyTable>();var original=p.Position;
  try
  {
   Move(p,table.transform.position+Vector3.back*2+Vector3.down*table.transform.position.y+Vector3.up*.05f);await Task.Delay(150);
   Need(table.Validate(p),"Cannot approach hockey table");LobbyPanel.Open(table,p);await Task.Delay(300);
   Need(table.Status.Contains(lobby.Nickname),"Paddle was not assigned");
   Move(p,table.transform.position+Vector3.back*8+Vector3.down*table.transform.position.y+Vector3.up*.05f);await Task.Delay(650);
   Need(!table.Status.Contains(lobby.Nickname),"BUG: walking away closes hockey UI but keeps paddle assigned");
   Pass("hockey releases paddle when player walks beyond the panel range");return table.Status;
  }
  finally{Move(p,original);table.Apply(p,LobbyAction.Reset,"",0,null);}
 }
 public static async Task<string> HostFlow()
 {
  var lobby=NetworkLobby.Instance;Need(EditorApplication.isPlaying&&lobby!=null,"Session manager required");
  string checkpoint=Path.Combine(Application.persistentDataPath,"round-checkpoint.json");byte[] previous=File.Exists(checkpoint)?File.ReadAllBytes(checkpoint):null;
  NetworkObject extra=null;IDictionary members=null;string extraId="quick-audit-peer";
  try
  {
   if(lobby.Offline||lobby.InSession){lobby.Leave();await Until(()=>!lobby.Offline&&!lobby.InSession&&!lobby.Busy,"Could not leave test session");}
   lobby.DebugHost();await Until(()=>lobby.IsHost&&lobby.Snapshot.phase==SessionPhase.Lobby&&Local()!=null,"Local FishNet host did not load social lobby");
   await Task.Delay(500);CheckRuntime();var p=Local();var origin=p.Position;
   Need(UnityEngine.Object.FindObjectsByType<LobbyProp>().Length==17,"Social props changed");
   Need(UnityEngine.Object.FindObjectsByType<PlayerLocker>().Length==6,"Lockers changed");
   // Owned RPC must reject a distant target and NaN commands.
   var board=UnityEngine.Object.FindAnyObjectByType<DrawingBoard>();int count=board.CommandCount;
   board.Request(p,LobbyAction.Stroke,"draw",3,new[]{new Vector2(.2f,.2f),new Vector2(.4f,.4f)});await Task.Delay(300);
   Need(board.CommandCount==count,"Distant drawing request was accepted");
   Move(p,board.transform.position-board.transform.forward*1.5f+Vector3.down*board.transform.position.y+Vector3.up*.05f);await Task.Delay(100);
   Need(board.Validate(p),"Board unreachable");board.Request(p,LobbyAction.Stroke,"draw",3,new[]{new Vector2(float.NaN,.2f)});await Task.Delay(300);Need(board.CommandCount==count,"NaN stroke was accepted");
   board.Request(p,LobbyAction.Stroke,"draw",3,new[]{new Vector2(.2f,.2f),new Vector2(.4f,.4f)});await Until(()=>board.CommandCount==count+1,"Valid RPC drawing did not apply",5000);
   await Task.Delay(200);board.Request(p,LobbyAction.ClearBoard);await Until(()=>board.CommandCount==0,"Host board clear failed",5000);Move(p,origin);
   Pass("FishNet host spawns lobby and validates distance/NaN drawing through real owned RPC; host clear works");
   await HockeyLease();
   members=(IDictionary)typeof(NetworkLobby).GetField("members",Flags).GetValue(lobby);var real=members[lobby.Identity];var member=Activator.CreateInstance(real.GetType(),true);
   void Set(string name,object value)=>member.GetType().GetField(name,Flags).SetValue(member,value);
   Set("Id",extraId);Set("Name","Audit peer");Set("Connection",real.GetType().GetField("Connection",Flags).GetValue(real));Set("ColorSlot",1);
   var prefab=(NetworkObject)typeof(NetworkLobby).GetField("playerPrefab",Flags).GetValue(lobby);var manager=lobby.GetComponent<NetworkManager>();
   extra=UnityEngine.Object.Instantiate(prefab,origin+Vector3.right*2,Quaternion.identity);extra.GetComponent<NetworkPlayer>().SetIdentity(extraId,"Audit peer");manager.ServerManager.Spawn(extra,null,p.gameObject.scene);Set("Avatar",extra);members.Add(extraId,member);typeof(NetworkLobby).GetMethod("Publish",Flags).Invoke(lobby,null);
   var lift=Lift();lobby.Ready(true);lobby.StartRound();await Task.Delay(700);Need(lobby.Snapshot.phase==SessionPhase.Lobby,"Legacy start bypasses lift");
   Move(p,lift.transform.TransformPoint(new Vector3(-.7f,.05f,0)));await Task.Delay(1000);Need(!lift.State.Closing,"One of two players started countdown");
   Move(extra.GetComponent<PlayerAvatar>(),lift.transform.TransformPoint(new Vector3(.7f,.05f,0)));await Task.Delay(1100);Need(lift.State.Closing,"Both avatars did not start countdown");
   Move(extra.GetComponent<PlayerAvatar>(),origin);await Task.Delay(3300);Need(lobby.Snapshot.phase==SessionPhase.Lobby&&!lift.State.Closing,"Exit failed to cancel start countdown");
   Pass("server start lift requires both avatars, rejects legacy Ready/Start and cancels when one exits");
   Move(extra.GetComponent<PlayerAvatar>(),lift.transform.TransformPoint(new Vector3(.7f,.05f,0)));
   await Until(()=>lobby.Snapshot.phase==SessionPhase.Round&&Local()!=null&&Local().gameObject.scene.name=="SampleScene","Host lift did not start factory");await Task.Delay(600);Calm();CheckRuntime();
   members=(IDictionary)typeof(NetworkLobby).GetField("members",Flags).GetValue(lobby);member=members[extraId];extra=(NetworkObject)member.GetType().GetField("Avatar",Flags).GetValue(member);// The fixture has no second real connection, hence no FishNet presence event for its avatar.
   if(extra==null){extra=UnityEngine.Object.Instantiate(prefab,new Vector3(2,.05f,-3),Quaternion.identity);extra.GetComponent<NetworkPlayer>().SetIdentity(extraId,"Audit peer");manager.ServerManager.Spawn(extra,null,Local().gameObject.scene);member.GetType().GetField("Avatar",Flags).SetValue(member,extra);typeof(NetworkLobby).GetMethod("Publish",Flags).Invoke(lobby,null);}
   p=Local();lift=Lift();origin=p.Position;Move(p,lift.transform.TransformPoint(new Vector3(-.7f,.05f,0)));await Task.Delay(3300);Need(lobby.Results==null,"One player ended two-person run");
   Move(extra.GetComponent<PlayerAvatar>(),lift.transform.TransformPoint(new Vector3(.7f,.05f,0)));await Until(()=>lobby.Snapshot.phase==SessionPhase.Results,"Host return lift did not finish",15000);
   Need(lobby.Results.entries.Length==2&&!lobby.InputAllowed,"Results omitted participant or input not locked");Pass("host loads factory and server return-lift consensus includes both participants in final show (synthetic peer respawned by fixture)");
   lobby.ContinueAfterResults();await Until(()=>lobby.Snapshot.phase==SessionPhase.Lobby&&Local()!=null&&Local().gameObject.scene.name==NetworkLobby.FriendslopSceneName&&!lobby.Busy,"Host results did not return to lobby");CheckRuntime();Need(lobby.Results==null&&!SceneManager.GetSceneByName("SampleScene").IsValid(),"Host return leaked factory/results");Pass("host return restores lobby, unloads factory and results, one EventSystem, no missing scripts");
   return report+"Remote client/EOS internet/Windows build not exercised.";
  }
  finally
  {
   lobby.Leave();await Until(()=>!lobby.InSession&&!lobby.Offline&&!lobby.Busy,"Audit cleanup failed");
   if(previous==null){if(File.Exists(checkpoint))File.Delete(checkpoint);}else File.WriteAllBytes(checkpoint,previous);
   lobby.StartOffline();await Until(()=>lobby.Offline&&!lobby.Busy&&Local()!=null,"Could not restore offline lobby");
  }
 }
}
