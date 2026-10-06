using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
[DefaultExecutionOrder(170)]
public sealed class ShoppingCart : MonoBehaviour
{
    private static readonly List<ShoppingCart> carts = new List<ShoppingCart>();
    private readonly Collider[] hits = new Collider[24];
    private readonly Dictionary<PlayerAvatar, float> struck = new Dictionary<PlayerAvatar, float>();
    private Rigidbody body;
    private NetworkShoppingCart network;
    private Vector3 home;
    private Quaternion homeRotation;
    private Vector2 input;
    private bool sprint;
    private float inputUntil, impactSpeed;
    private Vector3 impactVelocity;
    private float nextWallCrash;
    public PlayerAvatar Driver { get; private set; }
    public PlayerAvatar Rider { get; private set; }
    public bool HasAuthority => network == null || !network.isActiveAndEnabled || network.ServerActive;
    public float Speed => body != null ? Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude : 0;
    public static IReadOnlyList<ShoppingCart> All => carts;
    public static ShoppingCart For(PlayerAvatar player) => player == null ? null : carts.Find(c => c != null && (c.Driver == player || c.Rider == player));
    private void Awake()
    {
        body = GetComponent<Rigidbody>(); network = GetComponent<NetworkShoppingCart>();
        home = transform.position; homeRotation = transform.rotation;
    }
    private void Start() { if (HasAuthority) EnablePhysics(true); }
    private void OnEnable() { if (!carts.Contains(this)) carts.Add(this); }
    private void OnDisable() { ApplyOccupants(null, null); carts.Remove(this); }
    public void EnablePhysics(bool authority)
    {
        body.isKinematic = !authority;
        body.useGravity = authority;
    }
    public bool TryUse(PlayerAvatar player, bool sit)
    {
        if (!HasAuthority || player == null || player.gameObject.scene != gameObject.scene ||
            player.GetComponent<PlayerKnockdown>()?.IsDown == true ||
            player.GetComponent<PlayerMannequinCarry>()?.Held != null ||
            NetworkLobby.Instance?.Results != null) return false;
        var current = For(player);
        if (current == this) { Release(player, false); return true; }
        if (current != null || Vector3.Distance(player.Position, transform.position) > 4 || Speed > 3) return false;
        if (sit ? Rider != null : Driver != null) return false;
        ApplyOccupants(sit ? Driver : player, sit ? player : Rider);
        Sync(); return true;
    }
    public void SubmitInput(PlayerAvatar player, Vector2 movement, bool fast)
    {
        if (!HasAuthority || Driver != player || !float.IsFinite(movement.x) || !float.IsFinite(movement.y)) return;
        input = Vector2.ClampMagnitude(movement, 1); sprint = fast; inputUntil = Time.unscaledTime + .3f;
    }
    public void Release(PlayerAvatar player, bool launch)
    {
        if (!HasAuthority || player == null) return;
        bool wasDriver = Driver == player;
        if (!wasDriver && Rider != player) return;
        Vector3 position = SafeExit(player);
        ApplyOccupants(wasDriver ? null : Driver, Rider == player ? null : Rider);
        MovePlayer(player, position);
        if (wasDriver)
        {
            input = Vector2.zero;
            if (launch) body.linearVelocity = transform.forward * Mathf.Max(9, Speed) + Vector3.up * body.linearVelocity.y;
        }
        Sync();
    }
    public void ApplyOccupants(PlayerAvatar pushing, PlayerAvatar seated)
    {
        if (Driver != pushing) { SetCollision(Driver, false); Driver = pushing; SetCollision(Driver, true); }
        if (Rider != seated) { SetCollision(Rider, false); Rider = seated; SetCollision(Rider, true); }
    }
    private void SetCollision(PlayerAvatar player, bool riding)
    {
        if (player == null) return;
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = !riding;
        if (!riding) player.GetComponent<GrayboxPlayerController>()?.SetRemoteStance(0);
    }
    private void Sync() => network?.SetOccupants(Driver, Rider);
    private bool Unavailable(PlayerAvatar player) => player == null || !player.isActiveAndEnabled ||
        player.GetComponent<PlayerKnockdown>()?.IsDown == true ||
        player.GetComponent<NetworkPlayer>() is NetworkPlayer p && p.IsServerInitialized && (!p.Owner.IsValid || !p.Owner.IsActive);
    private void FixedUpdate()
    {
        if (!HasAuthority) return;
        if (body.isKinematic) EnablePhysics(true);
        if (Driver != null && (Unavailable(Driver) || NetworkLobby.Instance?.Results != null)) Release(Driver, false);
        if (Rider != null && (Unavailable(Rider) || NetworkLobby.Instance?.Results != null)) Release(Rider, false);
        // Destroyed/despawned avatars compare equal to null; clear the replicated references too.
        if (Driver == null && Rider == null) Sync();
        Vector3 velocity = body.linearVelocity;
        Vector3 planar = Vector3.ProjectOnPlane(velocity, Vector3.up);
        if (Driver != null)
        {
            Vector2 controls = Time.unscaledTime <= inputUntil ? input : Vector2.zero;
            float targetSpeed = controls.y * (sprint ? 8 : 4.5f);
            float turn = controls.x * 85 * Time.fixedDeltaTime * (controls.y < -.1f ? -1 : 1);
            body.MoveRotation(body.rotation * Quaternion.Euler(0, turn, 0));
            planar = Vector3.MoveTowards(planar, transform.forward * targetSpeed, 12 * Time.fixedDeltaTime);
        }
        else planar = Vector3.MoveTowards(planar, Vector3.zero, .9f * Time.fixedDeltaTime);
        body.linearVelocity = planar + Vector3.up * velocity.y;
        impactVelocity = planar; impactSpeed = planar.magnitude;
        if (impactSpeed > 4) StrikePlayers(planar);
        if (transform.position.y < -8 || Vector3.Distance(transform.position, home) > 200)
        {
            if (Driver != null) Release(Driver, false);
            if (Rider != null) Release(Rider, false);
            body.position = home; body.rotation = homeRotation; body.linearVelocity = Vector3.zero;
        }
    }
    private void LateUpdate()
    {
        if (Driver != null) MovePlayer(Driver, transform.TransformPoint(new Vector3(0, 0, -1.15f)));
        if (Rider != null)
        {
            Rider.GetComponent<GrayboxPlayerController>()?.SetRemoteStance(1);
            MovePlayer(Rider, transform.TransformPoint(new Vector3(0, .62f, .02f)));
        }
    }
    private static void MovePlayer(PlayerAvatar player, Vector3 point)
    {
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>(); bool enabled = cc != null && cc.enabled;
        if (enabled) cc.enabled = false;
        player.transform.position = point;
        if (enabled) cc.enabled = true;
    }
    private Vector3 SafeExit(PlayerAvatar player)
    {
        var cc = player.GetComponent<CharacterController>();
        float radius = cc != null ? cc.radius : .35f;
        foreach (Vector3 local in new[] { new Vector3(-1.2f, .1f, 0), new Vector3(1.2f, .1f, 0), new Vector3(0, .1f, -1.65f), new Vector3(0, .1f, 1.65f) })
        {
            Vector3 point = transform.TransformPoint(local);
            if (!gameObject.scene.GetPhysicsScene().Raycast(point + Vector3.up * 2, Vector3.down, out RaycastHit floor, 4, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            point.y = floor.point.y + .05f;
            int count = gameObject.scene.GetPhysicsScene().OverlapCapsule(point + Vector3.up * (radius + .05f), point + Vector3.up * (1.75f - radius), radius, hits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool free = true;
            for (int i = 0; i < count; i++) if (!hits[i].transform.IsChildOf(player.transform)) { free = false; break; }
            if (free) return point;
        }
        return home + Vector3.back * 1.7f + Vector3.up * .05f;
    }
    private void StrikePlayers(Vector3 velocity)
    {
        Vector3 center = transform.position + Vector3.up * .85f;
        int count = gameObject.scene.GetPhysicsScene().OverlapCapsule(center, center + velocity * Time.fixedDeltaTime, .8f, hits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var player = hits[i].GetComponentInParent<PlayerAvatar>();
            if (player == null || player == Driver || player == Rider || struck.TryGetValue(player, out float until) && Time.time < until) continue;
            if (player.GetComponent<PlayerKnockdown>()?.TryFall(velocity.normalized) == true) struck[player] = Time.time + 3;
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (!HasAuthority || impactSpeed < 4.5f || Time.time < nextWallCrash) return;
        bool wall = false;
        foreach (ContactPoint contact in collision.contacts)
            if (Mathf.Abs(contact.normal.y) < .5f && Vector3.Dot(contact.normal, impactVelocity.normalized) < -.3f) { wall = true; break; }
        if (!wall) return;
        nextWallCrash = Time.time + 1;
        PlayerAvatar driver = Driver, rider = Rider;
        if (driver != null) { Release(driver, false); driver.GetComponent<PlayerKnockdown>()?.TryFall(impactVelocity.normalized); }
        if (rider != null) { Release(rider, false); rider.GetComponent<PlayerKnockdown>()?.TryFall(impactVelocity.normalized); }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() => carts.Clear();
}
