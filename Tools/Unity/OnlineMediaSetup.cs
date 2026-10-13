using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
public static class OnlineMediaSetup
{
 public static async Task<string> Registry()
 {
  var method=typeof(Client).GetMethods(BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).Single(m=>m.Name=="AddScopedRegistry"&&m.GetParameters().Length==4);
  var request=method.Invoke(null,new object[]{"VoltUPR","https://upr.voltstro.dev",new[]{"dev.voltstro","org.nuget","com.cysharp.unitask"},false});
  var type=request.GetType();var deadline=EditorApplication.timeSinceStartup+30;
  while(!(bool)type.GetProperty("IsCompleted").GetValue(request)){if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("UPM registry timeout");await Task.Delay(100);}
  return type.GetProperty("Status").GetValue(request)+" "+type.GetProperty("Error").GetValue(request);
 }
 public static string Install()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode required");
  Client.AddAndRemove(new[]{"https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11","dev.voltstro.unitywebbrowser@2.2.8","dev.voltstro.unitywebbrowser.engine.cef@2.2.8","dev.voltstro.unitywebbrowser.engine.cef.win.x64@2.2.8","dev.voltstro.unitywebbrowser.engine.cef.macos.x64@2.2.8","dev.voltstro.unitywebbrowser.engine.cef.macos.arm64@2.2.8"});
  return "PackageManager request started; verify resolved PackageInfo after compilation";
 }
}
