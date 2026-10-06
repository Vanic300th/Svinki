using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Pre-round face editor with a real 3D preview of the modular game prefab.</summary>
public sealed class PigAppearanceMenu : MonoBehaviour
{
    private GameObject panel, previewWorld;
    private PigAppearance preview;
    private Camera previewCamera;
    private RenderTexture texture;
    private NetworkLobby lobby;
    private TMP_FontAsset font;
    private PigFace face;
    private readonly List<UnityEngine.UI.Button> buttons = new List<UnityEngine.UI.Button>();
    private readonly List<Action> refreshLabels = new List<Action>();
    private float nextRender;
    private RectTransform choices;
    private float previewYaw;

    public void Build(Transform canvas, TMP_FontAsset menuFont, NetworkLobby session)
    {
        lobby = session; font = menuFont; face = PigFace.Decode(PigFace.Selected);
        panel = new GameObject("Pig Appearance Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvas, false);
        var root = (RectTransform)panel.transform;
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.sizeDelta = new Vector2(480, 970);
        root.anchoredPosition = new Vector2(462, 0);
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.065f, .085f, .12f, .98f);
        Label("ТВОЯ СВИНЬЯ", 26, 32, 428, 42, 30);
        Label("Сочетай аксессуары, цвета и окрас", 26, 76, 428, 30, 19);
        RectTransform image = Rect("3D Pig Preview", panel.transform, 26, 120, 428, 350);
        var raw = image.gameObject.AddComponent<UnityEngine.UI.RawImage>(); raw.raycastTarget = false;
        CreatePreview(raw);
        Button("Влево", 26, 428, 90, () => RotatePreview(-30));
        Button("Вправо", 364, 428, 90, () => RotatePreview(30));
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            Button(new[] { "Очки и усы", "Борода", "Пирсинг" }[i], 26 + i * 146, 486, 136,
                () => { face = PigFace.Preset(index); Save(); });
        }
        CreateChoices();
        void Choice(Func<string> label, Action change)
        {
            var button = Button(label(), 0, 0, 410, () => { change(); Save(); }, choices);
            var layout = button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = 40;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            refreshLabels.Add(() => text.text = label());
        }
        Choice(() => "Глаза: " + (face.eyes == 0 ? "открытые" : "прищур"), () => face.eyes = (face.eyes + 1) % 2);
        Choice(() => "Брови: " + new[] { "спокойные", "приподнятые", "наглые" }[face.brows], () => face.brows = (face.brows + 1) % 3);
        Choice(() => "Очки: " + new[] { "нет", "круглые", "квадратные", "кошачьи", "авиаторы", "солнечные круглые", "солнечные квадратные" }[face.glassesStyle],
            () => face.glassesStyle = (face.glassesStyle + 1) % PigFace.GlassesCount);
        Choice(() => "Ирокез: " + Yes(face.mohawk), () => face.mohawk = !face.mohawk);
        Choice(() => "Цвет кожи: " + PigAppearance.ColorNames[face.skinColor], () => face.skinColor = (face.skinColor + 1) % PigFace.ColorCount);
        Choice(() => "Окрас: " + PigAppearance.PatternNames[face.pattern], () => face.pattern = (face.pattern + 1) % PigFace.PatternCount);
        Choice(() => "Цвет узора: " + PigAppearance.HairColorNames[face.patternColor], () => face.patternColor = (face.patternColor + 1) % PigFace.ColorCount);
        Choice(() => "Цвет волос: " + PigAppearance.HairColorNames[face.hairColor], () => face.hairColor = (face.hairColor + 1) % PigFace.ColorCount);
        Choice(() => "Усы: " + new[] { "нет", "закрученные", "обычные" }[face.mustache], () => face.mustache = (face.mustache + 1) % 3);
        Choice(() => "Борода: " + Yes(face.beard), () => face.beard = !face.beard);
        Choice(() => "Серьга в ухе: " + Yes(face.earPiercing), () => face.earPiercing = !face.earPiercing);
        Choice(() => "Пирсинг брови: " + Yes(face.browPiercing), () => face.browPiercing = !face.browPiercing);
        Choice(() => "Татуировка: " + PigAppearance.TattooNames[face.tattoo], () => face.tattoo = (face.tattoo + 1) % PigFace.TattooCount);
        Choice(() => "Кольцо в носу: " + Yes(face.nosePiercing), () => face.nosePiercing = !face.nosePiercing);
        Label("Прокручивай список. Выбор сохраняется.", 26, 911, 428, 36, 17);
        if (preview != null) preview.Apply(face.Encode());
    }

    public void SetVisible(bool visible)
    {
        if (panel == null) return;
        panel.SetActive(visible);
        bool editable = !lobby.Busy && !lobby.Offline &&
            (lobby.Snapshot.phase == SessionPhase.Menu || lobby.Snapshot.phase == SessionPhase.Lobby);
        foreach (var button in buttons) button.interactable = editable;
        if (visible && previewCamera != null && Time.unscaledTime >= nextRender)
        { nextRender = Time.unscaledTime + 1f / 15; previewCamera.Render(); }
    }

    public void SelectFace(int code) { face = PigFace.Decode(PigFace.Sanitize(code)); Save(); }

    private static string Yes(bool value) => value ? "да" : "нет";
    private void RotatePreview(float degrees)
    {
        if (previewCamera == null) return;
        previewYaw = Mathf.Repeat(previewYaw + degrees, 360);
        previewCamera.transform.localPosition = Quaternion.Euler(0, previewYaw, 0) * new Vector3(.12f, 1.02f, 3.7f);
        previewCamera.transform.LookAt(previewWorld.transform.position + Vector3.up * .98f);
        previewCamera.Render();
    }
    private void CreateChoices()
    {
        var root = Rect("Appearance Choices", panel.transform, 26, 542, 428, 350);
        var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var viewport = Rect("Viewport", root, 0, 0, 428, 350);
        var image = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(.065f, .085f, .12f);
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
        choices = Rect("Content", viewport, 0, 0, 410, 0);
        choices.anchorMin = new Vector2(0, 1); choices.anchorMax = new Vector2(1, 1);
        choices.sizeDelta = new Vector2(-18, 0);
        var layout = choices.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 8; layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        choices.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = choices; scroll.horizontal = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
        var track = Rect("Scrollbar", root, 416, 0, 12, 350);
        track.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.12f, .16f, .22f);
        var handle = Rect("Handle", track, 0, 0, 12, 100);
        var handleImage = handle.gameObject.AddComponent<UnityEngine.UI.Image>(); handleImage.color = new Color(.42f, .63f, .72f);
        var scrollbar = track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
        scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.sizeDelta = Vector2.zero;
        scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
        scroll.verticalScrollbar = scrollbar;
    }
    private void Save()
    {
        lobby.SelectAppearance(face.Encode());
        if (preview != null) preview.Apply(face.Encode());
        foreach (Action refresh in refreshLabels) refresh();
    }

    private void CreatePreview(UnityEngine.UI.RawImage image)
    {
        GameObject prefab = Resources.Load<GameObject>("Pigs/PigAvatar");
        if (prefab == null) { Debug.LogWarning("PigAvatar preview prefab is missing."); return; }
        previewWorld = new GameObject("Pig Preview Studio"); previewWorld.transform.SetParent(transform, false);
        previewWorld.transform.position = new Vector3(10000, 10000, 10000);
        GameObject model = Instantiate(prefab, previewWorld.transform);
        preview = model.GetComponent<PigAppearance>();
        // The preview occupies a reserved layer, with its own lights and camera.
        const int layer = 31;
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
        texture = new RenderTexture(768, 640, 24) { name = "Pig Appearance Preview" }; texture.Create(); image.texture = texture;
        GameObject cameraObject = new GameObject("Pig Preview Camera"); cameraObject.transform.SetParent(previewWorld.transform, false);
        previewCamera = cameraObject.AddComponent<Camera>(); previewCamera.enabled = false;
        cameraObject.transform.localPosition = new Vector3(.12f, 1.02f, 3.7f);
        cameraObject.transform.LookAt(previewWorld.transform.position + Vector3.up * .98f);
        previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(.20f, .24f, .30f);
        previewCamera.fieldOfView = 32; previewCamera.nearClipPlane = .05f; previewCamera.farClipPlane = 8;
        previewCamera.cullingMask = 1 << layer; previewCamera.targetTexture = texture;
        void Light(string name, Vector3 position, float intensity, Color color)
        {
            GameObject obj = new GameObject(name); obj.transform.SetParent(previewWorld.transform, false);
            obj.transform.localPosition = position; obj.transform.LookAt(previewWorld.transform.position + Vector3.up);
            var light = obj.AddComponent<UnityEngine.Light>(); light.type = LightType.Spot;
            light.spotAngle = 95; light.range = 8; light.intensity = intensity; light.color = color; light.cullingMask = 1 << layer;
        }
        Light("Pig Preview Key", new Vector3(-2, 3, 3), 7, new Color(1, .90f, .85f));
        Light("Pig Preview Fill", new Vector3(2, 2, 2), 3, new Color(.75f, .85f, 1));
    }

    private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var r = (RectTransform)obj.transform; r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height); return r;
    }
    private TMP_Text Label(string text, float x, float y, float width, float height, float size, Transform parent = null)
    {
        var r = Rect(text, parent ?? panel.transform, x, y, width, height);
        var label = r.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size;
        label.text = text; label.color = Color.white; label.richText = false; label.raycastTarget = false; return label;
    }
    private UnityEngine.UI.Button Button(string text, float x, float y, float width, Action action, Transform parent = null)
    {
        var r = Rect(text, parent ?? panel.transform, x, y, width, 40);
        var image = r.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.16f, .24f, .32f);
        var button = r.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var label = Label(text, 10, 3, width - 20, 34, 20, r); label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => action()); buttons.Add(button); return button;
    }
    private void OnDestroy()
    {
        if (previewWorld != null) Destroy(previewWorld);
        if (texture != null) { texture.Release(); Destroy(texture); }
    }
}
