using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using FishNet.Object;

// Run through Unity Pipeline run_script. Kept outside Assets to avoid domain reloads.
public static class BuildPeachDepartmentStore
{
    [Serializable] public class Layout { public Room[] rooms; public Wall[] walls; public Placement[] pickups, mannequins; public Fixture[] props; }
    [Serializable] public class Room { public string id,name,color; public float x0,z0,x1,z1,height; }
    [Serializable] public class Door { public float start,end; }
    [Serializable] public class Wall { public string axis; public float fixedValue,start,end,height; public Door[] doors; }
    [Serializable] public class Placement { public string kind; public float x,y,z,yaw; }
    [Serializable] public class Fixture { public string kind; public float x,z,w,l; }
    const string Art = "Assets/Art/LevelDesign";
    const string Mats = "Assets/Materials/PeachStore";
    static Transform root,architecture,furniture,decor,gameplay,lighting;
    static Material plaster,metal,wood,cream,peach,teal,glow,glass;
    static Dictionary<string,GameObject> pickupSources;
    static Dictionary<string,ClothingDefinition> definitions;
    static Layout layout;

    public static string Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open SampleScene in Edit Mode first.");
        EnsureFolder(Mats); EnsureFolder(Art);
        // Save a recoverable snapshot of the user's current scene before rebuilding its geometry.
        EditorSceneManager.SaveScene(scene);
        string backup = "Docs/LevelDesign/BeforePeachStore.unity";
        if (!File.Exists(backup)) File.Copy(scene.path,backup);
        string json=File.ReadAllText("Docs/LevelDesign/PeachStoreLayout.json").Replace("\"fixed\":","\"fixedValue\":");
        layout=Newtonsoft.Json.JsonConvert.DeserializeObject<Layout>(json);
        if(layout?.rooms==null||layout.walls==null||layout.pickups==null)throw new InvalidOperationException("Invalid level layout specification.");
        pickupSources=new Dictionary<string,GameObject> {
            {"hat",Find("Pickup Red Head")},{"shirt",Find("Pickup Yellow Torso")},
            {"pants",Find("Pickup Green Legs")},{"shoes",Find("Pickup Blue Shoes")},{"hoodie",Find("Pickup hoodie1")}
        };
        foreach(var pair in pickupSources)
            if(pair.Value==null || pair.Value.GetComponent<ClothingPickup>()?.Clothing==null)
                throw new InvalidOperationException("Missing configured pickup: "+pair.Key);
        definitions=pickupSources.ToDictionary(p=>p.Key,p=>p.Value.GetComponent<ClothingPickup>().Clothing);
        GameObject stalker=Find("Mannequin_Online"),watcher=Find("Mannequin_Watcher_Online"),display=Find("White Animated Character");
        GameObject[] thieves=Enumerable.Range(0,5).Select(i=>Find(i==0?"Mannequin_Thief_Online":"Mannequin_Thief_Online ("+i+")")).ToArray();
        if(stalker==null||watcher==null||display==null||thieves.Any(t=>t==null)) throw new InvalidOperationException("Existing mannequins missing.");
        foreach(Transform child in display.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Display clothing - ")).ToArray())
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        string[] keptNames={"Player Mirror","ThiefNest","sneakers2","sneakers2 (1)","pants1","Hat1 (2)","Shirt1 (3)","Hat1"};
        GameObject[] kept=keptNames.Select(Find).Where(g=>g!=null).ToArray();
        Undo.IncrementCurrentGroup(); int undoGroup=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Build store after closing");
        foreach(var go in pickupSources.Values.Concat(thieves).Concat(new[]{stalker,watcher,display}).Concat(kept))
            Undo.SetTransformParent(go.transform,null,"Preserve original gameplay object");
        var old=Find("Graybox Level"); if(old!=null) Undo.DestroyObjectImmediate(old);
        root=Group("Graybox Level",null); Undo.RegisterCreatedObjectUndo(root.gameObject,"Build store");
        architecture=Group("Architecture - 10 distinct spaces",root); furniture=Group("Shelving, cover and fitting maze",root);
        decor=Group("Art, signage and clothing displays",root); gameplay=Group("48 Pickups and 16 Mannequins",root); lighting=Group("Store lighting",root);
        plaster=Mat("Warm plaster",new Color(.58f,.55f,.49f)); metal=Mat("Dark steel",new Color(.065f,.085f,.095f),.4f);
        wood=Mat("Walnut display",new Color(.24f,.13f,.085f)); cream=Mat("Cream fixtures",new Color(.68f,.63f,.52f));
        peach=Mat("Peach accent",new Color(.7f,.28f,.16f)); teal=Mat("Petrol accent",new Color(.07f,.23f,.23f));
        glow=Mat("Warm diffuser",new Color(.95f,.82f,.6f),.2f,new Color(1f,.7f,.4f)*2.1f);
        glass=Mat("Smoked panel",new Color(.16f,.22f,.24f),.65f);
        BuildRooms(); BuildWalls(); BuildUpperRoute(); BuildFittingMaze(); BuildFixtures(); BuildPoster();
        PlacePickups(); PlaceMannequins(stalker,watcher,thieves,display);
        PlaceExistingDecor(kept);
        BuildLighting(); ConfigureScene();
        AssignNetworkIds(scene);
        Physics.SyncTransforms();
        var surface=Find("NavMesh").GetComponent<NavMeshSurface>();
        Undo.RecordObject(surface,"Bake store navigation"); surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
        // Collect the current scene only, including ramp colliders. Pickups/decor remain excluded.
        surface.collectObjects=CollectObjects.All;
        surface.BuildNavMesh();
        EditorUtility.SetDirty(surface);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject=root.gameObject;
        if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.LookAt(new Vector3(1,1.5f,13),Quaternion.Euler(47,-28,0),38,false);
        return "Built 10 spaces, 48 working clothing pickups, 16 mannequins, raised bridge and two ramps, fitting maze, stockroom and framed peach poster. Scene saved; NavMesh baked.";
    }

    static void BuildRooms()
    {
        foreach(Room r in layout.rooms)
        {
            ColorUtility.TryParseHtmlString(r.color,out Color c);
            Transform room=Group(r.id+" "+r.name,architecture);
            Material floor=Mat("Floor "+r.id,c*.52f,.19f);
            Box("Floor",room,new Vector3((r.x0+r.x1)/2,-.15f,(r.z0+r.z1)/2),new Vector3(r.x1-r.x0,.3f,r.z1-r.z0),floor);
            Box("Ceiling",room,new Vector3((r.x0+r.x1)/2,r.height+.12f,(r.z0+r.z1)/2),new Vector3(r.x1-r.x0,.24f,r.z1-r.z0),plaster);
            // Tile joints and alternating borders add scale without adding collision clutter.
            for(float x=r.x0+2;x<r.x1;x+=2)
                Box("Floor tile joint",room,new Vector3(x,.002f,(r.z0+r.z1)/2),new Vector3(.015f,.004f,r.z1-r.z0),metal,false);
            for(float z=r.z0+2;z<r.z1;z+=2)
                Box("Floor tile joint",room,new Vector3((r.x0+r.x1)/2,.002f,z),new Vector3(r.x1-r.x0,.004f,.015f),metal,false);
        }
        // Taller enclosing walls above the side rooms frame the seven-metre atrium.
        Box("Atrium west clerestory",architecture,new Vector3(-10,6,9),new Vector3(.28f,2.4f,18),teal);
        Box("Atrium east clerestory",architecture,new Vector3(10,6,9),new Vector3(.28f,2.4f,18),teal);
        Box("Atrium north clerestory",architecture,new Vector3(0,6,18),new Vector3(20,2.4f,.28f),teal);
        Box("Atrium south clerestory",architecture,new Vector3(0,6,0),new Vector3(10,2.4f,.28f),teal);
        for(int i=0;i<4;i++)
            Box("Suspended atrium beam",architecture,new Vector3(0,6.95f,3+i*4),new Vector3(20,.3f,.2f),metal);
    }
    static void BuildWalls()
    {
        int n=0;
        foreach(Wall w in layout.walls)
        {
            Transform parent=Group("Partition "+(++n),architecture);float cursor=w.start;
            foreach(Door d in w.doors.Concat(new[]{new Door{start=w.end,end=w.end}}))
            {
                if(d.start>cursor) Segment(w,parent,cursor,d.start,0,w.height,true);
                if(d.end>d.start)
                {
                    Segment(w,parent,d.start,d.end,2.9f,w.height-2.9f,false);
                    Vector3 pos=w.axis=="h"?new Vector3((d.start+d.end)/2,0,w.fixedValue):new Vector3(w.fixedValue,0,(d.start+d.end)/2);
                    Vector3 size=w.axis=="h"?new Vector3(d.end-d.start,.008f,.24f):new Vector3(.24f,.008f,d.end-d.start);
                    Box("Open doorway threshold",parent,pos+Vector3.up*.008f,size,peach,false);
                }
                cursor=d.end;
            }
        }
        Sign("AFTER HOURS",new Vector3(0,3.45f,.17f),0,2.9f);
        Sign("03 / BOUTIQUE",new Vector3(-9.82f,3.35f,4.5f),90,2.8f);
        Sign("04 / ATELIER",new Vector3(9.82f,3.35f,4.5f),270,2.8f);
        Sign("FITTING ROOMS",new Vector3(-20.5f,3.35f,11.82f),0,2.8f);
        Sign("COLLECTION ARCHIVE",new Vector3(-4.5f,3.35f,27.82f),0,3.4f);
        Sign("STAFF ONLY",new Vector3(20.5f,3.35f,27.82f),0,2.6f);
        Sign("GALLERY",new Vector3(-5,3.35f,17.82f),0,2.8f);
    }
    static void Segment(Wall w,Transform parent,float a,float b,float bottom,float height,bool trim)
    {
        bool h=w.axis=="h";
        Box("Solid wall",parent,h?new Vector3((a+b)/2,bottom+height/2,w.fixedValue):new Vector3(w.fixedValue,bottom+height/2,(a+b)/2),
            h?new Vector3(b-a+.28f,height,.28f):new Vector3(.28f,height,b-a+.28f),plaster);
        if(trim)
        {
            Box("Lower colour band",parent,h?new Vector3((a+b)/2,.48f,w.fixedValue):new Vector3(w.fixedValue,.48f,(a+b)/2),
                h?new Vector3(b-a,.86f,.295f):new Vector3(.295f,.86f,b-a),teal,false);
            Box("Skirting",parent,h?new Vector3((a+b)/2,.075f,w.fixedValue):new Vector3(w.fixedValue,.075f,(a+b)/2),
                h?new Vector3(b-a,.15f,.32f):new Vector3(.32f,.15f,b-a),metal,false);
        }
    }
    static void BuildUpperRoute()
    {
        Transform upper=Group("Upper escape route +2.6m",architecture);
        for(int i=0;i<2;i++)
        {
            float x=i==0?-7.3f:7.3f;
            Ramp("Walkable ramp",upper,new Vector3(x,0,3),2.4f,11,2.6f,wood);
            for(int side=-1;side<=1;side+=2)
                Ramp("Sloped guardrail",upper,new Vector3(x+side*1.2f,1.03f,3),.09f,11,2.6f,metal);
            for(int j=0;j<=4;j++)
                foreach(float side in new[]{-1f,1f})
                    Box("Ramp rail post",upper,new Vector3(x+side*1.2f,.55f+j*.65f,3+j*2.75f),new Vector3(.075f,1.1f,.075f),metal);
        }
        Box("Bridge deck",upper,new Vector3(0,2.5f,15.35f),new Vector3(17,.2f,2.7f),wood);
        Box("North bridge rail",upper,new Vector3(0,3.62f,16.72f),new Vector3(17,.1f,.09f),metal);
        Box("South bridge rail",upper,new Vector3(0,3.62f,13.97f),new Vector3(12,.1f,.09f),metal);
        for(int i=-8;i<=8;i+=2)
        {
            Box("Bridge baluster",upper,new Vector3(i,3.1f,16.72f),new Vector3(.07f,1.1f,.07f),metal);
            if(Math.Abs(i)<=6)Box("Bridge baluster",upper,new Vector3(i,3.1f,13.97f),new Vector3(.07f,1.1f,.07f),metal);
        }
        foreach(float x in new[]{-7.3f,7.3f})Box("Bridge support",upper,new Vector3(x,1.2f,15.2f),new Vector3(.3f,2.4f,.3f),metal);
        var stage=GameObject.CreatePrimitive(PrimitiveType.Cylinder);stage.name="Circular fashion podium";stage.transform.SetParent(furniture,false);
        stage.transform.position=new Vector3(0,.25f,9);stage.transform.localScale=new Vector3(6,.25f,5);
        UnityEngine.Object.DestroyImmediate(stage.GetComponent<Collider>());
        var collider=stage.AddComponent<MeshCollider>();collider.sharedMesh=stage.GetComponent<MeshFilter>().sharedMesh;
        stage.GetComponent<Renderer>().sharedMaterial=peach;
        Ramp("Podium access",furniture,new Vector3(0,0,4),2,3,.5f,wood);
        // Vertical coloured panels make the central stage a strong landmark.
        foreach(float x in new[]{-3.8f,3.8f})
        {
            Box("Suspended fashion banner",decor,new Vector3(x,4.8f,9),new Vector3(1.2f,2.3f,.06f),x<0?peach:teal,false);
            Sign(x<0?"LOOK":"AGAIN",new Vector3(x,4.8f,8.95f),0,.85f);
        }
    }
    static void BuildFittingMaze()
    {
        Transform maze=Group("Fitting maze - offset openings",furniture);
        SegmentMaze(-20,14,18);SegmentMaze(-20,20,22);SegmentMaze(-20,24,26);SegmentMaze(-15,16,22);SegmentMaze(-15,24,26);
        // Short stall walls create blind turns, while each bay stays reachable.
        foreach(float z in new[]{17f,21f,25f})
        {
            Box("Stall separator",maze,new Vector3(-22,1.35f,z),new Vector3(4,2.7f,.12f),cream);
            Box("Half-open curtain",maze,new Vector3(-20.08f,1.3f,z-.7f),new Vector3(.06f,2.6f,.9f),peach);
        }
        Box("Fitting bench",maze,new Vector3(-12,.3f,20),new Vector3(.8f,.6f,3),wood);
        void SegmentMaze(float x,float a,float b)=>Box("Tall sight blocker",maze,new Vector3(x,1.45f,(a+b)/2),new Vector3(.14f,2.9f,b-a),cream);
    }
    static void BuildFixtures()
    {
        int n=0;
        foreach(var f in layout.props)
        {
            Transform fixture=Group((f.kind=="shelf"?"Stock shelving ":"Fashion rail ")+(++n),furniture);
            float h=f.kind=="shelf"?2.65f:2.05f;
            foreach(float dx in new[]{-f.w/2,f.w/2})foreach(float dz in new[]{-f.l/2,f.l/2})
                Box("Steel upright",fixture,new Vector3(f.x+dx,h/2,f.z+dz),new Vector3(.09f,h,.09f),metal);
            if(f.kind=="shelf")
            {
                for(int j=0;j<4;j++)
                    Box("Shelf board",fixture,new Vector3(f.x,.25f+j*.65f,f.z),new Vector3(f.w,.08f,f.l),wood);
                // Mixed crates also close sight lines, creating cover in the stock aisles.
                for(int j=0;j<5;j++)
                {
                    float dz=f.l>f.w?(j-2)*f.l/5:0;
                    float dx=f.w>f.l?(j-2)*f.w/5:0;
                    Box("Stock carton",fixture,new Vector3(f.x+dx,.58f+(j%2)*.65f,f.z+dz),new Vector3(Mathf.Min(.95f,f.w*.8f),.58f,Mathf.Min(.95f,f.l*.8f)),j%2==0?cream:peach);
                }
            }
            else
            {
                Box("Top hanging rail",fixture,new Vector3(f.x,h,f.z),new Vector3(.09f,.09f,f.l),metal);
                Box("Clothes obscure sight",fixture,new Vector3(f.x,1.15f,f.z),new Vector3(.55f,1.65f,f.l-.3f),teal);
                for(int j=0;j<6;j++) DecorativeClothing(definitions[j%2==0?"shirt":"hoodie"],new Vector3(f.x-.4f,1.2f,f.z-f.l/2+.5f+j*(f.l-1)/5),.72f,90,fixture);
            }
        }
        // Entrance counters and a small sculptural installation in the gallery.
        foreach(float x in new[]{-3.7f,3.7f})
        {
            Box("Checkout counter",furniture,new Vector3(x,.5f,-1.2f),new Vector3(1.6f,1,1.2f),wood);
            Box("Countertop",furniture,new Vector3(x,1.04f,-1.2f),new Vector3(1.75f,.08f,1.3f),cream);
        }
        foreach(float x in new[]{-7f,-4f,-1f})
            Box("Gallery sculptural plinth",furniture,new Vector3(x,.5f,23),new Vector3(.9f,1,.9f),peach);
        for(int i=0;i<6;i++)DecorativeClothing(definitions[i%2==0?"hat":"shoes"],new Vector3(-7+(i%3)*3,1.35f,22.7f+(i/3)*.6f),.48f,0,decor);
        for(int j=0;j<5;j++)
            Box("Nest packing carton",furniture,new Vector3(17+(j%3)*1.1f,.33f+(j/3)*.6f,34+(j/3)*.8f),new Vector3(.95f,.6f,.9f),j%2==0?cream:wood);
    }
    static void PlacePickups()
    {
        var used=new HashSet<string>();int n=0;
        foreach(var p in layout.pickups)
        {
            GameObject go;
            if(used.Add(p.kind))go=pickupSources[p.kind];
            else {go=UnityEngine.Object.Instantiate(pickupSources[p.kind]);go.name="Store Pickup "+(++n)+" "+p.kind;}
            go.transform.SetParent(gameplay,true);go.transform.position=new Vector3(p.x,p.y+1.12f,p.z);
            go.transform.localScale=Vector3.one*(p.y>0?.5f:.65f);go.transform.rotation=Quaternion.Euler(0,((n*47)%120)-60,0);
            foreach(var col in go.GetComponentsInChildren<Collider>(true))col.isTrigger=true;
            var mod=go.GetComponent<NavMeshModifier>()??go.AddComponent<NavMeshModifier>();mod.ignoreFromBuild=true;
            SerializedObject so=new SerializedObject(go.GetComponent<PickupItem>());
            so.FindProperty("sparkleRate").floatValue=0f;so.ApplyModifiedPropertiesWithoutUndo();
            float baseY=p.y;
            Box("Clothing display pedestal",furniture,new Vector3(p.x,baseY+.33f,p.z),new Vector3(p.y>0?.65f:1.0f,.66f,p.y>0?.5f:.85f),n%2==0?cream:wood);
        }
    }
    static void PlaceMannequins(GameObject stalker,GameObject watcher,GameObject[] thieves,GameObject display)
    {
        int st=0,wa=0,th=0,ds=0;
        foreach(var p in layout.mannequins)
        {
            GameObject go;
            if(p.kind=="thief")go=thieves[th++];
            else
            {
                GameObject source=p.kind=="stalker"?stalker:p.kind=="watcher"?watcher:display;
                int index=p.kind=="stalker"?st++:p.kind=="watcher"?wa++:ds++;
                go=index==0?source:UnityEngine.Object.Instantiate(source);
                if(index>0)go.name="Store "+p.kind+" "+index;
            }
            go.transform.SetParent(gameplay,true);go.transform.SetPositionAndRotation(new Vector3(p.x,p.y+.02f,p.z),Quaternion.Euler(0,p.yaw,0));
            foreach(var component in go.GetComponents<MonoBehaviour>())
            {
                var so=new SerializedObject(component);var field=so.FindProperty("showStateLabel");
                if(field!=null){field.boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();}
            }
            if(p.kind=="display")
            {
                var animator=go.GetComponent<Animator>();var sample=stalker.GetComponentInChildren<Animator>(true);
                animator.avatar=sample.avatar;animator.runtimeAnimatorController=sample.runtimeAnimatorController;
                animator.applyRootMotion=false;animator.Rebind();animator.Update(0);
                DecorativeClothing(definitions[ds%2==0?"shirt":"hoodie"],go.transform.position+new Vector3(0,1.14f,-.18f),.75f,p.yaw,go.transform);
                ThrowableMannequinSetup.Configure(go);
            }
            if(p.kind=="stalker"||p.kind=="thief")
            {
                // Their live colliders must not cut holes into the baked navigation surface.
                var mod=go.GetComponent<NavMeshModifier>()??go.AddComponent<NavMeshModifier>();mod.ignoreFromBuild=true;
            }
        }
        var nest=Find("ThiefNest");nest.transform.SetParent(gameplay,true);nest.transform.position=new Vector3(18,0,32);
        foreach(var thief in thieves)
        {
            var so=new SerializedObject(thief.GetComponent<ThiefBrain>());so.FindProperty("nest").objectReferenceValue=nest.transform;so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
    static void PlaceExistingDecor(GameObject[] kept)
    {
        var mirror=kept.FirstOrDefault(g=>g.name=="Player Mirror");
        if(mirror!=null){mirror.transform.SetParent(decor,true);mirror.transform.SetPositionAndRotation(new Vector3(-23.75f,1.4f,15),Quaternion.Euler(0,90,0));}
        int n=0;
        foreach(var go in kept.Where(g=>g!=mirror&&g.name!="ThiefNest"))
        {go.transform.SetParent(decor,true);go.transform.position=new Vector3(11.3f+(n%3)*2.1f,.75f,32+(n/3)*1.7f);n++;}
    }
    static void BuildPoster()
    {
        AssetDatabase.ImportAsset(Art+"/PeachPoster.png",ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(Art+"/PeachPoster.png");
        importer.textureType=TextureImporterType.Default;importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.SaveAndReimport();
        Material image=Mat("Peach poster",Color.white,0,null,true);
        image.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/PeachPoster.png"));EditorUtility.SetDirty(image);
        Box("Peach poster frame",decor,new Vector3(0,2.2f,-7.79f),new Vector3(2.9f,2.9f,.12f),metal);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="The Peach - original generated illustration";quad.transform.SetParent(decor,false);
        quad.transform.SetPositionAndRotation(new Vector3(0,2.2f,-7.715f),Quaternion.Euler(0,180,0));quad.transform.localScale=new Vector3(2.7f,2.7f,1);
        UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());quad.GetComponent<Renderer>().sharedMaterial=image;
        Sign("PEACH / NEW COLLECTION",new Vector3(0,3.9f,-7.68f),180,3.4f);
    }
    static void BuildLighting()
    {
        foreach(var r in layout.rooms)
        {
            float x=(r.x0+r.x1)/2,z=(r.z0+r.z1)/2;
            if(r.id=="02")
            {
                foreach(var v in new[]{new Vector3(-5,6.5f,5),new Vector3(5,6.5f,5),new Vector3(-5,6.5f,14),new Vector3(5,6.5f,14)})Lamp("Atrium pool",v,4.5f,14,new Color(1,.79f,.59f),true);
                Lamp("Runway peach accent",new Vector3(0,4,9),2.7f,6,new Color(1,.38f,.2f),false);
            }
            else
            {
                bool shadow=r.id=="05"||r.id=="08";
                Color colour=r.id=="08"?new Color(.61f,.78f,.86f):r.id=="10"?new Color(1,.53f,.3f):new Color(1,.84f,.65f);
                Lamp(r.name+" pool A",new Vector3(x-2,r.height-.5f,z-2),r.id=="08"?2.3f:3.4f,12,colour,shadow);
                if(r.x1-r.x0>12||r.z1-r.z0>12)Lamp(r.name+" pool B",new Vector3(x+3,r.height-.5f,z+3),3.1f,12,colour,false);
            }
        }
        Lamp("Poster warm pool",new Vector3(0,4,-5),3f,7,new Color(1,.76f,.53f),false);
    }
    static void ConfigureScene()
    {
        var sun=Find("Directional Light");if(sun!=null)sun.SetActive(false);
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.24f,.26f,.28f);
        RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.2f;RenderSettings.fog=false;
        Camera cam=Find("Main Camera").GetComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.018f,.024f,.03f);
        cam.farClipPlane=100;cam.transform.SetPositionAndRotation(new Vector3(0,1.7f,-3),Quaternion.identity);
        var player=Find("Player");if(player!=null)player.transform.position=new Vector3(0,.05f,-3);
    }
    static void AssignNetworkIds(UnityEngine.SceneManagement.Scene scene)
    {
        var create=typeof(NetworkObject).GetMethod("CreateSceneId",BindingFlags.Static|BindingFlags.NonPublic);
        var serialize=typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",BindingFlags.Instance|BindingFlags.NonPublic);
        if(create==null||serialize==null)throw new InvalidOperationException("FishNet scene-ID API missing.");
        object[] args={scene,true,0};var objects=(List<NetworkObject>)create.Invoke(null,args);
        foreach(var obj in objects)serialize.Invoke(obj,new object[]{true,false});
    }
    static void DecorativeClothing(ClothingDefinition definition,Vector3 pos,float maxSize,float yaw,Transform parent)
    {
        var holder=Group("Display clothing - "+definition.DisplayName,parent);holder.position=pos;holder.rotation=Quaternion.Euler(0,yaw,0);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(definition.Model,holder);
        model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(definition.DisplayRotation);model.transform.localScale=Vector3.one;
        foreach(var col in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(col);
        var renderers=model.GetComponentsInChildren<Renderer>(true);Bounds bounds=new Bounds();bool first=true;
        foreach(var renderer in renderers)
        {
            if(definition.FabricMaterial!=null)renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>definition.FabricMaterial).ToArray();
            Bounds b=renderer.bounds;var corners=new[]{b.min,b.max,new Vector3(b.min.x,b.max.y,b.min.z),new Vector3(b.max.x,b.min.y,b.max.z)};
            foreach(var c in corners){var v=holder.InverseTransformPoint(c);if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}
        }
        float scale=maxSize/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z,.001f);model.transform.localScale=Vector3.one*scale;model.transform.localPosition=-bounds.center*scale;
        var mod=holder.gameObject.AddComponent<NavMeshModifier>();mod.ignoreFromBuild=true;
    }
    static void Sign(string text,Vector3 position,float yaw,float width)
    {
        Transform sign=Group("Sign - "+text,decor);sign.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        Box("Sign board",sign,Vector3.zero,new Vector3(width,.5f,.05f),metal,false,true);
        var go=new GameObject("Lettering",typeof(TextMesh));go.transform.SetParent(sign,false);go.transform.localPosition=new Vector3(0,0,-.035f);
        var tm=go.GetComponent<TextMesh>();tm.text=text;tm.fontSize=48;tm.characterSize=Mathf.Min(.035f,width/(text.Length*1.9f));tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=new Color(.97f,.82f,.62f);
        go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }
    static void Lamp(string name,Vector3 pos,float intensity,float range,Color colour,bool shadows)
    {
        var group=Group(name,lighting);group.position=pos;
        Box("Ceiling housing",group,new Vector3(0,.22f,0),new Vector3(1.4f,.15f,.32f),metal,false,true);
        Box("Light diffuser",group,new Vector3(0,.12f,0),new Vector3(1.25f,.04f,.23f),glow,false,true);
        var source=new GameObject("Light source",typeof(Light));source.transform.SetParent(group,false);
        var light=source.GetComponent<Light>();light.type=shadows?LightType.Spot:LightType.Point;light.intensity=intensity*(shadows?3:1);light.range=range;light.color=colour;
        if(shadows){light.spotAngle=125;light.innerSpotAngle=85;source.transform.localRotation=Quaternion.Euler(90,0,0);}
        light.shadows=shadows?LightShadows.Soft:LightShadows.None;light.shadowBias=.02f;light.shadowNormalBias=.1f;light.lightmapBakeType=LightmapBakeType.Realtime;
    }
    static void Ramp(string name,Transform parent,Vector3 near,float width,float length,float rise,Material mat)
    {
        float angle=Mathf.Atan2(rise,length)*Mathf.Rad2Deg;
        var ramp=Box(name,parent,near+new Vector3(0,rise/2-.09f*Mathf.Cos(angle*Mathf.Deg2Rad),length/2),new Vector3(width,.18f,Mathf.Sqrt(length*length+rise*rise)),mat);
        ramp.transform.rotation=Quaternion.Euler(-angle,0,0);
    }
    static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,Material material,bool solid=true,bool local=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
        if(local)go.transform.localPosition=pos;else go.transform.position=pos;
        go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
        if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static Transform Group(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    static GameObject Find(string name)=>EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name==name)?.gameObject;
    static Material Mat(string name,Color color,float smooth=.12f,Color? emission=null,bool unlit=false)
    {
        string path=Mats+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",smooth);
        if(emission.HasValue){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",emission.Value);}
        EditorUtility.SetDirty(mat);return mat;
    }
    static void EnsureFolder(string folder)
    {
        string parent="Assets";foreach(string part in folder.Substring(7).Split('/')){string path=parent+"/"+part;if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,part);parent=path;}
    }
}
