using Godot;

namespace Urman.Godot;

/// <summary>
/// Everyday Tatar greetings when the player walks up to someone: authored
/// caption texts "urman.chapter1:text/greeting-&lt;characterId&gt;-N" (village-greetings.json)
/// shown as a subtitle with their Russian gloss, one at a time and in order.
/// Presentation only: a greeting never opens a dialogue or grants progress;
/// Tatar words already in the vocabulary are noticed as in any other line.
/// </summary>
public partial class Act1ConnectedWorld
{
    private const ulong GreetingCooldownMsec = 40_000;
    private readonly Dictionary<string, (int Next, ulong Until)> _greetings = new(StringComparer.Ordinal);

    private void GreetOnApproach(Node3D npc, ulong now)
    {
        var characterId = npc.Name.ToString().StartsWith("Npc_", StringComparison.Ordinal)
            ? npc.Name.ToString()["Npc_".Length..] : string.Empty;
        if (characterId.Length == 0 || _lifePlayer is null || _lifePlayer.ModalOpen) return;
        if (_alsuWalk is { ControlsFacing: true } && ReferenceEquals(npc, _alsuWalk.Actor)) return;
        if (GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge) return;
        var lines = bridge.TextIdsWithPrefix($"urman.chapter1:text/greeting-{characterId}-");
        if (lines.Count == 0) return;
        var state = _greetings.GetValueOrDefault(characterId);
        if (now < state.Until) return;
        var textId = lines[state.Next % lines.Count];
        var line = bridge.ResolveText(textId);
        _lifePlayer.ShowRemark(DialogueUi.SpeakerName(characterId.Replace('_', '-')), line);
        _ = bridge.ObserveVocabularyTextAsync(line, textId);
        _greetings[characterId] = (state.Next + 1, now + GreetingCooldownMsec);
        npc.SetMeta("lastGreeting", textId);
    }
}
