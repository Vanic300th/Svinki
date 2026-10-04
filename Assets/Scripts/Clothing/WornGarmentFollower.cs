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

    public void Bind(Transform source)
    {
        bone = source;
        if (bone == null) return;
        initialBonePosition = bone.position;
        initialBoneRotation = bone.rotation;
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (bone == null) return;
        Quaternion delta = bone.rotation * Quaternion.Inverse(initialBoneRotation);
        transform.SetPositionAndRotation(
            bone.position + delta * (initialPosition - initialBonePosition),
            delta * initialRotation);
    }
}
