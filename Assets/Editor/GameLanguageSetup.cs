using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>Rebuild translation tables from the reviewed English/Russian catalogue.</summary>
public static class GameLanguageSetup
{
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit mode required");
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null) throw new Exception("Existing LocalizationSettings were not found");
        foreach (string code in new[] { "en", "ru" })
            if (!LocalizationEditorSettings.GetLocales().Any(l => l.Identifier.Code == code))
            {
                var locale = Locale.CreateLocale(code);
                AssetDatabase.CreateAsset(locale, "Assets/Localization/" + (code == "ru" ? "Russian" : "English") + ".asset");
                LocalizationEditorSettings.AddLocale(locale);
            }
        settings.GetStartupLocaleSelectors().RemoveAll(s => s is SavedGameLocaleSelector);
        settings.GetStartupLocaleSelectors().Insert(0,new SavedGameLocaleSelector());
        var data = JsonUtility.FromJson<GameLanguage.Catalog>(File.ReadAllText("Tools/Localization/en-ru.json"));
        var duplicate = data.entries.GroupBy(e => e.source).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null || data.entries.Any(e => string.IsNullOrWhiteSpace(e.translation))) throw new Exception("Invalid translation catalogue");
        var collection = LocalizationEditorSettings.GetStringTableCollection(GameLanguage.Table) ??
            LocalizationEditorSettings.CreateStringTableCollection(GameLanguage.Table,"Assets/Localization");
        foreach (string code in new[] { "en", "ru" })
        {
            var table = collection.StringTables.FirstOrDefault(t => t.LocaleIdentifier.Code == code) ??
                (StringTable)collection.AddNewTable(new LocaleIdentifier(code));
            foreach (var entry in data.entries) table.AddEntry(GameLanguage.Key(entry.source),code == "en" ? entry.source : entry.translation);
            table.AddEntry("passthrough","{0}");
            EditorUtility.SetDirty(table); LocalizationEditorSettings.SetPreloadTableFlag(table,true);
        }
        EditorUtility.SetDirty(collection); EditorUtility.SetDirty(collection.SharedData);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);
        // Older authored labels keep their original table and entry IDs.
        var old = LocalizationEditorSettings.GetStringTableCollection("GameEnglish");
        if (old != null)
        {
            var en = old.StringTables.Single(t => t.LocaleIdentifier.Code == "en");
            var ru = old.StringTables.FirstOrDefault(t => t.LocaleIdentifier.Code == "ru") ?? (StringTable)old.AddNewTable(new LocaleIdentifier("ru"));
            var translations = data.entries.ToDictionary(e => e.source,e => e.translation);
            foreach (var entry in en.Values)
            {
                if (!translations.TryGetValue(entry.Value,out string value)) throw new Exception("Untranslated legacy entry: " + entry.Value);
                ru.AddEntry(entry.KeyId,value);
            }
            EditorUtility.SetDirty(ru); EditorUtility.SetDirty(old); EditorUtility.SetDirty(old.SharedData);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,old);
        }
        var assets = LocalizationEditorSettings.GetAssetTableCollection("GameAssets") ??
            LocalizationEditorSettings.CreateAssetTableCollection("GameAssets","Assets/Localization");
        const string fontPath="Assets/Localization/Fonts/SvinkiBilingual.asset";
        var font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fontPath);
        if(font==null)
        {
            Directory.CreateDirectory("Assets/Localization/Fonts");AssetDatabase.Refresh();
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            font=TMPro.TMP_FontAsset.CreateFontAsset(source);
            font.name="Svinki Bilingual";font.atlasPopulationMode=TMPro.AtlasPopulationMode.Dynamic; font.isMultiAtlasTexturesEnabled=true;
            AssetDatabase.CreateAsset(font,fontPath);
            foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
            AssetDatabase.AddObjectToAsset(font.material,font);font.material.mainTexture=font.atlasTexture;
        }
        const string russian="АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        string required = new string(russian.Where(c => !font.HasCharacter(c)).ToArray());
        if (required.Length > 0) font.TryAddCharacters(required);
        string missing = new string(russian.Where(c => !font.HasCharacter(c)).ToArray());
        if (missing.Length > 0) throw new Exception("Missing Cyrillic glyphs: " + missing);
        string additional = new string(string.Join("", data.entries.Select(e => e.source + e.translation))
            .Distinct().Where(c => !font.HasCharacter(c)).ToArray());
        if (additional.Length > 0) font.TryAddCharacters(additional);
        EditorUtility.SetDirty(font);EditorUtility.SetDirty(font.material);foreach(var atlas in font.atlasTextures)EditorUtility.SetDirty(atlas);
        foreach(string code in new[]{"en","ru"})
        {
            var table=assets.AssetTables.FirstOrDefault(t=>t.LocaleIdentifier.Code==code)??(AssetTable)assets.AddNewTable(new LocaleIdentifier(code));
            assets.AddAssetToTable(table,"ui.font",font);EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(assets);EditorUtility.SetDirty(assets.SharedData);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,assets);
        var addressables = AddressableAssetSettingsDefaultObject.Settings;
        addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
        EditorUtility.SetDirty(addressables); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        string validation = Validate();
        Directory.CreateDirectory("ArtSource/Localization");File.WriteAllText("ArtSource/Localization/table-validation.txt",validation);
        return validation;
    }
    public static string Validate()
    {
        int count=0;
        foreach (var collection in LocalizationEditorSettings.GetStringTableCollections())
        foreach (var key in collection.SharedData.Entries)
        foreach (var table in collection.StringTables)
        {
            if (string.IsNullOrWhiteSpace(table.GetEntry(key.Id)?.Value)) throw new Exception("Missing translation: " + collection.TableCollectionName + "/" + table.LocaleIdentifier.Code + "/" + key.Key);
            count++;
        }
        if (count == 0) throw new Exception("No table entries checked");
        return "PASS: " + count + " nonempty translation entries; English and Russian locales; saved-locale startup selector; Addressables build with player.\n";
    }
    public static void BuildContent()
    {
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if (!string.IsNullOrEmpty(result.Error))
        {
            File.WriteAllText("ArtSource/Localization/addressables-validation.txt", "FAIL: " + result.Error + "\n");
            throw new Exception(result.Error);
        }
        File.WriteAllText("ArtSource/Localization/addressables-validation.txt","PASS: Addressables content built for " + EditorUserBuildSettings.activeBuildTarget + ".\n");
    }
}
