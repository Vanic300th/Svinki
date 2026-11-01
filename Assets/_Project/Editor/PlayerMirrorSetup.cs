using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerMirrorSetup
{
    private const string PrefabPath = SvinkiAssetPaths.Prefabs + "/PlayerMirror.prefab";
    private const string SurfaceMaterialPath = SvinkiAssetPaths.SharedMaterials + "/Player Mirror Surface.mat";
    private const string FrameMaterialPath = SvinkiAssetPaths.SharedMaterials + "/Player Mirror Frame.mat";
    private const string ScenePath = SvinkiAssetPaths.GameplayScene;

    [MenuItem("Svinki/Зеркало/Поставить зеркало у старта")]
    public static void PlaceAtSpawn()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject prefab = EnsurePrefab();
        if (prefab == null) return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Player Mirror") return;

            GameObject mirror = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            mirror.name = "Player Mirror";
            mirror.transform.SetPositionAndRotation(
                new Vector3(-2.7f, 1.4f, -2.2f),
                Quaternion.LookRotation(new Vector3(2.7f, 0f, -0.8f), Vector3.up));

            Camera main = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.CompareTag("MainCamera")) main = camera;
            if (main != null)
            {
                SerializedObject data = new SerializedObject(mirror.GetComponent<PlanarMirror>());
                data.FindProperty("sourceCamera").objectReferenceValue = main;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Mirror] Рабочее зеркало установлено рядом со стартом в SampleScene.");
        }
        finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    private static GameObject EnsurePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        Shader mirrorShader = Shader.Find("Svinki/Planar Mirror");
        Shader frameShader = Shader.Find("Universal Render Pipeline/Lit");
        if (mirrorShader == null || frameShader == null)
        {
            Debug.LogError("Не найден шейдер зеркала или URP/Lit.");
            return null;
        }

        Material mirrorMaterial = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMaterialPath);
        if (mirrorMaterial == null)
        {
            mirrorMaterial = new Material(mirrorShader) { name = "Player Mirror Surface" };
            AssetDatabase.CreateAsset(mirrorMaterial, SurfaceMaterialPath);
        }
        Material frameMaterial = AssetDatabase.LoadAssetAtPath<Material>(FrameMaterialPath);
        if (frameMaterial == null)
        {
            frameMaterial = new Material(frameShader) { name = "Player Mirror Frame" };
            frameMaterial.SetColor("_BaseColor", new Color(.075f, .09f, .12f));
            AssetDatabase.CreateAsset(frameMaterial, FrameMaterialPath);
        }

        mirrorMaterial.SetColor("_Tint", new Color(.99f, .99f, .99f, 1f));
        EditorUtility.SetDirty(mirrorMaterial);

        int mirrorLayer = LayerMask.NameToLayer("MirrorSurface");
        GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Player Mirror");
        try
        {
            PlanarMirror controller = root.GetComponent<PlanarMirror>();
            if (controller == null) controller = root.AddComponent<PlanarMirror>();
            Renderer surface = AddPart(root.transform, PrimitiveType.Quad, "Mirror Glass",
                new Vector3(0f, 0f, .075f), new Vector3(2.4f, 2.6f, 1f), mirrorMaterial, mirrorLayer, false);
            AddPart(root.transform, PrimitiveType.Cube, "Solid Back",
                new Vector3(0f, 0f, -.045f), new Vector3(2.56f, 2.76f, .12f), frameMaterial, mirrorLayer, true);
            AddPart(root.transform, PrimitiveType.Cube, "Left Frame",
                new Vector3(-1.25f, 0f, .035f), new Vector3(.1f, 2.8f, .19f), frameMaterial, mirrorLayer, false);
            AddPart(root.transform, PrimitiveType.Cube, "Right Frame",
                new Vector3(1.25f, 0f, .035f), new Vector3(.1f, 2.8f, .19f), frameMaterial, mirrorLayer, false);
            AddPart(root.transform, PrimitiveType.Cube, "Top Frame",
                new Vector3(0f, 1.35f, .035f), new Vector3(2.6f, .1f, .19f), frameMaterial, mirrorLayer, false);
            AddPart(root.transform, PrimitiveType.Cube, "Bottom Frame",
                new Vector3(0f, -1.35f, .035f), new Vector3(2.6f, .1f, .19f), frameMaterial, mirrorLayer, false);
            AddPart(root.transform, PrimitiveType.Cube, "Mirror Foot",
                new Vector3(0f, -1.35f, -.1f), new Vector3(2.4f, .1f, .5f), frameMaterial, mirrorLayer, true);

            ConfigurePhotoFixtures(root.transform, frameShader, mirrorLayer);
            SerializedObject data = new SerializedObject(controller);
            data.FindProperty("mirrorSurface").objectReferenceValue = surface;
            data.FindProperty("resolutionScale").floatValue = .85f;
            data.FindProperty("maximumResolution").intValue = 1536;
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { if (existing != null) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }

    [MenuItem("Svinki/Зеркало/Улучшить место для фотографий")]
    public static void UpgradePhotoCorner()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsurePrefab();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool loaded = scene.IsValid() && scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var mirror in root.GetComponentsInChildren<PlanarMirror>(true))
            {
                var position = mirror.transform.position;
                // The dressing-room mirror is placed between the two existing stall walls.
                if (position.x < -25f) { position.z = 19.35f; mirror.transform.position = position; }
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Sightline screen" && Vector3.Distance(t.position, mirror.transform.position) < 4f)
                    {
                        t.position += mirror.transform.forward * 2.5f;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    }
                Transform oldLight = mirror.transform.Find("Mirror warm light");
                if (oldLight != null) Object.DestroyImmediate(oldLight.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mirror.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
    }

    private static Material PhotoMaterial(string name, Shader shader, Color color, bool emission = false)
    {
        string path = SvinkiAssetPaths.SharedMaterials + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color);
        if (emission) { material.SetColor("_EmissionColor", color * 2f); material.EnableKeyword("_EMISSION"); }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigurePhotoFixtures(Transform root, Shader shader, int layer)
    {
        var lamp = PhotoMaterial("Player Mirror Light", shader, new Color(1f, .93f, .87f), true);
        var mark = PhotoMaterial("Player Mirror Photo Mark", shader, new Color(.75f, .43f, .52f));
        for (int side = -1; side <= 1; side += 2)
        {
            string name = side < 0 ? "Left softbox" : "Right softbox";
            AddPart(root, PrimitiveType.Cube, name, new Vector3(side * 1.25f, .05f, .15f),
                new Vector3(.045f, 2.35f, .035f), lamp, layer, false);
            Transform t = root.Find(name + " fill");
            if (t == null) { t = new GameObject(name + " fill").transform; t.SetParent(root, false); }
            t.localPosition = new Vector3(side * .9f, .45f, .7f);
            var light = t.GetComponent<Light>();
            if (light == null) light = t.gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = new Color(1f, .94f, .9f);
            light.intensity = 5f; light.range = 3.5f; light.shadows = LightShadows.None;
        }
        for (int corner = 0; corner < 4; corner++)
        {
            float x = corner < 2 ? -.36f : .36f;
            float z = corner % 2 == 0 ? 1.35f : 2.05f;
            AddPart(root, PrimitiveType.Cube, "Photo mark " + corner + " across",
                new Vector3(x, -1.385f, z), new Vector3(.18f, .008f, .025f), mark, -1, false);
            AddPart(root, PrimitiveType.Cube, "Photo mark " + corner + " along",
                new Vector3(x + (x < 0 ? -.08f : .08f), -1.385f, z + (corner % 2 == 0 ? .07f : -.07f)),
                new Vector3(.025f, .008f, .16f), mark, -1, false);
        }
    }

    private static Renderer AddPart(Transform parent, PrimitiveType type, string name,
        Vector3 position, Vector3 scale, Material material, int layer, bool keepCollider)
    {
        Transform previous = parent.Find(name);
        GameObject part = previous != null ? previous.gameObject : GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        if (layer >= 0) part.layer = layer;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        if (!keepCollider && collider != null) Object.DestroyImmediate(collider);
        return renderer;
    }
}
