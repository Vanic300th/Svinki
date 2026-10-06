using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OutfitRatingSetup
{
    const string Path = "Assets/Resources/OutfitRating.asset";
    [MenuItem("Svinki/Одежда/Создать правила оценки по шести образам")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode to save rating rules.");
        var definitions = AssetDatabase.FindAssets("", new[] { "Assets/ClothingLibrary/Generated/Definitions" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ClothingDefinition>)
            .Where(c => c != null && c.PigRigged).ToArray();
        ClothingDefinition Item(string key) => definitions.Single(c => c.Model.name == key + "_worn");
        ClothingRatingProfile Profile(string key, string hex, ClothingPattern pattern, float dressiness, float warmth)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return new ClothingRatingProfile { key = key, clothing = Item(key), color = color, pattern = pattern, dressiness = dressiness, warmth = warmth };
        }
        ReferenceOutfit Set(string name, params string[] keys) => new ReferenceOutfit { name = name, items = keys.Select(Item).ToArray() };
        var asset = AssetDatabase.LoadAssetAtPath<OutfitRatingCatalog>(Path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<OutfitRatingCatalog>(); AssetDatabase.CreateAsset(asset, Path); }
        asset.items = new[] {
            Profile("cap1", "#666447", ClothingPattern.Camouflage, .10f, .35f),
            Profile("hat1", "#47494A", ClothingPattern.Woven, .20f, .80f),
            Profile("hat2", "#3B6179", ClothingPattern.Woven, .55f, .80f),
            Profile("shirt1", "#292626", ClothingPattern.Plain, .85f, .20f),
            Profile("shirt2", "#646A6C", ClothingPattern.Woven, .15f, .20f),
            Profile("shirt3", "#AAA280", ClothingPattern.Camouflage, .10f, .20f),
            Profile("hoodie1", "#65676A", ClothingPattern.Plain, .15f, .85f),
            Profile("sleeve1", "#515F3C", ClothingPattern.Camouflage, .10f, .65f),
            Profile("sweater1", "#959595", ClothingPattern.Stripes, .55f, .85f),
            Profile("pants1", "#3D302E", ClothingPattern.Plain, .85f, .55f),
            Profile("pants2", "#637382", ClothingPattern.Denim, .35f, .60f),
            Profile("pants3", "#ADA783", ClothingPattern.Camouflage, .10f, .55f),
            Profile("loafers1", "#AC8960", ClothingPattern.Plain, .95f, .30f),
            Profile("sneakers2", "#736A7B", ClothingPattern.Plain, .15f, .45f),
            Profile("sneakers3", "#929390", ClothingPattern.Plain, .10f, .45f)
        };
        asset.references = new[] {
            Set("Military", "shirt3", "pants3", "cap1", "sneakers3"),
            Set("Streetwear", "hoodie1", "pants2", "hat1", "sneakers2"),
            Set("Classic", "shirt1", "pants1", "hat2", "loafers1"),
            Set("Camouflage casual", "sleeve1", "pants1", "cap1", "sneakers3"),
            Set("Cozy", "sweater1", "pants2", "hat2", "loafers1"),
            Set("Sport", "shirt2", "pants3", "hat1", "sneakers2")
        };
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
        Debug.Log("Rating rules saved: 15 items and 6 reference outfits.");
    }
}
