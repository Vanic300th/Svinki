using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Svinki → Audio: добавляет звук (вложенный префаб Assets/Audio/Prefabs/MannequinAudio) всем бросаемым
/// манекенам открытой сцены. Нужен, если манекены пересоздали скриптом настройки уровня. Уже озвученные пропускает.
/// </summary>
public static class MannequinAudioSetup
{
    public const string PrefabPath = "Assets/Audio/Prefabs/MannequinAudio.prefab";

    [MenuItem("Svinki/Audio/Добавить звук бросаемым манекенам")]
    public static void AddToOpenScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[MannequinAudio] Нет префаба " + PrefabPath); return; }
        int added = 0, skipped = 0;
        foreach (ThrowableMannequin mannequin in Object.FindObjectsByType<ThrowableMannequin>(FindObjectsInactive.Include))
        {
            if (EditorUtility.IsPersistent(mannequin)) continue;
            if (mannequin.GetComponentInChildren<MannequinAudio>(true) != null) { skipped++; continue; }
            var audio = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mannequin.transform);
            var capsule = mannequin.GetComponent<CapsuleCollider>();
            audio.transform.SetLocalPositionAndRotation(capsule != null ? capsule.center : Vector3.up, Quaternion.identity);
            audio.name = "Audio";
            Undo.RegisterCreatedObjectUndo(audio, "Add mannequin audio");
            EditorSceneManager.MarkSceneDirty(mannequin.gameObject.scene);
            added++;
        }
        Debug.Log($"[MannequinAudio] Добавлено: {added}, уже было: {skipped}. Не забудьте сохранить сцену.");
    }
}
