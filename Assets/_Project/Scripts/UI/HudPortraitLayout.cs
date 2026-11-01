using UnityEngine;

/// <summary>Scale the portrait at its screen edge, independently of the actual pig model.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.RawImage))]
public sealed class HudPortraitLayout : MonoBehaviour
{
    public GameObject background;
    private RectTransform portrait;
    private Vector2 authoredSize, authoredPosition;
    private float appliedScale = -1;
    private void Awake()
    {
        portrait = (RectTransform)transform;
        authoredSize = portrait.sizeDelta; authoredPosition = portrait.anchoredPosition;
        if (background != null) background.SetActive(false);
    }
    private void LateUpdate()
    {
        float scale = HudDisplaySettings.PigScale;
        if (Mathf.Approximately(appliedScale, scale)) return;
        appliedScale = scale;
        portrait.sizeDelta = authoredSize * scale;
        portrait.anchoredPosition = authoredPosition + Vector2.Scale(portrait.pivot - portrait.anchorMin, authoredSize * (scale - 1));
    }
}
