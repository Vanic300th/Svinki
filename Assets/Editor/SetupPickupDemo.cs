using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupPickupDemo
{
    [MenuItem("Tools/Graybox/Add Pickup Example")]
    public static void AddPickupExample()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Stop Play Mode before adding the pickup example.");
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("The scene needs a camera tagged MainCamera.");
            return;
        }

        if (camera.GetComponent<PlayerPickupInteractor>() == null)
            camera.gameObject.AddComponent<PlayerPickupInteractor>();

        GameObject example = GameObject.Find("Pickup Example");
        if (example == null)
        {
            example = GameObject.CreatePrimitive(PrimitiveType.Cube);
            example.name = "Pickup Example";
            example.transform.position = new Vector3(0f, 1.55f, -0.5f);
            example.transform.localScale = Vector3.one * 0.55f;
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Graybox Obstacles.mat");
            if (material != null) example.GetComponent<Renderer>().sharedMaterial = material;
        }

        if (example.GetComponent<PickupItem>() == null)
            example.AddComponent<PickupItem>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = example;
        Debug.Log("Pickup example ready. Look at the glowing cube and press E to pick it up.");
    }
}
