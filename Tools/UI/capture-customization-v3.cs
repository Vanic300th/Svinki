var menu=UnityEngine.Object.FindFirstObjectByType<PigAppearanceMenu>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var camera=(UnityEngine.Camera)typeof(PigAppearanceMenu).GetField("previewCamera",flags).GetValue(menu);
var pig=(PigAppearance)typeof(PigAppearanceMenu).GetField("preview",flags).GetValue(menu);
var origin=camera.transform.parent;
var previous=UnityEngine.RenderTexture.active;var position=camera.transform.localPosition;var rotation=camera.transform.localRotation;float fov=camera.fieldOfView;
var image=new UnityEngine.Texture2D(camera.targetTexture.width,camera.targetTexture.height,UnityEngine.TextureFormat.RGBA32,false);
var manifest=new System.Collections.Generic.List<string>{"category\tfile\tlabel\tappearance"};
void Shot(string category,string file,string label,PigFace face,float yaw=0,float focus=1.48f,float distance=1.72f,float field=38)
{
 pig.Apply(face.Encode());camera.fieldOfView=field;
 camera.transform.localPosition=UnityEngine.Quaternion.Euler(0,yaw,0)*new UnityEngine.Vector3(.10f,focus+.10f,distance);
 camera.transform.LookAt(origin.position+new UnityEngine.Vector3(0,focus,0));camera.Render();
 UnityEngine.RenderTexture.active=camera.targetTexture;
 image.ReadPixels(new UnityEngine.Rect(0,0,image.width,image.height),0,0);image.Apply();
 System.IO.File.WriteAllBytes("ArtSource/Pigs/v3/Previews/"+file+".png",image.EncodeToPNG());
 manifest.Add(category+"\t"+file+".png\t"+label+"\t"+face.Encode());
}
try
{
 for(int i=1;i<PigFace.HairCount;i++)Shot("hair","hair-"+i,PigAppearance.HairNames[i],new PigFace{hairStyle=i,hairColor=i==10?8:1,skinColor=1},i==3?125:0);
 for(int i=7;i<PigFace.GlassesCount;i++)Shot("glasses","glasses-"+i,PigAppearance.GlassesNames[i],new PigFace{glassesStyle=i,skinColor=0});
 for(int i=0;i<PigFace.EyesCount;i++)Shot("face","eyes-"+i,"Eyes: "+PigAppearance.EyesNames[i],new PigFace{eyes=i,skinColor=1});
 for(int i=0;i<PigFace.BrowsCount;i++)Shot("face","brows-"+i,"Brows: "+PigAppearance.BrowsNames[i],new PigFace{brows=i,skinColor=1});
 for(int i=1;i<PigFace.MustacheCount;i++)Shot("face","moustache-"+i,"Moustache: "+PigAppearance.MustacheNames[i],new PigFace{mustache=i,skinColor=1});
 Shot("face","goatee","Goatee",new PigFace{beard=true,skinColor=1});
 for(int i=0;i<PigFace.PatternCount;i++)Shot("patterns","pattern-"+i,PigAppearance.PatternNames[i],new PigFace{skinColor=1,pattern=i,patternColor=1},20,.99f,3.72f,32);
 for(int i=0;i<PigFace.PlacementCount;i++)Shot("tattoos","tattoo-"+i,PigAppearance.PlacementNames[i],new PigFace{skinColor=1,tattoo=1,tattooPlacement=i},i==1?155:i==3?180:0,i==2?.30f:i==1?1.08f:.90f,i==2?1.25f:1.75f,32);
 for(int i=0;i<PigFace.SkinColorCount;i++)Shot("palette","skin-"+i,PigAppearance.ColorNames[i],new PigFace{skinColor=i},0,.99f,3.72f,32);
 Shot("hero","comedy-pig","Project S.V.I.N.",new PigFace{eyes=2,brows=5,hairStyle=2,hairColor=1,glassesStyle=7,mustache=4,beard=true,skinColor=0},0,1.4f,2.1f,38);
 System.IO.File.WriteAllLines("ArtSource/Pigs/v3/Previews/manifest.tsv",manifest);
 return new {renders=manifest.Count-1,shaderErrors=UnityEditor.ShaderUtil.ShaderHasError(UnityEngine.Shader.Find("Svinki/Pig Skin"))};
}
finally
{
 UnityEngine.RenderTexture.active=previous;camera.transform.localPosition=position;camera.transform.localRotation=rotation;camera.fieldOfView=fov;pig.Apply(PigFace.Selected);camera.Render();UnityEngine.Object.DestroyImmediate(image);
}
