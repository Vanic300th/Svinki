import bpy,json
from mathutils import Vector
for o in bpy.data.objects:
 if any(s in o.name.lower() for s in ['chimp','gorilla','orang']):
  print('ANIMAL',o.name,o.type,'parent',o.parent.name if o.parent else None,'children',[(c.name,c.type) for c in o.children])
  for c in [o]+list(o.children_recursive):
   if c.type=='MESH':print('MESH',c.name,len(c.data.vertices),'bounds',[list(c.matrix_world@Vector(v)) for v in c.bound_box],'materials',[m.name if m else None for m in c.data.materials])
