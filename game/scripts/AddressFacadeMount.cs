using Godot;

namespace Urman.Godot;

/// <summary>Mounts the entire physical plate on an exterior facade. Exact polygon
/// subtraction checks openings; a foreground prism includes posts and annexes.
/// The plate is a village house plate, 0.60 x 0.23 m (2026-09-24: the author
/// found the former 1.18 m street-sign size too big), the same on every house,
/// and hangs beside the door at human reading height so a row of plates reads
/// level (author 2026-10-04: no roof edges, plinths, fences or invented
/// platforms; when the facade cannot carry the plate beside the entrance, the
/// import records the failure instead of inventing a mount).</summary>
internal static class AddressFacadeMount
{
    internal const float HalfWidth=.30f, HalfHeight=.115f;
    internal const float RivetX=HalfWidth-.03f, RivetY=HalfHeight-.03f;
    // Plate centre above the facade's own ground. This is a human reading band
    // beside the entrance, not "under the eave": 1.5 m is the lowest comfortable
    // letter line, 2.1 m the highest, 1.8 m the target. A wall shorter than the
    // band yields no candidate, and the mount honestly fails.
    internal const float MountHeight=1.8f, MountLow=1.5f, MountHigh=2.1f;
    // The board may follow hewn-log courses and casing by at most this much, so
    // it still reads as screwed to the wall - never a ledge, shelf, canopy or
    // roof verge pushed out in front of it.
    internal const float MaxCladdingProud=.09f;
    // The plate offset in front of the drawn wall plane (2.1 cm).
    internal const float MountOffset=.021f;
    internal const string FenceMountSuffix=" (parcel fence mount)";
    internal sealed record Triangle(Vector2 A,Vector2 B,Vector2 C);
    internal sealed record Coverage(bool Supported,double MissingArea,string Owner,string Reason,bool TimberCladding=false);
    private sealed record Face(Vector3 A,Vector3 B,Vector3 C,string Owner);
    private sealed class Plane(float depth,string owner)
    {
        public float Depth=depth;
        public string Owner=owner;
        public readonly List<Triangle> Triangles=[];
    }

    private static readonly string[] NonWallParts =
    [
        "_Left_","_Right_","Plinth","Step","Stair","Bench","Shelf","Ledge","Platform","Fence","Gate",
        "Rail","Beam","Post","Canopy","Awning","Rafter","Verge","Gutter","Sill","Downpipe","Splash",
        "Trough","Spout","Towel","Ladder","Woodpile","Path","Sign"
    ];

    public static bool Eligible(string name)
    {
        if(new[]{"Roof","Snow","Window","Door","Foundation","Footing","Chimney","Interior","Rear","Back","SeniSide"}
            .Any(s=>name.Contains(s,StringComparison.Ordinal)))return false;
        // Walls only: a plate screwed to a plinth, step, bench, fence, gate post
        // or free-standing prop reads as a strange platform, not an address.
        if(NonWallParts.Any(s=>name.Contains(s,StringComparison.Ordinal)))return false;
        return name.Contains("_Wall_",StringComparison.Ordinal)||name.Contains("_Body_",StringComparison.Ordinal)
            ||name.Contains("GableFace",StringComparison.Ordinal)||name.Contains("GablePanel",StringComparison.Ordinal)
            ||name.Contains("_Gable_",StringComparison.Ordinal)||name.Contains("BoardedGable",StringComparison.Ordinal)
            ||name=="CoreWallVolume"||name.StartsWith("MosqueHallEast",StringComparison.Ordinal);
    }

    public static bool TryFind(Node3D building,Vector3 door,Vector3 outward,Node3D world,out Vector3 point,out Vector3 mountedOutward,out string owner,out string failure,string? explicitExteriorWall=null)
    {
        if(TryFindOnWalls(building,door,outward,world,out point,out mountedOutward,out owner,out failure,explicitExteriorWall))return true;
        // No façade wall beside this door carries the plate at reading height.
        // Parcel fences, gate posts, plinths and props are deliberately not a
        // fallback (author 2026-10-04): a random rail or ledge reads as a strange
        // platform. The import records SIGN_MOUNT_NOT_FOUND and keeps failing;
        // only a household's own street gate (TryFindOnOwnYardFence, the
        // documented gate-entrance exception) may carry the plate instead.
        failure+="; no façade wall for the plate beside the door (fences and props are not facades)";
        return false;
    }

    private static bool TryFindOnWalls(Node3D building,Vector3 door,Vector3 outward,Node3D world,out Vector3 point,out Vector3 mountedOutward,out string owner,out string failure,string? explicitExteriorWall=null)
    {
        point=default;mountedOutward=default;owner="";failure="";outward.Y=0;outward=outward.Normalized();
        // Seni-door houses face their side entry away from the street: the
        // door-facing planes are narrow and pierced while a blank street wall
        // stands around the corner. Try the door orientation first, then the
        // other three cardinals with a distance penalty — the sightline check
        // below still guarantees the plate reads from the street.
        var tried=new List<string>();
        foreach(var sweep in new[]{outward,-outward,new Vector3(outward.Z,0,-outward.X),new Vector3(-outward.Z,0,outward.X)})
        {
            if(TryFindOnWallsOriented(building,door,sweep,world,out point,out owner,out var orientedFailure,explicitExteriorWall,oriented:sweep!=outward))
            {
                mountedOutward=sweep;
                if(sweep!=outward)owner+=" (street-facing wall; seni door faces elsewhere)";
                return true;
            }
            tried.Add($"[{sweep.X:0.0},{sweep.Z:0.0}]: "+orientedFailure);
        }
        failure=string.Join("; ",tried);
        return false;
    }

    private static bool TryFindOnWallsOriented(Node3D building,Vector3 door,Vector3 outward,Node3D world,out Vector3 point,out string owner,out string failure,string? explicitExteriorWall,bool oriented=false)
    {
        point=default;owner="";failure="";outward.Y=0;outward=outward.Normalized();
        var right=new Vector3(outward.Z,0,-outward.X);
        var planes=Planes(building,outward,right,explicitExteriorWall);
        // The plate hangs on the wall segment the door pierces (or its gate),
        // beside the entrance at human reading height: the free rectangle
        // nearest the door axis, target centre 1.8 m above the facade's own
        // ground. No normative height is claimed (B-R28-03).
        var doorAlong=door.Dot(right);
        var candidates=new List<(float Score,Vector2 Center,Plane Plane)>();
        foreach(var plane in planes)
        {
            var vertices=plane.Triangles.SelectMany(t=>new[]{t.A,t.B,t.C}).ToArray();
            var minX=vertices.Min(p=>p.X)+HalfWidth+.025f;var maxX=vertices.Max(p=>p.X)-HalfWidth-.025f;
            if(minX>maxX)continue;
            // The plate must sit beside the door, not above it: the door
            // pierces this wall segment, so require a full plate width of
            // clearance from the door axis along the facade. When the door
            // axis lies outside this plane's span (side door vs street wall),
            // the whole span is already beside the door.
            var ranges=new List<(float Lo,float Hi)>();
            if(doorAlong<minX||doorAlong>maxX)ranges.Add((minX,maxX));
            else
            {
                var clearance=minX;var doorClear=doorAlong+HalfWidth+.10f;
                if(doorClear>minX&&doorClear<maxX)clearance=doorClear;
                var lo=Math.Max(minX,clearance);var hi=maxX;
                // When the door-side segment cannot fit the plate, the far side
                // (left of the door) is tried as an explicit fallback.
                if(lo<=hi)ranges.Add((lo,hi));
                var farHi=Math.Min(maxX,doorAlong-HalfWidth-.10f);
                if(minX<=farHi)ranges.Add((minX,farHi));
            }
            if(ranges.Count==0)continue;
            foreach(var (rangeLo,rangeHi) in ranges)
            foreach(var x in Samples(rangeLo,rangeHi,Math.Clamp(doorAlong,rangeLo,rangeHi),.08f))
            {
                // A recessed annex entrance may lie several metres down the
                // slope. Judge plate height against the facade's own ground.
                var facadeGround=Act1ConnectedWorld.AddressGround(right*x+outward*plane.Depth).Y;
                var minY=Math.Max(vertices.Min(p=>p.Y)+HalfHeight+.025f,facadeGround+MountLow);
                var maxY=Math.Min(vertices.Max(p=>p.Y)-HalfHeight-.025f,facadeGround+MountHigh);
                if(minY>maxY)continue;
                var targetY=Math.Clamp(facadeGround+MountHeight,minY,maxY);
                foreach(var y in Samples(minY,maxY,targetY,.06f))
                {
                    var center=new Vector2(x,y);
                    var fastCheck=BoardJoints(plane.Owner)?FastenerPoints(center):RequiredMountPoints(center);
                    if(!fastCheck.All(p=>plane.Triangles.Any(t=>Contains(t,p))))continue;
                    var orientationPenalty=oriented?4f:0f;
                    // Prefer the legal spot closest to the door axis and closest
                    // to the 1.8 m target; the wall plane itself must be the
                    // door's own plane when possible.
                    var score=(x-doorAlong)*(x-doorAlong)+(y-targetY)*(y-targetY)*1.6f
                        +Math.Abs(plane.Depth-door.Dot(outward))*.12f+orientationPenalty;
                    candidates.Add((score,center,plane));
                }
            }
        }
        if(candidates.Count==0){failure="No exterior facade rectangle with supported fasteners; eligible planes="+planes.Count;return false;}
        // Include sibling porch meshes and nearby buildings, not just eligible
        // wall owners. Bound the mesh read once, before testing candidates.
        var bounds=Bounds(candidates.Select(c=>right*c.Center.X+Vector3.Up*c.Center.Y+outward*c.Plane.Depth)).Grow(4.2f);
        var faces=VisibleFaces(world,bounds);
        var refusals=new Dictionary<string,int>(StringComparer.Ordinal);
        void Refused(string reason){refusals[reason]=refusals.GetValueOrDefault(reason)+1;}
        foreach(var candidate in candidates.OrderBy(c=>c.Score).ThenBy(c=>c.Plane.Owner,StringComparer.Ordinal))
        {
            var coverage=Cover(candidate.Plane.Triangles,candidate.Center,BoardJoints(candidate.Plane.Owner));
            if(!coverage.Supported){Refused(coverage.Reason+" at "+candidate.Plane.Owner);continue;}
            var p=right*candidate.Center.X+Vector3.Up*candidate.Center.Y+outward*(candidate.Plane.Depth+MountOffset);
            var mounted=ProudOfOwnCladding(building,p,outward,right);
            if(mounted.Dot(outward)-candidate.Plane.Depth>MaxCladdingProud
                &&!TimberFastenersSupported(building,mounted,outward,right))continue;
            // Check from the finished mount, not from behind its own cladding.
            if(Occluder(faces,mounted,outward,right) is { } occluder){Refused("Exterior view blocked by "+occluder);continue;}
            point=mounted;owner=candidate.Plane.Owner;return true;
        }
        failure=string.Join("; ",refusals.OrderByDescending(r=>r.Value).Take(3).Select(r=>r.Key+" ("+r.Value+" candidates)"));
        return false;
    }

    internal static Coverage Inspect(Node3D building,Vector3 point,Vector3 outward,Node3D world,string? explicitExteriorWall=null)
    {
        outward=outward.Normalized();var right=new Vector3(outward.Z,0,-outward.X);
        if(explicitExteriorWall is null&&building.HasMeta("addressSignMountOwner")
            &&building.GetMeta("addressSignMountOwner").AsString() is { } mountOwner&&mountOwner.EndsWith(FenceMountSuffix,StringComparison.Ordinal))
            return InspectOwnMember(world,mountOwner[..^FenceMountSuffix.Length],point,outward,right);
        var center=new Vector2(point.Dot(right),point.Y);
        foreach(var plane in Planes(building,outward,right,explicitExteriorWall).Where(p=>point.Dot(outward)-p.Depth is >=.001f and <=.321f)
            .OrderByDescending(p=>p.Depth))
        {
            var covered=Cover(plane.Triangles,center,BoardJoints(plane.Owner));
            if(!covered.Supported)continue;
            var timber=point.Dot(outward)-plane.Depth>.09f;
            if(timber&&!TimberFastenersSupported(building,point,outward,right))continue;
            var obstruction=Occluder(VisibleFaces(world,new Aabb(point-Vector3.One*4.2f,Vector3.One*8.4f)),point,outward,right);
            return new(obstruction is null,covered.MissingArea,plane.Owner,obstruction is null?"complete facade and exterior sightline":"occluded by "+obstruction,timber);
        }
        return new(false,4*HalfWidth*HalfHeight,"","full rim and rivets lack an eligible facade");
    }

    internal static bool StructuralTimber(string name)=>name.Contains("_Street_Log",StringComparison.Ordinal)
        ||name.Contains("_Street_Portal",StringComparison.Ordinal)&&name.Contains("_Jamb",StringComparison.Ordinal);

    // A rigid sign bridges the grooves of a log wall, but every rivet must
    // actually meet timber. The complete backing wall still owns the outline
    // and opening checks; window trim, snow and loose props cannot support it.
    private static bool TimberFastenersSupported(Node3D building,Vector3 point,Vector3 outward,Vector3 right)
    {
        var faces=Descendants(building).OfType<MeshInstance3D>()
            .Where(m=>m.Mesh is not null&&m.IsVisibleInTree()&&StructuralTimber(m.Name.ToString()))
            .SelectMany(m=>m.Mesh!.GetFaces().Select(p=>m.ToGlobal(p))).ToArray();
        foreach(var fastener in FastenerPoints(new(point.Dot(right),point.Y)))
        {
            var origin=right*fastener.X+Vector3.Up*fastener.Y+outward*point.Dot(outward);
            var supported=false;
            for(var i=0;i+2<faces.Length;i+=3)
            {
                var hit=Geometry3D.RayIntersectsTriangle(origin,-outward,faces[i],faces[i+1],faces[i+2]);
                if(hit.VariantType==Variant.Type.Nil||origin.DistanceTo(hit.AsVector3())>.09f)continue;
                supported=true;break;
            }
            if(!supported)return false;
        }
        return true;
    }

    // Thin construction joints between vertical gable boards may sit behind a
    // rigid metal plate, including where a joint crosses the rim. Four fasteners
    // require solid wood. Material on both sides distinguishes a construction
    // joint from a missing exterior edge. Larger openings remain rejected.
    private static bool BoardJoints(string owner)=>owner.Contains("BoardedGable",StringComparison.Ordinal);
    internal static Coverage Cover(IReadOnlyList<Triangle> triangles,Vector2 center,bool allowBoardJoints=false)
    {
        var required=allowBoardJoints?FastenerPoints(center):RequiredMountPoints(center);
        if(!required.All(p=>triangles.Any(t=>Contains(t,p))))return new(false,0,"","unsupported boundary or fastener");
        var rect=Rectangle(center,HalfWidth,HalfHeight);
        var remaining=new List<List<Vector2>>{rect};
        foreach(var triangle in triangles)
        {
            var clip=new[]{triangle.A,triangle.B,triangle.C};
            if(Math.Abs(Cross(clip[1]-clip[0],clip[2]-clip[0]))<.0000001f)continue;
            if(Cross(clip[1]-clip[0],clip[2]-clip[0])<0)Array.Reverse(clip);
            var next=new List<List<Vector2>>();
            foreach(var polygon in remaining)Subtract(polygon,clip,next);
            remaining=next;
            if(remaining.Count==0)return new(true,0,"","fully covered");
        }
        var area=remaining.Sum(Area);
        var acceptable=remaining.All(p=>Area(p)<.000001 || allowBoardJoints&&IsNarrowBoardJoint(triangles,p));
        return new(acceptable,area,"",acceptable?"narrow vertical board joints only":"uncovered facade opening");
    }
    private static bool IsNarrowBoardJoint(IReadOnlyList<Triangle> triangles,List<Vector2> missing)
    {
        var min=missing.Min(p=>p.X);var max=missing.Max(p=>p.X);
        if(max-min>.012f)return false;
        // A joint crossing the rim is normal construction. An overhang beyond
        // the end of the last board has no material on its outside and fails.
        foreach(var y in new[]{missing.Min(p=>p.Y),missing.Max(p=>p.Y),(missing.Min(p=>p.Y)+missing.Max(p=>p.Y))*.5f})
            if(!triangles.Any(t=>Contains(t,new(min-.015f,y)))||!triangles.Any(t=>Contains(t,new(max+.015f,y))))return false;
        return true;
    }
    private static IEnumerable<Vector2> FastenerPoints(Vector2 center)
    {
        foreach(var x in new[]{-RivetX,RivetX})foreach(var y in new[]{-RivetY,RivetY})yield return center+new Vector2(x,y);
    }
    internal static IEnumerable<Vector2> RequiredMountPoints(Vector2 center)
    {
        // All corners and a dense perimeter expose overhang; exact subtraction
        // below is what detects holes between these points.
        for(var i=0;i<=12;i++){var x=-HalfWidth+2*HalfWidth*i/12;yield return center+new Vector2(x,-HalfHeight);yield return center+new Vector2(x,HalfHeight);}
        for(var i=1;i<6;i++){var y=-HalfHeight+2*HalfHeight*i/6;yield return center+new Vector2(-HalfWidth,y);yield return center+new Vector2(HalfWidth,y);}
        foreach(var x in new[]{-RivetX,RivetX})foreach(var y in new[]{-RivetY,RivetY})yield return center+new Vector2(x,y);
    }
    private static List<Plane> Planes(Node3D building,Vector3 outward,Vector3 right,string? explicitExteriorWall=null)
    {
        // The first two names are exterior walls in author_rural_dwelling; Pier1Skin is the
        // outer skin of the civic square's hollow front walls and Walls the solid shells of
        // the office and post. None is an interior room divider.
        // Explicit selection still checks actual faces,
        // openings, fasteners and every foreground obstruction. Default policy
        // and the reading-height band are unchanged.
        if(explicitExteriorWall is not null && !VerifiedExplicitWall(explicitExteriorWall))
            throw new ArgumentException("Unverified explicit exterior facade: "+explicitExteriorWall);
        var planes=new List<Plane>();
        foreach(var mesh in Descendants(building).OfType<MeshInstance3D>().Where(m=>m.IsVisibleInTree()&&m.Mesh is not null
            &&(explicitExteriorWall is null?Eligible(m.Name.ToString()):m.Name==explicitExteriorWall)))
        {
            var owner=mesh.GetPath().ToString();var faces=mesh.Mesh!.GetFaces();
            for(var i=0;i+2<faces.Length;i+=3)
            {
                var a=mesh.GlobalTransform*faces[i];var b=mesh.GlobalTransform*faces[i+1];var c=mesh.GlobalTransform*faces[i+2];
                if(Math.Abs((b-a).Cross(c-a).Normalized().Dot(outward))<.999f)continue;
                var depth=a.Dot(outward);var plane=planes.FirstOrDefault(p=>Math.Abs(p.Depth-depth)<.006f&&p.Owner==owner);
                if(plane is null){plane=new(depth,owner);planes.Add(plane);}
                plane.Triangles.Add(new(new(a.Dot(right),a.Y),new(b.Dot(right),b.Y),new(c.Dot(right),c.Y)));
            }
        }
        return planes.GroupBy(p=>p.Owner,StringComparer.Ordinal).Select(g=>g.OrderByDescending(p=>p.Depth).First()).ToList();
    }
    // Explicit surface policy. The list is citation-explicit; the civic square
    // generates its exterior wall skins as "Pier<n>Skin" (SWall indexes every
    // pier), so the House of Culture's Pier4Skin is the same class of surface
    // as the school's Pier1Skin and must not throw on import.
    private static bool VerifiedExplicitWall(string name)
    {
        if(name is "DwellingFacade_Right_Wall_LOD0" or "DwellingFacade_SeniOuter_Wall_LOD0" or "Pier1Skin" or "Walls")return true;
        if(!name.StartsWith("Pier",StringComparison.Ordinal)||!name.EndsWith("Skin",StringComparison.Ordinal))return false;
        var digits=name.AsSpan(4,name.Length-8);
        if(digits.Length==0)return false;
        foreach(var c in digits)if(c<'0'||c>'9')return false;
        return true;
    }
    /// <summary>A yard whose street gate is the entrance carries its plate on that gate's own
    /// posts and rails, where a passer-by in the street can read it. Only members of this
    /// household are considered, never a neighbour's fence, and the complete rectangle must
    /// lie on one solid member's own street-facing face at reading height.</summary>
    internal static bool TryFindOnOwnYardFence(Node3D building,Vector3 gate,Vector3 outward,Node3D world,out Vector3 point,out string owner,out string failure)
    {
        point=default;owner="";failure="";outward.Y=0;outward=outward.Normalized();
        var right=new Vector3(outward.Z,0,-outward.X);
        var pickets=new List<(MeshInstance3D Mesh,string Owner)>();
        foreach(var mesh in Descendants(building).OfType<MeshInstance3D>().Where(m=>m.Mesh is not null&&m.IsVisibleInTree()))
        {
            var name=mesh.Name.ToString();
            if(!(name.Contains("Fence",StringComparison.Ordinal)||name.Contains("Gate",StringComparison.Ordinal)
                ||name.Contains("Post",StringComparison.Ordinal)||name.Contains("Rail",StringComparison.Ordinal)))continue;
            if(name.Contains("Glass",StringComparison.Ordinal)||name.Contains("Leaf",StringComparison.Ordinal))continue;
            pickets.Add((mesh,mesh.GetPath().ToString()));
        }
        if(pickets.Count==0){failure="no own fence, gate or post meshes";return false;}
        var rails=new List<(MeshInstance3D Mesh,Face[] Faces,string Owner)>();
        foreach(var (mesh,path) in pickets)
        {
            var name=mesh.Name.ToString();
            // Solid members only: rails, posts, gate frames. Picket slats and
            // snow caps never carry the plate.
            var solid=name.Contains("Rail",StringComparison.Ordinal)||name.Contains("Post",StringComparison.Ordinal)
                ||name.Contains("Gate",StringComparison.Ordinal)||name.Contains("Frame",StringComparison.Ordinal)
                ||name.Contains("Beam",StringComparison.Ordinal);
            if(!solid)continue;
            var raw=mesh.Mesh!.GetFaces();var faces=new List<Face>();
            for(var i=0;i+2<raw.Length;i+=3)
            {
                var a=mesh.GlobalTransform*raw[i];var b=mesh.GlobalTransform*raw[i+1];var c=mesh.GlobalTransform*raw[i+2];
                // Near-vertical member faces only (|ny| below ~0.35): a sloped
                // rail is not a plate board.
                if(Math.Abs((b-a).Cross(c-a).Normalized().Dot(outward))<.94f)continue;
                faces.Add(new(a,b,c,path));
            }
            if(faces.Count>0)rails.Add((mesh,faces.ToArray(),path));
        }
        if(rails.Count==0){failure="own fence members have no street-facing solid planes";return false;}
        var candidates=new List<(float Score,Vector3 Point,string Owner)>();
        foreach(var (_,faces,path) in rails)
        {
            // The plate belongs on the member's street-facing face, not on its
            // back: pick the frontmost drawn plane of this solid member.
            var depth=faces.Select(f=>new[]{f.A,f.B,f.C}.Average(p=>p.Dot(outward))).Max();
            var along=faces.SelectMany(f=>new[]{f.A,f.B,f.C}).ToArray();
            var minA=along.Min(p=>p.Dot(right));var maxA=along.Max(p=>p.Dot(right));
            var minY=along.Min(p=>p.Y);var maxY=along.Max(p=>p.Y);
            var groundY=Act1ConnectedWorld.AddressGround(new Vector3((minA+maxA)*.5f*right.X,0,(minA+maxA)*.5f*right.Z)+outward*depth).Y;
            // The gate board is read from the street: keep it in the same
            // human band as the façade plate (never a fence-top shelf).
            var wantY=Math.Max(minY+.25f,groundY+1.45f);
            if(wantY+HalfHeight>Math.Min(maxY,groundY+MountHigh))continue;
            var memberTriangles=MemberTriangles(faces,outward,right,depth);
            foreach(var t in new[]{.2f,.35f,.5f,.65f,.8f})
            {
                var a=minA+(maxA-minA)*t;
                // A gate plate must lie wholly on the member's own face: no overhang past a post.
                if(!Cover(memberTriangles,new(a,wantY)).Supported)continue;
                var p=right*a+Vector3.Up*wantY+outward*(depth+MountOffset);
                var toGate=new Vector2(p.X-gate.X,p.Z-gate.Z).Length();
                if(toGate>9f)continue;
                candidates.Add((toGate,p,path));
            }
        }
        if(candidates.Count==0){failure="no own gate/rail rectangle at reading height";return false;}
        var bounds=Bounds(candidates.Select(c=>c.Point)).Grow(4.2f);
        var faces2=VisibleFaces(world,bounds);
        var refusals=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var candidate in candidates.OrderBy(c=>c.Score))
        {
            if(Occluder(faces2,candidate.Point,outward,right) is { } occluder)
            {
                refusals[occluder]=refusals.GetValueOrDefault(occluder)+1;continue;
            }
            point=candidate.Point;owner=candidate.Owner+FenceMountSuffix;return true;
        }
        failure="own gate candidates occluded: "+string.Join("; ",refusals.OrderByDescending(r=>r.Value).Take(2).Select(r=>r.Key+" ("+r.Value+")"));
        return false;
    }
    private static List<Triangle> MemberTriangles(IEnumerable<Face> faces,Vector3 outward,Vector3 right,float depth)
    {
        var result=new List<Triangle>();
        foreach(var f in faces)
        {
            if(Math.Abs(new[]{f.A,f.B,f.C}.Average(p=>p.Dot(outward))-depth)>.006f)continue;
            result.Add(new(new(f.A.Dot(right),f.A.Y),new(f.B.Dot(right),f.B.Y),new(f.C.Dot(right),f.C.Y)));
        }
        return result;
    }

    /// <summary>Verifies a plate recorded as mounted on a household's own gate or fence member:
    /// the plate rectangle must lie wholly on that member's street-facing plane, 1 mm to 9 cm proud,
    /// with a clear exterior sightline.</summary>
    private static Coverage InspectOwnMember(Node3D world,string ownerPath,Vector3 point,Vector3 outward,Vector3 right)
    {
        if(world.GetNodeOrNull<MeshInstance3D>(ownerPath) is not { Mesh: not null } mesh)
            return new(false,4*HalfWidth*HalfHeight,ownerPath,"recorded fence member is missing");
        var raw=mesh.Mesh.GetFaces();var faces=new List<Face>();
        for(var i=0;i+2<raw.Length;i+=3)
        {
            var a=mesh.GlobalTransform*raw[i];var b=mesh.GlobalTransform*raw[i+1];var c=mesh.GlobalTransform*raw[i+2];
            if(Math.Abs((b-a).Cross(c-a).Normalized().Dot(outward))<.9f)continue;
            faces.Add(new(a,b,c,ownerPath));
        }
        var center=new Vector2(point.Dot(right),point.Y);
        foreach(var depth in faces.Select(f=>new[]{f.A,f.B,f.C}.Average(p=>p.Dot(outward))).Distinct().OrderByDescending(d=>d))
        {
            var gap=point.Dot(outward)-depth;
            if(gap is <.001f or >.09f)continue;
            var covered=Cover(MemberTriangles(faces,outward,right,depth),center);
            if(!covered.Supported)continue;
            var obstruction=Occluder(VisibleFaces(world,new Aabb(point-Vector3.One*4.2f,Vector3.One*8.4f)),point,outward,right);
            return new(obstruction is null,covered.MissingArea,ownerPath,obstruction is null?"complete gate member and exterior sightline":"occluded by "+obstruction);
        }
        return new(false,4*HalfWidth*HalfHeight,ownerPath,"plate is not wholly on the recorded fence member");
    }

    /// <summary>The house's own cladding in front of the wall plane (hewn log
    /// courses, casings) is presentation geometry the occluder ignores; a plate
    /// on the plane behind it would be buried. Move the plate onto the
    /// frontmost near-vertical cladding face, by at most MaxCladdingProud, that
    /// overlaps its rectangle; sloped roofs, snow and canopies never carry it.</summary>
    private static Vector3 ProudOfOwnCladding(Node3D building,Vector3 point,Vector3 outward,Vector3 right)
    {
        var depth=point.Dot(outward)-MountOffset;var front=depth;var cx=point.Dot(right);
        foreach(var mesh in Descendants(building).OfType<MeshInstance3D>().Where(m=>m.Mesh is not null&&m.IsVisibleInTree()))
        {
            if(mesh.GetParent() is AddressSignVisualComponent)continue;
            var raw=mesh.Mesh!.GetFaces();
            for(var i=0;i+2<raw.Length;i+=3)
            {
                var a=mesh.GlobalTransform*raw[i];var b=mesh.GlobalTransform*raw[i+1];var c=mesh.GlobalTransform*raw[i+2];
                var hi=Math.Max(a.Dot(outward),Math.Max(b.Dot(outward),c.Dot(outward)));
                if(hi<=front+.002f)continue;
                if(Math.Max(a.Y,Math.Max(b.Y,c.Y))<point.Y-HalfHeight||Math.Min(a.Y,Math.Min(b.Y,c.Y))>point.Y+HalfHeight)continue;
                // Only near-vertical faces (|n·outward| >= .94, |ny| below
                // ~0.35) may carry the plate forward: a sloped roof verge, snow
                // lip or canopy crossing the rectangle is not cladding, and
                // following it would put the plate on a roof edge.
                var normal=(b-a).Cross(c-a);
                if(normal.LengthSquared()<.0000001f||Math.Abs(normal.Normalized().Dot(outward))<.94f)continue;
                var ar=a.Dot(right);var br=b.Dot(right);var cr=c.Dot(right);
                if(Math.Max(ar,Math.Max(br,cr))<cx-HalfWidth||Math.Min(ar,Math.Min(br,cr))>cx+HalfWidth)continue;
                // A diagonal verge's bounds can overlap the plate while its
                // actual triangle stays above it. Only the clipped surface
                // beneath the plate may push it away from the wall.
                var overlap=ClipDepth([a,b,c],right,cx-HalfWidth,true);
                overlap=ClipDepth(overlap,right,cx+HalfWidth,false);
                overlap=ClipDepth(overlap,Vector3.Up,point.Y-HalfHeight,true);
                overlap=ClipDepth(overlap,Vector3.Up,point.Y+HalfHeight,false);
                if(Area(overlap.Select(p=>new Vector2(p.Dot(right),p.Y)).ToList())<.00001)continue;
                hi=overlap.Max(p=>p.Dot(outward));
                if(hi<=front+.002f||hi>depth+MaxCladdingProud)continue;
                front=hi;
            }
        }
        return front>depth?point+outward*(front-depth):point;
    }

    private static Face[] VisibleFaces(Node3D root,Aabb bounds)
    {
        var result=new List<Face>();
        foreach(var mesh in Descendants(root).OfType<MeshInstance3D>().Where(m=>m.Mesh is not null&&m.IsVisibleInTree()))
        {
            if(mesh.GetParent() is AddressSignVisualComponent)continue;
            // Walk-through presentation foliage (no collision owner) must not
            // veto a plate the player can actually stand in front of and read.
            // Solid occluders — walls, posts, annexes, rooted stems — stay.
            // Interior dressing is likewise invisible to the street prism.
            if(IsWalkThroughFoliage(mesh)||IsInteriorDressing(mesh))continue;
            var aabb=mesh.Mesh!.GetAabb();
            var worldBounds=Bounds(Enumerable.Range(0,8).Select(i=>mesh.GlobalTransform*aabb.GetEndpoint(i)));
            if(!worldBounds.Intersects(bounds))continue;
            var faces=mesh.Mesh.GetFaces();var owner=mesh.GetPath().ToString();
            for(var i=0;i+2<faces.Length;i+=3)result.Add(new(mesh.GlobalTransform*faces[i],mesh.GlobalTransform*faces[i+1],mesh.GlobalTransform*faces[i+2],owner));
        }
        return result.ToArray();
    }
    private static bool IsWalkThroughFoliage(MeshInstance3D mesh)
    {
        for(var node=(Node?)mesh;node is not null;node=node.GetParent())
        {
            if(node.HasMeta(PresentationOnlyKey)||node.HasMeta(PresentationOnlyInstanceKey)||node.HasMeta(VisualOnlyKey))return true;
            if(node is StaticBody3D||node is CollisionShape3D||node is CollisionObject3D)return false;
            var name=node.Name.ToString();
            if(name.Contains("PlantedFoliage",StringComparison.Ordinal)||name.Contains("Foliage",StringComparison.Ordinal)
                ||name.Contains("Thicket",StringComparison.Ordinal)||name.Contains("Undergrowth",StringComparison.Ordinal))return true;
        }
        return false;
    }
    // Interior dressing (ceilings, room volumes, furniture shells) is never a
    // legitimate street occluder: the sight prism starts outside the house and
    // only the exterior envelope may block it. A ceiling slab above the plate
    // height band is already skipped by the Y check; anything flagged here is
    // a room volume the prism should not see at all.
    private static bool IsInteriorDressing(MeshInstance3D mesh)
    {
        for(var node=(Node?)mesh;node is not null;node=node.GetParent())
        {
            if(node.HasMeta(InteriorZoneKey)||node.HasMeta(InteriorRoomKey)||node.HasMeta(RoomVolumeKey))return true;
            var name=node.Name.ToString();
            if(name is "Ceiling" or "Visible"||name.Contains("Interior",StringComparison.Ordinal)
                ||name.Contains("RoomVolume",StringComparison.Ordinal)||name.Contains("house_old_pc",StringComparison.Ordinal)
                ||name.Contains("babay-abi-house",StringComparison.Ordinal))return true;
        }
        return mesh.Name.ToString() is "Ceiling" or "Visible";
    }
    private static string? Occluder(IEnumerable<Face> faces,Vector3 point,Vector3 outward,Vector3 right)
    {
        var depth=point.Dot(outward);var cx=point.Dot(right);
        foreach(var face in faces)
        {
            if(Math.Max(face.A.Y,Math.Max(face.B.Y,face.C.Y))<point.Y-HalfHeight || Math.Min(face.A.Y,Math.Min(face.B.Y,face.C.Y))>point.Y+HalfHeight)continue;
            var ar=face.A.Dot(right);var br=face.B.Dot(right);var cr=face.C.Dot(right);
            if(Math.Max(ar,Math.Max(br,cr))<cx-HalfWidth||Math.Min(ar,Math.Min(br,cr))>cx+HalfWidth)continue;
            var ad=face.A.Dot(outward);var bd=face.B.Dot(outward);var cd=face.C.Dot(outward);
            if(Math.Max(ad,Math.Max(bd,cd))<depth+.035f||Math.Min(ad,Math.Min(bd,cd))>depth+4)continue;
            // Clip in depth before projection: a sloped canopy or a post can
            // cross the sight prism even when none of its vertices is inside.
            var polygon=new List<Vector3>{face.A,face.B,face.C};
            polygon=ClipDepth(polygon,outward,depth+.035f,true);
            polygon=ClipDepth(polygon,outward,depth+4.0f,false);
            if(polygon.Count<3)continue;
            var projected=polygon.Select(p=>new Vector2(p.Dot(right)-cx,p.Y-point.Y)).ToList();
            var rect=Rectangle(Vector2.Zero,HalfWidth,HalfHeight);
            for(var i=0;i<4&&projected.Count>0;i++)projected=Clip(projected,rect[i],rect[(i+1)%4],true);
            if(Area(projected)>.00001)return face.Owner;
        }
        return null;
    }
    internal static bool TriangleObstructs(Vector3 a,Vector3 b,Vector3 c,Vector3 point,Vector3 outward)
        =>Occluder([new(a,b,c,"regression surface")],point,outward,new(outward.Z,0,-outward.X)) is not null;
    private static List<Vector3> ClipDepth(List<Vector3> p,Vector3 axis,float depth,bool above)
    {
        var result=new List<Vector3>();if(p.Count==0)return result;
        var previous=p[^1];var pv=(previous.Dot(axis)-depth)*(above?1:-1);
        foreach(var current in p)
        {
            var cv=(current.Dot(axis)-depth)*(above?1:-1);
            if((cv>=0)!=(pv>=0))result.Add(previous.Lerp(current,pv/(pv-cv)));
            if(cv>=0)result.Add(current);previous=current;pv=cv;
        }
        return result;
    }
    private static void Subtract(List<Vector2> polygon,Vector2[] triangle,List<List<Vector2>> result)
    {
        var inside=polygon;
        for(var i=0;i<3&&inside.Count>0;i++)
        {
            var outside=Clip(inside,triangle[i],triangle[(i+1)%3],false);
            if(Area(outside)>.0000001)result.Add(outside);
            inside=Clip(inside,triangle[i],triangle[(i+1)%3],true);
        }
    }
    private static List<Vector2> Clip(List<Vector2> polygon,Vector2 a,Vector2 b,bool inside)
    {
        var result=new List<Vector2>();if(polygon.Count==0)return result;
        var previous=polygon[^1];var pv=Cross(b-a,previous-a)*(inside?1:-1);
        foreach(var current in polygon)
        {
            var cv=Cross(b-a,current-a)*(inside?1:-1);
            if((cv>=0)!=(pv>=0))result.Add(previous.Lerp(current,pv/(pv-cv)));
            if(cv>=0)result.Add(current);previous=current;pv=cv;
        }
        return result;
    }
    private static List<Vector2> Rectangle(Vector2 c,float x,float y)=>[c+new Vector2(-x,-y),c+new Vector2(x,-y),c+new Vector2(x,y),c+new Vector2(-x,y)];
    private static double Area(List<Vector2> p){double a=0;for(var i=0;i<p.Count;i++)a+=(double)p[i].X*p[(i+1)%p.Count].Y-(double)p[(i+1)%p.Count].X*p[i].Y;return Math.Abs(a)*.5;}
    private static Aabb Bounds(IEnumerable<Vector3> points){var a=points.ToArray();var min=a[0];var max=a[0];foreach(var p in a){min=min.Min(p);max=max.Max(p);}return new(min,max-min);}
    private static float[] Samples(float min,float max,float desired,float step){var points=new List<float>{min,max,Math.Clamp(desired,min,max),(min+max)*.5f};for(var x=min+step;x<max;x+=step)points.Add(x);return points.Distinct().ToArray();}
    private static float Cross(Vector2 a,Vector2 b)=>a.X*b.Y-a.Y*b.X;
    private static bool Contains(Triangle t,Vector2 p){var a=Cross(t.B-t.A,p-t.A);var b=Cross(t.C-t.B,p-t.B);var c=Cross(t.A-t.C,p-t.C);return a>=-.00001f&&b>=-.00001f&&c>=-.00001f||a<=.00001f&&b<=.00001f&&c<=.00001f;}
    // HasMeta(string) builds a StringName on every call, and both walks below ask
    // the same key set for every ancestor of every candidate mesh; keeping the
    // keys as StringNames is what the engine's own guidance recommends for
    // repeatedly used names.
    private static readonly StringName PresentationOnlyKey=new("presentationOnly");
    private static readonly StringName PresentationOnlyInstanceKey=new("presentationOnlyInstance");
    private static readonly StringName VisualOnlyKey=new("visualOnly");
    private static readonly StringName InteriorZoneKey=new("interiorZone");
    private static readonly StringName InteriorRoomKey=new("interiorRoom");
    private static readonly StringName RoomVolumeKey=new("roomVolume");
    // GetChildren returns an owned native array and this walk visits every
    // descendant, so leaving each array to the finaliser was real churn during the
    // world build. The typed view is iterated while the untyped alias owns the same
    // object and disposes it when the enumerator ends.
    private static IEnumerable<Node> Descendants(Node node)
    {
        var children=node.GetChildren();
        using var owned=(global::Godot.Collections.Array)children;
        foreach(var child in children)
        {
            yield return child;
            foreach(var nested in Descendants(child))yield return nested;
        }
    }
}
