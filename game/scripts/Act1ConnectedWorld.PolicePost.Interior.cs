using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildPoliceInterior(Node3D room)
    {
        // Public lobby in front; enclosed offices flank a short clear corridor.
        foreach(var span in new[]{(-5.9f,-2.6f),(-1.6f,-1.0f),(1.0f,5.9f)})
            FacilitySolid(room,"PoliceOfficePartition"+span.Item1,new(span.Item2-span.Item1,3,.12f),
                new((span.Item1+span.Item2)*.5f,1.5f,1.0f),"bcc4b5","plaster");
        FacilitySolid(room,"PoliceDeskStaffHeader",new(1,.8f,.12f),new(-2.1f,2.6f,1.0f),"bcc4b5","plaster");
        foreach(var x in new[]{-.96f,.96f})
        {
            foreach(var span in new[]{(-2.16f,-1.06f),(-.10f,.94f)})
                FacilitySolid(room,"PoliceCorridorSide"+x+"_"+span.Item1,new(.12f,3,span.Item2-span.Item1),
                    new(x,1.5f,(span.Item1+span.Item2)*.5f),"b6bdae","plaster");
            FacilitySolid(room,"PoliceStaffDoorHeader"+x,new(.12f,.8f,.96f),new(x,2.6f,-.58f),"b6bdae","plaster");
            FacilityManualDoor(room,x<0?"PoliceOfficeDoor":"PoliceStoreDoor",x<0?"police.office":"police.store",
                new(x,0,-1.0f),.86f,2.17f,0,x<0?-90:90);
            FacilityLabel(room,"PoliceStaffRoomSign"+x,x<0?"СЛУЖЕБНОЕ":"КЛАДОВАЯ",new(x<0?-.883f:.883f,2.51f,-.58f),x<0?90:-90,.0009f);
        }
        FacilitySolid(room,"PoliceStoreCupboard",new(1.4f,1.85f,.55f),new(4.9f,.925f,-.6f),"848d80","wood_furniture");
        FacilityBench(room,"PoliceStaffRestBench",new(-3.9f,0,-1.66f),2.1f,0);
        PolicePostAssets.Attach(room,"metal_office_desk","PoliceStaffDesk",new(-3.7f,0,-3.0f),180);
        FacilitySolid(room,"PoliceStaffDeskContact",new(2.02f,.77f,.95f),new(-3.7f,.385f,-3.0f),"6d5d46","wood_furniture").Visible=false;
        PolicePostAssets.Attach(room,"office_chair","PoliceStaffChair",new(-3.7f,0,-3.83f));
        AddVisualBox(room,"PoliceStaffBlankFiles",new(.23f,.035f,.29f),new(-4.26f,.817f,-3.03f),"d4cab4","paper");
        foreach (var x in new[] { -1.59f, 1.59f })
            FacilitySolid(room, "PoliceCellSide" + x, new(.12f, 3, 2.2f), new(x, 1.5f, -3.26f), "bfc4b7", "plaster");
        // Service desk separates the officer from visitors, with a low open voice slot.
        PolicePostAssets.Attach(room,"metal_office_desk","PoliceServiceDesk",new(-3.1f,0,2.05f),180);
        FacilitySolid(room,"PoliceDeskContact",new(2.02f,.77f,.91f),new(-3.1f,.385f,2.05f),"6d5d46","wood_furniture").Visible=false;
        var glass = FacilitySolid(room, "PoliceReceptionGlass", new(3.45f, 1.21f, .018f), new(-3.1f, 1.685f, 2.10f), "9baead", "glass");
        glass.MaterialOverride = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new(.70f, .81f, .80f, .13f), Roughness = .12f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        foreach (var x in new[] { -4.86f, -1.34f })
            FacilitySolid(room, "PoliceGlassPost" + x, new(.055f, 1.52f, .075f), new(x, 1.54f, 2.10f), "68716b", "metal");
        FacilitySolid(room, "PoliceGlassHeader", new(3.55f, .055f, .075f), new(-3.1f, 2.315f, 2.10f), "68716b", "metal");
        FacilitySolid(room, "PoliceWritingLedge", new(3.55f, .045f, .24f), new(-3.1f, .82f, 2.46f), "997d59", "wood_furniture");
        AddVisualBox(room,"PoliceReceptionPlaque",new(1.35f,.23f,.035f),new(-3.1f,2.47f,2.10f),"40545d","wood_painted_trim");
        FacilityLabel(room, "PoliceReceptionSign", "ПРИЁМНАЯ", new(-3.1f, 2.47f, 2.125f), 0, .0025f);
        foreach(var x in new[]{2.90f,4.30f})
        {
            PolicePostAssets.Attach(room,"painted_wooden_bench","PoliceWaitingBench"+x,new(x,0,3.78f),180);
            FacilitySolid(room,"PoliceWaitingSeatContact"+x,new(1.25f,.48f,.45f),new(x,.24f,3.78f),"685847","wood_furniture").Visible=false;
        }
        FacilityTable(room, "PoliceWritingTable", new(4.75f, 0, 2.10f), new(1.1f, .76f, .60f));
        PolicePostAssets.Attach(room,"office_dining_chair","PoliceWritingChair",new(4.75f,0,2.93f),0,.88f);
        AddVisualBox(room, "PoliceForms", new(.25f, .014f, .33f), new(4.7f, .80f, 2.05f), "ded3b7", "paper");
        FacilityLabel(room, "PoliceFormsCaption", "Бланки заявлений", new(4.7f, 1.4f, 1.064f), 0, .0013f);
        AddVisualBox(room, "PoliceNoticeBoard", new(1.8f, 1.1f, .045f), new(3.6f, 1.82f, 1.076f), "796546", "wood_furniture");
        foreach (var x in new[] { 3.10f, 3.65f, 4.12f })
            AddVisualBox(room, "PoliceNotice" + x, new(.38f, .54f, .005f), new(x, 1.84f, 1.102f), "ded8bf", "paper");
        FacilityLabel(room, "PoliceNoticeText", "ПРИЁМ ГРАЖДАН\nОбращения и справки\nБерегите соседей", new(3.6f, 1.85f, 1.110f), 0, .0010f);
        // Papers, mug and wired telephone make the desk a workplace.
        AddVisualBox(room, "PoliceLedger", new(.30f, .025f, .37f), new(-3.95f, .813f, 1.95f), "bbaa87", "paper");
        AddVisualBox(room, "PolicePaperStack", new(.22f, .04f, .30f), new(-2.5f, .82f, 1.90f), "ded6bd", "paper");
        PolicePostAssets.Attach(room,"rotary_phone","PoliceTelephone",new(-3.77f,.80f,2.05f),-20);
        FacilityVessel(room, "PoliceTeaMug", new(-2.3f, .88f, 1.97f), .055f, .11f, "9ba99d", true);
        PolicePostAssets.Attach(room,"painted_wooden_cabinet","PoliceFilesCabinet",new(-4.9f,0,-.10f),0);
        FacilitySolid(room,"PoliceFilesContact",new(1.20f,1.18f,.62f),new(-4.9f,.59f,-.10f),"727c75","wood_furniture").Visible=false;
        FacilityLabel(room, "PoliceFilesLabel", "Обращения · Справки", new(-4.9f, 1.3f, .18f), 0, .0009f);
        PolicePostAssets.Attach(room,"desk_lamp_arm_01","PoliceDeskLamp",new(-3.97f,.79f,1.92f),65,.55f);
        for (var i=0; i<4; i++) FacilityRod(room, "PoliceCoatHook"+i, new(5.72f, 1.68f, 2.1f+i*.25f), new(5.6f, 1.75f, 2.1f+i*.25f), .018f, "6d716a");
        FacilityLamp(room, "PoliceLobbyLight", new(0, 2.78f, 2.9f), "f0e3c6", .85f, 7);
        FacilityLamp(room, "PoliceDeskLight", new(-3.1f, 2.7f, 1.4f), "efdfba", .6f, 4);
        FacilityLamp(room, "PoliceCorridorLight", new(0, 2.75f, -.7f), "e2dec9", .65f, 4);
        FacilityLamp(room, "PoliceCellLight", new(0, 2.70f, -3.3f), "d4dbca", .4f, 3);
        BuildPoliceCell(room);
        BuildPoliceOfficer(room);
    }

    private void BuildPoliceCell(Node3D room)
    {
        var bars = new Node3D { Name = "PoliceCellBars" }; room.AddChild(bars);
        foreach (var x in new[] { -1.10f, 1.10f })
        {
            FacilitySolid(bars, "BarPanelContact"+x, new(.83f, 2.35f, .055f), new(x, 1.175f, -2.18f), "6c736b", "metal").Visible=false;
            for(var i=0;i<7;i++) FacilityRod(bars,"Bar"+x+"_"+i,new(x-.36f+i*.12f,0,-2.18f),new(x-.36f+i*.12f,2.35f,-2.18f),.014f,"646d66");
        }
        foreach (var y in new[] { .10f, 1.15f, 2.35f })
        {
            if(y>2) FacilityRod(bars,"HeaderRail",new(-1.56f,y,-2.18f),new(1.56f,y,-2.18f),.020f,"69726a");
            else foreach(var sign in new[]{-1f,1f}) FacilityRod(bars,"PanelRail"+sign+"_"+y,new(sign*.67f,y,-2.18f),new(sign*1.56f,y,-2.18f),.020f,"69726a");
        }
        // The unused gate rests open against the cell wall, leaving a 1.36m opening.
        var gate = new Node3D { Name="PoliceCellOpenGate", Position=new(-.67f,0,-2.18f), RotationDegrees=new(0,-105,0) };bars.AddChild(gate);
        FacilitySolid(gate,"OpenGateContact",new(1.28f,2.3f,.05f),new(-.64f,1.175f,0),"707970","metal").Visible=false;
        for(var i=0;i<10;i++) FacilityRod(gate,"GateBar"+i,new(-i*.135f,.04f,0),new(-i*.135f,2.31f,0),.014f,"707970");
        foreach(var y in new[]{.05f,1.15f,2.32f}) FacilityRod(gate,"GateRail"+y,new(-1.28f,y,0),new(0,y,0),.02f,"707970");
        // Crossbar above the door is solid; middle/bottom span only the side panels.
        FacilitySolid(room,"PoliceCellDoorHeader",new(3.1f,.58f,.12f),new(0,2.68f,-2.18f),"b8beb0","plaster");
        FacilityBench(room,"PoliceCellBunk",new(.95f,0,-3.38f),1.62f,90);
        AddVisualBox(room,"PoliceFoldedBlanket",new(.31f,.09f,.40f),new(.93f,.54f,-3.80f),"888777","cloth");
        FacilityVessel(room,"PoliceCellEmptyBasin",new(-1.10f,.12f,-4.02f),.16f,.18f,"8b9688",false);
        FacilityLabel(room,"PoliceCellLabel","ИЗОЛЯТОР",new(0,2.70f,-2.108f),0,.0014f);
        var mouse = new Node3D { Name="PoliceCellMouse", Position=new(-1.0f,.012f,-3.735f), Scale=Vector3.One*.65f };room.AddChild(mouse);
        PoliceEllipsoid(mouse,"MouseBody",new(.047f,.032f,.075f),new(0,.022f,0),"726c60","cloth");
        PoliceEllipsoid(mouse,"MouseHead",new(.029f,.025f,.035f),new(0,.032f,.067f),"797367","cloth");
        foreach(var sign in new[]{-1f,1f})
        {
            PoliceEllipsoid(mouse,"MouseEar"+sign,new(.019f,.018f,.008f),new(sign*.023f,.055f,.071f),"a39383","cloth");
            PoliceEllipsoid(mouse,"MouseEye"+sign,new(.003f,.003f,.003f),new(sign*.022f,.038f,.088f),"252925","metal");
        }
        foreach(var x in new[]{-.027f,.027f})foreach(var z in new[]{-.036f,.036f})
            PoliceEllipsoid(mouse,"MouseFoot"+x+"_"+z,new(.011f,.004f,.016f),new(x,-.01f,z),"9a8d7b","cloth");
        FacilityRod(mouse,"MouseTail",new(0,.014f,-.065f),new(.095f,.008f,-.15f),.003f,"958879");
        PoliceEllipsoid(room,"PoliceCellPotato",new(.045f,.032f,.06f),new(-1.0f,.032f,-3.61f),"9f8963","wood");
        for(var i=0;i<5;i++) PoliceEllipsoid(room,"PotatoEye"+i,new(.003f,.002f,.003f),new(-1.015f+i*.006f,.059f,-3.61f+(i%2)*.02f),"66543c","wood");
        var target=FacilityTarget("PoliceCellInspect","presentation.police-cell","Осмотреть пустой изолятор",room,new(0,1.3f,-2.12f),new(1.6f,1.8f,.08f));
        target.PresentationRepeatAvailable=()=>FacilityExteriorActive;
        target.PresentationRepeat=()=>PoliceRemark("cellRemark");
    }

    private static MeshInstance3D PoliceEllipsoid(Node3D parent,string name,Vector3 radius,Vector3 at,string color,string surface)
    {
        var mesh=new MeshInstance3D { Name=name, Position=at, Scale=radius,
            Mesh=new SphereMesh { Radius=1,Height=2,RadialSegments=12,Rings=6 },
            MaterialOverride=PainterlyMaterialLibrary.ForColor(color,surface,sheltered:true) };
        parent.AddChild(mesh);return mesh;
    }
}
