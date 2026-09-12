"""Prepare CC0 Quaternius head derivatives; no source rig or body is exported."""

import argparse, hashlib, json, sys, tempfile
import bpy, bmesh
from pathlib import Path
from mathutils import Matrix, Vector

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument(
    "--source",
    type=Path,
    required=True,
    help="Unpacked Base Characters/Godot - UE directory",
)
parser.add_argument(
    "--output", type=Path, required=True, help="Derived .blend source path"
)
args = parser.parse_args(
    sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
)
source = args.source.resolve()
args.output.parent.mkdir(parents=True, exist_ok=True)


def import_source(gltf):
    before = set(bpy.context.scene.objects)
    # Some official Standard URIs contain an extra _png suffix. Resolve the
    # supplied sibling file in a temporary import document; keep originals intact.
    payload = json.loads(gltf.read_text())
    for resource in payload.get("images", []) + payload.get("buffers", []):
        uri = resource.get("uri", "")
        if not uri or uri.startswith("data:"):
            continue
        file = gltf.parent / uri
        if not file.exists() and file.stem.endswith("_png"):
            file = file.with_name(file.stem[:-4] + file.suffix)
        if not file.is_file():
            raise FileNotFoundError(file)
        resource["uri"] = str(file.resolve())
    with tempfile.TemporaryDirectory(prefix="urman-head-import-") as temporary:
        document = Path(temporary) / gltf.name
        document.write_text(json.dumps(payload))
        bpy.ops.import_scene.gltf(filepath=str(document))
    return set(bpy.context.scene.objects) - before


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
keep = []
for gender, center, cut in [("Female", 1.64, 1.49), ("Male", 1.68, 1.57)]:
    gltf = source / f"Superhero_{gender}_FullBody.gltf"
    imported = import_source(gltf)
    for ob in sorted(imported, key=lambda item: item.name):
        if ob.type != "MESH" or ob.name.startswith("Icosphere"):
            continue
        part = (
            "FaceBrows"
            if ob.name.startswith("Eyebrows")
            else "FaceEyes"
            if ob.name.startswith("Eyes")
            else "Head"
        )
        bpy.ops.object.select_all(action="DESELECT")
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        for m in list(ob.modifiers):
            bpy.ops.object.modifier_apply(modifier=m.name)
        matrix = ob.matrix_world.copy()
        ob.parent = None
        ob.matrix_world = Matrix.Identity(4)
        for v in ob.data.vertices:
            v.co = matrix @ v.co
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00001)
        if part == "Head":
            bmesh.ops.bisect_plane(
                bm,
                geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                dist=0.000001,
                plane_co=(0, 0, cut),
                plane_no=(0, 0, 1),
                clear_inner=True,
            )
            border = [
                e
                for e in bm.edges
                if e.is_boundary and all(abs(v.co.z - cut) < 0.00001 for v in e.verts)
            ]
            if border:
                bmesh.ops.holes_fill(bm, edges=border, sides=0)
        bm.to_mesh(ob.data)
        bm.free()
        ob.data.calc_loop_triangles()
        budget = {"Head": 900, "FaceEyes": 192, "FaceBrows": 80}[part]
        dec = ob.modifiers.new("HeadBudget", "DECIMATE")
        dec.ratio = min(1, budget / len(ob.data.loop_triangles))
        dec.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=dec.name)
        for v in ob.data.vertices:
            v.co = Vector((v.co.x * 1.4, v.co.y * 1.4, (v.co.z - center) * 1.4))
            if gender == "Male" and v.co.z < -0.105:
                v.co.z = -0.105 + (v.co.z + 0.105) * 2.15
                if part == "Head" and v.co.y > 0:
                    # Fit the posterior neck into the winter collar instead
                    # of stretching the source shoulder flare behind the head.
                    taper = min(1.0, (-v.co.z - 0.105) / 0.105)
                    taper = taper * taper * (3.0 - 2.0 * taper)
                    taper *= min(1.0, v.co.y / 0.151)
                    v.co.x *= 1.0 - 0.35 * taper
                    v.co.y *= 1.0 - 0.55 * taper
        for f in ob.data.polygons:
            f.use_smooth = True
        ob.vertex_groups.clear()
        ob.name = f"Quaternius{gender}_{part}"
        ob.data.name = ob.name + "Mesh"
        ob["license"] = "CC0-1.0"
        ob["source_author"] = "Quaternius"
        ob["source_url"] = "https://quaternius.com/packs/universalbasecharacters.html"
        ob["source_sha256"] = hashlib.sha256(gltf.read_bytes()).hexdigest()
        ob["derivation"] = (
            "Head only; seam vertices merged, neck cut, decimated, normalized -Y forward/Z up; no source body or rig."
        )
        for mat in ob.data.materials:
            mat.name = f"Quaternius{gender}_{part}Material"
            bsdf = mat.node_tree.nodes.get("Principled BSDF")
            albedo_name = (
                (
                    f"T_Superhero_{gender}_Dark_BaseColor.png"
                    if gender == "Female"
                    else "T_Superhero_Male_Dark.png"
                )
                if part == "Head"
                else "T_Eye_Brown.png"
                if part == "FaceEyes"
                else None
            )
            image = (
                bpy.data.images.load(
                    str(source / albedo_name), check_existing=True
                ).copy()
                if albedo_name
                else None
            )
            if image:
                image.name = f"Quaternius{gender}_{part}Albedo"
                image.scale(
                    1024 if part == "Head" else 256, 1024 if part == "Head" else 256
                )
                image.pack()
            for n in list(mat.node_tree.nodes):
                if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
                    mat.node_tree.nodes.remove(n)
            bsdf.inputs["Roughness"].default_value = 1
            bsdf.inputs["Metallic"].default_value = 0
            bsdf.inputs["Specular IOR Level"].default_value = 0
            if image:
                n = mat.node_tree.nodes.new("ShaderNodeTexImage")
                n.image = image
                mat.node_tree.links.new(n.outputs["Color"], bsdf.inputs["Base Color"])
                normal_name = (
                    f"T_Superhero_{gender}_Normal.png"
                    if part == "Head"
                    else "T_Eye_Normal.png"
                )
                normal = bpy.data.images.load(
                    str(source / normal_name), check_existing=True
                ).copy()
                normal.name = f"Quaternius{gender}_{part}Normal"
                normal.colorspace_settings.name = "Non-Color"
                normal.scale(
                    512 if part == "Head" else 256, 512 if part == "Head" else 256
                )
                normal.pack()
                n = mat.node_tree.nodes.new("ShaderNodeTexImage")
                n.image = normal
                normal_node = mat.node_tree.nodes.new("ShaderNodeNormalMap")
                mat.node_tree.links.new(n.outputs["Color"], normal_node.inputs["Color"])
                mat.node_tree.links.new(
                    normal_node.outputs["Normal"], bsdf.inputs["Normal"]
                )
            else:
                bsdf.inputs["Base Color"].default_value = (0.055, 0.035, 0.025, 1)
        bpy.context.view_layer.update()
        ob.data.calc_loop_triangles()
        print(
            "TEMPLATE",
            ob.name,
            len(ob.data.loop_triangles),
            tuple(round(x, 4) for x in ob.dimensions),
        )
        keep.append(ob)
    for ob in sorted(imported, key=lambda item: item.name):
        if ob not in keep:
            bpy.data.objects.remove(ob, do_unlink=True)
hair_source = (
    source.parent.parent / "Hairstyles/Rigged to Head Bone/glTF (Godot -Unreal)"
)
hair_material = bpy.data.materials.new("QuaterniusHairGeometry")
hair_material.diffuse_color = (0.09, 0.055, 0.035, 1)
for style, center, budget in [
    ("Long", 1.64, 600),
    ("Buns", 1.64, 500),
    ("SimpleParted", 1.68, 400),
    ("Buzzed", 1.68, 250),
    ("Beard", 1.68, 250),
]:
    gltf = hair_source / f"Hair_{style}.gltf"
    imported = import_source(gltf)
    for ob in sorted(imported, key=lambda item: item.name):
        if ob.type != "MESH" or ob.name.startswith("Icosphere"):
            continue
        bpy.ops.object.select_all(action="DESELECT")
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        for mod in list(ob.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        matrix = ob.matrix_world.copy()
        ob.parent = None
        ob.matrix_world = Matrix.Identity(4)
        for v in ob.data.vertices:
            v.co = matrix @ v.co
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00001)
        bm.to_mesh(ob.data)
        bm.free()
        ob.data.calc_loop_triangles()
        dec = ob.modifiers.new("HairBudget", "DECIMATE")
        dec.ratio = min(1, budget / len(ob.data.loop_triangles))
        dec.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=dec.name)
        for v in ob.data.vertices:
            v.co = Vector((v.co.x * 1.4, v.co.y * 1.4, (v.co.z - center) * 1.4))
        if style == "Buns":
            # Keep the authored scalp and fringe, but gather the two side buns into
            # one ordinary back bun for Gulsina and Naila's existing restrained roles.
            bm = bmesh.new()
            bm.from_mesh(ob.data)
            unseen = set(bm.verts)
            buns = []
            while unseen:
                todo = [next(iter(unseen))]
                island = set()
                while todo:
                    vertex = todo.pop()
                    if vertex in island:
                        continue
                    island.add(vertex)
                    unseen.discard(vertex)
                    todo.extend(
                        edge.other_vert(vertex)
                        for edge in vertex.link_edges
                        if edge.other_vert(vertex) not in island
                    )
                center_point = sum((v.co for v in island), Vector()) / len(island)
                if abs(center_point.x) > 0.13 and center_point.y > 0.08:
                    buns.append((center_point, island))
            if len(buns) != 2:
                raise RuntimeError(
                    "Source Hair_Buns no longer contains the two expected side islands"
                )
            for center_point, island in buns:
                if center_point.x < 0:
                    bmesh.ops.delete(bm, geom=list(island), context="VERTS")
                else:
                    for vertex in island:
                        vertex.co = (vertex.co - center_point) * 1.10 + Vector(
                            (0, 0.185, 0.065)
                        )
            bm.to_mesh(ob.data)
            bm.free()
        for face in ob.data.polygons:
            face.use_smooth = True
        ob.vertex_groups.clear()
        ob.data.materials.clear()
        ob.data.materials.append(hair_material)
        ob.name = f"Quaternius_Hair{style}"
        ob.data.name = ob.name + "Mesh"
        ob["license"] = "CC0-1.0"
        ob["source_author"] = "Quaternius"
        ob["source_url"] = "https://quaternius.com/packs/universalbasecharacters.html"
        ob["source_sha256"] = hashlib.sha256(gltf.read_bytes()).hexdigest()
        ob["derivation"] = (
            "Head hair only; seam vertices merged, decimated, normalized -Y forward/Z up; source rig removed."
        )
        keep.append(ob)
        ob.data.calc_loop_triangles()
        print("TEMPLATE", ob.name, len(ob.data.loop_triangles))
    for ob in imported:
        if ob not in keep:
            bpy.data.objects.remove(ob, do_unlink=True)
for ob in list(bpy.context.scene.objects):
    if ob not in keep:
        bpy.data.objects.remove(ob, do_unlink=True)
for data in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images):
    for block in list(data):
        if block.users == 0:
            data.remove(block)
bpy.context.scene["license"] = "CC0-1.0"
bpy.context.scene["source_author"] = "Quaternius"
bpy.context.scene["source_archive_sha256"] = (
    "fdbf1804c90dfc1ea03e992bff7da2dfd1a79318e13270a660180f9308455f40"
)
bpy.ops.wm.save_as_mainfile(filepath=str(args.output.resolve()))
