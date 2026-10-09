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
    private float nextChatAllowed;
    private VoicePlayback voicePlayback;
    private float voiceBudget=8,voiceBudgetAt,lastVoiceAt=-10;
    private ushort lastVoiceSequence;
    public int RelayedVoiceFrames { get; private set; }
    public void SendVoice(ushort sequence,byte[] packet)
    { if(IsOwner&&IsClientStarted)VoiceServerRpc(sequence,packet); }
    [ServerRpc(RequireOwnership = true)]
    private void VoiceServerRpc(ushort sequence,byte[] packet,Channel channel=Channel.Unreliable)
    {
        if(!VoiceCodec.IsValid(packet)||IsDead||NetworkLobby.Instance?.CanEditOutfit(this)!=true)return;
        float now=Time.unscaledTime;
        voiceBudget=Mathf.Min(8,voiceBudget+Mathf.Max(0,now-voiceBudgetAt)*60);voiceBudgetAt=now;
        if(voiceBudget<1||now-lastVoiceAt<.5f&&!VoiceCodec.IsNewer(sequence,lastVoiceSequence))return;
        voiceBudget-=1;lastVoiceAt=now;lastVoiceSequence=sequence;RelayedVoiceFrames++;
        VoiceObserversRpc(sequence,packet);
    }
    [ObserversRpc]
    private void VoiceObserversRpc(ushort sequence,byte[] packet,Channel channel=Channel.Unreliable)
    {
        if(IsOwner||!IsClientStarted)return;
        if(voicePlayback==null)voicePlayback=GetComponent<VoicePlayback>()??gameObject.AddComponent<VoicePlayback>();
        voicePlayback.Receive(sequence,packet);
    }
    private readonly SyncVar<uint> emote = new SyncVar<uint>();
    private float emoteUntil, nextEmoteAllowed;
    private PigMotion pigMotion;
    public void RequestEmote(PigEmote kind) { if (IsOwner) EmoteServerRpc((byte)kind); }
    private void OnEmoteChanged(uint previous, uint next, bool asServer)
    { if (asServer || !IsServerInitialized) pigMotion?.PlayEmote((PigEmote)(next & 7u)); }
    [ServerRpc(RequireOwnership = true)]
    private void EmoteServerRpc(byte kind)
    {
        if (kind < 1 || kind > 6 || Time.time < nextEmoteAllowed || NetworkLobby.Instance?.CanEditOutfit(this) != true ||
            !PigMotion.CanEmote(GetComponent<PlayerAvatar>())) return;
        nextEmoteAllowed = Time.time + .8f; emoteUntil = Time.time + PigMotion.EmoteDuration;
        emote.Value = ((emote.Value & ~7u) + 8u) | kind;
    }
    private float nextRagdollSend;
    private readonly SyncVar<string> participantId = new SyncVar<string>();
    private readonly SyncVar<string> participantName = new SyncVar<string>();
    private readonly SyncVar<PlayerPose> pose = new SyncVar<PlayerPose>();
    private readonly SyncVar<int> pigFace = new SyncVar<int>();
    private readonly SyncVar<int> hitsTaken = new SyncVar<int>();
    public int HitsTaken => hitsTaken.Value;
    public bool IsDead => hitsTaken.Value >= 2;
    private readonly SyncVar<KnockdownPose> knockdownPose = new SyncVar<KnockdownPose>();
    private PlayerKnockdown knockdown;
    private PigAppearance pigAppearance;
    private Light remoteFlashlight;
    public string ParticipantId => participantId.Value;
    public string ParticipantName => participantName.Value;
    public bool FlashlightOn => pose.Value.Flashlight;
    public float MotionSpeed => pose.Value.Speed;
    public float VerticalSpeed => pose.Value.VerticalSpeed;
    public bool Grounded => pose.Value.Grounded;
    public void SetIdentity(string id, string nickname) { participantId.Value = id; participantName.Value = nickname; }
    public void SetAppearance(int value)
    {
        pigFace.Value = PigFace.Sanitize(value);
        pigAppearance?.Apply(pigFace.Value);
    }
    private void OnPigFaceChanged(int previous, int next, bool asServer) => pigAppearance?.Apply(next);
    public void RestoreOutfit(string[] ids)
    {
        var items = new List<ClothingDefinition>();
        foreach (string id in ids) { var item = FindInCatalog(id); if (item != null) items.Add(item); }
        GetComponent<PlayerOutfit>().SetItems(items);
    }
    public void FreezeDisconnected() { GetComponent<PlayerMonkeyCarry>()?.Release(); move = Vector2.zero; sprint = false; GetComponent<PlayerMannequinCarry>()?.Release(false); ShoppingCart.For(GetComponent<PlayerAvatar>())?.Release(GetComponent<PlayerAvatar>(), false); }
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
        knockdown = GetComponent<PlayerKnockdown>();
        knockdownPose.OnChange += OnKnockdownChanged;
        hitsTaken.OnChange += (previous, next, asServer) => knockdown?.ApplyHit(next);
        GameObject lamp = new GameObject("Remote flashlight"); lamp.transform.SetParent(transform, false);
        remoteFlashlight = lamp.AddComponent<Light>(); remoteFlashlight.type = LightType.Spot;
        FlashlightController.ConfigureLight(remoteFlashlight);
        remoteFlashlight.enabled = false;
        // Игрок для манекенов (PlayerAvatar) и его комплект (PlayerOutfit) — обычные компоненты, добавляем сами
        if (GetComponent<PlayerAvatar>() == null) gameObject.AddComponent<PlayerAvatar>();
        if (GetComponent<PlayerNameplate>() == null) gameObject.AddComponent<PlayerNameplate>();
        outfit = GetComponent<PlayerOutfit>();
        worldOutfit = GetComponent<WorldOutfitRenderer>();
        pigAppearance = GetComponentInChildren<PigAppearance>(true);
        pigMotion = pigAppearance?.GetComponent<PigMotion>();
        emote.OnChange += OnEmoteChanged;
        pigFace.OnChange += OnPigFaceChanged;
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
        pigAppearance?.Apply(pigFace.Value);
        pigMotion?.PlayEmote((PigEmote)(emote.Value & 7u));
        knockdown?.ApplyHit(hitsTaken.Value);
        knockdown?.ApplyNetworkPose(knockdownPose.Value);
        if (IsOwner) StartCoroutine(BindLocalView());
    }

    private IEnumerator BindLocalView()
    {
        while (Camera.main == null) { if (!IsOwner) yield break; yield return null; }
        if (!IsOwner) yield break;
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(gameObject.scene);
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
        if (NetworkObject != null && IsServerInitialized && (emote.Value & 7u) != 0 &&
            (Time.time >= emoteUntil || MotionSpeed > .3f || !PigMotion.CanEmote(GetComponent<PlayerAvatar>()) || NetworkLobby.Instance?.CanEditOutfit(this) != true))
            emote.Value &= ~7u;
        if (IsOwner && view != null && Keyboard.current != null)
        {
            bool allowed = (knockdown == null || !knockdown.IsDown) && !PlayerChat.BlocksInput && (NetworkLobby.Instance == null || NetworkLobby.Instance.InputAllowed);
            move = allowed ? GrayboxPlayerController.ReadMoveInput() : Vector2.zero;
            yaw = view.transform.eulerAngles.y; pitch = Mathf.DeltaAngle(0f, view.transform.eulerAngles.x);
            sprint = allowed && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            crouch = allowed && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
            motor.Simulate(move, yaw, allowed && Keyboard.current.spaceKey.wasPressedThisFrame, sprint, crouch, Time.deltaTime);
            if (Time.unscaledTime >= nextPoseSend)
            {
                nextPoseSend = Time.unscaledTime + .05f;
                var cart = ShoppingCart.For(GetComponent<PlayerAvatar>());
                if (cart != null && cart.Driver == GetComponent<PlayerAvatar>()) CartControlServerRpc(cart.GetComponent<NetworkObject>(), move, sprint);
                bool lightOn = !IsDead && (view.GetComponentInChildren<FlashlightController>()?.IsOn ?? false);
                SubmitPoseServerRpc(new PlayerPose { Yaw = yaw, Pitch = pitch, Crouch = motor.CrouchAmount, Flashlight = lightOn,
                    Speed = motor.MotionSpeed, VerticalSpeed = motor.VerticalVelocity, Grounded = motor.Grounded });
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
            remoteFlashlight.enabled = !IsOwner && !IsDead && pose.Value.Flashlight;
            remoteFlashlight.transform.position = EyePosition;
            remoteFlashlight.transform.rotation = EyeRotation;
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void SubmitPoseServerRpc(PlayerPose value, Channel channel = Channel.Unreliable)
    {
        if (!float.IsFinite(value.Yaw) || !float.IsFinite(value.Pitch) || !float.IsFinite(value.Crouch) ||
            !float.IsFinite(value.Speed) || !float.IsFinite(value.VerticalSpeed)) return;
        value.Speed = Mathf.Clamp(value.Speed, 0, 12);
        value.VerticalSpeed = Mathf.Clamp(value.VerticalSpeed, -40, 12);
        value.Crouch = Mathf.Clamp01(value.Crouch); pose.Value = value;
        yaw = value.Yaw; pitch = value.Pitch;
    }

    // Hit detection belongs to the server; there is no client RPC that can knock somebody down.
    public bool TryKnockDown(Vector3 direction)
    {
        if (NetworkObject == null || !IsServerInitialized || knockdown == null || !knockdown.CanFall) return false;
        GetComponent<PlayerMannequinCarry>()?.Release(false);
        ShoppingCart.For(GetComponent<PlayerAvatar>())?.Release(GetComponent<PlayerAvatar>(), false);
        knockdown.SetDown(true, direction);
        knockdownPose.Value = new KnockdownPose { Down = true, Direction = direction, FallId = knockdown.FallId };
        return true;
    }

    public bool TryMannequinHit(Vector3 direction)
    {
        if (!IsServerInitialized || knockdown == null || !knockdown.CanFall ||
            NetworkLobby.Instance?.CanEditOutfit(this) != true) return false;
        hitsTaken.Value = Mathf.Min(2, hitsTaken.Value + 1);
        knockdown.ApplyHit(hitsTaken.Value);
        if (IsDead) knockdown.DropOutfit(NetworkObject.NetworkManager);
        GetComponent<PlayerMannequinCarry>()?.Release(false);
        ShoppingCart.For(GetComponent<PlayerAvatar>())?.Release(GetComponent<PlayerAvatar>(), false);
        knockdown.SetDown(true, direction);
        knockdownPose.Value = new KnockdownPose { Down = true, Direction = direction, FallId = knockdown.FallId };
        if (IsDead) NetworkLobby.Instance.PlayerEliminated(this);
        return true;
    }

    public void RestoreEliminated()
    {
        if (!IsServerInitialized || knockdown == null) return;
        hitsTaken.Value = 2; knockdown.ApplyHit(2); outfit.SetItems(System.Array.Empty<ClothingDefinition>());
        knockdown.SetDown(true, Vector3.forward);
        knockdownPose.Value = new KnockdownPose { Down = true, Direction = Vector3.forward, FallId = knockdown.FallId };
    }

    public void RecoverFromKnockdown()
    {
        if (NetworkObject == null || !IsServerInitialized || IsDead) return;
        knockdown?.SetDown(false, Vector3.zero);
        knockdownPose.Value = new KnockdownPose { FallId = knockdown != null ? knockdown.FallId : 0 };
    }

    private void OnKnockdownChanged(KnockdownPose previous, KnockdownPose next, bool asServer)
    {
        if (!asServer && !IsServerInitialized) knockdown?.ApplyNetworkPose(next);
    }

    private void LateUpdate()
    {
        if (NetworkObject == null || !IsServerInitialized || knockdown == null || !knockdown.IsDown || Time.unscaledTime < nextRagdollSend) return;
        nextRagdollSend = Time.unscaledTime + .05f;
        RagdollObserversRpc(knockdown.CaptureRagdoll());
    }

    [ObserversRpc]
    private void RagdollObserversRpc(RagdollFrame frame, Channel channel = Channel.Unreliable)
    {
        if (!IsServerInitialized) knockdown?.ReceiveRagdoll(frame);
    }

    public void SendChat(string message)
    {
        if (IsOwner && IsClientStarted) SendChatServerRpc(message);
    }

    [ServerRpc(RequireOwnership = true)]
    private void SendChatServerRpc(string message)
    {
        if (Time.unscaledTime < nextChatAllowed) return;
        string clean = PlayerChat.CleanMessage(message);
        if (clean.Length == 0) return;
        nextChatAllowed = Time.unscaledTime + .5f;
        ShowChatObserversRpc(clean);
    }

    [ObserversRpc]
    private void ShowChatObserversRpc(string message) => PlayerChatBubble.Show(gameObject, message);

    [ServerRpc(RequireOwnership = true)]
    private void SubmitViewServerRpc(float fieldOfView, float aspect)
    {
        if (!float.IsFinite(fieldOfView) || !float.IsFinite(aspect)) return;
        viewFieldOfView = Mathf.Clamp(fieldOfView, 20f, 120f);
        viewAspect = Mathf.Clamp(aspect, 0.5f, 4f);
    }

    public void RequestCart(ShoppingCart cart, bool sit, bool launch = false)
    {
        if (IsOwner && cart != null) CartUseServerRpc(cart.GetComponent<NetworkObject>(), sit, launch);
    }
    [ServerRpc(RequireOwnership = true)]
    private void CartUseServerRpc(NetworkObject target, bool sit, bool launch)
    {
        if (target == null || target.gameObject.scene != gameObject.scene) return;
        var cart = target.GetComponent<ShoppingCart>(); var avatar = GetComponent<PlayerAvatar>();
        if (cart == null) return;
        if (ShoppingCart.For(avatar) == cart) { cart.Release(avatar, launch && cart.Driver == avatar); return; }
        if (launch || NetworkLobby.Instance?.CanEditOutfit(this) != true || knockdown?.IsDown == true) return;
        var collider = cart.GetComponent<BoxCollider>();
        Vector3 direction = collider.ClosestPoint(EyePosition) - EyePosition;
        if (direction.magnitude > 3.8f || direction.magnitude < .01f || Vector3.Angle(EyeRotation * Vector3.forward, direction) > 45) return;
        if (!gameObject.scene.GetPhysicsScene().Raycast(EyePosition, direction.normalized, out RaycastHit hit,
            direction.magnitude + .2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || !hit.collider.transform.IsChildOf(cart.transform)) return;
        cart.TryUse(avatar, sit);
    }
    [ServerRpc(RequireOwnership = true)]
    private void CartControlServerRpc(NetworkObject target, Vector2 movement, bool fast)
    {
        if (target == null || target.gameObject.scene != gameObject.scene || NetworkLobby.Instance?.CanEditOutfit(this) != true) return;
        target.GetComponent<ShoppingCart>()?.SubmitInput(GetComponent<PlayerAvatar>(), movement, fast);
    }

    private float nextCargoAllowed;
    public void RequestCargo(CartCargo cargo, CargoAction action, int index, ClothingPickup pickup = null)
    {
        if (IsOwner && cargo != null) CargoServerRpc(cargo.GetComponent<NetworkObject>(), action, index, cargo.Revision,
            pickup != null ? pickup.GetComponent<NetworkObject>() : null);
    }
    [ServerRpc(RequireOwnership = true)]
    private void CargoServerRpc(NetworkObject target, CargoAction action, int index, int revision, NetworkObject pickup)
    {
        if (target == null || target.gameObject.scene != gameObject.scene || Time.unscaledTime < nextCargoAllowed ||
            NetworkLobby.Instance?.CanEditOutfit(this) != true) return;
        nextCargoAllowed = Time.unscaledTime + .12f;
        target.GetComponent<CartCargo>()?.Transfer(GetComponent<PlayerAvatar>(), action, index, revision,
            pickup != null ? pickup.GetComponent<ClothingPickup>() : null);
    }

    public void RequestPickup(NetworkPickup pickup, Vector3 hitPoint)
    {
        if (!IsOwner || pickup == null) return;
        RequestPickupServerRpc(pickup.NetworkObject, hitPoint);
    }

    public void RequestMonkeyGrab(NetworkMonkeyToy toy)
    { if(IsOwner && toy != null) GrabMonkeyServerRpc(toy.NetworkObject); }
    [ServerRpc(RequireOwnership = true)]
    private void GrabMonkeyServerRpc(NetworkObject target)
    { if(target != null && target.gameObject.scene == gameObject.scene) target.GetComponent<MonkeyToy>()?.TryGrab(GetComponent<PlayerAvatar>()); }
    public void RequestMonkeyAction(bool strike)
    { if(IsOwner) MonkeyActionServerRpc(strike); }
    [ServerRpc(RequireOwnership = true)]
    private void MonkeyActionServerRpc(bool strike)
    { var toy=GetComponent<PlayerMonkeyCarry>()?.Held; if(toy == null)return; if(strike)toy.Swing();else toy.Release(); }

    public void RequestMannequinGrab(NetworkThrowableMannequin mannequin, Vector3 hitPoint)
    {
        if (IsOwner && mannequin != null && (knockdown == null || !knockdown.IsDown)) GrabMannequinServerRpc(mannequin.NetworkObject, hitPoint);
    }

    [ServerRpc(RequireOwnership = true)]
    private void GrabMannequinServerRpc(NetworkObject target, Vector3 hitPoint)
    {
        if (knockdown != null && knockdown.IsDown) return;
        if (target == null || target.gameObject.scene != gameObject.scene ||
            !float.IsFinite(hitPoint.x) || !float.IsFinite(hitPoint.y) || !float.IsFinite(hitPoint.z)) return;
        var mannequin = target.GetComponent<ThrowableMannequin>();
        var collider = target.GetComponent<CapsuleCollider>();
        if (mannequin == null || collider == null || !collider.enabled ||
            Vector3.Distance(collider.ClosestPoint(hitPoint), hitPoint) > .15f) return;
        Vector3 direction = hitPoint - EyePosition;
        if (direction.magnitude > 3.8f || direction.magnitude < .01f ||
            Vector3.Angle(EyeRotation * Vector3.forward, direction) > 28) return;
        if (!gameObject.scene.GetPhysicsScene().Raycast(EyePosition, direction.normalized, out RaycastHit hit,
            direction.magnitude + .1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide) ||
            !hit.collider.transform.IsChildOf(target.transform)) return;
        mannequin.TryGrab(GetComponent<PlayerAvatar>());
    }

    public void RequestMannequinRelease(bool thrown)
    {
        if (IsOwner) ReleaseMannequinServerRpc(thrown);
    }

    [ServerRpc(RequireOwnership = true)]
    private void ReleaseMannequinServerRpc(bool thrown) => GetComponent<PlayerMannequinCarry>()?.Held?.Release(thrown);

    [ServerRpc(RequireOwnership = true)]
    private void RequestPickupServerRpc(NetworkObject target, Vector3 hitPoint)
    {
        if (knockdown != null && knockdown.IsDown) return;
        if (target == null || target.gameObject.scene != gameObject.scene ||
            !float.IsFinite(hitPoint.x) || !float.IsFinite(hitPoint.y) || !float.IsFinite(hitPoint.z)) return;
        NetworkPickup pickup = target.GetComponent<NetworkPickup>();
        if (pickup == null || Vector3.Distance(transform.position, pickup.transform.position) > 3.8f) return;
        Collider itemCollider = pickup.GetComponentInChildren<Collider>();
        if (itemCollider == null || !itemCollider.enabled || Vector3.Distance(itemCollider.ClosestPoint(hitPoint), hitPoint) > .15f) return;
        Vector3 point = hitPoint;
        Vector3 eye = EyePosition;
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
    public bool AwardClothing(ClothingDefinition clothing)
    {
        return IsServerStarted && !IsDead && NetworkLobby.Instance?.CanEditOutfit(this) == true && outfit != null && outfit.TryAdd(clothing);
    }
    public void RequestDropClothing(ClothingSlot slot)
    {
        if (IsOwner) DropClothingServerRpc(slot);
    }
    [ServerRpc(RequireOwnership = true)]
    private void DropClothingServerRpc(ClothingSlot slot)
    {
        if ((int)slot < 0 || (int)slot > 3 || NetworkLobby.Instance?.CanEditOutfit(this) != true) return;
        ClothingDropper.TryDrop(GetComponent<PlayerAvatar>(), slot, NetworkObject.NetworkManager);
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
        NetworkLobby.Instance?.OutfitChanged(this);

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
        (knockdown != null && knockdown.VisualAmount > 0 ? knockdown.EyePosition : transform.position + Vector3.up * Mathf.Lerp(1.65f, 0.95f, motor.CrouchAmount))
        + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * GrayboxFirstPersonCamera.EyeForwardOffset;
    public Quaternion EyeRotation => Quaternion.Euler(pitch, yaw, 0f);
    float IPlayerViewSource.FieldOfView => viewFieldOfView;
    float IPlayerViewSource.Aspect => viewAspect;
}

public struct PlayerPose
{
    public float Yaw, Pitch, Crouch, Speed, VerticalSpeed;
    public bool Flashlight, Grounded;
}
