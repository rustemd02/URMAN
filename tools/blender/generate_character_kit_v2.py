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
from mathutils.bvhtree import BVHTree

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
        coat_length="hip", skirt="4a3f44", apron="b6a389", scarf="a2463c", sash="4a3f44"),
    # Reference: short grey hair, a stout silhouette and the navy/red housecoat.
    # Long sleeves, wool leggings and felt boots suit her brief winter outing.
    "Tamara": dict(
        body="Female", height=1.61, shoulder=1.02, arm=1.12, neck=1.14, belly=1.30, hips=1.20, leg=1.04,
        hair="Hair_SimpleParted", hair_color="928a83", head=(1.22, .95, 1.08), cheek_fullness=.22,
        skin_normal=.28, brow_color="716258", hair_lift=.010,
        beard=False, headwear=None, skin="d9b397",
        coat=("252e49", "cloth"), collar=None, trousers="34323a", boots="49413b",
        coat_length="hip", skirt="252e49", sash="a33d39", polka=True),
    "PhoneGuy": dict(
        body="Male", height=1.76, shoulder=.96, arm=.90, neck=.90, belly=1.06, hips=1.0, leg=.93,
        hair="Hair_SimpleParted", hide_hair=True, beard=False, headwear="knit", hat="565f6e", skin="d8b199",
        coat=("4c5566", "cloth"), collar=None, trousers="292e37", boots="24262b",
        coat_length="hip", sash="343c49", boot_cut="ankle"),
    "TimurHazrat": dict(
        body="Male", height=1.78, shoulder=.92, arm=.86, neck=.86, belly=1.04, hips=1.0, leg=.92,
        hair="Hair_Buzzed", beard=True, headwear="karakul", skin="d6b096",
        coat=("3f4a44", "wool"), collar=None, trousers="2a2b29", boots="1f1f1e",
        coat_length="knee", boot_cut="ankle"),
    "Alsu": dict(
        body="Female", height=1.64, shoulder=.92, arm=.88, neck=.88, belly=.98, hips=1.02, leg=.96,
        hair="Hair_Long", beard=False, headwear="knit", skin="e2bca2",
        coat=("6f5448", "cloth"), collar=None, trousers="34404f", boots="3d342e",
        coat_length="thigh", sash="4a3a32"),
    "Rinat": dict(
        body="Male", height=1.76, shoulder=.96, arm=.90, neck=.90, belly=1.06, hips=1.0, leg=.93,
        hair="Hair_SimpleParted", beard=False, headwear="ushanka", hat="3b4250", skin="d8b199",
        coat=("3a4452", "cloth"), collar=None, trousers="2a2e36", boots="1f1f1e",
        coat_length="thigh", sash="23262b"),
    # The neighbour at the woodpile: an older villager in a quilted fufaika,
    # ushanka and grey felt valenki. Anonymous background life, no dialogue.
    "Resident": dict(
        body="Male", height=1.70, shoulder=.94, arm=.88, neck=.90, belly=1.14, hips=1.02, leg=.90,
        hair="Hair_Buzzed", beard=False, headwear="ushanka", hat="4a4038", skin="d4ab90",
        coat=("414840", "cloth"), collar=None, trousers="2f302c", boots="6a655c",
        coat_length="thigh", sash="2a2b27"),
    "Naila": dict(
        body="Female", height=1.66, shoulder=.92, arm=.88, neck=.88, belly=1.0, hips=1.04, leg=.94,
        hair="Hair_Buns", beard=False, headwear=None, skin="e3bfa6",
        coat=("dfe3dc", "cloth"), collar=None, trousers="3b4448", boots="2d2f30",
        coat_length="knee", sweater="728887", sash="c9cec6"),
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
    if mesh is not None:
        world = mesh.matrix_world.copy()
        source_arm = mesh.find_armature()
        # SimpleParted uses the male head bind pose, about 5 cm above the
        # female one. Preserve each attachment's offset when changing rigs.
        source_head = source_arm.matrix_world @ source_arm.data.bones["Head"].matrix_local
        target_head = arm.matrix_world @ arm.data.bones["Head"].matrix_local
        mesh.data.transform(world.inverted() @ target_head @ source_head.inverted() @ world)
    for o in objs:
        if o is not mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    if mesh is None:
        return None
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
    head_width, head_height, head_depth = spec.get("head", (1, 1, 1))
    pb["Head"].scale = (head_width / spec["neck"], head_height, head_depth / spec["neck"])
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
    if name in {"Tamara_Coat_LOD0", "PhoneGuy_Coat_LOD0"}:
        # Replace the ragged bone-ownership boundary by a continuous sleeve
        # section. Solidify then closes its rim; the cuff overlaps the hand 8 mm.
        bm = bmesh.new()
        bm.from_mesh(mesh)
        weights = bm.verts.layers.deform.active
        inverse = obj.matrix_world.inverted()
        for side in ("l", "r"):
            bone = arm.data.bones[f"lowerarm_{side}"]
            axis = (arm.matrix_world.to_3x3() @ (bone.tail_local - bone.head_local)).normalized()
            wrist = arm.matrix_world @ arm.data.bones[f"hand_{side}"].head_local
            cut = wrist - axis * .085
            bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                                  plane_co=inverse @ cut,
                                  plane_no=obj.matrix_world.to_3x3().transposed() @ axis,
                                  dist=.000001, clear_outer=True)
            cuff = [v for v in bm.verts if v.is_boundary and
                    abs((obj.matrix_world @ v.co - cut).dot(axis)) < .00001]
            assert len(cuff) >= 6, f"{name}: missing {side} cuff ring"
            group = obj.vertex_groups[f"lowerarm_{side}"].index
            for v in cuff:
                point = obj.matrix_world @ v.co
                if name == "PhoneGuy_Coat_LOD0":
                    # His hand is wider than the narrowed forearm; allow the
                    # animated wrist to turn inside the cuff without poking out.
                    radial = point - cut - axis * (point - cut).dot(axis)
                    point += radial.normalized() * .008
                v.co = inverse @ (point + axis * .093)
                v[weights].clear()
                v[weights][group] = 1
        bm.to_mesh(mesh)
        bm.free()
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


def coat_hem_level(coat: bpy.types.Object, hip: float, buckets: int = 16) -> float:
    """Lowest height at which the coat still closes all the way round the body."""
    pts = [coat.matrix_world @ v.co for v in coat.data.vertices]
    ring = [p for p in pts if hip - .15 < p.z < hip + .45]
    cx = sum(p.x for p in ring) / len(ring)
    cy = sum(p.y for p in ring) / len(ring)
    lowest = [math.inf] * buckets
    for p in ring:
        # Hanging hands and sleeves are not the coat body.
        if math.hypot(p.x - cx, p.y - cy) > .30:
            continue
        b = int((math.atan2(p.y - cy, p.x - cx) + math.pi) / math.tau * buckets) % buckets
        lowest[b] = min(lowest[b], p.z)
    return max(z for z in lowest if z < math.inf)


def fitted_sash(coat: bpy.types.Object, name: str, z: float, material) -> bpy.types.Object:
    """A narrow belt cut from the coat, retaining its exact deformation weights."""
    obj = coat.copy()
    obj.data = coat.data.copy()
    obj.name = name
    bpy.context.scene.collection.objects.link(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    world = obj.matrix_world
    inverse = world.inverted()
    for height in (z - .025, z + .025):
        bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                              plane_co=inverse @ Vector((0, 0, height)),
                              plane_no=world.to_3x3().transposed() @ Vector((0, 0, 1)),
                              dist=.000001)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if abs((world @ v.co).z - z) > .025001], context="VERTS")
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * .004
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for face in obj.data.polygons:
        face.material_index = 0
    return obj


def sash(body: bpy.types.Object, arm: bpy.types.Object, name: str, z: float, material) -> bpy.types.Object:
    """A cloth sash tied over the coat at the waist, riding the pelvis."""
    pts = [body.matrix_world @ v.co for v in body.data.vertices]
    band = [p for p in pts if abs(p.z - z) < .03]
    cx = sum(p.x for p in band) / len(band)
    cy = sum(p.y for p in band) / len(band)
    rx = max(abs(p.x - cx) for p in band) + .052
    ry = max(abs(p.y - cy) for p in band) + .067
    bm = bmesh.new()
    segments = 28
    rings = []
    for dz in (-.028, .028):
        rings.append([bm.verts.new((cx + math.cos(a) * rx, cy + math.sin(a) * ry, z + dz))
                      for a in (math.tau * i / segments for i in range(segments))])
    for i in range(segments):
        j = (i + 1) % segments
        bm.faces.new((rings[0][i], rings[0][j], rings[1][j], rings[1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=.01)
    return rigid_part(arm, name, bm, material, "pelvis")


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


def knit_hat(body: bpy.types.Object, arm: bpy.types.Object, name: str, color: str) -> bpy.types.Object:
    """A knitted hat pulled over the crown with a folded cuff; hair shows below.
    It follows the skull's own width and depth, not a circle round its longest
    axis, which read as a helmet."""
    centre, radius, brow = head_frame(body)
    world = body.matrix_world
    groups = {g.index: g.name for g in body.vertex_groups}
    head = [world @ v.co for v in body.data.vertices
            if v.groups and groups.get(max(v.groups, key=lambda g: g.weight).group) == "Head"]
    top = max(p.z for p in head)
    upper = [p for p in head if p.z > brow]
    rx = max(abs(p.x - centre.x) for p in upper) + .022
    ry = max(abs(p.y - centre.y) for p in upper) + .022
    bm = bmesh.new()
    crown = bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=1.0)
    for v in crown["verts"]:
        v.co = Vector((centre.x + v.co.x * rx, centre.y + .004 + v.co.y * ry,
                       brow + .012 + max(v.co.z, -.2) * (top + .035 - brow - .012)))
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < brow + .01], context="VERTS")
    ring = []
    for dz in (.0, .042):
        ring.append([bm.verts.new((centre.x + math.cos(t) * (rx + .006), centre.y + .004 + math.sin(t) * (ry + .006),
                                   brow + .008 + dz)) for t in (math.tau * i / 24 for i in range(24))])
    for i in range(24):
        j = (i + 1) % 24
        bm.faces.new((ring[0][i], ring[0][j], ring[1][j], ring[1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.solidify(bm, geom=[f for f in bm.faces], thickness=.008)
    return rigid_part(arm, name, bm, flat_material(color, "knit"), "Head")


def karakul(body: bpy.types.Object, arm: bpy.types.Object, name: str, color: str) -> bpy.types.Object:
    """A low karakul hat: a short cylinder with a softly domed top."""
    centre, radius, brow = head_frame(body)
    bm = bmesh.new()
    wall = bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=radius + .03, radius2=radius + .035, depth=.11)
    for v in wall["verts"]:
        v.co += Vector((centre.x, centre.y + .005, brow + .008))
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


def soften_tamara(body, brows, eyes, hair):
    """Tamara-only lower face, relaxed brow and broad short-hair waves."""
    group=body.vertex_groups['Head'].index
    head=[v for v in body.data.vertices if any(g.group==group and g.weight>.5 for g in v.groups)]
    bottom=min(v.co.z for v in head); top=max(v.co.z for v in head)
    for v in body.data.vertices:
        weight=next((g.weight for g in v.groups if g.group==group),0)
        h=(v.co.z-bottom)/(top-bottom)
        lower=max(0,1-abs(h-.12)/.33)*weight
        front=min(1,max(0,(.07-v.co.y)/.09))
        v.co.x+=.012*math.sin(v.co.x/.144*math.pi)*lower
        v.co.y-=.016*lower*front
    # Relax the superhero's pinched inner brow along with the skin beneath it.
    # Apply one continuous warp to skin, eyebrow strands and eyeballs.
    for mesh in (body,brows,eyes):
        if mesh is None:continue
        for v in mesh.data.vertices:
            x,y,z=v.co
            band=max(0,1-abs(z-(bottom+(top-bottom)*.56))/.045)
            front=min(1,max(0,(.045-y)/.075))
            inside=max(0,1-abs(x)/.11)
            v.co.z+=.009*inside*band*front
    # A little weight below the cheekbone; the eye and eyelid meshes stay put.
    for v in head:
        x,y,z=v.co
        h=(z-bottom)/(top-bottom)
        lower=math.exp(-((abs(x)-.083)/.037)**2-((h-.18)/.14)**2)*min(1,max(0,(.055-y)/.08))
        v.co.z-=.008*lower
        v.co.y-=.003*lower
    surface=BVHTree.FromPolygons([v.co for v in body.data.vertices],
                                [list(p.vertices) for p in body.data.polygons])
    if brows:
        for v in brows.data.vertices:
            # The source object includes lashes below this separate brow band.
            if v.co.z < bottom+(top-bottom)*.55:continue
            old=surface.ray_cast(Vector((v.co.x,-.4,v.co.z)),Vector((0,1,0)),.6)[0]
            along=min(1,max(0,(abs(v.co.x)-.012)/.090))
            v.co.z+=.016*(1-along)+.006*math.sin(math.pi*along)
            raised=surface.ray_cast(Vector((v.co.x,-.4,v.co.z)),Vector((0,1,0)),.6)[0]
            if old is None or raised is None:
                raise ValueError("Tamara eyebrow has no forehead support")
            # Preserve strand depth instead of flattening front/back vertices
            # onto one plane, which causes overlaps and erases the eyebrow.
            v.co.y+=raised.y-old.y
    if hair:
        for v in hair.data.vertices:
            crown=min(1,max(0,(v.co.z-top+.035)/.09))
            v.co.z+=.006*math.sin(v.co.x*26+v.co.y*10)*crown
            v.co.x*=1+.045*crown
        # Sample short waves without shrinking the fitted hairline. A shared
        # vertical displacement keeps coincident strand edges together.
        select_only(hair)
        subdivision=hair.modifiers.new("ShortWaveSurface", "SUBSURF")
        subdivision.subdivision_type="SIMPLE"
        subdivision.levels=2
        bpy.ops.object.modifier_move_to_index(modifier=subdivision.name,index=0)
        bpy.ops.object.modifier_apply(modifier=subdivision.name)
        for v in hair.data.vertices:
            x,y,z=v.co
            crown=min(1,max(0,(z-top+.075)/.08))
            v.co.z+=.004*math.sin(x*115+y*45+1.3*math.sin(y*22))*crown
        # The male source's nape is narrower than this head. Keep its existing
        # rear surface outside the skull instead of adding a second hair cap.
        for v in hair.data.vertices:
            if v.co.y < .06:continue
            point,normal,_,_=surface.find_nearest(v.co)
            if point is not None and normal.dot(v.co-point)<.003:
                v.co=point+normal*.006


def fit_tamara_head(root: Path, arm: bpy.types.Object, body: bpy.types.Object,
                    parts: list[bpy.types.Object]) -> None:
    """Use the fitted CC0 age study with the existing skeleton and clothing."""
    names = [f"Tamara_{part}_LOD0" for part in ("Head", "FaceEyes", "FaceBrows", "Hair")]
    materials = {m.name: m for m in bpy.data.materials}
    for part in list(parts):
        if part.name in names:
            parts.remove(part)
            bpy.data.objects.remove(part, do_unlink=True)
    # The retained body owns the hands; the authored head extends into the collar.
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if abs(v.co.x) < .3 and v.co.z > 1.405],
                     context="VERTS")
    bm.to_mesh(body.data)
    bm.free()
    source = root / "assets/source/blender/characters/tamara_aged_head.blend"
    with bpy.data.libraries.load(str(source), link=False) as (_, loaded):
        loaded.objects = names
    for part in loaded.objects:
        if part is None:
            raise ValueError(f"Incomplete Tamara head source: {source}")
        bpy.context.scene.collection.objects.link(part)
        part.parent = arm
        part.matrix_parent_inverse.identity()
        part.matrix_basis.identity()
        part.modifiers.new("Armature", "ARMATURE").object = arm
        for slot in part.material_slots:
            material = slot.material
            if material and material.name.rsplit(".", 1)[0] in materials:
                slot.material = materials[material.name.rsplit(".", 1)[0]]
        parts.append(part)


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
    # The coat runs well under the skirt's waistband: with only a few
    # centimetres of overlap a walking pelvis opened a gap at the waist.
    coat = garment(body, arm, f"{prefix}_Coat_LOD0", TORSO | ARMS, .028, coat_mat,
                   keep=lambda co: co.z > waist - .13)
    parts.append(coat)
    # The coat is cut where torso bones own the body, so round the hips it
    # ends higher than the waist line. The skirt starts where the coat is
    # still whole all round and a sash covers the seam.
    waist = max(waist, coat_hem_level(coat, hip) + .03)
    if "skirt" in spec:
        # A long village skirt to mid-calf under a hip-length cardigan.
        parts.append(coat_skirt(body, arm, f"{prefix}_Skirt_LOD0", waist, knee - .20,
                                flat_material(spec["skirt"], "cloth"), flare=1.32))
        coat_bottom = knee - .20
    else:
        # A coat below the waist hangs almost straight; only a skirt flares.
        parts.append(coat_skirt(body, arm, f"{prefix}_CoatSkirt_LOD0", waist, coat_bottom,
                                flat_material(*spec["coat"]), flare=1.07))
    if spec.get("polka"):
        for part in parts:
            if "_Coat_" in part.name or "_Skirt_" in part.name:
                tamara_housecoat(part, arm, collar="_Coat_" in part.name)
    sash_material = flat_material(spec.get("sash", "2e2924"), "cloth")
    parts.append(fitted_sash(coat, f"{prefix}_Sash_LOD0", waist, sash_material) if prefix == "Tamara"
                 else sash(body, arm, f"{prefix}_Sash_LOD0", waist, sash_material))
    # Trousers only show below the coat; their hidden upper part bulged
    # through the hem when a relaxed knee came forward.
    trousers = garment(body, arm, f"{prefix}_Trousers_LOD0", LEGS_UPPER | LEGS_LOWER, .014,
                       flat_material(spec["trousers"], "cloth"), keep=lambda co: co.z < coat_bottom + .06)
    parts.append(trousers)
    # Felt boots to below the knee; Timur wears low leather boots he leaves at
    # the mosque door, and the indoor sock is derived from their ankle profile.
    boot_top = .095 if spec.get("boot_cut") == "ankle" else knee - .06
    boot_surface = "leather" if spec.get("boot_cut") == "ankle" else "felt"
    boots = garment(body, arm, f"{prefix}_BootLeft_LOD0", {"calf_l", "foot_l", "ball_l", "ball_leaf_l"}, .022,
                    flat_material(spec["boots"], boot_surface), keep=lambda co: co.z < boot_top)
    boots_r = garment(body, arm, f"{prefix}_BootRight_LOD0", {"calf_r", "foot_r", "ball_r", "ball_leaf_r"}, .022,
                      flat_material(spec["boots"], boot_surface), keep=lambda co: co.z < boot_top)
    parts += [boots, boots_r]
    for boot in (boots, boots_r):
        world = [boot.matrix_world @ v.co for v in boot.data.vertices]
        size = [max(p[i] for p in world) - min(p[i] for p in world) for i in range(3)]
        print(f"character-kit-v2: {boot.name} size x={size[0]:.3f} y={size[1]:.3f} z={size[2]:.3f}")
    if spec.get("collar"):
        neck_base = (arm.matrix_world @ arm.data.bones["neck_01"].head_local).z
        parts.append(fur_collar(body, arm, f"{prefix}_Collar_LOD0", spec["collar"][0], neck_base))
    headwear = spec.get("headwear")
    head_top = top
    brow = top - .085
    if headwear == "ushanka":
        parts.append(ushanka(body, arm, f"{prefix}_Hat_LOD0", spec.get("hat", "6a5846")))
    elif headwear == "knit":
        parts.append(knit_hat(body, arm, f"{prefix}_Hat_LOD0", spec.get("hat", "7d6a3e")))
    elif headwear == "karakul":
        parts.append(karakul(body, arm, f"{prefix}_Hat_LOD0", "4a4640"))
    elif headwear == "scarf":
        parts.append(garment(body, arm, f"{prefix}_Scarf_LOD0", {"Head", "neck_01"}, .016,
                             flat_material(spec["scarf"], "cloth"),
                             keep=lambda co, b=brow: not (co.y < -.045 and b - .15 < co.z < b + .015 and abs(co.x) < .075)))
    # Hide skin under the coat, trousers and boots; hands, neck and face stay.
    # Bone ownership already excludes hands, neck and face. A height cutoff
    # preserved bind-pose elbows, which then pierced raised/bent sleeves.
    keep_skin = None
    if prefix in {"Tamara", "PhoneGuy"}:
        # The open cuff needs skin beneath it when the wrist bends.
        wrists = [arm.matrix_world @ arm.data.bones[f"hand_{side}"].head_local for side in ("l", "r")]
        keep_skin = lambda point: any((point - wrist).length < .12 for wrist in wrists)
    hide_covered_skin(body, TORSO | ARMS | LEGS_UPPER | LEGS_LOWER | FEET, keep=keep_skin)
    return parts



def tamara_housecoat(obj: bpy.types.Object, arm: bpy.types.Object, collar: bool) -> None:
    """Packed, repeatable cloth print; authored UVs survive skinning and LODs."""
    name = "TamaraPolkaDot"
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        shader = mat.node_tree.nodes["Principled BSDF"]
        shader.inputs["Roughness"].default_value = .95
        image = bpy.data.images.new(name, width=128, height=128)
        navy, ivory = G("252e49"), G("e7e3da")
        pixels = []
        for y in range(128):
            for x in range(128):
                # Two staggered rows avoid a rigid checkerboard appearance.
                dx = ((x / 128 - (.25 if y < 64 else .75) + .5) % 1) - .5
                dy = ((y / 64 - .5 + .5) % 1) - .5
                pixels.extend(ivory if dx * dx + (dy / 2) ** 2 < .115 ** 2 else navy)
        image.pixels[:] = pixels
        image.pack()
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = image
        mat.node_tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    obj.data.materials.append(flat_material("a33d39", "cloth"))
    neck = arm.matrix_world @ arm.data.bones["neck_01"].head_local
    if collar:
        # Cut the cloth at the trim edges before assigning red. Whole original
        # triangles turned the narrow fastening into a serrated stripe.
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        inverse = obj.matrix_world.inverted()
        for axis, value in ((0, -.12), (0, -.016), (0, .016), (0, .12),
                            (1, neck.y), (2, neck.z - .018)):
            point, normal = Vector(), Vector()
            point[axis], normal[axis] = value, 1
            bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                                  plane_co=inverse @ point,
                                  plane_no=obj.matrix_world.to_3x3().transposed() @ normal,
                                  dist=.000001)
        bm.to_mesh(obj.data)
        bm.free()
    uv = obj.data.uv_layers.get("UVMap") or obj.data.uv_layers.new(name="UVMap")
    coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
    for face in obj.data.polygons:
        points = [coords[obj.data.loops[i].vertex_index] for i in face.loop_indices]
        centre = sum(points, Vector()) / len(points)
        # Collar and narrow front fastening reproduce the red housecoat trim.
        if collar and centre.y < neck.y and (abs(centre.x) < .016 or
                (centre.z > neck.z - .018 and abs(centre.x) < .12)):
            face.material_index = 1
        angles = [math.atan2(p.x, -p.y) for p in points]
        if max(angles) - min(angles) > math.pi:
            angles = [a + math.tau if a < 0 else a for a in angles]
        for loop, point, angle in zip(face.loop_indices, points, angles):
            uv.data[loop].uv = (angle * .21 / .095, point.z / .095)
    if collar:
        # One torso cylinder collapses the horizontal sleeves into long stripes.
        # Project cloth panels natively, then restore the print's 95 mm repeat.
        select_only(obj)
        obj.data.uv_layers.active = uv
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.01,
                                 area_weight=0, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        uv = obj.data.uv_layers["UVMap"]
        area = 0
        for face in obj.data.polygons:
            points = [uv.data[i].uv for i in face.loop_indices]
            area += abs(sum(p.x*q.y-q.x*p.y for p, q in zip(points, points[1:]+points[:1]))) * .5
        scale = math.sqrt(sum(f.area for f in obj.data.polygons) / area) / .095
        for loop in uv.data:
            loop.uv *= scale


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


# Held arm poses layered over library clips: bone -> armature-space direction
# of the bone and a hint for its Z axis (the character faces -Y, its right is -X).
LAMP_ARM = {"upperarm_l": ((.14, -.22, -1.0), (0, -1, 0)), "lowerarm_l": ((.06, -.95, -.35), (0, 0, 1)),
            "hand_l": ((.04, -.9, -.45), (0, 0, 1))}
STOP_ARM = {"upperarm_r": ((-.12, -.84, -.52), (0, 0, 1)), "lowerarm_r": ((-.14, -.86, .48), (0, 1, 0)),
            "hand_r": ((-.04, -.3, .95), (0, 1, 0))}
HELD_POSES = {"Rinat": {"Idle": LAMP_ARM, "Tension": {**LAMP_ARM, **STOP_ARM}, "Talk": LAMP_ARM}}


def action_curves(action: bpy.types.Action):
    """The F-curve collection that actually drives the action (Blender 4.4+
    keeps it in the first slot's channel bag; older versions on the action)."""
    if getattr(action, "layers", None) and len(action.layers) and len(action.slots):
        strip = action.layers[0].strips[0]
        bag = strip.channelbag(action.slots[0], ensure=True)
        return bag.fcurves
    return action.fcurves


def hold_pose(arm: bpy.types.Object, action: bpy.types.Action, aims: dict) -> None:
    """Replace the listed bones' rotation channels with one held pose."""
    from mathutils import Matrix
    data = arm.animation_data
    previous = (data.action, [t.mute for t in data.nla_tracks])
    for t in data.nla_tracks:
        t.mute = True
    data.action = action
    if getattr(action, "slots", None) and len(action.slots):
        data.action_slot = action.slots[0]
    start, end = (int(f) for f in action.frame_range)
    bpy.context.scene.frame_set(start)
    # Solve the pose with no action attached: an update would otherwise
    # re-apply the clip over every bone just set.
    data.action = None
    held = {}
    # Directions are authored in world axes; the imported armature is rotated.
    to_arm = arm.matrix_world.to_3x3().normalized().inverted()
    for bone, (direction, up) in aims.items():
        pb = arm.pose.bones[bone]
        y = (to_arm @ Vector(direction)).normalized()
        z = to_arm @ Vector(up)
        z = (z - y * z.dot(y)).normalized()
        x = y.cross(z)
        m = Matrix((x, y, z)).transposed().to_4x4()
        m.translation = pb.matrix.translation
        pb.matrix = m
        bpy.context.view_layer.update()
        held[bone] = pb.rotation_quaternion.copy()
    curves = action_curves(action)
    for bone, q in held.items():
        path = f'pose.bones["{bone}"].rotation_quaternion'
        for fc in [fc for fc in curves if fc.data_path == path]:
            curves.remove(fc)
        for i in range(4):
            fc = curves.new(path, index=i)
            fc.keyframe_points.insert(start, q[i])
            fc.keyframe_points.insert(end, q[i])
    data.action = previous[0]
    if previous[0] is not None and len(previous[0].slots):
        data.action_slot = previous[0].slots[0]
    for t, m in zip(data.nla_tracks, previous[1]):
        t.mute = m


CLIPS = {"Idle": "Idle_Loop", "Tension": "Idle_Talking_Loop", "Talk": "Idle_Talking_Loop", "Walk": "Walk_Loop"}


def assign_clips(prefix: str, arm: bpy.types.Object, actions: dict[str, bpy.types.Action]) -> None:
    arm.animation_data_create()
    for suffix, source in CLIPS.items():
        action = actions[source].copy()
        action.name = f"{prefix}_{suffix}"
        if suffix in ("Idle", "Tension", "Talk"):
            relax_action(action)
        if suffix in HELD_POSES.get(prefix, {}):
            hold_pose(arm, action, HELD_POSES[prefix][suffix])
        action.use_fake_user = True
        # Every clip rides its own NLA track: with several armatures in one
        # scene the glTF exporter only discovers clips through tracks.
        if True:
            track = arm.animation_data.nla_tracks.new()
            track.name = action.name
            strip = track.strips.new(action.name, int(action.frame_range[0]), action)
            track.mute = False
    arm.animation_data.action = bpy.data.actions[f"{prefix}_Idle"]


def idle_sole_height(arm: bpy.types.Object, boots: list[bpy.types.Object]) -> float:
    """Lowest boot point in the first Idle frame, with the other clips muted."""
    tracks = list(arm.animation_data.nla_tracks)
    muted = [t.mute for t in tracks]
    for t in tracks:
        t.mute = True
    scene = bpy.context.scene
    frame = scene.frame_current
    idle = arm.animation_data.action
    start, end = (int(f) for f in idle.frame_range)
    samples = []
    for f in range(start, end + 1, max(1, (end - start) // 12)):
        scene.frame_set(f)
        depsgraph = bpy.context.evaluated_depsgraph_get()
        low = math.inf
        for boot in boots:
            evaluated = boot.evaluated_get(depsgraph)
            mesh = evaluated.to_mesh()
            low = min(low, min((evaluated.matrix_world @ v.co).z for v in mesh.vertices))
            evaluated.to_mesh_clear()
        samples.append(low)
    print(f"character-kit-v2: {idle.name} sole over loop {min(samples):.3f}..{max(samples):.3f}")
    lowest = sum(samples) / len(samples)
    for t, m in zip(tracks, muted):
        t.mute = m
    scene.frame_set(frame)
    return lowest


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
            copy.data.validate()
        lod1 += 1
    # The runtime removes a 10 mm sole clearance (GroundSolesOnAnchor), so the
    # anchor sits 10 mm below the soles as they stand in the Idle clip, as in
    # the first kit. A fixed -10 mm left the offset boots about 5 cm under the
    # ground people stand on, which a mosque floor shows plainly.
    boots = [p for p in parts if "_Boot" in p.name and p.name.endswith("_LOD0")]
    soles = {}
    for clip in ("Idle", "Tension"):
        action = bpy.data.actions[f"{prefix}_{clip}"]
        arm.animation_data.action = action
        # Blender 4.4+ plays an action only through one of its slots.
        if getattr(action, "slots", None) and len(action.slots):
            arm.animation_data.action_slot = action.slots[0]
        soles[clip] = idle_sole_height(arm, boots)
    arm.animation_data.action = bpy.data.actions[f"{prefix}_Idle"]
    # Standing clips share one anchor: split the difference so neither the
    # idle nor the talking stance leaves the soles more than a centimetre off.
    sole = (soles["Idle"] + soles["Tension"]) * .5
    print(f"character-kit-v2: {prefix} sole idle={soles['Idle']:.3f} tension={soles['Tension']:.3f}")
    anchor = bpy.data.objects.new(f"{prefix}_Anchor", None)
    bpy.context.scene.collection.objects.link(anchor)
    anchor.location = (arm.location.x, 0, sole - .010)
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
    parser.add_argument("--rest", action="store_true", help="preview in the bind pose, without clips")
    parser.add_argument("--clip", default="", help="preview one clip (Idle, Tension, Talk, Walk) alone")
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
        if "skin_normal" in spec:
            for node in body.data.materials[0].node_tree.nodes:
                if node.type == "NORMAL_MAP":
                    node.inputs["Strength"].default_value = spec["skin_normal"]
        rigged = [body] + [o for o in (eyes, brows) if o]
        if eyes:
            eyes.name = f"{prefix}_FaceEyes_LOD0"
        if brows:
            brows.name = f"{prefix}_FaceBrows_LOD0"
            if spec.get("brow_color"):
                # Tint the existing hair cards; retain their alpha and strands.
                mat = brows.data.materials[0].copy()
                brows.data.materials[0] = mat
                mat.name = f"{prefix}_Brows_Textured"
                bsdf = mat.node_tree.nodes.get("Principled BSDF")
                source = bsdf.inputs["Base Color"].links[0].from_socket
                tint = mat.node_tree.nodes.new("ShaderNodeMix")
                tint.data_type = "RGBA"
                tint.blend_type = "MULTIPLY"
                tint.inputs[0].default_value = 1
                colors = {s.identifier: s for s in tint.inputs}
                colors["B_Color"].default_value = G(spec["brow_color"])
                mat.node_tree.links.new(source, colors["A_Color"])
                mat.node_tree.links.new(tint.outputs["Result"], bsdf.inputs["Base Color"])
        # Under a headscarf the hair is covered, as it is in the village.
        hair = None if spec.get("headwear") == "scarf" or spec.get("hide_hair") else attach_rigged(arm, hair_dir / f"{spec['hair']}.gltf", f"{prefix}_Hair_LOD0",
                             spec.get("hair_color", "d8d4cc" if prefix in ("Mansur",) else "2a2420" if spec["body"] == "Female" else "3a3530"))
        if hair:
            rigged.append(hair)
        if spec.get("beard"):
            beard = attach_rigged(arm, hair_dir / "Hair_Beard.gltf", f"{prefix}_Beard_LOD0",
                                  "cfcac0" if prefix == "Mansur" else "2e2a26")
            if beard:
                rigged.append(beard)
        shape_body(arm, rigged, spec)
        if spec.get("cheek_fullness"):
            head_group = body.vertex_groups["Head"].index
            head = [v for v in body.data.vertices
                    if any(g.group == head_group and g.weight > .5 for g in v.groups)]
            bottom, top = min(v.co.z for v in head), max(v.co.z for v in head)
            # Fill the lower cheeks/jaw without moving the eyes or hairline.
            for vertex in body.data.vertices:
                weight = next((g.weight for g in vertex.groups if g.group == head_group), 0)
                height = (vertex.co.z - bottom) / (top - bottom)
                fullness = max(0, 1 - abs(height - .20) / .35)
                vertex.co.x *= 1 + spec["cheek_fullness"] * fullness * weight
            if hair and spec.get("hair_lift"):
                # Lift the short crown while leaving scalp attachments intact.
                for vertex in hair.data.vertices:
                    crown = min(1, max(0, (vertex.co.z - top + .04) / .10))
                    vertex.co.z += spec["hair_lift"] * crown
        if prefix == "Tamara":
            soften_tamara(body, brows, eyes, hair)
        rigged += dress(prefix, spec, arm, body)
        if prefix == "Tamara":
            fit_tamara_head(Path(args.root), arm, body, rigged)
        assign_clips(prefix, arm, actions)
        arm.location.x = index * 1.2
        lod1 = finish_character(prefix, arm, rigged)
        print(f"character-kit-v2: {prefix} parts={len(rigged)} lod1={lod1}")

    # Keep the age-study albedo at its authored 2048; other skin maps stay at 1024.
    for image in bpy.data.images:
        if image.size[0] > 1024 and image.name != "old_lightskinned_female_diffuse.png":
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
        if args.rest:
            for obj in bpy.data.objects:
                if obj.type == "ARMATURE":
                    obj.data.pose_position = "REST"
        if args.clip:
            for obj in bpy.data.objects:
                if obj.type == "ARMATURE" and obj.animation_data:
                    for t in obj.animation_data.nla_tracks:
                        t.mute = True
                    action = bpy.data.actions.get(obj.name.replace("_Rig", "") + "_" + args.clip)
                    if action:
                        obj.animation_data.action = action
                        if len(action.slots):
                            obj.animation_data.action_slot = action.slots[0]
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
