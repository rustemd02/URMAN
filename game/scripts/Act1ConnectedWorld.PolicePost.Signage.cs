using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private static readonly Dictionary<string,StandardMaterial3D> PoliceSignMaterials=new();
    private static Dictionary<string,Rect2>? _policeSignImageRegions;

    private static Rect2 PoliceSignImageRegion(string file)
    {
        if(_policeSignImageRegions is null)
        {
            using var doc=System.Text.Json.JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(
                "res://content/world/act1_police_signs_imagegen.v1.json"));
            _policeSignImageRegions=new(StringComparer.Ordinal);
            foreach(var row in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var uv=row.GetProperty("uvRegion").EnumerateArray().Select(x=>x.GetSingle()).ToArray();
                _policeSignImageRegions.Add(row.GetProperty("file").GetString()!,new(uv[0],uv[1],uv[2],uv[3]));
            }
        }
        return _policeSignImageRegions[file];
    }

    private static StandardMaterial3D PolicePrintedMaterial(string file,bool paper=false,bool vinyl=false)
    {
        var key=file+":"+paper+":"+vinyl;
        if(PoliceSignMaterials.TryGetValue(key,out var saved))return saved;
        var source=ResourceLoader.Load<Texture2D>("res://assets/textures/police_signage_imagegen_v1/"+file)
            ??throw new InvalidOperationException("Missing police sign artwork: "+file);
        using var image=source.GetImage();
        if(image.IsCompressed())image.Decompress();
        image.GenerateMipmaps();
        var region=PoliceSignImageRegion(file);
        var material=new StandardMaterial3D
        {
            AlbedoTexture=ImageTexture.CreateFromImage(image), Roughness=paper ? .94f:.42f,
            MetallicSpecular=paper ? .08f:.32f,
            CullMode=paper ? BaseMaterial3D.CullModeEnum.Disabled:BaseMaterial3D.CullModeEnum.Back,
            TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            Uv1Scale=new(region.Size.X,region.Size.Y,1),
            Uv1Offset=new(region.Position.X,region.Position.Y,0),
            Transparency=vinyl?BaseMaterial3D.TransparencyEnum.Alpha:BaseMaterial3D.TransparencyEnum.Disabled,
            AlphaScissorThreshold=.35f
        };
        material.SetMeta("printAspect",image.GetWidth()*region.Size.X/(image.GetHeight()*region.Size.Y));
        material.SetMeta("artworkOrigin","built-in ImageGen; Cyrillic lettering is part of generated image");
        PoliceSignMaterials[key]=material;
        return material;
    }

    private static Node3D PoliceSignPlate(Node3D parent,string name,string file,Vector3 at,Vector2 size,
        float yaw=0,bool paper=false)
    {
        var artwork=PolicePrintedMaterial(file,paper);
        if(!paper)size.Y=size.X/(float)artwork.GetMeta("printAspect").AsDouble();
        var root=new Node3D {Name=name,Position=at,RotationDegrees=new(0,yaw,0)};
        parent.AddChild(root);
        var thickness=paper ? .0003f:.018f;
        RuralPropGeometry.Block(root,"Backing",new(size.X,size.Y,thickness),Vector3.Zero,
            RuralPropMaterials.Surface(paper?"plywood":"steel",paper?"d4cebf":"afb3ad"),paper ? .00003f:.004f);
        if(paper)
            CivicSurfaceLibrary.Paper(root,"PrintedFace",size-Vector2.One*.001f,
                new(0,0,thickness*.5f+.0005f),artwork,curl:.0015f);
        else
            root.AddChild(new MeshInstance3D {Name="PrintedFace",Position=new(0,0,thickness*.5f+.0008f),
                Mesh=new QuadMesh {Size=size-Vector2.One*.005f},MaterialOverride=artwork,
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
        var radius=paper ? .002f:Math.Min(.006f,size.Y*.025f);
        var inset=paper ? .012f:Math.Min(.025f,size.Y*.16f);
        foreach(var x in new[]{-1f,1f})foreach(var y in new[]{-1f,1f})
        {
            if(paper&&y<0)continue;
            var p=new Vector3(x*(size.X*.5f-inset),y*(size.Y*.5f-inset),thickness*.5f+.002f);
            root.AddChild(new MeshInstance3D {Name="Fastener"+x+"_"+y,Position=p,Rotation=new(Mathf.Pi*.5f,0,0),
                Mesh=new CylinderMesh {TopRadius=radius,BottomRadius=radius,Height=.002f,RadialSegments=12},
                MaterialOverride=RuralPropMaterials.Surface("zinc","b5b9b5"),
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
            if(!paper)root.AddChild(new MeshInstance3D {Name="ScrewSlot"+x+"_"+y,
                Position=p+Vector3.Back*.0016f,Rotation=new(0,0,.45f),
                Mesh=new BoxMesh {Size=new(radius*1.35f,.0008f,.0003f)},
                MaterialOverride=new StandardMaterial3D {AlbedoColor=new Color("454b4e"),Roughness=.8f},
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
        }
        root.SetMeta("signagePolicy","printed Cyrillic artwork on a fixed physical plate; bevelled edge and real fasteners; no floating text or billboard");
        return root;
    }

    private static void BuildPoliceSignage(Node3D room)
    {
        foreach(var label in room.FindChildren("*",nameof(Label3D),true,false).OfType<Label3D>())
            if(label.Name=="NameBoardText"||label.Name.ToString().StartsWith("Police",StringComparison.Ordinal))label.Visible=false;
        var oldBoard=room.GetNode<MeshInstance3D>("NameBoard");oldBoard.Visible=false;
        PoliceSignPlate(room,"PoliceMainEnamelSign","police_main.png",oldBoard.Position+Vector3.Up*.15f,new(3.2f,.72f));
        PoliceSignPlate(room,"PoliceDistrictEnamelPlaque","police_district.png",new(1.8f,2.07f,4.548f),new(.58f,.387f));
        PoliceSignPlate(room,"PoliceReceptionInformation","police_hours.png",new(1.8f,1.49f,4.548f),new(.58f,.483f));
        room.GetNode<MeshInstance3D>("PoliceReceptionPlaque").Visible=false;
        PoliceSignPlate(room,"PoliceReceptionEnamelSign","police_reception.png",new(-3.1f,2.47f,2.12f),new(1.35f,.2475f));
        foreach(var sign in new[]{-1f,1f})
            PoliceSignPlate(room,"PoliceStaffDoorPlate"+sign,sign<0?"police_staff.png":"police_store.png",
                new(sign*.883f,2.51f,-.58f),new(.38f,.10f),sign<0?90:-90);
        PoliceSignPlate(room,"PoliceHoldingRoomPlate","police_cell.png",new(0,2.70f,-2.101f),new(1.72f,.263f));
        PoliceSignPlate(room,"PoliceBlankFormsPlate","police_forms.png",new(4.7f,1.4f,1.081f),new(.56f,.124f));
        PoliceSignPlate(room,"PoliceFilesCabinetPlate","police_files.png",new(-4.9f,1.04f,.235f),new(.72f,.158f));
        foreach(var mesh in room.GetChildren().OfType<MeshInstance3D>())
            if(mesh.Name.ToString().StartsWith("PoliceNotice",StringComparison.Ordinal)&&mesh.Name!="PoliceNoticeBoard")mesh.Visible=false;
        var xs=new[]{3.10f,3.65f,4.12f};
        for(var i=0;i<xs.Length;i++)
            PoliceSignPlate(room,"PolicePrintedNotice"+i,"police_notice_"+(i+1)+".png",new(xs[i],1.84f,1.112f),new(.38f,.54f),paper:true);
        PoliceDeskPrint(room,"PoliceForms","police_form.png",new(.245f,.325f));
        PoliceDeskPrint(room,"PolicePaperStack","police_form.png",new(.215f,.295f));
        PoliceDeskPrint(room,"PoliceLedger","police_ledger.png",new(.294f,.362f));
        foreach(var label in room.FindChildren("DistrictDoorLettering*",nameof(Label3D),true,false).OfType<Label3D>())
        {
            label.Visible=false;
            var lettering=PolicePrintedMaterial("police_car.png",vinyl:true);
            var aspect=(float)lettering.GetMeta("printAspect").AsDouble();
            var size=aspect>1.61f/.19f?new Vector2(1.61f,1.61f/aspect):new Vector2(.19f*aspect,.19f);
            var decal=new MeshInstance3D {Name="PoliceDoorVinyl"+label.Name,
                Transform=label.Transform,Mesh=new QuadMesh {Size=size},
                MaterialOverride=lettering,
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
            label.GetParent().AddChild(decal);
            decal.Position+=decal.Basis.Z*.0008f;
        }
    }

    private static void PoliceDeskPrint(Node3D room,string target,string file,Vector2 size)
    {
        var surface=room.GetNode<MeshInstance3D>(target);
        var height=surface.Mesh!.GetAabb().Size.Y*.5f;
        room.AddChild(new MeshInstance3D {Name=target+"PrintedTop",Position=surface.Position+Vector3.Up*(height+.001f),
            Rotation=new(-Mathf.Pi*.5f,0,0),Mesh=new QuadMesh {Size=size},
            MaterialOverride=PolicePrintedMaterial(file,paper:true),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
    }
}
