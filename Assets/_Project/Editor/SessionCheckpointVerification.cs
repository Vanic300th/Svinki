#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
public static class SessionCheckpointVerification
{
    public static string Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "svinki-checkpoint-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "round.json"); int checks = 0;
        void Assert(bool condition) { if (!condition) throw new Exception("Checkpoint verification failed at " + checks); checks++; }
        try
        {
            var checkpoint = new SessionCheckpoint { round = 4, players = new[] { new CheckpointPlayer { id = "guest", nickname = "Игрок", outfit = new[] { "hat" } } } };
            CheckpointStore.Write(checkpoint, path);
            var read = CheckpointStore.Read(path); Assert(read.round == 4 && read.players[0].outfit[0] == "hat");
            checkpoint.round = 5; CheckpointStore.Write(checkpoint, path);
            Assert(CheckpointStore.Read(path).round == 5 && !File.Exists(path + ".tmp"));
            checkpoint.version = 99;
            bool rejected = false; try { CheckpointStore.Write(checkpoint, path); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected && CheckpointStore.Read(path).round == 5);
            File.WriteAllText(path, "{broken");
            rejected = false; try { CheckpointStore.Read(path); } catch (Exception) { rejected = true; } Assert(rejected);
            File.WriteAllText(path, "{}");
            rejected = false; try { CheckpointStore.Read(path); } catch (Exception) { rejected = true; } Assert(rejected);
            return checks + " checkpoint checks passed: roundtrip, atomic replacement, incompatible save preservation, malformed JSON, empty save rejection.";
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
#endif
