using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// VIS-076 — world-space wind streaks (reference V4).
///
/// What this is: a small pool of thin ribbons that describe the movement of the
/// air. They are anchored in the world, oriented by the wind vector the existing
/// weather owner already publishes, depth-tested against trunks and tinted by the
/// atmosphere's own fog colour. Nothing here reads the camera basis, nothing is
/// drawn in canvas or HUD space, and no object gets an edge: the recipe's ban on
/// outline/line art and the card's ban on anime speed-lines are the same ban.
///
/// What this is not: a second weather owner. Direction and speed come from a
/// sampler the exterior layer injects, which reads the single snow emitter — the
/// same values <c>SetOpeningBlizzard</c> writes — so the streaks cannot disagree
/// with the storm. This node writes no Environment, no sky, no sun, no foreign
/// material and no gameplay state.
///
/// Rarity is engineered, not hoped for: a hard pool ceiling, a per-region visible
/// cap, and a rest gap after every lifetime, so the live count sits well below the
/// cap and never becomes the constant noise V4 forbids.
///
/// Disable test (recipe §7): <c>URMAN_WIND_STREAKS=off</c>, or
/// <see cref="SetPresentationEnabled"/>(false), or the player's reduced-motion
/// setting. In each case the frame is the one this feature never existed in, and
/// only the wind-state information leaves the picture: nothing else reads this node.
/// </summary>
public partial class AgentBWindStreaks : Node3D
{
    // The card's budget: "одновременно <=8-16 заметных линий". The pool is the hard
    // ceiling; the per-region cap is what may be alive at once, so a village frame
    // carries the delicate end of the range and the forest rim the stranger end.
    internal const int PoolSize = 16;
    internal const int VillageVisibleCap = 8;
    internal const int ForestVisibleCap = 16;

    // Card's start range: lifetime 0.6-2.0 s.
    internal const float VillageLifetimeMin = 1.1f;
    internal const float VillageLifetimeMax = 2.0f;
    internal const float ForestLifetimeMin = .6f;
    internal const float ForestLifetimeMax = 1.3f;

    // Rest gap between two consecutive ribbons of one slot. Together with the
    // lifetimes this gives an expected live count of roughly 4-6 in the village and
    // 6-9 at the forest rim, under the caps above.
    internal const float VillageRestGapMin = 1.0f;
    internal const float VillageRestGapMax = 2.6f;
    internal const float ForestRestGapMin = .7f;
    internal const float ForestRestGapMax = 1.9f;

    // Alpha ceilings, deliberately low: a streak that has to be noticed is a streak
    // that has become an interface.
    internal const float VillageAlphaCeiling = .13f;
    internal const float ForestAlphaCeiling = .21f;

    internal const float VillageWidth = .045f;
    internal const float ForestWidth = .075f;
    internal const float VillageLengthMin = .9f;
    internal const float VillageLengthMax = 1.9f;
    internal const float ForestLengthMin = 1.8f;
    internal const float ForestLengthMax = 3.8f;

    /// <summary>
    /// Below this wind speed the air is not moving enough to be drawn. The existing
    /// weather owner runs at 8 m/s in ordinary snowfall and 13.5 m/s in the opening
    /// blizzard, so the gate reads the storm rather than freezing the motif off.
    /// </summary>
    internal const float MinimumWindSpeed = 4.2f;

    internal const float ShellNearDistance = 7f;
    internal const float ShellFarDistance = 24f;
    internal const float ShellKillDistance = 30f;

    /// <summary>Unit ribbon width the pool mesh is built at; per-slot width scales it.</summary>
    private const float BaseWidth = .06f;

    private const string StreakShaderSource = """
        shader_type spatial;
        // Unshaded but not emissive: the streak is a thin slice of moving air, so it
        // keeps the scene's fog tint and still disappears behind a trunk. Fog is left
        // enabled on purpose — a distant streak must dissolve in the mist like
        // everything else in the world, which is what an HUD overlay cannot do.
        render_mode unshaded, cull_disabled, depth_draw_never;

        uniform vec4 streak_tint : source_color = vec4(0.86, 0.90, 0.94, 0.13);
        // How much of the ribbon's half-width is core rather than feathered edge. A
        // wide feather is exactly what stops this reading as a drawn line.
        uniform float streak_softness = 0.42;
        instance uniform float streak_alpha = 0.0;

        void fragment() {
            float across = abs(UV.x * 2.0 - 1.0);
            float along = abs(UV.y * 2.0 - 1.0);
            float core = 1.0 - smoothstep(streak_softness, 1.0, across);
            float taper = 1.0 - smoothstep(0.30, 1.0, along);
            ALBEDO = streak_tint.rgb;
            ALPHA = streak_tint.a * core * taper * streak_alpha;
        }
        """;

    private readonly record struct Slot(
        Vector3 Position,
        float Age,
        float Lifetime,
        float Rest,
        float Length,
        float Width,
        float Roll,
        float SpeedFactor,
        bool Alive);

    private readonly MeshInstance3D[] _lines = new MeshInstance3D[PoolSize];
    private readonly Slot[] _slots = new Slot[PoolSize];
    private readonly RandomNumberGenerator _rng = new() { Seed = 20261008 };
    private readonly Func<Vector3> _windSampler;
    private ShaderMaterial? _material;
    private bool _poolBuilt;
    private bool _presentationEnabled = true;
    private bool _forestRim;
    private int _liveCount;
    private Color _worldTint = new(.86f, .90f, .94f);
    private string _tintSource = "authored-default";
    // Change-only metadata cache, so the readback never becomes the cost.
    private string? _lastMode;
    private bool? _lastReducedMotion;
    private bool? _lastAllowed;
    private float _lastWindSpeed = float.MaxValue;
    private Vector3 _lastWind = new(float.MaxValue, float.MaxValue, float.MaxValue);

    /// <summary>
    /// Parameterless form exists only so the class stays attachable as a script; the
    /// world always uses the sampler constructor, and without a sampler the wind reads
    /// as still air, which draws nothing.
    /// </summary>
    public AgentBWindStreaks() : this(() => Vector3.Zero)
    {
    }

    public AgentBWindStreaks(Func<Vector3> windSampler)
    {
        _windSampler = windSampler;
        Name = "AgentBWindStreaks";
        SetMeta("presentationOnly", true);
        SetMeta("visualOnly", true);
        SetMeta("featureId", "VIS-076");
        SetMeta("referenceCode", "V4");
        SetMeta("abstractionKind", "world-space wind streaks");
        SetMeta("windBinding", "direction and speed sampled from the single exterior weather owner (AgentBSnow)");
        SetMeta("interactionOwner", "none");
        SetMeta("navigationOwner", "none");
        SetMeta("collisionOwner", "none");
        SetMeta("lightingOwner", "none");
        SetMeta("screenSpace", false);
        SetMeta("billboard", false);
        SetMeta("outline", false);
        SetMeta("poolSize", PoolSize);
        SetMeta("visibleCapVillage", VillageVisibleCap);
        SetMeta("visibleCapForest", ForestVisibleCap);
        SetMeta("lifetimeRangeSeconds", new[] { ForestLifetimeMin, VillageLifetimeMax });
        SetMeta("alphaCeilingVillage", VillageAlphaCeiling);
        SetMeta("alphaCeilingForest", ForestAlphaCeiling);
        SetMeta("minimumWindSpeed", MinimumWindSpeed);
        SetMeta("shellDistancesMeters", new[] { ShellNearDistance, ShellFarDistance, ShellKillDistance });
    }

    /// <summary>Session override for the disable test: auto / on / off.</summary>
    internal static string WindStreakMode =>
        OS.GetEnvironment("URMAN_WIND_STREAKS") switch
        {
            "off" or "0" => "off",
            "on" or "1" => "on",
            _ => "auto"
        };

    private float AlphaCeiling => _forestRim ? ForestAlphaCeiling : VillageAlphaCeiling;
    private int VisibleCap => _forestRim ? ForestVisibleCap : VillageVisibleCap;

    /// <summary>One shared unshaded material and one shared quad for the whole pool.</summary>
    private ShaderMaterial StreakMaterial()
    {
        if (_material is { } existing) return existing;
        var material = new ShaderMaterial
        {
            Shader = new Shader { Code = StreakShaderSource, ResourceName = "urman_wind_streak" },
            ResourceName = "urman_wind_streak"
        };
        _material = material;
        material.SetShaderParameter("streak_softness", .42f);
        PushTint();
        return material;
    }

    private void PushTint()
    {
        _material?.SetShaderParameter("streak_tint",
            new Color(_worldTint.R, _worldTint.G, _worldTint.B, AlphaCeiling));
        SetMeta("materialAlphaCeiling", AlphaCeiling);
    }

    /// <summary>
    /// The atmosphere owner's fog colour is the world's own air tint; reading it is
    /// what makes the streaks belong to the scene instead of floating above it. This
    /// node never writes back to the Environment.
    /// </summary>
    public void SetWorldTint(Color fogColor, string source)
    {
        var lifted = fogColor.Lightened(.22f);
        if (lifted == _worldTint && source == _tintSource) return;
        _worldTint = lifted;
        _tintSource = source;
        SetMeta("worldTintSource", source);
        SetMeta("worldTint", lifted);
        PushTint();
    }

    /// <summary>Exterior presentation gate: interiors and disabled exterior hide all.</summary>
    public void SetPresentationEnabled(bool enabled)
    {
        _presentationEnabled = enabled;
        SetMeta("presentationEnabled", enabled);
        if (!enabled) ClearAll();
    }

    /// <summary>Village register stays delicate; the forest rim may be stranger.</summary>
    public void SetRegionMode(bool forestRim)
    {
        if (_forestRim == forestRim) return;
        _forestRim = forestRim;
        SetMeta("regionMode", forestRim ? "kara-forest-rim" : "village");
        PushTint();
    }

    /// <summary>
    /// Pool construction: one shared quad, one shared material, sixteen instances
    /// that start parked. Called once by the exterior layer right after the snow
    /// emitter exists, because the wind sampler reads that emitter.
    /// </summary>
    internal void BuildPool()
    {
        var quad = new QuadMesh { Size = new Vector2(BaseWidth, 1f) };
        var material = StreakMaterial();
        for (var index = 0; index < PoolSize; index++)
        {
            var line = new MeshInstance3D
            {
                Name = $"WindStreak{index}",
                Mesh = quad,
                MaterialOverride = material,
                Visible = false,
                // Air casts no shadow: the streak must not add a shadow caster to the
                // budget VIS-029 is trying to lower.
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            line.SetMeta("presentationOnly", true);
            line.SetMeta("visualOnly", true);
            line.SetMeta("featureId", "VIS-076");
            AddChild(line);
            _lines[index] = line;
            // Staggered first rest so the first allowed frame does not light all
            // available ribbons at once; a burst is precisely the pop the envelope
            // elsewhere is designed to avoid.
            var firstRest = 2.2f * index / PoolSize;
            _slots[index] = new Slot(Vector3.Zero, 0f, 1f, firstRest, BaseWidth, BaseWidth, 0f, .4f, false);
        }

        SetMeta("sharedMeshCount", 1);
        SetMeta("sharedMaterialCount", 1);
        SetMeta("streakShadowCasting", "off");
        SetMeta("worldTint", _worldTint);
        SetMeta("worldTintSource", _tintSource);
        _poolBuilt = true;
    }

    /// <summary>
    /// Called from the exterior layer's own process pass. This node owns no timer of
    /// its own, so it cannot keep running when the layer is switched off.
    /// </summary>
    public void Refresh(Vector3 focus, double deltaSeconds)
    {
        if (!_poolBuilt) return;
        var wind = _windSampler();
        var speed = wind.Length();
        var reducedMotion = IsReducedMotionRequested();
        var mode = WindStreakMode;
        // "on" is only ever set by an explicit capture run; it forces the motif even
        // under reduced motion, so the A/B can show the feature itself. An ordinary
        // launch never sets it.
        var allowed = mode == "on"
            || mode != "off" && _presentationEnabled && !reducedMotion && speed >= MinimumWindSpeed;
        // Metas are written on change only, the way the snow uniforms are cached: a
        // per-frame SetMeta would make the readback itself the cost of this feature.
        if (_lastMode != mode || _lastReducedMotion != reducedMotion || _lastAllowed != allowed)
        {
            _lastMode = mode;
            _lastReducedMotion = reducedMotion;
            _lastAllowed = allowed;
            SetMeta("sessionMode", mode);
            SetMeta("reducedMotionBlocked", reducedMotion);
            SetMeta("windGatePassed", allowed);
        }

        if (Mathf.Abs(speed - _lastWindSpeed) > .25f)
        {
            _lastWindSpeed = speed;
            SetMeta("currentWindSpeed", speed);
        }

        if (!allowed)
        {
            ClearAll();
            return;
        }

        var direction = wind / speed;
        var delta = (float)deltaSeconds;
        var cap = VisibleCap;
        var live = 0;
        for (var index = 0; index < PoolSize; index++)
        {
            var line = _lines[index];
            var slot = _slots[index];
            if (index >= cap)
            {
                // Over the region cap: parked with a deterministic stagger, so a later
                // cap increase (walking from the village to the forest rim) recovers
                // ribbons one after another instead of lighting the whole ceiling in a
                // single frame. No RNG value is consumed here, so a replay of the same
                // frame count produces the same set.
                _slots[index] = new Slot(Vector3.Zero, 0f, 1f, .35f + .17f * (index - cap),
                    BaseWidth, BaseWidth, 0f, .4f, false);
                line.Visible = false;
                continue;
            }

            if (slot.Alive)
            {
                var age = slot.Age + delta;
                var moved = slot.Position + direction * speed * slot.SpeedFactor * delta;
                var floor = (float)AgentBAct1HeightField.Ground(moved.X, moved.Z) + .35f;
                if (moved.Y < floor) moved = moved with { Y = floor };
                if (age >= slot.Lifetime || focus.DistanceSquaredTo(moved) > ShellKillDistance * ShellKillDistance)
                {
                    // Rest, do not immediately replace: the gap is what makes the set
                    // read as gusts of air instead of a permanent lattice.
                    _slots[index] = slot with { Alive = false, Age = 0f };
                    line.Visible = false;
                    continue;
                }

                slot = slot with { Age = age, Position = moved };
                _slots[index] = slot;
                live++;
            }
            else if (slot.Rest > 0f)
            {
                var rest = slot.Rest - delta;
                _slots[index] = slot with { Rest = Mathf.Max(rest, 0f) };
                line.Visible = false;
                continue;
            }
            else
            {
                slot = Spawn(focus, direction);
                _slots[index] = slot;
                live++;
            }

            var life = Mathf.Clamp(slot.Age / Mathf.Max(slot.Lifetime, .0001f), 0f, 1f);
            // Soft in, softer out: a hard appear or disappear is the pop the visual
            // contract forbids and what would read as an interface blinking.
            var envelope = Mathf.Min(life / .18f, 1f) * (1f - Mathf.SmoothStep(.55f, 1f, life));
            var alpha = Mathf.Max(0f, envelope);
            line.SetInstanceShaderParameter("streak_alpha", alpha);
            line.Visible = alpha > .004f;
            ApplyPose(line, slot, direction);
        }

        if (_liveCount != live)
        {
            _liveCount = live;
            SetMeta("liveStreakCount", live);
        }

        if (_lastWind != wind)
        {
            _lastWind = wind;
            // The exact world vector the ribbons are aligned to, for the frame's
            // readback: a capture can prove the streak ran with the storm, not with
            // the camera.
            SetMeta("windVector", wind);
        }
    }

    private Slot Spawn(Vector3 focus, Vector3 direction)
    {
        // Spawned upwind so a ribbon travels across the view instead of sliding along
        // it, with a lateral ring offset so the set is never one plane.
        var upwind = -direction;
        var distance = Mathf.Lerp(ShellNearDistance, ShellFarDistance, _rng.Randf());
        var right = upwind.Cross(Vector3.Up);
        if (right.LengthSquared() < .0001f) right = upwind.Cross(Vector3.Right);
        right = right.Normalized();
        var up = right.Cross(upwind).Normalized();
        var angle = Mathf.Tau * _rng.Randf();
        var lateral = ShellFarDistance * .45f * Mathf.Sqrt(_rng.Randf());
        var position = focus + upwind * distance + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * lateral;
        position.Y = Mathf.Max(position.Y, (float)AgentBAct1HeightField.Ground(position.X, position.Z) + .6f);
        var length = _forestRim
            ? Mathf.Lerp(ForestLengthMin, ForestLengthMax, _rng.Randf())
            : Mathf.Lerp(VillageLengthMin, VillageLengthMax, _rng.Randf());
        var lifetime = _forestRim
            ? Mathf.Lerp(ForestLifetimeMin, ForestLifetimeMax, _rng.Randf())
            : Mathf.Lerp(VillageLifetimeMin, VillageLifetimeMax, _rng.Randf());
        var rest = _forestRim
            ? Mathf.Lerp(ForestRestGapMin, ForestRestGapMax, _rng.Randf())
            : Mathf.Lerp(VillageRestGapMin, VillageRestGapMax, _rng.Randf());
        return new Slot(position, 0f, lifetime, rest, length, _forestRim ? ForestWidth : VillageWidth,
            // Roll spreads ribbons around the wind axis, so some are broadside and
            // some nearly edge-on: that unevenness is what keeps the set from reading
            // as one repeated glyph.
            Mathf.Tau * _rng.Randf(), _forestRim ? .55f : .38f, true);
    }

    private static void ApplyPose(MeshInstance3D line, Slot slot, Vector3 direction)
    {
        // The long axis is the wind itself; the ribbon lies in the plane spanned by
        // the wind and its own roll. There is no camera term in this basis at all,
        // which is the difference between "the air moves this way" and a speed line.
        var side = direction.Cross(Vector3.Up);
        if (side.LengthSquared() < .0001f) side = direction.Cross(Vector3.Right);
        side = side.Normalized().Rotated(direction, slot.Roll);
        var normal = direction.Cross(side).Normalized();
        var basis = new Basis(side, direction, normal)
            .Scaled(new Vector3(slot.Width / BaseWidth, slot.Length, 1f));
        line.GlobalTransform = new Transform3D(basis, slot.Position);
        // The ribbon is posed by transform, so the CPU AABB tracks it; the small
        // margin only covers the width the scale cannot reach at grazing angles.
        line.ExtraCullMargin = .4f;
    }

    private void ClearAll()
    {
        if (!_poolBuilt) return;
        for (var index = 0; index < PoolSize; index++)
        {
            _slots[index] = default;
            _lines[index].Visible = false;
        }

        if (_liveCount != 0)
        {
            _liveCount = 0;
            SetMeta("liveStreakCount", 0);
        }
    }

    /// <summary>
    /// The established read of the player's own accessibility owner — the same
    /// pattern Act1ConnectedWorld and Main use. Reduced motion removes the moving
    /// abstraction exactly as it removes the vertex sway; nothing here writes that
    /// setting.
    /// </summary>
    private bool IsReducedMotionRequested() =>
        WindStreakMode == "on"
            ? false
            : GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ReducedMotion: true };

    /// <summary>Receipt line: what the streak layer is spending, on one line.</summary>
    public string DescribeWindStreaks()
    {
        var lifetimeMin = _forestRim ? ForestLifetimeMin : VillageLifetimeMin;
        var lifetimeMax = _forestRim ? ForestLifetimeMax : VillageLifetimeMax;
        var lengthMin = _forestRim ? ForestLengthMin : VillageLengthMin;
        var lengthMax = _forestRim ? ForestLengthMax : VillageLengthMax;
        var restMin = _forestRim ? ForestRestGapMin : VillageRestGapMin;
        var restMax = _forestRim ? ForestRestGapMax : VillageRestGapMax;
        // One line, assembled from invariant fragments: `a + b` of two interpolated
        // strings is a plain string and no longer binds to
        // FormattableString.Invariant (CS1503). The receipt text is unchanged.
        return string.Concat(
            FormattableString.Invariant($"mode={WindStreakMode} presentation={_presentationEnabled}"),
            FormattableString.Invariant($" region={(_forestRim ? "kara-forest-rim" : "village")} pool={PoolSize} cap={VisibleCap} live={_liveCount}"),
            FormattableString.Invariant($" lifetime=[{lifetimeMin:F2},{lifetimeMax:F2}]s rest=[{restMin:F2},{restMax:F2}]s"),
            FormattableString.Invariant($" alphaCeiling={AlphaCeiling:F3} width={(_forestRim ? ForestWidth : VillageWidth):F3}m"),
            FormattableString.Invariant($" length=[{lengthMin:F2},{lengthMax:F2}]m windMin={MinimumWindSpeed:F1}m/s"),
            FormattableString.Invariant($" tint={_tintSource} reducedMotion={IsReducedMotionRequested()} shell=[{ShellNearDistance:F0},{ShellFarDistance:F0}]m"));
    }
}
