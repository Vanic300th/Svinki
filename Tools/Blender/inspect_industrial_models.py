import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
report={}
for path in (root/'Assets/_Project/Content/Environment/IndustrialCC0').glob('*/*.fbx'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    report[path.parent.name]=[]
    for o in bpy.context.scene.objects:
        if o.type!='MESH':continue
        verts=[o.matrix_world@Vector(v) for v in o.bound_box]
        low=[min(v[i] for v in verts) for i in range(3)];high=[max(v[i] for v in verts) for i in range(3)]
        report[path.parent.name].append({'name':o.name,'min':low,'max':high,'materials':[m.name for m in o.data.materials if m]})
(root/'ArtSource/IndustrialCC0/fbx-inspection.json').write_text(json.dumps(report,indent=2))
