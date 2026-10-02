using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(GrayboxPlayerController))]
public sealed class NetworkPlayer : NetworkBehaviour, IPlayerViewSource
{
    [SerializeField] private ClothingDefinition[] clothingCatalog;

    // Что собрал игрок: id вещей (имена ClothingDefinition). Ведёт сервер, получает только владелец.
    private readonly SyncList<string> wornClothing = new SyncList<string>(new SyncTypeSettings(ReadPermission.OwnerOnly));

    private GrayboxPlayerController motor;
    private PlayerOutfit outfit;
    private MannequinWardrobe wardrobe;
    // Кадр камеры владельца: нужен серверу, чтобы считать, видит ли игрок манекена
    private float viewFieldOfView = 60f;
    private float viewAspect = 16f / 9f;
    private float sentFieldOfView = -1f;
    private float sentAspect = -1f;
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

    private void Awake()
    {
        motor = GetComponent<GrayboxPlayerController>();
        // Игрок для манекенов (PlayerAvatar) и его комплект (PlayerOutfit) — обычные компоненты, добавляем сами
        if (GetComponent<PlayerAvatar>() == null) gameObject.AddComponent<PlayerAvatar>();
        outfit = GetComponent<PlayerOutfit>();
        wornClothing.OnChange += OnWornClothingChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        outfit.Changed += ServerSyncOutfit;
        outfit.Removed += ServerOutfitRemoved;
    }

    public override void OnStopServer()
    {
        outfit.Changed -= ServerSyncOutfit;
        outfit.Removed -= ServerOutfitRemoved;
        base.OnStopServer();
    }

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
        // HUD-манекен своей комнаты показывает комплект этого игрока
        wardrobe = FindWardrobe();
        if (wardrobe != null) wardrobe.Bind(outfit);
        ApplyWornClothing();
        NetworkLobby.Instance?.SetPlaying(true);
    }

    public override void OnStopClient()
    {
        if (IsOwner)
        {
            if (view != null) view.SetTarget(null);
            if (interactor != null) interactor.SetLocalPlayer(null);
            if (wardrobe != null) wardrobe.Bind(null);
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

            // Угол обзора и пропорции экрана меняются редко — шлём, только когда изменились
            Camera camera = view.GetComponent<Camera>();
            if (camera != null && (Mathf.Abs(camera.fieldOfView - sentFieldOfView) > 0.5f ||
                                   Mathf.Abs(camera.aspect - sentAspect) > 0.01f))
            {
                sentFieldOfView = camera.fieldOfView;
                sentAspect = camera.aspect;
                SubmitViewServerRpc(sentFieldOfView, sentAspect);
            }
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

    [ServerRpc(RequireOwnership = true)]
    private void SubmitViewServerRpc(float fieldOfView, float aspect)
    {
        if (!float.IsFinite(fieldOfView) || !float.IsFinite(aspect)) return;
        viewFieldOfView = Mathf.Clamp(fieldOfView, 20f, 120f);
        viewAspect = Mathf.Clamp(aspect, 0.5f, 4f);
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

    /// <summary>Сервер: выдать вещь игроку. Она попадает в его PlayerOutfit, а владельцу приходит список.</summary>
    public void AwardClothing(ClothingDefinition clothing)
    {
        if (!IsServerStarted || clothing == null) return;
        outfit.Add(clothing);
    }

    // ---------- Комплект одежды ----------

    // Сервер: список id повторяет комплект (вещь добавили, Воришка украл)
    private void ServerSyncOutfit()
    {
        if (!IsServerStarted) return;
        var ids = new List<string>();
        foreach (ClothingDefinition clothing in outfit.Items)
            if (clothing != null) ids.Add(clothing.name);

        bool same = ids.Count == wornClothing.Count;
        if (same)
            foreach (string id in ids)
                if (!wornClothing.Contains(id)) { same = false; break; }
        if (same) return;

        wornClothing.Clear();
        foreach (string id in ids) wornClothing.Add(id);
    }

    // Сервер: вещь пропала с подписью («Воришка украл») — показать её владельцу
    private void ServerOutfitRemoved(ClothingDefinition clothing, string reason)
    {
        if (!IsServerStarted || clothing == null || string.IsNullOrEmpty(reason) || !Owner.IsValid) return;
        OutfitMessageTargetRpc(Owner, reason + ": " + clothing.DisplayName);
    }

    [TargetRpc]
    private void OutfitMessageTargetRpc(NetworkConnection connection, string text)
    {
        if (wardrobe == null) wardrobe = FindWardrobe();
        if (wardrobe != null) wardrobe.ShowMessage(text);
    }

    // Клиент-владелец: пришёл новый список — повторяем его в своём PlayerOutfit (HUD обновится сам)
    private void OnWornClothingChanged(SyncListOperation op, int index, string previous, string next, bool asServer)
    {
        if (asServer || IsServerStarted) return; // на сервере (и у хоста) комплект и так главный
        ApplyWornClothing();
    }

    private void ApplyWornClothing()
    {
        if (IsServerStarted || outfit == null) return;
        var items = new List<ClothingDefinition>();
        foreach (string id in wornClothing)
        {
            ClothingDefinition clothing = FindInCatalog(id);
            if (clothing != null) items.Add(clothing);
            else Debug.LogWarning("Одежда отсутствует в каталоге игрока: " + id, this);
        }
        outfit.SetItems(items);
    }

    private ClothingDefinition FindInCatalog(string clothingId)
    {
        if (clothingCatalog == null) return null;
        foreach (ClothingDefinition definition in clothingCatalog)
            if (definition != null && definition.name == clothingId) return definition;
        return null;
    }

    private MannequinWardrobe FindWardrobe()
    {
        foreach (MannequinWardrobe candidate in FindObjectsByType<MannequinWardrobe>())
            if (candidate.gameObject.scene == gameObject.scene) return candidate;
        return FindAnyObjectByType<MannequinWardrobe>();
    }

    // ---------- Взгляд игрока для манекенов (IPlayerViewSource) ----------

    bool IPlayerViewSource.IsLocalPlayer => IsOwner;
    bool IPlayerViewSource.HasView => IsServerStarted;
    Vector3 IPlayerViewSource.EyePosition =>
        transform.position + Vector3.up * Mathf.Lerp(1.65f, 0.95f, motor.CrouchAmount); // как у камеры
    Quaternion IPlayerViewSource.EyeRotation => Quaternion.Euler(pitch, yaw, 0f);
    float IPlayerViewSource.FieldOfView => viewFieldOfView;
    float IPlayerViewSource.Aspect => viewAspect;
}
