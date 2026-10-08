"""Agent B Act I Kit 3: authored foliage variants.

Silhouette library so the compositor can scatter non-repeating vegetation:
birches, crooked pines, young spruces, shrubs, ferns, sedge tufts, grass
tufts, fallen branches, moss stones, dead stumps. Each variant is authored
distinctly; nothing is a uniform cone. Variants are laid on a 12 m grid
(Batman space) and re-placed in Godot by name.
"""

from __future__ import annotations

import math
import hashlib
import os
import random
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import agent_b_common as ab  # noqa: E402


def _conifer_branch_spray(name, centre, spread, lobe_width, thickness,
                          seed, material, lobe_count=3, station_count=5):
    """Build one broken, low-poly conifer spray as a single mesh.

    Each spray is a small fan of tapered extruded ribbons.  The lobes share
    only their root area, leaving gaps between uneven outer tips instead of
    forming another closed disc.  Tip lift, width and heading are seeded so
    every exported Tier remains deterministic while still reading as branch
    rhythm at first-person distance.
    """
    rng = random.Random(ab.stable_hash(f"{name}:{seed:.3f}"))
    lobe_count = max(2, min(3, lobe_count))
    verts = []
    faces = []
    for lobe_index in range(lobe_count):
        angle = math.tau * lobe_index / lobe_count + rng.uniform(-0.28, 0.28)
        direction = (math.cos(angle), math.sin(angle))
        normal = (-direction[1], direction[0])
        length = spread * (0.78 + rng.random() * 0.24)
        root = spread * rng.uniform(0.01, 0.08)
        tip_lift = rng.uniform(-thickness * 1.15, thickness * 1.25)
        branch_sag = thickness * rng.uniform(0.24, 0.46)
        side_bias = rng.uniform(-lobe_width * 0.18, lobe_width * 0.18)
        station_count = max(4, min(8, station_count))
        stations = tuple(
            (root if station_index == 0 else
             length * station_index / (station_count - 1),
             max(0.045, 0.52 - 0.475 * station_index / (station_count - 1)))
            for station_index in range(station_count)
        )
        start = len(verts)
        for station, width_ratio in stations:
            t = station / max(length, 1e-6)
            # Deliberately broken outline: each lobe narrows unevenly and its
            # final tip can turn up or down rather than ending in a flat rim.
            half_width = lobe_width * width_ratio * (0.84 + rng.random() * 0.28)
            centre_offset = side_bias * (1.0 - t) + rng.uniform(
                -lobe_width * 0.08, lobe_width * 0.08) * t
            x = direction[0] * station + normal[0] * centre_offset
            y = direction[1] * station + normal[1] * centre_offset
            z_mid = (t ** 1.32) * tip_lift - math.sin(math.pi * t) * branch_sag + math.sin(
                seed * 0.37 + lobe_index * 1.7 + t * 3.1) * thickness * 0.22
            lower = thickness * (0.42 + rng.random() * 0.18)
            upper = thickness * (0.52 + rng.random() * 0.20)
            for side in (-1.0, 1.0):
                px = x + normal[0] * half_width * side
                py = y + normal[1] * half_width * side
                verts.extend((
                    (px, py, z_mid - lower),
                    (px, py, z_mid + upper),
                ))

        station_count = len(stations)
        for station_index in range(station_count - 1):
            a = start + station_index * 4
            b = a + 4
            # top, bottom and the two thin outer edges
            faces.extend((
                (a + 1, b + 1, b + 3, a + 3),
                (a, a + 2, b + 2, b),
                (a, b, b + 1, a + 1),
                (a + 2, a + 3, b + 3, b + 2),
            ))
        last = start + (station_count - 1) * 4
        faces.extend(((start, start + 2, start + 3, start + 1),
                      (last, last + 1, last + 3, last + 2)))

    spray = ab.mesh_from_pydata(name, verts, faces)
    spray.location = centre
    ab.assign_material(spray, material)
    return spray


def _shape_trunk(obj, height, bend_x, bend_y, seed):
    """Add a restrained bend, taper and asymmetric root flare in-place."""
    phase = seed * 0.73
    for vertex in obj.data.vertices:
        t = max(0.0, min(1.0, vertex.co.z / max(height, 1e-6)))
        angle = math.atan2(vertex.co.y, vertex.co.x)
        root_weight = max(0.0, 1.0 - t * 7.0) ** 2
        flare = 1.0 + 0.30 * (1.0 - t) ** 2
        vertex.co.x = vertex.co.x * flare
        vertex.co.y = vertex.co.y * flare
        vertex.co.x += bend_x * height * t ** 1.55
        vertex.co.y += bend_y * height * t ** 1.55
        vertex.co.x += math.cos(angle + phase) * 0.045 * root_weight
        vertex.co.y += math.sin(angle + phase) * 0.035 * root_weight
    obj.data.update()


def _birch_leaf_crown(name, centre, radius, squish, seed,
                      material="AB_foliage_birch", shoots=48, leaves=9):
    """Pendant shoots with open leaf silhouettes, not a closed canopy solid."""
    rng = random.Random(ab.stable_hash(f"{name}:{seed:.3f}"))
    verts, faces = [], []
    for shoot in range(shoots):
        angle = shoot * 2.399963 + seed
        z = 1.0 - 2.0 * (shoot + 0.5) / shoots
        spread = math.sqrt(1.0 - z * z)
        direction = Vector((math.cos(angle) * spread, math.sin(angle) * spread, z))
        side = direction.cross(Vector((0, 0, 1))).normalized()
        normal = side.cross(direction).normalized()
        length = rng.uniform(0.85, 1.15)
        for leaf in range(leaves):
            along = 0.45 + leaf * 0.09
            hand = 1 if leaf % 2 else -1
            anchor = direction * (length * along) + side * (hand * 0.12 * along)
            anchor.z -= 0.20 * along * along
            axis = (direction * 0.45 + side * hand).normalized()
            across = normal.cross(axis).normalized()
            size = rng.uniform(0.21, 0.25)
            points = (anchor - axis * size,
                      anchor + across * size * 0.58 + normal * size * 0.18,
                      anchor + axis * size,
                      anchor - across * size * 0.58 + normal * size * 0.18)
            # Distinct vertices preserve both sides through mesh.validate();
            # reversed faces sharing indices are discarded as duplicates.
            for face in ((0, 1, 2), (0, 2, 3), (2, 1, 0), (3, 2, 0)):
                start = len(verts)
                verts.extend((points[v].x * radius, points[v].y * radius,
                              points[v].z * radius * squish) for v in face)
                faces.append((start, start + 1, start + 2))
    crown = ab.mesh_from_pydata(name, verts, faces)
    crown.location = centre
    ab.assign_material(crown, material)
    return crown


def birch_variant(index, height, lean):
    prefix = f"Birch_{index}"
    objs = []
    trunk = ab.make_cylinder(f"{prefix}_Trunk", (0, 0, 0), 0.12, 0.052,
                             height, segments=7)
    _shape_trunk(trunk, height, lean * 0.45, -lean * 0.18, index)
    ab.rotate_around(trunk, lean, "x", (0.0, 0.0, 0.0))
    ab.rotate_around(trunk, lean * 0.6, "y", (0.0, 0.0, 0.0))
    ab.assign_material(trunk, "AB_bark_birch")
    objs.append(trunk)
    # Two restrained side branches break the straight pole silhouette.  They
    # stay well above the ground and use the existing bark material, so the
    # added geometry remains presentation-only when extracted by Godot.
    for branch_index, (branch_height, branch_angle, branch_length) in enumerate((
        (0.47 + (index % 2) * 0.05, 0.28 + index * 0.05, 0.86 + (index % 3) * 0.09),
        (0.66 + (index % 3) * 0.035, -0.31 + index * 0.04, 0.68 + (index % 2) * 0.12),
    )):
        branch = ab.make_cylinder(
            f"{prefix}_Branch{branch_index}",
            (0.0, 0.0, height * branch_height),
            0.052,
            0.016,
            branch_length,
            segments=5,
            axis="x",
        )
        ab.rotate_around(branch, branch_angle + lean * 0.4, "z")
        ab.rotate_around(branch, -0.18 + branch_index * 0.15, "y")
        ab.assign_material(branch, "AB_bark_birch")
        objs.append(branch)
    # Three interlocking crown masses: a broad lower shoulder, a smaller
    # windward crown and a low side mass.  They overlap in height so the
    # family reads as one branching tree instead of three pancakes on a pole.
    crown_specs = (
        (0.90, height * 0.80, 0.74, lean * height * 0.26 - 0.18, -0.08),
        (0.60, height * 0.94, 0.62, lean * height * 0.38 + 0.32, 0.24),
        (0.56, height * 0.69, 0.62, lean * height * 0.15 + 0.10, -0.25),
    )
    for k, (r, crown_z, sq, crown_x, crown_y) in enumerate(crown_specs):
        blob = _birch_leaf_crown(f"{prefix}_Canopy{k}",
                                 (crown_x, crown_y, crown_z), r * height / 6.0, sq,
                                 seed=index * 3.1 + k)
        objs.append(blob)
    return objs


def pine_variant(index, height, crook):
    """Crooked pine with broken branch sprays and visible trunk gaps."""
    prefix = f"Pine_{index}"
    objs = []
    trunk = ab.make_cylinder(f"{prefix}_Trunk", (0, 0, 0), 0.17, 0.062,
                             height, segments=7)
    _shape_trunk(trunk, height, crook * 0.13, -crook * 0.07, index + 7.0)
    ab.rotate_around(trunk, crook * 0.22, "x", (0.0, 0.0, 0.0))
    ab.rotate_around(trunk, -crook * 0.14, "y", (0.0, 0.0, 0.0))
    ab.assign_material(trunk, "AB_bark_dark")
    objs.append(trunk)
    tiers = (
        # z, spread, lobe width, thickness, yaw, x/y offset, lobe count
        (0.27, 0.34, 0.118, 0.078, -0.16, -0.04, 0.02, 3),
        (0.43, 0.255, 0.095, 0.064, 0.22, 0.07, -0.03, 2),
        (0.59, 0.285, 0.104, 0.070, -0.28, -0.06, 0.055, 3),
        (0.76, 0.205, 0.082, 0.056, 0.18, 0.08, -0.045, 2),
        (0.91, 0.135, 0.056, 0.048, -0.12, -0.05, 0.03, 3),
    )
    for k, (z_rel, spread_rel, width_rel, thickness_rel, yaw,
            offset_x, offset_y, lobe_count) in enumerate(tiers):
        # Each tree keeps its own branch rhythm; this is arithmetic variation,
        # not unseeded randomness, so the preview board remains deterministic.
        z_rel += (((index * 5 + k * 3) % 5) - 2) * 0.010
        spread_rel *= 1.0 + (((index + k) % 3) - 1) * 0.055
        spray = _conifer_branch_spray(
            f"{prefix}_Tier{k}",
            (crook * z_rel * height * 0.36 + offset_x * height,
             offset_y * height + ((index + k * 2) % 3 - 1) * 0.025 * height,
             z_rel * height - 0.18 + (k % 2) * 0.035 * height),
            spread=spread_rel * height,
            lobe_width=width_rel * height,
            thickness=thickness_rel * height,
            seed=index * 7.7 + k * 1.3,
            material="AB_foliage_pine",
            lobe_count=lobe_count)
        ab.rotate_around(spray, yaw + crook * (0.025 if k % 2 else -0.018),
                         "z", (0.0, 0.0, 0.0))
        objs.append(spray)
    return objs


def spruce_variant(index, height):
    """Young spruce: narrow broken sprays with a readable central trunk."""
    prefix = f"Spruce_{index}"
    objs = []
    trunk = ab.make_cylinder(f"{prefix}_Trunk", (0, 0, 0), 0.082, 0.034,
                             height, segments=6)
    _shape_trunk(trunk, height, (index - 2) * 0.024, (index % 2 - 0.5) * 0.024,
                 index + 13.0)
    ab.assign_material(trunk, "AB_bark_dark")
    objs.append(trunk)
    tiers = (
        (0.18, 0.225, 0.078, 0.054, -0.18, 2),
        (0.37, 0.172, 0.064, 0.045, 0.24, 3),
        (0.57, 0.142, 0.054, 0.039, -0.26, 2),
        (0.79, 0.092, 0.040, 0.032, 0.16, 3),
    )
    for k, (z_rel, spread_rel, width_rel, thickness_rel, yaw, lobe_count) in enumerate(tiers):
        z_rel += (((index + 2 * k) % 3) - 1) * 0.012
        spread_rel *= 1.0 + (((index + k) % 2) * 0.08 - 0.04)
        spray = _conifer_branch_spray(
            f"{prefix}_Tier{k}",
            (math.sin(index * 0.8 + k * 1.7) * 0.035 * height,
             math.cos(index * 0.6 + k * 1.3) * 0.030 * height,
             z_rel * height - 0.06 + (k % 2) * 0.018 * height),
            spread=spread_rel * height,
            lobe_width=width_rel * height,
            thickness=thickness_rel * height,
            seed=index * 5.3 + k,
            material="AB_foliage_spruce",
            lobe_count=lobe_count)
        ab.rotate_around(spray, yaw + (index - 2) * 0.035, "z",
                         (0.0, 0.0, 0.0))
        objs.append(spray)
    return objs


def shrub_variant(index, radius):
    prefix = f"Shrub_{index}"
    objs = []
    lobe_layout = (
        (0.0, 0.0, 0.76, 1.00),
        (0.58, -0.12, 0.56, 0.64),
        (-0.52, 0.20, 0.48, 0.58),
    )
    for k, (ox, oy, oz, rr) in enumerate(lobe_layout):
        phase = (index - 2) * 0.18
        ox += math.cos(phase + k * 1.9) * radius * 0.08
        oy += math.sin(phase + k * 1.7) * radius * 0.06
        oz += math.sin(index * 0.7 + k) * radius * 0.04
        # Open shoots reuse the tree leaf geometry, with a lower, irregular
        # shrub habit. Respect radius instead of exporting identical boulders.
        centre = (ox * radius, oy * radius, oz * radius)
        crown = _birch_leaf_crown(
            f"{prefix}_Lobe{k}", centre, rr * radius,
            (0.66, 0.72, 0.78)[k], seed=index * 11.0 + k,
            material="AB_foliage_shrub", shoots=32, leaves=7)
        objs.append(crown)
        branch = ab.make_cylinder(f"{prefix}_Branch{k}", (0, 0, 0),
                                  .025 * radius, .006 * radius,
                                  Vector(centre).length, segments=5)
        branch.rotation_euler = Vector(centre).to_track_quat("Z", "Y").to_euler()
        # Bake before the preview-grid translation, otherwise export rotates
        # that translation too and separates branches from their leaf shoots.
        ab.bake_transforms(branch)
        ab.assign_material(branch, "AB_bark")
        objs.append(branch)
    return objs


def _blade(prefix, index_k, size_x, size_y, size_z, material):
    """Tapered, slightly bent blade extending along +y from the origin.

    A thin cuboid reads as a card or disc when many are planted together.
    This small extruded ribbon has a broad wet base, a narrowing middle and a
    pointed tip; the deterministic bend keeps each tuft from forming a
    repeated X-shaped stamp.
    """
    width = max(size_x * 1.65, 0.035)
    length = size_y
    thickness = max(size_z * 0.8, 0.012)
    rng = random.Random(ab.stable_hash(f"{prefix}:{index_k}"))
    bend = (rng.random() - 0.5) * width * 2.4
    mid_bend = bend * 0.42
    sections = (
        (0.0, width * 0.50, 0.0),
        (length * 0.46, width * 0.37, mid_bend),
        (length * 0.80, width * 0.19, bend),
        (length, max(width * 0.10, 0.004), bend * 1.08),
    )
    verts = []
    for y, half_width, x_offset in sections:
        curve = (y / max(length, 1e-6)) ** 1.35 * thickness * 0.60
        for x in (-half_width, half_width):
            verts.append((x + x_offset, y, curve))
            verts.append((x + x_offset, y, curve + thickness))

    # Cross-sections are centimetre-scale leaves, not metre-wide paddles.
    assert all(abs(verts[i + 2][0] - verts[i][0]) <= width + 1e-6
               for i in range(0, len(verts), 4)), prefix
    faces = []
    for section in range(len(sections) - 1):
        a = section * 4
        b = (section + 1) * 4
        # front, back and the two tapered edges
        faces.extend(((a, b, b + 2, a + 2),
                      (a + 1, a + 3, b + 3, b + 1),
                      (a, a + 1, b + 1, b),
                      (a + 2, b + 2, b + 3, a + 3)))
    faces.append((0, 2, 3, 1))
    blade = ab.mesh_from_pydata(prefix, verts, faces)
    ab.assign_material(blade, material)
    return blade


def fern_variant(index):
    prefix = f"Fern_{index}"
    objs = []
    fronds = 6 if index % 2 == 0 else 7
    for k in range(fronds):
        angle = (k / fronds) * math.tau + index * 0.35
        length = 0.52 + 0.08 * ((k + index) % 3)
        verts, faces = [], []

        def axis(t):
            return (0.0, length * t,
                    0.015 + length * 0.62 * math.sin(t * math.pi * 0.86))

        # All fronds emerge from the same grounded crown. The arched rachis
        # connects paired pinnae; no detached elevated billboard leaves.
        for j in range(9):
            x, y, z = axis(j / 8)
            radius = 0.006 * (1.0 - j / 9)
            verts.extend(((x - radius, y, z), (x + radius, y, z)))
            if j:
                a = (j - 1) * 2
                faces.append((a, a + 1, a + 3, a + 2))
        for j in range(1, 8):
            t = j / 9
            _, y, z = axis(t)
            reach = length * 0.24 * math.sin(math.pi * t) ** 0.7
            for side in (-1, 1):
                a = len(verts)
                verts.extend(((0.0, y, z),
                              (side * reach * 0.48, y - 0.024, z + 0.012),
                              (side * reach, y + length * 0.08, z - 0.016),
                              (side * reach * 0.48, y + 0.032, z + 0.012)))
                faces.extend(((a, a + 1, a + 2), (a, a + 2, a + 3)))
        blade = ab.mesh_from_pydata(f"{prefix}_Frond{k}", verts, faces)
        assert min(v.co.z for v in blade.data.vertices) <= 0.016, blade.name
        ab.rotate_around(blade, angle, "z", (0.0, 0.0, 0.0))
        ab.assign_material(blade, "AB_fern")
        objs.append(blade)
    return objs


def sedge_variant(index):
    prefix = f"Sedge_{index}"
    objs = []
    blades = 8
    for k in range(blades):
        angle = (k / blades) * math.tau + index
        length = 0.46 + 0.08 * ((k * 3 + index) % 4)
        width = 0.018 + 0.006 * ((k + index) % 3)
        blade = _blade(f"{prefix}_Blade{k}", k, width, length, 0.03, "AB_sedge")
        ab.rotate_around(blade, 0.42 + ((k + index) % 3) * 0.14,
                        "x", (0.0, 0.0, 0.0))
        ab.rotate_around(blade, angle, "z", (0.0, 0.0, 0.0))
        base_radius = 0.09 + 0.025 * ((k + index) % 2)
        ab.translate_vertices(blade, math.cos(angle) * base_radius,
                              math.sin(angle) * base_radius,
                              0.25 + 0.035 * (k % 3))
        ab.assign_material(blade, "AB_sedge")
        objs.append(blade)
    return objs


def grass_tuft(index):
    prefix = f"GrassTuft_{index}"
    objs = []
    for k in range(5):
        angle = (k / 5) * math.tau + index * 0.8
        length = 0.26 + 0.07 * ((k + index) % 3)
        width = 0.032 + 0.010 * ((k * 2 + index) % 2)
        blade = _blade(f"{prefix}_B{k}", k, width, length, 0.024, "AB_grass")
        ab.rotate_around(blade, 0.30 + ((k + index) % 3) * 0.10,
                        "x", (0.0, 0.0, 0.0))
        ab.rotate_around(blade, angle, "z", (0.0, 0.0, 0.0))
        base_radius = 0.055 + 0.018 * ((k + index) % 2)
        ab.translate_vertices(blade, math.cos(angle) * base_radius,
                              math.sin(angle) * base_radius,
                              0.12 + 0.025 * (k % 2))
        ab.assign_material(blade, "AB_grass" if index % 3 else "AB_grass_dry")
        objs.append(blade)
    return objs


def fallen_branch(index, length):
    prefix = f"FallenBranch_{index}"
    if index == 2:
        # Mature windthrow, consumed only by the forest rim. Reuse
        # the rooted wood/snow library rather than enlarge a garden twig.
        points = [Vector((length*t, y, z)) for t, y, z in
                  ((-.5, 0, .8), (-.34, .12, .9), (-.18, .05, .82),
                   (0, .2, .73), (.19, .25, .58), (.37, .48, .4), (.5, .4, .3))]
        vertices, faces, snow_vertices, snow_faces = [], [], [], []
        _bare_branch(prefix, vertices, faces, points, [.48, .44, .38, .35, .31, .26, .22], segments=9)
        # A blunt irregular fracture, rather than a uniformly sharpened tip.
        for i, splinter in enumerate((.14, 0, -.22, .08, -.18, .11, .02, -.15, .04)):
            vertex = Vector(vertices[(len(points)-1)*9+i])
            vertex.x += splinter
            vertices[(len(points)-1)*9+i] = tuple(vertex)
        for i in range(5):
            angle = .25 + i*1.15
            root = points[0]
            tip = root + Vector((-.4-.12*i, math.cos(angle)*1.3, math.sin(angle)*1.3))
            _bare_branch(prefix, vertices, faces,
                         [root, root.lerp(tip, .55), tip], [.19, .11, .025], segments=5)
        for i, t in enumerate((.24, .46, .69)):
            root = _point_on_polyline(points, t)
            tip = root + Vector((.8, (-1 if i%2 else 1)*1.7, .75+.25*i))
            _bare_branch(prefix, vertices, faces,
                         [root, root.lerp(tip, .65), tip], [.12, .07, .012], segments=5)
        # Broken lower limbs support the raised bole opposite its root plate.
        for root in (points[4], points[-1]):
            foot = root + Vector((.18, -.35, -.5-root.z))
            _bare_branch(prefix, vertices, faces, [root, foot], [.13, .04], segments=5)
        for a, b in zip(points[::2], points[2::2]):
            _append_snow_cap(snow_vertices, snow_faces, [a, b], .75, .29, index)
        wood = ab.mesh_from_pydata(prefix + "_Branch", vertices, faces)
        snow = ab.mesh_from_pydata(prefix + "_Snow", snow_vertices, snow_faces)
        ab.assign_material(wood, "AB_bark_dark")
        ab.assign_material(snow, "AB_snow")
        objects = [wood, snow]
        for polygon in wood.data.polygons:
            polygon.use_smooth = len(polygon.vertices) == 4
        _smooth_winter_surfaces(objects, "near")
        base = min(v.co.z for obj in objects for v in obj.data.vertices)
        for obj in objects:
            for v in obj.data.vertices:
                v.co.z -= base
                assert all(math.isfinite(value) for value in v.co), prefix
        triangles = sum(len(p.vertices)-2 for obj in objects for p in obj.data.polygons)
        assert triangles <= 600, (prefix, triangles)
        print(f"AGENTB_DEADFALL {prefix} triangles={triangles} length={length}")
        return objects
    objs = []
    branch = ab.make_cylinder(f"{prefix}_Main", (0, 0, 0.06), 0.035, 0.015,
                              length, segments=6, axis="x")
    ab.rotate_around(branch, -0.045 + index * 0.09, "z")
    ab.assign_material(branch, "AB_bark")
    objs.append(branch)
    twig = ab.make_cylinder(f"{prefix}_Twig", (length * 0.4, 0, 0.08), 0.015,
                            0.005, 0.45, segments=5)
    ab.rotate_around(twig, 0.38 + index * 0.12, "x",
                    (length * 0.4, 0, 0.08))
    ab.assign_material(twig, "AB_bark")
    objs.append(twig)
    # A second, shorter fork gives the fallen branch a readable direction and
    # prevents every ground accent from becoming the same straight cylinder.
    fork = ab.make_cylinder(f"{prefix}_Fork", (length * 0.03, 0.0, 0.075),
                            0.012, 0.003, 0.30 + index * 0.08, segments=5,
                            axis="x")
    ab.rotate_around(fork, -0.52 + index * 0.18, "z")
    ab.rotate_around(fork, 0.24, "y")
    ab.assign_material(fork, "AB_bark")
    objs.append(fork)
    return objs


def _faceted_rock(name, radius, seed):
    """Low-poly stone with a grounded base and a broken shoulder."""
    rng = random.Random(ab.stable_hash(f"{name}:{seed}"))
    segments = 7
    levels = ((-0.48, 0.66), (-0.22, 1.0), (0.12, 0.91), (0.42, 0.52))
    verts = []
    for level_index, (z_rel, radius_rel) in enumerate(levels):
        for segment in range(segments):
            angle = math.tau * segment / segments + seed * 0.17 + level_index * 0.21
            local_radius = radius * radius_rel * (0.84 + rng.random() * 0.28)
            verts.append((math.cos(angle) * local_radius,
                          math.sin(angle) * local_radius,
                          z_rel * radius * 1.35))
    faces = []
    for level_index in range(len(levels) - 1):
        row = level_index * segments
        next_row = (level_index + 1) * segments
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            faces.append((row + segment, row + next_segment,
                          next_row + next_segment, next_row + segment))
    faces.append(tuple(range(segments - 1, -1, -1)))
    top = (len(levels) - 1) * segments
    faces.append(tuple(top + segment for segment in range(segments)))
    stone = ab.mesh_from_pydata(name, verts, faces)
    ab.assign_material(stone, "AB_stone")
    return stone


def _moss_cap(name, radius, seed):
    """Shallow off-centre moss cap, not a second flattened sphere."""
    rng = random.Random(ab.stable_hash(f"{name}:{seed}"))
    segments = 7
    verts = []
    for z_rel, radius_rel in ((0.0, 0.28), (0.08, 0.82),
                              (0.18, 1.0), (0.25, 0.34)):
        for segment in range(segments):
            angle = math.tau * segment / segments + seed * 0.11
            jitter = 0.88 + rng.random() * 0.20
            local_radius = radius * 0.66 * radius_rel * jitter
            verts.append((math.cos(angle) * local_radius + radius * 0.08,
                          math.sin(angle) * local_radius - radius * 0.05,
                          radius * 0.58 * z_rel))
    faces = []
    for ring_index in range(3):
        row = ring_index * segments
        next_row = (ring_index + 1) * segments
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            faces.append((row + segment, row + next_segment,
                          next_row + next_segment, next_row + segment))
    cap = ab.mesh_from_pydata(name, verts, faces)
    ab.assign_material(cap, "AB_moss")
    return cap


def moss_stone(index, radius):
    prefix = f"MossStone_{index}"
    stone = _faceted_rock(f"{prefix}_Stone", radius, index * 3.3)
    stone.location = (0, 0, radius * 0.4)
    moss = _moss_cap(f"{prefix}_Moss", radius, index * 6.1)
    moss.location = (0, 0, radius * 0.55)
    return [stone, moss]


def _stump_body(name, height, index):
    """Short trunk with a chipped top and subtly flared root collar."""
    rng = random.Random(ab.stable_hash(f"{name}:{index}"))
    segments = 7
    rings = ((0.0, 1.22), (0.16, 1.06), (0.82, 0.94), (1.0, 0.99))
    verts = []
    for ring_index, (z_rel, radius_rel) in enumerate(rings):
        for segment in range(segments):
            angle = math.tau * segment / segments + index * 0.23
            local_radius = 0.22 * radius_rel * (0.88 + rng.random() * 0.20)
            top_chip = (rng.random() - 0.5) * height * 0.10 if ring_index == 3 else 0.0
            verts.append((math.cos(angle) * local_radius,
                          math.sin(angle) * local_radius,
                          height * z_rel + top_chip))
    faces = []
    for ring_index in range(len(rings) - 1):
        row = ring_index * segments
        next_row = (ring_index + 1) * segments
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            faces.append((row + segment, row + next_segment,
                          next_row + next_segment, next_row + segment))
    faces.append(tuple(range(segments - 1, -1, -1)))
    top = (len(rings) - 1) * segments
    faces.append(tuple(top + segment for segment in range(segments)))
    stump = ab.mesh_from_pydata(name, verts, faces)
    ab.assign_material(stump, "AB_bark_dark")
    return stump


def dead_stump(index, height):
    prefix = f"Stump_{index}"
    stump = _stump_body(f"{prefix}_Stump", height, index)
    root = ab.make_box(f"{prefix}_Root", -0.4, 0.4, -0.1, 0.1, 0.0, 0.14,
                       jitter=0.03)
    for vertex in root.data.vertices:
        top_weight = max(0.0, min(1.0, vertex.co.z / 0.14))
        vertex.co.x *= 1.0 - 0.24 * top_weight
        vertex.co.y *= 1.0 - 0.18 * top_weight
        vertex.co.x += 0.04 * (1.0 - top_weight) * math.sin(
            index + vertex.co.y * 8.0)
    root.data.update()
    root.rotation_euler.z = index * 0.7
    ab.assign_material(root, "AB_bark")
    return [stump, root]


def _point_on_polyline(points, t):
    """Return an attachment point on the same centreline used for the tube."""
    if len(points) == 1:
        return Vector(points[0])
    lengths = []
    total = 0.0
    for first, second in zip(points, points[1:]):
        length = (Vector(second) - Vector(first)).length
        lengths.append(length)
        total += length
    target = max(0.0, min(1.0, t)) * max(total, 1e-6)
    walked = 0.0
    for index, length in enumerate(lengths):
        if target <= walked + length or index == len(lengths) - 1:
            local_t = (target - walked) / max(length, 1e-6)
            return Vector(points[index]).lerp(Vector(points[index + 1]), local_t)
        walked += length
    return Vector(points[-1])


def _append_polyline_tube(vertices, faces, points, radii, sides=5, flatten=1.0):
    """Append one connected, tapering tube around an explicit 3D centreline."""
    assert len(points) == len(radii) and len(points) >= 2
    rings = []
    for index, point in enumerate(points):
        point = Vector(point)
        if index == 0:
            tangent = Vector(points[1]) - point
        elif index == len(points) - 1:
            tangent = point - Vector(points[index - 1])
        else:
            tangent = Vector(points[index + 1]) - Vector(points[index - 1])
        tangent.normalize()
        reference = Vector((0.0, 0.0, 1.0))
        if abs(tangent.dot(reference)) > 0.92:
            reference = Vector((1.0, 0.0, 0.0))
        side = tangent.cross(reference).normalized()
        up = side.cross(tangent).normalized()
        ring = []
        for side_index in range(sides):
            angle = math.tau * side_index / sides
            vertex = point + (side * math.cos(angle) + up * math.sin(angle) * flatten) * radii[index]
            vertex.z = max(0.0, vertex.z)
            ring.append(len(vertices))
            vertices.append(tuple(vertex))
        rings.append(ring)
    for ring_index, (first, second) in enumerate(zip(rings, rings[1:])):
        for side_index in range(sides):
            next_side = (side_index + 1) % sides
            face = (first[side_index], second[side_index],
                    second[next_side], first[next_side])
            first_vertex = Vector(vertices[face[0]])
            second_vertex = Vector(vertices[face[1]])
            third_vertex = Vector(vertices[face[2]])
            face_normal = (second_vertex - first_vertex).cross(
                third_vertex - first_vertex).normalized()
            ring_center = Vector(points[ring_index])
            radial = (sum((Vector(vertices[index]) for index in face),
                          Vector()) / 4.0 - ring_center).normalized()
            assert face_normal.dot(radial) > 1e-5, (
                "polyline tube winding", ring_index, side_index,
                face_normal.dot(radial))
            faces.append(face)
    # The ring basis has side x up == -tangent: the first cap faces down,
    # while the last cap must reverse its winding to face up the centreline.
    faces.append(tuple(rings[0]))
    faces.append(tuple(reversed(rings[-1])))


def _bare_branch(name, vertices, faces, centreline, radii, segments=5):
    """Append a tapering branch; children must attach to this centreline."""
    _append_polyline_tube(vertices, faces, centreline, radii, sides=segments)
    return centreline


def _append_snow_cap(vertices, faces, centreline, radius, width, seed):
    """Append a short flattened snow mantle with rounded pointed ends."""
    centre = _point_on_polyline(centreline, 0.58)
    before = _point_on_polyline(centreline, 0.52)
    after = _point_on_polyline(centreline, 0.64)
    tangent = (after - before).normalized()
    side = tangent.cross(Vector((0.0, 0.0, 1.0)))
    if side.length < 1e-5:
        side = tangent.cross(Vector((1.0, 0.0, 0.0)))
    side.normalize()
    upper = Vector((0.0, 0.0, 1.0)) - tangent * tangent.z
    if upper.length < 1e-5:
        upper = Vector((0.0, 0.0, 1.0))
    upper.normalize()
    thickness = width * (0.24 + 0.05 * (seed % 3))
    half_length = min((Vector(centreline[-1]) - Vector(centreline[0])).length * 0.16,
                      width * 2.7)
    centre_offset = radius * 0.42 + thickness * 0.32
    stations = ((-half_length, 0.42), (-half_length * 0.45, 0.78),
                (0.0, 1.0), (half_length * 0.48, 0.72),
                (half_length, 0.18))
    cross_sections = 6
    rings = []
    for along, width_ratio in stations:
        point = centre + tangent * along
        half_width = width * width_ratio
        cap_centre = point + upper * centre_offset
        ring_start = len(vertices)
        for cross_index in range(cross_sections):
            angle = math.tau * cross_index / cross_sections
            vertices.append(tuple(
                cap_centre + side * (half_width * math.cos(angle))
                + upper * (thickness * 0.5 * math.sin(angle))))
        rings.append(tuple(range(ring_start, ring_start + cross_sections)))
    for first, second in zip(rings, rings[1:]):
        for cross_index in range(cross_sections):
            next_cross = (cross_index + 1) % cross_sections
            faces.append((first[cross_index], second[cross_index],
                          second[next_cross], first[next_cross]))
    first_tip = len(vertices)
    vertices.append(tuple(centre - tangent * half_length + upper * centre_offset))
    last_tip = len(vertices)
    vertices.append(tuple(centre + tangent * half_length + upper * centre_offset))
    for cross_index in range(cross_sections):
        next_cross = (cross_index + 1) % cross_sections
        faces.append((first_tip, rings[0][next_cross], rings[0][cross_index]))
        faces.append((last_tip, rings[-1][cross_index], rings[-1][next_cross]))


def _append_berry(vertices, faces, centre, radius):
    """Append one tiny low-poly berry to the single optional berry mesh."""
    centre = Vector(centre)
    start = len(vertices)
    vertices.extend((
        tuple(centre + Vector((0.0, 0.0, radius))),
        tuple(centre + Vector((radius, 0.0, 0.0))),
        tuple(centre + Vector((0.0, radius, 0.0))),
        tuple(centre + Vector((-radius, 0.0, 0.0))),
        tuple(centre + Vector((0.0, -radius, 0.0))),
        tuple(centre + Vector((0.0, 0.0, -radius))),
    ))
    faces.extend((
        (start, start + 1, start + 2), (start, start + 2, start + 3),
        (start, start + 3, start + 4), (start, start + 4, start + 1),
        (start + 5, start + 2, start + 1), (start + 5, start + 3, start + 2),
        (start + 5, start + 4, start + 3), (start + 5, start + 1, start + 4),
    ))


def _smooth_winter_surfaces(objects, tier):
    """Smooth only winter bark and snow; needles stay deliberately faceted."""
    if tier not in ("near", "light"):
        return
    for obj in objects:
        if obj.name.endswith("_Trunk") or obj.name.endswith("_Snow"):
            for polygon in obj.data.polygons:
                polygon.use_smooth = True


def _winter_trunk_centerline(height, lean_x, lean_y, seed, offset=(0.0, 0.0), count=6):
    points = []
    for index in range(count):
        t = index / (count - 1)
        sway = math.sin(seed * 0.31 + t * 4.6) * height * 0.012 * t * (1.0 - t * 0.35)
        points.append((
            offset[0] + lean_x * height * t ** 1.45 + sway,
            offset[1] + lean_y * height * t ** 1.35 + math.cos(seed + t * 3.2) * height * 0.008 * t,
            height * t,
        ))
    return points


def _assert_winter_variant(prefix, objects, tier, limits=None):
    assert len(objects) <= 3, f"{prefix}: too many mesh objects"
    assert any(obj.name == f"{prefix}_Trunk" for obj in objects), prefix
    triangle_count = 0
    for obj in objects:
        assert obj.name.startswith(prefix + "_"), obj.name
        assert obj.data.materials and obj.data.materials[0].name in {
            "AB_bark", "AB_bark_birch", "AB_bark_dark", "AB_snow",
            "AB_foliage_rowan", "AB_foliage_spruce",
            # VIS-082/075: readable bark per species and one old-growth bark.
            "AB_bark_old", "AB_bark_linden", "AB_bark_willow", "AB_bark_rowan",
            "AB_bark_pine", "AB_needles_spruce"
        }, obj.name
        for vertex in obj.data.vertices:
            assert all(math.isfinite(value) for value in vertex.co), (prefix, obj.name)
        triangle_count += sum(max(1, len(polygon.vertices) - 2)
                              for polygon in obj.data.polygons)
    trunk = next(obj for obj in objects if obj.name == f"{prefix}_Trunk")
    min_z = min(vertex.co.z for vertex in trunk.data.vertices)
    assert -1e-5 <= min_z <= 1e-5, (prefix, min_z)
    # The shared Agent B winter budget. A bare old-growth giant legitimately
    # carries less geometry than a needle crown, so that family passes its own
    # documented band rather than being padded with filler triangles.
    limits = limits or {"near": (2000, 6000), "light": (600, 1500), "far": (100, 400)}
    low, high = limits[tier]
    assert low <= triangle_count <= high, (prefix, tier, triangle_count)
    print(f"AGENTB_WINTER_VARIANT {prefix} tier={tier} triangles={triangle_count} objects={len(objects)}")


def _avoid_repeated_angle(angle, taken, minimum=0.55):
    """Nudge a new primary off the azimuth of a limb already placed on the stem.

    VIS-082: a repeating Y-fork was the second half of the "radial stick"
    reading. Two primaries of one stem never come closer than `minimum` rad, so
    walking round the trunk never presents the same silhouette twice.
    """
    for _ in range(6):
        if all(abs((angle - other + math.pi) % math.tau - math.pi) >= minimum
               for other in taken):
            break
        angle += minimum * 1.13
    return angle


def _winter_habit_records(species, index, height, spread, branches,
                          seed_offset, droop):
    """Return one rooted trunk and the shared primary fork paths for all LODs."""
    habit_key = f"Winter{species}_{index}:{seed_offset:.2f}"
    rng = random.Random(ab.stable_hash(habit_key))
    # Caliper is a fraction of height, and a mature birch is a slender tree
    # while a linden or maple carries a visibly heavier bole. A single shared
    # fraction made every winter tree read as a forked post: the near arrival
    # birch measured about 0.5 m across the trunk and 0.16 m across a primary,
    # so limbs rendered as thick horns and the fine shoots vanished between
    # them. These ratios are per species so the same trunk is reused by the
    # near, light and far tiers of one variant.
    caliper = {
        "Birch": 0.90, "Linden": 1.30, "Maple": 1.20,
        "Rowan": 0.85, "BirdCherry": 0.95, "Willow": 1.05,
    }.get(species, 1.0)
    # VIS-082/VIS-083: one authored habit per species and silhouette variant.
    # `phase` rotates the whole primary system, `first`/`span` set where on the
    # bole the limbs attach, `crowd` is how many primaries the species carries,
    # and `fork` marks the branch that continues the trunk as a co-dominant
    # stem instead of ending as a limb. Three genuinely different habits at
    # hero distance (slender ascending birch, broad low-forked linden, dense
    # small-crowned rowan, drooping willow, forked variant 3) is the acceptance
    # criterion; a shared Y with random scale is not.
    habit = {
        "Birch": {"phase": 0.35, "first": 0.36, "span": 0.54, "crowd": 5, "fork": None},
        "Linden": {"phase": 2.55, "first": 0.26, "span": 0.40, "crowd": 6, "fork": 1},
        "Maple": {"phase": 4.30, "first": 0.30, "span": 0.42, "crowd": 5, "fork": 2},
        "Rowan": {"phase": 5.55, "first": 0.44, "span": 0.50, "crowd": 6, "fork": None},
        "Willow": {"phase": 1.45, "first": 0.30, "span": 0.34, "crowd": 5, "fork": None},
        "BirdCherry": {"phase": 3.35, "first": 0.40, "span": 0.46, "crowd": 2, "fork": None},
    }.get(species, {"phase": seed_offset * 0.11, "first": 0.34, "span": 0.50,
                    "crowd": 5, "fork": None})
    variant_phase = (index - 1) * 0.83
    if species != "BirdCherry":
        habit = dict(habit)
        habit["crowd"] = max(3, habit["crowd"] - (1 if index == 2 else 0))
        habit["first"] += 0.05 * ((index - 1) % 3)
    if species == "BirdCherry":
        stem_specs = (
            ((0.00, 0.00), 1.00, 0.00, 0.00),
            ((-0.16, 0.04), 0.82, -0.06, 0.02),
            ((0.14, -0.05), 0.74, 0.07, -0.02),
        )
    else:
        stem_specs = (((0.0, 0.0), 1.0, rng.uniform(-0.05, 0.05),
                       rng.uniform(-0.04, 0.04)),)

    stems = []
    primaries = []
    for stem_index, (offset, height_scale, lean_x, lean_y) in enumerate(stem_specs):
        stem_height = height * height_scale
        # Every tier uses these exact six centreline stations. Detail changes
        # only by radial sides and secondary/fine branch omission.
        trunk_points = _winter_trunk_centerline(
            stem_height, lean_x, lean_y, seed_offset + stem_index * 7.3,
            offset=offset, count=6)
        # VIS-082: a bole with a readable root flare and an even taper. The old
        # schedule ended at a needle tip, so the trunk read as a cone and every
        # fork looked glued on.
        trunk_radii = [stem_height * caliper * (0.0135 - 0.0113 * t)
                       * (1.0 + 0.42 * (1.0 - t) ** 2) * (1.0 + 0.16 * t ** 3)
                       for t in (i / 5.0 for i in range(6))]
        stems.append((trunk_points, trunk_radii, stem_index))

        if species == "BirdCherry":
            primary_count = 2
        elif species in ("Birch", "Linden", "Maple"):
            primary_count = 5
        else:
            primary_count = 5
        primary_count = habit["crowd"]
        if species in ("Linden", "Maple"):
            levels = tuple(0.28 + 0.20 * i / max(primary_count - 1, 1)
                           for i in range(primary_count))
        else:
            levels = tuple(habit["first"] + habit["span"] * i / max(primary_count - 1, 1)
                           for i in range(primary_count))
        taken_angles = []
        for branch_index, level in enumerate(levels):
            branch_rng = random.Random(ab.stable_hash(
                f"{habit_key}:primary:{stem_index}:{branch_index}"))
            # Uneven azimuths are intentional: these are fork paths, not a
            # radial bottlebrush repeated around the trunk. The golden step plus
            # the per-habit phase and the anti-repeat guard are what stop the
            # limb ladder reading as one repeated Y.
            angle = (seed_offset * 0.17 + stem_index * 1.91
                     + habit["phase"] + variant_phase
                     + branch_index * 2.39996
                     + branch_rng.uniform(-0.30, 0.30)
                     + 0.42 * level)
            angle = _avoid_repeated_angle(angle, taken_angles)
            taken_angles.append(angle)
            if species in ("Linden", "Maple"):
                length_factor = 0.60 - 0.08 * level
                rise_factor = 1.00 - 0.12 * level
            elif species == "Birch":
                length_factor = 0.40 - 0.10 * level
                rise_factor = 0.22 - 0.06 * level
            elif species == "BirdCherry":
                length_factor = 0.43 - 0.12 * level
                rise_factor = 0.12 - 0.10 * level
            else:
                length_factor = 0.42 - 0.12 * level
                rise_factor = -droop * (0.42 + level) + 0.10 * (1.0 - level)
            length = stem_height * spread * length_factor * branch_rng.uniform(0.88, 1.12)
            origin = _point_on_polyline(trunk_points, level)
            radial = Vector((math.cos(angle), math.sin(angle), 0.0))
            rise = length * rise_factor
            bend = branch_rng.uniform(-0.08, 0.08) * length
            if species == "Birch":
                # Ascend out of the trunk, then let the outer bough hang.
                branch_points = [
                    origin,
                    origin + radial * length * 0.20 + Vector((0.0, 0.0, rise * 0.58)),
                    origin + radial * length * 0.48 + Vector((0.0, 0.0, rise * 1.08)),
                    origin + radial * length * 0.78 + Vector((0.0, 0.0, rise * 0.78 + bend)),
                    origin + radial * length + Vector((0.0, 0.0, rise * 0.18)),
                ]
            elif species in ("Linden", "Maple"):
                # Five broad, ascending forks form the open crown.
                branch_points = [
                    origin,
                    origin + radial * length * 0.19 + Vector((0.0, 0.0, rise * 0.30)),
                    origin + radial * length * 0.43 + Vector((0.0, 0.0, rise * 0.64)),
                    origin + radial * length * 0.72 + Vector((0.0, 0.0, rise * 0.91 + bend)),
                    origin + radial * length + Vector((0.0, 0.0, rise)),
                ]
            else:
                branch_points = [
                    origin,
                    origin + radial * length * 0.20 + Vector((0.0, 0.0, rise * 0.28)),
                    origin + radial * length * 0.48 + Vector((0.0, 0.0, rise * 0.67)),
                    origin + radial * length * 0.76 + Vector((0.0, 0.0, rise * 0.92 + bend)),
                    origin + radial * length + Vector((0.0, 0.0, rise)),
                ]
            primary_r1 = stem_height * caliper * (0.0074 - 0.0029 * level)
            taper = (1.0, 0.80, 0.62, 0.44, 0.28)
            if habit["fork"] is not None and branch_index == habit["fork"]:
                # VIS-082 co-dominant fork: the limb keeps a real share of the
                # bole's caliper and climbs to the crown apex, so the tree reads
                # as two load-bearing stems out of one crotch instead of a post
                # with five equal arms.
                climb = stem_height * (1.0 - level) * 0.94
                branch_points = [
                    origin,
                    origin + radial * climb * 0.10 + Vector((0.0, 0.0, climb * 0.34)),
                    origin + radial * climb * 0.17 + Vector((0.0, 0.0, climb * 0.63)),
                    origin + radial * climb * 0.23 + Vector((bend, 0.0, climb * 0.86)),
                    origin + radial * climb * 0.29 + Vector((0.0, 0.0, climb)),
                ]
                length = max(length, climb * 0.62)
                primary_r1 *= 1.55
                taper = (1.0, 0.92, 0.82, 0.70, 0.56)
            # Keep the outer taper continuous instead of collapsing the last
            # station to a needle, which is what turned every limb into a
            # smooth horn with a single sharp point.
            primary_radii = [primary_r1 * factor for factor in taper]
            primaries.append((branch_points, primary_radii, level, stem_index,
                              branch_index, angle, length, primary_r1))
    return stems, primaries


def winter_tree_variant(species, index, height, spread=1.0, branches=18,
                        twigs=2, berry=False, bark="AB_bark", droop=0.0,
                        seed_offset=0.0, snow_density=0.8, tier="near"):
    """Bare, connected winter deciduous tree built from shared fork paths.

    Near, Light and Far use the same rooted trunk and primary polylines. The
    lower tiers remove secondary/fine shoots and radial sides; no leaf crowns
    are authored on deciduous variants.
    """
    prefix = f"Winter{'' if tier == 'near' else tier.capitalize()}{species}_{index}"
    wood_vertices, wood_faces = [], []
    snow_vertices, snow_faces = [], []
    berry_vertices, berry_faces = [], []
    stems, primary_records = _winter_habit_records(
        species, index, height, spread, branches, seed_offset, droop)
    habit_key = f"Winter{species}_{index}:{seed_offset:.2f}"
    trunk_sides = 6 if tier == "near" else 4 if tier == "light" else 3
    branch_sides = 6 if tier == "near" else 4 if tier == "light" else 3
    for trunk_points, trunk_radii, stem_index in stems:
        _bare_branch(f"{prefix}_Trunk", wood_vertices, wood_faces,
                     trunk_points, trunk_radii, segments=trunk_sides)

    for points, radii, level, stem_index, branch_index, angle, length, primary_r1 in primary_records:
        _bare_branch(f"{prefix}_Primary{stem_index}_{branch_index}",
                     wood_vertices, wood_faces, points, radii,
                     segments=branch_sides)
        secondary_count = 0
        if tier == "near":
            secondary_count = 3 if species in ("Birch", "Linden", "Maple") else 2
        elif tier == "light":
            secondary_count = 2
        else:
            # One secondary per primary keeps the far tier from reading as a
            # handful of bare sticks on the horizon. The three-stemmed bird
            # cherry already carries its structure in the stems and stays at
            # the budget's top edge without them.
            secondary_count = 0 if species == "BirdCherry" else 1
        for secondary_index in range(secondary_count):
            secondary_rng = random.Random(ab.stable_hash(
                f"{habit_key}:secondary:{stem_index}:{branch_index}:{secondary_index}"))
            along = 0.42 + secondary_index * 0.20 + secondary_rng.uniform(-0.045, 0.045)
            secondary_origin = _point_on_polyline(points, along)
            primary_tangent = (_point_on_polyline(points, min(1.0, along + 0.08))
                               - _point_on_polyline(points, max(0.0, along - 0.08))).normalized()
            secondary_angle = angle + (1 if secondary_index % 2 else -1) * (
                0.72 + secondary_rng.uniform(-0.20, 0.20))
            secondary_direction = Vector((math.cos(secondary_angle),
                                          math.sin(secondary_angle),
                                          0.10 + secondary_rng.uniform(-0.10, 0.18)))
            secondary_direction.normalize()
            secondary_length = length * (0.28 - 0.025 * secondary_index)
            secondary_points = [
                secondary_origin,
                secondary_origin + (primary_tangent * 0.22 + secondary_direction * 0.78) * secondary_length * 0.34,
                secondary_origin + (primary_tangent * 0.10 + secondary_direction * 0.90) * secondary_length * 0.68,
                secondary_origin + secondary_direction * secondary_length,
            ]
            secondary_r1 = primary_r1 * 0.50
            secondary_radii = [secondary_r1 * factor
                               for factor in (1.0, 0.70, 0.44, 0.22)]
            _bare_branch(f"{prefix}_Secondary{stem_index}_{branch_index}_{secondary_index}",
                         wood_vertices, wood_faces, secondary_points,
                         secondary_radii, segments=branch_sides)
            # The near tier carries the fine winter haze, light keeps a share of
            # it so the 24-64 m band still reads as a tree instead of a fork,
            # and far stays at trunk plus one step. The three-stemmed bird cherry
            # spends its light-tier budget on stems rather than shoots.
            #
            # VIS-082: third-order wood is *rare*, not everywhere. The per-primary
            # twig budget is numerically unchanged, but it now concentrates on
            # one seeded secondary per primary - the single tuft real trees
            # actually build - while the remaining secondaries end as bare spur
            # wood. Repeating the same spray on every secondary was what made
            # each variant read as one feather duster scaled up.
            per_secondary = (0 if tier == "far"
                             else (1 if species == "BirdCherry" else 2) if tier == "light"
                             else max(3, min(5, twigs)))
            fine_budget = per_secondary * secondary_count
            bearing_index = (ab.stable_hash(
                f"{habit_key}:bearing:{stem_index}:{branch_index}")
                % secondary_count) if secondary_count else 0
            if fine_budget == 0 or not secondary_count:
                fine_count = 0
            elif secondary_index == bearing_index:
                fine_count = max(2, fine_budget - (secondary_count - 1))
            else:
                fine_count = 1
            for twig_index in range(fine_count):
                twig_rng = random.Random(ab.stable_hash(
                    f"{habit_key}:fine:{stem_index}:{branch_index}:{secondary_index}:{twig_index}"))
                # The clustered tuft spreads its shoots over the outer part of
                # one secondary instead of running past its tip.
                twig_step = 0.58 / max(1, fine_count - 1) if fine_count > 1 else 0.0
                twig_along = 0.32 + twig_index * twig_step + twig_rng.uniform(-0.035, 0.035)
                twig_origin = _point_on_polyline(secondary_points, twig_along)
                twig_tangent = (_point_on_polyline(secondary_points, min(1.0, twig_along + 0.08))
                                - _point_on_polyline(secondary_points, max(0.0, twig_along - 0.08))).normalized()
                twig_angle = secondary_angle + twig_rng.uniform(-0.80, 0.80)
                twig_direction = Vector((math.cos(twig_angle), math.sin(twig_angle),
                                         twig_rng.uniform(-0.30, 0.30)))
                twig_direction.normalize()
                twig_length = secondary_length * twig_rng.uniform(0.34, 0.58)
                twig_points = [
                    twig_origin,
                    twig_origin + (twig_tangent * 0.24 + twig_direction * 0.76) * twig_length * 0.34,
                    twig_origin + (twig_tangent * 0.10 + twig_direction * 0.90) * twig_length * 0.68,
                    twig_origin + twig_direction * twig_length,
                ]
                twig_radius = secondary_r1 * 0.34
                twig_radii = [twig_radius * factor for factor in (1.0, 0.62, 0.38, 0.20)]
                _bare_branch(f"{prefix}_Twig{stem_index}_{branch_index}_{secondary_index}_{twig_index}",
                             wood_vertices, wood_faces, twig_points, twig_radii,
                             segments=4)
                if berry and twig_index == 0 and branch_index % 2 == 0:
                    _append_berry(berry_vertices, berry_faces,
                                  _point_on_polyline(twig_points, 0.90),
                                  max(0.018, height * 0.008))

    # VIS-082: snow accents only on suitable upper surfaces. A limb that is
    # already descending at its mid-span cannot carry a mantle, so drooping
    # willow and bird-cherry tips stay bare wood and the white reads as
    # accumulated weight on the crown's topside.
    candidates = [record for record in primary_records
                  if (record[2] >= 0.58 or record[0][-1].z >= height * 0.58)
                  and record[0][min(2, len(record[0]) - 1)].z >= record[0][0].z]
    cap_count = min(len(candidates), 4 if tier == "near" else 3 if tier == "light" else 1)
    for cap_index, (points, radii, _level, _stem, _branch, _angle, _length, _r1) in enumerate(candidates[:cap_count]):
        _append_snow_cap(snow_vertices, snow_faces, points,
                         radii[min(2, len(radii) - 1)],
                         max(0.032, height * (0.013 if tier == "near" else 0.010)),
                         cap_index + int(seed_offset))

    objects = []
    trunk = ab.mesh_from_pydata(f"{prefix}_Trunk", wood_vertices, wood_faces)
    ab.assign_material(trunk, bark)
    objects.append(trunk)
    if snow_faces:
        snow = ab.mesh_from_pydata(f"{prefix}_Snow", snow_vertices, snow_faces)
        ab.assign_material(snow, "AB_snow")
        objects.append(snow)
    if berry_faces:
        berries = ab.mesh_from_pydata(f"{prefix}_Berries", berry_vertices, berry_faces)
        ab.assign_material(berries, "AB_foliage_rowan")
        objects.append(berries)
    _smooth_winter_surfaces(objects, tier)
    _assert_winter_variant(prefix, objects, tier)
    return objects


def _spruce_mass_plan(index, level_count, tier):
    """Group the fir's whorl ladder into 3-5 irregular crown masses (VIS-025).

    R025 read as one flat disc repeated up a single axis: every whorl sat at an
    even fraction of the trunk and shared one radial three-way angle step
    (`level * 1.17 + branch * tau/3`). A real spruce builds its needles in
    heavy, uneven masses with clear trunk between them.

    This schedule keeps the number of emitted boughs exactly as it was, so the
    triangle budget asserted by `_assert_winter_variant` and every LOD tier is
    unchanged. It only moves where a bough attaches, how far it reaches and
    which way it faces:

      * masses get uneven level counts and uneven vertical extents, so two
        whorls can sit almost on top of each other while the next band is open;
      * a fixed share of the crown height is reserved as inter-mass gaps, and
        the last bough of a mass shortens, which is what makes the просвет
        readable instead of leaving an empty pole;
      * every mass owns its own azimuth base and the levels inside it step by
        the golden angle, so no two masses share one radial twist;
      * the longest bough of a mass is chosen by the seed instead of always
        being the lowest level, which is what flattened the crown into saucers;
      * the snow cap rides the mass's dominant bough, so the mantle supports
        the mass instead of ringing every level;
      * the far tier runs at 45 percent modulation, so the distance silhouette
        stays one simple fir.
    """
    strength = 1.0 if tier != "far" else .45
    rng = random.Random(ab.stable_hash(f"spruce-masses:{index}:{tier}"))
    mass_count = min(max(3, 3 + (index - 1) % 3), max(3, level_count // 3))
    # Uneven masses: some carry five whorls, some carry two.
    weights = [rng.uniform(.55, 1.55) for _ in range(mass_count)]
    total_weight = sum(weights)
    counts = []
    used = 0
    for position, weight in enumerate(weights):
        if position == mass_count - 1:
            counts.append(max(1, level_count - used))
        else:
            share = max(1, int(round(level_count * weight / total_weight)))
            share = min(share, max(1, level_count - used - (mass_count - position - 1)))
            counts.append(share)
            used += counts[-1]
    counts[-1] = max(1, level_count - sum(counts[:-1]))
    # Vertical extents follow the level counts with a flattening exponent, and
    # a fixed gap share between the masses.
    gap = (.035 + .03 * rng.random()) * strength
    usable = 1.0 - gap * (mass_count - 1)
    extents = [(count / level_count) ** .85 for count in counts]
    extent_total = sum(extents) or 1.0
    extents = [extent / extent_total * usable for extent in extents]
    starts, walked = [], 0.0
    for extent in extents:
        starts.append(walked)
        walked += extent + gap
    plan, mass_of = {}, []
    level = 0
    for mass, count in enumerate(counts):
        angle_base = rng.uniform(0.0, math.tau)
        reach = rng.uniform(.82, 1.12) * (1.0 + .10 * strength * (mass % 2))
        dominant = rng.random() if count > 1 else .5
        cap_fraction = mass_count - 1 - mass
        for local in range(count):
            if level >= level_count:
                break
            u = local / (count - 1) if count > 1 else .5
            slot = {
                "fraction": starts[mass] + extents[mass] * u
                            + rng.uniform(-.012, .012) * strength,
                "angle": angle_base + local * 2.39996
                         + rng.uniform(-.16, .16) * strength,
                "reach": reach * (.72 + .48 * math.exp(
                    -((u - dominant) ** 2) / (2 * .17 ** 2))),
                "cap": abs(u - dominant) < .34 and local % 2 == 0,
                "cap_rank": cap_fraction,
            }
            # The final bough of a mass gives way to the gap above it.
            if local == count - 1 and mass < mass_count - 1:
                slot["reach"] *= (.62 + .16 * (1.0 - strength))
            plan[level] = slot
            mass_of.append(mass)
            level += 1
    for level in range(level, level_count):
        plan[level] = {"fraction": 1.0, "angle": rng.uniform(0.0, math.tau),
                       "reach": .9, "cap": False, "cap_rank": 0}
        mass_of.append(mass_count - 1)
    return plan, mass_of


def winter_spruce_variant(index, height, tier="near"):
    """Drooping, rounded needle boughs; every tier retains the same rooted habit.

    The bough ladder is tied to the trunk height, not to a fixed level count.
    A tall forest spruce reuses one level per ~0.75 m of trunk, so its crown
    keeps the same bough rhythm as the young sapling instead of stretching into
    a bare pole with a handful of separated tiers.  Bough reach also shrinks
    asymptotically with height so a 26 m spruce does not sweep a 14 m disc over
    the road envelope the way a uniformly scaled sapling would.
    """
    prefix = f"Winter{'' if tier == 'near' else tier.capitalize()}Spruce_{index}"
    bend_x, bend_y = (index - 2) * .024, (index % 2 - .5) * .024
    if index >= 4:
        # Old forest boles carry the same crooked load-bearing axis at every
        # LOD. Crown attachments below sample this actual axis as well.
        trunk_points = [Vector((height * (bend_x*t + .055*math.sin(t*math.pi)*(1 if index%2 else -1)),
                                height * (bend_y*t + .035*math.sin(t*math.pi*1.3)*t), height*t))
                        for t in (0, .08, .26, .50, .76, 1)]
        wood_vertices, wood_faces = [], []
        _append_polyline_tube(wood_vertices, wood_faces, trunk_points,
                              [height*r for r in (.047, .033, .027, .018, .009, .001)],
                              sides=6 if tier == "near" else 4 if tier == "light" else 3)
        for limb in range(3 if tier == "near" else 2 if tier == "light" else 0):
            root = _point_on_polyline(trunk_points, .18 + limb*.11)
            angle = index*1.31 + limb*2.7
            direction = Vector((math.cos(angle), math.sin(angle), 0))
            side = Vector((-direction.y, direction.x, 0))
            length = height * (.21 - .025*limb)
            limb_points = [root,
                           root + direction*length*.42 - Vector((0,0,length*.10)),
                           root + direction*length*.76 + side*length*.12,
                           root + direction*length + side*length*.21 + Vector((0,0,length*.18))]
            if tier == "light":
                limb_points = [limb_points[0], limb_points[2], limb_points[3]]
            radii = [height*r for r in ((.010, .007, .004, .0008) if tier == "near" else (.010, .004, .0008))]
            _bare_branch(f"{prefix}_OldLimb{limb}", wood_vertices, wood_faces,
                         limb_points, radii, segments=4 if tier == "near" else 3)
        trunk = ab.mesh_from_pydata(f"{prefix}_Trunk", wood_vertices, wood_faces)
    else:
        trunk = ab.make_cylinder(f"{prefix}_Trunk", (0, 0, 0), height * .029, height * .001,
                                 height, segments=6)
        _shape_trunk(trunk, height, bend_x, bend_y, index + 13.0)
        trunk_points = [Vector((0,0,0)), Vector((bend_x*height,bend_y*height,height))]
    ab.assign_material(trunk, "AB_bark_dark")
    vertices, faces, snow_vertices, snow_faces = [], [], [], []
    # A forest spruce carries one bough whorl roughly every metre of trunk; the
    # level count is therefore driven by height, and the per-tier geometry
    # drops sides/stations on the tall trees so every tier still lands inside
    # the shared Agent B triangle budget asserted below. Vertical whorl density
    # is spent first: a thinned whorl ladder is what turns a tall tree into a
    # blackened spike with sky between the levels.
    tall = height > 10.0
    crown_base = .28 + .02 * math.sin(index * 2.17) if index >= 4 else .12
    level_count = max(12, min(26, int(round(height * 0.85))))
    if tier == "light":
        level_count = max(12, min(14 if tall else 16, int(round(level_count * 0.68))))
    elif tier == "far":
        level_count = min(24, level_count) if tall else max(9, int(round(level_count * 0.50)))
    last_level = level_count - 1
    # Heavy, overlapping winter crowns. Shrinking bough reach as the trunk grew
    # was wrong for a forest wall: the ring has to close as a mass, not as a row
    # of poles, so reach falls only gently with height.
    bough_ratio = .27 if height <= 8.0 else max(.228, .28 - (height - 8.0) * .0022)
    levels = range(level_count)
    # The young village firs keep their original two-branch far tier, which is
    # already at its budget ceiling. A tall forest spruce keeps the full radial
    # crown in every tier, because a two-branch far tier is exactly what leaves
    # a pole silhouette on the skyline.
    cap_limit = (5 if tall else 4) if tier == "near" else 3 if tier == "light" else 1
    # VIS-025: the crown is read as masses, so the snow mantle is spent on the
    # dominant bough of the upper masses first, not on the first level that
    # happens to cross an arbitrary height fraction.
    mass_plan, _masses = _spruce_mass_plan(index, level_count, tier)
    cap_levels = set()
    for level in reversed(range(level_count)):
        if len(cap_levels) >= cap_limit:
            break
        if mass_plan[level]["cap"]:
            cap_levels.add(level)
    if not cap_levels:
        # Every crown keeps at least one supported snow mantle on its top mass,
        # whatever the seeded dominant position turned out to be.
        cap_levels.add(level_count - 1)
    branches = (0, 2) if tier == "far" and not tall else (0, 1, 2)
    if height > 10.0:
        # The tallest dominants trade bough smoothness for whorl count: at this
        # height a denser ladder reads as forest, a smoother single bough does
        # not.
        stations = 5 if tier == "near" and level_count <= 22 else 4 if tier == "near" else 3 if tier == "light" else 2
        sides = 5 if tier == "near" else 5 if tier == "light" else 3
    else:
        stations = 6 if tier == "near" else 3
        sides = 6 if tier == "near" else 5 if tier == "light" else 3
    caps = 0
    for level in levels:
        for branch in branches:
            if tier == "far" and level >= last_level - 1 and branch != 0:
                continue
            rng = random.Random(ab.stable_hash(f"spruce:{index}:{level}:{branch}"))
            slot = mass_plan[level]
            # Mass-local heading plus the bough's own three-way offset. The
            # offsets are uneven, because an exact tau/3 with one shared twist
            # is what turned every level into the same visible saucer.
            angle = slot["angle"] + branch * (2.09 + .34 * ((index + branch) % 3))
            # Mature trees carry a heavy upper canopy over visible trunks.
            # The same crown base and whorl count at every LOD keep this habit;
            # young firs and the smaller transition stand retain their shape.
            spread = rng.uniform(.78, 1.08) if tall else rng.uniform(.62, 1.1)
            spread *= slot["reach"]
            length = height * bough_ratio * (1.0 - level / level_count) ** .65 * spread
            first_whorl = crown_base if tall else .22
            whorl_span = 1.0 - crown_base if tall else .74
            whorl_height = height * (first_whorl + whorl_span * slot["fraction"]
                                      + rng.uniform(-.022, .022))
            # Every bough stays rooted on the trunk's actual crooked axis.
            centre = _point_on_polyline(trunk_points, whorl_height / height)
            # A long bough on a tall trunk cannot keep the sapling's droop without
            # reaching the ground, and the vertex clamp that keeps it above zero
            # would then flatten the tube and break its winding. Cap the droop so
            # the lowest whorl always keeps a share of the trunk clear.
            droop = .46 if tall else .52
            droop = min(droop, max(0.0, centre.z - height * .05) / max(length, 1e-6))
            direction = Vector((math.cos(angle), math.sin(angle), 0))
            points, radii = [], []
            for station in range(stations):
                t = station / (stations - 1)
                point = centre + direction * length * t
                point.z -= length * (droop * t - .10 * math.sin(math.pi * t))
                points.append(point)
                # Slender needle boughs: the previous band reached 47 percent
                # of the bough length at mid-span, so twelve tiers of wide flat
                # blades stacked into visible saucers. Halving the caliper lets
                # the tiers merge into one soft crown instead.
                radii.append(length * (.13 + .21 * math.sin(math.pi * t)) * (1 - .90 * t))
            if tall and tier == "far":
                # Four triangles per spray preserve the whorl count. The old
                # eight-triangle two-ring bough halved the whorls and left a
                # ladder of disconnected flags at ordinary street distances.
                middle = points[0].lerp(points[-1], .38)
                tangent = (points[-1] - points[0]).normalized()
                side = tangent.cross(Vector((0, 0, 1))).normalized()
                up = side.cross(tangent).normalized()
                base = len(vertices)
                # Root, two shoulders and tip: all four faces belong to a
                # bough attached to the trunk, not a floating triangular base.
                vertices.extend(tuple(point) for point in (
                    points[0], middle + side * length * .22 + up * length * .12,
                    middle - side * length * .22 + up * length * .12, points[-1]))
                faces.extend(((base, base + 1, base + 2),
                              (base, base + 3, base + 1),
                              (base + 1, base + 3, base + 2),
                              (base + 2, base + 3, base)))
            elif tall:
                # Keep a continuous foliage mass. Scalloped cross-sections
                # break the long straight blade without thinning it into a
                # bare branch skeleton at street distance.
                fractions = ([0, .12, .25, .38, .50, .63, .75, .88, 1]
                             if tier == "near" else [0, .30, .64, 1])
                lobes = ([.08, .20, .14, .23, .14, .19, .105, .13, .012]
                         if tier == "near" else [.08, .22, .18, .012])
                side = Vector((-direction.y, direction.x, 0))
                crown_points, crown_radii = [], []
                for t, radius in zip(fractions, lobes):
                    point = _point_on_polyline(points, t)
                    point += side * length * .055 * math.sin(t * math.tau + angle) * math.sin(t * math.pi)
                    crown_points.append(point)
                    crown_radii.append(length * radius * rng.uniform(.88, 1.12))
                _append_polyline_tube(vertices, faces, crown_points, crown_radii,
                                      sides=4, flatten=.95)
            else:
                _append_polyline_tube(vertices, faces, points, radii,
                                      sides=sides, flatten=.80)
            if tier == "near" and not tall:
                attachment = _point_on_polyline(points, .46)
                fork_direction = Vector((math.cos(angle + .75), math.sin(angle + .75), -.45))
                fork_points = [attachment + fork_direction * length * .54 * t
                               for t in (0.0, .30, 0.68, 1.0)]
                _append_polyline_tube(vertices, faces, fork_points,
                                      [length * r for r in (.09, .16, .10, .009)],
                                      sides=5, flatten=.72)
                # One shorter spray between the tiers breaks the radial
                # symmetry that made every level read as the same saucer. A
                # tall forest spruce already carries a whorl every metre, so the
                # spray would only spend triangles on detail the crown hides.
                if not tall:
                    spray_angle = angle + 1.94
                    spray_direction = Vector((math.cos(spray_angle), math.sin(spray_angle), 0))
                    spray_length = length * .62
                    spray_base = centre + Vector((0.0, 0.0, -height * .012))
                    spray_points = [
                        spray_base + spray_direction * spray_length * t
                        - Vector((0.0, 0.0, spray_length * .46 * t))
                        for t in (0.0, 0.34, 0.70, 1.0)
                    ]
                    _append_polyline_tube(vertices, faces, spray_points,
                                          [spray_length * r for r in (.11, .17, .10, .008)],
                                          sides=5, flatten=.76)
            if level in cap_levels and caps < cap_limit and branch == 0:
                # The cap is embedded in the actual upper needle bough,
                # following the same drooping path rather than its old level.
                radius = (length * .15 if tall and tier != "far" else
                          length * (.19 + .28 * math.sin(math.pi * .58)) * (1 - .88 * .58))
                _append_snow_cap(snow_vertices, snow_faces, points,
                                 max(.001, radius * .80 - .003) / .42,
                                 radius * .72, level + index)
                caps += 1
    if tall:
        # A narrow inner crown joins the sprays without owning the silhouette.
        heights = [crown_base + (1.0 - crown_base) * t
                   for t in (0, .205, .489, .75, 1.0)]
        points = [_point_on_polyline(trunk_points, t) for t in heights]
        _append_polyline_tube(vertices, faces, points,
                              [height * r for r in (.035, .05, .03, .012, .001)],
                              sides=4)
    needles = ab.mesh_from_pydata(f"{prefix}_Needles", vertices, faces)
    ab.assign_material(needles, "AB_foliage_spruce")
    needle_heights = [vertex.co.z for vertex in needles.data.vertices]
    assert min(needle_heights) > height * .02 and max(needle_heights) > height * .8, (
        prefix, height, min(needle_heights), max(needle_heights))
    snow = ab.mesh_from_pydata(f"{prefix}_Snow", snow_vertices, snow_faces)
    ab.assign_material(snow, "AB_snow")
    objects = [trunk, needles, snow]
    _smooth_winter_surfaces(objects, tier)
    for polygon in needles.data.polygons:
        polygon.use_smooth = tall or tier != "far"
    _assert_winter_variant(prefix, objects, tier)
    return objects


def winter_old_branch_variant(index, height, tier="near"):
    """VIS-075 (H3-1): five rare old-growth silhouettes with long, wrong limbs.

    The author selected exactly one property from Darkwood: a few long,
    unpleasant branches that break the normal rhythm of the wood. Everything
    else about the reference (giant roots, a forest that behaves like one
    organism, faces in the bark, hypertrophied scale) is out of contract, so
    this family stays a *botanically plausible* old broadleaf:

      * a heavy, real bole - root flare, taper, a broken or split crown, the
        same load-bearing axis at every LOD;
      * 3-5 primaries that reach far past the species norm at uneven heights
        and uneven headings, one of them sweeping low and sideways (the beat
        that breaks the rhythm), none of them a mirrored twin;
      * second order only where a real tree would rebuild wood, third order
        only as a single small tuft;
      * snow only on the topside of limbs that could physically carry it.

    Heights stay in the 11-17 m band of an old linden or birch at the wood
    edge; nothing here is scaled up into fantasy. Placement is capped by
    AgentBFoliagePlan at about ten percent of the near forest hero slots.
    """
    prefix = f"Winter{'' if tier == 'near' else tier.capitalize()}OldBranch_{index}"
    rng = random.Random(ab.stable_hash(f"oldbranch:{index}"))
    lean_x = rng.uniform(-0.07, 0.07) + (0.05 if index % 2 else -0.04)
    lean_y = rng.uniform(-0.05, 0.05)
    trunk_points = _winter_trunk_centerline(
        height, lean_x, lean_y, seed=37.0 * index, count=7)
    # Old-growth caliper with a real root flare and a broken top: the crown ends
    # because the leader failed, not because the generator ran out of levels.
    collapse = (1.0, 1.0, 0.96, 0.86, 0.72, 0.55, 0.30)
    flare = (1.55, 1.34, 1.06, 0.86, 0.66, 0.44, 0.26)
    trunk_radii = [height * 0.030 * flare[i] * collapse[i] for i in range(7)]
    wood_vertices, wood_faces = [], []
    _bare_branch(f"{prefix}_Trunk", wood_vertices, wood_faces, trunk_points,
                 trunk_radii,
                 segments=8 if tier == "near" else 6 if tier == "light" else 4)

    # Long, uneven reach limbs. The schedule is authored per variant, so the
    # five silhouettes differ in where they break the rhythm; a random loop
    # would produce five copies of one idea.
    limb_schedule = {
        1: ((0.34, 1.42, -0.30, 0.24), (0.52, 1.16, 0.62, 0.10),
            (0.69, 0.92, 2.31, -0.18), (0.83, 0.58, 4.02, 0.34)),
        2: ((0.28, 1.66, -0.12, -0.42), (0.47, 0.86, 1.72, 0.18),
            (0.62, 1.28, 3.30, -0.10), (0.76, 1.02, 4.90, 0.28),
            (0.88, 0.54, 0.62, 0.06)),
        3: ((0.38, 1.28, 2.96, -0.34), (0.55, 1.52, 0.84, 0.22),
            (0.72, 0.74, 4.42, -0.12), (0.86, 0.62, 2.10, 0.30)),
        4: ((0.31, 1.72, -0.55, -0.30), (0.49, 1.04, 1.48, 0.20),
            (0.64, 1.38, 3.62, -0.22), (0.79, 0.86, 5.20, 0.16),
            (0.90, 0.48, 0.30, 0.34)),
        5: ((0.36, 1.34, 1.12, -0.26), (0.53, 1.62, 3.86, 0.18),
            (0.70, 0.94, 5.44, -0.30), (0.84, 0.66, 2.48, 0.26)),
    }.get(index, ((0.35, 1.30, 0.4, 0.1), (0.55, 1.10, 2.6, -0.2),
                  (0.75, 0.80, 4.6, 0.25)))
    if tier == "far":
        limb_schedule = limb_schedule[:4]
    elif tier == "light":
        limb_schedule = limb_schedule[:max(3, len(limb_schedule) - 1)]

    primaries = []
    for limb_index, (level, reach, azimuth, dip) in enumerate(limb_schedule):
        origin = _point_on_polyline(trunk_points, level)
        direction = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
        length = height * reach * 0.36
        # A long limb cannot stay straight: it lifts out of the bole, then
        # hangs under its own weight, then breaks. The kink is what makes the
        # silhouette read as old wood rather than as a swept antenna.
        tip_dip = dip - (0.34 + 0.12 * reach)
        points = [
            origin,
            origin + direction * length * 0.26 + Vector((0.0, 0.0, length * 0.16)),
            origin + direction * length * 0.55 + Vector((0.0, 0.0, length * 0.10)),
            origin + direction * length * 0.80 + Vector((0.0, 0.0, length * tip_dip * 0.28)),
            origin + direction * length + Vector((0.0, 0.0, length * tip_dip * 0.52)),
        ]
        if tier == "far":
            points = [points[0], points[2], points[4]]
        base = height * 0.017 * (1.25 - 0.55 * level)
        radii = [base * factor for factor in
                 ((1.0, 0.74, 0.54, 0.36, 0.18) if tier == "near"
                  else (1.0, 0.70, 0.50, 0.34, 0.16) if tier == "light"
                  else (1.0, 0.62, 0.28))]
        _bare_branch(f"{prefix}_Limb{limb_index}", wood_vertices, wood_faces,
                     points, radii,
                     segments=7 if tier == "near" else 5 if tier == "light" else 4)
        primaries.append((points, radii, level, limb_index))

        # Second order only on the older half of each long limb, and only two
        # per limb: sparse rebuilt wood, not a feathered spray.
        if tier == "far":
            continue
        for shoot in range(2 if tier == "near" else 1):
            along = 0.46 + shoot * 0.26
            shoot_origin = _point_on_polyline(points, along)
            tangent = (_point_on_polyline(points, min(1.0, along + 0.10))
                       - _point_on_polyline(points, max(0.0, along - 0.10))).normalized()
            side_angle = azimuth + (1.15 if shoot % 2 else -1.02) + rng.uniform(-0.22, 0.22)
            side = Vector((math.cos(side_angle), math.sin(side_angle), 0.0))
            shoot_length = length * (0.30 - 0.06 * shoot)
            shoot_points = [
                shoot_origin,
                shoot_origin + (tangent * 0.20 + side * 0.80) * shoot_length * 0.42
                + Vector((0.0, 0.0, shoot_length * 0.16)),
                shoot_origin + (tangent * 0.08 + side * 0.92) * shoot_length * 0.78
                + Vector((0.0, 0.0, -shoot_length * 0.10)),
                shoot_origin + side * shoot_length + Vector((0.0, 0.0, -shoot_length * 0.42)),
            ]
            shoot_radius = radii[min(2, len(radii) - 1)] * 0.52
            _bare_branch(f"{prefix}_Shoot{limb_index}_{shoot}", wood_vertices,
                         wood_faces, shoot_points,
                         [shoot_radius * factor
                          for factor in (1.0, 0.66, 0.40, 0.20)],
                         segments=5 if tier == "near" else 4)
            # Third order: one small tuft per tree, on the first limb only.
            if tier == "near" and limb_index == 0 and shoot == 0:
                for twig_index in range(3):
                    twig_origin = _point_on_polyline(shoot_points, 0.55 + twig_index * 0.14)
                    twig_angle = side_angle + (twig_index - 1) * 0.74
                    twig_direction = Vector((math.cos(twig_angle), math.sin(twig_angle),
                                             0.24 - 0.20 * twig_index))
                    twig_length = shoot_length * 0.38
                    _bare_branch(f"{prefix}_Tuft{twig_index}", wood_vertices, wood_faces,
                                 [twig_origin,
                                  twig_origin + twig_direction * twig_length * 0.4,
                                  twig_origin + twig_direction * twig_length],
                                 [shoot_radius * factor for factor in (0.55, 0.30, 0.12)],
                                 segments=4)

    # Snow only where an old limb can carry it: the upper side of the two
    # highest primaries, never the low sweeping limb.
    snow_vertices, snow_faces = [], []
    caps = [record for record in primaries if record[2] >= 0.50]
    for cap_index, (points, radii, _level, _limb) in enumerate(
            caps[:4 if tier == "near" else 3 if tier == "light" else 1]):
        _append_snow_cap(snow_vertices, snow_faces, points,
                         radii[min(2, len(radii) - 1)],
                         max(0.05, height * (0.020 if tier == "near" else 0.014)),
                         cap_index + index)

    objects = []
    trunk = ab.mesh_from_pydata(f"{prefix}_Trunk", wood_vertices, wood_faces)
    ab.assign_material(trunk, "AB_bark_old")
    objects.append(trunk)
    if snow_faces:
        snow = ab.mesh_from_pydata(f"{prefix}_Snow", snow_vertices, snow_faces)
        ab.assign_material(snow, "AB_snow")
        objects.append(snow)
    _smooth_winter_surfaces(objects, tier)
    _assert_winter_variant(prefix, objects, tier,
                           limits={"near": (1100, 6000), "light": (360, 1500),
                                   "far": (70, 400)})
    return objects


def main() -> None:
    ab.setup_scene()
    objects: list = []
    variants: list[tuple[str, object]] = []

    for spec in ((1, 6.4, 0.03), (2, 7.6, -0.02), (3, 5.6, 0.05)):
        objects.extend(birch_variant(*spec))
        variants.append((f"Birch_{spec[0]}", None))
    for spec in ((1, 8.0, 0.5), (2, 9.2, -0.7), (3, 6.8, 0.9)):
        objects.extend(pine_variant(*spec))
        variants.append((f"Pine_{spec[0]}", None))
    for spec in ((1, 2.6), (2, 3.2), (3, 2.1)):
        objects.extend(spruce_variant(*spec))
        variants.append((f"Spruce_{spec[0]}", None))
    for spec in ((1, 0.7), (2, 0.55), (3, 0.9)):
        objects.extend(shrub_variant(*spec))
        variants.append((f"Shrub_{spec[0]}", None))
    for i in range(3):
        objects.extend(fern_variant(i))
        variants.append((f"Fern_{i}", None))
    for i in range(3):
        objects.extend(sedge_variant(i))
        variants.append((f"Sedge_{i}", None))
    for i in range(3):
        objects.extend(grass_tuft(i))
        variants.append((f"GrassTuft_{i}", None))
    for i in range(2):
        objects.extend(fallen_branch(i, 1.4 + i * 0.5))
        variants.append((f"FallenBranch_{i}", None))
    objects.extend(fallen_branch(2, 9.8))
    variants.append(("FallenBranch_2", None))
    objects.extend(moss_stone(0, 0.5))
    variants.append(("MossStone_0", None))
    objects.extend(dead_stump(0, 0.55))
    variants.append(("Stump_0", None))
    # Winter deciduous library (Act I season lock, decision_log 2026-09-10):
    # bare branch skeletons with snow, no leaf crowns.
    #
    # VIS-083: four village/forest families, each with three authored silhouette
    # variants (a Tatar street yard is birch and rowan, an old yard is a broad
    # linden or a willow by the water, the wood edge is the tall narrow birch).
    # VIS-082 gives every species its own readable bark slot. Birch/Linden/Rowan/
    # Willow variant 3 is a genuinely different habit, not variant 1 scaled.
    for spec in (
        # species, index, height, spread, branches, twigs, berries, bark, droop, seed
        ("Birch", 1, 6.2, 1.05, 20, 4, False, "AB_bark_birch", 0.55, 11.0),
        ("Birch", 2, 7.4, 0.95, 22, 4, False, "AB_bark_birch", 0.48, 23.0),
        ("Birch", 3, 8.6, 0.78, 21, 4, False, "AB_bark_birch", 0.30, 17.0),
        ("Linden", 1, 6.8, 0.86, 20, 4, False, "AB_bark_linden", 0.14, 31.0),
        ("Linden", 2, 5.6, 1.35, 18, 4, False, "AB_bark_linden", 0.12, 43.0),
        ("Linden", 3, 9.4, 1.05, 19, 4, False, "AB_bark_linden", 0.08, 37.0),
        ("Maple", 1, 6.0, 1.15, 19, 4, False, "AB_bark", 0.18, 53.0),
        ("Rowan", 1, 4.6, 1.20, 17, 4, True, "AB_bark_rowan", 0.24, 61.0),
        ("Rowan", 2, 5.4, 1.10, 18, 4, True, "AB_bark_rowan", 0.22, 71.0),
        ("Rowan", 3, 5.9, 0.96, 16, 4, True, "AB_bark_rowan", 0.34, 67.0),
        ("BirdCherry", 1, 2.6, 1.30, 18, 4, True, "AB_bark_dark", 0.46, 83.0),
        ("Willow", 1, 4.2, 1.45, 20, 4, False, "AB_bark_willow", 0.85, 97.0),
        ("Willow", 2, 5.0, 1.40, 21, 4, False, "AB_bark_willow", 0.78, 103.0),
        ("Willow", 3, 5.8, 1.22, 19, 4, False, "AB_bark_willow", 0.94, 109.0),
    ):
        species, idx, h, spread, br, tw, berry, bark, droop, seed = spec
        objects.extend(winter_tree_variant(species, idx, h, spread, br, tw, berry,
                                           bark, droop, seed))
        variants.append((f"Winter{species}_{idx}", None))
    # Matching light variants keep the same rooted primary habit and drop only
    # fine branch detail for the 24-60 m native visibility tier.
    for spec in (
        ("Birch", 1, 6.2, 1.05, 20, 1, False, "AB_bark_birch", 0.55, 11.0),
        ("Birch", 2, 7.4, 0.95, 22, 1, False, "AB_bark_birch", 0.48, 23.0),
        ("Birch", 3, 8.6, 0.78, 21, 1, False, "AB_bark_birch", 0.30, 17.0),
        ("Linden", 1, 6.8, 0.86, 20, 1, False, "AB_bark_linden", 0.14, 31.0),
        ("Linden", 2, 5.6, 1.35, 18, 1, False, "AB_bark_linden", 0.12, 43.0),
        ("Linden", 3, 9.4, 1.05, 19, 1, False, "AB_bark_linden", 0.08, 37.0),
        ("Maple", 1, 6.0, 1.15, 19, 1, False, "AB_bark", 0.18, 53.0),
        ("Rowan", 1, 4.6, 1.20, 17, 1, True, "AB_bark_rowan", 0.24, 61.0),
        ("Rowan", 2, 5.4, 1.10, 18, 1, True, "AB_bark_rowan", 0.22, 71.0),
        ("Rowan", 3, 5.9, 0.96, 16, 1, True, "AB_bark_rowan", 0.34, 67.0),
        ("BirdCherry", 1, 2.6, 1.30, 18, 1, True, "AB_bark_dark", 0.46, 83.0),
        ("Willow", 1, 4.2, 1.45, 20, 1, False, "AB_bark_willow", 0.85, 97.0),
        ("Willow", 2, 5.0, 1.40, 21, 1, False, "AB_bark_willow", 0.78, 103.0),
        ("Willow", 3, 5.8, 1.22, 19, 1, False, "AB_bark_willow", 0.94, 109.0),
    ):
        species, idx, h, spread, br, tw, berry, bark, droop, seed = spec
        light_objects = winter_tree_variant(species, idx, h, spread, br, tw, berry,
                                            bark, droop, seed, snow_density=0.55,
                                            tier="light")
        objects.extend(light_objects)
        variants.append((f"WinterLight{species}_{idx}", None))

    # Far tier for every full winter deciduous family. It preserves the same
    # trunk/primary branch seed and removes fine shoots for the 60 m+ range.
    for spec in (
        ("Birch", 1, 6.2, 1.05, 20, 0, False, "AB_bark_birch", 0.55, 11.0),
        ("Birch", 2, 7.4, 0.95, 22, 0, False, "AB_bark_birch", 0.48, 23.0),
        ("Birch", 3, 8.6, 0.78, 21, 0, False, "AB_bark_birch", 0.30, 17.0),
        ("Linden", 1, 6.8, 0.86, 20, 0, False, "AB_bark_linden", 0.14, 31.0),
        ("Linden", 2, 5.6, 1.35, 18, 0, False, "AB_bark_linden", 0.12, 43.0),
        ("Linden", 3, 9.4, 1.05, 19, 0, False, "AB_bark_linden", 0.08, 37.0),
        ("Maple", 1, 6.0, 1.15, 19, 0, False, "AB_bark", 0.18, 53.0),
        ("Rowan", 1, 4.6, 1.20, 17, 0, True, "AB_bark_rowan", 0.24, 61.0),
        ("Rowan", 2, 5.4, 1.10, 18, 0, True, "AB_bark_rowan", 0.22, 71.0),
        ("Rowan", 3, 5.9, 0.96, 16, 0, True, "AB_bark_rowan", 0.34, 67.0),
        ("BirdCherry", 1, 2.6, 1.30, 18, 0, True, "AB_bark_dark", 0.46, 83.0),
        ("Willow", 1, 4.2, 1.45, 20, 0, False, "AB_bark_willow", 0.85, 97.0),
        ("Willow", 2, 5.0, 1.40, 21, 0, False, "AB_bark_willow", 0.78, 103.0),
        ("Willow", 3, 5.8, 1.22, 19, 0, False, "AB_bark_willow", 0.94, 109.0),
    ):
        species, idx, h, spread, br, tw, berry, bark, droop, seed = spec
        far_objects = winter_tree_variant(species, idx, h, spread, br, tw, berry,
                                          bark, droop, seed, snow_density=0.55,
                                          tier="far")
        objects.extend(far_objects)
        variants.append((f"WinterFar{species}_{idx}", None))

    # Spruce ladder: 1-2 are the young firs that dress the village-side
    # transition, 3-6 are the tall forest stand the Act I perimeter ring is
    # built from. The tall tiers reuse the same generator and rooted habit so
    # the trunk, bough whorls and crown scale together instead of stretching a
    # sapling along Y.
    spruce_specs = ((1, 2.8), (2, 3.4), (3, 13.5), (4, 18.5), (5, 24.5), (6, 30.5))
    for spec in spruce_specs:
        objects.extend(winter_spruce_variant(*spec, tier="near"))
        variants.append((f"WinterSpruce_{spec[0]}", None))
    for spec in spruce_specs:
        objects.extend(winter_spruce_variant(*spec, tier="light"))
        variants.append((f"WinterLightSpruce_{spec[0]}", None))
    for spec in spruce_specs:
        objects.extend(winter_spruce_variant(*spec, tier="far"))
        variants.append((f"WinterFarSpruce_{spec[0]}", None))

    # VIS-075 (H3-1): the rare old-growth family with long, unsettling limbs.
    # Five authored silhouettes plus their matching light/far tiers, so one
    # rooted habit survives the LOD switch (VIS-027). This family is never
    # auto-scattered: AgentBFoliagePlan places each instance by hand and keeps
    # the budget at roughly one tree per ten near forest hero slots.
    old_branch_specs = ((1, 12.6), (2, 15.4), (3, 11.2), (4, 16.8), (5, 13.8))
    for spec in old_branch_specs:
        objects.extend(winter_old_branch_variant(*spec, tier="near"))
        variants.append((f"WinterOldBranch_{spec[0]}", None))
    for spec in old_branch_specs:
        objects.extend(winter_old_branch_variant(*spec, tier="light"))
        variants.append((f"WinterLightOldBranch_{spec[0]}", None))
    for spec in old_branch_specs:
        objects.extend(winter_old_branch_variant(*spec, tier="far"))
        variants.append((f"WinterFarOldBranch_{spec[0]}", None))

    # Lay variants on a 12 m grid; Godot composer locates each by name.
    bpy.context.view_layer.update()
    offsets: list[str] = []
    variant_origins: dict[str, tuple[float, float]] = {}
    for idx, (name, _) in enumerate(variants):
        ox = (idx % 8) * 12.0
        oy = (idx // 8) * 12.0
        variant_origins[name] = (ox, oy)
        offsets.append(f"{name}:{ox:.1f},{oy:.1f}")
        for obj in objects:
            if obj.name == name or obj.name.startswith(name + "_"):
                ab.translate_vertices(obj, ox, oy, 0.0)
                if name.startswith("Shrub_"):
                    for vertex in obj.data.vertices:
                        point = obj.matrix_local @ vertex.co
                        assert abs(point.x - ox) < 2.5 and abs(point.y - oy) < 2.5 and -.3 < point.z < 2.0, (
                            f"Shrub part escaped its preview cell: {obj.name} {tuple(point)}")

    # Keep a native root marker at the authored board origin. Mesh vertices
    # already contain their preview-cell translation, so parent children with
    # world matrices preserved; the marker is the only authoritative pivot.
    winter_roots: list[bpy.types.Object] = []
    winter_names = {name for name, _ in variants if name.startswith("Winter")}
    for name in sorted(winter_names):
        ox, oy = variant_origins[name]
        root_marker = bpy.data.objects.new(f"{name}_Root", None)
        bpy.context.scene.collection.objects.link(root_marker)
        root_marker.location = (ox, oy, 0.0)
        root_marker["native_root_pivot"] = True
        root_marker["winter_variant"] = name
        bpy.context.view_layer.update()
        for obj in objects:
            if obj.name == name or obj.name.startswith(name + "_"):
                world_matrix = obj.matrix_world.copy()
                obj.parent = root_marker
                obj.matrix_world = world_matrix
        winter_roots.append(root_marker)
    bpy.context.view_layer.update()

    root = os.environ.get("AGENTB_OUT", HERE)
    glb_path = os.path.join(root, "game/assets/models/agent_b_act1",
                            "agentb_foliage_kit.glb")
    blend_path = os.path.join(root, "assets/source/blender/agent_b_act1",
                              "agentb_foliage_kit.blend")
    previous_glb_digest = None
    if os.path.isfile(glb_path):
        with open(glb_path, "rb") as handle:
            previous_glb_digest = hashlib.sha256(handle.read()).digest()
    ab.export_glb(objects + winter_roots, glb_path,
                  "URMAN_AgentB_FoliageKit", preserve_hierarchy=True)
    with open(glb_path, "rb") as handle:
        current_glb_digest = hashlib.sha256(handle.read()).digest()
    # Blender creates a transient Render Result image datablock in some
    # headless sessions.  It is not an authored asset, so keep the saved
    # source scene as texture/image-free as the exported GLB.
    for image in list(bpy.data.images):
        bpy.data.images.remove(image)
    if previous_glb_digest != current_glb_digest or not os.path.isfile(blend_path):
        ab.save_blend(blend_path)
    else:
        print("AGENTB_BLEND_SAVE skipped; exported GLB is unchanged")
    ab.report_kit("agentb_foliage_kit", objects, glb_path)
    print("AGENTB_VARIANTS " + ";".join(offsets))


if __name__ == "__main__":
    main()
