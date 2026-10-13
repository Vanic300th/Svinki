using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.AI.Navigation;
public static class SocialLobbyLightingSetup
{
    public static string Apply()
    {
        if (System.IO.File.Exists("Assets/Scenes/FriendslopLobby.unity")) return "Separate Friendslop scene already contains the authored map and lighting.";
        var scene=SceneManager.GetSceneByName("SampleScene");
        var lobby=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LobbyActivityManager>(true)).Single().transform;
        var navigation=lobby.GetComponent<NavMeshModifier>() ?? lobby.gameObject.AddComponent<NavMeshModifier>();
        navigation.ignoreFromBuild=true;navigation.applyToChildren=true;
        var safeVolume=lobby.GetComponent<NavMeshModifierVolume>() ?? lobby.gameObject.AddComponent<NavMeshModifierVolume>();
        safeVolume.area=UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");safeVolume.center=new Vector3(0,2,1.5f);safeVolume.size=new Vector3(22.6f,8,21.6f);
        foreach(var light in lobby.GetComponentsInChildren<Light>(true))
        {
            if(light.transform.parent==lobby && light.transform.localPosition.y>4) { light.intensity=6;light.range=12; }
            else if(light.transform.parent==lobby && light.color.r>light.color.b)light.intensity=5;
        }
        if(lobby.Find("Perimeter lighting") == null)
        {
            var root=new GameObject("Perimeter lighting").transform;root.SetParent(lobby,false);
            foreach(float z in new[]{-6f,6f})foreach(float x in new[]{-7f,0f,7f})
            {
                var go=new GameObject("Perimeter fluorescent");go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(x,4.15f,z);
                var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.76f,.89f,1);light.intensity=5;light.range=11;light.shadows=LightShadows.None;
            }
        }
        foreach(var label in lobby.GetComponentsInChildren<TMP_Text>(true))
        {
            label.textWrappingMode=TextWrappingModes.NoWrap;
            if(label.name.StartsWith("Sign WORKERS' REST ROOM"))label.transform.localRotation=Quaternion.identity;
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Brightened social room with cool perimeter lighting";
    }
}
