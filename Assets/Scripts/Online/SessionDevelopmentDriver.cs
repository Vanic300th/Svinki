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
    [Serializable] private sealed class Command { public string action; public string target; public int index; }
    [Serializable] private sealed class State
    {
        public SessionSnapshot snapshot; public string status; public string identity;
        public int avatars; public bool offline, inputAllowed, menuVisible, cargoOpen, forwardPressed;
        public string carriedMannequin;public string[] chat;public int relayedVoice,receivedVoice;public float voiceOutput;
        public bool knockedDown; public float cameraHeight; public int hits; public bool eliminated; public AvatarState[] players;
        public string cart, cartRole;
        public CartState[] carts;
        public PickupState[] pickups;
        public Vector3 localPosition; public Vector3 eyePosition; public Vector3 eyeForward;
        public MonkeyState[] monkeys; public StunState[] stuns;
    }
    [Serializable] private sealed class CartState { public string name, driver, rider; public Vector3 position; public float speed; public int cargoRevision; public string[] cargo; }
    [Serializable] private sealed class AvatarState { public string name, emote; public int hits; public bool down, dead, owner, server; public float headHeight; public string[] outfit; }
    [Serializable] private sealed class PickupState { public string name; public Vector3 position; public Quaternion rotation; }
    [Serializable] private sealed class MonkeyState { public int id; public string holder; public bool consumed; public int bodies; public Vector3 center,leftHand,rightHand; }
    [Serializable] private sealed class StunState { public string name; public bool down,brain; public float remaining; public Vector3 head; }
    private Keyboard testKeyboard;
    private InputSettings originalInputSettings, testInputSettings;
    private HideFlags originalInputFlags;
    private string path;
    private float nextPoll;
    private float toneUntil,nextTone;private ushort toneSequence;private int toneStep;
    private byte[] lastTone;private readonly float[] toneFrame=new float[VoiceCodec.FrameSamples],audioProbe=new float[2048];
    private void PumpTone()
    {
        if(Time.unscaledTime>=toneUntil)return;
        var local=PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal)?.GetComponent<NetworkPlayer>();if(local==null)return;
        int budget=0;while(Time.unscaledTime>=nextTone&&budget++<5)
        {
            for(int i=0;i<toneFrame.Length;i++)toneFrame[i]=Mathf.Sin((toneSequence*VoiceCodec.FrameSamples+i)*2*Mathf.PI*500/VoiceCodec.SampleRate)*.3f;
            lastTone=VoiceCodec.Encode(toneFrame,ref toneStep);local.SendVoice(toneSequence++,lastTone);nextTone+=.02f;
        }
    }
    private float OutputLevel()
    {AudioListener.GetOutputData(audioProbe,0);float sum=0;foreach(float f in audioProbe)sum+=f*f;return Mathf.Sqrt(sum/audioProbe.Length);}
    private NetworkLobby lobby;
    private void Start()
    {
        lobby = GetComponent<NetworkLobby>(); string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "--session-test-path");
        if (i >= 0 && i + 1 < args.Length) path = args[i + 1];
    }
    private void Update()
    {
        if(path==null)return;if(ProximityVoice.Instance!=null)ProximityVoice.Instance.TestCaptureSuppressed=true;PumpTone();if(Time.unscaledTime<nextPoll)return;
        nextPoll = Time.unscaledTime + .2f;
        if (testKeyboard == null)
        {
            originalInputSettings = InputSystem.settings;
            originalInputFlags=originalInputSettings.hideFlags;
            originalInputSettings.hideFlags=HideFlags.DontUnloadUnusedAsset;
            testInputSettings = Instantiate(originalInputSettings);
            testInputSettings.hideFlags=HideFlags.HideAndDontSave;
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testInputSettings;
            testKeyboard = InputSystem.AddDevice<Keyboard>("Session test keyboard");
            testKeyboard.MakeCurrent();
        }
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
                case "chat": PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal)?.GetComponent<NetworkPlayer>()?.SendChat(command.target);break;
                case "voice-position":
                    var voicePlayer=PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal);
                    if(voicePlayer!=null){var voiceController=voicePlayer.GetComponent<CharacterController>();voiceController.enabled=false;voicePlayer.transform.position=new Vector3(0,.05f,command.index==0?5:command.index==1?7:command.index==2?18.5f:40);voiceController.enabled=true;}break;
                case "voice-tone":toneUntil=Time.unscaledTime+Mathf.Clamp(command.index,1,10);nextTone=Time.unscaledTime;toneStep=0;break;
                case "voice-stop":toneUntil=0;break;
                case "voice-invalid":PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal)?.GetComponent<NetworkPlayer>()?.SendVoice(toneSequence++,new byte[3]);break;
                case "voice-duplicate":if(lastTone!=null)PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal)?.GetComponent<NetworkPlayer>()?.SendVoice((ushort)(toneSequence-1),lastTone);break;
                case "voice-isolate":foreach(var audio in UnityEngine.Object.FindObjectsByType<AudioSource>())if(audio.name!="Positional player voice")audio.mute=true;break;
                case "emote": PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal)?.GetComponent<NetworkPlayer>()?.RequestEmote((PigEmote)command.index); break;
                case "monkey-warp":
                    var toyTarget = MonkeyToy.All.OrderBy(t=>t.GetComponent<NetworkMonkeyToy>().ObjectId).ElementAtOrDefault(command.index);
                    var toyPlayer = PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal);
                    if(toyTarget!=null && toyPlayer!=null)
                    {
                        var controller=toyPlayer.GetComponent<CharacterController>();controller.enabled=false;toyPlayer.transform.position=toyTarget.transform.position+Vector3.back*.85f;controller.enabled=true;
                        var toyView=toyPlayer.EyeCamera.GetComponent<GrayboxFirstPersonCamera>();
                        typeof(GrayboxFirstPersonCamera).GetField("yaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(toyView,0f);
                        typeof(GrayboxFirstPersonCamera).GetField("pitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(toyView,45f);
                    }break;
                case "monkey-grab":
                    PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal)?.GetComponent<NetworkPlayer>()?.RequestMonkeyGrab(MonkeyToy.All.OrderBy(t=>t.GetComponent<NetworkMonkeyToy>().ObjectId).ElementAtOrDefault(command.index)?.GetComponent<NetworkMonkeyToy>());break;
                case "monkey-swing":PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal)?.GetComponent<PlayerMonkeyCarry>()?.Swing();break;
                case "monkey-drop":PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal)?.GetComponent<PlayerMonkeyCarry>()?.Release();break;
                case "look-forward":
                    var forwardView=PlayerRegistry.Players.FirstOrDefault(p=>p.IsLocal)?.EyeCamera?.GetComponent<GrayboxFirstPersonCamera>();
                    if(forwardView!=null){typeof(GrayboxFirstPersonCamera).GetField("yaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(forwardView,0f);typeof(GrayboxFirstPersonCamera).GetField("pitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(forwardView,0f);}break;
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
                case "cargo-store":
                case "cargo-wear":
                case "cargo-unload":
                    var storage = ShoppingCart.All.FirstOrDefault(c => c.name == command.target)?.GetComponent<CartCargo>();
                    var cargoPlayer = PlayerRegistry.Players.FirstOrDefault(p => p.IsLocal)?.GetComponent<NetworkPlayer>();
                    cargoPlayer?.RequestCargo(storage, command.action == "cargo-store" ? CargoAction.StoreWorn : command.action == "cargo-wear" ? CargoAction.Equip : CargoAction.Unload, command.index);
                    break;
                case "cargo-open":
                    CartCargoView.Instance?.Open(ShoppingCart.All.FirstOrDefault(c => c.name == command.target)?.GetComponent<CartCargo>());
                    break;
                case "cargo-close": CartCargoView.Instance?.Close(); break;
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
                        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(key));
                    break;
                case "key-up":
                    if (Keyboard.current != null) InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
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
            monkeys=MonkeyToy.All.Select(t=>new MonkeyState{id=t.GetComponent<NetworkMonkeyToy>().ObjectId,holder=t.Holder?.GetComponent<NetworkPlayer>()?.ParticipantName,consumed=t.Consumed,bodies=t.GetComponent<ArticulatedRagdoll>().BodyCount,center=t.GetComponent<ArticulatedRagdoll>().Center,leftHand=t.GetComponent<ArticulatedRagdoll>().BonePosition("Hand_L"),rightHand=t.GetComponent<ArticulatedRagdoll>().BonePosition("Hand_R")}).ToArray(),
            stuns=FindObjectsByType<MannequinStun>().Select(s=>new StunState{name=s.name,down=s.IsStunned,brain=s.GetComponent<MannequinBrain>().enabled,remaining=s.Remaining,head=s.GetComponent<ArticulatedRagdoll>().BonePosition("Head")}).ToArray(),
            chat=PlayerChat.Instance?.GetComponent<PlayerChatHistory>()?.Messages,
            relayedVoice=PlayerRegistry.Players.Where(p=>p!=null).Sum(p=>p.GetComponent<NetworkPlayer>()?.RelayedVoiceFrames??0),
            receivedVoice=PlayerRegistry.Players.Where(p=>p!=null).Sum(p=>p.GetComponent<VoicePlayback>()?.ReceivedFrames??0),voiceOutput=OutputLevel(),
            avatars = PlayerRegistry.Players.Count, offline = lobby.Offline, inputAllowed=lobby.InputAllowed,menuVisible=lobby.MenuVisible,cargoOpen=lobby.CargoOpen,forwardPressed=Keyboard.current?.wKey.isPressed==true,
            carriedMannequin = local != null ? local.GetComponent<PlayerMannequinCarry>()?.Held?.name : null,
            hits = local != null ? local.GetComponent<PlayerKnockdown>()?.Hits ?? 0 : 0,
            eliminated = local != null && !local.IsAlive,
            players = PlayerRegistry.Players.Where(p=>p!=null).Select(p=>new AvatarState { emote=p.GetComponentInChildren<PigMotion>()?.ActiveEmote.ToString(),name=p.GetComponent<NetworkPlayer>()?.ParticipantName, hits=p.GetComponent<PlayerKnockdown>()?.Hits??0,down=p.GetComponent<PlayerKnockdown>()?.IsDown==true,dead=!p.IsAlive,owner=p.IsLocal,server=p.GetComponent<NetworkPlayer>()?.IsServerInitialized==true,headHeight=p.GetComponent<PlayerKnockdown>()!=null?p.GetComponent<PlayerKnockdown>().HeadPosition.y-p.Position.y:0,outfit=p.Outfit.Items.Select(c=>c.name).ToArray()}).ToArray(),
            knockedDown = local != null && local.GetComponent<PlayerKnockdown>()?.IsDown == true,
            cameraHeight = eye != null && local != null ? eye.transform.position.y - local.Position.y : 0,
            cart = ShoppingCart.For(local)?.name, cartRole = ShoppingCart.For(local) is ShoppingCart occupied ? occupied.Driver == local ? "driver" : "rider" : null,
            carts = ShoppingCart.All.Select(c => new CartState { name=c.name, position=c.transform.position, speed=c.Speed, driver=c.Driver?.GetComponent<NetworkPlayer>()?.ParticipantName, rider=c.Rider?.GetComponent<NetworkPlayer>()?.ParticipantName,cargoRevision=c.GetComponent<CartCargo>()?.Revision??0,cargo=c.GetComponent<CartCargo>()?.Items.Select(i=>i.name).ToArray()??Array.Empty<string>() }).ToArray(),
            pickups = ClothingPickup.All.Select(p => new PickupState { name=p.name, position=p.transform.position, rotation=p.transform.rotation }).ToArray(),
            localPosition = local != null ? local.Position : Vector3.zero,
            eyePosition = eye != null ? eye.transform.position : Vector3.zero,
            eyeForward = eye != null ? eye.transform.forward : Vector3.forward });
        File.WriteAllText(path + ".state.tmp", json);
        if (File.Exists(path + ".state")) File.Delete(path + ".state");
        File.Move(path + ".state.tmp", path + ".state");
    }
    private void OnDestroy()
    {
        if (testKeyboard != null) InputSystem.RemoveDevice(testKeyboard);
        if (originalInputSettings != null){InputSystem.settings = originalInputSettings;originalInputSettings.hideFlags=originalInputFlags;}
        if (testInputSettings != null) Destroy(testInputSettings);
    }
}
#endif
