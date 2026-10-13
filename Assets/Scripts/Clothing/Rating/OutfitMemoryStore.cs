using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Only outfits explicitly selected on the runway are saved, independently of round inventory.</summary>
public static class OutfitMemoryStore
{
    [Serializable] public sealed class Entry { public string id, nickname; public int round, appearance; public string[] clothing, names; }
    [Serializable] private sealed class Archive { public Entry[] entries = Array.Empty<Entry>(); }
    private static List<Entry> entries;
    private static string path;
    public static string Error { get; private set; }
    public static IReadOnlyList<Entry> Entries { get { Ensure(); return entries; } }
    private static void Ensure()
    {
        if (entries != null) return;
        entries = new List<Entry>();
        var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--profile");
        string suffix = at >= 0 && at + 1 < args.Length ? args[at + 1] : "default";
        string key = "svinki.photos.profile." + suffix, profile = PlayerPrefs.GetString(key, "");
        if (profile.Length == 0) { profile = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(key, profile); PlayerPrefs.Save(); }
        path = Path.Combine(Application.persistentDataPath, "Outfits", profile + ".json");
        try { if (File.Exists(path)) entries = (JsonUtility.FromJson<Archive>(File.ReadAllText(path))?.entries ?? Array.Empty<Entry>()).Where(e => e != null && e.clothing != null && e.clothing.Length <= 4).ToList(); }
        catch (Exception e) { Error = "Could not load outfit memories: " + e.Message; }
    }
    public static bool Save(OutfitResultEntry outfit, int round)
    {
        Ensure(); if (outfit == null || outfit.eliminated) return false;
        var ids = outfit.clothing.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        var previous = entries.FirstOrDefault(e => e.appearance == outfit.appearance && e.clothing.OrderBy(c => c, StringComparer.Ordinal).SequenceEqual(ids));
        var next = new List<Entry>(entries); if (previous != null) next.Remove(previous);
        next.Insert(0, new Entry { id = previous?.id ?? Guid.NewGuid().ToString("N"), nickname = outfit.nickname, round = round,
            appearance = outfit.appearance, clothing = (string[])outfit.clothing.Clone(), names = (string[])outfit.names.Clone() });
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(new Archive { entries = next.ToArray() }, true));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            entries = next; Error = null; return true;
        }
        catch (Exception e) { Error = "Could not save outfit memory: " + e.Message; return false; }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() { entries = null; path = null; Error = null; }
}
