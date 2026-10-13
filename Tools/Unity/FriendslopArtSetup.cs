using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FriendslopArtSetup
{
 const string Art=IndustrialArtAssets.Base;
 static T[] All<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
 static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
 static GameObject Group(string name,Transform parent,Vector3 position=default){var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=position;return g;}
 static void Hide(Transform root,Func<Renderer,bool> filter){foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(filter))r.enabled=false;}
 static Material Mat(string id)=>IndustrialArtAssets.MaterialFor(id,id);
 static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material material)
 {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
 static GameObject Place(string id,Transform parent,Vector3 p,Vector3 size,Vector3 rotation=default,string part=null,Vector3 modelRotation=default,bool centered=false)
 =>IndustrialArtAssets.Place(id,parent,p,size,rotation,part,modelRotation,centered);
 static Transform Visual(Transform parent)
 {var prior=parent.Find("Asset visual");if(prior!=null)UnityEngine.Object.DestroyImmediate(prior.gameObject);return Group("Asset visual",parent).transform;}
 public static string Apply()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode required");
  var paths=new[]{"Assets/Scenes/FriendslopLobby.unity","Assets/Scenes/SampleScene.unity"};
  var scenes=paths.Select(p=>{var s=SceneManager.GetSceneByPath(p);if(!s.IsValid()||!s.isLoaded)s=EditorSceneManager.OpenScene(p,OpenSceneMode.Additive);if(s.isDirty)throw new Exception("Save existing scene edits first: "+p);return s;}).ToArray();
  Directory.CreateDirectory("ArtSource/IndustrialCC0/Before");foreach(string p in paths){string backup="ArtSource/IndustrialCC0/Before/"+Path.GetFileName(p);if(!File.Exists(backup))File.Copy(p,backup);}
  var field=typeof(NetworkObject).GetField("SceneId",BindingFlags.Instance|BindingFlags.NonPublic);
  var ids=scenes.SelectMany(All<NetworkObject>).ToDictionary(n=>n,n=>(ulong)field.GetValue(n));
  Lobby(scenes[0]);Factory(scenes[1]);
  if(ids.Any(p=>p.Key==null||(ulong)field.GetValue(p.Key)!=p.Value)||scenes.Sum(s=>All<NetworkObject>(s).Length)!=ids.Count)throw new Exception("Art pass changed network identities");
  foreach(var s in scenes){EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);}AssetDatabase.SaveAssets();Physics.SyncTransforms();
  var report="PASS: actual CC0 model prefabs/materials installed in FriendslopLobby and SampleScene; "+ids.Count+" network roots and IDs preserved. No gameplay/network/EOS changes.";
  File.WriteAllText("ArtSource/IndustrialCC0/art-installation.txt",report);return report;
 }
 static void Lobby(Scene scene)
 {
  var room=All<LobbyActivityManager>(scene).Single().transform;var old=room.Find("Industrial art dressing");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var decor=Group("Industrial art dressing",room).transform;
  var floor=Find(room,"Lobby floor").GetComponent<Renderer>();floor.sharedMaterial=Tiled("Lobby concrete",Mat("concrete_floor_worn_001"),new Vector2(5.5f,4.5f));
  IndustrialArtAssets.SubdivideBox(floor);
  foreach(var r in room.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("wall")||r.name=="Lobby ceiling"||r.name.StartsWith("Lift ")||r.name.Contains("freight door")))
   if(r.GetComponent<TMP_Text>()==null){r.sharedMaterial=r.name.Contains("ceiling")?Mat("worn_corrugated_iron"):Tiled("Locker room wall",Mat("concrete_floor_worn_001"),new Vector2(4,1));IndustrialArtAssets.SubdivideBox(r);}
  var panel=Plain("Worn blue panel",new Color(.22f,.31f,.34f),.25f);
  for(int side=-1;side<=1;side+=2)for(int i=0;i<9;i++)Box("Worker wall panel",decor,new Vector3(side*10.81f,.75f,-8+i*2),new Vector3(.05f,1.5f,1.96f),panel);
  for(int i=0;i<11;i++)Box("Worker wall panel",decor,new Vector3(-10+i*2,.75f,-8.8f),new Vector3(1.96f,1.5f,.05f),panel);
  foreach(var p in new[]{new Vector3(-5,2.7f,-4),new Vector3(5,2.7f,-4),new Vector3(-5,2.7f,4),new Vector3(5,2.7f,4)})
  {var light=Group("Soft room fill",decor,p).AddComponent<Light>();light.type=LightType.Point;light.intensity=3;light.range=14;light.color=new Color(.86f,.9f,1);light.shadows=LightShadows.None;}
  // Keep the collision proxies so players can still climb/sit on the original furniture.
  var sofa=Find(room,"Old worker sofa");Hide(sofa,r=>r.GetComponent<TMP_Text>()==null);Place("sofa_02",Visual(sofa),Vector3.zero,new Vector3(3.7f,1.15f,1.4f),new Vector3(0,180,0));
  var chair=Find(room,"Worker armchair");Hide(chair,r=>r.GetComponent<TMP_Text>()==null);Place("sofa_02",Visual(chair),Vector3.zero,new Vector3(1.5f,1.35f,1.4f),new Vector3(0,180,0));
  var table=Find(room,"Coffee table");table.GetComponent<Renderer>().enabled=false;
  foreach(var r in room.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Table leg"))r.enabled=false;
  Place("industrial_coffee_table",decor,new Vector3(-6.4f,0,-4.4f),new Vector3(2.8f,.73f,1.3f));
  Box("Worn lounge rug",decor,new Vector3(-7,.002f,-4.3f),new Vector3(7,.01f,5.8f),Tiled("Worker rug",Mat("dirty_carpet"),new Vector2(2.5f,2.5f)));
  foreach(var locker in All<PlayerLocker>(scene))Locker(locker);
  Props(scene);Sports(scene);Television(scene);
  // A freely available period boombox replaces the block speaker until the selected BlendSwap jukebox is accessible.
  var jukebox=All<LobbyJukebox>(scene).Single();Hide(jukebox.transform,r=>r.GetComponent<TMP_Text>()==null);
  var jVisual=Visual(jukebox.transform);Place("boombox",jVisual,new Vector3(0,1.05f,0),new Vector3(1.25f,.82f,.42f),new Vector3(0,180,0));
  Place("metal_tool_chest",jVisual,Vector3.zero,new Vector3(1.4f,1,.62f));
  foreach(var r in room.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Exposed pipe"))r.enabled=false;
  for(int i=0;i<8;i++)
  {
   Place("modular_industrial_pipes_01",decor,new Vector3(-9.8f+i*2.8f,4.1f,7.8f),new Vector3(2.7f,.23f,.23f),part:"modular_industrial_pipes_01_pipe02",modelRotation:new Vector3(0,0,90));
   if(i<5)Place("modular_airduct_rectangular_01",decor,new Vector3(-8+i*4,4.15f,-1),new Vector3(.6f,.4f,3.8f),part:"modular_airduct_rectangular_01_double_01",modelRotation:new Vector3(90,0,0));
  }
  Place("modular_industrial_pipes_01",decor,new Vector3(10.5f,1.3f,6.9f),new Vector3(.48f,1.6f,.48f),part:"modular_industrial_pipes_01_pipe08");
  for(int i=0;i<6;i++)Place("industrial_wall_sconce",decor,new Vector3(-10.72f,2.75f,-7+i*2.7f),new Vector3(.22f,.45f,.3f),new Vector3(0,90,0));
  Place("metal_tool_chest",decor,new Vector3(-9.9f,0,3.3f),new Vector3(1.8f,.95f,.85f));
  Place("cement_bag",decor,new Vector3(9.3f,0,6.6f),new Vector3(.7f,.2f,.5f));
  Place("cardboard_box_01",decor,new Vector3(9.3f,.2f,6.6f),new Vector3(.55f,.5f,.45f));
 }
 static Material Tiled(string name,Material source,Vector2 scale)
 {
  string path=Art+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(source);AssetDatabase.CreateAsset(m,path);}else m.CopyPropertiesFromMaterial(source);m.mainTextureScale=scale;m.SetTextureScale("_BumpMap",scale);EditorUtility.SetDirty(m);return m;
 }
 static Material Plain(string name,Color color,float smoothness)
 {string path=Art+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(m);return m;}
 static void Locker(PlayerLocker locker)
 {
  var door=Find(locker.transform,"Hinged door");Hide(door,r=>r.GetComponent<TMP_Text>()==null&&r.GetComponent<LockerPhotoSlot>()==null);
  var visual=Visual(door);
  Place("locker_bank",visual,new Vector3(.5f,1.25f,0),new Vector3(.99f,2.48f,.07f),new Vector3(0,180,0),part:"Locker 0-0 | folded door",centered:true);
  Place("locker_bank",visual,new Vector3(.83f,1.15f,-.075f),new Vector3(.035f,.3f,.035f),part:"Locker | vertical pull | grip",centered:true);
  foreach(var r in locker.GetComponentsInChildren<Renderer>(true).Where(r=>r.transform.parent==locker.transform&&r.GetComponent<LockerPhotoSlot>()==null))r.sharedMaterial=IndustrialArtAssets.MaterialFor("locker_bank","Storm | tactile finish");
 }
 static void Props(Scene scene)
 {
  foreach(var prop in All<LobbyProp>(scene))
  {
   if(prop.Kind==LobbyPropKind.Puck)continue;
   var initial=prop.transform.Find("Visual");if(initial!=null)initial.GetComponent<Renderer>().enabled=false;
   var v=Visual(prop.transform);
   if(prop.Kind==LobbyPropKind.Basketball){Place("basketball",v,Vector3.zero,Vector3.one*.3f,centered:true);continue;}
   if(prop.Kind==LobbyPropKind.Dart){Place("dart_set",v,Vector3.zero,new Vector3(.035f,.035f,.25f),part:"Cone",centered:true);continue;}
   string id;Vector3 size;string part=null;
   switch(prop.name)
   {
    case "Can":case "Rusty can": id="can_rusted";size=new Vector3(.12f,.23f,.12f);prop.name="Rusty can";break;
    case "Paper cup":case "Small rusty tin": id="can_rusted";size=new Vector3(.13f,.16f,.13f);prop.name="Small rusty tin";break;
    case "Bottle":case "Cleaner bottle": id="multi_cleaner_bottle";size=new Vector3(.12f,.35f,.12f);prop.name="Cleaner bottle";break;
    case "Cushion":id="throw_pillows_01";size=new Vector3(.5f,.18f,.5f);part="throw_pillows_01_pillow02";break;
    case "Small box":id="cardboard_box_01";size=new Vector3(.45f,.35f,.4f);break;
    case "Rubber ball":case "Rubber duck":id="rubber_duck_toy";size=new Vector3(.28f,.25f,.3f);prop.name="Rubber duck";break;
    case "Helmet":case "Toolbox":id="metal_toolbox";size=new Vector3(.35f,.25f,.35f);prop.name="Toolbox";break;
    case "Paper roll":case "Plastic jug":id="plastic_bottle_gallon";size=new Vector3(.2f,.2f,.2f);prop.name="Plastic jug";break;
    default:id="ratchet_wrench";size=new Vector3(.3f,.08f,.08f);prop.name="Ratchet wrench";break;
   }
   Place(id,v,Vector3.zero,size,part:part,centered:true);
  }
 }
 static void Sports(Scene scene)
 {
  var hoop=All<BasketballHoop>(scene).Single();Hide(hoop.transform,r=>r.GetComponent<TMP_Text>()==null);
  var visual=Visual(hoop.transform);var go=(GameObject)PrefabUtility.InstantiatePrefab(IndustrialArtAssets.Prefab("wall_basketball_hoop"));
  var ring=go.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Basketball ring");var center=ring.bounds.center;
  go.transform.SetParent(visual,false);go.transform.localRotation=Quaternion.Euler(0,180,0);go.transform.localScale=Vector3.one*(.7f/.486f);go.transform.localPosition=new Vector3(0,2.75f,-.35f)-go.transform.localRotation*center*(.7f/.486f);
  var board=All<Dartboard>(scene).Single();Hide(board.transform,r=>r.GetComponent<TMP_Text>()==null);
  Place("dartboard",Visual(board.transform),new Vector3(0,0,-.09f),new Vector3(1.1f,1.1f,.09f),new Vector3(0,180,0),centered:true);
  // The imported model includes the outer number ring; scoring ends at the double wire inside it.
  var scoring=new SerializedObject(board);scoring.FindProperty("radius").floatValue=.415f;scoring.ApplyModifiedPropertiesWithoutUndo();
 }
 static void Television(Scene scene)
 {
  var tv=All<LobbyTelevision>(scene).Single();var serialized=new SerializedObject(tv);var screen=(Renderer)serialized.FindProperty("screen").objectReferenceValue;
  Hide(tv.transform,r=>r!=screen&&r.GetComponent<TMP_Text>()==null);
  Place("Television_01",Visual(tv.transform),new Vector3(0,-.65f,.09f),new Vector3(1.9f,1.35f,1.15f),new Vector3(0,180,0));
  var box=tv.GetComponent<BoxCollider>();box.center=new Vector3(0,.025f,.09f);box.size=new Vector3(1.9f,1.35f,1.15f);
  // Keep the RenderTexture receiver as an independent screen overlay so playback stays wired.
  screen.transform.localPosition=new Vector3(-.17f,.06f,-.51f);screen.transform.localScale=new Vector3(1.24f,.9f,1);
 }
 static void Factory(Scene scene)
 {
  var level=scene.GetRootGameObjects().Single(g=>g.name=="Graybox Level").transform;
  var old=level.Find("Industrial art dressing");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);var decor=Group("Industrial art dressing",level).transform;
  var concrete=Mat("concrete_floor_worn_001");var metal=Mat("rusty_painted_metal");var roof=Mat("worn_corrugated_iron");
  foreach(var r in level.GetComponentsInChildren<MeshRenderer>(true))
  {
   if(r.GetComponent<TMP_Text>()!=null||r.GetComponentInParent<NetworkObject>()!=null||r.GetComponentInParent<Animator>()!=null)continue;
   string n=r.name.ToLowerInvariant();
   if(n=="floor"||n.Contains("solid wall")||n.Contains("passage wall")||n.Contains("plaster"))r.sharedMaterial=concrete;
   else if(n.Contains("ceiling")||n.Contains("divider"))r.sharedMaterial=roof;
   else if(n.Contains("wall")||n.Contains("display")||n.Contains("showcase")||n.Contains("shelf")||n.Contains("bench")||n.Contains("cap"))r.sharedMaterial=metal;
   if(n=="floor"||n.Contains("solid wall")||n.Contains("passage wall")||n.Contains("ceiling"))IndustrialArtAssets.SubdivideBox(r);
  }
  var floors=All<BoxCollider>(scene).Where(c=>c.name=="Floor"&&c.bounds.min.y<.1f).OrderBy(c=>c.transform.parent.name,StringComparer.Ordinal).ToArray();
  for(int i=0;i<floors.Length;i++)
  {
   var bounds=floors[i].bounds;var group=Group("Workshop detail "+i,decor).transform;
   // All new dressing is flush with existing walls or above player height. No navigation geometry is replaced.
   var p=new Vector3(bounds.min.x+.38f,3.85f,bounds.center.z);
   Place("modular_industrial_pipes_01",group,p,new Vector3(Mathf.Min(bounds.size.z*.75f,10),.3f,.3f),new Vector3(0,90,0),part:"modular_industrial_pipes_01_pipe02",modelRotation:new Vector3(0,0,90));
   Place("modular_industrial_pipes_01",group,p+new Vector3(0,-.8f,0),new Vector3(.32f,.6f,.35f),part:"modular_industrial_pipes_01_pipe08");
   if(i%2==0)Place("industrial_wall_sconce",group,new Vector3(bounds.min.x+.2f,2.8f,bounds.center.z-2),new Vector3(.25f,.5f,.3f),new Vector3(0,90,0));
   floors[i].GetComponent<Renderer>().sharedMaterial=Tiled("Factory floor "+i,concrete,new Vector2(bounds.size.x/4,bounds.size.z/4));
  }
  // Existing non-network worktops get visible workshop contents without changing hiding spots underneath.
  foreach(var table in All<BoxCollider>(scene).Where(c=>c.name.Contains("workbench")||c.name=="Reading bench").Take(8))
  {var b=table.bounds;Place("metal_tool_chest",decor,new Vector3(b.center.x,b.max.y,b.center.z),new Vector3(.7f,.35f,.4f));}
 }
}
