using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(GrayboxPlayerController))]
public sealed class NetworkPlayer : NetworkBehaviour
{
    [SerializeField] private ClothingDefinition[] clothingCatalog;
    private GrayboxPlayerController motor;
    private GrayboxFirstPersonCamera view;
    private PlayerPickupInteractor interactor;
    private Vector2 move;
    private float yaw;
    private float pitch;
    private bool sprint;
    private bool crouch;
    private bool jumpQueued;

    public float ViewYaw => yaw;
    public float ViewPitch => pitch;

    private void Awake() => motor = GetComponent<GrayboxPlayerController>();

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (IsOwner) StartCoroutine(BindLocalView());
    }

    private IEnumerator BindLocalView()
    {
        while (Camera.main == null) yield return null;
        view = Camera.main.GetComponent<GrayboxFirstPersonCamera>();
        interactor = Camera.main.GetComponent<PlayerPickupInteractor>();
        if (view != null) view.SetTarget(transform);
        if (interactor != null) interactor.SetLocalPlayer(this);
        NetworkLobby.Instance?.SetPlaying(true);
    }

    public override void OnStopClient()
    {
        if (IsOwner)
        {
            if (view != null) view.SetTarget(null);
            if (interactor != null) interactor.SetLocalPlayer(null);
            NetworkLobby.Instance?.SetPlaying(false);
        }
        base.OnStopClient();
    }

    private void Update()
    {
        if (IsOwner && view != null && Keyboard.current != null)
        {
            Vector2 input = GrayboxPlayerController.ReadMoveInput();
            float lookYaw = view.transform.eulerAngles.y;
            float lookPitch = view.transform.eulerAngles.x;
            bool isSprinting = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            bool isCrouching = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            bool jump = Keyboard.current.spaceKey.wasPressedThisFrame;
            SubmitInputServerRpc(input, lookYaw, lookPitch, isSprinting, isCrouching, jump);
        }

        if (IsServerStarted)
        {
            motor.Simulate(move, yaw, jumpQueued, sprint, crouch, Time.deltaTime);
            jumpQueued = false;
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void SubmitInputServerRpc(Vector2 input, float lookYaw, float lookPitch,
        bool isSprinting, bool isCrouching, bool jump, Channel channel = Channel.Unreliable)
    {
        if (!float.IsFinite(lookYaw) || !float.IsFinite(lookPitch)) return;
        move = Vector2.ClampMagnitude(input, 1f);
        yaw = lookYaw;
        pitch = Mathf.DeltaAngle(0f, lookPitch);
        sprint = isSprinting;
        crouch = isCrouching;
        jumpQueued |= jump;
    }

    public void RequestPickup(NetworkPickup pickup)
    {
        if (!IsOwner || pickup == null) return;
        RequestPickupServerRpc(pickup.NetworkObject);
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestPickupServerRpc(NetworkObject target)
    {
        if (target == null || target.gameObject.scene != gameObject.scene) return;
        NetworkPickup pickup = target.GetComponent<NetworkPickup>();
        if (pickup == null || Vector3.Distance(transform.position, pickup.transform.position) > 3.8f) return;
        Collider itemCollider = pickup.GetComponentInChildren<Collider>();
        Vector3 point = itemCollider != null ? itemCollider.bounds.center : pickup.transform.position;
        Vector3 eye = transform.position + Vector3.up * Mathf.Lerp(1.65f, 0.95f, motor.CrouchAmount);
        Vector3 toItem = point - eye;
        float distance = toItem.magnitude;
        if (distance > 3.8f || distance < 0.01f) return;
        Vector3 looking = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        if (Vector3.Angle(looking, toItem) > 28f) return;
        if (gameObject.scene.GetPhysicsScene().Raycast(eye, toItem / distance, out RaycastHit hit,
                distance + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide) &&
            !hit.collider.transform.IsChildOf(pickup.transform)) return;
        pickup.TryCollect(this);
    }

    public void AwardClothing(ClothingDefinition clothing)
    {
        if (!IsServerStarted || clothing == null) return;
        EquipClothingTargetRpc(Owner, clothing.name);
    }

    [TargetRpc]
    private void EquipClothingTargetRpc(NetworkConnection connection, string clothingId)
    {
        foreach (ClothingDefinition definition in clothingCatalog)
        {
            if (definition == null || definition.name != clothingId) continue;
            MannequinWardrobe wardrobe = FindAnyObjectByType<MannequinWardrobe>();
            if (wardrobe != null) wardrobe.Equip(definition);
            return;
        }
        Debug.LogWarning("Одежда отсутствует в каталоге игрока: " + clothingId, this);
    }
}
