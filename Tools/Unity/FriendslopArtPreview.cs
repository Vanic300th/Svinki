using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
public static class FriendslopArtPreview
{
 public static string Capture()
 {
  var social=SceneManager.GetSceneByName("FriendslopLobby");var factory=SceneManager.GetSceneByName("SampleScene");
  Directory.CreateDirectory("ArtSource/IndustrialCC0/Previews");
  Shot(social,"lobby",new Vector3(0,2.15f,-29.3f),new Vector3(-1,1.5f,-19));
  Shot(social,"lounge",new Vector3(-2.5f,1.8f,-29),new Vector3(-8,1.5f,-25));
  Shot(social,"sports",new Vector3(5.5f,1.9f,-24),new Vector3(10.1f,2.3f,-21));
  Shot(social,"darts",new Vector3(8,1.8f,-28.5f),new Vector3(10.7f,1.7f,-28.5f));
  Shot(factory,"factory",new Vector3(0,2.1f,-3),new Vector3(0,2.2f,15));
  return Path.GetFullPath("ArtSource/IndustrialCC0/Previews/lobby.png");
 }
 static void Shot(Scene scene,string name,Vector3 position,Vector3 targetPoint)
 {
  var previousScene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(scene);
  var others=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i)).Where(s=>s!=scene).SelectMany(s=>s.GetRootGameObjects()).Where(g=>g.activeSelf).ToArray();
  var beams=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).Where(l=>l.name=="Flashlight Beam"&&l.enabled).ToArray();
  var go=new GameObject("Art preview camera");SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=68;camera.nearClipPlane=.08f;camera.farClipPlane=160;
  camera.transform.position=position;camera.transform.LookAt(targetPoint);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.06f,.07f);
  go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
  var target=RenderTexture.GetTemporary(1600,900,24);var oldRT=RenderTexture.active;Texture2D image=null;
  try
  {
   foreach(var g in others)g.SetActive(false);
   foreach(var l in beams)l.enabled=false;
   RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
   image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("ArtSource/IndustrialCC0/Previews/"+name+".png",image.EncodeToPNG());
  }
  finally{foreach(var l in beams)l.enabled=true;foreach(var g in others)g.SetActive(true);SceneManager.SetActiveScene(previousScene);RenderTexture.active=oldRT;if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(go);}
 }
}
