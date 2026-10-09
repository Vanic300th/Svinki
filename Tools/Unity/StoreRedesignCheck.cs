using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class StoreRedesignCheck
{
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable]private class ReachReport{public int count,reachable;public string[] unreachable;}
    private static Scene Scene()=>SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
    private static T[] All<T>()where T:Component=>Scene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    private static void Assert(bool b,string why){if(!b)throw new Exception(why);}
    private static bool Reachable(ClothingPickup item,out Vector3 stand)
    {
        var collider=item.GetComponent<BoxCollider>();Vector3 target=collider.bounds.center;var physics=Scene().GetPhysicsScene();
        for(int d=0;d<24;d++)foreach(float distance in new[]{1.3f,1.9f,2.6f})
        {
            float a=d*Mathf.PI/12;Vector3 desired=item.transform.position+new Vector3(Mathf.Cos(a)*distance,0,Mathf.Sin(a)*distance);
            if(!NavMesh.SamplePosition(desired,out var nav,.7f,NavMesh.AllAreas))continue;
            var path=new NavMeshPath();if(!NavMesh.CalculatePath(new Vector3(0,0,-3),nav.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            foreach(float height in new[]{1.55f,.72f})
            {Vector3 eye=nav.position+Vector3.up*height,delta=target-eye;if(delta.magnitude>3.5f)continue;
                if(physics.Raycast(eye,delta.normalized,out var hit,delta.magnitude+.03f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide)&&hit.collider==collider){stand=nav.position;return true;}}
        }
        stand=default;return false;
    }
    public static string ExportLayout()
    {
        var floors=All<BoxCollider>().Where(b=>b.name=="Floor"&&b.bounds.center.y<.3f).OrderBy(b=>b.transform.parent.name).ToArray();
        string N(float v)=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
        var roomJson=floors.Select(b=>"{\"name\":\""+b.transform.parent.name+"\",\"x0\":"+N(b.bounds.min.x)+",\"x1\":"+N(b.bounds.max.x)+",\"z0\":"+N(b.bounds.min.z)+",\"z1\":"+N(b.bounds.max.z)+"}");
        var layout=new SerializedObject(All<RoundClothingLayout>().Single());var poses=layout.FindProperty("positions");var angles=layout.FindProperty("rotations");var slots=new List<string>();
        for(int i=0;i<poses.arraySize;i++){var p=poses.GetArrayElementAtIndex(i).vector3Value;slots.Add("{\"x\":"+N(p.x)+",\"y\":"+N(p.y)+",\"z\":"+N(p.z)+",\"yaw\":"+N(angles.GetArrayElementAtIndex(i).quaternionValue.eulerAngles.y)+"}");}
        File.WriteAllText("ArtSource/StoreRedesign/layout.json","{\"rooms\":["+string.Join(",",roomJson)+"],\"anchors\":["+string.Join(",",slots)+"]}");return "Exported 30 department bounds and 96 authored hiding slots";
    }
    public static string SavedScene()
    {
        var floors=All<BoxCollider>().Where(b=>b.name=="Floor"&&b.bounds.center.y<.3f).ToArray();Assert(floors.Length==30,"Department count");
        double area=floors.Sum(b=>(double)b.bounds.size.x*b.bounds.size.z);Assert(Math.Abs(area-13087.5)<.05,"Area changed unexpectedly: "+area);
        Assert(floors.Max(b=>b.bounds.max.x)-floors.Min(b=>b.bounds.min.x)>100,"Outline remains narrow rectangle");
        Assert(floors.Select(b=>Mathf.RoundToInt(b.bounds.size.x*100)).Distinct().Count()>8,"Departments still identical widths");
        Assert(All<ShoppingCart>().Length==3&&All<MannequinBrain>().Length==12,"Cart / attacker counts");
        var ids=All<NetworkObject>().Select(n=>(ulong)typeof(NetworkObject).GetField("SceneId",Private).GetValue(n)).ToArray();Assert(ids.All(i=>i!=0)&&ids.Distinct().Count()==ids.Length,"Network scene IDs");
        var clothes=All<ClothingPickup>();Assert(clothes.Length==96,"Clothing count");Physics.SyncTransforms();
        var fail=clothes.Where(c=>!Reachable(c,out _)).Select(c=>c.name+" @ "+c.transform.position).ToArray();
        var report=new ReachReport{count=clothes.Length,reachable=clothes.Length-fail.Length,unreachable=fail};
        Directory.CreateDirectory("ArtSource/StoreRedesign");File.WriteAllText("ArtSource/StoreRedesign/reachability.json",JsonUtility.ToJson(report,true));
        Assert(fail.Length==0,string.Join("; ",fail));
        return "PASS: 30 varied departments, original x3 floor area, 3 carts, 12 attackers, unique network IDs, all 96 hiding places reachable and visible to pickup ray";
    }
    public static string Shuffles()
    {
        var layout=All<RoundClothingLayout>().Single();var clothes=All<ClothingPickup>();var poses=clothes.Select(c=>new Pose(c.transform.position,c.transform.rotation)).ToArray();var failures=new List<int>();
        try{for(int i=0;i<12;i++){layout.Shuffle(i+730);Physics.SyncTransforms();if(clothes.Any(c=>!Reachable(c,out _)))failures.Add(i);}}
        finally{for(int i=0;i<clothes.Length;i++)clothes[i].transform.SetPositionAndRotation(poses[i].position,poses[i].rotation);Physics.SyncTransforms();}
        File.WriteAllText("ArtSource/StoreRedesign/shuffle-validation.txt","12 layouts / 1152 physical reachability checks; failed seeds="+string.Join(",",failures));Assert(failures.Count==0,"Shuffled garment variants blocked in a hiding place");return "PASS: 12 layouts, all 1152 garment placements accessible";
    }
    private static Keyboard keys;private static InputSettings original,test;private static HideFlags flags;
    private static void Press(params Key[] k)=>InputSystem.QueueStateEvent(keys,new KeyboardState(k));
    private static void Cleanup(){if(keys!=null)InputSystem.RemoveDevice(keys);if(original!=null){InputSystem.settings=original;original.hideFlags=flags;}if(test!=null)UnityEngine.Object.Destroy(test);}
    public static string Guide(){SessionState.SetString("Svinki.StoreDesign.Guide","RUNNING");NetworkLobby.Instance.StartCoroutine(GuideRun());return "Guide UI check running";}
    private static IEnumerator GuideRun()
    {
        var guide=HowToPlay.Instance;var lobby=NetworkLobby.Instance;
        original=InputSystem.settings;flags=original.hideFlags;original.hideFlags=HideFlags.DontUnloadUnusedAsset;test=UnityEngine.Object.Instantiate(original);test.hideFlags=HideFlags.HideAndDontSave;test.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=test;keys=InputSystem.AddDevice<Keyboard>("Guide test");keys.MakeCurrent();
        string error=null;bool proceeded=false;bool menuBefore=lobby.MenuVisible;
        guide.BeforeStart(()=>proceeded=true);yield return null;
        try{var wash=GameObject.Find("Guide backdrop").GetComponent<RectTransform>();var parent=wash.parent.GetComponent<RectTransform>();Assert(wash.rect.width>=parent.rect.width-1&&wash.rect.height>=parent.rect.height-1&&wash.rect.width>1000,"Guide backdrop does not cover the full canvas");Assert(HowToPlay.IsOpen&&!proceeded,"Guide did not defer starting");Assert(!lobby.InputAllowed,"Guide did not block game input");}catch(Exception e){error=error??e.Message;}
        for(int page=0;page<4&&error==null;page++)
        {
            guide.SelectPage(page);yield return null;Canvas.ForceUpdateCanvases();
            try
            {
                foreach(var t in GameObject.Find("Guide backdrop").GetComponentsInChildren<TMP_Text>())
                {t.ForceMeshUpdate();if(!t.gameObject.activeInHierarchy)continue;Assert(t.preferredHeight<=t.rectTransform.rect.height+3,"Text too tall: "+t.text);Assert(!t.isTextOverflowing,"Text overflow: "+t.text);}
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/StoreRedesign/guide-"+page+".png"));
            }catch(Exception e){error=error??e.Message;}
            yield return null;
        }
        guide.Continue();yield return null;
        try{Assert(proceeded&&guide.Acknowledged&&!HowToPlay.IsOpen,"Continue action failed");}catch(Exception e){error=error??e.Message;}
        Press(Key.F1);yield return null;yield return null;
        try{Assert(HowToPlay.IsOpen,"F1 did not reopen guide");}catch(Exception e){error=error??e.Message;}
        Press();yield return null;Press(Key.Escape);yield return null;yield return null;
        try{Assert(!HowToPlay.IsOpen&&lobby.MenuVisible==menuBefore,"Escape opened pause beneath guide");Assert(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Count(e=>e.enabled)==1,"More than one event system");}catch(Exception e){error=error??e.Message;}
        Press();Cleanup();File.WriteAllText("ArtSource/StoreRedesign/guide-validation.txt",error==null?"PASS: first start deferred; 4 readable pages; continue action; F1 reopen; Escape consumed; one EventSystem":"FAIL: "+error);SessionState.SetString("Svinki.StoreDesign.Guide",error==null?"PASS":"FAIL: "+error);
    }
    public static string CollectHidden()
    {
        var player=PlayerRegistry.Players.First(p=>p.IsLocal);foreach(var b in UnityEngine.Object.FindObjectsByType<MannequinBrain>())b.enabled=false;foreach(var b in UnityEngine.Object.FindObjectsByType<ThiefBrain>())b.enabled=false;
        var item=All<ClothingPickup>().First(c=>c.Item.IsAvailable&&c.transform.position.z>75&&c.transform.position.y<2&&Reachable(c,out _));Assert(Reachable(item,out var stand),"No pickup approach");
        var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=stand;cc.enabled=true;
        var view=player.EyeCamera.GetComponent<GrayboxFirstPersonCamera>();var direction=item.GetComponent<BoxCollider>().bounds.center-(player.Position+Vector3.up*1.65f);var e=Quaternion.LookRotation(direction).eulerAngles;typeof(GrayboxFirstPersonCamera).GetField("yaw",Private).SetValue(view,e.y);typeof(GrayboxFirstPersonCamera).GetField("pitch",Private).SetValue(view,Mathf.DeltaAngle(0,e.x));
        player.StartCoroutine(PickupKey(item));return "Testing real E pickup in redesigned wing";
    }
    private static IEnumerator PickupKey(ClothingPickup item)
    {
        original=InputSystem.settings;flags=original.hideFlags;original.hideFlags=HideFlags.DontUnloadUnusedAsset;test=UnityEngine.Object.Instantiate(original);test.hideFlags=HideFlags.HideAndDontSave;test.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=test;keys=InputSystem.AddDevice<Keyboard>("Hidden pickup test");keys.MakeCurrent();
        yield return new WaitForSecondsRealtime(.4f);ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/StoreRedesign/hidden-item.png"));yield return null;
        Press(Key.E);yield return null;yield return null;Press();yield return null;
        bool collected=!item.Item.IsAvailable&&PlayerRegistry.Players.First(p=>p.IsLocal).Outfit.Items.Contains(item.Clothing);Cleanup();File.WriteAllText("ArtSource/StoreRedesign/pickup-validation.txt",collected?"PASS: actual aimed E collected hidden garment and equipped it":"FAIL: actual E pickup");SessionState.SetString("Svinki.StoreDesign.Pickup",collected?"PASS":"FAIL");
    }
}
