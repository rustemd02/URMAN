using Godot;

namespace Urman.Godot;

public static partial class VehicleVisualFactory
{
    /// <summary>Licensed VAZ-2106 static prop; never registered in VehicleFleet.</summary>
    public static Node3D BuildDistrictZhiguli()
    {
        var root=new Node3D {Name="DistrictZhiguli"};
        root.SetMeta("drivable",false);root.SetMeta("roadworthy",false);
        root.SetMeta("flatTyre",true);
        root.SetMeta("assetOrigin","VAZ-2106 by domhathair, CC BY 4.0; Objaverse public licensed copy; reduced static derivative");
        root.SetMeta("assetSource","https://sketchfab.com/3d-models/a73b3cdeddd846209bf7c73d5f04d20f");
        var model=PolicePostAssets.Attach(root,"vaz2106_static","LicensedVAZ2106",Vector3.Zero);
        model.SetMeta("flatTyre",true);
        // VIS-105: the imported photo-PBR slots are rebinded onto the same URMAN
        // families as the Niva (paint / steel / rubber / glass / settled snow).
        // Geometry, UV, metre scale, the flattened tyre and the CC BY attribution
        // are untouched; the source mesh resource is never edited, only the
        // instance's surface overrides.
        var unmapped=new System.Collections.Generic.List<string>();
        RebindThirdPartyVehicleMaterials(model,"4c5d4c",unmapped);
        if(unmapped.Count>0)
            GD.PushWarning("District Zhiguli keeps unnormalised source materials: "+string.Join(", ",unmapped));
        root.SetMeta("materialNormalization","imported slots rebinded to URMAN vehicle families (VIS-105)");
        // Period-neutral lettering; the imported UV maps remain on every original part.
        foreach(var sign in new[]{-1f,1f})
        {
            var band=new MeshInstance3D {Name="DistrictDoorBand"+(sign<0?"Left":"Right"),
                Position=new(sign*.790f,.78f,.05f),
                Mesh=new BoxMesh {Size=new(.003f,.205f,1.76f)},
                MaterialOverride=RuralPropMaterials.Surface("steel","3b5571")};root.AddChild(band);
            var label=CabinLabel(root,"DistrictDoorLettering"+(sign<0?"Left":"Right"),"УЧАСТКОВЫЙ",
                new(sign*.794f,.78f,.05f),.0027f,new Color("e6e4d0"));
            label.FontSize=48;label.RotationDegrees=new(0,sign*90,0);
        }
        return root;
    }
}
