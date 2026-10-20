"""Extract existing H049 shed literally; render source examples, never a game frame."""
from pathlib import Path
import copy, hashlib, importlib.util, json, struct, sys

sys.dont_write_bytecode = True
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
SOURCE = ROOT / "game/assets/models/act1/urman_village_exterior_kit.glb"
SOURCE_SHA = "fdc69de9f580276f6a9c831bde60a0dad1728acdc533f00320e8a9009c233bf8"
ENTITY = "urman.world:act1/open-part/h049-house"
PARCEL = "VillageParcel_VariantA_TimberGable"
COMPONENT = PARCEL + "_Outbuilding"
GLB = HERE / "h049-outbuilding-source.glb"
RECEIPT = HERE / "source-receipt.json"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


studio = load("existing_house_studio", HERE.parent / "hero_house_pbr_v1/attach_hero_house_retexture.py")


def extract():
    # Same literal JSON/BIN writer pattern as prepare_aidar_player_runtime.py.
    # Compact only referenced bufferViews. Never export geometry through Blender.
    data = SOURCE.read_bytes()
    assert sha(SOURCE) == SOURCE_SHA
    size, kind = struct.unpack_from("<II", data, 12)
    assert kind == 0x4e4f534a
    doc = json.loads(data[20:20 + size])
    binary_size, binary_kind = struct.unpack_from("<II", data, 20 + size)
    assert binary_kind == 0x004e4942
    binary = data[28 + size:28 + size + binary_size]
    root_id = next(i for i, n in enumerate(doc["nodes"]) if n.get("name") == COMPONENT)
    ids = []
    def descend(i):
        ids.append(i)
        for c in doc["nodes"][i].get("children", []):
            descend(c)
    descend(root_id)
    node_map = {old: new for new, old in enumerate(ids)}
    mesh_ids = sorted({doc["nodes"][i]["mesh"] for i in ids if "mesh" in doc["nodes"][i]})
    mesh_map = {old: new for new, old in enumerate(mesh_ids)}
    meshes = [copy.deepcopy(doc["meshes"][i]) for i in mesh_ids]
    accessors = sorted({a for m in meshes for p in m["primitives"]
                        for a in [p["indices"], *p["attributes"].values()]})
    accessor_map = {old: new for new, old in enumerate(accessors)}
    views = sorted({doc["accessors"][i]["bufferView"] for i in accessors})
    view_map = {old: new for new, old in enumerate(views)}
    material_ids = sorted({p["material"] for m in meshes for p in m["primitives"]})
    material_map = {old: new for new, old in enumerate(material_ids)}
    materials = [copy.deepcopy(doc["materials"][i]) for i in material_ids]
    assert all("Texture" not in json.dumps(m) for m in materials), "Unexpected image dependency"
    output_binary = bytearray()
    output_views, byte_guards = [], []
    for i in views:
        old = doc["bufferViews"][i]
        chunk = binary[old.get("byteOffset", 0):old.get("byteOffset", 0) + old["byteLength"]]
        output_binary.extend(b"\0" * ((-len(output_binary)) % 4))
        new = copy.deepcopy(old)
        new.update(buffer=0, byteOffset=len(output_binary))
        output_views.append(new)
        output_binary.extend(chunk)
        byte_guards.append({"source_bufferView": i, "output_bufferView": view_map[i],
                            "bytes": len(chunk), "sha256": hashlib.sha256(chunk).hexdigest()})
    nodes = [copy.deepcopy(doc["nodes"][i]) for i in ids]
    source_root = copy.deepcopy(nodes[0])
    for n in nodes:
        if "children" in n:
            n["children"] = [node_map[i] for i in n["children"]]
        if "mesh" in n:
            n["mesh"] = mesh_map[n["mesh"]]
    assert set(nodes[0]) == {"children", "name", "translation"}
    nodes[0]["translation"] = [0, 0, 0]
    for m in meshes:
        for p in m["primitives"]:
            assert set(p).issubset({"attributes", "indices", "material", "mode"})
            p["indices"] = accessor_map[p["indices"]]
            p["attributes"] = {key: accessor_map[value] for key, value in p["attributes"].items()}
            p["material"] = material_map[p["material"]]
    output_accessors = [copy.deepcopy(doc["accessors"][i]) for i in accessors]
    for a in output_accessors:
        assert "sparse" not in a
        a["bufferView"] = view_map[a["bufferView"]]
    out = {"asset": copy.deepcopy(doc["asset"]), "scene": 0, "scenes": [{"nodes": [0]}],
           "nodes": nodes, "meshes": meshes, "accessors": output_accessors,
           "bufferViews": output_views, "buffers": [{"byteLength": len(output_binary)}],
           "materials": materials}
    if "extensionsUsed" in doc:
        out["extensionsUsed"] = copy.deepcopy(doc["extensionsUsed"])
    encoded = json.dumps(out, separators=(",", ":"), ensure_ascii=False).encode()
    encoded += b" " * ((-len(encoded)) % 4)
    output_binary.extend(b"\0" * ((-len(output_binary)) % 4))
    GLB.write_bytes(struct.pack("<III", 0x46546c67, 2, 28 + len(encoded) + len(output_binary))
                    + struct.pack("<II", len(encoded), 0x4e4f534a) + encoded
                    + struct.pack("<II", len(output_binary), 0x004e4942) + output_binary)
    # Re-read written output and compare actual data, not just planned hashes.
    written = GLB.read_bytes()
    output_size = struct.unpack_from("<I", written, 12)[0]
    actual = json.loads(written[20:20 + output_size])
    actual_binary = written[28 + output_size:]
    assert actual == out
    for guard in byte_guards:
        view = actual["bufferViews"][guard["output_bufferView"]]
        raw = actual_binary[view["byteOffset"]:view["byteOffset"] + view["byteLength"]]
        assert hashlib.sha256(raw).hexdigest() == guard["sha256"]
    assert len(meshes) == 21
    primitives = [p for m in meshes for p in m["primitives"]]
    assert len(primitives) == 27
    assert sum(actual["accessors"][p["indices"]]["count"] // 3 for p in primitives) == 524
    assert not any("TEXCOORD_0" in p["attributes"] for p in primitives)
    return source_root, byte_guards, [m["name"] for m in materials]


def main():
    assert bpy.app.version[:3] == (4, 5, 12)
    assert not RECEIPT.exists() and not GLB.exists(), "Frozen output already exists"
    plot_path = ROOT / "game/content/world/act1_open_part.world.v1.json"
    catalog_path = ROOT / "game/content/world/catalog.v1.json"
    entity = next(e for e in json.loads(plot_path.read_text())["entities"] if e["id"] == ENTITY)
    params = entity["params"]
    entry = next(e for e in json.loads(catalog_path.read_text())["entities"] if e["id"] == params["catalogId"])
    assert params["address"]["id"] == "ADR-H049" and params["scale"] == .731
    assert params["collision"] == "surfaces"
    assert not any(k in params for k in ("modelPath", "states", "hidden"))
    source_root, byte_guards, materials = extract()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(GLB))
    shed = bpy.data.objects[COMPONENT]
    meshes = [ob for ob in shed.children_recursive if ob.type == "MESH"]
    assert len(meshes) == 21
    assert all(not ob.data.uv_layers for ob in meshes)
    for ob in meshes:
        ob.data.calc_loop_triangles()
    assert sum(len(ob.data.loop_triangles) for ob in meshes) == 524
    bounds = studio.object_bounds(meshes)
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE / "h049-outbuilding-source.blend"))
    low, high = map(Vector, bounds)
    focus = (low + high) * .5
    camera = studio.create_studio(bpy.context.scene, focus, bounds)
    scene = bpy.context.scene
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 1280, 960
    width = max(high - low) * 1.5
    window = [ob for ob in meshes if ob.name.startswith("VariantA_Shed_SideWindow_")]
    wlow, whigh = map(Vector, studio.object_bounds(window))
    requests = [("source-front-right", focus, Vector((1, -1, .55)), width),
                ("source-rear-left", focus, Vector((-1, 1, .5)), width),
                ("source-front", focus, Vector((0, -1, .15)), width),
                ("source-left", focus, Vector((-1, 0, .15)), width),
                ("source-window-opening", (wlow + whigh) * .5, Vector((-1, -.25, .18)), max(whigh - wlow) * 2.25)]
    views = []
    for name, target, direction, scale in requests:
        path = HERE / (name + ".png")
        studio.render_view(scene, camera, target, direction, scale, path)
        views.append({"path": path.name, "sha256": sha(path), "bytes": path.stat().st_size,
                      "target_blender_z_up": list(target), "direction": list(direction), "ortho_scale": scale})
    report = {"schema": "h049-outbuilding-source-reference/v1",
              "status": "source_example_not_actual_runtime_frame",
              "source": {"path": str(SOURCE.relative_to(ROOT)), "sha256": SOURCE_SHA,
                         "bytes": SOURCE.stat().st_size, "license": "Project-original per assets/asset_registry.json"},
              "entity": entity, "catalog_entry": entry,
              "component": COMPONENT, "mesh_nodes": 21, "triangles": 524, "material_surfaces": 27,
              "materials": materials, "glass_node": "VariantA_Shed_SideWindow_Inset_LOD0",
              "uv_surfaces": 0, "keep_original_uv_ready": False,
              "original_root_node_gltf_y_up": source_root,
              "extraction": "Only ancestor/preview translation removed. No scale baked, no new basis, no Blender geometry export. Names, child TRS, mesh data/accessors/normals and material roles retained literally; only JSON indices and buffer offsets compacted.",
              "literal_referenced_bufferView_guards": byte_guards,
              "geometry_accessor_bytes_exact": True,
              "world_bounds_blender_z_up_at_source_scale": bounds,
              "dimensions_source_metres": list(high - low), "render_root_scale": 1,
              "runtime_not_reproduced": {"root_position": params["position"], "yaw_degrees": params["yawDegrees"],
                  "visual_scale": .731, "effective_outbuilding_scale": .9,
                  "edge_clamp": "BuildCollision computes whole-parcel Bounds, grows outbuilding about its base, then clamps within parcel plus .3m margins; not simulated",
                  "other": ["catalog origin rebase", "grounding/terrain", "PainterlyMaterialLibrary bindings", "world lighting", "neighbors", "runtime collision"]},
              "runtime_owner": "AuthoredWorldDirector.BuildVisual + BuildCollision(surfaces); static source triangles, no new door action",
              "current_material_binding": "Wood_Weathered/WetShadow: wood_facade; Wood_Dark: wood; roof: roof_metal; stone: stone. Existing W07 wood_shed_dark not bound here.",
              "outputs": [{"path": p.name, "sha256": sha(p), "bytes": p.stat().st_size} for p in
                          (GLB, HERE / "h049-outbuilding-source.blend")],
              "views": views, "blender": bpy.app.version_string,
              "input_hashes": [{"path": str(p.relative_to(ROOT)), "sha256": sha(p)} for p in
                  (Path(__file__), plot_path, catalog_path, ROOT / "game/scripts/AuthoredWorldDirector.cs",
                   HERE.parent / "hero_house_pbr_v1/attach_hero_house_retexture.py")],
              "next": "Inspect source views; one built-in ImageGen material reference. No atlas/UV/Meshy/runtime acceptance in this stage."}
    RECEIPT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print("H049_SOURCE_REFERENCE", json.dumps({"meshes": 21, "triangles": 524, "surfaces": 27,
          "bounds": bounds, "dimensions": report["dimensions_source_metres"], "views": len(views)}), flush=True)


if __name__ == "__main__":
    main()
