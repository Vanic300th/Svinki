using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class ProjectSvinUiClaritySetup
{
    [MenuItem("Svinki/UI/Smooth UI and remove exit hoof")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Edit mode required");
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType().Name == "GameView"))
        {
            var property = window.GetType().GetProperty("lowResolutionForAspectRatios", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property?.CanWrite == true) property.SetValue(window, false);
            EditorUtility.SetDirty(window); window.Repaint();
        }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SvinkiAssetPaths.Settings + "/PC_RPAsset.asset");
        pipeline.msaaSampleCount = 4;
        EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssetIfDirty(pipeline);
        var theme = ProjectSvinTheme.Current;
        var fonts = new[] { theme.headingFont, theme.bodyFont, theme.inputFont, theme.logoFont, theme.brandFont,
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Content/UI/ProjectSvin/Fonts/SvinMenuSnout SDF.asset") };
        foreach (var font in fonts.Where(f => f != null).Distinct())
        {
            foreach (var atlas in font.atlasTextures)
            { atlas.filterMode = FilterMode.Bilinear; EditorUtility.SetDirty(atlas); }
            if (font.material.HasProperty("_Sharpness")) font.material.SetFloat("_Sharpness", -.15f);
            EditorUtility.SetDirty(font.material); AssetDatabase.SaveAssetIfDirty(font);
        }
        var root = PrefabUtility.LoadPrefabContents(ProjectSvinUiSetup.MenuPath);
        try
        {
            var layout = root.GetComponent<ProjectSvinMenuLayout>(); var exit = layout.mainButtons[5];
            var hover = exit.GetComponent<ProjectSvinMenuHover>();
            if (hover.exitHoof != null) hover.exitHoof.gameObject.SetActive(false);
            var label = exit.GetComponentInChildren<TMP_Text>(true); var rect = label.rectTransform;
            rect.anchoredPosition = new Vector2(0, rect.anchoredPosition.y); rect.sizeDelta = new Vector2(620, rect.sizeDelta.y);
            hover.RefreshEars(); PrefabUtility.SaveAsPrefabAsset(root, ProjectSvinUiSetup.MenuPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
