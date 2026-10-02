"""Render the four clothing FBX assets as square pickup textures.

Run with: blender -b --factory-startup --python Tools/render_clothing_icons.py
"""

from pathlib import Path
from math import pi

import bpy
from mathutils import Quaternion, Vector


ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets" / "Сlothes"  # Cyrillic capital С in the source folder.
OUTPUT = ROOT / "Assets" / "Textures" / "Clothing"
OUTPUT.mkdir(parents=True, exist_ok=True)

ITEMS = (
    ("Hat1.fbx", "Hat.png", (0.20, 0.27, 0.37, 1), (2.4, -3.5, 2.4), "Z", 0.76),
    ("Shirt1 (3).fbx", "Shirt.png", (0.84, 0.72, 0.52, 1), (0, 0, -4), "Y", 0.75),
    ("pants1.fbx", "Jeans.png", (0.23, 0.36, 0.58, 1), (0, 0, -4), "Y", 0.82),
    ("sneakers2.fbx", "Shoes.png", (0.88, 0.92, 0.96, 1), (2.6, -3.5, 2.2), "Z", 0.72),
)


def bounds(meshes):
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def make_material(color):
    material = bpy.data.materials.new("Clothing studio fabric")
    material.diffuse_color = color
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = 0.76
    return material


for filename, output, color, camera_direction, up_axis, frame_ratio in ITEMS:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(SOURCE / filename))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("No mesh in " + filename)

    fabric = make_material(color)
    for obj in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(fabric)

    minimum, maximum = bounds(meshes)
    center = (minimum + maximum) / 2
    # Keep the source orientation while placing its center at the studio origin.
    for obj in bpy.context.scene.objects:
        if obj.parent is None:
            obj.location -= center
    bpy.context.view_layer.update()

    camera_data = bpy.data.cameras.new("Icon camera")
    camera = bpy.data.objects.new("Icon camera", camera_data)
    bpy.context.collection.objects.link(camera)
    direction = Vector(camera_direction).normalized()
    camera.location = direction * 4
    rotation = (-direction).to_track_quat("-Z", up_axis)
    if output in ("Shirt.png", "Jeans.png"):
        rotation = rotation @ Quaternion((0, 0, 1), pi)
    camera.rotation_euler = rotation.to_euler()
    camera_data.type = "ORTHO"
    bpy.context.scene.camera = camera

    # Fit the projected mesh in the square image with consistent padding.
    right = camera.rotation_euler.to_matrix() @ Vector((1, 0, 0))
    up = camera.rotation_euler.to_matrix() @ Vector((0, 1, 0))
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    width = max(point.dot(right) for point in points) - min(point.dot(right) for point in points)
    height = max(point.dot(up) for point in points) - min(point.dot(up) for point in points)
    camera_data.ortho_scale = max(width, height) / frame_ratio

    if output == "Jeans.png":
        # This source FBX has overlapping triangles around the crotch. Cover
        # their flicker in the pickup icon without changing the actual model.
        offset = direction * 0.08
        corners = [(-0.195, 0.20), (0.195, 0.20), (0.195, 0.465), (-0.195, 0.465)]
        vertices = [offset + right * x + up * y for x, y in corners]
        patch_mesh = bpy.data.meshes.new("Jeans icon front")
        patch_mesh.from_pydata(vertices, [], [(0, 1, 2, 3)])
        patch_mesh.materials.append(fabric)
        patch = bpy.data.objects.new("Jeans icon front", patch_mesh)
        bpy.context.collection.objects.link(patch)

    for index, location in enumerate(((3, -4, 5), (-3, 2, 4))):
        data = bpy.data.lights.new("Softbox", "AREA")
        light = bpy.data.objects.new("Softbox", data)
        bpy.context.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (-light.location).to_track_quat("-Z", "Y").to_euler()
        data.energy = 250 if index == 0 else 110
        data.shape = "DISK"
        data.size = 5

    world = bpy.data.worlds.new("Clothing icon backdrop")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.28, 0.32, 0.38, 1)
    background.inputs["Strength"].default_value = 0.5
    bpy.context.scene.world = world

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.film_transparent = False
    scene.render.filepath = str(OUTPUT / output)
    scene.view_settings.view_transform = "Standard"
    bpy.ops.render.render(write_still=True)
    print("Rendered", scene.render.filepath)
