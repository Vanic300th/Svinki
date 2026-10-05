using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Greybox traced from the supplied floor-plan. 20 drawing pixels = 1 metre.</summary>
public static class BuildSketchGraybox
{
    public const float Height = 3.5f;
    private const float Thickness = 0.25f;
    private const float DoorHeight = 2.5f;
    private static Material wall, floor, corridor, ceiling, fixture, glow;
    private static Transform walls, floors, ceilings, lights;

    [MenuItem("Tools/Graybox/Build Sketch Layout")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/SampleScene.unity")
            throw new System.InvalidOperationException("Open SampleScene in Edit Mode first.");

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build sketch graybox");
        var previous = GameObject.Find("Graybox Level");
        if (previous != null) Undo.DestroyObjectImmediate(previous);
        // This loose primitive was at the spawn point, outside the previous level hierarchy.
        var looseCube = GameObject.Find("Cube");
        if (looseCube != null && looseCube.transform.position == Vector3.zero &&
            looseCube.transform.localScale == Vector3.one && looseCube.GetComponent<BoxCollider>() != null)
            Undo.DestroyObjectImmediate(looseCube);

        wall = Material("Sketch Walls", new Color(.48f, .49f, .5f));
        floor = Material("Sketch Floor", new Color(.29f, .30f, .32f));
        corridor = Material("Sketch Corridor Floor", new Color(.36f, .37f, .38f));
        ceiling = Material("Sketch Ceiling", new Color(.32f, .33f, .35f));
        fixture = Material("Sketch Light Housing", new Color(.12f, .13f, .14f));
        glow = Material("Sketch Light Diffuser", new Color(.85f, .78f, .65f));
        glow.EnableKeyword("_EMISSION");
        glow.SetColor("_EmissionColor", new Color(1f, .83f, .62f) * .65f);
        EditorUtility.SetDirty(glow);

        Transform root = Group("Graybox Level", null);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Create sketch graybox");
        floors = Group("Floors", root);
        walls = Group("Walls — 3.5m", root);
        ceilings = Group("Ceilings", root);
        lights = Group("Dim Ceiling Lights", root);

        Slab("Northwest Room", 52, 68, 370, 308, floor);
        Slab("West Room", 105, 308, 334, 656, floor);
        Slab("South Room", 334, 560, 670, 752, floor);
        Slab("East Room", 526, 164, 910, 560, floor);
        Slab("North Alcove", 574, 44, 646, 164, floor);
        Slab("Corridor North Link", 370, 236, 454, 308, corridor);
        Slab("Central Corridor", 394, 308, 454, 464, corridor);
        Slab("Corridor Elbow", 394, 464, 526, 560, corridor);
        Slab("Corridor West Link", 334, 480, 394, 560, corridor);

        H("NW North", 52, 370, 68);
        V("NW West", 52, 68, 308);
        HDoor("NW to West", 52, 370, 308, 130, 174);
        VDoor("NW to Corridor", 370, 68, 308, 244, 298);
        V("West Outer", 105, 308, 656);
        H("West South", 105, 334, 656);
        VDoor("West to Corridor", 334, 308, 752, 488, 532);
        HDoor("South to Corridor", 334, 910, 560, 394, 438);
        H("South Outer", 334, 670, 752);
        V("South East", 670, 560, 752);
        V("East Outer", 910, 164, 560);
        VDoor("East to Corridor", 526, 164, 560, 484, 532);
        HDoor("East to Alcove", 526, 910, 164, 588, 632);
        V("Alcove West", 574, 44, 164);
        V("Alcove East", 646, 44, 164);
        H("Alcove North", 574, 646, 44);
        H("Corridor North", 370, 454, 236);
        V("Corridor East", 454, 236, 464);
        H("Corridor Elbow North", 454, 526, 464);
        V("Corridor West", 394, 308, 480);
        H("West Link North", 334, 394, 480);
        H("North Link South", 370, 394, 308);

        Lamp("Corridor 01", 424, 284, .65f, 5f);
        Lamp("Corridor 02", 424, 404, .65f, 5f);
        Lamp("Corridor 03", 470, 512, .75f, 5.5f);
        Lamp("Northwest", 210, 184, .9f, 10f);
        Lamp("West", 220, 448, .85f, 10f);
        Lamp("South", 500, 660, .85f, 9f);
        Lamp("East 01", 646, 290, .8f, 10f);
        Lamp("East 02", 800, 454, .8f, 10f);
        Lamp("Alcove", 610, 98, .45f, 4.5f);

        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            Undo.RecordObject(sun, "Disable daylight");
            sun.SetActive(false);
        }
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.045f, .05f, .06f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = .1f;
        RenderSettings.fog = false;

        // Preserve the gameplay setup and place its existing props on the new floor.
        Move("Pickup Red Head", new Vector3(-13f, 1.55f, 16f));
        Move("Pickup Yellow Torso", new Vector3(-12f, 1.55f, 16f));
        Move("Pickup Green Legs", new Vector3(-11f, 1.55f, 16f));
        Move("Pickup Blue Shoes", new Vector3(-10f, 1.55f, 16f));
        Move("Pickup hoodie1", new Vector3(-9f, .85f, 16f));
        Move("White Animated Character", new Vector3(-7f, .1f, 15f));
        Move("Player Mirror", new Vector3(-10f, 1.4f, 12f));
        Move("sneakers2", new Vector3(-12f, .02f, 14f));
        Move("sneakers2 (1)", new Vector3(-11f, .18f, 14f));
        Move("pants1", new Vector3(-10f, .02f, 14f));
        Move("Hat1 (2)", new Vector3(-9f, .34f, 14f));
        Move("Shirt1 (3)", new Vector3(-8f, .02f, 14f));
        Move("Hat1", new Vector3(-7f, .07f, 14f));
        Move("Mannequin_Online", new Vector3(10f, .07f, 10f));
        Move("Mannequin_Watcher_Online", new Vector3(-10f, .07f, 5f));
        Move("ThiefNest", new Vector3(18f, .07f, 4f));
        Move("Mannequin_Thief_Online", new Vector3(17f, .07f, 4f));
        Move("Player", new Vector3(0f, .05f, -3f));
        Move("Main Camera", new Vector3(0f, 1.7f, -3f));
        Camera camera = Camera.main;
        if (camera != null)
        {
            Undo.RecordObject(camera, "Indoor camera");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.015f, .018f, .022f);
            camera.farClipPlane = 80f;
        }

        var nav = GameObject.Find("NavMesh");
        if (nav != null && nav.TryGetComponent(out Unity.AI.Navigation.NavMeshSurface surface))
        {
            Undo.RecordObject(surface, "Use solid graybox geometry for navigation");
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = root.gameObject;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(new Vector3(2.5f, 0f, 7.6f),
                Quaternion.Euler(90f, 0f, 0f), 29f, true);
        Debug.Log("Sketch graybox saved: 3.5m walls, 2.5m doorways, 3m central corridor, 9 dim lights.");
    }

    private static Vector3 Point(float x, float y, float height) =>
        new Vector3((x - 430f) * .05f, height, (550f - y) * .05f);

    private static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void Slab(string name, float x0, float y0, float x1, float y1, Material material)
    {
        var size = new Vector3((x1 - x0) * .05f, .3f, (y1 - y0) * .05f);
        Box(name, floors, Point((x0 + x1) / 2, (y0 + y1) / 2, -.15f), size, material);
        Box(name + " Ceiling", ceilings, Point((x0 + x1) / 2, (y0 + y1) / 2, Height + .15f), size, ceiling);
    }

    private static void H(string name, float a, float b, float y, float height = Height, float bottom = 0)
    {
        Box(name, walls, Point((a + b) / 2, y, bottom + height / 2),
            new Vector3((b - a) * .05f + Thickness, height, Thickness), wall);
    }

    private static void V(string name, float x, float a, float b, float height = Height, float bottom = 0)
    {
        Box(name, walls, Point(x, (a + b) / 2, bottom + height / 2),
            new Vector3(Thickness, height, (b - a) * .05f + Thickness), wall);
    }

    private static void HDoor(string name, float a, float b, float y, float start, float end)
    {
        H(name + " A", a, start, y);
        H(name + " B", end, b, y);
        H(name + " Lintel", start, end, y, Height - DoorHeight, DoorHeight);
    }

    private static void VDoor(string name, float x, float a, float b, float start, float end)
    {
        V(name + " A", x, a, start);
        V(name + " B", x, end, b);
        V(name + " Lintel", x, start, end, Height - DoorHeight, DoorHeight);
    }

    private static void Lamp(string name, float x, float y, float intensity, float range)
    {
        var parent = Group(name, lights);
        Box("Housing", parent, Point(x, y, Height - .08f), new Vector3(.65f, .16f, .35f), fixture);
        Box("Diffuser", parent, Point(x, y, Height - .18f), new Vector3(.52f, .045f, .25f), glow, false);
        var source = new GameObject("Point Light", typeof(Light));
        source.transform.SetParent(parent, false);
        source.transform.localPosition = Point(x, y, Height - .38f);
        var light = source.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, .88f, .73f);
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = .8f;
        light.shadowBias = .03f;
        light.shadowNormalBias = .12f;
        light.lightmapBakeType = LightmapBakeType.Realtime;
    }

    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size,
        Material material, bool solid = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static Material Material(string name, Color color)
    {
        const string folder = "Assets/Materials/SketchGraybox";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Materials", "SketchGraybox");
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .08f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Move(string name, Vector3 position)
    {
        var go = GameObject.Find(name);
        if (go == null) return;
        Undo.RecordObject(go.transform, "Place existing prop in sketch layout");
        go.transform.position = position;
    }
}
