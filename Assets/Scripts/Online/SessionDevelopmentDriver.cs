#if UNITY_EDITOR || DEBUG
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public string cart, cartRole;
        public CartState[] carts;
        public PickupState[] pickups;
        public Vector3 localPosition; public Vector3 eyePosition; public Vector3 eyeForward;
    }
    [Serializable] private sealed class CartState { public string name, driver, rider; public Vector3 position; public float speed; }
    [Serializable] private sealed class PickupState { public string name; public Vector3 position; public Quaternion rotation; }
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
                case "warp-cart":
                    var cart = ShoppingCart.All.FirstOrDefault(c => c.name == command.target);
                    var avatar = PlayerRegistry.Players.FirstOrDefault(p => p.IsLocal);
                    if (cart != null && avatar != null)
                    {
                        var cc = avatar.GetComponent<CharacterController>(); cc.enabled = false;
                        avatar.transform.position = cart.transform.position - cart.transform.forward * 2.5f + Vector3.up * .05f; cc.enabled = true;
                        var view = avatar.EyeCamera.GetComponent<GrayboxFirstPersonCamera>();
                        var rotation = Quaternion.LookRotation(cart.transform.position + Vector3.up * .8f - (avatar.Position + Vector3.up * 1.65f + cart.transform.forward * .22f)).eulerAngles;
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        typeof(GrayboxFirstPersonCamera).GetField("yaw", flags).SetValue(view, rotation.y);
                        typeof(GrayboxFirstPersonCamera).GetField("pitch", flags).SetValue(view, Mathf.DeltaAngle(0, rotation.x));
                    }
                    break;
                case "cart-push":
                case "cart-sit":
                case "cart-launch":
                case "cart-exit":
                    var requested = ShoppingCart.All.FirstOrDefault(c => c.name == command.target);
                    PlayerRegistry.Players.FirstOrDefault(p => p.IsLocal)?.GetComponent<NetworkPlayer>()?.RequestCart(requested, command.action == "cart-sit", command.action == "cart-launch");
                    break;
                case "warp-finish":
                    var localAvatar = PlayerRegistry.Players.FirstOrDefault(p => p.IsLocal);
                    var finish = localAvatar != null ? RoundFinishStation.ForScene(localAvatar.gameObject.scene) : null;
                    if (finish != null)
                    { var controller = localAvatar.GetComponent<CharacterController>(); controller.enabled = false; localAvatar.transform.position = finish.Center; controller.enabled = true; }
                    break;
                case "reaction": lobby.RunwayReact(command.target); break;
                case "pose": if (int.TryParse(command.target, out int pose)) lobby.RunwayPose(pose); break;
                case "capture": ScreenCapture.CaptureScreenshot(path + "-capture.png"); break;
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
            cart = ShoppingCart.For(local)?.name, cartRole = ShoppingCart.For(local) is ShoppingCart occupied ? occupied.Driver == local ? "driver" : "rider" : null,
            carts = ShoppingCart.All.Select(c => new CartState { name=c.name, position=c.transform.position, speed=c.Speed, driver=c.Driver?.GetComponent<NetworkPlayer>()?.ParticipantName, rider=c.Rider?.GetComponent<NetworkPlayer>()?.ParticipantName }).ToArray(),
            pickups = ClothingPickup.All.Select(p => new PickupState { name=p.name, position=p.transform.position, rotation=p.transform.rotation }).ToArray(),
            localPosition = local != null ? local.Position : Vector3.zero,
            eyePosition = eye != null ? eye.transform.position : Vector3.zero,
            eyeForward = eye != null ? eye.transform.forward : Vector3.forward });
        File.WriteAllText(path + ".state.tmp", json);
        if (File.Exists(path + ".state")) File.Delete(path + ".state");
        File.Move(path + ".state.tmp", path + ".state");
    }
}
#endif
