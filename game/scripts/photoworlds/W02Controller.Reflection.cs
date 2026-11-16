using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class W02Controller
{
    private const double ReflectionObservationPollSeconds = 0.2;
    private const float ReflectionComparisonHoldSeconds = 0.45f;
    private const float ReflectionCompositionTolerance = 0.62f;
    private const float ReflectionViewpointTolerance = 0.52f;
    private const string ReflectionFactId = "water_reflection_relation_seen";
    private const string ReflectionSketchAssetId = "urman.fullgame:asset/w02-water-reflection-relation-sketch";
    private const string ReflectionLabelTextId = "urman.fullgame:text/pw-label-water-reflection";

    // Geometry is authored by W02Controller after the R03 outcrop is built.
    // These are the three reflected supports and the old approach edge below
    // the water; the same samples are used for the objective and comparison.
    private Vector3[] _reflectionObservationPoints = [];
    private Node3D? _reflectionObservationStand;
    private Vector2 _reflectionObservationStandHalfExtents;
    private readonly global::Godot.Collections.Array<Rid> _reflectionObservationRayExclusions = new();
    private readonly System.Collections.Generic.List<MeshInstance3D> _reflectionClueMeshes = [];
    private Material? _reflectionClueMaterial;
    private Material? _reflectionDiscoveredClueMaterial;
    private InteractionTarget? _reflectionTarget;
    private Label3D? _reflectionComparisonGuide;
    private object? _reflectionObservationSessionIdentity;
    private bool _reflectionComparisonActive;
    private double _reflectionComparisonElapsed;
    private object? _reflectionComparisonSession;
    private string? _reflectionComparisonSceneId;
    private object? _reflectionObservationDispatchSession;
    private bool _reflectionObservationDispatching;
    private int _reflectionObservationGeneration;
    private double _reflectionObservationPollElapsed;
    private object? _reflectionRemarkShownSession;
    private object? _reflectionRemarkPendingSession;

    internal bool TryGetReflectionObjectiveTarget(object? identity, FirstPersonController player,
        out Vector3 point, out string label, out bool inspectionPoint)
    {
        point = default;
        label = string.Empty;
        inspectionPoint = false;
        if (!IsCurrentRiverObjective(identity)
            || _reflectionObservationStand is not { } stand || !GodotObject.IsInstanceValid(stand)
            || !stand.IsVisibleInTree() || _reflectionObservationPoints.Length != 4)
            return false;

        if (_reflectionComparisonActive)
        {
            if (!player.IsOnFloor() || !IsInsideReflectionStand(stand, player.GlobalPosition))
            {
                point = stand.GlobalPosition;
                label = "Вернитесь на сухую площадку";
                return true;
            }

            var stance = stand.ToLocal(player.GlobalPosition);
            if (stance.X * stance.X + stance.Z * stance.Z > ReflectionViewpointTolerance * ReflectionViewpointTolerance)
            {
                point = stand.GlobalPosition;
                label = "Сместитесь к центру площадки — отсюда читается ракурс";
                return true;
            }

            point = ReflectionComparisonCenter();
            label = "Сведите отражённые опоры и край в кадре";
            inspectionPoint = true;
            return true;
        }

        if (!player.IsOnFloor())
        {
            point = stand.GlobalPosition;
            label = "Площадка у воды";
            return true;
        }

        var target = _reflectionTarget;
        if (target is { PresentationEnabled: true }
            && GodotObject.IsInstanceValid(target) && target.IsVisibleInTree()
            && player.GlobalPosition.DistanceTo(target.GlobalPosition) <= 2.4f)
        {
            point = target.GlobalPosition;
            label = "Сверить отражение с отпечатком";
            inspectionPoint = true;
            return true;
        }

        var offset = stand.ToLocal(player.GlobalPosition);
        const float centredRadius = 0.55f;
        if (offset.X * offset.X + offset.Z * offset.Z > centredRadius * centredRadius)
        {
            point = stand.GlobalPosition;
            label = "Площадка у воды";
            return true;
        }

        point = ReflectionComparisonCenter();
        label = "Сверьте три стойки и край настила с отпечатком";
        inspectionPoint = true;
        return true;
    }

    private Vector3 ReflectionComparisonCenter()
    {
        var center = Vector3.Zero;
        foreach (var subject in _reflectionObservationPoints) center += subject;
        return _reflectionObservationPoints.Length == 0
            ? center
            : center / _reflectionObservationPoints.Length;
    }

    private bool IsInsideReflectionStand(Node3D stand, Vector3 position)
    {
        var local = stand.ToLocal(position);
        return Math.Abs(local.X) <= _reflectionObservationStandHalfExtents.X
            && Math.Abs(local.Z) <= _reflectionObservationStandHalfExtents.Y;
    }

    private void BuildReflectionComparisonGuide()
    {
        _reflectionComparisonGuide = new Label3D
        {
            Name = "ReflectionComparisonGuide",
            Text = string.Empty,
            Font = ThemeDB.FallbackFont,
            FontSize = 38,
            PixelSize = 0.0018f,
            Width = 760,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Shaded = false,
            Modulate = new Color("f5efdd"),
            OutlineModulate = new Color("354037"),
            OutlineSize = 5,
            Visible = false
        };
        _world!.AddChild(_reflectionComparisonGuide);
        _reflectionComparisonGuide.Position = ReflectionStandCenter + new Vector3(0.1f, 1.12f, -0.45f);
    }

    private void SetReflectionComparisonGuide(string text, bool visible = true)
    {
        if (_reflectionComparisonGuide is not { } guide || !GodotObject.IsInstanceValid(guide)) return;
        if (!string.Equals(guide.Text, text, StringComparison.Ordinal)) guide.Text = text;
        guide.Visible = visible;
    }

    private void ApplyReflectionDiscoveryProjection(bool visible, bool discovered)
    {
        if (_reflectionComparisonGuide is { } guide && GodotObject.IsInstanceValid(guide))
            guide.Visible = visible && _reflectionComparisonActive;

        var material = discovered ? _reflectionDiscoveredClueMaterial : _reflectionClueMaterial;
        if (material is null) return;
        foreach (var mesh in _reflectionClueMeshes)
        {
            if (GodotObject.IsInstanceValid(mesh))
                mesh.MaterialOverride = material;
        }

        if (discovered)
        {
            _reflectionComparisonActive = false;
            _reflectionComparisonElapsed = 0;
            _reflectionComparisonSession = null;
            _reflectionComparisonSceneId = null;
            _reflectionTarget?.SetPresentationEnabled(false);
        }
    }

    private void ResetReflectionObservation(object? sessionIdentity)
    {
        _reflectionObservationGeneration++;
        _reflectionObservationSessionIdentity = sessionIdentity;
        _reflectionComparisonActive = false;
        _reflectionComparisonElapsed = 0;
        _reflectionComparisonSession = null;
        _reflectionComparisonSceneId = null;
        _reflectionObservationPollElapsed = 0;
        _reflectionObservationDispatchSession = null;
        _reflectionObservationDispatching = false;
        _reflectionRemarkShownSession = null;
        _reflectionRemarkPendingSession = null;
        _reflectionObservationRayExclusions.Clear();
        SetReflectionComparisonGuide(string.Empty, visible: false);
    }

    private bool BeginReflectionComparison()
    {
        if (_reflectionComparisonActive
            || _reflectionObservationDispatching
            || _bridge is not { } bridge
            || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || bridge.CurrentW02WaterProjection() is not { ActiveSceneId: RiverSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, session)
            || bridge.PhotoWorldFactObserved(ReflectionFactId)
            || !bridge.IsInteractionAvailable(ReflectionInteractionId)
            || MainMenuUi.AnyUndismissed(GetTree())
            || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor()
            || _reflectionObservationStand is not { } stand
            || !GodotObject.IsInstanceValid(stand)
            || !IsInsideReflectionStand(stand, player.GlobalPosition))
            return false;

        _reflectionComparisonActive = true;
        _reflectionComparisonSession = session;
        _reflectionComparisonSceneId = projection.ActiveSceneId;
        _reflectionComparisonElapsed = 0;
        _reflectionObservationGeneration++;
        var cancelHint = InputBindingService.ActionHint("ui_cancel", player.CurrentInputDevice == "gamepad");
        SetReflectionComparisonGuide(
            $"Сравните три отражённые опоры и край настила с отпечатком.\nСведите их к центру взгляда; {cancelHint} — выйти, можно отойти от площадки.");
        _reflectionTarget?.SetPresentationEnabled(false);
        return false;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!_reflectionComparisonActive || _reflectionObservationDispatching
            || inputEvent is InputEventKey { Echo: true }
            || !(inputEvent.IsActionPressed("ui_cancel") || inputEvent.IsActionPressed("pause")))
            return;

        CancelReflectionComparison(restoreTarget: true);
    }

    private void CancelReflectionComparison(bool restoreTarget)
    {
        if (!_reflectionComparisonActive) return;
        _reflectionObservationGeneration++;
        _reflectionComparisonActive = false;
        _reflectionComparisonElapsed = 0;
        _reflectionComparisonSession = null;
        _reflectionComparisonSceneId = null;
        SetReflectionComparisonGuide(string.Empty, visible: false);

        if (!restoreTarget
            || _reflectionTarget is not { } target || !GodotObject.IsInstanceValid(target)
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.CurrentW02WaterProjection() is not { ActiveSceneId: RiverSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, bridge.SessionIdentity)
            || bridge.PhotoWorldFactObserved(ReflectionFactId))
            return;

        target.SetPresentationEnabled(bridge.IsInteractionAvailable(ReflectionInteractionId));
    }

    private void ProcessReflectionObservation(double delta)
    {
        if (!IsInsideTree()
            || _bridge is not { } bridge
            || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || bridge.SessionIdentity is not { } session)
        {
            if (_reflectionComparisonActive) CancelReflectionComparison(restoreTarget: false);
            _reflectionRemarkPendingSession = null;
            return;
        }

        if (!ReferenceEquals(_reflectionObservationSessionIdentity, session))
            ResetReflectionObservation(session);

        var projection = bridge.CurrentW02WaterProjection();
        if (projection is null || projection.ActiveSceneId != RiverSceneId
            || !ReferenceEquals(projection.SessionIdentity, session))
        {
            if (_reflectionComparisonActive) CancelReflectionComparison(restoreTarget: false);
            if (!bridge.IsW01PresentationDeferred)
                _reflectionRemarkPendingSession = null;
            _reflectionObservationPollElapsed = 0;
            return;
        }

        if (bridge.PhotoWorldFactObserved(ReflectionFactId))
        {
            if (_reflectionComparisonActive && !_reflectionObservationDispatching)
                CancelReflectionComparison(restoreTarget: false);
            if (!ReferenceEquals(_reflectionRemarkPendingSession, session))
            {
                _reflectionObservationPollElapsed = 0;
                return;
            }

            _reflectionObservationPollElapsed += Math.Max(0, delta);
            if (_reflectionObservationPollElapsed >= ReflectionObservationPollSeconds)
            {
                _reflectionObservationPollElapsed = 0;
                _ = ShowRecordedReflectionRemark(bridge, session);
            }
            return;
        }

        _reflectionRemarkPendingSession = null;
        if (!_reflectionComparisonActive || _reflectionObservationDispatching) return;

        if (!ReferenceEquals(_reflectionComparisonSession, session)
            || _reflectionComparisonSceneId != projection.ActiveSceneId
            || !bridge.IsInteractionAvailable(ReflectionInteractionId)
            || MainMenuUi.AnyUndismissed(GetTree())
            || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor()
            || _reflectionObservationStand is not { } stand
            || !GodotObject.IsInstanceValid(stand)
            || !IsInsideReflectionStand(stand, player.GlobalPosition))
        {
            CancelReflectionComparison(restoreTarget: true);
            return;
        }

        var stance = stand.ToLocal(player.GlobalPosition);
        if (stance.X * stance.X + stance.Z * stance.Z > ReflectionViewpointTolerance * ReflectionViewpointTolerance)
        {
            _reflectionComparisonElapsed = 0;
            SetReflectionComparisonGuide("Сместитесь к центру сухой площадки — оттуда читается нужный ракурс.");
            return;
        }

        var camera = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (camera is null || !GodotObject.IsInstanceValid(camera) || !camera.Current)
        {
            CancelReflectionComparison(restoreTarget: true);
            return;
        }

        if (!_reflectionObservationRayExclusions.Contains(player.GetRid()))
        {
            _reflectionObservationRayExclusions.Clear();
            _reflectionObservationRayExclusions.Add(player.GetRid());
        }

        var eye = camera.GlobalPosition;
        var ready = ReflectionCompositionAligned(camera, eye, GetWorld3D().DirectSpaceState, out var feedback);
        SetReflectionComparisonGuide(feedback);
        if (!ready)
        {
            _reflectionComparisonElapsed = 0;
            return;
        }

        _reflectionComparisonElapsed += Math.Clamp(delta, 0d, 0.1d);
        if (_reflectionComparisonElapsed < ReflectionComparisonHoldSeconds) return;

        _reflectionComparisonElapsed = 0;
        _reflectionObservationDispatching = true;
        _reflectionObservationDispatchSession = session;
        SetReflectionComparisonGuide("Совпадение найдено. Отражённая линия продолжается под водой.");
        DispatchReflectionObservationAsync(bridge, session, _reflectionObservationGeneration);
    }

    private bool ReflectionCompositionAligned(
        Camera3D camera,
        Vector3 eye,
        PhysicsDirectSpaceState3D space,
        out string feedback)
    {
        feedback = "Наклоните взгляд к отражению.";
        if (_reflectionObservationPoints.Length != 4) return false;

        var viewport = camera.GetViewport().GetVisibleRect().Size;
        var aspect = viewport.X / Math.Max(viewport.Y, 1f);
        var tangent = Mathf.Tan(Mathf.DegToRad(camera.Fov) * 0.5f);
        var halfWidth = camera.KeepAspect == Camera3D.KeepAspectEnum.Width ? tangent : tangent * aspect;
        var halfHeight = camera.KeepAspect == Camera3D.KeepAspectEnum.Width ? tangent / aspect : tangent;
        var localCenter = Vector3.Zero;
        foreach (var point in _reflectionObservationPoints)
        {
            var local = camera.ToLocal(point);
            if (local.Z >= -0.05f)
            {
                feedback = "Повернитесь к воде и найдите отражённые опоры.";
                return false;
            }

            var normalizedX = local.X / -local.Z / Math.Max(halfWidth, 0.001f);
            var normalizedY = local.Y / -local.Z / Math.Max(halfHeight, 0.001f);
            if (Math.Abs(normalizedX) > 1f || Math.Abs(normalizedY) > 1f)
            {
                feedback = "Сведите в кадр три отражённые стойки и диагональный край.";
                return false;
            }

            using var ray = PhysicsRayQueryParameters3D.Create(eye, point, 3u, _reflectionObservationRayExclusions);
            using var hit = space.IntersectRay(ray);
            if (hit.Count != 0)
            {
                feedback = "Сместитесь по сухой площадке: камыш закрывает часть отражения.";
                return false;
            }

            localCenter += local;
        }

        localCenter /= _reflectionObservationPoints.Length;
        var centerX = localCenter.X / -localCenter.Z / Math.Max(halfWidth, 0.001f);
        var centerY = localCenter.Y / -localCenter.Z / Math.Max(halfHeight, 0.001f);
        if (Math.Abs(centerX) > ReflectionCompositionTolerance
            || Math.Abs(centerY) > ReflectionCompositionTolerance)
        {
            feedback = "Поверните взгляд к середине между отражёнными стойками.";
            return false;
        }

        feedback = "Совпало. Удержите кадр ещё мгновение.";
        return true;
    }

    private async void DispatchReflectionObservationAsync(
        RuntimeBridge bridge,
        object session,
        int generation)
    {
        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(ReflectionInteractionId);
        }
        catch (Exception exception)
        {
            GD.PushError($"W02 water-reflection comparison failed before commit: {exception.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || generation != _reflectionObservationGeneration) return;
        if (ReferenceEquals(_reflectionObservationDispatchSession, session))
        {
            _reflectionObservationDispatchSession = null;
            _reflectionObservationDispatching = false;
        }

        if (!GodotObject.IsInstanceValid(bridge) || !ReferenceEquals(bridge.SessionIdentity, session)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge))
        {
            ApplyProjection();
            return;
        }

        var projection = bridge.CurrentW02WaterProjection();
        if (committed && bridge.IsW01PresentationDeferred && bridge.ActiveSceneId == RiverSceneId)
        {
            // A failed pre-projection load can return to this same kernel.
            // Keep only this newly earned payoff; normal save restoration never seeds it.
            _reflectionRemarkPendingSession = session;
            _reflectionComparisonActive = false;
            _reflectionComparisonSession = null;
            _reflectionComparisonSceneId = null;
            SetReflectionComparisonGuide(string.Empty, visible: false);
            return;
        }
        if (committed && projection is { ActiveSceneId: RiverSceneId } current
            && ReferenceEquals(current.SessionIdentity, session)
            && bridge.PhotoWorldFactObserved(ReflectionFactId))
        {
            _reflectionComparisonActive = false;
            _reflectionComparisonSession = null;
            _reflectionComparisonSceneId = null;
            SetReflectionComparisonGuide(string.Empty, visible: false);
            ApplyReflectionDiscoveryProjection(visible: true, discovered: true);
            _reflectionRemarkPendingSession = session;
            await ShowRecordedReflectionRemark(bridge, session, deferIfModal: true);
            return;
        }

        _reflectionComparisonActive = false;
        _reflectionComparisonSession = null;
        _reflectionComparisonSceneId = null;
        SetReflectionComparisonGuide(string.Empty, visible: false);
        if (projection is { ActiveSceneId: RiverSceneId } stillCurrent
            && ReferenceEquals(stillCurrent.SessionIdentity, session)
            && !bridge.PhotoWorldFactObserved(ReflectionFactId))
        {
            _reflectionTarget?.SetPresentationEnabled(bridge.IsInteractionAvailable(ReflectionInteractionId));
            if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player
                && !player.ModalOpen)
                player.ShowRemark("Айдар", "Отражения разошлись. Попробую снова с сухой площадки.");
            return;
        }

        ApplyProjection();
    }

    private Task<bool> ShowRecordedReflectionRemark(
        RuntimeBridge bridge,
        object? session,
        bool deferIfModal = false)
    {
        if (!IsInsideTree() || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || session is null || !ReferenceEquals(session, bridge.SessionIdentity)
            || bridge.CurrentW02WaterProjection() is not { ActiveSceneId: RiverSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, session)
            || !bridge.PhotoWorldFactObserved(ReflectionFactId))
            return Task.FromResult(true);

        if (ReferenceEquals(_reflectionRemarkShownSession, session))
        {
            _reflectionRemarkPendingSession = null;
            return Task.FromResult(true);
        }

        var sketchRecorded = bridge.PhotoWorldKnowledgeEntries().Any(entry =>
            entry.SourceId == ReflectionInteractionId
            && entry.Images?.Any(image => image.AssetId == ReflectionSketchAssetId) == true);
        if (!sketchRecorded)
        {
            _reflectionRemarkPendingSession = null;
            return Task.FromResult(true);
        }

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var modalOpen = MainMenuUi.AnyUndismissed(GetTree())
            || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
            || player?.ModalOpen == true;
        if (player is null || !GodotObject.IsInstanceValid(player) || player.IsQueuedForDeletion() || modalOpen)
        {
            if (deferIfModal || ReferenceEquals(_reflectionRemarkPendingSession, session))
                _reflectionRemarkPendingSession = session;
            return Task.FromResult(true);
        }

        _reflectionRemarkPendingSession = null;
        _reflectionRemarkShownSession = session;
        var journalHint = InputBindingService.ActionHint("journal", player.CurrentInputDevice == "gamepad");
        player.ShowRemark("Айдар",
            $"Под отражёнными опорами продолжается старый подход. По воде идти нельзя — проверю берег у камышей. {journalHint} — открыть записную книжку.");
        return Task.FromResult(true);
    }
}
