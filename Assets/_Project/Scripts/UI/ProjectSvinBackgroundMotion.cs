using UnityEngine;

/// <summary>A slow horizontal drift of the artwork; the interface stays still.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
public sealed class ProjectSvinBackgroundMotion : MonoBehaviour
{
    private RectTransform rect;
    private Vector2 origin;
    private Vector3 initialScale;
    public float CurrentOffset { get; private set; }
    private void Awake() { rect = (RectTransform)transform; origin = rect.anchoredPosition; initialScale = rect.localScale; }
    private void LateUpdate()
    {
        var theme = ProjectSvinTheme.Current;
        if (theme == null) return;
        CurrentOffset = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / Mathf.Max(4, theme.backgroundPeriod)) * theme.backgroundTravel;
        rect.anchoredPosition = origin + Vector2.right * CurrentOffset;
        rect.localScale = initialScale * Mathf.Max(theme.backgroundZoom, 1 + theme.backgroundTravel * 2 / Mathf.Max(1, rect.rect.width));
    }
}
