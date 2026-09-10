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


def wall(name, x0, x1, y0, y1, z0, z1, material, jitter=0.0,
         top_inset=0.0, top_shift_x=0.0, top_shift_y=0.0):
    obj = ab.make_box(name, x0, x1, y0, y1, z0, z1, jitter=jitter)
    # Let a few authored wall bodies settle under their eaves instead of
    # reading as perfect cubes.  The base stays at the published footprint so
    # route/collision contact is unchanged; only the four top vertices taper.
    if top_inset or top_shift_x or top_shift_y:
        centre_x = (x0 + x1) * 0.5
        centre_y = (y0 + y1) * 0.5
        for vertex in obj.data.vertices[4:8]:
            vertex.co.x += (
                -math.copysign(top_inset, vertex.co.x - centre_x)
                + top_shift_x
            )
            vertex.co.y += (
                -math.copysign(top_inset * 0.75, vertex.co.y - centre_y)
                + top_shift_y
            )
        obj.data.update()
    ab.assign_material(obj, material)
    return obj


def place(obj, x_godot, z_godot, yaw_deg, elevate=0.0):
    """Yaw the part around the house anchor (0,0) in local space, then bake
    the anchor translation into vertex data. The exported GLB node then only
    carries scale/translation."""
    if yaw_deg:
        ab.rotate_around(obj, math.radians(yaw_deg), "z", (0.0, 0.0, 0.0))
    ab.translate_vertices(obj, x_godot, -z_godot, elevate)


def gable_roof(prefix, half_w, half_d, eave_z, ridge_z, material,
               ridge_offset=0.0, overhang=0.3,
               front_overhang=0.0, back_overhang=0.0):
    """Roof slabs running along local +y. Local space, origin centred."""
    objects = []
    ridge_x = max(-half_w * 0.28, min(half_w * 0.28, ridge_offset))
    roof_back = half_d + overhang + back_overhang
    roof_front = half_d + overhang + front_overhang
    ridge_back = half_d + 0.05 + back_overhang
    ridge_front = half_d + 0.05 + front_overhang
    for side, tag in ((-1, "W"), (1, "E")):
        x_base = side * (half_w + overhang)
        verts = [
            [x_base, -roof_back, eave_z - 0.1],
            [x_base, roof_front, eave_z - 0.1],
            [ridge_x, ridge_front, ridge_z + 0.06],
            [ridge_x, -ridge_back, ridge_z + 0.06],
        ]
        slab = ab.mesh_from_pydata(f"{prefix}_Slab{tag}", verts,
                                   [(0, 1, 2, 3), (3, 2, 1, 0)])
        for polygon in slab.data.polygons:
            polygon.use_smooth = False
        ab.assign_material(slab, material)
        objects.append(slab)
    ridge = ab.make_box(f"{prefix}_Ridge", ridge_x - 0.13, ridge_x + 0.13,
                        -half_d - 0.08 - back_overhang,
                        half_d + 0.08 + front_overhang,
                        ridge_z - 0.03, ridge_z + 0.14)
    ab.assign_material(ridge, "AB_timber_dark")
    objects.append(ridge)
    for end, tag in ((-1, "S"), (1, "N")):
        y = end * half_d
        verts = [
            [-half_w - 0.02, y, eave_z - 0.04],
            [half_w + 0.02, y, eave_z - 0.04],
            [ridge_x, y, ridge_z - 0.02],
        ]
        gable = ab.mesh_from_pydata(f"{prefix}_Gable{tag}", verts,
                                    [(0, 1, 2), (0, 2, 1)])
        ab.assign_material(gable, "AB_timber")
        objects.append(gable)
    return objects


def eave_details(prefix, half_w, half_d, eave_z, overhang=0.3,
                 front_overhang=0.0, back_overhang=0.0):
    """Broad fascia/eave members that make the full roof volume readable."""
    objects = []
    for side, tag in ((-1, "W"), (1, "E")):
        x = side * (half_w + overhang - 0.04)
        beam = ab.make_box(
            f"{prefix}_EaveBeam{tag}",
            x - 0.075,
            x + 0.075,
            -half_d - overhang - back_overhang,
            half_d + overhang + front_overhang,
            eave_z - 0.13,
            eave_z + 0.04,
            jitter=0.006,
        )
        ab.assign_material(beam, "AB_timber_dark")
        objects.append(beam)
    for end, tag in ((-1, "S"), (1, "N")):
        end_overhang = front_overhang if end > 0 else back_overhang
        y = end * (half_d + overhang + end_overhang - 0.04)
        fascia = ab.make_box(
            f"{prefix}_Fascia{tag}",
            -half_w - overhang,
            half_w + overhang,
            y - 0.065,
            y + 0.065,
            eave_z - 0.12,
            eave_z + 0.03,
            jitter=0.004,
        )
        ab.assign_material(fascia, "AB_timber_dark")
        objects.append(fascia)
    return objects


def rear_annex(prefix, half_w, half_d, foundation, style):
    """Small attached utility lean-to; kept behind the street-facing wall."""
    if style < 2:
        return []
    side = -1 if style % 2 == 0 else 1
    width = 2.15 if style == 2 else 1.75
    depth = 1.45 if style == 2 else 1.75
    wall_h = 1.45 if style == 2 else 1.72
    centre_x = side * (half_w - width * 0.40)
    y_back = -half_d - depth
    y_front = -half_d + 0.10
    objects = []
    body = ab.make_box(
        f"{prefix}_AnnexBody",
        centre_x - width / 2,
        centre_x + width / 2,
        y_back,
        y_front,
        foundation,
        foundation + wall_h,
        jitter=0.012,
    )
    ab.assign_material(body, "AB_timber_dark" if style == 2 else "AB_log_wall")
    objects.append(body)
    roof = ab.make_box(
        f"{prefix}_AnnexRoof",
        centre_x - width / 2 - 0.18,
        centre_x + width / 2 + 0.18,
        y_back - 0.16,
        y_front + 0.22,
        foundation + wall_h,
        foundation + wall_h + 0.16,
        jitter=0.008,
    )
    # Rotate around the authored roof volume, not the scene origin; baking an
    # origin rotation would otherwise pull rear annexes below the ground line.
    ab.rotate_around(roof, 0.16 if side > 0 else -0.12, "x",
                     ab.bbox_center(roof))
    ab.assign_material(roof, "AB_roof_shingle")
    objects.append(roof)
    eave = ab.make_box(
        f"{prefix}_AnnexEave",
        centre_x - width / 2 - 0.20,
        centre_x + width / 2 + 0.20,
        y_back - 0.19,
        y_back - 0.04,
        foundation + wall_h - 0.03,
        foundation + wall_h + 0.10,
        jitter=0.004,
    )
    ab.assign_material(eave, "AB_timber_dark")
    objects.append(eave)
    window_y = y_back + 0.06
    objects.extend(window_unit(
        f"{prefix}_AnnexWin",
        window_y,
        centre_x,
        foundation + wall_h * 0.55,
        w=0.52,
        h=0.62,
        glass_material="AB_window_cold",
    ))
    return objects


def window_unit(prefix, face_y, wx, wz, w=0.72, h=0.94,
                glass_material="AB_window_cold"):
    """Window recessed into a wall whose outer face is at face_y (local)."""
    outward = 1.0 if face_y > 0 else -1.0
    objects = []
    # Four frame members leave the glazing visible. A solid frame box
    # previously enclosed the glass, turning every opening into a dark panel.
    front, back = sorted((face_y - 0.02 * outward, face_y + 0.1 * outward))
    for tag, x0, x1, z0, z1 in (
        ("Left", wx - w / 2 - 0.08, wx - w / 2, wz - h / 2, wz + h / 2),
        ("Right", wx + w / 2, wx + w / 2 + 0.08, wz - h / 2, wz + h / 2),
        ("Top", wx - w / 2 - 0.08, wx + w / 2 + 0.08, wz + h / 2, wz + h / 2 + 0.08),
        ("Bottom", wx - w / 2 - 0.08, wx + w / 2 + 0.08, wz - h / 2 - 0.08, wz - h / 2),
    ):
        frame = ab.make_box(f"{prefix}_Frame{tag}", x0, x1, front, back, z0, z1)
        ab.assign_material(frame, "AB_timber_dark")
        objects.append(frame)
    glass_front, glass_back = sorted((face_y + 0.045 * outward, face_y + 0.075 * outward))
    glass = ab.make_box(f"{prefix}_Glass", wx - w / 2, wx + w / 2,
                        glass_front, glass_back,
                        wz - h / 2, wz + h / 2)
    ab.assign_material(glass, glass_material)
    objects.append(glass)
    # Sill.
    sill_front, sill_back = sorted((face_y, face_y + 0.16 * outward))
    sill = ab.make_box(f"{prefix}_Sill", wx - w / 2 - 0.12, wx + w / 2 + 0.12,
                       sill_front, sill_back,
                       wz - h / 2 - 0.14, wz - h / 2 - 0.05)
    ab.assign_material(sill, "AB_plaster_faded")
    objects.append(sill)
    if glass_material == "AB_window_warm":
        mullion_v = ab.make_box(
            f"{prefix}_MullionV",
            wx - 0.035,
            wx + 0.035,
            min(face_y, face_y + 0.09 * outward),
            max(face_y, face_y + 0.09 * outward),
            wz - h / 2 + 0.05,
            wz + h / 2 - 0.05,
        )
        ab.assign_material(mullion_v, "AB_timber_dark")
        objects.append(mullion_v)
        mullion_h = ab.make_box(
            f"{prefix}_MullionH",
            wx - w / 2 + 0.05,
            wx + w / 2 - 0.05,
            min(face_y, face_y + 0.09 * outward),
            max(face_y, face_y + 0.09 * outward),
            wz - 0.035,
            wz + 0.035,
        )
        ab.assign_material(mullion_h, "AB_timber_dark")
        objects.append(mullion_h)
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
    lintel = ab.make_box(
        f"{prefix}_Lintel",
        wx - w / 2 - 0.14,
        wx + w / 2 + 0.14,
        min(face_y - 0.02 * outward, face_y + 0.08 * outward),
        max(face_y - 0.02 * outward, face_y + 0.08 * outward),
        floor_z + h + 0.05,
        floor_z + h + 0.17,
    )
    ab.assign_material(lintel, "AB_timber")
    objects.append(lintel)
    return objects


def chimney(prefix, x, z_top, height=1.1):
    obj = ab.make_box(f"{prefix}_Chimney", x - 0.2, x + 0.2, -0.2, 0.2,
                      z_top - 0.3, z_top - 0.3 + height, jitter=0.01)
    ab.assign_material(obj, "AB_stone_dark")
    cap = ab.make_box(f"{prefix}_ChimneyCap", x - 0.26, x + 0.26, -0.26, 0.26,
                      z_top - 0.3 + height, z_top - 0.3 + height + 0.09)
    ab.assign_material(cap, "AB_stone_dark")
    return [obj, cap]


def porch(prefix, front_y, floor_z, width=1.35, depth=1.5,
          roof_pitch=-0.14, roof_shift_x=0.0):
    objects = []
    deck = ab.make_box(f"{prefix}_PorchDeck", -width, width, front_y,
                       front_y + depth, floor_z - 0.08, floor_z + 0.06,
                       jitter=0.01)
    ab.assign_material(deck, "AB_timber")
    objects.append(deck)
    roof = ab.make_box(f"{prefix}_PorchRoof", -width - 0.15, width + 0.15,
                       front_y + 0.2, front_y + depth + 0.25,
                       floor_z + 2.25, floor_z + 2.42)
    if roof_shift_x:
        ab.translate_vertices(roof, roof_shift_x, 0.0, 0.0)
    ab.rotate_vertices_z(roof, 0.0)
    ab.rotate_around(roof, roof_pitch, "x", ab.bbox_center(roof))
    ab.assign_material(roof, "AB_roof_shingle")
    objects.append(roof)
    roof_edge = ab.make_box(
        f"{prefix}_PorchRoofEdge",
        -width - 0.23 + roof_shift_x,
        width + 0.23 + roof_shift_x,
        front_y + depth + 0.18,
        front_y + depth + 0.32,
        floor_z + 2.22,
        floor_z + 2.36,
        jitter=0.004,
    )
    ab.rotate_around(roof_edge, roof_pitch, "x", ab.bbox_center(roof_edge))
    ab.assign_material(roof_edge, "AB_timber_dark")
    objects.append(roof_edge)
    for side in (-1, 1):
        post_x = side * (width - 0.15)
        post = ab.make_box(f"{prefix}_PorchPost{'W' if side < 0 else 'E'}",
                           post_x - 0.05, post_x + 0.05,
                           front_y + depth - 0.18, front_y + depth - 0.02,
                           floor_z - 0.05, floor_z + 2.3)
        ab.assign_material(post, "AB_timber_dark")
        objects.append(post)
        rail = ab.make_box(
            f"{prefix}_PorchRail{'W' if side < 0 else 'E'}",
            side * (width - 0.09) - 0.055,
            side * (width - 0.09) + 0.055,
            front_y + depth * 0.49,
            front_y + depth - 0.06,
            floor_z + 0.42,
            floor_z + 0.53,
        )
        ab.assign_material(rail, "AB_timber")
        objects.append(rail)
    for i in range(2):
        step = ab.make_box(f"{prefix}_PorchStep{i}", -0.55, 0.55,
                           front_y + depth + i * 0.34,
                           front_y + depth + (i + 1) * 0.34,
                           0.03, floor_z - 0.13 * i)
        ab.assign_material(step, "AB_timber")
        objects.append(step)
    return objects


def side_window_unit(prefix, side, hw, y0, z_center, w=0.6, h=0.75,
                     glass_material="AB_window_cold"):
    """Window on the east (+1) or west (-1) side wall at local x=±hw,
    centred at depth y0 and height z_center."""
    objects = window_unit(prefix, hw, -side * y0, z_center, w, h, glass_material)
    for obj in objects:
        ab.rotate_around(obj, -side * math.pi / 2, "z", (0.0, 0.0, 0.0))
    return objects


def build_house(prefix, x_g, z_g, yaw_deg, *, w, d, wall_h, pitch,
                wall_mat, roof_mat, roof2=None, warm_front=2, cold_back=1,
                side_windows=0, has_porch=False, chimney_x=None,
                foundation=0.45, log_bands=False, trim=False,
                body_taper=0.0, body_shift_x=0.0, ridge_offset=0.0,
                roof_overhang=0.3, front_window_bias=0.0,
                porch_width=1.35, porch_depth=1.5,
                porch_roof_pitch=-0.14, porch_shift_x=0.0):
    objects = []
    hw, hd = w / 2, d / 2
    objects.append(wall(f"{prefix}_Foundation", -hw - 0.09, hw + 0.09,
                        -hd - 0.09, hd + 0.09, 0, foundation,
                        "AB_stone_dark", jitter=0.02))
    body = wall(f"{prefix}_Body", -hw, hw, -hd, hd, foundation,
                foundation + wall_h, wall_mat, jitter=0.012,
                top_inset=body_taper, top_shift_x=body_shift_x)
    objects.append(body)
    if log_bands:
        for i in range(int(wall_h / 0.34)):
            band_z = foundation + 0.14 + i * 0.34
            if prefix in ("HouseBabai", "HouseA5"):
                band_seed = ab.stable_hash(f"{prefix}:log-band:{i}")
                band_out = 0.02 + (band_seed % 3) * 0.012
                band_h = 0.075 + ((band_seed // 3) % 3) * 0.008
                band = wall(
                    f"{prefix}_LogBand{i}",
                    -hw - band_out,
                    hw + band_out,
                    -hd - band_out * 0.9,
                    hd + band_out * 0.9,
                    band_z,
                    band_z + band_h,
                    "AB_log_wall",
                    jitter=0.006,
                    top_shift_x=((band_seed // 9) % 3 - 1) * 0.018,
                )
            else:
                band = wall(f"{prefix}_LogBand{i}", -hw - 0.02, hw + 0.02,
                            -hd - 0.02, hd + 0.02,
                            band_z, foundation + 0.22 + i * 0.34,
                            "AB_log_wall")
            objects.append(band)
    eave = foundation + wall_h
    ridge = eave + math.tan(math.radians(pitch)) * hw
    objects.extend(gable_roof(f"{prefix}_Roof", hw, hd, eave, ridge,
                              roof2 or roof_mat,
                              ridge_offset=ridge_offset,
                              overhang=roof_overhang))
    if chimney_x is not None:
        objects.extend(chimney(prefix, chimney_x, ridge))
    # Front (+y) windows: warm. Back (-y): cold.
    front_y = hd
    front_w = w - 1.4
    for k in range(warm_front):
        wx = 0.0 if warm_front == 1 else -front_w / 2 + front_w * k / (warm_front - 1)
        wx += front_window_bias
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
            side_glass = "AB_window_warm" if prefix in ("HouseBabai", "HouseA5") and side > 0 and k == 0 else "AB_window_cold"
            objects.extend(side_window_unit(f"{prefix}_Win{tag}{k}", side, hw,
                                            y0, foundation + wall_h * 0.55,
                                            glass_material=side_glass))
    # Door on the front, offset from windows when crowded.
    door_x = -hw + 1.05 if warm_front >= 2 else 0.0
    objects.extend(door_unit(f"{prefix}_Door", front_y, door_x, foundation))
    if has_porch:
        objects.extend(porch(prefix, front_y, foundation,
                             width=porch_width, depth=porch_depth,
                             roof_pitch=porch_roof_pitch,
                             roof_shift_x=porch_shift_x))
    # Trim boards under the eaves for painted facades.
    if trim:
        trimmer = wall(f"{prefix}_Trim", -hw - 0.05, hw + 0.05,
                       front_y - 0.01, front_y + 0.04,
                       eave - 0.16, eave - 0.02, "AB_plaster_faded")
        objects.append(trimmer)

    # A few broad, deterministic construction cues break the repeated-box
    # read without changing the accepted house footprint or front approach.
    objects.extend(eave_details(f"{prefix}_Roof", hw, hd, eave,
                                overhang=roof_overhang))
    profile = ab.stable_hash(prefix) % 4
    for corner_index, (cx, cy) in enumerate((
        (-hw - 0.02, -hd - 0.02),
        (hw - 0.02, -hd - 0.02),
        (-hw - 0.02, hd - 0.02),
        (hw - 0.02, hd - 0.02),
    )):
        stone = wall(
            f"{prefix}_FoundationStone{corner_index}",
            cx - 0.18,
            cx + 0.18,
            cy - 0.16,
            cy + 0.16,
            foundation - 0.04,
            foundation + 0.12,
            "AB_stone",
            jitter=0.025,
        )
        objects.append(stone)

    if warm_front and profile in (1, 3):
        shutter_w = 0.18
        shutter_z = foundation + wall_h * 0.55
        shutter = wall(
            f"{prefix}_WinF0_Shutter",
            -front_w / 2 - shutter_w - 0.05,
            -front_w / 2 - 0.03,
            front_y + 0.07,
            front_y + 0.16,
            shutter_z - 0.40,
            shutter_z + 0.40,
            "AB_timber_dark",
            jitter=0.008,
        )
        shutter.rotation_euler.z = math.radians(-4.0 if profile == 1 else 5.0)
        objects.append(shutter)

    objects.extend(rear_annex(f"{prefix}", hw, hd, foundation, profile))

    # Full-volume silhouette pass. The body/roof already occupy the accepted
    # footprint; these shallow, presentation-only trims explain the rear and
    # side planes in a first-person turn without adding another building or
    # touching the route owner. "Trim" keeps them outside AgentB's architecture
    # collision filter, while the deterministic per-house offsets stop the
    # street row from reading as one repeated box.
    variation = (ab.stable_hash(prefix) % 5 - 2) * 0.018
    trim_material = "AB_timber_dark" if wall_mat != "AB_plaster" else "AB_timber"
    for side, tag, depth_offset in (
        (-1, "W", -0.02),
        (1, "E", 0.015),
    ):
        for end, end_tag in ((-1, "S"), (1, "N")):
            corner_x = side * (hw - 0.07)
            corner_y = end * (hd - 0.06)
            corner = wall(
                f"{prefix}_CornerTrim_{tag}{end_tag}",
                corner_x - 0.085,
                corner_x + 0.085,
                corner_y - 0.085,
                corner_y + 0.085,
                foundation - 0.01,
                eave - 0.02 + variation + depth_offset,
                trim_material,
                jitter=0.004,
            )
            objects.append(corner)
    for end, end_tag in ((-1, "S"), (1, "N")):
        y = end * (hd + 0.11)
        fascia = wall(
            f"{prefix}_EaveTrim_{end_tag}",
            -hw - 0.18,
            hw + 0.18,
            y - 0.07,
            y + 0.07,
            eave - 0.16 + variation,
            eave - 0.02 + variation,
            "AB_timber_dark",
            jitter=0.006,
        )
        objects.append(fascia)
    threshold = wall(
        f"{prefix}_DoorThresholdTrim",
        door_x - 0.52,
        door_x + 0.52,
        front_y + 0.08,
        front_y + 0.30,
        foundation - 0.02,
        foundation + 0.10,
        "AB_stone_dark",
        jitter=0.006,
    )
    objects.append(threshold)
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
            post_height = height + rng.uniform(-0.14, 0.08)
            post = ab.make_box(f"{name}_Post{i}_{p}", -0.055, 0.055,
                               -0.055, 0.055, -0.05,
                               post_height)
            ab.rotate_vertices_z(post, rng.uniform(-lean, lean))
            post.rotation_euler.x = rng.uniform(-lean, lean)
            ab.place_godot(post, px, pz, 0.0)
            ab.assign_material(post, material)
            objects.append(post)
            cap = ab.make_box(
                f"{name}_PostCap{i}_{p}",
                -0.085,
                0.085,
                -0.085,
                0.085,
                post_height - 0.015,
                post_height + 0.075,
                jitter=0.006,
            )
            cap.rotation_euler.x = rng.uniform(-lean * 0.5, lean * 0.5)
            cap.rotation_euler.y = rng.uniform(-lean * 0.5, lean * 0.5)
            ab.place_godot(cap, px, pz, 0.0)
            ab.assign_material(cap, material)
            objects.append(cap)
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
        for p in range(0, count + 1, 2):
            t = p / count
            px = ax + (bx - ax) * t
            pz = az + (bz - az) * t
            board_height = height * 0.86 + rng.uniform(-0.16, 0.06)
            board = ab.make_box(
                f"{name}_Board{i}_{p}",
                -0.14,
                0.14,
                -0.035,
                0.035,
                0.04,
                board_height,
                jitter=0.006,
            )
            ab.rotate_vertices_z(board, angle)
            ab.place_godot(board, px, pz, 0.0)
            ab.assign_material(board, material)
            objects.append(board)
        brace = ab.make_box(
            f"{name}_Brace{i}",
            -length * 0.32,
            length * 0.32,
            -0.045,
            0.045,
            -0.045,
            0.045,
        )
        ab.rotate_vertices_z(brace, angle)
        brace.rotation_euler.y = -math.atan2(height * 0.34, max(length * 0.64, 0.1))
        ab.place_godot(brace, mid_x, mid_z, height * 0.46)
        ab.assign_material(brace, material)
        objects.append(brace)
    return objects


def gate(name, x_g, z_g, yaw_deg, width=1.7, height=1.2, open_angle=0.35):
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
        cap = ab.make_box(
            f"{name}_PostCap{'W' if side < 0 else 'E'}",
            -0.10,
            0.10,
            -0.10,
            0.10,
            height + 0.10,
            height + 0.22,
            jitter=0.006,
        )
        ab.rotate_vertices_z(cap, math.radians(yaw_deg))
        ab.place_godot(cap, x_g + px, z_g + pz, 0.0)
        ab.assign_material(cap, "AB_timber")
        objects.append(cap)
    leaf = ab.make_box(f"{name}_Leaf", -width / 2 + 0.12, width / 2 - 0.12,
                       -0.035, 0.035, 0.08, height)
    ab.assign_material(leaf, "AB_timber")
    objects.append(leaf)
    latch = ab.make_box(
        f"{name}_LatchRail",
        -width / 2 + 0.18,
        width / 2 - 0.18,
        -0.055,
        0.055,
        height * 0.47,
        height * 0.58,
    )
    ab.assign_material(latch, "AB_timber_dark")
    objects.append(latch)
    brace = ab.make_box(
        f"{name}_DiagonalBrace",
        -width * 0.34,
        width * 0.34,
        -0.045,
        0.045,
        -0.045,
        0.045,
    )
    brace.rotation_euler.y = -math.atan2(height * 0.56, max(width * 0.68, 0.1))
    ab.bake_transforms(brace)
    ab.assign_material(brace, "AB_timber_dark")
    objects.append(brace)
    # A gate swings around its hinge, not its centre. All three leaf parts
    # share this transform; the Babai entrance is fully open into the yard.
    hinge = -width / 2 + 0.12
    for part, lift in ((leaf, 0.0), (latch, 0.0), (brace, height * 0.48)):
        ab.translate_vertices(part, -hinge, 0.0, 0.0)
        ab.rotate_vertices_z(part, open_angle)
        ab.translate_vertices(part, hinge, 0.0, 0.0)
        if abs(open_angle - math.pi / 2) < 1e-6:
            assert max(abs(v.co.x - hinge) for v in part.data.vertices) < 0.06, "open gate intrudes into its passage"
        ab.rotate_vertices_z(part, math.radians(yaw_deg))
        ab.place_godot(part, x_g, z_g, lift)
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
    yaw = math.radians(yaw_deg)
    rack_height = 0.86 if rows >= 3 else 0.66
    for side, tag in ((-1, "W"), (1, "E")):
        post = ab.make_box(
            f"{name}_RackPost{tag}",
            -0.075,
            0.075,
            -0.075,
            0.075,
            0.0,
            rack_height,
            jitter=0.008,
        )
        ab.rotate_vertices_z(post, yaw)
        px = x_g + math.cos(yaw) * side * length / 2
        pz = z_g - math.sin(yaw) * side * length / 2
        ab.place_godot(post, px, pz, 0.0)
        ab.assign_material(post, "AB_bark_dark")
        objects.append(post)
    rack = ab.make_box(
        f"{name}_RackHeader",
        -length / 2 - 0.10,
        length / 2 + 0.10,
        -0.075,
        0.075,
        rack_height - 0.08,
        rack_height + 0.04,
        jitter=0.006,
    )
    ab.rotate_vertices_z(rack, yaw)
    ab.place_godot(rack, x_g, z_g, 0.0)
    ab.assign_material(rack, "AB_timber_dark")
    objects.append(rack)
    return objects


def lean_to(name, x_g, z_g, yaw_deg, w=2.6, d=1.9, h=2.0):
    """Lean-to hay shed: front open, single-slope roof."""
    objects = []
    hw, hd = w / 2, d / 2
    floor = ab.make_box(f"{name}_Deck", -hw - 0.08, hw + 0.08,
                        -hd - 0.08, hd + 0.08, -0.08, 0.06, jitter=0.008)
    ab.rotate_vertices_z(floor, math.radians(yaw_deg))
    ab.place_godot(floor, x_g, z_g, 0.0)
    ab.assign_material(floor, "AB_timber_dark")
    objects.append(floor)
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
    for side in (-1, 1):
        side_wall = ab.make_box(
            f"{name}_SideWall{'W' if side < 0 else 'E'}",
            side * hw - 0.04,
            side * hw + 0.04,
            -hd,
            hd * 0.66,
            0.08,
            h - 0.22,
        )
        ab.rotate_vertices_z(side_wall, math.radians(yaw_deg))
        ab.place_godot(side_wall, x_g, z_g, 0.0)
        ab.assign_material(side_wall, "AB_timber_dark")
        objects.append(side_wall)
    roof = ab.make_box(f"{name}_Roof", -hw - 0.2, hw + 0.2, -hd - 0.25,
                       hd + 0.25, h - 0.24, h - 0.1)
    ab.rotate_around(roof, 0.2, "x", ab.bbox_center(roof))
    ab.rotate_vertices_z(roof, math.radians(yaw_deg))
    ab.place_godot(roof, x_g, z_g, 0.0)
    ab.assign_material(roof, "AB_roof_shingle")
    objects.append(roof)
    eave = ab.make_box(
        f"{name}_RoofEdge",
        -hw - 0.24,
        hw + 0.24,
        hd + 0.19,
        hd + 0.34,
        h - 0.30,
        h - 0.12,
        jitter=0.004,
    )
    ab.rotate_around(eave, 0.2, "x", ab.bbox_center(eave))
    ab.rotate_vertices_z(eave, math.radians(yaw_deg))
    ab.place_godot(eave, x_g, z_g, 0.0)
    ab.assign_material(eave, "AB_timber_dark")
    objects.append(eave)
    threshold = ab.make_box(
        f"{name}_Threshold",
        -0.48,
        0.48,
        hd * 0.65,
        hd + 0.12,
        0.05,
        0.15,
        jitter=0.004,
    )
    ab.rotate_vertices_z(threshold, math.radians(yaw_deg))
    ab.place_godot(threshold, x_g, z_g, 0.0)
    ab.assign_material(threshold, "AB_stone_dark")
    objects.append(threshold)
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
    objects.extend(eave_details(f"{prefix}_Roof", hw, hd, 2.95))
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
    shed_foundation = wall(
        f"{prefix}_ShedFoundation",
        -1.32,
        1.32,
        -hd - 3.76,
        -hd - 1.08,
        -0.04,
        0.18,
        "AB_stone_dark",
        jitter=0.01,
    )
    objects.append(shed_foundation)
    shed_roof = wall(f"{prefix}_ShedRoof", -1.4, 1.4, -hd - 3.8, -hd - 1.0,
                     1.75, 1.9, "AB_roof_iron_dark")
    ab.rotate_around(shed_roof, 0.12, "x", ab.bbox_center(shed_roof))
    objects.append(shed_roof)
    shed_eave = wall(
        f"{prefix}_ShedEave",
        -1.48,
        1.48,
        -hd - 3.94,
        -hd - 3.76,
        1.68,
        1.84,
        "AB_timber_dark",
        jitter=0.006,
    )
    ab.rotate_around(shed_eave, 0.12, "x", ab.bbox_center(shed_eave))
    objects.append(shed_eave)
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
    objects.append(wall(f"{prefix}_Foundation", -1.78, 1.78, -2.08, 2.08, 0.0, 0.26,
                        "AB_stone_dark", jitter=0.02))
    objects.append(wall(f"{prefix}_Body", -1.6, 1.6, -1.9, 1.9, 0.26, 2.1,
                        "AB_log_wall"))
    hw, hd = 1.6, 1.9
    objects.extend(gable_roof(f"{prefix}_Roof", hw, hd, 2.1, 2.95,
                              "AB_roof_shingle"))
    objects.extend(eave_details(f"{prefix}_Roof", hw, hd, 2.1))
    objects.extend(chimney(prefix, 0.55, 2.85))
    objects.extend(window_unit(f"{prefix}_Win", hd, 0.0, 1.35, w=0.5, h=0.55,
                               glass_material="AB_window_cold"))
    objects.extend(door_unit(f"{prefix}_Door", -hd, 0.5, 0.18, w=0.75, h=1.7))
    objects.append(wall(f"{prefix}_DoorThreshold", 0.0, 0.95, -2.15, -1.90,
                        0.16, 0.28, "AB_stone"))
    for side, tag in ((-1, "W"), (1, "E")):
        post = wall(f"{prefix}_CornerPost{tag}", side * 1.56 - 0.08,
                    side * 1.56 + 0.08, -1.96, -1.78, 0.2, 2.12,
                    "AB_timber_dark", jitter=0.006)
        objects.append(post)
    for obj in objects:
        place(obj, x_g, z_g, yaw_deg)
    objects.extend(woodpile("WoodpileBanya", x_g + 2.6, z_g - 2.1,
                             yaw_deg + 20, rows=2, length=1.6))
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
                               chimney_x=1.1, log_bands=True,
                               body_taper=0.05, body_shift_x=-0.06,
                               ridge_offset=-0.18, roof_overhang=0.40,
                               front_window_bias=0.16,
                               porch_width=1.48, porch_depth=1.58,
                               porch_roof_pitch=-0.17, porch_shift_x=0.10))
    # Yard outbuildings and boundary.
    objects.extend(build_house("ShedBabai", -35.5, -4, 25, w=3.6, d=3.2,
                               wall_h=1.9, pitch=38, wall_mat="AB_timber",
                               roof_mat="AB_roof_shingle", warm_front=0,
                               cold_back=1, body_taper=0.14,
                               body_shift_x=0.08, ridge_offset=0.16,
                               roof_overhang=0.27))
    objects.extend(woodpile("WoodpileBabai", -33.8, -6.6, 10, rows=3))
    objects.extend(lean_to("LeanToBabai", -27.5, -5.4, 95, w=2.4, d=1.7))
    # West fence with a gate gap around z=2.6.
    objects.extend(fence_run("FenceBabaiW_S", [(-25.2, -6.4), (-25.2, 1.75)]))
    objects.extend(fence_run("FenceBabaiW_N", [(-25.2, 3.45), (-25.2, 5.2)]))
    objects.extend(fence_run("FenceBabaiS", [(-25.2, -6.4), (-36.2, -6.4)]))
    objects.extend(fence_run("FenceBabaiN", [(-25.2, 5.2), (-36.2, 5.2)]))
    objects.extend(gate("GateBabai", -25.2, 2.6, 90, open_angle=math.pi / 2))
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
        shape_override = {}
        if name == "HouseA5":
            shape_override = {
                "body_taper": 0.035,
                "body_shift_x": 0.06,
                "ridge_offset": -0.16,
                "roof_overhang": 0.37,
                "front_window_bias": 0.12,
                "porch_width": 1.28,
                "porch_depth": 1.46,
                "porch_roof_pitch": -0.12,
                "porch_shift_x": -0.08,
            }
        objects.extend(build_house(name, x, z, yaw, w=w, d=d, wall_h=wh,
                                   pitch=p, wall_mat=wm, roof_mat=rm,
                                   warm_front=wf, cold_back=cb, has_porch=po,
                                   chimney_x=chx, log_bands=bands,
                                   trim=not bands,
                                   **shape_override))
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
