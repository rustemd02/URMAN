using System;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string ConnectiveStreetBenchSlug = "connective-street-return-bench";
    private const string FapCarePorchSlug = "fap-exterior-care-porch";

    private Node3D? _connectiveBenchShelf;
    private StaticBody3D? _connectiveBenchCollision;
    private bool? _connectiveBenchFound;
    private Tween? _connectiveBenchShelfMove;

    private Node3D? _fapCareCup;
    private Node3D? _fapCareNapkin;
    private Vector3 _fapCareCupAtRest;
    private Vector3 _fapCareCupPlaced;
    private bool? _fapCareFound;
    private Tween? _fapCareReveal;

    private void BuildAct1QuietCareDiscoveries()
    {
        var village = _zoneInstances["village_day"] as StyleBenchmarkZone
            ?? throw new InvalidOperationException(
                "Connected Act I layout is missing the village_day interaction zone.");
        var core = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox")
            ?? throw new InvalidOperationException(
                "Act I core world is missing its presentation root.");
        var returnZone = core.GetNodeOrNull<Node3D>("ConnectiveStreetReturn")
            ?? throw new InvalidOperationException(
                "Act I core world is missing ConnectiveStreetReturn.");

        // Relayout v3: on the verge in front of the Тукай урамы plots (their fences stand at x -6).
        var anchor = new Vector3(
            -4.4f,
            AgentBAct1HeightField.CollisionGround(-4.4f, -38.8f),
            -38.8f);
        var minaretDirection = HorizontalDirection(
            new Vector3(AgentBAct1Layout.MosqueAnchor.X, 0f, AgentBAct1Layout.MosqueAnchor.Y) - anchor);
        var bench = new Node3D
        {
            Name = "ConnectiveReturnCareBench",
            Position = anchor,
            RotationDegrees = new Vector3(0f, DirectionYaw(minaretDirection), 0f)
        };
        bench.SetMeta("presentationOnly", true);
        bench.SetMeta("visualOnly", true);
        bench.SetMeta("physicalAction", "fold out the side shelf for a bag");
        returnZone.AddChild(bench);

        for (var index = 0; index < 3; index++)
        {
            AddVisualBox(
                bench,
                $"SeatPlank{index}",
                new(1.82f, .07f, .12f),
                new(0f, .49f, -.13f + index * .13f),
                index == 1 ? "8b6c4f" : "655648",
                "wood_furniture");
        }
        foreach (var side in new[] { "Left", "Right" })
        {
            var x = side == "Left" ? -.72f : .72f;
            AddVisualBox(
                bench,
                $"BenchLeg{side}",
                new(.12f, .45f, .12f),
                new(x, .225f, 0f),
                "655648",
                "wood_furniture");
            AddVisualBox(
                bench,
                $"BenchBackPost{side}",
                new(.075f, .58f, .075f),
                new(x, .61f, -.25f),
                "655648",
                "wood_furniture");
        }
        AddVisualBox(
            bench,
            "BenchBackRest",
            new(1.85f, .19f, .055f),
            new(0f, .79f, -.25f),
            "72614d",
            "wood_furniture");

        // A hinged side shelf is stowed upright beside the bench. The
        // interaction folds it down to a horizontal bag rest; the back and
        // posts provide the ordinary windbreak and minaret sightline.
        _connectiveBenchShelf = new Node3D
        {
            Name = "BagRestShelf",
            Position = new(.76f, .50f, .05f),
            RotationDegrees = new(0f, 0f, 90f)
        };
        _connectiveBenchShelf.SetMeta("presentationOnly", true);
        _connectiveBenchShelf.SetMeta("visualOnly", true);
        bench.AddChild(_connectiveBenchShelf);
        AddVisualBox(
            _connectiveBenchShelf,
            "ShelfBoard",
            new(.50f, .08f, .38f),
            new(.25f, .04f, 0f),
            "8b7457",
            "wood_furniture");
        AddVisualBox(
            _connectiveBenchShelf,
            "ShelfLip",
            new(.50f, .14f, .06f),
            new(.25f, .11f, .16f),
            "6e5845",
            "wood_furniture");
        AddVisualBox(
            bench,
            "BagRestShelfSupport",
            new(.08f, .44f, .08f),
            new(.76f, .31f, .05f),
            "655648",
            "wood_furniture");

        var benchTarget = DiscoveryTarget(
            village,
            ConnectiveStreetBenchSlug,
            new(2.10f, 1.45f, 1.05f),
            village.ToLocal(bench.GlobalPosition + Vector3.Up * .76f),
            journal: false);
        benchTarget.SetMeta(
            "activePropPath",
            "Act1CoreWorldGreybox/ConnectiveStreetReturn/ConnectiveReturnCareBench");
        benchTarget.SetMeta("physicalAction", "fold out the side shelf for a bag");

        // Keep the visual bench in the core presentation owner, while its
        // permanent gameplay collider follows the existing RoadsideDiscovery
        // pattern: one village-zone body, gated off for indoor zones.
        _connectiveBenchCollision = new StaticBody3D
        {
            Name = "ConnectiveReturnCareBenchCollision",
            Position = village.ToLocal(anchor),
            Rotation = bench.Rotation
        };
        village.AddChild(_connectiveBenchCollision);
        _connectiveBenchCollision.AddChild(new CollisionShape3D
        {
            Name = "Seat",
            Position = new(0f, .26f, 0f),
            Shape = new BoxShape3D { Size = new(1.8f, .52f, .47f) }
        });
        _connectiveBenchCollision.AddChild(new CollisionShape3D
        {
            Name = "Back",
            Position = new(0f, .70f, -.25f),
            Shape = new BoxShape3D { Size = new(1.85f, .38f, .075f) }
        });

        // FapEntryPorch_Deck's imported local bounds are y=.30.. .50,
        // x=-1.65..1.65 and z=-.47..1.15. Keep every care prop on that
        // authored deck rather than guessing against the world ground.

        var porch = core.GetNodeOrNull<Node3D>(
                "FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredEntryPorch")
            ?? throw new InvalidOperationException(
                "Act I authored FAP porch is missing FapAuthoredEntryPorch.");
        porch.SetMeta("quietCareAnchor", "active authored FapAuthoredEntryPorch");

        _fapCareNapkin = new Node3D
        {
            Name = "FapCareDryNapkin",
            Position = new Vector3(-.55f, .515f, .18f),
            Visible = false
        };
        _fapCareNapkin.SetMeta("presentationOnly", true);
        _fapCareNapkin.SetMeta("visualOnly", true);
        porch.AddChild(_fapCareNapkin);
        AddVisualBox(
            _fapCareNapkin,
            "FoldedNapkin",
            new(.14f, .018f, .12f),
            Vector3.Zero,
            "e2d8c4",
            "fabric",
            rollDegrees: 2f);
        AddVisualBox(
            _fapCareNapkin,
            "NapkinFold",
            new(.12f, .006f, .012f),
            new(0f, .014f, 0f),
            "b9aa8c",
            "fabric");

        _fapCareCupAtRest = new Vector3(-.55f, .58f, .18f);
        _fapCareCupPlaced = _fapCareCupAtRest + new Vector3(0f, 0f, .34f);
        _fapCareCup = new Node3D
        {
            Name = "FapCareEnamelCup",
            Position = _fapCareCupAtRest,
            RotationDegrees = new Vector3(180f, 0f, 0f)
        };
        _fapCareCup.SetMeta("presentationOnly", true);
        _fapCareCup.SetMeta("visualOnly", true);
        _fapCareCup.SetMeta("physicalAction", "lift and turn the enamel cup");
        porch.AddChild(_fapCareCup);
        _fapCareCup.AddChild(new MeshInstance3D
        {
            Name = "EnamelCupBody",
            Mesh = new CylinderMesh { TopRadius = .09f, BottomRadius = .075f, Height = .15f,
                RadialSegments = 24, CapTop = false },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("e8e5d9"),
                Roughness = .55f, CullMode = BaseMaterial3D.CullModeEnum.Disabled }
        });
        _fapCareCup.AddChild(new MeshInstance3D
        {
            Name = "EnamelCupRim", Position = new(0, .075f, 0),
            Mesh = new TorusMesh { InnerRadius = .082f, OuterRadius = .094f, Rings = 24, RingSegments = 8 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("707d7d", "metal", sheltered: true)
        });
        _fapCareCup.AddChild(new MeshInstance3D
        {
            Name = "EnamelCupHandle", Position = new(.105f, 0, 0), RotationDegrees = new(90, 0, 0),
            Mesh = new TorusMesh { InnerRadius = .034f, OuterRadius = .049f, Rings = 16, RingSegments = 8 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("d1cfc2", "metal", sheltered: true)
        });

        var thermos = new Node3D
        {
            Name = "FapCareThermos",
            Position = new Vector3(.05f, .67f, .22f)
        };
        thermos.SetMeta("presentationOnly", true);
        thermos.SetMeta("visualOnly", true);
        porch.AddChild(thermos);
        DiscoveryCylinder(thermos, "ThermosBody", .065f, .075f, .33f, Vector3.Zero, "65736c");
        DiscoveryCylinder(
            thermos,
            "ThermosCap",
            .055f,
            .055f,
            .06f,
            new Vector3(0f, .20f, 0f),
            "4a5652");
        var thermosStopper = new MeshInstance3D
        {
            Name = "ThermosStopper",
            Position = new Vector3(0f, .245f, 0f),
            Mesh = new SphereMesh
            {
                Radius = .042f,
                Height = .070f,
                RadialSegments = 12,
                Rings = 4
            },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("4a5652", "metal")
        };
        thermosStopper.SetMeta("visualOnly", true);
        thermos.AddChild(thermosStopper);
        AddVisualBox(
            thermos,
            "ThermosBand",
            new(.14f, .035f, .14f),
            new(0f, .06f, 0f),
            "b3a271",
            "fabric");

        for (var index = 0; index < 2; index++)
        {
            var glove = new Node3D
            {
                Name = $"FapCareGlove{index}",
                Position = new Vector3(.34f + index * .22f, .55f, .25f),
                RotationDegrees = new Vector3(0f, index == 0 ? -8f : 7f, index == 0 ? -5f : 4f)
            };
            glove.SetMeta("presentationOnly", true);
            glove.SetMeta("visualOnly", true);
            porch.AddChild(glove);
            glove.AddChild(new MeshInstance3D
            {
                Name = "GlovePalm", Scale = new(1f, 1f, .8f),
                Mesh = new SphereMesh { Radius = .10f, Height = .08f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = PainterlyMaterialLibrary.ForColor("76545a", "fabric", sheltered: true)
            });
            glove.AddChild(new MeshInstance3D
            {
                Name = "GloveThumb", Position = new(.08f, 0, .015f), Scale = new(1f, 1f, .7f),
                Mesh = new SphereMesh { Radius = .04f, Height = .065f, RadialSegments = 10, Rings = 5 },
                MaterialOverride = PainterlyMaterialLibrary.ForColor("76545a", "fabric", sheltered: true)
            });
            AddVisualBox(glove, "GloveCuff", new(.14f, .06f, .065f),
                new(0f, 0f, -.085f), "5d4a51", "fabric");
        }

        var careTarget = DiscoveryTarget(
            village,
            FapCarePorchSlug,
            new(.78f, .85f, .82f),
            village.ToLocal(_fapCareCup.GlobalPosition + Vector3.Up * .08f),
            journal: false);
        careTarget.SetMeta(
            "activePropPath",
            "Act1CoreWorldGreybox/FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredEntryPorch/FapCareEnamelCup");
        careTarget.SetMeta("physicalAction", "lift and turn the enamel cup");
    }

    private void UpdateAct1QuietCareDiscoveries()
    {
        if (_runtimeBridge?.ActiveSceneId is null) return;
        var knowledge = _runtimeBridge.SelectRuntimeState().GetProperty("knowledge");
        bool Found(string slug) => knowledge.TryGetProperty(DiscoveryPrefix + slug, out var entry)
            && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";
        var exterior = ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";

        var benchFound = Found(ConnectiveStreetBenchSlug);
        if (_connectiveBenchCollision is not null)
            _connectiveBenchCollision.CollisionLayer = exterior ? 1u : 0u;
        if (_connectiveBenchShelf is not null && _connectiveBenchFound != benchFound)
        {
            _connectiveBenchShelfMove?.Kill();
            if (_connectiveBenchFound == false && benchFound && exterior)
            {
                _connectiveBenchShelf.Rotation = new Vector3(0f, 0f, Mathf.Pi / 2f);
                _connectiveBenchShelfMove = CreateTween();
                _connectiveBenchShelfMove.TweenProperty(
                    _connectiveBenchShelf, "rotation:z", 0f, .45f);
            }
            else
            {
                _connectiveBenchShelf.Rotation = new Vector3(
                    0f, 0f, benchFound ? 0f : Mathf.Pi / 2f);
            }
            _connectiveBenchFound = benchFound;
        }

        var careFound = Found(FapCarePorchSlug);
        if (_fapCareCup is not null && _fapCareFound != careFound)
        {
            _fapCareReveal?.Kill();
            if (_fapCareNapkin is not null)
                _fapCareNapkin.Visible = careFound;
            if (_fapCareFound == false && careFound && exterior)
            {
                var cup = _fapCareCup;
                cup.Position = _fapCareCupAtRest;
                cup.Rotation = new Vector3(Mathf.Pi, 0f, 0f);
                _fapCareReveal = CreateTween();
                _fapCareReveal.TweenProperty(cup, "position:y", _fapCareCupAtRest.Y + .22f, .2f);
                _fapCareReveal.TweenProperty(cup, "rotation:x", 0f, .25f);
                _fapCareReveal.Parallel().TweenProperty(cup, "position:z", _fapCareCupPlaced.Z, .25f);
                _fapCareReveal.TweenProperty(cup, "position:y", _fapCareCupPlaced.Y, .2f);
            }
            else
            {
                _fapCareCup.Position = careFound ? _fapCareCupPlaced : _fapCareCupAtRest;
                _fapCareCup.Rotation = careFound
                    ? Vector3.Zero
                    : new Vector3(Mathf.Pi, 0f, 0f);
            }
            _fapCareFound = careFound;
        }
    }
}
