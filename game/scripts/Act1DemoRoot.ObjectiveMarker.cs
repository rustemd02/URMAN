using System;
using System.Collections.Generic;
using Godot;
using Urman.Core.Persistence;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private static readonly Vector2[] ObjectiveDiamond = [new(0, -10), new(8, 0), new(0, 10), new(-8, 0)];
    private static readonly Vector2[] ObjectiveArrow = [new(0, -12), new(10, 10), new(0, 5), new(-10, 10)];
    private static readonly Lazy<IReadOnlyList<Vector2>> HomeObjectiveRoute = new(() => Array.AsReadOnly(
        new[]
        {
            AgentBAct1Layout.WalkChain[2], AgentBAct1Layout.WalkChain[3], AgentBAct1Layout.WalkChain[4],
            AgentBAct1Layout.WalkChain[5], AgentBAct1Layout.WalkChain[6], AgentBAct1Layout.WalkChain[7]
        }));
    private Control? _objectiveMarker;
    private Polygon2D? _objectiveMarkerGlyph;
    private Polygon2D? _objectiveMarkerOutline;
    private Label? _objectiveMarkerLabel;
    private Node3D? _objectiveMarkerTarget;
    private Node3D? _objectiveMarkerSchool;
    private PlayerMission? _objectiveMarkerMission;
    private object? _objectiveMarkerSession;
    private string? _objectiveMarkerScene;
    private string? _objectiveMarkerZone;
    private double _objectiveMarkerRetry;
    private bool _objectiveMarkerUsingAnchor;
    private AccessibilitySettingsSnapshot? _objectiveMarkerSettings;

    private void UpdateObjectiveMarker(double delta)
    {
        var bridge = _bridge;
        var player = _player;
        if (_mission is not { } mission || bridge is null || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session
            || player is null || !GodotObject.IsInstanceValid(player) || !player.IsInsideTree()
            || !GodotObject.IsInstanceValid(_main) || !_main.IsInsideTree()
            || RouteCueBlockedByModal() || MainMenuVisible || IntroVisible || _endingShown
            || _introReplay || _photoWorldReplay || player.SessionTransitionActive || player.VehicleControlled
            || GetTree().Paused)
        {
            HideObjectiveMarker();
            return;
        }

        if (!Equals(_objectiveMarkerMission, mission) || !ReferenceEquals(_objectiveMarkerSession, session)
            || _objectiveMarkerScene != bridge.ActiveSceneId || _objectiveMarkerZone != bridge.CurrentZoneId)
        {
            _objectiveMarkerMission = mission;
            _objectiveMarkerSession = session;
            _objectiveMarkerScene = bridge.ActiveSceneId;
            _objectiveMarkerZone = bridge.CurrentZoneId;
            _objectiveMarkerTarget = null;
            _objectiveMarkerUsingAnchor = false;
            _objectiveMarkerRetry = 0;
        }

        var camera = GetViewport().GetCamera3D();
        if (camera is null || camera != player.GetNodeOrNull<Camera3D>("Head/Camera3D")
            || (!string.IsNullOrWhiteSpace(mission.TargetZoneId) && mission.TargetZoneId != bridge.CurrentZoneId))
        {
            HideObjectiveMarker();
            return;
        }

        var targetCurrent = ObjectiveMarkerTargetAvailable(_objectiveMarkerTarget, mission, _objectiveMarkerUsingAnchor);
        if (!targetCurrent || (_objectiveMarkerUsingAnchor && !string.IsNullOrWhiteSpace(mission.InteractionId)))
        {
            if (!targetCurrent) _objectiveMarkerTarget = null;
            _objectiveMarkerRetry -= delta;
            if (_objectiveMarkerRetry <= 0)
            {
                _objectiveMarkerRetry = .75;
                _objectiveMarkerTarget = ResolveObjectiveMarkerTarget(mission);
            }
        }
        if (_objectiveMarkerTarget is not { } target)
        {
            HideObjectiveMarker();
            return;
        }

        EnsureObjectiveMarker();
        var settings = player.Accessibility;
        if (!Equals(_objectiveMarkerSettings, settings))
        {
            _objectiveMarkerSettings = settings;
            _objectiveMarker!.Theme = UrmanUiTheme.For(settings);
            UrmanUiTheme.ApplyWorldText(_objectiveMarkerLabel!, settings, UrmanUiTheme.Size.Body);
            _objectiveMarkerGlyph!.Color = UrmanUiTheme.Colours(settings).Focus;
            _objectiveMarkerOutline!.Color = UrmanUiTheme.Colours(settings).InkSunken;
        }

        var scale = Mathf.Clamp((float)settings.TextScale, .8f, 1.6f);
        var viewportSize = GetViewport().GetVisibleRect().Size;
        if (viewportSize.X < 160 || viewportSize.Y < 160)
        {
            HideObjectiveMarker();
            return;
        }
        var label = _objectiveMarkerLabel!;
        var name = string.IsNullOrWhiteSpace(mission.TargetLabel) ? mission.Title : mission.TargetLabel;
        var destination = target.GlobalPosition;
        var waypoint = TryGetRootPassageObjectiveWaypoint(mission, bridge, player.GlobalPosition, out var routePoint, out var routeLabel)
            || TryGetSchoolObjectiveWaypoint(mission, player.GlobalPosition, out routePoint, out routeLabel)
            || TryGetHomeObjectiveWaypoint(mission, player.GlobalPosition, out routePoint, out routeLabel);
        if (waypoint)
        {
            destination = routePoint;
            name = routeLabel;
        }
        var distance = player.GlobalPosition.DistanceTo(destination);
        label.Text = $"{name}\n{Mathf.RoundToInt(distance)} м";
        var labelWidth = Mathf.Min(320f * scale, viewportSize.X - 48f);
        label.Size = new Vector2(labelWidth, 48f * scale);
        label.Position = new Vector2(-labelWidth / 2f, 18f * scale);

        // Anchors describe feet-level destinations; interaction bodies already own their centre.
        var worldPoint = destination + Vector3.Up * (!waypoint && target is InteractionTarget ? .65f : 1.5f);
        var screenPoint = camera.UnprojectPosition(worldPoint);
        var centre = viewportSize / 2f;
        var inset = new Vector2(labelWidth / 2f + 24f, 64f * scale);
        var limits = new Vector2(Mathf.Max(1f, centre.X - inset.X), Mathf.Max(1f, centre.Y - inset.Y));
        // Leave room below the glyph for the label at the bottom edge.
        var bottom = viewportSize.Y - 96f * scale;
        var behind = camera.IsPositionBehind(worldPoint);
        var onScreen = !behind && screenPoint.X >= inset.X && screenPoint.X <= viewportSize.X - inset.X
            && screenPoint.Y >= inset.Y && screenPoint.Y <= bottom
            && !ObjectiveMarkerOverlapsMission(screenPoint, labelWidth, scale);
        if (onScreen)
        {
            SetObjectiveMarkerGlyph(false, 0f, scale);
            _objectiveMarker!.Position = screenPoint;
        }
        else
        {
            // Camera-local X/Y retain the correct turn direction behind the player.
            var local = camera.GlobalTransform.AffineInverse() * worldPoint;
            var direction = behind || !float.IsFinite(screenPoint.X) || !float.IsFinite(screenPoint.Y)
                ? new Vector2(local.X, -local.Y) : screenPoint - centre;
            if (direction.LengthSquared() < .001f) direction = Vector2.Down;
            direction = direction.Normalized();
            var factor = Mathf.Min(limits.X / Mathf.Max(Mathf.Abs(direction.X), .001f),
                limits.Y / Mathf.Max(Mathf.Abs(direction.Y), .001f));
            var edge = centre + direction * factor;
            edge.Y = Mathf.Clamp(edge.Y, inset.Y, bottom);
            if (ObjectiveMarkerOverlapsMission(edge, labelWidth, scale))
            {
                edge.Y = _routeCuePanel!.GetGlobalRect().End.Y + 28f * scale;
                if (edge.Y > bottom)
                {
                    HideObjectiveMarker();
                    return;
                }
            }
            SetObjectiveMarkerGlyph(true, direction.Angle() + Mathf.Pi / 2f, scale);
            _objectiveMarker!.Position = edge;
        }
        _objectiveMarker!.Visible = true;
    }

    private bool TryGetRootPassageObjectiveWaypoint(PlayerMission mission, RuntimeBridge bridge, Vector3 playerPosition,
        out Vector3 waypoint, out string label)
    {
        waypoint = default;
        label = string.Empty;
        if (mission.InteractionId != "urman.fullgame:interaction/pw-forest-photographer"
            || bridge.ActiveSceneId != "urman.fullgame:scene/pw-forest"
            || _main.GetNodeOrNull<W01Controller>("ZoneHost/W01") is not { } forest
            || !GodotObject.IsInstanceValid(forest) || !forest.IsInsideTree() || forest.IsQueuedForDeletion())
            return false;

        var lookBack = forest.HasPendingRootPassageInspection(bridge.PhotoWorldPresentationIdentity);
        var anchor = forest.GetNodeOrNull<Node3D>(lookBack ? "Anchors/F13" : "Anchors/F12");
        if (!ObjectiveMarkerTargetAvailable(anchor, mission, endpoint: true)) return false;
        label = lookBack ? "Три ствола за поворотом" : "Проход у корней";
        if (!lookBack && forest.GetNodeOrNull<Node3D>("Anchors/F13") is { } returnAnchor
            && ObjectiveMarkerTargetAvailable(returnAnchor, mission, endpoint: true))
        {
            var position = new Vector2(playerPosition.X, playerPosition.Z);
            var entry = new Vector2(anchor!.GlobalPosition.X, anchor.GlobalPosition.Z);
            var exit = new Vector2(returnAnchor.GlobalPosition.X, returnAnchor.GlobalPosition.Z);
            // Near the far end after a reset, point back through it; distance never arms inspection.
            if (position.DistanceSquaredTo(exit) < position.DistanceSquaredTo(entry))
            {
                anchor = returnAnchor;
                label = "Вернитесь через проход";
            }
        }
        waypoint = anchor!.GlobalPosition;
        return true;
    }

    private bool TryGetSchoolObjectiveWaypoint(PlayerMission mission, Vector3 playerPosition,
        out Vector3 waypoint, out string label)
    {
        waypoint = default;
        label = "Учительская";
        if (_bridge?.CurrentZoneId != "village_day" || mission.InteractionId is not
            ("urman.fullgame:interaction/pw-school-copy-source" or "urman.fullgame:interaction/pw-school-copy-permission"))
            return false;

        if (_objectiveMarkerSchool is null || !GodotObject.IsInstanceValid(_objectiveMarkerSchool)
            || !_objectiveMarkerSchool.IsInsideTree() || _objectiveMarkerSchool.IsQueuedForDeletion()
            || !_main.IsAncestorOf(_objectiveMarkerSchool))
            _objectiveMarkerSchool = _main.ConnectedWorld?.GetNodeOrNull<Node3D>(
                "Act1CoreWorldGreybox/SovkhozSquare/OldSchool");
        var school = _objectiveMarkerSchool;
        if (!ObjectiveMarkerTargetAvailable(school, mission, endpoint: true)) return false;
        var local = school!.ToLocal(playerPosition);
        // SquareInteriors.School owns the 20 x 11 shell, corridor wall Z2.9 and these four doors.
        const float corridorZ = 4.2f, staffX = -7.8f, roomSideZ = 2.35f;
        var inside = Mathf.Abs(local.X) < 9.65f && Mathf.Abs(local.Z) < 5.15f
            && local.Y > -.2f && local.Y < 3.4f;
        Vector3 destination;
        if (!inside)
        {
            label = "Вход в школу";
            var side = local.X < 0f ? -1f : 1f;
            if (local.Z < -5.15f && Mathf.Abs(local.X) < 10.45f)
                destination = new(side * 10.6f, .05f, -6.1f);
            else if (Mathf.Abs(local.X) >= 9.65f && local.Z < 6.15f)
                destination = new(side * 10.6f, .05f, 6.3f);
            else if (Mathf.Abs(local.X) <= .4f && local.Z >= 5.15f && local.Z <= 6.45f)
                destination = new(0, .05f, corridorZ);
            else if (local.Z < 6.15f)
                destination = new(local.X, .05f, 6.3f);
            else
                destination = new(0, .05f, 6.3f);
        }
        else if (local.Z < 2.82f)
        {
            if (local.X < -6.28f)
            {
                if (local.Z <= 2.4f) return false;
                destination = new(staffX, .05f, roomSideZ);
            }
            else
            {
                // Leave the current class/museum through its own opening, never through a partition.
                var doorX = local.X < -.5f ? -2.5f : local.X < 5.2f ? 2.5f : 6.4f;
                destination = Mathf.Abs(local.X - doorX) > .1f
                    ? new(doorX, .05f, Mathf.Min(local.Z, roomSideZ))
                    : new(doorX, .05f, corridorZ);
            }
        }
        else if (Mathf.Abs(local.X - staffX) <= .1f)
            destination = new(staffX, .05f, roomSideZ);
        else if (Mathf.Abs(local.Z - corridorZ) > .12f)
            // Align in the hall before walking along it; the cloakroom bench ends at Z3.61.
            destination = new(local.X, .05f, corridorZ);
        else
            destination = new(staffX, .05f, corridorZ);

        waypoint = school.ToGlobal(destination);
        return true;
    }

    private bool TryGetHomeObjectiveWaypoint(PlayerMission mission, Vector3 playerPosition,
        out Vector3 waypoint, out string label)
    {
        waypoint = default;
        label = "Вход в дом";
        if (_bridge?.CurrentZoneId != "village_day" || mission.InteractionId is not
            ("urman.fullgame:interaction/pw-home-enter-house" or "urman.chapter1:interaction/arrival-enter-house"))
            return false;

        var route = HomeObjectiveRoute.Value;
        var position = new Vector2(playerPosition.X, playerPosition.Z);
        var inward = (route[3] - route[2]).Normalized();
        // Once across the gate plane, never send the player back to the street approach.
        var insideYard = (position - route[2]).Dot(inward) >= 0f;
        var firstSegment = insideYard ? 2 : 0;
        var lastSegment = insideYard ? route.Count - 2 : 1;
        var closestDistance = float.PositiveInfinity;
        var closestProgress = -1f;
        var next = firstSegment + 1;
        for (var index = firstSegment; index <= lastSegment; index++)
        {
            var leg = route[index + 1] - route[index];
            var fraction = Mathf.Clamp((position - route[index]).Dot(leg) / leg.LengthSquared(), 0f, 1f);
            var distance = position.DistanceSquaredTo(route[index] + leg * fraction);
            var progress = index + fraction;
            if (distance < closestDistance - .0001f
                || (Mathf.Abs(distance - closestDistance) <= .0001f && progress >= closestProgress))
            {
                closestDistance = distance;
                closestProgress = progress;
                next = index + 1;
            }
        }

        // The inside node is only .85 m beyond the gate: .15 m leaves .70 m before the turn,
        // keeping the .35 m capsule clear instead of cutting the corner through the fence.
        if (position.DistanceSquaredTo(route[next]) <= .15f * .15f) next++;
        if (next >= route.Count || closestProgress >= route.Count - 1) return false;
        var point = route[next];
        waypoint = new Vector3(point.X, AgentBAct1HeightField.CollisionGround(point.X, point.Y) + .05f, point.Y);
        label = next <= 2 ? "Калитка" : next <= 4 ? "Проход во дворе" : "Вход в дом";
        return true;
    }

    private bool ObjectiveMarkerOverlapsMission(Vector2 position, float labelWidth, float scale) =>
        _routeCuePanel is { Visible: true } panel
        && panel.GetGlobalRect().Grow(12f * scale).Intersects(new Rect2(
            position - new Vector2(labelWidth / 2f, 16f * scale), new Vector2(labelWidth, 82f * scale)));

    private Node3D? ResolveObjectiveMarkerTarget(PlayerMission mission)
    {
        _objectiveMarkerUsingAnchor = false;
        if (!string.IsNullOrWhiteSpace(mission.InteractionId))
        {
            // No public interaction index exists. Search only while unresolved, at most once per .75 s.
            var pending = new Stack<Node>();
            pending.Push(_main);
            while (pending.TryPop(out var node))
            {
                if (node.IsQueuedForDeletion() || (node is Node3D { Visible: false } spatial
                    && !IsActiveSuppressedObjectiveZone(spatial))) continue;
                if (node is InteractionTarget target && ObjectiveMarkerTargetAvailable(target, mission)) return target;
                foreach (var child in node.GetChildren()) pending.Push(child);
            }
        }
        if (string.IsNullOrWhiteSpace(mission.AnchorPath)) return null;
        var anchor = _main.GetNodeOrNull<Node3D>(mission.AnchorPath);
        if (!ObjectiveMarkerTargetAvailable(anchor, mission, endpoint: true)) return null;
        _objectiveMarkerUsingAnchor = true;
        return anchor;
    }

    private bool ObjectiveMarkerTargetAvailable(Node3D? target, PlayerMission mission, bool endpoint = false)
    {
        if (target is null || !GodotObject.IsInstanceValid(target) || target.IsQueuedForDeletion()
            || !target.IsInsideTree() || !ObjectiveMarkerTargetVisible(target) || !_main.IsAncestorOf(target)) return false;
        if (target is InteractionTarget { PresentationEnabled: false }) return false;
        // The mission owns semantic unlocks; proximity/aim gates must not hide a known destination.
        return endpoint || string.IsNullOrWhiteSpace(mission.InteractionId)
            || target is InteractionTarget interaction && interaction.InteractionId == mission.InteractionId;
    }

    private static bool ObjectiveMarkerTargetVisible(Node3D target)
    {
        if (!target.Visible) return false;
        for (var ancestor = target.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (ancestor.IsQueuedForDeletion()
                || (ancestor is Node3D { Visible: false } spatial && !IsActiveSuppressedObjectiveZone(spatial)))
                return false;
        }
        return true;
    }

    // The active legacy street hides its old visuals while retaining the real interaction owners.
    private static bool IsActiveSuppressedObjectiveZone(Node3D node) => node is StyleBenchmarkZone
        && node.GetMeta("legacyPresentationSuppressed", false).AsBool()
        && node.GetMeta("activePresentation", false).AsBool();

    private void EnsureObjectiveMarker()
    {
        if (_objectiveMarker is not null && GodotObject.IsInstanceValid(_objectiveMarker)) return;
        var layer = GetNodeOrNull<CanvasLayer>("Act1DemoRouteCue");
        if (layer is null)
        {
            layer = new CanvasLayer { Name = "Act1ObjectiveMarker", Layer = 84 };
            AddChild(layer);
        }
        _objectiveMarker = new Control { Name = "ObjectiveMarker", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        layer.AddChild(_objectiveMarker);
        _objectiveMarkerOutline = new Polygon2D { Name = "Outline" };
        _objectiveMarkerGlyph = new Polygon2D { Name = "Destination" };
        _objectiveMarker.AddChild(_objectiveMarkerOutline);
        _objectiveMarker.AddChild(_objectiveMarkerGlyph);
        _objectiveMarkerLabel = new Label
        {
            Name = "DestinationLabel", HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _objectiveMarker.AddChild(_objectiveMarkerLabel);
        _objectiveMarkerSettings = null;
    }

    private void SetObjectiveMarkerGlyph(bool arrow, float rotation, float scale)
    {
        var points = arrow ? ObjectiveArrow : ObjectiveDiamond;
        _objectiveMarkerGlyph!.Polygon = points;
        _objectiveMarkerOutline!.Polygon = points;
        _objectiveMarkerGlyph.Rotation = _objectiveMarkerOutline.Rotation = rotation;
        _objectiveMarkerGlyph.Scale = Vector2.One * scale;
        _objectiveMarkerOutline.Scale = Vector2.One * scale * 1.4f;
    }

    private void HideObjectiveMarker()
    {
        if (_objectiveMarker is not null && GodotObject.IsInstanceValid(_objectiveMarker)) _objectiveMarker.Visible = false;
    }
}
