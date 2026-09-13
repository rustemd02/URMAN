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

        // The forest stage has precedence over the FAP marker. Alerting occurs
        // when the house dialogue starts, so Rinat stays beside the target
        // until the authored zirat-road transition confirms the departure.
        var forestStage = alerted && routeReached;
        var houseStage = !forestStage && (fapVisited || alerted);
        var desiredStage = forestStage ? "forest" : houseStage ? "house" : "village";
        if (_rinatStage != desiredStage)
        {
            _rinatTurn?.Kill();
            var desiredHost = desiredStage == "house" ? _rinatHouseHost : _rinatCoreHost;
            if (desiredHost is null) return;

            var at = desiredStage switch
            {
                "house" => new Vector3(4.05f, 0f, -2.65f),
                "forest" => new Vector3(1.85f, 0f, -124f),
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
            _rinatNpc.RotationDegrees = new(0, desiredStage == "forest" ? -23f : desiredStage == "house" ? 0f : 12f, 0);
            _rinatNpc.SetMeta("anchor", at);
            _rinatNpc.SetMeta("rinatStage", desiredStage);
            _rinatStage = desiredStage;
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

    private readonly System.Collections.Generic.Dictionary<Node3D, (float RestYaw, bool Facing, Tween? Turn)>
        _conversationFacing = new();
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
        _lifePlayer ??= GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (_lifePlayer is null)
        {
            return;
        }

        if (!_conversationFacingResolved)
        {
            _conversationFacingResolved = true;
            foreach (var name in new[] { "Npc_alsu", "Npc_timur_hazrat", "MansurNpc", "NailaNpc" })
            {
                if (FindChild(name, recursive: true, owned: false) is Node3D npc)
                {
                    _conversationFacing[npc] = (npc.Rotation.Y, false, null);
                }
            }

            SetMeta("conversationFacingNpcCount", _conversationFacing.Count);
        }

        var playerPosition = _lifePlayer.GlobalPosition;
        foreach (var npc in _conversationFacing.Keys.ToArray())
        {
            if (!IsInstanceValid(npc))
            {
                _conversationFacing.Remove(npc);
                continue;
            }

            var entry = _conversationFacing[npc];
            var distance = npc.GlobalPosition.DistanceTo(playerPosition);
            if (!entry.Facing && distance <= 2.9f)
            {
                entry.Turn?.Kill();
                entry.Turn = TurnNpcTowardsPlayer(npc);
                entry.Facing = true;
                _conversationFacing[npc] = entry;
                npc.SetMeta("conversationFacing", "towards-player");
            }
            else if (entry.Facing && distance >= 3.9f)
            {
                entry.Turn?.Kill();
                var back = CreateTween();
                back.SetTrans(Tween.TransitionType.Sine);
                back.SetEase(Tween.EaseType.Out);
                back.TweenProperty(npc, "rotation:y", entry.RestYaw, .55f);
                entry.Turn = back;
                entry.Facing = false;
                _conversationFacing[npc] = entry;
                npc.SetMeta("conversationFacing", "rest");
            }
        }
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
}
