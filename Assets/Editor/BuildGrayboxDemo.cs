using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildGrayboxDemo
{
    [MenuItem("Tools/Graybox/Build Demo Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Stop Play Mode before rebuilding the scene.");
            return;
        }

        const string scenePath = "Assets/Scenes/SampleScene.unity";
        if (EditorSceneManager.GetActiveScene().path != scenePath)
        {
            Debug.LogError("Open SampleScene before building the graybox demo.");
            return;
        }

        DestroyIfPresent("Graybox Level");
        DestroyIfPresent("Player");

        Transform level = new GameObject("Graybox Level").transform;
        Material floorMaterial = GetMaterial("Graybox Floor", new Color(0.53f, 0.56f, 0.59f));
        Material wallMaterial = GetMaterial("Graybox Walls", new Color(0.75f, 0.77f, 0.79f));
        Material blockMaterial = GetMaterial("Graybox Obstacles", new Color(0.38f, 0.43f, 0.48f));
        Material playerMaterial = GetMaterial("Player Cylinder", new Color(0.95f, 0.54f, 0.22f));

        // CreatePrimitive adds a BoxCollider automatically: the floor, walls and blocks are solid.
        Box("Floor", level, new Vector3(0f, -0.25f, 0f), new Vector3(22f, 0.5f, 22f), floorMaterial);
        Box("North Wall", level, new Vector3(0f, 1.5f, 11f), new Vector3(22f, 3f, 0.5f), wallMaterial);
        Box("South Wall", level, new Vector3(0f, 1.5f, -11f), new Vector3(22f, 3f, 0.5f), wallMaterial);
        Box("East Wall", level, new Vector3(11f, 1.5f, 0f), new Vector3(0.5f, 3f, 22f), wallMaterial);
        Box("West Wall", level, new Vector3(-11f, 1.5f, 0f), new Vector3(0.5f, 3f, 22f), wallMaterial);
        Box("Block A", level, new Vector3(-4f, 0.75f, 2f), new Vector3(2f, 1.5f, 2f), blockMaterial);
        Box("Block B", level, new Vector3(4f, 1f, 1f), new Vector3(2f, 2f, 3f), blockMaterial);
        Box("Block C", level, new Vector3(0f, 0.5f, 6f), new Vector3(3f, 1f, 1.5f), blockMaterial);

        GameObject player = new GameObject("Player");
        player.layer = 2; // The first-person camera hides this layer, but Scene view still shows it.
        player.transform.position = new Vector3(0f, 0.05f, -3f);
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.center = new Vector3(0f, 1f, 0f);
        controller.stepOffset = 0.3f;
        GrayboxPlayerController movement = player.AddComponent<GrayboxPlayerController>();

        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "Cylinder Visual";
        cylinder.layer = 2;
        cylinder.transform.SetParent(player.transform, false);
        cylinder.transform.localPosition = new Vector3(0f, 1f, 0f);
        cylinder.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
        cylinder.GetComponent<Renderer>().sharedMaterial = playerMaterial;

        ConfigureCamera(player, movement);

        // The template already has exactly one Directional Light; keep it as the scene's source.
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = player;
        Debug.Log("Graybox demo ready. Press Play: WASD to move, mouse to look, Space to jump, Escape to free the cursor.");
    }

    [MenuItem("Tools/Graybox/Set First Person Camera")]
    public static void SetFirstPersonCamera()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.playModeStateChanged -= ApplyAfterPlayMode;
            EditorApplication.playModeStateChanged += ApplyAfterPlayMode;
            EditorApplication.isPlaying = false;
            return;
        }

        ApplyFirstPersonCamera();
    }

    private static void ApplyAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ApplyAfterPlayMode;
        EditorApplication.delayCall += ApplyFirstPersonCamera;
    }

    private static void ApplyFirstPersonCamera()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null || !player.TryGetComponent(out GrayboxPlayerController movement))
        {
            Debug.LogError("Player with GrayboxPlayerController was not found in the active scene.");
            return;
        }

        ConfigureCamera(player, movement);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("First-person camera is ready.");
    }

    private static void ConfigureCamera(GameObject player, GrayboxPlayerController movement)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("The scene needs a camera tagged MainCamera.");
            return;
        }

        GrayboxFirstPersonCamera look = camera.GetComponent<GrayboxFirstPersonCamera>();
        if (look == null) look = camera.gameObject.AddComponent<GrayboxFirstPersonCamera>();
        look.SetTarget(player.transform);
        movement.SetCamera(camera.transform);
        camera.transform.position = player.transform.position + Vector3.up * 1.65f;
        camera.transform.rotation = Quaternion.identity;
        camera.cullingMask &= ~(1 << player.layer);
    }

    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static Material GetMaterial(string name, Color color)
    {
        const string folder = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Materials");
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void DestroyIfPresent(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) Object.DestroyImmediate(existing);
    }
}
