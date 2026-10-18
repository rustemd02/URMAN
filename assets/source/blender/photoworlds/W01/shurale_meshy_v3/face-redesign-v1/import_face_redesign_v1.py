"""Import the pinned head/bust GLB unchanged for source-only anatomy review."""

from collections import defaultdict
from datetime import datetime, timezone
from hashlib import sha256
import json
import math
from pathlib import Path
import struct

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "meshy71-face-redesign-original.glb"
INPUT_IMAGE = ROOT / "input-face-redesign-v1.png"
INPUT_PROVENANCE = ROOT / "input-face-redesign-v1.provenance.json"
EXPECTED_SOURCE_SHA256 = "4a2ff86ad0e0add821c7e657ed24592a3d94b34f3e187bcef6412661507f2edc"
EXPECTED_INPUT_SHA256 = "d8d504c193ea29731a8c4d0c059e9501770305d7b81df72ac206d4f02d8909f9"
BLEND_OUT = ROOT / "shurale_face_redesign_v1_source.blend"
MANIFEST_OUT = ROOT / "manifest.json"
VIEW_DIR = ROOT / "views"
VIEW_DIR.mkdir(exist_ok=True)


def file_sha256(path):
    digest = sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_glb_json(path):
    with path.open("rb") as stream:
        header = stream.read(12)
        if len(header) != 12:
            raise RuntimeError("GLB header is truncated")
        magic, version, declared_length = struct.unpack("<4sII", header)
        if magic != b"glTF" or version != 2 or declared_length != path.stat().st_size:
            raise RuntimeError("Pinned source is not a complete glTF 2.0 binary")
        while True:
            chunk_header = stream.read(8)
            if not chunk_header:
                break
            chunk_length, chunk_type = struct.unpack("<II", chunk_header)
            if chunk_type == 0x4E4F534A:
                chunk = stream.read(chunk_length)
                if len(chunk) != chunk_length:
                    raise RuntimeError("GLB JSON chunk is truncated")
                return json.loads(chunk.decode("utf-8").rstrip(" \t\r\n\x00"))
            stream.seek(chunk_length, 1)
    raise RuntimeError("GLB has no JSON chunk")


def json_safe(value):
    if isinstance(value, (str, int, bool)) or value is None:
        return value
    if isinstance(value, float):
        return value if math.isfinite(value) else None
    if isinstance(value, Vector):
        return [json_safe(item) for item in value]
    if isinstance(value, (tuple, list)):
        return [json_safe(item) for item in value]
    if isinstance(value, dict):
        return {str(key): json_safe(item) for key, item in value.items()}
    return str(value)


def vec3(values):
    return [round(float(value), 8) for value in values]


def finite_vec(values):
    return all(math.isfinite(float(value)) for value in values)


def world_bounds(mesh_objects):
    points = [obj.matrix_world @ Vector(corner) for obj in mesh_objects for corner in obj.bound_box]
    if not points:
        raise RuntimeError("Imported head GLB has no mesh bounds")
    minimum = Vector(min(point[i] for point in points) for i in range(3))
    maximum = Vector(max(point[i] for point in points) for i in range(3))
    return minimum, maximum, (minimum + maximum) * 0.5


def component_record(mesh):
    parent = list(range(len(mesh.vertices)))
    rank = [0] * len(parent)

    def find(item):
        while parent[item] != item:
            parent[item] = parent[parent[item]]
            item = parent[item]
        return item

    def union(left, right):
        left_root, right_root = find(left), find(right)
        if left_root == right_root:
            return
        if rank[left_root] < rank[right_root]:
            left_root, right_root = right_root, left_root
        parent[right_root] = left_root
        if rank[left_root] == rank[right_root]:
            rank[left_root] += 1

    for edge in mesh.edges:
        union(int(edge.vertices[0]), int(edge.vertices[1]))
    vertices = defaultdict(int)
    faces = defaultdict(int)
    triangles = defaultdict(int)
    for vertex in mesh.vertices:
        vertices[find(vertex.index)] += 1
    for polygon in mesh.polygons:
        if polygon.vertices:
            root = find(int(polygon.vertices[0]))
            faces[root] += 1
            triangles[root] += max(0, len(polygon.vertices) - 2)
    largest = sorted(
        ({"vertices": count, "faces": faces[root], "triangles": triangles[root]}
         for root, count in vertices.items()),
        key=lambda item: (item["triangles"], item["vertices"]), reverse=True,
    )
    return {
        "count": len(vertices),
        "largest": largest[:12],
        "isolated_vertices": sum(
            count == 1 and faces[root] == 0 for root, count in vertices.items()
        ),
    }


def edge_boundary_record(mesh):
    edge_by_key = {tuple(sorted(int(v) for v in edge.vertices)): edge.index for edge in mesh.edges}
    face_use = [0] * len(mesh.edges)
    for polygon in mesh.polygons:
        vertices = [int(v) for v in polygon.vertices]
        for index, vertex in enumerate(vertices):
            following = vertices[(index + 1) % len(vertices)]
            edge_index = edge_by_key.get(tuple(sorted((vertex, following))))
            if edge_index is not None:
                face_use[edge_index] += 1
    return {
        "boundary_edges_one_face": sum(count == 1 for count in face_use),
        "wire_edges_zero_faces": sum(count == 0 for count in face_use),
        "nonmanifold_edges_not_two_faces": sum(count != 2 for count in face_use),
    }


def material_record(material):
    record = {
        "name": material.name,
        "use_nodes": bool(material.use_nodes),
        "diffuse_color": [round(float(value), 6) for value in material.diffuse_color],
        "blend_method": getattr(material, "surface_render_method", None),
        "images": [],
        "node_types": [],
    }
    if material.use_nodes and material.node_tree:
        record["node_types"] = [node.type for node in material.node_tree.nodes]
        for node in material.node_tree.nodes:
            if node.type == "BSDF_PRINCIPLED":
                record["principled"] = {
                    "base_color": list(node.inputs["Base Color"].default_value),
                    "metallic": float(node.inputs["Metallic"].default_value),
                    "roughness": float(node.inputs["Roughness"].default_value),
                }
            if node.type == "TEX_IMAGE" and node.image:
                image = node.image
                record["images"].append({
                    "node": node.name,
                    "name": image.name,
                    "size": list(image.size),
                    "channels": int(image.channels),
                    "source": image.source,
                    "packed": bool(image.packed_file),
                    "colorspace": image.colorspace_settings.name,
                })
    return json_safe(record)


def mesh_record(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    normals = [loop.normal[:] for loop in mesh.loops]
    finite_normals = all(finite_vec(normal) for normal in normals)
    normal_lengths = [math.sqrt(sum(float(c) ** 2 for c in normal))
                      for normal in normals if finite_vec(normal)]
    minimum = Vector(min(vertex.co[i] for vertex in mesh.vertices) for i in range(3)) if mesh.vertices else None
    maximum = Vector(max(vertex.co[i] for vertex in mesh.vertices) for i in range(3)) if mesh.vertices else None
    return {
        "object": obj.name,
        "mesh": mesh.name,
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "faces": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "all_vertex_positions_finite": all(finite_vec(vertex.co) for vertex in mesh.vertices),
        "components": component_record(mesh),
        "edge_boundaries": edge_boundary_record(mesh),
        "local_bounds_min": vec3(minimum) if minimum else None,
        "local_bounds_max": vec3(maximum) if maximum else None,
        "world_dimensions": vec3(obj.dimensions),
        "world_matrix": [[round(float(obj.matrix_world[row][column]), 8) for column in range(4)]
                         for row in range(4)],
        "material_slots": [slot.material.name if slot.material else None for slot in obj.material_slots],
        "uv_layers": [layer.name for layer in mesh.uv_layers],
        "color_attributes": [
            {"name": attr.name, "domain": attr.domain, "data_type": attr.data_type, "length": len(attr.data)}
            for attr in mesh.color_attributes
        ],
        "shape_keys": [key.name for key in mesh.shape_keys.key_blocks] if mesh.shape_keys else [],
        "modifiers": [modifier.type for modifier in obj.modifiers],
        "normals": {
            "imported_loop_normal_count": len(normals),
            "finite": finite_normals,
            "length_min": round(min(normal_lengths), 8) if normal_lengths else None,
            "length_max": round(max(normal_lengths), 8) if normal_lengths else None,
            "smooth_polygon_count": sum(polygon.use_smooth for polygon in mesh.polygons),
            "flat_polygon_count": sum(not polygon.use_smooth for polygon in mesh.polygons),
        },
    }


def gltf_primitive_records(gltf):
    accessors = gltf.get("accessors", [])
    records = []
    for mesh_index, source_mesh in enumerate(gltf.get("meshes", [])):
        for primitive_index, primitive in enumerate(source_mesh.get("primitives", [])):
            attributes = {}
            for name, accessor_index in primitive.get("attributes", {}).items():
                accessor = accessors[accessor_index]
                attributes[name] = {
                    "accessor": accessor_index,
                    "count": accessor.get("count"),
                    "type": accessor.get("type"),
                    "component_type": accessor.get("componentType"),
                    "min": accessor.get("min"),
                    "max": accessor.get("max"),
                }
            records.append({
                "mesh_index": mesh_index,
                "mesh_name": source_mesh.get("name"),
                "primitive_index": primitive_index,
                "material_index": primitive.get("material"),
                "attributes": attributes,
                "index_count": accessors[primitive["indices"]].get("count") if "indices" in primitive else None,
                "morph_target_count": len(primitive.get("targets", [])),
            })
    return records


def add_review_setup(scene, target, overall_radius):
    setup = bpy.data.collections.new("HEAD SOURCE REVIEW | cameras and lights")
    scene.collection.children.link(setup)
    camera_data = bpy.data.cameras.new("Face redesign review camera")
    camera = bpy.data.objects.new("Face redesign review camera", camera_data)
    setup.objects.link(camera)
    camera_data.type = "ORTHO"
    scene.camera = camera

    def area(name, offset, power, size):
        data = bpy.data.lights.new(name, type="AREA")
        data.energy = power
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        setup.objects.link(light)
        light.location = target + Vector(offset)
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()

    area("Review key", (-overall_radius, -overall_radius * 1.4, overall_radius * 1.15), 95, overall_radius * 0.8)
    area("Review fill", (overall_radius * 1.25, -overall_radius * 0.6, overall_radius * 0.25), 45, overall_radius)
    area("Review rim", (overall_radius * 0.25, overall_radius * 1.2, overall_radius * 0.9), 85, overall_radius * 0.72)
    return camera


def render_view(scene, camera, filename, target, direction, ortho_scale, width, height):
    direction = Vector(direction).normalized()
    camera.location = target + direction * 5.0
    camera.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = float(ortho_scale)
    scene.render.resolution_x = int(width)
    scene.render.resolution_y = int(height)
    scene.render.filepath = str(VIEW_DIR / filename)
    bpy.ops.render.render(write_still=True)
    return {
        "path": (VIEW_DIR / filename).relative_to(ROOT).as_posix(),
        "sha256": file_sha256(VIEW_DIR / filename),
        "resolution": [int(width), int(height)],
        "camera_direction": vec3(direction),
        "target": vec3(target),
        "ortho_scale": round(float(ortho_scale), 7),
    }


if bpy.app.version_string != "4.5.12 LTS":
    raise RuntimeError(f"Expected repository-pinned Blender 4.5.12 LTS, got {bpy.app.version_string}")
if file_sha256(SOURCE) != EXPECTED_SOURCE_SHA256:
    raise RuntimeError("Pinned face/bust GLB SHA-256 changed")
if file_sha256(INPUT_IMAGE) != EXPECTED_INPUT_SHA256:
    raise RuntimeError("Pinned own input image SHA-256 changed")
input_provenance = json.loads(INPUT_PROVENANCE.read_text(encoding="utf-8"))
gltf = read_glb_json(SOURCE)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
scene = bpy.context.scene
imported_objects = list(scene.objects)
mesh_objects = [obj for obj in imported_objects if obj.type == "MESH"]
if not mesh_objects:
    raise RuntimeError("Pinned head/bust GLB imported without mesh objects")
minimum, maximum, center = world_bounds(mesh_objects)
extent = maximum - minimum
if not finite_vec(extent) or min(extent) <= 0:
    raise RuntimeError(f"Invalid imported mesh bounds: {vec3(minimum)} .. {vec3(maximum)}")
height = float(extent.z)
width = float(extent.x)
depth = float(extent.y)

scene.render.engine = "CYCLES"
scene.cycles.samples = 32
scene.cycles.use_denoising = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "AgX"
scene.view_settings.look = "AgX - Medium High Contrast"
scene.view_settings.exposure = 0.0
scene.world = bpy.data.worlds.new("Face redesign source review world")
scene.world.use_nodes = True
background = scene.world.node_tree.nodes.get("Background")
background.inputs["Color"].default_value = (0.11, 0.12, 0.13, 1.0)
background.inputs["Strength"].default_value = 0.16
camera = add_review_setup(scene, center, max(extent.length, 0.5))
camera.data.lens = 50
camera.data.dof.use_dof = False

views = {}
views["bust-three-quarter-pbr.png"] = render_view(
    scene, camera, "bust-three-quarter-pbr.png", center, (0.42, -0.88, 0.18),
    max(height * 1.12, width * 1.22), 1200, 1400,
)

clay = bpy.data.materials.new("FACE REDESIGN REVIEW ONLY | neutral gray clay")
clay.use_nodes = True
clay_bsdf = clay.node_tree.nodes.get("Principled BSDF")
clay_bsdf.inputs["Base Color"].default_value = (0.30, 0.31, 0.32, 1.0)
clay_bsdf.inputs["Roughness"].default_value = 0.86
scene.view_layers[0].material_override = clay
directions = {
    "bust-front-neutral.png": (0.0, -1.0, 0.0),
    "bust-three-quarter-neutral.png": (0.42, -0.88, 0.18),
    "bust-profile-neutral.png": (1.0, 0.0, 0.0),
}
for filename, direction in directions.items():
    views[filename] = render_view(
        scene, camera, filename, center, direction,
        max(height * 1.12, width * 1.22), 1200, 1400,
    )
face_target = Vector((center.x, center.y, minimum.z + height * 0.77))
views["face-front-neutral.png"] = render_view(
    scene, camera, "face-front-neutral.png", face_target, (0.0, -1.0, 0.0),
    max(height * 0.62, width * 0.78, 0.2), 1400, 1200,
)

mesh_metrics = [mesh_record(obj) for obj in mesh_objects]
materials = {}
for obj in mesh_objects:
    for material in obj.data.materials:
        if material and material.name not in materials:
            materials[material.name] = material_record(material)
totals = {
    "mesh_objects": len(mesh_objects),
    "vertices": sum(item["vertices"] for item in mesh_metrics),
    "edges": sum(item["edges"] for item in mesh_metrics),
    "faces": sum(item["faces"] for item in mesh_metrics),
    "triangles": sum(item["triangles"] for item in mesh_metrics),
    "uv_layer_count": sum(len(item["uv_layers"]) for item in mesh_metrics),
    "material_slot_count": sum(len(item["material_slots"]) for item in mesh_metrics),
    "connected_component_count": sum(item["components"]["count"] for item in mesh_metrics),
    "boundary_edge_count": sum(item["edge_boundaries"]["boundary_edges_one_face"] for item in mesh_metrics),
    "wire_edge_count": sum(item["edge_boundaries"]["wire_edges_zero_faces"] for item in mesh_metrics),
    "nonmanifold_edge_count": sum(item["edge_boundaries"]["nonmanifold_edges_not_two_faces"] for item in mesh_metrics),
    "all_vertex_positions_finite": all(item["all_vertex_positions_finite"] for item in mesh_metrics),
    "all_imported_loop_normals_finite": all(item["normals"]["finite"] for item in mesh_metrics),
    "world_bounds_min": vec3(minimum),
    "world_bounds_max": vec3(maximum),
    "dimensions": vec3(extent),
}
source_scene_content = {
    "object_type_counts": {kind: sum(obj.type == kind for obj in imported_objects)
                            for kind in sorted({obj.type for obj in imported_objects})},
    "armature_objects": [obj.name for obj in imported_objects if obj.type == "ARMATURE"],
    "actions": sorted(action.name for action in bpy.data.actions),
    "skin_count": len(gltf.get("skins", [])),
    "animation_count": len(gltf.get("animations", [])),
}

# Keep all imported geometry/materials intact; only the review layer overrides them for clay views.
for image in bpy.data.images:
    if image.source == "FILE" and image.packed_file is None:
        try:
            image.pack()
        except RuntimeError:
            pass
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT), compress=True)

manifest = {
    "schema_version": 1,
    "artifact": "Shurale face redesign v1 | source anatomy and PBR review",
    "status": "single source-only import; not fitted, sculpted, reduced, rigged, or accepted as final art",
    "created_at_utc": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
    "generation": {
        "service": "Meshy",
        "model": "7.1",
        "root_reported_mode": "Standard head/bust with 4K PBR",
        "credits_spent": 30,
        "source_prompt": None,
        "task_id": None,
        "seed": None,
        "privacy_and_export_rights": "Not asserted by this import. Generation settings above are root-reported; exact task metadata and account/plan settings were not read from Meshy. This local review does not publish the output.",
    },
    "input": {
        "path": INPUT_IMAGE.name,
        "sha256": file_sha256(INPUT_IMAGE),
        "bytes": INPUT_IMAGE.stat().st_size,
        "provenance_path": INPUT_PROVENANCE.name,
        "provenance_sha256": file_sha256(INPUT_PROVENANCE),
        "source_references": input_provenance.get("source_references", []),
    },
    "source": {
        "path": SOURCE.name,
        "sha256": file_sha256(SOURCE),
        "bytes": SOURCE.stat().st_size,
        "source_mesh_modified": False,
    },
    "import": {
        "blender_version": bpy.app.version_string,
        "blender_build_hash": bpy.app.build_hash.decode("ascii") if isinstance(bpy.app.build_hash, bytes) else bpy.app.build_hash,
        "method": "Pinned Blender glTF importer; source mesh topology, transforms, material graphs, UV/color attributes, and normals were not changed. Clay is only a view-layer override for neutral anatomy views.",
        "review_material_override": {
            "name": clay.name,
            "scope": "Cycles view-layer override; imported PBR material slots and textures remain intact",
            "base_color": [0.30, 0.31, 0.32, 1.0],
            "roughness": 0.86,
            "samples": scene.cycles.samples,
            "denoising": scene.cycles.use_denoising,
        },
        "authoring_script": {"path": Path(__file__).name, "sha256": file_sha256(Path(__file__)),
                             "bytes": Path(__file__).stat().st_size},
        "gltf_asset": gltf.get("asset", {}),
        "gltf_json_top_level_keys": sorted(gltf.keys()),
        "gltf_primitive_attributes": gltf_primitive_records(gltf),
        "source_scene_content": source_scene_content,
        "saved_blend": {"path": BLEND_OUT.name, "sha256": file_sha256(BLEND_OUT),
                        "bytes": BLEND_OUT.stat().st_size},
    },
    "geometry": {"totals": totals, "meshes": mesh_metrics},
    "materials": list(materials.values()),
    "views": views,
    "limitations": [
        "This is one unmodified high-source import. It is not a retopology, face fit, rig, or canonical actor candidate.",
        "The PBR render documents the imported material appearance; neutral clay views are the evidence for orbital volume, eyelids, nose, lips, and horn silhouette.",
        "No texture, topology, normals, proportions, or source transforms were edited; no new Meshy generation was run.",
        "Exact prompt, task ID, seed, privacy selection, and plan-specific rights were not present in the local handoff and are left unknown.",
    ],
}
MANIFEST_OUT.write_text(json.dumps(json_safe(manifest), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({
    "source_sha256": file_sha256(SOURCE),
    "source_bytes": SOURCE.stat().st_size,
    "blend": str(BLEND_OUT),
    "manifest": str(MANIFEST_OUT),
    "totals": totals,
    "views": views,
    "materials": list(materials.values()),
}, ensure_ascii=False, indent=2))
