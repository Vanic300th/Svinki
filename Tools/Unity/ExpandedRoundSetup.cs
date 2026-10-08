using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using FishNet.Object;

public static class ExpandedRoundSetup
{
    private static Scene scene;
    private static Transform wing;
    private static Material plaster, wood, metal;
    private static T[] Items<T>() where T:Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
        scene=SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        if(!scene.IsValid()||!scene.isLoaded) scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);
        Scene previous=SceneManager.GetActiveScene();SceneManager.SetActiveScene(scene);
        try
        {
            Transform level=scene.GetRootGameObjects().Single(g=>g.name=="Graybox Level").transform;
            var definitions=AssetDatabase.FindAssets("t:ClothingDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ClothingDefinition>).Where(c=>c!=null).ToArray();
            plaster=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PeachStore/Warm plaster.mat");
            wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PeachStore/Walnut display.mat");
            metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PeachStore/Dark steel.mat");
            double oldArea=Items<BoxCollider>().Where(b=>b.name=="Floor"&&b.bounds.center.y<.3f&&!b.transform.IsChildOf(level.Find("North departments - triple area"))).Sum(b=>(double)b.bounds.size.x*b.bounds.size.z);
            wing=level.Find("North departments - triple area");
            if(wing==null)
            {
                var oldFloors=Items<BoxCollider>().Where(b=>b.name=="Floor"&&b.bounds.center.y<.3f).ToArray();
                oldArea=oldFloors.Sum(b=>(double)b.bounds.size.x*b.bounds.size.z);
                wing=new GameObject("North departments - triple area").transform;wing.SetParent(level,false);
                float width=65,depth=(float)(oldArea*2/width/4),start=65;
                string[] names={"COLOUR LAB","DENIM","STREETWEAR","TAILORING","NEON","SPORTSWEAR","SUMMER","WINTER","PRINT STUDIO","FOOTWEAR","ACCESSORIES","COLLECTIONS","COUTURE","CARGO","LIFESTYLE","OUTLET"};
                var pickups=Items<ClothingPickup>().Where(p=>p.Clothing!=null).ToArray();
                for(int row=0;row<4;row++)for(int col=0;col<4;col++)
                {
                    int index=row*4+col;float a=-30+col*width/4,b=a+width/4,z0=start+row*depth,z1=z0+depth,mx=(a+b)/2,mz=(z0+z1)/2;
                    var room=new GameObject((15+index)+" / "+names[index]).transform;room.SetParent(wing,false);
                    Box("Floor",room,new Vector3(mx,-.15f,mz),new Vector3(b-a,.3f,depth),AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PeachStore/Floor "+((index%8)+3).ToString("00")+".mat"));
                    Box("Ceiling",room,new Vector3(mx,4.92f,mz),new Vector3(b-a,.24f,depth),plaster);
                    if(row==3) Box("North wall",room,new Vector3(mx,2.4f,z1),new Vector3(b-a,4.8f,.28f),plaster);
                    else DoorHorizontal(room,a,b,z1,mx,4);
                    if(col==0)Box("West wall",room,new Vector3(a,2.4f,mz),new Vector3(.28f,4.8f,depth),plaster);
                    if(col==3)Box("East wall",room,new Vector3(b,2.4f,mz),new Vector3(.28f,4.8f,depth),plaster);
                    else DoorVertical(room,b,z0,z1,mz,4);
                    // Cover sits at the edges, leaving the middle routes clear for carts.
                    Box("Display island",room,new Vector3(a+3,.45f,mz+3),new Vector3(3,.9f,2),wood);
                    Box("Low rack",room,new Vector3(b-2,.62f,mz-3),new Vector3(1.3f,1.24f,4),metal);
                    for(int k=0;k<3;k++)
                    {
                        var copy=UnityEngine.Object.Instantiate(pickups[(index*3+k)%pickups.Length].gameObject,room);
                        copy.name="Department garment "+index+"-"+k;
                        copy.transform.position=new Vector3(mx+(k-1)*3,1.05f,mz+5);
                    }
                    var sign=new GameObject("Department sign",typeof(TextMesh));sign.transform.SetParent(room,false);
                    sign.transform.position=new Vector3(mx,3.5f,z0+.2f);sign.transform.rotation=Quaternion.Euler(0,180,0);
                    var text=sign.GetComponent<TextMesh>();text.text=names[index];text.fontSize=64;text.characterSize=.04f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(1,.75f,.48f);
                    foreach(float offset in new[]{-depth*.24f,depth*.24f})
                    {
                        var lamp=new GameObject("Department light",typeof(Light));lamp.transform.SetParent(room,false);lamp.transform.position=new Vector3(mx,4.2f,mz+offset);lamp.transform.rotation=Quaternion.Euler(90,0,0);
                        var light=lamp.GetComponent<Light>();light.type=LightType.Spot;light.range=18;light.spotAngle=110;light.innerSpotAngle=70;light.intensity=12;light.color=new Color(1,.87f,.72f);light.shadows=LightShadows.None;
                    }
                }
                // Open four wide connections in the previous north boundary.
                var oldWing=level.Find("Expanded north wing - v1");
                for(int i=0;i<oldWing.childCount;i++)
                {
                    Transform room=oldWing.GetChild(i);Transform wall=room.Find("Back wall");
                    if(wall==null)continue;var bounds=wall.GetComponent<BoxCollider>().bounds;
                    UnityEngine.Object.DestroyImmediate(wall.gameObject);
                    DoorHorizontal(room,bounds.min.x,bounds.max.x,65,(bounds.min.x+bounds.max.x)/2,4);
                }
            }
            Physics.SyncTransforms();
            // Twelve aggressive stalkers in total. Additional ones occupy distinct departments.
            var stalkers=Items<MannequinBrain>();var source=stalkers.First().gameObject;
            for(int i=stalkers.Length;i<12;i++)
            {
                int n=i-stalkers.Length;var room=wing.GetChild(n%wing.childCount);
                var floor=room.Find("Floor").GetComponent<BoxCollider>().bounds;
                var copy=UnityEngine.Object.Instantiate(source,level);copy.name="Aggressive mannequin "+(i+1);
                copy.transform.position=new Vector3(floor.center.x, .02f, floor.center.z-3);
                copy.transform.rotation=Quaternion.Euler(0,180,0);
                var data=new SerializedObject(copy.GetComponent<MannequinBrain>());data.FindProperty("target").objectReferenceValue=null;data.FindProperty("attackCooldown").floatValue=1.2f;data.ApplyModifiedPropertiesWithoutUndo();
            }
            var added=Items<MannequinBrain>().Where(b=>b.name.StartsWith("Aggressive mannequin ")).OrderBy(b=>int.Parse(b.name.Split(' ').Last())).ToArray();
            for(int i=0;i<added.Length;i++) {int index=Mathf.RoundToInt(i*15f/(added.Length-1));var floor=wing.GetChild(index).Find("Floor").GetComponent<BoxCollider>().bounds.center;added[i].transform.position=new Vector3(floor.x,.02f,floor.z-3);}
            var carts=Items<ShoppingCart>().OrderBy(c=>c.name,StringComparer.Ordinal).ToArray();
            for(int i=3;i<carts.Length;i++)UnityEngine.Object.DestroyImmediate(carts[i].gameObject);
            Vector3[] cartPoints={new Vector3(-3.7f,.03f,-6),new Vector3(-6,.03f,55),wing.GetChild(10).Find("Floor").GetComponent<BoxCollider>().bounds.center+Vector3.up*.18f};
            for(int i=0;i<3;i++)
            {
                var cart=carts[i];cart.transform.position=cartPoints[i];cart.transform.rotation=Quaternion.identity;
                var cargo=cart.GetComponent<CartCargo>();if(cargo==null)cargo=cart.gameObject.AddComponent<CartCargo>();
                var data=new SerializedObject(cargo);var list=data.FindProperty("catalog");list.arraySize=definitions.Length;
                for(int n=0;n<definitions.Length;n++)list.GetArrayElementAtIndex(n).objectReferenceValue=definitions[n];data.ApplyModifiedPropertiesWithoutUndo();
                var rb=cart.GetComponent<Rigidbody>();rb.mass=32;rb.isKinematic=true;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                rb.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;rb.interpolation=RigidbodyInterpolation.Interpolate;
                var collider=cart.GetComponent<BoxCollider>();collider.center=new Vector3(0,.62f,0);collider.size=new Vector3(1.02f,1.24f,1.5f);
            }
            SetupReady(level);SetupLayout(level);Brighten(definitions);
            foreach(var pickup in Items<ClothingPickup>()) { var modifier=pickup.GetComponent<NavMeshModifier>();if(modifier==null)modifier=pickup.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true; }
            Physics.SyncTransforms();var surface=Items<NavMeshSurface>().Single();surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.collectObjects=CollectObjects.All;
            surface.BuildNavMesh();var dataMesh=surface.navMeshData;var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Settings/StoreExpandedNavMesh.asset");
            if(saved!=null&&saved!=dataMesh){surface.RemoveData();EditorUtility.CopySerialized(dataMesh,saved);UnityEngine.Object.DestroyImmediate(dataMesh);surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(saved);}
            else if(saved==null)AssetDatabase.CreateAsset(dataMesh,"Assets/Settings/StoreExpandedNavMesh.asset");
            BuildMap(level);
            foreach(var brain in Items<MannequinBrain>())
            {
                var agent=brain.GetComponent<NavMeshAgent>();agent.enabled=false;
                if(!NavMesh.SamplePosition(brain.transform.position,out var hit,2,NavMesh.AllAreas))throw new InvalidOperationException("Stalker is outside navigation: "+brain.name);
                brain.transform.position=hit.position+Vector3.up*.02f;
            }
            var create=typeof(NetworkObject).GetMethod("CreateSceneId",BindingFlags.Static|BindingFlags.NonPublic);
            var serialize=typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(NetworkObject item in (List<NetworkObject>)create.Invoke(null,new object[]{scene,true,0}))serialize.Invoke(item,new object[]{true,false});
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            double total=Items<BoxCollider>().Where(b=>b.name=="Floor"&&b.bounds.center.y<.3f).Sum(b=>(double)b.bounds.size.x*b.bounds.size.z);
            return "Floor area "+oldArea+" -> "+total+"; 3 carts; "+Items<MannequinBrain>().Length+" stalkers; "+Items<ClothingPickup>().Length+" clothing anchors; navigation saved.";
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
    }
    private static void SetupReady(Transform level)
    {
        var station=Items<RoundFinishStation>().Single();station.transform.position=new Vector3(-6.03f,1.35f,-4.5f);station.transform.rotation=Quaternion.Euler(0,270,0);
        var stand=station.transform.Find("Stand");if(stand!=null)UnityEngine.Object.DestroyImmediate(stand.gameObject);
        var plate=station.transform.Find("Plate");plate.localScale=new Vector3(.65f,.75f,.16f);plate.localPosition=Vector3.zero;
        var button=station.transform.Find("Ready Button");button.localScale=new Vector3(.45f,.45f,.12f);button.localPosition=new Vector3(0,0,-.13f);
        var col=station.GetComponent<BoxCollider>();col.center=new Vector3(0,0,-.13f);col.size=new Vector3(.65f,.75f,.25f);
        var data=new SerializedObject(station);data.FindProperty("zoneCenterOffset").vector3Value=new Vector3(6.03f,-1.35f,1.5f);data.FindProperty("radius").floatValue=4.8f;data.ApplyModifiedPropertiesWithoutUndo();
        var lamp=station.GetComponentInChildren<Light>();if(lamp!=null)lamp.transform.localPosition=new Vector3(0,.6f,-.8f);
    }
    private static void SetupLayout(Transform level)
    {
        var layout=level.GetComponent<RoundClothingLayout>();var pickups=Items<ClothingPickup>().Where(p=>p.Clothing!=null).OrderBy(p=>p.name,StringComparer.Ordinal).ThenBy(p=>p.transform.position.x).ThenBy(p=>p.transform.position.z).ToArray();
        var data=new SerializedObject(layout);var items=data.FindProperty("pickups");var positions=data.FindProperty("positions");var rotations=data.FindProperty("rotations");
        items.arraySize=positions.arraySize=rotations.arraySize=pickups.Length;
        for(int i=0;i<pickups.Length;i++){items.GetArrayElementAtIndex(i).objectReferenceValue=pickups[i];positions.GetArrayElementAtIndex(i).vector3Value=pickups[i].transform.position;rotations.GetArrayElementAtIndex(i).quaternionValue=pickups[i].transform.rotation;}
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Brighten(ClothingDefinition[] definitions)
    {
        var mats=new HashSet<Material>();
        foreach(var c in definitions)
        {
            if(c.FabricMaterial!=null)mats.Add(c.FabricMaterial);
            foreach(var model in new[]{c.Model,c.WornModel,c.PickupPrefab})if(model!=null)
                foreach(var r in model.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(m!=null&&!(r is ParticleSystemRenderer))mats.Add(m);
        }
        var shader=Shader.Find("Svinki/Vivid Cloth");if(shader==null)throw new InvalidOperationException("Vivid fabric shader not imported");
        foreach(var mat in mats)
        {
            if(mat.shader.name=="Svinki/Vivid Cloth" || mat.name.Contains("Contour"))continue;
            Color color=mat.HasProperty("_BaseColor")?mat.GetColor("_BaseColor"):mat.color;
            var texture=mat.HasProperty("_BaseMap")?mat.GetTexture("_BaseMap"):mat.mainTexture;
            Vector2 scale=mat.mainTextureScale,offset=mat.mainTextureOffset;mat.shader=shader;
            mat.SetColor("_BaseColor",color);mat.SetTexture("_BaseMap",texture);mat.SetTextureScale("_BaseMap",scale);mat.SetTextureOffset("_BaseMap",offset);
            mat.SetFloat("_VividGain",1.5f);mat.SetFloat("_Saturation",1.3f);mat.SetFloat("_Contrast",1.1f);mat.SetFloat("_Cull",0);EditorUtility.SetDirty(mat);
        }
    }
    private static void BuildMap(Transform level)
    {
        var map=level.GetComponent<StoreMap>();if(map==null)map=level.gameObject.AddComponent<StoreMap>();
        var floors=Items<BoxCollider>().Where(b=>b.name=="Floor"&&b.bounds.center.y<.3f).ToArray();
        var walls=Items<BoxCollider>().Where(b=>b.bounds.min.y<.5f&&b.bounds.max.y>2.5f&&Mathf.Min(b.bounds.size.x,b.bounds.size.z)<.6f&&!b.isTrigger).ToArray();
        var data=new SerializedObject(map);
        void Set(string name,BoxCollider[] boxes)
        {
            var array=data.FindProperty(name);array.arraySize=boxes.Length;
            for(int i=0;i<boxes.Length;i++) {var item=array.GetArrayElementAtIndex(i);var b=boxes[i].bounds;item.FindPropertyRelative("center").vector2Value=new Vector2(b.center.x,b.center.z);item.FindPropertyRelative("size").vector2Value=new Vector2(b.size.x,b.size.z);}
        }
        Set("floors",floors);Set("walls",walls);data.FindProperty("minimum").vector2Value=new Vector2(floors.Min(b=>b.bounds.min.x),floors.Min(b=>b.bounds.min.z));data.FindProperty("maximum").vector2Value=new Vector2(floors.Max(b=>b.bounds.max.x),floors.Max(b=>b.bounds.max.z));data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void DoorHorizontal(Transform parent,float a,float b,float z,float mid,float gap)
    {
        float left=mid-gap/2,right=mid+gap/2;
        Box("Wall left",parent,new Vector3((a+left)/2,2.4f,z),new Vector3(left-a,4.8f,.28f),plaster);
        Box("Wall right",parent,new Vector3((right+b)/2,2.4f,z),new Vector3(b-right,4.8f,.28f),plaster);
        Box("Door lintel",parent,new Vector3(mid,4,z),new Vector3(gap,1.6f,.28f),plaster);
    }
    private static void DoorVertical(Transform parent,float x,float a,float b,float mid,float gap)
    {
        float left=mid-gap/2,right=mid+gap/2;
        Box("Wall south",parent,new Vector3(x,2.4f,(a+left)/2),new Vector3(.28f,4.8f,left-a),plaster);
        Box("Wall north",parent,new Vector3(x,2.4f,(right+b)/2),new Vector3(.28f,4.8f,b-right),plaster);
        Box("Side door lintel",parent,new Vector3(x,4,mid),new Vector3(.28f,1.6f,gap),plaster);
    }
    private static void Box(string name,Transform parent,Vector3 position,Vector3 size,Material mat)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.position=position;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=mat??plaster;
        GameObjectUtility.SetStaticEditorFlags(obj,StaticEditorFlags.BatchingStatic);
    }
}
