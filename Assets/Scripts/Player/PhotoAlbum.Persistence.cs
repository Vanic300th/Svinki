using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed partial class PhotoAlbum
{
    private PhotoDatabase database;
    private List<PhotoDatabase.Entry> saved;
    private readonly Dictionary<string, Texture2D> savedTextures = new Dictionary<string, Texture2D>();
    private bool archive;
    public string PersistenceError { get; private set; }
    public int SavedCount { get { EnsureDatabase(); return saved?.Count ?? 0; } }
    public static string DatabasePath => Path.Combine(Application.persistentDataPath, "Photos", "photos.sqlite");
    private void EnsureDatabase()
    {
        if (database != null || PersistenceError != null) return;
        try
        {
            // Match the existing --profile test isolation; normal Editor/build share the player's local archive.
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--profile");
            string suffix = at >= 0 && at + 1 < args.Length ? args[at + 1] : "default";
            string key = "svinki.photos.profile." + suffix, profile = PlayerPrefs.GetString(key, "");
            if (profile.Length == 0) { profile = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(key, profile); PlayerPrefs.Save(); }
            database = new PhotoDatabase(DatabasePath, profile); saved = database.Read();
        }
        catch (Exception error) { database?.Dispose(); database = null; PersistenceError = "Could not save photo archive: " + error.Message; Debug.LogError(PersistenceError); }
    }
    private void StoreCapture(Texture2D image)
    {
        EnsureDatabase(); if (database == null) return;
        try { database.Save(PhotoAlbum.EncodePhoto(image)); ReloadArchive(); }
        catch (Exception error) { PersistenceError = "Could not save photo: " + error.Message; Debug.LogError(PersistenceError); }
    }
    private void ReloadArchive()
    {
        saved = database.Read(); var kept = new HashSet<string>(); foreach (var entry in saved) kept.Add(entry.Id);
        var removed = new List<string>(); foreach (var pair in savedTextures) if (!kept.Contains(pair.Key)) { Destroy(pair.Value); removed.Add(pair.Key); }
        foreach (string key in removed) savedTextures.Remove(key);
    }
    public byte[] SavedJpeg(int index) { EnsureDatabase(); return saved != null && index >= 0 && index < saved.Count ? saved[index].Jpeg : null; }
    public Texture2D SavedPhoto(int index)
    {
        var jpeg = SavedJpeg(index); if (jpeg == null) return null; string id = saved[index].Id;
        if (!savedTextures.TryGetValue(id, out var image))
        {
            image = NetworkLobby.DecodePhoto(jpeg); if (image == null) return null; savedTextures[id] = image;
        }
        return image;
    }
    public Dictionary<int, byte[]> SavedLockerPhotos()
    {
        EnsureDatabase(); try { return database?.ReadPins() ?? new Dictionary<int, byte[]>(); }
        catch (Exception error) { PersistenceError = error.Message; Debug.LogError("Photo pins: " + error.Message); return new Dictionary<int, byte[]>(); }
    }
    public void CommitLockerPhoto(int slot, byte[] jpeg)
    {
        EnsureDatabase(); if (database == null) return;
        try { string id = jpeg == null ? null : database.Save(jpeg); database.Pin(slot, id); ReloadArchive(); }
        catch (Exception error) { PersistenceError = "Could not save locker photo: " + error.Message; Debug.LogError(PersistenceError); }
    }
    private void DisposeArchive()
    { foreach (var image in savedTextures.Values) if (image != null) Destroy(image); savedTextures.Clear(); database?.Dispose(); database = null; }
    public void ToggleArchive() { archive = !archive; selected = 0; EnsureDatabase(); Refresh(); }
}
