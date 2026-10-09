using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class EmoteMovementCheck
{
 public static string Start(){SessionState.SetString("Svinki.EmoteMovement","RUNNING");PlayerRegistry.Players.First(p=>p.IsLocal).StartCoroutine(Check());return "Moving to wheel check running";}
 private static IEnumerator Check()
 {
  foreach(var brain in UnityEngine.Object.FindObjectsByType<MannequinBrain>())brain.enabled=false;
  foreach(var thief in UnityEngine.Object.FindObjectsByType<ThiefBrain>())thief.enabled=false;
  var player=PlayerRegistry.Players.First(p=>p.IsLocal);var motor=player.GetComponent<GrayboxPlayerController>();
  var settings=InputSystem.settings;var oldFlags=settings.hideFlags;settings.hideFlags=HideFlags.DontUnloadUnusedAsset;var clone=UnityEngine.Object.Instantiate(settings);clone.hideFlags=HideFlags.HideAndDontSave;clone.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=clone;
  var keyboard=InputSystem.AddDevice<Keyboard>("Wheel movement keyboard");var mouse=InputSystem.AddDevice<Mouse>("Wheel movement mouse");keyboard.MakeCurrent();mouse.MakeCurrent();
  InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSecondsRealtime(.3f);
  bool moved=motor.MotionSpeed>.3f;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.V));yield return new WaitForSecondsRealtime(.3f);
  bool stopped=EmoteWheel.IsOpen&&motor.MotionSpeed<.1f;
  InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.75f)});yield return null;yield return null;
  InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.25f);
  bool performed=player.GetComponentInChildren<PigMotion>().ActiveEmote==PigEmote.Wave;
  InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.settings=settings;settings.hideFlags=oldFlags;UnityEngine.Object.Destroy(clone);
  string result=moved&&stopped&&performed?"PASS":"FAIL moved="+moved+" stopped="+stopped+" performed="+performed;
  SessionState.SetString("Svinki.EmoteMovement",result);File.AppendAllText("ArtSource/EmoteMonkey/offline-validation.txt",result+": open wheel while walking, stop motion, release to perform\n");
  if(result!="PASS")throw new Exception(result);
 }
}
