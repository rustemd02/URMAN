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
TARGETS = ("RoadCrown_SunkenWet", "RoadRuts_PuddleNear", "RoadRuts_PuddleFar")
ROAD_COMPONENTS = TARGETS + (
    "MuddyShoulder_Left",
    "MuddyShoulder_Right",
    "RoadsideDitch_Left",
    "RoadsideDitch_Right",
)
AXIS_BAKE_MARKER = "source_axis_bake_version"
AXIS_BAKE_VERSION = "legacy-godot-extractor-compensation-v1"
ROAD_RELIEF_MAX_VERTICAL = 0.22
COMPONENT_LOCATIONS = {
    "RoadCrown_SunkenWet": (0.0, 0.0, 0.0),
    "RoadRuts_PuddleNear": (0.0, -6.2, 0.0),
    "RoadRuts_PuddleFar": (0.0, 6.5, 0.0),
}
UNCHANGED = {
    "MuddyShoulder_Left": (10, 194),
    "MuddyShoulder_Right": (10, 194),
    "RoadsideDitch_Left": (10, 260),
    "RoadsideDitch_Right": (10, 260),
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
    obj["geometry_pass"] = "active wet-road readability pass 2026-08-26"
    obj["collision"] = "none"
    return obj


def road_height(x: float, y: float) -> float:
    """Source height; the legacy extraction rotates +Z into Godot -Y."""
    t = max(-1.0, min(1.0, x / 3.16))
    # Keep the whole shallow crown above the connected world's visible ground
    # top after the legacy extraction, so the profile reads instead of being
    # buried by the shared floor.
    edge = 0.058 + 0.004 * math.sin(y * 0.49 + 0.4) + 0.003 * math.sin(y * 1.13 - 0.7)
    # In the extracted basis this makes the center the shallow crown and the
    # wheel lanes sit slightly below it, while the entire surface stays under
    # the authoritative traversal top at Godot Y=0.
    value = edge - 0.040 * (1.0 - abs(t) ** 1.45) + 0.003 * t
    return max(0.018, min(0.092, value))


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
        # The three target roots are rebuilt in the legacy Blender basis on
        # every run.  Existing ten-component geometry is already baked.
        meshes = [
            child
            for name in TARGETS
            for child in mesh_descendants(bpy.data.objects[name])
        ]
        bake_mesh_object_transforms(meshes)
        bake_extractor_axis(meshes)
        flattened = {}
        mode = "targets-only"

    scene[AXIS_BAKE_MARKER] = AXIS_BAKE_VERSION
    return {
        "mode": mode,
        "mesh_count": len(meshes),
        "flattened_vertical_relief": flattened,
        "axis": "+90deg X mesh bake; legacy runtime +90deg X correction preserved",
    }


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


def road_surface(component: bpy.types.Object) -> None:
    ts = (-1.0, -0.77, -0.53, -0.27, 0.0, 0.26, 0.52, 0.78, 1.0)
    ys = (-14.0, -10.6, -7.1, -3.5, 0.0, 3.35, 6.9, 10.65, 14.0)
    widths = (3.04, 3.18, 3.08, 3.20, 3.06, 3.16, 3.10, 3.19, 3.05)
    centers = (-0.06, 0.07, 0.02, -0.08, 0.05, -0.04, 0.09, -0.07, 0.03)
    vertices: list[tuple[float, float, float]] = []
    for row, (y, width, center) in enumerate(zip(ys, widths, centers)):
        for col, t in enumerate(ts):
            edge_wobble = 0.018 * math.sin((row + 1.3) * (col + 2.1)) * (0.35 + abs(t))
            x = center + width * t + edge_wobble
            z = road_height(x, y) + 0.004 * math.sin(row * 1.31 + col * 0.77)
            vertices.append((x, y, max(0.018, min(0.092, z))))
    faces: list[tuple[int, int, int]] = []
    material_indices: list[int] = []
    for row in range(len(ys) - 1):
        for col in range(len(ts) - 1):
            a = row * len(ts) + col
            b = a + 1
            c = a + len(ts) + 1
            d = a + len(ts)
            faces.extend(((a, b, c), (a, c, d)))
            # Keep the rut-dark slot for source compatibility, but do not let
            # long dark facets draw a second rail over the authored crown.
            first = 1 if (row + col) % 5 == 0 else 0
            second = 1 if (row * 2 + col) % 7 == 0 else first
            material_indices.extend((first, second))
    mesh_object(
        "RoadCrown_SunkenWet_Surface_LOD0",
        component,
        vertices,
        faces,
        ("WetRoad_MutedOchre", "WetRoad_WornLight", "WetRoad_RutDark"),
        material_indices,
        "shallow uneven crowned road surface",
    )


def edge_break(
    component: bpy.types.Object,
    index: int,
    x: float,
    y: float,
    width: float,
    length: float,
    lean: float,
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
        base = road_height(local_x, local_y)
        bottom.append((local_x, local_y, base + 0.024))
        top.append((local_x, local_y, base - 0.004 + 0.003 * math.sin((px + py) * 5.0)))
    vertices = bottom + top
    faces: list[tuple[int, ...]] = [tuple(reversed(range(6))), tuple(range(6, 12))]
    faces.extend((i, (i + 1) % 6, (i + 1) % 6 + 6, i + 6) for i in range(6))
    material_indices = [1, 0, 1, 0, 0, 1, 0, 1]
    mesh_object(
        f"RoadCrown_EdgeClod_{index:02d}_LOD0",
        component,
        vertices,
        faces,
        ("WetRoad_WornLight", "WetRoad_RutDark"),
        material_indices,
        "broken low road-edge clod",
    )


def build_crown(component: bpy.types.Object) -> None:
    road_surface(component)
    clods = (
        (-3.00, -11.65, 0.62, 1.18, 0.12),
        (2.91, -8.25, 0.74, 0.92, -0.16),
        (-3.04, -3.35, 0.56, 1.06, -0.10),
        (2.95, 2.35, 0.68, 1.22, 0.15),
        (-2.92, 8.10, 0.78, 0.90, -0.18),
        (3.00, 11.75, 0.58, 1.34, 0.10),
    )
    for index, spec in enumerate(clods):
        edge_break(component, index, *spec)

    patches = (
        (-0.82, -10.85, 0.78, 1.05, 0.12),
        (0.66, -4.00, 0.88, 0.80, -0.24),
        (-0.56, 4.10, 0.72, 1.00, 0.18),
        (0.82, 10.65, 0.84, 0.92, -0.16),
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
) -> None:
    irregular_fan(
        name,
        component,
        center,
        radii,
        angle,
        lambda px, py, i: road_height(px, py) + 0.010 + 0.002 * math.sin(i + index),
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
) -> None:
    cx, cy = center
    rx, ry = radii
    variation = (0.93, 1.08, 0.86, 1.04, 0.97, 1.11, 0.89, 1.03, 0.91, 1.06)
    outer: list[tuple[float, float, float]] = []
    inner: list[tuple[float, float, float]] = []
    for vertex_index, multiplier in enumerate(variation):
        theta = angle + math.tau * vertex_index / len(variation)
        ox = cx + math.cos(theta) * rx * multiplier
        oy = cy + math.sin(theta) * ry * multiplier
        ix = cx + math.cos(theta) * rx * multiplier * 0.77
        iy = cy + math.sin(theta) * ry * multiplier * 0.75
        outer.append((ox, oy, road_height(ox, oy) + 0.006 + 0.002 * math.sin(vertex_index + index)))
        inner.append((ix, iy, road_height(ix, iy) + 0.025 + 0.001 * math.sin(vertex_index * 1.7 + index)))
    vertices = outer + inner + [(cx, cy, road_height(cx, cy) + 0.027)]
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
) -> None:
    irregular_fan(
        name,
        component,
        center,
        radii,
        angle,
        lambda px, py, i: road_height(px, py) + 0.020 + 0.001 * math.sin(i + index),
        ("Puddle_MutedGlint",),
        [0] * 6,
        "muted puddle glint inset",
        (0.90, 1.07, 0.85, 1.04, 0.95, 1.02),
    )


def build_ruts(component: bpy.types.Object, far: bool) -> None:
    if far:
        mud_specs = (
            ("RoadRuts_Far_MudBreak_00_LOD0", (1.16, -3.55), (0.43, 0.46), 0.20),
            ("RoadRuts_Far_MudBreak_01_LOD0", (-1.20, 0.55), (0.48, 0.55), -0.14),
            ("RoadRuts_Far_MudBreak_02_LOD0", (1.18, 3.70), (0.40, 0.60), 0.27),
        )
        puddle_specs = (
            ("RoadRuts_Far_Puddle_00_LOD0", (0.86, -3.70), (0.67, 1.06), 0.10),
            ("RoadRuts_Far_Puddle_01_LOD0", (-1.06, -1.22), (0.60, 0.83), -0.25),
            ("RoadRuts_Far_Puddle_02_LOD0", (1.12, 1.20), (0.62, 1.02), 0.18),
            ("RoadRuts_Far_Puddle_03_LOD0", (-0.88, 3.80), (0.72, 0.92), -0.12),
        )
        glints = (
            ("RoadRuts_Far_PuddleGlint_00_LOD0", (0.83, -3.62), (0.27, 0.17), 0.13),
            ("RoadRuts_Far_PuddleGlint_02_LOD0", (1.10, 1.18), (0.30, 0.16), -0.18),
        )
    else:
        mud_specs = (
            ("RoadRuts_Near_MudBreak_00_LOD0", (-1.17, -3.75), (0.45, 0.50), -0.18),
            ("RoadRuts_Near_MudBreak_01_LOD0", (1.22, -0.15), (0.47, 0.56), 0.16),
            ("RoadRuts_Near_MudBreak_02_LOD0", (-1.02, 1.95), (0.42, 0.58), -0.28),
        )
        puddle_specs = (
            ("RoadRuts_Near_Puddle_00_LOD0", (-0.91, -3.72), (0.66, 1.08), -0.12),
            ("RoadRuts_Near_Puddle_01_LOD0", (1.14, -1.98), (0.61, 0.82), 0.22),
            ("RoadRuts_Near_Puddle_02_LOD0", (-1.16, 0.93), (0.56, 1.04), -0.26),
            ("RoadRuts_Near_Puddle_03_LOD0", (0.87, 3.74), (0.70, 0.92), 0.12),
        )
        glints = (
            ("RoadRuts_Near_PuddleGlint_00_LOD0", (-0.88, -3.68), (0.28, 0.17), -0.10),
            ("RoadRuts_Near_PuddleGlint_02_LOD0", (-1.13, 0.95), (0.29, 0.16), 0.18),
        )

    for index, (name, center, radii, angle) in enumerate(mud_specs):
        rut_break(component, name, center, radii, angle, index)
    for index, (name, center, radii, angle) in enumerate(puddle_specs):
        puddle(component, name, center, radii, angle, index)
    for index, (name, center, radii, angle) in enumerate(glints):
        puddle_glint(component, name, center, radii, angle, index)


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
    for name, bounds in component_bounds.items():
        vertical = bounds[4] - bounds[1]
        longitudinal = bounds[5] - bounds[2]
        if vertical > 0.25:
            raise RuntimeError(f"Roadside vertical relief exceeds 0.25m: {name}: {bounds}")
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
    target_stats = {name: component_stats(bpy.data.objects[name]) for name in TARGETS}
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
    if min_z < 0.012 or max_z > 0.18 or relief > 0.18:
        raise RuntimeError(f"Road relief exceeds shallow contract: z={min_z:.4f}..{max_z:.4f}")
    return {
        "target_stats": target_stats,
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
    for name in TARGETS:
        component = bpy.data.objects.get(name)
        if component is None or component.parent is not root or component.type != "EMPTY":
            raise RuntimeError(f"Missing target component root: {name}")
        remove_children(component)

    build_crown(bpy.data.objects["RoadCrown_SunkenWet"])
    build_ruts(bpy.data.objects["RoadRuts_PuddleNear"], far=False)
    build_ruts(bpy.data.objects["RoadRuts_PuddleFar"], far=True)

    source_report = validate(root)
    bake_report = bake_source_axis_contract(root)
    baked_report = validate_baked_axis_contract(root)

    scene = bpy.context.scene
    scene["generator"] = "assets/source/blender/act1/urman_wet_village_road_kit.py"
    scene["active_geometry_pass"] = "RoadCrown_SunkenWet + RoadRuts_PuddleNear + RoadRuts_PuddleFar; shallow broken extracted-basis relief; linear rut ribbons omitted"
    scene["source_axis_contract"] = "Blender meshes axis-baked +90deg X after child transforms; legacy GLB extraction +90deg X produces Godot-horizontal road"
    scene["geometry_policy"] = "geometry-only; existing WetRoad_* and Puddle_* materials authoritative; presentation-only; no collision"
    for name in TARGETS:
        bpy.data.objects[name]["geometry_pass"] = "active wet-road readability pass 2026-08-26"
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
