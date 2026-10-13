using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>The host simulates a player's ball; observers display its replicated pose.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(220)]
public sealed class PlayerBallForm : MonoBehaviour
{
    private static readonly List<PlayerBallForm> balls = new List<PlayerBallForm>();
    private PlayerAvatar avatar;
    private NetworkPlayer network;
    private CharacterController controller;
    private GameObject visual;
    private Rigidbody body;
    private SphereCollider shape;
    private Material material;
    private Material eyes;
    private PhysicsMaterial bounce;
    private PlayerBallPose remote;
    public bool IsBall { get; private set; }
    public PlayerAvatar Holder { get; private set; }
    public Vector3 Position => visual != null ? visual.transform.position : transform.position + Vector3.up * .45f;
    public bool Authority => network == null || network.IsServerInitialized;
    public static PlayerBallForm ForHolder(PlayerAvatar player) => player == null ? null : balls.Find(b => b != null && b.Holder == player);
    private void Awake() { avatar = GetComponent<PlayerAvatar>(); network = GetComponent<NetworkPlayer>(); controller = GetComponent<CharacterController>(); }
    private void OnEnable() { if (!balls.Contains(this)) balls.Add(this); }
    private void OnDisable() { balls.Remove(this); if (visual != null) visual.SetActive(false); }
    private void Build()
    {
        if (visual != null) return;
        visual = GameObject.CreatePrimitive(PrimitiveType.Sphere); visual.name = "Player ball";
        SceneManager.MoveGameObjectToScene(visual, gameObject.scene); visual.transform.localScale = Vector3.one * .86f;
        shape = visual.GetComponent<SphereCollider>();
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        int face = GetComponentInChildren<PigAppearance>(true)?.FaceCode ?? PigFace.Selected;
        material.color = PigAppearance.SkinColors[PigFace.Decode(face).skinColor];
        visual.GetComponent<Renderer>().sharedMaterial = material;
        eyes = new Material(material); eyes.color = new Color(.02f, .025f, .04f);
        bounce = new PhysicsMaterial("Player volleyball") { bounciness = .82f, bounceCombine = PhysicsMaterialCombine.Maximum,
            dynamicFriction = .2f, staticFriction = .2f };
        shape.sharedMaterial = bounce;
        body = visual.AddComponent<Rigidbody>(); body.mass = 3; body.linearDamping = .15f; body.angularDamping = .2f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.interpolation = RigidbodyInterpolation.Interpolate;
        visual.AddComponent<PlayerBallCollision>().Owner = this;
        foreach (float x in new[] { -.16f, .16f })
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere); eye.name = "Ball eye"; eye.transform.SetParent(visual.transform, false);
            eye.transform.localPosition = new Vector3(x, .12f, .44f); eye.transform.localScale = Vector3.one * .08f;
            var collider = eye.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            eye.GetComponent<Renderer>().sharedMaterial = eyes;
        }
    }
    public bool Enter()
    {
        if (!Authority || IsBall || !CarryRules.FreeHands(avatar) || NetworkLobby.Instance?.Results != null) return false;
        Build(); IsBall = true; Holder = null; visual.SetActive(true);
        body.isKinematic = false; body.position = transform.position + Vector3.up * .5f; body.rotation = transform.rotation;
        body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; ApplyVisibility(); return true;
    }
    private void ApplyVisibility()
    {
        if (controller != null) controller.enabled = !IsBall && avatar.IsAlive;
        var appearance = GetComponentInChildren<PigAppearance>(true);
        if (appearance != null) appearance.gameObject.SetActive(!IsBall);
        if (visual != null) visual.SetActive(IsBall);
    }
    public void Exit()
    {
        if (!Authority || !IsBall) return;
        var point = SafeExit(); Holder = null; IsBall = false; transform.position = point;
        if (body != null) body.isKinematic = true; ApplyVisibility();
    }
    private Vector3 SafeExit()
    {
        shape.enabled = false;
        Vector3 start = Position - Vector3.up * .4f;
        Vector3[] offsets = { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        var hits = new Collider[16];
        foreach (var offset in offsets)
        {
            var point = start + offset;
            if (!gameObject.scene.GetPhysicsScene().Raycast(point + Vector3.up * 2, Vector3.down, out var floor, 5,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            point.y = floor.point.y + .06f;
            int count = gameObject.scene.GetPhysicsScene().OverlapCapsule(point + Vector3.up * .4f, point + Vector3.up * 1.4f,
                .32f, hits, ~0, QueryTriggerInteraction.Ignore);
            bool free = true; for (int i = 0; i < count; i++) if (!hits[i].transform.IsChildOf(transform)) { free = false; break; }
            if (free) { shape.enabled = true; return point; }
        }
        shape.enabled = true; return start;
    }
    public bool Grab(PlayerAvatar player)
    {
        if (!Authority || !IsBall || Holder != null || player == avatar || !CarryRules.FreeHands(player) ||
            player.gameObject.scene != gameObject.scene || NetworkLobby.Instance?.Results != null || Vector3.Distance(player.EyePosition, Position) > 3.8f) return false;
        Vector3 direction = Position - player.EyePosition;
        if (direction.sqrMagnitude > .01f && gameObject.scene.GetPhysicsScene().Raycast(player.EyePosition, direction.normalized, out var hit,
            direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.gameObject != visual) return false;
        Holder = player; body.isKinematic = true; shape.enabled = false; return true;
    }
    public bool Release(PlayerAvatar player, bool thrown)
    {
        if (!Authority || !IsBall || Holder != player) return false;
        Holder = null; body.isKinematic = false; shape.enabled = true;
        body.linearVelocity = thrown ? player.EyeRotation() * Vector3.forward * 11 + Vector3.up * 1.5f : Vector3.zero;
        body.angularVelocity = player.transform.right * 7; return true;
    }
    public PlayerBallPose Capture() => new PlayerBallPose { Active = IsBall, Position = IsBall ? Position : transform.position,
        Rotation = visual != null ? visual.transform.rotation : transform.rotation, Holder = Holder?.GetComponent<FishNet.Object.NetworkObject>() };
    public void Apply(PlayerBallPose value)
    {
        remote = value; bool previous = IsBall; IsBall = value.Active; Holder = value.Holder?.GetComponent<PlayerAvatar>();
        if (IsBall)
        {
            Build(); body.isKinematic = true; shape.enabled = Holder == null;
            if (!previous) visual.transform.SetPositionAndRotation(value.Position, value.Rotation);
        }
        else if (previous) { if (controller != null) controller.enabled = false; transform.position = value.Position; }
        ApplyVisibility();
    }
    private void LateUpdate()
    {
        if (!IsBall) return;
        if (Authority && (!avatar.IsAlive || NetworkLobby.Instance?.Results != null)) { if (network != null) network.StopBall(); else Exit(); return; }
        if (Authority && Holder != null && !Holder.IsAlive) Release(Holder, false);
        if (Holder != null)
        {
            Vector3 wanted = Holder.EyePosition + Holder.EyeRotation() * new Vector3(0, -.2f, 1.05f);
            Vector3 direction = wanted - Holder.EyePosition;
            if (gameObject.scene.GetPhysicsScene().Raycast(Holder.EyePosition, direction.normalized, out var obstruction, direction.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) wanted = Holder.EyePosition + direction.normalized * Mathf.Max(.25f, obstruction.distance - .45f);
            visual.transform.SetPositionAndRotation(wanted, Holder.EyeRotation());
        }
        else if (!Authority)
            visual.transform.SetPositionAndRotation(Vector3.Lerp(Position, remote.Position, 1 - Mathf.Exp(-Time.deltaTime * 20)),
                Quaternion.Slerp(visual.transform.rotation, remote.Rotation, 1 - Mathf.Exp(-Time.deltaTime * 20)));
        if (Authority || avatar.IsLocal) transform.position = Position - Vector3.up * .45f;
        if (network == null && avatar.IsLocal && (Keyboard.current?.spaceKey.wasPressedThisFrame == true || Keyboard.current?.leftCtrlKey.wasPressedThisFrame == true)) Exit();
    }
    public void Hit(Collision collision)
    {
        if (!Authority || !IsBall || Holder != null || collision.contactCount == 0 || collision.relativeVelocity.magnitude < 2.5f) return;
        var target = collision.collider.GetComponentInParent<MannequinStun>();
        var proxy = collision.collider.GetComponentInParent<RagdollColliderOwner>();
        if (target == null && proxy?.Owner != null) target = proxy.Owner.GetComponent<MannequinStun>();
        target?.TryStun(body.linearVelocity.sqrMagnitude > .01f ? body.linearVelocity.normalized : -collision.GetContact(0).normal);
    }
    private void OnDestroy() { if (visual != null) Destroy(visual); if (material != null) Destroy(material); if (eyes != null) Destroy(eyes); if (bounce != null) Destroy(bounce); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => balls.Clear();
}
public sealed class PlayerBallCollision : MonoBehaviour
{
    public PlayerBallForm Owner { get; set; }
    private void OnCollisionEnter(Collision collision) => Owner?.Hit(collision);
}
