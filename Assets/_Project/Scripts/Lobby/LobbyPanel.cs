using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(-400)]
public sealed partial class LobbyPanel : MonoBehaviour
{
    private static LobbyPanel instance;
    private static int escapeFrame = -1;
    public static bool ConsumedEscape => escapeFrame == Time.frameCount;
    private LobbyInteractable target;
    private PlayerAvatar player;
    private GameObject panel;
    private RectTransform content, drawingArea;
    private TMP_Text status;
    private TMP_InputField url;
    private RawImage preview;
    private CursorLockMode oldLock;
    private bool oldVisible, erase;
    private int photo;
    private float nextSend;
    private readonly List<Vector2> pending = new List<Vector2>();
    public static bool IsViewing(LobbyTelevision television) => instance != null && instance.panel != null && instance.target == television;
    public static void Open(LobbyInteractable item, PlayerAvatar owner)
    {
        if (instance == null) { var root = new GameObject("Lobby controls"); instance = root.AddComponent<LobbyPanel>(); DontDestroyOnLoad(root); }
        instance.Show(item, owner);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; escapeFrame = -1; }
    private void Show(LobbyInteractable item, PlayerAvatar owner)
    {
        Close(); target = item; player = owner; photo = 0; erase = false;
        oldLock = Cursor.lockState; oldVisible = Cursor.visible;
        if (NetworkLobby.Instance != null) NetworkLobby.Instance.CargoOpen = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        panel = new GameObject("Social object panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        panel.transform.SetParent(transform, false);
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 165;
        var scaler = panel.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        content = Rect("Window", panel.transform, new Vector2(1080, 850), Vector2.zero);
        content.gameObject.AddComponent<Image>().color = new Color(.035f, .055f, .055f, .98f);
        Label(item.name.ToUpperInvariant(), new Vector2(900, 65), new Vector2(0, 375), 32);
        Button("CLOSE · ESC", new Vector2(0, -370), Close);
        status = Label("", new Vector2(1000, 160), new Vector2(0, 200), 23);
        if (item is LobbyMediaPlayer media)
        {
            if (media is LobbyJukebox) BuildMusicSearch();
            bool tv = media is LobbyTelevision;
            if (tv)
            {
                content.sizeDelta = new Vector2(1080, 1000);
                panel.GetComponentsInChildren<Button>().First(b => b.name == "CLOSE · ESC").GetComponent<RectTransform>().anchoredPosition = new Vector2(0,-435);
                status.rectTransform.anchoredPosition = new Vector2(0,310); status.rectTransform.sizeDelta = new Vector2(1000,80);
                preview = Rect("TV player",content,new Vector2(480,270),new Vector2(0,125)).gameObject.AddComponent<RawImage>();preview.texture = media.LocalPreview;
                preview.raycastTarget=true;preview.gameObject.AddComponent<YouTubePlayerInput>().Television=(LobbyTelevision)media;
            }
            url = Input(tv ? "YouTube link or direct video URL" : "ccMixter track link or direct audio URL", new Vector2(0, tv ? -55 : 50));
            Button(media is LobbyJukebox ? "ADD TO QUEUE" : "PLAY URL", new Vector2(0, tv ? -130 : -25), () =>
            {
                if (!MediaSourceProvider.TryResolve(url.text, media.Kind, out _)) { mediaInputError = tv ? "Use a YouTube or compatible HTTP/HTTPS video URL" : "Use a ccMixter track page or direct HTTP/HTTPS audio URL"; return; }
                mediaInputError = null;
                item.Request(player, LobbyAction.Load, url.text);
            });
            Button("PLAY / PAUSE", new Vector2(-280, tv ? -210 : -105), () => item.Request(player, LobbyAction.PlayPause));
            Button(media is LobbyJukebox ? "SKIP" : "STOP", new Vector2(0, tv ? -210 : -105), () => item.Request(player, media is LobbyJukebox ? LobbyAction.Skip : LobbyAction.Stop));
            if (media is LobbyTelevision)
                Button("SEEK +10s", new Vector2(280, -210), () => item.Request(player, LobbyAction.Seek, index: Mathf.FloorToInt((float)media.Position) + 10));
            Button("VOLUME −", new Vector2(-280, tv ? -295 : -210), () => media.LocalVolume -= .1f);
            Button("MUTE", new Vector2(0, tv ? -295 : -210), () => media.LocalVolume = 0);
            Button("VOLUME +", new Vector2(280, tv ? -295 : -210), () => media.LocalVolume += .1f);
            var help = Label(tv ? "YouTube plays while the screen is visible · local volume" : "Music continues after closing this menu · local volume", new Vector2(950, 40), new Vector2(0, tv ? -365 : -275), 20);
            if(media is LobbyJukebox)musicAttribution=help;
        }
        else if (item is PlayerLocker locker)
        {
            BuildLocker(locker);
        }
        else if (item is DrawingBoard board)
        {
            status.rectTransform.anchoredPosition = new Vector2(0, 290); status.rectTransform.sizeDelta = new Vector2(950, 45);
            drawingArea = Rect("Drawing surface", content, new Vector2(840, 480), new Vector2(0, 10));
            preview = drawingArea.gameObject.AddComponent<RawImage>(); preview.texture = board.Texture; preview.raycastTarget = true;
            Button("DRAW", new Vector2(-280, -280), () => erase = false);
            Button("ERASE", new Vector2(0, -280), () => erase = true);
            var clear = Button("CLEAR · HOST", new Vector2(280, -280), () => item.Request(player, LobbyAction.ClearBoard));
            clear.interactable = NetworkLobby.Instance?.Offline == true || NetworkLobby.Instance?.IsHost == true;
        }
        else if (item is AirHockeyTable hockey)
        {
            status.rectTransform.anchoredPosition = new Vector2(0, 290);
            drawingArea = Rect("Paddle control", content, new Vector2(600, 450), new Vector2(0, -30));
            drawingArea.gameObject.AddComponent<Image>().color = new Color(.12f, .27f, .3f);
            Label("Hold LMB and move to control your paddle", new Vector2(900, 40), new Vector2(0, -285), 22);
            item.Request(player, LobbyAction.Toggle);
        }
    }
    private void Update()
    {
        if (panel == null) return;
        if (LockerPhotoViewer.IsOpen || LockerPhotoViewer.ConsumedEscape) return;
        var lobby = NetworkLobby.Instance;
        if (target == null || player == null || !player.IsAlive || lobby != null && (lobby.Results != null || lobby.MenuVisible) ||
            Vector3.Distance(player.Position, target.transform.position) > 6) { Close(); return; }
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) { escapeFrame = Time.frameCount; Close(); return; }
        if (target is LobbyMediaPlayer media)
        {
            string queue = media is LobbyJukebox box ? "\nQUEUE\n" + string.Join("\n", box.Queue.Take(3).Select((t, i) => (i + 1) + ". " + t.Title + " — " + t.Player)) + (box.Queue.Count() > 3 ? "\n… " + (box.Queue.Count() - 3) + " more queued" : "") : "";
            status.text = (media.State.Title ?? "No media") + " — " + (media.State.AddedBy ?? "") + "\n" + FormatTime(media.Position) + " / " + FormatTime(media.State.Duration) +
                " · volume " + Mathf.RoundToInt(media.LocalVolume * 100) + "%" + (mediaInputError != null ? "\n" + mediaInputError : media.LocalError != null ? "\n" + media.LocalError : queue);
            if (preview != null) preview.texture = media.LocalPreview;
            if(musicAttribution!=null)musicAttribution.text=media.LocalCatalogTrack!=null ? "ccMixter · "+media.LocalCatalogTrack.Credit+" · "+media.LocalCatalogTrack.LicenseName : "Music continues after closing this menu · local volume";
        }
        else if (target is PlayerLocker locker)
        {
            int count = PhotoAlbum.Instance?.SavedCount ?? 0; photo = Mathf.Clamp(photo, 0, Mathf.Max(0, count - 1));
            preview.texture = count > 0 ? PhotoAlbum.Instance.SavedPhoto(photo) : null;
            GameLanguage.Set(status, PhotoAlbum.Instance?.PersistenceError ?? (count == 0 ? "No saved photos yet. K — camera · LMB — shutter" : "SAVED PHOTO " + (photo + 1) + " / " + count));
            RefreshLocker(locker);
        }
        else if (target is DrawingBoard board)
        {
            status.text = (erase ? "ERASE" : "DRAW") + " · " + board.CommandCount + " commands · hold LMB";
            ReadPointer();
        }
        else if (target is AirHockeyTable hockey) { status.text = hockey.Status; ReadPointer(); }
    }
    private void ReadPointer()
    {
        if (Mouse.current == null || drawingArea == null) return;
        if (Mouse.current.leftButton.isPressed && RectTransformUtility.ScreenPointToLocalPointInRectangle(drawingArea, Mouse.current.position.ReadValue(), null, out var point))
        {
            var rect = drawingArea.rect;
            Vector2 normalized = new Vector2((point.x - rect.xMin) / rect.width, (point.y - rect.yMin) / rect.height);
            if (normalized.x >= 0 && normalized.x <= 1 && normalized.y >= 0 && normalized.y <= 1 &&
                (pending.Count == 0 || Vector2.Distance(pending[pending.Count - 1], normalized) > .004f)) pending.Add(normalized);
            if (pending.Count >= 24 || Time.unscaledTime >= nextSend) FlushPoints();
        }
        else if (Time.unscaledTime >= nextSend) FlushPoints();
    }
    private void FlushPoints()
    {
        if (pending.Count == 0) return;
        target.Request(player, LobbyAction.Stroke, erase ? "erase" : "draw", erase ? 10 : 3, pending.ToArray());
        pending.Clear(); nextSend = Time.unscaledTime + .12f;
    }
    private void Close()
    {
        if (panel == null) return;
        if (target is AirHockeyTable && player != null) target.Request(player, LobbyAction.Reset);
        Destroy(panel); panel = null; target = null; player = null; preview = null; drawingArea = null; pending.Clear();
        CancelMusicSearch(); mediaInputError = null;
        if (NetworkLobby.Instance != null) NetworkLobby.Instance.CargoOpen = false;
        Cursor.lockState = oldLock; Cursor.visible = oldVisible;
    }
    private static string FormatTime(double value) => !double.IsFinite(value) || value < 0 ? "00:00" : ((int)value / 60).ToString("00") + ":" + ((int)value % 60).ToString("00");
    private RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private TMP_Text Label(string text, Vector2 size, Vector2 position, int points)
    {
        var rect = Rect(text, content, size, position); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = PlayerChat.Instance != null ? PlayerChat.Instance.Font : TMP_Settings.defaultFontAsset;
        label.richText = false; GameLanguage.Set(label, text); label.fontSize = points; label.color = Color.white; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; return label;
    }
    private Button Button(string title, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(title, content, new Vector2(275, 58), position); var background = rect.gameObject.AddComponent<Image>(); background.color = new Color(.15f, .32f, .3f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = background; button.onClick.AddListener(action);
        var label = Label(title, new Vector2(265, 52), Vector2.zero, 20); label.transform.SetParent(rect, false); return button;
    }
    private TMP_InputField Input(string placeholder, Vector2 position)
    {
        var rect = Rect("URL", content, new Vector2(950, 58), position); rect.gameObject.AddComponent<Image>().color = new Color(.12f, .17f, .17f);
        var input = rect.gameObject.AddComponent<TMP_InputField>(); input.characterLimit = 2048;
        var text = Label("", new Vector2(910, 50), Vector2.zero, 23); text.transform.SetParent(rect, false); text.alignment = TextAlignmentOptions.MidlineLeft;
        var hint = Label(placeholder, new Vector2(910, 50), Vector2.zero, 23); hint.transform.SetParent(rect, false); hint.color = Color.gray;
        input.textViewport = rect; input.textComponent = (TextMeshProUGUI)text; input.placeholder = hint; return input;
    }
    private void OnDestroy() { Close(); if (instance == this) instance = null; }
}
