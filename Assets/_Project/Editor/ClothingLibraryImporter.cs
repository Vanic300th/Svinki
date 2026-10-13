using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Rebuilds explicitly approved garments; copying an FBX does not publish it to the wardrobe.</summary>
public sealed class ClothingLibraryImporter : AssetPostprocessor
{
    private const string Root = SvinkiAssetPaths.ClothingRoot;
    private const string Models = SvinkiAssetPaths.ClothingLegacyModels;
    private const string PigModels = SvinkiAssetPaths.ClothingPigModels;
    private const string Definitions = SvinkiAssetPaths.ClothingDefinitions;
    private const string Pickups = SvinkiAssetPaths.ClothingPickups;
    private const string Materials = SvinkiAssetPaths.ClothingGeneratedMaterials;
    private const string PlayerPrefab = SvinkiAssetPaths.NetworkPlayerPrefab;
    public const string PolicyPath = Root + "/Settings/ImportPolicy.asset";

    // SessionState survives a domain reload, so callbacks cannot undo an asset migration.
    public static bool RebuildSuspended
    {
        get => SvinkiAssetPaths.MigrationActive || SessionState.GetBool("Svinki.ClothingImport.Suspended", false);
        set => SessionState.SetBool("Svinki.ClothingImport.Suspended", value);
    }

    public static ClothingLibraryImportPolicy LoadPolicy() =>
        AssetDatabase.LoadAssetAtPath<ClothingLibraryImportPolicy>(PolicyPath);

    public static ClothingDefinition[] ApprovedDefinitions() =>
        (LoadPolicy()?.approvedDefinitions ?? Array.Empty<ClothingDefinition>())
        .Where(c => c != null).Distinct().ToArray();

    private sealed class Category
    {
        public readonly string Folder;
        public readonly Type Type;
        public readonly string Template;

        public Category(string folder, Type type, string template)
        {
            Folder = folder;
            Type = type;
            Template = template;
        }
    }

    private static readonly Category[] Categories =
    {
        new Category("Hats", typeof(HatClothing), "Hat1"),
        new Category("Shirts", typeof(ShirtClothing), "Shirt1"),
        new Category("Hoodies", typeof(HoodieClothing), "Shirt1"),
        new Category("Pants", typeof(PantsClothing), "Pants1"),
        new Category("Shoes", typeof(ShoesClothing), "Sneakers2")
    };

    private static bool scheduled;

    private void OnPreprocessModel()
    {
        if (RebuildSuspended) return;
        bool softClothing = assetPath.StartsWith(Models + "/Shirts/", StringComparison.Ordinal) ||
                            assetPath.StartsWith(Models + "/Hoodies/", StringComparison.Ordinal) ||
                            assetPath.StartsWith(Models + "/Pants/", StringComparison.Ordinal);
        if (softClothing && assetImporter is ModelImporter model)
            model.isReadable = true; // надетая мягкая одежда меняет копию вершин при движении
    }

    [InitializeOnLoadMethod]
    private static void StartAfterCompile() => Schedule();

    [MenuItem("Svinki/Одежда/Обновить библиотеку моделей")]
    public static void Rebuild()
    {
        scheduled = false;
        if (RebuildSuspended || EditorApplication.isPlayingOrWillChangePlaymode) return;
        ClothingLibraryImportPolicy policy = LoadPolicy();
        if (policy == null) return; // Initialisation is explicit, never a compile-time side effect.
        if (!AssetDatabase.IsValidFolder(PigModels) || AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab) == null)
        {
            Debug.LogWarning("[Clothing Library] Expected clothing source and player prefab paths are missing. Rebuild skipped.");
            return;
        }
        ClothingDefinition[] definitions = ApprovedDefinitions();
        foreach (ClothingDefinition definition in definitions)
            if (definition.Model == null || (!definition.PigRigged && !policy.allowLegacyModels))
                throw new InvalidOperationException("Invalid approved clothing definition: " + AssetDatabase.GetAssetPath(definition));
        EnsureFolders();
        foreach (ClothingDefinition definition in definitions)
        {
            Category category = Categories.Single(c => c.Type == definition.GetType());
            if (definition.PigRigged) PigClothingFit.Prepare(definition);
            EnsurePickupPrefab(definition, category);
        }
        // Existing entries remain resolvable by their saved/network ID, including compatibility definitions.
        RegisterOnNetworkPlayer(definitions.ToList());
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Svinki/Одежда/Создать каталог из правил оценки")]
    public static void CreatePolicyFromRating()
    {
        RequireEditable();
        ClothingLibraryImportPolicy policy = LoadPolicy();
        if (policy != null) { Selection.activeObject = policy; return; }
        var rating = AssetDatabase.LoadAssetAtPath<OutfitRatingCatalog>("Assets/Resources/OutfitRating.asset");
        var definitions = (rating?.items ?? Array.Empty<ClothingRatingProfile>())
            .Where(p => p?.clothing != null).Select(p => p.clothing).Distinct().ToArray();
        if (definitions.Length == 0 || definitions.Any(c => !c.PigRigged || c.Model == null))
            throw new InvalidOperationException("Rating rules must contain the approved pig-rigged wardrobe first.");
        EnsureFolder(Root + "/Settings");
        policy = ScriptableObject.CreateInstance<ClothingLibraryImportPolicy>();
        policy.approvedDefinitions = definitions;
        AssetDatabase.CreateAsset(policy, PolicyPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = policy;
        Debug.Log("[Clothing Library] Approved wardrobe created: " + definitions.Length + " garments.");
    }

    [MenuItem("Svinki/Одежда/Добавить выбранные модели в каталог")]
    public static void AdmitSelectedModels()
    {
        RequireEditable();
        var policy = LoadPolicy();
        if (policy == null) throw new InvalidOperationException("Create the approved wardrobe from rating rules first.");
        var models = Selection.objects.OfType<GameObject>()
            .Where(model => IsModel(AssetDatabase.GetAssetPath(model))).Distinct().ToArray();
        if (models.Length == 0) throw new InvalidOperationException("Select clothing source models in the Project window.");
        var entries = new List<(GameObject model, Category category)>();
        foreach (GameObject model in models)
        {
            string path = AssetDatabase.GetAssetPath(model);
            bool pig = path.StartsWith(PigModels + "/", StringComparison.Ordinal) && path.EndsWith("_worn.fbx", StringComparison.OrdinalIgnoreCase);
            string source = pig ? PigModels : Models;
            Category category = Categories.FirstOrDefault(c => path.StartsWith(source + "/" + c.Folder + "/", StringComparison.Ordinal));
            if (category == null || (!pig && !policy.allowLegacyModels))
                throw new InvalidOperationException("Select a rigged _worn.fbx inside the appropriate clothing category: " + path);
            entries.Add((model, category));
        }
        EnsureFolders();
        var approved = ApprovedDefinitions().ToList();
        foreach (var entry in entries)
        {
            ClothingDefinition definition = FindDefinition(entry.model, entry.category) ?? CreateDefinition(entry.model, entry.category);
            if (!approved.Contains(definition)) approved.Add(definition);
        }
        policy.approvedDefinitions = approved.ToArray();
        EditorUtility.SetDirty(policy);
        Rebuild();
    }

    private static void RequireEditable()
    {
        if (RebuildSuspended) throw new InvalidOperationException("Clothing import is suspended during asset migration.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
    }

    [MenuItem("Svinki/Одежда/Открыть папки моделей")]
    private static void OpenModelFolders()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(PigModels);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Svinki/Одежда/Выбрать пример худи")]
    private static void SelectHoodieExample()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PigModels + "/Hoodies/hoodie1/hoodie1_worn.fbx");
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Svinki/Одежда/Поставить пример худи на уровень")]
    private static void PlaceHoodieExample()
    {
        RequireEditable();
        Rebuild();
        GameObject source = ApprovedDefinitions().FirstOrDefault(c => c.Model != null && c.Model.name == "hoodie1_worn")?.PickupPrefab;
        if (source == null)
        {
            Debug.LogWarning("Сначала добавьте выбранную модель худи в утверждённый каталог.");
            return;
        }

        Scene room = EditorSceneManager.OpenScene(SvinkiAssetPaths.GameplayScene, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject root in room.GetRootGameObjects())
                if (root.name == "Pickup hoodie1") return;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, room);
            instance.name = "Pickup hoodie1";
            instance.transform.position = new Vector3(2.5f, .85f, 1.5f);
            EditorSceneManager.SaveScene(room);
        }
        finally { EditorSceneManager.CloseScene(room, true); }
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (RebuildSuspended) return;
        if (ContainsModel(imported) || ContainsModel(moved) || ContainsModel(deleted) || ContainsModel(movedFrom))
            Schedule();
    }

    private static bool ContainsModel(string[] paths)
    {
        var approvedPaths = new HashSet<string>(ApprovedDefinitions().Where(c => c.Model != null)
            .Select(c => AssetDatabase.GetAssetPath(c.Model)), StringComparer.Ordinal);
        foreach (string path in paths)
            if (approvedPaths.Contains(path)) return true;
        return false;
    }

    private static bool IsModel(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension == ".fbx" || extension == ".obj" || extension == ".prefab";
    }

    private static void Schedule()
    {
        if (scheduled || RebuildSuspended) return;
        scheduled = true;
        EditorApplication.delayCall += Rebuild;
    }

    private static void EnsureFolders()
    {
        EnsureFolder(Root);
        EnsureFolder(Root + "/Generated");
        EnsureFolder(Definitions);
        EnsureFolder(Pickups);
        EnsureFolder(Materials);
        foreach (Category category in Categories)
        {
            EnsureFolder(Definitions + "/" + category.Folder);
            EnsureFolder(Pickups + "/" + category.Folder);
        }
    }

    private static void EnsureFolder(string path)
    {
        SvinkiAssetPaths.EnsureFolder(path);
    }

    private static ClothingDefinition FindDefinition(GameObject model, Category category)
    {
        ClothingDefinition approved = ApprovedDefinitions().FirstOrDefault(c => c.GetType() == category.Type && c.Model == model);
        if (approved != null) return approved;
        foreach (string guid in AssetDatabase.FindAssets("", new[] { Definitions + "/" + category.Folder }))
        {
            ClothingDefinition item = AssetDatabase.LoadAssetAtPath<ClothingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && item.GetType() == category.Type && item.Model == model) return item;
        }
        return null;
    }

    private static ClothingDefinition CreateDefinition(GameObject model, Category category)
    {
        string sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(model));
        string safeName = model.name.Replace('/', '_').Replace('\\', '_');
        string path = AssetDatabase.GenerateUniqueAssetPath(
            Definitions + "/" + category.Folder + "/" + safeName + " " + sourceGuid.Substring(0, 8) + ".asset");
        ClothingDefinition item = (ClothingDefinition)ScriptableObject.CreateInstance(category.Type);
        item.name = Path.GetFileNameWithoutExtension(path);
        ClothingDefinition template = AssetDatabase.LoadAssetAtPath<ClothingDefinition>(
            SvinkiAssetPaths.ClothingTemplateDefinitions + "/" + category.Template + ".asset");

        SerializedObject data = new SerializedObject(item);
        bool pig = AssetDatabase.GetAssetPath(model).StartsWith(PigModels + "/", StringComparison.Ordinal);
        data.FindProperty("displayName").stringValue = pig ? PigClothingAssets.DisplayName(model.name) : model.name;
        data.FindProperty("pigRigged").boolValue = pig;
        data.FindProperty("model").objectReferenceValue = model;
        // Без fabricMaterial исходные текстуры FBX сохраняются.
        if (template != null)
        {
            data.FindProperty("outlineMaterial").objectReferenceValue = template.OutlineMaterial;
            data.FindProperty("highlightColor").colorValue = template.HighlightColor;
            data.FindProperty("displayCenter").vector3Value = template.DisplayCenter;
            data.FindProperty("displaySize").vector2Value = template.DisplaySize;
            data.FindProperty("displayRotation").vector3Value = template.DisplayRotation;
            data.FindProperty("flutterStrength").floatValue = template.FlutterStrength;
            if (item is ShoesClothing && template is ShoesClothing shoes)
                data.FindProperty("pairSpacing").floatValue = shoes.PairSpacing;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    private static void EnsurePickupPrefab(ClothingDefinition clothing, Category category)
    {
        string filename = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(clothing));
        string path = Pickups + "/" + category.Folder + "/" + filename + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) { LinkPickup(clothing, existing); return; }

        if (clothing.PigRigged)
        {
            PigClothingAssets.CreatePickup(clothing, path);
            LinkPickup(clothing, AssetDatabase.LoadAssetAtPath<GameObject>(path));
            return;
        }

        GameObject root = new GameObject("Pickup " + clothing.DisplayName);
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(clothing.Model, root.transform);
            model.name = "Clothing Model";
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(clothing.DisplayRotation));
            model.transform.localScale = Vector3.one;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

            Bounds bounds = LocalBounds(model, root.transform);
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, .001f);
            float scale = 1.2f / maxSize;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = Vector3.up * .7f - bounds.center * scale;

            Material contourMaterial = PickupContour(category, clothing.HighlightColor);
            GameObject contour = (GameObject)PrefabUtility.InstantiatePrefab(clothing.Model, root.transform);
            contour.name = "Clothing Contour";
            contour.transform.SetLocalPositionAndRotation(model.transform.localPosition, model.transform.localRotation);
            contour.transform.localScale = model.transform.localScale;
            foreach (Collider collider in contour.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Renderer renderer in contour.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = contourMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.up * .7f;
            box.size = new Vector3(Mathf.Max(.2f, bounds.size.x * scale),
                Mathf.Max(.2f, bounds.size.y * scale), Mathf.Max(.2f, bounds.size.z * scale));

            PickupItem pickup = root.AddComponent<PickupItem>();
            SerializedObject pickupData = new SerializedObject(pickup);
            pickupData.FindProperty("itemName").stringValue = clothing.DisplayName;
            pickupData.FindProperty("glowStrength").floatValue = 0f;
            pickupData.FindProperty("sparkleRate").floatValue = 0f;
            pickupData.FindProperty("preserveBaseColor").boolValue = true;
            pickupData.ApplyModifiedPropertiesWithoutUndo();

            ClothingPickup wearable = root.AddComponent<ClothingPickup>();
            SerializedObject wearableData = new SerializedObject(wearable);
            wearableData.FindProperty("clothing").objectReferenceValue = clothing;
            wearableData.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkPickup>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            LinkPickup(clothing, AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void LinkPickup(ClothingDefinition clothing, GameObject prefab)
    {
        if (clothing.PickupPrefab == prefab) return;
        var data = new SerializedObject(clothing); data.FindProperty("pickupPrefab").objectReferenceValue = prefab;
        data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(clothing);
    }

    private static Material PickupContour(Category category, Color color)
    {
        string path = Materials + "/" + category.Folder + " Pickup Contour.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Svinki/Clothing Contour");
        if (shader == null) throw new InvalidOperationException("Не найден шейдер Svinki/Clothing Contour");
        material = new Material(shader);
        material.SetColor("_OutlineColor", color);
        material.SetFloat("_OutlineWidth", .025f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Bounds LocalBounds(GameObject model, Transform reference)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 point = reference.InverseTransformPoint(new Vector3(
                    x == 0 ? world.min.x : world.max.x,
                    y == 0 ? world.min.y : world.max.y,
                    z == 0 ? world.min.z : world.max.z));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }

    private static void RegisterOnNetworkPlayer(List<ClothingDefinition> definitions)
    {
        if (definitions.Count == 0) return;
        GameObject prefab = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            NetworkPlayer player = prefab.GetComponent<NetworkPlayer>();
            if (player == null) return;
            SerializedObject data = new SerializedObject(player);
            SerializedProperty catalog = data.FindProperty("clothingCatalog");
            bool changed = false;
            foreach (ClothingDefinition definition in definitions)
            {
                bool found = false;
                for (int i = 0; i < catalog.arraySize; i++)
                    if (catalog.GetArrayElementAtIndex(i).objectReferenceValue == definition)
                    {
                        found = true;
                        break;
                    }
                if (found) continue;
                int index = catalog.arraySize;
                catalog.InsertArrayElementAtIndex(index);
                catalog.GetArrayElementAtIndex(index).objectReferenceValue = definition;
                changed = true;
            }
            if (!changed) return;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }
}
