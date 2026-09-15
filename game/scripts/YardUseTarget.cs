using Godot;

namespace Urman.Godot;

/// <summary>A ray target fitted to the visible material that a tool changes.</summary>
public partial class YardUseTarget : StaticBody3D
{
    public YardTool Tool { get; private set; } = null!;
    public string UseId { get; private set; } = string.Empty;
    public string Prompt { get; private set; } = string.Empty;
    public string StateKey => $"tool/{Tool.ToolId}/{UseId}";
    public bool InitialVisible { get; private set; }
    public bool ResultVisible { get; private set; }
    public bool Completed => InitialVisible != ResultVisible;
    private Node3D _effect = null!;
    private Action<bool>? _apply;
    private bool _worldEnabled = true;

    public static YardUseTarget Create(YardTool tool, string useId, string prompt, Node3D effect, Action<bool>? apply)
    {
        var target = new YardUseTarget
        {
            Name = $"Use_{tool.ToolId}_{useId}", Tool = tool, UseId = useId,
            Prompt = prompt, _effect = effect, _apply = apply,
            InitialVisible = effect.Visible, ResultVisible = effect.Visible,
            CollisionLayer = 4u, CollisionMask = 0u
        };
        target.SetMeta("collisionOwner", "yard-tool-target");
        target.SetMeta("runtimeStateOwner", "RuntimeBridge/world.props");
        target.SetMeta("worldPropId", target.StateKey);
        return target;
    }

    public override void _Ready()
    {
        if (_effect is not MeshInstance3D { Mesh: { } mesh })
            throw new InvalidOperationException($"Tool use {StateKey} needs its actual visible mesh.");
        var bounds = mesh.GetAabb();
        GlobalTransform = _effect.GlobalTransform * new Transform3D(Basis.Identity, bounds.GetCenter());
        AddChild(new CollisionShape3D { Name = "VisibleMaterialTarget",
            Shape = new BoxShape3D { Size = bounds.Size + Vector3.One * .012f } });
        ApplyResult(ResultVisible);
    }

    public void ApplyResult(bool visible)
    {
        ResultVisible = visible;
        _effect.Visible = visible;
        _apply?.Invoke(visible);
        CollisionLayer = Completed || !_worldEnabled ? 0u : 4u;
    }

    public void SetWorldEnabled(bool enabled)
    {
        _worldEnabled = enabled;
        CollisionLayer = Completed || !enabled ? 0u : 4u;
    }
}
