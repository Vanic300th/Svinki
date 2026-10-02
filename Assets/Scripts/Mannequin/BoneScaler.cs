using UnityEngine;

/// <summary>
/// Меняет пропорции модели: каждый кадр после анимации выставляет выбранным костям свой масштаб.
/// Масштаб кости тянет за собой всё, что ниже по иерархии: голова ×1.25 — крупная голова,
/// бедро ×0 — нога «схлопывается». Множитель считается от исходного масштаба кости в префабе.
/// </summary>
[DefaultExecutionOrder(50)] // после анимации и MannequinAnimator, до проверки видимости (100)
public sealed class BoneScaler : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public Transform bone;
        public Vector3 scale;
    }

    [SerializeField] private Entry[] bones = new Entry[0];

    private Vector3[] baseScales;

    private void Awake()
    {
        baseScales = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            baseScales[i] = bones[i].bone != null ? bones[i].bone.localScale : Vector3.one;
        Apply();
    }

    private void LateUpdate() => Apply();

    private void Apply()
    {
        for (int i = 0; i < bones.Length; i++)
            if (bones[i].bone != null)
                bones[i].bone.localScale = Vector3.Scale(baseScales[i], bones[i].scale);
    }
}
