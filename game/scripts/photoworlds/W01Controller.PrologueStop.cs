using System;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Prologue, written as a story (author request 2026-11-13).
///
/// «Остановка». The last bus leaves Aidar at the turn to Kara-Urman, under the
/// only lamp for kilometres. Babai's message gives him one rule: wait at the
/// stop, not a step from it. Then a voice he knows cannot be there calls his
/// name from the trees across the road, and a torch blinks twice — the signal
/// two boys once used. The lamp does not go out until he breaks the
/// rule. The prints he follows are a child's. They do not stay a child's.
/// On a stick in the snow hangs a small blue mitten, dry, as if hung a minute
/// ago; he pockets it without knowing why. Where the prints end, something
/// that has been walking in front of him all along turns round.
///
/// He wakes in babai's car: the bus was late, he fell asleep on the way from
/// the stop. A dream, then — until, at the gate, he puts his hand in his pocket.
///
/// This file owns the stop itself and the opening beats. Presentation only.
/// </summary>
public partial class W01Controller
{
    // ---- Staging, in metres along (u) and across (v) the first stretch of the trail -----------
    private const float StopRoadNear = 2.4f;
    private const float StopRoadFar = 8.4f;
    private const float StopLeaveAt = 8.9f;       // past the far edge of the road: the rule is broken
    private const float StopPrintsFrom = 9.6f;    // the child's prints begin where the road ends
    private const float StopLampEnergy = 5.2f;
    private const float StopRoadReach = 62f;      // how far the road is drawn each way
    private const float StopRoadWalk = 19f;       // how far along it he can go
    private const double StopLampPatience = 55;   // seconds before the lamp gives up by itself

    /// <summary>Set when he pockets the mitten; the ride home asks about it at the gate.</summary>
    internal static bool PrologueMittenTaken { get; private set; }

    private Node3D? _stop;
    private SpotLight3D? _stopLamp;
    private OmniLight3D? _stopLampFill, _stopShelterFill;
    private StandardMaterial3D? _stopBulb;
    private Node3D? _stopBusLights;
    private MeshInstance3D? _stopSignal;
    private OmniLight3D? _stopSignalLight;
    private Node3D? _stopMitten;
    private CanvasLayer? _stopCard;
    private Label? _stopCardLabel;
    private readonly bool[] _stopFired = new bool[12];
    private bool _stopLeft;
    private double _stopLeftAt;
    private double _stopSignalAt = -1;
    private Vector3 _stopPrintLocal;

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
            surface.Begin(Mesh.PrimitiveType.Triangles);
            void Strip(float from, float to, float lift, Color colour)
            {
                var rows = Mathf.Max(1, Mathf.CeilToInt((to - from) / 1f));
                for (var row = 0; row < rows; row++)
                for (var v = -StopRoadReach; v < StopRoadReach; v += 1.25f)
                {
                    var u0 = Mathf.Lerp(from, to, (float)row / rows); var u1 = Mathf.Lerp(from, to, (row + 1f) / rows);
                    var a = StopPoint(u0, v, lift); var b = StopPoint(u1, v, lift);
                    var c = StopPoint(u1, v + 1.25f, lift); var d = StopPoint(u0, v + 1.25f, lift);
                    foreach (var point in new[] { a, b, c, a, c, d })
                    {
                        surface.SetNormal(Vector3.Up);
                        surface.SetColor(colour);
                        surface.AddVertex(point);
                    }
                }
            }
            Strip(StopRoadNear, StopRoadFar, .05f, new Color(.6f, .65f, .74f));
            Strip(StopRoadNear + 1.3f, StopRoadNear + 1.8f, .065f, new Color(.42f, .47f, .57f));
            Strip(StopRoadFar - 1.8f, StopRoadFar - 1.3f, .065f, new Color(.42f, .47f, .57f));
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
                Name = "Road", Mesh = surface.Commit(),
                MaterialOverride = new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true, Roughness = 1f, MetallicSpecular = 0f,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }

        // The shelter: a concrete box open to the road, a bench, snow on the roof.
        var shelter = new Node3D { Name = "Shelter", Position = StopPoint(-1.9f, 0f) };
        shelter.Basis = Basis.LookingAt(-along3, Vector3.Up); // local +Z looks across the road at the forest
        stop.AddChild(shelter);
        var concrete = PainterlyMaterialLibrary.ForColor("7f8a8c", "plaster");
        var paint = PainterlyMaterialLibrary.ForColor("5f8088", "plaster");
        var wood = PainterlyMaterialLibrary.ForColor("6a5a48", "wood");
        var snowMaterial = PainterlyMaterialLibrary.ForColor("e8edf2", "snow_roof");
        MeshInstance3D Piece(Node3D parent, string name, Vector3 size, Vector3 at, Material material)
        {
            var piece = new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size }, Position = at, MaterialOverride = material };
            parent.AddChild(piece);
            return piece;
        }
        Piece(shelter, "BackWall", new(3.5f, 2.35f, .14f), new(0f, 1.17f, -.95f), paint);
        Piece(shelter, "BackWallBand", new(3.52f, .5f, .15f), new(0f, .25f, -.95f), concrete);
        foreach (var x in new[] { -1.68f, 1.68f })
            Piece(shelter, "Side" + x, new(.14f, 2.35f, 1.6f), new(x, 1.17f, -.2f), paint);
        Piece(shelter, "Roof", new(4.0f, .12f, 2.3f), new(0f, 2.41f, -.05f), concrete);
        Piece(shelter, "RoofSnow", new(3.9f, .17f, 2.2f), new(0f, 2.55f, -.05f), snowMaterial);
        Piece(shelter, "BenchSeat", new(2.5f, .06f, .4f), new(0f, .46f, -.6f), wood);
        Piece(shelter, "BenchBack", new(2.5f, .28f, .05f), new(0f, .78f, -.82f), wood);
        foreach (var x in new[] { -1.05f, 1.05f })
            Piece(shelter, "BenchLeg" + x, new(.07f, .46f, .36f), new(x, .23f, -.6f), concrete);
        Piece(shelter, "BenchSnow", new(.9f, .05f, .34f), new(.75f, .515f, -.6f), snowMaterial);
        // The print somebody left on the bench: the forest he is about to walk into.
        const string printPath = "res://assets/textures/photobook/p-forest-front-v2.png";
        if (ResourceLoader.Exists(printPath))
        {
            var print = new MeshInstance3D
            {
                Name = "LeftPhotograph",
                Mesh = new QuadMesh { Size = new Vector2(.15f, .1f), Orientation = PlaneMesh.OrientationEnum.Y },
                Position = new Vector3(-.55f, .496f, -.56f), RotationDegrees = new Vector3(0f, 17f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoTexture = ResourceLoader.Load<Texture2D>(printPath), Roughness = 1f },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            shelter.AddChild(print);
        }
        _stopPrintLocal = StopPoint(-2.45f, -.55f, .5f);
        _stopShelterFill = new OmniLight3D
        {
            Name = "ShelterBounce", Position = new Vector3(0f, 1.5f, .35f), LightColor = new Color(1f, .72f, .42f),
            LightEnergy = .55f, OmniRange = 3.2f, ShadowEnabled = false, LightSpecular = 0f
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
            AlbedoColor = new Color(1f, .8f, .5f), EmissionEnabled = true,
            Emission = new Color(1f, .68f, .3f), EmissionEnergyMultiplier = 4f
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
            LightColor = new Color(1f, .7f, .36f), LightEnergy = StopLampEnergy,
            SpotRange = 13f, SpotAngle = 58f, SpotAngleAttenuation = .9f, SpotAttenuation = .7f,
            ShadowEnabled = true, ShadowBias = .06f, ShadowBlur = 1.6f, LightSpecular = .2f,
            DistanceFadeEnabled = true, DistanceFadeBegin = 70f, DistanceFadeShadow = 36f, DistanceFadeLength = 14f
        };
        stop.AddChild(_stopLamp);
        _stopLampFill = new OmniLight3D
        {
            Name = "StopLampFill", Position = head - Vector3.Up * .5f, LightColor = new Color(1f, .7f, .4f),
            LightEnergy = .5f, OmniRange = 6f, ShadowEnabled = false, LightSpecular = 0f
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
        var mittenAt = PrologueRoutePoint(PrologueRouteLength * .71f, .85f);
        var stick = new MeshInstance3D
        {
            Name = "MittenStick", Mesh = new CylinderMesh { TopRadius = .012f, BottomRadius = .02f, Height = 1.5f, RadialSegments = 6 },
            Position = mittenAt + Vector3.Up * .72f, RotationDegrees = new Vector3(6f, 0f, -8f),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("3b3129", "wood_bark")
        };
        stop.AddChild(stick);
        _stopMitten = PrologueDeepForest.AddMitten(stop, mittenAt + new Vector3(.1f, 1.36f, 0f), new Vector3(0f, 30f, 172f));

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
        stop.AddChild(_stopCard);
    }

    private void ResetPrologueStop(bool newWalk)
    {
        Array.Clear(_stopFired);
        _stopLeft = false;
        _stopLeftAt = 0;
        _stopSignalAt = -1;
        if (newWalk) PrologueMittenTaken = false;
        if (_stopLamp is { } lamp && GodotObject.IsInstanceValid(lamp)) lamp.LightEnergy = StopLampEnergy;
        if (_stopLampFill is { } fill && GodotObject.IsInstanceValid(fill)) fill.LightEnergy = .5f;
        if (_stopShelterFill is { } bounce && GodotObject.IsInstanceValid(bounce)) bounce.LightEnergy = .55f;
        if (_stopBulb is { } bulb) bulb.EmissionEnergyMultiplier = 4f;
        if (_stopBusLights is { } bus && GodotObject.IsInstanceValid(bus))
        {
            bus.Visible = true;
            bus.Position = StopPoint(StopRoadNear + 1.6f, 7f, .95f);
        }
        if (_stopMitten is { } mitten && GodotObject.IsInstanceValid(mitten)) mitten.Visible = true;
        if (_stopCardLabel is { } card && GodotObject.IsInstanceValid(card)) card.Modulate = new Color(1, 1, 1, 0);
    }

    private bool StopOnce(int beat, bool condition)
    {
        if (_stopFired[beat] || !condition) return false;
        _stopFired[beat] = true;
        return true;
    }

    // ---- Every frame ---------------------------------------------------------------------------

    private void ProcessPrologueStop(FirstPersonController player, Vector2 feet, float progress, double t, float delta)
    {
        if (_stop is null || !GodotObject.IsInstanceValid(_stop)) return;
        var (along, across) = PrologueStopAxes();
        var u = (feet - new Vector2(PrologueTrailNodes[0].X, PrologueTrailNodes[0].Z)).Dot(along);

        // He is put down facing the road and the forest beyond it.
        if (StopOnce(0, true))
        {
            var ahead = ToGlobal(StopPoint(12f, 0f)) - ToGlobal(StopPoint(0f, 0f));
            player.Rotation = new Vector3(0f, Mathf.Atan2(-ahead.X, -ahead.Z), 0f);
        }
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
            if (StopOnce(1, t > 5.2))
                ShowNightmareThought("Голосовое от бабая: «Улым, я выехал. Жди на остановке.\nС остановки — ни шагу. Слышишь? Ни шагу».", 7.5);
            // The print on the bench.
            var local = ToLocal(player.GlobalPosition);
            if (StopOnce(2, t > 13.5 && _prologueCaptionRemaining <= 0
                    && new Vector2(local.X - _stopPrintLocal.X, local.Z - _stopPrintLocal.Z).Length() < 2.3f))
                ShowNightmareThought("На скамейке кто-то оставил снимок. Лес. Этот самый, через дорогу.", 4.5);
            // After the voice: two short blinks of a torch between the trunks.
            if (StopOnce(3, _nightmareRecognitionAt > 0 && t >= _nightmareRecognitionAt + 4.5)) _stopSignalAt = t;
            if (StopOnce(4, _stopSignalAt >= 0 && t >= _stopSignalAt + 2.2))
                ShowNightmareThought("Два коротких. Наш с Маратом сигнал. Из детства.", 4.5);
            if (StopOnce(5, _stopSignalAt >= 0 && t >= _stopSignalAt + 8.2))
                ShowNightmareThought("Он там, за дорогой. Я только посмотрю — и сразу назад.", 4.5);
            if (u > StopLeaveAt) LeavePrologueStop(player, t, waited: false);
            else if (t > StopLampPatience) LeavePrologueStop(player, t, waited: true);
        }
        else
        {
            if (StopOnce(6, t >= _stopLeftAt + 3.4 && progress >= StopPrintsFrom - 1f))
                ShowNightmareThought("Следы. Маленькие, детские. Уходят от дороги прямо в лес.", 4.5);
            if (StopOnce(7, progress >= PrologueRouteLength * .5f))
                ShowNightmareThought("Следы стали крупнее. Тот, кто шёл, будто вырос на ходу.", 4.5);
            if (StopOnce(8, progress >= PrologueRouteLength * .86f))
                ShowNightmareThought("Это уже не детский шаг. И не мой.", 4);
        }
        // The answering torch blinks: on, off, on.
        if (_stopSignal is { } signal && _stopSignalLight is { } signalLight && GodotObject.IsInstanceValid(signal))
        {
            var since = _stopSignalAt < 0 ? -1.0 : t - _stopSignalAt;
            var lit = since is >= 0 and < .28 or >= .62 and < .9;
            signal.Visible = signalLight.Visible = lit;
        }
        // The mitten: seen, then taken.
        if (_stopMitten is { Visible: true } mitten && GodotObject.IsInstanceValid(mitten))
        {
            var gap = mitten.GlobalPosition.DistanceTo(player.GlobalPosition + Vector3.Up * 1.2f);
            if (StopOnce(9, gap < 4.2f))
                ShowNightmareThought("Синяя детская варежка на палке. Сухая, без снега — её повесили только что.", 4.5);
            if (StopOnce(10, gap < 2.7f))
            {
                mitten.Visible = false;
                PrologueMittenTaken = true;
                UiFoley.PlayWorld(this, mitten.GlobalPosition, "forest/brush_rustle", -14f, 8f, 2f);
                ShowNightmareThought("Сунул в карман. Сам не знаю зачем.", 3.5);
            }
        }
    }

    private void LeavePrologueStop(FirstPersonController player, double t, bool waited)
    {
        _stopLeft = true;
        _stopLeftAt = t;
        _torchOverride = null;
        UiFoley.PlayWorld(this, player.GlobalPosition, "ui_click", -8f, 6f, 1f);
        // Behind him the lamp gives one bright stutter and dies.
        if (_stopLamp is { } lamp && GodotObject.IsInstanceValid(lamp))
        {
            var dying = lamp.CreateTween();
            dying.TweenProperty(lamp, "light_energy", StopLampEnergy * 1.5f, .08);
            dying.TweenProperty(lamp, "light_energy", StopLampEnergy * .2f, .12);
            dying.TweenProperty(lamp, "light_energy", StopLampEnergy * .9f, .1);
            dying.TweenProperty(lamp, "light_energy", 0f, .5);
        }
        foreach (var glow in new[] { _stopLampFill, _stopShelterFill })
            if (glow is not null && GodotObject.IsInstanceValid(glow))
                glow.CreateTween().TweenProperty(glow, "light_energy", 0f, .8);
        if (_stopBulb is { } bulb)
            GetTree().CreateTimer(.8).Timeout += () => bulb.EmissionEnergyMultiplier = 0f;
        var warned = _stopFired[1];
        _stopFired[1] = true;
        ShowNightmareThought(waited ? "Лампа погасла. Темно. В рюкзаке был фонарик."
            : warned ? "«Ни шагу», — сказал бабай. За спиной погасла лампа."
            : "За спиной погасла лампа. Остановки больше не видно.", 4.5);
    }
}
