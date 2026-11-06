"""Render review-only front, side and back views of the raw Meshy GLB."""

from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[6]
ASSET_DIR = ROOT / "assets/source/blender/photoworlds/W01/photographed_people_v1"
INPUT = ASSET_DIR / "bluecoat-human-meshy-raw-v1.glb"
VIEWS = {
    "front": Vector((0.0, -1.0, 0.0)),
    "side": Vector((1.0, 0.0, 0.0)),
    "rear": Vector((0.0, 1.0, 0.0)),
}


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(INPUT))
    scene = bpy.context.scene
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("Raw GLB contains no mesh objects")

    corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    minimum = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
    maximum = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
    center = (minimum + maximum) * 0.5
    height = maximum.z - minimum.z
    if height <= 0:
        raise RuntimeError("Raw GLB has no positive Z-up height")

    triangle_count = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangle_count += len(obj.data.loop_triangles)

    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world = bpy.data.worlds.new("RawMeshyReviewWorld")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.22, 0.22, 0.22, 1.0)
    background.inputs["Strength"].default_value = 0.8

    target = Vector((center.x, center.y, minimum.z + height * 0.52))
    camera_data = bpy.data.cameras.new("RawMeshyReviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(height * 1.22, max(maximum.x - minimum.x, maximum.y - minimum.y) * 1.25)
    camera = bpy.data.objects.new("RawMeshyReviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    def add_area(name, offset, energy, size):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = target + Vector(offset)
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()

    add_area("ReviewKey", (-3.0, -4.0, 4.0), 900.0, 4.0)
    add_area("ReviewFill", (3.0, -2.0, 2.0), 420.0, 3.0)
    add_area("ReviewRim", (0.0, 3.0, 3.0), 650.0, 3.0)

    for view_name, direction in VIEWS.items():
        camera.location = target + direction * (height * 3.0)
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(ASSET_DIR / f"bluecoat-human-meshy-raw-{view_name}.png")
        bpy.ops.render.render(write_still=True)

    print("RAW_MESHY_OBJECTS", len(meshes))
    print("RAW_MESHY_TRIANGLES", triangle_count)
    print("RAW_MESHY_AABB_Z_UP_M", minimum[:], maximum[:])
    print("RAW_MESHY_HEIGHT_M", height)
    print("RAW_MESHY_VIEWS", sorted(VIEWS))


if __name__ == "__main__":
    main()
