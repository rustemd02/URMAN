using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    internal const string OpeningStateId = "act1/opening";

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
    internal async Task SetTatarLanguageLevelAsync(string level)
    {
        if (FindPlayer() is not { } player) return;
        player.ApplySettings(player.CaptureSettings() with { TatarLanguageLevel = level });
        await DispatchWorldPropsAsync(new JsonArray
        {
            new JsonObject { ["propId"] = OpeningStateId, ["languageLevel"] = player.TatarLanguageLevel }
        });
    }
}
