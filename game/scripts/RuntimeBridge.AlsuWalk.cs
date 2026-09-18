using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;
using Urman.Core.Narrative;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    private const string AlsuWalkCharacterId = "urman.chapter1:character/alsu";

    public async Task<bool> AdvanceAlsuStreetWalkAsync(int checkpoint)
    {
        var session = SessionIdentity;
        if (session is null || checkpoint is < 1 or > 3
            || AlsuStreetWalkPresentation.Current(GetTree())?.CanCommitCheckpoint(checkpoint) != true)
            return false;

        var result = await session.DispatchAsync(new GameCommand(
            $"npc.alsu.walk-checkpoint:{Interlocked.Increment(ref _interactionSequence):D8}",
            "npc.alsu.walk-checkpoint", JsonSerializer.SerializeToElement(new { checkpoint })));
        if (!ReferenceEquals(session, SessionIdentity) || result.Status != CommandStatus.Committed)
            return false;
        QueueRuntimeStateChanged();
        // The physical owner flushes the attained pose and requests the existing
        // checkpoint after this dispatch returns. Keeping disk I/O outside the
        // dispatch lets an ordinary save wait for this commit without recursion.
        return ReferenceEquals(session, SessionIdentity);
    }

    private CommandPlan HandleAlsuWalkCheckpoint(GameCommand command, RuntimeCommandContext context)
    {
        if (!command.Payload.TryGetProperty("checkpoint", out var value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var next)
            || next is < 1 or > 3)
            return new(Rejection: new("alsu-walk-invalid-step", "Не удалось проверить пройденный участок."));
        if (!context.State.TryGetProperty("npc", out var npcs) || npcs.ValueKind != JsonValueKind.Object
            || !npcs.TryGetProperty(AlsuWalkCharacterId, out var alsu) || alsu.ValueKind != JsonValueKind.Object
            || !alsu.TryGetProperty("walk_requested", out var requested) || requested.ValueKind != JsonValueKind.True)
            return new(Rejection: new("alsu-walk-not-requested", "Сначала договоритесь с Алсу пройти до поворота."));

        var previous = 0;
        if (alsu.TryGetProperty("walk_checkpoint", out var saved)
            && (saved.ValueKind != JsonValueKind.Number || !saved.TryGetInt32(out previous)))
            return new(Rejection: new("alsu-walk-invalid-state", "Не удалось проверить место встречи с Алсу."));
        if (alsu.TryGetProperty("walk_arrived", out var savedArrival)
            && savedArrival.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return new(Rejection: new("alsu-walk-invalid-state", "Не удалось проверить место встречи с Алсу."));
        if (previous != next - 1 || alsu.TryGetProperty("walk_arrived", out var arrived) && arrived.ValueKind == JsonValueKind.True
            || SessionIdentity is null
            || AlsuStreetWalkPresentation.Current(GetTree())?.CanCommitCheckpoint(next) != true)
            return new(Rejection: new("alsu-walk-not-reached", "Этот участок ещё не пройден вместе с Алсу."));

        // The existing narrative state owns these fields. The actual walk is
        // checked again inside the transaction; no knowledge, dialogue choice
        // or journal entry is granted by proximity or by the checkpoint itself.
        var effects = new JsonArray(new JsonObject
        {
            ["op"] = "npc.set-state", ["characterId"] = AlsuWalkCharacterId,
            ["stateKey"] = "walk_checkpoint", ["value"] = next
        });
        if (next == 3) effects.Add(new JsonObject
        {
            ["op"] = "npc.set-state", ["characterId"] = AlsuWalkCharacterId,
            ["stateKey"] = "walk_arrived", ["value"] = true
        });
        var plan = ContentRuleEngine.PlanEffects(JsonSerializer.SerializeToElement(effects), context.State);
        return new(Effects: plan.Effects, Events: plan.Events);
    }
}
