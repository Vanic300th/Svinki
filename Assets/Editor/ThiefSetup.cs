using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tools > Mannequin:
///  • Build Thief Prefab — Mannequin_Thief: самостоятельный префаб (копия обычного манекена, не вариант).
///                         Маленький (×0.6), голова крупнее, вместо мозга сталкера — ThiefBrain.
///  • Place Thief        — готовит открытую сцену и ставит Воришку у его гнезда:
///                         игроку PlayerAvatar + PlayerOutfit, HUD-манекену — ссылку на комплект,
///                         коллайдеры вещей — в триггеры, гнездо ThiefNest с коробками в дальнем углу.
/// </summary>
public static class ThiefSetup
{
    public const string ThiefPrefabPath = "Assets/Prefabs/Mannequin_Thief.prefab";
    private const string BasePrefabPath = "Assets/Prefabs/Mannequin.prefab";
    private const string NestName = "ThiefNest";
    private const string BoxMaterialPath = "Assets/Materials/Graybox Obstacles.mat";
    private const float FullHeight = 1.8f;  // рост обычного манекена
    private const float BodyScale = 0.6f;   // Воришка — 0.6 от него
    private const float HeadScale = 1.25f;  // и с крупной головой
    private const string GrabClip = "Interact"; // движение рукой вперёд: вещи висят в воздухе

    [MenuItem("Tools/Mannequin/Build Thief Prefab")]
    public static void BuildThiefPrefabMenu() => BuildThiefPrefab();

    /// <summary>
    /// Собирает префаб заново из Mannequin.prefab. Настройки Воришки в Inspector при этом сбросятся.
    /// </summary>
    public static GameObject BuildThiefPrefab()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Thief] Сначала останови Play Mode.");
            return null;
        }

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (basePrefab == null) basePrefab = MannequinSetup.BuildPrefab();
        if (basePrefab == null) return null;

        // Собираем во временной сцене, чтобы не трогать открытую
        var temp = EditorSceneManager.NewPreviewScene();
        GameObject prefab = null;
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, temp);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            instance.name = "Mannequin_Thief";

            // Мозг сталкера не нужен: у Воришки свой
            var stalkerBrain = instance.GetComponent<MannequinBrain>();
            if (stalkerBrain != null) Object.DestroyImmediate(stalkerBrain);

            Transform model = instance.transform.Find("Model");
            if (model == null)
            {
                Debug.LogError("[Thief] В Mannequin.prefab нет дочернего объекта Model — пересобери его (Tools > Mannequin > Build Prefab).");
                return null;
            }

            // Маленький: модель ×0.6 (смещение тоже, чтобы ноги остались на полу)
            model.localPosition *= BodyScale;
            model.localScale *= BodyScale;

            float height = FullHeight * BodyScale;
            var capsule = instance.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.height = height;
                capsule.radius = 0.2f;
                capsule.center = new Vector3(0f, height / 2f, 0f);
            }
            var agent = instance.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.radius = 0.22f;
                agent.height = height;
                agent.speed = 5.5f;
                agent.acceleration = 40f;
                agent.angularSpeed = 720f;
                agent.stoppingDistance = 0.2f;
                agent.autoBraking = true;
            }

            // Детские пропорции: голова крупнее
            Transform head = FindDeep(model, "Head");
            Transform hand = FindDeep(model, "hand_r");
            if (head != null)
            {
                var scaler = instance.AddComponent<BoneScaler>();
                var sso = new SerializedObject(scaler);
                SerializedProperty bones = sso.FindProperty("bones");
                bones.arraySize = 1;
                SerializedProperty entry = bones.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("bone").objectReferenceValue = head;
                entry.FindPropertyRelative("scale").vector3Value = Vector3.one * HeadScale;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("[Thief] Не нашёл кость Head — голова останется обычной.");

            // Куда класть украденное: в правую руку, шапку — на макушку
            Transform handSocket = null;
            if (hand != null)
            {
                handSocket = new GameObject("HandSocket").transform;
                handSocket.SetParent(hand, false);
            }
            Transform headSocket = null;
            if (head != null)
            {
                headSocket = new GameObject("HeadSocket").transform;
                headSocket.position = new Vector3(head.position.x, HeadTop(model, head), head.position.z);
                headSocket.rotation = model.rotation;
                headSocket.SetParent(head, true); // при масштабе головы ×1.25 сокет поднимется вместе с макушкой
            }

            // «Атака» Воришки — это движение рукой за вещью
            var anim = instance.GetComponent<MannequinAnimator>();
            if (anim != null)
            {
                var aso = new SerializedObject(anim);
                aso.FindProperty("attackClip").stringValue = GrabClip;
                aso.ApplyModifiedPropertiesWithoutUndo();
            }

            var thief = instance.AddComponent<ThiefBrain>();
            var tso = new SerializedObject(thief);
            tso.FindProperty("handSocket").objectReferenceValue = handSocket;
            tso.FindProperty("headSocket").objectReferenceValue = headSocket;
            tso.FindProperty("bodyScale").floatValue = BodyScale;
            tso.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            prefab = PrefabUtility.SaveAsPrefabAsset(instance, ThiefPrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(temp);
        }

        AssetDatabase.SaveAssets();
        if (prefab != null) Debug.Log($"[Thief] Воришка готов (самостоятельный префаб): {ThiefPrefabPath}", prefab);
        return prefab;
    }

    [MenuItem("Tools/Mannequin/Place Thief")]
    public static void PlaceThief()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Thief] Сначала останови Play Mode.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThiefPrefabPath);
        if (prefab == null) prefab = BuildThiefPrefab();
        if (prefab == null) return;
        PlaceThiefInScene(prefab);
    }

    /// <summary>
    /// Ставит Воришку (обычного или онлайн-префаб) в открытую сцену у гнезда и готовит сцену под него.
    /// </summary>
    public static GameObject PlaceThiefInScene(GameObject prefab)
    {
        // Воришке нужен NavMesh
        if (Object.FindAnyObjectByType<Unity.AI.Navigation.NavMeshSurface>() == null) MannequinSetup.SetupNavMesh();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Place Thief");
        int undoGroup = Undo.GetCurrentGroup();

        // 1) Игрок: PlayerAvatar (его видят манекены) + PlayerOutfit (что он собрал)
        var controller = Object.FindAnyObjectByType<GrayboxPlayerController>();
        Transform player = controller != null ? controller.transform : null;
        PlayerOutfit outfit = null;
        if (controller != null)
        {
            outfit = controller.GetComponent<PlayerOutfit>();
            if (outfit == null) outfit = Undo.AddComponent<PlayerOutfit>(controller.gameObject);
            var avatar = controller.GetComponent<PlayerAvatar>();
            if (avatar == null) avatar = Undo.AddComponent<PlayerAvatar>(controller.gameObject);
            if (Camera.main != null)
            {
                var avso = new SerializedObject(avatar);
                SerializedProperty eyes = avso.FindProperty("eyeCamera");
                if (eyes.objectReferenceValue == null)
                {
                    eyes.objectReferenceValue = Camera.main;
                    avso.ApplyModifiedProperties();
                }
            }
        }
        else Debug.Log("[Thief] В сцене нет игрока (GrayboxPlayerController). Для уровня онлайна это нормально — игроки появятся из лобби.");

        // 2) Вещи: коллайдеры в триггеры — подбор работает, а взгляд сквозь них проходит
        int triggers = 0;
        foreach (ClothingPickup pickup in Object.FindObjectsByType<ClothingPickup>(FindObjectsInactive.Include))
        {
            foreach (Collider col in pickup.GetComponents<Collider>())
            {
                if (col.isTrigger) continue;
                Undo.RecordObject(col, "Pickup collider to trigger");
                col.isTrigger = true;
                triggers++;
            }
        }

        // 3) HUD-манекен показывает комплект игрока
        if (outfit != null)
        {
            foreach (MannequinWardrobe wardrobe in Object.FindObjectsByType<MannequinWardrobe>(FindObjectsInactive.Include))
            {
                var wso = new SerializedObject(wardrobe);
                wso.FindProperty("outfit").objectReferenceValue = outfit;
                wso.ApplyModifiedProperties();
            }
        }

        // 4) Гнездо
        GameObject nest = GameObject.Find(NestName);
        bool nestCreated = nest == null;
        if (nestCreated) nest = CreateNest(FindNestPosition(player), player);

        // 5) Воришка — рядом с гнездом, лицом к игроку
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place Thief");
        Vector3 pos = nest.transform.position;
        Vector3 toPlayer = player != null ? player.position - pos : Vector3.back;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f) pos += toPlayer.normalized * 1.2f;
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas)) pos = hit.position;
        instance.transform.position = pos;
        if (toPlayer.sqrMagnitude > 0.01f) instance.transform.rotation = Quaternion.LookRotation(toPlayer);

        var thief = instance.GetComponent<ThiefBrain>();
        if (thief != null)
        {
            var tso = new SerializedObject(thief);
            tso.FindProperty("nest").objectReferenceValue = nest.transform;
            tso.ApplyModifiedProperties();
        }

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);
        Debug.Log($"[Thief] Воришка поставлен в {pos}, гнездо {(nestCreated ? "создано" : "уже было")}: {nest.transform.position}. " +
                  $"Коллайдеров вещей переведено в триггеры: {triggers}. Сохрани сцену (Ctrl+S) и жми Play.", instance);
        return instance;
    }

    // ------------------------------------------------------------------

    /// <summary>Дальний от игрока угол уровня (Graybox Level), на NavMesh.</summary>
    private static Vector3 FindNestPosition(Transform player)
    {
        Vector3 from = player != null ? player.position : Vector3.zero;
        GameObject level = GameObject.Find("Graybox Level");
        Renderer[] renderers = level != null ? level.GetComponentsInChildren<Renderer>() : new Renderer[0];
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            const float inset = 2f;
            Vector3 best = from;
            float bestSqr = -1f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = new Vector3(sx < 0 ? b.min.x + inset : b.max.x - inset, b.min.y,
                                             sz < 0 ? b.min.z + inset : b.max.z - inset);
                if (!NavMesh.SamplePosition(corner, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
                float sqr = (hit.position - from).sqrMagnitude;
                if (sqr > bestSqr) { bestSqr = sqr; best = hit.position; }
            }
            if (bestSqr >= 0f) return best;
        }

        Vector3 fallback = player != null ? player.position + player.forward * 10f : new Vector3(0f, 0f, 10f);
        if (NavMesh.SamplePosition(fallback, out NavMeshHit fallbackHit, 10f, NavMesh.AllAreas)) fallback = fallbackHit.position;
        return fallback;
    }

    /// <summary>Пустой объект-гнездо и три коробки за ним (с дальней от игрока стороны).</summary>
    private static GameObject CreateNest(Vector3 position, Transform player)
    {
        var nest = new GameObject(NestName);
        Undo.RegisterCreatedObjectUndo(nest, "Create Thief Nest");
        nest.transform.position = position;

        Vector3 away = player != null ? position - player.position : Vector3.forward;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;

        Material boxMaterial = AssetDatabase.LoadAssetAtPath<Material>(BoxMaterialPath);
        float[] angles = { -50f, 0f, 50f };
        float[] sizes = { 0.7f, 0.9f, 0.6f };
        for (int i = 0; i < angles.Length; i++)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Nest Box " + (i + 1);
            box.transform.SetParent(nest.transform, false);
            Vector3 dir = Quaternion.Euler(0f, angles[i], 0f) * away;
            float size = sizes[i];
            box.transform.position = position + dir * 1.5f + Vector3.up * (size * 0.5f);
            box.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 15f * (i - 1), 0f);
            box.transform.localScale = Vector3.one * size;
            if (boxMaterial != null) box.GetComponent<Renderer>().sharedMaterial = boxMaterial;
        }
        return nest;
    }

    /// <summary>Высота макушки: верх модели над головой. Если границы модели с запасом — оценка от кости Head.</summary>
    private static float HeadTop(Transform model, Transform head)
    {
        float guess = head.position.y + 0.2f * BodyScale;
        float top = float.NegativeInfinity;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) top = Mathf.Max(top, r.bounds.max.y);
        return top > head.position.y && top < guess + 0.08f ? top : guess;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
