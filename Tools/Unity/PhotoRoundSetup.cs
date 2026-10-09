using System;
using System.Linq;
using FishNet.Object;
using FishNet.Managing.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PhotoRoundSetup
{
    public static void Build()
    {
        const string folder="Assets/Art/Items/PhotoCamera";
        System.IO.Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        Material Mat(string name,Color colour)
        {var path=folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.color=colour;EditorUtility.SetDirty(mat);return mat;}
        System.IO.Directory.CreateDirectory("Assets/Resources");AssetDatabase.Refresh();
        var hand=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/PhotoCameraHand.mat");if(hand==null){hand=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(hand,"Assets/Resources/PhotoCameraHand.mat");}
        hand.color=Color.white;EditorUtility.SetDirty(hand);
        var shell=Mat("Mint camera",new Color(.24f,.82f,.68f));var dark=Mat("Camera rubber",new Color(.025f,.055f,.07f));var accent=Mat("Camera flash",new Color(1,.85f,.5f));
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("Photo Camera");SceneManager.MoveGameObjectToScene(root,preview);
            PhotoCameraModel.Create(root.transform,shell,dark,accent);
            var box=root.AddComponent<BoxCollider>();box.size=new Vector3(.42f,.28f,.33f);box.center=new Vector3(0,0,.045f);box.isTrigger=true;
            root.AddComponent<NetworkObject>();var item=root.AddComponent<PickupItem>();var serialized=new SerializedObject(item);serialized.FindProperty("itemName").stringValue="Photo Camera";serialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<PhotoCameraPickup>();root.AddComponent<NetworkPickup>();root.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/PhotoCamera.prefab");
        }
        finally{EditorSceneManager.ClosePreviewScene(preview);}
        var prefab=AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/Prefabs/PhotoCamera.prefab");
        var registry=AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>("Assets/DefaultPrefabObjects.asset");registry.AddObject(prefab,true);registry.InitializePrefabRange(0);
        typeof(DefaultPrefabObjects).GetMethod("SetAssetPathHashes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(registry,new object[]{0});
        if(prefab.AssetPathHash==0)throw new Exception("Photo camera network asset hash was not initialized");
        EditorUtility.SetDirty(registry);
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);
        try
        {
            var spawner=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RoundMonkeySpawner>(true)).Single();
            var data=new SerializedObject(spawner);data.FindProperty("cameraPrefab").objectReferenceValue=prefab;data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        finally{EditorSceneManager.CloseScene(scene,true);}
    }
}
