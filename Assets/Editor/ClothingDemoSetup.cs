using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ClothingDemoSetup
{
    private const string ItemFolder = "Assets/ClothingItems";
    private const string MaterialFolder = "Assets/Materials/Clothing";
    private const string SourceFolder = "Assets/Сlothes"; // Первая буква в исходной папке — кириллическая С.

    public static void ApplySampleScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Apply();
    }

    [MenuItem("Tools/Clothes/Apply Clothing to Pickups")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Сначала остановите Play Mode.");
            return;
        }

        GameObject mannequin = GameObject.Find("HUD Character Model");
        if (mannequin == null)
        {
            Debug.LogError("Не найден HUD Character Model в открытой сцене.");
            return;
        }

        EnsureFolder(ItemFolder);
        EnsureFolder(MaterialFolder);
        Material hatFabric = MakeMaterial(MaterialFolder + "/Hat Fabric.mat", new Color(.22f, .27f, .35f), false, true);
        Material shirtFabric = MakeMaterial(MaterialFolder + "/Shirt Fabric.mat", new Color(.82f, .71f, .51f), false, true);
        Material pantsFabric = MakeMaterial(MaterialFolder + "/Jeans Fabric.mat", new Color(.25f, .37f, .57f), false, true);
        Material shoesFabric = MakeMaterial(MaterialFolder + "/Shoes Fabric.mat", new Color(.89f, .92f, .94f), false, true);
        Material redGlow = MakeContourMaterial(MaterialFolder + "/Hat Highlight.mat", new Color(.98f, .24f, .23f), .008f);
        Material yellowGlow = MakeContourMaterial(MaterialFolder + "/Shirt Highlight.mat", new Color(1f, .79f, .13f), .008f);
        Material greenGlow = MakeContourMaterial(MaterialFolder + "/Jeans Highlight.mat", new Color(.31f, .87f, .40f), .008f);
        Material blueGlow = MakeContourMaterial(MaterialFolder + "/Shoes Highlight.mat", new Color(.20f, .60f, 1f), .008f);

        ClothingDefinition hat = MakeItem<HatClothing>("Hat1.asset", "Hat", "Hat1.fbx", hatFabric, redGlow,
            new Color(.98f, .24f, .23f), new Vector3(0f, 1.77f, -.25f), new Vector2(.32f, .21f));
        ClothingDefinition shirt = MakeItem<ShirtClothing>("Shirt1.asset", "T-Shirt", "Shirt1 (3).fbx", shirtFabric, yellowGlow,
            new Color(1f, .79f, .13f), new Vector3(0f, 1.19f, -.29f), new Vector2(.85f, .67f), new Vector3(90f, 0f, 0f));
        ClothingDefinition pants = MakeItem<PantsClothing>("Pants1.asset", "Jeans", "pants1.fbx", pantsFabric, greenGlow,
            new Color(.31f, .87f, .40f), new Vector3(0f, .66f, -.27f), new Vector2(.52f, .87f), new Vector3(90f, 0f, 0f));
        ClothingDefinition shoes = MakeItem<ShoesClothing>("Sneakers2.asset", "Sneakers", "sneakers2.fbx", shoesFabric, blueGlow,
            new Color(.20f, .60f, 1f), new Vector3(0f, .12f, -.27f), new Vector2(.20f, .13f), new Vector3(0f, 0f, 90f));

        MannequinWardrobe wardrobe = mannequin.GetComponent<MannequinWardrobe>();
        if (wardrobe == null) wardrobe = Undo.AddComponent<MannequinWardrobe>(mannequin);
        SerializedObject wardrobeData = new SerializedObject(wardrobe);
        wardrobeData.FindProperty("mannequinRoot").objectReferenceValue = mannequin.transform;
        wardrobeData.ApplyModifiedProperties();

        ConfigurePickup("Pickup Red Head", "Hat", hat, wardrobe);
        ConfigurePickup("Pickup Yellow Torso", "T-Shirt", shirt, wardrobe);
        ConfigurePickup("Pickup Green Legs", "Jeans", pants, wardrobe);
        ConfigurePickup("Pickup Blue Shoes", "Sneakers", shoes, wardrobe);

        // Старые плоские маркеры на Canvas больше не нужны: одежду показывает камера портрета.
        foreach (string name in new[] { "Head Red Indicator", "Torso Yellow Indicator", "Legs Green Indicator", "Shoes Blue Indicator" })
        {
            Transform indicator = FindChildByName(GameObject.Find("Canvas")?.transform, name);
            if (indicator != null) indicator.gameObject.SetActive(false);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(mannequin.scene);
        EditorSceneManager.SaveScene(mannequin.scene);
        Debug.Log("[Clothes] Четыре предмета настроены. Подберите их на E: одежда появится на манекене справа.");
    }

    [MenuItem("Tools/Clothes/Refresh Pickup Models")]
    public static void RefreshPickupModels()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureFolder(MaterialFolder);
        foreach (ClothingPickup wearable in Object.FindObjectsByType<ClothingPickup>(FindObjectsInactive.Include))
            if (wearable.Clothing != null) BuildPickupModel(wearable.gameObject, wearable.Clothing);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    private static void ConfigurePickup(string objectName, string itemName, ClothingDefinition clothing,
        MannequinWardrobe wardrobe)
    {
        GameObject cube = GameObject.Find(objectName);
        if (cube == null) { Debug.LogError("Не найден предмет: " + objectName); return; }
        PickupItem pickup = cube.GetComponent<PickupItem>();
        if (pickup == null) { Debug.LogError("Нет PickupItem у " + objectName); return; }

        BuildPickupModel(cube, clothing);

        SerializedObject pickupData = new SerializedObject(pickup);
        pickupData.FindProperty("itemName").stringValue = itemName;
        pickupData.FindProperty("highlightColor").colorValue = clothing.HighlightColor;
        pickupData.FindProperty("glowStrength").floatValue = 0f;
        pickupData.FindProperty("sparkleRate").floatValue = 0f;
        pickupData.FindProperty("preserveBaseColor").boolValue = true;
        pickupData.FindProperty("onPickedUp").FindPropertyRelative("m_PersistentCalls.m_Calls").ClearArray();
        pickupData.ApplyModifiedProperties();

        ClothingPickup wearable = cube.GetComponent<ClothingPickup>();
        if (wearable == null) wearable = Undo.AddComponent<ClothingPickup>(cube);
        SerializedObject wearableData = new SerializedObject(wearable);
        wearableData.FindProperty("clothing").objectReferenceValue = clothing;
        wearableData.FindProperty("wardrobe").objectReferenceValue = wardrobe;
        wearableData.ApplyModifiedProperties();
    }

    private static void BuildPickupModel(GameObject pickup, ClothingDefinition clothing)
    {
        if (clothing.Model == null)
        {
            Debug.LogWarning("Для " + pickup.name + " не назначена 3D-модель одежды.", pickup);
            return;
        }

        foreach (string oldName in new[] { "Colored Outline", "Clothing Model", "Clothing Contour" })
        {
            Transform old = pickup.transform.Find(oldName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        MeshRenderer square = pickup.GetComponent<MeshRenderer>();
        if (square != null) { Undo.RecordObject(square, "Hide pickup square"); square.enabled = false; }

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(clothing.Model, pickup.transform);
        Undo.RegisterCreatedObjectUndo(model, "Create clothing pickup model");
        model.name = "Clothing Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(clothing.DisplayRotation);
        model.transform.localScale = Vector3.one;
        PreparePickupRenderers(model, clothing.FabricMaterial);

        Bounds bounds = LocalBounds(model, pickup.transform);
        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, .001f);
        float scale = 1.3f / maxSize;
        model.transform.localScale = Vector3.one * scale;
        model.transform.localPosition = -bounds.center * scale;

        string assetPath = AssetDatabase.GetAssetPath(clothing);
        string key = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(key)) key = clothing.name;
        Material contourMaterial = MakeContourMaterial(MaterialFolder + "/Pickup Contour " + key + ".mat",
            clothing.HighlightColor, .025f);
        GameObject contour = (GameObject)PrefabUtility.InstantiatePrefab(clothing.Model, pickup.transform);
        Undo.RegisterCreatedObjectUndo(contour, "Create clothing contour");
        contour.name = "Clothing Contour";
        contour.transform.localPosition = model.transform.localPosition;
        contour.transform.localRotation = model.transform.localRotation;
        contour.transform.localScale = model.transform.localScale;
        PreparePickupRenderers(contour, contourMaterial);

        BoxCollider box = pickup.GetComponent<BoxCollider>();
        if (box != null)
        {
            Undo.RecordObject(box, "Fit clothing pickup collider");
            box.center = Vector3.zero;
            box.size = new Vector3(Mathf.Max(.15f, bounds.size.x * scale),
                Mathf.Max(.15f, bounds.size.y * scale), Mathf.Max(.25f, bounds.size.z * scale));
        }
    }

    private static void PreparePickupRenderers(GameObject root, Material material)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (material != null)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static Bounds LocalBounds(GameObject root, Transform reference)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
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
                if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                else result.Encapsulate(point);
            }
        }
        return result;
    }

    private static Material MakeContourMaterial(string path, Color color, float width)
    {
        Shader shader = Shader.Find("Svinki/Clothing Contour");
        if (shader == null) throw new System.InvalidOperationException("Не найден шейдер Clothing Contour.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else material.shader = shader;
        material.SetColor("_OutlineColor", color);
        material.SetFloat("_OutlineWidth", width);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static T MakeItem<T>(string filename, string title, string modelName, Material fabric,
        Material glow, Color accent, Vector3 center, Vector2 size, Vector3 rotation = default)
        where T : ClothingDefinition
    {
        string path = ItemFolder + "/" + filename;
        T item = AssetDatabase.LoadAssetAtPath<T>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(item, path);
        }
        SerializedObject data = new SerializedObject(item);
        data.FindProperty("displayName").stringValue = title;
        data.FindProperty("model").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFolder + "/" + modelName);
        data.FindProperty("fabricMaterial").objectReferenceValue = fabric;
        data.FindProperty("outlineMaterial").objectReferenceValue = glow;
        data.FindProperty("highlightColor").colorValue = accent;
        data.FindProperty("displayCenter").vector3Value = center;
        data.FindProperty("displaySize").vector2Value = size;
        data.FindProperty("displayRotation").vector3Value = rotation;
        data.ApplyModifiedProperties();
        if (item.Model == null) Debug.LogError("Не найдена модель одежды: " + modelName);
        return item;
    }

    private static Material MakeMaterial(string path, Color color, bool backFacesOnly, bool bothFaces = false, bool emissive = false)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Cull", backFacesOnly ? 1f : bothFaces ? 0f : 2f);
        material.SetFloat("_Smoothness", .22f);
        if (emissive || backFacesOnly)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * (emissive ? 1.6f : .8f));
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string folder)
    {
        string parent = "Assets";
        foreach (string part in folder.Substring("Assets/".Length).Split('/'))
        {
            string path = parent + "/" + part;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, part);
            parent = path;
        }
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindChildByName(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
