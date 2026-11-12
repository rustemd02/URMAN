using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Interior rebuilt from an empty room (author request 12.11.2026). One plan,
    // read from the door: the lobby in front, the reception counter on the left,
    // and one straight partition across the back with three things in it — the
    // office door, the barred holding room in the middle, the store door.
    private const float PolicePartitionZ = .4f;
    private const float PoliceCellHalf = 1.7f;

    private void BuildPoliceInterior(Node3D room)
    {
        const string wall = "c3c8bc";
        const float z = PolicePartitionZ;
        // The partition: solid runs between the two doorways and the cell front.
        foreach (var (from, to) in new[] { (-5.8f, -5.6f), (-4.7f, -PoliceCellHalf), (PoliceCellHalf, 3.2f), (4.1f, 5.8f) })
            FacilitySolid(room, "PolicePartition" + from, new(to - from, 3f, .12f), new((from + to) * .5f, 1.5f, z), wall, "plaster");
        foreach (var x in new[] { -5.15f, 3.65f })
            FacilitySolid(room, "PoliceDoorHeader" + x, new(.9f, .8f, .12f), new(x, 2.6f, z), wall, "plaster");
        foreach (var x in new[] { -PoliceCellHalf, PoliceCellHalf })
            FacilitySolid(room, "PoliceCellSide" + x, new(.12f, 3f, 4.64f), new(x, 1.5f, -1.98f), wall, "plaster");
        FacilityManualDoor(room, "PoliceOfficeDoor", "police.office", new(-5.6f, 0, z), .86f, 2.17f, 90, -88);
        FacilityManualDoor(room, "PoliceStoreDoor", "police.store", new(3.2f, 0, z), .86f, 2.17f, 90, -88);

        // Reception: the officer's desk behind a glazed counter, closed on the lobby side by a low return.
        PolicePostAssets.Attach(room, "metal_office_desk", "PoliceServiceDesk", new(-3.1f, 0, 2.05f), 180);
        FacilitySolid(room, "PoliceDeskContact", new(2.02f, .77f, .91f), new(-3.1f, .385f, 2.05f), "6d5d46", "wood_furniture").Visible = false;
        FacilitySolid(room, "PoliceCounterWest", new(.95f, 1.05f, .5f), new(-5.3f, .525f, 2.1f), "8a7558", "wood_furniture");
        FacilitySolid(room, "PoliceCounterReturn", new(.14f, 1.05f, 1.62f), new(-1.25f, .525f, 1.27f), "8a7558", "wood_furniture");
        FacilitySolid(room, "PoliceCounterReturnTop", new(.26f, .04f, 1.7f), new(-1.25f, 1.07f, 1.27f), "997d59", "wood_furniture");
        var glass = FacilitySolid(room, "PoliceReceptionGlass", new(3.45f, 1.21f, .018f), new(-3.1f, 1.685f, 2.10f), "9baead", "glass");
        glass.MaterialOverride = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new(.70f, .81f, .80f, .13f), Roughness = .12f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        foreach (var x in new[] { -4.86f, -1.34f })
            FacilitySolid(room, "PoliceGlassPost" + x, new(.055f, 1.52f, .075f), new(x, 1.54f, 2.10f), "68716b", "metal");
        FacilitySolid(room, "PoliceGlassHeader", new(3.55f, .055f, .075f), new(-3.1f, 2.315f, 2.10f), "68716b", "metal");
        FacilitySolid(room, "PoliceWritingLedge", new(3.55f, .045f, .24f), new(-3.1f, .82f, 2.46f), "997d59", "wood_furniture");
        AddVisualBox(room, "PoliceReceptionPlaque", new(1.35f, .23f, .035f), new(-3.1f, 2.47f, 2.10f), "40545d", "wood_painted_trim");
        AddVisualBox(room, "PoliceLedger", new(.30f, .025f, .37f), new(-3.95f, .813f, 1.95f), "bbaa87", "paper");
        AddVisualBox(room, "PolicePaperStack", new(.22f, .04f, .30f), new(-2.5f, .82f, 1.90f), "ded6bd", "paper");
        PolicePostAssets.Attach(room, "rotary_phone", "PoliceTelephone", new(-3.77f, .80f, 2.05f), -20);
        FacilityVessel(room, "PoliceTeaMug", new(-2.3f, .88f, 1.97f), .055f, .11f, "9ba99d", true);
        PolicePostAssets.Attach(room, "desk_lamp_arm_01", "PoliceDeskLamp", new(-3.97f, .79f, 1.92f), 65, .55f);

        // Lobby: two benches under the front windows, a writing table and the notices on the partition.
        foreach (var x in new[] { 2.90f, 4.30f })
        {
            PolicePostAssets.Attach(room, "painted_wooden_bench", "PoliceWaitingBench" + x, new(x, 0, 3.78f), 180);
            FacilitySolid(room, "PoliceWaitingSeatContact" + x, new(1.25f, .48f, .45f), new(x, .24f, 3.78f), "685847", "wood_furniture").Visible = false;
        }
        FacilityTable(room, "PoliceWritingTable", new(4.95f, 0, .92f), new(1.1f, .76f, .60f));
        PolicePostAssets.Attach(room, "office_dining_chair", "PoliceWritingChair", new(4.95f, 0, 1.72f), 0, .88f);
        AddVisualBox(room, "PoliceForms", new(.25f, .014f, .33f), new(4.9f, .80f, .9f), "ded3b7", "paper");
        AddVisualBox(room, "PoliceNoticeBoard", new(1.36f, 1.1f, .04f), new(2.47f, 1.82f, z + .08f), "796546", "wood_furniture");
        for (var i = 0; i < 4; i++)
            FacilityRod(room, "PoliceCoatHook" + i, new(5.72f, 1.68f, 2.1f + i * .25f), new(5.6f, 1.75f, 2.1f + i * .25f), .018f, "6d716a");

        // Office, behind the left door.
        PolicePostAssets.Attach(room, "metal_office_desk", "PoliceStaffDesk", new(-3.9f, 0, -3.25f), 180);
        FacilitySolid(room, "PoliceStaffDeskContact", new(2.02f, .77f, .95f), new(-3.9f, .385f, -3.25f), "6d5d46", "wood_furniture").Visible = false;
        PolicePostAssets.Attach(room, "office_chair", "PoliceStaffChair", new(-3.9f, 0, -3.95f));
        AddVisualBox(room, "PoliceStaffBlankFiles", new(.23f, .035f, .29f), new(-4.46f, .817f, -3.28f), "d4cab4", "paper");
        PolicePostAssets.Attach(room, "painted_wooden_cabinet", "PoliceFilesCabinet", new(-5.3f, 0, -1.3f), 90);
        FacilitySolid(room, "PoliceFilesContact", new(.62f, 1.18f, 1.20f), new(-5.3f, .59f, -1.3f), "727c75", "wood_furniture").Visible = false;
        FacilityBench(room, "PoliceStaffRestBench", new(-2.1f, 0, -1.6f), 2.1f, 90);

        // Store, behind the right door: a cupboard and open shelves with boxes.
        FacilitySolid(room, "PoliceStoreCupboard", new(1.4f, 1.85f, .55f), new(4.9f, .925f, -3.95f), "848d80", "wood_furniture");
        foreach (var end in new[] { -3.2f, -.6f })
            FacilitySolid(room, "PoliceStoreUpright" + end, new(.45f, 1.9f, .05f), new(5.52f, .95f, end), "74604a", "wood_furniture");
        for (var level = 0; level < 4; level++)
        {
            FacilitySolid(room, "PoliceStoreShelf" + level, new(.45f, .04f, 2.6f), new(5.52f, .3f + level * .5f, -1.9f), "74604a", "wood_furniture");
            for (var box = 0; box < 3; box++)
                AddVisualBox(room, $"PoliceStoreBox{level}_{box}", new(.34f, .26f + (level + box) % 2 * .08f, .5f),
                    new(5.5f, .45f + level * .5f + (level + box) % 2 * .04f, -2.7f + box * .8f + level % 2 * .12f), box % 2 == 0 ? "a08f6c" : "8d8a7c", "paper");
        }

        FacilityLamp(room, "PoliceLobbyLight", new(.6f, 2.78f, 2.5f), "f0e3c6", .9f, 7.5f);
        FacilityLamp(room, "PoliceDeskLight", new(-3.1f, 2.7f, 1.4f), "efdfba", .6f, 4);
        FacilityLamp(room, "PoliceOfficeLight", new(-3.7f, 2.75f, -2.0f), "e8dcc0", .6f, 5);
        FacilityLamp(room, "PoliceStoreLight", new(3.7f, 2.75f, -2.0f), "e2dec9", .5f, 5);
        FacilityLamp(room, "PoliceCellLight", new(0, 2.70f, -2.0f), "d4dbca", .4f, 4);
        BuildPoliceCell(room);
        BuildPoliceOfficer(room);
    }

    private void BuildPoliceCell(Node3D room)
    {
        const float z = PolicePartitionZ;
        var bars = new Node3D { Name = "PoliceCellBars" }; room.AddChild(bars);
        // Two fixed barred wings and a gate that stands open against the inside wall.
        foreach (var side in new[] { -1f, 1f })
        {
            var outer = side * (PoliceCellHalf - .06f);
            var jamb = side * .67f;
            FacilitySolid(bars, "BarPanelContact" + side, new(Mathf.Abs(outer - jamb), 2.35f, .055f), new((outer + jamb) * .5f, 1.175f, z), "6c736b", "metal").Visible = false;
            PoliceCellRod(bars, "OuterFramePost" + side, new(outer, .08f, z), new(outer, 2.35f, z), .032f, "778078");
            PoliceCellRod(bars, "GateJambPost" + side, new(jamb, .08f, z), new(jamb, 2.35f, z), .032f, "778078");
            for (var i = 1; i < 7; i++)
            {
                var x = Mathf.Lerp(jamb, outer, i / 7f);
                PoliceCellRod(bars, "Bar" + side + "_" + i, new(x, .10f, z), new(x, 2.35f, z), .013f, "68746d");
            }
            foreach (var y in new[] { .10f, 1.16f, 2.35f })
                PoliceCellRod(bars, "PanelRail" + side + "_" + y, new(outer, y, z), new(jamb, y, z), .021f, "79837b");
        }
        var gate = new Node3D { Name = "PoliceCellOpenGate", Position = new(-.67f, 0, z), RotationDegrees = new(0, -100, 0) }; bars.AddChild(gate);
        FacilitySolid(gate, "OpenGateContact", new(1.28f, 2.3f, .05f), new(-.64f, 1.175f, 0), "707970", "metal").Visible = false;
        PoliceCellRod(gate, "GateOuterPost", new(-1.28f, .08f, 0), new(-1.28f, 2.35f, 0), .032f, "778078");
        PoliceCellRod(gate, "GateHingePost", new(0, .08f, 0), new(0, 2.35f, 0), .032f, "778078");
        for (var i = 1; i < 10; i++) PoliceCellRod(gate, "GateBar" + i, new(-i * .128f, .10f, 0), new(-i * .128f, 2.35f, 0), .013f, "68746d");
        foreach (var y in new[] { .10f, 1.16f, 2.35f }) PoliceCellRod(gate, "GateRail" + y, new(-1.28f, y, 0), new(0, y, 0), .021f, "79837b");
        FacilitySolid(room, "PoliceCellDoorHeader", new(PoliceCellHalf * 2f, .58f, .12f), new(0, 2.71f, z), "b8beb0", "plaster");
        FacilityBench(room, "PoliceCellBunk", new(1.25f, 0, -2.7f), 1.9f, 90);
        AddVisualBox(room, "PoliceFoldedBlanket", new(.31f, .09f, .40f), new(1.25f, .54f, -3.35f), "888777", "cloth");
        FacilityVessel(room, "PoliceCellEmptyBasin", new(-1.25f, .12f, -3.95f), .16f, .18f, "8b9688", false);
        var mouse = new Node3D { Name = "PoliceCellMouse", Position = new(-1.2f, .012f, -3.4f), Scale = Vector3.One * .65f }; room.AddChild(mouse);
        PoliceEllipsoid(mouse, "MouseBody", new(.047f, .032f, .075f), new(0, .022f, 0), "726c60", "cloth");
        PoliceEllipsoid(mouse, "MouseHead", new(.029f, .025f, .035f), new(0, .032f, .067f), "797367", "cloth");
        foreach (var sign in new[] { -1f, 1f })
        {
            PoliceEllipsoid(mouse, "MouseEar" + sign, new(.019f, .018f, .008f), new(sign * .023f, .055f, .071f), "a39383", "cloth");
            PoliceEllipsoid(mouse, "MouseEye" + sign, new(.003f, .003f, .003f), new(sign * .022f, .038f, .088f), "252925", "metal");
        }
        FacilityRod(mouse, "MouseTail", new(0, .014f, -.065f), new(.095f, .008f, -.15f), .003f, "958879");
        PoliceEllipsoid(room, "PoliceCellPotato", new(.045f, .032f, .06f), new(-1.2f, .032f, -3.27f), "9f8963", "wood");
        var target = FacilityTarget("PoliceCellInspect", "presentation.police-cell", "Осмотреть пустой изолятор", room, new(0, 1.3f, z + .06f), new(1.6f, 1.8f, .08f));
        target.PresentationRepeatAvailable = () => FacilityExteriorActive;
        target.PresentationRepeat = () => PoliceRemark("cellRemark");
    }

    private static void PoliceCellRod(Node3D parent, string name, Vector3 a, Vector3 b, float radius, string tint)
    {
        var delta = b - a;
        var mesh = DiscoveryCylinder(parent, name, radius, radius, delta.Length(), (a + b) * .5f, tint);
        mesh.Quaternion = new Quaternion(Vector3.Up, delta.Normalized());
        mesh.MaterialOverride = RuralPropMaterials.Surface("steel", tint);
    }

    private static MeshInstance3D PoliceEllipsoid(Node3D parent,string name,Vector3 radius,Vector3 at,string color,string surface)
    {
        var mesh=new MeshInstance3D { Name=name, Position=at, Scale=radius,
            Mesh=new SphereMesh { Radius=1,Height=2,RadialSegments=12,Rings=6 },
            MaterialOverride=PainterlyMaterialLibrary.ForColor(color,surface,sheltered:true) };
        parent.AddChild(mesh);return mesh;
    }
}
