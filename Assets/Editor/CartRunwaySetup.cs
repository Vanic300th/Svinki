using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FishNet.Component.Transforming;
using FishNet.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CartRunwaySetup
{
    [MenuItem("Svinki/Раунд/Добавить тележки и случайную раскладку")]
    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first");
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var level = scene.GetRootGameObjects().Single(g => g.name == "Graybox Level");
            var layout = level.GetComponent<RoundClothingLayout>();
            if (layout == null)
            {
                layout = Undo.AddComponent<RoundClothingLayout>(level);
                var pickups = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ClothingPickup>(true)).Where(p => p.Clothing != null).OrderBy(p => p.name, StringComparer.Ordinal).ThenBy(p => p.transform.position.x).ThenBy(p => p.transform.position.z).ToArray();
                var data = new SerializedObject(layout);
                var items = data.FindProperty("pickups"); var positions = data.FindProperty("positions"); var rotations = data.FindProperty("rotations");
                items.arraySize = positions.arraySize = rotations.arraySize = pickups.Length;
                for (int i = 0; i < pickups.Length; i++)
                {
                    items.GetArrayElementAtIndex(i).objectReferenceValue = pickups[i];
                    positions.GetArrayElementAtIndex(i).vector3Value = pickups[i].transform.position;
                    rotations.GetArrayElementAtIndex(i).quaternionValue = pickups[i].transform.rotation;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Material metal = Material("Cart Metal", new Color(.52f, .62f, .68f), .8f, .65f);
            Material rubber = Material("Cart Rubber", new Color(.025f, .035f, .05f), 0, .15f);
            Material peach = Material("Cart Peach", new Color(.98f, .38f, .27f), .1f, .4f);
            Vector3[] points = { new Vector3(-3.7f, .03f, -6), new Vector3(-6, .03f, 55), new Vector3(10.625f, .03f, 148.89423f) };
            for (int i = 0; i < points.Length; i++)
            {
                string name = "Shopping Cart " + (i + 1);
                if (level.transform.Find(name) is Transform existing)
                {
                    if (i == 2) { Undo.RecordObject(existing, "Move cart off ramp"); existing.position = points[i]; }
                    continue;
                }
                var root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, scene); root.transform.SetParent(level.transform, true);
                root.transform.position = points[i]; root.transform.rotation = Quaternion.Euler(0, i == 2 ? 90 : 0, 0);
                Undo.RegisterCreatedObjectUndo(root, "Create shopping cart");
                root.AddComponent<NetworkObject>();
                var body = root.AddComponent<Rigidbody>(); body.mass = 32; body.linearDamping = .05f; body.angularDamping = 3;
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; body.isKinematic = true;
                var collider = root.AddComponent<BoxCollider>(); collider.center = new Vector3(0, .60f, 0); collider.size = new Vector3(1.02f, 1.20f, 1.5f);
                var physics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Materials/Cart Physics.physicMaterial");
                if (physics == null)
                {
                    physics = new PhysicsMaterial("Cart Physics") { dynamicFriction = .08f, staticFriction = .08f, bounciness = .15f, frictionCombine = PhysicsMaterialCombine.Minimum };
                    AssetDatabase.CreateAsset(physics, "Assets/Materials/Cart Physics.physicMaterial");
                }
                collider.sharedMaterial = physics;
                root.AddComponent<ShoppingCart>(); root.AddComponent<NetworkShoppingCart>();
                var nt = root.AddComponent<NetworkTransform>(); var ntData = new SerializedObject(nt);
                ntData.FindProperty("_clientAuthoritative").boolValue = false; ntData.ApplyModifiedPropertiesWithoutUndo();
                var modifier = root.AddComponent<NavMeshModifier>(); modifier.ignoreFromBuild = true;
                // Steel frame, wire basket and contrasting handle, all measured in metres.
                foreach (float x in new[] { -.43f, .43f })
                {
                    Part(root.transform, "Chassis", PrimitiveType.Cube, new Vector3(x, .31f, 0), new Vector3(.065f, .065f, 1.42f), metal);
                    Part(root.transform, "Handle upright", PrimitiveType.Cube, new Vector3(x, .72f, -.66f), new Vector3(.06f, .82f, .06f), metal);
                    Part(root.transform, "Basket lip", PrimitiveType.Cube, new Vector3(x, 1.08f, .05f), new Vector3(.045f, .045f, 1.26f), metal);
                    for (int n = 0; n < 7; n++)
                        Part(root.transform, "Basket wire", PrimitiveType.Cube, new Vector3(x, .83f, -.51f + n * .185f), new Vector3(.024f, .50f, .024f), metal);
                    for (int n = 0; n < 3; n++)
                        Part(root.transform, "Basket rail", PrimitiveType.Cube, new Vector3(x, .61f + n * .15f, .05f), new Vector3(.022f, .022f, 1.26f), metal);
                    foreach (float z in new[] { -.53f, .53f })
                        Part(root.transform, "Wheel", PrimitiveType.Cylinder, new Vector3(x, .18f, z), new Vector3(.33f, .055f, .33f), rubber, Quaternion.Euler(0, 0, 90));
                }
                foreach (float z in new[] { -.55f, .68f })
                {
                    Part(root.transform, "Basket rim", PrimitiveType.Cube, new Vector3(0, 1.08f, z), new Vector3(.89f, .045f, .045f), metal);
                    for (int n = 0; n < 5; n++)
                        Part(root.transform, "End wire", PrimitiveType.Cube, new Vector3(-.37f + n * .185f, .83f, z), new Vector3(.024f, .5f, .024f), metal);
                }
                Part(root.transform, "Basket base", PrimitiveType.Cube, new Vector3(0, .57f, .05f), new Vector3(.87f, .045f, 1.23f), metal);
                Part(root.transform, "Peach handle", PrimitiveType.Cube, new Vector3(0, 1.12f, -.72f), new Vector3(1.01f, .085f, .085f), peach);
                Part(root.transform, "Peach front badge", PrimitiveType.Cube, new Vector3(0, .88f, .71f), new Vector3(.38f, .20f, .055f), peach);
            }
            var create = typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic);
            var serialize = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (NetworkObject item in (List<NetworkObject>)create.Invoke(null, new object[] { scene, true, 0 })) serialize.Invoke(item, new object[] { true, false });
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return "Saved 3 network shopping carts and " + layout.Count + " clothing anchors";
        }
        finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    private static Material Material(string name, Color color, float metallic, float smoothness)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
        material.color = color; material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness); EditorUtility.SetDirty(material); return material;
    }
    private static void Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size, Material material, Quaternion rotation = default)
    {
        var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position; obj.transform.localScale = size; obj.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>()); obj.GetComponent<Renderer>().sharedMaterial = material;
    }
}
