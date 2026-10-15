"""Produce an original P-FOREST book image and its finite W01 landmark GLB.

Run from the repository root with the project's Blender 4.5 executable:
  Blender --background --python assets/source/blender/photoworlds/W01/pforest_tableau.py

The photograph is an original fictional tableau. P1 informs only painterly
surfaces and broad color masses; the scene itself follows the W01 photo brief.
The runtime GLB contains only shared forest landmarks, never the book figures.
"""

from __future__ import annotations

import hashlib
import json
import math
import subprocess
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[5]
SOURCE_DIR = ROOT / "assets/source/blender/photoworlds/W01"
RUNTIME_DIR = ROOT / "game/assets/photoworlds/forest_entry"
PHOTO_DIR = ROOT / "game/assets/textures/photobook"
SCRIPT = Path(__file__).resolve()
BLEND_OUT = SOURCE_DIR / "pforest-tableau-v1.blend"
GLB_OUT = RUNTIME_DIR / "pforest-tableau-v1.glb"
FRONT_OUT = PHOTO_DIR / "p-forest-front-v1.png"
THUMB_OUT = PHOTO_DIR / "p-forest-front-v1-thumbnail.png"
SCHOOL_OUT = PHOTO_DIR / "s-school-front-v1.png"
SCHOOL_THUMB_OUT = PHOTO_DIR / "s-school-front-v1-thumbnail.png"
MANIFEST_OUT = SOURCE_DIR / "pforest-tableau-v1.manifest.json"

PINE_BLEND = ROOT / "assets/source/blender/urman_winter_pine.blend"
RESIDENT_BLEND = ROOT / "assets/source/blender/urman_character_kit_v2.blend"
PINE_MANIFEST = ROOT / "game/assets/models/act1/urman_winter_pine_v2_manifest.json"
CHAR_LICENSE = ROOT / "assets/source/blender/characters/Quaternius_CC0_License.txt"

W = 1800
H = 1200


def sha(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for part in iter(lambda: f.read(1024 * 1024), b""):
            h.update(part)
    return h.hexdigest()


def g(x: float, y: float, z: float) -> Vector:
    """Godot XYZ to Blender XYZ: (X, -Z, Y)."""
    return Vector((x, -z, y))


def reset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    # Materials are constructed at module load time below; preserve those
    # datablocks while clearing only unused geometry/scene objects.
    for dbs in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
        for db in list(dbs):
            if db.users == 0:
                dbs.remove(db)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = W
    scene.render.resolution_y = H
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.eevee.taa_render_samples = 96
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.15
    scene.view_settings.gamma = 1.0
    scene.render.filepath = str(FRONT_OUT)
    scene.frame_set(18)
    scene.render.fps = 24
    scene.render.image_settings.color_mode = "RGBA"
    bpy.context.preferences.filepaths.save_version = 0
    world = bpy.data.worlds.new("PForest_WinterAir")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.20, 0.31, 0.37, 1.0)
    bg.inputs["Strength"].default_value = 0.55
    scene.world = world


def collection(name: str):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


STAGE = None
WORLD = None
PHOTO = None


def put(obj, coll, runtime=False):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    coll.objects.link(obj)
    obj["pforest_runtime_landmark"] = bool(runtime)
    return obj


def mat(name, color, rough=0.88, metallic=0.0, noise=0.0, shadow=None):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1.0)
    m.use_nodes = True
    nodes = m.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metallic
    m.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if noise:
        texcoord = nodes.new("ShaderNodeTexCoord")
        texcoord.location = (-700, 0)
        tex = nodes.new("ShaderNodeTexNoise")
        tex.location = (-500, 0)
        tex.inputs["Scale"].default_value = noise
        tex.inputs["Detail"].default_value = 2.0
        tex.inputs["Roughness"].default_value = 0.68
        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.location = (-250, 0)
        dark = shadow if shadow else tuple(c * 0.76 for c in color)
        ramp.color_ramp.elements[0].position = 0.24
        ramp.color_ramp.elements[0].color = (*dark, 1)
        ramp.color_ramp.elements[1].position = 0.80
        ramp.color_ramp.elements[1].color = (*color, 1)
        m.node_tree.links.new(texcoord.outputs["Generated"], tex.inputs["Vector"])
        m.node_tree.links.new(tex.outputs["Fac"], ramp.inputs["Fac"])
        m.node_tree.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    return m


SNOW = mat("PForest_Snow_painterly_teal_lit", (0.57, 0.72, 0.73), rough=0.93, noise=3.0, shadow=(0.34, 0.53, 0.59))
SNOW_LIGHT = mat("PForest_Snow_sunlit_cold_cream", (0.79, 0.84, 0.79), rough=0.9, noise=2.2, shadow=(0.59, 0.70, 0.71))
BARK = mat("PForest_Bark_project_original_spruce", (0.22, 0.27, 0.25), rough=0.95)
BIRCH = mat("PForest_Birch_pale_ash", (0.65, 0.68, 0.62), rough=0.9)
BIRCH_MARK = mat("PForest_Birch_dappled_charcoal", (0.13, 0.19, 0.20), rough=0.92)
ROOT_BARK = mat("PForest_Root_amber_bark", (0.34, 0.25, 0.18), rough=0.93)
WOOD = mat("PForest_Crossbar_aged_warm_wood", (0.43, 0.31, 0.22), rough=0.9)
COAT_A = mat("PForest_PhotoCoat_deep_teal", (0.09, 0.24, 0.25), rough=0.94, noise=4.0, shadow=(0.045, 0.12, 0.15))
COAT_B = mat("PForest_PhotoCoat_muted_ochre", (0.43, 0.29, 0.16), rough=0.95, noise=4.0, shadow=(0.23, 0.18, 0.13))
COAT_C = mat("PForest_PhotoCoat_winter_plum", (0.27, 0.22, 0.32), rough=0.94, noise=4.0, shadow=(0.13, 0.14, 0.23))


def cylinder_between(name, a: Vector, b: Vector, radius: float, material, verts=14, radius2=None, coll=None, runtime=True):
    coll = coll or WORLD
    direction = b - a
    center = (a + b) * 0.5
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=radius, radius2=radius if radius2 is None else radius2,
                                    depth=direction.length, end_fill_type="NGON", location=center)
    o = bpy.context.object
    o.name = name
    o.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    o.data.materials.append(material)
    for p in o.data.polygons:
        p.use_smooth = True
    return put(o, coll, runtime)


def sweep(name, points_g, radii, material, coll=None, sides=12, runtime=True):
    coll = coll or WORLD
    points = [g(*p) for p in points_g]
    vertices, faces = [], []
    for i, (center, radius) in enumerate(zip(points, radii)):
        tangent = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        ref = Vector((0, 0, 1)) if abs(tangent.z) < 0.9 else Vector((0, 1, 0))
        u = tangent.cross(ref).normalized()
        v = tangent.cross(u).normalized()
        for j in range(sides):
            angle = math.tau * j / sides
            q = center + radius * (math.cos(angle) * u + math.sin(angle) * v)
            vertices.append(tuple(q))
    for i in range(len(points) - 1):
        for j in range(sides):
            a = i * sides + j
            b = i * sides + (j + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    obj["pforest_runtime_landmark"] = bool(runtime)
    for p in mesh.polygons:
        p.use_smooth = True
    return obj


def branch_trunk(name, x, z, height, radius, material=BARK, lean=0.0):
    pts = [(x, 0.0, z), (x + lean * .20, height * .27, z), (x + lean * .64, height * .72, z), (x + lean, height, z)]
    return sweep(name, pts, [radius, radius * .83, radius * .52, radius * .24], material, sides=18)


def terrain():
    # A gently undulating render-only snow apron. Runtime uses existing W01 ground.
    nx, nz = 34, 38
    verts, faces = [], []
    for iz in range(nz + 1):
        z = 31.0 + 34.0 * iz / nz
        for ix in range(nx + 1):
            x = -22.0 + 44.0 * ix / nx
            y = 0.025 * math.sin(x * .43 + z * .12) + .018 * math.sin(z * .74 - x * .21)
            verts.append(tuple(g(x, y, z)))
    for iz in range(nz):
        for ix in range(nx):
            a = iz * (nx + 1) + ix
            faces.append((a, a + 1, a + nx + 2, a + nx + 1))
    me = bpy.data.meshes.new("PForest_RenderSnow_mesh")
    me.from_pydata(verts, [], faces)
    me.materials.append(SNOW)
    ob = bpy.data.objects.new("PForest_RenderSnow_stage_only", me)
    STAGE.objects.link(ob)
    for f in me.polygons:
        f.use_smooth = True
    return ob


def rounded_snowdrift(name, x, z, sx, sz, height, material=SNOW_LIGHT):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=10, location=g(x, height * .45, z))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sz, height)
    o.data.materials.append(material)
    for p in o.data.polygons:
        p.use_smooth = True
    return put(o, WORLD, True)


def append_pine():
    with bpy.data.libraries.load(str(PINE_BLEND), link=False) as (src, dst):
        dst.objects = ["WinterPine_LOD0"]
    pine = bpy.data.objects.get("WinterPine_LOD0")
    if pine is None:
        raise RuntimeError("WinterPine_LOD0 not available from project source")
    if pine.name not in bpy.context.scene.objects:
        WORLD.objects.link(pine)
    pine.name = "PForest_Pine_Master"
    pine["source"] = "assets/source/blender/urman_winter_pine.blend#WinterPine_LOD0"
    pine["pforest_runtime_landmark"] = True
    return pine


def pine_instances(master):
    # Framing trees make depth planes; the three central trunks stay legible.
    placements = [
        (-8.0, 51.5, 7.8, -0.15), (-6.3, 58.0, 9.5, 0.12),
        (7.4, 53.5, 8.8, -0.20), (9.0, 61.0, 11.0, 0.12),
        (-11.0, 63.0, 10.0, 0.22), (12.0, 67.0, 12.0, -0.14),
        (-4.6, 67.0, 11.0, 0.12), (4.6, 66.0, 10.5, -0.08),
    ]
    for i, (x, z, scale, yaw) in enumerate(placements):
        p = master.copy()
        p.data = master.data
        WORLD.objects.link(p)
        p.name = f"PForest_Pine_Depth_{i+1:02d}"
        p.location = g(x, 0.0, z)
        p.scale = (scale, scale, scale)
        p.rotation_euler.z = yaw
        p["source"] = "assets/source/blender/urman_winter_pine.blend#WinterPine_LOD0"
        p["pforest_runtime_landmark"] = True


def birch_and_bar():
    # Two close, graceful stems split above the ground. Pale bark has a few
    # restrained dark marks rather than a high-contrast zebra pattern.
    left = sweep("PForest_SplitBirch_left_stem", [(4.1,0,45.8),(4.05,.7,45.9),(4.25,2.2,46.1),(4.7,4.6,46.3),(5.0,7.5,46.4)], [.22,.19,.15,.11,.035], BIRCH, sides=16)
    right = sweep("PForest_SplitBirch_right_stem", [(4.1,0,45.8),(3.85,.7,45.9),(3.5,2.0,46.0),(2.9,4.3,46.2),(2.4,7.3,46.4)], [.22,.19,.15,.10,.03], BIRCH, sides=16)
    # low horizontal timber crossbar, local to the photo tableau and kept left
    # of the walk line toward F11.
    cylinder_between("PForest_LowCrossbar", g(4.35,1.15,45.95), g(2.85,1.15,46.0), .075, WOOD, verts=12)
    # Small broken dark flecks on the white bark.
    for i, (x,y,w) in enumerate([(4.34,1.7,.18),(4.23,3.0,.13),(4.69,5.1,.16),(3.74,2.5,.13),(3.17,4.0,.12)]):
        cylinder_between(f"PForest_Birch_bark_mark_{i+1}", g(x,y,45.97), g(x+w,y+.04,46.02), .018, BIRCH_MARK, verts=8)


def central_trunks():
    # Three distinct straight winter trunks, grouped behind the people.
    branch_trunk("PForest_CentralTrunk_left", -2.25, 47.5, 8.2, .31, BARK, .20)
    branch_trunk("PForest_CentralTrunk_middle", .45, 48.0, 9.1, .37, BARK, -.12)
    branch_trunk("PForest_CentralTrunk_right", 2.65, 47.7, 8.0, .29, BARK, -.28)
    # A few high, long boughs, kept airy and irregular; no monster silhouette.
    for i, (x,z,h,dx) in enumerate([
        (-2.3, 53.2, 1.2, -1.8),(-2.1,54.1,1.4,1.4),
        (.4,54.5,1.5,-1.9),(.5,55.1,1.6,1.7),
        (2.5,53.8,1.1,-1.5),(2.7,54.2,1.3,1.3)]):
        sweep(f"PForest_CentralBough_{i+1:02d}",[(x,5.8,z),(x+dx*.46,6.3,z+.1),(x+dx,6.4,z+.2)], [.12,.075,.014], BARK, sides=12)


def modest_root_arch():
    # A compact natural root arch on the right, based low to the snow. It does
    # not touch the F10→F11 approach line and stays below the central canopy.
    pts = [(-4.7,0.00,45.8),(-5.0,.25,45.85),(-5.55,.75,45.9),(-6.0,1.35,45.95),
           (-6.2,1.85,46.0),(-6.0,2.0,46.05),(-5.6,1.85,46.1),(-5.05,1.5,46.15),
           (-4.5,1.05,46.2),(-4.0,.55,46.25)]
    sweep("PForest_RootArch_modest_natural", pts, [.22,.19,.16,.13,.12,.11,.10,.10,.11,.12], ROOT_BARK, sides=16)
    # Subtle toe roots ground both arch ends.
    sweep("PForest_RootArch_toe_left",[(-4.0,.07,46.25),(-3.55,.12,46.3),(-3.1,.08,46.35)], [.11,.075,.018], ROOT_BARK, sides=12)
    sweep("PForest_RootArch_toe_right",[(-5.9,.06,45.8),(-6.35,.08,45.75),(-6.8,.04,45.7)], [.13,.08,.02], ROOT_BARK, sides=12)


def snow_and_depth():
    # Two gentle snow shoulders and distant blue-grey masses reinforce a
    # readable painted winter depth without masking the clear walk corridor.
    rounded_snowdrift("PForest_Snowbank_left", -6.7, 48.5, 2.3, 4.8, .50)
    rounded_snowdrift("PForest_Snowbank_right", 8.5, 48.3, 2.0, 4.4, .42)
    # distant hazy trunks as large soft value masses
    for i, (x,z,h,r) in enumerate([(-15,65,10,.27),(-12,70,12,.24),(-8,72,13,.22),(15,70,12,.28),(18,66,9,.22)]):
        branch_trunk(f"PForest_DistantTrunk_{i+1}",x,z,h,r,mat(f"PForest_Distance_{i+1}",(0.31+i*.009,0.43+i*.009,0.48+i*.006),rough=.97),lean=.15*(-1 if i%2 else 1))


def append_resident():
    names = ["Resident_Rig"] + [f"Resident_{part}_LOD0" for part in (
        "Body", "BootLeft", "BootRight", "CoatSkirt", "Coat", "FaceBrows", "FaceEyes",
        "Hair", "Hat", "Sash", "Trousers")]
    with bpy.data.libraries.load(str(RESIDENT_BLEND), link=False) as (src, dst):
        dst.objects = [n for n in names if n in src.objects]
        dst.actions = ["Resident_Idle"] if "Resident_Idle" in src.actions else []
    objs = [bpy.data.objects.get(n) for n in names if bpy.data.objects.get(n)]
    for o in objs:
        if o.name not in bpy.context.scene.objects:
            PHOTO.objects.link(o)
        o.hide_render = False
        o["source"] = "assets/source/blender/urman_character_kit_v2.blend; Quaternius CC0 base with project-original winter clothing/material variants"
        o["pforest_runtime_landmark"] = False
    rig = bpy.data.objects.get("Resident_Rig")
    if rig is None:
        raise RuntimeError("The project Resident_Rig source was not loaded")
    action = bpy.data.actions.get("Resident_Idle")
    rig.animation_data_create()
    if action:
        rig.animation_data.action = action
    bpy.context.scene.frame_set(18)
    bpy.context.view_layer.update()
    for o in objs:
        if o.type == "MESH" and "LOD1" in o.name:
            o.hide_render = True
    # Hide auxiliary anchor if present in the source library.
    anchor = bpy.data.objects.get("Resident_Anchor")
    if anchor:
        anchor.hide_render = True
    return rig, [o for o in objs if o.type == "MESH" and "LOD0" in o.name]


def recolor_cloth(obj, coat):
    for i, material in enumerate(obj.data.materials):
        if material and material.name.startswith("PForest_PhotoCoat_"):
            obj.data.materials[i] = coat.copy()


def bake_resident(rig, source_meshes):
    """Freeze the evaluated idle skin into a single local-space photo figure."""
    for source in source_meshes:
        if any(part in source.name for part in ("Coat", "Sash", "Trousers")):
            for i in range(len(source.data.materials)):
                source.data.materials[i] = COAT_A
    bpy.context.scene.frame_set(18)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    baked_parts = []
    for source in source_meshes:
        evaluated = source.evaluated_get(depsgraph)
        mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
        mesh.transform(evaluated.matrix_world)
        obj = bpy.data.objects.new("Baked_" + source.name, mesh)
        PHOTO.objects.link(obj)
        obj.matrix_world.identity()
        obj["source"] = source.get("source", "project Resident LOD0")
        obj["pforest_runtime_landmark"] = False
        baked_parts.append(obj)
        source.hide_render = True
        source.hide_viewport = True
    if not baked_parts:
        raise RuntimeError("No evaluated Resident LOD0 meshes were available to bake")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in baked_parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = baked_parts[0]
    bpy.ops.object.join()
    figure = bpy.context.view_layer.objects.active
    figure.name = "PForest_Photo_Figure_1_BakedIdle"
    coords = [v.co for v in figure.data.vertices]
    low = Vector((min(v.x for v in coords), min(v.y for v in coords), min(v.z for v in coords)))
    high = Vector((max(v.x for v in coords), max(v.y for v in coords), max(v.z for v in coords)))
    center_bottom = Vector(((low.x + high.x) * .5, (low.y + high.y) * .5, low.z))
    for v in figure.data.vertices:
        v.co -= center_bottom
    figure.location = (0, 0, 0)
    figure.rotation_euler = (0, 0, 0)
    figure["fictional_anonymous_group"] = True
    figure["skin_pose_baked_from"] = "Resident_Idle frame 18, evaluated armature modifiers before cloning"
    rig.hide_render = True
    rig.hide_viewport = True
    return figure


def people():
    rig, source_meshes = append_resident()
    base = bake_resident(rig, source_meshes)
    # The source faces +Z; F10 looks from -Z toward +Z, so rotate half a turn
    # to present the clothed anonymous adults while each glances slightly aside.
    poses = [
        (1, (1.55,0.0,43.6), math.pi - .12, COAT_A),
        (2, (-0.05,0.0,43.1), math.pi + .08, COAT_B),
        (3, (-1.55,0.0,43.7), math.pi + .19, COAT_C),
    ]
    for idx, (num, pos, yaw, coat) in enumerate(poses):
        figure = base if idx == 0 else base.copy()
        if idx:
            figure.data = base.data.copy()
            PHOTO.objects.link(figure)
        figure.name = f"PForest_Photo_Figure_{num}_BakedIdle"
        figure.location = g(*pos)
        figure.rotation_euler.z = yaw
        figure["fictional_anonymous_group"] = True
        figure["pforest_runtime_landmark"] = False
        figure["cloth_color_variant"] = coat.name
        recolor_cloth(figure, coat)


def setup_camera_lights():
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("PForest_F10_book_camera")
    cam_data.type = "PERSP"
    cam_data.lens = 50
    cam_data.sensor_fit = "VERTICAL"
    cam_data.sensor_height = 36
    cam_data.lens = 36 / (2 * math.tan(math.radians(48) / 2))
    cam = bpy.data.objects.new("PForest_F10_book_camera", cam_data)
    STAGE.objects.link(cam)
    cam.location = g(0, .656942903 + 1.55, 35)
    target = g(.25, 3.8, 46)
    direction = target - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    cam["spec_camera_xyz_godot"] = [0, .656942903+1.55, 35]
    cam["vertical_fov_degrees"] = 48.0
    cam["coordinates"] = "Godot +Z forward; Blender location converted with (X,-Z,Y)"

    def area(name, locg, targetg, color, energy, size):
        data=bpy.data.lights.new(name,"AREA")
        data.energy=energy
        data.shape="DISK"
        data.size=size
        data.color=color
        obj=bpy.data.objects.new(name,data)
        STAGE.objects.link(obj)
        obj.location=g(*locg)
        obj.rotation_euler=(g(*targetg)-obj.location).to_track_quat("-Z","Y").to_euler()
        return obj
    area("PForest_Key_cold_winter_sky",(-6,11,38),(0,1.0,46),(0.56,0.73,0.86),1450,9.5)
    area("PForest_Fill_icy_bounce",(7,5,41),(0,1.2,46),(0.42,0.67,0.72),820,8)
    area("PForest_Warm_thin_wood_reflection",(-3,2.6,37),(0,.8,44),(1.0,.60,.34),110,5)
    scene.render.film_transparent=False
    return cam


def save_and_render():
    scene=bpy.context.scene
    scene.render.filepath=str(FRONT_OUT)
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT))
    bpy.ops.render.render(write_still=True)
    img=bpy.data.images.load(str(FRONT_OUT),check_existing=False)
    img.scale(450,300)
    img.filepath_raw=str(THUMB_OUT)
    img.file_format="PNG"
    img.save()


def derive_school_contact_copy():
    # Same staged capture with only a restrained paper border; no new scene or
    # text. The derived receipt stores the exact P-FOREST parent hash.
    code = 'from PIL import Image,ImageDraw; import sys; im=Image.open(sys.argv[1]).convert("RGB"); w,h=im.size; f=Image.new("RGB",(w,h),(222,218,204)); l,t=36,24; r,b=w-36,h-24; im=im.resize((r-l,b-t),Image.Resampling.LANCZOS); d=ImageDraw.Draw(f); d.rectangle((l-2,t-2,r+1,b+1),fill=(179,180,171)); f.paste(im,(l,t)); f.save(sys.argv[2],format="PNG",optimize=True)'
    subprocess.run(["python3", "-c", code, str(FRONT_OUT), str(SCHOOL_OUT)], check=True)
    thumb = 'from PIL import Image; import sys; Image.open(sys.argv[1]).convert("RGB").resize((450,300),Image.Resampling.LANCZOS).save(sys.argv[2],format="PNG",optimize=True)'
    subprocess.run(["python3", "-c", thumb, str(SCHOOL_OUT), str(SCHOOL_THUMB_OUT)], check=True)


def export_world():
    scene=bpy.context.scene
    runtime=[o for o in list(WORLD.objects) if o.get("pforest_runtime_landmark")]
    print("PFOREST_RUNTIME_OBJECTS", len(runtime), "PINES", [o.name for o in runtime if "Pine" in o.name])
    for o in scene.objects:
        o.select_set(False)
    for o in runtime:
        o.select_set(True)
    print("PFOREST_SELECTED", [(o.name,o.select_get(),o.hide_get()) for o in runtime if "Pine" in o.name])
    bpy.context.view_layer.objects.active=runtime[0]
    GLB_OUT.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(GLB_OUT),export_format="GLB",use_selection=True,
                              export_yup=True,export_apply=True,export_materials="EXPORT",
                              export_image_format="AUTO",export_normals=True,
                              export_texcoords=True,export_cameras=False,export_lights=False)
    for o in runtime:
        o.select_set(False)


def main():
    global STAGE,WORLD,PHOTO
    for path in (SOURCE_DIR,RUNTIME_DIR,PHOTO_DIR): path.mkdir(parents=True,exist_ok=True)
    reset()
    STAGE=collection("PForest_RenderOnly_Stage")
    WORLD=collection("PForest_Runtime_Landmarks")
    PHOTO=collection("PForest_PhotoOnly_FictionalGroup")
    terrain()
    pine=append_pine()
    pine_instances(pine)
    central_trunks()
    birch_and_bar()
    modest_root_arch()
    snow_and_depth()
    people()
    setup_camera_lights()
    scene=bpy.context.scene
    scene.render.resolution_x=W
    scene.render.resolution_y=H
    # Save the original layer stack as an editable Blender source, render once,
    # then export only runtime landmarks from the same authoring scene.
    save_and_render()
    derive_school_contact_copy()
    export_world()
    manifest={
        "asset_id":"P-FOREST",
        "version":"pforest-tableau-v1",
        "status":"original_candidate_produced_not_scene_integrated_or_art_accepted",
        "source_script":"assets/source/blender/photoworlds/W01/pforest_tableau.py",
        "source_script_sha256":sha(SCRIPT),
        "capture_generated_with_script_sha256":sha(SCRIPT),
        "editable_blend":"assets/source/blender/photoworlds/W01/pforest-tableau-v1.blend",
        "editable_blend_sha256":sha(BLEND_OUT),
        "runtime_landmarks_glb":"game/assets/photoworlds/forest_entry/pforest-tableau-v1.glb",
        "runtime_landmarks_glb_sha256":sha(GLB_OUT),
        "photobook_front":"game/assets/textures/photobook/p-forest-front-v1.png",
        "photobook_front_sha256":sha(FRONT_OUT),
        "photobook_thumbnail":"game/assets/textures/photobook/p-forest-front-v1-thumbnail.png",
        "photobook_thumbnail_sha256":sha(THUMB_OUT),
        "school_contact_copy":"game/assets/textures/photobook/s-school-front-v1.png",
        "school_contact_copy_sha256":sha(SCHOOL_OUT),
        "school_contact_copy_parent":"game/assets/textures/photobook/p-forest-front-v1.png",
        "school_contact_copy_parent_sha256":sha(FRONT_OUT),
        "school_contact_copy_method":"Same staged P-FOREST capture, 36px side / 24px top-bottom warm paper border, no text/new render",
        "school_contact_thumbnail":"game/assets/textures/photobook/s-school-front-v1-thumbnail.png",
        "school_contact_thumbnail_sha256":sha(SCHOOL_THUMB_OUT),
        "photo_spec":"docs/URMAN_PHOTOWORLDS_SPEC_2026-10-08/data/photos.spec.json#P-FOREST",
        "reference_scope":{
            "P1":"painterly surface treatment, broad painted value/color masses only; no copied setting, architecture, character, or exact palette",
            "H2-1":"cold winter forest depth property",
            "H2-2":"restrained warm wood in cold atmosphere",
            "W1":"warm/cold winter color relationship"
        },
        "fictional_photo_people":{
            "description":"three anonymous fictional clothed adults; no named or real family identity",
            "base":"assets/source/blender/urman_character_kit_v2.blend#Resident_Rig and LOD0 clothing",
            "source_license":"assets/source/blender/characters/Quaternius_CC0_License.txt (base figures); project-original material variants",
            "photo_only":True
        },
        "composition":"three central trunks; split birch left; low crossbar; modest natural root arch right; photo group looks slightly aside; no visible monster",
        "camera":{"authored_node":"F10","godot_xyz_m":[0,2.206942903,35],"vertical_fov_degrees":48,"resolution_px":[W,H],"forward":"Godot +Z","blender_xyz":"(Godot X,-Godot Z,Godot Y)"},
        "coordinate_mapping":{"Godot_xyz":["X","Y","Z"],"Blender_xyz_before_export":["Godot X","-Godot Z","Godot Y"],"GLTF_Y_up_export_mapping":"(Blender X, Blender Z, -Blender Y)","imported_Godot_xyz":["Godot X","Godot Y","Godot +Z"],"root":"world origin at F00"},
        "walk_clearance_note":"Root arch placed on the right of the photographed group; exact F10-to-F11 route clearance still requires current scene integration/runtime inspection.",
        "runtime_glb_scope":"only authored trunk/bough, split birch/crossbar, root-arch and snowbank meshes; excludes the photo-only group, camera, lights, render-only snow stage and winter-pine source meshes",
        "source_tree":{"file":"assets/source/blender/urman_winter_pine.blend#WinterPine_LOD0","upstream_manifest":"game/assets/models/act1/urman_winter_pine_v2_manifest.json","upstream_manifest_sha256":sha(PINE_MANIFEST),"source_blend_sha256":sha(PINE_BLEND),"used_in_photo_render":True,"included_in_runtime_glb":False,"limitation":"Nine selected pine objects were absent from the generated GLB node list; cause unresolved."},
        "not_verified":"Godot import, scene integration, collision, player walk/camera, photo-book handoff, licensed final-package review, human art acceptance, missing pine meshes in generated runtime GLB"
    }
    MANIFEST_OUT.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print("P-FOREST candidate outputs written")
    for p in (BLEND_OUT,FRONT_OUT,THUMB_OUT,SCHOOL_OUT,SCHOOL_THUMB_OUT,GLB_OUT,MANIFEST_OUT): print(p.relative_to(ROOT),p.stat().st_size,sha(p))


if __name__ == "__main__":
    main()
