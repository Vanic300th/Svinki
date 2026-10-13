using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>A local stage driven by the shared server result snapshot; inventories stay untouched.</summary>
public sealed class FinalRunwayView : MonoBehaviour
{
    public const float SecondsPerPlayer = 10;
    public static FinalRunwayView Active { get; private set; }
    public bool OwnControl => Showing && results != null && index >= 0 && index < results.entries.Length && (lobby.Offline || results.entries[index].id == lobby.Identity);
    private RunwayMovement localMovement;
    private float jumpVelocity, nextMovementSend;
    private TMP_Text controlHint;
    public void Emote(PigEmote kind)
    {
        if (!OwnControl) return;
        localMovement.emote = (int)kind; localMovement.emoteAt = lobby.RunwayElapsed;
        lobby.SubmitRunwayMovement(localMovement);
    }

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
        lobby = session; font = menuFont; Active = this;
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
        progress = Label("", 90, 735, 1000, 30, 23);
        reaction = Label("", 90, 775, 1000, 26, 20); reaction.color = new Color(1, .72f, .46f);
        poseButton = Button("SAVE OUTFIT", 90, 841, 220, () =>
        {
            if (OwnControl && !results.entries[index].eliminated)
            {
                bool saved = OutfitMemoryStore.Save(results.entries[index], results.round);
                GameLanguage.Set(reaction, saved ? "Outfit saved to your locker" : OutfitMemoryStore.Error ?? "Could not save outfit");
                reactionUntil = Time.unscaledTime + 3;
            }
        });
        poseLabel = poseButton.GetComponentInChildren<TMP_Text>();
        Button("Applaud", 330, 841, 270, () => React("clap"));
        Button("Heart", 620, 841, 230, () => React("heart"));
        Button("Oink!", 870, 841, 190, () => React("oink"));
        Button("View Scores →", 1120, 841, 360, Skip);
        controlHint = Label("WASD — walk · Space — jump · Ctrl — crouch · V — emotes", 90, 810, 1370, 28, 19);
        panel.SetActive(false);
    }
    public void Begin(OutfitRoundResults snapshot)
    {
        results = snapshot; start = Time.unscaledTime; index = -1; localPose = 0;
        seenReaction = lobby.Snapshot.runwayReactionSequence; reactionUntil = 0; GameLanguage.Set(reaction, "");
        panel.SetActive(snapshot != null && snapshot.entries.Length > 0);
        if (Showing) Tick(snapshot);
    }
    public void Hide() { if (panel != null) panel.SetActive(false); ClearPig(); results = null; }
    public void Skip() { if (panel != null) panel.SetActive(false); ClearPig(); }
    public void Tick(OutfitRoundResults snapshot)
    {
        if (!Showing || snapshot == null) return;
        results = snapshot;
        float elapsed = lobby.RunwayElapsed;
        int next = Mathf.FloorToInt(elapsed / SecondsPerPlayer);
        if (next >= results.entries.Length) { Skip(); return; }
        if (next != index) { index = next; Present(results.entries[index]); }
        var entry = results.entries[index];
        bool own = lobby.Offline || entry.id == lobby.Identity;
        poseButton.interactable = own && !entry.eliminated;
        controlHint.gameObject.SetActive(own);
        if (own) ReadMovement();
        GameLanguage.Set(poseLabel, own ? "SAVE OUTFIT" : "Player's turn");
        GameLanguage.Set(progress, "Walk " + (index + 1) + " of " + results.entries.Length + " · " + Mathf.CeilToInt(SecondsPerPlayer - elapsed % SecondsPerPlayer) + (index + 1 == results.entries.Length ? "s until scores" : "s until next"));
        if (lobby.Snapshot.runwayReactionSequence != seenReaction)
        {
            seenReaction = lobby.Snapshot.runwayReactionSequence;
            GameLanguage.Set(reaction, lobby.Snapshot.runwayReaction); reactionUntil = Time.unscaledTime + 3;
        }
        if (Time.unscaledTime > reactionUntil) GameLanguage.Set(reaction, "");
        Animate(elapsed % SecondsPerPlayer, Time.unscaledTime < poseUntil && own ? localPose : entry.presentationPose);
        if (Time.unscaledTime >= nextRender)
        { nextRender = Time.unscaledTime + 1f / 25; cameraStage.Render(); }
    }
    private void React(string kind)
    {
        lobby.RunwayReact(kind);
        if (lobby.Offline) { GameLanguage.Set(reaction, lobby.Nickname + ": " + NetworkLobby.ReactionText(kind)); reactionUntil = Time.unscaledTime + 3; }
    }
    private void Present(OutfitResultEntry entry)
    {
        ClearPig(); localPose = 0; localMovement = entry.movement; jumpVelocity = 0; nextMovementSend = 0;
        GameLanguage.Set(nameLabel, entry.nickname + (entry.eliminated ? " · ghost — eliminated" : " · outfit on show"));
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
        if (entry.eliminated) { pig.name = "Runway Ghost Pig"; pig.AddComponent<RunwayGhost>().Apply(); }
    }
    private void ReadMovement()
    {
        var keys = UnityEngine.InputSystem.Keyboard.current;
        float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
        bool allowed = !EmoteWheel.BlocksInput && !HowToPlay.BlocksInput && !PhotoAlbum.BlocksInput && !PlayerChat.BlocksInput;
        var input = allowed ? GrayboxPlayerController.ReadMoveInput() : Vector2.zero;
        localMovement.crouch = allowed && (keys?.leftCtrlKey.isPressed == true || keys?.rightCtrlKey.isPressed == true) ? 1 : 0;
        localMovement.position += new Vector3(input.x, 0, input.y) * dt * (localMovement.crouch > 0 ? 1 : 2.2f);
        if (input.sqrMagnitude > .01f) localMovement.yaw = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
        if (allowed && keys?.spaceKey.wasPressedThisFrame == true && localMovement.position.y <= .001f) jumpVelocity = 5;
        jumpVelocity -= dt * 12; localMovement.position.y = Mathf.Max(0, localMovement.position.y + jumpVelocity * dt);
        if (localMovement.position.y <= 0) jumpVelocity = 0;
        localMovement.position.x = Mathf.Clamp(localMovement.position.x, -.85f, .85f); localMovement.position.z = Mathf.Clamp(localMovement.position.z, -3.5f, 1.1f);
        if (Time.unscaledTime >= nextMovementSend) { nextMovementSend = Time.unscaledTime + .05f; lobby.SubmitRunwayMovement(localMovement); }
    }
    private void Animate(float time, int pose)
    {
        if (pig == null) return;
        foreach (var pair in rest) if (pair.Key != null) pair.Key.localRotation = pair.Value;
        var entry = results.entries[index]; var movement = OwnControl ? localMovement : entry.movement;
        pig.transform.localPosition = movement.position + Vector3.up * (entry.eliminated ? .18f : 0);
        pig.transform.localRotation = Quaternion.Euler(0, movement.yaw, 0);
        pig.transform.localScale = new Vector3(1 + movement.crouch * .25f, 1 - movement.crouch * .42f, 1 + movement.crouch * .2f);
        bool walking = OwnControl && GrayboxPlayerController.ReadMoveInput().sqrMagnitude > .01f;
        if (!OwnControl) walking = Vector3.Distance(pig.transform.localPosition, previousPresentedPosition) > .002f;
        previousPresentedPosition = pig.transform.localPosition;
        float step = walking ? Mathf.Sin(Time.unscaledTime * 9) * 22 : 0;
        Bone("Thigh_L", new Vector3(step, 0, 0)); Bone("Thigh_R", new Vector3(-step, 0, 0));
        Bone("UpperArm_L", new Vector3(-step * .7f, 0, 0)); Bone("UpperArm_R", new Vector3(step * .7f, 0, 0));
        if (movement.position.y > .05f) { Bone("Thigh_L", new Vector3(-18, 0, 0)); Bone("Thigh_R", new Vector3(-18, 0, 0)); }
        if (movement.emote == (int)PigEmote.Ball && lobby.RunwayElapsed - movement.emoteAt < PigMotion.EmoteDuration)
            pig.transform.localScale = new Vector3(1.15f, .55f, 1.15f);
        else if (movement.emote > 0 && movement.emote < (int)PigEmote.Ball)
            pig.GetComponent<PigMotion>()?.ApplyPreviewEmote((PigEmote)movement.emote, lobby.RunwayElapsed - movement.emoteAt);
    }
    private Vector3 previousPresentedPosition;
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
        GameLanguage.Set(label, text); label.color = Color.white; label.richText = false; label.raycastTarget = false;
        label.enableAutoSizing = true; label.fontSizeMin = size * .8f; label.fontSizeMax = size; return label;
    }
    private UnityEngine.UI.Button Button(string text, float x, float y, float width, Action action)
    {
        var rect = Rect(text, x, y, width, 50); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(.24f, .16f, .29f); var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var obj = new GameObject("Label", typeof(RectTransform)); obj.transform.SetParent(rect, false);
        var tr = (RectTransform)obj.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(8, 3); tr.offsetMax = new Vector2(-8, -3);
        var label = obj.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = 23; GameLanguage.Set(label, text);
        label.alignment = TextAlignmentOptions.Center; label.color = Color.white; label.raycastTarget = false;
        button.onClick.AddListener(() => action()); return button;
    }
    private void OnDestroy()
    {
        if (Active == this) Active = null;
        if (stage != null) Destroy(stage);
        if (texture != null) { texture.Release(); Destroy(texture); }
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}
