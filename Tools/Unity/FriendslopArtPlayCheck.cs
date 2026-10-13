using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// Real physics check for replacement art; use the normal offline session, never a live room.
public static class FriendslopArtPlayCheck
{
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static T[] Items<T>(Scene s) where T:Component=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
 public static async Task<string> Check()
 {
  Need(EditorApplication.isPlaying&&NetworkLobby.Instance?.Offline==true,"Start a normal offline lobby session first");
  var player=PlayerRegistry.Players.Single(p=>p!=null&&p.IsLocal);
  var scene=player.gameObject.scene;Need(scene.name=="FriendslopLobby","Social lobby required");
  var controller=player.GetComponent<GrayboxPlayerController>();var character=player.GetComponent<CharacterController>();
  var camera=player.EyeCamera;var follow=camera.GetComponent<GrayboxFirstPersonCamera>();
  bool controllerEnabled=controller.enabled,characterEnabled=character.enabled,followEnabled=follow.enabled;
  var home=player.transform.position;var rotation=player.transform.rotation;var eye=camera.transform.position;var eyeRotation=camera.transform.rotation;
  var props=Items<LobbyProp>(scene);string report="";
  try
  {
   controller.enabled=false;character.enabled=false;follow.enabled=false;
   void Take(LobbyProp prop)
   {
    var p=prop.transform.position;
    bool reachable=false;
    foreach(var approach in new[]{Vector3.back,Vector3.left,Vector3.forward,Vector3.right})
    {
     player.transform.position=p+approach*1.4f+Vector3.up*(-p.y+.05f);
     camera.transform.position=p+approach*1.4f+Vector3.up*.6f;camera.transform.LookAt(p);Physics.SyncTransforms();
     if(prop.Validate(player)){reachable=true;break;}
    }
    Need(reachable,"Pickup validation failed from all sides: "+prop.name);
    prop.Request(player,LobbyAction.Toggle);Need(prop.Holder==player,"Cannot pick up: "+prop.name);
    Need(prop.Release(true)&&prop.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude>1,"Cannot throw: "+prop.name);
   }
   foreach(var prop in props.Where(p=>p.Kind!=LobbyPropKind.Puck)){Take(prop);prop.ResetHome();}
   report+="PASS 16 replacement props: validated pickup, throw and reset\n";
   var hoop=Items<BasketballHoop>(scene).Single();var ball=props.First(p=>p.Kind==LobbyPropKind.Basketball);var body=ball.GetComponent<Rigidbody>();
   int before=hoop.Score.Last;Take(ball);
   ball.transform.position=body.position=hoop.transform.TransformPoint(new Vector3(0,3.6f,-.35f));body.linearVelocity=Vector3.down*2;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();
   await Task.Delay(950);Need(hoop.Score.Last==before+1,"Real downward hoop crossing did not score: ball="+body.position+" local="+hoop.transform.InverseTransformPoint(body.position)+" velocity="+body.linearVelocity+" authority="+hoop.Authority+" canScore="+ball.CanScore);ball.ResetHome();
   Take(ball);ball.transform.position=body.position=hoop.transform.TransformPoint(new Vector3(0,2.05f,-.35f));body.linearVelocity=Vector3.up*8;Physics.SyncTransforms();
   await Task.Delay(250);Need(hoop.Score.Last==before+1,"Upward hoop crossing incorrectly scored");ball.ResetHome();
   report+="PASS real hoop triggers: downward pass scores once, upward pass does not\n";
   var board=Items<Dartboard>(scene).Single();var dart=props.First(p=>p.Kind==LobbyPropKind.Dart);Take(dart);var dartBody=dart.GetComponent<Rigidbody>();
   dart.transform.position=dartBody.position=board.transform.position-board.transform.forward*.65f;
   dart.transform.rotation=dartBody.rotation=Quaternion.LookRotation(board.transform.forward);dartBody.linearVelocity=board.transform.forward*12;Physics.SyncTransforms();
   await Task.Delay(300);Need(board.Score.Last>0&&dartBody.isKinematic,"Real dart collision did not score/stick");
   board.Apply(player,LobbyAction.Reset,"",0,null);Need(!dartBody.isKinematic&&!dart.CanScore,"Dart reset failed");
   report+="PASS real dart collision: score, stick and return\n";
   var tv=Items<LobbyTelevision>(scene).Single();var settings=new SerializedObject(tv);var screen=settings.FindProperty("screen").objectReferenceValue as Renderer;
   Need(screen!=null&&screen.enabled&&screen.material.mainTexture is RenderTexture,"TV screen lost its live RenderTexture");
   Need(tv.GetComponent<VideoPlayer>()!=null,"TV lost local VideoPlayer");report+="PASS TV model retains live screen and local VideoPlayer\n";
   Directory.CreateDirectory("ArtSource/IndustrialCC0");File.WriteAllText("ArtSource/IndustrialCC0/play-validation.txt",report);return report;
  }
  finally
  {
   foreach(var prop in props)prop.ResetHome();player.transform.SetPositionAndRotation(home,rotation);camera.transform.SetPositionAndRotation(eye,eyeRotation);
   follow.enabled=followEnabled;character.enabled=characterEnabled;controller.enabled=controllerEnabled;
  }
 }
}
