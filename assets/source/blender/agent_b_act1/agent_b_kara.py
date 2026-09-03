"""Agent B Act I Kit 5: Kara-Urman night forest edge.

Authored forest masses, edge trunks, root banks, the clearing for the
cliffhanger endpoint and a single almost-human branch gesture. Kara-Urman is
part of the old order: dark and pressuring, but not a generic evil-forest
set piece. No creature reveals, no dungeon entrance, no void.
"""

from __future__ import annotations

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import agent_b_common as ab  # noqa: E402

# Godot space: path runs x~0..1 from z=-89.5 to z=-122.5; forest fills both
# flanks from z ~ -90 to -150.


def forest_mass(name, cx_g, cz_g, rx, rz, height, squish, material, seed):
    blob = ab.make_icosphere(name, (cx_g, -cz_g, height * 0.6), 1.0,
                             subdivisions=2)
    blob.scale = (rx, rz, height)
    ab.displace_object(blob, min(rx, rz) * 0.22, 0.55, seed_offset=seed)
    ab.assign_material(blob, material)
    for polygon in blob.data.polygons:
        polygon.use_smooth = False
    return blob


def edge_trunk(name, cx_g, cz_g, height, radius, lean, material):
    trunk = ab.make_cylinder(name, (cx_g, -cz_g, 0), radius, radius * 0.55,
                             height, segments=7)
    ab.rotate_around(trunk, lean, "x", (0.0, 0.0, 0.0))
    ab.assign_material(trunk, material)
    canopy = ab.make_icosphere(f"{name}_Crown",
                               (cx_g + lean * height * 0.3, -cz_g,
                                height - 0.4), radius * 6.0, subdivisions=2)
    canopy.scale = (1.0, 1.0, 0.62)
    ab.displace_object(canopy, radius * 1.6, 0.7,
                       seed_offset=ab.stable_hash(name) * 0.01)
    ab.assign_material(canopy, "AB_foliage_kara")
    return [trunk, canopy]


def root_bank(name, cx_g, cz_g, yaw_deg, width, height):
    """Gnarled root bank hugging the path edge: flattened stretched blob.
    The placement travels on the object location (GLB node translation).
    Never push (cx, -cz) into vertices while object scale is set: the
    scale multiplies the translation and teleports the bank (observed:
    KaraRoot_0 authored at z=-98 landing at z=-68.6)."""
    blob = ab.make_icosphere(name, (cx_g, -cz_g, height * 0.3), 1.0,
                             subdivisions=2)
    blob.scale = (width / 2, 0.7, height)
    ab.displace_object(blob, 0.35, 0.9, seed_offset=ab.stable_hash(name) * 0.01)
    # Yaw about the LOCAL origin: vertices live around the origin, placement
    # travels on location.
    ab.rotate_around(blob, math.radians(yaw_deg), "z", (0.0, 0.0, 0.0))
    ab.assign_material(blob, "AB_bark_dark")
    for polygon in blob.data.polygons:
        polygon.use_smooth = False
    return blob


def gesture_branch(name, base_x, base_z):
    """Single almost-human branch gesture leaning over the path, one arm
    bent like a beckoning wrist. Reads only as a silhouette against fog."""
    objects = []
    stem = ab.make_cylinder(f"{name}_Stem", (base_x + 1.9, -base_z, 0),
                            0.11, 0.045, 3.4, segments=6)
    ab.rotate_around(stem, -0.42, "y", (0.0, 0.0, 0.0))
    ab.assign_material(stem, "AB_bark_dark")
    objects.append(stem)
    arm = ab.make_cylinder(f"{name}_Arm", (base_x + 1.05, -base_z, 2.9),
                           0.045, 0.02, 1.5, segments=6)
    ab.rotate_around(arm, 0.9, "y", (0.0, 0.0, 0.0))
    ab.rotate_around(arm, -0.25, "z", (0.0, 0.0, 0.0))
    ab.assign_material(arm, "AB_bark_dark")
    objects.append(arm)
    wrist = ab.make_cylinder(f"{name}_Wrist", (base_x + 0.35, -base_z, 3.35),
                             0.02, 0.008, 0.8, segments=5)
    ab.rotate_around(wrist, 1.35, "y", (0.0, 0.0, 0.0))
    ab.assign_material(wrist, "AB_bark_dark")
    objects.append(wrist)
    return objects


def fallen_log(name, cx_g, cz_g, yaw_deg, length):
    log = ab.make_cylinder(name, (0, 0, 0.16), 0.14, 0.1, length,
                           segments=7, axis="x")
    ab.rotate_around(log, math.radians(yaw_deg), "z", (0, 0, 0))
    ab.translate_vertices(log, cx_g, -cz_g, 0)
    ab.assign_material(log, "AB_bark_dark")
    return log


def main() -> None:
    ab.setup_scene()
    objects: list = []

    # ---- Forest masses: near flanks ------------------------------------------
    mass_left = [
        (-8.5, -94, 6.5, 5.0, 6.0),
        (-14.0, -101, 8.0, 6.5, 7.5),
        (-7.5, -108, 6.0, 5.5, 6.5),
        (-13.0, -115, 9.0, 7.0, 8.5),
        (-8.0, -122, 7.0, 6.0, 7.0),
        (-15.0, -128, 9.5, 8.0, 9.0),
    ]
    mass_right = [
        (8.5, -95, 6.5, 5.0, 6.5),
        (13.5, -102, 8.0, 6.5, 7.5),
        (7.5, -109, 6.0, 5.5, 6.0),
        (14.0, -116, 9.0, 7.0, 8.0),
        (8.5, -123, 7.0, 6.5, 7.5),
        (15.5, -130, 10.0, 8.0, 9.5),
    ]
    for i, (cx, cz, rx, rz, h) in enumerate(mass_left):
        objects.append(forest_mass(f"KaraMass_W{i}", cx, cz, rx, rz, h, 0.8,
                                   "AB_foliage_kara" if i % 2 else "AB_foliage_kara_deep",
                                   seed=i * 4.7))
    for i, (cx, cz, rx, rz, h) in enumerate(mass_right):
        objects.append(forest_mass(f"KaraMass_E{i}", cx, cz, rx, rz, h, 0.8,
                                   "AB_foliage_kara" if i % 2 else "AB_foliage_kara_deep",
                                   seed=i * 5.9 + 13.0))

    # ---- Far value planes closing the horizon --------------------------------
    far_defs = [
        (-12, -146, 14, 6, 10), (6, -148, 16, 6, 11), (22, -142, 12, 6, 9),
        (-26, -138, 12, 6, 9), (0, -155, 20, 7, 12), (-30, -120, 10, 6, 8),
        (30, -124, 11, 6, 9),
    ]
    for i, (cx, cz, rx, rz, h) in enumerate(far_defs):
        objects.append(forest_mass(f"KaraMass_Far{i}", cx, cz, rx, rz, h,
                                   0.85, "AB_foliage_kara_deep",
                                   seed=i * 3.3 + 31.0))

    # ---- Edge trunks framing the path ----------------------------------------
    trunk_defs = [
        (-3.8, -93.5, 7.5, 0.16, 0.06),
        (3.4, -95.5, 8.2, 0.18, -0.05),
        (-4.4, -101, 9.0, 0.2, -0.08),
        (4.1, -104.5, 8.6, 0.17, 0.09),
        (-3.6, -111, 9.4, 0.22, -0.04),
        (4.6, -113.5, 9.8, 0.19, 0.05),
        (-4.2, -119, 10.2, 0.24, -0.06),
        (3.2, -121.5, 10.8, 0.21, 0.07),
    ]
    for i, (cx, cz, h, r, lean) in enumerate(trunk_defs):
        objects.extend(edge_trunk(f"KaraTrunk_{i}", cx, cz, h, r, lean,
                                  "AB_bark_dark"))

    # ---- Root banks hugging the approach -------------------------------------
    for i, (cx, cz, yaw, w, h) in enumerate((
        (-2.6, -98, 70, 2.6, 0.9),
        (2.8, -106, -75, 2.4, 0.8),
        (-2.7, -114, 80, 2.8, 1.0),
        (2.4, -119.5, -70, 2.2, 0.75),
    )):
        objects.append(root_bank(f"KaraRoot_{i}", cx, cz, yaw, w, h))

    # ---- Fallen logs and stones ----------------------------------------------
    objects.append(fallen_log("FallenLog_K0", -2.9, -108.5, 25, 2.6))
    objects.append(fallen_log("FallenLog_K1", 2.6, -116.5, -15, 2.2))
    stone = ab.make_icosphere("KaraStone_0", (2.2, 93.0, 0.25), 0.42,
                              subdivisions=1)
    ab.displace_object(stone, 0.14, 1.6, seed_offset=2.2)
    ab.assign_material(stone, "AB_stone_dark")
    objects.append(stone)

    # ---- One almost-human branch gesture (read as silhouette) -----------------
    objects.extend(gesture_branch("KaraGesture", 1.0, -120.0))

    # ---- Cliffhanger clearing ring --------------------------------------------
    for i, (cx, cz, h, r) in enumerate((
        (-2.6, -124.5, 11.0, 0.24), (3.1, -125.5, 11.6, 0.22),
        (-2.2, -129.0, 12.2, 0.26), (2.4, -130.0, 12.0, 0.23),
        (0.4, -131.5, 12.8, 0.28),
    )):
        objects.extend(edge_trunk(f"CliffRing_{i}", cx, cz, h, r, 0.04 * (i % 2 and 1 or -1),
                                  "AB_bark_dark"))

    root = os.environ.get("AGENTB_OUT", HERE)
    glb_path = os.path.join(root, "game/assets/models/agent_b_act1",
                            "agentb_kara_edge_kit.glb")
    blend_path = os.path.join(root, "assets/source/blender/agent_b_act1",
                              "agentb_kara_edge_kit.blend")
    ab.export_glb(objects, glb_path, "URMAN_AgentB_KaraEdgeKit")
    ab.save_blend(blend_path)
    ab.report_kit("agentb_kara_edge_kit", objects, glb_path)


if __name__ == "__main__":
    main()
