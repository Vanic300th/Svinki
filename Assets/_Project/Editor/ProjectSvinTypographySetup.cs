using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Bakes the supplied display fonts and updates typography without recreating the menu.</summary>
public static class ProjectSvinTypographySetup
{
    private const string Folder = "Assets/_Project/Content/UI/ProjectSvin/Fonts/";
    [Serializable] private sealed class Coverage { public FontInfo[] fonts; }
    [Serializable] private sealed class FontInfo { public string file, role; public string[] additionalRoles; public int[] codepoints, uiCodepoints; public int atlasSize; }

    [MenuItem("Svinki/UI/Apply Project S.V.I.N. typography")]
    public static void Apply()
    {
        var theme = AssetDatabase.LoadAssetAtPath<ProjectSvinTheme>(ProjectSvinUiSetup.ThemePath);
        if (theme == null) throw new InvalidOperationException("Create the editable UI theme first.");
        var coverage = JsonUtility.FromJson<Coverage>("{\"fonts\":" + File.ReadAllText("ArtSource/UI/ProjectSvin/Fonts/font-coverage.json") + "}");
        var fallback = theme.font;
        foreach (var info in coverage.fonts)
        {
            string path = Folder + Path.GetFileNameWithoutExtension(info.file) + " SDF.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + info.file);
                if (source == null) throw new InvalidOperationException("Font not imported: " + info.file);
                int atlasSize = info.atlasSize > 0 ? info.atlasSize : 1024;
                asset = TMP_FontAsset.CreateFontAsset(source, 80, 8, GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, false);
                asset.name = Path.GetFileNameWithoutExtension(info.file) + " SDF";
                // School Essay's personal-use file maps every digit to the same decorative logo.
                // Leave those code points to the readable fallback; the original font is unchanged.
                asset.TryAddCharacters((info.uiCodepoints ?? info.codepoints).Where(c => c >= 32 && !(info.file == "School Essay.ttf" && c >= '0' && c <= '9')).Select(c => (uint)c).ToArray(), out uint[] missing);
                if (missing != null && missing.Length > 0) throw new InvalidOperationException(info.file + " could not bake glyphs: " + string.Join(",", missing));
                asset.atlasPopulationMode = AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(asset, path);
                asset.material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                foreach (var texture in asset.atlasTextures)
                {
                    texture.name = asset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(texture, asset);
                }
            }
            asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            if (info.file == "School Essay.ttf")
            {
                asset.characterTable.RemoveAll(c => c.unicode >= '0' && c.unicode <= '9');
                asset.ReadFontAssetDefinition();
            }
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
            if (info.additionalRoles != null && info.additionalRoles.Contains("body")) theme.bodyFont = asset;
            if (info.role == "heading")
            {
                theme.headingFont = asset;
                theme.worldHeadingFont = AssetDatabase.LoadAssetAtPath<Font>(Folder + info.file);
            }
            else if (info.file == "School Essay.ttf") theme.bodyFont = asset;
            else if (info.role == "logo") theme.logoFont = asset;
            else if (info.role == "input") theme.inputFont = asset;
            else if (info.role == "brand") theme.brandFont = asset;
        }
        theme.revision++;
        EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme);
        var root = PrefabUtility.LoadPrefabContents(ProjectSvinUiSetup.MenuPath);
        try
        {
            var layout = root.GetComponent<ProjectSvinMenuLayout>();
            layout.gameTitle.GetComponent<ProjectSvinElement>().fontRole = ProjectSvinFontRole.Logo;
            layout.projectTitle.GetComponent<ProjectSvinElement>().fontRole = ProjectSvinFontRole.Brand;
            foreach (var element in root.GetComponentsInChildren<ProjectSvinElement>(true))
            {
                if (element.useThemeFont && element.TryGetComponent<TMP_Text>(out var label)) label.fontStyle = FontStyles.Normal;
                element.Apply();
            }
            PrefabUtility.SaveAsPrefabAsset(root, ProjectSvinUiSetup.MenuPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("Project S.V.I.N. typography applied from font-coverage.json; brand and input fonts are independent.");
    }
}
