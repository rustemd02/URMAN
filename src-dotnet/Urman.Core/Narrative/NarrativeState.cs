using System.Text.Json;

namespace Urman.Core.Narrative;

public static class NarrativeState
{
    public static JsonElement CreateInitial() => JsonSerializer.SerializeToElement(new
    {
        activeScene = (string?)null,
        knowledge = new Dictionary<string, object>(),
        vocabulary = new Dictionary<string, object>(),
        journal = Array.Empty<object>(),
        dialogueChoices = Array.Empty<object>(),
        quests = new Dictionary<string, object>(),
        npc = new Dictionary<string, object>(),
        beats = new Dictionary<string, object>(),
        pressure = 0,
        routes = Array.Empty<string>(),
        presentation = new
        {
            requestedSceneIds = Array.Empty<string>(),
            openedDocumentIds = Array.Empty<string>()
        }
    });
}
