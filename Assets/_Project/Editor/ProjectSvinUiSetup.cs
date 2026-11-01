using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Creates editable assets through Unity APIs. Never runs automatically or overwrites a user's prefab.</summary>
public static class ProjectSvinUiSetup
{
    public const string Root = "Assets/Resources/UI";
    public const string ThemePath = Root + "/ProjectSvinTheme.asset";
    public const string MenuPath = Root + "/ProjectSvinMainMenu.prefab";
    private static ProjectSvinTheme theme;
    [MenuItem("Svinki/UI/Select shared theme")]
    private static void SelectTheme() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<ProjectSvinTheme>(ThemePath);
    [MenuItem("Svinki/UI/Select editable main-menu prefab")]
    private static void SelectMenu() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
    [MenuItem("Svinki/UI/Create editable Project S.V.I.N. assets")]
    public static void Create()
    {
        Directory.CreateDirectory(Root); AssetDatabase.Refresh();
        theme = AssetDatabase.LoadAssetAtPath<ProjectSvinTheme>(ThemePath);
        if (theme == null) { theme = ScriptableObject.CreateInstance<ProjectSvinTheme>(); AssetDatabase.CreateAsset(theme, ThemePath); }
        if (theme.font == null) theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Localization/Fonts/SvinkiBilingual.asset");
        theme.panelShape = Sprite("panel-mask", new Vector4(30, 30, 30, 30));
        theme.buttonShape = Sprite("button-mask", new Vector4(20, 20, 20, 20));
        theme.snout = Sprite("snout"); theme.hoof = Sprite("hoof");
        theme.hanger = Sprite("hanger"); theme.gamepad = Sprite("gamepad");
        string[] icons = { "friends", "ticket", "play", "book", "settings", "hoof" };
        for (int i = 0; i < icons.Length; i++) theme.actionIcons[i] = Sprite(icons[i]);
        theme.menuBackground = Sprite("menu-background");
        EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath) != null) { Debug.Log("Existing editable menu preserved. Theme sprite references refreshed."); return; }
        var root = new GameObject("Project S.V.I.N. Main Menu", typeof(RectTransform), typeof(ProjectSvinMenuLayout));
        var layout = root.GetComponent<ProjectSvinMenuLayout>();
        var rr = (RectTransform)root.transform; rr.anchorMin = rr.anchorMax = new Vector2(.5f, .5f); rr.sizeDelta = new Vector2(1920, 1080);
        var backdrop = Rect("Background — replace image independently", root.transform, 0, 0, 1920, 1080);
        layout.background = backdrop.gameObject;
        Image(backdrop, ProjectSvinRole.Background).raycastTarget = false;
        var menu = Rect("Menu", root.transform, 155, 45, 680, 990); layout.menuPanel = menu.gameObject;
        layout.projectTitle = Text("Project — editable text", "Project", menu, 0, 0, 650, 58, 46, ProjectSvinRole.Text, true);
        layout.gameTitle = Text("S.V.I.N. — editable text", "S.V.I.N.", menu, 0, 52, 650, 132, 108, ProjectSvinRole.Accent, true);
        layout.nickname = Input("Nickname", menu, 0, 205, 315, "Игрок");
        layout.joinCode = Input("Friend's Code", menu, 335, 205, 315, "");
        layout.status = Text("Connection status", "", menu, 0, 276, 650, 45, 21, ProjectSvinRole.MutedText);
        layout.roster = Text("Lobby participants", "", menu, 0, 330, 650, 280, 25, ProjectSvinRole.Text);
        string[] keys = { "Create Lobby", "Join by Code", "Play Solo", "How to Play", "Settings", "Exit" };
        layout.mainButtons = new UnityEngine.UI.Button[6];
        for (int i = 0; i < keys.Length; i++)
        {
            float y = 338 + i * 88;
            layout.mainButtons[i] = Button(keys[i], menu, 0, y, i == 5 ? 245 : 650, 76, i == 0);
            var button = layout.mainButtons[i];
            Text("Label", theme.Label(keys[i]), button.transform, 78, 0, (i == 5 ? 245 : 650) - 115, 76, i == 5 ? 24 : 30, i == 0 ? ProjectSvinRole.PrimaryText : ProjectSvinRole.Text, true);
            var icon = Rect("Icon — separate replaceable sprite", button.transform, 23, 20, 34, 34);
            var image = icon.gameObject.AddComponent<UnityEngine.UI.Image>(); image.sprite = theme.actionIcons[i]; image.preserveAspect = true; image.raycastTarget = false;
            Bind(image, i == 0 ? ProjectSvinRole.PrimaryText : ProjectSvinRole.Accent, false);
        }
        layout.continueButton = Button("Continue from Checkpoint", menu, 0, 881, 650, 46);
        Text("Label", "Continue from Checkpoint", layout.continueButton.transform, 14, 0, 622, 46, 20, ProjectSvinRole.MutedText);
        layout.sessionActions = Rect("Session actions", menu, 0, 620, 650, 360);
        layout.actionTemplate = Button("Session Action Template — edit to change generated actions", layout.sessionActions, 0, 0, 650, 62);
        Text("Label", "Session action", layout.actionTemplate.transform, 14, 0, 622, 62, 26, ProjectSvinRole.Text);
        layout.actionTemplate.gameObject.SetActive(false);
        var appearance = Rect("Pig Appearance Panel", root.transform, 1045, 78, 700, 922); layout.appearancePanel = appearance.gameObject;
        Image(appearance, ProjectSvinRole.Panel);
        var tag = Rect("Title tag — separate background", appearance, 32, -25, 460, 84); Image(tag, ProjectSvinRole.PrimaryButton);
        layout.appearanceTitle = Text("Title — editable text", theme.Label("YOUR PIG"), tag, 24, 0, 412, 84, 35, ProjectSvinRole.PrimaryText, true);
        layout.previewRect = Rect("3D Pig Preview", appearance, 116, 80, 468, 390); layout.previewRect.gameObject.AddComponent<UnityEngine.UI.RawImage>().raycastTarget = false;
        layout.rotateLeft = Button("Left", appearance, 38, 426, 95, 45); Text("Label", "←", layout.rotateLeft.transform, 0, 0, 95, 45, 32, ProjectSvinRole.Accent);
        layout.rotateRight = Button("Right", appearance, 567, 426, 95, 45); Text("Label", "→", layout.rotateRight.transform, 0, 0, 95, 45, 32, ProjectSvinRole.Accent);
        layout.appearancePresets = new UnityEngine.UI.Button[0];
        layout.appearanceChoices = Rect("Appearance Choices", appearance, 30, 493, 640, 360);
        var scroll = layout.appearanceChoices.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var viewport = Rect("Viewport", layout.appearanceChoices, 0, 0, 640, 360); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var content = Rect("Content", viewport, 0, 0, 640, 0); content.anchorMax = new Vector2(1, 1); content.sizeDelta = Vector2.zero;
        var group = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); group.spacing = 9; group.childControlWidth = group.childControlHeight = true; group.childForceExpandHeight = false;
        content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.scrollSensitivity = 36; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        layout.appearanceChoiceTemplate = Button("Appearance Choice Template", appearance, 30, 493, 640, 46);
        Text("Label", "Appearance choice", layout.appearanceChoiceTemplate.transform, 14, 0, 612, 46, 24, ProjectSvinRole.Text);
        layout.appearanceChoiceTemplate.gameObject.SetActive(false);
        Text("Scroll hint", "Scroll for more. Changes are saved.", appearance, 30, 874, 640, 30, 18, ProjectSvinRole.MutedText);
        Text("Language", "RU / EN", root.transform, 45, 1023, 180, 36, 22, ProjectSvinRole.MutedText);
        var hanger = Rect("Hanger decoration", appearance, 538, 112, 105, 105); var hi = hanger.gameObject.AddComponent<UnityEngine.UI.Image>(); hi.sprite = theme.hanger; hi.color = new Color(1, 1, 1, .12f); hi.raycastTarget = false; hanger.SetAsFirstSibling();
        PrefabUtility.SaveAsPrefabAsset(root, MenuPath); UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets(); Debug.Log("Created editable Project S.V.I.N. theme and main-menu prefab.");
    }
    private static Sprite Sprite(string name, Vector4 border = default)
    {
        string path = "Assets/_Project/Content/UI/ProjectSvin/" + name + ".png";
        if (!File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single; importer.spriteBorder = border; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
        importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var r = (RectTransform)obj.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
    }
    private static UnityEngine.UI.Image Image(RectTransform rect, ProjectSvinRole role)
    {
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); Bind(image, role); ProjectSvinSkin.Outline(image); return image;
    }
    private static void Bind(UnityEngine.UI.Graphic graphic, ProjectSvinRole role, bool sprite = true)
    {
        var originalSprite = graphic is UnityEngine.UI.Image image ? image.sprite : null;
        var bind = graphic.gameObject.AddComponent<ProjectSvinElement>(); bind.theme = theme; bind.role = role; bind.useThemeSprite = sprite; bind.Apply();
        if (!sprite && graphic is UnityEngine.UI.Image icon) icon.sprite = originalSprite;
    }
    private static TMP_Text Text(string name, string text, Transform parent, float x, float y, float w, float h, float size, ProjectSvinRole role, bool bold = false)
    {
        var rect = Rect(name, parent, x, y, w, h); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = theme.font; label.fontSize = size; label.text = text; label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.enableAutoSizing = true; label.fontSizeMin = size * .7f; label.fontSizeMax = size;
        Bind(label, role);
        if (name.StartsWith("S.V.I.N.")) { label.GetComponent<ProjectSvinElement>().fontRole = ProjectSvinFontRole.Logo; label.GetComponent<ProjectSvinElement>().Apply(); }
        return label;
    }
    private static UnityEngine.UI.Button Button(string name, Transform parent, float x, float y, float w, float h, bool primary = false)
    {
        var rect = Rect(name, parent, x, y, w, h); var image = Image(rect, primary ? ProjectSvinRole.PrimaryButton : ProjectSvinRole.Button);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(1.13f, 1.13f, 1.13f); colors.pressedColor = new Color(.8f, .8f, .8f); colors.disabledColor = new Color(.55f, .55f, .55f, .65f); button.colors = colors;
        return button;
    }
    private static TMP_InputField Input(string source, Transform parent, float x, float y, float w, string value)
    {
        var rect = Rect(source, parent, x, y, w, 66); Image(rect, ProjectSvinRole.Panel);
        var viewport = Rect("Viewport", rect, 16, 4, w - 32, 58); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var text = Text("Input text", value, viewport, 0, 0, w - 32, 58, 26, ProjectSvinRole.Text);
        var hint = Text("Placeholder", source, viewport, 0, 0, w - 32, 58, 23, ProjectSvinRole.MutedText);
        var input = rect.gameObject.AddComponent<TMP_InputField>(); input.textViewport = viewport; input.textComponent = (TextMeshProUGUI)text;
        input.placeholder = hint; input.text = value; input.characterLimit = source == "Nickname" ? 24 : 6; input.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>(); return input;
    }
}
