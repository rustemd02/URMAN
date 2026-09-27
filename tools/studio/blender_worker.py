"""URMAN Studio geometry worker (spike STUDIO.X2).

A long-lived background Blender process that Studio drives over stdin/stdout,
one JSON request per line. Replies are printed on lines prefixed with
``@@STUDIO@@`` so Blender's own log never mixes with the protocol. The author
never sees or runs this; Studio starts it hidden behind its modelling view.

Run: Blender --background --factory-startup --python tools/studio/blender_worker.py
"""
import json
import sys
import time
import traceback

import bmesh
import bpy

MARK = "@@STUDIO@@ "


def reply(payload):
    sys.stdout.write(MARK + json.dumps(payload, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def mesh_object(name):
    obj = bpy.data.objects.get(name)
    if obj is None or obj.type != "MESH":
        raise ValueError(f"no mesh object {name}")
    return obj


def with_bmesh(obj, action):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    try:
        result = action(bm)
        bm.normal_update()
        bm.to_mesh(obj.data)
        obj.data.update()
        return result
    finally:
        bm.free()


def op_open(req):
    bpy.ops.wm.open_mainfile(filepath=req["path"])
    return {"objects": len(bpy.data.objects)}


def op_primitive(req):
    mesh = bpy.data.meshes.new(req["name"])
    obj = bpy.data.objects.new(req["name"], mesh)
    bpy.context.scene.collection.objects.link(obj)
    def build(bm):
        bmesh.ops.create_cube(bm, size=req.get("size", 1.0))
    with_bmesh(obj, build)
    return {"verts": len(mesh.vertices)}


def op_extrude(req):
    obj = mesh_object(req["object"])
    def run(bm):
        bm.faces.ensure_lookup_table()
        faces = [bm.faces[i] for i in req["faces"]]
        out = bmesh.ops.extrude_face_region(bm, geom=faces)
        verts = [g for g in out["geom"] if isinstance(g, bmesh.types.BMVert)]
        normal = faces[0].normal.copy()
        bmesh.ops.translate(bm, vec=normal * req.get("distance", 0.2), verts=verts)
        return len(verts)
    return {"moved": with_bmesh(obj, run), "verts": len(obj.data.vertices)}


def op_bevel(req):
    obj = mesh_object(req["object"])
    def run(bm):
        bm.edges.ensure_lookup_table()
        edges = [bm.edges[i] for i in req["edges"]] if "edges" in req else list(bm.edges)
        bmesh.ops.bevel(bm, geom=edges, offset=req.get("width", 0.05), segments=req.get("segments", 2),
                        affect="EDGES", profile=0.5)
    with_bmesh(obj, run)
    return {"verts": len(obj.data.vertices)}


def op_cut(req):
    obj = mesh_object(req["object"])
    def run(bm):
        geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=req["point"], plane_no=req["normal"])
    with_bmesh(obj, run)
    return {"verts": len(obj.data.vertices)}


def op_symmetrize(req):
    obj = mesh_object(req["object"])
    axis = {"-X": "-X", "+X": "X", "-Y": "-Y", "+Y": "Y", "-Z": "-Z", "+Z": "Z"}[req.get("direction", "+X")]
    def run(bm):
        bmesh.ops.symmetrize(bm, input=list(bm.verts) + list(bm.edges) + list(bm.faces), direction=axis)
    with_bmesh(obj, run)
    return {"verts": len(obj.data.vertices)}


def op_normals(req):
    obj = mesh_object(req["object"])
    with_bmesh(obj, lambda bm: bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)))
    return {}


def select_only(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def op_uv(req):
    obj = mesh_object(req["object"])
    select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    return {"uvLayers": len(obj.data.uv_layers)}


def op_garment(req):
    """Clothing from a selected surface: copy faces of the body inside a height
    band, give them thickness and a small offset, and copy the body's skin
    weights so the garment follows the same skeleton."""
    body = mesh_object(req["body"])
    low, high = req["zBand"]
    data = body.data.copy()
    garment = bpy.data.objects.new(req["name"], data)
    bpy.context.scene.collection.objects.link(garment)
    garment.matrix_world = body.matrix_world.copy()
    garment.parent = body.parent
    def keep_band(bm):
        doomed = [f for f in bm.faces if not all(low <= (body.matrix_world @ v.co).z <= high for v in f.verts)]
        bmesh.ops.delete(bm, geom=doomed, context="FACES")
    with_bmesh(garment, keep_band)
    for mod in list(garment.modifiers):
        garment.modifiers.remove(mod)
    solid = garment.modifiers.new("Thickness", "SOLIDIFY")
    solid.thickness = req.get("thickness", 0.012)
    solid.offset = 1.0
    shrink = garment.modifiers.new("Fit", "SHRINKWRAP")
    shrink.target = body
    shrink.offset = req.get("offset", 0.006)
    shrink.wrap_mode = "OUTSIDE"
    transfer = garment.modifiers.new("Skin", "DATA_TRANSFER")
    transfer.object = body
    transfer.use_vert_data = True
    transfer.data_types_verts = {"VGROUP_WEIGHTS"}
    transfer.layers_vgroup_select_src = "ALL"
    transfer.layers_vgroup_select_dst = "NAME"
    select_only(garment)
    for name in ("Fit", "Thickness", "Skin"):
        bpy.ops.object.modifier_apply(modifier=name)
    if body.parent and body.parent.type == "ARMATURE":
        arm = garment.modifiers.new("Armature", "ARMATURE")
        arm.object = body.parent
    return {"verts": len(garment.data.vertices), "groups": len(garment.vertex_groups)}


def op_auto_weights(req):
    """Bind a new mesh to the URMAN template skeleton with automatic weights."""
    obj = mesh_object(req["object"])
    rig = bpy.data.objects[req["armature"]]
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    weighted = sum(1 for g in obj.vertex_groups)
    return {"groups": weighted}


def op_export(req):
    bpy.ops.object.select_all(action="DESELECT")
    for name in req["objects"]:
        bpy.data.objects[name].select_set(True)
    bpy.ops.export_scene.gltf(filepath=req["path"], use_selection=True, export_format="GLB",
                              export_skins=True, export_animations=False)
    return {"path": req["path"]}


OPS = {
    "open": op_open, "primitive": op_primitive, "extrude": op_extrude, "bevel": op_bevel,
    "cut": op_cut, "symmetrize": op_symmetrize, "normals": op_normals, "uv": op_uv,
    "garment": op_garment, "auto-weights": op_auto_weights, "export": op_export,
}


def main():
    reply({"ready": True, "blender": bpy.app.version_string})
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        req = json.loads(line)
        if req.get("op") == "quit":
            reply({"id": req.get("id"), "ok": True})
            break
        started = time.perf_counter()
        try:
            result = OPS[req["op"]](req)
            reply({"id": req.get("id"), "ok": True, "ms": round((time.perf_counter() - started) * 1000, 1), "result": result})
        except Exception as error:  # reported to Studio, never swallowed
            reply({"id": req.get("id"), "ok": False, "error": str(error), "trace": traceback.format_exc(limit=3)})


main()
