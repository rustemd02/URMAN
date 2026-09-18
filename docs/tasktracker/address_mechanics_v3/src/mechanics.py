from __future__ import annotations
import math, hashlib, heapq, re, unicodedata
from copy import deepcopy
from collections import defaultdict, deque

def stable_id(prefix: str, source_key: str) -> str:
    """Stable across rebuilds as long as source_key is stable."""
    h = hashlib.sha256(source_key.encode("utf-8")).hexdigest()[:16].upper()
    return f"{prefix}-{h}"

def dist(a,b):
    return math.hypot(a[0]-b[0], a[1]-b[1])

def lerp(a,b,t):
    return (a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t)

def project_point_segment(p,a,b):
    vx,vy=b[0]-a[0],b[1]-a[1]
    wx,wy=p[0]-a[0],p[1]-a[1]
    vv=vx*vx+vy*vy
    t=0 if vv<1e-9 else max(0,min(1,(wx*vx+wy*vy)/vv))
    q=(a[0]+vx*t,a[1]+vy*t)
    return q,t,dist(p,q)

def segment_intersection(a,b,c,d,eps=1e-9):
    """Returns point and parametric t/u for proper or endpoint intersection."""
    rx,ry=b[0]-a[0],b[1]-a[1]
    sx,sy=d[0]-c[0],d[1]-c[1]
    den=rx*sy-ry*sx
    if abs(den)<eps:
        return None
    qpx,qpy=c[0]-a[0],c[1]-a[1]
    t=(qpx*sy-qpy*sx)/den
    u=(qpx*ry-qpy*rx)/den
    if -eps<=t<=1+eps and -eps<=u<=1+eps:
        return (a[0]+t*rx,a[1]+t*ry),t,u
    return None

def pin_road_vertices(roads):
    """Commit semantic vertex keys on first import; never regenerate them from coordinates.

    point_keys travel with their points when an editor reverses a polyline. The
    stored bindings also recover a reversed points-only array after enrichment,
    but reject ambiguous moves rather than silently exchanging identities.
    """
    result=deepcopy(roads)
    for road in result:
        points=road["points"]
        if len(points)<2:raise ValueError("A road requires at least two vertices")
        keys=road.get("point_keys")
        if keys is None:
            keys=[f'{road["id"]}/vertex/{i}' for i in range(len(points))]
            if tuple(points[-1])<tuple(points[0]):keys.reverse()
        if len(keys)!=len(points) or len(set(keys))!=len(keys) or any(not isinstance(k,str) or not k.strip() for k in keys):
            raise ValueError("point_keys must uniquely identify every road vertex")
        bindings=road.get("vertex_bindings")
        if bindings:
            if set(bindings)!=set(keys):raise ValueError("Committed vertex binding keys changed")
            # Closed-loop endpoints may coincide; correctly aligned explicit keys
            # remain authoritative and do not require a geometric disambiguation.
            if any(dist(point,bindings[key])>.5 for point,key in zip(points,keys)):
                recovered=[]
                for point in points:
                    candidates=[key for key,old in bindings.items() if dist(point,old)<=.5]
                    if len(candidates)!=1:raise ValueError("Ambiguous pinned vertex movement; update explicit point_keys and bindings together")
                    recovered.append(candidates[0])
                if len(set(recovered))!=len(keys):raise ValueError("Pinned vertex identities overlap")
                keys=recovered
        road["point_keys"]=list(keys)
        road["vertex_bindings"]={key:list(point) for point,key in sorted(zip(points,keys),key=lambda pair:pair[1])}
    return result

def build_road_graph(roads, snap=0.25, constraints=None, previous_graph=None):
    """Split actual intersections, retaining source geometry.

    IDs derive from source roads and canonical vertices, not input traversal
    order. snap is retained for API compatibility, but no gap is rounded shut.
    Persist point_keys for roads whose endpoints may exchange geometric order.
    """
    segments=[]
    pinned={}
    for node in (previous_graph or {}).get("nodes",[]):
        for key in node.get("source_keys",[node.get("source_key")]):
            if key is None:continue  # historical unkeyed demo graphs cannot pin identity
            if key in pinned and pinned[key]["id"]!=node["id"]:raise ValueError("Conflicting committed graph vertex identities")
            pinned[key]=node
    road_ids=set()
    for r in sorted(pin_road_vertices(roads),key=lambda r:r["id"]):
        if r["id"] in road_ids:raise ValueError("Duplicate road source ID")
        road_ids.add(r["id"])
        pts=[tuple(x) for x in r["points"]]
        if not all(all(math.isfinite(v) for v in p) for p in pts):raise ValueError("Non-finite road coordinate")
        keys=r["point_keys"]
        if keys[-1]<keys[0]:
            pts.reverse()
            if keys is not None:keys=list(reversed(keys))
        before=len(segments)
        for i,(a,b) in enumerate(zip(pts,pts[1:])):
            if dist(a,b)<1e-9:continue
            ka=keys[i] if keys[i].startswith(r["id"]+"/") else f'{r["id"]}/{keys[i]}'
            kb=keys[i+1] if keys[i+1].startswith(r["id"]+"/") else f'{r["id"]}/{keys[i+1]}'
            segments.append({"road":r,"a":a,"b":b,"index":i,
                "key":r["id"]+"/"+"|".join(sorted((ka,kb))),
                "cuts":[[0.0,a,ka,None],[1.0,b,kb,None]]})
        if len(segments)==before:raise ValueError("A road has no non-degenerate segment: "+r["id"])
    for i,s1 in enumerate(segments):
        for s2 in segments[i+1:]:
            hit=segment_intersection(s1["a"],s1["b"],s2["a"],s2["b"])
            hits=[hit] if hit else []
            if hit is None:
                # Also split partial collinear overlaps and endpoint contacts.
                for p in (s1["a"],s1["b"],s2["a"],s2["b"]):
                    q,t,d1=project_point_segment(p,s1["a"],s1["b"])
                    q,u,d2=project_point_segment(p,s2["a"],s2["b"])
                    if d1<1e-8 and d2<1e-8:hits.append((p,t,u))
            for p,t,u in hits:
                endpoints=[s["cuts"][0 if parameter<.5 else 1][2] for s,parameter in ((s1,t),(s2,u)) if parameter<1e-8 or parameter>1-1e-8]
                endpoint="/end/"+min(endpoints) if endpoints else "/cross"
                key="intersection/"+"|".join(sorted((s1["key"],s2["key"])))+endpoint
                s1["cuts"].append([max(0,min(1,t)),p,key,None])
                s2["cuts"].append([max(0,min(1,u)),p,key,None])
    groups=[]
    for cut in sorted((c for s in segments for c in s["cuts"]),key=lambda c:c[2]):
        group=next((g for g in groups if dist(g[0][1],cut[1])<1e-8),None)
        if group is None:groups.append([cut])
        else:group.append(cut)
    nodes=[]
    for group in groups:
        source_keys=sorted({c[2] for c in group})
        inherited={pinned[key]["id"]:pinned[key] for key in source_keys if key in pinned}
        if len(inherited)>1:raise ValueError("Geometry merges committed graph nodes; explicit identity migration required")
        keys=sorted(c[2] for c in group if not c[2].startswith("intersection/"))
        key=keys[0] if keys else min(c[2] for c in group)
        old=next(iter(inherited.values()),None)
        if old:key=old["source_key"]
        nid=old["id"] if old else stable_id("N",key)
        p=min(group,key=lambda c:c[2])[1]
        nodes.append({"id":nid,"x":p[0],"z":p[1],"source_key":key,"source_keys":source_keys})
        for cut in group:cut[3]=nid
    edges={}
    for s in segments:
        unique=[]
        for c in sorted(s["cuts"],key=lambda c:(c[0],c[2])):
            if not unique or c[3]!=unique[-1][3]:unique.append(c)
        for a,b in zip(unique,unique[1:]):
            if a[3]==b[3] or dist(a[1],b[1])<1e-8:continue
            r=s["road"]
            eid=stable_id("E",r["id"]+"|"+"|".join(sorted((a[3],b[3]))))
            edges[eid]={"id":eid,"a":a[3],"b":b[3],"length":dist(a[1],b[1]),
                "road_id":r["id"],"street_id":r.get("street_id"),"class":r.get("class","local"),
                "source_segment_index":s["index"],"modes":deepcopy(r.get("modes",["foot"])),
                "blocked":r.get("blocked",False),"winter_blocked":r.get("winter_blocked",False),
                "surface":r.get("surface","unspecified"),"width_m":r.get("width_m"),
                "gate_id":r.get("gate_id"),"gate_open":r.get("gate_open",False),
                "constraint_blocks":constraint_blocks(a[1],b[1],constraints or {},r.get("constraint_exemptions",[]))}
    return {"nodes":sorted(nodes,key=lambda n:n["id"]),"edges":sorted(edges.values(),key=lambda e:e["id"])}

def point_in_polygon(point,polygon):
    inside=False
    for a,b in zip(polygon,polygon[1:]+polygon[:1]):
        if project_point_segment(point,a,b)[2]<1e-8:return True
        if (a[1]>point[1])!=(b[1]>point[1]) and point[0]<(b[0]-a[0])*(point[1]-a[1])/(b[1]-a[1])+a[0]:inside=not inside
    return inside

def constraint_blocks(a,b,constraints,exemptions=()):
    """Only explicit movement blockers apply. A forest-edge line is a landmark,
    not an instruction to close all of the existing world behind that line."""
    rows=[]
    entries=constraints.items() if isinstance(constraints,dict) else ((str(i),c) for i,c in enumerate(constraints))
    for key,rule in entries:
        if not isinstance(rule,dict):continue
        if "blocks_movement" in rule and not isinstance(rule["blocks_movement"],bool):raise ValueError("Constraint blocks_movement must be boolean")
        if not rule.get("blocks_movement") or key in exemptions:continue
        points=rule.get("points",[])
        if rule.get("type") not in ("line","polygon"):raise ValueError("Explicit movement constraint requires line/polygon geometry")
        if len(points)<(3 if rule["type"]=="polygon" else 2):raise ValueError("Explicit movement constraint has insufficient points")
        if any(len(p)!=2 or any(not isinstance(v,(int,float)) or isinstance(v,bool) or not math.isfinite(v) for v in p) for p in points):raise ValueError("Constraint coordinates must be finite XZ pairs")
        if "winter_only" in rule and not isinstance(rule["winter_only"],bool):raise ValueError("Constraint winter_only must be boolean")
        spans=list(zip(points,points[1:]))
        if rule.get("type")=="polygon":spans.append((points[-1],points[0]))
        crossed=any(segment_intersection(a,b,c,d) is not None or project_point_segment(c,a,b)[2]<1e-8 for c,d in spans)
        if rule.get("type")=="polygon":crossed=crossed or point_in_polygon(a,points) or point_in_polygon(b,points)
        if crossed:rows.append({"id":key,"modes":rule.get("modes",["foot","car","motorcycle","horse_cart"]),"winter_only":rule.get("winter_only",False)})
    return sorted(rows,key=lambda row:row["id"])

def edge_allowed(edge,mode="foot",winter=True):
    # None explicitly asks for authored topology/chainage, whose numbering must
    # never change with the weather or with a resident closing a gate.
    if mode is None:return True
    if edge.get("blocked") or (winter and edge.get("winter_blocked")):return False
    if edge.get("gate_id") and not edge.get("gate_open"):return False
    if mode not in edge.get("modes",["foot"]):return False
    if any(mode in c["modes"] and (winter or not c["winter_only"]) for c in edge.get("constraint_blocks",[])):return False
    width=edge.get("width_m")
    minimum={"foot":.70,"car":1.84,"motorcycle":1.0,"horse_cart":1.90}.get(mode)
    return width is None or minimum is None or width>=minimum

def adjacency(graph, allowed_street_id=None, mode=None, winter=True, exclude_street_id=None):
    adj=defaultdict(list)
    for e in graph["edges"]:
        if allowed_street_id is not None and e.get("street_id")!=allowed_street_id:
            continue
        if e.get("street_id")==exclude_street_id and exclude_street_id is not None:continue
        if not edge_allowed(e,mode,winter):continue
        adj[e["a"]].append((e["b"],e))
        adj[e["b"]].append((e["a"],e))
    return adj

def components(graph,mode=None,winter=True,exclude_street_id=None):
    adj=adjacency(graph,mode=mode,winter=winter,exclude_street_id=exclude_street_id)
    unseen={n["id"] for n in graph["nodes"]}
    comps=[]
    while unseen:
        root=min(unseen); q=[root]; unseen.remove(root); c=[]
        while q:
            u=q.pop(); c.append(u)
            for v,_ in adj[u]:
                if v in unseen:
                    unseen.remove(v);q.append(v)
        comps.append(c)
    return sorted((sorted(c) for c in comps),key=lambda c:(-len(c),c))

def dijkstra(graph,start,allowed_street_id=None,mode=None,winter=True,exclude_street_id=None):
    adj=adjacency(graph,allowed_street_id,mode,winter,exclude_street_id)
    if start not in {n["id"] for n in graph["nodes"]}:raise ValueError("Unknown graph start node")
    d={n["id"]:float("inf") for n in graph["nodes"]};d[start]=0.0
    pq=[(0.0,start)]
    while pq:
        du,u=heapq.heappop(pq)
        if du!=d[u]:continue
        for v,e in adj[u]:
            surface_cost=1.7 if mode is not None and e.get("surface") in ("ice","snow_deep") else 1.0
            nd=du+e["length"]*surface_cost
            if nd<d[v]:
                d[v]=nd;heapq.heappush(pq,(nd,v))
    return d

def nearest_road_access(point, graph, street_id=None,mode=None,winter=True):
    nodes={n["id"]:(n["x"],n["z"]) for n in graph["nodes"]}
    best=None
    for e in graph["edges"]:
        if street_id is not None and e.get("street_id")!=street_id:
            continue
        if not edge_allowed(e,mode,winter):continue
        a,b=nodes[e["a"]],nodes[e["b"]]
        q,t,d=project_point_segment(point,a,b)
        row={"edge_id":e["id"],"street_id":e.get("street_id"),"point":q,"t":t,
             "distance":d,"a":e["a"],"b":e["b"],"tangent":(b[0]-a[0],b[1]-a[1])}
        if best is None or (d,e["id"])<(best["distance"],best["edge_id"]): best=row
    return best

def side_of_street(building_point, access):
    q=access["point"]; tx,tz=access["tangent"]
    vx,vz=building_point[0]-q[0],building_point[1]-q[1]
    cross=tx*vz-tz*vx
    return "left" if cross>0 else "right"

def audit_world(world, graph, max_access_distance=40.0, rules=None):
    issues=[]
    comps=components(graph)
    if len(comps)>1:
        issues.append({"severity":"HIGH","code":"DISCONNECTED_ROAD_GRAPH",
            "message":f"Road graph has {len(comps)} components","data":{"component_sizes":[len(x) for x in comps]}})
    seen_addr={}
    street_ids={s["id"] for s in world.get("streets",[])}
    primary=set(comps[0]) if comps else set()
    origin=choose_street_origin((rules or {}).get("special",{}).get("main_street_id","tukay"),graph)
    accessible=dijkstra(graph,origin,mode="foot") if origin is not None else {}
    current={a["address_id"]:a for a in world.get("addresses",[])}
    for b in world.get("buildings",[]):
        if not b.get("addressable",True):continue
        endpoint=b.get("gate_xz") or b.get("entrance_xz")
        if endpoint is None or not b.get("access_path") or b.get("access_path_verified") is not True:
            issues.append({"severity":"HIGH","code":"ACCESS_NOT_PHYSICALLY_VERIFIED","entity":b.get("building_id",b.get("id")),
                "message":"Reference JSON has no verified path to the actual gate/door; centroid proximity is not game acceptance."})
        p=tuple(endpoint or b["position_xz"])
        acc=nearest_road_access(p,graph,b.get("street_id"),mode="foot")
        if acc is None or acc["distance"]>max_access_distance:
            issues.append({"severity":"HIGH","code":"NO_REASONABLE_ACCESS","entity":b.get("id"),
                "message":"Address endpoint is too far from a viable road","data":{"distance_m":None if acc is None else round(acc["distance"],2)}})
        elif acc["a"] not in primary or not math.isfinite(accessible.get(acc["a"],float("inf"))):
            issues.append({"severity":"HIGH","code":"UNREACHABLE_ADDRESS","entity":b.get("id"),"message":"Access is in a disconnected road component."})
        if b.get("access_path") and endpoint and dist(b["access_path"][-1],endpoint)>.25:
            issues.append({"severity":"HIGH","code":"ACCESS_ENDPOINT_MISMATCH","entity":b.get("id"),"message":"Path does not end at gate/door."})
        record=current.get(b.get("address_id")) or b.get("legacy_address")
        if record:
            sid=record.get("street_id");num=normalize_house_number(record.get("house_number"))
            if sid not in street_ids:issues.append({"severity":"HIGH","code":"ADDRESS_STREET_MISSING","entity":b.get("id"),"message":f"Unknown street {sid}"})
            key=(sid,num)
            if key in seen_addr:issues.append({"severity":"HIGH","code":"DUPLICATE_ADDRESS","entity":b.get("id"),"message":f"Duplicate normalized address {sid} {num}"})
            seen_addr[key]=b.get("id")
    bound={b.get("address_id") for b in world.get("buildings",[]) if b.get("addressable",True)}
    for aid in current.keys()-bound:
        issues.append({"severity":"HIGH","code":"ORPHAN_ADDRESS_RECORD","entity":aid,"message":"Committed record is retained and its number remains reserved, but its primary building is absent."})
    issues.extend(audit_special_streets(world,graph,rules or {}))
    return sorted(issues,key=lambda i:(i["code"],i.get("entity","")))

def audit_special_streets(world,graph,rules):
    special=rules.get("special",{});usal=special.get("forest_easter_street_id","usal")
    if not any(s["id"]==usal for s in world.get("streets",[])):return []
    issues=[];by_number={normalize_house_number(a["house_number"]):a for a in world.get("addresses",[]) if a["street_id"]==usal}
    buildings={b["building_id"]:b for b in world.get("buildings",[])}
    pair=[str(n) for n in special.get("usal_required_house_numbers",[15,52])]
    selected=[buildings.get(by_number.get(n,{}).get("building_id")) for n in pair]
    if any(b is None for b in selected):
        issues.append({"severity":"HIGH","code":"USAL_PAIR_MISSING","entity":usal,"message":"Usal must retain the intentional neighbouring houses 15 and 52."})
    elif dist(selected[0]["position_xz"],selected[1]["position_xz"])>special.get("usal_pair_max_distance_m",40):
        issues.append({"severity":"HIGH","code":"USAL_PAIR_DISTANCE","entity":usal,"message":"Usal 15/52 exceed the committed maximum distance; do not renumber them."})
    main=special.get("main_street_id","tukay");origin=choose_street_origin(main,graph,main)
    if origin is not None:
        before=dijkstra(graph,origin,mode="foot");after=dijkstra(graph,origin,mode="foot",exclude_street_id=usal)
        for b in world.get("buildings",[]):
            if not b.get("addressable",True) or b.get("street_id")==usal:continue
            point=b.get("gate_xz") or b.get("entrance_xz") or b["position_xz"]
            access=nearest_road_access(point,graph,b.get("street_id"),mode="foot")
            if access and math.isfinite(before[access["a"]]) and not math.isfinite(after[access["a"]]):
                issues.append({"severity":"HIGH","code":"USAL_REQUIRED_TRANSIT","entity":b["building_id"],"message":"This ordinary address depends on transiting the optional Usal branch."})
    if special.get("usal_requires_forest_edge",True):
        constraint=world.get("constraints",{}).get("forest_edge",{}) if isinstance(world.get("constraints",{}),dict) else {}
        boundary=constraint.get("points",[]);points=[p for r in world["roads"] if r.get("street_id")==usal for p in r["points"]]
        if len(boundary)<2 or not points:
            issues.append({"severity":"HIGH","code":"USAL_FOREST_EDGE_UNVERIFIED","entity":usal,"message":"A real imported forest edge and the Usal branch are required for this check."})
        else:
            distance=min(project_point_segment(p,a,b)[2] for p in points for a,b in zip(boundary,boundary[1:]))
            # The pair's 40m limit is not silently reused as a forest-distance
            # rule. Preserve the measured distance for the editor's art review.
            issues.append({"severity":"INFO","code":"USAL_FOREST_EDGE_OBSERVATION","entity":usal,"message":"Imported Usal-to-forest distance requires scene review.","data":{"distance_m":round(distance,3),"physical_scene_review":"not-run"}})
    return issues

def choose_street_origin(street_id, graph, main_street_id="tukay"):
    nodes={n["id"]:(n["x"],n["z"]) for n in graph["nodes"]}
    own=[e for e in graph["edges"] if e.get("street_id")==street_id]
    if not own:return None
    own_nodes=set([e["a"] for e in own]+[e["b"] for e in own])
    if street_id==main_street_id:
        # Use lexicographically stable endpoint among degree-1 nodes as a deterministic external end.
        adj=adjacency(graph,street_id)
        ends=sorted([n for n in own_nodes if len(adj[n])<=1])
        return min(ends or own_nodes,key=lambda n:(nodes[n],n))
    main_nodes=set()
    for e in graph["edges"]:
        if e.get("street_id")==main_street_id:
            main_nodes|={e["a"],e["b"]}
    if main_nodes:
        return min(own_nodes,key=lambda a:(min(dist(nodes[a],nodes[m]) for m in main_nodes),nodes[a],a))
    return min(own_nodes,key=lambda n:(nodes[n],n))

def chainage_for_building(point, street_id, graph, origin_node):
    if origin_node is None:return None
    access=nearest_road_access(point,graph,street_id)
    if not access:return None
    d=dijkstra(graph,origin_node,street_id)
    # distance to projected point from whichever endpoint is cheaper
    edge=next(e for e in graph["edges"] if e["id"]==access["edge_id"])
    via_a=d[edge["a"]]+edge["length"]*access["t"]
    via_b=d[edge["b"]]+edge["length"]*(1-access["t"])
    chainage=min(via_a,via_b)
    return (chainage,access) if math.isfinite(chainage) else None

def normalize_house_number(v):
    value="".join(unicodedata.normalize("NFKC",str(v)).upper().split())
    value=value.translate(str.maketrans("ABEKMHOPCTX","АВЕКМНОРСТХ"))
    value=re.sub(r"^0+(?=\d)","",value)
    if not re.fullmatch(r"\d+[А-ЯЁ]?(?:[/.-]\d+)?(?:К\d+)?",value):
        raise ValueError(f"Unsupported house number: {v}")
    return value

def resolve_address(addresses,reference=None,street_id=None,house_number=None):
    """Resolve stable links first, or an unambiguous current/historical label.
    Gameplay saves should use IDs; old display labels are a migration input."""
    if reference is not None:
        found=[a for a in addresses if reference in (a["address_id"],a["building_id"],a["parcel_id"])]
    else:
        number=normalize_house_number(house_number)
        found=[a for a in addresses if a["street_id"]==street_id and normalize_house_number(a["house_number"])==number]
        if not found:
            found=[a for a in addresses if any(h["street_id"]==street_id and normalize_house_number(h["house_number"])==number for h in a.get("history",[]))]
    if len(found)>1:raise ValueError("Ambiguous address alias; a stable entity ID is required")
    return found[0] if found else None

def next_suffix(base, used, alphabet):
    if base not in used:return base
    for letter in alphabet:
        candidate=f"{base}{letter}"
        if candidate not in used:return candidate
    raise RuntimeError(f"No suffix available for {base}")

def assign_addresses(world, graph, catalog, rules):
    """Enrich imported data while retaining committed records and identities.

    This reference adapter does not certify Godot collisions. access_path_verified
    is only accepted by the audit when an explicit endpoint and path are present.
    """
    street_cfg={s["id"]:s for s in world.get("streets",[])}
    if len(street_cfg)!=len(world.get("streets",[])):raise ValueError("Duplicate street ID")
    known_catalog={s["id"]:s for s in catalog}
    if set(street_cfg)-known_catalog.keys(): raise ValueError("Unknown street catalog entry")
    for sid,street in street_cfg.items():
        original=known_catalog[sid]
        for field in ("tatar","russian","role","protected"):
            if original.get("protected") and field in street and street[field]!=original[field]:
                raise ValueError("Protected street catalog value changed: "+sid+"."+field)
            street.setdefault(field,original[field])
    if any(r.get("street_id") is not None and r["street_id"] not in street_cfg for r in world["roads"]):
        raise ValueError("Road references an unknown street")
    buildings=deepcopy(world.get("buildings",[]))
    records={r["address_id"]:deepcopy(r) for r in world.get("addresses",[])}
    if len(records)!=len(world.get("addresses",[])):raise ValueError("Duplicate committed address_id")
    used=defaultdict(dict)
    addresses=deepcopy(records)
    for aid,record in addresses.items():
        sid=record["street_id"];number=normalize_house_number(record["house_number"])
        if sid not in street_cfg:raise ValueError("Unknown committed street")
        if number in used[sid]:raise ValueError("Duplicate normalized address")
        record["house_number"]=number;record.setdefault("history",[])
        used[sid][number]=aid
    source_keys=set();entity_ids=set()
    for b in buildings:
        source_key=b.get("source_key") or b.get("id")
        if not source_key:raise ValueError("Stable source_key/id is required; coordinate identity is forbidden")
        if source_key in source_keys:raise ValueError("Duplicate source_key")
        source_keys.add(source_key)
        b.setdefault("source_key",source_key)
        b["building_id"]=b.get("building_id") or stable_id("BLD",source_key)
        if b["building_id"] in entity_ids:raise ValueError("Duplicate building_id")
        entity_ids.add(b["building_id"])
        b["parcel_id"]=b.get("parcel_id") or stable_id("PAR","parcel:"+source_key)
        if not b.get("addressable",True):continue
        current=records.get(b.get("address_id"))
        if current is None and b.get("street_id") and b.get("house_number") is not None:
            current={"street_id":b["street_id"],"house_number":b["house_number"]}
        if current is None:current=b.get("legacy_address")
        if b.get("address_id") and current is None:raise ValueError("Orphan committed address_id")
        if current is None:continue
        sid=current["street_id"];num=normalize_house_number(current["house_number"])
        if sid not in street_cfg:raise ValueError("Unknown committed street")
        aid=b.get("address_id") or current.get("address_id") or stable_id("ADR",b["building_id"])
        if current.get("building_id",b["building_id"])!=b["building_id"]:raise ValueError("Address belongs to another building")
        if current.get("parcel_id",b["parcel_id"])!=b["parcel_id"]:raise ValueError("Address belongs to another parcel")
        if aid in addresses and addresses[aid].get("building_id")!=b["building_id"]:raise ValueError("Duplicate address ownership")
        if records.get(aid) and b.get("street_id") and (b["street_id"]!=sid or normalize_house_number(b.get("house_number",num))!=num):
            raise ValueError("Committed address and building display fields disagree; use an explicit migration")
        if num in used[sid] and used[sid][num]!=aid:raise ValueError("Duplicate normalized address")
        used[sid][num]=aid
        b.update(address_id=aid,street_id=sid,house_number=num)
        record=deepcopy(current)
        record.update(address_id=aid,building_id=b["building_id"],parcel_id=b["parcel_id"],street_id=sid,house_number=num)
        record.setdefault("status","preserved");record.setdefault("history",[])
        addresses[aid]=record
    pending=defaultdict(list)
    for b in buildings:
        if not b.get("addressable",True):continue
        # The actual entrance selects the street; route termination prefers gate.
        point=tuple(b.get("entrance_xz") or b.get("gate_xz") or b["position_xz"])
        sid=b.get("street_id") or b.get("entrance_street_id") or b.get("driveway_street_id") or b.get("main_facade_street_id")
        acc=nearest_road_access(point,graph,sid)
        if acc is None or not acc.get("street_id"):continue
        sid=acc["street_id"]
        cfg=street_cfg[sid]
        origin=cfg.get("origin_node")
        if origin is not None and origin not in {n["id"] for n in graph["nodes"]}:raise ValueError("Committed street origin is missing from graph: "+sid)
        if origin is None:origin=choose_street_origin(sid,graph,rules["special"]["main_street_id"])
        cfg.setdefault("origin_node",origin)
        cfg.setdefault("odd_side","left")
        found=chainage_for_building(point,sid,graph,origin)
        if found is None:continue
        ch,acc=found
        if not math.isfinite(ch):continue
        # Orient the tangent outward from the committed origin, not source order.
        distances=dijkstra(graph,origin,sid)
        if distances[acc["b"]]<distances[acc["a"]]:acc["tangent"]=tuple(-v for v in acc["tangent"])
        side=side_of_street(point,acc)
        b.update(address_chainage_m=round(ch,3),address_side=side,
                 access_point_xz=list(b.get("gate_xz") or b.get("entrance_xz") or point),
                 road_projection_xz=list(acc["point"]),access_source="gate" if b.get("gate_xz") else "entrance" if b.get("entrance_xz") else "unverified-centroid")
        if not b.get("address_id"):pending[sid].append((ch,side,b))
    committed={b["building_id"] for b in buildings if b.get("addressable",True) and b.get("address_id")}
    for sid,rows in sorted(pending.items()):
        for ch,side,b in sorted(rows,key=lambda row:(row[0],row[2]["building_id"])):
            parity=1 if side==street_cfg[sid]["odd_side"] else 0
            existing=[x for x in buildings if x["building_id"] in committed and x.get("street_id")==sid and x.get("address_id") and
                      int(re.match(r"\d+",x["house_number"])[0])%2==parity]
            previous=[x for x in existing if x.get("address_chainage_m",-1)<=ch]
            if existing:
                anchor=max(previous,key=lambda x:(x.get("address_chainage_m",-1),x["building_id"])) if previous else min(existing,key=lambda x:(x.get("address_chainage_m",float("inf")),x["building_id"]))
                base=re.match(r"\d+",anchor["house_number"])[0]
                num=next_suffix(base,used[sid],rules["addressing"]["allowed_letter_suffixes"])
            else:
                number=1 if parity else 2
                while str(number) in used[sid]:number+=2
                num=str(number)
            aid=stable_id("ADR",b["building_id"])
            used[sid][num]=aid;b.update(address_id=aid,street_id=sid,house_number=num)
            addresses[aid]={"address_id":aid,"building_id":b["building_id"],"parcel_id":b["parcel_id"],"street_id":sid,"house_number":num,"status":"generated","history":[]}
    # Explicit parcel parentage, never a nearest-house guess.
    by_id={b["id"]:b for b in buildings}
    by_id.update({b["building_id"]:b for b in buildings})
    visiting=set();resolved=set()
    def inherit(b):
        identity=b["building_id"]
        if identity in resolved:return
        if identity in visiting:raise ValueError("Auxiliary ownership cycle")
        visiting.add(identity)
        if b.get("parent_building_id"):
            parent=by_id.get(b["parent_building_id"])
            if parent is None:raise ValueError("Missing auxiliary parent")
            inherit(parent)
            if not b.get("addressable",True):
                b["parcel_id"]=parent["parcel_id"]
                for key in ("address_id","street_id","house_number"):b[key]=parent.get(key)
                b["address_inherited"]=True
                if "game_quarter_id" in parent:b.setdefault("game_quarter_id",parent["game_quarter_id"])
        visiting.remove(identity);resolved.add(identity)
    for b in buildings:inherit(b)
    cadastral={}
    used_cadastral={}
    for b in buildings:
        if b.get("game_cadastral_id"):
            cid=b["game_cadastral_id"]
            if not re.fullmatch(r"URM-Q\d{2}-P\d{4}",cid):raise ValueError("Invalid fictional cadastral ID")
            if cid in used_cadastral and used_cadastral[cid]!=b["parcel_id"]:raise ValueError("Duplicate committed cadastral ID")
            if b["parcel_id"] in cadastral and cadastral[b["parcel_id"]]!=cid:raise ValueError("Conflicting cadastral IDs on one parcel")
            if b.get("game_quarter_id",cid.split("-")[1])!=cid.split("-")[1]:raise ValueError("Committed cadastral quarter mismatch")
            cadastral[b["parcel_id"]]=cid;used_cadastral[cid]=b["parcel_id"]
    for b in sorted(buildings,key=lambda b:b["parcel_id"]):
        parcel=b["parcel_id"];quarter=b.get("game_quarter_id",cadastral.get(parcel,"URM-Q01-P0000").split("-")[1])
        if not re.fullmatch(r"Q\d{2}",quarter):raise ValueError("Invalid fictional quarter ID")
        if parcel not in cadastral:
            serial=_cadastral_start(parcel)
            for offset in range(10000):
                cid=f"URM-{quarter}-P{(serial+offset)%10000:04d}"
                if cid not in used_cadastral:break
            else:raise ValueError("Cadastral quarter is full")
            cadastral[parcel]=cid;used_cadastral[cid]=parcel
        b["game_cadastral_id"]=cadastral[parcel]
    return sorted(buildings,key=lambda b:b["building_id"]),sorted(addresses.values(),key=lambda a:a["address_id"])

def _cadastral_start(parcel):
    return int(hashlib.sha256(parcel.encode()).hexdigest()[:8],16)%10000
