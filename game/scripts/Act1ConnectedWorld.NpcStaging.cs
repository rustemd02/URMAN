using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private Node3D? _rinatNpc;
    private bool? _rinatAlerted;
    private bool? _rinatTension;

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

        _rinatNpc = _zoneInstances["zirat_road"].GetNode<Node3D>("Act1NpcPresentation/Npc_rinat");
        _rinatNpc.Reparent(host, keepGlobalTransform: true);
        var timur = GeneratedCharacterKitDressing.Attach(host, "timur_hazrat", "TimurHazrat",
            new(-3.8f, AgentBAct1HeightField.CollisionGround(-3.8f, -19f), -19f));
        timur.Name = "Npc_timur_hazrat";
        timur.RotationDegrees = new(0, 55f, 0);
        host.SetMeta("runtimeStateOwnership", "RuntimeBridge; presentation projects Rinat.alerted and final knowledge");
    }

    private void UpdateAct1NpcStaging()
    {
        if (_rinatNpc is null || _runtimeBridge?.ActiveSceneId is null) return;
        var state = _runtimeBridge.SelectRuntimeState();
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
