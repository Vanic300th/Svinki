using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

// No claimed player identity: the host derives the owner from the authenticated room connection.
public struct SavedLockerUpload : IBroadcast
{ public int slot, total, offset; public string token; public bool restore; public byte[] bytes; }
public sealed partial class NetworkLobby
{
    private sealed class SavedUpload { public int Slot; public bool Restore; public string Token; public byte[] Bytes; }
    private sealed class SavedAssembly { public PhotoAssembly Photo; public int Slot; public bool Restore; }
    private readonly Queue<SavedUpload> savedUploadQueue = new Queue<SavedUpload>();
    private readonly Dictionary<NetworkConnection, SavedAssembly> savedAssemblies = new Dictionary<NetworkConnection, SavedAssembly>();
    private readonly Dictionary<NetworkConnection, float> nextSavedUpload = new Dictionary<NetworkConnection, float>();
    private readonly Dictionary<NetworkConnection, HashSet<int>> restoredSlots = new Dictionary<NetworkConnection, HashSet<int>>();
    private Coroutine savedUploadRoutine, savedRestoreRoutine;
    private readonly Dictionary<int,string> pendingLocalPins = new Dictionary<int,string>();
    private void RegisterSavedPhotos() => manager.ServerManager.RegisterBroadcast<SavedLockerUpload>(OnSavedLockerUpload);
    private void DisposeSavedPhotos() => manager.ServerManager.UnregisterBroadcast<SavedLockerUpload>(OnSavedLockerUpload);
    private void ClearSavedPhotoTransfers()
    {
        if (savedUploadRoutine != null) StopCoroutine(savedUploadRoutine); if (savedRestoreRoutine != null) StopCoroutine(savedRestoreRoutine);
        savedUploadRoutine = savedRestoreRoutine = null; savedUploadQueue.Clear(); savedAssemblies.Clear(); nextSavedUpload.Clear(); restoredSlots.Clear(); pendingLocalPins.Clear();
    }
    private PlayerLocker OwnLocker(string id)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(FriendslopSceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;
        return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerLocker>(true)).FirstOrDefault(l => l.OwnerId == id);
    }
    public void AssignSavedLockerPhoto(PlayerLocker locker, PlayerAvatar player, int slot, int photo)
    {
        if (locker == null || !locker.IsMine(player) || slot < 0 || slot >= 6 || PhotoAlbum.Instance == null) return;
        byte[] jpeg = PhotoAlbum.Instance.SavedJpeg(photo); if (jpeg == null) return;
        if (Offline) { if (locker.Validate(player)) PublishLockerPhoto("offline", slot, jpeg); }
        else if (manager.ClientManager.Started) QueueSavedUpload(slot, jpeg, false);
    }
    public void ClearSavedLockerPhoto(PlayerLocker locker, PlayerAvatar player, int slot)
    {
        if (locker == null || !locker.IsMine(player) || slot < 0 || slot >= 6) return;
        if (Offline) { if (locker.Validate(player)) PublishLockerPhoto("offline", slot, null); }
        else if (manager.ClientManager.Started) QueueSavedUpload(slot, null, false);
    }
    private void QueueSavedUpload(int slot, byte[] jpeg, bool restore)
    {
        if (savedUploadQueue.Count >= 12 || jpeg != null && jpeg.Length > MaxPhotoBytes) return;
        pendingLocalPins[slot] = jpeg == null ? "" : PhotoDatabase.ContentId(jpeg);
        savedUploadQueue.Enqueue(new SavedUpload { Slot = slot, Bytes = jpeg, Restore = restore, Token = Guid.NewGuid().ToString("N") });
        if (savedUploadRoutine == null) savedUploadRoutine = StartCoroutine(SendSavedUploads());
    }
    private IEnumerator SendSavedUploads()
    {
        yield return null;
        while (savedUploadQueue.Count > 0 && manager.ClientManager.Started)
        {
            var upload = savedUploadQueue.Dequeue(); var jpeg = upload.Bytes; int chunks = 0;
            if (jpeg == null) manager.ClientManager.Broadcast(new SavedLockerUpload { slot = upload.Slot, token = upload.Token, restore = upload.Restore, total = 0, bytes = Array.Empty<byte>() }, Channel.Reliable);
            else for (int offset = 0; offset < jpeg.Length && manager.ClientManager.Started; offset += PhotoChunkSize)
            {
                manager.ClientManager.Broadcast(new SavedLockerUpload { slot = upload.Slot, token = upload.Token, restore = upload.Restore, total = jpeg.Length, offset = offset, bytes = SlicePhoto(jpeg, offset) }, Channel.Reliable);
                if (++chunks % 4 == 0) yield return null;
            }
            yield return new WaitForSecondsRealtime(1.05f);
        }
        savedUploadRoutine = null;
    }
    private void BeginSavedLockerRestore()
    { if (!Offline && savedRestoreRoutine == null) savedRestoreRoutine = StartCoroutine(RestoreSavedLocker()); }
    private IEnumerator RestoreSavedLocker()
    {
        yield return null;
        for (int attempt = 0; attempt < 80 && manager.ClientManager.Started; attempt++)
        {
            if (Snapshot.phase == SessionPhase.Lobby && OwnLocker(identity) != null && PhotoAlbum.Instance != null)
            {
                var pins = PhotoAlbum.Instance.SavedLockerPhotos();
                for (int slot = 0; slot < 6; slot++) { pins.TryGetValue(slot, out var jpeg); QueueSavedUpload(slot, jpeg, true); }
                break;
            }
            yield return new WaitForSecondsRealtime(.25f);
        }
        savedRestoreRoutine = null;
    }
    private void RestoreOfflineLockerPhotos()
    {
        if (!Offline || PhotoAlbum.Instance == null) return;
        foreach (var pin in PhotoAlbum.Instance.SavedLockerPhotos()) PublishLockerPhoto("offline", pin.Key, pin.Value, false);
    }
    private void OnSavedLockerUpload(NetworkConnection connection, SavedLockerUpload message, Channel channel)
    {
        if (!hosting || phase != SessionPhase.Lobby || !connections.TryGetValue(connection, out var member) || member.Spectator ||
            member.Avatar == null || message.slot < 0 || message.slot >= 6 || message.token == null || message.token.Length != 32 ||
            message.total != 0 && !ValidPart(message.total, message.offset, message.bytes) ||
            message.total == 0 && (message.offset != 0 || message.bytes == null || message.bytes.Length != 0)) return;
        var locker = OwnLocker(member.Id); var player = member.Avatar.GetComponent<PlayerAvatar>();
        if (locker == null || !locker.IsMine(player) || !message.restore && !locker.Validate(player)) return;
        if (message.restore && restoredSlots.TryGetValue(connection, out var slots) && slots.Contains(message.slot)) return;
        if (message.offset == 0)
        {
            if (nextSavedUpload.TryGetValue(connection, out float at) && Time.unscaledTime < at) return;
            nextSavedUpload[connection] = Time.unscaledTime + 1;
            savedAssemblies[connection] = new SavedAssembly { Slot = message.slot, Restore = message.restore,
                Photo = new PhotoAssembly { Token = message.token, Bytes = new byte[message.total], Expires = Time.unscaledTime + 15 } };
        }
        if (!savedAssemblies.TryGetValue(connection, out var assembly) || assembly.Slot != message.slot || assembly.Restore != message.restore ||
            assembly.Photo.Token != message.token || assembly.Photo.Expires < Time.unscaledTime || assembly.Photo.Next != message.offset || assembly.Photo.Bytes.Length != message.total) return;
        if (message.total > 0) { Buffer.BlockCopy(message.bytes, 0, assembly.Photo.Bytes, message.offset, message.bytes.Length); assembly.Photo.Next += message.bytes.Length; }
        if (assembly.Photo.Next != message.total) return;
        savedAssemblies.Remove(connection); byte[] jpeg = message.total == 0 ? null : assembly.Photo.Bytes;
        if (jpeg != null) { var image = DecodeRoundPhoto(jpeg); if (image == null) return; Destroy(image); }
        if (message.restore) { if (!restoredSlots.TryGetValue(connection, out var restored)) restoredSlots[connection] = restored = new HashSet<int>(); restored.Add(message.slot); }
        PublishLockerPhoto(member.Id, message.slot, jpeg);
    }
    private void PublishLockerPhoto(string player, int slot, byte[] bytes, bool persist = true)
    {
        string key = LockerKey(player, slot); lockerStore.Put(key, bytes); int revision = ++lockerRevision;
        if (Offline) { SetLockerImage(key, bytes); if (persist) PhotoAlbum.Instance?.CommitLockerPhoto(slot, bytes); return; }
        foreach (var connection in connections.Keys.ToArray()) StartCoroutine(SendLockerPhoto(connection, player, slot, revision, bytes));
    }
    private void PersistOwnLockerImage(string player, int slot, byte[] bytes)
    {
        // An initial room snapshot must never overwrite this computer's pins before restoration.
        if (Offline || string.IsNullOrEmpty(identity) || player != identity || !pendingLocalPins.TryGetValue(slot,out var expected) ||
            expected != (bytes == null ? "" : PhotoDatabase.ContentId(bytes))) return;
        pendingLocalPins.Remove(slot); PhotoAlbum.Instance?.CommitLockerPhoto(slot, bytes);
    }
}
