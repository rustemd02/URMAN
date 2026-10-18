"""Assemble a complete, non-canonical native candidate from the frozen hero source and v2 PBR geometry."""

from __future__ import annotations

import hashlib
import json
import re
from collections import Counter
from pathlib import Path

import bpy
from mathutils import Matrix


HERE = Path(__file__).resolve().parent
SOURCE_BLEND = HERE / "fullhero.blend"
SEMANTIC_GLB = HERE / "hero-house-semantic-pbr-v2.glb"
MANIFEST_PATH = HERE / "manifest.json"
OUTPUT_GLB = HERE / "hero-house-native-pbr-v2.glb"
ROOT_NAME = "HeroHouse_TimberPlaster"
PBR_MATERIAL_NAME = "URMAN_Hero_PBR_v2"
OUTPUT_MANIFEST_KEY = "native_candidate_v2"
V3_MODE = "--v3" in __import__("sys").argv
if V3_MODE:
    SEMANTIC_GLB = HERE / "hero-house-semantic-pbr-v3.glb"
    OUTPUT_GLB = HERE / "hero-house-native-pbr-v3.glb"
    PBR_MATERIAL_NAME = "URMAN_Hero_PBR_v3"
    OUTPUT_METADATA = HERE / "hero-house-native-pbr-v3.metadata.json"

WINDOW_PREFIXES = {
    "Front0": "HeroHouse_Street_Window1_",
    "Front1": "HeroHouse_Street_Window2_",
    "Front2": "HeroHouse_Street_Window3_",
    "Rear0": "HeroHouse_Rear_Window0_",
    "Rear1": "HeroHouse_Rear_Window1_",
    "Left0": "HeroHouse_Left_Window0_",
    "Right0": "HeroHouse_Right_Window0_",
    "SeniOuter0": "HeroHouse_SeniOuter_Window0_",
}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sha256_json(value) -> str:
    encoded = json.dumps(value, separators=(",", ":"), sort_keys=True).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def rows(matrix):
    return [[round(float(value), 9) for value in row] for row in matrix]


def matrix_from_rows(data):
    return Matrix(tuple(tuple(float(value) for value in row) for row in data))


def matrix_close(left, right, tolerance=1e-6):
    return max(abs(float(left[row][col]) - float(right[row][col]))
               for row in range(4) for col in range(4)) <= tolerance


def blender_base_name(name):
    return re.sub(r"\.\d{3}$", "", name)


def parent_names_from_root(obj, root):
    names = []
    parent = obj.parent
    while parent is not None and parent is not root:
        names.append(parent.name)
        parent = parent.parent
    if parent is not root:
        raise RuntimeError(f"{obj.name} is not under component root {root.name}")
    return list(reversed(names))


def mesh_payload(obj):
    mesh = obj.data
    return {
        "vertices": [[round(float(v), 9) for v in vertex.co] for vertex in mesh.vertices],
        "polygons": [list(poly.vertices) for poly in mesh.polygons],
        "polygon_material_indices": [int(poly.material_index) for poly in mesh.polygons],
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
        "uv_layers": {
            layer.name: [[round(float(v), 9) for v in item.uv] for item in layer.data]
            for layer in mesh.uv_layers
        },
    }


def mesh_payload_hash(obj):
    return sha256_json(mesh_payload(obj))


def triangle_surface_rows(obj, root):
    mesh = obj.data
    mesh.calc_loop_triangles()
    uv = mesh.uv_layers.active or (mesh.uv_layers[0] if mesh.uv_layers else None)
    root_relative = root.matrix_world.inverted_safe() @ obj.matrix_world
    surfaces = []
    point_uvs = set()
    for triangle in mesh.loop_triangles:
        polygon = mesh.polygons[triangle.polygon_index]
        material = (mesh.materials[polygon.material_index].name
                    if polygon.material_index < len(mesh.materials)
                    and mesh.materials[polygon.material_index] else None)
        corners = []
        for loop_index in triangle.loops:
            vertex_index = mesh.loops[loop_index].vertex_index
            point = root_relative @ mesh.vertices[vertex_index].co
            position = tuple(round(float(value), 6) for value in point)
            texcoord = (tuple(round(float(value), 7) for value in uv.data[loop_index].uv)
                        if uv else None)
            point_uv = (position, texcoord)
            point_uvs.add(point_uv)
            corners.append(point_uv)
        surfaces.append((material, tuple(sorted(corners))))
    surfaces.sort()
    return surfaces, sorted(point_uvs)


def transform_signature(obj, root):
    return rows(root.matrix_world.inverted_safe() @ obj.matrix_world)


def require_unique_mesh_names(meshes, label):
    names = [obj.name for obj in meshes]
    duplicates = sorted(name for name, count in Counter(names).items() if count != 1)
    if duplicates:
        raise RuntimeError(f"{label} has duplicate mesh object names: {duplicates[:10]}")
    return set(names)


def main():
    required_inputs = [SOURCE_BLEND, SEMANTIC_GLB, MANIFEST_PATH]
    if V3_MODE:
        required_inputs.extend([
            HERE / "opaquehero-for-retexture-v3.metadata.json",
            HERE / "hero-house-semantic-pbr-v3-attachment.metadata.json",
        ])
    for path in required_inputs:
        if not path.is_file():
            raise FileNotFoundError(path)
    if OUTPUT_GLB.exists():
        raise FileExistsError(f"Refusing to overwrite native candidate: {OUTPUT_GLB}")
    if V3_MODE and OUTPUT_METADATA.exists():
        raise FileExistsError(f"Refusing to overwrite native candidate metadata: {OUTPUT_METADATA}")
    if bpy.app.version[:3] != (4, 5, 12):
        raise RuntimeError(f"Expected pinned Blender 4.5.12, got {bpy.app.version_string}")

    source_manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    component = source_manifest["component"]
    hidden_names = set(component["runtime_hidden_wall_courses"])
    excluded_names = {row["name"] for row in component["excluded_nodes"]}
    if len(hidden_names) != 91:
        raise RuntimeError(f"Expected the recorded 91 suppressed wall courses, got {len(hidden_names)}")
    if len(excluded_names) != len(component["excluded_nodes"]):
        raise RuntimeError("Source manifest has duplicate excluded mesh names")
    root_matrix_after_preview = matrix_from_rows(component["root_local_transform_after_preview_removal"])

    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))
    source_root = bpy.data.objects.get(ROOT_NAME)
    if source_root is None:
        raise RuntimeError(f"Source blend has no {ROOT_NAME} component root")
    source_objects = [source_root, *source_root.children_recursive]
    source_meshes = [obj for obj in source_objects if obj.type == "MESH"]
    source_names = require_unique_mesh_names(source_meshes, "Source blend")
    if len(source_meshes) != component["original_mesh_node_count"]:
        raise RuntimeError(f"Source mesh inventory changed: {len(source_meshes)} != {component['original_mesh_node_count']}")
    if not hidden_names.issubset(source_names) or not excluded_names.issubset(source_names):
        raise RuntimeError("Recorded suppressed/excluded nodes are missing from fullhero.blend")

    source_original_root_matrix = rows(source_root.matrix_local)
    expected_source_root = component["original_root_local_transform_blender"]
    if source_original_root_matrix != expected_source_root:
        raise RuntimeError("fullhero.blend root transform differs from its preparation manifest")
    source_by_name = {obj.name: obj for obj in source_objects}
    source_material_by_name = {material.name: material for material in bpy.data.materials}
    source_hidden_snapshots = {
        name: {
            "mesh_payload_sha256": mesh_payload_hash(source_by_name[name]),
            "parent_path": parent_names_from_root(source_by_name[name], source_root),
            "root_relative_transform": transform_signature(source_by_name[name], source_root),
        }
        for name in sorted(hidden_names)
    }
    source_excluded_snapshots = {
        name: {
            "mesh_payload_sha256": mesh_payload_hash(source_by_name[name]),
            "parent_path": parent_names_from_root(source_by_name[name], source_root),
            "root_relative_transform": transform_signature(source_by_name[name], source_root),
            "root_relative_surface_sha256": sha256_json(triangle_surface_rows(source_by_name[name], source_root)[0]),
            "root_relative_position_uv_points_sha256": sha256_json(triangle_surface_rows(source_by_name[name], source_root)[1]),
        }
        for name in sorted(excluded_names)
    }

    # The recorded (34, .8, 0) is the Blender preview-board placement, not the shipped component pose.
    source_root.matrix_local = root_matrix_after_preview
    bpy.context.view_layer.update()
    if not matrix_close(source_root.matrix_world, root_matrix_after_preview):
        raise RuntimeError("Normalized source component root is not at the recorded component transform")

    source_object_names = {obj: obj.name for obj in source_objects}
    for obj in source_objects:
        obj.name = "__native_source__" + source_object_names[obj]
    bpy.context.view_layer.update()
    prior_objects = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(SEMANTIC_GLB))
    donor_objects = set(bpy.context.scene.objects) - prior_objects
    donor_root = next((obj for obj in donor_objects if obj.name == ROOT_NAME), None)
    if donor_root is None:
        raise RuntimeError(f"Semantic v2 GLB did not import its {ROOT_NAME} root")
    donor_meshes = [obj for obj in donor_objects if obj.type == "MESH"]
    donor_names = require_unique_mesh_names(donor_meshes, "Semantic v2 GLB")
    crown_names = {name for name in donor_names if name.endswith("_LogCrown")}
    if crown_names != {
        "HeroHouse_Left_LogCrown",
        "HeroHouse_Rear_LogCrown",
        "HeroHouse_Right_LogCrown",
        "HeroHouse_Street_LogCrown",
    }:
        raise RuntimeError(f"Unexpected baked crown inventory: {sorted(crown_names)}")
    donor_original_names = donor_names - crown_names
    expected_donor_names = source_names - hidden_names - excluded_names
    if donor_original_names != expected_donor_names:
        raise RuntimeError(
            "Semantic v2 names do not exactly match the source opaque inventory: "
            f"missing={sorted(expected_donor_names - donor_original_names)[:10]}, "
            f"extra={sorted(donor_original_names - expected_donor_names)[:10]}"
        )
    if len(donor_meshes) != 473 or len(donor_original_names) != 469:
        raise RuntimeError(f"Unexpected semantic v2 inventory: {len(donor_meshes)} total / {len(donor_original_names)} source")
    donor_by_name = {obj.name: obj for obj in donor_meshes}

    pbr_materials = [material for material in bpy.data.materials if material.name == PBR_MATERIAL_NAME]
    if len(pbr_materials) != 1:
        raise RuntimeError(f"Expected one imported {PBR_MATERIAL_NAME} material, got {len(pbr_materials)}")

    semantic_transfer = {}
    for name in sorted(donor_original_names):
        target = source_by_name[name]
        donor = donor_by_name[name]
        if target.type != "MESH":
            raise RuntimeError(f"Opaque semantic source is not a mesh: {name}")
        source_transform = transform_signature(target, source_root)
        donor_transform = transform_signature(donor, donor_root)
        if not matrix_close(source_transform, donor_transform):
            raise RuntimeError(
                f"Semantic transform differs from normalized source root: {name}; "
                f"source={source_transform}; v2={donor_transform}"
            )
        parent_path = parent_names_from_root(target, source_root)
        if parent_path != parent_names_from_root(donor, donor_root):
            raise RuntimeError(f"Semantic parent path differs from fullhero source: {name}")
        if PBR_MATERIAL_NAME not in [mat.name for mat in donor.data.materials if mat]:
            raise RuntimeError(f"v2 PBR material is missing on semantic mesh: {name}")

        material_names = []
        for index, material in enumerate(list(donor.data.materials)):
            if material is None:
                material_names.append(None)
                continue
            if material.name == PBR_MATERIAL_NAME:
                material_names.append(material.name)
                continue
            candidates = [source_material_by_name.get(material.name),
                          source_material_by_name.get(blender_base_name(material.name))]
            source_material = next((candidate for candidate in candidates if candidate is not None), None)
            if source_material is None:
                raise RuntimeError(f"Could not bind v2 cap material to its unchanged source slot: {name}/{material.name}")
            donor.data.materials[index] = source_material
            material_names.append(source_material.name)

        original_parent = target.parent
        original_basis = target.matrix_basis.copy()
        target.data = donor.data
        target.parent = original_parent
        target.matrix_basis = original_basis
        bpy.context.view_layer.update()
        if not matrix_close(transform_signature(target, source_root), donor_transform):
            raise RuntimeError(f"Transferred opaque mesh moved from its v2 component position: {name}")
        if mesh_payload_hash(target) != mesh_payload_hash(donor):
            raise RuntimeError(f"Transferred opaque position/UV payload differs from v2 source: {name}")
        surfaces, _ = triangle_surface_rows(target, source_root)
        surface_materials = Counter(row[0] for row in surfaces)
        semantic_transfer[name] = {
            "position_uv_mesh_payload_sha256": mesh_payload_hash(target),
            "root_relative_surface_sha256": sha256_json(surfaces),
            "parent_path": parent_path,
            "root_relative_transform": source_transform,
            "material_slots": material_names,
            "triangles_by_material": dict(sorted(surface_materials.items(), key=lambda item: str(item[0]))),
            "exact_v2_mesh_payload_transferred": True,
        }

    crown_transfer = {}
    for name in sorted(crown_names):
        donor = donor_by_name[name]
        donor_parent = donor.parent
        if donor_parent is None or donor_parent.name not in source_by_name:
            raise RuntimeError(f"Baked crown has no corresponding source parent: {name}")
        target_parent = source_by_name[donor_parent.name]
        donor.name = "__native_donor_crown__" + name
        target = bpy.data.objects.new(name, donor.data)
        bpy.context.scene.collection.objects.link(target)
        target.parent = target_parent
        target.matrix_basis = donor.matrix_basis.copy()
        for key in donor.keys():
            target[key] = donor[key]
        bpy.context.view_layer.update()
        if not matrix_close(transform_signature(target, source_root), transform_signature(donor, donor_root)):
            raise RuntimeError(f"Baked crown placement differs from v2 source: {name}")
        if mesh_payload_hash(target) != mesh_payload_hash(donor):
            raise RuntimeError(f"Baked crown position/UV payload differs from v2 source: {name}")
        surfaces, _ = triangle_surface_rows(target, source_root)
        crown_transfer[name] = {
            "position_uv_mesh_payload_sha256": mesh_payload_hash(target),
            "root_relative_surface_sha256": sha256_json(surfaces),
            "parent_path": parent_names_from_root(target, source_root),
            "root_relative_transform": transform_signature(target, source_root),
            "exact_v2_mesh_payload_transferred": True,
        }

    for name in sorted(hidden_names):
        bpy.data.objects.remove(source_by_name[name], do_unlink=True)
    for obj in donor_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    for obj, original_name in source_object_names.items():
        if original_name not in hidden_names:
            obj.name = original_name
    bpy.context.view_layer.update()

    final_root = bpy.data.objects.get(ROOT_NAME)
    if final_root is not source_root:
        raise RuntimeError("Original semantic component root was not restored")
    final_meshes = [obj for obj in [final_root, *final_root.children_recursive] if obj.type == "MESH"]
    final_names = require_unique_mesh_names(final_meshes, "Assembled native candidate")
    expected_final_names = source_names - hidden_names | crown_names
    if final_names != expected_final_names:
        raise RuntimeError(
            "Assembled candidate mesh-name inventory is incomplete or contains extras: "
            f"missing={sorted(expected_final_names - final_names)[:20]}, "
            f"extra={sorted(final_names - expected_final_names)[:20]}, "
            f"actual_count={len(final_names)} expected_count={len(expected_final_names)}"
        )
    if len(final_meshes) != 579:
        raise RuntimeError(f"Expected 579 assembled mesh nodes, got {len(final_meshes)}")
    if sum(obj.name == "HeroHouse_StreetDoorClosed_LOD0" for obj in final_meshes) != 1:
        raise RuntimeError("Candidate must contain exactly one HeroHouse_StreetDoorClosed_LOD0")

    excluded_scene_checks = {}
    for name, before in source_excluded_snapshots.items():
        obj = source_by_name[name]
        after = {
            "mesh_payload_sha256": mesh_payload_hash(obj),
            "parent_path": parent_names_from_root(obj, final_root),
            "root_relative_transform": transform_signature(obj, final_root),
            "root_relative_surface_sha256": sha256_json(triangle_surface_rows(obj, final_root)[0]),
            "root_relative_position_uv_points_sha256": sha256_json(triangle_surface_rows(obj, final_root)[1]),
        }
        unchanged_payload = all(
            after[key] == before[key]
            for key in (
                "mesh_payload_sha256",
                "parent_path",
                "root_relative_surface_sha256",
                "root_relative_position_uv_points_sha256",
            )
        )
        unchanged_transform = matrix_close(after["root_relative_transform"], before["root_relative_transform"])
        if not unchanged_payload or not unchanged_transform:
            changed = [
                key for key in (
                    "mesh_payload_sha256",
                    "parent_path",
                    "root_relative_surface_sha256",
                    "root_relative_position_uv_points_sha256",
                ) if after[key] != before[key]
            ]
            raise RuntimeError(
                f"Excluded source node changed during assembly: {name}; "
                f"payload_fields={changed}; transform_match={unchanged_transform}; "
                f"before_transform={before['root_relative_transform']}; "
                f"after_transform={after['root_relative_transform']}"
            )
        excluded_scene_checks[name] = {
            **before,
            "assembled_root_relative_transform": after["root_relative_transform"],
            "unchanged_in_assembled_scene": True,
        }
    output_meshes = sorted(final_meshes, key=lambda obj: obj.name)
    expected_mesh_data = {
        obj.name: {
            "triangles": len(obj.data.loop_triangles) if obj.data.loop_triangles else len(obj.data.polygons),
            "mesh_payload_sha256": mesh_payload_hash(obj),
            "root_relative_surface_sha256": sha256_json(triangle_surface_rows(obj, final_root)[0]),
        }
        for obj in output_meshes
    }
    for window_id, prefix in WINDOW_PREFIXES.items():
        parts = sorted(obj.name for obj in output_meshes if obj.name.startswith(prefix))
        if not parts:
            raise RuntimeError(f"Studio window contract {window_id} has no source geometry ({prefix})")

    bpy.ops.object.select_all(action="DESELECT")
    export_objects = [final_root, *final_root.children_recursive]
    for obj in export_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = final_root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        use_visible=False,
    )
    if not OUTPUT_GLB.is_file():
        raise RuntimeError("Blender did not write the native candidate GLB")

    output_sha = sha256_file(OUTPUT_GLB)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB))
    imported_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    imported_names = require_unique_mesh_names(imported_meshes, "Reimported native candidate")
    imported_root = bpy.data.objects.get(ROOT_NAME)
    if imported_root is None:
        raise RuntimeError("Native candidate reimport lost its component root")
    if imported_names != expected_final_names:
        raise RuntimeError("Native candidate GLB reimport mesh inventory differs from the assembled source")
    if sum(obj.name == "HeroHouse_StreetDoorClosed_LOD0" for obj in imported_meshes) != 1:
        raise RuntimeError("Native candidate GLB reimport does not contain exactly one closed street door")

    imported_by_name = {obj.name: obj for obj in imported_meshes}
    native_payload_checks = {}
    for name, before in expected_mesh_data.items():
        obj = imported_by_name[name]
        surfaces, points = triangle_surface_rows(obj, imported_root)
        actual_surface_hash = sha256_json(surfaces)
        if actual_surface_hash != before["root_relative_surface_sha256"]:
            raise RuntimeError(f"Export/import changed a mesh position/UV/material surface payload: {name}")
        native_payload_checks[name] = {
            **before,
            "glb_reimport_root_relative_surface_sha256": actual_surface_hash,
            "glb_reimport_position_uv_points_sha256": sha256_json(points),
            "exact_position_uv_material_surface_roundtrip": True,
        }

    native_windows = {}
    for window_id, prefix in WINDOW_PREFIXES.items():
        parts = sorted(name for name in imported_names if name.startswith(prefix))
        if not parts:
            raise RuntimeError(f"Native GLB reimport lost Studio window geometry for {window_id}")
        native_windows[window_id] = parts

    semantic_version = "v3" if V3_MODE else "v2"
    output_record = {
        "status": "native_candidate_source_assembled_for_isolated_preview_only",
        "blender": {"version": bpy.app.version_string, "build_hash": bpy.app.build_hash.decode("ascii", "replace")},
        "inputs": {
            "source_blend": {"path": SOURCE_BLEND.name, "sha256": sha256_file(SOURCE_BLEND), "mesh_nodes": len(source_meshes)},
        "semantic_v3_glb" if V3_MODE else "semantic_v2_glb": {
            "path": SEMANTIC_GLB.name,
            "sha256": sha256_file(SEMANTIC_GLB),
            "mesh_nodes": len(donor_meshes),
        },
        },
        "component": {
            "root_node": ROOT_NAME,
            "fullhero_source_root_transform_blender": source_original_root_matrix,
            "preview_root_translation_removed_for_candidate": component["preview_root_translation_removed_for_glb"],
            "candidate_root_transform_blender": rows(root_matrix_after_preview),
            "source_mesh_nodes": len(source_meshes),
            "suppressed_old_course_nodes_omitted": sorted(hidden_names),
            "suppressed_old_course_count": len(hidden_names),
            "suppressed_old_course_source_inventory": source_hidden_snapshots,
            "excluded_source_nodes_preserved": excluded_scene_checks,
            "excluded_source_node_count": len(excluded_scene_checks),
            f"opaque_semantic_meshes_replaced_by_{semantic_version}": semantic_transfer,
            "opaque_semantic_mesh_count": len(semantic_transfer),
            f"baked_{semantic_version}_wall_crowns_added": crown_transfer,
            f"baked_{semantic_version}_wall_crown_count": len(crown_transfer),
            "street_door_node": "HeroHouse_StreetDoorClosed_LOD0",
            "street_door_count": 1,
            "studio_window_ids": native_windows,
            "studio_window_id_count": len(native_windows),
            "final_mesh_node_count": len(imported_meshes),
            "final_mesh_inventory": sorted(imported_names),
            "position_uv_material_payload_roundtrip": native_payload_checks,
            "geometry_authority": (
                "fullhero.blend excluded geometry + v3 semantic opaque geometry; Meshy donor geometry was never used"
                if V3_MODE else
                "fullhero.blend excluded geometry + v2 semantic opaque geometry; Meshy donor geometry was never used"
            ),
        },
        "output": {
            "path": OUTPUT_GLB.name,
            "sha256": output_sha,
            "bytes": OUTPUT_GLB.stat().st_size,
        },
        "limitations": [
            "This is an isolated native-preview candidate, not a canonical runtime asset or default Studio choice.",
            "No new render was produced; appearance acceptance remains the existing isolated native view.",
            "The canonical/default asset selection was not changed.",
        ],
    }
    if V3_MODE:
        uv_metadata = json.loads((HERE / "opaquehero-for-retexture-v3.metadata.json").read_text(encoding="utf-8"))
        uv_metadata_path = HERE / "opaquehero-for-retexture-v3.metadata.json"
        attachment_metadata_path = HERE / "hero-house-semantic-pbr-v3-attachment.metadata.json"
        output_record["inputs"]["v3_uv_metadata"] = {
            "path": uv_metadata_path.name,
            "sha256": sha256_file(uv_metadata_path),
            "schema": uv_metadata["schema"],
        }
        output_record["inputs"]["v3_pbr_attachment_metadata"] = {
            "path": attachment_metadata_path.name,
            "sha256": sha256_file(attachment_metadata_path),
        }
        OUTPUT_METADATA.write_text(json.dumps({
            "schema": "hero-house-native-preview-candidate/v3",
            "candidate": output_record,
        }, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    else:
        source_manifest[OUTPUT_MANIFEST_KEY] = output_record
        MANIFEST_PATH.write_text(json.dumps(source_manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(("hero-house-native-candidate-v3: " if V3_MODE else "hero-house-native-candidate-v2: ") + json.dumps({
        "status": output_record["status"],
        "glb": output_record["output"],
        "meshes": len(imported_meshes),
        "opaque_replaced": len(semantic_transfer),
        "excluded_preserved": len(excluded_scene_checks),
        "courses_omitted": len(hidden_names),
        "crowns_added": len(crown_transfer),
        "door_count": 1,
        "window_ids": len(native_windows),
        "metadata": OUTPUT_METADATA.name if V3_MODE else MANIFEST_PATH.name,
    }, sort_keys=True))


if __name__ == "__main__":
    main()
