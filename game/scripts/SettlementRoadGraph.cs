using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Urman.Godot;

public sealed record SettlementGraphNode(string Id, SettlementPoint Position);
public sealed record SettlementGraphEdge(string Id, string A, string B, string RoadId, string StreetId, double Length, double Width, string Surface, SettlementTravelMode Modes, bool WinterBlocked, string GateKey);
public sealed record SettlementRoad(string Id, string StreetId, IReadOnlyList<SettlementPoint> Points, double Width, string Surface, SettlementTravelMode Modes, bool WinterBlocked = false, string GateKey = "", IReadOnlyList<string>? PointKeys = null);

/// <summary>One imported graph for the notebook, access audit and travel policy.
 /// It never teleports the player, changes terrain, or draws a quest route.</summary>
public sealed class SettlementRoadGraph
{
    private readonly Dictionary<string, SettlementRoad> _roads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SettlementGraphNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SettlementGraphEdge> _edges = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blockedRoads = new(StringComparer.Ordinal);
    private readonly bool _measureRebuild = Environment.GetEnvironmentVariable("URMAN_ADDRESS_GRAPH_PERF") == "1";
    public IReadOnlyDictionary<string, SettlementRoad> Roads => _roads;
    public IReadOnlyDictionary<string, SettlementGraphNode> Nodes => _nodes;
    public IReadOnlyDictionary<string, SettlementGraphEdge> Edges => _edges;
    public Func<string, bool>? GateIsOpen { get; set; }

    public void AddRoad(SettlementRoad road)
    {
        ValidateRoad(road);
        if (_roads.ContainsKey(road.Id)) throw new InvalidOperationException("Road source ID is already imported: " + road.Id);
        _roads.Add(road.Id, road);
    }
    private static void ValidateRoad(SettlementRoad road)
    {
        if (road.Points.Count < 2 || road.Width <= 0 || string.IsNullOrWhiteSpace(road.Id)) throw new ArgumentException("Invalid imported road.");
        if (road.Points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z))) throw new ArgumentException("Road coordinate is not finite.");
        if (road.PointKeys is not null && (road.PointKeys.Count != road.Points.Count || road.PointKeys.Distinct(StringComparer.Ordinal).Count() != road.PointKeys.Count))
            throw new ArgumentException("Explicit road vertex keys must be unique and match the point count.");
    }
    internal void SetVerifiedFootAccess(string accessId,IReadOnlyList<SettlementPoint> points)
    {
        if(string.IsNullOrWhiteSpace(accessId))throw new ArgumentException("Missing access identity.");
        var road=new SettlementRoad("access/"+accessId,"",points,.75,"snow_trampled",SettlementTravelMode.Foot);
        ValidateRoad(road);
        if(_roads.TryGetValue(road.Id,out var old)&&(old.StreetId.Length>0||old.Modes!=SettlementTravelMode.Foot))
            throw new InvalidOperationException("A verified access cannot replace an authored street.");
        _roads[road.Id]=road;
    }
    internal bool RemoveVerifiedFootAccess(string accessId)
    {
        var id="access/"+accessId;
        if(!_roads.TryGetValue(id,out var road))return false;
        if(road.StreetId.Length>0||road.Modes!=SettlementTravelMode.Foot)
            throw new InvalidOperationException("Access invalidation cannot remove an authored street.");
        _blockedRoads.Remove(id);
        return _roads.Remove(id);
    }
    public void SetRoadBlocked(string roadId, bool blocked)
    {
        if (blocked) _blockedRoads.Add(roadId); else _blockedRoads.Remove(roadId);
    }
    private sealed class Cut(SettlementPoint point, string key, double t)
    {
        public SettlementPoint Point = point;
        public string Key = key;
        public double T = t;
        public string NodeId = "";
    }
    private sealed class Segment(SettlementRoad road, SettlementPoint a, SettlementPoint b, string key, string aKey, string bKey)
    {
        public SettlementRoad Road = road;
        public SettlementPoint A = a, B = b;
        public string Key = key;
        public List<Cut> Cuts = [new(a,aKey,0),new(b,bKey,1)];
        public readonly SegmentBounds Bounds = BoundsFor(a,b);
    }
    private readonly record struct SegmentBounds(double MinX,double MaxX,double MinZ,double MaxZ,bool Cullable);
    private static SegmentBounds BoundsFor(SettlementPoint a,SettlementPoint b)
    {
        // Cover both existing narrow-phase tolerances: endpoint parameters may
        // extend by 1e-8, and collinear projections may be less than 1e-5 apart.
        var padX=.00001+Math.Abs(b.X-a.X)*1e-8;
        var padZ=.00001+Math.Abs(b.Z-a.Z)*1e-8;
        // This is only an optimization envelope, never an import restriction.
        // Extreme coordinates retain the exhaustive path rather than assuming
        // that a world-sized absolute tolerance covers floating-point error.
        var cullable=Math.Max(Math.Max(Math.Abs(a.X),Math.Abs(a.Z)),Math.Max(Math.Abs(b.X),Math.Abs(b.Z)))<=10000;
        return new(Math.BitDecrement(Math.Min(a.X,b.X)-padX),Math.BitIncrement(Math.Max(a.X,b.X)+padX),
            Math.BitDecrement(Math.Min(a.Z,b.Z)-padZ),Math.BitIncrement(Math.Max(a.Z,b.Z)+padZ),cullable);
    }
    private static bool MayIntersect(Segment a,Segment b)
    {
        var x=a.Bounds;var y=b.Bounds;
        if(!x.Cullable||!y.Cullable||!(x.MaxX<y.MinX||y.MaxX<x.MinX||x.MaxZ<y.MinZ||y.MaxZ<x.MinZ))return true;
        // Do not cull an ill-conditioned non-parallel pair: cancellation in the
        // old t/u division can dominate its geometric endpoint tolerance. With
        // coordinates within 10 km and this determinant condition, rounding is
        // well below the retained 1e-5 pad. The old collinear branch uses clamped
        // projections, already covered by the padded bounds above.
        var productA=(a.B.X-a.A.X)*(b.B.Z-b.A.Z);
        var productB=(a.B.Z-a.A.Z)*(b.B.X-b.A.X);
        var determinant=Math.Abs(productA-productB);
        return determinant>1e-9&&determinant<=.001*(Math.Abs(productA)+Math.Abs(productB));
    }
    public void Rebuild(IReadOnlyDictionary<string, SettlementStreet> streets)=>RebuildCore(streets,true);
    // Same builder and narrow phase, with culling disabled only for equivalence
    // checks. No second graph algorithm or alternative production data owner.
    internal void RebuildExhaustiveForDiagnostics(IReadOnlyDictionary<string, SettlementStreet> streets)=>RebuildCore(streets,false);
    private void RebuildCore(IReadOnlyDictionary<string, SettlementStreet> streets,bool useBroadphase)
    {
        var startedUsec = _measureRebuild ? global::Godot.Time.GetTicksUsec() : 0UL;
        var allocatedBefore = _measureRebuild ? GC.GetAllocatedBytesForCurrentThread() : 0L;
        long acceptedPairs = 0,broadphaseRejectedPairs=0;
        _nodes.Clear(); _edges.Clear();
        var segments = new List<Segment>();
        foreach (var road in _roads.Values.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var points = road.Points.ToArray();
            var keys = road.PointKeys?.ToArray();
            // Direction belongs to the street's committed origin, not input array order.
            // For non-street paths use explicit endpoint keys whenever available.
            var distanceOrder = streets.TryGetValue(road.StreetId, out var street)
                ? points[^1].DistanceXZ(street.Origin).CompareTo(points[0].DistanceXZ(street.Origin)) : 0;
            // Equal endpoint distances still need one input-independent direction.
            // Keep pinned vertex identities first; unkeyed roads use the existing
            // X/Z ordering. No epsilon may reverse a previously unequal pair.
            var reverse = distanceOrder != 0 ? distanceOrder < 0
                : keys is not null ? string.CompareOrdinal(keys[^1],keys[0]) < 0
                : Compare(points[^1],points[0]) < 0;
            if (reverse) { Array.Reverse(points); if (keys is not null) Array.Reverse(keys); }
            for (var i=0;i<points.Length-1;i++)
            {
                if (points[i].DistanceXZ(points[i+1]) < .00001) continue;
                var aKey = keys is not null ? keys[i] : road.Id + "/point/" + i;
                var bKey = keys is not null ? keys[i+1] : road.Id + "/point/" + (i+1);
                var segmentKey = road.Id + "/" + string.Join("/", new[] {aKey,bKey}.Order(StringComparer.Ordinal));
                segments.Add(new(road,points[i],points[i+1],segmentKey,aKey,bKey));
            }
        }
        for (var i=0;i<segments.Count;i++) for(var j=i+1;j<segments.Count;j++)
        {
            var a=segments[i]; var b=segments[j];
            if(useBroadphase&&!MayIntersect(a,b))
            {
                if(_measureRebuild)broadphaseRejectedPairs++;
                continue;
            }
            string? junctionKey=null;
            foreach(var (t,u) in Intersections(a.A,a.B,b.A,b.B))
            {
                var p=a.A.Lerp(a.B,t); var q=b.A.Lerp(b.B,u);
                if (Math.Abs(p.Y-q.Y) > .5) continue; // Bridges at different levels do not connect.
                // Non-intersecting pairs need no identity. Collinear contacts
                // reuse exactly the same committed pair key and cut ordering.
                if(_measureRebuild && junctionKey is null)acceptedPairs++;
                junctionKey??="junction/" + string.Join("|",new[] {a.Key,b.Key}.Order(StringComparer.Ordinal));
                a.Cuts.Add(new(p,junctionKey,t));
                b.Cuts.Add(new(q,junctionKey,u));
            }
        }
        // Merge only physically coincident junctions; never round a gap shut.
        var groups = new List<List<Cut>>();
        foreach(var cut in segments.SelectMany(s=>s.Cuts).OrderBy(c=>c.Key,StringComparer.Ordinal))
        {
            var group=groups.FirstOrDefault(g=>g[0].Point.DistanceXZ(cut.Point)<.00001 && Math.Abs(g[0].Point.Y-cut.Point.Y)<.5);
            if(group is null) groups.Add([cut]); else group.Add(cut);
        }
        foreach(var group in groups)
        {
            var committedKey=group.Select(c=>c.Key).Where(k=>!k.StartsWith("junction/",StringComparison.Ordinal)).Order(StringComparer.Ordinal).FirstOrDefault()
                ?? group.Select(c=>c.Key).Order(StringComparer.Ordinal).First();
            var id=SettlementRegistry.StableId("ND",committedKey);
            var point=group.OrderBy(c=>c.Key,StringComparer.Ordinal).First().Point;
            _nodes[id]=new(id,point);
            foreach(var cut in group) cut.NodeId=id;
        }
        foreach(var s in segments)
        {
            var cuts=s.Cuts.OrderBy(c=>c.T).ThenBy(c=>c.Key,StringComparer.Ordinal).GroupBy(c=>c.NodeId).Select(g=>g.First()).OrderBy(c=>c.T).ToArray();
            for(var i=0;i<cuts.Length-1;i++)
            {
                var a=cuts[i];var b=cuts[i+1];
                if(a.NodeId==b.NodeId || a.Point.DistanceXZ(b.Point)<.00001) continue;
                var id=SettlementRegistry.StableId("ED",s.Road.Id+"|"+string.Join("|",new[]{a.NodeId,b.NodeId}.Order(StringComparer.Ordinal)));
                _edges[id]=new(id,a.NodeId,b.NodeId,s.Road.Id,s.Road.StreetId,a.Point.Distance(b.Point),s.Road.Width,s.Road.Surface,s.Road.Modes,s.Road.WinterBlocked,s.Road.GateKey);
            }
        }
        if(_measureRebuild)
        {
            var finishedUsec=global::Godot.Time.GetTicksUsec();
            var allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;
            // Capture before formatting/printing: one opt-in record per rebuild,
            // never per pair. Static Engine counters require no scene-tree owner.
            global::Godot.GD.Print(string.Create(CultureInfo.InvariantCulture,
                $"address-graph-rebuild: ticks_us={finishedUsec} process_frame={(global::Godot.Engine.GetProcessFrames())} physics_frame={(global::Godot.Engine.GetPhysicsFrames())} "
                + $"elapsed_us={finishedUsec-startedUsec} allocated_thread_bytes={allocatedBytes} roads={_roads.Count} segments={segments.Count} "
                + $"pairs={(long)segments.Count*(segments.Count-1)/2} broadphase={(useBroadphase?1:0)} broadphase_rejected_pairs={broadphaseRejectedPairs} "
                + $"accepted_pairs={acceptedPairs} nodes={_nodes.Count} edges={_edges.Count}"));
        }
    }
    private static int Compare(SettlementPoint a, SettlementPoint b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z);
    private static IEnumerable<(double T,double U)> Intersections(SettlementPoint a, SettlementPoint b, SettlementPoint c, SettlementPoint d)
    {
        var rx=b.X-a.X;var rz=b.Z-a.Z;var sx=d.X-c.X;var sz=d.Z-c.Z;
        var den=rx*sz-rz*sx;var qx=c.X-a.X;var qz=c.Z-a.Z;
        if(Math.Abs(den)>1e-9)
        {
            var t=(qx*sz-qz*sx)/den;var u=(qx*rz-qz*rx)/den;
            if(t>=-1e-8 && t<=1+1e-8 && u>=-1e-8 && u<=1+1e-8) yield return (Math.Clamp(t,0,1),Math.Clamp(u,0,1));
            yield break;
        }
        if(Math.Abs(qx*rz-qz*rx)>1e-7) yield break;
        // Collinear partial overlap must split at both endpoints.
        foreach(var p in new[]{a,b,c,d})
        {
            var pa=Project(p,a,b);var pb=Project(p,c,d);
            if(pa.Point.DistanceXZ(p)<.00001 && pb.Point.DistanceXZ(p)<.00001) yield return (pa.T,pb.T);
        }
    }
    public static (SettlementPoint Point,double T) Project(SettlementPoint p, SettlementPoint a, SettlementPoint b)
    {
        var dx=b.X-a.X;var dz=b.Z-a.Z;var length=dx*dx+dz*dz;
        var t=length<1e-12 ? 0 : Math.Clamp(((p.X-a.X)*dx+(p.Z-a.Z)*dz)/length,0,1);
        return(a.Lerp(b,t),t);
    }
    public (SettlementGraphEdge Edge, SettlementPoint Point, double Distance)? Nearest(SettlementPoint point, string? streetId=null, SettlementTravelMode mode=SettlementTravelMode.Foot, bool winter=true,Func<SettlementGraphEdge,bool>? filter=null)
    {
        return _edges.Values.Where(e=>(streetId is null || e.StreetId==streetId) && Allowed(e,mode,winter) && (filter is null || filter(e)))
            .Select(e=> { var projection=Project(point,_nodes[e.A].Position,_nodes[e.B].Position).Point; return (Edge:e,Point:projection,Distance:projection.DistanceXZ(point)); })
            .OrderBy(r=>r.Distance).ThenBy(r=>r.Edge.Id,StringComparer.Ordinal).Select(r=>((SettlementGraphEdge Edge,SettlementPoint Point,double Distance)?)r).FirstOrDefault();
    }
    public string? NodeAt(SettlementPoint point) => _nodes.Values.Where(n=>n.Position.DistanceXZ(point)<.001).OrderBy(n=>n.Id,StringComparer.Ordinal).FirstOrDefault()?.Id;
    public bool Allowed(SettlementGraphEdge edge,SettlementTravelMode mode,bool winter) =>
        (edge.Modes&mode)!=0 && !_blockedRoads.Contains(edge.RoadId) && (!winter || !edge.WinterBlocked)
        && (edge.GateKey.Length==0 || GateIsOpen?.Invoke(edge.GateKey)==true);
    public HashSet<string> ReachableNodes(string start,SettlementTravelMode mode,bool winter=true)
    {
        var seen=new HashSet<string>(StringComparer.Ordinal);
        if(!_nodes.ContainsKey(start))return seen;
        var adjacent=new Dictionary<string,List<string>>(StringComparer.Ordinal);
        foreach(var edge in _edges.Values.Where(e=>Allowed(e,mode,winter)))
        {
            if(!adjacent.TryGetValue(edge.A,out var a)){a=[];adjacent[edge.A]=a;}a.Add(edge.B);
            if(!adjacent.TryGetValue(edge.B,out var b)){b=[];adjacent[edge.B]=b;}b.Add(edge.A);
        }
        var pending=new Queue<string>();seen.Add(start);pending.Enqueue(start);
        while(pending.TryDequeue(out var node))
            if(adjacent.TryGetValue(node,out var next))foreach(var id in next)if(seen.Add(id))pending.Enqueue(id);
        return seen;
    }
    public IReadOnlyList<string> Route(string start,string destination,SettlementTravelMode mode,bool winter=true,string? streetId=null)
    {
        if(!_nodes.ContainsKey(start)||!_nodes.ContainsKey(destination)) return [];
        var distances=new Dictionary<string,double>(StringComparer.Ordinal){{start,0}};
        var previous=new Dictionary<string,string>(StringComparer.Ordinal);
        var queue=new PriorityQueue<string,(double,string)>();
        queue.Enqueue(start,(0,start));
        while(queue.TryDequeue(out var node,out var priority))
        {
            if(priority.Item1>distances[node]) continue;
            if(node==destination) break;
            foreach(var edge in _edges.Values.Where(e=>(e.A==node||e.B==node) && Allowed(e,mode,winter) && (streetId is null||e.StreetId==streetId)).OrderBy(e=>e.Id,StringComparer.Ordinal))
            {
                var next=edge.A==node?edge.B:edge.A;
                var surfaceCost=edge.Surface is "snow_deep" or "ice" ? 1.7 : 1;
                var candidate=distances[node]+edge.Length*surfaceCost;
                if(distances.TryGetValue(next,out var old)&&old<=candidate) continue;
                distances[next]=candidate;previous[next]=node;queue.Enqueue(next,(candidate,next));
            }
        }
        if(!distances.ContainsKey(destination))return [];
        var path=new List<string>{destination};
        while(path[^1]!=start)path.Add(previous[path[^1]]);
        path.Reverse();return path;
    }
    public bool CanTraverse(SettlementPoint from, SettlementPoint to, SettlementTravelMode mode,out string reason,bool winter=true)
    {
        var radius=mode switch{SettlementTravelMode.Car=>.92,SettlementTravelMode.HorseCart=>.95,SettlementTravelMode.Motorcycle=>.50,_=>.32};
        var count=Math.Max(1,(int)Math.Ceiling(from.DistanceXZ(to)/.3));
        for(var i=0;i<=count;i++)
        {
            var point=from.Lerp(to,(double)i/count);
            var road=Nearest(point,mode:mode,winter:winter);
            if(road is null || road.Value.Distance+radius>road.Value.Edge.Width*.5+.10)
            {reason="Здесь нет подходящей дороги.";return false;}
            if(Math.Abs(point.Y-road.Value.Point.Y)>2.5)
            {reason="Этот проезд находится на другом уровне.";return false;}
        }
        reason="";return true;
    }
}
