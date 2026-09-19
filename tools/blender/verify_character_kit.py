"""Verify the generated character kit's provenance and LOD contract."""

import bpy
import math
from mathutils import Vector
from mathutils.bvhtree import BVHTree


scene = bpy.context.scene
prefixes = [item for item in str(scene.get("character_prefixes", "")).split(",") if item]
if len(prefixes) != 9 or len(set(prefixes)) != len(prefixes):
    raise RuntimeError(f"Expected 9 character prefixes, got {prefixes!r}")

lod0 = [obj for obj in scene.objects if obj.type == "MESH" and obj.name.endswith("_LOD0")]
lod1 = [obj for obj in scene.objects if obj.type == "MESH" and obj.name.endswith("_LOD1")]
mesh_objects = [obj for obj in scene.objects if obj.type == "MESH"]
malformed_meshes = [
    obj.name
    for obj in mesh_objects
    if not (obj.name.endswith("_LOD0") or obj.name.endswith("_LOD1"))
]
if malformed_meshes:
    raise RuntimeError(f"Mesh names must end in _LOD0 or _LOD1: {malformed_meshes}")
unexpected_prefix_meshes = [
    obj.name
    for obj in mesh_objects
    if not any(obj.name.startswith(f"{prefix}_") for prefix in prefixes)
]
if unexpected_prefix_meshes:
    raise RuntimeError(f"Mesh names must belong to a published character prefix: {unexpected_prefix_meshes}")
expected = scene.get("lod1_mesh_count")
expected_lod0 = scene.get("lod0_mesh_count")
if expected != len(lod1) or expected_lod0 != len(lod0):
    raise RuntimeError(f"Expected matching LOD0/LOD1 meshes, got lod0={len(lod0)}, lod1={len(lod1)}, scene={expected!r}")
if expected != expected_lod0 or not isinstance(expected, int) or expected < 49:
    raise RuntimeError(f"Expected matching character LOD counts >=49, got lod0={expected_lod0!r}, lod1={expected!r}")

lod0_by_name = {obj.name: obj for obj in lod0}
lod1_by_name = {obj.name: obj for obj in lod1}
if len(lod0_by_name) != len(lod0) or len(lod1_by_name) != len(lod1):
    raise RuntimeError("LOD mesh names must be unique")
missing_pairs = [
    source.name
    for source in lod0
    if f"{source.name[:-5]}_LOD1" not in lod1_by_name
]
if missing_pairs:
    raise RuntimeError(f"LOD0 meshes missing matching LOD1 pairs: {missing_pairs}")
missing_sources = [
    obj.name
    for obj in lod1
    if obj.get("lod_source") not in lod0_by_name or obj.name != f"{obj.get('lod_source', '')[:-5]}_LOD1"
]
if missing_sources:
    raise RuntimeError(f"LOD1 meshes have invalid lod_source pairing: {missing_sources}")

collision_meshes = [obj.name for obj in mesh_objects if obj.get("collision") != "none"]
if collision_meshes:
    raise RuntimeError(f"Character kit must not publish collision meshes: {collision_meshes}")

degenerate_triangles = []
for obj in mesh_objects:
    obj.data.calc_loop_triangles()
    vertices = obj.data.vertices
    for triangle in obj.data.loop_triangles:
        a, b, c = (vertices[index].co for index in triangle.vertices)
        if (b - a).cross(c - a).length <= 1.0e-10:
            degenerate_triangles.append(f"{obj.name}[{triangle.index}]")
if degenerate_triangles:
    raise RuntimeError(f"Degenerate mesh triangles: {degenerate_triangles[:12]}")

missing_anchor = [
    prefix
    for prefix in prefixes
    if (
        scene.objects.get(f"{prefix}_Anchor") is None
        or scene.objects[f"{prefix}_Anchor"].type != "EMPTY"
        or scene.objects[f"{prefix}_Anchor"].get("anchor_policy") != "ground-origin"
        or abs(scene.objects[f"{prefix}_Anchor"].location.z) > 1.0e-6
    )
]
if missing_anchor:
    raise RuntimeError(f"Character anchors missing or invalid: {missing_anchor}")

missing_detail = [
    prefix
    for prefix in prefixes
    if not any(obj.name.startswith(f"{prefix}_Face") and obj.name.endswith("_LOD0") for obj in lod0)
]
if missing_detail:
    raise RuntimeError(f"Character face landmarks missing: {missing_detail}")

missing_human_detail = [
    prefix
    for prefix in prefixes
    if any(
        scene.objects.get(f"{prefix}_{name}") is None
        for name in ("Head_LOD0", "HeadHandLeft_LOD0", "HeadHandRight_LOD0", "ShoulderCuffLeft_LOD0", "ShoulderCuffRight_LOD0")
    )
]
if missing_human_detail:
    raise RuntimeError(f"Character silhouette detail missing: {missing_human_detail}")

def rest_vertices(obj):
    return [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


def extent(points, axis):
    return min(point[axis] for point in points), max(point[axis] for point in points)


# Inventory and correct skin weights alone did not catch facial pieces authored
# at world zero. Check actual surfaces in each head's frame after rigging, for
# every family and both LODs, without relying on the generator's anchor labels.
bpy.context.view_layer.update()
face_names = ("FaceEyeWhiteL", "FaceEyeWhiteR", "FaceEyeIrisL", "FaceEyeIrisR",
              "FaceNoseBridge", "FaceNoseTip", "FaceMouthLine", "EarL", "EarR")
face_piece_count = 0
for prefix in prefixes:
    rig = scene.objects.get(f"{prefix}_Rig")
    lod_centres = {}
    for level in (0, 1):
        head_obj = scene.objects[f"{prefix}_Head_LOD{level}"]
        head_inverse = head_obj.matrix_world.inverted()
        head_points = [vertex.co for vertex in head_obj.data.vertices]
        bounds = [extent(head_points, axis) for axis in range(3)]
        head_height = bounds[2][1] - bounds[2][0]
        centres = {}
        for name in face_names:
            obj = scene.objects.get(f"{prefix}_{name}_LOD{level}")
            if obj is None:
                raise RuntimeError(f"{prefix}: missing facial piece {name} at LOD{level}")
            skins = [modifier for modifier in obj.modifiers if modifier.type == "ARMATURE"]
            if (obj.parent is not rig or obj.parent_type != "OBJECT"
                    or len(skins) != 1 or skins[0].object is not rig
                    or [group.name for group in obj.vertex_groups] != ["Head"]):
                raise RuntimeError(f"{obj.name}: facial piece must follow its own rig's Head bone")
            points = [head_inverse @ point for point in rest_vertices(obj)]
            piece_bounds = [extent(points, axis) for axis in range(3)]
            # The mouth/eyes/ears occupy the face around the template origin,
            # above the neck and below the crown. Modest front/side protrusions
            # are expected for the nose and ears; displacement to a neighbour's
            # head or to the feet must fail even with otherwise valid skinning.
            for axis in (0, 1):
                if (piece_bounds[axis][0] < bounds[axis][0] - 0.04
                        or piece_bounds[axis][1] > bounds[axis][1] + 0.04):
                    raise RuntimeError(f"{obj.name}: facial vertices leave their own head envelope on axis {axis}")
            if (piece_bounds[2][0] < -head_height * 0.25
                    or piece_bounds[2][1] > head_height * 0.25):
                raise RuntimeError(f"{obj.name}: facial vertices are outside the face height band")
            centre = Vector(tuple((low + high) * 0.5 for low, high in piece_bounds))
            if not name.startswith("Ear") and centre.y >= 0.0:
                raise RuntimeError(f"{obj.name}: facial feature is behind the face")
            centres[name] = centre
            face_piece_count += 1
        for side, sign in (("L", -1.0), ("R", 1.0)):
            eye = centres[f"FaceEyeWhite{side}"]
            iris = centres[f"FaceEyeIris{side}"]
            ear = centres[f"Ear{side}"]
            if not 0.0 < sign * eye.x < sign * ear.x:
                raise RuntimeError(f"{prefix}/LOD{level}: eye and ear are not on their own side of the head")
            if iris.y >= eye.y or abs(iris.x - eye.x) > 0.01:
                raise RuntimeError(f"{prefix}/LOD{level}: iris is detached from its eye")
            if not eye.z > centres["FaceNoseTip"].z > centres["FaceMouthLine"].z:
                raise RuntimeError(f"{prefix}/LOD{level}: eyes, nose and mouth have invalid vertical order")
        lod_centres[level] = centres
    if any((lod_centres[0][name] - lod_centres[1][name]).length > 0.012 for name in face_names):
        raise RuntimeError(f"{prefix}: a facial piece moves by more than 12mm between LODs")
    print(f"character-face: {prefix} 18 pieces in own head frame; Head binding; both LODs")


# Measure actual surfaces, not the generator's profile labels. The previous
# collar sat inside the neck and the two-ring boots looked like blocks even
# though the mesh/rig/LOD inventory passed all contract checks above.
for prefix in prefixes:
    head = rest_vertices(scene.objects[f"{prefix}_Head_LOD0"])
    body = rest_vertices(scene.objects[f"{prefix}_Body_LOD0"])
    collar_obj = scene.objects[f"{prefix}_ScarfBand_LOD0"]
    collar = rest_vertices(collar_obj)
    head_bottom, head_top = extent(head, 2)
    collar_bottom, collar_top = extent(collar, 2)
    if collar_bottom >= extent(body, 2)[1] or collar_top <= head_bottom + 0.025:
        raise RuntimeError(f"{prefix}: collar does not meet the torso and cover the lower neck")
    if collar_top >= head_top - 0.18:
        raise RuntimeError(f"{prefix}: collar rises into the face")
    neck = [point for point in head if collar_bottom <= point.z <= collar_top]
    for axis in (0, 1):
        low, high = extent(neck, axis)
        outer_low, outer_high = extent(collar, axis)
        if outer_low > low - 0.003 or outer_high < high + 0.003:
            raise RuntimeError(f"{prefix}: collar is still buried inside the neck on axis {axis}")
    # A bounding box can pass while a neck bend intersects the inner wall.
    # From each covered vertex, both lateral rays must first meet an inward
    # cloth face. A vertex within the cloth, outside it or against the rim fails.
    collar_surface = BVHTree.FromPolygons(
        collar, [list(polygon.vertices) for polygon in collar_obj.data.polygons])
    for point in neck:
        if not collar_bottom + 0.0001 < point.z < collar_top - 0.0001:
            continue
        for direction in (Vector((-1.0, 0.0, 0.0)), Vector((1.0, 0.0, 0.0))):
            hit, normal, face_index, distance = collar_surface.ray_cast(point, direction, 1.0)
            if face_index is None or normal.dot(direction) >= -0.1 or distance < 0.003:
                raise RuntimeError(f"{prefix}: neck intersects or exits the collar at {tuple(point)}")

    for side in ("Left", "Right"):
        boot_obj = scene.objects[f"{prefix}_Boot{side}_LOD0"]
        boot = rest_vertices(boot_obj)
        trouser = rest_vertices(scene.objects[f"{prefix}_Trouser{side}_LOD0"])
        x0, x1 = extent(boot, 0)
        y0, y1 = extent(boot, 1)
        z0, z1 = extent(boot, 2)
        width, length = x1 - x0, y1 - y0
        if not 0.42 < width / length < 0.65:
            raise RuntimeError(f"{prefix}/{side}: boot reads as a broad block ({width:.3f} x {length:.3f}m)")
        if abs(z0 - 0.010) > 0.0001:
            raise RuntimeError(f"{prefix}/{side}: boot moved the established sole plane to {z0:.4f}m")
        sole = [point for point in boot if abs(point.z - z0) < 0.0001]
        if len(sole) < 4 or extent(sole, 0)[1] - extent(sole, 0)[0] < width * 0.90:
            raise RuntimeError(f"{prefix}/{side}: boot lost its broad, flat bearing sole")
        toe = [point for point in boot if point.y < y0 + length * 0.25]
        if z1 - max(point.z for point in toe) < 0.035 or z1 <= extent(trouser, 2)[0] + 0.020:
            raise RuntimeError(f"{prefix}/{side}: the toe/instep does not rise into the trouser ankle")
    print(f"character-fit: {prefix} collar={collar_top - collar_bottom:.3f}m "
          f"boot={width:.3f}x{length:.3f}m sole={z0:.3f}m")


for level in (0, 1):
    # The old detached cardigan strip passed whole-character bounds. Shoot
    # through both real surfaces at several heights to measure its fit.
    body_obj = scene.objects[f"Naila_Body_LOD{level}"]
    placket_obj = scene.objects[f"Naila_CardiganPlacket_LOD{level}"]
    body = rest_vertices(body_obj)
    placket = rest_vertices(placket_obj)
    low, high = extent(placket, 2)
    body_low, body_high = extent(body, 2)
    if low < body_low - 0.004 or high > body_high + 0.004:
        raise RuntimeError(f"Naila/LOD{level}: cardigan placket extends past its real torso")
    surfaces = [BVHTree.FromPolygons(rest_vertices(obj), [list(face.vertices) for face in obj.data.polygons])
                for obj in (body_obj, placket_obj)]
    gaps = []
    for fraction in (0.05, 0.2, 0.4, 0.6, 0.8, 0.95):
        origin = Vector((scene.objects["Naila_Anchor"].location.x,
                         min(extent(body, 1)[0], extent(placket, 1)[0]) - 0.10,
                         low + (high - low) * fraction))
        hits = [surface.ray_cast(origin, Vector((0, 1, 0)), 1.0) for surface in surfaces]
        if any(hit[2] is None for hit in hits):
            raise RuntimeError(f"Naila/LOD{level}: placket or supporting torso is missing at {fraction}")
        gap = hits[0][0].y - hits[1][0].y
        if not -0.005 <= gap <= 0.018:
            raise RuntimeError(f"Naila/LOD{level}: cardigan placket is detached/buried by {gap:.4f}m")
        gaps.append(gap)
    if [group.name for group in placket_obj.vertex_groups] != ["Spine"]:
        raise RuntimeError(f"Naila/LOD{level}: cardigan must move with the torso")
    print(f"character-clothing-fit: Naila/LOD{level} torso/placket surface gaps {min(gaps):.4f}..{max(gaps):.4f}m")

    cap_obj = scene.objects[f"TimurHazrat_Hat_LOD{level}"]
    cap = rest_vertices(cap_obj)
    scalp_objects = [scene.objects[f"TimurHazrat_{part}_LOD{level}"] for part in ("Head", "Hair")]
    cap_surface = BVHTree.FromPolygons(cap, [list(face.vertices) for face in cap_obj.data.polygons])
    scalp_surfaces = [BVHTree.FromPolygons(rest_vertices(obj), [list(face.vertices) for face in obj.data.polygons])
                      for obj in scalp_objects]
    origin = Vector((sum(extent(cap, 0)) * 0.5, sum(extent(cap, 1)) * 0.5, extent(cap, 2)[0] + 0.003))
    gaps = []
    for index in range(8):
        angle = 0.123 + index * math.tau / 8
        direction = Vector((math.cos(angle), math.sin(angle), 0))
        cap_hit = cap_surface.ray_cast(origin, direction, 1.0)
        scalp_hits = [surface.ray_cast(origin, direction, 1.0) for surface in scalp_surfaces]
        scalp_distances = [hit[3] for hit in scalp_hits if hit[2] is not None]
        if cap_hit[2] is None or not scalp_distances:
            raise RuntimeError(f"TimurHazrat/LOD{level}: cap rim has no real scalp section at {index}")
        gap = cap_hit[3] - max(scalp_distances)
        if not -0.012 <= gap <= 0.025:
            raise RuntimeError(f"TimurHazrat/LOD{level}: cap rim leaves its head/hair by {gap:.4f}m")
        gaps.append(gap)
    if [group.name for group in cap_obj.vertex_groups] != ["Head"]:
        raise RuntimeError(f"TimurHazrat/LOD{level}: cap must move with the head")
    print(f"character-clothing-fit: TimurHazrat/LOD{level} cap/scalp surface gaps {min(gaps):.4f}..{max(gaps):.4f}m")


detail_policy = scene.get("detail_policy")
if not isinstance(detail_policy, str) or "face landmarks" not in detail_policy or "layered clothing" not in detail_policy or "Idle/Tension" not in detail_policy:
    raise RuntimeError(f"Unexpected character detail policy: {detail_policy!r}")

armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
expected_armatures = scene.get("armature_count")
if expected_armatures != len(armatures) or expected_armatures != len(prefixes):
    raise RuntimeError(f"Expected one animation armature per prefix, got scene={expected_armatures!r}, actual={len(armatures)}")

missing_animation = []
for prefix in prefixes:
    rig = scene.objects.get(f"{prefix}_Rig")
    if (
        rig is None
        or rig.type != "ARMATURE"
        or rig.data.get("animation_clips") != "Idle,Tension"
        or rig.animation_data is None
        or rig.animation_data.action is None
        or rig.animation_data.action.name != f"{prefix}_Idle"
    ):
        missing_animation.append(f"{prefix}:rig")
        continue
    actions = {action.name for action in bpy.data.actions if action.name in {f"{prefix}_Idle", f"{prefix}_Tension"}}
    tension_strips = {
        strip.name
        for track in rig.animation_data.nla_tracks
        for strip in track.strips
        if strip.action is not None
    }
    if actions != {f"{prefix}_Idle", f"{prefix}_Tension"} or f"{prefix}_Tension" not in tension_strips:
        missing_animation.append(f"{prefix}:{sorted(actions)}")
    if prefix == "CouncilWitness":
        hand_bone = rig.data.bones.get("Hand.R")
        if hand_bone is None or hand_bone.parent is None or hand_bone.parent.name != "Arm.R":
            missing_animation.append(f"{prefix}:Hand.R must be a child of Arm.R")
        idle_action = bpy.data.actions.get(f"{prefix}_Idle")
        tension_action = bpy.data.actions.get(f"{prefix}_Tension")
        has_idle_hand_keys = idle_action is not None and any(
            'pose.bones["Hand.R"]' in curve.data_path for curve in idle_action.fcurves
        )
        has_tension_hand_keys = tension_action is not None and any(
            'pose.bones["Hand.R"]' in curve.data_path for curve in tension_action.fcurves
        )
        if not has_idle_hand_keys or not has_tension_hand_keys:
            missing_animation.append(f"{prefix}:Hand.R must be keyed in Idle and Tension")
if missing_animation:
    raise RuntimeError(f"Character animation clips missing: {missing_animation}")

# Pieces are bound by a rigid skin, not by bone parenting: bone-parented meshes
# export no skin, which made every imported Idle/Tension track inert in Godot
# (measured before the fix: the clip played and moved nothing). The binding is
# therefore read from the single vertex group at weight 1.0 plus the Armature
# modifier, and the same expected-bone contract is enforced.
skin_bindings = 0
for obj in mesh_objects:
    armature_modifiers = [item for item in obj.modifiers if item.type == "ARMATURE"]
    groups = [group.name for group in obj.vertex_groups]
    if len(armature_modifiers) != 1 or len(groups) != 1:
        raise RuntimeError(
            f"Piece must carry exactly one Armature modifier and one vertex group: "
            f"{obj.name} has {len(armature_modifiers)} modifiers and groups={groups}")
    if obj.parent_type != "OBJECT":
        raise RuntimeError(f"Skinned piece must be object-parented to its rig: {obj.name} is {obj.parent_type}")
    group = obj.vertex_groups[groups[0]]
    weighted = 0
    for vertex in obj.data.vertices:
        for assignment in vertex.groups:
            if assignment.group == group.index:
                if abs(assignment.weight - 1.0) > 1e-3:
                    raise RuntimeError(f"Rigid skin weight must be 1.0: {obj.name} vertex {vertex.index} is {assignment.weight}")
                weighted += 1
    if weighted != len(obj.data.vertices):
        raise RuntimeError(f"Every vertex must be weighted: {obj.name} has {weighted}/{len(obj.data.vertices)}")
    skin_bindings += 1

for obj in mesh_objects:
    for side, bone in (("Left", "Arm.L"), ("Right", "Arm.R")):
        if f"Hand{side}" in obj.name or f"HandThumb{side}" in obj.name:
            witness_right = (
                obj.name.startswith("CouncilWitness_HeadHandRight_")
                or obj.name.startswith("CouncilWitness_HeadHandThumbRight_")
            )
            expected_bone = "Hand.R" if witness_right else bone
            actual_bone = obj.vertex_groups[0].name
            if actual_bone != expected_bone:
                raise RuntimeError(f"Hand must follow {expected_bone}: {obj.name} follows {actual_bone}")

policy = scene.get("collision_policy")
if policy != "no collision meshes; Godot interaction targets and zone colliders own physics":
    raise RuntimeError(f"Unexpected collision policy: {policy!r}")

print(f"character-asset-smoke: {len(prefixes)} prefixes, {len(lod0)} LOD0/{len(lod1)} LOD1 meshes; face/clothing detail; Idle/Tension clips; {skin_bindings} rigid skin bindings; no collision meshes")
