using System;
using System.Text.RegularExpressions;
using UnityEngine;

public enum LobbyMediaBackend : byte { Direct, YouTube, Audius, CcMixter }

public sealed class CcMixterMediaProvider : IMediaSourceProvider
{
 public static bool IsHost(string host)=>host=="ccmixter.org"||host=="www.ccmixter.org";
 public static bool ValidId(string id)=>id!=null&&Regex.IsMatch(id,"^[1-9][0-9]{0,8}$");
 public bool TryResolve(string url,LobbyMediaKind kind,out ResolvedMedia media)
 {
  media=default;var uri=new Uri(url);if(kind!=LobbyMediaKind.Audio||!IsHost(uri.Host)||!uri.IsDefaultPort)return false;
  var parts=uri.AbsolutePath.Trim('/').Split('/');
  if(parts.Length!=3||parts[0]!="files"||!Regex.IsMatch(parts[1],"^[A-Za-z0-9_-]{1,80}$")||!ValidId(parts[2]))return false;
  media=new ResolvedMedia{Backend=LobbyMediaBackend.CcMixter,Id=parts[2],Url="https://ccmixter.org/files/"+parts[1]+"/"+parts[2],Title="ccMixter · "+parts[2],AudioType=AudioType.MPEG};return true;
 }
}
public sealed class YouTubeMediaProvider : IMediaSourceProvider
{
 public static bool IsHost(string host)=>host=="youtube.com"||host=="www.youtube.com"||host=="m.youtube.com"||host=="youtu.be"||host=="www.youtube-nocookie.com";
 public bool TryResolve(string url,LobbyMediaKind kind,out ResolvedMedia media)
 {
  media=default;var uri=new Uri(url);if(kind!=LobbyMediaKind.Video||!IsHost(uri.Host)||!uri.IsDefaultPort)return false;
  string id=null;
  if(uri.Host=="youtu.be")id=uri.AbsolutePath.Trim('/');
  else if(uri.AbsolutePath=="/watch")
  {foreach(var part in uri.Query.TrimStart('?').Split('&')){var pair=part.Split('=');if(pair.Length==2&&pair[0]=="v")id=Uri.UnescapeDataString(pair[1]);}}
  else {var parts=uri.AbsolutePath.Trim('/').Split('/');if(parts.Length==2&&(parts[0]=="embed"||parts[0]=="shorts"||parts[0]=="live"))id=parts[1];}
  if(id==null||!Regex.IsMatch(id,"^[A-Za-z0-9_-]{11}$"))return false;
  media=new ResolvedMedia{Backend=LobbyMediaBackend.YouTube,Id=id,Url="https://www.youtube.com/watch?v="+id,Title="YouTube · "+id};return true;
 }
}
public sealed class AudiusMediaProvider : IMediaSourceProvider
{
 public const string Api="https://api.audius.co/v1";
 public static string StreamUrl(string id)=>Api+"/tracks/"+id+"/stream";
 public static bool ValidId(string id)=>id!=null&&Regex.IsMatch(id,"^[A-Za-z0-9]{1,32}$");
 public bool TryResolve(string url,LobbyMediaKind kind,out ResolvedMedia media)
 {
  media=default;var uri=new Uri(url);if(kind!=LobbyMediaKind.Audio||uri.Scheme!="https"||!uri.IsDefaultPort)return false;
  if(uri.Host=="api.audius.co")
  {var p=uri.AbsolutePath.Trim('/').Split('/');if(p.Length!=4||p[0]!="v1"||p[1]!="tracks"||p[3]!="stream"||!ValidId(p[2]))return false;media=new ResolvedMedia{Backend=LobbyMediaBackend.Audius,Id=p[2],Url=StreamUrl(p[2]),Title="Audius · "+p[2],AudioType=AudioType.MPEG};return true;}
  if(uri.Host!="audius.co"&&uri.Host!="www.audius.co")return false;
  var parts=uri.AbsolutePath.Trim('/').Split('/');if(parts.Length!=2||parts[0].Length==0||parts[1].Length==0)return false;
  media=new ResolvedMedia{Backend=LobbyMediaBackend.Audius,Url="https://audius.co"+uri.AbsolutePath,Title="Audius",AudioType=AudioType.MPEG};return true;
 }
}
