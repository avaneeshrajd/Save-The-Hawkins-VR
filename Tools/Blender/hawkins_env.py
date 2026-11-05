"""Generates Hawkins street assets for Vecna's Curse.

Usage:
  Blender --background --factory-startup --python Tools/Blender/hawkins_env.py -- <project root> [preview dir]

Output in Assets/VecnaCurse:
  Environments/Hawkins/AmericanHouse.fbx  reduced house, LOD0 and LOD1, base centred at origin
  Environments/Hawkins/StreetLight.fbx    original procedural streetlight
  Environments/Hawkins/DeadTrees.fbx      original procedural dead trees
  Textures/VC_Sky_HawkinsNight.png        original procedural night sky (equirectangular)
"""
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index('--') + 1:]
root = args[0]
preview = args[1] if len(args) > 1 else None
HOUSE_SOURCE = os.path.join(root, 'Assets/Save The Hawkins/Hawkins City Assets/american-house/source/american_house_MARK_2.fbx')
HOUSE_TEXTURES = os.path.join(root, 'Assets/Save The Hawkins/Hawkins City Assets/american-house/textures')
OUT = os.path.join(root, 'Assets/VecnaCurse/Environments/Hawkins')
HOUSE_LOD0_TRIANGLES = 7000
HOUSE_LOD1_TRIANGLES = 1500


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export(path):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH'},
        apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
        axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
        use_mesh_modifiers=False, bake_anim=False, path_mode='STRIP', embed_textures=False)


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def build_house():
    clear()
    bpy.ops.import_scene.fbx(filepath=HOUSE_SOURCE)
    house = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    for o in [o for o in bpy.context.scene.objects if o is not house]:
        bpy.data.objects.remove(o)
    bpy.context.view_layer.objects.active = house
    house.select_set(True)
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    print('house source', tri_count(house), 'size', [round(v, 2) for v in house.dimensions],
          'materials', [s.material.name for s in house.material_slots])

    # Collapse repeated material slots so each material is one draw call.
    first_slot = {}
    remap = []
    for i, slot in enumerate(house.material_slots):
        remap.append(first_slot.setdefault(slot.material.name, i))
    bm = bmesh.new()
    bm.from_mesh(house.data)
    # Decal faces need alpha cutout, which costs overdraw on Quest.
    decal = first_slot.get('Decal')
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if remap[f.material_index] == decal], context='FACES')
    for face in bm.faces:
        face.material_index = remap[face.material_index]
    bm.to_mesh(house.data)
    bm.free()
    bpy.context.view_layer.objects.active = house
    bpy.ops.object.material_slot_remove_unused()

    pts = [Vector(v.co) for v in house.data.vertices]
    low = Vector(min(p[i] for p in pts) for i in range(3))
    high = Vector(max(p[i] for p in pts) for i in range(3))
    house.data.transform(Matrix.Translation(-Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))))
    house.data.update()
    print('house after decal removal', tri_count(house), 'size', [round(v, 2) for v in high - low],
          'materials', [slot.material.name for slot in house.material_slots])

    source = tri_count(house)
    for name, target in (('AmericanHouse_LOD1', HOUSE_LOD1_TRIANGLES), ('AmericanHouse_LOD0', HOUSE_LOD0_TRIANGLES)):
        lod = house.copy()
        lod.data = house.data.copy()
        lod.name = name
        lod.data.name = name
        bpy.context.scene.collection.objects.link(lod)
        mod = lod.modifiers.new('reduce', 'DECIMATE')
        mod.ratio = min(1.0, target / source)
        bpy.context.view_layer.objects.active = lod
        bpy.ops.object.modifier_apply(modifier=mod.name)
        print(name, tri_count(lod))
    bpy.data.objects.remove(house)

    export(os.path.join(OUT, 'AmericanHouse.fbx'))
    if preview:
        render_preview(bpy.data.objects['AmericanHouse_LOD0'], 'house', HOUSE_TEXTURES)


def render_preview(target, name, textures=None):
    if textures:
        bpy.ops.file.find_missing_files(directory=textures)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE' if textures else 'MATERIAL'
    scene.render.resolution_x, scene.render.resolution_y = 700, 500
    scene.render.image_settings.file_format = 'PNG'
    for o in scene.objects:
        o.hide_render = o.type == 'MESH' and target is not None and o is not target
    cam = bpy.data.objects.new('preview', bpy.data.cameras.new('preview'))
    scene.collection.objects.link(cam)
    scene.camera = cam
    size = max(target.dimensions)
    centre = Vector((0, 0, target.dimensions.z / 2))
    for label, direction in (('a', (1, -1, 0.3)), ('b', (-1, 1, 0.3))):
        cam.location = centre + Vector(direction).normalized() * size * 1.7
        cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(preview, f'{name}_{label}.png')
        bpy.ops.render.render(write_still=True)


def material(name, colour):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*colour, 1)
    return mat


def add_tube(bm, start, end, radius_start, radius_end, sides, slot=0):
    """Adds an open-ended tapered prism between two points."""
    axis = (end - start).normalized()
    rotation = axis.to_track_quat('Z', 'Y')
    rings = []
    for centre, radius in ((start, radius_start), (end, radius_end)):
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            ring.append(bm.verts.new(centre + rotation @ Vector((math.cos(a) * radius, math.sin(a) * radius, 0))))
        rings.append(ring)
    for i in range(sides):
        j = (i + 1) % sides
        face = bm.faces.new((rings[0][i], rings[0][j], rings[1][j], rings[1][i]))
        face.material_index = slot
        face.smooth = True
    return rings[1]


def new_mesh(name, materials):
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for m in materials:
        mesh.materials.append(m)
    return obj


def build_street_light():
    clear()
    metal = material('VC_StreetMetal', (0.1, 0.1, 0.11))
    lamp = material('VC_LampGlow', (1.0, 0.75, 0.42))
    obj = new_mesh('StreetLight', [metal, lamp])
    bm = bmesh.new()
    pole_top = Vector((0, 0, 6.0))
    add_tube(bm, Vector((0, 0, 0)), pole_top, 0.09, 0.06, 6)
    arm_end = Vector((0, 1.6, 6.35))
    add_tube(bm, pole_top, arm_end, 0.05, 0.035, 5)
    head = arm_end + Vector((0, 0.3, -0.02))
    add_tube(bm, arm_end, head, 0.12, 0.12, 6)
    bulb_low = head - Vector((0, 0, 0.1))
    # Emissive lens under the head.
    verts = [bm.verts.new(bulb_low + Vector((x, y, 0))) for x, y in ((-0.14, -0.3), (0.14, -0.3), (0.14, 0.05), (-0.14, 0.05))]
    face = bm.faces.new(list(reversed(verts)))
    face.material_index = 1
    bm.to_mesh(obj.data)
    bm.free()
    print('StreetLight', tri_count(obj))
    export(os.path.join(OUT, 'StreetLight.fbx'))


def grow(bm, start, direction, length, radius, depth, rng):
    end = start + direction * length
    add_tube(bm, start, end, radius, radius * 0.68, 5 if depth >= 2 else 3)
    if depth == 0:
        return
    for _ in range(2 if depth < 3 else 3):
        twist = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.1, 0.6)))
        grow(bm, end, (direction + twist * 0.75).normalized(), length * rng.uniform(0.6, 0.82),
             radius * 0.62, depth - 1, rng)


def build_dead_trees():
    clear()
    bark = material('VC_DeadBark', (0.05, 0.04, 0.035))
    for index, seed in enumerate((7, 21, 40)):
        rng = random.Random(seed)
        obj = new_mesh(f'DeadTree_{index}', [bark])
        bm = bmesh.new()
        grow(bm, Vector((0, 0, 0)), Vector((rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), 1)).normalized(),
             rng.uniform(2.6, 3.4), 0.3, 4, rng)
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
        print(obj.name, tri_count(obj), 'height', round(obj.dimensions.z, 1))
    export(os.path.join(OUT, 'DeadTrees.fbx'))
    if preview:
        render_preview(bpy.data.objects['DeadTree_0'], 'tree')


def value_noise(height, width, cells_y, cells_x, rng):
    grid = rng.random((cells_y + 1, cells_x))
    grid = np.concatenate([grid, grid[:, :1]], axis=1)  # Wrap horizontally for a seamless sky.
    y = np.linspace(0, cells_y, height, endpoint=False)
    x = np.linspace(0, cells_x, width, endpoint=False)
    yi, xi = y.astype(int), x.astype(int)
    fy, fx = y - yi, x - xi
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    top = grid[yi][:, xi] * (1 - fx) + grid[yi][:, xi + 1] * fx
    bottom = grid[yi + 1][:, xi] * (1 - fx) + grid[yi + 1][:, xi + 1] * fx
    return top * (1 - fy)[:, None] + bottom * fy[:, None]


def build_sky():
    width, height = 2048, 1024
    rng = np.random.default_rng(1986)
    clouds = np.zeros((height, width))
    amplitude, total = 1.0, 0.0
    for octave in range(5):
        clouds += value_noise(height, width, 4 * 2 ** octave, 8 * 2 ** octave, rng) * amplitude
        total += amplitude
        amplitude *= 0.5
    clouds /= total
    clouds = np.clip((clouds - 0.42) * 3.0, 0, 1)

    # Row 0 is the zenith; the horizon is at the middle row.
    elevation = (0.5 - (np.arange(height) + 0.5) / height) * math.pi
    up = np.clip(np.sin(elevation), 0, 1)[:, None]

    horizon = np.array([0.04, 0.026, 0.045])
    zenith = np.array([0.012, 0.016, 0.035])
    glow = np.array([0.20, 0.035, 0.04])
    sky = horizon + (zenith - horizon) * (up ** 0.45)[:, :, None]
    # Faint red band low in the sky, strongest ahead of the player (+Z, centre of the image).
    azimuth = np.linspace(-math.pi, math.pi, width, endpoint=False)
    ahead = (0.5 + 0.5 * np.cos(azimuth))[None, :]
    band = np.exp(-(up / 0.22) ** 2) * ahead ** 2
    sky = sky + glow[None, None, :] * (band * (0.35 + 0.65 * clouds))[:, :, None]
    # Dark storm clouds.
    sky = sky * (1 - 0.65 * clouds * np.clip(up * 4, 0.3, 1))[:, :, None]

    # Sparse stars, hidden by cloud.
    stars = rng.random((height, width)) > 0.9993
    star_glow = stars * (1 - clouds) * np.clip(up * 5, 0, 1) * rng.uniform(0.25, 0.8, (height, width))
    sky = sky + star_glow[:, :, None]

    # Pale moon behind thin cloud.
    moon_elevation, moon_azimuth = math.radians(38), math.radians(-35)
    lat = elevation[:, None]
    lon = azimuth[None, :]
    cos_angle = np.sin(lat) * math.sin(moon_elevation) + np.cos(lat) * math.cos(moon_elevation) * np.cos(lon - moon_azimuth)
    angle = np.arccos(np.clip(cos_angle, -1, 1))
    disc = np.clip((math.radians(2.2) - angle) / math.radians(0.3), 0, 1)
    halo = np.exp(-(angle / math.radians(9)) ** 2) * 0.13
    moon = np.array([0.72, 0.74, 0.8])
    sky = sky + (disc * (1 - 0.55 * clouds))[:, :, None] * moon + (halo * (1 - 0.4 * clouds))[:, :, None] * moon

    # Below the horizon is the same colour as the fog.
    sky[elevation < 0] = horizon

    srgb = np.clip(sky, 0, 1) ** (1 / 2.2)
    rgba = np.ones((height, width, 4), dtype=np.float32)
    rgba[:, :, :3] = srgb[::-1]  # Blender image rows run bottom to top.
    image = bpy.data.images.new('sky', width, height, alpha=False)
    image.colorspace_settings.name = 'Non-Color'
    image.pixels.foreach_set(rgba.ravel())
    image.filepath_raw = os.path.join(root, 'Assets/VecnaCurse/Textures/VC_Sky_HawkinsNight.png')
    image.file_format = 'PNG'
    image.save()
    print('sky saved')


os.makedirs(OUT, exist_ok=True)
build_house()
build_street_light()
build_dead_trees()
build_sky()
