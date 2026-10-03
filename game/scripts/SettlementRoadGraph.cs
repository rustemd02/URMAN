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
    // Cell-index storage is bounded so a pathological import cannot allocate an
    // unbounded bucket table. A segment that would overrun the budget is not
    // dropped: it is compared against every other segment in the candidate loop
    // below, exactly like the former whole-build fallback but only for that
    // segment. The candidate set therefore only grows, and the narrow phase and
    // its pair order are unchanged.
    private const long MaximumIndexMemberships = 65_536;
    // Reused per segment by the topology phase; rebuilt from scratch by every
    // publication, so no state can leak between rebuilds.
    private readonly List<Cut> cutScratch = new();
    private readonly HashSet<string> cutNodeScratch = new(StringComparer.Ordinal);
    // Total order for Cut, reproducing the former
    // OrderBy(T).ThenBy(Key, Ordinal) exactly: parameter, then the same ordinal
    // key comparison, then insertion position. The last term is what keeps the
    // unstable in-place sort equal to the stable pipeline when a segment holds
    // two cuts with the same parameter and the same junction key.
    private static readonly Comparison<Cut> CutOrder = static (a,b) =>
    {
        var byT=a.T.CompareTo(b.T);
        if(byT!=0)return byT;
        var byKey=string.CompareOrdinal(a.Key,b.Key);
        return byKey!=0?byKey:a.Order.CompareTo(b.Order);
    };
    // The grouping phase consumed segments.SelectMany(s=>s.Cuts).OrderBy(c=>c.Key,
    // StringComparer.Ordinal), which is a stable sort. Sorting the collected
    // sequence with (key, then its position in that sequence) reproduces exactly
    // the same permutation while replacing the iterator, buffer and key-array
    // allocations of the LINQ pipeline.
    private static readonly Comparison<(Cut Cut,int Order)> GroupOrder = static (left,right) =>
    {
        var byKey=string.CompareOrdinal(left.Cut.Key,right.Cut.Key);
        return byKey!=0?byKey:left.Order.CompareTo(right.Order);
    };
    // Node lookup by one-metre cell. NodeAt's tolerance is .001, so any matching
    // node lies in the query cell or one of its eight neighbours: the candidate set
    // is a superset and the same distance test and smallest-id tie-break are applied
    // to it, so the returned id is unchanged while the per-call scan over every node
    // (once per verified route on every publication) disappears. _nodes is written
    // only by RebuildCore, which rebuilds this index right after filling it.
    private readonly Dictionary<long,List<string>> _nodeCells=new();
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
    private sealed class Cut(SettlementPoint point, string key, double t, int order)
    {
        public SettlementPoint Point = point;
        public string Key = key;
        public double T = t;
        // Insertion position inside its segment. Two cuts may share both the
        // parameter and the key (a collinear overlap whose endpoints fall into
        // different junction groups), so the original pipeline's stable order is
        // only reproducible with this tie-break.
        public int Order = order;
        public string NodeId = "";
    }
    private sealed class Segment(SettlementRoad road, SettlementPoint a, SettlementPoint b, string key, string aKey, string bKey)
    {
        public SettlementRoad Road = road;
        public SettlementPoint A = a, B = b;
        public readonly double DeltaX=b.X-a.X, DeltaZ=b.Z-a.Z;
        public string Key = key;
        public List<Cut> Cuts = [new(a,aKey,0,0),new(b,bKey,1,1)];
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
    // The narrow phase tests tens of thousands of pairs, so its two predicates
    // read packed arrays of exactly the values the Segment objects already hold
    // (BoundsFor results and the endpoints' deltas). Copying a 40-byte bounds
    // struct out of two scattered objects per pair was measurable in
    // intersection_us; the arithmetic and therefore the decisions are unchanged.
    private static long CellKey(int x,int z)=>((long)x<<32)|(uint)z;
    private static bool MayIntersect(in SegmentBounds x,in SegmentBounds y,double ax,double az,double bx,double bz)
    {
        if(!x.Cullable||!y.Cullable||!(x.MaxX<y.MinX||y.MaxX<x.MinX||x.MaxZ<y.MinZ||y.MaxZ<x.MinZ))return true;
        // Do not cull an ill-conditioned non-parallel pair: cancellation in the
        // old t/u division can dominate its geometric endpoint tolerance. With
        // coordinates within 10 km and this determinant condition, rounding is
        // well below the retained 1e-5 pad. The old collinear branch uses clamped
        // projections, already covered by the padded bounds above.
        return RetainDisjointPair(ax,az,bx,bz);
    }
    private static bool RetainDisjointPair(double ax,double az,double bx,double bz)
    {
        var productA=ax*bz;
        var productB=az*bx;
        var determinant=Math.Abs(productA-productB);
        return determinant>1e-9&&determinant<=.001*(Math.Abs(productA)+Math.Abs(productB));
    }
    public void Rebuild(IReadOnlyDictionary<string, SettlementStreet> streets)=>RebuildCore(streets,true);
    // Same builder and narrow phase, with candidate indices disabled for equivalence
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
                var segmentKey = road.Id + "/" + (string.CompareOrdinal(aKey,bKey)<=0 ? aKey+"/"+bKey : bKey+"/"+aKey);
                segments.Add(new(road,points[i],points[i+1],segmentKey,aKey,bKey));
            }
        }
        var bounds=new SegmentBounds[segments.Count];
        var deltaX=new double[segments.Count];
        var deltaZ=new double[segments.Count];
        for(var i=0;i<segments.Count;i++)
        {
            bounds[i]=segments[i].Bounds;
            deltaX[i]=segments[i].DeltaX;
            deltaZ[i]=segments[i].DeltaZ;
        }
        var intersectionsStartedUsec = _measureRebuild ? global::Godot.Time.GetTicksUsec() : 0UL;
        Dictionary<long,List<int>>? spatialCells=null,directionCells=null;
        (int MinX,int MaxX,int MinZ,int MaxZ,int DirX,int DirZ,int OppositeX,int OppositeZ)[]? pairCells=null;
        // Segments left out of the cell index because their padded AABB exceeds
        // the storage budget. They are still tested against every other segment.
        List<int>? unindexed=null;
        bool[]? indexable=null;
        if(useBroadphase)
        {
            pairCells=new (int,int,int,int,int,int,int,int)[segments.Count];
            indexable=new bool[segments.Count];
            var cellsBySegment=new long[segments.Count];
            long memberships=0;
            var supported=true;
            for(var i=0;i<segments.Count;i++)
            {
                var cellBounds=bounds[i];var norm=Math.Max(Math.Abs(deltaX[i]),Math.Abs(deltaZ[i]));
                if(!cellBounds.Cullable||!double.IsFinite(cellBounds.MinX)||!double.IsFinite(cellBounds.MaxX)||
                    !double.IsFinite(cellBounds.MinZ)||!double.IsFinite(cellBounds.MaxZ)||!double.IsFinite(norm)||norm<=0)
                { supported=false;break; }
                var dx=deltaX[i]/norm;var dz=deltaZ[i]/norm;
                if(!double.IsFinite(dx)||!double.IsFinite(dz)) { supported=false;break; }
                var cell=(MinX:(int)Math.Floor(cellBounds.MinX/8),MaxX:(int)Math.Floor(cellBounds.MaxX/8),
                    MinZ:(int)Math.Floor(cellBounds.MinZ/8),MaxZ:(int)Math.Floor(cellBounds.MaxZ/8),
                    DirX:(int)Math.Floor(dx*16),DirZ:(int)Math.Floor(dz*16),
                    OppositeX:(int)Math.Floor(-dx*16),OppositeZ:(int)Math.Floor(-dz*16));
                pairCells[i]=cell;
                cellsBySegment[i]=(long)(cell.MaxX-cell.MinX+1)*(cell.MaxZ-cell.MinZ+1);
                memberships+=cellsBySegment[i];
            }
            if(!supported) pairCells=null;
            else
            {
                unindexed=[];
                Array.Fill(indexable,true);
                if(memberships>MaximumIndexMemberships)
                {
                    // Bound index storage without disabling the whole build: the
                    // fewest, largest segments leave the cell index and are later
                    // compared against every other segment. Demoting the largest
                    // first keeps the many small segments indexed.
                    var order=new int[segments.Count];
                    for(var i=0;i<order.Length;i++)order[i]=i;
                    Array.Sort(order,(x,y)=>cellsBySegment[y].CompareTo(cellsBySegment[x]));
                    foreach(var i in order)
                    {
                        if(memberships<=MaximumIndexMemberships)break;
                        indexable[i]=false;unindexed.Add(i);memberships-=cellsBySegment[i];
                    }
                }
                spatialCells=[];directionCells=[];
                for(var i=0;i<pairCells.Length;i++)
                {
                    if(!indexable[i]) continue;
                    var cell=pairCells[i];
                    for(var x=cell.MinX;x<=cell.MaxX;x++) for(var z=cell.MinZ;z<=cell.MaxZ;z++)
                    {
                        if(!spatialCells.TryGetValue(CellKey(x,z),out var bucket))spatialCells[CellKey(x,z)]=bucket=[];
                        bucket.Add(i);
                    }
                    if(!directionCells.TryGetValue(CellKey(cell.DirX,cell.DirZ),out var directions))
                        directionCells[CellKey(cell.DirX,cell.DirZ)]=directions=[];
                    directions.Add(i);
                }
            }
        }
        if(pairCells is not null)
        {
            var marks=new int[segments.Count];
            var candidates=new List<int>();
            for(var i=0;i<segments.Count;i++)
            {
                candidates.Clear();var stamp=i+1;var cell=pairCells[i];
                void Add(int j)
                {
                    if(j<=i||marks[j]==stamp)return;
                    marks[j]=stamp;candidates.Add(j);
                }
                void Include(List<int> entries,bool directionOnly=false)
                {
                    foreach(var j in entries)
                    {
                        if(j<=i||marks[j]==stamp)continue;
                        marks[j]=stamp;
                        // Spatial candidates have already been included. For
                        // directions alone retain exactly the numeric fallback,
                        // avoiding sorted lists of disjoint parallel segments.
                        if(directionOnly&&!RetainDisjointPair(deltaX[i],deltaZ[i],deltaX[j],deltaZ[j]))continue;
                        candidates.Add(j);
                    }
                }
                if(indexable![i])
                {
                    for(var x=cell.MinX;x<=cell.MaxX;x++) for(var z=cell.MinZ;z<=cell.MaxZ;z++)
                        if(spatialCells!.TryGetValue(CellKey(x,z),out var spatialBucket))Include(spatialBucket);
                    // MayIntersect also retains disjoint, ill-conditioned pairs.
                    // With max-component-normalized directions its determinant
                    // condition implies distance <= .002 plus roundoff from v or
                    // -v on the unit square. A 1/16 cell and its neighbours leave
                    // a wide margin around that bound.
                    for(var sign=0;sign<2;sign++)
                    {
                        var dirX=sign==0?cell.DirX:cell.OppositeX;
                        var dirZ=sign==0?cell.DirZ:cell.OppositeZ;
                        for(var x=dirX-1;x<=dirX+1;x++) for(var z=dirZ-1;z<=dirZ+1;z++)
                            if(directionCells!.TryGetValue(CellKey(x,z),out var directionBucket))Include(directionBucket,directionOnly:true);
                    }
                    // Segments left out of the index are compared against every
                    // later segment, so add them here. The loop is empty unless
                    // the build ran into the storage budget.
                    foreach(var j in unindexed!) Add(j);
                }
                else
                {
                    // A demoted segment carries no cell entry: test it against
                    // every later segment instead of a stale default cell.
                    for(var j=i+1;j<segments.Count;j++) Add(j);
                }
                // Keep the original i/j order, including insertion order of cuts.
                candidates.Sort();
                if(_measureRebuild)broadphaseRejectedPairs+=segments.Count-i-1-candidates.Count;
                foreach(var j in candidates)VisitPair(i,j);
            }
        }
        else
        {
            for(var i=0;i<segments.Count;i++) for(var j=i+1;j<segments.Count;j++)VisitPair(i,j);
        }
        void VisitPair(int i,int j)
        {
            var a=segments[i];var b=segments[j];
            if(useBroadphase&&!MayIntersect(in bounds[i],in bounds[j],deltaX[i],deltaZ[i],deltaX[j],deltaZ[j]))
            {
                if(_measureRebuild)broadphaseRejectedPairs++;
                return;
            }
            var accepted=AddIntersections(a,b);
            if(_measureRebuild&&accepted)acceptedPairs++;
        }
        // Merge only physically coincident junctions; never round a gap shut.
        var groupingStartedUsec = _measureRebuild ? global::Godot.Time.GetTicksUsec() : 0UL;
        var groups = new List<List<Cut>>();
        // Index only the original anchor: this proximity relation is not
        // transitive. One-metre cells avoid tiny tolerance quotient rounding;
        // extreme imports keep the original exhaustive candidate scan.
        var groupCullable=useBroadphase;
        if(groupCullable) for(var i=0;i<bounds.Length;i++) if(!bounds[i].Cullable){groupCullable=false;break;}
        var groupCells = groupCullable ? new Dictionary<long,List<int>>() : null;
        // OrderBy(Key, Ordinal) is a stable sort, so equal keys keep the order of
        // segments.SelectMany(s=>s.Cuts). The explicit comparator reproduces that
        // permutation exactly (key, then the position in that sequence) without the
        // iterator, buffer and key-array allocations of the LINQ pipeline.
        // Every segment starts with two cuts, so this is the common size; a
        // segment with junctions needs a little more and the list grows once.
        var groupSequence=new List<(Cut Cut,int Order)>(segments.Count*2);
        var groupPosition=0;
        foreach(var segment in segments) foreach(var cut in segment.Cuts) groupSequence.Add((cut,groupPosition++));
        groupSequence.Sort(GroupOrder);
        foreach(var (cut,_) in groupSequence)
        {
            List<Cut>? group=null;
            (int X,int Z) cell=default;
            if(groupCells is null)
                group=groups.FirstOrDefault(g=>g[0].Point.DistanceXZ(cut.Point)<.00001 && Math.Abs(g[0].Point.Y-cut.Point.Y)<.5);
            else
            {
                cell=((int)Math.Floor(cut.Point.X),(int)Math.Floor(cut.Point.Z));
                var winner=int.MaxValue;
                for(var x=cell.X-1;x<=cell.X+1;x++) for(var z=cell.Z-1;z<=cell.Z+1;z++)
                {
                    if(!groupCells.TryGetValue(CellKey(x,z),out var candidates))continue;
                    foreach(var index in candidates)
                    {
                        if(index>=winner)break; // Bucket entries retain creation order.
                        var anchor=groups[index][0].Point;
                        if(anchor.DistanceXZ(cut.Point)<.00001 && Math.Abs(anchor.Y-cut.Point.Y)<.5)
                        { winner=index;break; }
                    }
                }
                if(winner!=int.MaxValue)group=groups[winner];
            }
            if(group is null)
            {
                if(groupCells is not null)
                {
                    if(!groupCells.TryGetValue(CellKey(cell.X,cell.Z),out var candidates))groupCells[CellKey(cell.X,cell.Z)]=candidates=[];
                    candidates.Add(groups.Count);
                }
                groups.Add([cut]);
            }
            else group.Add(cut);
        }
        var topologyStartedUsec = _measureRebuild ? global::Godot.Time.GetTicksUsec() : 0UL;
        foreach(var group in groups)
        {
            // Members already follow the globally stable ordinal key order.
            var committedKey=group[0].Key;
            foreach(var cut in group)
                if(!cut.Key.StartsWith("junction/",StringComparison.Ordinal)){committedKey=cut.Key;break;}
            var id=SettlementRegistry.StableId("ND",committedKey);
            var point=group[0].Point;
            _nodes[id]=new(id,point);
            foreach(var cut in group) cut.NodeId=id;
        }
        foreach(var s in segments)
        {
            // OrderBy(T).ThenBy(Key, Ordinal) is one sort on this composite key.
            // The later OrderBy(T) in the former pipeline was a no-op: the
            // sequence was already non-decreasing in (T, Key) and a stable sort
            // by T preserves the order of equal-T elements. GroupBy(NodeId) plus
            // First() is the first occurrence of each node id in that order,
            // which an ordered dedupe reproduces exactly.
            s.Cuts.Sort(CutOrder);
            cutScratch.Clear();cutNodeScratch.Clear();
            foreach(var cut in s.Cuts)
                if(cutNodeScratch.Add(cut.NodeId))cutScratch.Add(cut);
            for(var i=0;i<cutScratch.Count-1;i++)
            {
                var a=cutScratch[i];var b=cutScratch[i+1];
                if(a.NodeId==b.NodeId || a.Point.DistanceXZ(b.Point)<.00001) continue;
                var pairKey=string.CompareOrdinal(a.NodeId,b.NodeId)<=0 ? a.NodeId+"|"+b.NodeId : b.NodeId+"|"+a.NodeId;
                var id=SettlementRegistry.StableId("ED",s.Road.Id+"|"+pairKey);
                _edges[id]=new(id,a.NodeId,b.NodeId,s.Road.Id,s.Road.StreetId,a.Point.Distance(b.Point),s.Road.Width,s.Road.Surface,s.Road.Modes,s.Road.WinterBlocked,s.Road.GateKey);
            }
        }
        _nodeCells.Clear();
        foreach(var node in _nodes.Values)
        {
            var key=CellKey((int)Math.Floor(node.Position.X),(int)Math.Floor(node.Position.Z));
            if(!_nodeCells.TryGetValue(key,out var bucket))_nodeCells[key]=bucket=[];
            bucket.Add(node.Id);
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
                + $"segment_us={intersectionsStartedUsec-startedUsec} intersection_us={groupingStartedUsec-intersectionsStartedUsec} "
                + $"group_us={topologyStartedUsec-groupingStartedUsec} topology_us={finishedUsec-topologyStartedUsec} group_index={(groupCells is null?0:1)} pair_index={(pairCells is null?0:1)} "
                + $"unindexed_segments={(unindexed is null?0:unindexed.Count)} "
                + $"pairs={(long)segments.Count*(segments.Count-1)/2} broadphase={(useBroadphase?1:0)} broadphase_rejected_pairs={broadphaseRejectedPairs} "
                + $"accepted_pairs={acceptedPairs} nodes={_nodes.Count} edges={_edges.Count}"));
        }
    }
    private static int Compare(SettlementPoint a, SettlementPoint b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z);
    private static bool AddIntersections(Segment a, Segment b)
    {
        string? junctionKey=null;
        var rx=a.DeltaX;var rz=a.DeltaZ;var sx=b.DeltaX;var sz=b.DeltaZ;
        var den=rx*sz-rz*sx;var qx=b.A.X-a.A.X;var qz=b.A.Z-a.A.Z;
        if(Math.Abs(den)>1e-9)
        {
            var t=(qx*sz-qz*sx)/den;var u=(qx*rz-qz*rx)/den;
            if(t>=-1e-8 && t<=1+1e-8 && u>=-1e-8 && u<=1+1e-8) Append(Math.Clamp(t,0,1),Math.Clamp(u,0,1));
            return junctionKey is not null;
        }
        if(Math.Abs(qx*rz-qz*rx)>1e-7) return false;
        // Collinear partial overlap must split at both endpoints.
        for(var i=0;i<4;i++)
        {
            var p=i switch{0=>a.A,1=>a.B,2=>b.A,_=>b.B};
            var pa=Project(p,a.A,a.B);var pb=Project(p,b.A,b.B);
            if(pa.Point.DistanceXZ(p)<.00001 && pb.Point.DistanceXZ(p)<.00001) Append(pa.T,pb.T);
        }
        return junctionKey is not null;

        void Append(double t,double u)
        {
            var p=a.A.Lerp(a.B,t);var q=b.A.Lerp(b.B,u);
            if(Math.Abs(p.Y-q.Y)>.5)return; // Bridges at different levels do not connect.
            // Direct calls keep the capture on the stack. Non-intersecting
            // pairs allocate no iterator, endpoint array or junction identity.
            junctionKey??="junction/" + (string.CompareOrdinal(a.Key,b.Key)<=0 ? a.Key+"|"+b.Key : b.Key+"|"+a.Key);
            a.Cuts.Add(new(p,junctionKey,t,a.Cuts.Count));
            b.Cuts.Add(new(q,junctionKey,u,b.Cuts.Count));
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
        (SettlementGraphEdge Edge, SettlementPoint Point, double Distance)? nearest=null;
        foreach(var edge in _edges.Values)
        {
            if((streetId is not null && edge.StreetId!=streetId) || !Allowed(edge,mode,winter) || (filter is not null && !filter(edge)))continue;
            var projection=Project(point,_nodes[edge.A].Position,_nodes[edge.B].Position).Point;
            var distance=projection.DistanceXZ(point);
            // Match the existing distance/ordinal ordering, including NaN and
            // exact ties, without materializing a sorted sequence per query.
            var order=nearest is { } best ? distance.CompareTo(best.Distance) : -1;
            if(order<0 || order==0 && string.CompareOrdinal(edge.Id,nearest!.Value.Edge.Id)<0)
                nearest=(edge,projection,distance);
        }
        return nearest;
    }
    // The same predicate as OrderBy(Id, Ordinal).FirstOrDefault(), without the
    // LINQ buffer: the ordinally smallest matching id does not depend on the
    // dictionary's enumeration order.
    public string? NodeAt(SettlementPoint point)
    {
        string? best=null;
        var cellX=(int)Math.Floor(point.X);var cellZ=(int)Math.Floor(point.Z);
        for(var x=cellX-1;x<=cellX+1;x++) for(var z=cellZ-1;z<=cellZ+1;z++)
        {
            if(!_nodeCells.TryGetValue(CellKey(x,z),out var bucket))continue;
            foreach(var id in bucket)
            {
                var node=_nodes[id];
                if(node.Position.DistanceXZ(point)<.001&&(best is null||string.CompareOrdinal(node.Id,best)<0))best=node.Id;
            }
        }
        return best;
    }
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
