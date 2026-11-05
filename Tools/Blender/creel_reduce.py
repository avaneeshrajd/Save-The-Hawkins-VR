"""Builds the Quest-ready Creel House exterior from the downloaded FBX.

Usage:
  Blender --background --factory-startup --python Tools/Blender/creel_reduce.py -- <source.fbx> <output.fbx> <textures dir> [preview dir]

Output holds three merged meshes (CreelHouse_LOD0/1/2, which Unity turns into a LODGroup)
and an empty, CreelHouse_DoorMarker, at the base of the front door.
"""
import os
import sys

import bpy
from mathutils import Matrix, Vector

source, output, textures = sys.argv[sys.argv.index('--') + 1:][:3]
extra = sys.argv[sys.argv.index('--') + 4:]
preview = extra[0] if extra else None

KEEP_LOD2 = ('Main', 'Main.001', 'Roof', 'Window', 'Cube.064')
SMALL_OBJECT_METERS = 1.2
DOOR_OBJECT = 'Cube.064'
# Metal_fence needs alpha cutout, which costs overdraw on Quest; the roof fence is too small to justify it.
DROPPED_MATERIALS = {'Dots Stroke', 'Metal_fence'}
SOLID_WINDOW_MATERIAL = 'Glass'


def triangles(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def world_bounds(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    low = Vector(min(p[i] for p in pts) for i in range(3))
    high = Vector(max(p[i] for p in pts) for i in range(3))
    return low, high


def select_only(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def lod0_ratio(tris):
    if tris > 6000:
        return 0.13
    if tris > 1500:
        return 0.25
    if tris > 400:
        return 0.5
    return 1.0


def decimate(obj, ratio):
    if ratio >= 1.0:
        return
    mod = obj.modifiers.new('reduce', 'DECIMATE')
    mod.ratio = ratio
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)


def build_lod(sources, name, chooser):
    copies = []
    for src in sources:
        ratio = chooser(src)
        if ratio is None:
            continue
        c = src.copy()
        c.data = src.data.copy()
        bpy.context.scene.collection.objects.link(c)
        decimate(c, ratio)
        copies.append(c)
    select_only(copies)
    bpy.ops.object.join()
    merged = bpy.context.view_layer.objects.active
    merged.name = name
    merged.data.name = name
    print(f'{name}: {triangles(merged)} triangles, {len(merged.material_slots)} materials')
    return merged


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=source)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in [o for o in bpy.context.scene.objects if o.type != 'MESH']:
    bpy.data.objects.remove(o)

select_only(meshes)
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.make_single_user(object=True, obdata=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

for o in list(meshes):
    if any(s.material and s.material.name in DROPPED_MATERIALS for s in o.material_slots):
        print('dropped', o.name)
        meshes.remove(o)
        bpy.data.objects.remove(o)

low = Vector((min(world_bounds(o)[0][i] for o in meshes) for i in range(3)))
high = Vector((max(world_bounds(o)[1][i] for o in meshes) for i in range(3)))
print('source triangles', sum(triangles(o) for o in meshes), 'objects', len(meshes),
      'size', [round(v, 2) for v in high - low])

# Centre the footprint and put the lowest point on the ground.
offset = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
for o in meshes:
    o.data.transform(Matrix.Translation(-offset))
    o.data.update()
bpy.context.view_layer.update()

door = next(o for o in meshes if o.name == DOOR_OBJECT)
door_low, door_high = world_bounds(door)


def diagonal(o):
    a, b = world_bounds(o)
    return (b - a).length


def is_main(o):
    # Glass objects stay so distant windows do not open onto the hollow shell.
    has_glass = any(s.material and s.material.name == SOLID_WINDOW_MATERIAL for s in o.material_slots)
    return has_glass or o.name.startswith(KEEP_LOD2)


lod0 = build_lod(meshes, 'CreelHouse_LOD0', lambda o: 1.0 if o.name.startswith('Main') else lod0_ratio(triangles(o)))
lod1 = build_lod(meshes, 'CreelHouse_LOD1',
                 lambda o: None if diagonal(o) < SMALL_OBJECT_METERS
                 else (1.0 if o.name.startswith('Main') else lod0_ratio(triangles(o)) * 0.3))
lod2 = build_lod(meshes, 'CreelHouse_LOD2',
                 lambda o: (0.5 if o.name.startswith('Main') else 0.2) if is_main(o) else None)

marker = bpy.data.objects.new('CreelHouse_DoorMarker', None)
bpy.context.scene.collection.objects.link(marker)
marker.location = ((door_low.x + door_high.x) / 2, (door_low.y + door_high.y) / 2, door_low.z)
print('door marker', [round(v, 2) for v in marker.location], 'door size', [round(v, 2) for v in door_high - door_low])

for o in meshes:
    bpy.data.objects.remove(o)

for mat in bpy.data.materials:
    if mat.users == 0:
        continue
    print('material', mat.name)

os.makedirs(os.path.dirname(output), exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=output, use_selection=True, object_types={'MESH', 'EMPTY'},
    apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
    use_mesh_modifiers=False, bake_anim=False, path_mode='STRIP', embed_textures=False,
    use_custom_props=False)

if preview:
    bpy.ops.file.find_missing_files(directory=textures)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.render.resolution_x, scene.render.resolution_y = 800, 600
    scene.render.image_settings.file_format = 'PNG'
    cam_data = bpy.data.cameras.new('preview')
    cam = bpy.data.objects.new('preview', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    size = high - low
    centre = Vector((0, 0, size.z / 2))
    cam.location = centre + Vector((size.length * 1.05, -size.length * 0.45, size.length * 0.12))
    cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
    for shown in (lod0, lod1, lod2):
        for o in (lod0, lod1, lod2):
            o.hide_render = o is not shown
        scene.render.filepath = os.path.join(preview, shown.name + '.png')
        bpy.ops.render.render(write_still=True)
