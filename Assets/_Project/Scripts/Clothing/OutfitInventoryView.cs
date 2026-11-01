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
    private readonly UnityEngine.UI.RawImage[] pictures = new UnityEngine.UI.RawImage[4];
    private readonly UnityEngine.UI.Image[] customPictures = new UnityEngine.UI.Image[4];
    private readonly ClothingSlotGlyph[] emptySlots = new ClothingSlotGlyph[4];
    private readonly ClothingDefinition[] shown = new ClothingDefinition[4];
    private readonly RenderTexture[] thumbnails = new RenderTexture[4];
    private ClothingIconRenderer iconRenderer;
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
        root.sizeDelta = new Vector2(1240, 540);
        var ownCanvas = panel.AddComponent<Canvas>(); ownCanvas.overrideSorting = true; ownCanvas.sortingOrder = 115;
        panel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.035f, .055f, .08f, 1);
        Label("YOUR OUTFIT", 35, 28, 580, 50, 34);
        Button("Close", 1060, 30, 145, 46, () => SetOpen(false));
        Label("Dropped clothes can be picked up again", 35, 86, 790, 42, 23);
        state = Label("", 850, 90, 355, 34, 22); state.alignment = TextAlignmentOptions.MidlineRight;
        var row = Rect("Clothing cards", panel.transform, 35, 143, 1170, 275);
        var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 18; layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var card = Rect(((ClothingSlot)i) + " clothing card", row, 0, 0, 279, 275);
            var surface = card.gameObject.AddComponent<UnityEngine.UI.Image>(); surface.raycastTarget = false;
            ProjectSvinSkin.Bind(surface, ProjectSvinRole.Track); ProjectSvinSkin.Outline(surface);
            if (ProjectSvinTheme.Current != null) { surface.sprite = ProjectSvinTheme.Current.panelShape; surface.type = UnityEngine.UI.Image.Type.Sliced; }
            var picture = Rect("Clothing picture", card, 18, 14, 243, 202);
            pictures[i] = picture.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            pictures[i].raycastTarget = false; pictures[i].gameObject.SetActive(false);
            var custom = Rect("Custom clothing icon", card, 18, 14, 243, 202);
            customPictures[i] = custom.gameObject.AddComponent<UnityEngine.UI.Image>();
            customPictures[i].preserveAspect = true; customPictures[i].raycastTarget = false; custom.gameObject.SetActive(false);
            var empty = Rect("Empty slot silhouette", card, 78, 56, 123, 123);
            emptySlots[i] = empty.gameObject.AddComponent<ClothingSlotGlyph>(); emptySlots[i].slot = (ClothingSlot)i;
            emptySlots[i].color = ProjectSvinTheme.Current != null ? ProjectSvinTheme.Current.muted * new Color(1, 1, 1, .35f) : new Color(1, 1, 1, .25f);
            emptySlots[i].raycastTarget = false;
            drop[i] = Button("Drop", 16, 226, 247, 43, () => Drop((ClothingSlot)index), card);
        }
        dropAll = Button("Drop All", 35, 450, 300, 54, () => { for (int i = 0; i < 4; i++) Drop((ClothingSlot)i); });
        Label("Tab / I — inventory · Esc — close", 625, 460, 580, 38, 20).alignment = TextAlignmentOptions.MidlineRight;
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
            { var clothing = outfit.Get((ClothingSlot)i); RefreshPicture(i, clothing); drop[i].interactable = clothing != null; }
            GameLanguage.Set(state, outfit.IsEmpty ? "No clothes equipped" : "Equipped: " + outfit.Count + " / 4");
            dropAll.interactable = !outfit.IsEmpty;
        }
        finishHint.transform.parent.gameObject.SetActive(playing && !lobby.MenuVisible && !lobby.InventoryOpen && lobby.NearFinish);
        if (finishHint.transform.parent.gameObject.activeSelf)
        {
            var players = lobby.Snapshot.players.Where(p => !p.spectator && !p.eliminated).ToArray();
            GameLanguage.Set(finishHint, lobby.Offline ? "Stay in the lift to finish the run" :
                "In return lift: " + players.Count(p => p.finishReady) + " / " + players.Length + "\nStay inside · Leaving cancels departure");
        }
    }
    private void RefreshPicture(int index, ClothingDefinition clothing)
    {
        if (shown[index] == clothing) return;
        shown[index] = clothing;
        pictures[index].gameObject.SetActive(false); customPictures[index].gameObject.SetActive(false);
        emptySlots[index].gameObject.SetActive(true);
        if (clothing == null) return;
        if (clothing.InventoryIcon != null)
        {
            customPictures[index].sprite = clothing.InventoryIcon;
            customPictures[index].gameObject.SetActive(true); emptySlots[index].gameObject.SetActive(false); return;
        }
        if (iconRenderer == null) iconRenderer = new ClothingIconRenderer(transform);
        if (thumbnails[index] == null)
        {
            thumbnails[index] = new RenderTexture(384, 320, 24, RenderTextureFormat.ARGB32) { name = "Inventory " + (ClothingSlot)index };
            thumbnails[index].Create(); pictures[index].texture = thumbnails[index];
        }
        bool rendered = iconRenderer.Render(clothing, thumbnails[index]);
        pictures[index].gameObject.SetActive(rendered); emptySlots[index].gameObject.SetActive(!rendered);
    }
    private void OnDestroy()
    {
        iconRenderer?.Dispose();
        foreach (var texture in thumbnails) if (texture != null) { texture.Release(); Destroy(texture); }
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
        GameLanguage.Set(label, text); label.color = Color.white; label.richText = false; label.raycastTarget = false;
        return label;
    }
    private TMP_Text Label(string text, float x, float y, float width, float height, float size) => Text(Rect(text, panel.transform, x, y, width, height), text, size);
    private UnityEngine.UI.Button Button(string text, float x, float y, float width, float height, Action action, Transform parent = null)
    {
        var rect = Rect(text, parent != null ? parent : panel.transform, x, y, width, height);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.19f, .27f, .34f);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var label = Text(Rect("Label", rect, 10, 4, width - 20, height - 8), text, 23); label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => action()); return button;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => escapeFrame = -1;
}
