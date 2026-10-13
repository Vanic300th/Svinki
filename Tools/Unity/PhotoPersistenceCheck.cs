using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class PhotoPersistenceCheck
{
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static string Root=>Path.GetFullPath("ArtSource/PhotoPersistence");
 static byte[] Picture(int n)
 {
  var image=new Texture2D(PhotoAlbum.PhotoWidth,PhotoAlbum.PhotoHeight,TextureFormat.RGB24,false);
  try{var pixels=Enumerable.Repeat(new Color32((byte)n,(byte)(n*23),(byte)(n*91),255),image.width*image.height).ToArray();image.SetPixels32(pixels);image.Apply();return image.EncodeToJPG(28);}
  finally{UnityEngine.Object.DestroyImmediate(image);}
 }
 public static string Storage()
 {
  Directory.CreateDirectory(Root);string path=Path.Combine(Root,"storage-check-"+Guid.NewGuid().ToString("N")+".sqlite");
  string pin;var jpeg=Picture(1);var clock=System.Diagnostics.Stopwatch.StartNew();
  try
  {
   using(var db=new PhotoDatabase(path,"owner'; DROP TABLE photos;--"))
   {
    pin=db.Save(jpeg);db.Save(jpeg);Need(db.Read().Count==1,"Identical photograph not deduplicated");db.Pin(0,pin);
    for(int i=2;i<142;i++)db.Save(Picture(i));Need(db.Read().Count==121,"Archive retention did not protect pinned old photo");
    Need(db.ReadPins()[0].SequenceEqual(jpeg),"Pinned bytes changed");
   }
   using(var db=new PhotoDatabase(path,"other-player"))
   {Need(db.Read().Count==0&&db.ReadPins().Count==0,"Another player's profile leaked");bool rejected=false;try{db.Pin(1,pin);}catch(IOException){rejected=true;}Need(rejected,"Foreign photo could be pinned");}
   using(var db=new PhotoDatabase(path,"owner'; DROP TABLE photos;--"))
   {Need(db.Read().Count==121&&db.ReadPins()[0].SequenceEqual(jpeg),"Photos/pins did not survive database reopen");db.Pin(0,null);Need(db.Read().Count==120&&db.ReadPins().Count==0,"Clear pin or retention failed");}
   var report="PASS real SQLite: JPEG bytes survive close/reopen; pins survive reopen; per-profile isolation; bound SQL parameters; deduplication; 120 recent photos plus protected pins; clear/reset retention. Elapsed "+clock.ElapsedMilliseconds+" ms.";
   File.WriteAllText(Path.Combine(Root,"database-validation.txt"),report);return report;
  }
  finally{foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
 }
 public static string InjectTestArchive()
 {
  Need(EditorApplication.isPlaying,"Play mode required");var album=PhotoAlbum.Instance;Need(album!=null,"No album");
  Directory.CreateDirectory(Root);var path=Path.Combine(Root,"play-check.sqlite");
  typeof(PhotoAlbum).GetMethod("DisposeArchive",Flags).Invoke(album,null);
  var db=new PhotoDatabase(path,"play-check");
  typeof(PhotoAlbum).GetField("database",Flags).SetValue(album,db);typeof(PhotoAlbum).GetField("saved",Flags).SetValue(album,db.Read());
  return "Temporary test archive injected; player's real archive untouched";
 }
 public static async Task<string> Offline()
 {
  Need(EditorApplication.isPlaying&&NetworkLobby.Instance?.Offline==true,"Normal offline lobby required");
  var album=PhotoAlbum.Instance;var player=PlayerRegistry.Players.First(p=>p!=null&&p.IsLocal);var camera=player.EyeCamera;
  album.Capture(camera,null);Need(album.Count>0&&album.SavedCount>0,"Capture did not archive");var captured=album.SavedJpeg(0);
  album.ResetRound();Need(album.Count==0&&album.SavedCount>0,"Round reset erased permanent archive");
  var locker=UnityEngine.Object.FindObjectsByType<PlayerLocker>().Single(l=>l.OwnerId=="offline");
  var control=player.GetComponent<CharacterController>();bool enabled=control.enabled;var original=player.transform.position;
  Move(player,locker.transform.position+new Vector3(0,0,-1.6f));
  try
  {
   await Task.Delay(150);Need(locker.Validate(player),"Cannot interact with test locker");
   NetworkLobby.Instance.AssignSavedLockerPhoto(locker,player,2,0);Need(album.SavedLockerPhotos().TryGetValue(2,out var pinned)&&pinned.SequenceEqual(captured),"Pin not saved to SQLite");
   Need(NetworkLobby.Instance.LockerPhoto("offline",2)!=null,"Pinned photo not displayed");
   // Reopen the actual archive connection, as a new process would, then restore the room cache.
   InjectTestArchive();Need(album.SavedLockerPhotos().TryGetValue(2,out pinned)&&pinned.SequenceEqual(captured),"Pin lost after reload");
   typeof(NetworkLobby).GetMethod("ClearLockerPhotos",Flags).Invoke(NetworkLobby.Instance,null);
   typeof(NetworkLobby).GetMethod("RestoreOfflineLockerPhotos",Flags).Invoke(NetworkLobby.Instance,null);
   Need(NetworkLobby.Instance.LockerPhoto("offline",2)!=null,"Room cache did not restore persistent pin");
   NetworkLobby.Instance.ClearSavedLockerPhoto(locker,player,2);Need(!album.SavedLockerPhotos().ContainsKey(2),"Pin clear not persisted");
   album.Open();album.ToggleArchive();await Task.Delay(120);Need(PhotoAlbum.IsOpen,"Archive UI not opened");album.Select(1);album.Close();
   Need(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Count(e=>e.isActiveAndEnabled)==1,"Album lost single active EventSystem");
   var report="PASS offline gameplay: camera capture stored to SQLite; round reset retains archive; saved-photo selection, pin and clear; database reopen restores locker image; archive UI opens/browses/closes with one EventSystem.";
   File.WriteAllText(Path.Combine(Root,"offline-validation.txt"),report);return report;
  }
  finally{Move(player,original);control.enabled=enabled;}
 }
 static async Task Until(Func<bool> ready,string failure,int milliseconds=10000)
 {var watch=System.Diagnostics.Stopwatch.StartNew();while(!ready()){Need(watch.ElapsedMilliseconds<milliseconds,failure);await Task.Delay(100);}}
 static void Move(PlayerAvatar player,Vector3 point)
 {var c=player.GetComponent<CharacterController>();bool active=c.enabled;c.enabled=false;player.transform.position=point;Physics.SyncTransforms();c.enabled=active;}
 public static async Task<string> Host()
 {
  var lobby=NetworkLobby.Instance;Need(EditorApplication.isPlaying&&lobby!=null&&lobby.IsHost&&!lobby.Offline,"Normal local host required");
  await Until(()=>PlayerRegistry.Players.Any(p=>p!=null&&p.IsLocal)&&UnityEngine.Object.FindObjectsByType<PlayerLocker>().Any(l=>l.OwnerId==lobby.Identity),"Host player/locker not loaded");
  await Until(()=>typeof(NetworkLobby).GetField("savedUploadRoutine",Flags).GetValue(lobby)==null&&typeof(NetworkLobby).GetField("savedRestoreRoutine",Flags).GetValue(lobby)==null,"Initial restore did not finish",15000);
  var album=PhotoAlbum.Instance;var player=PlayerRegistry.Players.First(p=>p!=null&&p.IsLocal);
  album.Capture(player.EyeCamera,null);Need(album.SavedCount>0,"Host capture not archived");var captured=album.SavedJpeg(0);
  var locker=UnityEngine.Object.FindObjectsByType<PlayerLocker>().Single(l=>l.OwnerId==lobby.Identity);
  var control=player.GetComponent<CharacterController>();bool enabled=control.enabled;var original=player.transform.position;
  Move(player,locker.transform.position+new Vector3(0,0,-1.6f));
  try
  {
   await Task.Delay(200);Need(locker.Validate(player),"Host cannot use test locker");
   lobby.AssignSavedLockerPhoto(locker,player,1,0);
   await Until(()=>lobby.LockerPhoto(lobby.Identity,1)!=null&&album.SavedLockerPhotos().TryGetValue(1,out var pin)&&pin.SequenceEqual(captured),"Real FishNet upload/echo did not persist pin");
   InjectTestArchive();Need(album.SavedLockerPhotos()[1].SequenceEqual(captured),"Acknowledged host pin lost after SQLite reopen");
   // Exercise the server guards through the real client broadcast transport.
   var manager=lobby.GetComponent<FishNet.Managing.NetworkManager>();Need(manager!=null,"No network manager");
   await Until(()=>typeof(NetworkLobby).GetField("savedUploadRoutine",Flags).GetValue(lobby)==null,"Upload queue did not drain");
   byte[] invalid={255,216,0,0};
   manager.ClientManager.Broadcast(new SavedLockerUpload{slot=6,total=invalid.Length,offset=0,token=Guid.NewGuid().ToString("N"),bytes=invalid});
   manager.ClientManager.Broadcast(new SavedLockerUpload{slot=0,total=32769,offset=0,token=Guid.NewGuid().ToString("N"),bytes=invalid});
   manager.ClientManager.Broadcast(new SavedLockerUpload{slot=0,total=invalid.Length,offset=0,token=Guid.NewGuid().ToString("N"),bytes=invalid});
   await Task.Delay(1200);Need(lobby.LockerPhoto(lobby.Identity,0)==null,"Host accepted malformed upload");
   Move(player,original);await Task.Delay(200);
   // A genuine JPEG from outside interaction range must not be accepted.
   for(int offset=0;offset<captured.Length;offset+=1024)
   {int length=Math.Min(1024,captured.Length-offset);var bytes=new byte[length];Buffer.BlockCopy(captured,offset,bytes,0,length);manager.ClientManager.Broadcast(new SavedLockerUpload{slot=0,total=captured.Length,offset=offset,token="00000000000000000000000000000000",bytes=bytes});}
   await Task.Delay(300);Need(lobby.LockerPhoto(lobby.Identity,0)==null,"Host accepted distant manual upload");
   // The snapshot download path must reconstruct the image for a joining client without changing its archive.
   typeof(NetworkLobby).GetMethod("SetLockerImage",Flags).Invoke(lobby,new object[]{lobby.Identity+":1",null});
   lobby.RequestLockerPhotos();await Until(()=>lobby.LockerPhoto(lobby.Identity,1)!=null,"Room snapshot failed to restore cached image");
   Need(album.SavedLockerPhotos()[1].SequenceEqual(captured),"Room snapshot changed local archive");
   Move(player,locker.transform.position+new Vector3(0,0,-1.6f));await Task.Delay(200);
   lobby.ClearSavedLockerPhoto(locker,player,1);await Until(()=>lobby.LockerPhoto(lobby.Identity,1)==null&&!album.SavedLockerPhotos().ContainsKey(1),"Host clear echo did not persist");
   var report="PASS normal FishNet local host: saved JPEG upload and echoed pin persist to SQLite; database reopen; malformed, oversize and distant uploads rejected; snapshot download reconstructs room cache; confirmed clear persists. Separate remote client/reconnect not exercised.";
   File.WriteAllText(Path.Combine(Root,"host-validation.txt"),report);return report;
  }
  finally{Move(player,original);control.enabled=enabled;}
 }
 public static async Task<string> Factory()
 {
  var lobby=NetworkLobby.Instance;Need(EditorApplication.isPlaying&&lobby?.Offline==true,"Offline lobby required");
  var player=PlayerRegistry.Players.First(p=>p!=null&&p.IsLocal);var count=PhotoAlbum.Instance.SavedCount;
  var lift=UnityEngine.Object.FindObjectsByType<LobbyFreightElevator>().Single(l=>l.gameObject.scene.name==NetworkLobby.FriendslopSceneName);
  Move(player,lift.transform.TransformPoint(new Vector3(0,.05f,0)));Need(lift.Contains(player.Position),"Test player missed lift");
  await Until(()=>!lobby.Busy&&PlayerRegistry.Players.Any(p=>p!=null&&p.IsLocal&&p.gameObject.scene.name=="SampleScene"),"Normal lift did not load factory",25000);
  return await Arrival(count);
 }
 public static async Task<string> Arrival(int savedCount=-1)
 {
  var player=PlayerRegistry.Players.First(p=>p!=null&&p.IsLocal&&p.gameObject.scene.name=="SampleScene");
  var displays=UnityEngine.Object.FindObjectsByType<FixedFactoryDisplay>().Where(d=>d.gameObject.scene.name=="SampleScene").OrderBy(d=>d.name).ToArray();
  Need(displays.Length==6,"Factory arrival lost fixed displays");
  Vector3[] expected={new Vector3(-6,0,1.5f),new Vector3(6,0,1.5f),new Vector3(-8,0,5),new Vector3(8,0,5),new Vector3(-6,0,7.5f),new Vector3(6,0,7.5f)};
  for(int i=0;i<6;i++)
  // Authoring snaps intended positions onto the baked NavMesh within 0.6 metres.
  {var d=displays[i];Need(Vector2.Distance(new Vector2(d.transform.position.x,d.transform.position.z),new Vector2(expected[i].x,expected[i].z))<.6f,"Round shuffle moved arrival display");Need(d.GetComponent<MannequinBrain>()==null&&d.GetComponent<ThiefBrain>()==null&&d.GetComponent<MannequinHeadWatcher>()==null&&d.GetComponent<Rigidbody>().isKinematic,"Arrival display has AI or is not stationary");}
  var original=player.Position;var mannequin=displays[0].GetComponent<ThrowableMannequin>();
  try
  {Move(player,mannequin.transform.position+new Vector3(0,0,-1.6f));await Task.Delay(150);Need(mannequin.TryGrab(player)&&mannequin.IsHeld,"Arrival mannequin cannot be carried");Need(mannequin.Release(false)&&!mannequin.IsHeld,"Arrival mannequin cannot be released");}
  finally{Move(player,original);}
  Need(savedCount<0||PhotoAlbum.Instance.SavedCount==savedCount,"Factory transition erased permanent photos");
  var report="PASS normal offline freight lift loads factory; six AFK mannequins remain at authored arrival positions after round shuffle; no hostile AI, stationary rigidbodies; existing carry/release works; permanent album survives scene transition.";
  File.WriteAllText(Path.Combine(Root,"factory-play-validation.txt"),report);return report;
 }
}
