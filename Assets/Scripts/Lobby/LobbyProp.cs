using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public enum LobbyPropKind : byte { Junk, Basketball, Dart, Puck }
[Serializable] public struct LobbyPropPose { public Vector3 Position; public Quaternion Rotation; public NetworkObject Holder; public bool Held; }

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public sealed class LobbyProp : LobbyInteractable
{
    [SerializeField] private LobbyPropKind kind;
    [SerializeField, Min(1)] private float throwSpeed = 11;
    [SerializeField, Min(1)] private float resetAfterSeconds = 40;
    [SerializeField] private Bounds playBounds = new Bounds(new Vector3(-15, 3, -5), new Vector3(48, 16, 38));
    private readonly SyncVar<LobbyPropPose> pose = new SyncVar<LobbyPropPose>();
    private Rigidbody body;
    private Collider shape;
    private Vector3 home;
    private Quaternion homeRotation;
    private PlayerAvatar holder;
    private float outsideSince;
    private bool stuck;
    private Vector3 clientPosition;
    private Quaternion clientRotation;
    private bool receivedPose;
    private Collider pickupArea;
    private PhysicsMaterial playMaterial;
    public LobbyPropKind Kind => kind;
    public PlayerAvatar Holder => holder;
    public string LastThrower { get; private set; }
    public int ThrowSerial { get; private set; }
    public bool CanScore => Authority && holder == null && !stuck && ThrowSerial > 0 && !string.IsNullOrEmpty(LastThrower);
    public override string Prompt(PlayerAvatar player) => "E — pick up " + name;
    private void Awake()
    {
        body = GetComponent<Rigidbody>(); shape = GetComponent<Collider>();
        home = transform.position; homeRotation = transform.rotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        if (kind == LobbyPropKind.Dart)
        {
            body.mass = .22f; body.linearDamping = .015f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var area = new GameObject("Dart pickup area"); area.transform.SetParent(transform, false);
            var capsule = area.AddComponent<CapsuleCollider>(); capsule.isTrigger = true;
            capsule.direction = 2; capsule.radius = .12f; capsule.height = .46f; pickupArea = capsule;
        }
        else if (kind == LobbyPropKind.Basketball)
        {
            body.mass = .65f; body.linearDamping = .12f; body.angularDamping = .15f;
            playMaterial = new PhysicsMaterial("Basketball bounce") { bounciness = .84f, dynamicFriction = .35f,
                staticFriction = .35f, bounceCombine = PhysicsMaterialCombine.Maximum };
            shape.sharedMaterial = playMaterial;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        pose.OnChange += PoseChanged;
    }
    public override void OnStartServer() { base.OnStartServer(); SendPose(); }
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsServerInitialized) { body.isKinematic = true; body.interpolation = RigidbodyInterpolation.None; Receive(pose.Value); }
    }
    public override void Apply(PlayerAvatar player, LobbyAction action, string text, int index, Vector2[] points)
    {
        if (action == LobbyAction.Reset) { if (holder == null) ResetHome(); return; }
        var hands = player.GetComponent<PlayerLobbyHands>();
        if (action == LobbyAction.Toggle && !CarryRules.FreeHands(player)) return;
        if (action != LobbyAction.Toggle || holder != null || hands == null || hands.Held != null ||
            player.GetComponent<PlayerMannequinCarry>()?.Held != null || player.GetComponent<PlayerMonkeyCarry>()?.Held != null ||
            player.GetComponent<PlayerPhotoCamera>()?.Equipped == true || ShoppingCart.For(player) != null) return;
        holder = player; hands.Set(this); stuck = false; shape.enabled = false; if (pickupArea != null) pickupArea.enabled = false; body.isKinematic = true;
        FollowHands(); SendPose();
    }
    public bool Release(bool thrown)
    {
        if (!Authority || holder == null) return false;
        var previous = holder; FollowHands(); holder.GetComponent<PlayerLobbyHands>()?.Set(null); holder = null;
        shape.enabled = true; if (pickupArea != null) pickupArea.enabled = true; body.isKinematic = false;
        Vector3 direction = previous.TryGetView(out var view) ? view.Rotation * Vector3.forward : previous.transform.forward;
        body.linearVelocity = previous.Velocity * .3f + (thrown ? direction * throwSpeed + Vector3.up : Vector3.zero);
        body.angularVelocity = kind == LobbyPropKind.Dart ? Vector3.zero : Vector3.Cross(Vector3.up, direction) * 3;
        body.WakeUp(); LastThrower = Nickname(previous); if (thrown) ThrowSerial++;
        SendPose(); return true;
    }
    private void FollowHands()
    {
        if (holder == null || !holder.TryGetView(out var view)) return;
        Vector3 wanted = view.Eye + view.Rotation * new Vector3(.35f, -.3f, .85f);
        Vector3 travel = wanted - view.Eye;
        if (gameObject.scene.GetPhysicsScene().Raycast(view.Eye, travel.normalized, out var hit, travel.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) wanted = view.Eye + travel.normalized * Mathf.Max(.2f, hit.distance - .2f);
        transform.SetPositionAndRotation(wanted, view.Rotation);
    }
    public override void LobbyTick(float delta)
    {
        if (Authority)
        {
            if (holder != null)
            {
                var network = holder.GetComponent<NetworkPlayer>();
                if (!holder.isActiveAndEnabled || !holder.IsAlive || NetworkLobby.Instance?.Offline != true && network != null && NetworkLobby.Instance?.CanUseLobby(network) != true ||
                    holder.GetComponent<PlayerKnockdown>()?.IsDown == true) Release(false);
                else FollowHands();
            }
            if (!playBounds.Contains(transform.position)) { outsideSince += delta; if (transform.position.y < -8 || outsideSince >= resetAfterSeconds) ResetHome(); }
            else outsideSince = 0;
            if (kind == LobbyPropKind.Dart && !body.isKinematic && body.linearVelocity.sqrMagnitude > 1) body.rotation = Quaternion.LookRotation(body.linearVelocity);
            // Sleeping props have no pose changes, hence no traffic. Moving props are sampled by one manager at 10 Hz.
            if (NetworkObject != null && IsServerInitialized && (pose.Value.Position - transform.position).sqrMagnitude > .0001f ||
                NetworkObject != null && IsServerInitialized && Quaternion.Angle(pose.Value.Rotation, transform.rotation) > .5f) SendPose();
        }
    }
    private void SendPose()
    {
        if (NetworkObject != null && IsServerInitialized) pose.Value = new LobbyPropPose { Position = transform.position, Rotation = transform.rotation,
            Holder = holder != null ? holder.GetComponent<NetworkObject>() : null, Held = holder != null };
    }
    private void PoseChanged(LobbyPropPose previous, LobbyPropPose next, bool asServer) { if (!asServer && !IsServerInitialized) Receive(next); }
    private void Receive(LobbyPropPose next)
    {
        var oldHands = holder?.GetComponent<PlayerLobbyHands>();
        if (oldHands != null && oldHands.Held == this) oldHands.Set(null);
        holder = next.Holder != null ? next.Holder.GetComponent<PlayerAvatar>() : null;
        holder?.GetComponent<PlayerLobbyHands>()?.Set(this); shape.enabled = !next.Held;
        if (pickupArea != null) pickupArea.enabled = !next.Held;
        clientPosition = next.Position; clientRotation = next.Rotation;
        if (!receivedPose) { transform.SetPositionAndRotation(next.Position, next.Rotation); receivedPose = true; }
    }
    public void VisualFrame()
    {
        if (holder != null && (Authority || holder.IsLocal)) FollowHands();
        else if (!Authority && receivedPose)
        {
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, clientPosition, Time.unscaledDeltaTime * 15),
                Quaternion.Slerp(transform.rotation, clientRotation, Time.unscaledDeltaTime * 15));
        }
    }
    public void ResetHome()
    {
        if (!Authority) return;
        if (holder != null) Release(false);
        body.isKinematic = false; body.linearVelocity = body.angularVelocity = Vector3.zero;
        body.position = home; body.rotation = homeRotation;
        transform.SetPositionAndRotation(home, homeRotation); outsideSince = 0; LastThrower = null; stuck = false; shape.enabled = true; body.WakeUp(); SendPose();
    }
    public void Stick(Vector3 point) { if (!CanScore) return; body.linearVelocity = body.angularVelocity = Vector3.zero; body.isKinematic = true; transform.position = point; stuck = true; SendPose(); }
    private void OnCollisionEnter(Collision collision)
    {
        if (!CanScore || kind != LobbyPropKind.Dart || collision.contactCount == 0) return;
        collision.collider.GetComponentInParent<Dartboard>()?.Hit(this, collision.GetContact(0).point);
    }
    private void OnDestroy() { if (playMaterial != null) Destroy(playMaterial); }
}
