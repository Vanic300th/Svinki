using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DefaultExecutionOrder(-350)]
public sealed class PhotoAlbum : MonoBehaviour
{
    public const int Capacity=12;
    public const int PhotoWidth=300, PhotoHeight=200;
    public static PhotoAlbum Instance {get;private set;}
    public static bool IsOpen=>Instance!=null&&Instance.panel!=null&&Instance.panel.activeSelf;
    public static bool BlocksInput=>IsOpen||dismissedFrame==Time.frameCount;
    public static bool ConsumedEscape=>escapeFrame==Time.frameCount;
    private static int dismissedFrame=-1,escapeFrame=-1;
    private readonly List<Texture2D> photos=new List<Texture2D>();
    public int Count=>photos.Count;
    private Canvas cameraCanvas;private RectTransform finderFrame;
    private GameObject panel,viewfinder;private TMP_Text caption,hint;private RawImage preview;
    private UnityEngine.SceneManagement.Scene roundScene;
    private int selected;private CursorLockMode oldLock;private bool oldVisible;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset(){Instance=null;dismissedFrame=escapeFrame=-1;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap(){if(Instance==null)new GameObject("Round Photo Album").AddComponent<PhotoAlbum>();}
    private void Awake(){Instance=this;DontDestroyOnLoad(gameObject);}
    private void Start()=>Build();
    public void Capture(Camera source,Transform heldModel)
    {
        var go=new GameObject("Photo capture camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
        camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);camera.aspect=1.5f;camera.fieldOfView=PlayerPhotoCamera.PhotoFieldOfView;
        // The additional camera renders only during a shutter press, never as a continuous HUD camera.
        var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=source.GetUniversalAdditionalCameraData().renderPostProcessing;
        var target=RenderTexture.GetTemporary(PhotoWidth,PhotoHeight,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
        bool shown=heldModel!=null&&heldModel.gameObject.activeSelf;Texture2D image=null;
        try
        {
            if(shown)heldModel.gameObject.SetActive(false);
            var request=new UniversalRenderPipeline.SingleCameraRequest {destination=target};
            if(!RenderPipeline.SupportsRenderRequest(camera,request))return;
            RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;
            image=new Texture2D(PhotoWidth,PhotoHeight,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,PhotoWidth,PhotoHeight),0,0);image.Apply(false,false);
            ApplyPhotoNoise(image);
            if(photos.Count==Capacity){Destroy(photos[0]);photos.RemoveAt(0);}
            photos.Add(image);image=null;selected=photos.Count-1;Refresh();
        }
        finally
        {
            if(shown&&heldModel!=null)heldModel.gameObject.SetActive(true);
            if(image!=null)Destroy(image);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Destroy(go);
        }
    }
    private static void ApplyPhotoNoise(Texture2D image)
    {
        // Local image grain must not change the gameplay random sequence.
        var random=new System.Random(System.Environment.TickCount);
        var pixels=image.GetPixels32();
        for(int i=0;i<pixels.Length;i++)
        {
            var pixel=pixels[i];int grain=random.Next(-14,15);
            pixel.r=(byte)Mathf.Clamp(pixel.r+grain+random.Next(-3,4),0,255);
            pixel.g=(byte)Mathf.Clamp(pixel.g+grain+random.Next(-3,4),0,255);
            pixel.b=(byte)Mathf.Clamp(pixel.b+grain+random.Next(-3,4),0,255);
            pixels[i]=pixel;
        }
        image.SetPixels32(pixels);image.Apply(false,false);
        if(!image.LoadImage(image.EncodeToJPG(28),false))throw new System.InvalidOperationException("Could not process camera photo.");
        image.filterMode=FilterMode.Point;image.wrapMode=TextureWrapMode.Clamp;
    }
    private PlayerAvatar LocalPlayer(){foreach(var p in PlayerRegistry.Players)if(p!=null&&p.IsLocal)return p;return null;}
    private void Update()
    {
        if(panel==null)return;var lobby=NetworkLobby.Instance;var keys=Keyboard.current;
        var local=LocalPlayer();var scene=local!=null?local.gameObject.scene:default;if(scene!=roundScene){ResetRound();roundScene=scene;}
        if(IsOpen)
        {
            if(LocalPlayer()==null||lobby!=null&&(lobby.MenuVisible||lobby.Results!=null)){Close();return;}
            if(keys?.escapeKey.wasPressedThisFrame==true){escapeFrame=Time.frameCount;Close();}
            else if(keys?.pKey.wasPressedThisFrame==true)Close();
            else if(keys?.leftArrowKey.wasPressedThisFrame==true)Select(-1);
            else if(keys?.rightArrowKey.wasPressedThisFrame==true)Select(1);
        }
        else if(keys?.pKey.wasPressedThisFrame==true && LocalPlayer()!=null && !HowToPlay.BlocksInput && !PlayerChat.BlocksInput && !EmoteWheel.BlocksInput &&
            (lobby==null||!lobby.MenuVisible&&!lobby.InventoryOpen&&!lobby.CargoOpen&&lobby.Results==null&&(lobby.Offline||lobby.Snapshot.phase==SessionPhase.Round)))Open();
        var photo=LocalPlayer()?.GetComponent<PlayerPhotoCamera>();
        bool visible=photo!=null&&photo.Equipped&&!IsOpen&&!HowToPlay.BlocksInput&&!PlayerChat.BlocksInput&&!EmoteWheel.BlocksInput&&(lobby==null||lobby.InputAllowed);
        viewfinder.SetActive(visible);
        if(visible&&local.EyeCamera!=null)
        {
            float height=local.EyeCamera.pixelHeight*Mathf.Tan(PlayerPhotoCamera.PhotoFieldOfView*.5f*Mathf.Deg2Rad)/Mathf.Tan(local.EyeCamera.fieldOfView*.5f*Mathf.Deg2Rad)/cameraCanvas.scaleFactor;
            finderFrame.sizeDelta=new Vector2(height*1.5f,height);
        }
        if(visible)hint.text=photo.CooldownRemaining>0?"SHUTTER READY IN  "+Mathf.CeilToInt(photo.CooldownRemaining)+"s":"LMB — take photo   ·   K / RMB — stow   ·   P — album ("+Count+")";
    }
    public void Open(){if(panel==null)return;oldLock=Cursor.lockState;oldVisible=Cursor.visible;panel.SetActive(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Refresh();}
    public void Close(){if(panel!=null)panel.SetActive(false);dismissedFrame=Time.frameCount;Cursor.lockState=oldLock;Cursor.visible=oldVisible;}
    public void Select(int direction){if(photos.Count>0)selected=(selected+direction+photos.Count)%photos.Count;Refresh();}
    private void Refresh(){if(caption==null)return;caption.text=photos.Count==0?"No photos yet. Find a camera and press LMB.":"PHOTO "+(selected+1)+" / "+photos.Count+"  ·  Latest "+Capacity+" pictures are kept this round";preview.texture=photos.Count==0?null:photos[selected];preview.gameObject.SetActive(photos.Count>0);}
    public void ResetRound(){if(IsOpen)Close();if(preview!=null)preview.texture=null;foreach(var image in photos)if(image!=null)Destroy(image);photos.Clear();selected=0;Refresh();}
    private void OnDestroy(){ResetRound();if(Instance==this)Instance=null;}
    private RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 pos)
    {var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.sizeDelta=size;r.anchoredPosition=pos;return r;}
    private TMP_Text Text(string text,Transform parent,Vector2 size,Vector2 pos,int points)
    {var r=Rect(text,parent,size,pos);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=PlayerChat.Instance!=null?PlayerChat.Instance.Font:TMP_Settings.defaultFontAsset;t.text=text;t.fontSize=points;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.color=Color.white;return t;}
    private void Button(string title,Transform parent,Vector2 pos,UnityEngine.Events.UnityAction action)
    {var r=Rect(title,parent,new Vector2(230,52),pos);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.12f,.3f,.32f);var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);Text(title,r,new Vector2(220,46),Vector2.zero,24);}
    private void Build()
    {
        var root=Rect("Camera Canvas",transform,Vector2.zero,Vector2.zero);var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=150;cameraCanvas=canvas;
        root.gameObject.AddComponent<GraphicRaycaster>();var scaler=root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var album=Rect("Photo Album",root,Vector2.zero,Vector2.zero);album.anchorMin=Vector2.zero;album.anchorMax=Vector2.one;album.offsetMin=album.offsetMax=Vector2.zero;album.gameObject.AddComponent<Image>().color=new Color(.015f,.035f,.045f,.96f);panel=album.gameObject;
        Text("ROUND PHOTO ALBUM",album,new Vector2(1300,65),new Vector2(0,410),36);
        var frame=Rect("Photo",album,new Vector2(1050,700),new Vector2(0,20));preview=frame.gameObject.AddComponent<RawImage>();preview.raycastTarget=false;
        caption=Text("",album,new Vector2(1550,52),new Vector2(0,-365),24);
        Button("← Previous",album,new Vector2(-310,-440),()=>Select(-1));Button("Next →",album,new Vector2(0,-440),()=>Select(1));Button("Close · P / Esc",album,new Vector2(310,-440),Close);panel.SetActive(false);Refresh();
        var finder=Rect("Camera Viewfinder",root,new Vector2(900,600),Vector2.zero);viewfinder=finder.gameObject;finderFrame=finder;
        for(int i=0;i<4;i++){var line=Rect("Frame",finder,i<2?new Vector2(900,2):new Vector2(2,600),i<2?new Vector2(0,i==0?300:-300):new Vector2(i==2?-450:450,0));var image=line.gameObject.AddComponent<Image>();image.color=new Color(.7f,1,.85f,.65f);image.raycastTarget=false;line.anchorMin=line.anchorMax=i<2?new Vector2(.5f,i==0?1:0):new Vector2(i==2?0:1,.5f);line.anchoredPosition=Vector2.zero;if(i<2){line.anchorMin=new Vector2(0,i==0?1:0);line.anchorMax=new Vector2(1,i==0?1:0);line.sizeDelta=new Vector2(0,2);}else{line.anchorMin=new Vector2(i==2?0:1,0);line.anchorMax=new Vector2(i==2?0:1,1);line.sizeDelta=new Vector2(2,0);}}
        hint=Text("",finder,new Vector2(1050,45),new Vector2(0,-330),22);hint.rectTransform.anchorMin=hint.rectTransform.anchorMax=new Vector2(.5f,0);hint.rectTransform.anchoredPosition=new Vector2(0,-30);viewfinder.SetActive(false);
    }
}
