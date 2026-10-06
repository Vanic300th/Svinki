using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class IntegratePulledHud
{
    public static object Apply()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode first");
        var original=EditorSceneManager.GetSceneManagerSetup();
        int sceneCount=0, bindings=0;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameHUD.prefab");
        if(prefab==null) throw new Exception("Pulled GameHUD prefab missing");
        try
        {
            foreach(string path in new[]{"Assets/Scenes/SampleScene.unity","Assets/Scenes/testroom.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var roots=scene.GetRootGameObjects();
                foreach(var oldCanvas in roots.Where(r=>r.name=="Canvas" && r.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Any(i=>i.name=="Character Portrait")))
                    UnityEngine.Object.DestroyImmediate(oldCanvas);
                var hud=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameHud>(true)).FirstOrDefault();
                if(hud==null) hud=((GameObject)PrefabUtility.InstantiatePrefab(prefab,scene)).GetComponent<GameHud>();
                foreach(var component in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)))
                {
                    if(!(component is PlayerPickupInteractor) && !(component is FlashlightController) && !(component is MannequinWardrobe)) continue;
                    var data=new SerializedObject(component);var field=data.FindProperty("hud");
                    if(field==null) throw new Exception("HUD binding missing on "+component.GetType().Name);
                    field.objectReferenceValue=hud;data.ApplyModifiedPropertiesWithoutUndo();bindings++;
                }
                // Preserve the URP light components added by the incoming UI commit.
                if(path.EndsWith("SampleScene.unity"))
                    foreach(var light in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true)))
                    {
                        ulong id=GlobalObjectId.GetGlobalObjectIdSlow(light.gameObject).targetObjectId;
                        if(new ulong[]{447860099,1071885771,1257267954,1315999500}.Contains(id) && light.GetComponent<UniversalAdditionalLightData>()==null)
                            light.gameObject.AddComponent<UniversalAdditionalLightData>();
                    }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);sceneCount++;
            }
        }
        finally {EditorSceneManager.RestoreSceneManagerSetup(original);}
        return new{scenes=sceneCount,hudBindings=bindings};
    }
}
