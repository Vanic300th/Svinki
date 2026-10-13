using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public static class SocialLobbySetup
{
    private const string Folder = "Assets/Art/Lobby";
    private static Material concrete, steel, rust, lockerPaint, cloth, leather, wood, white, dark, orange, pink;
    private static TMP_FontAsset font;
    private static T[] Items<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static Material Mat(string name, Color color, bool unlit = false)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.color = color; EditorUtility.SetDirty(material); return material;
    }
    private static GameObject Root(string name, Transform parent, Vector3 position)
    { var root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.localPosition = position; return root; }
    private static GameObject Shape(string name, Transform parent, Vector3 position, Vector3 scale, Material material, PrimitiveType type = PrimitiveType.Cube, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material; if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider = true) => Shape(name, parent, position, scale, material, PrimitiveType.Cube, collider);
    private static void Set(UnityEngine.Object target, string name, object value)
    {
        var data = new SerializedObject(target); var field = data.FindProperty(name);
        if (field == null) throw new Exception(target.GetType().Name + "." + name + " missing");
        if (value is UnityEngine.Object asset) field.objectReferenceValue = asset;
        else if (value is Vector3 vector) field.vector3Value = vector;
        else if (value is Vector2 vector2) field.vector2Value = vector2;
        else if (value is Bounds bounds) field.boundsValue = bounds;
        else if (value is bool flag) field.boolValue = flag;
        else if (value is int integer) field.intValue = integer;
        else if (value is float number) field.floatValue = number;
        else throw new Exception("Unsupported serialized field " + name);
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static T Network<T>(GameObject go) where T : MonoBehaviour
    { if (go.GetComponent<NetworkObject>() == null) go.AddComponent<NetworkObject>(); go.AddComponent<NavMeshModifier>().ignoreFromBuild = true; return go.AddComponent<T>(); }
    private static TMP_Text Sign(string text, Transform parent, Vector3 position, float width = 2.5f, float size = 2.5f, float angle = 0)
    {
        var go = Root("Sign " + text, parent, position); go.transform.localRotation = Quaternion.Euler(0, angle, 0);
        var label = go.AddComponent<TextMeshPro>(); label.font = font; label.text = text; label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(width, 1); label.color = new Color(.86f, .85f, .69f); label.richText = false; label.textWrappingMode = TextWrappingModes.NoWrap; return label;
    }
    private static Light Lamp(Transform parent, Vector3 position, Color color, float range = 9, float intensity = 2)
    {
        var go = Root("Worker light", parent, position); var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.range = range; light.intensity = intensity; light.shadows = LightShadows.None; return light;
    }
    private static LobbyProp Prop(string name, Transform parent, Vector3 position, Vector3 scale, Material material, LobbyPropKind kind = LobbyPropKind.Junk, PrimitiveType type = PrimitiveType.Cube)
    {
        // Keep the network root at scale one so mass, CCD and holding distance remain predictable.
        var go = Root(name, parent, position);
        Shape("Visual", go.transform, Vector3.zero, scale, material, type, false);
        Collider shape;
        if (type == PrimitiveType.Sphere) { var sphere = go.AddComponent<SphereCollider>(); sphere.radius = scale.x * .5f; shape = sphere; }
        else { var box = go.AddComponent<BoxCollider>(); box.size = scale; shape = box; }
        var body = go.AddComponent<Rigidbody>(); body.mass = kind == LobbyPropKind.Basketball ? .6f : kind == LobbyPropKind.Dart ? .04f : .3f;
        body.linearDamping = .15f; body.angularDamping = .3f; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.sleepThreshold = .05f; body.maxDepenetrationVelocity = 4;
        var prop = Network<LobbyProp>(go); Set(prop, "kind", (int)kind);
        Set(prop, "playBounds", new Bounds(new Vector3(0, 3, -20), new Vector3(28, 12, 42)));
        if (kind == LobbyPropKind.Puck) body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        return prop;
    }
    public static string Apply()
    {
        if (File.Exists("Assets/Scenes/FriendslopLobby.unity")) return "Separate Friendslop lobby already installed; use FriendslopSceneSetup/Check.";
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit mode required");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        if (scene.isDirty) throw new Exception("Scene has unsaved edits; save them before setup");
        if (Items<LobbyActivityManager>(scene).Length > 0) return "Social lobby already installed";
        var idField = typeof(NetworkObject).GetField("SceneId", BindingFlags.Instance | BindingFlags.NonPublic);
        var idsBefore = Items<NetworkObject>(scene).ToDictionary(n => n, n => (ulong)idField.GetValue(n));
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        concrete = Mat("Aged concrete", new Color(.29f, .32f, .3f)); steel = Mat("Industrial steel", new Color(.19f, .24f, .24f));
        rust = Mat("Rust", new Color(.31f, .17f, .10f)); lockerPaint = Mat("Locker paint", new Color(.21f, .36f, .33f));
        cloth = Mat("Worn fabric", new Color(.36f, .23f, .14f)); leather = Mat("Old leather", new Color(.16f, .10f, .07f));
        wood = Mat("Workbench wood", new Color(.32f, .24f, .14f)); white = Mat("Photo paper", new Color(.88f, .87f, .75f), true);
        dark = Mat("Screen", new Color(.04f, .07f, .08f), true); orange = Mat("Safety ochre", new Color(.9f, .55f, .13f));
        pink = Mat("Jukebox accent", new Color(.8f, .20f, .43f), true);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var level = scene.GetRootGameObjects().Single(g => g.name == "Graybox Level").transform;
        var lobby = Root("Worker Social Lobby", level, new Vector3(0, 0, -22)); lobby.AddComponent<LobbyActivityManager>();
        lobby.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var safeVolume=lobby.AddComponent<NavMeshModifierVolume>();safeVolume.area=UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
        safeVolume.center=new Vector3(0,2,1.5f);safeVolume.size=new Vector3(22.6f,8,21.6f);
        // Reuse the existing entrance, opening only its southern wall. Existing IDs/assets stay intact.
        var oldWall = Items<BoxCollider>(scene).Single(c => c.name == "Solid wall" && Vector3.Distance(c.bounds.center, new Vector3(0, 2.4f, -10)) < .1f);
        oldWall.transform.localScale = new Vector3(4.4f, 4.8f, .36f); oldWall.transform.position = new Vector3(-4.1f, 2.4f, -10);
        Box("Entrance wall right", level, new Vector3(4.1f, 2.4f, -10), new Vector3(4.4f, 4.8f, .36f), concrete);
        Box("Entrance lintel", level, new Vector3(0, 4.05f, -10), new Vector3(3.8f, 1.5f, .36f), steel);
        foreach (var poster in Items<Transform>(scene).Where(t => t.name == "Peach poster frame" || t.name == "Sign - PEACH / NEW COLLECTION").ToArray()) poster.position += Vector3.left * 4;
        Box("Passage floor", lobby.transform, new Vector3(0, -.15f, 10.5f), new Vector3(4, .3f, 3), concrete);
        Box("Passage west", lobby.transform, new Vector3(-2, 2.4f, 10.5f), new Vector3(.2f, 4.8f, 3), steel);
        Box("Passage east", lobby.transform, new Vector3(2, 2.4f, 10.5f), new Vector3(.2f, 4.8f, 3), steel);
        Box("Lobby floor", lobby.transform, new Vector3(0, -.15f, 0), new Vector3(22, .3f, 18), concrete);
        Box("Lobby ceiling", lobby.transform, new Vector3(0, 4.92f, 0), new Vector3(22, .24f, 18), steel);
        Box("West wall", lobby.transform, new Vector3(-11, 2.4f, 0), new Vector3(.3f, 4.8f, 18), concrete);
        Box("East wall", lobby.transform, new Vector3(11, 2.4f, 0), new Vector3(.3f, 4.8f, 18), concrete);
        Box("South wall", lobby.transform, new Vector3(0, 2.4f, -9), new Vector3(22, 4.8f, .3f), concrete);
        Box("North wall west", lobby.transform, new Vector3(-6.5f, 2.4f, 9), new Vector3(9, 4.8f, .3f), concrete);
        Box("North wall east", lobby.transform, new Vector3(6.5f, 2.4f, 9), new Vector3(9, 4.8f, .3f), concrete);
        Box("North lintel", lobby.transform, new Vector3(0, 4.05f, 9), new Vector3(4, 1.5f, .3f), steel);
        for (int i = -2; i <= 2; i++)
        {
            Box("Fluorescent housing", lobby.transform, new Vector3(i * 4, 4.7f, .5f), new Vector3(2.4f, .15f, .4f), steel, false);
            Box("Fluorescent tube", lobby.transform, new Vector3(i * 4, 4.58f, .5f), new Vector3(2.2f, .08f, .24f), white, false);
            Lamp(lobby.transform, new Vector3(i * 4, 4.25f, 0), new Color(.76f, .89f, 1), 10, 2.3f);
            var pipe = Shape("Exposed pipe", lobby.transform, new Vector3(i * 4, 4.4f, 0), new Vector3(.08f, 8.8f, .08f), rust, PrimitiveType.Cylinder, false); pipe.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        Lamp(lobby.transform, new Vector3(-7, 2.8f, -3), new Color(1, .71f, .39f), 8, 3);
        Lamp(lobby.transform, new Vector3(0, 3, 10.4f), new Color(.76f, .89f, 1), 7, 2);
        Sign("WORKERS' REST ROOM\nKEEP THE AISLES CLEAR", lobby.transform, new Vector3(0, 3.5f, 8.7f), 3.7f, 2.6f);
        Lockers(lobby.transform); Lounge(lobby.transform); Sports(lobby.transform); Board(lobby.transform); Media(lobby.transform); Lift(scene, lobby.transform); Anomaly(scene, lobby.transform);
        foreach (var label in lobby.GetComponentsInChildren<TMP_Text>(true)) label.richText = false;
        typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { scene, false, 0 });
        var serialize = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var obj in Items<NetworkObject>(scene)) serialize.Invoke(obj, new object[] { true, false });
        if (idsBefore.Any(p => (ulong)idField.GetValue(p.Key) != p.Value)) throw new Exception("Existing scene network IDs changed");
        var ids = Items<NetworkObject>(scene).Select(n => (ulong)idField.GetValue(n)).ToArray();
        if (ids.Any(id => id == 0) || ids.Distinct().Count() != ids.Length) throw new Exception("Invalid network IDs");
        Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        string report = "PASS: social lobby created; " + idsBefore.Count + " existing network IDs preserved; " + ids.Length + " unique IDs.\n";
        Directory.CreateDirectory("ArtSource/SocialLobby"); File.WriteAllText("ArtSource/SocialLobby/scene-validation.txt", report); return report;
    }
    private static void Lockers(Transform parent)
    {
        for (int i = 0; i < 6; i++)
        {
            var go = Root("Player Locker " + (i + 1), parent, new Vector3(3 + i * 1.15f, 0, 8.2f));
            Box("Back", go.transform, new Vector3(0, 1.25f, .25f), new Vector3(1, 2.5f, .1f), steel);
            Box("Side L", go.transform, new Vector3(-.5f, 1.25f, 0), new Vector3(.06f, 2.5f, .6f), lockerPaint);
            Box("Side R", go.transform, new Vector3(.5f, 1.25f, 0), new Vector3(.06f, 2.5f, .6f), lockerPaint);
            Box("Top", go.transform, new Vector3(0, 2.48f, 0), new Vector3(1, .06f, .6f), lockerPaint);
            Box("Shelf", go.transform, new Vector3(0, .85f, 0), new Vector3(1, .07f, .6f), steel);
            var door = Root("Hinged door", go.transform, new Vector3(-.5f, 0, -.34f));
            Box("Door", door.transform, new Vector3(.5f, 1.25f, 0), new Vector3(1, 2.5f, .07f), lockerPaint, false);
            Box("Handle", door.transform, new Vector3(.83f, 1.15f, -.08f), new Vector3(.05f, .25f, .05f), steel, false);
            for (int slit = 0; slit < 3; slit++) Box("Vent slit", door.transform, new Vector3(.5f, 2.05f + slit * .08f, -.045f), new Vector3(.55f, .025f, .01f), dark, false);
            var collider = go.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 1.25f, 0); collider.size = new Vector3(1, 2.5f, .65f);
            var locker = Network<PlayerLocker>(go); Set(locker, "slot", i); Set(locker, "door", door.transform);
            Set(locker, "nameplate", Sign("UNASSIGNED", door.transform, new Vector3(.5f, 2.3f, -.06f), .85f, 1.5f));
            for (int slot = 0; slot < 6; slot++)
            {
                Transform mount = slot < 3 ? door.transform : go.transform;
                var position = slot == 0 ? new Vector3(.5f, 1.55f, -.055f) : slot == 1 ? new Vector3(.3f, 1.7f, .055f) : slot == 2 ? new Vector3(.7f, 1.3f, .055f) : new Vector3((slot - 4) * .27f, 1.3f + (slot % 2) * .4f, .18f);
                var image = Shape("Photo Slot " + slot, mount, position, new Vector3(.25f, .18f, 1), white, PrimitiveType.Quad, false);
                if (slot == 1 || slot == 2) image.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var photo = image.AddComponent<LockerPhotoSlot>(); Set(photo, "slot", slot); Set(photo, "frame", image.GetComponent<Renderer>());
            }
        }
        Box("Locker bench", parent, new Vector3(6, .45f, 6.6f), new Vector3(6.6f, .18f, .65f), wood);
    }
    private static void Lounge(Transform parent)
    {
        var sofa = Root("Old worker sofa", parent, new Vector3(-7.4f, 0, -2.5f));
        Box("Seat", sofa.transform, new Vector3(0, .5f, 0), new Vector3(3.6f, .45f, 1.15f), leather);
        Box("Back", sofa.transform, new Vector3(0, 1, .5f), new Vector3(3.6f, .9f, .25f), leather);
        for (int i = -1; i <= 1; i++) Box("Worn cushion", sofa.transform, new Vector3(i * 1.1f, .77f, -.1f), new Vector3(1, .13f, .8f), cloth);
        for (int i = -1; i <= 1; i += 2) Box("Arm", sofa.transform, new Vector3(i * 1.9f, .8f, 0), new Vector3(.3f, .8f, 1.3f), leather);
        var chair = Root("Worker armchair", parent, new Vector3(-8.7f, 0, -6)); chair.transform.localRotation = Quaternion.Euler(0, 35, 0);
        Box("Seat", chair.transform, new Vector3(0, .48f, 0), new Vector3(1.5f, .4f, 1.3f), cloth); Box("Back", chair.transform, new Vector3(0, 1, .5f), new Vector3(1.5f, 1, .3f), leather);
        Box("Coffee table", parent, new Vector3(-6.4f, .65f, -4.4f), new Vector3(2.8f, .16f, 1.3f), wood);
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) Box("Table leg", parent, new Vector3(-6.4f + x, .3f, -4.4f + z * .45f), new Vector3(.12f, .6f, .12f), steel);
        Prop("Can", parent, new Vector3(-6, 1, -4.4f), new Vector3(.12f, .23f, .12f), rust, type: PrimitiveType.Cylinder);
        Prop("Paper cup", parent, new Vector3(-7, 1, -4.5f), new Vector3(.13f, .16f, .13f), white, type: PrimitiveType.Cylinder);
        Prop("Bottle", parent, new Vector3(-5.5f, 1, -4.4f), new Vector3(.12f, .35f, .12f), lockerPaint, type: PrimitiveType.Cylinder);
        Prop("Cushion", parent, new Vector3(-7.5f, 1, -2.6f), new Vector3(.5f, .18f, .5f), cloth);
        Prop("Small box", parent, new Vector3(-5, .3f, -6), new Vector3(.45f, .35f, .4f), wood);
        Prop("Rubber ball", parent, new Vector3(-5, .3f, -5), Vector3.one * .3f, pink, type: PrimitiveType.Sphere);
        Prop("Helmet", parent, new Vector3(-6, .3f, -6), new Vector3(.35f, .25f, .35f), orange, type: PrimitiveType.Sphere);
        Prop("Paper roll", parent, new Vector3(-5.5f, .3f, -6), new Vector3(.2f, .2f, .2f), white, type: PrimitiveType.Cylinder);
        Prop("Tool handle", parent, new Vector3(-6.5f, 1, -4.2f), new Vector3(.3f, .08f, .08f), rust);
    }
    private static void Sports(Transform parent)
    {
        var go = Root("Basketball", parent, new Vector3(9.9f, 0, 2)); go.transform.localRotation = Quaternion.Euler(0, 90, 0);
        Box("Backboard", go.transform, new Vector3(0, 3.2f, .25f), new Vector3(1.8f, 1.2f, .12f), white);
        var hoop = Network<BasketballHoop>(go); Set(hoop, "scoreLabel", Sign("BASKETBALL", go.transform, new Vector3(0, 4, 0), 2, 2.2f));
        for (int i = 0; i < 20; i++)
        {
            float angle = i * Mathf.PI * 2 / 20; var rim = Box("Hoop rim", go.transform, new Vector3(Mathf.Cos(angle) * .35f, 2.75f, -.35f + Mathf.Sin(angle) * .35f), new Vector3(.13f, .045f, .055f), orange);
            rim.transform.localRotation = Quaternion.Euler(0, -i * 18, 0);
        }
        for (int i = 0; i < 2; i++)
        {
            var gate = Root(i == 0 ? "Upper scoring gate" : "Lower scoring gate", go.transform, new Vector3(0, i == 0 ? 2.97f : 2.45f, -.35f));
            var collider = gate.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = new Vector3(.58f, .09f, .58f);
            var score = gate.AddComponent<BasketballScoreGate>(); Set(score, "hoop", hoop); Set(score, "upper", i == 0);
        }
        for (int i = 0; i < 3; i++) Prop("Basketball " + (i + 1), parent, new Vector3(8 + i * .65f, .35f, 4.5f), Vector3.one * .3f, orange, LobbyPropKind.Basketball, PrimitiveType.Sphere);
        for (int i = 0; i < 24; i++)
        {
            float angle = i * Mathf.PI * 2 / 24; Box("Faded basketball line", parent, new Vector3(9.2f + Mathf.Cos(angle) * 2, .007f, 2 + Mathf.Sin(angle) * 2), new Vector3(.24f, .008f, .05f), white, false).transform.localRotation = Quaternion.Euler(0, -i * 15, 0);
        }
        var dartGo = Root("Darts", parent, new Vector3(10.7f, 1.7f, -6.5f)); dartGo.transform.localRotation = Quaternion.Euler(0, 90, 0);
        Shape("Target", dartGo.transform, Vector3.zero, new Vector3(1.1f, 1.1f, .07f), wood, PrimitiveType.Cylinder, false).transform.localRotation = Quaternion.Euler(90, 0, 0);
        var hitbox = dartGo.AddComponent<BoxCollider>(); hitbox.size = new Vector3(1.1f, 1.1f, .1f);
        var board = Network<Dartboard>(dartGo); Set(board, "scoreLabel", Sign("DARTS", dartGo.transform, new Vector3(0, 1, -.08f), 2.2f, 2));
        for (int i = 0; i < 20; i++)
        {
            float angle = i * Mathf.PI * 2 / 20;
            var sector = Box("Target sector", dartGo.transform, new Vector3(Mathf.Sin(angle) * .35f, Mathf.Cos(angle) * .35f, -.063f), new Vector3(.055f, .45f, .01f), i % 2 == 0 ? dark : white, false); sector.transform.localRotation = Quaternion.Euler(0, 0, -i * 18);
        }
        Shape("Bull", dartGo.transform, new Vector3(0, 0, -.08f), new Vector3(.06f, .06f, .012f), pink, PrimitiveType.Sphere, false);
        for (int i = 0; i < 4; i++) Prop("Dart " + (i + 1), parent, new Vector3(9.4f, 1.2f, -6 + i * .15f), new Vector3(.045f, .045f, .25f), orange, LobbyPropKind.Dart);
        Box("Dart shelf", parent, new Vector3(9.6f, .95f, -6), new Vector3(.8f, .08f, 1.2f), wood);
        Hockey(parent);
    }
    private static void Hockey(Transform parent)
    {
        var go = Root("Air hockey", parent, new Vector3(4.5f, 0, -3.5f));
        Box("Table base", go.transform, new Vector3(0, .45f, 0), new Vector3(2.9f, .9f, 4.9f), steel);
        Box("Playing surface", go.transform, new Vector3(0, 1.01f, 0), new Vector3(2.7f, .08f, 4.7f), white);
        for (int side = -1; side <= 1; side += 2)
        {
            Box("Side rail", go.transform, new Vector3(side * 1.4f, 1.2f, 0), new Vector3(.1f, .3f, 4.8f), orange);
            for (int end = -1; end <= 1; end += 2) Box("End rail", go.transform, new Vector3(side * .95f, 1.2f, end * 2.4f), new Vector3(.9f, .3f, .1f), steel);
        }
        var box = go.AddComponent<BoxCollider>(); box.center = new Vector3(0, .5f, 0); box.size = new Vector3(2.9f, 1, 4.9f);
        var table = Network<AirHockeyTable>(go); Sign("AIR HOCKEY", go.transform, new Vector3(0, .6f, -2.51f), 2.4f, 2.2f);
        var puck = Prop("Hockey puck", parent, go.transform.localPosition + new Vector3(0, 1.14f, 0), new Vector3(.24f, .1f, .24f), dark, LobbyPropKind.Puck, PrimitiveType.Cylinder);
        Set(table, "puck", puck);
        for (int i = 0; i < 2; i++)
        {
            var paddle = Shape("Paddle", go.transform, new Vector3(0, 1.15f, i == 0 ? -1.3f : 1.3f), new Vector3(.4f, .12f, .4f), i == 0 ? pink : lockerPaint, PrimitiveType.Cylinder);
            var rigidbody = paddle.AddComponent<Rigidbody>(); rigidbody.isKinematic = true; Set(table, i == 0 ? "paddleA" : "paddleB", rigidbody);
            var goal = Root("Goal", go.transform, new Vector3(0, 1.2f, i == 0 ? -2.5f : 2.5f));
            var trigger = goal.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(.95f, .4f, .3f);
            var score = goal.AddComponent<AirHockeyGoal>(); Set(score, "table", table); Set(score, "sideA", i == 0);
        }
    }
    private static void Board(Transform parent)
    {
        var go = Root("Shared drawing board", parent, new Vector3(0, 1.8f, -8.65f));
        Box("Frame", go.transform, Vector3.zero, new Vector3(4.8f, 2.5f, .12f), wood);
        var surface = Shape("Canvas", go.transform, new Vector3(0, 0, -.08f), new Vector3(4.5f, 2.2f, 1), white, PrimitiveType.Quad, false);
        go.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(4.8f, 2.5f, .18f);
        var board = Network<DrawingBoard>(go); Set(board, "surface", surface.GetComponent<Renderer>());
    }
    private static void Media(Transform parent)
    {
        var go = Root("Worker TV", parent, new Vector3(-10.6f, 1.9f, -4.3f)); go.transform.localRotation = Quaternion.Euler(0, -90, 0);
        Box("TV casing", go.transform, Vector3.zero, new Vector3(1.9f, 1.35f, .4f), steel);
        var screen = Shape("TV screen", go.transform, new Vector3(0, .08f, -.22f), new Vector3(1.65f, .94f, 1), dark, PrimitiveType.Quad, false);
        var hitbox = go.AddComponent<BoxCollider>(); hitbox.size = new Vector3(1.9f, 1.35f, .5f);
        go.AddComponent<AudioSource>(); go.AddComponent<VideoPlayer>(); var television = Network<LobbyTelevision>(go);
        Set(television, "speaker", go.GetComponent<AudioSource>()); Set(television, "screen", screen.GetComponent<Renderer>());
        Sign("TV · DIRECT VIDEO URL", go.transform, new Vector3(0, -.53f, -.23f), 1.8f, 1.2f);
        var music = Root("Factory jukebox", parent, new Vector3(-4.8f, 0, -7.8f));
        Box("Cabinet", music.transform, new Vector3(0, .95f, 0), new Vector3(1.3f, 1.9f, .6f), rust);
        Box("Speaker grille", music.transform, new Vector3(0, .65f, -.32f), new Vector3(.95f, 1, .04f), steel);
        for (int i = 0; i < 8; i++) Box("Grille", music.transform, new Vector3(0, .26f + i * .11f, -.35f), new Vector3(.8f, .025f, .01f), dark, false);
        Box("Neon left", music.transform, new Vector3(-.6f, 1, -.34f), new Vector3(.055f, 1.8f, .03f), pink, false);
        Box("Neon right", music.transform, new Vector3(.6f, 1, -.34f), new Vector3(.055f, 1.8f, .03f), pink, false);
        Box("Neon top", music.transform, new Vector3(0, 1.85f, -.34f), new Vector3(1.2f, .055f, .03f), pink, false);
        music.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var collider = music.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 1, 0); collider.size = new Vector3(1.3f, 2, .65f);
        music.AddComponent<AudioSource>(); var jukebox = Network<LobbyJukebox>(music); Set(jukebox, "speaker", music.GetComponent<AudioSource>());
        Sign("JUKEBOX", music.transform, new Vector3(0, 1.55f, -.35f), 1, 1.7f);
    }
    private static void Lift(Scene scene, Transform parent)
    {
        var station = Items<RoundFinishStation>(scene).Single();
        foreach (var child in station.GetComponentsInChildren<Transform>(true).Where(t => t.parent == station.transform).ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        foreach (var collider in station.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        station.name = "Freight lift · final show"; station.transform.SetParent(parent, false); station.transform.localPosition = new Vector3(-7, 0, 6.6f); station.transform.localRotation = Quaternion.identity; station.transform.localScale = Vector3.one;
        var go = station.gameObject;
        Box("Lift floor", go.transform, new Vector3(0, .02f, 0), new Vector3(6.2f, .04f, 4.2f), steel);
        Box("Lift rear", go.transform, new Vector3(0, 1.65f, 2), new Vector3(6.2f, 3.3f, .15f), steel);
        Box("Lift left", go.transform, new Vector3(-3.1f, 1.65f, 0), new Vector3(.15f, 3.3f, 4.2f), steel);
        Box("Lift right", go.transform, new Vector3(3.1f, 1.65f, 0), new Vector3(.15f, 3.3f, 4.2f), steel);
        Box("Lift ceiling", go.transform, new Vector3(0, 3.3f, 0), new Vector3(6.2f, .15f, 4.2f), steel);
        var left = Box("Left freight door", go.transform, new Vector3(-4.125f, 1.6f, -2), new Vector3(2.75f, 3.2f, .1f), rust, false);
        var right = Box("Right freight door", go.transform, new Vector3(4.125f, 1.6f, -2), new Vector3(2.75f, 3.2f, .1f), rust, false);
        for (int i = 0; i < 12; i++) Box("Lift threshold stripe", go.transform, new Vector3(-2.8f + i * .5f, .065f, -1.9f), new Vector3(.25f, .025f, .25f), i % 2 == 0 ? orange : dark, false);
        var elevator = Network<LobbyFreightElevator>(go); Set(elevator, "leftDoor", left.transform); Set(elevator, "rightDoor", right.transform);
        Set(elevator, "cabinLight", Lamp(go.transform, new Vector3(0, 3, 0), new Color(1, .83f, .58f), 7, 2));
        Set(elevator, "sign", Sign("FINAL SHOW\nENTER TOGETHER", go.transform, new Vector3(0, 3.9f, -2), 5, 3.2f));
        Set(station, "zoneCenterOffset", Vector3.zero);
    }
    private static void Anomaly(Scene scene, Transform parent)
    {
        var go = Root("Lobby anomalies", parent, Vector3.zero); var manager = Network<LobbyAnomalyManager>(go);
        var eventGo = Root("Quiet mannequin", go.transform, new Vector3(-9.2f, 0, 4.4f));
        var original = Items<ThrowableMannequin>(scene).First();
        // Clone only the visible model, never its AI, physics or network scripts.
        var animator = original.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            var visual = UnityEngine.Object.Instantiate(animator.gameObject, eventGo.transform);
            visual.transform.localPosition = Vector3.zero;
            for (int pass = 0; pass < 2; pass++) foreach (var behavior in visual.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) UnityEngine.Object.DestroyImmediate(behavior);
            foreach (var audio in visual.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(audio);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var animation in visual.GetComponentsInChildren<Animator>(true)) animation.enabled = false;
        }
        else Shape("Mannequin silhouette", eventGo.transform, Vector3.up, new Vector3(.5f, 1, .5f), white, PrimitiveType.Capsule, false);
        var anomaly = go.AddComponent<LobbyMannequinAnomaly>(); Set(anomaly, "mannequin", eventGo.transform);
        var data = new SerializedObject(anomaly); var positions = data.FindProperty("positions"); positions.arraySize = 4;
        Vector3[] spots = { new Vector3(-9.2f, 0, 4.4f), new Vector3(-8.7f, 0, -1.6f), new Vector3(9.8f, 0, 7), new Vector3(-3.4f, 0, 5) };
        for (int i = 0; i < 4; i++) { var spot = Root("Anomaly position " + i, go.transform, spots[i]); spot.transform.localRotation = Quaternion.Euler(0, 90 * i, 0); positions.GetArrayElementAtIndex(i).objectReferenceValue = spot.transform; }
        data.ApplyModifiedPropertiesWithoutUndo(); var managerData = new SerializedObject(manager); var events = managerData.FindProperty("events"); events.arraySize = 1; events.GetArrayElementAtIndex(0).objectReferenceValue = anomaly; managerData.ApplyModifiedPropertiesWithoutUndo();
    }
}
