using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

/// <summary>
/// EX01/EX03 carry coordinator: finds the carryable the player is facing, moves
/// it to the camera hold point while carried, validates and commits placements,
/// and rotates the held item. Ownership and the placement deviation go to the
/// runtime state through the wired world.custody handler, so a taken, carried
/// or placed thing survives save/load with no second save path, and a rejected
/// transition leaves the world untouched.
/// </summary>
public partial class CarryCoordinator : Node
{
    private const float Reach = 2.4f;
    private const float FocusHalfAngleDegrees = 40f;
    private const float RotateStepDegrees = 15f;
    private const float PlaceReach = 2.2f;
    private const float MaxGroundSlopeDot = 0.82f;

    private FirstPersonController? _player;
    private Camera3D? _camera;
    private RuntimeBridge? _bridge;
    private readonly List<CarryableProp> _props = new();
    private readonly List<YardTool> _tools = new();
    private CarryableProp? _held;
    private CarryableProp? _focus;
    private YardTool? _toolFocus;
    private Label3D? _prompt;
    private readonly StringName _interactAction = new("interact");
    private readonly StringName _rotateAction = new("carry_rotate");
    private bool _interactWasPressed;

    public static CarryCoordinator Create(IEnumerable<CarryableProp> props)
    {
        var coordinator = new CarryCoordinator { Name = "CarryCoordinator" };
        foreach (var prop in props)
        {
            coordinator.Register(prop);
        }

        return coordinator;
    }

    public void Register(CarryableProp prop)
    {
        _props.Add(prop);
        AddChild(prop);
    }

    /// <summary>EX03 tools share the same focus cone and interact input.</summary>
    public void Register(YardTool tool)
    {
        _tools.Add(tool);
        AddChild(tool);
    }

    public override void _Ready()
    {
        _prompt = new Label3D
        {
            Name = "CarryPrompt",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false,
            FixedSize = false,
            FontSize = 40,
            PixelSize = 0.002f,
            Modulate = new Color("e8e2d4"),
            OutlineSize = 6,
            OutlineModulate = new Color("1c1a17")
        };
        AddChild(_prompt);
        _prompt.Visible = false;
    }

    public override void _Process(double delta)
    {
        _player ??= GetTree().GetFirstNodeInGroup("player") as FirstPersonController
            ?? GetTree().Root.FindChild("Player", true, false) as FirstPersonController;
        _camera ??= _player?.GetNodeOrNull<Camera3D>("Head/Camera3D")
            ?? _player?.FindChild("Camera3D", true, false) as Camera3D;
        if (_player is null || _camera is null)
        {
            return;
        }

        var interactPressed = Input.IsActionJustPressed(_interactAction);
        var rotatePressed = Input.IsActionJustPressed(_rotateAction);

        if (_held is not null)
        {
            UpdateHeld();
            if (rotatePressed)
            {
                _held.Rotate(RotateStepDegrees);
            }

            if (interactPressed)
            {
                TryPlaceUnderHold();
            }

            return;
        }

        UpdateFocus();
        if (interactPressed && _focus is not null)
        {
            Take(_focus);
            return;
        }

        UpdateToolFocus();
        if (interactPressed && _toolFocus is not null)
        {
            _toolFocus.Use();
            _ = PersistToolUseAsync(_toolFocus, _toolFocus.LastUseId);
        }
    }

    private void UpdateFocus()
    {
        _focus = null;
        var cameraOrigin = _camera!.GlobalPosition;
        var forward = -_camera.GlobalTransform.Basis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var best = float.MaxValue;
        foreach (var prop in _props)
        {
            if (prop.State != CarryableProp.CarryState.World)
            {
                continue;
            }

            var toProp = prop.GlobalPosition - cameraOrigin;
            var flat = new Vector3(toProp.X, 0, toProp.Z);
            var distance = flat.Length();
            if (distance > Reach || distance < 0.05f)
            {
                continue;
            }

            var angle = Mathf.RadToDeg(flat.AngleTo(forward));
            if (angle > FocusHalfAngleDegrees)
            {
                continue;
            }

            if (distance < best)
            {
                best = distance;
                _focus = prop;
            }
        }

        ShowPrompt(_focus, $"Взять: {_focus?.PromptName}", (_focus?.GlobalPosition ?? Vector3.Zero) + new Vector3(0, 0.9f, 0));
    }

    private void UpdateToolFocus()
    {
        _toolFocus = null;
        var cameraOrigin = _camera!.GlobalPosition;
        var forward = -_camera.GlobalTransform.Basis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var best = float.MaxValue;
        foreach (var tool in _tools)
        {
            var toTool = tool.GlobalPosition - cameraOrigin;
            var flat = new Vector3(toTool.X, 0, toTool.Z);
            var distance = flat.Length();
            if (distance > tool.Reach || distance < 0.05f)
            {
                continue;
            }

            if (Mathf.RadToDeg(flat.AngleTo(forward)) > FocusHalfAngleDegrees)
            {
                continue;
            }

            if (distance < best)
            {
                best = distance;
                _toolFocus = tool;
            }
        }

        ShowPrompt(_toolFocus, _toolFocus?.NextPrompt() ?? string.Empty,
            _toolFocus is null ? Vector3.Zero : _toolFocus.GlobalPosition + new Vector3(0, 0.9f, 0));
    }

    private void UpdateHeld()
    {
        var held = _held!;
        var origin = _camera!.GlobalPosition;
        var forward = -_camera.GlobalTransform.Basis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var holdPoint = origin + forward * held.HoldDistance + new Vector3(0, held.HoldDrop, 0);
        held.HoldAt(new Transform3D(Basis.Identity, holdPoint));
        ShowPrompt(held, $"Положить [E] · Поворот [R]", held.GlobalPosition + new Vector3(0, held.Height + 0.25f, 0));
    }

    private void TryPlaceUnderHold()
    {
        var held = _held!;
        var origin = _camera!.GlobalPosition;
        var forward = -_camera.GlobalTransform.Basis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var probe = origin + forward * held.HoldDistance;
        var space = held.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(
            probe + new Vector3(0, 0.6f, 0),
            probe + new Vector3(0, -1.4f, 0));
        query.CollisionMask = 1u;
        var hit = space.IntersectRay(query);
        if (hit.Count == 0)
        {
            ShowPromptTemporarily("Сюда ставить нельзя");
            return;
        }

        var ground = hit["position"].AsVector3();
        var normal = hit["normal"].AsVector3();
        if (normal.Y < MaxGroundSlopeDot)
        {
            ShowPromptTemporarily("Слишком круто");
            return;
        }

        held.Place(new Vector3(ground.X, ground.Y, ground.Z), held.YawDegrees);
        _held = null;
        _prompt!.Visible = false;
        _ = PersistAsync(held, "placed");
    }

    private void ShowPrompt(Node3D? anchor, string text, Vector3 at)
    {
        if (_prompt is null || anchor is null)
        {
            if (_prompt is not null)
            {
                _prompt.Visible = false;
            }

            return;
        }

        _prompt.Text = text;
        _prompt.GlobalPosition = at;
        _prompt.Visible = true;
    }

    private void ShowPromptTemporarily(string text)
    {
        if (_prompt is null || _held is null)
        {
            return;
        }

        _prompt.Text = text;
        _prompt.GlobalPosition = _held.GlobalPosition + new Vector3(0, _held.Height + 0.3f, 0);
        _prompt.Visible = true;
    }

    private void Take(CarryableProp prop)
    {
        prop.Take();
        _held = prop;
        _focus = null;
        _ = PersistAsync(prop, "held");
    }

    /// <summary>Every registered item id, for the ownership registration pass.</summary>
    public IReadOnlyList<string> ItemIds => _props.Select(prop => prop.ItemId).ToArray();

    /// <summary>
    /// Registers the authored item set with the ownership layer and re-applies
    /// the recorded deviations. Idempotent: registration keeps a loaded
    /// session's ownership, and the apply pass only moves what the player
    /// actually moved.
    /// </summary>
    public void AttachRuntimeState(RuntimeBridge bridge)
    {
        _bridge = bridge;
        _ = bridge.RegisterWorldItemsAsync(ItemIds);
        ApplyWorldState();
    }

    /// <summary>
    /// Re-applies the persisted props state: a held item comes back to the
    /// player's hands, a placed one returns to its recorded transform, and every
    /// performed tool use shows its result again. Only deviations are stored, so
    /// everything else still sits where the authored layout put it.
    /// </summary>
    public void ApplyWorldState()
    {
        if (_bridge is null || !IsInsideTree())
        {
            return;
        }

        JsonElement props;
        try
        {
            props = _bridge.SelectWorldProps();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        foreach (var prop in _props)
        {
            if (!props.TryGetProperty(prop.ItemId, out var record))
            {
                continue;
            }

            var state = record.TryGetProperty("state", out var stateValue) ? stateValue.GetString() : null;
            if (state == "held")
            {
                if (_held is null)
                {
                    _held = prop;
                    prop.Take();
                }

                continue;
            }

            if (state != "placed" || ReferenceEquals(prop, _held))
            {
                continue;
            }

            if (record.TryGetProperty("x", out var x)
                && record.TryGetProperty("y", out var y)
                && record.TryGetProperty("z", out var z))
            {
                var yaw = record.TryGetProperty("yaw", out var yawValue)
                    ? (float)yawValue.GetDouble()
                    : prop.YawDegrees;
                prop.Place(new Vector3((float)x.GetDouble(), (float)y.GetDouble(), (float)z.GetDouble()), yaw);
            }
        }

        foreach (var tool in _tools)
        {
            foreach (var useId in tool.UseIds)
            {
                var key = ToolStateKey(tool.ToolId, useId);
                if (props.TryGetProperty(key, out var record)
                    && record.TryGetProperty("visible", out var visible))
                {
                    tool.ApplyResult(useId, visible.GetBoolean());
                }
            }
        }
    }

    private static string ToolStateKey(string toolId, string useId) => $"tool/{toolId}/{useId}";

    /// <summary>
    /// Records one tool use. The persisted value is the effect's own
    /// visibility, so no semantics are invented here: the restored yard shows
    /// exactly what the player left behind.
    /// </summary>
    private async Task PersistToolUseAsync(YardTool tool, string useId)
    {
        if (_bridge is null || tool.ResultOf(useId) is not { } visible)
        {
            return;
        }

        var records = new JsonArray
        {
            new JsonObject
            {
                ["propId"] = ToolStateKey(tool.ToolId, useId),
                ["visible"] = visible
            }
        };
        await _bridge.DispatchWorldPropsAsync(records);
    }

    private async Task PersistAsync(CarryableProp prop, string state)
    {
        if (_bridge is null)
        {
            return;
        }

        var placement = new JsonObject
        {
            ["propId"] = prop.ItemId,
            ["state"] = state
        };
        if (state == "placed")
        {
            var position = prop.GlobalPosition;
            placement["x"] = position.X;
            placement["y"] = position.Y;
            placement["z"] = position.Z;
            placement["yaw"] = prop.YawDegrees;
        }

        var operations = new JsonArray
        {
            (JsonNode)new JsonObject
            {
                ["op"] = "transfer",
                ["itemId"] = prop.ItemId,
                ["fromOwnerId"] = state == "held" ? "world" : "player",
                ["toOwnerId"] = state == "held" ? "player" : "world"
            }
        };
        await _bridge.DispatchWorldCustodyAsync(operations, new JsonArray { placement });
    }
}
