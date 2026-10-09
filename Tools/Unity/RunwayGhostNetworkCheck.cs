using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RunwayGhostNetworkCheck
{
    public static string KillHost()
    {
        foreach(var b in UnityEngine.Object.FindObjectsByType<MannequinBrain>())b.enabled=false;
        foreach(var b in UnityEngine.Object.FindObjectsByType<ThiefBrain>())b.enabled=false;
        NetworkLobby.Instance.StartCoroutine(Hits()); return "Two-hit host elimination check started";
    }
    private static IEnumerator Hits()
    {
        var host=PlayerRegistry.Players.First(p=>p.IsLocal);
        var life=host.GetComponent<PlayerKnockdown>();
        SessionState.SetInt("Svinki.Ghost.HostFace",host.GetComponentInChildren<PigAppearance>(true).FaceCode);
        if(!life.TryMannequinHit(Vector3.forward))throw new Exception("Host first hit failed");
        yield return new WaitForSecondsRealtime(3.3f);
        if(!life.TryMannequinHit(Vector3.forward))throw new Exception("Host second hit failed");
        yield return new WaitForSecondsRealtime(.3f);
        if(NetworkLobby.Instance.Results!=null||NetworkLobby.Instance.Snapshot.phase!=SessionPhase.Round||!life.IsDead)
            throw new Exception("Dead host must spectate until living guest finishes");
        SessionState.SetString("Svinki.Ghost.NetworkDeath","PASS");
    }
    public static string Inspect()
    {
        var lobby=NetworkLobby.Instance;
        var dead=lobby.Results.entries.Single(e=>e.nickname=="GhostHost");
        var living=lobby.Results.entries.Single(e=>e.nickname=="LiveGuest");
        if(!dead.eliminated||dead.appearance!=SessionState.GetInt("Svinki.Ghost.HostFace",-1)||dead.clothing.Length!=0||living.eliminated)
            throw new Exception("Incorrect shared living/dead presentation entries");
        string result="PASS: eliminated host spectates until living guest finishes; final results retain both players; host ghost keeps original face, living guest stays alive";
        File.WriteAllText("ArtSource/RunwayGhost/network-validation.txt",result);return result;
    }
}
