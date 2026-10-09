using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerChatHistory : MonoBehaviour
{
    public const int Capacity=50;
    private readonly Queue<TMP_Text> rows=new Queue<TMP_Text>();
    private RectTransform root,content;private UnityEngine.UI.ScrollRect scroll;private UnityEngine.UI.Image viewportImage;
    private bool wasOpen,wasSession;private string room;
    public int Count=>rows.Count;
    public string[] Messages { get {var values=new List<string>();foreach(var row in rows)values.Add(row.text);return values.ToArray();} }
    private void Start()=>Build();
    public void Append(string nickname,string message,bool local)
    {
        string clean=PlayerChat.CleanMessage(message);if(clean.Length==0)return;
        if(root==null)Build();
        bool follow=!PlayerChat.IsOpen||scroll.verticalNormalizedPosition<.1f;
        var go=new GameObject("Chat message",typeof(RectTransform),typeof(TextMeshProUGUI),typeof(UnityEngine.UI.LayoutElement));go.transform.SetParent(content,false);
        var label=go.GetComponent<TextMeshProUGUI>();label.font=PlayerChat.Instance.Font;label.fontSize=23;label.richText=false;label.raycastTarget=false;
        label.color=local?new Color(.58f,.94f,.86f):new Color(.91f,.94f,.97f);label.alignment=TextAlignmentOptions.TopLeft;
        string name=PlayerChat.CleanMessage(nickname);label.text=(string.IsNullOrEmpty(name)?"Player":name)+": "+clean;
        go.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=Mathf.Max(32,label.GetPreferredValues(label.text,448,0).y);
        rows.Enqueue(label);
        while(rows.Count>Capacity){var old=rows.Dequeue();old.gameObject.SetActive(false);Destroy(old.gameObject);}
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);Canvas.ForceUpdateCanvases();
        if(follow)scroll.verticalNormalizedPosition=0;
    }
    private void Clear(){while(rows.Count>0)Destroy(rows.Dequeue().gameObject);}
    private void Update()
    {
        var lobby=NetworkLobby.Instance;bool session=lobby!=null&&(lobby.Offline||lobby.InSession);
        string nextRoom=lobby==null?"":lobby.Offline?"offline":lobby.Snapshot.code;
        if(wasSession&&(!session||room!=nextRoom))Clear();wasSession=session;room=nextRoom;
        if(root==null)return;
        bool open=PlayerChat.IsOpen;
        root.gameObject.SetActive(rows.Count>0&&PlayerRegistry.Players.Count>0&&(lobby==null||!lobby.MenuVisible&&lobby.Results==null)&&!EmoteWheel.IsOpen);
        viewportImage.raycastTarget=open;scroll.enabled=open;
        if(wasOpen!=open)
        {
            root.sizeDelta=new Vector2(480,open?380:240);wasOpen=open;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;
        }
    }
    private RectTransform Rect(string name,Transform parent)
    {var go=new GameObject(name,typeof(RectTransform));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;}
    private void Stretch(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=min;rect.offsetMax=max;}
    private void Build()
    {
        if(root!=null)return;
        var canvas=Rect("Chat history canvas",transform);var c=canvas.gameObject.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=106;
        canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();var scale=canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
        root=Rect("Side chat history",canvas);root.anchorMin=root.anchorMax=Vector2.zero;root.pivot=Vector2.zero;root.anchoredPosition=new Vector2(24,140);root.sizeDelta=new Vector2(480,240);
        var bg=root.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=new Color(.025f,.055f,.07f,.78f);bg.raycastTarget=false;
        var header=Rect("History title",root);header.anchorMin=new Vector2(0,1);header.anchorMax=new Vector2(1,1);header.pivot=new Vector2(.5f,1);header.sizeDelta=new Vector2(-24,32);header.anchoredPosition=new Vector2(0,-7);
        var title=header.gameObject.AddComponent<TextMeshProUGUI>();title.font=PlayerChat.Instance.Font;title.fontSize=18;title.text="CHAT · ENTER TO TYPE";title.color=new Color(.63f,.81f,.84f);title.raycastTarget=false;
        var viewport=Rect("History viewport",root);Stretch(viewport,new Vector2(10,8),new Vector2(-10,-40));
        viewportImage=viewport.gameObject.AddComponent<UnityEngine.UI.Image>();viewportImage.color=Color.white;viewportImage.raycastTarget=false;viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
        content=Rect("History content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;layout.spacing=6;
        var fitter=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll=root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;scroll.inertia=false;scroll.enabled=false;
        root.gameObject.SetActive(false);
    }
}
