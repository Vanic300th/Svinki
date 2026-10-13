#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FishNet.Managing.Object;
using FishNet.Object;

/// <summary>
/// Read-only migration checks. Run `return AssetRefactorValidation.Run();` via Unity MCP
/// or use the Svinki/Validation menu after asset import and compilation finish.
/// Reads existing assets; never enters Play Mode, opens/saves scenes or changes assets.
/// Baseline lives outside Assets so moving project content does not invalidate it.
/// </summary>
public static class AssetRefactorValidation
{
    [MenuItem("Svinki/Validation/Check asset refactor")]
    public static void CheckFromMenu()
    {
        var report = Run();
        Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
    }

    /// <summary>Optionally saves a diagnostic report under Docs/QA; project assets remain read-only.</summary>
    public static string WriteReport(string relativePath = "Docs/QA/asset-refactor-validation.json", bool assertSuccess = false)
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        string reportPath = Path.GetFullPath(Path.Combine(root, relativePath));
        string reportFolder = Path.GetFullPath(Path.Combine(root, "Docs/QA")) + Path.DirectorySeparatorChar;
        if (!reportPath.StartsWith(reportFolder, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Validation reports must be saved under Docs/QA.", nameof(relativePath));
        object report = Run();
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        if (assertSuccess && !JObject.FromObject(report).Value<bool>("success"))
            throw new InvalidOperationException("Asset refactor validation failed; see " + reportPath);
        return reportPath;
    }

    public static object Run(bool assertSuccess = false)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Finish asset import and stop Play Mode before validating the refactor.");

        string root = Path.GetDirectoryName(Application.dataPath);
        var baseline = JObject.Parse(File.ReadAllText(Path.Combine(root, "Tools/Validation/AssetRefactorBaseline.json")));
        var failures = new List<string>();
        var missingGuidReferences = new List<object>();
        var missingScripts = new List<string>();
        var staleNetworkHashes = new List<object>();
        var pathsByGuid = new Dictionary<string, string>();

        Func<string, string> assetPath = guid =>
        {
            if (!pathsByGuid.TryGetValue(guid, out string path))
                pathsByGuid[guid] = path = AssetDatabase.GUIDToAssetPath(guid);
            return path;
        };
        Func<string, string> protectedPath = originalPath =>
        {
            var entry = baseline["protectedAssets"].First(x => (string)x["path"] == originalPath);
            return assetPath((string)entry["guid"]);
        };

        int originalSourceFiles = 0;
        long originalSourceBytes = 0;
        foreach (var entry in baseline["originalArtSource"])
        {
            string path = (string)entry["path"];
            string fullPath = Path.Combine(root, path);
            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length != (long)entry["bytes"] ||
                Hash(fullPath) != (string)entry["sha256"])
                failures.Add("Original ArtSource file missing or changed: " + path);
            originalSourceFiles++;
            originalSourceBytes += (long)entry["bytes"];
        }
        foreach (var entry in baseline["protectedAssets"])
            if (string.IsNullOrEmpty(assetPath((string)entry["guid"])))
                failures.Add("Protected asset GUID no longer resolves: " + (string)entry["path"]);
        foreach (var entry in baseline["runtimeFiles"])
        {
            string path = (string)entry["path"];
            string fullPath = Path.Combine(root, path);
            if (!File.Exists(fullPath) || Hash(fullPath) != (string)entry["sha256"])
                failures.Add("Required runtime payload missing or changed: " + path);
        }
        var sqliteImporter = AssetImporter.GetAtPath("Assets/Plugins/SQLite/Windows/x86_64/sqlite3.dll") as PluginImporter;
        if (sqliteImporter == null || !sqliteImporter.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64))
            failures.Add("SQLite plugin is not enabled for Windows x64.");

        string[] resources = { "Assets/Resources/Pigs/PigAvatar.prefab", "Assets/Resources/OutfitRating.asset",
            "Assets/Resources/GameLanguageIndex.json", "Assets/Resources/PhotoCameraHand.mat" };
        foreach (string path in resources)
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                failures.Add("Required Resources address changed or missing: " + path);

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(protectedPath("Assets/Prefabs/NetworkPlayer.prefab"));
        var player = playerPrefab == null ? null : playerPrefab.GetComponent<NetworkPlayer>();
        int catalogCount = 0;
        int savedIdsResolved = 0;
        var catalog = new List<ClothingDefinition>();
        if (player == null) failures.Add("NetworkPlayer prefab/component missing.");
        else
        {
            var serializedCatalog = new SerializedObject(player).FindProperty("clothingCatalog");
            catalogCount = serializedCatalog.arraySize;
            for (int i = 0; i < catalogCount; i++)
            {
                var definition = serializedCatalog.GetArrayElementAtIndex(i).objectReferenceValue as ClothingDefinition;
                if (definition == null) failures.Add("Null clothing catalog entry: " + i);
                else catalog.Add(definition);
            }
            foreach (var duplicate in catalog.GroupBy(c => c.name).Where(g => g.Count() != 1))
                failures.Add("Ambiguous saved clothing ID: " + duplicate.Key);
            var resolve = typeof(NetworkPlayer).GetMethod("FindInCatalog", BindingFlags.Instance | BindingFlags.NonPublic);
            if (resolve == null) failures.Add("Saved-ID resolver FindInCatalog missing.");
            else foreach (var entry in baseline["clothingIds"])
            {
                var definition = resolve.Invoke(player, new object[] { (string)entry["name"] }) as ClothingDefinition;
                if (definition == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition)) != (string)entry["guid"])
                    failures.Add("Saved clothing ID no longer resolves to its original definition: " + (string)entry["name"]);
                else savedIdsResolved++;
            }
        }

        var rating = AssetDatabase.LoadAssetAtPath<OutfitRatingCatalog>(protectedPath("Assets/Resources/OutfitRating.asset"));
        int activeClothing = 0;
        int aliasesChecked = 0;
        int legacyVisualsChecked = 0;
        var templateGuids = new HashSet<string>(baseline["clothingIds"]
            .Where(x => ((string)x["path"]).StartsWith("Assets/ClothingItems/", StringComparison.Ordinal))
            .Select(x => (string)x["guid"]));
        if (rating == null) failures.Add("OutfitRating catalog missing.");
        else
        {
            var actualGuids = rating.items.Where(p => p != null && p.clothing != null)
                .Select(p => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p.clothing))).ToArray();
            var expectedGuids = baseline["activeRatingGuids"].Select(x => (string)x).ToArray();
            if (!actualGuids.OrderBy(x => x).SequenceEqual(expectedGuids.OrderBy(x => x)))
                failures.Add("The original approved wardrobe changed (expected " + expectedGuids.Length + " definitions).");
            foreach (var profile in rating.items)
            {
                if (profile == null || profile.clothing == null) { failures.Add("Null rating profile/definition."); continue; }
                var definition = profile.clothing;
                activeClothing++;
                CheckDefinition(definition, failures);
                if (!catalog.Contains(definition)) failures.Add("Approved clothing absent from player catalog: " + definition.name);
                foreach (var alias in profile.aliases ?? Array.Empty<ClothingDefinition>())
                {
                    aliasesChecked++;
                    if (alias == null) { failures.Add("Null legacy alias for: " + definition.name); continue; }
                    if (!catalog.Contains(alias)) failures.Add("Legacy alias absent from player catalog: " + alias.name);
                    if (rating.FindClothing(alias.name) != alias || rating.Profile(alias) != profile)
                        failures.Add("Rating resolver lost legacy alias: " + alias.name);
                    // Four original templates intentionally have no generated pickup prefab.
                    if (templateGuids.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(alias)))) continue;
                    legacyVisualsChecked++;
                    CheckDefinition(alias, failures);
                    if (alias.Model != definition.Model || alias.WornModel != definition.WornModel ||
                        alias.FabricMaterial != definition.FabricMaterial || alias.OutlineMaterial != definition.OutlineMaterial)
                        failures.Add("Legacy alias still uses old visual assets: " + alias.name);
                }
            }
        }

        var registry = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(protectedPath("Assets/DefaultPrefabObjects.asset"));
        int registeredNetworkPrefabs = 0;
        if (registry == null) failures.Add("FishNet default prefab registry missing.");
        else
        {
            registeredNetworkPrefabs = registry.Prefabs.Count;
            var registeredGuids = new List<string>();
            var hashes = new HashSet<ulong>();
            foreach (NetworkObject networkObject in registry.Prefabs)
            {
                if (networkObject == null) { failures.Add("Null FishNet registry entry."); continue; }
                string path = AssetDatabase.GetAssetPath(networkObject.gameObject);
                registeredGuids.Add(AssetDatabase.AssetPathToGUID(path));
                ulong expected = NetworkHash(path, networkObject.gameObject.name);
                if (expected != networkObject.AssetPathHash)
                    staleNetworkHashes.Add(new { path, actual = networkObject.AssetPathHash.ToString(), expected = expected.ToString() });
                if (networkObject.AssetPathHash == 0 || !hashes.Add(networkObject.AssetPathHash))
                    failures.Add("Zero or duplicate FishNet prefab hash: " + path);
            }
            var expectedGuids = baseline["networkPrefabGuids"].Select(x => (string)x).ToArray();
            if (!registeredGuids.OrderBy(x => x).SequenceEqual(expectedGuids.OrderBy(x => x)))
                failures.Add("FishNet registered prefab GUID set changed.");
        }

        int inspectedPrefabs = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = assetPath(guid);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { failures.Add("Prefab does not load: " + path); continue; }
            inspectedPrefabs++;
            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    missingScripts.Add(path + " :: " + transform.name);
        }

        var yamlExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".asset", ".prefab", ".unity", ".mat", ".controller", ".overridecontroller", ".meta", ".lighting", ".rendertexture" };
        var guidRegex = new Regex(@"guid:\s*([a-f0-9]{32})", RegexOptions.Compiled);
        int serializedFilesInspected = 0;
        foreach (string folder in new[] { Application.dataPath, Path.Combine(root, "ProjectSettings") })
        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (!yamlExtensions.Contains(Path.GetExtension(file))) continue;
            // Binary Unity assets do not contain text GUID dependencies.
            using (var stream = File.OpenRead(file))
                if (stream.ReadByte() != '%' && Path.GetExtension(file) != ".meta") continue;
            serializedFilesInspected++;
            string[] lines = File.ReadAllLines(file);
            for (int line = 0; line < lines.Length; line++)
            foreach (Match match in guidRegex.Matches(lines[line]))
            {
                string guid = match.Groups[1].Value;
                if (guid.StartsWith("0000000000000000", StringComparison.Ordinal) || !string.IsNullOrEmpty(assetPath(guid))) continue;
                missingGuidReferences.Add(new { path = file.Substring(root.Length + 1).Replace('\\', '/'), line = line + 1, guid });
            }
        }

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => new { s.path, exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) != null }).ToArray();
        foreach (var scene in scenes) if (!scene.exists) failures.Add("Build scene missing: " + scene.path);
        if (scenes.Length < 3 || Path.GetFileNameWithoutExtension(scenes[0].path) != "Lobby" ||
            !scenes.Any(s => Path.GetFileNameWithoutExtension(s.path) == "SampleScene") ||
            !scenes.Any(s => Path.GetFileNameWithoutExtension(s.path) == "FriendslopLobby"))
            failures.Add("Build scenes lost bootstrap/gameplay/social-lobby order or names.");
        var openScenes = Enumerable.Range(0, EditorSceneManager.sceneCount).Select(i => EditorSceneManager.GetSceneAt(i))
            .Select(s => new { s.path, s.name, s.isDirty, s.isLoaded }).ToArray();

        bool success = failures.Count == 0 && missingGuidReferences.Count == 0 && missingScripts.Count == 0 && staleNetworkHashes.Count == 0;
        var report = new { success, originalSourceFiles, originalSourceBytes, catalogCount, savedIdsResolved, activeClothing, aliasesChecked, legacyVisualsChecked,
            registeredNetworkPrefabs, inspectedPrefabs, serializedFilesInspected, failures, missingGuidReferences, missingScripts,
            staleNetworkHashes, scenes, openScenes, windowsBuildSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64) };
        if (assertSuccess && !success)
            throw new InvalidOperationException(Newtonsoft.Json.JsonConvert.SerializeObject(report));
        return report;
    }

    private static void CheckDefinition(ClothingDefinition definition, List<string> failures)
    {
        // FabricMaterial is an optional override; imported/baked meshes keep their own materials.
        if (definition.Model == null || definition.WornModel == null ||
            definition.OutlineMaterial == null || definition.PickupPrefab == null)
            failures.Add("Incomplete clothing definition: " + definition.name);
        CheckModelMaterials(definition.WornModel, definition.name + " worn model", failures);
        CheckModelMaterials(definition.PickupPrefab, definition.name + " pickup", failures);
        var pickup = definition.PickupPrefab == null ? null : definition.PickupPrefab.GetComponent<ClothingPickup>();
        if (pickup == null || pickup.Clothing != definition)
            failures.Add("Pickup definition binding broken: " + definition.name);
    }

    private static void CheckModelMaterials(GameObject model, string label, List<string> failures)
    {
        if (model == null) return; // Missing model is reported by CheckDefinition.
        var meshRenderers = model.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer).ToArray();
        if (meshRenderers.Length == 0)
            failures.Add("No mesh renderer in clothing visual: " + label);
        foreach (var renderer in meshRenderers)
            if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null))
                failures.Add("Missing mesh renderer material: " + label + " :: " + renderer.name);
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static ulong NetworkHash(string path, string name)
    {
        string key = (path + name).Trim().ToLowerInvariant();
        unchecked
        {
            ulong value = 14695981039346656037;
            foreach (char c in key)
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) value = value * 1099511628211 ^ c;
            return value;
        }
    }
}
#endif
