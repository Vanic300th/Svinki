using System;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Creates the mode destination and adds only the selector to the existing lobby.</summary>
public static class GameModeSceneSetup
{
    public const string ScenePath = SvinkiAssetPaths.Scenes + "/Gameplay/GrandpasHouse.unity";
    private const string MaterialFolder = SvinkiAssetPaths.Environment + "/GrandpasHouse/Materials";
    private static T[] Items<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("Svinki/Game Modes/Create Grandpa's Fridge and lift selector")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        Scene lobby = SceneManager.GetSceneByName(NetworkLobby.FriendslopSceneName);
        bool openedLobby = !lobby.IsValid();
        if (openedLobby) lobby = EditorSceneManager.OpenScene(SvinkiAssetPaths.SocialLobbyScene, OpenSceneMode.Additive);
        if (lobby.isDirty) throw new InvalidOperationException("Save lobby changes before adding the mode selector.");
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(lobby);
        var lift = Items<LobbyFreightElevator>(lobby).Single();
        if (lift.GetComponentInChildren<LobbyGameSelector>(true) == null)
        {
            SvinkiAssetPaths.EnsureFolder(MaterialFolder);
            Material panelMaterial = Material("Lift panel", new Color(.045f, .08f, .09f));
            var panel = Box(lift.transform, "Choose game", new Vector3(1.65f, 1.5f, 1.84f), new Vector3(1.7f, 1.15f, .13f), panelMaterial);
            panel.AddComponent<NetworkObject>();
            panel.AddComponent<LobbyGameSelector>();
            // Unscaled child text, so the label stays readable at the panel's physical size.
            Label(lift.transform, "CHOOSE GAME\n01  Become a Model\n02  Grandpa's Fridge\n[E] OPEN PANEL", new Vector3(1.65f, 1.5f, 1.76f), new Vector2(1.55f, 1.02f), .18f);
            lift.name = "Freight lift - choose game";
            var liftData = new SerializedObject(lift);
            liftData.FindProperty("destinationTitle").stringValue = "CHOOSE GAME";
            liftData.ApplyModifiedPropertiesWithoutUndo();
            var sign = liftData.FindProperty("sign").objectReferenceValue as TMP_Text;
            if (sign != null) sign.text = "CHOOSE GAME\nSELECT AT THE CABIN PANEL";
            typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { lobby, false, 0 });
            foreach (var obj in Items<NetworkObject>(lobby))
                typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(obj, new object[] { true, false });
            EditorSceneManager.MarkSceneDirty(lobby); EditorSceneManager.SaveScene(lobby);
        }

        Scene house = SceneManager.GetSceneByPath(ScenePath);
        if (!house.IsValid() && System.IO.File.Exists(ScenePath)) house = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (!System.IO.File.Exists(ScenePath))
        {
            house = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(house);
            var root = new GameObject("Grandpa's Fridge - house blockout").transform;
            Material floor = Material("Wood floor", new Color(.38f, .24f, .13f));
            Material wall = Material("Warm plaster", new Color(.78f, .70f, .53f));
            Material green = Material("Kitchen green", new Color(.24f, .40f, .27f));
            Material cream = Material("Fridge enamel", new Color(.88f, .86f, .71f));
            Material dark = Material("Dark wood", new Color(.19f, .12f, .08f));
            Material accent = Material("Food orange", new Color(.92f, .45f, .12f));
            Box(root, "House floor", new Vector3(0, -.15f, 6), new Vector3(20, .3f, 24), floor);
            Box(root, "West wall", new Vector3(-10, 2, 6), new Vector3(.25f, 4, 24), wall);
            Box(root, "East wall", new Vector3(10, 2, 6), new Vector3(.25f, 4, 24), wall);
            Box(root, "North wall", new Vector3(0, 2, 18), new Vector3(20, 4, .25f), wall);
            Box(root, "South wall", new Vector3(0, 2, -6), new Vector3(20, 4, .25f), wall);
            Box(root, "Kitchen partition", new Vector3(0, 1.8f, 12), new Vector3(.2f, 3.6f, 12), wall);
            Box(root, "Living room front wall", new Vector3(-7, 1.8f, 3), new Vector3(6, 3.6f, .2f), wall);
            Box(root, "Kitchen front wall", new Vector3(7, 1.8f, 3), new Vector3(6, 3.6f, .2f), wall);
            Box(root, "Kitchen doorway lintel", new Vector3(2, 3.35f, 3), new Vector3(4, .5f, .2f), wall);
            Box(root, "Living doorway lintel", new Vector3(-2, 3.35f, 3), new Vector3(4, .5f, .2f), wall);
            Box(root, "Entry runner", new Vector3(0, .02f, -1), new Vector3(5, .035f, 6), green);
            var kitchen = new GameObject("Kitchen - food gameplay area").transform; kitchen.SetParent(root, false);
            Box(kitchen, "Kitchen counter", new Vector3(4.7f, .55f, 16.9f), new Vector3(7.5f, 1.1f, 1.4f), green);
            Box(kitchen, "Countertop", new Vector3(4.7f, 1.14f, 16.9f), new Vector3(7.7f, .14f, 1.55f), cream);
            Box(kitchen, "Fridge", new Vector3(8.6f, 1.3f, 13.7f), new Vector3(1.8f, 2.6f, 1.7f), cream);
            Box(kitchen, "Fridge door seam", new Vector3(8.6f, 1.85f, 12.83f), new Vector3(1.7f, .035f, .035f), dark);
            Box(kitchen, "Fridge handle", new Vector3(7.96f, 1.3f, 12.76f), new Vector3(.08f, .6f, .12f), dark);
            Box(kitchen, "Dining tabletop", new Vector3(4.8f, 1, 8.5f), new Vector3(3.6f, .16f, 2.3f), dark);
            foreach (float x in new[] { 3.3f, 6.3f }) foreach (float z in new[] { 7.6f, 9.4f })
                Box(kitchen, "Table leg", new Vector3(x, .5f, z), new Vector3(.14f, 1, .14f), dark);
            // Food stays as static placeholders until eating and network authority are implemented.
            for (int i = 0; i < 4; i++)
                Box(kitchen, "Food placeholder " + (i + 1), new Vector3(3.5f + i * .8f, 1.27f, 16.7f), new Vector3(.38f, .25f, .38f), accent);
            var living = new GameObject("Living room - Grandpa area").transform; living.SetParent(root, false);
            Box(living, "Sofa seat", new Vector3(-7.5f, .45f, 10), new Vector3(2.6f, .9f, 4.5f), green);
            Box(living, "Sofa back", new Vector3(-8.7f, 1, 10), new Vector3(.35f, 1.4f, 4.5f), green);
            Box(living, "Coffee table", new Vector3(-4.5f, .55f, 10), new Vector3(2, 1.1f, 2.8f), dark);
            Box(living, "Pantry shelving", new Vector3(-5.5f, 1.4f, 17), new Vector3(6.5f, 2.8f, 1), dark);
            for (int i = 0; i < 3; i++) Box(living, "Pantry shelf", new Vector3(-5.5f, .65f + i * .85f, 16.4f), new Vector3(6.4f, .08f, .5f), cream);
            var markers = new GameObject("Gameplay anchors - future mechanics").transform; markers.SetParent(root, false);
            Anchor(markers, "GrandpaSpawn", new Vector3(-5.5f, .05f, 13));
            Anchor(markers, "FridgeFoodSpawn", new Vector3(8.6f, 1.2f, 13.7f));
            for (int i = 0; i < 6; i++) Anchor(markers, "PlayerSpawn" + i, new Vector3((i % 3 - 1) * 1.5f, .05f, -3 + i / 3 * 1.5f));
            Label(root, "GRANDPA'S FRIDGE", new Vector3(0, 3.35f, 2.82f), new Vector2(13, .8f), .7f);
            Label(root, "KITCHEN", new Vector3(7, 2.7f, 2.84f), new Vector2(5.5f, .6f), .5f);
            Label(root, "LIVING ROOM", new Vector3(-7, 2.7f, 2.84f), new Vector2(5.5f, .6f), .45f);
            Label(root, "Raid the food. Don't get caught!\nHOUSE BLOCKOUT / GAMEPLAY IN DEVELOPMENT\nESC > Return to Lobby", new Vector3(0, 2, 17.82f), new Vector2(15, 1.8f), .45f);

            var source = lobby.GetRootGameObjects().Single(r => r.name == "Friendslop presentation");
            var presentation = Object.Instantiate(source); presentation.name = "Grandpa house presentation";
            SceneManager.MoveGameObjectToScene(presentation, house);
            foreach (var camera in presentation.GetComponentsInChildren<GrayboxFirstPersonCamera>(true))
            {
                var data = new SerializedObject(camera); data.FindProperty("target").objectReferenceValue = null; data.ApplyModifiedPropertiesWithoutUndo();
                camera.transform.SetPositionAndRotation(new Vector3(0, 1.7f, -3), Quaternion.identity);
            }
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.64f, .59f, .48f);
            RenderSettings.fog = false;
            var map = root.gameObject.AddComponent<StoreMap>(); var serialized = new SerializedObject(map);
            serialized.FindProperty("visibleRange").floatValue = 1;
            serialized.FindProperty("minimum").vector2Value = new Vector2(-10.2f, -6.2f);
            serialized.FindProperty("maximum").vector2Value = new Vector2(10.2f, 18.2f);
            var floors = serialized.FindProperty("floors"); floors.arraySize = 1;
            floors.GetArrayElementAtIndex(0).FindPropertyRelative("center").vector2Value = new Vector2(0, 6);
            floors.GetArrayElementAtIndex(0).FindPropertyRelative("size").vector2Value = new Vector2(20, 24);
            var walls = serialized.FindProperty("walls");
            Physics.SyncTransforms();
            var boxes = root.GetComponentsInChildren<BoxCollider>().Where(c => c.name.Contains("wall") || c.name.Contains("partition")).ToArray();
            walls.arraySize = boxes.Length;
            for (int i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i].bounds; var entry = walls.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("center").vector2Value = new Vector2(b.center.x, b.center.z);
                entry.FindPropertyRelative("size").vector2Value = new Vector2(b.size.x, b.size.z);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(house, ScenePath);
        }
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        else scenes.First(s => s.path == ScenePath).enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray(); AssetDatabase.SaveAssets();
        if (openedLobby && lobby != previous) EditorSceneManager.CloseScene(lobby, true);
        if (house.IsValid()) { SceneManager.SetActiveScene(house); Selection.activeGameObject = house.GetRootGameObjects().First(); }
    }

    private static Material Material(string name, Color color)
    {
        SvinkiAssetPaths.EnsureFolder(MaterialFolder);
        string path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.color = color; return material;
    }
    private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    private static void Anchor(Transform parent, string name, Vector3 position)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
    }
    private static void Label(Transform parent, string value, Vector3 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(value.Split('\n')[0]); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        var text = go.AddComponent<TextMeshPro>(); text.font = TMP_Settings.defaultFontAsset;
        text.text = value; text.fontSize = fontSize * 10; text.alignment = TextAlignmentOptions.Center; text.color = new Color(.98f, .91f, .71f);
        text.rectTransform.sizeDelta = size; text.enableAutoSizing = true;
        text.fontSizeMax = fontSize * 10; text.fontSizeMin = .2f;
    }
}
