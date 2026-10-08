using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

// Development only: URMAN_VIEW_CAPTURE=<output dir> with
// URMAN_VIEW_POINTS="name:x,y,z>tx,ty,tz;..." saves one still per viewpoint from
// a free camera in the village and quits. Unset, it does nothing.
// Optional authored manifest res://content/world/visual_checkpoints.v1.json binds a frame
// name to an atmosphere profile, so one capture job can photograph the same street under
// every VIS-069…074 state without a second transport (see VisualCheckpointManifestPath).
public partial class Act1DemoRoot
{
    // Development only: URMAN_WALK_PROBE="Building;x,y,z;yawDegrees;seconds;action" puts the real
    // player controller at a building-local point, holds move_forward and prints where it ends up.
    // Optional sixth field is look pitch for capturing the visible body in motion.
    private async void DevWalkProbeBoot()
    {
        var spec = System.Environment.GetEnvironmentVariable("URMAN_WALK_PROBE");
        if (string.IsNullOrEmpty(spec)) return;
        for (var frame = 0; frame < 900 && !(MainMenuVisible && _main is not null && _player is not null); frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _mainMenu?.Dismiss();
        _mainMenu = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        foreach (var leg in spec.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = leg.Split(';');
            var building = _main!.FindChild(parts[0], true, false) as Node3D;
            var n = parts[1].Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var yaw = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            var seconds = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
            var action = parts.Length > 4 ? parts[4] : "move_forward";
            _player!.SetSessionTransition(false);
            _player.SetModalOpen(false);
            var start = building!.ToGlobal(new Vector3(n[0], n[1], n[2]));
            _player.ApplyZoneSpawn(start, building.RotationDegrees.Y + yaw);
            if (parts.Length > 5)
                _player.ApplySmokeLook(float.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture), building.RotationDegrees.Y + yaw);
            DisplayServer.WindowMoveToForeground();
            for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (_pauseMenu?.IsOpen == true) _pauseMenu.Resume();
            Input.ActionPress(action);
            var elapsed = 0d;
            while (elapsed < seconds)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                elapsed += GetPhysicsProcessDeltaTime();
            }
            Input.ActionRelease(action);
            var local = building.ToLocal(_player.GlobalPosition);
            GD.Print($"walk-probe-view: focus={DisplayServer.WindowIsFocused()} pause={_pauseMenu?.IsOpen} preset={GraphicsQuality.Preset} scale={GetViewport().Scaling3DScale} fov={GetViewport().GetCamera3D()?.Fov}");
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            GD.Print(string.Format(ci, "walk-probe: {0} start=({1},{2},{3}) yaw={4} floorY={5:0.00} startGlobal={6} -> local=({7:0.00},{8:0.00},{9:0.00}) global={10}",
                parts[0], n[0], n[1], n[2], yaw, building.GlobalPosition.Y, start, local.X, local.Y, local.Z, _player.GlobalPosition));
        }
        GetTree().Quit();
    }

    private static readonly JsonSerializerOptions ViewJsonOptions = new() { WriteIndented = true };

    // The authored checkpoint manifest is repo-side evidence: it carries the atmosphere
    // profile per frame so the existing station transport (capture -> URMAN_VIEW_POINTS) can
    // photograph states that no zone owns. Absent, every frame keeps today's zone default.
    private const string VisualCheckpointManifestPath = "res://content/world/visual_checkpoints.v1.json";

    private sealed record ViewSideResult(string Space, Node3D? Owner, Vector3 World);

    // "profile" is optional: a row without it documents the subject but leaves the light to
    // the zone, which is how the canonical C1…C8 comparison set keeps its before baseline.
    private sealed record VisualCheckpoint(string Id, string Spec, string? Profile, string? Subject);

    private sealed record ViewPoint(
        string Name,
        string Spec,
        string CameraSpace,
        Node3D? CameraOwner,
        Vector3 CameraWorld,
        string TargetSpace,
        Node3D? TargetOwner,
        Vector3 TargetWorld,
        string? Profile = null,
        string? Subject = null);

    private async void DevViewCaptureBoot()
    {
        var dir = System.Environment.GetEnvironmentVariable("URMAN_VIEW_CAPTURE");
        var points = System.Environment.GetEnvironmentVariable("URMAN_VIEW_POINTS");
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(points)) return;
        var zone = System.Environment.GetEnvironmentVariable("URMAN_VIEW_ZONE");
        for (var frame = 0; frame < 900 && !(MainMenuVisible && _main is not null && _player is not null); frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!string.IsNullOrEmpty(zone))
        {
            if (System.Environment.GetEnvironmentVariable("URMAN_PROTECTED_RUN") != "1"
                || !await StartDebugZoneAsync(zone, "entry"))
            {
                GD.PushError($"View capture refused: protected debug zone '{zone}' unavailable.");
                GetTree().Quit(1);
                return;
            }
        }
        // VIS-111: control views keep FOV 70 for before/after comparability, but a
        // capture can also take the player's own camera FOV (gameplay baseline 75 or
        // the player's setting) or an explicit value; the source is written per frame.
        var viewFov = 70f;
        var viewFovSource = "control-view-default";
        var fovSpec = System.Environment.GetEnvironmentVariable("URMAN_VIEW_FOV");
        if (!string.IsNullOrEmpty(fovSpec))
        {
            if (fovSpec == "player")
            {
                viewFov = (float)_player!.CaptureSettings().FieldOfView;
                viewFovSource = "player-camera";
            }
            else if (float.TryParse(fovSpec, System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out var explicitFov)
                     && explicitFov is >= 40f and <= 100f)
            {
                viewFov = explicitFov;
                viewFovSource = "explicit";
            }
            else
            {
                GD.PushError($"View capture refused: URMAN_VIEW_FOV='{fovSpec}' is neither 'player' nor a number in 40..100.");
                GetTree().Quit(1);
                return;
            }
        }
        // VIS-039: optional diagnostic decomposition for "light vs form" pairs —
        // neutral grey albedo and/or no fog. Capture-only; written into each frame.
        var diagnostic = System.Environment.GetEnvironmentVariable("URMAN_DIAGNOSTIC_VIEW") ?? "";
        if (diagnostic is not ("" or "neutral" or "no-fog" or "neutral-no-fog"))
        {
            GD.PushError($"View capture refused: URMAN_DIAGNOSTIC_VIEW='{diagnostic}' is not neutral, no-fog or neutral-no-fog.");
            GetTree().Quit(1);
            return;
        }
        // A frame is evidence only when its subject is known, so every point is bound
        // before the first PNG; an unknown or doubled owner stops the run instead of
        // silently becoming world space.
        var checkpoints = LoadVisualCheckpoints(out var manifestFailure);
        if (checkpoints is null)
        {
            GD.PushError($"View capture refused: {manifestFailure} No frame was written.");
            GetTree().Quit(1);
            return;
        }
        var plan = new List<ViewPoint>();
        foreach (var entry in points.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (ResolveViewPoint(entry, out var point) is { } failure)
            {
                GD.PushError($"View capture refused: {failure} Point '{entry}' wrote no frame.");
                GetTree().Quit(1);
                return;
            }
            if (BindVisualCheckpoint(point!, checkpoints, out var bound) is { } checkpointFailure)
            {
                GD.PushError($"View capture refused: {checkpointFailure} Point '{entry}' wrote no frame.");
                GetTree().Quit(1);
                return;
            }
            plan.Add(bound!);
        }
        if (plan.Count == 0)
        {
            GD.PushError("View capture refused: URMAN_VIEW_POINTS contained no point entries.");
            GetTree().Quit(1);
            return;
        }
        try
        {
            System.IO.Directory.CreateDirectory(dir);
        }
        catch (System.Exception error)
        {
            GD.PushError($"View capture refused: output directory '{dir}' is not usable: {error.Message}.");
            GetTree().Quit(1);
            return;
        }
        _mainMenu?.Dismiss();
        _mainMenu = null;
        _player!.SetModalOpen(true);
        var camera = new Camera3D { Name = "DevViewCamera", Fov = viewFov, Far = GetViewport().GetCamera3D()?.Far ?? 160f };
        camera.SetMeta("fovSource", viewFovSource);
        camera.SetMeta("diagnosticView", diagnostic);
        if (diagnostic.Contains("neutral", StringComparison.Ordinal)) PainterlyMaterialLibrary.SetDiagnosticNeutral(true);
        _main!.AddChild(camera);
        camera.MakeCurrent();
        var frames = new JsonArray();
        var phaseApplied = false;
        foreach (var point in plan)
        {
            if (!string.IsNullOrEmpty(zone) && _main!.ConnectedWorld?.ActiveZoneId != zone)
            {
                GD.PushError($"View capture refused: presented zone '{_main.ConnectedWorld?.ActiveZoneId}' differs from requested '{zone}'. Point '{point.Name}' wrote no frame.");
                GetTree().Quit(1);
                return;
            }
            // The authored state is applied before the camera settles, so the frame that is
            // saved is the frame the profile produced. A row without a profile leaves the
            // zone default untouched, and the override is released as soon as the batch
            // reaches an unrowed frame, so a capture with no manifest behaves as before.
            if (point.Profile is not null || phaseApplied)
            {
                if (_main!.ConnectedWorld is not { } world || !world.ApplyCapturePhase(point.Profile))
                {
                    GD.PushError($"View capture refused: atmosphere profile '{point.Profile ?? "zone default"}' could not be applied for point '{point.Name}'. Point '{point.Name}' wrote no frame.");
                    GetTree().Quit(1);
                    return;
                }
                phaseApplied = point.Profile is not null;
            }
            // Zone switches re-apply the profile, so fog is cleared per point.
            if (diagnostic.Contains("no-fog", StringComparison.Ordinal) && GetViewport().World3D?.Environment is { } diagnosticEnvironment)
            {
                diagnosticEnvironment.FogEnabled = false;
                diagnosticEnvironment.VolumetricFogEnabled = false;
            }
            camera.GlobalPosition = point.CameraWorld;
            camera.LookAt(point.TargetWorld, Vector3.Up);
            DisplayServer.WindowMoveToForeground();
            camera.MakeCurrent();
            for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_pauseMenu?.IsOpen == true) _pauseMenu.Resume();
            for (var frame = 0; frame < 150; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using (var image = GetViewport().GetTexture().GetImage())
            {
                if (image.SavePng($"{dir}/{point.Name}.png") != Error.Ok)
                {
                    GD.PushError($"View capture could not save '{point.Name}'.");
                    GetTree().Quit(1);
                    return;
                }
            }
            var metadata = ViewFrameMetadata(point, camera, zone);
            var payload = metadata.ToJsonString(ViewJsonOptions);
            frames.Add(JsonNode.Parse(payload)!);
            if (WriteViewJson($"{dir}/{point.Name}.json", payload) is { } sidecarError)
            {
                GD.PushError($"View capture refused: {sidecarError} Frame '{point.Name}' is not comparable evidence.");
                GetTree().Quit(1);
                return;
            }
            GD.Print($"view-capture: {point.Name} camera={GetViewport().GetCamera3D()?.Name} at={camera.GlobalPosition} zone={_main.ConnectedWorld?.ActiveZoneId} profile={ViewMeta(_main.ConnectedWorld?.GetNodeOrNull<Node3D>("Act1CoreWorldGreybox"), "unifiedAtmosphereProfile")} preset={GraphicsQuality.Preset} scale={GetViewport().Scaling3DScale} fov={camera.Fov} focus={DisplayServer.WindowIsFocused()} pause={_pauseMenu?.IsOpen} drawCalls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} frameMs={GetProcessDeltaTime()*1000:0.0}");
        }
        var summary = new JsonObject
        {
            ["engineVersion"] = ViewProbe(() => Engine.GetVersionInfo()["string"].AsString()),
            ["os"] = ViewProbe(() => OS.GetName()),
            ["osVersion"] = ViewProbe(() => System.Environment.OSVersion.ToString()),
            ["requestedZoneId"] = ViewText(zone),
            ["checkpointManifest"] = ViewText(VisualCheckpointManifestPath),
            ["checkpointRows"] = ViewNumber(checkpoints.Count),
            ["pointCount"] = ViewNumber(frames.Count),
            ["frames"] = frames
        };
        if (WriteViewJson($"{dir}/_capture-summary.json", summary.ToJsonString(ViewJsonOptions)) is { } summaryError)
        {
            GD.PushError($"View capture refused: {summaryError}");
            GetTree().Quit(1);
            return;
        }
        var tree = GetTree();
        await Tests.GodotSmokeCleanup.ReleaseAsync(this);
        QuitAfterViewCaptureAsync(tree);
    }

    /// <summary>
    /// Reads the optional authored checkpoint manifest. An empty result is normal and means
    /// every frame keeps its zone/default profile exactly as before this feature; a manifest
    /// that exists but cannot be read, or that names a profile the atmosphere data does not
    /// carry, is returned as a refusal reason so no PNG is written against unknown light.
    /// </summary>
    private static Dictionary<string, VisualCheckpoint>? LoadVisualCheckpoints(out string? failure)
    {
        failure = null;
        var rows = new Dictionary<string, VisualCheckpoint>(System.StringComparer.Ordinal);
        if (!global::Godot.FileAccess.FileExists(VisualCheckpointManifestPath)) return rows;
        var text = global::Godot.FileAccess.GetFileAsString(VisualCheckpointManifestPath);
        if (string.IsNullOrWhiteSpace(text))
        {
            failure = $"'{VisualCheckpointManifestPath}' is present but empty, so no checkpoint profile could be read.";
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("schemaVersion", out var schema)
                || schema.ValueKind != JsonValueKind.Number
                || schema.GetInt32() != 1)
            {
                failure = $"'{VisualCheckpointManifestPath}' is not a schemaVersion 1 checkpoint manifest.";
                return null;
            }
            if (!root.TryGetProperty("points", out var points) || points.ValueKind != JsonValueKind.Array)
            {
                failure = $"'{VisualCheckpointManifestPath}' carries no 'points' array, so it binds no frame to a profile.";
                return null;
            }
            foreach (var row in points.EnumerateArray())
            {
                var id = ViewJsonText(row, "id");
                var spec = ViewJsonText(row, "spec");
                if (id is null || spec is null)
                {
                    failure = $"a checkpoint row in '{VisualCheckpointManifestPath}' needs both 'id' and 'spec'.";
                    return null;
                }
                if (!ViewFileNameSafe(id!))
                {
                    failure = $"'{id}' in '{VisualCheckpointManifestPath}' is not a safe frame name of 1..48 [A-Za-z0-9_-].";
                    return null;
                }
                if (spec!.Contains(':') || !spec.Contains('>'))
                {
                    failure = $"checkpoint '{id}' has spec '{spec}', which is not the 'camera>target' part of a URMAN_VIEW_POINTS entry.";
                    return null;
                }
                if (rows.ContainsKey(id!))
                {
                    failure = $"checkpoint '{id}' is authored twice in '{VisualCheckpointManifestPath}'.";
                    return null;
                }
                rows.Add(id!, new VisualCheckpoint(id!, spec, ViewJsonText(row, "profile"), ViewJsonText(row, "subject")));
            }
        }
        catch (System.Exception error)
        {
            failure = $"could not parse '{VisualCheckpointManifestPath}': {error.Message}.";
            return null;
        }

        // Every authored row is checked, not only the requested ones: the manifest is
        // repo-side evidence, and a typo in a profile id has to surface the moment the
        // file is loaded instead of turning a later frame into an unlabelled light.
        foreach (var row in rows.Values.Where(row => row.Profile is not null))
            if (!AtmosphereProfiles.Has(row.Profile!))
            {
                failure = $"checkpoint '{row.Id}' names atmosphere profile '{row.Profile}', "
                    + $"which is not authored in {AtmosphereProfiles.Path} (known profiles: "
                    + string.Join(", ", AtmosphereProfiles.Ids) + ").";
                return null;
            }
        return rows;
    }

    // A manifest row only decides the light for its own frame, and only when the camera the
    // job asked for is the camera the checkpoint was authored on: an id reused at different
    // coordinates would silently photograph another subject under a state label.
    private static string? BindVisualCheckpoint(
        ViewPoint point,
        Dictionary<string, VisualCheckpoint> checkpoints,
        out ViewPoint? bound)
    {
        bound = point;
        if (!checkpoints.TryGetValue(point.Name, out var checkpoint)) return null;
        var coordinates = point.Spec[(point.Spec.IndexOf(':') + 1)..];
        if (!System.String.Equals(checkpoint.Spec, coordinates, System.StringComparison.Ordinal))
            return $"checkpoint '{checkpoint.Id}' is authored for spec '{checkpoint.Spec}' but the job asked "
                + $"'{coordinates}'; the frame would not show the agreed subject.";
        if (checkpoint.Profile is not null)
            bound = point with { Profile = checkpoint.Profile, Subject = checkpoint.Subject };
        return null;
    }

    // Null means the point is bound to a real subject; any other value is the refusal reason.
    private string? ResolveViewPoint(string entry, out ViewPoint? point)
    {
        point = null;
        var separator = entry.IndexOf(':');
        if (separator < 0) return "the entry has no ':' between the frame name and the coordinates.";
        var name = entry[..separator];
        if (!ViewFileNameSafe(name)) return $"'{name}' is not a safe frame name of 1..48 [A-Za-z0-9_-].";
        var sides = entry[(separator + 1)..].Split('>');
        if (sides.Length != 2) return "the entry needs exactly one '>' between camera and target.";
        var cameraOwner = sides[0].Contains('@') ? sides[0][..sides[0].IndexOf('@')] : "";
        var targetText = sides[1].Contains('@') || cameraOwner.Length == 0 ? sides[1] : cameraOwner + "@" + sides[1];
        if (ViewSide(sides[0], out var camera) is { } cameraFailure) return cameraFailure;
        if (ViewSide(targetText, out var target) is { } targetFailure) return targetFailure;
        if ((camera!.World - target!.World).LengthSquared() < 1e-4f)
            return "camera and target coincide, so the frame would have no subject.";
        point = new ViewPoint(name, entry, camera.Space, camera.Owner, camera.World, target.Space, target.Owner, target.World);
        return null;
    }

    // "Building@x,y,z" points are in that building's local space (front = +Z) and must bind
    // to exactly one Node3D; bare coordinates stay the only world-space form.
    private string? ViewSide(string text, out ViewSideResult? result)
    {
        result = null;
        var at = text.IndexOf('@');
        var owner = at < 0 ? "" : text[..at];
        if (!ViewVector(at < 0 ? text : text[(at + 1)..], out var local))
            return $"'{text}' is not three finite comma-separated numbers.";
        if (owner.Length == 0)
        {
            result = new ViewSideResult("world", null, local);
            return null;
        }
        if (owner == "Ground")
        {
            var grounded = new Vector3(local.X, local.Y + Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(local.X, local.Z), local.Z);
            result = new ViewSideResult("ground", null, grounded);
            return null;
        }
        var matches = _main!.FindChildren(owner, "", true, false).OfType<Node3D>().ToArray();
        if (matches.Length == 0)
            return $"no Node3D named '{owner}' exists under Main, and world-space fallback would frame a different subject.";
        if (matches.Length > 1)
            return $"'{owner}' binds to {matches.Length} nodes ({string.Join(", ", matches.Select(node => node.GetPath().ToString()))}); the owner must be unique.";
        var node = matches[0];
        result = new ViewSideResult($"node:{node.GetPath()}", node, node.ToGlobal(local));
        return null;
    }

    // Zone instances carry their logical id; an owner outside them belongs to the shared
    // exterior and frames a street view rather than another zone's presentation.
    private static string? ViewOwnerZone(Node3D? node)
    {
        for (var ancestor = node?.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
            if (ViewMeta(ancestor, "logicalZoneId") is { } zoneId)
                return zoneId;
        return null;
    }

    private JsonObject ViewFrameMetadata(ViewPoint point, Camera3D camera, string? requestedZone)
    {
        var viewport = GetViewport();
        var world = _main!.ConnectedWorld;
        var environmentNode = ViewFrameEnvironmentNode();
        var environment = environmentNode?.Environment;
        // The storm toggle has one caller (the first-night cutscene), so the running flag
        // is the only runtime authority for blizzard state; snow is read off its emitter.
        var snow = world?.GetNodeOrNull<CpuParticles3D>("Act1CoreWorldGreybox/AgentBExteriorWorld/AgentBSnow");
        return new JsonObject
        {
            ["metadataVersion"] = ViewNumber(2),
            ["engineVersion"] = ViewProbe(() => Engine.GetVersionInfo()["string"].AsString()),
            ["os"] = ViewProbe(() => OS.GetName()),
            ["point"] = ViewText(point.Name),
            ["spec"] = ViewText(point.Spec),
            ["cameraName"] = ViewText(camera.Name.ToString()),
            ["cameraPath"] = ViewText(camera.GetPath().ToString()),
            ["cameraPosition"] = ViewVector(camera.GlobalPosition),
            ["cameraTarget"] = ViewVector(point.TargetWorld),
            ["cameraForward"] = ViewVector(camera.GlobalTransform.Basis.Z * -1f),
            ["cameraSpace"] = ViewText(point.CameraSpace),
            ["targetSpace"] = ViewText(point.TargetSpace),
            ["cameraOwnerPath"] = ViewText(point.CameraOwner?.GetPath().ToString()),
            ["targetOwnerPath"] = ViewText(point.TargetOwner?.GetPath().ToString()),
            ["cameraOwnerZone"] = ViewText(ViewOwnerZone(point.CameraOwner)),
            ["targetOwnerZone"] = ViewText(ViewOwnerZone(point.TargetOwner)),
            ["fov"] = ViewNumber(camera.Fov),
            ["fovSource"] = ViewText(camera.GetMeta("fovSource", "").AsString()),
            ["diagnosticView"] = ViewText(camera.GetMeta("diagnosticView", "").AsString()),
            ["diagnosticNeutralScope"] = ViewText(PainterlyMaterialLibrary.DiagnosticNeutral ? "painterly-library" : null),
            ["near"] = ViewNumber(camera.Near),
            ["far"] = ViewNumber(camera.Far),
            ["viewportWidth"] = ViewNumber(viewport.GetVisibleRect().Size.X),
            ["viewportHeight"] = ViewNumber(viewport.GetVisibleRect().Size.Y),
            ["scaling3DScale"] = ViewNumber(viewport.Scaling3DScale),
            ["scaling3DMode"] = ViewText(viewport.Scaling3DMode.ToString()),
            ["graphicsPreset"] = ViewText(GraphicsQuality.Preset),
            ["activeZoneId"] = ViewText(world?.ActiveZoneId),
            ["requestedZoneId"] = ViewText(requestedZone),
            // The manifest asks, the world answers: 'requestedProfile' is what the authored
            // checkpoint row named, 'atmosphereProfile' is what the core world actually
            // reports after the tune. They must agree for the frame to be state evidence.
            ["checkpointSubject"] = ViewText(point.Subject),
            ["requestedProfile"] = ViewText(point.Profile),
            ["atmosphereProfile"] = ViewText(ViewMeta(world?.GetNodeOrNull<Node3D>("Act1CoreWorldGreybox"), "unifiedAtmosphereProfile")),
            ["atmosphereOwner"] = ViewText(ViewMeta(world, "activeAtmosphereOwner")),
            ["studioPreviewProfile"] = ViewText(Act1ConnectedWorld.StudioPreviewProfile),
            ["environmentPath"] = ViewText(environmentNode?.GetPath().ToString()),
            ["tonemapMode"] = ViewText(environment?.TonemapMode.ToString()),
            ["fogEnabled"] = ViewFlag(environment?.FogEnabled),
            ["volumetricFogEnabled"] = ViewFlag(environment?.VolumetricFogEnabled),
            ["fogColor"] = ViewText(environment?.FogLightColor.ToHtml()),
            ["fogDensity"] = ViewNumber(environment?.FogDensity),
            ["snowActive"] = ViewFlag(snow is null ? null : snow.Emitting && snow.Visible),
            ["snowAmount"] = ViewNumber(snow?.Amount),
            ["blizzardActive"] = ViewFlag(_firstNightRunning),
            ["drawCalls"] = ViewNumber(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)),
            ["capturedUnixSeconds"] = ViewNumber(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };
    }

    // Main.SwitchZone leaves exactly one WorldEnvironment resource in place, so the zone
    // placement decides which one rather than a guess from tree order.
    private WorldEnvironment? ViewFrameEnvironmentNode()
    {
        var world = _main?.ConnectedWorld;
        var exterior = world?.GetNodeOrNull<WorldEnvironment>("Act1CoreWorldGreybox/AgentBExteriorWorld/AgentBEnvironment");
        if (world?.ActiveZoneId is not { } zone
            || !Act1WorldLayout.TryGetPlacement(zone, out var placement)
            || !placement.Interior)
            return exterior;
        var zoneNode = world.GetChildren()
            .FirstOrDefault(child => child is Node3D && ViewMeta(child, "logicalZoneId") == zone);
        var indoor = zoneNode?.FindChildren("*", "WorldEnvironment", true, false)
            .OfType<WorldEnvironment>()
            .FirstOrDefault(candidate => candidate.Environment is not null);
        return indoor ?? exterior;
    }

    private string? WriteViewJson(string path, string json)
    {
        try
        {
            System.IO.File.WriteAllText(path, json);
            return null;
        }
        catch (System.Exception error)
        {
            return $"could not write '{path}': {error.Message}.";
        }
    }

    private static bool ViewFileNameSafe(string name) =>
        name.Length is > 0 and <= 48 && name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool ViewVector(string text, out Vector3 vector)
    {
        vector = Vector3.Zero;
        var values = text.Split(',').Select(value =>
            float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && float.IsFinite(parsed) ? parsed : float.NaN).ToArray();
        if (values.Length != 3 || values.Any(float.IsNaN)) return false;
        vector = new Vector3(values[0], values[1], values[2]);
        return true;
    }

    private static string? ViewMeta(Node? node, string key) =>
        node is not null && node.HasMeta(key) ? node.GetMeta(key).AsString() : null;

    // Optional manifest text: a row that is not an object, misses the key, or writes it as
    // null/empty all resolve to "no authored value", so 'profile' absence keeps the zone default.
    private static string? ViewJsonText(JsonElement row, string key) =>
        row.ValueKind == JsonValueKind.Object
        && row.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrEmpty(value.GetString())
            ? value.GetString()
            : null;

    private static JsonNode? ViewText(string? value) =>
        string.IsNullOrEmpty(value) ? null : JsonValue.Create(value);

    private static JsonNode? ViewFlag(bool? value) =>
        value is null ? null : JsonValue.Create(value.Value);

    private static JsonNode? ViewNumber(double? value) =>
        value is { } number && double.IsFinite(number)
            ? JsonNode.Parse(number.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture))
            : null;

    private static JsonNode? ViewProbe(System.Func<string> read)
    {
        try
        {
            return ViewText(read());
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    private static JsonObject ViewVector(Vector3 value) => new()
    {
        ["x"] = ViewNumber(value.X),
        ["y"] = ViewNumber(value.Y),
        ["z"] = ViewNumber(value.Z)
    };

    private static async void QuitAfterViewCaptureAsync(SceneTree tree)
    {
        // Let the capture state machine release its scene references before
        // collecting C# resource wrappers and flushing deferred render frees.
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        for (var frame = 0; frame < 3; frame++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Node.PrintOrphanNodes();
        GD.Print("view-capture: scene/resource cleanup completed");
        tree.Quit();
    }
}
