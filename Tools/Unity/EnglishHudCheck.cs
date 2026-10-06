using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class EnglishHudCheck
{
    static void Check(bool value,string message) {if(!value) throw new Exception(message);}
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true)
        {
            bool running;object current;
            try {running=routine.MoveNext();current=running?routine.Current:null;}
            catch(Exception e) {InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());SessionState.SetString("Svinki.EnglishHud.Check","FAILED: "+e);Debug.LogException(e);yield break;}
            if(!running)yield break;yield return current;
        }
    }
    public static string Run()
    {
        Check(EditorApplication.isPlaying && !NetworkLobby.Instance.InSession && !NetworkLobby.Instance.Offline,"Requires isolated menu");
        SessionState.SetString("Svinki.EnglishHud.Check","running"); NetworkLobby.Instance.StartCoroutine(Guard(RunCheck())); return "Checking English UI and live pig portrait";
    }
    static void CheckText()
    {
        var russian=UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).Where(t=>Regex.IsMatch(t.text??"","[А-Яа-яЁё]")).Select(t=>t.name+": "+t.text).ToArray();
        Check(russian.Length==0,"Russian UI: "+string.Join("; ",russian));
    }
    static void CheckPose(PigAppearance source,PigAppearance preview)
    {
        foreach(string name in new[]{"Hips","Spine","Head","UpperArm_L","Forearm_L","Hand_L","UpperArm_R","Thigh_L","Shin_L","Foot_L","Thigh_R","Shin_R","Foot_R"})
        {
            var a=source.ModelRoot.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
            var b=preview.ModelRoot.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
            Check(Vector3.Distance(a.localPosition,b.localPosition)<.00001f && Quaternion.Angle(a.localRotation,b.localRotation)<.05f,name+" pose mismatch");
        }
        Check(Vector3.Distance(source.ModelRoot.localScale,preview.ModelRoot.localScale)<.00001f,"Squash mismatch");
        Check(source.FaceCode==preview.FaceCode,"Appearance mismatch");
    }
    static IEnumerator RunCheck()
    {
        Directory.CreateDirectory("ArtSource/EnglishHud");
        var lobby=NetworkLobby.Instance;CheckText();lobby.StartOffline();
        float deadline=Time.realtimeSinceStartup+30;
        while((lobby.Busy || !PlayerRegistry.Players.Any(p=>p.IsLocal))&&Time.realtimeSinceStartup<deadline)yield return null;
        var player=PlayerRegistry.Players.Single(p=>p.IsLocal);
        foreach(var brain in UnityEngine.Object.FindObjectsByType<ThiefBrain>())brain.enabled=false;
        var source=player.GetComponentInChildren<PigAppearance>(true);
        var wardrobe=UnityEngine.Object.FindAnyObjectByType<MannequinWardrobe>();
        var preview=wardrobe.GetComponentInChildren<PigAppearance>(true);
        Check(preview!=null && !preview.GetComponent<PigMotion>().enabled,"Preview pig missing or independently animated");
        var items=Resources.LoadAll<ClothingDefinition>("");
        if(items.Length==0) items=AssetDatabase.FindAssets("t:ClothingDefinition",new[]{"Assets/ClothingLibrary/Generated/Definitions"}).Select(g=>AssetDatabase.LoadAssetAtPath<ClothingDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        var outfit=items.Where(c=>c.PigRigged).GroupBy(c=>c.Slot).Select(g=>g.First()).ToArray();
        Check(outfit.Length==4,"Missing four clothing slots");
        player.Outfit.SetItems(outfit);source.Apply(PigFace.Preset(2).Encode());
        yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
        CheckPose(source,preview);
        foreach(var skin in preview.GetComponentsInChildren<SkinnedMeshRenderer>(true))Check(skin.gameObject.layer==5,"Preview layer leakage");
        Check(preview.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(s=>s.name.EndsWith("_worn"))==4,"Equipped garments missing");
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W,Key.LeftCtrl));
        for(int i=0;i<20;i++) {yield return new WaitForEndOfFrame();CheckPose(source,preview);}
        Check(player.CrouchAmount>.2f,"Crouch input was not applied");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/EnglishHud/crouch.png"));
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return new WaitForSecondsRealtime(.5f);
        CheckPose(source,preview);CheckText();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/EnglishHud/full-outfit.png"));
        yield return new WaitForEndOfFrame();
        var camera=UnityEngine.Object.FindObjectsByType<Camera>().First(c=>c.name=="HUD Character Camera");camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=camera.targetTexture;
        var image=new Texture2D(camera.targetTexture.width,camera.targetTexture.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();
        File.WriteAllBytes("ArtSource/EnglishHud/portrait.png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
        player.Outfit.Remove(ClothingSlot.Legs,null,out var removed);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
        Check(preview.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(s=>s.name.EndsWith("_worn"))==3,"Removed garment still present");
        player.Outfit.Add(removed);yield return new WaitForEndOfFrame();
        lobby.InventoryOpen=true;yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();CheckText();ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/EnglishHud/inventory.png"));
        lobby.InventoryOpen=false;
        var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=RoundFinishStation.ForScene(player.gameObject.scene).Center;cc.enabled=true;
        lobby.EndRound();yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();CheckText();ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/EnglishHud/runway.png"));
        yield return new WaitForSecondsRealtime(8.2f);CheckText();ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/EnglishHud/results.png"));
        lobby.Leave();while(lobby.Offline||lobby.Busy)yield return null;
        CheckText();
        var table=UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection("GameEnglish").StringTables.Single(t=>t.LocaleIdentifier.Code=="en");
        Check(table.Count==282 && table.Values.All(e=>!string.IsNullOrWhiteSpace(e.Value)&&!Regex.IsMatch(e.Value,"[А-Яа-яЁё]")),"English table gaps");
        SessionState.SetString("Svinki.EnglishHud.Check","PASS: 282 English entries; menu, inventory, runway and results; all four garments, removal, live appearance and walking/crouch pose");
        File.WriteAllText("ArtSource/EnglishHud/validation.txt",SessionState.GetString("Svinki.EnglishHud.Check",""));
    }
}
