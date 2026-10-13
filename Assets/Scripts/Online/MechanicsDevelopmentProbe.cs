#if UNITY_EDITOR || DEBUG
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

[Serializable] public sealed class MechanicsProbeState
{
    public MechanicsPlayerState[] players;
    public int activeHostiles, memories, scares;
    public bool photoViewer, runwayOwn;
    public int runwayIndex;
    public float danger, musicVolume, runwayElapsed;
    public Vector3 runwayPosition;
}
[Serializable] public sealed class MechanicsPlayerState
{
    public string name, bodyHolder, bodyCart, ballHolder;
    public bool ball, ghost;
    public Vector3 bodyPosition, ballPosition, ghostPosition;
}
public static class MechanicsDevelopmentProbe
{
    private static T Field<T>(object owner, string name) => owner != null ? (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner) : default;
    private static PlayerAvatar Local => PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal);
    private static PlayerAvatar Named(string name) => PlayerRegistry.Players.FirstOrDefault(p => p?.GetComponent<NetworkPlayer>()?.ParticipantName == name);
    private static void Warp(PlayerAvatar player, Vector3 position)
    {
        if (player == null) return;
        var controller = player.GetComponent<CharacterController>(); bool enabled = controller.enabled;
        controller.enabled = false; player.transform.position = position; controller.enabled = enabled;
    }
    public static MechanicsProbeState Read()
    {
        var runway = FinalRunwayView.Active; var pig = Field<GameObject>(runway, "pig");
        return new MechanicsProbeState {
            players = PlayerRegistry.Players.Where(p => p != null).Select(p => {
                var b = p.GetComponent<PlayerBodyCarry>(); var ball = p.GetComponent<PlayerBallForm>();
                var flight = p.GetComponent<NetworkPlayer>()?.GhostPose ?? default;
                return new MechanicsPlayerState { name = p.GetComponent<NetworkPlayer>()?.ParticipantName, bodyHolder = b?.Holder?.GetComponent<NetworkPlayer>()?.ParticipantName,
                    bodyCart = b?.Cart?.name, bodyPosition = b?.Position ?? p.Position, ball = ball?.IsBall == true, ballPosition = ball?.Position ?? p.Position,
                    ballHolder = ball?.Holder?.GetComponent<NetworkPlayer>()?.ParticipantName, ghost = flight.Active, ghostPosition = flight.Position };
            }).ToArray(),
            activeHostiles = UnityEngine.Object.FindObjectsByType<NetworkMannequinAuthority>().Count(m => m.RoundActive), memories = OutfitMemoryStore.Entries.Count,
            photoViewer = LockerPhotoViewer.IsOpen, runwayOwn = runway?.OwnControl == true, runwayIndex = runway?.PresentedIndex ?? -1,
            musicVolume = MusicSettings.Volume, runwayElapsed = NetworkLobby.Instance?.RunwayElapsed ?? 0, runwayPosition = pig != null ? pig.transform.localPosition : Vector3.zero,
            danger = Camera.main?.GetComponent<PlayerFearEffects>()?.Danger ?? 0, scares = Camera.main?.GetComponent<PlayerFearEffects>()?.Scares ?? 0
        };
    }
    public static void Command(string action, string target, int index)
    {
        var player = Local; var actor = player?.GetComponent<NetworkPlayer>();
        switch (action)
        {
            case "mechanics-position": Warp(player, JsonUtility.FromJson<Vector3>(target)); break;
            case "mechanics-music": MusicSettings.Set(index / 100f); break;
            case "mechanics-ball-grab": actor?.RequestBallGrab(Named(target)?.GetComponent<PlayerBallForm>()); break;
            case "mechanics-ball-throw": actor?.RequestBallRelease(PlayerBallForm.ForHolder(player), true); break;
            case "mechanics-ball-shot":
                var form = player?.GetComponent<PlayerBallForm>(); var npc = UnityEngine.Object.FindObjectsByType<MannequinBrain>().FirstOrDefault(m => m.GetComponent<NetworkMannequinAuthority>()?.RoundActive != false);
                if (form?.Authority == true && form.IsBall && npc != null)
                {
                    var body = Field<Rigidbody>(form, "body"); body.position = npc.transform.position + Vector3.up * .65f - npc.transform.forward * 2;
                    body.linearVelocity = npc.transform.forward * 11;
                }
                break;
            case "mechanics-body-grab": actor?.RequestBody(Named(target)?.GetComponent<PlayerBodyCarry>()); break;
            case "mechanics-body-cart": actor?.RequestBody(PlayerBodyCarry.ForHolder(player), ShoppingCart.All.FirstOrDefault(c => c.name == target)); break;
            case "mechanics-prep-cart":
                var cart = ShoppingCart.All.FirstOrDefault();
                if (cart?.HasAuthority == true)
                {
                    var body = cart.GetComponent<Rigidbody>(); body.position = player.Position + Vector3.forward * 1.8f; body.rotation = Quaternion.identity;
                    actor?.AwardClothing(Field<ClothingDefinition[]>(actor, "clothingCatalog").First(c => c != null));
                    var view = Camera.main?.GetComponent<GrayboxFirstPersonCamera>(); if (view != null) { typeof(GrayboxFirstPersonCamera).GetField("yaw", BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view, 0f); typeof(GrayboxFirstPersonCamera).GetField("pitch", BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view, 0f); }
                }
                break;
            case "mechanics-cart-lift":
                var selected = ShoppingCart.All.FirstOrDefault(c => c.name == target); var station = selected != null ? RoundFinishStation.ForScene(selected.gameObject.scene) : null;
                if (selected?.HasAuthority == true && station != null)
                { var body = selected.GetComponent<Rigidbody>(); body.position = station.Center + Vector3.up * .08f; body.linearVelocity = body.angularVelocity = Vector3.zero; }
                break;
            case "mechanics-ghost-edge":
                var spectator = UnityEngine.Object.FindAnyObjectByType<SpectatorCamera>();
                if (spectator != null) typeof(SpectatorCamera).GetField("position", BindingFlags.NonPublic|BindingFlags.Instance).SetValue(spectator, new Vector3(100000, 3, 100000));
                break;
            case "mechanics-runway-save": Field<UnityEngine.UI.Button>(FinalRunwayView.Active, "poseButton")?.onClick.Invoke(); break;
            case "mechanics-runway-emote": FinalRunwayView.Active?.Emote((PigEmote)index); break;
            case "mechanics-photo-view": LockerPhotoViewer.Open(PhotoAlbum.Instance?.SavedPhoto(0)); break;
            case "mechanics-photo-full":
                var full = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().FirstOrDefault(b => b.name == "FULL SCREEN / WINDOW"); full?.onClick.Invoke(); break;
            case "mechanics-locker":
                var locker = UnityEngine.Object.FindObjectsByType<PlayerLocker>().FirstOrDefault(l => l.IsMine(player));
                if (locker != null) { Warp(player, locker.transform.position + Vector3.back * 1.3f); locker.Interact(player); }
                break;
        }
    }
}
#endif
