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
    cube(
        "OldPc_Keyboard_LOD0",
        (1.15, 0.48, 0.1),
        (-9, 7.3, 0.96),
        materials["pc_light"],
        "prop.oldpc.keyboard",
        5000,
        bevel_width=0.045,
        bevel_segments=1,
    )
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
        lod.select_set(False)
        created += 1

    return created


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
    create_fence(materials)
    create_yard_props(materials)
    create_boundary_gate(materials)
    create_road(materials)
    create_pine(materials)
    create_furniture_and_pc(materials)
    lod_count = generate_lod1_variants()

    bpy.context.scene["generator"] = "tools/blender/generate_modular_environment.py"
    bpy.context.scene["blender_version_lock"] = "4.5 LTS"
    bpy.context.scene["units"] = "meters"
    bpy.context.scene["lod_policy"] = "LOD1 generated with deterministic Decimate ratios; Godot visibility ranges remain scene-specific"
    bpy.context.scene["detail_policy"] = "authored edge breaks on hard-surface kit; bounded HouseA facade pass; WellA/WoodpileA/GateA village anchors; four-tier faceted PineA crown joined into one mesh; 49-mesh contract with OldPc tower/panel/button/drive-slot/label-plate hero details"
    bpy.context.scene["lod1_mesh_count"] = lod_count
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
