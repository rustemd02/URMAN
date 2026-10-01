using Godot;
using System.Text.Json;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private static bool AddressReadabilityOnly =>
        System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_ADDRESS_ONLY") == "1";

    private sealed record AddressKnowledgeSnapshot(string Knowledge, string AddressProps,
        string Known, string Located, string? Scene, string Zone);

    private async Task RunAddressReadabilityChecks()
    {
        Require(DisplayServer.GetName() != "headless" &&
            System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_CAPTURE") == "1",
            "driver address observation requires native images, not a headless or capture-disabled result");
        var world = _demo.DemoMain.ConnectedWorld ?? throw new InvalidOperationException("Missing address world.");
        var registry = world.AddressRegistry ?? throw new InvalidOperationException("Missing live address registry.");
        var failures = new List<string>();
        var cartOnly = System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_CART_ONLY") == "1";
        var initialKnowledge = AddressKnowledge();
        foreach (var sample in new[] {
            (Vehicle: "babay-niva", Address: "ADR-H016"),
            (Vehicle: "village-motorcycle", Address: "ADR-FAP"),
            (Vehicle: "forest-horse-cart", Address: "ADR-H023") })
        {
            if (cartOnly && sample.Vehicle != "forest-horse-cart") continue;
            var vehicle = _fleet.Vehicles.Single(v => v.Definition.Id == sample.Vehicle);
            try
            {
                var plate = world.FindChildren("*", "Node3D", true, false)
                    .OfType<AddressSignVisualComponent>().Single(p => p.AddressId == sample.Address);
                var record = registry.Addresses[sample.Address];
                var street = registry.Streets[record.StreetId];
                var labels = plate.GetChildren().OfType<Label3D>().OrderBy(l => l.Name.ToString(), StringComparer.Ordinal).ToArray();
                Require(labels.Length == 3 && labels.Single(l => l.Name == "TatarStreet").Text == street.Tatar
                    && labels.Single(l => l.Name == "RussianStreet").Text == street.Russian
                    && labels.Single(l => l.Name == "HouseNumber").Text == record.HouseNumber,
                    sample.Vehicle + ": three actual mounted labels equal the live registry");
                var mount = plate.GlobalTransform;
                var originalFov = _player.CaptureSettings().FieldOfView;
                _records.Add(new { kind = "driver-address-live-mount", sample.Vehicle, sample.Address,
                    record.BuildingId, record.ParcelId, record.StreetId, record.HouseNumber,
                    plate = plate.GetPath().ToString(), position = AddressCoordinates(mount.Origin),
                    basis = mount.Basis.ToString(), labels = labels.Select(l => new { name = l.Name.ToString(), l.Text,
                        l.FontSize, l.PixelSize, l.NoDepthTest, l.DoubleSided }).ToArray(),
                    geometryChanged = false, automaticKnowledge = false, readabilityAccepted = false });
                await Approach(vehicle); // Existing declared pedestrian local fixture; vehicle and sign never teleported.
                await Press("interact"); await Frames(4);
                Require(_fleet.Occupied == vehicle && vehicle.Driver == _player && _player.VehicleControlled
                    && GetViewport().GetCamera3D() == vehicle.VehicleCamera && !_camera.Current,
                    sample.Vehicle + ": ordinary entry owns the real driver camera");
                await Press("carry_use"); await Frames(40);
                Require(vehicle.EngineRunning && !vehicle.ParkingBrake, sample.Vehicle + ": mapped ignition releases parking");
                if (vehicle.Definition.Kind == VehicleKind.Niva)
                    await DriveAddressLeg(vehicle, [new(-1.65f, 2.5f)], reverse: true, "niva-existing-parking-reverse");
                else if (vehicle.Definition.Kind == VehicleKind.Motorcycle)
                {
                    await DriveAddressLeg(vehicle,
                        [new(.9f, -19), new(.4f, -15), new(-.02f, -9.8f)], reverse: true, "motorcycle-main-road-return");
                    await DriveAddressLeg(vehicle, [new(4.5f, -12.5f), new(10, -17), new(17, -21.5f),
                        new(23, -25), new(27.2f, -25.4f)], reverse: false, "motorcycle-existing-fap-branch");
                }
                else
                    await DriveAddressLeg(vehicle, [new(-.39f, -37), new(-.06087f, -40.8f)],
                        reverse: false, "cart-existing-main-road");
                Require(vehicle.ParkingBrake && Math.Abs(vehicle.Speed) < .035f && vehicle.ValidatePhysicalPlacement(out _),
                    sample.Vehicle + ": measured stop has the actual compound support and clear stationary chassis");
                var beforeLook = AddressKnowledge();
                await CaptureDriverAddress(vehicle, plate, labels, originalFov);
                var afterLook = AddressKnowledge();
                _records.Add(new { kind = "driver-address-passive-state", sample.Vehicle, sample.Address,
                    beforeLook, afterLook, unchanged = beforeLook == afterLook, mountUnchanged = plate.GlobalTransform == mount,
                    explicitPlateInteraction = false, readabilityAccepted = false });
                Require(beforeLook == afterLook && afterLook == initialKnowledge,
                    sample.Vehicle + ": entering, driving and passive looking add no knowledge or located address");
                Require(plate.GlobalTransform == mount && labels.Single(l => l.Name == "TatarStreet").Text == street.Tatar
                    && labels.Single(l => l.Name == "RussianStreet").Text == street.Russian
                    && labels.Single(l => l.Name == "HouseNumber").Text == record.HouseNumber,
                    sample.Vehicle + ": actual sign mount and registry text survive observation unchanged");
            }
            catch (Exception error)
            {
                failures.Add(sample.Vehicle + ": " + error.Message);
                _records.Add(new { kind = "driver-address-observation-failed", sample.Vehicle, sample.Address,
                    error = error.ToString(), vehiclePosition = AddressCoordinates(vehicle.GlobalPosition),
                    vehicle.LastRefusal, accepted = false });
            }
            finally
            {
                ReleaseAddressDrivingInput();
                if (vehicle.Driver == _player)
                {
                    await StopAddressVehicle(vehicle);
                    await Press("interact"); await Frames(4);
                    Require(vehicle.Driver is null && !_player.VehicleControlled && _camera.Current
                        && _player.CanStandAt(_player.GlobalPosition), sample.Vehicle + ": mapped E returns to supported pedestrian control");
                }
            }
        }
        _records.Add(new { kind = "driver-address-scope-result", cartOnly, technicalFailures = failures,
            passiveKnowledgeUnchanged = AddressKnowledge() == initialKnowledge, acceptance = false,
            readability = "requires independent inspection of actual 720p/1080p driver images",
            limit = "Projection and candidate ray contacts do not establish readable glyphs. No GPS, sign relocation, FOV override or seated notebook action." });
        Require(AddressKnowledge() == initialKnowledge, "complete driver observation leaves knowledge, address props and notebook locations unchanged");
        Require(failures.Count == 0, "selected driver address observations completed: " + string.Join(" | ", failures));
    }

    private AddressKnowledgeSnapshot AddressKnowledge()
    {
        var props = _bridge.SelectWorldProps().EnumerateObject().Where(p => p.Name.StartsWith("address/", StringComparison.Ordinal))
            .OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new { p.Name, value = p.Value.Clone() });
        return new(_bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText(), JsonSerializer.Serialize(props),
            JsonSerializer.Serialize(_bridge.KnownAddressIds()), JsonSerializer.Serialize(_bridge.LocatedAddressIds()),
            _bridge.ActiveSceneId, _bridge.CurrentZoneId);
    }

    private async Task DriveAddressLeg(VehicleController vehicle, Vector2[] waypoints, bool reverse, string phase)
    {
        if (vehicle.ParkingBrake) { await Press("crouch"); await Frames(3); }
        var started = Time.GetTicksMsec(); var startedFrame = Engine.GetPhysicsFrames();
        var before = vehicle.GlobalPosition; var previous = before; var stops = vehicle.CollisionStops;
        var revision = _player.PresentationTransformRevision; var index = 0; var distance = 0f; var stalled = 0f;
        var samples = new List<object>(); var nextSample = 0UL;
        try
        {
            while (Time.GetTicksMsec() - started < 90000 && Engine.GetPhysicsFrames() - startedFrame < 10800)
            {
                var current = new Vector2(vehicle.GlobalPosition.X, vehicle.GlobalPosition.Z);
                while (index < waypoints.Length - 1 && current.DistanceTo(waypoints[index]) < 1.0f) index++;
                var remaining = current.DistanceTo(waypoints[index]);
                if (index == waypoints.Length - 1 && remaining < .40f) break;
                RequireAddressDriver(vehicle, revision);
                if (vehicle.CollisionStops != stops || !string.IsNullOrWhiteSpace(vehicle.LastRefusal))
                    throw new InvalidOperationException(phase + ": real driving refusal: " + vehicle.LastRefusal);
                var direction = (waypoints[index] - current).Normalized() * (reverse ? -1 : 1);
                var forward = new Vector2(-vehicle.GlobalBasis.Z.X, -vehicle.GlobalBasis.Z.Z).Normalized();
                var error = Mathf.AngleDifference(Mathf.Atan2(-forward.X, -forward.Y), Mathf.Atan2(-direction.X, -direction.Y));
                var steering = Mathf.Clamp(-error * 2.2f * (reverse ? -1 : 1), -1, 1);
                SetAddressAxis("move_left", "move_right", steering);
                var speed = Math.Min(reverse ? .65f : 1.6f, Math.Max(.22f, remaining * .7f));
                SetAddressAxis("move_backward", "move_forward", reverse ? -speed / vehicle.Definition.ReverseSpeed : speed / vehicle.Definition.MaxForwardSpeed);
                await Frames(1);
                var delta = (float)GetPhysicsProcessDeltaTime(); var step = vehicle.GlobalPosition.DistanceTo(previous);
                if (step > vehicle.Definition.MaxForwardSpeed * delta + .25f)
                    throw new InvalidOperationException(phase + ": discontinuous vehicle pose");
                distance += step; stalled = step < .001f ? stalled + delta : 0; previous = vehicle.GlobalPosition;
                if (stalled > 3) throw new InvalidOperationException(phase + ": bounded real driving stalled");
                if (Time.GetTicksMsec() >= nextSample)
                {
                    nextSample = Time.GetTicksMsec() + 500;
                    samples.Add(new { physicsFrame = Engine.GetPhysicsFrames(), position = AddressCoordinates(previous),
                        vehicle.Speed, waypoint = index, target = waypoints[index].ToString(), reverse,
                        vehicle.LastRefusal, vehicle.CollisionStops });
                }
            }
        }
        finally
        {
            ReleaseAddressDrivingInput(); await StopAddressVehicle(vehicle);
            _records.Add(new { kind = "driver-address-actual-route", phase, vehicle = vehicle.Definition.Id,
                reverse, start = AddressCoordinates(before), end = AddressCoordinates(vehicle.GlobalPosition), distance,
                waypoints = waypoints.Select(p => p.ToString()).ToArray(), samples,
                milliseconds = Time.GetTicksMsec() - started, fullTraversalFromNewGame = false, teleportedVehicle = false });
        }
        RequireAddressDriver(vehicle, revision);
        Require(vehicle.CollisionStops == stops && string.IsNullOrWhiteSpace(vehicle.LastRefusal)
            && new Vector2(vehicle.GlobalPosition.X, vehicle.GlobalPosition.Z).DistanceTo(waypoints[^1]) <= .65f,
            phase + ": actual controls reach and brake at the selected road stop without a rejected move");
    }

    private void RequireAddressDriver(VehicleController vehicle, int revision)
    {
        if (vehicle.Driver != _player || _fleet.Occupied != vehicle || !vehicle.VehicleCamera.Current
            || GetViewport().GetCamera3D() != vehicle.VehicleCamera || _player.ModalOpen || _fleet.Suspended
            || !vehicle.PlacementAvailable || _player.PresentationTransformRevision != revision)
            throw new InvalidOperationException("Actual address driver/camera/session ownership changed.");
    }

    private async Task StopAddressVehicle(VehicleController vehicle)
    {
        ReleaseAddressDrivingInput();
        if (vehicle.Driver != _player) return;
        try
        {
            Input.ActionPress("jump");
            for (var frames = 0; frames < 180 && Math.Abs(vehicle.Speed) >= .02f; frames++) await Frames(1);
        }
        finally { Input.ActionRelease("jump"); }
        if (!vehicle.ParkingBrake) await Press("crouch");
        await Frames(6);
        Require(Math.Abs(vehicle.Speed) < .035f && vehicle.ParkingBrake,
            vehicle.Definition.Id + ": ordinary service brake and parking brake hold the stop");
    }

    private static void ReleaseAddressDrivingInput()
    {
        foreach (var action in new[] { "move_forward", "move_backward", "move_left", "move_right", "jump" }) Input.ActionRelease(action);
    }
    private static void SetAddressAxis(string negative, string positive, float value)
    { Input.ActionRelease(value >= 0 ? negative : positive); Input.ActionPress(value >= 0 ? positive : negative, Math.Abs(value)); }

    private async Task CaptureDriverAddress(VehicleController vehicle, AddressSignVisualComponent plate, Label3D[] labels, double originalFov)
    {
        var size = DisplayServer.WindowGetSize(); var mode = DisplayServer.WindowGetMode();
        var position = DisplayServer.WindowGetPosition(); var beforeLook = vehicle.DriverLookAngles;
        try
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            foreach (var requested in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                DisplayServer.WindowSetSize(requested);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "address-window-layout");
                await AimDriverAddress(vehicle, plate.GlobalPosition + plate.GlobalBasis.Z * .03f);
                await Frames(12); // Passive view after real mouse delivery; no Interact/read API.
                var camera = vehicle.VehicleCamera; var pose = vehicle.GlobalTransform;
                Require(vehicle.Driver == _player && _player.VehicleControlled && GetViewport().GetCamera3D() == camera
                    && camera.Current && !_camera.Current && vehicle.ParkingBrake,
                    vehicle.Definition.Id + ": screenshot belongs to the actual parked driver, not a diagnostic camera");
                var passive = AddressKnowledge();
                var name = "address-" + vehicle.Definition.Id + "-" + plate.AddressId.ToLowerInvariant() + "-" + requested.Y;
                await Capture(name);
                using var image = GetViewport().GetTexture().GetImage();
                var raster = new Vector2(image.GetWidth(), image.GetHeight());
                var logical = camera.GetViewport().GetVisibleRect().Size;
                var ratio = raster / logical;
                var rim = AddressProjectedBounds(camera, plate.GlobalTransform, new Aabb(new(-.59f, -.215f, .016f), new(1.18f, .43f, .025f)), ratio);
                var projectedLabels = labels.Select(label => new { name = label.Name.ToString(), label.Text,
                    label.FontSize, label.PixelSize, rasterBounds = AddressProjectedBounds(camera, label.GlobalTransform, label.GetAabb(), ratio) }).ToArray();
                var contacts = new List<object>();
                foreach (var offset in new[] { Vector2.Zero, new Vector2(-.48f, .14f), new Vector2(.48f, .14f),
                    new Vector2(-.48f, -.14f), new Vector2(.48f, -.14f) })
                {
                    var target = plate.ToGlobal(new(offset.X, offset.Y, .027f));
                    var pixel = camera.UnprojectPosition(target); var uv = pixel / logical;
                    var visible = Act1VisibleSurfaceProbe.Nearest(_demo.DemoMain.ConnectedWorld!, camera, uv);
                    using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, target, 3);
                    ray.Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
                    var hit = camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                    var material = visible?.Mesh.MaterialOverride ?? visible?.Mesh.GetActiveMaterial(0);
                    contacts.Add(new { localSample = offset.ToString(), pixel = (pixel * ratio).ToString(),
                        physicsHasHit = hit.Count > 0, physicsOwner = hit.Count > 0 ? (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() : null,
                        physicsPoint = hit.Count > 0 ? AddressCoordinates(hit["position"].AsVector3()) : null,
                        candidateRenderOwner = visible?.Mesh.GetPath().ToString(), candidateDistance = visible?.Distance,
                        targetDistance = camera.GlobalPosition.DistanceTo(target),
                        candidateIsPlate = visible is { } value && plate.IsAncestorOf(value.Mesh),
                        candidateMaterial = material?.GetClass(),
                        transparency = material is BaseMaterial3D basic ? basic.Transparency.ToString() : "shader-or-unknown",
                        alpha = material is BaseMaterial3D basicAlpha ? (float?)basicAlpha.AlbedoColor.A : null,
                        visibilityAccepted = false });
                }
                var projectedInside = rim.All(point => point[2] > 0 && point[0] >= 0 && point[1] >= 0
                    && point[0] < raster.X && point[1] < raster.Y);
                _records.Add(new { kind = "driver-address-image-observation", vehicle = vehicle.Definition.Id, plate.AddressId,
                    path = Path.Combine(_directory, name + ".png"), camera = camera.GetPath().ToString(),
                    cameraPosition = AddressCoordinates(camera.GlobalPosition), cameraBasis = camera.GlobalBasis.ToString(),
                    vehiclePosition = AddressCoordinates(vehicle.GlobalPosition), vehicle.Speed, vehicle.ParkingBrake,
                    originalFov, actualFov = camera.Fov, requested = requested.ToString(), actualRaster = raster.ToString(),
                    logicalViewport = logical.ToString(), rim, projectedLabels, projectedInside, contacts,
                    passiveUnchanged = passive == AddressKnowledge(), vehiclePoseUnchanged = pose == vehicle.GlobalTransform,
                    readabilityAccepted = false, independentImageReviewRequired = true,
                    limit = "Raw triangle candidates include glass/cutouts and do not prove opaque pixel ownership; physics shell differs from cabin visibility. Screen inclusion is framing only, never readable-text acceptance." });
                Require(image.GetWidth() == requested.X && image.GetHeight() == requested.Y && projectedInside,
                    vehicle.Definition.Id + ": actual requested raster frames the mounted plate");
                Require(Math.Abs(camera.Fov - originalFov) < .0001 && passive == AddressKnowledge() && pose == vehicle.GlobalTransform,
                    vehicle.Definition.Id + ": capture keeps normal FOV, parked pose and passive knowledge");
            }
        }
        finally
        {
            try
            {
                if (vehicle.Driver == _player)
                {
                    var accepted = vehicle.DriverLookAngles - beforeLook;
                    using var restore = new InputEventMouseMotion { Relative = accepted / _player.MouseSensitivity,
                        ScreenRelative = accepted / _player.MouseSensitivity };
                    Input.ParseInputEvent(restore);
                    Require(await WaitVehicleMouseProjection(vehicle, false, "address-view-restore", beforeLook),
                        vehicle.Definition.Id + ": ordinary input restores the previous driver look");
                }
            }
            finally
            {
                DisplayServer.WindowSetSize(size); DisplayServer.WindowSetMode(mode); DisplayServer.WindowSetPosition(position);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "address-window-restore");
            }
        }
    }

    private async Task AimDriverAddress(VehicleController vehicle, Vector3 target)
    {
        var local = vehicle.GlobalBasis.Inverse() * (target - vehicle.VehicleCamera.GlobalPosition);
        var desired = new Vector2(Mathf.RadToDeg(Mathf.Atan2(-local.X, -local.Z)),
            Mathf.RadToDeg(Mathf.Atan2(local.Y, new Vector2(local.X, local.Z).Length())));
        Require(Math.Abs(desired.X) < 125 && desired.Y > -67 && desired.Y < 65,
            vehicle.Definition.Id + ": sign is within ordinary driver head-turn limits");
        var delta = desired - vehicle.DriverLookAngles;
        using var motion = new InputEventMouseMotion { Relative = -delta / _player.MouseSensitivity,
            ScreenRelative = -delta / _player.MouseSensitivity };
        Input.ParseInputEvent(motion);
        Require(await WaitVehicleMouseProjection(vehicle, false, "address-sign-aim", desired),
            vehicle.Definition.Id + ": ordinary mouse input publishes the requested driver view");
        Require((-vehicle.VehicleCamera.GlobalBasis.Z).Normalized().Dot((target - vehicle.VehicleCamera.GlobalPosition).Normalized()) > .9999f,
            vehicle.Definition.Id + ": actual camera axis aims at the live sign");
    }

    private static float[][] AddressProjectedBounds(Camera3D camera, Transform3D transform, Aabb bounds, Vector2 rasterRatio) =>
        Enumerable.Range(0, 8).Select(index =>
        {
            var point = transform * bounds.GetEndpoint(index); var pixel = camera.UnprojectPosition(point) * rasterRatio;
            return new[] { pixel.X, pixel.Y, camera.IsPositionBehind(point) ? -1f : 1f };
        }).ToArray();
    private static float[] AddressCoordinates(Vector3 value) => [value.X, value.Y, value.Z];
}
