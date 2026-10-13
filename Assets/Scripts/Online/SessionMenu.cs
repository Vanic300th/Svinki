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
    private void Start()
    {
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
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
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
        appearanceMenu = gameObject.AddComponent<PigAppearanceMenu>();
        appearanceMenu.Build(canvasObject.transform, font, lobby);
        BuildSettings();
        resultsView = gameObject.AddComponent<OutfitResultsView>();
        resultsView.Build(canvasObject.transform, font, lobby);
        gameObject.AddComponent<OutfitInventoryView>().Build(canvasObject.transform, font, lobby);
        gameObject.AddComponent<CartCargoView>().Build(canvasObject.transform, font, lobby);
        gameObject.AddComponent<PlayerRoundHud>().Build(canvasObject.transform, font, lobby);
        guide=gameObject.AddComponent<HowToPlay>();guide.Build(canvasObject.transform,font,lobby);
        lobby.Changed += Refresh;
        GameLanguage.Changed += RefreshLanguage;
        Refresh();
    }
    private void OnDestroy()
    {
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
        if (panel != null)
        {
            bool showingResults = lobby.Results != null;
            panel.SetActive(!showingResults && !lobby.InventoryOpen && (lobby.MenuVisible || !lobby.InSession && !lobby.Offline));
            appearanceMenu?.SetVisible(panel.activeSelf);
            resultsView?.SetVisible(showingResults);
            if (panel.activeSelf && (lobby.Offline || lobby.Snapshot.phase == SessionPhase.Round)) Refresh();
        }
    }
    private void Refresh()
    {
        if (panel == null) return;
        var snapshot = lobby.Snapshot;
        GameLanguage.Set(title, lobby.Offline ? "SVINKI — Single Player" : lobby.InSession ? "LOBBY  " + snapshot.code : "SVINKI — Play with Friends");
        GameLanguage.Set(status, lobby.Status);
        nickname.gameObject.SetActive(!lobby.InSession && !lobby.Offline && !lobby.Busy);
        code.gameObject.SetActive(nickname.gameObject.activeSelf);
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
            var button = Button(label, 52, 605 + row++ * 60, 795, action); button.interactable = enabled; controls.Add(button.gameObject);
        }
        if (lobby.Busy) { Add("Cancel Connection", lobby.Cancel); return; }
        if (lobby.Offline) { Add("Resume", Resume); Add("Main Menu", lobby.Leave); return; }
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
        }
        else if (snapshot.phase == SessionPhase.Round)
        {
            Add(lobby.IsSpectator ? "Resume Spectating" : "Resume", Resume);
        }
        Add("Copy Code: " + snapshot.code, () => { GUIUtility.systemCopyBuffer = snapshot.code; lobby.Report("Code copied: " + snapshot.code); });
        if (lobby.IsHost) Add(snapshot.closed ? "Open Admission" : "Close Admission", lobby.ToggleAdmission);
        Add(lobby.IsHost ? "End Session" : "Leave Session", lobby.Leave);
        if (lobby.IsHost)
        {
            int n = 0;
            foreach (var player in snapshot.players.Where(p => p.id != snapshot.host))
            {
                string id = player.id;
                var button = Button("× " + player.nickname, 600, 315 + n++ * 44, 245, () => lobby.Kick(id), 38);
                controls.Add(button.gameObject);
            }
        }
    }
    private void BuildSettings()
    {
        var box = Rect("Settings", panel.transform, 52, 250, 795, 530);
        settingsPanel = box.gameObject;
        settingsPanel.AddComponent<UnityEngine.UI.Image>().color = new Color(.07f,.10f,.15f,1);
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
            var choice = language.gameObject.AddComponent<UnityEngine.UI.Button>(); choice.targetGraphic = image;
            Label(name, 10, 5, 330, 42, 24, language).alignment = TextAlignmentOptions.Center;
            choice.onClick.AddListener(() => GameLanguage.Select(code)); return choice;
        }
        englishLanguage = LanguageButton("English",24,"en");
        russianLanguage = LanguageButton("Русский",400,"ru");
        RefreshLanguage();
        settingsPanel.SetActive(false);
    }
    private void RefreshLanguage()
    {
        layoutKey = null; Refresh();
        if (englishLanguage != null) englishLanguage.targetGraphic.color = GameLanguage.Code == "en" ? new Color(.28f,.47f,.5f) : new Color(.16f,.26f,.36f);
        if (russianLanguage != null) russianLanguage.targetGraphic.color = GameLanguage.Code == "ru" ? new Color(.28f,.47f,.5f) : new Color(.16f,.26f,.36f);
        if (settingsPanel != null && settingsPanel.activeSelf) settingsPanel.transform.SetAsLastSibling();
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
