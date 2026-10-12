"""Build an original, neutral family-book asset stage for URMAN.

Run with:
  blender --background --python assets/source/blender/photoworlds/urman_family_book.py \
    -- --output-dir /private/tmp/urman-family-book-YYYYMMDD

The default pose is a closed, centred 220 x 285 x 38 mm volume. The source is
procedural Blender geometry with two existing URMAN surface maps; it contains
no title lettering, portrait, or copied contemporary cover art. Actions are
finite pose clips for a later runtime owner, not an interaction or hand rig.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


BOOK_W = 0.220
BOOK_D = 0.285
BOOK_T = 0.038
BOARD_T = 0.004
HINGE_X = -0.1125  # 2.5 mm outside the cosmetic board edge for a 5 mm open gutter.
TILE_METRES = 0.080
FPS = 24


def parse_args():
    tail = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True, type=Path)
    return parser.parse_args(tail)


def project_root():
    return Path(__file__).resolve().parents[4]


def srgb_channel_to_linear(value):
    value = value / 255.0
    if value <= 0.04045:
        return value / 12.92
    return ((value + 0.055) / 1.055) ** 2.4


def hex_rgb(value):
    value = value.lstrip("#")
    return tuple(srgb_channel_to_linear(int(value[index:index + 2], 16)) for index in (0, 2, 4))


def hash_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.actions):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = 36
    scene.frame_set(0)
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.world.color = (0.08, 0.08, 0.08)


def make_material(name, color_hex, texture_path, texture_strength, roughness):
    material = bpy.data.materials.new(name=name)
    material.diffuse_color = (*hex_rgb(color_hex), 1.0)
    material.use_nodes = True
    material["semantic_surface"] = name
    material["base_color_srgb"] = f"#{color_hex.lower()}"
    material["tile_metres"] = TILE_METRES
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (700, 0)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (420, 0)
    shader.inputs["Base Color"].default_value = (*hex_rgb(color_hex), 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = 0.0
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    image = bpy.data.images.load(str(texture_path), check_existing=True)
    image.name = f"{name}_source_map"
    image.colorspace_settings.name = "sRGB"
    image.pack()
    texcoord = nodes.new("ShaderNodeTexCoord")
    texcoord.location = (-800, 0)
    uv_scale = nodes.new("ShaderNodeVectorMath")
    uv_scale.operation = "MULTIPLY"
    uv_scale.inputs[1].default_value = (1.0 / TILE_METRES, 1.0 / TILE_METRES, 1.0)
    uv_scale.location = (-600, 0)
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.extension = "REPEAT"
    texture.location = (-380, 0)
    grayscale = nodes.new("ShaderNodeRGBToBW")
    grayscale.location = (-160, -160)
    value_range = nodes.new("ShaderNodeMapRange")
    value_range.clamp = True
    value_range.inputs["From Min"].default_value = 0.0
    value_range.inputs["From Max"].default_value = 1.0
    value_range.inputs["To Min"].default_value = 1.0 - texture_strength
    value_range.inputs["To Max"].default_value = 1.0 + texture_strength
    value_range.location = (30, -160)
    multiplier = nodes.new("ShaderNodeVectorMath")
    multiplier.operation = "MULTIPLY"
    multiplier.inputs[0].default_value = hex_rgb(color_hex)
    multiplier.location = (220, 0)
    factor_vector = nodes.new("ShaderNodeCombineXYZ")
    factor_vector.location = (30, -340)
    links = material.node_tree.links
    links.new(texcoord.outputs["UV"], uv_scale.inputs[0])
    links.new(uv_scale.outputs["Vector"], texture.inputs["Vector"])
    links.new(texture.outputs["Color"], grayscale.inputs["Color"])
    links.new(grayscale.outputs["Val"], value_range.inputs["Value"])
    for axis in ("X", "Y", "Z"):
        links.new(value_range.outputs["Result"], factor_vector.inputs[axis])
    links.new(factor_vector.outputs["Vector"], multiplier.inputs[1])
    links.new(multiplier.outputs["Vector"], shader.inputs["Base Color"])
    return material, image


def recalc_normals(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    if bm.faces:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def assign_metric_uv(mesh, tile_metres=TILE_METRES):
    uv_layer = mesh.uv_layers.active or mesh.uv_layers.new(name="UVMap")
    for polygon in mesh.polygons:
        normal = polygon.normal
        axis = max(range(3), key=lambda index: abs(normal[index]))
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            if axis == 2:
                u, v = vertex.x, vertex.y
            elif axis == 1:
                u, v = vertex.x, vertex.z
            else:
                u, v = vertex.y, vertex.z
            uv_layer.data[loop_index].uv = (u, v)
    mesh["uv_units"] = "metres; shader repeats one source tile every 0.080 m"


def set_material(obj, material):
    obj.data.materials.clear()
    obj.data.materials.append(material)


def new_empty(name, location=(0.0, 0.0, 0.0), parent=None, display="PLAIN_AXES", size=0.012):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.empty_display_type = display
    obj.empty_display_size = size
    obj.location = location
    if parent is not None:
        obj.parent = parent
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.location = location
    return obj


def make_bevel_box(name, dimensions, center, material, bevel, parent):
    """Create a beveled box at a parent-local center with identity parent inverse."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.0))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0.0:
        modifier = obj.modifiers.new("Soft cloth-board edges", "BEVEL")
        modifier.width = min(bevel, min(dimensions) * 0.45)
        modifier.segments = 3
        modifier.limit_method = "ANGLE"
        modifier.angle_limit = math.radians(28.0)
        modifier.harden_normals = True
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    assign_metric_uv(obj.data)
    set_material(obj, material)
    obj.parent = parent
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.location = center
    obj["tile_metres"] = TILE_METRES
    return obj


def make_mesh_object(name, vertices, faces, material, parent, uv=True):
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    recalc_normals(mesh)
    if uv:
        assign_metric_uv(mesh)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.location = (0.0, 0.0, 0.0)
    set_material(obj, material)
    obj["tile_metres"] = TILE_METRES
    return obj


def make_bowed_cover(name, parent, material, center_x, center_z, width, depth, thickness, bow,
                     columns=11, rows=15):
    x0, x1 = center_x - width * 0.5, center_x + width * 0.5
    y0, y1 = -depth * 0.5, depth * 0.5
    z_bottom = center_z - thickness * 0.5
    z_top_edge = center_z + thickness * 0.5
    vertices = []
    for layer in (0, 1):
        for row in range(rows + 1):
            y = y0 + (y1 - y0) * row / rows
            v = (2.0 * row / rows) - 1.0
            for col in range(columns + 1):
                x = x0 + (x1 - x0) * col / columns
                u = (2.0 * col / columns) - 1.0
                if layer == 0:
                    z = z_bottom
                else:
                    z = z_top_edge - bow * (1.0 - u * u) * (1.0 - v * v)
                vertices.append((x, y, z))
    layer_size = (rows + 1) * (columns + 1)
    faces = []
    for row in range(rows):
        for col in range(columns):
            a = row * (columns + 1) + col
            b = a + 1
            c = a + columns + 2
            d = a + columns + 1
            faces.append((layer_size + a, layer_size + b, layer_size + c, layer_size + d))
            faces.append((d, c, b, a))
    perimeter = []
    perimeter.extend(range(0, columns + 1))
    perimeter.extend(row * (columns + 1) + columns for row in range(1, rows + 1))
    perimeter.extend(rows * (columns + 1) + col for col in range(columns - 1, -1, -1))
    perimeter.extend(row * (columns + 1) for row in range(rows - 1, 0, -1))
    for index, low in enumerate(perimeter):
        high = perimeter[(index + 1) % len(perimeter)]
        faces.append((low, high, layer_size + high, layer_size + low))
    obj = make_mesh_object(name, vertices, faces, material, parent)
    obj["physical_depression_m"] = bow
    obj["board_dimensions_m"] = [width, depth, thickness]
    return obj


def make_extruded_spine(name, profile_xz, y_start, y_end, material, parent):
    count = len(profile_xz)
    vertices = [(x, y_start, z) for x, z in profile_xz]
    vertices += [(x, y_end, z) for x, z in profile_xz]
    faces = [tuple(range(count - 1, -1, -1)), tuple(range(count, 2 * count))]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, next_index, count + next_index, count + index))
    return make_mesh_object(name, vertices, faces, material, parent)


def make_page_leaf(name, parent, material, width, depth, thickness, bow,
                   columns=12, rows=16):
    x0, x1 = 0.006, 0.006 + width
    y0, y1 = -depth * 0.5, depth * 0.5
    zhalf = thickness * 0.5
    vertices = []
    for side in (1.0, -1.0):
        for row in range(rows + 1):
            y = y0 + (y1 - y0) * row / rows
            v = 2.0 * row / rows - 1.0
            for col in range(columns + 1):
                x = x0 + (x1 - x0) * col / columns
                u = 2.0 * col / columns - 1.0
                camber = bow * (1.0 - u * u) * (1.0 - v * v)
                z = side * zhalf + camber
                vertices.append((x, y, z))
    layer_size = (rows + 1) * (columns + 1)
    faces = []
    for row in range(rows):
        for col in range(columns):
            a = row * (columns + 1) + col
            b = a + 1
            c = a + columns + 2
            d = a + columns + 1
            faces.append((a, b, c, d))
            faces.append((layer_size + d, layer_size + c, layer_size + b, layer_size + a))
    perimeter = []
    perimeter.extend(range(0, columns + 1))
    perimeter.extend(row * (columns + 1) + columns for row in range(1, rows + 1))
    perimeter.extend(rows * (columns + 1) + col for col in range(columns - 1, -1, -1))
    perimeter.extend(row * (columns + 1) for row in range(rows - 1, 0, -1))
    for index, low in enumerate(perimeter):
        high = perimeter[(index + 1) % len(perimeter)]
        faces.append((low, layer_size + low, layer_size + high, high))
    obj = make_mesh_object(name, vertices, faces, material, parent)
    obj["physical_page_thickness_m"] = thickness
    obj["page_camber_m"] = bow
    return obj


def make_page_layer_relief(name, material, parent):
    vertices, faces = [], []
    line_count = 12
    for index in range(line_count):
        z = -0.0108 + index * (0.0216 / (line_count - 1))
        x0, x1 = 0.0988, 0.1000
        y0, y1 = -0.121, 0.121
        dz = 0.00009
        base = len(vertices)
        vertices.extend(((x0, y0, z - dz), (x1, y0, z - dz), (x1, y1, z - dz), (x0, y1, z - dz),
                         (x0, y0, z + dz), (x1, y0, z + dz), (x1, y1, z + dz), (x0, y1, z + dz)))
        faces.extend(((base + 4, base + 5, base + 6, base + 7), (base + 3, base + 2, base + 1, base),
                      (base, base + 1, base + 5, base + 4), (base + 1, base + 2, base + 6, base + 5),
                      (base + 2, base + 3, base + 7, base + 6), (base + 3, base, base + 4, base + 7)))
    obj = make_mesh_object(name, vertices, faces, material, parent)
    obj["layer_relief_count"] = line_count
    return obj


def make_l_corner(name, sx, sy, paper_material, page_parent, card_center_x, card_center_y,
                  card_half_x, card_half_y, arm=0.013, rail=0.0028,
                  z_bottom=0.00065, thickness=0.00048):
    x0 = card_center_x + sx * card_half_x
    y0 = card_center_y + sy * card_half_y
    outline = [(0.0, 0.0), (sx * arm, 0.0), (sx * arm, sy * rail),
               (sx * rail, sy * rail), (sx * rail, sy * arm), (0.0, sy * arm)]
    vertices = [(x0 + x, y0 + y, z_bottom) for x, y in outline]
    vertices += [(x0 + x, y0 + y, z_bottom + thickness) for x, y in outline]
    count = len(outline)
    faces = [tuple(range(count - 1, -1, -1)), tuple(range(count, 2 * count))]
    for index in range(count):
        nxt = (index + 1) % count
        faces.append((index, nxt, count + nxt, count + index))
    obj = make_mesh_object(name, vertices, faces, paper_material, page_parent)
    obj["part_role"] = "separate folded photo-corner pocket"
    return obj


def add_curve(name, points, bevel_depth, material, parent, cyclic=False):
    curve = bpy.data.curves.new(f"{name}_Curve", type="CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = bevel_depth
    curve.bevel_resolution = 2
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, co in zip(spline.points, points):
        point.co = (*co, 1.0)
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    obj.parent = parent
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.location = (0.0, 0.0, 0.0)
    obj["tile_metres"] = TILE_METRES
    return obj


def key_clip(obj, name, keys, contract):
    """Create a named, single-control NLA clip with explicit finite keys."""
    action = bpy.data.actions.new(name=name)
    animation_data = obj.animation_data_create()
    animation_data.action = action
    for frame, state in keys:
        if "location" in state:
            obj.location = Vector(state["location"])
            obj.keyframe_insert(data_path="location", frame=frame, group=name)
        if "rotation_euler" in state:
            obj.rotation_euler = state["rotation_euler"]
            obj.keyframe_insert(data_path="rotation_euler", frame=frame, group=name)
    animation_data.action = None
    track = animation_data.nla_tracks.new()
    track.name = name
    track.mute = False
    strip = track.strips.new(name, keys[0][0], action)
    strip.action_frame_start = keys[0][0]
    strip.action_frame_end = keys[-1][0]
    strip.blend_type = "REPLACE"
    strip.extrapolation = "NOTHING"
    strip.influence = 1.0
    strip.use_auto_blend = False
    return {"name": name, "node": obj.name, "action": action.name,
            "start_frame": keys[0][0], "end_frame": keys[-1][0],
            "duration_seconds": round((keys[-1][0] - keys[0][0]) / FPS, 4),
            "contract": contract}


def build_asset(output_dir):
    root = project_root()
    cloth_path = root / "game/assets/textures/painterly/urman_t01_v02_basecolor.png"
    paper_path = root / "game/assets/textures/painterly/book_paper_fibers_v1_albedo.png"
    for texture_path in (cloth_path, paper_path):
        if not texture_path.is_file():
            raise FileNotFoundError(texture_path)
    output_dir = output_dir.expanduser().resolve()
    if output_dir.exists():
        raise FileExistsError(f"Refusing to overwrite stage: {output_dir}")
    output_dir.mkdir(parents=True, exist_ok=False)

    reset_scene()
    cloth, cloth_image = make_material("cloth", "674b45", cloth_path, 0.10, 0.91)
    paper, paper_image = make_material("paper", "d6ccb3", paper_path, 0.08, 0.84)
    scene = bpy.context.scene
    book = new_empty("FamilyBook_Root", display="CUBE", size=0.025)
    book["asset_role"] = "original neutral family literary volume; no title or portrait imagery"
    book["closed_dimensions_m"] = [BOOK_W, BOOK_D, BOOK_T]
    book["source_axis_contract"] = "Blender X=width, Y=depth, Z=thickness; glTF exports Y-up"

    # The fixed back board and page stack stay centred within the existing
    # table footprint. The upper cover has its own spine-axis hinge.
    make_bevel_box("BackClothBoard", (BOOK_W, BOOK_D, BOARD_T), (0.0, 0.0, -0.017), cloth, 0.0015, book)
    make_bevel_box("BackBoardInnerEndpaper", (0.198, 0.263, 0.0006), (0.0, 0.0, -0.0145), paper, 0.001, book)
    make_bevel_box("RecessedPageBlock", (0.200, 0.265, 0.0266), (0.0, 0.0, 0.0), paper, 0.0012, book)
    make_page_layer_relief("PageBlock_ForeEdgeLayerRelief", paper, book)

    cover_pivot = new_empty("FrontCoverHinge", location=(HINGE_X, 0.0, 0.0), parent=book)
    cover_pivot["hinge_axis"] = "local Y; spine edge; positive Y rotation opens cover to left"
    cover_pivot["pivot_offset_m"] = 0.0025
    cover_core_center_x = 0.1125
    cover_core_center_z = 0.0166
    make_bevel_box("FrontClothBoard_Core", (BOOK_W, BOOK_D, 0.0032),
                   (cover_core_center_x, 0.0, cover_core_center_z), cloth, 0.0015, cover_pivot)
    make_bowed_cover("FrontClothBoard", cover_pivot, cloth,
                     cover_core_center_x, 0.0186, BOOK_W, BOOK_D, 0.0008, 0.0007)
    make_bevel_box("FrontCover_InnerEndpaper", (0.198, 0.263, 0.00025),
                   (cover_core_center_x, 0.0, 0.01475), paper, 0.001, cover_pivot)

    # A rounded, continuous cloth spine and restrained endbands give the book
    # real thickness and an identifiable binding edge without a flat card prop.
    spine_profile = [(-0.101, 0.019), (-0.105, 0.0186), (-0.1085, 0.0165),
                     (-0.110, 0.012), (-0.110, 0.006), (-0.110, 0.0),
                     (-0.110, -0.006), (-0.110, -0.012), (-0.1085, -0.0165),
                     (-0.105, -0.0186), (-0.101, -0.019), (-0.096, -0.019),
                     (-0.093, -0.014), (-0.093, 0.014), (-0.096, 0.019)]
    make_extruded_spine("ClothSpine", spine_profile, -BOOK_D * 0.5, BOOK_D * 0.5, cloth, book)
    make_extruded_spine("SpineHeadband_Upper", spine_profile, BOOK_D * 0.5 - 0.004, BOOK_D * 0.5, cloth, book)
    make_extruded_spine("SpineHeadband_Lower", spine_profile, -BOOK_D * 0.5, -BOOK_D * 0.5 + 0.004, cloth, book)
    for side in (-1.0, 1.0):
        y = side * 0.124
        add_curve(f"SpineSewingChannel_{'Head' if side > 0 else 'Tail'}",
                  [(-0.1092, y, 0.008), (-0.1095, y, 0.004), (-0.1095, y, -0.004), (-0.1092, y, -0.008)],
                  0.00020, cloth, book)

    # A paper leaf on the right page carries a blank album insert and four
    # independent folded corner pockets. The insert is text-free and face-free.
    page_pivot = new_empty("AlbumPageTurnHinge", location=(HINGE_X, 0.0, 0.01345), parent=book)
    page_pivot["hinge_axis"] = "local Y; bound page leaf turns around spine"
    make_page_leaf("AlbumPageLeaf", page_pivot, paper, width=0.210, depth=0.264,
                   thickness=0.00035, bow=0.00010)
    card_center_x = 0.110
    card_center_y = 0.0
    card_half_x = 0.075
    card_half_y = 0.050
    card_mount = new_empty("PhotoInsertMount", location=(card_center_x, card_center_y, 0.0006),
                           parent=page_pivot, display="CUBE", size=0.007)
    card_mount["part_role"] = "static 150 by 100 mm insert rest position on the page"
    card_motion = new_empty("PhotoInsertMotionControl", location=(0.0, 0.0, 0.0),
                            parent=card_mount, display="CIRCLE", size=0.006)
    card_motion["part_role"] = "center pivot for bounded mount, inspect, and flip clips"
    make_bevel_box("AlbumInsert", (card_half_x * 2.0, card_half_y * 2.0, 0.00065),
                   (0.0, 0.0, 0.0), paper, 0.0007, card_motion)
    corners = []
    for label, sx, sy in (("NearLeft", -1, -1), ("NearRight", 1, -1),
                          ("FarLeft", -1, 1), ("FarRight", 1, 1)):
        corners.append(make_l_corner(f"PhotoCorner_{label}", sx, sy, paper, page_pivot,
                                     card_center_x, card_center_y, card_half_x, card_half_y,
                                     z_bottom=0.00082, thickness=0.00026))

    # Named empty controls create one transform-only NLA strip each, so each
    # glTF animation stays finite and portable; no visibility keys or rig claim.
    clips = []
    clips.append(key_clip(cover_pivot, "open", [
        (1, {"rotation_euler": (0.0, 0.0, 0.0)}),
        (31, {"rotation_euler": (0.0, math.radians(-180.0), 0.0)})], {
            "precondition": "closed/rest pose",
            "start": {"cover_hinge_y_degrees": 0.0},
            "end": {"cover_hinge_y_degrees": -180.0},
            "bounded_range_degrees": [-180.0, 0.0],
            "support": "front cloth board rotates on the spine-side Y hinge"
        }))
    clips.append(key_clip(cover_pivot, "close", [
        (1, {"rotation_euler": (0.0, math.radians(-180.0), 0.0)}),
        (31, {"rotation_euler": (0.0, 0.0, 0.0)})], {
            "precondition": "open pose at -180 degrees",
            "start": {"cover_hinge_y_degrees": -180.0},
            "end": {"cover_hinge_y_degrees": 0.0},
            "bounded_range_degrees": [-180.0, 0.0],
            "support": "same physical spine hinge as open"
        }))
    clips.append(key_clip(page_pivot, "turn", [
        (1, {"rotation_euler": (0.0, 0.0, 0.0)}),
        (12, {"rotation_euler": (0.0, math.radians(-50.0), 0.0)}),
        (22, {"rotation_euler": (0.0, math.radians(-130.0), 0.0)}),
        (31, {"rotation_euler": (0.0, math.radians(-180.0), 0.0)})], {
            "precondition": "book open; no hand is represented",
            "start": {"page_hinge_y_degrees": 0.0},
            "end": {"page_hinge_y_degrees": -180.0},
            "bounded_range_degrees": [-180.0, 0.0],
            "support": "the thin curved album leaf rotates around the spine and lifts above the page block"
        }))
    clips.append(key_clip(card_motion, "mount", [
        (1, {"location": (0.012, 0.0, 0.0294),
             "rotation_euler": (0.0, math.radians(-16.0), 0.0)}),
        (13, {"location": (0.003, 0.0, 0.0124),
              "rotation_euler": (0.0, math.radians(-5.0), 0.0)}),
        (25, {"location": (0.0, 0.0, 0.0),
              "rotation_euler": (0.0, 0.0, 0.0)})], {
            "precondition": "open pose; inspection-space insert ready above the page",
            "start": {"insert_local_height_m": 0.030, "rotation_y_degrees": -16.0},
            "end": {"insert_local_height_m": 0.0006, "rotation_y_degrees": 0.0},
            "bounded_range_m": {"insert_local_height": [0.0006, 0.030]},
            "support": "blank insert lowers into four fixed corner pockets"
        }))
    clips.append(key_clip(card_motion, "inspect", [
        (1, {"location": (0.0, 0.0, 0.0),
             "rotation_euler": (0.0, 0.0, 0.0)}),
        (12, {"location": (0.0, 0.0, 0.0244),
              "rotation_euler": (math.radians(-8.0), math.radians(-12.0), 0.0)}),
        (18, {"location": (0.0, 0.0, 0.0244),
              "rotation_euler": (math.radians(-8.0), math.radians(-12.0), 0.0)}),
        (30, {"location": (0.0, 0.0, 0.0),
              "rotation_euler": (0.0, 0.0, 0.0)})], {
            "precondition": "open pose; insert mounted",
            "start": {"insert_local_height_m": 0.0006, "rotation_degrees": [0.0, 0.0, 0.0]},
            "inspect_hold": {"insert_local_height_m": 0.025, "rotation_degrees": [-8.0, -12.0, 0.0]},
            "end": {"insert_local_height_m": 0.0006, "rotation_degrees": [0.0, 0.0, 0.0]},
            "bounded_range_m": {"insert_local_height": [0.0006, 0.025]},
            "support": "the insert lifts clear of its pockets and returns to the page"
        }))
    clips.append(key_clip(card_motion, "flip", [
        (1, {"location": (0.0, 0.0, 0.0),
             "rotation_euler": (0.0, 0.0, 0.0)}),
        (8, {"location": (0.0, 0.0, 0.0799),
             "rotation_euler": (0.0, 0.0, 0.0)}),
        (15, {"location": (0.0, 0.0, 0.0799),
              "rotation_euler": (0.0, math.radians(180.0), 0.0)}),
        (20, {"location": (0.0, 0.0, 0.0799),
              "rotation_euler": (0.0, math.radians(180.0), 0.0)}),
        (27, {"location": (0.0, 0.0, 0.0),
              "rotation_euler": (0.0, 0.0, 0.0)})], {
            "precondition": "open pose; mounted insert lifted clear of the fixed photo corners",
            "start": {"insert_local_height_m": 0.0005, "rotation_y_degrees": 0.0},
            "flip_clearance": {"insert_local_height_m": 0.0805, "rotation_y_degrees": 180.0},
            "end": {"insert_local_height_m": 0.0006, "rotation_y_degrees": 0.0},
            "bounded_range_m": {"insert_local_height": [0.0006, 0.0805]},
            "bounded_range_degrees": [0.0, 180.0],
            "support": "the 150 by 100 mm blank insert lifts above its corners, flips around its center, and returns face up"
        }))
    clips.append(key_clip(cover_pivot, "rest", [
        (1, {"rotation_euler": (0.0, math.radians(-2.0), 0.0)}),
        (5, {"rotation_euler": (0.0, math.radians(-0.35), 0.0)}),
        (10, {"rotation_euler": (0.0, 0.0, 0.0)})], {
            "precondition": "closed cover nearly settled",
            "start": {"cover_hinge_y_degrees": -2.0},
            "apex": {"cover_hinge_y_degrees": -0.35},
            "end": {"cover_hinge_y_degrees": 0.0},
            "bounded_range_degrees": [-2.0, 0.0],
            "support": "small cloth-board rebound damps into the closed pose"
        }))
    clips.append(key_clip(book, "return", [
        (1, {"location": (0.0, 0.0, 0.024),
             "rotation_euler": (math.radians(-9.0), 0.0, 0.0)}),
        (13, {"location": (0.0, 0.0, 0.010),
              "rotation_euler": (math.radians(-4.0), 0.0, 0.0)}),
        (25, {"location": (0.0, 0.0, 0.0), "rotation_euler": (0.0, 0.0, 0.0)})], {
            "precondition": "closed book held in the bounded inspection pose",
            "start": {"root_lift_m": 0.024, "root_pitch_x_degrees": -9.0},
            "end": {"root_lift_m": 0.0, "root_pitch_x_degrees": 0.0},
            "bounded_range": {"root_lift_m": [0.0, 0.024], "root_pitch_x_degrees": [-9.0, 0.0]},
            "support": "whole volume returns to its centred tabletop rest transform"
        }))

    # NLA strips never hold outside their finite spans. Restore every animated
    # control explicitly so the ordinary GLB node transforms are the closed,
    # centered rest pose rather than the last keyed pose from clip authoring.
    for control, location in (
        (book, (0.0, 0.0, 0.0)),
        (cover_pivot, (HINGE_X, 0.0, 0.0)),
        (page_pivot, (HINGE_X, 0.0, 0.01345)),
        (card_motion, (0.0, 0.0, 0.0)),
    ):
        control.location = location
        control.rotation_euler = (0.0, 0.0, 0.0)
        control.scale = (1.0, 1.0, 1.0)
    scene.frame_set(0)
    scene.render.fps = FPS
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    bounds_points = []
    object_bounds = []
    for obj in scene.objects:
        if obj.type not in {"MESH", "CURVE"}:
            continue
        evaluated = obj.evaluated_get(depsgraph)
        evaluated_mesh = evaluated.to_mesh()
        points = [evaluated.matrix_world @ vertex.co for vertex in evaluated_mesh.vertices]
        bounds_points.extend(points)
        object_bounds.append((obj.name,
                              [min(point[axis] for point in points) for axis in range(3)],
                              [max(point[axis] for point in points) for axis in range(3)]))
        evaluated.to_mesh_clear()
    bounds_min = [min(point[axis] for point in bounds_points) for axis in range(3)]
    bounds_max = [max(point[axis] for point in bounds_points) for axis in range(3)]
    bounds_size = [bounds_max[axis] - bounds_min[axis] for axis in range(3)]
    expected_bounds = [BOOK_W, BOOK_D, BOOK_T]
    if any(abs(actual - expected) > 0.0001 for actual, expected in zip(bounds_size, expected_bounds)):
        offenders = [(name, minimum, maximum) for name, minimum, maximum in object_bounds
                     if any(minimum[axis] < (-expected_bounds[axis] * 0.5 - 0.0001)
                            or maximum[axis] > (expected_bounds[axis] * 0.5 + 0.0001)
                            for axis in range(3))]
        transform_chain = []
        current = bpy.data.objects.get("AlbumInsert")
        while current is not None:
            evaluated = current.evaluated_get(depsgraph)
            transform_chain.append({
                "name": current.name,
                "parent": current.parent.name if current.parent else None,
                "location": tuple(current.location),
                "evaluated_location": tuple(evaluated.location),
                "world_translation": tuple(evaluated.matrix_world.translation),
                "parent_inverse_translation": tuple(current.matrix_parent_inverse.translation),
            })
            current = current.parent
        raise RuntimeError(f"Closed-pose AABB {bounds_size} does not match {expected_bounds}; "
                           f"out-of-volume objects={offenders}; insert transform chain={transform_chain}")
    bpy.ops.file.pack_all()

    blend_path = output_dir / "urman_family_book.blend"
    glb_path = output_dir / "urman_family_book.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    export_kwargs = {
        "filepath": str(glb_path),
        "export_format": "GLB",
        "use_selection": False,
        "export_animations": True,
        "export_nla_strips": True,
        "export_force_sampling": True,
        "export_current_frame": False,
        "export_yup": True,
    }
    bpy.ops.export_scene.gltf(**export_kwargs)
    if not glb_path.is_file():
        raise RuntimeError("glTF exporter did not create the GLB")

    glb_bytes = glb_path.read_bytes()
    if glb_bytes[:4] != b"glTF":
        raise RuntimeError("Export is not a GLB")
    json_length, json_type = struct.unpack_from("<II", glb_bytes, 12)
    if json_type != 0x4E4F534A:
        raise RuntimeError("Export GLB has no JSON chunk")
    gltf = json.loads(glb_bytes[20:20 + json_length])
    exported_animations = gltf.get("animations", [])
    exported_names = [animation.get("name") for animation in exported_animations]
    expected_names = [clip["name"] for clip in clips]
    if len(exported_animations) != 8 or set(exported_names) != set(expected_names):
        raise RuntimeError(f"Expected exactly eight named glTF clips; got {exported_names}")
    for animation in exported_animations:
        if not animation.get("channels") or not animation.get("samplers"):
            raise RuntimeError(f"Empty transform animation: {animation.get('name')}")

    mesh_inventory = []
    for obj in sorted((obj for obj in scene.objects if obj.type == "MESH"), key=lambda item: item.name):
        triangle_count = sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)
        mesh_inventory.append({"name": obj.name, "vertices": len(obj.data.vertices),
                               "triangles": triangle_count,
                               "material_names": [mat.name for mat in obj.data.materials if mat]})

    manifest = {
        "schema": "urman-family-book-stage/v1",
        "status": "source asset stage; runtime, hands, authored type, and human/art acceptance remain open",
        "rights": "original neutral cloth-bound family volume; no contemporary cover copy, baked title, portrait, or face imagery",
        "dimensions_m": {
            "closed_default": {"width_x": BOOK_W, "depth_y": BOOK_D, "thickness_z": BOOK_T},
            "closed_default_actual_bounds": {"min_xyz": bounds_min, "max_xyz": bounds_max,
                                              "size_xyz": bounds_size},
        "open_cover_180deg": {"width_x": 0.445, "depth_y": BOOK_D,
                                   "gutter_m": 0.005, "cover_hinge_x": HINGE_X},
            "axis_contract": "Blender source: X width, Y depth, Z thickness; exported glTF uses Y-up"
        },
        "materials": {
            "cloth": {"base_color_srgb": "#674b45", "source_map": "game/assets/textures/painterly/urman_t01_v02_basecolor.png",
                      "source_sha256": hash_file(cloth_path), "source_size_px": list(cloth_image.size),
                      "tile_metres": TILE_METRES, "texture_strength": 0.10},
            "paper": {"base_color_srgb": "#d6ccb3", "source_map": "game/assets/textures/painterly/book_paper_fibers_v1_albedo.png",
                      "source_sha256": hash_file(paper_path), "source_size_px": list(paper_image.size),
                      "tile_metres": TILE_METRES, "texture_strength": 0.08}
        },
        "meshes": mesh_inventory,
        "clips": clips,
        "glb_animation_names": exported_names,
        "source": {"script": "assets/source/blender/photoworlds/urman_family_book.py",
                   "script_sha256": hash_file(Path(__file__).resolve()),
                   "textures": ["game/assets/textures/painterly/urman_t01_v02_basecolor.png",
                                "game/assets/textures/painterly/book_paper_fibers_v1_albedo.png"]},
        "outputs": {
            "blend": {"path": blend_path.name, "sha256": hash_file(blend_path), "bytes": blend_path.stat().st_size},
            "glb": {"path": glb_path.name, "sha256": hash_file(glb_path), "bytes": glb_path.stat().st_size}
        }
    }
    (output_dir / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"BOOK_STAGE_OK blend={blend_path} glb={glb_path} animations={','.join(exported_names)}")


if __name__ == "__main__":
    build_asset(parse_args().output_dir)
