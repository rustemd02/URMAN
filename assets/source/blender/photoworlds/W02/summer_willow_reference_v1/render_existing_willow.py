"""Render W02's current procedural willow; not a replacement model or game capture."""
from pathlib import Path
import hashlib
import math

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = next(parent for parent in HERE.parents if (parent / "game/project.godot").is_file())
SOURCE = ROOT / "game/scripts/photoworlds/W02Controller.cs"


def blender_point(point):
    x, y, z = point
    return (x, -z, y)


def palette_material(name, color):
    rgb = [int(color[index:index + 2], 16) / 255.0 for index in (0, 2, 4)]
    linear = [value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4 for value in rgb]
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*linear, 1)
    material.use_nodes = True
    shader = material.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (*linear, 1)
    shader.inputs["Roughness"].default_value = .95
    shader.inputs["Specular IOR Level"].default_value = .06
    return material


def primitive(name, vertices, normals, triangles, material):
    mesh = bpy.data.meshes.new(name)
    # Godot primitives use clockwise fronts; Blender uses counter-clockwise.
    mesh.from_pydata([blender_point(point) for point in vertices], [],
                     [(a, c, b) for a, b, c in triangles])
    mesh.materials.append(material)
    for face in mesh.polygons:
        face.use_smooth = True
    mesh.normals_split_custom_set([blender_point(normals[loop.vertex_index]) for loop in mesh.loops])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def angles(segments):
    return [(0.0, 1.0) if index == segments else
            (math.sin(math.tau * index / segments), math.cos(math.tau * index / segments))
            for index in range(segments + 1)]


def grid_triangles(rows, columns):
    result = []
    for row in range(1, rows):
        for column in range(1, columns):
            above = (row - 1) * columns + column
            current = row * columns + column
            result.extend([(above - 1, above, current - 1), (above, current, current - 1)])
    return result


def trunk(material):
    # CylinderMesh: Rings is omitted by W02, retaining Godot's default of 4.
    segments, rings, height, top, bottom = 6, 4, 3.2, .14, .24
    vertices, normals = [], []
    for row in range(rings + 2):
        v = row / (rings + 1)
        radius = top + (bottom - top) * v
        for x, z in angles(segments):
            vertices.append((x * radius, height * .5 - height * v, z * radius))
            normals.append(tuple(Vector((x, (bottom - top) / height, z)).normalized()))
    triangles = grid_triangles(rings + 2, segments + 1)
    for radius, y, normal, flip in [(top, height * .5, (0, 1, 0), False),
                                   (bottom, -height * .5, (0, -1, 0), True)]:
        center = len(vertices)
        vertices.append((0, y, 0))
        normals.append(normal)
        for x, z in angles(segments):
            vertices.append((x * radius, y, z * radius))
            normals.append(normal)
        for index in range(1, segments + 1):
            face = (center, center + index + 1, center + index)
            triangles.append((face[0], face[2], face[1]) if flip else face)
    result = primitive("Trunk", vertices, normals, triangles, material)
    result.location = blender_point((0, 1.6, 0))
    return result


def crown(index, material):
    segments, rings, radius, height = 7, 4, 1.1, 2.2
    vertices, normals = [], []
    for row in range(rings + 2):
        v = row / (rings + 1)
        width = 0.0 if row == rings + 1 else math.sin(math.pi * v)
        y = -1.0 if row == rings + 1 else math.cos(math.pi * v)
        for x, z in angles(segments):
            vertices.append((x * radius * width, y * height * .5, z * radius * width))
            normals.append(tuple(Vector((x * width, y, z * width)).normalized()))
    result = primitive(f"Crown{index}", vertices, normals,
                       grid_triangles(rings + 2, segments + 1), material)
    result.location = blender_point(((index - 1) * .75, 3.1 + (index % 2) * .5, (index - 1) * .4))
    result.scale = (1.15, 1.15, .85)
    return result


def build_willow(name, position):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    parts = [trunk(BARK)] + [crown(index, CENTER if index == 1 else LEAVES) for index in range(3)]
    for part in parts:
        part.parent = root
    root.location = blender_point(position)
    return root, parts


def render(camera, filename, position, target, scale):
    camera.location = position
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = scale
    bpy.context.scene.render.filepath = str(HERE / filename)
    bpy.ops.render.render(write_still=True)


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
BARK = palette_material("PaletteOnly_wood_cut_5b5044", "5b5044")
LEAVES = palette_material("PaletteOnly_leaf_birch_516440", "516440")
CENTER = palette_material("PaletteOnly_leaf_birch_5c7046", "5c7046")
root, parts = build_willow("ExistingW02Willow_LocalOrigin", (0, 0, 0))
scene = bpy.context.scene
world = scene.world or bpy.data.worlds.new("NeutralReferenceWorld")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (.36, .42, .48, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = .65
bpy.ops.object.light_add(type="AREA", location=(-3, -6, 9))
bpy.context.object.data.energy = 900
bpy.context.object.data.size = 6
bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.type = "ORTHO"
scene.camera = camera
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.render.resolution_x = 1200
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.view_settings.view_transform = "AgX"
render(camera, "existing-willow-three-quarter.png", (7, -10, 6), (0, 0, 2.2), 6.1)
render(camera, "existing-willow-front.png", (0, -12, 3.6), (0, 0, 2.2), 5.5)
render(camera, "existing-willow-side.png", (12, 0, 3.6), (0, 0, 2.2), 5.5)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE / "existing-willow-reference.blend"))
for part in parts:
    part.data.calc_loop_triangles()
triangles = sum(len(part.data.loop_triangles) for part in parts)
nondegenerate = sum(1 for part in parts for tri in part.data.loop_triangles
                    if (part.data.vertices[tri.vertices[1]].co - part.data.vertices[tri.vertices[0]].co).cross(
                        part.data.vertices[tri.vertices[2]].co - part.data.vertices[tri.vertices[0]].co).length > 1e-10)
points = [part.matrix_world @ vertex.co for part in parts for vertex in part.data.vertices]
dimensions = [max(point[axis] for point in points) - min(point[axis] for point in points) for axis in range(3)]
(HERE / "source_metrics.txt").write_text(
    f"source={SOURCE.relative_to(ROOT)}\nsource_sha256={hashlib.sha256(SOURCE.read_bytes()).hexdigest()}\n"
    f"parts_per_willow={len(parts)}\nindexed_triangles_per_willow={triangles}\n"
    f"nondegenerate_triangles_per_willow={nondegenerate}\n"
    f"blender_xyz_dimensions_m={dimensions}\nblender_version={bpy.app.version_string}\n"
    "status=Current C# primitive form reference; not a Godot frame or generated candidate.\n")
print(f"W02 willow reference saved: {triangles} indexed / {nondegenerate} nondegenerate triangles")
