using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class PigAnimationCapture
{
    public static string Gallery()
    {
        var player=UnityEngine.Object.FindObjectsByType<GrayboxPlayerController>().Single(p=>p.GetComponent<NetworkPlayer>()==null || p.GetComponent<NetworkPlayer>().IsOwner);
        var pig=player.GetComponentInChildren<PigAppearance>();var motion=pig.GetComponent<PigMotion>();
        var animator=player.transform.Find("Legacy Animation Driver").GetComponent<Animator>();
        var parts=pig.GetComponentsInChildren<Transform>(true);int[] layers=parts.Select(t=>t.gameObject.layer).ToArray();foreach(var part in parts)part.gameObject.layer=31;
        var studio=new GameObject("Temporary animation capture");studio.transform.position=player.transform.position;
        var cameraObject=new GameObject("Animation camera",typeof(Camera));cameraObject.transform.SetParent(studio.transform,false);
        var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.20f,.26f);
        camera.fieldOfView=33;camera.nearClipPlane=.05f;camera.farClipPlane=8;
        camera.transform.localPosition=new Vector3(1.8f,1.05f,3.7f);camera.transform.LookAt(player.transform.position+Vector3.up*.98f);
        foreach(Vector3 v in new[]{new Vector3(-2,3,3),new Vector3(2,2,2)})
        {
            var go=new GameObject("Capture light",typeof(Light));go.transform.SetParent(studio.transform,false);go.transform.localPosition=v;go.transform.LookAt(player.transform.position+Vector3.up);
            var light=go.GetComponent<Light>();light.type=LightType.Spot;light.spotAngle=95;light.range=8;light.intensity=8;light.cullingMask=1<<31;
        }
        var target=new RenderTexture(400,640,24);target.Create();camera.targetTexture=target;
        var tile=new Texture2D(400,640,TextureFormat.RGB24,false);var gallery=new Texture2D(1600,640,TextureFormat.RGB24,false);
        var skins=pig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled).ToArray();
        var baked=new Mesh[skins.Length];var copies=new GameObject[skins.Length];
        for(int i=0;i<skins.Length;i++)
        {
            baked[i]=new Mesh();copies[i]=new GameObject("Baked animation part",typeof(MeshFilter),typeof(MeshRenderer));copies[i].transform.SetParent(studio.transform,false);copies[i].layer=31;
            copies[i].GetComponent<MeshFilter>().sharedMesh=baked[i];copies[i].GetComponent<MeshRenderer>().sharedMaterials=skins[i].sharedMaterials;
            var block=new MaterialPropertyBlock();
            for(int m=0;m<skins[i].sharedMaterials.Length;m++){skins[i].GetPropertyBlock(block,m);copies[i].GetComponent<MeshRenderer>().SetPropertyBlock(block,m);}
        }
        RenderTexture oldActive=RenderTexture.active;
        try
        {
            animator.SetBool("Grounded",true);animator.SetFloat("Crouch",0);
            for(int i=0;i<4;i++)
            {
                animator.SetFloat("Speed",i==1 || i==2 ?2.5f:0);animator.SetLayerWeight(1,i==3?1:0);
                animator.Play("Locomotion",0,i==1?.25f:i==2?.75f:0);animator.Update(0);
                typeof(PigMotion).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(motion,null);
                for(int n=0;n<skins.Length;n++)
                {
                    skins[n].BakeMesh(baked[n]);skins[n].enabled=false;
                    copies[n].transform.SetPositionAndRotation(skins[n].transform.position,skins[n].transform.rotation);copies[n].transform.localScale=skins[n].transform.lossyScale;
                }
                camera.Render();RenderTexture.active=target;tile.ReadPixels(new Rect(0,0,400,640),0,0);tile.Apply();gallery.SetPixels(i*400,0,400,640,tile.GetPixels());
            }
            gallery.Apply();File.WriteAllBytes("ArtSource/GameplayExpansion/animation.png",gallery.EncodeToPNG());return "Idle, two walk phases and previous torch arm clip captured.";
        }
        finally
        {
            for(int i=0;i<parts.Length;i++)parts[i].gameObject.layer=layers[i];
            for(int i=0;i<skins.Length;i++){skins[i].enabled=true;UnityEngine.Object.DestroyImmediate(baked[i]);}
            animator.SetFloat("Speed",0);animator.SetLayerWeight(1,0);animator.Play("Locomotion",0,0);animator.Update(0);
            RenderTexture.active=oldActive;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(tile);UnityEngine.Object.DestroyImmediate(gallery);UnityEngine.Object.DestroyImmediate(studio);
        }
    }
}
