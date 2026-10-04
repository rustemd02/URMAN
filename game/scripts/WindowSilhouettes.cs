using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only occupancy behind the inhabited residential glass: a bounded
/// pool of dark silhouette quads placed just inside the pane, visible through the
/// existing warm window material. It is a sibling of the window-spill pool and
/// the chimney-smoke pool: fixed slot count, nearest-window selection at 4 Hz,
/// nothing per-house, and an ordinary frame touches only PoolBudget nodes.
///
/// The household director keeps its window list private, so windows are
/// rediscovered with the director's exact rule (visible mesh, "Window"+"Glass"
/// name or occupiedWindow meta, ResidentialOwner) and the pane AABB centre and
/// horizontal outward normal are recomputed the same way, never invented.
///
/// Budget: PoolBudget (10) MeshInstance3D nodes, 5 shared ArrayMesh variants
/// (32 triangles total; at most 8 triangles per instance), one shared unshaded
/// StandardMaterial3D, no shadows, no textures. Selection is O(windows x pool)
/// every 0.25 s; per-frame work is O(pool) and allocates nothing. Opacity uses
/// the per-instance GeometryInstance3D.Transparency fade, and a window only
/// leaves selection beyond the fade end, so occupants never pop in or out.
///
/// Day/night: the authored AgentBSun energy (AtmosphereProfiles: 0.55 for the
/// day and zirat profiles, 0.22 for the Kara night) selects a fainter, shorter
/// day pool or the full night pool. SetNight is an optional explicit override;
/// without it the sun is read live, so a scene without the connected world still
/// works in the subtle day mode.
/// </summary>
public partial class WindowSilhouettes : Node3D
{
    public const int PoolBudget = 10;
    public const int DayPoolBudget = 6;
    public const int VariantCount = 5;
    public const float SelectionInterval = .25f;
    public const float NightSelectionDistance = 24f;
    public const float DaySelectionDistance = 16f;
    // A window is only unbound beyond the selection distance, which is past the
    // fade end; a dropped occupant is already fully faded out at that range.
    private const float NightFadeBegin = 11f, NightFadeEnd = 22f;
    private const float DayFadeBegin = 7f, DayFadeEnd = 15f;
    private const float NightOpacity = .78f, DayOpacity = .34f;
    // Squared-distance stickiness copied from the chimney-smoke pool: a bound
    // window keeps its slot unless a challenger is clearly closer, which stops
    // churn along the boundary between two rows of houses.
    private const float IncumbentFactor = .72f;
    // AtmosphereProfiles authors day/zirat sun energy 0.55 and Kara night 0.22.
    private const float NightSunEnergy = .35f;

    private static readonly float[] VariantWidth = [.85f, .92f, .85f, .62f, .85f];
    private static readonly float[] VariantHeight = [.80f, .60f, .42f, .50f, .95f];
    private static readonly bool[] VariantOnSill = [true, true, true, true, false];

    private readonly MeshInstance3D[] _slots = new MeshInstance3D[PoolBudget];
    private readonly int[] _slotWindow = new int[PoolBudget];
    private readonly int[] _nearest = new int[PoolBudget];
    private readonly float[] _distances = new float[PoolBudget];
    private readonly Vector3[] _slotOrigin = new Vector3[PoolBudget];
    private readonly Basis[] _slotBasis = new Basis[PoolBudget];
    private readonly Vector3[] _slotRight = new Vector3[PoolBudget];
    private readonly Vector3[] _slotNormal = new Vector3[PoolBudget];
    private readonly float[] _slotPhase = new float[PoolBudget];
    private readonly float[] _slotSpeed = new float[PoolBudget];

    private Mesh[] _variants = [];
    private Window[] _windows = [];
    private int[] _boundSlot = [];
    private DirectionalLight3D? _sun;
    private bool? _nightOverride;
    private bool _night;
    private double _tick = SelectionInterval;
    private double _time;
    private float _life;
    private int _boundCount = -1;

    public int WindowCount => _windows.Length;
    public int BoundCount => _boundCount < 0 ? 0 : _boundCount;

    /// <summary>
    /// Discovers the director's windows once, builds the shared pool and prints
    /// the budget line. Must run after the village (and its inhabited windows)
    /// is mounted; running before or after VillageHouseholdDirector.Initialize
    /// is both valid, the filters do not depend on its metadata.
    /// </summary>
    public void Initialize(Node3D world)
    {
        if (_slots[0] is not null) return;
        _sun = world.FindChild("AgentBSun", true, false) as DirectionalLight3D;
        DirectionalLight3D? fallback = null;
        if (_sun is null)
            foreach (var light in world.FindChildren("*", nameof(DirectionalLight3D), true, false).OfType<DirectionalLight3D>())
            {
                fallback ??= light;
                if (light.Name.ToString().Contains("Sun", StringComparison.Ordinal)) { _sun = light; break; }
            }
        _sun ??= fallback;

        var found = new List<Window>(64);
        foreach (var pane in world.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (pane.Mesh is null || !pane.IsVisibleInTree()) continue;
            var name = pane.Name.ToString();
            var glass = name.Contains("Window", StringComparison.Ordinal) && name.Contains("Glass", StringComparison.Ordinal);
            if (!glass && !pane.HasMeta("occupiedWindow")) continue;
            if (VillageHouseholdDirector.ResidentialOwner(pane) is not { } owner) continue;
            var bounds = pane.Mesh.GetAabb();
            var centre = pane.GlobalTransform * bounds.GetCenter();
            var normal = pane.GlobalBasis * (bounds.Size.X < bounds.Size.Z ? Vector3.Right : Vector3.Back);
            normal.Y = 0;
            normal = normal.Normalized();
            if (normal.Dot(centre - owner.GlobalPosition) < 0) normal = -normal;
            var scale = pane.GlobalBasis.Scale;
            var width = bounds.Size.X < bounds.Size.Z ? bounds.Size.Z * scale.Z : bounds.Size.X * scale.X;
            var height = bounds.Size.Y * Mathf.Abs(scale.Y);
            if (width < .2f || height < .2f || !centre.IsFinite() || !normal.IsFinite()) continue;
            var hash = TimberHomeStyle.StableHash(owner.GetPath().ToString() + "|" + pane.GetPath().ToString());
            // Stable occupant: one pane keeps its variant, phase, speed and a
            // presence between 0.72 and 1.0 across sessions and rebinds.
            found.Add(new Window(
                pane, centre, normal, width, height,
                (int)(hash % VariantCount),
                .72f + (hash % 977) / 977f * .28f,
                .04f + hash % 3 * .01f,
                (hash % 733) / 733f,
                .05f + (hash % 53) / 530f,
                pane.GetPath().ToString()));
        }
        _windows = [.. found];
        _boundSlot = new int[_windows.Length];
        Array.Fill(_boundSlot, -1);

        _variants = BuildVariants();
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(.045f, .04f, .038f, .82f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        foreach (var mesh in _variants) mesh.SurfaceSetMaterial(0, material);

        for (var slot = 0; slot < PoolBudget; slot++)
        {
            var node = new MeshInstance3D
            {
                Name = $"WindowSilhouette{slot}", Visible = false, Transparency = 1f,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            node.SetMeta("lightingRole", "presentation-only occupant silhouette behind inhabited glass");
            AddChild(node);
            _slots[slot] = node;
            _slotWindow[slot] = -1;
        }
        Visible = false;
        UpdateCounters();
        SetMeta("windowSilhouetteBudget", PoolBudget);
        SetMeta("windowSilhouetteVariants", VariantCount);
        SetMeta("windowSilhouetteWindows", _windows.Length);
        SetMeta("presentationOnly", true);
        GD.Print($"village-window-silhouettes: windows={WindowCount} pool={PoolBudget} variants={VariantCount}");
    }

    /// <summary>
    /// Optional explicit day/night decision for tests or a caller that knows the
    /// zone. Without it the pool reads the authored sun energy every refresh.
    /// </summary>
    public void SetNight(bool night) => _nightOverride = night;

    /// <summary>
    /// Called at most once per frame by the world. <paramref name="audible"/> is
    /// the village-life gate (the same one the household voices use): false fades
    /// the pool out and freezes it, for interiors, dialogue, the modal debug
    /// panel and pause; true ramps it back in. The pool never allocates here.
    /// </summary>
    public void Tick(double delta, Vector3 viewer, bool audible)
    {
        if (_slots[0] is null || delta <= 0d) return;
        if (!audible && _life <= 0f)
        {
            if (Visible) Visible = false;
            return;
        }
        var step = (float)Math.Min(delta, .1);
        _life = Mathf.MoveToward(_life, audible ? 1f : 0f, step * (audible ? 2.5f : 4f));
        Visible = _life > 0f || audible;
        if (audible) _time += step;
        _tick += delta;
        if (_tick >= SelectionInterval)
        {
            _tick = 0d;
            _night = _nightOverride ?? (_sun is not null && IsInstanceValid(_sun) && _sun.LightEnergy < NightSunEnergy);
            Select(viewer);
        }
        AnimatePool(viewer, step);
    }

    /// <summary>
    /// Rebuilds the nearest set with the bounded insertion pass the window spill
    /// and chimney pools use, then rebinds only the changed slots. A day refresh
    /// fills only DayPoolBudget slots, so fewer occupants show in daylight.
    /// </summary>
    private void Select(Vector3 viewer)
    {
        var pool = _night ? PoolBudget : DayPoolBudget;
        var maxDistance = _night ? NightSelectionDistance : DaySelectionDistance;
        var maxSquared = maxDistance * maxDistance;
        Array.Fill(_nearest, -1);
        Array.Fill(_distances, maxSquared);
        for (var index = 0; index < _windows.Length; index++)
        {
            if (!_windows[index].Pane.IsVisibleInTree()) continue;
            var distance = viewer.DistanceSquaredTo(_windows[index].Centre);
            if (distance >= maxSquared) continue;
            if (_boundSlot[index] >= 0) distance *= IncumbentFactor;
            for (var slot = 0; slot < pool; slot++)
            {
                if (distance >= _distances[slot]) continue;
                for (var shift = pool - 1; shift > slot; shift--)
                {
                    _distances[shift] = _distances[shift - 1];
                    _nearest[shift] = _nearest[shift - 1];
                }
                _distances[slot] = distance;
                _nearest[slot] = index;
                break;
            }
        }
        for (var slot = 0; slot < PoolBudget; slot++)
        {
            var next = _nearest[slot];
            if (_slotWindow[slot] == next) continue;
            Unbind(slot);
            if (next >= 0) Bind(slot, next);
        }
        UpdateCounters();
    }

    private void Bind(int slot, int index)
    {
        var window = _windows[index];
        var width = window.Width * VariantWidth[window.Variant];
        var height = window.Height * VariantHeight[window.Variant];
        var right = Vector3.Up.Cross(window.Normal).Normalized();
        var origin = window.Centre - window.Normal * window.Inset;
        // Variants with a floor line sit on the bottom of the glass (a figure at
        // the curtain, a silhouette above a table, a cat on the sill, a lamp).
        if (VariantOnSill[window.Variant]) origin += Vector3.Up * ((height - window.Height) * .5f);
        var basis = new Basis(right * width, Vector3.Up * height, window.Normal);
        var node = _slots[slot];
        node.Mesh = _variants[window.Variant];
        node.GlobalTransform = new Transform3D(basis, origin);
        node.Transparency = 1f;
        node.Visible = _life > 0f;
        node.SetMeta("silhouetteWindow", window.Path);
        _slotOrigin[slot] = origin;
        _slotBasis[slot] = basis;
        _slotRight[slot] = right;
        _slotNormal[slot] = window.Normal;
        _slotPhase[slot] = window.Phase;
        _slotSpeed[slot] = window.Speed;
        _slotWindow[slot] = index;
        _boundSlot[index] = slot;
    }

    private void Unbind(int slot)
    {
        var index = _slotWindow[slot];
        if (index < 0) return;
        _boundSlot[index] = -1;
        _slotWindow[slot] = -1;
        var node = _slots[slot];
        node.Visible = false;
        node.Mesh = null;
    }

    /// <summary>
    /// O(pool): distance fade into GeometryInstance3D.Transparency and one slow
    /// pose per variant. Standing figures breathe and shift weight, the seated
    /// profile bobs above the table, the cat dips its head, the lamp shade
    /// breathes, the empty curtain drifts and billows. Every motion is a pair of
    /// sine terms; nothing here allocates.
    /// </summary>
    private void AnimatePool(Vector3 viewer, float step)
    {
        var fadeBegin = _night ? NightFadeBegin : DayFadeBegin;
        var fadeEnd = _night ? NightFadeEnd : DayFadeEnd;
        var opacity = _night ? NightOpacity : DayOpacity;
        var phaseBase = (float)_time;
        for (var slot = 0; slot < PoolBudget; slot++)
        {
            var index = _slotWindow[slot];
            if (index < 0) continue;
            var window = _windows[index];
            var node = _slots[slot];
            var fade = 1f - Mathf.SmoothStep(fadeBegin, fadeEnd, viewer.DistanceTo(window.Centre));
            var target = _life * window.Presence * opacity * fade;
            var next = Mathf.MoveToward(1f - node.Transparency, target, step * 2.2f);
            node.Transparency = 1f - next;
            node.Visible = next > .004f;
            if (!node.Visible) continue;
            var phase = phaseBase * _slotSpeed[slot] + _slotPhase[slot];
            var sway = Mathf.Sin(phase * Mathf.Tau);
            var drift = Mathf.Sin(phase * Mathf.Tau * .37f + _slotPhase[slot] * 3.1f);
            float dx, dy, angle, breath;
            switch (window.Variant)
            {
                case 0: // standing figure: breath plus an occasional weight shift
                    dx = sway * .011f + drift * .009f;
                    dy = drift * .004f;
                    angle = drift * .010f;
                    breath = 1f;
                    break;
                case 1: // seated profile: a shoulder bob above the table line
                    dx = sway * .005f;
                    dy = sway * .003f;
                    angle = sway * .004f;
                    breath = 1f;
                    break;
                case 2: // cat: the head dips and rises, a hint of a stretch
                    dx = 0f;
                    dy = Mathf.Abs(sway) * .004f;
                    angle = sway * .012f;
                    breath = 1f;
                    break;
                case 3: // lamp: only the shade breathes, very slowly
                    dx = 0f;
                    dy = 0f;
                    angle = 0f;
                    breath = 1f + sway * .008f;
                    break;
                default: // empty curtain: cloth breathing and a slow rod drift
                    dx = sway * .016f + drift * .008f;
                    dy = 0f;
                    angle = sway * .005f;
                    breath = 1f + sway * .013f;
                    break;
            }
            var basis = _slotBasis[slot].Rotated(_slotNormal[slot], angle);
            if (breath != 1f) basis = basis.Scaled(Vector3.One * breath);
            node.GlobalTransform = new Transform3D(basis, _slotOrigin[slot] + _slotRight[slot] * dx + Vector3.Up * dy);
        }
    }

    private void UpdateCounters()
    {
        var bound = 0;
        for (var slot = 0; slot < PoolBudget; slot++)
            if (_slotWindow[slot] >= 0) bound++;
        if (bound == _boundCount) return;
        _boundCount = bound;
        SetMeta("windowSilhouetteCount", bound);
    }

    /// <summary>
    /// Five shared occupancy variants, all quads in a unit box (x -0.5..0.5,
    /// y -0.5..0.5): 0 standing figure, 1 seated profile at a table, 2 cat on
    /// the sill, 3 lamp, 4 empty moving curtain. 32 triangles in total.
    /// </summary>
    private static Mesh[] BuildVariants()
    {
        return
        [
            BuildMesh(
                (.010f, -.50f, .150f, -.50f, .130f, -.16f, .030f, -.16f),
                (.025f, -.16f, .135f, -.16f, .185f, .220f, -.025f, .220f),
                (.032f, .220f, .128f, .220f, .128f, .310f, .032f, .310f)),
            BuildMesh(
                (-.420f, -.38f, .400f, -.38f, .400f, -.31f, -.420f, -.31f),
                (-.300f, -.33f, -.060f, -.33f, -.020f, .000f, -.240f, .040f),
                (-.170f, .010f, -.070f, .010f, -.065f, .120f, -.165f, .120f),
                (-.120f, -.180f, .220f, -.220f, .220f, -.150f, -.120f, -.110f)),
            BuildMesh(
                (-.300f, -.36f, .050f, -.36f, .070f, -.24f, -.280f, -.22f),
                (.050f, -.32f, .160f, -.33f, .160f, -.23f, .050f, -.22f),
                (.060f, -.22f, .120f, -.22f, .110f, -.16f, .075f, -.16f),
                (-.280f, -.30f, -.220f, -.30f, -.380f, -.10f, -.310f, -.08f)),
            BuildMesh(
                (-.170f, -.04f, .170f, -.04f, .090f, .120f, -.090f, .120f),
                (-.016f, -.30f, .016f, -.30f, .016f, -.04f, -.016f, -.04f),
                (-.110f, -.38f, .110f, -.38f, .110f, -.30f, -.110f, -.30f)),
            BuildMesh(
                (-.460f, -.50f, -.200f, -.50f, -.180f, .50f, -.440f, .50f),
                (.180f, -.50f, .320f, -.50f, .360f, .50f, .220f, .50f))
        ];
    }

    private static ArrayMesh BuildMesh(
        (float X0, float Y0, float X1, float Y1, float X2, float Y2, float X3, float Y3) first,
        params (float X0, float Y0, float X1, float Y1, float X2, float Y2, float X3, float Y3)[] rest)
    {
        var quads = new (float X0, float Y0, float X1, float Y1, float X2, float Y2, float X3, float Y3)[rest.Length + 1];
        quads[0] = first;
        rest.CopyTo(quads, 1);
        var vertices = new Vector3[quads.Length * 4];
        var indices = new int[quads.Length * 6];
        for (var quad = 0; quad < quads.Length; quad++)
        {
            var (x0, y0, x1, y1, x2, y2, x3, y3) = quads[quad];
            var vertex = quad * 4;
            vertices[vertex] = new Vector3(x0, y0, 0f);
            vertices[vertex + 1] = new Vector3(x1, y1, 0f);
            vertices[vertex + 2] = new Vector3(x2, y2, 0f);
            vertices[vertex + 3] = new Vector3(x3, y3, 0f);
            var index = quad * 6;
            indices[index] = vertex;
            indices[index + 1] = vertex + 1;
            indices[index + 2] = vertex + 2;
            indices[index + 3] = vertex;
            indices[index + 4] = vertex + 2;
            indices[index + 5] = vertex + 3;
        }
        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private readonly record struct Window(
        MeshInstance3D Pane, Vector3 Centre, Vector3 Normal, float Width, float Height,
        int Variant, float Presence, float Inset, float Phase, float Speed, string Path);
}
