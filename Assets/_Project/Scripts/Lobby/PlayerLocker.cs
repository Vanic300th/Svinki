using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

public sealed class PlayerLocker : LobbyInteractable
{
    [SerializeField, Range(0, 5)] private int slot;
    [SerializeField] private Transform door;
    [SerializeField] private TMP_Text nameplate;
    private readonly SyncVar<string> owner = new SyncVar<string>("");
    private readonly SyncVar<string> ownerName = new SyncVar<string>("UNASSIGNED");
    private readonly SyncVar<bool> open = new SyncVar<bool>();
    private string offlineOwner = "", offlineName = "UNASSIGNED";
    private bool offlineOpen;
    private float nextPhotoChange;
    private LockerPhotoSlot[] photos;
    public int Slot => slot;
    public string OwnerId => NetworkLobby.Instance?.Offline == true ? offlineOwner : owner.Value;
    public bool IsMine(PlayerAvatar player) => player != null && OwnerId == Identity(player);
    private void Awake() => photos = GetComponentsInChildren<LockerPhotoSlot>(true);
    public override void OnStartClient() { base.OnStartClient(); if (Slot == 0) NetworkLobby.Instance?.RequestLockerPhotos(); }
    public void Assign(string id, string nickname)
    {
        if (!Authority) return;
        if (NetworkLobby.Instance?.Offline == true) { offlineOwner = id; offlineName = nickname; }
        else { if (owner.Value != id) { owner.Value = id; open.Value = false; } if (ownerName.Value != nickname) ownerName.Value = nickname; }
    }
    public override string Prompt(PlayerAvatar player) => IsMine(player) ? "E — your locker · door / photo slots" : "Locker: " + (NetworkLobby.Instance?.Offline == true ? offlineName : ownerName.Value);
    public bool IsOpen => NetworkLobby.Instance?.Offline == true ? offlineOpen : open.Value;
    public override void Interact(PlayerAvatar player)
    {
        if (!IsMine(player)) return;
        if (!IsOpen) Request(player, LobbyAction.Toggle);
        LobbyPanel.Open(this, player);
    }
    public override void Apply(PlayerAvatar player, LobbyAction action, string text, int index, Vector2[] points)
    {
        if (!IsMine(player)) return;
        if (action == LobbyAction.WearMemory)
        {
            var ids = (text ?? "").Split('|');
            var online = player.GetComponent<NetworkPlayer>();
            if (online != null) online.WearLobbyMemory(ids);
            else if (ids.Length <= 4)
            {
                var rules = OutfitRatingCatalog.Load(); var items = new System.Collections.Generic.List<ClothingDefinition>();
                foreach (var id in ids) { var clothing = rules?.FindClothing(id); if (clothing != null) items.Add(clothing); }
                player.Outfit.SetItems(items);
            }
            return;
        }
        if (action == LobbyAction.Toggle) { if (NetworkLobby.Instance?.Offline == true) offlineOpen = !offlineOpen; else open.Value = !open.Value; }
        else if (Time.unscaledTime < nextPhotoChange) return;
        else if (action == LobbyAction.AssignPhoto && int.TryParse(text, out int photo) && index >= 0 && index < photos.Length)
            { nextPhotoChange = Time.unscaledTime + 1; NetworkLobby.Instance?.PinLockerPhoto(OwnerId, index, photo); }
        else if (action == LobbyAction.ClearPhoto && index >= 0 && index < photos.Length) { nextPhotoChange = Time.unscaledTime + 1; NetworkLobby.Instance?.PinLockerPhoto(OwnerId, index, -1); }
    }
    public override void LobbyTick(float delta)
    {
        if (nameplate != null) nameplate.text = NetworkLobby.Instance?.Offline == true ? offlineName : ownerName.Value;
        bool shown = NetworkLobby.Instance?.Offline == true ? offlineOpen : open.Value;
        if (door != null) door.localRotation = Quaternion.Slerp(door.localRotation, Quaternion.Euler(0, shown ? -105 : 0, 0), delta * 8);
        foreach (var photo in photos) photo.Show(NetworkLobby.Instance?.LockerPhoto(OwnerId, photo.Slot));
    }
}
