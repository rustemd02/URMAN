using Godot;

namespace Urman.Godot;

/// <summary>
/// One carryable prop of the EX01 set: a world item the player can pick up,
/// carry in front of the camera and put back down, repeatedly, with no
/// narrative commitment. Session-local by design in this slice - persistence
/// lands with the world.custody handler wiring.
/// </summary>
public partial class CarryableProp : StaticBody3D
{
    public enum CarryState { World, Held, Placed }

    public enum ItemClass { Light, Medium, Bucket }

    public string ItemId { get; private set; } = string.Empty;
    public string PromptName { get; private set; } = string.Empty;
    public ItemClass Class { get; private set; }
    public CarryState State { get; private set; } = CarryState.World;
    public float YawDegrees { get; private set; }
    public float HoldDistance { get; private set; }
    public float HoldDrop { get; private set; }
    public float HalfWidth { get; private set; }
    public float Height { get; private set; }

    private uint _restCollisionLayer;
    private MeshInstance3D _mesh = null!;

    private static (Vector3 Size, float HoldDistance, float HoldDrop) Body(ItemClass itemClass) =>
        itemClass switch
        {
            ItemClass.Light => (new Vector3(0.36f, 0.12f, 0.13f), 0.70f, -0.14f),
            ItemClass.Medium => (new Vector3(0.42f, 0.30f, 0.34f), 0.92f, -0.28f),
            _ => (new Vector3(0.26f, 0.28f, 0.26f), 0.85f, -0.22f)
        };

    public static CarryableProp Create(string itemId, string promptName, ItemClass itemClass,
        Vector3 position, float yawDegrees, string colour, string surface)
    {
        var (size, holdDistance, holdDrop) = Body(itemClass);
        var prop = new CarryableProp
        {
            ItemId = itemId,
            PromptName = promptName,
            Class = itemClass,
            Position = position,
            YawDegrees = yawDegrees,
            HoldDistance = holdDistance,
            HoldDrop = holdDrop,
            HalfWidth = size.X * 0.5f,
            Height = size.Y
        };
        prop.Name = $"Carryable_{itemId}";
        var mesh = new MeshInstance3D
        {
            Name = "Body",
            Position = new(0, size.Y * 0.5f, 0),
            Mesh = itemClass == ItemClass.Bucket
                ? new CylinderMesh { TopRadius = size.X * 0.5f, BottomRadius = size.X * 0.42f, Height = size.Y }
                : new BoxMesh { Size = size },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, surface)
        };
        prop.AddChild(mesh);
        prop._mesh = mesh;
        var shape = new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new(0, size.Y * 0.5f, 0) };
        prop.AddChild(shape);
        prop.SetMeta("presentationOnly", true);
        prop.SetMeta("carryItemId", itemId);
        prop.CollisionLayer = 1u;
        prop.CollisionMask = 1u;
        prop.SetMeta("collisionOwner", "carryable-prop");
        return prop;
    }

    public override void _Ready()
    {
        _restCollisionLayer = CollisionLayer;
        SetState(CarryState.World);
    }

    public void Take()
    {
        SetState(CarryState.Held);
    }

    public void Place(Vector3 groundPoint, float yawDegrees)
    {
        State = CarryState.Placed;
        YawDegrees = yawDegrees;
        RotationDegrees = new Vector3(0, yawDegrees, 0);
        GlobalPosition = new Vector3(groundPoint.X, groundPoint.Y, groundPoint.Z);
    }

    public void HoldAt(Transform3D holdTransform)
    {
        State = CarryState.Held;
        GlobalTransform = holdTransform;
        RotationDegrees = new Vector3(0, YawDegrees, 0);
    }

    public void Rotate(float stepDegrees)
    {
        YawDegrees += stepDegrees;
        RotationDegrees = new Vector3(0, YawDegrees, 0);
    }

    private void SetState(CarryState state)
    {
        State = state;
        // A carried item must not collide with the player or the world; a
        // placed or resting one must, so the surface holds it.
        CollisionLayer = state == CarryState.Held ? 0u : _restCollisionLayer;
        CollisionMask = state == CarryState.Held ? 0u : 1u;
    }
}
