using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OutfitRatingSetup
{
    const string Path = "Assets/Resources/OutfitRating.asset";
    [MenuItem("Svinki/Clothing/Combine original and expanded rating examples")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode to save rating rules.");
        if (ClothingLibraryImporter.LoadPolicy() == null) ClothingLibraryImporter.CreatePolicyFromRating();
        ClothingLibraryImporter.Rebuild();
        var definitions = ClothingLibraryImporter.ApprovedDefinitions().Where(c => c.PigRigged).ToArray();
        ClothingDefinition Item(string key) => definitions.Single(c => c.Model.name == key + "_worn");
        ClothingRatingProfile Profile(string key, string hex, ClothingPattern pattern, float dressiness, float warmth)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return new ClothingRatingProfile { key = key, clothing = Item(key), color = color, pattern = pattern, dressiness = dressiness, warmth = warmth };
        }
        ReferenceOutfit Set(string name, params string[] keys) => new ReferenceOutfit { name = name, items = keys.Select(Item).ToArray() };
        var asset = AssetDatabase.LoadAssetAtPath<OutfitRatingCatalog>(Path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<OutfitRatingCatalog>(); AssetDatabase.CreateAsset(asset, Path); }
        var defaults = new[] {
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
            Profile("sneakers3", "#929390", ClothingPattern.Plain, .10f, .45f),
            Profile("cap2", "#988571", ClothingPattern.Woven, .25f, .35f),
            Profile("hat3", "#E4D8AD", ClothingPattern.Speckled, .45f, .60f),
            Profile("shirt4", "#E8DEC5", ClothingPattern.Floral, .25f, .20f),
            Profile("shirt5", "#737A77", ClothingPattern.Stripes, .15f, .20f),
            Profile("shirt6", "#E9C52B", ClothingPattern.Floral, .10f, .20f),
            Profile("shirt7", "#E3B4C1", ClothingPattern.Floral, .20f, .20f),
            Profile("hoodie4", "#91988B", ClothingPattern.Camouflage, .10f, .85f),
            Profile("hoodie5", "#743D32", ClothingPattern.Woven, .35f, .85f),
            Profile("sweater2", "#76B2D8", ClothingPattern.Speckled, .30f, .85f),
            Profile("sweater4", "#A1593B", ClothingPattern.Woven, .45f, .85f),
            Profile("sweater5", "#909090", ClothingPattern.Checks, .45f, .85f),
            Profile("sweater6", "#AAB6AE", ClothingPattern.Floral, .25f, .85f),
            Profile("pants6", "#B89E61", ClothingPattern.Woven, .25f, .55f),
            Profile("pants7", "#737C76", ClothingPattern.Camouflage, .10f, .55f),
            Profile("pants8", "#8B532F", ClothingPattern.Camouflage, .20f, .55f),
            Profile("pants9", "#E6BAC0", ClothingPattern.Speckled, .15f, .55f),
            Profile("loafers2", "#B57C4E", ClothingPattern.Woven, .70f, .30f),
            Profile("sneakers4", "#D9DED2", ClothingPattern.Speckled, .10f, .45f)
        };
        var examples = new[] {
            Set("Military", "shirt3", "pants3", "cap1", "sneakers3"),
            Set("Streetwear", "hoodie1", "pants2", "hat1", "sneakers2"),
            Set("Classic", "shirt1", "pants1", "hat2", "loafers1"),
            Set("Camouflage casual", "sleeve1", "pants1", "cap1", "sneakers3"),
            Set("Cozy", "sweater1", "pants2", "hat2", "loafers1"),
            Set("Sport", "shirt2", "pants3", "hat1", "sneakers2"),
            Set("Street", "hoodie1", "pants2", "hat1", "sneakers2"),
            Set("Desert", "shirt3", "pants3", "cap1", "sneakers3"),
            Set("Commando", "sleeve1", "pants7", "cap1", "sneakers3"),
            Set("Classic", "shirt1", "pants1", "hat2", "loafers1"),
            Set("Sportswear", "shirt2", "pants6", "cap2", "sneakers4"),
            Set("French", "sweater1", "pants8", "hat2", "loafers1"),
            Set("Army", "hoodie4", "pants7", "cap2", "sneakers4"),
            Set("Baron", "hoodie5", "pants8", "hat3", "loafers2"),
            Set("Gardener", "shirt6", "pants9", "hat3", "loafers2"),
            Set("Forester", "shirt4", "pants6", "hat1", "loafers1"),
            Set("Tiger", "shirt5", "pants1", "cap1", "sneakers3"),
            Set("Sakura", "shirt7", "pants9", "hat2", "sneakers2"),
            Set("Sky", "sweater2", "pants2", "hat2", "sneakers4"),
            Set("Autumn", "sweater4", "pants8", "cap2", "loafers1"),
            Set("Chess", "sweater5", "pants1", "hat1", "sneakers3"),
            Set("Grandma's Garden", "sweater6", "pants9", "hat3", "loafers2")
        };
        // Keep artist-tuned profiles and previous examples; add the expanded collection once.
        asset.items = (asset.items ?? Array.Empty<ClothingRatingProfile>()).Concat(defaults)
            .Where(p => p != null && p.clothing != null).GroupBy(p => p.clothing).Select(g => g.First()).ToArray();
        string Signature(ReferenceOutfit outfit) => string.Join("|", outfit.items.Select(c => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c))).OrderBy(id => id, StringComparer.Ordinal));
        asset.references = (asset.references ?? Array.Empty<ReferenceOutfit>()).Concat(examples)
            .Where(r => r != null && r.items != null && r.items.All(c => c != null))
            .GroupBy(Signature).Select(g => g.First()).ToArray();
        // Earlier unrigged definitions are the same garments, not new style variants.
        var folders = new[] { SvinkiAssetPaths.ClothingDefinitions, SvinkiAssetPaths.ClothingTemplateDefinitions,
            SvinkiAssetPaths.ClothingRoot + "/Compatibility/Definitions" }.Where(AssetDatabase.IsValidFolder).ToArray();
        var all = AssetDatabase.FindAssets("", folders)
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ClothingDefinition>).Where(c => c != null).ToArray();
        foreach (var profile in asset.items)
        {
            profile.aliases = (profile.aliases ?? Array.Empty<ClothingDefinition>())
                .Concat(all.Where(c => c != profile.clothing && c.Model != null && Key(c) == profile.key))
                .Where(c => c != null && c != profile.clothing).Distinct().ToArray();
        }
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
        Debug.Log("Rating rules saved: " + asset.items.Length + " garments and " + asset.references.Length + " unique reference outfits.");
    }
    private static string Key(ClothingDefinition clothing)
    {
        if (AssetDatabase.GetAssetPath(clothing).StartsWith(SvinkiAssetPaths.ClothingTemplateDefinitions + "/", StringComparison.Ordinal))
        {
            switch (clothing.name)
            {
                case "Hat1": return "hat1";
                case "Shirt1": return "shirt1";
                case "Pants1": return "pants2";
                case "Sneakers2": return "sneakers2";
            }
        }
        return clothing.Model.name.ToLowerInvariant().Replace("_worn", "").Replace("_withtex", "").Replace("_tex", "");
    }
}
