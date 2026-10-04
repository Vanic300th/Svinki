using UnityEditor;
using UnityEngine;

public static class PlayerVisualSetup
{
    private const string PlayerPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string ModelPath = "Assets/Models/UAL1_Standard.fbx";
    private const string MannequinPath = "Assets/Prefabs/Mannequin.prefab";

    [MenuItem("Svinki/Одежда/Установить модель игрока")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        GameObject mannequin = AssetDatabase.LoadAssetAtPath<GameObject>(MannequinPath);
        if (source == null || mannequin == null)
        {
            Debug.LogError("Не найдена модель UAL1_Standard или Mannequin.prefab.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            Transform cylinder = root.transform.Find("Cylinder Visual");
            if (cylinder != null) Object.DestroyImmediate(cylinder.gameObject);

            Transform visual = root.transform.Find("Character Visual");
            if (visual == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                instance.name = "Character Visual";
                visual = instance.transform;
            }
            visual.localPosition = new Vector3(0f, .102f, .023f);
            visual.localRotation = Quaternion.Euler(0f, 180f, 0f);
            visual.localScale = Vector3.one * .9256f;

            Animator animator = visual.GetComponentInChildren<Animator>(true);
            Animator sampleAnimator = mannequin.GetComponentInChildren<Animator>(true);
            if (animator != null && sampleAnimator != null)
            {
                animator.runtimeAnimatorController = sampleAnimator.runtimeAnimatorController;
                animator.applyRootMotion = false;
            }

            Material white = AssetDatabase.LoadAssetAtPath<Material>(
                AssetDatabase.GUIDToAssetPath("7679814e250b748d3872139c0d2cbf2f"));
            if (white != null)
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = white;
                    renderer.sharedMaterials = materials;
                }

            WorldOutfitRenderer outfit = root.GetComponent<WorldOutfitRenderer>();
            if (outfit == null) outfit = root.AddComponent<WorldOutfitRenderer>();
            SerializedObject outfitData = new SerializedObject(outfit);
            outfitData.FindProperty("characterVisual").objectReferenceValue = visual;
            outfitData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            Debug.Log("[Player] UAL1_Standard заменил цилиндр; одежда показывается на персонаже.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
