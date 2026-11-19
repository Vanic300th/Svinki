using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run through Pipeline run_script. The authoring tool stays outside Assets.
public static class GrandpaHouseLayout
{
    private const string ScenePath = "Assets/_Project/Scenes/Gameplay/GrandpasHouse.unity";
    private const string ArtPath = "Assets/_Project/Content/Environment/GrandpasHouse";
    private const string Output = "output/grandpa-house-layout";
    private static readonly Dictionary<string, Material> Palette = new Dictionary<string, Material>();
    private static Transform ground, upper, anchors;
    private static Scene house;

    [Serializable] public class RoomInfo
    {
        public string name;
        public int floor;
        public float x, z, width, depth;
        public string purpose;
    }

    [Serializable] public class PointInfo
    {
        public string name;
        public Vector3 position;
    }

    private static readonly List<RoomInfo> Rooms = new List<RoomInfo>();

    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before changing the layout.");
        house = SceneManager.GetSceneByPath(ScenePath);
        if (!house.IsValid()) house = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (house.isDirty) throw new InvalidOperationException("The house has unsaved changes.");
        Directory.CreateDirectory(Output);
        string backup = Output + "/BeforeLayout.unity";
        if (!File.Exists(backup)) File.Copy(ScenePath, backup);
        SceneManager.SetActiveScene(house);
        Transform root = house.GetRootGameObjects().Single(g => g.name.Contains("house blockout")).transform;
        ground = root.Find("Ground floor");
        upper = root.Find("Upper floor");
        anchors = root.Find("Gameplay anchors - future mechanics");
        if (ground == null || upper == null || anchors == null)
            throw new InvalidOperationException("Expected the existing two-floor house and gameplay anchors.");

        foreach (Transform floor in new[] { ground, upper })
        {
            foreach (Transform child in floor.Cast<Transform>().ToArray())
            {
                Vector3 p = child.localPosition;
                bool perimeter = child.name == "Wall" &&
                    (Mathf.Abs(Mathf.Abs(p.x) - 10) < .05f || Mathf.Abs(p.z + 6) < .05f || Mathf.Abs(p.z - 18) < .05f);
                bool structure = child.name == "Ground slab" || child.name == "Floor panel" || child.name == "Roof";
                bool light = child.name == "Ceiling fixture" || child.name == "Warm room light";
                if (!perimeter && !structure && !light) Object.DestroyImmediate(child.gameObject);
            }
        }
        Palette.Clear();
        Rooms.Clear();
        GroundRooms();
        UpperRooms();
        GroundWalls();
        UpperWalls();
        Windows();
        PlaceStairLights();
        FoodAndRouteAnchors();
        UpdateMap(root);
        Physics.SyncTransforms();

        BakeNavigation(root);
        var food = root.Find("Food locations");
        var routes = root.Find("Walkthrough points");
        var layout = new
        {
            rooms = Rooms.ToArray(),
            food = PointData(food).Select(p => new { p.name, position = new[] { p.position.x, p.position.y, p.position.z } }).ToArray(),
            routes = PointData(routes).Select(p => new { p.name, position = new[] { p.position.x, p.position.y, p.position.z } }).ToArray()
        };
        // Unity's serializer ignores ephemeral Pipeline assemblies. Use the project's JSON assembly by reflection.
        Type json = System.Reflection.Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert");
        string layoutJson = (string)json.GetMethod("SerializeObject", new[] { typeof(object) }).Invoke(null, new object[] { layout });
        File.WriteAllText(Output + "/layout.json", layoutJson);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(house);
        EditorSceneManager.SaveScene(house);
        Selection.activeGameObject = root.gameObject;
        SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0, 2, 6), new Vector3(22, 10, 26)), false);
        return "Built 11 distinct rooms, preserved both staircases and existing spawn transforms; baked navigation.";
    }

    private static void BakeNavigation(Transform root)
    {
        var surface = root.GetComponent<NavMeshSurface>();
        if (surface == null) surface = root.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true;
        surface.voxelSize = .09f;
        surface.BuildNavMesh();
        SvinkiAssetPaths.EnsureFolder(ArtPath + "/Navigation");
        string navPath = ArtPath + "/Navigation/HouseLayout.asset";
        var oldData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
        if (oldData == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
        else
        {
            EditorUtility.CopySerialized(surface.navMeshData, oldData);
            surface.RemoveData();
            surface.navMeshData = oldData;
            surface.AddData();
            EditorUtility.SetDirty(oldData);
        }
        AssetDatabase.SaveAssetIfDirty(surface.navMeshData);
    }

    private static Material Mat(string name, string hex)
    {
        if (Palette.TryGetValue(name, out Material found)) return found;
        SvinkiAssetPaths.EnsureFolder(ArtPath + "/Materials/Layout");
        string path = ArtPath + "/Materials/Layout/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        material.color = color;
        material.SetFloat("_Smoothness", .17f);
        EditorUtility.SetDirty(material);
        Palette.Add(name, material);
        return material;
    }

    private static Material Wood => Mat("Walnut", "70503D");
    private static Material Cream => Mat("Linen", "D8C7A6");
    private static Material Metal => Mat("Iron", "454C4B");
    private static Material Sage => Mat("Sage", "71876B");
    private static Material Blue => Mat("Slate blue", "637C8B");
    private static Material Rust => Mat("Terracotta", "B76E51");

    private static Transform Group(Transform parent, string name)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    private static GameObject Box(Transform parent, string name, Vector3 p, Vector3 size, Material material, bool collider = true)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = p;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(obj.GetComponent<Collider>());
        return obj;
    }

    private static GameObject Round(Transform parent, string name, Vector3 p, Vector3 size, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = p;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        return obj;
    }

    private static Transform Room(Transform floor, string name, float x, float z, float width, float depth, Material finish, string purpose)
    {
        Transform room = Group(floor, name);
        Box(room, "Floor finish", new Vector3(x + width / 2, .008f, z + depth / 2), new Vector3(width - .08f, .016f, depth - .08f), finish, false);
        Rooms.Add(new RoomInfo { name = name, floor = floor == ground ? 0 : 1, x = x, z = z, width = width, depth = depth, purpose = purpose });
        return room;
    }

    // Openings are authored as intervals along a straight wall, in increasing order.
    private static void Wall(Transform floor, bool alongX, float fixedPosition, float from, float to, Material finish, params float[] openings)
    {
        Transform wall = Group(floor, "Room wall");
        float cursor = from;
        for (int i = 0; i <= openings.Length; i += 2)
        {
            float end = i == openings.Length ? to : openings[i];
            if (end > cursor + .01f)
            {
                Vector3 p = alongX ? new Vector3((cursor + end) / 2, 1.98f, fixedPosition) : new Vector3(fixedPosition, 1.98f, (cursor + end) / 2);
                Vector3 size = alongX ? new Vector3(end - cursor, 3.96f, .18f) : new Vector3(.18f, 3.96f, end - cursor);
                Box(wall, "Wall", p, size, finish);
                p.y = .11f;
                size.y = .22f;
                if (alongX) size.z = .23f; else size.x = .23f;
                Box(wall, "Skirting", p, size, Wood, false);
            }
            if (i == openings.Length) break;
            float a = openings[i], b = openings[i + 1];
            Vector3 lintel = alongX ? new Vector3((a + b) / 2, 3.48f, fixedPosition) : new Vector3(fixedPosition, 3.48f, (a + b) / 2);
            Box(wall, "Doorway lintel", lintel, alongX ? new Vector3(b - a, .96f, .18f) : new Vector3(.18f, .96f, b - a), finish);
            foreach (float edge in new[] { a, b })
                Box(wall, "Door frame", alongX ? new Vector3(edge, 1.5f, fixedPosition) : new Vector3(fixedPosition, 1.5f, edge), new Vector3(.09f, 3, .09f), Wood, false);
            cursor = b;
        }
    }

    private static void Table(Transform parent, string name, float x, float z, float width, float depth, float height, Material finish)
    {
        Transform table = Group(parent, name);
        Box(table, "Table top", new Vector3(x, height, z), new Vector3(width, .14f, depth), finish);
        foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
            Box(table, "Table leg", new Vector3(x + sx * (width / 2 - .14f), height / 2, z + sz * (depth / 2 - .14f)), new Vector3(.14f, height, .14f), Wood);
    }

    private static void Chair(Transform parent, float x, float z, float yaw)
    {
        Transform chair = Group(parent, "Chair");
        chair.localPosition = new Vector3(x, 0, z);
        chair.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(chair, "Seat", new Vector3(0, .46f, 0), new Vector3(.65f, .13f, .65f), Wood);
        Box(chair, "Back", new Vector3(0, .85f, .27f), new Vector3(.65f, .85f, .12f), Wood);
        foreach (float sx in new[] { -.24f, .24f }) foreach (float sz in new[] { -.24f, .24f })
            Box(chair, "Leg", new Vector3(sx, .24f, sz), new Vector3(.08f, .48f, .08f), Wood);
    }

    private static void Cabinet(Transform parent, string name, float x, float z, float width, float depth, float height, Material finish)
    {
        Transform cabinet = Group(parent, name);
        Box(cabinet, "Cabinet", new Vector3(x, height / 2, z), new Vector3(width, height, depth), finish);
        Box(cabinet, "Top", new Vector3(x, height + .06f, z), new Vector3(width + .08f, .12f, depth + .08f), Cream);
        int doors = Mathf.Max(1, Mathf.RoundToInt(width / .8f));
        for (int i = 0; i < doors; i++)
        {
            float px = x - width / 2 + width * (i + .5f) / doors;
            Box(cabinet, "Door panel", new Vector3(px, height * .52f, z - depth / 2 - .015f), new Vector3(width / doors - .06f, height - .13f, .045f), finish, false);
            Box(cabinet, "Handle", new Vector3(px, height * .70f, z - depth / 2 - .065f), new Vector3(.23f, .045f, .065f), Metal, false);
        }
    }

    private static void Shelf(Transform parent, string name, float x, float z, float width, float yaw, bool books)
    {
        Transform shelf = Group(parent, name);
        shelf.localPosition = new Vector3(x, 0, z);
        shelf.localRotation = Quaternion.Euler(0, yaw, 0);
        foreach (float side in new[] { -1f, 1f })
            Box(shelf, "Upright", new Vector3(side * (width / 2 - .06f), 1.12f, 0), new Vector3(.12f, 2.24f, .52f), Wood);
        Box(shelf, "Back panel", new Vector3(0, 1.12f, .24f), new Vector3(width, 2.24f, .08f), Wood);
        for (int level = 0; level < 4; level++)
        {
            float y = .25f + level * .58f;
            Box(shelf, "Shelf", new Vector3(0, y, 0), new Vector3(width, .1f, .57f), Wood);
            int count = books ? Mathf.FloorToInt(width / .19f) - 2 : Mathf.FloorToInt(width / .46f);
            for (int i = 0; i < count; i++)
            {
                float px = -width / 2 + (i + 1) * width / (count + 1);
                Material tint = i % 3 == 0 ? Rust : i % 3 == 1 ? Sage : Blue;
                if (books)
                    Box(shelf, "Book", new Vector3(px, y + .23f, -.07f), new Vector3(.12f + i % 2 * .025f, .28f + (i + level) % 3 * .065f, .30f), tint, false);
                else
                {
                    Round(shelf, "Storage jar", new Vector3(px, y + .19f, -.04f), new Vector3(.23f, .14f, .23f), tint);
                    Round(shelf, "Jar lid", new Vector3(px, y + .34f, -.04f), new Vector3(.25f, .025f, .25f), Cream);
                }
            }
        }
    }

    private static void Plant(Transform parent, float x, float z)
    {
        Round(parent, "Plant pot", new Vector3(x, .28f, z), new Vector3(.62f, .28f, .62f), Rust);
        Box(parent, "Plant stem", new Vector3(x, .85f, z), new Vector3(.09f, .9f, .09f), Wood, false);
        for (int i = 0; i < 5; i++)
        {
            var leaf = Box(parent, "Leaf", new Vector3(x + Mathf.Sin(i * 2.4f) * .20f, .90f + i * .13f, z + Mathf.Cos(i * 2.4f) * .17f), new Vector3(.30f, .12f, .56f), Sage, false);
            leaf.transform.localRotation = Quaternion.Euler(25, i * 137, 15);
        }
    }

    private static void GroundRooms()
    {
        Transform living = Room(ground, "01 Living room", -10, -6, 8, 10.2f, Mat("Living oak", "9A7654"), "Sofa and coffee table create a loop and interrupted sightlines.");
        Box(living, "Rug", new Vector3(-6, .028f, -.2f), new Vector3(4.6f, .025f, 4.4f), Rust, false);
        Box(living, "Sofa base", new Vector3(-8.4f, .43f, -.1f), new Vector3(1.45f, .62f, 3.9f), Sage);
        Box(living, "Sofa back", new Vector3(-9.0f, .92f, -.1f), new Vector3(.32f, 1.35f, 3.9f), Sage);
        foreach (float z in new[] { -2.0f, 1.8f }) Box(living, "Sofa arm", new Vector3(-8.4f, .83f, z), new Vector3(1.45f, .42f, .25f), Sage);
        for (int i = 0; i < 3; i++) Box(living, "Seat cushion", new Vector3(-8.24f, .78f, -1.35f + i * 1.25f), new Vector3(1.16f, .20f, 1.17f), Cream, false);
        Table(living, "Coffee table", -5.7f, -.15f, 1.8f, 2.4f, .56f, Wood);
        Cabinet(living, "Television stand", -5.4f, -5.2f, 2.8f, .8f, .55f, Wood);
        Box(living, "Television", new Vector3(-5.4f, 1.27f, -5.2f), new Vector3(1.7f, 1.05f, .45f), Metal);
        Box(living, "TV screen", new Vector3(-5.4f, 1.29f, -4.96f), new Vector3(1.42f, .80f, .035f), Blue, false);
        Plant(living, -8.8f, -4.8f);

        Transform entry = Room(ground, "02 Entrance", -2, -6, 6.2f, 6.2f, Mat("Entry tile", "AD9E83"), "Six existing player starts, clear access to living and dining rooms.");
        Box(entry, "Entrance runner", new Vector3(1.2f, .025f, -2.8f), new Vector3(1.8f, .025f, 4.3f), Blue, false);
        Cabinet(entry, "Shoe chest", 3.5f, -5.25f, 1.1f, .85f, .52f, Wood);
        Box(entry, "Coat rail", new Vector3(.2f, 1.7f, -5.83f), new Vector3(2.7f, .14f, .12f), Wood, false);
        for (int i = 0; i < 4; i++) Box(entry, "Coat hook", new Vector3(-.7f + i * .6f, 1.64f, -5.68f), new Vector3(.06f, .18f, .19f), Metal, false);

        Transform dining = Room(ground, "03 Dining room", -2, .2f, 6.2f, 10, Mat("Dining wood", "B58B5F"), "Open high-value meal with routes around both long sides of the table.");
        Box(dining, "Dining rug", new Vector3(.9f, .026f, 5.4f), new Vector3(4.1f, .026f, 4.9f), Mat("Wine red", "894C46"), false);
        Table(dining, "Feast table", .9f, 5.4f, 2.35f, 3.5f, .95f, Wood);
        Chair(dining, .9f, 3.07f, 180);
        Chair(dining, .9f, 7.73f, 0);
        Cabinet(dining, "Display cabinet", -1.37f, 9.18f, .8f, .7f, 1.65f, Cream);
        for (int i = 0; i < 3; i++) Round(dining, "Empty plate", new Vector3(.4f, 1.04f, 4.3f + i * 1.05f), new Vector3(.38f, .015f, .38f), Cream);

        Transform kitchen = Room(ground, "04 Kitchen", 4.2f, 6, 5.8f, 12, Mat("Kitchen tile", "B1B7A1"), "Cooking wall, fridge, oven and an island with two approach routes.");
        Box(kitchen, "Kitchen extension floor", new Vector3(3.4f, .008f, 14.1f), new Vector3(1.6f, .016f, 7.8f), Mat("Kitchen tile", "B1B7A1"), false);
        Cabinet(kitchen, "Cooking counter", 6.3f, 17.14f, 5.4f, 1.20f, .92f, Sage);
        Cabinet(kitchen, "Island", 7.0f, 10.0f, 2.4f, 2.05f, .92f, Sage);
        Box(kitchen, "Preparation board", new Vector3(7.0f, 1.08f, 10), new Vector3(.85f, .045f, .6f), Wood, false);
        Box(kitchen, "Fridge", new Vector3(9.08f, 1.23f, 14.6f), new Vector3(1.40f, 2.46f, 1.35f), Cream);
        Box(kitchen, "Fridge seam", new Vector3(9.08f, 1.62f, 13.907f), new Vector3(1.32f, .025f, .035f), Metal, false);
        Box(kitchen, "Fridge handle", new Vector3(8.61f, 1.15f, 13.84f), new Vector3(.07f, .54f, .11f), Metal, false);
        Transform oven = Group(kitchen, "Grandpa oven");
        Box(oven, "Oven body", new Vector3(8.85f, .82f, 6.95f), new Vector3(1.7f, 1.64f, 1.28f), Mat("Oven enamel", "B9A58D"));
        Box(oven, "Oven frame", new Vector3(8.85f, .72f, 7.61f), new Vector3(1.41f, 1.08f, .08f), Metal, false);
        Box(oven, "Oven window", new Vector3(8.85f, .72f, 7.66f), new Vector3(1.10f, .75f, .025f), Mat("Oven dark", "292622"), false);
        Box(oven, "Oven handle", new Vector3(8.85f, 1.25f, 7.71f), new Vector3(.99f, .09f, .12f), Metal, false);
        foreach (float x in new[] { 5.2f, 6.1f }) foreach (float z in new[] { 16.85f, 17.4f })
            Round(kitchen, "Hob", new Vector3(x, 1.055f, z), new Vector3(.45f, .015f, .45f), Metal);
        Box(kitchen, "Cooker hood", new Vector3(5.7f, 2.35f, 17.3f), new Vector3(2.05f, .25f, .94f), Cream);
        Cabinet(kitchen, "Wall cupboard", 7.6f, 17.54f, 1.75f, .55f, .7f, Sage);
        // Wall cupboards are raised as a complete group so the doors move with them.
        kitchen.Find("Wall cupboard").localPosition = Vector3.up * 1.6f;

        Transform pantry = Room(ground, "05 Pantry", -6.5f, 10.2f, 9.1f, 7.8f, Mat("Pantry tile", "968D72"), "Shelving hides the far doorway; two exits connect to dining and kitchen.");
        Shelf(pantry, "North pantry shelves", -3.6f, 17.46f, 4.1f, 0, false);
        Shelf(pantry, "Pantry island shelves", -3.3f, 13.2f, 3.3f, 0, false);
        Box(pantry, "Flour chest", new Vector3(.9f, .48f, 16.6f), new Vector3(1.5f, .96f, 1.25f), Wood);
        Round(pantry, "Flour container", new Vector3(.9f, 1.17f, 16.6f), new Vector3(.65f, .22f, .65f), Cream);

        Transform utility = Room(ground, "06 Utility room", -6.5f, 4.2f, 4.5f, 6, Mat("Utility tile", "7C9291"), "Secondary route between living room, dining room and pantry.");
        UtilityFurnishings(utility);
    }

    private static void UtilityFurnishings(Transform utility)
    {
        Transform counter = Group(utility, "Laundry counter alcove");
        counter.localPosition = new Vector3(-5.95f, 0, 5.15f);
        counter.localRotation = Quaternion.Euler(0, -90, 0);
        Cabinet(counter, "Laundry counter", 0, 0, 1.12f, .90f, 1, Cream);

        Transform niche = Group(utility, "Laundry niche");
        niche.localPosition = new Vector3(-5.95f, 0, 9.40f);
        niche.localRotation = Quaternion.Euler(0, -90, 0);
        Box(niche, "Washing machine", new Vector3(0, .55f, 0), new Vector3(1.05f, 1.1f, .94f), Cream);
        var washer = Round(niche, "Washer porthole", new Vector3(0, .57f, -.49f), new Vector3(.65f, .045f, .65f), Metal);
        washer.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Box(utility, "Laundry basket", new Vector3(-2.7f, .43f, 4.9f), new Vector3(.8f, .86f, .8f), Wood);
    }

    private static void PlaceStairLights()
    {
        foreach (Transform item in ground)
        {
            bool fixture = item.name == "Ceiling fixture";
            bool light = item.name == "Warm room light";
            if (!fixture && !light) continue;
            Vector3 p = item.localPosition;
            bool stairA = p.x < -6 && p.z > 7.45f && p.z < 15.75f;
            bool stairB = p.x > 6 && p.z > -4.1f && p.z < 4.2f;
            if (!stairA && !stairB) continue;
            float side = stairA ? -1 : 1;
            item.localPosition = new Vector3(side * (fixture ? 9.79f : 9.5f), 4.05f, stairA ? 12 : 0);
            if (fixture) item.localScale = new Vector3(.20f, .32f, .65f);
        }
    }

    public static string FixUtilityAndStairLights()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before saving the fix.");
        house = SceneManager.GetSceneByPath(ScenePath);
        if (!house.IsValid()) house = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (house.isDirty) throw new InvalidOperationException("The house has unsaved changes.");
        Transform root = house.GetRootGameObjects().Single(g => g.name.Contains("house blockout")).transform;
        ground = root.Find("Ground floor");
        upper = root.Find("Upper floor");
        Transform utility = ground.Find("06 Utility room");
        Undo.RegisterFullObjectHierarchyUndo(utility.gameObject, "Place laundry furniture against solid walls");
        string[] furniture = { "Laundry counter", "Washing machine", "Washer porthole", "Laundry basket", "Laundry niche", "Laundry counter alcove" };
        foreach (Transform child in utility.Cast<Transform>().Where(t => furniture.Contains(t.name)).ToArray())
            Object.DestroyImmediate(child.gameObject);
        UtilityFurnishings(utility);
        PlaceStairLights();
        Physics.SyncTransforms();
        BakeNavigation(root);
        EditorSceneManager.MarkSceneDirty(house);
        EditorSceneManager.SaveScene(house);
        return "Moved laundry furniture against solid walls and mounted the two stair lights on outer walls.";
    }

    private static void UpperRooms()
    {
        Transform workshop = Room(upper, "07 Workshop", -10, -6, 9, 9, Mat("Workshop wood", "8F775E"), "Bench and storage, linked to study and upstairs lounge.");
        Table(workshop, "Workbench", -6.3f, -5.16f, 4.8f, 1.05f, .98f, Wood);
        Box(workshop, "Tool board", new Vector3(-6.3f, 1.80f, -5.76f), new Vector3(4.4f, 1.1f, .09f), Sage, false);
        for (int i = 0; i < 6; i++) Box(workshop, "Tool", new Vector3(-8.05f + i * .65f, 1.77f, -5.68f), new Vector3(.12f, .43f + i % 2 * .19f, .09f), Metal, false);
        Shelf(workshop, "Workshop storage", -9.40f, -.7f, 3.3f, 90, false);
        Table(workshop, "Repair table", -5.8f, -.5f, 2.35f, 1.4f, .86f, Wood);
        Box(workshop, "Repair crate", new Vector3(-5.8f, 1.13f, -.5f), new Vector3(.8f, .4f, .6f), Blue);

        Transform study = Room(upper, "08 Grandpa study", -1, -6, 5.2f, 9, Mat("Study wood", "A4825C"), "Grandpa starting area, writing desk and a tempting small snack.");
        Table(study, "Writing desk", 1.2f, -2, 2.7f, 1.25f, .88f, Wood);
        Chair(study, 1.2f, -3.08f, 180);
        Box(study, "Desk blotter", new Vector3(1.2f, .97f, -2), new Vector3(1.1f, .03f, .7f), Sage, false);
        Shelf(study, "Study books", 2.7f, -5.48f, 2.2f, 0, true);
        Box(study, "Study rug", new Vector3(1.3f, .026f, -.9f), new Vector3(3.2f, .025f, 4.3f), Rust, false);

        Transform lounge = Room(upper, "09 Reading lounge", -6.5f, 3, 10.7f, 6.8f, Mat("Upstairs wood", "AE9271"), "Broad cross-connection with books, seating and a tea station.");
        Shelf(lounge, "Reading shelves", -5.85f, 8.68f, 2.9f, 90, true);
        Box(lounge, "Reading rug", new Vector3(-1.1f, .026f, 6.5f), new Vector3(5.0f, .025f, 3.2f), Blue, false);
        Box(lounge, "Reading settee", new Vector3(-1.2f, .47f, 8.52f), new Vector3(3.3f, .72f, 1.1f), Blue);
        Box(lounge, "Settee back", new Vector3(-1.2f, .99f, 8.96f), new Vector3(3.3f, 1.1f, .21f), Blue);
        Table(lounge, "Tea table", -1.1f, 6.3f, 1.6f, 1.2f, .63f, Wood);
        Cabinet(lounge, "Tea station", 3.42f, 8.87f, .9f, .7f, .9f, Sage);
        Plant(lounge, -5.8f, 3.75f);

        Transform bathroom = Room(upper, "10 Bathroom", -6.5f, 9.8f, 5.7f, 4.6f, Mat("Bathroom tile", "95B1AD"), "Two entrances provide a short route around the central partition.");
        Box(bathroom, "Bath base", new Vector3(-5.5f, .38f, 12.45f), new Vector3(1.25f, .76f, 2.4f), Cream);
        Box(bathroom, "Bath interior", new Vector3(-5.5f, .775f, 12.45f), new Vector3(.88f, .035f, 1.99f), Blue, false);
        Cabinet(bathroom, "Washstand", -2.15f, 13.82f, 1.65f, .70f, .84f, Cream);
        Box(bathroom, "Mirror frame", new Vector3(-2.15f, 1.73f, 14.24f), new Vector3(1.3f, 1.1f, .09f), Wood, false);
        Box(bathroom, "Mirror", new Vector3(-2.15f, 1.73f, 14.18f), new Vector3(1.12f, .92f, .02f), Blue, false);

        Transform bedroom = Room(upper, "11 Bedroom", 2.4f, 9.5f, 7.6f, 8.5f, Mat("Bedroom wood", "9B8067"), "Bed and wardrobe interrupt sightlines; two doors connect to the gallery and lounge.");
        Box(bedroom, "Bed base", new Vector3(6.7f, .32f, 15.2f), new Vector3(2.6f, .64f, 3.8f), Wood);
        Box(bedroom, "Mattress", new Vector3(6.7f, .74f, 15.2f), new Vector3(2.57f, .24f, 3.7f), Cream);
        Box(bedroom, "Blanket", new Vector3(6.7f, .9f, 14.7f), new Vector3(2.59f, .10f, 2.55f), Rust, false);
        Box(bedroom, "Headboard", new Vector3(6.7f, 1.1f, 17.12f), new Vector3(2.7f, 1.45f, .17f), Wood);
        foreach (float x in new[] { 6.0f, 7.3f }) Box(bedroom, "Pillow", new Vector3(x, .94f, 16.5f), new Vector3(1.05f, .20f, .63f), Cream, false);
        Cabinet(bedroom, "Bedside cabinet", 8.78f, 16.3f, .75f, .75f, .68f, Wood);
        Cabinet(bedroom, "Wardrobe", 3.25f, 16.5f, 1.15f, .9f, 2.35f, Sage);
        Box(bedroom, "Bedside rug", new Vector3(6.6f, .026f, 11.8f), new Vector3(4.5f, .025f, 2.3f), Blue, false);

        Transform landing = Group(upper, "12 Rear landing");
        Table(landing, "Preserves table", -.1f, 16.88f, 2.2f, 1.05f, .86f, Wood);
        Shelf(landing, "Landing books", -4.6f, 17.45f, 2.4f, 0, true);
        Transform gallery = Group(upper, "13 Gallery");
        Plant(gallery, 9.1f, 8.8f);
        Cabinet(gallery, "Linen chest", 9.37f, 6.1f, .75f, .9f, .64f, Cream);
    }

    private static void GroundWalls()
    {
        Material plaster = Mat("Warm plaster", "CFBD9E");
        Wall(ground, false, -2, -6, 4.2f, plaster, -3.4f, -.8f, 1.2f, 3.6f);
        Wall(ground, true, 4.2f, -10, -2, plaster, -9.35f, -6.65f, -5.75f, -3.35f);
        Wall(ground, false, -6.5f, 4.2f, 18, plaster, 6.0f, 8.6f, 16.0f, 17.9f);
        Wall(ground, false, -2, 4.2f, 10.2f, plaster, 6.35f, 8.75f);
        Wall(ground, true, .2f, -2, 4.2f, plaster, -.3f, 2.2f);
        Wall(ground, true, 10.2f, -6.5f, 4.2f, plaster, -5.6f, -3.3f, -.4f, 2.0f);
        Wall(ground, false, 2.6f, 10.2f, 18, Mat("Pantry plaster", "B6B38F"), 13.0f, 15.4f);
        Wall(ground, false, 4.2f, -6, 10.2f, Mat("Kitchen plaster", "B5C1A8"), 3.4f, 5.8f, 7.0f, 9.4f);
    }

    private static void UpperWalls()
    {
        Material plaster = Mat("Upstairs plaster", "C7C0AD");
        Wall(upper, false, -1, -6, 3, plaster, -3.7f, -1.3f);
        Wall(upper, false, 4.2f, -6, 9.5f, plaster, -5.8f, -3.6f, 5.5f, 8.0f);
        Wall(upper, true, 3, -10, 4.2f, plaster, -5.8f, -3.4f, .1f, 2.6f);
        Wall(upper, false, -6.5f, 3, 18, plaster, 4.5f, 6.7f, 16.0f, 17.9f);
        Wall(upper, true, 9.8f, -6.5f, 2.4f, plaster, -4.9f, -2.5f, .1f, 2.25f);
        Wall(upper, false, -.8f, 9.8f, 14.4f, Mat("Bathroom plaster", "A9C1B9"), 11.0f, 13.3f);
        Wall(upper, true, 14.4f, -6.5f, 2.4f, plaster, -.1f, 2.15f);
        Wall(upper, false, 2.4f, 9.5f, 18, Mat("Bedroom plaster", "C7AF9B"), 11.1f, 13.5f);
        Wall(upper, true, 9.5f, 2.4f, 10, Mat("Bedroom plaster", "C7AF9B"), 6.25f, 8.65f);
    }

    private static void Windows()
    {
        Window(ground, new Vector3(-9.86f, 2.05f, -3.4f), 90);
        Window(ground, new Vector3(9.86f, 2.05f, 11.9f), -90);
        Window(ground, new Vector3(.3f, 2.4f, 17.86f), 180);
        Window(upper, new Vector3(-9.86f, 2.1f, -3.7f), 90);
        Window(upper, new Vector3(6.7f, 2.5f, 17.86f), 180);
    }

    private static void Window(Transform floor, Vector3 position, float yaw)
    {
        Transform window = Group(floor, "Window");
        window.localPosition = position;
        window.localRotation = Quaternion.Euler(0, yaw, 0);
        Material glass = Mat("Window glass", "8FAAB3");
        glass.EnableKeyword("_EMISSION");
        glass.SetColor("_EmissionColor", new Color(.10f, .16f, .18f));
        Box(window, "Glass", Vector3.zero, new Vector3(1.60f, 1.40f, .035f), glass, false);
        foreach (float x in new[] { -.83f, 0, .83f })
            Box(window, "Window frame", new Vector3(x, 0, .04f), new Vector3(.07f, 1.51f, .10f), Cream, false);
        foreach (float y in new[] { -.73f, .73f })
            Box(window, "Window frame", new Vector3(0, y, .04f), new Vector3(1.73f, .07f, .10f), Cream, false);
        Box(window, "Sill", new Vector3(0, -.78f, .13f), new Vector3(1.91f, .12f, .35f), Wood, false);
    }

    private static void Anchor(Transform parent, string name, Vector3 position)
    {
        Transform point = parent.Find(name);
        if (point == null) point = Group(parent, name);
        point.position = position;
    }

    private static void FoodAndRouteAnchors()
    {
        Transform root = ground.parent;
        foreach (string name in new[] { "Food locations", "Walkthrough points" })
        {
            Transform old = root.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        Transform food = Group(root, "Food locations");
        Anchor(food, "DiningCake", new Vector3(.9f, 1.08f, 5.4f));
        Anchor(food, "KitchenPreparation", new Vector3(7, 1.1f, 10));
        Anchor(food, "KitchenHob", new Vector3(5.7f, 1.1f, 17.1f));
        Anchor(food, "PantryIngredients", new Vector3(-3.6f, .95f, 17.2f));
        Anchor(food, "PantryPreserves", new Vector3(-3.3f, .95f, 13.05f));
        Anchor(food, "LivingSnack", new Vector3(-5.7f, .69f, -.15f));
        Anchor(food, "StudySnack", new Vector3(1.2f, 5.21f, -2));
        Anchor(food, "UpstairsTea", new Vector3(-1.1f, 4.96f, 6.3f));
        Anchor(food, "BedroomSnack", new Vector3(8.78f, 5.05f, 16.3f));
        Anchor(food, "LandingPreserves", new Vector3(-.1f, 5.2f, 16.88f));
        Anchor(anchors, "GrandpaSpawn", new Vector3(1.0f, 4.25f, -4.4f));
        Anchor(anchors, "FridgeFoodSpawn", new Vector3(9.08f, 1.1f, 13.85f));
        Anchor(anchors, "UpperFridgeFoodSpawn", new Vector3(3.42f, 5.22f, 8.5f));
        Anchor(anchors, "OvenDropoff", new Vector3(8.85f, .05f, 8.6f));
        Transform routes = Group(root, "Walkthrough points");
        var points = new[]
        {
            new PointInfo { name = "Entry", position = new Vector3(0, 0, -2) },
            new PointInfo { name = "Living", position = new Vector3(-3.4f, 0, .5f) },
            new PointInfo { name = "Utility", position = new Vector3(-4.1f, 0, 6.4f) },
            new PointInfo { name = "DiningWest", position = new Vector3(-1.0f, 0, 5.4f) },
            new PointInfo { name = "DiningEast", position = new Vector3(3.25f, 0, 5.4f) },
            new PointInfo { name = "Pantry", position = new Vector3(.7f, 0, 13.8f) },
            new PointInfo { name = "KitchenWest", position = new Vector3(4.8f, 0, 10) },
            new PointInfo { name = "KitchenEast", position = new Vector3(9.2f, 0, 10) },
            new PointInfo { name = "Fridge", position = new Vector3(8.8f, 0, 12.6f) },
            new PointInfo { name = "Cooking", position = new Vector3(5.7f, 0, 15.8f) },
            new PointInfo { name = "Oven", position = new Vector3(8.85f, 0, 8.6f) },
            new PointInfo { name = "StairA_Bottom", position = new Vector3(-8, 0, 6.5f) },
            new PointInfo { name = "StairA_Top", position = new Vector3(-8, 4.2f, 16.8f) },
            new PointInfo { name = "StairB_Bottom", position = new Vector3(8, 0, 5.2f) },
            new PointInfo { name = "StairB_Top", position = new Vector3(8, 4.2f, -5.0f) },
            new PointInfo { name = "Workshop", position = new Vector3(-3.0f, 4.2f, -.3f) },
            new PointInfo { name = "Study", position = new Vector3(2.8f, 4.2f, .7f) },
            new PointInfo { name = "Reading", position = new Vector3(1.6f, 4.2f, 5.5f) },
            new PointInfo { name = "Bathroom", position = new Vector3(-3.4f, 4.2f, 11.7f) },
            new PointInfo { name = "RearLanding", position = new Vector3(.8f, 4.2f, 15.5f) },
            new PointInfo { name = "Bedroom", position = new Vector3(4.2f, 4.2f, 12.0f) },
            new PointInfo { name = "Gallery", position = new Vector3(7.1f, 4.2f, 7.0f) },
        };
        foreach (PointInfo point in points) Anchor(routes, point.name, point.position);
    }

    private static PointInfo[] PointData(Transform parent) => parent.Cast<Transform>()
        .Select(t => new PointInfo { name = t.name, position = t.position }).ToArray();

    private static void UpdateMap(Transform root)
    {
        StoreMap map = root.GetComponent<StoreMap>();
        if (map == null) return;
        var data = new SerializedObject(map);
        var walls = ground.GetComponentsInChildren<BoxCollider>().Where(c => c.name == "Wall").ToArray();
        Physics.SyncTransforms();
        var entries = data.FindProperty("walls");
        entries.arraySize = walls.Length;
        for (int i = 0; i < walls.Length; i++)
        {
            Bounds b = walls[i].bounds;
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("center").vector2Value = new Vector2(b.center.x, b.center.z);
            entry.FindPropertyRelative("size").vector2Value = new Vector2(b.size.x, b.size.z);
            entry.FindPropertyRelative("angle").floatValue = 0;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    public static string Validate()
    {
        house = SceneManager.GetSceneByPath(ScenePath);
        Transform root = house.GetRootGameObjects().Single(g => g.name.Contains("house blockout")).transform;
        Physics.SyncTransforms();
        Transform routes = root.Find("Walkthrough points");
        var points = routes.Cast<Transform>().Concat(root.Find("Gameplay anchors - future mechanics").Cast<Transform>()
            .Where(t => t.name.StartsWith("PlayerSpawn") || t.name == "GrandpaSpawn")).ToArray();
        var failures = new List<string>();
        var positions = new Dictionary<string, Vector3>();
        foreach (Transform point in points)
        {
            if (!NavMesh.SamplePosition(point.position, out NavMeshHit hit, .45f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - point.position.y) > .4f)
                failures.Add("No walkable surface at " + point.name);
            else positions[point.name] = hit.position;
            Collider[] overlaps = Physics.OverlapCapsule(point.position + Vector3.up * .55f,
                point.position + Vector3.up * 1.55f, .43f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlaps.Length > 0)
                failures.Add("Player-sized capsule blocked at " + point.name + ": " + string.Join(", ", overlaps.Select(c => c.name)));
        }
        if (!positions.TryGetValue("Entry", out Vector3 origin)) throw new Exception("Entry is off NavMesh.");
        foreach (var point in positions)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, point.Value, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                failures.Add("Disconnected route to " + point.Key);
        }
        // Check the usable doorway width, not only its centerline.
        foreach (float x in new[] { -5.0f, -4.45f, -3.9f })
        {
            for (float z = 8.9f; z <= 11.1f; z += .15f)
            {
                Vector3 p = new Vector3(x, 0, z);
                if (Physics.CheckCapsule(p + Vector3.up * .55f, p + Vector3.up * 1.55f, .43f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    failures.Add("Laundry doorway obstructed at " + p);
            }
        }
        // Side lanes catch fixtures that miss a controller walking along the center.
        foreach (float offset in new[] { -.75f, 0, .75f })
        {
            Vector3 lane = Vector3.right * offset;
            CheckStair("A lane " + offset, new Vector3(-8, .08f, 6.5f) + lane, new Vector3(-8, 4.2f, 16.8f) + lane, failures);
            CheckStair("B lane " + offset, new Vector3(8, .08f, 5.2f) + lane, new Vector3(8, 4.2f, -5) + lane, failures);
        }
        string report = "Checked " + points.Length + " room, staircase and spawn points with a player-sized capsule.\n" +
            "NavMesh triangles: " + NavMesh.CalculateTriangulation().indices.Length / 3 + "\n" +
            (failures.Count == 0 ? "PASS: every room connects to the entrance; laundry doorway clear across three lanes; a player CharacterController walked up and down both stairs in center and side lanes." : string.Join("\n", failures));
        File.WriteAllText(Output + "/validation.txt", report);
        if (failures.Count > 0) throw new Exception(report);
        return report;
    }

    private static void CheckStair(string name, Vector3 bottom, Vector3 top, List<string> failures)
    {
        var probe = new GameObject("Temporary stair walkthrough");
        SceneManager.MoveGameObjectToScene(probe, house);
        probe.transform.position = bottom;
        var controller = probe.AddComponent<CharacterController>();
        controller.height = 2;
        controller.radius = .45f;
        controller.center = Vector3.up;
        controller.stepOffset = .3f;
        controller.slopeLimit = 45;
        controller.skinWidth = .08f;
        try
        {
            foreach (Vector3 target in new[] { top, bottom })
            {
                for (int i = 0; i < 500; i++)
                {
                    Vector3 delta = target - probe.transform.position;
                    delta.y = 0;
                    if (delta.magnitude < .10f) break;
                    controller.Move(Vector3.ClampMagnitude(delta, .045f) + Vector3.down * .04f);
                }
                for (int i = 0; i < 25; i++) controller.Move(Vector3.down * .04f);
                if (Vector3.Distance(probe.transform.position, target) > .3f)
                    failures.Add("Stair " + name + " walkthrough blocked at " + probe.transform.position + " towards " + target);
            }
        }
        finally { Object.DestroyImmediate(probe); }
    }

    public static string Capture()
    {
        house = SceneManager.GetSceneByPath(ScenePath);
        Transform root = house.GetRootGameObjects().Single(g => g.name.Contains("house blockout")).transform;
        ground = root.Find("Ground floor"); upper = root.Find("Upper floor");
        var roof = upper.Find("Roof");
        var renderers = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled &&
            (r.name == "Doorway lintel" || r.name == "Ceiling fixture")).ToArray();
        var others = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
            .Where(s => s != house).SelectMany(s => s.GetRootGameObjects()).Where(g => g.activeSelf).ToArray();
        var flashlights = house.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true))
            .Where(l => l.enabled && (l.name.Contains("Flashlight") || l.name.Contains("HUD"))).ToArray();
        bool upperActive = upper.gameObject.activeSelf;
        bool roofActive = roof.gameObject.activeSelf;
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(house);
        try
        {
            foreach (var obj in others) obj.SetActive(false);
            foreach (var light in flashlights) light.enabled = false;
            foreach (var renderer in renderers) renderer.enabled = false;
            roof.gameObject.SetActive(false);
            upper.gameObject.SetActive(false);
            Shot("ground-plan", new Vector3(0, 34, 6), new Vector3(0, 0, 6), 28, 1100, 1250);
            upper.gameObject.SetActive(true);
            Shot("upper-plan", new Vector3(0, 38, 6), new Vector3(0, 4.2f, 6), 28, 1100, 1250);
            foreach (var renderer in renderers) renderer.enabled = true;
            roof.gameObject.SetActive(roofActive);
            Shot("kitchen", new Vector3(4.9f, 1.7f, 12.6f), new Vector3(8.2f, 1.2f, 16.5f), 0, 1440, 900);
            Shot("dining", new Vector3(3.25f, 1.7f, 1.75f), new Vector3(.65f, 1.0f, 7.9f), 0, 1440, 900);
            Shot("living", new Vector3(-2.9f, 1.7f, 2.6f), new Vector3(-7.2f, .9f, -1.5f), 0, 1440, 900);
            Shot("reading", new Vector3(2.9f, 5.9f, 4.1f), new Vector3(-2.7f, 5.0f, 7.9f), 0, 1440, 900);
            Shot("utility-fixed", new Vector3(-4.45f, 1.7f, 6.5f), new Vector3(-4.45f, 1.15f, 10.8f), 0, 1440, 900);
            Shot("stairs-fixed", new Vector3(8, 1.7f, 5.2f), new Vector3(8, 4.7f, -3.7f), 0, 1440, 900);
        }
        finally
        {
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = true;
            foreach (var obj in others) if (obj != null) obj.SetActive(true);
            foreach (var light in flashlights) if (light != null) light.enabled = true;
            upper.gameObject.SetActive(upperActive);
            roof.gameObject.SetActive(roofActive);
            SceneManager.SetActiveScene(previous);
        }
        // Preview visibility changes restore the scene's saved state.
        EditorSceneManager.SaveScene(house);
        return Path.GetFullPath(Output + "/ground-plan.png");
    }

    private static void Shot(string name, Vector3 position, Vector3 targetPoint, float ortho, int width, int height)
    {
        var go = new GameObject("Layout preview camera");
        SceneManager.MoveGameObjectToScene(go, house);
        var camera = go.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = 72;
        camera.nearClipPlane = .06f;
        camera.farClipPlane = 100;
        camera.transform.position = position;
        camera.transform.LookAt(targetPoint, ortho > 0 ? Vector3.forward : Vector3.up);
        camera.orthographic = ortho > 0;
        if (ortho > 0) camera.orthographicSize = ortho / 2;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f, .10f, .12f);
        var additional = go.AddComponent<UniversalAdditionalCameraData>();
        additional.renderPostProcessing = ortho == 0;
        var target = RenderTexture.GetTemporary(width, height, 24);
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(go);
        }
    }
}
