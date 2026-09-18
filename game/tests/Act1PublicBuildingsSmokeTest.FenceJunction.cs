using Godot;
using System.Text.Json;

namespace Urman.Godot.Tests;

public partial class Act1PublicBuildingsSmokeTest
{
    private void CheckCouncilFenceJunction(Act1ConnectedWorld.PublicBuildingRoom council)
    {
        var repair=_world.CouncilFenceJunction??throw new InvalidOperationException("Missing measured council fence repair.");
        Require(repair.Building==council.Building&&repair.Fence.Name=="EastStreetHorizonFence"
            &&repair.Members.Length==15,"council: the repair owns only the identified fifteen-member fence");
        Require(repair.CutZ>repair.WallBounds.Position.Z+.04f&&repair.CutZ<repair.WallBounds.End.Z-.04f,
            "council: rail ends lie inside the real existing rear wall thickness");
        var beforeProps=_bridge.SelectWorldProps().GetRawText();
        var beforePose=_player.GlobalTransform;
        var rows=new List<object>();
        float[] Point(Vector3 p)=>new[]{p.X,p.Y,p.Z};
        var footing=council.Building.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>()
            .Single(mesh=>mesh.Name.ToString().EndsWith("_SeniFooting_LOD0",StringComparison.Ordinal));
        var lowerSupport=FenceSupportWitness(council.Building,footing);
        var upperSupport=FenceWallPanelWitness(council.Building,repair.RearWall);
        var seamY=upperSupport.Bounds.Position.Y;
        // Preserve actual imported arrays and transforms even if a following
        // support assertion fails. The cap may end in footing, wall, or both.
        var capEvidence=repair.Members.Where(member=>member.Action=="rail-ended-in-wall").Select(member=>
        {
            var toBuilding=council.Building.GlobalTransform.AffineInverse()*member.Mesh.GlobalTransform;
            var faces=FenceWitness((ArrayMesh)member.Mesh.Mesh).Faces.Select(point=>toBuilding*point).ToArray();
            return new{owner=member.Mesh.GetPath().ToString(),transform=toBuilding.ToString(),
                publishedBuildingFaces=faces.Select(Point).ToArray(),caps=Enumerable.Range(0,faces.Length/3)
                    .Select(index=>faces.Skip(index*3).Take(3).ToArray())
                    .Where(triangle=>triangle.All(point=>Math.Abs(point.Z-repair.CutZ)<.00003f))
                    .Select(triangle=>new{triangle=triangle.Select(Point).ToArray(),
                        lower=ClipFenceCapByHeight(triangle,seamY,true).Select(Point).ToArray(),
                        upper=ClipFenceCapByHeight(triangle,seamY,false).Select(Point).ToArray()}).ToArray()};
        }).ToArray();
        object SupportEvidence(FenceSupportSolid support)=>new{owner=support.Mesh.GetPath().ToString(),
            transform=(council.Building.GlobalTransform.AffineInverse()*support.Mesh.GlobalTransform).ToString(),
            bounds=support.Bounds.ToString(),support.Closed,support.Convex,support.RectangularPanel,
            buildingFaces=support.Faces.Select(Point).ToArray(),
            planes=support.Planes.Select(plane=>new[]{plane.X,plane.Y,plane.Z,plane.D}).ToArray()};
        var capJson=JsonSerializer.Serialize(new{repair.CutZ,seamY,
            footing=SupportEvidence(lowerSupport),wall=SupportEvidence(upperSupport),rails=capEvidence,
            numericPlaneTolerance=FenceSupportEpsilon,
            source="indexed SurfaceGetArrays; no GetFaces cache or expanded support bounds"},new JsonSerializerOptions{WriteIndented=true});
        Directory.CreateDirectory(Output);
        using(var capFile=new FileStream(Path.Combine(Output,"council-fence-junction-cap-support.json"),FileMode.CreateNew,System.IO.FileAccess.Write))
        using(var writer=new StreamWriter(capFile))writer.Write(capJson);
        Require(footing.IsVisibleInTree()&&repair.RearWall.IsVisibleInTree()
            &&lowerSupport.Closed&&lowerSupport.Convex&&upperSupport.RectangularPanel,
            "council: the actual footing is closed and convex, and the visible wall has two complete matching panel faces");
        Require(Math.Abs(lowerSupport.Bounds.End.Y-seamY)<=FenceSupportEpsilon
            &&upperSupport.Faces.Where(point=>Math.Abs(point.Y-seamY)<=FenceSupportEpsilon)
                .All(point=>FenceSupportContains(lowerSupport,point)),
            "council: the full real wall base meets the actual bevelled footing at the shared seam");
        foreach(var member in repair.Members)
        {
            var mesh=member.Mesh;
            var source=FenceWitness(member.Source);
            Require(mesh.GlobalTransform==member.OriginalTransform,"council: fence member keeps its original transform: "+mesh.Name);
            var toBuilding=council.Building.GlobalTransform.AffineInverse()*mesh.GlobalTransform;
            var sourceZ=source.Faces.Select(p=>(toBuilding*p).Z).ToArray();
            var keptFaces=0;var missingFaces=0;var missingAttributes=0;var offSource=0;var capCount=0;
            var maximumContactError=0f;
            double maximumParentAreaError=0;
            if(member.Action=="inside-member-hidden")
            {
                Require(!mesh.Visible&&mesh.Mesh==member.Source&&member.Contacts.Length==0
                    &&sourceZ.Min()>=repair.CutZ,"council: only an entirely intruding member is hidden without a remaining contact: "+mesh.Name);
            }
            else
            {
                Require(mesh.Visible==member.OriginalVisible,"council: retained fence visibility is unchanged: "+mesh.Name);
                var published=mesh.Mesh as ArrayMesh??throw new InvalidOperationException("Fence has no published triangle mesh.");
                Require(published.GetSurfaceCount()==member.Source.GetSurfaceCount()
                    &&Enumerable.Range(0,published.GetSurfaceCount()).All(surface=>
                        published.SurfaceGetMaterial(surface)==member.Source.SurfaceGetMaterial(surface)),
                    "council: every retained fence surface keeps its material: "+mesh.Name);
                var result=FenceWitness(published);
                Require(result.Faces.All(p=>(toBuilding*p).Z<=repair.CutZ+.00003f),
                    "council: published fence has no surface extending into the seni: "+mesh.Name);
                if(member.Action=="retained")
                    Require(published==member.Source,"council: exterior member retains the exact original mesh: "+mesh.Name);
                else
                {
                    Require(member.Action=="rail-ended-in-wall","council: every changed member has the measured rail role");
                    Require(member.Source.GetSurfaceCount()==1&&published.GetSurfaceCount()==1,
                        "council: only the measured single-surface rails receive a cut");
                    var parentAreas=new double[source.Faces.Length/3];
                    var triangles=Enumerable.Range(0,result.Faces.Length/3).Select(i=>FenceTriangleKey(result.Faces,i*3)).ToHashSet(StringComparer.Ordinal);
                    for(var i=0;i<source.Faces.Length;i+=3)
                    {
                        if(Enumerable.Range(0,3).Any(c=>(toBuilding*source.Faces[i+c]).Z>repair.CutZ))continue;
                        keptFaces++;
                        if(!triangles.Contains(FenceTriangleKey(source.Faces,i)))missingFaces++;
                    }
                    for(var i=0;i<source.Vertices.Length;i++)
                    {
                        if((toBuilding*source.Vertices[i]).Z>repair.CutZ)continue;
                        if(!Enumerable.Range(0,result.Vertices.Length).Any(j=>source.Vertices[i].DistanceTo(result.Vertices[j])<.00002f
                            &&source.HasNormals[i]==result.HasNormals[j]&&source.HasUv[i]==result.HasUv[j]
                            &&(!source.HasNormals[i]||source.Normals[i].DistanceTo(result.Normals[j])<.0001f)
                            &&(!source.HasUv[i]||source.Uv[i].DistanceTo(result.Uv[j])<.00002f)))missingAttributes++;
                    }
                    for(var i=0;i<result.Faces.Length;i+=3)
                    {
                        var a=result.Faces[i];var b=result.Faces[i+1];var c=result.Faces[i+2];
                        if(new[]{a,b,c}.All(p=>Math.Abs((toBuilding*p).Z-repair.CutZ)<.00003f))
                        {
                            capCount++;
                            var normal=(toBuilding.Basis.Transposed()*Vector3.Back).Normalized();
                            Require((b-a).Cross(c-a).Dot(normal)<-1e-10f,
                                "council: wall-end cap has outward clockwise winding: "+mesh.Name);
                            var cap=new[]{a,b,c}.Select(point=>toBuilding*point).ToArray();
                            var lower=ClipFenceCapByHeight(cap,seamY,true);
                            var upper=ClipFenceCapByHeight(cap,seamY,false);
                            Require(lower.All(point=>FenceSupportContains(lowerSupport,point))
                                &&upper.All(point=>FenceSupportContains(upperSupport,point))
                                &&Math.Abs(FencePolygonArea(lower)+FencePolygonArea(upper)-FencePolygonArea(cap))<.0000002,
                                "council: every complete cap lies inside the real footing/wall union: "+mesh.Name
                                +"; cap="+string.Join(" | ",cap.Select(point=>point.ToString()))+"; seamY="+seamY);
                        }
                        else
                        {
                            var parent=Enumerable.Range(0,source.Faces.Length/3).FirstOrDefault(p=>FenceFragmentFits(a,b,c,source.Faces,p*3),-1);
                            if(parent<0)offSource++;
                            else parentAreas[parent]+=FenceArea((a.X,a.Y,a.Z),(b.X,b.Y,b.Z),(c.X,c.Y,c.Z));
                        }
                    }
                    for(var parent=0;parent<parentAreas.Length;parent++)maximumParentAreaError=Math.Max(maximumParentAreaError,
                        Math.Abs(parentAreas[parent]-RetainedFenceArea(source.Faces,parent*3,toBuilding,repair.CutZ)));
                    Require(keptFaces>0&&missingFaces==0&&missingAttributes==0&&offSource==0&&capCount==member.CapTriangles&&capCount>0
                        &&maximumParentAreaError<.00002,
                        "council: original exterior triangles, attributes and clipped source surfaces survive the closed rail cut: "+mesh.Name);
                }
                foreach(var contact in member.Contacts)
                {
                    Require(!contact.Disabled&&contact.GetMeta("authoredSourceMesh").AsString()==mesh.GetPath().ToString(),
                        "council: each remaining fence contact names its real source mesh");
                    if(member.Action!="rail-ended-in-wall")continue;
                    var shape=contact.Shape as ConcavePolygonShape3D??throw new InvalidOperationException("Trimmed rail retained a bounding-box contact.");
                    var physical=shape.GetFaces();var cached=published.GetFaces();
                    Require(physical.Length==cached.Length,"council: existing rail contact uses all published faces");
                    for(var i=0;i<physical.Length;i++)maximumContactError=Math.Max(maximumContactError,
                        (contact.GlobalTransform*physical[i]).DistanceTo(mesh.GlobalTransform*cached[i]));
                    Require(maximumContactError<.0002f,"council: rail render cache and its sole physical owner agree in world space");
                }
            }
            var final=mesh.Mesh as ArrayMesh??throw new InvalidOperationException("Missing retained fence source.");
            rows.Add(new{owner=mesh.GetPath().ToString(),member.Action,member.SourceTriangles,member.PublishedTriangles,member.CapTriangles,
                keptFaces,missingFaces,missingAttributes,offSource,capCount,maximumContactError,maximumParentAreaError,
                contacts=member.Contacts.Select(c=>c.GetPath().ToString()).ToArray(),
                original=source.Faces.Select(Point).ToArray(),published=FenceWitness(final).Faces.Select(Point).ToArray(),
                rendered=mesh.Visible,transform=mesh.GlobalTransform.ToString(),cutZ=repair.CutZ});
        }
        Require(repair.Members.Count(m=>m.Action=="inside-member-hidden")==4
            &&repair.Members.Count(m=>m.Action=="rail-ended-in-wall")==3
            &&repair.Members.Count(m=>m.Action=="retained")==8,"council: the measured four/three/eight member boundary is preserved");
        Require(_player.GlobalTransform==beforePose&&_bridge.SelectWorldProps().GetRawText()==beforeProps,
            "council: geometry inspection changes no player pose or persistent state");
        Directory.CreateDirectory(Output);
        using var file=new FileStream(Path.Combine(Output,"council-fence-junction-geometry.json"),FileMode.CreateNew,System.IO.FileAccess.Write);
        JsonSerializer.Serialize(file,new{wall=repair.RearWall.GetPath().ToString(),wallBounds=repair.WallBounds.ToString(),
            triangleSource="indexed SurfaceGetArrays; physics compared separately to its published GetFaces cache",rows,
            scope="geometry and source ownership; ordinary entrance/return and camera evidence follow in the same run"},new JsonSerializerOptions{WriteIndented=true});
        _events.Add(new{kind="council-fence-junction",members=15,hidden=4,trimmed=3,retained=8,repair.CutZ});
    }

    private const double FenceSupportEpsilon=.000002;
    private readonly record struct FenceSupportPlane(double X,double Y,double Z,double D)
    {
        internal double Distance(Vector3 point)=>X*point.X+Y*point.Y+Z*point.Z-D;
    }
    private sealed record FenceSupportSolid(MeshInstance3D Mesh,Vector3[] Faces,Aabb Bounds,
        FenceSupportPlane[] Planes,bool Closed,bool Convex,bool RectangularPanel=false);

    private static FenceSupportSolid FenceWallPanelWitness(Node3D building,MeshInstance3D mesh)
    {
        var source=FenceSupportWitness(building,mesh);
        var min=source.Bounds.Position;var max=source.Bounds.End;
        // This authored wall has two rectangular faces and omits the four thin
        // reveal faces. Prove its actual panel contour before using the space
        // between those faces; a generic mesh AABB is not sufficient evidence.
        var valid=source.Faces.Length==12&&max.X>min.X&&max.Y>min.Y&&max.Z>min.Z;
        var rectangleArea=((double)max.X-min.X)*(max.Y-min.Y);
        foreach(var z in new[]{min.Z,max.Z})
        {
            var triangles=Enumerable.Range(0,source.Faces.Length/3)
                .Select(index=>source.Faces.Skip(index*3).Take(3).ToArray())
                .Where(triangle=>triangle.All(point=>Math.Abs(point.Z-z)<=FenceSupportEpsilon)).ToArray();
            if(triangles.Length!=2){valid=false;continue;}
            int Corner(Vector3 point)
            {
                var x=Math.Abs(point.X-min.X)<=FenceSupportEpsilon?0:Math.Abs(point.X-max.X)<=FenceSupportEpsilon?1:-1;
                var y=Math.Abs(point.Y-min.Y)<=FenceSupportEpsilon?0:Math.Abs(point.Y-max.Y)<=FenceSupportEpsilon?2:-1;
                return x<0||y<0?-1:x|y;
            }
            var first=triangles[0].Select(Corner).ToArray();var second=triangles[1].Select(Corner).ToArray();
            var shared=first.Intersect(second).ToArray();
            valid&=first.All(corner=>corner>=0)&&second.All(corner=>corner>=0)
                &&first.Distinct().Count()==3&&second.Distinct().Count()==3
                &&first.Union(second).Count()==4&&shared.Length==2&&(shared[0]^shared[1])==3
                &&Math.Abs(triangles.Sum(FencePolygonArea)-rectangleArea)<.00002;
        }
        var planes=new[]{new FenceSupportPlane(-1,0,0,-min.X),new FenceSupportPlane(1,0,0,max.X),
            new FenceSupportPlane(0,-1,0,-min.Y),new FenceSupportPlane(0,1,0,max.Y),
            new FenceSupportPlane(0,0,-1,-min.Z),new FenceSupportPlane(0,0,1,max.Z)};
        return source with{Planes=planes,RectangularPanel=valid};
    }

    private static FenceSupportSolid FenceSupportWitness(Node3D building,MeshInstance3D mesh)
    {
        var source=mesh.Mesh as ArrayMesh??throw new InvalidOperationException("The fence support must be a real imported mesh.");
        var transform=building.GlobalTransform.AffineInverse()*mesh.GlobalTransform;
        var faces=FenceWitness(source).Faces.Select(point=>transform*point).ToArray();
        var points=faces.Distinct().ToArray();
        var center=points.Aggregate(Vector3.Zero,(sum,point)=>sum+point)/points.Length;
        var bounds=new Aabb(points[0],Vector3.Zero);foreach(var point in points)bounds=bounds.Expand(point);
        var planes=new List<FenceSupportPlane>();var edges=new Dictionary<(string,string),int>();
        string Key(Vector3 p)=>$"{BitConverter.SingleToInt32Bits(p.X)},{BitConverter.SingleToInt32Bits(p.Y)},{BitConverter.SingleToInt32Bits(p.Z)}";
        var valid=true;
        for(var index=0;index<faces.Length;index+=3)
        {
            var a=faces[index];var b=faces[index+1];var c=faces[index+2];
            var ex=(double)b.X-a.X;var ey=(double)b.Y-a.Y;var ez=(double)b.Z-a.Z;
            var fx=(double)c.X-a.X;var fy=(double)c.Y-a.Y;var fz=(double)c.Z-a.Z;
            var nx=ey*fz-ez*fy;var ny=ez*fx-ex*fz;var nz=ex*fy-ey*fx;
            var length=Math.Sqrt(nx*nx+ny*ny+nz*nz);
            if(length<1e-12){valid=false;continue;}
            nx/=length;ny/=length;nz/=length;
            if(nx*(center.X-a.X)+ny*(center.Y-a.Y)+nz*(center.Z-a.Z)>0){nx=-nx;ny=-ny;nz=-nz;}
            planes.Add(new(nx,ny,nz,nx*a.X+ny*a.Y+nz*a.Z));
            for(var edge=0;edge<3;edge++)
            {
                var left=Key(faces[index+edge]);var right=Key(faces[index+(edge+1)%3]);
                var key=StringComparer.Ordinal.Compare(left,right)<0?(left,right):(right,left);
                edges[key]=edges.GetValueOrDefault(key)+1;
            }
        }
        return new(mesh,faces,bounds,planes.ToArray(),valid&&edges.Count>0&&edges.Values.All(count=>count==2),
            valid&&planes.Count>0&&planes.All(plane=>points.All(point=>plane.Distance(point)<=FenceSupportEpsilon)));
    }

    private static bool FenceSupportContains(FenceSupportSolid support,Vector3 point)
        =>support.Planes.All(plane=>plane.Distance(point)<=FenceSupportEpsilon);

    private static Vector3[] ClipFenceCapByHeight(Vector3[] polygon,float seamY,bool lower)
    {
        var result=new List<Vector3>();
        for(var i=0;i<polygon.Length;i++)
        {
            var a=polygon[i];var b=polygon[(i+1)%polygon.Length];
            var da=lower?(double)seamY-a.Y:(double)a.Y-seamY;
            var db=lower?(double)seamY-b.Y:(double)b.Y-seamY;
            if(da>=0)result.Add(a);
            if((da>=0)==(db>=0))continue;
            var t=da/(da-db);result.Add(new((float)(a.X+((double)b.X-a.X)*t),seamY,(float)(a.Z+((double)b.Z-a.Z)*t)));
        }
        return result.ToArray();
    }

    private static double FencePolygonArea(Vector3[] polygon)
    {
        double area=0;
        for(var i=1;i+1<polygon.Length;i++)
        {
            var a=polygon[0];var b=polygon[i];var c=polygon[i+1];
            area+=FenceArea((a.X,a.Y,a.Z),(b.X,b.Y,b.Z),(c.X,c.Y,c.Z));
        }
        return area;
    }

    private static (Vector3[] Vertices,Vector3[] Normals,Vector2[] Uv,Vector3[] Faces,bool[] HasNormals,bool[] HasUv) FenceWitness(ArrayMesh mesh)
    {
        if(mesh.GetSurfaceCount()==0)throw new InvalidOperationException("Fence witness has no actual triangle surfaces.");
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();
        var faces=new List<Vector3>();var hasNormals=new List<bool>();var hasUv=new List<bool>();
        for(var surface=0;surface<mesh.GetSurfaceCount();surface++)
        {
            if(mesh.SurfaceGetPrimitiveType(surface)!=Mesh.PrimitiveType.Triangles)
                throw new InvalidOperationException("Fence witness requires actual triangle surfaces.");
            var arrays=mesh.SurfaceGetArrays(surface);var points=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normalValue=arrays[(int)Mesh.ArrayType.Normal];var uvValue=arrays[(int)Mesh.ArrayType.TexUV];
            var actualNormals=normalValue.VariantType==Variant.Type.Nil?Array.Empty<Vector3>():normalValue.AsVector3Array();
            var actualUv=uvValue.VariantType==Variant.Type.Nil?Array.Empty<Vector2>():uvValue.AsVector2Array();
            var indexValue=arrays[(int)Mesh.ArrayType.Index];var indices=indexValue.VariantType==Variant.Type.Nil?Array.Empty<int>():indexValue.AsInt32Array();
            if(points.Length==0||(indices.Length==0?points.Length:indices.Length)%3!=0
                ||indices.Any(i=>i<0||i>=points.Length)
                ||(actualNormals.Length!=0&&actualNormals.Length!=points.Length)
                ||(actualUv.Length!=0&&actualUv.Length!=points.Length))
                throw new InvalidOperationException("Incomplete published fence geometry or present vertex attributes.");
            vertices.AddRange(points);faces.AddRange(indices.Length==0?points:indices.Select(i=>points[i]));
            normals.AddRange(actualNormals.Length==0?Enumerable.Repeat(Vector3.Zero,points.Length):actualNormals);
            uv.AddRange(actualUv.Length==0?Enumerable.Repeat(Vector2.Zero,points.Length):actualUv);
            hasNormals.AddRange(Enumerable.Repeat(actualNormals.Length!=0,points.Length));
            hasUv.AddRange(Enumerable.Repeat(actualUv.Length!=0,points.Length));
        }
        return(vertices.ToArray(),normals.ToArray(),uv.ToArray(),faces.ToArray(),hasNormals.ToArray(),hasUv.ToArray());
    }
    private static string FenceTriangleKey(Vector3[] faces,int start)
    {
        string Point(Vector3 p)=>$"{BitConverter.SingleToInt32Bits(p.X)},{BitConverter.SingleToInt32Bits(p.Y)},{BitConverter.SingleToInt32Bits(p.Z)}";
        var corners=Enumerable.Range(0,3).Select(c=>Point(faces[start+c])).ToArray();
        return Enumerable.Range(0,3).Select(offset=>string.Join(";",Enumerable.Range(0,3).Select(c=>corners[(c+offset)%3])))
            .OrderBy(value=>value,StringComparer.Ordinal).First();
    }
    private static double FenceArea((double X,double Y,double Z) a,(double X,double Y,double Z) b,(double X,double Y,double Z) c)
    {
        var ex=b.X-a.X;var ey=b.Y-a.Y;var ez=b.Z-a.Z;var fx=c.X-a.X;var fy=c.Y-a.Y;var fz=c.Z-a.Z;
        var nx=ey*fz-ez*fy;var ny=ez*fx-ex*fz;var nz=ex*fy-ey*fx;
        return Math.Sqrt(nx*nx+ny*ny+nz*nz)*.5;
    }
    private static double RetainedFenceArea(Vector3[] source,int at,Transform3D transform,float cutZ)
    {
        var vertices=Enumerable.Range(0,3).Select(i=>source[at+i]).Select(p=>(X:(double)p.X,Y:(double)p.Y,Z:(double)p.Z)).ToArray();
        double Distance((double X,double Y,double Z) p)=>cutZ-(transform.Basis.X.Z*p.X+transform.Basis.Y.Z*p.Y+transform.Basis.Z.Z*p.Z+transform.Origin.Z);
        var polygon=new List<(double X,double Y,double Z)>();
        for(var i=0;i<3;i++)
        {
            var a=vertices[i];var b=vertices[(i+1)%3];var da=Distance(a);var db=Distance(b);
            if(da>=0)polygon.Add(a);
            if((da>=0)==(db>=0))continue;
            var t=da/(da-db);polygon.Add((a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t));
        }
        double area=0;for(var i=1;i+1<polygon.Count;i++)area+=FenceArea(polygon[0],polygon[i],polygon[i+1]);return area;
    }
    private static bool FenceFragmentFits(Vector3 p0,Vector3 p1,Vector3 p2,Vector3[] source,int at)
    {
        var a=source[at];var b=source[at+1];var c=source[at+2];
        var ex=(double)b.X-a.X;var ey=(double)b.Y-a.Y;var ez=(double)b.Z-a.Z;
        var fx=(double)c.X-a.X;var fy=(double)c.Y-a.Y;var fz=(double)c.Z-a.Z;
        var nx=ey*fz-ez*fy;var ny=ez*fx-ex*fz;var nz=ex*fy-ey*fx;
        var length=Math.Sqrt(nx*nx+ny*ny+nz*nz);if(length<1e-12)return false;
        var drop=Math.Abs(nx)>=Math.Abs(ny)&&Math.Abs(nx)>=Math.Abs(nz)?0:Math.Abs(ny)>=Math.Abs(nz)?1:2;
        var axis0=(drop+1)%3;var axis1=(drop+2)%3;
        var eU=(double)b[axis0]-a[axis0];var eV=(double)b[axis1]-a[axis1];
        var fU=(double)c[axis0]-a[axis0];var fV=(double)c[axis1]-a[axis1];var denominator=eU*fV-eV*fU;
        foreach(var p in new[]{p0,p1,p2})
        {
            var du=(double)p[axis0]-a[axis0];var dv=(double)p[axis1]-a[axis1];
            var u=(du*fV-dv*fU)/denominator;var v=(eU*dv-eV*du)/denominator;
            var distance=Math.Abs(((double)p.X-a.X)*nx+((double)p.Y-a.Y)*ny+((double)p.Z-a.Z)*nz)/length;
            if(distance>.00003||u<-.0001||v<-.0001||u+v>1.0001)return false;
        }
        return (p1-p0).Cross(p2-p0).Dot((b-a).Cross(c-a))>0;
    }
}
