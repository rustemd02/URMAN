using Godot;

namespace Urman.Godot.Tests;

public partial class Act1FacilitiesSmokeTest
{
    private async Task CheckOptionalMatches()
    {
        var shop = _world.PublicBuildingRooms.Single(room => room.Id == "shop");
        await LocalStart(shop.Room.ToGlobal(new(.65f, .035f, -.58f)), "explicit supported shop-counter fixture for the optional supply test");
        var counter = Target("urman.chapter1:local/shop-counter");
        Aim(counter.GlobalPosition); await Frames(3);
        Check(AimedAt(counter) && _world.CanUsePublicBuildingInteraction(counter.InteractionId), "the actual shop counter is reachable before purchase");
        Check(await _bridge.PurchaseShopItemAsync("matches"), "buy the one matchbox through the normal guarded purchase command");
        Check(_bridge.HasPocketShopItem("matches") && _bridge.ShopLedger().Count(entry => entry.Sku == "matches") == 1,
            "the obtained matchbox and single debt-book row agree");
        var outsideState = BathProps();
        Check(!_world.CanIgniteBathStove() && !_world.CanUseBathhouseMatches()
            && !await _bridge.FireBathStoveAsync(true) && BathProps() == outsideState && _bridge.HasPocketShopItem("matches"),
            "owning matches at the shop cannot ignite the distant bath or consume the box");

        var feet = _bath.ToGlobal(new(.55f, .035f, -.40f));
        await LocalStart(feet, "explicit previously traversed bath heater position for the optional-source comparison");
        var stove = Target("urman.chapter1:local/bathhouse/stove");
        Aim(stove.GlobalPosition); await Frames(3);
        Check(_world.CanIgniteBathStove() && _world.CanUseBathhouseMatches(), "both sources are physically usable at the real cold stove");
        Check(await _bridge.SaveSlotAsync("facilities-matches-cold"), "save attained purchase and the cold stove before choosing a source");

        var crate = _carry.Items.Single(item => item.ItemId == "carry-crate");
        await ApproachCarry(crate);
        await Press("interact"); await _carry.PendingAction;
        Check(_carry.HeldItem == crate, "obtain the real crate for the occupied-hands refusal");
        await LocalStart(feet, "explicit same stove fixture while carrying the acquired crate");
        Aim(stove.GlobalPosition); await Frames(3);
        Check(!_world.CanIgniteBathStove() && !_world.CanUseBathhouseMatches() && !await _bridge.FireBathStoveAsync(true)
            && _carry.HeldItem == crate && _bridge.HasPocketShopItem("matches") && VisibleLogs() == 5,
            "occupied hands refuse ignition without consuming fuel, the purchase or the carried item");
        await Load("facilities-matches-cold");
        Check(_carry.HeldItem is null && _bridge.HasPocketShopItem("matches"), "cold comparison save restores attained empty hands and pocket supply");

        var beforeChoice = BathProps();
        var choice = await OpenIgnitionChoice(stove);
        choice.ApplyAccessibilitySettings(_player.Accessibility with { TextScale = 1.6, HighContrast = true });
        await Frames(3);
        var panel = choice.GetNode<Control>("Screen/Ignition");
        Check(GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect()), "large-text ignition choice remains inside the real viewport");
        Check(BathProps() == beforeChoice && _bridge.HasPocketShopItem("matches") && VisibleLogs() == 5,
            "opening the source choice does not spend a log or a matchbox");
        await Capture("11_bath_ignition_choice_160pct");
        ChoiceButton(choice, "Cancel").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntil(() => FacilityIdle() && !_player.ModalOpen, "cancel closes the choice and releases its modal without action");
        Check(BathProps() == beforeChoice && _bridge.HasPocketShopItem("matches"), "cancel leaves the cold stove and purchase unchanged");

        choice = await OpenIgnitionChoice(stove);
        ChoiceButton(choice, "FamilyMatches").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntil(FacilityIdle, "family ignition and its ordinary checkpoint finish");
        Check(VisibleLogs() == 4 && _bridge.HasPocketShopItem("matches") && !Flag("shop/used/matches", "used")
            && !_bath.GetNode<Node3D>("BathPurchasedMatchbox").Visible,
            "family choice consumes one log while retaining the bought box in the pocket");
        await Load("facilities-matches-cold");

        choice = await OpenIgnitionChoice(stove);
        ChoiceButton(choice, "PurchasedMatches").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntil(FacilityIdle, "purchased ignition and its single checkpoint finish");
        Check(VisibleLogs() == 4 && Number("bathhouse/stove", "heatUntil") > _bridge.PlayTimeSeconds
            && !_bridge.HasPocketShopItem("matches") && Flag("shop/used/matches", "used")
            && _bath.GetNode<Node3D>("BathPurchasedMatchbox").Visible
            && _bridge.ShopLedger().Count(entry => entry.Sku == "matches") == 1,
            "one purchased ignition commits fuel, heat, pocket consumption, shelf projection and the original single debt row");
        var afterIgnition = BathProps();
        Check(!await _bridge.FireBathStoveAsync(true) && !await _bridge.FireBathStoveAsync(false)
            && BathProps() == afterIgnition && VisibleLogs() == 4,
            "repeated bought and family attempts while burning cannot spend another log or recreate the purchase");
        Check(await _bridge.SaveSlotAsync("facilities-matches-used"), "save the attained purchased ignition");
        await Capture("12_bath_purchased_matches_result");
        await Load("facilities-matches-cold");
        Check(_bridge.HasPocketShopItem("matches") && VisibleLogs() == 5 && !_bath.GetNode<Node3D>("BathPurchasedMatchbox").Visible,
            "earlier save restores the pocket box, five logs and an empty purchased-box shelf position");
        await Load("facilities-matches-used");
        Check(!_bridge.HasPocketShopItem("matches") && VisibleLogs() == 4 && _bath.GetNode<Node3D>("BathPurchasedMatchbox").Visible,
            "later save restores the consumed box on its shelf and four logs without duplication");

        await Load("facilities-matches-cold");
        var attempts = new[] { _bridge.FireBathStoveAsync(false), _bridge.FireBathStoveAsync(true) };
        var accepted = await Task.WhenAll(attempts);
        await Frames(4);
        Check(accepted.Count(value => value) == 1 && VisibleLogs() == 4
            && _bridge.HasPocketShopItem("matches") == !accepted[1]
            && Flag("shop/used/matches", "used") == accepted[1],
            "overlapping family and purchased API attempts commit exactly one coherent ignition");

        await Load("facilities-matches-cold");
        choice = await OpenIgnitionChoice(stove);
        // This call intentionally starts a real load while the local choice is
        // open. The usual Load helper waits for user action and is inappropriate.
        Check(await _bridge.LoadSlotAsync("facilities-pristine"), "ordinary load cancels a pending source choice");
        await Frames(8);
        Check(!choice.IsOpen && !_player.ModalOpen && FacilityIdle() && VisibleLogs() == 5
            && !_bridge.HasPocketShopItem("matches") && !Flag("shop/used/matches", "used"),
            "loading closes the choice, releases its modal and prevents a late ignition in the new session");
    }

    private async Task<BathIgnitionChoiceUi> OpenIgnitionChoice(InteractionTarget stove)
    {
        await EnsureFocus();
        await WaitUntil(FacilityIdle, "previous action completes before opening the ignition choice");
        Aim(stove.GlobalPosition); await Frames(3);
        Check(AimedAt(stove) && stove.IsAvailable(), "source choice begins at the actual aimed stove");
        await Press("interact");
        await WaitUntil(() => GetTree().GetFirstNodeInGroup("bath_ignition_choice_ui") is BathIgnitionChoiceUi { IsOpen: true },
            "the existing stove target opens the source choice for an owned purchase");
        return (BathIgnitionChoiceUi)GetTree().GetFirstNodeInGroup("bath_ignition_choice_ui");
    }

    private static Button ChoiceButton(BathIgnitionChoiceUi choice, string name) =>
        choice.FindChildren(name, "Button", true, false).OfType<Button>().Single();
}
