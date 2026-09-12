"""Agent B Act I Kit 1: authored terrain + wet village road.

All planar coordinates are Blender coordinates: (x, -godot_z).
Heights are Godot heights. Exports agentb_terrain_road_kit.glb + .blend.
"""

from __future__ import annotations

import math
import hashlib
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

ROAD_MAIN_HALF_WIDTH = 2.0
ROAD_WHEEL_CENTER_SEPARATION = 1.56
ROAD_RUT_WIDTH = 0.28
ROAD_MAIN_RUT_DEPTH = 0.03
ROAD_FAP_RUT_DEPTH = 0.022
ROAD_ZIRAT_RUT_DEPTH = 0.02
ROAD_SURFACE_MAX_ABOVE_GROUND = 0.045
ZIRAT_START_BLENDER_Y = -ZIRAT_AXIS[0][1]

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
                     height_wander: float = 0.0, z_pad: float = 0.025,
                     edge_wander: float = 0.0,
                     profile_height_modifier=None,
                     smooth_normals: bool = False,
                     max_surface_above_ground: float | None = None,
                     end_fade_length: float = 0.0,
                     end_width_ratio: float = 1.0,
                     end_height_ratio: float = 0.0) -> object:
    """Build a ground-hugging ribbon with authored, bounded variation.

    The route axes remain unchanged.  Only presentation vertices wander a
    little across the sampled strip, which breaks the long machine-cut edges
    while leaving the existing terrain/collision owner authoritative.
    """
    samples = ab.sample_polyline(points, spacing)
    seed = (ab.stable_hash(name) % 1000) * 0.01
    assert end_fade_length >= 0.0
    assert 0.0 < end_width_ratio <= 1.0
    assert 0.0 <= end_height_ratio <= 1.0
    path_length = sum(
        math.hypot(points[index + 1][0] - points[index][0],
                   points[index + 1][1] - points[index][1])
        for index in range(len(points) - 1)
    )
    verts = []
    faces = []
    cols = len(profile)
    profile_min = min(profile[0][0], profile[-1][0])
    profile_max = max(profile[0][0], profile[-1][0])
    reverse_winding = profile[-1][0] < profile[0][0]
    for index, (sx, sy, tx, ty) in enumerate(samples):
        end_blend = 0.0
        if end_fade_length > 0.0:
            remaining = path_length * (1.0 - index / max(len(samples) - 1, 1))
            linear = 1.0 - min(1.0, remaining / end_fade_length)
            end_blend = linear * linear * (3.0 - 2.0 * linear)
        lateral_scale = 1.0 - (1.0 - end_width_ratio) * end_blend
        height_scale = end_height_ratio + (1.0 - end_height_ratio) * (1.0 - end_blend)
        normal_x = -ty
        normal_y = tx
        wander = (ab.value_noise(sx * 0.15 + seed,
                                 sy * 0.15 - seed) - 0.5) * 2.0
        rise = (ab.value_noise(sy * 0.11 - seed,
                               sx * 0.11 + seed) - 0.5) * 2.0
        for lateral, rel_height in profile:
            if profile_height_modifier is not None:
                rel_height = profile_height_modifier(sx, sy, lateral, rel_height)
            lateral_offset = lateral + wander * lateral_wander
            if edge_wander:
                # Keep the centreline stable while the two shoulders break
                # independently. Clamp to the declared profile envelope so
                # presentation never grows into the protected route corridor.
                edge_ratio = min(
                    1.0,
                    abs(lateral) / max(abs(profile_min), abs(profile_max), 1e-6),
                )
                edge_seed = seed + (17.3 if lateral < 0.0 else -23.7)
                edge_noise = (ab.value_noise(
                    sx * 0.10 + edge_seed,
                    sy * 0.10 - edge_seed,
                ) - 0.5) * 2.0
                lateral_offset += edge_noise * edge_wander * edge_ratio * edge_ratio
            lateral_offset = max(profile_min, min(profile_max, lateral_offset))
            lateral_offset *= lateral_scale
            x = sx + normal_x * lateral_offset
            y = sy + normal_y * lateral_offset
            base = height_lookup(x, y) if height_lookup else 0.0
            verts.append([x, y,
                          base + (rel_height + rise * height_wander) * height_scale + z_pad])
    for row in range(len(samples) - 1):
        for col in range(cols - 1):
            a = row * cols + col
            b = a + 1
            c = a + cols + 1
            d = a + cols
            faces.append((a, b, c, d) if reverse_winding else (a, d, c, b))

    if max_surface_above_ground is not None:
        assert height_lookup is not None, f"{name} requires a ground lookup for height bounds"
        max_above_ground = max(
            vertex[2] - height_lookup(vertex[0], vertex[1])
            for vertex in verts
        )
        assert max_above_ground <= max_surface_above_ground + 1e-6, (
            f"{name} surface exceeds {max_surface_above_ground:.3f}m above h_ground: "
            f"{max_above_ground:.4f}m"
        )

    obj = ab.mesh_from_pydata(name, verts, faces)
    if smooth_normals:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    return obj


def _rut_shape(lateral: float) -> float:
    distance = abs(abs(lateral) - ROAD_WHEEL_CENTER_SEPARATION * 0.5)
    normalized = max(0.0, min(1.0, 1.0 - distance / (ROAD_RUT_WIDTH * 0.5)))
    return 0.5 - 0.5 * math.cos(math.pi * normalized)


def _road_surface_height(lateral: float, half_width: float, crown: float) -> float:
    edge = max(0.0, min(1.0, (abs(lateral) / half_width - 0.68) / 0.32))
    edge = edge * edge * (3.0 - 2.0 * edge)
    return crown - edge * 0.018


def _painterly_road_profile(half_width: float,
                            rut_depth: float = 0.03,
                            crown: float = 0.034,
                            vehicle_ruts: bool = True) -> list[tuple[float, float]]:
    """Low, rounded presentation profile over the unchanged height field.

    Vehicle profiles have two fine, rounded wheel depressions. Footpaths use
    the same seated shoulder language without vehicle ruts.
    """
    assert half_width > 0.0
    assert 0.0 <= rut_depth <= 0.05
    assert 0.0 < crown <= ROAD_SURFACE_MAX_ABOVE_GROUND
    if vehicle_ruts:
        assert 1.45 <= ROAD_WHEEL_CENTER_SEPARATION <= 1.65
        assert 0.22 <= ROAD_RUT_WIDTH <= 0.35
        assert 0.02 <= rut_depth <= 0.05
        centre = ROAD_WHEEL_CENTER_SEPARATION * 0.5
        rut_half_width = ROAD_RUT_WIDTH * 0.5
        lateral_points = [
            -half_width, -half_width * 0.94, -half_width * 0.84,
            -centre - rut_half_width, -centre - 0.10, -centre - 0.06,
            -centre - 0.03, -centre, -centre + 0.03, -centre + 0.06,
            -centre + 0.10, -centre + rut_half_width,
            -0.48, -0.24, 0.0, 0.24, 0.48,
            centre - rut_half_width, centre - 0.10, centre - 0.06,
            centre - 0.03, centre, centre + 0.03, centre + 0.06,
            centre + 0.10, centre + rut_half_width,
            half_width * 0.84, half_width * 0.94, half_width,
        ]
    else:
        lateral_points = [
            -half_width, -half_width * 0.92, -half_width * 0.72,
            -half_width * 0.48, 0.0, half_width * 0.48,
            half_width * 0.72, half_width * 0.92, half_width,
        ]

    hw = half_width
    profile = []
    for lateral in lateral_points:
        height = _road_surface_height(lateral, hw, crown)
        if vehicle_ruts:
            height -= rut_depth * _rut_shape(lateral)
        profile.append((lateral, height))

    assert len(profile) == (29 if vehicle_ruts else 9)
    assert profile[0][0] == -half_width and profile[-1][0] == half_width
    assert all(profile[index][0] < profile[index + 1][0]
               for index in range(len(profile) - 1))
    assert max(height for _, height in profile) <= ROAD_SURFACE_MAX_ABOVE_GROUND
    if vehicle_ruts:
        assert math.isclose(2.0 * rut_half_width, ROAD_RUT_WIDTH)
        for centre in (-ROAD_WHEEL_CENTER_SEPARATION * 0.5,
                       ROAD_WHEEL_CENTER_SEPARATION * 0.5):
            centre_height = next(height for lateral, height in profile
                                 if math.isclose(lateral, centre))
            assert math.isclose(centre_height, crown - rut_depth)
    return profile


def _forest_floor_weight(x: float, y: float) -> float:
    field = ab.fbm(x * 0.035 - 13.0, y * 0.035 + 19.0, 2)
    weight = max(0.0, min(1.0, (y - 96.0 + (field - 0.5) * 8.0) / 18.0))
    return weight * weight * (3.0 - 2.0 * weight)


def _assign_road_materials(obj: object, profile: list[tuple[float, float]],
                           crown_material: str = "AB_road_crown",
                           vehicle_ruts: bool = True) -> None:
    """Paint worn tracks continuously, not as hard parallel material bands."""
    import bpy

    obj.data.materials.clear()
    paint = bpy.data.materials.get("AB_road_pigment")
    if paint is None:
        paint = ab.mat(crown_material).copy()
        paint.name = "AB_road_pigment"
        color_node = paint.node_tree.nodes.new("ShaderNodeVertexColor")
        color_node.layer_name = "RoadPigment"
        paint.node_tree.links.new(color_node.outputs["Color"],
            paint.node_tree.nodes.get("Principled BSDF").inputs["Base Color"])
    obj.data.materials.append(paint)
    pigment = obj.data.color_attributes.new(name="RoadPigment", type="FLOAT_COLOR", domain="POINT")
    obj.data.color_attributes.active_color = pigment
    assert len(obj.data.vertices) % len(profile) == 0, "road vertex/profile alignment changed"
    for index, vertex in enumerate(obj.data.vertices):
        x, y, _ = vertex.co
        normalizedrut = abs(profile[index % len(profile)][0]) / max(abs(profile[0][0]), 1e-6)
        noise = ab.value_noise(x * 0.65 + 9.0, y * 0.65)
        if vehicle_ruts:
            rut_centre = ROAD_WHEEL_CENTER_SEPARATION * 0.5 / abs(profile[0][0])
            rut_half_width = ROAD_RUT_WIDTH * 0.5 / abs(profile[0][0])
            rut_wear = math.exp(-((normalizedrut - rut_centre)
                                   / max(rut_half_width * 1.25, 0.01)) ** 2)
            packed = rut_wear * (0.58 + 0.42 * noise)
        else:
            packed = 0.0
        edge = max(0.0, min(1.0, (normalizedrut - 0.78) / 0.22))
        surface = (0.84, 0.85, 0.86)
        packed_surface = (0.66, 0.68, 0.70)
        shoulder = (0.90, 0.91, 0.92)
        rgb = [surface[c] * (1.0 - packed) + packed_surface[c] * packed
               for c in range(3)]
        rgb = [value * (1.0 - edge * 0.25) + shoulder[c] * edge * 0.25
               for c, value in enumerate(rgb)]
        variation = (noise - 0.5) * 0.025
        rgb = [max(0.0, min(1.0, value + variation)) for value in rgb]
        pigment.data[index].color = tuple(
            c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
            for c in rgb) + (1.0,)


def _field_bank(name: str, points: list[tuple[float, float]], width: float,
                height: float) -> object:
    """Short, broken low-poly bank used to close distant field horizons.

    The old ribbon was a single paper-thin polygon strip.  Keep its public
    name, but give it a shallow shoulder/crest/shoulder section and closed end
    caps so the horizon cue has contact and a readable silhouette.
    """
    return _low_bank(
        name,
        points,
        width,
        height * 1.12,
        "AB_earth",
        spacing=2.35,
        lateral_wander=0.20,
        height_wander=0.035,
    )


def _low_bank(
    name: str,
    points: list[tuple[float, float]],
    width: float,
    crest_height: float,
    material_name: str,
    spacing: float = 2.2,
    lateral_wander: float = 0.12,
    height_wander: float = 0.022,
) -> object:
    """Build one ground-seated, sloped bank instead of a flat overlay.

    The cross-section is deliberately broad and low: it closes a field edge
    from the first-person horizon without becoming a wall or changing the
    authoritative collision heightfield.
    """
    samples = ab.sample_polyline(points, spacing)
    profile = (
        (-width, 0.0),
        (-width * 0.58, crest_height * 0.40),
        (-width * 0.14, crest_height * 0.88),
        (width * 0.24, crest_height),
        (width * 0.68, crest_height * 0.48),
        (width, 0.0),
    )
    seed = (ab.stable_hash(name) % 1000) * 0.01
    vertices = []
    for row, (sx, sy, tx, ty) in enumerate(samples):
        normal_x = -ty
        normal_y = tx
        lateral_noise = (ab.value_noise(sx * 0.09 + seed, sy * 0.09 - seed) - 0.5) * 2.0
        rise_noise = (ab.value_noise(sy * 0.08 - seed, sx * 0.08 + seed) - 0.5) * 2.0
        for col, (lateral, rel_height) in enumerate(profile):
            edge_ratio = min(1.0, abs(lateral) / max(width, 0.001))
            offset = lateral + lateral_noise * lateral_wander * edge_ratio**1.45
            x = sx + normal_x * offset
            y = sy + normal_y * offset
            base = h_ground(x, y)
            height = rel_height + rise_noise * height_wander * (0.25 + rel_height / max(crest_height, 0.001))
            vertices.append([x, y, base + height])
    faces = []
    cols = len(profile)
    for row in range(len(samples) - 1):
        for col in range(cols - 1):
            a = row * cols + col
            faces.append((a, a + 1, a + cols + 1, a + cols))
    # Close the two ends so the bank reads as a shallow solid at oblique views.
    faces.append(tuple(reversed(range(cols))))
    last = (len(samples) - 1) * cols
    faces.append(tuple(last + col for col in range(cols)))
    bank = ab.mesh_from_pydata(name, vertices, faces)
    ab.assign_material(bank, material_name)
    return bank


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
    segments = 10 + ab.stable_hash(name) % 5
    phase = rng.uniform(-0.35, 0.35)
    rotation = rng.uniform(-0.32, 0.32)
    cos_rotation = math.cos(rotation)
    sin_rotation = math.sin(rotation)

    def ellipse_point(angle: float, radius_scale: float,
                      z_height: float) -> list[float]:
        local_x = math.cos(angle) * radius_x * radius_scale
        local_y = math.sin(angle) * radius_z * radius_scale
        return [
            cx + local_x * cos_rotation - local_y * sin_rotation,
            cy + local_x * sin_rotation + local_y * cos_rotation,
            z_height,
        ]

    outer = []
    inner = []
    for index in range(segments):
        angle = phase + index / segments * math.tau
        variation = 0.84 + rng.random() * 0.27
        outer.append(ellipse_point(
            angle,
            variation * 1.18,
            base + 0.018 + (rng.random() - 0.5) * 0.008,
        ))
        inner.append(ellipse_point(
            angle,
            variation * 0.68,
            base + 0.004 + (rng.random() - 0.5) * 0.003,
        ))

    # A shallow faceted bed; the raised irregular rim gives the water a seat
    # without changing the authoritative traversal surface underneath.
    bed_verts = outer + inner + [[cx, cy, base + max(0.002, 0.006 - depth * 0.08)]]
    bed_faces = []
    bed_centre = len(bed_verts) - 1
    for index in range(segments):
        next_index = (index + 1) % segments
        bed_faces.append((index, next_index, segments + next_index, segments + index))
        bed_faces.append((segments + index, segments + next_index, bed_centre))
    bed = ab.mesh_from_pydata(f"{name}_Bed", bed_verts, bed_faces)

    water_verts = [
        [point[0], point[1], base + 0.008 + (rng.random() - 0.5) * 0.002]
        for point in inner
    ] + [[cx, cy, base + 0.010]]
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
    for pts, axis_width in zip(samples, widths):
        d = ab.distance_to_polyline(pts, xb, yb)
        if d < best:
            best = d
            width = axis_width
    return best, width


def reverse_field_rise(x: float, z: float) -> float:
    """Broad rear watershed closes the horizon beyond the playable arrival."""
    along = max(0.0, min(1.0, (z - 52.0) / 52.0))
    along = along * along * (3.0 - 2.0 * along)
    centre = -8.0 + (z - 52.0) * 0.10
    lateral = max(0.0, min(1.0, 1.0 - abs(x - centre) / 52.0))
    lateral = lateral * lateral * (3.0 - 2.0 * lateral)
    variation = ab.fbm(x * 0.035 + 19.0, z * 0.035 - 7.0, 2)
    return along * (0.65 + 0.35 * lateral) * (5.0 + 3.0 * variation)


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
    # Low forest shoulders belong to the walkable terrain, not stacked kit
    # wedges. Keep the complete central route strip at its existing height.
    if zg < -90.0:
        edge = max(0.0, min(1.0, (abs(x) - 3.0) / 3.0))
        edge = edge * edge * (3.0 - 2.0 * edge)
        approach = max(0.0, min(1.0, (-zg - 90.0) / 8.0))
        approach = approach * approach * (3.0 - 2.0 * approach)
        for cx, cz, rx, rz, rise in (
            (-8.0, -107.0, 5.5, 8.0, 1.1), (9.5, -113.0, 6.5, 9.0, 1.35),
            (-11.0, -121.0, 7.0, 10.0, 1.6), (12.0, -129.0, 8.0, 10.0, 1.4),
        ):
            h += edge * approach * rise * math.exp(-((x-cx)/rx)**2 - ((zg-cz)/rz)**2)
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
    h += reverse_field_rise(x, zg)
    # Watershed shoulders outside every authored yard and the final walk
    # endpoint. Their broad grades replace the exposed flat map perimeter.
    west_rim = max(0.0, min(1.0, (-x - 40.0) / 24.0))
    east_rim = max(0.0, min(1.0, (x - 44.0) / 22.0))
    forest_rim = max(0.0, min(1.0, (-zg - 128.0) / 24.0))
    for rim, phase in ((west_rim, 0.7), (east_rim, 2.1), (forest_rim, 4.3)):
        grade = rim * rim * (3.0 - 2.0 * rim)
        h += grade * (5.5 + 1.3 * math.sin(zg * 0.055 + x * 0.035 + phase))
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
    # -152..104. The old -56 cap left no low grade beyond the reverse view.
    min_yb, max_yb = -104.0, 152.0  # godot z 104 -> -152
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
    # Keep the collider-aligned heightfield as one continuous mesh, but let
    # broad low-frequency material bands describe wet road shoulders and
    # maintained/unmaintained parcels.  The previous per-cell thresholding
    # produced a patchwork of rectangular colour islands at route distance.
    # Paint shared vertices rather than whole 2 m cells. The same ground
    # geometry/collision now carries continuous verge-to-road transitions.
    terrain.data.materials.clear()
    terrain_material = ab.mat("AB_earth").copy()
    terrain_material.name = "AB_terrain"
    terrain.data.materials.append(terrain_material)
    pigment = terrain.data.color_attributes.new(name="GroundPigment", type="FLOAT_COLOR", domain="POINT")
    terrain.data.color_attributes.active_color = pigment
    vertex_color = terrain_material.node_tree.nodes.new("ShaderNodeVertexColor")
    vertex_color.layer_name = pigment.name
    terrain_material.node_tree.links.new(
        vertex_color.outputs["Color"],
        terrain_material.node_tree.nodes.get("Principled BSDF").inputs["Base Color"],
    )
    for vertex in terrain.data.vertices:
        x, y = vertex.co.x, vertex.co.y
        distance, half_width = road_info(x, y)
        field = ab.fbm(x * 0.035 - 13.0, y * 0.035 + 19.0, 2)
        shoulder = max(0.0, min(1.0, (distance - half_width + 0.3) / 3.6))
        shoulder = shoulder * shoulder * (3.0 - 2.0 * shoulder)
        grass = (0.34 + field * 0.12, 0.39 + field * 0.10, 0.29 + field * 0.08)
        earth = (0.337, 0.388, 0.329)
        # The village meadow gives way to damp needle/litter soil under the
        # forest canopy. Paint this on the existing ground, not floating tiles.
        forest = _forest_floor_weight(x, y)
        litter = (0.36 + field * 0.09, 0.345 + field * 0.075, 0.28 + field * 0.065)
        grass = tuple(a + (b - a) * forest for a, b in zip(grass, litter))
        earth = tuple(a + (b - a) * forest for a, b in zip(earth, (0.36, 0.35, 0.31)))
        srgb = tuple(a + (b - a) * shoulder for a, b in zip(earth, grass))
        pigment.data[vertex.index].color = tuple(
            value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4
            for value in srgb
        ) + (1.0,)
    objects.append(terrain)

    # Compact, irregular yard aprons retain their public names but stop
    # reading as two detached square slabs around the route.
    for name, cx, cz, size in (("Apron_BabaiYard", -30, 0, 15.0),
                               ("Apron_FapYard", 31, -28, 12.0)):
        patch = ab.make_patch(name, (cx, -cz), size * 0.64, size * 0.58,
                              h_ground, z_pad=0.014, segments=4)
        for vertex in patch.data.vertices:
            nx = (vertex.co.x - cx) / max(size * 0.32, 1e-6)
            ny = (vertex.co.y + cz) / max(size * 0.29, 1e-6)
            vertex.co.z += nx * 0.030 + ny * 0.020
        patch.data.update()
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
         [(-50.0, -55.0), (-46.0, -51.0), (-43.0, -46.0),
          (-47.0, -40.0), (-44.0, -35.0)], 5.0, 0.42),
        ("Ditch_FieldBank_EastArrival",
         [(48.0, -54.0), (44.0, -49.0), (51.0, -44.0),
          (46.0, -39.0), (49.0, -34.0)], 4.7, 0.36),
        ("Ditch_FieldBank_WestStreet",
         [(-48.0, -1.0), (-52.0, 4.0), (-47.0, 9.0),
          (-51.0, 14.0), (-46.0, 20.0)], 4.8, 0.38),
        ("Ditch_FieldBank_EastStreet",
         [(47.0, -1.0), (52.0, 5.0), (46.0, 10.0),
          (51.0, 15.0), (47.0, 21.0)], 4.9, 0.40),
        ("Ditch_FieldBank_WestZirat",
         [(-36.0, 54.0), (-42.0, 59.0), (-37.0, 65.0),
          (-41.0, 71.0), (-35.0, 77.0)], 4.5, 0.34),
        ("Ditch_FieldBank_EastZirat",
         [(36.0, 54.0), (42.0, 60.0), (35.0, 66.0),
          (40.0, 72.0), (35.0, 78.0)], 4.4, 0.38),
    ]
    for name, points, width, height in field_banks:
        bank = _field_bank(name, points, width, height)
        ab.assign_material(bank, "AB_earth")
        objects.append(bank)

    # Broad parcel grades are authored as named, presentation-only landforms
    # outside the declared route envelopes. Their foot follows h_ground for a
    # seamless contact edge; only the off-route crest rises, so the runtime
    # heightfield and every walkable centreline remain authoritative.
    parcel_grades = [
        ("Grade_Arrival_West",
         [(-14.0, 16.0), (-16.5, 24.0), (-14.5, 32.0),
          (-18.0, 40.0), (-15.0, 49.0)], 3.7, 0.30, "AB_grass_dry"),
        ("Grade_Arrival_East",
         [(14.0, 18.0), (17.0, 25.0), (15.0, 33.0),
          (19.0, 41.0), (16.0, 50.0)], 3.4, 0.26, "AB_sedge"),
        ("Grade_Street_West",
         [(-14.0, -3.0), (-17.0, -12.0), (-15.0, -21.0),
          (-18.0, -31.0), (-16.0, -42.0)], 3.2, 0.24, "AB_earth"),
        ("Grade_Street_East",
         [(14.0, -5.0), (17.0, -14.0), (15.0, -24.0),
          (18.5, -34.0), (16.5, -44.0)], 3.5, 0.28, "AB_grass_dry"),
        ("Grade_Babai_West",
         [(-40.0, -11.0), (-43.0, -5.0), (-41.0, 2.0),
          (-45.0, 9.0), (-42.0, 15.0)], 3.3, 0.32, "AB_earth_wet"),
        ("Grade_Fap_East",
         [(37.0, -39.0), (40.0, -33.0), (38.0, -27.0),
          (43.0, -21.0), (40.0, -16.0)], 3.5, 0.30, "AB_sedge"),
        ("Grade_Zirat_West",
         [(-16.0, -56.0), (-20.0, -64.0), (-18.0, -72.0),
          (-22.0, -80.0), (-19.0, -88.0)], 3.9, 0.38, "AB_moss"),
        ("Grade_Zirat_East",
         [(16.0, -57.0), (20.0, -65.0), (18.0, -73.0),
          (22.0, -81.0), (19.0, -89.0)], 3.6, 0.34, "AB_grass_dry"),
        ("Grade_Kara_West",
         [(-14.0, -93.0), (-18.0, -101.0), (-16.0, -110.0),
          (-20.0, -119.0), (-17.0, -128.0)], 4.1, 0.46, "AB_moss"),
        ("Grade_Kara_East",
         [(14.0, -94.0), (18.0, -102.0), (16.0, -111.0),
          (20.0, -120.0), (17.0, -128.0)], 3.8, 0.42, "AB_earth_wet"),
    ]
    for name, points, width, crest, material in parcel_grades:
        grade = _low_bank(
            name,
            [ab.P(x, z) for x, z in points],
            width,
            crest,
            material,
            spacing=2.5,
            lateral_wander=0.16,
            height_wander=0.024,
        )
        objects.append(grade)

    # Babai/әби yard ground-contact pass: a few broad domestic landforms tie
    # the authored house, depth well and neighboring parcels together without
    # touching the route center or the authoritative heightfield.
    yard_banks = [
        (
            "BabaiYard_WestDepthBank",
            [(-37.8, 3.0), (-36.5, 6.0), (-33.5, 8.4), (-30.8, 9.5)],
            2.0,
            0.20,
            "AB_earth_wet",
        ),
        (
            "BabaiYard_EastFenceSwale",
            [(-16.0, 7.5), (-13.0, 10.0), (-10.0, 12.0), (-8.0, 15.0)],
            1.7,
            0.15,
            "AB_sedge",
        ),
    ]
    for name, points, width, crest, material in yard_banks:
        bank = _low_bank(
            name,
            [ab.P(x, z) for x, z in points],
            width,
            crest,
            material,
            spacing=1.55,
            lateral_wander=0.18,
            height_wander=0.020,
        )
        objects.append(bank)

    # A worn apron on the house-facing east side carries the existing path
    # vocabulary toward the porch while keeping the approach corridor open.
    worked_apron = ab.make_patch(
        "BabaiYard_HouseWorkedApron",
        (-22.2, -4.8),
        5.4,
        4.3,
        h_ground,
        z_pad=0.024,
        segments=5,
    )
    for vertex in worked_apron.data.vertices:
        nx = (vertex.co.x + 22.2) / 2.7
        ny = (vertex.co.y + 4.8) / 2.15
        vertex.co.x += 0.10 * ny + 0.04 * nx * ny
        vertex.co.y += 0.08 * nx - 0.03 * ny * ny
        vertex.co.z += 0.012 * (1.0 - min(1.0, abs(nx)))
    worked_apron.data.update()
    ab.assign_material(worked_apron, "AB_earth_path")
    objects.append(worked_apron)

    # One shallow wet pocket by the depth well supplies a domestic rain cue;
    # both meshes remain visual overlays seated from h_ground.
    wet_bed, wet_water = _integrated_puddle(
        "BabaiYard_WellWetPocket",
        (-22.0, -16.2),
        1.45,
        1.05,
        0.05,
        h_ground,
    )
    ab.assign_material(wet_bed, "AB_earth_wet")
    ab.assign_material(wet_water, "AB_water_dark")
    objects.extend((wet_bed, wet_water))
    return objects


def _broken_rut_strength(x: float, y: float, threshold: float,
                         seed: float) -> float:
    noise = ab.value_noise(x * 0.075 + seed, y * 0.075 - seed)
    upper_threshold = min(1.0, threshold + 0.15)
    normalized = max(
        0.0,
        min(1.0, (noise - threshold) / max(upper_threshold - threshold, 1e-6)),
    )
    return normalized * normalized * (3.0 - 2.0 * normalized)


def _main_rut_height(x: float, y: float, lateral: float,
                     rel_height: float) -> float:
    shape = _rut_shape(lateral)
    if shape <= 0.0:
        return rel_height
    side = -1.0 if lateral < 0.0 else 1.0
    depth_variation = (
        ab.value_noise(x * 0.14 + 91.0 + side * 5.0,
                       y * 0.14 - 13.0 - side * 3.0) - 0.5
    ) * 0.002
    if y < ZIRAT_START_BLENDER_Y:
        return rel_height + (ROAD_MAIN_RUT_DEPTH - (
            ROAD_MAIN_RUT_DEPTH + depth_variation
        )) * shape

    strength = _broken_rut_strength(x, y, 0.55, 31.0)
    effective_depth = max(0.0, ROAD_ZIRAT_RUT_DEPTH + depth_variation) * strength
    return rel_height + (
        ROAD_MAIN_RUT_DEPTH - effective_depth
    ) * shape


def _fap_rut_height(x: float, y: float, lateral: float,
                    rel_height: float) -> float:
    shape = _rut_shape(lateral)
    if shape <= 0.0:
        return rel_height
    side = -1.0 if lateral < 0.0 else 1.0
    depth_variation = (
        ab.value_noise(x * 0.14 + 121.0 + side * 5.0,
                       y * 0.14 + 17.0 - side * 3.0) - 0.5
    ) * 0.002
    strength = _broken_rut_strength(x, y, 0.40, 47.0)
    effective_depth = max(0.0, ROAD_FAP_RUT_DEPTH + depth_variation) * strength
    return rel_height + (ROAD_FAP_RUT_DEPTH - effective_depth) * shape


def build_roads() -> list:
    objects = []
    # Visible road continues around the rear holdings, not into a transverse
    # fence. Keep height-field axes unchanged: collision remains authoritative.
    main_b = [ab.P(x, z) for x, z in [(10, 82), (8, 70), (4, 55)] + MAIN_AXIS + ZIRAT_AXIS[1:]]
    kara_b = [ab.P(x, z) for x, z in KARA_AXIS]
    fap_b = [ab.P(x, z) for x, z in FAP_AXIS]
    # The physical walkthrough and terrain owner use HousePathAxis, not the
    # older Act1WorldLayout arrival-to-house-yard presentation connector.
    house_b = [ab.P(x, z) for x, z in HOUSE_AXIS]

    assert 3.8 <= ROAD_MAIN_HALF_WIDTH * 2.0 <= 4.2
    assert math.isclose(ROAD_WHEEL_CENTER_SEPARATION, 1.56)
    assert math.isclose(ROAD_RUT_WIDTH, 0.28)
    assert math.isclose(ROAD_MAIN_RUT_DEPTH, 0.03)
    road_profile = _painterly_road_profile(
        ROAD_MAIN_HALF_WIDTH, ROAD_MAIN_RUT_DEPTH, 0.040)
    road = _deformed_ribbon("Road_Main", main_b, road_profile,
                            spacing=0.25, height_lookup=h_ground,
                            lateral_wander=0.10, height_wander=0.003,
                            z_pad=0.0, edge_wander=0.16,
                            profile_height_modifier=_main_rut_height,
                            smooth_normals=True,
                            max_surface_above_ground=ROAD_SURFACE_MAX_ABOVE_GROUND)
    _assign_road_materials(road, road_profile, vehicle_ruts=True)
    objects.append(road)

    fap_profile = _painterly_road_profile(2.0, ROAD_FAP_RUT_DEPTH, 0.035)
    fap_road = _deformed_ribbon("Road_FapBranch", fap_b, fap_profile,
                                spacing=0.25, height_lookup=h_ground,
                                lateral_wander=0.08, height_wander=0.003,
                                z_pad=0.0, edge_wander=0.12,
                                profile_height_modifier=_fap_rut_height,
                                smooth_normals=True,
                                max_surface_above_ground=ROAD_SURFACE_MAX_ABOVE_GROUND)
    _assign_road_materials(fap_road, fap_profile, "AB_earth_path", vehicle_ruts=True)
    objects.append(fap_road)

    house_profile = _painterly_road_profile(1.5, 0.0, 0.028, vehicle_ruts=False)
    house_path = _deformed_ribbon("Road_HousePath", house_b, house_profile,
                                  spacing=0.25, height_lookup=h_ground,
                                  lateral_wander=0.06, height_wander=0.003,
                                  z_pad=0.0, edge_wander=0.08,
                                  smooth_normals=True,
                                  max_surface_above_ground=ROAD_SURFACE_MAX_ABOVE_GROUND)
    _assign_road_materials(house_path, house_profile, "AB_earth_path", vehicle_ruts=False)
    objects.append(house_path)

    kara_profile = _painterly_road_profile(1.7, 0.0, 0.028, vehicle_ruts=False)
    kara_path = _deformed_ribbon("Road_KaraPath", kara_b, kara_profile,
                                 spacing=0.25, height_lookup=h_ground,
                                 lateral_wander=0.07, height_wander=0.003,
                                 z_pad=0.0, edge_wander=0.10,
                                 smooth_normals=True,
                                 max_surface_above_ground=ROAD_SURFACE_MAX_ABOVE_GROUND,
                                 end_fade_length=3.0,
                                 end_width_ratio=0.05,
                                 # Keep the final road a few millimetres above
                                 # the terrain to avoid coplanar z-fighting.
                                 end_height_ratio=0.18)
    # Keep the continuous Kara route readable at night instead of letting
    # the darkest Kara palette turn the path into a black slab.
    _assign_road_materials(kara_path, kara_profile, "AB_road_crown", vehicle_ruts=False)
    objects.append(kara_path)

    # Keep the published Rut_* names for the existing presentation suppression
    # contract, but make them tiny inset seams. The raised/lowered profile on
    # Road_* is the one continuous route owner; these meshes must not become a
    # second pair of dark road ribbons.
    for side, name in ((-1, "Rut_West"), (1, "Rut_East")):
        rut = _deformed_ribbon(
            name, main_b,
            [(side * 0.48, 0.002), (side * 0.55, -0.012),
             (side * 0.62, 0.002)],
            spacing=1.45, height_lookup=h_ground,
            lateral_wander=0.05, height_wander=0.008,
            edge_wander=0.03)
        ab.assign_material(rut, "AB_road_rut")
        objects.append(rut)

    # Side ditches along main road and FAP branch remain named for the
    # connected-world suppression contract. They are shallow, narrow flow
    # seams; the visible legacy kit supplies local bank/ditch framing.
    for side, name in ((-1, "Ditch_West"), (1, "Ditch_East")):
        ditch = _deformed_ribbon(
            name, main_b,
            [(side * 3.18, 0.000), (side * 3.50, -0.026),
             (side * 3.84, -0.012)],
            spacing=1.55, height_lookup=h_ground,
            lateral_wander=0.10, height_wander=0.018,
            edge_wander=0.08)
        ab.assign_material(ditch, "AB_earth_wet")
        objects.append(ditch)
    ditch_fap = _deformed_ribbon(
        "Ditch_FapNorth", fap_b,
        [(-2.62, 0.000), (-2.94, -0.024), (-3.28, -0.010)],
        spacing=1.6, height_lookup=h_ground,
        lateral_wander=0.10, height_wander=0.016,
        edge_wander=0.08)
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
    # Default to the repository root so the documented no-env command writes
    # the canonical source/derived pair; AGENTB_OUT remains an explicit export
    # override for isolated generation.
    root = os.environ.get("AGENTB_OUT") or os.path.abspath(
        os.path.join(HERE, "../../../..")
    )
    glb_path = os.path.join(root, "game/assets/models/agent_b_act1",
                            "agentb_terrain_road_kit.glb")
    blend_path = os.path.join(root, "assets/source/blender/agent_b_act1",
                              "agentb_terrain_road_kit.blend")
    previous_glb_sha = None
    if os.path.isfile(glb_path):
        digest = hashlib.sha256()
        with open(glb_path, "rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
        previous_glb_sha = digest.hexdigest()
    ab.export_glb(objects, glb_path, "URMAN_AgentB_TerrainRoadKit")
    digest = hashlib.sha256()
    with open(glb_path, "rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    if previous_glb_sha != digest.hexdigest() or not os.path.isfile(blend_path):
        ab.save_blend(blend_path)
    else:
        print("AGENTB_TERRAIN_BLEND_SAVE skipped; exported GLB is unchanged")
    ab.report_kit("agentb_terrain_road_kit", objects, glb_path)


if __name__ == "__main__":
    main()
