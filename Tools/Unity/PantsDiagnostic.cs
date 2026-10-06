using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class PantsDiagnostic
{
    private static bool submeshes;
    private static bool after;
    public static object Submeshes(){submeshes=true;return Main();}
    public static object After(){after=true;return Main();}
    public static object Main()
    {
        var pants=OutfitRatingCatalog.Load().items.Where(p=>p.clothing.Slot==ClothingSlot.Legs).OrderBy(p=>p.key).ToArray();
        var report=new List<object>();
        var studio=new GameObject("Temporary pants diagnostic");studio.transform.position=new Vector3(14000,10000,14000);
        var camera=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(studio.transform,false);
        camera.enabled=false;camera.cullingMask=1<<30;camera.orthographic=true;camera.orthographicSize=1.12f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.23f,.28f);camera.farClipPlane=9;
        foreach(var pos in new[]{new Vector3(-2,3,3),new Vector3(2,2,2)}){var light=new GameObject("Light",typeof(Light)).GetComponent<Light>();light.transform.SetParent(studio.transform,false);light.transform.localPosition=pos;light.transform.LookAt(studio.transform.position+Vector3.up);light.type=LightType.Spot;light.range=9;light.spotAngle=105;light.intensity=9;light.cullingMask=1<<30;}
        var rt=new RenderTexture(240,360,24);rt.Create();camera.targetTexture=rt;
        var tile=new Texture2D(240,360,TextureFormat.RGB24,false);var gallery=new Texture2D(1440,1440,TextureFormat.RGB24,false);
        var active=RenderTexture.active;
        try
        {
            for(int p=0;p<3;p++)for(int mode=0;mode<2;mode++)
            {
                var pig=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Pigs/PigAvatar"),studio.transform);
                var appearance=pig.GetComponent<PigAppearance>();var root=appearance.ModelRoot;
                foreach(var animator in pig.GetComponentsInChildren<Animator>())animator.enabled=false;
                var bones=root.GetComponentsInChildren<Transform>(true);var rest=bones.ToDictionary(b=>b,b=>new Pose(b.localPosition,b.localRotation));
                var def=UnityEngine.Object.Instantiate(pants[p].clothing);if(mode==0){var so=new SerializedObject(def);so.FindProperty("pigWornModel").objectReferenceValue=null;so.ApplyModifiedPropertiesWithoutUndo();}
                var holder=new GameObject("Pants");PigClothingBinding.TryAttach(def,root,holder.transform);
                foreach(var part in pig.GetComponentsInChildren<Transform>(true))part.gameObject.layer=30;
                var skin=holder.GetComponentInChildren<SkinnedMeshRenderer>();
                if(submeshes){var m=UnityEngine.Object.Instantiate(skin.sharedMesh);for(int sub=0;sub<m.subMeshCount;sub++)if(sub!=mode)m.SetTriangles(Array.Empty<int>(),sub);skin.sharedMesh=m;}
                var weights=skin.sharedMesh.boneWeights;var totals=new float[skin.bones.Length];foreach(var w in weights){totals[w.boneIndex0]+=w.weight0;totals[w.boneIndex1]+=w.weight1;totals[w.boneIndex2]+=w.weight2;totals[w.boneIndex3]+=w.weight3;}
                report.Add(new {name=pants[p].key,prepared=mode==1,vertices=weights.Length,weights=skin.bones.Select((b,i)=>b.name+"="+(totals[i]/weights.Length).ToString("F4")).ToArray()});
                for(int pose=0;pose<4;pose++)
                {
                    foreach(var r in rest)r.Key.SetLocalPositionAndRotation(r.Value.position,r.Value.rotation);root.localScale=Vector3.one;
                    if(pose==1)foreach(var b in bones.Where(b=>b.name.StartsWith("Thigh_")))b.rotation=Quaternion.AngleAxis(b.name.EndsWith("L")?28:-28,root.right)*b.rotation;
                    if(pose==2)root.localScale=new Vector3(1.4f,.58f,1.28f);
                    camera.transform.localPosition=new Vector3(pose==3?-2.5f:0,1.02f,pose==3?-3.5f:3.7f);camera.transform.LookAt(studio.transform.position+Vector3.up*.95f);
                    camera.Render();RenderTexture.active=rt;tile.ReadPixels(new Rect(0,0,240,360),0,0);tile.Apply();gallery.SetPixels((p*2+mode)*240,(3-pose)*360,240,360,tile.GetPixels());
                }
                pig.SetActive(false);UnityEngine.Object.DestroyImmediate(pig);UnityEngine.Object.DestroyImmediate(def);
            }
            gallery.Apply();Directory.CreateDirectory("ArtSource/Clothing3D");File.WriteAllBytes("ArtSource/Clothing3D/"+(submeshes?"pants-submeshes.png":after?"pants-after.png":"pants-before.png"),gallery.EncodeToPNG());
            File.WriteAllText("ArtSource/Clothing3D/pants-diagnostic.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally{RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tile);UnityEngine.Object.DestroyImmediate(gallery);UnityEngine.Object.DestroyImmediate(studio);}
    }
}
