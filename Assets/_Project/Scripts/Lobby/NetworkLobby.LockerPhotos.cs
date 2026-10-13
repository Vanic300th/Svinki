using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

public struct LockerPhotoRequest : IBroadcast { }
public struct LockerPhotoPart : IBroadcast { public string player; public int slot, revision, total, offset; public byte[] bytes; }
public sealed partial class NetworkLobby
{
    private ILockerPhotoStore lockerStore = new SessionLockerPhotoStore();
    private readonly Dictionary<string, Texture2D> lockerImages = new Dictionary<string, Texture2D>();
    private readonly Dictionary<string, PhotoAssembly> lockerParts = new Dictionary<string, PhotoAssembly>();
    private readonly Dictionary<string, int> lockerRevisions = new Dictionary<string, int>();
    private readonly Dictionary<NetworkConnection, float> nextLockerDownload = new Dictionary<NetworkConnection, float>();
    private readonly HashSet<NetworkConnection> downloadingLockers = new HashSet<NetworkConnection>();
    private int lockerRevision;
    private static string LockerKey(string player, int slot) => player + ":" + slot;
    public Texture2D LockerPhoto(string player, int slot) => lockerImages.TryGetValue(LockerKey(player, slot), out var image) ? image : null;
    public void RequestLockerPhotos() { if (!Offline && manager.ClientManager.Started) { manager.ClientManager.Broadcast(new LockerPhotoRequest()); BeginSavedLockerRestore(); } }
    private void RegisterLockerPhotos()
    {
        manager.ServerManager.RegisterBroadcast<LockerPhotoRequest>(OnLockerPhotoRequest);
        manager.ClientManager.RegisterBroadcast<LockerPhotoPart>(OnLockerPhotoPart);
        RegisterSavedPhotos();
    }
    private void PruneLockerPhotos()
    {
        foreach (var key in lockerImages.Keys.Concat(lockerParts.Keys).Concat(lockerStore.Entries.Select(e=>e.Key)).Distinct().ToArray())
        {
            int separator = key.LastIndexOf(':');
            if (separator < 0 || Snapshot.players.Any(p => p.id == key.Substring(0, separator))) continue;
            if (lockerImages.TryGetValue(key, out var image) && image != null) Destroy(image);
            lockerImages.Remove(key); lockerParts.Remove(key); lockerRevisions.Remove(key);
            lockerStore.Put(key,null);
        }
        foreach (var connection in nextLockerDownload.Keys.Where(c => !c.IsActive).ToArray()) nextLockerDownload.Remove(connection);
        foreach (var connection in nextSavedUpload.Keys.Where(c => !c.IsActive).ToArray()) { nextSavedUpload.Remove(connection); savedAssemblies.Remove(connection); restoredSlots.Remove(connection); }
    }
    private void DisposeLockerPhotos()
    {
        manager.ServerManager.UnregisterBroadcast<LockerPhotoRequest>(OnLockerPhotoRequest);
        manager.ClientManager.UnregisterBroadcast<LockerPhotoPart>(OnLockerPhotoPart);
        DisposeSavedPhotos();
        ClearLockerPhotos();
    }
    private void ClearLockerPhotos()
    {
        foreach (var image in lockerImages.Values) if (image != null) Destroy(image);
        lockerImages.Clear(); lockerParts.Clear(); lockerRevisions.Clear(); lockerStore.Clear();
        nextLockerDownload.Clear(); downloadingLockers.Clear();
        ClearSavedPhotoTransfers();
    }
    public void PinLockerPhoto(string player, int slot, int photo)
    {
        // Only called after PlayerLocker checks the requester's identity and proximity.
        if (!Offline && !hosting || slot < 0 || slot >= 6 || photo < -1) return;
        byte[] bytes = null;
        if (photo >= 0)
        {
            if (Offline)
            {
                if (PhotoAlbum.Instance == null || photo >= PhotoAlbum.Instance.Count) return;
                bytes = PhotoAlbum.EncodePhoto(PhotoAlbum.Instance.Photos[photo]);
            }
            else
            {
                if (!serverPhotos.TryGetValue(player, out var album) || photo >= album.Count) return;
                bytes = album[photo];
            }
        }
        PublishLockerPhoto(player, slot, bytes);
    }
    private void OnLockerPhotoRequest(NetworkConnection connection, LockerPhotoRequest message, Channel channel)
    {
        if (!hosting || !connections.ContainsKey(connection) || downloadingLockers.Contains(connection) ||
            nextLockerDownload.TryGetValue(connection, out float deadline) && Time.unscaledTime < deadline) return;
        nextLockerDownload[connection] = Time.unscaledTime + 3; downloadingLockers.Add(connection);
        StartCoroutine(SendLockerAlbum(connection));
    }
    private IEnumerator SendLockerAlbum(NetworkConnection connection)
    {
        int revision = lockerRevision;
        foreach (var entry in lockerStore.Entries.ToArray())
        {
            int separator = entry.Key.LastIndexOf(':');
            yield return SendLockerPhoto(connection, entry.Key.Substring(0, separator), int.Parse(entry.Key.Substring(separator + 1)), revision, entry.Value);
        }
        downloadingLockers.Remove(connection);
    }
    private IEnumerator SendLockerPhoto(NetworkConnection connection, string player, int slot, int revision, byte[] bytes)
    {
        yield return null;
        if (!connection.IsActive) yield break;
        if (bytes == null) { manager.ServerManager.Broadcast(connection, new LockerPhotoPart { player = player, slot = slot, revision = revision, total = 0, bytes = Array.Empty<byte>() }); yield break; }
        for (int offset = 0, count = 0; offset < bytes.Length && connection.IsActive; offset += PhotoChunkSize)
        {
            manager.ServerManager.Broadcast(connection, new LockerPhotoPart { player = player, slot = slot, revision = revision,
                total = bytes.Length, offset = offset, bytes = SlicePhoto(bytes, offset) });
            if (++count % 4 == 0) yield return null;
        }
    }
    private void OnLockerPhotoPart(LockerPhotoPart message, Channel channel)
    {
        if (message.player == null || message.player.Length > 64 || message.slot < 0 || message.slot >= 6 ||
            !Snapshot.players.Any(p => p.id == message.player)) return;
        string key = LockerKey(message.player, message.slot);
        if (lockerRevisions.TryGetValue(key, out int revision) && revision > message.revision) return;
        if (message.total == 0) { lockerRevisions[key] = message.revision; SetLockerImage(key, null); PersistOwnLockerImage(message.player,message.slot,null); lockerParts.Remove(key); return; }
        if (!ValidPart(message.total, message.offset, message.bytes)) return;
        lockerParts.TryGetValue(key, out var assembly); string token = message.revision.ToString();
        if (message.offset == 0) { lockerRevisions[key] = message.revision; }
        if (message.offset == 0) assembly = lockerParts[key] = new PhotoAssembly { Token = token, Bytes = new byte[message.total], Expires = Time.unscaledTime + 15 };
        if (assembly == null || assembly.Token != token || assembly.Expires < Time.unscaledTime || assembly.Next != message.offset || assembly.Bytes.Length != message.total) return;
        Buffer.BlockCopy(message.bytes, 0, assembly.Bytes, message.offset, message.bytes.Length); assembly.Next += message.bytes.Length;
        if (assembly.Next != assembly.Bytes.Length) return;
        lockerRevisions[key] = message.revision; SetLockerImage(key, assembly.Bytes); lockerParts.Remove(key);
        PersistOwnLockerImage(message.player,message.slot,assembly.Bytes);
    }
    private void SetLockerImage(string key, byte[] bytes)
    {
        var texture = bytes != null ? DecodeRoundPhoto(bytes) : null;
        if (bytes != null && texture == null) return;
        if (lockerImages.TryGetValue(key, out var old) && old != null) Destroy(old);
        if (texture == null) lockerImages.Remove(key); else lockerImages[key] = texture;
    }
}
