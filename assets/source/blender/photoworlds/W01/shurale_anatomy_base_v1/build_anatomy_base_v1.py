#!/usr/bin/env python3
"""Build one source-only Shurale anatomy candidate from CC0 hm08 data."""

from __future__ import annotations

import gzip
import hashlib
import json
import math
import time
from collections import Counter, deque
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
INPUT = ROOT / "inputs"
BASE_OBJ = INPUT / "hm08_base.obj"
VIEWS = ROOT / "views"
BLEND_PATH = ROOT / "shurale_anatomy_base_v1.blend"
MANIFEST_PATH = ROOT / "manifest.json"
STARTED = time.monotonic()

TARGETS = {
    "macrodetails/universal-male-old-minmuscle-minweight.target.gz": {
        "weight": 0.9331099699,
        "sha256": "0d5e260cedd07cd8bacd14f74a5cda27977051cae413906996c59527f372ee42",
    },
    "macrodetails/universal-male-old-minmuscle-averageweight.target.gz": {
        "weight": 0.0186545853,
        "sha256": "8c7923d2afd0a53f317a89aa9a71f8ae57e0a7172ef3b577d4db7fd06c6a7a80",
    },
    "macrodetails/universal-male-old-averagemuscle-minweight.target.gz": {
        "weight": 0.0186545853,
        "sha256": "c9698e079ab31870666948b804c1f3b633338c16a216e2494ede7809969dee40",
    },
    "macrodetails/universal-male-young-minmuscle-minweight.target.gz": {
        "weight": 0.0186545853,
        "sha256": "166a31465a2790a321e55e274a7f43c56bfe58ad1cf443afacd45a7a23abf407",
    },
    "macrodetails/asian-male-old.target.gz": {
        "weight": 0.3203613864,
        "sha256": "8f931e96529904375a442c77be0c31d60ace6c0461645ed7ab1a43a3c378a247",
    },
    "macrodetails/caucasian-male-old.target.gz": {
        "weight": 0.3203613864,
        "sha256": "123cb7758c86aa2aa71d80ea0c8f5f4b3e96f394180edc1682e34720c4949df6",
    },
    "macrodetails/african-male-old.target.gz": {
        "weight": 0.3203613864,
        "sha256": "fb3477ab3c05d6ea421d82717c3df89ff23f926d1e5148092e30c0efe2ea96de",
    },
    "head/head-age-incr.target.gz": {
        "weight": 0.45,
        "sha256": "ea5b7b9404daa1f6b8ea14a11a0e8ae6a082e98d0d65f63cc2150057a168e396",
    },
    "head/head-fat-decr.target.gz": {
        "weight": 0.55,
        "sha256": "6ef17a5ba87b23aa11746ac8c539fef70be5a8f89cae4abb2c2f3f32a9c6cc57",
    },
    "eyes/l-eye-push1-in.target.gz": {
        "weight": 0.35,
        "sha256": "80658fd1ec6967bed34f04be1dcb2fa5cbabb8a17fdae2427e0735ff3474906d",
    },
    "eyes/r-eye-push1-in.target.gz": {
        "weight": 0.35,
        "sha256": "7667d6d4ecfd2bec4f44ea842da3a849ca506378715fffcf39c380ae59d0582a",
    },
    "cheek/l-cheek-bones-incr.target.gz": {
        "weight": 0.35,
        "sha256": "ea5aa60abcc2926e61cdabf5d40701c2b23b3f8027f949cea0b80bcbc55df301",
    },
    "cheek/r-cheek-bones-incr.target.gz": {
        "weight": 0.35,
        "sha256": "c608528b9e5e1c670179cfb502dab3d9478a77bb942f7c8186cd072a7369e9b9",
    },
    "cheek/l-cheek-volume-decr.target.gz": {
        "weight": 0.45,
        "sha256": "202ff036e39abea154e0896ad34641184c5e8b77610dca9e1e8e4ce4a5db1bc4",
    },
    "cheek/r-cheek-volume-decr.target.gz": {
        "weight": 0.45,
        "sha256": "ea12455c7a85e7b3b5e6e4882b4b68823a00bcaef4ea048cc49be452a139c1bc",
    },
    "arms/measure-upperarm-length-incr.target.gz": {
        "weight": 0.85,
        "sha256": "72890d3448778f6d25e3c9abd540582ad36598a832c28fd0b1e16073b1b3f962",
    },
    "arms/measure-lowerarm-length-incr.target.gz": {
        "weight": 0.85,
        "sha256": "d725e5b7a35dad30a2a68fae52ea0efad72ed8ac77b186409a2cdc59d3e2368d",
    },
    "hands/l-hand-fingers-length-incr.target.gz": {
        "weight": 1.0,
        "sha256": "37620de4a7d2f3c8f1b233fc42ebf6c6c6b100ce1aecfe4ed4ada2f0e4006637",
    },
    "hands/r-hand-fingers-length-incr.target.gz": {
        "weight": 1.0,
        "sha256": "ac90a1e7b1776bb41f896b2b5308ab791b860a3fbf4f854f17d56234b3da48a9",
    },
    "asym/asym-brown-1-l.target.gz": {
        "weight": 0.32,
        "sha256": "36b9dd9c8c96107bd1ba83c5629efd09efc4a3e337042bd27c894d16dd369ef9",
    },
}
BASE_SHA256 = "8e761e6624b8f54536409135d1636da63b32486a90d4897f84e121d144f6fb4c"
LICENSE_HASHES = {
    "inputs/licenses/MPFB2-LICENSE.md": "5cefb60680cb9efd4550a2e65d719021cfbec9dad3658481d11d48ae863ce04d",
    "inputs/licenses/MPFB2-LICENSE.ASSETS.md": "f6089cba01cb570a24712b41ab8a586ccd3cc5ef53dc266ca50b95c288956d2c",
}
TARGET_BODY_HEIGHT_M = 2.34


def log(message: str) -> None:
    print(f"[anatomy-base-v1 +{time.monotonic() - STARTED:.1f}s] {message}", flush=True)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require_hash(path: Path, expected: str) -> str:
    actual = sha256(path)
    if actual != expected:
        raise RuntimeError(f"Pinned input hash mismatch for {path}: {actual} != {expected}")
    return actual


def parse_hm08(path: Path):
    raw_vertices: list[Vector] = []
    body_faces: list[tuple[int, ...]] = []
    groups: Counter[str] = Counter()
    marker_indices: dict[str, set[int]] = {}
    current_group = ""
    with path.open("r", encoding="utf-8", errors="strict") as stream:
        for line in stream:
            fields = line.split()
            if not fields:
                continue
            if fields[0] == "v" and len(fields) >= 4:
                raw_vertices.append(Vector(tuple(float(value) for value in fields[1:4])))
            elif fields[0] == "g":
                current_group = " ".join(fields[1:])
            elif fields[0] == "f":
                indices = tuple(int(token.split("/", 1)[0]) - 1 for token in fields[1:])
                groups[current_group] += 1
                if current_group == "body":
                    body_faces.append(indices)
                elif current_group.startswith("joint-"):
                    marker_indices.setdefault(current_group, set()).update(indices)
    if not raw_vertices or not body_faces:
        raise RuntimeError("Pinned hm08 OBJ has no vertices or body faces")
    used = sorted({index for face in body_faces for index in face})
    if min(used) < 0 or max(used) >= len(raw_vertices):
        raise RuntimeError("Body faces reference a vertex outside the pinned OBJ")
    remap = {old: new for new, old in enumerate(used)}
    body = [raw_vertices[index].copy() for index in used]
    faces = [tuple(remap[index] for index in face) for face in body_faces]
    markers = {}
    for name, indices in marker_indices.items():
        if indices:
            markers[name] = sum((raw_vertices[index] for index in indices), Vector()) / len(indices)
    return raw_vertices, used, body, faces, groups, markers


def apply_target(vertices: list[Vector], path: Path, weight: float) -> int:
    changed = 0
    with gzip.open(path, "rt", encoding="utf-8") as stream:
        for line in stream:
            fields = line.split()
            if len(fields) != 4:
                continue
            index = int(fields[0])
            if index < 0 or index >= len(vertices):
                raise RuntimeError(f"Target {path} references missing vertex {index}")
            delta = Vector(tuple(float(value) for value in fields[1:4]))
            vertices[index] += delta * weight
            changed += 1
    return changed


def morph_helper_markers(
    original_vertices: list[Vector],
    morphed_vertices: list[Vector],
    body_global_indices: list[int],
    markers: dict[str, Vector],
    marker_names: tuple[str, ...],
    neighbor_count: int = 24,
):
    """Move point-only helper markers by the nearby body's applied morph deltas."""
    if neighbor_count < 1 or not body_global_indices:
        raise RuntimeError("Cannot recompute helper markers without body vertices")
    result = {}
    details = {}
    for name in marker_names:
        if name not in markers:
            raise RuntimeError(f"Pinned hm08 OBJ is missing required point marker {name}")
        marker = markers[name]
        nearest = sorted(
            ((original_vertices[index] - marker).length_squared, index)
            for index in body_global_indices
        )[:neighbor_count]
        if not nearest:
            raise RuntimeError(f"No body vertices are available near {name}")
        weights = [1.0 / max(distance_squared, 1e-10) for distance_squared, _ in nearest]
        weight_sum = sum(weights)
        displacement = sum(
            ((morphed_vertices[index] - original_vertices[index]) * weight
             for weight, (_, index) in zip(weights, nearest)),
            Vector(),
        ) / weight_sum
        result[name] = marker + displacement
        details[name] = {
            "neighbor_body_vertices": len(nearest),
            "nearest_original_surface_distance_raw": round(math.sqrt(nearest[0][0]), 6),
            "source_raw": [round(value, 6) for value in marker],
            "morphed_raw": [round(value, 6) for value in result[name]],
            "weighted_delta_raw": [round(value, 6) for value in displacement],
        }
    return result, details


def topology_stats(faces: list[tuple[int, ...]], vertex_count: int):
    edge_counts: Counter[tuple[int, int]] = Counter()
    adjacency = [set() for _ in range(vertex_count)]
    used = set()
    for face in faces:
        for index, a in enumerate(face):
            b = face[(index + 1) % len(face)]
            key = (a, b) if a < b else (b, a)
            edge_counts[key] += 1
            adjacency[a].add(b)
            adjacency[b].add(a)
            used.add(a)
            used.add(b)
    unseen = set(used)
    components = 0
    while unseen:
        components += 1
        start = unseen.pop()
        queue = deque([start])
        while queue:
            current = queue.popleft()
            reached = adjacency[current] & unseen
            if reached:
                unseen.difference_update(reached)
                queue.extend(reached)
    nonmanifold = {str(count): sum(1 for edges in edge_counts.values() if edges == count)
                   for count in sorted(set(edge_counts.values())) if count != 2}
    return {
        "vertices_used": len(used),
        "faces": len(faces),
        "edges": len(edge_counts),
        "connected_components": components,
        "boundary_edges": sum(1 for count in edge_counts.values() if count == 1),
        "nonmanifold_edge_incidence_counts": nonmanifold,
        "all_face_edges_degree_two": all(count == 2 for count in edge_counts.values()),
    }


def raw_to_world(point: Vector, scale: float, min_y: float, center_x: float, center_z: float) -> Vector:
    return Vector(((point.x - center_x) * scale,
                   -(point.z - center_z) * scale,
                   (point.y - min_y) * scale))


def make_material(name: str, color: tuple[float, float, float, float], roughness: float):
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    return material


def add_eye(name: str, center: Vector, radius: float, material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, radius=radius, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def add_horn(raw_base: Vector, transform, material, raw_to_meter_scale: float):
    # One short, gently swept brow horn; its closed base is buried in the scalp.
    centers = [
        raw_base + Vector((0.0, -0.105, 0.035)),
        raw_base + Vector((0.0, -0.050, 0.060)),
        raw_base + Vector((-0.015, 0.050, 0.080)),
        raw_base + Vector((-0.025, 0.170, 0.060)),
        raw_base + Vector((-0.040, 0.290, 0.020)),
        raw_base + Vector((-0.052, 0.390, -0.030)),
        raw_base + Vector((-0.060, 0.455, -0.060)),
    ]
    radii_raw = [0.105, 0.113, 0.098, 0.075, 0.048, 0.026, 0.012]
    radii_m = [radius * raw_to_meter_scale for radius in radii_raw]
    centers = [transform(point) for point in centers]
    ring_count = 14
    vertices = []
    faces = []
    for index, center in enumerate(centers):
        before = centers[max(0, index - 1)]
        after = centers[min(len(centers) - 1, index + 1)]
        tangent = (after - before).normalized()
        side = Vector((1.0, 0.0, 0.0))
        if abs(tangent.dot(side)) > 0.94:
            side = Vector((0.0, 1.0, 0.0))
        side = (side - tangent * tangent.dot(side)).normalized()
        other = tangent.cross(side).normalized()
        for step in range(ring_count):
            theta = math.tau * step / ring_count
            co = center + radii_m[index] * (math.cos(theta) * side + math.sin(theta) * other)
            vertices.append(tuple(co))
        if index:
            current = index * ring_count
            previous = current - ring_count
            for step in range(ring_count):
                nxt = (step + 1) % ring_count
                faces.append((previous + step, previous + nxt, current + nxt, current + step))
    faces.append(tuple(reversed(tuple(range(ring_count)))))
    tip_index = len(vertices)
    tip_extension_raw = 0.018
    vertices.append(tuple(
        centers[-1] + (centers[-1] - centers[-2]).normalized() * (tip_extension_raw * raw_to_meter_scale)
    ))
    last_ring = (len(centers) - 1) * ring_count
    for step in range(ring_count):
        faces.append((last_ring + step, last_ring + (step + 1) % ring_count, tip_index))
    mesh = bpy.data.meshes.new("Single short horn | CC0 body source addition")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new("Single short swept horn", mesh)
    bpy.context.scene.collection.objects.link(obj)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return obj


def camera_look(camera, location: Vector, target: Vector):
    camera.location = location
    camera.rotation_euler = (target - location).to_track_quat("-Z", "Y").to_euler()


def add_area(name: str, location: tuple[float, float, float], energy: float, size: float, target: Vector):
    data = bpy.data.lights.new(name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def render_views(actor, views_dir: Path):
    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new("Anatomy review camera")
    camera = bpy.data.objects.new("Anatomy review camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = "ORTHO"
    camera_data.lens = 55
    lights_target = Vector((0.0, 0.0, 1.38))
    add_area("Key softbox", (2.5, -3.0, 3.2), 420, 2.2, lights_target)
    add_area("Front fill", (-2.6, -2.0, 1.7), 240, 2.6, lights_target)
    add_area("Back rim", (-0.4, 2.4, 2.8), 360, 1.8, lights_target)

    # Quiet studio ground and neutral world; no presentation graphics or materials.
    floor_material = make_material("Studio ground gray", (0.105, 0.108, 0.115, 1.0), 0.92)
    bpy.ops.mesh.primitive_plane_add(size=200.0, location=(0.0, 0.0, -0.008))
    floor = bpy.context.object
    floor.name = "Neutral studio floor"
    floor.data.materials.append(floor_material)

    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.eevee.taa_render_samples = 48
    scene.render.resolution_x = 1120
    scene.render.resolution_y = 1560
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world.color = (0.045, 0.049, 0.056)
    views_dir.mkdir(parents=True, exist_ok=True)

    full_target = Vector((0.0, 0.0, 1.22))
    full = [
        ("full-front.png", Vector((0.0, -4.0, 1.85)), full_target, 2.82),
        ("full-three-quarter.png", Vector((2.9, -3.4, 1.78)), full_target, 2.92),
        ("profile.png", Vector((4.1, 0.0, 1.85)), full_target, 2.82),
        ("back.png", Vector((0.0, 4.0, 1.85)), full_target, 2.82),
    ]
    for filename, direction, target, scale in full:
        camera_look(camera, direction, target)
        camera_data.ortho_scale = scale
        scene.render.resolution_x = 1120
        scene.render.resolution_y = 1560
        scene.render.filepath = str(views_dir / filename)
        bpy.ops.render.render(write_still=True)
        log(f"rendered {filename}")

    face_target = Vector((0.0, 0.0, 2.16))
    camera_look(camera, Vector((0.08, -2.4, 2.16)), face_target)
    camera_data.ortho_scale = 0.72
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1400
    scene.render.filepath = str(views_dir / "face-close.png")
    bpy.ops.render.render(write_still=True)
    log("rendered face-close.png")
    return [str(views_dir / name) for name, *_ in full] + [str(views_dir / "face-close.png")]


# --- Pin and parse only licensed geometry data. ---
require_hash(BASE_OBJ, BASE_SHA256)
for relative, expected in LICENSE_HASHES.items():
    require_hash(ROOT / relative, expected)
for relative, item in TARGETS.items():
    require_hash(INPUT / "targets" / relative, item["sha256"])
raw_vertices, body_global_indices, body_faces, body_polygons, group_faces, markers = parse_hm08(BASE_OBJ)
log(f"read hm08 body: {len(body_global_indices)} vertices, {len(body_polygons)} faces; OBJ groups={len(group_faces)}")
for marker in ("joint-l-shoulder", "joint-r-shoulder", "joint-l-elbow", "joint-r-elbow", "joint-l-hand", "joint-r-hand", "joint-l-eye", "joint-r-eye", "joint-neck"):
    if marker not in markers:
        raise RuntimeError(f"Pinned hm08 OBJ is missing required point marker {marker}")

# Apply the official target data in raw hm08 coordinates. Keep the original
# positions so the point-only eye/neck helpers can inherit nearby body deltas.
original_raw_vertices = [point.copy() for point in raw_vertices]
for relative, item in TARGETS.items():
    changed = apply_target(raw_vertices, INPUT / "targets" / relative, item["weight"])
    log(f"applied {relative} at {item['weight']:.2f} to {changed} indexed vertices")
body_points = [raw_vertices[index].copy() for index in body_global_indices]
markers_after_morph, morphed_marker_details = morph_helper_markers(
    original_raw_vertices,
    raw_vertices,
    body_global_indices,
    markers,
    ("joint-l-eye", "joint-r-eye", "joint-neck"),
)

# The forehead base is found on the actual source surface, not placed by a fixed world-space guess.
body_top = max(point.y for point in body_points)
horn_x = -0.10
horn_y = body_top - 0.22
forehead_candidates = [
    point for point in body_points
    if abs(point.x - horn_x) < 0.24 and body_top - 0.74 <= point.y <= body_top - 0.18 and point.z > 0.3
]
if len(forehead_candidates) < 8:
    raise RuntimeError("Cannot identify a stable forehead patch for the single horn")
forehead = min(forehead_candidates, key=lambda point: ((point.x - horn_x) ** 2 + (point.y - horn_y) ** 2, -point.z))
horn_base = Vector((forehead.x, forehead.y, forehead.z - 0.045))

body_min_y = min(point.y for point in body_points)
body_max_y = max(point.y for point in body_points)
body_min_x = min(point.x for point in body_points)
body_max_x = max(point.x for point in body_points)
body_min_z = min(point.z for point in body_points)
body_max_z = max(point.z for point in body_points)
raw_height = body_max_y - body_min_y
if raw_height <= 1e-6:
    raise RuntimeError("Transformed hm08 body has no usable height")
scale = TARGET_BODY_HEIGHT_M / raw_height
center_x = (body_min_x + body_max_x) * 0.5
center_z = (body_min_z + body_max_z) * 0.5
transform = lambda point: raw_to_world(point, scale, body_min_y, center_x, center_z)
world_vertices = [tuple(transform(point)) for point in body_points]
base_topology = topology_stats(body_polygons, len(body_global_indices))

# --- Create one neutral clay source mesh and add only the essential facial/horn geometry. ---
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
clay = make_material("Neutral clay | anatomy source review", (0.47, 0.465, 0.45, 1.0), 0.84)
eye_clay = make_material("Deep eye clay | no emission", (0.16, 0.17, 0.18, 1.0), 0.67)
pupil_clay = make_material("Pupil clay | no emission", (0.055, 0.058, 0.062, 1.0), 0.52)
body_mesh = bpy.data.meshes.new("hm08 body topology | body group only")
body_mesh.from_pydata(world_vertices, [], body_polygons)
body_mesh.materials.append(clay)
body_mesh.update()
body = bpy.data.objects.new("Shurale anatomy base v1 | source-only", body_mesh)
bpy.context.scene.collection.objects.link(body)
for polygon in body_mesh.polygons:
    polygon.use_smooth = True
subdivision = body.modifiers.new("Presentation smoothing | retained base topology", "SUBSURF")
subdivision.subdivision_type = "CATMULL_CLARK"
subdivision.levels = 1
subdivision.render_levels = 2

eye_objects = []
for side, sign in (("l", 1.0), ("r", -1.0)):
    eye_raw = markers_after_morph[f"joint-{side}-eye"].copy()
    eye_raw.z -= 0.035  # Seat the globe behind the lid line; positive raw z is the face.
    eye = transform(eye_raw)
    radius = 0.0155
    eye_objects.append(add_eye(f"hm08 eye globe {side} | deep neutral clay", eye, radius, eye_clay))
    iris_raw = eye_raw.copy()
    iris_raw.x += sign * 0.010
    iris_raw.z += 0.092
    iris = transform(iris_raw)
    eye_objects.append(add_eye(f"Eye iris {side} | matte charcoal clay", iris, 0.0068, pupil_clay))
    pupil_raw = iris_raw.copy()
    pupil_raw.z += 0.0055
    pupil = transform(pupil_raw)
    eye_objects.append(add_eye(f"Eye pupil {side} | matte charcoal clay", pupil, 0.0035, pupil_clay))

horn_material = clay
horn_object = add_horn(horn_base, transform, horn_material, scale)
for part in eye_objects + [horn_object]:
    bpy.ops.object.select_all(action="DESELECT")
    part.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
body.name = "Shurale anatomy base v1 | hm08 body topology plus eyes and one horn"
for polygon in body.data.polygons:
    polygon.use_smooth = True

# Verify finite source geometry and body-only source topology before saving/renders.
for vertex in body.data.vertices:
    if not all(math.isfinite(value) for value in vertex.co):
        raise RuntimeError("Non-finite vertex found in the authored anatomy mesh")
if len(body.data.polygons) < len(body_polygons):
    raise RuntimeError("Authored mesh lost body polygons while adding face geometry")
if len(body_global_indices) != 13380 or len(body_polygons) != 13378:
    raise RuntimeError(
        f"Pinned body group topology changed unexpectedly: {len(body_global_indices)} vertices, {len(body_polygons)} faces"
    )

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.view_transform = "AgX"
scene.world.color = (0.045, 0.049, 0.056)

rendered_views = render_views(body, VIEWS)
bpy.context.scene.camera.data.dof.use_dof = False
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

asset_inputs = [{
    "path": "inputs/hm08_base.obj",
    "source_url": "https://raw.githubusercontent.com/makehumancommunity/mpfb2/v2.0.18/src/mpfb/data/3dobjs/base.obj",
    "sha256": BASE_SHA256,
    "license": "CC0 1.0 Universal; explicitly stated in the OBJ header and pinned release LICENSE.ASSETS.md",
    "used_groups": {"body": group_faces.get("body", 0)},
    "excluded_groups": {name: count for name, count in group_faces.items() if name != "body"},
}, *[{
    "path": f"inputs/targets/{relative}",
    "source_url": f"https://raw.githubusercontent.com/makehumancommunity/mpfb2/v2.0.18/src/mpfb/data/targets/{relative}",
    "sha256": item["sha256"],
    "weight": item["weight"],
    "license": "CC0 1.0 Universal; pinned release LICENSE.ASSETS.md covers targets",
} for relative, item in TARGETS.items()]]
manifest = {
    "candidate": "Shurale anatomy base v1",
    "status": "source-only candidate for art-direction review; not canonical or runtime-ready",
    "authored_with": {
        "blender_version": bpy.app.version_string,
        "generator": "build_anatomy_base_v1.py",
        "python_runtime": bpy.app.version_string,
        "render_engine": scene.render.engine,
    },
    "source_scope": "One HM08 body-group-derived static anatomy source with official CC0 morph target data, the source rest/A-pose, two deep clay eyes placed from post-morph helper markers, and one short horn with dimensions converted from raw hm08 units to meters. No MPFB code/addon, runtime, rig, animation, PBR, or LOD was used or authored.",
    "license_provenance": {
        "repository": "https://github.com/makehumancommunity/mpfb2",
        "pinned_release": "v2.0.18",
        "asset_license": "CC0 1.0 Universal",
        "asset_license_file": "inputs/licenses/MPFB2-LICENSE.ASSETS.md",
        "asset_license_file_url": "https://raw.githubusercontent.com/makehumancommunity/mpfb2/v2.0.18/LICENSE.ASSETS.md",
        "asset_license_file_sha256": LICENSE_HASHES["inputs/licenses/MPFB2-LICENSE.ASSETS.md"],
        "release_license_file": "inputs/licenses/MPFB2-LICENSE.md",
        "release_license_file_url": "https://raw.githubusercontent.com/makehumancommunity/mpfb2/v2.0.18/LICENSE.md",
        "release_license_file_sha256": LICENSE_HASHES["inputs/licenses/MPFB2-LICENSE.md"],
        "code_license_note": "The pinned release lists MPFB Python, shell, and configuration source as GPLv3. No MPFB executable/addon/code/dependencies were downloaded or used; this generator is newly authored for URMAN and uses Blender's bundled bpy API.",
        "attribution_from_obj_header": [
            "Data Collection AB (2020)",
            "Joel Palmius (2020)",
            "Jonas Hauquier (2020)",
        ],
    },
    "inputs": asset_inputs,
    "macro_target_recipe": {
        "pinned_targetservice_url": "https://github.com/makehumancommunity/mpfb2/blob/v2.0.18/src/mpfb/services/targetservice.py#L842-L905",
        "pinned_macro_config_url": "https://raw.githubusercontent.com/makehumancommunity/mpfb2/v2.0.18/src/mpfb/data/targets/macrodetails/macro.json",
        "macro_info": {
            "gender": 1.0,
            "age": 1.0,
            "muscle": 0.0,
            "weight": 0.0,
            "proportions": 0.5,
            "height": 0.5,
            "cupsize": 0.5,
            "firmness": 0.5,
            "race": {"asian": 0.33, "caucasian": 0.33, "african": 0.33},
        },
        "interpolated_component_weights_rounded_to_4_decimals": {
            "gender": {"male": 0.9902},
            "age": {"old": 0.9804, "young": 0.0196},
            "muscle": {"minmuscle": 0.9804, "averagemuscle": 0.0196},
            "weight": {"minweight": 0.9804, "averageweight": 0.0196},
        },
        "target_weights": {
            "macrodetails/universal-male-old-minmuscle-minweight.target.gz": 0.9331099699,
            "macrodetails/universal-male-old-minmuscle-averageweight.target.gz": 0.0186545853,
            "macrodetails/universal-male-old-averagemuscle-minweight.target.gz": 0.0186545853,
            "macrodetails/universal-male-young-minmuscle-minweight.target.gz": 0.0186545853,
            "macrodetails/asian-male-old.target.gz": 0.3203613864,
            "macrodetails/caucasian-male-old.target.gz": 0.3203613864,
            "macrodetails/african-male-old.target.gz": 0.3203613864,
        },
        "weight_basis": "TargetService calculates both universal gender-age-muscle-weight targets and race-gender-age targets, multiplying their interpolated component weights and retaining products above the 0.01 default cutoff. Component values above are rounded as pinned by its interpolation method. No hand-tuned race proportions or MPFB addon code are used.",
        "service_cutoff": 0.01,
    },
    "reference_guidance": {
        "concept_image": "assets/source/blender/photoworlds/W01/shurale-concepts/modern-horror-anatomy-v1.png",
        "use": "Used as anatomy and long-arm reference only; excluded its bark armor, spikes, and superhero muscle plating. The prior v6 smooth hourglass and raw TripoSR body/face/hand failures were read as rejection guidance.",
    },
    "geometry": {
        "coordinate_conversion": "HM08 raw (x, y-up, z-depth) to Blender (x, -z, y); front is raw +z / Blender -y.",
        "body_group_topology_before_additions": base_topology,
        "body_group_polygons_preserved": len(body_polygons),
        "source_vertices_used": len(body_global_indices),
        "body_group_faces_from_obj": group_faces.get("body", 0),
        "pose_state": "Original HM08 source rest/A-pose retained; custom static arm rotations and head field-tilt removed.",
        "post_morph_helper_markers_raw": morphed_marker_details,
        "body_height_m_before_horn": round(raw_height * scale, 6),
        "actor_mesh_objects": sum(1 for obj in scene.objects if obj.type == "MESH" and obj.name.startswith("Shurale anatomy base v1")),
        "scene_mesh_objects_including_floor": sum(1 for obj in scene.objects if obj.type == "MESH"),
        "joined_body_vertex_count_including_eye_and_horn_islands": len(body.data.vertices),
        "joined_body_polygon_count_including_eye_and_horn_islands": len(body.data.polygons),
        "modifier": "Catmull-Clark presentation smoothing, viewport level 1/render level 2; original HM08 body topology remains the editable base mesh.",
        "horn": {
            "count": 1,
            "construction": "Closed swept low-poly horn intersected into the actual forehead surface and joined into the same source mesh object.",
            "base_raw_hm08": [round(value, 6) for value in horn_base],
            "raw_radii": [0.105, 0.113, 0.098, 0.075, 0.048, 0.026, 0.012],
            "radii_space": "All listed radii are hm08 raw units multiplied by body raw-to-meter scale before mesh construction.",
            "tip_extension_raw_hm08": 0.018,
            "tip_extension_m": round(0.018 * scale, 6),
            "no_second_horn": True,
        },
        "eyes": "Eye centers use joint-eye point markers adjusted by inverse-distance-weighted nearby body morph deltas; neutral dark clay geometry is seated behind the lid line, with matte iris/pupil clay and no emission.",
        "materials": "Neutral monochrome clay only; not PBR or final look development.",
    },
    "verification": {
        "input_hashes_matched_before_build": True,
        "body_topology_count_matches_pinned_hm08_group": True,
        "helper_or_joint_cube_geometry_imported": False,
        "finite_joined_mesh_coordinates": True,
        "actor_mesh_objects": sum(1 for obj in scene.objects if obj.type == "MESH" and obj.name.startswith("Shurale anatomy base v1")),
        "scene_mesh_objects_including_floor": sum(1 for obj in scene.objects if obj.type == "MESH"),
        "rendered_views": [str(Path(path).relative_to(ROOT)) for path in rendered_views],
        "saved_blend": str(BLEND_PATH.relative_to(ROOT)),
        "build_seconds": round(time.monotonic() - STARTED, 2),
        "limitations": [
            "This is a static source-art candidate, not rigged, animated, exported, or reviewed in runtime.",
            "The horn and eye globes are intersecting closed islands within the same mesh object; body mesh topology itself remains the pinned hm08 body-group topology.",
            "Neutral clay inspection does not establish final horror art direction, texture, lighting, or author acceptance.",
        ],
    },
}
MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
manifest["verification"]["saved_blend_sha256"] = sha256(BLEND_PATH)
manifest["verification"]["generator_sha256"] = sha256(Path(__file__))
manifest["verification"]["render_sha256"] = {str(Path(path).relative_to(ROOT)): sha256(Path(path)) for path in rendered_views}
MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
log(f"saved {BLEND_PATH} and {len(rendered_views)} neutral review views")
log(f"body topology={base_topology}; output mesh verts={len(body.data.vertices)} polys={len(body.data.polygons)}")
log("candidate source-stage complete; awaiting art-direction review")
