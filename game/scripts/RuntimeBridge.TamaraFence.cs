using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Core.Contracts;
using Urman.Core.Narrative;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    public const string TamaraFenceStateId = "tamara/fence";
    private const string TamaraCharacterId = "urman.chapter1:character/tamara";

    public sealed record TamaraFenceSnapshot(
        bool Crashed,
        bool Repaired,
        bool CutsceneWatched,
        int Carried,
        int Delivered);

    /// <summary>
    /// Reads the quest's shared narrative/world state. One projection for the
    /// journal UI, the world staging and tests; no state is changed here.
    /// </summary>
    public TamaraFenceSnapshot? TamaraFenceSnapshotNow()
    {
        if (_kernel is null) return null;
        var state = _kernel.SelectState();
        var crashed = TamaraNpcFlag(state, "fence_crashed");
        var repaired = TamaraNpcFlag(state, "fence_repaired");
        var carried = 0;
        for (var board = 1; board <= 6; board++)
        {
            if (TamaraNpcFlag(state, $"board_{board}")) carried++;
        }

        var delivered = 0;
        if (state.TryGetProperty("npc", out var npc)
            && npc.TryGetProperty(TamaraCharacterId, out var tamara)
            && tamara.TryGetProperty("boards_delivered", out var count)
            && count.ValueKind == JsonValueKind.Number)
        {
            _ = count.TryGetInt32(out delivered);
        }

        var watched = false;
        var props = SelectWorldProps();
        if (props.ValueKind == JsonValueKind.Object
            && props.TryGetProperty(TamaraFenceStateId, out var fence)
            && fence.ValueKind == JsonValueKind.Object
            && fence.TryGetProperty("cutsceneWatched", out var flag)
            && flag.ValueKind == JsonValueKind.True)
        {
            watched = true;
        }

        return new TamaraFenceSnapshot(crashed, repaired, watched, carried, delivered);
    }

    internal bool TamaraFenceBoardTaken(int board)
    {
        if (_kernel is null) return false;
        var state = _kernel.SelectState();
        if (TamaraNpcFlag(state, $"board_taken_{board}") || TamaraNpcFlag(state, $"board_{board}")) return true;
        // Existing saves recorded picked places here, before the immutable flag.
        var props = SelectWorldProps();
        return props.ValueKind == JsonValueKind.Object
            && props.TryGetProperty($"tamara/board/{board}", out var record)
            && record.ValueKind == JsonValueKind.Object
            && record.TryGetProperty("taken", out var taken) && taken.ValueKind == JsonValueKind.True;
    }

    private static bool TamaraNpcFlag(JsonElement state, string stateKey) =>
        state.TryGetProperty("npc", out var npc)
        && npc.TryGetProperty(TamaraCharacterId, out var tamara)
        && tamara.TryGetProperty(stateKey, out var value)
        && value.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Commits the crash itself: Tamara's fence flag, the physical fence state
    /// and the quest start gate land in one kernel transaction, so a quit
    /// during the following presentation can never split them.
    /// </summary>
    public async Task<bool> TamaraFenceCrashAsync(Vector3 impactPoint)
    {
        var session = SessionIdentity;
        if (session is null) return false;
        var result = await session.DispatchAsync(new GameCommand(
            $"tamara-fence.crash:{Interlocked.Increment(ref _interactionSequence):D8}",
            "tamara-fence.crash",
            JsonSerializer.SerializeToElement(new { impactX = impactPoint.X, impactZ = impactPoint.Z })));
        if (!ReferenceEquals(session, SessionIdentity)) return false;
        if (result.Status != CommandStatus.Committed)
        {
            GD.PushWarning("tamara-fence: crash was rejected: " + result.Error?.Message);
            return false;
        }

        await ReconcileQuestsAsync();
        QueueRuntimeStateChanged();
        await SaveCheckpointAsync(force: true);
        return true;
    }

    /// <summary>
    /// Hands over every board currently carried. The dialogue that follows
    /// reads the already-committed count, so its entry routes and the journal
    /// progress describe the same world.
    /// </summary>
    public async Task<bool> TamaraFenceDeliverAsync()
    {
        var session = SessionIdentity;
        if (session is null) return false;
        var result = await session.DispatchAsync(new GameCommand(
            $"tamara-fence.deliver:{Interlocked.Increment(ref _interactionSequence):D8}",
            "tamara-fence.deliver",
            JsonSerializer.SerializeToElement(new { })));
        if (!ReferenceEquals(session, SessionIdentity)) return false;
        if (result.Status != CommandStatus.Committed) return false;
        await ReconcileQuestsAsync();
        QueueRuntimeStateChanged();
        await SaveCheckpointAsync(force: true);
        return true;
    }

    private CommandPlan HandleTamaraFenceCrash(GameCommand command, RuntimeCommandContext context)
    {
        if (TamaraNpcFlag(context.State, "fence_crashed"))
            return new CommandPlan(Rejection: new("tamara-fence-already-crashed", "Забор уже сломан."));
        if (!command.Payload.TryGetProperty("impactX", out var x) || !x.TryGetSingle(out var impactX)
            || !command.Payload.TryGetProperty("impactZ", out var z) || !z.TryGetSingle(out var impactZ)
            || !float.IsFinite(impactX) || !float.IsFinite(impactZ))
            return new CommandPlan(Rejection: new("tamara-fence-invalid-impact", "Не удалось определить место удара."));
        var effects = JsonSerializer.SerializeToElement(new object[]
        {
            new { op = "npc.set-state", characterId = TamaraCharacterId, stateKey = "fence_crashed", value = true }
        });
        var planned = ContentRuleEngine.PlanEffects(effects, context.State);
        var placement = JsonSerializer.SerializeToElement(new[]
        {
            new { propId = TamaraFenceStateId, crashed = true, cutsceneWatched = false, impactX, impactZ }
        });
        var list = planned.Effects.ToList();
        list.Add(new StateEffect(StateEffectOperation.Set, WorldPropsStateKey,
            WritePlacement(context.State, placement)));
        return new CommandPlan(
            Effects: list,
            Events: [new("tamara-fence.crashed", command.Payload.Clone())]);
    }

    private CommandPlan HandleTamaraFenceDeliver(GameCommand command, RuntimeCommandContext context)
    {
        var state = context.State;
        if (!TamaraNpcFlag(state, "fence_crashed"))
            return new CommandPlan(Rejection: new("tamara-fence-not-started", "Рано."));
        if (TamaraNpcFlag(state, "fence_repaired"))
            return new CommandPlan(Rejection: new("tamara-fence-already-repaired", "Забор уже починен."));
        var carriedKeys = new List<string>();
        for (var board = 1; board <= 6; board++)
        {
            if (TamaraNpcFlag(state, $"board_{board}")) carriedKeys.Add($"board_{board}");
        }

        if (carriedKeys.Count == 0)
            return new CommandPlan(Rejection: new("tamara-fence-no-boards", "Досок при себе нет."));
        var delivered = 0;
        if (state.TryGetProperty("npc", out var npc)
            && npc.TryGetProperty(TamaraCharacterId, out var tamara)
            && tamara.TryGetProperty("boards_delivered", out var count)
            && count.ValueKind == JsonValueKind.Number)
        {
            _ = count.TryGetInt32(out delivered);
        }

        if (delivered is < 0 or > 6 || delivered + carriedKeys.Count > 6)
            return new CommandPlan(Rejection: new("tamara-fence-invalid-count", "Счёт досок испорчен."));
        var authored = new List<object>
        {
            new { op = "npc.set-state", characterId = TamaraCharacterId, stateKey = "boards_delivered", value = delivered + carriedKeys.Count }
        };
        authored.AddRange(carriedKeys.Select(key =>
            (object)new { op = "npc.set-state", characterId = TamaraCharacterId, stateKey = key, value = false }));
        var planned = ContentRuleEngine.PlanEffects(JsonSerializer.SerializeToElement(authored), state);
        return new CommandPlan(
            Effects: planned.Effects,
            Events: [new("tamara-fence.delivered", JsonSerializer.SerializeToElement(
                new { delivered = delivered + carriedKeys.Count, handedOver = carriedKeys.Count }))]);
    }
}
