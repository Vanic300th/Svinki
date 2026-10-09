using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-450)]
public sealed class HowToPlay : MonoBehaviour
{
    public static HowToPlay Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;
    public static bool BlocksInput => IsOpen || dismissedFrame == Time.frameCount;
    public static bool ConsumedEscape => escapeFrame == Time.frameCount;
    private static int dismissedFrame=-1,escapeFrame=-1;
    private NetworkLobby lobby;
    private TMP_FontAsset font;
    private GameObject root;
    private RectTransform content;
    private TMP_Text footer,continueLabel;
    private UnityEngine.UI.Button[] tabs;
    private Action pending;
    private bool acknowledged;
    private CursorLockMode previousLock;
    private bool previousVisible;
    public int Page { get; private set; }
    public bool Acknowledged => acknowledged;
    private static readonly string[] Titles={"Movement & HUD","Clothes & round","Carts & toys","Friends & danger","Camera & photos"};
    private static readonly string[][] Controls={
        new[]{"W A S D / arrows|Move around the store","Mouse|Look around and aim at items","Shift|Hold to sprint","Space|Jump","Ctrl|Hold to crouch; look under displays","F|Toggle your flashlight","Esc|Open the menu or close the current panel","F1|Open this guide again"},
        new[]{"E|Collect the clothing under your crosshair","Tab / I|Open your outfit inventory","Inventory buttons|Drop one piece or your whole outfit","E at READY|Finish at the entrance when everyone is ready","Ready / Start|In the lobby, everyone readies; the host starts","Next Round|The host returns everyone to the lobby","Pig customisation|Choose your pig’s face, skin and accessories","Settings|Adjust mouse sensitivity in the menu"},
        new[]{"E at cart|Push a cart; E again releases it","R at cart|Ride in the basket","W/S · A/D|Push forward/backward and steer","Shift · LMB|Sprint with a cart; left click launches it","E / Space|Leave the cart basket","G|Open cart cargo to store, wear or unload clothes","E at toy / figure|Grab with free hands","LMB · RMB / E|LMB: attack / throw. RMB or E: release"},
        new[]{"Hold B|Talk nearby; release to mute","Enter|Open text chat; Enter again sends","Esc in chat|Cancel the message","Mouse wheel|Scroll chat history while chat is open","Hold V|Open the emote wheel; aim and release V to choose","Esc / RMB|Cancel the emote wheel","Left / Right|Switch living players while spectating","Lobby code|Copy the code and share it with friends"},
        new[]{"E at camera|Pick up a camera with free hands","K|Equip or stow your camera","LMB with camera|Take a photo for your album","RMB / E|Stow your camera","P|Open or close your round photo album","Left / Right|Browse photos while the album is open","Album buttons|Previous photo, next photo or close","Esc in album|Close the album and return to the game"}
    };
    private static readonly string[][] Rules={
        new[]{"FIND YOUR WAY\nThe store has offset departments, side routes and concealed shelves. Search behind displays and inside stock cabinets.","NEARBY MAP\nThe map follows you and shows your room and nearby departments. It follows the selected player while spectating.","PLAYER COLOURS\nEach arrow and through-wall nickname share a unique colour, kept across rounds and reconnects. Carts: yellow squares. READY: green R.","LIVE PIG & ITEMS\nThe side pig mirrors your appearance and outfit. Aim at items for an E prompt; crouch to inspect low shelves."},
        new[]{"BUILD A LOOK\nCollect headwear, a top, trousers and footwear. Your inventory shows the four slots; drop a piece to make room for another.","NEW ROUND, NEW SEARCH\nClothing changes hiding spots each round. Mannequins, carts, toys and cameras also get new reachable spawn positions.","RETURN TO THE ENTRANCE\nREADY is on the left wall towards the back of the entrance. Gather together; leaving the zone or changing your outfit cancels readiness.","FASHION SHOW\nEveryone sees the runway and outfit scores. Colours, patterns and combinations affect the rating. Eliminated pigs appear as ghosts."},
        new[]{"THREE SHARED CARTS\nA cart has one driver and one passenger. Wall collisions stop it; a launched cart can knock down other pigs.","EIGHT STORAGE SLOTS\nCargo carries spare garments, including several pieces for the same clothing slot. Transfer items while the cart is stopped or moving slowly.","ONE MONKEY PER PLAYER\nToys spawn at random reachable spots. One successful hit consumes a toy, even against an ordinary mannequin; a miss keeps it.","THIRTY-SECOND STUN\nA toy hit knocks an aggressive mannequin down for 30 seconds. You need free hands and cannot carry a toy while using a cart."},
        new[]{"PROXIMITY VOICE\nFull volume nearby, fading to silence at 25 metres. Your microphone is active only while B is held; allow microphone access when prompted.","WATCH THE MANNEQUINS\nAggressive mannequins freeze while a living player sees them. Coordinate who keeps watch while friends search.","TWO HITS\nThe first aggressive hit knocks you down. The second eliminates you until the next round; you drop your clothes and spectate friends.","THIEVES & CHAT\nSmall thieves steal clothing and take it to their nest. Messages appear above pigs and stay in the side history. Emotes need free hands; voice is muted after death."},
        new[]{"ONE CAMERA PER PLAYER\nCameras spawn at random spots in the store. Pick one up with E, then use K to equip or stow it. Stowing frees your hands for toys and carts.","CHEAP CAMERA PHOTOS\nPictures have chunky pixels, grain and compression artefacts. The viewfinder marks the photo area; the shutter needs 1 second.","ROUND PHOTO ALBUM\nLMB takes a photo; P opens your latest 12 pictures. Photos exclude the HUD and stay in your local album until the round ends, even after death.","FLASH & RANDOM SPAWNS\nThe flash is cosmetic; it never stuns mannequins. Everyone sees the same random clothing, mannequins, carts, toys and cameras. The entrance stays clear."}
    };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset(){Instance=null;dismissedFrame=escapeFrame=-1;}
    private void Awake()=>Instance=this;
    public void Build(Transform parent,TMP_FontAsset textFont,NetworkLobby session)
    {
        lobby=session;font=textFont;
        var canvas=Rect("How to play canvas",parent,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,Vector2.zero);
        canvas.anchorMin=Vector2.zero;canvas.anchorMax=Vector2.one;canvas.offsetMin=canvas.offsetMax=Vector2.zero;
        var c=canvas.gameObject.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.overrideSorting=true;c.sortingOrder=200;
        canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var scaler=canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var wash=Rect("Guide backdrop",canvas,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);wash.anchorMax=Vector2.one;wash.offsetMin=wash.offsetMax=Vector2.zero;
        wash.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.015f,.025f,.04f,.91f);root=wash.gameObject;
        var panel=Rect("Guide panel",wash,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(1480,920));panel.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.04f,.075f,.095f,1);
        Label("HOW TO PLAY SVINKI",panel,new Vector2(44,-32),new Vector2(1392,62),38,new Color(1,.79f,.57f));
        Label("Find an outfit. Keep an eye on the mannequins. Meet your friends at the entrance.",panel,new Vector2(44,-102),new Vector2(1392,40),23,new Color(.75f,.86f,.89f));
        tabs=new UnityEngine.UI.Button[Titles.Length];for(int i=0;i<tabs.Length;i++){int page=i;tabs[i]=Button(Titles[i],panel,new Vector2(44+i*280,-165),new Vector2(268,56),()=>SelectPage(page),24);}
        content=Rect("Guide page",panel,new Vector2(0,1),new Vector2(0,1),new Vector2(44,-250),new Vector2(1392,565));
        footer=Label("",panel,new Vector2(44,-842),new Vector2(930,38),21,new Color(.65f,.79f,.84f));
        var close=Button("Continue",panel,new Vector2(1112,-824),new Vector2(324,60),Continue,26);continueLabel=close.GetComponentInChildren<TMP_Text>();
        SelectPage(0);root.SetActive(false);
    }
    public void BeforeStart(Action action){if(acknowledged){action();return;}Show(action);}
    public void Show(Action action=null)
    {
        if(root==null)return;if(!IsOpen){previousLock=Cursor.lockState;previousVisible=Cursor.visible;}
        pending=action;root.SetActive(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;SelectPage(0);
        continueLabel.text=pending==null?"Back to game / menu":"Got it — Continue";
    }
    public void SelectPage(int index)
    {
        Page=Mathf.Clamp(index,0,Titles.Length-1);
        foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        for(int i=0;i<tabs.Length;i++)tabs[i].targetGraphic.color=i==Page?new Color(.28f,.45f,.49f):new Color(.10f,.20f,.25f);
        for(int i=0;i<Controls[Page].Length;i++)
        {
            var pair=Controls[Page][i].Split('|');float y=-i*68;
            var row=Rect("Control row",content,new Vector2(0,1),new Vector2(0,1),new Vector2(0,y),new Vector2(695,62));row.gameObject.AddComponent<UnityEngine.UI.Image>().color=i%2==0?new Color(.065f,.12f,.15f):new Color(.05f,.095f,.12f);
            Label(pair[0],row,new Vector2(14,-4),new Vector2(178,54),21,new Color(1,.78f,.52f));
            Label(pair[1],row,new Vector2(206,-4),new Vector2(470,54),21,new Color(.92f,.95f,.96f));
        }
        for(int i=0;i<Rules[Page].Length;i++)Label(Rules[Page][i],content,new Vector2(735,-i*135),new Vector2(650,122),23,new Color(.85f,.92f,.92f));
        footer.text="Page "+(Page+1)+" / "+Titles.Length+"  ·  Use the tabs  ·  F1 — guide  ·  Esc — back";
    }
    public void Continue(){acknowledged=true;var action=pending;Close();action?.Invoke();}
    public void Close()
    {
        pending=null;if(root!=null)root.SetActive(false);dismissedFrame=Time.frameCount;
        bool free=lobby==null||lobby.MenuVisible||lobby.Results!=null||!lobby.Offline&&!lobby.InSession;
        Cursor.lockState=free?CursorLockMode.None:previousLock;Cursor.visible=free||previousVisible;
    }
    private void Update()
    {
        var keys=Keyboard.current;if(keys==null||root==null)return;
        if(IsOpen)
        {
            if(keys.escapeKey.wasPressedThisFrame){escapeFrame=Time.frameCount;Close();}
            else if(keys.f1Key.wasPressedThisFrame)Close();
        }
        else if(keys.f1Key.wasPressedThisFrame&&!PhotoAlbum.BlocksInput&&!PlayerChat.BlocksInput&&!EmoteWheel.BlocksInput&&!lobby.InventoryOpen&&!lobby.CargoOpen)Show();
    }
    private void OnDestroy(){if(Instance==this)Instance=null;}
    private static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=position;r.sizeDelta=size;return r;}
    private TMP_Text Label(string text,Transform parent,Vector2 position,Vector2 size,float points,Color colour)
    {var r=Rect(text.Split('\n')[0],parent,new Vector2(0,1),new Vector2(0,1),position,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=points;t.text=text;t.color=colour;t.richText=false;t.raycastTarget=false;t.alignment=TextAlignmentOptions.TopLeft;return t;}
    private UnityEngine.UI.Button Button(string text,Transform parent,Vector2 position,Vector2 size,Action action,float points)
    {var r=Rect(text,parent,new Vector2(0,1),new Vector2(0,1),position,size);var img=r.gameObject.AddComponent<UnityEngine.UI.Image>();img.color=new Color(.12f,.25f,.3f);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=img;var t=Label(text,r,new Vector2(8,-8),size-new Vector2(16,16),points,Color.white);t.alignment=TextAlignmentOptions.Center;b.onClick.AddListener(()=>action());return b;}
}
