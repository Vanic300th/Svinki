using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public static class ExpandedRoundNetworkCheck
{
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static NetworkPlayer Host()=>PlayerRegistry.Players.Where(p=>p.IsLocal).Select(p=>p.GetComponent<NetworkPlayer>()).Single();
    private static NetworkPlayer Guest()=>PlayerRegistry.Players.Where(p=>!p.IsLocal).Select(p=>p.GetComponent<NetworkPlayer>()).Single();
    private static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Move(PlayerAvatar avatar,Vector3 point){var cc=avatar.GetComponent<CharacterController>();cc.enabled=false;avatar.transform.position=point;cc.enabled=true;}
    public static string Prepare()
    {
        Assert(NetworkLobby.Instance.Snapshot.phase==SessionPhase.Round&&PlayerRegistry.Players.Count==2,"Two-player round required");
        var definitions=AssetDatabase.FindAssets("t:ClothingDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ClothingDefinition>).Where(c=>c!=null&&c.PigRigged).GroupBy(c=>c.Slot).Select(g=>g.First()).ToArray();
        Host().RestoreOutfit(definitions.Select(c=>c.name).ToArray());Guest().RestoreOutfit(definitions.Select(c=>c.name).ToArray());
        return "Both players equipped; host and client ready for cargo tests";
    }
    public static string ThrowAtHost()
    {
        var host=Host().GetComponent<PlayerAvatar>();Move(host,new Vector3(0,.05f,5));
        var source=UnityEngine.Object.FindObjectsByType<ThrowableMannequin>().First(t=>t.HasAuthority&&!t.IsHeld);
        var body=source.GetComponent<Rigidbody>();body.position=host.Position+Vector3.back*2+Vector3.up*.35f;body.rotation=Quaternion.identity;
        typeof(ThrowableMannequin).GetField("thrower",Private).SetValue(source,Guest().GetComponent<PlayerAvatar>());
        typeof(ThrowableMannequin).GetField("dangerousUntil",Private).SetValue(source,Time.time+2);
        body.isKinematic=false;body.linearVelocity=Vector3.forward*10;body.angularVelocity=Vector3.zero;body.WakeUp();
        NetworkLobby.Instance.StartCoroutine(CheckThrownFall());return "Real physics mannequin launched into host";
    }
    private static IEnumerator CheckThrownFall()
    {
        yield return new WaitForSecondsRealtime(.5f);
        try
        {
            var host=Host();var life=host.GetComponent<PlayerKnockdown>();Assert(life.IsDown,"Thrown mannequin did not knock host down");
            Assert(host.HitsTaken==0,"Thrown display incorrectly counted as lethal NPC attack");
            var ragdoll=host.GetComponent<PlayerRagdoll>();Assert(ragdoll.BodyCount==11,"Host ragdoll not built");
            var parts=(IEnumerable)typeof(PlayerRagdoll).GetField("parts",Private).GetValue(ragdoll);
            var pig=host.GetComponentInChildren<PigAppearance>();var skin=pig.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>s.name=="Body");
            foreach(var part in parts){var bone=(Transform)part.GetType().GetField("Bone").GetValue(part);Assert(skin.bones.Contains(bone),"Host uses unused clothes bone");}
            Assert(life.HeadPosition.y-host.transform.position.y<.9f,"Host has no visible fall pose");
            File.AppendAllText("ArtSource/ExpandedRound/network-validation.txt","PASS: actual physics throw hits host; host pig uses 11 real body bones; camera/body fall; display throws do not consume lives\n");
            UnityEditor.SessionState.SetString("Svinki.ExpandedRound.Throw","PASS");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/ExpandedRound/host-fall.png"));
        }catch(Exception e){UnityEditor.SessionState.SetString("Svinki.ExpandedRound.Throw","FAIL "+e.Message);Debug.LogException(e);}
    }
    public static string CombatHost()
    {
        NetworkLobby.Instance.StartCoroutine(CombatChecks());return "Host NPC combat, death and spectator tests running";
    }
    private static IEnumerator CombatChecks()
    {
        var host=Host();var avatar=host.GetComponent<PlayerAvatar>();var life=host.GetComponent<PlayerKnockdown>();
        float deadline=Time.realtimeSinceStartup+5;
        while(!life.CanFall&&Time.realtimeSinceStartup<deadline)yield return null;
        var brain=UnityEngine.Object.FindObjectsByType<MannequinBrain>().First();
        var agent=brain.GetComponent<UnityEngine.AI.NavMeshAgent>();agent.enabled=true;
        agent.Warp(avatar.Position+Vector3.forward*1.1f);brain.SetTarget(host.transform);brain.enabled=true;
        deadline=Time.realtimeSinceStartup+7;
        while(life.Hits==0&&Time.realtimeSinceStartup<deadline)yield return null;
        brain.enabled=false;agent.isStopped=true;
        try{Assert(life.Hits==1&&life.IsDown,"Actual NPC first hit failed");Assert(host.HitsTaken==1,"Host hit SyncVar wrong");}
        catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(.5f);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/ExpandedRound/host-first-hit.png"));
        yield return new WaitForSecondsRealtime(3);
        brain.enabled=true;deadline=Time.realtimeSinceStartup+8;
        while(!life.IsDead&&Time.realtimeSinceStartup<deadline)yield return null;
        brain.enabled=false;agent.isStopped=true;
        try{Assert(host.IsDead,"Actual NPC second lethal hit failed");}
        catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(.6f);
        try
        {
            Assert(NetworkLobby.Instance.Snapshot.phase==SessionPhase.Round,"Host death stopped round");
            Assert(NetworkLobby.Instance.IsHost&&NetworkLobby.Instance.IsEliminated&&NetworkLobby.Instance.IsSpectator,"Host not spectating");
            Assert(SpectatorCamera.CurrentTarget==Guest(),"Spectator selected dead host");
            Assert(avatar.Outfit.IsEmpty&&!NetworkLobby.Instance.InputAllowed,"Dead host can act or retained clothes");
            File.AppendAllText("ArtSource/ExpandedRound/network-validation.txt","PASS: actual NPC attacks cause host two hits; death keeps server and round alive; spectator targets living guest; input blocked and clothes dropped\n");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/ExpandedRound/host-spectator.png"));SessionState.SetString("Svinki.ExpandedRound.Combat","PASS");
        }catch(Exception e){Fail(e);}
    }
    public static string KillGuest()
    {
        NetworkLobby.Instance.StartCoroutine(GuestChecks());return "Guest combat and all-dead round completion running";
    }
    private static IEnumerator GuestChecks()
    {
        var guest=Guest();var life=guest.GetComponent<PlayerKnockdown>();
        try{Assert(life.TryMannequinHit(Vector3.forward),"Guest first hit failed");}
        catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(3.5f);
        try{Assert(life.TryMannequinHit(Vector3.back)&&guest.IsDead,"Guest second hit failed");}
        catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(.7f);
        try
        {
            Assert(NetworkLobby.Instance.Snapshot.phase==SessionPhase.Results&&NetworkLobby.Instance.Results.entries.Length==2,"All-dead round did not complete with both results");
            Assert(NetworkLobby.Instance.Snapshot.players.All(p=>p.eliminated),"Elimination not published");
            File.AppendAllText("ArtSource/ExpandedRound/network-validation.txt","PASS: guest two hits; everyone dead completes round; results retain both participants\n");SessionState.SetString("Svinki.ExpandedRound.AllDead","PASS");
        }catch(Exception e){Fail(e);}
    }
    private static void Fail(Exception e){SessionState.SetString("Svinki.ExpandedRound.Combat","FAIL "+e.Message);File.AppendAllText("ArtSource/ExpandedRound/network-validation.txt","FAIL: "+e+"\n");Debug.LogException(e);}
}
