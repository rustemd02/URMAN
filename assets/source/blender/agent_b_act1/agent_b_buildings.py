"""Agent B Act I Kit 2: authored Tatar village buildings.

Houses are built in local space (front face = +y street side), then yawed
and translated by baking vertex transforms. Windows are exported as separate
AB_window_warm / AB_window_cold meshes so the Godot composer keeps them as
distinct light points. No cameras, lights, collision or text authored.
"""

from __future__ import annotations

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import agent_b_common as ab  # noqa: E402


def wall(name, x0, x1, y0, y1, z0, z1, material, jitter=0.0):
    obj = ab.make_box(name, x0, x1, y0, y1, z0, z1, jitter=jitter)
    ab.assign_material(obj, material)
    return obj


def place(obj, x_godot, z_godot, yaw_deg, elevate=0.0):
    """Yaw the part around the house anchor (0,0) in local space, then bake
    the anchor translation into vertex data. The exported GLB node then only
    carries scale/translation."""
    if yaw_deg:
        ab.rotate_around(obj, math.radians(yaw_deg), "z", (0.0, 0.0, 0.0))
    ab.translate_vertices(obj, x_godot, -z_godot, elevate)


def gable_roof(prefix, half_w, half_d, eave_z, ridge_z, material):
    """Roof slabs running along local +y. Local space, origin centred."""
    objects = []
    overhang = 0.3
    for side, tag in ((-1, "W"), (1, "E")):
        x_base = side * (half_w + overhang)
        verts = [
            [x_base, -half_d - overhang, eave_z - 0.1],
            [x_base, half_d + overhang, eave_z - 0.1],
            [side * 0.1, half_d + 0.05, ridge_z + 0.06],
            [side * 0.1, -half_d - 0.05, ridge_z + 0.06],
        ]
        slab = ab.mesh_from_pydata(f"{prefix}_Slab{tag}", verts,
                                   [(0, 1, 2, 3), (3, 2, 1, 0)])
        for polygon in slab.data.polygons:
            polygon.use_smooth = False
        ab.assign_material(slab, material)
        objects.append(slab)
    ridge = ab.make_box(f"{prefix}_Ridge", -0.13, 0.13, -half_d - 0.08,
                        half_d + 0.08, ridge_z - 0.03, ridge_z + 0.14)
    ab.assign_material(ridge, "AB_timber_dark")
    objects.append(ridge)
    for end, tag in ((-1, "S"), (1, "N")):
        y = end * half_d
        verts = [
            [-half_w - 0.02, y, eave_z - 0.04],
            [half_w + 0.02, y, eave_z - 0.04],
            [0.0, y, ridge_z - 0.02],
        ]
        gable = ab.mesh_from_pydata(f"{prefix}_Gable{tag}", verts,
                                    [(0, 1, 2), (0, 2, 1)])
        ab.assign_material(gable, "AB_timber")
        objects.append(gable)
    return objects


def window_unit(prefix, face_y, wx, wz, w=0.72, h=0.94,
                glass_material="AB_window_cold"):
    """Window recessed into a wall whose outer face is at face_y (local)."""
    outward = 1.0 if face_y > 0 else -1.0
    objects = []
    frame = ab.make_box(f"{prefix}_Frame", wx - w / 2 - 0.08, wx + w / 2 + 0.08,
                        face_y - 0.02 * outward,
                        face_y + 0.1 * outward,
                        wz - h / 2 - 0.08, wz + h / 2 + 0.08)
    ab.assign_material(frame, "AB_timber_dark")
    objects.append(frame)
    glass = ab.make_box(f"{prefix}_Glass", wx - w / 2, wx + w / 2,
                        face_y + 0.005 * outward, face_y + 0.075 * outward,
                        wz - h / 2, wz + h / 2)
    ab.assign_material(glass, glass_material)
    objects.append(glass)
    # Sill.
    sill = ab.make_box(f"{prefix}_Sill", wx - w / 2 - 0.12, wx + w / 2 + 0.12,
                       face_y, face_y + 0.16 * outward,
                       wz - h / 2 - 0.14, wz - h / 2 - 0.05)
    ab.assign_material(sill, "AB_plaster_faded")
    objects.append(sill)
    return objects


def door_unit(prefix, face_y, wx, floor_z, w=0.86, h=1.92):
    outward = 1.0 if face_y > 0 else -1.0
    objects = []
    frame = ab.make_box(f"{prefix}_Frame", wx - w / 2 - 0.07, wx + w / 2 + 0.07,
                        face_y - 0.02 * outward, face_y + 0.08 * outward,
                        floor_z - 0.02, floor_z + h + 0.08)
    ab.assign_material(frame, "AB_timber")
    objects.append(frame)
    leaf = ab.make_box(f"{prefix}_Leaf", wx - w / 2, wx + w / 2,
                       face_y + 0.005 * outward, face_y + 0.06 * outward,
                       floor_z, floor_z + h)
    ab.assign_material(leaf, "AB_timber_dark")
    objects.append(leaf)
    return objects


def chimney(prefix, x, z_top, height=1.1):
    obj = ab.make_box(f"{prefix}_Chimney", x - 0.2, x + 0.2, -0.2, 0.2,
                      z_top - 0.3, z_top - 0.3 + height, jitter=0.01)
    ab.assign_material(obj, "AB_stone_dark")
    cap = ab.make_box(f"{prefix}_ChimneyCap", x - 0.26, x + 0.26, -0.26, 0.26,
                      z_top - 0.3 + height, z_top - 0.3 + height + 0.09)
    ab.assign_material(cap, "AB_stone_dark")
    return [obj, cap]


def porch(prefix, front_y, floor_z):
    objects = []
    deck = ab.make_box(f"{prefix}_PorchDeck", -1.35, 1.35, front_y,
                       front_y + 1.5, floor_z - 0.08, floor_z + 0.06,
                       jitter=0.01)
    ab.assign_material(deck, "AB_timber")
    objects.append(deck)
    roof = ab.make_box(f"{prefix}_PorchRoof", -1.5, 1.5, front_y + 0.2,
                       front_y + 1.75, floor_z + 2.25, floor_z + 2.42)
    ab.rotate_vertices_z(roof, 0.0)
    roof.rotation_euler.x = -0.14
    ab.assign_material(roof, "AB_roof_shingle")
    objects.append(roof)
    for side in (-1, 1):
        post = ab.make_box(f"{prefix}_PorchPost{'W' if side < 0 else 'E'}",
                           side * 1.2 - 0.05, side * 1.2 + 0.05,
                           front_y + 1.32, front_y + 1.48,
                           floor_z - 0.05, floor_z + 2.3)
        ab.assign_material(post, "AB_timber_dark")
        objects.append(post)
    for i in range(2):
        step = ab.make_box(f"{prefix}_PorchStep{i}", -0.55, 0.55,
                           front_y + 1.5 + i * 0.34,
                           front_y + 1.5 + (i + 1) * 0.34,
                           0.03, floor_z - 0.13 * i)
        ab.assign_material(step, "AB_timber")
        objects.append(step)
    return objects


def side_window_unit(prefix, side, hw, y0, z_center, w=0.6, h=0.75,
                     glass_material="AB_window_cold"):
    """Window on the east (+1) or west (-1) side wall at local x=±hw,
    centred at depth y0 and height z_center."""
    x_face = side * hw
    objects = []
    frame = ab.make_box(f"{prefix}_Frame",
                        x_face - 0.02 * side, x_face + 0.1 * side,
                        y0 - w / 2 - 0.08, y0 + w / 2 + 0.08,
                        z_center - h / 2 - 0.08, z_center + h / 2 + 0.08)
    ab.assign_material(frame, "AB_timber_dark")
    objects.append(frame)
    glass = ab.make_box(f"{prefix}_Glass", x_face + 0.005 * side,
                        x_face + 0.075 * side,
                        y0 - w / 2, y0 + w / 2,
                        z_center - h / 2, z_center + h / 2)
    ab.assign_material(glass, glass_material)
    objects.append(glass)
    return objects


def build_house(prefix, x_g, z_g, yaw_deg, *, w, d, wall_h, pitch,
                wall_mat, roof_mat, roof2=None, warm_front=2, cold_back=1,
                side_windows=0, has_porch=False, chimney_x=None,
                foundation=0.45, log_bands=False, trim=False):
    objects = []
    hw, hd = w / 2, d / 2
    objects.append(wall(f"{prefix}_Foundation", -hw - 0.09, hw + 0.09,
                        -hd - 0.09, hd + 0.09, 0, foundation,
                        "AB_stone_dark", jitter=0.02))
    body = wall(f"{prefix}_Body", -hw, hw, -hd, hd, foundation,
                foundation + wall_h, wall_mat, jitter=0.012)
    objects.append(body)
    if log_bands:
        for i in range(int(wall_h / 0.34)):
            band = wall(f"{prefix}_LogBand{i}", -hw - 0.02, hw + 0.02,
                        -hd - 0.02, hd + 0.02,
                        foundation + 0.14 + i * 0.34,
                        foundation + 0.22 + i * 0.34, "AB_log_wall")
            objects.append(band)
    eave = foundation + wall_h
    ridge = eave + math.tan(math.radians(pitch)) * hw
    objects.extend(gable_roof(f"{prefix}_Roof", hw, hd, eave, ridge,
                              roof2 or roof_mat))
    if chimney_x is not None:
        objects.extend(chimney(prefix, chimney_x, ridge))
    # Front (+y) windows: warm. Back (-y): cold.
    front_y = hd
    front_w = w - 1.4
    for k in range(warm_front):
        wx = 0.0 if warm_front == 1 else -front_w / 2 + front_w * k / (warm_front - 1)
        objects.extend(window_unit(f"{prefix}_WinF{k}", front_y, wx,
                                   foundation + wall_h * 0.55,
                                   glass_material="AB_window_warm"))
    back_w = w - 1.6
    for k in range(cold_back):
        wx = 0.0 if cold_back == 1 else -back_w / 2 + back_w * k / max(1, cold_back - 1)
        objects.extend(window_unit(f"{prefix}_WinB{k}", -hd, wx,
                                   foundation + wall_h * 0.55,
                                   glass_material="AB_window_cold"))
    for side, tag in ((-1, "W"), (1, "E")):
        for k in range(side_windows):
            y0 = -hd * 0.55 + k * d * 0.55
            objects.extend(side_window_unit(f"{prefix}_Win{tag}{k}", side, hw,
                                            y0, foundation + wall_h * 0.55))
    # Door on the front, offset from windows when crowded.
    door_x = -hw + 1.05 if warm_front >= 2 else 0.0
    objects.extend(door_unit(f"{prefix}_Door", front_y, door_x, foundation))
    if has_porch:
        objects.extend(porch(prefix, front_y, foundation))
    # Trim boards under the eaves for painted facades.
    if trim:
        trimmer = wall(f"{prefix}_Trim", -hw - 0.05, hw + 0.05,
                       front_y - 0.01, front_y + 0.04,
                       eave - 0.16, eave - 0.02, "AB_plaster_faded")
        objects.append(trimmer)
    for obj in objects:
        place(obj, x_g, z_g, yaw_deg)
    return objects


def fence_run(name, points_g: list[tuple[float, float]], height=1.05,
              material="AB_timber", lean=0.045, post_spacing=1.5):
    """Fence along world-space Godot points."""
    import random as _random
    rng = _random.Random(ab.stable_hash(name))
    objects = []
    for i in range(len(points_g) - 1):
        ax, az = points_g[i]
        bx, bz = points_g[i + 1]
        length = math.hypot(bx - ax, bz - az)
        if length < 0.01:
            continue
        count = max(1, int(length / post_spacing))
        for p in range(count + 1):
            t = p / count
            px = ax + (bx - ax) * t
            pz = az + (bz - az) * t
            if 0 < p < count and i < len(points_g) - 1:
                pass
            post = ab.make_box(f"{name}_Post{i}_{p}", -0.055, 0.055,
                               -0.055, 0.055, -0.05,
                               height + rng.uniform(-0.14, 0.08))
            ab.rotate_vertices_z(post, rng.uniform(-lean, lean))
            post.rotation_euler.x = rng.uniform(-lean, lean)
            ab.place_godot(post, px, pz, 0.0)
            ab.assign_material(post, material)
            objects.append(post)
        angle = math.atan2(-(bz - az), bx - ax)  # Blender rotation around z
        mid_x = (ax + bx) / 2
        mid_z = (az + bz) / 2
        for rz, tag in ((height * 0.32, "RailLow"), (height * 0.72, "RailHigh")):
            rail = ab.make_box(f"{name}_{tag}{i}", -length / 2, length / 2,
                               -0.028, 0.028, -0.032, 0.032)
            ab.rotate_vertices_z(rail, angle)
            ab.place_godot(rail, mid_x, mid_z, rz)
            ab.assign_material(rail, material)
            objects.append(rail)
    return objects


def gate(name, x_g, z_g, yaw_deg, width=1.7, height=1.2):
    objects = []
    for side in (-1, 1):
        post = ab.make_box(f"{name}_Post{'W' if side < 0 else 'E'}",
                           -0.07, 0.07, -0.07, 0.07, -0.05, height + 0.15)
        ab.rotate_vertices_z(post, math.radians(yaw_deg))
        px = math.cos(math.radians(yaw_deg)) * side * width / 2
        pz = -math.sin(math.radians(yaw_deg)) * side * width / 2
        ab.place_godot(post, x_g + px, z_g + pz, 0.0)
        ab.assign_material(post, "AB_timber_dark")
        objects.append(post)
    leaf = ab.make_box(f"{name}_Leaf", -width / 2 + 0.12, width / 2 - 0.12,
                       -0.035, 0.035, 0.08, height)
    ab.rotate_vertices_z(leaf, math.radians(yaw_deg) + 0.35)
    ab.place_godot(leaf, x_g, z_g, 0.0)
    ab.assign_material(leaf, "AB_timber")
    objects.append(leaf)
    return objects


def woodpile(name, x_g, z_g, yaw_deg, rows=3, length=2.2):
    import random as _random
    rng = _random.Random(ab.stable_hash(name))
    objects = []
    radius = 0.095
    for row in range(rows):
        count = max(1, int(length / (radius * 2.3)) - row)
        for i in range(count):
            lx = -length / 2 + radius + i * radius * 2.3 + row * radius * 1.1
            ly = row * radius * 2.0
            log = ab.make_cylinder(f"{name}_Log{row}_{i}", (0.0, 0.0, 0.0),
                                   radius, radius, length - row * radius * 2.2,
                                   segments=7, axis="x")
            ab.translate_vertices(log, lx, ly, radius + row * radius * 1.8)
            ab.rotate_around(log, rng.uniform(-0.04, 0.04), "z",
                             ab.bbox_center(log))
            ab.rotate_around(log, math.radians(yaw_deg), "z", (0.0, 0.0, 0.0))
            ab.translate_vertices(log, x_g, -z_g, 0.0)
            ab.assign_material(log, "AB_bark")
            objects.append(log)
    return objects


def lean_to(name, x_g, z_g, yaw_deg, w=2.6, d=1.9, h=2.0):
    """Lean-to hay shed: front open, single-slope roof."""
    objects = []
    hw, hd = w / 2, d / 2
    for side in (-1, 1):
        post = ab.make_box(f"{name}_Post{'W' if side < 0 else 'E'}",
                           -0.06, 0.06, -0.06, 0.06, 0.0, h)
        ab.rotate_vertices_z(post, math.radians(yaw_deg))
        ab.place_godot(post, x_g, z_g, 0.0)
        objects.append(post)
        ab.assign_material(post, "AB_timber")
    back = ab.make_box(f"{name}_Back", -hw, hw, -0.05, 0.05, 0.0, h - 0.3)
    ab.rotate_vertices_z(back, math.radians(yaw_deg))
    ab.place_godot(back, x_g, z_g, 0.0)
    ab.assign_material(back, "AB_wood_weathered" if False else "AB_timber")
    objects.append(back)
    roof = ab.make_box(f"{name}_Roof", -hw - 0.2, hw + 0.2, -hd - 0.25,
                       hd + 0.25, h - 0.24, h - 0.1)
    roof.rotation_euler.x = 0.2
    ab.rotate_vertices_z(roof, math.radians(yaw_deg))
    ab.place_godot(roof, x_g, z_g, 0.0)
    ab.assign_material(roof, "AB_roof_shingle")
    objects.append(roof)
    return objects


def build_fap(x_g, z_g, yaw_deg):
    """Rural FAP: plastered clinic, tin roof, sign, fence, shed."""
    prefix = "Fap"
    objects = []
    w, d = 7.8, 5.4
    hw, hd = w / 2, d / 2
    objects.append(wall(f"{prefix}_Foundation", -hw, hw, -hd, hd, 0, 0.42,
                        "AB_stone_dark", jitter=0.02))
    objects.append(wall(f"{prefix}_Body", -hw, hw, -hd, hd, 0.42, 2.95,
                        "AB_plaster", jitter=0.01))
    objects.extend(gable_roof(f"{prefix}_Roof", hw, hd, 2.95, 2.95 + 1.3,
                              "AB_roof_iron"))
    objects.extend(porch(f"{prefix}_Entry", hd, 0.42))
    for k, wx in enumerate((-2.6, -1.1, 1.1, 2.6)):
        if abs(wx - (-hw + 1.05)) < 0.9:
            continue
        objects.extend(window_unit(f"{prefix}_Win{k}", hd, wx, 1.72,
                                   glass_material="AB_window_warm"))
    objects.extend(door_unit(f"{prefix}_Door", hd, -hw + 1.05, 0.42))
    # Blank sign board next to the door.
    board = wall(f"{prefix}_Sign", -0.65, 0.65, hd + 0.02, hd + 0.09, 2.25,
                 2.8, "AB_sign_blank")
    objects.append(board)
    # Service shed behind.
    shed = wall(f"{prefix}_Shed", -1.2, 1.2, -hd - 3.6, -hd - 1.2, 0.0, 1.75,
                "AB_timber")
    objects.append(shed)
    shed_roof = wall(f"{prefix}_ShedRoof", -1.4, 1.4, -hd - 3.8, -hd - 1.0,
                     1.75, 1.9, "AB_roof_iron_dark")
    shed_roof.rotation_euler.x = 0.12
    objects.append(shed_roof)
    for obj in objects:
        place(obj, x_g, z_g, yaw_deg)
    # Fence along the road (north) side with an open gate at the branch end.
    fx = x_g
    north_z = z_g + 4.6
    objects.extend(fence_run(f"{prefix}_FenceW",
                             [(fx - 6.0, north_z), (26.9, north_z)],
                             height=1.0, material="AB_fade_paint"))
    objects.extend(fence_run(f"{prefix}_FenceE",
                             [(29.3, north_z), (fx + 6.0, north_z)],
                             height=1.0, material="AB_fade_paint"))
    objects.extend(fence_run(f"{prefix}_FenceSW",
                             [(fx - 6.0, north_z), (fx - 6.0, z_g - 3.6)],
                             height=1.0, material="AB_fade_paint"))
    objects.extend(fence_run(f"{prefix}_FenceSE",
                             [(fx + 6.0, north_z), (fx + 6.0, z_g - 3.6)],
                             height=1.0, material="AB_fade_paint"))
    objects.extend(gate(f"{prefix}_Gate", 28.1, north_z, 0, width=2.2,
                        height=1.1))
    return objects


def build_banya(x_g, z_g, yaw_deg):
    prefix = "Banya"
    objects = []
    objects.append(wall(f"{prefix}_Body", -1.6, 1.6, -1.9, 1.9, 0.18, 2.1,
                        "AB_log_wall"))
    hw, hd = 1.6, 1.9
    objects.extend(gable_roof(f"{prefix}_Roof", hw, hd, 2.1, 2.95,
                              "AB_roof_shingle"))
    objects.extend(chimney(prefix, 0.55, 2.85))
    objects.extend(window_unit(f"{prefix}_Win", hd, 0.0, 1.35, w=0.5, h=0.55,
                               glass_material="AB_window_cold"))
    objects.extend(door_unit(f"{prefix}_Door", -hd, 0.5, 0.18, w=0.75, h=1.7))
    for obj in objects:
        place(obj, x_g, z_g, yaw_deg)
    return objects


def build_well(x_g, z_g):
    prefix = "Well"
    objects = []
    for i in range(8):
        angle = i / 8 * math.tau
        stone = ab.make_box(f"{prefix}_Stone{i}", -0.36, 0.36, -0.19, 0.19,
                            0.0, 0.52, jitter=0.03)
        ab.rotate_vertices_z(stone, angle)
        ab.place_godot(stone, x_g + math.cos(angle) * 0.8,
                       z_g - math.sin(angle) * 0.8, 0.0)
        ab.assign_material(stone, "AB_stone")
        objects.append(stone)
    for side in (-1, 1):
        post = ab.make_box(f"{prefix}_Post{'W' if side < 0 else 'E'}",
                           -0.06, 0.06, -0.06, 0.06, 0.0, 1.55)
        ab.place_godot(post, x_g + side * 0.72, z_g, 0.0)
        ab.assign_material(post, "AB_timber")
        objects.append(post)
    roof = ab.make_box(f"{prefix}_Roof", -1.05, 1.05, -0.85, 0.85, 1.55, 1.7)
    roof.rotation_euler.x = 0.22
    ab.place_godot(roof, x_g, z_g, 0.0)
    ab.assign_material(roof, "AB_roof_shingle")
    objects.append(roof)
    return objects


def main() -> None:
    ab.setup_scene()
    objects: list = []

    # ---- Babai/ebi hero house ----------------------------------------------
    objects.extend(build_house("HouseBabai", -30, -1, -90, w=6.6, d=5.6,
                               wall_h=2.5, pitch=42, wall_mat="AB_log_wall",
                               roof_mat="AB_roof_iron", warm_front=3,
                               cold_back=2, side_windows=1, has_porch=True,
                               chimney_x=1.1, log_bands=True))
    # Yard outbuildings and boundary.
    objects.extend(build_house("ShedBabai", -35.5, -4, 25, w=3.6, d=3.2,
                               wall_h=1.9, pitch=38, wall_mat="AB_timber",
                               roof_mat="AB_roof_shingle", warm_front=0,
                               cold_back=1))
    objects.extend(woodpile("WoodpileBabai", -33.8, -6.6, 10, rows=3))
    objects.extend(lean_to("LeanToBabai", -27.5, -5.4, 95, w=2.4, d=1.7))
    # West fence with a gate gap around z=2.6.
    objects.extend(fence_run("FenceBabaiW_S", [(-25.2, -6.4), (-25.2, 1.75)]))
    objects.extend(fence_run("FenceBabaiW_N", [(-25.2, 3.45), (-25.2, 5.2)]))
    objects.extend(fence_run("FenceBabaiS", [(-25.2, -6.4), (-36.2, -6.4)]))
    objects.extend(fence_run("FenceBabaiN", [(-25.2, 5.2), (-36.2, 5.2)]))
    objects.extend(gate("GateBabai", -25.2, 2.6, 90))
    objects.extend(build_well(-20.8, -2.2))

    # ---- Street houses, distinct silhouettes --------------------------------
    spec = [
        ("HouseA1", -8.6, 2.5, -94, 5.6, 4.6, 2.4, 40, "AB_plaster_faded", "AB_roof_iron_dark", 2, 1, True,  0.9,  True),
        ("HouseA2",  8.2, 0.5, 94, 5.2, 4.4, 2.3, 44, "AB_log_wall",   "AB_roof_shingle",   2, 1, False, -0.9, True),
        ("HouseA3", -8.8, -9.5, -92, 6.0, 4.8, 2.6, 38, "AB_plaster",    "AB_roof_iron",      3, 2, True,  1.0,  False),
        ("HouseA4",  8.4, -11.0, 98, 4.6, 4.0, 2.2, 46, "AB_timber",   "AB_roof_iron_dark", 1, 2, False, 0.7,  True),
        ("HouseA5", -9.2, -32.0, -95, 5.8, 4.6, 2.5, 41, "AB_log_wall",  "AB_roof_shingle",   2, 2, True, -1.0, True),
        ("HouseA6",  8.6, -30.5, 92, 5.0, 4.2, 2.3, 40, "AB_plaster_faded", "AB_roof_iron", 2, 1, False, 0.8,  False),
        ("HouseA7", -9.4, -42.5, -90, 6.2, 4.8, 2.6, 36, "AB_log_wall",  "AB_roof_iron_dark", 3, 1, True,  1.1,  True),
        ("HouseA8",  8.8, -40.8, 95, 4.8, 4.0, 2.2, 43, "AB_timber",   "AB_roof_shingle",   1, 2, False, -0.8, False),
    ]
    for name, x, z, yaw, w, d, wh, p, wm, rm, wf, cb, po, chx, bands in spec:
        objects.extend(build_house(name, x, z, yaw, w=w, d=d, wall_h=wh,
                                   pitch=p, wall_mat=wm, roof_mat=rm,
                                   warm_front=wf, cold_back=cb, has_porch=po,
                                   chimney_x=chx, log_bands=bands,
                                   trim=not bands))
        sx = x + (2.4 if x > 0 else -2.4)
        objects.extend(woodpile(f"Woodpile_{name}", sx, z - 2.2, yaw + 90,
                                rows=2, length=1.8))
        fx = x + (2.6 if x > 0 else -2.6)
        objects.extend(fence_run(f"Fence_{name}",
                                 [(fx, z - 3.6), (fx, z + 2.7)]))
        objects.extend(gate(f"Gate_{name}", fx, z + 3.6, 0, width=1.6,
                            height=1.1))

    # ---- FAP ------------------------------------------------------------------
    objects.extend(build_fap(31.0, -31.0, 127))

    # ---- Banya + lean-tos near return street ---------------------------------
    objects.extend(build_banya(-14.5, -47.0, 30))
    objects.extend(lean_to("LeanToA5", -12.6, -35.0, 80))

    # ---- Far silhouette houses ------------------------------------------------
    for name, x, z, yaw in (
        ("FarHouse1", -18.0, -52.0, 60),
        ("FarHouse2", 16.5, -50.0, -70),
        ("FarHouse3", -20.0, 8.0, 40),
    ):
        objects.extend(build_house(name, x, z, yaw, w=4.6, d=3.8, wall_h=2.1,
                                   pitch=38, wall_mat="AB_timber",
                                   roof_mat="AB_roof_shingle", warm_front=1,
                                   cold_back=0))

    root = os.environ.get("AGENTB_OUT", HERE)
    glb_path = os.path.join(root, "game/assets/models/agent_b_act1",
                            "agentb_village_buildings_kit.glb")
    blend_path = os.path.join(root, "assets/source/blender/agent_b_act1",
                              "agentb_village_buildings_kit.blend")
    ab.export_glb(objects, glb_path, "URMAN_AgentB_VillageBuildingsKit")
    ab.save_blend(blend_path)
    ab.report_kit("agentb_village_buildings_kit", objects, glb_path)


if __name__ == "__main__":
    main()
