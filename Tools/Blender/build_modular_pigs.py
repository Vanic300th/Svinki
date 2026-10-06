"""Create the capsule-pig source file and modular FBX exports in local Blender.

Run: Blender --background --factory-startup --python build_modular_pigs.py
Writes v2 pig deliverables; preserves the original v1 Blender source.
"""
from pathlib import Path
import bpy
import json
import math
import sys
from mathutils import Vector, Quaternion

PROJECT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT / 'ArtSource/Pigs/v2'
EXPORT = PROJECT / 'Assets/Art/Characters/Pigs'
SOURCE.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.users == 0:
        bpy.data.collections.remove(c)


def material(name, color, roughness=.68, metallic=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Roughness'].default_value = roughness
    bs.inputs['Metallic'].default_value = metallic
    return m


PINK = material('Pig_Skin', (.81, .365, .32))
EAR = material('Pig_InnerEar', (.63, .22, .20))
NOSE = material('Pig_Snout', (.73, .235, .23))
HOOF = material('Pig_Hooves', (.115, .064, .046))
EYE = material('Pig_Eyes', (.015, .010, .008), .35)
HAIR = material('Pig_FacialHair', (.085, .037, .024))
FRAME = material('Pig_Glasses', (.025, .029, .032), .38)
LENS = material('Pig_SunLenses', (.022, .055, .072), .20, .25)
SILVER = material('Pig_Piercings', (.65, .70, .74), .24, .85)
FLOOR = material('Studio_WarmGray', (.30, .285, .265), .88)
TEXT = material('Studio_Text', (.065, .061, .055))


def activate(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def finish(obj, name, mat, apply_scale=True):
    obj.name = name
    if mat:
        obj.data.materials.append(mat)
    if apply_scale:
        activate(obj)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if obj.type == 'MESH':
        for p in obj.data.polygons:
            p.use_smooth = True
    return obj


def sphere(name, loc, scale, mat, segments=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    obj = bpy.context.object
    obj.scale = scale
    return finish(obj, name, mat)


def mesh(name, verts, faces, mat):
    data = bpy.data.meshes.new(name + '_Mesh')
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return finish(obj, name, mat)


def join(objects, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    objects[0].name = name
    return objects[0]


def tube(name, coords, radius, mat, radii=None, closed=False, sides=8):
    # An actual mesh, not a Blender-only curve, so accessories export reliably.
    points = [Vector(p) for p in coords]
    verts, faces = [], []
    for i, p in enumerate(points):
        before = points[(i-1) % len(points)] if closed or i else p
        after = points[(i+1) % len(points)] if closed or i < len(points)-1 else p
        tangent = (after-before).normalized()
        normal = tangent.cross(Vector((0, 1, 0)))
        if normal.length < .01:
            normal = tangent.cross(Vector((1, 0, 0)))
        normal.normalize()
        second = tangent.cross(normal).normalized()
        r = radius * (radii[i] if radii else 1)
        for j in range(sides):
            a = 2*math.pi*j/sides
            verts.append(p + r*(math.cos(a)*normal + math.sin(a)*second))
    for i in range(len(points) if closed else len(points)-1):
        n = (i+1) % len(points)
        for j in range(sides):
            k = (j+1) % sides
            faces.append((i*sides+j, i*sides+k, n*sides+k, n*sides+j))
    if not closed:
        faces.append(tuple(reversed(range(sides))))
        faces.append(tuple((len(points)-1)*sides+j for j in range(sides)))
    return mesh(name, verts, faces, mat)


def capsule():
    # Elliptical cross-section, long straight sides, soft rounded ends.
    profiles = []
    for i in range(1, 9):
        a = -math.pi/2 + i*math.pi/16
        profiles.append((.73 + .37*math.sin(a), math.cos(a)))
    profiles += [(z, 1) for z in (.83, .96, 1.09, 1.22, 1.35, 1.43)]
    for i in range(1, 8):
        a = i*math.pi/16
        profiles.append((1.43 + .37*math.sin(a), math.cos(a)))
    verts = [(0, 0, .36)]
    sides = 40
    for z, r in profiles:
        for j in range(sides):
            a = j*2*math.pi/sides
            verts.append((.395*r*math.cos(a), .295*r*math.sin(a), z))
    top = len(verts)
    verts.append((0, 0, 1.8))
    faces = []
    for j in range(sides):
        faces.append((0, 1+(j+1)%sides, 1+j))
    for i in range(len(profiles)-1):
        for j in range(sides):
            k = (j+1)%sides
            faces.append((1+i*sides+j, 1+i*sides+k, 1+(i+1)*sides+k, 1+(i+1)*sides+j))
    for j in range(sides):
        faces.append((top, 1+(len(profiles)-1)*sides+j, 1+(len(profiles)-1)*sides+(j+1)%sides))
    body = mesh('Body', verts, faces, PINK)
    parts = [body]
    for side in (-1, 1):
        arm = sphere('Arm', (side*.424, 0, .855), (.105, .10, .265), None)
        arm.rotation_euler[1] = -side*math.radians(23)
        parts.append(arm)
        parts.append(sphere('Leg', (side*.177, 0, .285), (.099, .106, .235), None))
    body = join(parts, 'Body')
    remesh = body.modifiers.new('Connected_body', 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = .018
    remesh.use_smooth_shade = True
    activate(body)
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    sm = body.modifiers.new('Soft_joints', 'SMOOTH')
    sm.factor = .65
    sm.iterations = 4
    bpy.ops.object.modifier_apply(modifier=sm.name)
    dec = body.modifiers.new('Game_mesh', 'DECIMATE')
    dec.ratio = .32
    bpy.ops.object.modifier_apply(modifier=dec.name)
    return finish(body, 'Body', None)


def ear(side):
    # Rounded thick folded leaf: front inset uses a second material.
    outline = [(.17, .015, 1.60), (.19, .015, 1.80), (.315, .015, 1.985),
               (.405, -.07, 1.955), (.555, -.15, 1.79), (.565, -.17, 1.725),
               (.44, -.19, 1.67), (.29, -.105, 1.645)]
    center = Vector((.345, -.045, 1.785))
    verts = []
    for factor, dy in ((1, 0), (.63, -.018), (.63, .047), (1, .048)):
        for p in outline:
            v = center + factor*(Vector(p)-center)
            verts.append((side*v.x, v.y+dy, v.z))
    verts += [(side*center.x, center.y-.025, center.z),
              (side*center.x, center.y+.055, center.z)]
    faces, mids = [], []
    for ring in (0, 2):
        for j in range(8):
            faces.append((ring*8+j, ring*8+(j+1)%8, (ring+1)*8+(j+1)%8, (ring+1)*8+j))
            mids.append(0)
    for j in range(8):
        faces.append((j, 24+j, 24+(j+1)%8, (j+1)%8)); mids.append(0)
        faces.append((32, 8+j, 8+(j+1)%8)); mids.append(1)
        faces.append((33, 16+(j+1)%8, 16+j)); mids.append(0)
    o = mesh('Ear_L' if side == 1 else 'Ear_R', verts, faces, PINK)
    o.data.materials.append(EAR)
    for p, idx in zip(o.data.polygons, mids):
        p.material_index = idx
    # Recalculate because mirroring reverses winding.
    activate(o)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    sub = o.modifiers.new('Rounded_ears', 'SUBSURF')
    sub.levels = 2
    bpy.ops.object.modifier_apply(modifier=sub.name)
    return o


def uv_all(objects):
    for o in objects:
        if o.type != 'MESH':
            continue
        activate(o)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.025)
        bpy.ops.object.mode_set(mode='OBJECT')


def rig_create():
    data = bpy.data.armatures.new('Pig_Skeleton')
    rig = bpy.data.objects.new('Pig_Rig', data)
    bpy.context.collection.objects.link(rig)
    activate(rig)
    bpy.ops.object.mode_set(mode='EDIT')
    specs = [
        ('Root', (0,0,0), (0,0,.25), None),
        ('Hips', (0,0,.48), (0,0,.78), 'Root'),
        ('Spine', (0,0,.78), (0,0,1.13), 'Hips'),
        ('Head', (0,0,1.13), (0,0,1.73), 'Spine')]
    for side, sign in (('L',1),('R',-1)):
        specs += [
            ('UpperArm_'+side, (sign*.32,0,1.04), (sign*.45,0,.81), 'Spine'),
            ('Forearm_'+side, (sign*.45,0,.81), (sign*.535,0,.63), 'UpperArm_'+side),
            ('Hand_'+side, (sign*.535,0,.63), (sign*.55,0,.56), 'Forearm_'+side),
            ('Thigh_'+side, (sign*.177,0,.48), (sign*.177,0,.27), 'Hips'),
            ('Shin_'+side, (sign*.177,0,.27), (sign*.177,0,.10), 'Thigh_'+side),
            ('Foot_'+side, (sign*.177,0,.10), (sign*.177,-.13,.055), 'Shin_'+side)]
    for name, head, tail, parent in specs:
        b = data.edit_bones.new(name)
        b.head, b.tail = head, tail
        if parent:
            b.parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.show_in_front = True
    rig.data.display_type = 'STICK'
    rig['rig_type'] = 'Generic capsule pig; Blender Z-up, front -Y'
    return rig


def skin(o, rig, bone='Head'):
    o.parent = rig
    vg = o.vertex_groups.new(name=bone)
    vg.add(list(range(len(o.data.vertices))), 1, 'REPLACE')
    m = o.modifiers.new('Pig_Skinning', 'ARMATURE')
    m.object = rig


def body_weights(o, rig):
    o.parent = rig
    groups = {b.name:o.vertex_groups.new(name=b.name) for b in rig.data.bones}
    def clamp(v): return max(0, min(1, v))
    def blend(a,b,t): return {a:1-t,b:t}
    for v in o.data.vertices:
        x,y,z = v.co
        side = 'L' if x >= 0 else 'R'
        ax = abs(x)
        if z < .50 and ax > .07:
            if z < .27:
                weights = blend('Shin_'+side, 'Thigh_'+side, clamp((z-.20)/.13))
            else:
                weights = blend('Thigh_'+side, 'Hips', clamp((z-.36)/.17))
        elif ax > .32 and .53 < z < 1.11:
            shoulder = clamp((ax-.31)/.09)
            elbow = clamp((.89-z)/.17)
            weights = {'Spine':1-shoulder, 'UpperArm_'+side:shoulder*(1-elbow),
                       'Forearm_'+side:shoulder*elbow}
        elif z > 1.06:
            weights = blend('Spine', 'Head', clamp((z-1.06)/.26))
        else:
            weights = blend('Hips', 'Spine', clamp((z-.62)/.26))
        for name,w in weights.items():
            if w > 0:
                groups[name].add([v.index], w, 'REPLACE')
    m = o.modifiers.new('Pig_Skinning', 'ARMATURE')
    m.object = rig


def empty(name, parent):
    o = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(o)
    o.parent = parent
    o.empty_display_size = .06
    o.empty_display_type = 'PLAIN_AXES'
    return o


def slot(name, parts, rig):
    root = empty(name, rig)
    root['appearance_slot'] = name.split('__')[0]
    root['option_id'] = name.split('__')[1]
    for part in parts:
        skin(part, rig)
        part.parent = root
    return root


rig = rig_create()
body = capsule()
body_weights(body, rig)
base = [body]
for s, side in ((1,'L'),(-1,'R')):
    e = ear(s); skin(e, rig); base.append(e)
    hand = sphere('Hoof_Hand_'+side, (s*.535,-.006,.619), (.086,.095,.092), HOOF)
    skin(hand, rig, 'Hand_'+side); base.append(hand)
    foot = sphere('Hoof_Foot_'+side, (s*.177,-.035,.062), (.108,.151,.076), HOOF)
    for v in foot.data.vertices:
        v.co.z=max(v.co.z,-.060)
    skin(foot, rig, 'Foot_'+side); base.append(foot)

snout = sphere('Snout', (0,-.332,1.295), (.147,.111,.100), NOSE, 32, 20)
for s in (-1,1):
    cutter = sphere('NostrilCutter', (s*.053,-.430,1.304), (.023,.036,.036), None, 20,12)
    activate(snout)
    mod = snout.modifiers.new('Nostril', 'BOOLEAN')
    mod.operation = 'DIFFERENCE'; mod.object = cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    nostril = sphere('Nostril_L' if s==1 else 'Nostril_R', (s*.053,-.410,1.304), (.017,.015,.028), HOOF,16,12)
    skin(nostril,rig); base.append(nostril)
skin(snout,rig); base.append(snout)

options = {}
options['Eyes__Open'] = slot('Eyes__Open', [sphere('Eye_Open_'+str(s), (s*.151,-.288,1.471), (.036,.023,.046), EYE) for s in (-1,1)], rig)
# A half-round eye mesh with a flat upper edge, rather than obscuring eyes with overlays.
squints=[]
for s in (-1,1):
    coords=[]
    for i in range(17):
        a=math.pi+i*math.pi/16
        coords.append((s*.151+.037*math.cos(a),-.305,1.482+.050*math.sin(a)))
    front = coords
    back = [(x,y+.024,z) for x,y,z in coords]
    n=len(front)
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    squints.append(mesh('Eye_Squint_'+str(s),front+back,faces,EYE))
options['Eyes__Squint']=slot('Eyes__Squint',squints,rig)
for style in ('Neutral','Raised','Confident'):
    parts=[]
    for s in (-1,1):
        coords=[]
        for i in range(9):
            t=i/8
            z=1.573+.018*math.sin(t*math.pi)
            if style=='Raised' and s==-1: z+=.026-.018*t
            if style=='Confident': z+=(t-.5)*s*.031
            x=s*.151+(t-.5)*.097
            y=-.266 + .012*(abs(x)/.20)
            coords.append((x,y,z))
        parts.append(tube('Brow_'+style+'_'+str(s),coords,.012,HAIR,
                          [.65,.9,1,1,1,1,1,.9,.65]))
    options['Brows__'+style]=slot('Brows__'+style,parts,rig)

glasses=[]
for s in (-1,1):
    coords=[]
    for i in range(48):
        a=i*2*math.pi/48
        coords.append((s*.151+.101*math.cos(a),-.344,1.468+.107*math.sin(a)))
    glasses.append(tube('Glasses_Rim_'+str(s),coords,.014,FRAME,closed=True))
    glasses.append(tube('Glasses_Temple_'+str(s),[(s*.25,-.344,1.487),(s*.32,-.28,1.489),
       (s*.367,-.16,1.49),(s*.372,-.04,1.475),(s*.371,.01,1.445)],.012,FRAME))
glasses.append(tube('Glasses_Bridge',[(-.050,-.344,1.48),(-.023,-.352,1.493),
                                   (0,-.355,1.497),(.023,-.352,1.493),(.05,-.344,1.48)],.012,FRAME))
options['Glasses__Round']=slot('Glasses__Round',[join(glasses,'Glasses_Round')],rig)

# Separate closed frames and opaque tinted lenses, all following the Head bone.
def eyewear(style, outline, sun=False):
    parts=[]
    for s in (-1,1):
        points=[(s*(.151+x),-.359,1.468+z) for x,z in outline]
        parts.append(tube(style+'_Rim_'+str(s),points,.013,FRAME,closed=True))
        parts.append(tube(style+'_Temple_'+str(s),[(s*.251,-.359,1.485),
            (s*.33,-.24,1.49),(s*.372,-.04,1.475),(s*.371,.01,1.445)],.012,FRAME))
        if sun:
            # A closed shallow extrusion avoids backface loss in mirrors.
            n=len(points)
            front=[(x,-.360,z) for x,y,z in points]
            back=[(x,-.346,z) for x,y,z in points]
            faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
            faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
            lens=mesh(style+'_Lens_'+str(s),front+back,faces,LENS)
            activate(lens)
            bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
            parts.append(lens)
    parts.append(tube(style+'_Bridge',[(-.053,-.359,1.48),(0,-.365,1.492),(.053,-.359,1.48)],.012,FRAME))
    return slot('Glasses__'+style,[join(parts,'Glasses_'+style)],rig)

square=[(-.098,-.045),(-.085,-.074),(.073,-.074),(.098,-.047),
        (.098,.061),(.075,.084),(-.074,.084),(-.098,.061)]
cat_eye=[(-.090,-.040),(-.065,-.072),(.041,-.075),(.084,-.035),
         (.127,.095),(.026,.069),(-.079,.070),(-.099,.043)]
aviator=[(.10*math.cos(i*2*math.pi/48),
          .087*math.sin(i*2*math.pi/48)-.03*max(0,-math.sin(i*2*math.pi/48))) for i in range(48)]
options['Glasses__Square']=eyewear('Square',square)
options['Glasses__CatEye']=eyewear('CatEye',cat_eye)
options['Glasses__Aviator']=eyewear('Aviator',aviator)
options['Glasses__SunRound']=eyewear('SunRound',[(.101*math.cos(i*2*math.pi/48),.107*math.sin(i*2*math.pi/48)) for i in range(48)],True)
options['Glasses__SunSquare']=eyewear('SunSquare',square,True)

# A row of tapered spikes sits along the crown, clear of both ears.
spikes=[]
for i in range(7):
    y=-.18+i*.058
    z=1.70-.24*(y/.35)**2
    height=.19+.09*math.sin(i*math.pi/6)
    bpy.ops.mesh.primitive_cone_add(vertices=10,radius1=.059,radius2=.005,
                                  depth=height,location=(0,y,z+height/2))
    spike=finish(bpy.context.object,'Mohawk_Spike_'+str(i),HAIR)
    spike.scale=(.72,1,1)
    activate(spike); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    spikes.append(spike)
options['Hair__Mohawk']=slot('Hair__Mohawk',[join(spikes,'Hair_Mohawk')],rig)

septum=tube('Piercing_Septum',[(.045*math.cos(i*2*math.pi/40),-.433,
                             1.244+.048*math.sin(i*2*math.pi/40)) for i in range(40)],
            .008,SILVER,closed=True,sides=10)
options['NosePiercing__Septum']=slot('NosePiercing__Septum',[septum],rig)

curled=[]
for s in (-1,1):
    coords=[(s*x,y,z) for x,y,z in [(0,-.334,1.192),(.035,-.350,1.175),(.075,-.355,1.153),
              (.12,-.345,1.151),(.166,-.330,1.161),(.198,-.312,1.184),(.198,-.310,1.210),(.178,-.316,1.214)]]
    curled.append(tube('CurledMustache_'+str(s),coords,.039,HAIR,
                       [.75,1,1,.86,.65,.38,.19,.07],sides=12))
options['Mustache__Curled']=slot('Mustache__Curled',[join(curled,'Mustache_Curled')],rig)
classic=[]
for s in (-1,1):
    o=sphere('ClassicMustache_'+str(s),(s*.081,-.334,1.172),(.097,.042,.039),HAIR)
    o.rotation_euler[1]=s*math.radians(-15)
    classic.append(o)
options['Mustache__Classic']=slot('Mustache__Classic',[join(classic,'Mustache_Classic')],rig)

# A single closed, shallow U-shaped beard mesh; separate from the mustache.
verts=[]
steps=32
for layer in (0,1):
    for edge in (0,1):
        for i in range(steps+1):
            a=i*math.pi/steps
            rx,rz=(.252,.25) if edge==0 else (.214,.133)
            x=rx*math.cos(a); z=1.225-rz*math.sin(a)
            front=-.295*math.sqrt(max(.1,1-(x/.395)**2))-.032
            verts.append((x,front+layer*.038,z))
N=steps+1
faces=[]
for i in range(steps):
    faces += [(i,i+1,N+i+1,N+i),(2*N+i,3*N+i,3*N+i+1,2*N+i+1),
              (i,2*N+i,2*N+i+1,i+1),(N+i,N+i+1,3*N+i+1,3*N+i)]
for i in (0,steps): faces.append((i,N+i,3*N+i,2*N+i))
beard=mesh('Beard_Short',verts,faces,HAIR)
activate(beard)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
bev=beard.modifiers.new('Soft_beard_edge','BEVEL'); bev.width=.009; bev.segments=2
bpy.ops.object.modifier_apply(modifier=bev.name)
options['Beard__Short']=slot('Beard__Short',[beard],rig)

earring=tube('Piercing_EarHoop',[(.509+.047*math.cos(i*2*math.pi/32),-.189,
                               1.717+.050*math.sin(i*2*math.pi/32)) for i in range(32)],
             .008,SILVER,closed=True,sides=8)
options['EarPiercing__Hoop']=slot('EarPiercing__Hoop',[earring],rig)
bar=tube('Piercing_BrowBar',[(.174,-.290,1.541),(.197,-.28,1.606)],.0065,SILVER)
balls=[sphere('Piercing_BrowBall_'+str(i),p,(.011,.011,.011),SILVER,12,8)
       for i,p in enumerate([(.174,-.29,1.541),(.197,-.28,1.606)])]
options['BrowPiercing__Bar']=slot('BrowPiercing__Bar',[join([bar]+balls,'Piercing_BrowBar')],rig)

canonical=list(bpy.context.scene.objects)
uv_all([o for o in canonical if o.type=='MESH'])
for o in canonical:
    if o.type=='MESH':
        o['modular_pig']=True

PRESETS={
 '01_GlassesMustache':['Eyes__Open','Brows__Raised','Glasses__Round','Mustache__Curled'],
 '02_Beard':['Eyes__Open','Brows__Neutral','Mustache__Classic','Beard__Short'],
 '03_Piercings':['Eyes__Squint','Brows__Confident','EarPiercing__Hoop','BrowPiercing__Bar']}
DEFAULT=['Eyes__Open','Brows__Neutral']


def export_fbx(objects, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.hide_set(False); o.hide_viewport=False; o.select_set(True)
    bpy.context.view_layer.objects.active=next(o for o in objects if o.type=='ARMATURE')
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,
        object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,
        bake_anim=False,axis_forward='-Z',axis_up='Y',use_custom_props=True,
        use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,
        apply_scale_options='FBX_SCALE_UNITS')


export_fbx(canonical,EXPORT/'Pig_Modular.fbx')
for name,enabled in PRESETS.items():
    chosen=[o for o in canonical if o==rig or o in base or
            o.name in enabled or (o.parent and o.parent.name in enabled)]
    export_fbx(chosen,EXPORT/('Pig_'+name+'.fbx'))


def clone_to_scene(scene, prefix, offset, enabled):
    keep=[o for o in canonical if o==rig or o in base or
          o.name in enabled or (o.parent and o.parent.name in enabled)]
    mapping={}
    collection=bpy.data.collections.new(prefix)
    scene.collection.children.link(collection)
    for o in keep:
        c=o.copy()
        c.hide_render=False
        c.hide_viewport=False
        # Mesh data is shared across presets; rigs are independent.
        if o.type=='ARMATURE': c.data=o.data.copy()
        c.name=prefix+' | '+o.name
        collection.objects.link(c); mapping[o]=c
    for o,c in mapping.items():
        c.parent=mapping.get(o.parent)
        for m in c.modifiers:
            if m.type=='ARMATURE': m.object=mapping[rig]
    mapping[rig].location.x=offset
    return mapping


master=bpy.context.scene
master.name='02_Modular_Master'
for name,root in options.items():
    enabled=name in DEFAULT
    root['default_enabled']=enabled
    for o in [root,*root.children]:
        o.hide_render=not enabled
        o.hide_set(not enabled)
master['usage']='One body + selectable Eyes/Brows/Glasses/Mustache/Beard/Hair/EarPiercing/BrowPiercing/NosePiercing. Unity adds skin, hair and pattern palettes.'
gallery=bpy.data.scenes.new('01_Three_Pigs')
for (name,enabled),x in zip(PRESETS.items(),(-1.23,0,1.23)):
    clone_to_scene(gallery,name,x,enabled)
bpy.context.window.scene=gallery


def studio(scene, gallery_mode):
    bpy.context.window.scene=scene
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    scene.render.engine='CYCLES'
    scene.cycles.samples=32
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1680 if gallery_mode else 900
    scene.render.resolution_y=1040 if gallery_mode else 1000
    scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new(scene.name+'_World')
    scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.56,.58,.63,1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45
    scene.view_settings.view_transform='AgX'
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.004))
    finish(bpy.context.object,'Studio_Floor',FLOOR)
    def area(name,loc,power,size):
        data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
        o=bpy.data.objects.new(name,data); scene.collection.objects.link(o); o.location=loc
        o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
    area('Studio_Key',(-3,-4,6),550,5)
    area('Studio_Fill',(4,-2,3),280,4)
    area('Studio_Rim',(1,3,5),600,3)
    camdata=bpy.data.cameras.new(scene.name+'_Camera')
    cam=bpy.data.objects.new('Studio_Camera',camdata); scene.collection.objects.link(cam)
    cam.location=(.32,-8,3.1) if gallery_mode else (.14,-6,2.65)
    target=Vector((0,0,1.00))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    camdata.type='ORTHO'; camdata.ortho_scale=4.85 if gallery_mode else 2.45
    scene.camera=cam
    if gallery_mode:
        for x,label in [(-1.23,'01  GLASSES + MUSTACHE'),(0,'02  BEARD'),(1.23,'03  PIERCINGS')]:
            font=bpy.data.curves.new(label,'FONT'); font.body=label; font.align_x='CENTER'; font.size=.073
            obj=bpy.data.objects.new(label,font); scene.collection.objects.link(obj)
            obj.location=(x,-.51,-.002); obj.data.materials.append(TEXT)
    for screen in bpy.data.screens:
        for a in screen.areas:
            if a.type=='VIEW_3D':
                space=a.spaces.active
                space.shading.type='MATERIAL'
                space.overlay.show_extras=False
                space.overlay.show_floor=False
                space.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
                space.region_3d.view_distance=5 if gallery_mode else 3
                space.region_3d.view_location=(0,0,1)
                space.region_3d.view_perspective='ORTHO'
    return cam


studio(master,False)
studio(gallery,True)
bpy.context.window.scene=gallery
bpy.ops.object.select_all(action='DESELECT')
guide=bpy.data.texts.new('READ_ME_Modular_Pig.txt')
guide.write('''MODULAR CAPSULE PIG / v2

Scenes: 01_Three_Pigs = the three requested appearances.
        02_Modular_Master = one base with all independent options.

Master options (Outliner):
Eyes__Open / Eyes__Squint
Brows__Neutral / Brows__Raised / Brows__Confident
Glasses__Round
Glasses__Square / Glasses__CatEye / Glasses__Aviator
Glasses__SunRound / Glasses__SunSquare
Hair__Mohawk / NosePiercing__Septum
Mustache__Curled / Mustache__Classic
Beard__Short
EarPiercing__Hoop
BrowPiercing__Bar

Options are separate objects, not baked into Body. In the master,
alternate options start hidden. Toggle a root and its child meshes.
Use one Eyes option and one Brows option at a time. Both mustaches
are independent of the beard and glasses. Piercings are independent.
All parts follow the Head bone through normalized vertex groups.

Scale: metres. Pig height approx. 2 m including ears. Front: -Y.
16-bone Generic rig in neutral A-pose; no baked animations.
Flat palette materials, UVs, separate objects, FBX with no leaf bones.
FBX copies live in Assets/Art/Characters/Pigs.
Pig_Modular.fbx includes every option; enable the desired slot roots.
Three preset FBXs contain only the selected appearance.

This is the first modeled version, not a claim of finished game
animation integration. Rig deformation should be checked with your
actual gameplay clips. Existing player prefabs and scenes untouched.
''')

manifest={
 'version':2,'units':'metres','height_m':round(max(v.co.z for v in base[1].data.vertices),3),
 'front_blender':'-Y','rig':'Generic','bones':[b.name for b in rig.data.bones],
 'base_objects':[o.name for o in base],
 'slots':{name:[o.name for o in root.children] for name,root in options.items()},
 'default':DEFAULT,'presets':PRESETS,
 'mesh_stats':{o.name:{'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons)}
               for o in canonical if o.type=='MESH'}}
(SOURCE/'appearance_manifest.json').write_text(json.dumps(manifest,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Svinki_Modular_Pigs.blend'))
gallery.render.filepath=str(SOURCE/'three_pigs_preview.png')
if '--skip-render' not in sys.argv:
    bpy.ops.render.render(write_still=True)
print('PIG_DELIVERABLES_READY',str(SOURCE))
