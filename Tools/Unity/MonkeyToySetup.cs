using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using FishNet.Object;
using FishNet.Component.Transforming;
using FishNet.Managing.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class MonkeyToySetup
{
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        const string modelPath="Assets/Art/Items/MonkeyToy/ChimpanzeeToy.fbx",prefabPath="Assets/Prefabs/MonkeyToy.prefab";
        var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=false;importer.SaveAndReimport();
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Items/MonkeyToy/Monkey Toy.mat");
        if(material==null){material=new Material(Shader.Find("Svinki/Animal Toy"));AssetDatabase.CreateAsset(material,"Assets/Art/Items/MonkeyToy/Monkey Toy.mat");}
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_VividGain",1.12f);material.SetFloat("_Saturation",1);material.SetFloat("_Contrast",1);EditorUtility.SetDirty(material);
        var temp=EditorSceneManager.NewPreviewScene();GameObject root=null;
        try
        {
            root=new GameObject("Monkey Toy");SceneManager.MoveGameObjectToScene(root,temp);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),temp);model.transform.SetParent(root.transform,false);
            foreach(var r in model.GetComponentsInChildren<Renderer>(true)){r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();if(r is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;}
            foreach(var a in model.GetComponentsInChildren<Animator>(true))a.enabled=false;
            root.AddComponent<NetworkObject>();root.AddComponent<ArticulatedRagdoll>();var toy=root.AddComponent<MonkeyToy>();root.AddComponent<NetworkMonkeyToy>();
            var collider=root.GetComponent<CapsuleCollider>();collider.isTrigger=true;collider.radius=.19f;collider.height=.72f;collider.center=Vector3.zero;
            var nt=root.AddComponent<NetworkTransform>();var data=new SerializedObject(nt);data.FindProperty("_clientAuthoritative").boolValue=false;data.ApplyModifiedPropertiesWithoutUndo();
            var modifier=root.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
            Rig(root,true);PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }
        finally{EditorSceneManager.ClosePreviewScene(temp);}
        var prefab=AssetDatabase.LoadAssetAtPath<NetworkObject>(prefabPath);
        var registry=AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>("Assets/DefaultPrefabObjects.asset");registry.AddObject(prefab,true);registry.InitializePrefabRange(0);EditorUtility.SetDirty(registry);
        foreach(var path in new[]{"Assets/Prefabs/NetworkPlayer.prefab","Assets/Prefabs/OfflinePlayer.prefab"})
        {
            var player=PrefabUtility.LoadPrefabContents(path);try{if(player.GetComponent<PlayerMonkeyCarry>()==null)player.AddComponent<PlayerMonkeyCarry>();PrefabUtility.SaveAsPrefabAsset(player,path);}finally{PrefabUtility.UnloadPrefabContents(player);}
        }
        var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);
        try
        {
            var brains=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MannequinBrain>(true)).ToArray();
            foreach(var brain in brains){if(brain.GetComponent<ArticulatedRagdoll>()==null)brain.gameObject.AddComponent<ArticulatedRagdoll>();if(brain.GetComponent<MannequinStun>()==null)brain.gameObject.AddComponent<MannequinStun>();if(brain.GetComponent<NetworkMannequinStun>()==null)brain.gameObject.AddComponent<NetworkMannequinStun>();Rig(brain.gameObject,false);}
            var spawner=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RoundMonkeySpawner>(true)).FirstOrDefault();
            if(spawner==null){var go=new GameObject("Monkey toy round spawner");SceneManager.MoveGameObjectToScene(go,scene);spawner=go.AddComponent<RoundMonkeySpawner>();}
            var spawnData=new SerializedObject(spawner);spawnData.FindProperty("prefab").objectReferenceValue=prefab;spawnData.ApplyModifiedPropertiesWithoutUndo();
            var create=typeof(NetworkObject).GetMethod("CreateSceneId",BindingFlags.Static|BindingFlags.NonPublic);var serialize=typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var item in (List<NetworkObject>)create.Invoke(null,new object[]{scene,true,0}))serialize.Invoke(item,new object[]{true,false});
            // Component layouts changed on existing scene NPCs too.
            foreach(var item in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NetworkObject>(true)))serialize.Invoke(item,new object[]{true,false});
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "Monkey rig/prefab registered; player hands configured; "+brains.Length+" NPC stun rigs saved";
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
    private static void Rig(GameObject root,bool monkey)
    {
        var bones=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s=>s.bones).Where(b=>b!=null).GroupBy(b=>b.name).ToDictionary(g=>g.Key,g=>g.First());
        string[] names=monkey?new[]{"Hips","Spine","Head","UpperArm_L","Forearm_L","Hand_L","UpperArm_R","Forearm_R","Hand_R","Thigh_L","Shin_L","Thigh_R","Shin_R"}:new[]{"pelvis","spine_03","Head","upperarm_l","lowerarm_l","upperarm_r","lowerarm_r","thigh_l","calf_l","thigh_r","calf_r"};
        string[] ends=monkey?new[]{"Spine","Head",null,"Forearm_L","Hand_L",null,"Forearm_R","Hand_R",null,"Shin_L","Foot_L","Shin_R","Foot_R"}:new[]{"spine_01","neck_01",null,"lowerarm_l","hand_l","lowerarm_r","hand_r","calf_l","foot_l","calf_r","foot_r"};
        int[] parents=monkey?new[]{-1,0,1,1,3,4,1,6,7,0,9,0,11}:new[]{-1,0,1,1,3,1,5,0,7,0,9};
        var data=new SerializedObject(root.GetComponent<ArticulatedRagdoll>());var list=data.FindProperty("segments");list.arraySize=names.Length;
        for(int i=0;i<names.Length;i++)
        {
            var part=list.GetArrayElementAtIndex(i);var bone=bones[names[i]];part.FindPropertyRelative("bone").objectReferenceValue=bone;part.FindPropertyRelative("end").objectReferenceValue=ends[i]!=null?bones[ends[i]]:null;
            part.FindPropertyRelative("parent").intValue=parents[i];part.FindPropertyRelative("mass").floatValue=monkey?(i<3?.55f:.14f):(i<3?5:1.5f);
            part.FindPropertyRelative("radius").floatValue=monkey?(i<2?.055f:.025f):(i<2?.13f:i>6?.065f:.045f);
            part.FindPropertyRelative("boxSize").vector3Value=monkey?(i==2?new Vector3(.10f,.105f,.12f):new Vector3(.045f,.045f,.055f)):new Vector3(.19f,.23f,.19f);
            part.FindPropertyRelative("boxCenter").vector3Value=monkey?(i==2?new Vector3(0,.05f,0):Vector3.zero):bone.InverseTransformDirection(Vector3.up)*.10f;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
