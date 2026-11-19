using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Displays the 71 new rigged garments from commit 08c0c95, one pig per item.</summary>
public static class NewClothingGallerySetup
{
    public const string ScenePath = SvinkiAssetPaths.Scenes + "/Development/NewClothingGallery.unity";
    private const string AssetsFolder = SvinkiAssetPaths.Content + "/Clothing/Gallery";
    private const string SourceFolder = "Assets/ClothingLibrary/3D Models";
    private const int Columns = 8;
    private const float ColumnSpacing = 3.1f;
    private const float RowSpacing = 4.8f;

    private static readonly string[] NeutralBodyParts =
    {
        "Body", "Ear_L", "Ear_R", "Snout", "Nostril_L", "Nostril_R",
        "Hoof_Foot_L", "Hoof_Foot_R", "Hoof_Hand_L", "Hoof_Hand_R",
        "Eye_Open_-1", "Eye_Open_1", "Brow_Neutral_-1", "Brow_Neutral_1"
    };

    private static readonly (string folder, Type type, string names)[] Categories =
    {
        ("Hats", typeof(HatClothing), "duckring1 duckring2 duckring3 hijab4 hijab5 sunhat4 turban4"),
        ("Hoodies", typeof(HoodieClothing), "abaya4 abaya5 bathrobe4 kigurumi1 kigurumi2 kigurumi3 puffer4 puffer5"),
        ("Pants", typeof(PantsClothing), "baggy1 baggy2 baggy3 baggy4 baggy5 jeans1 jeans2 jeans3 kilt1 kilt2 kilt3 shorts1 shorts2 shorts3 skinny1 skinny2 skinny3 trunks1 trunks2 trunks3"),
        ("Shirts", typeof(ShirtClothing), "beachshirt1 beachshirt2 beachshirt3 buttonshirt1 buttonshirt2 buttonshirt3 buttonshirt4 dress1 dress2 dress3 dress4 emotop1 emotop2 emotop3 kimono1 kimono2 kimono3 kimono4 sundress4 sundress5 swimsuit1 swimsuit2 swimsuit3"),
        ("Shoes", typeof(ShoesClothing), "newrocks1 newrocks2 newrocks3 slides1 slides2 slides3 slipons1 slipons2 slipons3 slippers4 tufli1 tufli2 tufli3")
    };

    [MenuItem("Svinki/Одежда/Создать выставку новой одежды (08c0c95)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before creating the gallery.");
        Scene existing = SceneManager.GetSceneByPath(ScenePath);
        if (existing.IsValid() && existing.isLoaded)
        {
            if (existing.isDirty) throw new InvalidOperationException("Save gallery changes before rebuilding it.");
        }

        var avatar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Pigs/PigAvatar.prefab");
        if (avatar == null) throw new InvalidOperationException("Pig avatar prefab is missing.");
        foreach (var category in Categories)
            foreach (string item in category.names.Split(' '))
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(category.folder, item)) == null)
                    throw new InvalidOperationException("Missing imported garment: " + item);

        SvinkiAssetPaths.EnsureFolder(AssetsFolder);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        if (existing.IsValid() && existing.isLoaded) EditorSceneManager.CloseScene(existing, true);
        var root = new GameObject("New clothing - commit 08c0c95 - 71 exhibits");
        Material floor = MakeMaterial("Floor", new Color(.16f, .20f, .25f));
        Material pedestal = MakeMaterial("Pedestal", new Color(.31f, .39f, .45f));
        Material trim = MakeMaterial("Accent", new Color(.97f, .65f, .44f));
        float depth = 0;
        int total = 0;
        foreach (var category in Categories)
        {
            string[] items = category.names.Split(' ');
            Transform section = new GameObject(category.folder + " - " + items.Length + " new items").transform;
            section.SetParent(root.transform, false);
            section.localPosition = new Vector3(0, 0, -depth);
            Label(section, category.folder.ToUpperInvariant() + " / " + items.Length,
                new Vector3(0, 3.2f, 0), .1f, TextAnchor.MiddleCenter);
            Box(section, "Section stripe", new Vector3(0, .015f, 1.6f), new Vector3(26, .025f, .12f), trim);
            for (int i = 0; i < items.Length; i++)
            {
                var display = new GameObject("Exhibit " + items[i]);
                display.transform.SetParent(section, false);
                display.transform.localPosition = new Vector3((i % Columns - 3.5f) * ColumnSpacing, 0,
                    -(i / Columns) * RowSpacing);
                Box(display.transform, "Plinth", new Vector3(0, .1f, 0), new Vector3(2.35f, .2f, 2), pedestal);
                Box(display.transform, "Front accent", new Vector3(0, .18f, 1.01f), new Vector3(2.35f, .045f, .025f), trim);
                var pig = (GameObject)PrefabUtility.InstantiatePrefab(avatar, display.transform);
                pig.name = "Pig wearing " + items[i];
                pig.transform.localPosition = Vector3.up * .2f;
                pig.GetComponent<PigMotion>().enabled = false;
                pig.GetComponent<PigAppearance>().Apply(0);
                // The new FBX also exposes ungrouped accessory meshes beside the old option groups.
                foreach (var renderer in pig.GetComponentsInChildren<Renderer>(true))
                    if (!NeutralBodyParts.Contains(renderer.name)) renderer.enabled = false;
                AttachGarment(pig, category.folder, category.type, items[i]);
                Label(display.transform, items[i], new Vector3(0, .44f, 1.1f), .045f, TextAnchor.MiddleCenter);
                total++;
            }
            depth += Mathf.CeilToInt(items.Length / (float)Columns) * RowSpacing + 3;
        }

        Box(root.transform, "Gallery floor", new Vector3(0, -.15f, -(depth - 8) * .5f),
            new Vector3(29, .25f, depth + 7), floor);
        Label(root.transform, "SVINKI / NEW WARDROBE", new Vector3(0, 6, 0), .1f, TextAnchor.MiddleCenter);
        Label(root.transform, "71 new garments / 08c0c95\nHold RMB + WASD to fly / Q-E vertical / Shift faster",
            new Vector3(0, 5.1f, 0), .035f, TextAnchor.MiddleCenter);

        var key = new GameObject("Key light", typeof(Light));
        key.transform.SetParent(root.transform, false);
        key.transform.rotation = Quaternion.Euler(45, -25, 0);
        var light = key.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        light.shadows = LightShadows.Soft;
        var fill = new GameObject("Fill light", typeof(Light));
        fill.transform.SetParent(root.transform, false);
        fill.transform.rotation = Quaternion.Euler(35, 155, 0);
        fill.GetComponent<Light>().type = LightType.Directional;
        fill.GetComponent<Light>().intensity = .65f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.65f, .68f, .73f);
        RenderSettings.fog = false;
        RenderSettings.skybox = null;

        var cameraObject = new GameObject("Gallery camera", typeof(Camera), typeof(AudioListener), typeof(ClothingGalleryCamera));
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 6.5f, 13);
        cameraObject.transform.LookAt(new Vector3(0, 1.4f, -3));
        var camera = cameraObject.GetComponent<Camera>();
        camera.fieldOfView = 65;
        camera.nearClipPlane = .05f;
        camera.farClipPlane = 180;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .16f, .21f);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save gallery scene.");
        foreach (string guid in AssetDatabase.FindAssets("", new[] { AssetsFolder }))
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.LookAt(new Vector3(0, 0, -depth * .45f), Quaternion.Euler(55, 180, 0), depth * .65f);
        Debug.Log("[Clothing Gallery] Saved " + total + " dressed pigs to " + ScenePath);
    }

    private static string ModelPath(string category, string item) =>
        SourceFolder + "/" + category + "/" + item + "/" + item + "_worn.fbx";

    private static void AttachGarment(GameObject pig, string category, Type type, string item)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(category, item));
        var clothing = (ClothingDefinition)ScriptableObject.CreateInstance(type);
        try
        {
            var data = new SerializedObject(clothing);
            data.FindProperty("model").objectReferenceValue = model;
            data.FindProperty("displayName").stringValue = item;
            data.FindProperty("pigRigged").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            var holder = new GameObject("Garment " + item);
            if (!PigClothingBinding.TryAttach(clothing, pig.GetComponent<PigAppearance>().ModelRoot, holder.transform))
                throw new InvalidOperationException("Cannot attach garment " + item);

            // Keep the artist's persistent material assets instead of runtime material copies.
            var sourceMaterials = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .ToDictionary(skin => skin.name, skin => skin.sharedMaterials);
            foreach (var skin in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.sharedMaterials = sourceMaterials[skin.name];

            var mask = pig.GetComponentInChildren<PigClothingMask>();
            if (mask != null)
            {
                // The gameplay mask owns a temporary mesh. Persist a copy for scene reloads.
                var body = pig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(skin => skin.name == "Body");
                Mesh mesh = Object.Instantiate(body.sharedMesh);
                Object.DestroyImmediate(mask);
                body.sharedMesh = SaveAsset(mesh, AssetsFolder + "/" + item + " body.asset");
            }
        }
        finally { Object.DestroyImmediate(clothing); }
    }

    private static T SaveAsset<T>(T asset, string path) where T : Object
    {
        asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
        T saved = AssetDatabase.LoadAssetAtPath<T>(path);
        if (saved == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
        EditorUtility.CopySerialized(asset, saved);
        Object.DestroyImmediate(asset);
        EditorUtility.SetDirty(saved);
        return saved;
    }

    private static Material MakeMaterial(string name, Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .2f);
        return SaveAsset(material, AssetsFolder + "/" + name + ".mat");
    }

    private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void Label(Transform parent, string text, Vector3 position, float size, TextAnchor anchor)
    {
        var go = new GameObject(text.Split('\n')[0], typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var label = go.GetComponent<TextMesh>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 48;
        label.characterSize = size;
        label.anchor = anchor;
        label.alignment = TextAlignment.Center;
        label.color = new Color(.97f, .92f, .83f);
        go.GetComponent<Renderer>().sharedMaterial = label.font.material;
    }
}
