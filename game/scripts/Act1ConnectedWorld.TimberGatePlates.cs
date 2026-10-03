using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void MountTimberGateAddressPlates(Node3D fences)
    {
        var registry=AddressRegistry!;
        foreach(var gate in fences.GetChildren().OfType<Node3D>().Where(n=>n.HasMeta("addressId")))
        {
            var id=gate.GetMeta("addressId").AsString();
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
            RuralPropGeometry.Block(backing,"PaintedSupport",new(1.22f,.46f,.045f),new(0,0,-.045f),
                PainterlyMaterialLibrary.ForColor("ded7bd","wood_painted_trim"),.007f);
            sign.GlobalPosition=centre;sign.GlobalBasis=backing.GlobalBasis;
            sign.SetMeta("timberGateMount",gate.GetPath().ToString());
            sign.SetMeta("addressSignSupport",backing.GetPath().ToString());
        }
    }
}
