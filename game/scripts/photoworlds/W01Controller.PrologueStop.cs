using System;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

/// <summary>
/// A late bus, a message from Babai and a light across the road. Aidar can
/// signal back or cross without answering; only what he did carries into the ride.
/// </summary>
public partial class W01Controller
{
    // ---- Staging, in metres along (u) and across (v) the first stretch of the trail -----------
    private const float StopRoadNear = 2.4f;
    private const float StopRoadFar = 8.4f;
    private const float StopLeaveAt = 8.9f;       // past the far edge of the road: the rule is broken
    private const float StopPrintsFrom = 9.6f;    // the child's prints begin where the road ends
    private const float StopLampEnergy = 1.8f;
    private const float StopLampFillEnergy = .16f;
    private const float StopShelterFillEnergy = .28f;
    private const float StopRoadReach = 62f;      // how far the road is drawn each way
    private const float StopRoadWalk = 19f;       // how far along it he can go

    internal const string PrologueMittenKey = "prologue/blue-mitten";
    internal const string PrologueSignalKey = "prologue/forest-signal";

    private Node3D? _stop;
    private SpotLight3D? _stopLamp;
    private OmniLight3D? _stopLampFill, _stopShelterFill;
    private StandardMaterial3D? _stopBulb;
    private Node3D? _stopBusLights;
    private MeshInstance3D? _stopSignal;
    private OmniLight3D? _stopSignalLight;
    private Node3D? _stopMitten;
    private InteractionTarget? _stopMittenTarget;
    private bool _takingMitten;
    private CanvasLayer? _stopCard;
    private Label? _stopCardLabel;
    private readonly bool[] _stopFired = new bool[12];
    private bool _stopLeft;
    private double _stopSignalAt = -1;
    private double _replyAt = -1;
    private bool _answeredSignal, _writingSignal, _signalInputReleased;
    private Label? _signalPrompt;

    // The journal is available on demand, including before the voice or the signal.
    internal RuntimeBridge.PhotoWorldHintProjection StopJournalHint(bool branchMoved)
    {
        if (branchMoved && _nightmareBestProgress > PrologueRouteLength * .52f)
            return new("prologue-exit", "Куда ведут следы?", "За поворотом тропа выходит на поляну.", "Осмотрите последние следы на поляне.");
        return new("prologue-hut", "Где дождаться бабая?", "Через дорогу светится окно сторожки.",
            "Перейдите дорогу и постучите в дверь сторожки.");

    }

    private static (Vector2 Along, Vector2 Across) PrologueStopAxes()
    {
        var nodes = PrologueTrailNodes;
        var along = new Vector2(nodes[1].X - nodes[0].X, nodes[1].Z - nodes[0].Z).Normalized();
        return (along, new Vector2(-along.Y, along.X));
    }

    /// <summary>A point of the stop set in controller-local space, on the ground plus a lift.</summary>
    private static Vector3 StopPoint(float u, float v, float lift = 0f)
    {
        var (along, across) = PrologueStopAxes();
        var origin = new Vector2(PrologueTrailNodes[0].X, PrologueTrailNodes[0].Z);
        var point = origin + along * u + across * v;
        return new Vector3(point.X, ForestGroundHeight(point.X, point.Y) + lift, point.Y);
    }

    /// <summary>Where the forest must keep clear: the stop, its light and the road through the trees.</summary>
    private static bool InsidePrologueStopClearing(Vector2 point)
    {
        var (along, across) = PrologueStopAxes();
        var offset = point - new Vector2(PrologueTrailNodes[0].X, PrologueTrailNodes[0].Z);
        var u = offset.Dot(along);
        var v = offset.Dot(across);
        if (u > StopRoadNear - 2.2f && u < StopRoadFar + 1.6f && Mathf.Abs(v) < StopRoadReach + 30f) return true;   // the road
        if (u > -4f && u < -.5f && v > -8.2f && v < -5f) return true; // clearance around the opening camera
        return u > -7.5f && u <= StopRoadNear && Mathf.Abs(v) < 7f;                                   // the stop
    }

    // ---- Build ---------------------------------------------------------------------------------

    private void BuildPrologueStop(Node3D owner)
    {
        var stop = new Node3D { Name = "PrologueBusStop" };
        stop.SetMeta("presentationOnly", true);
        owner.AddChild(stop);
        _stop = stop;
        var (along, across) = PrologueStopAxes();
        var along3 = new Vector3(along.X, 0f, along.Y);
        var across3 = new Vector3(across.X, 0f, across.Y);

        // The road: packed snow with two ruts, running off into the dark both ways.
        using (var surface = new SurfaceTool())
        {
            ShaderMaterial RoadSnow(string tint, string family)
            {
                // Keep this lamp's matte snow response local; the shared daytime
                // family and its winded-snow normal maps stay unchanged.
                var material = (ShaderMaterial)PainterlyMaterialLibrary.ForColor(tint, family).Duplicate();
                material.SetShaderParameter("snow_texture_contrast", .16f);
                material.SetShaderParameter("snow_sparkle", 0f);
                material.SetShaderParameter("snow_roughness_range", new Vector2(.88f, .97f));
                material.SetShaderParameter("roughness_value", .94f);
                material.SetShaderParameter("specular_value", .08f);
                material.SetShaderParameter("snow_relief_scale", .3f);
                return material;
            }
            surface.Begin(Mesh.PrimitiveType.Triangles);
            void Strip(float from, float to, float lift, bool wheelTrack)
            {
                var rows = Mathf.Max(1, Mathf.CeilToInt((to - from) / 1f));
                Vector3 RoadPoint(float u, float v)
                {
                    // Both wheel tracks follow the same steering line.
                    if (wheelTrack) u += .13f * Mathf.Sin(v * .16f) + .045f * Mathf.Sin(v * .63f);
                    return StopPoint(u, v, lift);
                }
                for (var row = 0; row < rows; row++)
                for (var v = -StopRoadReach; v < StopRoadReach; v += 1.25f)
                {
                    var u0 = Mathf.Lerp(from, to, (float)row / rows); var u1 = Mathf.Lerp(from, to, (row + 1f) / rows);
                    var a = RoadPoint(u0, v); var b = RoadPoint(u1, v);
                    var c = RoadPoint(u1, v + 1.25f); var d = RoadPoint(u0, v + 1.25f);
                    foreach (var point in new[] { a, b, c, a, c, d })
                    {
                        const float sample = .25f;
                        var dx = ForestGroundHeight(point.X + sample, point.Z) - ForestGroundHeight(point.X - sample, point.Z);
                        var dz = ForestGroundHeight(point.X, point.Z + sample) - ForestGroundHeight(point.X, point.Z - sample);
                        surface.SetNormal(new Vector3(-dx, 2f * sample, -dz).Normalized());
                        surface.SetUV(new Vector2(point.X, point.Z) * .3f);
                        surface.AddVertex(point);
                    }
                }
            }
            surface.SetMaterial(RoadSnow("b8bbbc", "snow_road"));
            // The shared trample field may lower this ribbon by 5 cm. Keep a
            // positive gap above the undeformed imported ground even then.
            Strip(StopRoadNear, StopRoadFar, .08f, false);
            var roadMesh = surface.Commit();
            surface.Clear();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            surface.SetMaterial(RoadSnow("a3a9ad", "snow_trampled"));
            Strip(StopRoadNear + 1.42f, StopRoadNear + 1.78f, .092f, true);
            Strip(StopRoadFar - 1.78f, StopRoadFar - 1.42f, .092f, true);
            surface.Commit(roadMesh);
            // He cannot walk the road home: nine kilometres of dark either way.
            var bounds = new StaticBody3D { Name = "RoadEnds", CollisionLayer = 1, CollisionMask = 0 };
            stop.AddChild(bounds);
            void Wall(Vector3 at, Vector3 size, float yaw)
            {
                bounds.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = at + Vector3.Up * 2f, Rotation = new Vector3(0f, yaw, 0f) });
            }
            var facing = Mathf.Atan2(along.X, along.Y);
            Wall(StopPoint(1f, StopRoadWalk), new Vector3(.4f, 6f, 22f), facing);
            Wall(StopPoint(1f, -StopRoadWalk), new Vector3(.4f, 6f, 22f), facing);
            Wall(StopPoint(-8.2f, 0f), new Vector3(2f * StopRoadWalk, 6f, .4f), facing);
            stop.AddChild(new MeshInstance3D
            {
                Name = "Road", Mesh = roadMesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }

        // The shelter sits beside the road, leaving the first view open to the far side.
        var shelter = new Node3D { Name = "Shelter", Position = StopPoint(-1.9f, -3.5f) };
        shelter.Basis = Basis.LookingAt(-along3, Vector3.Up); // local +Z looks across the road at the forest
        stop.AddChild(shelter);
        var concrete = PainterlyMaterialLibrary.ForColor("7f8a8c", "plaster");
        var paint = PainterlyMaterialLibrary.ForColor("5f8088", "plaster");
        var wood = PainterlyMaterialLibrary.ForColor("6a5a48", "wood");
        var snowMaterial = PainterlyMaterialLibrary.ForColor("e8edf2", "snow_roof");
        var shelterBody = new StaticBody3D { Name = "ShelterBody", CollisionLayer = 1, CollisionMask = 0 };
        shelter.AddChild(shelterBody);
        MeshInstance3D Piece(Node3D parent, string name, Vector3 size, Vector3 at, Material material, bool solid = false)
        {
            var piece = new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size }, Position = at, MaterialOverride = material };
            parent.AddChild(piece);
            if (solid) shelterBody.AddChild(new CollisionShape3D
            {
                Name = name + "Contact", Position = at, Shape = new BoxShape3D { Size = size }
            });
            return piece;
        }
        Piece(shelter, "BackWall", new(3.5f, 2.35f, .14f), new(0f, 1.17f, -.55f), paint, solid: true);
        Piece(shelter, "BackWallBand", new(3.52f, .5f, .15f), new(0f, .25f, -.55f), concrete);
        Piece(shelter, "Side-1.68", new(.14f, 2.35f, 1.6f), new(-1.68f, 1.17f, -.2f), paint, solid: true);
        // Leave the front corner open to the outside view of the bench print.
        Piece(shelter, "Side1.68", new(.14f, 2.35f, 1.15f), new(1.68f, 1.17f, -.55f), paint, solid: true);
        Piece(shelter, "Roof", new(4.0f, .12f, 2.3f), new(0f, 2.41f, -.05f), concrete, solid: true);
        Piece(shelter, "RoofSnow", new(3.9f, .17f, 2.2f), new(0f, 2.55f, -.05f), snowMaterial);
        Piece(shelter, "BenchSeat", new(2.5f, .06f, .4f), new(0f, .46f, 0f), wood, solid: true);
        Piece(shelter, "BenchBack", new(2.5f, .28f, .05f), new(0f, .78f, -.22f), wood);
        foreach (var x in new[] { -1.05f, 1.05f })
            Piece(shelter, "BenchLeg" + x, new(.07f, .46f, .36f), new(x, .23f, 0f), concrete);
        Piece(shelter, "BenchSnow", new(.9f, .05f, .34f), new(.75f, .515f, 0f), snowMaterial);
        // The print somebody left on the bench: the forest he is about to walk into.
        // Move the existing readable photograph together with its interaction shape.
        var print = GetNode<Marker3D>("Anchors/F00/ProloguePhotoSource");
        print.GlobalTransform = shelter.GlobalTransform * new Transform3D(
            Basis.FromEuler(new Vector3(-Mathf.Pi / 2f, Mathf.DegToRad(17f), 0f)),
            new Vector3(1.2f, .496f, .15f));
        _stopShelterFill = new OmniLight3D
        {
            Name = "ShelterBounce", Position = new Vector3(0f, 1.5f, .35f), LightColor = new Color(1f, .9f, .75f),
            LightEnergy = StopShelterFillEnergy, OmniRange = 3.2f, ShadowEnabled = false, LightSpecular = 0f
        };
        shelter.AddChild(_stopShelterFill);
        // The stop sign: a plate with the letter every such stop carries.
        var signAt = StopPoint(1.2f, 2.9f);
        var pole = new MeshInstance3D
        {
            Name = "SignPole", Mesh = new CylinderMesh { TopRadius = .03f, BottomRadius = .03f, Height = 2.7f, RadialSegments = 8 },
            Position = signAt + Vector3.Up * 1.35f, MaterialOverride = PainterlyMaterialLibrary.ForColor("6c7375", "metal")
        };
        stop.AddChild(pole);
        var plate = new Node3D { Name = "SignPlate", Position = signAt + Vector3.Up * 2.45f };
        plate.Basis = Basis.LookingAt(-across3, Vector3.Up);
        stop.AddChild(plate);
        Piece(plate, "Plate", new(.5f, .42f, .02f), Vector3.Zero, PainterlyMaterialLibrary.ForColor("c9a53a", "metal"));
        plate.AddChild(new Label3D
        {
            Text = "А", Position = new Vector3(0f, 0f, .012f), FontSize = 96, PixelSize = .0032f,
            Modulate = new Color(.08f, .08f, .08f), OutlineSize = 0, Shaded = true
        });

        // The only lamp for kilometres.
        var lampFoot = StopPoint(-.4f, -2.6f);
        stop.AddChild(new MeshInstance3D
        {
            Name = "LampPole", Mesh = new CylinderMesh { TopRadius = .05f, BottomRadius = .075f, Height = 5.6f, RadialSegments = 10 },
            Position = lampFoot + Vector3.Up * 2.8f, MaterialOverride = PainterlyMaterialLibrary.ForColor("5f6668", "metal")
        });
        var head = lampFoot + Vector3.Up * 5.55f + across3 * 1.25f + along3 * .5f;
        var arm = new MeshInstance3D
        {
            Name = "LampArm", Mesh = new BoxMesh { Size = new Vector3(.06f, .06f, 1.5f) },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("5f6668", "metal")
        };
        stop.AddChild(arm);
        arm.Position = (lampFoot + Vector3.Up * 5.55f + head) * .5f;
        arm.Basis = Basis.LookingAt((head - (lampFoot + Vector3.Up * 5.55f)).Normalized(), Vector3.Up);
        _stopBulb = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, .9f, .72f), EmissionEnabled = true,
            Emission = new Color(1f, .82f, .57f), EmissionEnergyMultiplier = 2f
        };
        stop.AddChild(new MeshInstance3D
        {
            Name = "LampBulb", Mesh = new SphereMesh { Radius = .1f, Height = .14f, RadialSegments = 10, Rings = 5 },
            Position = head - Vector3.Up * .06f, MaterialOverride = _stopBulb,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        _stopLamp = new SpotLight3D
        {
            Name = "StopLamp", Position = head - Vector3.Up * .12f, RotationDegrees = new Vector3(-90f, 0f, 0f),
            LightColor = new Color(1f, .87f, .67f), LightEnergy = StopLampEnergy,
            SpotRange = 13f, SpotAngle = 58f, SpotAngleAttenuation = 1.6f, SpotAttenuation = 1f,
            ShadowEnabled = true, ShadowBias = .06f, ShadowBlur = 1.6f, ShadowOpacity = .65f, LightSpecular = .03f,
            DistanceFadeEnabled = true, DistanceFadeBegin = 70f, DistanceFadeShadow = 36f, DistanceFadeLength = 14f
        };
        stop.AddChild(_stopLamp);
        _stopLampFill = new OmniLight3D
        {
            Name = "StopLampFill", Position = head - Vector3.Up * .5f, LightColor = new Color(1f, .9f, .75f),
            LightEnergy = StopLampFillEnergy, OmniRange = 6f, ShadowEnabled = false, LightSpecular = 0f
        };
        stop.AddChild(_stopLampFill);
        // Snow turning in the cone of the lamp.
        var flakes = new CpuParticles3D
        {
            Name = "LampSnow", Amount = 260, Lifetime = 4.4f, Preprocess = 4f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(4.2f, .3f, 4.2f),
            Direction = Vector3.Down, Spread = 18f, InitialVelocityMin = .5f, InitialVelocityMax = 1.1f,
            Gravity = new Vector3(.25f, -.7f, .1f), ScaleAmountMin = .6f, ScaleAmountMax = 1.5f,
            Position = head - Vector3.Up * .3f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new QuadMesh
            {
                Size = new Vector2(.05f, .05f),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(.95f, .96f, 1f), AlbedoTexture = SoftDot(),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, Roughness = 1f
                }
            }
        };
        stop.AddChild(flakes);

        // The tail lights of the bus that has just left him here.
        _stopBusLights = new Node3D { Name = "BusTailLights", Position = StopPoint(StopRoadNear + 1.6f, 7f, .95f) };
        var red = new StandardMaterial3D
        {
            AlbedoColor = new Color(.3f, .02f, .02f), EmissionEnabled = true, Emission = new Color(1f, .12f, .08f),
            EmissionEnergyMultiplier = 3.2f, DisableFog = true
        };
        foreach (var side in new[] { -.85f, .85f })
            _stopBusLights.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = .09f, Height = .18f, RadialSegments = 8, Rings = 4 },
                Position = along3 * side, MaterialOverride = red, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        stop.AddChild(_stopBusLights);

        // The torch that answers from the trees: two short blinks.
        _stopSignal = new MeshInstance3D
        {
            Name = "AnsweringTorch", Visible = false, Position = StopPoint(34f, 1.2f, 1.25f),
            Mesh = new SphereMesh { Radius = .07f, Height = .14f, RadialSegments = 8, Rings = 4 },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, .95f, .8f), EmissionEnabled = true, Emission = new Color(1f, .93f, .75f),
                EmissionEnergyMultiplier = 6f, DisableFog = true
            }
        };
        stop.AddChild(_stopSignal);
        _stopSignalLight = new OmniLight3D
        {
            Name = "AnsweringTorchLight", Visible = false, Position = _stopSignal.Position,
            LightColor = new Color(1f, .93f, .75f), LightEnergy = 1.6f, OmniRange = 7f, ShadowEnabled = false
        };
        stop.AddChild(_stopSignalLight);

        // The mitten, on a stick someone pushed into the snow beside the prints.
        var mittenAt = HutTrailPoint(.78f) + Vector3.Right * .8f;
        var stick = new MeshInstance3D
        {
            Name = "MittenStick", Mesh = new CylinderMesh { TopRadius = .012f, BottomRadius = .02f, Height = 1.5f, RadialSegments = 6 },
            Position = mittenAt + Vector3.Up * .72f, RotationDegrees = new Vector3(6f, 0f, -8f),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("3b3129", "wood_bark")
        };
        stop.AddChild(stick);
        _stopMitten = PrologueDeepForest.AddMitten(stop, mittenAt + new Vector3(.1f, 1.36f, 0f), new Vector3(0f, 30f, 172f));
        // The thicket switches all its tree bodies to the walking collision layer.
        // Keep the mitten's ray target with the controller, outside that tree.
        _stopMittenTarget = new InteractionTarget
        {
            Name = "PrologueMittenPickup", Position = ToLocal(_stopMitten.GlobalPosition),
            CollisionLayer = 4, CollisionMask = 0,
            InteractionId = "urman.fullgame:local/prologue/blue-mitten", Prompt = "Снять варежку с палки",
            PresentationRepeatAvailable = () => _nightmareMoodApplied && !_nightmareClimax
                && GodotObject.IsInstanceValid(_stopMitten) && _stopMitten.IsVisibleInTree(),
            PresentationRepeat = () => _ = TakePrologueMittenAsync()
        };
        _stopMittenTarget.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = .32f } });
        AddChild(_stopMittenTarget);

        // The place and the hour, written small in the corner.
        _stopCard = new CanvasLayer { Name = "PrologueStopCard", Layer = 97 };
        _stopCardLabel = new Label
        {
            Text = "Поворот на Кара-Урман\n23:40. Последний автобус ушёл.",
            AnchorLeft = 0, AnchorRight = 0, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = 56, OffsetRight = 760, OffsetTop = -190, OffsetBottom = -96,
            MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0)
        };
        _stopCardLabel.AddThemeFontOverride("font", UrmanUiTheme.HandFont);
        _stopCardLabel.AddThemeFontSizeOverride("font_size", 34);
        _stopCardLabel.AddThemeColorOverride("font_color", new Color(.93f, .89f, .8f));
        _stopCardLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, .7f));
        _stopCardLabel.AddThemeConstantOverride("outline_size", 6);
        _stopCard.AddChild(_stopCardLabel);
        _signalPrompt = new Label
        {
            Name = "AnswerTheLight", Visible = false, AnchorLeft = .5f, AnchorRight = .5f,
            AnchorTop = .67f, AnchorBottom = .67f, OffsetLeft = -260, OffsetRight = 260,
            HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _signalPrompt.AddThemeFontSizeOverride("font_size", 24);
        _signalPrompt.AddThemeConstantOverride("outline_size", 6);
        AccessibilityPresentation.ApplyToControl(_signalPrompt, _accessibility);
        _stopCard.AddChild(_signalPrompt);
        stop.AddChild(_stopCard);
    }

    private void ResetPrologueStop()
    {
        Array.Clear(_stopFired);
        _stopLeft = false;
        _stopSignalAt = -1;
        _replyAt = -1;
        HideSignalPrompt();
        if (GodotObject.IsInstanceValid(_stopSignal) && GodotObject.IsInstanceValid(_stopSignalLight))
            _stopSignal.Position = _stopSignalLight.Position = StopPoint(34f, 1.2f, 1.25f);
        if (_stopLamp is { } lamp && GodotObject.IsInstanceValid(lamp)) lamp.LightEnergy = StopLampEnergy;
        if (_stopLampFill is { } fill && GodotObject.IsInstanceValid(fill)) fill.LightEnergy = StopLampFillEnergy;
        if (_stopShelterFill is { } bounce && GodotObject.IsInstanceValid(bounce)) bounce.LightEnergy = StopShelterFillEnergy;
        if (_stopBulb is { } bulb) bulb.EmissionEnergyMultiplier = 2f;
        if (_stopBusLights is { } bus && GodotObject.IsInstanceValid(bus))
        {
            bus.Visible = true;
            bus.Position = StopPoint(StopRoadNear + 1.6f, 7f, .95f);
        }
        if (_stopCardLabel is { } card && GodotObject.IsInstanceValid(card)) card.Modulate = new Color(1, 1, 1, 0);
    }

    private bool StopOnce(int beat, bool condition)
    {
        if (_stopFired[beat] || !condition) return false;
        _stopFired[beat] = true;
        return true;
    }

    // ---- Every frame ---------------------------------------------------------------------------

    private void ProcessPrologueStop(FirstPersonController player, Vector2 feet, float progress, double t)
    {
        if (_stop is null || !GodotObject.IsInstanceValid(_stop)) return;
        var (along, _) = PrologueStopAxes();
        var u = (feet - new Vector2(PrologueTrailNodes[0].X, PrologueTrailNodes[0].Z)).Dot(along);

        // F00 already faces the road; on Continue the saved camera belongs to the player.
        // The card in the corner.
        if (_stopCardLabel is { } card && GodotObject.IsInstanceValid(card))
            card.Modulate = new Color(1, 1, 1, Mathf.SmoothStep(1.2f, 2.8f, (float)t) * (1f - Mathf.SmoothStep(8f, 10f, (float)t)));
        // The bus pulls away down the road and is gone.
        if (_stopBusLights is { Visible: true } bus && GodotObject.IsInstanceValid(bus))
        {
            var travelled = 7f + (float)(t * t) * .55f + (float)t * 2.2f;
            bus.Position = StopPoint(StopRoadNear + 1.6f, travelled, .95f);
            if (travelled > 150f) bus.Visible = false;
        }

        if (!_stopLeft)
        {
            _torchOverride = 0f; // under the lamp the torch stays in his pocket
            if (_stopLamp is { } lamp && GodotObject.IsInstanceValid(lamp))
            {
                // An old sodium lamp: steady, with a nervous buzz in it.
                var buzz = _accessibility.ReducedMotion ? 1f
                    : 1f - .08f * Mathf.Abs(Mathf.Sin((float)t * 31f)) - (Mathf.Sin((float)t * 2.3f) > .985f ? .5f : 0f);
                lamp.LightEnergy = StopLampEnergy * buzz;
            }
            // Babai's message. The one rule.
            if (StopOnce(1, t > 2.4))
                ShowNightmareThought("Бабай · 23:38\n«Жди у сторожки, там светло. Подъеду — посигналю».", 5);
            // After the voice: two short blinks of a torch between the trunks.
            if (StopOnce(3, _nightmareRecognitionAt > 0 && t >= _nightmareRecognitionAt + 4.5)) _stopSignalAt = t;
            if (StopOnce(4, _stopSignalAt >= 0 && t >= _stopSignalAt + 1.2))
                ShowNightmareThought("Два коротких. Мы так друг друга искали.", 3);
            if (u > StopLeaveAt) LeavePrologueStop(player);
        }
        else
        {
            if (StopOnce(7, progress >= PrologueRouteLength * .5f))
                ShowNightmareThought("У дороги был детский след. Этот — больше моего.", 3.5);
        }
        // He can test whether those are headlights. The returning light has crossed
        // the distance from the trees to the shoulder between two flashes.
        if (_stopSignal is { } signal && _stopSignalLight is { } signalLight && GodotObject.IsInstanceValid(signal))
        {
            var since = _stopSignalAt < 0 ? -1.0 : t - _stopSignalAt;
            var replying = _replyAt >= 0 ? t - _replyAt : -1;
            var pulse = since >= 0 && !_stopLeft && !_answeredSignal ? since % 7.0 : -1;
            var lit = _accessibility.ReducedMotion ? pulse is >= 0 and < 1.2 : pulse is >= 0 and < .34 or >= .78 and < 1.15;
            if (replying >= 0)
            {
                if (!_stopLeft && replying < 1.2)
                    _torchOverride = (_accessibility.ReducedMotion || replying is < .34 or >= .78) ? 1f : 0f;
                if (replying >= 1.45)
                {
                    lit = replying < 3.25;
                    if (StopOnce(10, true))
                    {
                        signal.Position = signalLight.Position = StopPoint(StopRoadFar + 1.1f, 1.2f, 1.25f);
                        UiFoley.PlayWorld(this, signal.GlobalPosition, "forest/brush_rustle", -8f, 18f, 2f);
                        ShowNightmareThought("Уже у дороги. Как он так быстро?..", 2.5);
                    }
                }
            }
            signal.Visible = signalLight.Visible = lit;
            var camera = GetViewport().GetCamera3D();
            var facing = since >= 1.2 && !_stopLeft && !_answeredSignal && !_writingSignal
                && camera is not null && (-camera.GlobalBasis.Z).Dot(camera.GlobalPosition.DirectionTo(signal.GlobalPosition)) > .77f;
            if (facing && _signalPrompt is { } prompt)
            {
                if (!prompt.Visible || prompt.GetMeta("binding", "").AsString() != player.InteractionHint)
                {
                    prompt.SetMeta("binding", player.InteractionHint);
                    prompt.Text = $"{player.InteractionHint}  Ответить фонариком";
                    prompt.Show();
                }
                if (!Input.IsActionPressed("interact")) _signalInputReleased = true;
                if (_signalInputReleased && Input.IsActionJustPressed("interact") && player.PeekRayTarget() is null)
                    _ = AnswerForestSignalAsync();
            }
            else HideSignalPrompt();
        }
        // Walking past lets Aidar notice it. Only the focused object action takes it.
        if (_stopMitten is { Visible: true } mitten && GodotObject.IsInstanceValid(mitten))
        {
            var gap = mitten.GlobalPosition.DistanceTo(player.GlobalPosition + Vector3.Up * 1.2f);
            if (StopOnce(9, gap < 4.2f))
                ShowNightmareThought("Одна варежка. А где вторая?", 3);
        }
    }

    private void HideSignalPrompt()
    {
        _signalInputReleased = false;
        if (_signalPrompt is { } prompt && GodotObject.IsInstanceValid(prompt) && prompt.Visible) prompt.Hide();
    }

    private async Task AnswerForestSignalAsync()
    {
        if (_writingSignal || _answeredSignal || _stopLeft || _bridge is not { } bridge
            || bridge.SessionIdentity is not { } session || bridge.ActiveSceneId != PrologueSceneId) return;
        _writingSignal = true;
        HideSignalPrompt();
        try
        {
            if (await bridge.DispatchWorldPropsAsync(new JsonArray(new JsonObject
                { ["propId"] = PrologueSignalKey, ["answered"] = true }))
                && GodotObject.IsInstanceValid(this) && IsInsideTree() && GodotObject.IsInstanceValid(bridge)
                && ReferenceEquals(session, bridge.SessionIdentity) && bridge.ActiveSceneId == PrologueSceneId)
            {
                _answeredSignal = true;
                _replyAt = _nightmareElapsed;
            }
        }
        catch (Exception error) { GD.PushWarning($"Could not answer the forest light: {error.Message}"); }
        finally { _writingSignal = false; }
    }

    private void ProjectStopObjects(RuntimeBridge bridge, bool inPrologue)
    {
        var props = inPrologue ? bridge.SelectWorldProps() : default;
        _answeredSignal = inPrologue && YardMechanism.Flag(props, PrologueSignalKey, "answered");
        if (_stopMittenTarget is not { } target || !GodotObject.IsInstanceValid(target)) return;
        if (_stopMitten is not { } mitten || !GodotObject.IsInstanceValid(mitten))
        {
            target.SetPresentationEnabled(false);
            return;
        }
        var available = inPrologue && !YardMechanism.Flag(props, PrologueMittenKey, "taken");
        mitten.Visible = available;
        target.SetPresentationEnabled(available && !_takingMitten);
    }

    private async Task TakePrologueMittenAsync()
    {
        if (_takingMitten || _nightmareClimax || _bridge is not { } bridge ||
            bridge.ActiveSceneId != PrologueSceneId || bridge.SessionIdentity is not { } session ||
            _stopMittenTarget is not { } target || !target.IsPresentationCurrent(bridge, session)) return;
        _takingMitten = true;
        target.SetPresentationEnabled(false);
        try
        {
            var committed = await bridge.DispatchWorldPropsAsync(new JsonArray(new JsonObject
            {
                ["propId"] = PrologueMittenKey, ["taken"] = true
            }));
            if (!GodotObject.IsInstanceValid(target) ||
                !target.IsPresentationCurrent(bridge, session) || bridge.ActiveSceneId != PrologueSceneId) return;
            if (!committed)
            {
                ShowNightmareThought("Варежка осталась на палке.", 2);
                return;
            }
            UiFoley.PlayWorld(this, target.GlobalPosition, "forest/brush_rustle", -14f, 8f, 2f);
            ShowNightmareThought("Варежка совсем сухая. Спрошу у бабая, чья она.", 3.5);
        }
        catch (Exception error)
        {
            GD.PushWarning($"Could not take the prologue mitten: {error.Message}");
        }
        finally
        {
            _takingMitten = false;
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && GodotObject.IsInstanceValid(bridge))
                ProjectStopObjects(bridge, bridge.SessionIdentity is not null && bridge.ActiveSceneId == PrologueSceneId);
        }
    }

    private void LeavePrologueStop(FirstPersonController player)
    {
        _stopLeft = true;
        _torchOverride = null;
        UiFoley.PlayWorld(this, player.GlobalPosition, "ui_click", -8f, 6f, 1f);
        // Behind him the lamp gives one bright stutter and dies.
        if (_stopLamp is { } lamp && GodotObject.IsInstanceValid(lamp))
        {
            var dying = lamp.CreateTween();
            if (!_accessibility.ReducedMotion)
            {
                dying.TweenProperty(lamp, "light_energy", StopLampEnergy * 1.5f, .08);
                dying.TweenProperty(lamp, "light_energy", StopLampEnergy * .2f, .12);
                dying.TweenProperty(lamp, "light_energy", StopLampEnergy * .9f, .1);
            }
            dying.TweenProperty(lamp, "light_energy", 0f, .5);
        }
        foreach (var glow in new[] { _stopLampFill, _stopShelterFill })
            if (glow is not null && GodotObject.IsInstanceValid(glow))
                glow.CreateTween().TweenProperty(glow, "light_energy", 0f, .8);
        if (_stopBulb is { } bulb)
            GetTree().CreateTimer(.8).Timeout += () => bulb.EmissionEnergyMultiplier = 0f;
        _stopFired[1] = true;
    }
}
