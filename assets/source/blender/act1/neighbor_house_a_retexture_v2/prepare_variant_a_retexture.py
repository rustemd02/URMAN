"""Source-only VariantA opaque donor; no service calls or runtime writes.

Reuse the hero-house atlas/mesh preparation helpers. The one local crown routine
is the nonhero branch of Act1ConnectedWorld.DwellingLogWalls.cs: 14-sided round logs with matched course/radius, physical saddle and longitudinal
bearing cuts, half-course offset, source main-wall overhangs and apertures.
The canonical GLB and C# remain untouched; original opaque source geometry stays
authoritative. This is a texture donor, never a runtime replacement of its own.
"""
from pathlib import Path
import hashlib, importlib.util, json, math, sys
import bpy
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
HELPER = HERE.parent / "hero_house_pbr_v1/prepare_hero_house_retexture.py"
spec = importlib.util.spec_from_file_location("existing_hero_house_prepare", HELPER)
h = importlib.util.module_from_spec(spec)
sys.dont_write_bytecode = True
spec.loader.exec_module(h)
SOURCE = ROOT / "game/assets/models/act1/urman_village_exterior_kit.glb"
RUNTIME = ROOT / "game/scripts/Act1ConnectedWorld.DwellingLogWalls.cs"
COMPONENT = "VillageParcel_VariantA_TimberGable_Dwelling"
GLB = HERE / "variant-a-opaque-for-retexture.glb"
BLEND = HERE / "variant-a-opaque-for-retexture.blend"
RECEIPT = HERE / "candidate-receipt.json"
WALL_COURSE = .26
WALL_LOG_SEGMENTS = 14
blender_to_runtime = h.blender_to_runtime
axis_aligned_bounds = h.axis_aligned_bounds
bounds_to_runtime_local = h.bounds_to_runtime_local
mesh_from_triangles = h.mesh_from_triangles
mesh_triangles = h.mesh_triangles
matrix_rows = h.matrix_rows
OVERLAP_DETAILS = []


def wall_log_specs(wall, parts, centre_world):
    low, high = axis_aligned_bounds([blender_to_runtime(v.co) for v in wall.data.vertices])
    size = high-low
    along_x = size.x >= size.z
    course = size.y / max(3, round(size.y/WALL_COURSE))
    radius = course*.52
    local_centre = blender_to_runtime(wall.matrix_world.inverted_safe() @ centre_world)
    mid = (low+high)*.5
    outward = 1 if (mid.z-local_centre.z if along_x else mid.x-local_centre.x) >= 0 else -1
    face = (high.z if outward > 0 else low.z) if along_x else (high.x if outward > 0 else low.x)
    depth = face + outward*(.035-radius)
    side_prefix = wall.name[:wall.name.index("Wall_LOD0")]
    openings = [axis_aligned_bounds(bounds_to_runtime_local(ob, wall.matrix_world.inverted_safe()))
                for ob in parts if ob is not wall and ob.name.startswith(side_prefix)
                and any(token in ob.name for token in ("Recess", "_Glass", "_Leaf"))]
    rows = []
    # Half-height starter courses provide a flat foundation seat under the
    # staggered walls; the full round courses then bear in longitudinal grooves.
    heights = ([float(low.y)] if not along_x else [])
    heights += [float(low.y+course*(k+.5)+(0 if along_x else course*.5))
                for k in range(max(3, round(size.y/WALL_COURSE)))
                if low.y+course*(k+.5)+(0 if along_x else course*.5)+course*.5 <= high.y+.01]
    for y in heights:
        spans = [((low.x if along_x else low.z)-.18, (high.x if along_x else high.z)+.18)]
        for op_low, op_high in openings:
            if not (y+radius > op_low.y and y-radius < op_high.y):
                continue
            c0,c1 = ((op_low.x-.02,op_high.x+.02) if along_x else (op_low.z-.02,op_high.z+.02))
            spans = [(a,b) for s0,s1 in spans for a,b in
                     ([(s0,s1)] if c1<=s0 or c0>=s1 else [(s0,min(c0,s1)),(max(c1,s0),s1)]) if b-a>=.2]
        for a,b in spans:
            rows.append({"wall": wall, "along_x": along_x, "from": float(a), "to": float(b),
                         "y": y, "depth": float(depth), "radius": float(radius), "base": float(low.y),
                         "course": float(course), "starter": y == float(low.y)})
    return rows, {"wall": wall.name, "crown_node": wall.name.replace("_Wall_LOD0","_LogCrown"),
                  "round_radius": float(radius), "course": float(course), "diameter_minus_course": float(2*radius-course),
                  "outward_limit": float(face+outward*.035), "opening_count": len(openings),
                  "log_spans": len(rows), "starter_half_log_count": sum(r["starter"] for r in rows),
                  "end_overhang": .18}


def cylinder_log(spec, material, index):
    along_x = spec["along_x"]
    a,b,y,d = spec["from"],spec["to"],spec["y"],spec["depth"]
    point = Vector(((a+b)*.5,y,d) if along_x else (d,y,(a+b)*.5))
    bpy.ops.mesh.primitive_cylinder_add(vertices=WALL_LOG_SEGMENTS, radius=spec["radius"], depth=b-a,
                                       end_fill_type="TRIFAN", location=h.runtime_to_blender(point))
    ob = bpy.context.object
    ob.name = "ConstructiveLog_%03d" % index
    axis = Vector((1,0,0)) if along_x else Vector((0,-1,0))
    ob.rotation_euler = axis.to_track_quat("Z","Y").to_euler()
    ob.data.materials.append(material)
    bpy.context.view_layer.update()
    matrix = ob.matrix_world.copy()
    ob.parent = spec["wall"].parent
    ob.matrix_world = spec["wall"].matrix_world @ matrix
    spec["object"] = ob
    cutter = ob.copy()
    cutter.data = ob.data.copy()
    cutter.name = "SeatCutter_%03d" % index
    bpy.context.scene.collection.objects.link(cutter)
    cutter.hide_render = True
    cutter.hide_set(True)
    spec["cutter"] = cutter
    return ob


def boolean_seat(ob, cutter):
    bpy.context.view_layer.objects.active = ob
    modifier = ob.modifiers.new("PhysicalBearingSeat", "BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.object = cutter
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def spec_bounds(spec):
    a,b,y,d,r = spec["from"],spec["to"],spec["y"],spec["depth"],spec["radius"]
    return ((a,y-r,d-r),(b,y+r,d+r)) if spec["along_x"] else ((d-r,y-r,a),(d+r,y+r,b))


def build_constructive_crowns(root, parts, centre, material):
    from mathutils.bvhtree import BVHTree
    walls = [ob for ob in parts if ob.name.endswith("_Wall_LOD0")
             and h.object_material_names(ob) == ["URMAN_Wood_Weathered"]]
    specs, stats = [], []
    for wall in walls:
        rows, row = wall_log_specs(wall, parts, centre)
        specs.extend(rows)
        stats.append(row)
    for index, row in enumerate(specs):
        cylinder_log(row, material, index)
    bpy.context.view_layer.update()
    # All source wall transforms are identical in this kit; refuse an unknown
    # basis rather than cutting a plausible-looking but geometrically wrong joint.
    assert all(matrix_rows(wall.matrix_world) == matrix_rows(walls[0].matrix_world) for wall in walls)
    cuts, support_pairs = [], []
    for upper in sorted(specs, key=lambda row: row["y"]):
        ulo,uhi = spec_bounds(upper)
        for lower in specs:
            if not 1e-5 < upper["y"]-lower["y"] < upper["radius"]+lower["radius"]:
                continue
            llo,lhi = spec_bounds(lower)
            if any(min(uhi[i],lhi[i])-max(ulo[i],llo[i]) <= 1e-5 for i in range(3)):
                continue
            before = mesh_triangles(upper["object"].data)
            boolean_seat(upper["object"], lower["cutter"])
            after = mesh_triangles(upper["object"].data)
            kind = "longitudinal" if upper["along_x"] == lower["along_x"] else "saddle"
            cuts.append({"upper":upper["object"].name,"lower":lower["object"].name,"type":kind,
                         "triangles_before":before,"triangles_after":after})
            if kind == "saddle":
                x = lower["depth"] if upper["along_x"] else upper["depth"]
                z = upper["depth"] if upper["along_x"] else lower["depth"]
                ua,la = (x,z) if upper["along_x"] else (z,x)
            else:
                ua = la = (max(upper["from"],lower["from"])+min(upper["to"],lower["to"]))* .5
                x,z = (ua,upper["depth"]) if upper["along_x"] else (upper["depth"],ua)
            if (upper["from"]+.001 < ua < upper["to"]-.001
                    and lower["from"]+.001 < la < lower["to"]-.001):
                support_pairs.append((upper,lower,x,z,kind))
    # Clip only below the existing wall/foundation datum. This creates actual
    # flat bearing faces, including the side-wall starter half-courses.
    for row in specs:
        if row["y"]-row["radius"] >= row["base"]:
            continue
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0,0,row["base"]-50))
        cutter = bpy.context.object
        cutter.scale = (100,100,100)
        boolean_seat(row["object"],cutter)
        bpy.data.objects.remove(cutter,do_unlink=True)
    # Sample the actual upper underside and lower top at each saddle/seat axis,
    # not just the construction parameters. Compare their mesh ray intersections.
    trees = {}
    for row in specs:
        ob = row["object"]
        cleanup = bmesh.new()
        cleanup.from_mesh(ob.data)
        before_vertices, before_faces = len(cleanup.verts), len(cleanup.faces)
        bmesh.ops.remove_doubles(cleanup,verts=list(cleanup.verts),dist=1e-6)
        bmesh.ops.dissolve_degenerate(cleanup,edges=list(cleanup.edges),dist=1e-6)
        cleanup.to_mesh(ob.data)
        row["boolean_cleanup"]={"log":ob.name,"maximum_merge_distance":1e-6,"vertices_before":before_vertices,
                                 "vertices_after":len(cleanup.verts),"faces_before":before_faces,"faces_after":len(cleanup.faces)}
        cleanup.free()
        # Freeze Blender's actual render/export tessellation. BMesh's alternate
        # triangulation of concave Boolean n-gons can bridge a bearing groove.
        source_mesh = ob.data
        source_mesh.calc_loop_triangles()
        triangles = [tuple(t.vertices) for t in source_mesh.loop_triangles]
        mesh = bpy.data.meshes.new(source_mesh.name+"ExportTriangles")
        mesh.from_pydata([v.co.copy() for v in source_mesh.vertices],[],triangles)
        for mat in source_mesh.materials: mesh.materials.append(mat)
        mesh.update()
        ob.data = mesh
        if source_mesh.users == 0: bpy.data.meshes.remove(source_mesh)
        bm = bmesh.new();bm.from_mesh(ob.data)
        assert all(edge.is_manifold for edge in bm.edges), ob.name
        trees[ob.name] = BVHTree.FromPolygons([ob.matrix_world@v.co for v in mesh.vertices],
                                             triangles,all_triangles=True)
        bm.free()
    contact_rows = []
    for upper,lower,x,z,kind in support_pairs:
        world = upper["wall"].matrix_world @ h.runtime_to_blender(Vector((x,upper["y"],z)))
        above = world+Vector((0,0,1))
        below = world-Vector((0,0,1))
        upper_hit = trees[upper["object"].name].ray_cast(below,Vector((0,0,1)),2)[0]
        lower_hits = []
        for candidate in specs:
            if candidate["y"] >= upper["y"]-1e-5:
                continue
            lo,hi = spec_bounds(candidate)
            if not (lo[0]-1e-5 <= x <= hi[0]+1e-5 and lo[2]-1e-5 <= z <= hi[2]+1e-5):
                continue
            hit = trees[candidate["object"].name].ray_cast(above,Vector((0,0,-1)),2)[0]
            if hit is not None:
                lower_hits.append((hit,candidate["object"].name))
        lower_hit, bearing_name = max(lower_hits,key=lambda pair:pair[0].z) if lower_hits else (None,None)
        if upper_hit is None or lower_hit is None:
            raise RuntimeError(("Missing actual bearing surface",upper["object"].name,lower["object"].name))
        error = abs(upper_hit.z-lower_hit.z)
        if error >= .0002:
            diagnostic = {"upper": {k:v for k,v in upper.items() if k not in ("object","cutter","wall")},
                          "upper_wall":upper["wall"].name,
                          "lower": {k:v for k,v in lower.items() if k not in ("object","cutter","wall")},
                          "lower_wall":lower["wall"].name,"upper_hit":list(upper_hit),"lower_hit":list(lower_hit),
                          "signed_gap":upper_hit.z-lower_hit.z,"bearing":bearing_name,
                          "cuts_on_upper":[r for r in cuts if r["upper"]==upper["object"].name],
                          "actual_upper_world_bounds":[[min((upper["object"].matrix_world@v.co)[i] for v in upper["object"].data.vertices),max((upper["object"].matrix_world@v.co)[i] for v in upper["object"].data.vertices)] for i in range(3)]}
            print("ACTUAL_BEARING_FAILURE",json.dumps(diagnostic),flush=True)
            bpy.context.preferences.filepaths.save_version=0
            bpy.ops.wm.save_as_mainfile(filepath=str(HERE/"construction-diagnostic.blend"))
        assert error < .0002, (kind,error,upper["object"].name,lower["object"].name)
        contact_rows.append({"upper":upper["object"].name,"lower":bearing_name,
                             "type":kind,"actual_mesh_vertical_gap":error,"world_contact":list(lower_hit)})
    supported_names = {row["upper"] for row in contact_rows}
    foundation_seats = []
    for row in specs:
        if row["object"].name in supported_names:
            continue
        ob = row["object"]
        base = row["wall"].matrix_world @ h.runtime_to_blender(Vector((0,row["base"],0)))
        lowest = min((ob.matrix_world @ vertex.co).z for vertex in ob.data.vertices)
        assert abs(lowest-base.z) < .0002, ("Unsupported log",ob.name,lowest,base.z)
        foundation_seats.append({"log":ob.name,"actual_flat_base_height":lowest,"foundation_datum":base.z})
    crowns = []
    for wall,row in zip(walls,stats):
        members = [s["object"] for s in specs if s["wall"] is wall]
        bpy.ops.object.select_all(action="DESELECT")
        for ob in members: ob.select_set(True)
        bpy.context.view_layer.objects.active = members[0]
        bpy.ops.object.join()
        crown = bpy.context.object
        crown.data.transform(wall.matrix_world.inverted_safe() @ crown.matrix_world)
        crown.parent = wall.parent
        crown.matrix_basis = wall.matrix_basis.copy()
        crown.name = row["crown_node"]
        crown["runtime_owner"] = "Act1ConnectedWorld.DwellingLogWalls.BuildDwellingLogWalls"
        crown["source_only_constructive_fix"] = True
        for polygon in crown.data.polygons:
            normal = polygon.normal
            polygon.use_smooth = (abs(normal.x) < .999 if "_Left_" not in wall.name and "_Right_" not in wall.name and "SeniOuter" not in wall.name else abs(normal.y) < .999)
        crown.data.set_sharp_from_angle(angle=math.radians(50))
        row["constructive_triangles"] = mesh_triangles(crown.data)
        crowns.append(crown)
    for row in specs:
        cutter = row["cutter"]
        mesh = cutter.data
        bpy.data.objects.remove(cutter,do_unlink=True)
        if mesh.users == 0: bpy.data.meshes.remove(mesh)
    hidden = {ob.name for ob in parts if "_Corner_" in ob.name}
    assert sum(mesh_triangles(ob.data) for ob in crowns) < 18000
    proof = {"round_logs":len(specs),"radial_segments":WALL_LOG_SEGMENTS,
             "saddle_cuts":sum(c["type"]=="saddle" for c in cuts),
             "longitudinal_grooves":sum(c["type"]=="longitudinal" for c in cuts),
             "cuts":cuts,"actual_contact_samples":contact_rows,
             "boolean_degenerate_cleanup":[row["boolean_cleanup"] for row in specs],
             "foundation_seats":foundation_seats,"all_logs_have_measured_support":True,
             "max_actual_contact_vertical_gap":max(r["actual_mesh_vertical_gap"] for r in contact_rows),
             "all_individual_seated_logs_closed_manifold":True,
             "construction_triangle_ceiling":18000,"decimation_used":False}
    print("CONSTRUCTIVE_GEOMETRY",json.dumps({"round_logs":len(specs),"triangles":sum(mesh_triangles(ob.data) for ob in crowns),
                                              "saddles":proof["saddle_cuts"],"grooves":proof["longitudinal_grooves"],
                                              "actual_contact_samples":len(contact_rows),"foundation_seats":len(foundation_seats),
                                              "max_gap":proof["max_actual_contact_vertical_gap"]}),flush=True)
    return crowns,stats,hidden,proof


def triangle_signature(obj, with_uv=False):
    """Ignore GLB's vertex splitting and triangle order, retain surface geometry."""
    obj.data.calc_loop_triangles()
    uv = obj.data.uv_layers.active
    rows = []
    for tri in obj.data.loop_triangles:
        row = []
        for li in tri.loops:
            vertex = obj.data.vertices[obj.data.loops[li].vertex_index].co
            value = tuple(round(float(c), 5) for c in vertex)
            if with_uv:
                value += tuple(round(float(c), 6) for c in uv.data[li].uv)
            row.append(value)
        rows.append(tuple(sorted(row)))
    return hashlib.sha256(repr(sorted(rows)).encode()).hexdigest()


def triangle_uv_values(obj):
    obj.data.calc_loop_triangles()
    uv = obj.data.uv_layers.active
    rows = []
    for tri in obj.data.loop_triangles:
        vertices = []
        for li in tri.loops:
            position = obj.data.vertices[obj.data.loops[li].vertex_index].co
            vertices.append((tuple(round(float(c), 5) for c in position), tuple(float(c) for c in uv.data[li].uv)))
        rows.append(tuple(sorted(vertices)))
    return [value for row in sorted(rows) for _, point in row for value in point]


def actual_uv_overlap_count(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.context.scene.tool_settings.use_uv_select_sync = False
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="DESELECT")
    bpy.ops.uv.select_overlap(extend=False)
    count = 0
    for ob in objects:
        bm = bmesh.from_edit_mesh(ob.data)
        uv = bm.loops.layers.uv.active
        selected = [face for face in bm.faces if any(loop[uv].select for loop in face.loops)]
        count += len(selected)
        if selected:
            OVERLAP_DETAILS.append({"node":ob.name,"selected_face_count":len(selected),
                                     "selected_face_indices_first64":[face.index for face in selected[:64]],
                                     "minimum_selected_3d_area":min(face.calc_area() for face in selected)})
    bpy.ops.object.mode_set(mode="OBJECT")
    return count


def build_constructive_atlas(objects, records):
    """Same hero Smart Project/global Pack pattern, without hero cap topology."""
    missing_before = sum(not ob.data.uv_layers for ob in objects)
    for ob in objects:
        while len(ob.data.uv_layers) > 1:
            ob.data.uv_layers.remove(ob.data.uv_layers[-1])
        if not ob.data.uv_layers: ob.data.uv_layers.new(name="UVMap")
        ob.data.uv_layers[0].name="UVMap"
        ob.data.uv_layers.active_index=0
        bpy.ops.object.select_all(action="DESELECT")
        ob.select_set(True)
        bpy.context.view_layer.objects.active=ob
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),margin_method="FRACTION",
                                 island_margin=.001,correct_aspect=True,scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objects: ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True,scale=True,merge_overlap=False,margin_method="FRACTION",margin=.002)
    bpy.ops.object.mode_set(mode="OBJECT")
    missing, coordinates = [], []
    for ob in objects:
        uv=h.uv_layer_summary(ob.data)
        records[ob.name]["input_uv_template"]={**uv,"sha256_loop_order":h.uv_loop_sha256(ob.data)}
        if uv["status"] != "present": missing.append(ob.name)
        coordinates.extend(tuple(loop.uv) for loop in ob.data.uv_layers.active.data)
    bounds=[[min(p[i] for p in coordinates),max(p[i] for p in coordinates)] for i in range(2)]
    assert all(-1e-6<=v<=1+1e-6 for pair in bounds for v in pair)
    return {"method":"Existing hero Smart UV Project (66 degrees) plus one multi-object Pack Islands; no fixed cap-fan topology assumed for real Boolean saddle cuts",
            "source_nodes_missing_uv":missing_before,"packed_mesh_node_count":len(objects),
            "packed_loop_count":len(coordinates),"packed_global_bounds":bounds,
            "pack_margin_fraction":.002,"missing_after_pack":missing}


def uv_intersection_area(first, second):
    def cross(a,b,c): return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    clip=list(second)
    if cross(*clip)==0 or cross(*first)==0: return 0.0
    if cross(*clip)<0: clip.reverse()
    points=list(first)
    for a,b in zip(clip,clip[1:]+clip[:1]):
        output=[]
        for p,q in zip(points,points[1:]+points[:1]):
            cp,cq=cross(a,b,p),cross(a,b,q)
            if cp>=0: output.append(p)
            if (cp<0<cq) or (cq<0<cp):
                t=cp/(cp-cq)
                output.append((p[0]+(q[0]-p[0])*t,p[1]+(q[1]-p[1])*t))
        points=output
        if len(points)<3: return 0.0
    origin=points[0]
    return abs(sum(cross(origin,points[i],points[i+1]) for i in range(1,len(points)-1)))*.5


def inspect_existing_uv():
    from collections import defaultdict
    data=json.loads(RECEIPT.read_text())
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    objects=[bpy.data.objects[name] for name,row in data["semantic_nodes"].items() if "input_triangles" in row]
    OVERLAP_DETAILS.clear()
    count=actual_uv_overlap_count(objects)
    selected=set()
    for row in OVERLAP_DETAILS:
        assert row["selected_face_count"]<=64
        selected.update((row["node"],index) for index in row["selected_face_indices_first64"])
    triangles=[]
    bins=defaultdict(list)
    for ob in objects:
        assert all(len(face.vertices)==3 for face in ob.data.polygons)
        layer=ob.data.uv_layers.active
        for face in ob.data.polygons:
            uv=[tuple(float(c) for c in layer.data[li].uv) for li in face.loop_indices]
            xyz=[tuple(float(c) for c in ob.matrix_world@ob.data.vertices[index].co) for index in face.vertices]
            ab=[xyz[1][i]-xyz[0][i] for i in range(3)]
            ac=[xyz[2][i]-xyz[0][i] for i in range(3)]
            normal=(ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0])
            area=abs((uv[1][0]-uv[0][0])*(uv[2][1]-uv[0][1])-(uv[1][1]-uv[0][1])*(uv[2][0]-uv[0][0]))*.5
            record={"node":ob.name,"face":face.index,"uv":uv,"uv_area":area,"uv_area_pixels_4096":area*4096**2,
                    "world_3d_area":math.hypot(*normal)*.5,
                    "world_3d_edge_lengths":[math.dist(xyz[i],xyz[(i+1)%3]) for i in range(3)],
                    "uv_edge_lengths":[math.dist(uv[i],uv[(i+1)%3]) for i in range(3)]}
            low=[min(p[i] for p in uv) for i in range(2)]
            high=[max(p[i] for p in uv) for i in range(2)]
            record["bbox"]=(low,high)
            index=len(triangles);triangles.append(record)
            for x in range(max(0,int(low[0]*128)),min(127,int(high[0]*128))+1):
                for y in range(max(0,int(low[1]*128)),min(127,int(high[1]*128))+1): bins[x,y].append(index)
    details=[]
    for row in triangles:
        if (row["node"],row["face"]) not in selected: continue
        low,high=row["bbox"]
        candidates=set()
        for x in range(max(0,int(low[0]*128)),min(127,int(high[0]*128))+1):
            for y in range(max(0,int(low[1]*128)),min(127,int(high[1]*128))+1): candidates.update(bins[x,y])
        partners=[]
        for index in candidates:
            other=triangles[index]
            if row is other: continue
            olo,ohi=other["bbox"]
            if any(min(high[i],ohi[i])-max(low[i],olo[i])<0 for i in range(2)):continue
            area=uv_intersection_area(row["uv"],other["uv"])
            if area>1e-22:
                partners.append({"node":other["node"],"face":other["face"],"intersection_uv_area":area,
                                 "intersection_area_pixels_4096":area*4096**2})
        details.append({k:v for k,v in row.items() if k!="bbox"}|{"overlap_partners":partners})
    data["uv_technical_completion"]={"stage":"exact_metrics_only_geometry_unchanged", "actual_selector_face_count":count,
                                      "selected_faces":details,"pair_area_method":"64-bit Sutherland-Hodgman triangle clipping; positive intersection area>1e-22UV units; ordinary zero-area shared boundaries omitted"}
    RECEIPT.write_text(json.dumps(data,ensure_ascii=False,indent=2)+"\n")
    print("UV_EXACT_METRICS",json.dumps({"selected_faces":len(details),"max_3d_area":max(r["world_3d_area"] for r in details),
                                         "max_uv_area_pixels":max(r["uv_area_pixels_4096"] for r in details),
                                         "face_metrics":[{"node":r["node"],"face":r["face"],"3d_area":r["world_3d_area"],"uv_px_area":r["uv_area_pixels_4096"],"partners":len(r["overlap_partners"])} for r in details]}),flush=True)
    return data,objects


def ordered_mesh_contract(objects):
    import struct
    result={}
    for ob in objects:
        mesh=ob.data
        def digest(rows,kind):
            value=hashlib.sha256()
            for row in rows:
                value.update(struct.pack("<"+kind*len(row),*row))
            return value.hexdigest()
        result[ob.name]={"ordered_vertex_float32_sha256":digest([tuple(v.co) for v in mesh.vertices],"f"),
                         "ordered_polygon_indices_uint32_sha256":digest([tuple(face.vertices) for face in mesh.polygons],"I"),
                         "ordered_corner_normals_float32_sha256":digest([tuple(n.vector) for n in mesh.corner_normals],"f"),
                         "object_matrix_float32_sha256":digest([tuple(row) for row in ob.matrix_world],"f"),
                         "vertices":len(mesh.vertices),"polygons":len(mesh.polygons),"loops":len(mesh.loops)}
    return result


def remap_eight_existing_faces():
    expected_files={"variant-a-opaque-for-retexture.glb":"c9893cbfadb85b74d80eef8ff59d39aee67265053d5f6623a418205a8bd16ab0",
                    "variant-a-opaque-for-retexture.blend":"6bb2473e9c0fcd10c3d417252f0fcabcd10f4ef2bdcc3225019c7803b6a658dc"}
    assert h.sha256_file(RECEIPT)=="f8d537c0350402f1d2cdfd9ccf51a0c6a0220143b7eadab4ebd368b6173d092d"
    for name,sha in expected_files.items(): assert h.sha256_file(HERE/name)==sha
    data=json.loads(RECEIPT.read_text())
    left="VariantA_TimberGable_Dwelling_Left_LogCrown"
    street="VariantA_TimberGable_Dwelling_Street_LogCrown"
    targets=[(left,index) for index in (23,30,31,35,38)]+[(street,index) for index in (3133,3264,3270)]
    metrics=data["uv_technical_completion"]["selected_faces"]
    five={(row["node"],row["face"]) for row in metrics if row["overlap_partners"]}
    assert five=={(left,30),(left,31),(left,38),(street,3133),(street,3270)}
    all_pairs=five|{(row["node"],row["face"]) for item in metrics for row in item["overlap_partners"]}
    assert all_pairs==set(targets)
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    objects=[bpy.data.objects[name] for name,row in data["semantic_nodes"].items() if "input_triangles" in row]
    before=ordered_mesh_contract(objects)
    contact_hash=hashlib.sha256(json.dumps(data["construction_geometry"],sort_keys=True,separators=(",",":")).encode()).hexdigest()
    OVERLAP_DETAILS.clear()
    assert actual_uv_overlap_count(objects)==60
    assert ordered_mesh_contract(objects)==before
    scale=4024/4096
    changed=[]
    for ob in objects:
        layer=ob.data.uv_layers.active
        for loop in layer.data: loop.uv=(loop.uv.x*scale,loop.uv.y*scale)
    for ordinal,(name,index) in enumerate(targets):
        ob=bpy.data.objects[name]
        face=ob.data.polygons[index]
        assert len(face.loop_indices)==3
        layer=ob.data.uv_layers.active
        old=[tuple(layer.data[li].uv) for li in face.loop_indices]
        sign=(old[1][0]-old[0][0])*(old[2][1]-old[0][1])-(old[1][1]-old[0][1])*(old[2][0]-old[0][0])
        x,y=4040,8+ordinal*64
        points=[(x/4096,y/4096),((x+48)/4096,y/4096),(x/4096,(y+48)/4096)]
        if sign<0: points[1],points[2]=points[2],points[1]
        for li,point in zip(face.loop_indices,points):layer.data[li].uv=point
        changed.append({"node":name,"face":index,"uv_after":points,"uv_area_pixels_4096":1152,
                        "island_bbox_pixels":[[x,y],[x+48,y+48]],"old_scaled_uv":old})
    assert ordered_mesh_contract(objects)==before
    OVERLAP_DETAILS.clear()
    after_count=actual_uv_overlap_count(objects)
    after_diagnostic=list(OVERLAP_DETAILS)
    assert ordered_mesh_contract(objects)==before
    result={"result":"remap_applied_in_memory_pending_export", "input_frozen_files":expected_files,
            "before_actual_selector_faces":60,"after_remap_actual_selector_faces":after_count,
            "remaining_selector_faces":after_diagnostic,"mapped_faces":changed,
            "atlas_strip":{"x_pixels":[4032,4096],"width_fraction":64/4096,"uniform_rest_scale":scale,
                           "inter_region_gutter_pixels":8,"island_edge_gutter_pixels":8,"vertical_island_gutter_pixels":16},
            "seams":"Eight selected face loops become independent UV islands. All other loop coordinates receive identical uniform scale; geometry and shading normals remain unchanged.",
            "ordered_mesh_contract_before":before,"ordered_mesh_contract_after":ordered_mesh_contract(objects),
            "contact_data_sha256_before":contact_hash,"contact_data_sha256_after":contact_hash}
    data["uv_technical_completion"]["single_eight_face_remap"]=result
    if after_count:
        result["result"]="stopped_selector_nonzero_no_export_or_source_overwrite"
        RECEIPT.write_text(json.dumps(data,ensure_ascii=False,indent=2)+"\n")
        print("UV_REMAP_STOP",json.dumps({"before":60,"after":after_count,"geometry_normals_unchanged":True,
                                           "remaining":after_diagnostic}),flush=True)
        return
    # Export only if the one remap achieves the unchanged strict metric.
    out_blend=HERE/"variant-a-opaque-uv-complete.blend"
    out_glb=HERE/"variant-a-opaque-uv-complete.glb"
    assert not out_blend.exists() and not out_glb.exists()
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(out_blend))
    bpy.ops.object.select_all(action="DESELECT")
    root=bpy.data.objects[COMPONENT]
    for ob in [root,*objects]:ob.select_set(True)
    bpy.context.view_layer.objects.active=root
    expected={ob.name:(mesh_triangles(ob.data),triangle_signature(ob),triangle_uv_values(ob)) for ob in objects}
    bpy.ops.export_scene.gltf(filepath=str(out_glb),export_format="GLB",use_selection=True,export_apply=False,
                              export_texcoords=True,export_normals=True,export_materials="EXPORT",use_visible=False)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(out_glb))
    imported=[ob for ob in bpy.data.objects[COMPONENT].children_recursive if ob.type=="MESH"]
    assert {ob.name for ob in imported}==set(expected)
    for ob in imported:
        tri,geometry,uv=expected[ob.name]
        assert tri==mesh_triangles(ob.data) and geometry==triangle_signature(ob)
        actual_uv=triangle_uv_values(ob)
        assert len(actual_uv)==len(uv) and max(abs(a-b) for a,b in zip(actual_uv,uv))<=1e-6
    OVERLAP_DETAILS.clear()
    export_count=actual_uv_overlap_count(imported)
    result["after_export_reimport_actual_selector_faces"]=export_count
    result["result"]="complete" if export_count==0 else "stopped_export_selector_nonzero"
    result["candidate_outputs"]=[{"path":p.name,"bytes":p.stat().st_size,"sha256":h.sha256_file(p)} for p in (out_glb,out_blend)]
    RECEIPT.write_text(json.dumps(data,ensure_ascii=False,indent=2)+"\n")
    print("UV_REMAP_EXPORT",json.dumps({"result":result["result"],"export_selector":export_count}),flush=True)


def render_imported(root, meshes):
    scene = bpy.context.scene
    world = bpy.data.worlds.new("DonorNeutralWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.2, .24, .28, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .65
    for position, energy, size in [((-6, -8, 12), 1800, 8), ((8, 0, 7), 1300, 7), ((0, 9, 10), 1400, 6)]:
        bpy.ops.object.light_add(type="AREA", location=position)
        light = bpy.context.object
        light.data.energy, light.data.size = energy, size
        light.rotation_euler = (Vector((0, 1, 2)) - light.location).to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    scene.camera = camera
    camera.data.type = "ORTHO"
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 1280, 960
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    scene.render.image_settings.file_format = "PNG"
    views = [
        ("actual-glb-front", (0, -16, 5.8), (.65, 1.3, 2.4), 10.8),
        ("actual-glb-front-right", (12, -13, 8), (.65, 1.3, 2.4), 11.5),
        ("actual-glb-rear-left", (-12, 13, 8), (.65, 1.3, 2.4), 11.5),
        ("actual-glb-corner-openings", (-7, -8, 4.9), (-1.6, -.9, 1.7), 5.3),
        ("actual-glb-right", (16, 1.5, 5.2), (.65, 1.3, 2.4), 10.8),
        ("actual-glb-left", (-16, 1.5, 5.2), (.65, 1.3, 2.4), 10.8),
        ("actual-glb-rear", (0, 17, 5.8), (.65, 1.3, 2.4), 10.8),
    ]
    outputs = []
    for name, position, target, scale in views:
        camera.location = position
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera.data.ortho_scale = scale
        path = HERE / (name + ".png")
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        outputs.append({"path": path.name, "sha256": h.sha256_file(path), "camera": position,
                        "target": target, "orthographic_scale": scale})
    return outputs


def main():
    if bpy.app.version[:3] != (4, 5, 12):
        raise RuntimeError("Pinned Blender 4.5.12 required")
    for path in (GLB, BLEND, RECEIPT):
        if path.exists():
            raise FileExistsError(path)
    assert h.sha256_file(SOURCE) == "fdc69de9f580276f6a9c831bde60a0dad1728acdc533f00320e8a9009c233bf8"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    root = bpy.data.objects[COMPONENT]
    parts = [ob for ob in root.children_recursive if ob.type == "MESH"]
    assert len(parts) == 122 and sum(mesh_triangles(ob.data) for ob in parts) == 3676
    keep = {root, *root.children_recursive}
    for ob in list(bpy.data.objects):
        if ob not in keep:
            if ob == root.parent:
                matrix = root.matrix_world.copy()
                root.parent = None
                root.matrix_world = matrix
            bpy.data.objects.remove(ob, do_unlink=True)
    # Remove only the component's preview-board translation, never its basis.
    preview_location = list(root.location)
    root.location = (0, 0, 0)
    bpy.context.view_layer.update()
    root_inverse = root.matrix_world.inverted_safe()
    records = {}
    for ob in parts:
        ob.data = ob.data.copy()
        records[ob.name] = {"source_triangles": mesh_triangles(ob.data),
                            "source_material_slots": h.object_material_names(ob),
                            "source_uv": h.uv_layer_summary(ob.data),
                            "source_geometry_sha256": triangle_signature(ob),
                            "source_root_relative_transform": matrix_rows(root_inverse @ ob.matrix_world)}
    centre, centre_source = h.hero_centre_world(root, parts)
    material = bpy.data.materials.new("Runtime_VariantA_LogCrown_85715c_PreRegrade")
    material.use_nodes = True
    rgb = [int(v, 16)/255 for v in ("85", "71", "5c")]
    linear = [c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4 for c in rgb]
    material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*linear, 1)
    material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .82
    crowns, crown_stats, hidden_posts, construction = build_constructive_crowns(root, parts, centre, material)
    for crown, stats in zip(crowns, crown_stats):
        records[crown.name] = {"source_uv": h.uv_layer_summary(crown.data), "generated_from": stats["wall"],
                               "construction": stats}
    assert len(crowns) == 7
    included, excluded = [], []
    for ob in parts:
        mats = h.object_material_names(ob)
        reason = ("runtime-hidden plain corner post" if ob.name in hidden_posts else
                  "glass excluded from opaque donor" if any("Glass" in m for m in mats) else
                  "snow excluded from opaque donor" if any("RoofSnow" in m for m in mats) else None)
        if reason:
            excluded.append({"node": ob.name, "reason": reason})
            ob.hide_render = True
            ob.hide_set(True)
        else:
            included.append(ob)
    included += crowns
    geometry_before = {ob.name: triangle_signature(ob) for ob in included}
    atlas = build_constructive_atlas(included, records)
    atlas["source_missing_uv_cause"] = "Selected VariantA opaque source nodes lack UVs; crowns used repeated length/girth UVs. All retained surfaces were reprojected and globally packed."
    assert not atlas["missing_after_pack"]
    atlas["actual_blender_overlap_selected_face_count"] = actual_uv_overlap_count(included)
    atlas["overlap_diagnostic"] = OVERLAP_DETAILS
    print("UV_OVERLAP_DIAGNOSTIC",json.dumps(OVERLAP_DETAILS),flush=True)
    assert all(triangle_signature(ob) == geometry_before[ob.name] for ob in included)
    assert all(triangle_signature(ob) == records[ob.name]["source_geometry_sha256"] for ob in parts)
    for ob in included:
        records[ob.name]["input_triangles"] = mesh_triangles(ob.data)
        records[ob.name]["input_geometry_sha256"] = triangle_signature(ob)
        records[ob.name]["input_geometry_uv_sha256"] = triangle_signature(ob, with_uv=True)
        records[ob.name]["input_root_relative_transform"] = matrix_rows(root_inverse @ ob.matrix_world)
    source_points=[root_inverse@(ob.matrix_world@v.co) for ob in parts for v in ob.data.vertices]
    generated_points=[root_inverse@(ob.matrix_world@v.co) for ob in crowns for v in ob.data.vertices]
    source_low,source_high=axis_aligned_bounds(source_points)
    crown_low,crown_high=axis_aligned_bounds(generated_points)
    assert all(source_low[i]-1e-5<=crown_low[i]<=crown_high[i]<=source_high[i]+1e-5 for i in range(3))
    envelope={"source_bounds_blender":[list(source_low),list(source_high)],
              "generated_bounds_blender":[list(crown_low),list(crown_high)],
              "all_generated_inside_original_component_bounds":True,
              "wall_outside_faces_preserved":"axes moved inward so outer ring limit remains source wall face+0.035m",
              "seni_end_change":"Seni course ends now use0.18m corner overhang as the main walls; this remains inside original roof/component bounds. Source-only integration decision required."}
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    bpy.ops.object.select_all(action="DESELECT")
    for ob in [root, *included]:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(filepath=str(GLB), export_format="GLB", use_selection=True,
                              export_apply=False, export_texcoords=True, export_normals=True,
                              export_materials="EXPORT", use_visible=False)
    expected = {ob.name: (mesh_triangles(ob.data), triangle_signature(ob), triangle_uv_values(ob),
                         matrix_rows(root_inverse @ ob.matrix_world)) for ob in included}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(GLB))
    imported_root = bpy.data.objects[COMPONENT]
    imported = [ob for ob in imported_root.children_recursive if ob.type == "MESH"]
    assert {ob.name for ob in imported} == set(expected)
    inverse = imported_root.matrix_world.inverted_safe()
    maximum_uv_error = 0.0
    for ob in imported:
        tri, geometry, geometry_uv, transform = expected[ob.name]
        assert mesh_triangles(ob.data) == tri, ob.name
        assert triangle_signature(ob) == geometry, ob.name
        actual_uv = triangle_uv_values(ob)
        assert len(actual_uv) == len(geometry_uv), ob.name
        uv_error = max(abs(a-b) for a,b in zip(actual_uv, geometry_uv))
        maximum_uv_error = max(maximum_uv_error, uv_error)
        assert uv_error <= 1e-6, (ob.name, uv_error)
        actual = matrix_rows(inverse @ ob.matrix_world)
        assert max(abs(a-b) for rowa, rowb in zip(actual, transform) for a,b in zip(rowa,rowb)) < 1e-5, ob.name
    views = render_imported(imported_root, imported)
    receipt = {"schema": "variant-a-opaque-retexture-donor/v2", "status": ("source_prepared_geometry_review_uv_overlap_blocker" if atlas["actual_blender_overlap_selected_face_count"] else "source_prepared_construction_review_required"),
               "source": {"path": str(SOURCE.relative_to(ROOT)), "sha256": h.sha256_file(SOURCE), "bytes": SOURCE.stat().st_size,
                          "rights": "Project-authored procedural kit; no external material incorporated; no new public license asserted."},
               "dependencies": [{"path": str(p.relative_to(ROOT)), "sha256": h.sha256_file(p)} for p in (HELPER, RUNTIME)],
               "component": COMPONENT, "preview_translation_removed": preview_location,
               "source_mesh_count": len(parts), "source_triangles": 3676,
               "excluded": excluded, "generated_crowns": crown_stats, "center_source": centre_source,
               "opaque_mesh_count": len(imported), "opaque_triangles": sum(mesh_triangles(ob.data) for ob in imported),
               "uv_atlas": atlas, "semantic_nodes": records,
               "invariants": {"all_original_geometry_unchanged": True, "all_export_mesh_names_preserved": True,
                              "export_reimport_triangle_geometry_signatures_equal": True,
                              "export_reimport_uv_max_absolute_error": maximum_uv_error,
                              "export_reimport_uv_error_tolerance": 1e-6,
                              "export_reimport_root_relative_transforms_equal_1e_5": True,
                              "glass_snow_excluded": True, "constructive_notches_real_mesh": True},
               "geometry_scope": "V2 constructive source fix: circular logs, real saddle and longitudinal seats. Original opaque source nodes and transforms remain unchanged. No runtime integration.",
               "construction_geometry": construction,
               "placement_envelope":envelope,
               "output": [{"path": p.name, "sha256": h.sha256_file(p), "bytes": p.stat().st_size} for p in (GLB, BLEND)],
               "actual_glb_views": views, "blender": bpy.app.version_string,
               "construction_review": {"result": "parent_actual_views_review_required", "references": ["actual-glb-corner-openings.png", "actual-glb-front-right.png"], "not_drop_in_runtime": "Source candidate only: integration must prevent duplicate runtime crowns and preserve opaque atlas UVs."},
               "remaining": "Parent actual geometry review and zero UV overlap required before any paid retexture. No API upload, generation, runtime promotion or native-performance claim."}
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"status": receipt["status"], "meshes": len(imported), "triangles": receipt["opaque_triangles"], "glb_sha256": h.sha256_file(GLB)}))


if __name__ == "__main__":
    if "--remap-eight-faces" in sys.argv: remap_eight_existing_faces()
    elif "--uv-details-only" in sys.argv: inspect_existing_uv()
    else: main()
