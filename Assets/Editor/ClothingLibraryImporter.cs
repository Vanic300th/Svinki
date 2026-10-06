using System;
using System.Collections.Generic;
using System.IO;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Новая модель в Models/Папка автоматически становится карточкой нужного типа и префабом подбора.</summary>
public sealed class ClothingLibraryImporter : AssetPostprocessor
{
    private const string Root = "Assets/ClothingLibrary";
    private const string Models = Root + "/Models";
    private const string PigModels = Root + "/3D Models";
    private const string Definitions = Root + "/Generated/Definitions";
    private const string Pickups = Root + "/Generated/Pickups";
    private const string Materials = Root + "/Generated/Materials";
    private const string PlayerPrefab = "Assets/Prefabs/NetworkPlayer.prefab";

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
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureFolders();

        var definitions = new List<ClothingDefinition>();
        int created = 0;
        foreach (Category category in Categories)
        {
            string folder = Models + "/" + category.Folder;
            string pigFolder = PigModels + "/" + category.Folder;
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                bool pig = path.StartsWith(pigFolder + "/", StringComparison.Ordinal) && path.EndsWith("_worn.fbx", StringComparison.OrdinalIgnoreCase);
                if ((!path.StartsWith(folder + "/", StringComparison.Ordinal) && !pig) || !IsModel(path)) continue;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                ClothingDefinition definition = FindDefinition(model, category);
                if (definition == null)
                {
                    definition = CreateDefinition(model, category);
                    created++;
                }
                definitions.Add(definition);
                if (definition.PigRigged) PigClothingFit.Prepare(definition);
                EnsurePickupPrefab(definition, category);
            }
        }

        RegisterOnNetworkPlayer(definitions);
        if (created > 0) Debug.Log("[Clothing Library] Добавлено вариантов одежды: " + created);
    }

    [MenuItem("Svinki/Одежда/Открыть папки моделей")]
    private static void OpenModelFolders()
    {
        EnsureFolders();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(Models);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Svinki/Одежда/Добавить пример худи")]
    private static void AddHoodieExample()
    {
        EnsureFolders();
        const string source = "Assets/Ye_Clothes/Hoodies/hoodie1.fbx";
        const string destination = Models + "/Hoodies/hoodie1.fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(destination) == null &&
            !AssetDatabase.CopyAsset(source, destination))
        {
            Debug.LogError("Не удалось скопировать пример худи: " + source);
            return;
        }
        Rebuild();
    }

    [MenuItem("Svinki/Одежда/Поставить пример худи на уровень")]
    private static void PlaceHoodieExample()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Rebuild();
        string[] matches = AssetDatabase.FindAssets("t:Prefab", new[] { Pickups + "/Hoodies" });
        if (matches.Length == 0)
        {
            Debug.LogWarning("Сначала выберите «Добавить пример худи».");
            return;
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(matches[0]));
        Scene room = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
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
        if (ContainsModel(imported) || ContainsModel(moved) || ContainsModel(deleted) || ContainsModel(movedFrom))
            Schedule();
    }

    private static bool ContainsModel(string[] paths)
    {
        foreach (string path in paths)
            if ((path.StartsWith(Models + "/", StringComparison.Ordinal) || path.StartsWith(PigModels + "/", StringComparison.Ordinal)) && IsModel(path)) return true;
        return false;
    }

    private static bool IsModel(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension == ".fbx" || extension == ".obj" || extension == ".prefab";
    }

    private static void Schedule()
    {
        if (scheduled) return;
        scheduled = true;
        EditorApplication.delayCall += Rebuild;
    }

    private static void EnsureFolders()
    {
        EnsureFolder(Root);
        EnsureFolder(Models);
        EnsureFolder(Root + "/Generated");
        EnsureFolder(Definitions);
        EnsureFolder(Pickups);
        EnsureFolder(Materials);
        foreach (Category category in Categories)
        {
            EnsureFolder(Models + "/" + category.Folder);
            EnsureFolder(Definitions + "/" + category.Folder);
            EnsureFolder(Pickups + "/" + category.Folder);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static ClothingDefinition FindDefinition(GameObject model, Category category)
    {
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
            "Assets/ClothingItems/" + category.Template + ".asset");

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
            pickupData.FindProperty("highlightColor").colorValue = clothing.HighlightColor;
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
