using Godot;

namespace Urman.Godot;

/// <summary>A single authored thing, projected from the runtime custody/snapshot.</summary>
public partial class CarryableProp : StaticBody3D
{
    public enum CarryState { World, Held, Placed, Combined }
    public enum ItemClass { Light, Medium, Bucket, Bulky }
    public enum ItemKind { Log, Crate, Bucket, Axe, Shovel, Pole, Lantern, Board, Ladder, Cloth }

    public string ItemId { get; private set; } = string.Empty;
    public string PromptName { get; private set; } = string.Empty;
    public ItemClass Class { get; private set; }
    public ItemKind Kind { get; private set; }
    public CarryState State { get; private set; } = CarryState.World;
    public float YawDegrees { get; private set; }
    public float HoldDistance => Class == ItemClass.Bulky ? 1.1f : .78f;
    public float HoldDrop => -.44f;
    public Vector3 Size { get; private set; }
    public float HalfWidth => Size.X * .5f;
    public float Height => Size.Y;
    public bool IsConcealed { get; private set; }
    public bool HasOwnDeviation { get; private set; }
    public bool LightOn { get; private set; }
    public string ToolId { get; internal set; } = string.Empty;
    public string PlacementZone { get; internal set; } = string.Empty;

    private Transform3D _authoredTransform;
    private bool _authoredConcealed;
    private bool _authoredCaptured;
    private OmniLight3D? _lamp;
    private MeshInstance3D? _lampGlow;
    private bool _presentationEnabled = true;

    public static CarryableProp Create(string itemId, string promptName, ItemClass itemClass,
        Vector3 position, float yawDegrees, string colour, string surface,
        ItemKind? kind = null, Vector3? size = null)
    {
        var resolvedKind = kind ?? (itemId.Contains("axe", StringComparison.Ordinal) ? ItemKind.Axe
            : itemId.Contains("crate", StringComparison.Ordinal) ? ItemKind.Crate
            : itemClass == ItemClass.Bucket ? ItemKind.Bucket
            : itemClass == ItemClass.Bulky ? ItemKind.Board : ItemKind.Log);
        var prop = new CarryableProp
        {
            Name = $"Carryable_{itemId}", ItemId = itemId, PromptName = promptName,
            Class = itemClass, Kind = resolvedKind, Position = position,
            RotationDegrees = new(0, yawDegrees, 0), YawDegrees = yawDegrees,
            Size = size ?? (resolvedKind switch
            {
                ItemKind.Crate => new(.52f, .38f, .42f),
                ItemKind.Bucket => new(.28f, .42f, .28f),
                ItemKind.Axe => new(.40f, .075f, .18f),
                ItemKind.Shovel => new(.30f, 1.18f, .12f),
                ItemKind.Pole => new(.055f, .075f, 1.85f),
                ItemKind.Lantern => new(.22f, .39f, .22f),
                ItemKind.Board => new(.32f, .075f, 1.8f),
                ItemKind.Ladder => new(.52f, .14f, 2.15f),
                ItemKind.Cloth => new(.28f, .07f, .20f),
                _ => new(.38f, .13f, .14f)
            }),
            CollisionLayer = 1u, CollisionMask = 0u
        };
        prop.BuildGeometry(colour, surface);
        prop.AddChild(new CollisionShape3D
        {
            Name = "BodyCollision", Shape = new BoxShape3D { Size = prop.Size },
            Position = Vector3.Up * prop.Size.Y * .5f
        });
        prop.SetMeta("carryItemId", itemId);
        prop.SetMeta("collisionOwner", "carryable-prop");
        prop.SetMeta("runtimeStateOwner", "RuntimeBridge/world.custody + world.props");
        return prop;
    }

    public override void _Ready()
    {
        _authoredTransform = Transform;
        _authoredConcealed = IsConcealed;
        _authoredCaptured = true;
        ResetToAuthored();
    }

    /// <summary>Loading a record without a deviation really restores the authored state.</summary>
    public void ResetToAuthored()
    {
        if (!_authoredCaptured) return;
        Transform = _authoredTransform;
        YawDegrees = RotationDegrees.Y;
        HasOwnDeviation = false;
        State = CarryState.World;
        PlacementZone = string.Empty;
        SetConcealed(_authoredConcealed);
        SetLight(false);
    }

    public void Take()
    {
        HasOwnDeviation = true;
        IsConcealed = false;
        SetState(CarryState.Held);
    }

    public void SetConcealed(bool concealed)
    {
        IsConcealed = concealed;
        SetState(State);
    }

    public void Place(Vector3 groundPoint, float yawDegrees, bool combined = false)
    {
        HasOwnDeviation = true;
        IsConcealed = false;
        YawDegrees = yawDegrees;
        GlobalTransform = new(Basis.FromEuler(new(0, Mathf.DegToRad(yawDegrees), 0)), groundPoint);
        SetState(combined ? CarryState.Combined : CarryState.Placed);
    }

    public void HoldAt(Vector3 point)
    {
        if (State != CarryState.Held) return;
        GlobalTransform = new(Basis.FromEuler(new(0, Mathf.DegToRad(YawDegrees), 0)), point);
    }

    public void Rotate(float stepDegrees) => YawDegrees = Mathf.PosMod(YawDegrees + stepDegrees, 360f);

    public void SetLight(bool enabled)
    {
        LightOn = Kind == ItemKind.Lantern && enabled;
        if (_lamp is not null) _lamp.Visible = LightOn && !IsConcealed && _presentationEnabled;
        if (_lampGlow is not null) _lampGlow.Visible = LightOn && !IsConcealed && _presentationEnabled;
    }

    public void SetPresentationEnabled(bool enabled)
    {
        _presentationEnabled = enabled;
        SetState(State);
    }

    private void SetState(CarryState state)
    {
        State = state;
        Visible = !IsConcealed && _presentationEnabled;
        // Resting collision is a constant, never captured from a concealed/held
        // frame. Re-taking, restoring a buried find and placing all use this path.
        CollisionLayer = state == CarryState.Held || IsConcealed || !_presentationEnabled ? 0u : 1u;
        CollisionMask = 0u;
        SetLight(LightOn);
    }
}
