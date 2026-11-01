"""Extend the preserved v2 source with rigged v3 accessories; export the existing Unity model path."""
from pathlib import Path
import bpy, ast, math, json, random, bmesh, shutil
from mathutils import Vector
PROJECT=Path(__file__).resolve().parents[2]
SOURCE=PROJECT/'ArtSource/Pigs/v3';SOURCE.mkdir(parents=True,exist_ok=True)
old=PROJECT/'ArtSource/Pigs/v2/Svinki_Modular_Pigs.blend'
bpy.ops.wm.open_mainfile(filepath=str(old))
scene=bpy.data.scenes['02_Modular_Master'];bpy.context.window.scene=scene
# Reuse the established mesh and skinning helpers without running the old scene generator.
module=ast.parse((PROJECT/'Tools/Blender/build_modular_pigs.py').read_text())
names={'material','activate','finish','sphere','mesh','join','tube','skin','empty','slot'}
for node in module.body:
 if isinstance(node,ast.FunctionDef) and node.name in names:exec(compile(ast.Module(body=[node],type_ignores=[]),str(old),'exec'))
rig=next(o for o in scene.objects if o.type=='ARMATURE')
HAIR=bpy.data.materials['Pig_FacialHair'];FRAME=bpy.data.materials['Pig_Glasses'];LENS=bpy.data.materials['Pig_SunLenses'];EYE=bpy.data.materials['Pig_Eyes']
BASE=material('Pig_HairBase',(.20,.13,.09));ACCENT=material('Pig_HairAccent',(.48,.28,.55));TIPS=material('Pig_HairTips',(.68,.32,.48))
WHITE=material('Pig_EyeWhite',(.98,.96,.91));RED=material('Pig_KaminaRed',(.65,.035,.045));CHROME=bpy.data.materials['Pig_Piercings']
created=[]
def option(name,parts):
 root=slot(name,[join(parts,name.replace("__","_")) if len(parts)>1 else parts[0]],rig);created.append(root);return root

def cap(name,mat,angle=1.1):
 verts=[(0,0,1.813)];faces=[];sides=48;rings=12
 for i in range(1,rings+1):
  theta=angle*i/rings
  for j in range(sides):
   phi=2*math.pi*j/sides;verts.append((.404*math.sin(theta)*math.cos(phi),.306*math.sin(theta)*math.sin(phi),1.43+.383*math.cos(theta)))
 for j in range(sides):faces.append((0,1+j,1+(j+1)%sides))
 for i in range(rings-1):
  for j in range(sides):a=1+i*sides+j;b=1+i*sides+(j+1)%sides;faces.append((a,a+sides,b+sides,b))
 # A narrow inner lip gives the cut a closed silhouette.
 edge=1+(rings-1)*sides
 for j in range(sides):x,y,z=verts[edge+j];verts.append((x*.985,y*.985,z+.018))
 inner=len(verts)-sides
 for j in range(sides):k=(j+1)%sides;faces.append((edge+j,edge+k,inner+k,inner+j))
 return mesh(name,verts,faces,mat)

def strand(name,points,radius,mat=BASE,radii=None):return tube(name,points,radius,mat,radii,sides=10)

def lock(name,points,widths,depth=.025,mat=BASE):
 # Smooth broad locks, rather than strings of cylindrical segments.
 samples=[];sizes=[]
 for i in range(len(points)-1):
  a=Vector(points[max(0,i-1)]);b=Vector(points[i]);c=Vector(points[i+1]);d=Vector(points[min(len(points)-1,i+2)])
  for j in range(7):
   t=j/7
   samples.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
   sizes.append(widths[i]*(1-t)+widths[i+1]*t)
 samples.append(Vector(points[-1]));sizes.append(widths[-1])
 verts=[];faces=[];sides=12
 for i,p in enumerate(samples):
  tangent=(samples[min(i+1,len(samples)-1)]-samples[max(i-1,0)]).normalized()
  normal=tangent.cross(Vector((0,1,0))).normalized()
  if normal.length<.01:normal=Vector((1,0,0))
  second=tangent.cross(normal).normalized()
  for j in range(sides):
   a=j*2*math.pi/sides
   verts.append(p+normal*(math.cos(a)*sizes[i])+second*(math.sin(a)*depth*min(1,sizes[i]/.025)))
 for i in range(len(samples)-1):
  for j in range(sides):
   k=(j+1)%sides;faces.append((i*sides+j,i*sides+k,(i+1)*sides+k,(i+1)*sides+j))
 faces.extend([tuple(reversed(range(sides))),tuple((len(samples)-1)*sides+j for j in range(sides))])
 return mesh(name,verts,faces,mat)

def smoothstep(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)

def front_angle(phi):return math.atan2(math.cos(phi),-math.sin(phi))

def solid_hair(name,hem,sculpt=None,columns=160,rows=40,outline=False):
 # One continuous outer surface, closed at its rim. No separate cap under fringe.
 # The inward wall is thickness within the same watertight volume.
 verts=[(0,0,1.832)];vertex_ink=[0.0];faces=[]
 for r in range(1,rows+1):
  t=r/rows
  for j in range(columns):
   phi=2*math.pi*j/columns;z=1.832-(1.832-hem(phi))*t*t
   radius=math.sqrt(max(0,1-((z-1.43)/.402)**2)) if z>1.43 else 1
   flare=1+.035*math.sin(min(1,max(0,(1.43-z)/.30))*math.pi)
   p=Vector((.418*radius*math.cos(phi)*flare,.327*radius*math.sin(phi)*flare,z))
   ink=0.0
   if sculpt:p,ink=sculpt(p,phi,t)
   vertex_ink.append(ink)
   verts.append(tuple(p))
 for j in range(columns):faces.append((0,1+j,1+(j+1)%columns))
 for r in range(rows-1):
  for j in range(columns):
   a=1+r*columns+j;b=1+r*columns+(j+1)%columns;faces.append((a,a+columns,b+columns,b))
 outer_count=len(verts);outer_faces=list(faces)
 for p in list(verts):
  v=Vector(p);direction=Vector((v.x/.418**2,v.y/.327**2,max(.04,v.z-1.43)/.402**2)).normalized()
  verts.append(tuple(v-direction*.014))
 faces.extend(tuple(i+outer_count for i in reversed(face)) for face in outer_faces)
 rim=1+(rows-1)*columns
 for j in range(columns):
  a=rim+j;b=rim+(j+1)%columns;faces.append((a,b,b+outer_count,a+outer_count))
 obj=mesh(name,verts,faces,BASE)
 if outline:
  obj.data.materials.clear();obj.data.materials.append(LEOPARD)
  attribute=obj.data.color_attributes.new(name='HairInk',type='FLOAT_COLOR',domain='CORNER')
  vertex_ink.extend(vertex_ink)
  for loop in obj.data.loops:
   ink=vertex_ink[loop.vertex_index]
   attribute.data[loop.index].color=(ink,ink,ink,1)
 obj['continuous_hair_volume']=True
 return obj

# Replace the full beard with a compact pointed goatee.
old_beard=scene.objects.get('Beard__Short')
if old_beard:
 for o in list(old_beard.children_recursive)+[old_beard]:bpy.data.objects.remove(o,do_unlink=True)
goatee=[sphere('Goatee_Chin',(0,-.311,1.112),(.061,.034,.078),HAIR)]
for x in [-.033,0,.033]:goatee.append(strand('Goatee_Tuft',[(x,-.327,1.125),(x*.7,-.330,1.06),(x*.2,-.313,1.008)],.018,HAIR,[1,.8,.04]))
option('Beard__Goatee',goatee)
# Comically protruding whites and small pupils.
e=[]
for s in [-1,1]:
 e += [sphere('Bulging_White',(s*.151,-.306,1.473),(.076,.073,.082),WHITE),sphere('Bulging_Pupil',(s*.151,-.375,1.473),(.024,.012,.031),EYE),sphere('Bulging_Glint',(s*.144,-.387,1.486),(.006,.004,.008),WHITE,12,8)]
option('Eyes__Bulging',e)
option('Brows__Unibrow',[strand('Unibrow',[(-.258,-.254,1.589),(-.17,-.286,1.608),(0,-.304,1.581),(.17,-.286,1.608),(.258,-.254,1.589)],.024,HAIR,[.2,1,1,1,.2])])
for name in ['Thick','Angry']:
 parts=[]
 for s in [-1,1]:
  if name=='Thick':pts=[(s*.075,-.292,1.57),(s*.145,-.282,1.611),(s*.244,-.255,1.588)]
  else:pts=[(s*.055,-.303,1.548),(s*.151,-.286,1.606),(s*.267,-.250,1.653)]
  parts.append(strand(name+'Brow',pts,.032 if name=='Thick' else .03,HAIR,[.7,1,.25]))
 option('Brows__'+name,parts)
for name in ['Bushy','LongCurled']:
 parts=[]
 for s in [-1,1]:
  if name=='Bushy':
   parts.append(sphere('Bushy_Moustache',(s*.105,-.34,1.17),(.137,.06,.063),HAIR))
   for i in range(6):parts.append(strand('Bushy_Edge',[(s*(.02+i*.032),-.373,1.19),(s*(.03+i*.032),-.378,1.135)],.012,HAIR,[1,.12]))
  else:
   pts=[(s*x,y,z) for x,y,z in [(0,-.34,1.19),(.07,-.363,1.155),(.17,-.346,1.142),(.275,-.30,1.17),(.32,-.26,1.22),(.30,-.265,1.267),(.258,-.29,1.259),(.262,-.29,1.231)]]
   parts.append(strand('Long_Curl',pts,.041,HAIR,[.7,1,1,.8,.5,.28,.12,.03]))
 option('Mustache__'+name,parts)
# Hair fringes are contours of a single sculpted surface, not meshes over a cap.
def curtains_hem(phi):
 a=abs(front_angle(phi))
 return 1.61+.12*math.exp(-(a/.22)**2)-.19*math.exp(-((a-.76)/.28)**2)
option('Hair__Curtains',[solid_hair('Curtains_One_Volume',curtains_hem)])
parts=[cap('Ponytail_Cap',BASE,1.12)]
for i in range(6):
 a=2*math.pi*i/6;dx=.045*math.cos(a);dy=.04*math.sin(a)
 parts.append(lock('Full_Ponytail',[(dx,.26+dy,1.68),(.025+dx,.43+dy,1.62),(.06+dx,.53+dy,1.42),(.08+dx*.5,.48+dy*.5,1.18)],[.025,.055,.05,.003],.034))
parts.append(tube('Ponytail_Tie',[(.073*math.cos(j*2*math.pi/32),.345,1.647+.064*math.sin(j*2*math.pi/32)) for j in range(32)],.012,BASE,closed=True))
option('Hair__Ponytail',parts)
def barcode_hem(phi):
 a=front_angle(phi)
 if abs(a)>1.16:return 1.67
 stripe=(a+1.16)/.154;local=stripe-math.floor(stripe)
 bar=1-smoothstep(.26,.35,abs(local-.5))
 return 1.69-(.13+.012*(int(stripe)%3))*bar
option('Hair__Barcode',[solid_hair('Barcode_Short_One_Volume',barcode_hem,columns=288,rows=36)])
head=cap('Bowl_Cap',BASE,1.28)
# A continuous blunt hem with small irregularities keeps the bowl silhouette smooth.
for v in head.data.vertices:
 if v.co.z<1.59:v.co.z+=.004*math.sin(v.co.x*63)+.003*math.cos(v.co.y*49)
parts=[head]
option('Hair__Bowl',parts)
def emo_hem(phi):
 a=front_angle(phi)
 cover=smoothstep(-.22,.26,a)*(1-smoothstep(1.50,2.0,a))
 return 1.64-.51*cover+.035*math.exp(-((a+.38)/.25)**2)
def emo_sculpt(p,phi,t):
 p.y-=.078*max(0,-math.sin(phi))**2*(1-smoothstep(1.55,1.75,p.z))
 return p,False
option('Hair__Emo',[solid_hair('Emo_Wrapped_Right_One_Volume',emo_hem,emo_sculpt)])
def bob_hem(phi):
 a=abs(front_angle(phi))
 return 1.17+.56*math.exp(-(a/.57)**4)
option('Hair__SplitBob',[solid_hair('Bob_One_Continuous_Volume',bob_hem)])
parts=[cap('Dread_Cap',BASE,.90)]
for i in range(15):
 a=2*math.pi*i/15;start=(.11*math.cos(a),.10*math.sin(a),1.79);p1=(.29*math.cos(a),.23*math.sin(a),1.82);p2=(.36*math.cos(a+.12),.30*math.sin(a+.12),1.62);p3=(.33*math.cos(a+.2),.32*math.sin(a+.2),1.46)
 parts.append(strand('Dread_Root',[start,p1,p2],.029,BASE if i%3 else ACCENT,[.7,1,.86]));parts.append(strand('Dread_Tip',[p2,p3],.026,TIPS,[1,.3]))
option('Hair__Dreads',parts)
parts=[cap('Pompadour_Cap',BASE,.95)]
for i in range(9):
 x=-.18+i*.045;parts.append(strand('Swept_Quiff',[(x,-.24,1.64),(x,-.26,1.84),(x*.9,-.12,1.947),(x*.7,.12,1.83)],.037,BASE,[.45,1,1,.15]))
option('Hair__Pompadour',parts)
LEOPARD=material('Pig_HairLeopardBase',(.20,.13,.09))
nodes=LEOPARD.node_tree.nodes;links=LEOPARD.node_tree.links
attribute=nodes.new('ShaderNodeVertexColor');attribute.layer_name='HairInk'
mix=nodes.new('ShaderNodeMixRGB');mix.inputs[1].default_value=(.20,.13,.09,1);mix.inputs[2].default_value=(.012,.010,.009,1)
links.new(attribute.outputs['Color'],mix.inputs[0]);links.new(mix.outputs[0],nodes.get('Principled BSDF').inputs['Base Color'])
rosettes=[]
for i in range(21):
 rosettes.append((.15+.87*math.sqrt((i+.5)/21),i*2.39996%(2*math.pi),i))
def leopard_sculpt(p,phi,t):
 theta=math.acos(max(-1,min(1,(p.z-1.43)/.402)))
 distance=100
 for center_theta,center_phi,i in rosettes:
  dp=(phi-center_phi+math.pi)%(2*math.pi)-math.pi
  x=(theta-center_theta)/.064;y=dp*max(.4,math.sin(center_theta))/.12
  a=math.atan2(y,x)
  irregular=1+.20*math.sin(a*3+i*1.73)+.10*math.cos(a*5+i)
  distance=min(distance,abs(math.hypot(x,y)-irregular))
 ridge=.012*(1-smoothstep(.10,.60,distance))
 normal=Vector((p.x/.418**2,p.y/.327**2,(p.z-1.43)/.402**2)).normalized()
 return p+normal*ridge,1-smoothstep(.30,.55,distance)
option('Hair__LeopardBuzz',[solid_hair('Leopard_Raised_One_Surface',lambda phi:1.60,leopard_sculpt,columns=256,rows=72,outline=True)])
# New eyewear uses closed front/back lenses and actual temple arms.
def eyewear(name,outline,sun=False,mat=FRAME):
 parts=[]
 depth=-.46 if name=='GiantRound' else -.385
 for s in [-1,1]:
  pts=[(s*(.151+x),depth,1.468+z) for x,z in outline];parts.append(tube(name+'_Rim',pts,.014,mat,closed=True))
  parts.append(strand(name+'_Temple',[(s*.27,depth,1.49),(s*.36,-.18,1.49),(s*.375,.01,1.445)],.012,mat))
  if sun:
   n=len(pts);verts=pts+[(x,y+.016,z) for x,y,z in pts];faces=[tuple(range(n)),tuple(reversed(range(n,2*n)))]+[(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)];parts.append(mesh(name+'_Lens',verts,faces,LENS))
 parts.append(strand(name+'_Bridge',[(-.045,depth,1.48),(0,depth-.005,1.492),(.045,depth,1.48)],.013,mat))
 return parts
heart=[]
for i in range(64):
 a=2*math.pi*i/64;heart.append((.008*16*math.sin(a)**3,.008*(13*math.cos(a)-5*math.cos(2*a)-2*math.cos(3*a)-math.cos(4*a))))
option('Glasses__Heart',eyewear('Heart',heart))
option('Glasses__GiantRound',eyewear('GiantRound',[(.143*math.cos(i*2*math.pi/64),.151*math.sin(i*2*math.pi/64)) for i in range(64)]))
pixel=[(-.12,.07),(.10,.07),(.10,.04),(.13,.04),(.13,-.03),(.10,-.03),(.10,-.07),(-.08,-.07),(-.08,-.04),(-.12,-.04)]
parts=eyewear('Pixel',pixel,True)
for s in [-1,1]:
 for i in range(3):
  bpy.ops.mesh.primitive_cube_add(size=1,location=(s*(.09+i*.027),-.403,1.49-i*.024));o=finish(bpy.context.object,'Pixel_Glint',WHITE);o.scale=(.023,.006,.018);activate(o);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);parts.append(o)
option('Glasses__Pixel',parts)
option('Glasses__Kamina',eyewear('Kamina',[(-.13,.043),(.28,.16),(.15,-.105)],True,RED))
hand=[(-.10,-.075),(.08,-.075),(.10,-.01),(.07,.03),(.055,.015),(.043,.015),(.043,.17),(.002,.17),(.002,.015),(-.025,.015),(-.025,.055),(-.06,.055),(-.06,.015),(-.10,.025)]
option('Glasses__MiddleFinger',eyewear('MiddleFinger',hand,True))
square=[(-.10,-.075),(.10,-.075),(.115,-.05),(.115,.075),(-.10,.075)]
parts=eyewear('Spiked',square)
for s in [-1,1]:
 for i in range(6):
  a=.1+math.pi*i/5;direction=Vector((s*math.cos(a),0,math.sin(a)));point=Vector((s*.151,-.385,1.468))+Vector((s*.12*math.cos(a),0,.095*math.sin(a)));bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=.014,radius2=.001,depth=.063,location=point+direction*.026);o=finish(bpy.context.object,'Frame_Spike',CHROME);o.rotation_euler=direction.to_track_quat('Z','Y').to_euler();parts.append(o)
option('Glasses__Spiked',parts)
# All hair is one editable colour. Legacy colour fields remain readable in saved codes.
for root in scene.objects:
 if root.type=='EMPTY' and root.name.startswith('Hair__'):
  for obj in root.children_recursive:
   if obj.type!='MESH':continue
   if any(m.name=='Pig_HairLeopardBase' for m in obj.data.materials):continue
   indices=[0 if obj.data.materials[p.material_index].name!='Pig_LeopardOutline' else 1 for p in obj.data.polygons]
   obj.data.materials.clear();obj.data.materials.append(BASE)
   for polygon,index in zip(obj.data.polygons,indices):polygon.material_index=index
# Normals, deterministic names, default visibility, and a standalone editable source.
for root in created:
 root['appearance_slot']=root.name.split('__')[0];root['option_id']=root.name.split('__')[1]
 for o in root.children:
  if o.type=='MESH':
   bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
continuous_validation={}
for name in ['Curtains','Barcode','Emo','SplitBob','LeopardBuzz']:
 root=scene.objects['Hair__'+name]
 obj=next(o for o in root.children_recursive if o.type=='MESH')
 bm=bmesh.new();bm.from_mesh(obj.data)
 remaining=set(bm.verts);components=0
 while remaining:
  components+=1;pending=[remaining.pop()]
  while pending:
   for edge in pending.pop().link_edges:
    for vertex in edge.verts:
     if vertex in remaining:remaining.remove(vertex);pending.append(vertex)
 open_edges=sum(not e.is_manifold for e in bm.edges)
 continuous_validation[root.name]={'connected_volumes':components,'non_manifold_edges':open_edges,'mesh_objects':sum(o.type=='MESH' for o in root.children_recursive)}
 bm.free()
 assert components==1 and open_edges==0,continuous_validation[root.name]
for root in scene.objects:
 if root.type=='EMPTY' and '__' in root.name:
  show=root.name in {'Eyes__Open','Brows__Neutral'}
  for o in [root,*root.children_recursive]:o.hide_set(not show);o.hide_render=not show
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Svinki_Modular_Pigs_v3.blend'))
objects=[o for o in [rig,*rig.children_recursive] if o.type in {'MESH','ARMATURE','EMPTY'}]
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.hide_set(False);o.hide_viewport=False;o.hide_render=False;o.select_set(True)
bpy.context.view_layer.objects.active=rig
export=PROJECT/'Assets/_Project/Content/Characters/Pigs/Pig_Modular.fbx'
backup=PROJECT/'Tools/UI/.deps/Pig_Modular_before_v3.fbx'
if not backup.exists():shutil.copy2(export,backup)
bpy.ops.export_scene.fbx(filepath=str(export),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',use_custom_props=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,apply_scale_options='FBX_SCALE_UNITS')
report={'new_slots':[o.name for o in created],'slots':sorted(o.name for o in objects if o.type=='EMPTY' and '__' in o.name),'meshes':sum(o.type=='MESH' for o in objects),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects if o.type=='MESH'),'continuous_hair':continuous_validation}
(SOURCE/'model-manifest.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
