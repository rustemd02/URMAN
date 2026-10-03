using Godot;
using System.Text.Json;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // Explicit diagnostic of the existing probe, never an ordinary gameplay profiler.
    // GPU queries may describe an earlier completed frame; their backend delay is
    // not exposed here. Observation IDs are not fabricated GPU submission IDs.
    private bool _rendererDiagnosticsRequested, _rendererTimersOwned, _rendererCensusCaptured;
    private string _rendererDiagnosticOutput = string.Empty;
    private ulong _rendererLastDrawn;
    private int _rendererDroppedSamples;
    private const int MaximumRendererObservations = 120_000;
    private readonly List<RendererObservation> _rendererObservations = new();
    private readonly List<string> _rendererDiagnosticErrors = new();
    private object? _rendererCensus;
    private Rid _rendererViewport;
    private int _rendererKeyEvents, _rendererMouseEvents, _rendererJoyEvents, _rendererActionEvents;
    private int _rendererLastInputDevice = -1;
    private Vector2 _rendererMouseScreenDelta;

    private readonly record struct RendererPoint(float X, float Y, float Z)
    {
        public static RendererPoint From(Vector3 value) => new(value.X, value.Y, value.Z);
    }

    private readonly record struct RendererGameplayObservation(
        RendererPoint? Feet, RendererPoint? Velocity, RendererPoint? CameraPosition, RendererPoint? CameraForward,
        ulong CameraId, bool Focused, bool PauseOpen, bool IntroOpen, bool MainMenuOpen, bool ModalOpen,
        bool VehicleControlled, bool IsCrouching, bool SessionAvailable, string? Zone, string? InputDevice, int MouseMode,
        float MoveX, float MoveY, float LookX, float LookY, bool Sprint, bool Crouch, bool Jump,
        int KeyEvents, int MouseMotionEvents, int JoyEvents, int ActionEvents, int LastEventDevice,
        float MouseScreenX, float MouseScreenY, string? PauseTransitionReason, ulong PauseTransitionFrame,
        int PresentationRevision, int FallRecoveries, int EdgeClamps);

    private readonly record struct RendererObservation(
        int Sample, double ElapsedSeconds, double FrameMilliseconds, ulong ProcessFrame,
        ulong DrawnFrameObserved, bool NewDrawnFrame,
        double? CpuMilliseconds, double? GpuMilliseconds, double? SetupMilliseconds,
        double? ProcessMilliseconds, double? PhysicsMilliseconds,
        long VisibleDraws, long VisiblePrimitives, long ShadowDraws, long ShadowPrimitives,
        RendererGameplayObservation Gameplay);

    public override void _Input(InputEvent inputEvent)
    {
        if (DevQuickKey(inputEvent)) return;
        if (_rideCamera is not null && inputEvent is InputEventMouseMotion rideMotion
            && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _rideLook -= rideMotion.ScreenRelative * (_player?.MouseSensitivity ?? .1f);
            ApplyPrologueRideLook();
        }
        if (_prologueForestActive && inputEvent.IsActionPressed("ui_cancel")
            && inputEvent is not InputEventKey { Echo: true })
        {
            _prologueSkipRequested = true;
            GetViewport().SetInputAsHandled();
        }
        if (_prologueForestActive && (inputEvent.IsActionPressed("journal")
            || inputEvent.IsActionPressed("quick_save") || inputEvent.IsActionPressed("quick_load")))
            GetViewport().SetInputAsHandled();
        if (!_rendererDiagnosticsRequested || !_rendererTimersOwned) return;
        // Observe delivery only. Device IDs cannot identify a human or prove
        // native versus synthetic input; no event is handled or replayed here.
        switch (inputEvent)
        {
            case InputEventKey: _rendererKeyEvents++; break;
            case InputEventMouseMotion motion:
                _rendererMouseEvents++;
                _rendererMouseScreenDelta += motion.ScreenRelative;
                break;
            case InputEventJoypadMotion or InputEventJoypadButton: _rendererJoyEvents++; break;
            case InputEventAction: _rendererActionEvents++; break;
            default: return;
        }
        _rendererLastInputDevice = inputEvent.Device;
    }

    private void ResetRendererInputObservation()
    {
        _rendererKeyEvents = _rendererMouseEvents = _rendererJoyEvents = _rendererActionEvents = 0;
        _rendererMouseScreenDelta = Vector2.Zero;
    }

    private RendererGameplayObservation ObserveRendererGameplay(bool focused)
    {
        var player = _player;
        var camera = GetViewport().GetCamera3D();
        var move = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        var look = Input.GetVector("look_left", "look_right", "look_up", "look_down");
        var observed = new RendererGameplayObservation(
            player is null ? null : RendererPoint.From(player.GlobalPosition),
            player is null ? null : RendererPoint.From(player.Velocity),
            camera is null ? null : RendererPoint.From(camera.GlobalPosition),
            camera is null ? null : RendererPoint.From(-camera.GlobalBasis.Z.Normalized()),
            camera?.GetInstanceId() ?? 0, focused, _pauseMenu?.IsOpen == true, IntroVisible, _mainMenu is not null,
            player?.ModalOpen != false, player?.VehicleControlled == true, player?.IsCrouching == true, _bridge?.SessionIdentity is not null,
            _bridge?.CurrentZoneId, player?.CurrentInputDevice, (int)Input.MouseMode,
            move.X, move.Y, look.X, look.Y, Input.IsActionPressed("sprint"), Input.IsActionPressed("crouch"),
            Input.IsActionPressed("jump"), _rendererKeyEvents, _rendererMouseEvents, _rendererJoyEvents,
            _rendererActionEvents, _rendererLastInputDevice, _rendererMouseScreenDelta.X, _rendererMouseScreenDelta.Y,
            _pauseMenu?.LastTransitionReason, _pauseMenu?.LastTransitionFrame ?? 0,
            player?.PresentationTransformRevision ?? -1, player?.FallRecoveries ?? -1, player?.EdgeClamps ?? -1);
        ResetRendererInputObservation();
        return observed;
    }

    private void ConfigureRendererDiagnostics()
    {
        _rendererDiagnosticsRequested = _performanceProbe
            && OS.GetEnvironment("URMAN_PERF_RENDER_DIAGNOSTICS") == "1";
        if (!_rendererDiagnosticsRequested) return;
        _rendererObservations.Clear();
        _rendererDiagnosticErrors.Clear();
        _rendererDroppedSamples = 0;
        _rendererLastDrawn = 0;
        try
        {
            _rendererDiagnosticOutput = OS.GetEnvironment("URMAN_PERF_RENDER_OUTPUT");
            if (!Path.IsPathFullyQualified(_rendererDiagnosticOutput)
                || !Directory.Exists(Path.GetDirectoryName(_rendererDiagnosticOutput))
                || File.Exists(_rendererDiagnosticOutput))
                throw new IOException("URMAN_PERF_RENDER_OUTPUT needs an unused absolute file path in an existing directory.");
            if (!_performanceProbeConfigurationValid || !_performanceWindowed || !_performanceRealRenderer
                || _performanceProbeMode != PerformanceProbeModeGameplay)
                throw new InvalidOperationException("Renderer diagnostics require the existing valid native gameplay probe.");
            _rendererViewport = GetViewport().GetViewportRid();
            // Reserve outside the timed interval; record structs avoid per-frame JSON/allocation.
            _rendererObservations.EnsureCapacity(16_384);
            RenderingServer.ViewportSetMeasureRenderTime(_rendererViewport, true);
            _rendererTimersOwned = true;
        }
        catch (Exception error) { _rendererDiagnosticErrors.Add("configure: " + error.Message); }
    }

    private void RecordRendererDiagnosticSample(double frameMilliseconds, bool focused)
    {
        if (!_rendererDiagnosticsRequested || !_rendererTimersOwned) return;
        if (_rendererObservations.Count >= MaximumRendererObservations)
        { _rendererDroppedSamples++; return; }
        try
        {
            var drawn = (ulong)Engine.GetFramesDrawn();
            var fresh = drawn > _rendererLastDrawn && drawn >= 2;
            _rendererLastDrawn = drawn;
            static double? Finite(double value) => double.IsFinite(value) ? value : null;
            long Count(RenderingServer.ViewportRenderInfoType pass, RenderingServer.ViewportRenderInfo item) =>
                RenderingServer.ViewportGetRenderInfo(_rendererViewport, pass, item);
            _rendererObservations.Add(new RendererObservation(
                _performanceSamples.Count, _performanceMeasurementElapsed + frameMilliseconds / 1000.0,
                frameMilliseconds, Engine.GetProcessFrames(), drawn, fresh,
                Finite(RenderingServer.ViewportGetMeasuredRenderTimeCpu(_rendererViewport)),
                Finite(RenderingServer.ViewportGetMeasuredRenderTimeGpu(_rendererViewport)),
                Finite(RenderingServer.GetFrameSetupTimeCpu()),
                Finite(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0),
                Finite(Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0),
                Count(RenderingServer.ViewportRenderInfoType.Visible, RenderingServer.ViewportRenderInfo.DrawCallsInFrame),
                Count(RenderingServer.ViewportRenderInfoType.Visible, RenderingServer.ViewportRenderInfo.PrimitivesInFrame),
                Count(RenderingServer.ViewportRenderInfoType.Shadow, RenderingServer.ViewportRenderInfo.DrawCallsInFrame),
                Count(RenderingServer.ViewportRenderInfoType.Shadow, RenderingServer.ViewportRenderInfo.PrimitivesInFrame),
                ObserveRendererGameplay(focused)));
        }
        catch (Exception error)
        {
            _rendererDiagnosticErrors.Add("sample: " + error.Message);
            StopRendererDiagnostics();
        }
    }

    private void StopRendererDiagnostics()
    {
        if (!_rendererTimersOwned) return;
        _rendererTimersOwned = false;
        if (!_rendererViewport.IsValid) return;
        try { RenderingServer.ViewportSetMeasureRenderTime(_rendererViewport, false); }
        catch (Exception error) { _rendererDiagnosticErrors.Add("stop: " + error.Message); }
    }

    private void CaptureRendererCensus()
    {
        if (!_rendererDiagnosticsRequested || _rendererCensusCaptured) return;
        _rendererCensusCaptured = true;
        StopRendererDiagnostics();
        if (_performanceSamples.Count == 0 || _main.ConnectedWorld is not { } world) return;
        try
        {
            var camera = GetViewport().GetCamera3D();
            if (camera is null) throw new InvalidOperationException("The measured camera is unavailable.");
            var candidates = new HashSet<ulong>();
            // One read outside timing. This API flushes pending rendering resources;
            // its output is a spatial candidate set, not proof of an actual GPU draw.
            foreach (var id in RenderingServer.InstancesCullConvex(camera.GetFrustum(), camera.GetWorld3D().Scenario))
                candidates.Add(unchecked((ulong)id));
            var nodes = new List<Node>();
            var pending = new Stack<Node>();
            pending.Push(world);
            while (pending.TryPop(out var node))
            {
                nodes.Add(node);
                foreach (var child in node.GetChildren()) pending.Push(child);
            }
            var meshes = nodes.OfType<MeshInstance3D>().Where(mesh => mesh.Mesh is not null).ToArray();
            var meshReferences = meshes.GroupBy(mesh => mesh.Mesh!.GetRid().Id)
                .ToDictionary(group => group.Key, group => group.Count());
            var owners = new Dictionary<string, RendererCensusOwner>(StringComparer.Ordinal);
            var rootFallback = new Dictionary<string, (int MeshNodes, int Surfaces, int Visible, int Candidates)>(StringComparer.Ordinal);
            var resourceGroups = new Dictionary<string, (int Surfaces, HashSet<(ulong Mesh, int Surface, ulong Material, bool Mirror)> Pairs)>(StringComparer.Ordinal);
            var worldShell = world.GetNodeOrNull<Node>("Act1CoreWorldGreybox");
            var windowRows = new List<object>();
            var windowResources = new Dictionary<ulong, object>();
            foreach (var mesh in meshes)
            {
                Node owner = mesh;
                while (owner.GetParent() is { } parent && !owner.HasMeta("assetSource") && owner != world)
                    owner = parent;
                var ownerPath = owner.GetPath().ToString();
                if (!owners.TryGetValue(ownerPath, out var group))
                    owners[ownerPath] = group = new RendererCensusOwner(ownerPath,
                        owner.GetMeta("assetSource", "").AsString());
                var source = mesh.Mesh!;
                var visible = mesh.IsVisibleInTree();
                var layerMatches = (mesh.Layers & camera.CullMask) != 0;
                var candidate = candidates.Contains(mesh.GetInstanceId());
                if (owner == world)
                {
                    // Procedural pieces have no imported asset owner. Preserve the
                    // original census and separately name their existing world group.
                    Node fallback = mesh;
                    while (fallback.GetParent() is { } ancestor && ancestor != world && ancestor != worldShell)
                        fallback = ancestor;
                    var key = fallback.GetPath().ToString();
                    var counts = rootFallback.GetValueOrDefault(key);
                    rootFallback[key] = (counts.MeshNodes + 1, counts.Surfaces + source.GetSurfaceCount(),
                        counts.Visible + (visible ? 1 : 0), counts.Candidates + (visible && layerMatches && candidate ? 1 : 0));
                }
                group.MeshNodes++;
                if (visible) group.VisibleInTree++;
                if (visible && layerMatches && candidate) group.VisibleSpatialCandidates++;
                group.SurfaceCount += source.GetSurfaceCount();
                group.MeshRids.Add(source.GetRid().Id);
                var activeMaterials = Enumerable.Range(0, source.GetSurfaceCount())
                    .Select(surface => mesh.GetActiveMaterial(surface)?.GetRid().Id ?? 0UL).ToArray();
                foreach (var rid in activeMaterials) group.MaterialRids.Add(rid);
                if (visible && layerMatches && candidate && mesh.Skin is null
                    && (source is not ArrayMesh rigid || rigid.GetBlendShapeCount() == 0))
                {
                    Node spatialRoot = mesh;
                    while (spatialRoot.GetParent() is { } ancestor && ancestor != world && ancestor != worldShell)
                        spatialRoot = ancestor;
                    var key = spatialRoot.GetPath().ToString();
                    if (!resourceGroups.TryGetValue(key, out var counts))
                        counts = (0, new HashSet<(ulong, int, ulong, bool)>());
                    for (var surface = 0; surface < activeMaterials.Length; surface++)
                        counts.Pairs.Add((source.GetRid().Id, surface, activeMaterials[surface], mesh.GlobalBasis.Determinant() < 0));
                    resourceGroups[key] = (counts.Surfaces + activeMaterials.Length, counts.Pairs);
                }
                if (!mesh.HasMeta("windowSurroundPaint")) continue;
                group.PaintedWindowNodes++;
                if (visible && layerMatches && candidate) group.WindowSpatialCandidates++;
                var sourceRid = source.GetRid().Id;
                windowRows.Add(new
                {
                    path = mesh.GetPath().ToString(), owner = ownerPath, meshRid = sourceRid,
                    activeMaterialRids = activeMaterials, visibleInTree = visible, cameraLayerMatches = layerMatches,
                    spatialFrustumCandidate = candidate, layers = mesh.Layers, transform = mesh.GlobalTransform.ToString(),
                    castShadow = mesh.CastShadow.ToString(), mesh.LodBias,
                    mesh.VisibilityRangeBegin, mesh.VisibilityRangeEnd,
                    mesh.VisibilityRangeBeginMargin, mesh.VisibilityRangeEndMargin,
                    fade = mesh.VisibilityRangeFadeMode.ToString()
                });
                if (windowResources.ContainsKey(sourceRid)) continue;
                object levels;
                if (source is ArrayMesh array)
                {
                    using var imported = ImporterMesh.FromMesh(array);
                    levels = Enumerable.Range(0, array.GetSurfaceCount()).Select(surface => new
                    {
                        surface, format = array.SurfaceGetFormat(surface).ToString(),
                        distances = Enumerable.Range(0, imported.GetSurfaceLodCount(surface))
                            .Select(lod => imported.GetSurfaceLodSize(surface, lod)).ToArray()
                    }).ToArray();
                }
                else levels = "not-an-ArrayMesh; imported-levels-not-inspected";
                windowResources[sourceRid] = new
                {
                    meshRid = sourceRid, resourcePath = source.ResourcePath, resourceName = source.ResourceName,
                    worldReferenceCount = meshReferences[sourceRid], surfaceCount = source.GetSurfaceCount(),
                    importedLevels = levels, shadowMeshRid = (source as ArrayMesh)?.ShadowMesh?.GetRid().Id
                };
            }
            _rendererCensus = new
            {
                timing = "once after measurement; before transport exit; no geometry, visibility or state writes",
                processFrame = Engine.GetProcessFrames(), drawnFrame = Engine.GetFramesDrawn(),
                camera = camera.GetPath().ToString(), cameraPose = camera.GlobalTransform.ToString(),
                scope = "connected world MeshInstance3D inventory; tree visibility and camera-mask/frustum candidates exclude GPU occlusion/LOD decisions",
                meshNodes = meshes.Length, distinctMeshResources = meshReferences.Count,
                addressAudits = nodes.OfType<AddressAccessVerifier>().Select(audit => audit.DescribeProgress()).ToArray(),
                multiMeshNodes = nodes.OfType<MultiMeshInstance3D>().Count(),
                multiMeshInstances = nodes.OfType<MultiMeshInstance3D>().Sum(node => (long)(node.Multimesh?.InstanceCount ?? 0)),
                ownerGroups = owners.Values.OrderByDescending(group => group.SurfaceCount).ToArray(),
                rootFallbackGroups = rootFallback.OrderByDescending(pair => pair.Value.Candidates).Select(pair => new
                {
                    owner = pair.Key, meshNodes = pair.Value.MeshNodes, surfaceCount = pair.Value.Surfaces,
                    visibleInTree = pair.Value.Visible, visibleSpatialCandidates = pair.Value.Candidates
                }).ToArray(),
                rigidResourceGroups = resourceGroups.OrderByDescending(pair => pair.Value.Surfaces).Select(pair => new
                {
                    owner = pair.Key, candidateSurfaces = pair.Value.Surfaces,
                    distinctMeshMaterialMirrorPairs = pair.Value.Pairs.Count,
                    scope = "frustum candidates only; excludes skins/blendshapes; depth sorting, lights, transparency and GPU culling may split actual draws"
                }).ToArray(),
                occlusionCulling = GetViewport().UseOcclusionCulling,
                windowCandidates = windowRows, windowMeshResources = windowResources.Values.ToArray(),
                rids = "process-local resource identities; compare counts/paths across runs, not numeric RID equality",
                batchEligibility = "not decided; snapshot does not prove a mesh is static or safe to combine"
            };
        }
        catch (Exception error) { _rendererDiagnosticErrors.Add("census: " + error.Message); }
    }

    private sealed class RendererCensusOwner(string owner, string assetSource)
    {
        public string Owner { get; } = owner;
        public string AssetSource { get; } = assetSource;
        public int MeshNodes { get; set; }
        public int VisibleInTree { get; set; }
        public int VisibleSpatialCandidates { get; set; }
        public int SurfaceCount { get; set; }
        public int PaintedWindowNodes { get; set; }
        public int WindowSpatialCandidates { get; set; }
        public HashSet<ulong> MeshRids { get; } = new();
        public HashSet<ulong> MaterialRids { get; } = new();
    }

    private void FinishRendererDiagnostics(ref int exitCode, ref string? report)
    {
        if (!_rendererDiagnosticsRequested) return;
        StopRendererDiagnostics();
        var originalReport = report;
        var originalExit = exitCode;
        // Keep the measured FPS/threshold/route report separately. An instrumented
        // invocation cannot be accepted by the ordinary runner as status=PASS/exit0.
        report = report is null
            ? $"act1-demo-package-performance: status=DIAGNOSTIC_RENDER sample={ProbeToken(_performanceSample.Label)} acceptance=false"
            : string.Join(' ', report.Split(' ').Select(token => token.StartsWith("status=", StringComparison.Ordinal)
                ? "status=DIAGNOSTIC_RENDER" : token)) + " acceptance=false";
        exitCode = 2;
        try
        {
            if (!Path.IsPathFullyQualified(_rendererDiagnosticOutput)
                || !Directory.Exists(Path.GetDirectoryName(_rendererDiagnosticOutput)))
                throw new IOException("No valid renderer diagnostic output path.");
            object Stats(Func<RendererObservation, double?> selector, bool positiveOnly)
            {
                var all = _rendererObservations.Select(row => (row.NewDrawnFrame, Value: selector(row))).ToArray();
                var valid = all.Where(item => item.NewDrawnFrame && item.Value is { } value
                        && double.IsFinite(value) && (positiveOnly ? value > 0 : value >= 0))
                    .Select(item => item.Value!.Value).Order().ToArray();
                return new
                {
                    observed = all.Length, valid = valid.Length,
                    notNewDrawnFrame = all.Count(item => !item.NewDrawnFrame),
                    unavailableOrNegative = all.Count(item => item.Value is null || item.Value < 0),
                    zero = all.Count(item => item.Value == 0),
                    zeroPolicy = positiveOnly ? "excluded; unavailable or below timer resolution" : "valid counter value after renderer initialization",
                    mean = valid.Length == 0 ? (double?)null : valid.Average(),
                    p95 = valid.Length == 0 ? (double?)null : Percentile(valid, .95),
                    p99 = valid.Length == 0 ? (double?)null : Percentile(valid, .99),
                    maximum = valid.Length == 0 ? (double?)null : valid[^1]
                };
            }
            var receipt = new
            {
                schema = "urman.renderer_diagnostics.v1", status = _rendererDiagnosticErrors.Count == 0
                    && _rendererObservations.Count == _performanceSamples.Count && _rendererObservations.Count > 0
                    && _rendererDroppedSamples == 0 ? "RECORDED" : "PARTIAL",
                acceptance = false, sample = _performanceSample.Label, mode = _performanceProbeMode,
                performanceReport = originalReport, performanceExitWithoutDiagnosticClassification = originalExit,
                requestedWarmupSeconds = _performanceWarmupSeconds, actualWarmupSeconds = _performanceWarmupElapsed,
                requestedMeasurementSeconds = _performanceDurationSeconds, actualMeasurementSeconds = _performanceMeasurementElapsed,
                performanceSampleCount = _performanceSamples.Count, rendererSampleCount = _rendererObservations.Count,
                droppedSamples = _rendererDroppedSamples, errors = _rendererDiagnosticErrors.ToArray(),
                timing = "observations only on existing measurement samples; warmup/placement/exit/census excluded",
                gameplayObservation = "pose/action state at each process sample; input event counts since the previous sample or warmup frame. Mouse deltas are observed delivery, not proof of accepted camera rotation. Device IDs do not identify the user. No input/pose writes. Pause reason/frame comes from the actual pause owner.",
                querySemantics = "CPU/setup and counters are latest published values. GPU results may lag several frames; no backend submission ID/delay is exposed. Process/drawn IDs identify observation time only. Initial/final GPU values may straddle the measurement boundary; no forced synchronization or GPU fence is added.",
                aggregation = "CPU/GPU/setup are independent distributions, not summed or paired to explain individual FPS stalls. Equal consecutive GPU values are retained; equality does not prove a duplicate query.",
                viewportOnly = "render CPU/GPU are this gameplay viewport; setup is global. Other viewport GPU times are not silently included.",
                preset = _player?.GraphicsPreset, scale = GetViewport().Scaling3DScale,
                msaa = GetViewport().Msaa3D.ToString(), window = DisplayServer.WindowGetSize().ToString(),
                focusedSamples = _performanceFocusedSamples, obscuredSamples = _performanceObscuredSamples,
                summary = new
                {
                    cpuMs = Stats(row => row.CpuMilliseconds, true), gpuMs = Stats(row => row.GpuMilliseconds, true),
                    setupMs = Stats(row => row.SetupMilliseconds, true),
                    processMs = Stats(row => row.ProcessMilliseconds, false),
                    physicsMs = Stats(row => row.PhysicsMilliseconds, false),
                    visibleDraws = Stats(row => row.VisibleDraws, false),
                    visiblePrimitives = Stats(row => row.VisiblePrimitives, false),
                    shadowDraws = Stats(row => row.ShadowDraws, false),
                    shadowPrimitives = Stats(row => row.ShadowPrimitives, false)
                },
                observations = _rendererObservations, census = _rendererCensus
            };
            using var file = new FileStream(_rendererDiagnosticOutput, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(file, receipt, new JsonSerializerOptions
                { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            file.Flush();
            GD.Print("act1-render-diagnostics: " + JsonSerializer.Serialize(new
                { status = receipt.status, path = _rendererDiagnosticOutput, samples = _rendererObservations.Count, acceptance = false }));
        }
        catch (Exception error)
        {
            GD.Print("act1-render-diagnostics: " + JsonSerializer.Serialize(new
                { status = "NOT_WRITTEN", error = error.Message, acceptance = false }));
        }
    }
}
