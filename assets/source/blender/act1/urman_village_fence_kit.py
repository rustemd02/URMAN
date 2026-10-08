"""Deterministic village fence and gate kit for the URMAN visual reset (VIS-088, VIS-089).

The card's problem statement is short: a random colour is not a construction. This
generator authors *families of carpentry* — rough picket, painted palisadnik,
repaired mixed board, wattle (плетень), simple rail — plus modular wickets and a
cart gate, each with real board thickness, real bearing rails, real posts and a
bounded, editable amount of lean and damage.

The numbers are the same contract the runtime builder uses in
``game/scripts/Act1ConnectedWorld.YardFences.cs``:

* board thickness 20-40 mm equivalent (nothing thinner pretends to be timber);
* post lean typically <= 4 degrees, one settled post per repaired run up to 8;
* at least 45 mm of daylight between two boards, so a run never reads as a sheet;
* no continuous horizontal member crosses the readable 0.70 m band of the fence,
  which is where the eye counts the separate boards;
* every support foot authored on the kit's own ground plane (local z = 0), so the
  runtime seats a post by its own footprint instead of by a guessed centre;
* an entrance passage that clears the player capsule plus 0.15 m (0.85 m minimum).

Run with Blender 4.5+ to produce geometry:
  blender --background --python assets/source/blender/act1/urman_village_fence_kit.py -- --root <repo>

Check the contract without Blender (no geometry, no writes):
  python3 assets/source/blender/act1/urman_village_fence_kit.py -- --check
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

try:  # Blender is only needed to write geometry; the contract is checked anywhere.
    import bpy
except ImportError:  # pragma: no cover - exercised by --check
    bpy = None


KIT_ROOT = "URMAN_VillageFenceKit"
GLB_NAME = "urman_village_fence_kit.glb"
MANIFEST_NAME = "urman_village_fence_kit.manifest.json"
GEOMETRY_PASS = "constructive village fence families v1; real thickness, bounded lean, readable joinery"
READABLE_BAND_M = 0.70

CONTRACT = {
    "board_thickness_min_m": 0.020,
    "board_thickness_max_m": 0.040,
    "lean_typical_max_deg": 4.0,
    "lean_rare_max_deg": 8.0,
    "board_daylight_min_m": 0.045,
    "wicket_clear_width_min_m": 0.85,
    "player_capsule_diameter_m": 0.70,
    "passenger_margin_m": 0.15,
    "support_foot_local_z_m": 0.0,
    "variation_height_scale_max": 1.03,
    "variation_lean_scale_max": 1.25,
}

WOOD_WEATHERED = "URMAN_Wood_Weathered"
WOOD_DARK = "URMAN_Wood_Dark"
WOOD_FRESH = "URMAN_Wood_CutEnd"
METAL_DULLED = "URMAN_Metal_Dulled"
STONE_MOSSY = "URMAN_Stone_Mossy"
STONE_FACE = "URMAN_Stone_MossFace"

# One entry per family. Every value is a carpentry measurement the author can edit;
# within a family all boards are the same piece and only the authored crown profile
# varies by a fixed three-step pattern, so the kit cannot decay into per-board noise.
FAMILIES = (
    {
        "id": "rough-picket", "style": 0, "segment_length": 2.35, "height": 1.18,
        "board_thickness": 0.028, "board_width": 0.095, "board_spacing": 0.17,
        "post_section": 0.12, "rail_thickness": 0.045, "rail_depth": 0.075,
        "lower_rail": 0.27, "upper_rail_drop": 0.23, "lean_deg": 1.2, "settled_lean_deg": 0.0,
        "crown": "pointed", "cap_rail": False, "weave": False, "uprights": False, "mixed": False,
        "courses": (0.27, 0.95), "material": WOOD_WEATHERED,
    },
    {
        "id": "painted-palisadnik", "style": 1, "segment_length": 2.10, "height": 1.10,
        "board_thickness": 0.026, "board_width": 0.095, "board_spacing": 0.17,
        "post_section": 0.11, "rail_thickness": 0.040, "rail_depth": 0.070,
        "lower_rail": 0.26, "upper_rail_drop": 0.21, "lean_deg": 0.8, "settled_lean_deg": 0.0,
        "crown": "scalloped", "cap_rail": True, "weave": False, "uprights": False, "mixed": False,
        "courses": (0.26, 0.89), "material": WOOD_FRESH,
    },
    {
        "id": "repaired-mixed-board", "style": 2, "segment_length": 2.45, "height": 1.24,
        "board_thickness": 0.034, "board_width": 0.115, "board_spacing": 0.165,
        "post_section": 0.13, "rail_thickness": 0.045, "rail_depth": 0.080,
        "lower_rail": 0.28, "upper_rail_drop": 0.24, "lean_deg": 2.4, "settled_lean_deg": 8.0,
        "crown": "hand-cut", "cap_rail": False, "weave": False, "uprights": False, "mixed": True,
        "courses": (0.28, 1.00), "material": WOOD_WEATHERED,
    },
    {
        "id": "wattle", "style": 3, "segment_length": 2.25, "height": 1.20,
        "board_thickness": 0.030, "board_width": 0.075, "board_spacing": 0.16,
        "post_section": 0.12, "rail_thickness": 0.032, "rail_depth": 0.050,
        "lower_rail": 0.30, "upper_rail_drop": 0.10, "lean_deg": 1.6, "settled_lean_deg": 0.0,
        "crown": "level", "cap_rail": False, "weave": True, "uprights": False, "mixed": False,
        # A woven course is continuous along the run, so none of them may fall in the
        # readable band: the daylight there belongs to the upright stakes alone.
        "courses": (0.30, 0.48, 0.92, 1.10), "material": WOOD_WEATHERED,
    },
    {
        "id": "simple-rail", "style": 4, "segment_length": 2.60, "height": 1.30,
        "board_thickness": 0.024, "board_width": 0.070, "board_spacing": 0.26,
        "post_section": 0.14, "rail_thickness": 0.060, "rail_depth": 0.140,
        "lower_rail": 0.42, "upper_rail_drop": 0.10, "lean_deg": 3.2, "settled_lean_deg": 0.0,
        "crown": "level", "cap_rail": True, "weave": False, "uprights": True, "mixed": False,
        "courses": (0.42, 0.84, 1.20), "material": WOOD_DARK,
    },
)

# Modular entrances. A leaf is authored closed across its opening with the hinge line
# at its local x = 0, so turning the hinge node about its own vertical axis is a real
# swing; the runtime shows the same leaf in the open pose (0 deg authored, -90 deg
# closed) because an entrance has to read as an entrance.
ENTRANCES = (
    {"id": "board-wicket", "family": 0, "type": "wicket", "jamb_section": 0.14, "jamb_spacing": 0.62,
     "leaf_boards": 6, "height": 1.22},
    {"id": "repaired-board-wicket", "family": 2, "type": "wicket", "jamb_section": 0.14, "jamb_spacing": 0.62,
     "leaf_boards": 6, "height": 1.28},
    {"id": "wattle-wicket", "family": 3, "type": "wicket", "jamb_section": 0.14, "jamb_spacing": 0.66,
     "leaf_boards": 7, "height": 1.24},
    {"id": "cart-gate", "family": 1, "type": "gate", "jamb_section": 0.17, "jamb_spacing": 1.72,
     "leaf_boards": 9, "height": 2.40},
)


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument("--check", action="store_true",
                        help="validate the contract and exit (no Blender, no writes)")
    parser.add_argument("--report-json", action="store_true", help="print the contract as JSON and exit")
    tokens: list[str] = []
    if "--" in sys.argv:
        tokens = sys.argv[sys.argv.index("--") + 1:]
    return parser.parse_args(tokens)


def variation(seed: int) -> dict:
    """Bounded authored variation between *segments*, never between single boards: a
    few centimetres of height and one extra degree of settle on one post."""
    return {
        "height_scale": 1.0 + ((seed % 5) - 2) * 0.012,
        "lean_scale": 1.25 if seed % 7 == 0 else 1.0,
        "damaged_bay": seed % 4,
    }


def check_contract() -> list[str]:
    """The card's numbers as an executable rule, so a future edit that turns the kit
    back into slop fails here instead of arriving on screen."""
    faults: list[str] = []
    for family in FAMILIES:
        thickness = family["board_thickness"]
        if not CONTRACT["board_thickness_min_m"] <= thickness <= CONTRACT["board_thickness_max_m"]:
            faults.append(f"{family['id']}: board thickness {thickness * 1000:.0f} mm outside 20-40 mm")
        if family["lean_deg"] * CONTRACT["variation_lean_scale_max"] > CONTRACT["lean_typical_max_deg"]:
            faults.append(f"{family['id']}: typical lean {family['lean_deg']} deg cannot stay under 4 deg "
                          "once the authored segment variation is applied")
        if family["settled_lean_deg"] > CONTRACT["lean_rare_max_deg"]:
            faults.append(f"{family['id']}: settled lean {family['settled_lean_deg']} deg exceeds the rare 8 deg")
        daylight = family["board_spacing"] - family["board_width"]
        if daylight < CONTRACT["board_daylight_min_m"]:
            faults.append(f"{family['id']}: only {daylight * 1000:.0f} mm of daylight between boards")
        for course in family["courses"]:
            if abs(course - READABLE_BAND_M) < family["rail_depth"] * 0.5 + 0.01:
                faults.append(f"{family['id']}: course {course} m crosses the readable "
                              f"{READABLE_BAND_M} m band and merges the run into one sheet")
        if family["height"] - family["upper_rail_drop"] <= family["lower_rail"]:
            faults.append(f"{family['id']}: upper rail at or below the lower rail")
        for seed in range(4):
            scale = variation(seed + family["style"])["height_scale"]
            if scale > CONTRACT["variation_height_scale_max"]:
                faults.append(f"{family['id']}: segment height variation {scale} above the authored cap")
    for entrance in ENTRANCES:
        clear = (entrance["jamb_spacing"] - entrance["jamb_section"] * 0.5) * 2.0
        minimum = CONTRACT["player_capsule_diameter_m"] + CONTRACT["passenger_margin_m"]
        if clear < minimum:
            faults.append(f"{entrance['id']}: clear passage {clear:.2f} m under capsule + 0.15 m ({minimum} m)")
    styles = [family["style"] for family in FAMILIES]
    if len(set(styles)) != len(styles):
        faults.append("two families share a timberStyle value; neighbours would be indistinguishable")
    return faults


def board_crown(family: dict, t: float, index: int, height: float) -> float:
    crown = height
    if family["crown"] == "scalloped":
        crown -= 0.16 * math.sin(t * math.pi)
    elif family["crown"] == "hand-cut":
        crown -= ((index * 7 + family["style"]) % 3) * 0.025
    elif family["crown"] == "level":
        crown -= 0.04
    if family["uprights"]:
        crown = height - 0.34
    return crown


def board_base(family: dict) -> float:
    if family["uprights"]:
        return family["lower_rail"] - 0.06
    if family["weave"]:
        return -0.04  # an upright stake is driven into the ground, not hung on a rail
    return family["lower_rail"] + family["rail_depth"] * 0.5 + 0.01


if bpy is not None:

    def material(name: str) -> bpy.types.Material:
        target = bpy.data.materials.get(name)
        if target is None:
            target = bpy.data.materials.new(name)
            target["source_slot"] = name
            target["runtime_material_owner"] = {
                WOOD_WEATHERED: "wood_fence",
                WOOD_DARK: "wood_fence",
                WOOD_FRESH: "wood_fence / repaired board",
                METAL_DULLED: "metal",
                STONE_MOSSY: "stone_foundation",
                STONE_FACE: "stone_foundation",
            }.get(name, "wood_fence")
        return target

    def box_object(
        name: str,
        parent: bpy.types.Object,
        centre: tuple[float, float, float],
        size: tuple[float, float, float],
        materials: tuple[str, ...],
        role: str,
        component_root: str,
        rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
        chamfer: float = 0.012,
    ) -> bpy.types.Object:
        """A member with real thickness and a chamfered arris. Along the fence line is
        local X, across it is local Y, up is local Z."""
        sx, sy, sz = (value / 2.0 for value in size)
        c = min(chamfer, sx * 0.45, sy * 0.45, sz * 0.45)
        ring = [
            (-sx + c, -sy), (sx - c, -sy), (sx, -sy + c), (sx, sy - c),
            (sx - c, sy), (-sx + c, sy), (-sx, sy - c), (-sx, -sy + c),
        ]
        vertices = [(x, y, -sz) for x, y in ring] + [(x, y, sz) for x, y in ring]
        faces = [tuple(reversed(range(8))), tuple(range(8, 16))]
        faces.extend((i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8))
        mesh = bpy.data.meshes.new(f"{name}Mesh")
        mesh.from_pydata(vertices, [], faces)
        mesh.validate(verbose=False)
        mesh.update()
        mesh.materials.clear()
        for material_name in materials:
            mesh.materials.append(material(material_name))
        for polygon in mesh.polygons:
            polygon.material_index = 0
        mesh.calc_loop_triangles()
        # Metric UV: one metre of texture per metre of timber, chosen per face so the
        # grain follows the board instead of being stretched over the whole run.
        uv_layer = mesh.uv_layers.new(name="UVMap")
        for loop in mesh.loops:
            point = mesh.vertices[loop.vertex_index].co
            normal = mesh.polygons[loop.polygon_index].normal
            if abs(normal.z) > 0.5:
                uv_layer.data[loop.index].uv = (point.x, point.y)
            elif abs(normal.x) > 0.5:
                uv_layer.data[loop.index].uv = (point.y, point.z)
            else:
                uv_layer.data[loop.index].uv = (point.x, point.z)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.parent = parent
        obj.location = centre
        obj.rotation_mode = "XYZ"
        obj.rotation_euler = rotation
        obj["urman_asset_id"] = f"urman.act1.fence.{component_root.lower()}"
        obj["component_root"] = component_root
        obj["license"] = "Project-original"
        obj["scale_meters"] = 1.0
        obj["collision"] = "none; the runtime seats the member by its own footprint"
        obj["presentation_only"] = True
        obj["lod_status"] = "LOD0"
        obj["asset_role"] = role
        obj["geometry_pass"] = GEOMETRY_PASS
        obj["member_size_m"] = [round(value, 4) for value in size]
        return obj

    def empty_object(name: str, parent: bpy.types.Object, location, role: str,
                     component_root: str) -> bpy.types.Object:
        obj = bpy.data.objects.get(name)
        if obj is None:
            obj = bpy.data.objects.new(name, None)
            bpy.context.collection.objects.link(obj)
        obj.parent = parent
        obj.location = location
        obj.rotation_mode = "XYZ"
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj["urman_asset_id"] = f"urman.act1.fence.{component_root.lower()}"
        obj["component_root"] = component_root if parent.name == KIT_ROOT else parent["component_root"]
        obj["license"] = "Project-original"
        obj["scale_meters"] = 1.0
        obj["asset_role"] = role
        obj["geometry_pass"] = GEOMETRY_PASS
        return obj

    def leaning_post(name: str, parent: bpy.types.Object, foot_x: float, rise: float, section: float,
                    lean_deg: float, materials: tuple[str, ...], component_root: str):
        """The post stands on its own foot at local z = 0 and leans along the fence
        line. Returns its object and the point its crown rails hang from."""
        lean = math.radians(lean_deg)
        length = rise / math.cos(lean)
        centre = (foot_x, math.sin(lean) * rise * 0.5, math.cos(lean) * rise * 0.5)
        obj = box_object(name, parent, centre, (section, section, length), materials,
                         "fence post with authored lean", component_root,
                         rotation=(0.0, lean, 0.0), chamfer=min(0.02, section * 0.2))
        obj["post_lean_deg"] = round(lean_deg, 3)
        obj["post_foot_local_z_m"] = 0.0
        return obj, (math.sin(lean) * rise, math.cos(lean) * rise)

    def author_family(parent: bpy.types.Object, family: dict, layout_x: float) -> None:
        root_name = f"FenceFamily_{family['id'].replace('-', '_').capitalize()}"
        root = empty_object(root_name, parent, (layout_x, 0.0, 0.0), "village fence family segment", root_name)
        tune = variation(family["style"])
        height = family["height"] * tune["height_scale"]
        length = family["segment_length"]
        boards_material = (family["material"],)
        for index in range(2):
            lean = family["lean_deg"] * (tune["lean_scale"] if index else 1.0)
            leaning_post(f"{root_name}_Post_{index:02d}_LOD0", root, index * length, height + 0.11,
                         family["post_section"], min(lean, CONTRACT["lean_typical_max_deg"]),
                         (WOOD_DARK,), root_name)
        # Rail courses follow the posts: a leaning crown drags its rail with it, which
        # is what makes a settled run read as settled instead of as broken geometry.
        lean = math.radians(family["lean_deg"])
        for course in family["courses"]:
            far_y = math.sin(lean) * course
            far_z = math.cos(lean) * course
            span = math.hypot(length, far_z - course)
            pitch = math.atan2(far_z - course, length)
            box_object(f"{root_name}_Rail_{int(course * 1000):04d}_LOD0", root,
                       (length * 0.5, far_y * 0.5, (course + far_z) * 0.5),
                       (span, family["rail_thickness"], family["rail_depth"]), boards_material,
                       "woven withy course" if family["weave"] else "bearing rail", root_name,
                       rotation=(0.0, -pitch, 0.0), chamfer=0.008)
        count = max(1, math.floor(length / family["board_spacing"]))
        for index in range(count):
            t = (index + 0.5) / count
            narrow = family["mixed"] and index % 2 == 1
            width = family["board_width"] * (0.65 if narrow else 1.0)
            crown = board_crown(family, t, index, height)
            base = board_base(family)
            across = 0.03 * (1 if index % 2 == 0 else -1) if family["weave"] else 0.0
            box_object(f"{root_name}_Board_{index:02d}_LOD0", root, (t * length, across, (crown + base) * 0.5),
                       (width, family["board_thickness"], crown - base), boards_material,
                       "woven upright stake" if family["weave"] else "fence board", root_name,
                       chamfer=min(0.008, family["board_thickness"] * 0.3))
            if family["crown"] == "pointed" and not narrow:
                box_object(f"{root_name}_PicketPoint_{index:02d}_LOD0", root,
                           (t * length, across, crown + 0.03),
                           (width * 0.9, family["board_thickness"] * 0.9, 0.06), boards_material,
                           "sharpened picket crown", root_name,
                           rotation=(math.radians(45), 0.0, 0.0), chamfer=0.004)
        if family["cap_rail"]:
            box_object(f"{root_name}_CapRail_LOD0", root, (length * 0.5, 0.0, height + 0.03),
                       (length, 0.055, 0.06), (WOOD_FRESH,), "top cap rail", root_name, chamfer=0.01)
        if family["mixed"]:
            # The biography of this family is a repair: four fresher boards and the
            # patch rail nailed across them on the yard face.
            for index in range(4):
                x = 0.34 + index * family["board_spacing"]
                box_object(f"{root_name}_RepairedBoard_{index:02d}_LOD0", root,
                           (x, 0.0, height * 0.52), (family["board_width"], family["board_thickness"] + 0.002,
                                                     height * 0.86), (WOOD_FRESH,), "repaired fresher board",
                           root_name, chamfer=0.008)
            box_object(f"{root_name}_PatchRail_LOD0", root,
                       (0.58, family["board_thickness"] + 0.033, height * 0.55),
                       (0.62, 0.030, 0.09), (WOOD_FRESH,), "patch rail nailed across the replaced boards",
                       root_name, chamfer=0.006)
        root["family_id"] = family["id"]
        root["timber_style"] = family["style"]
        root["board_thickness_m"] = family["board_thickness"]
        root["board_daylight_m"] = round(family["board_spacing"] - family["board_width"], 4)
        root["segment_height_m"] = round(height, 4)
        root["post_lean_deg"] = round(family["lean_deg"], 3)

    def author_entrance(parent: bpy.types.Object, entrance: dict, layout_x: float) -> None:
        family = FAMILIES[entrance["family"]]
        root_name = f"Gate_{entrance['id'].replace('-', '_').capitalize()}"
        root = empty_object(root_name, parent, (layout_x, 0.0, 0.0), "modular village entrance", root_name)
        jamb_half = entrance["jamb_section"] * 0.5
        clear = (entrance["jamb_spacing"] - jamb_half) * 2.0
        height = entrance["height"]
        for index, sign in enumerate((-1.0, 1.0)):
            leaning_post(f"{root_name}_Jamb_{index}_LOD0", root, sign * entrance["jamb_spacing"], height,
                         entrance["jamb_section"], 0.0, (WOOD_DARK,), root_name)
            box_object(f"{root_name}_JambCap_{index}_LOD0", root,
                       (sign * entrance["jamb_spacing"], 0.0, height + 0.03),
                       (entrance["jamb_section"] + 0.04, entrance["jamb_section"] + 0.04, 0.05),
                       (STONE_FACE,), "jamb cap", root_name, chamfer=0.01)
        box_object(f"{root_name}_Lintel_LOD0", root, (0.0, 0.0, height + 0.12),
                   (entrance["jamb_spacing"] * 2.0 + entrance["jamb_section"], 0.12, 0.14),
                   (WOOD_WEATHERED,), "gate lintel", root_name, chamfer=0.012)
        box_object(f"{root_name}_Sill_LOD0", root, (0.0, 0.0, 0.025), (0.30, clear + 0.10, 0.05),
                   (STONE_MOSSY, STONE_FACE), "worn entrance sill: the doorway has a floor", root_name,
                   chamfer=0.014)
        # A cart gate is two leaves; a wicket is one. Each leaf is authored in its own
        # hinge frame with the hinge line at local x = 0 and the leaf running along
        # local +x, so turning that hinge about its own vertical axis is a real swing.
        leaves = 2 if entrance["type"] == "gate" else 1
        leaf_width = clear / leaves - 0.02
        for leaf_index, sign in enumerate((-1.0, 1.0)[:leaves]):
            hinge = empty_object(f"{root_name}_Hinge_{leaf_index}", root,
                                 (sign * (entrance["jamb_spacing"] - jamb_half), 0.0, 0.0),
                                 "leaf hinge: rotation about local Z is a real swing", root_name)
            if sign > 0.0:
                hinge.rotation_euler = (0.0, 0.0, math.radians(180.0))
            hinge["hinge_axis"] = "local_z"
            hinge["closed_degrees"] = 0.0
            hinge["open_degrees"] = 90.0
            hinge["runtime_authored_pose"] = "open (the fence builder shows the leaf swung into the yard)"
            hinge["leaf_index"] = leaf_index
            leaf_height = height - 0.06
            for course in (0.30, leaf_height - 0.26):
                box_object(f"{root_name}_LeafRail{leaf_index}_{int(course * 1000):04d}_LOD0", hinge,
                           (leaf_width * 0.5 + 0.01, 0.0, course),
                           (leaf_width, family["rail_thickness"], 0.07), (WOOD_WEATHERED,), "leaf bearing rail",
                           root_name, chamfer=0.008)
            boards = max(2, entrance["leaf_boards"] // leaves + 1)
            for index in range(boards):
                x = 0.02 + (index + 0.5) * (leaf_width - 0.04) / boards
                crown = leaf_height - ((index * 5 + family["style"]) % 3) * 0.02
                box_object(f"{root_name}_LeafBoard{leaf_index}_{index:02d}_LOD0", hinge,
                           (x, 0.0, 0.05 + (crown - 0.10) * 0.5),
                           (family["board_width"], family["board_thickness"], crown - 0.10), (WOOD_WEATHERED,),
                           "leaf board", root_name, chamfer=0.006)
            rise = leaf_height - 0.56
            brace = math.hypot(leaf_width, rise)
            pitch = math.atan2(rise, leaf_width)
            box_object(f"{root_name}_LeafBrace{leaf_index}_LOD0", hinge,
                       (leaf_width * 0.5 + 0.01, family["rail_thickness"] * 0.5 + 0.016, 0.30 + rise * 0.5),
                       (brace, 0.028, 0.075), (WOOD_DARK,), "diagonal leaf brace: keeps a narrow leaf square",
                       root_name, rotation=(0.0, -pitch, 0.0), chamfer=0.008)
            for course in (0.34, leaf_height - 0.34):
                box_object(f"{root_name}_HingeStrap{leaf_index}_{int(course * 1000):04d}_LOD0", hinge,
                           (0.15, family["rail_thickness"] * 0.5 + 0.02, course), (0.30, 0.018, 0.06),
                           (METAL_DULLED,), "hinge strap clamped over the jamb", root_name, chamfer=0.004)
                box_object(f"{root_name}_HingePintle{leaf_index}_{int(course * 1000):04d}_LOD0", hinge,
                           (0.022, family["rail_thickness"] * 0.5 + 0.10, course), (0.055, 0.19, 0.055),
                           (METAL_DULLED,), "hinge pintle", root_name, chamfer=0.006)
            box_object(f"{root_name}_LatchPlate{leaf_index}_LOD0", hinge,
                       (leaf_width - 0.04, family["rail_thickness"] * 0.5 + 0.026, leaf_height - 0.55),
                       (0.10, 0.055, 0.05), (METAL_DULLED,), "latch plate on the closing edge", root_name,
                       chamfer=0.005)
            box_object(f"{root_name}_Handle{leaf_index}_LOD0", hinge,
                       (leaf_width - 0.06, -(family["board_thickness"] * 0.5 + 0.045), 1.05),
                       (0.05, 0.05, 0.18), (METAL_DULLED,), "handle bar at standing hand height", root_name,
                       chamfer=0.006)
            hinge["leaf_width_m"] = round(leaf_width, 3)
        root["entrance_type"] = entrance["type"]
        root["leaves"] = leaves
        root["clear_passage_m"] = round(clear, 3)
        root["leaf_width_m"] = round(leaf_width, 3)
        root["family_id"] = family["id"]

    def author_kit(root: bpy.types.Object) -> None:
        x = 0.0
        for family in FAMILIES:
            author_family(root, family, x)
            x += family["segment_length"] + 1.6
        x += 1.2
        for entrance in ENTRANCES:
            author_entrance(root, entrance, x)
            x += entrance["jamb_spacing"] * 2.0 + 2.4

    def validate(root: bpy.types.Object) -> dict:
        stats = {"families": 0, "entrances": 0, "meshes": 0, "triangles": 0,
                 "thin_boards": 0, "floating_posts": 0, "sheet_runs": 0}
        for child in root.children:
            if child.get("entrance_type"):
                stats["entrances"] += 1
            elif child.get("family_id"):
                stats["families"] += 1
        for obj in root.children_recursive:
            if obj.type != "MESH":
                continue
            stats["meshes"] += 1
            obj.data.calc_loop_triangles()
            stats["triangles"] += len(obj.data.loop_triangles)
            size = obj.get("member_size_m")
            name = obj.name
            if size and ("Board" in name or "Picket" in name or "Withy" in name):
                thickness = min(size[0], size[1])
                if not CONTRACT["board_thickness_min_m"] <= thickness <= CONTRACT["board_thickness_max_m"]:
                    stats["thin_boards"] += 1
            if size and ("Rail" in name or "Cap" in name) and "Leaf" not in name:
                # A continuous member may not sit in the readable band: that is the
                # difference between a fence and a hoarding.
                centre_z = obj.location.z
                if abs(centre_z - READABLE_BAND_M) < size[2] * 0.5 + 0.01:
                    stats["sheet_runs"] += 1
            if "_Post_" in name or "_Jamb_" in name:
                # A support must stand on the kit's own ground plane, so the runtime
                # can seat it by its footprint (VIS-009, VIS-088 step 3).
                lowest = min((obj.matrix_world @ co).z for co in obj.data.vertices)
                if abs(lowest) > 0.02:
                    stats["floating_posts"] += 1
        if stats["families"] != len(FAMILIES) or stats["entrances"] != len(ENTRANCES):
            raise RuntimeError(f"Fence kit lost a family or an entrance: {stats}")
        if stats["thin_boards"] or stats["floating_posts"] or stats["sheet_runs"]:
            raise RuntimeError(f"Fence kit contract failed: {stats}")
        return stats

    def save_kit(root_path: Path, blend_path: Path, glb_path: Path, root: bpy.types.Object,
                 stats: dict) -> None:
        scene = bpy.context.scene
        scene["generator"] = "assets/source/blender/act1/urman_village_fence_kit.py"
        scene["fence_geometry_pass"] = GEOMETRY_PASS
        scene["fence_contract"] = json.dumps(CONTRACT, sort_keys=True)
        blend_path.parent.mkdir(parents=True, exist_ok=True)
        glb_path.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

        def under_kit(obj: bpy.types.Object) -> bool:
            parent = obj.parent
            while parent is not None:
                if parent is root:
                    return True
                parent = parent.parent
            return obj is root

        for obj in bpy.data.objects:
            obj.select_set(under_kit(obj))
        bpy.ops.export_scene.gltf(filepath=str(glb_path), use_selection=True, export_apply=True,
                                  export_yup=True)
        digest = hashlib.sha256(glb_path.read_bytes()).hexdigest()
        manifest = {
            "generator": "assets/source/blender/act1/urman_village_fence_kit.py",
            "geometry_pass": GEOMETRY_PASS,
            "contract": CONTRACT,
            "families": [{key: value for key, value in family.items() if key != "material"}
                         for family in FAMILIES],
            "entrances": [{key: value for key, value in entrance.items()} for entrance in ENTRANCES],
            "stats": stats,
            "glb": str(glb_path.relative_to(root_path)).replace("\\", "/"),
            "glb_sha256": digest,
        }
        (glb_path.parent / MANIFEST_NAME).write_text(
            json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"urman-fence-kit: families={stats['families']} entrances={stats['entrances']} "
              f"meshes={stats['meshes']} triangles={stats['triangles']} glb={glb_path.name} sha256={digest[:16]}")


def main() -> None:
    args = arguments()
    faults = check_contract()
    if faults:
        for fault in faults:
            print(f"FENCE CONTRACT FAULT: {fault}", file=sys.stderr)
        raise SystemExit(1)
    if args.report_json:
        print(json.dumps({"contract": CONTRACT,
                          "families": [{key: value for key, value in family.items() if key != "material"}
                                       for family in FAMILIES],
                          "entrances": list(ENTRANCES)}, indent=2, ensure_ascii=False))
        return
    if args.check:
        print(f"urman-fence-kit contract OK: {len(FAMILIES)} families, {len(ENTRANCES)} entrances")
        return
    if bpy is None:
        raise SystemExit("Blender is required to write geometry; use --check or --report-json without it")
    root_path = Path(args.root).resolve()
    blend_path = root_path / "assets/source/blender/act1/urman_village_fence_kit.blend"
    glb_path = root_path / "game/assets/models/act1" / GLB_NAME
    if blend_path.exists():
        bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    root = bpy.data.objects.get(KIT_ROOT)
    if root is None:
        root = bpy.data.objects.new(KIT_ROOT, None)
        bpy.context.collection.objects.link(root)
    root["urman_asset_id"] = "urman.act1.village.fence_kit"
    root["license"] = "Project-original"
    root["scale_meters"] = 1.0
    root["component_root"] = KIT_ROOT
    root["geometry_pass"] = GEOMETRY_PASS
    author_kit(root)
    bpy.context.view_layer.update()
    stats = validate(root)
    save_kit(root_path, blend_path, glb_path, root, stats)


if __name__ == "__main__":
    main()
