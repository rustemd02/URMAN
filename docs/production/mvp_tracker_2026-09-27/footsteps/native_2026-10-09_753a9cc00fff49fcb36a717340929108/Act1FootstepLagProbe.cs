using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// ACT1-FOOTSTEP.1 data-only profile: walks from the arrival road into the
/// exterior snow and back using ordinary movement input. Emits compact,
/// contiguous road/off-road timing, playback, GC and snow-stamp observations.
/// </summary>
public partial class Act1FootstepLagProbe : Node
{
    private const double OffroadOutboundMeters = 4.0;
    private const double RoadReturnMeters = 2.2;
    private const double MaximumCaptureSeconds = 75.0;
    private const double HitchThresholdMs = 33.3;
    private const double MovementTraceThresholdMs = 500.0;
    private const int FrameCapacity = 24_000;

    private enum RoutePhase { RoadOutbound, OffroadOutbound, TurningForReturn, OffroadReturn, RoadReturn, Complete }

    private readonly record struct FrameSample(
        int Frame, int DrawnFrame, int DrawDelta, double ElapsedMs, double FrameMs,
        double ProcessMonitorMs, double PhysicsMonitorMs,
        int Gen0Delta, int Gen1Delta, int Gen2Delta, long AllocatedBytesDelta,
        RoutePhase Phase, int SegmentId, bool PackedBand, float X, float Z, float VelocitySpeed,
        float DisplacementX, float DisplacementZ, double FrameDisplacementMeters, double CumulativePathMeters,
        bool OnFloor, bool ModalOpen, bool ClimbingLadder, bool VehicleControlled,
        int PresentationTransformRevision, bool PresentationTransformRevisionChanged,
        double TeleportThresholdMeters, bool TeleportThresholdExceeded, string Surface,
        int TriggerDelta, int StampDelta, double? SnowUpdateMs, double? SnowRedrawMs, double? SnowMeshMs);

    private readonly record struct StepEvent(
        int FrameIndex, int StampDelta, double? SnowUpdateMs, double? SnowRedrawMs, double? SnowMeshMs,
        int TriggerCount, int TriggerDelta, double ElapsedMs, RoutePhase Phase, int SegmentId,
        bool PackedBand, string Surface, float X, float Z, float Speed,
        double? GapFromPreviousMs, bool SameSegmentAsPrevious,
        bool PlaybackPlaying, bool HasStreamPlayback, double PlaybackPosition,
        double CumulativePathMeters, object MovementSincePreviousStep);

    private readonly record struct SurfaceChange(
        double ElapsedMs, RoutePhase Phase, int SegmentId, string From, string To, float X, float Z);

    private readonly record struct RouteTransition(
        double ElapsedMs, string Phase, float X, float Z, double RoadDistance, double RoadHalfWidth);

    private readonly List<FrameSample> _frames = new(FrameCapacity);
    private readonly List<StepEvent> _stepEvents = new(256);
    private readonly List<SurfaceChange> _surfaceChanges = new(16);
    private readonly List<RouteTransition> _routeTransitions = new(8);

    private Main? _main;
    private FirstPersonController? _player;
    private FootstepAudioController? _controller;
    private SnowTrampleField? _field;
    private AudioStreamPlayer? _stepPlayer;
    private RoutePhase _routePhase = RoutePhase.RoadOutbound;
    private Vector2 _offroadStart;
    private Vector2 _roadReturnStart;
    private ulong _captureStartUsec;
    private ulong _lastFrameUsec;
    private Vector2 _captureStartPosition;
    private Vector2 _lastSamplePosition;
    private bool _hasLastSamplePosition;
    private double _cumulativePathMeters;
    private int _lastPresentationTransformRevision;
    private bool _hasLastPresentationTransformRevision;
    private ulong _nextYawTurnUsec;
    private ulong _roadSteerUntilUsec;
    private ulong _offroadTurnUntilUsec;
    private int _lastDrawnFrame;
    private int _lastTriggerCount;
    private int _lastStampCount;
    private int _lastGen0;
    private int _lastGen1;
    private int _lastGen2;
    private long _lastAllocatedBytes;
    private string _lastSurface = string.Empty;
    private RoutePhase _lastSegmentPhase;
    private bool _lastSegmentPackedBand;
    private bool _hasSegment;
    private bool _roadSteering;
    private bool _recording;
    private bool _completionStarted;
    private int _segmentId;

    public override async void _Ready()
    {
        // Main and its player/audio nodes use the default priority. Sampling at
        // a later priority observes their completed callbacks in the same frame.
        ProcessPriority = 100;

        _main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (_main is null)
        {
            Fail("Footstep lag profile could not load main.");
            return;
        }

        _main.InitialZoneId = "village_day";
        _main.InitialSpawnPointId = "arrival";
        _main.EnableAct1ConnectedWorld = true;
        AddChild(_main);
        for (var index = 0; index < 10; index++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        _controller = GetTree().GetFirstNodeInGroup("footstep_audio") as FootstepAudioController;
        _field = Descendants(_main).OfType<SnowTrampleField>().FirstOrDefault();
        _stepPlayer = _controller?.GetNodeOrNull<AudioStreamPlayer>("FootstepPlayer");
        if (_player is null || _controller is null || _field is null || _stepPlayer is null)
        {
            Fail("Footstep profile could not find player, footstep player or SnowTrampleField.");
            return;
        }

        _player.SetPhysicsProcess(true);
        BeginCapture();
        Input.ActionPress("move_forward");
    }

    public override void _Process(double delta)
    {
        if (!_recording || _player is null || _controller is null || _field is null || _stepPlayer is null)
            return;

        var now = Time.GetTicksUsec();
        var elapsedUsec = now - _captureStartUsec;
        var elapsedMs = elapsedUsec / 1000.0;
        var frameMs = now >= _lastFrameUsec ? (now - _lastFrameUsec) / 1000.0 : delta * 1000.0;
        _lastFrameUsec = now;

        var position = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
        var displacement = _hasLastSamplePosition ? position - _lastSamplePosition : Vector2.Zero;
        var frameDisplacementMeters = displacement.Length();
        var teleportThresholdMeters = Mathf.Max(.5f, _player.Velocity.Length() * (float)delta * 2f + .1f);
        var teleportThresholdExceeded = _hasLastSamplePosition && frameDisplacementMeters > teleportThresholdMeters;
        _cumulativePathMeters += frameDisplacementMeters;
        _lastSamplePosition = position;
        _hasLastSamplePosition = true;

        var onFloor = _player.IsOnFloor();
        var modalOpen = _player.ModalOpen;
        var climbingLadder = _player.IsClimbingLadder;
        var vehicleControlled = _player.VehicleControlled;
        var presentationTransformRevision = _player.PresentationTransformRevision;
        var presentationTransformRevisionChanged = _hasLastPresentationTransformRevision
            && presentationTransformRevision != _lastPresentationTransformRevision;
        _lastPresentationTransformRevision = presentationTransformRevision;
        _hasLastPresentationTransformRevision = true;

        var road = AgentBAct1HeightField.RoadInfo(position.X, position.Y);
        var packedBand = road.Distance < road.HalfWidth + .6f;
        EnsureSegment(_routePhase, packedBand);

        var drawnFrame = Engine.GetFramesDrawn();
        var drawDelta = drawnFrame >= _lastDrawnFrame ? drawnFrame - _lastDrawnFrame : 0;
        _lastDrawnFrame = drawnFrame;

        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var allocatedBytes = GC.GetTotalAllocatedBytes(false);
        var gen0Delta = Math.Max(0, gen0 - _lastGen0);
        var gen1Delta = Math.Max(0, gen1 - _lastGen1);
        var gen2Delta = Math.Max(0, gen2 - _lastGen2);
        var allocatedBytesDelta = Math.Max(0, allocatedBytes - _lastAllocatedBytes);
        _lastGen0 = gen0; _lastGen1 = gen1; _lastGen2 = gen2; _lastAllocatedBytes = allocatedBytes;

        var surface = _controller.LastSurface;
        var speed = new Vector2(_player.Velocity.X, _player.Velocity.Z).Length();
        var stampCount = _field.GetMeta("snowTrampleStampCount", 0).AsInt32();
        var stampDelta = stampCount >= _lastStampCount ? stampCount - _lastStampCount : 0;
        _lastStampCount = stampCount;
        // Snow timings are refreshed only when a stamp is accepted. Null means
        // no new timing sample this frame, rather than a repeated stale value.
        double? snowUpdateMs = stampDelta > 0 ? _field.GetMeta("snowTrampleUpdateMs", 0f).AsDouble() : null;
        double? snowRedrawMs = stampDelta > 0 ? _field.GetMeta("snowTrampleRedrawMs", 0f).AsDouble() : null;
        double? snowMeshMs = stampDelta > 0 ? _field.GetMeta("snowTrampleMeshMs", 0f).AsDouble() : null;

        var triggerCount = _controller.StepTriggerCount;
        var triggerDelta = triggerCount >= _lastTriggerCount ? triggerCount - _lastTriggerCount : 0;
        _lastTriggerCount = triggerCount;
        var hasStreamPlayback = _stepPlayer.HasStreamPlayback();
        var playbackPosition = hasStreamPlayback ? _stepPlayer.GetPlaybackPosition() : 0.0;
        var frame = new FrameSample(_frames.Count, drawnFrame, drawDelta, elapsedMs, frameMs,
            Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0,
            Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0,
            gen0Delta, gen1Delta, gen2Delta, allocatedBytesDelta,
            _routePhase, _segmentId, packedBand, position.X, position.Y, speed,
            displacement.X, displacement.Y, frameDisplacementMeters, _cumulativePathMeters,
            onFloor, modalOpen, climbingLadder, vehicleControlled,
            presentationTransformRevision, presentationTransformRevisionChanged,
            teleportThresholdMeters, teleportThresholdExceeded, surface,
            triggerDelta, stampDelta, snowUpdateMs, snowRedrawMs, snowMeshMs);
        _frames.Add(frame);

        if (!string.Equals(surface, _lastSurface, StringComparison.Ordinal))
        {
            _surfaceChanges.Add(new SurfaceChange(elapsedMs, _routePhase, _segmentId,
                _lastSurface, surface, position.X, position.Y));
            _lastSurface = surface;
        }

        if (triggerDelta > 0)
        {
            StepEvent? previousStep = _stepEvents.Count > 0 ? _stepEvents[^1] : null;
            var gapMs = previousStep is { } previous
                ? elapsedMs - previous.ElapsedMs
                : (double?)null;
            var sameSegment = previousStep is { } prior && prior.SegmentId == _segmentId;
            _stepEvents.Add(new StepEvent(frame.Frame, stampDelta, snowUpdateMs, snowRedrawMs, snowMeshMs,
                triggerCount, triggerDelta, elapsedMs, _routePhase,
                _segmentId, packedBand, surface, position.X, position.Y, speed, gapMs, sameSegment,
                _stepPlayer.Playing, hasStreamPlayback, playbackPosition, _cumulativePathMeters,
                BuildMovementInterval(previousStep, frame)));
        }

        AdvanceRoute(now, elapsedMs, position, road, packedBand);
        if (_routePhase == RoutePhase.Complete)
        {
            _ = FinishCapture();
        }
        else if (elapsedUsec >= (ulong)(MaximumCaptureSeconds * 1_000_000.0))
        {
            _ = FinishCapture();
        }
    }

    private void BeginCapture()
    {
        _captureStartUsec = Time.GetTicksUsec();
        _lastFrameUsec = _captureStartUsec;
        _lastDrawnFrame = Engine.GetFramesDrawn();
        _lastTriggerCount = _controller!.StepTriggerCount;
        _lastStampCount = _field!.GetMeta("snowTrampleStampCount", 0).AsInt32();
        _lastGen0 = GC.CollectionCount(0);
        _lastGen1 = GC.CollectionCount(1);
        _lastGen2 = GC.CollectionCount(2);
        _lastAllocatedBytes = GC.GetTotalAllocatedBytes(false);
        _lastSurface = _controller.LastSurface;
        _captureStartPosition = new Vector2(_player!.GlobalPosition.X, _player.GlobalPosition.Z);
        _lastSamplePosition = _captureStartPosition;
        _hasLastSamplePosition = true;
        _cumulativePathMeters = 0.0;
        _lastPresentationTransformRevision = _player.PresentationTransformRevision;
        _hasLastPresentationTransformRevision = true;
        _nextYawTurnUsec = _captureStartUsec + 2_500_000;
        _routePhase = RoutePhase.RoadOutbound;
        var startRoad = AgentBAct1HeightField.RoadInfo(_player!.GlobalPosition.X, _player.GlobalPosition.Z);
        _routeTransitions.Add(new RouteTransition(0, PhaseName(_routePhase),
            _player.GlobalPosition.X, _player.GlobalPosition.Z, startRoad.Distance, startRoad.HalfWidth));
        _recording = true;
    }

    private void AdvanceRoute(ulong now, double elapsedMs, Vector2 position,
        (double Distance, double HalfWidth) road, bool packedBand)
    {
        switch (_routePhase)
        {
            case RoutePhase.RoadOutbound:
                if (!packedBand)
                {
                    if (_roadSteering)
                    {
                        Input.ActionRelease("look_left");
                        _roadSteering = false;
                    }
                    _offroadStart = position;
                    SetRoutePhase(RoutePhase.OffroadOutbound, elapsedMs, position, road);
                }
                else if (_roadSteering && now >= _roadSteerUntilUsec)
                {
                    Input.ActionRelease("look_left");
                    _roadSteering = false;
                    _nextYawTurnUsec = now + 750_000;
                }
                else if (!_roadSteering && now >= _nextYawTurnUsec)
                {
                    Input.ActionPress("look_left");
                    _roadSteering = true;
                    _roadSteerUntilUsec = now + (ulong)Math.Ceiling(
                        30.0 / Math.Max(1.0, _player!.GamepadLookSpeed) * 1_000_000.0);
                }
                break;

            case RoutePhase.OffroadOutbound:
                if (position.DistanceTo(_offroadStart) >= OffroadOutboundMeters)
                {
                    Input.ActionRelease("move_forward");
                    Input.ActionPress("look_left");
                    _offroadTurnUntilUsec = now + (ulong)Math.Ceiling(
                        180.0 / Math.Max(1.0, _player!.GamepadLookSpeed) * 1_000_000.0);
                    SetRoutePhase(RoutePhase.TurningForReturn, elapsedMs, position, road);
                }
                break;

            case RoutePhase.TurningForReturn:
                if (now >= _offroadTurnUntilUsec)
                {
                    Input.ActionRelease("look_left");
                    Input.ActionPress("move_forward");
                    SetRoutePhase(RoutePhase.OffroadReturn, elapsedMs, position, road);
                }
                break;

            case RoutePhase.OffroadReturn:
                if (packedBand)
                {
                    _roadReturnStart = position;
                    SetRoutePhase(RoutePhase.RoadReturn, elapsedMs, position, road);
                }
                break;

            case RoutePhase.RoadReturn:
                if (position.DistanceTo(_roadReturnStart) >= RoadReturnMeters)
                    SetRoutePhase(RoutePhase.Complete, elapsedMs, position, road);
                break;
        }
    }

    private void SetRoutePhase(RoutePhase phase, double elapsedMs, Vector2 position,
        (double Distance, double HalfWidth) road)
    {
        _routePhase = phase;
        _routeTransitions.Add(new RouteTransition(elapsedMs, PhaseName(phase),
            position.X, position.Y, road.Distance, road.HalfWidth));
    }

    private void EnsureSegment(RoutePhase phase, bool packedBand)
    {
        if (_hasSegment && _lastSegmentPhase == phase && _lastSegmentPackedBand == packedBand)
            return;
        _segmentId++;
        _lastSegmentPhase = phase;
        _lastSegmentPackedBand = packedBand;
        _hasSegment = true;
    }

    private async Task FinishCapture()
    {
        if (_completionStarted) return;
        _completionStarted = true;
        _recording = false;
        Input.ActionRelease("move_forward");
        Input.ActionRelease("look_left");

        var packedEvents = _stepEvents.Where(e => e.PackedBand).Sum(e => e.TriggerDelta);
        var offroadEvents = _stepEvents.Where(e => !e.PackedBand).Sum(e => e.TriggerDelta);
        var routeComplete = _routePhase == RoutePhase.Complete;
        var receipt = new
        {
            probe = "act1-footstep-lag-profile",
            result = routeComplete && packedEvents >= 6 && offroadEvents >= 6 ? "data_complete" : "data_partial",
            route_complete = routeComplete,
            route_steps = new { road_band = packedEvents, offroad = offroadEvents },
            elapsed_ms = _frames.Count > 0 ? _frames[^1].ElapsedMs : 0,
            frame_count = _frames.Count,
            step_interval_m = _controller?.StepIntervalMeters,
            preset = _player?.GraphicsPreset ?? "unknown",
            display_driver = DisplayServer.GetName(),
            adapter = RenderingServer.GetVideoAdapterName(),
            route = new
            {
                outbound_offroad_m = OffroadOutboundMeters,
                road_return_m = RoadReturnMeters,
                transitions = _routeTransitions,
                end = _player is null ? null : new { x = _player.GlobalPosition.X, z = _player.GlobalPosition.Z }
            },
            segment_definition = "continuous phase + packed-band/offroad; packed band uses road.Distance < road.HalfWidth + 0.6m, actual audio surface is recorded separately",
            segments = _frames.GroupBy(frame => frame.SegmentId).Select(SummarizeSegment).ToArray(),
            step_events = _stepEvents,
            transition_gaps_ms = _stepEvents.Where(e => e.GapFromPreviousMs.HasValue && !e.SameSegmentAsPrevious)
                .Select(e => new { e.ElapsedMs, e.GapFromPreviousMs, e.Phase, e.SegmentId, e.Surface }).ToArray(),
            last_step_surface_changes = _surfaceChanges,
            timing_notes = new
            {
                frame_ms = "monotonic Time.GetTicksUsec delta between ProcessPriority=100 samples; drawn frame counters accompany it",
                process_physics_ms = "Godot Performance monitors sampled after default-priority game/audio callbacks",
                movement = $"XZ displacement and cumulative path are measured between later-priority Process samples; each step event summarizes the interval since the previous event. Same-segment intervals >= {MovementTraceThresholdMs:0} ms also include their per-frame movement/gate trace. Gate counts observe public actor state matching FootstepAudioController reset checks; the controller's private accumulator/reset calls are not exposed.",
                snow = "update/redraw/mesh timings are included only on frames where snowTrampleStampCount advanced; a multi-stamp frame reports the stamp delta and latest timing once",
                playback = "Playing/HasStreamPlayback/GetPlaybackPosition describe the single AudioStreamPlayer; internal polyphonic voice count is unavailable",
                surface = "LastSurface is the last surface resolved by an actual step; trigger events carry its fresh value, while per-frame fields can carry it forward between steps",
                data_only = "capture status is not a diagnosis or task acceptance"
            },
            hitches = BuildHitches()
        };
        GD.Print("act1-footstep-lag-profile: " + JsonSerializer.Serialize(receipt));
        GD.Print($"act1-footstep-lag-profile: data {(routeComplete ? "route-complete" : "route-incomplete")}; no diagnosis asserted");

        if (_main is not null && GodotObject.IsInstanceValid(_main))
            await GodotSmokeCleanup.ReleaseAsync(_main);
        GetTree().Quit(0);
    }

    private object SummarizeSegment(IGrouping<int, FrameSample> group)
    {
        var frames = group.ToArray();
        var first = frames[0];
        var last = frames[^1];
        var events = _stepEvents.Where(e => e.SegmentId == group.Key).ToArray();
        var intervals = events.Where(e => e.SameSegmentAsPrevious && e.GapFromPreviousMs.HasValue)
            .Select(e => e.GapFromPreviousMs!.Value).ToArray();
        var snowUpdates = frames.Where(frame => frame.StampDelta > 0 && frame.SnowUpdateMs.HasValue).ToArray();

        return new
        {
            id = group.Key,
            phase = PhaseName(first.Phase),
            location = first.PackedBand ? "packed_band" : "offroad",
            frames = frames.Length,
            elapsed_ms = new { start = first.ElapsedMs, end = last.ElapsedMs },
            position = new { start_x = first.X, start_z = first.Z, end_x = last.X, end_z = last.Z },
            step_trigger_count = events.Sum(e => e.TriggerDelta),
            step_intervals_ms = Distribution(intervals),
            frame_wall_ms = Distribution(frames.Select(frame => frame.FrameMs)),
            process_monitor_ms = Distribution(frames.Select(frame => frame.ProcessMonitorMs)),
            physics_monitor_ms = Distribution(frames.Select(frame => frame.PhysicsMonitorMs)),
            gc = new
            {
                gen0 = frames.Sum(frame => frame.Gen0Delta),
                gen1 = frames.Sum(frame => frame.Gen1Delta),
                gen2 = frames.Sum(frame => frame.Gen2Delta),
                allocated_bytes = frames.Sum(frame => frame.AllocatedBytesDelta)
            },
            snow = new
            {
                stamp_delta = frames.Sum(frame => frame.StampDelta),
                timed_update_frames = snowUpdates.Length,
                update_ms = Distribution(snowUpdates.Where(frame => frame.SnowUpdateMs.HasValue)
                    .Select(frame => frame.SnowUpdateMs!.Value)),
                redraw_ms = Distribution(snowUpdates.Where(frame => frame.SnowRedrawMs.HasValue)
                    .Select(frame => frame.SnowRedrawMs!.Value)),
                mesh_ms = Distribution(snowUpdates.Where(frame => frame.SnowMeshMs.HasValue)
                    .Select(frame => frame.SnowMeshMs!.Value))
            },
            last_step_surfaces = frames.Select(frame => frame.Surface).Where(surface => !string.IsNullOrEmpty(surface))
                .Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private object BuildMovementInterval(StepEvent? previousStep, FrameSample currentFrame)
    {
        var firstFrameExclusive = previousStep?.FrameIndex ?? -1;
        var samples = _frames.Where(frame => frame.Frame > firstFrameExclusive && frame.Frame <= currentFrame.Frame)
            .ToArray();
        var previousPosition = previousStep is { } previous
            ? new Vector2(previous.X, previous.Z)
            : _captureStartPosition;
        var currentPosition = new Vector2(currentFrame.X, currentFrame.Z);

        return new
        {
            from_event_frame = previousStep?.FrameIndex,
            to_event_frame = currentFrame.Frame,
            sampled_frames = samples.Length,
            elapsed_ms = previousStep is { } prior
                ? currentFrame.ElapsedMs - prior.ElapsedMs
                : currentFrame.ElapsedMs,
            xz_chord_meters = previousPosition.DistanceTo(currentPosition),
            xz_path_meters = samples.Sum(frame => frame.FrameDisplacementMeters),
            cumulative_xz_path_meters = currentFrame.CumulativePathMeters,
            max_frame_xz_displacement_meters = samples.Length > 0
                ? samples.Max(frame => frame.FrameDisplacementMeters)
                : 0.0,
            zero_displacement_frames = samples.Count(frame => frame.FrameDisplacementMeters < .0001),
            reset_gate_frame_counts = new
            {
                off_floor = samples.Count(frame => !frame.OnFloor),
                modal = samples.Count(frame => frame.ModalOpen),
                ladder = samples.Count(frame => frame.ClimbingLadder),
                vehicle = samples.Count(frame => frame.VehicleControlled),
                presentation_transform_revision_changed = samples.Count(frame => frame.PresentationTransformRevisionChanged),
                teleport_threshold_exceeded = samples.Count(frame => frame.TeleportThresholdExceeded)
            },
            frame_trace = previousStep is { } traceStart
                && currentFrame.ElapsedMs - traceStart.ElapsedMs >= MovementTraceThresholdMs
                && traceStart.SegmentId == currentFrame.SegmentId
                ? samples.Select(frame => new
                {
                    frame = frame.Frame,
                    frame.ElapsedMs,
                    frame.FrameMs,
                    frame.X,
                    frame.Z,
                    frame.DisplacementX,
                    frame.DisplacementZ,
                    frame.FrameDisplacementMeters,
                    frame.CumulativePathMeters,
                    velocity_speed = frame.VelocitySpeed,
                    frame.OnFloor,
                    frame.ModalOpen,
                    frame.ClimbingLadder,
                    frame.VehicleControlled,
                    frame.PresentationTransformRevision,
                    frame.PresentationTransformRevisionChanged,
                    frame.TeleportThresholdMeters,
                    frame.TeleportThresholdExceeded,
                    frame.Surface,
                    frame.TriggerDelta,
                    frame.StampDelta
                }).ToArray()
                : null
        };
    }

    private object BuildHitches()
    {
        var longFrames = _frames.Where(frame => frame.FrameMs >= HitchThresholdMs
            || frame.ProcessMonitorMs >= HitchThresholdMs || frame.PhysicsMonitorMs >= HitchThresholdMs)
            .OrderByDescending(frame => frame.FrameMs).Take(12);
        var gcFrames = _frames.Where(frame => frame.Gen0Delta > 0 || frame.Gen1Delta > 0 || frame.Gen2Delta > 0)
            .OrderByDescending(frame => frame.FrameMs).Take(12);
        var samples = longFrames.Concat(gcFrames).GroupBy(frame => frame.Frame)
            .Select(group => group.First()).OrderBy(frame => frame.ElapsedMs).Take(24)
            .Select(frame => new
            {
                frame = frame.Frame,
                frame_index = frame.Frame,
                frame.ElapsedMs,
                frame.FrameMs,
                frame.ProcessMonitorMs,
                frame.PhysicsMonitorMs,
                frame.Gen0Delta,
                frame.Gen1Delta,
                frame.Gen2Delta,
                frame.AllocatedBytesDelta,
                frame.X,
                frame.Z,
                velocity_speed = frame.VelocitySpeed,
                frame.DisplacementX,
                frame.DisplacementZ,
                frame.FrameDisplacementMeters,
                frame.CumulativePathMeters,
                frame.OnFloor,
                frame.ModalOpen,
                frame.ClimbingLadder,
                frame.VehicleControlled,
                frame.PresentationTransformRevision,
                frame.PresentationTransformRevisionChanged,
                frame.TeleportThresholdMeters,
                frame.TeleportThresholdExceeded,
                stamp_delta = frame.StampDelta,
                snow_update_ms = frame.SnowUpdateMs,
                snow_redraw_ms = frame.SnowRedrawMs,
                snow_mesh_ms = frame.SnowMeshMs,
                phase = PhaseName(frame.Phase),
                location = frame.PackedBand ? "packed_band" : "offroad",
                frame.Surface,
                frame.TriggerDelta,
                frame.DrawnFrame,
                frame.DrawDelta
            }).ToArray();

        return new
        {
            threshold_ms = HitchThresholdMs,
            threshold_is_diagnostic_only = true,
            long_frame_count = _frames.Count(frame => frame.FrameMs >= HitchThresholdMs),
            process_over_threshold_count = _frames.Count(frame => frame.ProcessMonitorMs >= HitchThresholdMs),
            physics_over_threshold_count = _frames.Count(frame => frame.PhysicsMonitorMs >= HitchThresholdMs),
            gc_frame_count = _frames.Count(frame => frame.Gen0Delta > 0 || frame.Gen1Delta > 0 || frame.Gen2Delta > 0),
            samples
        };
    }

    private static object Distribution(IEnumerable<double> source)
    {
        var values = source.Where(double.IsFinite).OrderBy(value => value).ToArray();
        if (values.Length == 0)
            return new { samples = 0, median = (double?)null, p95 = (double?)null, max = (double?)null };
        var median = values[values.Length / 2];
        var p95Index = Math.Clamp((int)Math.Ceiling(values.Length * .95) - 1, 0, values.Length - 1);
        return new
        {
            samples = values.Length,
            median = (double?)median,
            p95 = (double?)values[p95Index],
            max = (double?)values[^1]
        };
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }

    private static string PhaseName(RoutePhase phase) => phase switch
    {
        RoutePhase.RoadOutbound => "road_outbound",
        RoutePhase.OffroadOutbound => "offroad_outbound",
        RoutePhase.TurningForReturn => "turn_for_return",
        RoutePhase.OffroadReturn => "offroad_return",
        RoutePhase.RoadReturn => "road_return",
        _ => "complete"
    };

    private void Fail(string message)
    {
        Input.ActionRelease("move_forward");
        Input.ActionRelease("look_left");
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
