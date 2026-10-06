using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEditor.Localization;
using UnityEngine.Localization.Settings;

public static class EnglishGameSetup
{
    private static Dictionary<string,string> translations;
    private static int changes;
    public static object Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        translations = File.ReadAllLines("Tools/Unity/EnglishGameStrings.tsv").Select(line=>line.Split('\t')).ToDictionary(pair=>pair[0],pair=>pair[1]);
        changes = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ClothingDefinition", new[]{"Assets"}))
            Translate(AssetDatabase.LoadAssetAtPath<ClothingDefinition>(AssetDatabase.GUIDToAssetPath(guid)), "displayName");
        var rating = Resources.Load<OutfitRatingCatalog>("OutfitRating");
        if (rating != null) Translate(rating, "name");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs","Assets/ClothingLibrary/Generated/Pickups"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            GameObject root=PrefabUtility.LoadPrefabContents(path);
            int before=changes;
            TranslateHierarchy(root);
            if (changes!=before) PrefabUtility.SaveAsPrefabAsset(root,path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        var original = EditorSceneManager.GetSceneManagerSetup();
        foreach (string path in new[]{"Assets/Scenes/Lobby.unity","Assets/Scenes/SampleScene.unity","Assets/Scenes/testroom.unity"}.Where(File.Exists))
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened=!scene.IsValid() || !scene.isLoaded;
            if(opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            int before=changes;
            foreach(GameObject root in scene.GetRootGameObjects()) TranslateHierarchy(root);
            foreach(var w in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MannequinWardrobe>(true))) ReplacePortrait(w);
            if(changes!=before) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            if(opened) EditorSceneManager.CloseScene(scene,true);
        }
        if (!AssetDatabase.IsValidFolder("Assets/Localization")) AssetDatabase.CreateFolder("Assets","Localization");
        var settings=LocalizationEditorSettings.ActiveLocalizationSettings;
        if(settings==null)
        {
            settings=ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings,"Assets/Localization/LocalizationSettings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings=settings;
        }
        var en=LocalizationEditorSettings.GetLocales().FirstOrDefault(l=>l.Identifier.Code=="en");
        if(en==null) {en=Locale.CreateLocale("en");AssetDatabase.CreateAsset(en,"Assets/Localization/English.asset");LocalizationEditorSettings.AddLocale(en);}
        settings.SetSelectedLocale(en);
        settings.GetStartupLocaleSelectors().Clear();
        settings.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier("en") });
        var table=LocalizationEditorSettings.GetStringTableCollection("GameEnglish") ?? LocalizationEditorSettings.CreateStringTableCollection("GameEnglish","Assets/Localization");
        var english=table.StringTables.First(t=>t.LocaleIdentifier.Code=="en");
        foreach(var pair in translations) english.AddEntry(pair.Key,pair.Value.Replace("\\n","\n"));
        EditorUtility.SetDirty(settings); EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData); EditorUtility.SetDirty(english);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,table);
        AssetDatabase.SaveAssets();
        return new {changedValues=changes,englishEntries=english.Count};
    }
    public static string BuildEnglishContent()
    {
        UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out UnityEditor.AddressableAssets.Build.AddressablesPlayerBuildResult result);
        if (!string.IsNullOrEmpty(result.Error)) throw new Exception(result.Error);
        return "English localization content built successfully";
    }
    private static void Translate(UnityEngine.Object target, params string[] names)
    {
        var so=new SerializedObject(target); var p=so.GetIterator(); bool changed=false;
        while(p.Next(true))
            if(p.propertyType==SerializedPropertyType.String && names.Contains(p.name) && translations.TryGetValue(p.stringValue,out string english))
            {p.stringValue=english;changes++;changed=true;}
        if(changed) {so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);}
    }
    private static void TranslateHierarchy(GameObject root)
    {
        foreach(var item in root.GetComponentsInChildren<PickupItem>(true)) Translate(item,"itemName");
        foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
            if(translations.TryGetValue(label.text,out string english)) {label.text=english;EditorUtility.SetDirty(label);changes++;}
        foreach(var label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            if(translations.TryGetValue(label.text,out string english)) {label.text=english;EditorUtility.SetDirty(label);changes++;}
    }
    private static void ReplacePortrait(MannequinWardrobe wardrobe)
    {
        var root=(Transform)new SerializedObject(wardrobe).FindProperty("mannequinRoot").objectReferenceValue;
        if(root==null) return;
        if(root.GetComponentInChildren<PigAppearance>(true)==null)
        {
        foreach(Transform child in root.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        foreach(var animator in root.GetComponents<Animator>()) UnityEngine.Object.DestroyImmediate(animator);
        var prefab=Resources.Load<GameObject>("Pigs/PigAvatar");
        var pig=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);
        pig.name="HUD Player Pig"; pig.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);pig.transform.localScale=Vector3.one;
        root.localScale=Vector3.one;
        foreach(Transform part in pig.GetComponentsInChildren<Transform>(true)) part.gameObject.layer=5;
        foreach(var motion in pig.GetComponentsInChildren<PigMotion>(true)) motion.enabled=false;
        }
        var camera=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).FirstOrDefault(c=>c.gameObject.scene==root.gameObject.scene && c.name=="HUD Character Camera");
        if(camera!=null) {camera.transform.position=root.position+new Vector3(.55f,1.05f,4.8f);camera.transform.LookAt(root.position+Vector3.up*.95f);camera.fieldOfView=36;}
        void Light(string name, Vector3 position, float intensity, Color color)
        {
            var child=root.Find(name);
            if(child==null) {var go=new GameObject(name);go.transform.SetParent(root,false);child=go.transform;}
            child.localPosition=position;child.LookAt(root.position+Vector3.up);
            var light=child.GetComponent<UnityEngine.Light>(); if(light==null) light=child.gameObject.AddComponent<UnityEngine.Light>();
            light.type=LightType.Spot;light.spotAngle=95;light.range=8;light.intensity=intensity;light.color=color;light.cullingMask=1<<5;
        }
        Light("HUD Pig Key",new Vector3(-2,3,3),7,new Color(1,.90f,.85f));
        Light("HUD Pig Fill",new Vector3(2,2,2),3,new Color(.75f,.85f,1));
        changes++;
    }
}
