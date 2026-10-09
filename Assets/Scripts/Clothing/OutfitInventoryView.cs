using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public sealed class OutfitInventoryView : MonoBehaviour
{
    private NetworkLobby lobby;
    private TMP_FontAsset font;
    private GameObject panel;
    private TMP_Text[] names = new TMP_Text[4];
    private UnityEngine.UI.Button[] drop = new UnityEngine.UI.Button[4];
    private UnityEngine.UI.Button dropAll;
    private TMP_Text state, finishHint;
    private static int escapeFrame = -1;
    public static bool ConsumedEscape => escapeFrame == Time.frameCount;
    private PlayerAvatar Player => PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal);
    public void Build(Transform canvas, TMP_FontAsset menuFont, NetworkLobby session)
    {
        lobby = session; font = menuFont;
        panel = new GameObject("Outfit Inventory", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvas, false);
        var root = (RectTransform)panel.transform; root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(820, 690);
        var ownCanvas = panel.AddComponent<Canvas>(); ownCanvas.overrideSorting = true; ownCanvas.sortingOrder = 115;
        panel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.035f, .055f, .08f, 1);
        Label("YOUR OUTFIT", 35, 28, 580, 50, 34);
        Button("Close", 640, 30, 145, 46, () => SetOpen(false));
        Label("Dropped clothes can be picked up again", 35, 91, 750, 52, 23);
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            Label(OutfitRatingCatalog.SlotName((ClothingSlot)i).ToUpperInvariant(), 35, 173 + i * 88, 450, 26, 18).color = new Color(.55f, .72f, .82f);
            names[i] = Label("", 35, 200 + i * 88, 500, 43, 25);
            drop[i] = Button("Drop", 560, 184 + i * 88, 225, 51, () => Drop((ClothingSlot)index));
        }
        state = Label("", 35, 533, 750, 44, 23);
        dropAll = Button("Drop All", 35, 592, 300, 52, () => { for (int i = 0; i < 4; i++) Drop((ClothingSlot)i); });
        Label("Tab / I — inventory · Esc — close", 355, 606, 430, 38, 20);
        panel.SetActive(false);
        var hint = Rect("Finish readiness", canvas, 0, 0, 760, 100);
        hint.anchorMin = hint.anchorMax = new Vector2(.5f, 0); hint.pivot = new Vector2(.5f, 0); hint.anchoredPosition = new Vector2(0, 35);
        var hintImage = hint.gameObject.AddComponent<UnityEngine.UI.Image>(); hintImage.color = new Color(.035f, .055f, .08f, .92f); hintImage.raycastTarget = false;
        var hintText = Rect("Finish status text", hint, 0, 0, 0, 0);
        hintText.anchorMin = Vector2.zero; hintText.anchorMax = Vector2.one; hintText.sizeDelta = Vector2.zero;
        finishHint = Text(hintText, "", 25); finishHint.alignment = TextAlignmentOptions.Center;
        finishHint.rectTransform.offsetMin = new Vector2(16, 10); finishHint.rectTransform.offsetMax = new Vector2(-16, -10);
    }
    private void Update()
    {
        if (lobby == null || panel == null) return;
        bool playing = lobby.Results == null && !lobby.IsSpectator && Player != null && (lobby.Offline || lobby.Snapshot.phase == SessionPhase.Round);
        var keys = Keyboard.current;
        if (lobby.InventoryOpen && (!playing || lobby.MenuVisible)) SetOpen(false);
        if (playing && !lobby.MenuVisible && !lobby.CargoOpen && !HowToPlay.BlocksInput && !PhotoAlbum.BlocksInput && !PlayerChat.BlocksInput && !EmoteWheel.BlocksInput && (keys?.tabKey.wasPressedThisFrame == true || keys?.iKey.wasPressedThisFrame == true)) SetOpen(!lobby.InventoryOpen);
        if (lobby.InventoryOpen && keys?.escapeKey.wasPressedThisFrame == true) { escapeFrame = Time.frameCount; SetOpen(false); }
        panel.SetActive(lobby.InventoryOpen && playing);
        if (panel.activeSelf)
        {
            var outfit = Player.Outfit;
            for (int i = 0; i < 4; i++)
            { var clothing = outfit.Get((ClothingSlot)i); names[i].text = clothing != null ? clothing.DisplayName : "Empty"; drop[i].interactable = clothing != null; }
            state.text = outfit.IsEmpty ? "No clothes equipped" : "Equipped: " + outfit.Count + " / 4";
            dropAll.interactable = !outfit.IsEmpty;
        }
        finishHint.transform.parent.gameObject.SetActive(playing && !lobby.MenuVisible && !lobby.InventoryOpen && lobby.NearFinish);
        if (finishHint.transform.parent.gameObject.activeSelf)
        {
            var players = lobby.Snapshot.players.Where(p => !p.spectator && !p.eliminated).ToArray();
            finishHint.text = lobby.Offline ? "Press E at the Ready button to rate your outfit" :
                "Ready at the entrance: " + players.Count(p => p.finishReady) + " / " + players.Length + "\n" +
                (lobby.IsFinishReady ? "You're ready. Waiting for others • Stay near the entrance" : "Look at the Ready button and press E");
        }
    }
    public void SetOpen(bool value)
    {
        if (lobby == null) return;
        if (value && (lobby.Results != null || lobby.MenuVisible || Player == null || lobby.IsSpectator)) return;
        lobby.InventoryOpen = value; panel.SetActive(value);
        bool free = value || lobby.MenuVisible || lobby.Results != null || !lobby.Offline && lobby.Snapshot.phase != SessionPhase.Round;
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = free;
    }
    private void Drop(ClothingSlot slot)
    {
        var player = Player;
        if (player == null || lobby.Results != null || !lobby.InventoryOpen) return;
        var network = player.GetComponent<NetworkPlayer>();
        if (!lobby.Offline && network != null) network.RequestDropClothing(slot);
        else if (!ClothingDropper.TryDrop(player, slot) && player.Outfit.Has(slot)) lobby.Report("Could not drop the item here. Try moving away from the wall.");
    }
    private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }
    private TMP_Text Text(RectTransform rect, string text, float size)
    {
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size;
        label.text = text; label.color = Color.white; label.richText = false; label.raycastTarget = false;
        return label;
    }
    private TMP_Text Label(string text, float x, float y, float width, float height, float size) => Text(Rect(text, panel.transform, x, y, width, height), text, size);
    private UnityEngine.UI.Button Button(string text, float x, float y, float width, float height, Action action)
    {
        var rect = Rect(text, panel.transform, x, y, width, height);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.19f, .27f, .34f);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var label = Text(Rect("Label", rect, 10, 4, width - 20, height - 8), text, 23); label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => action()); return button;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => escapeFrame = -1;
}
