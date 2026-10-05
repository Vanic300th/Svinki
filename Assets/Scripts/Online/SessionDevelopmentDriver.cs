#if UNITY_EDITOR || DEBUG
using System;
using System.IO;
using UnityEngine;
// Explicit opt-in, development builds only. Files allow a repeatable multi-process integration test.
public sealed class SessionDevelopmentDriver : MonoBehaviour
{
    [Serializable] private sealed class Command { public string action; public string target; }
    [Serializable] private sealed class State
    {
        public SessionSnapshot snapshot; public string status; public string identity;
        public int avatars; public bool offline;
    }
    private string path;
    private float nextPoll;
    private NetworkLobby lobby;
    private void Start()
    {
        lobby = GetComponent<NetworkLobby>(); string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "--session-test-path");
        if (i >= 0 && i + 1 < args.Length) path = args[i + 1];
    }
    private void Update()
    {
        if (path == null || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + .2f;
        string commandPath = path + ".command";
        if (File.Exists(commandPath))
        {
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(commandPath)); File.Delete(commandPath);
            switch (command.action)
            {
                case "ready": lobby.Ready(true); break;
                case "start": lobby.StartRound(); break;
                case "end": lobby.EndRound(); break;
                case "close": lobby.ToggleAdmission(); break;
                case "kick": lobby.Kick(command.target); break;
                case "leave": lobby.Leave(); break;
                case "disconnect": FishNet.InstanceFinder.ClientManager.StopConnection(); break;
                case "quit": Application.Quit(); break;
            }
        }
        string json = JsonUtility.ToJson(new State { snapshot = lobby.Snapshot, status = lobby.Status, identity = lobby.Identity,
            avatars = PlayerRegistry.Players.Count, offline = lobby.Offline });
        File.WriteAllText(path + ".state.tmp", json);
        if (File.Exists(path + ".state")) File.Delete(path + ".state");
        File.Move(path + ".state.tmp", path + ".state");
    }
}
#endif
