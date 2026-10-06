using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A display mannequin: carried kinematically, thrown with authoritative physics.</summary>
[DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class ThrowableMannequin : MonoBehaviour
{
    private Rigidbody body;
    private CapsuleCollider capsule;
    private NetworkThrowableMannequin network;
    private PlayerAvatar holder;
    private Vector3 displayPosition;
    private Quaternion displayRotation;
    private readonly Collider[] obstacles = new Collider[24];
    private readonly List<Collider> ignored = new List<Collider>();
    private Coroutine restoreCollisions;
    private readonly RaycastHit[] throwHits = new RaycastHit[24];
    private PlayerAvatar thrower;
    private float dangerousUntil;
    public PlayerAvatar Holder => holder;
    public bool IsHeld => holder != null;
    public bool HasAuthority => network == null || !network.isActiveAndEnabled || network.ServerActive;
    public Vector3 Center => transform.TransformPoint(capsule.center);

    private void Awake()
    {
        body = GetComponent<Rigidbody>(); capsule = GetComponent<CapsuleCollider>();
        network = GetComponent<NetworkThrowableMannequin>();
        displayPosition = transform.position; displayRotation = transform.rotation;
        body.mass = 8; body.linearDamping = .25f; body.angularDamping = .2f;
        body.maxDepenetrationVelocity = 5;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private IEnumerator Start()
    {
        // Let the imported animation establish its idle pose, then freeze this display.
        yield return null;
        foreach (Animator animator in GetComponentsInChildren<Animator>()) animator.enabled = false;
    }

    public void SetClientPhysics()
    {
        body.isKinematic = true;
        // NetworkTransform already interpolates observers; physics interpolation would compete with it.
        body.interpolation = RigidbodyInterpolation.None;
    }

    public bool TryGrab(PlayerAvatar player)
    {
        if (!HasAuthority || IsHeld || player == null || player.gameObject.scene != gameObject.scene ||
            Vector3.Distance(player.EyePosition, Center) > 3.8f) return false;
        var hands = player.GetComponent<PlayerMannequinCarry>();
        if (hands == null || hands.Held != null) return false;
        RestoreCollisions();
        dangerousUntil = 0;
        ApplyHolder(player);
        SetHeldPose(true);
        if (network != null && network.ServerActive) network.SetCarrier(player.GetComponent<FishNet.Object.NetworkObject>());
        return true;
    }

    public void ApplyHolder(PlayerAvatar player)
    {
        if (holder != null) holder.GetComponent<PlayerMannequinCarry>()?.Clear(this);
        holder = player;
        if (holder != null) holder.GetComponent<PlayerMannequinCarry>()?.Set(this);
        capsule.enabled = holder == null;
        if (holder != null || !HasAuthority) body.isKinematic = true;
    }

    private void FixedUpdate()
    {
        if (!HasAuthority) return;
        if (holder == null)
        {
            if (body.position.y < -10)
            {
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true;
                dangerousUntil = 0;
                body.position = displayPosition; body.rotation = displayRotation;
            }
            CheckThrownHit();
            return;
        }
        if (!holder.isActiveAndEnabled) { Release(false); return; }
        SetHeldPose(false);
    }

    private void LateUpdate()
    {
        // The owning guest follows their camera immediately; observers use NetworkTransform.
        if (!HasAuthority && holder != null && holder.IsLocal) SetHeldPose(true);
    }

    private void SetHeldPose(bool immediate)
    {
        if (holder == null || !holder.TryGetView(out PlayerView view)) return;
        Quaternion rotation = view.Rotation * Quaternion.Euler(70, 0, -12);
        Vector3 center = view.Eye + view.Rotation * new Vector3(.48f, -.32f, 1.35f);
        float scale = Mathf.Abs(transform.lossyScale.y);
        float radius = capsule.radius * scale;
        float half = Mathf.Max(0, capsule.height * scale * .5f - radius);
        Vector3 axis = rotation * Vector3.up * half;
        Vector3 travel = center - view.Eye;
        var physics = gameObject.scene.GetPhysicsScene();
        if (physics.CapsuleCast(view.Eye + axis, view.Eye - axis, radius, travel.normalized,
            out RaycastHit hit, travel.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            center = view.Eye + travel.normalized * Mathf.Max(.2f, hit.distance - .05f);
        // Also handle an initial overlap at a doorway: shorten the reach until the body fits.
        for (int step = 0; step < 5; step++)
        {
            int count = physics.OverlapCapsule(center + axis, center - axis, radius, obstacles,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == 0) break;
            center = Vector3.Lerp(center, view.Eye, .2f);
        }
        Vector3 position = center - rotation * Vector3.Scale(capsule.center, transform.lossyScale);
        if (immediate) { body.position = position; body.rotation = rotation; }
        else { body.MovePosition(position); body.MoveRotation(rotation); }
    }

    public bool Release(bool thrown)
    {
        if (!HasAuthority || holder == null) return false;
        PlayerAvatar previous = holder;
        SetHeldPose(true);
        Vector3 direction = previous.TryGetView(out PlayerView view) ? view.Rotation * Vector3.forward : previous.transform.forward;
        ApplyHolder(null);
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = previous.Velocity * .5f + (thrown ? direction * 12 + Vector3.up * 2 : Vector3.zero);
        body.angularVelocity = thrown ? Vector3.Cross(Vector3.up, direction) * 5 + direction * 2 : Vector3.zero;
        body.WakeUp();
        thrower = previous;
        dangerousUntil = thrown ? Time.time + 2 : 0;
        foreach (Collider collider in previous.GetComponentsInChildren<Collider>(true))
            if (collider != null && collider.enabled)
            {
                Physics.IgnoreCollision(capsule, collider, true); ignored.Add(collider);
            }
        if (isActiveAndEnabled && gameObject.activeInHierarchy) restoreCollisions = StartCoroutine(RestoreAfterThrow());
        else RestoreCollisions();
        if (network != null && network.ServerActive) network.SetCarrier(null);
        return true;
    }

    private void CheckThrownHit()
    {
        if (body.isKinematic || dangerousUntil <= 0 || Time.time > dangerousUntil || body.linearVelocity.sqrMagnitude < 25) return;
        // CharacterControllers do not reliably provide Rigidbody collision callbacks. Sweep the
        // next physics step, including Ignore Raycast (the player layer), with walls blocking hits.
        float scale = Mathf.Abs(transform.lossyScale.y);
        float radius = capsule.radius * scale;
        Vector3 center = body.position + body.rotation * Vector3.Scale(capsule.center, transform.lossyScale);
        Vector3 axis = body.rotation * Vector3.up * Mathf.Max(0, capsule.height * scale * .5f - radius);
        Vector3 velocity = body.linearVelocity;
        var physics = gameObject.scene.GetPhysicsScene();
        Vector3 travel = velocity * Time.fixedDeltaTime;
        int count = physics.CapsuleCast(center + axis, center - axis, radius, velocity.normalized,
            throwHits, travel.magnitude, ~0, QueryTriggerInteraction.Ignore);
        float obstruction = float.PositiveInfinity, nearest = float.PositiveInfinity;
        PlayerAvatar victim = null;
        for (int i = 0; i < count; i++)
        {
            Collider hit = throwHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform)) continue;
            var player = hit.GetComponentInParent<PlayerAvatar>();
            if (player != null && player == thrower) continue;
            if (player == null) { obstruction = Mathf.Min(obstruction, throwHits[i].distance); continue; }
            if (throwHits[i].distance < nearest) { nearest = throwHits[i].distance; victim = player; }
        }
        if (victim != null && nearest <= obstruction + .01f) HitPlayer(victim, velocity);
        if (Time.time > dangerousUntil) return;
        count = physics.OverlapCapsule(center + axis, center - axis, radius, obstacles, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var player = obstacles[i].GetComponentInParent<PlayerAvatar>();
            if (player == null || player == thrower) continue;
            Vector3 point = obstacles[i].ClosestPoint(center);
            Vector3 ray = point - center;
            if (ray.magnitude > .01f && physics.Raycast(center, ray.normalized, out RaycastHit hit, ray.magnitude + .01f,
                    ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(player.transform) &&
                    !hit.collider.transform.IsChildOf(transform)) continue;
            HitPlayer(player, velocity);
            break;
        }
    }

    private void HitPlayer(PlayerAvatar player, Vector3 velocity)
    {
        if (!HasAuthority || IsHeld || player == thrower || dangerousUntil <= 0 || Time.time > dangerousUntil || velocity.sqrMagnitude < 25) return;
        player.GetComponent<PlayerKnockdown>()?.TryFall(velocity.normalized);
        // One throw causes one impact; rolling or bouncing bodies cannot repeatedly stun players.
        dangerousUntil = 0;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!HasAuthority || IsHeld || dangerousUntil <= 0 || Time.time > dangerousUntil) return;
        var player = collision.collider.GetComponentInParent<PlayerAvatar>();
        if (player != null) HitPlayer(player, collision.relativeVelocity);
        else dangerousUntil = 0;
    }

    private IEnumerator RestoreAfterThrow()
    {
        yield return new WaitForSeconds(.6f);
        RestoreCollisions();
    }

    private void RestoreCollisions()
    {
        if (restoreCollisions != null) StopCoroutine(restoreCollisions);
        restoreCollisions = null;
        foreach (Collider collider in ignored) if (collider != null && capsule != null) Physics.IgnoreCollision(capsule, collider, false);
        ignored.Clear();
    }

    private void OnDisable()
    {
        dangerousUntil = 0;
        if (HasAuthority && holder != null) Release(false);
        else if (holder != null) ApplyHolder(null);
        RestoreCollisions();
    }
}
