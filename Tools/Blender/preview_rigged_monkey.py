import bpy
from mathutils import Vector
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.resolution_x=600;scene.render.resolution_y=600;scene.render.resolution_percentage=100
scene.world.color=(.3,.3,.3)
c=bpy.data.cameras.new('Preview');camera=bpy.data.objects.new('Preview',c);scene.collection.objects.link(camera);camera.location=(1.5,-2.5,1.5);camera.rotation_euler=(Vector((0,-.1,.45))-camera.location).to_track_quat('-Z','Y').to_euler();c.type='ORTHO';c.ortho_scale=1.6;scene.camera=camera
for pos,power,size in [((2,-2,3),150,3),((-2,-1,2),100,2)]:
 l=bpy.data.lights.new('Softbox','AREA');l.energy=power;l.shape='DISK';l.size=size;o=bpy.data.objects.new('Softbox',l);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,.4))-o.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=bpy.path.abspath('//rigged-preview.png');bpy.ops.render.render(write_still=True)
