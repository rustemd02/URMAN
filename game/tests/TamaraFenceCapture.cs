using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Automated capture for visual review: the standing plot, the collapse
/// itself, every shot of the scene, a board spot and the mended fence. Writes
/// PNGs to URMAN_TAMARA_FRAMES.
/// </summary>
public partial class TamaraFenceCapture : Node
{
    private string _output = "/tmp/tamara_frames";
    private bool _walkRoute;
    private float _walkedMetres;
    private bool _recordingCutscene;
    private static readonly Vector2[] RoadsidePassage = [new(-12, -2.5f), new(-12.25f, -4.2f)];

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            _output = System.Environment.GetEnvironmentVariable("URMAN_TAMARA_FRAMES") ?? _output;
            _walkRoute = System.Environment.GetEnvironmentVariable("URMAN_TAMARA_WALK_ROUTE") == "1";
            Directory.CreateDirectory(_output);
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await this.StartThroughMainMenuAsync(demo)) throw new InvalidOperationException("no start");
            // Without this the arrival card stays over every frame below and the
            // capture shows the title panel instead of the world.
            demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            var baseAccessibility = player.Accessibility;
            var quest = TamaraFenceQuest.Current(GetTree())!;
            var fleet = (VehicleFleet)GetTree().GetFirstNodeInGroup("vehicle_fleet");
            var niva = fleet.Vehicles.Single(vehicle => vehicle.Definition.Id == "babay-niva");
            var tamaraRoute = new Vector2[] { new(8.6f, -41.3f), new(6.4f, -42.9f), new(4.7f, -44.4f) };
            for (var segment = 1; segment < tamaraRoute.Length; segment++)
            {
                var a = tamaraRoute[segment - 1];
                var b = tamaraRoute[segment];
                Vector3 Raised(Vector2 p) => new(p.X,
                    Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(p.X, p.Y) + .85f, p.Y);
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(Raised(a), Raised(b), 1));
                GD.Print($"tamara-walk-contact: segment={segment} hit={(hit.Count > 0 ? hit["collider"].AsGodotObject() is Node node ? node.GetPath().ToString() : "unknown" : "clear")}");
            }

            // Standing plot from the road, the wicket side, then the fence line
            // itself: pillars, shields and the footing.
            player.GlobalPosition = new(-1.2f, .1f, -40.0f);
            player.RotationDegrees = new(0, -54f, 0);
            await Frames(20);
            await Capture("01_fence_from_road");
            player.GlobalPosition = new(1.4f, .1f, -39.6f);
            player.RotationDegrees = new(0, -47f, 0);
            await Frames(8);
            await Capture("02_wicket_close");
            player.GlobalPosition = new(-1.6f, .1f, -46.6f);
            player.RotationDegrees = new(0, -110f, 0);
            await Frames(8);
            await Capture("02b_fence_line_close");

            // Board spot at the village-edge shed.
            player.GlobalPosition = new(15.4f, .1f, -43.5f);
            player.ApplySmokeLook(-20f, 0f);
            await Frames(8);
            await Capture("03_board_spot_shed");

            // Use the same ordinary steering/ignition route as the drive proof.
            quest.AccelerateCutsceneForTest(1.0);
            // The listener goes on before the crash: the opening shot fires as
            // the scene starts and would otherwise never be photographed.
            string? currentShot = null;
            string? currentLine = null;
            Task? recording = null;
            quest.CutsceneShotListenerForTest = tag =>
            {
                currentShot = tag;
                if (recording is null && System.Environment.GetEnvironmentVariable("URMAN_TAMARA_MOVIE") == "1")
                    recording = RecordCutscene(quest, () => currentShot, () => currentLine);
            };
            await TamaraFenceDriveProof.DriveIntoFenceAsync(this, player, niva, bridge);
            for (var i = 0; i < 600 && quest.ActiveCutscene is null; i++) await Frames(1);

            // The shields go over in the first second; three frames across it
            // are what the collapse is judged by.
            if (quest.ActiveCutscene is null) throw new InvalidOperationException("no cutscene after the crash");
            if (System.Environment.GetEnvironmentVariable("URMAN_TAMARA_INTERACTIONS_ONLY") == "1")
            {
                Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Space, Keycode = Key.Space, Pressed = true });
                await Frames(1);
                Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Space, Keycode = Key.Space, Pressed = false });
            }
            quest.ActiveCutscene?.SetLineListenerForTest(tag => currentLine = tag);
            await Frames(14);
            await Capture("04a_collapse_early");
            await Frames(24);
            await Capture("04b_collapse_falling");
            await Frames(60);
            await Capture("04c_collapse_settled");
            var captured = new HashSet<string>();
            var guard = 0;
            while (quest.ActiveCutscene is not null && guard++ < 12000)
            {
                await Frames(1);
                quest.DebugExposeGuyForTest(out _, out var skeleton, out _, out _);
                if (skeleton is not null)
                    for (var bone = 0; bone < skeleton.GetBoneCount(); bone++)
                    {
                        var scale = skeleton.GetBonePoseScale(bone);
                        if (!scale.IsFinite() || scale.Length() > 4f)
                            throw new InvalidOperationException($"invalid filming pose: {skeleton.GetBoneName(bone)} scale={scale}");
                    }
                var tag = currentShot;
                if (tag is not null && !captured.Contains(tag))
                {
                    await Frames(14);
                    // Photograph only the named shot, never a stale event queue.
                    if (tag == currentShot)
                    {
                        await Capture($"05_shot_{tag}");
                        captured.Add(tag);
                    }
                }
                if (currentLine is "tamara-cutscene-guy-line-2" or "tamara-cutscene-tamara-boards"
                    && captured.Add(currentLine))
                {
                    await Frames(3);
                    await Capture($"06_caption_{currentLine}");
                    if (currentLine == "tamara-cutscene-tamara-boards")
                    {
                        AccessibilityPresentation.ApplyToTree(GetTree(), baseAccessibility with { TextScale = 1.6 });
                        await Frames(5);
                        await Capture("07_caption_large_text");
                        AccessibilityPresentation.ApplyToTree(GetTree(), baseAccessibility);
                    }
                }
            }
            if (quest.ActiveCutscene is not null) throw new InvalidOperationException("cutscene did not finish");
            if (recording is not null) await recording;

            await Frames(10);
            GD.Print("tamara-car-support-after-scene: " + niva.DescribePlacementProbe(niva.GlobalTransform).ToJsonString());
            await Capture("09_after_scene");

            // The work list while the boards are still missing.
            await CaptureJournalWorkList("11_journal_in_progress");

            // Continuous mode starts at the ordinary vehicle exit. Earlier
            // establishing shots and the pre-drive pose remain explicit fixtures.
            Input.ActionPress("interact");
            for (var i = 0; i < 2; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ActionRelease("interact");
            await Frames(4);
            if (niva.Driver is not null) throw new InvalidOperationException("no safe exit after crash");
            GD.Print("tamara-car-support-after-exit: " + niva.DescribePlacementProbe(niva.GlobalTransform).ToJsonString());
            if (System.Environment.GetEnvironmentVariable("URMAN_TAMARA_WOODPILE_PROBE") == "1")
            {
                var cart = fleet.Vehicles.Single(vehicle => vehicle.Definition.Id == "forest-horse-cart");
                if (!cart.PlacementAvailable)
                    throw new InvalidOperationException("Shoulder contact disabled the horse cart: " + cart.PlacementFailure);
                GD.Print("tamara-shoulder-regression: forest-horse-cart authored placement remains available");
                // Local geometry diagnosis only. This explicit fixture does NOT
                // establish the continuous route from the vehicle exit.
                _walkRoute = true;
                var start = RoadsidePassage[0];
                player.GlobalPosition = new(start.X,
                    Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(start.X, start.Y) + .06f, start.Y);
                player.Velocity = Vector3.Zero;
                await Frames(12);
                foreach (var target in quest.BoardTargets.Take(2))
                {
                    var spot = (Node3D)target.GetParent();
                    GD.Print($"tamara-board-surface: {target.Name} spot={spot.GlobalPosition}");
                    foreach (var mesh in demo.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                    {
                        if (!mesh.IsVisibleInTree() || mesh.Mesh is null || spot.IsAncestorOf(mesh)) continue;
                        var bounds = mesh.GlobalTransform * mesh.GetAabb();
                        var p = spot.GlobalPosition;
                        if (p.X < bounds.Position.X || p.X > bounds.End.X || p.Z < bounds.Position.Z || p.Z > bounds.End.Z) continue;
                        var from = mesh.ToLocal(p + Vector3.Up * .8f);
                        var to = mesh.ToLocal(p - Vector3.Up);
                        var faces = mesh.Mesh.GetFaces();
                        for (var i = 0; i < faces.Length; i += 3)
                        {
                            var hit = Geometry3D.SegmentIntersectsTriangle(from, to, faces[i], faces[i + 1], faces[i + 2]);
                            if (hit.VariantType != Variant.Type.Nil)
                            {
                                GD.Print($"tamara-board-surface: {target.Name} visible={mesh.GetPath()} hit={mesh.ToGlobal(hit.AsVector3())}");
                                if (mesh.Name == "Grade_Street_West")
                                {
                                    var surface = mesh.ToGlobal(hit.AsVector3());
                                    var support = player.GetWorld3D().DirectSpaceState.IntersectRay(
                                        PhysicsRayQueryParameters3D.Create(surface + Vector3.Up * .4f, surface - Vector3.Up, 1u));
                                    if (support.Count == 0 || support["position"].AsVector3().DistanceTo(surface) > .01f)
                                        throw new InvalidOperationException($"Shoulder physics disagrees with visible snow at {target.Name}.");
                                    GD.Print($"tamara-shoulder-support: {target.Name} gap={support["position"].AsVector3().DistanceTo(surface):F6} collider={(support["collider"].AsGodotObject() as Node)?.GetPath()}");
                                }
                            }
                        }
                    }
                }
                foreach (var point in RoadsidePassage) await WalkTo(player, point);
                await InteractLocally(player, quest.BoardTargets[0]);
                await WalkBoardApproach(player, 1);
                await InteractLocally(player, quest.BoardTargets[1]);
                await WalkTo(player, new(-14.8f, -3.55f));
                await Frames(12);
                player.ApplySmokeLook(-72, 0);
                await Capture("woodpile_supported_feet");
                GD.Print($"tamara-shoulder-feet: player={player.GlobalPosition} floor={player.IsOnFloor()} soles={string.Join(";", player.VisibleBodySoles.Select(p => p.ToString()))}");
                for (var i = 0; i < 240 && bridge.TamaraFenceSnapshotNow()?.Carried != 2; i++) await Frames(1);
                if (bridge.TamaraFenceSnapshotNow()?.Carried != 2) throw new InvalidOperationException("woodpile probe pickups failed");
                await Capture("woodpile_two_boards");
                AccessibilityPresentation.ApplyToTree(GetTree(), baseAccessibility with { TextScale = 1.6 });
                await Frames(8);
                await Capture("woodpile_banner_large_text");
                AccessibilityPresentation.ApplyToTree(GetTree(), baseAccessibility);
                await WalkTo(player, RoadsidePassage[0]);
                await Capture("woodpile_return_to_path");
                GD.Print($"tamara-woodpile-probe: LOCAL FIXTURE ONLY; two pickups/return passed, metres={_walkedMetres:0.00}");
                exit = 0;
                return;
            }
            var approachFailures = new List<string>();
            var order = _walkRoute ? new[] { 5, 2, 3, 4, 0, 1 } : new[] { 0, 1, 2, 3, 4, 5 };
            var collected = 0;
            foreach (var board in order)
            {
                if (_walkRoute) await WalkBoardApproach(player, board);
                try { await InteractLocally(player, quest.BoardTargets[board]); }
                catch (InvalidOperationException error)
                {
                    if (_walkRoute) throw;
                    approachFailures.Add(error.Message);
                    continue;
                }
                collected++;
                for (var i = 0; i < 240 && bridge.TamaraFenceSnapshotNow()?.Carried != collected; i++) await Frames(1);
                if (bridge.TamaraFenceSnapshotNow()?.Carried != collected)
                    throw new InvalidOperationException($"physical pickup {board + 1} failed");
                if (_walkRoute) await Capture($"13_board_{board + 1}_pickup");
                else if (board == 0) await Capture("13_board_pickup");
                if (collected == 1) await CaptureJournalWorkList("11b_journal_one_board");
            }
            if (approachFailures.Count > 0) throw new InvalidOperationException(string.Join("; ", approachFailures));
            await CaptureJournalWorkList("11c_journal_all_boards");
            if (_walkRoute)
            {
                await WalkTo(player, RoadsidePassage[0]);
                foreach (var point in new Vector2[] { new(-6, -5.5f), new(0, -8), new(0, -38.5f), new(4.05f, -38.5f), new(4.05f, -42.85f) })
                    await WalkTo(player, point);
            }
            await InteractLocally(player, quest.HandInTarget!);
            for (var i = 0; i < 240 && bridge.TamaraFenceSnapshotNow()?.Repaired != true; i++) await Frames(1);
            if (bridge.TamaraFenceSnapshotNow()?.Repaired != true) throw new InvalidOperationException("physical hand-over failed");
            await Frames(15);
            var banner = (SideQuestBannerUi)GetTree().GetFirstNodeInGroup("side_quest_banner");
            var bannerPanel = banner.GetNode<PanelContainer>("Screen/Panel");
            if (bannerPanel.Size.Y > GetViewport().GetVisibleRect().Size.Y * .4f)
                throw new InvalidOperationException($"oversized quest banner: {bannerPanel.Size}");
            await Capture("14_hand_in_dialogue");
            AccessibilityPresentation.ApplyToTree(GetTree(), baseAccessibility with { TextScale = 1.6 });
            await Frames(8);
            await Capture("15_hand_in_large_text");
            AccessibilityPresentation.ApplyToTree(GetTree(), baseAccessibility);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = true });
            await Frames(2);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = false });
            await Frames(2);
            if (player.ModalOpen) throw new InvalidOperationException("hand-over dialogue kept controls");
            await Capture("16_hand_in_banner_after_dialogue");
            await CaptureJournalWorkList("16b_journal_boards_delivered");
            await InteractLocally(player, quest.HandInTarget!);
            if (GetTree().GetFirstNodeInGroup("dialogue_ui") is not DialogueUi { IsOpen: true })
                throw new InvalidOperationException("Tamara's repeat conversation did not open");
            await Capture("16c_tamara_repeat");
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = true });
            await Frames(2);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = false });
            await Frames(2);
            if (player.ModalOpen) throw new InvalidOperationException("repeat conversation kept controls");
            if (_walkRoute)
                foreach (var point in new Vector2[] { new(4.05f, -38.5f), new(0, -38.5f), new(-3.5f, -38.5f), new(-3.5f, -28) }) await WalkTo(player, point);
            else player.GlobalPosition = new(-2f, .1f, -20f);
            for (var i = 0; i < 30 && !quest.FenceRepairApplied; i++) await Frames(1);
            if (!quest.FenceRepairApplied) throw new InvalidOperationException("fence did not repair after leaving the yard");
            GD.Print("tamara-car-support-after-repair: " + niva.DescribePlacementProbe(niva.GlobalTransform).ToJsonString());
            if (_walkRoute)
                foreach (var point in new Vector2[] { new(-3.5f, -28), new(-3.5f, -48.7f), new(-1.6f, -48.7f) }) await WalkTo(player, point);
            else player.GlobalPosition = new(-1.6f, .1f, -48.7f);
            player.ApplySmokeLook(-6f, -125f);
            await Frames(10);
            await Capture("10_repaired_fence");

            // The same page once the fence is mended.
            await CaptureJournalWorkList("12_journal_done");
            exit = 0;
            GD.Print($"tamara-fence-capture: done -> {_output}; continuousAfterExit={_walkRoute} walkedMetres={_walkedMetres:0.00}");
        }
        catch (Exception error)
        {
            GD.PushError("tamara-fence-capture failed: " + error);
        }
        finally
        {
            _recordingCutscene = false;
            GetTree().Quit(exit);
        }
    }

    private async Task RecordCutscene(TamaraFenceQuest quest, Func<string?> shot, Func<string?> line)
    {
        _recordingCutscene = true;
        var started = Time.GetTicksMsec();
        var samples = new List<object>();
        var fleet = (VehicleFleet)GetTree().GetFirstNodeInGroup("vehicle_fleet");
        var niva = fleet.Vehicles.Single(vehicle => vehicle.Definition.Id == "babay-niva");
        // The first shot event can fire while the scene is still being assigned.
        await Frames(1);
        while (_recordingCutscene && quest.ActiveCutscene is not null)
        {
            await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "tamara-movie");
            var tamara = quest.TamaraActor;
            samples.Add(new { milliseconds = Time.GetTicksMsec() - started, shot = shot(), line = line(),
                nivaPosition = new[] { niva.GlobalPosition.X, niva.GlobalPosition.Y, niva.GlobalPosition.Z },
                nivaYaw = niva.Rotation.Y,
                guyInsideVehicle = new[] { .4f, .9f, 1.3f }.Any(height =>
                    niva.PhysicalEnvelopeContains(niva.ToLocal(quest.GuyActor.GlobalPosition + Vector3.Up * height), 0f)),
                guyYaw = quest.GuyActor.Rotation.Y,
                guyClip = quest.GuyActor.GetMeta("animationClip", "").AsString(),
                guyPosition = new[] { quest.GuyActor.GlobalPosition.X, quest.GuyActor.GlobalPosition.Y, quest.GuyActor.GlobalPosition.Z },
                tamaraYaw = tamara.Rotation.Y,
                tamaraPosition = new[] { tamara.GlobalPosition.X, tamara.GlobalPosition.Y, tamara.GlobalPosition.Z } });
            using var frame = GetViewport().GetTexture().GetImage();
            if (frame.SaveJpg(Path.Combine(_output, $"movie_{samples.Count:00000}.jpg"), .9f) != Error.Ok)
                throw new IOException("Could not save Tamara cutscene frame.");
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        }
        File.WriteAllText(Path.Combine(_output, "movie-timing.json"),
            System.Text.Json.JsonSerializer.Serialize(samples));
        GD.Print($"tamara-movie: frames={samples.Count} elapsedMs={Time.GetTicksMsec() - started}; wall-clock timing, no audio, no performance claim");
    }

    private async Task InteractLocally(FirstPersonController player, InteractionTarget target)
    {
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var ray = camera.GetNode<RayCast3D>("InteractionRay");
        for (var side = 0; side < (_walkRoute ? 1 : 8); side++)
        {
            var angle = side * Mathf.Pi / 4f;
            var at = target.GlobalPosition + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 1.35f;
            at.Y = Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(at.X, at.Z) + .06f;
            if (!player.CanStandAt(_walkRoute ? player.GlobalPosition : at))
            {
                GD.Print($"tamara-approach: {target.Name} side={side} standing=blocked at={at}");
                continue;
            }
            if (!_walkRoute)
            {
                player.GlobalPosition = at;
                player.Velocity = Vector3.Zero;
            }
            await Frames(16);
            var toward = target.GlobalPosition - camera.GlobalPosition;
            player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(toward.Y, new Vector2(toward.X, toward.Z).Length())),
                Mathf.RadToDeg(Mathf.Atan2(-toward.X, -toward.Z)));
            await Frames(8);
            ray.ForceRaycastUpdate();
            if (!player.CanStandAt(player.GlobalPosition) || ray.GetCollider() != target)
            {
                GD.Print($"tamara-approach: {target.Name} side={side} at={player.GlobalPosition} target={target.GlobalPosition} ray={(ray.GetCollider() as Node)?.GetPath()} hit={ray.GetCollisionPoint()}");
                continue;
            }
            if (_walkRoute)
            {
                await Capture($"before_{target.Name}");
                // A native focus request from capture is asynchronous. Let it
                // settle before sending one key press through the physics loop.
                await Frames(4);
                ray.ForceRaycastUpdate();
                if (ray.GetCollider() != target)
                    throw new InvalidOperationException($"capture changed aim before {target.InteractionId}");
            }
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.E, Keycode = Key.E, Pressed = true });
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.E, Keycode = Key.E, Pressed = false });
            await Frames(3);
            GD.Print($"tamara-fence-capture: physical E -> {target.InteractionId}");
            return;
        }
        await Capture($"failed_approach_{target.Name}");
        throw new InvalidOperationException($"no standing ray approach to {target.InteractionId}");
    }

    private async Task WalkBoardApproach(FirstPersonController player, int board)
    {
        // Authored road/gate routes, not an automatic pathfinder. A blocked
        // segment fails at its real capsule; it never teleports or drops fences.
        Vector2[] route = board switch
        {
            5 => [new(-2.2f, -42.2f), new(-2.2f, -38.5f), new(0, -38.5f), new(0, -10),
                  new(4.5f, -12.5f), new(10, -17), new(17, -21.5f), new(19, -22.5f), new(24, -17), new(23, -12), new(21, -8.5f), new(17.5f, -8.75f)],
            2 => [new(21, -8.5f), new(23, -12), new(24, -17), new(19, -22.5f), new(18, -23),
                  new(18, -44.55f), new(14.5f, -44.55f)],
            3 => [new(16.4f, -44.55f)],
            4 => [new(18, -44.55f), new(18, -23), new(19, -22.5f), new(17, -21.5f),
                  new(10, -17), new(4.5f, -12.5f), new(0, -10), new(0, -8),
                  new(-6, -5.5f), new(-12, -2.5f), new(-12.9f, -2.5f), new(-12.9f, -6), new(-16.5f, -9.5f), new(-19.6454f, -11.5454f)],
            0 => [new(-16.5f, -9.5f), new(-12.9f, -6), new(-12.9f, -2.5f)],
            1 => [new(-12, -2.5f), new(-14.8f, -2.2f)],
            _ => throw new ArgumentOutOfRangeException(nameof(board))
        };
        foreach (var point in route) await WalkTo(player, point);
        if (board == 0) foreach (var point in RoadsidePassage) await WalkTo(player, point);
    }

    private async Task WalkTo(FirstPersonController player, Vector2 goal)
    {
        float Distance() => new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z).DistanceTo(goal);
        var from = player.GlobalPosition;
        var revision = player.PresentationTransformRevision;
        var recoveries = player.FallRecoveries;
        var previousDistance = Distance();
        var stalled = 0;
        var blocker = "none";
        try
        {
            for (var frame = 0; frame < 1500 && Distance() > .14f && stalled < 150; frame++)
            {
                var delta = goal - new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z);
                player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Y)));
                Input.ActionPress("move_forward", Distance() < .4f ? .35f : 1);
                var previous = player.GlobalPosition;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                _walkedMetres += new Vector2(previous.X - player.GlobalPosition.X, previous.Z - player.GlobalPosition.Z).Length();
                stalled = Math.Abs(previousDistance - Distance()) < .002f ? stalled + 1 : 0;
                previousDistance = Distance();
                for (var i = 0; i < player.GetSlideCollisionCount(); i++)
                {
                    var contact = player.GetSlideCollision(i);
                    if (Math.Abs(contact.GetNormal().Y) >= .7f) continue;
                    blocker = (contact.GetCollider() as Node)?.GetPath().ToString() ?? "unknown";
                    if (contact.GetColliderShape() is CollisionShape3D shape)
                        blocker += $" shape={shape.Name} source={shape.GetMeta("authoredSourceMesh", "none")} at={contact.GetPosition()}";
                }
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        await Frames(4);
        GD.Print($"tamara-route: from={from} goal={goal} actual={player.GlobalPosition} metres={_walkedMetres:0.00} blocker={blocker} step={player.LastStepRejection}");
        if (Distance() > .25f || !player.IsOnFloor() || !player.CanStandAt(player.GlobalPosition)
            || player.PresentationTransformRevision != revision || player.FallRecoveries != recoveries)
        {
            await Capture("failed_walk");
            throw new InvalidOperationException($"continuous walk blocked/repositioned at {player.GlobalPosition} toward {goal}; blocker={blocker}");
        }
    }

    private async Task Capture(string name)
    {
        await Frames(2);
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "tamara/" + name);
        using var image = GetViewport().GetTexture().GetImage();
        image.Convert(Image.Format.Rgba8);
        var path = Path.Combine(_output, name + ".png");
        image.SavePng(path);
        GD.Print($"tamara-fence-capture: saved {path}");
    }

    private async Task CaptureJournalWorkList(string name)
    {
        if (GetTree().GetFirstNodeInGroup("journal_ui") is not JournalUi journal) return;
        journal.Open((RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge"));
        if (journal.FindChild("Tabs", true, false) is TabBar tabs) tabs.CurrentTab = 2;
        await Frames(8);
        await Capture(name);
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close")
            .EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(6);
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
