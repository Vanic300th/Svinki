using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Connects the artist's pig-rigged wardrobe and places searchable, unlit-by-default loot.</summary>
public static class PigClothingAssets
{
    public const string ScenePath = "Assets/Scenes/SampleScene.unity";
    public const string ScatterRoot = "Hidden 3D clothing";
    private const string MeshFolder = "Assets/ClothingLibrary/Generated/GroundMeshes";

    public static string DisplayName(string model)
    {
        switch (model)
        {
            case "hat1_worn": return "Knitted hat";
            case "hat2_worn": return "Light hat";
            case "cap1_worn": return "Cap";
            case "shirt1_worn": return "Collared shirt";
            case "shirt2_worn": return "Dark T-shirt";
            case "shirt3_worn": return "Colourful T-shirt";
            case "hoodie1_worn": return "Hoodie";
            case "sleeve1_worn": return "Camouflage long sleeve";
            case "sweater1_worn": return "Warm sweater";
            case "pants1_worn": return "Light trousers";
            case "pants2_worn": return "Jeans";
            case "pants3_worn": return "Patterned trousers";
            case "loafers1_worn": return "Leather loafers";
            case "sneakers2_worn": return "Lace-up sneakers";
            case "sneakers3_worn": return "Sport sneakers";
            default: return model.Replace("_worn", "");
        }
    }

    public static void CreatePickup(ClothingDefinition clothing, string prefabPath)
    {
        if (!AssetDatabase.IsValidFolder(MeshFolder))
            AssetDatabase.CreateFolder("Assets/ClothingLibrary/Generated", "GroundMeshes");
        GameObject source = UnityEngine.Object.Instantiate(clothing.Model);
        GameObject root = new GameObject("Pickup 3D " + clothing.DisplayName);
        try
        {
            source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            source.transform.localScale = Vector3.one;
            var baked = new List<(Mesh mesh, Material[] materials)>();
            foreach (SkinnedMeshRenderer skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh { name = clothing.Model.name + " ground" };
                skin.BakeMesh(mesh);
                Matrix4x4 toRoot = source.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                mesh.vertices = mesh.vertices.Select(toRoot.MultiplyPoint3x4).ToArray();
                mesh.normals = mesh.normals.Select(n => toRoot.MultiplyVector(n).normalized).ToArray();
                // The artist's outline submesh is for presentation, not permanent pickup glow.
                for (int i = 0; i < skin.sharedMaterials.Length && i < mesh.subMeshCount; i++)
                    if (skin.sharedMaterials[i] != null && skin.sharedMaterials[i].name.Contains("obvodka"))
                        mesh.SetTriangles(Array.Empty<int>(), i);
                mesh.RecalculateBounds();
                baked.Add((mesh, skin.sharedMaterials));
            }
            if (baked.Count == 0) throw new InvalidOperationException("No pig skin in " + clothing.Model.name);
            Bounds bounds = baked[0].mesh.bounds;
            foreach (var piece in baked.Skip(1)) bounds.Encapsulate(piece.mesh.bounds);
            bool soft = clothing.Slot == ClothingSlot.Torso || clothing.Slot == ClothingSlot.Legs;
            Quaternion rotation = soft ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
            float span = clothing.Slot == ClothingSlot.Head ? .48f : clothing.Slot == ClothingSlot.Feet ? .56f : .85f;
            float scale = span / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, .001f);
            Matrix4x4 shape = Matrix4x4.Scale(new Vector3(1, soft ? .28f : 1, 1)) *
                              Matrix4x4.Rotate(rotation) * Matrix4x4.Scale(Vector3.one * scale);
            float minY = baked.Min(p => p.mesh.vertices.Min(v => shape.MultiplyPoint3x4(v - bounds.center).y));
            int index = 0;
            Bounds combined = new Bounds();
            foreach (var piece in baked)
            {
                Mesh mesh = piece.mesh;
                mesh.vertices = mesh.vertices.Select(v => shape.MultiplyPoint3x4(v - bounds.center) + Vector3.up * (.025f - minY)).ToArray();
                mesh.normals = mesh.normals.Select(n => shape.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                string filename = Path.GetFileNameWithoutExtension(prefabPath) + " " + index;
                string path = MeshFolder + "/" + filename + ".asset";
                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                var part = new GameObject("Clothing fabric", typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(root.transform, false);
                part.GetComponent<MeshFilter>().sharedMesh = saved;
                part.GetComponent<MeshRenderer>().sharedMaterials = piece.materials;
                if (index++ == 0) combined = saved.bounds; else combined.Encapsulate(saved.bounds);
            }
            BoxCollider collider = root.AddComponent<BoxCollider>(); collider.isTrigger = true;
            collider.center = combined.center;
            collider.size = new Vector3(Mathf.Max(.25f, combined.size.x), Mathf.Max(.18f, combined.size.y), Mathf.Max(.25f, combined.size.z));
            PickupItem item = root.AddComponent<PickupItem>();
            var pickup = new SerializedObject(item);
            pickup.FindProperty("itemName").stringValue = clothing.DisplayName;
            pickup.FindProperty("glowStrength").floatValue = 0;
            pickup.FindProperty("sparkleRate").floatValue = 0;
            pickup.FindProperty("preserveBaseColor").boolValue = true;
            pickup.ApplyModifiedPropertiesWithoutUndo();
            ClothingPickup wearable = root.AddComponent<ClothingPickup>();
            var data = new SerializedObject(wearable); data.FindProperty("clothing").objectReferenceValue = clothing;
            data.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<NetworkObject>(); root.AddComponent<NetworkPickup>();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(root); }
    }

    [MenuItem("Svinki/Одежда/Разложить 3D одежду по тайникам")]
    public static void ScatterMenu() => Debug.Log(Scatter());

    public static string Scatter()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        ClothingLibraryImporter.Rebuild();
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ClothingLibrary/Generated/Pickups" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(p => p.GetComponent<ClothingPickup>()?.Clothing?.PigRigged == true).OrderBy(p => p.name).ToArray();
        if (prefabs.Length != 15) throw new InvalidOperationException("Expected all 15 pig-rigged clothes, found " + prefabs.Length);
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (scene.isDirty) throw new InvalidOperationException("Save the level before scattering clothing.");
        SceneManager.SetActiveScene(scene);
        try
        {
            Directory.CreateDirectory("Docs/LevelDesign");
            const string backup = "Docs/LevelDesign/Before3DClothing.unity";
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Replace old clothing with hidden 3D loot");
            GameObject level = scene.GetRootGameObjects().First(g => g.name == "Graybox Level");
            Transform previousScatter = level.transform.Find(ScatterRoot);
            if (previousScatter != null) Undo.DestroyObjectImmediate(previousScatter.gameObject);
            var old = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ClothingPickup>(true)).ToArray();
            foreach (var pickup in old) Undo.DestroyObjectImmediate(pickup.gameObject);
            // Empty stands advertised the old flat cards and made the new loot too obvious.
            foreach (Transform stand in level.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Clothing display pedestal").ToArray())
                Undo.DestroyObjectImmediate(stand.gameObject);
            var group = new GameObject(ScatterRoot); group.transform.SetParent(level.transform, false);
            Undo.RegisterCreatedObjectUndo(group, "Hidden 3D clothing");
            var floors = level.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name == "Floor").ToArray();
            var positions = new List<Vector3>();
            int count = 0;
            foreach (BoxCollider floor in floors)
            {
                Bounds area = floor.bounds;
                for (int corner = 0; corner < 3; corner++)
                {
                    bool placed = false;
                    for (int attempt = 0; attempt < 16 && !placed; attempt++)
                    {
                        float inset = 1.05f + (attempt % 4) * .55f;
                        float slide = attempt / 4 * .65f;
                        float x = corner == 1 ? area.max.x - inset : area.min.x + inset;
                        float z = corner == 2 ? area.max.z - inset : area.min.z + inset;
                        x += corner == 1 ? -slide : slide;
                        Vector3 point = new Vector3(x, area.max.y, z);
                        if (positions.Any(p => (p - point).sqrMagnitude < 1.5f)) continue;
                        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, .7f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - area.max.y) > .2f) continue;
                        if (Physics.CheckBox(point + Vector3.up * .2f, new Vector3(.46f,.16f,.46f), Quaternion.identity,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                        Spawn(prefabs[count * 7 % prefabs.Length], group.transform, point + Vector3.up * .015f, count++, "Corner / " + floor.transform.parent.name);
                        positions.Add(point); placed = true;
                    }
                    if (!placed) throw new InvalidOperationException("No reachable clothing corner in " + floor.transform.parent.name);
                }
            }
            foreach (BoxCollider floor in floors.Take(6))
            {
                Vector3 point = ExtraCorner(floor, positions);
                Spawn(prefabs[count * 7 % prefabs.Length], group.transform, point, count++, "Corner / " + floor.transform.parent.name);
                positions.Add(point);
            }
            Physics.SyncTransforms();
            RefreshNavigation(level, scene);
            RegenerateSceneIds(scene);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Undo.CollapseUndoOperations(undo);
            Directory.CreateDirectory("ArtSource/Clothing3D");
            File.WriteAllText("ArtSource/Clothing3D/placement.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
                removedOldPickups = old.Length, newPickups = count, variants = prefabs.Length,
                rooms = floors.Select(f => f.transform.parent.name).ToArray(),
                items = group.GetComponentsInChildren<ClothingPickup>().Select(c => new { c.name, clothing = c.Clothing.DisplayName,
                    position = new { x=c.transform.position.x,y=c.transform.position.y,z=c.transform.position.z } }).ToArray()
            }, Newtonsoft.Json.Formatting.Indented));
            return $"Replaced {old.Length} old clothing pickups with {count} hidden 3D items: {prefabs.Length} variants across {floors.Length} rooms. Navigation and FishNet scene IDs saved.";
        }
        finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void Spawn(GameObject prefab, Transform parent, Vector3 point, int index, string spot)
    {
        var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        item.name = $"Hidden clothing {index+1:00} - {item.GetComponent<ClothingPickup>().Clothing.DisplayName} ({spot})";
        item.transform.SetPositionAndRotation(point, Quaternion.Euler(0, (index * 137 + 23) % 360, 0));
        Undo.RegisterCreatedObjectUndo(item, "Scatter 3D clothing");
    }

    private static Vector3 ExtraCorner(BoxCollider floor, List<Vector3> positions)
    {
        Bounds b = floor.bounds;
        for(int attempt=0;attempt<32;attempt++)
        {
            float inset=1.05f+attempt%4*.55f, slide=attempt/4*.65f;
            Vector3 p=new Vector3(b.max.x-inset-slide,b.max.y+.015f,b.max.z-inset);
            if(positions.Any(other=>(other-p).sqrMagnitude<1.5f))continue;
            if(!NavMesh.SamplePosition(p,out NavMeshHit hit,.7f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-b.max.y)>.2f)continue;
            if(Physics.CheckBox(p+Vector3.up*.2f,new Vector3(.46f,.16f,.46f),Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
            return p;
        }
        throw new InvalidOperationException("No free fourth corner in "+floor.transform.parent.name);
    }

    public static string RepairShelfPlacements()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
        var scene=SceneManager.GetSceneByPath(ScenePath);
        var level=scene.GetRootGameObjects().Single(g=>g.name=="Graybox Level");
        var items=level.transform.Find(ScatterRoot).GetComponentsInChildren<ClothingPickup>(true);
        var bad=items.Where(c=>c.name.Contains("Lowest shelf")).ToArray();
        var floors=level.GetComponentsInChildren<BoxCollider>(true).Where(c=>c.name=="Floor").Take(6).ToArray();
        var positions=items.Except(bad).Select(c=>c.transform.position).ToList();
        for(int i=0;i<bad.Length;i++)
        {
            Undo.RecordObject(bad[i].transform,"Move inaccessible shelf clothing");
            bad[i].transform.position=ExtraCorner(floors[i],positions);
            bad[i].name=bad[i].name.Replace("Lowest shelf","Corner / "+floors[i].transform.parent.name);
            positions.Add(bad[i].transform.position);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bad[i].transform);
        }
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Moved "+bad.Length+" inaccessible shelf pickups to free fourth corners.";
    }

    private static void RefreshNavigation(GameObject level, Scene scene)
    {
        var surface = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NavMeshSurface>(true)).Single();
        surface.BuildNavMesh();
        NavMeshData data = surface.navMeshData;
        const string path = "Assets/Settings/StoreExpandedNavMesh.asset";
        data.name = Path.GetFileNameWithoutExtension(path); // asset main object name must match the file name
        var saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if (saved == null) AssetDatabase.CreateAsset(data, path);
        else if (saved != data)
        {
            surface.RemoveData(); EditorUtility.CopySerialized(data, saved); UnityEngine.Object.DestroyImmediate(data);
            surface.navMeshData = saved; surface.AddData(); EditorUtility.SetDirty(saved);
        }
        EditorUtility.SetDirty(surface);
    }

    private static void RegenerateSceneIds(Scene scene)
    {
        var create = typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic);
        var serialize = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (NetworkObject item in (List<NetworkObject>)create.Invoke(null, new object[] { scene, true, 0 }))
            serialize.Invoke(item, new object[] { true, false });
    }
}
