using System;
using System.Collections.Generic;
using Godot;
using Urman.Core.Persistence;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private const string ForestGroupObservationInteractionId = "urman.fullgame:interaction/pw-forest-group";
    private const string ForestBeyondFrameInteractionId = "urman.fullgame:interaction/pw-forest-photographer";
    private const string ForestSceneId = "urman.fullgame:scene/pw-forest";
    private const string BankSceneId = "urman.fullgame:scene/pw-bank-approach";
    private const string BankCopySourceInteractionId = "urman.fullgame:interaction/pw-bank-copy-source";
    private const string BankCopyPermissionInteractionId = "urman.fullgame:interaction/pw-bank-copy-permission";
    private const string CouncilOfficeDoorInteractionId = "urman.chapter1:local/council/office";
    private static readonly Vector2[] ObjectiveDiamond = [new(0, -14), new(11, 0), new(0, 14), new(-11, 0)];
    private static readonly Vector2[] ObjectiveArrow = [new(0, -16), new(13, 13), new(0, 6), new(-13, 13)];
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
                _objectiveMarkerTarget = ResolveObjectiveMarkerTarget(mission, out _objectiveMarkerUsingAnchor);
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
            UrmanUiTheme.ApplyWorldText(_objectiveMarkerLabel!, settings, UrmanUiTheme.Size.WorldPrompt);
            var colours = UrmanUiTheme.Colours(settings);
            _objectiveMarkerLabel!.AddThemeFontOverride("font", UrmanUiTheme.BodyBoldFont);
            _objectiveMarkerLabel.AddThemeStyleboxOverride("normal", UrmanUiTheme.PlateBox(
                colours.Ink, colours.Focus, 1, marginX: UrmanUiTheme.Space.S, marginY: UrmanUiTheme.Space.Xs));
            _objectiveMarkerGlyph!.Color = colours.Focus;
            _objectiveMarkerOutline!.Color = colours.InkSunken;
        }

        var scale = Mathf.Clamp((float)settings.TextScale, .8f, 1.6f);
        var viewportSize = GetViewport().GetVisibleRect().Size;
        if (viewportSize.X < 160 || viewportSize.Y < 160)
        {
            HideObjectiveMarker();
            return;
        }
        var label = _objectiveMarkerLabel!;
        label.Visible = true;
        var name = string.IsNullOrWhiteSpace(mission.TargetLabel) ? mission.Title : mission.TargetLabel;
        var destination = target.GlobalPosition;
        var waypoint = TryGetRootPassageObjectiveWaypoint(mission, bridge, player.GlobalPosition,
            out var routePoint, out var routeLabel, out var inspectionTarget);
        if (!waypoint)
        {
            waypoint = TryGetPublicBuildingObjectiveWaypoint(mission, player.GlobalPosition, player.BodyRadius,
                out routePoint, out routeLabel);
            if (!waypoint && mission.InteractionId is BankCopySourceInteractionId or BankCopyPermissionInteractionId
                && bridge.ActiveSceneId == BankSceneId && bridge.CurrentZoneId == "village_day")
            {
                // Never fall back to a straight marker through the council walls
                // if an authored approach or office waypoint cannot be resolved.
                HideObjectiveMarker();
                return;
            }
            waypoint = waypoint
                || TryGetHomeYardExitObjectiveWaypoint(mission, player.GlobalPosition, out routePoint, out routeLabel)
                || TryGetSchoolObjectiveWaypoint(mission, player.GlobalPosition, out routePoint, out routeLabel)
                || TryGetHomeObjectiveWaypoint(mission, player.GlobalPosition, out routePoint, out routeLabel);
        }
        if (waypoint)
        {
            destination = routePoint;
            name = routeLabel;
        }
        var distance = player.GlobalPosition.DistanceTo(destination);
        var destinationText = $"{name} · {Mathf.CeilToInt(distance)} м";
        label.Text = destinationText;
        if (_routeCueDestination is { } destinationLabel)
        {
            if (destinationLabel.Text != destinationText) destinationLabel.Text = destinationText;
            destinationLabel.Visible = true;
        }
        var labelWidth = Mathf.Min(320f * scale, viewportSize.X - 48f);
        label.Size = new Vector2(labelWidth, 40f * scale);
        label.Position = new Vector2(-labelWidth / 2f, 22f * scale);

        // Observation subjects already contain their exact sight point; route anchors describe feet-level destinations.
        var worldPoint = destination + Vector3.Up * (inspectionTarget ? 0f
            : !waypoint && target is InteractionTarget ? .65f : 1.5f);
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
                var panel = _routeCuePanel!.GetGlobalRect();
                var below = panel.End.Y + 28f * scale;
                if (below <= bottom)
                    edge.Y = below;
                else
                {
                    // Large task text must not erase the only direction cue.
                    // Use the free right edge, shrinking the caption before dropping it.
                    var availableWidth = viewportSize.X - panel.End.X - 48f * scale;
                    if (availableWidth >= 96f * scale)
                    {
                        labelWidth = Mathf.Min(labelWidth, availableWidth);
                        label.Size = new Vector2(labelWidth, label.Size.Y);
                        label.Position = new Vector2(-labelWidth / 2f, label.Position.Y);
                        edge.X = viewportSize.X - labelWidth / 2f - 24f * scale;
                    }
                    else
                    {
                        label.Visible = false;
                        edge.X = viewportSize.X - 24f * scale;
                    }
                }
            }
            SetObjectiveMarkerGlyph(true, direction.Angle() + Mathf.Pi / 2f, scale);
            _objectiveMarker!.Position = edge;
        }
        _objectiveMarker!.Visible = true;
    }

    private bool TryGetRootPassageObjectiveWaypoint(PlayerMission mission, RuntimeBridge bridge, Vector3 playerPosition,
        out Vector3 waypoint, out string label, out bool inspectionTarget)
    {
        waypoint = default;
        label = string.Empty;
        inspectionTarget = false;
        if (mission.InteractionId == ForestGroupObservationInteractionId
            && bridge.ActiveSceneId == ForestSceneId
            && _main.GetNodeOrNull<W01Controller>("ZoneHost/W01") is { } cameraForest
            && GodotObject.IsInstanceValid(cameraForest) && cameraForest.IsInsideTree() && !cameraForest.IsQueuedForDeletion())
        {
            return cameraForest.TryGetForestCameraBackObjectiveTarget(
                bridge.PhotoWorldPresentationIdentity, playerPosition,
                out waypoint, out label, out inspectionTarget);
        }

        if (mission.InteractionId != ForestBeyondFrameInteractionId
            || bridge.ActiveSceneId != ForestSceneId
            || _main.GetNodeOrNull<W01Controller>("ZoneHost/W01") is not { } forest
            || !GodotObject.IsInstanceValid(forest) || !forest.IsInsideTree() || forest.IsQueuedForDeletion())
            return false;

        var lookBack = forest.TryGetRootPassageInspectionTarget(
            bridge.PhotoWorldPresentationIdentity, out var subject, out var seenCount);
        var anchor = forest.GetNodeOrNull<Node3D>(lookBack ? "Anchors/F13" : "Anchors/F12");
        if (!ObjectiveMarkerTargetAvailable(anchor, mission, endpoint: true)) return false;
        if (lookBack)
        {
            waypoint = subject;
            label = $"Осмотрите стволы: {seenCount}/3";
            inspectionTarget = true;
            return true;
        }
        label = "Проход у корней";
        if (forest.GetNodeOrNull<Node3D>("Anchors/F13") is { } returnAnchor
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

    private bool TryGetHomeYardExitObjectiveWaypoint(PlayerMission mission, Vector3 playerPosition,
        out Vector3 waypoint, out string label)
    {
        waypoint = default;
        label = "Калитка";
        if (_bridge?.CurrentZoneId != "village_day" || mission.TargetZoneId != "village_day"
            || mission.InteractionId is not ("urman.fullgame:interaction/pw-school-copy-source"
                or "urman.fullgame:interaction/pw-school-copy-permission"))
            return false;

        var position = new Vector2(playerPosition.X, playerPosition.Z);
        // Scope this detour to the Babai parcel and the short passage through
        // the same gate plane used by the house-entry route. The school goal
        // resumes after the player clears that gate approach.
        if (BabaiRelocation.OutsideNewYard(position.X, position.Y) > .001f)
            return false;

        var route = HomeObjectiveRoute.Value;
        var inward = (route[3] - route[2]).Normalized();
        var gateOffset = (position - route[2]).Dot(inward);
        if (gateOffset < 0f)
        {
            // Keep the exit cue until the capsule clears the gate post. The
            // existing outside approach is only 1.2 m past the gate plane, so
            // its short, narrow corridor cannot capture the street route.
            const float corridorHalfWidth = .45f;
            var outsideLeg = route[1] - route[2];
            var routeFraction = (position - route[2]).Dot(outsideLeg) / outsideLeg.LengthSquared();
            var fraction = Mathf.Clamp(routeFraction, 0f, 1f);
            var corridorPoint = route[2] + outsideLeg * fraction;
            if (position.DistanceSquaredTo(corridorPoint) > corridorHalfWidth * corridorHalfWidth
                || routeFraction >= 1f)
                return false;

            waypoint = new Vector3(route[1].X,
                AgentBAct1HeightField.CollisionGround(route[1].X, route[1].Y) + .05f,
                route[1].Y);
            label = "Дорожка за калиткой";
            return true;
        }

        var closestDistance = float.PositiveInfinity;
        var closestProgress = -1f;
        var closestPoint = route[4];
        var closestSegment = -1;
        // HomeObjectiveRoute runs from the street through the gate to the door.
        // Follow its last three segments in reverse, ending at the gate crossing.
        for (var index = route.Count - 2; index >= 2; index--)
        {
            var outwardLeg = route[index] - route[index + 1];
            var fraction = Mathf.Clamp((position - route[index + 1]).Dot(outwardLeg) / outwardLeg.LengthSquared(), 0f, 1f);
            var point = route[index + 1] + outwardLeg * fraction;
            var distance = position.DistanceSquaredTo(point);
            var progress = route.Count - 2 - index + fraction;
            if (distance < closestDistance - .0001f
                || (Mathf.Abs(distance - closestDistance) <= .0001f && progress >= closestProgress))
            {
                closestDistance = distance;
                closestProgress = progress;
                closestPoint = point;
                closestSegment = index;
            }
        }

        if (closestSegment < 2)
            return false;
        var next = closestSegment;
        // Advance a corner only after reaching it; never aim at the street-side
        // continuation of the line, which would cut across the closed fence.
        if (next > 2 && position.DistanceSquaredTo(route[next]) <= .15f * .15f)
            next--;

        var recovering = closestDistance > .5f * .5f;
        var pointOnRoute = recovering ? closestPoint : route[next];
        waypoint = new Vector3(pointOnRoute.X,
            AgentBAct1HeightField.CollisionGround(pointOnRoute.X, pointOnRoute.Y) + .05f,
            pointOnRoute.Y);
        label = recovering ? "Дорожка к калитке" : next <= 3 ? "Калитка" : "Проход во дворе";
        return true;
    }

    private bool TryGetSchoolObjectiveWaypoint(PlayerMission mission, Vector3 playerPosition,
        out Vector3 waypoint, out string label)
    {
        waypoint = default;
        var schoolCopyGoal = mission.InteractionId is
            "urman.fullgame:interaction/pw-school-copy-source" or "urman.fullgame:interaction/pw-school-copy-permission";
        label = schoolCopyGoal ? "Дверь учительской" : "Выход из школы";
        if (_bridge?.CurrentZoneId != "village_day" || (!schoolCopyGoal && mission.TargetZoneId != "village_day"))
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
        const float corridorZ = 4.05f, staffX = -7.8f, roomSideZ = 2.35f, schoolFront = 5.5f;
        var inside = Mathf.Abs(local.X) < 9.65f && Mathf.Abs(local.Z) < 5.15f
            && local.Y > -.2f && local.Y < 3.4f;
        var clearingEntrance = Mathf.Abs(local.X) <= .4f && local.Z >= 5.15f && local.Z < 6.3f
            && local.Y > -.2f && local.Y < 3.4f;
        if (!schoolCopyGoal && !inside && !clearingEntrance) return false;
        if (!schoolCopyGoal) label = clearingEntrance ? "Выход из школы" : "Коридор к выходу";
        Vector3 destination;
        if (!schoolCopyGoal && clearingEntrance)
            destination = new(0, .05f, 6.3f);
        else if (!inside)
        {
            label = "Вход в школу";
            var side = local.X < 0f ? -1f : 1f;
            if (local.Z < -5.15f && Mathf.Abs(local.X) < 10.45f)
                destination = new(side * 10.6f, .05f, -6.1f);
            else if (Mathf.Abs(local.X) >= 9.65f && local.Z < 6.15f)
                destination = new(side * 10.6f, .05f, 6.3f);
            else if (schoolCopyGoal && local.Z >= schoolFront && Mathf.Abs(local.X) > 1.25f
                && _main.ConnectedWorld?.AddressApproachPath("ACC-ADR-SQUARE-SCHOOL") is { Count: > 0 } schoolApproach)
            {
                // Align outside the dynamic stair flight before the marker resumes the centerline.
                waypoint = schoolApproach[0];
                return true;
            }
            else if (Mathf.Abs(local.X) <= .4f && local.Z >= 5.15f && local.Z <= 6.45f)
                destination = new(0, .05f, corridorZ);
            else if (local.Z < 6.15f)
                destination = new(local.X, .05f, 6.3f);
            else
                destination = new(0, .05f, 6.3f);
        }
        else if (local.Z < 2.82f)
        {
            if (schoolCopyGoal && local.X < -6.28f)
            {
                if (local.Z <= 2.4f) return false;
                destination = new(staffX, .05f, roomSideZ);
            }
            else
            {
                // Leave the current class/museum through its own opening, never through a partition.
                var doorX = local.X < -6.28f ? staffX : local.X < -.5f ? -2.5f : local.X < 5.2f ? 2.5f : 6.4f;
                destination = Mathf.Abs(local.X - doorX) > .1f
                    ? new(doorX, .05f, Mathf.Min(local.Z, roomSideZ))
                    : new(doorX, .05f, corridorZ);
            }
        }
        else if (schoolCopyGoal && Mathf.Abs(local.X - staffX) <= .1f)
            destination = new(staffX, .05f, roomSideZ);
        else if (local.Z < corridorZ)
        {
            // The bench's near edge is Z3.61; Z4.05 leaves .09 m beyond the
            // player's .35 m radius. Stay on this X until clear of the bench.
            label = schoolCopyGoal ? "Коридор к учительской" : "Коридор к выходу";
            destination = new(local.X, .05f, corridorZ);
        }
        else if (!schoolCopyGoal && Mathf.Abs(local.X) <= .1f)
            // Clear the doorway before resuming the distant street destination.
            destination = new(0, .05f, 6.3f);
        else if (schoolCopyGoal)
        {
            label = "Дверь учительской";
            destination = new(staffX, .05f, local.Z);
        }
        else
        {
            label = "Входная дверь";
            destination = new(0, .05f, local.Z);
        }

        waypoint = school.ToGlobal(destination);
        return true;
    }

    private bool TryGetPublicBuildingObjectiveWaypoint(PlayerMission mission, Vector3 playerPosition,
        float playerRadius, out Vector3 waypoint, out string label)
    {
        waypoint = default;
        label = string.Empty;
        if (mission.InteractionId is not (BankCopySourceInteractionId or BankCopyPermissionInteractionId)
            || _bridge is not { } bridge || bridge.ActiveSceneId != BankSceneId || bridge.CurrentZoneId != "village_day"
            || _objectiveMarkerTarget is not { } target || !IsCouncilBankNavigationTarget(target, mission)
            || _main.ConnectedWorld is not { } world)
            return false;

        Act1ConnectedWorld.PublicBuildingRoom? council = null;
        foreach (var room in world.PublicBuildingRooms)
        {
            if (room.Id != "council") continue;
            council = room;
            break;
        }
        if (council is null || !world.IsAncestorOf(council.Building)) return false;

        var playerPlanar = new Vector2(playerPosition.X, playerPosition.Z);
        var interior = world.PublicInteriorAt(playerPosition);
        if (interior != council.Id)
        {
            var approach = new Vector2(council.Outside.X, council.Outside.Z);
            var entrance = new Vector2(council.Entrance.X, council.Entrance.Z);
            var approachDistance = approach.DistanceTo(entrance);
            if (playerPlanar.DistanceTo(approach) > approachDistance)
            {
                waypoint = council.Outside;
                label = "Сельсовет";
            }
            else
            {
                waypoint = council.OuterDoor.GlobalPosition;
                label = "Вход в сельсовет";
            }
            return true;
        }

        if (council.SeniVolume.HasPoint(council.Metric.ToLocal(playerPosition)))
        {
            var vestibule = new Vector2(council.Vestibule.X, council.Vestibule.Z);
            var inside = new Vector2(council.Inside.X, council.Inside.Z);
            if (playerPlanar.DistanceTo(vestibule) > vestibule.DistanceTo(inside))
            {
                waypoint = council.Vestibule;
                label = "Входной проход";
            }
            else
            {
                waypoint = council.InnerDoor.GlobalPosition;
                label = "Дверь в зал";
            }
            return true;
        }

        if (_objectiveMarkerTarget is InteractionTarget { PresentationEnabled: false })
        {
            var sidePartition = council.Room.GetNodeOrNull<StaticBody3D>("OfficeSidePartitionBody");
            var sideShape = sidePartition?.GetNodeOrNull<CollisionShape3D>("Contact");
            StaticBody3D? frontPier = null;
            CollisionShape3D? frontShape = null;
            var doorFrame = council.Room.GetNodeOrNull<Node3D>("CouncilOfficeFrame");
            if (doorFrame is null) return false;
            var frameRoomX = council.Room.ToLocal(doorFrame.GlobalPosition).X;
            var openingLeft = float.NegativeInfinity;
            var openingRight = float.PositiveInfinity;
            var foundLeftPier = false;
            var foundRightPier = false;
            foreach (var child in council.Room.GetChildren())
            {
                if (child is StaticBody3D body
                    && body.Name.ToString().StartsWith("OfficeFrontPier", StringComparison.Ordinal))
                {
                    var contact = body.GetNodeOrNull<CollisionShape3D>("Contact");
                    if (contact?.Shape is not BoxShape3D pierBox) return false;
                    frontPier ??= body;
                    frontShape ??= contact;
                    var pierCenterX = council.Room.ToLocal(contact.GlobalPosition).X;
                    var pierHalfWidth = pierBox.Size.X * .5f;
                    var pierLeft = pierCenterX - pierHalfWidth;
                    var pierRight = pierCenterX + pierHalfWidth;
                    if (pierRight <= frameRoomX)
                    {
                        openingLeft = Mathf.Max(openingLeft, pierRight);
                        foundLeftPier = true;
                    }
                    else if (pierLeft >= frameRoomX)
                    {
                        openingRight = Mathf.Min(openingRight, pierLeft);
                        foundRightPier = true;
                    }
                }
            }
            var desk = council.Room.GetNodeOrNull<Node3D>("CouncilDesk");
            if (sidePartition is null || sideShape is null || sideShape.Shape is not BoxShape3D sideBox
                || frontPier is null || frontShape is null || frontShape.Shape is not BoxShape3D frontBox
                || !foundLeftPier || !foundRightPier || openingLeft >= frameRoomX || openingRight <= frameRoomX
                || desk is null)
                return false;

            var sidePlayer = sideShape.ToLocal(playerPosition);
            var sideDesk = sideShape.ToLocal(desk.GlobalPosition);
            var frontPlayer = frontShape.ToLocal(playerPosition);
            var frontDesk = frontShape.ToLocal(desk.GlobalPosition);
            if (Mathf.IsZeroApprox(sideDesk.X) || Mathf.IsZeroApprox(frontDesk.Z)) return false;
            var insideOffice = sidePlayer.X * Mathf.Sign(sideDesk.X) > sideBox.Size.X * .5f
                && frontPlayer.Z * Mathf.Sign(frontDesk.Z) > frontBox.Size.Z * .5f;
            if (!insideOffice)
            {
                var officeDoor = world.GetZoneInstance("village_day")?.GetNodeOrNull<InteractionTarget>("CouncilOfficeDoorUse");
                if (officeDoor is null || !GodotObject.IsInstanceValid(officeDoor) || !officeDoor.IsInsideTree()
                    || officeDoor.IsQueuedForDeletion() || officeDoor.InteractionId != CouncilOfficeDoorInteractionId
                    || !ObjectiveMarkerTargetVisible(officeDoor))
                    return false;

                var hallSideX = -Mathf.Sign(sideDesk.X);
                var hallSideZ = -Mathf.Sign(frontDesk.Z);
                var frontEndZ = sideShape.ToLocal(frontPier.GlobalPosition).Z;
                const float routeMargin = .05f;
                var frontWallClearance = frontBox.Size.Z * .5f + playerRadius + routeMargin;
                var behindPartitionEnd = (sidePlayer.Z - frontEndZ) * hallSideZ < frontWallClearance - .02f;
                var onHallSide = sidePlayer.X * hallSideX > sideBox.Size.X * .5f;
                if (onHallSide && behindPartitionEnd)
                {
                    var detour = sideShape.ToGlobal(new Vector3(
                        hallSideX * (sideBox.Size.X * .5f + playerRadius + routeMargin), 0,
                        frontEndZ + hallSideZ * frontWallClearance));
                    var roomPoint = council.Room.ToLocal(detour);
                    roomPoint.Y = council.Room.ToLocal(council.Inside).Y;
                    waypoint = council.Room.ToGlobal(roomPoint);
                    label = "Обойдите перегородку";
                    return true;
                }

                var frameInFrontWall = frontShape.ToLocal(doorFrame.GlobalPosition);
                var insideHeightInFrontWall = frontShape.ToLocal(council.Inside).Y;
                var doorwayApproach = frontShape.ToGlobal(new Vector3(frameInFrontWall.X, insideHeightInFrontWall,
                    hallSideZ * frontWallClearance));
                var safeStripLeft = openingLeft + playerRadius + routeMargin;
                var safeStripRight = openingRight - playerRadius - routeMargin;
                if (safeStripLeft >= safeStripRight) return false;
                var playerInRoom = council.Room.ToLocal(playerPosition);
                var inSafeDoorwayStrip = playerInRoom.X >= safeStripLeft && playerInRoom.X <= safeStripRight;
                var approachSideDistance = (frontPlayer.Z - hallSideZ * frontWallClearance) * hallSideZ;
                if (!inSafeDoorwayStrip || approachSideDistance > -.02f)
                {
                    waypoint = doorwayApproach;
                    label = "Подойдите к проёму";
                    return true;
                }

                waypoint = officeDoor.GlobalPosition;
                label = "Дверь в кабинет";
                return true;
            }
        }

        waypoint = target.GlobalPosition;
        label = string.IsNullOrWhiteSpace(mission.TargetLabel) ? "Схема на столе" : mission.TargetLabel;
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

        // If the player can already aim at the actual door, do not send them
        // back to an intermediate gate waypoint on the other side of the yard.
        if (_player is { } player && player.FocusedInteractionId == mission.InteractionId
            && _objectiveMarkerTarget is InteractionTarget doorTarget && doorTarget.IsAvailable())
            return false;

        var route = HomeObjectiveRoute.Value;
        var position = new Vector2(playerPosition.X, playerPosition.Z);
        var door = _objectiveMarkerTarget!.GlobalPosition;
        var fromDoor = playerPosition - door;
        // The gate's infinite plane also cuts through the porch. Once in the
        // narrow front-door approach, the door is the destination, not the gate behind us.
        if (fromDoor.X >= 0f && fromDoor.X <= 3f && Mathf.Abs(fromDoor.Z) <= 1f)
            return false;
        var inward = (route[3] - route[2]).Normalized();
        // Once across the gate plane, never send the player back to the street approach.
        var insideYard = (position - route[2]).Dot(inward) >= 0f;
        var firstSegment = insideYard ? 2 : 0;
        var lastSegment = insideYard ? route.Count - 2 : 1;
        var closestDistance = float.PositiveInfinity;
        var closestProgress = -1f;
        var closestPoint = route[firstSegment];
        var closestSegment = firstSegment;
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
                closestPoint = route[index] + leg * fraction;
                closestSegment = index;
                next = index + 1;
            }
        }

        // The inside node is only .85 m beyond the gate: .15 m leaves .70 m before the turn,
        // keeping the .35 m capsule clear instead of cutting the corner through the fence.
        if (position.DistanceSquaredTo(route[next]) <= .15f * .15f) next++;
        if (next >= route.Count || closestProgress >= route.Count - 1) return false;
        var point = route[next];
        var recovering = closestDistance > .5f * .5f;
        if (recovering)
        {
            // Recover onto the authored approach before aiming at its next corner through a fence.
            point = closestPoint;
            if (!insideYard && closestSegment == 1)
            {
                var approach = route[2] - route[1];
                // Leave room outside the gate for the .35 m capsule to clear the fence end.
                var lastRecoveryFraction = Mathf.Max(0f, 1f - .6f / approach.Dot(inward));
                point = route[1] + approach * Mathf.Min(closestProgress - 1f, lastRecoveryFraction);
            }
        }
        waypoint = new Vector3(point.X, AgentBAct1HeightField.CollisionGround(point.X, point.Y) + .05f, point.Y);
        label = recovering ? "Дорожка к дому" : next == 1 ? "Подход к дому"
            : next == 2 ? "Калитка" : next <= 4 ? "Проход во дворе" : "Вход в дом";
        return true;
    }

    private bool ObjectiveMarkerOverlapsMission(Vector2 position, float labelWidth, float scale) =>
        _routeCuePanel is { Visible: true } panel
        && panel.GetGlobalRect().Grow(12f * scale).Intersects(new Rect2(
            position - new Vector2(labelWidth / 2f, 16f * scale), new Vector2(labelWidth, 82f * scale)));

    internal bool TryGetVillageMapNavigation(RuntimeBridge bridge, out Vector2 position, out Vector2 forward,
        out Vector2? objective, out string? label)
    {
        position = forward = default;
        objective = null;
        label = null;
        if (!ReferenceEquals(_bridge, bridge) || !bridge.IsPhotoWorldsCampaign
            || bridge.SessionIdentity is null || bridge.IsPhotoWorldPresentationDeferred
            || bridge.ActiveSceneId != "urman.fullgame:scene/pw-home"
            || bridge.CurrentZoneId is not ("village_day" or "house_old_pc")
            || !GodotObject.IsInstanceValid(_main) || !_main.IsInsideTree()
            || _player is not { } player || !GodotObject.IsInstanceValid(player)
            || !player.IsInsideTree() || player.IsQueuedForDeletion() || player.SessionTransitionActive
            || player.GetNodeOrNull<Camera3D>("Head/Camera3D") is not { } camera)
            return false;

        position = new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z);
        var direction = -camera.GlobalBasis.Z;
        forward = new Vector2(direction.X, direction.Z).Normalized();
        if (JournalUi.CurrentPlayerMission(bridge) is not { } mission
            || (!string.IsNullOrWhiteSpace(mission.TargetZoneId) && mission.TargetZoneId != bridge.CurrentZoneId))
            return true;

        // A map marks the authored approach stance; the world cue may point at its facing witness.
        var anchor = string.IsNullOrWhiteSpace(mission.AnchorPath) ? null : _main.GetNodeOrNull<Node3D>(mission.AnchorPath);
        var target = ObjectiveMarkerTargetAvailable(anchor, mission, endpoint: true)
            ? anchor : ResolveObjectiveMarkerTarget(mission, out _);
        if (target is null) return true;
        objective = new Vector2(target.GlobalPosition.X, target.GlobalPosition.Z);
        label = string.IsNullOrWhiteSpace(mission.TargetLabel) ? mission.Title : mission.TargetLabel;
        return true;
    }

    private Node3D? ResolveObjectiveMarkerTarget(PlayerMission mission, out bool usingAnchor)
    {
        usingAnchor = false;
        if (!string.IsNullOrWhiteSpace(mission.InteractionId))
        {
            // The HUD caches this lookup; the map resolves once when its notebook projection refreshes.
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
        usingAnchor = true;
        return anchor;
    }

    private bool ObjectiveMarkerTargetAvailable(Node3D? target, PlayerMission mission, bool endpoint = false)
    {
        if (target is null || !GodotObject.IsInstanceValid(target) || target.IsQueuedForDeletion()
            || !target.IsInsideTree() || !ObjectiveMarkerTargetVisible(target) || !_main.IsAncestorOf(target)) return false;
        if (target is InteractionTarget { PresentationEnabled: false }
            && !IsCouncilBankNavigationTarget(target, mission)) return false;
        // The mission owns semantic unlocks; proximity/aim gates must not hide a known destination.
        return endpoint || string.IsNullOrWhiteSpace(mission.InteractionId)
            || target is InteractionTarget interaction && interaction.InteractionId == mission.InteractionId;
    }

    private bool IsCouncilBankNavigationTarget(Node3D target, PlayerMission mission)
    {
        if (target is not InteractionTarget interaction || !GodotObject.IsInstanceValid(interaction)
            || interaction.InteractionId != mission.InteractionId
            || mission.InteractionId is not (BankCopySourceInteractionId or BankCopyPermissionInteractionId)
            || !interaction.IsInsideTree() || interaction.IsQueuedForDeletion()
            || _bridge is not { } bridge || bridge.ActiveSceneId != BankSceneId || bridge.CurrentZoneId != "village_day"
            || !GodotObject.IsInstanceValid(_main) || !_main.IsInsideTree()
            || _main.ConnectedWorld is not { } world || !world.IsAncestorOf(interaction))
            return false;

        foreach (var room in world.PublicBuildingRooms)
            if (room.Id == "council" && room.Building.IsAncestorOf(interaction)) return true;
        return false;
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
        if (_routeCueDestination is not null) _routeCueDestination.Visible = false;
    }
}
