using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public static class OnlineMediaCheck
{
 static void Need(bool yes,string message){if(!yes)throw new Exception(message);}
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static async Task Until(Func<bool> yes,string message,int timeout=30000)
 {var clock=System.Diagnostics.Stopwatch.StartNew();while(!yes()){Need(clock.ElapsedMilliseconds<timeout,message);await Task.Delay(100);}}
 static void Move(PlayerAvatar player,Vector3 point){var cc=player.GetComponent<CharacterController>();bool enabled=cc.enabled;cc.enabled=false;player.transform.position=point;Physics.SyncTransforms();cc.enabled=enabled;}
 static string Report(string name,string message){Directory.CreateDirectory("ArtSource/OnlineMedia");File.WriteAllText("ArtSource/OnlineMedia/"+name+".txt",message);return message;}
 public static string Urls()
 {
  foreach(var input in new[]{"https://www.youtube.com/watch?v=M7lc1UVf-VE&t=20","https://youtu.be/M7lc1UVf-VE","https://www.youtube.com/shorts/M7lc1UVf-VE","https://www.youtube-nocookie.com/embed/M7lc1UVf-VE"})
  {Need(MediaSourceProvider.TryResolve(input,LobbyMediaKind.Video,out var m)&&m.Backend==LobbyMediaBackend.YouTube&&m.Id=="M7lc1UVf-VE"&&m.Url=="https://www.youtube.com/watch?v=M7lc1UVf-VE","YouTube URL not canonicalized: "+input);}
  foreach(var input in new[]{"file:///tmp/video.mp4","javascript:alert(1)","https://youtube.com.evil.example/watch?v=M7lc1UVf-VE","https://www.youtube.com/watch?v=evil');alert(1)","https://user:password@www.youtube.com/watch?v=M7lc1UVf-VE","https://youtu.be/M7lc1UVf-VE/test","https://api.audius.co/v1/tracks/../stream","https://www.youtube.com:999/watch?v=M7lc1UVf-VE"})
   Need(!MediaSourceProvider.TryResolve(input,LobbyMediaKind.Video,out _)&&!MediaSourceProvider.TryResolve(input,LobbyMediaKind.Audio,out _),"Unsafe URL accepted: "+input);
  Need(MediaSourceProvider.TryResolve("https://cdn.example.com/a.mp3",LobbyMediaKind.Audio,out var audio)&&audio.Backend==LobbyMediaBackend.Direct,"Direct audio regression");
  Need(MediaSourceProvider.TryResolve("https://cdn.example.com/a.mp4",LobbyMediaKind.Video,out var video)&&video.Backend==LobbyMediaBackend.Direct,"Direct video regression");
  Need(!MediaSourceProvider.TryResolve("https://youtu.be/M7lc1UVf-VE",LobbyMediaKind.Audio,out _),"YouTube audio-only accepted");
  Need(MediaSourceProvider.TryResolve("https://api.audius.co/v1/tracks/x5dg3/stream?temp=expired",LobbyMediaKind.Audio,out var stream)&&stream.Url==AudiusMediaProvider.StreamUrl("x5dg3"),"Audius stable URL failed");
  return Report("url-validation","PASS canonical YouTube watch/short/embed URLs; stable Audius IDs; direct MP3/MP4 retained; wrong domains, ports, credentials, file/JS URLs and invalid IDs rejected; YouTube audio-only rejected.");
 }
 public static async Task<string> Music()
 {
  var lobby=NetworkLobby.Instance;Need(EditorApplication.isPlaying&&lobby!=null&&(lobby.Offline||lobby.IsHost),"Normal offline/host session required");
  var player=PlayerRegistry.Players.First(p=>p!=null&&p.IsLocal);var box=UnityEngine.Object.FindObjectsByType<LobbyJukebox>().Single();
  var original=player.Position;Move(player,new Vector3(box.transform.position.x,.05f,box.transform.position.z+2));
  try
  {
   await Task.Delay(200);Need(box.Validate(player),"Player cannot interact with jukebox");LobbyPanel.Open(box,player);
   var panel=UnityEngine.Object.FindAnyObjectByType<LobbyPanel>();var input=(TMP_InputField)typeof(LobbyPanel).GetField("searchText",Hidden).GetValue(panel);input.text="chill";
   typeof(LobbyPanel).GetMethod("BeginMusicSearch",Hidden).Invoke(panel,null);
   await Until(()=>panel.GetComponentsInChildren<Button>().Any(b=>b.transform.parent.name=="Music search"&&b.name.Contains("\n")),"Live ccMixter search returned no rows",20000);
   var result=panel.GetComponentsInChildren<Button>().First(b=>b.transform.parent.name=="Music search"&&b.name.Contains("\n"));result.onClick.Invoke();
   await Until(()=>!string.IsNullOrEmpty(box.State.Url)&&box.State.Url.StartsWith("https://ccmixter.org/files/"),"Catalog selection did not enter synchronized state",20000);
   var speaker=box.GetComponent<AudioSource>();await Until(()=>speaker.clip!=null&&speaker.isPlaying,"ccMixter MP3 did not play: "+box.LocalError,45000);
   Need(speaker.clip.length>30&&box.State.Title.Contains(" — ")&&box.State.Duration>0,"Track metadata/duration missing");
   var loaded=box.State;Need(box.LocalCatalogTrack?.LicenseName?.StartsWith("CC BY ")==true&&box.LocalCatalogTrack.Credit.Length>0,"CC BY attribution missing");
   Need(panel.GetComponentsInChildren<Button>().Any(b=>b.name=="TRACK SOURCE")&&panel.GetComponentsInChildren<Button>().Any(b=>b.name=="CC BY LICENSE"),"Source/license links missing");
   float[] samples=new float[1024];speaker.clip.GetData(samples,Math.Min(speaker.clip.samples-1024,speaker.clip.frequency*5));Need(samples.Any(s=>Mathf.Abs(s)>.0001f),"Downloaded track is silent");
   await Task.Delay(800);box.Request(player,LobbyAction.PlayPause);await Until(()=>!box.State.Playing&&!speaker.isPlaying,"Music pause failed");
   await Task.Delay(800);box.Request(player,LobbyAction.PlayPause);await Until(()=>box.State.Playing&&speaker.isPlaying,"Music resume failed");
   float volume=box.LocalVolume;box.LocalVolume=0;Need(speaker.volume==0&&box.State.Playing,"Local mute changed shared state");box.LocalVolume=volume;
   await Task.Delay(2200);result.onClick.Invoke();await Task.Delay(1800);Need(!box.Queue.Any(),"Duplicate current track entered queue");
   var second=panel.GetComponentsInChildren<Button>().Where(b=>b.transform.parent.name=="Music search"&&b.name.Contains("\n")).Skip(1).First();
   await Task.Delay(2200);second.onClick.Invoke();await Until(()=>box.Queue.Count()==1,"Second catalog track did not enter replicated queue",20000);
   var queued=box.Queue.Single();Need(queued.Url.StartsWith("https://ccmixter.org/files/")&&queued.Title.Contains("CC BY"),"Queue metadata missing");
   box.Request(player,LobbyAction.Skip);await Until(()=>box.State.Url==queued.Url&&!box.Queue.Any(),"Skip did not advance replicated queue");
   await Until(()=>speaker.clip!=null&&speaker.isPlaying&&box.State.Duration>0,"Queued stream did not play: "+box.LocalError,40000);
   typeof(LobbyPanel).GetMethod("Close",Hidden).Invoke(panel,null);float audioTime=speaker.time;
   Move(player,original);await Task.Delay(1800);Need(speaker.isPlaying&&speaker.time>audioTime+.7f,"Music stopped after menu closed and player walked away");
   Move(player,new Vector3(box.transform.position.x,.05f,box.transform.position.z+2));await Task.Delay(200);
   box.Request(player,LobbyAction.Skip);await Until(()=>string.IsNullOrEmpty(box.State.Url),"Skip did not stop empty queue");
   Need(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Count(e=>e.isActiveAndEnabled)==1,"More than one active EventSystem");
   return Report(lobby.Offline?"ccmixter-offline-validation":"ccmixter-host-validation","PASS live ccMixter search through UI, result button requests host-validated CC BY track, stable track page in state, real decoded audible MP3, artist/license/source links, pause/resume, local mute, duplicate rejection, replicated queue and metadata, skip to next decoded stream, passive playback after closing menu and walking away, skip empty queue, one active EventSystem. Track: "+loaded.Title);
  }
  finally{Move(player,original);}
 }
 public static async Task<string> YouTube()
 {
  var lobby=NetworkLobby.Instance;Need(EditorApplication.isPlaying&&lobby!=null&&(lobby.Offline||lobby.IsHost),"Normal offline/host required");
  var player=PlayerRegistry.Players.First(p=>p!=null&&p.IsLocal);var tv=UnityEngine.Object.FindObjectsByType<LobbyTelevision>().Single();var original=player.Position;Move(player,new Vector3(tv.transform.position.x+2,.05f,tv.transform.position.z));
  try
  {
   await Task.Delay(200);Need(tv.Validate(player),"Cannot use TV");LobbyPanel.Open(tv,player);tv.Request(player,LobbyAction.Load,"https://youtu.be/M7lc1UVf-VE");
   await Until(()=>tv.UsesYouTube,"YouTube backend did not start");
   await Until(()=>tv.State.Duration>0,"YouTube did not prepare: "+tv.LocalError,40000);
   var playback=(YouTubePlayback)typeof(LobbyTelevision).GetField("youtube",Hidden).GetValue(tv);
   if(playback.ReportedState!=1){tv.PlayerPointer(new Vector2(320,344),true,false);await Task.Delay(120);tv.PlayerPointer(new Vector2(320,344),false,true);}
   await Until(()=>playback.ReportedState==1,"YouTube video did not actually play: "+tv.LocalError,25000);
   await Task.Delay(2800);
   for(int attempt=0;attempt<3&&tv.State.Playing;attempt++){tv.PlayerPointer(new Vector2(320,344),true,false);await Task.Delay(120);tv.PlayerPointer(new Vector2(320,344),false,true);await Task.Delay(900);}
   await Until(()=>!tv.State.Playing&&playback.ReportedState==2,"Native browser footer pause did not change shared state",10000);
   await Task.Delay(800);tv.Request(player,LobbyAction.PlayPause);await Until(()=>tv.State.Playing&&playback.ReportedState==1,"Resume after native pause failed");
   double actual=playback.ReportedPosition;await Task.Delay(1800);Need(playback.ReportedPosition>actual+.7,"YouTube decoded timeline did not advance");
   var texture=(RenderTexture)tv.LocalPreview;var previous=RenderTexture.active;RenderTexture.active=texture;
   var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();
   File.WriteAllBytes("ArtSource/OnlineMedia/"+(lobby.Offline?"youtube-offline-frame":"youtube-host-frame")+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
   Need(tv.LocalPreview is RenderTexture&&tv.State.Url=="https://www.youtube.com/watch?v=M7lc1UVf-VE","Wrong TV output or source");
   await Task.Delay(800);tv.Request(player,LobbyAction.PlayPause);await Until(()=>!tv.State.Playing&&playback.ReportedState==2,"YouTube shared pause failed");
   await Task.Delay(800);tv.Request(player,LobbyAction.Seek,index:20);await Task.Delay(800);Need(Math.Abs(tv.Position-20)<.5,"YouTube shared seek failed");
   tv.Request(player,LobbyAction.PlayPause);await Until(()=>tv.State.Playing,"YouTube shared resume failed");await Task.Delay(2500);
   Need(playback.ReportedState==1&&Math.Abs(playback.ReportedPosition-tv.Position)<3,"YouTube playback did not resume at shared seek position");Need(playback.Ready&&playback.Error==null,"YouTube browser error: "+playback.Error);
   typeof(LobbyPanel).GetMethod("Close",Hidden).Invoke(UnityEngine.Object.FindAnyObjectByType<LobbyPanel>(),null);
   await Task.Delay(800);tv.Request(player,LobbyAction.Stop);await Until(()=>!tv.UsesYouTube&&string.IsNullOrEmpty(tv.State.Url),"YouTube process did not stop");
   return Report(lobby.Offline?"youtube-offline-validation":"youtube-host-validation","PASS official YouTube IFrame in native CEF on TV RenderTexture; real video duration, synchronized URL/play/pause/seek/stop, native player click permits autoplay, native browser footer pause updates shared state, decoded timeline advances, pause reaches actual player, resume matches shared seek position, browser ready without player errors; browser disposed on stop.");
  }
  finally{Move(player,original);}
 }
 public static async Task<string> DirectVideo()
 {
  var lobby=NetworkLobby.Instance;Need(EditorApplication.isPlaying&&lobby!=null&&lobby.IsHost,"Host required");
  var player=PlayerRegistry.Players.First(p=>p!=null&&p.IsLocal);var tv=UnityEngine.Object.FindObjectsByType<LobbyTelevision>().Single();var original=player.Position;
  Move(player,new Vector3(tv.transform.position.x+2,.05f,tv.transform.position.z));
  try
  {
   await Task.Delay(900);Need(tv.Validate(player),"Cannot use TV");tv.Request(player,LobbyAction.Load,"http://127.0.0.1:8874/test.mp4");var video=tv.GetComponent<UnityEngine.Video.VideoPlayer>();
   await Until(()=>!tv.UsesYouTube&&video.isPrepared&&video.isPlaying&&tv.State.Duration>0,"Direct MP4 regression: "+tv.LocalError,25000);
   double initial=video.time;await Task.Delay(1500);Need(video.time>initial+.5,"Direct MP4 timeline frozen");
   tv.Request(player,LobbyAction.PlayPause);await Until(()=>!tv.State.Playing&&!video.isPlaying,"Direct pause failed");
   await Task.Delay(800);tv.Request(player,LobbyAction.Seek,index:10);await Until(()=>Math.Abs(video.time-10)<1,"Direct seek failed",10000);
   tv.Request(player,LobbyAction.PlayPause);await Until(()=>tv.State.Playing&&video.isPlaying,"Direct resume failed");
   await Task.Delay(800);tv.Request(player,LobbyAction.Stop);await Until(()=>string.IsNullOrEmpty(tv.State.Url)&&!video.isPlaying,"Direct stop failed");
   return Report("direct-video-host-validation","PASS real local HTTP H.264 MP4 after YouTube stop; prepared, decoded timeline, duration, pause, seek, resume and stop on FishNet Host.");
  }
  finally{Move(player,original);}
 }

}
