using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(PlayerOutfit))]
public sealed class PlayerOutfitEditor : Editor
{
    [MenuItem("Svinki/Одежда/Открыть Player Outfit")]
    private static void OpenPlayerOutfit()
    {
        const string path = "Assets/Prefabs/NetworkPlayer.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogError("Не найден префаб игрока: " + path);
            return;
        }

        PrefabStageUtility.OpenPrefab(path);
        EditorApplication.delayCall += () =>
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                Selection.activeObject = stage.prefabContentsRoot.GetComponent<PlayerOutfit>();
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(
            "Здесь задаётся только одежда при старте игры. Для множества вариантов добавляйте модели в ClothingLibrary/Models по категориям: карточки и предметы подбора создаются автоматически.",
            MessageType.Info);
        if (GUILayout.Button("Открыть библиотеку моделей"))
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/ClothingLibrary/Models");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        bool changed = false;
        changed |= DrawSlot<HatClothing>("Шапка · голова", "startingHat", "Hat1");
        changed |= DrawSlot<ShirtClothing>("Верх · тело", "startingTop", "Shirt1");
        changed |= DrawSlot<PantsClothing>("Джинсы · ноги", "startingPants", "Pants1");
        changed |= DrawSlot<ShoesClothing>("Обувь · стопы", "startingShoes", "Sneakers2");

        serializedObject.ApplyModifiedProperties();
        if (changed)
            foreach (Object selected in targets)
                AddStartingItemsToCatalog((PlayerOutfit)selected);
    }

    private bool DrawSlot<T>(string title, string field, string templateName) where T : ClothingDefinition
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        SerializedProperty slot = serializedObject.FindProperty(field);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(slot, new GUIContent("Одежда"));
        bool changed = EditorGUI.EndChangeCheck();

        GameObject model = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Добавить 3D-модель", "Перетащите FBX или префаб из окна Project"),
            null, typeof(GameObject), false);
        if (model == null) return changed;
        if (!EditorUtility.IsPersistent(model))
        {
            Debug.LogWarning("Перетащите модель из окна Project, а не объект из Hierarchy.", model);
            return changed;
        }

        T created = CreateClothing<T>(model, templateName);
        if (created != null)
        {
            slot.objectReferenceValue = created;
            changed = true;
            EditorGUIUtility.PingObject(created);
        }
        return changed;
    }

    private static T CreateClothing<T>(GameObject model, string templateName) where T : ClothingDefinition
    {
        const string folder = "Assets/ClothingItems";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "ClothingItems");

        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (existing != null && existing.Model == model) return existing;
        }

        string safeName = model.name.Replace('/', '_').Replace('\\', '_');
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeName + " " + typeof(T).Name + ".asset");
        T definition = ScriptableObject.CreateInstance<T>();
        definition.name = Path.GetFileNameWithoutExtension(path);

        SerializedObject properties = new SerializedObject(definition);
        properties.FindProperty("displayName").stringValue = model.name;
        properties.FindProperty("model").objectReferenceValue = model;

        T template = AssetDatabase.LoadAssetAtPath<T>(folder + "/" + templateName + ".asset");
        if (template != null)
        {
            properties.FindProperty("outlineMaterial").objectReferenceValue = template.OutlineMaterial;
            properties.FindProperty("highlightColor").colorValue = template.HighlightColor;
            properties.FindProperty("displayCenter").vector3Value = template.DisplayCenter;
            properties.FindProperty("displaySize").vector2Value = template.DisplaySize;
            properties.FindProperty("displayRotation").vector3Value = template.DisplayRotation;
            if (definition is ShoesClothing && template is ShoesClothing shoes)
                properties.FindProperty("pairSpacing").floatValue = shoes.PairSpacing;
        }
        properties.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(definition, path);
        AssetDatabase.SaveAssets();
        return definition;
    }

    private static void AddStartingItemsToCatalog(PlayerOutfit outfit)
    {
        NetworkPlayer player = outfit.GetComponent<NetworkPlayer>();
        if (player == null) return;

        SerializedObject playerData = new SerializedObject(player);
        SerializedProperty catalog = playerData.FindProperty("clothingCatalog");
        foreach (ClothingDefinition clothing in outfit.StartingItems)
        {
            bool alreadyThere = false;
            for (int i = 0; i < catalog.arraySize; i++)
                if (catalog.GetArrayElementAtIndex(i).objectReferenceValue == clothing)
                {
                    alreadyThere = true;
                    break;
                }
            if (alreadyThere) continue;

            int index = catalog.arraySize;
            catalog.InsertArrayElementAtIndex(index);
            catalog.GetArrayElementAtIndex(index).objectReferenceValue = clothing;
        }
        playerData.ApplyModifiedProperties();
    }
}
