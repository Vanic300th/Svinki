using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public struct GhostFlightPose { public Vector3 Position; public float Yaw, Pitch; public bool Active; }
public struct PlayerBodyAttachment { public NetworkObject Holder, Cart; }
public struct PlayerBallPose { public bool Active; public Vector3 Position; public Quaternion Rotation; public NetworkObject Holder; }

public sealed partial class NetworkPlayer
{
    public GhostFlightPose GhostPose => ghostFlight.Value;
    private readonly SyncVar<GhostFlightPose> ghostFlight = new SyncVar<GhostFlightPose>();
    private readonly SyncVar<PlayerBodyAttachment> bodyAttachment = new SyncVar<PlayerBodyAttachment>();
    private readonly SyncVar<PlayerBallPose> ballPose = new SyncVar<PlayerBallPose>();
    private PlayerBodyCarry bodyCarry;
    private PlayerBallForm ball;
    private GameObject ghostVisual;
    private StoreMap flightMap;
    private float nextGhostRequest, nextBallPose;
    private void SetupMechanics()
    {
        bodyCarry = GetComponent<PlayerBodyCarry>() ?? gameObject.AddComponent<PlayerBodyCarry>();
        ball = GetComponent<PlayerBallForm>() ?? gameObject.AddComponent<PlayerBallForm>();
        bodyAttachment.OnChange += (previous, next, asServer) => bodyCarry.Apply(next.Holder, next.Cart);
        ballPose.OnChange += (previous, next, asServer) => { if (!IsServerInitialized) ball.Apply(next); };
        hitsTaken.OnChange += (previous, next, asServer) =>
        {
            worldOutfit?.SetFirstPersonHidden(IsOwner && next < 2);
            if (next < 2 && previous >= 2 && IsOwner) StartCoroutine(BindLocalView());
        };
    }
    public void RequestGhostPose(Vector3 position, float yawValue, float pitchValue)
    { if (IsOwner && IsDead) GhostFlightServerRpc(position, yawValue, pitchValue); }
    [ServerRpc(RequireOwnership = true)]
    private void GhostFlightServerRpc(Vector3 position, float yawValue, float pitchValue)
    {
        if (!IsDead || NetworkLobby.Instance?.Snapshot.phase != SessionPhase.Round || Time.unscaledTime < nextGhostRequest ||
            !Finite(position) || !float.IsFinite(yawValue) || !float.IsFinite(pitchValue)) return;
        nextGhostRequest = Time.unscaledTime + .035f;
        if (flightMap == null) foreach (var map in FindObjectsByType<StoreMap>()) if (map.gameObject.scene == gameObject.scene) { flightMap = map; break; }
        if (ghostFlight.Value.Active) position = ghostFlight.Value.Position + Vector3.ClampMagnitude(position - ghostFlight.Value.Position, 3);
        ghostFlight.Value = new GhostFlightPose { Active = true, Position = SpectatorCamera.ClampToMap(position, flightMap), Yaw = yawValue, Pitch = Mathf.Clamp(pitchValue, -85, 85) };
    }
    private static bool Finite(Vector3 point) => float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
    private void TickMechanics()
    {
        if (ghostVisual != null) ghostVisual.SetActive(IsDead && ghostFlight.Value.Active && !IsOwner && NetworkLobby.Instance?.Results == null);
        if (IsDead && ghostFlight.Value.Active && !IsOwner && ghostVisual == null)
        {
            var prefab = Resources.Load<GameObject>("Pigs/PigAvatar");
            if (prefab != null)
            {
                ghostVisual = Instantiate(prefab); ghostVisual.name = "Flying player ghost";
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ghostVisual, gameObject.scene);
                ghostVisual.GetComponent<PigAppearance>()?.Apply(pigFace.Value);
                ghostVisual.AddComponent<RunwayGhost>().Apply();
            }
        }
        if (ghostVisual != null && ghostVisual.activeSelf)
        {
            ghostVisual.transform.SetPositionAndRotation(ghostFlight.Value.Position - Vector3.up * .7f,
                Quaternion.Euler(0, ghostFlight.Value.Yaw, 0));
        }
        if (IsServerInitialized && Time.unscaledTime >= nextBallPose && ball != null && ball.IsBall)
        { nextBallPose = Time.unscaledTime + .05f; ballPose.Value = ball.Capture(); }
    }
    public void SetBodyAttachment(NetworkObject holder, NetworkObject cart)
    {
        if (!IsServerInitialized) return;
        bodyAttachment.Value = new PlayerBodyAttachment { Holder = holder, Cart = cart }; bodyCarry.Apply(holder, cart);
    }
    public void RequestBody(PlayerBodyCarry corpse, ShoppingCart cart = null)
    { if (IsOwner && corpse != null) BodyCarryServerRpc(corpse.GetComponent<NetworkObject>(), cart != null ? cart.GetComponent<NetworkObject>() : null); }
    [ServerRpc(RequireOwnership = true)]
    private void BodyCarryServerRpc(NetworkObject corpse, NetworkObject cart)
    {
        if (NetworkLobby.Instance?.CanEditOutfit(this) != true || corpse == null || corpse.gameObject.scene != gameObject.scene) return;
        corpse.GetComponent<PlayerBodyCarry>()?.Use(GetComponent<PlayerAvatar>(), cart != null ? cart.GetComponent<ShoppingCart>() : null);
    }
    public void RequestBallGrab(PlayerBallForm target)
    { if (IsOwner && target != null) BallGrabServerRpc(target.GetComponent<NetworkObject>()); }
    [ServerRpc(RequireOwnership = true)]
    private void BallGrabServerRpc(NetworkObject target)
    {
        if (!IsDead && NetworkLobby.Instance?.CanCommunicate(this) == true && target != null && target.gameObject.scene == gameObject.scene)
            target.GetComponent<PlayerBallForm>()?.Grab(GetComponent<PlayerAvatar>());
    }
    public void RequestBallRelease(PlayerBallForm target, bool thrown)
    { if (IsOwner && target != null) BallReleaseServerRpc(target.GetComponent<NetworkObject>(), thrown); }
    [ServerRpc(RequireOwnership = true)]
    private void BallReleaseServerRpc(NetworkObject target, bool thrown)
    { if (target != null && target.gameObject.scene == gameObject.scene) target.GetComponent<PlayerBallForm>()?.Release(GetComponent<PlayerAvatar>(), thrown); }
    public void RequestBallExit() { if (IsOwner) BallExitServerRpc(); }
    [ServerRpc(RequireOwnership = true)]
    private void BallExitServerRpc() { if (ball.IsBall) { ball.Exit(); ballPose.Value = ball.Capture(); } }
    public void StopBall() { if (IsServerInitialized && ball.IsBall) { ball.Exit(); ballPose.Value = ball.Capture(); } }
    public bool Revive(Vector3 position)
    {
        if (!IsServerInitialized || !IsDead || knockdown == null || !Finite(position)) return false;
        SetBodyAttachment(null, null);
        hitsTaken.Value = 0; knockdown.ApplyHit(0); knockdown.SetDown(false, Vector3.zero);
        knockdownPose.Value = new KnockdownPose { FallId = knockdown.FallId };
        ghostFlight.Value = default; ReviveObserversRpc(position);
        NetworkLobby.Instance?.PlayerRevived(this); return true;
    }
    [ObserversRpc]
    private void ReviveObserversRpc(Vector3 position)
    {
        var controller = GetComponent<CharacterController>(); controller.enabled = false;
        transform.position = position; controller.enabled = true;
        if (IsOwner) StartCoroutine(BindLocalView());
    }
    private void CleanupMechanics() { if (ghostVisual != null) Destroy(ghostVisual); }
    public bool WearLobbyMemory(string[] ids)
    {
        if (!IsServerInitialized || NetworkLobby.Instance?.CanUseLobby(this) != true || ids == null || ids.Length > 4) return false;
        var items = new System.Collections.Generic.List<ClothingDefinition>(); var slots = new System.Collections.Generic.HashSet<ClothingSlot>();
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var definition = FindInCatalog(id);
            if (definition == null || !slots.Add(definition.Slot)) return false;
            items.Add(definition);
        }
        outfit.SetItems(items); return true;
    }
}
