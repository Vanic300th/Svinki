using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEditor;
using UnityEngine;
public static class CartSessionBackup
{
    [Serializable] public class State { public string nickname; public int appearance; public string[] clothing; public Vector3 position; public bool offline; public string checkpoint; public bool checkpointExists; }
    public static string Capture()
    {
        var lobby=NetworkLobby.Instance;
        if(lobby == null) return "No active session";
        if(!lobby.Offline && (!lobby.IsHost || lobby.Snapshot.players.Length != 1)) throw new Exception("Cannot interrupt a shared session");
        var player=PlayerRegistry.Players.First(p=>p.IsLocal);
        var state=new State { nickname=lobby.Nickname, appearance=lobby.SelectedAppearance, clothing=player.Outfit.Items.Select(c=>c.name).ToArray(), position=player.Position, offline=lobby.Offline,
            checkpointExists=File.Exists(CheckpointStore.DefaultPath), checkpoint=File.Exists(CheckpointStore.DefaultPath)?File.ReadAllText(CheckpointStore.DefaultPath):null };
        File.WriteAllText("ArtSource/CartRunway/session-backup.json",JsonUtility.ToJson(state,true));
        SessionState.SetString("Svinki.EditBackup",JsonUtility.ToJson(state));
        return "Saved single-player session and checkpoint";
    }
    public static string Restore()
    {
        var lobby = NetworkLobby.Instance;
        if (lobby == null || lobby.InSession || lobby.Offline) throw new Exception("Restore only from the isolated menu");
        var state = JsonUtility.FromJson<State>(File.ReadAllText("ArtSource/CartRunway/session-backup.json"));
        if (state.checkpointExists) File.WriteAllText(CheckpointStore.DefaultPath, state.checkpoint);
        else if (File.Exists(CheckpointStore.DefaultPath)) File.Delete(CheckpointStore.DefaultPath);
        lobby.Nickname = state.nickname; PigFace.SaveSelection(state.appearance);
        lobby.StartCoroutine(RestoreGame(state)); return "Restoring player and checkpoint";
    }
    static IEnumerator RestoreGame(State state)
    {
        var lobby = NetworkLobby.Instance;
        if (state.offline) lobby.StartOffline(); else lobby.DebugHost(state.checkpointExists);
        float deadline = Time.realtimeSinceStartup + 30;
        if (!state.offline)
        {
            while (lobby.Snapshot.phase != SessionPhase.Lobby && Time.realtimeSinceStartup < deadline) yield return null;
            lobby.Ready(true); yield return new WaitForSecondsRealtime(.3f); lobby.StartRound();
        }
        while ((!PlayerRegistry.Players.Any(p=>p.IsLocal) || lobby.Busy) && Time.realtimeSinceStartup < deadline) yield return null;
        var player = PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal);
        if (player == null) { SessionState.SetString("Svinki.CartRunway.Restore", "FAILED: player missing"); yield break; }
        var network = player.GetComponent<NetworkPlayer>();
        if (network != null) network.RestoreOutfit(state.clothing);
        else player.Outfit.SetItems(state.clothing.Select(id=>OutfitRatingCatalog.Load().FindClothing(id)).Where(c=>c!=null));
        var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = state.position; cc.enabled = true;
        if (state.checkpointExists) File.WriteAllText(CheckpointStore.DefaultPath, state.checkpoint);
        else if (File.Exists(CheckpointStore.DefaultPath)) File.Delete(CheckpointStore.DefaultPath);
        SessionState.SetString("Svinki.CartRunway.Restore", "restored player, clothing, appearance and original checkpoint");
        SessionState.EraseString("Svinki.EditBackup");
    }
}
