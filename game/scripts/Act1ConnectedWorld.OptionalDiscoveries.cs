using System;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string MainStreetSideWindowSlug = "main-street-side-window";
    private const string ConnectiveStreetRepairBenchSlug = "connective-street-repair-bench";

    private Node3D? _mainStreetSideWindowShutter;
    private Node3D? _mainStreetSideWindowReveal;
    private Node3D? _mainStreetSideWindowHousehold;
    private Node3D? _mainStreetSideWindowFog;
    private StaticBody3D? _connectiveRepairBenchCollision;
    private Node3D? _connectiveRepairLooseWrap;
    private Node3D? _connectiveRepairFinishedWrap;
    private bool? _mainStreetSideWindowFound;
    private bool? _connectiveStreetRepairBenchFound;
    private Tween? _mainStreetSideWindowTurn;
    private Tween? _connectiveRepairWrapReveal;

    private void BuildAct1OptionalDiscoveries()
    {
        var village = _zoneInstances["village_day"] as StyleBenchmarkZone
            ?? throw new InvalidOperationException(
                "Connected Act I layout is missing the village_day interaction zone.");
        var core = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox")
            ?? throw new InvalidOperationException(
                "Act I core world is missing its presentation root.");

        BuildMainStreetSideWindowDiscovery(village, core);
        BuildConnectiveStreetRepairBenchDiscovery(village, core);
    }

    private void BuildMainStreetSideWindowDiscovery(StyleBenchmarkZone village, Node3D core)
    {
        var facade = core.GetNodeOrNull<Node3D>(
                "Act1AuthoredExteriorKitPresentation/NeighborParcels/MainStreet/EastParcel/MainStreetEastNeighborFacade")
            ?? throw new InvalidOperationException(
                "MainStreetEastNeighborFacade is required for the side-window discovery.");
        // Use the imported street window and sill as the placement contract. The
        // facade is scaled/rotated by its parcel owner, so no world-space Y or
        // guessed facade offset belongs here.
        var glass = FindDescendants<MeshInstance3D>(facade)
            .FirstOrDefault(mesh => mesh.Name == "DwellingFacade_Street_Window1_Glass_LOD0")
            ?? throw new InvalidOperationException(
                "MainStreetEastNeighborFacade is missing DwellingFacade_Street_Window1_Glass_LOD0.");
        var sill = FindDescendants<MeshInstance3D>(facade)
            .FirstOrDefault(mesh => mesh.Name == "DwellingFacade_Street_Window1_Sill_LOD0")
            ?? throw new InvalidOperationException(
                "MainStreetEastNeighborFacade is missing DwellingFacade_Street_Window1_Sill_LOD0.");
        var glassMesh = glass.Mesh
            ?? throw new InvalidOperationException("The authored side-window glass has no mesh.");
        var sillMesh = sill.Mesh
            ?? throw new InvalidOperationException("The authored side-window sill has no mesh.");
        var glassBounds = glassMesh.GetAabb();
        var sillBounds = sillMesh.GetAabb();
        var glassLocal = facade.ToLocal(glass.GlobalPosition);
        var sillLocal = facade.ToLocal(sill.GlobalPosition);
        var frontLocal = facade.ToLocal(
                glass.GlobalPosition + facade.GlobalTransform.Basis.Z.Normalized()) - glassLocal;
        frontLocal = frontLocal.LengthSquared() > .0001f
            ? frontLocal.Normalized()
            : new Vector3(0f, 0f, 1f);
        var sillTop = sillLocal.Y + sillBounds.End.Y;
        var windowWidth = Mathf.Max(glassBounds.Size.X, .20f);
        var windowHeight = Mathf.Max(glassBounds.Size.Y, .20f);
        var windowDepth = Mathf.Max(glassBounds.Size.Z, .02f);
        var revealPosition = glassLocal + frontLocal * (windowDepth * .5f + .065f);

        // Keep the existing inset just visible through the slightly ajar shutter.
        // Household details remain state-gated until the player opens it.
        _mainStreetSideWindowReveal = new Node3D
        {
            Name = "MainStreetSideWindowReveal",
            Position = revealPosition,
            Visible = true
        };
        _mainStreetSideWindowReveal.SetMeta("presentationOnly", true);
        _mainStreetSideWindowReveal.SetMeta("visualOnly", true);
        _mainStreetSideWindowReveal.SetMeta("physicalAction", "open the exterior shutter");
        _mainStreetSideWindowReveal.SetMeta("sourceWindow", glass.GetPath().ToString());
        facade.AddChild(_mainStreetSideWindowReveal);

        AddVisualBox(
            _mainStreetSideWindowReveal,
            "WarmInteriorInset",
            new(windowWidth * .98f, windowHeight * .82f, .032f),
            new(0f, 0f, -.026f),
            "a88a5f",
            "glass");
        // A softly lit fabric backing suggests a curtain behind the pane.
        var curtain = GD.Load<Texture2D>("res://assets/textures/painterly/old_fabric_v3_albedo.png");
        _mainStreetSideWindowReveal.GetNode<MeshInstance3D>("WarmInteriorInset").MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml("e1bc85"),
            AlbedoTexture = curtain,
            EmissionEnabled = true,
            Emission = Color.FromHtml("bd9766"),
            EmissionTexture = curtain,
            EmissionEnergyMultiplier = .35f,
            Roughness = .92f
        };
        var sillRelativeToGlass = sillTop - glassLocal.Y;
        var sillDetail = new Node3D
        {
            Name = "SillHouseholdDetail",
            Position = new Vector3(0f, sillRelativeToGlass + .012f, .038f),
            Scale = new Vector3(1.08f, 1.08f, 1f),
            Visible = false
        };
        _mainStreetSideWindowHousehold = sillDetail;
        sillDetail.SetMeta("presentationOnly", true);
        sillDetail.SetMeta("visualOnly", true);
        _mainStreetSideWindowReveal.AddChild(sillDetail);

        AddSideWindowMitten(sillDetail, "LeftChildMitten", new(-windowWidth * .21f, .071f, .026f), "76504a", -8f);
        AddSideWindowMitten(sillDetail, "RightChildMitten", new(windowWidth * .21f, .071f, .026f), "65756e", 7f);

        var cup = new Node3D { Name = "ChildCup", Position = new(0f, .071f, .030f) };
        cup.SetMeta("presentationOnly", true);
        cup.SetMeta("visualOnly", true);
        sillDetail.AddChild(cup);
        DiscoveryCylinder(cup, "CupBody", .071f, .060f, .114f, Vector3.Zero, "d7d0bc");
        DiscoveryCylinder(cup, "CupRim", .078f, .078f, .014f, new(0f, .057f, 0f), "ede4ca");
        AddVisualBox(cup, "CupHandle", new(.062f, .056f, .024f), new(.074f, 0f, 0f), "c6bea9", "ceramic", rollDegrees: 8f);

        var fogMark = new Node3D
        {
            Name = "FogCircleAndEars",
            Position = new(-windowWidth * .24f, windowHeight * .16f, .050f),
            Scale = Vector3.One * .86f,
            Visible = false
        };
        _mainStreetSideWindowFog = fogMark;
        fogMark.SetMeta("presentationOnly", true);
        fogMark.SetMeta("visualOnly", true);
        _mainStreetSideWindowReveal.AddChild(fogMark);
        foreach (var part in new[] { ("FogCircle", Vector3.Zero, .102f, .112f),
            ("FogLeftEar", new Vector3(-.075f, .081f, 0f), .032f, .040f),
            ("FogRightEar", new Vector3(.075f, .081f, 0f), .032f, .040f) })
        {
            fogMark.AddChild(new MeshInstance3D
            {
                Name = part.Item1, Position = part.Item2, RotationDegrees = new(90f, 0f, 0f),
                Mesh = new TorusMesh { InnerRadius = part.Item3, OuterRadius = part.Item4,
                    Rings = 24, RingSegments = 6 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("b7c4bd"), Roughness = 1f }
            });
        }

        // Leave a narrow pane visible beside the leaf at its -42° rest pose.
        var shutterWidth = windowWidth + .04f;
        var shutterHeight = windowHeight + .14f;
        var shutterDepth = windowDepth + .055f;
        var shutterCenter = glassLocal + frontLocal * (windowDepth * .5f + .095f);
        _mainStreetSideWindowShutter = new Node3D
        {
            Name = "MainStreetSideWindowShutterHinge",
            Position = new Vector3(
                shutterCenter.X - shutterWidth * .5f,
                shutterCenter.Y,
                shutterCenter.Z)
        };
        _mainStreetSideWindowShutter.SetMeta("presentationOnly", true);
        _mainStreetSideWindowShutter.SetMeta("visualOnly", true);
        _mainStreetSideWindowShutter.SetMeta("physicalAction", "open the exterior shutter");
        facade.AddChild(_mainStreetSideWindowShutter);
        AddVisualBox(
            _mainStreetSideWindowShutter,
            "ShutterLeaf",
            new(shutterWidth, shutterHeight, shutterDepth),
            new(shutterWidth * .5f, 0f, 0f),
            "72533d",
            "wood_furniture");
        AddVisualBox(
            _mainStreetSideWindowShutter,
            "ShutterLatch",
            new(.045f, .075f, .030f),
            new(shutterWidth * .83f, -.035f, shutterDepth * .58f),
            "c6a86f",
            "metal");

        var targetPosition = glass.GlobalPosition
            + facade.GlobalTransform.Basis.Z.Normalized() * .58f;
        var target = DiscoveryTarget(
            village,
            MainStreetSideWindowSlug,
            new(windowWidth + .45f, windowHeight + .55f, .95f),
            village.ToLocal(targetPosition),
            journal: true);
        target.SetMeta("activePropPath", glass.GetPath().ToString());
        target.SetMeta("physicalAction", "open the exterior shutter");
        target.SetMeta("sourceSillPath", sill.GetPath().ToString());
    }

    private static Node3D AddSideWindowMitten(
        Node3D parent,
        string name,
        Vector3 at,
        string color,
        float rollDegrees)
    {
        var mitten = new Node3D
        {
            Name = name,
            Position = at,
            RotationDegrees = new(0f, 0f, rollDegrees)
        };
        mitten.SetMeta("presentationOnly", true);
        mitten.SetMeta("visualOnly", true);
        parent.AddChild(mitten);
        var palm = new MeshInstance3D
        {
            Name = "Palm",
            Mesh = new SphereMesh { Radius = .084f, Height = .17f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "fabric")
        };
        palm.Scale = new Vector3(.92f, .80f, .62f);
        palm.SetMeta("presentationOnly", true);
        palm.SetMeta("visualOnly", true);
        mitten.AddChild(palm);
        var thumb = new MeshInstance3D
        {
            Name = "Thumb",
            Position = new(.058f, -.005f, .018f),
            Mesh = new SphereMesh { Radius = .040f, Height = .086f, RadialSegments = 10, Rings = 5 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "fabric")
        };
        thumb.RotationDegrees = new(0f, 0f, -18f);
        thumb.SetMeta("presentationOnly", true);
        thumb.SetMeta("visualOnly", true);
        mitten.AddChild(thumb);
        return mitten;
    }

    private void BuildConnectiveStreetRepairBenchDiscovery(StyleBenchmarkZone village, Node3D core)
    {
        var returnZone = core.GetNodeOrNull<Node3D>("ConnectiveStreetReturn")
            ?? throw new InvalidOperationException(
                "Act I core world is missing ConnectiveStreetReturn.");
        var activeParcel = core.GetNodeOrNull<Node3D>(
                "Act1AuthoredExteriorKitPresentation/NeighborParcels/ConnectiveStreet/DeepParcel/ConnectiveStreetDeepBanyaYardParcel")
            ?? throw new InvalidOperationException(
                "The active connective authored parcel is missing.");
        var shed = FindDescendants<Node3D>(activeParcel)
            .FirstOrDefault(node => node.Name == "VillageParcel_VariantC_BanyaYard_Outbuilding")
            ?? throw new InvalidOperationException(
                "The active connective authored parcel is missing its outbuilding.");
        var openBay = FindDescendants<MeshInstance3D>(shed)
            .FirstOrDefault(mesh => mesh.Name == "VariantC_Utility_OpenBay_LOD0")
            ?? throw new InvalidOperationException(
                "The active connective outbuilding is missing VariantC_Utility_OpenBay_LOD0.");
        var openBayMesh = openBay.Mesh
            ?? throw new InvalidOperationException("The connective outbuilding open bay has no mesh.");
        var openBayBounds = openBayMesh.GetAabb();
        var outwardWorld = openBay.GlobalTransform.Basis.Z.Normalized();
        var bayFrontWorld = openBay.GlobalPosition + outwardWorld * (openBayBounds.Size.Z * .5f);
        var benchWorld = bayFrontWorld + outwardWorld * .85f;
        benchWorld.Y = AgentBAct1HeightField.CollisionGround(benchWorld.X, benchWorld.Z);
        var benchDirection = returnZone.ToLocal(benchWorld + outwardWorld)
            - returnZone.ToLocal(benchWorld);
        if (benchDirection.LengthSquared() <= .0001f)
            benchDirection = new Vector3(0f, 0f, 1f);

        var bench = new Node3D
        {
            Name = "ConnectiveStreetRepairBench",
            Position = returnZone.ToLocal(benchWorld),
            RotationDegrees = new(0f, DirectionYaw(benchDirection), 0f)
        };
        bench.SetMeta("presentationOnly", true);
        bench.SetMeta("visualOnly", true);
        bench.SetMeta("physicalAction", "finish wrapping the shovel handle");
        bench.SetMeta("anchorSource", openBay.GetPath().ToString());
        returnZone.AddChild(bench);

        AddVisualBox(bench, "BenchTop", new(1.62f, .10f, .58f), new(0f, .78f, 0f), "6f543c", "wood_furniture");
        AddVisualBox(bench, "BenchEdge", new(1.48f, .055f, .07f), new(0f, .715f, .28f), "8a6b4e", "wood_furniture");
        foreach (var x in new[] { -.66f, .66f })
        {
            AddVisualBox(bench, x < 0f ? "BenchLegLeft" : "BenchLegRight", new(.12f, .72f, .12f), new(x, .36f, 0f), "594332", "wood_furniture");
            AddVisualBox(bench, x < 0f ? "BenchBraceLeft" : "BenchBraceRight", new(.10f, .10f, .52f), new(x, .53f, 0f), "66503e", "wood_furniture");
        }

        _connectiveRepairBenchCollision = new StaticBody3D
        {
            Name = "ConnectiveRepairBenchCollision",
            Transform = village.GlobalTransform.AffineInverse() * bench.GlobalTransform
        };
        village.AddChild(_connectiveRepairBenchCollision);
        _connectiveRepairBenchCollision.AddChild(new CollisionShape3D
        {
            Position = new(0f, .415f, 0f),
            Shape = new BoxShape3D { Size = new(1.62f, .83f, .58f) }
        });

        var twineRoll = new Node3D
        {
            Name = "RepairTwineRoll",
            Position = new(-.43f, .8675f, -.07f)
        };
        twineRoll.SetMeta("presentationOnly", true);
        twineRoll.SetMeta("visualOnly", true);
        bench.AddChild(twineRoll);
        DiscoveryCylinder(twineRoll, "TwineCoil", .105f, .105f, .075f, Vector3.Zero, "bda87d");
        DiscoveryCylinder(twineRoll, "TwineCore", .038f, .038f, .083f, new(0f, .004f, 0f), "685646");
        AddVisualBox(twineRoll, "TwineLooseEnd", new(.17f, .018f, .020f), new(.11f, -.025f, .025f), "bda87d", "fabric", rollDegrees: -14f);

        var shovelWorld = benchWorld
            + openBay.GlobalTransform.Basis.X.Normalized() * 1.02f
            - outwardWorld * .16f;
        shovelWorld.Y = 0f;
        AddVisualSnowShovel(
            returnZone,
            "ConnectiveRepairShovel",
            returnZone.ToLocal(shovelWorld),
            DirectionYaw(benchDirection) - 14f);
        var shovel = returnZone.GetNode<Node3D>("ConnectiveRepairShovel");
        shovel.SetMeta("physicalAction", "finish wrapping the shovel handle");
        var handle = shovel.GetNodeOrNull<MeshInstance3D>("WoodHandle")
            ?? throw new InvalidOperationException("The repair shovel helper did not create WoodHandle.");
        var handleMesh = handle.Mesh
            ?? throw new InvalidOperationException("The repair shovel handle has no mesh.");
        var handleBounds = handleMesh.GetAabb();
        var handleBottom = handle.Position.Y + handleBounds.Position.Y;
        var handleTop = handle.Position.Y + handleBounds.End.Y;
        var wrapY = Mathf.Lerp(handleBottom, handleTop, .42f);

        _connectiveRepairLooseWrap = new Node3D { Name = "LooseHalfTurnWrap" };
        _connectiveRepairLooseWrap.SetMeta("presentationOnly", true);
        _connectiveRepairLooseWrap.SetMeta("visualOnly", true);
        shovel.AddChild(_connectiveRepairLooseWrap);
        DiscoveryCylinder(_connectiveRepairLooseWrap, "LooseBandLower", .027f, .030f, .022f, new(0f, wrapY, .025f), "bda87d");
        DiscoveryCylinder(_connectiveRepairLooseWrap, "LooseBandUpper", .027f, .030f, .022f, new(0f, wrapY + .048f, .025f), "bda87d");
        AddVisualBox(_connectiveRepairLooseWrap, "LooseTail", new(.095f, .022f, .020f), new(.045f, wrapY + .055f, .025f), "bda87d", "fabric", rollDegrees: -18f);

        _connectiveRepairFinishedWrap = new Node3D
        {
            Name = "FinishedHandleWrap",
            Visible = false
        };
        _connectiveRepairFinishedWrap.SetMeta("presentationOnly", true);
        _connectiveRepairFinishedWrap.SetMeta("visualOnly", true);
        shovel.AddChild(_connectiveRepairFinishedWrap);
        for (var index = 0; index < 4; index++)
        {
            DiscoveryCylinder(
                _connectiveRepairFinishedWrap,
                $"FinishedBand{index}",
                .028f,
                .031f,
                .034f,
                new(0f, wrapY - .018f + index * .032f, .025f),
                "c4ae7e");
        }
        AddVisualBox(_connectiveRepairFinishedWrap, "FinishedTail", new(.07f, .020f, .019f), new(.035f, wrapY + .087f, .025f), "c4ae7e", "fabric", rollDegrees: -8f);

        var target = DiscoveryTarget(
            village,
            ConnectiveStreetRepairBenchSlug,
            new(2.15f, 1.55f, 1.50f),
            village.ToLocal(bench.GlobalPosition + Vector3.Up * .68f),
            journal: true);
        target.SetMeta("activePropPath", bench.GetPath().ToString());
        target.SetMeta("physicalAction", "finish wrapping the shovel handle");
        target.SetMeta("anchorSource", openBay.GetPath().ToString());
    }

    private void UpdateAct1OptionalDiscoveries()
    {
        if (_runtimeBridge?.ActiveSceneId is null) return;
        var knowledge = _runtimeBridge.SelectRuntimeState().GetProperty("knowledge");
        bool Found(string slug) => knowledge.TryGetProperty(DiscoveryPrefix + slug, out var entry)
            && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";
        var exterior = ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";

        if (_connectiveRepairBenchCollision is not null)
            _connectiveRepairBenchCollision.CollisionLayer = exterior ? 1u : 0u;
        var windowFound = Found(MainStreetSideWindowSlug);
        if (_mainStreetSideWindowReveal is not null
            && _mainStreetSideWindowShutter is not null
            && _mainStreetSideWindowFound != windowFound)
        {
            // The warm inset is a restrained pre-action clue; only the household
            // details are gated until the shutter is actually opened.
            _mainStreetSideWindowReveal.Visible = true;
            if (_mainStreetSideWindowHousehold is not null)
                _mainStreetSideWindowHousehold.Visible = windowFound;
            if (_mainStreetSideWindowFog is not null)
                _mainStreetSideWindowFog.Visible = windowFound;
            _mainStreetSideWindowTurn?.Kill();
            var targetYaw = windowFound ? Mathf.DegToRad(-160f) : Mathf.DegToRad(-42f);
            if (_mainStreetSideWindowFound == false && windowFound && exterior)
            {
                _mainStreetSideWindowTurn = CreateTween();
                _mainStreetSideWindowTurn.TweenProperty(
                    _mainStreetSideWindowShutter,
                    "rotation:y",
                    targetYaw,
                    .58f);
            }
            else
            {
                _mainStreetSideWindowShutter.Rotation = new(0f, targetYaw, 0f);
            }
            _mainStreetSideWindowFound = windowFound;
        }

        var repairFound = Found(ConnectiveStreetRepairBenchSlug);
        if (_connectiveRepairLooseWrap is not null
            && _connectiveRepairFinishedWrap is not null
            && _connectiveStreetRepairBenchFound != repairFound)
        {
            _connectiveRepairWrapReveal?.Kill();
            _connectiveRepairLooseWrap.Visible = !repairFound;
            _connectiveRepairFinishedWrap.Visible = repairFound;
            if (_connectiveStreetRepairBenchFound == false && repairFound && exterior)
            {
                _connectiveRepairFinishedWrap.Scale = Vector3.One * .82f;
                _connectiveRepairWrapReveal = CreateTween();
                _connectiveRepairWrapReveal.TweenProperty(
                    _connectiveRepairFinishedWrap,
                    "scale",
                    Vector3.One,
                    .35f);
            }
            else
            {
                _connectiveRepairFinishedWrap.Scale = Vector3.One;
            }
            _connectiveStreetRepairBenchFound = repairFound;
        }
    }
}
