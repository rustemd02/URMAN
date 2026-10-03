using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

public partial class CarryCoordinator
{
    private readonly List<YardMechanism> _mechanisms = new();
    internal IReadOnlyList<YardMechanism> Mechanisms => _mechanisms;

    public void Register(YardMechanism mechanism)
    {
        if (_mechanisms.Any(item => item.StateKey == mechanism.StateKey))
            throw new InvalidOperationException($"Duplicate yard mechanism {mechanism.StateKey}.");
        _mechanisms.Add(mechanism);
        AddChild(mechanism);
    }

    private bool HandleMechanism(GodotObject? aimed, bool interact, out string prompt)
    {
        prompt = string.Empty;
        if (aimed is CarryableProp other && _held is not null
            && ((_held.Kind == CarryableProp.ItemKind.Pole && other.Kind == CarryableProp.ItemKind.Hook)
                || (_held.Kind == CarryableProp.ItemKind.Hook && other.Kind == CarryableProp.ItemKind.Pole)))
        {
            prompt = $"{_player!.InteractionHint} Закрепить крючок в прорези шеста";
            if (interact) PendingAction = JoinHookAsync(other);
            if (Time.GetTicksMsec() < _feedbackUntil) prompt = _feedback;
            return true;
        }
        if (aimed is not YardMechanism target) return false;
        var reason = MechanismRefusal(target);
        prompt = $"{_player!.InteractionHint} {target.Prompt}";
        if (target.Solved) prompt = target.ResultText;
        else if (reason is not null) prompt += " — " + reason;
        if (!target.Solved && !string.IsNullOrEmpty(target.SoundCaption))
            prompt = target.SoundCaption + " · " + prompt;
        if (interact)
        {
            if (target.Solved && target.StateKey == "yard/loose-footboard")
                PendingAction = CommitAsync(() => RecordQuietBoardAsync(target));
            else if (reason is null) PendingAction = UseMechanismAsync(target);
            else Feedback(reason);
        }
        if (Time.GetTicksMsec() < _feedbackUntil) prompt = _feedback;
        return true;
    }

    private string? MechanismRefusal(YardMechanism target)
    {
        if (target.Solved && target.Action != YardMechanism.Operation.WarmCloth)
            return target.ResultText;
        if (!string.IsNullOrEmpty(target.PrerequisiteKey)
            && !YardMechanism.Flag(_bridge!.SelectWorldProps(), target.PrerequisiteKey))
            return target.RequirementText;
        var kind = _held?.Kind;
        return target.Action switch
        {
            YardMechanism.Operation.WarmCloth => kind != CarryableProp.ItemKind.Cloth
                ? "Нужна чистая ткань; в миске тёплая вода" : null,
            YardMechanism.Operation.Thaw => kind != CarryableProp.ItemKind.Cloth || !_held!.IsWarm
                ? "Нужна тёплая влажная ткань. Свет фонаря не растопит лёд" : null,
            YardMechanism.Operation.ReadByLight => LightRefusal(target.GlobalPosition),
            YardMechanism.Operation.Nudge => kind != CarryableProp.ItemKind.Pole
                && !(kind is null && target.AlternativeApproach is { } side
                    && _player!.GlobalPosition.DistanceTo(side) < 1.25f)
                ? target.RequirementText : null,
            YardMechanism.Operation.HookLatch => !(kind == CarryableProp.ItemKind.Pole && _held!.HasHook)
                && !(kind is null && _player!.IsOnFloor()
                    && target.GlobalPosition.Y - _player.GlobalPosition.Y < 1.75f)
                ? "Нужен шест с крючком; с верхней площадки можно открыть рукой" : null,
            YardMechanism.Operation.RestBoard => kind != CarryableProp.ItemKind.Board
                ? "На две устойчивые опоры можно положить доску" : null,
            YardMechanism.Operation.DetachHook => kind != CarryableProp.ItemKind.Pole || !_held!.HasHook
                ? "Здесь можно снять крючок с шеста и оставить на полке" : null,
            YardMechanism.Operation.QuietRattle => kind is not null
                ? "Сначала освободите руку" : null,
            _ => null
        };
    }

    private string? LightRefusal(Vector3 point)
    {
        if (_camera is null) return "Поднесите включённый фонарь";
        var nearby = false;
        foreach (var lamp in _props.Where(item => item.Kind == CarryableProp.ItemKind.Lantern
            && item.LightOn && item.IsVisibleInTree()))
        {
            var source = lamp.GlobalPosition + Vector3.Up * .185f;
            if (source.DistanceSquaredTo(point) > 6.25f) continue;
            nearby = true;
            using var ray = PhysicsRayQueryParameters3D.Create(source, point, 3u);
            var lampExclude = new global::Godot.Collections.Array<Rid> { lamp.GetRid(), _player!.GetRid() };
            using var lampExcludeOwner = (global::Godot.Collections.Array)lampExclude;
            ray.Exclude = lampExclude;
            using var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            if (hit.Count == 0 || hit["position"].AsVector3().DistanceTo(point) < .08f) return null;
        }
        return nearby
            ? "Свет перекрыт. Присядьте или поставьте фонарь напротив карточки"
            : "Поднесите включённый фонарь или поставьте его рядом с этой стороной";
    }

    private Task<bool> UseMechanismAsync(YardMechanism target) => CommitAsync(async () =>
    {
        // Recheck the actual physical requirement at the commit boundary.
        if (MechanismRefusal(target) is not null) return false;
        var changes = new JsonArray();
        JsonArray? moves = null;
        if (target.Action == YardMechanism.Operation.WarmCloth)
            changes.Add(new JsonObject { ["propId"] = _held!.ItemId, ["warm"] = true });
        else if (target.Action == YardMechanism.Operation.Thaw)
            changes.Add(new JsonObject { ["propId"] = _held!.ItemId, ["warm"] = false });
        else if (target.Action == YardMechanism.Operation.RestBoard)
        {
            var board = _held!;
            if (!TryRestAt(board, target.RestPoint, target.RestYaw, out var supported, out var reason))
            { Feedback(reason); return false; }
            moves = Transfer(board, "player", "world");
            var placed = Placement(board, "combined", supported, target.RestYaw);
            placed["assembly"] = target.StateKey;
            placed["zone"] = target.ZoneId;
            changes.Add(placed);
        }
        else if (target.Action == YardMechanism.Operation.DetachHook)
        {
            var hook = _props.Single(item => item.Kind == CarryableProp.ItemKind.Hook);
            if (!TryRestAt(hook, target.RestPoint, 0, out var supported, out var reason))
            { Feedback(reason); return false; }
            moves = Transfer(hook, "attachment/" + _held!.ItemId, "world");
            var detached = Placement(hook, "placed", supported, 0);
            detached["attachedTo"] = string.Empty;
            detached["zone"] = target.ZoneId;
            changes.Add(detached);
            changes.Add(new JsonObject { ["propId"] = _held.ItemId, ["hook"] = false });
        }
        var solved = target.Action is not (YardMechanism.Operation.WarmCloth or YardMechanism.Operation.DetachHook);
        changes.Add(new JsonObject { ["propId"] = target.StateKey, ["solved"] = solved, ["heard"] = true });
        var accepted = moves is null ? await _bridge!.DispatchWorldPropsAsync(changes)
            : await _bridge!.DispatchWorldCustodyAsync(moves, changes);
        if (accepted)
        {
            Feedback(target.ResultText);
            if (!string.IsNullOrEmpty(target.SoundSample))
                UiFoley.PlayWorld(this, target.GlobalPosition, target.SoundSample);
            if (target.StateKey == "yard/loose-footboard") await RecordQuietBoardAsync(target);
        }
        return accepted;
    });

    private async Task<bool> RecordQuietBoardAsync(YardMechanism target)
    {
        const string action = "urman.chapter1:interaction/mark-underdeck-quiet";
        // Repair can precede the source inspection. Rechecking the repaired
        // board afterwards records the observation; ticking/entering never does.
        if (!_bridge!.IsInteractionAvailable(action)) { Feedback(target.ResultText); return true; }
        var recorded = await _bridge.DispatchInteractionAsync(action);
        Feedback(recorded ? "Дощечка больше не стучит. Теперь можно рассказать, что было под настилом." : target.ResultText);
        return recorded;
    }

    private Task<bool> JoinHookAsync(CarryableProp other) => CommitAsync(async () =>
    {
        if (_held is null || other.State is not (CarryableProp.CarryState.World or CarryableProp.CarryState.Placed)) return false;
        var pole = _held.Kind == CarryableProp.ItemKind.Pole ? _held : other;
        var hook = _held.Kind == CarryableProp.ItemKind.Hook ? _held : other;
        if (pole.HasHook || pole.Kind != CarryableProp.ItemKind.Pole || hook.Kind != CarryableProp.ItemKind.Hook) return false;
        if (IsSupportingSomething(pole) || IsSupportingSomething(hook)
            || !TryHeldPose(pole, Vector3.Zero, pole.YawDegrees, out var heldPose,
                resultingEnvelope: new Vector3(.16f, .14f, 1.90f), fromWorld: pole != _held))
        { Feedback("Для шеста с крючком здесь тесно. Отойдите к открытому месту."); return false; }
        var moves = Transfer(hook, hook == _held ? "player" : "world", "attachment/" + pole.ItemId);
        if (pole != _held)
            moves.Add(new JsonObject { ["op"] = "transfer", ["itemId"] = pole.ItemId,
                ["fromOwnerId"] = "world", ["toOwnerId"] = "player" });
        var held = Placement(pole, "held", yaw: pole.YawDegrees);
        held["hook"] = true;
        _approvedHeldPose = (pole.ItemId, heldPose);
        return await _bridge!.DispatchWorldCustodyAsync(moves, new JsonArray
        {
            held, new JsonObject { ["propId"] = hook.ItemId, ["state"] = "attached", ["attachedTo"] = pole.ItemId }
        });
    });

    // Local diagnostic only; it neither moves a prop nor changes runtime state.
    internal string DescribeRestProbe(CarryableProp prop, Vector3 desired, float yaw)
    {
        if (_camera is null || _player is null) return "rest probe has no player";
        var accepted = TryRestAt(prop, desired, yaw, out var feet, out var reason);
        var space = _camera.GetWorld3D().DirectSpaceState;
        using var shape = new BoxShape3D { Size = prop.Size };
        string Contacts(float margin)
        {
            using var query = new PhysicsShapeQueryParameters3D
            {
                Shape = shape, Transform = new(Basis.FromEuler(new(0, Mathf.DegToRad(yaw), 0)),
                    feet + Vector3.Up * prop.Height * .5f), CollisionMask = 3u, Margin = margin,
                Exclude = new global::Godot.Collections.Array<Rid> { prop.GetRid() }
            };
            return string.Join(" | ", space.IntersectShape(query, 8).Select(hit =>
            {
                var body = hit["collider"].AsGodotObject() as Node;
                var owner = body is CollisionObject3D physical
                    ? physical.ShapeOwnerGetOwner(physical.ShapeFindOwner(hit["shape"].AsInt32())) as Node : null;
                return $"{body?.GetPath()} / {owner?.Name}";
            }));
        }
        return $"item={prop.ItemId} feet={feet} accepted={accepted} reason={reason}; full-body margin2mm=[{Contacts(.002f)}]; carried-margin8mm=[{Contacts(.008f)}]";
    }

    // Full-footprint validation for authored rests: no snapping through walls,
    // placing in mid-air or straddling unequal feet.
    private bool TryRestAt(CarryableProp prop, Vector3 desired, float yaw, out Vector3 feet, out string reason)
    {
        feet = desired;
        reason = "Нужна свободная устойчивая опора";
        if (_camera is null || _player is null || _camera.GlobalPosition.DistanceTo(desired) > Reach + .15f) return false;
        var basis = Basis.FromEuler(new Vector3(0, Mathf.DegToRad(yaw), 0));
        var heights = new List<float>();
        foreach (var x in new[] { -.43f, .43f })
        foreach (var z in new[] { -.44f, .44f })
        {
            var foot = desired + basis * new Vector3(prop.Size.X * x, 0, prop.Size.Z * z);
            using var support = Trace(foot + Vector3.Up * .20f, foot - Vector3.Up * .22f);
            if (support.Count == 0 || support["normal"].AsVector3().Y < SlopeLimit) return false;
            heights.Add(support["position"].AsVector3().Y);
        }
        if (heights.Max() - heights.Min() > .025f) return false;
        feet.Y = heights.Max() + .008f;
        using var hit = Trace(_camera.GlobalPosition, feet + Vector3.Up * (prop.Height * .5f));
        if (hit.Count > 0 && hit["position"].AsVector3().DistanceTo(feet) > .12f)
        { reason = "Между рукой и опорой есть преграда"; return false; }
        // The full body rests 8mm above its verified support. The carried sweep
        // margin is also 8mm, so using it here reports the shelf's mere contact
        // as occupied space. A 2mm rest margin leaves the full item volume and
        // the player in this query while separating them from the support skin.
        if (!ClearVolume(prop, feet, yaw, includePlayer: true, margin: .002f))
        { reason = "Место занято. Отойдите в сторону и освободите опору."; return false; }
        reason = string.Empty;
        return true;
    }
}
