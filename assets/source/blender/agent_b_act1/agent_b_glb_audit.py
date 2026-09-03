"""GLB audit: import every Agent B kit into a blank Blender scene and
verify scale/bounds/counts, absence of cameras, lights, collision nodes.
Prints one AGENTB_AUDIT line per mesh."""

from __future__ import annotations

import json
import os
import sys

import bpy

KIT_DIR = os.environ["AGENTB_GLB_DIR"]
KITS = [
    "agentb_terrain_road_kit.glb",
    "agentb_village_buildings_kit.glb",
    "agentb_foliage_kit.glb",
    "agentb_zirat_kit.glb",
    "agentb_kara_edge_kit.glb",
]


def clear():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    for cam in list(bpy.data.cameras):
        bpy.data.cameras.remove(cam)
    for light in list(bpy.data.lights):
        bpy.data.lights.remove(light)


def bounds(obj):
    import mathutils
    corners = [obj.matrix_world @ mathutils.Vector(c) for c in obj.bound_box]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    return [min(xs), min(ys), min(zs)], [max(xs), max(ys), max(zs)]


def main():
    report = {"kits": []}
    for kit in KITS:
        clear()
        path = os.path.join(KIT_DIR, kit)
        bpy.ops.import_scene.gltf(filepath=path)
        cameras = [o for o in bpy.data.cameras]
        lights = [o for o in bpy.data.lights]
        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        empties = [o for o in bpy.data.objects if o.type == "EMPTY"]
        total_tris = 0
        material_names = set()
        big = []
        for obj in meshes:
            tris = sum(max(1, len(p.vertices) - 2) for p in obj.data.polygons)
            total_tris += tris
            (mn, mx) = bounds(obj)
            size = [mx[i] - mn[i] for i in range(3)]
            for mat_slot in obj.data.materials:
                if mat_slot and mat_slot.name:
                    material_names.add(mat_slot.name)
            if max(size) > 60:
                big.append(obj.name)
            ranges = {}
            if abs(obj.location.x) > 1e-4 or abs(obj.location.y) > 1e-4 or abs(obj.location.z) > 1e-4:
                ranges["location"] = [round(obj.location.x, 2),
                                      round(obj.location.y, 2),
                                      round(obj.location.z, 2)]
            rot = obj.rotation_euler
            if rot.x or rot.y or rot.z:
                ranges["rotation"] = [round(rot.x, 3), round(rot.y, 3),
                                      round(rot.z, 3)]
            entry = {
                "name": obj.name,
                "tris": tris,
                "bounds_min": [round(v, 2) for v in mn],
                "bounds_max": [round(v, 2) for v in mx],
            }
            entry.update(ranges)
            print("AGENTB_AUDIT_MESH " + json.dumps(entry))
        entry = {
            "kit": kit,
            "root_empties": [e.name for e in empties],
            "meshes": len(meshes),
            "triangles": total_tris,
            "cameras": len(cameras),
            "lights": len(lights),
            "materials": sorted(material_names),
            "oversized": big,
        }
        report["kits"].append(entry)
        print("AGENTB_AUDIT_KIT " + json.dumps(entry))
    print("AGENTB_AUDIT_DONE")


main()
