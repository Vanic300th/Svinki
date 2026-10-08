using UnityEngine;

/// <summary>Server-approved physics fall and automatic recovery, with a short stun immunity.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(PlayerRagdoll)), DefaultExecutionOrder(150)]
public sealed class PlayerKnockdown : MonoBehaviour
{
    public const float Duration = 2f;
    [SerializeField] private Transform characterVisual;
    private NetworkPlayer network;
    private PlayerRagdoll ragdoll;
    private Transform head;
    private Vector3 slideVelocity, direction;
    private float downUntil, immuneUntil;
    private RagdollFrame pendingFrame;
    public bool IsDown { get; private set; }
    public int Hits { get; private set; }
    public bool IsDead => Hits >= 2;
    public int HitsRemaining => Mathf.Max(0, 2 - Hits);
    public uint FallId { get; private set; }
    public float VisualAmount { get; private set; }
    public Vector3 HeadPosition => head != null ? head.position : transform.position + Vector3.up * .45f;
    public Vector3 EyePosition => Vector3.Lerp(transform.position + Vector3.up * 1.65f, HeadPosition + Vector3.up * .15f, VisualAmount);
    public float EyeHeight => Mathf.Clamp(EyePosition.y - transform.position.y, .2f, 1.65f);
    public bool CanFall => !IsDead && !IsDown && Time.time >= immuneUntil && isActiveAndEnabled;

    private void Awake()
    {
        network = GetComponent<NetworkPlayer>();
        ragdoll = GetComponent<PlayerRagdoll>() ?? gameObject.AddComponent<PlayerRagdoll>();
        if (characterVisual == null) characterVisual = GetComponentInChildren<PigAppearance>(true)?.transform;
        if (characterVisual == null) return;
        foreach (Transform bone in characterVisual.GetComponentsInChildren<Transform>(true))
            if (bone.name == "Head") { head = bone; break; }
    }

    public bool TryFall(Vector3 impactDirection)
    {
        if (!CanFall) return false;
        if (network != null) return network.TryKnockDown(impactDirection);
        GetComponent<PlayerMannequinCarry>()?.Release(false);
        SetDown(true, impactDirection);
        return true;
    }

    // Called only by an authoritative NPC attack, never by a client damage RPC.
    public bool TryMannequinHit(Vector3 impactDirection)
    {
        if (!CanFall || NetworkLobby.Instance?.Results != null) return false;
        if (network != null) return network.TryMannequinHit(impactDirection);
        ApplyHit(Hits + 1);
        if (IsDead) DropOutfit(null);
        GetComponent<PlayerMannequinCarry>()?.Release(false);
        ShoppingCart.For(GetComponent<PlayerAvatar>())?.Release(GetComponent<PlayerAvatar>(), false);
        SetDown(true, impactDirection);
        return true;
    }
    public void ApplyHit(int count) => Hits = Mathf.Clamp(count, 0, 2);
    public void DropOutfit(FishNet.Managing.NetworkManager server)
    {
        var avatar = GetComponent<PlayerAvatar>();
        if (avatar == null) return;
        for (int i = 0; i < 4; i++) ClothingDropper.TryDrop(avatar, (ClothingSlot)i, server, true);
    }

    public void SetDown(bool down, Vector3 impactDirection)
    {
        if (IsDown == down) return;
        IsDown = down;
        if (down)
        {
            GetComponent<PlayerMonkeyCarry>()?.Release();
            FallId++;
            direction = Vector3.ProjectOnPlane(impactDirection, Vector3.up).normalized;
            if (direction.sqrMagnitude < .01f) direction = transform.forward;
            downUntil = Time.time + Duration;
            slideVelocity = direction * 3.8f;
            ragdoll.Begin(direction);
        }
        else
        {
            immuneUntil = Time.time + 1; slideVelocity = Vector3.zero;
            ragdoll.End();
        }
    }

    public void ApplyNetworkPose(KnockdownPose value)
    {
        if (value.Down && (!IsDown || value.FallId != FallId))
        {
            if (IsDown) SetDown(false, Vector3.zero);
            FallId = value.FallId - 1;
            SetDown(true, value.Direction);
            if (pendingFrame.FallId == FallId) ragdoll.Receive(pendingFrame);
            pendingFrame = default;
        }
        else if (!value.Down && value.FallId >= FallId) SetDown(false, Vector3.zero);
    }

    public void ReceiveRagdoll(RagdollFrame frame)
    {
        if (frame.FallId < FallId) return;
        if (IsDown && frame.FallId == FallId) ragdoll.Receive(frame);
        else if (frame.FallId > FallId) pendingFrame = frame; // RPC may arrive before the SyncVar.
    }

    public RagdollFrame CaptureRagdoll() => ragdoll.Capture(FallId);

    public Vector3 ConsumeSlide(float deltaTime)
    {
        if (IsDown && ragdoll.BodyCount > 0 && deltaTime > 0)
            return Vector3.ProjectOnPlane(ragdoll.PelvisPosition - transform.position, Vector3.up) / deltaTime;
        Vector3 velocity = slideVelocity;
        slideVelocity *= Mathf.Exp(-7 * deltaTime);
        return velocity;
    }

    private void Update()
    {
        bool authority = network == null || network.NetworkObject != null && network.IsServerInitialized;
        if (authority && IsDown && !IsDead && Time.time >= downUntil)
        {
            if (network != null) network.RecoverFromKnockdown();
            else SetDown(false, direction);
        }
        VisualAmount = Mathf.MoveTowards(VisualAmount, IsDown ? 1 : 0, Time.deltaTime * (IsDown ? 6 : 1.82f));
    }

    private void OnDisable()
    {
        IsDown = false; VisualAmount = 0; slideVelocity = Vector3.zero; pendingFrame = default;
        ragdoll?.End();
    }
}

public struct KnockdownPose
{
    public bool Down;
    public Vector3 Direction;
    public uint FallId;
}
