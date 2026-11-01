using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectSvinHudDisplaySetup
{
    [MenuItem("Svinki/UI/Apply adjustable HUD portraits")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Edit mode required");
        const string prefabPath = "Assets/_Project/Prefabs/UI/GameHUD.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try { Configure(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (string path in new[] { "Assets/_Project/Scenes/Gameplay/SampleScene.unity", "Assets/_Project/Scenes/Lobby/FriendslopLobby.unity" })
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (var root in scene.GetRootGameObjects()) Configure(root);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
        }
    }
    private static void Configure(GameObject root)
    {
        foreach (var portrait in root.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Where(p => p.name == "Character Portrait"))
        {
            var layout = portrait.GetComponent<HudPortraitLayout>() ?? portrait.gameObject.AddComponent<HudPortraitLayout>();
            var background = portrait.transform.parent.Find("Portrait Background");
            layout.background = background != null ? background.gameObject : null;
            if (layout.background != null) layout.background.SetActive(false);
            portrait.raycastTarget = false; EditorUtility.SetDirty(layout);
        }
    }
}
