using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Local menu hover face; the shared game typography remains unchanged.</summary>
public static class ProjectSvinMenuHoverSetup
{
    private const string Folder = "Assets/_Project/Content/UI/ProjectSvin/Fonts/";
    [MenuItem("Svinki/UI/Apply soft ears and O nostrils")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Edit mode required");
        var root = PrefabUtility.LoadPrefabContents(ProjectSvinUiSetup.MenuPath);
        try { Configure(root); PrefabUtility.SaveAsPrefabAsset(root, ProjectSvinUiSetup.MenuPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void Configure(GameObject root)
    {
        const string path = Folder + "SvinMenuSnout SDF.asset";
        var face = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (face == null)
        {
            var original = ProjectSvinTheme.Current.headingFont;
            var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + "SvinMenuSnout-Regular.ttf");
            face = TMP_FontAsset.CreateFontAsset(source, (int)original.faceInfo.pointSize, original.atlasPadding,
                original.atlasRenderMode, original.atlasWidth, original.atlasHeight, AtlasPopulationMode.Dynamic, false);
            face.name = "SvinMenuSnout SDF";
            face.TryAddCharacters(original.characterTable.Select(c => c.unicode).ToArray(), out uint[] missing);
            if (missing != null && missing.Length > 0) throw new System.InvalidOperationException("Missing hover font characters: " + string.Join(",", missing));
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original.fontFeatureTable), face.fontFeatureTable);
            face.fallbackFontAssetTable = original.fallbackFontAssetTable.ToList();
            face.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(face, path);
            face.material.name = face.name + " Material"; AssetDatabase.AddObjectToAsset(face.material, face);
            foreach (var atlas in face.atlasTextures) { atlas.name = face.name + " Atlas"; AssetDatabase.AddObjectToAsset(atlas, face); }
            EditorUtility.SetDirty(face); AssetDatabase.SaveAssetIfDirty(face);
        }
        foreach (var hover in root.GetComponentsInChildren<ProjectSvinMenuHover>(true))
        {
            hover.snoutFont = face; hover.leftOverlap = hover.rightOverlap = 7;
            hover.RefreshEars(); EditorUtility.SetDirty(hover);
        }
    }
}
