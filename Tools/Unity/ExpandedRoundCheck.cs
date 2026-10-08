using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

public static class ExpandedRoundCheck
{
    private const string Key="Svinki.ExpandedRound.Check";
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    private static void Move(PlayerAvatar avatar,Vector3 point)
    {
        var controller=avatar.GetComponent<CharacterController>();bool enabled=controller.enabled;controller.enabled=false;avatar.transform.position=point;controller.enabled=enabled;
    }
    public static string SavedScene()
    {
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        if(!scene.IsValid()||!scene.isLoaded)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
        var roots=scene.GetRootGameObjects();
        var floor=roots.SelectMany(g=>g.GetComponentsInChildren<BoxCollider>(true)).Where(b=>b.name=="Floor"&&b.bounds.center.y<.3f).ToArray();
        double area=floor.Sum(b=>(double)b.bounds.size.x*b.bounds.size.z);
        Assert(Math.Abs(area/4362.5-3)<.0001,"Floor area not three times original");
        Assert(roots.Sum(g=>g.GetComponentsInChildren<ShoppingCart>(true).Length)==3,"Not exactly three carts");
        Assert(roots.Sum(g=>g.GetComponentsInChildren<MannequinBrain>(true).Length)==12,"Not twelve stalkers");
        var wing=roots.Single(g=>g.name=="Graybox Level").transform.Find("North departments - triple area");
        foreach(Transform room in wing)
        {
            var point=room.Find("Floor").GetComponent<BoxCollider>().bounds.center;point.y=0;
            var path=new NavMeshPath();Assert(NavMesh.CalculatePath(new Vector3(0,0,-3),point,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"No route to "+room.name);
        }
        var ids=roots.SelectMany(g=>g.GetComponentsInChildren<FishNet.Object.NetworkObject>(true)).Select(n=>(ulong)typeof(FishNet.Object.NetworkObject).GetField("SceneId",Private).GetValue(n)).ToArray();
        Assert(ids.Distinct().Count()==ids.Length&&ids.All(n=>n!=0),"Duplicate scene network IDs");
        var station=roots.SelectMany(g=>g.GetComponentsInChildren<RoundFinishStation>(true)).Single();
        Assert(station.transform.Find("Stand")==null&&Mathf.Abs(station.transform.position.x+6.03f)<.01f,"Ready is not a wall button");
        Assert(!ShaderUtil.ShaderHasError(Shader.Find("Svinki/Vivid Cloth")),"Clothing shader has errors");
        string result="PASS: area="+area+" (x3), 3 carts, 12 stalkers, 16 connected new rooms, "+ids.Length+" unique network IDs, wall READY, shader valid";
        Directory.CreateDirectory("ArtSource/ExpandedRound");File.WriteAllText("ArtSource/ExpandedRound/scene-validation.txt",result);return result;
    }
    public static string Offline()
    {
        var lobby=NetworkLobby.Instance;Assert(lobby!=null&&!lobby.InSession&&!lobby.Offline,"Start from menu");
        SessionState.SetString(Key,"running");lobby.StartCoroutine(OfflineChecks());return "Offline checks running";
    }
    private static IEnumerator OfflineChecks()
    {
        var lobby=NetworkLobby.Instance;lobby.StartOffline();
        float deadline=Time.realtimeSinceStartup+30;
        while((lobby.Busy||!PlayerRegistry.Players.Any(p=>p.IsLocal))&&Time.realtimeSinceStartup<deadline)yield return null;
        yield return new WaitForSecondsRealtime(.5f);
        try{PrepareCargo();}catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(.25f);
        try{CargoChecks();}catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(.3f);
        try{StartFall();}catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(.45f);
        try{InspectFall();}catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(3.4f);
        try{SecondHit();}catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(2.8f);
        try
        {
            var player=PlayerRegistry.Players.First(p=>p.IsLocal);var life=player.GetComponent<PlayerKnockdown>();
            Assert(life.IsDead&&life.IsDown&&lobby.IsSpectator&&!lobby.InputAllowed,"Eliminated player recovered or can move");
            Assert(player.Outfit.IsEmpty,"Death did not drop clothes");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/ExpandedRound/offline-death.png"));
            File.AppendAllText("ArtSource/ExpandedRound/runtime-validation.txt","PASS: offline two hits, no repeat hit during knockdown, full pig rig physics, corpse stays down, clothes dropped, restart visible\n");
            lobby.RestartOfflineRound();
        }
        catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(2.5f);
        try
        {
            var player=PlayerRegistry.Players.First(p=>p.IsLocal);
            Assert(player.IsAlive&&player.GetComponent<PlayerKnockdown>().Hits==0,"Offline restart did not reset lives");
            Assert(ShoppingCart.All.Count==3&&ShoppingCart.All.All(c=>c.GetComponent<CartCargo>().Items.Count==0),"New round cargo not reset");
            SessionState.SetString(Key,"PASS: offline cargo, ragdoll, elimination, restart");
            File.AppendAllText("ArtSource/ExpandedRound/runtime-validation.txt","PASS: new round resets hits and cart cargo\n");
        }
        catch(Exception e){Fail(e);}
    }
    private static PlayerAvatar Player()=>PlayerRegistry.Players.First(p=>p.IsLocal);
    private static void PrepareCargo()
    {
        Assert(NetworkLobby.Instance.Offline&&!NetworkLobby.Instance.Busy,"Offline load timed out");
        foreach(var brain in UnityEngine.Object.FindObjectsByType<MannequinBrain>())brain.enabled=false;
        foreach(var thief in UnityEngine.Object.FindObjectsByType<ThiefBrain>())thief.enabled=false;
        var player=Player();player.Outfit.SetItems(Array.Empty<ClothingDefinition>());
        var cart=ShoppingCart.All.OrderBy(c=>c.name).First();
        Move(player,cart.transform.position-cart.transform.forward*2.2f+Vector3.up*.05f);
        var view=Camera.main.GetComponent<GrayboxFirstPersonCamera>();view.SetTarget(player.transform);
        typeof(GrayboxFirstPersonCamera).GetField("yaw",Private).SetValue(view,0f);
        typeof(GrayboxFirstPersonCamera).GetField("pitch",Private).SetValue(view,20f);
    }
    private static void CargoChecks()
    {
        var player=Player();var cart=ShoppingCart.All.OrderBy(c=>c.name).First();var cargo=cart.GetComponent<CartCargo>();
        Assert(cargo.CanAccess(player),"Cannot access nearby cart");
        for(int i=0;i<8;i++)
        {
            var pickup=ClothingPickup.All.First(p=>p.IsOnFloor&&p.Clothing!=null);
            pickup.transform.position=player.Position+new Vector3(.8f,.75f,.7f);Physics.SyncTransforms();
            Assert(cargo.Transfer(player,CargoAction.LoadPickup,0,cargo.Revision,pickup),"Cannot load floor clothes "+i);
            Assert(!pickup.Item.IsAvailable,"Loaded pickup still available");
        }
        var extra=ClothingPickup.All.First(p=>p.IsOnFloor&&p.Clothing!=null);extra.transform.position=player.Position+new Vector3(.8f,.75f,.7f);Physics.SyncTransforms();
        Assert(!cargo.Transfer(player,CargoAction.LoadPickup,0,cargo.Revision,extra)&&extra.IsOnFloor,"Full cart consumed ninth item");
        int revision=cargo.Revision;Assert(cargo.Transfer(player,CargoAction.Equip,0,revision),"Cannot wear cargo");
        Assert(!cargo.Transfer(player,CargoAction.Equip,0,revision),"Stale transfer duplicated cargo");
        var equipped=player.Outfit.Items.Single();Assert(cargo.Transfer(player,CargoAction.StoreWorn,(int)equipped.Slot,cargo.Revision),"Cannot store worn garment");
        Assert(player.Outfit.IsEmpty&&cargo.Items.Count==8,"Worn transfer corrupted inventory");
        Assert(cargo.Transfer(player,CargoAction.Unload,0,cargo.Revision)&&cargo.Items.Count==7,"Cannot unload cargo");
        Assert(cart.TryUse(player,false),"Cannot push cart");cart.Release(player,false);Assert(player.GetComponent<CharacterController>().enabled,"Cart exit leaves controller disabled");
        Move(player,cart.transform.position-cart.transform.forward*2.2f+Vector3.up*.05f);CartCargoView.Instance.Open(cargo);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/ExpandedRound/cargo.png"));CartCargoView.Instance.Close();
        File.WriteAllText("ArtSource/ExpandedRound/runtime-validation.txt","PASS: cargo eight items, capacity rejection, stale revision rejection, store/wear/unload, no item duplication, cart push/release\n");
    }
    private static void StartFall()
    {
        var player=Player();Move(player,new Vector3(0,.05f,5));
        var definitions=AssetDatabase.FindAssets("t:ClothingDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ClothingDefinition>).Where(c=>c!=null&&c.PigRigged).GroupBy(c=>c.Slot).Select(g=>g.First()).ToArray();
        player.Outfit.SetItems(definitions);
        Assert(player.GetComponent<PlayerKnockdown>().TryMannequinHit(Vector3.forward),"First NPC hit failed");
        Assert(!player.GetComponent<PlayerKnockdown>().TryMannequinHit(Vector3.forward),"Same attack repeated damage");
    }
    private static void InspectFall()
    {
        var player=Player();var ragdoll=player.GetComponent<PlayerRagdoll>();var life=player.GetComponent<PlayerKnockdown>();
        Assert(life.Hits==1&&life.IsDown&&ragdoll.BodyCount==11,"First hit did not create ragdoll");
        var pig=player.GetComponentInChildren<PigAppearance>();var skin=pig.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>s.name=="Body");
        var parts=(IEnumerable)typeof(PlayerRagdoll).GetField("parts",Private).GetValue(ragdoll);
        foreach(var part in parts)
        {
            var bone=(Transform)part.GetType().GetField("Bone").GetValue(part);
            Assert(skin.bones.Contains(bone),"Ragdoll drives hidden clothing rig: "+bone.name);
        }
        Assert(life.HeadPosition.y<1.5f,"Fall did not lower the pig head");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/ExpandedRound/offline-fall.png"));
    }
    private static void SecondHit()
    {
        var life=Player().GetComponent<PlayerKnockdown>();Assert(life.CanFall,"Did not recover from first hit");
        Assert(life.TryMannequinHit(Vector3.back)&&life.IsDead,"Second hit did not eliminate player");
    }
    private static void Fail(Exception error){SessionState.SetString(Key,"FAIL: "+error.Message);Debug.LogException(error);File.AppendAllText("ArtSource/ExpandedRound/runtime-validation.txt","FAIL: "+error+"\n");}
}
