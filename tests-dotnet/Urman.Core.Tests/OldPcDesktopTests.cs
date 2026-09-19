using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Capabilities;
using Urman.Core.Capabilities.OldPc;
using Urman.Core.Contracts;
using Xunit;

namespace Urman.Core.Tests;

public sealed class OldPcDesktopTests
{
    private const string DocumentId = "urman.oldpc:document/msg_marat_saved_last_normal";
    private const string InstanceId = "oldpc:house";

    [Fact]
    public void Desktop_RoundTripPreservesWindowsNotesTrashAndHistoryWithoutReadingSources()
    {
        var desktop = new OldPcDesktopSnapshot
        {
            Windows = [
                new() { Id = "archive", X = .12f, Y = .1f, Width = .8f, Height = .8f, Minimized = true },
                new() { Id = "browser", X = .2f, Y = .2f, Width = .7f, Height = .7f }
            ],
            ActiveWindowId = "browser",
            Files = [
                new() { Id = "note-a", Title = "Позвонить.txt", Text = "Әби попросила позвонить маме." },
                new() { Id = "note-b", Title = "Черновик.txt", Text = "Не отправлено", Deleted = true }
            ],
            NoteId = "note-a",
            BrowserHistory = ["home", "yalkyn", "doc:" + DocumentId],
            BrowserIndex = 2,
            VisitedDocumentIds = [DocumentId],
            ElapsedSeconds = 37.5
        };
        CapabilitySessionSnapshot saved;
        using (var host = NewHost())
        {
            var result = host.Handle(InstanceId, DesktopInput(desktop));
            Assert.Equal(1, result.GetProperty("nextActionSequence").GetInt64());
            Assert.Equal(JsonValueKind.Null, result.GetProperty("activeDocumentId").ValueKind);
            Assert.Empty(result.GetProperty("savedDocumentIds").EnumerateArray());
            saved = host.Capture(InstanceId);
        }
        using var restored = NewHost(saved);
        var copy = ReadDesktop(restored);
        Assert.True(copy.Windows[0].Minimized);
        Assert.Equal(.2f, copy.Windows[1].X);
        Assert.Equal("browser", copy.ActiveWindowId);
        Assert.Equal("Әби попросила позвонить маме.", copy.Files[0].Text);
        Assert.True(copy.Files[1].Deleted);
        Assert.Equal(2, copy.BrowserIndex);
        Assert.Equal(DocumentId, Assert.Single(copy.VisitedDocumentIds));
        Assert.Equal(37.5, copy.ElapsedSeconds);
    }

    [Fact]
    public void Desktop_RestoresLegacyFiveFieldSnapshot()
    {
        var legacy = new CapabilitySessionSnapshot(InstanceId, OldPcCapabilityProvider.Protocol, "1.0.0", 1,
            JsonSerializer.SerializeToElement(new
            {
                activeDocumentId = DocumentId, activeSection = "saved_messages",
                nextActionSequence = 7, query = "Марат", savedDocumentIds = new[] { DocumentId }
            }));
        using var host = NewHost(legacy);
        var state = host.Capture(InstanceId).State;
        Assert.Equal(DocumentId, state.GetProperty("activeDocumentId").GetString());
        Assert.Equal(7, state.GetProperty("nextActionSequence").GetInt32());
        Assert.Equal("Марат", state.GetProperty("query").GetString());
        Assert.Empty(ReadDesktop(host).Files);
        Assert.Empty(ReadDesktop(host).Windows);
    }

    [Fact]
    public void Desktop_RoundTripsChatHintsAndTetrisRecordAlongsideNineWindows()
    {
        using var host = NewHost();
        host.Handle(InstanceId, DesktopInput(new()
        {
            Windows =
            [
                new() { Id = "chat", X = .1f, Y = .1f, Width = .7f, Height = .7f },
                new() { Id = "tetris", X = .2f, Y = .2f, Width = .5f, Height = .5f, Minimized = true }
            ],
            Chat =
            [
                new() { Id = "alsu", Unread = true, Messages =
                    [new() { From = "npc", Text = "Сәлам, Айдар." }, new() { From = "player", Text = "Исәнмесез." }] }
            ],
            HintsSeen = ["hint-look-at-registry"],
            TetrisHigh = 12345
        }));
        var copy = ReadDesktop(host);
        Assert.Equal(["chat", "tetris"], copy.Windows.Select(window => window.Id));
        Assert.Equal(12345, copy.TetrisHigh);
        Assert.Equal(["hint-look-at-registry"], copy.HintsSeen);
        var thread = Assert.Single(copy.Chat);
        Assert.Equal("alsu", thread.Id);
        Assert.True(thread.Unread);
        Assert.Equal(2, thread.Messages.Count);
        Assert.Equal("npc", thread.Messages[0].From);
        Assert.Equal("player", thread.Messages[1].From);
    }

    [Fact]
    public void Desktop_DefaultsNewFieldsForASnapshotWrittenBeforeTheyExisted()
    {
        using var host = NewHost();
        host.Handle(InstanceId, DesktopInput(new()
        {
            Files = [new() { Id = "note-kept", Title = "Заметка.txt", Text = "Из старого сейва" }]
        }));
        var saved = JsonNode.Parse(host.Capture(InstanceId).State.GetRawText())!;
        var desktop = saved["desktop"]!.AsObject();
        Assert.True(desktop.Remove("chat"));
        Assert.True(desktop.Remove("hintsSeen"));
        Assert.True(desktop.Remove("tetrisHigh"));
        host.Handle(InstanceId, JsonSerializer.SerializeToElement(new { type = "desktop", desktop = saved["desktop"] }));
        var copy = ReadDesktop(host);
        Assert.Empty(copy.Chat);
        Assert.Empty(copy.HintsSeen);
        Assert.Equal(0, copy.TetrisHigh);
        Assert.Equal("Из старого сейва", Assert.Single(copy.Files).Text);
    }

    [Fact]
    public void Desktop_DropsWindowsOfProgramsThisBuildDoesNotShip()
    {
        using var host = NewHost();
        host.Handle(InstanceId, DesktopInput(new() { Windows = [new() { Id = "browser" }] }));
        var desktop = JsonNode.Parse(host.Capture(InstanceId).State.GetRawText())!["desktop"]!.AsObject();
        desktop["windows"]!.AsArray().Add(JsonNode.Parse(
            """{"id":"legacy-net","x":0.1,"y":0.1,"width":0.5,"height":0.5,"minimized":false,"maximized":false}"""));
        desktop["activeWindowId"] = "legacy-net";
        host.Handle(InstanceId, JsonSerializer.SerializeToElement(new { type = "desktop", desktop = desktop }));
        var copy = ReadDesktop(host);
        Assert.Equal("browser", Assert.Single(copy.Windows).Id);
        Assert.Null(copy.ActiveWindowId);
    }

    [Fact]
    public void Desktop_RejectsOutOfBoundsChatHintsAndTetris()
    {
        using var host = NewHost();
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(new()
        {
            Chat = [.. Enumerable.Range(0, 5).Select(_ => new OldPcChatThreadSnapshot { Id = "alsu" })]
        })));
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(new()
        {
            Chat = [new() { Id = "rinat", Messages = [.. Enumerable.Range(0, 65)
                .Select(_ => new OldPcChatMessageSnapshot { From = "npc", Text = "Строка" })] }]
        })));
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(new()
        {
            Chat = [new() { Id = "mansur", Messages =
                [new() { From = "npc", Text = new string('я', 501) }] }]
        })));
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(new()
        {
            Chat = [new() { Id = "unknown-thread" }]
        })));
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(new()
        {
            HintsSeen = [.. Enumerable.Range(0, 65).Select(index => $"hint-{index}")]
        })));
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(new()
        {
            TetrisHigh = 1000000
        })));
    }

    [Fact]
    public void Desktop_InvalidMutationDoesNotLosePreviousState()
    {
        using var host = NewHost();
        var valid = new OldPcDesktopSnapshot
        {
            Files = [new() { Id = "note-kept", Title = "Заметка.txt", Text = "Сохранить" }]
        };
        host.Handle(InstanceId, DesktopInput(valid));
        var before = host.Capture(InstanceId).State.GetRawText();
        valid.Files.Add(new() { Id = "note-kept", Title = "Дубликат.txt" });
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(valid)));
        Assert.Equal(before, host.Capture(InstanceId).State.GetRawText());
    }

    [Theory]
    [InlineData("windows", "x")]
    [InlineData("windows", "minimized")]
    [InlineData("files", "text")]
    [InlineData("files", "deleted")]
    public void Desktop_RejectsIncompleteEntriesOnInputAndRestore(string collection, string missingProperty)
    {
        using var host = NewHost();
        host.Handle(InstanceId, DesktopInput(new()
        {
            Windows = [new() { Id = "browser" }],
            Files = [new() { Id = "note-kept", Title = "Заметка.txt", Text = "Не терять" }]
        }));
        var before = host.Capture(InstanceId).State.GetRawText();
        var incomplete = JsonNode.Parse(before)!;
        Assert.True(incomplete["desktop"]![collection]![0]!.AsObject().Remove(missingProperty));
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId,
            JsonSerializer.SerializeToElement(new { type = "desktop", desktop = incomplete["desktop"] })));
        Assert.Equal(before, host.Capture(InstanceId).State.GetRawText());
        var damagedSave = new CapabilitySessionSnapshot(InstanceId, OldPcCapabilityProvider.Protocol,
            "1.0.0", 1, JsonSerializer.SerializeToElement(incomplete));
        Assert.Throws<InvalidDataException>(() => NewHost(damagedSave));
    }

    [Fact]
    public void Desktop_RejectsUnknownPagesOversizedNotesAndOffscreenWindows()
    {
        using var host = NewHost();
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(new()
        {
            BrowserHistory = ["https://example.com/"], BrowserIndex = 0
        })));
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(new()
        {
            Files = [new() { Id = "note-large", Title = "Заметка.txt", Text = new string('x', 32769) }]
        })));
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(new()
        {
            Windows = [new() { Id = "browser", X = 2 }]
        })));
        Assert.Throws<InvalidDataException>(() => host.Handle(InstanceId, DesktopInput(new()
        {
            VisitedDocumentIds = ["urman.oldpc:document/unknown"]
        })));
    }

    [Fact]
    public void Desktop_DeleteRestoreOnlyTouchesPersonalFileAndPreservesArchiveState()
    {
        using var host = NewHost();
        host.Handle(InstanceId, JsonSerializer.SerializeToElement(new { type = "open", documentId = DocumentId }));
        host.Handle(InstanceId, JsonSerializer.SerializeToElement(new { type = "save", documentId = DocumentId }));
        var desktop = new OldPcDesktopSnapshot { Files = [new() { Id = "note-a", Title = "Список.txt" }] };
        desktop.Files[0].Deleted = true;
        host.Handle(InstanceId, DesktopInput(desktop));
        desktop = ReadDesktop(host);
        desktop.Files[0].Deleted = false;
        host.Handle(InstanceId, DesktopInput(desktop));
        var state = host.Capture(InstanceId).State;
        Assert.False(ReadDesktop(host).Files[0].Deleted);
        Assert.Equal(DocumentId, state.GetProperty("activeDocumentId").GetString());
        Assert.Equal(DocumentId, Assert.Single(state.GetProperty("savedDocumentIds").EnumerateArray()).GetString());
        Assert.Equal(3, state.GetProperty("nextActionSequence").GetInt64());
    }

    private static OldPcDesktopSnapshot ReadDesktop(CapabilityHost host) =>
        host.Capture(InstanceId).State.GetProperty("desktop").Deserialize<OldPcDesktopSnapshot>(OldPcDesktopSnapshot.JsonOptions)!;

    private static JsonElement DesktopInput(OldPcDesktopSnapshot desktop) =>
        JsonSerializer.SerializeToElement(new { type = "desktop",
            desktop = JsonSerializer.SerializeToElement(desktop, OldPcDesktopSnapshot.JsonOptions) });

    private static CapabilityHost NewHost(CapabilitySessionSnapshot? saved = null)
    {
        var host = new CapabilityHost(new CapabilityRegistry([
            new OldPcCapabilityProvider([new OldPcDocumentDescriptor(DocumentId, "saved_messages")])
        ]));
        host.Create(InstanceId, OldPcCapabilityProvider.Protocol, "1.0.0",
            JsonSerializer.SerializeToElement(new { moduleId = OldPcCapabilityProvider.ModuleId }), saved);
        host.Start(InstanceId);
        return host;
    }
}
