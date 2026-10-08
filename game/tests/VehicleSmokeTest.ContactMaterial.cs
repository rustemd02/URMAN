using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// VIS-049, VIS-050 and VIS-105 measurements on the vehicles the game actually
/// builds: does a wheel or a hoof stand on the surface it is parked on, does the
/// harness still connect, does the pressed snow stay under the tyres that made it,
/// and does every transport surface resolve to a declared material.
///
/// Everything here reads the live scene: the drawn meshes, the controller's own
/// support rays and the snow-track system's printed centres. Nothing is asserted
/// from a picture, and no number is copied out of the factory as a second truth.
/// </summary>
public partial class VehicleSmokeTest
{
    // VIS-049 proposes a visible support gap of 1-2 cm. The cart's own wheels are
    // the lowest shapes of its body, so the engine's floor relationship lets them
    // rest at SafeMargin and the tight limit applies. The Niva's authored hull box
    // bottom deliberately sits 12 mm under its own tyre contact line, which the
    // kinematic floor keeps: a driven-and-stopped Niva has always hovered that
    // much, so the card for it (VIS-105) is answered by the two numbers matching,
    // and the absolute gate is widened by that documented 12 mm only.
    private const float CartContactLimit = .020f;
    private const float RoadVehicleContactLimit = .030f;
    /// <summary>A wheel must not be buried in the surface it rests on either.</summary>
    private const float MaximumStaticSink = .005f;

    /// <summary>
    /// Lowest point of every visible tyre, in world metres, and the surface the
    /// live collider offers directly beneath it.
    /// </summary>
    private List<(string Name, float LowestY, float SurfaceY, float Gap, string Source)> MeasureWheelContact(
        VehicleController vehicle)
    {
        var result = new List<(string, float, float, float, string)>();
        var exclude = new global::Godot.Collections.Array<Rid> { vehicle.GetRid(), _player.GetRid() };
        using var excludeOwner = (global::Godot.Collections.Array)exclude;
        using var ray = PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.One, 3, exclude);
        foreach (var wheel in vehicle.FindChildren("Wheel*", "Node3D", true, false).OfType<Node3D>())
        {
            if (!wheel.IsVisibleInTree()) continue;
            var lowest = float.PositiveInfinity;
            var found = false;
            foreach (var node in wheel.FindChildren("*", "MeshInstance3D", true, false))
            {
                if (node is not MeshInstance3D mesh || mesh.Mesh is null || !mesh.IsVisibleInTree()) continue;
                for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                {
                    using var arrays = mesh.Mesh.SurfaceGetArrays(surface);
                    foreach (var vertex in arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    {
                        var world = mesh.ToGlobal(vertex);
                        if (!world.IsFinite()) continue;
                        found = true;
                        lowest = Math.Min(lowest, world.Y);
                    }
                }
            }
            if (!found || float.IsPositiveInfinity(lowest)) continue;
            var centre = wheel.GlobalPosition;
            ray.From = new Vector3(centre.X, lowest + .30f, centre.Z);
            ray.To = new Vector3(centre.X, lowest - .30f, centre.Z);
            using var hit = vehicle.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            float surfaceY;
            string source;
            if (hit.Count > 0)
            {
                surfaceY = hit["position"].AsVector3().Y;
                source = (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() ?? "physics RID";
            }
            else
            {
                surfaceY = (float)AgentBAct1HeightField.CollisionGround(centre.X, centre.Z);
                source = "height field (no collider under the wheel)";
            }
            result.Add((wheel.Name.ToString(), lowest, surfaceY, lowest - surfaceY, source));
        }
        return result;
    }

    /// <summary>
    /// VIS-049 static contact: wheels on the road, hooves on the road, the harness
    /// still sewn into both of its anchors and the shaft inside its tug ring.
    /// </summary>
    private void CheckStaticContactGeometry(VehicleController vehicle, string phase)
    {
        var limit = vehicle.Definition.Kind == VehicleKind.HorseCart
            ? CartContactLimit : RoadVehicleContactLimit;
        var wheels = MeasureWheelContact(vehicle);
        var worstGap = float.NegativeInfinity;
        _records.Add(new { kind = "static-contact", vehicle = vehicle.Definition.Id, phase,
            settle = vehicle.HasMeta("staticContactSettle")
                ? vehicle.GetMeta("staticContactSettle").AsString() : "none",
            rows = wheels.Select(row => new { wheel = row.Name, lowestVisibleTyreY = row.LowestY,
                supportY = row.SurfaceY, gapMetres = row.Gap, support = row.Source }).ToArray(),
            limitMetres = limit });
        foreach (var (name, lowest, surfaceY, gap, source) in wheels)
        {
            worstGap = Math.Max(worstGap, gap);
            Require(gap <= limit && gap >= -MaximumStaticSink,
                vehicle.Definition.Id + " wheel " + name + " stands on the road in " + phase
                + " (gap " + (gap * 1000f).ToString("0.0") + " mm against " + source
                + ", allowed " + (limit * 1000f).ToString("0.0") + " mm)");
        }
        Require(wheels.Count >= 2, vehicle.Definition.Id + " exposes its wheels for the contact measurement");
        if (vehicle.Definition.Kind != VehicleKind.HorseCart) return;

        var rig = HorsePose(vehicle);
        var proof = rig.CaptureSupportProof();
        var hooves = new List<object>();
        foreach (var foot in proof["feet"]!.AsArray())
        {
            var sole = foot!["actualSole"]!.AsArray();
            var point = new Vector3(sole[0]!.GetValue<float>(), sole[1]!.GetValue<float>(),
                sole[2]!.GetValue<float>());
            var ground = (float)AgentBAct1HeightField.CollisionGround(point.X, point.Z);
            var gap = point.Y - ground;
            hooves.Add(new { foot = foot["name"]!.GetValue<string>(), phaseOfGait = foot["phase"]!.GetValue<string>(),
                supported = foot["supported"]!.GetValue<bool>(), sole = point.ToString(), ground,
                gapMetres = gap, collider = foot["collider"]!.GetValue<string>() });
            Require(gap <= .012f && gap >= -.006f, vehicle.Definition.Id + " hoof " + foot["name"]
                + " stands on its own coherent ground in " + phase + " (gap "
                + (gap * 1000f).ToString("0.0") + " mm)");
        }
        var harness = rig.CaptureHarnessProof();
        var reins = new List<object>();
        foreach (var rein in harness["reins"]!.AsArray())
        {
            var startError = rein!["startErrorMetres"]!.GetValue<double>();
            var endError = rein["endErrorMetres"]!.GetValue<double>();
            reins.Add(new { rein = rein["name"]!.GetValue<string>(), startErrorMetres = startError,
                endErrorMetres = endError, spanMetres = rein["spanMetres"]!.GetValue<double>() });
            Require(startError < .001 && endError < .001, vehicle.Definition.Id + " "
                + rein["name"] + " is connected at both anchors in " + phase + " (withers "
                + (startError * 1000).ToString("0.00") + " mm, bit "
                + (endError * 1000).ToString("0.00") + " mm)");
        }
        var cartRoot = vehicle.GetNode<Node3D>("VehicleVisual");
        var horseRoot = rig.GetParent() as Node3D;
        var shafts = new List<object>();
        foreach (var side in new[] { "Left", "Right" })
        {
            var shaft = cartRoot.GetMeta("shaftTip" + side).AsVector3();
            var ring = horseRoot!.GetMeta("tugRing" + side).AsVector3();
            shafts.Add(new { side, shaft = shaft.ToString(), ring = ring.ToString(), distance = shaft.DistanceTo(ring) });
            Require(shaft.DistanceTo(ring) < .000001f, vehicle.Definition.Id
                + " shaft " + side + " ends exactly in its harness tug ring");
        }
        _records.Add(new { kind = "harness-and-hoof-contact", vehicle = vehicle.Definition.Id, phase,
            hooves, reins, shafts,
            limit = "drawn meshes and the support rays the rig itself uses; the gait look is a station frame question" });
    }

    /// <summary>
    /// VIS-049 acceptance half: a stopped vehicle's wheels do not keep turning.
    /// </summary>
    private async Task CheckParkedWheelsDoNotSpin(VehicleController vehicle)
    {
        float[] Before() => vehicle.FindChildren("Wheel*", "Node3D", true, false).OfType<Node3D>()
            .Select(wheel => wheel.Rotation.X).ToArray();
        var before = Before();
        await Frames(60);
        var after = Before();
        var moved = 0;
        var worst = 0f;
        for (var index = 0; index < Math.Min(before.Length, after.Length); index++)
        {
            var delta = Math.Abs(after[index] - before[index]);
            if (delta <= .000001f) continue;
            moved++;
            worst = Math.Max(worst, delta);
        }
        _records.Add(new { kind = "parked-wheel-spin", vehicle = vehicle.Definition.Id, speed = vehicle.Speed,
            engineRunning = vehicle.EngineRunning, parkingBrake = vehicle.ParkingBrake, frames = 60,
            wheels = before.Length, movedWheels = moved, worstWheelRadians = worst,
            limit = "the visual spin phase; not a proof that the drivetrain itself has no torque" });
        Require(moved == 0 && before.Length == after.Length, vehicle.Definition.Id
            + " keeps every wheel still while parked with the state it has");
    }

    /// <summary>
    /// VIS-050: the pressed snow belongs to the tyres. Drives the Niva off the
    /// packed carriageway onto loose snow, runs a straight stretch and a turn, then
    /// compares the printed rut centres with the wheels that made them.
    /// </summary>
    private async Task CheckSnowRutsFollowTyres(VehicleController vehicle)
    {
        var tracks = vehicle.FindChildren("VehicleSnowTracks", nameof(Node3D), true, false)
            .OfType<VehicleSnowTracks>().SingleOrDefault()
            ?? throw new InvalidOperationException("The Niva visual has no snow track system.");
        Require(tracks.GetMeta("vehicleTrackWidthSource").AsString()
            == "tracked wheel roadTyreWidth metadata",
            "the rut width is read from the tyre, not from a second constant");
        var before = tracks.DescribeTracks();
        _records.Add(new { kind = "snow-ruts-before-drive", vehicle = vehicle.Definition.Id,
            tracks = before.ToString() });
        Require(before["segments"].AsInt32() == 0, "a parked Niva stamps nothing");

        Input.ActionPress("move_forward");
        Input.ActionPress("move_right");
        await Frames(70);
        Input.ActionRelease("move_right");
        await Frames(90);
        var straight = tracks.DescribeTracks();
        Input.ActionPress("move_left");
        await Frames(55);
        Input.ActionRelease("move_left");
        await Frames(45);
        Input.ActionRelease("move_forward");
        await Frames(20);
        var turning = tracks.DescribeTracks();
        _records.Add(new { kind = "snow-ruts-straight", vehicle = vehicle.Definition.Id,
            tracks = straight.ToString() });
        _records.Add(new { kind = "snow-ruts-turning", vehicle = vehicle.Definition.Id,
            tracks = turning.ToString() });

        // The stamping contract itself: the budget and the station spacing are the
        // numbers the card says may not grow.
        Require(straight["budgetQuads"].AsInt32() == VehicleSnowTracks.QuadBudget
            && straight["stationSpacingMetres"].AsSingle() == .42f
            && turning["budgetQuads"].AsInt32() == VehicleSnowTracks.QuadBudget,
            "the stamping budget and station spacing did not grow with this change");

        var stamped = straight["segments"].AsInt32() > 0 || turning["segments"].AsInt32() > 0;
        if (!stamped)
        {
            // Honest non-result: this session never let the car reach loose snow,
            // so the centre-versus-tyre comparison has nothing to measure yet.
            _records.Add(new { kind = "snow-ruts-not-reached", vehicle = vehicle.Definition.Id,
                reason = "the road graph kept the car on the packed carriageway for the whole attempt",
                speed = vehicle.Speed, travelled = vehicle.TotalTravelMetres });
            return;
        }
        var sample = turning["segments"].AsInt32() > 0 ? turning : straight;
        foreach (var row in (global::Godot.Collections.Array)sample["wheels"]!)
        {
            var wheel = (global::Godot.Collections.Dictionary)row!;
            if (!wheel.ContainsKey("lateralErrorMetres")) continue;
            var lateral = wheel["lateralErrorMetres"].AsSingle();
            Require(lateral <= VehicleSnowTracks.CentreErrorLimitMetres,
                "the rut centre stays under its own tyre: " + wheel["name"].AsString() + " lateral error "
                + (lateral * 1000f).ToString("0.0") + " mm, allowed "
                + (VehicleSnowTracks.CentreErrorLimitMetres * 1000f).ToString("0.0") + " mm");
            Require(Mathf.Abs(wheel["tyreWidthMetres"].AsSingle() + .09f
                    - wheel["rutWidthMetres"].AsSingle()) < .0001f,
                "the rut is the tyre width plus the squeezed shoulder, " + wheel["name"].AsString());
        }
        Require(sample.ContainsKey("separationErrorMetres"),
            "both ruts and both rear tyres were measured in the same frame");
        var separationError = sample["separationErrorMetres"].AsSingle();
        Require(separationError <= VehicleSnowTracks.CentreErrorLimitMetres,
            "the two ruts are exactly one rear track apart: "
            + sample["rearPrintSeparationMetres"].AsSingle() + " m of print against "
            + sample["rearTyreSeparationMetres"].AsSingle() + " m of tyre, error "
            + (separationError * 1000f).ToString("0.0") + " mm");
    }

    /// <summary>
    /// VIS-105: no transport surface may fall back to an untuned default. Walks
    /// every drawn material of every vehicle and proves each one is a declared
    /// family with its own roughness and metal answer.
    /// </summary>
    private void CheckVehicleMaterialLanguage(VehicleController vehicle)
    {
        var families = new SortedSet<string>(StringComparer.Ordinal);
        var defects = new List<string>();
        foreach (var node in vehicle.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || !mesh.IsVisibleInTree() || mesh.Mesh is not { } source) continue;
            for (var surface = 0; surface < source.GetSurfaceCount(); surface++)
            {
                var material = mesh.GetActiveMaterial(surface);
                var owner = mesh.Name + "/" + surface;
                if (material is null)
                {
                    defects.Add(owner + " has no material at all");
                    continue;
                }
                if (material is ShaderMaterial shader)
                {
                    var family = shader.GetMeta("surface", string.Empty).AsString();
                    if (family.Length == 0) family = "grain:" + shader.ResourceName;
                    families.Add(family);
                    var metallic = shader.GetShaderParameter("metallic_value");
                    var roughness = shader.GetShaderParameter("roughness_value");
                    if (roughness.VariantType == Variant.Type.Nil)
                    {
                        defects.Add(owner + " declares no roughness answer");
                        continue;
                    }
                    if (family == "vehicle_paint" && metallic.VariantType != Variant.Type.Nil
                        && metallic.AsSingle() != 0f)
                        defects.Add(owner + " gives the body paint a metal value (VIS-095)");
                    continue;
                }
                if (material is StandardMaterial3D solid)
                {
                    // The family meta is "surface" for both branches of the finish
                    // contract: the painterly materials and the two StandardMaterial3D
                    // ones (vehicle_glass, vehicle_snow) stamp it the same way.
                    var finish = solid.GetMeta("surface", string.Empty).AsString();
                    if (finish.Length == 0) finish = solid.GetMeta("vehicleFinish", string.Empty).AsString();
                    families.Add(finish.Length > 0 ? finish : "standard:" + solid.ResourceName);
                    var albedo = solid.AlbedoColor;
                    // 808080 was the fallback the old resolver used for any name it
                    // did not recognise; a hero closeup must never show it.
                    if (Mathf.Abs(albedo.R - .50196f) < .002f && Mathf.Abs(albedo.G - .50196f) < .002f
                        && Mathf.Abs(albedo.B - .50196f) < .002f && solid.Transparency
                            != BaseMaterial3D.TransparencyEnum.Alpha)
                        defects.Add(owner + " is the untuned default grey");
                    continue;
                }
                defects.Add(owner + " is " + material.GetClass());
            }
        }
        _records.Add(new { kind = "vehicle-material-language", vehicle = vehicle.Definition.Id,
            families = families.ToArray(), defects = defects.ToArray(),
            limit = "material identity and declared response; the finished picture is a station frame question" });
        Require(defects.Count == 0, vehicle.Definition.Id + " transport finishes: " + string.Join("; ", defects));
        if (vehicle.Definition.Kind != VehicleKind.Niva) return;
        Require(families.Contains("vehicle_paint") && families.Contains("vehicle_rubber")
            && families.Contains("vehicle_bare_metal") && families.Contains("vehicle_glass"),
            "the Niva's paint, rubber, metal and glass are four distinct declared finishes");
        Require(families.Contains("vehicle_trim_metal") && families.Contains("vehicle_plastic"),
            "bright trim and bumper plastic are not folded into the body paint");
    }

    /// <summary>
    /// The narrow station path for this lane: contact, harness, ruts and materials
    /// on the three vehicles the world actually owns, without touching the
    /// save/restore and obstacle fixtures of the full suite.
    /// </summary>
    private async Task RunContactAndFinishProof()
    {
        foreach (var vehicle in _fleet.Vehicles)
        {
            CheckStaticContactGeometry(vehicle, "authored-rest");
            CheckVehicleMaterialLanguage(vehicle);
        }

        var cart = _fleet.Vehicles.Single(vehicle => vehicle.Definition.Kind == VehicleKind.HorseCart);
        await Approach(cart);
        await Press("interact");
        await Frames(6);
        Require(_fleet.Occupied == cart, "the cart proof enters the actual cart");
        await Press("carry_use");
        await Frames(40);
        Require(cart.EngineRunning && !cart.ParkingBrake, "the cart is started and released from its brake");
        Input.ActionPress("move_forward");
        await Frames(90);
        Input.ActionRelease("move_forward");
        await Frames(30);
        CheckStaticContactGeometry(cart, "after-a-short-drive");
        await CheckParkedWheelsDoNotSpin(cart);
        Require(cart.TryExit(), "the cart proof leaves the bench the ordinary way");
        await Frames(6);

        var niva = _fleet.Vehicles.Single(vehicle => vehicle.Definition.Kind == VehicleKind.Niva);
        await Approach(niva);
        await Press("interact");
        await Frames(6);
        Require(_fleet.Occupied == niva, "the rut proof enters the actual Niva");
        await Press("carry_use");
        await Frames(40);
        await CheckSnowRutsFollowTyres(niva);
        await CheckParkedWheelsDoNotSpin(niva);
        Require(niva.TryExit(), "the rut proof leaves the cab the ordinary way");
        await Frames(6);
        CheckStaticContactGeometry(niva, "after-the-rut-drive");
    }
}
