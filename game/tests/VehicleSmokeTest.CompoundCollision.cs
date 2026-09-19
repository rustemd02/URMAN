using Godot;
using System.Text.Json.Nodes;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private void CheckCompoundModelEnvelope(VehicleController vehicle, string phase)
    {
        if (vehicle.Definition.Kind == VehicleKind.Niva) return;
        var count = 0; var outside = new JsonArray(); var conformingHoofs = 0;
        var hoofVertices = 0; var hoofOutsideVertices = 0; var maximumBelowFixedSole = 0f;
        var hoofOutsideSamples = new JsonArray();
        foreach (var mesh in vehicle.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var hoof = mesh.Name == "HoofWallAndSole";
            if (hoof) conformingHoofs++;
            var geometry = mesh.Mesh ?? throw new InvalidOperationException("A visible vehicle mesh is missing.");
            var transform = vehicle.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
            for (var surface = 0; surface < geometry.GetSurfaceCount(); surface++)
            foreach (var vertex in geometry.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
            {
                var point = transform * vertex;
                var contained = vehicle.PhysicalEnvelopeContains(point, vehicle.SteeringRadians);
                if (hoof)
                {
                    hoofVertices++;
                    if (!contained)
                    {
                        hoofOutsideVertices++;
                        maximumBelowFixedSole = Math.Max(maximumBelowFixedSole, Math.Max(0, -point.Y));
                        if (hoofOutsideSamples.Count < 24) hoofOutsideSamples.Add(new JsonObject {
                            ["mesh"] = mesh.GetPath().ToString(), ["local"] = new JsonArray(point.X, point.Y, point.Z),
                            ["world"] = (mesh.GlobalTransform * vertex).ToString() });
                    }
                    continue;
                }
                count++;
                if (!contained && outside.Count < 24)
                    outside.Add(new JsonObject { ["mesh"] = mesh.GetPath().ToString(),
                        ["point"] = new JsonArray(point.X, point.Y, point.Z) });
            }
        }
        var physical = vehicle.DescribeCompoundCollision();
        _records.Add(new { kind = "actual-compound-mesh-containment", phase, vehicle = vehicle.Definition.Id,
            checkedVertices = count, outside, conformingHoofs, physical,
            actualHoofDiagnostic = new { hoofVertices, hoofOutsideVertices, maximumBelowFixedSoleMetres = maximumBelowFixedSole,
                samples = hoofOutsideSamples, fullContainmentAccepted = false, currentQueryContainmentAccepted = hoofOutsideVertices == 0 },
            limit = "One native body supports upper geometry and wheels; four actual current hoof queries constrain articulation and movement. Query containment does not claim native body-RID containment. Full-sole support and actual obstacle approaches remain separate checks." });
        Require(count > 100 && outside.Count == 0, vehicle.Definition.Id + " actual non-hoof mesh lies in the physical union at " + phase);
        Require(vehicle.Definition.Kind != VehicleKind.HorseCart || conformingHoofs == 4,
            "only four articulated hoof meshes are checked by their existing support proof");
        Require(hoofOutsideVertices == 0, "all actual current hoof vertices lie in their articulated query volumes");
        var shapes = vehicle.GetChildren().OfType<CollisionShape3D>().Count(node => !node.Disabled);
        Require(shapes == physical["nativeBodyShapeCount"]!.GetValue<int>()
            && shapes + physical["hoofQueryCount"]!.GetValue<int>() == physical["volumes"]!.AsArray().Count,
            "every native shape and each declared articulated hoof query participates in the query union");
        Require(physical["requiredGroups"]!.GetValue<int>() == physical["totalGroups"]!.GetValue<int>()
            && physical["totalGroups"]!.GetValue<int>() == (vehicle.Definition.Kind == VehicleKind.Motorcycle ? 2 : 8),
            "every real motorcycle wheel or cart-wheel/hoof support group is required");
        foreach (var group in physical["supports"]!.AsArray())
        {
            var entry = group ?? throw new InvalidOperationException("A physical support group is missing.");
            var name = entry["group"]!.GetValue<string>();
            var coordinates = entry["local"]!.AsArray();
            var point = new Vector3(coordinates[0]!.GetValue<float>(), coordinates[1]!.GetValue<float>(), coordinates[2]!.GetValue<float>());
            if (name.Contains("Wheel", StringComparison.Ordinal))
            {
                var node = vehicle.GetNode<CollisionShape3D>(name);
                var actualBottom = ((ConvexPolygonShape3D)node.Shape!).Points.Min(vertex => (node.Transform * vertex).Y);
                Require(Math.Abs(point.Y - actualBottom) <= .000002f,
                    "support point follows the actual applied convex bottom: " + name);
            }
            else if (name.Contains("hoof", StringComparison.Ordinal))
            {
                var legIndex = int.Parse(name[(name.LastIndexOf('-') + 1)..]);
                var actualHoof = vehicle.FindChild("HorseLeg" + legIndex, true, false)!
                    .FindChild("Hoof", true, false) as Node3D ?? throw new InvalidOperationException("The actual hoof binding is missing.");
                for (var index = 0; index < VehicleHorsePose.SoleSegments; index++)
                {
                    var angle = Mathf.Tau * index / VehicleHorsePose.SoleSegments;
                    var sole = vehicle.ToLocal(actualHoof.ToGlobal(new Vector3(Mathf.Cos(angle) * VehicleHorsePose.SoleHalfWidth,
                        -VehicleHorsePose.SoleDepth, Mathf.Sin(angle) * VehicleHorsePose.SoleHalfLength)));
                    Require(vehicle.PhysicalEnvelopeContains(sole, vehicle.SteeringRadians),
                        "current articulated query covers every actual supported sole vertex: " + name + "/" + index);
                }
            }
        }
    }

    private async Task RunCompoundOnlyChecks()
    {
        foreach (var vehicle in _fleet.Vehicles.Where(vehicle => vehicle.Definition.Kind != VehicleKind.Niva))
        {
            CheckCompoundModelEnvelope(vehicle, "authored-parking");
            await Approach(vehicle); await Press("interact"); await Frames(4);
            Require(vehicle.Driver == _player, "compound proof uses ordinary ray entry");
            if (vehicle.Definition.Kind == VehicleKind.Motorcycle)
            {
                foreach (var action in new[] { "move_left", "move_right" })
                {
                    Input.ActionPress(action); await Frames(45); Input.ActionRelease(action);
                    CheckCompoundModelEnvelope(vehicle, action);
                    Require(Math.Abs(vehicle.SteeringRadians) > Mathf.DegToRad(28),
                        "the motorcycle reaches its real full steering lock with its complete physical union");
                }
                await Frames(45);
            }
            await Press("carry_use"); await Frames(40);
            Require(vehicle.EngineRunning && !vehicle.ParkingBrake, "compound proof uses ordinary ignition/horse action");
            await CompoundGroundPartBlocksPost(vehicle);
            if (vehicle.Definition.Kind == VehicleKind.HorseCart)
            {
                await CompoundGroundPartBlocksPost(vehicle, reverse: true);
                await CompoundGroundPartBlocksPost(vehicle, side: 1);
            }
            if (vehicle.Definition.Kind == VehicleKind.Motorcycle) await CompoundBankChangeMeetsActualPost(vehicle);
            if (!vehicle.ParkingBrake) await Press("crouch"); await Frames(35);
            Require(vehicle.ParkingBrake && Math.Abs(vehicle.Speed) < .01f, "compound vehicle parks through the ordinary brake");
            CheckCompoundModelEnvelope(vehicle, "parked-after-post");
            if (vehicle.Definition.Kind == VehicleKind.HorseCart) await CheckHorseRest(vehicle);
            var slot = "compound-" + vehicle.Definition.Id;
            Require(await _bridge.SaveSlotAsync(slot), "compound vehicle saves through the existing snapshot");
            var saved = vehicle.Capture().DeepClone();
            Require(vehicle.TryExit(), "compound vehicle has an ordinary safe exit");
            await Frames(4);
            Require(await _bridge.LoadSlotAsync(slot), "compound occupied save restores through the existing snapshot");
            await Frames(6);
            Require(JsonNode.DeepEquals(saved, vehicle.Capture()) && vehicle.Driver == _player,
                "compound pose and complete state round-trip without a second owner or repair");
            Require(vehicle.ValidatePhysicalPlacement(out _), "restored compound union and all support groups remain physically valid");
            CheckCompoundModelEnvelope(vehicle, "after-load");
            if (vehicle.Definition.Kind == VehicleKind.HorseCart) await CompoundChangedSavedHoof(vehicle, slot, saved);
            Require(vehicle.TryExit(), "loaded compound vehicle returns to a clear standing capsule");
            await Frames(4);
        }
    }

    private async Task CompoundChangedSavedHoof(VehicleController vehicle, string slot, JsonNode saved)
    {
        var savedPose = vehicle.GlobalTransform;
        var hoof = (Node3D)vehicle.FindChild("HorseLeg0", true, false)!.FindChild("Hoof", true, false)!;
        var blockedSole = hoof.ToGlobal(Vector3.Down * VehicleHorsePose.SoleDepth);
        var store = new AtomicSaveGameStore(ProjectSettings.GlobalizePath("user://savegames"));
        var files = new[] { store.SlotPath(slot), store.BackupPath(slot), "user://settings.json" };
        var hashes = files.ToDictionary(path => path, FileDigest);
        StaticBody3D? post = null;
        try
        {
            // Leave the saved hoof site through ordinary input before inserting
            // the new obstacle. The current live animal is never spawned into it.
            if (vehicle.ParkingBrake) await Press("crouch");
            if (!vehicle.EngineRunning) { await Press("carry_use"); await Frames(40); }
            Input.ActionPress("move_backward");
            for (var frame = 0; frame < 240 && vehicle.GlobalPosition.DistanceTo(savedPose.Origin) < 1.2f; frame++) await Frames(1);
            Input.ActionRelease("move_backward"); Input.ActionPress("jump"); await Frames(35); Input.ActionRelease("jump");
            if (!vehicle.ParkingBrake) await Press("crouch"); await Frames(25);
            Require(vehicle.GlobalPosition.DistanceTo(savedPose.Origin) >= 1.2f && vehicle.ValidatePhysicalPlacement(out _),
                "the horse leaves the saved hoof site through an actually supported reverse");
            post = new StaticBody3D { Name = "ChangedSavedHoofPost", CollisionLayer = 1, CollisionMask = 0 };
            post.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(.04f, .20f, .04f) } });
            AddChild(post); post.GlobalTransform = new(savedPose.Basis, blockedSole + Vector3.Up * .10f);
            await Frames(3);
            var path = post.GetPath().ToString();
            var savedHits = WheelContacts(vehicle, savedPose, 0, path).ToArray();
            Require(savedHits.Length != 0 && savedHits.All(hit => hit["vehicleShape"]!.GetValue<string>()
                    .StartsWith("HoofContactQuery", StringComparison.Ordinal)),
                "the changed saved obstacle occupies the actual restored hoof only, clear of upper body and cart wheels");
            Require(!WheelContacts(vehicle, vehicle.GlobalTransform, vehicle.SteeringRadians, path).Any(),
                "the previous live pose is clear before the obstructed saved projection");
            Require(await _bridge.LoadSlotAsync(slot), "the existing parking owner handles a newly obstructed saved hoof");
            await Frames(6);
            Require(vehicle.Driver == _player && vehicle.PlacementAvailable && vehicle.ValidatePhysicalPlacement(out _)
                && vehicle.GlobalPosition.DistanceTo(savedPose.Origin) > .25f
                && !WheelContacts(vehicle, vehicle.GlobalTransform, vehicle.SteeringRadians, path).Any(),
                "hoof rejection selects and publishes a supported clear parking before restoring possession");
            CheckCompoundModelEnvelope(vehicle, "changed-saved-hoof-recovery");
            _records.Add(new { kind = "changed-saved-hoof-recovery", savedPose = savedPose.ToString(),
                post = path, blockedSole = blockedSole.ToString(), savedHits,
                actual = vehicle.Capture(), physical = vehicle.DescribeCompoundCollision(),
                support = HorsePose(vehicle).CaptureSupportProof(),
                files = files.Select(path => new { path, beforeSha256 = hashes[path], afterSha256 = FileDigest(path) }).ToArray() });
            Require(files.All(path => FileDigest(path) == hashes[path]), "hoof recovery never rewrites the source slot, backup or profile");
        }
        finally
        {
            Input.ActionRelease("move_backward"); Input.ActionRelease("jump");
            if (post is not null) post.QueueFree(); await Frames(3);
        }
        Require(await _bridge.LoadSlotAsync(slot), "the same unmodified occupied save loads after removal of the hoof obstacle");
        await Frames(6);
        Require(vehicle.Driver == _player && JsonNode.DeepEquals(saved, vehicle.Capture())
            && vehicle.ValidatePhysicalPlacement(out _) && files.All(path => FileDigest(path) == hashes[path]),
            "removing the obstacle restores exact saved vehicle state and a freshly checked four-foot pose");
        CheckCompoundModelEnvelope(vehicle, "changed-saved-hoof-positive-pair");
    }

    private async Task CompoundBankChangeMeetsActualPost(VehicleController vehicle)
    {
        StaticBody3D? post = null;
        PauseMenuUi? pause = null;
        try
        {
            Input.ActionPress("move_right"); await Frames(45);
            var steering = vehicle.SteeringRadians;
            Require(Math.Abs(steering - Mathf.DegToRad(vehicle.Definition.SteeringDegrees)) < .0001f,
                "bank proof holds an ordinarily attained full steering angle");
            Input.ActionPress("move_forward");
            for (var frame = 0; frame < 45; frame++)
            {
                await Frames(1);
                if (vehicle.Capture()["leanRadians"]!.GetValue<float>() >= .016f) break;
            }
            var lean = vehicle.Capture()["leanRadians"]!.GetValue<float>();
            Require(lean > .010f && lean < .035f && vehicle.Speed > .1f,
                "bank proof reaches a real partial lean through acceleration");
            var nextSpeed = Math.Max(0, vehicle.Speed - vehicle.Definition.BrakeDeceleration / Engine.PhysicsTicksPerSecond);
            var predictedLean = Mathf.Clamp(steering * nextSpeed / vehicle.Definition.MaxForwardSpeed * .4f, -.04f, .04f);
            Require(predictedLean < lean, "ordinary next-frame braking requests a smaller bank at the same steering angle");
            var targetLean = new Transform3D(Basis.FromEuler(new(0, 0, predictedLean)),
                new(Mathf.Sin(predictedLean) * .8f, (1 - Mathf.Cos(predictedLean)) * .8f, 0));
            var rear = vehicle.GetNode<CollisionShape3D>("RearWheelCollision");
            var rearShape = (ConvexPolygonShape3D)rear.Shape!;
            // Select only a physical fixture. The vehicle's pose, speed and
            // accepted bank are never set by this diagnostic.
            var rearCentre = vehicle.FindChildren("*", "Node3D", true, false).OfType<Node3D>()
                .Single(node => node.HasMeta("roadTyreRadius") && node.Position.Z > 0).Position;
            var box = new BoxShape3D();
            post = new StaticBody3D { Name = "MotorcycleBankBrakePost", CollisionLayer = 1, CollisionMask = 0 };
            post.AddChild(new CollisionShape3D { Shape = box }); AddChild(post);
            var path = post.GetPath().ToString(); var selected = false;
            bool Hits(float bank) => vehicle.DescribePhysicalVolumeContacts(vehicle.GlobalTransform, steering, bank)
                ["contacts"]!.AsArray().Any(hit => hit?["colliderPath"]?.GetValue<string>() == path);
            foreach (var vertex in rearShape.Points.Where(point => point.X < 0).OrderBy(point => point.Y))
            {
                var point = targetLean * (rearCentre + vertex);
                if (point.Y < .035f || point.Y > .17f) continue;
                point.X -= .0097f;
                var world = vehicle.ToGlobal(point);
                using var ray = PhysicsRayQueryParameters3D.Create(world + Vector3.Up, world - Vector3.Up, 3);
                ray.Exclude = new global::Godot.Collections.Array<Rid> { vehicle.GetRid(), vehicle.EntryTarget.GetRid(), _player.GetRid(), post.GetRid() };
                var hit = vehicle.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                if (hit.Count == 0) continue;
                var floor = hit["position"].AsVector3().Y; var top = world.Y + .003f;
                if (top - floor <= .02f || top - floor > .22f) continue;
                box.Size = new(.020f, top - floor, .025f); world.Y = (top + floor) * .5f;
                post.GlobalTransform = new(vehicle.GlobalBasis, world); post.ForceUpdateTransform();
                if (!Hits(lean) && Hits(predictedLean)) { selected = true; break; }
            }
            _records.Add(new { kind = "speed-driven-bank-fixture", selected, speed = vehicle.Speed, steering, lean,
                predictedLean, nextSpeed, post = path, pose = post.GlobalTransform.ToString(), size = box.Size.ToString(),
                actualBefore = vehicle.DescribePhysicalVolumeContacts(vehicle.GlobalTransform, steering),
                predicted = vehicle.DescribePhysicalVolumeContacts(vehicle.GlobalTransform, steering, predictedLean) });
            Require(selected, "a real low post is clear at the accepted bank and obstructs the next braking bank only");
            var stops = vehicle.SteeringContactStops; var witnessed = false;
            Input.ActionRelease("move_forward"); Input.ActionPress("jump");
            for (var frame = 0; frame < 5; frame++)
            {
                await Frames(1);
                var state = vehicle.DescribeSteeringCollision();
                var atPost = state["lastContact"]?["colliderPath"]?.GetValue<string>() == path;
                if (vehicle.SteeringContactStops > stops && atPost
                    && state["lastRejectedAngle"]!.GetValue<float>() == steering
                    && state["lastRejectedLean"]!.GetValue<float>() != state["lastAcceptedLeanBeforeContact"]!.GetValue<float>())
                    witnessed = true;
                Require(!Hits(vehicle.Capture()["leanRadians"]!.GetValue<float>()),
                    "the actually applied bank never penetrates the braking post");
                _records.Add(new { kind = "actual-speed-driven-bank-contact", frame, witnessed, speed = vehicle.Speed, state });
                if (witnessed) break;
            }
            Require(witnessed, "same-angle ordinary braking is refused only for the changed physical bank");
            Input.ActionRelease("jump");
            pause = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi
                ?? throw new InvalidOperationException("The ordinary pause menu is unavailable.");
            pause.Open(); await Frames(3, allowPause: true);
            Require(vehicle.Capture()["leanRadians"]!.GetValue<float>() > .001f,
                "the ordinary pause retains a genuinely banked attainable pose for saving");
            const string slot = "compound-banked-motorcycle";
            Require(await _bridge.SaveSlotAsync(slot), "ordinary snapshot stores the attained bank");
            var saved = vehicle.Capture().DeepClone();
            Require(await _bridge.LoadSlotAsync(slot), "ordinary load checks the same banked physical union");
            await Frames(3, allowPause: true);
            Require(JsonNode.DeepEquals(saved, vehicle.Capture()) && vehicle.Driver == _player,
                "held steering preserves the complete saved bank before neutral release");
            Require(vehicle.ValidatePhysicalPlacement(out _), "banked load uses actual transformed tyre support points");
            CheckCompoundModelEnvelope(vehicle, "banked-save-load");
            pause.Resume(); await Frames(3);
        }
        finally
        {
            foreach (var action in new[] { "move_forward", "move_backward", "move_left", "move_right", "jump" }) Input.ActionRelease(action);
            if (pause is { IsOpen: true }) pause.Resume();
            if (post is not null && GodotObject.IsInstanceValid(post)) post.QueueFree();
            await Frames(40);
        }
    }

    private async Task CompoundGroundPartBlocksPost(VehicleController vehicle, bool reverse = false, int side = 0)
    {
        var horse = vehicle.Definition.Kind == VehicleKind.HorseCart;
        var local = horse ? new Vector3(-.225f, 0, -2.86f) : new Vector3(0, 0, -1.27f);
        var point = vehicle.ToGlobal(local);
        if (horse && (reverse || side != 0))
        {
            var leg = vehicle.FindChild(reverse ? "HorseLeg1" : "HorseLeg2", true, false)!
                .FindChild("Hoof", true, false) as Node3D ?? throw new InvalidOperationException("The actual hoof binding is missing.");
            point = leg.ToGlobal(Vector3.Down * VehicleHorsePose.SoleDepth)
                + (reverse ? vehicle.GlobalBasis.Z * .44f : vehicle.GlobalBasis.X * .18f);
        }
        using var ray = PhysicsRayQueryParameters3D.Create(point + Vector3.Up * 2, point - Vector3.Up * 2, 3);
        ray.Exclude = new global::Godot.Collections.Array<Rid> { vehicle.GetRid(), vehicle.EntryTarget.GetRid(), _player.GetRid() };
        var ground = vehicle.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        Require(ground.Count != 0, "compound low-post fixture has a real ground owner");
        point.Y = ground["position"].AsVector3().Y + .10f;
        var post = new StaticBody3D { Name = "CompoundGroundContactPost", CollisionLayer = 1, CollisionMask = 0 };
        var size = new Vector3(.04f, .20f, .04f);
        post.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        post.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new(1, .35f, .03f) } });
        AddChild(post); post.GlobalTransform = new(vehicle.GlobalBasis, point);
        var path = post.GetPath().ToString();
        try
        {
            await Frames(3);
            Require(!WheelContacts(vehicle, vehicle.GlobalTransform, vehicle.SteeringRadians, path).Any(),
                "the low post starts clear of every actual physical volume");
            var start = vehicle.GlobalPosition;
            var hitPost = false; string? contactedShape = null;
            var driveAction = reverse ? "move_backward" : "move_forward";
            var escapeAction = reverse ? "move_forward" : "move_backward";
            var contactStart = vehicle.CollisionStops;
            Input.ActionPress(driveAction);
            if (side != 0) Input.ActionPress("move_right");
            for (var frame = 0; frame < 180; frame++)
            {
                await Frames(1);
                if(frame%8==0)CheckCompoundModelEnvelope(vehicle, "low-post-motion-" + frame);
                if (horse && vehicle.CollisionStops > contactStart && vehicle.LastHoofContact is {} hoofContact
                    && hoofContact["colliderPath"]?.GetValue<string>() == path)
                {
                    hitPost = true; contactedShape = hoofContact["vehicleShape"]?.GetValue<string>();
                    var hoofAtContact = CompoundHorseAtPost(vehicle, post);
                    _records.Add(new { kind = "actual-articulated-hoof-post-contact", vehicle = vehicle.Definition.Id,
                        reverse, side, contactedShape, contact = hoofContact, movement = vehicle.GlobalPosition.DistanceTo(start),
                        physicsFrame = Engine.GetPhysicsFrames(), physical = vehicle.DescribeCompoundCollision(),
                        horseAtContact = hoofAtContact, nativeHoofBodyContact = false });
                    Require(hoofAtContact["minimumActualMeshGapMetres"]!.GetValue<float>() < .025f,
                        "articulated contact occurs within 25 mm of the actual hoof surface, not the old half-metre stride proxy");
                }
                for (var slide = 0; slide < vehicle.GetSlideCollisionCount(); slide++)
                {
                    var hit = vehicle.GetSlideCollision(slide);
                    for (var contact = 0; contact < hit.GetCollisionCount(); contact++)
                        if (hit.GetCollider(contact) == post)
                        {
                            hitPost = true;
                            contactedShape = (hit.GetLocalShape(contact) as Node)?.Name.ToString();
                            _records.Add(new { kind = "actual-compound-post-contact", vehicle = vehicle.Definition.Id,
                                contactedShape, point = hit.GetPosition(contact).ToString(), normal = hit.GetNormal(contact).ToString(),
                                movement = vehicle.GlobalPosition.DistanceTo(start), physicsFrame = Engine.GetPhysicsFrames(),
                                physical = vehicle.DescribeCompoundCollision(),
                                horseAtContact = horse ? CompoundHorseAtPost(vehicle, post) : null });
                        }
                }
                Require(!WheelContacts(vehicle, vehicle.GlobalTransform, vehicle.SteeringRadians, path).Any(),
                    "accepted whole vehicle and articulated hoof queries never penetrate the post");
                if (hitPost || vehicle.GlobalPosition.DistanceTo(start) > .85f) break;
            }
            Input.ActionRelease(driveAction); Input.ActionRelease("move_right");
            Input.ActionPress("jump"); await Frames(15); Input.ActionRelease("jump");
            Require(hitPost && (horse ? contactedShape?.StartsWith("HoofContactQuery", StringComparison.Ordinal) == true
                    : contactedShape == "FrontWheelCollision"),
                "ordinary motion meets the real low post with a rounded ground-contact part");
            Require(vehicle.GlobalPosition.DistanceTo(start) > .025f,
                "low-post contact follows real movement rather than an initial overlap");
            await CaptureCompoundPostContact(vehicle, post, contactedShape,
                vehicle.Definition.Id + (reverse ? "-rear" : side != 0 ? "-side" : "") + "-compound-post-contact");
            var contactedPosition = vehicle.GlobalPosition;
            Input.ActionPress(escapeAction); await Frames(65); Input.ActionRelease(escapeAction);
            Input.ActionPress("jump"); await Frames(20); Input.ActionRelease("jump");
            Require(vehicle.GlobalPosition.DistanceTo(contactedPosition) > .10f
                && !WheelContacts(vehicle, vehicle.GlobalTransform, vehicle.SteeringRadians, path).Any(),
                "ordinary reverse leaves the same obstacle without collision bypass");
        }
        finally
        {
            foreach (var action in new[] { "move_forward", "move_backward", "move_left", "move_right", "jump" })
                Input.ActionRelease(action);
            post.QueueFree(); await Frames(3);
        }
    }

    private JsonObject CompoundHorseAtPost(VehicleController vehicle, StaticBody3D post)
    {
        var hoofs = new JsonArray();
        var half = ((BoxShape3D)post.GetChildren().OfType<CollisionShape3D>().Single().Shape!).Size * .5f;
        var minimumGap = float.PositiveInfinity;
        foreach (var mesh in vehicle.FindChildren("HoofWallAndSole", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var geometry = mesh.Mesh ?? throw new InvalidOperationException("The actual hoof mesh is missing.");
            var vertices = Enumerable.Range(0, geometry.GetSurfaceCount())
                .SelectMany(surface => geometry.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).ToArray();
            var minimumY = vertices.Min(vertex => vertex.Y);
            var faces = geometry.GetFaces().Select(vertex => post.ToLocal(mesh.ToGlobal(vertex))).ToArray();
            var meshGap = float.PositiveInfinity;
            for (var index = 0; index + 2 < faces.Length; index += 3)
                meshGap = Math.Min(meshGap, CompoundTriangleBoxGap(faces[index], faces[index + 1], faces[index + 2], half));
            minimumGap = Math.Min(minimumGap, meshGap);
            var rim = new JsonArray(); var outside = 0; var below = 0f;
            foreach (var vertex in vertices)
            {
                var world = mesh.ToGlobal(vertex); var local = vehicle.ToLocal(world);
                if (!vehicle.PhysicalEnvelopeContains(local, vehicle.SteeringRadians)) outside++;
                below = Math.Max(below, Math.Max(0, -local.Y));
            }
            foreach (var vertex in vertices.Where(vertex => vertex.Y == minimumY).Distinct())
            {
                var world = mesh.ToGlobal(vertex); var relative = post.ToLocal(world);
                rim.Add(new JsonObject { ["world"] = new JsonArray(world.X, world.Y, world.Z),
                    ["postLocal"] = new JsonArray(relative.X, relative.Y, relative.Z) });
            }
            hoofs.Add(new JsonObject { ["mesh"] = mesh.GetPath().ToString(), ["pose"] = mesh.GlobalTransform.ToString(),
                ["localMeshBounds"] = geometry.GetAabb().ToString(), ["vertices"] = vertices.Length,
                ["actualTrianglePostGapMetres"] = meshGap,
                ["outsidePhysicalUnionVertices"] = outside, ["maximumBelowFixedSoleMetres"] = below, ["actualSoleRim"] = rim });
        }
        return new JsonObject { ["physicsFrame"] = Engine.GetPhysicsFrames(), ["vehiclePose"] = vehicle.GlobalTransform.ToString(),
            ["speed"] = vehicle.Speed, ["postPose"] = post.GlobalTransform.ToString(), ["hoofMeshes"] = hoofs,
            ["minimumActualMeshGapMetres"] = minimumGap,
            ["support"] = HorsePose(vehicle).CaptureSupportProof(), ["fullContainmentAccepted"] = false,
            ["scope"] = "Actual IK, mesh-triangle to post-box gap, and visible sole vertices at this exact contact/capture; articulated query contact is distinct from native body contact." };
    }

    private static float CompoundTriangleBoxGap(Vector3 a, Vector3 b, Vector3 c, Vector3 half)
    {
        var triangle = new[] { a, b, c }; var box = new Aabb(-half, half * 2);
        var vertices = Enumerable.Range(0, 8).Select(index => new Vector3((index & 1) == 0 ? -half.X : half.X,
            (index & 2) == 0 ? -half.Y : half.Y, (index & 4) == 0 ? -half.Z : half.Z)).ToArray();
        var distance = triangle.Min(point => point.DistanceTo(point.Clamp(-half, half)));
        var cross = (b - a).Cross(c - a);
        if (cross.LengthSquared() < 1e-14f) throw new InvalidOperationException("Actual hoof mesh has a degenerate contact triangle.");
        var normal = cross.Normalized();
        foreach (var point in vertices)
        {
            var projection = point - normal * normal.Dot(point - a);
            if (normal.Dot((b - a).Cross(projection - a)) >= 0
                && normal.Dot((c - b).Cross(projection - b)) >= 0
                && normal.Dot((a - c).Cross(projection - c)) >= 0)
                distance = Math.Min(distance, point.DistanceTo(projection));
        }
        for (var edge = 0; edge < 3; edge++)
        {
            var from = triangle[edge]; var to = triangle[(edge + 1) % 3];
            if (box.IntersectsSegment(from, to)) return 0;
            for (var corner = 0; corner < 8; corner++) foreach (var axis in new[] { 1, 2, 4 })
            {
                if ((corner & axis) != 0) continue;
                var p = vertices[corner]; var q = vertices[corner | axis];
                if (Geometry3D.SegmentIntersectsTriangle(p, q, a, b, c).VariantType != Variant.Type.Nil) return 0;
                var closest = Geometry3D.GetClosestPointsBetweenSegments(from, to, p, q);
                distance = Math.Min(distance, closest[0].DistanceTo(closest[1]));
            }
        }
        return distance;
    }

    private async Task CaptureCompoundPostContact(VehicleController vehicle, StaticBody3D post, string? contactedShape, string name)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_CAPTURE") != "1") return;
        var horse = vehicle.Definition.Kind == VehicleKind.HorseCart;
        var part = vehicle.FindChildren(horse ? "HoofWallAndSole" : "RoadWheelMesh", "MeshInstance3D", true, false)
            .OfType<MeshInstance3D>().OrderBy(mesh => mesh.GlobalPosition.DistanceSquaredTo(post.GlobalPosition)).First();
        var meshBounds = part.Mesh!.GetAabb();
        var postMesh = post.GetChildren().OfType<MeshInstance3D>().Single();
        var postSize = ((BoxShape3D)post.GetChildren().OfType<CollisionShape3D>().Single().Shape!).Size;
        var postTarget = post.ToGlobal(new(0, postSize.Y * .3f, 0));
        var centre = part.ToGlobal(meshBounds.GetCenter());
        var subject = (centre + post.GlobalPosition) * .5f + Vector3.Up * .08f;
        var points = new List<Vector3>();
        foreach (var x in new[] { 0f, 1f }) foreach (var y in new[] { 0f, 1f }) foreach (var z in new[] { 0f, 1f })
        {
            points.Add(part.ToGlobal(meshBounds.Position + meshBounds.Size * new Vector3(x, y, z)));
            points.Add(post.ToGlobal(postSize * (new Vector3(x, y, z) - Vector3.One * .5f)));
        }
        if (horse) points.Add(part.GetParent<Node3D>().GetParent<Node3D>().GlobalPosition); // Actual knee, for leg context.
        var previous = GetViewport().GetCamera3D();
        var camera = new Camera3D { Name = "CompoundContactProofCamera", Fov = 56, Near = .05f }; AddChild(camera);
        var before = vehicle.GlobalTransform;
        try
        {
            var space = vehicle.GetWorld3D().DirectSpaceState;
            var excluded = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
            using var lens = new SphereShape3D { Radius = .08f };
            using var query = new PhysicsShapeQueryParameters3D { Shape = lens, CollisionMask = 3, Margin = .01f, Exclude = excluded };
            using var ray = PhysicsRayQueryParameters3D.Create(Vector3.Zero, postTarget, 3, excluded);
            var viewport = GetViewport().GetVisibleRect().Size; var accepted = false; var rejected = new List<string>();
            foreach (var side in horse ? new[] { -1f, 1f } : new[] { 1f, -1f })
            {
                foreach (var distance in new[] { 1.1f, 1.5f, 1.9f })
                {
                    foreach (var height in new[] { .30f, .50f, .70f })
                    {
                        foreach (var forward in new[] { -.35f, -.65f, .15f, .45f })
                        {
                            camera.GlobalPosition = subject + vehicle.GlobalBasis * new Vector3(side * distance, height, forward);
                            camera.LookAt(subject, Vector3.Up); query.Transform = new(Basis.Identity, camera.GlobalPosition);
                            if (space.IntersectShape(query, 1).Count > 0) { rejected.Add("lens overlap"); continue; }
                            if (points.Any(point => !camera.IsPositionInFrustum(point) || camera.UnprojectPosition(point).X < 24
                                || camera.UnprojectPosition(point).Y < 24 || camera.UnprojectPosition(point).X > viewport.X - 24
                                || camera.UnprojectPosition(point).Y > viewport.Y - 24)) { rejected.Add("actual part/post outside framing"); continue; }
                            ray.From = camera.GlobalPosition; var hit = space.IntersectRay(ray);
                            if (hit.Count == 0 || hit["collider"].AsGodotObject() != post) { rejected.Add("physical post occluded"); continue; }
                            var postHit = Act1VisibleSurfaceProbe.Nearest(GetTree().Root, camera, camera.UnprojectPosition(postTarget) / viewport);
                            var partHit = Act1VisibleSurfaceProbe.Nearest(GetTree().Root, camera, camera.UnprojectPosition(centre) / viewport);
                            if (postHit?.Mesh != postMesh || partHit?.Mesh != part) { rejected.Add("actual post/part surface occluded"); continue; }
                            accepted = true; break;
                        }
                        if (accepted) break;
                    }
                    if (accepted) break;
                }
                if (accepted) break;
            }
            Require(accepted, "compound contact camera frames the actual low post and visible tyre/hoof with both surface owners unobscured");
            camera.MakeCurrent(); await Frames(2); await Capture(name);
            var postPixel = camera.UnprojectPosition(postTarget) / viewport;
            var partPixel = camera.UnprojectPosition(part.ToGlobal(meshBounds.GetCenter())) / viewport;
            var actualPost = Act1VisibleSurfaceProbe.Nearest(GetTree().Root, camera, postPixel);
            var actualPart = Act1VisibleSurfaceProbe.Nearest(GetTree().Root, camera, partPixel);
            Act1VisibleSurfaceProbe.Log(GetTree().Root, camera, "vehicle/" + name + "/actual-post", postPixel);
            Act1VisibleSurfaceProbe.Log(GetTree().Root, camera, "vehicle/" + name + "/actual-part", partPixel);
            _records.Add(new { kind = "compound-contact-close-up", name, contactedShape, physicsFrame = Engine.GetPhysicsFrames(),
                post = post.GetPath().ToString(), postPose = post.GlobalTransform.ToString(), postSize = postSize.ToString(),
                part = part.GetPath().ToString(), partPose = part.GlobalTransform.ToString(), camera = camera.GlobalTransform.ToString(),
                postPixel = postPixel.ToString(), partPixel = partPixel.ToString(), visiblePostOwner = actualPost?.Mesh.GetPath().ToString(),
                visiblePartOwner = actualPart?.Mesh.GetPath().ToString(), speed = vehicle.Speed,
                vehicleBeforeCapture = before.ToString(), vehicleAfterCapture = vehicle.GlobalTransform.ToString(),
                rejectedCandidates = rejected.Count, firstRejections = rejected.Take(6).ToArray(),
                horseAtCapture = horse ? CompoundHorseAtPost(vehicle, post) : null,
                limit = "Temporary diagnostic camera only. Exact post and current visible part are probed; proxy contact and actual IK sole state are separate evidence, not whole-hoof containment or art acceptance." });
            Require(actualPost?.Mesh == postMesh && actualPart?.Mesh == part,
                "rendered compound contact retains the actual post and tyre/hoof as the nearest visible surfaces");
        }
        finally
        {
            if (previous is not null && GodotObject.IsInstanceValid(previous) && previous.IsInsideTree()) previous.MakeCurrent();
            camera.QueueFree();
        }
    }
}
