using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Physics proxies stay outside the animated/scaled hierarchy. Only the server simulates them.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(160)]
public sealed class PlayerRagdoll : MonoBehaviour
{
    private sealed class Part
    {
        public Transform Bone;
        public Rigidbody Body;
        public Collider Collider;
    }
    [System.Serializable]
    public struct BodyShape
    {
        public string Bone;
        public Vector3 Center, Size;
    }
    [SerializeField] private BodyShape[] bodyShapes;
    private readonly List<Part> parts = new List<Part>();
    private GameObject physicsRoot;
    private NetworkPlayer network;
    private PigMotion motion;
    private Pose[] targets, recovery, animated;
    private float recoveryTime;
    private bool running, simulate, received;
    public bool IsRecovering => recoveryTime > 0;
    public Vector3 PelvisPosition => parts.Count > 0 && running ? parts[0].Body.position : transform.position;
    public int BodyCount => parts.Count;

    private void Awake()
    {
        network = GetComponent<NetworkPlayer>();
        motion = GetComponentInChildren<PigMotion>(true);
    }

    private bool Build()
    {
        if (physicsRoot != null) return parts.Count == 11;
        var appearance = GetComponentInChildren<PigAppearance>(true);
        if (appearance == null) return false;
        var bones = new Dictionary<string, Transform>();
        // Garments contain unused copies of the source rig. Only bones actually used by
        // the pig body may drive the physics fall (especially on the host).
        foreach (SkinnedMeshRenderer skin in appearance.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (Transform bone in skin.bones)
                if (bone != null && !bones.ContainsKey(bone.name)) bones.Add(bone.name, bone);
        foreach (Transform bone in appearance.ModelRoot.GetComponentsInChildren<Transform>(true))
            if (!bones.ContainsKey(bone.name)) bones.Add(bone.name, bone);
        string[] names = { "Hips", "Spine", "Head", "UpperArm_L", "Forearm_L", "UpperArm_R", "Forearm_R", "Thigh_L", "Shin_L", "Thigh_R", "Shin_R" };
        foreach (string name in names) if (!bones.ContainsKey(name)) return false;
        physicsRoot = new GameObject("Player ragdoll physics");
        SceneManager.MoveGameObjectToScene(physicsRoot, gameObject.scene);
        var bodies = new Dictionary<string, Rigidbody>();
        foreach (string name in names)
        {
            Transform bone = bones[name];
            var proxy = new GameObject(name + " physics"); proxy.layer = 2; proxy.transform.SetParent(physicsRoot.transform);
            proxy.transform.SetPositionAndRotation(bone.position, bone.rotation);
            Rigidbody body = proxy.AddComponent<Rigidbody>();
            body.isKinematic = true; body.mass = name == "Hips" ? 9 : name == "Spine" ? 12 : name == "Head" ? 5 : 2;
            body.linearDamping = .45f; body.angularDamping = .65f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 12; body.solverVelocityIterations = 6;
            body.maxAngularVelocity = 14; body.maxDepenetrationVelocity = 2;
            Collider collider;
            if (name == "Hips" || name == "Spine" || name == "Head")
            {
                var box = proxy.AddComponent<BoxCollider>();
                bool fitted = false;
                if (bodyShapes != null) foreach (BodyShape shape in bodyShapes)
                    if (shape.Bone == name) { box.center = shape.Center; box.size = shape.Size; fitted = true; break; }
                if (!fitted)
                {
                    box.center = bone.InverseTransformVector(Vector3.up * .15f);
                    Vector3 size = name == "Head" ? new Vector3(.6f, .5f, .7f) : new Vector3(.55f, .35f, .6f);
                    Vector3 right = bone.InverseTransformDirection(transform.right), up = bone.InverseTransformDirection(Vector3.up), forward = bone.InverseTransformDirection(transform.forward);
                    box.size = Abs(right) * size.x + Abs(up) * size.y + Abs(forward) * size.z;
                }
                collider = box;
            }
            else
            {
                string end = name.Replace("UpperArm", "Forearm").Replace("Forearm", "Hand");
                if (name.StartsWith("UpperArm")) end = name.Replace("UpperArm", "Forearm");
                if (name.StartsWith("Thigh")) end = name.Replace("Thigh", "Shin");
                if (name.StartsWith("Shin")) end = name.Replace("Shin", "Foot");
                Vector3 delta = bones[end].position - bone.position;
                // A child aligns its capsule with the limb, independently of the skeleton's axes.
                var shape = new GameObject("Limb collider"); shape.layer = 2; shape.transform.SetParent(proxy.transform, false);
                shape.transform.position = bone.position + delta * .5f;
                shape.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
                var capsule = shape.AddComponent<CapsuleCollider>();
                capsule.radius = name.Contains("Arm") || name.Contains("arm") ? .085f : .105f;
                capsule.height = delta.magnitude + capsule.radius * 2; collider = capsule;
            }
            collider.enabled = false;
            parts.Add(new Part { Bone = bone, Body = body, Collider = collider }); bodies[name] = body;
            if (name != "Hips")
            {
                Transform parent = bone.parent;
                while (parent != null && !bodies.ContainsKey(parent.name)) parent = parent.parent;
                if (parent == null) continue;
                var joint = proxy.AddComponent<ConfigurableJoint>(); joint.connectedBody = bodies[parent.name];
                joint.autoConfigureConnectedAnchor = false; joint.anchor = Vector3.zero;
                joint.connectedAnchor = parent.InverseTransformPoint(bone.position);
                joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
                bool limb = name != "Spine" && name != "Head";
                joint.lowAngularXLimit = new SoftJointLimit { limit = limb ? -85 : -25 }; joint.highAngularXLimit = new SoftJointLimit { limit = limb ? 85 : 25 };
                float swing = name.StartsWith("UpperArm") ? 125 : name.StartsWith("Thigh") ? 95 : limb ? 80 : 35;
                joint.angularYLimit = joint.angularZLimit = new SoftJointLimit { limit = swing };
                joint.projectionMode = JointProjectionMode.PositionAndRotation; joint.projectionDistance = .025f; joint.projectionAngle = 12;
                joint.enablePreprocessing = false; joint.enableCollision = false;
            }
        }
        targets = new Pose[parts.Count]; recovery = new Pose[parts.Count]; animated = new Pose[parts.Count];
        return true;
    }

    private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

    public void Begin(Vector3 direction)
    {
        motion?.ResetPose();
        if (!Build()) return;
        recoveryTime = 0; received = false; running = true;
        simulate = network == null || network.NetworkObject != null && network.IsServerInitialized;
        foreach (Part part in parts)
        {
            part.Body.position = part.Bone.position; part.Body.rotation = part.Bone.rotation;
            part.Body.isKinematic = !simulate; part.Collider.enabled = simulate;
            targets[parts.IndexOf(part)] = new Pose(part.Bone.position, part.Bone.rotation);
            if (simulate) foreach (PlayerAvatar avatar in PlayerRegistry.Players)
            {
                if (avatar == null) continue;
                var controller = avatar.GetComponent<CharacterController>();
                if (controller != null) Physics.IgnoreCollision(part.Collider, controller);
            }
            if (simulate)
            {
                part.Body.linearVelocity = direction * 2.4f + Vector3.up * .35f;
                bool limb = part.Bone.name != "Hips" && part.Bone.name != "Spine" && part.Bone.name != "Head";
                part.Body.angularVelocity = Vector3.Cross(Vector3.up, direction) * 3 + (limb ? Random.onUnitSphere * Random.Range(6f, 11f) : Random.insideUnitSphere);
                if (limb) part.Body.linearVelocity += Random.insideUnitSphere * .7f;
            }
        }
        if (simulate)
            for (int i = 0; i < parts.Count; i++)
                for (int j = i + 1; j < parts.Count; j++) Physics.IgnoreCollision(parts[i].Collider, parts[j].Collider);
        if (simulate) parts[1].Body.AddForceAtPosition(direction * 2f, parts[1].Body.worldCenterOfMass + Vector3.up * .25f, ForceMode.VelocityChange);
    }

    public void End()
    {
        if (!running) return;
        for (int i = 0; i < parts.Count; i++)
        {
            // Capture the displayed pose so a client's recovery does not jump to the last packet.
            recovery[i] = new Pose(parts[i].Bone.position, parts[i].Bone.rotation);
            parts[i].Body.isKinematic = true; parts[i].Collider.enabled = false;
        }
        running = false; recoveryTime = .55f;
    }

    public RagdollFrame Capture(uint id)
    {
        var frame = new RagdollFrame { FallId = id, Positions = new Vector3[parts.Count], Rotations = new Quaternion[parts.Count] };
        for (int i = 0; i < parts.Count; i++) { frame.Positions[i] = parts[i].Body.position; frame.Rotations[i] = parts[i].Body.rotation; }
        return frame;
    }

    public void Receive(RagdollFrame frame)
    {
        if (!running || simulate || frame.Positions == null || frame.Rotations == null || frame.Positions.Length != parts.Count || frame.Rotations.Length != parts.Count) return;
        for (int i = 0; i < parts.Count; i++)
        {
            targets[i] = new Pose(frame.Positions[i], frame.Rotations[i]);
            if (!received) parts[i].Body.transform.SetPositionAndRotation(targets[i].position, targets[i].rotation);
        }
        received = true;
    }

    private void LateUpdate()
    {
        if (running)
        {
            float blend = 1 - Mathf.Exp(-Time.deltaTime * 30);
            for (int i = 0; i < parts.Count; i++)
            {
                Transform proxy = parts[i].Body.transform;
                if (!simulate && received) proxy.SetPositionAndRotation(Vector3.Lerp(proxy.position, targets[i].position, blend), Quaternion.Slerp(proxy.rotation, targets[i].rotation, blend));
                parts[i].Bone.SetPositionAndRotation(proxy.position, proxy.rotation);
            }
        }
        else if (recoveryTime > 0)
        {
            recoveryTime = Mathf.Max(0, recoveryTime - Time.deltaTime);
            float blend = Mathf.SmoothStep(0, 1, 1 - recoveryTime / .55f);
            // Cache every target before moving a parent, which also moves its child bones.
            for (int i = 0; i < parts.Count; i++) animated[i] = new Pose(parts[i].Bone.position, parts[i].Bone.rotation);
            // PigMotion restored this frame's animated target before these bones are blended.
            for (int i = 0; i < parts.Count; i++)
            {
                Transform bone = parts[i].Bone;
                bone.SetPositionAndRotation(Vector3.Lerp(recovery[i].position, animated[i].position, blend), Quaternion.Slerp(recovery[i].rotation, animated[i].rotation, blend));
            }
        }
    }

    private void OnDisable()
    {
        running = false; recoveryTime = 0;
        // Physics bodies live outside the avatar; on scene unload they can be destroyed before this component.
        foreach (Part part in parts) { if (part.Body != null) part.Body.isKinematic = true; if (part.Collider != null) part.Collider.enabled = false; }
        if (motion != null) motion.ResetPose();
    }
    private void OnDestroy() { if (physicsRoot != null) Destroy(physicsRoot); }
}

public struct RagdollFrame
{
    public uint FallId;
    public Vector3[] Positions;
    public Quaternion[] Rotations;
}
