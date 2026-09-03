"""Agent B Act I Kit 1: authored terrain + wet village road.

All planar coordinates are Blender coordinates: (x, -godot_z).
Heights are Godot heights. Exports agentb_terrain_road_kit.glb + .blend.
"""

from __future__ import annotations

import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import agent_b_common as ab  # noqa: E402

# Layout shared with AgentBAct1Layout.cs (Godot space).
MAIN_AXIS = [(0, 40), (0, 9), (-0.6, -1.5), (-1.2, -8), (0, -19),
             (-1, -30), (0, -41.5), (-0.4, -53.5)]
ZIRAT_AXIS = [(-0.4, -53.5), (0.3, -64), (0, -76), (-0.6, -89.5)]
KARA_AXIS = [(-0.6, -89.5), (0.9, -96), (-0.4, -103), (1.1, -110),
             (0, -117), (0.6, -122.5)]
FAP_AXIS = [(0, -10), (4.5, -12.5), (10, -17), (17, -21.5), (23, -25), (28, -26.2)]
HOUSE_AXIS = [(-1.2, -8), (-6, -5.5), (-12, -2.5), (-19, 0), (-24, 1.2)]

ALL_AXES = MAIN_AXIS + ZIRAT_AXIS[1:] + KARA_AXIS[1:]

# Yard plateaus (Godot space): (x, z, radius)
YARDS = [
    (-30, 0, 9.0),      # babai/ebi yard
    (31, -28, 7.5),     # fap yard
    (6.5, -70, 8.5),    # zirat knoll top
    (-9.5, 0, 4.2),
    (-9, -10, 4.2),
    (8, -2, 4.2),
    (8, -12, 4.2),
    (-9, -32, 4.2),
    (8, -30, 4.2),
    (-9.5, -42, 4.2),
    (8.5, -40, 4.2),
]

AXES_B = [ab.P(x, z) for x, z in ALL_AXES]
_AXES_SAMPLED = None
_AXIS_WIDTHS = None


def _axis_samples():
    global _AXES_SAMPLED, _AXIS_WIDTHS
    if _AXES_SAMPLED is None:
        _AXES_SAMPLED = [
            ab.sample_polyline([ab.P(x, z) for x, z in MAIN_AXIS], 1.0),
            ab.sample_polyline([ab.P(x, z) for x, z in FAP_AXIS], 1.0),
            ab.sample_polyline([ab.P(x, z) for x, z in HOUSE_AXIS], 1.0),
            ab.sample_polyline([ab.P(x, z) for x, z in ZIRAT_AXIS], 1.0),
            ab.sample_polyline([ab.P(x, z) for x, z in KARA_AXIS], 1.0),
        ]
        _AXIS_WIDTHS = (2.8, 2.3, 1.4, 2.1, 1.75)
    return _AXES_SAMPLED, _AXIS_WIDTHS


def _deformed_ribbon(name: str, points: list[tuple[float, float]],
                     profile: list[tuple[float, float]], spacing: float,
                     height_lookup, lateral_wander: float = 0.0,
                     height_wander: float = 0.0, z_pad: float = 0.025) -> object:
    """Build a ground-hugging ribbon with authored, bounded variation.

    The route axes remain unchanged.  Only presentation vertices wander a
    little across the sampled strip, which breaks the long machine-cut edges
    while leaving the existing terrain/collision owner authoritative.
    """
    samples = ab.sample_polyline(points, spacing)
    seed = (ab.stable_hash(name) % 1000) * 0.01
    verts = []
    faces = []
    cols = len(profile)
    reverse_winding = profile[-1][0] < profile[0][0]
    for index, (sx, sy, tx, ty) in enumerate(samples):
        normal_x = -ty
        normal_y = tx
        wander = (ab.value_noise(sx * 0.15 + seed,
                                 sy * 0.15 - seed) - 0.5) * 2.0
        rise = (ab.value_noise(sy * 0.11 - seed,
                               sx * 0.11 + seed) - 0.5) * 2.0
        for lateral, rel_height in profile:
            lateral_offset = lateral + wander * lateral_wander
            x = sx + normal_x * lateral_offset
            y = sy + normal_y * lateral_offset
            base = height_lookup(x, y) if height_lookup else 0.0
            verts.append([x, y, base + rel_height + rise * height_wander + z_pad])
    for row in range(len(samples) - 1):
        for col in range(cols - 1):
            a = row * cols + col
            b = a + 1
            c = a + cols + 1
            d = a + cols
            faces.append((a, b, c, d) if reverse_winding else (a, d, c, b))
    return ab.mesh_from_pydata(name, verts, faces)


def _painterly_road_profile(half_width: float,
                            rut_depth: float = 0.09,
                            crown: float = 0.065) -> list[tuple[float, float]]:
    """Broad, shallow road crown/ruts without knife-straight rails."""
    hw = half_width
    return [
        (-hw, -0.015),
        (-hw * 0.88, -0.008),
        (-hw * 0.72, -0.004),
        (-hw * 0.56, -rut_depth * 0.30),
        (-hw * 0.40, -rut_depth),
        (-hw * 0.19, -rut_depth * 0.35),
        (0.0, crown),
        (hw * 0.19, -rut_depth * 0.35),
        (hw * 0.40, -rut_depth),
        (hw * 0.56, -rut_depth * 0.30),
        (hw * 0.72, -0.004),
        (hw * 0.88, -0.008),
        (hw, -0.015),
    ]


def _field_bank(name: str, points: list[tuple[float, float]], width: float,
                height: float) -> object:
    """Short, broken low-poly bank used to close distant field horizons."""
    profile = [
        (-width, -0.035),
        (-width * 0.56, 0.035),
        (-width * 0.18, height * 0.72),
        (0.0, height),
        (width * 0.22, height * 0.66),
        (width * 0.62, 0.02),
        (width, -0.04),
    ]
    return _deformed_ribbon(name, points, profile, 1.8, h_ground,
                            lateral_wander=0.22, height_wander=0.025)


def _integrated_puddle(name: str, centre: tuple[float, float],
                       radius_x: float, radius_z: float, depth: float,
                       height_lookup) -> tuple[object, object]:
    """Return a wet-earth bed and a smaller irregular water surface.

    The water stays just above the route surface to avoid z-fighting, while
    the faceted bed, uneven rim and inset centre make it read as a shallow
    depression instead of a regular polygon sticker.
    """
    rng = random.Random(ab.stable_hash(name))
    cx, cy = centre
    base = height_lookup(cx, cy) if height_lookup else 0.0
    segments = 12
    phase = rng.uniform(-0.18, 0.18)
    outer = []
    inner = []
    for index in range(segments):
        angle = phase + index / segments * math.tau
        variation = 0.84 + rng.random() * 0.27
        outer.append((
            cx + math.cos(angle) * radius_x * variation * 1.18,
            cy + math.sin(angle) * radius_z * variation * 1.16,
            base + 0.002 + (rng.random() - 0.5) * 0.006,
        ))
        inner.append((
            cx + math.cos(angle) * radius_x * variation * 0.84,
            cy + math.sin(angle) * radius_z * variation * 0.82,
            base + 0.007 + (rng.random() - 0.5) * 0.003,
        ))

    # A shallow faceted bed; depth is intentionally bounded so presentation
    # never turns into a step against the authoritative traversal surface.
    bed_verts = outer + [(cx, cy, base + max(0.0, 0.004 - depth * 0.04))]
    bed_faces = []
    bed_centre = len(bed_verts) - 1
    for index in range(segments):
        bed_faces.append((index, (index + 1) % segments, bed_centre))
    bed = ab.mesh_from_pydata(f"{name}_Bed", bed_verts, bed_faces)

    water_verts = inner + [(cx, cy, base + max(0.004, 0.008 - depth * 0.02))]
    water_faces = []
    water_centre = len(water_verts) - 1
    for index in range(segments):
        water_faces.append((index, (index + 1) % segments, water_centre))
    water = ab.mesh_from_pydata(name, water_verts, water_faces)
    return bed, water


def road_info(xb: float, yb: float) -> tuple[float, float]:
    samples, widths = _axis_samples()
    best = float("inf")
    width = 3.0
    for pts, width in zip(samples, widths):
        d = ab.distance_to_polyline(pts, xb, yb)
        if d < best:
            best = d
    return best, width


def h_terrain(xb: float, yb: float) -> float:
    """Natural terrain height before road flattening (Blender planar coords)."""
    zg = -yb
    x = xb
    h = (ab.fbm(x * 0.03 + 40.0, zg * 0.03, 3) - 0.5) * 1.5
    west = max(0.0, min(1.0, (-12.0 - x) / 34.0))
    h += west * (1.2 + ab.fbm(x * 0.05, zg * 0.05 + 11.0, 2) * 0.9)
    east = max(0.0, min(1.0, (x - 40.0) / 24.0))
    h -= east * 0.7
    kara = max(0.0, min(1.0, (-90.0 - zg) / 20.0))
    h -= kara * 0.55
    h += kara * (ab.fbm(x * 0.16, zg * 0.16, 2) - 0.5) * 0.5
    h += (ab.fbm(x * 0.23, zg * 0.23, 2) - 0.5) * 0.22
    # Zirat knoll.
    dx = x - 6.5
    dz = zg - (-70.0)
    h += math.exp(-(dx * dx + dz * dz) / 60.0) * 0.55
    # Yard plateaus relax toward 0.
    for cx, cz, radius in YARDS:
        d = math.hypot(x - cx, zg - cz)
        if d < radius:
            blend = (math.cos(min(1.0, d / radius) * math.pi) + 1.0) * 0.5 * 0.85
            h *= (1.0 - blend)
        # Zirat plateau raises a little.
    return h


def h_ground(xb: float, yb: float) -> float:
    """Ground height including road flattening; used by ribbons."""
    base = h_terrain(xb, yb)
    d, hw = road_info(xb, yb)
    if d >= hw + 3.0:
        return base
    crown = base - 0.02
    if d <= hw:
        return crown
    blend = 1.0 - (d - hw) / 3.0
    return base * (1.0 - blend) + crown * blend


def build_terrain() -> list:
    objects = []
    min_x, max_x = -64.0, 66.0
    # Match AgentBAct1HeightField's complete reverse-view envelope: Godot z
    # -152..56. The old -44 start left the arrival-side 12 m unsupported.
    min_yb, max_yb = -56.0, 152.0  # godot z 56 -> -152
    step = 2.0
    cols = int((max_x - min_x) / step) + 1
    rows = int((max_yb - min_yb) / step) + 1
    verts = []
    for iy in range(rows):
        yb = min_yb + iy * step
        for ix in range(cols):
            xb = min_x + ix * step
            d, hw = road_info(xb, yb)
            margin = 0.75 if d < hw + 1.2 else 0.22
            jx = (ab.value_noise(xb * 0.9, yb * 0.77) - 0.5) * 2.0 * margin
            jy = (ab.value_noise(yb * 0.9 + 3.1, xb * 0.77) - 0.5) * 2.0 * margin
            xj = xb + jx
            yj = yb + jy
            verts.append([xj, yj, h_ground(xj, yj)])
    faces = []
    for iy in range(rows - 1):
        for ix in range(cols - 1):
            a = iy * cols + ix
            faces.append((a, a + 1, a + cols + 1, a + cols))
    terrain = ab.mesh_from_pydata("Terrain_Main", verts, faces)
    for polygon in terrain.data.polygons:
        polygon.use_smooth = False
    ab.assign_material(terrain, "AB_grass")
    objects.append(terrain)

    # Dirt aprons in yards.
    for name, cx, cz, size in (("Apron_BabaiYard", -30, 0, 15.0),
                               ("Apron_FapYard", 31, -28, 12.0)):
        patch = ab.make_patch(name, (cx, -cz), size, size, h_ground,
                              z_pad=0.012, segments=6)
        ab.assign_material(patch, "AB_earth_path")
        objects.append(patch)

    # Broken shoulder banks: a low, irregular transition from road to field
    # reads as lived-in drainage instead of two straight decorative rails.
    for name, side in (("MudStrip_West", -1), ("MudStrip_East", 1)):
        strip = _deformed_ribbon(
            name, [ab.P(x, z) for x, z in MAIN_AXIS],
            [(side * 3.0, -0.075), (side * 3.5, -0.015),
             (side * 4.0, 0.09), (side * 4.65, 0.13),
             (side * 5.35, 0.035), (side * 6.15, -0.07)],
            spacing=1.8, height_lookup=h_ground,
            lateral_wander=0.24, height_wander=0.025)
        ab.assign_material(strip, "AB_earth_wet")
        objects.append(strip)

    # Short field-bank sections keep the horizon authored without turning the
    # whole perimeter into a continuous wall. They are presentation-only;
    # terrain/collision remains the existing height-field owner.
    field_banks = [
        ("Ditch_FieldBank_WestArrival",
         [(-47.0, -51.0), (-42.0, -45.0), (-46.0, -38.0)], 3.8, 0.18),
        ("Ditch_FieldBank_EastArrival",
         [(44.0, -50.0), (50.0, -43.0), (45.0, -36.0)], 3.6, 0.16),
        ("Ditch_FieldBank_WestStreet",
         [(-47.0, 2.0), (-51.0, 10.0), (-46.0, 18.0)], 3.5, 0.15),
        ("Ditch_FieldBank_EastStreet",
         [(45.0, 4.0), (51.0, 12.0), (46.0, 20.0)], 3.6, 0.17),
        ("Ditch_FieldBank_WestZirat",
         [(-34.0, 58.0), (-40.0, 66.0), (-35.0, 74.0)], 3.3, 0.14),
        ("Ditch_FieldBank_EastZirat",
         [(34.0, 58.0), (39.0, 66.0), (34.0, 74.0)], 3.2, 0.15),
    ]
    for name, points, width, height in field_banks:
        bank = _field_bank(name, points, width, height)
        ab.assign_material(bank, "AB_earth")
        objects.append(bank)
    return objects


def build_roads() -> list:
    objects = []
    main_b = [ab.P(x, z) for x, z in MAIN_AXIS + ZIRAT_AXIS[1:]]
    kara_b = [ab.P(x, z) for x, z in KARA_AXIS]
    fap_b = [ab.P(x, z) for x, z in FAP_AXIS]
    house_b = [ab.P(x, z) for x, z in HOUSE_AXIS]

    road = _deformed_ribbon("Road_Main", main_b,
                            _painterly_road_profile(2.8, 0.045, 0.065),
                            spacing=1.0, height_lookup=h_ground,
                            lateral_wander=0.06, height_wander=0.012)
    ab.assign_material(road, "AB_road_crown")
    objects.append(road)

    fap_road = _deformed_ribbon("Road_FapBranch", fap_b,
                                _painterly_road_profile(2.3, 0.04, 0.06),
                                spacing=1.0, height_lookup=h_ground,
                                lateral_wander=0.05, height_wander=0.01)
    ab.assign_material(fap_road, "AB_road_crown")
    objects.append(fap_road)

    house_path = _deformed_ribbon("Road_HousePath", house_b,
                                  _painterly_road_profile(1.5, 0.03, 0.05),
                                  spacing=1.0, height_lookup=h_ground,
                                  lateral_wander=0.04, height_wander=0.008)
    ab.assign_material(house_path, "AB_earth_path")
    objects.append(house_path)

    kara_path = _deformed_ribbon("Road_KaraPath", kara_b,
                                 _painterly_road_profile(1.7, 0.035, 0.045),
                                 spacing=1.0, height_lookup=h_ground,
                                 lateral_wander=0.05, height_wander=0.01)
    ab.assign_material(kara_path, "AB_road_kara")
    objects.append(kara_path)

    # Broad, gently wandering wheel ruts. Their shallow overlap keeps the
    # road legible in a turn without creating two continuous black rails.
    for side, name in ((-1, "Rut_West"), (1, "Rut_East")):
        rut = _deformed_ribbon(
            name, main_b,
            [(side * 0.44, -0.006), (side * 0.63, -0.020),
             (side * 0.86, -0.028), (side * 1.10, -0.008)],
            spacing=1.3, height_lookup=h_ground,
            lateral_wander=0.11, height_wander=0.012)
        ab.assign_material(rut, "AB_road_rut")
        objects.append(rut)

    # Side ditches along main road and fap branch.
    for side, name in ((-1, "Ditch_West"), (1, "Ditch_East")):
        ditch = _deformed_ribbon(
            name, main_b,
            [(side * 2.95, -0.006), (side * 3.45, -0.028),
             (side * 3.95, -0.042), (side * 4.55, -0.016),
             (side * 5.05, -0.004)],
            spacing=1.4, height_lookup=h_ground,
            lateral_wander=0.18, height_wander=0.022)
        ab.assign_material(ditch, "AB_earth_wet")
        objects.append(ditch)
    ditch_fap = _deformed_ribbon(
        "Ditch_FapNorth", fap_b,
        [(-2.55, -0.006), (-3.15, -0.028), (-3.7, -0.042),
         (-4.3, -0.016), (-4.85, -0.004)],
        spacing=1.5, height_lookup=h_ground,
        lateral_wander=0.18, height_wander=0.02)
    ab.assign_material(ditch_fap, "AB_earth_wet")
    objects.append(ditch_fap)

    # Ford stones where the FAP branch leaves the main road.
    for i in range(6):
        stone = ab.make_box(f"FordStone_{i}", -0.45, 0.45, -0.3, 0.3, 0.0,
                            0.2, jitter=0.05)
        ab.place_godot(stone, 2.2 + i * 0.55, 10.4,
                       h_ground(2.2 + i * 0.55, -(-10.4)) - 0.02)
        stone.rotation_euler.z = (i % 3) * 0.14 - 0.12
        ab.assign_material(stone, "AB_stone")
        objects.append(stone)

    # Puddles in ruts and yards (Blender coords).
    puddle_defs = [
        ("Puddle_0", (0.75, -3.0), 0.55, 0.85),
        ("Puddle_1", (-0.95, 6.0), 0.45, 0.7),
        ("Puddle_2", (0.8, 17.5), 0.6, 1.0),
        ("Puddle_3", (-0.9, 28.0), 0.5, 0.75),
        ("Puddle_4", (0.4, 45.0), 0.55, 0.9),
        ("Puddle_5", (0.2, 62.0), 0.45, 0.7),
        ("Puddle_6", (-0.5, 98.0), 0.5, 0.8),
        ("Puddle_7", (-22.0, -0.4), 0.7, 1.1),
        ("Puddle_8", (26.5, 25.6), 0.6, 0.9),
        ("Puddle_9", (3.0, 11.4), 0.5, 0.8),
    ]
    for name, centre, rx, rz in puddle_defs:
        bed, puddle = _integrated_puddle(name, centre, rx, rz, 0.05,
                                          h_ground)
        ab.assign_material(bed, "AB_earth_wet")
        ab.assign_material(puddle, "AB_water_dark")
        objects.extend((bed, puddle))
    return objects


def main() -> None:
    ab.setup_scene()
    objects = build_terrain() + build_roads()
    root = os.environ.get("AGENTB_OUT", HERE)
    glb_path = os.path.join(root, "game/assets/models/agent_b_act1",
                            "agentb_terrain_road_kit.glb")
    blend_path = os.path.join(root, "assets/source/blender/agent_b_act1",
                              "agentb_terrain_road_kit.blend")
    ab.export_glb(objects, glb_path, "URMAN_AgentB_TerrainRoadKit")
    ab.save_blend(blend_path)
    ab.report_kit("agentb_terrain_road_kit", objects, glb_path)


if __name__ == "__main__":
    main()
