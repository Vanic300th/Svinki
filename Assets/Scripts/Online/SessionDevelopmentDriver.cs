#if UNITY_EDITOR || DEBUG
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
// Explicit opt-in, development builds only. Files allow a repeatable multi-process integration test.
public sealed class SessionDevelopmentDriver : MonoBehaviour
{
    [Serializable] private sealed class Command { public string action; public string target; }
    [Serializable] private sealed class State
    {
        public SessionSnapshot snapshot; public string status; public string identity;
        public int avatars; public bool offline;
        public string carriedMannequin;
        public bool knockedDown; public float cameraHeight;
        public Vector3 localPosition; public Vector3 eyePosition; public Vector3 eyeForward;
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
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(commandPath)); }
            catch (ArgumentException) { return; } // A writer may not have finished the test command yet.
            catch (IOException) { return; }
            if (command == null || string.IsNullOrEmpty(command.action)) return;
            File.Delete(commandPath);
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
                case "key-down":
                    if (Keyboard.current != null && Enum.TryParse(command.target, true, out Key key))
                        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
                    break;
                case "key-up":
                    if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    break;
                case "mouse-left":
                case "mouse-right":
                case "mouse-up":
                    if (Mouse.current != null)
                        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = Mouse.current.position.ReadValue(),
                            buttons = (ushort)(command.action == "mouse-left" ? 1 : command.action == "mouse-right" ? 2 : 0) });
                    break;
            }
        }
        PlayerAvatar local = null;
        foreach (PlayerAvatar player in PlayerRegistry.Players) if (player != null && player.IsLocal) { local = player; break; }
        Camera eye = local != null ? local.EyeCamera : null;
        string json = JsonUtility.ToJson(new State { snapshot = lobby.Snapshot, status = lobby.Status, identity = lobby.Identity,
            avatars = PlayerRegistry.Players.Count, offline = lobby.Offline,
            carriedMannequin = local != null ? local.GetComponent<PlayerMannequinCarry>()?.Held?.name : null,
            knockedDown = local != null && local.GetComponent<PlayerKnockdown>()?.IsDown == true,
            cameraHeight = eye != null && local != null ? eye.transform.position.y - local.Position.y : 0,
            localPosition = local != null ? local.Position : Vector3.zero,
            eyePosition = eye != null ? eye.transform.position : Vector3.zero,
            eyeForward = eye != null ? eye.transform.forward : Vector3.forward });
        File.WriteAllText(path + ".state.tmp", json);
        if (File.Exists(path + ".state")) File.Delete(path + ".state");
        File.Move(path + ".state.tmp", path + ".state");
    }
}
#endif
