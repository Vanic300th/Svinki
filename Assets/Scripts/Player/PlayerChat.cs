using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

// Shared input UI; messages belong to the avatar and travel through its owned RPC.
[DefaultExecutionOrder(-200)]
public sealed class PlayerChat : MonoBehaviour
{
    public const int MessageLimit = 160;
    public static PlayerChat Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.panel != null && Instance.panel.activeSelf;
    public static bool BlocksInput => IsOpen || dismissedFrame == Time.frameCount;
    public static bool ConsumedEscape => dismissedFrame == Time.frameCount;
    private static int dismissedFrame = -1;
    private TMP_FontAsset font;
    private GameObject panel;
    private TMP_InputField input;
    private PlayerAvatar speaker;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; dismissedFrame = -1; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) new GameObject("Player Chat").AddComponent<PlayerChat>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public TMP_FontAsset Font
    {
        get
        {
            if (font == null)
            {
                var source = NetworkLobby.Instance != null ? NetworkLobby.Instance.MenuFont : null;
                if (source == null) source = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                font = TMP_FontAsset.CreateFontAsset(source);
                font.name = "Chat dynamic font";
            }
            return font;
        }
    }

    private bool CanChat => NetworkLobby.Instance == null ||
        !NetworkLobby.Instance.MenuVisible && !NetworkLobby.Instance.InventoryOpen && (NetworkLobby.Instance.Offline ||
        NetworkLobby.Instance.Snapshot.phase == SessionPhase.Round && !NetworkLobby.Instance.IsSpectator);

    private PlayerAvatar LocalPlayer()
    {
        foreach (var player in PlayerRegistry.Players)
        {
            if (player == null || !player.isActiveAndEnabled || !player.IsLocal) continue;
            var network = player.GetComponent<NetworkPlayer>();
            if (network == null || network.IsClientStarted && network.IsOwner) return player;
        }
        return null;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (IsOpen)
        {
            if (!CanChat || speaker == null || !speaker.isActiveAndEnabled || !speaker.IsLocal)
            { Close(); return; }
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                string message = CleanMessage(input.text);
                if (message.Length > 0)
                {
                    var network = speaker.GetComponent<NetworkPlayer>();
                    if (network != null) network.SendChat(message);
                    else PlayerChatBubble.Show(speaker.gameObject, message);
                }
                Close();
            }
        }
        else if (CanChat && keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            speaker = LocalPlayer();
            if (speaker == null) return;
            if (panel == null) BuildInput();
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            panel.SetActive(true);
            input.SetTextWithoutNotify("");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // The opening Enter must not reach the newly focused input field.
            StartCoroutine(FocusInput());
        }
    }

    private IEnumerator FocusInput()
    {
        yield return null;
        if (!IsOpen) yield break;
        EventSystem.current?.SetSelectedGameObject(input.gameObject);
        input.ActivateInputField();
    }

    private void Close()
    {
        if (!IsOpen) return;
        dismissedFrame = Time.frameCount;
        input.DeactivateInputField();
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == input.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        panel.SetActive(false);
        speaker = null;
        bool menu = NetworkLobby.Instance != null && NetworkLobby.Instance.MenuVisible;
        Cursor.lockState = menu ? CursorLockMode.None : previousCursorLock;
        Cursor.visible = menu || previousCursorVisible;
    }

    public static string CleanMessage(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var result = new StringBuilder(MessageLimit);
        foreach (char c in value)
        {
            if (char.IsControl(c)) continue;
            if (result.Length == MessageLimit) break;
            result.Append(c);
        }
        // Do not leave a half surrogate pair at the length limit.
        if (result.Length > 0 && char.IsHighSurrogate(result[result.Length - 1])) result.Length--;
        return result.ToString().Trim();
    }

    private void BuildInput()
    {
        if (EventSystem.current == null)
        {
            var events = new GameObject("Chat EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }
        var canvas = new GameObject("Chat Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 110;
        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        panel = new GameObject("Chat Input", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvas.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.02f, .025f); rect.anchorMax = new Vector2(.55f, .025f);
        rect.pivot = new Vector2(0, 0); rect.sizeDelta = new Vector2(0, 104);
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.04f, .055f, .085f, .94f);
        var hint = CreateText("Chat Help", panel.transform);
        Stretch(hint.rectTransform, new Vector2(18, 62), new Vector2(-18, -12));
        hint.text = "Chat  ·  Enter — send  ·  Esc — cancel";
        hint.fontSize = 20; hint.color = new Color(.7f, .78f, .87f);
        var field = new GameObject("Message", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        field.transform.SetParent(panel.transform, false);
        Stretch(field.GetComponent<RectTransform>(), new Vector2(12, 12), new Vector2(-12, -44));
        field.GetComponent<UnityEngine.UI.Image>().color = new Color(.1f, .15f, .22f);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
        viewport.transform.SetParent(field.transform, false);
        Stretch(viewport.GetComponent<RectTransform>(), new Vector2(12, 4), new Vector2(-12, -4));
        var text = CreateText("Message Text", viewport.transform);
        var placeholder = CreateText("Placeholder", viewport.transform);
        placeholder.text = "Type a message…"; placeholder.color = new Color(.55f, .62f, .7f);
        input = field.AddComponent<TMP_InputField>();
        input.textViewport = viewport.GetComponent<RectTransform>();
        input.textComponent = text; input.placeholder = placeholder;
        input.targetGraphic = field.GetComponent<UnityEngine.UI.Image>();
        input.characterLimit = MessageLimit; input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.customCaretColor = true; input.caretColor = Color.white;
        panel.SetActive(false);
    }

    private TextMeshProUGUI CreateText(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<TextMeshProUGUI>();
        Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
        text.font = Font; text.fontSize = 26; text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = false; text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = min; rect.offsetMax = max;
    }

    private void OnDisable() { if (Instance == this) Close(); }
    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (font != null) Destroy(font);
    }
}
