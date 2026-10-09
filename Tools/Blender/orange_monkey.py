import bpy, os
# Recolor only the rigged toy copy; preserve the original animal library.
mesh=next(o.data for o in bpy.data.objects if o.type=='MESH')
for attribute in mesh.color_attributes:
 for item in attribute.data:
  r,g,b,a=item.color
  if max(r,g,b)<.012 or min(r,g,b)>.7: continue # Eyes stay black and white.
  if r/max(g,.001)<1.35:
   shade=max(.78,min(1.12,(r+g+b)/.25))
   item.color=(.72*shade,.18*shade,.025*shade,a)
  else:
   shade=max(.72,min(1.05,(r+g+b)/.45))
   item.color=(.88*shade,.62*shade,.32*shade,a)
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
 if o.type in {'MESH','ARMATURE'}:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.abspath('Assets/Art/Items/MonkeyToy/ChimpanzeeToy.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,mesh_smooth_type='FACE')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath('ArtSource/MonkeyToy/ChimpanzeeToy.blend'))
print('ORANGE_PLUSH_EXPORTED')
