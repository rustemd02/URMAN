from __future__ import annotations
import math, hashlib, heapq
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

def _quant(p, snap):
    return (round(p[0]/snap)*snap, round(p[1]/snap)*snap)

def build_road_graph(roads, snap=0.25):
    """
    roads: [{id, points:[[x,z]...], street_id?, class?}]
    Splits graph logically at geometric intersections without moving source geometry.
    """
    segments=[]
    for r in roads:
        pts=[tuple(x) for x in r["points"]]
        for i,(a,b) in enumerate(zip(pts,pts[1:])):
            segments.append({
                "road_id":r["id"],"street_id":r.get("street_id"),
                "class":r.get("class","local"),
                "a":a,"b":b,"index":i,"cuts":[(0.0,a),(1.0,b)]
            })
    # O(N^2) is fine for a small village reference implementation.
    for i in range(len(segments)):
        for j in range(i+1,len(segments)):
            s1,s2=segments[i],segments[j]
            hit=segment_intersection(s1["a"],s1["b"],s2["a"],s2["b"])
            if not hit: continue
            p,t,u=hit
            s1["cuts"].append((max(0,min(1,t)),p))
            s2["cuts"].append((max(0,min(1,u)),p))
    node_by_q={}
    nodes={}
    edges=[]
    def node_for(p):
        q=_quant(p,snap)
        if q not in node_by_q:
            nid=f"N{len(node_by_q)+1:04d}"
            node_by_q[q]=nid
            nodes[nid]={"id":nid,"x":q[0],"z":q[1]}
        return node_by_q[q]
    for s in segments:
        cuts=sorted(s["cuts"],key=lambda x:x[0])
        unique=[]
        for t,p in cuts:
            if not unique or abs(t-unique[-1][0])>1e-6:
                unique.append((t,p))
        for (t1,p1),(t2,p2) in zip(unique,unique[1:]):
            if dist(p1,p2)<0.05: continue
            a=node_for(p1); b=node_for(p2)
            edges.append({
                "id":f"E{len(edges)+1:05d}","a":a,"b":b,
                "length":dist(p1,p2),
                "road_id":s["road_id"],"street_id":s["street_id"],
                "class":s["class"],
                "source_segment_index":s["index"]
            })
    return {"nodes":list(nodes.values()),"edges":edges}

def adjacency(graph, allowed_street_id=None):
    adj=defaultdict(list)
    for e in graph["edges"]:
        if allowed_street_id is not None and e.get("street_id")!=allowed_street_id:
            continue
        adj[e["a"]].append((e["b"],e))
        adj[e["b"]].append((e["a"],e))
    return adj

def components(graph):
    adj=adjacency(graph)
    unseen={n["id"] for n in graph["nodes"]}
    comps=[]
    while unseen:
        root=next(iter(unseen)); q=[root]; unseen.remove(root); c=[]
        while q:
            u=q.pop(); c.append(u)
            for v,_ in adj[u]:
                if v in unseen:
                    unseen.remove(v);q.append(v)
        comps.append(c)
    return sorted(comps,key=len,reverse=True)

def dijkstra(graph,start,allowed_street_id=None):
    adj=adjacency(graph,allowed_street_id)
    d={n["id"]:float("inf") for n in graph["nodes"]};d[start]=0.0
    pq=[(0.0,start)]
    while pq:
        du,u=heapq.heappop(pq)
        if du!=d[u]:continue
        for v,e in adj[u]:
            nd=du+e["length"]
            if nd<d[v]:
                d[v]=nd;heapq.heappush(pq,(nd,v))
    return d

def nearest_road_access(point, graph, street_id=None):
    nodes={n["id"]:(n["x"],n["z"]) for n in graph["nodes"]}
    best=None
    for e in graph["edges"]:
        if street_id is not None and e.get("street_id")!=street_id:
            continue
        a,b=nodes[e["a"]],nodes[e["b"]]
        q,t,d=project_point_segment(point,a,b)
        row={"edge_id":e["id"],"street_id":e.get("street_id"),"point":q,"t":t,
             "distance":d,"a":e["a"],"b":e["b"],"tangent":(b[0]-a[0],b[1]-a[1])}
        if best is None or d<best["distance"]: best=row
    return best

def side_of_street(building_point, access):
    q=access["point"]; tx,tz=access["tangent"]
    vx,vz=building_point[0]-q[0],building_point[1]-q[1]
    cross=tx*vz-tz*vx
    return "left" if cross>0 else "right"

def audit_world(world, graph, max_access_distance=40.0):
    issues=[]
    comps=components(graph)
    if len(comps)>1:
        issues.append({
            "severity":"HIGH","code":"DISCONNECTED_ROAD_GRAPH",
            "message":f"Road graph has {len(comps)} components",
            "data":{"component_sizes":[len(x) for x in comps]}
        })
    seen_addr={}
    street_ids={s["id"] for s in world.get("streets",[])}
    for b in world.get("buildings",[]):
        if not b.get("addressable",True): continue
        p=tuple(b["position_xz"])
        acc=nearest_road_access(p,graph)
        if acc is None or acc["distance"]>max_access_distance:
            issues.append({
                "severity":"HIGH","code":"NO_REASONABLE_ACCESS",
                "entity":b.get("id"),"message":"Addressable building is too far from road",
                "data":{"distance_m":None if acc is None else round(acc["distance"],2)}
            })
        legacy=b.get("legacy_address")
        if legacy:
            sid=legacy.get("street_id")
            num=str(legacy.get("house_number"))
            if sid and sid not in street_ids:
                issues.append({"severity":"HIGH","code":"ADDRESS_STREET_MISSING",
                               "entity":b.get("id"),"message":f"Unknown street {sid}"})
            if sid and num:
                key=(sid,num)
                if key in seen_addr:
                    issues.append({"severity":"HIGH","code":"DUPLICATE_ADDRESS",
                                   "entity":b.get("id"),"message":f"Duplicate {sid} {num}",
                                   "data":{"other":seen_addr[key]}})
                else:
                    seen_addr[key]=b.get("id")
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
        return ends[0] if ends else sorted(own_nodes)[0]
    main_nodes=set()
    for e in graph["edges"]:
        if e.get("street_id")==main_street_id:
            main_nodes|={e["a"],e["b"]}
    if main_nodes:
        return min(own_nodes,key=lambda a:min(dist(nodes[a],nodes[m]) for m in main_nodes))
    return sorted(own_nodes)[0]

def chainage_for_building(point, street_id, graph, origin_node):
    access=nearest_road_access(point,graph,street_id)
    if not access:return None
    d=dijkstra(graph,origin_node,street_id)
    # distance to projected point from whichever endpoint is cheaper
    edge=next(e for e in graph["edges"] if e["id"]==access["edge_id"])
    via_a=d[edge["a"]]+edge["length"]*access["t"]
    via_b=d[edge["b"]]+edge["length"]*(1-access["t"])
    return min(via_a,via_b),access

def normalize_house_number(v):
    return str(v).strip().upper().replace(" ", "")

def next_suffix(base, used, alphabet):
    if base not in used:return base
    for letter in alphabet:
        candidate=f"{base}{letter}"
        if candidate not in used:return candidate
    raise RuntimeError(f"No suffix available for {base}")

def assign_addresses(world, graph, catalog, rules):
    """
    Preserves legacy addresses. New addresses are deterministic.
    Returns enriched buildings and address records.
    """
    cat={x["id"]:x for x in catalog}
    street_cfg={s["id"]:s for s in world.get("streets",[])}
    buildings=[dict(b) for b in world.get("buildings",[])]
    used=defaultdict(set)
    addresses=[]
    # Stable IDs and preserved legacy.
    for b in buildings:
        source_key=b.get("source_key") or b.get("id") or f'pos:{b["position_xz"]}'
        b["building_id"]=b.get("building_id") or stable_id("BLD",source_key)
        b["parcel_id"]=b.get("parcel_id") or stable_id("PAR","parcel:"+source_key)
        if b.get("legacy_address"):
            sid=b["legacy_address"]["street_id"]
            num=normalize_house_number(b["legacy_address"]["house_number"])
            used[sid].add(num)
            aid=stable_id("ADR",f'{b["building_id"]}:{sid}:{num}')
            b["address_id"]=aid;b["street_id"]=sid;b["house_number"]=num
            addresses.append({"address_id":aid,"building_id":b["building_id"],"parcel_id":b["parcel_id"],
                              "street_id":sid,"house_number":num,"status":"preserved"})
    # Group new buildings by nearest street.
    pending=defaultdict(list)
    for b in buildings:
        if not b.get("addressable",True) or b.get("address_id"):continue
        p=tuple(b["position_xz"])
        acc=nearest_road_access(p,graph)
        if not acc or not acc.get("street_id"):continue
        sid=acc["street_id"]
        origin=choose_street_origin(sid,graph,rules["special"]["main_street_id"])
        ch,acc2=chainage_for_building(p,sid,graph,origin)
        side=side_of_street(p,acc2)
        pending[sid].append((ch,side,b,acc2))
    # Number per street, preserving parity.
    for sid,rows in pending.items():
        rows.sort(key=lambda x:(x[0],x[2]["building_id"]))
        odd_next=1;even_next=2
        side_rule=street_cfg.get(sid,{}).get("odd_side","left")
        for ch,side,b,acc in rows:
            parity_odd=(side==side_rule)
            n=odd_next if parity_odd else even_next
            while str(n) in used[sid]:
                n+=2
            if parity_odd: odd_next=n+2
            else: even_next=n+2
            num=str(n);used[sid].add(num)
            aid=stable_id("ADR",f'{b["building_id"]}:{sid}:{num}')
            b["address_id"]=aid;b["street_id"]=sid;b["house_number"]=num
            b["address_chainage_m"]=round(ch,2)
            b["address_side"]=side
            b["access_point_xz"]=[round(acc["point"][0],3),round(acc["point"][1],3)]
            addresses.append({"address_id":aid,"building_id":b["building_id"],"parcel_id":b["parcel_id"],
                              "street_id":sid,"house_number":num,"status":"generated"})
    # Fictional cadastral-like IDs: stable and visibly not EGRN.
    for b in buildings:
        quarter=b.get("game_quarter_id","Q01")
        serial=int(hashlib.sha256(b["parcel_id"].encode()).hexdigest()[:8],16)%9000+1000
        b["game_cadastral_id"]=b.get("game_cadastral_id") or f"URM-{quarter}-P{serial:04d}"
    return buildings,addresses
