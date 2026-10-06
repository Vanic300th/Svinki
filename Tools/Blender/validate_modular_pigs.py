"""Validate source geometry, skinning, and actual FBX reimports."""
from pathlib import Path
import bpy
import bmesh
import json
import math
from mathutils import Vector
from io_scene_fbx import parse_fbx

PROJECT=Path(__file__).resolve().parents[2]
SOURCE=PROJECT/'ArtSource/Pigs/v2'
EXPORT=PROJECT/'Assets/Art/Characters/Pigs'
manifest=json.loads((SOURCE/'appearance_manifest.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Svinki_Modular_Pigs.blend'))
scene=bpy.data.scenes['02_Modular_Master']
bpy.context.window.scene=scene
rig=scene.objects['Pig_Rig']
assert len(rig.data.bones)==16
meshes=[o for o in scene.objects if o.type=='MESH' and o.get('modular_pig')]
assert len(meshes)==len(manifest['mesh_stats'])
results={'source_meshes':len(meshes),'bones':16,'slots':len(manifest['slots']),
         'geometry':{},'fbx_imports':{}}

for o in meshes:
    assert o.data.uv_layers, f'No UVs: {o.name}'
    armatures=[m for m in o.modifiers if m.type=='ARMATURE']
    assert len(armatures)==1 and armatures[0].object==rig, o.name
    for v in o.data.vertices:
        total=sum(g.weight for g in v.groups)
        assert abs(total-1)<.0001, (o.name,v.index,total)
    bm=bmesh.new(); bm.from_mesh(o.data)
    nonmanifold=sum(not e.is_manifold for e in bm.edges)
    degenerate=sum(f.calc_area()<1e-10 for f in bm.faces)
    assert nonmanifold==0, (o.name,'nonmanifold',nonmanifold)
    assert degenerate==0, (o.name,'degenerate',degenerate)
    results['geometry'][o.name]={'nonmanifold_edges':nonmanifold,'degenerate_faces':degenerate}
    bm.free()

for name,children in manifest['slots'].items():
    root=scene.objects[name]
    assert root.type=='EMPTY'
    assert sorted(c.name for c in root.children)==sorted(children)
    assert root.parent==rig
    for child in root.children:
        assert len(child.vertex_groups)==1 and child.vertex_groups[0].name=='Head'

# Check accessories against the exact Head-bone transform, rather than merely
# checking whether an Armature modifier exists.
rest={}
deps=bpy.context.evaluated_depsgraph_get()
for children in manifest['slots'].values():
    for name in children:
        o=scene.objects[name]
        evaluated=o.evaluated_get(deps)
        rest[name]=[evaluated.matrix_world@v.co for v in evaluated.data.vertices]
head=rig.pose.bones['Head']
head.rotation_mode='XYZ'
head.rotation_euler=(math.radians(12),0,math.radians(18))
bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get()
matrix=rig.matrix_world@head.matrix@head.bone.matrix_local.inverted()@rig.matrix_world.inverted()
max_error=0
for name,vertices in rest.items():
    evaluated=scene.objects[name].evaluated_get(deps)
    for old,v in zip(vertices,evaluated.data.vertices):
        err=(matrix@old-evaluated.matrix_world@v.co).length
        max_error=max(err,max_error)
assert max_error<.00002, ('Head-follow error',max_error)
results['head_follow_max_error_m']=max_error
head.rotation_euler=(0,0,0)
bpy.context.view_layer.update()

# Exercise both arms and legs and ensure connected skin moves while staying
# finite; the final source file remains in its neutral pose.
body=scene.objects['Body']
before=[body.matrix_world@v.co for v in body.evaluated_get(deps).data.vertices]
for name in ('UpperArm_L','UpperArm_R','Thigh_L','Thigh_R'):
    bone=rig.pose.bones[name]; bone.rotation_mode='XYZ'
    bone.rotation_euler[0]=math.radians(18)
bpy.context.view_layer.update()
after=[body.matrix_world@v.co for v in body.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.vertices]
assert len(before)==len(after)
moved=sum((a-b).length>.002 for a,b in zip(before,after))
assert moved>100
assert all(math.isfinite(axis) for p in after for axis in p)
results['limb_pose_moved_vertices']=moved

files={'Pig_Modular.fbx':list(manifest['slots'])}
files.update({'Pig_'+name+'.fbx':slots for name,slots in manifest['presets'].items()})
for filename,slots in files.items():
    # Blender's FBX importer reparents skinned objects to their armature.
    # Check the serialized FBX hierarchy itself before testing its reimport.
    fbx,_=parse_fbx.parse(str(EXPORT/filename))
    fbx_objects=next(e for e in fbx.elems if e.id==b'Objects')
    ids={}
    for e in fbx_objects.elems:
        if e.id==b'Model':
            name=e.props[1].split(b'\x00\x01')[0].decode()
            ids[name.removeprefix('Model::')]=e.props[0]
    fbx_connections=next(e for e in fbx.elems if e.id==b'Connections')
    links={(e.props[1],e.props[2]) for e in fbx_connections.elems
           if e.id==b'C' and e.props[0]==b'OO'}
    for slot in slots:
        for name in manifest['slots'][slot]:
            assert (ids[name],ids[slot]) in links, (filename,name,'lost FBX slot link')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(EXPORT/filename))
    objects=bpy.context.scene.objects
    imported_meshes=[o for o in objects if o.type=='MESH']
    expected=set(manifest['base_objects'])
    for slot in slots:
        expected.update(manifest['slots'][slot])
        assert slot in objects, (filename,'missing slot',slot)
    actual=set(o.name for o in imported_meshes)
    assert actual==expected, (filename,'mesh names','missing',sorted(expected-actual),'extra',sorted(actual-expected))
    armatures=[o for o in objects if o.type=='ARMATURE']
    assert len(armatures)==1 and len(armatures[0].data.bones)==16
    for slot in slots:
        for name in manifest['slots'][slot]:
            obj=objects[name]
            assert any(m.type=='ARMATURE' for m in obj.modifiers), (filename,name,'lost skin')
            assert obj.vertex_groups.get('Head'), (filename,name,'lost Head weights')
    results['fbx_imports'][filename]={'mesh_count':len(imported_meshes),'bones':16,
                                     'appearance_roots_preserved':True}

(SOURCE/'validation_report.json').write_text(json.dumps(results,indent=2))
print('VALIDATION_PASSED',json.dumps({k:v for k,v in results.items() if k!='geometry'}))
