"""Agent B Act I shared Blender helpers.

Deterministic authored-geometry generation for the UCMAN (Kyrly) Act I
experimental world variant. 1 BU = 1 metre.

Space convention inside the generators:
    Blender x == Godot x
    Blender y == -Godot z
    Blender z == height

The glTF export with export_yup=True maps this 1:1 into Godot space.
No cameras, lights or physics data are ever authored. Materials are
textureless principled surfaces named AB_*; the Godot composer rebinds
them to PainterlyMaterialLibrary by material name.
"""

from __future__ import annotations

import json
import math
import os
import random
import zlib
from typing import Sequence

import bpy
import mathutils

SEED = 20260819


def stable_hash(value: str) -> int:
    return zlib.crc32(value.encode("utf-8")) & 0x7FFFFFFF

# ---------------------------------------------------------------------------
# Material palette. Names starting AB_ are rebound by the C# composer.
# ---------------------------------------------------------------------------
PALETTE: dict[str, tuple[str, float]] = {
    "AB_earth": ("4a4136", 0.94),
    "AB_earth_wet": ("3b352c", 0.90),
    "AB_earth_path": ("5c5040", 0.94),
    "AB_earth_zirat_path": ("39433b", 0.94),
    "AB_road_crown": ("665847", 0.90),
    "AB_road_rut": ("3a332b", 0.86),
    "AB_road_kara": ("453c33", 0.92),
    "AB_water_dark": ("232830", 0.35),
    "AB_grass": ("5a6248", 0.95),
    "AB_grass_dry": ("75714c", 0.95),
    "AB_sedge": ("55603f", 0.95),
    "AB_fern": ("4c5c3c", 0.95),
    "AB_moss": ("5c6647", 0.95),
    "AB_bark": ("5f4f3e", 0.95),
    "AB_bark_dark": ("493c30", 0.95),
    "AB_bark_birch": ("c8c2b2", 0.90),
    "AB_foliage_birch": ("75834e", 0.95),
    "AB_foliage_pine": ("3c4f3c", 0.95),
    "AB_foliage_spruce": ("425043", 0.95),
    "AB_foliage_shrub": ("556440", 0.95),
    "AB_foliage_kara": ("2a3430", 0.96),
    "AB_foliage_kara_deep": ("222b28", 0.97),
    "AB_plaster": ("c3b493", 0.92),
    "AB_plaster_faded": ("a99c82", 0.93),
    "AB_timber": ("6b5a45", 0.94),
    "AB_timber_dark": ("4e4133", 0.94),
    "AB_log_wall": ("7a674e", 0.94),
    "AB_fade_paint": ("8e7f66", 0.92),
    "AB_roof_iron": ("6f7268", 0.85),
    "AB_roof_iron_dark": ("575a52", 0.85),
    "AB_roof_shingle": ("5d5044", 0.93),
    "AB_window_warm": ("d99b3d", 0.6),
    "AB_window_cold": ("5d6a72", 0.5),
    "AB_stone": ("87837a", 0.92),
    "AB_stone_dark": ("63605a", 0.92),
    "AB_sign_blank": ("8a7a5f", 0.93),
}

_material_cache: dict[str, bpy.types.Material] = {}


def mat(name: str) -> bpy.types.Material:
    if name in _material_cache:
        return _material_cache[name]
    if name not in PALETTE:
        raise KeyError(f"unknown palette material {name}")
    hex_color, rough = PALETTE[name]
    r = int(hex_color[0:2], 16) / 255.0
    g = int(hex_color[2:4], 16) / 255.0
    b = int(hex_color[4:6], 16) / 255.0
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = (r, g, b, 1.0)
        principled.inputs["Roughness"].default_value = rough
        if "Specular IOR Level" in principled.inputs:
            principled.inputs["Specular IOR Level"].default_value = 0.25
        if name == "AB_window_warm":
            principled.inputs["Emission Color"].default_value = (r, g, b, 1.0)
            principled.inputs["Emission Strength"].default_value = 3.2
        elif name == "AB_window_cold":
            principled.inputs["Emission Color"].default_value = (r, g, b, 1.0)
            principled.inputs["Emission Strength"].default_value = 0.15
    _material_cache[name] = material
    return material


# ---------------------------------------------------------------------------
# Deterministic noise
# ---------------------------------------------------------------------------
def _hash2(ix: int, iy: int) -> float:
    value = math.sin(ix * 127.1 + iy * 311.7) * 43758.5453123
    return value - math.floor(value)


def value_noise(x: float, y: float) -> float:
    ix = int(math.floor(x))
    iy = int(math.floor(y))
    fx = x - ix
    fy = y - iy
    sx = fx * fx * (3.0 - 2.0 * fx)
    sy = fy * fy * (3.0 - 2.0 * fy)
    corners = (
        _hash2(ix, iy),
        _hash2(ix + 1, iy),
        _hash2(ix, iy + 1),
        _hash2(ix + 1, iy + 1),
    )
    top = corners[0] + (corners[1] - corners[0]) * sx
    bottom = corners[2] + (corners[3] - corners[2]) * sx
    return top + (bottom - top) * sy


def fbm(x: float, y: float, octaves: int = 3) -> float:
    total = 0.0
    weight = 0.5
    frequency = 1.0
    for _ in range(octaves):
        total += value_noise(x * frequency, y * frequency) * weight
        frequency *= 2.0
        weight *= 0.5
    return total  # ~ [0, 1)


# ---------------------------------------------------------------------------
# Godot <-> Blender coordinate helpers
# ---------------------------------------------------------------------------
def P(x_godot: float, z_godot: float) -> tuple[float, float]:
    """Godot x/z -> Blender x/y for planar positions."""
    return (x_godot, -z_godot)


def place_godot(obj: bpy.types.Object, x_godot: float, z_godot: float,
                height: float = 0.0) -> None:
    obj.location = (x_godot, -z_godot, height)


# ---------------------------------------------------------------------------
# Mesh builders
# ---------------------------------------------------------------------------
def _link(obj: bpy.types.Object) -> bpy.types.Object:
    bpy.context.scene.collection.objects.link(obj)
    return obj


def mesh_from_pydata(name: str, verts, faces) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    mesh.validate()
    return _link(bpy.data.objects.new(name, mesh))


def make_box(name: str, x0: float, x1: float, y0: float, y1: float,
             z0: float, z1: float, jitter: float = 0.0) -> bpy.types.Object:
    rng = random.Random(stable_hash(name))
    verts = [
        [x0, y0, z0], [x1, y0, z0], [x1, y1, z0], [x0, y1, z0],
        [x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1],
    ]
    if jitter:
        for vertex in verts:
            vertex[0] += rng.uniform(-jitter, jitter)
            vertex[1] += rng.uniform(-jitter, jitter)
            vertex[2] += rng.uniform(-jitter, jitter)
    faces = [
        (0, 3, 2, 1),  # bottom, -z
        (4, 5, 6, 7),  # top, +z
        (0, 1, 5, 4),  # -y
        (2, 3, 7, 6),  # +y
        (1, 2, 6, 5),  # +x
        (0, 4, 7, 3),  # -x
    ]
    return mesh_from_pydata(name, verts, faces)


def make_cylinder(name: str, center: tuple[float, float, float],
                  radius_bottom: float, radius_top: float, height: float,
                  segments: int = 8, cap_top: bool = True,
                  axis: str = "z") -> bpy.types.Object:
    """Axis 'z': base ring at local z=0 rising to height; object located at
    `center` (base centre). Axis 'x': lying along local x, centred."""
    verts = []
    faces = []
    if axis == "z":
        for i in range(segments):
            angle = (i / segments) * math.tau
            verts.append([math.cos(angle) * radius_bottom,
                          math.sin(angle) * radius_bottom, 0.0])
        for i in range(segments):
            angle = (i / segments) * math.tau
            verts.append([math.cos(angle) * radius_top,
                          math.sin(angle) * radius_top, height])
    else:
        half = height / 2.0
        for i in range(segments):
            angle = (i / segments) * math.tau
            verts.append([-half, math.cos(angle) * radius_bottom,
                          math.sin(angle) * radius_bottom])
        for i in range(segments):
            angle = (i / segments) * math.tau
            verts.append([half, math.cos(angle) * radius_top,
                          math.sin(angle) * radius_top])
    for i in range(segments):
        j = (i + 1) % segments
        faces.append((i, j, segments + j, segments + i))
    if cap_top and radius_top > 0.001:
        faces.append(tuple(range(segments, segments * 2)))
    if radius_bottom > 0.001:
        faces.append(tuple(range(segments - 1, -1, -1)))
    obj = mesh_from_pydata(name, verts, faces)
    obj.location = center
    return obj


def rotate_around(obj: bpy.types.Object, angle: float, axis: str = "z",
                  pivot: tuple[float, float, float] = (0.0, 0.0, 0.0)) -> None:
    """Rotate local vertices about a pivot point."""
    pivot_v = mathutils.Vector(pivot)
    if axis == "z":
        rotation = mathutils.Euler((0.0, 0.0, angle))
    elif axis == "x":
        rotation = mathutils.Euler((angle, 0.0, 0.0))
    else:
        rotation = mathutils.Euler((0.0, angle, 0.0))
    matrix = rotation.to_matrix().to_4x4()
    for vertex in obj.data.vertices:
        vertex.co = matrix @ (vertex.co - pivot_v) + pivot_v
    obj.data.update()


def translate_vertices(obj: bpy.types.Object, dx: float, dy: float,
                       dz: float) -> None:
    for vertex in obj.data.vertices:
        vertex.co.x += dx
        vertex.co.y += dy
        vertex.co.z += dz
    obj.data.update()


def bbox_center(obj: bpy.types.Object) -> tuple[float, float, float]:
    xs = [v.co.x for v in obj.data.vertices] or [0.0]
    ys = [v.co.y for v in obj.data.vertices] or [0.0]
    zs = [v.co.z for v in obj.data.vertices] or [0.0]
    return ((min(xs) + max(xs)) / 2.0, (min(ys) + max(ys)) / 2.0,
            (min(zs) + max(zs)) / 2.0)


def bake_transforms(obj: bpy.types.Object) -> None:
    """Fold object rotation and scale into vertex data with exact
    world-preservation: world transform is location + R * (S * v), so the
    COMPOSITE matrix R*S is applied to local vertices and location stays as
    the GLB node translation. export_apply does NOT bake object transforms,
    so any leftover scale would keep multiplying vertex positions around the
    GLB node origin in Godot (observed: KaraRoot_0 authored at z=-98 landing
    at z≈-68.6 because its scale y=0.7 shrank the authored position)."""
    rotation = obj.rotation_euler
    scale = obj.scale
    has_rotation = rotation.x or rotation.y or rotation.z
    has_scale = any(abs(s - 1.0) > 1e-6 for s in scale)
    if not (has_rotation or has_scale):
        return
    matrix = rotation.to_matrix().to_4x4()
    if has_scale:
        matrix = matrix @ mathutils.Matrix.Diagonal(
            mathutils.Vector((scale.x, scale.y, scale.z, 1.0)))
    for vertex in obj.data.vertices:
        transformed = matrix @ vertex.co.to_4d()
        vertex.co = transformed.to_3d()
    obj.data.update()
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)


def make_icosphere(name: str, center: tuple[float, float, float], radius: float,
                   subdivisions: int = 2) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name)
    bm = bmesh_module().new()
    bmesh_module().ops.create_icosphere(
        bm, subdivisions=subdivisions, radius=radius,
        matrix=mathutils.Matrix.Identity(4))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    obj = _link(bpy.data.objects.new(name, mesh))
    obj.location = center
    return obj


def bmesh_module():
    import bmesh
    return bmesh


def displace_object(obj: bpy.types.Object, scale: float, frequency: float,
                    seed_offset: float = 0.0) -> None:
    for vertex in obj.data.vertices:
        wx = vertex.co.x + obj.location.x
        wy = vertex.co.y + obj.location.y
        wz = vertex.co.z + obj.location.z
        nx = (fbm(wx * frequency + seed_offset, wy * frequency, 2) - 0.5) * 2.0
        ny = (fbm(wy * frequency + 17.3 + seed_offset, wz * frequency, 2) - 0.5) * 2.0
        nz = (fbm(wz * frequency + 41.7 + seed_offset, wx * frequency, 2) - 0.5) * 2.0
        vertex.co.x += nx * scale
        vertex.co.y += ny * scale
        vertex.co.z += nz * scale
    obj.data.update()


def rotate_vertices_z(obj: bpy.types.Object, angle: float) -> None:
    """Rotate mesh vertices about local origin around Z (yaw)."""
    if angle == 0:
        return
    cos = math.cos(angle)
    sin = math.sin(angle)
    for vertex in obj.data.vertices:
        x = vertex.co.x
        y = vertex.co.y
        vertex.co.x = x * cos - y * sin
        vertex.co.y = x * sin + y * cos
    obj.data.update()


def assign_material(obj: bpy.types.Object, material_name: str) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(mat(material_name))


# ---------------------------------------------------------------------------
# Polyline helpers (planar, Blender coordinates)
# ---------------------------------------------------------------------------
def sample_polyline(points: list[tuple[float, float]], spacing: float):
    """Re-sample a polyline at roughly equal spacing. Returns list of
    (x, y, tangent_x, tangent_y)."""
    segments = []
    total = 0.0
    for i in range(len(points) - 1):
        ax, ay = points[i]
        bx, by = points[i + 1]
        length = math.hypot(bx - ax, by - ay)
        segments.append((ax, ay, bx, by, length))
        total += length
    count = max(2, int(total / spacing) + 1)
    result = []
    seg_index = 0
    seg_start = 0.0
    for step in range(count):
        target = total * step / (count - 1)
        while seg_index < len(segments) - 1 and target > seg_start + segments[seg_index][4]:
            seg_start += segments[seg_index][4]
            seg_index += 1
        ax, ay, bx, by, length = segments[seg_index]
        t = 0.0 if length <= 1e-6 else (target - seg_start) / length
        tangent_x = (bx - ax) / max(length, 1e-6)
        tangent_y = (by - ay) / max(length, 1e-6)
        result.append((ax + (bx - ax) * t, ay + (by - ay) * t, tangent_x, tangent_y))
    return result


def distance_to_polyline(points: Sequence, x: float,
                         y: float) -> float:
    best = float("inf")
    for i in range(len(points) - 1):
        ax, ay = points[i][0], points[i][1]
        bx, by = points[i + 1][0], points[i + 1][1]
        abx = bx - ax
        aby = by - ay
        length_sq = abx * abx + aby * aby
        t = 0.0 if length_sq <= 1e-9 else max(
            0.0, min(1.0, ((x - ax) * abx + (y - ay) * aby) / length_sq))
        px = ax + abx * t
        py = ay + aby * t
        best = min(best, math.hypot(x - px, y - py))
    # Close distance to last point as well.
    if points:
        lx, ly = points[-1][0], points[-1][1]
        best = min(best, math.hypot(x - lx, y - ly))
    return best


def build_ribbon(name: str, points: list[tuple[float, float]],
                 profile: list[tuple[float, float]],
                 spacing: float = 1.0, height_lookup=None,
                 z_pad: float = 0.02) -> bpy.types.Object:
    """Ground-hugging ribbon along a polyline. Profile entries are
    (lateral offset, relative height) from left to right."""
    samples = sample_polyline(points, spacing)
    verts = []
    faces = []
    cols = len(profile)
    for sx, sy, tx, ty in samples:
        normal_x = -ty
        normal_y = tx
        for lateral, rel_height in profile:
            x = sx + normal_x * lateral
            y = sy + normal_y * lateral
            base = height_lookup(x, y) if height_lookup else 0.0
            verts.append([x, y, base + rel_height + z_pad])
    for row in range(len(samples) - 1):
        for col in range(cols - 1):
            a = row * cols + col
            b = a + 1
            c = a + cols + 1
            d = a + cols
            faces.append((a, d, c, b))
    return mesh_from_pydata(name, verts, faces)


def ribbon_profile_road(half_width: float, rut_depth: float = 0.12,
                        crown: float = 0.05) -> list[tuple[float, float]]:
    hw = half_width
    return [
        (-hw, -0.16),
        (-hw * 0.78, -0.06),
        (-hw * 0.52, 0.0),
        (-hw * 0.38, -rut_depth),
        (-hw * 0.30, -rut_depth * 1.15),
        (-hw * 0.22, -rut_depth * 0.6),
        (0.0, crown),
        (hw * 0.22, -rut_depth * 0.6),
        (hw * 0.30, -rut_depth * 1.15),
        (hw * 0.38, -rut_depth),
        (hw * 0.52, 0.0),
        (hw * 0.78, -0.06),
        (hw, -0.16),
    ]


def make_puddle(name: str, center: tuple[float, float], radius_x: float,
                radius_z: float, depth: float, height_lookup) -> bpy.types.Object:
    """Flat dark water disc in Blender coordinates."""
    cx, cy = center
    base = height_lookup(cx, cy) if height_lookup else 0.0
    verts = []
    faces = []
    segments = 14
    for scale in (1.0, 0.55):
        for i in range(segments):
            angle = i / segments * math.tau
            verts.append([
                cx + math.cos(angle) * radius_x * scale,
                cy + math.sin(angle) * radius_z * scale,
                base + 0.022 + 0.012 * scale,
            ])
    verts.append([cx, cy, base + 0.016])
    centre = len(verts) - 1
    # Band between outer (0) and inner (1) rings, facing up.
    for i in range(segments):
        a = i
        b = (i + 1) % segments
        c = segments + (i + 1) % segments
        d = segments + i
        faces.append((a, b, c, d))
    # Fan from inner ring to centre, facing up.
    for i in range(segments):
        faces.append((segments + i, segments + (i + 1) % segments, centre))
    return mesh_from_pydata(name, verts, faces)


def make_patch(name: str, centre: tuple[float, float], size_x: float,
               size_y: float, height_lookup, z_pad: float = 0.025,
               segments: int = 5) -> bpy.types.Object:
    """Rough ground patch hugging terrain, edges jittered. Coordinates are
    passed in the same space as height_lookup."""
    rng = random.Random(stable_hash(name))
    cx, cy = centre
    verts = []
    faces = []
    rows = segments + 1
    cols = segments + 1
    for iy in range(rows):
        for ix in range(cols):
            u = ix / segments
            v = iy / segments
            edge = ix == 0 or ix == segments or iy == 0 or iy == segments
            jx = rng.uniform(-0.12, 0.12) if edge else rng.uniform(-0.05, 0.05)
            jy = rng.uniform(-0.12, 0.12) if edge else rng.uniform(-0.05, 0.05)
            x = cx - size_x / 2 + u * size_x + jx
            y = cy - size_y / 2 + v * size_y + jy
            z = (height_lookup(x, y) if height_lookup else 0.0) + z_pad
            verts.append([x, y, z])
    for iy in range(segments):
        for ix in range(segments):
            a = iy * cols + ix
            faces.append((a, a + 1, a + cols + 1, a + cols))
    return mesh_from_pydata(name, verts, faces)


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------
def setup_scene() -> None:
    _material_cache.clear()
    for block in list(bpy.data.objects):
        bpy.data.objects.remove(block, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)
    for camera in list(bpy.data.cameras):
        bpy.data.cameras.remove(camera)
    for light in list(bpy.data.lights):
        bpy.data.lights.remove(light)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def export_glb(objects: list[bpy.types.Object], out_path: str,
               root_name: str) -> None:
    for obj in bpy.data.objects:
        obj.select_set(False)
    for obj in objects:
        bake_transforms(obj)
    root = bpy.data.objects.new(root_name, None)
    bpy.context.scene.collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    for obj in objects:
        obj.parent = root
        obj.select_set(True)
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format="GLB",
        use_selection=True,
        export_apply=True,
    )


def save_blend(out_path: str) -> None:
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out_path)


def report_kit(kit_name: str, objects: list[bpy.types.Object],
               glb_path: str) -> None:
    total_tris = 0
    entries = []
    for obj in objects:
        if obj.data is None:
            continue
        mesh = obj.data
        tris = 0
        for polygon in mesh.polygons:
            tris += max(1, len(polygon.vertices) - 2)
        xs = [v.co.x + obj.location.x for v in mesh.vertices] or [0.0]
        ys = [v.co.y + obj.location.y for v in mesh.vertices] or [0.0]
        zs = [v.co.z + obj.location.z for v in mesh.vertices] or [0.0]
        entries.append({
            "name": obj.name,
            "verts": len(mesh.vertices),
            "tris": tris,
            "bounds_x": [min(xs), max(xs)],
            "bounds_y": [min(ys), max(ys)],
            "bounds_z": [min(zs), max(zs)],
        })
        total_tris += tris
    payload = {
        "kit": kit_name,
        "glb": glb_path,
        "objects": len(entries),
        "triangles": total_tris,
        "entries": entries,
    }
    print("AGENTB_KIT_JSON " + json.dumps(payload))
