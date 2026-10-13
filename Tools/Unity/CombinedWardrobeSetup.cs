using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using FishNet.Managing.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Run in an isolated Editor: import the wardrobe, retain search anchors and verify rating.</summary>
public static class CombinedWardrobeSetup
{
    public static void Build()
    {
        OutfitRatingSetup.Build();
        var rules = OutfitRatingCatalog.Load();
        if (rules.items.Length != 33) throw new Exception("Expected the complete 33-piece wardrobe.");
        foreach (var profile in rules.items)
        {
            var clothing = profile.clothing;
            if (!clothing.PigRigged || clothing.PickupPrefab == null || clothing.WornModel == null)
                throw new Exception("Unprepared garment: " + profile.key);
            var data = new SerializedObject(clothing);
            data.FindProperty("displayName").stringValue = PigClothingAssets.DisplayName(clothing.Model.name);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clothing);
        }
        var registry = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>("Assets/DefaultPrefabObjects.asset");
        foreach (var profile in rules.items) registry.AddObject(profile.clothing.PickupPrefab.GetComponent<NetworkObject>(), true);
        registry.InitializePrefabRange(0);
        typeof(DefaultPrefabObjects).GetMethod("SetAssetPathHashes", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(registry, new object[] { 0 });
        EditorUtility.SetDirty(registry);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var layout = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RoundClothingLayout>(true)).Single();
        var serialized = new SerializedObject(layout);
        var pickups = serialized.FindProperty("pickups");
        var positions = serialized.FindProperty("positions");
        var rotations = serialized.FindProperty("rotations");
        if (pickups.arraySize != 96 || positions.arraySize != 96 || rotations.arraySize != 96) throw new Exception("Search anchors changed.");
        for (int i = 0; i < pickups.arraySize; i++)
        {
            var old = (ClothingPickup)pickups.GetArrayElementAtIndex(i).objectReferenceValue;
            if (old == null) throw new Exception("Empty original search slot.");
            var item = rules.items[i % rules.items.Length].clothing;
            if (old.Clothing == item) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(item.PickupPrefab, scene);
            go.transform.SetParent(old.transform.parent, true);
            go.transform.SetPositionAndRotation(positions.GetArrayElementAtIndex(i).vector3Value, rotations.GetArrayElementAtIndex(i).quaternionValue);
            go.transform.localScale = old.transform.localScale;
            go.name = "Hidden clothing " + i.ToString("00") + " / " + item.DisplayName;
            var modifier = go.GetComponent<NavMeshModifier>() ?? go.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            pickups.GetArrayElementAtIndex(i).objectReferenceValue = go.GetComponent<ClothingPickup>();
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var create = typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic);
        var save = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var obj in (List<NetworkObject>)create.Invoke(null, new object[] { scene, true, 0 })) save.Invoke(obj, new object[] { true, false });
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        OutfitRatingCheck.Main();
        ValidateAssets(scene, rules, registry);
        RenderReferences(rules);
        // Re-running the merge must not accumulate profiles or repeated examples.
        OutfitRatingSetup.Build();
        if (rules.items.Length != 33 || rules.references.Length != 19) throw new Exception("Rating setup is not idempotent.");
        File.WriteAllText("ArtSource/OutfitRating/wardrobe-validation.txt", "PASS: 33 rigged garments; 19 unique references (6 originals + 16 supplied, 3 overlaps); 2800 complete combinations; original definitions rated; mixed wardrobes; 96 shuffled hiding slots cover every garment; all network prefabs/catalogs and scene IDs valid; every garment attaches to the pig; setup can be repeated.\n");
        Debug.Log("PASS: combined wardrobe and outfit rating.");
    }
    private static void ValidateAssets(Scene scene, OutfitRatingCatalog rules, DefaultPrefabObjects registry)
    {
        var pickups = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ClothingPickup>(true)).ToArray();
        if (pickups.Length != 96 || pickups.Select(p => p.Clothing).Distinct().Count() != 33) throw new Exception("A garment is missing from the store.");
        var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkObject>(true)).ToArray();
        var field = typeof(NetworkObject).GetField("SceneId", BindingFlags.NonPublic | BindingFlags.Instance);
        var ids = objects.Select(o => (ulong)field.GetValue(o)).ToArray();
        if (ids.Any(id => id == 0) || ids.Distinct().Count() != ids.Length) throw new Exception("Invalid network scene IDs.");
        var player = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab").GetComponent<NetworkPlayer>());
        var catalogue = player.FindProperty("clothingCatalog");
        var registered = new SerializedObject(registry).FindProperty("_prefabs");
        foreach (var profile in rules.items)
        {
            var clothing = profile.clothing;
            if (!Enumerable.Range(0, catalogue.arraySize).Any(i => catalogue.GetArrayElementAtIndex(i).objectReferenceValue == clothing)) throw new Exception("Not replicated: " + profile.key);
            var prefab = clothing.PickupPrefab.GetComponent<NetworkObject>();
            if (prefab.AssetPathHash == 0 || !Enumerable.Range(0, registered.arraySize).Any(i => registered.GetArrayElementAtIndex(i).objectReferenceValue == prefab)) throw new Exception("Not registered: " + profile.key);
        }
        var pig = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Pigs/PigAvatar"));
        try
        {
            var appearance = pig.GetComponent<PigAppearance>();
            foreach (var profile in rules.items)
            {
                var holder = new GameObject(profile.key);
                if (!PigClothingBinding.TryAttach(profile.clothing, appearance.ModelRoot, holder.transform)) throw new Exception("Cannot wear " + profile.key);
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(pig); }
    }
    public static void RenderOnly()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        RenderReferences(OutfitRatingCatalog.Load());
    }
    private static void RenderReferences(OutfitRatingCatalog rules)
    {
        var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var stage = new GameObject("Wardrobe reference studio");
        SceneManager.MoveGameObjectToScene(stage, preview);
        stage.transform.position = new Vector3(25000, 12000, 12000);
        var cameraObject = new GameObject("Reference camera"); cameraObject.transform.SetParent(stage.transform, false);
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.84f, .85f, .87f);
        camera.cullingMask = 1 << 30; camera.fieldOfView = 32; camera.nearClipPlane = .05f; camera.farClipPlane = 10;
        camera.transform.localPosition = new Vector3(0, 1.02f, 3.9f); camera.transform.LookAt(stage.transform.position + Vector3.up * .98f);
        var render = new RenderTexture(240, 300, 24); render.Create(); camera.targetTexture = render;
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("Reference light"); go.transform.SetParent(stage.transform, false);
            go.transform.localPosition = i == 0 ? new Vector3(-2, 3, 3) : i == 1 ? new Vector3(2, 2, 3) : new Vector3(0, 3, -2);
            go.transform.LookAt(stage.transform.position + Vector3.up);
            var light = go.AddComponent<Light>(); light.type = LightType.Spot; light.spotAngle = 100; light.range = 9; light.intensity = i == 0 ? 9 : 5; light.cullingMask = 1 << 30;
        }
        Directory.CreateDirectory("ArtSource/OutfitRating/RenderFrames");
        try
        {
            for (int i = 0; i < rules.references.Length; i++)
            {
                var pig = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Pigs/PigAvatar"), stage.transform);
                try
                {
                    pig.GetComponent<PigMotion>().enabled = false;
                    var appearance = pig.GetComponent<PigAppearance>(); appearance.Apply(0);
                    foreach (var clothing in rules.references[i].items)
                        if (!PigClothingBinding.TryAttach(clothing, appearance.ModelRoot, new GameObject(clothing.name).transform)) throw new Exception("Reference clothing cannot attach.");
                    foreach (Transform t in pig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                    foreach (int side in new[] {0, 1})
                    {
                        pig.transform.localRotation = Quaternion.Euler(0, side * 180, 0);
                        camera.Render();
                        var previous = RenderTexture.active; RenderTexture.active = render;
                        var image = new Texture2D(240, 300, TextureFormat.RGB24, false);
                        image.ReadPixels(new Rect(0, 0, 240, 300), 0, 0); image.Apply(); RenderTexture.active = previous;
                        File.WriteAllBytes("ArtSource/OutfitRating/RenderFrames/" + i.ToString("00") + "-" + side + ".png", image.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(image);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(pig); }
            }
        }
        finally { camera.targetTexture = null; render.Release(); UnityEngine.Object.DestroyImmediate(render); EditorSceneManager.CloseScene(preview, true); }
    }
}
