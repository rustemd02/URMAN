"""Reconstruct one current C# rough-picket bay and open cart gate, source only."""
from pathlib import Path
import hashlib, importlib.util, json, math, sys, traceback
sys.dont_write_bytecode = True
import bpy
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
KIT = HERE.parent/"urman_village_fence_kit.py"
ATLAS = HERE.parent/"neighbor_house_a_retexture_v2/prepare_variant_a_retexture.py"
STUDIO = HERE.parent/"hero_house_pbr_v1/attach_hero_house_retexture.py"
RUNTIME = ROOT/"game/scripts/Act1ConnectedWorld.YardFences.cs"
PLOT = ROOT/"game/content/world/act1_open_part.world.v1.json"
GLB = HERE/"rough-picket-family-source.glb"
BLEND = HERE/"rough-picket-family-source.blend"
RECEIPT = HERE/"source-receipt.json"
report = {"schema":"rough-picket-current-family-source/v3", "status":"preparing"}


def load(name, path):
    spec=importlib.util.spec_from_file_location(name,path)
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value)
    return value


k=load("existing_fence_primitives",KIT)
p=load("existing_single_atlas",ATLAS);p.HERE=HERE
h=p.h
a=load("existing_source_studio",STUDIO)
# Runtime Y-up → Blender Z-up, one proper rotation, no scale.
C=Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
meshes=[]
members={}


def save(): RECEIPT.write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n")


def rotation(axis, angle): return Matrix.Rotation(angle,3,axis)


def transform(basis, position):
    value=basis.to_4x4();value.translation=Vector(position);return value


def empty(name, parent, location=(0,0,0)):
    ob=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(ob)
    ob.parent=parent;ob.location=location
    ob["component_root"]=name;ob["license"]="Project-original"
    return ob


def box(name, owner, assembly, size, at, basis=None, colour="a8854a", surface="wood_painted_trim", role="timber"):
    basis=basis if basis is not None else Matrix.Identity(3)
    material="Source_"+colour+"_"+surface
    # Same member envelope as C#. Existing primitive's 2 mm arris is disclosed.
    # Existing box_object's ring construction, kept local because its UV loop
    # reads MeshLoop.polygon_index (not a Blender 4.5 RNA property). Walk actual
    # polygon loop_indices instead. No shared helper file is changed.
    sx,sy,sz=(float(v)/2 for v in size);c=min(.002,sx*.45,sy*.45,sz*.45)
    ring=[(-sx+c,-sy),(sx-c,-sy),(sx,-sy+c),(sx,sy-c),(sx-c,sy),(-sx+c,sy),(-sx,sy-c),(-sx,-sy+c)]
    vertices=[(x,y,-sz) for x,y in ring]+[(x,y,sz) for x,y in ring]
    faces=[tuple(reversed(range(8))),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    mesh=bpy.data.meshes.new(name+"Mesh");mesh.from_pydata(vertices,[],faces);mesh.update()
    mesh.materials.append(k.material(material));uv=mesh.uv_layers.new(name='UVMap')
    for face in mesh.polygons:
        for li in face.loop_indices:
            point=mesh.vertices[mesh.loops[li].vertex_index].co
            uv.data[li].uv=(point.x,point.y) if abs(face.normal.z)>.5 else (point.y,point.z) if abs(face.normal.x)>.5 else (point.x,point.z)
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);ob.parent=owner
    ob.matrix_world=assembly.matrix_world@C@transform(basis,at)
    ob["source_runtime_dimensions_m"]=list(size)
    members[name]={"role":role,"runtime_size_m":list(size),"runtime_position_in_assembly":list(at),
                   "runtime_basis":[list(row) for row in basis],"owner":owner.name,"surface":surface,"colour":colour,
                   "arris_chamfer_m":.002}
    meshes.append(ob)
    return ob


def colour_materials():
    for ob in meshes:
        record=members[ob.name];colour=record["colour"]
        rgb=[int(colour[i:i+2],16)/255 for i in (0,2,4)]
        linear=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb]
        mat=ob.data.materials[0];mat.use_nodes=True
        mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(*linear,1)
        mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value=.85


def build_bay(root, colour):
    bay=empty("RoughPicket_Bay235",root,(-5,0,0))
    length,height,foot,lean=2.35,1.18,-.015,math.radians(1.2)
    basis=rotation("Y",math.pi/2)
    def point(x,y): return Vector((x+y*math.tan(lean),foot+y,0))
    rise=height+.11-foot
    for i,x in enumerate((0,length)):
        box(f"Bay_Post_{i}",bay,bay,(.12,rise/math.cos(lean),.12),point(x,rise*.5),basis@rotation("X",lean),colour,role="post seated 15 mm into flat datum")
        box(f"Bay_PostCap_{i}",bay,bay,(.16,.045,.16),point(x,rise+.0225),basis,"e6dfc6",role="post cap")
    for i,y in enumerate((.27,.95)):
        first,last=point(0,y),point(length,y)
        box(f"Bay_BearingRail_{i}",bay,bay,(.045,.075,(last-first).length),(first+last)*.5,basis,colour,role="bearing rail")
    count=math.floor(length/.17)
    assert count==13
    for i in range(count):
        x=(i+.5)*length/count;top=height-(i*7%3)*.025;bottom=.27+.075*.5+.01
        box(f"Bay_Picket_{i:02d}",bay,bay,(.028,top-bottom,.095),(x,(top+bottom)*.5,0),basis,colour,role="separate vertical picket")
        box(f"Bay_PointedCrown_{i:02d}",bay,bay,(.035,.068,.068),(x,top,0),basis@rotation("X",math.pi/4),colour,role="current C# pointed crown block")
    return {"bay_length_m":length,"picket_count":count,"picket_width_m":.095,"picket_thickness_m":.028,
            "picket_pitch_m":length/count,"clear_gap_at_0_7m":length/count-.095,"nominal_board_crown_centres_m":height,
            "crown_pattern_seed_mod5":0,"pattern_note":"One explicit reusable phase of the runtime three-step profile, not a captured ADR-H015 run seed/index.",
            "post_lean_degrees":1.2,"ground_datum_m":0,"post_foot_m":foot,"rail_heights_above_post_foot_m":[.27,.95]}


def build_gate(root, colour):
    gate=empty("RoughPicket_OpenCartGate",root)
    right,back,up=Vector((1,0,0)),Vector((0,0,1)),Vector((0,1,0))
    basis=Matrix((right,up,-back)).transposed()
    leafbasis=Matrix((back,up,right)).transposed()
    pivots=[]
    for index,sign in enumerate((-1,1)):
        post=right*(sign*1.72)
        box(f"Gate_Post_{index}",gate,gate,(.17,2.415,.17),post+up*1.1925,basis,colour,role="cart-gate post seated 15 mm into datum")
        box(f"Gate_PostCap_{index}",gate,gate,(.23,.055,.23),post+up*2.42,basis,"eadcc0",role="cart-gate post cap")
        pivot=empty(f"CartLeaf_{index}_StaticAttachmentPivot",gate)
        pivot.matrix_world=gate.matrix_world@C@transform(leafbasis,post)
        pivot["role"]="Source semantic attachment pivot only; runtime cart leaf is baked/static and has no hinge controller"
        pivot["source_open_pose_degrees"]=90.0
        pivots.append({"name":pivot.name,"runtime_post_position":list(post),"runtime_open_basis":[list(row) for row in leafbasis]})
        leaf=post+back*.65
        for j,y in enumerate((.40,1.38)):
            box(f"CartLeaf_{index}_BearingRail_{j}",pivot,gate,(1.30,.09,.065),leaf+up*y,leafbasis,colour,role="open leaf bearing rail")
        for j in range(9):
            box(f"CartLeaf_{index}_Board_{j:02d}",pivot,gate,(.105,1.48,.04),post+back*(.09+j*.145)+up*.82,leafbasis,colour,role="cart leaf board")
        box(f"CartLeaf_{index}_Brace",pivot,gate,(1.55,.085,.05),leaf+up*.89,leafbasis@rotation("Z",-math.atan2(.98,1.30)),"6f5b45","wood_fence","current C# diagonal brace")
        for j,y in enumerate((.46,1.44)):
            box(f"CartLeaf_{index}_HingeStrap_{j}",pivot,gate,(.02,.07,.34),post+up*y+back*.02,leafbasis,"4a4d4a","metal","iron hinge strap visual")
            box(f"CartLeaf_{index}_HingeWrap_{j}",pivot,gate,(.022,.055,.09),post+up*y+right*(-sign*.075),basis,"4a4d4a","metal","iron post wrap visual")
        box(f"CartLeaf_{index}_Handle",pivot,gate,(.045,.19,.045),post+back*1.28+up*1.06+right*(-sign*.10),basis,"4a4d4a","metal","handle visual")
    box("Gate_Header",gate,gate,(3.6,.14,.19),(0,2.32,0),basis,colour,role="cart passage header")
    box("Gate_HeaderCap",gate,gate,(3.72,.065,.25),(0,2.44,0),basis,"eadcc0",role="header cap")
    for i in range(7):
        angle=(i-3)*.24
        box(f"Gate_FanBar_{i}",gate,gate,(.035,.40,.04),up*2.63+right*(math.sin(angle)*.20),basis@rotation("Z",angle),"eadcc0",role="current C# restrained capka fan bar")
    return {"post_centres_m":[-1.72,1.72],"post_section_m":.17,"clear_width_between_post_inner_faces_m":3.27,
            "header_lower_face_m":2.25,"leaf_width_m":1.30,"leaf_board_count_each":9,"leaf_board_runtime_size_m":[.105,1.48,.04],
            "leaves_pose":"two static leaves folded 90 degrees into yard, matching BuildTimberStreetGate",
            "semantic_attachment_pivots":pivots,"collision_owner":"Existing runtime YardFences/YardFenceBody. Two OpenGateLeaf BoxShape3D [1.3,1.55,.10] at leaf+Up*.80; not exported or replaced.",
            "interaction_owner":"No current interactive cart-gate/hinge controller; source pivots do not invent one."}


def main():
    assert bpy.app.version[:3]==(4,5,12)
    assert not any(path.exists() for path in (GLB,BLEND,RECEIPT))
    plot=json.loads(PLOT.read_text());lot=next(x for x in plot['lots'] if x['id']=='ADR-H015')
    assert lot['size']==[10.0,18.0]
    report.update({"source_kind":"Reconstructed from current C# family measurements, NOT captured runtime geometry or a native game frame",
                   "rights":"Project-original source primitives and project C#; no external materials/generation or new public license",
                   "dependencies":[{"path":str(path.relative_to(ROOT)),"sha256":h.sha256_file(path)} for path in (KIT,ATLAS,STUDIO,RUNTIME,PLOT)],
                   "actual_private_lot":lot,"family":"rough-picket","scope":"One reusable nominal bay plus current open cart-gate family; no wicket fiction, snow, landscape, paid services, runtime or collision changes",
                   "parity_limits":["Flat zero ground datum replaces actual terrain SupportBase", "Nominal 2.35 m bay, not terrain/run-length placement", "Crown profile phase explicitly zero; actual run seed requires runtime run ordering", "Existing source primitive adds a disclosed 2 mm arris within the unchanged member envelope", "Existing fencekit box ring reused locally with polygon.loop_indices UV traversal replacing unsupported MeshLoop.polygon_index; shared helper unchanged", "Separate source members/pivots reconstruct current batched C# visuals; they are not current runtime nodes"],
                   "blender":bpy.app.version_string})
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root=empty("URMAN_RoughPicketFamilySourceV3",None)
    # Current lot colour selection from the exact runtime hash rules.
    def stable(text):
        value=2166136261
        for ch in text:value=((value^ord(ch))*16777619)&0xffffffff
        return value
    base=["5f7d5a","4f6f8c","a8854a","8a8f86","6b8a7a","7d5a4a"]
    painted=["c9874a","7f9a74","8fb0c4","dcd6c8"]
    colour=base[(stable(lot['id'])>>8)%len(base)]
    if stable(lot['id']+":painted")%100<45:colour=painted[(stable(lot['id']+":painted-colour")>>8)%len(painted)]
    report['source_colour']=colour
    report['bay']=build_bay(root,colour);report['cart_gate']=build_gate(root,colour)
    colour_materials();bpy.context.view_layer.update()
    before={ob.name:p.triangle_signature(ob) for ob in meshes}
    records={ob.name:{"source_uv":h.uv_layer_summary(ob.data)} for ob in meshes}
    atlas=p.build_constructive_atlas(meshes,records)
    atlas['method']='One existing Smart Project66/global Pack pass for this source specimen; no cap/crown mesh assumptions'
    atlas['actual_overlap_selected_faces']=p.actual_uv_overlap_count(meshes)
    atlas['overlap_details']=list(p.OVERLAP_DETAILS)
    assert all(before[ob.name]==p.triangle_signature(ob) for ob in meshes)
    report.update({"members":members,"member_count":len(meshes),"source_triangles":sum(h.mesh_triangles(ob.data) for ob in meshes),"uv_atlas":atlas,"per_node_uv":records,"phase":"export"})
    save()
    expected={ob.name:(p.triangle_signature(ob),p.triangle_uv_values(ob),h.matrix_rows(ob.matrix_world)) for ob in meshes}
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    bpy.ops.object.select_all(action='DESELECT')
    for ob in [root,*root.children_recursive]:ob.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.gltf(filepath=str(GLB),export_format='GLB',use_selection=True,export_apply=False,export_texcoords=True,export_normals=True,export_materials='EXPORT')
    root_name=root.name
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(GLB))
    root=bpy.data.objects[root_name];actual=[ob for ob in root.children_recursive if ob.type=='MESH']
    assert {ob.name for ob in actual}==set(expected)
    maxuv,maxmatrix=0.0,0.0
    for ob in actual:
        geometry,uv,matrix=expected[ob.name];assert geometry==p.triangle_signature(ob),ob.name
        actualuv=p.triangle_uv_values(ob);assert len(uv)==len(actualuv)
        maxuv=max(maxuv,max(abs(x-y) for x,y in zip(uv,actualuv)))
        maxmatrix=max(maxmatrix,max(abs(x-y) for first,second in zip(matrix,h.matrix_rows(ob.matrix_world)) for x,y in zip(first,second)))
    afteroverlap=p.actual_uv_overlap_count(actual)
    report['roundtrip']={"semantic_geometry_equal":True,"maximum_uv_error":maxuv,"maximum_transform_error":maxmatrix,"actual_overlap_selected_faces":afteroverlap,"byte_identical_indices_claimed":False}
    report['outputs']=[{"path":path.name,"sha256":h.sha256_file(path),"bytes":path.stat().st_size} for path in (GLB,BLEND)]
    scene=bpy.context.scene;bounds=a.object_bounds(actual);low,high=map(Vector,bounds);focus=(low+high)*.5
    camera=a.create_studio(scene,focus,bounds)
    ground=bpy.data.objects['PBRReview_Ground']
    for vertex in ground.data.vertices:vertex.co.z=0
    scene.render.resolution_x,scene.render.resolution_y=1440,960;scene.cycles.samples=24;scene.render.threads_mode='FIXED';scene.render.threads=4
    width=max(high-low)*1.22
    views=[('actual-source-front',focus,Vector((0,1,.18)),width),('actual-source-front-right',focus,Vector((1,1,.30)),width),('actual-source-rear',focus,Vector((0,-1,.18)),width),('actual-source-side',focus,Vector((-1,-1,.35)),width),('actual-source-cart-opening',Vector((0,-.35,1.25)),Vector((.25,1,.18)),4.2)]
    report['actual_source_views']=[]
    for name,target,direction,scale in views:
        path=HERE/(name+'.png');a.render_view(scene,camera,target,direction,scale,path)
        report['actual_source_views'].append({"path":path.name,"sha256":h.sha256_file(path),"bytes":path.stat().st_size,"target":list(target),"direction":list(direction),"orthographic_scale":scale})
    gates={"single_pack_overlap_zero":atlas['actual_overlap_selected_faces']==0 and afteroverlap==0,"uv_roundtrip":maxuv<=1e-6,"transform_roundtrip":maxmatrix<=1e-5}
    report['technical_gates']=gates;report['status']='source_specimen_parent_review_required' if all(gates.values()) else 'source_specimen_technical_hold_stop'
    report['remaining']='Root personally accepts source specimen before any new ImageGen or Meshy. No runtime/native/art/performance acceptance claimed. Stop on any failed gate; no micro iterations.'
    save();print('ROUGH_PICKET_SOURCE',json.dumps({"status":report['status'],"meshes":len(actual),"triangles":report['source_triangles'],"gates":gates}),flush=True)


if __name__=='__main__':
    try:main()
    except Exception as error:
        report.update({"status":"source_preparation_failed_stop","failure":repr(error),"traceback":traceback.format_exc()});save();raise
