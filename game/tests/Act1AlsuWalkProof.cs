using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>Actual player input and the one authored companion, never a flag fixture.</summary>
internal static class Act1AlsuWalkProof
{
    internal static async Task<bool> CompleteAsync(Node owner, RuntimeBridge bridge, bool verifyReturn = false)
    {
        var tree = owner.GetTree();
        FirstPersonController? player = null;
        try
        {
            var dialogue = tree.GetFirstNodeInGroup("dialogue_ui") as DialogueUi
                ?? throw new InvalidOperationException("Alsu walk has no conversation UI.");
            player = tree.GetFirstNodeInGroup("player_controller") as FirstPersonController
                ?? throw new InvalidOperationException("Alsu walk has no player.");
            var main = tree.GetFirstNodeInGroup("zone_manager") as Main
                ?? throw new InvalidOperationException("Alsu walk has no current world.");
            var camera = player.GetNode<Camera3D>("Head/Camera3D");
            var ray = camera.GetNode<RayCast3D>("InteractionRay");
            var compact = main.ConnectedWorld is null;
            var stepCaptured = false;
            var walkLeg = 0;
            await Frames(3);
            var walk = FindWalk();
            if (walk.Arrived || Known("clue_alsu_heard_versions")) return true;
            Check(bridge.CurrentZoneId == "village_day", "Alsu's walk requires the actual village street.");
            await Close();
            var revisions = player.PresentationTransformRevision;
            var falls = player.FallRecoveries;
            var npcStart = walk.Actor.GlobalPosition;
            var invitationBefore = Known("alsu_walk_invitation");
            if (!invitationBefore)
            {
                var requestedBefore = WalkRequested();
                Check(!await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/alsu_route_context", "walk-invitation", "walk-with-alsu")
                    && !await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/alsu_route_context", "walk-start-reply")
                    && WalkRequested() == requestedBefore,
                    "The companion was requested before the invitation was actually heard.");
            }
            Check(!await bridge.AdvanceAlsuStreetWalkAsync(1) && !await bridge.AdvanceAlsuStreetWalkAsync(3),
                "An unrequested or distant checkpoint committed without the companion's actual travel.");
            Check(!await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/alsu_route_context", "name-road", "ask-versions")
                && !Known("clue_alsu_heard_versions"), "A direct question bypassed the shared street encounter.");

            // Follow the already-authored exit through the same yard gate when
            // a state-flow caller has just returned from the house. No positioning
            // fixture is used, including in the physical corridor/walkthrough.
            if (main.ConnectedWorld is not null && player.GlobalPosition.X < -4)
            {
                // Turn toward the same open gate before the packed hay surface.
                // Alsu06 measured its contact at z=2.3147; z=2.6 is not a
                // standing destination even though the old .30 m route stopped short.
                // Alsu07's diagonal crossed the parked Niva's real front face.
                // Cross in front at z=-2.8, then use the street side x=-.1:
                // the chassis ends at x=-.74, leaving .64 m for the .35 m body.
                // Continue past the initial NPC before approaching her from behind.
                var exit = new Vector2[] { new(-26.05f,.2f),new(-26.05f,2.2f),new(-25.2f,2.6f),
                    new(-22.5f,2.4f),new(-16,-.5f),new(-8,-3.5f),new(-3.5f,-2.8f),
                    new(-.1f,-2.8f),new(-.1f,3.7f) };
                var closest = Enumerable.Range(0, exit.Length).MinBy(i =>
                    exit[i].DistanceTo(new(player.GlobalPosition.X, player.GlobalPosition.Z)));
                foreach (var point in exit.Skip(closest))
                    await WalkTo(new(point.X, player.GlobalPosition.Y, point.Y),
                        verifyStandingGoal: point == new Vector2(-26.05f,2.2f) || point.X == -.1f,
                        standingPurpose: point.X == -.1f ? "niva-bypass" : "yard-turn");
            }
            await WalkTo(walk.Actor.GlobalPosition + Vector3.Back * 1.65f);
            Check(Horizontal(walk.Actor.GlobalPosition, npcStart) < .02f && Known("alsu_walk_invitation") == invitationBefore,
                "Approaching Alsu automatically invited, moved, or informed the player.");
            await Talk();
            Check(Known("alsu_walk_invitation") && !Known("clue_alsu_heard_versions"),
                "The actual invitation either did not reach the notebook or supplied the later account.");
            var requestedBeforeTerminal = WalkRequested();
            Check(await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/alsu_route_context", "walk-start-reply")
                && WalkRequested() == requestedBeforeTerminal && !Known("clue_alsu_heard_versions"),
                "The pure reply node manufactured consent or the later account without the player's choice.");
            await Capture("alsu_invitation");
            if (verifyReturn)
            {
                await Choose("choice-alsu-walk-later");
                await Close();
                await RoundTrip("len01-alsu-walk-deferred");
                Check(walk.Checkpoint == 0 && !walk.Arrived && !Known("clue_alsu_heard_versions"),
                    "A declined walk advanced or lost its honest return after load.");
                await Talk();
            }
            await Choose("choice-alsu-walk-together");
            await Close();
            if (verifyReturn || compact)
            {
                await FollowUntil(1, stopAfterMetres: 1.4f);
                Check(walk.Checkpoint == 0 && !walk.Arrived,
                    "The mid-segment check accidentally started at a reached checkpoint.");
                if (verifyReturn)
                {
                    Check(await bridge.SaveSlotAsync("len01-alsu-midsegment"), "Walking save failed before the first checkpoint.");
                    var pose = bridge.SelectWorldProps().GetProperty(AlsuStreetWalkPresentation.SavePropId);
                    var expected = SavedPosition(pose);
                    var travelled = pose.GetProperty("segmentTravel").GetSingle();
                    Check(travelled > .8f && travelled < 2.4f && pose.GetProperty("checkpoint").GetInt32() == 0,
                        "The native walking snapshot did not capture an actual partial first segment.");
                    Check(await bridge.LoadSlotAsync("len01-alsu-midsegment"), "The actual mid-segment save was rejected.");
                    walk = FindWalk();
                    Check(WalkRequested(), "Loading the actual walking snapshot lost the accepted invitation.");
                    // Inspect in the load continuation, before another gameplay
                    // tick can resume the willingly accompanied movement.
                    Check(walk.Actor.GlobalPosition.DistanceTo(expected) < .025f
                        && walk.Checkpoint == 0 && !walk.Arrived && player.CanCrouchAt(player.GlobalPosition)
                        && Horizontal(walk.Actor.GlobalPosition,player.GlobalPosition) > .6f,
                        "Mid-segment projection rewound the companion into the loaded player.");
                    await Talk();
                    await Choose("choice-alsu-walk-later");
                    await Close();
                    Check(walk.Checkpoint == 0 && !walk.Arrived,
                        "The mid-segment refusal happened after a whole segment was already reached.");
                    await Capture("alsu_midsegment_restored");
                    // Corrupt only this proof's new negative slot. The original
                    // reachable snapshot and all user slots remain untouched.
                    var codec = new Urman.Core.Persistence.SaveGameV3Codec();
                    var originalPath = ProjectSettings.GlobalizePath("user://savegames/len01-alsu-midsegment.savegame-v3.json");
                    var original = codec.Decode(File.ReadAllBytes(originalPath));
                    var badState = System.Text.Json.Nodes.JsonNode.Parse(original.Runtime.State.GetRawText())!;
                    var badPosition = badState["world.props"]![AlsuStreetWalkPresentation.SavePropId]!["position"]!;
                    badPosition["x"] = expected.X + 2;
                    var corrupted = original with { Runtime = original.Runtime with
                        { State = System.Text.Json.JsonSerializer.SerializeToElement(badState) } };
                    await RejectSavedPose(corrupted,"len01-alsu-invalid-pose");
                    await Capture("alsu_invalid_pose_rollback");
                    // A second DTO stays on the valid road with matching travel
                    // and ground, but overlaps the saved player's real capsule.
                    // This reaches physical projection rather than the path guard.
                    var savedPlayer = new Vector3((float)original.PlayerTransform.Position.X,
                        (float)original.PlayerTransform.Position.Y,(float)original.PlayerTransform.Position.Z);
                    var start = AlsuStreetWalkPresentation.Waypoints[0];
                    var segment = AlsuStreetWalkPresentation.Waypoints[1]-start;
                    var along = Math.Clamp((new Vector2(savedPlayer.X,savedPlayer.Z)-start).Dot(segment.Normalized()),0,segment.Length());
                    var onPath = start+segment.Normalized()*along;
                    var overlapAt = new Vector3(onPath.X,savedPlayer.Y,onPath.Y);
                    using(var groundQuery = PhysicsRayQueryParameters3D.Create(overlapAt+Vector3.Up*.7f,
                        overlapAt-Vector3.Up*.8f,3,new global::Godot.Collections.Array<Rid>
                        {player.GetRid(),walk.Target.GetRid(),walk.GetNode<AnimatableBody3D>("AlsuPhysicalContact").GetRid()}))
                    {
                        var support = player.GetWorld3D().DirectSpaceState.IntersectRay(groundQuery);
                        Check(support.Count>0 && support["normal"].AsVector3().Y>.85f,
                            "Physical-overlap negative fixture has no real street support.");
                        overlapAt.Y = support["position"].AsVector3().Y;
                    }
                    Check(Horizontal(overlapAt,savedPlayer)<.5f,"The collision-negative pose does not overlap the saved player.");
                    var overlapState=System.Text.Json.Nodes.JsonNode.Parse(original.Runtime.State.GetRawText())!;
                    var overlap=overlapState["world.props"]![AlsuStreetWalkPresentation.SavePropId]!;
                    overlap["position"]=new System.Text.Json.Nodes.JsonObject
                        {["x"]=overlapAt.X,["y"]=overlapAt.Y,["z"]=overlapAt.Z};
                    overlap["segmentTravel"]=along;
                    overlap["pendingCheckpoint"]=0;
                    await RejectSavedPose(original with {Runtime=original.Runtime with
                        {State=System.Text.Json.JsonSerializer.SerializeToElement(overlapState)}},"len01-alsu-overlap-pose");
                    await Capture("alsu_overlap_pose_rollback");
                }
                if (compact)
                {
                    // Exercise the existing compact transition boundary itself;
                    // mapped player spawns are declared here, never NPC progress.
                    Check(await main.SwitchZoneAsync("house_old_pc","entry"), "Compact departure did not flush the walking companion.");
                    var pose = bridge.SelectWorldProps().GetProperty(AlsuStreetWalkPresentation.SavePropId);
                    var expected = SavedPosition(pose);
                    await Frames(3);
                    Check(await main.SwitchZoneAsync("village_day","arrival"), "Compact street return failed.");
                    await Frames(3);
                    walk = FindWalk();
                    Check(walk.Actor.GlobalPosition.DistanceTo(expected) < .025f && walk.Checkpoint == 0 && !walk.Arrived,
                        "Recreating the compact street rewound the actual partial companion pose.");
                    await WalkTo(walk.Actor.GlobalPosition + Vector3.Back * 1.65f);
                }
                if (verifyReturn && !compact)
                {
                    // This declared local transition checks the real interior
                    // physics/save lifecycle, not traversal of the house door.
                    // The partial walk and refusal above were actual player actions.
                    var beforeHouse = walk.Actor.GlobalPosition;
                    Check(await main.SwitchZoneAsync("house_old_pc", "entry"), "The existing house transition failed.");
                    await Frames(3);
                    Check(!walk.PhysicalAccessReady && walk.PhysicalPlacementFailure is null
                        && walk.GetNode<AnimatableBody3D>("AlsuPhysicalContact").CollisionLayer == 0
                        && walk.Target.CollisionLayer == 0 && !walk.Target.IsAvailable(),
                        "The inactive exterior companion retained physics or conversation access indoors.");
                    Check(await bridge.SaveSlotAsync("len01-alsu-midsegment-house"), "Saving the partial companion indoors failed.");
                    var indoorPose = bridge.SelectWorldProps().GetProperty(AlsuStreetWalkPresentation.SavePropId);
                    var expected = SavedPosition(indoorPose);
                    Check(expected.DistanceTo(beforeHouse) < .025f && indoorPose.GetProperty("checkpoint").GetInt32() == 0,
                        "Entering the house replaced the actual partial companion position.");
                    Check(await bridge.LoadSlotAsync("len01-alsu-midsegment-house"), "Valid indoor companion placement was rejected.");
                    walk = AlsuStreetWalkPresentation.SessionOwner(tree)
                        ?? throw new InvalidOperationException("Indoor load lost the persistent companion owner.");
                    Check(!WalkRequested(), "Indoor load cancelled the player's explicit decision to continue later.");
                    Check(bridge.CurrentZoneId == "house_old_pc" && walk.Actor.GlobalPosition.DistanceTo(expected) < .025f
                        && walk.Checkpoint == 0 && !walk.Arrived && !walk.PhysicalAccessReady
                        && walk.PhysicalPlacementFailure is null
                        && walk.GetNode<AnimatableBody3D>("AlsuPhysicalContact").CollisionLayer == 0
                        && walk.Target.CollisionLayer == 0 && !walk.Target.IsAvailable(),
                        "Indoor load invented support, rewound the pose, or enabled exterior interaction.");
                    await Capture("alsu_midsegment_loaded_inside_house");
                    Check(await main.SwitchZoneAsync("village_day", "from_house"), "The existing street return failed.");
                    var returnFrames = 0;
                    void RecordExteriorReturn(string phase) => GD.Print("alsu-exterior-return: "
                        + System.Text.Json.JsonSerializer.Serialize(new
                        {
                            phase, waitedPhysicsFrames = returnFrames, expected = expected.ToString(),
                            poseDelta = walk.Actor.GlobalPosition.DistanceTo(expected), requested = WalkRequested(),
                            bodyLayer = walk.GetNode<AnimatableBody3D>("AlsuPhysicalContact").CollisionLayer,
                            targetLayer = walk.Target.CollisionLayer, targetAvailable = walk.Target.IsAvailable(),
                            eligibility = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(walk.DescribeWalkEligibility())
                        }));
                    void CheckStoppedCompanion()
                    {
                        Check(walk.PhysicalPlacementFailure is null && !WalkRequested()
                            && walk.Actor.GlobalPosition.DistanceTo(expected) < .025f
                            && walk.Checkpoint == 0 && !walk.Arrived,
                            "Exterior return changed or rejected the stopped companion: " + walk.DescribeWalkEligibility());
                        if (!walk.PhysicalAccessReady)
                            Check(walk.GetNode<AnimatableBody3D>("AlsuPhysicalContact").CollisionLayer == 0
                                && walk.Target.CollisionLayer == 0 && !walk.Target.IsAvailable(),
                                "Exterior return exposed the companion before validating re-enabled physics.");
                    }
                    RecordExteriorReturn("immediate");
                    try
                    {
                        Check(!walk.PhysicalAccessReady && !walk.Target.IsAvailable(),
                            "Exterior return exposed the companion before validating re-enabled physics.");
                        // Observe the ordinary owner. PhysicsFrame resumes before
                        // node callbacks, and the companion also waits for Fleet.
                        // A fixed three-frame delay is not this readiness contract.
                        while (returnFrames < 120 && !walk.PhysicalAccessReady)
                        {
                            CheckStoppedCompanion();
                            await Frames(1);
                            returnFrames++;
                            if (returnFrames == 3) RecordExteriorReturn("frame3");
                            CheckStoppedCompanion();
                        }
                        Check(walk.PhysicalAccessReady,
                            "Exterior companion readiness timed out: " + walk.DescribeWalkEligibility());
                        CheckStoppedCompanion();
                        RecordExteriorReturn("complete");
                    }
                    catch
                    {
                        RecordExteriorReturn("failure");
                        try { await Capture("alsu_exterior_return_failure"); }
                        catch (Exception imageError) { GD.Print("alsu-exterior-return capture failed: " + imageError.Message); }
                        throw;
                    }
                    GD.Print("alsu-walk-proof: actual partial walk -> declared existing house boundary -> indoor save/load -> actual exterior support/body validation; door traversal=separate-check");
                    foreach (var point in new Vector2[] { new(-26.05f,.2f), new(-26.05f,2.2f),
                        new(-25.2f,2.6f), new(-22.5f,2.4f), new(-16,-.5f), new(-8,-3.5f), new(-3.5f,-2.8f),
                        new(-.1f,-2.8f), new(-.1f,3.7f) })
                        await WalkTo(new(point.X, player.GlobalPosition.Y, point.Y),
                            verifyStandingGoal: point == new Vector2(-26.05f,2.2f) || point.X == -.1f,
                            standingPurpose: point.X == -.1f ? "niva-bypass" : "yard-turn");
                }
                if (verifyReturn)
                {
                    await Talk();
                    await Choose("choice-alsu-walk-together");
                    await Close();
                }
            }
            await FollowUntil(verifyReturn ? 1 : 3);
            if (verifyReturn)
            {
                Check(!await bridge.AdvanceAlsuStreetWalkAsync(3), "A later checkpoint skipped the unwalked street.");
                await Talk();
                await Choose("choice-alsu-walk-later");
                await Close();
                var parked = walk.Actor.GlobalPosition;
                var partial = walk.Checkpoint;
                await WalkTo(player.GlobalPosition + Vector3.Back * 5.5f);
                await Frames(15);
                Check(Horizontal(parked, walk.Actor.GlobalPosition) < .02f && walk.Checkpoint == partial,
                    "Alsu continued walking after an explicit refusal or the player's departure.");
                await Capture("alsu_waiting_after_departure");
                await RoundTrip("len01-alsu-walk-partial");
                Check(walk.Checkpoint == partial && !walk.Arrived && !Known("clue_alsu_heard_versions"),
                    "Partial save/load completed an unwalked segment or supplied the account.");
                await WalkTo(walk.Actor.GlobalPosition + Vector3.Back * 1.65f);
                await Talk();
                await Capture("alsu_return_after_load");
                await Choose("choice-alsu-walk-together");
                await Close();
                await FollowUntil(3);
            }
            Check(walk.Arrived && walk.Checkpoint == 3 && !Known("clue_alsu_heard_versions")
                && !Known("clue_marat_versions_conflict"), "Physical arrival fabricated either narrative conclusion.");
            Check(player.FallRecoveries == falls, "Following the companion required a fall recovery.");
            if (!verifyReturn && !compact) Check(player.PresentationTransformRevision == revisions,
                "The shared walk used a presentation teleport instead of player movement.");
            Check(bridge.JournalEntries().Count(entry => entry.EntryId == "urman.chapter1:knowledge/alsu_walk_invitation") == 1,
                "Refusal, return, or load duplicated the actual invitation.");
            await Talk();
            Check(Button("choice-alsu-versions") is not null,
                "The old substantive question was unavailable after reaching the intersection together.");
            await Capture("alsu_at_existing_turn");
            await Close();
            if (verifyReturn)
            {
                await RoundTrip("len01-alsu-walk-arrived");
                Check(walk.Arrived && !Known("clue_alsu_heard_versions"),
                    "Loading the completed walk lost the meeting or silently answered its question.");
            }
            GD.Print($"alsu-walk-proof: requested, physical input, one actor/body/ray, checkpoint={walk.Checkpoint}; return={verifyReturn}; no account until manual question; human-duration=not-run");
            return true;

            AlsuStreetWalkPresentation FindWalk() => AlsuStreetWalkPresentation.Current(tree)
                ?? throw new InvalidOperationException("The authored visible Alsu has no shared walk component.");
            Vector3 SavedPosition(System.Text.Json.JsonElement record)
            {
                var at = record.GetProperty("position");
                return new(at.GetProperty("x").GetSingle(),at.GetProperty("y").GetSingle(),at.GetProperty("z").GetSingle());
            }
            async Task RejectSavedPose(Urman.Core.Persistence.SaveGameV3 corrupted,string slot)
            {
                var path=ProjectSettings.GlobalizePath("user://savegames/"+slot+".savegame-v3.json");
                using(var output=new FileStream(path,FileMode.CreateNew,System.IO.FileAccess.Write,FileShare.None))
                    output.Write(new Urman.Core.Persistence.SaveGameV3Codec().Encode(corrupted));
                var before=walk.Actor.GlobalPosition;
                var playerBefore=player.GlobalPosition;
                var checkpointBefore=walk.Checkpoint;
                var requestedBefore=WalkRequested();
                Check(!await bridge.LoadSlotAsync(slot),"Invalid companion projection was accepted: "+slot);
                walk=FindWalk();
                Check(walk.Actor.GlobalPosition.DistanceTo(before)<.025f && player.GlobalPosition.DistanceTo(playerBefore)<.025f
                    && player.CanCrouchAt(player.GlobalPosition) && !player.ModalOpen
                    && walk.Checkpoint==checkpointBefore && WalkRequested()==requestedBefore
                    && !walk.Arrived && !Known("clue_alsu_heard_versions"),
                    "Rejected companion projection did not roll back the physical scene and input: "+slot);
            }
            bool Known(string id) => bridge.SelectRuntimeState().GetProperty("knowledge")
                .TryGetProperty("urman.chapter1:knowledge/" + id, out var entry)
                && entry.GetProperty("status").GetString() == "confirmed";
            bool WalkRequested() => bridge.SelectRuntimeState().TryGetProperty("npc", out var people)
                && people.TryGetProperty(AlsuStreetWalkPresentation.CharacterId, out var person)
                && person.TryGetProperty("walk_requested", out var requested)
                && requested.ValueKind == System.Text.Json.JsonValueKind.True;
            async Task Frames(int count)
            {
                for (var i = 0; i < count; i++) await owner.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            async Task Aim(Vector3 point)
            {
                for (var i = 0; i < 6; i++)
                {
                    var d = point - camera.GlobalPosition;
                    player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(d.Y, new Vector2(d.X,d.Z).Length())),
                        Mathf.RadToDeg(Mathf.Atan2(-d.X,-d.Z)));
                }
                await Frames(3);
            }
            async Task Talk()
            {
                Release();
                if (dialogue.IsOpen) await Close();
                await WalkTo(walk.Actor.GlobalPosition + Vector3.Back * 1.65f);
                await Aim(walk.Target.GlobalPosition);
                ray.ForceRaycastUpdate();
                using var query = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition,
                    camera.GlobalPosition - camera.GlobalBasis.Z * ray.TargetPosition.Length(), ray.CollisionMask,
                    new global::Godot.Collections.Array<Rid> { player.GetRid() });
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(query);
                Check(ray.GetCollider() == walk.Target && hit.Count > 0
                    && hit["collider"].AsGodotObject() == walk.Target,
                    "The current -camera.Basis.Z ray did not reach the same moving Alsu target.");
                Check(walk.Target.CollisionLayer == 4 && walk.Target.CollisionMask == 0
                    && walk.GetNode<AnimatableBody3D>("AlsuPhysicalContact").CollisionLayer == 1,
                    "The ray target still duplicates or replaces the physical body.");
                Input.ParseInputEvent(new InputEventKey { Keycode=Key.E,PhysicalKeycode=Key.E,Pressed=true });
                await Frames(2);
                Input.ParseInputEvent(new InputEventKey { Keycode=Key.E,PhysicalKeycode=Key.E,Pressed=false });
                for (var i=0;i<180 && !dialogue.IsOpen;i++) await Frames(1);
                await Frames(3);
                Check(dialogue.IsOpen && player.ModalOpen, "Physical E did not open Alsu's current conversation.");
            }
            Button? Button(string id) => dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices").GetChildren()
                .OfType<Button>().SingleOrDefault(button => !button.Disabled
                    && button.Text == bridge.ResolveText("urman.chapter1:text/" + id));
            async Task Choose(string id)
            {
                var button = Button(id) ?? throw new InvalidOperationException("Missing visible Alsu choice: " + id);
                button.EmitSignal(global::Godot.Button.SignalName.Pressed);
                await Frames(8);
                if (id is "choice-alsu-walk-together" or "choice-alsu-walk-later")
                {
                    var requested = id == "choice-alsu-walk-together";
                    var replyId = requested ? "dialogue-alsu-walk-start" : "dialogue-alsu-walk-later";
                    Check(WalkRequested() == requested && dialogue.IsOpen
                        && dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text
                            == bridge.ResolveText("urman.chapter1:text/" + replyId),
                        "The actual visible choice did not commit its consent state and reply: " + id
                            + "; " + walk.DescribeWalkEligibility());
                }
            }
            async Task Close()
            {
                var requested = WalkRequested();
                if (dialogue.IsOpen) dialogue._UnhandledInput(new InputEventKey { Keycode=Key.Escape,PhysicalKeycode=Key.Escape,Pressed=true });
                await Frames(3);
                Check(!dialogue.IsOpen && !player.ModalOpen, "Closing the companion conversation retained input.");
                Check(WalkRequested() == requested, "Closing the conversation changed the player's walking decision.");
            }
            async Task WalkTo(Vector3 goal, bool verifyStandingGoal = false, string standingPurpose = "yard-turn")
            {
                if (verifyStandingGoal)
                {
                    using var floorQuery=PhysicsRayQueryParameters3D.Create(
                        goal+Vector3.Up*2,goal-Vector3.Up*2,player.CollisionMask,
                        new global::Godot.Collections.Array<Rid> { player.GetRid() });
                    var floor=player.GetWorld3D().DirectSpaceState.IntersectRay(floorQuery);
                    Check(floor.Count>0 && floor["normal"].AsVector3().Y>.9f,
                        "The standing route point has no actual walkable support: "+standingPurpose+" "+goal);
                    goal=floor["position"].AsVector3()+Vector3.Up*.01f;
                    var standingClear=player.CanStandAt(goal);
                    var chassis=standingPurpose=="niva-bypass"
                        ? main.ConnectedWorld?.FindChild("babay-niva",true,false)?.GetNodeOrNull<CollisionShape3D>("ChassisCollision")
                        : null;
                    Check(standingPurpose!="niva-bypass" || chassis?.Shape is BoxShape3D,
                        "The pedestrian bypass lost the actual parked Niva chassis.");
                    GD.Print("alsu-"+standingPurpose+": "+System.Text.Json.JsonSerializer.Serialize(new
                    {
                        goal=goal.ToString(),standingClear,standingPurpose,bodyRadius=player.BodyRadius,
                        support=(floor["collider"].AsGodotObject() as Node)?.GetPath().ToString(),
                        chassisOwner=chassis?.GetPath().ToString(),chassisTransform=chassis?.GlobalTransform.ToString(),
                        chassisSize=(chassis?.Shape as BoxShape3D)?.Size.ToString(),
                        playerMoved=false,revision=player.PresentationTransformRevision
                    }));
                    Check(standingClear,"The route point does not fit the actual full standing capsule: "+standingPurpose+" "+goal);
                }
                var revision=player.PresentationTransformRevision;
                var start=player.GlobalPosition;
                var leg=++walkLeg;
                var contacts=new Queue<string>();
                var lastMovingPosition=start;
                var stalledFrames=0;
                var elapsedFrames=0;
                void RecordWalk(string phase)
                {
                    var d=goal-player.GlobalPosition;
                    var motion=new Vector3(d.X,0,d.Z).Normalized()
                        * player.WalkSpeed * (float)player.GetPhysicsProcessDeltaTime();
                    using var sweep=new KinematicCollision3D();
                    var blocked=player.TestMove(player.GlobalTransform,motion,sweep,.001f,false,4);
                    var sweepContacts=new List<object>();
                    if(blocked)
                        for(var index=0;index<sweep.GetCollisionCount();index++)
                            sweepContacts.Add(new
                            {
                                owner=(sweep.GetCollider(index) as Node)?.GetPath().ToString(),
                                shape=(sweep.GetColliderShape(index) as Node)?.GetPath().ToString(),
                                normal=sweep.GetNormal(index).ToString(),point=sweep.GetPosition(index).ToString()
                            });
                    GD.Print("alsu-player-walk: "+System.Text.Json.JsonSerializer.Serialize(new
                    {
                        phase,leg,elapsedFrames,stalledFrames,start=start.ToString(),goal=goal.ToString(),
                        feet=player.GlobalPosition.ToString(),velocity=player.Velocity.ToString(),
                        remaining=Horizontal(player.GlobalPosition,goal),onFloor=player.IsOnFloor(),
                        crouching=player.IsCrouching,modal=player.ModalOpen,zone=bridge.CurrentZoneId,
                        input=Input.GetVector("move_left","move_right","move_forward","move_backward").ToString(),
                        revisionBefore=revision,revisionNow=player.PresentationTransformRevision,
                        lastStepRejection=player.LastStepRejection,stepsClimbed=player.StepsClimbed,
                        recentSlideContacts=contacts.ToArray(),probeMotion=motion.ToString(),blocked,sweepContacts
                    }));
                }
                try
                {
                    for(var frame=0;frame<720 && Horizontal(player.GlobalPosition,goal)>.15f;frame++)
                    {
                        var d=goal-player.GlobalPosition;
                        player.ApplySmokeLook(0,Mathf.RadToDeg(Mathf.Atan2(-d.X,-d.Z)));
                        Input.ActionPress("move_forward",Horizontal(player.GlobalPosition,goal)<.4f?.3f:1);
                        await Frames(1);
                        elapsedFrames=frame+1;
                        for(var index=0;index<player.GetSlideCollisionCount();index++)
                        {
                            var hit=player.GetSlideCollision(index);
                            if(hit.GetNormal().Y>.9f) continue;
                            var contact=$"owner={(hit.GetCollider() as Node)?.GetPath()} "
                                +$"shape={(hit.GetColliderShape() as Node)?.GetPath()} normal={hit.GetNormal()} point={hit.GetPosition()}";
                            if(contacts.Count==0 || contacts.Last()!=contact)
                            {
                                contacts.Enqueue(contact);
                                while(contacts.Count>8) contacts.Dequeue();
                            }
                        }
                        if(Horizontal(lastMovingPosition,player.GlobalPosition)>.02f)
                        { lastMovingPosition=player.GlobalPosition;stalledFrames=0; }
                        else stalledFrames++;
                        if(stalledFrames is 30 or 90) RecordWalk("stalled-input-held");
                    }
                }
                finally { Release(); }
                await Frames(3);
                var reached=Horizontal(player.GlobalPosition,goal)<.24f && player.IsOnFloor()
                    && player.PresentationTransformRevision==revision;
                if(!reached)
                {
                    RecordWalk("failed-after-release");
                    try { await Capture($"alsu_player_route_blocked_{leg:00}"); }
                    catch(Exception imageError) { GD.Print("alsu-player-walk capture failed: "+imageError.Message); }
                }
                Check(reached,"Real walking could not reach the companion approach: "+goal
                    +"; actual="+player.GlobalPosition+"; contacts="+string.Join(" | ",contacts)
                    +"; step="+player.LastStepRejection);
                if(verifyStandingGoal)
                {
                    Check(!player.IsCrouching && player.CanStandAt(player.GlobalPosition),
                        "The actual route point did not retain a clear standing capsule: "+standingPurpose);
                    RecordWalk("standing-"+standingPurpose+"-reached");
                    await Capture($"alsu_{standingPurpose.Replace('-', '_')}_clear_{leg:00}");
                }
            }
            async Task FollowUntil(int checkpoint, float? stopAfterMetres = null)
            {
                var initial = walk.Actor.GlobalPosition;
                var initialMetres = walk.SessionWalkedMetres;
                var revision = player.PresentationTransformRevision;
                try
                {
                    for(var frame=0;frame<2400 && walk.Checkpoint<checkpoint
                        && (!stopAfterMetres.HasValue || walk.SessionWalkedMetres-initialMetres < stopAfterMetres.Value);frame++)
                    {
                        var goal=walk.Actor.GlobalPosition+Vector3.Back*1.55f;
                        var d=goal-player.GlobalPosition;
                        if(Horizontal(player.GlobalPosition,goal)>.12f)
                        {
                            player.ApplySmokeLook(0,Mathf.RadToDeg(Mathf.Atan2(-d.X,-d.Z)));
                            Input.ActionPress("move_forward",.45f);
                        }
                        else Input.ActionRelease("move_forward");
                        await Frames(1);
                        Check(walk.PhysicalPlacementFailure is null,
                            "The companion's physical placement was explicitly refused: " + walk.PhysicalPlacementFailure);
                        if (frame >= 240 && walk.SessionWalkedMetres - initialMetres < .02f)
                        {
                            GD.Print("alsu-walk-stall: " + walk.DescribeWalkEligibility());
                            throw new InvalidOperationException("The companion did not begin walking; blocked="
                                + walk.GetMeta("walkBlockedOwner", "none") + " actor=" + walk.Actor.GlobalPosition
                                + " player=" + player.GlobalPosition);
                        }
                        if(verifyReturn && checkpoint==1 && frame>=35 && !stepCaptured
                            && walk.SessionWalkedMetres-initialMetres>.15f)
                        {
                            stepCaptured = true;
                            Release();
                            await Aim(walk.Actor.GlobalPosition+Vector3.Up*.8f);
                            await Capture("alsu_actual_supported_step");
                        }
                    }
                }
                finally { Release(); }
                await Frames(3);
                Check((stopAfterMetres.HasValue ? walk.SessionWalkedMetres-initialMetres >= stopAfterMetres.Value : walk.Checkpoint==checkpoint)
                    && Horizontal(initial,walk.Actor.GlobalPosition)>.5f
                    && player.PresentationTransformRevision==revision,
                    "The actual companion did not traverse the expected checkpoint: "+checkpoint
                    +" actor="+walk.Actor.GlobalPosition+" player="+player.GlobalPosition
                    +" saved="+walk.Checkpoint+" walked="+walk.SessionWalkedMetres
                    +" blocked="+walk.GetMeta("walkBlockedOwner", "none").AsString());
            }
            async Task RoundTrip(string slot)
            {
                var requested = WalkRequested();
                Check(await bridge.SaveSlotAsync(slot) && await bridge.LoadSlotAsync(slot),"Alsu save/load failed: "+slot);
                await Frames(6);
                walk=FindWalk();
                Check(WalkRequested() == requested, "Save/load changed the player's walking decision: " + slot);
            }
            async Task Capture(string name)
            {
                if(!verifyReturn) return;
                var directory=System.Environment.GetEnvironmentVariable("URMAN_ALSU_WALK_CAPTURE_DIR");
                if(string.IsNullOrEmpty(directory)) return;
                Check(Path.IsPathFullyQualified(directory) && Directory.Exists(directory),"Alsu capture needs an existing absolute directory.");
                var path=Path.Combine(directory,name+".png");
                Check(!File.Exists(path),"Refusing to overwrite an Alsu walk capture.");
                await Act1StateFlowProof.WaitForRenderedFrameAsync(owner,"alsu-walk/"+name);
                using var image=owner.GetViewport().GetTexture().GetImage();
                Check(!image.IsEmpty() && image.SavePng(path)==Error.Ok,"Could not save actual companion viewport.");
                GD.Print("alsu-walk-capture: "+path);
            }
        }
        catch(Exception error) { GD.PrintErr("Alsu walk proof: "+error); return false; }
        finally { Release(); }
    }

    private static float Horizontal(Vector3 a,Vector3 b)=>new Vector2(a.X-b.X,a.Z-b.Z).Length();
    private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Release()
    {
        foreach(var action in new[]{"move_forward","move_backward","move_left","move_right","sprint","jump","interact"}) Input.ActionRelease(action);
        Input.ParseInputEvent(new InputEventKey { Keycode=Key.E,PhysicalKeycode=Key.E,Pressed=false });
    }
}
