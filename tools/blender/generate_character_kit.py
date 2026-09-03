"""Generate the project-original low-poly character kit for URMAN.

Run with Blender 4.5 LTS:
  blender --background --python tools/blender/generate_character_kit.py -- --root <repo>

The kit intentionally contains readable silhouettes and a small animation-safe
rig, not final facial art. It is a real GLB production input with deterministic
LOD1 meshes, provenance, Idle/Tension clips and no physics geometry. Godot
selects one character prefix per authored zone.
"""

from __future__ import annotations

import argparse
import math
from pathlib import Path

import bpy


CHARACTERS = (
    ("Mansur", (0.30, 0.22, 0.17, 1.0), (0.42, 0.31, 0.22, 1.0), True, True),
    ("Gulsina", (0.34, 0.28, 0.31, 1.0), (0.52, 0.37, 0.28, 1.0), False, False),
    ("Alsu", (0.20, 0.29, 0.32, 1.0), (0.50, 0.35, 0.24, 1.0), False, False),
    ("TimurHazrat", (0.25, 0.32, 0.29, 1.0), (0.43, 0.36, 0.24, 1.0), True, True),
    ("CouncilElder", (0.34, 0.28, 0.22, 1.0), (0.43, 0.34, 0.23, 1.0), True, True),
    ("CouncilWitness", (0.24, 0.31, 0.34, 1.0), (0.50, 0.37, 0.28, 1.0), False, False),
    ("Naila", (0.34, 0.27, 0.23, 1.0), (0.56, 0.42, 0.28, 1.0), False, False),
    ("ArchiveClerk", (0.27, 0.30, 0.32, 1.0), (0.48, 0.38, 0.29, 1.0), False, False),
    ("PactKeeper", (0.24, 0.22, 0.21, 1.0), (0.57, 0.40, 0.27, 1.0), True, True),
)


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    tokens = []
    if "--" in __import__("sys").argv:
        tokens = __import__("sys").argv[__import__("sys").argv.index("--") + 1 :]
    return parser.parse_args(tokens)


def material(name: str, color: tuple[float, float, float, float], roughness: float = 0.9) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.diffuse_color = color
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    return result


def tag(obj: bpy.types.Object, asset_id: str, budget: int, lod_status: str = "LOD0") -> None:
    obj["urman_asset_id"] = asset_id
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["triangle_budget"] = budget
    obj["collision"] = "none"
    obj["lod_status"] = lod_status


def cube(
    name: str,
    size: tuple[float, float, float],
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def faceted_prism(
    name: str,
    size: tuple[float, float, float],
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
    bottom_ratio: float = 1.0,
    top_ratio: float = 1.0,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    vertices: int = 8,
) -> bpy.types.Object:
    """Create a restrained faceted taper for readable human proportions."""
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=bottom_ratio,
        radius2=top_ratio,
        depth=1.0,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.rotation_euler = rotation
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def faceted_eye_with_brow(
    name: str,
    eye_location: tuple[float, float, float],
    eye_size: tuple[float, float, float],
    brow_location: tuple[float, float, float],
    brow_size: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
) -> bpy.types.Object:
    """Keep the eye/brow pair in one low-poly LOD mesh and material slot."""
    eye = faceted_prism(
        name,
        eye_size,
        eye_location,
        surface,
        asset_id,
        96,
        bottom_ratio=0.86,
        top_ratio=0.96,
        rotation=(0.0, 0.0, math.radians(22.5)),
        vertices=6,
    )
    brow = faceted_prism(
        f"{name}_BrowPart",
        brow_size,
        brow_location,
        surface,
        asset_id,
        48,
        bottom_ratio=0.84,
        top_ratio=0.96,
        rotation=(0.0, 0.0, math.radians(8.0) if "Left" in name else math.radians(-8.0)),
        vertices=6,
    )
    bpy.ops.object.select_all(action="DESELECT")
    eye.select_set(True)
    brow.select_set(True)
    bpy.context.view_layer.objects.active = eye
    bpy.ops.object.join()
    eye.name = name
    tag(eye, asset_id, 144)
    return eye


def sphere(
    name: str,
    radius: float,
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=radius, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def hat(
    name: str,
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cone_add(
        vertices=8,
        radius1=0.26,
        radius2=0.16,
        depth=0.14,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(surface)
    tag(obj, asset_id, 300)
    return obj


def empty_anchor(name: str, location: tuple[float, float, float]) -> bpy.types.Object:
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=location)
    obj = bpy.context.object
    obj.name = name
    obj["urman_asset_id"] = "character.anchor"
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["collision"] = "none"
    obj["anchor_policy"] = "ground-origin"
    return obj


def create_character(
    prefix: str,
    coat_color: tuple[float, float, float, float],
    accent_color: tuple[float, float, float, float],
    has_hat: bool,
    has_beard: bool,
    origin_x: float,
    materials: dict[str, bpy.types.Material],
) -> None:
    asset_id = "character.fullgame.lowpoly.v1"
    x = origin_x
    z = 0.0
    empty_anchor(f"{prefix}_Anchor", (x, 0.0, z))
    faceted_prism(
        f"{prefix}_Body_LOD0",
        (0.62, 0.42, 1.15),
        (x, 0.0, 0.78),
        material(f"{prefix}Coat", coat_color),
        asset_id,
        512,
        bottom_ratio=0.90,
        top_ratio=1.0,
    )
    faceted_prism(
        f"{prefix}_ShoulderWrap_LOD0",
        (0.70, 0.48, 0.28),
        (x, -0.02, 1.03),
        material(f"{prefix}Accent", accent_color),
        asset_id,
        256,
        bottom_ratio=0.92,
        top_ratio=1.0,
    )
    faceted_prism(
        f"{prefix}_SleeveLeft_LOD0",
        (0.22, 0.34, 0.70),
        (x - 0.40, 0.0, 0.82),
        material(f"{prefix}Coat", coat_color),
        asset_id,
        256,
        bottom_ratio=0.74,
        top_ratio=1.0,
        rotation=(0.0, 0.18, 0.0),
    )
    faceted_prism(
        f"{prefix}_SleeveRight_LOD0",
        (0.22, 0.34, 0.70),
        (x + 0.40, 0.0, 0.82),
        material(f"{prefix}Coat", coat_color),
        asset_id,
        256,
        bottom_ratio=0.74,
        top_ratio=1.0,
        rotation=(0.0, -0.18, 0.0),
    )
    trouser_surface = material(
        f"{prefix}Trousers",
        tuple(max(0.0, channel * 0.72) for channel in coat_color[:3]) + (1.0,),
    )
    faceted_prism(
        f"{prefix}_TrouserLeft_LOD0",
        (0.22, 0.30, 0.52),
        (x - 0.17, 0.0, 0.25),
        trouser_surface,
        asset_id,
        256,
        bottom_ratio=0.76,
        top_ratio=1.0,
    )
    faceted_prism(
        f"{prefix}_TrouserRight_LOD0",
        (0.22, 0.30, 0.52),
        (x + 0.17, 0.0, 0.25),
        trouser_surface,
        asset_id,
        256,
        bottom_ratio=0.76,
        top_ratio=1.0,
    )
    cube(f"{prefix}_BootLeft_LOD0", (0.26, 0.38, 0.16), (x - 0.17, -0.03, 0.06), materials["boot"], asset_id, 128)
    cube(f"{prefix}_BootRight_LOD0", (0.26, 0.38, 0.16), (x + 0.17, -0.03, 0.06), materials["boot"], asset_id, 128)
    sphere(f"{prefix}_Head_LOD0", 0.24, (x, 0.0, 1.62), materials["skin"], asset_id, 600)
    sphere(f"{prefix}_Hair_LOD0", 0.245, (x, -0.01, 1.78), materials["hair"], asset_id, 600)
    cube(f"{prefix}_ScarfBand_LOD0", (0.64, 0.46, 0.08), (x, -0.02, 1.32), material(f"{prefix}Scarf", accent_color), asset_id, 128)
    faceted_eye_with_brow(
        f"{prefix}_FaceEyeLeft_LOD0",
        (x - 0.09, -0.265, 1.66),
        (0.09, 0.055, 0.068),
        (x - 0.09, -0.252, 1.708),
        (0.12, 0.042, 0.026),
        materials["eye"],
        asset_id,
    )
    faceted_eye_with_brow(
        f"{prefix}_FaceEyeRight_LOD0",
        (x + 0.09, -0.265, 1.66),
        (0.09, 0.055, 0.068),
        (x + 0.09, -0.252, 1.708),
        (0.12, 0.042, 0.026),
        materials["eye"],
        asset_id,
    )
    faceted_prism(
        f"{prefix}_FaceNose_LOD0",
        (0.075, 0.105, 0.10),
        (x, -0.272, 1.60),
        materials["skin"],
        asset_id,
        96,
        bottom_ratio=0.76,
        top_ratio=0.94,
        rotation=(0.0, 0.0, math.radians(22.5)),
        vertices=6,
    )
    faceted_prism(
        f"{prefix}_FaceMouth_LOD0",
        (0.145, 0.055, 0.038),
        (x, -0.282, 1.535),
        materials["eye"],
        asset_id,
        96,
        bottom_ratio=0.82,
        top_ratio=0.94,
        rotation=(0.0, 0.0, math.radians(22.5)),
        vertices=6,
    )
    if has_beard:
        faceted_prism(
            f"{prefix}_FaceBeard_LOD0",
            (0.23, 0.06, 0.17),
            (x, -0.255, 1.53),
            materials["hair"],
            asset_id,
            192,
            bottom_ratio=0.84,
            top_ratio=0.96,
            rotation=(0.0, 0.0, math.radians(22.5)),
            vertices=8,
        )
    if has_hat:
        hat(f"{prefix}_Hat_LOD0", (x, 0.0, 1.88), materials["hair"], asset_id)


def _bone_for_mesh(name: str) -> str:
    """Map a generated mesh to the smallest useful presentation bone."""
    if any(token in name for token in ("Head", "Hair", "Face", "Hat")):
        return "Head"
    if "SleeveLeft" in name:
        return "Arm.L"
    if "SleeveRight" in name:
        return "Arm.R"
    if "TrouserLeft" in name or "BootLeft" in name:
        return "Leg.L"
    if "TrouserRight" in name or "BootRight" in name:
        return "Leg.R"
    return "Spine"


def _make_action(
    armature: bpy.types.Object,
    prefix: str,
    name: str,
    frames: tuple[int, ...],
    poses: dict[str, tuple[tuple[float, float, float], ...]],
) -> bpy.types.Action:
    action = bpy.data.actions.new(f"{prefix}_{name}")
    action.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = action
    for bone_name, rotations in poses.items():
        bone = armature.pose.bones[bone_name]
        bone.rotation_mode = "XYZ"
        for frame, rotation in zip(frames, rotations, strict=True):
            bone.rotation_euler = rotation
            bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone_name)

    for curve in action.fcurves:
        curve.modifiers.new(type="CYCLES")
        for key in curve.keyframe_points:
            key.interpolation = "BEZIER"
    return action


def add_animation_rig(prefix: str, origin_x: float) -> bpy.types.Object:
    """Add a lightweight bone rig and two authored presentation clips.

    The generated meshes remain separate low-poly pieces so LOD provenance is
    inspectable. Parenting them to bones preserves that ownership while making
    the GLB usable by Godot's Skeleton3D/AnimationPlayer importer.
    """
    bpy.ops.object.armature_add(enter_editmode=True, location=(origin_x, 0.0, 0.0))
    armature = bpy.context.object
    armature.name = f"{prefix}_Rig"
    armature.data.name = f"{prefix}_RigData"
    armature.data.display_type = "BBONE"
    armature.data["urman_asset_id"] = "character.fullgame.rig.v1"
    armature.data["license"] = "Project-original"
    armature.data["collision"] = "none"
    armature.data["animation_clips"] = "Idle,Tension"

    edit_bones = armature.data.edit_bones
    root = edit_bones.new("Root")
    root.head = (0.0, 0.0, 0.0)
    root.tail = (0.0, 0.0, 0.12)
    spine = edit_bones.new("Spine")
    spine.head = (0.0, 0.0, 0.28)
    spine.tail = (0.0, 0.0, 1.28)
    spine.parent = root
    head = edit_bones.new("Head")
    head.head = (0.0, 0.0, 1.28)
    head.tail = (0.0, 0.0, 1.95)
    head.parent = spine
    arm_l = edit_bones.new("Arm.L")
    arm_l.head = (-0.28, 0.0, 1.12)
    arm_l.tail = (-0.48, 0.0, 0.62)
    arm_l.parent = spine
    arm_r = edit_bones.new("Arm.R")
    arm_r.head = (0.28, 0.0, 1.12)
    arm_r.tail = (0.48, 0.0, 0.62)
    arm_r.parent = spine
    leg_l = edit_bones.new("Leg.L")
    leg_l.head = (-0.16, 0.0, 0.50)
    leg_l.tail = (-0.16, 0.0, 0.04)
    leg_l.parent = root
    leg_r = edit_bones.new("Leg.R")
    leg_r.head = (0.16, 0.0, 0.50)
    leg_r.tail = (0.16, 0.0, 0.04)
    leg_r.parent = root
    bpy.ops.object.mode_set(mode="OBJECT")

    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH" or not obj.name.startswith(f"{prefix}_"):
            continue
        world_matrix = obj.matrix_world.copy()
        obj.parent = armature
        obj.parent_type = "BONE"
        obj.parent_bone = _bone_for_mesh(obj.name)
        obj.matrix_world = world_matrix

    frames = (1, 20, 40)
    _make_action(
        armature,
        prefix,
        "Idle",
        frames,
        {
            "Spine": ((0.0, -0.012, 0.0), (0.0, 0.016, 0.0), (0.0, -0.012, 0.0)),
            "Head": ((0.0, 0.008, 0.0), (0.0, -0.012, 0.0), (0.0, 0.008, 0.0)),
            "Arm.L": ((0.0, 0.0, -0.018), (0.0, 0.0, 0.012), (0.0, 0.0, -0.018)),
            "Arm.R": ((0.0, 0.0, 0.018), (0.0, 0.0, -0.012), (0.0, 0.0, 0.018)),
        },
    )
    tension = _make_action(
        armature,
        prefix,
        "Tension",
        frames,
        {
            "Spine": ((0.0, -0.025, 0.0), (0.0, 0.035, 0.0), (0.0, -0.025, 0.0)),
            "Head": ((0.025, 0.016, 0.0), (-0.035, -0.022, 0.0), (0.025, 0.016, 0.0)),
            "Arm.L": ((0.0, 0.0, -0.05), (0.0, 0.0, 0.028), (0.0, 0.0, -0.05)),
            "Arm.R": ((0.0, 0.0, 0.05), (0.0, 0.0, -0.028), (0.0, 0.0, 0.05)),
        },
    )
    # Blender's glTF exporter emits the active action for every armature, but
    # only discovers additional actions on a multi-armature scene through NLA
    # tracks. Keep Tension as a single-strip, non-muted track so each imported
    # character carries both presentation clips without relying on the
    # single-armature export shortcut.
    tension_track = armature.animation_data.nla_tracks.new()
    tension_track.name = f"{prefix}_Tension"
    tension_strip = tension_track.strips.new(f"{prefix}_Tension", 1, tension)
    tension_strip.action_frame_start = 1
    tension_strip.action_frame_end = 40
    tension_strip.frame_start = 1
    tension_strip.frame_end = 40
    armature.animation_data.action = bpy.data.actions.get(f"{prefix}_Idle")
    armature["urman_asset_id"] = "character.fullgame.rig.v1"
    armature["license"] = "Project-original"
    armature["scale_meters"] = 1.0
    armature["collision"] = "none"
    armature["animation_policy"] = "authored Idle/Tension clips; final expression and performance review remains open"
    return armature


def generate_lod1_variants() -> int:
    created = 0
    for source in [obj for obj in list(bpy.context.scene.objects) if obj.type == "MESH" and "_LOD0" in obj.name]:
        lod = source.copy()
        lod.data = source.data.copy()
        lod.name = source.name.replace("_LOD0", "_LOD1")
        lod["urman_asset_id"] = source.get("urman_asset_id", "character.fullgame.lowpoly.v1")
        lod["license"] = source.get("license", "Project-original")
        lod["scale_meters"] = source.get("scale_meters", 1.0)
        lod["triangle_budget"] = max(12, round(int(source.get("triangle_budget", 256)) * 0.5))
        lod["collision"] = "none"
        lod["lod_status"] = f"LOD1 generated from {source.name}; ratio=0.50"
        lod["lod_source"] = source.name
        bpy.context.collection.objects.link(lod)

        bpy.context.view_layer.objects.active = lod
        lod.select_set(True)
        modifier = lod.modifiers.new("URMAN_CHARACTER_LOD1_Decimate", "DECIMATE")
        modifier.ratio = 0.5
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        lod.select_set(False)
        created += 1
    return created


def main() -> None:
    args = arguments()
    root = Path(args.root).resolve()
    blend_path = root / "assets" / "source" / "blender" / "urman_character_kit.blend"
    glb_path = root / "game" / "assets" / "generated" / "urman_character_kit.glb"
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    glb_path.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(data):
            if block.users == 0:
                data.remove(block)

    materials = {
        "skin": material("StylizedSkin", (0.57, 0.39, 0.31, 1.0)),
        "hair": material("StylizedHair", (0.10, 0.08, 0.07, 1.0)),
        "eye": material("StylizedFaceInk", (0.025, 0.021, 0.019, 1.0)),
        "boot": material("StylizedBoot", (0.09, 0.075, 0.06, 1.0)),
    }
    for index, (prefix, coat, accent, has_hat, has_beard) in enumerate(CHARACTERS):
        create_character(prefix, coat, accent, has_hat, has_beard, index * 2.4, materials)

    lod_count = generate_lod1_variants()
    rigs = [add_animation_rig(prefix, index * 2.4) for index, (prefix, *_rest) in enumerate(CHARACTERS)]
    bpy.context.scene["generator"] = "tools/blender/generate_character_kit.py"
    bpy.context.scene["blender_version_lock"] = "4.5 LTS"
    bpy.context.scene["units"] = "meters"
    bpy.context.scene["character_prefixes"] = ",".join(prefix for prefix, *_ in CHARACTERS)
    bpy.context.scene["lod_policy"] = "LOD1 generated with deterministic Decimate ratio=0.50; Godot ranges are scene-specific"
    bpy.context.scene["lod1_mesh_count"] = lod_count
    bpy.context.scene["lod0_mesh_count"] = len([obj for obj in bpy.context.scene.objects if obj.type == "MESH" and "_LOD0" in obj.name])
    bpy.context.scene["detail_policy"] = "project-original low-poly face landmarks, layered clothing, sleeves, trousers and boots; authored Idle/Tension rigs; final face/expression review remains open"
    bpy.context.scene["animation_policy"] = "nine project-original armatures with Idle/Tension clips; Godot may select clips per presentation state"
    bpy.context.scene["armature_count"] = len(rigs)
    bpy.context.scene["collision_policy"] = "no collision meshes; Godot interaction targets and zone colliders own physics"
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0

    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        export_apply=True,
        export_materials="EXPORT",
        export_yup=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_nla_strips=False,
        export_force_sampling=True,
    )
    print(f"URMAN character kit: {blend_path}")
    print(f"URMAN character kit: {glb_path}")
    print(f"URMAN character LOD1 variants: {lod_count}")


if __name__ == "__main__":
    main()
