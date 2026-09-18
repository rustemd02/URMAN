"""Generate the first project-original Painterly Low-Poly modular kit.

Run with Blender 4.5 LTS:
  blender --background --python tools/blender/generate_modular_environment.py -- --root <repo>
"""

from __future__ import annotations

import argparse
import math
from pathlib import Path

import bpy


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    tokens = []
    if "--" in __import__("sys").argv:
        tokens = __import__("sys").argv[__import__("sys").argv.index("--") + 1 :]
    return parser.parse_args(tokens)


def material(name: str, color: tuple[float, float, float, float], roughness: float = 0.88) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.diffuse_color = color
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    return result


def tag(obj: bpy.types.Object, asset_id: str, budget: int, collision: str, lod_status: str = "LOD0") -> None:
    obj["urman_asset_id"] = asset_id
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["triangle_budget"] = budget
    obj["collision"] = collision
    obj["lod_status"] = lod_status


def bevel_object(obj: bpy.types.Object, width: float, segments: int = 1) -> None:
    """Apply a small authored edge break without adding another scene mesh."""
    if width <= 0.0:
        return
    modifier = obj.modifiers.new("URMAN_AuthoredEdgeBreak", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def cube(
    name: str,
    size: tuple[float, float, float],
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
    collision: bool = False,
    bevel_width: float = 0.0,
    bevel_segments: int = 1,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(surface)
    bevel_object(obj, bevel_width, bevel_segments)
    tag(obj, asset_id, budget, "simple-box" if collision else "none")
    return obj


def triangular_roof(name: str, location: tuple[float, float, float], surface: bpy.types.Material) -> bpy.types.Object:
    # A slightly overhanging, hand-broken gable reads less like a single
    # primitive while staying inside the 5k-triangle environment budget.
    width, depth, height = 7.2, 6.2, 1.75
    x, y, z = width / 2, depth / 2, height
    vertices = [
        (-x, -y, 0),
        (x, -y, 0),
        (x, y, 0),
        (-x, y, 0),
        (0, -y, z),
        (0, y, z),
    ]
    faces = [
        (0, 1, 4),
        (3, 5, 2),
        (0, 3, 2, 1),
        (0, 4, 5, 3),
        (1, 2, 5, 4),
    ]
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(surface)
    bevel_object(obj, 0.055, 1)
    tag(obj, "env.house.roof.a", 128, "none")
    return obj


def small_gable_roof(
    name: str,
    location: tuple[float, float, float],
    width: float,
    depth: float,
    height: float,
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
) -> bpy.types.Object:
    """Create a compact gable roof for authored yard/threshold props."""
    x, y = width / 2.0, depth / 2.0
    vertices = [
        (-x, -y, 0),
        (x, -y, 0),
        (x, y, 0),
        (-x, y, 0),
        (0, -y, height),
        (0, y, height),
    ]
    faces = [
        (0, 1, 4),
        (3, 5, 2),
        (0, 3, 2, 1),
        (0, 4, 5, 3),
        (1, 2, 5, 4),
    ]
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(surface)
    bevel_object(obj, 0.035, 1)
    tag(obj, asset_id, budget, "none")
    return obj


def collision_box(name: str, size: tuple[float, float, float], location: tuple[float, float, float]) -> None:
    collision_material = material(f"{name}Material", (0.04, 0.04, 0.04, 1.0))
    obj = cube(f"{name}-col", size, location, collision_material, name, 12, collision=True)
    obj.display_type = "WIRE"
    obj.hide_render = True


def house_interior_cube(
    name: str,
    size: tuple[float, float, float],
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    budget: int,
    asset_id: str = "env.house.interior.a",
    bevel_width: float = 0.0,
) -> bpy.types.Object:
    """Author a Godot-oriented interior box in Blender's Y-up coordinates."""
    x, y, z = location
    width, height, depth = size
    return cube(
        name,
        (width, depth, height),
        (x, -z, y),
        surface,
        asset_id,
        budget,
        bevel_width=bevel_width,
    )


def house_interior_tapered_box(
    name: str,
    height: float,
    location: tuple[float, float, float],
    bottom_size: tuple[float, float],
    top_size: tuple[float, float],
    surface: bpy.types.Material,
    budget: int,
    asset_id: str = "env.house.interior.a",
    bevel_width: float = 0.0,
) -> bpy.types.Object:
    """Author one slightly hand-built furniture silhouette in room coordinates."""
    x, y, z = location
    bottom_width, bottom_depth = bottom_size
    top_width, top_depth = top_size
    bottom_x = bottom_width / 2.0
    bottom_y = bottom_depth / 2.0
    top_x = top_width / 2.0
    top_y = top_depth / 2.0
    half_height = height / 2.0
    vertices = [
        (-bottom_x, -bottom_y, -half_height),
        (bottom_x, -bottom_y, -half_height),
        (bottom_x, bottom_y, -half_height),
        (-bottom_x, bottom_y, -half_height),
        (-top_x, -top_y, half_height),
        (top_x, -top_y, half_height),
        (top_x, top_y, half_height),
        (-top_x, top_y, half_height),
    ]
    faces = [
        (0, 3, 2, 1),
        (4, 5, 6, 7),
        (0, 1, 5, 4),
        (1, 2, 6, 5),
        (2, 3, 7, 6),
        (3, 0, 4, 7),
    ]
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = (x, -z, y)
    obj.data.materials.append(surface)
    bevel_object(obj, bevel_width)
    tag(obj, asset_id, budget, "none")
    return obj


def house_interior_vessel(
    name: str,
    radius: float,
    height: float,
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    budget: int = 220,
) -> bpy.types.Object:
    """Create a small, abstract low-poly household vessel in room coordinates."""
    x, y, z = location
    bpy.ops.mesh.primitive_cone_add(
        vertices=8,
        radius1=radius * 1.05,
        radius2=radius * 0.82,
        depth=height,
        location=(x, -z, y),
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(surface)
    bevel_object(obj, 0.014, 1)
    tag(obj, "env.house.interior.a", budget, "none")
    return obj


def create_house_interior(materials: dict[str, bpy.types.Material]) -> None:
    """Create the compact 12 m x 10 m authored house shell and silhouettes."""
    # Floorboards and edge trims keep the existing walkable footprint while
    # giving the room a continuous, readable floor field.
    for index in range(6):
        house_interior_cube(
            f"HouseInterior_FloorBoard_{index:02d}_LOD0",
            (1.94, 0.05, 9.70),
            (-5.0 + index * 2.0, 0.03, 0.0),
            materials["wood"],
            320,
            bevel_width=0.018,
        )
    house_interior_cube(
        "HouseInterior_BaseTrimBack_LOD0",
        (11.70, 0.26, 0.22),
        (0.0, 0.16, -4.84),
        materials["wood_dark"],
        260,
        bevel_width=0.025,
    )
    house_interior_cube(
        "HouseInterior_BaseTrimFront_LOD0",
        (11.70, 0.26, 0.22),
        (0.0, 0.16, 4.84),
        materials["wood_dark"],
        260,
        bevel_width=0.025,
    )

    house_interior_cube(
        "HouseInterior_CeilingField_LOD0",
        (11.80, 0.12, 9.80),
        (0.0, 3.34, 0.0),
        materials["plaster"],
        900,
        bevel_width=0.03,
    )
    for name, x, size in (
        ("HouseInterior_CeilingBeamLeft_LOD0", -3.40, (0.28, 0.28, 9.60)),
        ("HouseInterior_CeilingBeamRight_LOD0", 3.25, (0.22, 0.34, 9.60)),
    ):
        house_interior_cube(
            name,
            size,
            (x, 3.20, 0.0),
            materials["wood_dark"],
            420,
            bevel_width=0.025,
        )

    # Wall volumes deliberately retain the central front opening used by the
    # existing HouseExit target and its collision body. The rear wall carries
    # two real openings matching the exterior's two rear windows, so the shell
    # is not a sealed box behind framed glass: the interior collision segments
    # in StyleBenchmarkZone mirror these exact rectangles.
    back_wall_openings = ((-3.60, 1.06, 0.99, 2.51), (2.55, 1.06, 0.99, 2.51))
    for name, x0, x1 in (
        ("HouseInterior_BackWall_Left_LOD0", -5.90, -4.13),
        ("HouseInterior_BackWall_Mid_LOD0", -3.07, 2.02),
        ("HouseInterior_BackWall_Right_LOD0", 3.08, 5.90),
    ):
        house_interior_cube(
            name,
            (x1 - x0, 3.20, 0.22),
            ((x0 + x1) / 2, 1.68, -4.90),
            materials["plaster"],
            520,
            bevel_width=0.035,
        )
    for name, centre_x, y0, y1 in (
        ("HouseInterior_BackWall_UnderSillWest_LOD0", -3.60, 0.08, 0.99),
        ("HouseInterior_BackWall_UnderSillEast_LOD0", 2.55, 0.08, 0.99),
        ("HouseInterior_BackWall_HeadWest_LOD0", -3.60, 2.51, 3.28),
        ("HouseInterior_BackWall_HeadEast_LOD0", 2.55, 2.51, 3.28),
    ):
        house_interior_cube(
            name,
            (1.06, y1 - y0, 0.22),
            (centre_x, (y0 + y1) / 2, -4.90),
            materials["plaster"],
            260,
            bevel_width=0.035,
        )
    for name, x in (("HouseInterior_LeftWall_LOD0", -5.90), ("HouseInterior_RightWall_LOD0", 5.90)):
        house_interior_cube(
            name,
            (0.22, 3.20, 9.80),
            (x, 1.68, 0.0),
            materials["plaster"],
            1400,
            bevel_width=0.035,
        )
    for name, x in (("HouseInterior_FrontWallLeft_LOD0", -3.45), ("HouseInterior_FrontWallRight_LOD0", 3.45)):
        house_interior_cube(
            name,
            (5.10, 3.20, 0.22),
            (x, 1.68, 4.90),
            materials["plaster"],
            900,
            bevel_width=0.035,
        )
    house_interior_cube(
        "HouseInterior_FrontWallLintel_LOD0",
        (1.98, 0.22, 0.24),
        (0.0, 2.82, 4.90),
        materials["wood"],
        520,
        bevel_width=0.04,
    )

    house_interior_tapered_box(
        "HouseInterior_EntryDoor_LOD0",
        2.16,
        (0.0, 1.05, 4.80),
        (1.38, 0.10),
        (1.26, 0.10),
        materials["wood_dark"],
        520,
        bevel_width=0.045,
    )
    for name, x in (("HouseInterior_EntryFrameLeft_LOD0", -0.80), ("HouseInterior_EntryFrameRight_LOD0", 0.80)):
        house_interior_cube(
            name,
            (0.20, 2.40, 0.18),
            (x, 1.20, 4.72),
            materials["wood"],
            360,
            bevel_width=0.032,
        )
    house_interior_cube(
        "HouseInterior_EntryFrameTop_LOD0",
        (1.92, 0.18, 0.20),
        (0.0, 2.40, 4.72),
        materials["wood"],
        360,
        bevel_width=0.032,
    )
    house_interior_cube(
        "HouseInterior_EntryThreshold_LOD0",
        (1.72, 0.08, 0.42),
        (0.0, 0.05, 4.60),
        materials["wood_dark"],
        260,
        bevel_width=0.018,
    )

    # Two rear windows, one per opening. Each is a frame in the wall plane with
    # a muntin, a sill board and a glass pane set into the opening, so the shell
    # reads as a real opening rather than framed glass pasted on a solid wall.
    for label, centre_x in (("West", -3.60), ("East", 2.55)):
        house_interior_cube(
            f"HouseInterior_WindowRecess_{label}_LOD0",
            (0.24, 1.52, 0.22),
            (centre_x, 1.75, -4.90),
            materials["wood_dark"],
            300,
            bevel_width=0.035,
        )
        house_interior_cube(
            f"HouseInterior_WindowGlass_{label}_LOD0",
            (0.94, 1.40, 0.05),
            (centre_x, 1.75, -4.90),
            materials["warm"],
            300,
            bevel_width=0.018,
        )
        for name, x in (
            (f"HouseInterior_WindowFrameLeft_{label}_LOD0", centre_x - 0.57),
            (f"HouseInterior_WindowFrameRight_{label}_LOD0", centre_x + 0.57),
        ):
            house_interior_cube(
                name,
                (0.16, 1.66, 0.20),
                (x, 1.75, -4.70),
                materials["wood"],
                320,
                bevel_width=0.032,
            )
        for name, y in (
            (f"HouseInterior_WindowFrameTop_{label}_LOD0", 2.53),
            (f"HouseInterior_WindowFrameBottom_{label}_LOD0", 0.97),
        ):
            house_interior_cube(
                name,
                (1.24, 0.16, 0.20),
                (centre_x, y, -4.70),
                materials["wood"],
                320,
                bevel_width=0.032,
            )
        house_interior_cube(
            f"HouseInterior_WindowMuntin_{label}_LOD0",
            (0.06, 1.32, 0.05),
            (centre_x, 1.75, -4.62),
            materials["wood_dark"],
            180,
            bevel_width=0.012,
        )
        house_interior_cube(
            f"HouseInterior_WindowSill_{label}_LOD0",
            (1.30, 0.14, 0.42),
            (centre_x, 0.90, -4.56),
            materials["wood"],
            320,
            bevel_width=0.032,
        )

    # Furniture stays on the exact current clearances so the existing PC,
    # NPC and interaction coordinates remain valid.
    house_interior_cube(
        "HouseInterior_TableTop_LOD0",
        (3.20, 0.14, 1.35),
        (0.0, 0.82, -3.60),
        materials["wood"],
        800,
        bevel_width=0.045,
    )
    for name, x, z in (
        ("HouseInterior_TableLegLeft_LOD0", -1.35, -3.10),
        ("HouseInterior_TableLegRight_LOD0", 1.35, -3.10),
        ("HouseInterior_TableLegBackLeft_LOD0", -1.35, -4.10),
        ("HouseInterior_TableLegBackRight_LOD0", 1.35, -4.10),
    ):
        house_interior_cube(
            name,
            (0.18, 0.82, 0.18),
            (x, 0.40, z),
            materials["wood_dark"],
            320,
            bevel_width=0.022,
        )
    house_interior_cube(
        "HouseInterior_TableApron_LOD0",
        (2.75, 0.12, 0.10),
        (0.0, 0.69, -3.60),
        materials["wood_dark"],
        260,
        bevel_width=0.018,
    )
    house_interior_cube(
        "HouseInterior_ChairSeat_LOD0",
        (0.52, 0.07, 0.50),
        (-2.15, 0.435, -1.25),
        materials["wood"],
        420,
        bevel_width=0.035,
    )
    house_interior_tapered_box(
        "HouseInterior_ChairBack_LOD0",
        0.64,
        (-2.15, 0.77, -1.015),
        (0.52, 0.06),
        (0.48, 0.045),
        materials["wood"],
        420,
        bevel_width=0.015,
    )
    for name, x, z in (
        ("HouseInterior_ChairLegLeft_LOD0", -2.34, -1.41),
        ("HouseInterior_ChairLegRight_LOD0", -1.96, -1.41),
        ("HouseInterior_ChairLegBackLeft_LOD0", -2.34, -1.04),
        ("HouseInterior_ChairLegBackRight_LOD0", -1.96, -1.04),
    ):
        house_interior_cube(
            name,
            (0.07, 0.40, 0.07),
            (x, 0.20, z),
            materials["wood_dark"],
            220,
            bevel_width=0.018,
        )
    house_interior_tapered_box(
        "HouseInterior_CupboardBody_LOD0",
        2.42,
        (-4.82, 1.21, -3.88),
        (1.58, 0.82),
        (1.36, 0.70),
        materials["wood"],
        700,
        bevel_width=0.055,
    )
    house_interior_cube(
        "HouseInterior_CupboardDoor_LOD0",
        (1.16, 2.12, 0.07),
        (-4.82, 1.22, -3.49),
        materials["wood_dark"],
        360,
        bevel_width=0.032,
    )

    # The textile is deliberately quiet: broad bands read as a lived-in rug,
    # not as an ethnic symbol or a decorative theme-park motif.
    house_interior_cube(
        "HouseInterior_RugField_LOD0",
        (4.05, 0.025, 2.40),
        (-1.35, 0.070, 0.45),
        materials["fabric"],
        420,
        bevel_width=0.018,
    )
    for name, z, surface in (
        ("HouseInterior_RugBandA_LOD0", 0.05, materials["wood"]),
        ("HouseInterior_RugBandB_LOD0", 0.72, materials["fabric"]),
    ):
        house_interior_cube(
            name,
            (4.05, 0.032, 0.16),
            (-1.35, 0.04, z + 0.05),
            surface,
            240,
            bevel_width=0.012,
        )

    # The reverse/right edge cluster closes the empty half of the room while
    # keeping the centre lane, table approach and front exit unoccupied. It is
    # deliberately a single domestic grouping rather than decorative clutter.
    house_interior_tapered_box(
        "HouseInterior_DaybedFrame_LOD0",
        0.50,
        (4.78, 0.25, 1.25),
        (1.36, 2.95),
        (1.18, 2.78),
        materials["wood"],
        640,
        bevel_width=0.055,
    )
    house_interior_tapered_box(
        "HouseInterior_DaybedCushion_LOD0",
        0.23,
        (4.78, 0.59, 1.25),
        (1.20, 2.70),
        (1.05, 2.54),
        materials["fabric"],
        420,
        bevel_width=0.06,
    )
    house_interior_tapered_box(
        "HouseInterior_DaybedBack_LOD0",
        0.86,
        (5.48, 0.81, 1.25),
        (0.18, 2.95),
        (0.16, 2.72),
        materials["fabric"],
        420,
        bevel_width=0.055,
    )
    house_interior_tapered_box(
        "HouseInterior_StorageChestBody_LOD0",
        0.72,
        (3.98, 0.38, -4.20),
        (1.78, 0.92),
        (1.58, 0.80),
        materials["wood"],
        520,
        bevel_width=0.055,
    )
    chest_lid = house_interior_cube(
        "HouseInterior_StorageChestLid_LOD0",
        (1.92, 0.14, 0.98),
        (3.98, 0.78, -4.20),
        materials["wood_dark"],
        320,
        bevel_width=0.042,
    )
    chest_lid.rotation_euler[0] = math.radians(-4.0)
    house_interior_cube(
        "HouseInterior_StorageChestFront_LOD0",
        (1.46, 0.26, 0.07),
        (3.98, 0.38, -3.70),
        materials["wood_dark"],
        260,
        bevel_width=0.024,
    )
    house_interior_cube(
        "HouseInterior_RightShelfBoard_LOD0",
        (0.48, 0.16, 2.70),
        (5.62, 2.08, 1.25),
        materials["wood_dark"],
        320,
        bevel_width=0.032,
    )
    house_interior_cube(
        "HouseInterior_RightShelfBack_LOD0",
        (0.14, 0.70, 2.70),
        (5.80, 2.28, 1.25),
        materials["wood"],
        320,
        bevel_width=0.032,
    )
    for name, radius, height, z, surface in (
        ("HouseInterior_ShelfVesselA_LOD0", 0.14, 0.38, 0.47, materials["plaster"]),
        ("HouseInterior_ShelfVesselB_LOD0", 0.11, 0.30, 1.25, materials["stone"]),
        ("HouseInterior_ShelfVesselC_LOD0", 0.13, 0.44, 2.03, materials["wood_dark"]),
    ):
        house_interior_vessel(
            name,
            radius,
            height,
            (5.48, 2.12 + height * 0.5, z),
            surface,
        )
    house_interior_cube(
        "HouseInterior_RightRunnerField_LOD0",
        (1.42, 0.025, 3.65),
        (3.62, 0.025, 1.15),
        materials["fabric"],
        420,
        bevel_width=0.018,
    )
    house_interior_cube(
        "HouseInterior_RightRunnerBand_LOD0",
        (1.42, 0.032, 0.14),
        (3.62, 0.04, -0.60),
        materials["fabric"],
        220,
        bevel_width=0.012,
    )
    house_interior_cube(
        "HouseInterior_RightRunnerHem_LOD0",
        (1.42, 0.032, 0.08),
        (3.62, 0.04, 2.90),
        materials["fabric"],
        220,
        bevel_width=0.012,
    )
    create_house_lived_in_cluster(materials)


def create_house_lived_in_cluster(materials: dict[str, bpy.types.Material]) -> None:
    """Add one restrained domestic cluster without narrowing the interaction lane.

    The left-wall heater, high cupboard, tableware and floor basket add the
    missing vertical and low foreground hierarchy to reverse house views. All
    pieces remain presentation-only; the one large floor footprint gets its
    gameplay proxy in StyleBenchmarkZone, alongside the existing furniture
    proxies.
    """
    # A compact matte heater sits against the blank left wall. The body clears
    # the room centre, while the flue stops below the authored ceiling field.
    house_interior_cube(
        "HouseInterior_HearthBase_LOD0",
        (1.50, 0.14, 1.10),
        (-5.00, 0.07, 0.55),
        materials["stone"],
        360,
        bevel_width=0.025,
    )
    house_interior_tapered_box(
        "HouseInterior_HearthBody_LOD0",
        1.04,
        (-5.00, 0.64, 0.55),
        (1.22, 0.96),
        (1.04, 0.82),
        materials["stone"],
        540,
        bevel_width=0.065,
    )
    house_interior_cube(
        "HouseInterior_HearthTop_LOD0",
        (1.28, 0.12, 1.02),
        (-5.00, 1.20, 0.55),
        materials["stone"],
        320,
        bevel_width=0.03,
    )
    house_interior_cube(
        "HouseInterior_HearthDoor_LOD0",
        (0.05, 0.60, 0.66),
        (-4.40, 0.65, 0.55),
        materials["wood_dark"],
        300,
        bevel_width=0.018,
    )
    house_interior_cube(
        "HouseInterior_HearthHandle_LOD0",
        (0.12, 0.08, 0.08),
        (-4.32, 0.65, 0.55),
        materials["wood_dark"],
        160,
        bevel_width=0.014,
    )
    house_interior_cube(
        "HouseInterior_HearthFlue_LOD0",
        (0.20, 1.55, 0.20),
        (-5.00, 2.00, 0.55),
        materials["wood_dark"],
        300,
        bevel_width=0.018,
    )
    bpy.context.scene.objects.get("HouseInterior_HearthFlue_LOD0").rotation_euler[1] = math.radians(0.8)
    house_interior_cube(
        "HouseInterior_HearthFlueCollar_LOD0",
        (0.34, 0.08, 0.34),
        (-5.00, 1.28, 0.55),
        materials["wood_dark"],
        180,
        bevel_width=0.014,
    )

    # A small high cupboard breaks the left-wall plane without competing with
    # the existing rear still-life shelf or either NPC position.
    house_interior_tapered_box(
        "HouseInterior_LeftWallCupboardBody_LOD0",
        0.92,
        (-5.62, 2.10, 2.65),
        (0.64, 1.06),
        (0.54, 0.94),
        materials["wood"],
        460,
        bevel_width=0.045,
    )
    house_interior_cube(
        "HouseInterior_LeftWallCupboardDoor_LOD0",
        (0.06, 0.76, 0.90),
        (-5.30, 2.08, 2.65),
        materials["wood_dark"],
        260,
        bevel_width=0.024,
    )
    house_interior_cube(
        "HouseInterior_LeftWallCupboardTop_LOD0",
        (0.70, 0.12, 1.18),
        (-5.62, 2.68, 2.65),
        materials["wood"],
        220,
        bevel_width=0.024,
    )

    # Quiet table forms add scale at the existing table edge while keeping the
    # CRT, documents and all narrative targets unobstructed.
    house_interior_cube(
        "HouseInterior_TableTray_LOD0",
        (1.16, 0.04, 0.68),
        (1.10, 0.94, -3.62),
        materials["wood"],
        280,
        bevel_width=0.018,
    )
    create_table_kettle(materials)
    house_interior_vessel(
        "HouseInterior_TableBowl_LOD0",
        0.20,
        0.10,
        (1.38, 1.01, -3.74),
        materials["plaster"],
        180,
    )

    # A low woven storage basket gives the left foreground a soft silhouette;
    # its handle is intentionally a simple broad arc cue rather than a symbol.
    house_interior_vessel(
        "HouseInterior_StorageBasketBody_LOD0",
        0.30,
        0.45,
        (-4.45, 0.25, -0.70),
        materials["wood"],
        300,
    )
    house_interior_cube(
        "HouseInterior_StorageBasketRim_LOD0",
        (0.66, 0.06, 0.66),
        (-4.45, 0.50, -0.70),
        materials["wood_dark"],
        180,
        bevel_width=0.014,
    )
    house_interior_cube(
        "HouseInterior_StorageBasketHandle_LOD0",
        (0.62, 0.08, 0.08),
        (-4.45, 0.72, -0.70),
        materials["wood_dark"],
        180,
        bevel_width=0.014,
    )

    # The old PC shares the room's table anchor, but its separate published
    # OldPc_ family is intentionally kept at the exact 8/8 contract. Build the
    # surrounding joinery in the presentation-only HouseInterior_ layer: a
    # shallow wall hutch frames the screen, gives the documents a believable
    # domestic/archive context and closes the otherwise blank rear wall.
    house_interior_tapered_box(
        "HouseInterior_OldPcBackboard_LOD0",
        1.82,
        (0.0, 1.68, -4.68),
        (3.30, 0.14),
        (3.10, 0.12),
        materials["wood_dark"],
        680,
        bevel_width=0.055,
    )
    for name, x in (
        ("HouseInterior_OldPcHutchCleatLeft_LOD0", -1.46),
        ("HouseInterior_OldPcHutchCleatRight_LOD0", 1.46),
    ):
        house_interior_cube(
            name,
            (0.18, 1.58, 0.18),
            (x, 1.70, -4.57),
            materials["wood"],
            300,
            bevel_width=0.032,
        )
    house_interior_cube(
        "HouseInterior_OldPcHutchShelf_LOD0",
        (2.96, 0.14, 0.52),
        (0.0, 2.26, -4.43),
        materials["wood"],
        360,
        bevel_width=0.034,
    )
    house_interior_cube(
        "HouseInterior_OldPcHutchTop_LOD0",
        (3.62, 0.18, 0.56),
        (0.0, 2.66, -4.43),
        materials["wood_dark"],
        360,
        bevel_width=0.036,
    )
    house_interior_cube(
        "HouseInterior_OldPcHutchFolder_LOD0",
        (0.58, 0.30, 0.34),
        (-0.96, 2.48, -4.18),
        materials["fabric"],
        260,
        bevel_width=0.024,
    )
    folio = house_interior_cube(
        "HouseInterior_OldPcDocumentFolio_LOD0",
        (0.38, 0.028, 0.48),
        (-0.98, 0.912, -3.52),
        materials["plaster"],
        300,
        bevel_width=0.018,
    )
    # A slight yaw reads as a handled folder rather than another axis-aligned
    # debug slab; it stays wholly on the existing tabletop footprint.
    folio.rotation_euler[2] = math.radians(-5.0)

    # One low, constructed wainscot run breaks the left wall's empty plane and
    # gives the reverse turn a strong horizontal/vertical domestic rhythm. It
    # stops before the hearth so neither form gains a new gameplay clearance.
    house_interior_cube(
        "HouseInterior_LeftWallWainscotField_LOD0",
        (0.12, 0.88, 4.45),
        (-5.72, 0.64, -2.25),
        materials["wood"],
        420,
        bevel_width=0.032,
    )
    house_interior_cube(
        "HouseInterior_LeftWallWainscotRail_LOD0",
        (0.16, 0.14, 4.60),
        (-5.70, 1.10, -2.25),
        materials["wood_dark"],
        260,
        bevel_width=0.026,
    )
    house_interior_cube(
        "HouseInterior_TableFootRail_LOD0",
        (2.78, 0.12, 0.14),
        (0.0, 0.40, -3.60),
        materials["wood_dark"],
        260,
        bevel_width=0.024,
    )

def create_house(materials: dict[str, bpy.types.Material]) -> None:
    cube(
        "HouseA_Walls_LOD0",
        (6.2, 5.2, 2.8),
        (0, 0, 1.4),
        materials["plaster"],
        "env.house.a",
        5000,
        bevel_width=0.075,
        bevel_segments=2,
    )
    triangular_roof("HouseA_Roof_LOD0", (0, 0, 2.8), materials["roof"])
    cube(
        "HouseA_WindowFrame_LOD0",
        (1.25, 0.12, 1.05),
        (1.15, -2.64, 1.55),
        materials["wood"],
        "env.house.window.a",
        500,
        bevel_width=0.045,
        bevel_segments=1,
    )
    cube(
        "HouseA_WindowGlow_LOD0",
        (0.92, 0.06, 0.72),
        (1.15, -2.72, 1.55),
        materials["warm"],
        "env.house.window.glow",
        128,
        bevel_width=0.035,
        bevel_segments=1,
    )
    # A bounded facade pass keeps the HouseA module readable at the fixed
    # first-person street distance without turning it into a bespoke hero
    # building. These are presentation-only meshes; the existing Godot
    # provisional house proxy remains the sole imported-kit collision owner.
    cube(
        "HouseA_Foundation_LOD0",
        (6.45, 5.45, 0.42),
        (0, 0, 0.18),
        materials["wood_dark"],
        "env.house.foundation.a",
        500,
        bevel_width=0.045,
    )
    cube(
        "HouseA_Door_LOD0",
        (1.0, 0.10, 2.05),
        (-1.55, -2.68, 1.03),
        materials["wood_dark"],
        "env.house.door.a",
        500,
        bevel_width=0.04,
    )
    cube(
        "HouseA_DoorFrame_LOD0",
        (1.28, 0.12, 0.12),
        (-1.55, -2.74, 2.12),
        materials["wood"],
        "env.house.door.a",
        500,
        bevel_width=0.025,
    )
    cube(
        "HouseA_WindowTrimTop_LOD0",
        (1.48, 0.12, 0.12),
        (1.15, -2.74, 2.12),
        materials["wood"],
        "env.house.window.a",
        500,
        bevel_width=0.025,
    )
    cube(
        "HouseA_WindowTrimBottom_LOD0",
        (1.48, 0.12, 0.12),
        (1.15, -2.74, 0.98),
        materials["wood"],
        "env.house.window.a",
        500,
        bevel_width=0.025,
    )
    cube(
        "HouseA_Porch_LOD0",
        (2.20, 1.0, 0.18),
        (-1.55, -3.05, 0.20),
        materials["wood"],
        "env.house.porch.a",
        500,
        bevel_width=0.035,
    )
    cube(
        "HouseA_PorchStep_LOD0",
        (1.80, 0.55, 0.16),
        (-1.55, -3.45, 0.08),
        materials["wood_dark"],
        "env.house.porch.a",
        500,
        bevel_width=0.03,
    )
    cube(
        "HouseA_FrontEave_LOD0",
        (6.60, 0.18, 0.18),
        (0, -2.66, 2.78),
        materials["wood_dark"],
        "env.house.roof.a",
        500,
        bevel_width=0.025,
    )
    collision_box("HouseA", (6.2, 5.2, 2.8), (0, 0, 1.4))


def create_fence(materials: dict[str, bpy.types.Material]) -> None:
    origin_x = 9.0
    for index in range(5):
        cube(
            f"FenceA_Post_{index:02d}_LOD0",
            (0.16, 0.16, 1.25),
            (origin_x + index * 0.75, 0, 0.625),
            materials["wood"],
            "env.fence.a",
            500,
            bevel_width=0.025,
        )
    cube(
        "FenceA_Rail_LOD0",
        (3.2, 0.12, 0.12),
        (origin_x + 1.5, 0, 0.82),
        materials["wood"],
        "env.fence.a",
        500,
        bevel_width=0.02,
    )


def create_yard_props(materials: dict[str, bpy.types.Material]) -> None:
    """Create readable, non-interactive village yard anchors for first-person framing."""
    well_x, well_y = 5.6, -0.8
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=10,
        radius=0.78,
        depth=0.26,
        location=(well_x, well_y, 0.13),
    )
    rim = bpy.context.object
    rim.name = "WellA_Rim_LOD0"
    rim.data.materials.append(materials["stone"])
    bevel_object(rim, 0.045, 1)
    tag(rim, "env.well.a", 1200, "none")

    bpy.ops.mesh.primitive_cylinder_add(
        vertices=10,
        radius=0.56,
        depth=0.08,
        location=(well_x, well_y, 0.30),
    )
    water = bpy.context.object
    water.name = "WellA_Water_LOD0"
    water.data.materials.append(materials["water_dark"])
    tag(water, "env.well.a", 300, "none")

    for name, x in (("WellA_PostLeft_LOD0", well_x - 0.58), ("WellA_PostRight_LOD0", well_x + 0.58)):
        cube(name, (0.18, 0.18, 1.9), (x, well_y, 1.2), materials["wood"], "env.well.a", 500, bevel_width=0.025)
    cube(
        "WellA_Header_LOD0",
        (1.45, 0.20, 0.20),
        (well_x, well_y, 2.12),
        materials["wood_dark"],
        "env.well.a",
        400,
        bevel_width=0.025,
    )
    small_gable_roof(
        "WellA_Roof_LOD0",
        (well_x, well_y, 2.16),
        2.0,
        1.35,
        0.48,
        materials["roof"],
        "env.well.a",
        800,
    )
    cube(
        "WellA_Bucket_LOD0",
        (0.42, 0.42, 0.52),
        (well_x + 0.88, well_y + 0.08, 0.56),
        materials["wood_dark"],
        "env.well.a",
        500,
        bevel_width=0.035,
    )

    for index in range(4):
        row = index // 2
        column = index % 2
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=8,
            radius=0.18,
            depth=1.85,
            location=(3.8, 2.2 + column * 0.38, 0.22 + row * 0.38),
            rotation=(0.0, math.pi / 2.0, 0.0),
        )
        log = bpy.context.object
        log.name = f"WoodpileA_Log_{index:02d}_LOD0"
        log.data.materials.append(materials["bark"])
        bevel_object(log, 0.025, 1)
        tag(log, "env.woodpile.a", 700, "none")


def create_boundary_gate(materials: dict[str, bpy.types.Material]) -> None:
    """Create a quiet threshold gate beside the authored PineA anchor.

    The Blender Y coordinate is chosen so the exported gate lands near the
    existing Act 5 boundary interaction at world x≈0/z≈−7 when PineA is the
    placement reference. It remains decorative and has no collision tag.
    """
    gate_y = 9.4
    for name, x in (("GateA_PostLeft_LOD0", 14.4), ("GateA_PostRight_LOD0", 16.4)):
        cube(name, (0.18, 0.18, 1.8), (x, gate_y, 0.9), materials["wood_dark"], "env.gate.a", 500, bevel_width=0.025)
    cube(
        "GateA_Crossbar_LOD0",
        (2.2, 0.16, 0.18),
        (15.4, gate_y, 1.58),
        materials["wood"],
        "env.gate.a",
        500,
        bevel_width=0.025,
    )
    cube(
        "GateA_Ribbon_LOD0",
        (0.12, 0.035, 0.72),
        (15.4, gate_y - 0.12, 1.08),
        materials["fabric"],
        "env.gate.a",
        256,
        bevel_width=0.012,
    )


def create_road(materials: dict[str, bpy.types.Material]) -> None:
    cube(
        "RoadDirt_Straight_LOD0",
        (5.2, 10.0, 0.08),
        (0, 9, 0.04),
        materials["earth"],
        "env.road.dirt.straight",
        5000,
        bevel_width=0.025,
    )


def create_pine(materials: dict[str, bpy.types.Material]) -> None:
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.18, depth=3.2, location=(10, 8, 1.6))
    trunk = bpy.context.object
    trunk.name = "PineA_Trunk_LOD0"
    trunk.data.materials.append(materials["bark"])
    tag(trunk, "env.vegetation.pine.a", 1200, "capsule")
    crown_parts = []
    tiers = (
        (1.42, 1.62, 3.20, 0.00, 0.00, 0.0),
        (1.18, 1.42, 4.05, -0.14, 0.06, 13.0),
        (0.92, 1.18, 4.82, 0.16, -0.08, -18.0),
        (0.60, 0.95, 5.48, -0.08, 0.12, 9.0),
    )
    for radius, depth, z, offset_x, offset_y, rotation in tiers:
        bpy.ops.mesh.primitive_cone_add(
            vertices=7,
            radius1=radius,
            radius2=0.04,
            depth=depth,
            location=(10 + offset_x, 8 + offset_y, z),
            rotation=(0.0, 0.0, math.radians(rotation)),
        )
        crown_parts.append(bpy.context.object)

    bpy.ops.object.select_all(action="DESELECT")
    for part in crown_parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = crown_parts[0]
    bpy.ops.object.join()
    crown = crown_parts[0]
    crown.name = "PineA_Crown_LOD0"
    crown.data.materials.append(materials["foliage"])
    tag(crown, "env.vegetation.pine.a", 1200, "none")


def household_mesh(name, vertices, faces, location, surface, asset_id, budget):
    """Publish joined household details under the existing object/anchor contract."""
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv = mesh.uv_layers.new(name="HouseholdSurface")
    for polygon in mesh.polygons:
        # Keep an explicit, metric UV projection even where the game currently
        # uses a sheltered painterly override for the complete mesh.
        axis = max(range(3), key=lambda index: abs(polygon.normal[index]))
        axes = [index for index in range(3) if index != axis]
        for loop_index in polygon.loop_indices:
            point = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (point[axes[0]], point[axes[1]])
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget, "none")
    obj["household_detail_version"] = 1
    return obj


def append_household_lathe(vertices, faces, profile, sides=16):
    """Closed profile around Blender Z; winding follows the profile outline."""
    rings = []
    for radius, height in profile:
        first = len(vertices)
        if radius == 0:
            vertices.append((0, 0, height))
            rings.append([first])
        else:
            vertices.extend((radius * math.cos(index * math.tau / sides),
                             radius * math.sin(index * math.tau / sides), height)
                            for index in range(sides))
            rings.append(list(range(first, first + sides)))
    for lower, upper in zip(rings, rings[1:]):
        for index in range(sides):
            other = (index + 1) % sides
            if len(lower) == 1:
                faces.append((lower[0], upper[other], upper[index]))
            elif len(upper) == 1:
                faces.append((lower[index], lower[other], upper[0]))
            else:
                faces.append((lower[index], lower[other], upper[other], upper[index]))


def append_household_tube(vertices, faces, path, radii, sides=8, close_ends=True):
    """Sweep a ring through a YZ path. X stays the perpendicular frame axis."""
    from mathutils import Vector
    first = len(vertices)
    for index, point in enumerate(path):
        previous, following = path[max(0, index - 1)], path[min(len(path) - 1, index + 1)]
        tangent = (Vector(following) - Vector(previous)).normalized()
        across = tangent.cross(Vector((1, 0, 0)))
        for side in range(sides):
            angle = side * math.tau / sides
            offset = Vector((1, 0, 0)) * math.cos(angle) + across * math.sin(angle)
            vertices.append(tuple(Vector(point) + offset * radii[index]))
    for ring in range(len(path) - 1):
        for index in range(sides):
            other = (index + 1) % sides
            a, b = first + ring * sides + index, first + ring * sides + other
            faces.append((a, b, b + sides, a + sides))
    if close_ends:
        faces.append(tuple(first + index for index in reversed(range(sides))))
        faces.append(tuple(first + (len(path) - 1) * sides + index for index in range(sides)))
    return first


def create_table_kettle(materials):
    # The complete kettle stays inside the old 342 x 342 mm footprint and
    # .96..1.31 m vertical envelope. Its base still rests on the original tray.
    vertices, faces = [], []
    append_household_lathe(vertices, faces, [
        (0, -.150), (.079, -.150), (.099, -.141), (.112, -.115),
        (.116, -.065), (.113, .015), (.104, .080), (.084, .122),
        (.064, .144), (.063, .150), (.055, .150), (.055, .136),
        (.076, .113), (.096, .072), (.105, .012), (.107, -.065),
        (.101, -.111), (.085, -.137), (0, -.137)])
    # An actual open spout, including the inside wall and rolled mouth. The
    # root ends inside the vessel; no disk fills the visible outlet.
    spout = [(0, -.094, -.020), (0, -.119, .020), (0, -.141, .068),
             (0, -.148, .115)]
    radii = [.031, .028, .023, .020]
    outer = append_household_tube(vertices, faces, spout, radii, close_ends=False)
    inner = append_household_tube(vertices, faces, spout,
                                  [radius - .004 for radius in radii], close_ends=False)
    # Reverse the inner tube's faces; connect both rims without sealing them.
    inner_face_count = (len(spout) - 1) * 8
    for index in range(len(faces) - inner_face_count, len(faces)):
        faces[index] = tuple(reversed(faces[index]))
    for ring in (0, len(spout) - 1):
        for side in range(8):
            nxt = (side + 1) % 8
            a, b = outer + ring * 8 + side, outer + ring * 8 + nxt
            c, d = inner + ring * 8 + side, inner + ring * 8 + nxt
            faces.append((a, c, d, b) if ring == 0 else (a, b, d, c))
    # Open D handle, with both ends seated in the same body's shoulder/base.
    handle = [(0, .089, .080), (0, .119, .092), (0, .147, .085),
              (0, .156, .063), (0, .158, .025), (0, .153, -.023),
              (0, .138, -.056), (0, .109, -.069), (0, .098, -.066)]
    append_household_tube(vertices, faces, handle, [.012] * len(handle))
    kettle = household_mesh("HouseInterior_TableKettle_LOD0", vertices, faces,
                            (1.08, 3.62, 1.11), materials["stone"], "env.house.interior.a", 1200)
    kettle["household_form"] = "hollow enamel kettle; open spout; open D handle; seated base"

    vertices, faces = [], []
    # A round lid seats into the neck; the small grip and its stem are part of
    # this existing lid object, whose pivot remains exactly where it was.
    append_household_lathe(vertices, faces, [
        (0, -.034), (.052, -.034), (.059, -.027), (.067, -.025),
        (.067, -.020), (.059, -.015), (.045, -.010), (.015, -.007),
        (.012, -.004), (.012, .003), (.022, .006), (.024, .013),
        (.020, .020), (0, .020)])
    lid = household_mesh("HouseInterior_TableKettleLid_LOD0", vertices, faces,
                         (1.08, 3.62, 1.29), materials["wood_dark"], "env.house.interior.a", 500)
    lid["household_form"] = "round seated lid; raised stem and finger grip"


def create_old_pc_keyboard(surface):
    vertices, faces = [], []

    def rectangular_loft(rings):
        first = len(vertices)
        for x, y, width, depth, front_z, back_z in rings:
            vertices.extend([(x - width / 2, y - depth / 2, front_z),
                             (x + width / 2, y - depth / 2, front_z),
                             (x + width / 2, y + depth / 2, back_z),
                             (x - width / 2, y + depth / 2, back_z)])
        faces.append(tuple(first + index for index in (3, 2, 1, 0)))
        for ring in range(len(rings) - 1):
            for index in range(4):
                a, b = first + ring * 4 + index, first + ring * 4 + (index + 1) % 4
                faces.append((a, b, b + 4, a + 4))
        faces.append(tuple(first + (len(rings) - 1) * 4 + index for index in range(4)))

    # Same origin and enclosing box as the former slab. Low front lip, taller
    # rear case and inset key stems produce useful shadows under one material.
    rectangular_loft([(0, 0, 1.11, .44, -.05, -.05),
                      (0, 0, 1.15, .48, -.039, -.039),
                      (0, 0, 1.15, .48, -.006, .026),
                      (0, 0, 1.125, .455, .001, .032)])
    keys = []
    pitch, left = .047, -.544

    def key(column, row, width=1, height=1, group="typing"):
        x = left + (column + width / 2) * pitch
        y = -.151 + row * .053 + (height - 1) * .053 / 2
        width_m, depth_m = width * pitch - .006, height * .053 - .008
        base = .001 + (y + .2275) * .031 / .455
        rectangular_loft([(x, y, width_m - .008, depth_m - .008, base - .002, base - .002),
                          (x, y, width_m, depth_m, base + .012, base + .012),
                          (x, y, width_m - .005, depth_m - .005, base + .018, base + .018)])
        keys.append((group, x, y, width_m, depth_m, base + .018))

    # A 104-key ANSI arrangement: five typing rows, separated function row,
    # navigation block, inverted-T arrows and a full numeric keypad.
    for row, widths in enumerate([
        [1.25, 1.25, 1.25, 6.25, 1.25, 1.25, 1.25, 1.25],
        [2.25] + [1] * 10 + [2.75],
        [1.75] + [1] * 11 + [2.25],
        [1.5] + [1] * 12 + [1.5],
        [1] * 13 + [2],
    ]):
        column = 0
        for width in widths:
            key(column, row, width)
            column += width
    key(0, 6, group="function")
    for index in range(12):
        key(2 + index + (index // 4) * .45, 6, group="function")
    for column in range(3):
        key(15.5 + column, 6, group="navigation")
        key(15.5 + column, 4, group="navigation")
        key(15.5 + column, 3, group="navigation")
        key(15.5 + column, 0, group="arrows")
    key(16.5, 1, group="arrows")
    for column in range(4):
        key(19 + column, 4, group="numeric")
    for row in (3, 2, 1):
        for column in range(3):
            key(19 + column, row, group="numeric")
    key(22, 2, height=2, group="numeric")
    key(22, 0, height=2, group="numeric")
    key(19, 0, width=2, group="numeric")
    key(21, 0, group="numeric")
    # The separated F-row also stays below the old .05 m half-height.
    keyboard = household_mesh("OldPc_Keyboard_LOD0", vertices, faces, (-9, 7.3, .96),
                              surface, "prop.oldpc.keyboard", 5000)
    keyboard["household_form"] = "104 separated keycaps; typing function navigation arrows numeric blocks"
    keyboard["keyboard_keys"] = len(keys)
    keyboard["keyboard_key_centers"] = [value for item in keys for value in item[1:]]


def create_furniture_and_pc(materials: dict[str, bpy.types.Material]) -> None:
    cube(
        "TableA_Top_LOD0",
        (3.2, 1.35, 0.14),
        (-9, 8, 0.82),
        materials["wood"],
        "prop.furniture.table.a",
        5000,
        bevel_width=0.045,
    )
    for x in (-10.35, -7.65):
        for y in (7.5, 8.5):
            cube(
                "TableA_Leg_LOD0",
                (0.18, 0.18, 0.82),
                (x, y, 0.41),
                materials["wood_dark"],
                "prop.furniture.table.a",
                5000,
                bevel_width=0.025,
            )
    cube(
        "OldPc_Crt_LOD0",
        (1.4, 0.72, 1.15),
        (-9, 8, 1.46),
        materials["pc"],
        "prop.oldpc.crt",
        20000,
        bevel_width=0.105,
        bevel_segments=2,
    )
    cube(
        "OldPc_Glass_LOD0",
        (0.92, 0.04, 0.62),
        (-9, 7.62, 1.54),
        materials["screen"],
        "prop.oldpc.crt",
        20000,
        bevel_width=0.055,
        bevel_segments=2,
    )
    create_old_pc_keyboard(materials["pc_light"])
    # The CRT is a hero prop rather than a single anonymous box: a compact
    # tower, recessed front panel and one tactile power button give the player
    # a readable silhouette at the fixed first-person interaction distance.
    cube(
        "OldPc_Tower_LOD0",
        (0.52, 0.72, 1.06),
        (-7.95, 8.0, 1.40),
        materials["pc"],
        "prop.oldpc.crt",
        20000,
        bevel_width=0.075,
        bevel_segments=2,
    )
    cube(
        "OldPc_TowerPanel_LOD0",
        (0.30, 0.035, 0.52),
        (-7.95, 7.62, 1.52),
        materials["pc_dark"],
        "prop.oldpc.crt",
        20000,
        bevel_width=0.018,
        bevel_segments=1,
    )
    cube(
        "OldPc_PowerButton_LOD0",
        (0.10, 0.045, 0.10),
        (-7.95, 7.59, 1.82),
        materials["pc_light"],
        "prop.oldpc.crt",
        20000,
        bevel_width=0.018,
        bevel_segments=1,
    )
    # Two restrained hero-prop details keep the CRT readable at the fixed
    # first-person interaction distance without introducing gameplay parts:
    # a shallow drive slot on the tower and a faded identification plate on
    # the lower CRT shell. Both remain presentation-only and inherit the
    # existing deterministic OldPc LOD/material policy.
    cube(
        "OldPc_DriveSlot_LOD0",
        (0.23, 0.04, 0.07),
        (-7.95, 7.585, 1.66),
        materials["pc_dark"],
        "prop.oldpc.crt",
        5000,
        bevel_width=0.012,
        bevel_segments=1,
    )
    cube(
        "OldPc_LabelPlate_LOD0",
        (0.42, 0.035, 0.08),
        (-9.0, 7.615, 1.13),
        materials["pc_light"],
        "prop.oldpc.crt",
        5000,
        bevel_width=0.012,
        bevel_segments=1,
    )
    collision_box("OldPc", (1.4, 0.72, 1.15), (-9, 8, 1.46))


def constrain_household_lod(lod, source):
    """Keep only the three new household LODs inside their original source cage."""
    from mathutils import Vector
    cages = {
        "HouseInterior_TableKettle_LOD0": ((-.171325, -.171325, -.15), (.171325, .171325, .15)),
        "HouseInterior_TableKettleLid_LOD0": ((-.17, -.17, -.034), (.17, .17, .02)),
        "OldPc_Keyboard_LOD0": ((-.575, -.24, -.05), (.575, .24, .05)),
    }
    if source.name not in cages:
        return
    minimum, maximum = (Vector(bound) for bound in cages[source.name])
    # Export 37's Decimate result moved the kettle base by 79.4 micrometres
    # and the lid by 55.9 micrometres. Correct that bounded simplification
    # drift geometrically; larger changes remain an export failure.
    correction_limit = .0001
    mesh = lod.data
    mesh.calc_loop_triangles()
    before = []
    for triangle in mesh.loop_triangles:
        indices = tuple(triangle.vertices)
        a, b, c = (mesh.vertices[index].co for index in indices)
        before.append((indices, (b - a).cross(c - a)))
    adjusted, largest = 0, 0.0
    for vertex in mesh.vertices:
        clipped = Vector(tuple(max(minimum[axis], min(maximum[axis], vertex.co[axis])) for axis in range(3)))
        distance = (clipped - vertex.co).length
        if distance > correction_limit:
            raise RuntimeError(f"{lod.name}: Decimate exceeded the bounded household cage repair: {distance:.9f}m")
        if distance > 0:
            vertex.co = clipped
            adjusted += 1
            largest = max(largest, distance)
    # Preserve every existing face/index and reject a collapsed or reversed
    # triangle. In particular, this must not close the spout or handle opening.
    for indices, previous in before:
        a, b, c = (mesh.vertices[index].co for index in indices)
        current = (b - a).cross(c - a)
        if current.length_squared <= 1e-24 or previous.dot(current) <= 0:
            raise RuntimeError(f"{lod.name}: household cage repair collapsed or reversed a triangle")
    mesh.update()
    lod["household_lod_cage_version"] = 1
    lod["household_lod_cage_adjusted_vertices"] = adjusted
    lod["household_lod_cage_max_delta_meters"] = largest
    lod["household_lod_cage_limit_meters"] = correction_limit
    print(f"URMAN household LOD cage: {lod.name}; adjusted={adjusted}; max_delta={largest:.9f}m")


def generate_lod1_variants() -> int:
    """Create deterministic, rebuildable LOD1 meshes for every visible LOD0 mesh."""
    created = 0
    for source in [obj for obj in list(bpy.context.scene.objects) if obj.type == "MESH" and "_LOD0" in obj.name]:
        if source.name.endswith("-col") or source.get("urman_asset_id") is None:
            continue

        asset_id = str(source["urman_asset_id"])
        ratio = 0.35 if asset_id.startswith("env.vegetation") else 0.5
        if asset_id.startswith("prop.oldpc"):
            ratio = 0.65

        lod = source.copy()
        lod.data = source.data.copy()
        lod.name = source.name.replace("_LOD0", "_LOD1")
        lod["urman_asset_id"] = asset_id
        lod["license"] = source.get("license", "Project-original")
        lod["scale_meters"] = source.get("scale_meters", 1.0)
        lod["triangle_budget"] = max(12, round(int(source.get("triangle_budget", 500)) * ratio))
        lod["collision"] = "none"
        lod["lod_status"] = f"LOD1 generated from {source.name}; ratio={ratio:.2f}"
        lod["lod_source"] = source.name
        bpy.context.collection.objects.link(lod)

        bpy.context.view_layer.objects.active = lod
        lod.select_set(True)
        modifier = lod.modifiers.new("URMAN_LOD1_Decimate", "DECIMATE")
        modifier.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        constrain_household_lod(lod, source)
        lod.select_set(False)
        created += 1

    return created


def normalize_export_uvs() -> None:
    """Remove sub-ULP bevel UV drift before writing the source/export pair."""
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        for layer in obj.data.uv_layers:
            for loop in layer.data:
                # Blender stores UVs as float32, so quantize below the noisy
                # bevel boundary rather than assigning a decimal that can
                # round to adjacent float32 values on different runs. Three
                # decimals keep the painterly mapping intact while avoiding
                # half-ULP ties in the glTF exporter.
                loop.uv.x = round(float(loop.uv.x), 3)
                loop.uv.y = round(float(loop.uv.y), 3)


def main() -> None:
    args = arguments()
    root = Path(args.root).resolve()
    blend_path = root / "assets" / "source" / "blender" / "urman_modular_kit.blend"
    glb_path = root / "game" / "assets" / "generated" / "urman_modular_kit.glb"
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    glb_path.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(data):
            if block.users == 0:
                data.remove(block)

    materials = {
        "plaster": material("PaintedPlaster", (0.43, 0.36, 0.28, 1)),
        "roof": material("OldRoof", (0.20, 0.18, 0.16, 1)),
        "wood": material("WeatheredWood", (0.27, 0.20, 0.14, 1)),
        "wood_dark": material("DarkWood", (0.18, 0.12, 0.08, 1)),
        "warm": material("WarmWindow", (0.87, 0.54, 0.24, 1), 0.72),
        "earth": material("DampEarth", (0.30, 0.25, 0.18, 1)),
        "stone": material("MossyStone", (0.31, 0.32, 0.27, 1)),
        "water_dark": material("WellWater", (0.08, 0.16, 0.16, 1), 0.55),
        "fabric": material("OldFabric", (0.30, 0.34, 0.32, 1)),
        "bark": material("PineBark", (0.19, 0.15, 0.12, 1)),
        "foliage": material("PineFoliage", (0.10, 0.18, 0.14, 1)),
        "pc": material("OldPcPlastic", (0.18, 0.20, 0.18, 1)),
        "pc_dark": material("OldPcVent", (0.10, 0.12, 0.11, 1), 0.92),
        "pc_light": material("OldPcKeys", (0.42, 0.42, 0.36, 1)),
        "screen": material("CrtGlass", (0.22, 0.37, 0.32, 1), 0.38),
    }

    create_house(materials)
    create_house_interior(materials)
    create_fence(materials)
    create_yard_props(materials)
    create_boundary_gate(materials)
    create_road(materials)
    create_pine(materials)
    create_furniture_and_pc(materials)
    house_interior_count = sum(
        1
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and obj.name.startswith("HouseInterior_") and "_LOD0" in obj.name
    )
    house_interior_edge_count = sum(
        1
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and obj.name.startswith("HouseInterior_")
        and "_LOD0" in obj.name
        and any(
            token in obj.name
            for token in ("Daybed", "StorageChest", "RightShelf", "ShelfVessel", "RightRunner")
        )
    )
    house_interior_lived_in_count = sum(
        1
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and obj.name.startswith("HouseInterior_")
        and "_LOD0" in obj.name
        and any(
            token in obj.name
            for token in ("Hearth", "LeftWallCupboard", "TableTray", "TableKettle", "TableBowl", "StorageBasket")
        )
    )
    house_interior_hero_count = sum(
        1
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        and obj.name.startswith("HouseInterior_")
        and "_LOD0" in obj.name
        and any(token in obj.name for token in ("OldPc", "Wainscot", "TableFootRail"))
    )
    lod_count = generate_lod1_variants()
    normalize_export_uvs()

    bpy.context.scene["generator"] = "tools/blender/generate_modular_environment.py"
    bpy.context.scene["blender_version_lock"] = "4.5 LTS"
    bpy.context.scene["units"] = "meters"
    bpy.context.scene["lod_policy"] = "LOD1 generated with deterministic Decimate ratios; Godot visibility ranges remain scene-specific"
    bpy.context.scene["detail_policy"] = "authored edge breaks on hard-surface kit; bounded HouseA facade pass; HouseInterior authored 12x10m shell, threshold/window framing, tapered furniture silhouettes, rug, reverse/right lived-in edge cluster, restrained hearth/cupboard/table/storage cluster, PC hutch/document folio and left wainscot joinery; WellA/WoodpileA/GateA village anchors; four-tier faceted PineA crown joined into one mesh; OldPc tower/panel/button/drive-slot/label-plate hero details"
    bpy.context.scene["lod1_mesh_count"] = lod_count
    bpy.context.scene["house_interior_lod0_count"] = house_interior_count
    bpy.context.scene["house_interior_edge_lod0_count"] = house_interior_edge_count
    bpy.context.scene["house_interior_lived_in_lod0_count"] = house_interior_lived_in_count
    bpy.context.scene["house_interior_hero_lod0_count"] = house_interior_hero_count
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0

    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
    )
    print(f"URMAN modular kit: {blend_path}")
    print(f"URMAN modular kit: {glb_path}")
    print(f"URMAN LOD1 variants: {lod_count}")


if __name__ == "__main__":
    main()
