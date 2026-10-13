using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Authoring only: no gameplay scripts, network objects or colliders are imported from art packs.
public static class IndustrialArtAssets
{
 public const string Base="Assets/Art/IndustrialCC0";
 [Serializable] class MaterialInfo {public string name,texture;public float[] color;public float metal,roughness;}
 [Serializable] class MaterialList {public MaterialInfo[] items;}
 public static Bounds BoundsOf(GameObject go)
 {
  var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new Exception("No mesh: "+go.name);
  var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
 }
 public static void SubdivideBox(Renderer renderer)
 {
  var filter=renderer.GetComponent<MeshFilter>();if(filter==null)return;
  var dimensions=renderer.transform.lossyScale;string directory=Base+"/Meshes";
  if(!Directory.Exists(directory)){Directory.CreateDirectory(directory);AssetDatabase.Refresh();}
  string path=directory+"/Grid_"+Safe(dimensions.ToString("F2"))+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(mesh==null)
  {
   var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
   foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
   {
    var u=Mathf.Abs(normal.x)>.5f?Vector3.forward:Vector3.right;var v=Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up;
    int nx=Mathf.Max(1,Mathf.CeilToInt(Vector3.Scale(u,dimensions).magnitude)),ny=Mathf.Max(1,Mathf.CeilToInt(Vector3.Scale(v,dimensions).magnitude));
    int start=vertices.Count;
    for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++){var p=new Vector2((float)x/nx,(float)y/ny);vertices.Add(normal*.5f+u*(p.x-.5f)+v*(p.y-.5f));normals.Add(normal);uv.Add(p);}
    bool reverse=Vector3.Dot(Vector3.Cross(u,v),normal)<0;
    for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){int a=start+y*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;if(reverse)triangles.AddRange(new[]{a,c,b,b,c,d});else triangles.AddRange(new[]{a,b,c,b,d,c});}
   }
   mesh=new Mesh{name="Subdivided static surface"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
  }
  filter.sharedMesh=mesh;
 }
 static string Safe(string name)=>string.Concat(name.Select(c=>char.IsLetterOrDigit(c)||c=='_'?c:'_'));
 static Texture2D Texture(string folder,string prefix,string suffix)
 {
  var paths=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith(".meta")&&p.Contains(suffix)&&(p.EndsWith(".jpg")||p.EndsWith(".png")||p.EndsWith(".exr"))).ToArray();
  var file=paths.FirstOrDefault(p=>Path.GetFileName(p).StartsWith(prefix+"_",StringComparison.OrdinalIgnoreCase))??paths.FirstOrDefault();
  if(file==null)return null;file=file.Replace('\\','/');
  var importer=(TextureImporter)AssetImporter.GetAtPath(file);
  bool normal=suffix.Contains("nor_gl")||suffix=="normal";bool linear=normal||suffix.Contains("metalSmooth");
  if(importer.textureType!=(normal?TextureImporterType.NormalMap:TextureImporterType.Default)||importer.sRGBTexture==linear||importer.maxTextureSize>1024)
  {importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!linear;importer.maxTextureSize=1024;importer.SaveAndReimport();}
  return AssetDatabase.LoadAssetAtPath<Texture2D>(file);
 }
 public static Material MaterialFor(string id,string original)
 {
  string folder=Base+"/"+id,path=folder+"/URP_"+Safe(original)+".mat";
  var result=AssetDatabase.LoadAssetAtPath<Material>(path);bool existed=result!=null;
  if(!existed)result=new Material(Shader.Find("Universal Render Pipeline/Lit"));result.name=id+" · "+original;result.SetFloat("_Smoothness",.25f);
  var diffuse=Texture(folder,original,"_diff");var normal=Texture(folder,original,"_nor_gl");var packed=Texture(folder,original,"_metalSmooth");
  if(diffuse!=null)result.SetTexture("_BaseMap",diffuse);
  if(normal!=null){result.SetTexture("_BumpMap",normal);result.EnableKeyword("_NORMALMAP");result.SetFloat("_BumpScale",.65f);}
  if(packed!=null){result.SetTexture("_MetallicGlossMap",packed);result.EnableKeyword("_METALLICSPECGLOSSMAP");result.SetFloat("_Smoothness",1);}
  if(id=="dart_set")result.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/dart_texture.png"));
  if(id=="basketball"){result.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/textures/classic_baseColor.png"));result.SetTexture("_BumpMap",Texture(folder,"","normal"));result.EnableKeyword("_NORMALMAP");}
  if(File.Exists(folder+"/materials.json"))
  {
   var info=JsonUtility.FromJson<MaterialList>("{\"items\":"+File.ReadAllText(folder+"/materials.json")+"}").items.FirstOrDefault(m=>m.name==original);
   if(info!=null){result.SetColor("_BaseColor",new Color(info.color[0],info.color[1],info.color[2],1));result.SetFloat("_Metallic",info.metal);result.SetFloat("_Smoothness",1-info.roughness);
    if(info.texture!=null){result.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/"+info.texture));result.SetColor("_BaseColor",Color.white);}}
  }
  if(!existed)AssetDatabase.CreateAsset(result,path);else EditorUtility.SetDirty(result);return result;
 }
 public static GameObject Prefab(string id,string part=null,Vector3 rotation=default)
 {
  if(!Directory.Exists(Base+"/Prefabs")){Directory.CreateDirectory(Base+"/Prefabs");AssetDatabase.Refresh();}
  string path=Base+"/Prefabs/"+id+(part!=null?"_"+Safe(part):"")+".prefab";
  var prior=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prior!=null&&AssetImporter.GetAtPath(path).userData=="IndustrialCC0/v3")
  {if(id=="basketball")MaterialFor(id,"Lit");return prior;}
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(Base+"/"+id+"/"+id+".fbx");if(source==null)throw new Exception("Missing model "+id);
  var container=new GameObject(id);var instance=UnityEngine.Object.Instantiate(source,container.transform);instance.name="Imported geometry";
  try
  {
   if(part!=null)foreach(var r in instance.GetComponentsInChildren<Renderer>())if(r.name!=part)UnityEngine.Object.DestroyImmediate(r.gameObject);
   // FBX roots contain the conversion from their authoring axis to Unity Y-up. Preserve it.
   instance.transform.localRotation=Quaternion.Euler(rotation)*instance.transform.localRotation;
   foreach(var r in instance.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>MaterialFor(id,m!=null?m.name:id)).ToArray();
   var bounds=BoundsOf(instance);instance.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
   foreach(var animator in instance.GetComponentsInChildren<Animator>())UnityEngine.Object.DestroyImmediate(animator);
   var prefab=PrefabUtility.SaveAsPrefabAsset(container,path);var importer=AssetImporter.GetAtPath(path);importer.userData="IndustrialCC0/v3";importer.SaveAndReimport();return prefab;
  }finally{UnityEngine.Object.DestroyImmediate(container);}
 }
 public static GameObject Place(string id,Transform parent,Vector3 position,Vector3 size,Vector3 rotation=default,string part=null,Vector3 modelRotation=default,bool centered=false)
 {
  var prefab=Prefab(id,part,modelRotation);var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var b=BoundsOf(go);
  go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(rotation);
  go.transform.localScale=new Vector3(size.x/Mathf.Max(.001f,b.size.x),size.y/Mathf.Max(.001f,b.size.y),size.z/Mathf.Max(.001f,b.size.z));
  if(centered)go.transform.localPosition-=go.transform.localRotation*Vector3.up*(size.y*.5f);
  return go;
 }
 public static string Inspect()
 {
  var values=new List<string>();
  foreach(string path in Directory.GetFiles(Base,"*.fbx",SearchOption.AllDirectories))
  {var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)continue;var go=UnityEngine.Object.Instantiate(asset);var b=BoundsOf(go);values.Add(Path.GetFileNameWithoutExtension(path)+" "+b.size+" "+string.Join(",",go.GetComponentsInChildren<Renderer>().Take(8).Select(r=>r.name+" "+r.bounds.center)));UnityEngine.Object.DestroyImmediate(go);}
  return string.Join("\n",values);
 }
}
