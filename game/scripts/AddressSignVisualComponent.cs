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
        AddChild(new MeshInstance3D{Name="FoldedMetalRim",Mesh=new BoxMesh{Size=new(1.18f,.43f,.032f)},MaterialOverride=frame});
        AddChild(new MeshInstance3D{Name="EnamelFace",Position=new(0,0,.019f),Mesh=new BoxMesh{Size=new(1.145f,.396f,.01f)},MaterialOverride=enamel});
        _tatar=Line("TatarStreet",new(-.18f,.077f,.026f),34);
        _russian=Line("RussianStreet",new(-.18f,-.069f,.026f),30);
        _number=Line("HouseNumber",new(.425f,0,.027f),65);
        foreach(var x in new[]{-.535f,.535f})foreach(var y in new[]{-.153f,.153f})
            AddChild(new MeshInstance3D{Name="Rivet",Position=new(x,y,.032f),Mesh=new SphereMesh{Radius=.012f,Height=.024f,RadialSegments=6,Rings=3},MaterialOverride=frame});
        RefreshLabels();
    }
    private Label3D Line(string name,Vector3 position,int fontSize)
    {
        var label=new Label3D{Name=name,Position=position,Font=ThemeDB.FallbackFont,FontSize=fontSize,PixelSize=.0026f,
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

