using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Localization;
using UnityEngine;

/// <summary>Run in an isolated verification checkout, never while the project is open elsewhere.</summary>
public static class MechanicsBuild
{
    public static void Mac() => Build(BuildTarget.StandaloneOSX, "SVINKI_MECHANICS_MAC_OUTPUT");
    public static void Windows() => Build(BuildTarget.StandaloneWindows64, "SVINKI_MECHANICS_WINDOWS_OUTPUT");
    public static void Translate()
    {
        var data = JsonUtility.FromJson<GameLanguage.Catalog>(File.ReadAllText("Tools/Localization/en-ru.json"));
        var collection = LocalizationEditorSettings.GetStringTableCollection(GameLanguage.Table);
        if (collection == null) throw new Exception("GameUI tables missing");
        foreach (var table in collection.StringTables)
        {
            foreach (var entry in data.entries) table.AddEntry(GameLanguage.Key(entry.source), table.LocaleIdentifier.Code == "ru" ? entry.translation : entry.source);
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(collection); EditorUtility.SetDirty(collection.SharedData);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        File.WriteAllText("Assets/Resources/GameLanguageIndex.json", JsonUtility.ToJson(data, true));
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    }
    private static void Build(BuildTarget target, string outputKey)
    {
        Translate();
        UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(null);
        EditorUserBuildSettings.selectedStandaloneTarget = target;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        var output = Environment.GetEnvironmentVariable(outputKey);
        if (string.IsNullOrEmpty(output)) throw new Exception("Build output missing");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { SvinkiAssetPaths.BootstrapScene, SvinkiAssetPaths.SocialLobbyScene, SvinkiAssetPaths.GameplayScene },
            locationPathName = output, target = target, targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.Development | BuildOptions.CompressWithLz4
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Mechanics build failed: " + report.summary.result);
        Debug.Log("MECHANICS BUILD PASSED: " + target + "; " + report.summary.totalErrors + " errors");
    }
}
