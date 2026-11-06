using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildPoliceOfficer(Node3D room)
    {
        PolicePostAssets.Attach(room,"office_chair","PoliceOfficerChair",new(-3.1f,0,1.43f));
        FacilitySolid(room,"PoliceOfficerChairSeat",new(.46f,.06f,.46f),new(-3.1f,.46f,1.45f),"5e5b48","wood_furniture");
        FacilitySolid(room,"PoliceOfficerChairBack",new(.46f,.55f,.055f),new(-3.1f,.75f,1.19f),"655a47","wood_furniture");
        foreach(var x in new[]{-3.28f,-2.92f}) foreach(var z in new[]{1.27f,1.63f})
            FacilitySolid(room,"PoliceChairLeg"+x+"_"+z,new(.045f,.44f,.045f),new(x,.22f,z),"615841","wood_furniture");
        foreach(var mesh in room.GetChildren().OfType<MeshInstance3D>())
            if(mesh.Name.ToString().StartsWith("PoliceOfficerChair",StringComparison.Ordinal)
                ||mesh.Name.ToString().StartsWith("PoliceChairLeg",StringComparison.Ordinal))mesh.Visible=false;
        var officer=GeneratedCharacterKitDressing.Attach(room,"police-duty-officer","Resident",new(-3.1f,0,1.43f),sheltered:true);
        officer.Name="PoliceDutyOfficer";
        officer.SetMeta("narrativeRole","author-requested unnamed background duty officer; existing Rinat story actor is independent");
        foreach(var mesh in officer.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>())
        {
            var name=mesh.Name.ToString();
            if(name.Contains("_Hat_",StringComparison.Ordinal)) {mesh.Visible=false;continue;}
            if(name.Contains("_Coat",StringComparison.Ordinal)||name.Contains("_Trousers_",StringComparison.Ordinal))
                mesh.MaterialOverride=RuralPropMaterials.Surface("cloth","46596b");
            if(name.Contains("_Boot",StringComparison.Ordinal))
                mesh.MaterialOverride=RuralPropMaterials.Surface("rubber","343933");
        }
        var skeleton=officer.FindChildren("*",nameof(Skeleton3D),true,false).OfType<Skeleton3D>().Single();
        var head=skeleton.FindBone("Head");
        if(head<0)throw new InvalidOperationException("Duty officer needs the real human head bone.");
        var rest=skeleton.GetBoneGlobalRest(head);
        var attachment=new BoneAttachment3D {Name="PoliceCapAttachment",BoneName="Head"};skeleton.AddChild(attachment);
        var cap=new Node3D {Name="PolicePeakedCap",Transform=rest.AffineInverse()*new Transform3D(Basis.Identity,rest.Origin+Vector3.Up*.15f)};
        attachment.AddChild(cap);
        PoliceEllipsoid(cap,"CapCrown",new(.135f,.036f,.128f),new(0,.032f,0),"354858","cloth");
        PoliceEllipsoid(cap,"CapBand",new(.114f,.027f,.111f),Vector3.Zero,"3f4746","cloth");
        PoliceEllipsoid(cap,"CapPiping",new(.117f,.007f,.114f),new(0,.024f,0),"974e4a","cloth");
        PoliceEllipsoid(cap,"CapVisor",new(.125f,.008f,.089f),new(0,-.026f,.102f),"262e2b","leather");
        PoliceEllipsoid(cap,"CapPlainBadge",new(.013f,.018f,.003f),new(0,.007f,.112f),"c1af71","metal");
        foreach(var side in new[]{"l","r"})
        {
            // Place each board midway along the actual clavicle-to-upper-arm
            // shoulder line, then lift it slightly above the coat surface.
            // Attach to the clavicle so the board stays at the shoulder seam
            // when the seated arm moves.
            var bone="clavicle_"+side;var index=skeleton.FindBone(bone);
            var armIndex=skeleton.FindBone("upperarm_"+side);
            if(index<0||armIndex<0)throw new InvalidOperationException("Duty officer needs real shoulder bones for shoulder boards.");
            var shoulderRest=skeleton.GetBoneGlobalRest(index);
            var armRest=skeleton.GetBoneGlobalRest(armIndex);
            var boardCenter=(shoulderRest.Origin+armRest.Origin)*.5f+Vector3.Up*.055f;
            var outward=(armRest.Origin-shoulderRest.Origin).Normalized();
            var across=Vector3.Up.Cross(outward).Normalized();
            var boardBasis=new Basis(across,outward.Cross(across).Normalized(),outward);
            var mount=new BoneAttachment3D {Name="PoliceEpauletteAttachment"+side,BoneName=bone};skeleton.AddChild(mount);
            var epaulette=new Node3D {Name="PoliceShoulderBoard"+side,
                Transform=shoulderRest.AffineInverse()*new Transform3D(boardBasis,boardCenter)};mount.AddChild(epaulette);
            var board=AddVisualBox(epaulette,"Board",new(.075f,.012f,.14f),Vector3.Zero,"354858","cloth");
            board.MaterialOverride=RuralPropMaterials.Surface("cloth","354858");
            foreach(var edge in new[]{-1f,1f})
                AddVisualBox(epaulette,"RedPiping"+edge,new(.004f,.014f,.14f),new(edge*.035f,0,0),"974e4a","cloth");
            AddVisualBox(epaulette,"Braid",new(.051f,.003f,.010f),new(0,.008f,.03f),"b2a473","cloth");
            DiscoveryCylinder(epaulette,"Button",.009f,.009f,.005f,new(0,.012f,-.045f),"c5b57a");
        }
        var presentation=new PolicePostPresentation {Name="PolicePostPresentation",Room=room,Officer=officer,
            Mouse=room.GetNode<Node3D>("PoliceCellMouse"),IsActive=()=>FacilityExteriorActive};room.AddChild(presentation);
        var talk=FacilityTarget("PoliceDutyOfficerTalk","presentation.police-duty-officer","Поговорить с участковым",room,new(-3.1f,1.4f,2.34f),new(1.2f,1.25f,.12f));
        talk.PresentationRepeatAvailable=()=>FacilityExteriorActive
            &&GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player&&InPolicePost(player.GlobalPosition);
        talk.PresentationRepeat=presentation.BeginGreeting;
    }
}
