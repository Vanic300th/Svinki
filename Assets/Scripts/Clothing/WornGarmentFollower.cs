using UnityEngine;

/// <summary>Сохраняет посадку вещи на модели и повторяет движение выбранной кости.</summary>
[DefaultExecutionOrder(1100)]
public sealed class WornGarmentFollower : MonoBehaviour
{
    private Transform bone;
    private Vector3 initialBonePosition;
    private Quaternion initialBoneRotation;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private PlayerAnimation owner;
    private Pose restPose;
    private Vector3 fittedPosition;
    private Quaternion fittedRotation;
    private bool hasRestPose;

    public void Bind(Transform source)
    {
        bone = source;
        if (bone == null) return;
        initialBonePosition = bone.position;
        initialBoneRotation = bone.rotation;
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        owner = GetComponentInParent<PlayerAnimation>();
        hasRestPose = owner != null && owner.RestPose(bone, out restPose);
        if (hasRestPose)
        {
            fittedPosition = owner.transform.InverseTransformPoint(transform.position);
            fittedRotation = Quaternion.Inverse(owner.transform.rotation) * transform.rotation;
        }
    }

    private void LateUpdate()
    {
        if (bone == null) return;
        if (hasRestPose && owner != null)
        {
            Transform root = owner.transform;
            Quaternion motion = bone.rotation * Quaternion.Inverse(root.rotation * restPose.rotation);
            transform.SetPositionAndRotation(bone.position + motion *
                (root.TransformPoint(fittedPosition) - root.TransformPoint(restPose.position)),
                motion * root.rotation * fittedRotation);
            return;
        }
        Quaternion delta = bone.rotation * Quaternion.Inverse(initialBoneRotation);
        transform.SetPositionAndRotation(
            bone.position + delta * (initialPosition - initialBonePosition),
            delta * initialRotation);
    }
}
