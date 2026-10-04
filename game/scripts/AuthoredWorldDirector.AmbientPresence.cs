using Godot;

namespace Urman.Godot;

public partial class AuthoredWorldDirector
{
    // Only explicitly marked background residents use this presentation schedule.
    // Story actors, routine claims and quest state remain with their existing owners.
    private sealed record AmbientVisit(AuthoredObject Item, double Offset, double Outside, double Inside);
    private readonly List<AmbientVisit> _ambientVisits = new();
    private bool _ambientNight;
    private double _ambientClock;
    private double _ambientTick;

    private void RegisterAmbientVisit(AuthoredObject item)
    {
        _ambientVisits.RemoveAll(visit => visit.Item.Id == item.Id);
        if (item.Kind != "npc" || !item.Params.TryGetProperty("ambientResidence", out var config)) return;
        var key = Text(config, "household", item.Id);
        uint hash = 2166136261;
        foreach (var c in key) hash = (hash ^ c) * 16777619;
        var outside = config.GetProperty("outsideSeconds").GetDouble();
        var inside = config.GetProperty("insideSeconds").GetDouble();
        _ambientVisits.Add(new(item, hash % (uint)(outside + inside), outside, inside));
        ApplyAmbientPresence();
    }

    internal void SetAmbientNight(bool night)
    {
        _ambientNight = night;
        ApplyAmbientPresence();
    }

    private void StepAmbientPresence(double delta)
    {
        // Pause visits with the ordinary game; no wall-clock drift while talking.
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: true }) return;
        _ambientClock += Math.Min(delta, .25);
        _ambientTick += delta;
        if (_ambientTick < .5) return;
        _ambientTick = 0;
        ApplyAmbientPresence();
    }

    private void ApplyAmbientPresence()
    {
        foreach (var visit in _ambientVisits)
        {
            var item = visit.Item;
            if (!IsInstanceValid(item.Root)) continue;
            // Scripted scenes can claim an actor without a background timer hiding them.
            var claimed = _routines.TryGetValue(item.Id, out var routine) && routine.Claim is not null;
            var outside = !_ambientNight && ((_ambientClock + visit.Offset) % (visit.Outside + visit.Inside) < visit.Outside);
            var visible = claimed || outside;
            var process = visible ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            if (item.Root.Visible == visible && item.Root.ProcessMode == process) continue;
            var player = GetTree().GetFirstNodeInGroup("player_controller") as Node3D;
            // Daytime visits change only outside the player's close view. Night projection
            // happens at the story/zone transition and immediately clears ordinary residents.
            if (!claimed && !_ambientNight && player is not null && item.Root.Visible == !visible)
            {
                var distance = item.Root.GlobalPosition.DistanceTo(player.GlobalPosition);
                var camera = GetViewport().GetCamera3D();
                if (distance < 14f || distance < 45f && camera?.IsPositionInFrustum(item.Root.GlobalPosition + Vector3.Up * .9f) == true) continue;
            }
            item.Root.Visible = visible;
            item.Root.ProcessMode = process;
            if (item.Body is not null) item.Body.CollisionLayer = visible ? item.BodyLayer : 0;
            if (item.Target is not null) item.Target.CollisionLayer = visible ? 4u : 0;
            item.Root.SetMeta("ambientPresence", visible ? "short-daytime-visit" : "indoors");
            if (_greetings.TryGetValue(item.Id, out var greeting)) { greeting.Near = false; greeting.Greeted = false; }
        }
    }
}
