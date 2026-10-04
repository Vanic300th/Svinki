using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerMirrorSetup
{
    private const string PrefabPath = "Assets/Prefabs/PlayerMirror.prefab";
    private const string SurfaceMaterialPath = "Assets/Materials/Player Mirror Surface.mat";
    private const string FrameMaterialPath = "Assets/Materials/Player Mirror Frame.mat";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

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
        if (existing != null) return existing;

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
            mirrorMaterial.SetColor("_Tint", new Color(.92f, .97f, 1f, 1f));
            AssetDatabase.CreateAsset(mirrorMaterial, SurfaceMaterialPath);
        }
        Material frameMaterial = AssetDatabase.LoadAssetAtPath<Material>(FrameMaterialPath);
        if (frameMaterial == null)
        {
            frameMaterial = new Material(frameShader) { name = "Player Mirror Frame" };
            frameMaterial.SetColor("_BaseColor", new Color(.075f, .09f, .12f));
            AssetDatabase.CreateAsset(frameMaterial, FrameMaterialPath);
        }

        int mirrorLayer = LayerMask.NameToLayer("MirrorSurface");
        GameObject root = new GameObject("Player Mirror");
        try
        {
            PlanarMirror controller = root.AddComponent<PlanarMirror>();
            Renderer surface = AddPart(root.transform, PrimitiveType.Quad, "Mirror Glass",
                new Vector3(0f, 0f, .075f), new Vector3(1.8f, 2.35f, 1f), mirrorMaterial, mirrorLayer, false);
            AddPart(root.transform, PrimitiveType.Cube, "Solid Back",
                new Vector3(0f, 0f, -.045f), new Vector3(1.98f, 2.55f, .12f), frameMaterial, mirrorLayer, true);
            AddPart(root.transform, PrimitiveType.Cube, "Left Frame",
                new Vector3(-.98f, 0f, .035f), new Vector3(.12f, 2.62f, .19f), frameMaterial, mirrorLayer, false);
            AddPart(root.transform, PrimitiveType.Cube, "Right Frame",
                new Vector3(.98f, 0f, .035f), new Vector3(.12f, 2.62f, .19f), frameMaterial, mirrorLayer, false);
            AddPart(root.transform, PrimitiveType.Cube, "Top Frame",
                new Vector3(0f, 1.25f, .035f), new Vector3(2.04f, .12f, .19f), frameMaterial, mirrorLayer, false);
            AddPart(root.transform, PrimitiveType.Cube, "Bottom Frame",
                new Vector3(0f, -1.25f, .035f), new Vector3(2.04f, .12f, .19f), frameMaterial, mirrorLayer, false);
            AddPart(root.transform, PrimitiveType.Cube, "Mirror Foot",
                new Vector3(0f, -1.35f, -.1f), new Vector3(1.8f, .1f, .5f), frameMaterial, mirrorLayer, true);

            SerializedObject data = new SerializedObject(controller);
            data.FindProperty("mirrorSurface").objectReferenceValue = surface;
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Renderer AddPart(Transform parent, PrimitiveType type, string name,
        Vector3 position, Vector3 scale, Material material, int layer, bool keepCollider)
    {
        GameObject part = GameObject.CreatePrimitive(type);
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
