using System.Text.Json.Nodes;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Scenes;
using Xunit;

namespace Urman.Studio.Tests;

// NPC02 / CINE09: what the routine editor shows before anyone runs the game.
public sealed class RoutineCheckTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urman-studio-tests", Guid.NewGuid().ToString("N"));

    public RoutineCheckTests()
    {
        Write("game/content/animations/catalog.v1.json", """
            { "schemaVersion": 1, "entities": [
              { "id": "urman.anim:idle", "kind": "motion", "params": {} },
              { "id": "urman.anim:walk", "kind": "motion", "params": {} } ] }
            """);
        Write("game/content/world/test.world.v1.json", """
            { "schemaVersion": 1, "kind": "urman.world-plot", "executor": "generic", "id": "urman.world:test", "entities": [
              { "id": "urman.world:test/rustem", "kind": "npc", "name": "Рустем", "params": { "characterId": "urman.test:character/rustem", "position": [0, 0, 0], "schedule": [
                { "id": "b1", "name": "У колодца днём", "when": [ { "op": "quest.status", "questId": "urman.test:quest/well", "status": "active" } ], "place": [5, 0, 5], "motion": "urman.anim:idle" },
                { "id": "b2", "name": "Дома", "place": [0, 0, 0], "motion": "urman.anim:sit" },
                { "id": "b3", "name": "На лавке", "place": [9, 0, 9] } ] } },
              { "id": "urman.world:test/alsu", "kind": "npc", "name": "Алсу", "params": { "characterId": "urman.test:character/alsu", "position": [1, 0, 1], "schedule": [
                { "id": "c1", "name": "У колодца после", "when": [ { "op": "quest.status", "questId": "urman.test:quest/well", "status": "completed" } ], "place": [5.3, 0, 5], "motion": "urman.anim:idle" },
                { "id": "c2", "name": "Тоже у дома", "place": [0.2, 0, 0.1], "motion": "urman.anim:walk" } ] } },
              { "id": "urman.world:test/timur", "kind": "npc", "name": "Тимур", "params": { "characterId": "urman.test:character/timur", "position": [3, 0, 3], "schedule": [
                { "id": "d1", "name": "Провожает", "when": [ { "op": "quest.status", "questId": "urman.test:quest/walk", "status": "active" } ], "follow": { "distance": 2.5 } },
                { "id": "d2", "name": "У ворот", "place": [0.1, 0, 0.1], "onBlocked": "safe-point", "motion": "urman.anim:idle" } ] } } ] }
            """);
        Write("game/content/cutscenes/one.cutscene.v1.json", """
            { "schemaVersion": 1, "kind": "urman.cutscene", "id": "urman.cutscene:one", "name": "Первая", "actors": { "r": "urman.test:character/rustem" }, "entities": [] }
            """);
        Write("game/content/cutscenes/two.cutscene.v1.json", """
            { "schemaVersion": 1, "kind": "urman.cutscene", "id": "urman.cutscene:two", "name": "Вторая", "occupancy": "cancel", "actors": { "r": "urman.test:character/rustem" }, "entities": [] }
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void RoutineNotesNameUnreachableBlocksMissingMotionsSharedSpotsAndScenes()
    {
        var workspace = StudioWorkspace.Open(_root);
        var notes = RoutineCheck.Notes(workspace, new EntityCatalog(workspace)).Select(note => note.Text).ToArray();

        Assert.Contains(notes, text => text.Contains("«На лавке» никогда не наступит", StringComparison.Ordinal));
        Assert.Contains(notes, text => text.Contains("urman.anim:sit нет в библиотеке", StringComparison.Ordinal));
        // Both unconditional home blocks can hold together: flagged.
        Assert.Contains(notes, text => text.Contains("«Дома» и", StringComparison.Ordinal) && text.Contains("Тоже у дома", StringComparison.Ordinal));
        // Well blocks need the same quest active vs completed: never together, not flagged.
        Assert.DoesNotContain(notes, text => text.Contains("«У колодца днём» и", StringComparison.Ordinal));
        Assert.Contains(notes, text => text.Contains("занимает персонажа; распорядок уступает", StringComparison.Ordinal));
        Assert.Contains(notes, text => text.Contains("вторая не начнётся", StringComparison.Ordinal));
        Assert.Contains(notes, text => text.Contains("«У ворот»: выбрана безопасная точка, но она не задана", StringComparison.Ordinal));
        // An escort block has no place of its own: neither "no place" nor "same spot".
        Assert.DoesNotContain(notes, text => text.Contains("«Провожает»", StringComparison.Ordinal));
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
