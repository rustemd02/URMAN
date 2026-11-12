using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private readonly record struct BabaiShellOpening(string Name, float Across, float Bottom, float Width, float Height, bool Door=false);

    private void BuildBabaiNewExteriorGeometry(Node3D shell, Material logMaterial, Material roofMaterial, Material snowMaterial)
    {
        var contacts = new StaticBody3D { Name="BabaiContinuousShellContacts", CollisionLayer=1, CollisionMask=0 };
        contacts.SetMeta("collisionOwner","babai-continuous-house");
        shell.AddChild(contacts);
        var lining=PainterlyMaterialLibrary.ForColor("d5c9ae","plaster_domestic",sheltered:true);
        var trim=PainterlyMaterialLibrary.ForColor("e4e0d4","wood_painted_trim");
        void Contact(string name,Vector3 size,Vector3 at)
            => contacts.AddChild(new CollisionShape3D {Name=name,Position=at,Shape=new BoxShape3D {Size=size}});
        MeshInstance3D MeshPart(string name,Mesh mesh,Vector3 at,Material material,Vector3 rotation=default)
        {
            var part=new MeshInstance3D {Name=name,Mesh=mesh,Position=at,Rotation=rotation,MaterialOverride=material};
            shell.AddChild(part);return part;
        }
        var front=new List<BabaiShellOpening>
        {
            new("FrontWest",-5.1f,.74f,1.06f,1.4f),new("Front0",-.85f,.74f,1.06f,1.4f),
            new("Front1",1f,.74f,1.06f,1.4f),new("Front2",2.85f,.74f,1.06f,1.4f),
            new("FrontEast",5.05f,.74f,1.06f,1.4f),new("Entry",StyleBenchmarkInteriorFactory.DoorX,0,1.30f,2.25f,true),
            new("AtticFrontWest",-2.4f,6.4f,.9f,1.2f),new("AtticFrontEast",2.4f,6.4f,.9f,1.2f)
        };
        var rear=new List<BabaiShellOpening> {new("AtticRear",0,6.4f,.9f,1.2f)};
        var upper=new[]{-4.8f,-2.4f,0f,2.4f,4.8f};
        for(var i=0;i<upper.Length;i++)
        {
            front.Add(new("UpperFront"+i,upper[i],3.54f,1.06f,1.4f));
            rear.Add(new("UpperRear"+i,i==2?1f:upper[i],i<2?4f:3.54f,1.06f,i<2?1.2f:1.4f));
        }
        var rearLower=new[]{-4.8f,-2.4f,1.6f,4.7f};
        for(var i=0;i<rearLower.Length;i++)rear.Add(new("Rear"+i,rearLower[i],.74f,1.06f,1.4f));
        var west=new List<BabaiShellOpening>();var east=new List<BabaiShellOpening>();
        for(var floor=0;floor<2;floor++)
        {
            west.Add(new("WestRear"+floor,-5.5f,floor==0?1.15f:3.54f,1.06f,floor==0?1.05f:1.4f));
            west.Add(new("WestFront"+floor,.5f,.74f+floor*2.8f,1.06f,1.4f));
            east.Add(new("EastRear"+floor,-5.5f,.74f+floor*2.8f,1.06f,1.4f));
        }

        void Wall(string name,bool alongX,float plane,float from,float to,float outward,float yaw,List<BabaiShellOpening> openings)
        {
            using var timber=new SurfaceTool();timber.Begin(Mesh.PrimitiveType.Triangles);
            const float pitch=.26f;
            // Alternate lap lengths and half-course elevations at the corners.
            // These are complete horizontal logs, with round visible end grain.
            for(var row=0;row*pitch<7.4f;row++)
            {
                var y=.13f+row*pitch+(alongX?0:.13f);
                if(y>7.4f)continue;
                var extension=row%2==0?.22f:.08f;
                var cylinder=new CylinderMesh {TopRadius=.14f,BottomRadius=.14f,Height=to-from+extension*2,
                    RadialSegments=12,Rings=1};
                var rotation=alongX?new Vector3(0,0,Mathf.Pi*.5f):new Vector3(Mathf.Pi*.5f,0,0);
                var at=alongX?new Vector3((from+to)*.5f,y,plane):new Vector3(plane,y,(from+to)*.5f);
                timber.AppendFrom(cylinder,0,new Transform3D(Basis.FromEuler(rotation),at));
            }
            // Level wall plate closes the final partial course beneath both
            // gables and eaves; clip it with the same attic openings below.
            var plateSize=alongX?new Vector3(to-from,.18f,.26f):new Vector3(.26f,.18f,to-from);
            var plateAt=alongX?new Vector3((from+to)*.5f,7.31f,plane):new Vector3(plane,7.31f,(from+to)*.5f);
            timber.AppendFrom(new BoxMesh {Size=plateSize},0,new Transform3D(Basis.Identity,plateAt));
            var sillSize=alongX?new Vector3(to-from,.14f,.26f):new Vector3(.26f,.14f,to-from);
            timber.AppendFrom(new BoxMesh {Size=sillSize},0,
                new Transform3D(Basis.Identity,plateAt with {Y=.06f}));
            var logs=MeshPart("Babai_Wall_"+name,timber.Commit(),Vector3.Zero,logMaterial);
            Aabb Hole(BabaiShellOpening o)=>alongX
                ?new(new Vector3(o.Across-o.Width*.5f,o.Bottom,plane-.4f),new Vector3(o.Width,o.Height,.8f))
                :new(new Vector3(plane-.4f,o.Bottom,o.Across-o.Width*.5f),new Vector3(.8f,o.Height,o.Width));
            foreach(var opening in openings)CutBabaiMeshOpening(logs,shell,Hole(opening));
            // Flat inner skins and physical strips share the same exact holes.
            var heights=new[]{0f,7.4f}.Concat(openings.SelectMany(o=>new[]{Mathf.Clamp(o.Bottom,0,7.4f),Mathf.Clamp(o.Bottom+o.Height,0,7.4f)}))
                .Distinct().OrderBy(y=>y).ToArray();
            using var plaster=new SurfaceTool();plaster.Begin(Mesh.PrimitiveType.Triangles);
            for(var band=1;band<heights.Length;band++)
            {
                var bottom=heights[band-1];var top=heights[band];var cursor=from;
                foreach(var hole in openings.Where(o=>o.Bottom<top&&o.Bottom+o.Height>bottom).OrderBy(o=>o.Across))
                {Span(cursor,hole.Across-hole.Width*.5f);cursor=hole.Across+hole.Width*.5f;}
                Span(cursor,to);
                void Span(float a,float b)
                {
                    if(b-a<.001f)return;
                    var size=alongX?new Vector3(b-a,top-bottom,.20f):new Vector3(.20f,top-bottom,b-a);
                    var at=alongX?new Vector3((a+b)*.5f,(top+bottom)*.5f,plane):new Vector3(plane,(top+bottom)*.5f,(a+b)*.5f);
                    Contact(name+"Strip"+contacts.GetChildCount(),size,at);
                    var inner=at+(alongX?Vector3.Back:Vector3.Right)*(-outward*.095f);
                    var skin=alongX?new Vector3(size.X,size.Y,.012f):new Vector3(.012f,size.Y,size.Z);
                    plaster.AppendFrom(new BoxMesh {Size=skin},0,new Transform3D(Basis.Identity,inner));
                }
            }
            MeshPart("BabaiInnerLining"+name,plaster.Commit(),Vector3.Zero,lining);
            foreach(var o in openings.Where(o=>!o.Door))
                BuildBabaiActiveWindow(shell,o.Name,alongX?new(o.Across,o.Bottom+o.Height*.5f,plane):new(plane,o.Bottom+o.Height*.5f,o.Across),yaw,o.Width,o.Height);
            if(!alongX)return;
            var gable=MeshPart("Babai_Wall_"+name+"Gable",new PrismMesh {Size=new(13.2f,2.5f,.20f),LeftToRight=.5f},new(0,8.65f,plane),logMaterial);
            foreach(var o in openings.Where(o=>o.Bottom+o.Height>7.4f))CutBabaiMeshOpening(gable,shell,Hole(o));
            contacts.AddChild(new CollisionShape3D {Name=name+"GableContact",Transform=gable.Transform,Shape=gable.Mesh.CreateTrimeshShape()});
        }
        Wall("Front",true,3.60f,-6.6f,6.6f,1,0,front);
        Wall("Back",true,-7.60f,-6.6f,6.6f,-1,180,rear);
        Wall("West",false,-6.60f,-7.6f,3.6f,-1,-90,west);
        Wall("East",false,6.60f,-7.6f,3.6f,1,90,east);

        var slope=2.5f/6.5f;var angle=Mathf.Atan(slope);var run=7.05f;
        var length=run/Mathf.Cos(angle);
        var innerRoof=PainterlyMaterialLibrary.ForColor("927a5c","wood_floor_planked",sheltered:true);
        foreach(var side in new[]{-1f,1f})
        {
            var rotation=new Vector3(0,0,-side*angle);
            var normal=new Vector3(side*Mathf.Sin(angle),Mathf.Cos(angle),0);
            var centre=new Vector3(side*run*.5f,9.9f-run*.5f*slope,-2f);
            var roof=MeshPart("BabaiRoof"+side,new BoxMesh {Size=new(length,.16f,12.3f)},centre+normal*.08f,roofMaterial,rotation);
            contacts.AddChild(new CollisionShape3D {Name="RoofContact"+side,Transform=roof.Transform,Shape=new BoxShape3D {Size=new(length,.16f,12.3f)}});
            MeshPart("LoftRoofLining"+side,new BoxMesh {Size=new(length,.035f,11.2f)},centre-normal*.0175f,innerRoof,rotation);
            MeshPart("BabaiRoofSnow"+side,new BoxMesh {Size=new(length,.10f,12.3f)},centre+normal*.21f,snowMaterial,rotation);
            foreach(var (name,z) in new[]{("Front",4.12f),("Back",-8.12f)})
                MeshPart("Babai_"+name+(side<0?"_VergeLeft":"_VergeRight"),new BoxMesh {Size=new(length,.19f,.12f)},
                    centre with {Z=z},trim,rotation);
            MeshPart("Babai_Eave_"+side,new BoxMesh {Size=new(.14f,.22f,12.3f)},
                new(side*7.03f,9.9f-7.03f*slope,-2),trim);
        }
        MeshPart("BabaiRoofRidge",new CylinderMesh {TopRadius=.15f,BottomRadius=.15f,Height=12.3f,RadialSegments=12},
            new(0,10.02f,-2),roofMaterial,new(Mathf.Pi*.5f,0,0));
        MeshPart("BabaiRoofRidgeSnow",new CylinderMesh {TopRadius=.11f,BottomRadius=.11f,Height=12.3f,RadialSegments=12},
            new(0,10.14f,-2),snowMaterial,new(Mathf.Pi*.5f,0,0));
        foreach(var z in new[]{-5f,-2f,1.4f})
        {
            var size=new Vector3(9.7f,.16f,.14f);var at=new Vector3(0,8.08f,z);
            MeshPart("LoftCollarBeam"+z,RuralPropGeometry.BevelBox(size,.01f),at,logMaterial);
            Contact("LoftCollarBeamContact"+z,size,at);
        }
        // A low landing meets the retained entrance panel/target, without rails
        // across its approach. Terrain/foundation integration remains the caller.
        var porchSize=new Vector3(2.4f,.16f,1.6f);
        var porchAt=new Vector3(StyleBenchmarkInteriorFactory.DoorX,-.08f,4.28f);
        MeshPart("BabaiEntranceLanding",RuralPropGeometry.BevelBox(porchSize,.015f),porchAt,logMaterial);
        Contact("EntranceLandingContact",porchSize,porchAt);
    }
}
