using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public static class MonkeyCalmCheck
{
 public static string Start(){SessionState.SetString("Svinki.MonkeyCalm.State","RUNNING");PlayerRegistry.Players.First(p=>p.IsLocal).StartCoroutine(Run());return "Toy movement check running";}
 static void Warp(PlayerAvatar p,Vector3 v){var cc=p.GetComponent<CharacterController>();cc.enabled=false;p.transform.position=v;cc.enabled=true;}
 static IEnumerator Run()
 {
  var p=PlayerRegistry.Players.First(x=>x.IsLocal);foreach(var b in UnityEngine.Object.FindObjectsByType<MannequinBrain>())b.enabled=false;foreach(var b in UnityEngine.Object.FindObjectsByType<ThiefBrain>())b.enabled=false;
  var toy=MonkeyToy.All.Single();Warp(p,toy.transform.position+Vector3.back*.8f);yield return null;
  toy.ApplyHolder(p);
  Warp(p,new Vector3(0,.05f,5));var view=p.EyeCamera.GetComponent<GrayboxFirstPersonCamera>();var yaw=typeof(GrayboxFirstPersonCamera).GetField("yaw",BindingFlags.Instance|BindingFlags.NonPublic);var pitch=typeof(GrayboxFirstPersonCamera).GetField("pitch",BindingFlags.Instance|BindingFlags.NonPublic);pitch.SetValue(view,0f);yaw.SetValue(view,0f);
  var rag=toy.GetComponent<ArticulatedRagdoll>();yield return new WaitForSecondsRealtime(3);
  float t=0,peak=0;while(t<3){t+=Time.fixedDeltaTime;yaw.SetValue(view,Mathf.Sin(t*4)*35);Warp(p,new Vector3(Mathf.Sin(t*3)*.6f,.05f,5));var a=rag.Capture();yield return new WaitForFixedUpdate();var b=rag.Capture();for(int i=0;i<a.Rotations.Length;i++)peak=Mathf.Max(peak,Quaternion.Angle(a.Rotations[i],b.Rotations[i])/Time.fixedDeltaTime);}
  yaw.SetValue(view,0f);Warp(p,new Vector3(0,.05f,5));yield return new WaitForSecondsRealtime(.5f);
  float distance=0,rotation=0;int samples=0;var last=rag.Capture();t=0;
  while(t<2){yield return new WaitForFixedUpdate();t+=Time.fixedDeltaTime;var now=rag.Capture();for(int i=0;i<now.Positions.Length;i++){distance+=Vector3.Distance(now.Positions[i],last.Positions[i]);rotation+=Quaternion.Angle(now.Rotations[i],last.Rotations[i]);samples++;}last=now;}
  bool hangs=rag.Center.y<rag.BonePosition("Hand_L").y-.1f;bool free=Vector3.Distance(rag.BonePosition("Hand_L"),rag.BonePosition("Hand_R"))>.12f;
  string result="Bodies="+rag.BodyCount+"; hanging="+hangs+"; free hand="+free+"; peak angular deg/s="+peak.ToString("F2")+"; residual mean linear m/s="+(distance/samples/Time.fixedDeltaTime).ToString("F4")+"; residual angular deg/s="+(rotation/samples/Time.fixedDeltaTime).ToString("F2");
  Directory.CreateDirectory("ArtSource/MonkeyCalm");string tag=SessionState.GetString("Svinki.MonkeyCalm.Tag","check");File.WriteAllText("ArtSource/MonkeyCalm/"+tag+".txt",result);ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/MonkeyCalm/"+tag+".png"));
  toy.Swing();yield return new WaitForSecondsRealtime(1.3f);bool miss=toy!=null&&!toy.Consumed;bool released=toy.Release();File.AppendAllText("ArtSource/MonkeyCalm/"+tag+".txt","\nMiss retained="+miss+"; release="+released+"\n");SessionState.SetString("Svinki.MonkeyCalm.State",hangs&&free&&miss&&released?"PASS":"FAIL");
 }
}
