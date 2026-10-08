import bpy
from mathutils import Vector
chimp=bpy.data.objects['Chimpanzee']; world=chimp.matrix_world.copy(); keep=set([chimp]+list(chimp.children_recursive))
for o in list(bpy.data.objects):
 if o not in keep:bpy.data.objects.remove(o,do_unlink=True)
chimp.parent=None;chimp.matrix_world=world
for o in keep:o.hide_render=False;o.hide_viewport=False;o.hide_set(False)
for c in bpy.data.collections:c.hide_render=False;c.hide_viewport=False
def show(l):
 l.exclude=False;l.hide_viewport=False
 for c in l.children:show(c)
show(bpy.context.view_layer.layer_collection)
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.resolution_x=600;scene.render.resolution_y=600;scene.render.resolution_percentage=100
scene.world.color=(.3,.3,.3)
c=bpy.data.cameras.new('Preview');camera=bpy.data.objects.new('Preview',c);scene.collection.objects.link(camera);camera.location=(1.5,-2.5,1.5);camera.rotation_euler=(Vector((0,-.1,.45))-camera.location).to_track_quat('-Z','Y').to_euler();c.type='ORTHO';c.ortho_scale=1.4;scene.camera=camera
for pos,power,size in [((2,-2,3),150,3),((-2,-1,2),100,2)]:
 l=bpy.data.lights.new('Softbox','AREA');l.energy=power;l.shape='DISK';l.size=size;o=bpy.data.objects.new('Softbox',l);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,.4))-o.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=bpy.path.abspath('//../../ArtSource/MonkeyToy/source-preview.png');bpy.ops.render.render(write_still=True)
