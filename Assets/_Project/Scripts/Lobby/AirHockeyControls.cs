using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Local table view. Only normalized paddle targets are sent to the host.</summary>
[DefaultExecutionOrder(450)]
public sealed class AirHockeyControls : MonoBehaviour
{
    private static AirHockeyControls instance;
    private static int escapeFrame = -1;
    public static bool ConsumedEscape => escapeFrame == Time.frameCount;
    private AirHockeyTable table;
    private PlayerAvatar player;
    private Camera eye;
    private GrayboxFirstPersonCamera look;
    private bool lookEnabled, cursorVisible, cargoOpen;
    private CursorLockMode cursorLock;
    private float oldFov, openedAt, nextSend;
    private int side, openedFrame, oldMask;
    private GameObject hud;
    private TMP_Text caption;

    public static void Open(AirHockeyTable target, PlayerAvatar owner)
    {
        if (target == null || owner == null || !target.CanPlayFrom(owner)) return;
        if (instance == null)
        {
            instance = new GameObject("Air hockey mouse controls").AddComponent<AirHockeyControls>();
            DontDestroyOnLoad(instance.gameObject);
        }
        instance.Begin(target, owner);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; escapeFrame = -1; }

    private void Begin(AirHockeyTable target, PlayerAvatar owner)
    {
        Close();
        eye = owner.EyeCamera;
        if (eye == null) return;
        table = target; player = owner; side = table.PreferredSide(owner);
        look = eye.GetComponent<GrayboxFirstPersonCamera>();
        lookEnabled = look != null && look.enabled;
        cursorLock = Cursor.lockState; cursorVisible = Cursor.visible; oldFov = eye.fieldOfView; oldMask = eye.cullingMask;
        int bodyLayer = LayerMask.NameToLayer("FirstPersonBody");
        if (bodyLayer >= 0) eye.cullingMask &= ~(1 << bodyLayer);
        cargoOpen = NetworkLobby.Instance != null && NetworkLobby.Instance.CargoOpen;
        if (NetworkLobby.Instance != null) NetworkLobby.Instance.CargoOpen = true;
        if (look != null) look.enabled = false;
        Cursor.lockState = CursorLockMode.Confined; Cursor.visible = true;
        openedFrame = Time.frameCount;
        openedAt = Time.unscaledTime; nextSend = openedAt + .12f;
        BuildHud(); hud.SetActive(true);
        target.Request(owner, LobbyAction.Toggle, index: side);
    }

    private void Update()
    {
        if (table == null) { if (player != null) Close(); return; }
        var lobby = NetworkLobby.Instance;
        if (player == null || !player.IsAlive || player.GetComponent<PlayerKnockdown>()?.IsDown == true ||
            player.gameObject.scene != table.gameObject.scene || !table.CanPlayFrom(player) ||
            Vector3.Distance(player.Position, table.transform.position) > 6 ||
            lobby != null && (lobby.MenuVisible || lobby.Results != null)) { Close(); return; }
        if (Time.frameCount == openedFrame) return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Keyboard.current?.eKey.wasPressedThisFrame == true)
        { escapeFrame = Time.frameCount; Close(); return; }
        int seat = table.Seat(player);
        if (seat < 0)
        {
            if (Time.unscaledTime - openedAt > 2f) { GameHud.Find(player)?.ShowNotification("Both paddles are occupied", 2); Close(); }
            return;
        }
        side = seat;
        string help = GameLanguage.Code == "ru" ? "Води мышью — двигать биту · E / Esc — выйти" : "Move mouse — move paddle · E / Esc — leave";
        caption.text = table.Status + "\n" + help;
        if (Mouse.current == null || Time.unscaledTime < nextSend) return;
        var ray = eye.ScreenPointToRay(Mouse.current.position.ReadValue());
        var plane = new Plane(table.transform.up, table.transform.TransformPoint(new Vector3(0, 1.15f, 0)));
        if (!plane.Raycast(ray, out float distance)) return;
        Vector2 point = table.EncodePointer(table.transform.InverseTransformPoint(ray.GetPoint(distance)), side);
        table.Request(player, LobbyAction.Stroke, points: new[] { point });
        nextSend = Time.unscaledTime + .04f;
    }

    private void LateUpdate()
    {
        if (table == null || eye == null) return;
        // Keep the whole surface visible without covering it with a separate interaction window.
        int bodyLayer = LayerMask.NameToLayer("FirstPersonBody");
        if (bodyLayer >= 0) eye.cullingMask &= ~(1 << bodyLayer);
        var center = table.transform.TransformPoint(new Vector3(0, 1.1f, 0));
        var up = side == 0 ? table.transform.forward : -table.transform.forward;
        eye.fieldOfView = Mathf.Clamp(2 * Mathf.Atan(3.2f / 3.4f) * Mathf.Rad2Deg, 75, 95);
        eye.transform.SetPositionAndRotation(center + table.transform.up * 3.4f,
            Quaternion.LookRotation(-table.transform.up, up));
    }

    public void Close()
    {
        if (player == null && table == null) return;
        if (table != null && player != null) table.Request(player, LobbyAction.Reset);
        if (eye != null) { eye.fieldOfView = oldFov; eye.cullingMask = oldMask; }
        if (look != null) look.enabled = lookEnabled;
        if (NetworkLobby.Instance != null) NetworkLobby.Instance.CargoOpen = cargoOpen;
        Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        table = null; player = null; eye = null;
        if (hud != null) hud.SetActive(false);
    }
    private void OnDisable() => Close();
    private void BuildHud()
    {
        if (hud != null) return;
        hud = new GameObject("Air hockey HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        hud.transform.SetParent(transform, false);
        var canvas = hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 170;
        var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        var go = new GameObject("Hockey help", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(hud.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, 20); rect.sizeDelta = new Vector2(980, 110);
        go.GetComponent<UnityEngine.UI.Image>().color = new Color(.24f, .15f, .2f, .38f);
        go.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        var label = new GameObject("Hockey score and controls", typeof(RectTransform), typeof(TextMeshProUGUI));
        var r = label.GetComponent<RectTransform>(); r.SetParent(rect, false); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(12, 8); r.offsetMax = new Vector2(-12, -8);
        caption = label.GetComponent<TextMeshProUGUI>(); caption.font = ProjectSvinTheme.Current?.bodyFont ?? GameLanguage.Font;
        caption.fontSize = 25; caption.alignment = TextAlignmentOptions.Center; caption.raycastTarget = false; caption.color = Color.white;
    }
}
