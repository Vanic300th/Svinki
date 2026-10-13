using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

/// <summary>Retains saved clothing IDs and registered prefab roots while retiring their old visual sources.</summary>
public static class ClothingCompatibilityMigration
{
    public const string DefinitionFolder = SvinkiAssetPaths.ClothingRoot + "/Compatibility/Definitions";

    private sealed class Pair
    {
        public ClothingDefinition Alias;
        public ClothingDefinition Canonical;
        public GameObject LegacyPickup;
    }

    [MenuItem("Svinki/Одежда/Проверить совместимость старых карточек")]
    private static void ReportMenu() => Debug.Log(Report());

    public static string Report()
    {
        Pair[] pairs = GetPairs();
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            aliases = pairs.Length,
            canonicalGarments = pairs.Select(p => p.Canonical).Distinct().Count(),
            destination = DefinitionFolder,
            definitions = pairs.Select(p => new
            {
                id = p.Alias.name,
                definition = AssetDatabase.GetAssetPath(p.Alias),
                canonical = AssetDatabase.GetAssetPath(p.Canonical),
                retiredModel = AssetDatabase.GetAssetPath(p.Alias.Model),
                retainedPickup = AssetDatabase.GetAssetPath(p.LegacyPickup)
            }).ToArray()
        }, Newtonsoft.Json.Formatting.Indented);
    }

    /// <summary>Explicitly invoked by migration tooling; never runs on compile or import.</summary>
    public static string Apply()
    {
        if (ClothingLibraryImporter.RebuildSuspended)
            throw new InvalidOperationException("Resume clothing import after the folder migration before applying compatibility changes.");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode first.");
        Pair[] pairs = GetPairs();
        Validate(pairs);
        const string ratingPath = "Assets/Resources/OutfitRating.asset";
        var backups = pairs.SelectMany(p => new[] { AssetDatabase.GetAssetPath(p.Alias), AssetDatabase.GetAssetPath(p.LegacyPickup) })
            .Concat(new[] { ratingPath })
            .Distinct().ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        bool suspended = ClothingLibraryImporter.RebuildSuspended;
        ClothingLibraryImporter.RebuildSuspended = true;
        try
        {
            foreach (Pair pair in pairs) RefreshLegacyPickup(pair);
            foreach (Pair pair in pairs)
            {
                // m_Name and the asset GUID are the existing persistence protocol and stay untouched.
                var source = new SerializedObject(pair.Canonical);
                var target = new SerializedObject(pair.Alias);
                foreach (string property in new[] { "displayName", "model", "fabricMaterial", "outlineMaterial", "highlightColor",
                    "displayCenter", "displaySize", "displayRotation", "pigRigged", "pigWornModel", "flutterStrength" })
                    target.CopyFromSerializedProperty(source.FindProperty(property));
                // Dropping a restored legacy ID must spawn its existing registered pickup and return the same ID.
                target.FindProperty("pickupPrefab").objectReferenceValue = pair.LegacyPickup;
                target.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pair.Alias);
            }
            var rating = AssetDatabase.LoadAssetAtPath<OutfitRatingCatalog>(ratingPath);
            foreach (Pair pair in pairs)
            {
                ClothingRatingProfile profile = rating.items.Single(p => p?.clothing == pair.Canonical);
                profile.aliases = (profile.aliases ?? Array.Empty<ClothingDefinition>()).Concat(new[] { pair.Alias }).Distinct().ToArray();
            }
            EditorUtility.SetDirty(rating);
            AssetDatabase.SaveAssets();
            return $"Updated {pairs.Length} compatibility definitions and retained pickup prefab roots. Their GUIDs, names and network components are unchanged. No assets moved or deleted.";
        }
        catch
        {
            // Restore the entire batch if a prefab root identity check or a later update fails.
            foreach (var backup in backups) File.WriteAllBytes(backup.Key, backup.Value);
            foreach (string path in backups.Keys) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            throw;
        }
        finally { ClothingLibraryImporter.RebuildSuspended = suspended; }
    }

    private static Pair[] GetPairs()
    {
        var rating = AssetDatabase.LoadAssetAtPath<OutfitRatingCatalog>("Assets/Resources/OutfitRating.asset");
        if (rating?.items == null) throw new InvalidOperationException("Existing rating aliases are required to prove the garment mapping.");
        var pickups = AssetDatabase.FindAssets("t:Prefab", new[] { SvinkiAssetPaths.ClothingPickups })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var pairs = new Dictionary<ClothingDefinition, Pair>();
        foreach (ClothingRatingProfile profile in rating.items.Where(p => p?.clothing != null))
        foreach (ClothingDefinition alias in (profile.aliases ?? Array.Empty<ClothingDefinition>()).Where(c => c != null))
        {
            string path = AssetDatabase.GetAssetPath(alias);
            if (!path.StartsWith(SvinkiAssetPaths.ClothingDefinitions + "/", StringComparison.Ordinal) &&
                !path.StartsWith(DefinitionFolder + "/", StringComparison.Ordinal)) continue;
            if (alias == profile.clothing) continue;
            if (pairs.TryGetValue(alias, out Pair previous))
            {
                if (previous.Canonical != profile.clothing) throw new InvalidOperationException("Ambiguous rating alias: " + alias.name);
                continue;
            }
            AddPair(alias, profile.clothing);
        }
        // A repaired historical hoodie ID is missing from the rating aliases, but uses exactly
        // the same source object and definition type as a proven alias. Do not infer matches by name.
        var folders = new[] { SvinkiAssetPaths.ClothingDefinitions, DefinitionFolder }.Where(AssetDatabase.IsValidFolder).ToArray();
        var unlisted = AssetDatabase.FindAssets("", folders).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ClothingDefinition>)
            .Where(c => c != null && !c.PigRigged && !pairs.ContainsKey(c)).ToArray();
        foreach (ClothingDefinition alias in unlisted)
        {
            ClothingDefinition[] matches = pairs.Values.Where(p => p.Alias.GetType() == alias.GetType() && p.Alias.Model == alias.Model)
                .Select(p => p.Canonical).Distinct().ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("No proven unique mapping for legacy clothing ID: " + alias.name);
            AddPair(alias, matches[0]);
        }
        return pairs.Values.OrderBy(p => p.Alias.name, StringComparer.Ordinal).ToArray();

        void AddPair(ClothingDefinition alias, ClothingDefinition canonical)
        {
            string pickupPath = pickups.Single(p => Path.GetFileNameWithoutExtension(p) == alias.name);
            pairs.Add(alias, new Pair { Alias = alias, Canonical = canonical,
                LegacyPickup = AssetDatabase.LoadAssetAtPath<GameObject>(pickupPath) });
        }
    }

    private static void Validate(Pair[] pairs)
    {
        if (pairs.Length == 0) throw new InvalidOperationException("No generated compatibility definitions were found.");
        foreach (Pair pair in pairs)
        {
            if (pair.Alias.GetType() != pair.Canonical.GetType() || !pair.Canonical.PigRigged || pair.Canonical.Model == null ||
                pair.Canonical.WornModel == null || pair.Canonical.PickupPrefab == null)
                throw new InvalidOperationException("Incomplete canonical garment: " + pair.Canonical.name);
            if (pair.LegacyPickup == null || pair.LegacyPickup == pair.Canonical.PickupPrefab ||
                pair.LegacyPickup.GetComponent<ClothingPickup>()?.Clothing != pair.Alias ||
                pair.LegacyPickup.GetComponent<NetworkObject>() == null || pair.LegacyPickup.GetComponent<BoxCollider>() == null)
                throw new InvalidOperationException("Unexpected legacy pickup layout: " + pair.Alias.name);
            GameObject canonical = pair.Canonical.PickupPrefab;
            if (canonical.GetComponent<BoxCollider>() == null || canonical.GetComponent<PickupItem>() == null ||
                canonical.transform.childCount == 0 || canonical.GetComponentsInChildren<NetworkObject>(true).Length != 1)
                throw new InvalidOperationException("Unexpected canonical pickup layout: " + pair.Canonical.name);
            // Generated canonical pickup children contain baked meshes only, never a second network object.
            if (canonical.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
                throw new InvalidOperationException("Canonical ground pickup has an unbaked skin: " + pair.Canonical.name);
        }
    }

    private static void RefreshLegacyPickup(Pair pair)
    {
        string path = AssetDatabase.GetAssetPath(pair.LegacyPickup);
        string expectedName = pair.LegacyPickup.name;
        Dictionary<string, string> identities = RootIdentities(pair.LegacyPickup);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // Keep the authored root and every existing root component, including FishNet indices.
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            GameObject canonical = pair.Canonical.PickupPrefab;
            foreach (Transform child in canonical.transform)
            {
                GameObject visual = UnityEngine.Object.Instantiate(child.gameObject, root.transform, false);
                visual.name = child.name;
            }
            BoxCollider collider = root.GetComponent<BoxCollider>();
            BoxCollider sourceCollider = canonical.GetComponent<BoxCollider>();
            collider.center = sourceCollider.center;
            collider.size = sourceCollider.size;
            var source = new SerializedObject(canonical.GetComponent<PickupItem>());
            var target = new SerializedObject(root.GetComponent<PickupItem>());
            foreach (string property in new[] { "itemName", "glowStrength", "sparkleRate", "preserveBaseColor" })
                target.CopyFromSerializedProperty(source.FindProperty(property));
            target.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Dictionary<string, string> actual = RootIdentities(saved);
        if (saved.name != expectedName || identities.Count != actual.Count || identities.Any(p => !actual.TryGetValue(p.Key, out string value) || value != p.Value))
            throw new InvalidOperationException("Saving changed a registered pickup root identity: " + path);
    }

    private static Dictionary<string, string> RootIdentities(GameObject root)
    {
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(root, "GameObject");
        Component[] components = root.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            Add(components[i], i + ":" + components[i].GetType().FullName);
        return identities;

        void Add(UnityEngine.Object item, string key)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out string guid, out long fileId))
                throw new InvalidOperationException("Cannot identify registered prefab object " + key);
            identities.Add(key, guid + ":" + fileId);
        }
    }
}
