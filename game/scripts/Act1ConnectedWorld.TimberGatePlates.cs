using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Author 2026-10-04/05: the rebuilt yard wicket may carry the address plate
    // only for the four documented gate-board exceptions. Every other addressed
    // house keeps the plate mounted beside its door by AddressFacadeMount
    // (1.5-2.1 m band) or honestly has none. Rewriting every gate lot's plate
    // onto the wicket (2026-10-02 behaviour, ~84 lots) defeated the wall band
    // and masked SIGN_MOUNT_NOT_FOUND with an invented mount; the wickets are
    // also below the band (+1.47 m) and carry no wall/sightline check.
    private static readonly string[] DocumentedGatePlateExceptions=["ADR-H032","ADR-H034","ADR-H045","ADR-H046"];

    private void MountTimberGateAddressPlates(Node3D fences)
    {
        var registry=AddressRegistry!;
        foreach(var gate in fences.GetChildren().OfType<Node3D>().Where(n=>n.HasMeta("addressId")))
        {
            var id=gate.GetMeta("addressId").AsString();
            if(!DocumentedGatePlateExceptions.Contains(id,StringComparer.Ordinal))continue;
            if(!registry.Addresses.ContainsKey(id))continue;
            var sign=_addressSigns.FirstOrDefault(p=>p.AddressId==id);
            if(sign is null)
            {
                sign=new AddressSignVisualComponent();_addressRoot!.AddChild(sign);sign.Bind(registry,id);_addressSigns.Add(sign);
                var read=new InteractionTarget {Name="Read_"+id,InteractionId="urman.address:read/"+id,
                    Prompt="Прочитать табличку",CollisionLayer=4,CollisionMask=0,PresentationRepeatAvailable=()=>true};
                sign.AddChild(read);read.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new(1.2f,.44f,.07f)}});
                read.PresentationRepeat=()=> {AddressRead?.Invoke(id);
                    (GetTree().GetFirstNodeInGroup("player_controller")as FirstPersonController)?.NotifyTraversal(registry.FormatAddress(id));};
            }
            var outward=-gate.GetMeta("inward").AsVector3();
            var along=gate.GetMeta("along").AsVector3();
            var centre=gate.GlobalPosition+along*1.72f+Vector3.Up*1.47f+outward*.13f;
            var backing=new Node3D {Name="AddressPlateBacking",Position=fences.ToLocal(centre)};
            fences.AddChild(backing);backing.GlobalBasis=new Basis(Vector3.Up,Mathf.Atan2(outward.X,outward.Z));
            // The painted support's street face meets the plate's back face:
            // the plate rim box spans -0.012..+0.012 along local Z, so the
            // 0.045 board centred at -0.0345 has its front at -0.012. The old
            // -0.045 left a 1.05 cm air gap behind the plate, visible from
            // the side with the fasteners apparently floating (audit A3).
            RuralPropGeometry.Block(backing,"PaintedSupport",new(1.22f,.46f,.045f),new(0,0,-.0345f),
                PainterlyMaterialLibrary.ForColor("ded7bd","wood_painted_trim"),.007f);
            sign.GlobalPosition=centre;sign.GlobalBasis=backing.GlobalBasis;
            sign.SetMeta("timberGateMount",gate.GetPath().ToString());
            sign.SetMeta("addressSignSupport",backing.GetPath().ToString());
        }
    }
}
