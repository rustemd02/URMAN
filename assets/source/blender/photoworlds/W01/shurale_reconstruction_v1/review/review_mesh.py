import math
import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = os.fspath(Path(__file__).resolve().parents[7])
STUDY = os.path.join(
    ROOT,
    "assets/source/blender/photoworlds/W01/shurale_reconstruction_v1/review",
)
OBJ = os.path.join(
    ROOT,
    "assets/source/blender/photoworlds/W01/shurale_reconstruction_v1/"
    "triposr-output/head_mesh/0/mesh.obj",
)


def clean_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def make_materials():
    color_mat = bpy.data.materials.new("Raw TriPoSR vertex colors")
    color_mat.diffuse_color = (0.35, 0.25, 0.18, 1.0)
    color_mat.use_nodes = True
    nodes = color_mat.node_tree.nodes
    links = color_mat.node_tree.links
    principled = nodes.get("Principled BSDF")
    principled.inputs["Roughness"].default_value = 0.88
    attribute = nodes.new("ShaderNodeVertexColor")
    attribute.layer_name = "Color"
    links.new(attribute.outputs["Color"], principled.inputs["Base Color"])

    clay = bpy.data.materials.new("Neutral clay review override")
    clay.diffuse_color = (0.22, 0.23, 0.24, 1.0)
    clay.use_nodes = True
    clay_principled = clay.node_tree.nodes.get("Principled BSDF")
    clay_principled.inputs["Base Color"].default_value = (0.19, 0.20, 0.21, 1.0)
    clay_principled.inputs["Roughness"].default_value = 0.92
    return color_mat, clay


def point_camera(camera, target):
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


def create_camera(name, location, target, ortho_scale):
    camera_data = bpy.data.cameras.new(name)
    camera = bpy.data.objects.new(name, camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = location
    point_camera(camera, target)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = ortho_scale
    camera_data.lens = 70.0
    return camera


def add_area(name, location, target, watts, size, color=(1.0, 0.96, 0.90)):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = watts
    light_data.shape = "DISK"
    light_data.size = size
    light_data.color = color
    light = bpy.data.objects.new(name, light_data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    point_camera(light, target)
    return light


def setup_render():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1400
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.filepath = ""
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.world.color = (0.12, 0.12, 0.12)
    world = scene.world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.18, 0.19, 0.21, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.20


def position_lights(camera, center, scale):
    rotation = camera.rotation_euler.to_matrix()
    right = rotation @ Vector((1.0, 0.0, 0.0))
    up = rotation @ Vector((0.0, 1.0, 0.0))
    toward_camera = (camera.location - Vector(center)).normalized()
    key, fill, rim = (bpy.data.objects[name] for name in ("Key softbox", "Fill softbox", "Rim softbox"))
    key.location = Vector(center) + toward_camera * (1.8 * scale) - right * (0.52 * scale) + up * (0.58 * scale)
    fill.location = Vector(center) + toward_camera * (1.45 * scale) + right * (0.72 * scale) + up * (0.08 * scale)
    rim.location = Vector(center) - toward_camera * (1.0 * scale) + up * (0.48 * scale)
    for light in (key, fill, rim):
        point_camera(light, center)


def render_cardinals(mesh, color_mat, clay):
    scene = bpy.context.scene
    center = tuple(mesh.location)
    scale = max(mesh.dimensions)
    distance = scale * 3.25
    camera = create_camera("ReviewCamera", (distance, 0.0, 0.0), center, scale * 1.20)
    scene.camera = camera
    add_area("Key softbox", (distance * 0.65, -distance * 0.6, distance * 0.8), center, 55.0, scale * 1.1)
    add_area("Fill softbox", (distance * 0.25, distance * 0.72, distance * 0.15), center, 24.0, scale * 1.35, (0.86, 0.91, 1.0))
    add_area("Rim softbox", (-distance * 0.55, distance * 0.15, distance * 0.55), center, 34.0, scale * 0.95)
    mesh.data.materials.clear()
    mesh.data.materials.append(clay)

    final_views = (
        ("head-neutral-front.png", Vector((1.0, 0.0, 0.0))),
        ("head-neutral-three-quarter.png", Vector((0.839, 0.515, 0.16)).normalized()),
        ("head-neutral-profile.png", Vector((0.0, 1.0, 0.0))),
        ("head-neutral-back.png", Vector((-1.0, 0.0, 0.0))),
    )
    for filename, direction in final_views:
        camera.location = Vector(center) + direction * distance
        point_camera(camera, center)
        position_lights(camera, center, scale)
        scene.render.filepath = os.path.join(STUDY, filename)
        bpy.ops.render.render(write_still=True)

    mesh.data.materials.clear()
    mesh.data.materials.append(color_mat)
    front = Vector((1.0, 0.0, 0.0))
    camera.location = Vector(center) + front * distance
    point_camera(camera, center)
    position_lights(camera, center, scale)
    scene.render.filepath = os.path.join(STUDY, "head-vertex-color-front.png")
    bpy.ops.render.render(write_still=True)

    scene.camera = camera
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(STUDY, "triposr-head-raw-review.blend"))


def main():
    os.makedirs(STUDY, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    clean_scene()
    setup_render()
    before = []
    with open(OBJ, "r", encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("v "):
                before = [float(v) for v in line.split()[1:4]]
                break

    bpy.ops.wm.obj_import(filepath=OBJ, forward_axis="NEGATIVE_Y", up_axis="Z")
    meshes = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one raw OBJ mesh, imported {len(meshes)}")
    mesh = meshes[0]
    mesh.name = "TriPoSR_raw_mesh"
    mesh.data.name = "TriPoSR_raw_mesh_geometry"
    color_mat, clay = make_materials()
    mesh.data.materials.clear()
    mesh.data.materials.append(color_mat)
    mesh.location = (0.0, 0.0, 0.0)
    mesh.rotation_euler = (0.0, 0.0, 0.0)
    mesh.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()

    first = tuple(mesh.data.vertices[0].co)
    print("RAW_FIRST_OBJ_VERTEX", tuple(before))
    print("IMPORTED_FIRST_VERTEX", first)
    print("RAW_OBJECT_DIMENSIONS", tuple(mesh.dimensions))
    print("RAW_COUNTS", len(mesh.data.vertices), len(mesh.data.edges), len(mesh.data.polygons))
    print("VERTEX_COLOR_ATTRIBUTE", [(a.name, a.domain, a.data_type, len(a.data)) for a in mesh.data.attributes if a.name == "Color"])
    if any(abs(a - b) > 1e-6 for a, b in zip(before, first)):
        raise RuntimeError("Importer transformed raw vertex coordinates; preserve source axes before rendering")
    render_cardinals(mesh, color_mat, clay)


if __name__ == "__main__":
    main()
