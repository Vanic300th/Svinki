using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-600)]
public sealed class LockerPhotoViewer : MonoBehaviour
{
    private static LockerPhotoViewer instance;
    private static int escapeFrame = -1;
    private GameObject panel;
    private RectTransform window;
    private bool full, oldCargo, oldVisible;
    private CursorLockMode oldLock;
    private UnityEngine.UI.RawImage photo;
    public static bool IsOpen => instance != null && instance.panel != null;
    public static bool ConsumedEscape => escapeFrame == Time.frameCount;
    public static void Open(Texture image)
    {
        if (image == null) return;
        if (instance == null) instance = new GameObject("Locker photo viewer").AddComponent<LockerPhotoViewer>();
        instance.Show(image);
    }
    private void Show(Texture image)
    {
        Close(); oldLock = Cursor.lockState; oldVisible = Cursor.visible;
        oldCargo = NetworkLobby.Instance?.CargoOpen == true;
        if (NetworkLobby.Instance != null) NetworkLobby.Instance.CargoOpen = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        panel = new GameObject("Enlarged locker photo", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        panel.transform.SetParent(transform, false);
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 190;
        var scaler = panel.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        window = Rect("Photo window", panel.transform, new Vector2(1050, 800), Vector2.zero);
        window.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.018f, .03f, .045f, .98f);
        var frame = Rect("Photo area", window, Vector2.zero, Vector2.zero);
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.offsetMin = new Vector2(30, 85); frame.offsetMax = new Vector2(-30, -30);
        var inner = Rect("Photo", frame, Vector2.zero, Vector2.zero);
        photo = inner.gameObject.AddComponent<UnityEngine.UI.RawImage>(); photo.texture = image; photo.raycastTarget = false;
        var fitter = inner.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>(); fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)image.width / image.height;
        Button("FULL SCREEN / WINDOW", new Vector2(-200, 32), () => { full = !full; Resize(); });
        Button("CLOSE · ESC", new Vector2(240, 32), Close);
        full = false; Resize();
    }
    private void Resize()
    {
        window.anchorMin = full ? Vector2.zero : Vector2.one * .5f; window.anchorMax = full ? Vector2.one : Vector2.one * .5f;
        window.offsetMin = full ? Vector2.zero : new Vector2(-525, -400); window.offsetMax = full ? Vector2.zero : new Vector2(525, 400);
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 at)
    { var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = at; return rect; }
    private void Button(string title, Vector2 at, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(title, window, new Vector2(370, 50), at); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.14f, .26f, .3f);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        var text = Rect("Label", rect, Vector2.zero, Vector2.zero); text.anchorMin = Vector2.zero; text.anchorMax = Vector2.one; text.offsetMin = new Vector2(8, 4); text.offsetMax = new Vector2(-8, -4);
        var label = text.gameObject.AddComponent<TextMeshProUGUI>(); label.font = PlayerChat.Instance?.Font ?? TMP_Settings.defaultFontAsset; label.fontSize = 23;
        label.alignment = TextAlignmentOptions.Center; label.color = Color.white; label.raycastTarget = false; GameLanguage.Set(label, title);
    }
    private void Update()
    {
        if (panel == null) return;
        if (photo.texture == null) { Close(); return; }
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) { escapeFrame = Time.frameCount; Close(); }
    }
    private void Close()
    {
        if (panel == null) return;
        Destroy(panel); panel = null;
        if (NetworkLobby.Instance != null) NetworkLobby.Instance.CargoOpen = oldCargo;
        Cursor.lockState = oldLock; Cursor.visible = oldVisible;
    }
    private void OnDestroy() { Close(); if (instance == this) instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() { instance = null; escapeFrame = -1; }
}
