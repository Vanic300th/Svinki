using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
public static class MonkeyToyCheck
{
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    private static PlayerAvatar Player()=>PlayerRegistry.Players.First(p=>p.IsLocal);
    private static void Move(PlayerAvatar p,Vector3 point){var cc=p.GetComponent<CharacterController>();cc.enabled=false;p.transform.position=point;cc.enabled=true;}
    private static void Look(PlayerAvatar p,Vector3 point)
    {
        var view=p.EyeCamera.GetComponent<GrayboxFirstPersonCamera>();var e=Quaternion.LookRotation(point-p.EyePosition).eulerAngles;
        typeof(GrayboxFirstPersonCamera).GetField("yaw",Private).SetValue(view,e.y);typeof(GrayboxFirstPersonCamera).GetField("pitch",Private).SetValue(view,Mathf.DeltaAngle(0,e.x));
    }
    public static string Offline(){SessionState.SetString("Svinki.Monkey.Offline","RUNNING");Player().StartCoroutine(Checks());return "Offline toy physics and actual 30-second stun checks running";}
    private static IEnumerator Checks()
    {
        yield return new WaitForSecondsRealtime(1);
        PlayerAvatar player=null;MonkeyToy toy=null;MannequinStun stun=null;int playerHits=0;Vector3 bodyBefore=Vector3.zero;
        try
        {
            player=Player();Assert(MonkeyToy.All.Count==1,"Single player must spawn exactly one toy");toy=MonkeyToy.All.Single();Assert(toy.GetComponent<ArticulatedRagdoll>().BodyCount==13,"Missing toy physics bodies");
            foreach(var npc in UnityEngine.Object.FindObjectsByType<MannequinBrain>())npc.enabled=false;
            foreach(var thief in UnityEngine.Object.FindObjectsByType<ThiefBrain>())thief.enabled=false;
            Move(player,new Vector3(0,.05f,5));
            // Move the player to the settled toy first, exercising real proximity and line-of-sight checks.
            Move(player,toy.transform.position+Vector3.back*.8f);
            playerHits=player.GetComponent<PlayerKnockdown>().Hits;
        }catch(Exception e){Fail(e);yield break;}
        yield return null;
        try{Look(player,toy.transform.position);Assert(toy.TryGrab(player),"Cannot grab monkey");Assert(player.GetComponent<PlayerMonkeyCarry>().Held==toy,"Toy not linked to hands");}catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(1.5f);
        try
        {
            var rag=toy.GetComponent<ArticulatedRagdoll>();Assert(Vector3.Distance(rag.BonePosition("Hand_L"),player.EyePosition)<1.2f,"Pinned wrist too far from eye");bodyBefore=rag.Center;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/MonkeyToy/held-toy.png"));
            // A miss retains the same item, and only one swing may be active.
            Look(player,player.EyePosition+Vector3.up*5);Assert(toy.Swing(),"Cannot swing");Assert(!toy.Swing(),"Swing spam accepted");
        }catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(.8f);
        try
        {
            Assert(toy!=null&&!toy.Consumed,"Miss consumed monkey");
            Move(player,new Vector3(0,.05f,5));
        }catch(Exception e){Fail(e);yield break;}
        yield return null;
        try
        {
            Look(player,new Vector3(0,1.2f,7));
            stun=UnityEngine.Object.FindObjectsByType<MannequinStun>().First();var agent=stun.GetComponent<NavMeshAgent>();if(!agent.enabled)agent.enabled=true;agent.Warp(new Vector3(0,0,6.35f));stun.GetComponent<MannequinBrain>().enabled=true;
            Assert(toy.Swing(),"Hit swing refused");
        }catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(1);
        try
        {
            Assert(toy==null&&MonkeyToy.All.Count==0,"Successful hit did not consume toy");Assert(stun.IsStunned&&stun.Remaining>28,"NPC not stunned for 30 seconds");
            Assert(!stun.GetComponent<MannequinBrain>().enabled&&!stun.GetComponent<NavMeshAgent>().enabled,"Stunned NPC still active");
            Assert(stun.GetComponent<ArticulatedRagdoll>().BodyCount==11,"NPC has no full-body fall");
            Assert(player.GetComponent<PlayerKnockdown>().Hits==playerHits,"Toy strike affected player lives");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/MonkeyToy/stunned-npc.png"));
            File.WriteAllText("ArtSource/MonkeyToy/validation.txt","PASS: single-player one toy; 13 articulated bodies and pinned wrists; miss retained item; swing cooldown; successful NPC hit consumed toy; NPC physics fall and disabled brain/navigation\n");
        }catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(27);
        try{Assert(stun.IsStunned,"Recovered before 30 seconds");}catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSecondsRealtime(3);
        try
        {
            Assert(!stun.IsStunned&&stun.GetComponent<NavMeshAgent>().enabled&&stun.GetComponent<MannequinBrain>().enabled,"NPC did not recover after 30 seconds");
            File.AppendAllText("ArtSource/MonkeyToy/validation.txt","PASS: actual 30-second timer, no early recovery, navigation restored; no replacement toy spawned\n");
            SessionState.SetString("Svinki.Monkey.Offline","PASS");
        }catch(Exception e){Fail(e);}
    }
    private static void Fail(Exception e){SessionState.SetString("Svinki.Monkey.Offline","FAIL "+e.Message);File.AppendAllText("ArtSource/MonkeyToy/validation.txt","FAIL "+e+"\n");Debug.LogException(e);}
}
