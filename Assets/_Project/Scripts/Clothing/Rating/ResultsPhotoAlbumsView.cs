using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Everyone's shared photos, browsed alongside the round's outfit scores.</summary>
public sealed class ResultsPhotoAlbumsView : MonoBehaviour
{
    private NetworkLobby lobby;
    private TMP_FontAsset font;
    private GameObject panel;
    private TMP_Text heading,caption,empty;
    private UnityEngine.UI.RawImage image;
    private UnityEngine.UI.Button previous,next;
    private readonly List<UnityEngine.UI.Button> players=new List<UnityEngine.UI.Button>();
    private OutfitRoundResults results;
    private int playerIndex,photoIndex;
    public bool Showing=>panel!=null && panel.activeSelf;
    public string SelectedPlayer=>results!=null && playerIndex<results.entries.Length?results.entries[playerIndex].id:null;
    public int SelectedPhoto=>photoIndex;

    public void Build(Transform parent,TMP_FontAsset menuFont,NetworkLobby session)
    {
        lobby=session;font=menuFont;
        panel=Rect("Results Photo Albums",parent,0,0,1560,920).gameObject;
        panel.GetComponent<RectTransform>().anchorMin=panel.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,.5f);
        panel.GetComponent<RectTransform>().pivot=new Vector2(.5f,.5f);
        panel.AddComponent<UnityEngine.UI.Image>().color=new Color(.025f,.045f,.06f,1);
        Label("ROUND PHOTO ALBUMS",panel.transform,36,30,1200,52,36);
        Label("Choose a player and browse their round photos",panel.transform,36,92,1488,38,23).color=new Color(.67f,.8f,.83f);
        Label("PLAYERS",panel.transform,36,145,300,36,22);
        for(int i=0;i<NetworkLobby.Capacity;i++)
        {int selected=i;players.Add(Button("",36,196+i*88,300,76,()=>SelectPlayer(selected)));}
        heading=Label("",panel.transform,400,145,1110,40,27);
        heading.alignment=TextAlignmentOptions.Center;
        var frame=Rect("Shared Photo Frame",panel.transform,400,196,1110,580);
        frame.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.01f,.02f,.03f,1);
        var picture=Rect("Shared Photo",frame,0,0,870,580);
        picture.anchorMin=picture.anchorMax=new Vector2(.5f,.5f);picture.pivot=new Vector2(.5f,.5f);picture.anchoredPosition=Vector2.zero;
        image=picture.gameObject.AddComponent<UnityEngine.UI.RawImage>();image.raycastTarget=false;
        empty=Label("",frame,40,220,1030,140,27);empty.alignment=TextAlignmentOptions.Center;
        caption=Label("",panel.transform,400,794,1110,36,23);caption.alignment=TextAlignmentOptions.Center;
        previous=Button("← Previous",430,844,230,48,()=>SelectPhoto(-1));
        next=Button("Next →",680,844,230,48,()=>SelectPhoto(1));
        Button("← Outfit scores",1070,844,440,48,Hide);
        panel.SetActive(false);
    }
    public void Show(OutfitRoundResults snapshot,int selected=0)
    {results=snapshot;playerIndex=Mathf.Clamp(selected,0,Mathf.Max(0,(snapshot?.entries.Length??1)-1));photoIndex=0;panel.SetActive(true);Refresh();}
    public void Hide(){if(panel!=null)panel.SetActive(false);if(image!=null)image.texture=null;}
    public void SelectPlayer(int index){if(results==null || index<0 || index>=results.entries.Length)return;playerIndex=index;photoIndex=0;Refresh();}
    public void SelectPhoto(int direction)
    {var photos=lobby.RoundPhotos(SelectedPlayer);if(photos.Count>0)photoIndex=(photoIndex+direction+photos.Count)%photos.Count;Refresh();}
    private void Update(){if(Showing)Refresh();}
    private void Refresh()
    {
        if(results==null)return;
        for(int i=0;i<players.Count;i++)
        {
            bool exists=i<results.entries.Length;players[i].gameObject.SetActive(exists);if(!exists)continue;
            var entry=results.entries[i];int count=lobby.RoundPhotoCount(entry.id);
            GameLanguage.Set(players[i].GetComponentInChildren<TMP_Text>(), entry.nickname+"\n"+(lobby.RoundAlbumReceived(entry.id)?count+" photos":"Receiving album…"));
            players[i].GetComponent<UnityEngine.UI.Image>().color=i==playerIndex?new Color(.18f,.36f,.4f):new Color(.1f,.16f,.22f);
        }
        if(results.entries.Length==0)return;
        var selected=results.entries[playerIndex];var photos=lobby.RoundPhotos(selected.id);int expected=lobby.RoundPhotoCount(selected.id);
        GameLanguage.Set(heading, selected.nickname+(selected.eliminated?" · ghost photographer":"")+" — photo album");
        photoIndex=Mathf.Clamp(photoIndex,0,Mathf.Max(0,photos.Count-1));image.texture=photos.Count>0?photos[photoIndex]:null;
        image.gameObject.SetActive(photos.Count>0);empty.gameObject.SetActive(photos.Count==0);
        GameLanguage.Set(empty, !lobby.RoundAlbumReceived(selected.id)||expected>0?"Receiving this player's photos…":"This player did not take any photos this round.");
        GameLanguage.Set(caption, photos.Count==0?"":("PHOTO "+(photoIndex+1)+" / "+photos.Count+(photos.Count<expected?" · receiving "+expected+" photos":" · latest "+PhotoAlbum.Capacity+" pictures from this round")));
        previous.interactable=next.interactable=photos.Count>1;
    }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return r;
    }
    private TMP_Text Label(string text,Transform parent,float x,float y,float width,float height,float size)
    {
        var label=Rect(text.Length>0?text:"Album text",parent,x,y,width,height).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=font;GameLanguage.Set(label, text);label.fontSize=size;label.fontSizeMin=size*.78f;label.fontSizeMax=size;label.enableAutoSizing=true;
        label.richText=false;label.raycastTarget=false;label.color=Color.white;return label;
    }
    private UnityEngine.UI.Button Button(string text,float x,float y,float width,float height,Action action)
    {
        var r=Rect(text.Length>0?text:"Photographer",panel.transform,x,y,width,height);
        var bg=r.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=new Color(.1f,.16f,.22f);
        var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=bg;b.onClick.AddListener(()=>action());
        Label(text,r,12,5,width-24,height-10,23).alignment=TextAlignmentOptions.Center;return b;
    }
}
