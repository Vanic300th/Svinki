using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>A local stage driven by the shared server result snapshot; inventories stay untouched.</summary>
public sealed class FinalRunwayView : MonoBehaviour
{
    public const float SecondsPerPlayer = 8;
    private NetworkLobby lobby;
    private GameObject panel, stage, pig;
    private TMP_FontAsset font;
    private TMP_Text nameLabel, progress, reaction, poseLabel;
    private UnityEngine.UI.Button poseButton;
    private RenderTexture texture;
    private Camera cameraStage;
    private OutfitRoundResults results;
    private int index = -1, localPose, seenReaction;
    private float start, nextRender, reactionUntil, poseUntil;
    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
    private readonly List<Material> materials = new List<Material>();
    public bool Showing => panel != null && panel.activeSelf;
    public int PresentedIndex => index;

    public void Build(Transform parent, TMP_FontAsset menuFont, NetworkLobby session)
    {
        lobby = session; font = menuFont;
        panel = new GameObject("Final Runway", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(parent, false);
        var root = (RectTransform)panel.transform; root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.025f, .03f, .055f, 1);
        Label("PIGS ON THE RUNWAY", 60, 24, 1440, 60, 40).alignment = TextAlignmentOptions.Center;
        nameLabel = Label("", 80, 96, 1400, 42, 29); nameLabel.alignment = TextAlignmentOptions.Center;
        var portrait = Rect("Runway stage", 70, 157, 1420, 570);
        var raw = portrait.gameObject.AddComponent<UnityEngine.UI.RawImage>(); raw.raycastTarget = false;
        CreateStage(raw);
        progress = Label("", 90, 740, 1000, 40, 23);
        reaction = Label("", 90, 785, 1000, 36, 25); reaction.color = new Color(1, .72f, .46f);
        poseButton = Button("Pose", 90, 841, 220, () =>
        {
            if (results == null || index < 0) return;
            localPose = (localPose + 1) % 3; poseUntil = Time.unscaledTime + 2;
            lobby.RunwayPose(localPose);
        });
        poseLabel = poseButton.GetComponentInChildren<TMP_Text>();
        Button("Applaud", 330, 841, 270, () => React("clap"));
        Button("Heart", 620, 841, 230, () => React("heart"));
        Button("Oink!", 870, 841, 190, () => React("oink"));
        Button("View Scores →", 1120, 841, 360, Skip);
        panel.SetActive(false);
    }
    public void Begin(OutfitRoundResults snapshot)
    {
        results = snapshot; start = Time.unscaledTime; index = -1; localPose = 0;
        seenReaction = lobby.Snapshot.runwayReactionSequence; reactionUntil = 0; reaction.text = "";
        panel.SetActive(snapshot != null && snapshot.entries.Length > 0);
        if (Showing) Tick(snapshot);
    }
    public void Hide() { if (panel != null) panel.SetActive(false); ClearPig(); results = null; }
    public void Skip() { if (panel != null) panel.SetActive(false); ClearPig(); }
    public void Tick(OutfitRoundResults snapshot)
    {
        if (!Showing || snapshot == null) return;
        results = snapshot;
        float elapsed = Time.unscaledTime - start;
        int next = Mathf.FloorToInt(elapsed / SecondsPerPlayer);
        if (next >= results.entries.Length) { Skip(); return; }
        if (next != index) { index = next; Present(results.entries[index]); }
        var entry = results.entries[index];
        bool own = lobby.Offline || entry.id == lobby.Identity;
        poseButton.interactable = own;
        poseLabel.text = own ? "Pose: " + (entry.presentationPose == 1 ? "Wave" : entry.presentationPose == 2 ? "Star" : "Spin") : "Player's pose";
        progress.text = "Walk " + (index + 1) + " of " + results.entries.Length + " · " + Mathf.CeilToInt(SecondsPerPlayer - elapsed % SecondsPerPlayer) + (index + 1 == results.entries.Length ? "s until scores" : "s until next");
        if (lobby.Snapshot.runwayReactionSequence != seenReaction)
        {
            seenReaction = lobby.Snapshot.runwayReactionSequence;
            reaction.text = lobby.Snapshot.runwayReaction; reactionUntil = Time.unscaledTime + 3;
        }
        if (Time.unscaledTime > reactionUntil) reaction.text = "";
        Animate(elapsed % SecondsPerPlayer, Time.unscaledTime < poseUntil && own ? localPose : entry.presentationPose);
        if (Time.unscaledTime >= nextRender)
        { nextRender = Time.unscaledTime + 1f / 25; cameraStage.Render(); }
    }
    private void React(string kind)
    {
        lobby.RunwayReact(kind);
        if (lobby.Offline) { reaction.text = lobby.Nickname + ": " + NetworkLobby.ReactionText(kind); reactionUntil = Time.unscaledTime + 3; }
    }
    private void Present(OutfitResultEntry entry)
    {
        ClearPig(); localPose = 0;
        nameLabel.text = entry.nickname + " · outfit on show";
        var prefab = Resources.Load<GameObject>("Pigs/PigAvatar"); if (prefab == null) return;
        pig = Instantiate(prefab, stage.transform); pig.name = "Runway Pig";
        var motion = pig.GetComponent<PigMotion>(); if (motion != null) motion.enabled = false;
        var appearance = pig.GetComponent<PigAppearance>(); appearance.Apply(entry.appearance);
        foreach (var bone in appearance.ModelRoot.GetComponentsInChildren<Transform>(true))
        { if (!bones.ContainsKey(bone.name)) bones.Add(bone.name, bone); rest[bone] = bone.localRotation; }
        var rules = OutfitRatingCatalog.Load();
        foreach (string id in entry.clothing)
        {
            var definition = rules?.FindClothing(id); if (definition == null) continue;
            var holder = new GameObject(definition.DisplayName);
            if (!PigClothingBinding.TryAttach(definition, appearance.ModelRoot, holder.transform)) Destroy(holder);
        }
        foreach (Transform part in pig.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 30;
    }
    private void Animate(float time, int pose)
    {
        if (pig == null) return;
        foreach (var pair in rest) if (pair.Key != null) pair.Key.localRotation = pair.Value;
        bool walking = time < 2 || time > 6;
        float z = time < 2 ? Mathf.SmoothStep(-2.7f, .5f, time / 2) : time > 6 ? Mathf.SmoothStep(.5f, -2.7f, (time - 6) / 2) : .5f;
        pig.transform.localPosition = new Vector3(0, walking ? Mathf.Abs(Mathf.Sin(time * 9)) * .025f : .008f * Mathf.Sin(time * 3), z);
        pig.transform.localRotation = Quaternion.Euler(0, time > 6 ? 180 : time < 2 ? 0 : pose == 0 ? (time - 2) * 90 : 0, 0);
        if (walking)
        {
            float step = Mathf.Sin(time * 9) * 22;
            Bone("Thigh_L", new Vector3(step, 0, 0)); Bone("Thigh_R", new Vector3(-step, 0, 0));
            Bone("UpperArm_L", new Vector3(-step * .7f, 0, 0)); Bone("UpperArm_R", new Vector3(step * .7f, 0, 0));
        }
        else if (pose == 1)
        { Bone("UpperArm_R", new Vector3(-35, 0, -105)); Bone("Forearm_R", new Vector3(0, 0, Mathf.Sin(time * 10) * 25)); }
        else if (pose == 2)
        { Bone("UpperArm_L", new Vector3(0, 0, 110)); Bone("UpperArm_R", new Vector3(0, 0, -110)); Bone("Head", new Vector3(0, 0, Mathf.Sin(time * 3) * 9)); }
    }
    private void Bone(string name, Vector3 rotation)
    { if (bones.TryGetValue(name, out var bone)) bone.localRotation = rest[bone] * Quaternion.Euler(rotation); }
    private void ClearPig()
    { bones.Clear(); rest.Clear(); if (pig != null) { pig.SetActive(false); Destroy(pig); pig = null; } }
    private void CreateStage(UnityEngine.UI.RawImage image)
    {
        stage = new GameObject("Final Runway Studio"); stage.transform.SetParent(transform, false);
        stage.transform.position = new Vector3(21000, 10000, 10000);
        texture = new RenderTexture(1420, 570, 24) { name = "Runway Preview" }; texture.Create(); image.texture = texture;
        var cameraObject = new GameObject("Runway Camera"); cameraObject.transform.SetParent(stage.transform, false);
        cameraStage = cameraObject.AddComponent<Camera>(); cameraStage.enabled = false; cameraStage.cullingMask = 1 << 30;
        cameraStage.targetTexture = texture; cameraStage.clearFlags = CameraClearFlags.SolidColor;
        cameraStage.backgroundColor = new Color(.035f, .04f, .07f); cameraStage.nearClipPlane = .05f; cameraStage.farClipPlane = 20;
        cameraStage.fieldOfView = 29; cameraStage.transform.localPosition = new Vector3(2.5f, 1.85f, 5.2f);
        cameraStage.transform.LookAt(stage.transform.position + new Vector3(0, .85f, -.3f));
        var floor = Material(new Color(.14f, .16f, .20f)); var gold = Material(new Color(1, .64f, .22f), true);
        var pink = Material(new Color(.85f, .25f, .48f), true);
        Part("Catwalk", new Vector3(0, -.12f, -.9f), new Vector3(2.4f, .22f, 6), floor);
        Part("Backdrop", new Vector3(0, 1.4f, -4), new Vector3(6, 3, .15f), Material(new Color(.075f, .055f, .105f)));
        Part("Left strip", new Vector3(-1.16f, .005f, -.9f), new Vector3(.045f, .035f, 6), gold);
        Part("Right strip", new Vector3(1.16f, .005f, -.9f), new Vector3(.045f, .035f, 6), pink);
        for (int i = 0; i < 7; i++)
        {
            Part("Backdrop bar " + i, new Vector3((i - 3) * .7f, 1.4f, -3.9f), new Vector3(.06f, 1.7f + .4f * (i % 2), .05f), i % 2 == 0 ? pink : gold);
        }
        Light("Stage key", new Vector3(-2, 3.7f, 3.8f), 18, new Color(1, .86f, .73f));
        Light("Stage fill", new Vector3(2.5f, 3, 2), 10, new Color(.72f, .83f, 1));
        Light("Stage rim", new Vector3(0, 3, -3), 13, new Color(1, .28f, .49f));
    }
    private Material Material(Color color, bool glowing = false)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color;
        if (glowing) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 2); }
        materials.Add(material); return material;
    }
    private void Part(string name, Vector3 position, Vector3 size, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.SetParent(stage.transform, false);
        obj.transform.localPosition = position; obj.transform.localScale = size; obj.layer = 30;
        Destroy(obj.GetComponent<Collider>()); obj.GetComponent<Renderer>().sharedMaterial = material;
    }
    private void Light(string name, Vector3 position, float intensity, Color color)
    {
        var obj = new GameObject(name); obj.transform.SetParent(stage.transform, false); obj.transform.localPosition = position;
        obj.transform.LookAt(stage.transform.position + new Vector3(0, .8f, -.5f));
        var light = obj.AddComponent<UnityEngine.Light>(); light.type = LightType.Spot; light.spotAngle = 90;
        light.range = 12; light.intensity = intensity; light.color = color; light.cullingMask = 1 << 30;
    }
    private RectTransform Rect(string name, float x, float y, float width, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(panel.transform, false);
        var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }
    private TMP_Text Label(string text, float x, float y, float width, float height, float size)
    {
        var rect = Rect(string.IsNullOrEmpty(text) ? "Runway text" : text, x, y, width, height);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size;
        label.text = text; label.color = Color.white; label.richText = false; label.raycastTarget = false;
        label.enableAutoSizing = true; label.fontSizeMin = size * .8f; label.fontSizeMax = size; return label;
    }
    private UnityEngine.UI.Button Button(string text, float x, float y, float width, Action action)
    {
        var rect = Rect(text, x, y, width, 50); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(.24f, .16f, .29f); var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var obj = new GameObject("Label", typeof(RectTransform)); obj.transform.SetParent(rect, false);
        var tr = (RectTransform)obj.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(8, 3); tr.offsetMax = new Vector2(-8, -3);
        var label = obj.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = 23; label.text = text;
        label.alignment = TextAlignmentOptions.Center; label.color = Color.white; label.raycastTarget = false;
        button.onClick.AddListener(() => action()); return button;
    }
    private void OnDestroy()
    {
        if (stage != null) Destroy(stage);
        if (texture != null) { texture.Release(); Destroy(texture); }
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}
