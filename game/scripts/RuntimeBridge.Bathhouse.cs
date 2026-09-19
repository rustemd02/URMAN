using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Core.Contracts;
using Urman.Core.Runtime;
using Urman.Core.World;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    public async Task<bool> FireBathStoveAsync(bool purchasedMatches)
    {
        var session = SessionIdentity;
        if (session is null || GetTree().GetFirstNodeInGroup("act1_connected_world") is not Act1ConnectedWorld world
            || !(purchasedMatches ? world.CanUseBathhouseMatches() : world.CanIgniteBathStove())) return false;
        var kind = purchasedMatches ? "shop.use" : "bathhouse.ignite";
        var payload = purchasedMatches
            ? JsonSerializer.SerializeToElement(new { sku = "matches", useId = "bathhouse/stove" })
            : JsonSerializer.SerializeToElement(new { });
        var result = await session.DispatchAsync(new GameCommand(
            $"{kind}:{Interlocked.Increment(ref _interactionSequence):D8}", kind, payload));
        if (!ReferenceEquals(session, SessionIdentity)) return false;
        if (result.Status != CommandStatus.Committed)
        {
            FindPlayer()?.NotifyTraversal(result.Error?.Message ?? "Сейчас растопить печь не получилось.");
            return false;
        }
        QueueRuntimeStateChanged();
        await SaveCheckpointAsync(force: true);
        return ReferenceEquals(session, SessionIdentity);
    }

    private CommandPlan HandleBathIgnition(GameCommand command, RuntimeCommandContext context) =>
        PlanBathIgnition(context.State, _playTimeSeconds, purchasedMatches: false);

    // Both ignition choices read the latest state inside the kernel transaction.
    // A second input cannot consume another log or overwrite the first ignition.
    private static CommandPlan PlanBathIgnition(JsonElement state, double now, bool purchasedMatches)
    {
        var logs = 5.0;
        var burnUntil = 0.0;
        if (state.TryGetProperty(WorldPropsStateKey, out var props) && props.ValueKind != JsonValueKind.Object)
            return new CommandPlan(Rejection: new("bathhouse-invalid-state", "Не удалось проверить состояние печи."));
        if (props.ValueKind == JsonValueKind.Object && props.TryGetProperty("bathhouse/stove", out var stove))
        {
            if (stove.ValueKind != JsonValueKind.Object)
                return new CommandPlan(Rejection: new("bathhouse-invalid-state", "Не удалось проверить состояние печи."));
            if (stove.TryGetProperty("logsRemaining", out var savedLogs)
                && (savedLogs.ValueKind != JsonValueKind.Number || !savedLogs.TryGetDouble(out logs)))
                return new CommandPlan(Rejection: new("bathhouse-invalid-state", "Не удалось проверить запас дров."));
            if (stove.TryGetProperty("burnUntil", out var savedBurn)
                && (savedBurn.ValueKind != JsonValueKind.Number || !savedBurn.TryGetDouble(out burnUntil)))
                return new CommandPlan(Rejection: new("bathhouse-invalid-state", "Не удалось проверить состояние печи."));
        }
        if (!double.IsFinite(now) || !double.IsFinite(burnUntil) || !double.IsFinite(logs)
            || logs < 0 || logs > 5 || logs != Math.Floor(logs))
            return new CommandPlan(Rejection: new("bathhouse-invalid-state", "Не удалось проверить состояние печи."));
        if (burnUntil > now)
            return new CommandPlan(Rejection: new("bathhouse-already-burning", "Полено ещё горит; добавлять дрова пока не нужно."));
        if (logs < 1)
            return new CommandPlan(Rejection: new("bathhouse-no-firewood", "Сухие поленья на полке закончились."));

        var plan = new CommandPlan(Effects: []);
        var placement = new JsonArray();
        if (purchasedMatches)
        {
            if (ShopCatalog.Find("matches") is not { } product)
                return new CommandPlan(Rejection: new("shop-unknown-product", "Такого товара нет."));
            var operations = JsonSerializer.SerializeToElement(new[]
                { new { op = "consume", itemId = product.ItemId, ownerId = PocketOwner } });
            plan = CustodyStore.PlanBatch(state, operations, "shop/custody/matches", WorldPropsClaimScope);
            if (plan.Effects is null || plan.Effects.Any(effect => effect.Operation == StateEffectOperation.Increment))
                return plan;
            placement.Add(new JsonObject { ["propId"] = "shop/used/matches", ["useId"] = "bathhouse/stove", ["used"] = true });
        }
        placement.Add(new JsonObject { ["propId"] = "bathhouse/stove",
            ["logsRemaining"] = logs - 1, ["burnUntil"] = now + 180, ["heatUntil"] = now + 600 });
        var effects = plan.Effects!.ToList();
        effects.Add(new StateEffect(StateEffectOperation.Set, WorldPropsStateKey,
            WritePlacement(state, JsonSerializer.SerializeToElement(placement))));
        return plan with { Effects = effects };
    }
}
