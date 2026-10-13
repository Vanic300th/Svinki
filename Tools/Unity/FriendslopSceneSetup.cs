using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
public static class FriendslopSceneSetup
{
    static T[] Items<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    public static string Apply()
    {
        const string path="Assets/Scenes/FriendslopLobby.unity";
        if(File.Exists(path))return "Friendslop scene already created";
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode required");
        var factory=SceneManager.GetSceneByName("SampleScene");
        if(!factory.IsValid())factory=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);
        if(factory.isDirty)throw new Exception("Unsaved factory changes");
        const string recovery="Assets/_Recovery/FriendslopSource.unity";
        File.Copy("ArtSource/SocialLobby/Before/SampleScene.unity",recovery,true);AssetDatabase.ImportAsset(recovery);
        var baseline=EditorSceneManager.OpenScene(recovery,OpenSceneMode.Additive);
        var idField=typeof(NetworkObject).GetField("SceneId",BindingFlags.NonPublic|BindingFlags.Instance);
        var oldIDs=Items<NetworkObject>(baseline).Select(n=>(ulong)idField.GetValue(n)).ToArray();
        var level=factory.GetRootGameObjects().Single(g=>g.name=="Graybox Level");
        var room=Items<LobbyActivityManager>(factory).Single().gameObject;
        var lift=room.GetComponentInChildren<LobbyFreightElevator>(true);
        UnityEngine.Object.DestroyImmediate(lift.GetComponent<RoundFinishStation>());
        lift.name="Freight lift · factory run";
        foreach(var sign in lift.GetComponentsInChildren<TMP_Text>(true))sign.text=sign.text.Replace("FINAL SHOW","FACTORY RUN");
        // Restore only the final gathering station and entrance geometry changed for the annex.
        var oldReady=Items<RoundFinishStation>(baseline).Single();
        var ready=UnityEngine.Object.Instantiate(oldReady.gameObject);ready.name=oldReady.name;
        ready.transform.SetParent(level.transform,false);ready.transform.position=oldReady.transform.position;
        ready.transform.rotation=oldReady.transform.rotation;ready.transform.localScale=oldReady.transform.localScale;
        var oldWall=Items<BoxCollider>(baseline).Single(c=>c.name=="Solid wall" && Vector3.Distance(c.bounds.center,new Vector3(0,2.4f,-10))<.1f);
        var wall=Items<Transform>(factory).Single(t=>t.name=="Solid wall"&& Vector3.Distance(t.position,new Vector3(-4.1f,2.4f,-10))<.1f);
        wall.position=oldWall.transform.position;wall.rotation=oldWall.transform.rotation;wall.localScale=oldWall.transform.localScale;
        foreach(string name in new[]{"Entrance wall right","Entrance lintel"})
            foreach(var obj in Items<Transform>(factory).Where(t=>t.name==name).ToArray())UnityEngine.Object.DestroyImmediate(obj.gameObject);
        foreach(string name in new[]{"Peach poster frame","Sign - PEACH / NEW COLLECTION"})
        {
            var old=Items<Transform>(baseline).Single(t=>t.name==name);var current=Items<Transform>(factory).Single(t=>t.name==name);
            current.position=old.position;current.rotation=old.rotation;current.localScale=old.localScale;
        }
        EditorUtility.CopySerialized(Items<StoreMap>(baseline).Single(),Items<StoreMap>(factory).Single());
        SceneManager.SetActiveScene(factory);
        var social=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        room.transform.SetParent(null,true);SceneManager.MoveGameObjectToScene(room,social);room.name="Friendslop · workers' safe room";
        UnityEngine.Object.DestroyImmediate(room.GetComponent<NavMeshModifier>());
        UnityEngine.Object.DestroyImmediate(room.GetComponent<NavMeshModifierVolume>());
        foreach(var part in room.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Passage ")).ToArray())UnityEngine.Object.DestroyImmediate(part.gameObject);
        var seal=GameObject.CreatePrimitive(PrimitiveType.Cube);seal.name="North industrial wall";seal.transform.SetParent(room.transform,false);
        seal.transform.localPosition=new Vector3(0,1.6f,9);seal.transform.localScale=new Vector3(4,3.2f,.3f);
        seal.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Lobby/Aged concrete.mat");
        // Clone a group together so camera stacks and HUD references are remapped within the new scene.
        var source=new GameObject("Friendslop presentation");SceneManager.MoveGameObjectToScene(source,factory);
        string[] names={"Main Camera","Directional Light","Global Volume","EventSystem","GameHUD","HUD Character Model","HUD Character Camera","Room Physics"};
        var originals=factory.GetRootGameObjects().Where(g=>names.Contains(g.name)).ToArray();
        foreach(var original in originals)original.transform.SetParent(source.transform,true);
        var presentation=UnityEngine.Object.Instantiate(source);presentation.name=source.name;SceneManager.MoveGameObjectToScene(presentation,social);
        foreach(var original in originals)original.transform.SetParent(null,true);UnityEngine.Object.DestroyImmediate(source);
        foreach(var camera in presentation.GetComponentsInChildren<GrayboxFirstPersonCamera>(true))
        {var data=new SerializedObject(camera);data.FindProperty("target").objectReferenceValue=null;data.ApplyModifiedPropertiesWithoutUndo();camera.transform.position=new Vector3(0,1.7f,-22);}
        var map=room.AddComponent<StoreMap>();var serialized=new SerializedObject(map);serialized.FindProperty("visibleRange").floatValue=1;
        void Footprint(SerializedProperty p,Vector2 center,Vector2 size){p.FindPropertyRelative("center").vector2Value=center;p.FindPropertyRelative("size").vector2Value=size;}
        var floors=serialized.FindProperty("floors");floors.arraySize=1;Footprint(floors.GetArrayElementAtIndex(0),new Vector2(0,-22),new Vector2(22,18));
        var walls=serialized.FindProperty("walls");walls.arraySize=4;
        Footprint(walls.GetArrayElementAtIndex(0),new Vector2(-11,-22),new Vector2(.3f,18));Footprint(walls.GetArrayElementAtIndex(1),new Vector2(11,-22),new Vector2(.3f,18));
        Footprint(walls.GetArrayElementAtIndex(2),new Vector2(0,-31),new Vector2(22,.3f));Footprint(walls.GetArrayElementAtIndex(3),new Vector2(0,-13),new Vector2(22,.3f));
        serialized.FindProperty("minimum").vector2Value=new Vector2(-11.15f,-31.15f);serialized.FindProperty("maximum").vector2Value=new Vector2(11.15f,-12.85f);serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.CloseScene(baseline,true);AssetDatabase.DeleteAsset(recovery);
        EditorSceneManager.MarkSceneDirty(social);EditorSceneManager.SaveScene(social,path);
        // A separate scene gets its own FishNet scene identity; factory IDs must remain unchanged.
        foreach(var obj in Items<NetworkObject>(social))idField.SetValue(obj,(ulong)0);
        typeof(NetworkObject).GetMethod("CreateSceneId",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{social,false,0});
        foreach(var obj in Items<NetworkObject>(social))typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,new object[]{true,false});
        var newIDs=Items<NetworkObject>(factory).Select(n=>(ulong)idField.GetValue(n)).ToArray();
        if(!oldIDs.OrderBy(x=>x).SequenceEqual(newIDs.OrderBy(x=>x)))throw new Exception("Factory network IDs changed");
        EditorSceneManager.MarkSceneDirty(factory);EditorSceneManager.SaveScene(factory);EditorSceneManager.MarkSceneDirty(social);EditorSceneManager.SaveScene(social);
        var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==path))scenes.Add(new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=scenes.ToArray();
        AssetDatabase.SaveAssets();SceneManager.SetActiveScene(social);
        string report="PASS: separate FriendslopLobby, "+Items<NetworkObject>(social).Length+" social network objects; "+newIDs.Length+" original factory network IDs preserved; original Ready station restored; build scene registered.";
        File.WriteAllText("ArtSource/SocialLobby/friendslop-scene-validation.txt",report+"\n");return report;
    }
}
