"""Bake a source-only Shurale surface pass from the frozen v6 body.

Run with the pinned Blender 4.5.12:
    Blender --background shurale-character-look-v6-input.blend \
        --python bake-shurale-surface-v3.py

The input blend is a frozen copy. This script writes its outputs beside itself
and does not alter the source v6 project, mesh, rig, or shape keys.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import bpy
from mathutils import Vector


EXPECTED_BLENDER_VERSION = "4.5.12 LTS"
EXPECTED_INPUT_SHA256 = "8c49565c415b416ca962259da7f8f43e617835ee746a91abade95fe2d56ef9f2"
EXPECTED_CONCEPT_SHA256 = "edc0ea02d013c8a4f42eac20ac74461d8e5f13d079c91646f35193d4ab3e59b7"
EXPECTED_BARK_SHA256 = "adae33c905c2058d9fc9875e23f71a1da514ba8b961b05415b6292226db34420"
IMAGEGEN_PROMPT = """Create a production-ready original seamless tileable PBR ALBEDO texture of ancient grey ash-tree bark with restrained dark umber inner grain, for the woody skin of an original unsettling humanoid forest spirit in a grounded contemporary psychological horror game. Square flat texture swatch filling the entire image, perfectly front-on orthographic, even diffuse neutral illumination, NO shadows from lighting, NO highlights, NO perspective, NO text, NO border, NO character or face or branches. Large coherent longitudinal bark plates with irregular broken edges, a few deep branching fissures, coarse weathered cellulose fibers and worn smooth lighter ridge crests; muted ash grey / cool weathered taupe / near-black brown cavities with sparse desaturated olive patina. Material should be tactile, physically plausible and weathered, NOT glossy wood veneer, NOT marbling, NOT mushy noise, NOT uniform parallel stripes, NOT small all-over scale pattern. Readable medium and broad structures suitable for a 2K real-time character texture. Entire image is the material color only. Make all four edges tile naturally."""
BODY_NAME = "ShuraleCharacterBodyV6"
RIG_OBJECT_NAME = "ShuraleCharacterRigV6"
REGION_ATTRIBUTE = "ShuraleSurfaceRegionsBakeOnly"
HAND_ATTRIBUTE = "ShuraleSurfaceHandsBakeOnly"
FACE_ATTRIBUTE = "ShuraleSurfaceFaceMasksBakeOnly"
ALBEDO_SIZE = 2048
NORMAL_SIZE = 2048
AO_SIZE = 1024


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def make_node(tree: bpy.types.NodeTree, kind: str, name: str, location: tuple[int, int]) -> bpy.types.Node:
    node = tree.nodes.new(kind)
    node.name = name
    node.label = name
    node.location = location
    return node


def set_socket(node: bpy.types.Node, name: str, value) -> None:
    socket = node.inputs.get(name)
    if socket is None:
        raise KeyError(f"{node.bl_idname} has no input named {name!r}")
    socket.default_value = value


def math_node(tree, operation: str, name: str, location, a=None, b=None):
    node = make_node(tree, "ShaderNodeMath", name, location)
    node.operation = operation
    if a is not None:
        node.inputs[0].default_value = a
    if b is not None:
        node.inputs[1].default_value = b
    return node


def ramp(tree, name, location, stops):
    node = make_node(tree, "ShaderNodeValToRGB", name, location)
    elements = node.color_ramp.elements
    elements[0].position = stops[0][0]
    elements[0].color = stops[0][1]
    elements[1].position = stops[-1][0]
    elements[1].color = stops[-1][1]
    for position, color in stops[1:-1]:
        item = elements.new(position)
        item.color = color
    node.color_ramp.interpolation = "EASE"
    return node


def smoothstep(edge0: float, edge1: float, value: float) -> float:
    t = max(0.0, min(1.0, (value - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


def color_attribute(body: bpy.types.Object) -> dict:
    if any(body.vertex_groups.get(name) is None for name in ("head", "neck_01", "spine_03")):
        raise RuntimeError("Frozen v6 body is missing the expected head/neck/scapula-region weight groups")
    arm_groups = [
        group for group in body.vertex_groups
        if group.name in {
            "clavicle_l", "clavicle_r", "upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r"
        }
    ]
    hand_groups = [
        group for group in body.vertex_groups
        if group.name in {"hand_l", "hand_r"}
        or group.name.startswith(("thumb_", "index_", "middle_", "ring_", "pinky_"))
        and group.name[-1:] in {"l", "r"}
    ]
    if not arm_groups or not hand_groups:
        raise RuntimeError("Frozen v6 body is missing expected arm/hand weight groups")
    by_vertex = [dict() for _ in body.data.vertices]
    for vertex in body.data.vertices:
        weights = by_vertex[vertex.index]
        for assignment in vertex.groups:
            weights[assignment.group] = assignment.weight

    groups = {group.name: group.index for group in body.vertex_groups}
    attr = body.data.color_attributes.new(
        name=REGION_ATTRIBUTE,
        type="FLOAT_COLOR",
        domain="POINT",
    )
    hand_attr = body.data.color_attributes.new(
        name=HAND_ATTRIBUTE,
        type="FLOAT_COLOR",
        domain="POINT",
    )
    face_attr = body.data.color_attributes.new(
        name=FACE_ATTRIBUTE,
        type="FLOAT_COLOR",
        domain="POINT",
    )
    for vertex in body.data.vertices:
        weights = by_vertex[vertex.index]
        head = weights.get(groups["head"], 0.0)
        neck = weights.get(groups["neck_01"], 0.0)
        spine = weights.get(groups["spine_03"], 0.0)
        arms = max((weights.get(group.index, 0.0) for group in arm_groups), default=0.0)
        hands = max((weights.get(group.index, 0.0) for group in hand_groups), default=0.0)
        front = 1.0 - smoothstep(-0.14, -0.04, vertex.co.y)
        face_height = smoothstep(1.42, 1.45, vertex.co.z) * (1.0 - smoothstep(1.56, 1.59, vertex.co.z))
        face = head * front * face_height
        forehead_height = smoothstep(1.515, 1.53, vertex.co.z) * (1.0 - smoothstep(1.56, 1.575, vertex.co.z))
        forehead = face * forehead_height
        posterior = smoothstep(-0.01, 0.035, vertex.co.y)
        scapula_height = smoothstep(1.16, 1.23, vertex.co.z) * (1.0 - smoothstep(1.43, 1.48, vertex.co.z))
        scapula = spine * posterior * scapula_height
        attr.data[vertex.index].color = (head, neck, arms, scapula)
        hand_attr.data[vertex.index].color = (hands, hands, hands, 1.0)
        face_attr.data[vertex.index].color = (face, forehead, 0.0, 1.0)
    return {
        "regionAttribute": REGION_ATTRIBUTE,
        "handAttribute": HAND_ATTRIBUTE,
        "faceMaskAttribute": FACE_ATTRIBUTE,
        "domain": "POINT",
        "type": "FLOAT_COLOR",
        "channels": {
            "regions.R": "native head vertex group",
            "regions.G": "native neck_01 vertex group",
            "regions.B": "native clavicle/upperarm/lowerarm vertex groups",
            "regions.A": "native spine_03 weight, masked in source coordinates to posterior shoulder height as scapula coverage",
            "hands.RGB": "native hand and finger vertex groups",
            "face.R": "head weight multiplied by front-plane and chin-to-brow source-coordinate masks",
            "face.G": "face mask multiplied by narrow forehead source-coordinate mask",
        },
    "removedAfterBake": True,
    }


def make_authored_surface(body: bpy.types.Object, bark_image: bpy.types.Image) -> bpy.types.Material:
    material = bpy.data.materials.new("Shurale | ashwood plates and face planes | bake source")
    material.use_nodes = True
    material.diffuse_color = (0.062, 0.060, 0.055, 1.0)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    tex = make_node(material.node_tree, "ShaderNodeTexCoord", "Native object coordinates", (-1280, 260))
    regions = make_node(material.node_tree, "ShaderNodeVertexColor", "Native head neck arm weights", (-1280, -80))
    regions.layer_name = REGION_ATTRIBUTE
    split = make_node(material.node_tree, "ShaderNodeSeparateColor", "Head neck and arm masks", (-1060, -60))
    split.mode = "RGB"
    links.new(regions.outputs["Color"], split.inputs["Color"])
    head = split.outputs["Red"]
    neck = split.outputs["Green"]
    arms = split.outputs["Blue"]

    hands_attr = make_node(material.node_tree, "ShaderNodeVertexColor", "Native hand and finger weights", (-1280, -300))
    hands_attr.layer_name = HAND_ATTRIBUTE
    hands_bw = make_node(material.node_tree, "ShaderNodeRGBToBW", "Hand-region mask", (-1060, -300))
    links.new(hands_attr.outputs["Color"], hands_bw.inputs["Color"])
    hands = hands_bw.outputs["Val"]

    # The generated original bark image is the source of both visible color and
    # the shallow bake-only height. One object-space direction keeps the grain
    # coherent across the selected anatomy without transverse arm bands.
    def image_projection(name: str, vector, location: tuple[int, int]):
        image_node = make_node(material.node_tree, "ShaderNodeTexImage", name, location)
        image_node.image = bark_image
        image_node.projection = "BOX"
        image_node.projection_blend = 0.18
        image_node.extension = "REPEAT"
        image_node.interpolation = "Linear"
        links.new(vector, image_node.inputs["Vector"])
        return image_node

    body_mapping = make_node(material.node_tree, "ShaderNodeMapping", "Ashwood scale across the full body", (-1040, 380))
    body_mapping.vector_type = "POINT"
    body_mapping.inputs["Scale"].default_value = (1.0, 0.8, 0.55)
    links.new(tex.outputs["Object"], body_mapping.inputs["Vector"])
    bark_body = image_projection("Generated ash bark | full body", body_mapping.outputs["Vector"], (-800, 380))

    bark_tone = make_node(material.node_tree, "ShaderNodeMixRGB", "Darken to ash grey and umber", (-540, 300))
    bark_tone.blend_type = "MULTIPLY"
    bark_tone.inputs["Fac"].default_value = 1.0
    links.new(bark_body.outputs["Color"], bark_tone.inputs["Color1"])
    bark_tone.inputs["Color2"].default_value = (0.50, 0.52, 0.53, 1.0)

    # Face and posterior-shoulder masks are computed from frozen source
    # coordinates and packed into bake-only attributes for deterministic masks.
    face_attr = make_node(material.node_tree, "ShaderNodeVertexColor", "Source-coordinate face and forehead masks", (-1040, -580))
    face_attr.layer_name = FACE_ATTRIBUTE
    face_split = make_node(material.node_tree, "ShaderNodeSeparateColor", "Face and forehead masks", (-820, -560))
    face_split.mode = "RGB"
    links.new(face_attr.outputs["Color"], face_split.inputs["Color"])
    face_mask = face_split.outputs["Red"]
    forehead_mask = face_split.outputs["Green"]
    head_outer = math_node(material.node_tree, "SUBTRACT", "Bark on outer head only", (-140, -260), 0.0, 1.0)
    links.new(head, head_outer.inputs[0])
    links.new(face_mask, head_outer.inputs[1])
    scapula_mask = regions.outputs["Alpha"]

    bark_a = math_node(material.node_tree, "MAXIMUM", "Neck and arm bark", (80, -80))
    links.new(neck, bark_a.inputs[0])
    links.new(arms, bark_a.inputs[1])
    bark_b = math_node(material.node_tree, "MAXIMUM", "Add hand bark", (280, -60))
    links.new(bark_a.outputs[0], bark_b.inputs[0])
    links.new(hands, bark_b.inputs[1])
    bark_c = math_node(material.node_tree, "MAXIMUM", "Add posterior shoulder bark", (480, -40))
    links.new(bark_b.outputs[0], bark_c.inputs[0])
    links.new(scapula_mask, bark_c.inputs[1])
    bark_mask = math_node(material.node_tree, "MAXIMUM", "Add outer head bark", (680, -20))
    links.new(bark_c.outputs[0], bark_mask.inputs[0])
    links.new(head_outer.outputs[0], bark_mask.inputs[1])
    organic_plate_mask = make_node(material.node_tree, "ShaderNodeMapRange", "Ease localized bark growth", (880, -20))
    organic_plate_mask.interpolation_type = "SMOOTHSTEP"
    organic_plate_mask.clamp = True
    organic_plate_mask.inputs["From Min"].default_value = 0.0
    organic_plate_mask.inputs["From Max"].default_value = 1.0
    organic_plate_mask.inputs["To Min"].default_value = 0.0
    organic_plate_mask.inputs["To Max"].default_value = 1.0
    links.new(bark_mask.outputs[0], organic_plate_mask.inputs["Value"])

    ash_skin = make_node(material.node_tree, "ShaderNodeRGB", "Neutral ash-brown face and body base", (-520, 80))
    ash_skin.outputs["Color"].default_value = (0.050, 0.052, 0.052, 1.0)
    whole_body_grain = make_node(material.node_tree, "ShaderNodeMixRGB", "Aged ashwood fine grain across the full body", (-180, 280))
    whole_body_grain.inputs["Fac"].default_value = 0.28
    links.new(ash_skin.outputs["Color"], whole_body_grain.inputs["Color1"])
    links.new(bark_tone.outputs["Color"], whole_body_grain.inputs["Color2"])
    plate_strength = math_node(material.node_tree, "MULTIPLY", "Localized plate coverage up to 78 percent", (1080, -20), None, 0.78)
    links.new(organic_plate_mask.outputs["Result"], plate_strength.inputs[0])
    barked_body = make_node(material.node_tree, "ShaderNodeMixRGB", "Coarse ashwood plates on selected anatomy", (1280, 300))
    links.new(plate_strength.outputs[0], barked_body.inputs["Fac"])
    links.new(whole_body_grain.outputs["Color"], barked_body.inputs["Color1"])
    links.new(bark_tone.outputs["Color"], barked_body.inputs["Color2"])

    # The face uses the shared, low-strength whole-body base; only forehead
    # creases are added so brow, nose and lip planes remain legible.
    face_surface = make_node(material.node_tree, "ShaderNodeMixRGB", "Preserve textured ashwood face planes", (100, 120))
    face_surface.inputs["Fac"].default_value = 0.0
    links.new(whole_body_grain.outputs["Color"], face_surface.inputs["Color1"])
    links.new(bark_tone.outputs["Color"], face_surface.inputs["Color2"])
    wrinkle = make_node(material.node_tree, "ShaderNodeTexWave", "Sparse horizontal forehead wrinkles", (-820, -1190))
    wrinkle.wave_type = "BANDS"
    wrinkle.bands_direction = "Z"
    wrinkle.inputs["Scale"].default_value = 28.0
    wrinkle.inputs["Distortion"].default_value = 1.5
    wrinkle.inputs["Detail"].default_value = 2.0
    wrinkle.inputs["Detail Scale"].default_value = 1.2
    links.new(tex.outputs["Object"], wrinkle.inputs["Vector"])
    wrinkle_lines = ramp(material.node_tree, "Narrow, broken forehead crease lines", (-580, -1170), [
        (0.46, (0.0, 0.0, 0.0, 1.0)),
        (0.50, (0.72, 0.72, 0.72, 1.0)),
        (0.54, (0.0, 0.0, 0.0, 1.0)),
    ])
    links.new(wrinkle.outputs["Fac"], wrinkle_lines.inputs["Fac"])
    wrinkle_mask = math_node(material.node_tree, "MULTIPLY", "Forehead lines by head and plane weights", (-360, -900))
    links.new(wrinkle_lines.outputs["Color"], wrinkle_mask.inputs[0])
    links.new(forehead_mask, wrinkle_mask.inputs[1])
    face_creases = make_node(material.node_tree, "ShaderNodeMixRGB", "Age the forehead planes", (320, 100))
    links.new(wrinkle_mask.outputs[0], face_creases.inputs["Fac"])
    links.new(face_surface.outputs["Color"], face_creases.inputs["Color1"])
    face_creases.inputs["Color2"].default_value = (0.026, 0.022, 0.018, 1.0)
    final_color = make_node(material.node_tree, "ShaderNodeMixRGB", "Face planes over selective bark", (980, 240))
    links.new(face_mask, final_color.inputs["Fac"])
    links.new(barked_body.outputs["Color"], final_color.inputs["Color1"])
    links.new(face_creases.outputs["Color"], final_color.inputs["Color2"])

    # Bake the same image as low-strength whole-body relief, with a stronger
    # but millimeter-bounded contribution inside the anatomical plate regions.
    bark_bw = make_node(material.node_tree, "ShaderNodeRGBToBW", "Bark light-to-cavity height source", (-300, 520))
    links.new(bark_tone.outputs["Color"], bark_bw.inputs["Color"])
    bark_centered = math_node(material.node_tree, "SUBTRACT", "Center bark height", (-80, 520), 0.0, 0.5)
    links.new(bark_bw.outputs["Val"], bark_centered.inputs[0])
    base_height = math_node(material.node_tree, "MULTIPLY", "Fine grain relief across skin and anatomy", (140, 520), None, 0.24)
    links.new(bark_centered.outputs[0], base_height.inputs[0])
    plate_height = math_node(material.node_tree, "MULTIPLY", "Coarse plate relief on selected anatomy", (140, 360))
    links.new(bark_centered.outputs[0], plate_height.inputs[0])
    links.new(organic_plate_mask.outputs["Result"], plate_height.inputs[1])
    plate_height_scale = math_node(material.node_tree, "MULTIPLY", "Plate relief contribution", (360, 360), None, 0.62)
    links.new(plate_height.outputs[0], plate_height_scale.inputs[0])
    wrinkle_depth = math_node(material.node_tree, "MULTIPLY", "Shallow forehead crease height", (140, -720), 0.0, -0.30)
    links.new(wrinkle_mask.outputs[0], wrinkle_depth.inputs[0])
    height_delta = math_node(material.node_tree, "ADD", "Whole-body grain plus selected plate relief", (360, 480))
    links.new(base_height.outputs[0], height_delta.inputs[0])
    links.new(plate_height_scale.outputs[0], height_delta.inputs[1])
    facial_relief = math_node(material.node_tree, "ADD", "Add authored forehead creases", (560, 360))
    links.new(height_delta.outputs[0], facial_relief.inputs[0])
    links.new(wrinkle_depth.outputs[0], facial_relief.inputs[1])
    height = math_node(material.node_tree, "ADD", "Neutral height outside masked regions", (560, 480), 0.5)
    links.new(facial_relief.outputs[0], height.inputs[0])

    shader = make_node(material.node_tree, "ShaderNodeBsdfPrincipled", "Temporary source material", (1220, 240))
    shader.inputs["Roughness"].default_value = 0.82
    shader.inputs["Metallic"].default_value = 0.0
    bump = make_node(material.node_tree, "ShaderNodeBump", "Shallow bark and wrinkle relief for bake", (980, -180))
    bump.inputs["Strength"].default_value = 0.60
    bump.inputs["Distance"].default_value = 0.0055
    links.new(height.outputs[0], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    links.new(final_color.outputs["Color"], shader.inputs["Base Color"])
    output = make_node(material.node_tree, "ShaderNodeOutputMaterial", "Temporary bake output", (1460, 240))
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material

def set_bake_target(material: bpy.types.Material, image: bpy.types.Image) -> None:
    nodes = material.node_tree.nodes
    for node in nodes:
        node.select = False
    target = nodes.new("ShaderNodeTexImage")
    target.name = f"BakeTarget_{image.name}"
    target.label = "Temporary UV bake target"
    target.image = image
    target.select = True
    nodes.active = target


def save_image(image: bpy.types.Image, path: Path) -> None:
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    image.pack()


def bake_maps(body: bpy.types.Object, rig: bpy.types.Object, out_dir: Path) -> dict:
    if not body.data.uv_layers or body.data.uv_layers.active is None:
        raise RuntimeError("The frozen v6 body has no active UV map")
    if body.data.uv_layers.active.name != "UVMap":
        raise RuntimeError(f"Unexpected active UV map: {body.data.uv_layers.active.name}")
    if len(body.data.materials) < 2:
        raise RuntimeError("Expected separate body and eye material slots in the frozen v6 source")

    scene = bpy.context.scene
    previous_engine = scene.render.engine
    previous_pose_position = rig.data.pose_position
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = False
    scene.render.bake.margin = 12
    scene.render.bake.use_clear = True
    scene.render.bake.normal_space = "TANGENT"
    scene.render.bake.normal_r = "POS_X"
    scene.render.bake.normal_g = "POS_Y"
    scene.render.bake.normal_b = "POS_Z"
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body

    materials = [material for material in body.data.materials if material is not None]
    outputs = {}
    try:
        maps = (
            ("albedo", "shurale-surface-v3-albedo-2k.png", ALBEDO_SIZE, "sRGB", "DIFFUSE", {"COLOR"}),
            ("normal", "shurale-surface-v3-normal-tangent-2k.png", NORMAL_SIZE, "Non-Color", "NORMAL", None),
            ("ao", "shurale-surface-v3-ao-1k.png", AO_SIZE, "Non-Color", "AO", None),
        )
        for key, filename, size, color_space, bake_type, pass_filter in maps:
            image = bpy.data.images.new(
                f"Shurale_SurfaceV3_{key.upper()}",
                width=size,
                height=size,
                alpha=False,
                float_buffer=False,
            )
            image.colorspace_settings.name = color_space
            for material in materials:
                set_bake_target(material, image)
            scene.cycles.samples = 32 if key == "ao" else 1
            kwargs = {"type": bake_type, "margin": 12, "use_clear": True}
            if pass_filter is not None:
                kwargs["pass_filter"] = pass_filter
            bpy.ops.object.bake(**kwargs)
            path = out_dir / filename
            save_image(image, path)
            outputs[key] = {
                "file": filename,
                "sizePx": [size, size],
                "colorSpace": color_space,
                "sha256": sha256(path),
            }
        for material in materials:
            for node in list(material.node_tree.nodes):
                if node.name.startswith("BakeTarget_"):
                    material.node_tree.nodes.remove(node)
    finally:
        rig.data.pose_position = previous_pose_position
        scene.render.engine = previous_engine
        bpy.context.view_layer.update()

    return outputs


def make_gltf_occlusion_group() -> bpy.types.NodeTree:
    name = "glTF Material Output"
    group = bpy.data.node_groups.get(name)
    if group is None:
        group = bpy.data.node_groups.new(name, "ShaderNodeTree")
        group.interface.new_socket("Occlusion", socket_type="NodeSocketFloat")
        thickness = group.interface.new_socket("Thickness", socket_type="NodeSocketFloat")
        thickness.default_value = 0.0
        group.nodes.new("NodeGroupOutput")
        group_input = group.nodes.new("NodeGroupInput")
        group_input.location = (-200, 0)
    return group


def finalize_gltf_material(body: bpy.types.Object, images: dict[str, bpy.types.Image]) -> None:
    material = bpy.data.materials.new("Shurale | ashwood plates and readable face | surface v3")
    material.use_nodes = True
    material.diffuse_color = (0.062, 0.060, 0.055, 1.0)
    material["albedo_texture"] = images["albedo"].name
    material["normal_texture"] = images["normal"].name
    material["occlusion_texture"] = images["ao"].name
    material["mask_semantics"] = "glTF grayscale ambient occlusion; cavities dark, exposed surfaces light"
    material["normal_map_semantics"] = "Tangent space, +Y green channel, POS_X/POS_Y/POS_Z bake swizzle; no channel flip"
    material["bake_source"] = "v6 stable UVMap; generated ash-bark albedo box-projected across the whole body at low strength and reinforced over native anatomical masks; image-derived relief and limited face wrinkles"

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = make_node(material.node_tree, "ShaderNodeOutputMaterial", "Material Output", (620, 100))
    shader = make_node(material.node_tree, "ShaderNodeBsdfPrincipled", "glTF metallic-roughness surface", (360, 100))
    set_socket(shader, "Roughness", 0.82)
    set_socket(shader, "Metallic", 0.0)
    set_socket(shader, "Specular IOR Level", 0.24)

    albedo = make_node(material.node_tree, "ShaderNodeTexImage", "Albedo sRGB 2K", (-440, 260))
    albedo.image = images["albedo"]
    albedo.interpolation = "Linear"
    links.new(albedo.outputs["Color"], shader.inputs["Base Color"])

    normal_image = make_node(material.node_tree, "ShaderNodeTexImage", "Tangent normal Non-Color 2K", (-440, 40))
    normal_image.image = images["normal"]
    normal_image.interpolation = "Linear"
    normal_image.image.colorspace_settings.name = "Non-Color"
    normal = make_node(material.node_tree, "ShaderNodeNormalMap", "Tangent space normal (+Y)", (-160, 40))
    normal.space = "TANGENT"
    normal.uv_map = "UVMap"
    normal.inputs["Strength"].default_value = 1.0
    links.new(normal_image.outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], shader.inputs["Normal"])

    ao_image = make_node(material.node_tree, "ShaderNodeTexImage", "glTF occlusion Non-Color 1K", (-440, -200))
    ao_image.image = images["ao"]
    ao_image.interpolation = "Linear"
    ao_image.image.colorspace_settings.name = "Non-Color"
    settings = make_node(material.node_tree, "ShaderNodeGroup", "glTF Material Output | Occlusion", (-140, -200))
    settings.node_tree = make_gltf_occlusion_group()
    links.new(ao_image.outputs["Color"], settings.inputs["Occlusion"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    if not body.data.materials:
        raise RuntimeError("The frozen body lost its material slots")
    body.data.materials[0] = material
    region_attr = body.data.color_attributes.get(REGION_ATTRIBUTE)
    if region_attr is None:
        raise RuntimeError("Bake-only region attribute was lost before material finalization")
    body.data.color_attributes.remove(region_attr)
    hand_attr = body.data.color_attributes.get(HAND_ATTRIBUTE)
    if hand_attr is None:
        raise RuntimeError("Bake-only hand attribute was lost before material finalization")
    body.data.color_attributes.remove(hand_attr)
    face_attr = body.data.color_attributes.get(FACE_ATTRIBUTE)
    if face_attr is None:
        raise RuntimeError("Bake-only face-mask attribute was lost before material finalization")
    body.data.color_attributes.remove(face_attr)
    for stale_material in list(bpy.data.materials):
        if stale_material.name.endswith("| bake source") and stale_material.users == 0:
            bpy.data.materials.remove(stale_material)
    bpy.context.view_layer.update()


def render_previews(out_dir: Path) -> list[str]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.eevee.taa_render_samples = 48
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1500
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.film_transparent = False
    camera = scene.camera
    if camera is None:
        raise RuntimeError("Frozen v6 scene is missing its studio camera")

    targets = [
        ("overview", (0.0, -7.0, 1.18), (0.0, -0.04, 1.18), 3.15),
        ("face", (1.4, -5.5, 2.55), (0.0, -0.38, 1.82), 1.58),
        ("hands", (0.0, -3.4, 2.25), (0.06, -0.42, 1.72), 0.90),
    ]
    names = []
    for suffix, location, target, scale in targets:
        camera.data.ortho_scale = scale
        camera.location = location
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(out_dir / f"shurale-surface-v3-{suffix}.png")
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)
        names.append(Path(scene.render.filepath).name)
    return names


def main() -> None:
    if bpy.app.version_string != EXPECTED_BLENDER_VERSION:
        raise RuntimeError(f"Expected Blender {EXPECTED_BLENDER_VERSION}, got {bpy.app.version_string}")
    out_dir = Path(__file__).resolve().parent
    source_path = Path(bpy.data.filepath).resolve()
    if source_path.name != "shurale-character-look-v6-input.blend":
        raise RuntimeError(f"Expected the copied v6 input blend, got {source_path}")
    input_hash = sha256(source_path)
    if input_hash != EXPECTED_INPUT_SHA256:
        raise RuntimeError(f"Frozen v6 input SHA mismatch: {input_hash}")
    concept_path = out_dir.parent / "shurale-concepts" / "modern-horror-anatomy-v1.png"
    if not concept_path.is_file() or sha256(concept_path) != EXPECTED_CONCEPT_SHA256:
        raise RuntimeError("The named internal concept reference is missing or changed")
    bark_path = out_dir / "source" / "original-ashwood-bark-albedo.png"
    if not bark_path.is_file() or sha256(bark_path) != EXPECTED_BARK_SHA256:
        raise RuntimeError("The copied generated bark albedo is missing or changed")
    body = bpy.data.objects.get(BODY_NAME)
    rig = body.parent if body is not None else None
    if body is None or rig is None or rig.type != "ARMATURE" or rig.name != RIG_OBJECT_NAME:
        raise RuntimeError("Frozen v6 source does not contain the expected body and native rig")
    if len(body.data.vertices) != 19158 or len(body.data.polygons) != 18486:
        raise RuntimeError("Frozen v6 visible mesh topology changed; refusing to bake against a new input")

    bark_image = bpy.data.images.load(str(bark_path), check_existing=False)
    bark_image.name = "Original generated ashwood bark albedo | source"
    bark_image.colorspace_settings.name = "sRGB"
    regions = color_attribute(body)
    source_material = make_authored_surface(body, bark_image)
    body.data.materials[0] = source_material
    textures = bake_maps(body, rig, out_dir)
    images = {
        key: bpy.data.images.load(str(out_dir / data["file"]), check_existing=False)
        for key, data in textures.items()
    }
    for image in images.values():
        image.pack()
    bark_image.pack()
    finalize_gltf_material(body, images)

    previews = render_previews(out_dir)
    blend_path = out_dir / "shurale-surface-v3.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    outputs = [
        "source/original-ashwood-bark-albedo.png",
        textures["albedo"]["file"], textures["normal"]["file"], textures["ao"]["file"],
        *previews, blend_path.name,
    ]
    manifest = {
        "candidateStatus": "source-only material pass for review; not runtime promotion or final art acceptance",
        "blenderVersion": bpy.app.version_string,
        "source": {
            "inputBlend": source_path.name,
            "inputBlendSHA256": input_hash,
            "geometryOrRigModified": False,
            "bodyObject": BODY_NAME,
            "rigObject": rig.name,
            "nativeRigData": rig.data.name,
            "uvMap": body.data.uv_layers.active.name,
            "vertexCount": len(body.data.vertices),
            "polygonCount": len(body.data.polygons),
            "conceptReference": str(concept_path.relative_to(out_dir.parents[4])),
            "conceptSHA256": EXPECTED_CONCEPT_SHA256,
            "referencePixelsCopied": False,
        },
        "generatedBarkImage": {
            "file": "source/original-ashwood-bark-albedo.png",
            "sha256": EXPECTED_BARK_SHA256,
            "resolutionPx": list(bark_image.size),
            "generatedBy": "image_gen.imagegen, requested by root agent",
            "generationDate": "2026-10-18",
            "model": "not reported",
            "seed": "not reported",
            "prompt": IMAGEGEN_PROMPT,
            "imageCopiedUnmodified": True,
            "folkloreAuthority": False,
            "licenseOrArtAcceptance": "not implied by image generation; project and art acceptance remain open",
        },
        "method": {
            "description": "Original generated bark albedo is box-projected in one object-space direction and baked to the frozen v6 UVMap as low-strength ashwood grain across the whole body. Native head/neck/clavicle/arm/hand weights select a stronger ashwood-plate layer; posterior spine_03 and local height masks select the scapulae. The frontal face retains its planes over the shared textured base with restrained forehead creases. Image-derived relief is baked to tangent normals. No highpoly, runtime geometry, rig or pose changes.",
            "regionMasks": regions,
            "bakePose": "native armature rest pose; original stored working-pose state restored before preview and save",
            "normalConvention": "Tangent space, POS_X/POS_Y/POS_Z swizzle; +Y green channel; no normal channel flip",
            "gltfOcclusionPath": "image red channel -> glTF Material Output group Occlusion input; grayscale cavities dark, exposed surfaces light",
            "finalShader": "sRGB albedo -> Principled Base Color; Non-Color tangent normal -> NormalMap(TANGENT,+Y) -> Principled Normal; Non-Color AO -> glTF Material Output.Occlusion",
            "coverageBlend": {
                "wholeBodyBaseMix": 0.28,
                "localizedPlateMixFactor": 0.78,
                "maximumEffectiveBarkMix": 0.8416,
                "localizedMaskInterpolation": "SMOOTHSTEP over native weight and source-coordinate masks from 0 to 1",
                "faceUsesWholeBodyBase": True,
            },
            "normalRelief": {
                "wholeBodyHeightFactor": 0.24,
                "localizedPlateHeightFactor": 0.62,
                "bumpStrength": 0.60,
                "bumpDistanceBlendUnits": 0.0055,
                "normalConvention": "Tangent space POS_X/POS_Y/POS_Z; +Y green; no channel flip",
            },
            "coverageIntent": "low-strength aged ashwood grain across the whole body, with stronger broad plates on the outer head, neck, clavicle/scapula, arms and hands; face planes stay readable over the same textured base",
        },
        "textures": textures,
        "previews": previews,
        "outputs": outputs,
        "runtimePromotion": False,
        "artAcceptance": "pending independent/root review; material appearance in previews only",
    }
    for filename in outputs:
        path = out_dir / filename
        if not path.is_file():
            raise RuntimeError(f"Declared output does not exist: {filename}")
    manifest["outputSHA256"] = {filename: sha256(out_dir / filename) for filename in outputs}
    manifest_path = out_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Surface bake complete: {len(outputs)} outputs in {out_dir}")

if __name__ == "__main__":
    main()
