using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Shared theme for existing dynamically created screens, including photo and cargo windows.</summary>
public sealed class ProjectSvinSkin : MonoBehaviour
{
    public static bool InGame
    {
        get
        {
            var lobby = NetworkLobby.Instance;
            return lobby != null && (lobby.Offline || lobby.InSession);
        }
    }
    private float nextScan;
    private readonly Dictionary<UnityEngine.UI.Button, Vector3> buttonScales = new Dictionary<UnityEngine.UI.Button, Vector3>();
    private readonly Dictionary<TMP_Text, float> buttonTextSizes = new Dictionary<TMP_Text, float>();
    // Unity restores these baselines when scripts reload during Play Mode.
    [System.Serializable] private sealed class ButtonMetric { public UnityEngine.UI.Button button; public Vector3 scale; }
    [System.Serializable] private sealed class TextMetric { public TMP_Text label; public float size; }
    [SerializeField, HideInInspector] private List<ButtonMetric> buttonMetrics = new List<ButtonMetric>();
    [SerializeField, HideInInspector] private List<TextMetric> textMetrics = new List<TextMetric>();
    private int buttonRevision = -1;
    private void OnEnable()
    {
        foreach (var item in buttonMetrics) if (item.button != null) buttonScales[item.button] = item.scale;
        foreach (var item in textMetrics) if (item.label != null) buttonTextSizes[item.label] = item.size;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartSkin()
    {
        var root = new GameObject("Project S.V.I.N. UI Theme");
        DontDestroyOnLoad(root); root.AddComponent<ProjectSvinSkin>();
    }
    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .65f;
        foreach (var graphic in FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!graphic.gameObject.scene.IsValid() || graphic.GetComponent<ProjectSvinElement>() != null) continue;
            if (graphic is UnityEngine.UI.RawImage || graphic.color.a < .01f && !(graphic is TMP_Text)) continue;
            var color = graphic.color;
            bool oldBlue = color.b > color.r * 1.18f && color.b > .025f && color.g < color.b * 1.5f;
            if (graphic is TMP_Text text)
            {
                var originalFont = text.font;
                var typography = Bind(text, oldBlue ? ProjectSvinRole.MutedText : ProjectSvinRole.Text);
                typography.opacity = color.a;
                typography.Apply();
                // The crosshair is a graphic symbol, so retain its authored glyph metrics.
                if (text.name == "Crosshair") { typography.useThemeFont = false; text.font = originalFont; }
                // Preserve semantic colours while applying the shared font roles to every UI label.
                if (!oldBlue && !(color.r > .9f && color.g > .9f && color.b > .9f))
                { typography.useThemeColor = false; text.color = color; }
                continue;
            }
            if (!(graphic is UnityEngine.UI.Image image) || !oldBlue) continue;
            if (image.GetComponent<UnityEngine.UI.Mask>() != null) continue;
            var button = image.GetComponent<UnityEngine.UI.Button>();
            var role = button != null ? ProjectSvinRole.Button : color.maxColorComponent < .17f ? ProjectSvinRole.Panel : ProjectSvinRole.Track;
            var element = Bind(image, role); element.opacity = color.a;
            var rect = image.rectTransform.rect;
            bool surface = rect.width > 75 && rect.height > 24 && rect.width < rect.height * 16;
            element.useThemeSprite = surface;
            if (surface) Outline(image);
            element.Apply();
        }
        foreach (var text in FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!text.gameObject.scene.IsValid() || text.GetComponent<ProjectSvinElement>() != null) continue;
            var element = text.gameObject.AddComponent<ProjectSvinElement>();
            element.theme = ProjectSvinTheme.Current;
            element.useThemeColor = false;
            element.Apply();
        }
        foreach (var slider in FindObjectsByType<UnityEngine.UI.Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (slider.handleRect == null) continue;
            var existing = slider.handleRect.GetComponent<ProjectSvinElement>();
            if (existing != null && existing.role == ProjectSvinRole.Snout) continue;
            var image = slider.handleRect.GetComponent<UnityEngine.UI.Image>();
            if (image == null) continue;
            var area = slider.handleRect.parent as RectTransform;
            if (area != null)
            {
                float height = area.rect.height;
                area.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 34);
                area.anchoredPosition += new Vector2(0, (34 - height) * .5f);
            }
            slider.handleRect.sizeDelta = new Vector2(46, 0);
            slider.handleRect.anchorMin = new Vector2(slider.handleRect.anchorMin.x, 0);
            slider.handleRect.anchorMax = new Vector2(slider.handleRect.anchorMax.x, 1);
            image.preserveAspect = true;
            var snout = Bind(image, ProjectSvinRole.Snout); snout.useThemeSprite = true; snout.Apply();
            var hitArea = slider.GetComponent<UnityEngine.UI.Image>();
            if (hitArea != null)
            {
                var binding = hitArea.GetComponent<ProjectSvinElement>();
                if (binding != null) { binding.useThemeColor = false; binding.useThemeSprite = false; }
                hitArea.color = Color.clear;
            }
            var visual = new GameObject("Theme Slider Track", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            visual.transform.SetParent(slider.transform, false); visual.transform.SetAsFirstSibling();
            var track = (RectTransform)visual.transform;
            track.anchorMin = new Vector2(0, .5f); track.anchorMax = new Vector2(1, .5f); track.sizeDelta = new Vector2(0, 8);
            var trackImage = visual.GetComponent<UnityEngine.UI.Image>(); trackImage.raycastTarget = false; Bind(trackImage, ProjectSvinRole.Track);
            var fillObject = new GameObject("Theme Slider Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fillObject.transform.SetParent(track, false); var fill = (RectTransform)fillObject.transform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.sizeDelta = Vector2.zero;
            var fillImage = fillObject.GetComponent<UnityEngine.UI.Image>(); fillImage.raycastTarget = false; Bind(fillImage, ProjectSvinRole.Accent);
            slider.fillRect = fill;
        }
        FitButtons();
    }
    private void FitButtons()
    {
        var theme = ProjectSvinTheme.Current;
        if (theme == null) return;
        buttonMetrics.RemoveAll(item => item.button == null);
        textMetrics.RemoveAll(item => item.label == null);
        foreach (var dead in buttonScales.Keys.Where(b => b == null).ToArray()) buttonScales.Remove(dead);
        foreach (var dead in buttonTextSizes.Keys.Where(t => t == null).ToArray()) buttonTextSizes.Remove(dead);
        foreach (var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // The text-only menu owns its authored size and hover animation.
            if (button.GetComponent<ProjectSvinMenuHover>() != null) continue;
            // Templates stay at their authored size so new clones receive the scale exactly once.
            if (!button.gameObject.scene.IsValid() || button.name.IndexOf("template", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            bool added = !buttonScales.TryGetValue(button, out var originalScale);
            if (added) { originalScale = button.transform.localScale; buttonScales.Add(button, originalScale); buttonMetrics.Add(new ButtonMetric { button = button, scale = originalScale }); }
            if (added || buttonRevision != theme.revision)
                button.transform.localScale = Vector3.Scale(originalScale, new Vector3(theme.buttonScale, theme.buttonScale, 1));
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.GetComponentInParent<UnityEngine.UI.Button>(true) != button) continue;
                bool newLabel = !buttonTextSizes.TryGetValue(label, out float originalSize);
                if (newLabel)
                {
                    originalSize = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
                    buttonTextSizes.Add(label, originalSize);
                    textMetrics.Add(new TextMetric { label = label, size = originalSize });
                }
                if (!newLabel && buttonRevision == theme.revision) continue;
                float size = originalSize * theme.buttonTextScale;
                label.fontSize = label.fontSizeMax = size;
                label.fontSizeMin = size * .7f;
                label.enableAutoSizing = true;
                var style = label.GetComponent<ProjectSvinElement>();
                if (style != null) style.preserveFontSize = true;
            }
        }
        buttonRevision = theme.revision;
    }
    public static ProjectSvinElement Bind(UnityEngine.UI.Graphic graphic, ProjectSvinRole role)
    {
        var element = graphic.GetComponent<ProjectSvinElement>() ?? graphic.gameObject.AddComponent<ProjectSvinElement>();
        element.theme = ProjectSvinTheme.Current; element.role = role; element.Apply(); return element;
    }
    public static void Outline(UnityEngine.UI.Image image)
    {
        var outline = image.GetComponent<UnityEngine.UI.Outline>() ?? image.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectDistance = new Vector2(1, -1); outline.useGraphicAlpha = true;
        if (ProjectSvinTheme.Current != null) outline.effectColor = ProjectSvinTheme.Current.border;
    }
    public static Color Selection(bool selected) => ProjectSvinTheme.Current != null ?
        selected ? ProjectSvinTheme.Current.selected : ProjectSvinTheme.Current.button :
        selected ? new Color(.28f, .47f, .5f) : new Color(.16f, .26f, .36f);
}
