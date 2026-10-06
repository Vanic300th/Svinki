"""Run from Blender's Scripting workspace to preview any face combination.

Edit ENABLED below, then Run Script. Works on the modular master scene.
No installation, server, networking or add-on needed.
"""
import bpy

ENABLED = {'Eyes__Open', 'Brows__Raised', 'Glasses__Round', 'Mustache__Curled'}
SCENE = bpy.data.scenes.get('02_Modular_Master')
if SCENE is None:
    raise RuntimeError('Open Svinki_Modular_Pigs.blend first.')
bpy.context.window.scene = SCENE
for root in SCENE.objects:
    if root.type == 'EMPTY' and 'appearance_slot' in root:
        show = root.name in ENABLED
        for obj in [root, *root.children]:
            obj.hide_set(not show)
            obj.hide_render = not show
print('Pig appearance:', ', '.join(sorted(ENABLED)))
