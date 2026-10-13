"""Convert CC0 GLB/Blender parts into Unity-native FBX; preserve provenance."""
import bpy,json,os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DEST=ROOT/'Assets/Art/IndustrialCC0'
report={}
def describe():
    return [{'name':o.name,'type':o.type,'dimensions':list(o.dimensions),'location':list(o.location),
             'materials':[m.name for m in o.data.materials if m] if o.type=='MESH' else []} for o in bpy.context.scene.objects]
for name in ['wall_basketball_hoop','locker_bank']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path=DEST/name
    bpy.ops.import_scene.gltf(filepath=str(path/(name+'.glb')))
    report[name]=describe()
    mats=[]
    for mat in bpy.data.materials:
        node=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if mat.use_nodes else None
        entry={'name':mat.name,'color':list(node.inputs['Base Color'].default_value) if node else list(mat.diffuse_color),
               'metal':node.inputs['Metallic'].default_value if node else 0,'roughness':node.inputs['Roughness'].default_value if node else .65}
        if node and node.inputs['Base Color'].is_linked:
            tex=node.inputs['Base Color'].links[0].from_node
            if tex.type=='TEX_IMAGE':
                image=tex.image; image.filepath_raw=str(path/(mat.name.replace('/','_')+'.png'));image.file_format='PNG';image.save()
                entry['texture']=Path(image.filepath_raw).name
        mats.append(entry)
    (path/'materials.json').write_text(json.dumps(mats,indent=2))
    bpy.ops.export_scene.fbx(filepath=str(path/(name+'.fbx')),use_selection=False,object_types={'MESH','EMPTY'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/IndustrialCC0/dartboard-set.blend'))
report['dart_set']=describe()
for o in list(bpy.context.scene.objects):
    if o.type not in {'MESH','EMPTY'}:bpy.data.objects.remove(o,do_unlink=True)
for image in bpy.data.images:
    if image.type=='IMAGE' and image.size[0]>0:
        path=DEST/'dart_set';path.mkdir(exist_ok=True)
        image.filepath_raw=str(path/'dart_texture.png');image.file_format='PNG';image.save()
bpy.ops.export_scene.fbx(filepath=str(DEST/'dart_set/dart_set.fbx'),object_types={'MESH','EMPTY'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
(ROOT/'ArtSource/IndustrialCC0/model-inspection.json').write_text(json.dumps(report,indent=2))
