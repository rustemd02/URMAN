using Godot;
using System.Text.Json;
using Urman.Core.Capabilities.OldPc;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// Focused proof of the authored chat: threads arrive by ordinary conditions,
/// answers advance the script, free text earns only the authored stub, and
/// nothing in the chat writes knowledge or a journal entry.
/// </summary>
public partial class OldPcChatSmokeTest : Node
{
    private const string Slot = "oldpc-chat-proof";
    private const string LogPath = "Screen/App_chat/Layout/Content/Columns/Conversation/Log";
    private const string ChoicesPath = "Screen/App_chat/Layout/Content/Columns/Conversation/Choices";
    private const string InputPath = "Screen/App_chat/Layout/Content/Columns/Conversation/Input/Text";
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
            ui.LaunchApplication("chat");
            await Frames(4);

            Check(ui.VisibleChatIds.SequenceEqual(["urman.oldpc:chat/alsu", "urman.oldpc:chat/mansur", "urman.oldpc:chat/self"]),
                "the rinat thread stays closed until the story alerts him: " + string.Join(", ", ui.VisibleChatIds));
            Check(ui.ChatThreadId == "urman.oldpc:chat/alsu", "the first visible thread opens by itself");
            var log = ui.GetNode<RichTextLabel>(LogPath);
            Check(log.GetParsedText().Contains("исәнме", StringComparison.Ordinal),
                "the greeting arrives as the first message: " + log.GetParsedText().Split('\n')[0]);
            var choices = ui.GetNode<VBoxContainer>(ChoicesPath);
            var offered = choices.GetChildren().OfType<Button>().Select(button => button.Name.ToString()).ToArray();
            Check(offered.Length == 2 && offered.Contains("Choice_village") && offered.Contains("Choice_marat"),
                "two authored answers are offered: " + string.Join(", ", offered));

            var knowledgeBefore = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
            var journalBefore = bridge.JournalEntries().Count;
            var messagesBefore = Thread(ui, "alsu").Messages.Count;

            Press(choices, "Choice_village");
            await Frames(4);
            var thread = Thread(ui, "alsu");
            Check(thread.Messages.Count == messagesBefore + 2
                && thread.Messages[^1].From == "npc"
                && thread.Messages[^2].From == "player"
                && log.GetParsedText().Contains("школа закрыта", StringComparison.Ordinal),
                "an authored answer adds the player line and the next scripted reply");
            Check(thread.NodeId == "village", "the script waits on the answered node's choices: " + thread.NodeId);
            Check(ui.GetNode<VBoxContainer>(ChoicesPath).GetChildren().OfType<Button>()
                    .Select(button => button.Name.ToString()).Order(StringComparer.Ordinal)
                    .SequenceEqual(["Choice_enough", "Choice_school"]),
                "the next node offers its own answers");

            var input = ui.GetNode<LineEdit>(InputPath);
            input.Text = "Салам!";
            input.EmitSignal(LineEdit.SignalName.TextSubmitted, "Салам!");
            await Frames(4);
            thread = Thread(ui, "alsu");
            Check(thread.Messages.Count == messagesBefore + 4
                && thread.Messages[^2].From == "player" && thread.Messages[^2].Text == "Салам!"
                && thread.Messages[^1].From == "npc"
                && thread.Messages[^1].Text.Contains("не поняла", StringComparison.Ordinal),
                "free text earns the authored stub answer");
            Check(thread.NodeId == "village", "free text never moves the script: " + thread.NodeId);

            Check(bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == knowledgeBefore
                && bridge.JournalEntries().Count == journalBefore,
                "the chat grants no knowledge and writes no journal entry");

            Check(await bridge.SaveSlotAsync(Slot), "the chat saves inside the existing desktop snapshot");
            Check(await bridge.LoadSlotAsync(Slot), "the chat loads back");
            await Frames(6);
            var restored = Thread(ui, "alsu");
            Check(restored.Messages.Count == messagesBefore + 4 && restored.NodeId == "village",
                "the delivered log and the script position survive the load");
            // The opened thread is read; every other visible thread received its
            // greeting while the player looked elsewhere, so it stays unread.
            Check(ui.VisibleChatIds.Count == 3 && ui.ChatUnreadCount == ui.VisibleChatIds.Count - 1,
                $"the loaded desktop keeps the same visible threads; opened=read, the other two keep their honest unread marks: {ui.ChatUnreadCount}");
            exit = 0;
            GD.Print($"oldpc-chat-smoke: PASS {_checks.Count} checks; no human duration or art claim");
        }
        catch (Exception error)
        {
            GD.PrintErr($"oldpc-chat-smoke: FAIL {error}");
        }
        finally
        {
            if (main is not null) await GodotSmokeCleanup.ReleaseAsync(main);
            GetTree().Quit(exit);
        }
    }

    private static OldPcChatThreadSnapshot Thread(OldPcUi ui, string id) =>
        ui.ChatThreads.Single(thread => thread.Id == id);

    private static void Press(Node parent, string name)
    {
        var button = parent.GetNodeOrNull<Button>(name)
            ?? throw new InvalidOperationException($"Missing chat answer {name}.");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private void Check(bool passed, string name)
    {
        if (!passed) throw new InvalidOperationException(name);
        _checks.Add(name);
        GD.Print($"oldpc-chat: check {name}");
    }
}
