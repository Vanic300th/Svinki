using System;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class SessionCheckpoint
{
    public int version = 1;
    public string scene = "SampleScene";
    public int round = 1;
    public CheckpointPlayer[] players = Array.Empty<CheckpointPlayer>();
    public void Validate()
    {
        if (version != 1 || scene != "SampleScene" || round < 1 || players == null || players.Length < 1 || players.Length > 6 ||
            players.Any(p => p == null || string.IsNullOrEmpty(p.id) || p.outfit == null) ||
            players.Select(p => p.id).Distinct().Count() != players.Length)
            throw new InvalidDataException("Incompatible or damaged checkpoint.");
    }
}
[Serializable]
public sealed class CheckpointPlayer
{
    public string id;
    public string nickname;
    public string[] outfit = Array.Empty<string>();
}
public static class CheckpointStore
{
    public static string DefaultPath => Path.Combine(Application.persistentDataPath, "round-checkpoint.json");
    public static SessionCheckpoint Read(string path = null)
    {
        var checkpoint = JsonUtility.FromJson<SessionCheckpoint>(File.ReadAllText(path ?? DefaultPath));
        if (checkpoint == null) throw new InvalidDataException("The checkpoint is damaged.");
        checkpoint.Validate();
        return checkpoint;
    }
    public static void Write(SessionCheckpoint checkpoint, string path = null)
    {
        checkpoint.Validate();
        path = path ?? DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(JsonUtility.ToJson(checkpoint, true));
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
