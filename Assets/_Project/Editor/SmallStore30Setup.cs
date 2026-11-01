using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Object;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Thirty human-scale rooms; retains scene network objects, their IDs and dimensions.</summary>
public static class SmallStore30Setup
{
    private const string RootName = "30 small departments";
    private const string Modules = SvinkiAssetPaths.LevelDesign + "/RoomDressing/Prefabs/";
    private static Scene scene;
    private static Transform level;
    private static Material wall, ceiling, wood, metal, trim, glow;
    private static readonly List<Room> rooms = new List<Room>();
    private static readonly List<Pose> slots = new List<Pose>();
    private static readonly Collider[] hits = new Collider[256];
    [Serializable] private sealed class Room { public string name; public Vector3 center, size; [NonSerialized] public Transform root; public float X0 => center.x-size.x/2; public float X1 => center.x+size.x/2; public float Z0 => center.z-size.z/2; public float Z1 => center.z+size.z/2; }
    [Serializable] private sealed class Report { public int count, garments, fixtures; public float area, width, depth; public Room[] rooms; }
    private static T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    private static void LoadScene()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit mode required");
        scene=SceneManager.GetSceneByPath(SvinkiAssetPaths.GameplayScene);
        if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(SvinkiAssetPaths.GameplayScene,OpenSceneMode.Additive);
        level=scene.GetRootGameObjects().Single(r=>r.name=="Graybox Level").transform;
    }
    [MenuItem("Svinki/Level/Build thirty small rooms")]
    public static string Build()
    {
        LoadScene(); if(scene.isDirty)throw new InvalidOperationException("Save existing scene changes first");
        var previous=SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
        try
        {
            var floors=All<BoxCollider>().Where(b=>b.name=="Floor"&&b.bounds.center.y<.3f).ToArray();
            var names=floors.Select(f=>f.transform.parent.name).Select(n=>n.EndsWith(" / A")||n.EndsWith(" / B")?n.Substring(0,n.Length-4):n).Distinct().OrderBy(n=>n,StringComparer.Ordinal).ToArray();
            if(names.Length!=30)throw new InvalidOperationException("Expected thirty department themes");
            wall=AssetDatabase.LoadAssetAtPath<Material>(SvinkiAssetPaths.IndustrialArt+"/concrete_floor_worn_001/URP_concrete_floor_worn_001.mat");
            ceiling=AssetDatabase.LoadAssetAtPath<Material>(SvinkiAssetPaths.IndustrialArt+"/worn_corrugated_iron/URP_worn_corrugated_iron.mat");
            Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>(SvinkiAssetPaths.SharedMaterials+"/PeachStore/"+n+".mat");
            wood=Mat("Walnut display");metal=Mat("Dark steel");trim=Mat("Petrol accent");glow=Mat("Warm diffuser");
            var pickups=All<ClothingPickup>().OrderBy(p=>p.name,StringComparer.Ordinal).ToArray();
            var network=level.GetComponentsInChildren<NetworkObject>(true).Select(n=>n.transform).ToHashSet();
            var actors=network.Where(t=>!t.GetComponentsInParent<NetworkObject>(true).Any(n=>n.transform!=t&&network.Contains(n.transform))).ToArray();
            foreach(var actor in actors)
            {
                actor.SetParent(level,true);
                var modifier=actor.GetComponent<NavMeshModifier>()??actor.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
            }
            foreach(var child in level.Cast<Transform>().Where(t=>!actors.Contains(t)).ToArray())Object.DestroyImmediate(child.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(level,false);
            rooms.Clear();slots.Clear();
            float[] widths={11,12,10,12,11},depths={11,12,10,12,11,12};float z=-8;
            var grid=new Room[6,5];
            for(int row=0;row<6;row++)
            {
                float x=-28;
                for(int col=0;col<5;col++)
                {
                    int index=row==0?new[]{3,1,0,2,4}[col]:row*5+col;
                    var r=new Room{name=names[index],center=new Vector3(x+widths[col]/2,0,z+depths[row]/2),size=new Vector3(widths[col],.3f,depths[row])};
                    r.root=new GameObject(r.name).transform;r.root.SetParent(root,false);rooms.Add(r);grid[row,col]=r;
                    var floor=AssetDatabase.LoadAssetAtPath<Material>(SvinkiAssetPaths.IndustrialArt+"/Factory floor "+index%4+".mat");
                    Box("Floor",r.root,r.center+Vector3.down*.15f,new Vector3(r.size.x,.3f,r.size.z),floor);
                    Box("Ceiling",r.root,r.center+Vector3.up*5.02f,new Vector3(r.size.x,.24f,r.size.z),ceiling);
                    if(col==0)Boundary(r,false,r.X0,r.Z0,r.Z1,null);
                    Boundary(r,false,r.X1,r.Z0,r.Z1,col==4?(float?)null:r.center.z+((row+col)%2==0? .8f:-.8f));
                    if(row==0)Boundary(r,true,r.Z0,r.X0,r.X1,null);
                    Boundary(r,true,r.Z1,r.X0,r.X1,row==5?(float?)null:r.center.x);
                    LightAndSigns(r,index);x+=widths[col];
                }
                z+=depths[row];
            }
            rooms.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));
            var lift=actors.Single(t=>t.GetComponent<LobbyFreightElevator>()!=null);lift.position=new Vector3(0,0,-5.8f);
            foreach(var actor in actors.Where(t=>t!=lift))actor.position=new Vector3(1000,0,1000); // keep old actors out of authoring collision checks
            int fixtureCount=0;
            for(int i=0;i<rooms.Count;i++)
            {
                int count=i==0?2:i<=7?4:3;
                CreateCabinets(rooms[i],count);
                fixtureCount+=Furnish(rooms[i],i);
            }
            if(slots.Count!=96||pickups.Length!=96)throw new InvalidOperationException("Expected exactly 96 garment slots");
            var data=new SerializedObject(level.GetComponent<RoundClothingLayout>());var list=data.FindProperty("pickups");var positions=data.FindProperty("positions");var rotations=data.FindProperty("rotations");
            list.arraySize=positions.arraySize=rotations.arraySize=96;
            for(int i=0;i<96;i++)
            {
                pickups[i].transform.SetPositionAndRotation(slots[i].position,slots[i].rotation);
                list.GetArrayElementAtIndex(i).objectReferenceValue=pickups[i];positions.GetArrayElementAtIndex(i).vector3Value=slots[i].position;rotations.GetArrayElementAtIndex(i).quaternionValue=slots[i].rotation;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();StoreRoomDressingSetup.Bake();
            PlaceActors(actors.Where(t=>t!=lift&&t.GetComponent<ClothingPickup>()==null).ToArray());
            var dropOff=scene.GetRootGameObjects().Single(r=>r.name=="Thief drop-off points");int stash=0;
            foreach(Transform point in dropOff.transform)point.position=rooms[8+(stash++*3)%22].center+Vector3.up*.05f;
            scene.GetRootGameObjects().Single(r=>r.name=="Main Camera").transform.position=new Vector3(0,1.65f,-3);
            Physics.SyncTransforms();CompactStoreSetup.BuildMap();
            var failures=StoreRoomDressingSetup.Validate();
            if(failures.Length>0)throw new InvalidOperationException("Left unsaved: "+string.Join("; ",failures));
            var ids=All<NetworkObject>().Select(n=>new SerializedObject(n).FindProperty("SceneId").ulongValue).ToArray();
            if(ids.Any(id=>id==0)||ids.Distinct().Count()!=ids.Length)throw new InvalidOperationException("Network scene IDs changed");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            var report=new Report{count=30,garments=96,fixtures=fixtureCount,area=3808,width=56,depth=68,rooms=rooms.ToArray()};
            Directory.CreateDirectory("ArtSource/SmallStore30");File.WriteAllText("ArtSource/SmallStore30/layout.json",JsonUtility.ToJson(report,true));
            string result="PASS: 30 rooms, sides 10–12m, 56×68m footprint, 96 accessible garments, "+fixtureCount+" furniture groups; unique network IDs preserved.";
            File.WriteAllText("ArtSource/SmallStore30/validation.txt",result);return result;
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
    }
    private static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material,Quaternion? rotation=null,bool collider=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(position,rotation??Quaternion.identity);go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=material;
        if(!collider)Object.DestroyImmediate(go.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);return go;
    }
    private static void Boundary(Room r,bool horizontal,float at,float start,float end,float? door)
    {
        void Segment(float a,float b)
        {
            var pos=horizontal?new Vector3((a+b)/2,2.45f,at):new Vector3(at,2.45f,(a+b)/2);
            var size=horizontal?new Vector3(b-a,4.9f,.25f):new Vector3(.25f,4.9f,b-a);
            Box("Room wall",r.root,pos,size,wall);
            Box("Skirting",r.root,new Vector3(pos.x,.14f,pos.z),new Vector3(size.x+.03f,.28f,size.z+.03f),trim);
        }
        if(door==null){Segment(start,end);return;}
        Segment(start,door.Value-1.7f);Segment(door.Value+1.7f,end);
        Box("Passage lintel",r.root,horizontal?new Vector3(door.Value,4.15f,at):new Vector3(at,4.15f,door.Value),horizontal?new Vector3(3.4f,1.5f,.25f):new Vector3(.25f,1.5f,3.4f),trim);
    }
    private static void LightAndSigns(Room r,int index)
    {
        var lamp=new GameObject("Department light",typeof(Light));lamp.transform.SetParent(r.root,false);lamp.transform.SetPositionAndRotation(r.center+Vector3.up*4.35f,Quaternion.Euler(90,0,0));
        var light=lamp.GetComponent<Light>();light.type=LightType.Spot;light.range=15;light.spotAngle=120;light.innerSpotAngle=95;light.intensity=45;light.color=index%4==0?new Color(.74f,.87f,1):new Color(1,.86f,.73f);light.shadows=LightShadows.None;
        Box("Fluorescent housing",r.root,r.center+Vector3.up*4.75f,new Vector3(2.5f,.15f,.65f),metal,null,false);
        Box("Warm tube",r.root,r.center+Vector3.up*4.65f,new Vector3(2.2f,.08f,.42f),glow,null,false);
        var sign=new GameObject("Department sign",typeof(TextMeshPro));sign.transform.SetParent(r.root,false);sign.transform.position=new Vector3(r.center.x,3.6f,r.Z0+.18f);
        var text=sign.GetComponent<TextMeshPro>();text.font=ProjectSvinTheme.Current.bodyFont;text.fontSize=4.5f;text.text=r.name;text.color=new Color(1,.82f,.69f);text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(r.size.x-1,1);text.textWrappingMode=TextWrappingModes.NoWrap;text.enableAutoSizing=true;text.fontSizeMin=2.5f;text.fontSizeMax=4.5f;
    }
    private static void CreateCabinets(Room r,int count)
    {
        // Four corners leave both the horizontal and vertical through-routes free.
        var corners=new[]{new Vector2(-1,1),new Vector2(1,1),new Vector2(-1,-1),new Vector2(1,-1)};
        for(int n=0;n<count;n++)
        {
            var c=corners[n];var pos=new Vector3(c.x<0?r.X0+1.9f:r.X1-1.9f,0,c.y>0?r.Z1-1.1f:r.Z0+1.1f);
            var rotation=Quaternion.Euler(0,c.y>0?180:0,0);var root=new GameObject("Garment cabinet "+slots.Count.ToString("00")).transform;root.SetParent(r.root,false);root.SetPositionAndRotation(pos,rotation);
            Vector3 World(Vector3 p)=>pos+rotation*p;
            Box("Cabinet back",root,World(new Vector3(0,1.25f,-.58f)),new Vector3(2.6f,2.5f,.14f),wood,rotation);
            foreach(float side in new[]{-1.24f,1.24f})Box("Cabinet side",root,World(new Vector3(side,1.25f,0)),new Vector3(.12f,2.5f,1.15f),metal,rotation);
            Box("Cabinet canopy",root,World(new Vector3(0,2.55f,0)),new Vector3(2.6f,.1f,1.3f),wood,rotation);
            Box("Clothing shelf",root,World(new Vector3(0,.19f,0)),new Vector3(2.4f,.1f,1.15f),wood,rotation);
            slots.Add(new Pose(World(new Vector3(0,.26f,.03f)),rotation*Quaternion.Euler(0,180,0)));
        }
        Physics.SyncTransforms();
    }
    private static int Furnish(Room r,int index)
    {
        var types=index%6==0?new[]{"Shoes","Bench","Planter"}:index%6==1?new[]{"Stock","Workbench","Planter"}:index%6==2?new[]{"Denim shelf","Rail","Planter"}:index%6==3?new[]{"Accessories","Bench","Planter"}:new[]{"Rail","Shelf","Planter"};
        int count=0;
        foreach(var kind in types)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Modules+kind+".prefab");var shape=prefab.GetComponent<BoxCollider>();var half=shape.size*.5f;
            foreach(var corner in new[]{new Vector2(1,-1),new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,1)})
            {
                var pos=r.center+new Vector3(corner.x*(r.size.x/2-1.65f),0,corner.y*(r.size.z/2-1.4f));var rotation=Quaternion.Euler(0,corner.y>0?180:0,0);
                if(index==0&&pos.z < -3)continue;
                int overlaps=scene.GetPhysicsScene().OverlapBox(pos+Vector3.up*(half.y+.05f),new Vector3(half.x+.15f,Mathf.Max(.2f,half.y-.05f),half.z+.15f),hits,rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if(hits.Take(overlaps).Any(h=>h!=null&&h.GetComponentInParent<NetworkObject>()==null))continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,r.root);go.transform.SetPositionAndRotation(pos,rotation);Physics.SyncTransforms();count++;break;
            }
        }
        var poster=AssetDatabase.LoadAssetAtPath<GameObject>(Modules+"Poster.prefab");
        foreach(int side in new[]{-1,1})
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(poster,r.root);go.transform.SetPositionAndRotation(new Vector3(r.center.x+side*(r.size.x/2-.16f),3.1f,r.center.z+(index%2==0?-3:3)),Quaternion.Euler(0,-side*90,0));
        }
        return count;
    }
    private static void PlaceActors(Transform[] actors)
    {
        var used=new List<Vector3>();
        foreach(var actor in actors.OrderByDescending(t=>t.GetComponent<ShoppingCart>()!=null).ThenBy(t=>t.name,StringComparer.Ordinal))
        {
            if(actor.GetComponent<FixedFactoryDisplay>()!=null)
            {
                int index=Array.IndexOf(actors.Where(t=>t.GetComponent<FixedFactoryDisplay>()!=null).OrderBy(t=>t.name,StringComparer.Ordinal).ToArray(),actor);
                float side=index%2==0?-1:1;
                actor.SetPositionAndRotation(new Vector3(side*4,.03f,-6.3f+(index/2)*1.8f),Quaternion.Euler(0,-side*90,0));
                continue;
            }
            bool cart=actor.GetComponent<ShoppingCart>()!=null;float radius=cart?1.35f:.48f;bool found=false;
            foreach(var r in rooms.Skip(1))
            {
            if(found)break;
            foreach(var offset in new[]{Vector3.zero,new Vector3(-2,0,0),new Vector3(2,0,0),new Vector3(0,0,-2),new Vector3(0,0,2),new Vector3(-2,0,-2),new Vector3(2,0,2)})
            {
                var point=r.center+offset;
                if(used.Any(p=>Vector3.Distance(p,point)<(cart?3:1.7f)))continue;
                if(!NavMesh.SamplePosition(point,out var nav,.4f,NavMesh.AllAreas))continue;
                int count=scene.GetPhysicsScene().OverlapBox(nav.position+Vector3.up*1.2f,new Vector3(radius,1.1f,radius),hits,Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if(hits.Take(count).Any(h=>h!=null&&h.GetComponentInParent<NetworkObject>()==null))continue;
                actor.SetPositionAndRotation(nav.position+Vector3.up*.03f,Quaternion.Euler(0,used.Count*83%360,0));used.Add(nav.position);found=true;break;
            }
            }
            if(!found)throw new InvalidOperationException("No authored actor position: "+actor.name);
        }
    }
}
