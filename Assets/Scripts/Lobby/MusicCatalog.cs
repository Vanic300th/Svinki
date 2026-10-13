using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

public sealed class CatalogTrack
{
 public string Id,Title,Artist,Url,Source; public int Duration;
 public string StreamUrl,LicenseUrl,LicenseName,Credit;
}
public interface IMusicCatalogProvider
{
 string Name {get;}
 IEnumerator Search(string query,Action<List<CatalogTrack>,string> result);
 IEnumerator Resolve(ResolvedMedia input,Action<CatalogTrack,string> result);
}
/// <summary>Search and metadata use the public read-only API. Stable IDs, never signed URLs, enter FishNet.</summary>
public sealed class AudiusMusicCatalog : IMusicCatalogProvider
{
 public string Name=>"Audius";
 public IEnumerator Search(string query,Action<List<CatalogTrack>,string> result)
 {
  query=query?.Trim();if(string.IsNullOrEmpty(query)||query.Length>120){result(new List<CatalogTrack>(),"Enter a song or artist name (up to 120 characters)");yield break;}
  yield return Json(AudiusMediaProvider.Api+"/tracks/search?query="+Uri.EscapeDataString(query)+"&limit=12&app_name=Friendslop",(data,error)=>
  {var tracks=new List<CatalogTrack>();if(data is JArray list)foreach(var entry in list){var track=Read(entry);if(track!=null&&tracks.Count<6)tracks.Add(track);}result(tracks,error);});
 }
 public IEnumerator Resolve(ResolvedMedia input,Action<CatalogTrack,string> result)
 {
  string url=input.Id!=null?AudiusMediaProvider.Api+"/tracks/"+input.Id:AudiusMediaProvider.Api+"/resolve?url="+Uri.EscapeDataString(input.Url);
  yield return Json(url,(data,error)=>{var track=Read(data);result(track,error??(track==null?"Track unavailable or requires an account/payment":null));});
 }
 private static CatalogTrack Read(JToken token)
 {
  try
  {
  if(!(token is JObject t)||t.Value<bool?>("is_streamable")!=true||t.Value<bool?>("is_delete")==true||t.Value<bool?>("is_stream_gated")==true||(t["stream_conditions"]!=null&&t["stream_conditions"].Type!=JTokenType.Null))return null;
  string id=t.Value<string>("id"),title=t.Value<string>("title"),artist=t["user"]?.Value<string>("name"),permalink=t.Value<string>("permalink");
  int duration=t.Value<int?>("duration")??0;if(!AudiusMediaProvider.ValidId(id)||string.IsNullOrWhiteSpace(title)||duration<=0||duration>1800)return null;
  string source="https://audius.co";if(permalink!=null&&permalink.StartsWith("/")&&!permalink.StartsWith("//")&&permalink.Length<=1000)source+=permalink;
  return new CatalogTrack{Id=id,Title=Limit(title,160),Artist=Limit(artist??"Unknown artist",80),Url=AudiusMediaProvider.StreamUrl(id),Source=source,Duration=duration};
  }
  catch(Exception){return null;} // External metadata may contain unexpected field types.
 }
 private static string Limit(string text,int size)=>text.Length>size?text.Substring(0,size):text;
 private static IEnumerator Json(string url,Action<JToken,string> result)
 {
  using(var request=UnityWebRequest.Get(url))
  {
   request.timeout=12;request.redirectLimit=3;request.SetRequestHeader("Accept","application/json");var operation=request.SendWebRequest();
   while(!operation.isDone){if(request.downloadedBytes>512*1024){request.Abort();break;}yield return null;}
   if(request.result!=UnityWebRequest.Result.Success||request.downloadedBytes>512*1024){result(null,"Music service unavailable. Try again later.");yield break;}
   JToken data=null;string error=null;try{data=JObject.Parse(request.downloadHandler.text)["data"];}catch(Exception){error="Music service returned an invalid response";}result(data,error);
  }
 }
}
