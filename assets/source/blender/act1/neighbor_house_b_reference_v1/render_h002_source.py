"""Render the current H002 VariantB source template, not an actual game frame."""
from pathlib import Path
import hashlib,importlib.util,json,sys
import bpy
from mathutils import Vector

sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
SOURCE=ROOT/"game/assets/models/act1/urman_village_exterior_kit.glb"
PLOT=ROOT/"game/content/world/act1_open_part.world.v1.json"
CATALOG=ROOT/"game/content/world/catalog.v1.json"
ENTITY="urman.world:act1/open-part/h002-house"
PARCEL="VillageParcel_VariantB_PlasterAnnex"
COMPONENT=PARCEL+"_Dwelling"


def load(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    return module


h=load("existing_house_preparation",HERE.parent/"hero_house_pbr_v1/prepare_hero_house_retexture.py")
a=load("existing_house_studio",HERE.parent/"hero_house_pbr_v1/attach_hero_house_retexture.py")


def main():
    assert bpy.app.version[:3]==(4,5,12)
    assert not (HERE/"source-receipt.json").exists(),"Refusing to overwrite the source reference"
    plot=json.loads(PLOT.read_text());catalog=json.loads(CATALOG.read_text())
    entity=next(item for item in plot["entities"] if item["id"]==ENTITY)
    params=entity["params"]
    entry=next(item for item in catalog["entities"] if item["id"]==params["catalogId"])
    assert plot["executor"]=="generic" and entity["kind"]=="prop"
    assert entry["source"]=={"scene":"res://assets/models/act1/urman_village_exterior_kit.glb","node":PARCEL}
    assert params["address"]["id"]=="ADR-H002" and params["collision"]=="surfaces"
    assert params["scale"]==.758 and not any(key in params for key in ("modelPath","states","hidden"))
    assert h.sha256_file(SOURCE)=="fdc69de9f580276f6a9c831bde60a0dad1728acdc533f00320e8a9009c233bf8"
    seed=2166136261
    for character in ENTITY:seed=((seed^ord(character))*16777619)&0xffffffff
    assert seed==2733781018 and seed%17==2
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    parcel=bpy.data.objects[PARCEL]
    dwelling=bpy.data.objects[COMPONENT]
    assert dwelling.parent is parcel
    original_world=h.matrix_rows(dwelling.matrix_world)
    original_local=h.matrix_rows(dwelling.matrix_local)
    meshes=[ob for ob in dwelling.children_recursive if ob.type=="MESH"]
    assert meshes and all(ob.name.startswith("VariantB_PlasterAnnex_Dwelling_") for ob in meshes)
    root_inverse=dwelling.matrix_world.inverted_safe()
    geometry_before={ob.name:{"triangles":h.mesh_triangles(ob.data),"vertices":len(ob.data.vertices),
                              "root_relative_transform":h.matrix_rows(root_inverse@ob.matrix_world),
                              "mesh_local_transform":h.matrix_rows(ob.matrix_basis),
                              "materials":h.object_material_names(ob)} for ob in meshes}
    matrix=dwelling.matrix_world.copy()
    dwelling.parent=None;dwelling.matrix_world=matrix
    keep={dwelling,*dwelling.children_recursive}
    for ob in list(bpy.data.objects):
        if ob not in keep:bpy.data.objects.remove(ob,do_unlink=True)
    preview=list(dwelling.location)
    dwelling.location=(0,0,0)
    # The generic surfaces path restores only the house to effective0.9 scale.
    # Parcel origin/edge Push, terrain, carpentry and game lighting are not simulated.
    dwelling.scale=(.9,.9,.9)
    bpy.context.view_layer.update()
    after_inverse=dwelling.matrix_world.inverted_safe()
    assert all(h.matrix_rows(ob.matrix_basis)==geometry_before[ob.name]["mesh_local_transform"] for ob in meshes)
    relative_matrix_error=max(abs(x-y) for ob in meshes
                              for arow,brow in zip(h.matrix_rows(after_inverse@ob.matrix_world),geometry_before[ob.name]["root_relative_transform"])
                              for x,y in zip(arow,brow))
    assert relative_matrix_error<1e-5
    bounds=a.object_bounds(meshes)
    low,high=map(Vector,bounds);focus=(low+high)*.5
    camera=a.create_studio(bpy.context.scene,focus,bounds)
    scene=bpy.context.scene;scene.cycles.samples=24
    scene.render.resolution_x=1280;scene.render.resolution_y=960
    width=max(high-low)*1.42
    requests=[("source-template-front",focus,Vector((0,-1,.18)),width),
              ("source-template-right",focus,Vector((1,0,.18)),width),
              ("source-template-rear",focus,Vector((0,1,.18)),width),
              ("source-template-left",focus,Vector((-1,0,.18)),width)]
    pane=next(ob for ob in meshes if ob.name=="VariantB_PlasterAnnex_Dwelling_Street_Window1_Glass_LOD0")
    window_stem=pane.name.removesuffix("_Glass_LOD0")
    opening=[ob for ob in meshes if ob.name.startswith(window_stem+"_")]
    wlow,whigh=map(Vector,a.object_bounds(opening))
    requests.append(("source-template-window-opening-detail",(wlow+whigh)*.5,Vector((.3,-1,.15)),max(whigh-wlow)*1.65))
    wall=next(ob for ob in meshes if ob.name=="VariantB_PlasterAnnex_Dwelling_Street_Wall_LOD0")
    wall_low,wall_high=map(Vector,a.object_bounds([wall]))
    corner=Vector((wall_high.x,wall_low.y,(wall_low.z+wall_high.z)*.5))
    requests.append(("source-template-corner-bearing-detail",corner,Vector((1,-1,.35)),3.8))
    views=[]
    for name,target,direction,scale in requests:
        path=HERE/(name+".png")
        a.render_view(scene,camera,target,direction,scale,path)
        views.append({"path":path.name,"sha256":h.sha256_file(path),"bytes":path.stat().st_size,
                      "target_blender_z_up":list(target),"view_direction_blender_z_up":list(direction),"orthographic_scale":scale})
    report={"schema":"h002-variant-b-source-reference/v1","status":"source_geometry_example_not_actual_game_frame",
            "source":{"path":str(SOURCE.relative_to(ROOT)),"sha256":h.sha256_file(SOURCE),"bytes":SOURCE.stat().st_size,
                      "rights":"Project-original perassets/asset_registry.json; no new public license asserted"},
            "current_plot_entity":entity,"current_catalog_entry":entry,"plot_executor":plot["executor"],
            "role":"Private addressed residential household H002/ADR-H002; currententity has no public building or KitPlacement façade role. Sourcefamily is plaster VariantB, not VariantA logs.",
            "visibility_contract":{"hidden_parameter_present":False,"states_parameter_present":False,"model_path_override_present":False,"generic_ApplyStates_default":"base visible, base collision"},
            "runtime_consumer":{"owner":"AuthoredWorldDirector.BuildVisual+BuildCollision(surfaces)",
                                "visual_scale":.758,"catalog_origin":entry["origin"],"house_scale_compensation":.9/.758,
                                "effective_dwelling_scale":.9,"placement":params["position"],"yaw_degrees":params["yawDegrees"],
                                "grounding":"Ground addsAgentBAct1HeightField.CollisionGround; not reproduced",
                                "edge_push":"BuildCollision computesBounds ofwholeparcel afterOpenYardGateway, then Pushes scaledDwelling/Outbuilding insideparcel+0.3m margins; not reproduced"},
            "runtime_style":{"stable_id":ENTITY,"stable_hash_fnv1a_uint32":seed,"variant_mod5":seed%5,"casing_paint_srgb":"e9e3d4",
                             "allowAddedStorey_generic_default":True,"hash_mod17":seed%17,"two_storey_condition":False},
            "component":{"parcel":PARCEL,"root":COMPONENT,"original_root_local_transform_blender":original_local,
                         "original_root_world_transform_blender":original_world,"preview_translation_removed":preview,
                         "render_only_root_scale":.9,"world_bounds_blender_z_up_at_render_scale":bounds,
                         "mesh_local_transforms_exactly_preserved":True,"root_relative_matrix_float_roundoff_max":relative_matrix_error,
                         "mesh_nodes":len(meshes),"source_triangles":sum(h.mesh_triangles(ob.data) for ob in meshes),
                         "mesh_inventory":geometry_before},
            "not_reproduced":["RuntimeTatarCarpentry trim/batches, painted material overrides andPiercedGableLace",
                              "RuntimeVillageWindowMaterials/occupied-windowlight/smoke/audio",
                              "Genericcatalogorigin/wholeparcelrebase, parcelEdgePush, terrainPad/ground and collision",
                              "GamePainterlyMaterialLibrary/worldlighting/fog/snowparticles and neighboringworldscene"],
            "source_integrity":"CanonicalsourceGLB untouched. Allselectedsource meshdata/relativetransforms retained; only isolatedpreviewroot translation removed and uniformrender0.9scale applied. Noatlas/retexture/LOD/export/generatedgeometry.",
            "input_hashes":[{"path":str(path.relative_to(ROOT)),"sha256":h.sha256_file(path)} for path in
                            (PLOT,CATALOG,ROOT/"game/scripts/AuthoredWorldDirector.cs",ROOT/"game/scripts/TimberHomeStyle.cs")],
            "views":views,"blender":bpy.app.version_string,"next":"Rootacceptance ofthis exactsourceexample/currentconsumer beforeImageGen orMeshy"}
    (HERE/"source-receipt.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n")
    print("H002_SOURCE_REFERENCE",json.dumps({"status":report["status"],"meshes":len(meshes),"triangles":report["component"]["source_triangles"],"views":len(views),"preview_removed":preview}),flush=True)


if __name__=="__main__":main()
