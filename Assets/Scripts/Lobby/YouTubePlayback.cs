using System;
using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using VoltstroStudios.UnityWebBrowser.Core;
using VoltstroStudios.UnityWebBrowser.Core.Engines;
using VoltstroStudios.UnityWebBrowser.Communication;
using VoltstroStudios.UnityWebBrowser.Shared;
using VoltstroStudios.UnityWebBrowser.Shared.Events;
using Resolution = VoltstroStudios.UnityWebBrowser.Shared.Resolution;

/// <summary>Local browser playback only. The shared timeline stays in LobbyMediaState/FishNet.</summary>
public sealed class YouTubePlayback : IDisposable
{
 [Serializable] public sealed class Report {public int version,state,error,intent;public double duration,time;public bool blocked;}
 private WebBrowserClient browser;
 private TCPCommunicationLayer communication;
 private YouTubePlayerPage page;
 private readonly ConcurrentQueue<string> reports=new ConcurrentQueue<string>();
 private float started,nextSync;
 private readonly int version;
 public string Error {get;private set;}
 public bool Ready=>browser!=null&&browser.ReadySignalReceived&&browser.IsConnected;
 public Texture Texture=>browser?.BrowserTexture;
 public double Duration {get;private set;}
 public double ReportedPosition {get;private set;}
 public int ReportedState {get;private set;}=-1;
 public int NativeIntent {get;private set;}=-1;
 public double NativePosition {get;private set;}
 public float NativeInputUntil {get;private set;}
 public YouTubePlayback(string id,int revision)
 {
  version=revision;started=Time.unscaledTime;
  try
  {
   string html=File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"LobbyMedia","youtube-player.html"));
   html=html.Replace("__CONFIG__",JsonConvert.SerializeObject(new{id,version,referrer="https://"+Application.identifier}).Replace("'","\\u0027"));
   page=new YouTubePlayerPage(html);communication=ScriptableObject.CreateInstance<TCPCommunicationLayer>();communication.inPort=YouTubePlayerPage.AvailablePort();do{communication.outPort=YouTubePlayerPage.AvailablePort();}while(communication.outPort==communication.inPort);
   browser=new WebBrowserClient{engine=Resources.Load<EngineConfiguration>("Cef Engine Configuration"),communicationLayer=communication,initialUrl=page.Url,incognitoMode=true,webRtc=false,remoteDebugging=false,windowlessFrameRate=20,Resolution=new Resolution(640,360),engineStartupTimeout=15000};
   browser.jsMethodManager.jsMethodsEnable=true;browser.RegisterJsMethod<string>("FriendslopMedia",OnReport);browser.Init();
  }
  catch(Exception e){Error="YouTube browser could not start: "+e.Message;Dispose();}
 }
 private void OnReport(string json){if(json!=null&&json.Length<2048&&reports.Count<8)reports.Enqueue(json);}
 public void Tick(double position,bool playing,float volume,bool visible)
 {
  NativeIntent=-1;
  while(reports.TryDequeue(out string json))
  {
   Report report;try{report=JsonConvert.DeserializeObject<Report>(json);}catch(Exception){continue;}
   if(report==null||report.version!=version)continue;
   if(double.IsFinite(report.time)&&report.time>=0)ReportedPosition=report.time;
   ReportedState=report.state;
   if(double.IsFinite(report.duration)&&report.duration>0&&report.duration<=86400)Duration=report.duration;
   if(report.error!=0)Error=report.error==101||report.error==150?"This video does not allow embedding":report.error==153?"YouTube rejected the player identity (153)":"YouTube playback error "+report.error;
   else if(report.blocked)Error="Click the player to allow YouTube playback";
   else Error=null;
   if(Time.unscaledTime<NativeInputUntil&&report.intent>=1&&report.intent<=3&&double.IsFinite(report.time)&&report.time>=0){NativeIntent=report.intent;NativePosition=report.time;}
  }
  if(Duration==0&&Error==null&&Time.unscaledTime-started>25)Error="YouTube player timed out. Check the link or try again.";
  if(!Ready){if(Error==null&&Time.unscaledTime-started>25)Error="YouTube browser timed out";return;}
  if(Time.unscaledTime<nextSync)return;nextSync=Time.unscaledTime+.5f;
  // No stream extraction, ad removal or audio-only YouTube player. Controls and branding remain visible.
  browser.ExecuteJs("if(window.friendslopSync)friendslopSync("+JsonConvert.SerializeObject(position)+","+(playing?"true":"false")+","+JsonConvert.SerializeObject(volume)+","+(visible?"true":"false")+");");
 }
 public void Render(RenderTexture target)
 {if(Ready){browser.LoadTextureData();if(browser.BrowserTexture!=null)Graphics.Blit(browser.BrowserTexture,target,new Vector2(1,-1),new Vector2(0,1));}}
 public void Mouse(Vector2 point,bool down,bool up)
 {if(!Ready)return;NativeInputUntil=Time.unscaledTime+1.5f;browser.ExecuteJs("window.friendslopNativeUntil=Date.now()+1500;");browser.SendMouseMove(point);if(down||up)browser.SendMouseClick(point,1,MouseClickType.Left,up?MouseEventType.Up:MouseEventType.Down);}
 public void Dispose()
 {
  try{browser?.Dispose();}catch(Exception e){Debug.LogWarning("YouTube browser cleanup: "+e.Message);}
  finally{browser=null;page?.Dispose();page=null;if(communication!=null)UnityEngine.Object.Destroy(communication);communication=null;}
 }
}
