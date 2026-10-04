using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private Node3D? _rinatNpc;
    private Node3D? _rinatCoreHost;
    private Node3D? _rinatHouseHost;
    private string? _rinatStage;
    private bool? _rinatTension;
    private object? _rinatSession;
    private bool? _rinatAlerted;
    private Tween? _rinatTurn;
    private Node3D? _gulsinaNpc;
    private bool? _gulsinaWarningHeard;
    private Tween? _gulsinaTurn;
    private float _gulsinaRestYaw;
    private object? _gulsinaSession;
    private AlsuStreetWalkPresentation? _alsuWalk;

    private void BuildAct1NpcStaging()
    {
        var exterior = GetNode<Node3D>("Act1CoreWorldGreybox");
        var host = new Node3D { Name = "Act1People" };
        host.SetMeta("presentationOnly", true);
        exterior.AddChild(host);

        // Only the legacy street rendering is suppressed. Its people belong
        // to the visible exterior, while their existing ray targets stay put.
        var villagePeople = _zoneInstances["village_day"].GetNode<Node3D>("Act1NpcPresentation");
        villagePeople.Reparent(host, keepGlobalTransform: true);
        var alsu = villagePeople.GetNode<Node3D>("Npc_alsu");
        alsu.Position = new(.85f, AgentBAct1HeightField.CollisionGround(.85f, 2.05f), 2.05f);
        alsu.RotationDegrees = new(0, -15f, 0);
        GeneratedCharacterKitDressing.GroundSolesOnAnchor(alsu);
        _alsuWalk = AlsuStreetWalkPresentation.Attach(alsu,
            _zoneInstances["village_day"].GetNode<InteractionTarget>("AlsuNpc"));

        _gulsinaNpc = _zoneInstances["house_old_pc"]
            .GetNode<Node3D>("Act1NpcPresentation/Npc_gulsina");
        _gulsinaRestYaw = _gulsinaNpc.Rotation.Y;

        _rinatCoreHost = host;
        _rinatHouseHost = _zoneInstances["house_old_pc"]
            .GetNode<Node3D>("Act1NpcPresentation");
        _rinatNpc = _zoneInstances["zirat_road"].GetNode<Node3D>("Act1NpcPresentation/Npc_rinat");
        _rinatNpc.Reparent(host, keepGlobalTransform: true);
        var timur = GeneratedCharacterKitDressing.Attach(host, "timur_hazrat", "TimurHazrat",
            new(-3.8f, AgentBAct1HeightField.CollisionGround(-3.8f, -19f), -19f));
        timur.Name = "Npc_timur_hazrat";
        timur.RotationDegrees = new(0, 55f, 0);
        host.SetMeta("runtimeStateOwnership",
            "RuntimeBridge; presentation projects Gulsina.warning_heard, Rinat.alerted and final knowledge");
    }

    private void UpdateAct1NpcStaging()
    {
        if (_rinatNpc is null || _gulsinaNpc is null || _runtimeBridge?.ActiveSceneId is null) return;
        var session = _runtimeBridge.SessionIdentity;
        if (session is null) return;
        if (!ReferenceEquals(_gulsinaSession, session))
        {
            _gulsinaSession = session;
            _gulsinaWarningHeard = null;
            _gulsinaTurn?.Kill();
            _gulsinaNpc.Rotation = new Vector3(0f, _gulsinaRestYaw, 0f);
        }
        if (!ReferenceEquals(_rinatSession, session))
        {
            // New Game and a successful load replace the RuntimeKernel. Clear
            // only presentation caches; RuntimeBridge remains the state owner.
            _rinatSession = session;
            _rinatStage = null;
            _rinatTension = null;
            _rinatAlerted = null;
            _rinatTurn?.Kill();
        }

        var state = _runtimeBridge.SelectRuntimeState();
        var warningHeard = state.TryGetProperty("npc", out var npcState)
            && npcState.TryGetProperty("urman.chapter1:character/gulsina", out var gulsina)
            && gulsina.TryGetProperty("warning_heard", out var warning)
            && warning.ValueKind == System.Text.Json.JsonValueKind.True;
        if (_gulsinaWarningHeard != warningHeard)
        {
            var newlyHeard = _gulsinaWarningHeard == false && warningHeard;
            _gulsinaWarningHeard = warningHeard;
            _gulsinaTurn?.Kill();
            if (!warningHeard)
            {
                _gulsinaNpc.Rotation = new Vector3(0f, _gulsinaRestYaw, 0f);
            }
            else if (newlyHeard && ActiveZoneId == "house_old_pc")
            {
                _gulsinaTurn = TurnNpcTowardsPlayer(_gulsinaNpc);
            }
        }

        var alerted = state.TryGetProperty("npc", out var people)
            && people.TryGetProperty("urman.chapter1:character/rinat", out var rinat)
            && rinat.TryGetProperty("alerted", out var alert) && alert.ValueKind == System.Text.Json.JsonValueKind.True;
        var fapVisited = state.TryGetProperty("npc", out var fapPeople)
            && fapPeople.TryGetProperty("urman.chapter1:character/naila", out var naila)
            && naila.TryGetProperty("record_access_granted", out var recordAccess)
            && recordAccess.ValueKind == System.Text.Json.JsonValueKind.True;
        var routeReached = state.TryGetProperty("knowledge", out var knowledgeState)
            && knowledgeState.TryGetProperty("urman.chapter1:knowledge/route_kara_urman_edge_hint", out var routeHint)
            && routeHint.TryGetProperty("status", out var routeStatus)
            && routeStatus.GetString() == "confirmed";

        // The route clue alone does not put him at the distant forest edge.
        // Keep the ongoing house conversation coherent, then show the same
        // ordinary person and his lamp beside the mandatory zirat route.
        var roadsideStage = alerted && routeReached
            && (ActiveZoneId != "house_old_pc" || ObservationKnown("clue_rinat_at_roadside"));
        var forestStage = roadsideStage && ActiveZoneId == "kara_urman_night";
        var houseStage = !roadsideStage && (fapVisited || alerted);
        var desiredStage = forestStage ? "forest" : roadsideStage ? "roadside" : houseStage ? "house" : "village";
        if (_rinatStage != desiredStage)
        {
            _rinatTurn?.Kill();
            var desiredHost = desiredStage == "house" ? _rinatHouseHost : _rinatCoreHost;
            if (desiredHost is null) return;

            var at = desiredStage switch
            {
                "house" => StyleBenchmarkInteriorFactory.RinatAnchor,
                "forest" => RinatForestAnchor,
                "roadside" => RinatRoadsideAnchor,
                _ => new Vector3(-1.5f, 0f, -3.8f)
            };
            if (desiredStage != "house")
            {
                at.Y = AgentBAct1HeightField.CollisionGround(at.X, at.Z);
            }

            if (!ReferenceEquals(_rinatNpc.GetParent(), desiredHost))
            {
                _rinatNpc.Reparent(desiredHost, keepGlobalTransform: false);
            }
            _rinatNpc.Position = at;
            // At home, face the room entrance until the first conversation
            // turns the actor toward the player. Exterior directions stay authored.
            _rinatNpc.RotationDegrees = new(0, desiredStage is "forest" or "roadside" ? -30f : desiredStage == "house" ? 0f : 12f, 0);
            _rinatNpc.SetMeta("anchor", at);
            _rinatNpc.SetMeta("rinatStage", desiredStage);
            _rinatStage = desiredStage;
            _rinatPresence?.ApplyStage(desiredStage);
        }

        if (_rinatAlerted != alerted)
        {
            var newlyAlerted = _rinatAlerted == false && alerted;
            _rinatAlerted = alerted;
            if (newlyAlerted && houseStage && ActiveZoneId == "house_old_pc")
                _rinatTurn = TurnNpcTowardsPlayer(_rinatNpc);
        }

        var tension = state.TryGetProperty("knowledge", out var knowledge)
            && knowledge.TryGetProperty("urman.chapter1:knowledge/clue_do_not_answer_rule", out var rule)
            && rule.GetProperty("status").GetString() == "confirmed";
        if (_rinatTension != tension)
        {
            GeneratedCharacterKitDressing.PlayClip(_rinatNpc, tension ? "Tension" : "Idle");
            _rinatTension = tension;
        }
    }

    private readonly System.Collections.Generic.Dictionary<Node3D, (float RestYaw, bool Facing, Tween? Turn, ulong RetargetAfter)>
        _conversationFacing = new();
    // Reused per-frame key snapshot: the loop below removes entries as it goes, so it
    // must iterate a copy of the keys exactly like the previous Keys.ToArray() did.
    private readonly System.Collections.Generic.List<Node3D> _conversationFacingSnapshot = new();
    private bool _conversationFacingResolved;

    /// <summary>
    /// Presentation-only idle behaviour for the NPCs the player talks to
    /// outside the story-driven turns (Alsu, Timur, Mansur, Naila): they turn
    /// to face the player who stands in conversation range and return to their
    /// rest yaw when the player walks away. Gulsina and Rinat keep their
    /// existing authored turns.
    /// </summary>
    private void UpdateConversationFacing()
    {
        _lifePlayer = LifePlayer();
        if (_lifePlayer is null)
        {
            return;
        }

        if (!_conversationFacingResolved)
        {
            _conversationFacingResolved = true;
            // The visible actors are separate siblings of their hidden ray
            // targets. Turning MansurNpc/NailaNpc only rotated those colliders
            // and left the people motionless during conversation.
            foreach (var name in new[] { "Npc_alsu", "Npc_timur_hazrat", "Npc_mansur", "Npc_naila" })
            {
                if (FindChild(name, recursive: true, owned: false) is Node3D npc)
                {
                    _conversationFacing[npc] = (npc.Rotation.Y, false, null, 0);
                }
            }

            SetMeta("conversationFacingNpcCount", _conversationFacing.Count);
        }

        var playerPosition = _lifePlayer.GlobalPosition;
        var now = Time.GetTicksMsec();
        // Same iteration contract as the previous Keys.ToArray(): a snapshot taken
        // before the loop, so removals inside it stay legal and are not revisited.
        // Only the per-frame array/LINQ allocation is gone.
        _conversationFacingSnapshot.Clear();
        foreach (var npc in _conversationFacing.Keys) _conversationFacingSnapshot.Add(npc);
        foreach (var npc in _conversationFacingSnapshot)
        {
            if (!IsInstanceValid(npc))
            {
                _conversationFacing.Remove(npc);
                continue;
            }

            var entry = _conversationFacing[npc];
            if (_alsuWalk is { ControlsFacing: true } && ReferenceEquals(npc, _alsuWalk.Actor))
            {
                entry.Turn?.Kill();
                entry.Turn = null;
                entry.Facing = false;
                _conversationFacing[npc] = entry;
                continue;
            }
            if (!npc.IsVisibleInTree()) continue;
            var distance = npc.GlobalPosition.DistanceTo(playerPosition);
            if (!entry.Facing && distance <= 2.9f)
            {
                entry.Turn?.Kill();
                entry.Turn = TurnNpcTowardsPlayer(npc);
                entry.Facing = true;
                entry.RetargetAfter = now + 730;
                _conversationFacing[npc] = entry;
                npc.SetMeta("conversationFacing", "towards-player");
                RecordConversationTurn(npc, "entered-range");
                GreetOnApproach(npc, now);
            }
            else if (entry.Facing && distance >= 3.9f)
            {
                entry.Turn?.Kill();
                var back = CreateTween();
                back.SetTrans(Tween.TransitionType.Sine);
                back.SetEase(Tween.EaseType.Out);
                var rest = npc.Rotation.Y + Mathf.AngleDifference(npc.Rotation.Y, entry.RestYaw);
                back.TweenProperty(npc, "rotation:y", rest, .55f);
                entry.Turn = back;
                entry.Facing = false;
                _conversationFacing[npc] = entry;
                npc.SetMeta("conversationFacing", "rest");
                RecordConversationTurn(npc, "left-range");
            }
            else if (entry.Facing && now >= entry.RetargetAfter
                && (entry.Turn is null || !IsInstanceValid(entry.Turn) || !entry.Turn.IsRunning()))
            {
                // Keep the existing ground origin, foot rig and short yaw
                // tween. A person walking around a speaker changes the target,
                // while a still listener or a small shift does not restart it.
                var direction = npc.GetParent<Node3D>().ToLocal(playerPosition) - npc.Position;
                direction.Y = 0f;
                if (direction.LengthSquared() <= .0001f
                    || Mathf.Abs(Mathf.AngleDifference(npc.Rotation.Y,
                        Mathf.Atan2(direction.X, direction.Z))) <= Mathf.DegToRad(18f)) continue;
                entry.Turn = TurnNpcTowardsPlayer(npc);
                // 480ms turn plus 250ms of settled attention before considering
                // another target. Never kill/restart a tracking tween per frame.
                entry.RetargetAfter = now + 730;
                _conversationFacing[npc] = entry;
                RecordConversationTurn(npc, "observer-moved");
            }
        }
    }

    private static void RecordConversationTurn(Node3D npc, string reason)
    {
        npc.SetMeta("conversationTurnCount", npc.GetMeta("conversationTurnCount", 0L).AsInt64() + 1);
        npc.SetMeta("conversationTurnReason", reason);
    }

    private Tween? TurnNpcTowardsPlayer(Node3D npc)
    {
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player) return null;
        var toPlayer = npc.GetParent<Node3D>().ToLocal(player.GlobalPosition) - npc.Position;
        toPlayer.Y = 0f;
        if (toPlayer.LengthSquared() <= .0001f) return null;
        var yaw = npc.Rotation.Y + Mathf.AngleDifference(npc.Rotation.Y, Mathf.DegToRad(DirectionYaw(toPlayer)));
        var turn = CreateTween();
        turn.SetTrans(Tween.TransitionType.Sine);
        turn.SetEase(Tween.EaseType.Out);
        turn.TweenProperty(npc, "rotation:y", yaw, .48f);
        return turn;
    }
    private void UpdateOrdinaryNpcNightPresence(bool night)
    {
        if (FindChild("Npc_alsu", true, false) is Node3D alsu)
        {
            alsu.Visible = !night;
            alsu.SetMeta("nightPresence", night ? "indoors" : "daytime-street-route");
        }
        _alsuWalk?.SetZonePresentation(ActiveZoneId, FacilityExteriorActive && !night);
        if (_rinatNpc is not null)
            _rinatNpc.Visible = !night || _rinatStage is "forest" or "roadside" or "house";
        // Rinat's authored roadside/forest meeting stays with UpdateAct1NpcStaging.
        // Outdoor greetings must not come from an empty night street.
        if (night)
            foreach (var target in FindDescendants<InteractionTarget>(_zoneInstances["village_day"]))
                if (target.Name == "AlsuNpc" || _rinatStage is null or "village" && target.Name == "RinatNpc") target.CollisionLayer = 0;
    }

}
