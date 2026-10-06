using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(100)]
public sealed class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    private GrayboxPlayerController motor;
    private NetworkPlayer network;
    private PigAppearance pig;
    private Transform torch;
    private Transform[] torchParts;
    private int torchLayer = -1;
    private readonly Dictionary<Transform, Pose> rest = new Dictionary<Transform, Pose>();
    private float torchWeight;
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Crouch = Animator.StringToHash("Crouch");
    private static readonly int Grounded = Animator.StringToHash("Grounded");
    private static readonly int Vertical = Animator.StringToHash("VerticalSpeed");

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        motor = GetComponent<GrayboxPlayerController>();
        network = GetComponent<NetworkPlayer>();
        pig = GetComponentInChildren<PigAppearance>(true);
        if (animator == null) return;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
            rest[bone] = new Pose(transform.InverseTransformPoint(bone.position), Quaternion.Inverse(transform.rotation) * bone.rotation);
        // Clothes follow the visible pig, while clips are evaluated on the hidden old rig.
        if (pig != null)
            foreach (Transform bone in pig.ModelRoot.GetComponentsInChildren<Transform>(true))
                rest[bone] = new Pose(transform.InverseTransformPoint(bone.position), Quaternion.Inverse(transform.rotation) * bone.rotation);
    }

    public bool RestPose(Transform bone, out Pose pose) => rest.TryGetValue(bone, out pose);

    private void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null || motor == null) return;
        bool local = network == null || network.IsOwner;
        float speed = local ? motor.MotionSpeed : network.MotionSpeed;
        bool grounded = local ? motor.Grounded : network.Grounded;
        float vertical = local ? motor.VerticalVelocity : network.VerticalSpeed;
        animator.SetFloat(Speed, speed, .1f, Time.deltaTime);
        // Pig crouch squashes the upright model in PigMotion; keep its walk/idle clips.
        animator.SetFloat(Crouch, pig != null ? 0 : motor.CrouchAmount);
        animator.SetFloat(Vertical, vertical);
        animator.SetBool(Grounded, grounded);
        bool lit = local ? Camera.main?.GetComponentInChildren<FlashlightController>()?.IsOn ?? false : network.FlashlightOn;
        torchWeight = Mathf.MoveTowards(torchWeight, lit ? 1 : 0, Time.deltaTime * 5);
        if (animator.layerCount > 1) animator.SetLayerWeight(1, torchWeight);
        if (pig != null) return;
        if (torch == null)
        {
            Transform hand = FindBone(animator, HumanBodyBones.LeftHand);
            if (hand != null)
            {
                torch = FlashlightController.CreateModel(hand, "Held Flashlight");
                torch.localPosition = new Vector3(0, .03f, .02f);
                // Align the lamp with this rig's left-hand torch pose.
                torch.localRotation = Quaternion.Euler(279.61f, 197.28f, 335.15f);
                torchParts = torch.GetComponentsInChildren<Transform>(true);
            }
        }
        if (torch != null)
        {
            int layer = local ? LayerMask.NameToLayer("LocalPlayerMirror") : 0;
            if (layer < 0) layer = 0;
            if (layer != torchLayer)
            {
                foreach (Transform t in torchParts) t.gameObject.layer = layer;
                torchLayer = layer;
            }
        }
    }

    public static Transform FindBone(Animator rig, HumanBodyBones bone)
    {
        if (rig == null) return null;
        if (rig.isHuman && rig.avatar != null && rig.avatar.isValid) return rig.GetBoneTransform(bone);
        string name = bone == HumanBodyBones.Head ? "head" : bone == HumanBodyBones.Chest ? "spine_03" :
            bone == HumanBodyBones.Hips ? "pelvis" : bone == HumanBodyBones.LeftFoot ? "foot_l" :
            bone == HumanBodyBones.RightFoot ? "foot_r" : bone == HumanBodyBones.RightHand ? "hand_r" :
            bone == HumanBodyBones.LeftHand ? "hand_l" : "";
        foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
            if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
        string capsuleBone = bone == HumanBodyBones.Chest ? "Spine" : bone == HumanBodyBones.Hips ? "Hips" : "";
        foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
            if (t.name == capsuleBone) return t;
        return null;
    }
}
