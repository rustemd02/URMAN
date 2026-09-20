using Godot;
using System.Text.Json;
using Urman.Core.Capabilities.OldPc;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// Focused proof of the tetris easter egg (§7): the window opens, the keys
/// reach the game, a pause really stops the fall, the well can be lost, and the
/// record is the only thing that leaves the window - through the ordinary
/// desktop snapshot, with no knowledge, journal or search side effect.
/// </summary>
public partial class OldPcTetrisSmokeTest : Node
{
    private const string Slot = "oldpc-tetris-proof";
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

            // A record that already exists in the desktop state, so the proof can
            // check the persistence without pretending the game scored.
            var seeded = new OldPcDesktopSnapshot { TetrisHigh = 500 };
            Check(await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
            {
                type = "desktop",
                desktop = JsonSerializer.SerializeToElement(seeded, OldPcDesktopSnapshot.JsonOptions)
            })).ContinueWith(task => task.Result.GetProperty("desktop").GetProperty("tetrisHigh").GetInt32() == 500),
                "the desktop state carries the stored record");
            ui.Open(bridge);
            ui.ApplyAccessibilitySettings(AccessibilitySettingsSnapshot.Default);
            await Frames(4);

            var knowledgeBefore = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
            var journalBefore = bridge.JournalEntries().Count;
            var notesBefore = ui.PersonalFiles.Count;

            ui.LaunchApplication("tetris");
            await Frames(4);
            var tetris = ui.Tetris;
            Check(tetris.Visible && !tetris.Paused && !tetris.GameOver, "the tetris window opens a fresh game");
            Check(tetris.Record == 500 && tetris.Score == 0 && tetris.Level == 1,
                $"the game starts from the stored record and an empty score: record={tetris.Record}");

            tetris.GrabFocus();
            await Frames(2);
            tetris._Input(new InputEventKey { Pressed = true, Keycode = Key.Left });
            tetris._Input(new InputEventKey { Pressed = true, Keycode = Key.Space });
            await Frames(3);
            Check(tetris.LandedPieces == 1 && tetris.FilledCells == 4,
                $"the keys reach the game and a hard drop lands one piece: pieces={tetris.LandedPieces} cells={tetris.FilledCells}");

            tetris.SetRecord(500);
            tetris._Input(new InputEventKey { Pressed = true, Keycode = Key.P });
            await Frames(2);
            var pausedPieces = tetris.LandedPieces;
            await Frames(180);
            Check(tetris.Paused && tetris.LandedPieces == pausedPieces,
                $"a pause stops the fall: pieces={tetris.LandedPieces} paused={tetris.Paused}");
            tetris._Input(new InputEventKey { Pressed = true, Keycode = Key.P });
            await Frames(2);
            Check(!tetris.Paused, "the same key resumes the game");

            // Losing the well is reachable by ordinary play, and it changes no
            // knowledge, no journal entry and no personal note.
            for (var drop = 0; drop < 80 && !tetris.GameOver; drop++)
            {
                tetris._Input(new InputEventKey { Pressed = true, Keycode = Key.Space });
                await Frames(1);
            }
            Check(tetris.GameOver && tetris.FilledCells >= 40,
                $"ordinary drops lose the well: pieces={tetris.LandedPieces} cells={tetris.FilledCells}");
            Check(bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == knowledgeBefore
                && bridge.JournalEntries().Count == journalBefore && ui.PersonalFiles.Count == notesBefore,
                "the game touches no knowledge, journal entry or personal note");

            // The one persistent field is wired to the snapshot: what the game
            // reports as its record is what the desktop stores and reloads.
            tetris.RecordChanged!.Invoke(777);
            // The desktop flushes on a short timer (and on leaving the computer);
            // wait for that real time before saving the slot.
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            await Frames(2);
            Check(await bridge.SaveSlotAsync(Slot), "the desktop with the record saves");
            Check(await bridge.LoadSlotAsync(Slot), "the desktop with the record loads");
            // The desktop state is applied when the player opens the computer, so
            // the proof reopens it exactly like the ordinary flow does.
            ui.Open(bridge);
            await Frames(6);
            Check(ui.Tetris.Record == 777,
                $"the record is the only thing that leaves the window: {ui.Tetris.Record}");
            exit = 0;
            GD.Print($"oldpc-tetris-smoke: PASS {_checks.Count} checks; no human duration or art claim");
        }
        catch (Exception error)
        {
            GD.PrintErr($"oldpc-tetris-smoke: FAIL {error}");
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
        GD.Print($"oldpc-tetris: check {name}");
    }
}
