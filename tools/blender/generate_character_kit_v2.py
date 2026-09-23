"""Generate URMAN's human character kit (v2) from CC0 Quaternius bodies.

Run with Blender 4.5 LTS:
  blender --background --python tools/blender/generate_character_kit_v2.py -- \
      --root <repo> --ubc <Universal Base Characters[Standard]> --ual <Universal Animation Library[Standard]> \
      [--only Mansur] [--preview <dir>]

Bodies, hair and the 65-bone humanoid skeleton come from Quaternius Universal
Base Characters; Idle/Talk/Walk motion from the Universal Animation Library
(both CC0 1.0). Proportions, winter clothing, headwear, colours and the clip
selection are URMAN's own. Clothing is cut from each body's own surface and
offset outward, so every garment carries the body's skin weights and deforms
with it; body skin hidden under clothing is removed to prevent poke-through.

Runtime contract (GeneratedCharacterKitDressing): per prefix, meshes
<Prefix>_<Part>_LOD0/_LOD1, an empty <Prefix>_Anchor at the feet and clips
<Prefix>_Idle and <Prefix>_Tension (plus <Prefix>_Talk and <Prefix>_Walk).
Material names are "<hex>__<surface>" except the textured skin/eyes/hair.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

# Build: height (m), shoulder (clavicle length), arm girth, neck girth, belly, hips
PEOPLE = {
    "Mansur": dict(
        body="Male", height=1.72, shoulder=.90, arm=.84, neck=.84, belly=1.12, hips=1.0, leg=.90,
        hair="Hair_Buzzed", beard=True, headwear="ushanka", skin="d9b39a",
        # The contrasting fur collar still lands on the chest; it returns once
        # the ring is fitted to the neck (see fur_collar).
        coat=("5e4638", "sheepskin"), collar=None, trousers="34332f", boots="3f3a35",
        coat_length="thigh"),
    "Gulsina": dict(
        body="Female", height=1.60, shoulder=.95, arm=.92, neck=.90, belly=1.10, hips=1.12, leg=.95,
        hair="Hair_Buns", beard=False, headwear="scarf", skin="e0baa0",
        coat=("6b5960", "knit"), collar=None, trousers="3c3538", boots="4a4038",
        coat_length="hip", skirt="4a3f44", apron="b6a389", scarf="a2463c"),
    "TimurHazrat": dict(
        body="Male", height=1.78, shoulder=.92, arm=.86, neck=.86, belly=1.04, hips=1.0, leg=.92,
        hair="Hair_Buzzed", beard=True, headwear="karakul", skin="d6b096",
        coat=("3f4a44", "wool"), collar=("2c2a28", "fur"), trousers="2a2b29", boots="1f1f1e",
        coat_length="knee"),
    "Naila": dict(
        body="Female", height=1.66, shoulder=.92, arm=.88, neck=.88, belly=1.0, hips=1.04, leg=.94,
        hair="Hair_Buns", beard=False, headwear=None, skin="e3bfa6",
        coat=("dfe3dc", "cloth"), collar=None, trousers="3b4448", boots="2d2f30",
        coat_length="knee", sweater="728887"),
}


def G(color: str) -> tuple[float, float, float, float]:
    return tuple(int(color[i:i + 2], 16) / 255 for i in (0, 2, 4)) + (1.0,)


_materials: dict[str, bpy.types.Material] = {}


def flat_material(color: str, surface: str) -> bpy.types.Material:
    name = f"{color}__{surface}"
    if name not in _materials:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = G(color)
        mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85
        _materials[name] = mat
    return _materials[name]


def select_only(obj: bpy.types.Object) -> None:
    for other in bpy.context.selected_objects:
        other.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def import_gltf(path: Path) -> list[bpy.types.Object]:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    return [o for o in bpy.data.objects if o not in before]


def dominant_bone(obj: bpy.types.Object, vertex) -> str:
    best, weight = "", 0.0
    for g in vertex.groups:
        if g.weight > weight:
            best, weight = obj.vertex_groups[g.group].name, g.weight
    return best


def body_parts(arm: bpy.types.Object, body: bpy.types.Object, prefix: str) -> None:
    """Rename the imported body and remove non-character helpers."""
    body.name = f"{prefix}_Body_LOD0"
    body.data.name = body.name
    arm.name = f"{prefix}_Rig"
    arm.data.name = arm.name


def set_skin(body: bpy.types.Object, texture: Path, tint: str) -> None:
    """The Light albedo carries face and hand detail; a gentle tint sets tone."""
    mat = body.data.materials[0].copy()
    body.data.materials[0] = mat
    mat.name = f"{tint}__skin_textured"
    image = bpy.data.images.load(str(texture), check_existing=True)
    for node in mat.node_tree.nodes:
        if node.type == "TEX_IMAGE" and node.image and not any(k in node.image.name for k in ("Normal", "Rough")):
            node.image = image
    mat.diffuse_color = G(tint)  # Workbench preview colour


def attach_rigged(arm: bpy.types.Object, source: Path, name: str, color: str | None) -> bpy.types.Object | None:
    """Hair/beard/brows rigged to the Head bone: retarget onto this armature."""
    objs = import_gltf(source)
    mesh = next((o for o in objs if o.type == "MESH"), None)
    for o in objs:
        if o is not mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    if mesh is None:
        return None
    world = mesh.matrix_world.copy()
    mesh.parent = arm
    mesh.matrix_world = world
    mod = next((m for m in mesh.modifiers if m.type == "ARMATURE"), None) or mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    mesh.name = name
    mesh.data.name = name
    if color:
        for slot in mesh.material_slots:
            slot.material = flat_material(color, "hair")
    return mesh


def shape_body(arm: bpy.types.Object, meshes: list[bpy.types.Object], spec: dict) -> None:
    """Villager proportions: scale pose bones, bake into meshes, apply as rest."""
    bpy.context.view_layer.objects.active = arm
    select_only(arm)
    bpy.ops.object.mode_set(mode="POSE")
    pb = arm.pose.bones
    for side in ("l", "r"):
        pb[f"clavicle_{side}"].scale = (1, spec["shoulder"], 1)
        pb[f"upperarm_{side}"].scale = (spec["arm"], 1, spec["arm"])
        pb[f"lowerarm_{side}"].scale = (spec["arm"] ** .5, 1, spec["arm"] ** .5)
        pb[f"hand_{side}"].scale = (1 / spec["arm"] ** 1.5, 1, 1 / spec["arm"] ** 1.5)
        pb[f"thigh_{side}"].scale = (spec["leg"], 1, spec["leg"])
        pb[f"calf_{side}"].scale = (1 / spec["leg"] ** .5, 1, 1 / spec["leg"] ** .5)
    pb["neck_01"].scale = (spec["neck"], 1, spec["neck"])
    pb["Head"].scale = (1 / spec["neck"], 1, 1 / spec["neck"])
    pb["spine_01"].scale = (spec["belly"], 1, spec["belly"])
    pb["spine_02"].scale = (1 / spec["belly"] ** .5, 1, 1 / spec["belly"] ** .5)
    pb["pelvis"].scale = (spec["hips"], 1, spec["hips"])
    bpy.ops.object.mode_set(mode="OBJECT")
    for mesh in meshes:
        select_only(mesh)
        for mod in mesh.modifiers:
            if mod.type == "ARMATURE":
                bpy.ops.object.modifier_apply(modifier=mod.name)
    select_only(arm)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for mesh in meshes:
        mod = mesh.modifiers.new("Armature", "ARMATURE")
        mod.object = arm
    # Overall height: scale the whole character about the feet.
    current = max((arm.matrix_world @ b.head_local).z for b in arm.data.bones) + .11
    factor = spec["height"] / current
    arm.scale = (factor, factor, factor)
    select_only(arm)
    for mesh in meshes:
        mesh.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)


def garment(body: bpy.types.Object, arm: bpy.types.Object, name: str, bones: set[str], offset: float,
            material: bpy.types.Material, keep=None, thickness=.006, smooth=25, inflate=None) -> bpy.types.Object:
    """Cut a garment from the body surface: vertices dominated by `bones`
    (optionally filtered by `keep(co)`), pushed out along normals. The copy keeps
    the body's vertex groups, so it deforms exactly with the skeleton."""
    mesh = body.data.copy()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = arm
    obj.matrix_world = body.matrix_world.copy()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    deform = bm.verts.layers.deform.active
    groups = {i: g.name for i, g in enumerate(obj.vertex_groups)}
    remove = []
    world = body.matrix_world
    for v in bm.verts:
        weights = v[deform]
        top = max(weights.items(), key=lambda kv: kv[1])[0] if weights else None
        # Region filters are written in world space (Z up, -Y front); the
        # imported body's own object transform must not change their meaning.
        if groups.get(top) not in bones or (keep is not None and not keep(world @ v.co)):
            remove.append(v)
    bmesh.ops.delete(bm, geom=remove, context="VERTS")
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * offset
    # Cloth is not skin: smoothing erases muscles, toes and the ear/face
    # relief; the open edges (collar, cuffs, hem) stay put so the garment
    # remains attached where it meets the body. Re-inflate what smoothing lost.
    interior = [v for v in bm.verts if not v.is_boundary]
    for _ in range(smooth):
        bmesh.ops.smooth_vert(bm, verts=interior, factor=.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.normal_update()
    for v in interior:
        v.co += v.normal * (offset * .6 if inflate is None else inflate)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.clear()
    mesh.materials.append(material)
    # Smoothed cloth carries no fine relief: half the body's density is plenty.
    dec = obj.modifiers.new("Density", "DECIMATE")
    dec.ratio = .5
    select_only(obj)
    bpy.ops.object.modifier_apply(modifier=dec.name)
    solid = obj.modifiers.new("Thickness", "SOLIDIFY")
    solid.thickness = thickness
    solid.offset = 1
    select_only(obj)
    bpy.ops.object.modifier_apply(modifier=solid.name)
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return obj


def hide_covered_skin(body: bpy.types.Object, covered: set[str], keep=None) -> None:
    """Remove body faces fully under clothing so skin never pokes through."""
    bm = bmesh.new()
    bm.from_mesh(body.data)
    deform = bm.verts.layers.deform.active
    groups = {i: g.name for i, g in enumerate(body.vertex_groups)}

    world = body.matrix_world

    def hidden(v):
        weights = v[deform]
        top = max(weights.items(), key=lambda kv: kv[1])[0] if weights else None
        return groups.get(top) in covered and (keep is None or not keep(world @ v.co))

    faces = [f for f in bm.faces if all(hidden(v) for v in f.verts)]
    bmesh.ops.delete(bm, geom=faces, context="FACES")
    bm.to_mesh(body.data)
    bm.free()


def coat_skirt(body: bpy.types.Object, arm: bpy.types.Object, name: str, waist_z: float, hem_z: float,
               material: bpy.types.Material, flare: float = 1.22, segments: int = 28, rings: int = 5) -> bpy.types.Object:
    """One tube around both legs from waist to hem, so a coat or skirt never
    splits into shorts. Rings blend from pelvis to the nearer thigh, so the hem
    follows the legs a little when walking."""
    pts = [body.matrix_world @ v.co for v in body.data.vertices]
    band = [p for p in pts if abs(p.z - waist_z) < .03]
    cx = sum(p.x for p in band) / len(band)
    cy = sum(p.y for p in band) / len(band)
    rx = max(abs(p.x - cx) for p in band) + .04
    ry = max(abs(p.y - cy) for p in band) + .055
    bm = bmesh.new()
    grid = []
    for r in range(rings + 1):
        t = r / rings
        z = waist_z + (hem_z - waist_z) * t
        scale = 1 + (flare - 1) * t
        ring = []
        for a in (math.tau * i / segments for i in range(segments)):
            # The front (-Y) hangs further out: a coat falls in front of the knees.
            front = max(0.0, -math.sin(a))
            ring.append(bm.verts.new((cx + math.cos(a) * rx * scale,
                                      cy + math.sin(a) * ry * (scale + .7 * t * front), z)))
        grid.append(ring)
    for r in range(rings):
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((grid[r][i], grid[r][j], grid[r + 1][j], grid[r + 1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    mesh.materials.append(material)
    groups = {n: obj.vertex_groups.new(name=n) for n in ("pelvis", "thigh_l", "thigh_r")}
    # Each hem vertex rides the thigh it covers: weight falls off with the
    # distance to that leg's axis and grows toward the hem, so a knee brought
    # forward pushes the coat instead of poking through it.
    legs = {side: (arm.matrix_world @ arm.data.bones[side].head_local) for side in ("thigh_l", "thigh_r")}
    reach = rx * 1.1
    for r in range(rings + 1):
        t = r / rings
        for i in range(segments):
            index = r * segments + i
            co = mesh.vertices[index].co
            weights = {}
            for side, head in legs.items():
                # Which leg the cloth hangs over is a sideways question: the
                # hem in front of or behind a thigh rides that thigh.
                d = abs(co.x - head.x)
                weights[side] = t ** .7 * max(0.0, 1 - d / reach) ** .4
            total = sum(weights.values())
            if total > .95:
                weights = {k: v * .95 / total for k, v in weights.items()}
                total = .95
            groups["pelvis"].add([index], 1 - total, "REPLACE")
            for side, w in weights.items():
                if w > 0:
                    groups[side].add([index], w, "REPLACE")
    solid = obj.modifiers.new("Thickness", "SOLIDIFY")
    solid.thickness = .008
    select_only(obj)
    bpy.ops.object.modifier_apply(modifier=solid.name)
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return obj


def head_frame(body: bpy.types.Object) -> tuple[Vector, float, float]:
    """Centre, radius and brow height of the head from its own vertices."""
    world = body.matrix_world
    groups = {g.index: g.name for g in body.vertex_groups}
    head = [world @ v.co for v in body.data.vertices
            if v.groups and groups.get(max(v.groups, key=lambda g: g.weight).group) == "Head"]
    top = max(p.z for p in head)
    upper = [p for p in head if p.z > top - .12]
    cx = sum(p.x for p in upper) / len(upper)
    cy = sum(p.y for p in upper) / len(upper)
    radius = max(math.hypot(p.x - cx, p.y - cy) for p in upper)
    return Vector((cx, cy, top - .10)), radius, top - .085


def rigid_part(arm: bpy.types.Object, name: str, bm: bmesh.types.BMesh, material, bone: str) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    mesh.materials.append(material)
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(mesh.vertices))), 1.0, "REPLACE")
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    select_only(obj)
    bpy.ops.object.shade_smooth()
    return obj


def ushanka(body: bpy.types.Object, arm: bpy.types.Object, name: str, color: str) -> bpy.types.Object:
    """A fur ushanka: a crown over the skull, a turned-up band round the
    forehead and two ear flaps hanging past the ears."""
    centre, radius, brow = head_frame(body)
    bm = bmesh.new()
    crown = bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=radius + .025)
    for v in crown["verts"]:
        v.co.z *= .9
        v.co += Vector((centre.x, centre.y + .006, centre.z + .045))
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < brow + .02], context="VERTS")
    # The turned-up fur band hugs the crown just above the brow.
    band = bmesh.ops.create_cone(bm, cap_ends=False, segments=24, radius1=radius + .034, radius2=radius + .03, depth=.05)
    for v in band["verts"]:
        v.co += Vector((centre.x, centre.y + .006, brow + .04))
    bmesh.ops.solidify(bm, geom=[f for f in bm.faces], thickness=.012)
    for side in (-1, 1):
        flap = bmesh.ops.create_cube(bm, size=1.0)
        for v in flap["verts"]:
            v.co = Vector((v.co.x * .022, v.co.y * .085, v.co.z * .10))
            v.co += Vector((centre.x + side * (radius + .012), centre.y + .012, brow - .025))
    return rigid_part(arm, name, bm, flat_material(color, "fur"), "Head")


def karakul(body: bpy.types.Object, arm: bpy.types.Object, name: str, color: str) -> bpy.types.Object:
    """A low karakul hat: a short cylinder with a softly domed top."""
    centre, radius, brow = head_frame(body)
    bm = bmesh.new()
    wall = bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=radius + .03, radius2=radius + .035, depth=.11)
    for v in wall["verts"]:
        v.co += Vector((centre.x, centre.y + .005, brow + .065))
    return rigid_part(arm, name, bm, flat_material(color, "fur"), "Head")


def fur_collar(body: bpy.types.Object, arm: bpy.types.Object, name: str, color: str, neck_z: float) -> bpy.types.Object:
    """A fur collar: a thick ring lying round the base of the neck."""
    world = body.matrix_world
    pts = [world @ v.co for v in body.data.vertices]
    band = [p for p in pts if abs(p.z - neck_z) < .02 and abs(p.x) < .1]
    cx = sum(p.x for p in band) / len(band)
    cy = sum(p.y for p in band) / len(band)
    radius = max(math.hypot(p.x - cx, p.y - cy) for p in band)
    bm = bmesh.new()
    ring = bmesh.ops.create_cone(bm, cap_ends=False, segments=24, radius1=radius + .06, radius2=radius + .025, depth=.06)
    for v in ring["verts"]:
        v.co += Vector((cx, cy + .01, neck_z - .015))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=.025)
    return rigid_part(arm, name, bm, flat_material(color, "fur"), "spine_03")


TORSO = {"pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r"}
ARMS = {"upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r"}
LEGS_UPPER = {"thigh_l", "thigh_r"}
LEGS_LOWER = {"calf_l", "calf_r"}
FEET = {"foot_l", "foot_r", "ball_l", "ball_r", "ball_leaf_l", "ball_leaf_r"}


def dress(prefix: str, spec: dict, arm: bpy.types.Object, body: bpy.types.Object) -> list[bpy.types.Object]:
    """Winter clothes cut from the body: coat, trousers, felt boots, collar, headwear."""
    parts = []
    bounds = [body.matrix_world @ Vector(c) for c in body.bound_box]
    top = max(b.z for b in bounds)
    knee = top * .285
    hip = top * .47
    wrist_margin = .0
    coat_mat = flat_material(*spec["coat"])
    coat_bottom = {"hip": hip - .02, "thigh": hip - .20, "knee": knee + .04}[spec["coat_length"]]
    waist = hip + .07
    coat = garment(body, arm, f"{prefix}_Coat_LOD0", TORSO | ARMS, .028, coat_mat,
                   keep=lambda co: co.z > waist - .06)
    parts.append(coat)
    if "skirt" in spec:
        # A long village skirt to mid-calf under a hip-length cardigan.
        parts.append(coat_skirt(body, arm, f"{prefix}_Skirt_LOD0", waist, knee - .20,
                                flat_material(spec["skirt"], "cloth"), flare=1.32))
        coat_bottom = knee - .20
    else:
        # A coat below the waist hangs almost straight; only a skirt flares.
        parts.append(coat_skirt(body, arm, f"{prefix}_CoatSkirt_LOD0", waist, coat_bottom,
                                flat_material(*spec["coat"]), flare=1.07))
    # Trousers only show below the coat; their hidden upper part bulged
    # through the hem when a relaxed knee came forward.
    trousers = garment(body, arm, f"{prefix}_Trousers_LOD0", LEGS_UPPER | LEGS_LOWER, .014,
                       flat_material(spec["trousers"], "cloth"), keep=lambda co: co.z < coat_bottom + .06)
    parts.append(trousers)
    boots = garment(body, arm, f"{prefix}_BootLeft_LOD0", {"calf_l", "foot_l", "ball_l", "ball_leaf_l"}, .022,
                    flat_material(spec["boots"], "felt"), keep=lambda co: co.z < knee - .06)
    boots_r = garment(body, arm, f"{prefix}_BootRight_LOD0", {"calf_r", "foot_r", "ball_r", "ball_leaf_r"}, .022,
                      flat_material(spec["boots"], "felt"), keep=lambda co: co.z < knee - .06)
    parts += [boots, boots_r]
    if spec.get("collar"):
        neck_base = (arm.matrix_world @ arm.data.bones["neck_01"].head_local).z
        parts.append(fur_collar(body, arm, f"{prefix}_Collar_LOD0", spec["collar"][0], neck_base))
    headwear = spec.get("headwear")
    head_top = top
    brow = top - .085
    if headwear == "ushanka":
        parts.append(ushanka(body, arm, f"{prefix}_Hat_LOD0", "6a5846"))
    elif headwear == "karakul":
        parts.append(karakul(body, arm, f"{prefix}_Hat_LOD0", "4a4640"))
    elif headwear == "scarf":
        parts.append(garment(body, arm, f"{prefix}_Scarf_LOD0", {"Head", "neck_01"}, .016,
                             flat_material(spec["scarf"], "cloth"),
                             keep=lambda co, b=brow: not (co.y < -.045 and b - .15 < co.z < b + .015 and abs(co.x) < .075)))
    # Hide skin under the coat, trousers and boots; hands, neck and face stay.
    hide_covered_skin(body, TORSO | ARMS | LEGS_UPPER | LEGS_LOWER | FEET,
                      keep=lambda co, n=top * .80: co.z > n)
    return parts


def load_actions(ual: Path) -> dict[str, bpy.types.Action]:
    """Idle/Talk/Walk clips from the Universal Animation Library (same skeleton)."""
    objs = import_gltf(ual / "Unreal-Godot" / "UAL1_Standard.glb")
    actions = {a.name: a for a in bpy.data.actions}
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    return actions


def relax_action(action: bpy.types.Action) -> None:
    """The library idle stands like an athlete: feet wide, fists closed. Bring
    the thighs a few degrees together and open the fingers halfway, keyframe by
    keyframe, so a villager simply stands."""
    from mathutils import Quaternion
    adduct = {"thigh_l": Quaternion((0, 0, 1), math.radians(5)), "thigh_r": Quaternion((0, 0, 1), math.radians(-5))}
    finger_parts = ("index_", "middle_", "ring_", "pinky_", "thumb_")
    for group in {fc.data_path.split('"')[1] for fc in action.fcurves if fc.data_path.startswith('pose.bones["')}:
        paths = [fc for fc in action.fcurves if fc.data_path == f'pose.bones["{group}"].rotation_quaternion']
        if len(paths) != 4:
            continue
        paths.sort(key=lambda fc: fc.array_index)
        count = len(paths[0].keyframe_points)
        if any(len(fc.keyframe_points) != count for fc in paths):
            continue
        for k in range(count):
            q = Quaternion([fc.keyframe_points[k].co[1] for fc in paths])
            if group in adduct:
                q = adduct[group] @ q
            elif group.startswith(finger_parts):
                q = Quaternion().slerp(q, .45)
            else:
                continue
            for i, fc in enumerate(paths):
                fc.keyframe_points[k].co[1] = q[i]
                fc.keyframe_points[k].handle_left[1] = q[i]
                fc.keyframe_points[k].handle_right[1] = q[i]


CLIPS = {"Idle": "Idle_Loop", "Tension": "Idle_Talking_Loop", "Talk": "Idle_Talking_Loop", "Walk": "Walk_Loop"}


def assign_clips(prefix: str, arm: bpy.types.Object, actions: dict[str, bpy.types.Action]) -> None:
    arm.animation_data_create()
    for suffix, source in CLIPS.items():
        action = actions[source].copy()
        action.name = f"{prefix}_{suffix}"
        if suffix in ("Idle", "Tension", "Talk"):
            relax_action(action)
        action.use_fake_user = True
        # Every clip rides its own NLA track: with several armatures in one
        # scene the glTF exporter only discovers clips through tracks.
        if True:
            track = arm.animation_data.nla_tracks.new()
            track.name = action.name
            strip = track.strips.new(action.name, int(action.frame_range[0]), action)
            track.mute = False
    arm.animation_data.action = bpy.data.actions[f"{prefix}_Idle"]


def finish_character(prefix: str, arm: bpy.types.Object, parts: list[bpy.types.Object]) -> int:
    """LOD1 copies, the ground anchor and export-time cleanup for one character."""
    lod1 = 0
    for part in list(parts):
        copy = part.copy()
        copy.data = part.data.copy()
        bpy.context.scene.collection.objects.link(copy)
        copy.name = part.name.replace("_LOD0", "_LOD1")
        copy.data.name = copy.name
        copy.parent = arm
        copy.matrix_world = part.matrix_world.copy()
        if len(copy.data.polygons) > 400:
            dec = copy.modifiers.new("Decimate", "DECIMATE")
            dec.ratio = .35
            # Keep the armature last so the decimated copy stays skinned.
            select_only(copy)
            bpy.ops.object.modifier_move_to_index(modifier=dec.name, index=0)
            bpy.ops.object.modifier_apply(modifier=dec.name)
        lod1 += 1
    # The runtime removes a 10 mm sole clearance (GroundSolesOnAnchor), so the
    # anchor sits 10 mm below the soles, as in the first kit.
    anchor = bpy.data.objects.new(f"{prefix}_Anchor", None)
    bpy.context.scene.collection.objects.link(anchor)
    anchor.location = (arm.location.x, 0, -.010)
    arm["urman_asset_id"] = "character.act1.human.v2"
    arm["license"] = "CC0 1.0 bodies/hair/motion (Quaternius); project-original clothing, proportions and clip selection"
    arm["collision"] = "none"
    return lod1


def main() -> None:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--ubc", required=True)
    parser.add_argument("--ual", required=True)
    parser.add_argument("--only", default="")
    parser.add_argument("--preview", default="")
    parser.add_argument("--debug-colors", action="store_true")
    parser.add_argument("--export", action="store_true")
    args = parser.parse_args(argv)
    ubc = Path(args.ubc)
    bodies = ubc / "Base Characters" / "Godot - UE"
    textures = ubc / "Base Characters" / "Textures"
    hair_dir = ubc / "Hairstyles" / "Rigged to Head Bone" / "glTF (Godot -Unreal)"

    bpy.ops.wm.read_factory_settings(use_empty=True)
    actions = load_actions(Path(args.ual))
    names = [n for n in PEOPLE if not args.only or n in args.only.split(",")]
    for index, prefix in enumerate(names):
        spec = PEOPLE[prefix]
        objs = import_gltf(bodies / f"Superhero_{spec['body']}_FullBody.gltf")
        arm = next(o for o in objs if o.type == "ARMATURE")
        body = max((o for o in objs if o.type == "MESH" and o.parent == arm and o.name not in ("Eyes", "Eyebrows")
                    and not o.name.startswith(("Eyes", "Eyebrows"))), key=lambda o: len(o.data.polygons))
        for o in objs:
            if o.type == "MESH" and o not in (body,) and o.name not in ("Eyes", "Eyebrows"):
                bpy.data.objects.remove(o, do_unlink=True)
        eyes = next((o for o in bpy.data.objects if o.name.startswith("Eyes") and o.parent == arm), None)
        brows = next((o for o in bpy.data.objects if o.name.startswith("Eyebrows") and o.parent == arm), None)
        body_parts(arm, body, prefix)
        light = textures / ("T_Superhero_Male_Ligh.png" if spec["body"] == "Male" else "T_Superhero_Female_Light_BaseColor.png")
        set_skin(body, light, spec["skin"])
        rigged = [body] + [o for o in (eyes, brows) if o]
        if eyes:
            eyes.name = f"{prefix}_FaceEyes_LOD0"
        if brows:
            brows.name = f"{prefix}_FaceBrows_LOD0"
        # Under a headscarf the hair is covered, as it is in the village.
        hair = None if spec.get("headwear") == "scarf" else attach_rigged(arm, hair_dir / f"{spec['hair']}.gltf", f"{prefix}_Hair_LOD0",
                             "d8d4cc" if prefix in ("Mansur",) else "2a2420" if spec["body"] == "Female" else "3a3530")
        if hair:
            rigged.append(hair)
        if spec.get("beard"):
            beard = attach_rigged(arm, hair_dir / "Hair_Beard.gltf", f"{prefix}_Beard_LOD0",
                                  "cfcac0" if prefix == "Mansur" else "2e2a26")
            if beard:
                rigged.append(beard)
        shape_body(arm, rigged, spec)
        rigged += dress(prefix, spec, arm, body)
        assign_clips(prefix, arm, actions)
        arm.location.x = index * 1.2
        lod1 = finish_character(prefix, arm, rigged)
        print(f"character-kit-v2: {prefix} parts={len(rigged)} lod1={lod1}")

    # Skin albedo at 1024 keeps the kit light; faces stay readable at talk distance.
    for image in bpy.data.images:
        if image.size[0] > 1024:
            image.scale(1024, 1024)
    if args.export:
        root = Path(args.root)
        blend = root / "assets/source/blender/urman_character_kit_v2.blend"
        glb = root / "game/assets/generated/urman_character_kit_v2.glb"
        for image in bpy.data.images:
            if image.size[0] > 0:
                image.pack()
        bpy.context.scene.frame_set(1)
        # Keep only the clips the characters use; the library's other 39 stay out.
        for action in list(bpy.data.actions):
            if not any(action.name.startswith(prefix + "_") for prefix in PEOPLE):
                bpy.data.actions.remove(action)
        bpy.ops.outliner.orphans_purge(do_recursive=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
        bpy.ops.export_scene.gltf(filepath=str(glb), export_format="GLB", export_apply=False,
                                  export_materials="EXPORT", export_yup=True, export_animations=True,
                                  export_animation_mode="ACTIONS", export_nla_strips=False,
                                  export_force_sampling=True, export_extras=True)
        print(f"character-kit-v2: wrote {blend} and {glb}")
    if args.preview:
        bpy.context.scene.frame_set(12)
        render_preview(Path(args.preview), names, debug_colors=args.debug_colors)


DEBUG_COLORS = {"Body": (1, .8, .6), "Coat": (.85, .2, .2), "CoatSkirt": (.2, .8, .2), "Skirt": (.2, .8, .2),
                "Trousers": (.2, .3, .95), "BootLeft": (.95, .9, .1), "BootRight": (.1, .9, .9),
                "Collar": (.95, .5, 0), "Hat": (.6, 0, .6), "Scarf": (.6, 0, .6)}


def render_preview(out: Path, names: list[str], debug_colors: bool = False) -> None:
    """One front and one side frame per character (Workbench, material colour)."""
    sc = bpy.context.scene
    cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.lens = 50
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sc.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "OBJECT" if debug_colors else "MATERIAL"
    if debug_colors:
        for o in bpy.data.objects:
            if o.type == "MESH" and "_" in o.name:
                o.color = (*DEBUG_COLORS.get(o.name.split("_")[1], (.45, .45, .45)), 1)
    sc.render.film_transparent = False
    sc.world = sc.world or bpy.data.worlds.new("World")
    for mat in bpy.data.materials:
        if "__" in mat.name and len(mat.name.split("__")[0]) == 6:
            mat.diffuse_color = G(mat.name.split("__")[0])
    sc.render.resolution_x = 480
    sc.render.resolution_y = 800
    out.mkdir(parents=True, exist_ok=True)
    for index, prefix in enumerate(names):
        x = index * 1.2
        for view, loc, rot in (("front", (x, -3.6, .95), (89, 0, 0)), ("side", (x + 3.6, 0, .95), (89, 0, 90))):
            cam.location = loc
            cam.rotation_euler = tuple(math.radians(a) for a in rot)
            sc.render.filepath = str(out / f"{prefix}_{view}.png")
            bpy.ops.render.render(write_still=True)
    print("character-kit-v2: preview", out)


if __name__ == "__main__":
    main()
