using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class SvinkiExpansionVerification
{
    const string Report="ArtSource/GameplayExpansion/validation.json";
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static GrayboxPlayerController Player()=>UnityEngine.Object.FindObjectsByType<GrayboxPlayerController>().Single(p=>p.GetComponent<NetworkPlayer>()==null || p.GetComponent<NetworkPlayer>().IsOwner);
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Save(string key,object data){var report=File.Exists(Report)?JObject.Parse(File.ReadAllText(Report)):new JObject();report[key]=JToken.FromObject(data);File.WriteAllText(Report,report.ToString());return report.ToString();}
    public static string SavedScene()
    {
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);var roots=scene.GetRootGameObjects();
            var surface=roots.SelectMany(g=>g.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>(true)).Single();
            Assert(surface.navMeshData!=null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(surface.navMeshData)),"Baked navigation not saved as an asset.");
            var agents=roots.SelectMany(g=>g.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)).ToArray();
            Assert(agents.All(a=>a.agentTypeID==surface.agentTypeID && (a.GetComponent<Rigidbody>()==null || a.GetComponent<Rigidbody>().isKinematic)),"Agent settings conflict.");
            var points=new[]{new Vector3(-22,0,55),new Vector3(-6,0,55),new Vector3(10,0,55),new Vector3(26.5f,0,55)};
            foreach(var point in points){var path=new UnityEngine.AI.NavMeshPath();Assert(UnityEngine.AI.NavMesh.CalculatePath(new Vector3(0,0,-3),point,UnityEngine.AI.NavMesh.AllAreas,path) && path.status==UnityEngine.AI.NavMeshPathStatus.PathComplete,"New room unreachable after reload.");}
            var field=typeof(FishNet.Object.NetworkObject).GetField("SceneId",Private);
            var ids=roots.SelectMany(g=>g.GetComponentsInChildren<FishNet.Object.NetworkObject>(true)).Select(n=>(ulong)field.GetValue(n)).ToArray();
            Assert(ids.Length==ids.Distinct().Count() && ids.All(i=>i!=0),"Duplicate or unset scene IDs.");
            var pickups=roots.SelectMany(g=>g.GetComponentsInChildren<ClothingPickup>(true)).ToArray();
            foreach(var pickup in pickups)
            {
                Assert(UnityEngine.AI.NavMesh.SamplePosition(pickup.transform.position-Vector3.up*1.12f,out var hit,1.5f,UnityEngine.AI.NavMesh.AllAreas),"No navmesh near clothing.");
                var path=new UnityEngine.AI.NavMeshPath();Assert(UnityEngine.AI.NavMesh.CalculatePath(new Vector3(0,0,-3),hit.position,UnityEngine.AI.NavMesh.AllAreas,path) && path.status==UnityEngine.AI.NavMeshPathStatus.PathComplete,"Clothing unreachable after reload.");
            }
            return Save("savedLevel",new{rooms=14,footprintScale=1.25f,newRoomRoutes=4,reachableClothing=pickups.Length,uniqueSceneIDs=ids.Length,navMeshAsset=AssetDatabase.GetAssetPath(surface.navMeshData),agents=agents.Length,throws=roots.Sum(g=>g.GetComponentsInChildren<ThrowableMannequin>(true).Length)});
        }
        finally { UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true); }
    }
    public static string Offline()
    {
        Assert(NetworkLobby.Instance.Offline && !NetworkLobby.Instance.Busy,"Offline scene not loaded.");
        var player=Player();var motion=player.GetComponentInChildren<PigMotion>();
        Assert(motion.UsesLegacyClips,"Pig has no legacy clip bindings.");
        var sourceRest=player.GetComponent<PlayerAnimation>();
        Assert(player.GetComponentInChildren<PigAppearance>().ModelRoot.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"Head","Spine","Hips","Foot_L","Foot_R"}.Contains(t.name)).All(t=>sourceRest.RestPose(t,out _)),"Garments cannot follow pig rest poses.");
        var driver=player.transform.Find("Legacy Animation Driver");var animator=driver.GetComponent<Animator>();
        Assert(driver.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),"Driver mesh visible.");
        var bones=motion.GetComponent<PigAppearance>().ModelRoot.GetComponentsInChildren<Transform>(true);
        Transform leg=bones.Single(t=>t.name=="Thigh_L"),arm=bones.Single(t=>t.name=="UpperArm_L");
        var animate=typeof(PigMotion).GetMethod("LateUpdate",Private);
        animator.SetBool("Grounded",true);animator.SetFloat("Crouch",0);animator.SetFloat("Speed",0);animator.SetLayerWeight(1,0);
        animator.Play("Locomotion",0,0);animator.Update(0);animate.Invoke(motion,null);Quaternion idleLeg=leg.rotation,idleArm=arm.rotation;
        animator.SetFloat("Speed",2.5f);animator.Play("Locomotion",0,.25f);animator.Update(0);animate.Invoke(motion,null);
        float walk=Quaternion.Angle(idleLeg,leg.rotation);
        animator.SetFloat("Speed",0);animator.Play("Locomotion",0,0);animator.SetLayerWeight(1,1);animator.Update(0);animate.Invoke(motion,null);
        float torch=Quaternion.Angle(idleArm,arm.rotation);
        Assert(walk>3,"Walk clip did not move pig legs: "+walk);Assert(torch>15,"Torch clip did not raise pig arm: "+torch);
        animator.SetLayerWeight(1,0);animator.SetFloat("Speed",0);animator.Play("Locomotion",0,0);animator.Update(0);
        float sensitivity=MouseSettings.Multiplier;
        var camera=Camera.main.GetComponent<GrayboxFirstPersonCamera>();float before=camera.MouseSensitivity, changed;
        try { MouseSettings.Set(2); changed=camera.MouseSensitivity; Assert(Mathf.Approximately(changed,before/sensitivity*2),"Mouse sensitivity not live."); }
        finally { MouseSettings.Set(sensitivity); }
        var item=UnityEngine.Object.FindObjectsByType<ClothingPickup>().First().GetComponent<PickupItem>();
        var renderer=item.GetComponentsInChildren<Renderer>().First(r=>r.enabled && !(r is ParticleSystemRenderer));var ordinary=renderer.sharedMaterials;
        item.SetTargeted(true);Assert(item.IsTargeted,"Targeted clothes did not highlight.");Assert(renderer.sharedMaterials[0]!=ordinary[0],"Target material missing.");
        item.SetTargeted(false);Assert(!item.IsTargeted && renderer.sharedMaterials.SequenceEqual(ordinary),"Highlight did not reset.");
        item.SetTargeted(true);item.SetAvailable(false);Assert(!item.IsTargeted,"Hidden item still highlighted.");item.SetAvailable(true);
        int checks=0;var appearance=player.GetComponentInChildren<PigAppearance>();int original=appearance.FaceCode;
        for(int tattoo=0;tattoo<PigFace.TattooCount;tattoo++)for(int pattern=0;pattern<PigFace.PatternCount;pattern++)for(int color=0;color<PigFace.ColorCount;color++)
        {
            var face=new PigFace{tattoo=tattoo,pattern=pattern,skinColor=color,glassesStyle=6,mohawk=true,nosePiercing=true,hairColor=7,patternColor=3};int code=face.Encode();
            Assert(PigFace.Sanitize(code)==code && PigFace.Decode(code).tattoo==tattoo,"Tattoo lost in encoding.");
            Assert(JsonUtility.FromJson<SessionRequest>(JsonUtility.ToJson(new SessionRequest{Appearance=code})).Appearance==code,"Tattoo lost in network payload.");
            appearance.Apply(code);var skin=appearance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Body");var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block,0);
            Assert(block.GetFloat("_Tattoo")==tattoo,"Tattoo shader property missing.");checks++;
        }
        appearance.Apply(original);
        return Save("offline",new{legacyWalkAngle=walk,legacyTorchArmAngle=torch,liveMouseSensitivity=changed,targetHighlight=true,tattooCombinations=checks});
    }
    public static string Host()
    {
        PigAppearanceVerification.VerifyNetworkSpawn();var player=Player();
        Assert(player.GetComponent<NetworkPlayer>().IsServerInitialized,"Host not authoritative.");
        var face=PigFace.Decode(player.GetComponentInChildren<PigAppearance>().FaceCode);
        Assert(face.tattoo==1 && player.GetComponentInChildren<PigMotion>().UsesLegacyClips,"Network avatar lost customization or animation.");
        return Save("localhostHost",new{spawned=true,tattoo=face.tattoo,legacyAnimation=true,mannequins=UnityEngine.Object.FindObjectsByType<ThrowableMannequin>().Length,clothes=UnityEngine.Object.FindObjectsByType<ClothingPickup>().Length});
    }
    public static string TargetRaycast()
    {
        var camera=Camera.main;var interactor=camera.GetComponent<PlayerPickupInteractor>();
        var update=typeof(PlayerPickupInteractor).GetMethod("Update",Private);
        var item=UnityEngine.Object.FindObjectsByType<ClothingPickup>().First(c=>c.name.StartsWith("North wing clothing")).GetComponent<PickupItem>();
        var collider=item.GetComponent<Collider>();Pose old=new Pose(camera.transform.position,camera.transform.rotation);
        GameObject blocker=null;
        try
        {
            Vector3 point=collider.bounds.center;camera.transform.position=point+Vector3.back*2;camera.transform.LookAt(point);
            Physics.SyncTransforms();update.Invoke(interactor,null);Assert(item.IsTargeted,"Aimed clothing not highlighted by interaction ray.");
            blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Temporary occlusion test";blocker.transform.position=point+Vector3.back;blocker.transform.localScale=Vector3.one*.6f;
            Physics.SyncTransforms();update.Invoke(interactor,null);Assert(!item.IsTargeted,"Clothing highlighted through wall.");
            UnityEngine.Object.DestroyImmediate(blocker);blocker=null;Physics.SyncTransforms();camera.transform.rotation=Quaternion.LookRotation(Vector3.left);
            update.Invoke(interactor,null);Assert(!item.IsTargeted,"Clothing stays highlighted when looking away.");
            return Save("interactionRay",new{aimHighlight=true,wallOcclusion=true,lookAwayClears=true});
        }
        finally { if(blocker!=null)UnityEngine.Object.DestroyImmediate(blocker);camera.transform.SetPositionAndRotation(old.position,old.rotation);update.Invoke(interactor,null); }
    }
    public static string BeginFall()
    {
        var player=Player();player.GetComponent<CharacterController>().enabled=false;player.transform.position=new Vector3(10,.05f,55);player.GetComponent<CharacterController>().enabled=true;
        Assert(player.GetComponent<PlayerKnockdown>().TryFall(Vector3.forward),"Fall rejected.");
        var bodies=player.gameObject.scene.GetRootGameObjects().Single(g=>g.name=="Player ragdoll physics").GetComponentsInChildren<Rigidbody>();
        Assert(bodies.Length==11 && bodies.All(b=>!b.isKinematic),"Ragdoll bodies not dynamic.");
        float variation=bodies.Select(b=>b.angularVelocity).Distinct().Count();Assert(variation>=9,"Limb impulses not varied.");
        return Save("fallStart",new{bodies=bodies.Length,variedImpulses=variation,joints=bodies.Count(b=>b.GetComponent<ConfigurableJoint>()!=null)});
    }
    public static string Recovered()
    {
        var player=Player();var ragdoll=player.GetComponent<PlayerRagdoll>();var fall=player.GetComponent<PlayerKnockdown>();
        var bodies=player.gameObject.scene.GetRootGameObjects().Single(g=>g.name=="Player ragdoll physics").GetComponentsInChildren<Rigidbody>();
        Assert(!fall.IsDown && !ragdoll.IsRecovering,"Ragdoll did not recover.");Assert(bodies.All(b=>b.isKinematic && float.IsFinite(b.position.x) && float.IsFinite(b.position.y) && b.position.y>-.5f),"Unstable physics or below floor.");
        Assert(bodies.Where(b=>b.GetComponent<ConfigurableJoint>()!=null).All(b=>b.GetComponent<ConfigurableJoint>().connectedBody!=null),"Detached joint.");
        var mannequin=UnityEngine.Object.FindObjectsByType<ThrowableMannequin>().Where(t=>t.name.StartsWith("Store display expansion ")).OrderBy(t=>Vector3.Distance(t.transform.position,player.transform.position)).First();
        player.GetComponent<CharacterController>().enabled=false;player.transform.position=mannequin.transform.position+Vector3.back*2;player.GetComponent<CharacterController>().enabled=true;
        typeof(GrayboxFirstPersonCamera).GetMethod("LateUpdate",Private).Invoke(Camera.main.GetComponent<GrayboxFirstPersonCamera>(),null);
        Assert(mannequin.TryGrab(player.GetComponent<PlayerAvatar>()),"Expanded display cannot be grabbed.");Assert(mannequin.Release(true),"Expanded display cannot be thrown.");
        var body=mannequin.GetComponent<Rigidbody>();Assert(!body.isKinematic && body.linearVelocity.magnitude>10,"Throw has no impulse.");
        return Save("recoveryAndThrow",new{recovered=true,stableBodies=true,heldThenThrown=mannequin.name,throwSpeed=body.linearVelocity.magnitude,mannequins=UnityEngine.Object.FindObjectsByType<ThrowableMannequin>().Length});
    }
}
