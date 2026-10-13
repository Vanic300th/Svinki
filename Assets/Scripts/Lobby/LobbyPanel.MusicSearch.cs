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
 private void BuildMusicSearch()
 {
  var window=content;window.sizeDelta=new Vector2(1580,940);
  content=Rect("Music search",window,new Vector2(460,740),new Vector2(510,0));searchPane=content;
  Label("SEARCH MUSIC · "+musicCatalog.Name,new Vector2(450,65),new Vector2(0,315),26);
  searchText=Input("Song / artist · empty = latest",new Vector2(0,245));searchText.characterLimit=120;
  searchText.GetComponent<RectTransform>().sizeDelta=new Vector2(450,58);searchText.textComponent.rectTransform.sizeDelta=new Vector2(420,50);searchText.placeholder.GetComponent<RectTransform>().sizeDelta=new Vector2(420,50);
  var button=Button("SEARCH",new Vector2(0,170),BeginMusicSearch);button.GetComponent<RectTransform>().sizeDelta=new Vector2(450,58);
  searchStatus=Label("CC BY tracks · try chill or leave empty",new Vector2(450,60),new Vector2(0,95),20);
  content=Rect("Playback controls",window,new Vector2(1000,850),new Vector2(-260,0));status.transform.SetParent(content,false);
  var source=Button("TRACK SOURCE",new Vector2(-130,-320),()=>{if(target is LobbyMediaPlayer media&&media.LocalCatalogTrack!=null)Application.OpenURL(media.LocalCatalogTrack.Source);});
  var license=Button("CC BY LICENSE",new Vector2(130,-320),()=>{if(target is LobbyMediaPlayer media&&media.LocalCatalogTrack!=null)Application.OpenURL(media.LocalCatalogTrack.LicenseUrl);});
  foreach(var link in new[]{source,license}){link.GetComponent<RectTransform>().sizeDelta=new Vector2(240,38);var label=link.GetComponentInChildren<TMP_Text>();label.rectTransform.sizeDelta=new Vector2(230,35);label.fontSize=17;}
 }
 private void BeginMusicSearch()
 {
  if(Time.unscaledTime<nextSearch)return;nextSearch=Time.unscaledTime+2;
  if(musicSearch!=null)StopCoroutine(musicSearch);
  foreach(var row in searchRows)if(row!=null)Destroy(row);searchRows.Clear();searchStatus.text="Searching "+musicCatalog.Name+"…";
  musicSearch=StartCoroutine(SearchMusic(searchText.text));
 }
 private IEnumerator SearchMusic(string query)
 {
  yield return musicCatalog.Search(query,(tracks,error)=>
  {
   if(panel==null||searchPane==null)return;
   searchStatus.text=error??(tracks.Count==0?"No available tracks found":"Click a track to add it · WEB opens source");
   var previous=content;content=searchPane;
   for(int i=0;i<tracks.Count;i++)
   {
    var track=tracks[i];var position=new Vector2(-20,25-i*65);
    var add=Button(track.Title+"\n"+track.Artist+" · "+track.LicenseName,position,()=>{mediaInputError=null;target?.Request(player,LobbyAction.Load,track.Url);searchStatus.text="Track requested";});
    add.GetComponent<RectTransform>().sizeDelta=new Vector2(390,58);var label=add.GetComponentInChildren<TMP_Text>();label.rectTransform.sizeDelta=new Vector2(380,52);label.fontSize=17;label.overflowMode=TextOverflowModes.Ellipsis;searchRows.Add(add.gameObject);
    var source=Button("WEB",new Vector2(210,position.y),()=>Application.OpenURL(track.Source));source.GetComponent<RectTransform>().sizeDelta=new Vector2(40,58);source.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta=new Vector2(35,52);source.GetComponentInChildren<TMP_Text>().fontSize=12;searchRows.Add(source.gameObject);
   }
   content=previous;
  });
  musicSearch=null;
 }
 private void CancelMusicSearch()
 {if(musicSearch!=null)StopCoroutine(musicSearch);musicSearch=null;searchRows.Clear();searchPane=null;searchText=null;searchStatus=null;musicAttribution=null;}
}
