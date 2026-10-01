using UnityEngine;

/// <summary>
/// «Подглядывающий» манекен: стоит в позе покоя и никуда не ходит.
/// Пока игрок на него не смотрит — поворачивает голову к игроку.
/// Как только посмотрел — голова застывает там, где была (если двигалась, доли секунды «дотягивает»).
/// Поза покоя приходит от MannequinAnimator (тот же кадр, что у обычных манекенов), голова крутится поверх неё.
/// </summary>
[RequireComponent(typeof(MannequinVisibility))]
[DefaultExecutionOrder(200)] // после MannequinVisibility (100): видимость этого кадра уже посчитана
public class MannequinHeadWatcher : MonoBehaviour
{
    // Игрок почти за спиной: в этой зоне голова не перекидывается на другую сторону
    private const float BehindZone = 30f;

    [Header("Кости")]
    [SerializeField] private Transform headBone;
    [Tooltip("Шея берёт часть поворота, чтобы при сильном повороте не перекручивало меш. Можно оставить пустым")]
    [SerializeField] private Transform neckBone;
    [Range(0f, 0.8f)] [SerializeField] private float neckShare = 0.35f;

    [Header("Куда смотреть")]
    [Tooltip("Пусто = камера игрока (глаза)")]
    [SerializeField] private Transform lookTarget;
    [Tooltip("Дальше этой дистанции не реагирует на игрока")]
    [SerializeField] private float reactDistance = 15f;
    [Tooltip("Игрок ушёл дальше React Distance — голова медленно возвращается прямо (тоже только пока не смотрят)")]
    [SerializeField] private bool returnWhenFar = false;

    [Header("Поворот")]
    [Tooltip("Насколько голова может повернуться влево/вправо от тела (градусы)")]
    [Range(10f, 170f)] [SerializeField] private float maxYaw = 100f;
    [Tooltip("Насколько вверх/вниз (градусы)")]
    [Range(0f, 80f)] [SerializeField] private float maxPitch = 35f;
    [Tooltip("Максимальная скорость поворота, градусов в секунду")]
    [SerializeField] private float turnSpeed = 120f;
    [Tooltip("Плавность разгона и остановки (сек). Меньше = резче, механичнее")]
    [SerializeField] private float smoothTime = 0.25f;

    [Header("Когда поворачивать")]
    [Tooltip("Через сколько секунд после того, как игрок отвернулся, голова начинает поворот (случайно от X до Y)")]
    [SerializeField] private Vector2 startDelay = new Vector2(0.2f, 0.8f);
    [Tooltip("Шанс, что при очередном отворачивании голова вообще повернётся")]
    [Range(0f, 1f)] [SerializeField] private float turnChance = 1f;
    [Tooltip("Сколько секунд голова ещё доворачивается, когда на неё посмотрели. 0 = замирает мгновенно")]
    [SerializeField] private float catchMotionTime = 0.12f;

    [Header("Звук")]
    [Tooltip("Скрип шеи в начале поворота. Пусто = без звука")]
    [SerializeField] private AudioClip neckCreak;
    [Range(0f, 1f)] [SerializeField] private float creakVolume = 0.8f;

    [Header("Отладка")]
    [SerializeField] private bool showStateLabel = true;

    /// <summary>Голова сейчас двигается.</summary>
    public bool IsTurning { get; private set; }

    private MannequinVisibility visibility;
    private AudioSource audioSource;

    private float yaw, pitch, yawVel, pitchVel;
    private bool wasSeen = true;
    private bool caughtMoving, turnThisTime, creakPlayed;
    private float seenSince, turnAllowedAt;

    private Quaternion headRest, neckRest;
    private int warmupFrames;
    private bool restCached;

    private void Awake()
    {
        visibility = GetComponent<MannequinVisibility>();
        if (headBone == null) headBone = FindDeep(transform, "Head");
        if (neckBone == null) neckBone = FindDeep(transform, "neck_01");
        if (headBone == null) Debug.LogWarning("[Mannequin] Watcher: не назначена кость головы", this);

        // Защита: если на этом же объекте оказался мозг живого манекена — он не должен ходить
        var brain = GetComponent<MannequinBrain>();
        if (brain != null)
        {
            brain.enabled = false;
            if (TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) agent.enabled = false;
            Debug.LogWarning("[Mannequin] Watcher: на объекте есть MannequinBrain — выключил, этот манекен только смотрит", this);
        }
    }

    private void LateUpdate()
    {
        if (headBone == null) return;

        // Ждём, пока аниматор выставит позу покоя, и запоминаем её
        if (!restCached)
        {
            if (++warmupFrames < 2) return;
            headRest = headBone.localRotation;
            if (neckBone != null) neckRest = neckBone.localRotation;
            restCached = true;
        }

        UpdateAngles();
        ApplyHead();
    }

    // ---------- Куда и когда поворачивать ----------

    private void UpdateAngles()
    {
        bool seen = visibility.IsSeen;
        float now = Time.time;

        if (seen && !wasSeen)
        {
            seenSince = now;
            caughtMoving = IsTurning; // доворачивает, только если его поймали в движении
        }
        if (!seen && wasSeen)
        {
            // Игрок отвернулся — решаем, повернётся ли голова в этот раз и когда
            turnThisTime = Random.value <= turnChance;
            turnAllowedAt = now + Random.Range(startDelay.x, startDelay.y);
            creakPlayed = false;
        }
        wasSeen = seen;

        float timeScale = 1f;
        if (seen)
        {
            float t = catchMotionTime > 0f ? (now - seenSince) / catchMotionTime : 1f;
            if (!caughtMoving || t >= 1f) { Stop(); return; }
            timeScale = 1f - t; // плавно гаснет до полной остановки
        }
        else if (!turnThisTime || now < turnAllowedAt)
        {
            Stop();
            return;
        }

        if (!GetTargetAngles(out float targetYaw, out float targetPitch)) { Stop(); return; }

        float remaining = Mathf.Abs(targetYaw - yaw) + Mathf.Abs(targetPitch - pitch);
        float dt = Time.deltaTime * timeScale;
        yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVel, smoothTime, turnSpeed, dt);
        pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVel, smoothTime, turnSpeed, dt);
        IsTurning = remaining > 0.5f;

        // Скрип — когда голова только тронулась (слышно, даже если не смотришь)
        if (!seen && !creakPlayed && remaining > 15f)
        {
            creakPlayed = true;
            PlayCreak();
        }
    }

    private bool GetTargetAngles(out float targetYaw, out float targetPitch)
    {
        targetYaw = 0f;
        targetPitch = 0f;

        Vector3 toTarget = LookPoint() - headBone.position;
        if (toTarget.sqrMagnitude > reactDistance * reactDistance)
            return returnWhenFar; // далеко: либо возвращаемся прямо (0, 0), либо стоим

        // Направление на игрока в осях тела манекена
        Vector3 local = transform.InverseTransformDirection(toTarget);
        float rawYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

        // Игрок почти за спиной на другой стороне — держим сторону, куда голова уже повёрнута, чтобы не мотало
        if (Mathf.Abs(rawYaw) > 180f - BehindZone && Mathf.Abs(yaw) > 1f && Mathf.Sign(rawYaw) != Mathf.Sign(yaw))
            rawYaw = Mathf.Sign(yaw) * maxYaw;

        targetYaw = Mathf.Clamp(rawYaw, -maxYaw, maxYaw);
        float flat = new Vector2(local.x, local.z).magnitude;
        targetPitch = Mathf.Clamp(Mathf.Atan2(local.y, flat) * Mathf.Rad2Deg, -maxPitch, maxPitch);
        return true;
    }

    private Vector3 LookPoint()
    {
        if (lookTarget != null) return lookTarget.position;
        Camera cam = visibility.ObserverCamera != null ? visibility.ObserverCamera : Camera.main;
        return cam != null ? cam.transform.position : headBone.position + transform.forward;
    }

    private void Stop()
    {
        yawVel = 0f;
        pitchVel = 0f;
        IsTurning = false;
    }

    // ---------- Применение к костям ----------

    private void ApplyHead()
    {
        // Каждый кадр от позы покоя — поворот не накапливается
        headBone.localRotation = headRest;
        if (neckBone != null) neckBone.localRotation = neckRest;
        if (Mathf.Abs(yaw) < 0.01f && Mathf.Abs(pitch) < 0.01f) return;

        float share = neckBone != null ? neckShare : 0f;
        if (neckBone != null) RotateBone(neckBone, share);
        RotateBone(headBone, 1f - share);
    }

    private void RotateBone(Transform bone, float part)
    {
        // Поворот в осях тела: yaw вокруг его «вверх», pitch вокруг его «вправо»
        Quaternion local = Quaternion.Euler(-pitch * part, yaw * part, 0f);
        Quaternion world = transform.rotation * local * Quaternion.Inverse(transform.rotation);
        bone.rotation = world * bone.rotation;
    }

    private void PlayCreak()
    {
        if (neckCreak == null) return;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 20f;
            }
        }
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(neckCreak, creakVolume);
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showStateLabel || !Application.isPlaying || headBone == null || visibility == null) return;
        string state = visibility.IsSeen ? "Seen" : IsTurning ? "Turning" : "Waiting";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.1f, $"Watcher: {state}  yaw {yaw:0}°");

        // Куда сейчас смотрит голова
        Vector3 dir = transform.rotation * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
        Gizmos.color = IsTurning ? Color.yellow : new Color(0.6f, 0.8f, 1f, 0.6f);
        Gizmos.DrawRay(headBone.position, dir * 1.5f);
    }
#endif
}
