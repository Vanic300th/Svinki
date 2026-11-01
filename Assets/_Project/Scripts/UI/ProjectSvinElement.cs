using TMPro;
using UnityEngine;

public enum ProjectSvinRole { Panel, Button, PrimaryButton, Text, PrimaryText, MutedText, Track, Accent, Background, Snout }
public enum ProjectSvinFontRole { Auto, Body, Heading, Logo, Input, Brand }

/// <summary>Each element can follow the shared theme or be customized independently in the Inspector.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class ProjectSvinElement : MonoBehaviour
{
    public ProjectSvinTheme theme;
    public ProjectSvinRole role;
    public bool useThemeColor = true;
    public bool useThemeSprite = true;
    public bool useThemeFont = true;
    public ProjectSvinFontRole fontRole;
    public bool preserveFontSize;
    [Range(0, 1)] public float opacity = 1;
    private int revision = -1;
    private UnityEngine.UI.Image surface;
    private bool inMainMenu, translucent;
    private float surfaceAlpha = 1;
    private void OnEnable() { CacheSurface(); if (theme != null) Apply(); }
    private void OnValidate() { if (theme != null) Apply(); }
    private void OnTransformParentChanged() { CacheSurface(); revision = -1; }
    private void CacheSurface()
    {
        surface = GetComponent<UnityEngine.UI.Image>();
        inMainMenu = surface != null && GetComponentInParent<ProjectSvinMenuLayout>(true) != null;
    }
    private bool IsGameSurface => surface != null && !inMainMenu && surface.canvas != null &&
        surface.canvas.renderMode != RenderMode.WorldSpace && ProjectSvinSkin.InGame;
    private void Update()
    {
        if (theme != null && (revision != theme.revision || translucent != IsGameSurface)) Apply();
    }
    private void LateUpdate()
    {
        // Selected tabs can replace their tint every frame. Keep that tint and cap its surface alpha.
        if (!translucent || !useThemeColor || surface == null || surface.color.a <= surfaceAlpha) return;
        var color = surface.color; color.a = surfaceAlpha; surface.color = color;
    }
    public TMP_FontAsset ResolvedFont
    {
        get
        {
            if (theme == null) return GameLanguage.Font;
            return theme.FontFor(fontRole == ProjectSvinFontRole.Auto ? RoleFor(GetComponent<TMP_Text>()) : fontRole);
        }
    }
    public static ProjectSvinFontRole RoleFor(TMP_Text label)
    {
        if (label == null) return ProjectSvinFontRole.Body;
        if (label.GetComponentInParent<TMP_InputField>(true) != null) return ProjectSvinFontRole.Input;
        bool heading = label is TextMeshPro || label.GetComponentInParent<UnityEngine.UI.Button>(true) != null ||
            label.name.IndexOf("title", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            label.name.IndexOf("heading", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            label.name.IndexOf("header", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            (label.enableAutoSizing ? label.fontSizeMax : label.fontSize) >= 32;
        return heading ? ProjectSvinFontRole.Heading : ProjectSvinFontRole.Body;
    }

    public void Apply()
    {
        if (theme == null) theme = ProjectSvinTheme.Current;
        if (theme == null) return;
        revision = theme.revision;
        if (surface == null) CacheSurface();
        translucent = IsGameSurface;
        var label = GetComponent<TMP_Text>();
        if (label != null && useThemeFont && ResolvedFont != null && label.font != ResolvedFont)
        {
            // Physical signs keep their previous footprint with the wider display face.
            Vector2 previous = label is TextMeshPro ? label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity) : Vector2.zero;
            label.font = ResolvedFont;
            if (label is TextMeshPro && !preserveFontSize)
            {
                Vector2 current = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity);
                float fit = Mathf.Min(1, previous.x > 0 && current.x > 0 ? previous.x / current.x : 1,
                    previous.y > 0 && current.y > 0 ? previous.y / current.y : 1);
                label.fontSize *= fit;
            }
            GetComponent<WorldTextDepth>()?.RefreshMaterial();
        }
        var graphic = GetComponent<UnityEngine.UI.Graphic>();
        if (graphic == null) return;
        Color color = theme.text;
        switch (role)
        {
            case ProjectSvinRole.Panel: color = theme.panel; color.a = theme.panelOpacity; break;
            case ProjectSvinRole.Button: color = theme.button; break;
            case ProjectSvinRole.PrimaryButton: case ProjectSvinRole.Accent: case ProjectSvinRole.Snout: color = theme.primary; break;
            case ProjectSvinRole.PrimaryText: color = theme.primaryText; break;
            case ProjectSvinRole.MutedText: color = theme.muted; break;
            case ProjectSvinRole.Track: color = theme.track; break;
            case ProjectSvinRole.Background: color = theme.menuBackground != null ? Color.white : theme.background; color.a = theme.backgroundOpacity; break;
        }
        color.a *= opacity;
        float surfaceOpacity = 1;
        if (translucent)
        {
            if (role == ProjectSvinRole.Panel) surfaceOpacity = theme.inGamePanelOpacity;
            else if (role == ProjectSvinRole.Button || role == ProjectSvinRole.PrimaryButton) surfaceOpacity = theme.inGameButtonOpacity;
            else if (role == ProjectSvinRole.Track) surfaceOpacity = theme.inGameTrackOpacity;
        }
        color.a *= surfaceOpacity;
        surfaceAlpha = color.a;
        if (useThemeColor) graphic.color = color;
        if (graphic is UnityEngine.UI.Image image)
        {
            if (useThemeSprite)
            {
                Sprite sprite = role == ProjectSvinRole.Snout ? theme.snout :
                    role == ProjectSvinRole.Background ? theme.menuBackground :
                    role == ProjectSvinRole.Panel ? theme.panelShape :
                    role == ProjectSvinRole.Button || role == ProjectSvinRole.PrimaryButton ? theme.buttonShape : null;
                if (sprite != null) { image.sprite = sprite; image.type = role == ProjectSvinRole.Snout || role == ProjectSvinRole.Background ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced; }
            }
            var outline = GetComponent<UnityEngine.UI.Outline>();
            if (outline != null) { var border = theme.border; border.a *= surfaceOpacity; outline.effectColor = border; }
        }
    }
}
