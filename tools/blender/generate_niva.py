"""Generate babay's VAZ-2121 Niva body, wheel and car radio for URMAN.

Run with Blender 4.5 LTS:
  blender --background --python tools/blender/generate_niva.py -- --root <repo>

Project-original geometry, modelled from the car's public proportions
(3.74 m long, 1.68 m wide, 2.20 m wheelbase) rather than any photograph or
third-party model. The runtime keeps its own live nodes (wheel pivots, steering
wheel, gauges, headlights and the "101.4" tuning label); this file supplies
only presentation meshes with no collision authority:

  NivaBody   hollow painted shell with real window openings, flared arches,
             chrome bumpers, round headlamps, grille, lamps, seals, mirrors
  NivaWheel  stamped steel wheel with hubcap and a rounded tyre, axle on X
  NivaRadio  a 1-DIN cassette car radio: LCD window, knobs, presets, tape door
  NivaInterior  dash around the live gauges, seats (sheepskin on the driver's),
             rubber mats, levers, door cards, headliner, mirror, visors
  NivaSteering  two-spoke VAZ wheel in a laced leather cover (local XY rim)
  NivaCharm  shamail medallion with the name of God in square Kufic and a
             wooden tasbih, hung from the mirror (local origin = hanging point)

The body shell is lofted from sections; road grime is a vertex colour.

Coordinates are authored in Godot space (x right, y up, -z forward) and mapped
to Blender (x, -z, y) so the glTF +Y-up export lands back in Godot space.
Material names are "<hex>__<surface>"; VehicleVisualFactory resolves them with
the same glass/trim/painterly rules as the procedural cabin.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

PAINT = "596c59"
PAINT_DARK = "4c5d4c"
CHROME = "c9cdc6"
RUBBER = "1f2320"
PLASTIC = "2a2e2b"


def G(x: float, y: float, z: float) -> Vector:
    """Godot (x, y up, -z forward) to Blender (x, y forward=-z_g, z up)."""
    return Vector((x, -z, y))


_materials: dict[str, bpy.types.Material] = {}


def material(color: str, surface: str) -> bpy.types.Material:
    name = f"{color}__{surface}"
    if name not in _materials:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        rgb = tuple(int(color[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.35 if surface in ("chrome", "glass", "gold") else 0.3 if surface == "paint" else 0.7
        if surface == "paint":
            bsdf.inputs["Metallic"].default_value = 0.55
        if surface == "glass":
            bsdf.inputs["Alpha"].default_value = 0.3
        _materials[name] = mat
    return _materials[name]


def new_object(name: str, mesh: bpy.types.Mesh, parent: bpy.types.Object | None) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def assign(obj: bpy.types.Object, mat: bpy.types.Material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def apply_modifiers(obj: bpy.types.Object) -> None:
    bpy.context.view_layer.objects.active = obj
    for other in bpy.context.selected_objects:
        other.select_set(False)
    obj.select_set(True)
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def bevel(obj: bpy.types.Object, width: float, segments: int = 2, angle: float = 40.0) -> None:
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(angle)
    mod.harden_normals = False


def box(name, size, center, color, surface, parent, bevel_width=0.0, segments=2, rotation=(0, 0, 0)):
    """Axis-aligned Godot box (size and center in Godot space)."""
    sx, sy, sz = size
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sz, v.co.z * sy))  # Blender X/Y/Z extents = Godot x/z/y
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = new_object(name, mesh, parent)
    obj.location = G(*center)
    # Godot X = Blender X, Godot Y = Blender Z, Godot Z = Blender -Y.
    obj.rotation_euler = (math.radians(rotation[0]), -math.radians(rotation[2]), math.radians(rotation[1]))
    assign(obj, material(color, surface))
    if bevel_width > 0:
        bevel(obj, bevel_width, segments, angle=30.0)
        apply_modifiers(obj)
    return obj


def beam(name, a, b, thickness, color, surface, parent, depth=None):
    """Rectangular bar between two Godot points."""
    pa, pb = G(*a), G(*b)
    axis = pb - pa
    length = axis.length
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * thickness, v.co.y * (depth or thickness), v.co.z * length))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = new_object(name, mesh, parent)
    obj.location = (pa + pb) * 0.5
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(axis.normalized())
    assign(obj, material(color, surface))
    bevel(obj, min(thickness, depth or thickness) * 0.3, 2)
    apply_modifiers(obj)
    return obj


def cylinder_x(name, radius, width, center, color, surface, parent, segments=28, bevel_width=0.0):
    """Cylinder whose axis runs along Godot X (wheels, knobs seen from the side)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=width)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=_rot_y90())
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = new_object(name, mesh, parent)
    obj.location = G(*center)
    assign(obj, material(color, surface))
    if bevel_width > 0:
        bevel(obj, bevel_width, 3, angle=30.0)
        apply_modifiers(obj)
    return obj


def cylinder_z(name, radius, depth, center, color, surface, parent, segments=24, bevel_width=0.0):
    """Cylinder whose axis runs along Godot Z (lamps, knobs facing the driver)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=depth)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=_rot_x90())
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = new_object(name, mesh, parent)
    obj.location = G(*center)
    assign(obj, material(color, surface))
    if bevel_width > 0:
        bevel(obj, bevel_width, 2, angle=30.0)
        apply_modifiers(obj)
    return obj


def _rot_y90():
    from mathutils import Matrix
    return Matrix.Rotation(math.radians(90), 3, "Y")


def _rot_x90():
    from mathutils import Matrix
    return Matrix.Rotation(math.radians(90), 3, "X")


def extrude_profile(name, points_zy, x0, x1, color, surface, parent, drop_top_between=None):
    """Side silhouette (Godot z, y) extruded across x0..x1. Optionally removes the
    top face spanning drop_top_between=(z_from, z_to) to open the cabin."""
    bm = bmesh.new()
    left = [bm.verts.new(G(x0, y, z)) for z, y in points_zy]
    right = [bm.verts.new(G(x1, y, z)) for z, y in points_zy]
    bm.faces.new(left)
    bm.faces.new(list(reversed(right)))
    count = len(points_zy)
    for i in range(count):
        j = (i + 1) % count
        face = bm.faces.new((left[i], left[j], right[j], right[i]))
        if drop_top_between is not None:
            (za, ya), (zb, yb) = points_zy[i], points_zy[j]
            lo, hi = drop_top_between
            if abs(ya - yb) < 1e-4 and min(za, zb) >= lo - 1e-4 and max(za, zb) <= hi + 1e-4:
                bm.faces.remove(face)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = new_object(name, mesh, parent)
    assign(obj, material(color, surface))
    return obj


def quad(name, a, b, c, d, color, surface, parent, thickness=0.006):
    """Thin double-sided pane through four Godot points."""
    bm = bmesh.new()
    pts = [G(*p) for p in (a, b, c, d)]
    normal = (pts[1] - pts[0]).cross(pts[3] - pts[0]).normalized() * (thickness * 0.5)
    front = [bm.verts.new(p + normal) for p in pts]
    back = [bm.verts.new(p - normal) for p in pts]
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((front[i], back[i], back[j], front[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = new_object(name, mesh, parent)
    assign(obj, material(color, surface))
    return obj


def arch_flare(name, axle_z, side, parent):
    """Pronounced Niva wheel-arch flare: a bevelled arc over the wheel."""
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = 0.028
    curve.bevel_resolution = 2
    spline = curve.splines.new("POLY")
    steps = 22
    spline.points.add(steps)
    for i in range(steps + 1):
        a = math.radians(-12 + (204 * i / steps))  # from rear-low through top to front-low
        z = axle_z + math.cos(a) * 0.415
        y = 0.345 + math.sin(a) * 0.415
        p = G(side * 0.822, max(y, 0.43), z)
        spline.points[i].co = (p.x, p.y, p.z, 1)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    curve.materials.append(material(PAINT, "paint"))
    bpy.context.view_layer.objects.active = obj
    for other in bpy.context.selected_objects:
        other.select_set(False)
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    return bpy.context.view_layer.objects.active


def boolean_cut(target, cutter):
    mod = target.modifiers.new("Cut", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.solver = "EXACT"
    mod.object = cutter
    apply_modifiers(target)
    bpy.data.objects.remove(cutter, do_unlink=True)


FRONT_Z = -1.985
REAR_Z = 1.862
HALF_W = .812


def _round_in(distance: float, radius: float) -> float:
    """How far a corner of the given radius pulls in at `distance` from its edge."""
    if distance >= radius:
        return 0.0
    d = radius - distance
    return radius - math.sqrt(max(radius * radius - d * d, 0.0))


def _station(z: float) -> tuple[float, float, float, float]:
    """(half width, bottom, top, crown) of the body section at Godot z."""
    from_front = z - FRONT_Z
    from_rear = REAR_Z - z
    w = HALF_W - _round_in(from_front, .075) - _round_in(from_rear, .06)
    if z < -.70:
        # Bonnet rises gently from the grille to the cowl.
        t = (z - FRONT_Z) / (-.70 - FRONT_Z)
        top = 1.036 + .058 * t
        crown = .016
    else:
        top = 1.100 - .028 * max(0.0, (z - 1.805) / (REAR_Z - 1.805))
        crown = .006
    top -= _round_in(from_front, .055) + _round_in(from_rear, .04)
    return w, .47, top, crown


def _section(z: float) -> list[tuple[float, float]]:
    """Closed-over-the-top body section, left sill to right sill: tucked sill,
    upright side with the Niva's shoulder crease, tumblehome and a rounded
    shoulder into a crowned top."""
    w, yb, yt, crown = _station(z)
    left = [(-(w - .032), yb), (-(w - .008), yb + .05), (-w, yb + .14), (-w, yt - .15),
            (-(w + .006), yt - .125), (-(w + .006), yt - .105), (-w, yt - .088),
            (-(w - .010), yt - .048), (-(w - .026), yt - .018), (-(w - .052), yt - .004), (-(w - .09), yt)]
    top = [(-.55, yt + crown * .55), (-.28, yt + crown * .92), (0.0, yt + crown),
           (.28, yt + crown * .92), (.55, yt + crown * .55)]
    right = [(-x, y) for x, y in reversed(left)]
    return left + top + right


def loft_shell(name, parent):
    """Body shell lofted from sections, so its sides, shoulders and plan
    corners are curved surfaces rather than one extruded silhouette. The top
    between the cowl and the hatch stays open for the cabin."""
    stations = {FRONT_Z, REAR_Z, -.70, 1.805}
    for d in (.004, .012, .025, .04, .06, .085, .11, .15, .2):
        stations.add(FRONT_Z + d)
        stations.add(REAR_Z - d)
    z = FRONT_Z + .3
    while z < REAR_Z - .25:
        stations.add(round(z, 4))
        z += .1
    stations = sorted(stations)
    sections = [_section(z) for z in stations]
    count = len(sections[0])
    top_from, top_to = 10, count - 11  # indices of the flat top run
    bm = bmesh.new()
    rows = [[bm.verts.new(G(x, y, z)) for x, y in section] for z, section in zip(stations, sections)]
    for i in range(len(rows) - 1):
        z0, z1 = stations[i], stations[i + 1]
        cabin = z0 >= -.70 - 1e-4 and z1 <= 1.805 + 1e-4
        for j in range(count - 1):
            if cabin and top_from <= j < top_to:
                continue
            bm.faces.new((rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]))
    # Front and rear faces close the section outline.
    bm.faces.new(list(reversed(rows[0])))
    bm.faces.new(rows[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = new_object(name, mesh, parent)
    assign(obj, material(PAINT, "paint"))
    solid = obj.modifiers.new("Solidify", "SOLIDIFY")
    solid.thickness = .03
    solid.offset = -1
    apply_modifiers(obj)
    return obj


def snow_cap(name, size, center, parent, bumps=.018, seed=0):
    """A soft settled snow layer: a subdivided slab whose top is lifted by noise."""
    obj = box(name, size, center, "eef2f6", "snow", parent, .02, 2)
    sub = obj.modifiers.new("Sub", "SUBSURF")
    sub.levels = 2
    sub.render_levels = 2
    tex = bpy.data.textures.new(name + "Noise", "CLOUDS")
    tex.noise_scale = .18
    disp = obj.modifiers.new("Lumps", "DISPLACE")
    disp.texture = tex
    disp.strength = bumps
    disp.direction = "Z"
    apply_modifiers(obj)
    return obj


def tube(name, points, radius, color, surface, parent, resolution=3):
    """A round tube along Godot points (rack rails, cables, lacing)."""
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = resolution
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for i, p in enumerate(points):
        g = G(*p)
        spline.points[i].co = (g.x, g.y, g.z, 1)
    curve.use_fill_caps = True
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    curve.materials.append(material(color, surface))
    bpy.context.view_layer.objects.active = obj
    for other in bpy.context.selected_objects:
        other.select_set(False)
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    return bpy.context.view_layer.objects.active


def build_body(root):
    shell = loft_shell("NivaBody_Shell", root)
    for axle in (-1.18, 1.10):
        cutter = cylinder_x("ArchCutter", .392, 2.2, (0, .345, axle), PAINT, "paint", None, segments=40)
        boolean_cut(shell, cutter)

    for side in (-1, 1):
        box(f"NivaBody_Sill{side}", (.03, .07, 1.20), (side * .80, .49, -.08), "262a26", "plastic", root, .012)
        arch_flare(f"NivaBody_ArchFlareFront{side}", -1.18, side, root)
        arch_flare(f"NivaBody_ArchFlareRear{side}", 1.10, side, root)
        for axle in (-1.18, 1.10):
            cylinder_x(f"NivaBody_WellLiner{side}_{axle}", .40, .30, (side * .64, .40, axle), "161a18", "rubber", root, 28)
        # Mud flaps behind every wheel: black rubber with a stiff top edge.
        for axle in (-1.18, 1.10):
            box(f"NivaBody_MudFlap{side}_{axle}", (.012, .24, .22), (side * .70, .30, axle + .43), "181a18", "rubber", root, .004)
        # Side repeater on the front wing.
        box(f"NivaBody_Repeater{side}", (.012, .025, .055), (side * .818, .975, -1.52), "d9912f", "lamp", root, .005)

    # Cabin tub and a closed underbody so nothing shows through from low views.
    box("NivaBody_Floor", (1.52, .04, 2.52), (0, .48, .54), "262a26", "rubber", root)
    box("NivaBody_Firewall", (1.54, .60, .03), (0, .80, -.705), "2e332e", "metal", root)
    box("NivaBody_CargoFloor", (1.40, .05, .52), (0, .72, 1.55), "2a2f2a", "rubber", root)
    box("NivaUnder_Pan", (1.50, .02, 3.55), (0, .455, -.06), "141614", "rubber", root)
    # Running gear under the body: axles, differentials, propshafts, exhaust.
    cylinder_x("NivaUnder_RearAxle", .042, 1.30, (0, .345, 1.10), "1d201d", "metal", root, 14)
    for name, center, r in (("NivaUnder_RearDiff", (0, .34, 1.10), .13), ("NivaUnder_FrontDiff", (.08, .37, -1.18), .11)):
        obj = cylinder_z(name, r, .22, center, "1d201d", "metal", root, 18, .03)
    cylinder_x("NivaUnder_FrontShafts", .022, 1.24, (0, .345, -1.18), "2a2d2a", "metal", root, 10)
    beam("NivaUnder_RearProp", (0, .36, -.05), (0, .35, 1.0), .05, "252825", "metal", root)
    beam("NivaUnder_FrontProp", (.05, .37, -.15), (.08, .37, -1.05), .045, "252825", "metal", root)
    tube("NivaUnder_Exhaust", [(-.25, .36, -1.45), (-.30, .33, -.80), (-.32, .33, .40), (-.40, .33, 1.35), (-.52, .40, 1.72),
                               (-.52, .47, 1.90)], .024, "3a3833", "metal", root)
    cylinder_z("NivaUnder_Muffler", .075, .50, (-.33, .33, .80), "34322e", "metal", root, 18, .02)

    # Greenhouse: crowned roof, pillars, drip rails, hatch frame.
    roof = box("NivaBody_Roof", (1.50, .05, 1.84), (0, 1.668, .615), PAINT, "paint", root, .04, 4)
    for side in (-1, 1):
        beam(f"NivaBody_APillar{side}", (side * .783, 1.10, -.70), (side * .735, 1.645, -.30), .055, PAINT, "paint", root, .05)
        beam(f"NivaBody_BPillar{side}", (side * .790, 1.10, .505), (side * .742, 1.645, .505), .085, PAINT, "paint", root, .045)
        cp = extrude_profile(f"NivaBody_CPillar{side}", [(1.36, 1.10), (1.815, 1.10), (1.545, 1.645), (1.30, 1.645)],
                             side * .745, side * .790, PAINT, "paint", root)
        bevel(cp, .014, 3)
        apply_modifiers(cp)
        beam(f"NivaBody_DripRail{side}", (side * .752, 1.652, -.30), (side * .752, 1.652, 1.53), .016, "485648", "paint", root)
        beam(f"NivaBody_BeltSeal{side}", (side * .795, 1.108, -.66), (side * .795, 1.108, 1.36), .016, RUBBER, "rubber", root, .02)
        beam(f"NivaBody_QuarterVent{side}", (side * .788, 1.12, -.52), (side * .752, 1.52, -.40), .012, RUBBER, "rubber", root)
    beam("NivaBody_Header", (-.73, 1.645, -.302), (.73, 1.645, -.302), .045, PAINT, "paint", root, .05)
    beam("NivaBody_Cowl", (-.78, 1.105, -.705), (.78, 1.105, -.705), .05, PAINT, "paint", root, .06)
    box("NivaBody_CowlGrille", (1.30, .008, .07), (0, 1.112, -.745), "1a1d1a", "plastic", root, .003)
    beam("NivaBody_HatchSill", (-.77, 1.10, 1.818), (.77, 1.10, 1.818), .045, PAINT, "paint", root, .05)
    beam("NivaBody_HatchTop", (-.70, 1.64, 1.545), (.70, 1.64, 1.545), .04, PAINT, "paint", root, .04)

    # Roof rack: the village Niva always carries one.
    for side in (-1, 1):
        for z in (-.12, 1.32):
            box(f"NivaRack_Foot{side}_{z}", (.035, .08, .06), (side * .735, 1.70, z), "232523", "metal", root, .008)
        tube(f"NivaRack_Rail{side}", [(side * .70, 1.745, -.22), (side * .70, 1.75, 1.42)], .013, "2b2d2a", "metal", root)
    for z in (-.14, .26, .66, 1.06, 1.34):
        tube(f"NivaRack_Bar{z}", [(-.70, 1.748, z), (.70, 1.748, z)], .011, "2b2d2a", "metal", root)

    # Settled snow: roof, the bonnet's cowl corner and the rack bars' shelter.
    snow_cap("NivaSnow_Roof", (1.38, .045, 1.66), (0, 1.705, .62), root, .02)
    snow_cap("NivaSnow_Cowl", (1.30, .035, .24), (0, 1.12, -.84), root, .014)
    snow_cap("NivaSnow_HatchSill", (1.30, .02, .08), (0, 1.13, 1.80), root, .008)

    # Glass: windshield, doors, rear quarters and hatch, with black seals.
    quad("NivaGlass_Windshield", (-.745, 1.12, -.69), (.745, 1.12, -.69), (.715, 1.63, -.31), (-.715, 1.63, -.31), "83988c", "glass", root)
    for side in (-1, 1):
        def P(z, y):
            return (side * (0.788 - (y - 1.12) / .51 * .045), y, z)
        quad(f"NivaGlass_Door{side}", P(-.66, 1.125), P(.46, 1.125), P(.46, 1.625), P(-.27, 1.625), "83988c", "glass", root)
        quad(f"NivaGlass_Quarter{side}", P(.55, 1.125), P(1.33, 1.125), P(1.29, 1.625), P(.55, 1.625), "83988c", "glass", root)
    quad("NivaGlass_Hatch", (-.72, 1.14, 1.806), (.72, 1.14, 1.806), (.68, 1.615, 1.552), (-.68, 1.615, 1.552), "83988c", "glass", root)
    for x0, x1, y0, y1, z0, z1 in ((-.745, .745, 1.115, 1.115, -.695, -.695), (-.715, .715, 1.635, 1.635, -.305, -.305)):
        beam(f"NivaSeal_W{y0}", (x0, y0, z0), (x1, y1, z1), .018, RUBBER, "rubber", root)
    # Wipers parked at the base of the windshield and on the hatch.
    for name, a, b in (("NivaWiper_L", (-.52, 1.15, -.70), (-.06, 1.20, -.665)), ("NivaWiper_R", (.16, 1.15, -.70), (.62, 1.20, -.665)),
                       ("NivaWiper_Rear", (0, 1.17, 1.815), (.42, 1.26, 1.77))):
        beam(name + "Arm", a, b, .008, "1c1f1c", "plastic", root)
        beam(name + "Blade", (a[0] + .02, a[1] + .012, a[2] + .01), (b[0] + .02, b[1] + .012, b[2] + .01), .012, "111311", "rubber", root)
    for x in (-.30, .30):
        box(f"NivaWasher{x}", (.02, .012, .025), (x, 1.10, -.74), "1a1d1a", "plastic", root, .003)

    # Doors: seams, handles, locks, mirrors.
    for side in (-1, 1):
        x = side * .815
        beam(f"NivaDoor_FrontSeam{side}", (x, .50, -.64), (x, 1.10, -.66), .006, "1a1e1b", "rubber", root, .01)
        beam(f"NivaDoor_RearSeam{side}", (x, .50, .49), (x, 1.10, .49), .006, "1a1e1b", "rubber", root, .01)
        beam(f"NivaDoor_LowerSeam{side}", (x, .50, -.64), (x, .50, .49), .006, "1a1e1b", "rubber", root, .01)
        box(f"NivaDoor_Handle{side}", (.02, .022, .13), (side * .826, 1.035, .33), CHROME, "chrome", root, .006)
        box(f"NivaDoor_HandleRecess{side}", (.006, .03, .15), (side * .818, 1.035, .33), "1c201d", "rubber", root)
        cylinder_x(f"NivaDoor_Lock{side}", .011, .012, (side * .822, 1.035, .43), CHROME, "chrome", root, 14)
        beam(f"NivaMirror_Arm{side}", (side * .80, 1.16, -.57), (side * .873, 1.215, -.53), .02, PLASTIC, "plastic", root)
        box(f"NivaMirror_Housing{side}", (.035, .10, .14), (side * .898, 1.235, -.52), PLASTIC, "plastic", root, .015)
        box(f"NivaMirror_Glass{side}", (.004, .08, .115), (side * .899, 1.235, -.453), "9aa8a2", "glass", root)
    cylinder_x("NivaBody_FuelCap", .045, .012, (.818, .96, 1.47), PAINT_DARK, "paint", root, 20)
    # Whip antenna on the right front wing.
    cylinder_z("NivaAntenna_Base", .016, .03, (.70, 1.06, -1.05), "1a1d1a", "plastic", root, 12)
    tube("NivaAntenna_Whip", [(.70, 1.07, -1.05), (.70, 1.50, -1.16), (.70, 1.77, -1.30)], .0025, "2a2c2a", "metal", root, 1)

    # Front: black grille panel with the headlamps in chrome rings, plastic bumper.
    box("NivaFront_GrillePanel", (1.40, .21, .035), (0, .925, -1.99), "181a18", "plastic", root, .012)
    for i in range(6):
        box(f"NivaFront_GrilleBar{i}", (.72, .010, .018), (0, .842 + i * .033, -2.006), "2b2e2b", "plastic", root, .003)
    box("NivaFront_Badge", (.07, .045, .008), (0, .925, -2.018), CHROME, "chrome", root, .01)
    for side in (-1, 1):
        cylinder_z(f"NivaFront_LampBezel{side}", .094, .03, (side * .56, .925, -2.0), CHROME, "chrome", root, 40, .008)
        cylinder_z(f"NivaFront_LampLens{side}", .079, .02, (side * .56, .925, -2.012), "e6ecea", "lamp", root, 40, .012)
        box(f"NivaFront_Indicator{side}", (.13, .05, .03), (side * .56, .79, -1.99), "d9912f", "lamp", root, .01)
        box(f"NivaFront_TowHook{side}", (.03, .04, .06), (side * .40, .50, -2.03), "2a2e2a", "metal", root)
    box("NivaFront_Bumper", (1.62, .13, .13), (0, .615, -2.015), "262826", "plastic", root, .035, 4)
    for side in (-1, 1):
        box(f"NivaFront_BumperWrap{side}", (.07, .13, .30), (side * .79, .615, -1.90), "262826", "plastic", root, .03, 3)
    box("NivaFront_PlateHolder", (.54, .13, .012), (0, .63, -2.079), "151715", "plastic", root, .004)
    box("NivaFront_Plate", (.52, .112, .008), (0, .63, -2.0815), "f2f2ec", "metal", root, .004)
    beam("NivaFront_HoodSeamL", (-.70, 1.034, -1.93), (-.70, 1.094, -.74), .006, "343d34", "rubber", root, .004)
    beam("NivaFront_HoodSeamR", (.70, 1.034, -1.93), (.70, 1.094, -.74), .006, "343d34", "rubber", root, .004)
    beam("NivaFront_HoodSeamFront", (-.70, 1.030, -1.93), (.70, 1.030, -1.93), .006, "343d34", "rubber", root, .004)

    # Rear: vertical lamp clusters, plastic bumper, plate, handle, towbar, exhaust.
    for side in (-1, 1):
        x = side * .665
        box(f"NivaRear_LampHousing{side}", (.17, .30, .03), (x, .85, 1.862), "1c201d", "plastic", root, .01)
        box(f"NivaRear_Indicator{side}", (.14, .08, .02), (x, .955, 1.874), "d9912f", "lamp", root, .005)
        box(f"NivaRear_Tail{side}", (.14, .11, .02), (x, .855, 1.874), "a12b22", "lamp", root, .005)
        box(f"NivaRear_Reverse{side}", (.14, .06, .02), (x, .755, 1.874), "e9ece4", "lamp", root, .005)
        box(f"NivaRear_BumperWrap{side}", (.07, .13, .28), (side * .79, .62, 1.78), "262826", "plastic", root, .03, 3)
    box("NivaRear_Bumper", (1.62, .13, .13), (0, .62, 1.915), "262826", "plastic", root, .035, 4)
    box("NivaRear_Plate", (.52, .112, .008), (0, .80, 1.872), "f2f2ec", "metal", root, .004)
    box("NivaRear_PlateLight", (.10, .02, .03), (0, .87, 1.878), "1c201d", "plastic", root, .005)
    box("NivaRear_Handle", (.18, .025, .03), (0, 1.03, 1.855), CHROME, "chrome", root, .008)
    beam("NivaRear_TowbarBar", (0, .52, 1.90), (0, .52, 2.08), .05, "1d1f1d", "metal", root)
    cylinder_z("NivaRear_TowbarNeck", .018, .06, (0, .56, 2.08), "1d1f1d", "metal", root, 12)
    obj = cylinder_z("NivaRear_TowbarBall", .025, .05, (0, .60, 2.08), "7c7f78", "chrome", root, 16, .02)


def lathe_x(name, profile_xr, color, surface, parent, steps=40):
    """Revolve a closed (x, radius) profile around Godot X: a real tyre ring
    or rim, open in the middle so the disc inside stays visible."""
    bm = bmesh.new()
    verts = [bm.verts.new((x, 0.0, r)) for x, r in profile_xr]
    edges = [bm.edges.new((verts[i], verts[(i + 1) % len(verts)])) for i in range(len(verts))]
    bmesh.ops.spin(bm, geom=verts + edges, cent=(0, 0, 0), axis=(1, 0, 0), angle=math.tau, steps=steps,
                   use_merge=True)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = new_object(name, mesh, parent)
    assign(obj, material(color, surface))
    return obj


def build_wheel(root):
    # 175/80 R16 on a stamped steel wheel: a rounded tyre ring with sidewall
    # and shoulders, the rim flange and disc inside it, slots and a hubcap.
    # Stays inside the physical wheel (r .345, half-width .095): the vehicle
    # smoke checks every tyre vertex against that envelope while steering.
    # The physical tyre (VehicleWheelGeometry) rounds its shoulder from
    # (x .068, r .342) to (x .095, r .304); the profile stays just inside it.
    # The outer sidewall reaches the section's own corner (x .0949, r .3033):
    # a steered wheel must still sweep past x 1.017 like the physical tyre.
    tyre = lathe_x("NivaWheel_Tyre", [(-.080, .205), (-.090, .232), (-.093, .258), (-.0949, .290), (-.0949, .3033),
                                      (-.086, .314), (-.070, .336), (-.045, .340), (.045, .340), (.070, .336),
                                      (.086, .314), (.0949, .3033), (.0949, .290), (.093, .258), (.090, .232),
                                      (.080, .205)],
                   "262a26", "rubber", root, 44)
    lathe_x("NivaWheel_Rim", [(-.086, .200), (-.090, .212), (-.084, .214), (-.080, .204), (.080, .204),
                              (.084, .214), (.090, .212), (.086, .200)], "8f948c", "metal", root, 40)
    for side in (-1, 1):
        # The disc is dished: an outer ring, a raised stamped ridge and a hub plate.
        cylinder_x(f"NivaWheel_Disc{side}", .201, .008, (side * .060, 0, 0), "9aa097", "metal", root, 36)
        lathe_x(f"NivaWheel_Ridge{side}", [(side * .064, .150), (side * .075, .158), (side * .075, .172),
                                           (side * .064, .180)], "a3a99f", "metal", root, 36)
        for i in range(5):
            a = math.tau * i / 5
            box(f"NivaWheel_Slot{side}_{i}", (.006, .045, .026),
                (side * .065, math.cos(a) * .125, math.sin(a) * .125), "262a26", "rubber", root,
                rotation=(math.degrees(a), 0, 0))
        cylinder_x(f"NivaWheel_Hubcap{side}", .078, .022, (side * .076, 0, 0), CHROME, "chrome", root, 28, .010)
        for i in range(5):
            a = math.tau * i / 5 + .3
            cylinder_x(f"NivaWheel_Nut{side}_{i}", .010, .010, (side * .066, math.cos(a) * .098, math.sin(a) * .098),
                       "5b5f57", "metal", root, 8)
    # Shallow tread blocks: they break the silhouette without reading as teeth.
    for i in range(36):
        a = math.tau * i / 36
        box(f"NivaWheel_Tread{i}", (.12, .008, .030), (0, math.cos(a) * .3385, math.sin(a) * .3385),
            "1d201d", "rubber", root, rotation=(math.degrees(a), 0, 0))
    return tyre


def build_radio(root):
    # 1-DIN cassette radio on the centre dash at the existing label position
    # (.10, 1.008, -.367). Its face points at the driver (+Z).
    cx, cy, face = .10, .997, -.370
    box("NivaRadio_Chassis", (.182, .054, .03), (cx, cy, face - .016), "121412", "rubber", root, .004)
    box("NivaRadio_Trim", (.186, .058, .004), (cx, cy, face - .001), "8a8f88", "chrome", root, .002)
    box("NivaRadio_Face", (.178, .050, .006), (cx, cy, face + .001), "1b1e1c", "rubber", root, .003)
    box("NivaRadio_LcdWindow", (.072, .019, .003), (cx, 1.008, face + .0045), "1d2b1f", "lcd", root)
    box("NivaRadio_TapeDoor", (.078, .011, .003), (cx, .9815, face + .0045), "2c302d", "rubber", root, .001)
    box("NivaRadio_TapeSlot", (.066, .002, .002), (cx, .9815, face + .0062), "0b0c0b", "rubber", root)
    box("NivaRadio_Eject", (.012, .008, .005), (cx + .048, .9815, face + .005), "9a9f97", "chrome", root, .0015)
    for side, x in (("Volume", cx - .068), ("Tuning", cx + .068)):
        cylinder_z(f"NivaRadio_{side}KnobBase", .013, .006, (x, cy, face + .004), "2a2e2b", "rubber", root, 20)
        cylinder_z(f"NivaRadio_{side}Knob", .010, .014, (x, cy, face + .011), "3a3e3a", "rubber", root, 20, .002)
        cylinder_z(f"NivaRadio_{side}Cap", .006, .002, (x, cy, face + .0185), "a5aaa2", "chrome", root, 16)
    for i in range(5):
        box(f"NivaRadio_Preset{i}", (.0105, .0065, .004), (cx - .031 + i * .0155, .9955, face + .0048),
            "3b403c", "rubber", root, .0012)
    box("NivaRadio_BandFM", (.014, .006, .004), (cx + .048, 1.013, face + .0048), "4a4f4a", "rubber", root, .0012)
    box("NivaRadio_BandAM", (.014, .006, .004), (cx + .048, 1.003, face + .0048), "4a4f4a", "rubber", root, .0012)


def fleece(name, size, center, parent, rotation=(0, 0, 0)):
    """Sheepskin: a soft slab whose surface is broken into woolly lumps."""
    obj = box(name, size, center, "b8a07c", "sheepskin", parent, min(size) * .45, 3, rotation)
    sub = obj.modifiers.new("Sub", "SUBSURF")
    sub.levels = 2
    tex = bpy.data.textures.new(name + "Wool", "CLOUDS")
    tex.noise_scale = .025
    disp = obj.modifiers.new("Wool", "DISPLACE")
    disp.texture = tex
    disp.strength = .012
    apply_modifiers(obj)
    return obj


def seat(prefix, x, parent, sheepskin=False):
    """Front bucket seat: cushion, reclined back and headrest on posts, vinyl
    bolsters around a cloth centre."""
    box(f"{prefix}_Frame", (.44, .06, .46), (x, .56, .10), "1c1e1c", "metal", parent, .01)
    for rail in (-.17, .17):
        box(f"{prefix}_Rail{rail}", (.03, .03, .60), (x + rail, .515, .05), "2c2e2b", "metal", parent, .006)
    box(f"{prefix}_Cushion", (.50, .12, .50), (x, .70, .10), "302f2b", "vinyl", parent, .045, 4)
    box(f"{prefix}_Back", (.50, .58, .11), (x, 1.02, .40), "302f2b", "vinyl", parent, .045, 4, rotation=(-12, 0, 0))
    for post in (-.07, .07):
        beam(f"{prefix}_HeadPost{post}", (x + post, 1.28, .455), (x + post, 1.36, .47), .012, "8f938c", "chrome", parent)
    box(f"{prefix}_Headrest", (.27, .17, .085), (x, 1.43, .485), "302f2b", "vinyl", parent, .035, 4, rotation=(-8, 0, 0))
    box(f"{prefix}_Recliner", (.02, .06, .06), (x + (.26 if x > 0 else -.26), .78, .32), "1c1e1c", "plastic", parent, .008)
    if sheepskin:
        # Babay drives on a sheepskin cover, as village drivers do in winter.
        fleece(f"{prefix}_FleeceSeat", (.46, .045, .46), (x, .775, .09), parent)
        fleece(f"{prefix}_FleeceBack", (.46, .54, .045), (x, 1.02, .343), parent, rotation=(-12, 0, 0))
    else:
        box(f"{prefix}_ClothSeat", (.30, .012, .42), (x, .763, .09), "5a5046", "cloth", parent, .004)
        box(f"{prefix}_ClothBack", (.30, .44, .012), (x, 1.03, .343), "5a5046", "cloth", parent, .004, rotation=(-12, 0, 0))


def rubber_mat(name, x0, x1, z0, z1, parent, y=.506):
    """Ribbed rubber floor mat with a raised rim."""
    w, d = x1 - x0, z1 - z0
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    box(name, (w, .01, d), (cx, y, cz), "191b19", "rubber", parent, .004)
    for side in (-1, 1):
        box(f"{name}_RimX{side}", (.012, .014, d), (cx + side * (w / 2 - .006), y + .009, cz), "141614", "rubber", parent, .003)
        box(f"{name}_RimZ{side}", (w, .014, .012), (cx, y + .009, cz + side * (d / 2 - .006)), "141614", "rubber", parent, .003)
    ribs = int(d / .045)
    for i in range(1, ribs):
        box(f"{name}_Rib{i}", (w - .06, .006, .012), (cx, y + .007, z0 + i * d / ribs), "222422", "rubber", parent)


def door_card(side, parent):
    x = side * .772
    box(f"NivaCab_DoorCard{side}", (.02, .50, 1.06), (x, .83, -.08), "3a3833", "vinyl", parent, .008)
    box(f"NivaCab_DoorInsert{side}", (.006, .20, .70), (x - side * .011, .86, -.12), "4b463d", "cloth", parent, .003)
    box(f"NivaCab_Armrest{side}", (.06, .05, .30), (side * .742, .92, .06), "2f2e2a", "vinyl", parent, .02, 3)
    box(f"NivaCab_Pocket{side}", (.03, .10, .60), (side * .752, .63, -.12), "2a2926", "vinyl", parent, .01)
    # Window crank and the chrome door lever in its recess.
    cylinder_x(f"NivaCab_CrankHub{side}", .018, .02, (side * .755, .86, -.34), "8f938c", "chrome", parent, 16)
    beam(f"NivaCab_CrankArm{side}", (side * .745, .86, -.34), (side * .742, .80, -.40), .012, "8f938c", "chrome", parent)
    cylinder_x(f"NivaCab_CrankKnob{side}", .013, .045, (side * .73, .80, -.40), "1d1f1d", "plastic", parent, 12, .004)
    box(f"NivaCab_LeverRecess{side}", (.008, .05, .12), (side * .760, .99, -.47), "1a1c1a", "plastic", parent, .004)
    box(f"NivaCab_Lever{side}", (.012, .014, .10), (side * .752, .99, -.47), "a2a69e", "chrome", parent, .004)
    cylinder_z(f"NivaCab_LockKnob{side}", .007, .05, (side * .77, 1.13, .38), "1d1f1d", "plastic", parent, 10)
    # Rear quarter trim behind the B-pillar.
    box(f"NivaCab_QuarterTrim{side}", (.02, .40, 1.20), (side * .772, .90, 1.10), "3a3833", "vinyl", parent, .008)


def kufic_allah(prefix, parent, z, scale, cy=0.0, color="d6ae4a"):
    """The name of God in square Kufic, the geometric script of tiles and
    medallions: alif standing apart on the right, two lams, and the ha as a
    square loop closing the baseline on the left (reads right to left)."""
    s = scale
    parts = [
        ("Base", (.022 * s, .004 * s), (-.005 * s, -.012 * s)),
        ("Alif", (.004 * s, .026 * s), (.014 * s, .001 * s)),
        ("Lam1", (.004 * s, .026 * s), (.004 * s, .001 * s)),
        ("Lam2", (.004 * s, .026 * s), (-.005 * s, .001 * s)),
        ("HaTop", (.008 * s, .003 * s), (-.0125 * s, -.0035 * s)),
        ("HaSide", (.003 * s, .010 * s), (-.0165 * s, -.0075 * s)),
    ]
    for name, (w, h), (x, y) in parts:
        # The back face is read from behind: mirror it so it still reads right to left.
        box(f"{prefix}_{name}", (w, h, .0012), (x if z > 0 else -x, cy + y, z), color, "gold", parent)


def build_charm(root):
    """Shamail medallion and a tasbih hanging from the rear-view mirror; local
    origin is the hanging point, the medallion faces the driver (+Z)."""
    for side in (-1, 1):
        tube(f"NivaCharm_Cord{side}", [(side * .012, 0, 0), (side * .004, -.12, .002), (0, -.205, 0)], .0012, "8a2020", "cloth", root, 1)
    cy = -.245
    tube("NivaCharm_Rim", [(math.cos(a) * .036, cy + math.sin(a) * .036, 0)
                                 for a in [math.tau * i / 40 for i in range(41)]], .003, "d6ae4a", "gold", root, 2)
    cylinder_z("NivaCharm_Face", .034, .004, (0, cy, 0), "1f5b3a", "enamel", root, 40)
    cylinder_z("NivaCharm_Loop", .004, .003, (0, cy + .040, 0), "d6ae4a", "gold", root, 12)
    for face in (1, -1):
        kufic_allah(f"NivaCharm_Kufic{face}", root, face * .0026, 1.1, cy)
        # Dotted ring inside the rim.
        for i in range(24):
            a = math.tau * i / 24
            box(f"NivaCharm_Dot{face}_{i}", (.0018, .0018, .001), (math.cos(a) * .029, cy + math.sin(a) * .029, face * .0026),
                "d6ae4a", "gold", root)
    # Tassel under the medallion.
    cylinder_z("NivaCharm_TasselCap", .005, .008, (0, cy - .042, 0), "d6ae4a", "gold", root, 10)
    for i in range(7):
        a = math.tau * i / 7
        beam(f"NivaCharm_Thread{i}", (math.cos(a) * .002, cy - .046, math.sin(a) * .002),
             (math.cos(a) * .006, cy - .085, math.sin(a) * .006), .0014, "1f5b3a", "cloth", root)
    # Tasbih: 33 wooden beads in a loop beside the medallion, and its tassel.
    beads = 33
    for i in range(beads):
        t = i / (beads - 1)
        a = math.pi * t
        x = .022 + .018 * math.cos(a) * 0 + (t - .5) * .03
        y = -.012 - .16 * math.sin(a)
        z = -.006 + .01 * math.cos(a)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=.0048, location=G(x, y, z))
        bead = bpy.context.active_object
        bead.name = f"NivaCharm_Bead{i}"
        bead.parent = root
        assign(bead, material("6b3f22", "wood_polished"))
    cylinder_z("NivaCharm_Imam", .006, .014, (.022, -.182, -.006), "6b3f22", "wood_polished", root, 12)
    for i in range(5):
        a = math.tau * i / 5
        beam(f"NivaCharm_BeadThread{i}", (.022, -.19, -.006), (.022 + math.cos(a) * .004, -.225, -.006 + math.sin(a) * .004),
             .0012, "2a2a2a", "cloth", root)


def build_steering(root):
    """Two-spoke VAZ wheel in a laced leather cover; rim in the local XY plane,
    face toward the driver (+Z). The runtime pivot tilts it onto the column."""
    r, t = .19, .0155
    ring = [(math.cos(a) * r, math.sin(a) * r, 0) for a in [math.tau * i / 64 for i in range(65)]]
    tube("NivaSteer_Rim", ring, t, "3b2a1f", "leather", root, 3)
    # Lacing stitched round the inner edge of the cover.
    lace = []
    for i in range(0, 361):
        a = math.tau * i / 360
        phi = a * 90
        rr = r - t * .95 * (1 + .25 * math.cos(phi))
        lace.append((math.cos(a) * rr, math.sin(a) * rr, t * .55 * math.sin(phi)))
    tube("NivaSteer_Lacing", lace, .0013, "1e1712", "leather", root, 0)
    for side in (-1, 1):
        beam(f"NivaSteer_Spoke{side}", (side * .045, -.012, -.012), (side * (r - .01), -.045, -.004), .014, "242624", "plastic", root, .038)
    box("NivaSteer_Hub", (.12, .085, .045), (0, -.018, -.018), "242624", "plastic", root, .02, 3)
    box("NivaSteer_HornPad", (.10, .065, .01), (0, -.018, .006), "1b1c1b", "plastic", root, .004)
    box("NivaSteer_Badge", (.026, .018, .003), (0, -.018, .012), CHROME, "chrome", root, .003)
    cylinder_z("NivaSteer_Boss", .03, .06, (0, -.012, -.06), "1b1c1b", "plastic", root, 16)


def build_interior(root):
    """Cabin of an old family Niva: black plastic dash around the live gauges
    and radio, laced wheel column, sheepskin on the driver's seat, rubber mats."""
    # Dash: padded top, driver binnacle with a visor, centre console, glovebox.
    box("NivaCab_DashTop", (1.54, .17, .28), (0, 1.02, -.565), "262826", "plastic", root, .035, 4)
    box("NivaCab_Binnacle", (.46, .20, .11), (-.44, 1.08, -.46), "222422", "plastic", root, .03, 3)
    box("NivaCab_BinnacleVisor", (.48, .035, .09), (-.44, 1.185, -.39), "222422", "plastic", root, .015, 3)
    box("NivaCab_GaugeWell", (.40, .145, .004), (-.44, 1.09, -.4065), "0f110f", "plastic", root)
    for i, colour in enumerate(("2f6b36", "8a6d24", "7a2a24", "2c4d7a", "2f6b36", "8a6d24")):
        box(f"NivaCab_Tell{i}", (.012, .009, .003), (-.555 + i * .023, 1.024, -.404), colour, "lamp", root)
    box("NivaCab_KneePanel", (.44, .13, .07), (-.44, .86, -.47), "242624", "plastic", root, .02, 3)
    box("NivaCab_Console", (.30, .31, .035), (.10, .93, -.39), "202220", "plastic", root, .012, 3)
    for i in range(3):
        box(f"NivaCab_HeaterSlot{i}", (.16, .006, .004), (.10, .905 - i * .022, -.371), "0d0e0d", "plastic", root)
        box(f"NivaCab_HeaterKnob{i}", (.014, .012, .012), (.06 + i * .04, .905 - i * .022, -.366), "a0a39b", "chrome", root, .003)
    box("NivaCab_Ashtray", (.13, .035, .01), (.10, .83, -.371), "2d2f2c", "plastic", root, .004)
    box("NivaCab_AshtrayPull", (.05, .006, .006), (.10, .84, -.364), "a0a39b", "chrome", root)
    cylinder_z("NivaCab_Lighter", .011, .012, (.19, .83, -.369), "1d1f1d", "plastic", root, 14)
    for x in (-.08, .52):
        box(f"NivaCab_Vent{x}", (.15, .06, .02), (x, 1.04, -.426), "141614", "plastic", root, .006)
        for i in range(4):
            box(f"NivaCab_VentSlat{x}_{i}", (.13, .004, .012), (x, 1.02 + i * .013, -.416), "3a3d38", "plastic", root)
    for x in (-.70, .70):
        cylinder_z(f"NivaCab_SideVent{x}", .032, .02, (x, 1.03, -.43), "141614", "plastic", root, 20, .005)
    box("NivaCab_Glovebox", (.34, .11, .012), (.50, .985, -.429), "2a2c2a", "plastic", root, .006)
    cylinder_z("NivaCab_GloveLock", .007, .006, (.50, 1.02, -.422), "a0a39b", "chrome", root, 12)
    tube("NivaCab_GrabHandle", [(.36, 1.10, -.43), (.38, 1.13, -.418), (.62, 1.13, -.418), (.64, 1.10, -.43)], .009,
         "1d1f1d", "plastic", root)
    box("NivaCab_Shelf", (.52, .02, .18), (.46, .80, -.53), "242624", "plastic", root, .006)
    box("NivaCab_ShelfLip", (.52, .04, .012), (.46, .815, -.44), "242624", "plastic", root, .004)
    # An embroidered napkin on the dash top by the windscreen.
    box("NivaCab_Doily", (.28, .004, .13), (.42, 1.106, -.60), "efe9d8", "cloth", root, .002, rotation=(0, 6, 0))
    for i in range(5):
        box(f"NivaCab_DoilyStitch{i}", (.25, .0016, .004), (.42, 1.1085, -.655 + i * .027), "b23a3a", "cloth", root,
            rotation=(0, 6, 0))

    # Steering column shroud, stalks and the key in the ignition.
    beam("NivaCab_Column", (-.40, .95, -.47), (-.40, 1.04, -.30), .07, "1d1f1d", "plastic", root, .08)
    for side, colour in ((-1, "1d1f1d"), (1, "1d1f1d")):
        beam(f"NivaCab_Stalk{side}", (-.40 + side * .03, 1.00, -.37), (-.40 + side * .15, 1.01, -.35), .009, colour, "plastic", root)
    cylinder_z("NivaCab_IgnitionRing", .013, .01, (-.34, .985, -.36), "a0a39b", "chrome", root, 14)
    box("NivaCab_Key", (.012, .035, .003), (-.34, .965, -.352), "b8b9b0", "chrome", root, .001)
    tube("NivaCab_KeyRing", [(-.34 + .012 * math.cos(a), .94 + .012 * math.sin(a), -.35) for a in
                             [math.tau * i / 16 for i in range(17)]], .0012, "b8b9b0", "chrome", root, 1)

    # Pedals under the dash.
    for x, pad in ((-.56, (.06, .075)), (-.45, (.07, .075)), (-.31, (.045, .10))):
        beam(f"NivaCab_PedalArm{x}", (x, .80, -.60), (x, .64, -.53), .016, "2a2c2a", "metal", root)
        box(f"NivaCab_PedalPad{x}", (pad[0], pad[1], .014), (x, .62, -.52), "1b1c1b", "rubber", root, .004, rotation=(-35, 0, 0))

    # Transmission tunnel, the three Niva levers, handbrake.
    box("NivaCab_Tunnel", (.30, .14, 1.30), (0, .56, .02), "33312d", "carpet", root, .06, 4)
    cylinder_z("NivaCab_GearBoot", .045, .03, (.015, .635, -.19), "181a18", "rubber", root, 16, .012)
    beam("NivaCab_GearLever", (.015, .63, -.19), (.02, .86, -.14), .014, "1b1c1b", "metal", root)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=.028, location=G(.02, .875, -.137))
    knob = bpy.context.active_object
    knob.name = "NivaCab_GearKnob"
    knob.parent = root
    assign(knob, material("151615", "plastic"))
    for name, a, b in (("Transfer", (.07, .63, -.06), (.09, .80, -.03)), ("DiffLock", (.11, .63, -.05), (.12, .73, -.035))):
        cylinder_z(f"NivaCab_{name}Boot", .025, .02, a, "181a18", "rubber", root, 12)
        beam(f"NivaCab_{name}Lever", a, b, .01, "1b1c1b", "metal", root)
        cylinder_z(f"NivaCab_{name}Knob", .016, .03, b, "151615", "plastic", root, 12, .006)
    beam("NivaCab_Handbrake", (0, .63, .20), (0, .74, .44), .03, "1b1c1b", "plastic", root, .045)
    cylinder_z("NivaCab_HandbrakeButton", .008, .012, (0, .745, .45), "a0a39b", "chrome", root, 10)

    # Floor: rubber mats front and rear.
    rubber_mat("NivaCab_MatDriver", -.64, -.18, -.66, -.04, root)
    rubber_mat("NivaCab_MatPassenger", .18, .64, -.66, -.04, root)
    rubber_mat("NivaCab_MatRearL", -.64, -.18, .55, .95, root)
    rubber_mat("NivaCab_MatRearR", .18, .64, .55, .95, root)

    # Seats: sheepskin on the driver's, cloth on the passenger's, rear bench.
    seat("NivaCab_SeatDriver", -.40, root, sheepskin=True)
    seat("NivaCab_SeatPassenger", .40, root)
    box("NivaCab_RearCushion", (1.26, .13, .44), (0, .69, 1.13), "302f2b", "vinyl", root, .045, 4)
    box("NivaCab_RearBack", (1.26, .46, .10), (0, .94, 1.40), "302f2b", "vinyl", root, .045, 4, rotation=(-10, 0, 0))
    box("NivaCab_RearCloth", (1.00, .012, .36), (0, .758, 1.12), "5a5046", "cloth", root, .004)
    # A folded wool blanket on the rear bench.
    box("NivaCab_Blanket", (.42, .07, .30), (.30, .79, 1.12), "7b3a2d", "wool", root, .03, 3, rotation=(0, 8, 0))
    for i in range(3):
        box(f"NivaCab_BlanketStripe{i}", (.425, .072, .02), (.30, .79, 1.02 + i * .1), "d9c9a3", "wool", root, .01,
            rotation=(0, 8, 0))

    # Door cards, headliner, visors, interior mirror, dome light, belts.
    for side in (-1, 1):
        door_card(side, root)
        tube(f"NivaCab_Belt{side}", [(side * .74, 1.52, .49), (side * .60, 1.20, .43), (side * .66, .70, .30)], .004,
             "2a2b28", "cloth", root, 1)
        box(f"NivaCab_BeltAnchor{side}", (.02, .05, .03), (side * .74, 1.52, .49), "3a3c38", "metal", root, .005)
    box("NivaCab_Headliner", (1.46, .012, 1.84), (0, 1.636, .615), "c9c3b3", "headliner", root, .006)
    for x in (-.36, .36):
        box(f"NivaCab_Visor{x}", (.36, .014, .15), (x, 1.605, -.25), "bfb9a8", "vinyl", root, .006, rotation=(-12, 0, 0))
    # A tucked card and a folded paper in the driver's visor strap.
    box("NivaCab_VisorPaper", (.14, .003, .09), (-.40, 1.595, -.245), "e8e2cf", "cloth", root, rotation=(-12, 0, 0))
    box("NivaCab_VisorStrap", (.02, .005, .13), (-.30, 1.597, -.25), "3a3833", "vinyl", root, rotation=(-12, 0, 0))
    beam("NivaCab_MirrorStem", (0, 1.63, -.33), (0, 1.585, -.345), .014, "1d1f1d", "plastic", root)
    box("NivaCab_Mirror", (.22, .065, .025), (0, 1.555, -.355), "1d1f1d", "plastic", root, .012, 3, rotation=(8, 0, 0))
    box("NivaCab_MirrorGlass", (.205, .05, .003), (0, 1.555, -.341), "9aa8a2", "glass", root, rotation=(8, 0, 0))
    box("NivaCab_DomeLight", (.12, .022, .07), (0, 1.622, .40), "d8d4c4", "plastic", root, .008)


def empty(name):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def join_children(parent: bpy.types.Object, name: str) -> bpy.types.Object:
    """Merge a group into one mesh object (fewer draw calls, stable export)."""
    children = [c for c in parent.children if c.type == "MESH"]
    for other in bpy.context.selected_objects:
        other.select_set(False)
    for child in children:
        child.select_set(True)
    bpy.context.view_layer.objects.active = children[0]
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    joined.data.name = name
    bpy.data.objects.remove(parent, do_unlink=True)
    return joined


# One material per finish where the eye cannot tell shades apart: every
# material is a separate draw call, so near-identical greys are merged.
PALETTE = {
    "rubber": ["141614", "1f2320"],
    "plastic": ["1b1d1b", "262826"],
    "metal": ["1d201d", "2e332e", "5b5f57", "8f948c"],
    "chrome": ["a0a39b", "c9cdc6"],
    "vinyl": ["302f2b", "3a3833", "bfb9a8"],
    "cloth": ["5a5046", "efe9d8", "b23a3a", "8a2020", "1f5b3a", "2a2b28"],
}


def _rgb(hex_colour):
    return tuple(int(hex_colour[i:i + 2], 16) for i in (0, 2, 4))


def consolidate_materials(obj: bpy.types.Object) -> None:
    mesh = obj.data
    unique: list[bpy.types.Material] = []
    remap = []
    for mat in mesh.materials:
        colour, surface = mat.name.split("__", 1)
        if surface in PALETTE:
            c = _rgb(colour)
            colour = min(PALETTE[surface], key=lambda p: sum((a - b) ** 2 for a, b in zip(_rgb(p), c)))
        target = material(colour, surface)
        if target not in unique:
            unique.append(target)
        remap.append(unique.index(target))
    old = [polygon.material_index for polygon in mesh.polygons]
    mesh.materials.clear()
    for mat in unique:
        mesh.materials.append(mat)
    for polygon, index in zip(mesh.polygons, old):
        polygon.material_index = remap[index]


def paint_road_grime(body: bpy.types.Object) -> None:
    """Winter road grime as a vertex colour the paint multiplies: grey-brown
    salt slush thick along the sills, thrown up behind each wheel and onto the
    tailgate by the rear vortex, fading to clean paint by the shoulder line."""
    def smooth(a, b, v):
        t = min(max((v - a) / (b - a), 0.0), 1.0)
        return t * t * (3 - 2 * t)
    mesh = body.data
    attr = mesh.color_attributes.new("Grime", "FLOAT_COLOR", "POINT")
    clean, dirty, mud = Vector((1, 1, 1)), Vector((.60, .57, .52)), Vector((.46, .40, .33))
    for index, vertex in enumerate(mesh.vertices):
        world = body.matrix_world @ vertex.co
        x, y, z = world.x, world.z, -world.y  # back to Godot axes
        low = smooth(.98, .52, y)
        arch = 0.0
        for axle in (-1.18, 1.10):
            d = math.hypot(y - .345, (z - axle) * .8)
            behind = 1.0 if z > axle else .55
            arch = max(arch, (1 - smooth(.36, .66, d)) * behind * smooth(.55, .75, abs(x)))
        tail = smooth(1.55, 1.86, z) * smooth(1.25, .70, y) * .7
        streak = .82 + .18 * math.sin(z * 23.0 + x * 7.0) * math.sin(y * 31.0)
        g = min(1.0, (.85 * low + .6 * arch + tail) * streak)
        colour = clean.lerp(dirty, g).lerp(mud, min(1.0, arch * .8 + low * low * .35))
        attr.data[index].color = (colour.x, colour.y, colour.z, 1.0)
    mesh.color_attributes.active_color = attr


def main() -> None:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    args = parser.parse_args(argv)
    root = Path(args.root)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    body = empty("NivaBody_Group")
    build_body(body)
    wheel = empty("NivaWheel_Group")
    build_wheel(wheel)
    radio = empty("NivaRadio_Group")
    build_radio(radio)
    interior = empty("NivaInterior_Group")
    build_interior(interior)
    steering = empty("NivaSteering_Group")
    build_steering(steering)
    charm = empty("NivaCharm_Group")
    build_charm(charm)
    parts = [join_children(body, "NivaBody"), join_children(wheel, "NivaWheel"), join_children(radio, "NivaRadio"),
             join_children(interior, "NivaInterior"), join_children(steering, "NivaSteering"),
             join_children(charm, "NivaCharm")]
    for part in parts:
        consolidate_materials(part)
    paint_road_grime(parts[0])
    for part in parts:
        bpy.context.view_layer.objects.active = part
        for other in bpy.context.selected_objects:
            other.select_set(False)
        part.select_set(True)
        bpy.ops.object.shade_auto_smooth(angle=math.radians(35))
        part["urman_provenance"] = "project-original procedural VAZ-2121 proportions; tools/blender/generate_niva.py"
        tris = sum(len(p.vertices) - 2 for p in part.data.polygons)
        # The runtime and the vehicle smoke compare the imported index count
        # with this authored count: no triangle may be lost on the way to Godot.
        part["urman_triangle_corners"] = tris * 3
        print(f"niva-generator: {part.name} triangles={tris} materials={len(part.data.materials)}")

    blend = root / "assets/source/blender/urman_niva.blend"
    glb = root / "game/assets/generated/urman_niva.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.export_scene.gltf(filepath=str(glb), export_format="GLB", export_yup=True,
                              export_apply=True, export_materials="EXPORT", export_animations=False,
                              export_extras=True, export_vertex_color="ACTIVE",
                              export_active_vertex_color_when_no_material=True)
    print(f"niva-generator: wrote {blend} and {glb}")


if __name__ == "__main__":
    main()
