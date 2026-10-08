using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Shows the captured end-of-round outfits rather than the players' live inventory.</summary>
public sealed class OutfitResultsView : MonoBehaviour
{
    private NetworkLobby lobby;
    private FinalRunwayView runway;
    private TMP_FontAsset font;
    private GameObject panel, studio, model;
    private Camera cameraPreview;
    private RenderTexture texture;
    private TMP_Text heading, playerName, score, verdict, strength, improvement, clothes, continueLabel;
    private UnityEngine.UI.Button next;
    private readonly List<UnityEngine.UI.Button> players = new List<UnityEngine.UI.Button>();
    private readonly List<UnityEngine.UI.Image> bars = new List<UnityEngine.UI.Image>();
    private readonly List<TMP_Text> values = new List<TMP_Text>();
    private OutfitRoundResults shown;
    private float yaw, nextRender;

    public void Build(Transform canvas, TMP_FontAsset menuFont, NetworkLobby session)
    {
        lobby = session; font = menuFont;
        panel = new GameObject("Outfit Results", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvas, false);
        var resultCanvas = panel.AddComponent<Canvas>(); resultCanvas.overrideSorting = true; resultCanvas.sortingOrder = 120;
        panel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var root = (RectTransform)panel.transform;
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(1560, 920);
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.035f, .05f, .075f, 1);
        heading = Label("OUTFIT RESULTS", 36, 30, 1488, 52, 36);
        Label("See how well your clothes work together", 36, 90, 1488, 40, 24).color = new Color(.67f, .75f, .83f);
        Label("PLAYERS", 36, 145, 300, 36, 22);
        for (int i = 0; i < 6; i++)
        {
            int index = i;
            players.Add(Button("", 36, 196 + i * 88, 300, 76, () => Select(index)));
        }
        var portrait = Rect("Outfit portrait", panel.transform, 365, 145, 450, 530);
        var raw = portrait.gameObject.AddComponent<UnityEngine.UI.RawImage>(); raw.raycastTarget = false;
        CreateStudio(raw);
        Button("Left", 365, 627, 110, 44, () => Rotate(-30));
        Button("Right", 705, 627, 110, 44, () => Rotate(30));
        clothes = Label("", 365, 696, 450, 110, 22);
        playerName = Label("", 850, 145, 670, 40, 27);
        score = Label("", 850, 188, 265, 84, 65);
        verdict = Label("", 1125, 207, 390, 60, 29);
        Metric("Compatibility", 290);
        Metric("Colours", 344);
        Metric("Patterns", 398);
        Metric("Outfit completeness", 452);
        Label("WHAT WORKS", 850, 534, 670, 32, 20).color = new Color(.5f, .82f, .7f);
        strength = Label("", 850, 574, 670, 95, 25);
        Label("WHAT TO TRY", 850, 685, 670, 32, 20).color = new Color(.92f, .76f, .48f);
        improvement = Label("", 850, 725, 670, 90, 23);
        next = Button("Next Round", 850, 840, 420, 48, lobby.ContinueAfterResults);
        continueLabel = next.GetComponentInChildren<TMP_Text>();
        Button("Main Menu", 1290, 840, 234, 48, lobby.Leave);
        runway = gameObject.AddComponent<FinalRunwayView>(); runway.Build(panel.transform, font, lobby);
        panel.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (panel == null) return;
        panel.SetActive(visible);
        if (!visible) { shown = null; runway.Hide(); return; }
        var results = lobby.Results;
        if (results != null && (shown == null || results.round != shown.round || results.entries.Length != shown.entries.Length))
        {
            shown = results;
            heading.text = "OUTFIT RESULTS · ROUND " + results.round;
            for (int i = 0; i < players.Count; i++)
            {
                bool exists = i < results.entries.Length;
                players[i].gameObject.SetActive(exists);
                if (exists) players[i].GetComponentInChildren<TMP_Text>().text =
                    (i + 1) + ". " + results.entries[i].nickname + "\n" + (results.entries[i].eliminated ? "Ghost · eliminated" : results.entries[i].rating.score + " / 100");
            }
            Select(0);
            runway.Begin(results);
        }
        shown = results; runway.Tick(results);
        next.interactable = !runway.Showing && (lobby.Offline || lobby.IsHost);
        continueLabel.text = lobby.Offline ? lobby.IsEliminated ? "Next Round" : "Keep Searching" : lobby.IsHost ? "Next Round" : "Waiting for host";
        if (Time.unscaledTime >= nextRender && cameraPreview != null)
        { nextRender = Time.unscaledTime + 1f / 15; cameraPreview.Render(); }
    }

    private void Select(int index)
    {
        if (shown == null || index >= shown.entries.Length)
        {
            playerName.text = "No players with clothes yet";
            score.text = "— / 100"; verdict.text = ""; strength.text = ""; improvement.text = ""; clothes.text = "";
            ClearModel(); return;
        }
        var entry = shown.entries[index]; var rating = entry.rating;
        playerName.text = entry.nickname + (entry.eliminated ? " · ghost" : "");
        score.text = rating.score + " / 100"; verdict.text = rating.verdict;
        strength.text = rating.strength; improvement.text = rating.improvement;
        clothes.text = entry.eliminated ? "Eliminated · original pig appearance" : entry.names.Length == 0 ? "No clothes found yet" : string.Join("\n", entry.names);
        int[] measures = { rating.matching, rating.palette, rating.patterns, rating.completeness };
        for (int i = 0; i < measures.Length; i++)
        { bars[i].rectTransform.sizeDelta = new Vector2(670 * measures[i] / 100f, 8); values[i].text = measures[i] + "%"; }
        for (int i = 0; i < players.Count; i++) players[i].GetComponent<UnityEngine.UI.Image>().color =
            i == index ? new Color(.20f, .36f, .43f) : new Color(.10f, .16f, .22f);
        ClearModel();
        var prefab = Resources.Load<GameObject>("Pigs/PigAvatar");
        if (prefab == null) return;
        model = Instantiate(prefab, studio.transform);
        model.name = "Presented outfit";
        var motion = model.GetComponent<PigMotion>(); if (motion != null) motion.enabled = false;
        var appearance = model.GetComponent<PigAppearance>();
        appearance?.Apply(entry.appearance);
        var rules = OutfitRatingCatalog.Load();
        foreach (string asset in entry.clothing)
        {
            var definition = rules?.FindClothing(asset); if (definition == null) continue;
            var holder = new GameObject(definition.DisplayName);
            if (!PigClothingBinding.TryAttach(definition, appearance.ModelRoot, holder.transform)) Destroy(holder);
        }
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 30;
        if (entry.eliminated) { model.name = "Presented ghost pig"; model.AddComponent<RunwayGhost>().Apply(); }
        yaw = 0; Rotate(0);
    }
    private void ClearModel() { if (model != null) { model.SetActive(false); Destroy(model); model = null; } }
    private void Rotate(float degrees)
    {
        if (cameraPreview == null) return;
        yaw = Mathf.Repeat(yaw + degrees, 360);
        cameraPreview.transform.localPosition = Quaternion.Euler(0, yaw, 0) * new Vector3(.1f, 1.02f, 3.7f);
        cameraPreview.transform.LookAt(studio.transform.position + Vector3.up * .98f);
        cameraPreview.Render();
    }
    private void CreateStudio(UnityEngine.UI.RawImage image)
    {
        studio = new GameObject("Outfit Presentation Studio"); studio.transform.SetParent(transform, false);
        studio.transform.position = new Vector3(20000, 10000, 10000);
        texture = new RenderTexture(540, 636, 24) { name = "Outfit Presentation" }; texture.Create(); image.texture = texture;
        var cameraObject = new GameObject("Presentation Camera"); cameraObject.transform.SetParent(studio.transform, false);
        cameraPreview = cameraObject.AddComponent<Camera>(); cameraPreview.enabled = false;
        cameraPreview.clearFlags = CameraClearFlags.SolidColor; cameraPreview.backgroundColor = new Color(.17f, .21f, .26f);
        cameraPreview.fieldOfView = 32; cameraPreview.nearClipPlane = .05f; cameraPreview.farClipPlane = 8;
        cameraPreview.cullingMask = 1 << 30; cameraPreview.targetTexture = texture;
        void Light(string name, Vector3 position, float intensity, Color color)
        {
            var obj = new GameObject(name); obj.transform.SetParent(studio.transform, false);
            obj.transform.localPosition = position; obj.transform.LookAt(studio.transform.position + Vector3.up);
            var light = obj.AddComponent<UnityEngine.Light>(); light.type = LightType.Spot;
            light.spotAngle = 95; light.range = 8; light.intensity = intensity; light.color = color; light.cullingMask = 1 << 30;
        }
        Light("Presentation key", new Vector3(-2, 3, 3), 7, new Color(1, .9f, .85f));
        Light("Presentation fill", new Vector3(2, 2, 2), 3, new Color(.75f, .85f, 1));
    }
    private void Metric(string name, float y)
    {
        Label(name, 850, y, 360, 30, 23);
        values.Add(Label("", 1420, y, 100, 30, 23));
        var track = Rect(name + " track", panel.transform, 850, y + 33, 670, 8);
        track.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.12f, .18f, .23f);
        var fill = Rect(name + " fill", track, 0, 0, 670, 8).gameObject.AddComponent<UnityEngine.UI.Image>();
        fill.color = new Color(.39f, .75f, .68f); bars.Add(fill);
    }
    private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }
    private TMP_Text Label(string text, float x, float y, float width, float height, float size)
    { return Label(text, x, y, width, height, size, panel.transform); }
    private TMP_Text Label(string text, float x, float y, float width, float height, float size, Transform parent)
    {
        var rect = Rect(text.Length == 0 ? "Result text" : text, parent, x, y, width, height);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size;
        label.text = text; label.color = Color.white; label.richText = false; label.raycastTarget = false;
        label.enableAutoSizing = true; label.fontSizeMin = size * .78f; label.fontSizeMax = size; return label;
    }
    private UnityEngine.UI.Button Button(string text, float x, float y, float width, float height, Action action)
    {
        var rect = Rect(text.Length == 0 ? "Participant" : text, panel.transform, x, y, width, height);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.10f, .16f, .22f);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var label = Label(text, 12, 5, width - 24, height - 10, 23, rect); label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => action()); return button;
    }
    private void OnDestroy()
    {
        if (studio != null) Destroy(studio);
        if (texture != null) { texture.Release(); Destroy(texture); }
    }
}
