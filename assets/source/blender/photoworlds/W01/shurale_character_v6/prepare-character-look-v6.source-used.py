"""Create a source-only Shurale v6 silhouette study from MPFB2 CC0 inputs.

This pass retains the accepted Shurale head-v4 target recipe, uses MPFB's
game_engine rig and weights, and creates one static branch-working pose for
visual review. It does not export a runtime asset or animation clips.

Usage (from Blender):
    blender --background --factory-startup --python prepare-character-look-v6.py -- \
        <extracted-mpfb-v2.0.17-root> <output-directory> [--contact-only|--material-review]
"""

from __future__ import annotations

import addon_utils
import hashlib
import importlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ARCHIVE_SHA256 = "c8ccfc6269f82cf95661ef81475ddd5a8586f70810ba58940a3d26f7fc281d17"
ARCHIVE_TAG = "v2.0.17"
ARCHIVE_COMMIT = "80919fa"
TARGET_HEIGHT_METERS = 2.35
ALBEDO_SIZE = 2048
MASK_SIZE = 1024
BODY_OBJECT_NAME = "ShuraleCharacterBodyV6"
RIG_NAME = "game_engine"
CONCEPT_REFERENCE = "assets/source/blender/photoworlds/W01/shurale-concepts/modern-horror-anatomy-v1.png"
CONCEPT_SHA256 = "edc0ea02d013c8a4f42eac20ac74461d8e5f13d079c91646f35193d4ab3e59b7"

MACRO = {
    "gender": 1.0,
    "age": 0.90,
    "muscle": 0.08,
    "weight": 0.08,
    "proportions": 0.50,
    "height": 0.50,
    "cupsize": 0.50,
    "firmness": 0.50,
}

# Start from the verified v4 anatomy recipe, then push it far enough to read
# as a distinctive gaunt forest worker rather than a bald MPFB donor.
HEAD_DETAILS = {
    "head/head-fat-decr": 0.92,
    "head/head-age-incr": 0.82,
    "head/head-scale-vert-incr": 0.02,
    "cheek/l-cheek-volume-decr": 0.88,
    "cheek/r-cheek-volume-decr": 0.97,
    "cheek/l-cheek-bones-incr": 0.72,
    "cheek/r-cheek-bones-incr": 0.88,
    "eyes/l-eye-push1-in": 0.48,
    "eyes/r-eye-push1-in": 0.30,
    "nose/nose-hump-incr": 0.40,
    "nose/nose-scale-depth-incr": 0.22,
    "chin/chin-width-decr": 0.28,
    "chin/chin-bones-incr": 0.26,
    "eyebrows/eyebrows-angle-down": 0.48,
}

# Arm length is applied to the unrigged MPFB body so game_engine refits its
# joints after the proportions are final. The wider waist and restrained
# shoulder taper avoid the previous donor's pronounced hourglass silhouette.
BODY_DETAILS = {
    "torso/measure-shoulder-dist-decr": 0.10,
    "torso/measure-waist-circ-incr": 0.42,
    "torso/torso-muscle-dorsi-decr": 0.28,
    "torso/torso-muscle-pectoral-decr": 0.24,
    "arms/measure-upperarm-length-incr": 0.82,
    "arms/measure-lowerarm-length-incr": 0.90,
    "arms/measure-upperarm-circ-decr": 0.24,
    "arms/l-upperarm-fat-decr": 0.22,
    "arms/r-upperarm-fat-decr": 0.22,
    "arms/l-upperarm-muscle-decr": 0.20,
    "arms/r-upperarm-muscle-decr": 0.20,
    "arms/l-upperarm-shoulder-muscle-decr": 0.28,
    "arms/r-upperarm-shoulder-muscle-decr": 0.28,
    "arms/l-lowerarm-scale-horiz-decr": 0.16,
    "arms/r-lowerarm-scale-horiz-decr": 0.16,
    "arms/l-lowerarm-scale-depth-decr": 0.12,
    "arms/r-lowerarm-scale-depth-decr": 0.12,
    "hands/measure-wrist-circ-decr": 0.12,
    "hands/l-hand-fingers-length-incr": 0.72,
    "hands/r-hand-fingers-length-incr": 0.72,
    "hands/l-hand-scale-incr": 0.18,
    "hands/r-hand-scale-incr": 0.18,
    "hands/l-hand-fingers-diameter-incr": 0.08,
    "hands/r-hand-fingers-diameter-incr": 0.08,
    "hands/l-hand-fingers-distance-incr": 0.12,
    "hands/r-hand-fingers-distance-incr": 0.12,
    "legs/l-upperleg-fat-decr": 0.18,
    "legs/r-upperleg-fat-decr": 0.18,
    "legs/l-lowerleg-fat-decr": 0.18,
    "legs/r-lowerleg-fat-decr": 0.18,
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def set_input(node: bpy.types.Node, names: tuple[str, ...], value) -> None:
    for name in names:
        socket = node.inputs.get(name)
        if socket is not None:
            socket.default_value = value
            return
    raise KeyError(f"None of the sockets {names!r} exist on {node.name}")


def clamp01(value: float) -> float:
    return min(1.0, max(0.0, value))


def smoothstep(edge0: float, edge1: float, value: float) -> float:
    if edge0 == edge1:
        return 1.0 if value >= edge1 else 0.0
    t = clamp01((value - edge0) / (edge1 - edge0))
    return t * t * (3.0 - 2.0 * t)


def gaussian(value: float, center: float, spread: float) -> float:
    if spread <= 0.0:
        return 0.0
    delta = (value - center) / spread
    return math.exp(-0.5 * delta * delta)


def bake_target_mix_to_v6_shape(body: bpy.types.Object) -> tuple[bpy.types.ShapeKey, dict]:
    """Capture the evaluated MPFB targets before applying the custom anatomy."""
    shape_keys = body.data.shape_keys
    if shape_keys is None or "Basis" not in shape_keys.key_blocks:
        raise RuntimeError("MPFB did not create the expected relative shape-key Basis")
    if "V6_Anatomy" in shape_keys.key_blocks:
        raise RuntimeError("The v6 anatomy shape already exists; refusing to stack another bake")

    source_keys = list(shape_keys.key_blocks)
    source_active_keys = [key.name for key in source_keys if key.name != "Basis" and abs(key.value) > 1e-8]
    bpy.context.view_layer.update()
    anatomy_key = body.shape_key_add(name="V6_Anatomy", from_mix=True)
    anatomy_key.relative_key = shape_keys.key_blocks["Basis"]
    for key in shape_keys.key_blocks:
        if key.name not in ("Basis", anatomy_key.name):
            key.value = 0.0
    anatomy_key.value = 1.0
    body.data.update()
    bpy.context.view_layer.update()
    return anatomy_key, {
        "name": anatomy_key.name,
        "relativeTo": anatomy_key.relative_key.name,
        "capturedMPFBShapeKeyCount": len(source_keys),
        "capturedActiveMPFBShapeKeyCount": len(source_active_keys),
        "activeMPFBShapeKeyNames": source_active_keys,
        "allSourceMPFBKeysDisabledAfterBake": all(
            key.name in ("Basis", anatomy_key.name) or abs(key.value) <= 1e-8
            for key in shape_keys.key_blocks
        ),
    }


def sculpt_v6_anatomy(
    body: bpy.types.Object,
    anatomy_key: bpy.types.ShapeKey,
    native_head_weights: dict[int, float],
    native_neck_weights: dict[int, float],
) -> dict:
    """Sculpt the evaluated MPFB mix in a relative key, bounded by native skin weights."""
    coordinates = anatomy_key.data
    head_ids = [index for index, weight in native_head_weights.items() if weight >= 0.5]
    neck_ids = [index for index, weight in native_neck_weights.items() if weight >= 0.1]
    if not head_ids or not neck_ids:
        raise RuntimeError("Native game_engine head/neck weights are unavailable for v6 anatomy")
    if max(head_ids + neck_ids) >= len(coordinates):
        raise RuntimeError("Native game_engine head/neck groups exceed the MPFB mesh topology")

    head_points = [coordinates[index].co for index in head_ids]
    neck_points = [coordinates[index].co for index in neck_ids]
    head_min_z = min(point.z for point in head_points)
    head_max_z = max(point.z for point in head_points)
    front_y = min(point.y for point in head_points)
    back_y = max(point.y for point in head_points)
    center_y = (front_y + back_y) * 0.5
    face_depth = max(center_y - front_y, 0.001)
    head_half_width = max(abs(point.x) for point in head_points)
    neck_half_width = max(abs(point.x) for point in neck_points)
    if head_half_width <= 0.0 or face_depth <= 0.0 or neck_half_width <= 0.0:
        raise RuntimeError("Native MPFB head/neck bounds are degenerate; stop before fitting the rig")

    original = [point.co.copy() for point in coordinates]
    head_height = head_max_z - head_min_z
    if head_height <= 0.0:
        raise RuntimeError("Native MPFB head landmarks have no vertical extent")

    maximum_offset = 0.0
    moved_vertices = 0
    for index, point in enumerate(coordinates):
        original_co = original[index]
        head_weight = native_head_weights.get(index, 0.0)
        neck_weight = native_neck_weights.get(index, 0.0)
        z = original_co.z

        if head_weight > 0.0:
            head_mask = smoothstep(0.02, 0.45, head_weight)
            z_norm = clamp01((z - head_min_z) / max(head_height, 1e-6))
            x_norm = original_co.x / head_half_width
            abs_x = abs(x_norm)
            front = clamp01((center_y - original_co.y) / face_depth)

            # This geometry lives in V6_Anatomy, which Blender actually evaluates;
            # the prior raw-mesh edits were hidden by MPFB's active morph keys.
            upper_skull = smoothstep(0.52, 0.96, z_norm)
            point.co.x *= 1.0 - 0.12 * upper_skull * head_mask
            point.co.y += 0.034 * front * gaussian(z_norm, 0.78, 0.16) * head_mask

            asymmetry = 1.22 if original_co.x > 0.0 else 0.82
            brow = gaussian(z_norm, 0.60, 0.07) * gaussian(abs_x, 0.27, 0.20)
            socket = gaussian(z_norm, 0.51, 0.07) * gaussian(abs_x, 0.22, 0.15)
            cheek = gaussian(z_norm, 0.40, 0.09) * gaussian(abs_x, 0.46, 0.20)
            hollow = gaussian(z_norm, 0.30, 0.085) * gaussian(abs_x, 0.40, 0.22)
            jaw_taper = 1.0 - 0.13 * (1.0 - smoothstep(0.16, 0.43, z_norm)) * head_mask
            point.co.x *= jaw_taper
            point.co.y -= front * brow * 0.032 * asymmetry * head_mask
            point.co.y += front * socket * 0.016 * head_mask
            point.co.y -= front * cheek * 0.030 * asymmetry * head_mask
            point.co.y += front * hollow * 0.020 * head_mask
            point.co.y -= front * gaussian(z_norm, 0.16, 0.07) * gaussian(abs_x, 0.0, 0.21) * 0.012 * head_mask
            if original_co.x > 0.0:
                point.co.z -= front * gaussian(z_norm, 0.60, 0.055) * gaussian(x_norm, 0.25, 0.14) * 0.014 * head_mask

        if neck_weight > 0.0:
            neck_mask = smoothstep(0.01, 0.55, neck_weight)
            neck_x = abs(original_co.x) / max(neck_half_width, 0.001)
            point.co.x *= 1.0 - 0.10 * neck_mask
            tendon = gaussian(neck_x, 0.63, 0.16) * neck_mask
            point.co.x += (1.0 if original_co.x >= 0.0 else -1.0) * 0.009 * tendon

        offset = (point.co - original_co).length
        if offset > 1e-5:
            moved_vertices += 1
            maximum_offset = max(maximum_offset, offset)

    body.data.update()
    bpy.context.view_layer.update()
    return {
        "operation": "same-topology sculpt in evaluated MPFB target mix stored as relative key V6_Anatomy before rig-fit",
        "headWeightGroup": "head (game_engine weights > 0.5 define landmarks; smooth weighted falloff) ",
        "neckWeightGroup": "neck_01 (game_engine weights; smooth weighted falloff)",
        "nativeHeadLandmarkVertexCount": len(head_ids),
        "nativeNeckLandmarkVertexCount": len(neck_ids),
        "nativeHeadLandmarksPreScale": {
            "minZ": round(head_min_z, 6),
            "maxZ": round(head_max_z, 6),
            "halfWidth": round(head_half_width, 6),
        },
        "nativeNeckLandmarksPreScale": {
            "halfWidth": round(neck_half_width, 6),
        },
        "shapeKey": anatomy_key.name,
        "movedEvaluatedVertices": moved_vertices,
        "maxEvaluatedVertexOffsetMetersPreScale": round(maximum_offset, 6),
        "features": [
            "tapered upper skull and recessed forehead",
            "asymmetric brow and eye-socket planes",
            "high cheek shelves, hollow cheeks, and tapered jaw",
            "narrow neck with paired tendon ridges",
        ],
    }


def topology_signature(body: bpy.types.Object) -> str:
    payload = {
        "vertices": len(body.data.vertices),
        "edges": [list(edge.vertices) for edge in body.data.edges],
        "faces": [list(poly.vertices) for poly in body.data.polygons],
    }
    encoded = json.dumps(payload, separators=(",", ":"), sort_keys=True).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def weight_signature(body: bpy.types.Object) -> str:
    group_names = {group.index: group.name for group in body.vertex_groups}
    weights = []
    for vertex in body.data.vertices:
        influences = sorted(
            (group_names.get(member.group, "?"), round(member.weight, 8))
            for member in vertex.groups
        )
        weights.append(influences)
    encoded = json.dumps(weights, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def native_weight_map(weights_path: Path, group_name: str) -> dict[int, float]:
    """Read the exact native game_engine skin group for pre-fit anatomy bounds."""
    payload = json.loads(weights_path.read_text(encoding="utf-8"))
    group = payload.get("weights", {}).get(group_name)
    if not isinstance(group, list) or not group:
        raise RuntimeError(f"Native game_engine weight group is unavailable: {group_name}")
    return {int(index): float(weight) for index, weight in group}


def measured_head_neck_fit(
    body: bpy.types.Object,
    rig: bpy.types.Object,
    root: bpy.types.Object,
    head_weights: dict[int, float],
    neck_weights: dict[int, float],
) -> dict:
    """Record evaluated weighted-skin bounds beside the native fitted joints."""
    hidden_modifiers = []
    for modifier in body.modifiers:
        if modifier.type == "MASK" and modifier.show_viewport:
            hidden_modifiers.append((modifier, modifier.show_viewport, modifier.show_render))
            modifier.show_viewport = False
            modifier.show_render = False
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = body.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        if len(mesh.vertices) != len(body.data.vertices):
            raise RuntimeError("The evaluated v6 body changed vertex order before head/neck fit measurement")
        scale = root.scale.x
        matrix = evaluated.matrix_world

        def bounds(weights: dict[int, float], threshold: float) -> dict:
            ids = [index for index, weight in weights.items() if weight >= threshold]
            if not ids:
                raise RuntimeError("A native head/neck weight group has no landmark vertices")
            points = [matrix @ mesh.vertices[index].co for index in ids]
            return {
                "vertexCount": len(ids),
                "minMeters": [round(min(point[axis] for point in points), 5) for axis in range(3)],
                "maxMeters": [round(max(point[axis] for point in points), 5) for axis in range(3)],
            }

        head_bounds = bounds(head_weights, 0.5)
        neck_bounds = bounds(neck_weights, 0.1)
    finally:
        evaluated.to_mesh_clear()
        for modifier, show_viewport, show_render in hidden_modifiers:
            modifier.show_viewport = show_viewport
            modifier.show_render = show_render
        bpy.context.view_layer.update()

    joint_names = ("neck_01", "head")
    joints = {}
    for name in joint_names:
        bone = rig.data.bones.get(name)
        if bone is None:
            raise RuntimeError(f"Native game_engine rig is missing the {name} landmark")
        head = rig.matrix_world @ bone.head_local
        tail = rig.matrix_world @ bone.tail_local
        joints[name] = {
            "headMeters": [round(value, 5) for value in head],
            "tailMeters": [round(value, 5) for value in tail],
            "lengthMeters": round(bone.length * scale, 5),
        }

    return {
        "measuredFrom": "evaluated body after MPFB fitting, before the working pose",
        "headWeightThreshold": 0.5,
        "neckWeightThreshold": 0.1,
        "headSkinBounds": head_bounds,
        "neckSkinBounds": neck_bounds,
        "fittedNativeJoints": joints,
    }


def measured_arm_chains(rig: bpy.types.Object, root: bpy.types.Object) -> dict:
    metrics = {}
    scale = root.scale.x
    for side in ("l", "r"):
        names = [
            f"upperarm_{side}",
            f"lowerarm_{side}",
            f"hand_{side}",
            *(f"middle_{segment:02d}_{side}" for segment in (1, 2, 3)),
        ]
        missing = [name for name in names if name not in rig.data.bones]
        if missing:
            raise RuntimeError(f"Native game_engine rig missing arm-chain bones: {missing}")
        bones = [rig.data.bones[name] for name in names]
        arm_only = (rig.data.bones[f"upperarm_{side}"].length + rig.data.bones[f"lowerarm_{side}"].length) * scale
        full_chain = sum(bone.length for bone in bones) * scale
        shoulder = rig.data.bones[f"upperarm_{side}"].head_local
        fingertip = rig.data.bones[f"middle_03_{side}"].tail_local
        reach = (fingertip - shoulder).length * scale
        metrics[side] = {
            "upperArmMeters": round(rig.data.bones[f"upperarm_{side}"].length * scale, 5),
            "lowerArmMeters": round(rig.data.bones[f"lowerarm_{side}"].length * scale, 5),
            "upperPlusLowerMeters": round(arm_only, 5),
            "handPlusMiddleFingerChainMeters": round(full_chain, 5),
            "shoulderToMiddleFingertipStraightReachMeters": round(reach, 5),
        }
    return metrics


def make_wood_material(name: str, darken: float = 0.46) -> bpy.types.Material:
    palette_factor = darken / 0.46
    material = bpy.data.materials.new(name)
    base = tuple(component * palette_factor for component in (0.075, 0.060, 0.046)) + (1.0,)
    material.diffuse_color = base
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (0, 0)
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (240, 0)
    set_input(shader, ("Base Color",), base)
    set_input(shader, ("Roughness",), 0.78)
    set_input(shader, ("Metallic",), 0.0)
    set_input(shader, ("Specular IOR Level", "Specular"), 0.18)
    set_input(shader, ("Coat Weight", "Clearcoat"), 0.0)
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def make_eye_material() -> bpy.types.Material:
    material = bpy.data.materials.new("Shurale | dark wet non-emissive eyes")
    material.diffuse_color = (0.008, 0.010, 0.010, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    set_input(shader, ("Base Color",), (0.006, 0.008, 0.007, 1.0))
    set_input(shader, ("Roughness",), 0.10)
    set_input(shader, ("Metallic",), 0.0)
    set_input(shader, ("Specular IOR Level", "Specular"), 0.46)
    set_input(shader, ("Coat Weight", "Clearcoat"), 0.20)
    set_input(shader, ("Coat Roughness", "Clearcoat Roughness"), 0.10)
    set_input(shader, ("Emission Color", "Emission"), (0.0, 0.0, 0.0, 1.0))
    return material


def set_active_bake_target(material: bpy.types.Material, image: bpy.types.Image) -> None:
    if not material.use_nodes:
        material.use_nodes = True
    nodes = material.node_tree.nodes
    for node in nodes:
        node.select = False
    target = nodes.new("ShaderNodeTexImage")
    target.name = f"BakeTarget_{image.name}"
    target.label = "Temporary UV bake target"
    target.image = image
    target.select = True
    nodes.active = target


def save_baked_image(image: bpy.types.Image, output_path: Path) -> None:
    image.filepath_raw = str(output_path)
    image.file_format = "PNG"
    image.save()
    image.pack()


def finalize_baked_skin(albedo: bpy.types.Image, ao_mask: bpy.types.Image) -> bpy.types.Material:
    material = bpy.data.materials.new("Shurale | baked umber-ash wood-skin")
    material.use_nodes = True
    material.diffuse_color = (0.085, 0.064, 0.047, 1.0)
    material["albedo_texture"] = albedo.name
    material["mask_texture"] = ao_mask.name
    material["mask_semantics"] = "grayscale ambient occlusion; cavities dark, exposed surfaces light"
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (500, 80)
    set_input(shader, ("Roughness",), 0.78)
    set_input(shader, ("Metallic",), 0.0)
    set_input(shader, ("Specular IOR Level", "Specular"), 0.22)
    set_input(shader, ("Coat Weight", "Clearcoat"), 0.0)
    albedo_node = nodes.new("ShaderNodeTexImage")
    albedo_node.name = "Shurale_BakedAlbedo_2K"
    albedo_node.image = albedo
    albedo_node.location = (-500, 200)
    links.new(albedo_node.outputs["Color"], shader.inputs["Base Color"])
    mask_node = nodes.new("ShaderNodeTexImage")
    mask_node.name = "Shurale_AmbientOcclusionMask_1K"
    mask_node.image = ao_mask
    mask_node.location = (-500, -80)
    mask_node.image.colorspace_settings.name = "Non-Color"
    roughness = nodes.new("ShaderNodeMapRange")
    roughness.location = (0, -70)
    roughness.inputs["From Min"].default_value = 0.0
    roughness.inputs["From Max"].default_value = 1.0
    roughness.inputs["To Min"].default_value = 0.84
    roughness.inputs["To Max"].default_value = 0.74
    links.new(mask_node.outputs["Color"], roughness.inputs["Value"])
    links.new(roughness.outputs["Result"], shader.inputs["Roughness"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def bake_material_maps(body: bpy.types.Object, output_dir: Path) -> dict:
    if not body.data.uv_layers or body.data.uv_layers.active is None:
        raise RuntimeError("The MPFB body needs an active UV map before its material bake")
    scene = bpy.context.scene
    previous_engine = scene.render.engine
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = False
    scene.render.bake.margin = 10
    scene.render.bake.use_clear = True

    materials = [material for material in body.data.materials if material is not None]
    if len(materials) < 2:
        raise RuntimeError("Expected separate body and eye materials for the Shurale bake")
    albedo_path = output_dir / "shurale-v4-body-albedo-2k.png"
    albedo = bpy.data.images.new(
        "Shurale_Body_Albedo_2K", width=ALBEDO_SIZE, height=ALBEDO_SIZE,
        alpha=False, float_buffer=False,
    )
    albedo.colorspace_settings.name = "sRGB"
    for material in materials:
        set_active_bake_target(material, albedo)
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, margin=10, use_clear=True)
    save_baked_image(albedo, albedo_path)

    mask_path = output_dir / "shurale-v4-body-mask-ao-1k.png"
    ao_mask = bpy.data.images.new(
        "Shurale_Body_AmbientOcclusion_Mask_1K", width=MASK_SIZE, height=MASK_SIZE,
        alpha=False, float_buffer=False,
    )
    ao_mask.colorspace_settings.name = "Non-Color"
    for material in materials:
        set_active_bake_target(material, ao_mask)
    scene.cycles.samples = 48
    bpy.ops.object.bake(type="AO", margin=10, use_clear=True)
    save_baked_image(ao_mask, mask_path)

    body.data.materials[0] = finalize_baked_skin(albedo, ao_mask)
    scene.render.engine = previous_engine
    return {
        "albedo": {
            "file": albedo_path.name,
            "sizePx": [ALBEDO_SIZE, ALBEDO_SIZE],
            "colorSpace": "sRGB",
            "semantics": "UV-baked project-original umber/ash skin base color; baked from the rest pose",
        },
        "mask": {
            "file": mask_path.name,
            "sizePx": [MASK_SIZE, MASK_SIZE],
            "colorSpace": "Non-Color",
            "semantics": "grayscale ambient occlusion; cavities dark, exposed surfaces light; used for subtle roughness variation",
        },
        "uvMap": body.data.uv_layers.active.name,
        "bakePose": "native MPFB rest pose before branch-working IK is applied",
        "outputs": [albedo_path.name, mask_path.name],
    }


def attach_visible_body_and_eyes(body: bpy.types.Object) -> tuple[int, int]:
    body_group = body.vertex_groups.get("body")
    left_eye_group = body.vertex_groups.get("helper-l-eye")
    right_eye_group = body.vertex_groups.get("helper-r-eye")
    if not body_group or not left_eye_group or not right_eye_group:
        raise RuntimeError("MPFB body/eye vertex groups were not found")

    body_indices = []
    eye_indices = []
    for vertex in body.data.vertices:
        membership = {member.group: member.weight for member in vertex.groups}
        if membership.get(body_group.index, 0.0) > 0.5:
            body_indices.append(vertex.index)
        if max(
            membership.get(left_eye_group.index, 0.0),
            membership.get(right_eye_group.index, 0.0),
        ) > 0.5:
            eye_indices.append(vertex.index)

    visible_group = body.vertex_groups.new(name="ShuraleBodyAndEyes")
    visible_group.add(body_indices + eye_indices, 1.0, "REPLACE")
    mask = next((modifier for modifier in body.modifiers if modifier.type == "MASK"), None)
    if mask is None:
        raise RuntimeError("Expected the native MPFB helper mask modifier")
    mask.vertex_group = visible_group.name
    mask.invert_vertex_group = False

    skin = make_wood_material("Shurale | desiccated umber and ash wood-skin")
    eyes = make_eye_material()
    body.data.materials.clear()
    body.data.materials.append(skin)
    body.data.materials.append(eyes)
    left_idx = left_eye_group.index
    right_idx = right_eye_group.index
    eye_faces = 0
    for polygon in body.data.polygons:
        eye_members = 0
        for vertex_index in polygon.vertices:
            vertex = body.data.vertices[vertex_index]
            if any(
                member.group in (left_idx, right_idx) and member.weight > 0.5
                for member in vertex.groups
            ):
                eye_members += 1
        if eye_members >= max(2, math.ceil(len(polygon.vertices) * 0.75)):
            polygon.material_index = 1
            eye_faces += 1
        else:
            polygon.material_index = 0
        polygon.use_smooth = True
    if not eye_faces:
        raise RuntimeError("No faces are assigned to MPFB's eye groups")
    return len(body_indices), eye_faces


def aim_camera(camera: bpy.types.Object, location: Vector, target: Vector) -> None:
    camera.location = location
    camera.rotation_euler = (target - location).to_track_quat("-Z", "Y").to_euler()


def make_area_light(name: str, location: tuple[float, float, float], color, power: float, size: float, target: Vector) -> None:
    light_data = bpy.data.lights.new(name, type="AREA")
    light_data.energy = power
    light_data.shape = "DISK"
    light_data.size = size
    light_data.color = color
    light = bpy.data.objects.new(name, light_data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()


def make_stage_material() -> bpy.types.Material:
    material = bpy.data.materials.new("Studio | muted warm-gray matte")
    material.diffuse_color = (0.19, 0.17, 0.15, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    set_input(shader, ("Base Color",), (0.19, 0.17, 0.15, 1.0))
    set_input(shader, ("Roughness",), 0.88)
    return material


def add_staging_branch(root: bpy.types.Object) -> bpy.types.Object:
    curve = bpy.data.curves.new("StagingOnly_BranchCurve", type="CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 8
    # The bough is a slender, tapered workpiece instead of a constant-radius
    # pipe. The thinner offshoot sits at the opposite hand's pinch point.
    curve.bevel_depth = 0.007
    curve.bevel_resolution = 3
    branches = [
        (
            [(-0.42, -0.41, 1.27), (-0.25, -0.42, 1.28), (-0.06, -0.43, 1.26), (0.13, -0.43, 1.27), (0.24, -0.427, 1.2803), (0.36, -0.41, 1.29)],
            (0.45, 0.60, 0.68, 0.82, 1.0, 0.42),
        ),
        (
            [(-0.12, -0.425, 1.266), (-0.115, -0.423, 1.291), (-0.1172, -0.4209, 1.3145)],
            (0.55, 0.48, 0.43),
        ),
    ]
    for coords, radii in branches:
        spline = curve.splines.new("BEZIER")
        spline.bezier_points.add(len(coords) - 1)
        for point, coordinate, radius in zip(spline.bezier_points, coords, radii):
            point.co = coordinate
            point.handle_left_type = "AUTO"
            point.handle_right_type = "AUTO"
            point.radius = radius
    branch = bpy.data.objects.new("StagingOnly_Branch_ExcludeFromRuntime", curve)
    bpy.context.scene.collection.objects.link(branch)
    branch.parent = root
    branch.matrix_parent_inverse.identity()
    curve.materials.append(make_wood_material("Staging branch | darker bark", 0.30))
    return branch


def add_working_pose(rig: bpy.types.Object, root: bpy.types.Object) -> dict:
    pose = rig.pose
    required = {
        "upperarm_l", "lowerarm_l", "hand_l", "upperarm_r", "lowerarm_r",
        "hand_r", "neck_01", "head", "spine_03",
    }
    missing = required.difference(pose.bones.keys())
    if missing:
        raise RuntimeError(f"MPFB game_engine rig is missing needed working-pose bones: {sorted(missing)}")

    scene = bpy.context.scene
    # Local targets put both hands forward at the bough, with the left hand
    # steadying it and the right hand pinching the smaller side shoot. Pole
    # empties are in front of and below each shoulder to keep elbows relaxed.
    targets = {
        "l": Vector((0.24, -0.34, 1.22)),
        "r": Vector((-0.15, -0.35, 1.25)),
    }
    poles = {
        "l": Vector((0.34, -0.50, 1.10)),
        "r": Vector((-0.34, -0.50, 1.10)),
    }
    for side in ("l", "r"):
        target = bpy.data.objects.new(f"Shurale_PoseIK_Target_{side}", None)
        scene.collection.objects.link(target)
        target.empty_display_type = "SPHERE"
        target.empty_display_size = 0.035
        target.parent = root
        target.matrix_parent_inverse.identity()
        target.location = targets[side]
        pole = bpy.data.objects.new(f"Shurale_PoseIK_Pole_{side}", None)
        scene.collection.objects.link(pole)
        pole.empty_display_type = "CIRCLE"
        pole.empty_display_size = 0.025
        pole.parent = root
        pole.matrix_parent_inverse.identity()
        pole.location = poles[side]

        constraint = pose.bones[f"lowerarm_{side}"].constraints.new("IK")
        constraint.name = f"Working pose IK {side} arm"
        constraint.target = target
        constraint.pole_target = pole
        constraint.chain_count = 2
        constraint.iterations = 160
        constraint.use_stretch = False
        # MPFB's game_engine rest-plane is mirrored on the left arm; this
        # angle keeps the two elbows in the same forward/down working plane.
        constraint.pole_angle = math.pi if side == "l" else 0.0

    # Lean into the task and lower the gaze toward the branch rather than pose
    # the donor in a display stance.
    pose.bones["spine_03"].rotation_mode = "XYZ"
    pose.bones["spine_03"].rotation_euler.x = 0.045
    pose.bones["neck_01"].rotation_mode = "XYZ"
    pose.bones["neck_01"].rotation_euler.x = 0.055
    pose.bones["head"].rotation_mode = "XYZ"
    pose.bones["head"].rotation_euler.x = 0.11
    pose.bones["clavicle_l"].rotation_mode = "XYZ"
    pose.bones["clavicle_l"].rotation_euler.y = -0.020
    pose.bones["clavicle_r"].rotation_mode = "XYZ"
    pose.bones["clavicle_r"].rotation_euler.y = 0.020

    # Close the stabilizing hand around the bough and make a firmer thumb/index
    # pinch with the other hand. This remains a study pose, not an animation.
    digit_prefixes = ("thumb", "index", "middle", "ring", "pinky")
    for side in ("l", "r"):
        for digit in digit_prefixes:
            for segment in (1, 2, 3):
                bone = pose.bones.get(f"{digit}_{segment:02d}_{side}")
                if bone is None:
                    continue
                bone.rotation_mode = "XYZ"
                if side == "l":
                    bend = (0.52, 0.64, 0.54)[segment - 1]
                    if digit == "thumb":
                        bend = (0.55, 0.64, 0.44)[segment - 1]
                elif digit in ("thumb", "index"):
                    bend = (0.52, 0.62, 0.50)[segment - 1]
                else:
                    bend = (0.24, 0.32, 0.26)[segment - 1]
                # Measured distal pad gaps in the posed skin mesh: this small
                # index curl brings each hand's thumb/index surfaces into
                # contact around the staged bough or twig without changing body shape.
                if digit == "index":
                    bend += 0.08 if side == "l" else 0.14
                bone.rotation_euler.x = bend

    bpy.context.view_layer.update()
    if len(rig.data.bones) < 50:
        raise RuntimeError(f"Unexpectedly incomplete native game_engine rig: {len(rig.data.bones)} bones")
    for side in ("l", "r"):
        if not pose.bones[f"lowerarm_{side}"].constraints.get(f"Working pose IK {side} arm"):
            raise RuntimeError(f"Failed to build working pose IK for arm {side}")
    return {
        "rig": RIG_NAME,
        "rigBones": len(rig.data.bones),
        "workingPose": "planted stance; left thumb/index close around tapered bough while right thumb/index pinch a small side shoot; elbows forward/down",
        "targetsLocalMeters": {side: list(value) for side, value in targets.items()},
        "poleTargetsLocalMeters": {side: list(value) for side, value in poles.items()},
        "stagingBranch": {
            "mainBoughDiameterMeters": 0.020,
            "sideShootDiameterAtPinchMeters": 0.0085,
            "leftGripCenterLocalMeters": [0.2411, -0.4274, 1.2803],
            "rightPinchCenterLocalMeters": [-0.1172, -0.4209, 1.3145],
            "geometry": "auto-handled Bezier splines with tapered point radii; source-preview only",
        },
        "indexCurlDeltaRadiansForMeasuredPadGap": {"l": 0.08, "r": 0.14},
        "poseNotAnimation": True,
    }


def main() -> None:
    try:
        separator = sys.argv.index("--")
    except ValueError as error:
        raise SystemExit("Expected -- <mpfb-archive-root> <output-directory> [--contact-only|--material-review]") from error
    arguments = sys.argv[separator + 1:]
    if len(arguments) not in (2, 3) or (len(arguments) == 3 and arguments[2] not in ("--contact-only", "--material-review")):
        raise SystemExit("Expected -- <mpfb-archive-root> <output-directory> [--contact-only|--material-review]")

    archive_root = Path(arguments[0]).resolve()
    output_dir = Path(arguments[1]).resolve()
    contact_only = len(arguments) == 3 and arguments[2] == "--contact-only"
    material_review = len(arguments) == 3 and arguments[2] == "--material-review"
    source = archive_root / "src"
    targets_root = source / "mpfb" / "data" / "targets"
    rig_root = source / "mpfb" / "data" / "rigs" / "standard"
    if not (source / "mpfb" / "__init__.py").is_file():
        raise SystemExit(f"Not an extracted MPFB source root: {archive_root}")
    if not (archive_root / "LICENSE.ASSETS.md").is_file():
        raise SystemExit(f"Missing MPFB asset license under {archive_root}")
    archive_zip = archive_root.parents[1] / "archive" / "mpfb2-v2.0.17.zip"
    if sha256(archive_zip) != ARCHIVE_SHA256:
        raise SystemExit("MPFB v2.0.17 archive hash does not match the verified license receipt")
    for target_name in {**HEAD_DETAILS, **BODY_DETAILS}:
        if not (targets_root / f"{target_name}.target.gz").is_file():
            raise FileNotFoundError(f"MPFB v2.0.17 target is unavailable: {target_name}")
    rig_file = rig_root / f"rig.{RIG_NAME}.json"
    rig_weights = rig_root / f"weights.{RIG_NAME}.json"
    if not rig_file.is_file() or not rig_weights.is_file():
        raise FileNotFoundError(f"Native rig definition or weights missing for {RIG_NAME}")
    output_dir.mkdir(parents=True, exist_ok=True)

    concept_path = Path(__file__).resolve().parents[6] / CONCEPT_REFERENCE
    if not concept_path.is_file() or sha256(concept_path) != CONCEPT_SHA256:
        raise SystemExit("The internal concept reference changed; stop and review the v6 art inputs")

    repository = bpy.context.preferences.extensions.repos.new(
        name="URMAN isolated MPFB Shurale v6",
        module="urman_shurale_v6",
        custom_directory=str(source),
    )
    repository.enabled = True
    addon_utils.enable("bl_ext.urman_shurale_v6.mpfb", default_set=True)
    human_service = importlib.import_module(
        "bl_ext.urman_shurale_v6.mpfb.services.humanservice"
    ).HumanService
    target_service = importlib.import_module(
        "bl_ext.urman_shurale_v6.mpfb.services.targetservice"
    ).TargetService

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    macro = target_service.get_default_macro_info_dict()
    macro.update(MACRO)
    human = human_service.create_human(macro_detail_dict=macro)
    applied_targets = []
    for target_name, weight in {**HEAD_DETAILS, **BODY_DETAILS}.items():
        target_service.load_target(human, str(targets_root / f"{target_name}.target.gz"), weight=weight)
        applied_targets.append({"target": target_name, "weight": weight})
    bpy.context.view_layer.update()

    # MPFB stores every target as a relative shape key. Editing mesh.vertices
    # here would change the raw data but leave Basis and the active target mix
    # untouched, so Blender would display/righ-fit the unsculpted anatomy.
    anatomy_key, shape_bake_info = bake_target_mix_to_v6_shape(human)
    native_head_weights = native_weight_map(rig_weights, "head")
    native_neck_weights = native_weight_map(rig_weights, "neck_01")
    topology_before_sculpt = topology_signature(human)
    sculpt_info = sculpt_v6_anatomy(human, anatomy_key, native_head_weights, native_neck_weights)
    topology_after_sculpt = topology_signature(human)
    if topology_after_sculpt != topology_before_sculpt:
        raise RuntimeError("The v6 anatomy sculpt changed MPFB mesh topology")

    # Fit the native runtime skeleton only after body targets and the v6
    # same-topology anatomy sculpt are final; no post-rig limb warping is used.
    rig = human_service.add_builtin_rig(human, RIG_NAME, import_weights=True)
    if rig is None:
        raise RuntimeError("MPFB failed to construct the native game_engine rig")
    human.name = BODY_OBJECT_NAME
    rig.name = "ShuraleCharacterRigV6"
    rig.data.name = "ShuraleCharacterRigV6Data"
    skin_vertex_count, eye_face_count = attach_visible_body_and_eyes(human)

    # Root scaling preserves the donor's full vertex weights and fitted rest
    # bones while placing the complete authored source at the requested height.
    body_group = human.vertex_groups["body"]
    body_indices = {
        vertex.index
        for vertex in human.data.vertices
        if any(member.group == body_group.index and member.weight > 0.5 for member in vertex.groups)
    }
    body_z = [human.data.vertices[index].co.z for index in body_indices]
    original_min_z = min(body_z)
    original_max_z = max(body_z)
    original_height = original_max_z - original_min_z
    if original_height <= 0:
        raise ValueError(f"Invalid MPFB body height: {original_height}")
    scale_factor = TARGET_HEIGHT_METERS / original_height
    root = bpy.data.objects.new("ShuraleCharacterRootV6", None)
    bpy.context.scene.collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.28
    root.scale = (scale_factor, scale_factor, scale_factor)
    root.location = (0.0, 0.0, -original_min_z * scale_factor)
    rig.parent = root
    rig.matrix_parent_inverse.identity()
    arm_chain_metrics = measured_arm_chains(rig, root)
    head_neck_fit = measured_head_neck_fit(
        human, rig, root, native_head_weights, native_neck_weights
    )
    weights_sha256 = weight_signature(human)

    # Bake against the unposed skin so UV detail remains attached when the
    # accepted working-pose rig later deforms the character.
    texture_info = bake_material_maps(human, output_dir) if material_review else None
    pose_info = add_working_pose(rig, root)
    add_staging_branch(root)

    floor_material = make_stage_material()
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.01))
    floor = bpy.context.object
    floor.name = "PreviewStage_Floor_ExcludeFromRuntime"
    floor.data.materials.append(floor_material)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1500
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.resolution_percentage = 100
    scene.render.filepath = str(output_dir / "shurale-v6-working-front.png")
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.camera = None
    world = bpy.data.worlds.new("ShuraleCharacterV6StudioWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.19, 0.18, 0.17, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.56
    scene.world = world
    scene.render.image_settings.file_format = "PNG"
    scene.eevee.taa_render_samples = 64
    scene.render.image_settings.color_mode = "RGBA"

    camera_data = bpy.data.cameras.new("ShuraleCharacterV6Camera")
    camera = bpy.data.objects.new("ShuraleCharacterV6Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = "ORTHO"
    camera_data.lens = 52
    stage_target = Vector((0.0, -0.04, TARGET_HEIGHT_METERS * 0.50))
    make_area_light("Key | warm overcast", (-3.6, -4.6, 4.7), (1.0, 0.82, 0.67), 1050, 3.0, Vector((0.0, 0.0, 1.35)))
    make_area_light("Fill | cool soft", (3.8, -3.0, 3.0), (0.66, 0.76, 0.83), 520, 3.5, Vector((0.0, -0.1, 1.30)))
    make_area_light("Rim | warm neutral", (0.5, 2.6, 4.1), (0.90, 0.76, 0.60), 850, 2.4, Vector((0.0, 0.0, 1.4)))
    make_area_light("Face lift | broad", (-0.8, -4.0, 2.7), (0.85, 0.82, 0.75), 190, 1.6, Vector((0.0, -0.1, 1.95)))

    if contact_only:
        views = [
            ("contact-hold-close", Vector((0.86, -3.10, 2.25)), Vector((0.3402, -0.6028, 1.8020)), 0.46),
            ("contact-pinch-close", Vector((-0.82, -3.10, 2.28)), Vector((-0.1653, -0.5939, 1.8501)), 0.43),
        ]
    elif material_review:
        views = [
            ("working-front", Vector((0.0, -7.0, stage_target.z)), stage_target, 3.15),
            ("face-hands-working", Vector((1.4, -5.5, 2.55)), Vector((0.0, -0.38, 1.82)), 1.58),
        ]
    else:
        views = [
            ("working-front", Vector((0.0, -7.0, stage_target.z)), stage_target, 3.15),
            ("working-three-quarter", Vector((2.7, -6.3, 2.7)), stage_target, 3.15),
            ("face-hands-working", Vector((1.4, -5.5, 2.55)), Vector((0.0, -0.38, 1.82)), 1.58),
        ]
    output_names = []
    for name, location, target, orthoscale in views:
        camera_data.ortho_scale = orthoscale
        aim_camera(camera, location, target)
        prefix = "shurale-v6-material-review" if material_review else "shurale-v6"
        output = output_dir / f"{prefix}-{name}.png"
        scene.render.filepath = str(output)
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)
        output_names.append(output.name)

    # Save the complete editable source with rig, weights, working pose, and
    # explicitly staging-only branch/floor/cameras; no runtime export is made.
    blend_name = "shurale-character-look-v6-material-review.blend" if material_review else "shurale-character-look-v6.blend"
    blend_path = output_dir / blend_name
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    bpy.context.view_layer.update()
    evaluated = human.evaluated_get(bpy.context.evaluated_depsgraph_get())
    evaluated_mesh = evaluated.to_mesh()
    try:
        bounds = [human.matrix_world @ Vector(corner) for corner in human.bound_box]
        min_z = min(point.z for point in bounds)
        max_z = max(point.z for point in bounds)
        face_count = len(evaluated_mesh.polygons)
        vertex_count = len(evaluated_mesh.vertices)
    finally:
        evaluated.to_mesh_clear()
    armature_mod = next((modifier for modifier in human.modifiers if modifier.type == "ARMATURE"), None)
    if armature_mod is None or armature_mod.object != rig:
        raise RuntimeError("The source human is not weighted to the native MPFB rig")
    if abs((max_z - min_z) - TARGET_HEIGHT_METERS) > 0.05:
        raise RuntimeError(f"Unexpected visible body height: {max_z - min_z:.3f}m")

    license_paths = [archive_root / name for name in ("LICENSE.md", "LICENSE.CODE.md", "LICENSE.ASSETS.md")]
    license_sha = {path.name: sha256(path) for path in license_paths if path.is_file()}
    manifest = {
        "candidateStatus": "v6 source-only silhouette study; review required; not final art acceptance or runtime",
        "renderMode": "material-review" if material_review else "contact-only" if contact_only else "full-study",
        "blenderVersion": bpy.app.version_string,
        "artDirection": {
            "intent": "distinctly gaunt, asymmetric human-derived forest worker with long arms and hands; noncombat; no borrowed screen-creature design",
            "internalConceptReference": CONCEPT_REFERENCE,
            "internalConceptSHA256": CONCEPT_SHA256,
            "referenceUse": "internal proportions and plane study only; no reference pixels or surface design copied into output",
        },
        "heightMeters": round(max_z - min_z, 5),
        "visibleBodyVertices": skin_vertex_count,
        "visibleEvaluatedVertices": vertex_count,
        "visibleEvaluatedFaces": face_count,
        "nativeEyeMaterialFaces": eye_face_count,
        "boundsMeters": {
            "min": [round(v, 5) for v in (min(point.x for point in bounds), min(point.y for point in bounds), min_z)],
            "max": [round(v, 5) for v in (max(point.x for point in bounds), max(point.y for point in bounds), max_z)],
        },
        "nativeRig": {
            "name": RIG_NAME,
            "boneCount": len(rig.data.bones),
            "fitOrder": "MPFB target mix baked into relative V6_Anatomy key + same-topology sculpt, then native game_engine fit and CC0 weights",
            "restArmChainMeters": arm_chain_metrics,
            "headNeckFit": head_neck_fit,
            "weightedMeshSHA256": weights_sha256,
            "weightsFile": f"src/mpfb/data/rigs/standard/weights.{RIG_NAME}.json",
            "weightsSHA256": sha256(rig_weights),
            **pose_info,
        },
        "meshIntegrity": {
            "topologyBeforeSculptSHA256": topology_before_sculpt,
            "topologyAfterSculptSHA256": topology_after_sculpt,
            "topologyUnchanged": topology_before_sculpt == topology_after_sculpt,
            "vertexCount": len(human.data.vertices),
            "edgeCount": len(human.data.edges),
            "polygonCount": len(human.data.polygons),
            "shapeBake": shape_bake_info,
            "sculpt": sculpt_info,
        },
        "bodyTargets": applied_targets,
        "source": {
            "name": "MakeHuman Community MPFB2",
            "archiveSHA256": ARCHIVE_SHA256,
            "tag": ARCHIVE_TAG,
            "commit": ARCHIVE_COMMIT,
            "sourceLicense": "GPL-3.0-or-later (local generator/API source only)",
            "baseMeshTargetsRigAndWeightsLicense": "CC0 1.0 Universal",
            "generatedOutputStatus": "MPFB LICENSE.md section D treats generated graphical data and saved models as user data",
            "archiveUrl": "https://github.com/makehumancommunity/mpfb2/tree/v2.0.17",
            "licenseFileSHA256": license_sha,
            "archiveRootForReproduction": str(archive_root),
        },
        "generator": {
            "path": Path(__file__).name,
            "sha256": sha256(Path(__file__).resolve()),
        },
        "runtimePromotion": False,
        "textures": texture_info,
        "clips": [],
        "stagingOnlyObjects": [
            "StagingOnly_Branch_ExcludeFromRuntime",
            "PreviewStage_Floor_ExcludeFromRuntime",
        ],
        "outputs": output_names + (texture_info["outputs"] if texture_info else []) + [blend_path.name],
    }
    for filename in manifest["outputs"]:
        manifest.setdefault("outputSHA256", {})[filename] = sha256(output_dir / filename)
    (output_dir / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "blend": str(blend_path),
        "outputs": output_names,
        "heightMeters": round(max_z - min_z, 5),
        "restArmChainMeters": arm_chain_metrics,
        "meshTopologySHA256": topology_after_sculpt,
        "weightedMeshSHA256": weights_sha256,
        "visibleVertices": vertex_count,
        "visibleFaces": face_count,
        "eyeFaces": eye_face_count,
        "rig": RIG_NAME,
        "rigBones": len(rig.data.bones),
        "generatorSHA256": manifest["generator"]["sha256"],
    }, indent=2))


if __name__ == "__main__":
    main()
