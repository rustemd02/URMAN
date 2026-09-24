using System;
using Godot;

namespace Urman.Godot;

/// <summary>Real mounted plate. All visible letters come from the address record.</summary>
public partial class AddressSignVisualComponent : Node3D
{
    public string AddressId { get; private set; } = "";
    private SettlementRegistry? _registry;
    private Label3D? _tatar, _russian, _number;
    public void Bind(SettlementRegistry registry, string addressId)
    {
        _registry=registry; AddressId=addressId;
        Name="AddressPlate_"+addressId;
        SetMeta("address_id",addressId);
        SetMeta("textSource","SettlementRegistry");
        SetMeta("notQuestMarker",true);
        var frame=new StandardMaterial3D{AlbedoColor=Color.FromHtml("a3a091"),Metallic=.4f,Roughness=.78f};
        var enamel=new StandardMaterial3D{AlbedoColor=Color.FromHtml("293b43"),Metallic=.12f,Roughness=.65f};
        // Sizes follow AddressFacadeMount's plate: 0.60 x 0.23 m.
        var w=AddressFacadeMount.HalfWidth*2f; var h=AddressFacadeMount.HalfHeight*2f;
        AddChild(new MeshInstance3D{Name="FoldedMetalRim",Mesh=new BoxMesh{Size=new(w,h,.024f)},MaterialOverride=frame});
        AddChild(new MeshInstance3D{Name="EnamelFace",Position=new(0,0,.014f),Mesh=new BoxMesh{Size=new(w-.02f,h-.02f,.008f)},MaterialOverride=enamel});
        _tatar=Line("TatarStreet",new(-.09f,.042f,.02f),34);
        _russian=Line("RussianStreet",new(-.09f,-.036f,.02f),30);
        _number=Line("HouseNumber",new(.215f,0,.021f),65);
        foreach(var x in new[]{-AddressFacadeMount.RivetX,AddressFacadeMount.RivetX})
        foreach(var y in new[]{-AddressFacadeMount.RivetY,AddressFacadeMount.RivetY})
            AddChild(new MeshInstance3D{Name="Rivet",Position=new(x,y,.024f),Mesh=new SphereMesh{Radius=.008f,Height=.016f,RadialSegments=6,Rings=3},MaterialOverride=frame});
        RefreshLabels();
    }
    private Label3D Line(string name,Vector3 position,int fontSize)
    {
        var label=new Label3D{Name=name,Position=position,Font=ThemeDB.FallbackFont,FontSize=fontSize,PixelSize=.00135f,
            OutlineSize=0,Modulate=Color.FromHtml("ece5d0"),DoubleSided=false,NoDepthTest=false,
            Shaded=true,Billboard=BaseMaterial3D.BillboardModeEnum.Disabled};
        AddChild(label);return label;
    }
    public void RefreshLabels()
    {
        if(_registry is null || !_registry.TryResolve(AddressId,out var record) || _tatar is null || _russian is null || _number is null)return;
        var street=_registry.Streets[record.StreetId];
        _tatar.Text=street.Tatar;_russian.Text=street.Russian;_number.Text=record.HouseNumber;
        SetMeta("displayAddress",_registry.FormatAddress(AddressId));
    }
}

