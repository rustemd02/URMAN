using System.Text.Json;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void ConfigureInvestigationRevisits()
    {
        foreach (var (zone, name) in new[]
        {
            ("house_old_pc", "HouseExit"),
            ("village_day", "RoadToFap"),
            ("fap_clinic", "OfficialRecordExitToStreet"),
            ("village_day", "ReturnToHouseRegister")
        })
        {
            var target = _zoneInstances[zone].GetNode<InteractionTarget>(name);
            if (string.IsNullOrWhiteSpace(target.TargetZoneId)
                || !TryGetWorldSpawn(target.TargetZoneId, target.TargetSpawnPointId, out _))
                throw new InvalidOperationException($"Investigation return portal has no reachable destination: {zone}/{name}");
            target.PresentationRepeatAvailable = () => CanRevisitInvestigation(zone, target);
            target.PresentationRepeat = () =>
            {
                if (!CanRevisitInvestigation(zone, target)
                    || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController { ModalOpen: false }
                    || GetTree().GetFirstNodeInGroup("zone_manager") is not Main main) return;
                // Walking back through a known door changes physical location,
                // ambience and save spawn, not the investigation's current beat.
                main.SwitchZone(target.TargetZoneId, target.TargetSpawnPointId);
                if (!string.IsNullOrEmpty(target.WorldFoleySample))
                {
                    var player = GetTree().GetFirstNodeInGroup("player_controller") as Node3D;
                    UiFoley.PlayWorld(main, player?.GlobalPosition ?? target.GlobalPosition, target.WorldFoleySample);
                }
            };
            target.SetMeta("repeatPolicy", "known investigation door; preserve active scene and all narrative effects");
        }
    }

    private bool CanRevisitInvestigation(string sourceZone, InteractionTarget target)
    {
        if (_runtimeBridge?.SessionIdentity is null || ActiveZoneId != sourceZone
            || _runtimeBridge.IsInteractionAvailable(target.InteractionId)) return false;
        var state = _runtimeBridge.SelectRuntimeState();
        if (!state.TryGetProperty("npc", out var people)
            || !people.TryGetProperty("urman.chapter1:character/naila", out var naila)
            || !naila.TryGetProperty("record_access_granted", out var permission)
            || permission.ValueKind != JsonValueKind.True
            || !state.TryGetProperty("knowledge", out var knowledge)
            || !knowledge.TryGetProperty("urman.chapter1:knowledge/clue_marat_case_boundary_marker", out var marker)
            || !marker.TryGetProperty("status", out var status) || status.GetString() != "confirmed") return false;
        // The later authored departure shares the physical doorway. Keep a
        // single focused target when it is ready; re-entry never skips it.
        return target.Name != "HouseExit"
            || !_runtimeBridge.IsInteractionAvailable("urman.chapter1:interaction/edge-sketch-to-zirat-road");
    }
}
