import bpy,math,os
from mathutils import Vector,Matrix
project=os.path.abspath('.')
chimp=bpy.data.objects['Chimpanzee'];source=chimp.children[0];world=source.matrix_world.copy();source.parent=None;source.matrix_world=world
for o in list(bpy.data.objects):
 if o!=source:bpy.data.objects.remove(o,do_unlink=True)
for c in list(source.users_collection):c.objects.unlink(source)
bpy.context.scene.collection.objects.link(source)
source.name='Chimpanzee Toy';source.hide_render=False;source.hide_viewport=False;source.hide_set(False)
for c in bpy.data.collections:c.hide_render=False;c.hide_viewport=False
bpy.context.view_layer.objects.active=source;source.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bones=[('Hips',(0,.16,.60),(0,.06,.68),None),('Spine',(0,.06,.68),(0,-.23,.70),'Hips'),('Head',(0,-.23,.70),(0,-.49,.66),'Spine')]
for sign,suffix in [(1,'L'),(-1,'R')]:
 bones += [('UpperArm_'+suffix,(sign*.18,-.11,.70),(sign*.23,-.24,.36),'Spine'),('Forearm_'+suffix,(sign*.23,-.24,.36),(sign*.23,-.40,.10),'UpperArm_'+suffix),('Hand_'+suffix,(sign*.23,-.40,.10),(sign*.23,-.47,.03),'Forearm_'+suffix),('Thigh_'+suffix,(sign*.13,.15,.60),(sign*.18,.22,.31),'Hips'),('Shin_'+suffix,(sign*.18,.22,.31),(sign*.18,.12,.065),'Thigh_'+suffix),('Foot_'+suffix,(sign*.18,.12,.065),(sign*.18,-.06,.035),'Shin_'+suffix)]
armData=bpy.data.armatures.new('Monkey Toy Rig');arm=bpy.data.objects.new('Monkey Toy Rig',armData);bpy.context.scene.collection.objects.link(arm)
source.select_set(False);arm.select_set(True);bpy.context.view_layer.objects.active=arm;bpy.ops.object.mode_set(mode='EDIT')
for name,head,tail,parent in bones:
 b=armData.edit_bones.new(name);b.head=head;b.tail=tail
 if parent:b.parent=armData.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');source.select_set(True);bpy.context.view_layer.objects.active=arm;bpy.ops.object.parent_set(type='ARMATURE_AUTO')
if not source.vertex_groups:
 # Deterministic skinning fallback for this low-poly model.
 groups={name:source.vertex_groups.new(name=name) for name,_,_,_ in bones}
 def distance(point,a,b):
  delta=b-a;t=max(0,min(1,(point-a).dot(delta)/delta.length_squared));return (point-(a+delta*t)).length
 for v in source.data.vertices:
  x,y,z=v.co
  side='L' if x>0 else 'R'
  if z>.48 and y<-.25 and abs(x)<.21:candidates=['Head']
  elif abs(x)>.16 and y<.05:candidates=['UpperArm_'+side,'Forearm_'+side,'Hand_'+side]
  elif y>.01 and z<.58:candidates=['Thigh_'+side,'Shin_'+side,'Foot_'+side]
  else:candidates=['Hips','Spine']
  scores=sorted((distance(v.co,Vector(h),Vector(t)),name) for name,h,t,_ in bones if name in candidates)[:2]
  weights=[1/max(.025,d)**3 for d,_ in scores];total=sum(weights)
  for (_,name),weight in zip(scores,weights):groups[name].add([v.index],weight/total,'REPLACE')
for v in source.data.vertices:
 if not v.groups:
  nearest=min(bones,key=lambda b:(v.co-(Vector(b[1])+Vector(b[2]))*.5).length_squared)[0]
  source.vertex_groups[nearest].add([v.index],1,'REPLACE')
group_names=[g.name for g in source.vertex_groups];saved_weights=[[(g.group,g.weight) for g in v.groups] for v in source.data.vertices]
print('SKIN_WEIGHTS',len(group_names),sum(bool(w) for w in saved_weights))
# A hanging-toy rest pose: arms raised, wrists above the head, feet hanging below.
for side in ['L','R']:
 bone=armData.bones['UpperArm_'+side];head=bone.head_local
 arm.pose.bones[bone.name].matrix=Matrix.Translation(head)@Matrix.Rotation(math.radians(155),4,'X')@Matrix.Translation(-head)@bone.matrix_local
bpy.context.view_layer.update()
evaluated=source.evaluated_get(bpy.context.evaluated_depsgraph_get());posed=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get());source.data=posed
source.vertex_groups.clear()
for name in group_names:source.vertex_groups.new(name=name)
for i,weights in enumerate(saved_weights):
 for group,weight in weights:source.vertex_groups[group].add([i],weight,'REPLACE')
for m in list(source.modifiers):
 if m.type=='ARMATURE':source.modifiers.remove(m)
bpy.context.view_layer.objects.active=arm;bpy.ops.object.mode_set(mode='POSE');bpy.ops.pose.armature_apply(selected=False);bpy.ops.object.mode_set(mode='OBJECT')
mod=source.modifiers.new('Toy skin','ARMATURE');mod.object=arm
# Apply the toy scale to mesh and rig data so runtime physics has uniform scale.
for o in [arm,source]:o.scale=(.42,.42,.42)
# Mesh inherits the armature; only scale its data locally once, not twice.
source.scale=(1,1,1)
source.select_set(True);arm.select_set(True);bpy.context.view_layer.objects.active=arm;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
os.makedirs(os.path.join(project,'Assets/Art/Items/MonkeyToy'),exist_ok=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(project,'Assets/Art/Items/MonkeyToy/ChimpanzeeToy.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,mesh_smooth_type='FACE')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(project,'ArtSource/MonkeyToy/ChimpanzeeToy.blend'))
print('RIGGED_MONKEY',len(armData.bones),len(source.data.vertices),[g.name for g in source.vertex_groups])
