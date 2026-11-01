using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class LobbyPanel
{
 private string mediaInputError;
 private readonly IMusicCatalogProvider musicCatalog=new CcMixterMusicCatalog();
 private TMP_Text musicAttribution;
 private Coroutine musicSearch;
 private RectTransform searchPane;
 private TMP_InputField searchText;
 private TMP_Text searchStatus;
 private readonly List<GameObject> searchRows=new List<GameObject>();
 private float nextSearch;
 private int searchRevision;
 private static string MusicText(string english,string russian)=>GameLanguage.Code=="ru"?russian:english;
 private void BuildMusicSearch()
 {
  var window=content;window.sizeDelta=new Vector2(1580,940);
  content=Rect("Music search",window,new Vector2(460,740),new Vector2(510,0));searchPane=content;
  Label(MusicText("FREE MUSIC","СВОБОДНАЯ МУЗЫКА")+"\n"+musicCatalog.Name,new Vector2(450,65),new Vector2(0,315),26);
  searchText=Input(MusicText("Search ccMixter · song / artist","Поиск в ccMixter · песня / автор"),new Vector2(0,245));searchText.characterLimit=120;
  searchText.GetComponent<RectTransform>().sizeDelta=new Vector2(450,58);searchText.textComponent.rectTransform.sizeDelta=new Vector2(420,50);searchText.placeholder.GetComponent<RectTransform>().sizeDelta=new Vector2(420,50);
  searchText.onSubmit.AddListener(_=>BeginMusicSearch());
  var button=Button(MusicText("SEARCH CCMIXTER","НАЙТИ В CCMIXTER"),new Vector2(0,170),BeginMusicSearch);button.GetComponent<RectTransform>().sizeDelta=new Vector2(450,58);
  searchStatus=Label(MusicText("CC BY catalog · not all commercial releases","Каталог CC BY · не все популярные песни"),new Vector2(450,60),new Vector2(0,95),20);
  var youtube=Button(MusicText("SEARCH YOUTUBE","НАЙТИ НА YOUTUBE"),new Vector2(0,-360),()=>Application.OpenURL(MediaSourceProvider.YouTubeSearchUrl(searchText.text)));
  youtube.GetComponent<RectTransform>().sizeDelta=new Vector2(450,42);
  var youtubeLabel=youtube.GetComponentInChildren<TMP_Text>();youtubeLabel.rectTransform.sizeDelta=new Vector2(430,40);youtubeLabel.fontSize=17;
  youtube.interactable=false;searchText.onValueChanged.AddListener(value=>youtube.interactable=!string.IsNullOrWhiteSpace(value));
  Label(MusicText("YouTube opens in your browser. Paste a video link into the TV to watch in-game.","YouTube откроется в браузере. Ссылку на видео можно вставить в телевизор."),new Vector2(450,70),new Vector2(0,-418),18);
  content=Rect("Playback controls",window,new Vector2(1000,850),new Vector2(-260,0));status.transform.SetParent(content,false);
  var source=Button("TRACK SOURCE",new Vector2(-130,-320),()=>{if(target is LobbyMediaPlayer media&&media.LocalCatalogTrack!=null)Application.OpenURL(media.LocalCatalogTrack.Source);});
  var license=Button("CC BY LICENSE",new Vector2(130,-320),()=>{if(target is LobbyMediaPlayer media&&media.LocalCatalogTrack!=null)Application.OpenURL(media.LocalCatalogTrack.LicenseUrl);});
  foreach(var link in new[]{source,license}){link.GetComponent<RectTransform>().sizeDelta=new Vector2(240,38);var label=link.GetComponentInChildren<TMP_Text>();label.rectTransform.sizeDelta=new Vector2(230,35);label.fontSize=17;}
  nextSearch=0;BeginMusicSearch();
 }
 private void BeginMusicSearch()
 {
  int revision=++searchRevision;
  if(musicSearch!=null)StopCoroutine(musicSearch);
  foreach(var row in searchRows)if(row!=null)Destroy(row);searchRows.Clear();searchStatus.text=MusicText("Searching ","Ищем в ")+musicCatalog.Name+"…";
  musicSearch=StartCoroutine(SearchMusic(searchText.text,revision));
 }
 private IEnumerator SearchMusic(string query,int revision)
 {
  if(Time.unscaledTime<nextSearch)yield return new WaitForSecondsRealtime(nextSearch-Time.unscaledTime);
  nextSearch=Time.unscaledTime+2;
  yield return musicCatalog.Search(query,(tracks,error)=>
  {
   if(panel==null||searchPane==null||revision!=searchRevision)return;
   searchStatus.text=error??(tracks.Count==0?MusicText("No matches in ccMixter. Try YouTube below.","В ccMixter ничего не найдено. Попробуй YouTube ниже."):MusicText("Click a track to add it · WEB opens source","Нажми на трек, чтобы добавить · WEB — источник"));
   var previous=content;content=searchPane;
   for(int i=0;i<tracks.Count;i++)
   {
    var track=tracks[i];var position=new Vector2(-20,25-i*65);
    var add=Button(track.Title+"\n"+track.Artist+" · "+track.LicenseName,position,()=>{mediaInputError=null;target?.Request(player,LobbyAction.Load,track.Url);searchStatus.text=MusicText("Track requested","Трек запрошен");});
    add.GetComponent<RectTransform>().sizeDelta=new Vector2(390,58);var label=add.GetComponentInChildren<TMP_Text>();label.rectTransform.sizeDelta=new Vector2(380,52);label.fontSize=17;label.overflowMode=TextOverflowModes.Ellipsis;searchRows.Add(add.gameObject);
    var source=Button("WEB",new Vector2(210,position.y),()=>Application.OpenURL(track.Source));source.GetComponent<RectTransform>().sizeDelta=new Vector2(40,58);source.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta=new Vector2(35,52);source.GetComponentInChildren<TMP_Text>().fontSize=12;searchRows.Add(source.gameObject);
   }
   content=previous;
  });
  if(revision==searchRevision)musicSearch=null;
 }
 private void CancelMusicSearch()
 {searchRevision++;if(musicSearch!=null)StopCoroutine(musicSearch);musicSearch=null;searchRows.Clear();searchPane=null;searchText=null;searchStatus=null;musicAttribution=null;}
}
