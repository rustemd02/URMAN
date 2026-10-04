using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private Node3D? _policePost;

    private void BuildPolicePost()
    {
        _policePost = FindChild("FarBankPublic_police", true, false) as Node3D
            ?? throw new InvalidOperationException("The authored far-police building is missing.");
        var room = _policePost;
        foreach (var child in room.GetChildren().OfType<Node3D>())
            if (child.Name == "Walls" || child.Name == "Door" || child.Name == "PoliceStripe"
                || child.Name.ToString().StartsWith("Window", StringComparison.Ordinal)) child.Visible = false;
        room.GetNode<StaticBody3D>("FarBankPublicBody").GetChildren().OfType<CollisionShape3D>().First().Disabled = true;
        var board = room.GetNode<Label3D>("NameBoardText");
        board.Text = "УЧАСТКОВЫЙ"; board.PixelSize = .0033f;
        room.SetMeta("publicRole", "приёмная участкового");
        room.SetMeta("clearSizeMetres", new Vector2(11.7f, 8.7f));
        room.SetMeta("corridorClearWidthMetres", 1.8f);
        FacilitySolid(room, "PoliceFloor", new(11.8f, .06f, 8.8f), new(0, -.03f, 0), "a5a393", "floor_institution");
        FacilitySolid(room, "PoliceCeiling", new(12, .1f, 9), new(0, 3.05f, 0), "d9d7c6", "plaster");
        foreach (var x in new[] { -5.9f, 5.9f })
            FacilitySolid(room, "PoliceSideWall" + x, new(.2f, 3, 9), new(x, 1.5f, 0), "c5c9be", "plaster");
        FacilitySolid(room, "PoliceRearWall", new(11.8f, 3, .2f), new(0, 1.5f, -4.4f), "c5c9be", "plaster");
        // Front is a real doorway plus two glazed openings, never a solid facade box.
        foreach (var span in new[] { (-5.9f, -4.25f), (-2.95f, -.65f), (.65f, 2.95f), (4.25f, 5.9f) })
            FacilitySolid(room, "PoliceFrontPier" + span.Item1, new(span.Item2-span.Item1, 3, .2f),
                new((span.Item1+span.Item2)*.5f, 1.5f, 4.4f), "c5c9be", "plaster");
        FacilitySolid(room, "PoliceDoorLintel", new(1.3f, .75f, .2f), new(0, 2.625f, 4.4f), "c5c9be", "plaster");
        foreach (var x in new[] { -3.6f, 3.6f })
        {
            FacilitySolid(room, "PoliceWindowBelow" + x, new(1.3f, 1.1f, .2f), new(x, .55f, 4.4f), "b5beb3", "plaster");
            FacilitySolid(room, "PoliceWindowAbove" + x, new(1.3f, .6f, .2f), new(x, 2.7f, 4.4f), "c5c9be", "plaster");
            FacilityWindow(room, "PoliceFrontWindow" + x, new(x, 1.75f, 4.4f), new(1.24f, 1.24f));
            FacilityRadiator(room, "PoliceRadiator" + x, new(x, .58f, 4.20f), 0);
        }
        FacilityManualDoor(room, "PoliceEntrance", "police.entrance", new(-.59f, 0, 4.42f), 1.15f, 2.20f, 90, 80);
        GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld")
            .ExcludeOccupiedRoomTerrain(room, new(5.79f, 4.29f));
        SquareWeatherShelter(room, new(0, 1.5f, 0), new(5.8f, 1.5f, 4.3f));
        BuildPoliceRoof(room);
        BuildPoliceInterior(room);
        BuildPoliceSurroundings(room);
        ApplyPoliceSurfaceMaterials(room);
        foreach(var light in room.FindChildren("*",nameof(OmniLight3D),true,false).OfType<OmniLight3D>())
            light.ShadowEnabled=light.Name=="PoliceLobbyLight";
        EnsureFacilityTick();
    }

    private bool InPolicePost(Vector3 point)
    {
        if (_policePost is null) return false;
        var p = _policePost.ToLocal(point);
        return Math.Abs(p.X) < 5.8f && Math.Abs(p.Z) < 4.3f && p.Y > -.2f && p.Y < 3;
    }

    private void BuildPoliceSurroundings(Node3D room)
    {
        PolicePostAssets.Attach(room,"painted_wooden_bench","PolicePorchBench",new(-3.2f,0,4.85f));
        FacilitySolid(room,"PolicePorchBenchContact",new(1.3f,.5f,.45f),new(-3.2f,.25f,4.85f),"6d5c47","wood_furniture").Visible=false;
        FacilityLamp(room, "PolicePorchLight", new(0, 2.40f, 5.4f), "efd9b6", .6f, 4);
        FacilityLabel(room, "PoliceHours", "ПРИЁМ ГРАЖДАН\nЗаходите, если горит свет", new(1.8f, 1.55f, 4.515f), 0, .0013f);
        AddVisualBox(room, "PoliceMailbox", new(.38f, .45f, .14f), new(-1.4f, 1.3f, 4.55f), "4f6568", "metal");
        AddVisualBox(room, "PoliceMailboxSlot", new(.27f, .018f, .025f), new(-1.4f, 1.43f, 4.635f), "242c2d", "metal");
        // Side parking keeps the street and the entire staircase clear.
        var car = VehicleVisualFactory.BuildDistrictZhiguli();
        room.AddChild(car);
        var centre = room.ToGlobal(new(7.8f, 0, 1.6f));
        var ground = AgentBAct1HeightField.CollisionGround(centre.X, centre.Z);
        car.GlobalPosition = new(centre.X, ground + .035f, centre.Z);
        car.Rotation = Vector3.Zero;
        var body = new StaticBody3D { Name = "DistrictZhiguliContact", CollisionLayer = 2, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(1.66f, .79f, 4.16f) }, Position = new(0, .70f, 0) });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(1.4f, .45f, 1.85f) }, Position = new(0, 1.25f, .03f) });
        car.AddChild(body); _facilityBodies.Add(body);
        var inspect = FacilityTarget("DistrictZhiguliInspect", "presentation.police-car", "Осмотреть старые Жигули", car, new(0, .9f, 0), new(1.7f, 1.4f, 4.2f));
        inspect.PresentationRepeatAvailable = () => FacilityExteriorActive;
        inspect.PresentationRepeat = () => PoliceRemark("carRemark");
        var parking = new Node3D { Name = "PoliceParking" }; room.AddChild(parking);
        parking.GlobalPosition = car.GlobalPosition;
        AddVisualBox(parking, "ParkingHardstanding", new(2.6f, .03f, 5.2f), new(0, -.02f, 0), "afb6b1", "snow_trampled");
    }

    private void PoliceRemark(string key)
    {
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            player.ShowRemark("Осмотр", PolicePostPresentation.ContentText(key));
    }
}
