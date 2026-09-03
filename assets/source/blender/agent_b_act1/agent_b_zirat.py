"""Agent B Act I Kit 4: zirat (village cemetery).

Restrained and respectful: low wooden fence, simple uncarved markers, a
quiet path and a bench. No carved symbols, no gothic or occult decoration.
Marker stele shapes stay culturally neutral pending religious review
(CULTURAL REVIEW REQUIRED for any inscription or ornament).
"""

from __future__ import annotations

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import agent_b_common as ab  # noqa: E402

# Zirat occupies a gentle knoll east of the zirat road; centre (6.5, -70),
# roughly 16 x 12 m.


def terrain_height(xg, zg):
    """Approximation of the zirat knoll (matches agent_b_terrain h_terrain)."""
    h = (ab.fbm(xg * 0.03 + 40.0, zg * 0.03, 3) - 0.5) * 1.5
    west = max(0.0, min(1.0, (-12.0 - xg) / 34.0))
    h += west * (1.2 + ab.fbm(xg * 0.05, zg * 0.05 + 11.0, 2) * 0.9)
    dz = zg - (-70.0)
    dx = xg - 6.5
    knoll = math.exp(-(dx * dx + dz * dz) / 60.0)
    h += knoll * 0.55
    h += (ab.fbm(xg * 0.23, zg * 0.23, 2) - 0.5) * 0.22
    return h


def fence_rail(name, x0, y0, x1, y1, height=0.9):
    objs = []
    length = math.hypot(x1 - x0, y1 - y0)
    angle = math.atan2(y1 - y0, x1 - x0)
    count = max(2, int(length / 1.4))
    dx = (x1 - x0) / count
    dy = (y1 - y0) / count
    for i in range(count + 1):
        post = ab.make_box(f"{name}_Post{i}", -0.045, 0.045, -0.045, 0.045,
                           0.0, height)
        post.location = (x0 + dx * i, y0 + dy * i, 0.0)
        ab.assign_material(post, "AB_timber")
        objs.append(post)
    for rz in (height * 0.4, height * 0.85):
        rail = ab.make_box(f"{name}_Rail{'L' if rz < height else 'H'}",
                           -length / 2, length / 2, -0.025, 0.025, -0.03, 0.03)
        rail.location = ((x0 + x1) / 2, (y0 + y1) / 2, rz)
        rail.rotation_euler.z = angle
        ab.assign_material(rail, "AB_timber")
        objs.append(rail)
    return objs


def marker(name, x, y, kind, angle):
    """Simple uncarved fieldstone/stele markers. No inscriptions authored."""
    objs = []
    if kind == 0:
        stele = ab.make_box(f"{name}_Stele", -0.12, 0.12, -0.05, 0.05,
                            0.0, 0.62, jitter=0.015)
        ab.assign_material(stele, "AB_stone")
        stele.location = (x, y, 0)
        stele.rotation_euler.z = angle
        objs.append(stele)
    elif kind == 1:
        stone = ab.make_icosphere(f"{name}_Fieldstone", (x, y, 0.12), 0.2,
                                  subdivisions=1)
        ab.displace_object(stone, 0.05, 1.8, seed_offset=x * 3.1 + y)
        stone.scale = (1.0, 0.8, 0.75)
        ab.assign_material(stone, "AB_stone")
        stone.location = (x, y, 0.1)
        objs.append(stone)
    else:
        curb = ab.make_box(f"{name}_Curb", -0.55, 0.55, -0.03, 0.03, 0.0, 0.08)
        curb.location = (x, y, 0.02)
        curb.rotation_euler.z = angle
        ab.assign_material(curb, "AB_stone_dark")
        objs.append(curb)
        mound = ab.make_box(f"{name}_Mound", -0.5, 0.5, -0.18, 0.18, 0.0,
                            0.12, jitter=0.01)
        mound.location = (x, y, 0.1)
        mound.rotation_euler.z = angle
        ab.assign_material(mound, "AB_earth")
        objs.append(mound)
    return objs


def build_zirat():
    prefix = "Zirat"
    objs = []
    cx_g, cz_g = 6.5, -70.0

    # Perimeter fence with an open gate on the west (road) side.
    west_x = cx_g - 8.0
    east_x = cx_g + 8.0
    north_z = cz_g - 6.0   # toward forest
    south_z = cz_g + 6.0   # toward village
    # Blender y = -godot z.
    def B(xg, zg):
        return (xg, -zg)

    gate_gap = 1.2
    gate_cz = cz_g + 2.0
    # West fence split around the gate.
    objs.extend(fence_rail(f"{prefix}_FenceW_S", *B(west_x, south_z),
                           *B(west_x, gate_cz + gate_gap)))
    objs.extend(fence_rail(f"{prefix}_FenceW_N", *B(west_x, gate_cz - gate_gap),
                           *B(west_x, north_z)))
    objs.extend(fence_rail(f"{prefix}_FenceE", *B(east_x, south_z),
                           *B(east_x, north_z)))
    objs.extend(fence_rail(f"{prefix}_FenceS", *B(west_x, south_z),
                           *B(east_x, south_z)))
    objs.extend(fence_rail(f"{prefix}_FenceN", *B(west_x, north_z),
                           *B(east_x, north_z), height=0.7))
    # Gate posts (open gate, leaf lying aside).
    for gz in (gate_cz + gate_gap, gate_cz - gate_gap):
        post = ab.make_box(f"{prefix}_GatePost", -0.07, 0.07, -0.07, 0.07,
                           0.0, 1.15)
        bx, by = B(west_x, gz)
        post.location = (bx, by, 0.0)
        ab.assign_material(post, "AB_timber_dark")
        objs.append(post)
    leaf = ab.make_box(f"{prefix}_GateLeaf", -0.55, 0.55, -0.03, 0.03, 0.0,
                       1.0)
    leaf.location = (west_x + 0.35, -(gate_cz + gate_gap + 0.9), 0.45)
    leaf.rotation_euler.z = -1.25
    ab.assign_material(leaf, "AB_timber")
    objs.append(leaf)

    # Path from gate into the middle of the zirat (gate is on west fence).
    path = ab.make_patch(f"{prefix}_Path", (2.5, 68.0), 8.0, 1.6,
                         lambda xb, yb: terrain_height(xb, -yb),
                         z_pad=0.03, segments=8)
    ab.assign_material(path, "AB_earth_zirat_path")
    objs.append(path)

    # Markers: three small quiet groups, aligned, tilted by age.
    marker_defs = [
        (cx_g + 0.6, cz_g + 3.4, 0, 0.08),
        (cx_g + 2.6, cz_g + 3.2, 2, 0.0),
        (cx_g + 4.4, cz_g + 3.5, 1, 0.2),
        (cx_g + 1.2, cz_g + 0.6, 2, -0.04),
        (cx_g + 3.4, cz_g + 0.4, 0, -0.1),
        (cx_g + 5.6, cz_g + 0.8, 2, 0.06),
        (cx_g + 0.8, cz_g - 2.2, 1, 0.12),
        (cx_g + 3.0, cz_g - 2.6, 2, 0.0),
        (cx_g + 5.2, cz_g - 2.2, 0, 0.15),
        (cx_g + 6.8, cz_g - 4.4, 1, -0.08),
    ]
    for i, (mx, mz, kind, ang) in enumerate(marker_defs):
        for obj in marker(f"{prefix}_Marker{i}", mx, -mz, kind, ang):
            obj.location.z = terrain_height(obj.location.x, -obj.location.y)
            objs.append(obj)

    # Bench facing the village (south side of path).
    bench_seat = ab.make_box(f"{prefix}_BenchSeat", -0.7, 0.7, -0.12, 0.12,
                             0.42, 0.5)
    bench_seat.location = (cx_g + 2.0, -(cz_g + 4.3), 0)
    ab.assign_material(bench_seat, "AB_timber")
    objs.append(bench_seat)
    for side in (-1, 1):
        leg = ab.make_box(f"{prefix}_BenchLeg{'W' if side < 0 else 'E'}",
                          -0.05, 0.05, -0.05, 0.05, 0.0, 0.42)
        leg.location = (cx_g + 2.0 + side * 0.55, -(cz_g + 4.3), 0)
        ab.assign_material(leg, "AB_timber_dark")
        objs.append(leg)
    return objs


def main() -> None:
    ab.setup_scene()
    objects = build_zirat()
    root = os.environ.get("AGENTB_OUT", HERE)
    glb_path = os.path.join(root, "game/assets/models/agent_b_act1",
                            "agentb_zirat_kit.glb")
    blend_path = os.path.join(root, "assets/source/blender/agent_b_act1",
                              "agentb_zirat_kit.blend")
    ab.export_glb(objects, glb_path, "URMAN_AgentB_ZiratKit")
    ab.save_blend(blend_path)
    ab.report_kit("agentb_zirat_kit", objects, glb_path)


if __name__ == "__main__":
    main()
