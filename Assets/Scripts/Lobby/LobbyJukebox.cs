using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[Serializable] public struct LobbyTrack { public string Url, Title, Player, PlayerId; }
[RequireComponent(typeof(AudioSource))]
public sealed class LobbyJukebox : LobbyMediaPlayer
{
 [SerializeField, Range(1,24)] private int maxQueue=12;
 [SerializeField, Range(1,6)] private int perPlayerLimit=3;
 [SerializeField, Min(1)] private float enqueueCooldown=2;
 private readonly SyncList<LobbyTrack> queue=new SyncList<LobbyTrack>();
 private readonly List<LobbyTrack> offlineQueue=new List<LobbyTrack>();
 private readonly Dictionary<string,float> nextEnqueue=new Dictionary<string,float>();
 private readonly Dictionary<string,int> resolving=new Dictionary<string,int>();
 public override LobbyMediaKind Kind=>LobbyMediaKind.Audio;
 public IEnumerable<LobbyTrack> Queue=>NetworkLobby.Instance?.Offline==true?offlineQueue:queue;
 private int Count=>NetworkLobby.Instance?.Offline==true?offlineQueue.Count:queue.Count;
 private int Owned(string id)=>(State.PlayerId==id&&!string.IsNullOrEmpty(State.Url)?1:0)+Queue.Count(t=>t.PlayerId==id);
 public override void Apply(PlayerAvatar player,LobbyAction action,string text,int index,Vector2[] points)
 {
  if(action==LobbyAction.Skip){if(AllowControl(player))Next();return;}
  if(action!=LobbyAction.Load){base.Apply(player,action,text,index,points);return;}
  string id=Identity(player);int pending=resolving.TryGetValue(id,out int n)?n:0;
  if(!MediaSourceProvider.TryResolve(text,Kind,out var media)){Notice(id,"Unsupported audio URL");return;}
  if(nextEnqueue.TryGetValue(id,out float deadline)&&Time.unscaledTime<deadline)return;
  if(Count+resolving.Values.Sum()>=maxQueue||Owned(id)+pending>=perPlayerLimit){Notice(id,"Queue or player track limit reached");return;}
  nextEnqueue[id]=Time.unscaledTime+enqueueCooldown;
  if(media.Backend==LobbyMediaBackend.Audius||media.Backend==LobbyMediaBackend.CcMixter){resolving[id]=pending+1;Notice(id,"Checking music source…");StartCoroutine(ResolveTrack(player,id,media));}
  else Enqueue(media,Nickname(player),id);
 }
 private IEnumerator ResolveTrack(PlayerAvatar player,string id,ResolvedMedia input)
 {
  IMusicCatalogProvider catalog=input.Backend==LobbyMediaBackend.CcMixter?new CcMixterMusicCatalog():new AudiusMusicCatalog();
  CatalogTrack track=null;string error=null;yield return catalog.Resolve(input,(t,e)=>{track=t;error=e;});
  if(resolving.TryGetValue(id,out int n)){if(n<=1)resolving.Remove(id);else resolving[id]=n-1;}
  // Recheck the authenticated avatar, room phase and proximity after the external request.
  if(!Authority||player==null||Identity(player)!=id||!Validate(player))yield break;
  if(track==null){Notice(id,error??"Track unavailable");yield break;}
  if(!MediaSourceProvider.TryResolve(track.Url,Kind,out var media)){Notice(id,"Unsupported catalog stream");yield break;}
  media.Title=track.Title+" — "+track.Artist+(track.LicenseName==null?"":" · "+track.LicenseName);Enqueue(media,Nickname(player),id);
 }
 private void Enqueue(ResolvedMedia media,string name,string id)
 {
  if(Count>=maxQueue||Owned(id)>=perPlayerLimit){Notice(id,"Queue or player track limit reached");return;}
  if(media.Url==State.Url||Queue.Any(t=>t.Url==media.Url)){Notice(id,"This track is already queued");return;}
  var track=new LobbyTrack{Url=media.Url,Title=media.Title,Player=name,PlayerId=id};
  if(NetworkLobby.Instance?.Offline==true)offlineQueue.Add(track);else queue.Add(track);
  Notice(id,"");if(string.IsNullOrEmpty(State.Url))Next();
 }
 private void Notice(string id,string message)
 {if(NetworkLobby.Instance?.Offline==true)LocalError=message.Length==0?null:message;else NotifyOwner(id,message);}
 [ObserversRpc] private void NotifyOwner(string id,string message)
 {if(NetworkLobby.Instance?.Identity==id)LocalError=message.Length==0?null:message;}
 private void Next()
 {
  if(!Authority)return;if(Count==0){StopMedia();return;}
  var track=NetworkLobby.Instance?.Offline==true?offlineQueue[0]:queue[0];
  if(NetworkLobby.Instance?.Offline==true)offlineQueue.RemoveAt(0);else queue.RemoveAt(0);
  if(MediaSourceProvider.TryResolve(track.Url,Kind,out var media)){media.Title=track.Title;Load(media,track.Player,track.PlayerId);}else StopMedia();
 }
 protected override void Finished()=>Next();
}
