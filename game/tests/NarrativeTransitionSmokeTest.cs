using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

public partial class NarrativeTransitionSmokeTest : Node
{
    private const string Arrival = "urman.chapter1:scene/arrival_vehicle_dusk";
    private const string House = "urman.chapter1:scene/house";
    private const string Crossroad = "urman.chapter1:scene/crossroad_signs_inspect";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";

    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Narrative transition smoke could not instantiate main.");
            return;
        }

        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null || ActiveScene(bridge) != Arrival)
        {
            Fail("Compiled campaign entrypoint was not applied.");
            return;
        }

        if (!await bridge.DispatchInteractionAsync("urman.chapter1:interaction/arrival-enter-house") || ActiveScene(bridge) != House)
        {
            Fail("Arrival interaction did not enter the authored house scene.");
            return;
        }

        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = OfficialNotice
        }));
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_official_death_version") != "confirmed"
            || bridge.IsInteractionAvailable("urman.chapter1:interaction/house-to-route"))
        {
            Fail("Opening the official Marat notice unlocked the authored house exit before Gulsina's warning dialogue.");
            return;
        }

        if (!bridge.IsInteractionAvailable("urman.chapter1:interaction/talk-gulsina")
            || !await bridge.DispatchInteractionAsync("urman.chapter1:interaction/talk-gulsina")
            || !await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/gulsina_yaramyy", "home-warning")
            || !bridge.IsInteractionAvailable("urman.chapter1:interaction/house-to-route"))
        {
            Fail("Gulsina warning dialogue did not unlock the authored house exit through the shared runtime path.");
            return;
        }

        if (!await bridge.DispatchInteractionAsync("urman.chapter1:interaction/house-to-route") || ActiveScene(bridge) != Crossroad)
        {
            Fail("House interaction did not enter the authored crossroad scene.");
            return;
        }

        if (await bridge.DispatchInteractionAsync("urman.chapter1:interaction/arrival-enter-house"))
        {
            Fail("An interaction from a non-active scene was accepted.");
            return;
        }

        const string missingInteraction = "urman.chapter1:interaction/missing-authored-id";
        if (bridge.IsInteractionAvailable(missingInteraction)
            || await bridge.DispatchInteractionAsync(missingInteraction))
        {
            Fail("A missing compiled interaction was exposed as a generic world command.");
            return;
        }

        GD.Print("narrative-transition-smoke: arrival -> house -> crossroad with source-scene ownership");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private static string? ActiveScene(RuntimeBridge bridge) =>
        bridge.SelectRuntimeState().GetProperty("activeScene").GetString();

    private static string? KnowledgeStatus(JsonElement state, string knowledgeId)
        => state.GetProperty("knowledge")
            .GetProperty($"urman.chapter1:knowledge/{knowledgeId}")
            .GetProperty("status")
            .GetString();

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
