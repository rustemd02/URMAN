"""Rebuild and export the active wet-road relief components deterministically.

Run with Blender 4.5+:
  blender --background --python assets/source/blender/act1/urman_wet_village_road_kit.py -- --root <repo>

The source GLB predates the canonical Y-up export and is rotated by the
connected-world extractor.  The generator builds in the legacy Blender source
basis, bakes every child-object transform into its mesh, then applies the
extractor's inverse +90-degree-X axis bake to all published geometry.  This
keeps the existing runtime correction while preventing imported components from
becoming vertical walls.  The other component names/material owners remain
unchanged.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy


ROOT_NAME = "URMAN_WetVillageRoadKit"
GLB_NAME = "urman_wet_village_road_kit.glb"
TARGETS = (
    "RoadCrown_SunkenWet",
    "RoadRuts_PuddleNear",
    "RoadRuts_PuddleFar",
    "RoadCrown_BranchWet",
    "RoadCrown_ApproachWorn",
)
REBUILT_SIDES = (
    "MuddyShoulder_Left",
    "MuddyShoulder_Right",
    "RoadsideDitch_Left",
    "RoadsideDitch_Right",
)
REBUILT_COMPONENTS = TARGETS + REBUILT_SIDES
ROAD_COMPONENTS = REBUILT_COMPONENTS
AXIS_BAKE_MARKER = "source_axis_bake_version"
AXIS_BAKE_VERSION = "legacy-godot-extractor-compensation-v1"
VEGETATION_COMPONENTS = (
    "GrassSedgeMass_Left",
    "GrassSedgeMass_Right",
    "FernShrubBreak_Left",
    "FernShrubBreak_Right",
)
VEGETATION_UP_MARKER = "vegetation_runtime_up_v1"
ROAD_RELIEF_MAX_VERTICAL = 0.34
ROAD_CROWN_MAX_VERTICAL = 0.28
MAX_SOURCE_TRIANGLES = 5000
COMPONENT_LOCATIONS = {
    "RoadCrown_SunkenWet": (0.0, 0.0, 0.0),
    "RoadRuts_PuddleNear": (0.0, -6.2, 0.0),
    "RoadRuts_PuddleFar": (0.0, 6.5, 0.0),
    "RoadCrown_BranchWet": (0.0, -21.5, 0.0),
    "RoadCrown_ApproachWorn": (0.0, 21.5, 0.0),
    "MuddyShoulder_Left": (-3.25, 0.0, 0.0),
    "MuddyShoulder_Right": (3.25, 0.0, 0.0),
    "RoadsideDitch_Left": (-4.75, 0.0, 0.0),
    "RoadsideDitch_Right": (4.75, 0.0, 0.0),
}
UNCHANGED = {
    "CulvertStoneCrossing": (13, 248),
    "GrassSedgeMass_Left": (18, 180),
    "GrassSedgeMass_Right": (18, 180),
    "FernShrubBreak_Left": (25, 242),
    "FernShrubBreak_Right": (25, 242),
    "RoadFenceBreak_Low": (9, 132),
}


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    tokens: list[str] = []
    if "--" in sys.argv:
        tokens = sys.argv[sys.argv.index("--") + 1 :]
    return parser.parse_args(tokens)


def material(name: str) -> bpy.types.Material:
    result = bpy.data.materials.get(name)
    if result is None:
        raise RuntimeError(f"Missing baseline material: {name}")
    return result


def mesh_object(
    name: str,
    parent: bpy.types.Object,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    materials: tuple[str, ...],
    material_indices: list[int],
    role: str,
) -> bpy.types.Object:
    if len(vertices) < 3 or not faces or len(material_indices) != len(faces):
        raise RuntimeError(f"Invalid mesh definition: {name}")
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=False)
    mesh.update()
    mesh.materials.clear()
    for material_name in materials:
        mesh.materials.append(material(material_name))
    for polygon, index in zip(mesh.polygons, material_indices):
        if index < 0 or index >= len(materials):
            raise RuntimeError(f"Invalid material index on {name}: {index}")
        polygon.material_index = index
        polygon.use_smooth = False
    mesh.calc_loop_triangles()
    if not mesh.loop_triangles:
        raise RuntimeError(f"Degenerate mesh: {name}")

    obj = bpy.data.objects.new(name, mesh)
    collection = parent.users_collection[0] if parent.users_collection else bpy.context.scene.collection
    collection.objects.link(obj)
    obj.parent = parent
    obj.matrix_parent_inverse.identity()
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    obj.delta_location = (0.0, 0.0, 0.0)
    obj.delta_rotation_euler = (0.0, 0.0, 0.0)
    obj.delta_scale = (1.0, 1.0, 1.0)
    obj.hide_render = False
    obj.hide_viewport = False
    obj["presentation_only"] = True
    obj["geometry_status"] = "authored"
    obj["lod"] = "LOD0"
    obj["asset_role"] = role
    obj["component_root"] = parent.name
    obj["geometry_pass"] = "active wet-road route-spine pass 2026-09-05"
    obj["collision"] = "none"
    return obj


def track_height(
    x: float,
    y: float,
    half_width: float = 2.82,
    center: float = 0.0,
    phase: float = 0.0,
    rut_depth: float = 0.072,
    crown: float = 0.095,
) -> float:
    """Return source-basis height for a crowned, wheel-worn lane.

    The published GLB is intentionally axis-baked for the existing Godot
    extractor, so source +Z is the inverse of runtime-up.  The center is
    therefore lower in this basis (runtime crown), while the two wheel lanes
    are raised (runtime depressions).  Keeping this profile in geometry makes
    the relief survive the muted material rebind in the connected world.
    """
    t = max(-1.0, min(1.0, (x - center) / max(half_width, 0.001)))
    u = abs(t)
    edge = (
        0.158
        + 0.010 * math.sin(y * 0.31 + phase)
        + 0.006 * math.sin(y * 0.79 - phase * 0.7)
    )
    # The crown eases into each wheel lane; a pair of smooth troughs keeps
    # the track legible without the two dark rails of the old strip.
    crowned = edge - crown * (1.0 - u**1.55)
    lane = rut_depth * math.exp(-((u - 0.43) / 0.14) ** 2)
    # Erosion drops the outer edge a little and lets left/right shoulders
    # diverge naturally as their width and base height vary by row.
    edge_drop = 0.022 * max(0.0, (u - 0.78) / 0.22) ** 1.35
    asymmetry = 0.006 * t + 0.0035 * math.sin(y * 0.17 + phase) * u * u
    return max(0.022, min(0.24, crowned + lane + edge_drop + asymmetry))


def road_height(x: float, y: float) -> float:
    """Main-track shorthand used by small edge details and puddle beds."""
    return track_height(x, y)


def remove_children(component: bpy.types.Object) -> None:
    for child in tuple(component.children):
        if child.type != "MESH":
            raise RuntimeError(f"Unexpected non-mesh child under {component.name}: {child.name}")
        mesh = child.data
        bpy.data.objects.remove(child, do_unlink=True)
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)


def mesh_descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    meshes: list[bpy.types.Object] = []
    for child in root.children:
        if child.type == "MESH":
            meshes.append(child)
        meshes.extend(mesh_descendants(child))
    return meshes


def reset_mesh_transform(mesh_object: bpy.types.Object) -> None:
    """Leave mesh children in a neutral local transform after baking."""
    mesh_object.matrix_parent_inverse.identity()
    mesh_object.location = (0.0, 0.0, 0.0)
    mesh_object.rotation_mode = "XYZ"
    mesh_object.rotation_euler = (0.0, 0.0, 0.0)
    mesh_object.scale = (1.0, 1.0, 1.0)
    mesh_object.delta_location = (0.0, 0.0, 0.0)
    mesh_object.delta_rotation_euler = (0.0, 0.0, 0.0)
    mesh_object.delta_scale = (1.0, 1.0, 1.0)


def bake_mesh_object_transforms(meshes: list[bpy.types.Object]) -> None:
    """Bake legacy child object transforms without changing component roots."""
    for mesh_object in meshes:
        # `matrix_local` includes the parent translation for Blender empties;
        # `matrix_basis` is the authored child transform we need to bake.
        local_matrix = mesh_object.matrix_basis.copy()
        mesh_object.data.transform(local_matrix)
        mesh_object.data.update()
        reset_mesh_transform(mesh_object)


def flatten_component_vertical_relief(
    component: bpy.types.Object,
    max_vertical: float = ROAD_RELIEF_MAX_VERTICAL,
) -> float:
    """Keep side-road relief shallow after child transforms are baked."""
    meshes = mesh_descendants(component)
    points = [vertex.co for mesh_object in meshes for vertex in mesh_object.data.vertices]
    if not points:
        raise RuntimeError(f"Empty component while flattening relief: {component.name}")
    minimum = min(point.z for point in points)
    maximum = max(point.z for point in points)
    extent = maximum - minimum
    if extent <= max_vertical:
        return extent
    midpoint = (minimum + maximum) * 0.5
    factor = max_vertical / extent
    for mesh_object in meshes:
        for vertex in mesh_object.data.vertices:
            vertex.co.z = midpoint + (vertex.co.z - midpoint) * factor
        mesh_object.data.update()
    return max_vertical


def bake_extractor_axis(meshes: list[bpy.types.Object]) -> None:
    """Apply Blender +90-degree-X so the existing runtime +90 correction cancels."""
    for mesh_object in meshes:
        for vertex in mesh_object.data.vertices:
            x, y, z = vertex.co
            vertex.co = (x, -z, y)
        mesh_object.data.update()


def bake_source_axis_contract(root: bpy.types.Object) -> dict[str, object]:
    """Bake the GLB axis contract once, while keeping target rebuilds idempotent."""
    scene = bpy.context.scene
    marker = scene.get(AXIS_BAKE_MARKER)
    if marker not in (None, AXIS_BAKE_VERSION):
        raise RuntimeError(f"Unknown source axis bake marker: {marker}")

    if marker is None:
        meshes = mesh_descendants(root)
        bake_mesh_object_transforms(meshes)
        flattened = {
            name: round(
                flatten_component_vertical_relief(bpy.data.objects[name]),
                6,
            )
            for name in (
                "MuddyShoulder_Left",
                "MuddyShoulder_Right",
                "RoadsideDitch_Left",
                "RoadsideDitch_Right",
            )
        }
        bake_extractor_axis(meshes)
        mode = "full"
    else:
        # Rebuilt road, shoulder, and ditch roots are authored in the legacy
        # Blender basis on every run. Existing untouched geometry is already
        # baked and must not receive a second axis conversion.
        meshes = [
            child
            for name in REBUILT_COMPONENTS
            for child in mesh_descendants(bpy.data.objects[name])
        ]
        bake_mesh_object_transforms(meshes)
        bake_extractor_axis(meshes)
        flattened = {}
        mode = "rebuilt-components-only"

    scene[AXIS_BAKE_MARKER] = AXIS_BAKE_VERSION
    # Retained foliage was authored with positive source height, unlike the
    # rebuilt road's reverse-sign profile. After the axis bake, Blender Y is
    # runtime Y (GLB -Z after extraction). Reflect only this height channel;
    # the saved per-component marker prevents repeated exports flipping back.
    for name in VEGETATION_COMPONENTS:
        component = bpy.data.objects[name]
        if not component.get(VEGETATION_UP_MARKER, False):
            for mesh_object in mesh_descendants(component):
                for vertex in mesh_object.data.vertices:
                    vertex.co.y = -vertex.co.y
                mesh_object.data.flip_normals()
                mesh_object.data.update()
            component[VEGETATION_UP_MARKER] = True
    rebuild_sedge_leaves()
    return {
        "mode": mode,
        "mesh_count": len(meshes),
        "flattened_vertical_relief": flattened,
        "axis": "+90deg X mesh bake; legacy runtime +90deg X correction preserved",
    }


def rebuild_sedge_leaves() -> None:
    """Rebuild four bent, tapered, two-sided leaves at each retained blade base."""
    for component_name in VEGETATION_COMPONENTS[:2]:
        component = bpy.data.objects[component_name]
        for obj in component.children:
            if "_Blade_" not in obj.name:
                continue
            mesh = obj.data
            if "sedge_anchor" not in obj:
                points = [vertex.co for vertex in mesh.vertices]
                obj["sedge_anchor"] = (
                    (min(p.x for p in points) + max(p.x for p in points)) * 0.5,
                    min(p.y for p in points),
                    (min(p.z for p in points) + max(p.z for p in points)) * 0.5,
                )
            x, base, z = obj["sedge_anchor"]
            index = int(obj.name.split("_Blade_")[1].split("_")[0])
            vertices, faces = [], []
            for leaf in range(4):
                angle = index * 2.39996 + leaf * math.tau / 4 + (0.4 if "Right" in component_name else 0)
                dx, dz = math.cos(angle), math.sin(angle)
                height = 0.35 + 0.23 * (0.5 + 0.5 * math.sin(index * 1.7 + leaf * 2.3))
                bend = 0.12 + leaf * 0.023
                width = 0.025 + 0.006 * ((index + leaf) % 4)
                start = len(vertices)
                for step in range(5):
                    t = step / 4
                    half_width = max(0.001, width * (1 - t) * 0.5)
                    for side in (-1, 1):
                        vertices.append((
                            x + dx * bend * t * t - dz * half_width * side,
                            base + height * (1.3 * t - 0.3 * t * t),
                            z + dz * bend * t * t + dx * half_width * side,
                        ))
                for step in range(4):
                    a = start + step * 2
                    for face in ((a, a + 2, a + 1), (a + 1, a + 2, a + 3)):
                        faces.append(face)
            # Separate back-face vertices keep Blender's export validation
            # from removing reverse-wound faces as duplicate polygons.
            for face in tuple(faces):
                start = len(vertices)
                vertices.extend(vertices[index] for index in reversed(face))
                faces.append((start, start + 1, start + 2))
            mesh.clear_geometry()
            mesh.from_pydata(vertices, [], faces)
            mesh.update()
            mesh.calc_loop_triangles()
            if len(mesh.loop_triangles) != 64 or any(face.area <= 1e-8 for face in mesh.polygons):
                raise RuntimeError(f"Invalid sedge leaf surface: {obj.name}")
        component["sedge_leaf_version"] = 1


def irregular_fan(
    name: str,
    parent: bpy.types.Object,
    center: tuple[float, float],
    radii: tuple[float, float],
    angle: float,
    z_fn,
    materials: tuple[str, ...],
    material_indices: list[int],
    role: str,
    variation: tuple[float, ...],
) -> bpy.types.Object:
    cx, cy = center
    rx, ry = radii
    ring: list[tuple[float, float, float]] = []
    for index, multiplier in enumerate(variation):
        theta = angle + math.tau * index / len(variation)
        x = cx + math.cos(theta) * rx * multiplier
        y = cy + math.sin(theta) * ry * multiplier
        ring.append((x, y, z_fn(x, y, index)))
    vertices = [(cx, cy, z_fn(cx, cy, -1)), *ring]
    faces = [
        (0, index + 1, (index + 1) % len(ring) + 1)
        for index in range(len(ring))
    ]
    return mesh_object(name, parent, vertices, faces, materials, material_indices, role)


def road_surface(
    component: bpy.types.Object,
    base_name: str = "RoadCrown_SunkenWet",
    ys: tuple[float, ...] = (-14.0, -11.2, -8.25, -5.15, -1.9, 1.45, 4.7, 7.95, 11.25, 14.0),
    widths: tuple[float, ...] = (2.72, 2.84, 2.77, 2.92, 2.75, 2.86, 2.79, 2.94, 2.76, 2.85),
    centers: tuple[float, ...] = (-0.12, 0.04, -0.05, 0.12, -0.02, 0.10, -0.09, 0.08, -0.04, 0.06),
    phase: float = 0.0,
    rut_depth: float = 0.082,
    crown: float = 0.115,
    dark_rule=None,
) -> None:
    """Build one continuous faceted lane with an authored cross-section."""
    # Extra samples around both wheel tracks make the relief visible from a
    # low first-person camera while keeping the mesh comfortably LOD0-sized.
    ts = (
        -1.0, -0.90, -0.76, -0.62, -0.50, -0.40, -0.27, -0.10,
        0.08, 0.26, 0.40, 0.50, 0.62, 0.78, 0.90, 1.0,
    )
    if not (len(ys) == len(widths) == len(centers)):
        raise RuntimeError(f"Road ribbon rows disagree: {base_name}")
    vertices: list[tuple[float, float, float]] = []
    for row, (y, width, center) in enumerate(zip(ys, widths, centers)):
        for col, t in enumerate(ts):
            # Independent edge wobble keeps the two margins broken without
            # moving the central route line enough to affect placement.
            side_phase = 0.7 if t < 0.0 else -1.1
            edge_wobble = 0.045 * math.sin((row + 1.6) * (col + 1.9) + side_phase)
            edge_wobble *= 0.18 + abs(t) ** 1.65
            x = center + width * t + edge_wobble
            z = track_height(
                x,
                y,
                half_width=width,
                center=center,
                phase=phase,
                rut_depth=rut_depth,
                crown=crown,
            )
            z += 0.0035 * math.sin(row * 1.43 + col * 0.81 + phase)
            vertices.append((x, y, max(0.024, min(ROAD_CROWN_MAX_VERTICAL, z))))
    faces: list[tuple[int, int, int]] = []
    material_indices: list[int] = []
    for row in range(len(ys) - 1):
        for col in range(len(ts) - 1):
            a = row * len(ts) + col
            b = a + 1
            c = a + len(ts) + 1
            d = a + len(ts)
            faces.extend(((a, b, c), (a, c, d)))
            if dark_rule is not None:
                first = dark_rule(row, col)
                second = dark_rule(row + 1, col + 2)
            else:
                # Dark facets break up wet wheel lanes without making
                # continuous painted rails.
                first = col in (3, 10) and (row * 5 + col) % 9 == 0
                second = col in (4, 9) and (row * 7 + col) % 11 == 0
            material_indices.extend(
                (2 if first else 1 if (row + col * 2) % 13 == 0 else 0,
                 2 if second else 1 if (row * 2 + col) % 17 == 0 else 0)
            )
    mesh_object(
        f"{base_name}_Surface_LOD0",
        component,
        vertices,
        faces,
        ("WetRoad_MutedOchre", "WetRoad_WornLight", "WetRoad_RutDark"),
        material_indices,
        f"continuous faceted crowned lane with depressed wheel ruts: {base_name}",
    )


def edge_break(
    component: bpy.types.Object,
    index: int,
    x: float,
    y: float,
    width: float,
    length: float,
    lean: float,
    height_fn=road_height,
    materials: tuple[str, ...] = ("WetRoad_WornLight", "WetRoad_RutDark"),
    role: str = "broken low road-edge clod",
    name_prefix: str = "RoadCrown_EdgeClod",
) -> None:
    # The source is rotated into Godot with a flipped vertical sign.  Making
    # the low side the source top keeps these clods seated, not rail-like.
    footprint = [
        (-0.50, -0.50),
        (0.37, -0.54),
        (0.55, -0.08),
        (0.43, 0.49),
        (-0.28, 0.55),
        (-0.56, 0.12),
    ]
    bottom: list[tuple[float, float, float]] = []
    top: list[tuple[float, float, float]] = []
    for px, py in footprint:
        local_x = x + px * width + py * lean * 0.12
        local_y = y + py * length
        base = height_fn(local_x, local_y)
        bottom.append((local_x, local_y, base + 0.018 + 0.004 * math.sin(px * 3.0 + py)))
        top.append((local_x, local_y, base - 0.006 + 0.006 * math.sin((px + py) * 5.0)))
    vertices = bottom + top
    faces: list[tuple[int, ...]] = [tuple(reversed(range(6))), tuple(range(6, 12))]
    faces.extend((i, (i + 1) % 6, (i + 1) % 6 + 6, i + 6) for i in range(6))
    material_indices = [1, 0, 1, 0, 0, 1, 0, 1]
    mesh_object(
        f"{name_prefix}_{index:02d}_LOD0",
        component,
        vertices,
        faces,
        materials,
        material_indices,
        role,
    )


def build_crown(component: bpy.types.Object) -> None:
    road_surface(component)
    # Keep only two low contact breaks at the far edges.  The previous six
    # clods plus four paper-thin patches made every extracted road segment read
    # as a scatter of stickers rather than one continuous lane.
    clods = (
        (-3.00, -11.65, 0.62, 1.18, 0.12),
        (3.00, 11.75, 0.58, 1.34, 0.10),
    )
    for index, spec in enumerate(clods):
        edge_break(component, index, *spec)

    patches = (
        (-0.82, -10.85, 0.78, 1.05, 0.12),
        (0.72, 5.55, 0.70, 0.88, -0.18),
    )
    ring_variation = (0.94, 1.06, 0.87, 1.10, 0.92, 1.03, 0.86, 1.08)
    for index, (x, y, rx, ry, angle) in enumerate(patches):
        irregular_fan(
            f"RoadCrown_WornPatch_{index:02d}_LOD0",
            component,
            (x, y),
            (rx, ry),
            angle,
            lambda px, py, _i: max(0.018, road_height(px, py) - 0.004),
            ("WetRoad_WornLight", "WetRoad_MutedOchre"),
            [0 if (i + index) % 4 else 1 for i in range(8)],
            "irregular worn road-crown patch",
            ring_variation,
        )


def rut_break(
    component: bpy.types.Object,
    name: str,
    center: tuple[float, float],
    radii: tuple[float, float],
    angle: float,
    index: int,
    height_fn=road_height,
) -> None:
    irregular_fan(
        name,
        component,
        center,
        radii,
        angle,
        lambda px, py, i: height_fn(px, py) + 0.006 + 0.002 * math.sin(i + index),
        ("WetRoad_MutedOchre", "WetRoad_WornLight"),
        [0 if (i + index) % 3 else 1 for i in range(7)],
        "irregular mud break interrupting wheel rut",
        (0.92, 1.08, 0.86, 1.11, 0.94, 1.04, 0.88),
    )


def puddle(
    component: bpy.types.Object,
    name: str,
    center: tuple[float, float],
    radii: tuple[float, float],
    angle: float,
    index: int,
    height_fn=road_height,
    water_offset: float = 0.034,
) -> None:
    cx, cy = center
    rx, ry = radii
    # Callers retain their older offset values, but the exported water should
    # sit just above the authored bed rather than floating as a bright disc.
    surface_offset = min(0.014, max(0.007, water_offset * 0.30))
    variation = (0.93, 1.08, 0.86, 1.04, 0.97, 1.11, 0.89, 1.03, 0.91, 1.06)
    outer: list[tuple[float, float, float]] = []
    inner: list[tuple[float, float, float]] = []
    for vertex_index, multiplier in enumerate(variation):
        theta = angle + math.tau * vertex_index / len(variation)
        ox = cx + math.cos(theta) * rx * multiplier
        oy = cy + math.sin(theta) * ry * multiplier
        ix = cx + math.cos(theta) * rx * multiplier * 0.77
        iy = cy + math.sin(theta) * ry * multiplier * 0.75
        outer.append((ox, oy, height_fn(ox, oy) + surface_offset + 0.010 + 0.002 * math.sin(vertex_index + index)))
        inner.append((ix, iy, height_fn(ix, iy) + surface_offset + 0.002 * math.sin(vertex_index * 1.7 + index)))
    vertices = outer + inner + [(cx, cy, height_fn(cx, cy) + surface_offset)]
    center_index = len(vertices) - 1
    faces: list[tuple[int, int, int]] = []
    material_indices: list[int] = []
    count = len(variation)
    for vertex_index in range(count):
        nxt = (vertex_index + 1) % count
        faces.extend(
            ((vertex_index, nxt, count + vertex_index), (nxt, count + nxt, count + vertex_index))
        )
        material_indices.extend((1 if (vertex_index + index) % 4 == 0 else 0, 1 if vertex_index % 5 == 0 else 2))
    for vertex_index in range(count):
        nxt = (vertex_index + 1) % count
        faces.append((center_index, count + vertex_index, count + nxt))
        material_indices.append(2)
    mesh_object(
        name,
        component,
        vertices,
        faces,
        ("WetRoad_MutedOchre", "WetRoad_RutDark", "Puddle_ShallowBlueGreen"),
        material_indices,
        "irregular shallow puddle depression and patch contour",
    )


def puddle_glint(
    component: bpy.types.Object,
    name: str,
    center: tuple[float, float],
    radii: tuple[float, float],
    angle: float,
    index: int,
    height_fn=road_height,
    surface_offset: float = 0.037,
) -> None:
    surface_offset = min(0.015, max(0.007, surface_offset * 0.30))
    irregular_fan(
        name,
        component,
        center,
        radii,
        angle,
        lambda px, py, i: height_fn(px, py) + surface_offset + 0.001 * math.sin(i + index),
        ("Puddle_MutedGlint",),
        [0] * 6,
        "muted puddle glint inset",
        (0.90, 1.07, 0.85, 1.04, 0.95, 1.02),
    )


def terrain_mass(
    component: bpy.types.Object,
    name: str,
    center: tuple[float, float],
    radii: tuple[float, float],
    angle: float,
    height_fn,
    crest_height: float,
    index: int,
    materials: tuple[str, ...],
    role: str,
) -> None:
    """Build one irregular low-poly berm with a sloped crest, not a strip."""
    cx, cy = center
    rx, ry = radii
    variation = (0.91, 1.08, 0.86, 1.12, 0.94, 1.04, 0.88, 1.06)
    outer: list[tuple[float, float, float]] = []
    inner: list[tuple[float, float, float]] = []
    for vertex_index, multiplier in enumerate(variation):
        theta = angle + math.tau * vertex_index / len(variation)
        ox = cx + math.cos(theta) * rx * multiplier
        oy = cy + math.sin(theta) * ry * multiplier
        base = max(0.02, min(0.12, height_fn(ox, oy)))
        outer.append((ox, oy, base))
        ix = cx + math.cos(theta) * rx * multiplier * 0.54
        iy = cy + math.sin(theta) * ry * multiplier * 0.54
        inner_height = crest_height * (0.66 + 0.11 * math.sin(vertex_index * 1.71 + index))
        inner.append((ix, iy, base + inner_height))

    crest_base = max(0.02, min(0.12, height_fn(cx, cy)))
    crest = (cx, cy, crest_base + crest_height * (0.96 + 0.03 * math.sin(index + angle)))
    vertices = outer + inner + [crest]
    count = len(outer)
    faces: list[tuple[int, ...]] = [tuple(reversed(range(count)))]
    material_indices = [0]
    for vertex_index in range(count):
        nxt = (vertex_index + 1) % count
        faces.append((vertex_index, nxt, count + nxt, count + vertex_index))
        faces.append((count + vertex_index, count + nxt, count * 2))
        material_indices.extend(
            (1 if (vertex_index + index) % 3 else 0,
             2 if (vertex_index + index) % 4 == 0 and len(materials) > 2 else 1)
        )
    mesh_object(name, component, vertices, faces, materials, material_indices, role)


def build_ruts(component: bpy.types.Object, far: bool) -> None:
    if far:
        mud_specs = (
            ("RoadRuts_Far_MudBreak_00_LOD0", (1.16, -3.55), (0.43, 0.46), 0.20),
            ("RoadRuts_Far_MudBreak_01_LOD0", (-1.20, 0.55), (0.48, 0.55), -0.14),
        )
        puddle_specs = (
            ("RoadRuts_Far_Puddle_00_LOD0", (0.86, -3.70), (0.67, 1.06), 0.10),
            ("RoadRuts_Far_Puddle_01_LOD0", (-1.06, -1.22), (0.60, 0.83), -0.25),
        )
        glints = (
            ("RoadRuts_Far_PuddleGlint_00_LOD0", (0.83, -3.62), (0.27, 0.17), 0.13),
        )
    else:
        mud_specs = (
            ("RoadRuts_Near_MudBreak_00_LOD0", (-1.17, -3.75), (0.45, 0.50), -0.18),
            ("RoadRuts_Near_MudBreak_01_LOD0", (1.22, -0.15), (0.47, 0.56), 0.16),
        )
        puddle_specs = (
            ("RoadRuts_Near_Puddle_00_LOD0", (-0.91, -3.72), (0.66, 1.08), -0.12),
            ("RoadRuts_Near_Puddle_01_LOD0", (1.14, -1.98), (0.61, 0.82), 0.22),
        )
        glints = (
            ("RoadRuts_Near_PuddleGlint_00_LOD0", (-0.88, -3.68), (0.28, 0.17), -0.10),
        )

    rut_height = lambda px, py: track_height(px, py, half_width=2.82, phase=0.42 if far else 0.08)
    for index, (name, center, radii, angle) in enumerate(mud_specs):
        rut_break(component, name, center, radii, angle, index, rut_height)
    for index, (name, center, radii, angle) in enumerate(puddle_specs):
        puddle(component, name, center, radii, angle, index, rut_height)
    for index, (name, center, radii, angle) in enumerate(glints):
        puddle_glint(component, name, center, radii, angle, index, rut_height)


def variant_surface(
    component: bpy.types.Object,
    base_name: str,
    ys: tuple[float, ...],
    widths: tuple[float, ...],
    centers: tuple[float, ...],
    dark_rule,
    phase: float = 0.0,
    rut_depth: float = 0.048,
    crown: float = 0.062,
) -> None:
    """Build a distinct segment while retaining the shared road profile."""
    road_surface(
        component,
        base_name=base_name,
        ys=ys,
        widths=widths,
        centers=centers,
        phase=phase,
        rut_depth=rut_depth,
        crown=crown,
        dark_rule=dark_rule,
    )


def build_branch(component: bpy.types.Object) -> None:
    """Narrower, muddier FAP-branch segment; visibly unlike the main strip."""
    branch_height = lambda px, py: track_height(
        px, py, half_width=2.28, center=0.15, phase=0.64,
        rut_depth=0.055, crown=0.074,
    )
    variant_surface(
        component,
        "RoadCrown_BranchWet",
        ys=(-7.0, -3.55, -0.15, 3.35, 7.0),
        widths=(2.24, 2.34, 2.20, 2.30, 2.22),
        centers=(0.2, 0.06, 0.24, 0.1, 0.18),
        dark_rule=lambda row, col: (row * 7 + col) % 11 == 0,
        phase=0.64,
        rut_depth=0.055,
        crown=0.074,
    )
    clods = (
        (11, -1.55, -5.2, 0.52, 0.86, 0.14),
    )
    for spec in clods:
        edge_break(
            component,
            spec[0],
            *spec[1:],
            height_fn=branch_height,
            name_prefix="RoadCrown_BranchWet_EdgeClod",
            role="broken muddy FAP-branch road-edge clod",
        )
    puddle(
        component,
        "RoadCrown_BranchWet_Puddle_00_LOD0",
        (0.55, -2.4),
        (0.5, 0.78),
        0.2,
        3,
        branch_height,
        0.036,
    )
    irregular_fan(
        "RoadCrown_BranchWet_WornPatch_00_LOD0",
        component,
        (-0.3, 5.1),
        (0.6, 0.82),
        0.1,
        lambda px, py, _i: max(0.024, branch_height(px, py) - 0.007),
        ("WetRoad_WornLight", "WetRoad_MutedOchre"),
        [0, 1, 0, 0, 1, 0, 0, 1],
        "irregular worn branch patch",
        (0.94, 1.06, 0.88, 1.09, 0.93, 1.04, 0.87, 1.07),
    )


def build_approach(component: bpy.types.Object) -> None:
    """Worn zirat-approach segment; muted, patchy, sparse water."""
    approach_height = lambda px, py: track_height(
        px, py, half_width=2.66, center=-0.06, phase=1.18,
        rut_depth=0.050, crown=0.075,
    )
    variant_surface(
        component,
        "RoadCrown_ApproachWorn",
        ys=(-7.0, -3.4, -0.1, 3.4, 7.0),
        widths=(2.58, 2.72, 2.62, 2.76, 2.64),
        centers=(-0.14, 0.04, -0.1, 0.12, -0.05),
        dark_rule=lambda row, col: (row * 5 + col * 3) % 13 == 0,
        phase=1.18,
        rut_depth=0.050,
        crown=0.075,
    )
    clods = (
        (21, 2.86, -0.4, 0.52, 0.9, 0.16),
    )
    for spec in clods:
        edge_break(
            component,
            spec[0],
            *spec[1:],
            height_fn=approach_height,
            name_prefix="RoadCrown_ApproachWorn_EdgeClod",
            role="broken worn zirat-approach road-edge clod",
        )
    for index, (x, y, rx, ry, angle) in enumerate(
        (
            (-0.6, -3.9, 0.7, 0.9, 0.14),
            (0.72, 1.15, 0.62, 0.84, -0.2),
        )
    ):
        irregular_fan(
            f"RoadCrown_ApproachWorn_WornPatch_{index:02d}_LOD0",
            component,
            (x, y),
            (rx, ry),
            angle,
            lambda px, py, _i: max(0.024, approach_height(px, py) - 0.007),
            ("WetRoad_WornLight", "WetRoad_MutedOchre"),
            [0 if (i + index) % 3 else 1 for i in range(8)],
            "irregular worn approach patch",
            (0.93, 1.07, 0.86, 1.1, 0.92, 1.05, 0.88, 1.06),
        )
    puddle(
        component,
        "RoadCrown_ApproachWorn_Puddle_00_LOD0",
        (0.4, -5.6),
        (0.56, 0.84),
        -0.16,
        7,
        approach_height,
        0.034,
    )
    puddle_glint(
        component,
        "RoadCrown_ApproachWorn_PuddleGlint_00_LOD0",
        (0.37, -5.52),
        (0.24, 0.15),
        0.1,
        4,
        approach_height,
        0.037,
    )


def shoulder_height(x: float, y: float, side: int, phase: float = 0.0) -> float:
    """Source-basis height for a slumped, asymmetric mud shoulder."""
    # `side*x` makes t=0 the road-facing edge for either direct root while
    # keeping the local component orientation neutral for extraction.
    t = max(0.0, min(1.0, (side * x + 1.22) / 2.44))
    edge = 0.104 + 0.010 * math.sin(y * 0.27 + phase)
    outer_slope = 0.070 * t**1.12
    soft_low = 0.022 * math.exp(-((t - 0.58) / 0.22) ** 2)
    return max(0.035, min(0.23, edge + outer_slope + soft_low + 0.007 * side * t))


def ditch_height(x: float, y: float, side: int, phase: float = 0.0) -> float:
    """Source-basis height for a shallow channel with raised banks."""
    t = max(-1.0, min(1.0, x / 1.52))
    u = abs(t)
    channel = 0.068 * (1.0 - u**1.30)
    bank = 0.018 * max(0.0, (u - 0.68) / 0.32)
    wave = 0.009 * math.sin(y * 0.22 + phase) + 0.005 * math.sin(y * 0.71 - phase)
    return max(0.055, min(0.23, 0.11 + channel + bank + wave + 0.006 * side * t))


def build_shoulder(component: bpy.types.Object, side_name: str, side: int, phase: float) -> None:
    """Build one broken mud shoulder with an integrated wet-pocket rhythm."""
    ys = (-13.5, -9.9, -6.2, -2.5, 1.3, 5.0, 9.4, 13.7)
    widths = (1.16, 1.24, 1.10, 1.28, 1.14, 1.22, 1.11, 1.20)
    centers = (0.06, -0.04, 0.11, -0.08, 0.04, -0.10, 0.08, -0.03)
    ts = (-1.0, -0.46, 0.0, 0.50, 1.0)
    vertices: list[tuple[float, float, float]] = []
    for row, (y, width, center) in enumerate(zip(ys, widths, centers)):
        for col, t in enumerate(ts):
            wobble = 0.035 * math.sin((row + 1.2) * (col + 2.6) + phase)
            wobble *= abs(t) ** 1.4
            x = center + width * t + wobble
            z = shoulder_height(x, y, side, phase) + 0.003 * math.sin(row * 1.1 + col * 0.8)
            vertices.append((x, y, max(0.035, min(0.23, z))))
    faces: list[tuple[int, int, int, int]] = []
    material_indices: list[int] = []
    for row in range(len(ys) - 1):
        for col in range(len(ts) - 1):
            a = row * len(ts) + col
            faces.append((a, a + 1, a + len(ts) + 1, a + len(ts)))
            material_indices.append(1 if (row * 3 + col + (side < 0)) % 7 == 0 else 0)
    mesh_object(
        f"MuddyShoulder_{side_name}_UnevenStrip_LOD0",
        component,
        vertices,
        faces,
        ("MuddyShoulder_WetBrown", "MuddyShoulder_ClayBreak"),
        material_indices,
        "faceted slumped shoulder transition with broken outer edge",
    )

    clods = (
        (0, side * 0.92, -11.45, 0.56, 0.86, 0.12),
        (1, side * 1.03, -2.35, 0.48, 1.00, 0.10),
        (2, side * 1.05, 7.35, 0.52, 0.94, 0.16),
    )
    height_fn = lambda px, py: shoulder_height(px, py, side, phase)
    for index, x, y, width, length, lean in clods:
        edge_break(
            component,
            index,
            x,
            y,
            width,
            length,
            lean,
            height_fn=height_fn,
            materials=("MuddyShoulder_ClayBreak", "MuddyShoulder_WetBrown"),
            role="irregular shoulder clay clod",
            name_prefix=f"MuddyShoulder_{side_name}_Clod",
        )

    for index, (x, y, rx, ry, angle) in enumerate(
        (
            (side * 0.68, -8.0, 0.24, 0.62, 0.12),
            (side * 0.83, 8.65, 0.22, 0.56, 0.18),
        )
    ):
        irregular_fan(
            f"MuddyShoulder_{side_name}_WetPocket_{index:02d}_LOD0",
            component,
            (x, y),
            (rx, ry),
            angle,
            lambda px, py, i: height_fn(px, py) + 0.028 + 0.002 * math.sin(i + index),
            ("WetRoad_RutDark",),
            [0] * 6,
            "inset wet shoulder pocket seated below its rim",
            (0.90, 1.08, 0.86, 1.04, 0.96, 1.10),
        )

    # A handful of broad, asymmetric berms turn the shoulder into a yard
    # transition and break the flat exported horizon without becoming a
    # repeated strip or affecting the route envelope.
    berm_specs = (
        (
            (-1.08, -10.8, 0.82, 1.12, 0.18, -0.22),
            (-1.26, 5.8, 0.96, 1.28, 0.205, 0.24),
        )
        if side < 0
        else (
            (0.72, -11.6, 0.62, 0.90, 0.15, 0.16),
            (0.65, 3.2, 0.66, 0.86, 0.16, 0.08),
        )
    )
    for index, (x, y, rx, ry, crest_height, angle) in enumerate(berm_specs):
        terrain_mass(
            component,
            f"MuddyShoulder_{side_name}_YardBerm_{index:02d}_LOD0",
            (x, y),
            (rx, ry),
            angle,
            height_fn,
            crest_height,
            index,
            ("MuddyShoulder_WetBrown", "MuddyShoulder_ClayBreak", "Moss_WetOlive"),
            "irregular shoulder-to-yard berm terrain mass",
        )


def ditch_water_pocket(
    component: bpy.types.Object,
    name: str,
    center: tuple[float, float],
    radii: tuple[float, float],
    angle: float,
    index: int,
    side: int,
    phase: float,
) -> None:
    """Build one faceted 8-sided ditch bed + inset water surface."""
    cx, cy = center
    rx, ry = radii
    variation = (0.88, 1.06, 0.94, 1.11, 0.86, 1.03, 0.97, 1.08)
    outer: list[tuple[float, float, float]] = []
    inner: list[tuple[float, float, float]] = []
    for vertex_index, multiplier in enumerate(variation):
        theta = angle + math.tau * vertex_index / len(variation)
        ox = cx + math.cos(theta) * rx * multiplier
        oy = cy + math.sin(theta) * ry * multiplier
        ix = cx + math.cos(theta) * rx * multiplier * 0.72
        iy = cy + math.sin(theta) * ry * multiplier * 0.72
        outer.append((ox, oy, ditch_height(ox, oy, side, phase) + 0.005))
        inner.append((ix, iy, ditch_height(ix, iy, side, phase) + 0.031 + 0.002 * math.sin(vertex_index + index)))
    vertices = outer + inner + [(cx, cy, ditch_height(cx, cy, side, phase) + 0.036)]
    center_index = len(vertices) - 1
    faces: list[tuple[int, int, int]] = []
    material_indices: list[int] = []
    count = len(variation)
    for vertex_index in range(count):
        nxt = (vertex_index + 1) % count
        faces.extend(
            ((vertex_index, nxt, count + vertex_index), (nxt, count + nxt, count + vertex_index))
        )
        material_indices.extend((0, 1 if (vertex_index + index) % 3 else 2))
    for vertex_index in range(count):
        nxt = (vertex_index + 1) % count
        faces.append((center_index, count + vertex_index, count + nxt))
        material_indices.append(1 if vertex_index % 4 else 2)
    mesh_object(
        name,
        component,
        vertices,
        faces,
        ("Ditch_DampGreenBrown", "Ditch_StillWater", "Puddle_ShallowBlueGreen"),
        material_indices,
        "inset ditch water pocket with faceted earthen rim",
    )


def build_ditch(component: bpy.types.Object, side_name: str, side: int, phase: float) -> None:
    """Build an uneven drainage channel rather than a linear side slab."""
    ys = (-13.5, -9.4, -5.0, -0.4, 4.2, 8.7, 13.4)
    widths = (1.44, 1.53, 1.48, 1.56, 1.43, 1.51, 1.46)
    centers = (0.08, -0.12, 0.10, -0.05, 0.13, -0.09, 0.06)
    ts = (-1.0, -0.58, -0.22, 0.22, 0.60, 1.0)
    vertices: list[tuple[float, float, float]] = []
    for row, (y, width, center) in enumerate(zip(ys, widths, centers)):
        for col, t in enumerate(ts):
            wobble = 0.055 * math.sin((row + 0.8) * (col + 2.4) + phase)
            wobble *= 0.25 + abs(t) ** 1.4
            x = center + width * t + wobble
            z = ditch_height(x, y, side, phase) + 0.003 * math.sin(row * 1.2 + col * 0.7)
            vertices.append((x, y, max(0.055, min(0.24, z))))
    faces: list[tuple[int, int, int, int]] = []
    material_indices: list[int] = []
    for row in range(len(ys) - 1):
        for col in range(len(ts) - 1):
            a = row * len(ts) + col
            faces.append((a, a + 1, a + len(ts) + 1, a + len(ts)))
            material_indices.append(1 if col in (2, 3) and (row + col + (side < 0)) % 3 else 0)
    mesh_object(
        f"RoadsideDitch_{side_name}_UnevenChannel_LOD0",
        component,
        vertices,
        faces,
        ("Ditch_DampGreenBrown", "Ditch_StillWater"),
        material_indices,
        "shallow uneven drainage channel with depressed center",
    )

    bank_clods = (
        (0, side * 1.06, -11.85, 0.64, 1.02, -0.12),
        (1, side * 1.15, 5.45, 0.58, 1.14, -0.10),
    )
    height_fn = lambda px, py: ditch_height(px, py, side, phase)
    for index, x, y, width, length, lean in bank_clods:
        edge_break(
            component,
            index,
            x,
            y,
            width,
            length,
            lean,
            height_fn=height_fn,
            materials=("Ditch_DampGreenBrown", "Moss_WetOlive"),
            role="irregular drainage-bank clod",
            name_prefix=f"RoadsideDitch_{side_name}_BankClod",
        )

    for index, (x, y, rx, ry, angle) in enumerate(
        (
            (side * 0.05, -10.50, 0.36, 0.84, 0.10),
            (side * 0.11, -0.70, 0.38, 0.92, 0.24),
            (side * 0.10, 10.95, 0.34, 0.82, 0.20),
        )
    ):
        ditch_water_pocket(
            component,
            f"RoadsideDitch_{side_name}_WaterPocket_{index:02d}_LOD0",
            (x, y),
            (rx, ry),
            angle,
            index,
            side,
            phase,
        )

    # Discrete drainage-bank masses provide local horizon breaks and a
    # painterly transition toward yards/vegetation; the left/right layouts
    # intentionally differ instead of repeating a modular wall.
    berm_specs = (
        (
            (-1.28, -9.9, 0.68, 1.10, 0.18, 0.10),
            (-1.34, 7.3, 0.72, 1.00, 0.20, 0.18),
        )
        if side < 0
        else (
            (1.15, -11.2, 0.82, 1.30, 0.21, -0.16),
            (1.34, 5.4, 0.92, 1.25, 0.22, -0.12),
        )
    )
    for index, (x, y, rx, ry, crest_height, angle) in enumerate(berm_specs):
        terrain_mass(
            component,
            f"RoadsideDitch_{side_name}_TerrainMass_{index:02d}_LOD0",
            (x, y),
            (rx, ry),
            angle,
            height_fn,
            crest_height,
            index,
            ("Ditch_DampGreenBrown", "Ditch_StillWater", "Moss_WetOlive"),
            "irregular drainage-bank berm / horizon break",
        )


def component_stats(component: bpy.types.Object, include_root: bool = False) -> dict[str, object]:
    meshes = [child for child in component.children if child.type == "MESH"]
    points = [child.matrix_world @ vertex.co for child in meshes for vertex in child.data.vertices]
    if not points:
        raise RuntimeError(f"Empty component: {component.name}")
    bounds = tuple(round(min(point[index] for point in points), 6) for index in range(3)) + tuple(
        round(max(point[index] for point in points), 6) for index in range(3)
    )
    return {
        "meshes": len(meshes),
        "triangles": sum(len(child.data.loop_triangles) for child in meshes),
        "bounds": bounds,
        "min_z": min(point.z for point in points),
        "max_z": max(point.z for point in points),
    }


def baked_component_bounds(component: bpy.types.Object) -> tuple[float, ...]:
    """Return the post-extraction local bounds represented by baked vertices."""
    points = [vertex.co for mesh in mesh_descendants(component) for vertex in mesh.data.vertices]
    if not points:
        raise RuntimeError(f"Empty baked component: {component.name}")
    return tuple(round(min(point[index] for point in points), 6) for index in range(3)) + tuple(
        round(max(point[index] for point in points), 6) for index in range(3)
    )


def is_identity_matrix(matrix) -> bool:
    return all(
        abs(matrix[row][column] - (1.0 if row == column else 0.0)) <= 0.000001
        for row in range(4)
        for column in range(4)
    )


def validate_baked_axis_contract(root: bpy.types.Object) -> dict[str, object]:
    """Verify neutral object transforms and runtime-facing horizontal bounds."""
    if bpy.context.scene.get(AXIS_BAKE_MARKER) != AXIS_BAKE_VERSION:
        raise RuntimeError("Source axis bake marker is missing")
    if not is_identity_matrix(root.matrix_world):
        raise RuntimeError(f"Authored root transform leaked: {root.matrix_world}")

    for mesh_object in mesh_descendants(root):
        if (
            any(abs(value) > 0.000001 for value in mesh_object.location)
            or any(abs(value) > 0.000001 for value in mesh_object.rotation_euler)
            or any(abs(value - 1.0) > 0.000001 for value in mesh_object.scale)
            or not is_identity_matrix(mesh_object.matrix_parent_inverse)
        ):
            raise RuntimeError(f"Non-identity mesh transform leaked: {mesh_object.name}")

    component_bounds = {name: baked_component_bounds(bpy.data.objects[name]) for name in ROAD_COMPONENTS}
    for name in VEGETATION_COMPONENTS:
        for mesh_object in mesh_descendants(bpy.data.objects[name]):
            heights = [vertex.co.y for vertex in mesh_object.data.vertices]
            if min(heights) < -0.01 or max(heights) <= 0.0:
                raise RuntimeError(f"Vegetation points below runtime ground: {mesh_object.name}")
    for name, bounds in component_bounds.items():
        vertical = bounds[4] - bounds[1]
        longitudinal = bounds[5] - bounds[2]
        if vertical > ROAD_RELIEF_MAX_VERTICAL + 0.04:
            raise RuntimeError(
                f"Roadside vertical relief exceeds {ROAD_RELIEF_MAX_VERTICAL + 0.04:.2f}m: {name}: {bounds}"
            )
        if longitudinal < 2.0:
            raise RuntimeError(f"Roadside route extent is too short: {name}: {bounds}")
    return {
        "component_bounds_after_extraction": component_bounds,
        "neutral_mesh_transform_count": len(mesh_descendants(root)),
        "max_vertical_relief": round(
            max(bounds[4] - bounds[1] for bounds in component_bounds.values()),
            6,
        ),
    }


def validate(root: bpy.types.Object) -> dict[str, object]:
    expected = (
        "RoadCrown_SunkenWet",
        "RoadRuts_PuddleNear",
        "RoadRuts_PuddleFar",
        "RoadCrown_BranchWet",
        "RoadCrown_ApproachWorn",
        "MuddyShoulder_Left",
        "MuddyShoulder_Right",
        "RoadsideDitch_Left",
        "RoadsideDitch_Right",
        "CulvertStoneCrossing",
        "GrassSedgeMass_Left",
        "GrassSedgeMass_Right",
        "FernShrubBreak_Left",
        "FernShrubBreak_Right",
        "RoadFenceBreak_Low",
    )
    direct = tuple(child.name for child in root.children)
    if set(direct) != set(expected) or len(direct) != len(expected):
        raise RuntimeError(f"Direct component contract changed: {direct}")
    for name, location in COMPONENT_LOCATIONS.items():
        component = bpy.data.objects[name]
        if component.parent is not root or tuple(round(value, 6) for value in component.location) != location:
            raise RuntimeError(f"Component root transform changed: {name}")
    for name, (mesh_count, triangle_count) in UNCHANGED.items():
        if bpy.data.objects[name].get("sedge_leaf_version") == 1:
            triangle_count += 12 * (64 - 6)
        stats = component_stats(bpy.data.objects[name])
        if (stats["meshes"], stats["triangles"]) != (mesh_count, triangle_count):
            raise RuntimeError(f"Untouched component changed: {name}: {stats}")
    if len(bpy.data.materials) != 19:
        raise RuntimeError(f"Material count changed: {len(bpy.data.materials)}")
    required_materials = {
        "WetRoad_RutDark",
        "WetRoad_MutedOchre",
        "WetRoad_WornLight",
        "Puddle_MutedGlint",
        "Puddle_ShallowBlueGreen",
        "Ditch_StillWater",
    }
    if not required_materials.issubset({material.name for material in bpy.data.materials}):
        raise RuntimeError("Required source material names are missing")
    if bpy.data.images or any(obj.type == "CAMERA" for obj in bpy.data.objects) or any(obj.type == "LIGHT" for obj in bpy.data.objects):
        raise RuntimeError("Cameras, lights, or images are not allowed")
    all_meshes = mesh_descendants(root)
    degenerate_meshes = [mesh.name for mesh in all_meshes if not mesh.data.loop_triangles]
    if degenerate_meshes:
        raise RuntimeError(f"Zero-triangle mesh(es): {degenerate_meshes}")
    source_triangles = sum(len(mesh.data.loop_triangles) for mesh in all_meshes)
    if source_triangles > MAX_SOURCE_TRIANGLES:
        raise RuntimeError(
            f"Source triangle budget exceeded: {source_triangles} > {MAX_SOURCE_TRIANGLES}"
        )
    target_stats = {name: component_stats(bpy.data.objects[name]) for name in TARGETS}
    side_stats = {name: component_stats(bpy.data.objects[name]) for name in REBUILT_SIDES}
    road_points = []
    for name in TARGETS:
        component = bpy.data.objects[name]
        for child in component.children:
            for vertex in child.data.vertices:
                road_points.append(vertex.co.copy())
                if len(child.data.loop_triangles) == 0:
                    raise RuntimeError(f"Zero-area key mesh: {child.name}")
    min_z = min(point.z for point in road_points)
    max_z = max(point.z for point in road_points)
    relief = max_z - min_z
    if min_z < 0.012 or max_z > ROAD_CROWN_MAX_VERTICAL or relief > ROAD_CROWN_MAX_VERTICAL:
        raise RuntimeError(f"Road relief exceeds shallow contract: z={min_z:.4f}..{max_z:.4f}")
    return {
        "target_stats": target_stats,
        "side_stats": side_stats,
        "road_aabb_component_local": (
            round(min(point.x for point in road_points), 6),
            round(min(point.y for point in road_points), 6),
            round(min_z, 6),
            round(max(point.x for point in road_points), 6),
            round(max(point.y for point in road_points), 6),
            round(max_z, 6),
        ),
        "max_vertical_relief": round(relief, 6),
        "central_route_clearance_to_godot_y0": round(min_z, 6),
        "source_meshes": len(all_meshes),
        "source_triangles": source_triangles,
    }


def main() -> None:
    args = arguments()
    root_path = Path(args.root).resolve()
    blend_path = root_path / "assets/source/blender/act1/urman_wet_village_road_kit.blend"
    glb_path = root_path / "game/assets/models/act1" / GLB_NAME
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None or root.type != "EMPTY":
        raise RuntimeError(f"Missing authored root: {ROOT_NAME}")

    # First-run creation of the variant module roots; later runs rebuild them
    # in place like the original three targets.
    for name in ("RoadCrown_BranchWet", "RoadCrown_ApproachWorn"):
        component = bpy.data.objects.get(name)
        if component is None:
            component = bpy.data.objects.new(name, None)
            component.empty_display_size = 0.5
            component.parent = root
            component.location = COMPONENT_LOCATIONS[name]
            bpy.context.scene.collection.objects.link(component)

    for name in REBUILT_COMPONENTS:
        component = bpy.data.objects.get(name)
        if component is None or component.parent is not root or component.type != "EMPTY":
            raise RuntimeError(f"Missing target component root: {name}")
        remove_children(component)

    build_crown(bpy.data.objects["RoadCrown_SunkenWet"])
    build_ruts(bpy.data.objects["RoadRuts_PuddleNear"], far=False)
    build_ruts(bpy.data.objects["RoadRuts_PuddleFar"], far=True)
    build_branch(bpy.data.objects["RoadCrown_BranchWet"])
    build_approach(bpy.data.objects["RoadCrown_ApproachWorn"])
    build_shoulder(bpy.data.objects["MuddyShoulder_Left"], "Left", side=-1, phase=0.37)
    build_shoulder(bpy.data.objects["MuddyShoulder_Right"], "Right", side=1, phase=1.11)
    build_ditch(bpy.data.objects["RoadsideDitch_Left"], "Left", side=-1, phase=0.59)
    build_ditch(bpy.data.objects["RoadsideDitch_Right"], "Right", side=1, phase=1.43)

    source_report = validate(root)
    bake_report = bake_source_axis_contract(root)
    baked_report = validate_baked_axis_contract(root)

    scene = bpy.context.scene
    scene["generator"] = "assets/source/blender/act1/urman_wet_village_road_kit.py"
    scene["active_geometry_pass"] = "continuous crown + embedded near/far ruts + BranchWet + ApproachWorn variants + tapered shoulders/ditches with reduced contact breaks; shallow broken extracted-basis relief; redundant slab patches omitted"
    scene["source_axis_contract"] = "Blender meshes axis-baked +90deg X after child transforms; legacy GLB extraction +90deg X produces Godot-horizontal road"
    scene["geometry_policy"] = "geometry-only; existing WetRoad_* and Puddle_* materials authoritative; presentation-only; no collision; asymmetric berm terrain masses"
    for name in REBUILT_COMPONENTS:
        bpy.data.objects[name]["geometry_pass"] = "authored wet-road route-spine pass 2026-09-05"
        bpy.data.objects[name]["target_component_contract"] = "root name/location preserved; child mesh geometry rebuilt and axis-baked"
    scene["road_pass_report"] = str({"source": source_report, "axis_bake": bake_report, "baked": baked_report})

    save_versions = bpy.context.preferences.filepaths.save_version
    bpy.context.preferences.filepaths.save_version = 0
    try:
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    finally:
        bpy.context.preferences.filepaths.save_version = save_versions
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=False,
        export_apply=True,
    )
    print("wet-road-pass: source=", source_report)
    print("wet-road-pass: axis_bake=", bake_report)
    print("wet-road-pass: baked=", baked_report)
    print(f"wet-road-pass: saved {blend_path}")
    print(f"wet-road-pass: exported {glb_path}")


if __name__ == "__main__":
    main()
