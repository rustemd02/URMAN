using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

/// <summary>
/// One input owner (FirstPersonController), one item owner (runtime custody).
/// The coordinator validates the physical attempt, commits it, then projects the
/// snapshot. Rejected attempts never move a prop or change a tool's result.
/// </summary>
public partial class CarryCoordinator : Node
{
    private const float Reach = 2.7f;
    private const float SlopeLimit = .90f;
    private readonly List<CarryableProp> _props = new();
    private readonly List<YardTool> _tools = new();
    private RuntimeBridge? _bridge;
    private FirstPersonController? _player;
    private Camera3D? _camera;
    private object? _registeredSession;
    private bool _registering;
    private bool _busy;
    private string? _lastProjection;
    private CarryableProp? _held;
    private string _feedback = string.Empty;
    private ulong _feedbackUntil;
    private string _zoneId = "village_day";
    private bool _exterior = true;
    private string? _heldPoseItem;
    private bool _heldPoseValid;

    internal CarryableProp? HeldItem => _held;
    internal IReadOnlyList<CarryableProp> Items => _props;
    internal bool ActionInProgress => _busy || _registering;
    internal Task<bool> PendingAction { get; private set; } = Task.FromResult(true);
    public IReadOnlyList<string> ItemIds => _props.Select(prop => prop.ItemId).ToArray();

    public static CarryCoordinator Create(IEnumerable<CarryableProp> props)
    {
        var owner = new CarryCoordinator { Name = "CarryCoordinator" };
        foreach (var prop in props) owner.Register(prop);
        return owner;
    }

    public void Register(CarryableProp prop)
    {
        if (_props.Any(item => item.ItemId == prop.ItemId))
            throw new InvalidOperationException($"Duplicate carry item {prop.ItemId}.");
        _props.Add(prop);
        AddChild(prop);
    }

    public void Register(YardTool tool)
    {
        if (_props.Any(item => item.ItemId == tool.Prop.ItemId))
            throw new InvalidOperationException($"Duplicate tool {tool.ToolId}.");
        _tools.Add(tool);
        _props.Add(tool.Prop);
        AddChild(tool);
    }

    public override void _Ready() => AddToGroup("carry_coordinator");

    public void AttachRuntimeState(RuntimeBridge bridge)
    {
        _bridge = bridge;
        ApplyWorldState();
    }

    // Called only by the ordinary controller, after modal gating and movement.
    // true consumes this frame's world interaction; false leaves a narrative
    // target to the controller, including when a lamp is held in the other hand.
    internal bool HandlePlayerInput(FirstPersonController player, Camera3D camera, out string prompt)
    {
        _player = player;
        _camera = camera;
        prompt = string.Empty;
        if (player.ModalOpen || _bridge?.SessionIdentity is not { } session) return false;
        if (!ReferenceEquals(_registeredSession, session)) ApplyWorldState();
        if (_registering || !ReferenceEquals(_registeredSession, session)) return false;

        var hit = Trace(camera.GlobalPosition, camera.GlobalPosition - camera.GlobalBasis.Z * Reach, 7u);
        var target = hit.Count == 0 ? null : hit["collider"].AsGodotObject();
        if (_held is not null) UpdateHeld();
        if (_busy)
        {
            prompt = "…";
            return true;
        }

        var interact = Input.IsActionJustPressed("interact");
        if (_held is not null)
        {
            // A commit may finish synchronously and clear _held. Finish this
            // input branch before consulting it again, and never replace the
            // accepted action's task with a second same-frame rejection.
            if (Input.IsActionJustPressed("carry_place"))
            {
                prompt = "Поставить предмет";
                PendingAction = PlaceAsync();
                return true;
            }
            if (Input.IsActionJustPressed("carry_rotate"))
            {
                prompt = "Повернуть предмет";
                PendingAction = RotateAsync();
                return true;
            }
            if (target is YardUseTarget use && !use.Completed)
            {
                var correct = _held.ToolId == use.Tool.ToolId;
                prompt = correct ? $"{player.InteractionHint} {use.Prompt}"
                    : $"Здесь пригодится {use.Tool.ToolName.ToLowerInvariant()}";
                if (interact)
                {
                    if (correct) PendingAction = UseAsync(use);
                    else Feedback($"{_held.PromptName}: здесь не поможет. Нужна {use.Tool.ToolName.ToLowerInvariant()}.");
                }
                prompt += $" · {Hint("carry_place")} поставить";
            }
            else if (target is InteractionTarget && _held.Class != CarryableProp.ItemClass.Bulky)
            {
                // The held lamp does not swallow a conversation or document.
                return false;
            }
            else
            {
                prompt = $"{Hint("carry_place")} поставить · {Hint("carry_rotate")} повернуть";
                if (_held.Kind == CarryableProp.ItemKind.Lantern)
                {
                    prompt = $"{player.InteractionHint} {(_held.LightOn ? "выключить" : "включить")} фонарь · " + prompt;
                    if (interact) PendingAction = LightAsync(_held, !_held.LightOn);
                }
                else if (interact) PendingAction = PlaceAsync();
            }
            if (Time.GetTicksMsec() < _feedbackUntil) prompt = _feedback;
            return true;
        }

        if (target is CarryableProp prop && !prop.IsConcealed && prop.IsVisibleInTree()
            && prop.State is CarryableProp.CarryState.World or CarryableProp.CarryState.Placed)
        {
            prompt = $"{player.InteractionHint} Взять: {prop.PromptName}";
            if (interact) PendingAction = TakeAsync(prop);
            return true;
        }
        if (target is YardUseTarget needed && !needed.Completed)
        {
            prompt = $"{needed.Prompt} — нужна {needed.Tool.ToolName.ToLowerInvariant()}";
            if (interact) Feedback($"Возьмите {needed.Tool.ToolName.ToLowerInvariant()} и поднесите к этому месту.");
            if (Time.GetTicksMsec() < _feedbackUntil) prompt = _feedback;
            return true;
        }
        return false;
    }

    private string Hint(string action) => InputBindingService.ActionHint(action, _player?.CurrentInputDevice == "gamepad");

    private global::Godot.Collections.Dictionary Trace(Vector3 from, Vector3 to, uint mask = 3u)
    {
        var ray = PhysicsRayQueryParameters3D.Create(from, to, mask);
        if (_player is not null) ray.Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
        return _camera!.GetWorld3D().DirectSpaceState.IntersectRay(ray);
    }

    private void UpdateHeld()
    {
        if (_held is null || _camera is null) return;
        if (_heldPoseItem != _held.ItemId) _heldPoseValid = false;
        _heldPoseItem = _held.ItemId;
        if (TryHeldPose(_held, Vector3.Zero, _held.YawDegrees, out var pose))
        {
            _held.HoldAt(pose);
            _heldPoseValid = true;
        }
    }

    private Task<bool> TakeAsync(CarryableProp prop) => CommitAsync(async () =>
    {
        if (_held is not null || prop.IsConcealed || !prop.IsVisibleInTree()
            || prop.State is not (CarryableProp.CarryState.World or CarryableProp.CarryState.Placed)) return false;
        if (IsSupportingSomething(prop))
        {
            Feedback("Сначала сойдите с опоры и снимите с неё вещи.");
            return false;
        }
        if (!TryHeldPose(prop, Vector3.Zero, prop.YawDegrees, out _))
        {
            Feedback("Здесь тесно для этого предмета. Подойдите с открытой стороны.");
            return false;
        }
        return await _bridge!.DispatchWorldCustodyAsync(Transfer(prop, "world", "player"),
            new JsonArray { Placement(prop, "held", yaw: prop.YawDegrees) });
    });

    private Task<bool> PlaceAsync() => CommitAsync(async () =>
    {
        if (_held is null) return false;
        if (!TryPlacement(_held, out var point, out var reason))
        {
            Feedback(reason);
            return false;
        }
        var record = Placement(_held, "placed", point, _held.YawDegrees);
        record["zone"] = _exterior ? string.Empty : _zoneId;
        return await _bridge!.DispatchWorldCustodyAsync(Transfer(_held, "player", "world"), new JsonArray { record });
    });

    private Task<bool> RotateAsync() => CommitAsync(() =>
    {
        if (_held is null) return Task.FromResult(false);
        var yaw = Mathf.PosMod(_held.YawDegrees + 15f, 360f);
        if (!CanRotateHeld(_held, yaw))
        {
            Feedback("Повернуть мешает преграда. Отойдите немного.");
            return Task.FromResult(false);
        }
        return _bridge!.DispatchWorldPropsAsync(new JsonArray { Placement(_held, "held", yaw: yaw) });
    });

    private Task<bool> LightAsync(CarryableProp prop, bool enabled) => CommitAsync(() =>
        _bridge!.DispatchWorldPropsAsync(new JsonArray { new JsonObject { ["propId"] = prop.ItemId, ["light"] = enabled } }));

    private Task<bool> UseAsync(YardUseTarget use) => CommitAsync(() =>
    {
        if (_held?.ToolId != use.Tool.ToolId || use.Completed) return Task.FromResult(false);
        return _bridge!.DispatchWorldPropsAsync(new JsonArray
        { new JsonObject { ["propId"] = use.StateKey, ["visible"] = !use.InitialVisible } });
    });

    private async Task<bool> CommitAsync(Func<Task<bool>> command)
    {
        if (_busy || _registering || _player?.ModalOpen != false || _bridge?.SessionIdentity is not { } session
            || !ReferenceEquals(session, _registeredSession)) return false;
        _busy = true;
        try
        {
            var committed = await command();
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || !ReferenceEquals(session, _bridge.SessionIdentity)) return false;
            if (committed) ApplyWorldState();
            else if (Time.GetTicksMsec() >= _feedbackUntil) Feedback("Не получилось. Предмет остался на месте.");
            return committed;
        }
        catch (Exception error)
        {
            GD.PushWarning($"carry: transaction rejected: {error.Message}");
            Feedback("Действие не сохранилось. Можно попробовать ещё раз.");
            return false;
        }
        finally { _busy = false; }
    }

    internal bool TryPlacement(CarryableProp prop, out Vector3 point, out string reason)
    {
        point = Vector3.Zero;
        reason = "Нужна ровная свободная опора";
        if (_camera is null || _player is null) return false;
        var origin = _camera.GlobalPosition;
        var forward = -_camera.GlobalBasis.Z;
        var horizontal = new Vector3(forward.X, 0, forward.Z).Normalized();
        var probe = origin + horizontal * Math.Max(1.15f, prop.HoldDistance);
        var aimed = Trace(origin, origin + forward * Reach);
        if (aimed.Count > 0 && aimed["normal"].AsVector3().Y >= SlopeLimit
            && (aimed["position"].AsVector3() - _player.GlobalPosition).Length() > .7f)
            probe = aimed["position"].AsVector3() + Vector3.Up * .5f;
        point = new(probe.X, probe.Y, probe.Z);
        var basis = Basis.FromEuler(new(0, Mathf.DegToRad(prop.YawDegrees), 0));
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        // A plank rests on both ends; requiring ground under its middle would
        // make a real short bridge impossible. Other items need the centre too.
        var feet = new List<Vector3>();
        if (prop.Kind != CarryableProp.ItemKind.Board) feet.Add(Vector3.Zero);
        foreach (var x in new[] { -.43f, .43f })
        foreach (var z in new[] { -.44f, .44f })
            feet.Add(new(prop.Size.X * x, 0, prop.Size.Z * z));
        foreach (var offset in feet)
        {
            var foot = point + basis * offset;
            var support = Trace(new(foot.X, origin.Y + .3f, foot.Z), new(foot.X, origin.Y - 3f, foot.Z));
            if (support.Count == 0 || support["normal"].AsVector3().Y < SlopeLimit) return false;
            var y = support["position"].AsVector3().Y;
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        if (maxY - minY > .055f) { reason = "Край предмета останется без опоры"; return false; }
        point.Y = maxY + .006f;
        if (origin.DistanceTo(point) > Reach + .15f) { reason = "Слишком далеко"; return false; }
        var visibility = Trace(origin, point + Vector3.Up * .035f);
        if (visibility.Count > 0 && visibility["position"].AsVector3().DistanceTo(point) > .10f)
        { reason = "Мешает преграда"; return false; }
        using var placementShape = new BoxShape3D { Size = prop.Size - new Vector3(.012f, .012f, .012f) };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = placementShape,
            Transform = new(basis, point + Vector3.Up * prop.Height * .5f), CollisionMask = 3u,
            Exclude = new global::Godot.Collections.Array<Rid> { prop.GetRid() }, Margin = .002f
        };
        if (_camera.GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count != 0)
        { reason = "Здесь не хватает места"; return false; }
        reason = string.Empty;
        return true;
    }

    private void Feedback(string message)
    {
        _feedback = message;
        _feedbackUntil = Time.GetTicksMsec() + 1800;
    }

    public void ApplyWorldState()
    {
        if (!IsInsideTree() || _bridge?.SessionIdentity is not { } session) return;
        if (!ReferenceEquals(_registeredSession, session))
        {
            if (!_registering) _ = RegisterSessionAsync(session);
            return;
        }
        var props = _bridge.SelectWorldProps();
        var projection = props.GetRawText();
        if (_lastProjection == projection) return;
        _lastProjection = projection;
        var previousHeldId = _held?.ItemId;
        var previousHeldPosition = _held?.GlobalPosition;
        _held = null;
        foreach (var prop in _props) prop.ResetToAuthored();
        // Reset source-dependent concealment before projecting a moved item.
        // A loaded held/placed axe cannot remain hidden under its old snow.
        foreach (var tool in _tools) tool.RestoreResults(props);
        foreach (var prop in _props)
        {
            if (!props.TryGetProperty(prop.ItemId, out var record)) continue;
            var state = record.TryGetProperty("state", out var value) ? value.GetString() : null;
            var yaw = record.TryGetProperty("yaw", out var angle) ? angle.GetSingle() : prop.YawDegrees;
            if (state == "held")
            {
                if (_held is not null) throw new InvalidOperationException("Snapshot has more than one carried item.");
                prop.Take();
                prop.Rotate(yaw - prop.YawDegrees);
                _held = prop;
            }
            else if (state is "placed" or "combined"
                && record.TryGetProperty("x", out var x) && record.TryGetProperty("y", out var y) && record.TryGetProperty("z", out var z))
                prop.Place(new(x.GetSingle(), y.GetSingle(), z.GetSingle()), yaw, state == "combined");
            if (record.TryGetProperty("light", out var light)) prop.SetLight(light.GetBoolean());
            prop.PlacementZone = record.TryGetProperty("zone", out var zone) ? zone.GetString() ?? string.Empty : string.Empty;
        }
        if (_held is not null && _heldPoseValid && previousHeldId == _held.ItemId
            && previousHeldPosition is { } heldPosition)
            _held.HoldAt(heldPosition);
        ApplyZonePresentation();
        if (_held is null) { _heldPoseItem = null; _heldPoseValid = false; }
        UpdateHeld();
    }

    public void SetZonePresentation(string zoneId, bool exterior)
    {
        _zoneId = zoneId;
        _exterior = exterior;
        ApplyZonePresentation();
    }

    private void ApplyZonePresentation()
    {
        foreach (var prop in _props)
            prop.SetPresentationEnabled(prop.State == CarryableProp.CarryState.Held
                || (string.IsNullOrEmpty(prop.PlacementZone) ? _exterior : prop.PlacementZone == _zoneId));
        foreach (var tool in _tools)
        foreach (var target in tool.Targets) target.SetWorldEnabled(_exterior);
    }

    private async Task RegisterSessionAsync(object session)
    {
        _registering = true;
        try
        {
            await _bridge!.RegisterWorldItemsAsync(ItemIds);
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || !ReferenceEquals(session, _bridge.SessionIdentity)) return;
            _registeredSession = session;
            _lastProjection = null;
            _heldPoseValid = false;
            _heldPoseItem = null;
            ApplyWorldState();
        }
        catch (Exception error) { GD.PushWarning($"carry: registration failed: {error.Message}"); }
        finally { _registering = false; }
    }

    private static JsonArray Transfer(CarryableProp prop, string from, string to) => new()
    {
        new JsonObject { ["op"] = "transfer", ["itemId"] = prop.ItemId, ["fromOwnerId"] = from, ["toOwnerId"] = to }
    };

    private static JsonObject Placement(CarryableProp prop, string state, Vector3? point = null, float? yaw = null)
    {
        var record = new JsonObject { ["propId"] = prop.ItemId, ["state"] = state };
        if (point is { } p) { record["x"] = p.X; record["y"] = p.Y; record["z"] = p.Z; }
        if (yaw is { } angle) record["yaw"] = angle;
        return record;
    }
}
