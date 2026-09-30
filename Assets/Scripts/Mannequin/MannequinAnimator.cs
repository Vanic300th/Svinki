using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Только визуал манекена: бег/ходьба, «дотягивание» позы и замирание, атака, поворот головы.
/// Решений не принимает — команды приходят из MannequinBrain.
/// </summary>
public class MannequinAnimator : MonoBehaviour
{
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private const string LocomotionState = "Locomotion";

    [SerializeField] private Animator animator;

    [Header("Замирание")]
    [Tooltip("Позы, в которые манекен может «дотянуться» перед замиранием (имена клипов)")]
    [SerializeField] private string[] freezePoses =
    {
        "Punch_Jab", "Punch_Cross", "Sword_Attack", "Spell_Simple_Shoot",
        "Interact", "PickUp_Table", "Hit_Head", "Hit_Chest"
    };
    [Tooltip("Шанс сменить позу при замирании. Иначе замирает прямо в текущем шаге")]
    [Range(0f, 1f)] [SerializeField] private float poseSwapChance = 0.6f;
    [Tooltip("Из какой части клипа брать случайный кадр позы")]
    [SerializeField] private Vector2 poseTimeRange = new Vector2(0.25f, 0.75f);
    [Tooltip("При старте сцены манекен стоит в случайной позе")]
    [SerializeField] private bool randomPoseOnStart = true;

    [Header("Атака")]
    [SerializeField] private string attackClip = "Punch_Cross";

    [Header("Голова")]
    [SerializeField] private bool headLookAtPlayer = true;
    [SerializeField] private Transform headBone;
    [Range(0f, 1f)] [SerializeField] private float headLookWeight = 0.85f;
    [SerializeField] private float headMaxAngle = 75f;

    private readonly Dictionary<string, float> clipLengths = new();
    private bool settling;
    private float settleTimer, settleDuration, settleHoldPart;
    private float headWeight, headWeightTarget;
    private Vector3 headLookTarget;
    private Transform headTarget;
    private Vector3 headLocalForward;

    public float AttackLength => GetLength(attackClip, 1f);

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // кости нужны для проверки видимости даже за кадром

        if (animator.runtimeAnimatorController != null)
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                clipLengths[ShortName(clip.name)] = clip.length;

        if (headBone != null)
            headLocalForward = Quaternion.Inverse(headBone.rotation) * transform.forward;
    }

    private void Start()
    {
        if (randomPoseOnStart && freezePoses.Length > 0)
        {
            string pose = freezePoses[Random.Range(0, freezePoses.Length)];
            animator.Play(pose, 0, Random.Range(poseTimeRange.x, poseTimeRange.y));
            animator.speed = 0f;
        }
    }

    // ---------- Команды от мозга ----------

    public void SetMoveSpeed(float metersPerSecond)
    {
        animator.SetFloat(SpeedParam, metersPerSecond, 0.1f, Time.deltaTime);
    }

    public void StartMoving()
    {
        settling = false;
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(LocomotionState, 0.15f, 0);
        headWeightTarget = 0f;
    }

    /// <summary>Начать «дотягивание» позы. keepCurrentPose — замереть в том, что сейчас играет (например, в ударе).</summary>
    public void BeginSettle(float duration, Transform lookAt)
    {
        BeginSettle(duration, lookAt, false);
    }

    public void BeginSettle(float duration, Transform lookAt, bool keepCurrentPose)
    {
        settling = true;
        settleTimer = 0f;
        settleDuration = Mathf.Max(0.01f, duration);
        settleHoldPart = 0f;
        animator.speed = 1f;

        if (!keepCurrentPose && freezePoses.Length > 0 && Random.value < poseSwapChance)
        {
            string pose = freezePoses[Random.Range(0, freezePoses.Length)];
            float t = Random.Range(poseTimeRange.x, poseTimeRange.y) * GetLength(pose, 1f);
            // Первые 60% времени — быстрый переход в позу, потом плавная остановка
            settleHoldPart = 0.6f;
            animator.CrossFadeInFixedTime(pose, settleDuration * settleHoldPart, 0, t);
        }

        headTarget = lookAt;
        headWeightTarget = headLookAtPlayer && lookAt != null ? headLookWeight : 0f;
    }

    public void Freeze()
    {
        settling = false;
        animator.speed = 0f;
        // Запоминаем, куда смотрела голова: пока на манекен смотрят, голова больше не двигается
        if (headTarget != null) headLookTarget = headTarget.position;
        headTarget = null;
        headWeight = headWeightTarget;
    }

    public void PlayAttack()
    {
        settling = false;
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(attackClip, 0.1f, 0);
        headWeightTarget = 0f;
    }

    // ---------- Внутреннее ----------

    private void Update()
    {
        if (!settling) return;
        settleTimer += Time.deltaTime;
        float t = Mathf.Clamp01(settleTimer / settleDuration);

        // Скорость анимации: держим 1, пока идёт переход в позу, потом плавно гасим до 0
        float slowT = Mathf.InverseLerp(settleHoldPart, 1f, t);
        animator.speed = 1f - slowT * slowT * (3f - 2f * slowT);

        // Голова доворачивается ближе к концу — самый жуткий момент
        headWeight = headWeightTarget * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, t));
    }

    private void LateUpdate()
    {
        // Во время движения плавно отпускаем голову обратно анимации
        if (!settling && headWeightTarget == 0f)
            headWeight = Mathf.MoveTowards(headWeight, 0f, Time.deltaTime * 4f);

        if (headBone != null && headWeight > 0.001f) ApplyHeadLook();
    }

    private void ApplyHeadLook()
    {
        if (headTarget != null) headLookTarget = headTarget.position;

        Vector3 currentForward = headBone.rotation * headLocalForward;
        Vector3 desired = (headLookTarget - headBone.position).normalized;
        if (desired == Vector3.zero) return;
        desired = Vector3.RotateTowards(currentForward, desired, headMaxAngle * Mathf.Deg2Rad, 0f);
        Quaternion delta = Quaternion.FromToRotation(currentForward, desired);
        headBone.rotation = Quaternion.Slerp(Quaternion.identity, delta, headWeight) * headBone.rotation;
    }

    private static string ShortName(string clipName)
    {
        int bar = clipName.LastIndexOf('|');
        return bar >= 0 ? clipName.Substring(bar + 1) : clipName;
    }

    private float GetLength(string clipName, float fallback)
    {
        return clipLengths.TryGetValue(clipName, out float length) ? length : fallback;
    }
}
