"""Verify the generated modular kit's provenance and LOD1 contract in Blender."""

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


scene = bpy.context.scene
lod1 = [
    obj
    for obj in scene.objects
    if obj.type == "MESH" and "_LOD1" in obj.name
]
expected = scene.get("lod1_mesh_count")
legacy_lod1 = [obj for obj in lod1 if not obj.name.startswith("HouseInterior_")]
house_interior_lod1 = [obj for obj in lod1 if obj.name.startswith("HouseInterior_")]
# Keep the existing 49-mesh environment contract fail-closed while adding the
# published 102-mesh interior family, including both real rear windows.
if expected != len(lod1) or expected != 151 or len(legacy_lod1) != 49 or len(house_interior_lod1) != 102:
    raise RuntimeError(
        "Expected 151 LOD1 meshes (49 legacy + 102 HouseInterior), "
        f"got scene={expected!r}, actual={len(lod1)}, "
        f"legacy={len(legacy_lod1)}, house_interior={len(house_interior_lod1)}"
    )

legacy_family_counts = {
    "HouseA_": 12,
    "FenceA_": 6,
    "RoadDirt_": 1,
    "PineA_": 2,
    "TableA_": 5,
    "OldPc_": 8,
    "WellA_": 7,
    "WoodpileA_": 4,
    "GateA_": 4,
}
for prefix, count in legacy_family_counts.items():
    actual = sum(1 for obj in legacy_lod1 if obj.name.startswith(prefix))
    if actual != count:
        raise RuntimeError(f"Legacy {prefix} LOD1 contract changed: expected={count}, actual={actual}")

house_interior_required = {
    *(f"HouseInterior_FloorBoard_{index:02d}" for index in range(6)),
    "HouseInterior_BaseTrimBack",
    "HouseInterior_BaseTrimFront",
    "HouseInterior_CeilingField",
    "HouseInterior_CeilingBeamLeft",
    "HouseInterior_CeilingBeamRight",
    *("HouseInterior_BackWall_" + part for part in
      ("Left", "Mid", "Right", "UnderSillWest", "UnderSillEast", "HeadWest", "HeadEast")),
    "HouseInterior_LeftWall",
    "HouseInterior_RightWall",
    "HouseInterior_FrontWallLeft",
    "HouseInterior_FrontWallRight",
    "HouseInterior_FrontWallLintel",
    "HouseInterior_EntryDoor",
    "HouseInterior_EntryFrameLeft",
    "HouseInterior_EntryFrameRight",
    "HouseInterior_EntryFrameTop",
    "HouseInterior_EntryThreshold",
    *("HouseInterior_Window" + part + "_" + side
      for side in ("West", "East")
      for part in ("Recess", "Glass", "FrameLeft", "FrameRight", "FrameTop", "FrameBottom", "Muntin", "Sill")),
    "HouseInterior_TableTop",
    "HouseInterior_TableLegLeft",
    "HouseInterior_TableLegRight",
    "HouseInterior_TableLegBackLeft",
    "HouseInterior_TableLegBackRight",
    "HouseInterior_TableApron",
    "HouseInterior_ChairSeat",
    "HouseInterior_ChairBack",
    "HouseInterior_ChairLegLeft",
    "HouseInterior_ChairLegRight",
    "HouseInterior_ChairLegBackLeft",
    "HouseInterior_ChairLegBackRight",
    "HouseInterior_CupboardBody",
    "HouseInterior_CupboardDoor",
    "HouseInterior_RugField",
    "HouseInterior_RugBandA",
    "HouseInterior_RugBandB",
    "HouseInterior_DaybedFrame",
    "HouseInterior_DaybedCushion",
    "HouseInterior_DaybedBack",
    "HouseInterior_StorageChestBody",
    "HouseInterior_StorageChestLid",
    "HouseInterior_StorageChestFront",
    "HouseInterior_RightShelfBoard",
    "HouseInterior_RightShelfBack",
    "HouseInterior_ShelfVesselA",
    "HouseInterior_ShelfVesselB",
    "HouseInterior_ShelfVesselC",
    "HouseInterior_RightRunnerField",
    "HouseInterior_RightRunnerBand",
    "HouseInterior_RightRunnerHem",
    "HouseInterior_HearthBase",
    "HouseInterior_HearthBody",
    "HouseInterior_HearthTop",
    "HouseInterior_HearthDoor",
    "HouseInterior_HearthHandle",
    "HouseInterior_HearthFlue",
    "HouseInterior_HearthFlueCollar",
    "HouseInterior_LeftWallCupboardBody",
    "HouseInterior_LeftWallCupboardDoor",
    "HouseInterior_LeftWallCupboardTop",
    "HouseInterior_TableTray",
    "HouseInterior_TableKettle",
    "HouseInterior_TableKettleLid",
    "HouseInterior_TableBowl",
    "HouseInterior_StorageBasketBody",
    "HouseInterior_StorageBasketRim",
    "HouseInterior_StorageBasketHandle",
    "HouseInterior_OldPcBackboard",
    "HouseInterior_OldPcHutchCleatLeft",
    "HouseInterior_OldPcHutchCleatRight",
    "HouseInterior_OldPcHutchShelf",
    "HouseInterior_OldPcHutchTop",
    "HouseInterior_OldPcHutchFolder",
    "HouseInterior_OldPcDocumentFolio",
    "HouseInterior_LeftWallWainscotField",
    "HouseInterior_LeftWallWainscotRail",
    "HouseInterior_TableFootRail",
}
house_interior_edge_required = {
    "HouseInterior_DaybedFrame",
    "HouseInterior_DaybedCushion",
    "HouseInterior_DaybedBack",
    "HouseInterior_StorageChestBody",
    "HouseInterior_StorageChestLid",
    "HouseInterior_StorageChestFront",
    "HouseInterior_RightShelfBoard",
    "HouseInterior_RightShelfBack",
    "HouseInterior_ShelfVesselA",
    "HouseInterior_ShelfVesselB",
    "HouseInterior_ShelfVesselC",
    "HouseInterior_RightRunnerField",
    "HouseInterior_RightRunnerBand",
    "HouseInterior_RightRunnerHem",
}
house_interior_lived_in_required = {
    "HouseInterior_HearthBase",
    "HouseInterior_HearthBody",
    "HouseInterior_HearthTop",
    "HouseInterior_HearthDoor",
    "HouseInterior_HearthHandle",
    "HouseInterior_HearthFlue",
    "HouseInterior_HearthFlueCollar",
    "HouseInterior_LeftWallCupboardBody",
    "HouseInterior_LeftWallCupboardDoor",
    "HouseInterior_LeftWallCupboardTop",
    "HouseInterior_TableTray",
    "HouseInterior_TableKettle",
    "HouseInterior_TableKettleLid",
    "HouseInterior_TableBowl",
    "HouseInterior_StorageBasketBody",
    "HouseInterior_StorageBasketRim",
    "HouseInterior_StorageBasketHandle",
}
house_interior_hero_required = {
    "HouseInterior_OldPcBackboard",
    "HouseInterior_OldPcHutchCleatLeft",
    "HouseInterior_OldPcHutchCleatRight",
    "HouseInterior_OldPcHutchShelf",
    "HouseInterior_OldPcHutchTop",
    "HouseInterior_OldPcHutchFolder",
    "HouseInterior_OldPcDocumentFolio",
    "HouseInterior_LeftWallWainscotField",
    "HouseInterior_LeftWallWainscotRail",
    "HouseInterior_TableFootRail",
}
house_interior_focus_groups = {
    "old-pc-document": {
        "HouseInterior_OldPcBackboard",
        "HouseInterior_OldPcHutchCleatLeft",
        "HouseInterior_OldPcHutchCleatRight",
        "HouseInterior_OldPcHutchShelf",
        "HouseInterior_OldPcHutchTop",
        "HouseInterior_OldPcHutchFolder",
        "HouseInterior_OldPcDocumentFolio",
    },
    "hearth-anchor": {
        "HouseInterior_HearthBase",
        "HouseInterior_HearthBody",
        "HouseInterior_HearthTop",
        "HouseInterior_HearthDoor",
        "HouseInterior_HearthFlue",
    },
    "domestic-cluster": {
        "HouseInterior_DaybedFrame",
        "HouseInterior_DaybedCushion",
        "HouseInterior_DaybedBack",
        "HouseInterior_StorageChestBody",
        "HouseInterior_StorageChestLid",
        "HouseInterior_StorageChestFront",
        "HouseInterior_TableTray",
        "HouseInterior_TableKettle",
        "HouseInterior_TableBowl",
    },
    "architectural-framing": {
        "HouseInterior_BaseTrimFront",
        "HouseInterior_BaseTrimBack",
        "HouseInterior_CeilingBeamLeft",
        "HouseInterior_CeilingBeamRight",
        "HouseInterior_EntryFrameLeft",
        "HouseInterior_EntryFrameRight",
        "HouseInterior_EntryFrameTop",
        *("HouseInterior_Window" + part + "_" + side
          for side in ("West", "East")
          for part in ("FrameLeft", "FrameRight", "FrameTop", "FrameBottom", "Sill")),
    },
}
house_interior_lod0 = [
    obj
    for obj in scene.objects
    if obj.type == "MESH" and obj.name.startswith("HouseInterior_") and "_LOD0" in obj.name
]
if scene.get("house_interior_lod0_count") != 102 or len(house_interior_lod0) != 102:
    raise RuntimeError(
        "Expected 102 HouseInterior LOD0 meshes, "
        f"got scene={scene.get('house_interior_lod0_count')!r}, actual={len(house_interior_lod0)}"
    )
actual_house_interior = {obj.name.replace("_LOD0", "") for obj in house_interior_lod0}
if actual_house_interior != house_interior_required:
    raise RuntimeError(
        "HouseInterior required mesh set changed: "
        f"missing={sorted(house_interior_required - actual_house_interior)!r}, "
        f"unexpected={sorted(actual_house_interior - house_interior_required)!r}"
    )
for group_name, required in house_interior_focus_groups.items():
    missing = required - actual_house_interior
    if missing:
        raise RuntimeError(f"HouseInterior {group_name} semantic contract missing={sorted(missing)!r}")
actual_house_interior_edge = actual_house_interior & house_interior_edge_required
if scene.get("house_interior_edge_lod0_count") != 14 or actual_house_interior_edge != house_interior_edge_required:
    raise RuntimeError(
        "HouseInterior edge-cluster contract changed: "
        f"expected=14, metadata={scene.get('house_interior_edge_lod0_count')!r}, "
        f"actual={sorted(actual_house_interior_edge)!r}"
    )
actual_house_interior_lived_in = actual_house_interior & house_interior_lived_in_required
if scene.get("house_interior_lived_in_lod0_count") != 17 or actual_house_interior_lived_in != house_interior_lived_in_required:
    raise RuntimeError(
        "HouseInterior lived-in cluster contract changed: "
        f"expected=17, metadata={scene.get('house_interior_lived_in_lod0_count')!r}, "
        f"actual={sorted(actual_house_interior_lived_in)!r}"
    )
actual_house_interior_hero = actual_house_interior & house_interior_hero_required
if scene.get("house_interior_hero_lod0_count") != 10 or actual_house_interior_hero != house_interior_hero_required:
    raise RuntimeError(
        "HouseInterior hero joinery contract changed: "
        f"expected=10, metadata={scene.get('house_interior_hero_lod0_count')!r}, "
        f"actual={sorted(actual_house_interior_hero)!r}"
    )
for obj in house_interior_lod0:
    if obj.get("urman_asset_id") != "env.house.interior.a" or obj.get("collision") != "none":
        raise RuntimeError(f"{obj.name} must be presentation-only env.house.interior.a geometry")

for obj in house_interior_lod1:
    if obj.get("urman_asset_id") != "env.house.interior.a" or obj.get("collision") != "none":
        raise RuntimeError(f"{obj.name} must be presentation-only env.house.interior.a geometry")
    if obj.get("lod_source") != obj.name.replace("_LOD1", "_LOD0"):
        raise RuntimeError(f"{obj.name} has incorrect lod_source={obj.get('lod_source')!r}")

missing_sources = [obj.name for obj in lod1 if not obj.get("lod_source")]
if missing_sources:
    raise RuntimeError(f"LOD1 meshes missing lod_source: {missing_sources}")

old_pc_expected = {
    "OldPc_DriveSlot_LOD0",
    "OldPc_DriveSlot_LOD1",
    "OldPc_LabelPlate_LOD0",
    "OldPc_LabelPlate_LOD1",
}
old_pc_names = {obj.name for obj in scene.objects if obj.type == "MESH" and obj.name in old_pc_expected}
if old_pc_names != old_pc_expected:
    raise RuntimeError(
        "OldPc hero-detail pairs are incomplete: "
        f"expected={sorted(old_pc_expected)!r}, actual={sorted(old_pc_names)!r}"
    )

for name in sorted(old_pc_expected):
    detail = scene.objects.get(name)
    if detail is None or detail.get("urman_asset_id") != "prop.oldpc.crt":
        raise RuntimeError(f"{name} missing prop.oldpc.crt asset id")
    if name.endswith("_LOD0") and detail.get("collision") != "none":
        raise RuntimeError(f"{name} must remain presentation-only collision=none")
    if name.endswith("_LOD1"):
        if detail.get("lod_source") != name.replace("_LOD1", "_LOD0"):
            raise RuntimeError(f"{name} has incorrect lod_source={detail.get('lod_source')!r}")
        if detail.get("collision") != "none":
            raise RuntimeError(f"{name} must remain presentation-only collision=none")

def component_vertices(mesh):
    neighbors = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        neighbors[a].add(b)
        neighbors[b].add(a)
    remaining = set(range(len(mesh.vertices)))
    result = []
    while remaining:
        root = min(remaining)
        pending, component = [root], []
        remaining.remove(root)
        while pending:
            index = pending.pop()
            component.append(mesh.vertices[index].co.copy())
            for other in neighbors[index]:
                if other in remaining:
                    remaining.remove(other)
                    pending.append(other)
        result.append(component)
    return result


def surface_tree(obj):
    return BVHTree.FromPolygons([vertex.co for vertex in obj.data.vertices],
                               [tuple(face.vertices) for face in obj.data.polygons])


def verify_household_mesh(obj, origin, minimum, maximum):
    if obj.get("household_detail_version") != 1 or obj.get("collision") != "none":
        raise RuntimeError(f"{obj.name}: missing presentation-only household detail contract")
    if (obj.location - Vector(origin)).length > .000002:
        raise RuntimeError(f"{obj.name}: original anchor changed: {tuple(obj.location)}")
    if any(abs(float(value)) > .000002 for value in obj.rotation_euler) or (obj.scale - Vector((1, 1, 1))).length > .000002:
        raise RuntimeError(f"{obj.name}: household source transform changed")
    for vertex in obj.data.vertices:
        if any(vertex.co[axis] < minimum[axis] - .000002 or vertex.co[axis] > maximum[axis] + .000002 for axis in range(3)):
            raise RuntimeError(f"{obj.name}: vertex outside original enclosing volume: {tuple(vertex.co)}")
    edge_uses = {}
    for face in obj.data.polygons:
        for a, b in zip(face.vertices, list(face.vertices[1:]) + [face.vertices[0]]):
            edge = tuple(sorted((a, b)))
            edge_uses[edge] = edge_uses.get(edge, 0) + 1
    if any(count != 2 for count in edge_uses.values()):
        raise RuntimeError(f"{obj.name}: open/nonmanifold household shell")
    obj.data.calc_loop_triangles()
    for triangle in obj.data.loop_triangles:
        a, b, c = (obj.data.vertices[index].co for index in triangle.vertices)
        if (b - a).cross(c - a).length_squared <= 1e-24:
            raise RuntimeError(f"{obj.name}: collapsed household triangle")
    triangles = len(obj.data.loop_triangles)
    if triangles > int(obj["triangle_budget"]):
        raise RuntimeError(f"{obj.name}: triangle budget exceeded: {triangles}/{obj['triangle_budget']}")
    if obj.name.endswith("_LOD1"):
        if (obj.get("household_lod_cage_version") != 1
                or obj.get("household_lod_cage_limit_meters") != .0001
                or not 0 <= obj.get("household_lod_cage_max_delta_meters", -1) <= .0001):
            raise RuntimeError(f"{obj.name}: missing bounded household LOD cage repair contract")
    return surface_tree(obj)


for suffix in ("LOD0", "LOD1"):
    kettle = scene.objects["HouseInterior_TableKettle_" + suffix]
    lid = scene.objects["HouseInterior_TableKettleLid_" + suffix]
    keyboard = scene.objects["OldPc_Keyboard_" + suffix]
    kettle_tree = verify_household_mesh(kettle, (1.08, 3.62, 1.11),
                                       (-.171325, -.171325, -.15), (.171325, .171325, .15))
    lid_tree = verify_household_mesh(lid, (1.08, 3.62, 1.29),
                                    (-.17, -.17, -.034), (.17, .17, .02))
    keyboard_tree = verify_household_mesh(keyboard, (-9, 7.3, .96),
                                         (-.575, -.24, -.05), (.575, .24, .05))
    # These rays inspect real holes, their adjacent solid walls, the grip and
    # support. They do not accept a metadata string in place of the geometry.
    handle_void = kettle_tree.ray_cast(Vector((-.25, .135, .025)), Vector((1, 0, 0)), .5)[0]
    handle_solid = kettle_tree.ray_cast(Vector((-.25, .158, .025)), Vector((1, 0, 0)), .5)[0]
    if handle_void is not None or handle_solid is None:
        raise RuntimeError(f"{kettle.name}: handle opening/wall lost")
    mouth = Vector((0, -.148, .115))
    mouth_normal = Vector((0, -.007, .047)).normalized()
    if kettle_tree.ray_cast(mouth + mouth_normal * .04, -mouth_normal, .053)[0] is not None:
        raise RuntimeError(f"{kettle.name}: spout mouth is filled")
    if kettle_tree.ray_cast(mouth + Vector((.018, 0, 0)) + mouth_normal * .04, -mouth_normal, .055)[0] is None:
        raise RuntimeError(f"{kettle.name}: spout rim lost")
    base = kettle_tree.ray_cast(Vector((0, 0, -.25)), Vector((0, 0, 1)), .2)[0]
    grip = lid_tree.ray_cast(Vector((0, 0, .10)), Vector((0, 0, -1)), .2)[0]
    if base is None or abs(base.z + .15) > .001 or grip is None or grip.z < .013:
        raise RuntimeError(f"{kettle.name}: original tray support or raised lid grip lost")
    # The former keyboard was one slab. Count actual disconnected case/key
    # shells, then inspect every cap and the physical gap to its right.
    components = component_vertices(keyboard.data)
    case = max(components, key=lambda points: max(point.x for point in points) - min(point.x for point in points))
    caps = [points for points in components if points is not case]
    if len(caps) != 104:
        raise RuntimeError(f"{keyboard.name}: expected 104 physical keycaps, got {len(caps)}")
    for points in caps:
        left, right = min(point.x for point in points), max(point.x for point in points)
        front, back = min(point.y for point in points), max(point.y for point in points)
        x, y = (left + right) * .5, (front + back) * .5
        top = keyboard_tree.ray_cast(Vector((x, y, .15)), Vector((0, 0, -1)), .3)[0]
        gap = keyboard_tree.ray_cast(Vector((right + .002, y, .15)), Vector((0, 0, -1)), .3)[0]
        if top is None or gap is None or top.z - gap.z < .008:
            raise RuntimeError(f"{keyboard.name}: missing cap/gap at {(x, y)}, top={top}, gap={gap}")
    print(f"asset-household-smoke: {suffix}; open kettle handle/spout, seated base, raised grip; 104 separate keycaps")


policy = scene.get("lod_policy")
if not isinstance(policy, str) or "deterministic Decimate" not in policy:
    raise RuntimeError(f"Unexpected lod_policy: {policy!r}")

detail_policy = scene.get("detail_policy")
if not isinstance(detail_policy, str) or "edge breaks" not in detail_policy or "HouseA facade" not in detail_policy or "HouseInterior" not in detail_policy or "threshold/window framing" not in detail_policy or "tapered furniture silhouettes" not in detail_policy or "hearth/cupboard/table/storage cluster" not in detail_policy or "WellA" not in detail_policy or "WoodpileA" not in detail_policy or "GateA" not in detail_policy or "four-tier" not in detail_policy or "OldPc tower" not in detail_policy or "drive-slot" not in detail_policy or "label-plate" not in detail_policy:
    raise RuntimeError(f"Unexpected detail_policy: {detail_policy!r}")

print(f"asset-lod-smoke: {len(lod1)} LOD1 meshes; policy={policy}; detail={detail_policy}")
