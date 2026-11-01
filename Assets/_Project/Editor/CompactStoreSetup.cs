using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Object;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Compact the authored store without scaling actors, then subdivide its largest halls.</summary>
public static class CompactStoreSetup
{
    private const float Scale = .92f;
    private const string Marker = "Compact departments · 42 rooms";
    private static Scene Scene => SceneManager.GetSceneByPath(SvinkiAssetPaths.GameplayScene);
    private static T[] All<T>() where T : Component => Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static Transform Level => Scene.GetRootGameObjects().Single(r => r.name == "Graybox Level").transform;
    private static BoxCollider[] Floors => All<BoxCollider>().Where(b => b.name == "Floor" && b.bounds.center.y < .3f).ToArray();
    private static Vector3 Compact(Vector3 point) => new Vector3(point.x * Scale, point.y, point.z * Scale);
    [Serializable] private sealed class Room { public string name; public Vector3 center, size; }
    [Serializable] private sealed class Layout { public int count; public float area, width, depth; public Room[] rooms; }
    private sealed class Actor { public Transform root; public Vector3 position, scale; }
    private sealed class Division { public BoxCollider floor; public bool x; public float at; public float[] doors; public int conflicts; }

    private static void Export(string suffix)
    {
        var floors = Floors;
        var data = new Layout { count = floors.Length, area = floors.Sum(f => f.bounds.size.x * f.bounds.size.z),
            width = floors.Max(f => f.bounds.max.x) - floors.Min(f => f.bounds.min.x),
            depth = floors.Max(f => f.bounds.max.z) - floors.Min(f => f.bounds.min.z),
            rooms = floors.Select(f => new Room { name = f.transform.parent.name, center = f.bounds.center, size = f.bounds.size }).ToArray() };
        Directory.CreateDirectory("ArtSource/CompactStore");
        File.WriteAllText("ArtSource/CompactStore/" + suffix + ".json", JsonUtility.ToJson(data, true));
    }

    public static string CompactFootprint()
    {
        if (Application.isPlaying || !Scene.IsValid() || !Scene.isLoaded || Scene.isDirty) throw new InvalidOperationException("Open the saved gameplay scene in Edit mode first.");
        if (Level.Cast<Transform>().Any(t => t.name == Marker) || Floors.Length != 30) throw new InvalidOperationException("Store already compacted or unexpected layout.");
        Export("before");
        // Physics and network roots retain their original dimensions. Only the static architecture shrinks.
        var roots = Level.GetComponentsInChildren<NetworkObject>(true).Select(n => n.transform).ToHashSet();
        var actors = roots.Where(t => !t.GetComponentsInParent<NetworkObject>(true).Any(n => n.transform != t && roots.Contains(n.transform)))
            .Select(t => new Actor { root = t, position = t.position, scale = t.lossyScale }).ToArray();
        var topActors = actors.Select(a => a.root).ToHashSet();
        foreach (Transform group in Level)
        {
            group.position = Compact(group.position);
            if (!topActors.Contains(group)) group.localScale = Vector3.Scale(group.localScale, new Vector3(Scale, 1, Scale));
        }
        foreach (var actor in actors)
        {
            var now = actor.root.lossyScale;
            actor.root.localScale = Vector3.Scale(actor.root.localScale, new Vector3(actor.scale.x / now.x, actor.scale.y / now.y, actor.scale.z / now.z));
            actor.root.position = Compact(actor.position);
        }
        var dropOff = Scene.GetRootGameObjects().Single(r => r.name == "Thief drop-off points");
        foreach (Transform point in dropOff.transform) point.position = Compact(point.position);
        var camera = Scene.GetRootGameObjects().Single(r => r.name == "Main Camera"); camera.transform.position = Compact(camera.transform.position);
        var layout = new SerializedObject(Level.GetComponent<RoundClothingLayout>()); var positions = layout.FindProperty("positions");
        for (int i = 0; i < positions.arraySize; i++) positions.GetArrayElementAtIndex(i).vector3Value = Compact(positions.GetArrayElementAtIndex(i).vector3Value);
        layout.ApplyModifiedPropertiesWithoutUndo();
        FixArrivalClearance();
        var marker = new GameObject(Marker); SceneManager.MoveGameObjectToScene(marker, Scene); marker.transform.SetParent(Level, false);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(Scene);
        return "Footprint reduced to 92% on X/Z; all network objects and clothing retain their dimensions. " + actors.Length + " actor roots preserved.";
    }

    public static string FixArrivalClearance()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit mode required.");
        var display = Level.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Hidden spot 00 / 01 ВХОД / ПЕРСИК");
        if (display.position.z > -4) return "Arrival display already clear of the lift.";
        var data = new SerializedObject(Level.GetComponent<RoundClothingLayout>()); var slot = data.FindProperty("positions").GetArrayElementAtIndex(0);
        var previous = slot.vector3Value; var offset = Vector3.forward * 4;
        display.position += offset;
        foreach (var item in All<ClothingPickup>().Where(p => Vector3.Distance(p.transform.position, previous) < .02f)) item.transform.position += offset;
        slot.vector3Value = previous + offset; data.ApplyModifiedPropertiesWithoutUndo();
        Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(Scene);
        return "Arrival cabinet and its garment slot moved 4m clear of the unscaled freight lift.";
    }

    private static bool Ignore(Collider c) => c.isTrigger || c.GetComponentInParent<NetworkObject>() != null;
    private static Division Plan(BoxCollider floor)
    {
        var b = floor.bounds; var candidates = new List<Division>();
        foreach (bool x in new[] { b.size.x >= b.size.z, b.size.x < b.size.z })
        foreach (float fraction in new[] { .5f, .46f, .54f, .42f, .58f })
        foreach (float second in new[] { .22f, .78f })
        {
            float at = x ? Mathf.Lerp(b.min.x, b.max.x, fraction) : Mathf.Lerp(b.min.z, b.max.z, fraction);
            float start = x ? b.min.z : b.min.x, end = x ? b.max.z : b.max.x;
            var doors = new[] { (start + end) * .5f, Mathf.Lerp(start, end, second) }.OrderBy(v => v).ToArray();
            var plan = new Division { floor = floor, x = x, at = at, doors = doors };
            foreach (var segment in Segments(start + .18f, end - .18f, doors))
            {
                var center = x ? new Vector3(at, 1.7f, (segment.x + segment.y) * .5f) : new Vector3((segment.x + segment.y) * .5f, 1.7f, at);
                var half = x ? new Vector3(.18f, 1.55f, (segment.y - segment.x) * .5f) : new Vector3((segment.y - segment.x) * .5f, 1.55f, .18f);
                int count = Scene.GetPhysicsScene().OverlapBox(center, half, hits, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                plan.conflicts += hits.Take(count).Count(c => c != null && !Ignore(c));
            }
            // Never build a wall across a garment, including non-solid pickup colliders.
            foreach (var item in All<ClothingPickup>())
            {
                var p = item.transform.position; float normal = x ? p.x : p.z, along = x ? p.z : p.x;
                if (along > start && along < end && Mathf.Abs(normal - at) < 1.6f && !doors.Any(d => Mathf.Abs(d - along) < 1.25f)) plan.conflicts += 100;
            }
            candidates.Add(plan);
        }
        return candidates.OrderBy(p => p.conflicts).ThenBy(p => Mathf.Abs(p.at - (p.x ? b.center.x : b.center.z))).First();
    }
    private static readonly Collider[] hits = new Collider[256];
    private static IEnumerable<Vector2> Segments(float start, float end, float[] doors)
    {
        float cursor = start;
        foreach (float door in doors)
        { if (door - 1.6f > cursor) yield return new Vector2(cursor, door - 1.6f); cursor = door + 1.6f; }
        if (cursor < end) yield return new Vector2(cursor, end);
    }
    public static string PreviewDivisions()
    {
        return string.Join("\n", SelectedFloors().Select(f => { var p = Plan(f); return f.transform.parent.name + ": " + (p.x ? "X" : "Z") + " @ " + p.at + ", conflicts=" + p.conflicts; }));
    }
    private static BoxCollider[] SelectedFloors() => Floors.Where(f => f.bounds.min.z > 59).OrderByDescending(f => f.bounds.size.x * f.bounds.size.z).Take(12).ToArray();
    public static string AddRooms()
    {
        if (Application.isPlaying || Floors.Length != 30) throw new InvalidOperationException("Expected 30 compacted departments.");
        var plans = SelectedFloors().Select(Plan).ToArray();
        if (plans.Length != 12 || plans.Any(p => p.conflicts != 0)) throw new InvalidOperationException("A partition would overlap furniture: " + PreviewDivisions());
        var root = Level.Cast<Transform>().Single(t => t.name == Marker);
        var plaster = AssetDatabase.LoadAssetAtPath<Material>(SvinkiAssetPaths.SharedMaterials + "/PeachStore/Warm plaster.mat");
        var trim = AssetDatabase.LoadAssetAtPath<Material>(SvinkiAssetPaths.SharedMaterials + "/PeachStore/Petrol accent.mat");
        foreach (var p in plans)
        {
            var b = p.floor.bounds; string name = p.floor.transform.parent.name;
            var group = new GameObject(name + " / two connected rooms").transform; group.SetParent(root, false);
            float start = p.x ? b.min.z : b.min.x, end = p.x ? b.max.z : b.max.x;
            foreach (var segment in Segments(start, end, p.doors))
            {
                var center = p.x ? new Vector3(p.at, 2.45f, (segment.x + segment.y) * .5f) : new Vector3((segment.x + segment.y) * .5f, 2.45f, p.at);
                var size = p.x ? new Vector3(.24f, 4.9f, segment.y - segment.x) : new Vector3(segment.y - segment.x, 4.9f, .24f);
                Box("Partition wall", group, center, size, plaster);
                Box("Partition skirting", group, new Vector3(center.x, .12f, center.z), new Vector3(size.x + .035f, .24f, size.z + .035f), trim);
            }
            foreach (float door in p.doors)
            {
                var center = p.x ? new Vector3(p.at, 4.2f, door) : new Vector3(door, 4.2f, p.at);
                Box("Wide passage lintel", group, center, p.x ? new Vector3(.26f, 1.4f, 3.2f) : new Vector3(3.2f, 1.4f, .26f), trim);
            }
            var a = new GameObject(name + " / A").transform; a.SetParent(group, false);
            var other = new GameObject(name + " / B").transform; other.SetParent(group, false);
            p.floor.transform.SetParent(a, true);
            var clone = Object.Instantiate(p.floor.gameObject, other); clone.name = "Floor";
            var firstMin = b.min; var firstMax = b.max; var secondMin = b.min; var secondMax = b.max;
            if (p.x) { firstMax.x = p.at; secondMin.x = p.at; } else { firstMax.z = p.at; secondMin.z = p.at; }
            SetFloor(p.floor.transform, firstMin, firstMax); SetFloor(clone.transform, secondMin, secondMax);
        }
        Physics.SyncTransforms(); BuildMap(); EditorSceneManager.MarkSceneDirty(Scene); Export("after");
        return "42 rooms, 24 wide passages; furniture and clothing preserved.";
    }
    private static void SetFloor(Transform floor, Vector3 min, Vector3 max)
    { floor.position = (min + max) * .5f; floor.rotation = Quaternion.identity; floor.localScale = max - min; }
    private static void Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }
    public static void BuildMap()
    {
        var data = new SerializedObject(Level.GetComponent<StoreMap>());
        var walls = All<BoxCollider>().Where(b => b.enabled && b.gameObject.activeInHierarchy && !Ignore(b) && b.bounds.min.y < .5f && b.bounds.max.y > 2.2f && Mathf.Min(b.bounds.size.x, b.bounds.size.z) < .6f).ToArray();
        void Set(string field, BoxCollider[] boxes)
        {
            var list = data.FindProperty(field); list.arraySize = boxes.Length;
            for (int i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i]; var item = list.GetArrayElementAtIndex(i); var center = b.transform.TransformPoint(b.center);
                item.FindPropertyRelative("center").vector2Value = new Vector2(center.x, center.z);
                item.FindPropertyRelative("size").vector2Value = new Vector2(Mathf.Abs(b.size.x * b.transform.lossyScale.x), Mathf.Abs(b.size.z * b.transform.lossyScale.z));
                item.FindPropertyRelative("angle").floatValue = -b.transform.eulerAngles.y;
            }
        }
        Set("floors", Floors); Set("walls", walls);
        data.FindProperty("minimum").vector2Value = new Vector2(Floors.Min(f => f.bounds.min.x), Floors.Min(f => f.bounds.min.z));
        data.FindProperty("maximum").vector2Value = new Vector2(Floors.Max(f => f.bounds.max.x), Floors.Max(f => f.bounds.max.z));
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    public static string ValidateAndSave()
    {
        if (Application.isPlaying || Floors.Length != 42) throw new InvalidOperationException("Expected 42 rooms in Edit mode.");
        StoreRoomDressingSetup.Bake();
        var failures = StoreRoomDressingSetup.Validate();
        if (failures.Length > 0) throw new InvalidOperationException(string.Join("; ", failures));
        var ids = All<NetworkObject>().Select(n => new SerializedObject(n).FindProperty("SceneId").ulongValue).ToArray();
        if (ids.Any(n => n == 0) || ids.Distinct().Count() != ids.Length) throw new InvalidOperationException("Invalid network IDs");
        if (All<ClothingPickup>().Length != 96) throw new InvalidOperationException("Garment count changed");
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(Scene); EditorSceneManager.SaveScene(Scene);
        Export("after");
        string result = "PASS: 42 connected rooms, 96 accessible garments, preserved unique network IDs; 92% width/depth, 84.64% floor area. Static geometry compacted, actor dimensions unchanged.";
        File.WriteAllText("ArtSource/CompactStore/validation.txt", result); return result;
    }
}
