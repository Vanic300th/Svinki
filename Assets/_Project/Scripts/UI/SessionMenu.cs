using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public sealed class SessionMenu : MonoBehaviour
{
    private NetworkLobby lobby;
    private GameObject panel, settingsPanel;
    private TMP_Text status, title, roster;
    private TMP_InputField nickname, code;
    private readonly List<GameObject> controls = new List<GameObject>();
    private TMP_FontAsset font;
    private string layoutKey;
    private EventSystem sessionEvents;
    private PigAppearanceMenu appearanceMenu;
    private OutfitResultsView resultsView;
    private HowToPlay guide;
    private UnityEngine.UI.Button englishLanguage, russianLanguage;
    private ProjectSvinMenuLayout editableMenu;
    private int themeRevision = -1;
    private bool choosingPlay;
    private bool appearanceOpen;
    private TMP_Text lobbyCode;
    private static SessionMenu activeMenu;
    private static int escapeFrame = -1;
    public static bool ConsumedEscape => escapeFrame == Time.frameCount || activeMenu != null && activeMenu.panel != null && activeMenu.panel.activeInHierarchy &&
        (activeMenu.appearanceOpen || activeMenu.choosingPlay || activeMenu.settingsPanel != null && activeMenu.settingsPanel.activeSelf) &&
        UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true;
    private void Start()
    {
        activeMenu = this;
        lobby = GetComponent<NetworkLobby>();
        var eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var events = new GameObject("Session EventSystem"); events.transform.SetParent(transform);
            events.AddComponent<EventSystem>(); events.AddComponent<InputSystemUIInputModule>();
        }
        else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            var legacy = eventSystem.GetComponent<StandaloneInputModule>(); if (legacy != null) Destroy(legacy);
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        sessionEvents = FindAnyObjectByType<EventSystem>();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        font = TMP_FontAsset.CreateFontAsset(lobby.MenuFont);
        font.name = "Session dynamic font";
        var canvasObject = new GameObject("Session Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.GetComponent<Canvas>().sortingOrder = 100;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        var menuPrefab = Resources.Load<GameObject>("UI/ProjectSvinMainMenu");
        if (menuPrefab != null) editableMenu = Instantiate(menuPrefab, canvasObject.transform, false).GetComponent<ProjectSvinMenuLayout>();
        if (editableMenu != null)
        {
            panel = editableMenu.menuPanel; title = editableMenu.gameTitle; status = editableMenu.status; roster = editableMenu.roster;
            nickname = editableMenu.nickname; code = editableMenu.joinCode; nickname.text = lobby.Nickname;
            var main = editableMenu.mainButtons;
            if (editableMenu.playButton != null) editableMenu.playButton.onClick.AddListener(() => { choosingPlay = true; Refresh(); });
            if (editableMenu.playBackButton != null) editableMenu.playBackButton.onClick.AddListener(() => { choosingPlay = false; Refresh(); });
            if (editableMenu.customizeButton != null) editableMenu.customizeButton.onClick.AddListener(() => { appearanceOpen = true; Refresh(); });
            if (editableMenu.appearanceClose != null) editableMenu.appearanceClose.onClick.AddListener(CloseAppearance);
            if (editableMenu.russianLanguageButton != null) editableMenu.russianLanguageButton.onClick.AddListener(() => GameLanguage.Select("ru"));
            if (editableMenu.englishLanguageButton != null) editableMenu.englishLanguageButton.onClick.AddListener(() => GameLanguage.Select("en"));
            main[0].onClick.AddListener(() => guide.BeforeStart(() => { lobby.Nickname = nickname.text; lobby.CreateOnline(); }));
            main[1].onClick.AddListener(() => guide.BeforeStart(() => { lobby.Nickname = nickname.text; lobby.JoinOnline(code.text); }));
            main[2].onClick.AddListener(() => guide.BeforeStart(lobby.StartOffline));
            main[3].onClick.AddListener(() => guide.Show());
            main[4].onClick.AddListener(ShowSettings);
            main[5].onClick.AddListener(QuitGame);
            editableMenu.continueButton.onClick.AddListener(() => guide.BeforeStart(() => { lobby.Nickname = nickname.text; lobby.CreateOnline(true); }));
            foreach (var label in editableMenu.GetComponentsInChildren<TMP_Text>(true))
                if (GameLanguage.HasSource(label.text)) GameLanguage.Set(label, label.text);
        }
        else
        {
        panel = new GameObject("Lobby panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(900, 970);
        rect.anchoredPosition = new Vector2(-252, 0);
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.04f, .055f, .085f, .98f);
        title = Label("SVINKI", 52, 44, 440, 64, 28);
        Button("How to Play", 510, 50, 155, () => guide.Show());
        Button("Settings", 680, 50, 165, () => { settingsPanel.SetActive(!settingsPanel.activeSelf); settingsPanel.transform.SetAsLastSibling(); });
        status = Label("", 52, 114, 800, 110, 24);
        nickname = Input("Nickname", 52, 230, 385, lobby.Nickname, 24);
        code = Input("Friend's Code", 460, 230, 385, "", 6);
        nickname.onEndEdit.AddListener(value => lobby.Nickname = value);
        code.onValueChanged.AddListener(value => { if (value != value.ToUpperInvariant()) code.SetTextWithoutNotify(value.ToUpperInvariant()); });
        roster = Label("", 52, 305, 800, 285, 26);
        }
        if (editableMenu != null)
        {
            nickname.onEndEdit.AddListener(value => lobby.Nickname = value);
            code.onValueChanged.AddListener(value => { if (value != value.ToUpperInvariant()) code.SetTextWithoutNotify(value.ToUpperInvariant()); });
        }
        appearanceMenu = gameObject.AddComponent<PigAppearanceMenu>();
        appearanceMenu.Build(canvasObject.transform, font, lobby, editableMenu);
        BuildSettings();
        resultsView = gameObject.AddComponent<OutfitResultsView>();
        resultsView.Build(canvasObject.transform, font, lobby);
        gameObject.AddComponent<OutfitInventoryView>().Build(canvasObject.transform, font, lobby);
        gameObject.AddComponent<CartCargoView>().Build(canvasObject.transform, font, lobby);
        gameObject.AddComponent<PlayerRoundHud>().Build(canvasObject.transform, font, lobby);
        guide=gameObject.AddComponent<HowToPlay>();guide.Build(canvasObject.transform,font,lobby);
        BuildLobbyCode(canvasObject.transform);
        lobby.Changed += Refresh;
        GameLanguage.Changed += RefreshLanguage;
        Refresh();
    }
    private void OnDestroy()
    {
        if (activeMenu == this) { activeMenu = null; escapeFrame = -1; }
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        if (lobby != null) lobby.Changed -= Refresh;
        GameLanguage.Changed -= RefreshLanguage;
        if (font != null) Destroy(font);
    }
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        foreach (EventSystem events in FindObjectsByType<EventSystem>())
            if (events != sessionEvents) { events.enabled = false; foreach (var module in events.GetComponents<BaseInputModule>()) module.enabled = false; }
    }
    private void Update()
    {
        if (ConsumedEscape)
        {
            escapeFrame = Time.frameCount;
            if (appearanceOpen) CloseAppearance();
            else if (settingsPanel.activeSelf) settingsPanel.SetActive(false);
            else { choosingPlay = false; Refresh(); }
        }
        if (lobbyCode != null)
        {
            bool visible = lobby.InSession && !lobby.Offline && !string.IsNullOrEmpty(lobby.Snapshot.code);
            lobbyCode.transform.parent.gameObject.SetActive(visible);
            if (visible)
            {
                string value = (GameLanguage.Code == "ru" ? "КОД ЛОББИ  " : "LOBBY CODE  ") + lobby.Snapshot.code;
                if (lobbyCode.text != value) GameLanguage.SetLiteral(lobbyCode, value);
            }
        }
        if (panel != null)
        {
            bool showingResults = lobby.Results != null;
            panel.SetActive(!showingResults && !lobby.InventoryOpen && (lobby.MenuVisible || !lobby.InSession && !lobby.Offline));
            if (editableMenu != null)
            {
                editableMenu.background.SetActive(panel.activeSelf);
                editableMenu.gameObject.SetActive(panel.activeSelf);
                if (!panel.activeSelf && settingsPanel.activeSelf) settingsPanel.SetActive(false);
                var theme = ProjectSvinTheme.Current;
                if (theme != null && themeRevision != theme.revision) { themeRevision = theme.revision; RefreshMenuText(); }
            }
            if (!panel.activeSelf) appearanceOpen = false;
            if (editableMenu != null && editableMenu.appearanceOverlay != null)
            {
                editableMenu.appearanceOverlay.SetActive(panel.activeSelf && appearanceOpen);
                if (appearanceOpen) editableMenu.appearanceOverlay.transform.SetAsLastSibling();
            }
            appearanceMenu?.SetVisible(panel.activeSelf && (editableMenu == null || appearanceOpen));
            resultsView?.SetVisible(showingResults);
            if (panel.activeSelf && (lobby.Offline || lobby.Snapshot.phase == SessionPhase.Round)) Refresh();
        }
    }
    private void Refresh()
    {
        if (panel == null) return;
        var snapshot = lobby.Snapshot;
        if (editableMenu == null) GameLanguage.Set(title, lobby.Offline ? "Project S.V.I.N. — Single Player" : lobby.InSession ? "LOBBY  " + snapshot.code : "Project S.V.I.N.");
        else RefreshMenuText();
        GameLanguage.Set(status, lobby.Status);
        nickname.gameObject.SetActive(!lobby.InSession && !lobby.Offline && !lobby.Busy);
        code.gameObject.SetActive(nickname.gameObject.activeSelf);
        if (editableMenu != null)
        {
            bool home = !lobby.InSession && !lobby.Offline && !lobby.Busy;
            if (!home) choosingPlay = false;
            bool compactMenu = editableMenu.playButton != null;
            bool modes = home && (!compactMenu || choosingPlay);
            for (int i = 0; i < editableMenu.mainButtons.Length; i++)
                editableMenu.mainButtons[i].gameObject.SetActive(home && (i < 3 ? modes : !compactMenu || !choosingPlay));
            if (editableMenu.customizeButton != null) editableMenu.customizeButton.gameObject.SetActive(home && !choosingPlay);
            if (compactMenu)
            {
                editableMenu.playButton.gameObject.SetActive(home && !choosingPlay);
                editableMenu.playBackButton.gameObject.SetActive(modes);
                editableMenu.playOptionsPanel.SetActive(modes);
                nickname.gameObject.SetActive(modes); code.gameObject.SetActive(modes);
                status.gameObject.SetActive(!home || modes);
            }
#if UNITY_WEBGL
            editableMenu.mainButtons[0].interactable = editableMenu.mainButtons[1].interactable = false;
#endif
            editableMenu.continueButton.gameObject.SetActive(modes && System.IO.File.Exists(CheckpointStore.DefaultPath));
            editableMenu.sessionActions.gameObject.SetActive(!home);
            roster.gameObject.SetActive(lobby.InSession);
            if (home)
            {
                if (controls.Count > 0) { foreach (var control in controls) Destroy(control); controls.Clear(); layoutKey = null; }
                return;
            }
        }
        // Names remain literal even when a nickname happens to be an English UI word.
        roster.text = string.Join("\n", snapshot.players.Select(p =>
            (p.id == snapshot.host ? GameLanguage.Text("Host: ") : "• ") + p.nickname + (p.id == lobby.Identity ? GameLanguage.Text(" (you)") : "") + " — " +
            GameLanguage.Text(!p.connected ? "reconnecting" : p.spectator ? "spectator" : p.ready ? "ready" : "not ready"))) +
            (lobby.InSession ? "\n\n" + string.Format(GameLanguage.Text("Players: {0}/6  ·  Round {1}{2}"),snapshot.players.Length,snapshot.round,
             GameLanguage.Text(snapshot.closed ? "  ·  Admission closed" : "  ·  Admission open")) : "");
        string key = snapshot.phase + ":" + lobby.IsHost + ":" + lobby.Busy + ":" + lobby.Offline + ":" +
            string.Join(",", snapshot.players.Select(p => p.id + p.ready + p.connected + p.finishReady)) + ":" + snapshot.closed + ":" + lobby.NearFinish;
        if (key == layoutKey) return;
        layoutKey = key;
        foreach (GameObject control in controls) Destroy(control); controls.Clear();
        int row = 0;
        void Add(string label, Action action, bool enabled = true)
        {
            var button = editableMenu != null ? SessionAction(label, row++, action) : Button(label, 52, 605 + row++ * 60, 795, action);
            button.interactable = enabled; controls.Add(button.gameObject);
        }
        if (lobby.Busy) { Add("Cancel Connection", lobby.Cancel); return; }
        if (lobby.Offline) { Add("Resume", Resume); Add("Settings", ShowSettings); Add("Main Menu", lobby.Leave); return; }
        if (!lobby.InSession)
        {
#if !UNITY_WEBGL
            Add("Create Lobby", () => guide.BeforeStart(() => { lobby.Nickname = nickname.text; lobby.CreateOnline(); }));
            Add("Continue from Checkpoint", () => guide.BeforeStart(() => { lobby.Nickname = nickname.text; lobby.CreateOnline(true); }));
            Add("Join by Code", () => guide.BeforeStart(() => { lobby.Nickname = nickname.text; lobby.JoinOnline(code.text); }));
#endif
            Add("Play Solo", () => guide.BeforeStart(lobby.StartOffline));
#if UNITY_EDITOR || DEBUG
            Add("Local test: host / client — F8 / F9", () => lobby.Report("Editor: F8 — host, F9 — localhost client; a second game instance is required."));
#endif
            return;
        }
        if (snapshot.phase == SessionPhase.Lobby)
        {
            Add("Enter Friendslop Lobby", Resume);
            if (editableMenu != null && editableMenu.customizeButton != null)
                Add(GameLanguage.Code == "ru" ? "Кастомайз" : "Customize", () => { appearanceOpen = true; Refresh(); });
        }
        else if (snapshot.phase == SessionPhase.Round)
        {
            Add(lobby.IsSpectator ? "Resume Spectating" : "Resume", Resume);
        }
        Add("Copy Code: " + snapshot.code, () => { GUIUtility.systemCopyBuffer = snapshot.code; lobby.Report("Code copied: " + snapshot.code); });
        if (lobby.IsHost) Add(snapshot.closed ? "Open Admission" : "Close Admission", lobby.ToggleAdmission);
        Add(lobby.IsHost ? "End Session" : "Leave Session", lobby.Leave);
        if (editableMenu != null) Add("Settings", ShowSettings);
        if (lobby.IsHost)
        {
            int n = 0;
            foreach (var player in snapshot.players.Where(p => p.id != snapshot.host))
            {
                string id = player.id;
                var button = Button("× " + player.nickname, editableMenu != null ? 440 : 600, 330 + n++ * 44, editableMenu != null ? 210 : 245, () => lobby.Kick(id), 38);
                controls.Add(button.gameObject);
            }
        }
    }
    private void BuildSettings()
    {
        RectTransform box;
        if (editableMenu != null)
        {
            var overlay = Rect("Settings Overlay", editableMenu.transform, 0, 0, 1920, 1080);
            var wash = overlay.gameObject.AddComponent<UnityEngine.UI.Image>(); wash.color = new Color(.07f, .03f, .055f, .9f);
            settingsPanel = overlay.gameObject;
            var window = Rect("Settings Window", overlay, 535, 80, 850, 920);
            ProjectSvinSkin.Bind(window.gameObject.AddComponent<UnityEngine.UI.Image>(), ProjectSvinRole.Panel); ProjectSvinSkin.Outline(window.GetComponent<UnityEngine.UI.Image>());
            Label("Settings", 28, 20, 650, 60, 36, window);
            var closeRect = Rect("Close", window, 748, 20, 72, 54);
            var closeImage = closeRect.gameObject.AddComponent<UnityEngine.UI.Image>(); ProjectSvinSkin.Bind(closeImage, ProjectSvinRole.Button);
            var close = closeRect.gameObject.AddComponent<UnityEngine.UI.Button>(); close.targetGraphic = closeImage;
            Label("×", 0, 0, 72, 54, 32, closeRect).alignment = TextAlignmentOptions.Center;
            close.onClick.AddListener(() => settingsPanel.SetActive(false));
            box = Rect("Settings Controls", window, 28, 95, 795, 795);
        }
        else
        {
            box = Rect("Settings", panel.transform, 52, 250, 795, 530); settingsPanel = box.gameObject;
            settingsPanel.AddComponent<UnityEngine.UI.Image>().color = new Color(.07f,.10f,.15f,1);
        }
        Label("Mouse Sensitivity", 24, 20, 730, 44, 26, box);
        var value = Label("", 24, 65, 730, 34, 22, box);
        var track = Rect("Mouse sensitivity", box, 28, 122, 735, 40);
        var background = track.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color(.15f,.22f,.30f);
        var handleArea = Rect("Handle area", track, 12, 0, 711, 40);
        var handle = Rect("Handle", handleArea, 0, 0, 24, 40);
        handle.sizeDelta = new Vector2(24, 0); handle.pivot = new Vector2(.5f, .5f);
        handle.anchoredPosition = Vector2.zero;
        var handleImage = handle.gameObject.AddComponent<UnityEngine.UI.Image>(); handleImage.color = new Color(.55f,.77f,.85f);
        var slider = track.gameObject.AddComponent<UnityEngine.UI.Slider>();
        slider.minValue = .25f; slider.maxValue = 3; slider.handleRect = handle; slider.targetGraphic = handleImage;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.SetValueWithoutNotify(MouseSettings.Multiplier);
        void RefreshValue(float v) => GameLanguage.Set(value, Mathf.RoundToInt(v * 100) + "%");
        RefreshValue(slider.value);
        slider.onValueChanged.AddListener(v => { MouseSettings.Set(v); RefreshValue(v); });
        Label("25% — slow                         300% — fast", 24, 177, 730, 36, 20, box);
        var reset = Rect("Reset sensitivity", box, 24, 225, 350, 44);
        var resetImage = reset.gameObject.AddComponent<UnityEngine.UI.Image>(); resetImage.color = new Color(.16f,.26f,.36f);
        var button = reset.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = resetImage;
        var resetLabel = Label("Reset to Default", 12, 5, 326, 34, 20, reset);
        resetLabel.alignment = TextAlignmentOptions.Center;
        resetLabel.textWrappingMode = TextWrappingModes.NoWrap;
        resetLabel.overflowMode = TextOverflowModes.Ellipsis;
        button.onClick.AddListener(() => slider.value = 1);
        var musicLabel = Label("", 24, 280, 730, 36, 24, box);
        var musicTrack = Rect("Music volume", box, 28, 326, 735, 36);
        musicTrack.gameObject.AddComponent<UnityEngine.UI.Image>().color = background.color;
        var musicArea = Rect("Music handle area", musicTrack, 12, 0, 711, 36);
        var musicHandle = Rect("Music handle", musicArea, 0, 0, 24, 36);
        musicHandle.sizeDelta = new Vector2(24, 0); musicHandle.pivot = new Vector2(.5f, .5f);
        musicHandle.anchoredPosition = Vector2.zero;
        var musicImage = musicHandle.gameObject.AddComponent<UnityEngine.UI.Image>(); musicImage.color = handleImage.color;
        var musicSlider = musicTrack.gameObject.AddComponent<UnityEngine.UI.Slider>();
        musicSlider.minValue = 0; musicSlider.maxValue = 1; musicSlider.handleRect = musicHandle; musicSlider.targetGraphic = musicImage;
        musicSlider.SetValueWithoutNotify(MusicSettings.Volume);
        void RefreshMusic(float v) => GameLanguage.Set(musicLabel, "Music volume: " + Mathf.RoundToInt(v * 100) + "%");
        RefreshMusic(musicSlider.value);
        musicSlider.onValueChanged.AddListener(v => { MusicSettings.Set(v); RefreshMusic(v); });
        Label("Language", 24, 398, 730, 36, 24, box);
        UnityEngine.UI.Button LanguageButton(string name, float x, string code)
        {
            var language = Rect(name + " language", box, x, 450, 350, 52);
            var image = language.gameObject.AddComponent<UnityEngine.UI.Image>();
            if (ProjectSvinTheme.Current != null) { ProjectSvinSkin.Bind(image, ProjectSvinRole.Button); ProjectSvinSkin.Outline(image); }
            var choice = language.gameObject.AddComponent<UnityEngine.UI.Button>(); choice.targetGraphic = image;
            Label(name, 10, 5, 330, 42, 24, language).alignment = TextAlignmentOptions.Center;
            choice.onClick.AddListener(() => GameLanguage.Select(code)); return choice;
        }
        englishLanguage = LanguageButton("English",24,"en");
        russianLanguage = LanguageButton("Русский",400,"ru");
        void HudSize(string caption, float y, float initial, Action<float> save)
        {
            var label = Label("", 24, y, 730, 38, 24, box);
            var trackRect = Rect(caption, box, 28, y + 48, 735, 36);
            trackRect.gameObject.AddComponent<UnityEngine.UI.Image>().color = background.color;
            var area = Rect("Handle area", trackRect, 12, 0, 711, 36);
            var grip = Rect("Handle", area, 0, 0, 24, 36);
            grip.sizeDelta = new Vector2(24, 0); grip.pivot = new Vector2(.5f, .5f); grip.anchoredPosition = Vector2.zero;
            var gripImage = grip.gameObject.AddComponent<UnityEngine.UI.Image>(); gripImage.color = handleImage.color;
            var sizeSlider = trackRect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            sizeSlider.minValue = HudDisplaySettings.MinimumScale; sizeSlider.maxValue = HudDisplaySettings.MaximumScale;
            sizeSlider.handleRect = grip; sizeSlider.targetGraphic = gripImage;
            sizeSlider.SetValueWithoutNotify(initial);
            void UpdateLabel(float amount) => GameLanguage.Set(label, caption + ": " + Mathf.RoundToInt(amount * 100) + "%");
            UpdateLabel(initial);
            sizeSlider.onValueChanged.AddListener(amount => { save(amount); UpdateLabel(amount); });
        }
        HudSize("Minimap size", 530, HudDisplaySettings.MinimapScale, HudDisplaySettings.SetMinimap);
        HudSize("Pig portrait size", 640, HudDisplaySettings.PigScale, HudDisplaySettings.SetPig);
        RefreshLanguage();
        settingsPanel.SetActive(false);
    }
    private void RefreshLanguage()
    {
        layoutKey = null; Refresh();
        if (englishLanguage != null) englishLanguage.targetGraphic.color = ProjectSvinSkin.Selection(GameLanguage.Code == "en");
        if (russianLanguage != null) russianLanguage.targetGraphic.color = ProjectSvinSkin.Selection(GameLanguage.Code == "ru");
        if (settingsPanel != null && settingsPanel.activeSelf) settingsPanel.transform.SetAsLastSibling();
    }
    private void RefreshMenuText()
    {
        var theme = ProjectSvinTheme.Current;
        if (editableMenu == null || theme == null) return;
        GameLanguage.SetLiteral(editableMenu.projectTitle, theme.projectName);
        GameLanguage.SetLiteral(title, theme.gameName);
        GameLanguage.SetLiteral(editableMenu.appearanceTitle, theme.Label("YOUR PIG"));
        if (editableMenu.playButton != null) GameLanguage.SetLiteral(editableMenu.playButton.GetComponentInChildren<TMP_Text>(), theme.Label("Play"));
        if (editableMenu.playBackButton != null) GameLanguage.SetLiteral(editableMenu.playBackButton.GetComponentInChildren<TMP_Text>(), theme.Label("Back"));
        if (editableMenu.customizeButton != null) GameLanguage.SetLiteral(editableMenu.customizeButton.GetComponentInChildren<TMP_Text>(), theme.Label("Customize"));
        if (editableMenu.appearanceClose != null) GameLanguage.SetLiteral(editableMenu.appearanceClose.GetComponentInChildren<TMP_Text>(), "×");
        string[] keys = { "Create Lobby", "Join by Code", "Play Solo", "How to Play", "Settings", "Exit" };
        for (int i = 0; i < keys.Length; i++) GameLanguage.SetLiteral(editableMenu.mainButtons[i].GetComponentInChildren<TMP_Text>(), theme.Label(keys[i]));
        RefreshLanguageButton(editableMenu.russianLanguageButton, "ru", theme);
        RefreshLanguageButton(editableMenu.englishLanguageButton, "en", theme);
    }
    private static void RefreshLanguageButton(UnityEngine.UI.Button button, string code, ProjectSvinTheme theme)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label == null) return;
        bool selected = GameLanguage.Code == code;
        label.color = selected ? theme.primary : theme.muted;
        label.fontStyle = selected ? FontStyles.Underline : FontStyles.Normal;
    }
    private void CloseAppearance()
    {
        appearanceOpen = false;
        if (sessionEvents != null) sessionEvents.SetSelectedGameObject(null);
    }
    private void BuildLobbyCode(Transform canvas)
    {
        var root = Rect("Shared lobby code", canvas, 0, 0, 430, 66);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, 1);
        root.anchoredPosition = new Vector2(0, -14);
        var image = root.gameObject.AddComponent<UnityEngine.UI.Image>();
        ProjectSvinSkin.Bind(image, ProjectSvinRole.Panel);
        image.raycastTarget = false;
        lobbyCode = Label("", 12, 0, 406, 66, 25, root);
        lobbyCode.alignment = TextAlignmentOptions.Center;
        lobbyCode.textWrappingMode = TextWrappingModes.NoWrap;
        var typography = ProjectSvinSkin.Bind(lobbyCode, ProjectSvinRole.Text);
        typography.fontRole = ProjectSvinFontRole.Heading; typography.Apply();
        root.gameObject.SetActive(false);
    }
    private void ShowSettings() { settingsPanel.SetActive(!settingsPanel.activeSelf); if (settingsPanel.activeSelf) { RefreshLanguage(); settingsPanel.transform.SetAsLastSibling(); } }
    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private UnityEngine.UI.Button SessionAction(string text, int row, Action action)
    {
        var button = Instantiate(editableMenu.actionTemplate, editableMenu.sessionActions, false);
        button.name = text; button.gameObject.SetActive(true);
        button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -row * 72);
        GameLanguage.Set(button.GetComponentInChildren<TMP_Text>(), text); button.onClick.AddListener(() => action()); return button;
    }
    private void Resume()
    {
        settingsPanel.SetActive(false);
        lobby.MenuVisible = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }
    private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private TMP_Text Label(string text, float x, float y, float width, float height, float size, Transform parent = null)
    {
        var rect = Rect("Text", parent ?? panel.transform, x, y, width, height);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size;
        GameLanguage.Set(label, text); label.color = Color.white; label.richText = false; label.raycastTarget = false;
        return label;
    }
    private UnityEngine.UI.Button Button(string text, float x, float y, float width, Action action, float height = 50)
    {
        var rect = Rect(text, panel.transform, x, y, width, height);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.16f, .26f, .36f);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var label = Label(text, 12, 5, width - 24, height - 10, 25, rect); label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => action());
        // Session refreshes append controls after the settings panel.
        // Keep the open settings above those controls on every refresh.
        if (settingsPanel != null && settingsPanel.activeSelf) settingsPanel.transform.SetAsLastSibling();
        return button;
    }
    private TMP_InputField Input(string placeholder, float x, float y, float width, string value, int limit)
    {
        var rect = Rect(placeholder, panel.transform, x, y, width, 60);
        rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.1f, .15f, .22f);
        var viewport = Rect("Viewport", rect, 12, 8, width - 24, 44);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var text = Label("", 0, 0, width - 24, 44, 26, viewport);
        var hint = Label(placeholder, 0, 0, width - 24, 44, 26, viewport); hint.color = new Color(.6f, .65f, .7f);
        var input = rect.gameObject.AddComponent<TMP_InputField>(); input.textViewport = viewport;
        input.textComponent = (TextMeshProUGUI)text; input.placeholder = hint; input.characterLimit = limit; input.text = value;
        return input;
    }
#if UNITY_EDITOR || DEBUG
    private void LateUpdate()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null || lobby == null || lobby.InSession || lobby.Busy) return;
        if (keyboard.f8Key.wasPressedThisFrame) lobby.DebugHost();
        if (keyboard.f9Key.wasPressedThisFrame) lobby.DebugJoin();
    }
#endif
}
