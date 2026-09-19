using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void ConfigureInvestigationRevisits()
    {
        // In the connected village this scene transition belongs to the actual
        // roadside threshold. Its old house-door position sent the player back
        // indoors after Timur and then skipped the intervening street entirely.
        // Keep the existing target, content conditions and destination; only its
        // physical owner and position change before interaction bindings are read.
        var departure = _zoneInstances["house_old_pc"].GetNode<InteractionTarget>("EdgeSketchToZiratRoad");
        departure.RequestReady();
        // It is now an observation on an open road, not a closed doorway.
        // Keep the interaction ray while avoiding an invisible body barrier.
        departure.CollisionLayer = 4u;
        departure.CollisionMask = 0u;
        departure.Reparent(_zoneInstances["village_day"], keepGlobalTransform: false);
        departure.GlobalTransform = new(Basis.Identity, ToGlobal(new Vector3(
            0f, AgentBAct1HeightField.CollisionGround(0f, -53.5f) + 1.05f, -53.5f)));
        departure.Prompt = "Проверить поворот к зирату по схеме";
        departure.SetMeta("connectedDeparturePolicy", "existing roadside threshold; walk from village; unchanged narrative gates");

        foreach (var (zone, name) in new[]
        {
            ("house_old_pc", "HouseExit"),
            ("village_day", "RoadToFap"),
            ("fap_clinic", "OfficialRecordExitToStreet"),
            ("village_day", "ReturnToHouseRegister")
        })
        {
            var target = _zoneInstances[zone].GetNode<InteractionTarget>(name);
            // Returning is possible before these sources have been earned.
            // The door names its physical destination; the journal owns the
            // current investigation objective and its source-dependent wording.
            if (name == "ReturnToHouseRegister") target.Prompt = "Войти в дом бабая";
            if (name == "OfficialRecordExitToStreet") target.Prompt = "Выйти из ФАПа на улицу";
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
        bool NpcFlag(string person, string flag) => state.TryGetProperty("npc", out var people)
            && people.TryGetProperty("urman.chapter1:character/" + person, out var npc)
            && npc.TryGetProperty(flag, out var value) && value.ValueKind == JsonValueKind.True;
        bool Confirmed(string id) => state.TryGetProperty("knowledge", out var knowledge)
            && knowledge.TryGetProperty("urman.chapter1:knowledge/" + id, out var entry)
            && entry.TryGetProperty("status", out var status) && status.GetString() == "confirmed";

        // The household's first exit already establishes this door. An optional
        // photograph found outside must be returnable before visiting the clinic.
        // Reuse the earned first-exit conditions; never grant a new story beat.
        if (target.Name == "HouseExit" || target.Name == "ReturnToHouseRegister")
            return NpcFlag("gulsina", "warning_heard")
                && Confirmed("clue_family_avoids_marat")
                && Confirmed("clue_marat_official_death_version");

        // A player inside the legitimately entered clinic can leave a question
        // unanswered and return. These active scenes survive presentation-only
        // door travel and save/load; they are not fabricated visit flags.
        if (target.Name == "OfficialRecordExitToStreet") return true;
        return NpcFlag("naila", "record_access_granted")
            || _runtimeBridge.ActiveSceneId is "urman.chapter1:scene/fap_waiting_room_day"
                or "urman.chapter1:scene/fap_pressure_document_desk"
                or "urman.chapter1:scene/evidence-official-death";
    }
}
