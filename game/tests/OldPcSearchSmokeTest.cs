using Godot;
using System.Text.Json;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// Focused proof of the global search (§2): one query reaches the readable
/// documents, the delivered chat history, Aidar's own notes and the pages he
/// really visited; a locked record never appears as a title, only as the same
/// non-selectable «🔒 Закрытые записи» line; a term link re-runs the query and
/// nothing in the search writes knowledge.
/// </summary>
public partial class OldPcSearchSmokeTest : Node
{
    private const string LockedRegisterTitle = "Строка реестра: дело Марата";
    private const string ResultsPath = "Screen/Computer/Layout/WorkArea/Results";
    private const string ReaderPath = "Screen/Computer/Layout/WorkArea/ReaderArea/Reader";
    private const string StatusPath = "Screen/Computer/Layout/Footer/Status";
    private readonly List<string> _checks = [];

    public override async void _Ready()
    {
        Main? main = null;
        var exit = 1;
        try
        {
            main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")!.Instantiate<Main>();
            AddChild(main);
            await Frames(3);
            var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge
                ?? throw new InvalidOperationException("Missing shared runtime.");
            var ui = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi
                ?? throw new InvalidOperationException("Missing existing old-PC UI.");
            ui.Open(bridge);
            ui.ApplyAccessibilitySettings(AccessibilitySettingsSnapshot.Default);
            await Frames(4);

            var query = ui.GetNode<LineEdit>("Screen/Computer/Layout/SearchRow/Query");
            var everywhere = ui.GetNode<Button>("Screen/Computer/Layout/SearchRow/SearchEverywhere");
            var results = ui.GetNode<ItemList>(ResultsPath);
            var reader = ui.GetNode<RichTextLabel>(ReaderPath);
            var status = ui.GetNode<Label>(StatusPath);
            var knowledgeBefore = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();

            // A real page visit first, so the history carrier has something to find.
            ui.NavigateBrowser("yalkyn");
            await Frames(4);

            query.Text = "Марат";
            everywhere.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(6);
            Check(ui.GlobalSearchActive && status.Text.StartsWith("Искать везде · найдено:", StringComparison.Ordinal),
                "the archive button runs the global search: " + status.Text);
            var labels = Enumerable.Range(0, results.ItemCount).Select(index => results.GetItemText(index)).ToArray();
            Check(labels.Any(label => label.StartsWith("Документ: ", StringComparison.Ordinal)),
                "readable documents take part: " + string.Join(" | ", labels));
            Check(labels.All(label => !label.Contains(LockedRegisterTitle, StringComparison.Ordinal)),
                "a locked record never shows its title");
            var lockedRow = Enumerable.Range(0, results.ItemCount).FirstOrDefault(
                index => results.GetItemText(index) == "🔒 Закрытые записи", -1);
            Check(lockedRow >= 0 && !results.IsItemSelectable(lockedRow),
                "a matching locked record raises the same non-selectable line the archive uses");

            // A chat result comes from the delivered history, never from lines the
            // player has not received yet.
            query.Text = "Наилю";
            everywhere.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(6);
            var chatRow = Enumerable.Range(0, results.ItemCount).FirstOrDefault(
                index => results.GetItemText(index).StartsWith("Чат: ", StringComparison.Ordinal), -1);
            Check(chatRow >= 0 && ui.ChatThreads.Any(thread => thread.Id == "self" && thread.Messages.Any(
                    message => message.Text.Contains("Наилю", StringComparison.Ordinal))),
                "the delivered chat history takes part: " + string.Join(" | ",
                    Enumerable.Range(0, results.ItemCount).Select(results.GetItemText)));
            results.EmitSignal(ItemList.SignalName.ItemSelected, chatRow);
            await Frames(6);
            Check(ui.ChatThreadId == "urman.oldpc:chat/self", "a chat result opens its thread: " + ui.ChatThreadId);

            // Aidar's own notes.
            ui.NewPersonalFile(false);
            await Frames(4);
            var editor = ui.GetNode<TextEdit>("Screen/App_notepad/Layout/Content/Text");
            editor.Text = "Проверить валенки и дрова до вечера.";
            ui.GetNode<Button>("Screen/App_notepad/Layout/Content/Tools/Save").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(4);
            query.Text = "валенки";
            everywhere.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(6);
            var noteRow = Enumerable.Range(0, results.ItemCount).FirstOrDefault(
                index => results.GetItemText(index).StartsWith("Заметка: ", StringComparison.Ordinal), -1);
            Check(noteRow >= 0, "personal notes take part: " + string.Join(" | ",
                Enumerable.Range(0, results.ItemCount).Select(results.GetItemText)));
            results.EmitSignal(ItemList.SignalName.ItemSelected, noteRow);
            await Frames(6);
            Check(ui.GetNode<TextEdit>("Screen/App_notepad/Layout/Content/Text").Text.Contains("валенки", StringComparison.Ordinal),
                "a note result opens it in the ordinary editor");

            // The visited page is reachable by its readable title.
            query.Text = "Ялкын";
            everywhere.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(6);
            Check(Enumerable.Range(0, results.ItemCount).Any(index =>
                    results.GetItemText(index) == "Интернет: Ялкын · соцсеть"),
                "the visited page takes part by its readable title: " + string.Join(" | ",
                    Enumerable.Range(0, results.ItemCount).Select(results.GetItemText)));

            // Zero results offer terms, and a term runs the same query path.
            query.Text = "ццц";
            everywhere.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(6);
            Check(reader.GetParsedText().Contains("Переформулируйте запрос", StringComparison.Ordinal)
                && reader.GetParsedText().Contains("попробуйте", StringComparison.Ordinal),
                "an empty search asks the player to rephrase and offers terms: " + reader.GetParsedText());
            reader.EmitSignal(RichTextLabel.SignalName.MetaClicked, Variant.From("term:Марат"));
            await Frames(6);
            Check(ui.GlobalQuery == "Марат" && results.ItemCount > 0,
                "a term link re-runs the query through the same path: " + ui.GlobalQuery);

            Check(bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == knowledgeBefore,
                "the search itself grants no knowledge");
            exit = 0;
            GD.Print($"oldpc-search-smoke: PASS {_checks.Count} checks; no human duration or art claim");
        }
        catch (Exception error)
        {
            GD.PrintErr($"oldpc-search-smoke: FAIL {error}");
        }
        finally
        {
            if (main is not null) await GodotSmokeCleanup.ReleaseAsync(main);
            GetTree().Quit(exit);
        }
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private void Check(bool passed, string name)
    {
        if (!passed) throw new InvalidOperationException(name);
        _checks.Add(name);
        GD.Print($"oldpc-search: check {name}");
    }
}
