using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FlashlightController))]
public sealed class FlashlightControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startOn"),
            new GUIContent("Включён при старте"));
        serializedObject.ApplyModifiedProperties();

        FlashlightController controller = (FlashlightController)target;
        Light light = controller.GetComponent<Light>() ?? controller.GetComponentInChildren<Light>();
        if (light == null)
        {
            EditorGUILayout.HelpBox("Добавьте компонент Light на объект фонарика.", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        float brightness = EditorGUILayout.Slider(
            new GUIContent("Яркость", "Меняется до запуска игры и сразу видна в Scene View."),
            light.intensity, 0f, 30f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(light, "Change flashlight brightness");
            light.intensity = brightness;
            EditorUtility.SetDirty(light);
        }

        EditorGUILayout.HelpBox("В игре клавиша F включает и выключает фонарик.", MessageType.Info);
    }
}
