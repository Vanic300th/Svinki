using System.Collections.Generic;
using UnityEngine;

/// <summary>Retargets the previous generic rig clips onto the pig proportions; standalone previews use a procedural idle.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(120)]
public sealed class PigMotion : MonoBehaviour
{
    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<Transform, Pose> rest = new Dictionary<Transform, Pose>();
    [SerializeField] private Animator legacyAnimator;
    private sealed class BoneLink { public Transform Source, Target; public Quaternion SourceRest, TargetRest; }
    private readonly List<BoneLink> links = new List<BoneLink>();
    public bool UsesLegacyClips => legacyAnimator != null && legacyAnimator.enabled && links.Count >= 13;
    public void BindLegacyAnimator(Animator value) => legacyAnimator = value;
    private GrayboxPlayerController motor;
    private NetworkPlayer network;
    private PlayerKnockdown knockdown;
    private Transform visual, lamp;
    private Transform[] lampParts;
    private int lampLayer = -1;
    private Vector3 basePosition, baseScale;
    private Transform leftFoot, rightFoot;
    private float standingFootHeight;
    private float phase, gait, torchWeight;
    private float squash, squashVelocity;

    private void Awake()
    {
        visual = GetComponent<PigAppearance>().ModelRoot;
        motor = GetComponentInParent<GrayboxPlayerController>();
        network = GetComponentInParent<NetworkPlayer>();
        knockdown = GetComponentInParent<PlayerKnockdown>();
        basePosition = visual.localPosition; baseScale = visual.localScale;
        squash = motor != null ? motor.CrouchAmount : 0;
        foreach (Transform part in visual.GetComponentsInChildren<Transform>(true))
        {
            bones[part.name] = part;
            rest[part] = new Pose(part.localPosition, part.localRotation);
        }
        if (motor != null && bones.TryGetValue("Foot_L", out leftFoot) && bones.TryGetValue("Foot_R", out rightFoot))
            standingFootHeight = Mathf.Min(motor.transform.InverseTransformPoint(leftFoot.position).y,
                motor.transform.InverseTransformPoint(rightFoot.position).y);
        if (legacyAnimator != null && motor != null)
        {
            var source = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
            foreach (Transform t in legacyAnimator.GetComponentsInChildren<Transform>(true)) source[t.name] = t;
            string[] oldNames = { "pelvis", "spine_03", "head", "upperarm_l", "lowerarm_l", "hand_l", "upperarm_r", "lowerarm_r", "hand_r", "thigh_l", "calf_l", "foot_l", "thigh_r", "calf_r", "foot_r" };
            string[] newNames = { "Hips", "Spine", "Head", "UpperArm_L", "Forearm_L", "Hand_L", "UpperArm_R", "Forearm_R", "Hand_R", "Thigh_L", "Shin_L", "Foot_L", "Thigh_R", "Shin_R", "Foot_R" };
            for (int i = 0; i < oldNames.Length; i++)
                if (source.TryGetValue(oldNames[i], out Transform from) && bones.TryGetValue(newNames[i], out Transform to))
                    links.Add(new BoneLink { Source = from, Target = to, SourceRest = Quaternion.Inverse(motor.transform.rotation) * from.rotation, TargetRest = Quaternion.Inverse(motor.transform.rotation) * to.rotation });
        }
    }

    private void ApplyLegacyPose()
    {
        Quaternion basis = motor.transform.rotation;
        foreach (BoneLink link in links)
        {
            Quaternion source = Quaternion.Inverse(basis) * link.Source.rotation;
            link.Target.rotation = basis * source * Quaternion.Inverse(link.SourceRest) * link.TargetRest;
        }
    }

    private void LateUpdate()
    {
        if (knockdown != null && knockdown.IsDown) return;
        bool local = network == null || network.IsOwner;
        float speed = motor == null ? 0 : local ? motor.MotionSpeed : network.MotionSpeed;
        bool grounded = motor == null || (local ? motor.Grounded : network.Grounded);
        float crouch = motor != null ? motor.CrouchAmount : 0;
        gait = Mathf.MoveTowards(gait, grounded ? Mathf.Clamp01(speed / 2.5f) : 0, Time.deltaTime * 6);
        phase += Time.deltaTime * Mathf.Lerp(0, 13, Mathf.Clamp01(speed / 6));
        float step = Mathf.Sin(phase) * gait;
        foreach (var pair in rest)
        { pair.Key.localPosition = pair.Value.position; pair.Key.localRotation = pair.Value.rotation; }
        visual.localPosition = basePosition + Vector3.up * (.008f * Mathf.Sin(Time.time * 2) +
            .018f * Mathf.Abs(Mathf.Cos(phase)) * gait);
        // Retarget rotations at the original scale, then squash the complete upright pose.
        visual.localScale = baseScale;
        if (UsesLegacyClips)
        {
            ApplyLegacyPose();
            // Keep the supporting foot at the model pivot while changing stance.
            if (crouch > 0 && leftFoot != null && rightFoot != null)
            {
                float footHeight = Mathf.Min(motor.transform.InverseTransformPoint(leftFoot.position).y,
                    motor.transform.InverseTransformPoint(rightFoot.position).y);
                visual.position += motor.transform.TransformVector(Vector3.up * (standingFootHeight - footHeight) * crouch);
            }
        }
        else
        {
            Swing("Thigh_L", step * 25); Swing("Thigh_R", -step * 25);
            Swing("Shin_L", Mathf.Max(0, -step) * 18); Swing("Shin_R", Mathf.Max(0, step) * 18);
            Swing("UpperArm_L", -step * 18); Swing("UpperArm_R", step * 18);
            if (!grounded) { Swing("Thigh_L", -18); Swing("Thigh_R", -18); Swing("Forearm_R", -18); }
        }
        float pitch = network != null && !local ? network.ViewPitch :
            motor != null && Camera.main != null ? Mathf.DeltaAngle(0, Camera.main.transform.eulerAngles.x) : 0;
        float headPitch = Mathf.Clamp(pitch, -35, 35) * .22f;
        if (UsesLegacyClips && bones.TryGetValue("Head", out Transform head))
            head.rotation = Quaternion.AngleAxis(headPitch, visual.right) * head.rotation;
        else Swing("Head", headPitch);
        bool lit = motor != null && (local ? Camera.main?.GetComponentInChildren<FlashlightController>()?.IsOn ?? false : network.FlashlightOn);
        torchWeight = Mathf.MoveTowards(torchWeight, lit ? 1 : 0, Time.deltaTime * 5);
        if (!UsesLegacyClips)
        {
            Swing("UpperArm_L", -step * 18 * (1 - torchWeight) - 65 * torchWeight);
            Swing("Forearm_L", -20 * torchWeight);
        }
        UpdateSquash(crouch, Time.deltaTime);
        float height = 1 - squash * (squash >= 0 ? .42f : .75f);
        visual.localScale = Vector3.Scale(baseScale, new Vector3(1 + squash * .40f, height, 1 + squash * .28f));
        if (motor != null && lamp == null && bones.TryGetValue("Hand_L", out Transform hand))
        {
            lamp = FlashlightController.CreateModel(hand, "Pig Held Flashlight");
            lamp.localPosition = Vector3.zero;
            lampParts = lamp.GetComponentsInChildren<Transform>(true);
        }
        if (lamp != null)
        {
            lamp.gameObject.SetActive(lit);
            lamp.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(pitch, motor.transform.right) * motor.transform.forward, Vector3.up);
            int layer = Mathf.Max(0, local ? LayerMask.NameToLayer("LocalPlayerMirror") : 0);
            if (lampLayer != layer)
            {
                foreach (Transform part in lampParts) part.gameObject.layer = layer;
                lampLayer = layer;
            }
        }
    }

    public void ResetPose()
    {
        foreach (var pair in rest)
            pair.Key.SetLocalPositionAndRotation(pair.Value.position, pair.Value.rotation);
        visual.localPosition = basePosition;
        visual.localScale = baseScale;
        squash = motor != null ? motor.CrouchAmount : 0;
        squashVelocity = 0;
    }

    private void UpdateSquash(float target, float deltaTime)
    {
        // A damped spring gives the pose a soft overshoot even at low frame rates.
        const float frequency = 22, damping = 8;
        float omega = Mathf.Sqrt(frequency * frequency - damping * damping);
        float offset = squash - target;
        float decay = Mathf.Exp(-damping * deltaTime);
        float sine = Mathf.Sin(omega * deltaTime), cosine = Mathf.Cos(omega * deltaTime);
        squash = target + decay * (offset * cosine + (squashVelocity + damping * offset) / omega * sine);
        squashVelocity = decay * (squashVelocity * cosine -
            (damping * squashVelocity + frequency * frequency * offset) / omega * sine);
        float bounded = Mathf.Clamp(squash, -.25f, 1.18f);
        if (bounded != squash) squashVelocity = 0;
        squash = bounded;
        if (Mathf.Abs(squash - target) < .001f && Mathf.Abs(squashVelocity) < .01f)
        { squash = target; squashVelocity = 0; }
    }

    private void Swing(string name, float degrees)
    {
        if (!bones.TryGetValue(name, out Transform bone)) return;
        Quaternion neutral = bone.parent.rotation * rest[bone].rotation;
        bone.rotation = Quaternion.AngleAxis(degrees, visual.right) * neutral;
    }
}
