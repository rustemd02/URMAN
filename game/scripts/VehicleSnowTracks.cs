using System;
using System.Collections.Generic;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Two bounded pressed-snow ruts left by the rear wheels of the drivable car.
///
/// Presentation only, session only: no collider, no navigation, no save state,
/// and no change to the vehicle's physics. This is a decal/ribbon system, not a
/// deformable one: the footsteps own the real snow displacement in
/// SnowTrampleField (one global 24 m mask, 512 stamps, refined terrain
/// vertices), and a second writer of that mask plus per-stamp terrain
/// subdivision at driving speed would not fit its budget. Instead each stamped
/// station emits one soft-edged quad of the existing trampled-snow vocabulary
/// just above the snow, distance-faded exactly like the footprint window fades
/// behind the player.
///
/// Hard budget (one instance for the Niva):
///   instances 512 quads (2 ruts x 256 x 0.42 m); transforms 512x48 B + colors
///             512x16 B ~ 33 KB, quad mesh 4 verts; no per-frame mesh rebuilds;
///   stamps    at most one per tracked wheel per physics frame, one per 0.42 m;
///   per tick  4 CollisionGround samples per stamp (pure maths, no physics
///             queries), a handful of struct writes; zero managed allocations;
///   per frame <=2 stamps (<=8 height samples) at any speed, 0 when parked;
///   eviction  ring buffer; distance fade has already hidden a quad long
///             before its slot is reused, so nothing ever pops.
///
/// Exclusion contract: a stamp needs the wheel on the exterior terrain surface
/// (CollisionGround under the wheel hub within GroundTolerance and no ice/water
/// mesh at snow level), off the authored packed carriageway, and outside every
/// interior (an interior floor is not the exterior height field, so the hub
/// check rejects it). Bridge decks are separate layer-2 bodies above the gorge
/// floor, so the hub check rejects them as well.
/// </summary>
public partial class VehicleSnowTracks : Node3D
{
    public const int QuadBudget = 512;
    private const float StationSpacing = .42f;
    // .19 m tyre + snow squeezed out under the shoulder of the tread.
    private const float RutWidth = .28f;
    // Above the snow microrelief (+-4 mm) and the trample ridge (+12 mm); low
    // enough not to read as a floating ribbon.
    private const float SurfaceOffset = .018f;
    // The kinematic car rests the hull 1 cm below the wheel contact line, so a
    // grounded wheel measures ~1 cm. Bridge decks sit .34 m and more above the
    // height field at their abutments, which this rejects.
    private const float GroundTolerance = .25f;
    private const float MinimumSpeed = .15f;
    private const float FadeNear = 32f;
    private const float FadeFar = 58f;
    private const float MaximumFrameTravel = 1.4f;
    private const float AabbRefreshDistance = 5f;
    private const string SnowTexturePath = "res://assets/textures/painterly/snow_trampled_v1_albedo.png";

    private static readonly Transform3D Hidden = Transform3D.Identity.Scaled(Vector3.Zero);

    private MultiMesh _multimesh = null!;
    private ShaderMaterial _material = null!;
    private VehicleController? _vehicle;
    private Vector2[] _last = Array.Empty<Vector2>();
    private bool[] _hasLast = Array.Empty<bool>();
    private bool[] _lastOffRoad = Array.Empty<bool>();
    private float[] _travelled = Array.Empty<float>();
    private readonly List<MeshInstance3D> _blockedSurfaces = new();
    private bool _blockedBound;
    private int _cursor;
    private int _segments;
    private float _metres;
    private Vector3 _aabbOrigin = new(float.NaN, 0f, 0f);
    private Vector3 _fadeOrigin = new(float.NaN, 0f, 0f);

    /// <summary>Rear wheels whose ground contact leaves the two ruts; set by the factory.</summary>
    public Node3D[] TrackedWheels { get; set; } = Array.Empty<Node3D>();
    public float WheelRadius { get; set; } = .345f;

    public override void _Ready()
    {
        SetMeta("presentationOnly", true);
        SetMeta("visualOnly", true);
        SetMeta("collisionOwner", "none");
        SetMeta("navigationOwner", "none");
        SetMeta("interactionOwner", "none");
        SetMeta("savePolicy", "session-only ruts; never serialized");
        SetMeta("trackPolicy", "two rear-wheel ruts; skips packed carriageway, ice/water, interiors and bridge decks; distance-faded");
        SetMeta("vehicleTrackSegments", 0);
        SetMeta("vehicleTrackBudget", QuadBudget);
        SetMeta("vehicleTrackMeters", 0f);
        SetMeta("vehicleTrackStationSpacingMetres", StationSpacing);
        SetMeta("vehicleTrackFadeMetres", $"{FadeNear}-{FadeFar}");
        _last = new Vector2[TrackedWheels.Length];
        _hasLast = new bool[TrackedWheels.Length];
        _lastOffRoad = new bool[TrackedWheels.Length];
        _travelled = new float[TrackedWheels.Length];
        var quad = new PlaneMesh { Size = Vector2.One, Orientation = PlaneMesh.OrientationEnum.Y };
        _material = new ShaderMaterial { Shader = new Shader { Code = RutShader } };
        _material.SetShaderParameter("fade_near", FadeNear);
        _material.SetShaderParameter("fade_far", FadeFar);
        if (!PainterlyMaterialLibrary.SuppressTextureLoadsForHeadlessTests
            && ResourceLoader.Exists(SnowTexturePath)
            && ResourceLoader.Load<Texture2D>(SnowTexturePath) is { } texture)
            _material.SetShaderParameter("snow_texture", texture);
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = quad,
            InstanceCount = QuadBudget
        };
        for (var index = 0; index < QuadBudget; index++) _multimesh.SetInstanceTransform(index, Hidden);
        var extent = FadeFar + 12f;
        _multimesh.CustomAabb = new Aabb(new Vector3(-extent, -40f, -extent), new Vector3(extent * 2f, 80f, extent * 2f));
        // TopLevel keeps the decal in world space while it is still freed with
        // the vehicle and hidden with it.
        AddChild(new MultiMeshInstance3D { Name = "VehicleSnowRuts", Multimesh = _multimesh, MaterialOverride = _material, TopLevel = true });
        GD.Print($"vehicle-snow-tracks: segments=0 budget={QuadBudget}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (TrackedWheels.Length == 0) return;
        var dt = Mathf.Min((float)delta, .05f);
        if (dt <= 0f) return;
        _vehicle ??= FindVehicle();
        if (_vehicle is null || !GodotObject.IsInstanceValid(_vehicle) || !_vehicle.IsInsideTree()) return;
        var speed = _vehicle.Speed;
        var wheels = Mathf.Min(TrackedWheels.Length, _last.Length);
        for (var index = 0; index < wheels; index++)
        {
            var wheel = TrackedWheels[index];
            if (wheel is null || !GodotObject.IsInstanceValid(wheel)) continue;
            var position = wheel.GlobalPosition;
            var here = new Vector2(position.X, position.Z);
            if (!_hasLast[index]) { _last[index] = here; _hasLast[index] = true; _travelled[index] = 0f; continue; }
            var previous = _last[index];
            _last[index] = here;
            var movement = here - previous;
            var distance = movement.Length();
            if (distance < .0001f) continue;
            // Session boundaries and debug spawns teleport the car; the previous
            // session's ruts are dropped exactly like the footprint window resets.
            if (distance > Mathf.Max(MaximumFrameTravel, Mathf.Abs(speed) * dt * 2f + .2f)) { ClearRuts(); return; }
            _travelled[index] += distance;
            if (_travelled[index] < StationSpacing || Mathf.Abs(speed) < MinimumSpeed) continue;
            if (TryStamp(index, position, previous, here, distance, movement / distance))
                _travelled[index] -= StationSpacing;
            else
                _travelled[index] = 0f;
        }
    }

    private VehicleController? FindVehicle()
    {
        for (var node = GetParent(); node is not null; node = node.GetParent())
            if (node is VehicleController controller) return controller;
        return null;
    }

    private bool TryStamp(int index, Vector3 wheelWorld, Vector2 previous, Vector2 here, float length, Vector2 direction)
    {
        if (length < .06f || length > MaximumFrameTravel) return false;
        if (!WithinTerrain(here) || !WithinTerrain(previous)) return false;
        var hubHeight = wheelWorld.Y - WheelRadius;
        var support = (float)AgentBAct1HeightField.CollisionGround(here.X, here.Y);
        if (Mathf.Abs(hubHeight - support) > GroundTolerance) return false;
        var previousSupport = (float)AgentBAct1HeightField.CollisionGround(previous.X, previous.Y);
        if (Mathf.Abs(hubHeight - previousSupport) > GroundTolerance + .08f) return false;
        var road = AgentBAct1HeightField.RoadInfo(here.X, here.Y);
        var offRoad = road.Distance > road.HalfWidth + .05f;
        // The packed carriageway owns its own authored ruts; start and stop the
        // decal at its verge instead of bridging the packed lane.
        if (!offRoad || !_lastOffRoad[index]) { _lastOffRoad[index] = offRoad; return false; }
        if (BlockedByIce(here)) return false;
        var side = new Vector2(-direction.Y, direction.X) * (RutWidth * .5f);
        var left = (float)AgentBAct1HeightField.CollisionGround(here.X + side.X, here.Y + side.Y);
        var right = (float)AgentBAct1HeightField.CollisionGround(here.X - side.X, here.Y - side.Y);
        var along = Mathf.Atan2(support - previousSupport, length);
        var roll = Mathf.Atan2(right - left, RutWidth);
        var basis = new Basis(Vector3.Up, Mathf.Atan2(direction.X, direction.Y));
        basis = basis.Rotated(basis.X, -along);
        basis = basis.Rotated(basis.Z, roll);
        var midpoint = (previous + here) * .5f;
        var transform = new Transform3D(basis.Scaled(new Vector3(RutWidth, 1f, length)),
            new Vector3(midpoint.X, (support + previousSupport) * .5f + SurfaceOffset, midpoint.Y));
        var slot = _cursor;
        _cursor = (_cursor + 1) % QuadBudget;
        _multimesh.SetInstanceTransform(slot, transform);
        var tint = .97f + .03f * Mathf.Sin(_metres * 2.3f + index);
        _multimesh.SetInstanceColor(slot, new Color(tint, tint, tint, 1f));
        _segments = Math.Min(_segments + 1, QuadBudget);
        _metres += length;
        UpdateWindow();
        SetMeta("vehicleTrackSegments", _segments);
        SetMeta("vehicleTrackMeters", _metres);
        return true;
    }

    /// <summary>Ice and open water are already built surfaces, not loose snow.</summary>
    private bool BlockedByIce(Vector2 point)
    {
        if (!_blockedBound) BindBlockedSurfaces();
        var support = float.NaN;
        for (var index = 0; index < _blockedSurfaces.Count; index++)
        {
            var mesh = _blockedSurfaces[index];
            if (!GodotObject.IsInstanceValid(mesh) || !mesh.IsVisibleInTree()) continue;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            if (point.X < box.Position.X || point.X > box.End.X
                || point.Y < box.Position.Z || point.Y > box.End.Z) continue;
            if (float.IsNaN(support)) support = (float)AgentBAct1HeightField.CollisionGround(point.X, point.Y);
            if (box.End.Y >= support - .03f && box.Position.Y < support + .4f) return true;
        }
        return false;
    }

    private void BindBlockedSurfaces()
    {
        _blockedBound = true;
        foreach (var mesh in Meshes(GetTree().Root))
        {
            if (mesh.Mesh is not ArrayMesh || !mesh.IsVisibleInTree()) continue;
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                if (mesh.GetActiveMaterial(surface)?.GetMeta("snowTrampleBlocked", false).AsBool() == true)
                { _blockedSurfaces.Add(mesh); break; }
        }
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        var count = node.GetChildCount();
        for (var index = 0; index < count; index++)
        {
            var child = node.GetChild(index);
            if (child is MeshInstance3D mesh) yield return mesh;
            foreach (var nested in Meshes(child)) yield return nested;
        }
    }

    private void UpdateWindow()
    {
        if (_vehicle is null) return;
        var origin = _vehicle.GlobalPosition;
        if (float.IsNaN(_fadeOrigin.X) || _fadeOrigin.DistanceTo(origin) > 1f)
        {
            _fadeOrigin = origin;
            _material.SetShaderParameter("fade_origin", origin);
        }
        if (float.IsNaN(_aabbOrigin.X)
            || new Vector2(_aabbOrigin.X - origin.X, _aabbOrigin.Z - origin.Z).Length() > AabbRefreshDistance)
        {
            _aabbOrigin = origin;
            var extent = FadeFar + 12f;
            _multimesh.CustomAabb = new Aabb(new Vector3(origin.X - extent, origin.Y - 40f, origin.Z - extent),
                new Vector3(extent * 2f, 80f, extent * 2f));
        }
    }

    private void ClearRuts()
    {
        for (var index = 0; index < _hasLast.Length; index++)
        { _hasLast[index] = false; _lastOffRoad[index] = false; _travelled[index] = 0f; }
        _cursor = 0;
        _segments = 0;
        _metres = 0f;
        for (var index = 0; index < QuadBudget; index++) _multimesh.SetInstanceTransform(index, Hidden);
        SetMeta("vehicleTrackSegments", 0);
        SetMeta("vehicleTrackMeters", 0f);
    }

    private static bool WithinTerrain(Vector2 point) =>
        point.X > AgentBAct1HeightField.MinX + 3f && point.X < AgentBAct1HeightField.MaxX - 3f
        && point.Y > AgentBAct1HeightField.MinZ + 3f && point.Y < AgentBAct1HeightField.MaxZ - 3f;

    // One material, one texture fetch: the trampled-snow albedo painted with
    // the same 0.90 + 0.12 * detail balance as PainterlyMaterialLibrary's snow
    // family. Soft edge alpha and a 32-58 m distance fade replace a per-stamp
    // mask write; nothing else is added to the low-spec frame.
    private const string RutShader = """
        shader_type spatial;
        render_mode blend_mix, depth_draw_never, cull_disabled, shadows_disabled, diffuse_burley, specular_schlick_ggx;
        uniform sampler2D snow_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform vec4 snow_tint : source_color = vec4(0.91, 0.93, 0.94, 1.0);
        uniform float snow_texture_scale = 1.6;
        uniform vec3 fade_origin = vec3(0.0);
        uniform float fade_near = 32.0;
        uniform float fade_far = 58.0;
        varying vec3 rut_world;
        varying vec4 rut_instance;
        void vertex() {
            rut_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            rut_instance = COLOR;
        }
        void fragment() {
            vec3 detail = texture(snow_texture, rut_world.xz * snow_texture_scale).rgb;
            float across = abs(UV.x - 0.5) * 2.0;
            float edge = 1.0 - smoothstep(0.55, 1.0, across);
            float press = 1.0 - smoothstep(0.30, 0.72, across);
            ALBEDO = snow_tint.rgb * (vec3(0.90) + detail * 0.12)
                * mix(vec3(1.0), rut_instance.rgb, 0.35) * mix(1.0, 0.93, press);
            ROUGHNESS = 0.81;
            SPECULAR = 0.25;
            METALLIC = 0.0;
            float fade = 1.0 - smoothstep(fade_near, fade_far, distance(rut_world.xz, fade_origin.xz));
            ALPHA = edge * fade;
        }
        """;
}
