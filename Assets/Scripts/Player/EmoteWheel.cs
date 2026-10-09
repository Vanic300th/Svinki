using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum PigEmote : byte { None, Wave, Cheer, Clap, Dance, Laugh, Shrug }

[DefaultExecutionOrder(-300)]
public sealed class EmoteWheel : MonoBehaviour
{
    public static EmoteWheel Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.panel != null && Instance.panel.activeSelf;
    public static bool BlocksInput => IsOpen || dismissedFrame == Time.frameCount;
    public static bool ConsumedEscape => escapeFrame == Time.frameCount;
    private static int dismissedFrame = -1, escapeFrame = -1;
    private GameObject panel;
    private RectTransform wheel;
    private EmoteWheelWedge[] wedges;
    private TMP_Text center, hint;
    private PlayerAvatar actor;
    private int selection = -1;
    private CursorLockMode oldLock;
    private bool oldVisible;
    private static readonly string[] Names = { "Wave", "Cheer", "Clap", "Dance", "Laugh", "Shrug" };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; dismissedFrame = escapeFrame = -1; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("Emote Wheel").AddComponent<EmoteWheel>(); }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
    }
    private PlayerAvatar LocalPlayer()
    {
        foreach (var player in PlayerRegistry.Players)
            if (player != null && player.IsLocal && player.isActiveAndEnabled) return player;
        return null;
    }
    private bool CanOpen(PlayerAvatar player)
    {
        var lobby = NetworkLobby.Instance;
        return !HowToPlay.BlocksInput && !PhotoAlbum.BlocksInput && PigMotion.CanEmote(player) && !PlayerChat.BlocksInput && (lobby == null ||
            lobby.Results == null && !lobby.MenuVisible && !lobby.InventoryOpen && !lobby.CargoOpen && !lobby.IsSpectator &&
            (lobby.Offline || lobby.Snapshot.phase == SessionPhase.Round));
    }
    private void Start() { Build(); }
    private void Update()
    {
        if (panel == null) return;
        var keys = Keyboard.current; var mouse = Mouse.current;
        if (!IsOpen)
        {
            var player = LocalPlayer(); bool can = CanOpen(player);
            hint.gameObject.SetActive(can);
            if (can && keys?.vKey.wasPressedThisFrame == true)
            {
                actor = player; oldLock = Cursor.lockState; oldVisible = Cursor.visible;
                panel.SetActive(true); selection = -1; Paint();
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                mouse?.WarpCursorPosition(new Vector2(Screen.width * .5f, Screen.height * .5f));
            }
            return;
        }
        hint.gameObject.SetActive(false);
        if (!CanOpen(actor) || keys == null) { Close(false); return; }
        if (keys.escapeKey.wasPressedThisFrame || mouse?.rightButton.wasPressedThisFrame == true)
        { if (keys.escapeKey.wasPressedThisFrame) escapeFrame = Time.frameCount; Close(false); return; }
        if (mouse != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(wheel, mouse.position.ReadValue(), null, out var point))
        {
            int next = point.magnitude < 100 ? -1 : Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg + 30, 360) / 60);
            if (next != selection) { selection = next; Paint(); }
        }
        if (keys.vKey.wasReleasedThisFrame) Close(true);
    }
    private void Close(bool perform)
    {
        var player = actor; int chosen = selection;
        panel.SetActive(false); actor = null; dismissedFrame = Time.frameCount;
        var lobby = NetworkLobby.Instance;
        bool menu = lobby != null && (lobby.MenuVisible || lobby.Results != null);
        Cursor.lockState = menu ? CursorLockMode.None : oldLock; Cursor.visible = menu || oldVisible;
        if (!perform || chosen < 0 || !CanOpen(player)) return;
        var kind = (PigEmote)(chosen + 1); var network = player.GetComponent<NetworkPlayer>();
        if (network != null) network.RequestEmote(kind); else player.GetComponentInChildren<PigMotion>()?.PlayEmote(kind);
    }
    private void Paint()
    {
        for (int i = 0; i < wedges.Length; i++) wedges[i].color = i == selection ? new Color(1,.40f,.32f,.96f) : new Color(.075f,.18f,.21f,.96f);
        center.text = selection < 0 ? "EMOTES\n<size=19>Move to choose</size>" : Names[selection].ToUpperInvariant() + "\n<size=19>Release V</size>";
    }
    private RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private TMP_Text Text(string name, Transform parent, string value, Vector2 size, Vector2 position, int fontSize)
    {
        var rect = Rect(name, parent, size, position); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = PlayerChat.Instance.Font; text.text = value; text.fontSize = fontSize; text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
    }
    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 130; gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        var background = Rect("Wheel overlay", transform, Vector2.zero, Vector2.zero);
        background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
        background.gameObject.AddComponent<Image>().color = new Color(0,0,0,.4f); panel = background.gameObject;
        wheel = Rect("Wheel", background, new Vector2(500,500), Vector2.zero); wedges = new EmoteWheelWedge[6];
        for (int i = 0; i < 6; i++)
        {
            var rect = Rect(Names[i] + " sector", wheel, new Vector2(500,500), Vector2.zero);
            var wedge = rect.gameObject.AddComponent<EmoteWheelWedge>(); wedge.Index = i; wedge.raycastTarget = false; wedges[i] = wedge;
        }
        for (int i = 0; i < 6; i++)
        {
            float angle = (90 - i * 60) * Mathf.Deg2Rad;
            Text(Names[i], wheel, "<size=18>0" + (i+1) + "</size>\n" + Names[i], new Vector2(150,80), new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 177, 29);
        }
        center = Text("Selected emote", wheel, "", new Vector2(192,110), Vector2.zero, 27);
        Text("Help", wheel, "Release V to perform  ·  Esc to cancel", new Vector2(650,50), new Vector2(0,-296), 22);
        hint = Text("Emotes shortcut", transform, "HOLD V · EMOTES", new Vector2(300,38), new Vector2(172,75), 19);
        var hintRect = hint.GetComponent<RectTransform>(); hintRect.anchorMin = hintRect.anchorMax = Vector2.zero;
        Paint(); panel.SetActive(false); hint.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        if (Instance != this) return;
        if (IsOpen) Close(false);
        Instance = null;
    }
}
