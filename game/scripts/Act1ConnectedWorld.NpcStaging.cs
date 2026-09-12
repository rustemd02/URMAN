using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private Node3D? _rinatNpc;
    private bool? _rinatAlerted;
    private bool? _rinatTension;
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
        if (!ReferenceEquals(_gulsinaSession, _runtimeBridge.SessionIdentity))
        {
            _gulsinaSession = _runtimeBridge.SessionIdentity;
            _gulsinaWarningHeard = null;
            _gulsinaTurn?.Kill();
            _gulsinaNpc.Rotation = new Vector3(0f, _gulsinaRestYaw, 0f);
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
            else if (newlyHeard && ActiveZoneId == "house_old_pc"
                && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            {
                // This gesture belongs to the spoken warning, not a later
                // zone entry before the player has reached the new spawn.
                var toPlayer = _gulsinaNpc.GetParent<Node3D>().ToLocal(player.GlobalPosition) - _gulsinaNpc.Position;
                toPlayer.Y = 0f;
                if (toPlayer.LengthSquared() > .0001f)
                {
                    var targetYaw = _gulsinaNpc.Rotation.Y + Mathf.AngleDifference(
                        _gulsinaNpc.Rotation.Y, Mathf.DegToRad(DirectionYaw(toPlayer)));
                    _gulsinaTurn = CreateTween();
                    _gulsinaTurn.SetTrans(Tween.TransitionType.Sine);
                    _gulsinaTurn.SetEase(Tween.EaseType.Out);
                    _gulsinaTurn.TweenProperty(_gulsinaNpc, "rotation:y", targetYaw, .48f);
                }
            }
        }
        var alerted = state.TryGetProperty("npc", out var people)
            && people.TryGetProperty("urman.chapter1:character/rinat", out var rinat)
            && rinat.TryGetProperty("alerted", out var alert) && alert.ValueKind == System.Text.Json.JsonValueKind.True;
        if (_rinatAlerted != alerted)
        {
            // The alert is authored while Aidar is indoors: Rinat is already
            // ahead at the forest endpoint before Aidar arrives, with no relocation
            // during the optional approach or on the final cue.
            var at = alerted ? new Vector3(1.85f, 0, -124f) : new Vector3(2.15f, 0, -1.7f);
            at.Y = AgentBAct1HeightField.CollisionGround(at.X, at.Z);
            _rinatNpc.Position = at;
            _rinatNpc.RotationDegrees = new(0, alerted ? -23f : 12f, 0);
            _rinatNpc.SetMeta("anchor", at);
            _rinatAlerted = alerted;
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
}
