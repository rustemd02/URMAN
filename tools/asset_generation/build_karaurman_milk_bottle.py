#!/usr/bin/env python3
"""Build a standalone glass milk bottle prop with its ImageGen wrap label."""

from __future__ import annotations

import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
LABEL_PATH = ROOT / "game/assets/textures/props/milk_bottle/karaurman_milk_bottle_label_v1.png"
MODEL_DIR = ROOT / "game/assets/models/props/milk_bottle"
BLEND_PATH = MODEL_DIR / "karaurman_milk_bottle_v1.blend"
GLB_PATH = MODEL_DIR / "karaurman_milk_bottle_v1.glb"
PREVIEW_PATH = MODEL_DIR / "karaurman_milk_bottle_preview_v1.png"

RADIAL_SEGMENTS = 64
LABEL_RADIUS = 0.0450
LABEL_BOTTOM = 0.0470
LABEL_TOP = 0.1412


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def make_material(name: str, color: tuple[float, float, float, float], *, roughness: float) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Roughness"].default_value = roughness
    return material


def create_lathe(name: str, profile: list[tuple[float, float]], material: bpy.types.Material,
                 *, segments: int = RADIAL_SEGMENTS) -> bpy.types.Object:
    vertices: list[tuple[float, float, float]] = []
    rings: list[list[int]] = []
    for radius, z in profile:
        ring: list[int] = []
        if radius <= 1e-7:
            ring.append(len(vertices))
            vertices.append((0.0, 0.0, z))
        else:
            for i in range(segments):
                angle = 2.0 * math.pi * i / segments
                ring.append(len(vertices))
                vertices.append((radius * math.sin(angle), -radius * math.cos(angle), z))
        rings.append(ring)

    faces: list[tuple[int, ...]] = []
    for lower, upper in zip(rings, rings[1:]):
        if len(lower) == 1:
            center = lower[0]
            for j in range(segments):
                faces.append((center, upper[j], upper[(j + 1) % segments]))
        elif len(upper) == 1:
            center = upper[0]
            for j in range(segments):
                faces.append((lower[j], center, lower[(j + 1) % segments]))
        else:
            for j in range(segments):
                j_next = (j + 1) % segments
                faces.append((lower[j], lower[j_next], upper[j_next], upper[j]))

    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return obj


def create_label_band(image: bpy.types.Image, material: bpy.types.Material) -> bpy.types.Object:
    segment_count = 128
    vertices: list[tuple[float, float, float]] = []
    for i in range(segment_count + 1):
        u = i / segment_count
        angle = -math.pi + 2.0 * math.pi * u
        x = LABEL_RADIUS * math.sin(angle)
        y = -LABEL_RADIUS * math.cos(angle)
        vertices.append((x, y, LABEL_BOTTOM))
        vertices.append((x, y, LABEL_TOP))

    faces: list[tuple[int, int, int, int]] = []
    for i in range(segment_count):
        a = i * 2
        b = a + 2
        faces.append((a, b, b + 1, a + 1))

    mesh = bpy.data.meshes.new("KaraUrmanMilkWrapLabelMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for polygon in mesh.polygons:
        i = polygon.index
        u0, u1 = i / segment_count, (i + 1) / segment_count
        uv_values = ((u0, 0.0), (u1, 0.0), (u1, 1.0), (u0, 1.0))
        for loop_index, uv in zip(polygon.loop_indices, uv_values):
            uv_layer.data[loop_index].uv = uv

    obj = bpy.data.objects.new("PaperWrapLabel", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return obj


def set_transparency(material: bpy.types.Material) -> None:
    if hasattr(material, "surface_render_method"):
        try:
            material.surface_render_method = "BLENDED"
        except TypeError:
            material.surface_render_method = "DITHERED"
    elif hasattr(material, "blend_method"):
        material.blend_method = "BLEND"


def add_camera_and_lighting() -> bpy.types.Object:
    world = bpy.data.worlds.new("MilkBottleWorld") if bpy.data.worlds.get("MilkBottleWorld") is None else bpy.data.worlds["MilkBottleWorld"]
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.72, 0.70, 0.65, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.06

    floor_mat = make_material("WarmNeutralStudio", (0.76, 0.72, 0.64, 1.0), roughness=0.84)
    bpy.ops.mesh.primitive_plane_add(size=200.0, location=(0.0, 0.0, -0.001))
    floor = bpy.context.object
    floor.name = "PreviewGround"
    floor.data.materials.append(floor_mat)

    camera_data = bpy.data.cameras.new("MilkBottleCamera")
    camera = bpy.data.objects.new("MilkBottleCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0.145, -0.56, 0.255)
    target = Vector((0.0, 0.0, 0.132))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 0.335
    bpy.context.scene.camera = camera

    def area(name: str, location: tuple[float, float, float], energy: float, size: float,
             color: tuple[float, float, float]) -> None:
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light_data.color = color
        light = bpy.data.objects.new(name, light_data)
        bpy.context.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (Vector((0.0, 0.0, 0.13)) - light.location).to_track_quat("-Z", "Y").to_euler()

    area("KeySoftbox", (0.25, -0.22, 0.39), 8.0, 0.25, (1.0, 0.93, 0.84))
    area("FrontFill", (-0.28, -0.22, 0.20), 2.0, 0.22, (0.84, 0.91, 1.0))
    area("GlassRim", (-0.08, 0.28, 0.33), 12.0, 0.20, (1.0, 1.0, 1.0))
    return camera


def build() -> None:
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    if not LABEL_PATH.exists():
        raise FileNotFoundError(f"Missing generated label image: {LABEL_PATH}")

    clear_scene()

    glass = make_material("ClearBottleGlass", (0.90, 0.96, 0.94, 1.0), roughness=0.09)
    glass_bsdf = glass.node_tree.nodes.get("Principled BSDF")
    glass_bsdf.inputs["Transmission Weight"].default_value = 1.0
    glass_bsdf.inputs["IOR"].default_value = 1.46
    glass_bsdf.inputs["Coat Weight"].default_value = 0.12
    glass_bsdf.inputs["Coat Roughness"].default_value = 0.06

    bottle_profile = [
        (0.000, 0.004), (0.031, 0.004), (0.039, 0.006), (0.043, 0.011),
        (0.044, 0.018), (0.044, 0.158), (0.043, 0.177), (0.039, 0.194),
        (0.029, 0.209), (0.020, 0.216), (0.020, 0.235), (0.022, 0.237),
        (0.022, 0.242), (0.020, 0.244), (0.018, 0.242), (0.018, 0.235),
        (0.018, 0.217), (0.027, 0.209), (0.037, 0.193), (0.041, 0.177),
        (0.042, 0.158), (0.042, 0.018), (0.038, 0.010), (0.000, 0.009),
    ]
    bottle = create_lathe("ReusableGlassMilkBottle", bottle_profile, glass)

    milk = make_material("FreshWhiteMilk", (0.91, 0.92, 0.86, 1.0), roughness=0.25)
    milk_profile = [
        (0.000, 0.010), (0.035, 0.010), (0.040, 0.015), (0.040, 0.158),
        (0.039, 0.176), (0.035, 0.190), (0.026, 0.202), (0.0175, 0.208),
        (0.000, 0.208),
    ]
    milk_obj = create_lathe("MilkFill", milk_profile, milk)

    label_image = bpy.data.images.load(str(LABEL_PATH), check_existing=True)
    label_image.pack()
    label = make_material("PrintedPaperLabel", (1.0, 1.0, 1.0, 1.0), roughness=0.7)
    label_nodes = label.node_tree.nodes
    label_links = label.node_tree.links
    label_bsdf = label_nodes.get("Principled BSDF")
    image_node = label_nodes.new("ShaderNodeTexImage")
    image_node.name = "ImageGenLabel"
    image_node.image = label_image
    image_node.interpolation = "Linear"
    texcoord = label_nodes.new("ShaderNodeTexCoord")
    texcoord.name = "LabelUV"
    label_links.new(texcoord.outputs["UV"], image_node.inputs["Vector"])
    label_links.new(image_node.outputs["Color"], label_bsdf.inputs["Base Color"])
    label_links.new(image_node.outputs["Color"], label_bsdf.inputs["Emission Color"])
    label_links.new(image_node.outputs["Alpha"], label_bsdf.inputs["Alpha"])
    label_bsdf.inputs["Roughness"].default_value = 0.72
    label_bsdf.inputs["Emission Strength"].default_value = 0.5
    set_transparency(label)
    label_obj = create_label_band(label_image, label)

    cap_mat = make_material("VermilionMetalCap", (0.56, 0.025, 0.018, 1.0), roughness=0.3)
    cap_mat.metallic = 0.55
    cap_bsdf = cap_mat.node_tree.nodes.get("Principled BSDF")
    cap_bsdf.inputs["Metallic"].default_value = 0.55
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=0.0218, depth=0.011,
                                        location=(0.0, 0.0, 0.2495))
    cap = bpy.context.object
    cap.name = "RedCrimpCap"
    cap.data.materials.append(cap_mat)
    bevel = cap.modifiers.new("SoftRolledEdges", "BEVEL")
    bevel.width = 0.001
    bevel.segments = 3
    cap.modifiers.new("WeightedCornerNormals", "WEIGHTED_NORMAL")

    model_objects = [bottle, milk_obj, label_obj, cap]
    add_camera_and_lighting()

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = -0.5
    scene.view_settings.gamma = 1.0

    bpy.ops.object.select_all(action="DESELECT")
    for obj in model_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = bottle
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

    bpy.ops.export_scene.gltf(
        filepath=str(GLB_PATH),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
    )

    bpy.ops.render.render(write_still=True)
    print(f"Saved Blender source: {BLEND_PATH}")
    print(f"Saved glTF model: {GLB_PATH}")
    print(f"Saved preview: {PREVIEW_PATH}")


if __name__ == "__main__":
    build()
