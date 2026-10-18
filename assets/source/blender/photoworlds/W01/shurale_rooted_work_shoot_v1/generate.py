"""Author a short curved W01 F14 rooted shoot that meets Shurale's work branch.

Run with the pinned Blender 4.5+ build:
  .tools/blender/Blender.app/Contents/MacOS/Blender --background \
    --python assets/source/blender/photoworlds/W01/shurale_rooted_work_shoot_v1/generate.py

The runtime candidate contains only one F14-local, non-colliding shoot mesh.
Preview-only inputs use the frozen F14 support blend and the reviewed Shurale
source-stage GLB; neither input is copied into the candidate output.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.geometry import closest_point_on_tri
from mathutils.bvhtree import BVHTree


ASSET_NAME = "shurale-rooted-work-shoot-v2"
SOURCE_DIR = Path(__file__).resolve().parent
REPO_ROOT = Path(__file__).resolve().parents[6]
OUTPUT_DIR = SOURCE_DIR / "export-v1"
REJECTED_DIR = OUTPUT_DIR / "rejected-v1"

SUPPORT_BLEND = REPO_ROOT / "assets/source/blender/photoworlds/W01/f14-old-root-support-v1.blend"
SUPPORT_MANIFEST = REPO_ROOT / "assets/source/blender/photoworlds/W01/f14-old-root-support-v1.manifest.json"
SHURALE_ORIGINAL_GLB = REPO_ROOT / "assets/source/blender/photoworlds/W01/shurale_character_v6/export-v1/shurale_v1_candidate.glb"
SHURALE_ORIGINAL_MANIFEST = REPO_ROOT / "assets/source/blender/photoworlds/W01/shurale_character_v6/export-v1/manifest.json"
SHURALE_GLB = REPO_ROOT / "assets/source/blender/photoworlds/W01/shurale_character_v6/export-v1/history/59f31b23/shurale_v1_candidate.glb"
SHURALE_MANIFEST = REPO_ROOT / "assets/source/blender/photoworlds/W01/shurale_character_v6/export-v1/history/59f31b23/manifest.json"

# F14-local Godot metres. These are measured on the frozen support and the
# frame-16 work pose of the frozen Shurale GLB (SHA recorded in the manifest).
SUPPORT_CONTACT = Vector((2.220475197, 1.245729208, 0.451900005))
SHURALE_BRANCH_CONTACT = Vector((2.304120779, 1.779515743, 0.402982324))
SHURALE_ROOT_GODOT = Vector((2.9, 0.0, 0.15))
SHURALE_YAW_DEG = 90.0
SHURALE_BRANCH_LOCAL_GODOT = Vector((-0.252982318, 1.779515743, -0.595879197))
SHURALE_CONTACT_NORMAL_GODOT = Vector((-0.585439, -0.806476, 0.082817)).normalized()

EXPECTED_ACTIONS = ("work", "noticed", "gesture", "speak", "yield", "step", "idle")
MAX_ACCEPTABLE_CONTACT_DRIFT_M = 0.005
BAKED_REPEAT_M = 1.0
RING_SIDES = 28
SAMPLES_PER_SEGMENT = 12
SHURALE_BRANCH_MATERIAL_NAME = "Shurale | exportable darkwood branch"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def godot_to_blender(point):
    x, y, z = point
    return Vector((x, -z, y))


def blender_to_godot(point):
    x, y, z = point
    return Vector((x, z, -y))


def ensure_output_dir():
    if OUTPUT_DIR.exists():
        expected = {
            f"{ASSET_NAME}.blend",
            f"{ASSET_NAME}.glb",
            f"{ASSET_NAME}-f14-contact-preview.png",
            f"{ASSET_NAME}-f14-relationship-preview.png",
            "manifest.json",
            REJECTED_DIR.name,
        }
        unexpected = {path.name for path in OUTPUT_DIR.iterdir()} - expected
        if unexpected:
            raise RuntimeError(f"Refusing to overwrite unknown stage outputs: {sorted(unexpected)}")
        if not REJECTED_DIR.is_dir() or not (REJECTED_DIR / "manifest.json").is_file():
            raise RuntimeError("The rejected v1 candidate must remain archived before generating v2")
    else:
        OUTPUT_DIR.mkdir(parents=True, exist_ok=False)


def catmull_rom(points, radii, samples_per_segment=SAMPLES_PER_SEGMENT):
    vectors = [Vector(point) for point in points]
    output_points, output_radii = [], []
    for segment in range(len(vectors) - 1):
        p0 = vectors[max(0, segment - 1)]
        p1 = vectors[segment]
        p2 = vectors[segment + 1]
        p3 = vectors[min(len(vectors) - 1, segment + 2)]
        r0, r1, r2, r3 = [radii[max(0, min(len(radii) - 1, i))]
                          for i in (segment - 1, segment, segment + 1, segment + 2)]
        for step in range(samples_per_segment):
            t = step / samples_per_segment
            t2, t3 = t * t, t * t * t
            point = 0.5 * ((2 * p1) + (-p0 + p2) * t
                           + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
                           + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            radius = 0.5 * ((2 * r1) + (-r0 + r2) * t
                            + (2 * r0 - 5 * r1 + 4 * r2 - r3) * t2
                            + (-r0 + 3 * r1 - 3 * r2 + r3) * t3)
            output_points.append(point)
            output_radii.append(max(0.006, radius))
    output_points.append(vectors[-1])
    output_radii.append(max(0.006, radii[-1]))
    return output_points, output_radii


def make_swept_mesh(name, points, radii, material, root):
    points, radii = catmull_rom(points, radii)
    vertices, uv_by_vertex, faces = [], [], []
    arc = [0.0]
    for a, b in zip(points, points[1:]):
        arc.append(arc[-1] + (b - a).length)

    previous_tangent = None
    previous_normal = None
    for index, center in enumerate(points):
        if index == 0:
            tangent = (points[1] - points[0]).normalized()
        elif index == len(points) - 1:
            tangent = (points[-1] - points[-2]).normalized()
        else:
            tangent = (points[index + 1] - points[index - 1]).normalized()

        if previous_tangent is None:
            reference = Vector((0.0, 1.0, 0.0))
            if abs(tangent.dot(reference)) > 0.92:
                reference = Vector((1.0, 0.0, 0.0))
            normal = tangent.cross(reference).normalized()
        else:
            rotation = previous_tangent.rotation_difference(tangent)
            normal = rotation @ previous_normal
            normal = (normal - tangent * normal.dot(tangent)).normalized()
        binormal = tangent.cross(normal).normalized()

        t = index / (len(points) - 1)
        # Keep the shoot narrow and softly irregular instead of adding hard
        # collars or exaggerated swellings at either joint.
        growth = 1.0 + 0.012 * math.sin(t * 7.0 + 0.4)
        for side in range(RING_SIDES + 1):
            angle = math.tau * side / RING_SIDES
            bark_irregularity = (
                1.0
                + 0.018 * math.sin(3.0 * angle + 0.7 + t * 3.4)
                + 0.008 * math.cos(5.0 * angle - 0.4 + t * 6.2)
            )
            radial = radii[index] * growth * bark_irregularity
            # A quiet oval section reads as a young woody shoot, not a pipe.
            vertex = center + radial * (0.82 * math.cos(angle) * normal + math.sin(angle) * binormal)
            vertices.append(tuple(godot_to_blender(vertex)))
            uv_by_vertex.append((arc[index] / BAKED_REPEAT_M, side / RING_SIDES * 4.0))
        previous_tangent = tangent
        previous_normal = normal

    stride = RING_SIDES + 1
    for ring in range(len(points) - 1):
        for side in range(RING_SIDES):
            a = ring * stride + side
            b = a + 1
            c = (ring + 1) * stride + side + 1
            d = c - 1
            faces.append((a, b, c, d))

    # The end caps are intentionally buried inside the existing root/held
    # branch, so their circular boundaries are not visible in the assembly.
    start_center = len(vertices)
    vertices.append(tuple(godot_to_blender(points[0])))
    uv_by_vertex.append((0.0, 2.0))
    end_center = len(vertices)
    vertices.append(tuple(godot_to_blender(points[-1])))
    uv_by_vertex.append((arc[-1] / BAKED_REPEAT_M, 2.0))
    for side in range(RING_SIDES):
        faces.append((start_center, side + 1, side))
        end_ring = (len(points) - 1) * stride
        faces.append((end_center, end_ring + side, end_ring + side + 1))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap_MetricBark")
    for polygon in mesh.polygons:
        polygon.use_smooth = True
        for loop_index in polygon.loop_indices:
            vertex_index = mesh.loops[loop_index].vertex_index
            uv_layer.data[loop_index].uv = uv_by_vertex[vertex_index]
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    obj.parent = root
    obj.matrix_parent_inverse.identity()
    bpy.context.collection.objects.link(obj)
    obj["semantic_role"] = "one curved, tapered rooted work shoot; visual only"
    obj["collision"] = "none"
    obj["coordinate_frame"] = "F14-local Godot metres; Blender source maps (X,-Z,Y)"
    # Recalculate outward normals after using the curved parallel-transport frame.
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)
    return obj, points, radii


def mesh_world_triangles(obj, depsgraph):
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    matrix = evaluated.matrix_world
    vertices = [matrix @ vertex.co for vertex in mesh.vertices]
    triangles = [tuple(triangle.vertices) for triangle in mesh.loop_triangles]
    evaluated.to_mesh_clear()
    return vertices, triangles


def bvh_from_object(obj, depsgraph):
    vertices, triangles = mesh_world_triangles(obj, depsgraph)
    return BVHTree.FromPolygons(vertices, triangles, all_triangles=True), len(triangles)


def closest_surface_to_point(obj, point_world, depsgraph):
    vertices, triangles = mesh_world_triangles(obj, depsgraph)
    nearest = None
    for triangle in triangles:
        closest = closest_point_on_tri(point_world, *(vertices[index] for index in triangle))
        distance = (closest - point_world).length
        if nearest is None or distance < nearest[0]:
            nearest = (distance, closest)
    return nearest


def sample_branch_contact_motion(scene, rig, branch, contact_world):
    animation_data = rig.animation_data
    if animation_data is None:
        raise RuntimeError("Imported Shurale rig has no animation data")
    for track in animation_data.nla_tracks:
        track.mute = True
    if hasattr(animation_data, "use_nla"):
        animation_data.use_nla = False

    result = {}
    for clip in EXPECTED_ACTIONS:
        action = bpy.data.actions.get(clip)
        if action is None:
            raise RuntimeError(f"Frozen Shurale GLB is missing the {clip!r} action")
        animation_data.action = action
        first, last = (int(round(value)) for value in action.frame_range)
        distances = []
        for frame in range(first, last + 1):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            depsgraph = bpy.context.evaluated_depsgraph_get()
            nearest = closest_surface_to_point(branch, contact_world, depsgraph)
            if nearest is None:
                raise RuntimeError(f"Could not sample ShuraleWorkBranch in {clip} frame {frame}")
            distances.append((nearest[0], frame, nearest[1]))
        maximum = max(distances, key=lambda sample: sample[0])
        minimum = min(distances, key=lambda sample: sample[0])
        result[clip] = {
            "frames_sampled": len(distances),
            "nearest_contact_min_distance_m": round(minimum[0], 6),
            "nearest_contact_min_frame": minimum[1],
            "nearest_contact_max_distance_m": round(maximum[0], 6),
            "nearest_contact_max_frame": maximum[1],
        }
    return result


def triangles_and_bounds(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    points = [blender_to_godot(obj.matrix_world @ vertex.co) for vertex in mesh.vertices]
    return {
        "vertex_count": len(mesh.vertices),
        "triangle_count": len(mesh.loop_triangles),
        "f14_local_godot_bounds_m": {
            axis: [round(min(point[index] for point in points), 6),
                   round(max(point[index] for point in points), 6)]
            for index, axis in enumerate(("x", "y", "z"))
        },
    }


def render_assembly(scene, shurale_root, rig, close_output_path, relationship_output_path):
    lod0 = bpy.data.objects.get("ShuraleBody_LOD0")
    lod1 = bpy.data.objects.get("ShuraleBody_LOD1")
    if lod0 is None or lod1 is None:
        raise RuntimeError("Preview needs the imported Shurale LOD0 and LOD1 nodes")
    lod0.hide_render = False
    lod0.hide_viewport = False
    lod1.hide_render = True
    lod1.hide_viewport = True
    for track in rig.animation_data.nla_tracks:
        track.mute = True
    if hasattr(rig.animation_data, "use_nla"):
        rig.animation_data.use_nla = False
    rig.animation_data.action = bpy.data.actions["work"]
    scene.frame_set(16)
    bpy.context.view_layer.update()

    fill_data = bpy.data.lights.new("Preview_F14_RootedShoot_Fill", "AREA")
    fill_data.energy = 460
    fill_data.shape = "DISK"
    fill_data.size = 3.2
    fill = bpy.data.objects.new("Preview_F14_RootedShoot_Fill", fill_data)
    scene.collection.objects.link(fill)
    fill.location = godot_to_blender((1.6, 3.3, 0.8))
    fill.rotation_euler = (godot_to_blender((2.5, 1.25, 0.30)) - fill.location).to_track_quat("-Z", "Y").to_euler()

    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1700
    scene.render.resolution_y = 1280
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.eevee.taa_render_samples = 48

    def render_camera(name, godot_position, target_godot, lens, output_path):
        camera_data = bpy.data.cameras.new(name)
        camera = bpy.data.objects.new(name, camera_data)
        scene.collection.objects.link(camera)
        camera.location = godot_to_blender(godot_position)
        target = godot_to_blender(target_godot)
        camera.rotation_mode = "XYZ"
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera_data.lens = lens
        scene.camera = camera
        scene.render.filepath = str(output_path)
        camera.hide_set(False)
        bpy.ops.render.render(write_still=True)
        camera.hide_set(True)
        return camera

    render_camera(
        "Preview_F14_RootedShoot_RelationshipCamera",
        (0.35, 3.25, 3.55), (2.56, 1.28, 0.20), 35,
        relationship_output_path)
    camera = render_camera(
        "Preview_F14_RootedShoot_ContactCamera",
        (1.15, 2.55, 2.65), (2.57, 1.42, 0.28), 54,
        close_output_path)
    fill.hide_set(True)
    return camera


def build_manifest(script_path, blend_path, glb_path, preview_path, relationship_preview_path, mesh_metrics,
                   contact_metrics, motion_metrics, reimport_metrics, branch_material_name, branch_material_color,
                   control_points, radii):
    shurale_doc = json.loads(SHURALE_MANIFEST.read_text(encoding="utf-8"))
    max_motion = max(item["nearest_contact_max_distance_m"] for item in motion_metrics.values())
    document = {
        "schema": 1,
        "asset_id": "A-HERO-ROOT / W01 F14 Shurale rooted work shoot",
        "version": ASSET_NAME,
        "status": "source-stage production candidate; no runtime promotion or art acceptance",
        "supersedes_rejected_candidate": {
            "path": str(REJECTED_DIR.relative_to(REPO_ROOT)),
            "manifest_sha256": sha256(REJECTED_DIR / "manifest.json"),
            "reason": "review rejected the previous stem as too thick, capped, and visibly collared",
        },
        "source_script": str(script_path.relative_to(REPO_ROOT)),
        "source_script_sha256": sha256(script_path),
        "inputs": {
            "f14_support_blend": {"path": str(SUPPORT_BLEND.relative_to(REPO_ROOT)), "sha256": sha256(SUPPORT_BLEND)},
            "f14_support_manifest": {"path": str(SUPPORT_MANIFEST.relative_to(REPO_ROOT)), "sha256": sha256(SUPPORT_MANIFEST)},
            "f14_support_mesh": "F14_OldRootSupport_LOD0 from the frozen support blend; exact source material reused",
            "shurale_source_glb": {
                "path": str(SHURALE_GLB.relative_to(REPO_ROOT)),
                "sha256": sha256(SHURALE_GLB),
                "original_production_source_path": str(SHURALE_ORIGINAL_GLB.relative_to(REPO_ROOT)),
            },
            "shurale_manifest": {
                "path": str(SHURALE_MANIFEST.relative_to(REPO_ROOT)),
                "sha256": sha256(SHURALE_MANIFEST),
                "original_production_manifest_path": str(SHURALE_ORIGINAL_MANIFEST.relative_to(REPO_ROOT)),
            },
            "shurale_expected_frozen_sha256": "59f31b23b41c021935e5ca80606b659260c5c86beb09a6d557817f4b7ec4713e",
        },
        "consumer": "W01 F14 visual-only rooted shoot connecting the existing old-root support to ShuraleWorkBranch",
        "runtime_candidate": {
            "prospective_path": "res://assets/photoworlds/W01/shurale_rooted_work_shoot_v1.glb",
            "runtime_copy_created": False,
            "root_node": "W01_RootedWorkShoot_v2",
            "mesh_node": "W01_RootedWorkShoot_Mesh_LOD0",
            "coordinate_frame": "F14-local Godot metres; Blender source maps (X,-Z,Y)",
            "root_transform_owner": "W01 scene instance under Anchors/F14; GLB root stays identity",
            "collision": "none; visual GLB only",
            "material": f"exact {branch_material_name} Principled material reused from the frozen Shurale work-branch GLB; no new texture",
        },
        "contacts": {
            "f14_support_surface_f14_local_godot_xyz_m": [round(value, 9) for value in SUPPORT_CONTACT],
            "support_surface_normal_f14_local_godot_xyz": contact_metrics["support_normal_godot"],
            "shurale_work_branch_surface_f14_local_godot_xyz_m": [round(value, 9) for value in SHURALE_BRANCH_CONTACT],
            "shurale_branch_surface_normal_godot_xyz": [round(value, 6) for value in SHURALE_CONTACT_NORMAL_GODOT],
            "shurale_root_marker_f14_local_godot_xyz_m": [round(value, 6) for value in SHURALE_ROOT_GODOT],
            "shurale_root_yaw_deg": SHURALE_YAW_DEG,
            "shurale_branch_local_godot_xyz_m": [round(value, 9) for value in SHURALE_BRANCH_LOCAL_GODOT],
            "endpoint_embedding_m": {
                "old_root_center_below_surface": 0.055,
                "shurale_work_branch_center_below_surface": 0.028,
                "old_root_radius_m": 0.025,
                "shurale_work_branch_radius_m": 0.008,
                "old_root_cap_bury_clearance_m": 0.030,
                "shurale_work_branch_cap_bury_clearance_m": 0.020,
            },
            "measured": contact_metrics,
        },
        "geometry": {
            **mesh_metrics,
            "construction": "single thin Catmull-Rom swept shoot with a softly irregular centerline, small oval cross-section, smooth 0.025-to-0.008m taper, branch-matched dark Principled material, metric UVMap, and fully buried end caps",
            "centerline_control_points_f14_local_godot_xyz_m": [
                [round(value, 6) for value in control_points[0]],
                [round(value, 6) for value in control_points[1]],
                [round(value, 6) for value in control_points[2]],
                [round(value, 6) for value in control_points[3]],
                [round(value, 6) for value in control_points[4]],
                [round(value, 6) for value in control_points[5]],
                [round(value, 6) for value in control_points[6]],
            ],
            "radius_control_points_m": radii,
            "cross_section_minor_to_major_ratio": 0.82,
            "motion_contact_drift_limit_m": MAX_ACCEPTABLE_CONTACT_DRIFT_M,
            "shurale_branch_fixed_contact_motion": motion_metrics,
            "maximum_sampled_branch_contact_gap_m": round(max_motion, 6),
            "branch_contact_stability_within_limit": max_motion <= MAX_ACCEPTABLE_CONTACT_DRIFT_M,
        },
        "materials_provenance": {
            "source_glb": str(SHURALE_GLB.relative_to(REPO_ROOT)),
            "original_production_source_glb": str(SHURALE_ORIGINAL_GLB.relative_to(REPO_ROOT)),
            "source_material": branch_material_name,
            "principled_base_color_linear": [round(value, 8) for value in branch_material_color],
            "source_surface_manifest": shurale_doc.get("source", {}).get("surface_manifest"),
            "branch_material_has_texture": False,
            "third_party_textures_added": False,
        },
        "artifacts": {
            "blend": {"path": blend_path.name, "sha256": sha256(blend_path), "bytes": blend_path.stat().st_size},
            "glb": {"path": glb_path.name, "sha256": sha256(glb_path), "bytes": glb_path.stat().st_size},
            "preview": {"path": preview_path.name, "sha256": sha256(preview_path), "bytes": preview_path.stat().st_size},
            "relationship_preview": {"path": relationship_preview_path.name, "sha256": sha256(relationship_preview_path), "bytes": relationship_preview_path.stat().st_size},
            "reimported_glb_metrics": reimport_metrics,
        },
        "verification": {
            "source_stage_generation": "completed with pinned Blender",
            "support_and_work_branch_contact": "closest surfaces and triangle overlaps measured against the actual imported source meshes",
            "shurale_animation_contact": "all seven imported clips sampled over every integer frame; see geometry.shurale_branch_fixed_contact_motion",
            "native_godot_import": "not run",
            "runtime_visual_and_collision": "not run; candidate has no collision",
            "full_shurale_art_acceptance": "pending; this adds only one rooted shoot and does not resolve the character art review",
        },
    }
    path = OUTPUT_DIR / "manifest.json"
    path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return path


def verify_glb_reimport(glb_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(glb_path))
    root = bpy.data.objects.get("W01_RootedWorkShoot_v2")
    mesh = bpy.data.objects.get("W01_RootedWorkShoot_Mesh_LOD0")
    if root is None or mesh is None or mesh.parent != root:
        raise RuntimeError("Reimported candidate lost its explicit root/mesh node contract")
    metrics = triangles_and_bounds(mesh)
    if metrics["triangle_count"] < 1000 or metrics["triangle_count"] > 10000:
        raise RuntimeError(f"Unexpected reimport triangle count: {metrics['triangle_count']}")
    if not mesh.data.materials:
        raise RuntimeError("Reimported candidate has no exported bark material")
    if not mesh.data.uv_layers:
        raise RuntimeError("Reimported candidate lost its authored UV map")
    return {
        **metrics,
        "root_transform_identity": all(abs(value) < 1e-5 for value in (*root.location, *root.rotation_euler))
            and all(abs(value - 1.0) < 1e-5 for value in root.scale),
        "material_count": len(mesh.data.materials),
        "material_names": [material.name for material in mesh.data.materials if material],
        "uv_layers": [uv.name for uv in mesh.data.uv_layers],
        "root_children": [child.name for child in root.children],
    }


def main():
    ensure_output_dir()
    bpy.context.preferences.filepaths.save_version = 0
    if sha256(SHURALE_GLB) != "59f31b23b41c021935e5ca80606b659260c5c86beb09a6d557817f4b7ec4713e":
        raise RuntimeError("Shurale source GLB changed from the reviewed branch-contact input")
    bpy.ops.wm.open_mainfile(filepath=str(SUPPORT_BLEND))
    scene = bpy.context.scene
    support_root = bpy.data.objects.get("F14_OldRootSupport")
    support_mesh = bpy.data.objects.get("F14_OldRootSupport_LOD0")
    if support_root is None or support_mesh is None:
        raise RuntimeError("Frozen F14 support blend is missing its root or mesh")

    bpy.ops.import_scene.gltf(filepath=str(SHURALE_GLB))
    shurale_root = bpy.data.objects.get("ShuraleRoot")
    rig = bpy.data.objects.get("ShuraleRig")
    branch = bpy.data.objects.get("ShuraleWorkBranch")
    if shurale_root is None or rig is None or branch is None or branch.parent_bone != "hand_l":
        raise RuntimeError("Frozen Shurale import missed the expected root, rig, or hand_l branch")
    branch_material = next((material for material in branch.data.materials
                            if material and material.name == SHURALE_BRANCH_MATERIAL_NAME), None)
    if branch_material is None or not branch_material.use_nodes:
        raise RuntimeError("Frozen held branch is missing its exact exportable darkwood Principled material")
    principled = next((node for node in branch_material.node_tree.nodes
                       if node.type == "BSDF_PRINCIPLED"), None)
    if principled is None:
        raise RuntimeError("Held branch darkwood material has no Principled BSDF")
    base_color = tuple(principled.inputs["Base Color"].default_value)
    branch_material_name = branch_material.name
    if max(base_color[:3]) > 0.12 or principled.inputs["Roughness"].default_value < 0.55:
        raise RuntimeError(f"Held branch material is no longer the approved dark, rough wood: {base_color}")
    shurale_root.location = godot_to_blender(SHURALE_ROOT_GODOT)
    # GLTF import uses quaternion rotation mode, so setting rotation_euler
    # alone would silently leave the actor unyawed in this Blender preview.
    shurale_root.rotation_mode = "XYZ"
    shurale_root.rotation_euler = (0.0, 0.0, math.radians(SHURALE_YAW_DEG))

    # Guard the reviewed transform against a silent axis/marker drift.
    radians = math.radians(SHURALE_YAW_DEG)
    local = SHURALE_BRANCH_LOCAL_GODOT
    transformed = Vector((
        SHURALE_ROOT_GODOT.x + math.cos(radians) * local.x + math.sin(radians) * local.z,
        SHURALE_ROOT_GODOT.y + local.y,
        SHURALE_ROOT_GODOT.z - math.sin(radians) * local.x + math.cos(radians) * local.z,
    ))
    if (transformed - SHURALE_BRANCH_CONTACT).length > 0.00001:
        raise RuntimeError(f"Shurale marker transform no longer reaches the reviewed contact: {transformed}")

    # Query the actual support source mesh before authoring the buried base.
    support_point_blender = godot_to_blender(SUPPORT_CONTACT)
    found, closest, support_normal_blender, polygon_index = support_mesh.closest_point_on_mesh(support_point_blender)
    if not found or (closest - support_point_blender).length > 0.001:
        raise RuntimeError("Reviewed F14 support contact is no longer on the frozen source mesh")
    support_normal_godot = blender_to_godot(support_normal_blender).normalized()
    root_embed = 0.055
    shurale_embed = 0.028
    base_center = SUPPORT_CONTACT - support_normal_godot * root_embed
    tip_center = SHURALE_BRANCH_CONTACT - SHURALE_CONTACT_NORMAL_GODOT * shurale_embed

    control_points = [
        base_center,
        base_center + support_normal_godot * 0.060,
        Vector((2.244, 1.390, 0.455)),
        Vector((2.304, 1.500, 0.425)),
        Vector((2.344, 1.625, 0.390)),
        tip_center + SHURALE_CONTACT_NORMAL_GODOT * 0.065,
        tip_center,
    ]
    radii = [0.025, 0.023, 0.020, 0.017, 0.014, 0.010, 0.008]
    shoot_root = bpy.data.objects.new("W01_RootedWorkShoot_v2", None)
    shoot_root.empty_display_type = "PLAIN_AXES"
    shoot_root.empty_display_size = 0.18
    bpy.context.scene.collection.objects.link(shoot_root)
    shoot_root["asset_role"] = "F14-local visual connector from old root support to Shurale's held work branch"
    shoot_root["collision"] = "none"
    shoot_root["source_axis_contract"] = "F14-local Godot metres; Blender source maps (X,-Z,Y)"
    shoot, sampled_points, sampled_radii = make_swept_mesh(
        "W01_RootedWorkShoot_Mesh_LOD0", control_points, radii, branch_material, shoot_root)

    # Contact checks use actual evaluated source meshes, not the AABB or a
    # hidden presentation-only connector.
    scene.frame_set(16)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    support_tree, support_triangles = bvh_from_object(support_mesh, depsgraph)
    shoot_tree, shoot_triangles = bvh_from_object(shoot, depsgraph)
    support_overlap = len(support_tree.overlap(shoot_tree))
    if support_overlap == 0:
        raise RuntimeError("The shoot does not intersect the measured F14 root surface")

    branch_contact_blender = godot_to_blender(SHURALE_BRANCH_CONTACT)
    branch_nearest = closest_surface_to_point(branch, branch_contact_blender, depsgraph)
    if branch_nearest is None or branch_nearest[0] > 0.002:
        raise RuntimeError(f"Reimported Shurale branch no longer matches the reviewed contact: {branch_nearest}")
    branch_tree, branch_triangles = bvh_from_object(branch, depsgraph)
    branch_overlap = len(branch_tree.overlap(shoot_tree))

    motion_metrics = sample_branch_contact_motion(scene, rig, branch, branch_contact_blender)
    motion_max = max(item["nearest_contact_max_distance_m"] for item in motion_metrics.values())

    # Restore the authored work frame for source preview, then render the three
    # real pieces together: old support, imported hand-held branch, and shoot.
    rig.animation_data.action = bpy.data.actions["work"]
    scene.frame_set(16)
    bpy.context.view_layer.update()
    preview_path = OUTPUT_DIR / f"{ASSET_NAME}-f14-contact-preview.png"
    relationship_preview_path = OUTPUT_DIR / f"{ASSET_NAME}-f14-relationship-preview.png"
    camera = render_assembly(scene, shurale_root, rig, preview_path, relationship_preview_path)

    blend_path = OUTPUT_DIR / f"{ASSET_NAME}.blend"
    glb_path = OUTPUT_DIR / f"{ASSET_NAME}.glb"
    bpy.ops.object.select_all(action="DESELECT")
    shoot_root.select_set(True)
    shoot.select_set(True)
    bpy.context.view_layer.objects.active = shoot_root
    scene.camera = camera
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path), export_format="GLB", use_selection=True,
        export_yup=True, export_apply=True, export_texcoords=True,
        export_normals=True, export_tangents=True, export_materials="EXPORT",
        export_cameras=False, export_lights=False,
    )
    if not all(path.is_file() for path in (blend_path, glb_path, preview_path, relationship_preview_path)):
        raise RuntimeError("Rooted shoot stage missed a required generated artifact")

    mesh_metrics = triangles_and_bounds(shoot)
    contact_metrics = {
        "support_source_mesh_nearest_distance_m": round((closest - support_point_blender).length, 6),
        "support_source_mesh_nearest_polygon": int(polygon_index),
        "support_normal_godot": [round(value, 6) for value in support_normal_godot],
        "support_shoot_surface_triangle_overlaps": support_overlap,
        "support_source_triangles": support_triangles,
        "shoot_triangles": shoot_triangles,
        "shurale_work_branch_nearest_surface_distance_m_at_frame16": round(branch_nearest[0], 6),
        "shurale_branch_shoot_surface_triangle_overlaps_at_frame16": branch_overlap,
        "shurale_branch_source_triangles": branch_triangles,
        "old_root_embed_m": root_embed,
        "shurale_branch_embed_m": shurale_embed,
    }
    reimport_metrics = verify_glb_reimport(glb_path)
    manifest_path = build_manifest(Path(__file__).resolve(), blend_path, glb_path,
                                   preview_path, relationship_preview_path, mesh_metrics, contact_metrics,
                                   motion_metrics, reimport_metrics, branch_material_name,
                                   base_color, control_points, radii)
    print(json.dumps({
        "output_dir": str(OUTPUT_DIR),
        "blend": str(blend_path),
        "glb": str(glb_path),
        "preview": str(preview_path),
        "relationship_preview": str(relationship_preview_path),
        "manifest": str(manifest_path),
        "mesh": mesh_metrics,
        "contacts": contact_metrics,
        "motion_contact_drift_m": motion_metrics,
        "maximum_sampled_contact_gap_m": round(motion_max, 6),
        "motion_stability_within_5mm": motion_max <= MAX_ACCEPTABLE_CONTACT_DRIFT_M,
        "reimport": reimport_metrics,
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
