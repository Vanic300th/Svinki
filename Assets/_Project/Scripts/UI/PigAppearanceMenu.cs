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
    private float previewFocus=.98f, previewDistance=3.7f;
    private ProjectSvinMenuLayout editableMenu;

    public void Build(Transform canvas, TMP_FontAsset menuFont, NetworkLobby session, ProjectSvinMenuLayout layout = null)
    {
        lobby = session; font = menuFont; face = PigFace.Decode(PigFace.Selected);
        editableMenu = layout;
        if (editableMenu != null)
        {
            panel = editableMenu.appearancePanel;
            CreatePreview(editableMenu.previewRect.GetComponent<UnityEngine.UI.RawImage>());
            editableMenu.rotateLeft.onClick.AddListener(() => RotatePreview(-30)); buttons.Add(editableMenu.rotateLeft);
            editableMenu.rotateRight.onClick.AddListener(() => RotatePreview(30)); buttons.Add(editableMenu.rotateRight);
            foreach (var preset in editableMenu.appearancePresets)
                if (preset != null) preset.gameObject.SetActive(false);
        }
        else
        {
        panel = new GameObject("Pig Appearance Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvas, false);
        var root = (RectTransform)panel.transform;
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.sizeDelta = new Vector2(480, 970);
        root.anchoredPosition = new Vector2(462, 0);
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.065f, .085f, .12f, .98f);
        Label("YOUR PIG", 26, 32, 428, 42, 30);
        Label("Mix accessories, colours and patterns", 26, 76, 428, 30, 19);
        RectTransform image = Rect("3D Pig Preview", panel.transform, 26, 120, 428, 350);
        var raw = image.gameObject.AddComponent<UnityEngine.UI.RawImage>(); raw.raycastTarget = false;
        CreatePreview(raw);
        Button("Left", 26, 428, 90, () => RotatePreview(-30));
        Button("Right", 364, 428, 90, () => RotatePreview(30));

        }
        CreateChoices();
        void Heading(string value, Func<bool> visible=null)
        {
            var row=Rect("Group: "+value,choices,0,0,760,40);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=40;
            var title=Label(value,10,0,700,40,24,row);title.alignment=TextAlignmentOptions.MidlineLeft;
            var style=ProjectSvinSkin.Bind(title,ProjectSvinRole.MutedText);style.fontRole=ProjectSvinFontRole.Heading;style.preserveFontSize=true;style.Apply();
            if(visible!=null)refreshLabels.Add(()=>row.gameObject.SetActive(visible()));
        }
        void Choice(string caption,Func<string> value,Action<int> change,Func<bool> visible=null,bool submenu=false)
        {
            var row=Rect(caption,choices,0,0,760,69);
            var entry=row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();entry.minHeight=entry.preferredHeight=69;
            float width=760*(ProjectSvinTheme.Current!=null?ProjectSvinTheme.Current.buttonScale:.92f);
            var surface=Rect("Row background",row,submenu?18:0,0,width-(submenu?18:0),63);
            var image=surface.gameObject.AddComponent<UnityEngine.UI.Image>();image.raycastTarget=false;
            var shape=ProjectSvinSkin.Bind(image,submenu?ProjectSvinRole.Track:ProjectSvinRole.Button);ProjectSvinSkin.Outline(image);
            if(ProjectSvinTheme.Current!=null){image.sprite=ProjectSvinTheme.Current.buttonShape;image.type=UnityEngine.UI.Image.Type.Sliced;}
            var text=Label("",submenu?84:66,0,width-(submenu?150:132),63,submenu?21:23,row);text.alignment=TextAlignmentOptions.MidlineLeft;
            var textStyle=ProjectSvinSkin.Bind(text,ProjectSvinRole.Text);textStyle.fontRole=ProjectSvinFontRole.Heading;textStyle.preserveFontSize=true;textStyle.Apply();
            text.enableAutoSizing=true;text.fontSizeMax=submenu?21:23;text.fontSizeMin=14;text.textWrappingMode=TextWrappingModes.NoWrap;
            Arrow(-1,submenu?26:8);Arrow(1,width-56);
            void Arrow(int direction,float x)
            {
                var r=Rect((direction<0?"Previous ":"Next ")+caption,row,x,9,48,46);
                var bg=r.gameObject.AddComponent<UnityEngine.UI.Image>();ProjectSvinSkin.Bind(bg,ProjectSvinRole.Track);
                if(ProjectSvinTheme.Current!=null){bg.sprite=ProjectSvinTheme.Current.buttonShape;bg.type=UnityEngine.UI.Image.Type.Sliced;}
                var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=bg;
                var arrow=Label(direction<0?"←":"→",0,0,48,46,30,r);arrow.alignment=TextAlignmentOptions.Center;
                ProjectSvinSkin.Bind(arrow,ProjectSvinRole.Text);button.onClick.AddListener(()=>{change(direction);Save();FocusFor(caption);});buttons.Add(button);
            }
            refreshLabels.Add(()=>{GameLanguage.Set(text,caption+": "+value());row.gameObject.SetActive(visible==null||visible());});
        }
        int Step(int value,int delta,int count)=>(value+delta+count)%count;
        Heading("FACE");
        Choice("Eyes",()=>PigAppearance.EyesNames[face.eyes],d=>face.eyes=Step(face.eyes,d,PigFace.EyesCount));
        Choice("Brows",()=>PigAppearance.BrowsNames[face.brows],d=>face.brows=Step(face.brows,d,PigFace.BrowsCount));
        Heading("HAIR & FACIAL HAIR");
        Choice("Hair style",()=>PigAppearance.HairNames[face.hairStyle],d=>face.hairStyle=Step(face.hairStyle,d,PigFace.HairCount));
        Choice("Hair colour",()=>PigAppearance.HairColorNames[face.hairColor],d=>face.hairColor=Step(face.hairColor,d,PigFace.ColorCount),()=>face.hairStyle>0,true);
        Choice("Moustache",()=>PigAppearance.MustacheNames[face.mustache],d=>face.mustache=Step(face.mustache,d,PigFace.MustacheCount));
        Choice("Goatee",()=>Yes(face.beard),d=>face.beard=!face.beard);
        Choice("Facial hair colour",()=>PigAppearance.HairColorNames[face.hairColor],d=>face.hairColor=Step(face.hairColor,d,PigFace.ColorCount));
        Heading("GLASSES");
        Choice("Glasses",()=>PigAppearance.GlassesNames[face.glassesStyle],d=>face.glassesStyle=Step(face.glassesStyle,d,PigFace.GlassesCount));
        Heading("PIERCINGS");
        Choice("Ear piercing",()=>Yes(face.earPiercing),d=>face.earPiercing=!face.earPiercing);
        Choice("Brow piercing",()=>Yes(face.browPiercing),d=>face.browPiercing=!face.browPiercing);
        Choice("Nose ring",()=>Yes(face.nosePiercing),d=>face.nosePiercing=!face.nosePiercing);
        Heading("SKIN & PATTERNS");
        Choice("Skin colour",()=>PigAppearance.ColorNames[face.skinColor],d=>face.skinColor=Step(face.skinColor,d,PigFace.SkinColorCount));
        Choice("Pattern",()=>PigAppearance.PatternNames[face.pattern],d=>face.pattern=Step(face.pattern,d,PigFace.PatternCount));
        Choice("Pattern colour",()=>PigAppearance.HairColorNames[face.patternColor],d=>face.patternColor=Step(face.patternColor,d,PigFace.ColorCount));
        Heading("TATTOOS");
        Choice("Tattoo",()=>PigAppearance.TattooNames[face.tattoo],d=>face.tattoo=Step(face.tattoo,d,PigFace.TattooCount));
        Choice("Tattoo placement",()=>PigAppearance.PlacementNames[face.tattooPlacement],d=>face.tattooPlacement=Step(face.tattooPlacement,d,PigFace.PlacementCount),()=>face.tattoo>0);
        foreach(var refresh in refreshLabels)refresh();
        if(preview!=null)preview.Apply(face.Encode());
        Canvas.ForceUpdateCanvases();
        var initialScroll=choices.GetComponentInParent<UnityEngine.UI.ScrollRect>(true);
        initialScroll.StopMovement();initialScroll.verticalNormalizedPosition=1;
    }

    public void SetVisible(bool visible)
    {
        if (panel == null) return;
        panel.SetActive(visible);
        bool editable = !lobby.Busy && !lobby.Offline &&
            (lobby.Snapshot.phase == SessionPhase.Menu || lobby.Snapshot.phase == SessionPhase.Lobby);
        foreach (var button in buttons) button.interactable = editable;
        if(!editable && previewFocus!=.98f)FocusFor("Skin colour");
        if (visible && previewCamera != null && Time.unscaledTime >= nextRender)
        { nextRender = Time.unscaledTime + 1f / 15; previewCamera.Render(); }
    }

    public void SelectFace(long code) { face = PigFace.Decode(PigFace.Sanitize(code)); Save(); FocusFor("Skin colour"); }

    private static string Yes(bool value) => value ? "yes" : "none";
    private void FocusFor(string caption)
    {
        if(previewCamera==null)return;
        previewYaw=0;
        bool body=caption=="Skin colour"||caption=="Pattern"||caption=="Pattern colour"||caption.StartsWith("Tattoo",StringComparison.Ordinal)&&face.tattoo==0;
        if(caption.StartsWith("Tattoo",StringComparison.Ordinal)&&face.tattoo>0)
        {
            previewFocus=new[]{.87f,1.08f,.30f,.96f}[face.tattooPlacement];
            previewDistance=face.tattooPlacement==2?1.4f:1.9f;
            previewYaw=new[]{0,155,0,180}[face.tattooPlacement];
        }
        else {previewFocus=body?.98f:1.47f;previewDistance=body?3.7f:2.1f;}
        RenderPreviewView();
    }
    private void RotatePreview(float degrees)
    {
        if(previewCamera==null)return;
        previewYaw=Mathf.Repeat(previewYaw+degrees,360);RenderPreviewView();
    }
    private void RenderPreviewView()
    {
        previewCamera.transform.localPosition=Quaternion.Euler(0,previewYaw,0)*new Vector3(.12f,previewFocus+.04f,previewDistance);
        previewCamera.transform.LookAt(previewWorld.transform.position+Vector3.up*previewFocus);
        previewCamera.Render();
    }
    private void CreateChoices()
    {
        if (editableMenu != null) { choices = editableMenu.appearanceChoices.GetComponent<UnityEngine.UI.ScrollRect>().content; return; }
        var root = Rect("Appearance Choices", panel.transform, 26, 486, 428, 406);
        var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var viewport = Rect("Viewport", root, 0, 0, 428, 406);
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
        var track = Rect("Scrollbar", root, 416, 0, 12, 406);
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
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = ProjectSvinTheme.Current != null ? ProjectSvinTheme.Current.panel : new Color(.20f, .24f, .30f);
        if (editableMenu != null) previewCamera.backgroundColor = new Color(0, 0, 0, 0);
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
        GameLanguage.Set(label, text); label.color = Color.white; label.richText = false; label.raycastTarget = false; return label;
    }
    private UnityEngine.UI.Button Button(string text, float x, float y, float width, Action action, Transform parent = null)
    {
        var r = Rect(text, parent ?? panel.transform, x, y, width, 40);
        var image = r.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.16f, .24f, .32f);
        var button = r.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var label = Label(text, 10, 3, width - 20, 34, 20, r); label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 20;
        button.onClick.AddListener(() => action()); buttons.Add(button); return button;
    }
    private void OnDestroy()
    {
        if (previewWorld != null) Destroy(previewWorld);
        if (texture != null) { texture.Release(); Destroy(texture); }
    }
}
