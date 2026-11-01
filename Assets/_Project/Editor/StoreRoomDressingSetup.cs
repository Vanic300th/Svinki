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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Additive, deterministic store furnishing. The original layout and pickup slots stay intact.</summary>
public static class StoreRoomDressingSetup
{
    private const string Folder = SvinkiAssetPaths.LevelDesign + "/RoomDressing";
    private const string RootName = "Department furnishings";
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private static readonly Dictionary<string, GameObject> modules = new Dictionary<string, GameObject>();
    private static readonly Collider[] overlaps = new Collider[256];
    private static Scene scene;
    private static Vector3[] clothingSlots;
    private static readonly string[] palette = { "Rose fabric", "Indigo fabric", "Sage fabric", "Oat fabric", "Ochre fabric" };
    [Serializable] public sealed class RoomReport { public string room; public int fixtures, posters; }
    [Serializable] public sealed class Report { public int rooms, fixtures, posters, renderers; public RoomReport[] departments; public string[] failures; }
    private static T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("Svinki/Level/Fill store departments")]
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode required");
        scene = SceneManager.GetSceneByPath(SvinkiAssetPaths.GameplayScene);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(SvinkiAssetPaths.GameplayScene, OpenSceneMode.Additive);
        var previous = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
        try
        {
            SvinkiAssetPaths.EnsureFolder(Folder + "/Materials"); SvinkiAssetPaths.EnsureFolder(Folder + "/Meshes"); SvinkiAssetPaths.EnsureFolder(Folder + "/Prefabs");
            var level = scene.GetRootGameObjects().Single(r => r.name == "Graybox Level");
            if (level.transform.Find(RootName) != null) throw new InvalidOperationException("Furnishings already exist; edit their prefab instances instead of duplicating them.");
            LoadMaterials(); CreateModules();
            var root = new GameObject(RootName).transform; root.SetParent(level.transform, false);
            var slots = new SerializedObject(level.GetComponent<RoundClothingLayout>()).FindProperty("positions");
            clothingSlots = Enumerable.Range(0, slots.arraySize).Select(i => slots.GetArrayElementAtIndex(i).vector3Value).ToArray();
            var floors = All<BoxCollider>().Where(b => b.name == "Floor" && b.bounds.center.y < .3f).OrderBy(b => b.transform.parent.name, StringComparer.Ordinal).ToArray();
            var reports = new List<RoomReport>();
            foreach (var floor in floors)
            {
                var bounds = floor.bounds; var center = bounds.center; center.y = 0;
                int number = int.Parse(floor.transform.parent.name.Substring(0, 2));
                var group = new GameObject(floor.transform.parent.name).transform; group.SetParent(root, false);
                var report = new RoomReport { room = group.name }; reports.Add(report);
                var theme = Theme(number);
                float area = bounds.size.x * bounds.size.z;
                int target = number == 1 ? 3 : area < 160 ? 3 : area < 350 ? 5 : area < 540 ? 7 : 9;
                var candidates = Candidates(bounds, number);
                foreach (var point in candidates)
                {
                    if (report.fixtures >= target) break;
                    string kind = theme[report.fixtures % theme.Length];
                    if (TryPlace(kind, group, bounds, point, center, number + report.fixtures)) report.fixtures++;
                }
                // Fill small remaining pockets with a plant rather than squeezing a broad fixture in.
                if (report.fixtures < target)
                    foreach (var point in candidates)
                    {
                        if (report.fixtures >= target) break;
                        if (TryPlace("Planter", group, bounds, point, center, number)) report.fixtures++;
                    }
                report.posters = PlacePosters(group, bounds, number);
            }
            Physics.SyncTransforms(); Bake();
            var validation = Validate();
            var result = new Report { rooms = reports.Count, fixtures = reports.Sum(r => r.fixtures), posters = reports.Sum(r => r.posters),
                renderers = root.GetComponentsInChildren<Renderer>().Length, departments = reports.ToArray(), failures = validation };
            Directory.CreateDirectory("ArtSource/RoomDressing");
            File.WriteAllText("ArtSource/RoomDressing/validation.json", JsonUtility.ToJson(result, true));
            if (validation.Length > 0) throw new InvalidOperationException("Furniture left unsaved: " + string.Join("; ", validation));
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            return $"Saved {result.fixtures} furniture groups and {result.posters} wall displays across {result.rooms} departments; {result.renderers} added renderers. All room centres and 96 clothing slots remain reachable.";
        }
        finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
    }

    private static string[] Theme(int room)
    {
        switch (room)
        {
            case 1: return new[] { "Planter", "Accessories", "Planter" };
            case 4: case 24: return new[] { "Shoes", "Bench", "Shoes", "Planter" };
            case 7: case 8: case 10: case 28: return new[] { "Stock", "Workbench", "Stock", "Planter" };
            case 13: case 19: case 21: case 22: case 29: return new[] { "Lounge", "Planter", "Bench", "Shelf" };
            case 18: case 27: return new[] { "Workbench", "Rail", "Shelf", "Accessories", "Planter" };
            case 16: return new[] { "Denim shelf", "Rail", "Bench", "Denim shelf", "Planter" };
            case 25: return new[] { "Accessories", "Bench", "Accessories", "Planter" };
            default: return new[] { "Rail", "Shelf", "Planter", "Accessories", "Bench", "Rail" };
        }
    }
    private static List<Vector3> Candidates(Bounds bounds, int seed)
    {
        var list = new List<Vector3>();
        // Edge-first and off-axis: the central cross remains a generous through-route.
        foreach (float inset in new[] { 1.7f, 4.7f, 7.7f })
        {
            for (float z = bounds.min.z + 2.5f; z < bounds.max.z - 2; z += 3.6f)
            {
                list.Add(new Vector3(bounds.min.x + inset, 0, z)); list.Add(new Vector3(bounds.max.x - inset, 0, z));
            }
            for (float x = bounds.min.x + 2.5f; x < bounds.max.x - 2; x += 3.6f)
            {
                list.Add(new Vector3(x, 0, bounds.min.z + inset)); list.Add(new Vector3(x, 0, bounds.max.z - inset));
            }
        }
        return list.Where(p => bounds.Contains(new Vector3(p.x, bounds.center.y, p.z)))
            .OrderBy(p => (Mathf.Sin(p.x * 7.13f + p.z * 3.17f + seed * .71f) + 1) * .8f + Mathf.Min(p.x - bounds.min.x, bounds.max.x - p.x, p.z - bounds.min.z, bounds.max.z - p.z)).ToList();
    }
    private static bool TryPlace(string kind, Transform parent, Bounds room, Vector3 point, Vector3 center, int seed)
    {
        var prefab = modules[kind]; var shape = prefab.GetComponent<BoxCollider>(); var half = shape.size * .5f;
        var inward = center - point;
        float yaw = Mathf.Abs(inward.x) > Mathf.Abs(inward.z) ? inward.x > 0 ? 90 : -90 : inward.z > 0 ? 0 : 180;
        var rotation = Quaternion.Euler(0, yaw, 0);
        var e = rotation * new Vector3(half.x, 0, half.z); e = new Vector3(Mathf.Abs(e.x), half.y, Mathf.Abs(e.z));
        if (point.x - e.x < room.min.x + .35f || point.x + e.x > room.max.x - .35f || point.z - e.z < room.min.z + .35f || point.z + e.z > room.max.z - .35f) return false;
        if (Mathf.Abs(point.x - center.x) < e.x + 1.55f || Mathf.Abs(point.z - center.z) < e.z + 1.55f) return false;
        var inverse = Quaternion.Inverse(rotation);
        foreach (var slot in clothingSlots)
        {
            var local = inverse * (slot - point);
            float dx = Mathf.Max(0, Mathf.Abs(local.x) - half.x), dz = Mathf.Max(0, Mathf.Abs(local.z) - half.z);
            if (dx * dx + dz * dz < 2.6f * 2.6f) return false;
        }
        if (Vector3.Distance(point, new Vector3(0, 0, -3)) < 4) return false;
        int count = scene.GetPhysicsScene().OverlapBox(point + Vector3.up * (half.y + .12f), new Vector3(half.x + .25f, Mathf.Max(.2f, half.y - .06f), half.z + .25f), overlaps, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count > 0) return false;
        if (!NavMesh.SamplePosition(point + rotation * Vector3.forward * (half.z + 1.3f), out var nav, .65f, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(new Vector3(0, 0, -3), nav.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = kind + " " + parent.childCount.ToString("00"); instance.transform.SetPositionAndRotation(point, rotation);
        foreach (var part in instance.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic);
        Physics.SyncTransforms(); return true;
    }
    private static int PlacePosters(Transform parent, Bounds bounds, int seed)
    {
        int count = 0; var center = bounds.center; center.y = 2.9f;
        var directions = new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        foreach (var direction in directions)
        {
            if (count == 2) break;
            float max = Mathf.Abs(direction.x) > .5f ? bounds.size.x * .5f + .5f : bounds.size.z * .5f + .5f;
            foreach (float offset in new[] { -3.4f, 3.4f, 0 })
            {
                var origin = center + Vector3.Cross(Vector3.up, direction) * offset;
                if (!scene.GetPhysicsScene().Raycast(origin, direction, out var hit, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.bounds.size.y < 2.5f || Mathf.Abs(hit.normal.y) > .1f || hit.distance < max - 1.2f) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(modules["Poster"], parent);
                instance.name = "Framed fashion print " + (++count);
                instance.transform.SetPositionAndRotation(hit.point - direction * .07f, Quaternion.LookRotation(-direction)); break;
            }
        }
        return count;
    }

    private static void LoadMaterials()
    {
        materials.Clear();
        foreach (string name in new[] { "Walnut display", "Dark steel", "Cream fixtures", "Peach accent", "Petrol accent" })
            materials[name] = AssetDatabase.LoadAssetAtPath<Material>(SvinkiAssetPaths.SharedMaterials + "/PeachStore/" + name + ".mat");
        string[] names = { "Rose fabric", "Indigo fabric", "Sage fabric", "Oat fabric", "Ochre fabric", "Leaf green", "Light leaf", "Terracotta", "Paper", "Ink" };
        string[] colours = { "B77C8B", "4C6382", "7B907C", "D3CAB7", "BD9560", "42634B", "779C75", "AD745E", "E4DAC6", "293638" };
        for (int i = 0; i < names.Length; i++)
        {
            string path = Folder + "/Materials/" + names[i] + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = names[i], enableInstancing = true };
                ColorUtility.TryParseHtmlString("#" + colours[i], out var colour); mat.SetColor("_BaseColor", colour); mat.SetFloat("_Smoothness", .18f);
                AssetDatabase.CreateAsset(mat, path);
            }
            materials[names[i]] = mat;
        }
    }
    private static void CreateModules()
    {
        modules.Clear();
        Make("Shelf", new Vector3(2.8f, 2.45f, 1.15f), r => Shelf(r, false, false));
        Make("Denim shelf", new Vector3(2.8f, 2.45f, 1.15f), r => Shelf(r, true, false));
        Make("Stock", new Vector3(2.8f, 2.6f, 1.3f), r => Shelf(r, false, true));
        Make("Rail", new Vector3(3.1f, 2.25f, 1.15f), Rail);
        Make("Shoes", new Vector3(2.8f, 2.2f, 1.15f), Shoes);
        Make("Workbench", new Vector3(3, 1.75f, 1.5f), Workbench);
        Make("Accessories", new Vector3(2.8f, 1.65f, 1.3f), Accessories);
        Make("Bench", new Vector3(2.8f, 1.15f, 1.2f), Bench);
        Make("Lounge", new Vector3(3.4f, 1.5f, 2.9f), Lounge);
        Make("Planter", new Vector3(.85f, 1.85f, .85f), Plant);
        Make("Poster", Vector3.zero, Poster);
    }
    private static void Make(string name, Vector3 size, Action<Transform> create)
    {
        string path = Folder + "/Prefabs/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) { modules[name] = existing; return; }
        var root = new GameObject(name);
        try
        {
            create(root.transform); Combine(root.transform, name);
            if (size != Vector3.zero) { var box = root.AddComponent<BoxCollider>(); box.size = size; box.center = Vector3.up * size.y * .5f; }
            foreach (var part in root.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic);
            modules[name] = PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static GameObject Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 size, string material, Vector3 angles = default)
    {
        var go = GameObject.CreatePrimitive(type); go.name = material; go.transform.SetParent(parent, false); go.transform.localPosition = position;
        go.transform.localScale = size; go.transform.localRotation = Quaternion.Euler(angles);
        Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = materials[material]; return go;
    }
    private static void Box(Transform r, Vector3 p, Vector3 s, string m, Vector3 a = default) => Part(PrimitiveType.Cube, r, p, s, m, a);
    private static void Beam(Transform r, Vector3 a, Vector3 b, float width, string material)
    {
        var part = Part(PrimitiveType.Cube, r, (a + b) * .5f, new Vector3(width, Vector3.Distance(a, b), width), material);
        part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
    }
    private static void Fold(Transform r, Vector3 p, int colour)
    {
        for (int i = 0; i < 3; i++) Box(r, p + new Vector3((i - 1) * .018f, .06f + i * .09f, 0), new Vector3(.65f, .09f, .62f), palette[(colour + i) % palette.Length]);
        Box(r, p + new Vector3(0, .293f, 0), new Vector3(.015f, .004f, .6f), "Oat fabric");
    }
    private static void Shelf(Transform r, bool denim, bool stock)
    {
        string frame = stock ? "Dark steel" : "Walnut display";
        foreach (float x in new[] { -1.32f, 1.32f }) Box(r, new Vector3(x, 1.2f, 0), new Vector3(.1f, 2.4f, 1.05f), frame);
        Box(r, new Vector3(0, 1.2f, -.51f), new Vector3(2.65f, 2.4f, .08f), frame);
        for (int level = 0; level < 3; level++)
        {
            float y = .25f + level * .75f; Box(r, new Vector3(0, y, 0), new Vector3(2.8f, .1f, 1.1f), frame);
            for (int col = 0; col < 3; col++)
            {
                if (stock) Model("cardboard_box_01", r, new Vector3((col - 1) * .85f, y + .06f, 0), .68f);
                else Fold(r, new Vector3((col - 1) * .85f, y + .06f, 0), denim ? 1 : level + col);
            }
            Box(r, new Vector3(0, y + .03f, .565f), new Vector3(.34f, .11f, .012f), "Paper");
        }
        Box(r, new Vector3(0, 2.43f, 0), new Vector3(2.8f, .1f, 1.1f), frame);
    }
    private static void Rail(Transform r)
    {
        foreach (float x in new[] { -1.45f, 1.45f })
        { Box(r, new Vector3(x, 1.05f, 0), new Vector3(.065f, 2.1f, .065f), "Dark steel"); Box(r, new Vector3(x, .05f, 0), new Vector3(.16f, .1f, 1.05f), "Dark steel"); }
        Box(r, new Vector3(0, 2.08f, 0), new Vector3(3.02f, .07f, .07f), "Dark steel");
        for (int i = 0; i < 6; i++)
        {
            float x = -1.08f + i * .43f; float z = i % 2 == 0 ? -.04f : .04f;
            Beam(r, new Vector3(x, 2.08f, z), new Vector3(x, 1.85f, z), .022f, "Cream fixtures");
            Beam(r, new Vector3(x, 1.85f, z), new Vector3(x - .21f, 1.65f, z), .025f, "Walnut display");
            Beam(r, new Vector3(x, 1.85f, z), new Vector3(x + .21f, 1.65f, z), .025f, "Walnut display");
            Box(r, new Vector3(x, 1.35f, z), new Vector3(.37f, .56f, .075f), palette[i % 5]);
            Box(r, new Vector3(x - .24f, 1.56f, z), new Vector3(.19f, .28f, .075f), palette[i % 5], new Vector3(0, 0, -42));
            Box(r, new Vector3(x + .24f, 1.56f, z), new Vector3(.19f, .28f, .075f), palette[i % 5], new Vector3(0, 0, 42));
            Box(r, new Vector3(x, 1.65f, z + .04f), new Vector3(.13f, .025f, .014f), "Oat fabric");
        }
    }
    private static void Shoes(Transform r)
    {
        foreach (float y in new[] { .45f, 1.25f, 2.05f }) Box(r, new Vector3(0, y, 0), new Vector3(2.8f, .09f, 1.1f), "Cream fixtures");
        foreach (float x in new[] { -1.3f, 1.3f }) Box(r, new Vector3(x, 1.1f, -.43f), new Vector3(.09f, 2.2f, .09f), "Dark steel");
        for (int row = 0; row < 2; row++) for (int col = 0; col < 3; col++) foreach (float side in new[] { -.14f, .14f })
        {
            var p = new Vector3((col - 1) * .85f + side, .52f + row * .8f, .12f);
            Box(r, p, new Vector3(.22f, .055f, .48f), "Paper");
            Part(PrimitiveType.Sphere, r, p + Vector3.up * .09f, new Vector3(.215f, .15f, .46f), palette[(row + col) % 5]);
            Box(r, p + new Vector3(0, .17f, -.12f), new Vector3(.18f, .2f, .2f), palette[(row + col) % 5]);
            for (int lace = 0; lace < 3; lace++) Box(r, p + new Vector3(0, .165f, .02f + lace * .05f), new Vector3(.15f, .013f, .017f), "Paper");
        }
    }
    private static void Workbench(Transform r)
    {
        Box(r, new Vector3(0, 1.04f, 0), new Vector3(3, .13f, 1.45f), "Walnut display");
        foreach (float x in new[] { -1.2f, 1.2f }) Box(r, new Vector3(x, .5f, 0), new Vector3(.16f, 1, 1.25f), "Dark steel");
        Box(r, new Vector3(-.65f, 1.16f, 0), new Vector3(.75f, .1f, .43f), "Cream fixtures");
        Box(r, new Vector3(-.85f, 1.43f, 0), new Vector3(.2f, .5f, .3f), "Cream fixtures");
        Box(r, new Vector3(-.62f, 1.64f, 0), new Vector3(.65f, .17f, .3f), "Cream fixtures");
        Box(r, new Vector3(-.35f, 1.42f, 0), new Vector3(.035f, .32f, .035f), "Dark steel");
        Fold(r, new Vector3(.73f, 1.115f, .05f), 2);
        for (int i = 0; i < 3; i++) Part(PrimitiveType.Cylinder, r, new Vector3(-.3f + i * .25f, 1.23f, -.48f), new Vector3(.14f, .12f, .14f), palette[i]);
        Box(r, new Vector3(.05f, 1.12f, .5f), new Vector3(1.4f, .015f, .08f), "Ochre fabric");
    }
    private static void Accessories(Transform r)
    {
        Box(r, new Vector3(0, .48f, 0), new Vector3(2.7f, .96f, 1.2f), "Walnut display");
        Box(r, new Vector3(0, 1.01f, 0), new Vector3(2.8f, .1f, 1.3f), "Cream fixtures");
        for (int i = 0; i < 3; i++)
        {
            float x = -.9f + i * .85f;
            Box(r, new Vector3(x, 1.23f, .05f), new Vector3(.5f, .34f, .23f), palette[i]);
            Beam(r, new Vector3(x - .13f, 1.38f, .05f), new Vector3(x - .13f, 1.57f, .05f), .035f, "Dark steel");
            Beam(r, new Vector3(x + .13f, 1.38f, .05f), new Vector3(x + .13f, 1.57f, .05f), .035f, "Dark steel");
            Beam(r, new Vector3(x - .13f, 1.57f, .05f), new Vector3(x + .13f, 1.57f, .05f), .035f, "Dark steel");
            Box(r, new Vector3(x, 1.25f, .173f), new Vector3(.1f, .06f, .012f), "Ochre fabric");
        }
    }
    private static void Bench(Transform r)
    {
        Box(r, new Vector3(0, .47f, 0), new Vector3(2.8f, .16f, 1.1f), "Walnut display");
        foreach (float x in new[] { -1.05f, 1.05f }) Box(r, new Vector3(x, .22f, 0), new Vector3(.15f, .44f, .85f), "Dark steel");
        Box(r, new Vector3(-.35f, .62f, 0), new Vector3(1.8f, .2f, 1.02f), "Sage fabric"); Fold(r, new Vector3(.95f, .57f, 0), 1);
    }
    private static void Lounge(Transform r)
    {
        Model("sofa_02", r, new Vector3(0, 0, -.7f), 2.65f);
        Model("industrial_coffee_table", r, new Vector3(0, 0, .82f), 1.15f);
        for (int i = 0; i < 2; i++) Box(r, new Vector3(-.2f + i * .1f, .96f + i * .025f, .8f), new Vector3(.38f, .02f, .48f), i == 0 ? "Rose fabric" : "Paper", new Vector3(0, i * 17, 0));
        Part(PrimitiveType.Cylinder, r, new Vector3(.3f, 1.06f, .78f), new Vector3(.13f, .1f, .13f), "Oat fabric");
    }
    private static void Plant(Transform r)
    {
        Part(PrimitiveType.Cylinder, r, new Vector3(0, .3f, 0), new Vector3(.7f, .3f, .7f), "Terracotta");
        Part(PrimitiveType.Cylinder, r, new Vector3(0, .605f, 0), new Vector3(.62f, .012f, .62f), "Ink");
        Beam(r, new Vector3(0, .5f, 0), new Vector3(.02f, 1.55f, 0), .045f, "Leaf green");
        for (int i = 0; i < 8; i++)
        {
            float a = i * 2.399f; var p = new Vector3(Mathf.Cos(a) * .2f, .9f + i * .105f, Mathf.Sin(a) * .2f);
            Part(PrimitiveType.Sphere, r, p, new Vector3(.17f, .48f, .09f), i % 2 == 0 ? "Leaf green" : "Light leaf", new Vector3(15, a * Mathf.Rad2Deg, 35));
        }
    }
    private static void Poster(Transform r)
    {
        Box(r, Vector3.zero, new Vector3(1.65f, 2.15f, .08f), "Dark steel");
        Box(r, new Vector3(0, 0, .047f), new Vector3(1.5f, 2, .015f), "Oat fabric");
        Part(PrimitiveType.Sphere, r, new Vector3(.12f, .2f, .065f), new Vector3(1.1f, 1.1f, .012f), "Rose fabric");
        Box(r, new Vector3(0, .05f, .084f), new Vector3(.5f, .85f, .012f), "Indigo fabric");
        Box(r, new Vector3(-.3f, .35f, .084f), new Vector3(.28f, .38f, .012f), "Indigo fabric", new Vector3(0, 0, -40));
        Box(r, new Vector3(.3f, .35f, .084f), new Vector3(.28f, .38f, .012f), "Indigo fabric", new Vector3(0, 0, 40));
        Box(r, new Vector3(0, -.73f, .07f), new Vector3(.95f, .055f, .012f), "Ink");
        Box(r, new Vector3(0, -.85f, .07f), new Vector3(.6f, .028f, .012f), "Ink");
    }
    private static void Model(string name, Transform parent, Vector3 position, float width)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SvinkiAssetPaths.IndustrialArt + "/Prefabs/" + name + ".prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); go.transform.localRotation = Quaternion.identity;
        var renderers = go.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        go.transform.localScale *= width / bounds.size.x;
        bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        go.transform.position += parent.TransformPoint(position) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }
    private static void Combine(Transform root, string moduleName)
    {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        var source = root.GetComponentsInChildren<MeshFilter>();
        foreach (var filter in source)
        {
            var renderer = filter.GetComponent<MeshRenderer>(); if (renderer == null || filter.sharedMesh == null) continue;
            for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
            {
                var mat = renderer.sharedMaterials[Mathf.Min(sub, renderer.sharedMaterials.Length - 1)];
                if (!groups.TryGetValue(mat, out var list)) groups[mat] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
        }
        foreach (var pair in groups)
        {
            var mesh = new Mesh { name = moduleName + " - " + pair.Key.name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(pair.Value.ToArray(), true, true); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "/Meshes/" + moduleName + " - " + pair.Key.name.Replace('/', '-') + ".asset");
        }
        foreach (Transform child in root.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        foreach (var pair in groups)
        {
            var go = new GameObject(pair.Key.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Meshes/" + moduleName + " - " + pair.Key.name.Replace('/', '-') + ".asset");
            go.GetComponent<MeshRenderer>().sharedMaterial = pair.Key;
        }
    }

    public static void Bake()
    {
        scene = SceneManager.GetSceneByPath(SvinkiAssetPaths.GameplayScene);
        var surface = All<NavMeshSurface>().Single(); surface.BuildNavMesh();
        var fresh = surface.navMeshData; var saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(SvinkiAssetPaths.Settings + "/StoreExpandedNavMesh.asset");
        if (saved != null && saved != fresh)
        {
            surface.RemoveData(); EditorUtility.CopySerialized(fresh, saved); Object.DestroyImmediate(fresh);
            surface.navMeshData = saved; surface.AddData(); EditorUtility.SetDirty(saved);
        }
    }
    public static string[] Validate()
    {
        scene = SceneManager.GetSceneByPath(SvinkiAssetPaths.GameplayScene); Physics.SyncTransforms();
        var failures = new List<string>();
        foreach (var floor in All<BoxCollider>().Where(b => b.name == "Floor" && b.bounds.center.y < .3f))
        {
            var point = floor.bounds.center; point.y = 0;
            if (!Connected(point, 2.8f)) failures.Add("Room route: " + floor.transform.parent.name);
        }
        foreach (var item in All<ClothingPickup>()) if (!CanReach(item)) failures.Add("Pickup approach: " + item.name);
        return failures.ToArray();
    }
    private static bool Connected(Vector3 point, float radius)
    {
        if (!NavMesh.SamplePosition(point, out var nav, radius, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath(); return NavMesh.CalculatePath(new Vector3(0, 0, -3), nav.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    private static bool CanReach(ClothingPickup item)
    {
        var collider = item.GetComponent<BoxCollider>(); var target = collider.bounds.center;
        for (int direction = 0; direction < 24; direction++) foreach (float distance in new[] { 1.3f, 1.9f, 2.6f })
        {
            float angle = direction * Mathf.PI / 12;
            var point = item.transform.position + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
            if (!NavMesh.SamplePosition(point, out var nav, .7f, NavMesh.AllAreas) || !Connected(nav.position, .1f)) continue;
            foreach (float height in new[] { 1.55f, .72f })
            {
                var eye = nav.position + Vector3.up * height; var delta = target - eye;
                if (delta.magnitude <= 3.5f && scene.GetPhysicsScene().Raycast(eye, delta.normalized, out var hit, delta.magnitude + .03f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide) && hit.collider == collider) return true;
            }
        }
        return false;
    }
}
