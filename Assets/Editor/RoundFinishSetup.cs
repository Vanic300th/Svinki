using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Managing.Object;
using FishNet.Object;

public static class RoundFinishSetup
{
    [MenuItem("Svinki/Раунд/Настроить кнопку готовности и выброс вещей")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ClothingLibrary/Generated/Pickups" }).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p.GetComponent<ClothingPickup>()?.Clothing != null).ToArray();
        var registry = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>("Assets/DefaultPrefabObjects.asset");
        foreach (var prefab in prefabs)
        {
            var clothing = prefab.GetComponent<ClothingPickup>().Clothing;
            var so = new SerializedObject(clothing); so.FindProperty("pickupPrefab").objectReferenceValue = prefab; so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clothing);
            registry.AddObject(prefab.GetComponent<NetworkObject>(), true);
        }
        EditorUtility.SetDirty(registry);
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        var level = scene.GetRootGameObjects().Single(g => g.name == "Graybox Level");
        var existing = level.transform.Find("Round Finish Station");
        if (existing == null)
        {
            var station = new GameObject("Round Finish Station"); SceneManager.MoveGameObjectToScene(station, scene); station.transform.SetParent(level.transform);
            Undo.RegisterCreatedObjectUndo(station, "Add finish station"); station.transform.position = new Vector3(0, .95f, -.3f);
            station.AddComponent<RoundFinishStation>();
            var collider = station.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.center = new Vector3(0, .14f, -.15f); collider.size = new Vector3(1.6f, .8f, .5f);
            Material Material(string name, Color color)
            {
                string path = "Assets/Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
                material.color = color; return material;
            }
            var metal = Material("Finish Station Metal", new Color(.15f, .21f, .23f));
            var green = Material("Finish Station Button", new Color(.12f, .65f, .38f));
            green.EnableKeyword("_EMISSION"); green.SetColor("_EmissionColor", new Color(.06f, .23f, .12f)); EditorUtility.SetDirty(green);
            void Piece(string name, Vector3 position, Vector3 scale, Material material)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.SetParent(station.transform, false);
                obj.transform.localPosition = position; obj.transform.localScale = scale; UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
                obj.GetComponent<Renderer>().sharedMaterial = material;
            }
            Piece("Stand", new Vector3(0, -.475f, .1f), new Vector3(.2f, .95f, .2f), metal);
            Piece("Plate", new Vector3(0, .16f, 0), new Vector3(1.55f, .65f, .22f), metal);
            Piece("Ready Button", new Vector3(0, .15f, -.15f), new Vector3(1.25f, .40f, .12f), green);
            var lamp = new GameObject("Ready button light", typeof(Light)); lamp.transform.SetParent(station.transform, false); lamp.transform.localPosition = new Vector3(0, .8f, -1);
            var light = lamp.GetComponent<Light>(); light.type = LightType.Point; light.range = 3; light.intensity = 1.2f; light.color = new Color(.7f, 1, .85f);
        }
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Debug.Log("Finish station saved at the entrance; pickup prefabs linked for " + prefabs.Length + " garments.");
    }
}
