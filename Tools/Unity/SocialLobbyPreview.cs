using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
public static class SocialLobbyPreview
{
    public static string Capture()
    {
        var scene=SceneManager.GetSceneByName("FriendslopLobby");
        if(!scene.IsValid())throw new Exception("SampleScene must be loaded");
        var lobby=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LobbyActivityManager>(true)).Single();
        var roofs=lobby.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Lobby ceiling"||r.name=="Lift ceiling").ToArray();
        var enabled=roofs.Select(r=>r.enabled).ToArray();var previousScene=SceneManager.GetActiveScene();
        var go=new GameObject("Lobby preview camera");SceneManager.MoveGameObjectToScene(go,scene);
        var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=10;
        camera.transform.position=new Vector3(0,22,-22);camera.transform.rotation=Quaternion.Euler(90,0,0);camera.nearClipPlane=.1f;camera.farClipPlane=35;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.06f,.065f);camera.aspect=1.6f;
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var target=RenderTexture.GetTemporary(1600,1000,24);var previous=RenderTexture.active;Texture2D image=null;
        try
        {
            foreach(var roof in roofs)roof.enabled=false;SceneManager.SetActiveScene(scene);
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new Exception("URP capture unavailable");
            RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;
            image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            string path="ArtSource/SocialLobby/layout-preview.png";File.WriteAllBytes(path,image.EncodeToPNG());return Path.GetFullPath(path);
        }
        finally
        {
            for(int i=0;i<roofs.Length;i++)roofs[i].enabled=enabled[i];SceneManager.SetActiveScene(previousScene);RenderTexture.active=previous;
            if(image!=null)UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
