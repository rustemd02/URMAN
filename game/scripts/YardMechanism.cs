using System.Text.Json;
using Godot;

namespace Urman.Godot;

/// <summary>
/// A local affordance attached to a real surface. CarryCoordinator owns input
/// and commits; this node only describes the attempt and projects world.props.
/// </summary>
public partial class YardMechanism : StaticBody3D
{
    public enum Operation { WarmCloth, Thaw, ReadByLight, Nudge, HookLatch, RestBoard, DetachHook, QuietRattle }
    public enum SoundCause { None, Wind, FootContact }
    public string StateKey { get; private set; } = string.Empty;
    public Operation Action { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public string RequirementText { get; private set; } = string.Empty;
    public string ResultText { get; private set; } = string.Empty;
    public string ZoneId { get; set; } = string.Empty;
    public bool Solved { get; private set; }
    public bool Heard { get; private set; }
    public Vector3 RestPoint { get; set; }
    public float RestYaw { get; set; }
    public Vector3? AlternativeApproach { get; set; }
    public string PrerequisiteKey { get; set; } = string.Empty;
    public string SoundSample { get; set; } = string.Empty;
    public string SoundCaption { get; set; } = string.Empty;
    public SoundCause CueCause { get; set; }
    internal int EmittedCueCount { get; private set; }
    public Node3D? MovingPart { get; set; }
    private MeshInstance3D _surface = null!;
    private Action<bool>? _project;
    private bool _enabled = true;
    private ulong _nextSound;
    private ulong _pulseUntil;
    private float _restRotation;
    private Vector3 _authoredPartRotation;

    public static YardMechanism Create(string id, Operation action, MeshInstance3D surface,
        string prompt, string requirement, string result, Action<bool>? project = null)
    {
        var target = new YardMechanism
        {
            Name = "Mechanism_" + id, StateKey = "yard/" + id, Action = action,
            _surface = surface, Prompt = prompt, RequirementText = requirement,
            ResultText = result, _project = project, CollisionLayer = 4, CollisionMask = 0
        };
        target.SetMeta("collisionOwner", "yard-mechanism-target");
        target.SetMeta("runtimeStateOwner", "RuntimeBridge/world.props");
        target.SetMeta("worldPropId", target.StateKey);
        return target;
    }

    public override void _Ready()
    {
        var bounds = _surface.Mesh?.GetAabb()
            ?? throw new InvalidOperationException($"{StateKey} has no visible surface.");
        // The tiny ray skin sits on the actual surface; it is never a body wall.
        GlobalTransform = _surface.GlobalTransform * new Transform3D(Basis.Identity, bounds.GetCenter());
        AddChild(new CollisionShape3D { Name = "SurfaceRayShape",
            Shape = new BoxShape3D { Size = bounds.Size + Vector3.One * .018f } });
        SetMeta("authoredSourceMesh", _surface.GetPath().ToString());
        _restRotation = MovingPart?.Rotation.Z ?? 0;
        _authoredPartRotation = MovingPart?.Rotation ?? Vector3.Zero;
    }

    public void Restore(JsonElement props)
    {
        Solved = Flag(props, StateKey, "solved");
        Heard = Flag(props, StateKey, "heard");
        if (MovingPart is not null) MovingPart.Rotation = _authoredPartRotation;
        _project?.Invoke(Solved);
        _restRotation = MovingPart?.Rotation.Z ?? 0;
        _pulseUntil = 0;
        _nextSound = Time.GetTicksMsec() + (CueCause == SoundCause.Wind ? 900u : 0u);
        SetWorldEnabled(_enabled);
    }

    internal static bool Flag(JsonElement props, string key, string field = "solved") =>
        props.ValueKind == JsonValueKind.Object && props.TryGetProperty(key, out var record)
        && record.ValueKind == JsonValueKind.Object && record.TryGetProperty(field, out var value)
        && value.ValueKind == JsonValueKind.True;

    public void SetWorldEnabled(bool enabled)
    {
        _enabled = enabled;
        CollisionLayer = enabled && !(Solved && Action == Operation.Thaw) ? 4u : 0u;
        if (!enabled && MovingPart is not null)
            MovingPart.Rotation = MovingPart.Rotation with { Z = _restRotation };
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_enabled || (Solved && Action == Operation.Thaw)) return;
        var bounds = _surface.Mesh!.GetAabb();
        var pose = _surface.GlobalTransform * new Transform3D(Basis.Identity, bounds.GetCenter());
        if (!GlobalTransform.IsEqualApprox(pose)) GlobalTransform = pose;
    }

    public override void _Process(double delta)
    {
        if (!_enabled || Solved || CueCause == SoundCause.None || string.IsNullOrEmpty(SoundSample)
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController { ModalOpen: false } player
            || player.GlobalPosition.DistanceSquaredTo(GlobalPosition) > 121f) return;
        var now = Time.GetTicksMsec();
        var footContact = CueCause == SoundCause.FootContact
            && player.IsOnFloor() && new Vector2(player.Velocity.X, player.Velocity.Z).LengthSquared() > .04f
            && HasFootContact(player);
        if (now >= _nextSound && (CueCause == SoundCause.Wind || footContact))
        {
            _nextSound = now + (CueCause == SoundCause.Wind ? 7200u : 650u);
            _pulseUntil = now + 620;
            // Hearing the cue is not a save effect or a solved puzzle. The
            // visible pulse and the aimed caption convey the same location.
            UiFoley.PlayWorld(this, GlobalPosition, SoundSample);
            EmittedCueCount++;
        }
        if (MovingPart is not null)
            MovingPart.Rotation = MovingPart.Rotation with
            { Z = _restRotation + (now < _pulseUntil && !player.ReducedMotion ? Mathf.Sin(now * .035f) * .018f : 0) };
    }

    private bool HasFootContact(FirstPersonController player)
    {
        for (var index = 0; index < player.GetSlideCollisionCount(); index++)
        {
            var contact = player.GetSlideCollision(index);
            if (contact.GetNormal().Y > .5f && contact.GetCollider() is Node body
                && (_surface.IsAncestorOf(body) || MovingPart?.IsAncestorOf(body) == true)) return true;
        }
        return false;
    }
}
