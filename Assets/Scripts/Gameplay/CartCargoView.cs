using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-110)]
public sealed class CartCargoView : MonoBehaviour
{
    public static CartCargoView Instance { get; private set; }
    private static int escapeFrame = -1;
    public static bool ConsumedEscape => escapeFrame == Time.frameCount;
    private NetworkLobby lobby;
    private GameObject panel;
    private CartCargo cargo;
    private TMP_FontAsset font;
    private TMP_Text heading, message;
    private readonly TMP_Text[] names = new TMP_Text[8];
    private readonly UnityEngine.UI.Button[] equip = new UnityEngine.UI.Button[8], unload = new UnityEngine.UI.Button[8], store = new UnityEngine.UI.Button[4];
    private PlayerAvatar Player => PlayerRegistry.Players.FirstOrDefault(p => p != null && p.IsLocal);
    public void Build(Transform canvas, TMP_FontAsset textFont, NetworkLobby session)
    {
        Instance = this; lobby = session; font = textFont;
        panel = Rect("Cart cargo", canvas, 0, 0, 850, 850).gameObject;
        var rect = (RectTransform)panel.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f);
        panel.AddComponent<UnityEngine.UI.Image>().color = new Color(.035f, .055f, .08f, 1);
        var own = panel.AddComponent<Canvas>(); own.overrideSorting = true; own.sortingOrder = 120;
        panel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        heading = Label("CART", 30, 20, 590, 50, 30);
        Button("Close", 670, 25, 150, 45, Close);
        Label("Store worn clothes", 30, 83, 780, 35, 22);
        for (int i = 0; i < 4; i++) { int slot = i; store[i] = Button(OutfitRatingCatalog.SlotName((ClothingSlot)i), 30 + i * 198, 127, 180, 46, () => Transfer(CargoAction.StoreWorn, slot)); }
        Button("Load nearest floor item", 30, 188, 365, 46, LoadNearest);
        Label("G / Esc — close · Items stay in this cart", 410, 190, 410, 45, 18);
        for (int i = 0; i < 8; i++)
        {
            int index = i;
            names[i] = Label("", 30, 260 + i * 61, 450, 48, 22);
            equip[i] = Button("Wear", 510, 260 + i * 61, 135, 44, () => Transfer(CargoAction.Equip, index));
            unload[i] = Button("Unload", 660, 260 + i * 61, 160, 44, () => Transfer(CargoAction.Unload, index));
        }
        message = Label("", 30, 770, 790, 55, 20); panel.SetActive(false);
    }
    public void Open(CartCargo target)
    {
        if (target == null || lobby == null || lobby.InventoryOpen || lobby.MenuVisible || !target.CanAccess(Player)) return;
        cargo = target; lobby.CargoOpen = true; panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    public void Close()
    {
        if (lobby != null) lobby.CargoOpen = false;
        if (panel != null) panel.SetActive(false); cargo = null;
        bool free = lobby == null || lobby.MenuVisible || lobby.Results != null;
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = free;
    }
    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        if (cargo == null || !lobby.CargoOpen || lobby.Results != null) { Close(); return; }
        if (!cargo.CanAccess(Player) || lobby.MenuVisible || lobby.IsSpectator || Keyboard.current?.gKey.wasPressedThisFrame == true || Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        { if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) escapeFrame = Time.frameCount; Close(); return; }
        heading.text = "CART STORAGE  " + cargo.Items.Count + " / " + CartCargo.Capacity;
        for (int i = 0; i < 8; i++)
        {
            var clothing = i < cargo.Items.Count ? cargo.Items[i] : null;
            names[i].text = (i + 1) + ". " + (clothing != null ? clothing.DisplayName : "Empty");
            equip[i].interactable = clothing != null && Player.Outfit.CanEquip(clothing);
            unload[i].interactable = clothing != null;
        }
        for (int i = 0; i < 4; i++) store[i].interactable = cargo.Items.Count < CartCargo.Capacity && Player.Outfit.Has((ClothingSlot)i);
    }
    private void LoadNearest()
    {
        if (cargo == null || Player == null) return;
        var pickup = ClothingPickup.All.Where(p => p != null && p.IsOnFloor && p.gameObject.scene == cargo.gameObject.scene &&
            Vector3.Distance(p.transform.position, cargo.transform.position) < 4 && Vector3.Distance(p.transform.position, Player.Position) < 4)
            .OrderBy(p => (p.transform.position - Player.Position).sqrMagnitude).FirstOrDefault();
        if (pickup == null) { message.text = "No floor clothes nearby. Move the cart closer."; return; }
        Transfer(CargoAction.LoadPickup, 0, pickup);
    }
    private void Transfer(CargoAction action, int index, ClothingPickup pickup = null)
    {
        if (cargo == null || Player == null) return;
        var network = Player.GetComponent<NetworkPlayer>();
        if (network != null) { network.RequestCargo(cargo, action, index, pickup); message.text = "Request sent"; }
        else message.text = cargo.Transfer(Player, action, index, cargo.Revision, pickup) ? "Done" : "Could not transfer. Check space, clothing slot and distance.";
    }
    private RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
    }
    private TMP_Text Label(string text, float x, float y, float w, float h, float size)
    {
        var label = Rect(text, panel.transform, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.fontSize = size; label.text = text; label.richText = false; label.color = Color.white; label.raycastTarget = false; return label;
    }
    private UnityEngine.UI.Button Button(string text, float x, float y, float w, float h, Action action)
    {
        var rect = Rect(text, panel.transform, x, y, w, h); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.18f, .28f, .35f);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var label = Rect("Label", rect, 0, 0, w, h).gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = 21;
        label.text = text; label.color = Color.white; label.raycastTarget = false; label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => action()); return button;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
