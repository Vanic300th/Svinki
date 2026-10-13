using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

// Images travel separately from score snapshots, in small reliable chunks.
public struct RoundPhotoUpload : IBroadcast { public int round,total,offset;public string token;public byte[] bytes; }
public struct RoundPhotoRequest : IBroadcast { public int round; }
public struct RoundPhotoManifest : IBroadcast { public int round,version,count;public string player; }
public struct RoundPhotoPart : IBroadcast { public int round,version,slot,total,offset;public string player;public byte[] bytes; }

public sealed partial class NetworkLobby
{
    private const int PhotoChunkSize=1024,MaxPhotoBytes=32768;
    private sealed class PhotoAssembly
    {
        public string Token;public byte[] Bytes;public int Next;public float Expires;
    }
    private sealed class ReceivedAlbum
    {
        public int Version=-1,Count;
        public readonly Texture2D[] Images=new Texture2D[PhotoAlbum.Capacity];
        public readonly PhotoAssembly[] Parts=new PhotoAssembly[PhotoAlbum.Capacity];
    }
    private sealed class PhotoToSend { public int Round;public byte[] Bytes;public string Token; }
    private readonly Queue<PhotoToSend> photoUploads=new Queue<PhotoToSend>();
    private readonly Dictionary<string,PhotoAssembly> incomingPhotos=new Dictionary<string,PhotoAssembly>();
    private readonly Dictionary<string,List<byte[]>> serverPhotos=new Dictionary<string,List<byte[]>>();
    private readonly Dictionary<string,int> photoVersions=new Dictionary<string,int>(),acceptedPhotos=new Dictionary<string,int>();
    private readonly Dictionary<string,ReceivedAlbum> receivedAlbums=new Dictionary<string,ReceivedAlbum>();
    private readonly Dictionary<NetworkConnection,Coroutine> photoDownloads=new Dictionary<NetworkConnection,Coroutine>();
    private readonly HashSet<NetworkConnection> downloadAgain=new HashSet<NetworkConnection>();
    private Coroutine photoUploadRoutine;

    private void RegisterRoundPhotos()
    {
        manager.ServerManager.RegisterBroadcast<RoundPhotoUpload>(OnPhotoUpload);
        manager.ServerManager.RegisterBroadcast<RoundPhotoRequest>(OnPhotoRequest);
        manager.ClientManager.RegisterBroadcast<RoundPhotoManifest>(OnPhotoManifest);
        manager.ClientManager.RegisterBroadcast<RoundPhotoPart>(OnPhotoPart);
    }
    private void DisposeRoundPhotos()
    {
        manager.ServerManager.UnregisterBroadcast<RoundPhotoUpload>(OnPhotoUpload);
        manager.ServerManager.UnregisterBroadcast<RoundPhotoRequest>(OnPhotoRequest);
        manager.ClientManager.UnregisterBroadcast<RoundPhotoManifest>(OnPhotoManifest);
        manager.ClientManager.UnregisterBroadcast<RoundPhotoPart>(OnPhotoPart);
        ResetRoundPhotos();
    }
    public IReadOnlyList<Texture2D> RoundPhotos(string player)
    {
        if(Offline)return PhotoAlbum.Instance?.Photos??Array.Empty<Texture2D>();
        return player!=null && receivedAlbums.TryGetValue(player,out var album)?album.Images.Where(p=>p!=null).ToArray():Array.Empty<Texture2D>();
    }
    public int RoundPhotoCount(string player)=>Offline?PhotoAlbum.Instance?.Count??0:
        player!=null && receivedAlbums.TryGetValue(player,out var album)?album.Count:0;
    public bool RoundAlbumReceived(string player)=>Offline||player!=null && receivedAlbums.ContainsKey(player);

    public void ShareRoundPhoto(Texture2D image)
    {
        if(Offline || image==null || !manager.ClientManager.Started || Snapshot.players.Any(p=>p.id==identity && p.spectator) ||
            (Snapshot.phase!=SessionPhase.Round && Snapshot.phase!=SessionPhase.Results))return;
        byte[] bytes=PhotoAlbum.EncodePhoto(image);
        if(bytes.Length>MaxPhotoBytes)return;
        photoUploads.Enqueue(new PhotoToSend{Round=Snapshot.round,Bytes=bytes,Token=Guid.NewGuid().ToString("N")});
        if(photoUploadRoutine==null)photoUploadRoutine=StartCoroutine(UploadRoundPhotos());
    }
    private IEnumerator UploadRoundPhotos()
    {
        // Assign the Coroutine before the first packet; a fast host can finish a tiny upload in one frame.
        yield return null;
        while(photoUploads.Count>0 && manager.ClientManager.Started)
        {
            var photo=photoUploads.Dequeue();int chunks=0;
            for(int offset=0;offset<photo.Bytes.Length;offset+=PhotoChunkSize)
            {
                if(Snapshot.round!=photo.Round)break;
                byte[] bytes=SlicePhoto(photo.Bytes,offset);
                manager.ClientManager.Broadcast(new RoundPhotoUpload{round=photo.Round,token=photo.Token,total=photo.Bytes.Length,offset=offset,bytes=bytes},Channel.Reliable);
                if(++chunks%4==0)yield return null;
            }
        }
        photoUploadRoutine=null;
    }
    private static byte[] SlicePhoto(byte[] source,int offset)
    {var bytes=new byte[Math.Min(PhotoChunkSize,source.Length-offset)];Buffer.BlockCopy(source,offset,bytes,0,bytes.Length);return bytes;}
    private static bool ValidPart(int total,int offset,byte[] bytes)=>total>0 && total<=MaxPhotoBytes && offset>=0 &&
        bytes!=null && bytes.Length>0 && bytes.Length<=PhotoChunkSize && offset<=total-bytes.Length;

    private void OnPhotoUpload(NetworkConnection connection,RoundPhotoUpload message,Channel channel)
    {
        if(!hosting || message.round!=round || (phase!=SessionPhase.Round && phase!=SessionPhase.Results) ||
            !connections.TryGetValue(connection,out var member) || member.Spectator || member.Avatar==null ||
            message.token==null || message.token.Length!=32 || !ValidPart(message.total,message.offset,message.bytes))return;
        acceptedPhotos.TryGetValue(member.Id,out int accepted);
        if(accepted>=member.Avatar.GetComponent<PlayerPhotoCamera>().Shots)return;
        incomingPhotos.TryGetValue(member.Id,out var assembly);
        if(message.offset==0)
        {
            assembly=new PhotoAssembly{Token=message.token,Bytes=new byte[message.total],Expires=Time.unscaledTime+10};
            incomingPhotos[member.Id]=assembly;
        }
        if(assembly==null || assembly.Token!=message.token || assembly.Expires<Time.unscaledTime ||
            assembly.Bytes.Length!=message.total || assembly.Next!=message.offset)return;
        Buffer.BlockCopy(message.bytes,0,assembly.Bytes,message.offset,message.bytes.Length);assembly.Next+=message.bytes.Length;
        if(assembly.Next!=assembly.Bytes.Length)return;
        incomingPhotos.Remove(member.Id);
        var image=DecodeRoundPhoto(assembly.Bytes);
        if(image==null)return;
        Destroy(image);
        acceptedPhotos[member.Id]=accepted+1;
        if(!serverPhotos.TryGetValue(member.Id,out var photos))serverPhotos[member.Id]=photos=new List<byte[]>();
        if(photos.Count==PhotoAlbum.Capacity)photos.RemoveAt(0);
        photos.Add(assembly.Bytes);photoVersions.TryGetValue(member.Id,out int version);photoVersions[member.Id]=version+1;
        if(phase==SessionPhase.Results)foreach(var peer in connections.Keys.ToArray())QueueAlbumDownload(peer);
    }
    private static Texture2D DecodeRoundPhoto(byte[] bytes)
    {
        if(bytes.Length<4 || bytes[0]!=255 || bytes[1]!=216)return null;
        // Check the JPEG frame dimensions before decoding; uploads may only contain our tiny camera frames.
        bool frame=false;
        for(int offset=2;offset+8<bytes.Length;)
        {
            if(bytes[offset++]!=255)return null;
            while(offset<bytes.Length && bytes[offset]==255)offset++;
            if(offset+2>=bytes.Length)return null;
            byte marker=bytes[offset++];
            if(marker==217 || marker==218)break;
            int length=(bytes[offset]<<8)|bytes[offset+1];
            if(length<2 || offset+length>bytes.Length)return null;
            if(marker==192 || marker==194)
            {
                if(length<8 || !ValidPhotoSize((bytes[offset+5]<<8)|bytes[offset+6], (bytes[offset+3]<<8)|bytes[offset+4]))return null;
                frame=true;break;
            }
            offset+=length;
        }
        if(!frame)return null;
        var texture=new Texture2D(2,2,TextureFormat.RGB24,false);
        if(!texture.LoadImage(bytes,false) || !ValidPhotoSize(texture.width,texture.height)){Destroy(texture);return null;}
        texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;return texture;
    }
    public static Texture2D DecodePhoto(byte[] bytes) => bytes != null && bytes.Length <= MaxPhotoBytes ? DecodeRoundPhoto(bytes) : null;
    private static bool ValidPhotoSize(int width,int height) => width == PhotoAlbum.PhotoWidth && height == PhotoAlbum.PhotoHeight || width == 300 && height == 200;
    private void OnPhotoRequest(NetworkConnection connection,RoundPhotoRequest message,Channel channel)
    {if(hosting && phase==SessionPhase.Results && message.round==round && connections.ContainsKey(connection))QueueAlbumDownload(connection);}
    private void QueueAlbumDownload(NetworkConnection connection)
    {
        if(!connection.IsActive)return;
        if(photoDownloads.ContainsKey(connection)){downloadAgain.Add(connection);return;}
        photoDownloads[connection]=StartCoroutine(DownloadRoundPhotos(connection));
    }
    private IEnumerator DownloadRoundPhotos(NetworkConnection connection)
    {
        yield return null;
        do
        {
            downloadAgain.Remove(connection);
            if(phase!=SessionPhase.Results || roundResults==null || !connection.IsActive)break;
            int currentRound=round,chunks=0;
            foreach(var entry in roundResults.entries)
            {
                byte[][] photos=serverPhotos.TryGetValue(entry.id,out var stored)?stored.ToArray():Array.Empty<byte[]>();
                photoVersions.TryGetValue(entry.id,out int version);
                manager.ServerManager.Broadcast(connection,new RoundPhotoManifest{round=currentRound,player=entry.id,version=version,count=photos.Length},true,Channel.Reliable);
                for(int slot=0;slot<photos.Length;slot++)
                    for(int offset=0;offset<photos[slot].Length;offset+=PhotoChunkSize)
                    {
                        if(phase!=SessionPhase.Results || round!=currentRound || !connection.IsActive){photoDownloads.Remove(connection);yield break;}
                        manager.ServerManager.Broadcast(connection,new RoundPhotoPart{round=currentRound,player=entry.id,version=version,slot=slot,total=photos[slot].Length,offset=offset,bytes=SlicePhoto(photos[slot],offset)},true,Channel.Reliable);
                        if(++chunks%4==0)yield return null;
                    }
            }
        }while(downloadAgain.Contains(connection));
        downloadAgain.Remove(connection);photoDownloads.Remove(connection);
    }
    private void ReceivePhotoPhase(SessionSnapshot previous)
    {
        if(Snapshot.round!=previous.round || Snapshot.phase==SessionPhase.Loading && previous.phase!=SessionPhase.Loading ||
            (Snapshot.phase==SessionPhase.Lobby || Snapshot.phase==SessionPhase.Menu) && previous.phase!=Snapshot.phase)ClearReceivedAlbums();
        if(Snapshot.phase==SessionPhase.Results && previous.phase!=SessionPhase.Results)
            manager.ClientManager.Broadcast(new RoundPhotoRequest{round=Snapshot.round},Channel.Reliable);
    }
    private bool ValidAlbumPlayer(string player,int photoRound)=>photoRound==Snapshot.round && Snapshot.phase==SessionPhase.Results &&
        player!=null && Results?.entries.Any(p=>p.id==player)==true;
    private void OnPhotoManifest(RoundPhotoManifest message,Channel channel)
    {
        if(!ValidAlbumPlayer(message.player,message.round) || message.count<0 || message.count>PhotoAlbum.Capacity)return;
        if(!receivedAlbums.TryGetValue(message.player,out var album))receivedAlbums[message.player]=album=new ReceivedAlbum();
        if(album.Version>message.version)return;
        if(album.Version!=message.version){foreach(var image in album.Images)if(image!=null)Destroy(image);Array.Clear(album.Images,0,album.Images.Length);Array.Clear(album.Parts,0,album.Parts.Length);}
        album.Version=message.version;album.Count=message.count;
    }
    private void OnPhotoPart(RoundPhotoPart message,Channel channel)
    {
        if(!ValidAlbumPlayer(message.player,message.round) || !receivedAlbums.TryGetValue(message.player,out var album) ||
            album.Version!=message.version || message.slot<0 || message.slot>=album.Count || album.Images[message.slot]!=null ||
            !ValidPart(message.total,message.offset,message.bytes))return;
        var part=album.Parts[message.slot];
        if(message.offset==0)album.Parts[message.slot]=part=new PhotoAssembly{Bytes=new byte[message.total]};
        if(part==null || part.Bytes.Length!=message.total || part.Next!=message.offset)return;
        Buffer.BlockCopy(message.bytes,0,part.Bytes,message.offset,message.bytes.Length);part.Next+=message.bytes.Length;
        if(part.Next==part.Bytes.Length){album.Images[message.slot]=DecodeRoundPhoto(part.Bytes);album.Parts[message.slot]=null;}
    }
    private void ClearReceivedAlbums()
    {foreach(var album in receivedAlbums.Values)foreach(var image in album.Images)if(image!=null)Destroy(image);receivedAlbums.Clear();}
    private void ResetRoundPhotos()
    {
        if(photoUploadRoutine!=null)StopCoroutine(photoUploadRoutine);photoUploadRoutine=null;photoUploads.Clear();
        foreach(var routine in photoDownloads.Values)if(routine!=null)StopCoroutine(routine);
        photoDownloads.Clear();downloadAgain.Clear();incomingPhotos.Clear();serverPhotos.Clear();photoVersions.Clear();acceptedPhotos.Clear();ClearReceivedAlbums();
    }
}
