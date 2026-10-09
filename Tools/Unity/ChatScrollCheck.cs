using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class ChatScrollCheck
{
 public static string Start(){SessionState.SetString("Svinki.ChatScroll","RUNNING");PlayerRegistry.Players.First(p=>p.IsLocal).StartCoroutine(Check());return "Chat input and scroll checks running";}
 private static IEnumerator Check()
 {
  var original=InputSystem.settings;var flags=original.hideFlags;original.hideFlags=HideFlags.DontUnloadUnusedAsset;
  var test=UnityEngine.Object.Instantiate(original);test.hideFlags=HideFlags.HideAndDontSave;test.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=test;
  var keys=InputSystem.AddDevice<Keyboard>("Chat history test keyboard");keys.MakeCurrent();
  InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Enter));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;
  var history=PlayerChat.Instance.GetComponent<PlayerChatHistory>();var scroll=history.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
  bool opened=PlayerChat.IsOpen&&scroll!=null&&scroll.enabled&&scroll.content.rect.height>scroll.viewport.rect.height;
  if(scroll!=null){scroll.verticalNormalizedPosition=1;yield return null;}
  bool scrolled=scroll!=null&&scroll.verticalNormalizedPosition>.99f;
  ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/VoiceChat/history-scroll.png"));yield return null;
  InputSystem.QueueStateEvent(keys,new KeyboardState(Key.Escape));yield return null;yield return null;
  bool cancelled=!PlayerChat.IsOpen&&!NetworkLobby.Instance.MenuVisible;
  InputSystem.RemoveDevice(keys);InputSystem.settings=original;original.hideFlags=flags;UnityEngine.Object.Destroy(test);
  string result=opened&&scrolled&&cancelled?"PASS":"FAIL opened="+opened+" scrolled="+scrolled+" cancelled="+cancelled;
  File.AppendAllText("ArtSource/VoiceChat/history-validation.txt",result+": Enter opens input and expanded history; old messages scroll into view; Escape closes without pause\n");SessionState.SetString("Svinki.ChatScroll",result);if(result!="PASS")throw new Exception(result);
 }
}
