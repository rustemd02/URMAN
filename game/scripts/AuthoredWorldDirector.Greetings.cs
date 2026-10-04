using System.Text.Json;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Everyday greetings of authored residents: a resident whose plot entry names a
/// <c>greeting</c> pool says one line when the player walks up. Lines are content
/// texts <c>urman.chapter1:text/greeting-resident-&lt;pool&gt;-N</c> (village-greetings.json),
/// shown as a subtitle in order, never opening a dialogue and never changing story
/// state; Tatar words are noticed as in any other line. A resident may turn to the
/// player ("turn": false keeps pairs facing each other and seated people seated).
/// </summary>
public partial class AuthoredWorldDirector
{
    private const float GreetReach = 3.0f;
    private const float GreetRelease = 4.6f;
    private const ulong ResidentCooldownMsec = 60_000;
    private const ulong AnyResidentGapMsec = 7_000;
    private sealed class GreetState { public int Next; public ulong Until; public bool Near; public bool Greeted; public float RestYaw; }
    private readonly Dictionary<string, GreetState> _greetings = new(StringComparer.Ordinal);
    private readonly HashSet<string> _greetPoolWarned = new(StringComparer.Ordinal);
    private ulong _lastGreetingAt;
    private double _greetClock;

    private void StepGreetings(double delta)
    {
        _greetClock += delta;
        if (_greetClock < .25) return;
        _greetClock = 0;
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player || player.ModalOpen) return;
        var now = Time.GetTicksMsec();
        foreach (var item in _objects.Values)
        {
            if (item.Kind != "npc" || !item.Root.IsVisibleInTree() || !item.Params.TryGetProperty("greeting", out var poolValue)) continue;
            var pool = poolValue.GetString() ?? "";
            if (!_greetings.TryGetValue(item.Id, out var state)) _greetings[item.Id] = state = new GreetState { RestYaw = item.Root.RotationDegrees.Y };
            var distance = item.Root.GlobalPosition.DistanceTo(player.GlobalPosition);
            var turns = !item.Params.TryGetProperty("turn", out var turn) || turn.ValueKind != JsonValueKind.False;
            if (!state.Near && distance <= GreetReach)
            {
                state.Near = true;
                state.RestYaw = item.Root.RotationDegrees.Y;
                if (turns) TurnTo(item.Root, player.GlobalPosition);
            }

            // While the player stays close, keep trying: another resident may have just spoken.
            if (state.Near && !state.Greeted && distance <= GreetReach)
            {
                if (now >= state.Until && now - _lastGreetingAt >= AnyResidentGapMsec && _bridge is not null)
                {
                    var lines = _bridge.TextIdsWithPrefix($"urman.chapter1:text/greeting-resident-{pool}-");
                    if (lines.Count == 0 && _greetPoolWarned.Add(pool))
                        GD.PushWarning($"authored-world: {item.Id} names greeting pool '{pool}' but the content pack has no greeting-resident-{pool}-N texts.");
                    if (lines.Count > 0)
                    {
                        var textId = lines[state.Next % lines.Count];
                        var line = _bridge.ResolveText(textId);
                        player.ShowRemark(Text(item.Params, "speaker", "ЖИТЕЛЬ"), line);
                        _ = _bridge.ObserveVocabularyTextAsync(line, textId);
                        state.Next++;
                        state.Greeted = true;
                        state.Until = now + ResidentCooldownMsec;
                        _lastGreetingAt = now;
                        item.Root.SetMeta("lastGreeting", textId);
                    }
                }
            }
            else if (state.Near && distance >= GreetRelease)
            {
                state.Near = false;
                state.Greeted = false;
                if (turns) item.Root.RotationDegrees = new Vector3(0, state.RestYaw, 0);
            }
        }
    }

    private static void TurnTo(Node3D root, Vector3 look)
    {
        var flat = new Vector2(look.X - root.GlobalPosition.X, look.Z - root.GlobalPosition.Z);
        if (flat.Length() > .05f) root.Rotation = new Vector3(0, Mathf.Atan2(flat.X, flat.Y), 0);
    }
}
