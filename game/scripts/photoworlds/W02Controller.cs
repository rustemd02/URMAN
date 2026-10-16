using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// W02 «Мостки» — the summer bank inside the water print. One river lies west
/// of the walking bank: dry piers, a bay platform, a reflection outcrop, the
/// reed curtain the place grew over, the ruined old approach, the far pier
/// where the water answers, and the summer meadow that opens back to the
/// winter bank. Presentation only — every knowledge commitment stays in the
/// compiled graph.
/// </summary>
public partial class W02Controller : Node3D
{
    private const string RiverSceneId = "urman.fullgame:scene/pw-river";
    private const string ReflectionInteractionId = "urman.fullgame:interaction/pw-water-reflection";
    private const string BypassInteractionId = "urman.fullgame:interaction/pw-water-bypass";
    private const string OldApproachInteractionId = "urman.fullgame:interaction/pw-water-old-approach";
    private const string EncounterInteractionId = "urman.fullgame:interaction/pw-water-encounter";
    private const string DampPrintInteractionId = "urman.fullgame:interaction/pw-water-damp-print";
    private const string ReturnInteractionId = "urman.fullgame:interaction/pw-water-return";
    private const string ReflectionLabelTextId = "urman.fullgame:text/pw-label-water-reflection";
    private const string BypassLabelTextId = "urman.fullgame:text/pw-label-water-bypass";
    private const string OldApproachLabelTextId = "urman.fullgame:text/pw-label-water-old-approach";
    private const string EncounterLabelTextId = "urman.fullgame:text/pw-label-water-encounter";
    private const string DampPrintLabelTextId = "urman.fullgame:text/pw-label-water-damp-print";
    private const string ReturnLabelTextId = "urman.fullgame:text/pw-label-water-return";

    // Authored placement from the W02 passport (§2). Y is the walking surface.
    private static readonly Vector3[] RouteNodes =
    [
        new(0f, 1.2f, 0f),      // R00 photo place / spawn
        new(0f, 1.2f, 14f),     // R01 dry piers, vista
        new(-16f, 0.5f, 25f),   // R02 quiet bay
        new(10f, 1.2f, 30f),    // R03 reflection window
        new(25f, 1f, 42f),      // R04 reed turn
        new(40f, 1.3f, 63f),    // R05 old approach
        new(48f, 1.3f, 83f),    // R06 far piers, encounter
        new(66f, 2f, 103f),     // R07 summer bank
        new(78f, 2f, 117f)      // R08 opening to the source place
    ];

    private static readonly int[] MainRoute = [0, 1, 3, 4, 5, 6, 7, 8];
    private const int SpurA = 1;
    private const int SpurB = 2;

    private const float WaterLevel = 0.15f;
    private const float WaterBed = -1.9f;
    private const float FarBank = 4.2f;
    private const float ShoreReach = 3f;
    private const float RenderStep = 1.5f;
    private const float TerrainMinX = -72f;
    private const float TerrainMaxX = 96f;
    private const float TerrainMinZ = -28f;
    private const float TerrainMaxZ = 148f;

    private RuntimeBridge? _bridge;
    private Node3D? _world;
    private readonly List<StaticBody3D> _worldBodies = [];
    private readonly List<InteractionTarget> _targets = [];
    private InteractionTarget? _encounterTarget;
    private Node3D? _reedCurtainLeft;
    private Node3D? _reedCurtainRight;
    private StaticBody3D? _reedCurtainBody;
    private Node3D? _suAnasy;
    private MeshInstance3D? _looseBoard;
    private MeshInstance3D? _boardMark;
    private readonly List<MeshInstance3D> _encounterRings = [];
    private bool _boardLifted;
    private bool _encounterWasResolved;
    private bool _reedCurtainOpened;
    private bool _firstProjection = true;
    private float _suAnasyTargetY = -1.35f;
    private float _suAnasyY = -1.35f;
    private float _ringClock;

    public override void _Ready()
    {
        AddToGroup("w02_presentation");
        _world = new Node3D { Name = "W02World", Visible = true };
        AddChild(_world);
        BuildTerrain();
        BuildWater();
        BuildWalkingRibbon();
        BuildDryBoardwalk();
        BuildViewPlatform();
        BuildBaySpur();
        BuildReflectionOutcrop();
        BuildReedTurn();
        BuildOldApproach();
        BuildFarPier();
        BuildSummerBank();
        BuildBankRushes();
        BuildFieldDressing();
        BuildWaterAnswer();
        BuildExitStand();
        TryBindRuntimeBridge();
        ApplyProjection();
    }

    public override void _Process(double delta)
    {
        TryBindRuntimeBridge();
        var dt = (float)delta;

        // The water answers only while the answer is live; a restored save snaps.
        if (_suAnasy is { } presence && GodotObject.IsInstanceValid(presence))
        {
            _suAnasyY = Mathf.MoveToward(_suAnasyY, _suAnasyTargetY, dt * 0.55f);
            var bob = Mathf.Sin((float)Time.GetTicksMsec() * 0.0004f) * 0.045f;
            presence.Position = new Vector3(presence.Position.X, _suAnasyY + bob, presence.Position.Z);
        }

        _ringClock += dt;
        for (var index = 0; index < _encounterRings.Count; index++)
        {
            var ring = _encounterRings[index];
            if (!GodotObject.IsInstanceValid(ring)) continue;
            var cycle = Mathf.PosMod(_ringClock * 0.22f + index * 0.37f, 1f);
            var scale = Mathf.Lerp(0.5f, 2.6f, cycle);
            ring.Scale = new Vector3(scale, 1f, scale);
            if (ring.MaterialOverride is StandardMaterial3D material)
                material.AlbedoColor = new Color(material.AlbedoColor, (1f - cycle) * 0.30f);
        }
    }

    public override void _ExitTree()
    {
        SetProcess(false);
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= ApplyProjection;
        _bridge = null;
    }

    private void TryBindRuntimeBridge()
    {
        if (_bridge is { } current && GodotObject.IsInstanceValid(current))
            return;

        if (!IsInsideTree()
            || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge)
            return;

        _bridge = bridge;
        bridge.RuntimeStateChanged += ApplyProjection;
        ApplyProjection();
    }

    private void ApplyProjection()
    {
        // Without a bridge (the visual capture harness) the authored building
        // stays visible exactly as built.
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge))
            return;

        var projection = bridge.CurrentW02WaterProjection();
        var visible = projection is not null && projection.ActiveSceneId == RiverSceneId;
        SetWorldPresentationVisible(visible);
        foreach (var target in _targets)
            if (GodotObject.IsInstanceValid(target)) target.SetPresentationEnabled(visible);
        foreach (var ring in _encounterRings)
            if (GodotObject.IsInstanceValid(ring)) ring.Visible = visible && projection?.EncounterResolved != true;

        if (projection is null)
        {
            _firstProjection = false;
            return;
        }

        EnsureTargets(bridge);

        // The reeds answer the authored fact once; a restored save stands open.
        if (projection.ReedOpened && !_reedCurtainOpened)
        {
            _reedCurtainOpened = true;
            PartReedCurtain(animated: !_firstProjection);
        }

        if (_encounterTarget is { } encounterTarget)
        {
            encounterTarget.PresentationGate = projection.EncounterResolved ? static () => false : null;
            encounterTarget.PresentationGateHint = string.Empty;
        }

        // The loose board: the first press is local, the second commits once.
        if (projection.EncounterResolved)
        {
            if (!_encounterWasResolved)
            {
                _encounterWasResolved = true;
                _boardLifted = false;
                SettleLooseBoard(animated: !_firstProjection);
                _suAnasyTargetY = 0.15f;
                if (_firstProjection) _suAnasyY = _suAnasyTargetY;
            }

            UpdateEncounterPrompt(encounterResolved: true);
        }
        else
        {
            _encounterWasResolved = false;
            _suAnasyTargetY = -1.35f;
            if (_firstProjection) _suAnasyY = _suAnasyTargetY;
            UpdateEncounterPrompt(encounterResolved: false);
        }

        _firstProjection = false;
    }

    private void SetWorldPresentationVisible(bool visible)
    {
        if (_world is { } world && GodotObject.IsInstanceValid(world)) world.Visible = visible;
        foreach (var body in _worldBodies)
            if (GodotObject.IsInstanceValid(body)) body.CollisionLayer = visible ? 1u : 0u;
    }

    private StaticBody3D TrackBody(StaticBody3D body)
    {
        _worldBodies.Add(body);
        return body;
    }

    // ------------------------------------------------------------------
    // Terrain
    // ------------------------------------------------------------------

    private static float Smooth(float value)
    {
        var t = Mathf.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Relief(float x, float z) =>
        0.30f * MathF.Sin(x * 0.213f + z * 0.131f)
        + 0.26f * MathF.Sin(x * 0.071f - z * 0.093f + 1.7f)
        + 0.15f * MathF.Sin(x * 0.53f + z * 0.41f + 4.1f)
        + 0.12f * MathF.Sin(x * 0.017f + z * 0.023f - 2.4f);

    private readonly record struct RouteSample(float Distance, float Side, float Height);

    private static RouteSample SampleRoute(float x, float z)
    {
        var best = new RouteSample(float.MaxValue, 0f, 0f);
        for (var leg = 0; leg + 1 < MainRoute.Length; leg++)
        {
            var sample = SampleSegment(RouteNodes[MainRoute[leg]], RouteNodes[MainRoute[leg + 1]], x, z);
            if (sample.Distance < best.Distance) best = sample;
        }

        return best;
    }

    private static RouteSample SampleSpur(float x, float z) =>
        SampleSegment(RouteNodes[SpurA], RouteNodes[SpurB], x, z);

    private static RouteSample SampleSegment(Vector3 a, Vector3 b, float x, float z)
    {
        var dx = b.X - a.X;
        var dz = b.Z - a.Z;
        var lengthSquared = dx * dx + dz * dz;
        var t = lengthSquared <= 0.0001f
            ? 0f
            : Mathf.Clamp(((x - a.X) * dx + (z - a.Z) * dz) / lengthSquared, 0f, 1f);
        var px = a.X + dx * t;
        var pz = a.Z + dz * t;
        var distance = Mathf.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
        // Right of travel (positive side) is the water side of this route.
        var side = dx * (z - a.Z) - dz * (x - a.X);
        return new RouteSample(distance, side, Mathf.Lerp(a.Y, b.Y, t));
    }

    /// <summary>Authored summer bank height at one world point.</summary>
    private static float TerrainHeight(float x, float z)
    {
        var route = SampleRoute(x, z);
        var d = route.Distance;
        var height = route.Height - 0.12f * d;
        if (d > ShoreReach)
        {
            if (route.Side >= 0f)
            {
                // Water side: bank slope, river bed, then the far wooded shore.
                if (d <= 6f)
                    height = Mathf.Lerp(route.Height - 0.36f, WaterBed, Smooth((d - ShoreReach) / 3f));
                else if (d <= 46f)
                    height = WaterBed + Relief(x, z) * 0.35f;
                else if (d <= 66f)
                    height = Mathf.Lerp(WaterBed + 0.3f, FarBank, Smooth((d - 46f) / 20f))
                        + Relief(x, z) * 0.5f;
                else
                    height = FarBank + Relief(x, z) * 0.85f;
            }
            else
            {
                // Land side: the walking bank rises to the summer meadow.
                var rise = Smooth((d - ShoreReach) / 33f);
                var reliefWeight = Smooth((d - 4f) / 8f);
                height = route.Height - 0.36f + 2.9f * rise + Relief(x, z) * 0.32f * reliefWeight;
            }
        }

        // The bay spur keeps its own ledge over the quiet water.
        var spur = SampleSpur(x, z);
        var spurReach = Smooth((spur.Distance - 1.2f) / 7.5f);
        if (spurReach < 1f)
        {
            var ledge = spur.Height - 0.10f * spur.Distance;
            height = Mathf.Lerp(height, ledge, (1f - spurReach) * (1f - spurReach));
        }

        return height;
    }

    private static Vector3 AnalyticNormal(float x, float z)
    {
        const float e = 1.2f;
        var dx = (TerrainHeight(x + e, z) - TerrainHeight(x - e, z)) / (2f * e);
        var dz = (TerrainHeight(x, z + e) - TerrainHeight(x, z - e)) / (2f * e);
        return new Vector3(-dx, 1f, -dz).Normalized();
    }

    private void BuildTerrain()
    {
        var columns = Mathf.CeilToInt((TerrainMaxX - TerrainMinX) / RenderStep) + 1;
        var rows = Mathf.CeilToInt((TerrainMaxZ - TerrainMinZ) / RenderStep) + 1;
        var heights = new float[columns * rows];
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
            heights[row * columns + column] =
                TerrainHeight(TerrainMinX + column * RenderStep, TerrainMinZ + row * RenderStep);

        Vector3 Point(int column, int row) => new(
            TerrainMinX + column * RenderStep,
            heights[row * columns + column],
            TerrainMinZ + row * RenderStep);

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var faces = new List<Vector3>((columns - 1) * (rows - 1) * 6);
        void Emit(Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = AnalyticNormal(a.X, a.Z);
            surface.SetNormal(normal);
            surface.SetUV(new Vector2(a.X, a.Z) * 0.06f);
            surface.AddVertex(a);
            surface.SetNormal(normal);
            surface.SetUV(new Vector2(b.X, b.Z) * 0.06f);
            surface.AddVertex(b);
            surface.SetNormal(normal);
            surface.SetUV(new Vector2(c.X, c.Z) * 0.06f);
            surface.AddVertex(c);
            faces.Add(a);
            faces.Add(b);
            faces.Add(c);
        }

        for (var row = 0; row + 1 < rows; row++)
        for (var column = 0; column + 1 < columns; column++)
        {
            // Upward-facing quads keep the engine's clockwise front-face order
            // established by the shipped interior lining: (a, d, b), (a, c, d).
            var a = Point(column, row);
            var b = Point(column, row + 1);
            var c = Point(column + 1, row);
            var d = Point(column + 1, row + 1);
            Emit(a, d, b);
            Emit(a, c, d);
        }

        var terrain = new MeshInstance3D
        {
            Name = "W02Terrain",
            Mesh = surface.Commit(),
            MaterialOverride = Snowless("6f7f52", "grass")
        };
        terrain.SetMeta("presentationRole", "walking summer bank; one river west of the route");
        _world!.AddChild(terrain);

        var body = TrackBody(new StaticBody3D
        {
            Name = "W02TerrainContact",
            CollisionLayer = 1u,
            CollisionMask = 1u
        });
        _world.AddChild(body);
        body.AddChild(new CollisionShape3D
        {
            Name = "W02TerrainTriangles",
            Shape = new ConcavePolygonShape3D
            {
                BackfaceCollision = true,
                Data = faces.ToArray()
            }
        });
        GD.Print($"w02-terrain: {columns}x{rows} rendered, {faces.Count / 3} collision triangles");
    }

    private void BuildWater()
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = WaterShaderCode } };
        var water = new MeshInstance3D
        {
            Name = "W02Water",
            Position = new Vector3(12f, WaterLevel, 60f),
            Mesh = new PlaneMesh { Size = new Vector2(340f, 340f), SubdivideWidth = 2, SubdivideDepth = 2 },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        water.SetMeta("presentationRole", "jade river; calm shelf tint through fresnel");
        _world!.AddChild(water);
    }

    private const string WaterShaderCode = """
        shader_type spatial;
        render_mode blend_mix, cull_disabled, specular_schlick_ggx;

        uniform vec3 deep_color : source_color = vec3(0.14, 0.3, 0.28);
        uniform vec3 shelf_color : source_color = vec3(0.34, 0.54, 0.46);
        uniform vec3 sky_color : source_color = vec3(0.64, 0.78, 0.8);
        uniform float alpha_base : hint_range(0.0, 1.0) = 0.62;
        uniform float alpha_fresnel : hint_range(0.0, 1.0) = 0.34;
        uniform float flow_speed : hint_range(0.0, 0.5) = 0.035;

        varying vec3 world_position;

        float water_hash(vec2 p) {
            return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
        }

        float water_noise(vec2 p) {
            vec2 cell = floor(p);
            vec2 local = fract(p);
            local = local * local * (3.0 - 2.0 * local);
            return mix(
                mix(water_hash(cell), water_hash(cell + vec2(1.0, 0.0)), local.x),
                mix(water_hash(cell + vec2(0.0, 1.0)), water_hash(cell + vec2(1.0, 1.0)), local.x),
                local.y);
        }

        void vertex() {
            world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
        }

        void fragment() {
            vec2 p = world_position.xz;
            float t = TIME * flow_speed;
            float near_ripple = water_noise(p * 0.55 + vec2(t * 0.9, t * 0.35));
            float far_ripple = water_noise(p * 0.17 - vec2(t * 0.5, t * 0.22));
            float fine = water_noise(p * 1.7 + vec2(t * 1.6, -t * 0.8));
            vec3 ripple_normal = normalize(vec3(
                (near_ripple - 0.5) * 0.22 + (fine - 0.5) * 0.10,
                1.0,
                (far_ripple - 0.5) * 0.26 + (fine - 0.5) * 0.10));
            NORMAL = normalize(mix(NORMAL, ripple_normal, 0.72));

            float fresnel = pow(1.0 - clamp(dot(NORMAL, normalize(VIEW)), 0.0, 1.0), 3.0);
            vec3 color = mix(deep_color, shelf_color, 0.35 + 0.25 * far_ripple);
            color = mix(color, sky_color, fresnel * 0.55);
            ALBEDO = color;
            ALPHA = clamp(alpha_base + fresnel * alpha_fresnel, 0.0, 0.94);
            ROUGHNESS = 0.16;
            SPECULAR = 0.5;
        }
        """;

    // ------------------------------------------------------------------
    // Wooden structures
    // ------------------------------------------------------------------

    /// <summary>
    /// W02 is the summer inside the water print. The shared painterly library
    /// carries the campaign's winter snow; asking it for the same finish
    /// "without snow" keeps this bank green while every other scene keeps its
    /// own weather, and never mutates a material another scene is using.
    /// </summary>
    private static Material Snowless(string htmlColor, string surface, bool sheltered = false)
    {
        var source = PainterlyMaterialLibrary.ForColor(htmlColor, surface, sheltered);
        return source is ShaderMaterial shader ? PainterlyMaterialLibrary.WithoutSnow(shader) : source;
    }

    private static Material DeckMaterial() => Snowless("8a6d47", "wood_cut");
    private static Material WetPostMaterial() => Snowless("5b4a36", "wood_cut");
    private static Material OldPlankMaterial() => Snowless("6d6552", "wood_cut");

    private void BuildDryBoardwalk() =>
        BuildBoardwalk("DryPiers", [new Vector3(0f, 1.26f, 0f), new Vector3(0f, 1.26f, 14f)], 1.9f, DeckMaterial());

    private void BuildViewPlatform()
    {
        var center = new Vector3(0f, 1.26f, 14.8f);
        BuildPlatform("PiersViewPlatform", center, 3.4f, 2.8f, DeckMaterial());
        BuildRailing("PiersViewRail", center, 3.4f, 2.8f, ["west"], WetPostMaterial());
    }

    private void BuildBaySpur()
    {
        BuildBoardwalk("BaySpur", [
            new Vector3(0f, 1.26f, 14.8f),
            new Vector3(-6.5f, 1.02f, 18.2f),
            new Vector3(-12.5f, 0.72f, 21.7f),
            new Vector3(-16f, 0.56f, 25f)
        ], 1.6f, DeckMaterial());
        var center = new Vector3(-16f, 0.56f, 25f);
        BuildPlatform("BayPlatform", center, 2.9f, 2.9f, DeckMaterial());
        BuildRailing("BayRail", center, 2.9f, 2.9f, ["west", "south", "north"], WetPostMaterial());
    }

    private void BuildReflectionOutcrop()
    {
        BuildBoardwalk("ReflectionOutcrop", [
            new Vector3(8.9f, 1.26f, 30.6f),
            new Vector3(5.8f, 1.26f, 32.3f)
        ], 2f, DeckMaterial());

        // The source composition: chipped left post, two standing posts and a
        // diagonal board, with the old approach line kept right of the frame.
        WaterPost("ReflectionPostChipped", 6.18f, 33.0f, 0.72f, chipped: true);
        WaterPost("ReflectionPostMid", 5.8f, 32.3f, 0.55f);
        WaterPost("ReflectionPostOuter", 5.42f, 31.6f, 0.66f);
        var board = new MeshInstance3D
        {
            Name = "ReflectionDiagonalBoard",
            Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.06f, 1.7f) },
            Position = new Vector3(5.9f, 0.72f, 31.9f),
            RotationDegrees = new Vector3(34f, -58f, 6f),
            MaterialOverride = WetPostMaterial()
        };
        _world!.AddChild(board);

        // The reflection tableau: the same supports and the hidden old-approach
        // line read only under the water surface, aligned to the R03 view.
        var reflected = Snowless("8fa694", "wood_cut");
        float Mirrored(float top) => WaterLevel - (top - WaterLevel);
        void MirrorPost(string name, float x, float z, float top, float length, Vector3? tilt)
        {
            var post = new MeshInstance3D
            {
                Name = name,
                Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.10f, Height = length, RadialSegments = 6 },
                Position = new Vector3(x, Mirrored(top) + length * 0.5f, z),
                MaterialOverride = reflected,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            if (tilt is { } rotation) post.RotationDegrees = rotation;
            _world.AddChild(post);
        }

        MirrorPost("MirrorPostChipped", 6.18f, 33.0f, WaterLevel + 0.72f, 0.72f, new Vector3(-6f, 0f, -7f));
        MirrorPost("MirrorPostMid", 5.8f, 32.3f, WaterLevel + 0.55f, 0.55f, null);
        MirrorPost("MirrorPostOuter", 5.42f, 31.6f, WaterLevel + 0.66f, 0.66f, null);
        _world.AddChild(new MeshInstance3D
        {
            Name = "MirrorDiagonalBoard",
            Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.06f, 1.7f) },
            Position = new Vector3(5.9f, Mirrored(0.72f), 31.9f),
            RotationDegrees = new Vector3(-34f, -58f, -6f),
            MaterialOverride = reflected,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });

        // The hidden line: the old approach the winter eyes never saw. It runs
        // toward the ruined piles and stays below the surface entirely.
        for (var step = 0; step < 5; step++)
        {
            var x = 5.0f + 0.42f * step;
            var z = 33.2f + 0.78f * step;
            var top = WaterLevel - 0.08f - 0.04f * step;
            MirrorPost($"HiddenApproachPile{step + 1}", x, z, top, 0.62f + 0.05f * step,
                new Vector3(0f, step * 14f, -4f + step * 2f));
        }

        _world.AddChild(new MeshInstance3D
        {
            Name = "HiddenApproachWalkway",
            Mesh = new BoxMesh { Size = new Vector3(0.36f, 0.06f, 2.9f) },
            Position = new Vector3(5.8f, WaterLevel - 0.16f, 35.1f),
            RotationDegrees = new Vector3(0f, 28f, -3f),
            MaterialOverride = reflected,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });

        // Two reed heights kept at the same edge, as in the source print.
        AddReedClump("ReflectionReedTall", new Vector3(6.9f, 0f, 34.1f), 2.25f, 6, 77);
        AddReedClump("ReflectionReedShort", new Vector3(4.6f, 0f, 31.2f), 1.45f, 5, 78);
    }

    private void BuildReedTurn()
    {
        // Reed curtain across the walk; it moves aside when the place allows it.
        var curtainRoot = new Node3D { Name = "ReedCurtain", Position = new Vector3(25f, 1.06f, 42f) };
        curtainRoot.RotationDegrees = new Vector3(0f, -125f, 0f);
        _world!.AddChild(curtainRoot);
        _reedCurtainLeft = new Node3D { Name = "ReedCurtainLeft" };
        _reedCurtainRight = new Node3D { Name = "ReedCurtainRight" };
        curtainRoot.AddChild(_reedCurtainLeft);
        curtainRoot.AddChild(_reedCurtainRight);
        var bladeMesh = new CylinderMesh { TopRadius = 0.016f, BottomRadius = 0.045f, Height = 1f, RadialSegments = 5 };
        var reedMaterial = Snowless("6f8f4f", "grass_tuft");
        var strawMaterial = Snowless("93894f", "grass_tuft");
        var random = new Random(20261016);
        for (var blade = 0; blade < 16; blade++)
        {
            var side = blade % 2 == 0 ? _reedCurtainLeft : _reedCurtainRight;
            var height = 1.9f + (float)random.NextDouble() * 0.55f;
            side.AddChild(new MeshInstance3D
            {
                Name = $"CurtainBlade{blade + 1:00}",
                Mesh = bladeMesh,
                Position = new Vector3(-1.7f + blade * 0.22f, height * 0.5f,
                    (float)random.NextDouble() * 0.34f - 0.17f),
                RotationDegrees = new Vector3((float)random.NextDouble() * 6f - 3f,
                    (float)random.NextDouble() * 360f, (float)random.NextDouble() * 7f - 3.5f),
                Scale = new Vector3(0.9f, height, 0.9f),
                MaterialOverride = blade % 3 == 0 ? strawMaterial : reedMaterial
            });
        }

        _reedCurtainBody = TrackBody(new StaticBody3D
        {
            Name = "ReedCurtainContact",
            CollisionLayer = 1u,
            CollisionMask = 1u,
            Position = new Vector3(25f, 1.06f, 42f),
            RotationDegrees = new Vector3(0f, -125f, 0f)
        });
        _world.AddChild(_reedCurtainBody);
        _reedCurtainBody.AddChild(new CollisionShape3D
        {
            Name = "ReedCurtainBox",
            Position = new Vector3(0f, 1f, 0f),
            Shape = new BoxShape3D { Size = new Vector3(3.6f, 2f, 0.5f) }
        });

        // The old, half-buried walkway the reeds grew over.
        var oldPlanks = new Node3D { Name = "ReedTurnOldWalkway", Position = new Vector3(25f, 1.02f, 42f) };
        oldPlanks.RotationDegrees = new Vector3(0f, -125f, 0f);
        _world.AddChild(oldPlanks);
        for (var plank = 0; plank < 6; plank++)
            oldPlanks.AddChild(new MeshInstance3D
            {
                Name = $"OldPlank{plank + 1}",
                Mesh = new BoxMesh { Size = new Vector3(1.5f, 0.05f, 0.22f) },
                Position = new Vector3((float)random.NextDouble() * 0.3f - 0.15f, 0f, -1.1f + plank * 0.42f),
                RotationDegrees = new Vector3(0f, (float)random.NextDouble() * 5f - 2.5f,
                    (float)random.NextDouble() * 3f - 1.5f),
                MaterialOverride = OldPlankMaterial()
            });
    }

    private void PartReedCurtain(bool animated)
    {
        if (_reedCurtainBody is { } body && GodotObject.IsInstanceValid(body))
        {
            body.CollisionLayer = 0u;
            foreach (var child in body.GetChildren())
                if (child is CollisionShape3D shape) shape.Disabled = true;
        }

        var leftGoal = new Vector3(-1.35f, 0f, 1.15f);
        var rightGoal = new Vector3(1.35f, 0f, 1.15f);
        if (!animated)
        {
            if (_reedCurtainLeft is { } left) left.Position = leftGoal;
            if (_reedCurtainRight is { } right) right.Position = rightGoal;
            return;
        }

        foreach (var (side, goal) in new[] { (_reedCurtainLeft, leftGoal), (_reedCurtainRight, rightGoal) })
        {
            if (side is not { } node || !GodotObject.IsInstanceValid(node)) continue;
            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(node, "position", goal, 1.4f);
            tween.Parallel().TweenProperty(node, "rotation_degrees",
                new Vector3(0f, node.RotationDegrees.Y + (goal.X < 0f ? -14f : 14f), goal.X < 0f ? -6f : 6f), 1.4f);
        }
    }

    private void BuildOldApproach()
    {
        var piles = new Node3D { Name = "OldApproachPiles" };
        _world!.AddChild(piles);
        var random = new Random(20261017);
        for (var pile = 0; pile < 7; pile++)
        {
            var x = 34.6f - 0.62f * pile;
            var z = 63.5f + 0.85f * pile;
            var above = 0.28f + (float)random.NextDouble() * 0.34f;
            piles.AddChild(new MeshInstance3D
            {
                Name = $"OldApproachPile{pile + 1}",
                Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.155f, Height = 1f, RadialSegments = 6 },
                MaterialOverride = WetPostMaterial(),
                Position = new Vector3(x, 0f, z)
            });
            var pillar = (MeshInstance3D)piles.GetChild(pile);
            var ground = TerrainHeight(x, z);
            var top = WaterLevel + above;
            var bottom = ground - 0.25f;
            var height = top - bottom;
            pillar.Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.155f, Height = height };
            pillar.Position = new Vector3(x, bottom + height * 0.5f, z);
            pillar.RotationDegrees = new Vector3(4f * ((pile % 3) - 1), 20f * pile, 5f + 4f * ((pile % 4) - 1.5f));
        }

        for (var plank = 0; plank < 4; plank++)
            piles.AddChild(new MeshInstance3D
            {
                Name = $"OldApproachPlank{plank + 1}",
                Mesh = new BoxMesh { Size = new Vector3(0.17f, 0.05f, 1.5f) },
                Position = new Vector3(33.2f - plank * 0.55f, WaterLevel + 0.10f + plank * 0.04f,
                    64.1f + 0.35f * plank),
                RotationDegrees = new Vector3(56f + plank * 6f, 24f - plank * 9f, 14f),
                MaterialOverride = OldPlankMaterial()
            });
    }

    private void BuildFarPier()
    {
        Vector3[] legs =
        [
            new(48f, 1.36f, 83f),
            new(42f, 1.36f, 83.2f),
            new(36.5f, 1.36f, 83.6f),
            new(33.2f, 1.36f, 83.9f)
        ];
        BuildBoardwalk("FarPier", legs, 1.8f, DeckMaterial());
        BuildLegRailing("FarPierRail", legs, 1.8f, south: true, WetPostMaterial());
        BuildLegRailing("FarPierRailNorth", legs, 1.8f, south: false, WetPostMaterial());

        var board = new MeshInstance3D
        {
            Name = "FarPierLooseBoard",
            Mesh = new BoxMesh { Size = new Vector3(0.24f, 0.045f, 1.15f) },
            Position = new Vector3(33.4f, 1.42f, 83.9f),
            RotationDegrees = new Vector3(0f, -128f, 0f),
            MaterialOverride = DeckMaterial()
        };
        _world!.AddChild(board);
        _looseBoard = board;
        var mark = new MeshInstance3D
        {
            Name = "FarPierBoardMark",
            Mesh = new BoxMesh { Size = new Vector3(0.13f, 0.012f, 0.34f) },
            Position = new Vector3(33.4f, 1.392f, 83.9f),
            RotationDegrees = new Vector3(0f, -128f, 0f),
            MaterialOverride = Snowless("3c4a40", "wood_carved"),
            Visible = false
        };
        _world.AddChild(mark);
        _boardMark = mark;
    }

    private void BuildSummerBank()
    {
        // The meadow rest: a plain log bench looking back at the piers.
        var bench = new Node3D
        {
            Name = "SummerBankBench",
            Position = new Vector3(66.5f, TerrainHeight(66.5f, 103f), 103f)
        };
        bench.RotationDegrees = new Vector3(0f, -140f, 0f);
        _world!.AddChild(bench);
        foreach (var x in new[] { -0.55f, 0.55f })
            bench.AddChild(new MeshInstance3D
            {
                Name = $"BenchLeg{x}",
                Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.07f, Height = 0.42f, RadialSegments = 6 },
                Position = new Vector3(x, 0.21f, 0f),
                MaterialOverride = WetPostMaterial()
            });
        bench.AddChild(new MeshInstance3D
        {
            Name = "BenchSeat",
            Mesh = new BoxMesh { Size = new Vector3(1.5f, 0.08f, 0.34f) },
            Position = new Vector3(0f, 0.45f, 0f),
            MaterialOverride = DeckMaterial()
        });

        // The opening between the trees: two willow groups flanking R08.
        for (var side = -1; side <= 1; side += 2)
        {
            var x = 78f + side * 3.6f;
            var group = new Node3D
            {
                Name = $"OpeningWillows{side}",
                Position = new Vector3(x, TerrainHeight(x, 114f), 114f)
            };
            _world.AddChild(group);
            group.AddChild(new MeshInstance3D
            {
                Name = "Trunk",
                Mesh = new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.24f, Height = 3.2f, RadialSegments = 6 },
                Position = new Vector3(0f, 1.6f, 0f),
                MaterialOverride = Snowless("5b5044", "wood_cut")
            });
            for (var crown = 0; crown < 3; crown++)
                group.AddChild(new MeshInstance3D
                {
                    Name = $"Crown{crown}",
                    Mesh = new SphereMesh { Radius = 1.1f, Height = 2.2f, RadialSegments = 7, Rings = 4 },
                    Position = new Vector3((crown - 1) * 0.75f, 3.1f + (crown % 2) * 0.5f, (crown - 1) * 0.4f),
                    Scale = new Vector3(1.15f, 0.85f, 1.15f),
                    MaterialOverride = Snowless(crown == 1 ? "5c7046" : "516440", "leaf_birch")
                });
        }
    }

    // ------------------------------------------------------------------
    // Ground cover
    // ------------------------------------------------------------------

    private static readonly (float X, float Z, float Radius)[] ReedExclusionCircles =
    [
        (0f, 14.8f, 2.8f),
        (-16f, 25f, 2.8f),
        (5.8f, 32.3f, 2.8f),
        (31.2f, 85.6f, 2.6f)
    ];

    private static readonly (Vector2 A, Vector2 B)[] ReedExclusionSegments =
    [
        (new Vector2(0f, 0f), new Vector2(0f, 14f)),
        (new Vector2(0f, 14.8f), new Vector2(-16f, 25f)),
        (new Vector2(8.9f, 30.6f), new Vector2(5.8f, 32.3f)),
        (new Vector2(48f, 83f), new Vector2(33.2f, 83.9f))
    ];

    private static bool ReedBlocked(float x, float z)
    {
        foreach (var (cx, cz, radius) in ReedExclusionCircles)
            if ((x - cx) * (x - cx) + (z - cz) * (z - cz) < radius * radius) return true;
        foreach (var (a, b) in ReedExclusionSegments)
        {
            var dx = b.X - a.X;
            var dz = b.Y - a.Y;
            var lengthSquared = dx * dx + dz * dz;
            var t = lengthSquared <= 0.001f
                ? 0f
                : Mathf.Clamp(((x - a.X) * dx + (z - a.Y) * dz) / lengthSquared, 0f, 1f);
            var px = a.X + dx * t;
            var pz = a.Y + dz * t;
            if ((x - px) * (x - px) + (z - pz) * (z - pz) < 2.4f) return true;
        }

        return false;
    }

    private void BuildBankRushes()
    {
        var bladeMesh = new CylinderMesh { TopRadius = 0.016f, BottomRadius = 0.045f, Height = 1f, RadialSegments = 5 };
        var fresh = Snowless("6f8f4f", "grass_tuft");
        var deep = Snowless("55743f", "grass_tuft");
        var straw = Snowless("93894f", "grass_tuft");
        var random = new Random(20261018);
        var placements = new List<(Transform3D Transform, int Variant)>();
        for (var leg = 0; leg + 1 < MainRoute.Length; leg++)
        {
            var a = RouteNodes[MainRoute[leg]];
            var b = RouteNodes[MainRoute[leg + 1]];
            var length = new Vector2(b.X - a.X, b.Z - a.Z).Length();
            var direction = new Vector2(b.X - a.X, b.Z - a.Z) / length;
            var right = new Vector2(-direction.Y, direction.X);
            for (var along = 0f; along < length; along += 1.05f)
            {
                var t = along / length;
                var baseX = Mathf.Lerp(a.X, b.X, t);
                var baseZ = Mathf.Lerp(a.Z, b.Z, t);
                var clumpCount = 2 + random.Next(2);
                for (var clump = 0; clump < clumpCount; clump++)
                {
                    var reach = 3.3f + (float)random.NextDouble() * 3.1f;
                    var drift = (float)random.NextDouble() * 1.2f - 0.6f;
                    var x = baseX + right.X * reach + direction.X * drift;
                    var z = baseZ + right.Y * reach + direction.Y * drift;
                    if (ReedBlocked(x, z)) continue;
                    var ground = TerrainHeight(x, z);
                    if (ground < WaterLevel - 0.45f) continue;
                    var height = 1.25f + (float)random.NextDouble() * 1.05f;
                    var basis = Basis.Identity
                        .Rotated(Vector3.Up, (float)random.NextDouble() * Mathf.Tau)
                        .Rotated(new Vector3(1f, 0f, 0.3f).Normalized(),
                            ((float)random.NextDouble() * 8f - 4f) * Mathf.Pi / 180f);
                    placements.Add((new Transform3D(basis.Scaled(new Vector3(0.9f, height, 0.9f)),
                        new Vector3(x, ground + height * 0.42f, z)), clump % 3));
                }
            }
        }

        // The quiet bay keeps its own dense fringe.
        for (var index = 0; index < 90; index++)
        {
            var angle = (float)random.NextDouble() * Mathf.Tau;
            var radius = 4f + (float)random.NextDouble() * 6.5f;
            var x = -16f + MathF.Cos(angle) * radius;
            var z = 25f + MathF.Sin(angle) * radius;
            if (ReedBlocked(x, z)) continue;
            var ground = TerrainHeight(x, z);
            if (ground < WaterLevel - 0.5f) continue;
            var height = 1.35f + (float)random.NextDouble() * 0.9f;
            var basis = Basis.Identity.Rotated(Vector3.Up, (float)random.NextDouble() * Mathf.Tau);
            placements.Add((new Transform3D(basis.Scaled(new Vector3(0.9f, height, 0.9f)),
                new Vector3(x, ground + height * 0.42f, z)), index % 3));
        }

        foreach (var group in placements.GroupBy(entry => entry.Variant))
        {
            var mesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = bladeMesh,
                InstanceCount = group.Count()
            };
            var index = 0;
            foreach (var entry in group) mesh.SetInstanceTransform(index++, entry.Transform);
            _world!.AddChild(new MultiMeshInstance3D
            {
                Name = $"BankRushes{group.Key}",
                Multimesh = mesh,
                MaterialOverride = group.Key == 0 ? fresh : group.Key == 1 ? deep : straw
            });
        }
    }

    private void AddReedClump(string name, Vector3 baseAt, float height, int blades, int seed)
    {
        var random = new Random(seed);
        var clump = new Node3D { Name = name };
        var ground = TerrainHeight(baseAt.X, baseAt.Z);
        clump.Position = new Vector3(baseAt.X, ground, baseAt.Z);
        _world!.AddChild(clump);
        var bladeMesh = new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.05f, Height = 1f, RadialSegments = 5 };
        for (var blade = 0; blade < blades; blade++)
            clump.AddChild(new MeshInstance3D
            {
                Name = $"Blade{blade + 1}",
                Mesh = bladeMesh,
                Position = new Vector3((float)random.NextDouble() * 0.5f - 0.25f, height * 0.42f,
                    (float)random.NextDouble() * 0.5f - 0.25f),
                RotationDegrees = new Vector3((float)random.NextDouble() * 8f - 4f,
                    (float)random.NextDouble() * 360f, (float)random.NextDouble() * 9f - 4.5f),
                Scale = new Vector3(0.9f, height * (0.85f + (float)random.NextDouble() * 0.3f), 0.9f),
                MaterialOverride = Snowless(
                    blade % 3 == 0 ? "93894f" : "55743f", "grass_tuft")
            });
    }

    private void BuildFieldDressing()
    {
        var random = new Random(20261019);
        var tuftMesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.028f, Height = 1f, RadialSegments = 4 };
        var tuftPlacements = new List<Transform3D>();
        var flowerPlacements = new List<Transform3D>();
        for (var leg = MainRoute.Length - 3; leg + 1 < MainRoute.Length; leg++)
        {
            var a = RouteNodes[MainRoute[leg]];
            var b = RouteNodes[MainRoute[leg + 1]];
            for (var sample = 0; sample < 130; sample++)
            {
                var t = (float)random.NextDouble();
                var x = Mathf.Lerp(a.X, b.X, t) + ((float)random.NextDouble() * 30f - 6f);
                var z = Mathf.Lerp(a.Z, b.Z, t) + ((float)random.NextDouble() * 24f - 12f);
                var ground = TerrainHeight(x, z);
                if (ground < WaterLevel + 0.3f) continue;
                var height = 0.18f + (float)random.NextDouble() * 0.3f;
                tuftPlacements.Add(new Transform3D(
                    Basis.Identity.Rotated(Vector3.Up, (float)random.NextDouble() * Mathf.Tau)
                        .Scaled(new Vector3(0.8f, height, 0.8f)),
                    new Vector3(x, ground + height * 0.4f, z)));
                if (random.Next(7) == 0)
                    flowerPlacements.Add(new Transform3D(
                        Basis.Identity.Scaled(new Vector3(0.05f, 0.05f, 0.05f)),
                        new Vector3(x + 0.2f, ground + 0.28f, z + 0.15f)));
            }
        }

        var tuftMultimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = tuftMesh,
            InstanceCount = tuftPlacements.Count
        };
        for (var index = 0; index < tuftPlacements.Count; index++)
            tuftMultimesh.SetInstanceTransform(index, tuftPlacements[index]);
        _world!.AddChild(new MultiMeshInstance3D
        {
            Name = "SummerMeadowGrass",
            Multimesh = tuftMultimesh,
            MaterialOverride = Snowless("748b4c", "grass_tuft")
        });
        var flowerMultimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 5, Rings = 3 },
            InstanceCount = flowerPlacements.Count
        };
        for (var index = 0; index < flowerPlacements.Count; index++)
            flowerMultimesh.SetInstanceTransform(index, flowerPlacements[index]);
        _world.AddChild(new MultiMeshInstance3D
        {
            Name = "SummerMeadowFlowers",
            Multimesh = flowerMultimesh,
            MaterialOverride = Snowless("d8d3a8", "grass_tuft")
        });

        // The far wooded shore: coarse silhouettes that carry the horizon.
        var trunkMesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.3f, Height = 1f, RadialSegments = 5 };
        var crownMesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 6, Rings = 3 };
        var trunkPlacements = new List<Transform3D>();
        var crownPlacements = new List<Transform3D>();
        for (var tree = 0; tree < 70; tree++)
        {
            var x = -34f - (float)random.NextDouble() * 26f;
            var z = -10f + (float)random.NextDouble() * 150f;
            var ground = TerrainHeight(x, z);
            if (ground < WaterLevel + 0.4f) continue;
            var height = 4.5f + (float)random.NextDouble() * 5f;
            trunkPlacements.Add(new Transform3D(
                Basis.Identity.Scaled(new Vector3(0.9f, height * 0.6f, 0.9f)),
                new Vector3(x, ground + height * 0.3f, z)));
            crownPlacements.Add(new Transform3D(
                Basis.Identity.Scaled(new Vector3(1.5f, height * 0.34f, 1.5f)),
                new Vector3(x, ground + height * 0.72f, z)));
        }

        for (var tree = 0; tree < 26; tree++)
        {
            var x = 40f + (float)random.NextDouble() * 50f;
            var z = -18f + (float)random.NextDouble() * 60f;
            var ground = TerrainHeight(x, z);
            var height = 3.4f + (float)random.NextDouble() * 3.4f;
            trunkPlacements.Add(new Transform3D(
                Basis.Identity.Scaled(new Vector3(0.9f, height * 0.6f, 0.9f)),
                new Vector3(x, ground + height * 0.3f, z)));
            crownPlacements.Add(new Transform3D(
                Basis.Identity.Scaled(new Vector3(1.2f, height * 0.34f, 1.2f)),
                new Vector3(x, ground + height * 0.72f, z)));
        }

        var trunkMultimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = trunkMesh,
            InstanceCount = trunkPlacements.Count
        };
        for (var index = 0; index < trunkPlacements.Count; index++)
            trunkMultimesh.SetInstanceTransform(index, trunkPlacements[index]);
        _world!.AddChild(new MultiMeshInstance3D
        {
            Name = "HorizonTrunks",
            Multimesh = trunkMultimesh,
            MaterialOverride = Snowless("5b5044", "wood_cut")
        });
        var crownMultimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = crownMesh,
            InstanceCount = crownPlacements.Count
        };
        for (var index = 0; index < crownPlacements.Count; index++)
            crownMultimesh.SetInstanceTransform(index, crownPlacements[index]);
        _world.AddChild(new MultiMeshInstance3D
        {
            Name = "HorizonCrowns",
            Multimesh = crownMultimesh,
            MaterialOverride = Snowless("4e6242", "leaf_birch")
        });
    }

    // ------------------------------------------------------------------
    // The water answer
    // ------------------------------------------------------------------

    private void BuildWaterAnswer()
    {
        var presence = new Node3D { Name = "SuAnasyPresence", Position = new Vector3(31.2f, -1.35f, 85.6f) };
        _world!.AddChild(presence);
        _suAnasy = presence;

        var skin = Snowless("b9cfc4", "cloth");
        var hairMaterial = Snowless("3f5f58", "cloth");
        presence.AddChild(new MeshInstance3D
        {
            Name = "Silhouette",
            Mesh = new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.26f, Height = 1.05f, RadialSegments = 10 },
            Position = new Vector3(0f, 0.55f, 0f),
            MaterialOverride = skin
        });
        presence.AddChild(new MeshInstance3D
        {
            Name = "Head",
            Mesh = new SphereMesh { Radius = 0.125f, Height = 0.25f, RadialSegments = 10, Rings = 6 },
            Position = new Vector3(0f, 1.19f, 0f),
            MaterialOverride = skin
        });
        var random = new Random(20261020);
        for (var strand = 0; strand < 9; strand++)
        {
            var angle = strand / 9f * Mathf.Tau;
            presence.AddChild(new MeshInstance3D
            {
                Name = $"HairStrand{strand + 1}",
                Mesh = new PlaneMesh { Size = new Vector2(0.17f, 1.15f) },
                Position = new Vector3(MathF.Cos(angle) * 0.1f, 0.74f, MathF.Sin(angle) * 0.1f),
                RotationDegrees = new Vector3(0f, -angle * 180f / Mathf.Pi, 8f - (float)random.NextDouble() * 16f),
                MaterialOverride = hairMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }

        for (var layer = 0; layer < 3; layer++)
            presence.AddChild(new MeshInstance3D
            {
                Name = $"StreamingLayer{layer + 1}",
                Mesh = new PlaneMesh { Size = new Vector2(1.25f, 0.55f) },
                Position = new Vector3(0f, 0.18f - layer * 0.07f, 0f),
                RotationDegrees = new Vector3(-70f - layer * 5f, layer * 55f, 0f),
                MaterialOverride = hairMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });

        // The first movement is part of the current: pale rings crossing the
        // far pier until the answer arrives.
        var ringRandom = new Random(20261021);
        for (var ring = 0; ring < 3; ring++)
        {
            var ringMaterial = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.82f, 0.9f, 0.86f, 0.3f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            var mesh = new MeshInstance3D
            {
                Name = $"CurrentRing{ring + 1}",
                Mesh = new TorusMesh { InnerRadius = 0.34f, OuterRadius = 0.42f, Rings = 24, RingSegments = 4 },
                Position = new Vector3(33f + (float)ringRandom.NextDouble() * 2f - 1f, WaterLevel + 0.02f,
                    84.5f + (float)ringRandom.NextDouble() * 2f - 1f),
                Scale = new Vector3(1f, 0.35f, 1f),
                MaterialOverride = ringMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            _world.AddChild(mesh);
            _encounterRings.Add(mesh);
        }
    }

    private void SettleLooseBoard(bool animated)
    {
        if (_looseBoard is not { } board || !GodotObject.IsInstanceValid(board)) return;
        var restRotation = new Vector3(0f, -128f, 0f);
        if (!animated)
        {
            board.RotationDegrees = restRotation;
            board.Position = new Vector3(board.Position.X, 1.42f, board.Position.Z);
            if (_boardMark is { } mark) mark.Visible = false;
            return;
        }

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(board, "rotation_degrees", restRotation, 0.9f);
        tween.Parallel().TweenProperty(board, "position",
            new Vector3(board.Position.X, 1.42f, board.Position.Z), 0.9f);
        if (_boardMark is { } markToHide)
        {
            var markTween = CreateTween();
            markTween.TweenInterval(0.7);
            markTween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(markToHide)) markToHide.Visible = false;
            }));
        }
    }

    private void LiftLooseBoard()
    {
        if (_looseBoard is not { } board || !GodotObject.IsInstanceValid(board)) return;
        if (_boardMark is { } mark) mark.Visible = true;
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(board, "rotation_degrees", new Vector3(-7f, -128f, 3f), 0.7f);
        tween.Parallel().TweenProperty(board, "position",
            new Vector3(board.Position.X, 1.47f, board.Position.Z), 0.7f);
    }

    // ------------------------------------------------------------------
    // Interaction targets
    // ------------------------------------------------------------------

    private InteractionTarget EnsureTarget(string name, string interactionId, string prompt, Vector3 at, float radius)
    {
        var target = new InteractionTarget
        {
            Name = name,
            InteractionId = interactionId,
            Prompt = prompt,
            CollisionLayer = 4u,
            CollisionMask = 0u
        };
        target.ConfigureRayOnly();
        target.AddChild(new CollisionShape3D
        {
            Name = name + "Ray",
            Shape = new SphereShape3D { Radius = radius }
        });
        AddChild(target);
        target.Position = at;
        _targets.Add(target);
        return target;
    }

    private void EnsureTargets(RuntimeBridge bridge)
    {
        if (_targets.Count == 0)
        {
            EnsureTarget("WaterReflectionTarget", ReflectionInteractionId,
                bridge.ResolveText(ReflectionLabelTextId), new Vector3(6.4f, 2.1f, 31.6f), 0.62f);
            EnsureTarget("WaterBypassTarget", BypassInteractionId,
                bridge.ResolveText(BypassLabelTextId), new Vector3(25f, 2.2f, 42f), 0.7f);
            EnsureTarget("WaterOldApproachTarget", OldApproachInteractionId,
                bridge.ResolveText(OldApproachLabelTextId), new Vector3(35.4f, 2.1f, 63.7f), 0.7f);
            _encounterTarget = EnsureTarget("WaterEncounterTarget", EncounterInteractionId,
                "Осмотреть край дощечки", new Vector3(33.4f, 2.3f, 83.9f), 0.55f);
            EnsureTarget("WaterDampPrintTarget", DampPrintInteractionId,
                bridge.ResolveText(DampPrintLabelTextId),
                new Vector3(79.3f, TerrainHeight(79.3f, 117.4f) + 1.15f, 117.4f), 0.55f);
            EnsureTarget("WaterReturnTarget", ReturnInteractionId,
                bridge.ResolveText(ReturnLabelTextId),
                new Vector3(78f, TerrainHeight(78f, 118.6f) + 1.5f, 118.6f), 0.8f);
        }

        if (_encounterTarget is { } encounter)
        {
            encounter.PresentationBeforeDispatch = () =>
            {
                if (_boardLifted) return true;
                _boardLifted = true;
                LiftLooseBoard();
                UpdateEncounterPrompt(encounterResolved: false);
                return false;
            };
        }

        UpdateEncounterPrompt(_encounterWasResolved);
    }

    private void UpdateEncounterPrompt(bool encounterResolved)
    {
        if (_encounterTarget is not { } target || !GodotObject.IsInstanceValid(target)) return;
        if (encounterResolved)
        {
            target.Prompt = "Дощечка на месте";
            return;
        }

        target.Prompt = _boardLifted
            ? _bridge?.ResolveText(EncounterLabelTextId) ?? "Положить дощечку на место и заметить ответ"
            : "Осмотреть край дощечки";
    }

    private void BuildExitStand()
    {
        var ground = TerrainHeight(79.3f, 117.4f);
        var stand = new Node3D { Name = "ExitPrintStand", Position = new Vector3(79.3f, ground, 117.4f) };
        stand.RotationDegrees = new Vector3(0f, -155f, 0f);
        _world!.AddChild(stand);
        stand.AddChild(new MeshInstance3D
        {
            Name = "StandCrate",
            Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.62f, 0.42f) },
            Position = new Vector3(0f, 0.31f, 0f),
            MaterialOverride = WetPostMaterial()
        });
        stand.AddChild(new MeshInstance3D
        {
            Name = "StandTop",
            Mesh = new BoxMesh { Size = new Vector3(0.58f, 0.05f, 0.5f) },
            Position = new Vector3(0f, 0.645f, 0f),
            MaterialOverride = DeckMaterial()
        });
        stand.AddChild(new MeshInstance3D
        {
            Name = "ExitPrint",
            Mesh = new QuadMesh { Size = new Vector2(0.21f, 0.26f) },
            Position = new Vector3(0f, 0.675f, 0f),
            RotationDegrees = new Vector3(-90f, 0f, 4f),
            MaterialOverride = Snowless("d6ccb3", "paper")
        });
        stand.AddChild(new MeshInstance3D
        {
            Name = "ExitPrintDampCorner",
            Mesh = new QuadMesh { Size = new Vector2(0.06f, 0.07f) },
            Position = new Vector3(0.065f, 0.677f, 0.09f),
            RotationDegrees = new Vector3(-90f, 0f, 4f),
            MaterialOverride = Snowless("9c9382", "paper")
        });
    }

    // ------------------------------------------------------------------
    // Structure helpers
    // ------------------------------------------------------------------

    private void BuildWalkingRibbon()
    {
        var ribbon = new Node3D { Name = "WalkingRibbon" };
        _world!.AddChild(ribbon);
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var halfWidth = 0.95f;
        void Strip(Vector3[] nodes)
        {
            for (var leg = 0; leg + 1 < nodes.Length; leg++)
            {
                var a = nodes[leg];
                var b = nodes[leg + 1];
                var flat = new Vector2(b.X - a.X, b.Z - a.Z);
                var length = flat.Length();
                var direction = flat / length;
                var across = new Vector2(-direction.Y, direction.X);
                var stations = Mathf.Max(1, Mathf.RoundToInt(length / 1.4f));
                Vector3 Corner(float t, float side, float yOffset = 0.035f)
                {
                    var cx = Mathf.Lerp(a.X, b.X, t) + across.X * side * halfWidth;
                    var cz = Mathf.Lerp(a.Z, b.Z, t) + across.Y * side * halfWidth;
                    return new Vector3(cx, TerrainHeight(cx, cz) + yOffset, cz);
                }

                void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
                {
                    var normal = AnalyticNormal(p0.X, p0.Z);
                    foreach (var vertex in new[] { p0, p2, p1, p0, p3, p2 })
                    {
                        surface.SetNormal(normal);
                        surface.SetUV(new Vector2(vertex.X, vertex.Z) * 0.4f);
                        surface.AddVertex(vertex);
                    }
                }

                for (var station = 0; station < stations; station++)
                {
                    var t0 = station / (float)stations;
                    var t1 = (station + 1) / (float)stations;
                    var p0 = Corner(t0, -1f);
                    var p1 = Corner(t1, -1f);
                    var p2 = Corner(t1, 1f);
                    var p3 = Corner(t0, 1f);
                    Quad(p0, p1, p2, p3);
                }
            }
        }

        var main = MainRoute.Select(index => RouteNodes[index]).ToArray();
        Strip(main);
        Strip([RouteNodes[SpurA], RouteNodes[SpurB]]);
        ribbon.AddChild(new MeshInstance3D
        {
            Name = "WalkingRibbonMesh",
            Mesh = surface.Commit(),
            MaterialOverride = Snowless("8b7a58", "grass"),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    private void BuildBoardwalk(string name, Vector3[] nodes, float width, Material deckMaterial)
    {
        var root = new Node3D { Name = name };
        _world!.AddChild(root);
        var planks = new List<Transform3D>();
        var posts = new List<Transform3D>();
        var body = TrackBody(new StaticBody3D { Name = name + "Contact", CollisionLayer = 1u, CollisionMask = 1u });
        _world.AddChild(body);
        var postMesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.085f, Height = 1f, RadialSegments = 6 };
        for (var leg = 0; leg + 1 < nodes.Length; leg++)
        {
            var a = nodes[leg];
            var b = nodes[leg + 1];
            var flat = new Vector2(b.X - a.X, b.Z - a.Z);
            var length = flat.Length();
            var direction = flat / length;
            var right = new Vector2(-direction.Y, direction.X);
            var yaw = Mathf.RadToDeg(MathF.Atan2(direction.X, direction.Y));
            var pitch = -Mathf.RadToDeg(MathF.Atan2(b.Y - a.Y, length));
            // Yaw first, then the slope tilt about the leg's own right axis.
            var legBasis = Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(yaw))
                * new Basis(Vector3.Right, Mathf.DegToRad(pitch));
            var midpoint = (a + b) * 0.5f;
            for (var along = 0.18f; along < length - 0.05f; along += 0.235f)
            {
                var t = along / length;
                planks.Add(new Transform3D(legBasis,
                    a + (b - a) * t + new Vector3(0f, 0.02f, 0f)));
            }

            for (var post = 0.4f; post < length; post += 2.3f)
            {
                var t = post / length;
                var at = a + (b - a) * t;
                foreach (var side in new[] { -1f, 1f })
                {
                    var px = at.X + right.X * side * (width * 0.5f - 0.12f);
                    var pz = at.Z + right.Y * side * (width * 0.5f - 0.12f);
                    var ground = TerrainHeight(px, pz);
                    var top = at.Y - 0.03f;
                    var bottom = Mathf.Min(ground - 0.55f, top - 0.3f);
                    var height = top - bottom;
                    posts.Add(new Transform3D(
                        Basis.Identity.Scaled(new Vector3(1f, height, 1f)),
                        new Vector3(px, bottom + height * 0.5f, pz)));
                }
            }

            // One physical board per leg keeps walking real over water.
            body.AddChild(new CollisionShape3D
            {
                Name = $"{name}Leg{leg + 1}Contact",
                Transform = new Transform3D(legBasis, midpoint + new Vector3(0f, -0.012f, 0f)),
                Shape = new BoxShape3D { Size = new Vector3(width, 0.09f, length + 0.18f) }
            });
            root.AddChild(new MeshInstance3D
            {
                Name = $"{name}Stringer{leg + 1}",
                Mesh = new BoxMesh { Size = new Vector3(width, 0.055f, length) },
                Transform = new Transform3D(legBasis, midpoint + new Vector3(0f, -0.035f, 0f)),
                MaterialOverride = deckMaterial
            });
        }

        var plankMultimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new BoxMesh { Size = new Vector3(width, 0.035f, 0.19f) },
            InstanceCount = planks.Count
        };
        for (var index = 0; index < planks.Count; index++)
            plankMultimesh.SetInstanceTransform(index, planks[index]);
        root.AddChild(new MultiMeshInstance3D
        {
            Name = name + "Planks",
            Multimesh = plankMultimesh,
            MaterialOverride = deckMaterial
        });

        var postMultimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = postMesh,
            InstanceCount = posts.Count
        };
        for (var index = 0; index < posts.Count; index++)
            postMultimesh.SetInstanceTransform(index, posts[index]);
        root.AddChild(new MultiMeshInstance3D
        {
            Name = name + "Posts",
            Multimesh = postMultimesh,
            MaterialOverride = WetPostMaterial()
        });
    }

    private void BuildPlatform(string name, Vector3 center, float sizeX, float sizeZ, Material deckMaterial)
    {
        var root = new Node3D { Name = name };
        _world!.AddChild(root);
        var planks = new List<Transform3D>();
        for (var z = -sizeZ * 0.5f + 0.11f; z < sizeZ * 0.5f; z += 0.235f)
            planks.Add(new Transform3D(Basis.Identity, center + new Vector3(0f, 0.02f, z)));
        var plankMultimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new BoxMesh { Size = new Vector3(sizeX, 0.035f, 0.19f) },
            InstanceCount = planks.Count
        };
        for (var index = 0; index < planks.Count; index++)
            plankMultimesh.SetInstanceTransform(index, planks[index]);
        root.AddChild(new MultiMeshInstance3D
        {
            Name = name + "Planks",
            Multimesh = plankMultimesh,
            MaterialOverride = deckMaterial
        });
        root.AddChild(new MeshInstance3D
        {
            Name = name + "Stringer",
            Mesh = new BoxMesh { Size = new Vector3(sizeX, 0.055f, sizeZ) },
            Position = center + new Vector3(0f, -0.035f, 0f),
            MaterialOverride = deckMaterial
        });

        var posts = new List<Transform3D>();
        foreach (var corner in new[]
                 {
                     new Vector2(-1f, -1f), new Vector2(1f, -1f),
                     new Vector2(-1f, 1f), new Vector2(1f, 1f)
                 })
        {
            var px = center.X + corner.X * (sizeX * 0.5f - 0.14f);
            var pz = center.Z + corner.Y * (sizeZ * 0.5f - 0.14f);
            var ground = TerrainHeight(px, pz);
            var top = center.Y - 0.03f;
            var bottom = Mathf.Min(ground - 0.55f, top - 0.3f);
            var height = top - bottom;
            posts.Add(new Transform3D(
                Basis.Identity.Scaled(new Vector3(1f, height, 1f)),
                new Vector3(px, bottom + height * 0.5f, pz)));
        }

        var postMultimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.085f, Height = 1f, RadialSegments = 6 },
            InstanceCount = posts.Count
        };
        for (var index = 0; index < posts.Count; index++)
            postMultimesh.SetInstanceTransform(index, posts[index]);
        root.AddChild(new MultiMeshInstance3D
        {
            Name = name + "Posts",
            Multimesh = postMultimesh,
            MaterialOverride = WetPostMaterial()
        });

        var body = TrackBody(new StaticBody3D { Name = name + "Contact", CollisionLayer = 1u, CollisionMask = 1u });
        _world.AddChild(body);
        body.AddChild(new CollisionShape3D
        {
            Name = name + "Pad",
            Position = center + new Vector3(0f, -0.012f, 0f),
            Shape = new BoxShape3D { Size = new Vector3(sizeX, 0.09f, sizeZ) }
        });
    }

    private void BuildRailing(string name, Vector3 center, float sizeX, float sizeZ, string[] sides, Material material)
    {
        var root = new Node3D { Name = name };
        _world!.AddChild(root);
        foreach (var side in sides)
        {
            var horizontal = side is "north" or "south";
            var length = horizontal ? sizeX : sizeZ;
            var offset = side switch
            {
                "north" => new Vector3(0f, 0f, -sizeZ * 0.5f + 0.05f),
                "south" => new Vector3(0f, 0f, sizeZ * 0.5f - 0.05f),
                "west" => new Vector3(-sizeX * 0.5f + 0.05f, 0f, 0f),
                _ => new Vector3(sizeX * 0.5f - 0.05f, 0f, 0f)
            };
            var along = horizontal ? Vector3.Right : Vector3.Back;
            for (var position = -length * 0.5f + 0.25f; position <= length * 0.5f - 0.25f; position += 1.35f)
                root.AddChild(new MeshInstance3D
                {
                    Name = $"{name}Post{side}{position:F1}",
                    Mesh = new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.055f, Height = 1.02f, RadialSegments = 5 },
                    Position = center + offset + along * position + new Vector3(0f, 0.51f, 0f),
                    MaterialOverride = material
                });
            root.AddChild(new MeshInstance3D
            {
                Name = $"{name}Top{side}",
                Mesh = new BoxMesh
                {
                    Size = horizontal
                        ? new Vector3(length, 0.06f, 0.09f)
                        : new Vector3(0.09f, 0.06f, length)
                },
                Position = center + offset + new Vector3(0f, 1f, 0f),
                MaterialOverride = material
            });
        }
    }

    private void BuildLegRailing(string name, Vector3[] nodes, float width, bool south, Material material)
    {
        var root = new Node3D { Name = name };
        _world!.AddChild(root);
        for (var leg = 0; leg + 1 < nodes.Length; leg++)
        {
            var a = nodes[leg];
            var b = nodes[leg + 1];
            var flat = new Vector2(b.X - a.X, b.Z - a.Z);
            var length = flat.Length();
            var direction = flat / length;
            var right = new Vector2(-direction.Y, direction.X);
            var side = south ? 1f : -1f;
            var yaw = Mathf.RadToDeg(MathF.Atan2(direction.X, direction.Y));
            for (var along = 0.3f; along < length - 0.2f; along += 1.5f)
            {
                var t = along / length;
                var at = a + (b - a) * t;
                var px = at.X + right.X * side * (width * 0.5f - 0.08f);
                var pz = at.Z + right.Y * side * (width * 0.5f - 0.08f);
                root.AddChild(new MeshInstance3D
                {
                    Name = $"{name}Post{leg}{along:F1}",
                    Mesh = new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.055f, Height = 1.05f, RadialSegments = 5 },
                    Position = new Vector3(px, at.Y + 0.52f, pz),
                    MaterialOverride = material
                });
            }

            var midpoint = (a + b) * 0.5f;
            root.AddChild(new MeshInstance3D
            {
                Name = $"{name}Top{leg}",
                Mesh = new BoxMesh { Size = new Vector3(0.09f, 0.06f, length) },
                Position = midpoint + new Vector3(right.X * side * (width * 0.5f - 0.08f), 1.02f,
                    right.Y * side * (width * 0.5f - 0.08f)),
                RotationDegrees = new Vector3(0f, yaw, 0f),
                MaterialOverride = material
            });
        }
    }

    private void WaterPost(string name, float x, float z, float aboveWater, bool chipped = false)
    {
        var ground = TerrainHeight(x, z);
        var top = WaterLevel + aboveWater;
        var bottom = Mathf.Min(ground - 0.3f, WaterLevel - 0.8f);
        var height = top - bottom;
        var post = new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.10f, Height = height, RadialSegments = 6 },
            Position = new Vector3(x, bottom + height * 0.5f, z),
            MaterialOverride = WetPostMaterial()
        };
        if (chipped)
        {
            post.Scale = new Vector3(1f, 0.72f, 1f);
            post.Position = new Vector3(x, bottom + height * 0.36f, z);
            post.RotationDegrees = new Vector3(-3f, 0f, 7f);
        }

        _world!.AddChild(post);
    }

    /// <summary>Capture-only state switch; the running game always projects runtime facts.</summary>
    internal void ApplyCaptureFacts(bool reedOpened, bool encounterResolved)
    {
        if (reedOpened) PartReedCurtain(animated: false);
        if (encounterResolved)
        {
            _encounterWasResolved = true;
            _boardLifted = false;
            SettleLooseBoard(animated: false);
            _suAnasyY = _suAnasyTargetY = 0.15f;
            UpdateEncounterPrompt(encounterResolved: true);
        }
    }
}
