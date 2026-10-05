using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Everything is seated after the yard's rigid move. Carryables enter the tree
    // only afterwards, so their authored reset/save pose is the actual new storage.
    private void BuildCozyBabaiStorage()
    {
        var core=GetNode<Node3D>("Act1CoreWorldGreybox");
        // Door-fix 2026-10-04: the relocated BabaiBathhouse stands at
        // (-18.6, 8.7) (BabaiRelocation second offset). The store's old anchor
        // (-18.0, 7.2) put its shell, floor slab and open leaf across the bath
        // interior, its firewood cluster and the bath door's 105-sample
        // outward sweep, so the door refused to open and the yard read as a
        // timber pile around the banya. The same store, yaw and inventory move
        // 7.0 m clear: bath shell 2.33 m, bath sweep 3.47 m, firewood 4.78 m,
        // well rim 0.73 m, outhouse 14.3 m, house envelope 2.6 m
        // (/tmp/urman_door_fix.md carries the arithmetic).
        var x=-11.0f;var z=7.0f;
        var ground=AgentBAct1HeightField.CollisionGround(x,z);
        var shed=new Node3D {Name="BabaiStorageBarn",Position=new(x,ground,z),RotationDegrees=new(0,180,0)};
        core.AddChild(shed);
        shed.SetMeta("presentationRole","one orderly tool store; original item/mechanism IDs remain available");
        var body=new StaticBody3D {Name="TimberMembers",CollisionLayer=2,CollisionMask=0};shed.AddChild(body);
        void Piece(string name,Vector3 size,Vector3 at,string paint="87745b",bool solid=true)
        {
            var mesh=RuralPropGeometry.Block(shed,name,size,at,PainterlyMaterialLibrary.ForColor(paint,"wood_fence"),.004f);
            if(solid)body.AddChild(new CollisionShape3D {Name=name+"Contact",Position=at,Shape=new BoxShape3D {Size=size}});
        }
        Piece("Floor",new(5f,.10f,4.4f),new(0,.05f,0));
        // At the back, the real workshop window is retained as the storage's window.
        Piece("RearLeft",new(1.8f,2.6f,.09f),new(-1.6f,1.35f,-2.2f));
        Piece("RearRight",new(1.8f,2.6f,.09f),new(1.6f,1.35f,-2.2f));
        Piece("RearSill",new(1.4f,.95f,.09f),new(0,.575f,-2.2f));
        Piece("RearHead",new(1.4f,.72f,.09f),new(0,2.29f,-2.2f));
        foreach(var side in new[]{-1f,1f})
        {
            Piece("Side"+side,new(.10f,2.6f,4.4f),new(side*2.5f,1.35f,0));
            Piece("FrontPier"+side,new(1.65f,2.6f,.10f),new(side*1.675f,1.35f,2.2f));
            Piece("DoorJamb"+side,new(.10f,2.4f,.15f),new(side*.84f,1.25f,2.23f),"596b59");
        }
        Piece("DoorHead",new(1.78f,.25f,.16f),new(0,2.53f,2.22f),"596b59");
        // A modest pitched barn roof: snow uses its own material, not white wood.
        var pitch=Mathf.Atan2(.75f,2.7f);
        var slopeLength=Mathf.Sqrt(2.7f*2.7f+.75f*.75f);
        foreach(var side in new[]{-1f,1f})
        {
            var centre=new Vector3(side*1.35f,3.125f,0);
            var rotation=new Vector3(0,0,-side*pitch);
            var roof=RuralPropGeometry.Block(shed,"PitchedRoof"+side,new(slopeLength,.13f,4.8f),centre,
                PainterlyMaterialLibrary.ForColor("5f665b","roof_metal"),.004f);
            roof.Rotation=rotation;
            body.AddChild(new CollisionShape3D {Name="RoofContact"+side,Position=centre,Rotation=rotation,
                Shape=new BoxShape3D {Size=new(slopeLength,.13f,4.8f)}});
            var snow=RuralPropGeometry.Block(shed,"RoofSnow"+side,new(slopeLength,.05f,4.7f),centre+Vector3.Up*.10f,
                PainterlyMaterialLibrary.ForColor("e6edf1","snow_roof"),.008f);
            snow.Rotation=rotation;
        }
        Piece("Ridge",new(.12f,.14f,4.85f),new(0,3.54f,0),"697960",false);
        // Open leaf lies beside the entrance, giving a full 1.58m standing doorway.
        Piece("OpenDoor",new(.10f,2.30f,.78f),new(-.87f,1.25f,2.65f),"697960");
        Piece("Handle",new(.055f,.16f,.06f),new(-.80f,1.25f,2.9f),"5b5c52",false);
        var workshop=core.GetNode<Node3D>("YardRepairCorner");
        var before=workshop.GlobalTransform;
        workshop.GlobalTransform=new Transform3D(new Basis(Vector3.Up,Mathf.Pi),shed.ToGlobal(new(0,.10f,-1.42f)));
        var workshopMove=workshop.GlobalTransform*before.AffineInverse();
        workshop.SetMeta("storedIn","BabaiStorageBarn");
        // Tiny redundant canopy disappears inside the larger store roof.
        foreach(var member in workshop.GetChildren().OfType<Node3D>().Where(n=>n.Name.ToString().StartsWith("Roof",StringComparison.Ordinal)))
            HidePresentationNode(member);
        if(_carryCoordinator is { } carry)
        {
            foreach(var prop in carry.Items)
            {
                // Coordinator is still detached. Its local pose is the original authored world pose.
                var old=prop.Transform;
                if(!BabaiRelocation.InsideOldYard(old.Origin.X,old.Origin.Z,.6f))continue;
                var moved=BabaiRelocation.Transform*old;
                if(prop.ItemId is "carry-cloth" or "carry-hook" or "carry-lantern")moved=workshopMove*moved;
                else if(prop.ItemId!="carry-axe")
                {
                    var slot=prop.ItemId switch {
                        "carry-crate"=>new Vector3(-1.85f,.10f,.85f),
                        "carry-bucket"=>new Vector3(-1.1f,.10f,.85f),
                        "carry-log"=>new Vector3(1.65f,.10f,.75f),
                        "carry-board"=>new Vector3(1.40f,.10f,-.45f),
                        "carry-tool-shovel"=>new Vector3(-2.08f,.10f,-.65f),
                        "carry-tool-pole"=>new Vector3(1.98f,.10f,-.65f),
                        _=>new Vector3(1.7f,.10f,-1.0f)};
                    moved.Origin=shed.ToGlobal(slot);
                    moved.Basis=shed.GlobalBasis;
                }
                prop.Transform=moved;
                prop.SetMeta("babaiRelocated",true);
            }
            foreach(var mechanism in FindDescendants<YardMechanism>(carry))
            {
                if(mechanism.RestPoint!=Vector3.Zero&&BabaiRelocation.InsideOldYard(mechanism.RestPoint.X,mechanism.RestPoint.Z,.6f))
                    mechanism.RestPoint=workshopMove*(BabaiRelocation.Transform*mechanism.RestPoint);
                if(mechanism.AlternativeApproach is { } side&&BabaiRelocation.InsideOldYard(side.X,side.Z,.6f))
                    mechanism.AlternativeApproach=workshopMove*(BabaiRelocation.Transform*side);
                if(mechanism.Action is YardMechanism.Operation.RestBoard or YardMechanism.Operation.DetachHook)
                    mechanism.RestYaw+=180f;
            }
            TidyBanyaYardAndBuriedAxe(core,carry);
            AddChild(carry);
        }
        BuildBabaiOuthouse(core,new(-19.25f,0,-9.3f));
        RegisterAddressInheritedBuilding(shed,"act1/babai-storage-barn","ADR-BABAI","shed");
        RegisterAddressInheritedBuilding(core.GetNode<Node3D>("BabaiMoonOuthouse"),"act1/babai-moon-outhouse","ADR-BABAI","outhouse");
        SetMeta("babaiStorageBuilt",true);
    }

    /// <summary>
    /// Banya-yard pass (author 2026-10-04: «около бани очень много мусора лежит»).
    /// The storage barn moved away earlier the same day; what still read as debris
    /// at the banya was a pre-relayout picket run that travelled with the bath and
    /// grazed its rear-west shell, plus the EX05 buried-axe drift that sat on the
    /// door's exit line while its axe item stayed 8.7 m away inside the barn.
    /// The stranded run is retired like every other leftover kit fence; the whole
    /// drift/handle/axe group moves to the woodpile's outer end, clear of the door
    /// band and of the stair landing. All ids, targets and interactions survive.
    /// </summary>
    private void TidyBanyaYardAndBuriedAxe(Node3D core,CarryCoordinator carry)
    {
        // 1. The authored west-yard-boundary run was within the relocation's
        // 4.6 m bath-cluster radius, so it travelled with the bath and now crosses
        // the bath's rear-west shell while bounding nothing in the new yard.
        var stranded=core.GetNodeOrNull<Node3D>("Act1AuthoredExteriorKitPresentation/BabaiYardAuthoredFence");
        if(stranded is not null)
        {
            if(FindDescendants<InteractionTarget>(stranded).Any())
                throw new InvalidOperationException("The stranded yard fence carries gameplay and must not be retired.");
            HidePresentationNode(stranded);
            stranded.SetMeta("suppressionReason",
                "banya yard 2026-10-04: pre-relayout west-boundary picket run (9.99 m, yaw 0 at -16.135,11.027), carried with the bath and crossing its rear-west shell; no boundary counterpart in the relayout v3 yard");
            SetMeta("banyaYardStrandedFenceRetired",true);
        }
        // 2. EX05 drift + handle + axe. The drift sat at bath-local (3.57,1.35),
        // 0.47 m off the stair landing and inside the door's Z band; the axe item
        // itself only took the rigid relocation move and stayed inside the
        // storage barn. Move the whole group to the woodpile's outer end:
        // bath-local (4.75,1.10) = +1.18 m local X, -0.25 m local Z.
        if(_bathhouse is null)return;
        var drift=core.GetNodeOrNull<Node3D>("ToolSnowPileWood");
        var handle=core.GetNodeOrNull<Node3D>("BuriedAxeHandleTip");
        var shift=_bathhouse.GlobalBasis.X.Normalized()*1.18f-_bathhouse.GlobalBasis.Z.Normalized()*.25f;
        void Shift(Node3D? node)
        {
            if(node is null)return;
            var before=node.GlobalPosition;
            var after=before+shift;
            after.Y=before.Y+(AgentBAct1HeightField.CollisionGround(after.X,after.Z)
                -AgentBAct1HeightField.CollisionGround(before.X,before.Z));
            node.GlobalPosition=after;
            node.SetMeta("banyaYardTidy","moved to the woodpile's outer end, clear of the banya door band and stair landing; same target and save id");
        }
        Shift(drift);
        Shift(handle);
        var axe=carry.Items.FirstOrDefault(item=>item.ItemId=="carry-axe");
        if(drift is not null&&axe is not null)
        {
            axe.Position=drift.GlobalPosition with { Y=axe.Position.Y };
            SeatAuthoredCarryOnSurface(axe,0f);
            axe.SetMeta("banyaYardTidy","reunited with its authored drift by the woodpile; the rigid-only storage move had left it inside the storage barn");
        }
    }

    private static void BuildBabaiOuthouse(Node3D core,Vector3 at)
    {
        at.Y=AgentBAct1HeightField.CollisionGround(at.X,at.Z);
        var root=new Node3D {Name="BabaiMoonOuthouse",Position=at,RotationDegrees=new(0,90,0)};core.AddChild(root);
        var body=new StaticBody3D {Name="WoodenShell",CollisionLayer=2,CollisionMask=0};root.AddChild(body);
        void Box(string name,Vector3 size,Vector3 p,string colour="9c8565")
        {
            RuralPropGeometry.Block(root,name,size,p,PainterlyMaterialLibrary.ForColor(colour,"wood_fence"),.004f);
            body.AddChild(new CollisionShape3D {Position=p,Shape=new BoxShape3D{Size=size}});
        }
        Box("Floor",new(1.40f,.11f,1.5f),new(0,.055f,0));
        Box("Back",new(1.4f,2.18f,.07f),new(0,1.16f,-.75f));
        foreach(var sign in new[]{-1f,1f})Box("Side"+sign,new(.07f,2.18f,1.5f),new(sign*.70f,1.16f,0));
        Box("Roof",new(1.65f,.10f,1.8f),new(0,2.31f,0),"606456");
        RuralPropGeometry.Block(root,"SnowRoof",new(1.62f,.045f,1.76f),new(0,2.38f,0),PainterlyMaterialLibrary.ForColor("eaf0f4","snow_roof"),.008f);
        // The crescent is a real opening cut into the mesh, not a yellow decal.
        const int nx=52,ny=92;const float width=1.25f,height=2.12f;
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        bool Moon(float x,float y)=>x*x+(y-1.66f)*(y-1.66f)<.185f*.185f
            &&(x-.075f)*(x-.075f)+(y-1.70f)*(y-1.70f)>.165f*.165f;
        for(var i=0;i<nx;i++)for(var j=0;j<ny;j++)
        {
            var x0=(i/(float)nx-.5f)*width;var x1=((i+1)/(float)nx-.5f)*width;
            var y0=j/(float)ny*height+.13f;var y1=(j+1)/(float)ny*height+.13f;
            if(Moon((x0+x1)*.5f,(y0+y1)*.5f))continue;
            TimberHomeStyle.AppendMetricBox(surface,new Transform3D(Basis.Identity.Scaled(new Vector3(x1-x0,y1-y0,.045f)),new((x0+x1)*.5f,(y0+y1)*.5f,.77f)));
        }
        surface.Index();var door=new MeshInstance3D {Name="MoonCutTimberDoor",Mesh=surface.Commit(),MaterialOverride=PainterlyMaterialLibrary.ForColor("aa9371","wood_fence")};root.AddChild(door);
        body.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new(width,height,.06f)},Position=new(0,height*.5f+.13f,.77f)});
        Box("DoorHandle",new(.045f,.15f,.045f),new(.46f,1.1f,.82f),"59584e");
    }
}
