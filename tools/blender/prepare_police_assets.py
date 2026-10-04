"""Prepare licensed static police-post assets from an explicit downloaded folder.

Run with pinned Blender: --background --python this-file -- --source DIR --root REPO.
Sources and licences are recorded in assets/third_party/police/manifest.json.
No generated car replaces a missing source. Original UVs/material maps survive.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector

args = argparse.ArgumentParser()
args.add_argument('--source', type=Path, required=True)
args.add_argument('--root', type=Path, required=True)
cfg = args.parse_args(sys.argv[sys.argv.index('--') + 1:])
out = cfg.root / 'game/assets/third_party/police'
out.mkdir(parents=True, exist_ok=True)
stats = {}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def meshes():
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def flatten_world():
    for o in meshes():
        transform = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = Matrix.Identity(4)
        o.data.transform(transform)
        # Some Collada exports include unused vertices from the opposite tyre.
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bm.to_mesh(o.data); bm.free()
    for o in list(bpy.context.scene.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)


def bounds():
    points = [v.co for o in meshes() for v in o.data.vertices]
    return Vector([min(v[i] for v in points) for i in range(3)]), Vector([max(v[i] for v in points) for i in range(3)])


def export(name, source):
    lo, hi = bounds()
    count = sum(len(p.vertices) - 2 for o in meshes() for p in o.data.polygons)
    for image in bpy.data.images:
        if image.size[0] > 1024 or image.size[1] > 1024:
            factor = 1024 / max(image.size)
            image.scale(round(image.size[0] * factor), round(image.size[1] * factor))
    path = out / (name + '.glb')
    bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', export_animations=False,
                             export_cameras=False, export_lights=False, export_image_format='AUTO')
    stats[name] = dict(source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                       runtime_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                       runtime_bytes=path.stat().st_size, triangles=count,
                       dimensions_metres=[round((hi-lo)[i], 4) for i in (0,2,1)],
                       material_count=len(bpy.data.materials),
                       texture_sizes=[list(i.size) for i in bpy.data.images])
    print('POLICE ASSET', name, stats[name])


clear()
source = cfg.source / 'vaz2106-original.glb'
bpy.ops.import_scene.gltf(filepath=str(source))
flatten_world()
lo, hi = bounds()
scale = 4.18 / (hi.y-lo.y)
centre = (lo+hi)/2
body=max((o for o in meshes() if o.data.materials[0].name=='Steel'),key=lambda o:len(o.data.polygons))
centre.x=(min(v.co.x for v in body.data.vertices)+max(v.co.x for v in body.data.vertices))*.5
for o in meshes():
    for v in o.data.vertices:
        # Front becomes Blender +Y, then Godot -Z. Floor is Y=0 in Godot.
        p=v.co.copy(); v.co=Vector((-(p.x-centre.x)*scale, -(p.y-centre.y)*scale, (p.z-lo.z)*scale))
    bpy.context.view_layer.objects.active=o
    if len(o.data.polygons)>1200:
        kind=o.data.materials[0].name
        mod=o.modifiers.new('Static prop detail reduction','DECIMATE')
        mod.ratio=.42 if kind=='Steel' else .12
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if o.data.materials[0].name=='Rubber':
        # A parked, deflated front-left tyre has a wider, low contact patch.
        for v in o.data.vertices:
            if v.co.x<-.45 and v.co.y>.8 and v.co.z<.53:
                v.co.z=max(.01, (v.co.z-.30)*.75+.245)
    o.data.update()
for mat in bpy.data.materials:
    if mat.node_tree is None:continue
    bsdf=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if bsdf is None:
        image=next((n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE'),None)
        mat.node_tree.nodes.clear()
        bsdf=mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
        output=mat.node_tree.nodes.new('ShaderNodeOutputMaterial')
        mat.node_tree.links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
        if image:
            texture=mat.node_tree.nodes.new('ShaderNodeTexImage');texture.image=image
            mat.node_tree.links.new(texture.outputs['Color'],bsdf.inputs['Base Color'])
    # The source exports opaque maps as BLEND. Correct that; only actual glass
    # uses transparency. This also avoids expensive sorting of every body part.
    for link in list(mat.node_tree.links):
        if link.to_node==bsdf and link.to_socket.name=='Alpha':mat.node_tree.links.remove(link)
    bsdf.inputs['Alpha'].default_value=.24 if mat.name=='Glass' else 1
    bsdf.inputs['Roughness'].default_value={'Glass':.12,'Chrome':.24,'Steel':.57,'Rubber':.9}.get(mat.name,.75)
    bsdf.inputs['Metallic'].default_value={'Chrome':.9,'Steel':.35,'Metal':.65,'Wheel_steel':.65}.get(mat.name,0)
    if mat.name=='Glass':mat.surface_render_method='BLENDED'
    limit=1024 if mat.name=='Steel' else 512 if mat.name in {'Seats','Leather','Plastic','Rubber','Headlights','Wheel_steel'} else 256
    for node in mat.node_tree.nodes:
        if node.type=='TEX_IMAGE' and node.image and max(node.image.size)>limit:node.image.scale(limit,limit)
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active=meshes()[0]
bpy.ops.object.join()
meshes()[0].name='VAZ2106StaticMesh'
# A thin settled snow skin follows the real roof/bonnet faces, not box slabs.
body=meshes()[0];points=[];faces=[]
for polygon in body.data.polygons:
    c=polygon.center
    if polygon.normal.z<.93:continue
    if not (c.z>1.39 and abs(c.y)<.85):continue
    face=[]
    for index in polygon.vertices:
        face.append(len(points));points.append(tuple(body.data.vertices[index].co+polygon.normal*.006))
    faces.append(face)
if faces:
    mesh=bpy.data.meshes.new('Settled snow');mesh.from_pydata(points,[],faces);mesh.update()
    skin=bpy.data.objects.new('VAZSettledSnow',mesh);bpy.context.collection.objects.link(skin)
    mat=bpy.data.materials.new('Settled winter snow');mat.use_nodes=True
    bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(.73,.77,.75,1);bsdf.inputs['Roughness'].default_value=.95
    mesh.materials.append(mat)
export('vaz2106_static',source)

for name in ['metal_office_desk','painted_wooden_bench','desk_lamp_arm_01','painted_wooden_cabinet']:
    clear(); source=cfg.source/name/(name+'_1k.gltf')
    bpy.ops.import_scene.gltf(filepath=str(source)); flatten_world()
    lo,hi=bounds(); shift=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    for o in meshes():
        for v in o.data.vertices:v.co-=shift
    for image in bpy.data.images:
        limit=1024 if name=='metal_office_desk' and 'diff' in image.name.lower() else 512
        if max(image.size)>limit:image.scale(limit,limit)
    if name=='desk_lamp_arm_01':
        # Static deployment; one small mesh, no imported animation owner.
        for o in meshes():
            if len(o.data.polygons)>1500:
                bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Static lamp detail','DECIMATE');mod.ratio=.3
                bpy.ops.object.modifier_apply(modifier=mod.name)
    export(name,source)

for name in ['chair','dining_chair']:
    clear();source=cfg.source/'OfficeSet/objfiles'/(name+'.obj')
    bpy.ops.wm.obj_import(filepath=str(source),forward_axis='NEGATIVE_Z',up_axis='Y');flatten_world()
    # Source uses inches. Preserve its UV atlas rather than a generic tint.
    lo,hi=bounds();shift=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    mat=bpy.data.materials.new('Gamekorp '+name);mat.use_nodes=True
    bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.7
    texture=mat.node_tree.nodes.new('ShaderNodeTexImage');texture.image=bpy.data.images.load(str(cfg.source/'OfficeSet/textures'/(name+'.png')))
    texture.image.scale(512,512)
    mat.node_tree.links.new(texture.outputs['Color'],bsdf.inputs['Base Color'])
    for o in meshes():
        for v in o.data.vertices:v.co=(v.co-shift)*.0254
        o.data.materials.clear();o.data.materials.append(mat)
    export('office_'+name,source)

clear();source=cfg.source/'rotary-phone-original.glb'
bpy.ops.import_scene.gltf(filepath=str(source));flatten_world()
# This source is upright. Lay it down with the dial facing upward; scale its
# handset to 24cm, without counting the extended cable in the phone's width.
handset=next(o for o in meshes() if o.name.startswith('telephone_low_'))
factor=.24/(max(v.co.x for v in handset.data.vertices)-min(v.co.x for v in handset.data.vertices))
rotation=Matrix.Rotation(-math.pi/2,4,'X')
for o in meshes():o.data.transform(rotation)
lo,hi=bounds();shift=Vector((0,(lo.y+hi.y)/2,lo.z))
for o in meshes():
    for v in o.data.vertices:v.co=(v.co-shift)*factor
    if len(o.data.polygons)>1500:
        bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Static telephone detail','DECIMATE');mod.ratio=.12
        bpy.ops.object.modifier_apply(modifier=mod.name)
for image in bpy.data.images:
    if max(image.size)>512:image.scale(512,512)
export('rotary_phone',source)
(out/'preparation_stats.json').write_text(json.dumps(stats,indent=2)+'\n')
