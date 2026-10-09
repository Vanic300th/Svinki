using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class StoreRedesignSetup
{
    private sealed class Room { public string name;public Transform root;public float x0,x1,z0,z1;public Vector3 Center=>new Vector3((x0+x1)/2,0,(z0+z1)/2); }
    private static Scene scene;
    private static Transform level,wing,hideRoot;
    private static Material plaster,wood,metal,cream,glow,teal,peach;
    private static readonly List<Room> rooms=new List<Room>();
    private static readonly List<Vector3> anchors=new List<Vector3>();
    private static readonly List<Quaternion> angles=new List<Quaternion>();
    private static readonly Collider[] overlap=new Collider[128];
    private static T[] All<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    public static string Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode required");
        scene=SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);
        var previous=SceneManager.GetActiveScene();SceneManager.SetActiveScene(scene);
        try
        {
            level=scene.GetRootGameObjects().Single(r=>r.name=="Graybox Level").transform;
            rooms.Clear();anchors.Clear();angles.Clear();
            Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PeachStore/"+n+".mat");
            plaster=Mat("Warm plaster");wood=Mat("Walnut display");metal=Mat("Dark steel");cream=Mat("Cream fixtures");glow=Mat("Warm diffuser");teal=Mat("Petrol accent");peach=Mat("Peach accent");
            var pickups=All<ClothingPickup>().Where(c=>c.Clothing!=null).OrderBy(c=>c.name,StringComparer.Ordinal).ToArray();
            if(pickups.Length!=96)throw new Exception("Expected all 96 clothing objects; found "+pickups.Length);
            var old=level.Find("North departments - triple area");if(old==null)throw new Exception("Expanded wing missing");
            var upper=pickups.Where(p=>p.transform.position.y>2.5f).Select(p=>p.transform.position).Distinct().Take(6).ToArray();
            foreach(var pickup in pickups)pickup.transform.SetParent(level,true);
            var priorHide=level.Find("Concealed clothing displays");if(priorHide!=null)UnityEngine.Object.DestroyImmediate(priorHide.gameObject);
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            wing=new GameObject("North departments - triple area").transform;wing.SetParent(level,false);
            hideRoot=new GameObject("Concealed clothing displays").transform;hideRoot.SetParent(level,false);
            foreach(var floor in All<BoxCollider>().Where(b=>b.name=="Floor"&&b.bounds.center.y<.3f).OrderBy(b=>b.transform.parent.name,StringComparer.Ordinal))
            {var b=floor.bounds;rooms.Add(new Room{name=floor.transform.parent.name,root=floor.transform.parent,x0=b.min.x,x1=b.max.x,z0=b.min.z,z1=b.max.z});}
            var oldWing=level.Find("Expanded north wing - v1");
            foreach(var b in oldWing.GetComponentsInChildren<BoxCollider>().Where(b=>Mathf.Abs(b.bounds.center.z-65)<.08f&&b.bounds.size.z<.6f&&b.bounds.max.y>2.5f).ToArray())UnityEngine.Object.DestroyImmediate(b.gameObject);
            float[][] edges={new[]{-40f,-18,0,18,40},new[]{-52f,-25,-3,20,48},new[]{-42f,-18,6,34,58},new[]{-28f,-10,14,34,52}};
            float[] depths={26,23,23,25.5625f};float z=65;
            string[] names={"COLOUR LAB","DENIM ARCADE","STREET MARKET","THE TAILOR","NEON LOUNGE","SPORTS CLUB","SUMMER COURT","WINTER CABIN","PRINT GALLERY","SHOE VAULT","ACCESSORY BAR","COLLECTIONS","COUTURE SALON","CARGO DEPOT","LIFESTYLE","THE OUTLET"};
            var bands=new List<Room[]>();
            for(int row=0;row<4;row++)
            {
                var band=new Room[4];
                for(int col=0;col<4;col++)
                {
                    int index=row*4+col;var root=new GameObject((index+15)+" / "+names[index]).transform;root.SetParent(wing,false);
                    var r=new Room{name=root.name,root=root,x0=edges[row][col],x1=edges[row][col+1],z0=z,z1=z+depths[row]};rooms.Add(r);band[col]=r;
                    var floor=Mat("Floor "+((index%8)+3).ToString("00"));
                    Box("Floor",root,r.Center+Vector3.down*.15f,new Vector3(r.x1-r.x0,.3f,r.z1-r.z0),floor);
                    Box("Ceiling",root,r.Center+Vector3.up*5.02f,new Vector3(r.x1-r.x0,.24f,r.z1-r.z0),plaster);
                    Box("West wall",root,new Vector3(r.x0,2.45f,(r.z0+r.z1)/2),new Vector3(.28f,4.9f,r.z1-r.z0),col==0?plaster:teal).SetActive(col==0);
                    if(col==3)Box("East wall",root,new Vector3(r.x1,2.45f,(r.z0+r.z1)/2),new Vector3(.28f,4.9f,r.z1-r.z0),plaster);
                    if(col<3)Boundary(root,false,r.x1,r.z0,r.z1,new[]{(r.z0+r.z1)/2+((index%2==0)?-3:3)},4.8f);
                    Furnish(r,index);
                }
                bands.Add(band);z+=depths[row];
            }
            Boundary(wing,true,65,-40,40,new[]{-22f,-6,10,26.5f},4.8f);
            for(int row=1;row<4;row++)
            {
                var doors=new List<float>();foreach(var a in bands[row-1])foreach(var b in bands[row]){float lo=Mathf.Max(a.x0,b.x0),hi=Mathf.Min(a.x1,b.x1);if(hi-lo>6.5f)doors.Add((lo+hi)/2);}
                Boundary(wing,true,bands[row][0].z0,Mathf.Min(bands[row-1][0].x0,bands[row][0].x0),Mathf.Max(bands[row-1][3].x1,bands[row][3].x1),doors,4.8f);
            }
            Boundary(wing,true,z,-28,52,Array.Empty<float>(),4.8f);
            Physics.SyncTransforms();Bake();
            for(int i=0;i<rooms.Count;i++)CreateHides(rooms[i],i,i==0?1:3);
            // Preserve the raised bridge as a worthwhile alternate search route.
            foreach(var point in upper){anchors.Add(point);angles.Add(Quaternion.Euler(0,90,0));}
            for(int i=0;i<120&&anchors.Count<96;i++)CreateHides(rooms[14+i%16],i+rooms.Count,1);
            if(anchors.Count!=96)throw new Exception("Hiding place count mismatch");
            for(int i=0;i<pickups.Length;i++)pickups[i].transform.SetPositionAndRotation(anchors[i],angles[i]);
            var layout=level.GetComponent<RoundClothingLayout>();var data=new SerializedObject(layout);var itemList=data.FindProperty("pickups");var positions=data.FindProperty("positions");var rotations=data.FindProperty("rotations");
            itemList.arraySize=positions.arraySize=rotations.arraySize=96;
            for(int i=0;i<96;i++){itemList.GetArrayElementAtIndex(i).objectReferenceValue=pickups[i];positions.GetArrayElementAtIndex(i).vector3Value=anchors[i];rotations.GetArrayElementAtIndex(i).quaternionValue=angles[i];}data.ApplyModifiedPropertiesWithoutUndo();
            var carts=All<ShoppingCart>().OrderBy(c=>c.name,StringComparer.Ordinal).ToArray();carts[2].transform.position=bands[2][2].Center+new Vector3(0,.03f,2);
            var added=All<MannequinBrain>().Where(b=>b.name.StartsWith("Aggressive mannequin ")).OrderBy(b=>b.name,StringComparer.Ordinal).ToArray();
            for(int i=0;i<added.Length;i++)added[i].transform.position=rooms[14+Mathf.RoundToInt(i*15f/(added.Length-1))].Center+new Vector3(0,.02f,-2);
            foreach(var r in All<RectTransform>())if(r.name=="Portrait Background"||r.name=="Character Portrait")r.anchoredPosition=new Vector2(r.anchoredPosition.x,-90);
            Physics.SyncTransforms();Bake();BuildMap();
            var invalid=new List<string>();
            foreach(var r in rooms)
            {
                if(!NavMesh.SamplePosition(r.Center,out var hit,3,NavMesh.AllAreas)){invalid.Add(r.name+" no floor");continue;}
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(new Vector3(0,0,-3),hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)invalid.Add(r.name+" disconnected");
            }
            foreach(var agent in All<NavMeshAgent>())
            {agent.enabled=false;if(NavMesh.SamplePosition(agent.transform.position,out var hit,3,NavMesh.AllAreas))agent.transform.position=hit.position+Vector3.up*.02f;else invalid.Add(agent.name+" outside NavMesh");}
            if(invalid.Count>0)throw new Exception(string.Join("; ",invalid));
            var create=typeof(NetworkObject).GetMethod("CreateSceneId",BindingFlags.Static|BindingFlags.NonPublic);var serialize=typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var obj in (List<NetworkObject>)create.Invoke(null,new object[]{scene,true,0}))serialize.Invoke(obj,new object[]{true,false});
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("ArtSource/StoreRedesign");
            string Number(float value)=>value.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            string Quote(string value)=>"\""+value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
            var roomData=rooms.Select(r=>"{\"name\":"+Quote(r.name)+",\"x0\":"+Number(r.x0)+",\"x1\":"+Number(r.x1)+",\"z0\":"+Number(r.z0)+",\"z1\":"+Number(r.z1)+"}");
            var slotData=anchors.Select((p,i)=>"{\"x\":"+Number(p.x)+",\"y\":"+Number(p.y)+",\"z\":"+Number(p.z)+",\"yaw\":"+Number(angles[i].eulerAngles.y)+"}");
            File.WriteAllText("ArtSource/StoreRedesign/layout.json","{\"rooms\":["+string.Join(",",roomData)+"],\"anchors\":["+string.Join(",",slotData)+"]}");
            string result="30 connected departments; stepped 110m-wide outline; 96 hidden clothing anchors; 3 carts; 12 attackers; angled partitions and varied interiors; navigation and network IDs saved.";
            File.WriteAllText("ArtSource/StoreRedesign/scene-validation.txt",result);return result;
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
    }
    private static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,float yaw=0,bool collision=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,yaw,0));go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collision){var c=go.GetComponent<Collider>();c.enabled=false;UnityEngine.Object.DestroyImmediate(c);}GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);return go;
    }
    private static void Boundary(Transform parent,bool horizontal,float fixedAt,float start,float end,IEnumerable<float> centers,float gap)
    {
        float cursor=start;foreach(float mid in centers.OrderBy(n=>n))
        {float a=Mathf.Max(start,mid-gap/2),b=Mathf.Min(end,mid+gap/2);if(a>cursor+.04f)Wall(cursor,a);cursor=Mathf.Max(cursor,b);Box("Passage lintel",parent,horizontal?new Vector3(mid,4.5f,fixedAt):new Vector3(fixedAt,4.5f,mid),horizontal?new Vector3(gap,.9f,.28f):new Vector3(.28f,.9f,gap),teal);}
        if(cursor<end-.04f)Wall(cursor,end);
        void Wall(float a,float b)=>Box("Offset passage wall",parent,horizontal?new Vector3((a+b)/2,2.45f,fixedAt):new Vector3(fixedAt,2.45f,(a+b)/2),horizontal?new Vector3(b-a,4.9f,.28f):new Vector3(.28f,4.9f,b-a),plaster);
    }
    private static void Furnish(Room r,int index)
    {
        float angle=index%2==0?28:-24;var c=r.Center;
        Box("Angled showroom divider",r.root,c+new Vector3(-3,1.25f,2),new Vector3(4.8f,2.5f,.22f),index%3==0?peach:teal,angle);
        Box("Divider cap",r.root,c+new Vector3(-3,2.55f,2),new Vector3(5,.13f,.3f),cream,angle,false);
        if(index%3==0)
        {
            for(int i=0;i<5;i++){float a=(i*24-48)*Mathf.Deg2Rad;Box("Curved display segment",r.root,c+new Vector3(4+Mathf.Sin(a)*3,.45f,-3+Mathf.Cos(a)*3),new Vector3(1.35f,.9f,.9f),wood,i*24-48);}
        }
        else if(index%3==1)
        {Box("Reading bench",r.root,c+new Vector3(4,.35f,-3),new Vector3(4,.7f,1.3f),wood,18);Box("Bench cushion",r.root,c+new Vector3(4,.76f,-3),new Vector3(3.8f,.18f,1.2f),teal,18,false);}
        else
        {for(int i=0;i<2;i++)Box("Offset low showcase",r.root,c+new Vector3(3+i*3,.45f,-4+i*2),new Vector3(2,.9f,1.5f),cream,-20+i*35);}
        var sign=new GameObject("Department sign",typeof(TMPro.TextMeshPro));sign.transform.SetParent(r.root,false);sign.transform.SetPositionAndRotation(new Vector3(c.x,3.35f,r.z0+.5f),Quaternion.identity);var text=sign.GetComponent<TMPro.TextMeshPro>();text.font=TMPro.TMP_Settings.defaultFontAsset;text.text=r.name.Split('/').Last().Trim();text.fontSize=8;text.rectTransform.sizeDelta=new Vector2(16,1.5f);text.alignment=TMPro.TextAlignmentOptions.Center;text.color=new Color(1,.8f,.6f);
        for(int i=0;i<2;i++)
        {
            var position=c+new Vector3((i==0?-1:1)*(r.x1-r.x0)*.18f,4.35f,(i==0?-1:1)*(r.z1-r.z0)*.23f);
            Box("Ceiling light diffuser",r.root,position+Vector3.up*.25f,new Vector3(3,.08f,.65f),glow,0,false);
            var go=new GameObject("Department light",typeof(Light));go.transform.SetParent(r.root,false);go.transform.SetPositionAndRotation(position,Quaternion.Euler(90,0,0));var l=go.GetComponent<Light>();l.type=LightType.Spot;l.range=22;l.spotAngle=125;l.innerSpotAngle=95;l.intensity=100;l.color=index%4==0?new Color(.74f,.9f,1):new Color(1,.88f,.72f);l.shadows=LightShadows.None;
        }
        Box("Wayfinding floor stripe",r.root,c+new Vector3(0,.008f,-(r.z1-r.z0)*.3f),new Vector3(5,.012f,.18f),index%2==0?peach:teal,0,false);
    }
    private static void CreateHides(Room r,int seed,int count)
    {
        var candidates=new List<Vector3>();
        float margin=Mathf.Min(2.4f,(r.x1-r.x0)*.28f);
        foreach(float z in new[]{r.z0+2.5f,r.z1-2.5f,r.z0+(r.z1-r.z0)*.30f,r.z0+(r.z1-r.z0)*.70f})
        foreach(float x in new[]{r.x0+margin,r.x1-margin})candidates.Add(new Vector3(x,0,z));
        foreach(float x in new[]{r.x0+(r.x1-r.x0)*.28f,r.x0+(r.x1-r.x0)*.72f})foreach(float z in new[]{r.z0+2.5f,r.z1-2.5f})candidates.Add(new Vector3(x,0,z));
        candidates=candidates.OrderBy(p=>(p-r.Center).sqrMagnitude+(seed%3==0?p.x:-p.x)*.02f).Reverse().ToList();
        int placed=0;
        foreach(var point in candidates)
        {
            if(placed==count)break;
            if(anchors.Any(a=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(point.x,point.z))<3.5f))continue;
            var direction=(r.Center-point).normalized;var rotation=Quaternion.LookRotation(direction);
            int n=scene.GetPhysicsScene().OverlapBox(point+Vector3.up*.95f,new Vector3(1.25f,.85f,1.35f),overlap,rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(Enumerable.Range(0,n).Any(i=>overlap[i]!=null&&!overlap[i].GetComponentInParent<ClothingPickup>()&&!(overlap[i].GetComponentInParent<NavMeshAgent>())))continue;
            if(!NavMesh.SamplePosition(point+direction*1.8f,out var walk,1,NavMesh.AllAreas))continue;
            var path=new NavMeshPath();if(!NavMesh.CalculatePath(new Vector3(0,0,-3),walk.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            var root=new GameObject("Hidden spot "+anchors.Count.ToString("00")+" / "+r.name).transform;root.SetParent(hideRoot,false);root.SetPositionAndRotation(point,rotation);
            Vector3 World(Vector3 p)=>point+rotation*p;
            int kind=(seed+placed)%3;float baseHeight=kind==2?.55f:.12f;
            if(kind==1)
            {
                Box("Sightline screen",root,World(new Vector3(0,.95f,1.25f)),new Vector3(2.6f,1.9f,.16f),wood,rotation.eulerAngles.y+22);
                Box("Stock plinth",root,World(new Vector3(0,.06f,0)),new Vector3(1.7f,.12f,1.4f),wood,rotation.eulerAngles.y);
            }
            else
            {
                float yaw=rotation.eulerAngles.y;
                Box("Cabinet back",root,World(new Vector3(0,1.25f,-.95f)),new Vector3(2.4f,2.5f,.15f),kind==2?metal:wood,yaw);
                foreach(float side in new[]{-1.15f,1.15f})Box("Cabinet side",root,World(new Vector3(side,1.25f,-.2f)),new Vector3(.15f,2.5f,1.65f),cream,yaw);
                Box("Cabinet canopy",root,World(new Vector3(0,2.55f,-.2f)),new Vector3(2.45f,.15f,1.7f),wood,yaw);
                Box("Clothing shelf",root,World(new Vector3(0,baseHeight-.06f,-.2f)),new Vector3(2.2f,.12f,1.5f),wood,yaw);
            }
            anchors.Add(World(new Vector3(0,baseHeight+.02f,-.15f)));angles.Add(rotation*Quaternion.Euler(0,180,0));placed++;Physics.SyncTransforms();
        }
        // Small existing rooms may fit fewer cabinets; the spacious new departments fill the remaining quota.
    }
    private static void Bake()
    {
        var surface=All<NavMeshSurface>().Single();surface.collectObjects=CollectObjects.All;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
        var fresh=surface.navMeshData;var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Settings/StoreExpandedNavMesh.asset");
        if(saved!=null&&saved!=fresh){surface.RemoveData();EditorUtility.CopySerialized(fresh,saved);UnityEngine.Object.DestroyImmediate(fresh);surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(saved);}else if(saved==null)AssetDatabase.CreateAsset(fresh,"Assets/Settings/StoreExpandedNavMesh.asset");
    }
    private static void BuildMap()
    {
        var map=level.GetComponent<StoreMap>();var data=new SerializedObject(map);
        var floors=All<BoxCollider>().Where(b=>b.name=="Floor"&&b.bounds.center.y<.3f).ToArray();
        var walls=All<BoxCollider>().Where(b=>b.bounds.min.y<.5f&&b.bounds.max.y>2.2f&&Mathf.Min(Mathf.Abs(b.transform.lossyScale.x*b.size.x),Mathf.Abs(b.transform.lossyScale.z*b.size.z))<.6f&&!b.isTrigger).ToArray();
        void Set(string field,BoxCollider[] boxes){var list=data.FindProperty(field);list.arraySize=boxes.Length;for(int i=0;i<boxes.Length;i++){var b=boxes[i];var value=list.GetArrayElementAtIndex(i);var center=b.transform.TransformPoint(b.center);value.FindPropertyRelative("center").vector2Value=new Vector2(center.x,center.z);value.FindPropertyRelative("size").vector2Value=new Vector2(Mathf.Abs(b.size.x*b.transform.lossyScale.x),Mathf.Abs(b.size.z*b.transform.lossyScale.z));value.FindPropertyRelative("angle").floatValue=-b.transform.eulerAngles.y;}}
        Set("floors",floors);Set("walls",walls);data.FindProperty("minimum").vector2Value=new Vector2(floors.Min(b=>b.bounds.min.x),floors.Min(b=>b.bounds.min.z));data.FindProperty("maximum").vector2Value=new Vector2(floors.Max(b=>b.bounds.max.x),floors.Max(b=>b.bounds.max.z));data.ApplyModifiedPropertiesWithoutUndo();
    }
}
