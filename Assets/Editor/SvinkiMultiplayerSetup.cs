#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SvinkiMultiplayerSetup
{
    private const string GameScene = "Assets/Scenes/SampleScene.unity";
    private const string LobbyScene = "Assets/Scenes/Lobby.unity";
    private const string PlayerPrefab = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string ManagerPrefab = "Assets/Prefabs/SvinkiNetworkManager.prefab";

    [MenuItem("Svinki/Prepare multiplayer scenes")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play Mode first."); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        Scene game = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

        NetworkManager manager = FindInScene<NetworkManager>(game);
        GrayboxPlayerController player = FindInScene<GrayboxPlayerController>(game);
        if (manager == null || player == null)
        {
            Debug.LogError("SampleScene needs the original NetworkManager and Player. Setup may already be complete.");
            return;
        }

        // Keep the existing cylinder, movement settings and collider as the player prefab.
        PreparePrefabInstance(player.gameObject);
        if (player.GetComponent<NetworkObject>() == null) player.gameObject.AddComponent<NetworkObject>();
        NetworkTransform playerTransform = player.GetComponent<NetworkTransform>();
        if (playerTransform == null) playerTransform = player.gameObject.AddComponent<NetworkTransform>();
        SetServerAuthority(playerTransform);
        NetworkPlayer networkPlayer = player.GetComponent<NetworkPlayer>();
        if (networkPlayer == null) networkPlayer = player.gameObject.AddComponent<NetworkPlayer>();
        List<ClothingDefinition> definitions = new List<ClothingDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<ClothingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null) definitions.Add(definition);
        }
        SerializedObject playerData = new SerializedObject(networkPlayer);
        SerializedProperty catalog = playerData.FindProperty("clothingCatalog");
        catalog.arraySize = definitions.Count;
        for (int i = 0; i < definitions.Count; i++) catalog.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
        playerData.ApplyModifiedPropertiesWithoutUndo();
        GameObject playerAsset = PrefabUtility.SaveAsPrefabAsset(player.gameObject, PlayerPrefab);
        Object.DestroyImmediate(player.gameObject);

        foreach (ClothingPickup clothing in FindAllInScene<ClothingPickup>(game))
        {
            GameObject pickup = clothing.gameObject;
            if (pickup.GetComponent<NetworkObject>() == null) pickup.AddComponent<NetworkObject>();
            if (pickup.GetComponent<NetworkPickup>() == null) pickup.AddComponent<NetworkPickup>();
        }

        MannequinBrain mannequin = FindInScene<MannequinBrain>(game);
        if (mannequin != null)
        {
            PreparePrefabInstance(mannequin.gameObject);
            if (mannequin.GetComponent<NetworkObject>() == null) mannequin.gameObject.AddComponent<NetworkObject>();
            NetworkTransform mannequinTransform = mannequin.GetComponent<NetworkTransform>();
            if (mannequinTransform == null) mannequinTransform = mannequin.gameObject.AddComponent<NetworkTransform>();
            SetServerAuthority(mannequinTransform);
            if (mannequin.GetComponent<NetworkMannequinAuthority>() == null)
                mannequin.gameObject.AddComponent<NetworkMannequinAuthority>();
            Animator animator = mannequin.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var networkAnimator = mannequin.GetComponent<FishNet.Component.Animating.NetworkAnimator>();
                if (networkAnimator == null)
                    networkAnimator = mannequin.gameObject.AddComponent<FishNet.Component.Animating.NetworkAnimator>();
                networkAnimator.SetAnimator(animator);
            }
        }

        GameObject physics = new GameObject("Room Physics");
        physics.AddComponent<RoomPhysics>();
        SceneManager.MoveGameObjectToScene(physics, game);

        PreparePrefabInstance(manager.gameObject);
        foreach (PlayerSpawner spawner in manager.GetComponentsInChildren<PlayerSpawner>(true))
            Object.DestroyImmediate(spawner);
        foreach (Transform child in manager.GetComponentsInChildren<Transform>(true))
            if (child != null && child != manager.transform && child.name == "NetworkHudCanvas")
                Object.DestroyImmediate(child.gameObject);
        NetworkLobby lobby = manager.GetComponent<NetworkLobby>();
        if (lobby == null) lobby = manager.gameObject.AddComponent<NetworkLobby>();
        SerializedObject managerData = new SerializedObject(manager);
        SerializedProperty persistent = managerData.FindProperty("_dontDestroyOnLoad");
        if (persistent != null) persistent.boolValue = true;
        managerData.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject lobbyData = new SerializedObject(lobby);
        lobbyData.FindProperty("playerPrefab").objectReferenceValue = playerAsset.GetComponent<NetworkObject>();
        lobbyData.ApplyModifiedPropertiesWithoutUndo();
        GameObject managerAsset = PrefabUtility.SaveAsPrefabAsset(manager.gameObject, ManagerPrefab);
        Object.DestroyImmediate(manager.gameObject);
        EditorSceneManager.SaveScene(game);

        Scene lobbyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject lobbyCamera = new GameObject("Lobby Camera");
        Camera camera = lobbyCamera.AddComponent<Camera>();
        camera.depth = -10f;
        camera.backgroundColor = new Color(0.07f, 0.09f, 0.14f);
        PrefabUtility.InstantiatePrefab(managerAsset, lobbyScene);
        EditorSceneManager.SaveScene(lobbyScene, LobbyScene);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(LobbyScene, true),
            new EditorBuildSettingsScene(GameScene, true)
        };
        RebuildSceneIds();
        AssetDatabase.SaveAssets();
        Debug.Log("Multiplayer scenes prepared. Open Lobby and press Play.");
    }

    [MenuItem("Svinki/Rebuild FishNet scene IDs")]
    public static void RebuildSceneIds()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play Mode first."); return; }
        string original = SceneManager.GetActiveScene().path;
        MethodInfo create = typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo serialize = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", BindingFlags.Instance | BindingFlags.NonPublic);
        if (create == null || serialize == null) { Debug.LogError("FishNet scene ID API changed."); return; }
        foreach (string path in new[] { GameScene, LobbyScene })
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            object[] args = { scene, true, 0 };
            var objects = (List<NetworkObject>)create.Invoke(null, args);
            foreach (NetworkObject networkObject in objects)
                serialize.Invoke(networkObject, new object[] { true, false });
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"FishNet scene IDs: {path}: {args[2]} assigned.");
        }
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
    }

    [MenuItem("Svinki/Build macOS test client")]
    public static void BuildMacTestClient()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Stop Play Mode first."); return; }
        string destination = Path.Combine(Path.GetTempPath(), "SvinkiMacTest.app");
        var options = new BuildPlayerOptions
        {
            scenes = new[] { LobbyScene, GameScene },
            locationPathName = destination,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Mac test client: {report.summary.result}, {destination}");
    }

    private static void SetServerAuthority(NetworkTransform component)
    {
        SerializedObject data = new SerializedObject(component);
        data.FindProperty("_clientAuthoritative").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PreparePrefabInstance(GameObject gameObject)
    {
        GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
        if (outer != null)
            PrefabUtility.UnpackPrefabInstance(outer, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T item in root.GetComponentsInChildren<T>(true)) return item;
        return null;
    }

    private static IEnumerable<T> FindAllInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T item in root.GetComponentsInChildren<T>(true)) yield return item;
    }
}
#endif
