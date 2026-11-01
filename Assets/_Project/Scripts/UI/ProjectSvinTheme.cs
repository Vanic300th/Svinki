using System;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Project S.V.I.N./UI Theme", fileName = "ProjectSvinTheme")]
public sealed class ProjectSvinTheme : ScriptableObject
{
    [Header("Brand — editable text, never baked into a background")]
    public string projectName = "Project";
    public string gameName = "S.V.I.N.";
    public TMP_FontAsset font;
    [Header("Typography — headings, input, body and brand")]
    public TMP_FontAsset headingFont;
    public TMP_FontAsset bodyFont;
    public TMP_FontAsset logoFont;
    public TMP_FontAsset inputFont;
    public TMP_FontAsset brandFont;
    public Font worldHeadingFont;
    public TMP_FontAsset FontFor(ProjectSvinFontRole role)
    {
        var selectedFont = role == ProjectSvinFontRole.Logo ? logoFont :
            role == ProjectSvinFontRole.Heading ? headingFont :
            role == ProjectSvinFontRole.Input ? inputFont :
            role == ProjectSvinFontRole.Brand ? brandFont : bodyFont;
        return selectedFont != null ? selectedFont : font;
    }
    [Header("Button spacing and type") ]
    [Range(.75f, 1)] public float buttonScale = .92f;
    [Range(.75f, 1)] public float buttonTextScale = .90f;
    [Header("Palette")]
    public Color background = Hex("241921");
    public Color panel = Hex("3D2633");
    public Color button = Hex("754253");
    public Color primary = Hex("EDB3C5");
    public Color text = Hex("F8E9E4");
    public Color primaryText = Hex("301D26");
    public Color muted = Hex("C99DAE");
    public Color border = Hex("B77B90");
    public Color track = Hex("573341");
    public Color selected = Hex("9F5C77");
    [Range(0, 1)] public float backgroundOpacity = .8f;
    [Range(0, 1)] public float panelOpacity = .97f;
    [Header("In-game surfaces — text and icons keep their authored opacity")]
    [Range(0, 1)] public float inGamePanelOpacity = .38f;
    [Range(0, 1)] public float inGameButtonOpacity = .55f;
    [Range(0, 1)] public float inGameTrackOpacity = .22f;
    [Header("Main menu motion")]
    [Range(0, 80)] public float backgroundTravel = 26;
    [Range(4, 60)] public float backgroundPeriod = 20;
    [Range(1, 1.2f)] public float backgroundZoom = 1.06f;
    [Range(1, 2)] public float appearanceControlScale = 1.5f;
    [Header("Replace individual PNGs here")]
    public Sprite menuBackground;
    public Sprite panelShape;
    public Sprite buttonShape;
    public Sprite snout;
    public Sprite hoof;
    public Sprite hanger;
    public Sprite gamepad;
    public Sprite[] actionIcons = new Sprite[6];
    [Header("Button labels — English / Russian")]
    public MenuText[] labels = {
        new MenuText("Create Lobby", "Create Lobby", "Создать лобби"),
        new MenuText("Join by Code", "Join by Code", "Войти по коду"),
        new MenuText("Play Solo", "Play Solo", "Играть одному"),
        new MenuText("How to Play", "How to Play", "Как играть"),
        new MenuText("Settings", "Settings", "Настройки"),
        new MenuText("Exit", "Exit", "Выход"),
        new MenuText("YOUR PIG", "YOUR PIG", "ТВОЯ СВИНКА")
    };
    [NonSerialized] public int revision;
    private static ProjectSvinTheme current;
    public static ProjectSvinTheme Current => current != null ? current : current = Resources.Load<ProjectSvinTheme>("UI/ProjectSvinTheme");
    public string Label(string source)
    {
        foreach (var item in labels)
            if (item.key == source) return GameLanguage.Code == "ru" ? item.russian : item.english;
        return GameLanguage.Text(source);
    }
    public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var value); return value; }
    private void OnValidate() => revision++;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetThemeCache() => current = null;
    [Serializable] public sealed class MenuText
    {
        public string key, english, russian;
        public MenuText(string key, string english, string russian) { this.key = key; this.english = english; this.russian = russian; }
    }
}
