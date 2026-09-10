"""Validate and export the authored Kara-Urman forest-edge kit reproducibly.

Run with the pinned Blender:
  .tools/blender/Blender.app/Contents/MacOS/Blender --background \
    --python tools/blender/export_kara_forest_edge_kit.py -- --root <repo>

The Kara forest-edge kit is project-original geometry and this script is the
documented reproducible export path required by tracker task ART-006. It
re-opens the saved source file, idempotently creates the declared Wave 3 edge,
Wave 4 threshold, Wave 15 silhouette and Wave 17 cleanup passes when needed,
validates the published component contract (names, parents, counts, no
images/collision), then
exports the runtime GLB with fixed settings
so repeated exports of an unchanged source are byte-stable. Threshold and
silhouette changes must keep the restrained boundary motif, avoid
creature-like silhouettes, preserve the central road sightline, and pass human
360 review before acceptance.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT_NAME = "URMAN_KaraForestEdgeKit"
EXPECTED_COMPONENTS = (
    "ForestBank_Left",
    "ForestBank_Right",
    "MixedTreeCluster_Left",
    "MixedTreeCluster_Right",
    "CrookedPineMass",
    "BirchEdgeMass",
    "RootWall_Left",
    "RootWall_Right",
    "FallenLogCluster",
    "MossyBoulderCluster",
    "CrookedStump",
    "DistantForestMass_Low",
    "DistantForestMass_Tall",
)
FORBIDDEN_NAME_PARTS = ("-col", "collision", "physics", "nav", "interact")
AUTHORED_PASS_ID = "wave3-kara-forest-edge-v1"
THRESHOLD_PASS_ID = "wave4-kara-forest-threshold-v1"
MAX_TRIANGLES = 5200
AUTHORED_PASS_OBJECTS = (
    "ForestBank_Left_GroundApron_00_LOD0",
    "ForestBank_Left_GroundApron_01_LOD0",
    "ForestBank_Right_GroundApron_00_LOD0",
    "ForestBank_Right_GroundApron_01_LOD0",
    "RootWall_Left_ContactLobe_00_LOD0",
    "RootWall_Left_ContactLobe_01_LOD0",
    "RootWall_Right_ContactLobe_00_LOD0",
    "RootWall_Right_ContactLobe_01_LOD0",
    "MixedTreeCluster_Left_FramingLimb_LOD0",
    "MixedTreeCluster_Right_FramingLimb_LOD0",
    "FallenLogCluster_LeafLitterApron_00_LOD0",
    "FallenLogCluster_LeafLitterApron_01_LOD0",
    "MossyBoulderCluster_ContactLobe_00_LOD0",
    "MossyBoulderCluster_ContactLobe_01_LOD0",
    "DistantForestMass_Tall_BrokenCrown_00_LOD0",
    "DistantForestMass_Tall_BrokenCrown_01_LOD0",
)
YOUNG_SPRUCE_SPECS = (
    ("MixedTreeCluster_Left", 0),
    ("MixedTreeCluster_Left", 1),
    ("MixedTreeCluster_Right", 0),
    ("MixedTreeCluster_Right", 1),
    ("CrookedPineMass", 0),
    ("BirchEdgeMass", 0),
)
YOUNG_SPRUCE_OBJECTS = tuple(
    f"{component}_YoungSpruce_{index:02d}_{part}"
    for component, index in YOUNG_SPRUCE_SPECS
    for part in ("Trunk", "NeedleTier_00", "NeedleTier_01", "NeedleTier_02")
)
THRESHOLD_PASS_OBJECTS = (
    "ForestBank_Left_ThresholdRise_00",
    "ForestBank_Left_ThresholdRise_01",
    "ForestBank_Right_ThresholdRise_00",
    "ForestBank_Right_ThresholdRise_01",
    "ForestBank_Left_RootFinger_00",
    "ForestBank_Left_RootFinger_01",
    "ForestBank_Right_RootFinger_00",
    "ForestBank_Right_RootFinger_01",
    "ForestBank_Left_Deadwood_00",
    "ForestBank_Left_Deadwood_01",
    "ForestBank_Right_Deadwood_00",
    "ForestBank_Right_Deadwood_01",
    "RootWall_Left_LeafLitterPatch_00",
    "RootWall_Left_LeafLitterPatch_01",
    "RootWall_Right_LeafLitterPatch_00",
    "RootWall_Right_LeafLitterPatch_01",
    "MixedTreeCluster_Left_ThresholdBranch_00",
    "MixedTreeCluster_Left_ThresholdBranch_01",
    "MixedTreeCluster_Right_ThresholdBranch_00",
    "MixedTreeCluster_Right_ThresholdBranch_01",
    "CrookedPineMass_ThresholdBranch_00",
    "CrookedPineMass_ThresholdBranch_01",
    "BirchEdgeMass_ThresholdBranch_00",
    "BirchEdgeMass_ThresholdBranch_01",
    "MixedTreeCluster_Left_MidCanopy_00",
    "MixedTreeCluster_Right_MidCanopy_00",
    "CrookedPineMass_MidCanopy_00",
    "BirchEdgeMass_MidCanopy_00",
    "DistantForestMass_Low_DeepCanopy_00",
    "DistantForestMass_Low_DeepCanopy_01",
    "DistantForestMass_Tall_DeepCanopy_00",
    "DistantForestMass_Tall_DeepCanopy_01",
) + YOUNG_SPRUCE_OBJECTS
WAVE15_PASS_ID = "wave15-kara-forest-edge-v1"
WAVE15_REMOVED_OBJECTS = (
    "DistantForestMass_Low_BreakupLobe_00",
    "DistantForestMass_Low_BreakupLobe_01",
    "DistantForestMass_Low_BreakupLobe_02",
    "DistantForestMass_Low_BreakupLobe_03",
    "DistantForestMass_Low_FacetedMass_00",
    "DistantForestMass_Low_FacetedMass_01",
    "DistantForestMass_Low_FacetedMass_02",
    "DistantForestMass_Low_FacetedMass_03",
    "DistantForestMass_Low_FacetedMass_04",
    "DistantForestMass_Low_FacetedMass_05",
) + tuple(
    f"DistantForestMass_Tall_LobedCrown_{group:02d}_{tier:02d}"
    for group in range(6)
    for tier in range(3)
)
WAVE15_REPLACED_OBJECTS = (
    "DistantForestMass_Tall_BrokenCrown_00_LOD0",
    "DistantForestMass_Tall_BrokenCrown_01_LOD0",
    "DistantForestMass_Tall_DeepCanopy_00",
    "DistantForestMass_Tall_DeepCanopy_01",
    "MossyBoulderCluster_Stone_00",
    "MossyBoulderCluster_Stone_01",
    "MossyBoulderCluster_Stone_02",
    "MossyBoulderCluster_Stone_03",
    "CrookedPineMass_AsymmetricCrown_00",
    "CrookedPineMass_AsymmetricCrown_01",
    "CrookedPineMass_AsymmetricCrown_02",
    "CrookedPineMass_AsymmetricCrown_03",
)
WAVE15_NEW_OBJECTS = (
    "DistantForestMass_Low_HorizonRidge_00",
    "DistantForestMass_Low_HorizonRidge_01",
    "DistantForestMass_Low_HorizonRidge_02",
    "DistantForestMass_Tall_FarProfile_00",
    "DistantForestMass_Tall_FarProfile_01",
    "DistantForestMass_Tall_FarProfile_02",
    "DistantForestMass_Tall_FarProfile_03",
    "DistantForestMass_Tall_FarTrunk_06",
    "DistantForestMass_Tall_FarTrunk_07",
    "DistantForestMass_Tall_FarTrunk_08",
    "DistantForestMass_Tall_FarTrunk_09",
    "ForestBank_Left_SplayedShoulder_00",
    "ForestBank_Left_SplayedShoulder_01",
    "ForestBank_Right_SplayedShoulder_00",
    "ForestBank_Right_SplayedShoulder_01",
    "RootWall_Left_RootPlate_00",
    "RootWall_Right_RootPlate_00",
    "MixedTreeCluster_Left_CanopyFork_00",
    "MixedTreeCluster_Right_CanopyFork_00",
    "CrookedPineMass_PineSpray_00",
    "BirchEdgeMass_BirchSpray_00",
    "MixedTreeCluster_Left_CanopyBrace_00",
    "MixedTreeCluster_Right_CanopyBrace_00",
)
WAVE15_OBJECTS = WAVE15_REPLACED_OBJECTS + WAVE15_NEW_OBJECTS
WAVE17_PASS_ID = "wave17-kara-threshold-v1"
WAVE17_REMOVED_OBJECTS = (
    tuple(
        f"MixedTreeCluster_{side}_FoliageLobe_{index:02d}"
        for side in ("Left", "Right")
        for index in range(4)
    )
    + tuple(f"BirchEdgeMass_BroadleafLobe_{index:02d}" for index in range(5))
    + tuple(f"BirchEdgeMass_LeafLitterLobe_{index:02d}" for index in range(3))
    + tuple(
        f"ForestBank_{side}_EarthMound_{index:02d}"
        for side in ("Left", "Right")
        for index in range(4)
    )
    + tuple(
        f"RootWall_{side}_{kind}_{index:02d}"
        for side in ("Left", "Right")
        for kind in ("MossLobe", "UnderstoryMound")
        for index in range(3 if kind == "MossLobe" else 4)
    )
    + tuple(f"MossyBoulderCluster_MossPatch_{index:02d}" for index in range(4))
    + tuple(f"DistantForestMass_Low_UnderstoryLobe_{index:02d}_LOD0" for index in range(2))
)
WAVE17_OBJECTS = (
    "ForestBank_Left_RidgeContact_00",
    "ForestBank_Right_RidgeContact_00",
    "RootWall_Left_RootFork_00",
    "RootWall_Right_RootFork_00",
    "MixedTreeCluster_Left_OrganicCrown_00",
    "MixedTreeCluster_Right_OrganicCrown_00",
    "CrookedPineMass_OrganicCrown_00",
    "BirchEdgeMass_OrganicCrown_00",
    "MixedTreeCluster_Left_OrganicBranch_00",
    "MixedTreeCluster_Right_OrganicBranch_00",
    "ForestBank_Left_UnderstoryFan_00",
    "ForestBank_Right_UnderstoryFan_00",
    "RootWall_Left_UnderstoryFan_00",
    "RootWall_Right_UnderstoryFan_00",
    "MossyBoulderCluster_MossSpray_00",
    "MossyBoulderCluster_MossSpray_01",
    "DistantForestMass_Low_UnderstoryFan_00",
    "DistantForestMass_Low_UnderstoryFan_01",
    "DistantForestMass_Tall_BranchFrame_00",
    "DistantForestMass_Tall_BranchFrame_01",
    "ForestBank_Right_EarthMound_01",
)


def source_material(name: str) -> bpy.types.Material:
    material = bpy.data.materials.get(name)
    if material is None:
        raise RuntimeError(f"Missing source material for authored Kara pass: {name}")
    return material


def authored_mesh(
    name: str,
    parent: bpy.types.Object,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material_name: str,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    role: str = "authored Kara forest-edge dressing",
) -> bpy.types.Object:
    existing = bpy.data.objects.get(name)
    if existing is not None:
        if existing.parent is not parent or existing.type != "MESH":
            raise RuntimeError(f"Authored Kara object has an unexpected parent/type: {name}")
        return existing

    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=False)
    mesh.update()
    mesh.calc_loop_triangles()
    mesh.materials.append(source_material(material_name))
    collection = parent.users_collection[0] if parent.users_collection else bpy.context.scene.collection
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = rotation
    obj["urman_asset_id"] = "urman.act1.kara_forest_edge_kit"
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["collision"] = "none"
    obj["presentation_only"] = True
    obj["lod_status"] = "LOD0"
    obj["asset_role"] = role
    obj["geometry_pass"] = AUTHORED_PASS_ID
    obj["triangle_count"] = len(mesh.loop_triangles)
    return obj


def faceted_lobe(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    size: tuple[float, float, float],
    material_name: str,
    top_scale: float = 0.72,
    skew: tuple[float, float] = (0.0, 0.0),
    role: str = "authored low-poly forest-ground lobe",
) -> bpy.types.Object:
    width, depth, height = size
    half_width = width * 0.5
    half_depth = depth * 0.5
    ring = [
        (-half_width * 0.82, -half_depth),
        (half_width * 0.72, -half_depth * 0.88),
        (half_width, -half_depth * 0.10),
        (half_width * 0.66, half_depth),
        (-half_width * 0.58, half_depth * 0.92),
        (-half_width, half_depth * 0.10),
    ]
    vertices = [(x, y, 0.0) for x, y in ring]
    vertices.extend((x * top_scale + skew[0], y * top_scale + skew[1], height) for x, y in ring)
    faces: list[tuple[int, ...]] = [tuple(reversed(range(6))), tuple(range(6, 12))]
    faces.extend((index, (index + 1) % 6, (index + 1) % 6 + 6, index + 6) for index in range(6))
    return authored_mesh(name, parent, vertices, faces, material_name, location, role=role)


def profile_prism(
    name: str,
    parent: bpy.types.Object,
    profile: tuple[tuple[float, float], ...],
    depth: float,
    material_name: str,
    location: tuple[float, float, float],
    role: str,
) -> bpy.types.Object:
    """Use an irregular side silhouette instead of another stacked lobe."""
    half_depth = depth * 0.5
    vertices = [(x, -half_depth, z) for x, z in profile]
    vertices.extend((x, half_depth, z) for x, z in profile)
    count = len(profile)
    faces: list[tuple[int, ...]] = [
        tuple(reversed(range(count))),
        tuple(range(count, count * 2)),
    ]
    faces.extend(
        (index, (index + 1) % count, count + (index + 1) % count, count + index)
        for index in range(count)
    )
    obj = authored_mesh(name, parent, vertices, faces, material_name, location, role=role)
    obj["geometry_pass"] = WAVE15_PASS_ID
    return obj


def mark_wave15(obj: bpy.types.Object, role: str) -> bpy.types.Object:
    obj["geometry_pass"] = WAVE15_PASS_ID
    obj["asset_role"] = role
    return obj


def remove_mesh_object(name: str) -> None:
    obj = bpy.data.objects.get(name)
    if obj is None:
        return
    if obj.type != "MESH":
        raise RuntimeError(f"Wave 15 cleanup found a non-mesh object: {name}")
    mesh = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    if mesh.users == 0:
        bpy.data.meshes.remove(mesh)


def replace_profile_mesh(
    name: str,
    profile: tuple[tuple[float, float], ...],
    depth: float,
    material_name: str,
    role: str,
) -> bpy.types.Object:
    old = bpy.data.objects.get(name)
    if old is None or old.type != "MESH" or old.parent is None:
        raise RuntimeError(f"Wave 15 replacement target is missing or invalid: {name}")
    parent = old.parent
    location = tuple(old.location)
    rotation = tuple(old.rotation_euler)
    mesh = old.data
    bpy.data.objects.remove(old, do_unlink=True)
    if mesh.users == 0:
        bpy.data.meshes.remove(mesh)
    obj = profile_prism(name, parent, profile, depth, material_name, location, role)
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = rotation
    return obj


def weathered_beam(
    name: str,
    parent: bpy.types.Object,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    radius: float,
    material_name: str,
    role: str = "authored forest framing limb",
) -> bpy.types.Object:
    existing = bpy.data.objects.get(name)
    if existing is not None:
        if existing.parent is not parent or existing.type != "MESH":
            raise RuntimeError(f"Authored Kara beam has an unexpected parent/type: {name}")
        return existing

    start_vector = Vector(start)
    end_vector = Vector(end)
    direction = end_vector - start_vector
    if direction.length <= 0.01:
        raise RuntimeError(f"Degenerate authored Kara beam: {name}")
    bpy.ops.mesh.primitive_cone_add(
        vertices=6,
        radius1=radius * 1.10,
        radius2=radius * 0.82,
        depth=direction.length,
        location=(0.0, 0.0, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = (start_vector + end_vector) * 0.5
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(source_material(material_name))
    obj["urman_asset_id"] = "urman.act1.kara_forest_edge_kit"
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["collision"] = "none"
    obj["presentation_only"] = True
    obj["lod_status"] = "LOD0"
    obj["asset_role"] = role
    obj["geometry_pass"] = AUTHORED_PASS_ID
    obj.data.calc_loop_triangles()
    obj["triangle_count"] = len(obj.data.loop_triangles)
    return obj


def bent_trunk(
    name: str,
    parent: bpy.types.Object,
    points: tuple[tuple[float, float, float], ...],
    radii: tuple[float, ...],
    material_name: str,
    role: str = "authored bent forest trunk/root family",
) -> bpy.types.Object:
    """Create a few-ring bent trunk so silhouettes are not stacked cones."""
    existing = bpy.data.objects.get(name)
    if existing is not None:
        if existing.parent is not parent or existing.type != "MESH":
            raise RuntimeError(f"Authored Kara trunk has an unexpected parent/type: {name}")
        return existing
    if len(points) < 2 or len(points) != len(radii) or any(radius <= 0.0 for radius in radii):
        raise RuntimeError(f"Invalid authored Kara trunk profile: {name}")

    sides = 6
    vertices: list[tuple[float, float, float]] = []
    for ring_index, ((x, y, z), radius) in enumerate(zip(points, radii)):
        phase = 0.11 * (ring_index % 2)
        for side in range(sides):
            angle = math.tau * side / sides + phase
            vertices.append((x + math.cos(angle) * radius, y + math.sin(angle) * radius, z))
    faces: list[tuple[int, ...]] = [tuple(reversed(range(sides)))]
    last_ring = (len(points) - 1) * sides
    faces.append(tuple(last_ring + side for side in range(sides)))
    for ring_index in range(len(points) - 1):
        start = ring_index * sides
        next_start = (ring_index + 1) * sides
        for side in range(sides):
            next_side = (side + 1) % sides
            faces.append((start + side, start + next_side, next_start + next_side, next_start + side))
    return authored_mesh(name, parent, vertices, faces, material_name, role=role)


def irregular_crown(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    size: tuple[float, float, float],
    material_name: str,
    lean: tuple[float, float],
    role: str,
) -> bpy.types.Object:
    """Build an anchored multi-ring crown with an asymmetric branch-side profile."""
    width, depth, height = size
    if min(width, depth, height) <= 0.0:
        raise RuntimeError(f"Invalid authored Kara crown dimensions: {name}")
    sides = 6
    profiles = (
        (0.00, 1.00, (1.00, 0.84, 1.08, 0.78, 0.94, 1.02), (0.0, 0.0)),
        (0.34, 0.91, (0.78, 1.02, 0.88, 1.04, 0.72, 0.92), (lean[0] * 0.28, lean[1] * 0.28)),
        (0.68, 0.62, (0.62, 0.88, 0.54, 0.76, 0.48, 0.72), (lean[0] * 0.64, lean[1] * 0.64)),
        (0.96, 0.26, (0.52, 0.76, 0.44, 0.68, 0.40, 0.60), lean),
    )
    vertices: list[tuple[float, float, float]] = []
    for level, scale, side_scales, offset in profiles:
        for side, side_scale in enumerate(side_scales):
            angle = math.tau * side / sides + 0.12 * (side % 2)
            vertices.append(
                (
                    offset[0] + math.cos(angle) * width * scale * side_scale,
                    offset[1] + math.sin(angle) * depth * scale * side_scale,
                    height * level,
                )
            )
    faces: list[tuple[int, ...]] = [tuple(reversed(range(sides)))]
    for ring_index in range(len(profiles) - 1):
        start = ring_index * sides
        following = (ring_index + 1) * sides
        faces.extend(
            (start + side, start + (side + 1) % sides,
             following + (side + 1) % sides, following + side)
            for side in range(sides)
        )
    last = (len(profiles) - 1) * sides
    faces.append(tuple(last + side for side in range(sides)))
    obj = authored_mesh(name, parent, vertices, faces, material_name, location, role=role)
    obj["geometry_pass"] = WAVE17_PASS_ID
    return obj


def mark_wave17(obj: bpy.types.Object, role: str) -> bpy.types.Object:
    obj["geometry_pass"] = WAVE17_PASS_ID
    obj["asset_role"] = role
    return obj


def young_spruce(
    name: str,
    parent: bpy.types.Object,
    base: tuple[float, float, float],
    height: float,
    lean: float,
    profile: tuple[tuple[float, float, float, float, float], ...],
) -> None:
    """Build one small, asymmetrical spruce from a bent trunk and tiers."""
    x, y, z = base
    bent_trunk(
        f"{name}_Trunk",
        parent,
        ((x, y, z), (x + lean * 0.35, y + 0.04, z + height * 0.42), (x + lean, y + 0.08, z + height * 0.82)),
        (0.11, 0.075, 0.045),
        "PineBark",
        role="young spruce bent trunk with visible ground anchor",
    )
    for tier_index, (level, width, depth, tier_height, skew_x) in enumerate(profile):
        center_x = x + lean * (level / max(height, 0.001))
        faceted_lobe(
            f"{name}_NeedleTier_{tier_index:02d}",
            parent,
            (center_x, y + 0.05 * tier_index, z + level),
            (width, depth, tier_height),
            "PineFoliage",
            top_scale=0.46 + tier_index * 0.03,
            skew=(skew_x, -0.03 if tier_index % 2 else 0.04),
            role="young spruce irregular needle tier; midground silhouette",
        )


def ensure_authored_pass(root: bpy.types.Object) -> bool:
    if root.get("kara_authored_pass") == AUTHORED_PASS_ID and all(
        bpy.data.objects.get(name) is not None for name in AUTHORED_PASS_OBJECTS
    ):
        return False

    left_bank = bpy.data.objects["ForestBank_Left"]
    right_bank = bpy.data.objects["ForestBank_Right"]
    for index, y in enumerate((-2.4, 2.4)):
        faceted_lobe(
            f"ForestBank_Left_GroundApron_{index:02d}_LOD0",
            left_bank,
            (2.35 + index * 0.16, y, 0.04),
            (2.20 + index * 0.18, 1.30, 0.24),
            "DampEarth" if index == 0 else "Understory",
            top_scale=0.74,
            skew=(0.08, -0.06),
            role="authored uneven forest-bank contact apron; route remains open",
        )
        faceted_lobe(
            f"ForestBank_Right_GroundApron_{index:02d}_LOD0",
            right_bank,
            (-2.35 - index * 0.14, y + 0.26, 0.04),
            (2.18 + index * 0.16, 1.28, 0.22),
            "DampEarth" if index == 0 else "Understory",
            top_scale=0.68,
            skew=(-0.08, 0.05),
            role="authored uneven forest-bank contact apron; route remains open",
        )

    left_root_wall = bpy.data.objects["RootWall_Left"]
    right_root_wall = bpy.data.objects["RootWall_Right"]
    for index, y in enumerate((-2.15, 2.65)):
        faceted_lobe(
            f"RootWall_Left_ContactLobe_{index:02d}_LOD0",
            left_root_wall,
            (1.35 + index * 0.18, y, 0.02),
            (1.62 + index * 0.12, 0.95, 0.38 + index * 0.06),
            "LeafLitter" if index == 0 else "MossGreen",
            top_scale=0.70,
            skew=(0.06, 0.02),
        )
        faceted_lobe(
            f"RootWall_Right_ContactLobe_{index:02d}_LOD0",
            right_root_wall,
            (-1.35 - index * 0.16, y + 0.18, 0.02),
            (1.58 + index * 0.14, 0.92, 0.36 + index * 0.05),
            "LeafLitter" if index == 0 else "MossGreen",
            top_scale=0.66,
            skew=(-0.05, -0.02),
        )

    left_tree = bpy.data.objects["MixedTreeCluster_Left"]
    right_tree = bpy.data.objects["MixedTreeCluster_Right"]
    weathered_beam(
        "MixedTreeCluster_Left_FramingLimb_LOD0",
        left_tree,
        (0.05, 0.55, 1.65),
        (1.12, -0.10, 2.95),
        0.075,
        "PineBark",
    )
    weathered_beam(
        "MixedTreeCluster_Right_FramingLimb_LOD0",
        right_tree,
        (-0.05, 0.72, 1.82),
        (-1.20, 0.06, 3.12),
        0.07,
        "PineBark",
    )

    fallen_logs = bpy.data.objects["FallenLogCluster"]
    for index, (x, y) in enumerate(((-0.58, -0.42), (1.18, 0.72))):
        faceted_lobe(
            f"FallenLogCluster_LeafLitterApron_{index:02d}_LOD0",
            fallen_logs,
            (x, y, 0.04),
            (1.28 if index == 0 else 1.02, 0.82, 0.12),
            "LeafLitter",
            top_scale=0.78,
            skew=(0.06 if index == 0 else -0.05, 0.0),
            role="authored log contact litter; presentation-only",
        )

    boulders = bpy.data.objects["MossyBoulderCluster"]
    for index, (x, y) in enumerate(((-1.24, -0.56), (1.18, 0.48))):
        faceted_lobe(
            f"MossyBoulderCluster_ContactLobe_{index:02d}_LOD0",
            boulders,
            (x, y, 0.07),
            (1.05 if index == 0 else 0.88, 0.70, 0.18),
            "MossGreen",
            top_scale=0.74,
            skew=(0.05, -0.02),
            role="authored mossy boulder ground contact",
        )

    distant_low = bpy.data.objects["DistantForestMass_Low"]
    for index, (x, y) in enumerate(((-9.6, -0.18), (8.9, 0.55))):
        faceted_lobe(
            f"DistantForestMass_Low_UnderstoryLobe_{index:02d}_LOD0",
            distant_low,
            (x, y, 0.02),
            (3.60 if index == 0 else 3.15, 1.60, 0.65 + index * 0.10),
            "DistantFoliage",
            top_scale=0.72,
            skew=(0.16 if index == 0 else -0.12, 0.0),
            role="authored low distant forest contact plane",
        )

    distant_tall = bpy.data.objects["DistantForestMass_Tall"]
    for index, (x, y, z) in enumerate(((-7.15, 1.65, 5.85), (8.10, 1.10, 6.45))):
        faceted_lobe(
            f"DistantForestMass_Tall_BrokenCrown_{index:02d}_LOD0",
            distant_tall,
            (x, y, z),
            (2.70 if index == 0 else 2.35, 1.55, 1.55 + index * 0.15),
            "DistantBlueGreen" if index == 0 else "DistantFoliage",
            top_scale=0.68,
            skew=(0.14 if index == 0 else -0.10, 0.0),
            role="authored broken far crown; central road window remains open",
        )

    root["kara_authored_pass"] = AUTHORED_PASS_ID
    root["kara_authored_pass_objects"] = len(AUTHORED_PASS_OBJECTS)
    root["kara_authored_pass_note"] = (
        "uneven bank aprons, root-wall contact lobes, framing limbs, log and "
        "boulder litter, low/far crown breakup; central sightline preserved"
    )
    return True


def ensure_threshold_pass(root: bpy.types.Object) -> bool:
    metadata_changed = False
    for name in THRESHOLD_PASS_OBJECTS:
        obj = bpy.data.objects.get(name)
        if (
            name not in WAVE15_OBJECTS
            and obj is not None
            and obj.get("geometry_pass") != THRESHOLD_PASS_ID
        ):
            obj["geometry_pass"] = THRESHOLD_PASS_ID
            metadata_changed = True
    if root.get("kara_threshold_pass") == THRESHOLD_PASS_ID and all(
        bpy.data.objects.get(name) is not None for name in THRESHOLD_PASS_OBJECTS
    ):
        return metadata_changed

    left_bank = bpy.data.objects["ForestBank_Left"]
    right_bank = bpy.data.objects["ForestBank_Right"]
    for name, parent, location, size, material_name, top_scale, skew in (
        (
            "ForestBank_Left_ThresholdRise_00",
            left_bank,
            (3.62, -3.0, 0.02),
            (2.65, 1.75, 0.48),
            "DampEarth",
            0.58,
            (0.14, -0.04),
        ),
        (
            "ForestBank_Left_ThresholdRise_01",
            left_bank,
            (4.05, 2.65, 0.03),
            (2.18, 1.42, 0.62),
            "Understory",
            0.63,
            (-0.08, 0.06),
        ),
        (
            "ForestBank_Right_ThresholdRise_00",
            right_bank,
            (-3.48, -2.15, 0.02),
            (2.34, 1.92, 0.46),
            "DampEarth",
            0.54,
            (-0.16, 0.05),
        ),
        (
            "ForestBank_Right_ThresholdRise_01",
            right_bank,
            (-4.12, 3.18, 0.03),
            (2.02, 1.32, 0.58),
            "MossGreen",
            0.61,
            (0.10, -0.05),
        ),
    ):
        faceted_lobe(
            name,
            parent,
            location,
            size,
            material_name,
            top_scale=top_scale,
            skew=skew,
            role="asymmetric threshold bank mass; road-side aperture remains open",
        )

    for name, parent, points in (
        (
            "ForestBank_Left_RootFinger_00",
            left_bank,
            ((3.70, -2.55, 0.04), (4.30, -2.22, 0.14), (5.00, -1.68, 0.22)),
        ),
        (
            "ForestBank_Left_RootFinger_01",
            left_bank,
            ((3.35, 3.35, 0.04), (3.82, 3.02, 0.12), (4.55, 2.56, 0.19)),
        ),
        (
            "ForestBank_Right_RootFinger_00",
            right_bank,
            ((-3.62, -1.86, 0.04), (-4.18, -1.52, 0.13), (-4.92, -0.96, 0.20)),
        ),
        (
            "ForestBank_Right_RootFinger_01",
            right_bank,
            ((-3.20, 3.80, 0.04), (-3.76, 3.45, 0.12), (-4.48, 2.94, 0.18)),
        ),
    ):
        bent_trunk(
            name,
            parent,
            points,
            (0.18, 0.12, 0.055),
            "RootDark",
            role="exposed threshold root finger with broken direction",
        )

    for name, parent, start, end, radius in (
        (
            "ForestBank_Left_Deadwood_00",
            left_bank,
            (2.95, -0.85, 0.11),
            (4.95, -1.78, 0.15),
            0.085,
        ),
        (
            "ForestBank_Left_Deadwood_01",
            left_bank,
            (3.55, 5.20, 0.10),
            (4.72, 4.45, 0.20),
            0.065,
        ),
        (
            "ForestBank_Right_Deadwood_00",
            right_bank,
            (-2.92, -0.58, 0.10),
            (-4.90, -1.52, 0.14),
            0.08,
        ),
        (
            "ForestBank_Right_Deadwood_01",
            right_bank,
            (-3.48, 5.02, 0.10),
            (-4.64, 4.18, 0.18),
            0.06,
        ),
    ):
        weathered_beam(
            name,
            parent,
            start,
            end,
            radius,
            "WeatheredWood",
            role="low deadwood threshold debris; path side only",
        )

    left_root_wall = bpy.data.objects["RootWall_Left"]
    right_root_wall = bpy.data.objects["RootWall_Right"]
    for name, parent, location, size, material_name, skew in (
        (
            "RootWall_Left_LeafLitterPatch_00",
            left_root_wall,
            (2.55, -1.05, 0.03),
            (1.48, 1.04, 0.14),
            "LeafLitter",
            (0.10, -0.04),
        ),
        (
            "RootWall_Left_LeafLitterPatch_01",
            left_root_wall,
            (3.10, 3.65, 0.03),
            (1.22, 0.86, 0.18),
            "MossGreen",
            (-0.08, 0.06),
        ),
        (
            "RootWall_Right_LeafLitterPatch_00",
            right_root_wall,
            (-2.48, -1.62, 0.03),
            (1.42, 0.96, 0.15),
            "LeafLitter",
            (-0.10, 0.04),
        ),
        (
            "RootWall_Right_LeafLitterPatch_01",
            right_root_wall,
            (-3.18, 3.38, 0.03),
            (1.18, 0.88, 0.17),
            "MossGreen",
            (0.09, -0.05),
        ),
    ):
        faceted_lobe(
            name,
            parent,
            location,
            size,
            material_name,
            top_scale=0.62,
            skew=skew,
            role="layered root-wall leaf litter and understory contact",
        )

    mixed_left = bpy.data.objects["MixedTreeCluster_Left"]
    mixed_right = bpy.data.objects["MixedTreeCluster_Right"]
    for name, parent, start, end, radius in (
        (
            "MixedTreeCluster_Left_ThresholdBranch_00",
            mixed_left,
            (0.15, 0.15, 2.25),
            (1.95, -0.78, 3.52),
            0.095,
        ),
        (
            "MixedTreeCluster_Left_ThresholdBranch_01",
            mixed_left,
            (1.10, 1.12, 2.82),
            (2.45, 0.18, 3.65),
            0.07,
        ),
        (
            "MixedTreeCluster_Right_ThresholdBranch_00",
            mixed_right,
            (-0.12, 0.24, 2.48),
            (-2.02, -0.70, 3.78),
            0.09,
        ),
        (
            "MixedTreeCluster_Right_ThresholdBranch_01",
            mixed_right,
            (-1.08, 1.08, 3.05),
            (-2.42, 0.24, 3.92),
            0.066,
        ),
    ):
        weathered_beam(
            name,
            parent,
            start,
            end,
            radius,
            "PineBark",
            role="asymmetric mid-canopy branch frame; sightline remains open",
        )

    crooked_pine = bpy.data.objects["CrookedPineMass"]
    birch = bpy.data.objects["BirchEdgeMass"]
    for name, parent, start, end, radius in (
        (
            "CrookedPineMass_ThresholdBranch_00",
            crooked_pine,
            (0.08, 0.45, 3.05),
            (1.72, -0.38, 4.12),
            0.10,
        ),
        (
            "CrookedPineMass_ThresholdBranch_01",
            crooked_pine,
            (-0.30, 1.10, 3.88),
            (-1.85, 0.46, 4.62),
            0.072,
        ),
        (
            "BirchEdgeMass_ThresholdBranch_00",
            birch,
            (-0.12, 0.42, 2.72),
            (-1.62, -0.52, 3.85),
            0.076,
        ),
        (
            "BirchEdgeMass_ThresholdBranch_01",
            birch,
            (0.42, 1.08, 3.46),
            (1.86, 0.34, 4.14),
            0.058,
        ),
    ):
        weathered_beam(
            name,
            parent,
            start,
            end,
            radius,
            "BirchBark" if parent is birch else "PineBark",
            role="distinct threshold branch family; no repeated crown wall",
        )

    spruce_profiles = (
        (
            (0.46, 1.06, 0.70, 0.34, -0.08),
            (0.98, 0.84, 0.56, 0.38, 0.10),
            (1.43, 0.56, 0.42, 0.34, -0.05),
        ),
        (
            (0.40, 0.92, 0.62, 0.30, 0.08),
            (0.86, 0.69, 0.48, 0.35, -0.12),
            (1.24, 0.44, 0.35, 0.31, 0.04),
        ),
    )
    spruce_placements = (
        (mixed_left, "MixedTreeCluster_Left_YoungSpruce_00", (1.60, -1.62, 0.03), 1.92, 0.22, spruce_profiles[0]),
        (mixed_left, "MixedTreeCluster_Left_YoungSpruce_01", (2.56, 2.06, 0.03), 1.68, -0.18, spruce_profiles[1]),
        (mixed_right, "MixedTreeCluster_Right_YoungSpruce_00", (-1.62, -2.08, 0.03), 2.10, -0.26, spruce_profiles[1]),
        (mixed_right, "MixedTreeCluster_Right_YoungSpruce_01", (-2.48, 1.86, 0.03), 1.76, 0.20, spruce_profiles[0]),
        (crooked_pine, "CrookedPineMass_YoungSpruce_00", (1.16, -1.04, 0.03), 1.84, 0.16, spruce_profiles[1]),
        (birch, "BirchEdgeMass_YoungSpruce_00", (-1.74, -2.02, 0.03), 1.72, -0.20, spruce_profiles[0]),
    )
    for parent, name, base, height, lean, profile in spruce_placements:
        young_spruce(name, parent, base, height, lean, profile)

    for name, parent, location, size, material_name, top_scale, skew in (
        (
            "MixedTreeCluster_Left_MidCanopy_00",
            mixed_left,
            (1.05, -0.16, 3.48),
            (2.42, 1.58, 1.08),
            "FoliageBlueGreen",
            0.58,
            (0.15, -0.08),
        ),
        (
            "MixedTreeCluster_Right_MidCanopy_00",
            mixed_right,
            (-1.36, 0.32, 3.80),
            (2.56, 1.52, 1.22),
            "FoliageBlueGreen",
            0.54,
            (-0.12, 0.08),
        ),
        (
            "CrookedPineMass_MidCanopy_00",
            crooked_pine,
            (0.76, 0.66, 4.66),
            (2.32, 1.48, 1.42),
            "PineFoliage",
            0.52,
            (0.18, -0.05),
        ),
        (
            "BirchEdgeMass_MidCanopy_00",
            birch,
            (-0.82, 0.18, 4.36),
            (2.46, 1.58, 1.04),
            "BirchLeaves",
            0.60,
            (-0.10, 0.06),
        ),
    ):
        faceted_lobe(
            name,
            parent,
            location,
            size,
            material_name,
            top_scale=top_scale,
            skew=skew,
            role="layered mid-canopy silhouette with an irregular crown profile",
        )

    distant_low = bpy.data.objects["DistantForestMass_Low"]
    distant_tall = bpy.data.objects["DistantForestMass_Tall"]
    for name, parent, location, size, material_name, top_scale, skew in (
        (
            "DistantForestMass_Low_DeepCanopy_00",
            distant_low,
            (-11.60, -1.12, 1.00),
            (3.92, 1.94, 1.02),
            "DistantFoliage",
            0.58,
            (0.22, -0.06),
        ),
        (
            "DistantForestMass_Low_DeepCanopy_01",
            distant_low,
            (8.25, 0.78, 1.55),
            (3.24, 1.72, 1.30),
            "DistantBlueGreen",
            0.64,
            (-0.16, 0.08),
        ),
        (
            "DistantForestMass_Tall_DeepCanopy_00",
            distant_tall,
            (-8.18, 1.62, 6.12),
            (3.34, 1.82, 1.88),
            "DistantBlueGreen",
            0.50,
            (0.20, -0.08),
        ),
        (
            "DistantForestMass_Tall_DeepCanopy_01",
            distant_tall,
            (7.52, -0.54, 7.46),
            (2.78, 1.54, 2.16),
            "DistantFoliage",
            0.57,
            (-0.14, 0.06),
        ),
    ):
        faceted_lobe(
            name,
            parent,
            location,
            size,
            material_name,
            top_scale=top_scale,
            skew=skew,
            role="broken far canopy silhouette; central road sightline remains open",
        )

    root["kara_threshold_pass"] = THRESHOLD_PASS_ID
    root["kara_threshold_pass_objects"] = len(THRESHOLD_PASS_OBJECTS)
    root["kara_threshold_pass_note"] = (
        "asymmetric banks, bent roots and branches, six young spruce families, "
        "deadwood/leaf litter and broken mid/far canopy silhouettes; no occluder wall"
    )
    return True


def ensure_wave15_pass(root: bpy.types.Object) -> bool:
    if root.get("kara_wave15_pass") == WAVE15_PASS_ID and all(
        (
            bpy.data.objects.get(name) is not None
            and bpy.data.objects[name].get("geometry_pass") == WAVE15_PASS_ID
        )
        for name in WAVE15_OBJECTS
    ):
        return False

    for name in WAVE15_REMOVED_OBJECTS:
        remove_mesh_object(name)

    bank_left = (
        (-2.00, 0.00),
        (1.78, 0.00),
        (1.45, 0.28),
        (0.86, 0.52),
        (0.12, 0.90),
        (-0.58, 0.66),
        (-1.08, 0.74),
        (-1.78, 0.32),
    )
    bank_right = (
        (-1.82, 0.00),
        (1.96, 0.00),
        (1.62, 0.38),
        (1.04, 0.62),
        (0.36, 0.42),
        (-0.22, 0.82),
        (-0.92, 0.55),
        (-1.55, 0.70),
    )
    bank_left_high = (
        (-1.56, 0.00),
        (1.86, 0.00),
        (1.40, 0.46),
        (0.74, 0.68),
        (0.18, 0.38),
        (-0.52, 0.92),
        (-1.20, 0.62),
        (-1.78, 0.86),
    )
    bank_right_high = (
        (-1.88, 0.00),
        (1.52, 0.00),
        (1.18, 0.30),
        (0.58, 0.78),
        (-0.08, 0.48),
        (-0.72, 0.70),
        (-1.10, 0.44),
        (-1.72, 0.78),
    )
    for name, parent, profile, location, material_name in (
        (
            "ForestBank_Left_SplayedShoulder_00",
            bpy.data.objects["ForestBank_Left"],
            bank_left,
            (2.58, -3.36, 0.02),
            "DampEarth",
        ),
        (
            "ForestBank_Left_SplayedShoulder_01",
            bpy.data.objects["ForestBank_Left"],
            bank_left_high,
            (3.38, 2.22, 0.03),
            "Understory",
        ),
        (
            "ForestBank_Right_SplayedShoulder_00",
            bpy.data.objects["ForestBank_Right"],
            bank_right,
            (-2.72, -2.50, 0.02),
            "DampEarth",
        ),
        (
            "ForestBank_Right_SplayedShoulder_01",
            bpy.data.objects["ForestBank_Right"],
            bank_right_high,
            (-3.58, 2.16, 0.03),
            "MossGreen",
        ),
    ):
        profile_prism(
            name,
            parent,
            profile,
            1.72,
            material_name,
            location,
            "asymmetric splayed forest-bank shoulder with visible route contact",
        )

    rock_profiles = (
        (
            (-0.88, -0.58),
            (0.58, -0.64),
            (0.94, -0.18),
            (0.64, 0.44),
            (0.02, 0.68),
            (-0.74, 0.42),
            (-1.00, -0.18),
        ),
        (
            (-0.74, -0.52),
            (0.80, -0.48),
            (0.92, 0.10),
            (0.36, 0.62),
            (-0.34, 0.50),
            (-0.88, 0.14),
        ),
        (
            (-0.92, -0.44),
            (0.42, -0.62),
            (0.86, -0.08),
            (0.54, 0.54),
            (-0.20, 0.72),
            (-0.82, 0.30),
        ),
        (
            (-0.66, -0.42),
            (0.72, -0.50),
            (0.96, 0.06),
            (0.44, 0.44),
            (-0.14, 0.58),
            (-0.78, 0.18),
        ),
    )
    for index, profile in enumerate(rock_profiles):
        replace_profile_mesh(
            f"MossyBoulderCluster_Stone_{index:02d}",
            profile,
            0.82 if index % 2 == 0 else 0.70,
            "MossyStone",
            "irregular mossy stone anchor with a readable sloped crown",
        )

    pine_crowns = (
        (
            (-1.20, -0.58),
            (1.02, -0.48),
            (0.70, -0.12),
            (1.18, 0.10),
            (0.42, 0.30),
            (0.78, 0.58),
            (0.02, 0.78),
            (-0.42, 0.52),
            (-0.98, 0.66),
            (-0.58, 0.20),
            (-1.26, -0.02),
        ),
        (
            (-1.08, -0.52),
            (1.16, -0.46),
            (0.72, -0.06),
            (1.00, 0.22),
            (0.30, 0.42),
            (0.54, 0.74),
            (-0.18, 0.58),
            (-0.64, 0.84),
            (-0.78, 0.34),
            (-1.22, 0.18),
        ),
        (
            (-1.26, -0.46),
            (0.88, -0.54),
            (1.20, -0.10),
            (0.62, 0.18),
            (0.98, 0.46),
            (0.14, 0.70),
            (-0.24, 0.42),
            (-0.74, 0.76),
            (-0.90, 0.18),
            (-1.34, 0.02),
        ),
        (
            (-1.06, -0.48),
            (1.22, -0.42),
            (0.74, -0.02),
            (1.34, 0.30),
            (0.52, 0.54),
            (0.42, 0.88),
            (-0.34, 0.62),
            (-0.72, 0.94),
            (-0.86, 0.34),
            (-1.22, 0.24),
        ),
    )
    for index, profile in enumerate(pine_crowns):
        replace_profile_mesh(
            f"CrookedPineMass_AsymmetricCrown_{index:02d}",
            profile,
            1.12 if index % 2 == 0 else 0.98,
            "PineFoliage" if index != 1 else "FoliageBlueGreen",
            "crooked pine crown profile with broken branch-side silhouette",
        )

    far_profiles = (
        (
            (-1.56, 0.00),
            (1.44, 0.00),
            (1.18, 0.72),
            (0.68, 0.54),
            (0.96, 1.42),
            (0.34, 1.18),
            (0.50, 2.20),
            (-0.12, 1.76),
            (-0.64, 2.90),
            (-0.94, 1.92),
            (-1.48, 2.26),
            (-1.04, 1.22),
            (-1.66, 0.86),
        ),
        (
            (-1.44, 0.00),
            (1.60, 0.00),
            (1.08, 0.48),
            (1.36, 1.02),
            (0.54, 0.90),
            (0.86, 1.70),
            (0.12, 1.42),
            (0.30, 2.72),
            (-0.36, 1.84),
            (-0.90, 2.36),
            (-0.72, 1.20),
            (-1.56, 1.48),
        ),
        (
            (-1.26, 0.00),
            (1.34, 0.00),
            (0.96, 0.64),
            (0.52, 0.48),
            (0.82, 1.30),
            (0.16, 1.08),
            (0.42, 2.08),
            (-0.30, 1.52),
            (-0.14, 3.12),
            (-0.72, 1.94),
            (-1.18, 2.34),
            (-0.92, 1.12),
            (-1.44, 0.78),
        ),
        (
            (-1.54, 0.00),
            (1.22, 0.00),
            (1.42, 0.62),
            (0.62, 0.74),
            (1.00, 1.44),
            (0.24, 1.12),
            (0.54, 2.30),
            (-0.24, 1.62),
            (-0.64, 3.44),
            (-0.98, 2.00),
            (-1.42, 2.56),
            (-1.02, 1.18),
            (-1.72, 0.72),
        ),
    )
    for name, profile, depth, material_name in (
        (
            "DistantForestMass_Tall_BrokenCrown_00_LOD0",
            far_profiles[0],
            1.24,
            "DistantBlueGreen",
        ),
        (
            "DistantForestMass_Tall_BrokenCrown_01_LOD0",
            far_profiles[1],
            1.36,
            "DistantFoliage",
        ),
        (
            "DistantForestMass_Tall_DeepCanopy_00",
            far_profiles[2],
            1.56,
            "DistantBlueGreen",
        ),
        (
            "DistantForestMass_Tall_DeepCanopy_01",
            far_profiles[3],
            1.48,
            "DistantFoliage",
        ),
    ):
        replace_profile_mesh(
            name,
            profile,
            depth,
            material_name,
            "far forest gate profile with broken crown rhythm",
        )

    ridge_profiles = (
        (
            (-3.72, 0.00),
            (3.48, 0.00),
            (3.18, 0.88),
            (2.38, 0.66),
            (1.66, 1.42),
            (0.82, 0.82),
            (0.08, 1.24),
            (-0.86, 0.68),
            (-1.62, 1.56),
            (-2.54, 0.82),
            (-3.46, 1.20),
        ),
        (
            (-3.28, 0.00),
            (3.62, 0.00),
            (3.10, 0.62),
            (2.18, 1.06),
            (1.26, 0.66),
            (0.50, 1.38),
            (-0.42, 0.80),
            (-1.14, 1.12),
            (-2.04, 0.58),
            (-2.70, 1.42),
        ),
        (
            (-3.84, 0.00),
            (3.18, 0.00),
            (2.70, 1.14),
            (1.92, 0.74),
            (1.12, 1.62),
            (0.18, 0.92),
            (-0.64, 1.34),
            (-1.54, 0.70),
            (-2.26, 1.24),
            (-3.46, 0.76),
        ),
    )
    low = bpy.data.objects["DistantForestMass_Low"]
    for index, (profile, location, material_name) in enumerate(
        (
            (ridge_profiles[0], (-10.4, 0.85, 0.02), "DistantFoliage"),
            (ridge_profiles[1], (-3.15, 1.48, 0.04), "DistantBlueGreen"),
            (ridge_profiles[2], (8.72, 0.78, 0.02), "DistantFoliage"),
        )
    ):
        profile_prism(
            f"DistantForestMass_Low_HorizonRidge_{index:02d}",
            low,
            profile,
            1.68,
            material_name,
            location,
            "broken far horizon ridge with a readable central route opening",
        )

    tall = bpy.data.objects["DistantForestMass_Tall"]
    far_trunks = (
        ((-4.80, 1.30, 0.02), (-4.58, 1.38, 4.24), (-4.26, 1.45, 5.00)),
        ((-2.96, 2.36, 0.02), (-3.16, 2.42, 2.72), (-2.88, 2.48, 3.48)),
        ((3.22, 2.02, 0.02), (3.48, 2.08, 3.36), (3.12, 2.15, 4.42)),
        ((4.86, 1.20, 0.02), (4.62, 1.28, 2.56), (4.92, 1.34, 3.24)),
    )
    for index, points in enumerate(far_trunks, start=6):
        mark_wave15(
            bent_trunk(
                f"DistantForestMass_Tall_FarTrunk_{index:02d}",
                tall,
                points,
                (0.15, 0.11, 0.065),
                "DistantBark",
                role="far gate trunk with a slight authored lean",
            ),
            "far gate trunk with a slight authored lean",
        )

    for index, (profile, location, depth, material_name) in enumerate(
        (
            (far_profiles[2], (-12.34, 2.00, 5.84), 1.42, "DistantFoliage"),
            (far_profiles[1], (-6.63, 2.16, 5.96), 1.32, "DistantBlueGreen"),
            (far_profiles[0], (6.30, 1.60, 4.96), 1.22, "DistantBlueGreen"),
            (far_profiles[3], (12.30, 2.20, 4.56), 1.34, "DistantFoliage"),
        )
    ):
        profile_prism(
            f"DistantForestMass_Tall_FarProfile_{index:02d}",
            tall,
            profile,
            depth,
            material_name,
            location,
            "distinct far pine/birch gate silhouette anchored to a trunk",
        )

    crown_left = (
        (-1.50, 0.00),
        (1.22, 0.00),
        (0.94, 0.42),
        (1.46, 0.78),
        (0.68, 1.10),
        (1.00, 1.64),
        (0.22, 1.90),
        (-0.10, 2.42),
        (-0.70, 1.96),
        (-1.34, 1.72),
        (-0.82, 1.12),
        (-1.58, 0.74),
    )
    crown_right = (
        (-1.34, 0.00),
        (1.48, 0.00),
        (1.08, 0.46),
        (0.72, 0.92),
        (1.26, 1.28),
        (0.42, 1.54),
        (0.18, 2.28),
        (-0.44, 1.78),
        (-1.10, 2.02),
        (-0.76, 1.18),
        (-1.48, 0.72),
    )
    profile_prism(
        "MixedTreeCluster_Left_CanopyFork_00",
        bpy.data.objects["MixedTreeCluster_Left"],
        crown_left,
        1.34,
        "FoliageBlueGreen",
        (1.56, -0.38, 3.18),
        "mixed tree fork canopy with an off-axis broadleaf silhouette",
    )
    profile_prism(
        "MixedTreeCluster_Right_CanopyFork_00",
        bpy.data.objects["MixedTreeCluster_Right"],
        crown_right,
        1.42,
        "BirchLeaves",
        (-1.68, 0.42, 3.46),
        "mixed tree fork canopy with a broken birch-side silhouette",
    )
    pine_spray = (
        (-1.18, 0.00),
        (1.02, 0.00),
        (0.62, 0.38),
        (1.22, 0.66),
        (0.46, 0.94),
        (0.76, 1.42),
        (0.06, 1.92),
        (-0.38, 1.34),
        (-0.98, 1.52),
        (-0.64, 0.86),
        (-1.28, 0.52),
    )
    birch_spray = (
        (-1.22, 0.00),
        (1.08, 0.00),
        (0.64, 0.50),
        (1.34, 0.88),
        (0.42, 1.16),
        (0.86, 1.74),
        (0.10, 2.14),
        (-0.46, 1.58),
        (-1.08, 1.90),
        (-0.70, 0.98),
        (-1.36, 0.56),
    )
    profile_prism(
        "CrookedPineMass_PineSpray_00",
        bpy.data.objects["CrookedPineMass"],
        pine_spray,
        1.08,
        "PineFoliage",
        (0.32, 0.44, 3.72),
        "crooked pine spray with an uneven stepped branch profile",
    )
    profile_prism(
        "BirchEdgeMass_BirchSpray_00",
        bpy.data.objects["BirchEdgeMass"],
        birch_spray,
        1.12,
        "BirchLeaves",
        (-0.34, 0.24, 3.88),
        "birch spray with an asymmetrical leaf-fan silhouette",
    )
    mark_wave15(
        weathered_beam(
            "MixedTreeCluster_Left_CanopyBrace_00",
            bpy.data.objects["MixedTreeCluster_Left"],
            (0.18, 0.08, 2.48),
            (1.46, -0.34, 3.42),
            0.068,
            "PineBark",
            role="connected left canopy fork brace",
        ),
        "connected left canopy fork brace",
    )
    mark_wave15(
        weathered_beam(
            "MixedTreeCluster_Right_CanopyBrace_00",
            bpy.data.objects["MixedTreeCluster_Right"],
            (-0.18, 0.16, 2.66),
            (-1.58, 0.38, 3.78),
            0.064,
            "BirchBark",
            role="connected right canopy fork brace",
        ),
        "connected right canopy fork brace",
    )
    for name, parent, profile, location, material_name in (
        (
            "RootWall_Left_RootPlate_00",
            bpy.data.objects["RootWall_Left"],
            ((-1.20, 0.00), (1.20, 0.00), (0.82, 0.26), (0.36, 0.58), (-0.20, 0.44), (-0.76, 0.72), (-1.38, 0.24)),
            (1.70, -2.46, 0.02),
            "LeafLitter",
        ),
        (
            "RootWall_Right_RootPlate_00",
            bpy.data.objects["RootWall_Right"],
            ((-1.18, 0.00), (1.18, 0.00), (0.72, 0.38), (0.24, 0.52), (-0.38, 0.42), (-0.82, 0.64), (-1.34, 0.20)),
            (-1.82, -2.22, 0.02),
            "MossGreen",
        ),
    ):
        profile_prism(
            name,
            parent,
            profile,
            1.04,
            material_name,
            location,
            "root-wall plate that fans into the route-side ground contact",
        )

    root["kara_wave15_pass"] = WAVE15_PASS_ID
    root["kara_wave15_removed_object_count"] = len(WAVE15_REMOVED_OBJECTS)
    root["kara_wave15_replaced_object_count"] = len(WAVE15_REPLACED_OBJECTS)
    root["kara_wave15_new_object_count"] = len(WAVE15_NEW_OBJECTS)
    root["kara_wave15_note"] = (
        "replaced stacked far crowns, low horizon blobs and rounded stone read "
        "with asymmetric bank shoulders, species profiles and broken far gate; "
        "central route aperture remains open"
    )
    return True


def ensure_wave17_pass(root: bpy.types.Object) -> bool:
    """Replace the remaining lobe grid with anchored crown and bank families."""
    if root.get("kara_wave17_pass") == WAVE17_PASS_ID and all(
        (
            bpy.data.objects.get(name) is not None
            and bpy.data.objects[name].get("geometry_pass") == WAVE17_PASS_ID
        )
        for name in WAVE17_OBJECTS
    ):
        return False

    for name in WAVE17_REMOVED_OBJECTS:
        remove_mesh_object(name)

    bank_left = bpy.data.objects["ForestBank_Left"]
    bank_right = bpy.data.objects["ForestBank_Right"]
    bank_left_profile = (
        (-1.72, 0.00),
        (1.42, 0.00),
        (1.18, 0.34),
        (0.70, 0.72),
        (0.04, 0.58),
        (-0.62, 1.02),
        (-1.30, 0.68),
        (-1.84, 0.32),
    )
    bank_right_profile = (
        (-1.52, 0.00),
        (1.76, 0.00),
        (1.30, 0.28),
        (0.78, 0.56),
        (0.12, 0.42),
        (-0.48, 0.86),
        (-1.08, 0.62),
        (-1.70, 0.94),
    )
    for name, parent, profile, location, material_name in (
        (
            "ForestBank_Left_RidgeContact_00",
            bank_left,
            bank_left_profile,
            (3.58, 0.48, 0.03),
            "DampEarth",
        ),
        (
            "ForestBank_Right_RidgeContact_00",
            bank_right,
            bank_right_profile,
            (-3.66, 0.76, 0.03),
            "MossGreen",
        ),
    ):
        mark_wave17(
            profile_prism(
                name,
                parent,
                profile,
                1.46,
                material_name,
                location,
                "rising asymmetric bank profile grounding the forest threshold",
            ),
            "rising asymmetric bank profile grounding the forest threshold",
        )

    # Act1ConnectedWorld hides this exact presentation-only target after
    # mounting the right bank. Keep the node contract while replacing the
    # retired repeated mound with a low, irregular bank-contact profile.
    mark_wave17(
        profile_prism(
            "ForestBank_Right_EarthMound_01",
            bank_right,
            (
                (-1.54, 0.00),
                (1.28, 0.00),
                (1.06, 0.20),
                (0.64, 0.38),
                (0.08, 0.34),
                (-0.54, 0.54),
                (-1.20, 0.30),
                (-1.58, 0.08),
            ),
            1.20,
            "DampEarth",
            (-3.46, 2.36, 0.03),
            "low irregular compatibility bank contact for the runtime suppression target",
        ),
        "low irregular compatibility bank contact for the runtime suppression target",
    )

    for name, parent, points in (
        (
            "RootWall_Left_RootFork_00",
            bpy.data.objects["RootWall_Left"],
            ((1.44, -2.20, 0.04), (1.82, -1.92, 0.20), (2.52, -1.56, 0.34), (3.00, -1.08, 0.38)),
        ),
        (
            "RootWall_Right_RootFork_00",
            bpy.data.objects["RootWall_Right"],
            ((-1.42, -2.02, 0.04), (-1.86, -1.78, 0.18), (-2.48, -1.42, 0.30), (-2.96, -0.92, 0.36)),
        ),
    ):
        mark_wave17(
            bent_trunk(
                name,
                parent,
                points,
                (0.16, 0.12, 0.085, 0.050),
                "RootDark",
                "exposed root fork extending from the grounded threshold wall",
            ),
            "exposed root fork extending from the grounded threshold wall",
        )

    crown_specs = (
        (
            "MixedTreeCluster_Left_OrganicCrown_00",
            bpy.data.objects["MixedTreeCluster_Left"],
            (1.28, -0.26, 3.12),
            (1.42, 0.96, 1.62),
            "FoliageBlueGreen",
            (0.24, 0.06),
            "anchored mixed-tree crown with an off-axis broadleaf branch rhythm",
        ),
        (
            "MixedTreeCluster_Right_OrganicCrown_00",
            bpy.data.objects["MixedTreeCluster_Right"],
            (-1.34, 0.34, 3.42),
            (1.34, 1.02, 1.74),
            "BirchLeaves",
            (-0.22, 0.08),
            "anchored birch-side crown with a broken wind-shaped profile",
        ),
        (
            "CrookedPineMass_OrganicCrown_00",
            bpy.data.objects["CrookedPineMass"],
            (0.42, 0.48, 3.96),
            (1.50, 1.00, 1.78),
            "PineFoliage",
            (0.20, -0.04),
            "anchored pine crown with an uneven outward spray",
        ),
        (
            "BirchEdgeMass_OrganicCrown_00",
            bpy.data.objects["BirchEdgeMass"],
            (-0.36, 0.22, 4.08),
            (1.42, 1.00, 1.48),
            "BirchLeaves",
            (-0.18, 0.06),
            "anchored birch crown with a quiet asymmetrical leaf mass",
        ),
    )
    for name, parent, location, size, material_name, lean, role in crown_specs:
        irregular_crown(name, parent, location, size, material_name, lean, role)

    for name, parent, start, end, radius, material_name, role in (
        (
            "MixedTreeCluster_Left_OrganicBranch_00",
            bpy.data.objects["MixedTreeCluster_Left"],
            (0.42, -0.04, 2.28),
            (1.26, -0.24, 3.34),
            0.082,
            "PineBark",
            "visible left trunk-to-crown branch anchoring the threshold silhouette",
        ),
        (
            "MixedTreeCluster_Right_OrganicBranch_00",
            bpy.data.objects["MixedTreeCluster_Right"],
            (-0.44, 0.12, 2.54),
            (-1.34, 0.30, 3.62),
            0.078,
            "BirchBark",
            "visible right trunk-to-crown branch anchoring the threshold silhouette",
        ),
    ):
        mark_wave17(weathered_beam(name, parent, start, end, radius, material_name, role), role)

    for name, parent, location, size, material_name, lean, role in (
        (
            "ForestBank_Left_UnderstoryFan_00",
            bank_left,
            (2.56, -1.56, 0.04),
            (1.72, 0.86, 0.78),
            "Understory",
            (0.22, -0.03),
            "low left understory fan nested into the bank contact",
        ),
        (
            "ForestBank_Right_UnderstoryFan_00",
            bank_right,
            (-2.72, -1.30, 0.04),
            (1.64, 0.84, 0.74),
            "Understory",
            (-0.18, 0.04),
            "low right understory fan nested into the bank contact",
        ),
        (
            "RootWall_Left_UnderstoryFan_00",
            bpy.data.objects["RootWall_Left"],
            (1.48, -2.14, 0.04),
            (1.30, 0.72, 0.58),
            "LeafLitter",
            (0.16, 0.02),
            "small left root-wall understory fan kept outside the route",
        ),
        (
            "RootWall_Right_UnderstoryFan_00",
            bpy.data.objects["RootWall_Right"],
            (-1.50, -1.92, 0.04),
            (1.24, 0.70, 0.54),
            "LeafLitter",
            (-0.14, 0.02),
            "small right root-wall understory fan kept outside the route",
        ),
    ):
        irregular_crown(name, parent, location, size, material_name, lean, role)

    boulders = bpy.data.objects["MossyBoulderCluster"]
    for name, location, size, lean in (
        (
            "MossyBoulderCluster_MossSpray_00",
            (1.04, -1.12, 0.26),
            (0.72, 0.42, 0.34),
            (0.16, -0.02),
        ),
        (
            "MossyBoulderCluster_MossSpray_01",
            (3.02, 0.30, 0.24),
            (0.66, 0.38, 0.30),
            (-0.14, 0.03),
        ),
    ):
        irregular_crown(
            name,
            boulders,
            location,
            size,
            "MossGreen",
            lean,
            "low moss spray tying the retained stones into one grounded cluster",
        )

    low = bpy.data.objects["DistantForestMass_Low"]
    for name, location, size, material_name, lean in (
        (
            "DistantForestMass_Low_UnderstoryFan_00",
            (-8.72, 1.08, 0.05),
            (2.46, 1.14, 1.12),
            "DistantFoliage",
            (0.26, 0.04),
        ),
        (
            "DistantForestMass_Low_UnderstoryFan_01",
            (8.18, 1.12, 0.07),
            (2.20, 1.06, 1.24),
            "DistantBlueGreen",
            (-0.22, 0.06),
        ),
    ):
        irregular_crown(
            name,
            low,
            location,
            size,
            material_name,
            lean,
            "low far understory fan closing the horizon without a flat wall",
        )

    tall = bpy.data.objects["DistantForestMass_Tall"]
    for name, start, end, radius, material_name, role in (
        (
            "DistantForestMass_Tall_BranchFrame_00",
            (-4.78, 1.34, 3.62),
            (-3.06, 1.84, 4.42),
            0.070,
            "DistantBark",
            "far left branch frame tying the tall gate silhouettes to their trunks",
        ),
        (
            "DistantForestMass_Tall_BranchFrame_01",
            (3.24, 2.06, 3.18),
            (4.82, 1.34, 4.06),
            0.066,
            "DistantBark",
            "far right branch frame tying the tall gate silhouettes to their trunks",
        ),
    ):
        mark_wave17(weathered_beam(name, tall, start, end, radius, material_name, role), role)

    root["kara_wave17_pass"] = WAVE17_PASS_ID
    root["kara_wave17_removed_object_count"] = len(WAVE17_REMOVED_OBJECTS)
    root["kara_wave17_new_object_count"] = len(WAVE17_OBJECTS)
    root["kara_wave17_note"] = (
        "removed remaining repeated lobe grid from mixed trees, birch, banks, "
        "root walls, boulder moss and low horizon; added anchored multi-ring "
        "crowns, trunk-to-crown branches, asymmetric bank ridges, root forks, "
        "understory fans and far branch frames while preserving the route aperture"
    )
    return True


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    tokens: list[str] = []
    if "--" in sys.argv:
        tokens = sys.argv[sys.argv.index("--") + 1 :]
    return parser.parse_args(tokens)


def mesh_descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    meshes: list[bpy.types.Object] = []
    for child in root.children:
        if child.type == "MESH":
            meshes.append(child)
        meshes.extend(mesh_descendants(child))
    return meshes


def validate(root: bpy.types.Object) -> dict[str, object]:
    direct = tuple(child.name for child in root.children)
    missing = [name for name in EXPECTED_COMPONENTS if name not in direct]
    if missing:
        raise RuntimeError(f"Missing published component roots: {missing}")
    unexpected = [name for name in direct if name not in EXPECTED_COMPONENTS]
    if unexpected:
        raise RuntimeError(f"Unexpected direct component roots: {unexpected}")

    meshes = mesh_descendants(root)
    if not meshes:
        raise RuntimeError("Authored kit has no presentation meshes")

    missing_authored = [name for name in AUTHORED_PASS_OBJECTS if bpy.data.objects.get(name) is None]
    if missing_authored:
        raise RuntimeError(f"Wave 3 Kara authored pass is incomplete: {missing_authored}")
    missing_threshold = [name for name in THRESHOLD_PASS_OBJECTS if bpy.data.objects.get(name) is None]
    if missing_threshold:
        raise RuntimeError(f"Wave 4 Kara threshold pass is incomplete: {missing_threshold}")
    missing_wave15 = [name for name in WAVE15_OBJECTS if bpy.data.objects.get(name) is None]
    if missing_wave15:
        raise RuntimeError(f"Wave 15 Kara silhouette pass is incomplete: {missing_wave15}")
    missing_wave17 = [name for name in WAVE17_OBJECTS if bpy.data.objects.get(name) is None]
    if missing_wave17:
        raise RuntimeError(f"Wave 17 Kara threshold pass is incomplete: {missing_wave17}")

    triangles = sum(len(mesh.data.loop_triangles) for mesh in meshes)
    if triangles > MAX_TRIANGLES:
        raise RuntimeError(f"Kara forest-edge triangle budget exceeded: {triangles} > {MAX_TRIANGLES}")
    for mesh in meshes:
        if not mesh.data.loop_triangles:
            raise RuntimeError(f"Degenerate mesh: {mesh.name}")
        lower_name = mesh.name.lower()
        if any(part in lower_name for part in FORBIDDEN_NAME_PARTS):
            raise RuntimeError(f"Collision-like mesh name leaked: {mesh.name}")

    images = [item for item in bpy.data.images if item.name not in ("Render Result", "Viewer Node")]
    if images:
        raise RuntimeError(f"Image textures are not allowed: {[image.name for image in images]}")

    forbidden_types = [
        obj.name
        for obj in bpy.data.objects
        if obj.type in ("CAMERA", "LIGHT", "CollisionShape") or obj.rigid_body is not None
    ]
    if forbidden_types:
        raise RuntimeError(f"Camera/light/physics objects are not allowed: {forbidden_types}")

    for name in EXPECTED_COMPONENTS:
        component = bpy.data.objects[name]
        if component.parent is not root:
            raise RuntimeError(f"Component root is not parented to the kit root: {name}")
    for name in THRESHOLD_PASS_OBJECTS:
        mesh = bpy.data.objects[name]
        expected_pass = WAVE15_PASS_ID if name in WAVE15_OBJECTS else THRESHOLD_PASS_ID
        if (
            mesh.type != "MESH"
            or mesh.parent.name not in EXPECTED_COMPONENTS
            or mesh.get("geometry_pass") != expected_pass
        ):
            raise RuntimeError(f"Threshold mesh has an invalid component parent/pass: {name}")
    for name in WAVE15_OBJECTS:
        mesh = bpy.data.objects[name]
        if (
            mesh.type != "MESH"
            or mesh.parent.name not in EXPECTED_COMPONENTS
            or mesh.get("geometry_pass") != WAVE15_PASS_ID
        ):
            raise RuntimeError(f"Wave 15 mesh has an invalid component parent/pass: {name}")
    for name in WAVE17_OBJECTS:
        mesh = bpy.data.objects[name]
        if (
            mesh.type != "MESH"
            or mesh.parent.name not in EXPECTED_COMPONENTS
            or mesh.get("geometry_pass") != WAVE17_PASS_ID
        ):
            raise RuntimeError(f"Wave 17 mesh has an invalid component parent/pass: {name}")

    return {
        "component_count": len(direct),
        "mesh_count": len(meshes),
        "triangle_count": triangles,
        "material_count": len({slot.material.name for mesh in meshes for slot in mesh.material_slots if slot.material}),
        "authored_pass": root.get("kara_authored_pass"),
        "threshold_pass": root.get("kara_threshold_pass"),
        "threshold_mesh_count": len(THRESHOLD_PASS_OBJECTS),
        "wave15_pass": root.get("kara_wave15_pass"),
        "wave15_mesh_count": len(WAVE15_OBJECTS),
        "wave17_pass": root.get("kara_wave17_pass"),
        "wave17_mesh_count": len(WAVE17_OBJECTS),
    }


def main() -> None:
    args = arguments()
    root_path = Path(args.root).resolve()
    blend_path = root_path / "assets/source/blender/act1/urman_kara_forest_edge_kit.blend"
    glb_path = root_path / "game/assets/models/act1/urman_kara_forest_edge_kit.glb"

    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None or root.type != "EMPTY":
        raise RuntimeError(f"Missing authored root: {ROOT_NAME}")

    changed = ensure_authored_pass(root)
    changed = ensure_threshold_pass(root) or changed
    changed = ensure_wave15_pass(root) or changed
    changed = ensure_wave17_pass(root) or changed
    if changed:
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    report = validate(root)

    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=False,
        export_apply=True,
    )
    for key, value in report.items():
        print(f"kara-export: {key}={value}")
    print(f"kara-export: exported {glb_path}")


if __name__ == "__main__":
    main()
