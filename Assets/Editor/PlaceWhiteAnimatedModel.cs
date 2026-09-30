using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlaceWhiteAnimatedModel
{
    private const string ModelPath = "Assets/Models/UAL1_Standard.fbx";
    private const string MaterialPath = "Assets/Materials/Animated Character White.mat";
    private const string InstanceName = "White Animated Character";

    [MenuItem("Tools/Graybox/Place White Animated Model")]
    public static void Place()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Stop Play Mode before placing the animated model.");
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("Model was not imported: " + ModelPath);
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("URP Lit shader was not found.");
            return;
        }

        Material white = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (white == null)
        {
            white = new Material(shader) { name = "Animated Character White" };
            AssetDatabase.CreateAsset(white, MaterialPath);
        }
        white.shader = shader;
        white.SetColor("_BaseColor", Color.white);
        white.SetFloat("_Metallic", 0f);
        white.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(white);

        GameObject instance = GameObject.Find(InstanceName);
        bool created = instance == null;
        if (created)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = InstanceName;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds();
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;

            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = white;
            renderer.sharedMaterials = slots;

            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        Animator animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (asset is not Avatar avatar) continue;
            animator.avatar = avatar;
            break;
        }

        if (created && hasBounds && bounds.size.y > 0.001f)
        {
            const float desiredHeight = 1.8f;
            instance.transform.localScale *= desiredHeight / bounds.size.y;

            bounds = new Bounds();
            hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            instance.transform.position += new Vector3(2f - bounds.center.x, -bounds.min.y, 0.5f - bounds.center.z);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = instance;

        Debug.Log($"Animated model ready: {renderers.Length} renderers, " +
                  $"position {instance.transform.position}, height {bounds.size.y:F2} m.");
    }
}
