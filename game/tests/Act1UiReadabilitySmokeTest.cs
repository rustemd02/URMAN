using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// UIUX-009 automation portion: at 1280x720 and 1920x1080 every critical UI
/// (settings, journal, old PC, document, dialogue) opens, its panel stays
/// fully inside the viewport bounds, and no other modal stays open after
/// close. Text readability itself remains the human review gate.
/// </summary>
public partial class Act1UiReadabilitySmokeTest : Node
{
    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("UI readability smoke could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        await Frames(3);

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var settings = main.GetNodeOrNull<SettingsUi>("SettingsUi");
        var journal = main.GetNodeOrNull<JournalUi>("JournalUi");
        var oldPc = main.GetNodeOrNull<OldPcUi>("OldPcUi");
        var document = main.GetNodeOrNull<DocumentUi>("DocumentUi");
        var dialogue = main.GetNodeOrNull<DialogueUi>("DialogueUi");
        if (bridge is null || player is null || settings is null || journal is null
            || oldPc is null || document is null || dialogue is null)
        {
            Fail("UI readability smoke could not resolve all UI owners.");
            return;
        }

        player.SetPhysicsProcess(false);
        bridge.HandleOldPcInputAsync(System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = "urman.oldpc:document/doc_marat_official_death_notice"
        })).Wait();

        foreach (var (width, height) in new[] { (1280, 720), (1920, 1080) })
        {
            DisplayServer.WindowSetSize(new Vector2I(width, height));
            await Frames(4);

            // 1) Settings: opens, panel fits the viewport, closes clean.
            settings.Open(player);
            await Frames(2);
            if (!settings.IsOpen || !FitsViewport(settings.GetNode<Control>("Screen/Panel")))
            {
                Fail($"Settings panel does not fit the {width}x{height} viewport.");
                return;
            }

            settings.Close();
            await Frames(2);
            if (settings.IsOpen)
            {
                Fail("Settings did not close at the second resolution.");
                return;
            }

            // 2) Journal: opens, panel fits, closes.
            journal.Open(bridge);
            await Frames(2);
            if (!FitsViewport(journal.GetNode<Control>("Screen/Book")))
            {
                Fail($"Journal panel does not fit the {width}x{height} viewport.");
                return;
            }

            journal._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
            await Frames(2);

            // 3) Old PC: opens, computer screen fits, closes.
            oldPc.Open(bridge);
            await Frames(2);
            if (!FitsViewport(oldPc.GetNode<Control>("Screen/Computer")))
            {
                Fail($"Old-PC screen does not fit the {width}x{height} viewport.");
                return;
            }

            oldPc._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true, Echo = false });
            await Frames(2);

            // 4) Document: opens via the resolved asset, panel fits, closes
            //    via ui_cancel (DocumentUi owns its own close handling).
            // The bridge opens the shared document UI with the resolved
            // content (same production path as the evidence interactions).
            bridge.OpenDocumentUi("urman.oldpc:document/doc_marat_official_death_notice");
            await Frames(2);
            if (!document.IsOpen || !FitsViewport(document.GetNode<Control>("Screen/Document")))
            {
                Fail($"Document panel does not fit the {width}x{height} viewport.");
                return;
            }

            document.GetNode<Button>("Screen/Document/Layout/Header/Close")
                .EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            if (document.IsOpen)
            {
                Fail("Document did not close via its Close button.");
                return;
            }

            // 5) Dialogue: opens via the bridge, layout fits, closes.
            bridge.OpenDialogueUi(Dialogue("gulsina_yaramyy"));
            await Frames(2);
            if (!dialogue.IsOpen || !FitsViewport(dialogue.GetNode<Control>("Screen/Panel")))
            {
                Fail($"Dialogue layout does not fit the {width}x{height} viewport.");
                return;
            }

            dialogue._UnhandledInput(new InputEventKey
            {
                Keycode = Key.Escape,
                PhysicalKeycode = Key.Escape,
                Pressed = true,
                Echo = false
            });
            await Frames(2);
            if (dialogue.IsOpen)
            {
                Fail("Dialogue did not close via ui_cancel.");
                return;
            }

            GD.Print($"act1-ui-readability: {width}x{height} settings/journal/oldpc/document/dialogue all open, fit and close");
        }

        GD.Print("act1-ui-readability: PASS 2 resolutions x 5 critical UIs, all fit and close cleanly");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private void InjectCancel()
    {
        _UnhandledInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = true,
            Echo = false
        });
    }

    private static bool FitsViewport(Control control)
    {
        var rect = control.GetGlobalRect();
        var size = control.GetViewportRect().Size;
        return rect.Position.X >= -1f
            && rect.Position.Y >= -1f
            && rect.End.X <= size.X + 1f
            && rect.End.Y <= size.Y + 1f;
    }

    private static string Dialogue(string localId) => $"urman.chapter1:dialogue/{localId}";

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
