using UnityEngine;

/// <summary>
/// Звуки свинки: шаги, прыжок, приземление, падение, подъём, подбор и сброс одежды, фонарик, фотокамера.
/// Стоит на объекте "Audio" внутри префаба игрока (OfflinePlayer / NetworkPlayer), источники звука — его дочерние объекты.
/// Работает и для своей свинки, и для чужих: скорость, «на земле» и падения берутся из состояния,
/// которое уже синхронизируется по сети, поэтому каждый игрок слышит шаги и падения остальных.
/// </summary>
[DisallowMultipleComponent, DefaultExecutionOrder(150)]
public sealed class PigAudio : MonoBehaviour
{
    [Header("Источники звука (дочерние объекты)")]
    [Tooltip("AudioSource у ног: шаги, прыжок, приземление.")]
    [SerializeField] private AudioSource footsteps;
    [Tooltip("AudioSource на теле: падение, удар, подъём.")]
    [SerializeField] private AudioSource body;

    [Header("Шаги")]
    [SerializeField] private SoundEffect walkStep;
    [SerializeField] private SoundEffect sprintStep;
    [SerializeField] private SoundEffect crouchStep;
    [Tooltip("Длина шага при ходьбе, м. Больше — шаги реже.")]
    [SerializeField, Min(0.1f)] private float walkStride = 1.7f;
    [Tooltip("Длина шага при беге, м.")]
    [SerializeField, Min(0.1f)] private float sprintStride = 2.4f;
    [Tooltip("Длина шага в приседе, м.")]
    [SerializeField, Min(0.1f)] private float crouchStride = 1.3f;
    [Tooltip("Скорость обычной ходьбы, м/с (как moveSpeed в GrayboxPlayerController).")]
    [SerializeField, Min(0.1f)] private float walkSpeed = 7f;
    [Tooltip("Скорость бега, м/с (как sprintSpeed в GrayboxPlayerController). Беговые шаги — с середины между ходьбой и бегом.")]
    [SerializeField, Min(0.1f)] private float runSpeed = 10f;
    [Tooltip("Медленнее этой скорости (м/с) шагов нет.")]
    [SerializeField, Min(0f)] private float minStepSpeed = 0.6f;
    [Tooltip("С какой глубины приседа (0..1) шаги становятся тихими.")]
    [SerializeField, Range(0f, 1f)] private float crouchThreshold = 0.5f;

    [Header("Прыжок и приземление")]
    [Tooltip("Отрыв от земли при прыжке. Можно оставить пустым.")]
    [SerializeField] private SoundEffect jump;
    [Tooltip("Ноги касаются пола.")]
    [SerializeField] private SoundEffect landStep;
    [Tooltip("Глухой низкий удар при приземлении, громче после долгого полёта.")]
    [SerializeField] private SoundEffect landThud;
    [Tooltip("Сколько секунд надо пробыть в воздухе, чтобы прозвучало приземление.")]
    [SerializeField, Min(0f)] private float minAirTime = 0.25f;
    [Tooltip("После скольких секунд полёта удар звучит в полную силу.")]
    [SerializeField, Min(0.01f)] private float fullThudAirTime = 0.9f;

    [Header("Падение (сбили с ног)")]
    [Tooltip("Удар в момент падения.")]
    [SerializeField] private SoundEffect fallHit;
    [Tooltip("Голос свинки при падении (визг). Пока пусто.")]
    [SerializeField] private SoundEffect fallVoice;
    [Tooltip("Тело ударяется о пол.")]
    [SerializeField] private SoundEffect fallThud;
    [Tooltip("Через сколько секунд после удара тело касается пола.")]
    [SerializeField, Min(0f)] private float fallThudDelay = 0.35f;
    [Tooltip("Свинка поднимается.")]
    [SerializeField] private SoundEffect getUp;

    [Header("Одежда")]
    [Tooltip("Вещь подобрали и надели.")]
    [SerializeField] private SoundEffect clothingPickup;
    [Tooltip("Вещь сняли и выбросили на пол (или её украли, или убрали в тележку).")]
    [SerializeField] private SoundEffect clothingDrop;
    [Tooltip("Если за один кадр изменилось больше вещей (новый раунд, выбывание, подключение к игре), звук не играет.")]
    [SerializeField, Min(1)] private int maxClothingChangesForSound = 2;

    [Header("Фонарик")]
    [SerializeField] private SoundEffect flashlightOn;
    [SerializeField] private SoundEffect flashlightOff;

    [Header("Фотокамера")]
    [Tooltip("Щелчок затвора при снимке. Слышат все рядом.")]
    [SerializeField] private SoundEffect cameraShutter;
    [Tooltip("Камеру достали (K). Можно оставить пустым.")]
    [SerializeField] private SoundEffect cameraEquip;
    [Tooltip("Камеру убрали. Можно оставить пустым.")]
    [SerializeField] private SoundEffect cameraStow;

    [Header("Своя свинка")]
    [Tooltip("Громкость своих шагов и приземлений относительно чужих (свои слышно всегда, не надо громко).")]
    [SerializeField, Range(0f, 1f)] private float ownVolume = 0.7f;

    [Header("Отладка")]
    [SerializeField] private bool logEvents;
    /// <summary>Сколько шагов прозвучало (для проверки).</summary>
    public int StepCount { get; private set; }
    /// <summary>Сколько падений прозвучало (для проверки).</summary>
    public int FallCount { get; private set; }

    private GrayboxPlayerController motor;
    private NetworkPlayer network;
    private PlayerKnockdown knockdown;
    private PlayerAvatar avatar;
    private Transform root;
    private Vector3 lastPosition;
    private float distanceSinceStep, airTime, thudAt = -1f, quietUntil;
    private bool wasGrounded = true, wasDown;
    private uint lastFallId;
    private PlayerOutfit outfit;
    private static readonly int ClothingSlots = System.Enum.GetValues(typeof(ClothingSlot)).Length;
    private readonly ClothingDefinition[] worn = new ClothingDefinition[ClothingSlots];
    private float clothingQuietUntil;
    private FlashlightController localFlashlight;
    private bool flashlightWasOn, flashlightKnown;
    private PlayerPhotoCamera photoCamera;
    private float cameraCooldown, nextCameraSearch;
    private bool cameraEquipped;

    private bool IsLocal => network == null || network.IsOwner;
    private float Speed => IsLocal ? (motor != null ? motor.MotionSpeed : 0f) : network.MotionSpeed;
    private bool Grounded => IsLocal ? (motor == null || motor.Grounded) : network.Grounded;
    private float OwnScale => IsLocal ? ownVolume : 1f;

    private void Awake()
    {
        motor = GetComponentInParent<GrayboxPlayerController>();
        network = GetComponentInParent<NetworkPlayer>();
        knockdown = GetComponentInParent<PlayerKnockdown>();
        root = motor != null ? motor.transform : transform.parent != null ? transform.parent : transform;
    }

    private void OnEnable()
    {
        lastPosition = root.position;
        distanceSinceStep = 0f; airTime = 0f; thudAt = -1f;
        wasGrounded = true;
        wasDown = knockdown != null && knockdown.IsDown;
        lastFallId = knockdown != null ? knockdown.FallId : 0u;
        quietUntil = Time.time + 0.5f; // появление игрока — не прыжок и не приземление
        clothingQuietUntil = Time.time + 2f; // стартовый комплект и синхронизация при подключении — без звука
        outfit = null;
        flashlightKnown = false;
        photoCamera = null; nextCameraSearch = 0f;
    }

    private void LateUpdate()
    {
        if (avatar == null) avatar = GetComponentInParent<PlayerAvatar>();
        float dt = Time.deltaTime;
        Vector3 position = root.position;
        Vector3 moved = position - lastPosition;
        lastPosition = position;

        UpdateFalls();
        UpdateClothing();
        UpdateFlashlight();
        UpdateCamera();
        if (thudAt >= 0f && Time.time >= thudAt) { thudAt = -1f; Play(fallThud, body, 1f, "fall thud"); }

        // Телепорт (возрождение, смена раунда): не считаем это шагами.
        if (moved.sqrMagnitude > 9f || Time.time < quietUntil) { distanceSinceStep = 0f; airTime = 0f; wasGrounded = Grounded; return; }

        bool canMove = CanMakeFootsteps();
        bool grounded = Grounded;

        if (!grounded)
        {
            if (wasGrounded && canMove && dt > 0f && moved.y / dt > 1.5f) Play(jump, footsteps, OwnScale, "jump");
            airTime += dt;
        }
        else
        {
            if (!wasGrounded && canMove && airTime >= minAirTime)
            {
                Play(landStep, footsteps, OwnScale, "land");
                Play(landThud, body, OwnScale * Mathf.Clamp01(airTime / fullThudAirTime), "land thud");
                distanceSinceStep = 0f;
            }
            airTime = 0f;
        }
        wasGrounded = grounded;

        if (!grounded || !canMove) return;
        float speed = Speed;
        if (speed < minStepSpeed)
        {
            // Первый шаг после остановки звучит быстро, а не через целый шаг.
            distanceSinceStep = Mathf.Max(distanceSinceStep, walkStride * 0.6f);
            return;
        }

        bool crouched = avatar != null && avatar.CrouchAmount >= crouchThreshold;
        float run = Mathf.InverseLerp(walkSpeed, runSpeed, speed);
        float stride = crouched ? crouchStride : Mathf.Lerp(walkStride, sprintStride, run);
        distanceSinceStep += new Vector2(moved.x, moved.z).magnitude;
        if (distanceSinceStep < stride) return;
        distanceSinceStep = Mathf.Repeat(distanceSinceStep - stride, stride);

        SoundEffect step = crouched && crouchStep != null ? crouchStep : run >= 0.5f && sprintStep != null ? sprintStep : walkStep;
        if (Play(step, footsteps, OwnScale, "step")) StepCount++;
    }

    private void UpdateFalls()
    {
        if (knockdown == null) return;
        bool down = knockdown.IsDown;
        if (knockdown.FallId != lastFallId)
        {
            lastFallId = knockdown.FallId;
            if (down)
            {
                Play(fallHit, body, 1f, "fall hit");
                Play(fallVoice, body, 1f, "fall voice");
                thudAt = Time.time + fallThudDelay;
                FallCount++;
            }
        }
        if (wasDown && !down) Play(getUp, body, 1f, "get up");
        wasDown = down;
    }

    // По сети список одежды у чужих свинок приходит как «очистить и заполнить заново» в одном кадре,
    // поэтому сравниваем итог кадра с прошлым, а не ловим отдельные события.
    private void UpdateClothing()
    {
        if (outfit == null)
        {
            outfit = GetComponentInParent<PlayerOutfit>();
            if (outfit == null) return;
            for (int i = 0; i < ClothingSlots; i++) worn[i] = outfit.Get((ClothingSlot)i);
            return;
        }
        int gained = 0, lost = 0;
        for (int i = 0; i < ClothingSlots; i++)
        {
            ClothingDefinition now = outfit.Get((ClothingSlot)i);
            if (now == worn[i]) continue;
            if (worn[i] != null) lost++;
            if (now != null) gained++;
            worn[i] = now;
        }
        if (gained + lost == 0 || Time.time < clothingQuietUntil || gained + lost > maxClothingChangesForSound) return;
        if (gained > 0) Play(clothingPickup, body, 1f, "clothing pickup");
        else Play(clothingDrop, body, 1f, "clothing drop");
    }

    // Своя свинка — фонарик на камере, чужая — состояние из сети (то же, что использует PigMotion).
    private void UpdateFlashlight()
    {
        bool on;
        if (IsLocal)
        {
            if (localFlashlight == null && Camera.main != null) localFlashlight = Camera.main.GetComponentInChildren<FlashlightController>();
            if (localFlashlight == null) return;
            on = localFlashlight.IsOn;
        }
        else on = network.FlashlightOn;
        if (!flashlightKnown) { flashlightKnown = true; flashlightWasOn = on; return; }
        if (on == flashlightWasOn) return;
        flashlightWasOn = on;
        Play(on ? flashlightOn : flashlightOff, body, 1f, on ? "flashlight on" : "flashlight off");
    }

    // Снимок виден по перезарядке: ShowFlash приходит всем игрокам по сети и заново заводит её на секунду.
    private void UpdateCamera()
    {
        if (photoCamera == null)
        {
            if (Time.time < nextCameraSearch) return;
            nextCameraSearch = Time.time + 1f;
            photoCamera = GetComponentInParent<PlayerPhotoCamera>();
            if (photoCamera == null) return;
            cameraCooldown = photoCamera.CooldownRemaining;
            cameraEquipped = photoCamera.Equipped;
            return;
        }
        float cooldown = photoCamera.CooldownRemaining;
        if (cooldown > cameraCooldown + 0.5f) Play(cameraShutter, body, 1f, "camera shutter");
        cameraCooldown = cooldown;
        bool equipped = photoCamera.Equipped;
        if (equipped == cameraEquipped) return;
        cameraEquipped = equipped;
        Play(equipped ? cameraEquip : cameraStow, body, 1f, equipped ? "camera equip" : "camera stow");
    }

    private bool CanMakeFootsteps()
    {
        if (knockdown != null && knockdown.IsDown) return false;
        if (avatar == null) return true;
        if (!avatar.IsAlive) return false;
        ShoppingCart cart = ShoppingCart.For(avatar);
        return cart == null || cart.Rider != avatar;
    }

    private bool Play(SoundEffect sound, AudioSource source, float volumeScale, string label)
    {
        if (sound == null) return false;
        bool played = sound.Play(source, volumeScale);
        if (played && logEvents) Debug.Log($"[PigAudio] {label}: {sound.name} ({root.name})", this);
        return played;
    }
}
