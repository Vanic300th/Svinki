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

    // Надетые вещи видят все игроки в комнате; сервер остаётся единственным источником изменений.
    private readonly SyncList<string> wornClothing = new SyncList<string>(new SyncTypeSettings(ReadPermission.Observers));

    private GrayboxPlayerController motor;
    private PlayerOutfit outfit;
    private MannequinWardrobe wardrobe;
    private WorldOutfitRenderer worldOutfit;
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
    private float nextPoseSend;
    private readonly SyncVar<string> participantId = new SyncVar<string>();
    private readonly SyncVar<string> participantName = new SyncVar<string>();
    private readonly SyncVar<PlayerPose> pose = new SyncVar<PlayerPose>();
    private Light remoteFlashlight;
    public string ParticipantId => participantId.Value;
    public string ParticipantName => participantName.Value;
    public bool FlashlightOn => pose.Value.Flashlight;
    public void SetIdentity(string id, string nickname) { participantId.Value = id; participantName.Value = nickname; }
    public void RestoreOutfit(string[] ids)
    {
        var items = new List<ClothingDefinition>();
        foreach (string id in ids) { var item = FindInCatalog(id); if (item != null) items.Add(item); }
        GetComponent<PlayerOutfit>().SetItems(items);
    }
    public void FreezeDisconnected() { move = Vector2.zero; sprint = false; }
    public override void OnOwnershipClient(NetworkConnection previousOwner)
    {
        base.OnOwnershipClient(previousOwner);
        if (worldOutfit != null) worldOutfit.SetFirstPersonHidden(IsOwner);
        if (IsOwner) StartCoroutine(BindLocalView()); else UnbindLocalView();
    }
    private void UnbindLocalView()
    {
        if (view == null) return;
        view.SetTarget(null);
        if (interactor != null) { interactor.SetLocalPlayer(null); interactor.enabled = false; }
        if (wardrobe != null) wardrobe.Bind(null);
        view = null; interactor = null;
    }

    public float ViewYaw => yaw;
    public float ViewPitch => pitch;

    private void Awake()
    {
        motor = GetComponent<GrayboxPlayerController>();
        GameObject lamp = new GameObject("Remote flashlight"); lamp.transform.SetParent(transform, false);
        remoteFlashlight = lamp.AddComponent<Light>(); remoteFlashlight.type = LightType.Spot;
        remoteFlashlight.range = 18f; remoteFlashlight.spotAngle = 58f; remoteFlashlight.intensity = 2f;
        remoteFlashlight.enabled = false;
        // Игрок для манекенов (PlayerAvatar) и его комплект (PlayerOutfit) — обычные компоненты, добавляем сами
        if (GetComponent<PlayerAvatar>() == null) gameObject.AddComponent<PlayerAvatar>();
        outfit = GetComponent<PlayerOutfit>();
        worldOutfit = GetComponent<WorldOutfitRenderer>();
        wornClothing.OnChange += OnWornClothingChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        outfit.Changed += ServerSyncOutfit;
        outfit.Removed += ServerOutfitRemoved;
        ServerSyncOutfit();
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
        if (worldOutfit != null) worldOutfit.SetFirstPersonHidden(IsOwner);
        ApplyWornClothing();
        if (IsOwner) StartCoroutine(BindLocalView());
    }

    private IEnumerator BindLocalView()
    {
        while (Camera.main == null) { if (!IsOwner) yield break; yield return null; }
        if (!IsOwner) yield break;
        view = Camera.main.GetComponent<GrayboxFirstPersonCamera>();
        interactor = Camera.main.GetComponent<PlayerPickupInteractor>();
        if (view != null) view.SetTarget(transform);
        if (interactor != null) { interactor.SetLocalPlayer(this); interactor.enabled = true; }
        // HUD-манекен своей комнаты показывает комплект этого игрока
        wardrobe = FindWardrobe();
        if (wardrobe != null) wardrobe.Bind(outfit);
        ApplyWornClothing();
        NetworkLobby.Instance?.SetPlaying(true);
    }

    public override void OnStopClient()
    {
        UnbindLocalView();
        base.OnStopClient();
    }

    private void Update()
    {
        if (IsOwner && view != null && Keyboard.current != null)
        {
            bool allowed = NetworkLobby.Instance == null || NetworkLobby.Instance.InputAllowed;
            move = allowed ? GrayboxPlayerController.ReadMoveInput() : Vector2.zero;
            yaw = view.transform.eulerAngles.y; pitch = Mathf.DeltaAngle(0f, view.transform.eulerAngles.x);
            sprint = allowed && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            crouch = allowed && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
            motor.Simulate(move, yaw, allowed && Keyboard.current.spaceKey.wasPressedThisFrame, sprint, crouch, Time.deltaTime);
            if (Time.unscaledTime >= nextPoseSend)
            {
                nextPoseSend = Time.unscaledTime + .05f;
                bool lightOn = view.GetComponent<FlashlightController>()?.IsOn ?? true;
                SubmitPoseServerRpc(new PlayerPose { Yaw = yaw, Pitch = pitch, Crouch = motor.CrouchAmount, Flashlight = lightOn });
            }
            Camera camera = view.GetComponent<Camera>();
            if (camera != null && (Mathf.Abs(camera.fieldOfView - sentFieldOfView) > .5f || Mathf.Abs(camera.aspect - sentAspect) > .01f))
            {
                sentFieldOfView = camera.fieldOfView; sentAspect = camera.aspect;
                SubmitViewServerRpc(sentFieldOfView, sentAspect);
            }
        }
        else
        {
            yaw = pose.Value.Yaw; pitch = pose.Value.Pitch;
            motor.SetRemoteStance(pose.Value.Crouch);
        }
        if (remoteFlashlight != null)
        {
            remoteFlashlight.enabled = !IsOwner && pose.Value.Flashlight;
            remoteFlashlight.transform.position = EyePosition;
            remoteFlashlight.transform.rotation = EyeRotation;
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void SubmitPoseServerRpc(PlayerPose value, Channel channel = Channel.Unreliable)
    {
        if (!float.IsFinite(value.Yaw) || !float.IsFinite(value.Pitch) || !float.IsFinite(value.Crouch)) return;
        value.Crouch = Mathf.Clamp01(value.Crouch); pose.Value = value;
        yaw = value.Yaw; pitch = value.Pitch;
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
        if (clothingCatalog != null)
            foreach (ClothingDefinition definition in clothingCatalog)
                if (definition != null && definition.name == clothingId) return definition;
        if (outfit != null)
            foreach (ClothingDefinition definition in outfit.StartingItems)
                if (definition.name == clothingId) return definition;
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
    public Vector3 EyePosition =>
        transform.position + Vector3.up * Mathf.Lerp(1.65f, 0.95f, motor.CrouchAmount); // как у камеры
    public Quaternion EyeRotation => Quaternion.Euler(pitch, yaw, 0f);
    float IPlayerViewSource.FieldOfView => viewFieldOfView;
    float IPlayerViewSource.Aspect => viewAspect;
}

public struct PlayerPose
{
    public float Yaw, Pitch, Crouch;
    public bool Flashlight;
}
