using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private Node3D? _arrivalBenchSnow;
    private Node3D? _arrivalBenchNotches;
    private Node3D? _arrivalWellMitten;
    private Node3D? _mainStreetSignBoard;
    private Node3D? _mainStreetSignReverseSide;
    private bool? _arrivalBenchFound;
    private bool? _arrivalWellFound;
    private bool? _mainStreetSignFound;
    private Tween? _arrivalWellMittenTurn;
    private Tween? _mainStreetSignTurn;
    private StaticBody3D? _arrivalDiscoveryCollision;

    private void BuildAct1ExteriorDiscoveries()
    {
        var village = (StyleBenchmarkZone)_zoneInstances["village_day"];
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var arrival = core.GetNode<Node3D>("Arrival");
        var mainStreet = core.GetNode<Node3D>("MainStreet");
        var authoredWell = core.GetNodeOrNull<Node3D>(
            "Act1AuthoredExteriorKitPresentation/MainStreetArrivalAuthoredWell")
            ?? throw new InvalidOperationException(
                "Exterior discovery well requires the active MainStreetArrivalAuthoredWell presentation.");

        var benchGround = (float)AgentBAct1HeightField.CollisionGround(4.9f, 6.1f);
        var wellGround = (float)AgentBAct1HeightField.CollisionGround(-4.9f, 4.6f);
        var signGround = (float)AgentBAct1HeightField.CollisionGround(-2.05f, 5.8f);
        BuildArrivalBenchDiscovery(arrival, benchGround);
        BuildArrivalWellDiscovery(mainStreet, authoredWell, wellGround);
        BuildMainStreetSignDiscovery(mainStreet, signGround);
        _arrivalDiscoveryCollision = new StaticBody3D { Name = "ArrivalDiscoveryCollision" };
        village.AddChild(_arrivalDiscoveryCollision);
        foreach (var (name, size, at) in new (string, Vector3, Vector3)[]
        {
            ("BenchSeat", new(1.8f, .53f, .42f), new(4.9f, benchGround + .265f, 6.1f)),
            ("BenchBack", new(1.8f, .50f, .12f), new(4.9f, benchGround + .75f, 6.27f)),
            ("SignPost", new(.11f, 1.80f, .11f), new(-2.05f, signGround + .90f, 5.8f))
        })
            _arrivalDiscoveryCollision.AddChild(new CollisionShape3D
            { Name = name, Position = village.ToLocal(at), Shape = new BoxShape3D { Size = size } });

        // The targets remain owned by the existing logical village zone. The
        // visible counterparts live in Act1CoreWorldGreybox because the legacy
        // StyleBenchmarkZone presentation is intentionally suppressed.
        DiscoveryTarget(village, "arrival-bench-race-notches", new(2.0f, 1.2f, .85f),
            new(4.9f, benchGround + 1.03f, 6.24f), true);
        DiscoveryTarget(village, "arrival-insulated-well", new(1.6f, 1.7f, 1.2f),
            new(-4.2f, wellGround + 1.35f, 4.6f), true);
        DiscoveryTarget(village, "main-street-sign-reverse", new(1.35f, .95f, .80f),
            new(-2.05f, signGround + 1.68f, 5.8f), true);

        // M1: the personal Marat detail is the player's own action, not an
        // automatic grant. The photograph he carries stands on the arrival
        // bench, leaning against the backrest so its silhouette reads from the
        // street, and opens the existing arrival-photo source whose authored
        // openEffects confirm the memory keys.
        var arrivalPhoto = new Node3D
        {
            Name = "ArrivalPhotoInRoad",
            Position = new(5.35f, benchGround + 0.80f, 6.16f),
            RotationDegrees = new(-16f, 8f, 0f)
        };
        village.AddChild(arrivalPhoto);
        AddVisualBox(arrivalPhoto, "PhotoFrameEdge", new(.235f, .175f, .012f), Vector3.Zero, "6f5a42");
        AddVisualBox(arrivalPhoto, "PhotoPrint", new(.205f, .145f, .010f), new(0f, 0f, .004f), "d8cdb2");
        AddVisualBox(arrivalPhoto, "PhotoSkyBand", new(.190f, .070f, .008f), new(0f, .032f, .008f), "9aa8ae");
        AddVisualBox(arrivalPhoto, "PhotoFigureA", new(.040f, .062f, .008f), new(-.042f, -.026f, .008f), "5f5a4e");
        AddVisualBox(arrivalPhoto, "PhotoFigureB", new(.040f, .070f, .008f), new(.036f, -.022f, .008f), "7a6a52");
        AddVisualBox(arrivalPhoto, "PhotoInscription", new(.085f, .014f, .007f), new(.028f, -.052f, .008f), "6a6154");
        AddVisualBox(arrivalPhoto, "PhotoProp", new(.070f, .080f, .070f), new(-.062f, -.052f, 0f), "7d6a4f");
        village.MakeInteractionBox("ArrivalPhotoTarget", new(.60f, .60f, .50f),
            new(5.35f, benchGround + .88f, 6.16f), "665b49", "",
            "Посмотреть фотографию",
            documentId: "urman.chapter1:document/arrival-photo-evidence",
            rayOnly: true);
    }

    private void BuildArrivalBenchDiscovery(Node3D parent, float groundY)
    {
        var bench = new Node3D
        {
            Name = "DiscoveryArrivalBench",
            Position = new(4.9f, groundY, 6.1f)
        };
        bench.SetMeta("visualOnly", true);
        bench.SetMeta("presentationRole", "arrival bench with snow-covered childhood race notches");
        parent.AddChild(bench);

        // Seat surface at .46m and back crest at 1.0m keep this as a
        // grounded human bench instead of a chest-height signboard.
        AddVisualBox(bench, "Seat", new(1.8f, .14f, .42f), new(0f, .46f, 0f), "6f543c", "wood_furniture");
        AddVisualBox(bench, "Back", new(1.8f, .50f, .12f), new(0f, .75f, .17f), "604a37", "wood_furniture");
        AddVisualBox(bench, "LegLeft", new(.12f, .40f, .12f), new(-.68f, .20f, 0f), "594332", "wood_furniture");
        AddVisualBox(bench, "LegRight", new(.12f, .40f, .12f), new(.68f, .20f, 0f), "594332", "wood_furniture");

        _arrivalBenchSnow = new Node3D { Name = "SnowCap" };
        bench.AddChild(_arrivalBenchSnow);
        AddVisualBox(_arrivalBenchSnow, "SeatSnow", new(1.92f, .07f, .46f),
            new(0f, .57f, .02f), "eef2f6", "snow_ground");
        AddVisualBox(_arrivalBenchSnow, "BackSnow", new(1.92f, .06f, .16f),
            new(0f, 1.03f, .16f), "eef2f6", "snow_ground");

        _arrivalBenchNotches = new Node3D { Name = "RaceNotches" };
        bench.AddChild(_arrivalBenchNotches);
        for (var index = 0; index < 4; index++)
        {
            // A shallow dark inset reads as a knife mark on the back rail;
            // wood_furniture has zero blanket coverage, so snow stays only
            // on the authored cap until the action reveals these marks.
            AddVisualBox(_arrivalBenchNotches, $"RaceNotch{index}", new(.012f, .11f, .002f),
                new(-.56f + index * .37f, .76f, .231f), "4b362a", "wood_furniture",
                rollDegrees: index % 2 == 0 ? -4f : 3f);
        }
    }

    private void BuildArrivalWellDiscovery(Node3D parent, Node3D authoredWell, float groundY)
    {
        var anchor = new Vector3(-4.9f, groundY, 4.6f);
        var detail = new Node3D { Name = "DiscoveryArrivalWellDetail", Position = anchor };
        detail.SetMeta("visualOnly", true);
        detail.SetMeta("anchorPresentationProp", authoredWell.GetPath().ToString());
        detail.SetMeta("presentationRole", "old mitten and polished hook on active arrival well");
        parent.AddChild(detail);

        AddVisualBox(detail, "PolishedHook", new(.035f, .16f, .035f),
            new(.78f, 1.43f, -.01f), "3f4441", "metal", rollDegrees: -12f);
        AddVisualBox(detail, "MittenTie", new(.035f, .12f, .035f),
            new(.72f, 1.33f, -.01f), "8a7a5e", "fabric", rollDegrees: 8f);
        _arrivalWellMitten = new Node3D
        {
            Name = "TiedMitten",
            Position = new(.69f, 1.28f, -.01f)
        };
        detail.AddChild(_arrivalWellMitten);
        AddVisualBox(_arrivalWellMitten, "Cuff", new(.14f, .08f, .11f),
            Vector3.Zero, "725348", "fabric");
        var feltHand = new MeshInstance3D
        {
            Name = "FeltHand",
            Position = new(0f, .15f, 0f),
            Mesh = new SphereMesh { Radius = .10f, Height = .24f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("98614f", "fabric")
        };
        feltHand.RotationDegrees = new(0f, 0f, -7f);
        feltHand.SetMeta("visualOnly", true);
        _arrivalWellMitten.AddChild(feltHand);
        var thumb = new MeshInstance3D
        {
            Name = "Thumb",
            Position = new(.075f, .14f, -.02f),
            Mesh = new SphereMesh { Radius = .045f, Height = .13f, RadialSegments = 10, Rings = 5 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("98614f", "fabric")
        };
        thumb.RotationDegrees = new(0f, -12f, 12f);
        thumb.SetMeta("visualOnly", true);
        _arrivalWellMitten.AddChild(thumb);
    }

    private void BuildMainStreetSignDiscovery(Node3D parent, float groundY)
    {
        var sign = new Node3D
        {
            Name = "DiscoveryMainStreetSign",
            Position = new(-2.05f, groundY, 5.8f)
        };
        sign.SetMeta("visualOnly", true);
        sign.SetMeta("presentationRole", "reused wayfinding board with readable reverse word");
        parent.AddChild(sign);

        AddVisualBox(sign, "Post", new(.11f, 1.80f, .11f), new(0f, .90f, 0f), "594939", "wood");
        _mainStreetSignBoard = new Node3D
        {
            Name = "Board",
            // Move the board face clear of the centered post so both sides
            // remain readable before and after the 180-degree turn.
            Position = new(0f, 1.68f, .16f),
            RotationDegrees = new(0f, -4f, 0f)
        };
        sign.AddChild(_mainStreetSignBoard);
        AddVisualBox(_mainStreetSignBoard, "BoardFace", new(1.15f, .28f, .08f),
            Vector3.Zero, "806b4f", "wood");
        var frontArrow = DiscoveryLabel(_mainStreetSignBoard, "FrontArrow", "→", new(.36f, 0f, .058f), .0015f);
        frontArrow.Modulate = new Color("c8b794");
        var frontText = DiscoveryLabel(_mainStreetSignBoard, "FrontText", "ФАП",
            new(-.23f, 0f, .058f), .0015f);
        frontText.Modulate = new Color("c8b794");

        _mainStreetSignReverseSide = new Node3D { Name = "ReverseSide", Visible = false };
        _mainStreetSignBoard.AddChild(_mainStreetSignReverseSide);
        AddVisualBox(_mainStreetSignReverseSide, "PeelingPaintStrip", new(.74f, .045f, .012f),
            new(0f, .10f, -.047f), "d1c3a0", "wood_furniture");
        var reverseArrow = DiscoveryLabel(_mainStreetSignReverseSide, "ReverseArrow", "←",
            new(-.38f, 0f, -.058f), .0015f);
        reverseArrow.RotationDegrees = new(0f, 180f, 0f);
        reverseArrow.Modulate = new Color("e2d5b8");
        var reverseText = DiscoveryLabel(_mainStreetSignReverseSide, "ReverseText", "юл",
            new(-.08f, 0f, -.058f), .0015f);
        reverseText.RotationDegrees = new(0f, 180f, 0f);
        reverseText.Modulate = new Color("e2d5b8");
    }

    private void UpdateAct1ExteriorDiscoveries()
    {
        if (_runtimeBridge?.ActiveSceneId is null) return;
        if (_arrivalDiscoveryCollision is not null)
            _arrivalDiscoveryCollision.CollisionLayer = ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night" ? 1u : 0u;
        var state = _runtimeBridge.SelectRuntimeState();
        var knowledge = state.GetProperty("knowledge");
        bool Found(string slug) => knowledge.TryGetProperty(DiscoveryPrefix + slug, out var entry)
            && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";

        var benchFound = Found("arrival-bench-race-notches");
        if (_arrivalBenchSnow is not null && _arrivalBenchNotches is not null && _arrivalBenchFound != benchFound)
        {
            _arrivalBenchSnow.Visible = !benchFound;
            _arrivalBenchNotches.Visible = benchFound;
            _arrivalBenchFound = benchFound;
        }

        var wellFound = Found("arrival-insulated-well");
        if (_arrivalWellMitten is not null && _arrivalWellFound != wellFound)
        {
            _arrivalWellMittenTurn?.Kill();
            var targetYaw = wellFound ? Mathf.Pi : 0f;
            if (_arrivalWellFound == false && wellFound && ActiveZoneId == "village_day")
            {
                _arrivalWellMittenTurn = CreateTween();
                _arrivalWellMittenTurn.TweenProperty(_arrivalWellMitten, "rotation:y", targetYaw, .55f);
            }
            else
            {
                _arrivalWellMitten.Rotation = new(0f, targetYaw, 0f);
            }
            _arrivalWellFound = wellFound;
        }

        if (_mainStreetSignBoard is not null && _mainStreetSignReverseSide is not null
            && _mainStreetSignFound != Found("main-street-sign-reverse"))
        {
            var signFound = Found("main-street-sign-reverse");
            _mainStreetSignTurn?.Kill();
            var targetYaw = Mathf.DegToRad(signFound ? 176f : -4f);
            _mainStreetSignReverseSide.Visible = signFound;
            if (_mainStreetSignFound == false && signFound && ActiveZoneId == "village_day")
            {
                _mainStreetSignTurn = CreateTween();
                _mainStreetSignTurn.TweenProperty(_mainStreetSignBoard, "rotation:y", targetYaw, .60f);
            }
            else
            {
                _mainStreetSignBoard.Rotation = new(0f, targetYaw, 0f);
            }
            _mainStreetSignFound = signFound;
        }
    }
}
