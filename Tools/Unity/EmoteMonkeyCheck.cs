using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class EmoteMonkeyCheck
{
 private static Keyboard keys;private static Mouse mouse;private static InputSettings original,test;private static HideFlags originalFlags;
 private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 private static void Assert(bool value,string reason){if(!value)throw new Exception(reason);}
 private static void Press(params Key[] pressed){InputSystem.QueueStateEvent(keys,new KeyboardState(pressed));}
 private static void Point(Vector2 point){InputSystem.QueueStateEvent(mouse,new MouseState{position=point});}
 private static void Move(PlayerAvatar p,Vector3 point){var cc=p.GetComponent<CharacterController>();cc.enabled=false;p.transform.position=point;cc.enabled=true;}
 private static void Look(PlayerAvatar p,float yaw,float pitch){var view=p.EyeCamera.GetComponent<GrayboxFirstPersonCamera>();typeof(GrayboxFirstPersonCamera).GetField("yaw",Flags).SetValue(view,yaw);typeof(GrayboxFirstPersonCamera).GetField("pitch",Flags).SetValue(view,pitch);}
 private static void Cleanup(){if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);if(original!=null){InputSystem.settings=original;original.hideFlags=originalFlags;}if(test!=null)UnityEngine.Object.Destroy(test);}
 private static bool Check(Action action){try{action();return true;}catch(Exception e){Cleanup();SessionState.SetString("Svinki.EmoteMonkey.Check","FAIL "+e.Message);File.AppendAllText("ArtSource/EmoteMonkey/offline-validation.txt","FAIL "+e+"\n");Debug.LogException(e);return false;}}
 public static string Start(){SessionState.SetString("Svinki.EmoteMonkey.Check","RUNNING");PlayerRegistry.Players.First(p=>p.IsLocal).StartCoroutine(Run());return "Wheel, six gestures and one-hand physics check running";}
 private static IEnumerator Run()
 {
  var player=PlayerRegistry.Players.First(p=>p.IsLocal);var motion=player.GetComponentInChildren<PigMotion>();
  if(!Check(()=>{foreach(var npc in UnityEngine.Object.FindObjectsByType<MannequinBrain>())npc.enabled=false;foreach(var npc in UnityEngine.Object.FindObjectsByType<ThiefBrain>())npc.enabled=false;
   original=InputSystem.settings;originalFlags=original.hideFlags;original.hideFlags=HideFlags.DontUnloadUnusedAsset;test=UnityEngine.Object.Instantiate(original);test.hideFlags=HideFlags.HideAndDontSave;test.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=test;
   keys=InputSystem.AddDevice<Keyboard>("Emote check keyboard");mouse=InputSystem.AddDevice<Mouse>("Emote check mouse");keys.MakeCurrent();mouse.MakeCurrent();Move(player,new Vector3(0,.05f,5));Look(player,0,0);
   File.WriteAllText("ArtSource/EmoteMonkey/offline-validation.txt","Protocol 9 / main\n");}))yield break;
  yield return new WaitForSecondsRealtime(.4f);Press(Key.V);yield return null;yield return null;
  if(!Check(()=>Assert(EmoteWheel.IsOpen&&!NetworkLobby.Instance.InputAllowed,"Wheel did not open or game input remained allowed")))yield break;
  Point(new Vector2(Screen.width*.5f,Screen.height*.5f+Screen.height*.22f));yield return null;yield return null;
  ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/EmoteMonkey/wheel.png"));yield return null;
  Press();yield return null;yield return null;
  if(!Check(()=>Assert(!EmoteWheel.IsOpen&&motion.ActiveEmote==PigEmote.Wave,"V release did not select Wave")))yield break;
  Press(Key.V);yield return null;yield return null;Press(Key.V,Key.Escape);yield return null;yield return null;
  if(!Check(()=>Assert(!EmoteWheel.IsOpen&&!NetworkLobby.Instance.MenuVisible,"Escape cancel also opened pause menu")))yield break;
  Press();yield return new WaitForSecondsRealtime(.2f);
  if(!Check(()=>{File.AppendAllText("ArtSource/EmoteMonkey/offline-validation.txt","PASS: actual V open, mouse sector selection, V release, input blocking and Escape cancellation\n");}))yield break;
  foreach(PigEmote kind in new[]{PigEmote.Wave,PigEmote.Cheer,PigEmote.Clap,PigEmote.Dance,PigEmote.Laugh,PigEmote.Shrug})
  {
   motion.PlayEmote(PigEmote.None);yield return null;
   var arm=motion.GetComponentsInChildren<Transform>().First(t=>t.name=="UpperArm_R");var before=arm.localRotation;motion.PlayEmote(kind);yield return new WaitForSecondsRealtime(.45f);
   if(!Check(()=>{Assert(motion.ActiveEmote==kind&&Quaternion.Angle(before,arm.localRotation)>5,kind+" has no actual bone animation");File.AppendAllText("ArtSource/EmoteMonkey/offline-validation.txt","PASS: "+kind+" changes pig bones\n");}))yield break;
  }
  motion.PlayEmote(PigEmote.Dance);Press(Key.W);yield return new WaitForSecondsRealtime(.25f);
  if(!Check(()=>Assert(motion.ActiveEmote==PigEmote.None,"Moving did not cancel emote")))yield break;
  Press();yield return new WaitForSecondsRealtime(.5f);motion.PlayEmote(PigEmote.Shrug);yield return new WaitForSecondsRealtime(3.2f);
  if(!Check(()=>Assert(motion.ActiveEmote==PigEmote.None,"Emote never timed out")))yield break;
  var toy=MonkeyToy.All.Single();Move(player,toy.transform.position+Vector3.back*.8f);yield return null;
  if(!Check(()=>Assert(toy.TryGrab(player),"Cannot grab updated toy")))yield break;
  Move(player,new Vector3(0,.05f,5));Look(player,0,0);yield return new WaitForSecondsRealtime(2);
  if(!Check(()=>{var rag=toy.GetComponent<ArticulatedRagdoll>();Assert(rag.BodyCount==13,"Missing toy bodies");
   Assert(Vector3.Distance(rag.BonePosition("Hand_L"),rag.BonePosition("Hand_R"))>.12f,"Free arm did not fall");
   Assert(rag.Center.y<rag.BonePosition("Hand_L").y-.1f,"Toy is not hanging from its wrist");
   Assert(toy.GetComponentsInChildren<Transform>().Any(t=>t.name=="Pig paw"&&t.gameObject.activeInHierarchy),"First person hand missing");
   Assert(!PigMotion.CanEmote(player),"Can emote while holding toy");}))yield break;
  ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/EmoteMonkey/one-hand.png"));yield return null;
  if(!Check(()=>Assert(toy.Swing()&&!toy.Swing(),"Swing cooldown failed")))yield break;
  yield return new WaitForSecondsRealtime(.4f);ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/EmoteMonkey/swing.png"));yield return new WaitForSecondsRealtime(.8f);
  if(!Check(()=>{Assert(toy!=null&&!toy.Consumed,"A miss consumed toy");File.AppendAllText("ArtSource/EmoteMonkey/offline-validation.txt","PASS: 13 bodies, single pinned wrist, free arm/body hanging, visible pig paw; smooth attack and cooldown; miss retains toy\n");}))yield break;
  var stun=UnityEngine.Object.FindObjectsByType<MannequinStun>().First();var agent=stun.GetComponent<UnityEngine.AI.NavMeshAgent>();agent.enabled=true;agent.Warp(new Vector3(0,0,6.35f));Look(player,0,12);
  if(!Check(()=>Assert(toy.Swing(),"Second swing refused after cooldown")))yield break;
  yield return new WaitForSecondsRealtime(1.1f);
  if(!Check(()=>{Assert(toy==null&&stun.IsStunned&&stun.Remaining>28,"Actual attack failed consumption or 30 second stun");File.AppendAllText("ArtSource/EmoteMonkey/offline-validation.txt","PASS: actual hit consumed toy and applied 30-second mannequin stun\n");SessionState.SetString("Svinki.EmoteMonkey.Check","PASS");Cleanup();}))yield break;
 }
}
