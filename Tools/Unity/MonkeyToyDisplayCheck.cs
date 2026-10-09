using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
public static class MonkeyToyDisplayCheck
{
 public static string Start(){PlayerRegistry.Players.First(p=>p.IsLocal).StartCoroutine(Check());return "Normal mannequin / visual check running";}
 private static IEnumerator Check()
 {
  yield return new WaitForSecondsRealtime(1);
  var player=PlayerRegistry.Players.First(p=>p.IsLocal);var toy=MonkeyToy.All.Single();
  var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=toy.transform.position+Vector3.back*.8f;cc.enabled=true;
  yield return null;
  if(!toy.TryGrab(player))throw new Exception("Cannot grab fresh round toy");
  cc.enabled=false;player.transform.position=new Vector3(0,.05f,5);cc.enabled=true;
  var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var view=player.EyeCamera.GetComponent<GrayboxFirstPersonCamera>();
  typeof(GrayboxFirstPersonCamera).GetField("yaw",flags).SetValue(view,0f);typeof(GrayboxFirstPersonCamera).GetField("pitch",flags).SetValue(view,0f);
  yield return new WaitForSecondsRealtime(1.5f);
  ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/MonkeyToy/held-forward.png"));
  yield return null;
  foreach(var brain in UnityEngine.Object.FindObjectsByType<MannequinBrain>()){brain.enabled=false;foreach(var c in brain.GetComponentsInChildren<Collider>())c.enabled=false;}
  foreach(var proxy in UnityEngine.Object.FindObjectsByType<RagdollColliderOwner>())if(proxy.Owner?.GetComponent<MannequinBrain>()!=null)foreach(var c in proxy.GetComponentsInChildren<Collider>())c.enabled=false;
  var display=UnityEngine.Object.FindObjectsByType<ThrowableMannequin>().First(m=>m.gameObject.scene==player.gameObject.scene&&!m.IsHeld);
  var body=display.GetComponent<Rigidbody>();body.position=new Vector3(0,0,6.4f);body.rotation=Quaternion.identity;
  yield return new WaitForSecondsRealtime(.2f);
  toy.Swing();yield return new WaitForSecondsRealtime(.9f);
  try
  {
   if(toy!=null||MonkeyToy.All.Count!=0||player.GetComponent<PlayerMonkeyCarry>().Held!=null)throw new Exception("Normal mannequin hit did not consume toy");
   if(!display.GetComponent<CapsuleCollider>().enabled)throw new Exception("Display hit collider was disabled");
   File.AppendAllText("ArtSource/MonkeyToy/validation.txt","PASS: ordinary display mannequin also consumes toy after one actual swing hit\n");UnityEditor.SessionState.SetString("Svinki.Monkey.Display","PASS");
  }catch(Exception e){UnityEditor.SessionState.SetString("Svinki.Monkey.Display","FAIL "+e.Message);Debug.LogException(e);}
 }
}
