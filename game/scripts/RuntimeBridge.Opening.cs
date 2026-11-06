using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal const string OpeningStateId = "act1/opening";
    internal const string NivaLanguageDialogueId = "urman.chapter1:dialogue/prologue-niva-language";
    private bool _prologueArrivalLoading;

    private bool ShouldShowPrologueArrivalLoading(CompiledInteractionContent interaction) =>
        interaction.SourceSceneId == Main.PrologueSceneId
        && interaction.TargetSceneId == Main.NivaArrivalSceneId
        && _content.HasPhotoWorlds && DisplayServer.GetName() != "headless"
        && GetTree().GetFirstNodeInGroup("zone_manager") is Main { EnableFullGamePhotoWorldRouting: true };

    private async Task<bool> DispatchPrologueArrivalAsync(CompiledInteractionContent interaction, RuntimeKernel session)
    {
        if (_prologueArrivalLoading || _loadPreparing || !IsInteractionAvailable(interaction.Id)
            || FindPlayer() is not { SessionTransitionActive: false } player) return false;

        _prologueArrivalLoading = true;
        _loadingTimeSampleUsec = Time.GetTicksUsec();
        player.SetSessionTransition(true);
        LoadingScreenUi? loading = null;
        // The nightmare ends on a hard cut: its black already covers the frame,
        // and a loading card would break the silence before the waking voice.
        var dreamCut = PrologueDreamCut.IsActive(GetTree());
        var dispatched = false;
        try
        {
            if (!dreamCut) loading = LoadingScreenUi.Show(this, "Едем в деревню", "Подготавливаем дорогу");
            // The scene commit queues synchronous village construction. Give the cover a frame first.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!ReferenceEquals(session, SessionIdentity) || !CanPhysicallyUseInteraction(interaction.Id)) return false;
            dispatched = await DispatchInteractionWithCheckpointAsync(interaction, session);
            return dispatched;
        }
        finally
        {
            // A rejected exit must not leave the player in front of a black frame.
            if (dreamCut && !dispatched) PrologueDreamCut.Release(fadeSeconds: .4);
            if (loading is not null && GodotObject.IsInstanceValid(loading)) loading.Hide();
            SampleLoadingTime();
            _prologueArrivalLoading = false;
            if (ReferenceEquals(session, _kernel) && !_loadingSlot && !_loadPreparing && GodotObject.IsInstanceValid(player))
                player.SetSessionTransition(NeedsPhysicalRecovery);
        }
    }

    internal bool HasNivaLanguageResponse => _kernel is { } session
        && session.SelectState().TryGetProperty("dialogueChoices", out var choices)
        && choices.ValueKind == JsonValueKind.Array
        && choices.EnumerateArray().Any(choice => choice.TryGetProperty("dialogueId", out var dialogue)
            && dialogue.GetString() == NivaLanguageDialogueId);

    // Shipped saves predate the first-night sequence and already show a broken
    // bridge. Only new sessions opt into day one; loading never rewrites them.
    internal bool FirstNightPassed => !SelectWorldProps().TryGetProperty(OpeningStateId, out var opening)
        || opening.ValueKind != JsonValueKind.Object
        || !opening.TryGetProperty("firstNightPassed", out var passed) || passed.ValueKind != JsonValueKind.False;

    private JsonElement SeedOpeningState(JsonElement initialState)
    {
        var state = JsonNode.Parse(initialState.GetRawText())!.AsObject();
        var props = state[WorldPropsStateKey] as JsonObject ?? new JsonObject();
        if (state[WorldPropsStateKey] is null) state[WorldPropsStateKey] = props;
        props[OpeningStateId] = new JsonObject
        {
            ["firstNightPassed"] = IsDebugSession,
            ["languageLevel"] = FindPlayer()?.TatarLanguageLevel ?? "none"
        };
        return JsonSerializer.SerializeToElement(state);
    }

    internal Task<bool> SetFirstNightPassedAsync(bool passed) => DispatchWorldPropsAsync(new JsonArray
    {
        new JsonObject { ["propId"] = OpeningStateId, ["firstNightPassed"] = passed }
    });

    /// <summary>
    /// The live talk in the Niva sets the starting Tatar density. It is the
    /// same profile field the settings screen edits, so a later manual change
    /// wins; the world prop only records how the session started.
    /// </summary>
    internal async Task<bool> SetTatarLanguageLevelAsync(string level, object? expectedSession = null)
    {
        var session = SessionIdentity;
        if (session is null || _loadPreparing
            || expectedSession is not null && !ReferenceEquals(expectedSession, session)
            || FindPlayer() is not { } player) return false;
        player.ApplySettings(player.CaptureSettings() with { TatarLanguageLevel = level });
        var committed = await DispatchWorldPropsAsync(new JsonArray
        {
            new JsonObject { ["propId"] = OpeningStateId, ["languageLevel"] = player.TatarLanguageLevel }
        });
        return committed && ReferenceEquals(session, SessionIdentity);
    }
}
