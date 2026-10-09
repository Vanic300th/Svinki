using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RunwayGhostCheck
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static string Offline()
    {
        SessionState.SetString("Svinki.Ghost.Offline", "RUNNING");
        PigFace.SaveSelection(new PigFace { eyes=1, brows=2, glassesStyle=3, beard=true, mustache=1,
            mohawk=true, nosePiercing=true, earPiercing=true, skinColor=6, hairColor=4, pattern=2, tattoo=1 }.Encode());
        NetworkLobby.Instance.StartCoroutine(OfflineChecks());
        return "Checking living and eliminated runway models, actual death and next round";
    }
    private static IEnumerator OfflineChecks()
    {
        var lobby = NetworkLobby.Instance;
        lobby.StartOffline();
        float deadline = Time.realtimeSinceStartup + 20;
        while ((lobby.Busy || PlayerRegistry.Players.Count == 0) && Time.realtimeSinceStartup < deadline) yield return null;
        yield return new WaitForSecondsRealtime(.3f);
        PlayerAvatar player = null; FinalRunwayView runway = null; int initial = 0;
        try
        {
            foreach (var brain in UnityEngine.Object.FindObjectsByType<MannequinBrain>()) brain.enabled=false;
            foreach (var brain in UnityEngine.Object.FindObjectsByType<ThiefBrain>()) brain.enabled=false;
            player = PlayerRegistry.Players.First(p => p.IsLocal);
            initial = player.GetComponentInChildren<PigAppearance>(true).FaceCode;
            // Exercise the real living results path before checking death.
            typeof(NetworkLobby).GetMethod("PresentOfflineOutfit", Private).Invoke(lobby, null);
        } catch (Exception e) { Fail(e); yield break; }
        yield return null;
        try
        {
            runway = UnityEngine.Object.FindAnyObjectByType<FinalRunwayView>();
            var pig = (GameObject)typeof(FinalRunwayView).GetField("pig", Private).GetValue(runway);
            Check(!lobby.Results.entries[0].eliminated && pig.GetComponent<RunwayGhost>()==null, "Living pig became a ghost");
            Check(pig.GetComponent<PigAppearance>().FaceCode==initial, "Living customization changed");
            lobby.ContinueAfterResults();
            Check(lobby.Results==null && player.IsAlive, "Living Continue changed restart behavior");
            Check(player.GetComponent<PlayerKnockdown>().TryMannequinHit(Vector3.forward), "First hit rejected");
        } catch (Exception e) { Fail(e); yield break; }
        yield return new WaitForSecondsRealtime(3.3f);
        try { Check(player.GetComponent<PlayerKnockdown>().TryMannequinHit(Vector3.forward), "Second hit rejected"); }
        catch (Exception e) { Fail(e); yield break; }
        yield return new WaitForSecondsRealtime(.35f);
        try
        {
            var entry = lobby.Results.entries.Single();
            Check(entry.eliminated && entry.appearance==initial && entry.clothing.Length==0, "Dead result lost original appearance or clothing status");
            var wire = JsonUtility.FromJson<OutfitRoundResults>(JsonUtility.ToJson(lobby.Results));
            Check(wire.entries[0].eliminated && wire.entries[0].appearance==initial, "Result JSON lost ghost state");
            var pig = (GameObject)typeof(FinalRunwayView).GetField("pig", Private).GetValue(runway);
            Check(runway.Showing && pig.GetComponent<RunwayGhost>()!=null && pig.GetComponent<PigAppearance>().FaceCode==initial, "Runway ghost absent or wrong face");
            Check(pig.transform.localPosition.y>.10f, "Ghost does not float");
            Check(pig.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m==null||m.shader.name=="Svinki/Runway Ghost")), "Ghost has opaque accessory materials");
            var body = pig.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Body");
            var colors = new MaterialPropertyBlock(); body.GetPropertyBlock(colors,0);
            Check(colors.GetFloat("_Pattern")==2 && colors.GetFloat("_Tattoo")==1, "Ghost lost initial markings");
            Check(Resources.Load<GameObject>("Pigs/PigAvatar").GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m==null||m.shader.name!="Svinki/Runway Ghost")), "Shared pig materials mutated");
            typeof(FinalRunwayView).GetField("start",Private).SetValue(runway,Time.unscaledTime-3.1f);
        } catch (Exception e) { Fail(e); yield break; }
        yield return new WaitForSecondsRealtime(.2f);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/RunwayGhost/offline-runway.png"));
        yield return new WaitForSecondsRealtime(.25f);
        try
        {
            runway.Skip();
            var view = UnityEngine.Object.FindAnyObjectByType<OutfitResultsView>();
            typeof(OutfitResultsView).GetMethod("Select",Private).Invoke(view,new object[]{0});
            var model=(GameObject)typeof(OutfitResultsView).GetField("model",Private).GetValue(view);
            Check(model.GetComponent<RunwayGhost>()!=null && model.GetComponent<PigAppearance>().FaceCode==initial, "Score portrait is not the matching ghost");
            lobby.ContinueAfterResults();
        } catch (Exception e) { Fail(e); yield break; }
        yield return new WaitForSecondsRealtime(2.5f);
        try
        {
            Check(lobby.Results==null && PlayerRegistry.Players.Any(p=>p.IsLocal&&p.IsAlive), "Dead Continue failed to start a fresh round");
            string result="PASS: living pig unaffected; actual two-hit death automatically presents matching ghost; customization/patterns/tattoo preserved; shared materials untouched; JSON ghost flag retained; scores use same ghost; Next Round resets death";
            File.WriteAllText("ArtSource/RunwayGhost/offline-validation.txt",result); SessionState.SetString("Svinki.Ghost.Offline",result);
        } catch (Exception e) { Fail(e); }
    }
    private static void Fail(Exception e)
    { SessionState.SetString("Svinki.Ghost.Offline","FAILED: "+e.Message); Debug.LogException(e); }
}
