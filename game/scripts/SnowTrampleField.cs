using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>Session-only snow presentation. Never changes colliders, player motion or saves.</summary>
public partial class SnowTrampleField : Node3D
{
    private const float WindowExtent = 24f;
    private const float StampSpacing = .55f;
    private const float FootLateralOffset = .09f;
    private const int HighResolution = 1024;
    private const int LowResolution = 512;
    private Image _mask = null!;
    private ImageTexture _texture = null!;
    private readonly List<(Vector2 Position, float Rotation, float Pack, float Height, float Depth)> _stamps = new();
    private readonly List<MeshInstance3D> _water = new();
    private readonly List<GroundSurface> _ground = new();
    private Vector2 _windowCentre;
    private Vector2? _lastPosition;
    private float _stepDistance;
    private int _footSide = 1;
    private int _transformRevision = -1;
    private int _totalStamps;
    private bool _enabled = true;
    private bool _surfacesBound;
    private CpuParticles3D? _puffs;
    private FirstPersonController? _player;

    public override void _Ready()
    {
        SetMeta("presentationOnly", true);
        SetMeta("visualOnly", true);
        SetMeta("collisionOwner", "none");
        SetMeta("navigationOwner", "none");
        SetMeta("interactionOwner", "none");
        SetMeta("runtimeStateOwnership", "RuntimeBridge");
        SetMeta("snowTramplePolicy", "session-only height/normal/compaction; never saved");
        var resolution = PainterlyMaterialLibrary.LowQualityMaterials ? LowResolution : HighResolution;
        // Signed metres in R/G, support-height times coverage in B, coverage in A.
        _mask = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgbah);
        _mask.Fill(new Color(0f, 0f, 0f, 0f));
        _texture = ImageTexture.CreateFromImage(_mask);
        _puffs = BuildPuffs();
        AddChild(_puffs);
        SetMeta("snowTrampleResolution", resolution);
        SetMeta("snowTrampleStampCount", 0);
        Callable.From(BindSurfaces).CallDeferred();
    }

    public void SetEnabled(bool enabled)
    {
        if (_enabled != enabled) ResetStep();
        _enabled = enabled;
        if (_puffs is not null) _puffs.Emitting = false;
    }

    private void ResetStep() { _lastPosition = null; _stepDistance = 0f; }

    public override void _ExitTree()
    {
        PainterlyMaterialLibrary.SetSnowTrample(null, Vector2.Zero, 0f);
        _texture?.Dispose();
        _mask?.Dispose();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_enabled) return;
        _player ??= GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (_player is null || !IsInstanceValid(_player)) return;
        if (_transformRevision != _player.PresentationTransformRevision)
        {
            _transformRevision = _player.PresentationTransformRevision;
            ResetStep();
        }
        var position = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
        if (!_player.IsOnFloor() || _player.ModalOpen) { ResetStep(); return; }
        if (_lastPosition is not Vector2 previous) { _lastPosition = position; return; }
        _lastPosition = position;
        var movement = position - previous;
        var distance = movement.Length();
        // Also reject direct debug teleports that bypass the shared spawn/load method.
        if (distance > Mathf.Max(.5f, _player.Velocity.Length() * (float)delta * 2f + .1f)) { ResetStep(); return; }
        if (distance < .0001f) return;
        _stepDistance += distance;
        if (_stepDistance < StampSpacing) return;
        _stepDistance %= StampSpacing;
        var direction = movement / distance;
        var foot = position + new Vector2(-direction.Y, direction.X) * (_footSide * FootLateralOffset);
        if (!TrySnowSupport(foot, out var support)) { ResetStep(); return; }
        _footSide = -_footSide;
        if (!_surfacesBound) BindSurfaces();
        var road = AgentBAct1HeightField.RoadInfo(foot.X, foot.Y);
        var packed = road.Distance < road.HalfWidth;
        var strength = .85f + .08f * Mathf.Sin(_totalStamps * 1.71f);
        _stamps.Add((foot, Mathf.Atan2(direction.Y, direction.X), strength, support, packed ? .010f : .035f));
        _totalStamps++;
        var rebuild = _totalStamps == 1 || position.DistanceTo(_windowCentre) > WindowExtent * .2f;
        if (rebuild)
        {
            var pixel = WindowExtent / _mask.GetWidth();
            _windowCentre = (position / pixel).Floor() * pixel;
        }
        var half = WindowExtent * .5f - .4f;
        rebuild |= _stamps.RemoveAll(stamp => Mathf.Abs(stamp.Position.X - _windowCentre.X) > half
            || Mathf.Abs(stamp.Position.Y - _windowCentre.Y) > half) > 0;
        // ponytail: at most 512 recent impressions in the 24 m presentation window.
        if (_stamps.Count > 512)
        {
            _stamps.RemoveRange(0, _stamps.Count - 512);
            rebuild = true;
        }
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        Redraw(rebuild);
        var redrawn = System.Diagnostics.Stopwatch.GetTimestamp();
        RefineGround(rebuild);
        SetMeta("snowTrampleRedrawMs", System.Diagnostics.Stopwatch.GetElapsedTime(started, redrawn).TotalMilliseconds);
        SetMeta("snowTrampleMeshMs", System.Diagnostics.Stopwatch.GetElapsedTime(redrawn).TotalMilliseconds);
        SetMeta("snowTrampleUpdateMs", System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        SetMeta("snowTrampleWindowCentre", _windowCentre);
        SetMeta("snowTrampleStampCount", _totalStamps);
        SetMeta("snowTrampleLastFootSide", -_footSide);
        SetMeta("snowTrampleLastPosition", foot);
        SetMeta("snowTrampleLastRotation", Mathf.Atan2(direction.Y, direction.X));
        if (_puffs is not null && !_player.ReducedMotion)
        {
            _puffs.GlobalPosition = new Vector3(foot.X, support + .025f, foot.Y);
            _puffs.Restart();
            _puffs.Emitting = true;
        }
    }

    private bool TrySnowSupport(Vector2 point, out float height)
    {
        height = 0;
        var origin = new Vector3(point.X, _player!.GlobalPosition.Y + .25f, point.Y);
        var ray = PhysicsRayQueryParameters3D.Create(origin, origin - Vector3.Up * .65f, 1u);
        ray.Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() is not Node body
            || body.GetMeta("collisionOwner", "").AsString() != "act1-exterior-terrain"
            || hit["normal"].AsVector3().Y < .7f) return false;
        height = hit["position"].AsVector3().Y;
        if (!_surfacesBound) BindSurfaces();
        foreach (var water in _water)
        {
            if (!water.IsVisibleInTree()) continue;
            var box = water.GlobalTransform * water.GetAabb();
            if (point.X >= box.Position.X && point.X <= box.End.X && point.Y >= box.Position.Z
                && point.Y <= box.End.Z && box.End.Y >= height - .03f && box.Position.Y < height + .4f) return false;
        }
        return true;
    }

    private void Redraw(bool rebuild)
    {
        if (rebuild) _mask.Fill(new Color(0f, 0f, 0f, 0f));
        var size = _mask.GetWidth();
        var pixel = WindowExtent / size;
        var origin = _windowCentre - Vector2.One * (WindowExtent * .5f);
        var minimum = rebuild ? 0f : GetMeta("snowTrampleMinHeight", 0f).AsSingle();
        var maximum = rebuild ? 0f : GetMeta("snowTrampleMaxHeight", 0f).AsSingle();
        // With the same origin and retained stamps, the old mask already
        // contains the exact prefix of the ordered raster operation.
        for (var index = rebuild ? 0 : _stamps.Count - 1; index < _stamps.Count; index++)
        {
            var stamp = _stamps[index];
            var centre = (stamp.Position - origin) / pixel;
            var radius = Mathf.CeilToInt(.22f / pixel);
            var cosine = Mathf.Cos(stamp.Rotation);
            var sine = Mathf.Sin(stamp.Rotation);
            for (var y = Mathf.Max(0, (int)centre.Y - radius); y <= Mathf.Min(size - 1, (int)centre.Y + radius); y++)
            for (var x = Mathf.Max(0, (int)centre.X - radius); x <= Mathf.Min(size - 1, (int)centre.X + radius); x++)
            {
                var offset = (new Vector2(x + .5f, y + .5f) - centre) * pixel;
                var along = (offset.X * cosine + offset.Y * sine) / .15f;
                var across = (-offset.X * sine + offset.Y * cosine) / .07f;
                var edge = Mathf.Pow(Mathf.Pow(Mathf.Abs(along), 4) + Mathf.Pow(Mathf.Abs(across), 4), .25f);
                if (edge >= 1.35f) continue;
                var sole = 1f - Mathf.SmoothStep(.7f, 1f, edge);
                var ridge = Mathf.Sin(Mathf.Clamp((edge - 1f) / .35f, 0, 1) * Mathf.Pi);
                var old = _mask.GetPixel(x, y);
                var coverage = Mathf.Clamp(old.A + Mathf.Max(sole, ridge) * stamp.Pack, 0, 1);
                var depression = Mathf.Min(old.R, -stamp.Depth * Mathf.Clamp(old.A + sole * stamp.Pack, 0, 1) * sole);
                var raised = sole > .05f ? 0 : Mathf.Max(old.G, ridge * .012f * stamp.Pack);
                if (depression < -.05f || depression > 0f || raised < 0f || raised > .02f)
                    throw new InvalidOperationException("Snow presentation height exceeds its centimetre bounds.");
                minimum = Mathf.Min(minimum, depression); maximum = Mathf.Max(maximum, raised);
                _mask.SetPixel(x, y, new Color(depression, raised, stamp.Height * coverage, coverage));
            }
        }
        SetMeta("snowTrampleMinHeight", minimum);
        SetMeta("snowTrampleMaxHeight", maximum);
        _texture.Update(_mask);
        PainterlyMaterialLibrary.SetSnowTrample(_texture, origin, WindowExtent);
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D mesh) yield return mesh;
            foreach (var nested in Meshes(child)) yield return nested;
        }
    }

    private void BindSurfaces()
    {
        if (_surfacesBound) return;
        _surfacesBound = true;
        foreach (var mesh in Meshes(GetTree().Root))
        {
            if (mesh.Mesh is not ArrayMesh original || !mesh.IsVisibleInTree()) continue;
            for (var s = 0; s < original.GetSurfaceCount(); s++)
            {
                if (mesh.GetActiveMaterial(s)?.GetMeta("snowTrampleBlocked", false).AsBool() == true) _water.Add(mesh);
            }
            var name = mesh.Name.ToString();
            if ((name == "Terrain_Main" || name.StartsWith("Road_", StringComparison.Ordinal))
                && mesh.GetMeta("supportOwner", "").AsString() == "AgentB_TerrainCollision")
                _ground.Add(new GroundSurface(mesh, original));
        }
    }

    // Subdivide the existing mesh only where the field needs vertices. The
    // collider is never touched; untouched triangles retain their original data.
    private sealed class GroundSurface
    {
        public readonly MeshInstance3D Node;
        public readonly ArrayMesh Original;
        public readonly List<(Vector3[] Vertices, Vector3[] Normals, Vector2[] Uv, Color[] Colors, int[] Indices)> Surfaces = new();
        public readonly List<(Vector3 Min, Vector3 Max)[]> Bounds = new();
        public readonly HashSet<(int Surface, int Triangle)> Refined = new();
        private Transform3D _boundsTransform;
        public GroundSurface(MeshInstance3D node, ArrayMesh original)
        {
            Node = node; Original = original;
            for (var s = 0; s < original.GetSurfaceCount(); s++)
            {
                var a = original.SurfaceGetArrays(s);
                var v = a[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var indices = a[(int)Mesh.ArrayType.Index].AsInt32Array();
                if (indices.Length == 0) indices = Enumerable.Range(0, v.Length).ToArray();
                Surfaces.Add((v, a[(int)Mesh.ArrayType.Normal].AsVector3Array(),
                    a[(int)Mesh.ArrayType.TexUV].AsVector2Array(), a[(int)Mesh.ArrayType.Color].AsColorArray(), indices));
            }
            RefreshBounds();
        }

        public bool RefreshBounds()
        {
            var transform = Node.GlobalTransform;
            if (Bounds.Count == Surfaces.Count && transform == _boundsTransform) return false;
            _boundsTransform = transform;
            Bounds.Clear();
            foreach (var data in Surfaces)
            {
                var bounds = new (Vector3 Min, Vector3 Max)[data.Indices.Length / 3];
                for (var t = 0; t < data.Indices.Length; t += 3)
                {
                    var a = transform * data.Vertices[data.Indices[t]];
                    var b = transform * data.Vertices[data.Indices[t + 1]];
                    var c = transform * data.Vertices[data.Indices[t + 2]];
                    bounds[t / 3] = (a.Min(b).Min(c), a.Max(b).Max(c));
                }
                Bounds.Add(bounds);
            }
            return true;
        }
    }

    private void RefineGround(bool rebuild)
    {
        var triangleCount = 0;
        var counts = new List<string>();
        foreach (var ground in _ground)
        {
            var reselect = ground.RefreshBounds() || rebuild;
            var selected = reselect ? new HashSet<(int Surface, int Triangle)>()
                : new HashSet<(int Surface, int Triangle)>(ground.Refined);
            var transform = ground.Node.GlobalTransform;
            for (var s = 0; s < ground.Surfaces.Count; s++)
            {
                var data = ground.Surfaces[s];
                for (var t = 0; t < data.Indices.Length; t += 3)
                {
                    var (min, max) = ground.Bounds[s][t / 3];
                    var half = WindowExtent * .5f;
                    if (min.X > _windowCentre.X + half || max.X < _windowCentre.X - half
                        || min.Z > _windowCentre.Y + half || max.Z < _windowCentre.Y - half) continue;
                    for (var stampIndex = reselect ? 0 : _stamps.Count - 1; stampIndex < _stamps.Count; stampIndex++)
                    {
                        var stamp = _stamps[stampIndex];
                        if (stamp.Position.X < min.X - .27f || stamp.Position.X > max.X + .27f
                            || stamp.Position.Y < min.Z - .27f || stamp.Position.Y > max.Z + .27f) continue;
                        selected.Add((s, t));
                        break;
                    }
                }
            }
            if (selected.SetEquals(ground.Refined)) continue;
            ground.Refined.Clear(); ground.Refined.UnionWith(selected);
            var previousCount = triangleCount;
            var rebuilt = new ArrayMesh();
            for (var s = 0; s < ground.Surfaces.Count; s++)
            {
                var data = ground.Surfaces[s];
                var vertices = new List<Vector3>(data.Vertices);
                var normals = data.Normals.Length == 0
                    ? Enumerable.Repeat(Vector3.Up, vertices.Count).ToList() : new List<Vector3>(data.Normals);
                var uv = new List<Vector2>(data.Uv);
                var colors = new List<Color>(data.Colors);
                var indices = new List<int>(data.Indices.Length);
                for (var t = 0; t < data.Indices.Length; t += 3)
                {
                    var i0 = data.Indices[t]; var i1 = data.Indices[t + 1]; var i2 = data.Indices[t + 2];
                    if (!selected.Contains((s, t)))
                    {
                        indices.Add(i0); indices.Add(i1); indices.Add(i2); triangleCount++;
                        continue;
                    }
                    var a = data.Vertices[i0]; var b = data.Vertices[i1]; var c = data.Vertices[i2];
                    var edge = Mathf.Max((transform.Basis * (a - b)).Length(), Mathf.Max((transform.Basis * (b - c)).Length(), (transform.Basis * (c - a)).Length()));
                    var divisions = Mathf.CeilToInt(edge / .06f);
                    var first = vertices.Count;
                    int Index(int u, int v) => first + u * (divisions + 1) - u * (u - 1) / 2 + v;
                    for (var u = 0; u <= divisions; u++)
                    for (var v = 0; v <= divisions - u; v++)
                    {
                        var wb = (float)u / divisions; var wc = (float)v / divisions; var wa = 1f - wb - wc;
                        vertices.Add(a * wa + b * wb + c * wc);
                        normals.Add((normals[i0] * wa + normals[i1] * wb + normals[i2] * wc).Normalized());
                        if (data.Uv.Length > 0) uv.Add(data.Uv[i0] * wa + data.Uv[i1] * wb + data.Uv[i2] * wc);
                        if (data.Colors.Length > 0) colors.Add(data.Colors[i0] * wa + data.Colors[i1] * wb + data.Colors[i2] * wc);
                    }
                    for (var u = 0; u < divisions; u++)
                    for (var v = 0; v < divisions - u; v++)
                    {
                        indices.Add(Index(u, v)); indices.Add(Index(u + 1, v)); indices.Add(Index(u, v + 1)); triangleCount++;
                        if (u + v < divisions - 1)
                        {
                            indices.Add(Index(u + 1, v)); indices.Add(Index(u + 1, v + 1)); indices.Add(Index(u, v + 1)); triangleCount++;
                        }
                    }
                }
                var arrays = new global::Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
                arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
                arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
                if (uv.Count > 0) arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
                if (colors.Count > 0) arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
                rebuilt.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                rebuilt.SurfaceSetMaterial(s, ground.Original.SurfaceGetMaterial(s));
            }
            var previous = ground.Node.Mesh;
            ground.Node.Mesh = rebuilt;
            if (previous != ground.Original) previous?.Dispose();
            ground.Node.ExtraCullMargin = .06f;
            counts.Add($"{ground.Node.Name}:{triangleCount - previousCount}");
        }
        SetMeta("snowTrampleRebuiltTriangles", triangleCount);
        SetMeta("snowTrampleGeometry", string.Join(",", counts));
    }

    private static CpuParticles3D BuildPuffs()
    {
        var quad = new QuadMesh { Size = new Vector2(0.12f, 0.12f) };
        quad.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.96f, 0.99f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        return new CpuParticles3D
        {
            Name = "SnowStepPuffs",
            Amount = 10,
            OneShot = true,
            Explosiveness = 0.96f,
            Lifetime = 0.55,
            LocalCoords = false,
            Mesh = quad,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.16f,
            Direction = new Vector3(0f, 1f, 0f),
            Spread = 55f,
            Gravity = new Vector3(0f, -1.6f, 0f),
            InitialVelocityMin = 0.35f,
            InitialVelocityMax = 0.9f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.2f,
            Emitting = false
        };
    }
}
