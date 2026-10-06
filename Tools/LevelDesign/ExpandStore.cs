using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using FishNet.Object;

public static class ExpandStore
{
    static Scene scene;
    static Transform wing;
    static Material plaster, metal, wood;
    static readonly float[] cuts = {-30,-14,2,18,35};
    static readonly string[] titles = {"11 / VINTAGE", "12 / SPORTS", "13 / LOUNGE", "14 / SHOWROOM"};
    static T[] Items<T>() where T : Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode.");
        Scene previous = SceneManager.GetActiveScene();
        scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            Transform level = scene.GetRootGameObjects().First(g=>g.name=="Graybox Level").transform;
            if (level.Find("Expanded north wing - v1") != null)
            {
                var savedSurface=Items<NavMeshSurface>().Single();
                if(savedSurface.navMeshData==null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(savedSurface.navMeshData)))
                { BakeNavigation(savedSurface);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Expansion already installed; repaired persistent navigation asset."; }
                return "Expansion already installed.";
            }
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Docs/LevelDesign");
            const string backup="Docs/LevelDesign/BeforeExpansion.unity";
            if (!File.Exists(backup)) File.Copy(scene.path, backup);
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Expand Svinki store");
            Transform architecture = level.Find("Architecture - 10 distinct spaces");
            Transform furniture = level.Find("Shelving, cover and fitting maze");
            Transform decoration = level.Find("Art, signage and clothing displays");
            Transform gameplay = level.Find("48 Pickups and 16 Mannequins");
            Transform lighting = level.Find("Store lighting");
            foreach(Transform group in new[]{architecture,furniture,decoration,lighting})
            { Undo.RecordObject(group,"Expand room footprint"); group.localScale=Vector3.Scale(group.localScale,new Vector3(1.25f,1,1.25f)); }
            foreach(Transform actor in gameplay.Cast<Transform>())
            { Undo.RecordObject(actor,"Move actor into larger room"); actor.localPosition=Vector3.Scale(actor.localPosition,new Vector3(1.25f,1,1.25f)); }
            Transform stashes=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Thief drop-off points")?.transform;
            if(stashes!=null) foreach(Transform stash in stashes)
            { Undo.RecordObject(stash,"Move stash"); stash.localPosition=Vector3.Scale(stash.localPosition,new Vector3(1.25f,1,1.25f)); }
            foreach(Light light in lighting.GetComponentsInChildren<Light>(true))
            { Undo.RecordObject(light,"Expand light reach"); light.range*=1.25f; }
            Undo.DestroyObjectImmediate(architecture.Find("Partition 1").gameObject);
            wing=new GameObject("Expanded north wing - v1").transform; wing.SetParent(level,false); Undo.RegisterCreatedObjectUndo(wing.gameObject,"New north wing");
            plaster=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PeachStore/Warm plaster.mat");
            metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PeachStore/Dark steel.mat");
            wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PeachStore/Walnut display.mat");
            var source=Items<ThrowableMannequin>().First(t=>t.transform.position.y<.1f).gameObject;
            var clothes=Items<ClothingPickup>().Where(c=>c.Clothing!=null).ToArray();
            for(int i=0;i<4;i++)
            {
                float a=cuts[i],b=cuts[i+1],mid=(a+b)*.5f;
                Transform room=new GameObject(titles[i]).transform;room.SetParent(wing,false);
                var floor=AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/PeachStore/Floor {(i+3):00}.mat");
                Box("Floor",room,new Vector3(mid,-.15f,55),new Vector3(b-a,.3f,20),floor);
                Box("Ceiling",room,new Vector3(mid,4.92f,55),new Vector3(b-a,.24f,20),plaster);
                DoorWall(room,a,b,45,mid-2,mid+2);
                Box("Back wall",room,new Vector3(mid,2.4f,65),new Vector3(b-a,4.8f,.28f),plaster);
                if(i==0) Box("West wall",room,new Vector3(a,2.4f,55),new Vector3(.28f,4.8f,20),plaster);
                if(i==3) Box("East wall",room,new Vector3(b,2.4f,55),new Vector3(.28f,4.8f,20),plaster);
                else
                {
                    Box("Side wall front",room,new Vector3(b,2.4f,48.5f),new Vector3(.28f,4.8f,7),plaster);
                    Box("Side wall back",room,new Vector3(b,2.4f,60),new Vector3(.28f,4.8f,10),plaster);
                    Box("Side door lintel",room,new Vector3(b,3.9f,53.5f),new Vector3(.28f,1.8f,3),plaster);
                }
                for(int n=0;n<4;n++) SpawnDisplay(source, room, new Vector3(n%2==0?a+3:b-3,.02f,n<2?49:60),i*4+n);
                for(int n=0;n<2;n++)
                {
                    var copy=UnityEngine.Object.Instantiate(clothes[(i*2+n)%clothes.Length].gameObject,room);
                    copy.name=$"North wing clothing {i+1}-{n+1}";
                    copy.transform.position=new Vector3(mid+(n==0?-3:3),1.12f,57);
                    Undo.RegisterCreatedObjectUndo(copy,"New clothing pickup");
                }
                // Low display table and a bench leave a wide circulation route down the middle.
                Box("Display table",room,new Vector3(mid, .40f,61.5f),new Vector3(4,.80f,1.4f),wood);
                Box("Side bench",room,new Vector3(a+1.2f,.30f,55),new Vector3(1.4f,.60f,3),wood);
                var sign=new GameObject("Room sign",typeof(TextMesh)); sign.transform.SetParent(room,false);
                sign.transform.position=new Vector3(mid,3.45f,45.17f);sign.transform.rotation=Quaternion.Euler(0,180,0);
                var text=sign.GetComponent<TextMesh>();text.text=titles[i];text.fontSize=64;text.characterSize=.045f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.9f,.75f,.53f);
                var light=new GameObject("Light source",typeof(Light));light.transform.SetParent(room,false);light.transform.position=new Vector3(mid,4.1f,55);light.transform.rotation=Quaternion.Euler(90,0,0);
                var lamp=light.GetComponent<Light>();lamp.type=LightType.Spot;lamp.spotAngle=100;lamp.innerSpotAngle=55;lamp.range=18;lamp.intensity=10;lamp.color=new Color(1,.82f,.65f);lamp.shadows=LightShadows.Soft;
            }
            Physics.SyncTransforms();
            NavMeshSurface surface=Items<NavMeshSurface>().Single();
            surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.collectObjects=CollectObjects.All;
            BakeNavigation(surface);
            Vector3[] oldRooms={new Vector3(-25,0,4),new Vector3(-16,0,10),new Vector3(24,0,10),new Vector3(0,0,8),new Vector3(-4,0,25),new Vector3(17,0,28),new Vector3(-20,0,39),new Vector3(23,0,40)};
            int added=0;
            foreach(Vector3 desired in oldRooms)
            {
                bool found=false;
                for(int offset=0;offset<9 && !found;offset++)
                {
                    Vector3 at=desired+new Vector3((offset%3-1)*1.4f,0,(offset/3-1)*1.4f);
                    if(!NavMesh.SamplePosition(at,out NavMeshHit hit,1.2f,NavMesh.AllAreas))continue;
                    if(Physics.CheckCapsule(hit.position+Vector3.up*.45f,hit.position+Vector3.up*1.6f,.33f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                    SpawnDisplay(source,gameplay,hit.position+Vector3.up*.02f,16+added++);found=true;
                }
                if(!found)throw new InvalidOperationException("No clear display position near "+desired);
            }
            var create=typeof(NetworkObject).GetMethod("CreateSceneId",BindingFlags.Static|BindingFlags.NonPublic);
            var serialize=typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",BindingFlags.Instance|BindingFlags.NonPublic);
            object[] args={scene,true,0};
            foreach(NetworkObject item in (List<NetworkObject>)create.Invoke(null,args))serialize.Invoke(item,new object[]{true,false});
            EditorUtility.SetDirty(surface);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);
            return $"Expanded existing rooms 25% on X/Z; added four 16–17 x 20 m rooms, 24 throwable displays and 8 clothing pickups. NavMesh baked and FishNet IDs regenerated.";
        }
        finally { if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true); }
    }
    static void BakeNavigation(NavMeshSurface surface)
    {
        surface.BuildNavMesh();
        var data=surface.navMeshData;
        if(data==null)throw new InvalidOperationException("Navigation bake produced no data.");
        const string path="Assets/Settings/StoreExpandedNavMesh.asset";
        var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if(saved==null){AssetDatabase.CreateAsset(data,path);saved=data;}
        else if(saved!=data)
        {
            surface.RemoveData();EditorUtility.CopySerialized(data,saved);UnityEngine.Object.DestroyImmediate(data);
            surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(saved);
        }
        EditorUtility.SetDirty(surface);AssetDatabase.SaveAssets();
    }
    static void SpawnDisplay(GameObject source,Transform parent,Vector3 position,int index)
    {
        var copy=UnityEngine.Object.Instantiate(source,parent);copy.name="Store display expansion "+(index+1);copy.transform.SetPositionAndRotation(position,Quaternion.Euler(0,index%2==0?130:230,0));
        ThrowableMannequinSetup.Configure(copy);Undo.RegisterCreatedObjectUndo(copy,"New throwable display");
    }
    static void DoorWall(Transform room,float a,float b,float z,float da,float db)
    {
        Box("Entrance wall left",room,new Vector3((a+da)/2,2.4f,z),new Vector3(da-a,4.8f,.28f),plaster);
        Box("Entrance wall right",room,new Vector3((b+db)/2,2.4f,z),new Vector3(b-db,4.8f,.28f),plaster);
        Box("Entrance lintel",room,new Vector3((da+db)/2,3.9f,z),new Vector3(db-da,1.8f,.28f),plaster);
    }
    static void Box(string name,Transform parent,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material??plaster;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);
    }
}
