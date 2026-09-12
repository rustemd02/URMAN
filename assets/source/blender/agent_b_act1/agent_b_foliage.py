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


def _assert_winter_variant(prefix, objects, tier):
    assert len(objects) <= 3, f"{prefix}: too many mesh objects"
    assert any(obj.name == f"{prefix}_Trunk" for obj in objects), prefix
    triangle_count = 0
    for obj in objects:
        assert obj.name.startswith(prefix + "_"), obj.name
        assert obj.data.materials and obj.data.materials[0].name in {
            "AB_bark", "AB_bark_birch", "AB_bark_dark", "AB_snow",
            "AB_foliage_rowan", "AB_foliage_spruce"
        }, obj.name
        for vertex in obj.data.vertices:
            assert all(math.isfinite(value) for value in vertex.co), (prefix, obj.name)
        triangle_count += sum(max(1, len(polygon.vertices) - 2)
                              for polygon in obj.data.polygons)
    trunk = next(obj for obj in objects if obj.name == f"{prefix}_Trunk")
    min_z = min(vertex.co.z for vertex in trunk.data.vertices)
    assert -1e-5 <= min_z <= 1e-5, (prefix, min_z)
    limits = {"near": (2000, 6000), "light": (600, 1500), "far": (100, 400)}
    low, high = limits[tier]
    assert low <= triangle_count <= high, (prefix, tier, triangle_count)
    print(f"AGENTB_WINTER_VARIANT {prefix} tier={tier} triangles={triangle_count} objects={len(objects)}")


def _winter_habit_records(species, index, height, spread, branches,
                          seed_offset, droop):
    """Return one rooted trunk and the shared primary fork paths for all LODs."""
    habit_key = f"Winter{species}_{index}:{seed_offset:.2f}"
    rng = random.Random(ab.stable_hash(habit_key))
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
        trunk_radii = [stem_height * (0.039 - 0.038 * t)
                       for t in (i / 5.0 for i in range(6))]
        stems.append((trunk_points, trunk_radii, stem_index))

        if species == "BirdCherry":
            primary_count = 2
        elif species in ("Birch", "Linden", "Maple"):
            primary_count = 5
        else:
            primary_count = 5
        if species in ("Linden", "Maple"):
            levels = tuple(0.28 + 0.20 * i / max(primary_count - 1, 1)
                           for i in range(primary_count))
        else:
            levels = tuple(0.34 + 0.56 * i / max(primary_count - 1, 1)
                           for i in range(primary_count))
        for branch_index, level in enumerate(levels):
            branch_rng = random.Random(ab.stable_hash(
                f"{habit_key}:primary:{stem_index}:{branch_index}"))
            # Uneven azimuths are intentional: these are fork paths, not a
            # radial bottlebrush repeated around the trunk.
            angle = (seed_offset * 0.17 + stem_index * 1.91
                     + branch_index * 1.23
                     + branch_rng.uniform(-0.43, 0.43))
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
            primary_r1 = stem_height * (0.016 - 0.008 * level)
            primary_radii = [primary_r1 * factor
                             for factor in (1.0, 0.82, 0.60, 0.36, 0.12)]
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
            secondary_count = 2 if species in ("Birch", "Linden", "Maple") else 3
        elif tier == "light":
            secondary_count = 2
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
                               for factor in (1.0, 0.66, 0.34, 0.10)]
            _bare_branch(f"{prefix}_Secondary{stem_index}_{branch_index}_{secondary_index}",
                         wood_vertices, wood_faces, secondary_points,
                         secondary_radii, segments=branch_sides)
            if tier != "near":
                continue
            fine_count = max(2, min(4, twigs))
            for twig_index in range(fine_count):
                twig_rng = random.Random(ab.stable_hash(
                    f"{habit_key}:fine:{stem_index}:{branch_index}:{secondary_index}:{twig_index}"))
                twig_along = 0.30 + twig_index * 0.16 + twig_rng.uniform(-0.035, 0.035)
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
                twig_radii = [twig_radius * factor for factor in (1.0, 0.56, 0.26, 0.08)]
                _bare_branch(f"{prefix}_Twig{stem_index}_{branch_index}_{secondary_index}_{twig_index}",
                             wood_vertices, wood_faces, twig_points, twig_radii,
                             segments=4)
                if berry and twig_index == 0 and branch_index % 2 == 0:
                    _append_berry(berry_vertices, berry_faces,
                                  _point_on_polyline(twig_points, 0.90),
                                  max(0.018, height * 0.008))

    candidates = [record for record in primary_records
                  if record[2] >= 0.58 or record[0][-1].z >= height * 0.58]
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


def winter_spruce_variant(index, height, tier="near"):
    """Drooping, rounded needle boughs; every tier retains the same rooted habit."""
    prefix = f"Winter{'' if tier == 'near' else tier.capitalize()}Spruce_{index}"
    trunk = ab.make_cylinder(f"{prefix}_Trunk", (0, 0, 0), height * .029, height * .001,
                             height, segments=6)
    _shape_trunk(trunk, height, (index - 2) * .024,
                 (index % 2 - .5) * .024, index + 13.0)
    ab.assign_material(trunk, "AB_bark_dark")
    vertices, faces, snow_vertices, snow_faces = [], [], [], []
    levels = range(12)
    branches = (0, 1, 2) if tier != "far" else (0, 2)
    stations = 6 if tier == "near" else 3
    cap_limit = 4 if tier == "near" else 3 if tier == "light" else 1
    caps = 0
    for level in levels:
        for branch in branches:
            if tier == "far" and level >= 10 and branch != 0:
                continue
            rng = random.Random(ab.stable_hash(f"spruce:{index}:{level}:{branch}"))
            angle = level * 1.17 + branch * math.tau / 3 + rng.uniform(-.22, .22)
            length = height * .27 * (1.0 - level / 12.0) ** .65 * rng.uniform(.62, 1.1)
            # Shared seeds keep gaps and uneven branch heights in all LODs.
            # Short boughs open the crown without widening its road envelope.
            centre = Vector((math.sin(index + level) * height * .016,
                             math.cos(index + level) * height * .012,
                             height * (.22 + .74 * level / 11 + rng.uniform(-.022, .022))))
            direction = Vector((math.cos(angle), math.sin(angle), 0))
            points, radii = [], []
            for station in range(stations):
                t = station / (stations - 1)
                point = centre + direction * length * t
                point.z -= length * (.40 * t - .12 * math.sin(math.pi * t))
                points.append(point)
                radii.append(length * (.19 + .28 * math.sin(math.pi * t)) * (1 - .88 * t))
            _append_polyline_tube(vertices, faces, points, radii,
                                  sides=6 if tier == "near" else 5 if tier == "light" else 3, flatten=.80)
            if tier == "near":
                attachment = _point_on_polyline(points, .46)
                fork_direction = Vector((math.cos(angle + .75), math.sin(angle + .75), -.45))
                fork_points = [attachment + fork_direction * length * .54 * t
                               for t in (0.0, .30, .68, 1.0)]
                _append_polyline_tube(vertices, faces, fork_points,
                                      [length * r for r in (.09, .16, .10, .009)],
                                      sides=5, flatten=.72)
            if level >= 4 and caps < cap_limit and branch == 0:
                # The cap is embedded in the actual upper needle bough,
                # following the same drooping path rather than its old level.
                radius = length * (.19 + .28 * math.sin(math.pi * .58)) * (1 - .88 * .58)
                _append_snow_cap(snow_vertices, snow_faces, points,
                                 max(.001, radius * .80 - .003) / .42,
                                 radius * .72, level + index)
                caps += 1
    needles = ab.mesh_from_pydata(f"{prefix}_Needles", vertices, faces)
    ab.assign_material(needles, "AB_foliage_spruce")
    needle_heights = [vertex.co.z for vertex in needles.data.vertices]
    assert min(needle_heights) > height * .02 and max(needle_heights) > height * .8
    snow = ab.mesh_from_pydata(f"{prefix}_Snow", snow_vertices, snow_faces)
    ab.assign_material(snow, "AB_snow")
    objects = [trunk, needles, snow]
    _smooth_winter_surfaces(objects, tier)
    for polygon in needles.data.polygons:
        polygon.use_smooth = tier != "far"
    _assert_winter_variant(prefix, objects, tier)
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
    objects.extend(moss_stone(0, 0.5))
    variants.append(("MossStone_0", None))
    objects.extend(dead_stump(0, 0.55))
    variants.append(("Stump_0", None))
    # Winter deciduous library (Act I season lock, decision_log 2026-09-10):
    # bare branch skeletons with snow, no leaf crowns.
    for spec in (
        # species, index, height, spread, branches, twigs, berries, bark, droop, seed
        ("Birch", 1, 6.2, 1.05, 20, 4, False, "AB_bark_birch", 0.55, 11.0),
        ("Birch", 2, 7.4, 0.95, 22, 4, False, "AB_bark_birch", 0.48, 23.0),
        ("Linden", 1, 6.8, 0.86, 20, 4, False, "AB_bark", 0.14, 31.0),
        ("Linden", 2, 5.6, 1.35, 18, 4, False, "AB_bark", 0.12, 43.0),
        ("Maple", 1, 6.0, 1.15, 19, 4, False, "AB_bark", 0.18, 53.0),
        ("Rowan", 1, 4.6, 1.20, 17, 4, True, "AB_bark", 0.24, 61.0),
        ("Rowan", 2, 5.4, 1.10, 18, 4, True, "AB_bark", 0.22, 71.0),
        ("BirdCherry", 1, 2.6, 1.30, 18, 4, True, "AB_bark_dark", 0.46, 83.0),
        ("Willow", 1, 4.2, 1.45, 20, 4, False, "AB_bark", 0.85, 97.0),
        ("Willow", 2, 5.0, 1.40, 21, 4, False, "AB_bark", 0.78, 103.0),
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
        ("Linden", 1, 6.8, 0.86, 20, 1, False, "AB_bark", 0.14, 31.0),
        ("Linden", 2, 5.6, 1.35, 18, 1, False, "AB_bark", 0.12, 43.0),
        ("Maple", 1, 6.0, 1.15, 19, 1, False, "AB_bark", 0.18, 53.0),
        ("Rowan", 1, 4.6, 1.20, 17, 1, True, "AB_bark", 0.24, 61.0),
        ("Rowan", 2, 5.4, 1.10, 18, 1, True, "AB_bark", 0.22, 71.0),
        ("BirdCherry", 1, 2.6, 1.30, 18, 1, True, "AB_bark_dark", 0.46, 83.0),
        ("Willow", 1, 4.2, 1.45, 20, 1, False, "AB_bark", 0.85, 97.0),
        ("Willow", 2, 5.0, 1.40, 21, 1, False, "AB_bark", 0.78, 103.0),
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
        ("Linden", 1, 6.8, 0.86, 20, 0, False, "AB_bark", 0.14, 31.0),
        ("Linden", 2, 5.6, 1.35, 18, 0, False, "AB_bark", 0.12, 43.0),
        ("Maple", 1, 6.0, 1.15, 19, 0, False, "AB_bark", 0.18, 53.0),
        ("Rowan", 1, 4.6, 1.20, 17, 0, True, "AB_bark", 0.24, 61.0),
        ("Rowan", 2, 5.4, 1.10, 18, 0, True, "AB_bark", 0.22, 71.0),
        ("BirdCherry", 1, 2.6, 1.30, 18, 0, True, "AB_bark_dark", 0.46, 83.0),
        ("Willow", 1, 4.2, 1.45, 20, 0, False, "AB_bark", 0.85, 97.0),
        ("Willow", 2, 5.0, 1.40, 21, 0, False, "AB_bark", 0.78, 103.0),
    ):
        species, idx, h, spread, br, tw, berry, bark, droop, seed = spec
        far_objects = winter_tree_variant(species, idx, h, spread, br, tw, berry,
                                          bark, droop, seed, snow_density=0.55,
                                          tier="far")
        objects.extend(far_objects)
        variants.append((f"WinterFar{species}_{idx}", None))

    for spec in ((1, 2.8), (2, 3.4)):
        objects.extend(winter_spruce_variant(*spec, tier="near"))
        variants.append((f"WinterSpruce_{spec[0]}", None))
    for spec in ((1, 2.8), (2, 3.4)):
        objects.extend(winter_spruce_variant(*spec, tier="light"))
        variants.append((f"WinterLightSpruce_{spec[0]}", None))
    for spec in ((1, 2.8), (2, 3.4)):
        objects.extend(winter_spruce_variant(*spec, tier="far"))
        variants.append((f"WinterFarSpruce_{spec[0]}", None))

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
