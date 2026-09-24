using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// A focused presentation check: existing zone switches, no quest flags or
/// save writes. Verifies the four proximity turns and the six initial cast
/// placements, intermediate turn contacts, and the nine asset families at their
/// unchanged native LOD ranges. Story-driven Rinat movement has separate,
/// reachable-state coverage in Act1FinalStateSmokeTest.
/// </summary>
public partial class Act1NpcPresentationSmokeTest : Node
{
    private Main _main = null!;
    private FirstPersonController _player = null!;
    private string? _captureDirectory;
    private readonly List<string> _supportErrors = [];
    private readonly List<string> _viewErrors = [];
    private readonly List<string> _familyErrors = [];
    private int _turnContactSamples;
    private int _verifiedFamilies;
    private int _renderedFamilyViews;
    private bool _turnsOnly;

    public override async void _Ready()
    {
        try
        {
            _main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
            _main.InitialZoneId = "village_day";
            _main.InitialSpawnPointId = "arrival";
            _main.EnableAct1ConnectedWorld = true;
            AddChild(_main);
            await Frames(4);
            _player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController
                ?? throw new InvalidOperationException("NPC presentation check has no player.");
            _player.SetPhysicsProcess(false);
            _turnsOnly = OS.GetEnvironment("URMAN_NPC_TURNS_ONLY") == "1";
            _captureDirectory = System.Environment.GetEnvironmentVariable("URMAN_NPC_CAPTURE_DIR");
            if (!string.IsNullOrWhiteSpace(_captureDirectory))
            {
                if (DisplayServer.GetName() == "headless")
                    GD.Print("act1-npc-captures: not-run; headless display");
                else
                    System.IO.Directory.CreateDirectory(_captureDirectory);
            }

            foreach (var spec in new[]
            {
                (Id: "alsu", Zone: "village_day", Spawn: "arrival", Turns: true),
                (Id: "timur_hazrat", Zone: "village_day", Spawn: "arrival", Turns: true),
                (Id: "rinat", Zone: "village_day", Spawn: "arrival", Turns: false),
                (Id: "mansur", Zone: "house_old_pc", Spawn: "entry", Turns: true),
                (Id: "gulsina", Zone: "house_old_pc", Spawn: "entry", Turns: false),
                (Id: "naila", Zone: "fap_clinic", Spawn: "waiting_room", Turns: true)
            })
            {
                if (_turnsOnly && !spec.Turns) continue;
                _main.SwitchZone(spec.Zone, spec.Spawn);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                await Frames(2);
                var actor = _main.ConnectedWorld?.FindChild($"Npc_{spec.Id}", true, false) as Node3D
                    ?? throw new InvalidOperationException($"Missing visible actor: {spec.Id}.");
                if (actor is CollisionObject3D || !actor.IsVisibleInTree()
                    || actor.GetMeta("characterId", "").AsString() != spec.Id)
                    throw new InvalidOperationException($"{spec.Id} resolved to a hidden/proxy node, not the visible character.");

                var footOrigin = actor.GlobalPosition;
                await PutPlayer(footOrigin + new Vector3(0f, .05f, 6.5f), footOrigin + Vector3.Up);
                await SettleTurn();
                var restYaw = actor.Rotation.Y;
                // FindChildren filters native engine classes. InteractionTarget
                // is a C# script on StaticBody3D, not a registered native class.
                var targets = _main.FindChildren("*", nameof(StaticBody3D), recursive: true, owned: false)
                    .OfType<InteractionTarget>().ToArray();
                if (targets.Length == 0) throw new InvalidOperationException("NPC presentation found no real interaction targets; ray exclusions and transform assertions are invalid.");
                GD.Print($"act1-npc-targets: {spec.Id} actualScriptTargets={targets.Length}");
                var returnPosition = footOrigin + new Vector3(0f, .05f, 6.5f);
                if (spec.Id is "mansur" or "naila")
                {
                    // The outdoor 6.5m offset put these return cameras beyond
                    // a room wall. Find a real standing view before starting
                    // the measured turn, then reuse that exact physical point.
                    var restForward = actor.GlobalBasis.Z;
                    var candidates = new[] { 0f, .65f, -.65f, 1.3f, -1.3f, 2.0f, -2.0f, Mathf.Pi }
                        .SelectMany(yaw => new[] { 4.2f, 4.7f }.Select(radius => footOrigin
                            + restForward.Rotated(Vector3.Up, yaw) * radius + Vector3.Up * .05f));
                    await PutPlayerAtClearView(actor, targets, candidates, $"{spec.Id}/return-view");
                    returnPosition = _player.GlobalPosition + Vector3.Up * .025f;
                    if (returnPosition.DistanceTo(footOrigin) < 4f)
                        _viewErrors.Add($"{spec.Id}/return-view: the actual standing point cannot release the 3.9m conversation radius.");
                    await SettleTurn();
                }
                var targetTransforms = targets.ToDictionary(target => target, target => target.GlobalTransform);
                var stand = footOrigin + new Vector3(-2.15f, .05f, 1.10f);
                await PutPlayer(stand, footOrigin + Vector3.Up * .95f);
                if (spec.Turns)
                {
                    var localDirection = actor.GetParent<Node3D>().ToLocal(_player.GlobalPosition) - actor.Position;
                    await ObserveTurnContacts(actor, targets, spec.Id, "towards", restYaw,
                        Mathf.Atan2(localDirection.X, localDirection.Z));
                }
                else
                    await SettleTurn();

                if (actor.GlobalPosition.DistanceTo(footOrigin) > .001f)
                    throw new InvalidOperationException($"{spec.Id} moved its ground origin while turning.");
                if (targets.Any(target => !target.GlobalTransform.IsEqualApprox(targetTransforms[target])))
                    throw new InvalidOperationException($"{spec.Id} proximity turn moved an interaction target.");

                if (spec.Turns)
                {
                    var direction = stand - footOrigin;
                    direction.Y = 0f;
                    var facing = actor.GlobalBasis.Z;
                    facing.Y = 0f;
                    var error = Mathf.RadToDeg(facing.Normalized().AngleTo(direction.Normalized()));
                    if (error > 3f || actor.GetMeta("conversationFacing", "").AsString() != "towards-player")
                        throw new InvalidOperationException($"{spec.Id} visible facing missed player by {error:0.00} degrees.");
                    GD.Print($"act1-npc-turn: {spec.Id} visible-facing error={error:0.00}deg targets=fixed origin=fixed");
                }

                var clearControlView = true;
                if (spec.Id == "gulsina")
                {
                    var candidates = new[] { 0f, -.55f, .55f, -1.0f, 1.0f }
                        .SelectMany(yaw => new[] { 2.4f, 1.9f }.Select(radius => footOrigin
                            + actor.GlobalBasis.Z.Rotated(Vector3.Up, yaw) * radius + Vector3.Up * .05f));
                    clearControlView = await PutPlayerAtClearView(actor, targets, candidates, "gulsina/front");
                }

                if (spec.Id == "naila" && !string.IsNullOrWhiteSpace(_captureDirectory)
                    && DisplayServer.GetName() != "headless")
                    Act1VisibleSurfaceProbe.Log(GetTree().Root, _player.GetNode<Camera3D>("Head/Camera3D"),
                        "fap-naila-interior-parts", new(.425f, .315f), new(.56f, .25f),
                        new(.76f, .16f), new(.335f, .35f), new(.11f, .335f));

                await Capture($"{spec.Id}_{(spec.Turns ? "facing" : "idle")}{(clearControlView ? "" : "_occluded_control")}");
                VerifyBootSupport(actor, targets, spec.Id, spec.Turns ? "facing" : "idle");

                if (spec.Turns)
                    await VerifyConversationRetarget(actor, targets, spec.Id, restYaw);

                if (spec.Id is "timur_hazrat" or "naila")
                {
                    await CaptureProfileControl(actor, targets, spec.Id);
                    VerifyBootSupport(actor, targets, spec.Id, "profile-control");
                }

                if (spec.Turns)
                {
                    var facingYaw = actor.Rotation.Y;
                    await PutPlayer(returnPosition, footOrigin + Vector3.Up);
                    await ObserveTurnContacts(actor, targets, spec.Id, "return", facingYaw, restYaw);
                    if (Mathf.Abs(Mathf.AngleDifference(actor.Rotation.Y, restYaw)) > .01f
                        || actor.GetMeta("conversationFacing", "").AsString() != "rest")
                        throw new InvalidOperationException($"{spec.Id} did not return to authored rest after walking away.");
                    VerifyBootSupport(actor, targets, spec.Id, "rest");
                    if (spec.Id == "timur_hazrat" && !_turnsOnly)
                    {
                        foreach (var at in new[] { new Vector3(0, 0, -3f), new Vector3(0, 0, .5f) })
                        {
                            var point = at;
                            point.Y = Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .05f;
                            await PutPlayer(point, footOrigin + Vector3.Up * .95f);
                            await SettleTurn();
                            var distance = _player.GlobalPosition.DistanceTo(footOrigin);
                            GD.Print($"act1-npc-lod-view: timur_hazrat distance={distance:0.00}m; native visibility-range blend unchanged");
                            var name = distance < 18f ? "timur_hazrat_lod_blend" : "timur_hazrat_lod1";
                            await Capture(name);
                            if (System.Environment.GetEnvironmentVariable("URMAN_NPC_LOD_DIAGNOSTICS") == "1")
                                await CaptureLodDiagnostic(actor, name);
                        }
                    }
                }
            }

            if (!_turnsOnly) await VerifyCharacterFamilies();
            if (_supportErrors.Count > 0)
                throw new InvalidOperationException("Skinned sole support failures: " + string.Join(" | ", _supportErrors));
            if (_viewErrors.Count > 0)
                throw new InvalidOperationException("Control views remain unverified: " + string.Join(" | ", _viewErrors));
            if (_familyErrors.Count > 0)
                throw new InvalidOperationException("Character-family failures: " + string.Join(" | ", _familyErrors));
            GD.Print($"act1-npc-presentation: PASS 4 enter/still/deadzone/retarget/return checks + {_turnContactSamples} intermediate turn contact samples + stable ray targets + turnsOnly={_turnsOnly} + {_verifiedFamilies} asset-family contracts/{_renderedFamilyViews} native rendered views; artistic and later-story movement review remain separate");
            await GodotSmokeCleanup.ReleaseAsync(_main);
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            if (_supportErrors.Count > 0)
                GD.PushError("act1-npc-support-failures-before-interruption: " + string.Join(" | ", _supportErrors));
            GD.PushError($"act1-npc-presentation: {exception.Message}");
            if (_main is not null && IsInstanceValid(_main))
                await GodotSmokeCleanup.ReleaseAsync(_main);
            GetTree().Quit(1);
        }
    }

    private void VerifyBootSupport(Node3D actor, InteractionTarget[] targets, string id, string pose, bool detailed = true)
    {
        var boots = actor.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null
                && mesh.Name.ToString().Contains("_Boot", StringComparison.Ordinal)
                && mesh.Name.ToString().Contains("_LOD0", StringComparison.Ordinal))
            .ToArray();
        if (boots.Length != 2)
            throw new InvalidOperationException($"{id} exposes {boots.Length} visible LOD0 boots instead of 2.");
        var exclude = new global::Godot.Collections.Array<Rid>(targets.Select(target => target.GetRid()));
        foreach (var contact in actor.FindChildren("*", nameof(CollisionObject3D), true, false).OfType<CollisionObject3D>())
            exclude.Add(contact.GetRid());
        var smallestGap = float.PositiveInfinity;
        var largestGap = float.NegativeInfinity;
        var supportedPoints = 0;
        foreach (var boot in boots)
        {
            // Imported skinned vertices retain the source display-board X.
            // MeshInstance.GetAabb is pre-skin: applying only GlobalTransform
            // wrongly sampled Alsu's terrain 4.6 metres away from her feet.
            // Apply the renderer's current skin/pose, then sample the actual
            // bottom vertices, including heel/toe corners on uneven ground.
            var vertices = SkinnedWorldVertices(boot);
            var skeleton = boot.GetNode<Skeleton3D>(boot.Skeleton);
            var frameDelta = boot.GlobalTransform.AffineInverse() * skeleton.GlobalTransform;
            if (detailed) GD.Print($"act1-npc-skin-frame: {id}/{boot.Name} originDelta={frameDelta.Origin} basisIdentity={frameDelta.Basis.IsEqualApprox(Basis.Identity)}");
            if (DisplayServer.GetName() != "headless")
            {
                using var baked = boot.BakeMeshFromCurrentSkeletonPose();
                var rendered = Enumerable.Range(0, baked.GetSurfaceCount()).SelectMany(surface =>
                    baked.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    .Select(vertex => boot.GlobalTransform * vertex).ToList();
                if (rendered.Count != vertices.Count)
                    throw new InvalidOperationException($"{id}/{boot.Name} renderer bake changed vertex count; support probe is unverified.");
                var error = rendered.Zip(vertices, (renderedPoint, probePoint) => renderedPoint.DistanceTo(probePoint)).Max();
                if (detailed) GD.Print($"act1-npc-renderer-bake: {id}/{boot.Name} maxProbeDelta={error:0.000000}m");
                if (error > .001f) _supportErrors.Add($"{id}/{boot.Name} support PROBE differs from renderer bake by {error:0.000000}m");
                vertices = rendered;
            }
            var lowest = vertices.Min(vertex => vertex.Y);
            var soles = vertices.Where(vertex => vertex.Y <= lowest + .0002f).Distinct().ToArray();
            for (var sample = 0; sample < soles.Length; sample++)
            {
                var sole = soles[sample];
                var query = PhysicsRayQueryParameters3D.Create(
                    sole + Vector3.Up * .12f, sole - Vector3.Up * .35f, 3u, exclude);
                var hit = actor.GetWorld3D().DirectSpaceState.IntersectRay(query);
                if (hit.Count == 0)
                {
                    _supportErrors.Add($"{id}/{pose}/{boot.Name}[{sample}] no support at {sole} (actor={actor.GlobalPosition})");
                    continue;
                }
                var support = hit["position"].AsVector3();
                var gap = sole.Y - support.Y;
                smallestGap = Mathf.Min(smallestGap, gap);
                largestGap = Mathf.Max(largestGap, gap);
                supportedPoints++;
                var camera = _player.GetNode<Camera3D>("Head/Camera3D");
                if (gap < -.020f || gap > .020f)
                    _supportErrors.Add($"{id}/{pose}/{boot.Name}[{sample}] gap={gap:0.000}m at {sole}");
                if (detailed) GD.Print($"act1-npc-support: {id}/{pose}/{boot.Name}[{sample}] sole={sole} supportY={support.Y:0.000} gap={gap:0.000}m actor={actor.GlobalPosition} pixel={camera.UnprojectPosition(sole)} framed={camera.IsPositionInFrustum(sole)}");
            }
        }
        if (!detailed) GD.Print($"act1-npc-motion-contact: {id}/{pose} points={supportedPoints} soleGap={smallestGap:0.0000}..{largestGap:0.0000}m origin={actor.GlobalPosition} yaw={actor.Rotation.Y:0.0000}");
    }

    private async Task ObserveTurnContacts(Node3D actor, InteractionTarget[] targets, string id,
        string phase, float startYaw, float endYaw)
    {
        var extent = Mathf.Abs(Mathf.AngleDifference(startYaw, endYaw));
        if (extent < .08f) throw new InvalidOperationException($"{id}/{phase} fixture has no measurable turn.");
        var origin = actor.GlobalPosition;
        var fixedTargets = targets.ToDictionary(target => target, target => target.GlobalTransform);
        var thresholds = new[] { .25f, .50f, .75f };
        var sample = 0;
        var previousProgress = -1f;
        var until = Time.GetTicksMsec() + 720UL;
        while (Time.GetTicksMsec() < until)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (DisplayServer.GetName() != "headless")
                await Act1StateFlowProof.WaitForRenderedFrameAsync(this, $"npc/{id}/{phase}");
            var progress = Mathf.Abs(Mathf.AngleDifference(startYaw, actor.Rotation.Y)) / extent;
            if (sample >= thresholds.Length || progress < thresholds[sample]) continue;
            if (progress >= .99f || progress - previousProgress < .04f)
            {
                _supportErrors.Add($"{id}/{phase} missed intermediate turn sample {sample}; progress={progress:0.000}.");
                break;
            }
            if (actor.GlobalPosition.DistanceTo(origin) > .001f
                || targets.Any(target => !target.GlobalTransform.IsEqualApprox(fixedTargets[target])))
                _supportErrors.Add($"{id}/{phase} moved the feet origin or a ray target during the turn.");
            VerifyBootSupport(actor, targets, id, $"{phase}-{progress:0.00}", detailed: false);
            _turnContactSamples++;
            if (sample == 1) await Capture($"{id}_{phase}_mid_turn");
            previousProgress = progress;
            sample++;
        }
        if (sample != thresholds.Length)
            _supportErrors.Add($"{id}/{phase} observed {sample}/3 distinct intermediate contact poses.");
        await Frames(2);
    }

    private async Task VerifyConversationRetarget(Node3D actor, InteractionTarget[] targets, string id, float restYaw)
    {
        await VerifyStoppedFacing(actor, id + "/initial-stop");
        var probe = CreateControlCamera("NpcObserverProbe_" + id);
        try
        {
            var offset = _player.GlobalPosition - actor.GlobalPosition;
            offset.Y = 0f;
            Vector3? smallMove = null;
            foreach (var angle in new[] { 6f, -6f })
            {
                var candidate = actor.GlobalPosition + offset.Rotated(Vector3.Up, Mathf.DegToRad(angle));
                if (TryStandingControlView(actor, targets, probe, candidate, out var feet))
                { smallMove = feet; break; }
            }
            if (smallMove is null) throw new InvalidOperationException(id + "/deadzone has no clear small standing displacement: " + _standingViewRejection);
            var stillYaw = actor.Rotation.Y;
            var stillTurns = ConversationTurns(actor);
            await PutPlayer(smallMove.Value, actor.GlobalPosition + Vector3.Up * .95f);
            await SettleTurn();
            if (ConversationTurns(actor) != stillTurns || Mathf.Abs(Mathf.AngleDifference(stillYaw, actor.Rotation.Y)) > .001f)
                throw new InvalidOperationException(id + "/deadzone restarted a body turn for a six-degree displacement.");
            GD.Print($"act1-npc-tracking: {id} six-degree displacement retained yaw and turn count={stillTurns}");

            Vector3? movedObserver = null;
            float selectedAngle = 0f;
            foreach (var angle in new[] { 180f, 150f, -150f, 125f, -125f, 100f, -100f, 80f, -80f })
            {
                foreach (var radius in new[] { 2.35f, 1.9f, 1.65f })
                {
                    var candidate = actor.GlobalPosition + actor.GlobalBasis.Z.Rotated(Vector3.Up, Mathf.DegToRad(angle)) * radius;
                    if (!TryStandingControlView(actor, targets, probe, candidate, out var feet)) continue;
                    var direction = actor.GetParent<Node3D>().ToLocal(feet) - actor.Position;
                    var desiredYaw = Mathf.Atan2(direction.X, direction.Z);
                    // Keep a measurable return to the authored rest direction.
                    if (Mathf.Abs(Mathf.AngleDifference(desiredYaw, restYaw)) < .30f) continue;
                    movedObserver = feet;
                    selectedAngle = angle;
                    break;
                }
                if (movedObserver is not null) break;
            }
            if (movedObserver is null) throw new InvalidOperationException(id + "/retarget has no accessible side or back standing view.");
            var origin = actor.GlobalPosition;
            var startYaw = actor.Rotation.Y;
            var before = ConversationTurns(actor);
            await PutPlayer(movedObserver.Value, actor.GlobalPosition + Vector3.Up * .95f);
            if (!_player.IsOnFloor() || _player.IsCrouching || !_player.CanStandAt(_player.GlobalPosition)
                || _player.GlobalPosition.DistanceTo(origin) >= 2.9f)
                throw new InvalidOperationException(id + "/retarget observer is not standing inside the active conversation radius.");
            var local = actor.GetParent<Node3D>().ToLocal(_player.GlobalPosition) - actor.Position;
            var endYaw = Mathf.Atan2(local.X, local.Z);
            await ObserveTurnContacts(actor, targets, id, "retarget", startYaw, endYaw);
            var error = Mathf.Abs(Mathf.AngleDifference(actor.Rotation.Y, endYaw));
            if (error > Mathf.DegToRad(3f) || actor.GlobalPosition.DistanceTo(origin) > .001f
                || ConversationTurns(actor) != before + 1)
                throw new InvalidOperationException($"{id}/retarget missed the observer or repeatedly restarted: error={Mathf.RadToDeg(error):0.00}deg turns={before}->{ConversationTurns(actor)}.");
            GD.Print($"act1-npc-tracking: {id} standing observer moved {selectedAngle:0}deg within radius; exactly one retarget; NPC origin unchanged; local placement fixture, no traversal claim");
            await VerifyStoppedFacing(actor, id + "/after-retarget-stop");
        }
        finally
        {
            probe.QueueFree();
            await Frames(2);
        }
    }

    private async Task VerifyStoppedFacing(Node3D actor, string label)
    {
        var count = ConversationTurns(actor);
        var yaw = actor.Rotation.Y;
        await ToSignal(GetTree().CreateTimer(.90d), SceneTreeTimer.SignalName.Timeout);
        if (ConversationTurns(actor) != count || Mathf.Abs(Mathf.AngleDifference(actor.Rotation.Y, yaw)) > .001f)
            throw new InvalidOperationException(label + " restarted a turn while the observer was still.");
        GD.Print($"act1-npc-tracking: {label} 0.90s still; turn count={count}, yaw unchanged");
    }

    // The first kit shades each part with one override; the human kit keeps
    // skin, eyes and cloth as separate surfaces, each with its own override.
    private static bool HasPresentedMaterial(MeshInstance3D mesh) =>
        mesh.MaterialOverride is not null || mesh.Mesh is { } source && source.GetSurfaceCount() > 0
            && Enumerable.Range(0, source.GetSurfaceCount()).All(surface => mesh.GetSurfaceOverrideMaterial(surface) is not null);

    // The human kit's head is part of the body mesh; its eyes mark where it is.
    private static string HeadPart(string prefix) =>
        GeneratedCharacterKitDressing.UsesHumanKit(prefix) ? "_FaceEyes_" : "_Head_";

    private static long ConversationTurns(Node3D actor) => actor.GetMeta("conversationTurnCount", 0L).AsInt64();

    private Camera3D CreateControlCamera(string name)
    {
        var actual = _player.GetNode<Camera3D>("Head/Camera3D");
        var camera = new Camera3D { Name = name, Fov = actual.Fov, Near = actual.Near,
            Far = actual.Far, CullMask = actual.CullMask, Current = false };
        AddChild(camera);
        return camera;
    }

    private string _standingViewRejection = string.Empty;

    private bool TryStandingControlView(Node3D actor, InteractionTarget[] targets, Camera3D camera,
        Vector3 candidate, out Vector3 feet)
    {
        feet = candidate;
        var excluded = new global::Godot.Collections.Array<Rid>(targets.Select(target => target.GetRid())) { _player.GetRid() };
        // A person's own contact body is not something standing between the
        // observer and that person.
        foreach (var own in actor.FindChildren("*", nameof(CollisionObject3D), true, false).OfType<CollisionObject3D>())
            excluded.Add(own.GetRid());
        // The same layers a player stands on: mosque and yard floors are layer 2.
        using var groundRay = PhysicsRayQueryParameters3D.Create(candidate + Vector3.Up,
            candidate - Vector3.Up, _player.CollisionMask, excluded);
        var ground = actor.GetWorld3D().DirectSpaceState.IntersectRay(groundRay);
        if (ground.Count == 0 || ground["normal"].AsVector3().Y < .85f) { _standingViewRejection = $"no level ground at {candidate}"; return false; }
        feet = ground["position"].AsVector3() + Vector3.Up * .02f;
        if (Mathf.Abs(feet.Y - actor.GlobalPosition.Y) > .35f || !_player.CanStandAt(feet)) { _standingViewRejection = $"cannot stand at {feet}"; return false; }
        var eyeHeight = _player.GetNode<Camera3D>("Head/Camera3D").GlobalPosition.Y - _player.GlobalPosition.Y;
        camera.GlobalPosition = feet + Vector3.Up * eyeHeight;
        camera.LookAt(actor.GlobalPosition + Vector3.Up * .95f);
        foreach (var sample in CharacterViewPoints(actor))
        {
            if (!camera.IsPositionInFrustum(sample.Point)) { _standingViewRejection = $"{sample.Point} outside the view from {feet}"; return false; }
            using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, sample.Point, 3u, excluded);
            var hit = actor.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            if (hit.Count > 0 && hit["position"].AsVector3().DistanceTo(sample.Point) >= .08f)
            {
                _standingViewRejection = $"{sample.Point} hidden by {(hit["collider"].AsGodotObject() as Node)?.GetPath()} from {feet}";
                return false;
            }
        }
        return true;
    }

    private async Task CaptureProfileControl(Node3D actor, InteractionTarget[] targets, string id)
    {
        if (string.IsNullOrWhiteSpace(_captureDirectory) || DisplayServer.GetName() == "headless")
        { GD.Print($"act1-npc-profile-control: {id} external/not-run; no native capture output"); return; }
        var actualCamera = _player.GetNode<Camera3D>("Head/Camera3D");
        var playerAt = _player.GlobalPosition;
        var actorYaw = actor.Rotation.Y;
        var turns = ConversationTurns(actor);
        var camera = CreateControlCamera("NpcProfileControl_" + id);
        var overlay = new CanvasLayer { Layer = 110, Name = "NpcProfileControlCaption" };
        var caption = new Label { Position = new Vector2(16, 16),
            Text = "Контрольная камера профиля. Игрок остаётся перед собеседником." };
        caption.AddThemeColorOverride("font_shadow_color", Colors.Black);
        caption.AddThemeConstantOverride("shadow_offset_x", 2);
        caption.AddThemeConstantOverride("shadow_offset_y", 2);
        overlay.AddChild(caption);
        AddChild(overlay);
        try
        {
            var ready = false;
            foreach (var offset in new[] { -2.45f, -2.1f, -1.7f, 2.45f, 2.1f, 1.7f })
                if (TryStandingControlView(actor, targets, camera,
                    actor.GlobalPosition + actor.GlobalBasis.X * offset, out var feet))
                {
                    GD.Print($"act1-npc-profile-control: {id} eye={camera.GlobalPosition} testedStandingFeet={feet}; player={playerAt}; camera-only view, no actor/player movement");
                    ready = true;
                    break;
                }
            if (!ready) throw new InvalidOperationException(id + "/profile-control has no standing unobstructed side camera.");
            camera.MakeCurrent();
            await Frames(2);
            await Capture(id + "_profile_control");
            LogClothingSurfaceOwners(actor, camera, id);
            if (_player.GlobalPosition.DistanceTo(playerAt) > .001f
                || Mathf.Abs(Mathf.AngleDifference(actor.Rotation.Y, actorYaw)) > .001f
                || ConversationTurns(actor) != turns)
                throw new InvalidOperationException(id + "/profile-control changed the actual observer or NPC attention.");
        }
        finally
        {
            actualCamera.MakeCurrent();
            camera.QueueFree();
            overlay.QueueFree();
            await Frames(2);
        }
    }

    private void LogClothingSurfaceOwners(Node3D actor, Camera3D camera, string id)
    {
        var prefix = actor.GetMeta("characterPrefix").AsString();
        var parts = id == "naila" ? new[] { "Body", "CardiganPlacket" }
            : new[] { "Head", "Hair", "Hat" };
        var surfaces = new List<(MeshInstance3D Mesh, Vector3[] Faces, Aabb Bounds)>();
        foreach (var mesh in actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .Where(mesh => mesh.IsVisibleInTree() && parts.Any(part => mesh.Name.ToString().StartsWith(prefix + "_" + part + "_LOD", StringComparison.Ordinal))))
        {
            // Imported character positions live in the display-board frame.
            // Use the renderer's actual skin, never Mesh.GetAabb + transform.
            using var baked = mesh.BakeMeshFromCurrentSkeletonPose();
            var faces = baked.GetFaces().Select(point => mesh.GlobalTransform * point).ToArray();
            if (faces.Length == 0) throw new InvalidOperationException(mesh.Name + " has no rendered clothing surface.");
            var bounds = new Aabb(actor.ToLocal(faces[0]), Vector3.Zero);
            foreach (var point in faces) bounds = bounds.Expand(actor.ToLocal(point));
            var skin = mesh.GetSkinReference()?.GetSkin() ?? mesh.Skin
                ?? throw new InvalidOperationException(mesh.Name + " clothing has no live skin.");
            var skeleton = mesh.GetNode<Skeleton3D>(mesh.Skeleton);
            var owners = new HashSet<string>();
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                var arrays = mesh.Mesh.SurfaceGetArrays(surface);
                var bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
                var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
                for (var index = 0; index < bones.Length; index++)
                {
                    if (weights[index] <= 0f) continue;
                    var bone = skin.GetBindName(bones[index]).ToString();
                    owners.Add(bone.Length > 0 ? bone : skeleton.GetBoneName(skin.GetBindBone(bones[index])).ToString());
                }
            }
            GD.Print($"act1-npc-clothing-bounds: {id} mesh={mesh.GetPath()} bones={string.Join(",", owners)} actorLocalMin={bounds.Position} actorLocalMax={bounds.End} meshWorld={mesh.GlobalTransform}");
            if (mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal))
                surfaces.Add((mesh, faces, bounds));
        }
        var suspect = surfaces.Single(item => item.Mesh.Name.ToString().Contains(
            id == "naila" ? "CardiganPlacket" : "_Hat_", StringComparison.Ordinal));
        var pixel = camera.UnprojectPosition(actor.ToGlobal(suspect.Bounds.GetCenter()));
        var origin = camera.ProjectRayOrigin(pixel);
        var direction = camera.ProjectRayNormal(pixel);
        var hits = new List<(float Distance, string Owner, Vector3 Point)>();
        foreach (var surface in surfaces)
            for (var index = 0; index + 2 < surface.Faces.Length; index += 3)
            {
                var hit = Geometry3D.RayIntersectsTriangle(origin, direction,
                    surface.Faces[index], surface.Faces[index + 1], surface.Faces[index + 2]);
                if (hit.VariantType != Variant.Type.Vector3) continue;
                var point = hit.AsVector3();
                hits.Add((origin.DistanceTo(point), surface.Mesh.GetPath().ToString(), point));
            }
        foreach (var hit in hits.OrderBy(item => item.Distance).Take(3))
            GD.Print($"act1-npc-clothing-surface: {id} pixel={pixel} distance={hit.Distance:0.0000} owner={hit.Owner} point={hit.Point} scope=actual-LOD0-clothing-renderer");
        if (hits.Count == 0) throw new InvalidOperationException(id + " clothing owner ray missed its rendered piece.");
    }

    private async Task VerifyCharacterFamilies()
    {
        // An isolated asset check in this same test scene. These are the nine
        // prefixes used by the existing Act 1 and FullGameNpcDressing callers;
        // the fixture creates no people, progress or colliders in the game world.
        var prefixes = new[] { "Mansur", "Gulsina", "Alsu", "TimurHazrat", "CouncilElder",
            "CouncilWitness", "Naila", "ArchiveClerk", "PactKeeper" };
        var native = DisplayServer.GetName() != "headless";
        var viewport = new SubViewport
        {
            Name = "NpcFamilyRenderProbe", Size = new Vector2I(480, 480), OwnWorld3D = true,
            TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);
        var host = new Node3D { Name = "CharacterFamilyAssetOnly" };
        viewport.AddChild(host);
        host.AddChild(new WorldEnvironment
        {
            Environment = new global::Godot.Environment
            {
                BackgroundMode = global::Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0, 0, 0, 0),
                AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(.72f, .77f, .82f), AmbientLightEnergy = .65f
            }
        });
        host.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, -30, 0),
            LightEnergy = 1.1f, ShadowEnabled = false });
        var camera = new Camera3D { Name = "FamilyCamera", Current = true, Fov = 75f };
        host.AddChild(camera);
        try
        {
            foreach (var prefix in prefixes)
            {
                Node3D? actor = null;
                try
                {
                    actor = GeneratedCharacterKitDressing.Attach(host, "npc-probe-" + prefix, prefix, Vector3.Zero);
                    await Frames(3);
                    var all = actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToArray();
                    var selected = all.Where(mesh => mesh.IsVisibleInTree()).ToArray();
                    var lod0 = selected.Where(mesh => mesh.Name.ToString().Contains("_LOD0", StringComparison.Ordinal)).ToArray();
                    var lod1 = selected.Where(mesh => mesh.Name.ToString().Contains("_LOD1", StringComparison.Ordinal)).ToArray();
                    if (lod0.Length == 0 || lod0.Length != lod1.Length || selected.Length != lod0.Length + lod1.Length
                        || selected.Any(mesh => !mesh.Name.ToString().StartsWith(prefix + "_", StringComparison.Ordinal)
                            || mesh.Mesh is null || !HasPresentedMaterial(mesh)
                            || mesh.VisibilityRangeFadeMode != GeometryInstance3D.VisibilityRangeFadeModeEnum.Self)
                        || lod0.Any(mesh => !Mathf.IsEqualApprox(mesh.VisibilityRangeBegin, 0f)
                            || !Mathf.IsEqualApprox(mesh.VisibilityRangeEnd, 18f)
                            || !Mathf.IsEqualApprox(mesh.VisibilityRangeBeginMargin, 0f)
                            || !Mathf.IsEqualApprox(mesh.VisibilityRangeEndMargin, 2f))
                        || lod1.Any(mesh => !Mathf.IsEqualApprox(mesh.VisibilityRangeBegin, 14f)
                            || !Mathf.IsEqualApprox(mesh.VisibilityRangeEnd, 48f)
                            || !Mathf.IsEqualApprox(mesh.VisibilityRangeBeginMargin, 2f)
                            || !Mathf.IsEqualApprox(mesh.VisibilityRangeEndMargin, 4f)))
                        throw new InvalidOperationException("selected meshes/materials do not preserve the 0–18m / 14–48m self-fade contract");
                    if (!lod0.Select(mesh => mesh.Name.ToString().Replace("_LOD0", "_LOD1", StringComparison.Ordinal))
                        .ToHashSet(StringComparer.Ordinal).SetEquals(lod1.Select(mesh => mesh.Name.ToString())))
                        throw new InvalidOperationException("LOD0/LOD1 part pairs differ");
                    _verifiedFamilies++;
                    GD.Print($"act1-npc-family-contract: {prefix} LOD0={lod0.Length} LOD1={lod1.Length} hiddenOtherMeshes={all.Length - selected.Length} ranges=unchanged");

                    using var sheet = native ? Image.CreateEmpty(1920, 480, false, Image.Format.Rgba8) : null;
                    sheet?.Fill(Colors.Transparent);
                    var views = new List<object>();
                    var column = 0;
                    foreach (var view in new[] { (Name: "near_idle", Distance: 2.5f, Clip: "Idle"),
                        (Name: "blend_idle", Distance: 16.5f, Clip: "Idle"),
                        (Name: "lod1_idle", Distance: 20f, Clip: "Idle"),
                        (Name: "near_tension", Distance: 2.5f, Clip: "Tension") })
                    {
                        if (!GeneratedCharacterKitDressing.PlayClip(actor, view.Clip))
                            throw new InvalidOperationException($"missing authored {view.Clip} animation");
                        camera.Position = new Vector3(0, 1.0f, view.Distance);
                        camera.LookAt(new Vector3(0, .95f, 0));
                        await Frames(4);
                        if (view.Clip == "Tension")
                            await ToSignal(GetTree().CreateTimer(.32d), SceneTreeTimer.SignalName.Timeout);
                        if (!native)
                        {
                            if (view.Distance < 3f) VerifyFaceAttachments(actor, selected, prefix, view.Name, native: false);
                            continue;
                        }
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        var faceAttachments = view.Distance < 3f
                            ? VerifyFaceAttachments(actor, selected, prefix, view.Name, native: true) : null;
                        using var frame = viewport.GetTexture().GetImage();
                        if (frame.GetFormat() != Image.Format.Rgba8) frame.Convert(Image.Format.Rgba8);
                        sheet!.BlitRect(frame, new Rect2I(0, 0, 480, 480), new Vector2I(column * 480, 0));
                        var points = lod0.Where(mesh => mesh.Name.ToString().StartsWith(prefix + HeadPart(prefix), StringComparison.Ordinal)
                            || mesh.Name.ToString().Contains("_Boot", StringComparison.Ordinal))
                            .SelectMany(SkinnedWorldVertices).Select(camera.UnprojectPosition).ToArray();
                        var top = points.Min(point => point.Y);
                        var bottom = points.Max(point => point.Y);
                        var expectedHeight = bottom - top;
                        var left = Mathf.Max(0, Mathf.FloorToInt(240 - expectedHeight * .65f));
                        var right = Mathf.Min(479, Mathf.CeilToInt(240 + expectedHeight * .65f));
                        var alphaPixels = 0;
                        var drawnTop = 480;
                        var drawnBottom = -1;
                        for (var y = Mathf.Max(0, Mathf.FloorToInt(top - 6)); y <= Mathf.Min(479, Mathf.CeilToInt(bottom + 6)); y++)
                        for (var x = left; x <= right; x++)
                        {
                            if (frame.GetPixel(x, y).A < .15f) continue;
                            alphaPixels++;
                            drawnTop = Mathf.Min(drawnTop, y);
                            drawnBottom = Mathf.Max(drawnBottom, y);
                        }
                        var backgroundClear = new[] { Vector2I.Zero, new Vector2I(479, 0),
                            new Vector2I(0, 479), new Vector2I(479, 479) }.All(point => frame.GetPixelv(point).A < .01f);
                        var fullHeight = drawnBottom - drawnTop + 1;
                        var passed = backgroundClear && alphaPixels >= 40 && expectedHeight > 15f
                            && top >= 0f && bottom < 480f && fullHeight >= expectedHeight - Mathf.Max(3f, expectedHeight * .08f);
                        if (!passed) _familyErrors.Add($"{prefix}/{view.Name}: renderer silhouette has {alphaPixels} pixels, height {fullHeight}/{expectedHeight:0.0}, clearBackground={backgroundClear}");
                        else _renderedFamilyViews++;
                        views.Add(new { view.Name, view.Distance, view.Clip, column, alphaPixels,
                            expectedHeight, drawnTop, drawnBottom, backgroundClear, passed, faceAttachments });
                        GD.Print($"act1-npc-family-render: {prefix}/{view.Name} distance={view.Distance:0.0}m alphaPixels={alphaPixels} height={fullHeight}/{expectedHeight:0.0}px clearBackground={backgroundClear} pass={passed}");
                        column++;
                    }
                    if (sheet is not null && !string.IsNullOrWhiteSpace(_captureDirectory))
                    {
                        var path = System.IO.Path.Combine(_captureDirectory, $"npc_family_{prefix}.png");
                        var receipt = System.IO.Path.ChangeExtension(path, ".json");
                        if (System.IO.File.Exists(path) || System.IO.File.Exists(receipt))
                            throw new IOException($"Refusing to overwrite historical family capture: {path}");
                        var result = sheet.SavePng(path);
                        if (result != Error.Ok) throw new IOException($"Family capture failed: {result}");
                        System.IO.File.WriteAllText(receipt, System.Text.Json.JsonSerializer.Serialize(new
                        {
                            prefix, scope = "isolated asset-family renderer; no game placement or narrative claim",
                            source = GeneratedCharacterKitDressing.ScenePath, tileWidth = 480, tileHeight = 480,
                            ranges = "unchanged: LOD0 0–18m, LOD1 14–48m", views
                        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
                        GD.Print($"act1-npc-family-capture: {path}");
                    }
                }
                catch (Exception error) { _familyErrors.Add(prefix + ": " + error.Message); }
                finally
                {
                    if (actor is not null && IsInstanceValid(actor)) actor.QueueFree();
                    await Frames(3);
                }
            }
        }
        finally
        {
            viewport.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await Frames(3);
        }
        if (!native) GD.Print("act1-npc-family-render: external/not-run; headless display, asset contracts only");
    }

    private object VerifyFaceAttachments(Node3D actor, MeshInstance3D[] selected, string prefix, string pose, bool native)
    {
        // Whole-silhouette alpha and height do not catch a tiny separate face
        // at the ground origin. NPC07 showed exactly that: a parent inverse
        // canceled the source head placement before the rigid skin was added.
        // Read every feature from the actual final skin, including both LODs.
        // The first kit builds a face from separate rigid features on a
        // separate head; the human kit has one skinned head with eyes, brows,
        // hair, beard and headwear as their own meshes. Either way every
        // feature must sit on that same small head.
        var human = GeneratedCharacterKitDressing.UsesHumanKit(prefix);
        var parts = human
            ? new[] { "FaceBrows", "Hair", "Beard", "Hat", "Scarf" }
                .Where(part => selected.Any(mesh => mesh.Name.ToString() == $"{prefix}_{part}_LOD0")).ToArray()
            : new[] { "FaceEyeWhiteL", "FaceEyeWhiteR", "FaceEyeIrisL", "FaceEyeIrisR",
                "FaceNoseBridge", "FaceNoseTip", "FaceMouthLine", "EarL", "EarR" };
        var records = new List<object>();
        var worst = 0f;
        var allPassed = true;
        foreach (var lod in new[] { "LOD0", "LOD1" })
        {
            var head = selected.Single(mesh => mesh.Name.ToString() == $"{prefix}{HeadPart(prefix)}{lod}");
            var headVertices = PresentedVertices(head);
            var bounds = new Aabb(headVertices[0], Vector3.Zero);
            foreach (var point in headVertices) bounds = bounds.Expand(point);
            var headCenter = bounds.GetCenter();
            foreach (var part in parts)
            {
                var name = $"{prefix}_{part}_{lod}";
                var mesh = selected.Single(item => item.Name.ToString() == name);
                var vertices = PresentedVertices(mesh);
                var center = vertices.Aggregate(Vector3.Zero, (sum, point) => sum + point) / vertices.Count;
                var radius = vertices.Max(point => point.DistanceTo(headCenter));
                // These features occupy the same small human head. The bound
                // includes ears and nose tips, with room for the source LOD
                // simplification; a metre-scale misplaced cluster cannot pass.
                var passed = float.IsFinite(radius) && radius <= .27f;
                worst = Mathf.Max(worst, radius);
                allPassed &= passed;
                if (!passed) _familyErrors.Add($"{prefix}/{pose}/{name}: face vertices are {radius:0.0000}m from the visible head; featureCenter={center}, headCenter={headCenter}, actor={actor.GlobalPosition}");
                records.Add(new { name, center = new { x = center.X, y = center.Y, z = center.Z },
                    headCenter = new { x = headCenter.X, y = headCenter.Y, z = headCenter.Z }, maxDistance = radius, passed });
            }
        }
        GD.Print($"act1-npc-face-attachments: {prefix}/{pose} renderer={native} features={records.Count} maxDistance={worst:0.0000}m pass={allPassed}");
        return new { count = records.Count, maxDistance = worst, passed = allPassed, records };

        List<Vector3> PresentedVertices(MeshInstance3D mesh)
        {
            if (!native) return SkinnedWorldVertices(mesh);
            using var baked = mesh.BakeMeshFromCurrentSkeletonPose();
            return Enumerable.Range(0, baked.GetSurfaceCount()).SelectMany(surface =>
                baked.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                .Select(vertex => mesh.GlobalTransform * vertex).ToList();
        }
    }

    private static List<Vector3> SkinnedWorldVertices(MeshInstance3D mesh)
    {
        var skeleton = mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton)
            ?? throw new InvalidOperationException($"{mesh.Name} has no live skeleton for its support measurement.");
        var skin = mesh.GetSkinReference()?.GetSkin() ?? mesh.Skin
            ?? throw new InvalidOperationException($"{mesh.Name} has no renderer skin for its support measurement.");
        var transforms = new Transform3D[skin.GetBindCount()];
        for (var bind = 0; bind < transforms.Length; bind++)
        {
            var name = skin.GetBindName(bind).ToString();
            var bone = name.Length > 0 ? skeleton.FindBone(name) : skin.GetBindBone(bind);
            if (bone < 0 || bone >= skeleton.GetBoneCount())
                throw new InvalidOperationException($"{mesh.Name} skin bind {bind} does not resolve to a live bone.");
            transforms[bind] = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone) * skin.GetBindPose(bind);
        }
        var world = new List<Vector3>();
        for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
        {
            var arrays = mesh.Mesh.SurfaceGetArrays(surface);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
            var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
            if (vertices.Length == 0 || bones.Length != weights.Length || weights.Length % vertices.Length != 0)
                throw new InvalidOperationException($"{mesh.Name} has invalid skin arrays for support measurement.");
            var stride = weights.Length / vertices.Length;
            for (var vertex = 0; vertex < vertices.Length; vertex++)
            {
                var point = Vector3.Zero;
                var totalWeight = 0f;
                for (var influence = 0; influence < stride; influence++)
                {
                    var offset = vertex * stride + influence;
                    if (weights[offset] <= 0f) continue;
                    point += (transforms[bones[offset]] * vertices[vertex]) * weights[offset];
                    totalWeight += weights[offset];
                }
                if (totalWeight < .99f || totalWeight > 1.01f)
                    throw new InvalidOperationException($"{mesh.Name} vertex {vertex} skin weights sum to {totalWeight}.");
                world.Add(point / totalWeight);
            }
        }
        return world;
    }

    private List<(string Label, Vector3 Point)> CharacterViewPoints(Node3D actor)
    {
        var meshes = actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .Where(mesh => mesh.IsVisibleInTree() && mesh.Name.ToString().Contains("_LOD0", StringComparison.Ordinal)).ToArray();
        var prefix = actor.GetMeta("characterPrefix").AsString();
        var head = meshes.Single(mesh => mesh.Name.ToString().StartsWith(prefix + HeadPart(prefix), StringComparison.Ordinal));
        var headVertices = SkinnedWorldVertices(head);
        var points = new List<(string Label, Vector3 Point)>
        {
            (head.Name.ToString(), headVertices.Aggregate(Vector3.Zero, (sum, point) => sum + point) / headVertices.Count)
        };
        foreach (var boot in meshes.Where(mesh => mesh.Name.ToString().Contains("_Boot", StringComparison.Ordinal)))
        {
            var vertices = SkinnedWorldVertices(boot);
            var low = vertices.Min(point => point.Y);
            var bottom = vertices.Where(point => point.Y < low + .0002f).ToArray();
            points.Add((boot.Name.ToString(), bottom.Aggregate(Vector3.Zero, (sum, point) => sum + point) / bottom.Length + Vector3.Up * .05f));
        }
        return points;
    }

    private async Task<bool> PutPlayerAtClearView(Node3D actor, InteractionTarget[] targets,
        IEnumerable<Vector3> candidates, string label)
    {
        var points = CharacterViewPoints(actor);
        var excluded = new global::Godot.Collections.Array<Rid>(targets.Select(target => target.GetRid())) { _player.GetRid() };
        var bestPosition = _player.GlobalPosition;
        var bestScore = -1;
        foreach (var candidate in candidates)
        {
            await PutPlayer(candidate, actor.GlobalPosition + Vector3.Up * .95f, settlePhysicsFrames: 8);
            var camera = _player.GetNode<Camera3D>("Head/Camera3D");
            var visiblePoints = 0;
            foreach (var sample in points)
            {
                var point = sample.Point;
                var framed = camera.IsPositionInFrustum(point);
                using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, point, 3u, excluded);
                var hit = actor.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                var clearPoint = framed && (hit.Count == 0 || hit["position"].AsVector3().DistanceTo(point) < .08f);
                if (clearPoint) visiblePoints++;
                var collider = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as Node;
                var colliderPath = collider is not null ? collider.GetPath().ToString() : "none";
                var owner = collider?.GetMeta("collisionOwner", "unlabelled").AsString() ?? "none";
                var interaction = collider is InteractionTarget target ? target.InteractionId : "none";
                var hitAt = hit.Count == 0 ? point : hit["position"].AsVector3();
                GD.Print($"act1-npc-view-ray: {label}/{sample.Label} eye={camera.GlobalPosition} point={point} pixel={camera.UnprojectPosition(point)} framed={framed} clear={clearPoint} hit={hitAt} collider={colliderPath} owner={owner} interaction={interaction} excludedTargets={targets.Length}");
            }
            var standing = !_player.IsCrouching && _player.IsOnFloor();
            var clear = standing && visiblePoints == points.Count;
            GD.Print($"act1-npc-camera-candidate: {label} at={_player.GlobalPosition} standing={!_player.IsCrouching} grounded={_player.IsOnFloor()} headAndBootsUnobstructed={clear}");
            if (clear) return true;
            var score = visiblePoints + (standing ? points.Count + 1 : 0);
            if (score > bestScore) { bestScore = score; bestPosition = _player.GlobalPosition; }
        }
        _viewErrors.Add($"No physically standing and unobstructed view for {label}; inspect exact collider rays and the occluded_control capture.");
        await PutPlayer(bestPosition + Vector3.Up * .03f, actor.GlobalPosition + Vector3.Up * .95f, settlePhysicsFrames: 8);
        return false;
    }

    private async Task CaptureLodDiagnostic(Node3D actor, string name)
    {
        if (string.IsNullOrWhiteSpace(_captureDirectory) || DisplayServer.GetName() == "headless") return;
        var camera = _player.GetNode<Camera3D>("Head/Camera3D");
        var meshes = actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null).ToArray();
        var ancestors = new HashSet<ulong>();
        foreach (var mesh in meshes.Where(mesh => new[] { "_Head_", "_CoatHem_", "_BootLeft_" }
            .Any(part => mesh.Name.ToString().Contains(part, StringComparison.Ordinal))))
        {
            using var baked = mesh.BakeMeshFromCurrentSkeletonPose();
            var center = mesh.GlobalTransform * baked.GetAabb().GetCenter();
            var rawCenter = mesh.GlobalTransform * mesh.GetAabb().GetCenter();
            GD.Print($"act1-npc-lod-diagnostic: {mesh.Name} actualCenter={center} actualDistance={camera.GlobalPosition.DistanceTo(center):0.00} actualPixel={camera.UnprojectPosition(center)} actualFramed={camera.IsPositionInFrustum(center)} rawCenter={rawCenter} rawDistance={camera.GlobalPosition.DistanceTo(rawCenter):0.00} rawPixel={camera.UnprojectPosition(rawCenter)} rawFramed={camera.IsPositionInFrustum(rawCenter)} rawAabb={mesh.GetAabb()} bakedAabb={baked.GetAabb()} customAabb={mesh.CustomAabb} origin={mesh.GlobalPosition} begin={mesh.VisibilityRangeBegin} end={mesh.VisibilityRangeEnd} margins={mesh.VisibilityRangeBeginMargin}/{mesh.VisibilityRangeEndMargin}");
            for (Node? ancestor = mesh; ancestor is not null; ancestor = ancestor.GetParent())
            {
                if (ancestor is not Node3D node || !ancestors.Add(node.GetInstanceId())) continue;
                GD.Print($"act1-npc-lod-parent: path={node.GetPath()} nativeClass={node.GetClass()} visible={node.Visible} visibilityParent={node.VisibilityParent} worldPosition={node.GlobalPosition}");
            }
        }
        var ranges = meshes.Select(mesh => (Mesh: mesh, Begin: mesh.VisibilityRangeBegin, End: mesh.VisibilityRangeEnd,
            BeginMargin: mesh.VisibilityRangeBeginMargin, EndMargin: mesh.VisibilityRangeEndMargin,
            Bounds: mesh.CustomAabb, Visible: mesh.Visible)).ToArray();
        try
        {
            // Keep the authored ranges for this first comparison. The raw
            // resource AABB is not the renderer's skeleton-updated AABB; an
            // actual baked bound distinguishes that cause from a LOD choice.
            foreach (var entry in ranges)
            {
                using var baked = entry.Mesh.BakeMeshFromCurrentSkeletonPose();
                entry.Mesh.CustomAabb = baked.GetAabb();
            }
            await Frames(2);
            await Capture(name + "_baked_bounds_diagnostic");

            foreach (var entry in ranges)
            {
                entry.Mesh.CustomAabb = entry.Bounds;
                entry.Mesh.VisibilityRangeBegin = 0f;
                entry.Mesh.VisibilityRangeEnd = 0f;
                entry.Mesh.VisibilityRangeBeginMargin = 0f;
                entry.Mesh.VisibilityRangeEndMargin = 0f;
            }
            foreach (var lod in new[] { "_LOD0", "_LOD1" })
            {
                foreach (var entry in ranges)
                    entry.Mesh.Visible = entry.Visible && entry.Mesh.Name.ToString().Contains(lod, StringComparison.Ordinal);
                await Frames(2);
                await Capture(name + "_unranged" + lod.ToLowerInvariant() + "_diagnostic");
            }
        }
        finally
        {
            foreach (var entry in ranges)
            {
                entry.Mesh.VisibilityRangeBegin = entry.Begin;
                entry.Mesh.VisibilityRangeEnd = entry.End;
                entry.Mesh.VisibilityRangeBeginMargin = entry.BeginMargin;
                entry.Mesh.VisibilityRangeEndMargin = entry.EndMargin;
                entry.Mesh.CustomAabb = entry.Bounds;
                entry.Mesh.Visible = entry.Visible;
            }
        }
    }

    private async Task PutPlayer(Vector3 position, Vector3 lookAt, int settlePhysicsFrames = 3)
    {
        _player.ApplyPortableTransform(new PlayerTransform(
            new(position.X, position.Y, position.Z), new(0, 0, 0)));
        // The real placement API briefly restores a crouched capsule until
        // physics can test the destination. Let that existing mechanism finish
        // before freezing presentation; use the live camera, never a guessed
        // standing eye height (which cropped NPC heads in npc-02 captures).
        _player.SetPhysicsProcess(true);
        for (var frame = 0; frame < settlePhysicsFrames; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        _player.SetPhysicsProcess(false);
        var camera = _player.GetNode<Camera3D>("Head/Camera3D");
        var delta = lookAt - camera.GlobalPosition;
        var horizontal = new Vector2(delta.X, delta.Z).Length();
        _player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y, horizontal)),
            Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
        // Refresh the actual interaction ray after aiming. Leaving physics
        // frozen here kept the previous camera's NPC prompt on control frames.
        _player.SetPhysicsProcess(true);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await Frames(1);
        _player.SetPhysicsProcess(false);
        GD.Print($"act1-npc-camera: feet={_player.GlobalPosition} eye={camera.GlobalPosition} crouched={_player.IsCrouching} lookAt={lookAt}");
    }

    private async Task Capture(string name)
    {
        if (string.IsNullOrWhiteSpace(_captureDirectory) || DisplayServer.GetName() == "headless") return;
        var path = System.IO.Path.Combine(_captureDirectory, $"npc_{name}.png");
        if (System.IO.File.Exists(path)) throw new IOException($"Refusing to overwrite historical NPC capture: {path}");
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "npc/" + name);
        using var picture = GetViewport().GetTexture().GetImage();
        var result = picture.SavePng(path);
        if (result != Error.Ok) throw new IOException($"NPC capture failed: {path} ({result}).");
        GD.Print($"act1-npc-capture: {path}");
    }

    private async Task SettleTurn()
    {
        await ToSignal(GetTree().CreateTimer(.62d), SceneTreeTimer.SignalName.Timeout);
        await Frames(2);
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
