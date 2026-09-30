using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupFlashlight
{
    [MenuItem("Tools/Graybox/Add Flashlight")]
    public static void AddFlashlight()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Stop Play Mode before adding the flashlight.");
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("A camera tagged MainCamera is required.");
            return;
        }

        Transform beam = camera.transform.Find("Flashlight Beam");
        if (beam == null)
        {
            GameObject child = new GameObject("Flashlight Beam");
            beam = child.transform;
            beam.SetParent(camera.transform, false);
            beam.localPosition = new Vector3(0.12f, -0.09f, 0.18f);
            beam.localRotation = Quaternion.identity;
        }

        Light light = beam.GetComponent<Light>();
        if (light == null)
        {
            light = beam.gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.97f, 0.88f);
            light.range = 18f;
            light.spotAngle = 60f;
            light.innerSpotAngle = 35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
        }

        FlashlightController oldController = camera.GetComponent<FlashlightController>();
        if (oldController != null) Object.DestroyImmediate(oldController);

        if (beam.GetComponent<FlashlightController>() == null)
            beam.gameObject.AddComponent<FlashlightController>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = beam.gameObject;
        Debug.Log("Flashlight ready: F toggles it. Adjust brightness on Flashlight Beam in the Inspector.");
    }
}
