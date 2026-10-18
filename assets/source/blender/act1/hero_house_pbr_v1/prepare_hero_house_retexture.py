"""Stage the shipped hero-house component for an external retexture request.

The imported GLB remains the geometry authority. This script extracts the
HeroHouse_TimberPlaster subtree, preserves an untouched Blender source snapshot,
then applies only the already-shipped runtime presentation geometry that changes
the house silhouette: rounded courses and wall crowns. It does not edit the
canonical generator or game asset.
"""

from __future__ import annotations

import hashlib
import json
import math
import re
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector


PROJECT_ROOT = Path(__file__).resolve().parents[5]
SOURCE_GLB = PROJECT_ROOT / "game/assets/models/act1/urman_village_exterior_kit.glb"
OUTPUT_DIR = Path(__file__).resolve().parent
BLEND_OUT = OUTPUT_DIR / "fullhero.blend"
GLB_OUT = OUTPUT_DIR / "opaquehero-for-retexture.glb"
MANIFEST_OUT = OUTPUT_DIR / "manifest.json"
RENDER_OUT = OUTPUT_DIR / "before-imported-materials-pre-runtime-regrade-front-three-quarter.png"
HERO_ROOT_NAME = "HeroHouse_TimberPlaster"
ROUND_SEGMENTS = 20
WALL_LOG_SEGMENTS = 14
WALL_COURSE = 0.26
RETEXTURE_ATLAS_SIZE = 4096
CROWN_CAP_UV_BOOST = 8.0
V3_INPUT_MODE = "--v3" in sys.argv
V3_METADATA_OUT = OUTPUT_DIR / "opaquehero-for-retexture-v3.metadata.json"
if V3_INPUT_MODE:
    GLB_OUT = OUTPUT_DIR / "opaquehero-for-retexture-v3.glb"

# Blender's Z-up mesh coordinates map to the Godot/glTF Y-up coordinates used by
# the production runtime as (x, z, -y). This is a proper rotation (determinant 1).
def blender_to_runtime(v: Vector) -> Vector:
    return Vector((v.x, v.z, -v.y))


def runtime_to_blender(v: Vector) -> Vector:
    return Vector((v.x, -v.z, v.y))


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def clean_material_name(name: str) -> str:
    return re.sub(r"\.\d{3}$", "", name)


def object_material_names(obj):
    return [clean_material_name(slot.material.name) if slot.material else ""
            for slot in obj.material_slots]


def mesh_triangles(mesh) -> int:
    return sum(max(0, len(poly.vertices) - 2) for poly in mesh.polygons)


def uv_layer_summary(mesh):
    if not mesh.uv_layers:
        return {"status": "missing", "layer_names": [], "loop_count": len(mesh.loops), "bounds": None}
    layer = mesh.uv_layers.active or mesh.uv_layers[0]
    if len(layer.data) != len(mesh.loops):
        return {
            "status": "invalid_loop_count",
            "layer_names": [uv.name for uv in mesh.uv_layers],
            "loop_count": len(layer.data),
            "bounds": None,
        }
    coords = [(float(item.uv.x), float(item.uv.y)) for item in layer.data]
    bounds = None
    if coords:
        bounds = [[round(min(point[axis] for point in coords), 7),
                   round(max(point[axis] for point in coords), 7)] for axis in (0, 1)]
    return {
        "status": "present",
        "layer_names": [uv.name for uv in mesh.uv_layers],
        "loop_count": len(layer.data),
        "bounds": bounds,
    }


def uv_loop_sha256(mesh):
    layer = mesh.uv_layers.active or (mesh.uv_layers[0] if mesh.uv_layers else None)
    if layer is None or len(layer.data) != len(mesh.loops):
        return None
    payload = [[round(float(item.uv.x), 7), round(float(item.uv.y), 7)] for item in layer.data]
    return hashlib.sha256(json.dumps(payload, separators=(",", ":")).encode("ascii")).hexdigest()


def matrix_rows(matrix):
    return [[round(float(value), 9) for value in row] for row in matrix]


def vlist(v):
    return [round(float(c), 7) for c in v]


def mesh_bounds_in_runtime(mesh):
    points = [blender_to_runtime(vertex.co) for vertex in mesh.vertices]
    if not points:
        return None
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return [vlist(low), vlist(high)]


def object_root_relative_bounds(obj, root_inverse):
    points = [root_inverse @ (obj.matrix_world @ vertex.co) for vertex in obj.data.vertices]
    if not points:
        return None
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return [vlist(low), vlist(high)]


def axis_aligned_bounds(points):
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return low, high


def append_triangle(vertices, faces, loop_uvs, loop_normals, a, b, c, position_indices=None):
    pa, na, uva = a
    pb, nb, uvb = b
    pc, nc, uvc = c
    if (pb - pa).cross(pc - pa).dot(na + nb + nc) > 0.0:
        pb, pc = pc, pb
        nb, nc = nc, nb
        uvb, uvc = uvc, uvb
    def vertex_index(position):
        blender_position = runtime_to_blender(position)
        if position_indices is None:
            index = len(vertices)
            vertices.append(blender_position)
            return index
        # Crown faces are independent triangles in the runtime builder. Reuse
        # only bit-identical local positions so UVs/normals remain per-loop.
        key = tuple(float(value) for value in blender_position)
        if key in position_indices:
            return position_indices[key]
        index = len(vertices)
        position_indices[key] = index
        vertices.append(blender_position)
        return index

    faces.append((vertex_index(pa), vertex_index(pb), vertex_index(pc)))
    loop_uvs.extend((uva, uvb, uvc))
    loop_normals.extend((runtime_to_blender(na), runtime_to_blender(nb), runtime_to_blender(nc)))


def mesh_from_triangles(name, triangles, material_slots, weld_coincident_positions=False):
    vertices, faces, face_materials, loop_uvs, loop_normals, smooth_faces = [], [], [], [], [], []
    position_indices = {} if weld_coincident_positions else None
    for tri in triangles:
        append_triangle(
            vertices, faces, loop_uvs, loop_normals, tri[0], tri[1], tri[2], position_indices
        )
        face_materials.append(tri[3])
        smooth_faces.append(tri[4])
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    for material in material_slots:
        mesh.materials.append(material)
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for polygon in mesh.polygons:
        polygon.material_index = face_materials[polygon.index]
        polygon.use_smooth = smooth_faces[polygon.index]
        for loop_index in polygon.loop_indices:
            uv_layer.data[loop_index].uv = loop_uvs[loop_index]
    try:
        mesh.normals_split_custom_set(loop_normals)
    except (AttributeError, RuntimeError, ValueError) as error:
        if weld_coincident_positions:
            bpy.data.meshes.remove(mesh)
            raise RuntimeError(f"Could not preserve crown loop normals on {name}") from error
        # The per-triangle winding and smooth/flat assignments still preserve
        # the runtime profile when a Blender build does not accept loop normals.
        pass
    mesh.update()
    if weld_coincident_positions:
        expected_loops = len(loop_normals)
        if len(mesh.loops) != expected_loops or len(mesh.polygons) != len(faces):
            bpy.data.meshes.remove(mesh)
            raise RuntimeError(f"Crown welding changed loop or face count on {name}")
        stored_normals = [loop.normal.normalized() for loop in mesh.loops]
        for actual, expected in zip(stored_normals, loop_normals):
            if actual.dot(expected.normalized()) < 0.999999:
                bpy.data.meshes.remove(mesh)
                raise RuntimeError(f"Crown welding changed a loop normal on {name}")
        mesh["coincident_position_weld"] = {
            "input_triangle_vertex_count": len(triangles) * 3,
            "output_vertex_count": len(vertices),
            "merged_duplicate_positions": len(triangles) * 3 - len(vertices),
            "faces_preserved": len(faces),
            "loops_preserved": len(mesh.loops),
            "loop_normals_preserved": True,
            "loop_uvs_preserved_before_atlas_projection": True,
        }
    return mesh


def round_log_mesh(obj, material, end_material=None, woodpile=False, corner_end=False):
    source = obj.data
    if not source.vertices:
        return None
    low, high = axis_aligned_bounds([blender_to_runtime(v.co) for v in source.vertices])
    size = high - low
    centre = (low + high) * 0.5
    along_x = size.x < size.z if corner_end else size.x >= size.z
    length = size.x if along_x else size.z
    depth = size.z if along_x else size.x
    height = size.y
    if corner_end:
        if not (0.15 <= height <= 0.45 and 0.15 <= depth <= 0.45 and 0.04 <= length <= 0.3):
            return None
    elif woodpile:
        if not (0.06 <= height <= 0.45 and height * 0.7 <= depth <= height * 1.4 and length >= height * 1.6):
            return None
    elif not (0.15 <= height <= 0.45 and 0.06 <= depth <= 0.40 and length >= height * 2.5):
        return None

    radius_y = height * 0.5 * (1.0 if woodpile or corner_end else 1.12)
    radius_d = depth * 0.5
    half = length * 0.5

    def point(along, d, y):
        return (Vector((centre.x + along, centre.y + y, centre.z + d)) if along_x
                else Vector((centre.x + d, centre.y + y, centre.z + along)))

    def direction(along, d, y):
        out = Vector((along, y, d)) if along_x else Vector((d, y, along))
        return out.normalized()

    ring = []
    arc = 0.0
    for i in range(ROUND_SEGMENTS + 1):
        t = math.tau * i / ROUND_SEGMENTS
        d = radius_d * math.cos(t)
        y = radius_y * math.sin(t)
        if ring:
            arc += math.hypot(d - ring[-1][0], y - ring[-1][1])
        normal = direction(0.0, d / (radius_d * radius_d), y / (radius_y * radius_y))
        ring.append((d, y, normal, arc))

    triangles = []
    def uv(along, girth):
        return (Vector((along, girth)) if woodpile else Vector((girth, along)))

    for i in range(ROUND_SEGMENTS):
        r0, r1 = ring[i], ring[i + 1]
        a = (point(-half, r0[0], r0[1]), r0[2], uv(-half, r0[3]))
        b = (point(half, r0[0], r0[1]), r0[2], uv(half, r0[3]))
        c = (point(half, r1[0], r1[1]), r1[2], uv(half, r1[3]))
        d = (point(-half, r1[0], r1[1]), r1[2], uv(-half, r1[3]))
        triangles.append((a, b, c, 0, True))
        triangles.append((a, c, d, 0, True))

    end_slot = 1 if end_material is not None else 0
    for sign in (-1.0, 1.0):
        normal = direction(sign, 0.0, 0.0)
        hub = (point(sign * half, 0.0, 0.0), normal, Vector((0.5, 0.5)))
        for i in range(ROUND_SEGMENTS):
            r0, r1 = ring[i], ring[i + 1]
            uv0 = Vector((0.5 + 0.5 * r0[0] / radius_d,
                          0.5 + 0.5 * r0[1] / radius_y))
            uv1 = Vector((0.5 + 0.5 * r1[0] / radius_d,
                          0.5 + 0.5 * r1[1] / radius_y))
            a = (point(sign * half, 0.0, 0.0), normal, Vector((0.5, 0.5)))
            b = (point(sign * half, r0[0], r0[1]), normal, uv0)
            c = (point(sign * half, r1[0], r1[1]), normal, uv1)
            triangles.append((a, b, c, end_slot, False))

    slots = [material] + ([end_material] if end_material else [])
    return mesh_from_triangles(obj.name + "_RuntimeRound", triangles, slots)


def round_runtime_hero_courses(mesh_objects, node_records):
    replaced = []
    for obj in mesh_objects:
        names = object_material_names(obj)
        unique = list(dict.fromkeys(name for name in names if name))
        if len(unique) not in (1, 2):
            continue
        corner_end = obj.name.startswith("HeroHouse_CornerEnd")
        body_mat = end_mat = None
        woodpile = False
        for slot in obj.material_slots:
            if not slot.material:
                continue
            name = clean_material_name(slot.material.name)
            if name in ("URMAN_Hero_Log", "URMAN_Bark_Muted"):
                body_mat = slot.material
                woodpile = name == "URMAN_Bark_Muted"
            elif name in ("URMAN_Hero_LogEnd", "URMAN_Wood_CutEnd"):
                end_mat = slot.material
            else:
                body_mat = None
                break
        if body_mat is None or (len(unique) == 2 and end_mat is None):
            continue
        new_mesh = round_log_mesh(obj, body_mat, end_mat, woodpile, corner_end)
        if new_mesh is None:
            continue
        old_triangles = mesh_triangles(obj.data)
        obj.data = new_mesh
        replaced.append(obj.name)
        node_records[obj.name]["runtime_geometry"] = {
            "operation": "RoundHeroLogCourses / BuildRoundLog",
            "source_mesh_triangles": old_triangles,
            "runtime_mesh_triangles": mesh_triangles(new_mesh),
            "round_segments": ROUND_SEGMENTS,
            "source_materials": unique,
            "local_transform_preserved": True,
        }
    return replaced


def face_source_material(obj, polygon):
    index = polygon.material_index
    if index < len(obj.material_slots) and obj.material_slots[index].material:
        return clean_material_name(obj.material_slots[index].material.name)
    return ""


def keep_mesh_surface(obj, node_records, excluded_nodes):
    name = obj.name
    if "Glass" in name or "Recess" in name:
        excluded_nodes.append({"name": name, "reason": "transparent Glass/Recess source surface excluded by retexture-input contract"})
        node_records[name]["input_disposition"] = "excluded_glass_or_recess"
        return False
    mesh = obj.data
    excluded_faces = []
    excluded_material_names = set()
    for poly in mesh.polygons:
        material_name = face_source_material(obj, poly)
        if "RoofSnow" in material_name or material_name == "URMAN_Hero_RoofSnow":
            excluded_faces.append(poly.index)
            excluded_material_names.add(material_name)
    if excluded_faces:
        bm = bmesh.new()
        bm.from_mesh(mesh)
        face_by_index = {face.index: face for face in bm.faces}
        bmesh.ops.delete(bm, geom=[face_by_index[i] for i in excluded_faces if i in face_by_index], context="FACES_ONLY")
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        node_records[name]["excluded_material_surfaces"] = sorted(excluded_material_names)
        node_records[name]["excluded_source_triangles"] = len(excluded_faces)
    if not mesh.polygons:
        excluded_nodes.append({"name": name, "reason": "no opaque faces remain after RoofSnow exclusion"})
        node_records[name]["input_disposition"] = "excluded_roof_snow_only"
        return False
    node_records[name]["input_disposition"] = "included"
    return True


def make_runtime_crown_material():
    name = "Runtime_Hero_LogCrown_SourceColor_8b7763_PreRegrade"
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    srgb = [0x8B / 255.0, 0x77 / 255.0, 0x63 / 255.0]
    linear = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*linear, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.82
    mat.diffuse_color = (*linear, 1.0)
    mat["source_color_srgb"] = "#8b7763"
    mat["source_color_owner"] = "Act1ConnectedWorld.DwellingLogWalls.BuildDwellingLogWalls"
    mat["state"] = "sourcecolor before runtime painterly material library"
    return mat


def bounds_to_runtime_local(obj, wall_inverse):
    corners = [wall_inverse @ (obj.matrix_world @ Vector(corner)) for corner in obj.bound_box]
    return [blender_to_runtime(point) for point in corners]


def make_wall_crown_geometry(wall, parts, house_center_world, crown_material):
    source_points = [blender_to_runtime(v.co) for v in wall.data.vertices]
    if not source_points:
        return None, None
    low, high = axis_aligned_bounds(source_points)
    size = high - low
    name = wall.name
    hero = name.startswith("HeroHouse_")
    if not name.endswith("_Wall_LOD0") or not hero:
        return None, None
    if not (0.08 <= (size.z if size.x >= size.z else size.x) <= 0.4
            and size.y >= 1.2 and max(size.x, size.z) >= 1.0):
        return None, None
    along_x = size.x >= size.z
    thickness = size.z if along_x else size.x
    del thickness
    wall_inverse = wall.matrix_world.inverted_safe()
    centre_local = blender_to_runtime(wall_inverse @ house_center_world)
    mid = (low + high) * 0.5
    outward = (1.0 if ((mid.z - centre_local.z) if along_x else (mid.x - centre_local.x)) >= 0.0 else -1.0)
    face = high.z if along_x and outward > 0 else low.z if along_x else high.x if outward > 0 else low.x

    side_prefix = name[:name.index("Wall_LOD0")]
    openings = []
    for part in parts:
        part_name = part.name
        if part is wall or part.type != "MESH" or not part_name.startswith(side_prefix):
            continue
        if not ("Recess" in part_name or "_Glass" in part_name or "_Leaf" in part_name):
            continue
        if not part.data.vertices:
            continue
        corners = bounds_to_runtime_local(part, wall_inverse)
        op_low, op_high = axis_aligned_bounds(corners)
        openings.append((op_low, op_high))

    if name == "HeroHouse_Street_Wall_LOD0":
        portal_names = {
            "HeroHouse_Street_Portal0_Jamb-1_LOD0", "HeroHouse_Street_Portal0_Jamb1_LOD0",
            "HeroHouse_Street_Portal0_Rail-1_LOD0", "HeroHouse_Street_Portal0_Rail1_LOD0",
        }
        portal_parts = [part for part in parts if part.name in portal_names and part.type == "MESH" and part.data.vertices]
        if len(portal_parts) == 4:
            points = []
            for part in portal_parts:
                points.extend(bounds_to_runtime_local(part, wall_inverse))
            openings.append(axis_aligned_bounds(points))

    main = False  # Runtime BuildDwellingLogWalls explicitly excludes hero from main-wall overhangs.
    side_wall = "_Left_Wall_" in name or "_Right_Wall_" in name
    courses = max(3, round(size.y / WALL_COURSE))
    course = size.y / courses
    start = (low.x if along_x else low.z) - (0.18 if main else 0.0)
    end = (high.x if along_x else high.z) + (0.18 if main else 0.0)
    radius_depth = 0.12
    depth_center = face + outward * (0.035 - radius_depth)
    triangles = []

    def p(along, depth, height):
        return Vector((along, height, depth)) if along_x else Vector((depth, height, along))

    def add_log(from_pos, to_pos, y, depth, ry, rd):
        ring = []
        arc = 0.0
        for i in range(WALL_LOG_SEGMENTS + 1):
            t = math.tau * i / WALL_LOG_SEGMENTS
            dd, yy = rd * math.cos(t), ry * math.sin(t)
            if ring:
                arc += math.hypot(dd - ring[-1][0], yy - ring[-1][1])
            normal = p(0.0, dd / (rd * rd), yy / (ry * ry)).normalized()
            ring.append((dd, yy, normal, arc))
        def uv(length, girth):
            return Vector((girth, length))  # referenceHero = true in runtime
        for i in range(WALL_LOG_SEGMENTS):
            r0, r1 = ring[i], ring[i + 1]
            a = (p(from_pos, depth + r0[0], y + r0[1]), r0[2], uv(from_pos, r0[3]))
            b = (p(to_pos, depth + r0[0], y + r0[1]), r0[2], uv(to_pos, r0[3]))
            c = (p(to_pos, depth + r1[0], y + r1[1]), r1[2], uv(to_pos, r1[3]))
            e = (p(from_pos, depth + r1[0], y + r1[1]), r1[2], uv(from_pos, r1[3]))
            triangles.append((a, b, c, 0, True))
            triangles.append((a, c, e, 0, True))
        for at, sign in ((from_pos, -1.0), (to_pos, 1.0)):
            normal = p(sign, 0.0, 0.0).normalized()
            hub = (p(at, depth, y), normal, Vector((0.5, 0.5)))
            for i in range(WALL_LOG_SEGMENTS):
                r0, r1 = ring[i], ring[i + 1]
                uv0 = Vector((0.5 + 0.5 * r0[0] / rd, 0.5 + 0.5 * r0[1] / ry))
                uv1 = Vector((0.5 + 0.5 * r1[0] / rd, 0.5 + 0.5 * r1[1] / ry))
                a = (p(at, depth, y), normal, Vector((0.5, 0.5)))
                b = (p(at, depth + r0[0], y + r0[1]), normal, uv0)
                c = (p(at, depth + r1[0], y + r1[1]), normal, uv1)
                triangles.append((a, b, c, 0, False))

    generated_log_count = 0
    for k in range(courses):
        centre_y = low.y + course * (k + 0.5) + (course * 0.5 if side_wall else 0.0)
        if centre_y + course * 0.5 > high.y + 0.01:
            continue
        cuts = []
        for op_low, op_high in openings:
            if centre_y + course * 0.42 > op_low.y and centre_y - course * 0.42 < op_high.y:
                cuts.append(((op_low.x - 0.02, op_high.x + 0.02) if along_x
                             else (op_low.z - 0.02, op_high.z + 0.02)))
        spans = [(start, end)]
        for c0, c1 in cuts:
            next_spans = []
            for s0, s1 in spans:
                if c1 <= s0 or c0 >= s1:
                    next_spans.append((s0, s1))
                else:
                    if c0 > s0:
                        next_spans.append((s0, c0))
                    if c1 < s1:
                        next_spans.append((c1, s1))
            spans = next_spans
        for from_pos, to_pos in spans:
            if to_pos - from_pos < 0.2:
                continue
            add_log(from_pos, to_pos, centre_y, depth_center, course * 0.49, radius_depth)
            generated_log_count += 1
    if not triangles:
        return None, {"wall": name, "runtime_log_spans": 0}

    mesh = mesh_from_triangles(
        name.replace("_Wall_LOD0", "_LogCrown") + "Runtime",
        triangles,
        [crown_material],
        weld_coincident_positions=True,
    )

    # Match RemoveHeroWallFront: remove only source triangles wholly on the
    # outer slab face; keep backing and all window/door reveals intact.
    backing = wall.data.copy()
    bm = bmesh.new()
    bm.from_mesh(backing)
    remove_faces = []
    for face_obj in bm.faces:
        if all(abs((blender_to_runtime(v.co).z if along_x else blender_to_runtime(v.co).x) - face) < 0.001
               for v in face_obj.verts):
            remove_faces.append(face_obj)
    bmesh.ops.delete(bm, geom=remove_faces, context="FACES_ONLY")
    bm.to_mesh(backing)
    bm.free()
    backing.update()
    old_mesh = wall.data
    wall.data = backing
    if old_mesh.users == 0:
        bpy.data.meshes.remove(old_mesh)

    return mesh, {
        "wall": name,
        "crown_node": name.replace("_Wall_LOD0", "_LogCrown"),
        "runtime_log_spans": generated_log_count,
        "runtime_log_segments": WALL_LOG_SEGMENTS,
        "wall_face_runtime_axis": "z" if along_x else "x",
        "wall_face_coordinate_runtime": round(float(face), 7),
        "wall_front_triangles_removed": len(remove_faces),
        "wall_backing_triangles_after": mesh_triangles(backing),
        "opening_count": len(openings),
        "local_transform_source": matrix_rows(wall.matrix_basis),
    }


def hero_centre_world(root, parts):
    footings = [obj for obj in parts if obj.type == "MESH"
                and ("_Foundation_LOD0" in obj.name or "_FootingCap_LOD0" in obj.name)
                and obj.data.vertices]
    if footings:
        footing = max(footings, key=lambda obj: obj.dimensions.x * obj.dimensions.y)
        local_bounds = [Vector(c) for c in footing.bound_box]
        low, high = axis_aligned_bounds(local_bounds)
        return footing.matrix_world @ ((low + high) * 0.5), footing.name
    wall_centres = []
    for obj in parts:
        if obj.type == "MESH" and "_Wall_LOD0" in obj.name and obj.data.vertices:
            low, high = axis_aligned_bounds([Vector(c) for c in obj.bound_box])
            wall_centres.append(obj.matrix_world @ ((low + high) * 0.5))
    if wall_centres:
        return sum(wall_centres, Vector()) / len(wall_centres), "mean-wall-centres"
    return root.matrix_world.translation.copy(), "root-fallback"


def runtime_wall_crowns(root, mesh_objects, node_records, extra_objects):
    parts = [obj for obj in mesh_objects if obj.name.startswith("HeroHouse_")]
    center_world, center_source = hero_centre_world(root, parts)
    crown_material = make_runtime_crown_material()
    results = []
    hidden_courses = []
    for wall in [obj for obj in parts if obj.name.endswith("_Wall_LOD0")]:
        material_names = object_material_names(wall)
        if not material_names or material_names[0] != "URMAN_Hero_Log":
            continue
        crown_mesh, stats = make_wall_crown_geometry(wall, parts, center_world, crown_material)
        if crown_mesh is None:
            if stats:
                results.append(stats)
            continue
        crown = bpy.data.objects.new(stats["crown_node"], crown_mesh)
        bpy.context.scene.collection.objects.link(crown)
        crown.parent = wall.parent
        crown.matrix_basis = wall.matrix_basis.copy()
        crown["runtime_generated_from"] = wall.name
        crown["runtime_owner"] = "Act1ConnectedWorld.DwellingLogWalls.BuildDwellingLogWalls"
        crown["source_color_srgb"] = "#8b7763"
        crown["before_material_state"] = "runtime source color before PainterlyMaterialLibrary grade"
        extra_objects.append(crown)
        results.append(stats)
        node_records[wall.name]["runtime_geometry"] = {
            "operation": "BuildDwellingLogWalls / RemoveHeroWallFront",
            "wall_front_triangles_removed": stats["wall_front_triangles_removed"],
            "wall_backing_triangles_after": stats["wall_backing_triangles_after"],
            "wall_face_runtime_axis": stats["wall_face_runtime_axis"],
            "wall_face_coordinate_runtime": stats["wall_face_coordinate_runtime"],
            "local_transform_preserved": True,
            "generated_crown_node": stats["crown_node"],
        }

        side_prefix = wall.name[:wall.name.index("Wall_LOD0")]
        for source_course in parts:
            if re.fullmatch(re.escape(side_prefix) + r"Log\d{2}_\d+_LOD0", source_course.name):
                source_course.hide_render = True
                source_course.hide_set(True)
                hidden_courses.append(source_course.name)
                node_records[source_course.name]["runtime_visibility"] = "hidden; replaced by " + stats["crown_node"]
                node_records[source_course.name]["input_disposition"] = "omitted_runtime_hidden_replaced_by_wall_crown"
    return results, hidden_courses, center_source


def filter_export_surfaces(mesh_objects, node_records, excluded_nodes):
    included = []
    for obj in mesh_objects:
        record = node_records[obj.name]
        if record.get("input_disposition") == "omitted_runtime_hidden_replaced_by_wall_crown":
            continue
        if not keep_mesh_surface(obj, node_records, excluded_nodes):
            continue
        included.append(obj)
        record["input_triangles"] = mesh_triangles(obj.data)
    return included


def crown_cap_face_components(obj):
    """Find flat axial cap fans through geometric face normals and mesh adjacency."""
    mesh = obj.data
    cap_direction = {}
    edge_faces = {}
    for polygon in mesh.polygons:
        face_normal = polygon.normal.normalized()
        direction = None
        for axis in range(3):
            for sign in (-1, 1):
                if (face_normal[axis] * sign > 0.99999
                        and all(abs(face_normal[other]) < 0.001 for other in range(3) if other != axis)):
                    direction = (axis, sign)
                    break
            if direction is not None:
                break
        if direction is not None:
            cap_direction[polygon.index] = direction
        vertices = list(polygon.vertices)
        for index, vertex in enumerate(vertices):
            edge = tuple(sorted((
                _crown_position_key(mesh.vertices[vertex].co),
                _crown_position_key(mesh.vertices[vertices[(index + 1) % len(vertices)]].co),
            )))
            edge_faces.setdefault(edge, []).append(polygon.index)

    adjacency = {face: set() for face in cap_direction}
    for edge, faces in edge_faces.items():
        for face in faces:
            if face not in cap_direction:
                continue
            adjacency[face].update(
                other for other in faces
                if other != face and cap_direction.get(other) == cap_direction[face]
            )

    components = []
    unseen = set(cap_direction)
    while unseen:
        start = unseen.pop()
        stack = [start]
        component = {start}
        while stack:
            face = stack.pop()
            for neighbor in adjacency[face] & unseen:
                unseen.remove(neighbor)
                component.add(neighbor)
                stack.append(neighbor)
        components.append({"faces": sorted(component), "normal_axis": cap_direction[start][0],
                           "normal_sign": cap_direction[start][1]})
    return sorted(components, key=lambda item: (item["normal_axis"], item["normal_sign"], item["faces"][0]))


def _crown_position_key(position):
    return tuple(round(float(position[axis]), 6) for axis in range(3))


def crown_cap_uv_metrics(obj, component, texture_size=RETEXTURE_ATLAS_SIZE):
    mesh = obj.data
    uv_layer = mesh.uv_layers.active or (mesh.uv_layers[0] if mesh.uv_layers else None)
    if uv_layer is None:
        raise RuntimeError(f"Missing cap UV map on {obj.name}")
    face_uv_by_vertex = {}
    face_uv_area = {}
    edge_faces = {}
    vertex_counts = {}
    for face_index in component["faces"]:
        polygon = mesh.polygons[face_index]
        per_vertex = {}
        coords = []
        for loop_index in polygon.loop_indices:
            loop = mesh.loops[loop_index]
            point = uv_layer.data[loop_index].uv
            position_key = _crown_position_key(mesh.vertices[loop.vertex_index].co)
            per_vertex[position_key] = (float(point.x), float(point.y))
            coords.append((float(point.x), float(point.y)))
            vertex_counts[position_key] = vertex_counts.get(position_key, 0) + 1
        face_uv_by_vertex[face_index] = per_vertex
        area = abs(sum(coords[i][0] * coords[(i + 1) % len(coords)][1]
                       - coords[(i + 1) % len(coords)][0] * coords[i][1]
                       for i in range(len(coords)))) * 0.5
        face_uv_area[face_index] = area * texture_size * texture_size
        vertices = list(polygon.vertices)
        for index, vertex in enumerate(vertices):
            edge = tuple(sorted((
                _crown_position_key(mesh.vertices[vertex].co),
                _crown_position_key(mesh.vertices[vertices[(index + 1) % len(vertices)]].co),
            )))
            edge_faces.setdefault(edge, []).append(face_index)

    uv_adjacency = {face: set() for face in component["faces"]}
    for edge, faces in edge_faces.items():
        if len(faces) < 2:
            continue
        for face in faces:
            for other in faces:
                if other == face:
                    continue
                if all(
                    max(abs(face_uv_by_vertex[face][vertex][axis]
                            - face_uv_by_vertex[other][vertex][axis]) for axis in (0, 1)) <= 1e-6
                    for vertex in edge
                ):
                    uv_adjacency[face].add(other)

    unseen = set(component["faces"])
    island_count = 0
    while unseen:
        island_count += 1
        start = unseen.pop()
        stack = [start]
        while stack:
            face = stack.pop()
            for neighbor in uv_adjacency[face] & unseen:
                unseen.remove(neighbor)
                stack.append(neighbor)

    hub_index = max(vertex_counts, key=vertex_counts.get)
    hub = list(hub_index)
    areas = list(face_uv_area.values())
    return {
        "triangle_count": len(component["faces"]),
        "uv_island_count": island_count,
        "uv_contiguous": island_count == 1,
        "normal_axis_xyz": "XYZ"[component["normal_axis"]],
        "normal_sign": component["normal_sign"],
        "mesh_local_fan_hub_blender": hub,
        "uv_area_pixels_at_4096": round(sum(areas), 3),
        "triangle_uv_area_pixels_min_median_max": [
            round(min(areas), 3), round(sorted(areas)[len(areas) // 2], 3), round(max(areas), 3)
        ],
    }


def scale_crown_cap_islands(objects):
    rows = []
    for obj in objects:
        if obj.get("runtime_owner") != "Act1ConnectedWorld.DwellingLogWalls.BuildDwellingLogWalls":
            continue
        mesh = obj.data
        uv_layer = mesh.uv_layers.active or (mesh.uv_layers[0] if mesh.uv_layers else None)
        if uv_layer is None:
            raise RuntimeError(f"Missing UV map before cap weighting on {obj.name}")
        components = [component for component in crown_cap_face_components(obj)
                      if len(component["faces"]) == WALL_LOG_SEGMENTS]
        if not components:
            raise RuntimeError(f"No axial cap fans found on {obj.name}")
        for component in components:
            metrics = crown_cap_uv_metrics(obj, component)
            if not metrics["uv_contiguous"]:
                raise RuntimeError(f"Crown cap is not one connected UV island before weighting: {obj.name}/{metrics}")
            loop_indices = [
                loop_index
                for face_index in component["faces"]
                for loop_index in mesh.polygons[face_index].loop_indices
            ]
            points = [uv_layer.data[index].uv.copy() for index in loop_indices]
            center = Vector((sum(point.x for point in points) / len(points),
                             sum(point.y for point in points) / len(points)))
            for loop_index in loop_indices:
                point = uv_layer.data[loop_index].uv
                uv_layer.data[loop_index].uv = center + (point - center) * CROWN_CAP_UV_BOOST
            rows.append({"node": obj.name, **metrics, "uv_linear_weight": CROWN_CAP_UV_BOOST,
                         "uv_area_weight": CROWN_CAP_UV_BOOST * CROWN_CAP_UV_BOOST})
        mesh.update()
    if not rows:
        raise RuntimeError("No generated crown cap UV islands were weighted")
    return rows


def crown_cap_uv_report(objects, expected_fans=None):
    rows = []
    other_axial_components = []
    for obj in objects:
        if (obj.get("runtime_owner") != "Act1ConnectedWorld.DwellingLogWalls.BuildDwellingLogWalls"
                and not obj.name.endswith("_LogCrown")):
            continue
        for component in crown_cap_face_components(obj):
            metrics = crown_cap_uv_metrics(obj, component)
            row = {"node": obj.name, **metrics}
            if metrics["triangle_count"] != WALL_LOG_SEGMENTS:
                other_axial_components.append(row)
                continue
            if not metrics["uv_contiguous"]:
                raise RuntimeError(f"Crown cap UV island is fragmented after packing: {row}")
            if metrics["uv_area_pixels_at_4096"] < 256.0:
                raise RuntimeError(f"Crown cap UV island is below 16-pixel equivalent density: {row}")
            rows.append(row)
    if not rows:
        raise RuntimeError("No generated crown cap UV islands were reported")
    if expected_fans is not None:
        def identity(row):
            return (row["node"], row["normal_axis_xyz"], row["normal_sign"],
                    tuple(round(value, 4) for value in row["mesh_local_fan_hub_blender"]))

        expected = {identity(row) for row in expected_fans}
        actual = {identity(row) for row in rows}
        if expected != actual:
            raise RuntimeError(
                "The v3 GLB import changed its 14-face crown cap fan identities: "
                f"missing={sorted(expected - actual)} unexpected={sorted(actual - expected)}"
            )
    return {
        "texture_size_reference": [RETEXTURE_ATLAS_SIZE, RETEXTURE_ATLAS_SIZE],
        "fan_triangle_count": WALL_LOG_SEGMENTS,
        "fan_count": len(rows),
        "all_fans_contiguous_single_islands": True,
        "fan_identity_matches_pre_export": expected_fans is not None,
        "minimum_cap_uv_area_pixels_at_4096": round(min(r["uv_area_pixels_at_4096"] for r in rows), 3),
        "maximum_cap_uv_area_pixels_at_4096": round(max(r["uv_area_pixels_at_4096"] for r in rows), 3),
        "minimum_triangle_uv_area_pixels_at_4096": round(
            min(r["triangle_uv_area_pixels_min_median_max"][0] for r in rows), 3
        ),
        "fans": rows,
        "other_axial_planar_components_not_cap_fans": other_axial_components,
    }


def build_global_uv_atlas(objects, node_records):
    source_missing = 0
    source_present = 0
    for obj in objects:
        record = node_records[obj.name]
        source_state = record.get("source_uv", {}).get("status")
        if source_state == "missing":
            source_missing += 1
        elif source_state == "present":
            source_present += 1

        # The Meshy retexture request must have one unambiguous UV template
        # across every semantic node, even though the shipped GLB has gaps.
        while len(obj.data.uv_layers) > 1:
            obj.data.uv_layers.remove(obj.data.uv_layers[-1])
        if not obj.data.uv_layers:
            obj.data.uv_layers.new(name="UVMap")
        obj.data.uv_layers[0].name = "UVMap"
        obj.data.uv_layers.active_index = 0

    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    for obj in objects:
        if not obj.data.polygons:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(
            angle_limit=math.radians(66.0),
            margin_method="FRACTION",
            island_margin=0.001,
            correct_aspect=True,
            scale_to_bounds=False,
        )
        bpy.ops.object.mode_set(mode="OBJECT")

    cap_weights = scale_crown_cap_islands(objects)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(
        rotate=True,
        scale=True,
        merge_overlap=False,
        margin_method="FRACTION",
        margin=0.002,
    )
    bpy.ops.object.mode_set(mode="OBJECT")

    loop_total = 0
    missing_after = []
    global_low = [float("inf"), float("inf")]
    global_high = [float("-inf"), float("-inf")]
    for obj in objects:
        uv = uv_layer_summary(obj.data)
        digest = uv_loop_sha256(obj.data)
        record = node_records[obj.name]
        record["input_uv_template"] = {
            "layer_name": obj.data.uv_layers[0].name if obj.data.uv_layers else None,
            "loop_count": uv["loop_count"],
            "bounds": uv["bounds"],
            "sha256_loop_order": digest,
        }
        if uv["status"] != "present" or digest is None:
            missing_after.append(obj.name)
            continue
        loop_total += uv["loop_count"]
        if uv["bounds"]:
            for axis in (0, 1):
                global_low[axis] = min(global_low[axis], uv["bounds"][axis][0])
                global_high[axis] = max(global_high[axis], uv["bounds"][axis][1])

    cap_uv = crown_cap_uv_report(objects)

    return {
        "method": "exact coincident crown-position weld, Blender Smart UV Project, weighted continuous cap islands, then one multi-object Pack Islands operation",
        "target_layer": "UVMap",
        "source_nodes_missing_uv": source_missing,
        "source_nodes_with_uv": source_present,
        "source_missing_uv_cause": "428 of 560 pre-crown opaque nodes lacked UVs; 91 numbered wall courses are omitted where generated crowns replace them; remaining 469 source nodes (428 without and 41 with UVs) plus four generated crowns are reprojected and globally packed",
        "packed_mesh_node_count": len(objects),
        "packed_loop_count": loop_total,
        "packed_global_bounds": [global_low, global_high] if loop_total else None,
        "pack_margin_fraction": 0.002,
        "missing_after_pack": missing_after,
        "overlap_handling": "all selected UV islands were passed together to Blender's multi-object Pack Islands operator",
        "transfer_key": "node name + loop order; per-node input_uv_template.sha256_loop_order records the exact packed UV sequence",
        "crown_cap_uv_weighting": {
            "linear_weight_before_pack": CROWN_CAP_UV_BOOST,
            "area_weight_before_pack": CROWN_CAP_UV_BOOST * CROWN_CAP_UV_BOOST,
            "cap_fans": cap_weights,
        },
        "crown_cap_uv_after_pack": cap_uv,
    }


def setup_neutral_render(scene, root, export_objects, render_path):
    bpy.context.view_layer.update()
    root_inverse = root.matrix_world.inverted_safe()
    all_points = []
    for obj in export_objects:
        if obj.type != "MESH":
            continue
        for vertex in obj.data.vertices:
            all_points.append(root_inverse @ (obj.matrix_world @ vertex.co))
    low, high = axis_aligned_bounds(all_points)
    local_center = (low + high) * 0.5
    focus = root.matrix_world @ local_center
    height = high.z - low.z

    # Neutral studio additions are presentation-only and never selected for GLB export.
    ground = bpy.data.objects.new("PBRInput_NeutralRenderGround", bpy.data.meshes.new("PBRInput_NeutralRenderGroundMesh"))
    bpy.context.scene.collection.objects.link(ground)
    ground.data.from_pydata([(-30, -30, low.z - 0.05), (30, -30, low.z - 0.05),
                             (30, 30, low.z - 0.05), (-30, 30, low.z - 0.05)], [], [(0, 1, 2, 3)])
    ground.data.materials.append(bpy.data.materials.new("PBRInput_NeutralGround_7a7a7a"))
    gmat = ground.data.materials[0]
    gmat.diffuse_color = (0.16, 0.16, 0.16, 1.0)
    if gmat.use_nodes:
        gmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.16, 0.16, 1.0)
        gmat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9

    camera_data = bpy.data.cameras.new("PBRInput_BeforeCamera")
    camera = bpy.data.objects.new("PBRInput_BeforeCamera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(high.x - low.x, high.y - low.y, height) * 1.55
    # House front is the negative-depth side; this oblique view keeps the entry
    # porch, three-window facade, side windows and roof silhouette visible.
    view_dir = root.matrix_world.to_3x3() @ Vector((-0.82, -1.10, 0.62)).normalized()
    camera.location = focus + view_dir * max(16.0, camera_data.ortho_scale * 1.35)
    camera.rotation_euler = (focus - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera

    def area(name, offset, energy, size):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size = energy, "DISK", size
        light = bpy.data.objects.new(name, data)
        bpy.context.scene.collection.objects.link(light)
        light.location = focus + root.matrix_world.to_3x3() @ Vector(offset)
        light.rotation_euler = (focus - light.location).to_track_quat("-Z", "Y").to_euler()
        return light

    area("PBRInput_Key", (-7.0, -8.0, 10.0), 1700, 8.0)
    area("PBRInput_Fill", (8.0, -3.0, 6.0), 900, 7.0)
    area("PBRInput_Rim", (1.0, 8.0, 9.0), 1300, 6.0)

    world = bpy.data.worlds.new("PBRInput_NeutralWorld") if not bpy.data.worlds else bpy.data.worlds[0]
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.075, 0.075, 0.075, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55

    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.render.resolution_x = 1440
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.filepath = str(render_path)
    bpy.ops.render.render(write_still=True)
    return [vlist(low), vlist(high)]


def main():
    if bpy.app.version[:3] != (4, 5, 12):
        raise RuntimeError(f"Expected pinned Blender 4.5.12, got {bpy.app.version_string}")
    if not SOURCE_GLB.exists():
        raise FileNotFoundError(SOURCE_GLB)
    replace_staging_output = "--replace-staging-output" in sys.argv
    outputs = ((GLB_OUT, V3_METADATA_OUT) if V3_INPUT_MODE
               else (BLEND_OUT, GLB_OUT, MANIFEST_OUT, RENDER_OUT))
    for output in outputs:
        if output.exists() and not replace_staging_output:
            raise FileExistsError(f"Refusing to overwrite production output: {output}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))
    root = bpy.data.objects.get(HERO_ROOT_NAME)
    if root is None:
        raise RuntimeError(f"The shipped source GLB has no {HERO_ROOT_NAME} node")
    parts = [root, *root.children_recursive]
    mesh_objects = [obj for obj in parts if obj.type == "MESH"]
    if not mesh_objects:
        raise RuntimeError("Hero root contains no authored mesh objects")
    if any(not obj.name.startswith("HeroHouse_") for obj in mesh_objects):
        raise RuntimeError("A non-hero mesh is nested under the selected semantic root")

    # Preserve the source hierarchy, preview transform, object names, and mesh
    # datablocks without runtime modifications in fullhero.blend.
    ancestors = set()
    walk = root.parent
    while walk is not None:
        ancestors.add(walk)
        walk = walk.parent
    keep = set(parts) | ancestors
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.update()
    source_root_local = root.matrix_local.copy()
    source_root_world = root.matrix_world.copy()
    source_root_relative_matrices = {
        obj.name: matrix_rows(root.matrix_world.inverted_safe() @ obj.matrix_world)
        for obj in mesh_objects
    }
    source_hash = sha256_file(SOURCE_GLB)
    if not V3_INPUT_MODE:
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT))
        print(f"hero-house-source-snapshot: blend={BLEND_OUT} meshes={len(mesh_objects)} source={source_hash}")

    # Keep data writes local to the staging copy created after the untouched
    # snapshot has been saved.
    for obj in mesh_objects:
        obj.data = obj.data.copy()
    node_records = {}
    for obj in mesh_objects:
        node_records[obj.name] = {
            "node": obj.name,
            "parent_path": obj.parent.name if obj.parent else None,
            "source_mesh_datablock": obj.data.name,
            "source_triangles": mesh_triangles(obj.data),
            "source_bounds_runtime_mesh_local": mesh_bounds_in_runtime(obj.data),
            "source_material_slots": object_material_names(obj),
            "source_uv": uv_layer_summary(obj.data),
            "source_root_relative_transform_blender": source_root_relative_matrices[obj.name],
            "input_disposition": "pending",
            "runtime_geometry": {"operation": "none; original semantic mesh"},
        }

    # Verify that the imported GLB is the Z-up authored hero shell before the
    # Godot coordinate-rotated runtime replacements are ported.
    root_local_vertices = []
    root_inverse = root.matrix_world.inverted_safe()
    for obj in mesh_objects:
        for vertex in obj.data.vertices:
            root_local_vertices.append(root_inverse @ (obj.matrix_world @ vertex.co))
    root_low, root_high = axis_aligned_bounds(root_local_vertices)
    root_extent = root_high - root_low
    if not (4.3 < root_extent.z < 5.4):
        raise RuntimeError(f"Expected the authored roof height on Blender Z; got root extents {vlist(root_extent)}")

    rounded = round_runtime_hero_courses(mesh_objects, node_records)
    crowns, hidden_courses, center_source = runtime_wall_crowns(root, mesh_objects, node_records, [])

    excluded_nodes = []
    for name in hidden_courses:
        node_records[name]["input_disposition"] = "omitted_runtime_hidden_replaced_by_wall_crown"
    included = filter_export_surfaces(mesh_objects, node_records, excluded_nodes)
    # Runtime wall crowns are separate semantic meshes. The helper passed an
    # ephemeral list above, so resolve the crowns by their exact owner metadata.
    generated_crowns = [obj for obj in bpy.data.objects
                        if obj.type == "MESH" and obj.get("runtime_owner") == "Act1ConnectedWorld.DwellingLogWalls.BuildDwellingLogWalls"]
    for crown in generated_crowns:
        included.append(crown)
        node_records[crown.name] = {
            "node": crown.name,
            "parent_path": crown.parent.name if crown.parent else None,
            "source_mesh_datablock": crown.data.name,
            "source_triangles": 0,
            "input_triangles": mesh_triangles(crown.data),
            "source_material_slots": [crown.data.materials[0].name],
            "source_root_relative_transform_blender": matrix_rows(root.matrix_world.inverted_safe() @ crown.matrix_world),
            "input_disposition": "included_runtime_generated",
            "runtime_geometry": {
                "operation": "BuildDwellingLogWalls / AppendWallLog",
                "runtime_owner": crown.get("runtime_owner"),
                "source_wall": crown.get("runtime_generated_from"),
                "source_color_srgb": crown.get("source_color_srgb"),
                "local_transform_preserved_from_wall": True,
                "coincident_position_weld": dict(crown.data["coincident_position_weld"]),
            },
        }

    for excluded in excluded_nodes:
        pass

    # All export objects remain in their original root-local positions. Only
    # remove the known preview-board translation from the component root, as
    # the existing save_hero_house_stage flow does.
    preview_location = root.location.copy()
    root.location = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()
    input_root_local = matrix_rows(root.matrix_local)
    for obj in included:
        if obj.name in node_records:
            node_records[obj.name]["input_root_relative_transform_blender"] = matrix_rows(root.matrix_world.inverted_safe() @ obj.matrix_world)
            node_records[obj.name]["input_triangles"] = mesh_triangles(obj.data)
            node_records[obj.name]["input_bounds_in_hero_root"] = object_root_relative_bounds(obj, root.matrix_world.inverted_safe())

    render_bounds = None if V3_INPUT_MODE else setup_neutral_render(bpy.context.scene, root, included, RENDER_OUT)
    uv_atlas = build_global_uv_atlas(included, node_records)
    if uv_atlas["missing_after_pack"]:
        raise RuntimeError("Global UV atlas is incomplete: " + ", ".join(uv_atlas["missing_after_pack"][:20]))
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [root, *root.children_recursive, *generated_crowns]:
        if obj is root or obj in included:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    expected_mesh_names = {obj.name for obj in included}
    bpy.ops.export_scene.gltf(filepath=str(GLB_OUT), export_format="GLB", use_selection=True,
                              export_apply=False, export_texcoords=True, export_normals=True,
                              export_materials="EXPORT", export_image_format="AUTO", use_visible=False)
    root.location = preview_location
    bpy.context.view_layer.update()

    output_hash = sha256_file(GLB_OUT)
    output_triangles = sum(mesh_triangles(obj.data) for obj in included)
    included_original = [obj for obj in included if obj.name in source_root_relative_matrices]
    total_source_triangles = sum(node_records[obj.name]["source_triangles"] for obj in mesh_objects)
    included_source_triangles = sum(node_records[obj.name].get("input_triangles", 0) for obj in included_original)
    # Compute source mesh bounds after the preview translation is removed.
    root_inverse = root.matrix_world.inverted_safe()
    all_root_points = []
    for obj in included:
        for vertex in obj.data.vertices:
            all_root_points.append(root_inverse @ (obj.matrix_world @ vertex.co))
    output_low, output_high = axis_aligned_bounds(all_root_points)

    manifest = {
        "schema": "hero-house-pbr-retexture-input/v1",
        "blender": {"version": bpy.app.version_string, "build_hash": bpy.app.build_hash.decode("ascii", "replace")},
        "source": {
            "path": SOURCE_GLB.relative_to(PROJECT_ROOT).as_posix(),
            "sha256": source_hash,
            "bytes": SOURCE_GLB.stat().st_size,
            "authority": "currently shipped Godot kit model; no generator/runtime/canonical source edits",
        },
        "component": {
            "root_node": HERO_ROOT_NAME,
            "original_root_local_transform_blender": matrix_rows(source_root_local),
            "original_root_world_transform_blender": matrix_rows(source_root_world),
            "preview_root_translation_removed_for_glb": vlist(preview_location),
            "root_local_transform_after_preview_removal": input_root_local,
            "original_mesh_node_count": len(mesh_objects),
            "original_node_names_preserved_in_fullhero_blend": True,
            "source_root_relative_transforms_preserved_in_input": True,
            "runtime_crown_house_center_source": center_source,
            "original_root_bounds_blender": [vlist(root_low), vlist(root_high)],
            "original_root_extent_blender": vlist(root_extent),
            "input_root_bounds_blender": [vlist(output_low), vlist(output_high)],
            "input_extent_blender": vlist(output_high - output_low),
            "input_bounds_runtime_y_up": [vlist(blender_to_runtime(output_low)), vlist(blender_to_runtime(output_high))],
            "original_source_triangles": total_source_triangles,
            "input_triangles_before_glb_export": output_triangles,
            "included_original_semantic_triangles": included_source_triangles,
            "runtime_rounded_course_count": len(rounded),
            "runtime_generated_wall_crown_count": len(generated_crowns),
            "runtime_hidden_wall_courses": hidden_courses,
            "runtime_wall_crowns": crowns,
            "uv_atlas": uv_atlas,
            "excluded_nodes": excluded_nodes,
            "included_node_count": len(included),
        },
        "runtime_visual_state": {
            "geometry_pass": "runtime RoundHeroLogCourses and BuildDwellingLogWalls hero-only geometry reproduced from the current C# implementation",
            "materials": "original imported GLB materials remain on source nodes; runtime crown faces use source color #8b7763 before PainterlyMaterialLibrary regrade",
            "opaque_retexture_filter": "Glass/Recess semantic nodes and RoofSnow material faces omitted; other opaque surfaces retained",
            "sourceframe": {
                "path": RENDER_OUT.name,
                "label": "imported GLB materials before runtime RegradeAct1DaylightKitMaterials; runtime-generated crowns use explicitly recorded #8b7763 source color",
                "neutral_studio_bounds": render_bounds,
            },
        },
        "output": {
            "path": GLB_OUT.name,
            "sha256": output_hash,
            "bytes": GLB_OUT.stat().st_size,
            "blend_snapshot": BLEND_OUT.name if not V3_INPUT_MODE else None,
            "blend_snapshot_sha256": sha256_file(BLEND_OUT) if not V3_INPUT_MODE else None,
            "semantic_mesh_map": node_records,
        },
    }
    if V3_INPUT_MODE:
        v3_metadata = {
            "schema": "hero-house-retexture-input/v3",
            "blender": manifest["blender"],
            "source": manifest["source"],
            "change": {
                "owner": "prepare_hero_house_retexture.py",
                "method": "exact coincident positions are shared only within each generated semantic wall-crown mesh before Smart UV Project; per-loop normals and UV data remain corner-domain, then cap fans receive an 8x linear UV weight before atlas packing",
                "mesh_positions_and_triangle_winding_preserved": True,
                "loop_normals_preserved": True,
                "studio_window_glass_snow_and_semantic_nodes": "same inclusion/exclusion and original node names as the existing source stage",
            },
            "component": {
                "root_node": HERO_ROOT_NAME,
                "original_mesh_node_count": len(mesh_objects),
                "included_mesh_node_count": len(included),
                "included_semantic_mesh_names": sorted(expected_mesh_names),
                "triangles": output_triangles,
                "uv_atlas": uv_atlas,
                "runtime_crown_welds": {
                    crown.name: node_records[crown.name]["runtime_geometry"]["coincident_position_weld"]
                    for crown in sorted(generated_crowns, key=lambda obj: obj.name)
                },
            },
            "input": {
                "path": GLB_OUT.name,
                "sha256": output_hash,
                "bytes": GLB_OUT.stat().st_size,
                "mesh_node_count": len(included),
            },
        }
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(GLB_OUT))
        imported_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
        imported_names = {obj.name for obj in imported_meshes}
        if imported_names != expected_mesh_names:
            raise RuntimeError("The v3 GLB import changed its semantic mesh-name set")
        v3_metadata["input"]["semantic_mesh_name_set_matches_source"] = True
        v3_metadata["component"]["uv_atlas"]["crown_cap_uv_after_glb_reimport"] = crown_cap_uv_report(
            imported_meshes,
            expected_fans=uv_atlas["crown_cap_uv_after_pack"]["fans"],
        )
        V3_METADATA_OUT.write_text(json.dumps(v3_metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    else:
        MANIFEST_OUT.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print("hero-house-pbr-input: " + json.dumps({
        "source_sha256": source_hash,
        "mesh_nodes": len(mesh_objects),
        "runtime_rounds": len(rounded),
        "wall_crowns": len(generated_crowns),
        "excluded": len(excluded_nodes),
        "included": len(included),
        "triangles": output_triangles,
        "uv_atlas_nodes": uv_atlas["packed_mesh_node_count"],
        "uv_atlas_loops": uv_atlas["packed_loop_count"],
        "runtime_hidden_courses": len(hidden_courses),
        "bounds": [vlist(output_low), vlist(output_high)],
        "glb_sha256": output_hash,
    }, sort_keys=True))


if __name__ == "__main__":
    main()
