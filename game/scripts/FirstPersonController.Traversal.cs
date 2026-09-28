using Godot;

namespace Urman.Godot;

public partial class FirstPersonController
{
    private LadderTraversal3D? _activeLadder;
    private string _traversalNotice = string.Empty;
    private ulong _traversalNoticeUntil;
    private float _lastLadderSoundDistance;

    public bool IsClimbingLadder => _activeLadder is not null
        && GodotObject.IsInstanceValid(_activeLadder) && _activeLadder.Player == this;
    internal LadderTraversal3D? ActiveLadder => IsClimbingLadder ? _activeLadder : null;
    public string? SaveBlockReason => InIntro ? "Сохранение доступно после вступления."
        : IsClimbingLadder ? "Сохраниться можно на площадке. Сначала сойдите с лестницы." : null;

    internal void NotifyTraversal(string message)
    {
        _traversalNotice = message;
        _traversalNoticeUntil = Time.GetTicksMsec() + 2200;
    }

    public bool RequestLadderReturn()
    {
        if (!IsClimbingLadder) return false;
        if (!_activeLadder!.Cancel()) NotifyTraversal(_activeLadder.LastFailure);
        return true;
    }

    private void ReleaseLadderForPlacement()
    {
        if (_activeLadder is not null && GodotObject.IsInstanceValid(_activeLadder))
            _activeLadder.ReleaseForWorldPlacement(this);
        _activeLadder = null;
        _traversalNoticeUntil = 0;
    }

    private bool UpdateLadderMotion(double delta)
    {
        if (!IsClimbingLadder) { _activeLadder = null; return false; }
        var ladder = _activeLadder!;
        if (Input.IsActionJustPressed("pause")) RequestLadderReturn();
        ladder.PhysicsTick(delta, Input.GetAxis("move_backward", "move_forward"));
        UpdateHeadBob(delta, moving: false);
        if (ladder.DistanceTravelled - _lastLadderSoundDistance > .34f)
        {
            _lastLadderSoundDistance = ladder.DistanceTravelled;
            UiFoley.PlayWorld(this, GlobalPosition, "wood_tap");
        }
        if (!ladder.IsTraversing) _activeLadder = null;
        var gamepad = CurrentInputDevice == "gamepad";
        var forward = InputBindingService.ActionHint("move_forward", gamepad);
        var backward = InputBindingService.ActionHint("move_backward", gamepad);
        var cancel = InputBindingService.ActionHint("pause", gamepad);
        SetInteractionPrompt(Time.GetTicksMsec() < _traversalNoticeUntil ? _traversalNotice
            : !string.IsNullOrEmpty(ladder.LastFailure) ? ladder.LastFailure
            : ladder.IsReturning ? ladder.DisplayPrompt
            : $"{forward} вверх · {backward} вниз · {cancel} вернуться на площадку");
        // Even on the arrival frame ordinary walking waits until the next tick;
        // a held W must not cause a second displacement at the ladder's end.
        return true;
    }

    private bool OfferLadderInteraction()
    {
        foreach (var ladder in GetTree().GetNodesInGroup(LadderTraversal3D.GroupName).OfType<LadderTraversal3D>())
        {
            if (!ladder.CanOffer(this)) continue;
            var lower = ladder.ToGlobal(ladder.LowerGrip);
            var upper = ladder.ToGlobal(ladder.UpperGrip);
            var fromTop = GlobalPosition.DistanceSquaredTo(upper) < GlobalPosition.DistanceSquaredTo(lower);
            var point = (fromTop ? upper : lower) + Vector3.Up * (fromTop ? .22f : .85f);
            var ray = point - _camera.GlobalPosition;
            if (ray.LengthSquared() > 7.29f || ray.Normalized().Dot(-_camera.GlobalBasis.Z) < .65f) continue;
            var attempt = Input.IsActionJustPressed("interact");
            // The existing place/rotate actions remain usable near the ladder.
            if (_carryCoordinator?.HeldItem is not null && !attempt) return false;
            if (attempt)
            {
                if (IsCrouching && CanStandAt(GlobalPosition)) SetCrouched(false);
                if (IsCrouching) NotifyTraversal("Здесь тесно. Подойдите к площадке, где можно встать.");
                else if (ladder.TryBegin(this))
                {
                    _activeLadder = ladder;
                    _lastLadderSoundDistance = 0;
                    _traversalNoticeUntil = 0;
                }
                else NotifyTraversal(ladder.LastFailure);
            }
            _focusedTarget = _focusCandidate = _promptTarget = null;
            SetInteractionPrompt(Time.GetTicksMsec() < _traversalNoticeUntil
                ? _traversalNotice : $"{InteractionHint} {ladder.DisplayPrompt}");
            return true;
        }
        return false;
    }
}
