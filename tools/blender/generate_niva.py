"""Generate babay's VAZ-2121 Niva body, wheel and car radio for URMAN.

Run with Blender 4.5 LTS:
  blender --background --python tools/blender/generate_niva.py -- --root <repo>

Project-original low-poly geometry, modelled from the car's public proportions
(3.74 m long, 1.68 m wide, 2.20 m wheelbase) rather than any photograph or
third-party model. The runtime keeps its own live nodes (wheel pivots, steering
wheel, gauges, headlights and the "101.4" tuning label); this file supplies
only presentation meshes with no collision authority:

  NivaBody   hollow painted shell with real window openings, flared arches,
             chrome bumpers, round headlamps, grille, lamps, seals, mirrors
  NivaWheel  stamped steel wheel with hubcap and a rounded tyre, axle on X
  NivaRadio  a 1-DIN cassette car radio: LCD window, knobs, presets, tape door

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
        bsdf.inputs["Roughness"].default_value = 0.35 if surface in ("chrome", "glass") else 0.3 if surface == "paint" else 0.7
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


def build_body(root):
    # Lower shell: one side silhouette, the cabin top left open so the driver
    # sees real inner door panels rather than a solid block.
    profile = [(-1.93, .47), (-1.975, .66), (-1.982, .96), (-1.935, 1.030), (-1.20, 1.065), (-.80, 1.090),
               (-.70, 1.100), (1.805, 1.100), (1.842, 1.072), (1.858, .64), (1.826, .47)]
    shell = extrude_profile("NivaBody_Shell", profile, -.812, .812, PAINT, "paint", root,
                            drop_top_between=(-.70, 1.805))
    solid = shell.modifiers.new("Solidify", "SOLIDIFY")
    solid.thickness = .032
    solid.offset = -1
    bevel(shell, .03, 3, angle=35.0)
    apply_modifiers(shell)
    for axle in (-1.18, 1.10):
        cutter = cylinder_x("ArchCutter", .392, 2.2, (0, .345, axle), PAINT, "paint", None, segments=36)
        boolean_cut(shell, cutter)

    # The Niva's shoulder crease and lower body line catch the light along the side.
    for side in (-1, 1):
        box(f"NivaBody_Crease{side}", (.012, .022, 3.62), (side * .815, 1.005, -.07), PAINT, "paint", root, .005)
        box(f"NivaBody_Sill{side}", (.03, .07, 1.28), (side * .807, .49, -.10), "303830", "metal", root, .01)
        arch = arch_flare(f"NivaBody_ArchFlareFront{side}", -1.18, side, root)
        arch = arch_flare(f"NivaBody_ArchFlareRear{side}", 1.10, side, root)
        # Wheel-well liners hide the shell cavity behind each arch.
        for axle in (-1.18, 1.10):
            cylinder_x(f"NivaBody_WellLiner{side}_{axle}", .40, .30, (side * .64, .40, axle), "161a18", "rubber", root, 28)

    # Cabin tub: floor, firewall, rear parcel area.
    box("NivaBody_Floor", (1.52, .04, 2.52), (0, .48, .54), "262a26", "rubber", root)
    box("NivaBody_Firewall", (1.54, .60, .03), (0, .80, -.705), "2e332e", "metal", root)
    box("NivaBody_CargoFloor", (1.40, .05, .52), (0, .72, 1.55), "2a2f2a", "rubber", root)

    # Greenhouse: roof, pillars, drip rails and a hatch frame around real openings.
    box("NivaBody_Roof", (1.50, .05, 1.84), (0, 1.668, .615), PAINT, "paint", root, .025, 3)
    for side in (-1, 1):
        beam(f"NivaBody_APillar{side}", (side * .783, 1.10, -.70), (side * .735, 1.645, -.30), .055, PAINT, "paint", root, .05)
        beam(f"NivaBody_BPillar{side}", (side * .790, 1.10, .505), (side * .742, 1.645, .505), .085, PAINT, "paint", root, .045)
        cp = extrude_profile(f"NivaBody_CPillar{side}", [(1.36, 1.10), (1.815, 1.10), (1.545, 1.645), (1.30, 1.645)],
                             side * .745, side * .790, PAINT, "paint", root)
        bevel(cp, .012, 2)
        apply_modifiers(cp)
        beam(f"NivaBody_DripRail{side}", (side * .752, 1.652, -.30), (side * .752, 1.652, 1.53), .016, "485648", "metal", root)
        beam(f"NivaBody_BeltSeal{side}", (side * .795, 1.108, -.66), (side * .795, 1.108, 1.36), .016, RUBBER, "rubber", root, .02)
        beam(f"NivaBody_QuarterVent{side}", (side * .788, 1.12, -.52), (side * .752, 1.52, -.40), .012, RUBBER, "rubber", root)
    beam("NivaBody_Header", (-.73, 1.645, -.302), (.73, 1.645, -.302), .045, PAINT, "paint", root, .05)
    beam("NivaBody_Cowl", (-.78, 1.105, -.705), (.78, 1.105, -.705), .05, PAINT, "paint", root, .06)
    beam("NivaBody_HatchSill", (-.77, 1.10, 1.818), (.77, 1.10, 1.818), .045, PAINT, "paint", root, .05)
    beam("NivaBody_HatchTop", (-.70, 1.64, 1.545), (.70, 1.64, 1.545), .04, PAINT, "paint", root, .04)

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

    # Doors: seams, handles, mirrors, key locks.
    for side in (-1, 1):
        x = side * .815
        beam(f"NivaDoor_FrontSeam{side}", (x, .50, -.64), (x, 1.10, -.66), .006, "1a1e1b", "rubber", root, .01)
        beam(f"NivaDoor_RearSeam{side}", (x, .50, .49), (x, 1.10, .49), .006, "1a1e1b", "rubber", root, .01)
        beam(f"NivaDoor_LowerSeam{side}", (x, .50, -.64), (x, .50, .49), .006, "1a1e1b", "rubber", root, .01)
        box(f"NivaDoor_Handle{side}", (.02, .022, .13), (side * .826, 1.035, .33), CHROME, "chrome", root, .006)
        box(f"NivaDoor_HandleRecess{side}", (.006, .03, .15), (side * .818, 1.035, .33), "1c201d", "rubber", root)
        beam(f"NivaMirror_Arm{side}", (side * .80, 1.16, -.57), (side * .873, 1.215, -.53), .02, PLASTIC, "rubber", root)
        box(f"NivaMirror_Housing{side}", (.035, .10, .14), (side * .898, 1.235, -.52), PLASTIC, "rubber", root, .015)
        box(f"NivaMirror_Glass{side}", (.004, .08, .115), (side * .899, 1.235, -.453), "9aa8a2", "glass", root)
    cylinder_x("NivaBody_FuelCap", .045, .012, (.818, .96, 1.47), PAINT_DARK, "paint", root, 20)

    # Front: grille between round headlamps, chrome bumper, plate, indicators.
    box("NivaFront_GrillePanel", (.82, .19, .04), (0, .925, -1.975), "1d211e", "rubber", root, .01)
    for i in range(5):
        box(f"NivaFront_GrilleBar{i}", (.78, .012, .02), (0, .855 + i * .035, -1.99), "7d8279", "chrome", root)
    box("NivaFront_GrilleFrame", (.84, .012, .025), (0, 1.024, -1.985), CHROME, "chrome", root)
    for side in (-1, 1):
        cylinder_z(f"NivaFront_LampBezel{side}", .092, .03, (side * .56, .925, -1.985), CHROME, "chrome", root, 32, .006)
        cylinder_z(f"NivaFront_LampLens{side}", .077, .02, (side * .56, .925, -1.999), "e6ecea", "lamp", root, 32)
        box(f"NivaFront_Indicator{side}", (.11, .055, .03), (side * .64, .785, -1.975), "d9912f", "lamp", root, .01)
        box(f"NivaFront_BumperEnd{side}", (.06, .10, .22), (side * .80, .615, -1.94), CHROME, "chrome", root, .02)
        box(f"NivaFront_TowHook{side}", (.03, .04, .06), (side * .40, .52, -2.02), "2a2e2a", "metal", root)
    box("NivaFront_Bumper", (1.56, .10, .10), (0, .615, -2.020), CHROME, "chrome", root, .025, 3)
    box("NivaFront_BumperStrip", (1.40, .035, .02), (0, .615, -2.073), RUBBER, "rubber", root, .008)
    box("NivaFront_Plate", (.52, .11, .01), (0, .70, -1.99), "f2f2ec", "metal", root, .004)
    box("NivaFront_PlateBorder", (.54, .13, .006), (0, .70, -1.986), "1a1a1a", "rubber", root)
    # Hood shut lines follow the bonnet's rise from the grille to the cowl.
    beam("NivaFront_HoodSeamL", (-.70, 1.034, -1.93), (-.70, 1.094, -.74), .006, "343d34", "rubber", root, .004)
    beam("NivaFront_HoodSeamR", (.70, 1.034, -1.93), (.70, 1.094, -.74), .006, "343d34", "rubber", root, .004)

    # Rear: vertical lamp clusters, chrome bumper, plate, handle, exhaust.
    for side in (-1, 1):
        x = side * .665
        box(f"NivaRear_LampHousing{side}", (.17, .30, .03), (x, .85, 1.862), "1c201d", "rubber", root, .01)
        box(f"NivaRear_Indicator{side}", (.14, .08, .02), (x, .955, 1.874), "d9912f", "lamp", root)
        box(f"NivaRear_Tail{side}", (.14, .11, .02), (x, .855, 1.874), "a12b22", "lamp", root)
        box(f"NivaRear_Reverse{side}", (.14, .06, .02), (x, .755, 1.874), "e9ece4", "lamp", root)
        box(f"NivaRear_BumperEnd{side}", (.06, .10, .20), (side * .80, .62, 1.80), CHROME, "chrome", root, .02)
    box("NivaRear_Bumper", (1.56, .10, .10), (0, .62, 1.905), CHROME, "chrome", root, .025, 3)
    box("NivaRear_BumperStrip", (1.40, .035, .02), (0, .62, 1.958), RUBBER, "rubber", root, .008)
    box("NivaRear_Plate", (.52, .11, .01), (0, .79, 1.87), "f2f2ec", "metal", root, .004)
    box("NivaRear_Handle", (.18, .025, .03), (0, 1.03, 1.855), CHROME, "chrome", root, .008)
    cylinder_z("NivaRear_Exhaust", .025, .16, (-.52, .47, 1.84), "3a3a36", "metal", root, 16)


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
    parts = [join_children(body, "NivaBody"), join_children(wheel, "NivaWheel"), join_children(radio, "NivaRadio")]
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
                              export_extras=True)
    print(f"niva-generator: wrote {blend} and {glb}")


if __name__ == "__main__":
    main()
