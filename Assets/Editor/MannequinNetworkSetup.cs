using FishNet.Component.Transforming;
using FishNet.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tools > Mannequin > Online:
///  • Build Online Prefabs — сетевые копии манекенов в Assets/Prefabs/Online
///    (обычный префаб + NetworkObject и сетевые компоненты). Настраивай обычные префабы,
///    а онлайн-копии пересобирай этой кнопкой — настройки скопируются.
///    Обычные префабы остаются без сети: так testroom работает как песочница без лобби.
///  • Place In Level — ставит онлайн-манекенов в открытую сцену-уровень (ту, что лобби грузит как комнату):
///    NavMesh, Сталкер, Подглядывающий, Воришка с гнездом. Потом сохраняет сцену и обновляет scene IDs FishNet.
/// </summary>
public static class MannequinNetworkSetup
{
    public enum Kind { Stalker, Watcher, Thief }

    private const string OnlineFolder = "Assets/Prefabs/Online";
    private static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, -3f); // где появляются игроки (NetworkLobby)

    private static readonly (Kind kind, string source, string online)[] Prefabs =
    {
        (Kind.Stalker, "Assets/Prefabs/Mannequin.prefab", OnlineFolder + "/Mannequin_Online.prefab"),
        (Kind.Watcher, "Assets/Prefabs/Mannequin_Watcher.prefab", OnlineFolder + "/Mannequin_Watcher_Online.prefab"),
        (Kind.Thief, ThiefSetup.ThiefPrefabPath, OnlineFolder + "/Mannequin_Thief_Online.prefab"),
    };

    [MenuItem("Tools/Mannequin/Online/Build Online Prefabs")]
    public static void BuildOnlinePrefabsMenu() => BuildOnlinePrefabs();

    public static int BuildOnlinePrefabs()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Online] Сначала останови Play Mode.");
            return 0;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(OnlineFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Online");

        int built = 0;
        foreach (var (kind, source, online) in Prefabs)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (sourcePrefab == null)
            {
                Debug.LogWarning($"[Online] Нет {source} — пропускаю. Собери его через Tools > Mannequin.");
                continue;
            }

            // Собираем во временной сцене, чтобы не трогать открытую
            var temp = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, temp);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                instance.name = System.IO.Path.GetFileNameWithoutExtension(online);
                AddNetworking(instance, kind);
                PrefabUtility.SaveAsPrefabAsset(instance, online);
                built++;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(temp);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Online] Онлайн-префабов собрано: {built} (папка {OnlineFolder}).");
        return built;
    }

    [MenuItem("Tools/Mannequin/Online/Place In Level")]
    public static void PlaceInLevel()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Online] Сначала останови Play Mode.");
            return;
        }
        var scene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[Online] Сначала сохрани сцену уровня.");
            return;
        }

        GameObject stalker = LoadOnline(Kind.Stalker);
        GameObject watcher = LoadOnline(Kind.Watcher);
        GameObject thief = LoadOnline(Kind.Thief);
        if (stalker == null || watcher == null || thief == null)
        {
            BuildOnlinePrefabs();
            stalker = LoadOnline(Kind.Stalker);
            watcher = LoadOnline(Kind.Watcher);
            thief = LoadOnline(Kind.Thief);
        }

        // Манекенам нужен NavMesh (пересобирается при каждом запуске — NavMeshAutoBake)
        if (Object.FindAnyObjectByType<NavMeshSurface>() == null) MannequinSetup.SetupNavMesh();

        if (stalker != null) Place(stalker, new Vector3(3f, 0f, 6f), 0f);
        if (watcher != null) Place(watcher, new Vector3(-3f, 0f, 6f), 90f); // боком — видно, как голова поворачивается
        if (thief != null) ThiefSetup.PlaceThiefInScene(thief);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // Новым сетевым объектам сцены нужны ID FishNet (скрипт напарника: SampleScene и Lobby)
        SvinkiMultiplayerSetup.RebuildSceneIds();
        Debug.Log("[Online] Манекены расставлены и сцена сохранена. Проверка: открой Lobby → Play → " +
                  "«Локальный сервер для проверки» → «Создать комнату».");
    }

    // ------------------------------------------------------------------

    private static GameObject LoadOnline(Kind kind)
    {
        foreach (var (k, _, online) in Prefabs)
            if (k == kind) return AssetDatabase.LoadAssetAtPath<GameObject>(online);
        return null;
    }

    private static void Place(GameObject prefab, Vector3 position, float extraYaw)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.GetActiveScene());
        Undo.RegisterCreatedObjectUndo(instance, "Place Online Mannequin");
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 10f, NavMesh.AllAreas)) position = hit.position;
        instance.transform.position = position;
        Vector3 look = PlayerSpawn - position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
            instance.transform.rotation = Quaternion.LookRotation(look) * Quaternion.Euler(0f, extraYaw, 0f);
    }

    /// <summary>Сетевые компоненты для своего типа манекена. Сначала NetworkObject, потом остальное.</summary>
    private static void AddNetworking(GameObject root, Kind kind)
    {
        if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
        switch (kind)
        {
            case Kind.Stalker:
                AddServerTransform(root);
                Ensure<NetworkMannequinAnimation>(root);
                Ensure<NetworkMannequinAuthority>(root);
                break;
            case Kind.Watcher:
                Ensure<NetworkHeadWatcher>(root); // стоит на месте — позицию синхронизировать не нужно
                break;
            case Kind.Thief:
                AddServerTransform(root);
                Ensure<NetworkMannequinAnimation>(root);
                Ensure<NetworkThief>(root);
                break;
        }
    }

    private static void Ensure<T>(GameObject root) where T : Component
    {
        if (root.GetComponent<T>() == null) root.AddComponent<T>();
    }

    // Позицию двигает только сервер (как у игрока в SvinkiMultiplayerSetup)
    private static void AddServerTransform(GameObject root)
    {
        NetworkTransform networkTransform = root.GetComponent<NetworkTransform>();
        if (networkTransform == null) networkTransform = root.AddComponent<NetworkTransform>();
        var data = new SerializedObject(networkTransform);
        SerializedProperty clientAuthority = data.FindProperty("_clientAuthoritative");
        if (clientAuthority != null) clientAuthority.boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
