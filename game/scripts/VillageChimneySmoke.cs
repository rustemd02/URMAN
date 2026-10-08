using Godot;

namespace Urman.Godot;

/// <summary>
/// Bounded, presentation-only chimney plumes for the inhabited village. The
/// pool is a fixed EmitterBudget of emitters, never a node per house: only the
/// nearest houses to the viewer emit, every other candidate keeps its geometry
/// but no particles, and a building without a real visible chimney mesh gets
/// nothing. Selection refreshes twice per second; an ordinary frame is a couple
/// of scalar compares, so the system allocates nothing and touches no house
/// outside those refreshes. One shared material, one quad and one 64x64 radial
/// texture serve every emitter.
/// </summary>
public partial class VillageChimneySmoke : Node3D
{
    public const int EmitterBudget = 14;
    public const int ParticlesPerEmitter = 10;
    public const int ParticleBudget = EmitterBudget * ParticlesPerEmitter;
    public const float SelectionInterval = .5f;
    public const float MaxSelectionDistance = 85f;
    // Squared-distance stickiness: a bound chimney keeps its slot unless a
    // challenger is clearly closer (~15% linear), which stops churn when the
    // viewer walks along the boundary between two rows of houses.
    private const float IncumbentFactor = .72f;
    private const float MaxSelectionDistanceSquared = MaxSelectionDistance * MaxSelectionDistance;

    private readonly CpuParticles3D[] _emitters = new CpuParticles3D[EmitterBudget];
    private readonly int[] _slotChimney = new int[EmitterBudget];
    private readonly int[] _nearest = new int[EmitterBudget];
    private readonly float[] _distances = new float[EmitterBudget];
    private int[] _boundSlot = [];
    private Chimney[] _chimneys = [];
    private StandardMaterial3D? _plumeMaterial;
    private double _tick = SelectionInterval;
    private int _emitterCount;
    private bool _present;
    private bool _animating = true;

    public int ChimneyCount => _chimneys.Length;
    public int EmitterCount => _emitterCount;

    private readonly struct Chimney
    {
        public readonly Vector3 Top;
        public readonly Node3D Owner;
        public readonly string Path;
        public Chimney(Vector3 top, Node3D owner, string path)
        {
            Top = top; Owner = owner; Path = path;
        }
    }

    // VIS-043: the plume must not contradict the falling snow. The wind truth is
    // the authored 'weather' block of the applied atmosphere profile, which is a
    // transcription of the existing weather owner's own emitter constants
    // (AgentBAct1ExteriorLayer: direction (-1, -.28, .22), 6–10 / 11–16 m/s).
    // This system reads it; it never writes to the snow emitter and is not a
    // second weather or atmosphere owner. Today's plume leaned to +X while the
    // powder travelled to -X, which is exactly the contradiction the card rejects.
    private const float PlumeBendPerWindUnit = .02f;
    private const float PlumeInitialLeaning = .20f;
    private const float ReferenceWindSpeed = 8f;
    private string _windProfileId = string.Empty;
    private Vector2 _windDirection = AtmosphereProfiles.AuthoredWindDirection;
    private float _windSpeed = AtmosphereProfiles.AuthoredWindSpeed;
    private Color _plumeColor = AtmosphereProfiles.DefaultSmokeColor;
    private float _plumeOpacity = AtmosphereProfiles.DefaultSmokeOpacity;

    /// <summary>
    /// Discovers real chimney tops once. <paramref name="existingSmokeRoot"/>
    /// is the village-life layer whose authored plumes already smoke their own
    /// chimneys; their recorded paths are skipped so one chimney never receives
    /// two emitters. Chimneys are accepted only on residential owner nodes the
    /// household director counted as inhabited, and only when the mesh is
    /// visible and actually emerges above every overlapping roof.
    /// </summary>
    public void Initialize(Node3D world, VillageHouseholdDirector households, Node3D? existingSmokeRoot, Vector3 initialViewer)
    {
        if (_emitters[0] is not null) return;
        var claimed = new List<string>(4);
        if (existingSmokeRoot is not null)
            foreach (var child in existingSmokeRoot.GetChildren())
                if (child is CpuParticles3D && child.HasMeta("chimneyOwner"))
                    claimed.Add(child.GetMeta("chimneyOwner").AsString());

        var candidates = new List<Chimney>(32);
        var ownerIndex = new Dictionary<ulong, int>(32);
        var roofs = new List<MeshInstance3D>(64);
        foreach (var mesh in world.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var resource = mesh.Mesh;
            if (resource is null || !mesh.IsVisibleInTree()) continue;
            var name = mesh.Name.ToString();
            if (name.Contains("Chimney", StringComparison.OrdinalIgnoreCase))
            {
                // Stack and cap share a dwelling; one candidate per house, at
                // the highest real top. A building with no chimney node simply
                // never enters the list.
                var owner = VillageHouseholdDirector.ResidentialOwner(mesh);
                if (owner is null || !households.IsInhabitedOwner(owner)) continue;
                var path = mesh.GetPath().ToString();
                if (claimed.Contains(path)) continue;
                var bounds = mesh.GlobalTransform * resource.GetAabb();
                var top = bounds.GetCenter();
                top.Y = bounds.End.Y;
                if (ownerIndex.TryGetValue(owner.GetInstanceId(), out var known))
                {
                    if (top.Y > candidates[known].Top.Y) candidates[known] = new Chimney(top, owner, path);
                    continue;
                }
                ownerIndex.Add(owner.GetInstanceId(), candidates.Count);
                candidates.Add(new Chimney(top, owner, path));
                continue;
            }
            if (name.Contains("Roof", StringComparison.OrdinalIgnoreCase)) roofs.Add(mesh);
        }
        RejectCoveredOutlets(candidates, roofs);
        _chimneys = [.. candidates];
        Array.Fill(_slotChimney, -1);
        _boundSlot = new int[_chimneys.Length];
        Array.Fill(_boundSlot, -1);

        BuildPool();
        SetMeta("presentationOnly", true);
        SetMeta("chimneySmokeBudget", EmitterBudget);
        SetMeta("chimneySmokeParticleBudget", ParticleBudget);
        SetMeta("chimneySmokeChimneys", _chimneys.Length);
        SetMeta("chimneySmokeEmitters", 0);
        SetMeta("chimneySmokeParticles", 0);
        // The plume starts in the weather the atmosphere owner already applied, so
        // a jump straight into the night edge never shows a daytime drift.
        ApplyAtmosphereState();
        _present = true;
        Select(initialViewer);
        GD.Print($"village-chimney-smoke: chimneys={ChimneyCount} emitters={EmitterCount} particles={EmitterCount * ParticlesPerEmitter}");
    }

    /// <summary>
    /// Mirrors the authored village-life plume (amount reduced to the pool
    /// budget) so the two accepted start-yard plumes and the village pool read
    /// as one material. All emitters share the quad, its material and the
    /// texture; the curve and ramp are read-only shared resources.
    /// </summary>
    private void BuildPool()
    {
        var radial = new GradientTexture2D
        {
            Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(.5f, 1f),
            Gradient = new Gradient { Colors = [Colors.White, new Color(1f, 1f, 1f, 0f)], Offsets = [0f, 1f] }
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(_plumeColor.R, _plumeColor.G, _plumeColor.B, _plumeOpacity),
            AlbedoTexture = radial,
            VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 1f
        };
        _plumeMaterial = material;
        var quad = new QuadMesh { Size = Vector2.One, Material = material };
        var scale = new Curve();
        scale.AddPoint(new Vector2(0f, .20f));
        scale.AddPoint(new Vector2(1f, 1.65f));
        var ramp = new Gradient
        {
            Offsets = [0f, .15f, .65f, 1f],
            Colors = [new Color(1f, 1f, 1f, 0f), Colors.White, new Color(1f, 1f, 1f, .55f), new Color(1f, 1f, 1f, 0f)]
        };
        var (initialDirection, initialGravity) = PlumeTrajectory(_windDirection, _windSpeed);
        for (var slot = 0; slot < EmitterBudget; slot++)
        {
            var emitter = new CpuParticles3D
            {
                Name = $"ChimneySmoke{slot}", Emitting = false,
                Amount = ParticlesPerEmitter, Lifetime = 6.5, Preprocess = 4, LifetimeRandomness = .35f,
                LocalCoords = false, Mesh = quad,
                Direction = initialDirection, Spread = 8f,
                Gravity = initialGravity,
                InitialVelocityMin = .45f, InitialVelocityMax = .65f,
                ScaleAmountMin = .8f, ScaleAmountMax = 1.1f, ScaleAmountCurve = scale,
                ColorRamp = ramp, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(emitter);
            _emitters[slot] = emitter;
        }
    }

    /// <summary>
    /// The plume trajectory for a wind state: it leaves the chimney almost
    /// vertically with a small downwind lean, then the same horizontal component
    /// keeps bending it downwind, scaled by the wind speed. Both vectors stay
    /// within the previous magnitude, so the plume height and spread are
    /// unchanged; only its leaning side follows the powder (VIS-043).
    /// </summary>
    private static (Vector3 Direction, Vector3 Gravity) PlumeTrajectory(Vector2 wind, float speed)
    {
        var bend = Mathf.Min(speed / ReferenceWindSpeed, 2f) * PlumeBendPerWindUnit * speed;
        var lean = PlumeInitialLeaning * Mathf.Min(speed / ReferenceWindSpeed, 1.5f);
        return (
            new Vector3(wind.X * lean, 1f, wind.Y * lean),
            new Vector3(wind.X * bend, .04f, wind.Y * bend));
    }

    /// <summary>
    /// Re-reads the applied atmosphere state once per call and re-asserts the
    /// plume only when it actually changed: the profile is written by the single
    /// atmosphere owner on a zone switch, so an ordinary frame costs one string
    /// comparison and nothing is allocated.
    /// </summary>
    private void ApplyAtmosphereState()
    {
        var profile = AtmosphereProfiles.Applied;
        var profileId = AtmosphereProfiles.AppliedProfileId;
        if (profile is null) return;
        if (string.Equals(profileId, _windProfileId, StringComparison.Ordinal)
            && profile.WindDirection == _windDirection && profile.WindSpeed == _windSpeed
            && profile.SmokeColor == _plumeColor && profile.SmokeOpacity == _plumeOpacity) return;
        _windProfileId = profileId;
        _windDirection = profile.WindDirection;
        _windSpeed = profile.WindSpeed;
        _plumeColor = profile.SmokeColor;
        _plumeOpacity = profile.SmokeOpacity;
        if (_plumeMaterial is not null)
            _plumeMaterial.AlbedoColor = new Color(_plumeColor.R, _plumeColor.G, _plumeColor.B, _plumeOpacity);
        var (direction, gravity) = PlumeTrajectory(_windDirection, _windSpeed);
        for (var slot = 0; slot < EmitterBudget; slot++)
        {
            var emitter = _emitters[slot];
            if (emitter is null) continue;
            if (emitter.Direction != direction) emitter.Direction = direction;
            if (emitter.Gravity != gravity) emitter.Gravity = gravity;
        }
        SetMeta("chimneySmokeWind", $"{_windDirection.X:0.###},{_windDirection.Y:0.###}");
        SetMeta("chimneySmokeWindSpeed", _windSpeed);
        SetMeta("chimneySmokePlumeColor", _plumeColor.ToHtml());
        SetMeta("chimneySmokeProfile", _windProfileId);
    }

    /// <summary>
    /// Rejects outlets a taller overlapping roof actually covers, using the
    /// same real-face ray test the authored village-life plumes use: an
    /// AABB-only test would either smoke through a roof or drop good chimneys.
    /// Init-time only; the face arrays are fetched once per touched roof.
    /// </summary>
    private static void RejectCoveredOutlets(List<Chimney> candidates, List<MeshInstance3D> roofs)
    {
        if (candidates.Count == 0 || roofs.Count == 0) return;
        var covered = new bool[candidates.Count];
        var rejected = false;
        foreach (var roof in roofs)
        {
            var resource = roof.Mesh;
            if (resource is null || !roof.IsVisibleInTree()) continue;
            var bounds = roof.GlobalTransform * resource.GetAabb();
            Vector3[]? faces = null;
            for (var index = 0; index < candidates.Count; index++)
            {
                if (covered[index]) continue;
                var top = candidates[index].Top;
                if (top.X < bounds.Position.X || top.X > bounds.End.X
                    || top.Z < bounds.Position.Z || top.Z > bounds.End.Z
                    || bounds.End.Y <= top.Y + .03f) continue;
                faces ??= resource.GetFaces();
                if (faces.Length == 0) break;
                var from = roof.ToLocal(top + Vector3.Up * .03f);
                var direction = roof.GlobalBasis.Inverse() * Vector3.Up;
                for (var face = 0; face < faces.Length; face += 3)
                    if (Geometry3D.RayIntersectsTriangle(from, direction, faces[face], faces[face + 1], faces[face + 2]).VariantType != Variant.Type.Nil)
                    {
                        covered[index] = true; rejected = true; break;
                    }
            }
        }
        if (!rejected) return;
        for (var index = candidates.Count - 1; index >= 0; index--)
            if (covered[index]) candidates.RemoveAt(index);
    }

    /// <summary>
    /// Called from the world process, at most once per frame. Outside the
    /// village zone the whole pool hides; under reduced motion the emitters
    /// freeze instead (the authored accessibility contract the village-life
    /// plumes already follow). Nothing here allocates.
    /// </summary>
    public void Tick(double delta, Vector3 viewer, bool present, bool animate)
    {
        if (_emitters[0] is null) return;
        if (!present)
        {
            if (!_present) return;
            _present = false;
            Visible = false;
            for (var slot = 0; slot < EmitterBudget; slot++)
            {
                if (_emitters[slot].Emitting) _emitters[slot].Emitting = false;
                Unbind(slot);
            }
            UpdateCounters();
            return;
        }
        if (!_present)
        {
            _present = true;
            Visible = true;
            _tick = SelectionInterval;
        }
        if (!animate)
        {
            if (_animating) { _animating = false; SetSpeedScale(0f); }
            return;
        }
        if (!_animating) { _animating = true; SetSpeedScale(1f); }
        // Cheap: one string comparison plus four value comparisons against the last
        // applied state. Only a real atmosphere change walks the fourteen emitters.
        ApplyAtmosphereState();
        _tick += delta;
        if (_tick < SelectionInterval) return;
        _tick = 0;
        Select(viewer);
    }

    /// <summary>
    /// Rebuilds the nearest set with the same bounded insertion pass the window
    /// spill pool uses, then rebinds only the slots whose chimney changed.
    /// A dropped chimney stops emitting but keeps its fading particles, so a
    /// boundary crossing never pops a plume out; the slot's next emission
    /// appears at the new top while the old tail dissolves (LocalCoords=false).
    /// </summary>
    private void Select(Vector3 viewer)
    {
        Array.Fill(_distances, MaxSelectionDistanceSquared);
        Array.Fill(_nearest, -1);
        for (var index = 0; index < _chimneys.Length; index++)
        {
            var chimney = _chimneys[index];
            if (!chimney.Owner.IsVisibleInTree()) continue;
            var distance = viewer.DistanceSquaredTo(chimney.Top);
            if (distance >= MaxSelectionDistanceSquared) continue;
            if (_boundSlot[index] >= 0) distance *= IncumbentFactor;
            for (var slot = 0; slot < EmitterBudget; slot++)
            {
                if (distance >= _distances[slot]) continue;
                for (var shift = EmitterBudget - 1; shift > slot; shift--)
                {
                    _distances[shift] = _distances[shift - 1];
                    _nearest[shift] = _nearest[shift - 1];
                }
                _distances[slot] = distance;
                _nearest[slot] = index;
                break;
            }
        }
        for (var slot = 0; slot < EmitterBudget; slot++)
        {
            var next = _nearest[slot];
            if (_slotChimney[slot] == next) continue;
            Unbind(slot);
            if (next >= 0) Bind(slot, next);
        }
        UpdateCounters();
    }

    private void Bind(int slot, int index)
    {
        var chimney = _chimneys[index];
        var emitter = _emitters[slot];
        emitter.GlobalPosition = chimney.Top;
        emitter.SetMeta("chimneyOwner", chimney.Path);
        emitter.Emitting = true;
        _slotChimney[slot] = index;
        _boundSlot[index] = slot;
    }

    private void Unbind(int slot)
    {
        var index = _slotChimney[slot];
        if (index < 0) return;
        _boundSlot[index] = -1;
        _slotChimney[slot] = -1;
        if (_emitters[slot].Emitting) _emitters[slot].Emitting = false;
    }

    private void UpdateCounters()
    {
        var bound = 0;
        for (var slot = 0; slot < EmitterBudget; slot++)
            if (_slotChimney[slot] >= 0) bound++;
        if (bound == _emitterCount) return;
        _emitterCount = bound;
        SetMeta("chimneySmokeEmitters", bound);
        SetMeta("chimneySmokeParticles", bound * ParticlesPerEmitter);
    }

    private void SetSpeedScale(float value)
    {
        for (var slot = 0; slot < EmitterBudget; slot++)
            if (_emitters[slot].SpeedScale != value) _emitters[slot].SpeedScale = value;
    }
}
