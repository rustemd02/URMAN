using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Core.Contracts;
using Urman.Core.Runtime;
using Urman.Core.World;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    private const string PocketOwner = "player/pocket";

    public IReadOnlyList<ShopLedgerEntry> ShopLedger()
    {
        if (_kernel is null) return [];
        var props = SelectWorldProps();
        return ShopCatalog.All.Where(product => props.TryGetProperty("shop/purchase/" + product.Sku, out _))
            .Select(product => new ShopLedgerEntry(product.Sku, product.ItemId, product.Title, product.Unit,
                HasPocketShopItem(product.Sku))).ToArray();
    }

    public bool HasPocketShopItem(string sku)
    {
        if (_kernel is null || ShopCatalog.Find(sku) is not { } product) return false;
        return ReadCustodyItems(_kernel.SelectState()).Any(item => item["itemId"]?.GetValue<string>() == product.ItemId
            && item["custodyOwnerId"]?.GetValue<string>() == PocketOwner);
    }

    public string ShopLedgerText()
    {
        var entries = ShopLedger();
        return entries.Count == 0 ? "Страница Мансура\n\nНовых покупок пока нет."
            : "Страница Мансура — на бабая\n\n" + string.Join("\n",
                entries.Select(entry => "• " + entry.Title + " — " + entry.Unit))
                + "\n\nПолучил Айдар.";
    }

    public async Task<bool> PurchaseShopItemAsync(string sku)
    {
        var session = SessionIdentity;
        if (session is null || ShopCatalog.Find(sku) is null || FindPlayer() is not { } player
            || player.VehicleControlled
            || GetTree().GetFirstNodeInGroup("act1_connected_world") is not Act1ConnectedWorld world
            || world.FacilityInteriorAt(player.GlobalPosition) != "shop"
            || !world.CanUsePublicBuildingInteraction("urman.chapter1:local/shop-counter")
            || GetTree().GetFirstNodeInGroup("village_shop_counter") is not Node3D counter
            || player.GlobalPosition.DistanceTo(counter.GlobalPosition) > 3.4f) return false;
        var result = await session.DispatchAsync(new GameCommand(
            $"shop.purchase:{sku}:{Interlocked.Increment(ref _interactionSequence):D8}", "shop.purchase",
            JsonSerializer.SerializeToElement(new { sku })));
        if (!ReferenceEquals(session, SessionIdentity)) return false;
        if (result.Status != CommandStatus.Committed)
        {
            player.NotifyTraversal(result.Error?.Message ?? "Не удалось записать покупку.");
            return false;
        }
        QueueRuntimeStateChanged();
        await SaveCheckpointAsync(force: true);
        return true;
    }

    public async Task<bool> TryUsePocketShopItemAsync(string sku, string useId)
    {
        if (sku == "matches") return useId == "bathhouse/stove" && await FireBathStoveAsync(purchasedMatches: true);
        var session = SessionIdentity;
        if (session is null || ShopCatalog.Find(sku) is not { } product
            || !product.Uses.Contains(useId, StringComparer.Ordinal)
            || GetTree().GetFirstNodeInGroup("act1_connected_world") is not Act1ConnectedWorld world
            || !world.CanUseShopSupply(sku, useId)) return false;
        var result = await session.DispatchAsync(new GameCommand(
            $"shop.use:{sku}:{Interlocked.Increment(ref _interactionSequence):D8}", "shop.use",
            JsonSerializer.SerializeToElement(new { sku, useId })));
        if (!ReferenceEquals(session, SessionIdentity) || result.Status != CommandStatus.Committed) return false;
        QueueRuntimeStateChanged();
        await SaveCheckpointAsync(force: true);
        return true;
    }

    private static CommandPlan HandleShopPurchase(GameCommand command, RuntimeCommandContext context)
    {
        var sku = command.Payload.GetProperty("sku").GetString()!;
        if (ShopCatalog.Find(sku) is not { } product)
            return new CommandPlan(Rejection: new("shop-unknown-product", "Такого товара нет."));
        var key = "shop/purchase/" + sku;
        if (context.State.TryGetProperty(WorldPropsStateKey, out var props) && props.TryGetProperty(key, out _))
            return new CommandPlan(Rejection: new("shop-already-received", "Эта покупка уже записана на бабая."));
        var items = ReadCustodyItems(context.State);
        if (items.Any(item => item["itemId"]?.GetValue<string>() == product.ItemId))
            return new CommandPlan(Rejection: new("shop-item-already-owned", "Предмет уже получен."));
        items.Add(new JsonObject { ["itemId"] = product.ItemId, ["custodyOwnerId"] = PocketOwner, ["condition"] = new JsonObject() });
        var custody = new JsonArray(items.Select(item => (JsonNode?)item.DeepClone()).ToArray());
        var placement = JsonSerializer.SerializeToElement(new[] { new
        {
            propId = key, itemId = product.ItemId, account = "mansur", quantity = 1, received = true
        } });
        // Both item ownership and the debt-book row commit in one kernel plan.
        // Consuming an item later never deletes the original purchase receipt.
        return new CommandPlan(
            Effects: [
                new StateEffect(StateEffectOperation.Set, CustodyStore.DefaultStateKey, JsonSerializer.SerializeToElement(custody)),
                new StateEffect(StateEffectOperation.Set, WorldPropsStateKey, WritePlacement(context.State, placement))
            ],
            Events: [new("shop.purchase.recorded", JsonSerializer.SerializeToElement(new { sku, itemId = product.ItemId, account = "mansur" }))],
            Claims: [new ResourceClaim("item/" + product.ItemId, "shop/custody/" + sku, WorldPropsClaimScope, ResourceClaimMode.Exclusive)]);
    }

    private CommandPlan HandleShopUse(GameCommand command, RuntimeCommandContext context)
    {
        var sku = command.Payload.GetProperty("sku").GetString()!;
        var useId = command.Payload.GetProperty("useId").GetString()!;
        if (ShopCatalog.Find(sku) is not { } product || !product.Uses.Contains(useId, StringComparer.Ordinal))
            return new CommandPlan(Rejection: new("shop-invalid-use", "Этот предмет здесь не подходит."));
        if (sku == "matches") return PlanBathIgnition(context.State, _playTimeSeconds, purchasedMatches: true);
        var operations = JsonSerializer.SerializeToElement(new[] { new { op = "consume", itemId = product.ItemId, ownerId = PocketOwner } });
        var plan = CustodyStore.PlanBatch(context.State, operations, "shop/custody/" + sku, WorldPropsClaimScope);
        if (plan.Effects is null || plan.Effects.Any(effect => effect.Operation == StateEffectOperation.Increment)) return plan;
        var effects = plan.Effects.ToList();
        var placement = JsonSerializer.SerializeToElement(new[] { new { propId = "shop/used/" + sku, useId, used = true } });
        effects.Add(new StateEffect(StateEffectOperation.Set, WorldPropsStateKey, WritePlacement(context.State, placement)));
        return plan with { Effects = effects };
    }
}
