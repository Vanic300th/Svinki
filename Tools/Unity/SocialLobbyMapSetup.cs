using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class SocialLobbyMapSetup
{
    public static string Apply()
    {
        if (System.IO.File.Exists(SvinkiAssetPaths.SocialLobbyScene)) return "Separate Friendslop scene already contains the authored map and lighting.";
        var scene=SceneManager.GetSceneByName("SampleScene");
        var map=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StoreMap>(true)).Single();
        if(map.Floors.Any(f=>f.center.y < -15))return "Lobby already mapped";
        var data=new SerializedObject(map);var floors=data.FindProperty("floors");int end=floors.arraySize;floors.arraySize+=2;
        void Add(int index,Vector2 center,Vector2 size){var item=floors.GetArrayElementAtIndex(index);item.FindPropertyRelative("center").vector2Value=center;item.FindPropertyRelative("size").vector2Value=size;item.FindPropertyRelative("angle").floatValue=0;}
        Add(end,new Vector2(0,-22),new Vector2(22,18));Add(end+1,new Vector2(0,-11.5f),new Vector2(4,3));
        var min=data.FindProperty("minimum");min.vector2Value=new Vector2(min.vector2Value.x,-31.15f);
        // Existing map geometry remains valid, including the entrance walls. Add the annex perimeter.
        var walls=data.FindProperty("walls");int start=walls.arraySize;walls.arraySize+=4;
        Vector2[] centers={new Vector2(-11,-22),new Vector2(11,-22),new Vector2(0,-31),new Vector2(0,-13)};
        Vector2[] sizes={new Vector2(.3f,18),new Vector2(.3f,18),new Vector2(22,.3f),new Vector2(22,.3f)};
        for(int i=0;i<4;i++){var w=walls.GetArrayElementAtIndex(start+i);w.FindPropertyRelative("center").vector2Value=centers[i];w.FindPropertyRelative("size").vector2Value=sizes[i];w.FindPropertyRelative("angle").floatValue=0;}
        data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Added lobby and passage to existing minimap";
    }
}
