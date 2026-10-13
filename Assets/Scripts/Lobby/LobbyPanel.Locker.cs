using System.Linq;
using TMPro;
using UnityEngine;

public sealed partial class LobbyPanel
{
    private UnityEngine.UI.RawImage[] lockerThumbnails;
    private UnityEngine.UI.Image[] lockerBorders;
    private TMP_Text[] lockerCaptions;
    private UnityEngine.UI.Button assignPhoto, viewPinned, wearMemory;
    private TMP_Text memoryLabel;
    private int chosenSlot, memoryIndex;
    private void BuildLocker(PlayerLocker locker)
    {
        content.sizeDelta = new Vector2(1080, 920); chosenSlot = 0; memoryIndex = 0;
        var title = content.Find(locker.name.ToUpperInvariant()) as RectTransform;
        if (title != null) { title.anchoredPosition = new Vector2(0, 400); title.sizeDelta = new Vector2(900, 50); }
        var close = panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "CLOSE · ESC");
        close.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -410);
        var door = Button("OPEN / CLOSE DOOR", new Vector2(250, 325), () => locker.Request(player, LobbyAction.Toggle));
        door.GetComponent<RectTransform>().sizeDelta = new Vector2(450, 52);
        preview = Rect("Selected archive photo", content, new Vector2(360, 240), new Vector2(-310, 205)).gameObject.AddComponent<UnityEngine.UI.RawImage>();
        preview.raycastTarget = true; var previewButton = preview.gameObject.AddComponent<UnityEngine.UI.Button>();
        previewButton.targetGraphic = preview; previewButton.onClick.AddListener(() => LockerPhotoViewer.Open(preview.texture));
        var prev = Button("PREVIOUS PHOTO", new Vector2(-435, 40), () => photo = Mathf.Max(0, photo - 1));
        var next = Button("NEXT PHOTO", new Vector2(-185, 40), () => photo = Mathf.Min(Mathf.Max(0, (PhotoAlbum.Instance?.SavedCount ?? 0) - 1), photo + 1));
        ResizeButton(prev, 220, 52); ResizeButton(next, 220, 52);
        status.rectTransform.anchoredPosition = new Vector2(-310, -38); status.rectTransform.sizeDelta = new Vector2(385, 80);
        Label("Click a photo to enlarge it", new Vector2(385, 42), new Vector2(-310, -102), 20);
        // Door, inner door and back wall are shown in their actual left-to-right arrangement.
        var positions = new[] { new Vector2(55, 138), new Vector2(240, 200), new Vector2(240, 35), new Vector2(425, 205), new Vector2(385, 20), new Vector2(475, 20) };
        var sizes = new[] { new Vector2(145, 225), new Vector2(145, 130), new Vector2(145, 130), new Vector2(145, 180), new Vector2(75, 75), new Vector2(75, 75) };
        foreach (float x in new[] { 55f, 240f, 425f })
        {
            var backing = Rect("Locker section", content, new Vector2(165, 375), new Vector2(x, 102));
            backing.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.07f, .105f, .11f);
            backing.SetAsFirstSibling();
        }
        lockerThumbnails = new UnityEngine.UI.RawImage[6]; lockerBorders = new UnityEngine.UI.Image[6]; lockerCaptions = new TMP_Text[6];
        for (int i = 0; i < 6; i++)
        {
            int slot = i; var rect = Rect("Photo slot " + (i + 1), content, sizes[i], positions[i]);
            var border = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); lockerBorders[i] = border;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = border; button.onClick.AddListener(() => chosenSlot = slot);
            var image = Rect("Pinned photo", rect, Vector2.zero, Vector2.zero); image.anchorMin = Vector2.zero; image.anchorMax = Vector2.one;
            image.offsetMin = Vector2.one * 6; image.offsetMax = Vector2.one * -6;
            var raw = image.gameObject.AddComponent<UnityEngine.UI.RawImage>(); raw.raycastTarget = false; lockerThumbnails[i] = raw;
            var label = Label("EMPTY", sizes[i] - Vector2.one * 8, Vector2.zero, i < 4 ? 22 : 16); label.transform.SetParent(rect, false); lockerCaptions[i] = label;
            var clear = Button("×", positions[i] + new Vector2(sizes[i].x * .5f - 15, sizes[i].y * .5f - 15), () => NetworkLobby.Instance?.ClearSavedLockerPhoto(locker, player, slot));
            ResizeButton(clear, 30, 30);
        }
        Label("Choose a slot on the locker", new Vector2(510, 45), new Vector2(250, -130), 22);
        assignPhoto = Button("PLACE SELECTED PHOTO", new Vector2(250, -205), () => NetworkLobby.Instance?.AssignSavedLockerPhoto(locker, player, chosenSlot, photo));
        viewPinned = Button("VIEW PINNED PHOTO", new Vector2(250, -285), () => LockerPhotoViewer.Open(NetworkLobby.Instance?.LockerPhoto(locker.OwnerId, chosenSlot)));
        ResizeButton(assignPhoto, 450, 58); ResizeButton(viewPinned, 450, 58);
        memoryLabel = Label("OUTFIT MEMORIES", new Vector2(400, 92), new Vector2(-310, -207), 22);
        var previousMemory = Button("←", new Vector2(-440, -290), () => memoryIndex = Mathf.Max(0, memoryIndex - 1));
        var nextMemory = Button("→", new Vector2(-180, -290), () => memoryIndex = Mathf.Min(OutfitMemoryStore.Entries.Count - 1, memoryIndex + 1));
        ResizeButton(previousMemory, 110, 48); ResizeButton(nextMemory, 110, 48);
        wearMemory = Button("WEAR OUTFIT", new Vector2(-310, -365), () =>
        {
            if (memoryIndex >= 0 && memoryIndex < OutfitMemoryStore.Entries.Count)
                locker.Request(player, LobbyAction.WearMemory, string.Join("|", OutfitMemoryStore.Entries[memoryIndex].clothing));
        });
        Label("Save your chosen outfits during the runway show", new Vector2(510, 60), new Vector2(250, -350), 21);
    }
    private static void ResizeButton(UnityEngine.UI.Button button, float width, float height)
    {
        var rect = button.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(width, height);
        var text = button.GetComponentInChildren<TMP_Text>().rectTransform; text.anchorMin = Vector2.zero; text.anchorMax = Vector2.one;
        text.offsetMin = new Vector2(5, 3); text.offsetMax = new Vector2(-5, -3);
    }
    private void RefreshLocker(PlayerLocker locker)
    {
        for (int i = 0; i < 6; i++)
        {
            var texture = NetworkLobby.Instance?.LockerPhoto(locker.OwnerId, i);
            lockerThumbnails[i].texture = texture; lockerThumbnails[i].gameObject.SetActive(texture != null);
            lockerCaptions[i].gameObject.SetActive(texture == null);
            lockerBorders[i].color = i == chosenSlot ? new Color(.74f, .54f, .2f) : new Color(.15f, .24f, .25f);
        }
        assignPhoto.interactable = (PhotoAlbum.Instance?.SavedCount ?? 0) > 0;
        viewPinned.interactable = NetworkLobby.Instance?.LockerPhoto(locker.OwnerId, chosenSlot) != null;
        int count = OutfitMemoryStore.Entries.Count; memoryIndex = Mathf.Clamp(memoryIndex, 0, Mathf.Max(0, count - 1));
        wearMemory.interactable = count > 0;
        if (OutfitMemoryStore.Error != null) GameLanguage.Set(memoryLabel, OutfitMemoryStore.Error);
        else if (count == 0) GameLanguage.Set(memoryLabel, "No saved outfits yet");
        else
        {
            var memory = OutfitMemoryStore.Entries[memoryIndex];
            GameLanguage.Set(memoryLabel, "OUTFIT MEMORY " + (memoryIndex + 1) + " / " + count + " · ROUND " + memory.round + "\n" + string.Join(", ", memory.names));
        }
    }
}
