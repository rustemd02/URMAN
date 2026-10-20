"""Attach returned PBR maps to the exact V2 semantic donor, never its geometry."""
from pathlib import Path
import copy,collections,hashlib,importlib.util,json,math,struct,sys
import bpy
from mathutils import Vector

sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
SOURCE=HERE/"variant-a-opaque-for-retexture.glb"
RETURNED=HERE/"meshy-retexture-returned.glb"
RECEIPT=HERE/"candidate-receipt.json"
OUT=HERE/"pbr_candidate_v2"
GLB=OUT/"variant-a-semantic-pbr-v2.glb"


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    loaded=importlib.util.module_from_spec(spec);spec.loader.exec_module(loaded)
    return loaded


a=module("existing_hero_attachment",HERE.parent/"hero_house_pbr_v1/attach_hero_house_retexture.py")
p=module("existing_variant_a_preparation",HERE/"prepare_variant_a_retexture.py")


def save_review(review):
    data=json.loads(RECEIPT.read_text())
    data["pbr_attachment"]=review
    RECEIPT.write_text(json.dumps(data,ensure_ascii=False,indent=2)+"\n")


def missing_uv_rows(objects,returned_counter):
    remaining=returned_counter.copy();rows=[]
    for ob in objects:
        ob.data.calc_loop_triangles()
        uv=ob.data.uv_layers.active
        for tri in ob.data.loop_triangles:
            coords=[tuple(float(c) for c in uv.data[li].uv) for li in tri.loops]
            key=tuple(sorted(a.uv_key(point) for point in coords))
            if remaining[key]:remaining[key]-=1;continue
            area=abs((coords[1][0]-coords[0][0])*(coords[2][1]-coords[0][1])-
                     (coords[1][1]-coords[0][1])*(coords[2][0]-coords[0][0]))*.5
            xyz=[tuple(float(c) for c in ob.matrix_world@ob.data.vertices[index].co) for index in tri.vertices]
            ab=[xyz[1][i]-xyz[0][i] for i in range(3)];ac=[xyz[2][i]-xyz[0][i] for i in range(3)]
            cross=(ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0])
            rows.append({"node":ob.name,"polygon":tri.polygon_index,"uv":coords,
                         "uv_area":area,"uv_area_pixels_4096":area*4096**2,
                         "world_3d_area":math.hypot(*cross)*.5,"world_3d_edge_lengths":[math.dist(xyz[i],xyz[(i+1)%3]) for i in range(3)]})
    return rows


def glb_binary(path):
    raw=path.read_bytes()
    assert struct.unpack_from("<III",raw)==(0x46546C67,2,len(raw))
    offset=12
    while offset<len(raw):
        size,kind=struct.unpack_from("<II",raw,offset);offset+=8
        payload=raw[offset:offset+size];offset+=size
        if kind==0x004E4942:return payload
    raise RuntimeError("GLB BIN chunk missing")


def attach_material_bytes(source,returned):
    """Use the existing Aidar writer pattern, retaining the entire source BIN."""
    output=copy.deepcopy(source)
    binary=bytearray(glb_binary(SOURCE))
    source_binary=bytes(binary)
    returned_binary=glb_binary(RETURNED)
    assert len(source["buffers"])==len(returned["buffers"])==1
    assert "uri" not in source["buffers"][0] and "uri" not in returned["buffers"][0]
    image_base=len(output.get("images",[]))
    sampler_base=len(output.get("samplers",[]))
    texture_base=len(output.get("textures",[]))
    material_index=len(output.get("materials",[]))
    image_rows=[]
    for image in returned["images"]:
        assert "bufferView" in image and "uri" not in image
        view=returned["bufferViews"][image["bufferView"]]
        assert view["buffer"]==0
        payload=returned_binary[view.get("byteOffset",0):view.get("byteOffset",0)+view["byteLength"]]
        assert len(payload)==view["byteLength"]
        binary.extend(b"\0"*((-len(binary))%4))
        new_view={"buffer":0,"byteOffset":len(binary),"byteLength":len(payload)}
        new_image=copy.deepcopy(image);new_image["bufferView"]=len(output["bufferViews"])
        output["bufferViews"].append(new_view)
        output.setdefault("images",[]).append(new_image)
        binary.extend(payload)
        image_rows.append({"source_image_index":len(image_rows),"output_buffer_view":new_image["bufferView"],
                           "mime_type":image["mimeType"],"bytes":len(payload),"sha256":hashlib.sha256(payload).hexdigest()})
    output.setdefault("samplers",[]).extend(copy.deepcopy(returned.get("samplers",[])))
    for texture in returned["textures"]:
        new_texture=copy.deepcopy(texture);new_texture["source"]+=image_base
        if "sampler" in new_texture:new_texture["sampler"]+=sampler_base
        output.setdefault("textures",[]).append(new_texture)
    assert len(returned["materials"])==1
    material=copy.deepcopy(returned["materials"][0])
    material["name"]="URMAN_NeighborA_Generated_PBR_v2"
    assert material.get("alphaMode","OPAQUE")=="OPAQUE"
    bindings=[material["pbrMetallicRoughness"]["baseColorTexture"],material["pbrMetallicRoughness"]["metallicRoughnessTexture"],material["normalTexture"]]
    for binding in bindings:
        assert binding.get("texCoord",0)==0 and not binding.get("extensions")
        binding["index"]+=texture_base
    output["materials"].append(material)
    for mesh in output["meshes"]:
        for primitive in mesh["primitives"]:primitive["material"]=material_index
    output["buffers"][0]["byteLength"]=len(binary)
    assert output["nodes"]==source["nodes"] and output["accessors"]==source["accessors"]
    assert output["bufferViews"][:len(source["bufferViews"])]==source["bufferViews"]
    assert bytes(binary[:len(source_binary)])==source_binary
    for old,new in zip(source["meshes"],output["meshes"]):
        old,new=copy.deepcopy(old),copy.deepcopy(new)
        for mesh in (old,new):
            for primitive in mesh["primitives"]:primitive.pop("material",None)
        assert old==new
    allowed={"meshes","materials","textures","samplers","images","bufferViews","buffers"}
    assert all(output[key]==value for key,value in source.items() if key not in allowed)
    # Same GLB chunk/alignment writer used by prepare_aidar_player_runtime.py.
    encoded=json.dumps(output,separators=(",",":"),ensure_ascii=False).encode()
    encoded+=b" "*((-len(encoded))%4)
    binary.extend(b"\0"*((-len(binary))%4))
    packed=(struct.pack("<III",0x46546C67,2,28+len(encoded)+len(binary))+
            struct.pack("<II",len(encoded),0x4E4F534A)+encoded+
            struct.pack("<II",len(binary),0x004E4942)+binary)
    OUT.mkdir();GLB.write_bytes(packed)
    actual=a.glb_document(GLB);actual_binary=glb_binary(GLB)
    assert actual["nodes"]==source["nodes"] and actual["accessors"]==source["accessors"]
    assert actual_binary[:len(source_binary)]==source_binary
    for row in image_rows:
        view=actual["bufferViews"][row["output_buffer_view"]]
        payload=actual_binary[view["byteOffset"]:view["byteOffset"]+view["byteLength"]]
        assert hashlib.sha256(payload).hexdigest()==row["sha256"]
    return {"source_bin_bytes":len(source_binary),"source_bin_sha256":hashlib.sha256(source_binary).hexdigest(),
            "output_original_bin_prefix_sha256":hashlib.sha256(actual_binary[:len(source_binary)]).hexdigest(),
            "all_source_accessor_bytes_nodes_indices_transforms_uv_normals_identical":True,
            "mesh_json_changed_only_material_index":True,"returned_geometry_incorporated":False,
            "returned_image_payloads_byte_identical":image_rows}


def main():
    assert bpy.app.version[:3]==(4,5,12)
    assert a.sha256_file(SOURCE)=="c9893cbfadb85b74d80eef8ff59d39aee67265053d5f6623a418205a8bd16ab0"
    assert a.sha256_file(RETURNED)=="2ff989ef67448059d23d232e7425a875c0d74044668e80fc6329b644156211b8"
    assert not OUT.exists(),"Refusing to overwrite a source candidate"
    original=json.loads(RECEIPT.read_text())
    source_doc,returned_doc=a.glb_document(SOURCE),a.glb_document(RETURNED)
    byte_proof=attach_material_bytes(source_doc,returned_doc)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    source_root=bpy.data.objects[p.COMPONENT]
    source_objects=set(bpy.data.objects)
    source_meshes=[ob for ob in source_root.children_recursive if ob.type=="MESH"]
    before=p.ordered_mesh_contract(source_meshes)
    uv_before={ob.name:p.h.uv_loop_sha256(ob.data) for ob in source_meshes}
    source_signatures=a.triangle_signatures(source_meshes)
    bpy.ops.import_scene.gltf(filepath=str(RETURNED))
    returned_objects=[ob for ob in bpy.data.objects if ob not in source_objects]
    returned_meshes=[ob for ob in returned_objects if ob.type=="MESH"]
    returned_signatures=a.triangle_signatures(returned_meshes)
    delta=a.counter_delta(source_signatures["uv_space"],returned_signatures["uv_space"])
    assert delta=={"missing":198,"extra":0,"exact":False}
    missing=missing_uv_rows(source_meshes,returned_signatures["uv_space"])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(GLB))
    root=bpy.data.objects[p.COMPONENT]
    meshes=[ob for ob in root.children_recursive if ob.type=="MESH"]
    assert len(meshes)==115 and sum(p.mesh_triangles(ob.data) for ob in meshes)==20772
    assert not any("Glass" in ob.name or "RoofSnow" in ob.name or "_Corner_" in ob.name for ob in meshes)
    after=p.ordered_mesh_contract(meshes)
    assert before==after
    assert uv_before=={ob.name:p.h.uv_loop_sha256(ob.data) for ob in meshes}
    materials=list(dict.fromkeys(material for ob in meshes for material in ob.data.materials))
    assert len(materials)==1
    report=a.material_report(materials[0])
    assert report and all(report["image_bindings"][role] for role in ("Base Color","Roughness","Metallic","Normal"))
    review={"status":"material_only_bytes_preserved_source_preview_pending",
            "source":{"path":SOURCE.name,"sha256":a.sha256_file(SOURCE),"bytes":SOURCE.stat().st_size},
            "returned":{"path":RETURNED.name,"sha256":a.sha256_file(RETURNED),"bytes":RETURNED.stat().st_size,"material":report},
            "literal_byte_guards":byte_proof,"source_mesh_nodes":115,"source_triangles":20772,
            "ordered_mesh_contract_before":before,"ordered_mesh_contract_after":after,
            "source_per_node_uv_loop_hashes_before":uv_before,"source_per_node_uv_loop_hashes_after":uv_before,
            "returned_uv_coverage_delta":delta,"missing_uv_triangles":missing,
            "missing_uv_area_pixels_4096_sum":sum(row["uv_area_pixels_4096"] for row in missing),
            "missing_uv_area_pixels_4096_max":max(row["uv_area_pixels_4096"] for row in missing),
            "missing_world_3d_area_sum":sum(row["world_3d_area"] for row in missing),
            "missing_world_3d_area_max":max(row["world_3d_area"] for row in missing),
            "missing_nodes":{name:{"triangles":sum(row["node"]==name for row in missing),"world_3d_area_sum":sum(row["world_3d_area"] for row in missing if row["node"]==name)} for name in sorted({row["node"] for row in missing})},
            "strict_uv_preflight":{"passed":False,"frozen_donor_selector_flags":60,"largest_measured_pair_overlap_pixels_4096":9.207391085605532e-05},
            "construction_contact_data_sha256":hashlib.sha256(json.dumps(original["construction_geometry"],sort_keys=True,separators=(",",":")).encode()).hexdigest(),
            "coverage_preview_authorization":"Root authorized known198missing/0extra subset for this one material preview. No UV/topology/normal repair, no hero cap exceptions.",
            "orientation":"Returned material JSON/samplers/UV0/channel bindings and JPEG bytes copied unchanged; no manual V/green flip.",
            "outputs":[{"path":str(GLB.relative_to(HERE)),"sha256":a.sha256_file(GLB),"bytes":GLB.stat().st_size}]}
    if "pbr_attachment_blender_export_diagnostic" not in original:
        original["pbr_attachment_blender_export_diagnostic"]=original.get("pbr_attachment",{})
        RECEIPT.write_text(json.dumps(original,ensure_ascii=False,indent=2)+"\n")
    save_review(review)
    print("PBR_LITERAL_BYTES_PASS",json.dumps({"output":review["outputs"],"missing_3d_area":review["missing_world_3d_area_sum"],"geometry_bytes_identical":True}),flush=True)
    p.HERE=OUT
    views=p.render_imported(root,meshes)
    for name,target,direction in [("actual-glb-seni-entry-detail",(3.75,2.,1.45),(1,-1,.35)),
                                  ("actual-glb-seni-rear-detail",(3.75,4.4,1.45),(.8,1,.35))]:
        path=OUT/(name+".png")
        a.render_view(bpy.context.scene,bpy.context.scene.camera,Vector(target),Vector(direction),4.2,path)
        views.append({"path":path.name,"sha256":a.sha256_file(path),"target":target,"direction":direction,"orthographic_scale":4.2})
    review.update({"status":"material_only_source_candidate_coverage_art_native_gates_open",
                   "actual_exported_glb_views":[{**row,"path":str((OUT/row["path"]).relative_to(HERE))} for row in views],
                   "remaining":"Root review of actual four sides/corner/SeniEntry/SeniRear. Strict UV preflight NOT PASSED; returned coverage198missing/0extra; no runtime promotion."})
    save_review(review)
    print("PBR_SOURCE_PREVIEW_READY",json.dumps({"status":review["status"],"views":len(views),"outputs":review["outputs"]}),flush=True)


if __name__=="__main__":main()
