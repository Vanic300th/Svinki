using UnityEngine;

/// <summary>
/// Звуки бросаемого (витринного) манекена: подъём, скрип при переносе, бросок и удары обо что угодно.
/// Стоит на дочернем объекте "Audio" каждого манекена (префаб Assets/Audio/Prefabs/MannequinAudio).
/// Сеть не трогает: следит за тем, кто держит манекен, и за его движением — это уже синхронизируется,
/// поэтому звуки слышат все игроки. Бросок — резкий разгон после отпускания, удар — резкая смена скорости.
/// </summary>
[DisallowMultipleComponent, DefaultExecutionOrder(160)]
public sealed class MannequinAudio : MonoBehaviour
{
    [Header("Источник звука")]
    [SerializeField] private AudioSource source;

    [Header("Подъём")]
    [Tooltip("Скрип суставов, когда манекен взяли в руки.")]
    [SerializeField] private SoundEffect grab;
    [Tooltip("Сухой щелчок по корпусу в момент захвата (второй слой).")]
    [SerializeField] private SoundEffect grabClick;

    [Header("Перенос")]
    [Tooltip("Скрип пластика, пока несёшь манекен и идёшь.")]
    [SerializeField] private SoundEffect carryCreak;
    [Tooltip("Через сколько метров ходьбы с манекеном может прозвучать следующий скрип.")]
    [SerializeField, Min(0.1f)] private float creakDistance = 2.5f;
    [Tooltip("Шанс скрипа на каждом таком отрезке (0..1). Меньше — скрипит реже и неравномернее.")]
    [SerializeField, Range(0f, 1f)] private float creakChance = 0.6f;

    [Header("Бросок")]
    [SerializeField] private SoundEffect throwWhoosh;
    [Tooltip("С какой скорости (м/с) сразу после отпускания считается, что манекен бросили, а не уронили.")]
    [SerializeField, Min(0f)] private float throwSpeed = 7f;

    [Header("Удары (пол, стены, полки, свинки, другие манекены)")]
    [SerializeField] private SoundEffect impactLight;
    [SerializeField] private SoundEffect impactMedium;
    [SerializeField] private SoundEffect impactHeavy;
    [Tooltip("Низкий глухой слой, добавляется к сильному удару.")]
    [SerializeField] private SoundEffect impactHeavyLow;
    [Tooltip("Резкая смена скорости (м/с), с которой звучит лёгкий удар.")]
    [SerializeField, Min(0f)] private float lightImpact = 2f;
    [Tooltip("Смена скорости (м/с) для среднего удара.")]
    [SerializeField, Min(0f)] private float mediumImpact = 4f;
    [Tooltip("Смена скорости (м/с) для сильного удара.")]
    [SerializeField, Min(0f)] private float heavyImpact = 7f;
    [Tooltip("С какой смены скорости (м/с) удар звучит в полную силу.")]
    [SerializeField, Min(0.1f)] private float fullVolumeImpact = 10f;
    [Tooltip("Минимальная пауза между ударами, с.")]
    [SerializeField, Min(0f)] private float impactCooldown = 0.12f;

    [Header("Отладка")]
    [SerializeField] private bool logEvents;
    public int GrabCount { get; private set; }
    public int CreakCount { get; private set; }
    public int ThrowCount { get; private set; }
    public int ImpactCount { get; private set; }

    // Скорость считается по окну ~0.04 с, а не по одному кадру: по сети позиция приходит тиками,
    // и покадровая скорость дёргается даже в ровном полёте.
    private const int Samples = 64;
    private const float Window = 0.03f;
    [Tooltip("Сколько секунд после начала удара ждать, чтобы оценить его полную силу.")]
    [SerializeField, Range(0.01f, 0.15f)] private float impactMeasureTime = 0.05f;
    private readonly Vector3[] positions = new Vector3[Samples];
    private readonly float[] times = new float[Samples];
    private int sampleCount, sampleHead;

    private ThrowableMannequin mannequin;
    private Vector3 lastPosition, holderLastPosition;
    private bool wasHeld;
    private float quietUntil, nextImpactAt, throwWindowUntil, creakDistanceLeft;
    private bool impactPending;
    private float impactPeak, impactDecideAt;

    private void Awake()
    {
        mannequin = GetComponentInParent<ThrowableMannequin>();
        if (source == null) source = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        wasHeld = mannequin != null && mannequin.IsHeld;
        sampleCount = 0; throwWindowUntil = 0f;
        quietUntil = Time.time + 1.5f; // старт сцены и подключение к игре: манекены встают на свои места
        creakDistanceLeft = creakDistance * Random.Range(0.4f, 1f);
    }

    private void LateUpdate()
    {
        if (mannequin == null) return;
        float now = Time.time, dt = Time.deltaTime;
        Vector3 position = transform.position;
        Vector3 moved = position - lastPosition;
        lastPosition = position;
        bool held = mannequin.IsHeld;

        if (held != wasHeld)
        {
            if (held) OnGrabbed(); else OnReleased(now);
            wasHeld = held;
        }
        if (held) { UpdateCarry(); sampleCount = 0; impactPending = false; return; }

        // Телепорт (вернулся на место после падения под карту) — не удар.
        if (moved.sqrMagnitude > 9f || dt <= 0f) { sampleCount = 0; impactPending = false; quietUntil = Mathf.Max(quietUntil, now + 0.2f); return; }

        Push(now, position);
        if (!TryVelocity(now, 0f, out Vector3 velocity)) return;

        if (now < throwWindowUntil && velocity.magnitude >= throwSpeed)
        {
            throwWindowUntil = 0f;
            if (Play(throwWhoosh, 1f, "throw")) ThrowCount++;
        }

        if (now < quietUntil || !TryVelocity(now, Window + 0.005f, out Vector3 before)) return;
        float change = (velocity - before).magnitude;
        if (!impactPending)
        {
            if (change < lightImpact || now < nextImpactAt) return;
            // Удар начался: ещё немного копим, чтобы оценить его полную силу.
            impactPending = true; impactPeak = change; impactDecideAt = now + impactMeasureTime;
            return;
        }
        impactPeak = Mathf.Max(impactPeak, change);
        if (now >= impactDecideAt) PlayImpact(now);
    }

    private void PlayImpact(float now)
    {
        impactPending = false;
        nextImpactAt = now + impactCooldown;
        float change = impactPeak;
        float volume = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(change / fullVolumeImpact));
        bool played;
        if (change >= heavyImpact)
        {
            played = Play(impactHeavy, volume, $"impact heavy {change:0.0}");
            Play(impactHeavyLow, volume, null);
        }
        else if (change >= mediumImpact) played = Play(impactMedium, volume, $"impact medium {change:0.0}");
        else played = Play(impactLight, volume, $"impact light {change:0.0}");
        if (played) ImpactCount++;
        sampleCount = 0; // один удар — один звук, без повторов от того же отскока
    }

    private void OnGrabbed()
    {
        if (Time.time >= quietUntil)
        {
            if (Play(grab, 1f, "grab")) GrabCount++;
            Play(grabClick, 1f, null);
        }
        holderLastPosition = mannequin.Holder != null ? mannequin.Holder.transform.position : transform.position;
        creakDistanceLeft = creakDistance * Random.Range(0.4f, 1f);
    }

    private void OnReleased(float now)
    {
        sampleCount = 0;
        throwWindowUntil = now + 0.3f;
        // Отпускание само по себе резко меняет скорость — не считаем его ударом.
        quietUntil = Mathf.Max(quietUntil, now + 0.12f);
        nextImpactAt = now + 0.12f;
    }

    private void UpdateCarry()
    {
        PlayerAvatar holder = mannequin.Holder;
        if (holder == null) return;
        Vector3 p = holder.transform.position;
        Vector3 step = p - holderLastPosition;
        holderLastPosition = p;
        step.y = 0f;
        float distance = step.magnitude;
        if (distance > 3f) return;
        creakDistanceLeft -= distance;
        if (creakDistanceLeft > 0f) return;
        creakDistanceLeft += creakDistance * Random.Range(0.7f, 1.3f);
        if (Random.value <= creakChance && Play(carryCreak, 1f, "carry creak")) CreakCount++;
    }

    private void Push(float time, Vector3 position)
    {
        times[sampleHead] = time; positions[sampleHead] = position;
        sampleHead = (sampleHead + 1) % Samples;
        sampleCount = Mathf.Min(sampleCount + 1, Samples);
    }

    /// <summary>Средняя скорость за окно Window, закончившееся age секунд назад.</summary>
    private bool TryVelocity(float now, float age, out Vector3 velocity)
    {
        velocity = Vector3.zero;
        if (!TrySample(now - age, out Vector3 end, out float endTime)) return false;
        if (!TrySample(endTime - Window, out Vector3 start, out float startTime)) return false;
        float span = endTime - startTime;
        if (span < Window * 0.5f) return false;
        velocity = (end - start) / span;
        return true;
    }

    /// <summary>Последний сохранённый кадр не позже момента time.</summary>
    private bool TrySample(float time, out Vector3 position, out float sampleTime)
    {
        for (int i = 1; i <= sampleCount; i++)
        {
            int index = (sampleHead - i + Samples) % Samples;
            if (times[index] <= time + 0.0001f) { position = positions[index]; sampleTime = times[index]; return true; }
        }
        position = Vector3.zero; sampleTime = 0f;
        return false;
    }

    private bool Play(SoundEffect sound, float volumeScale, string label)
    {
        if (sound == null) return false;
        bool played = sound.Play(source, volumeScale);
        if (played && logEvents && label != null) Debug.Log($"[MannequinAudio] {label}: {sound.name} ({mannequin.name})", this);
        return played;
    }
}
