using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>Protected native capture of the exploration UI requested by the author.</summary>
public partial class ExplorationUiCapture : Node
{
    private readonly List<string> _checks = [];
    private string _output = string.Empty;

    public override async void _Ready()
    {
        try { await RunAsync(); }
        catch (Exception error)
        {
            GD.PushError("exploration-ui-capture: FAIL " + error);
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        Require(OS.GetEnvironment("URMAN_PROTECTED_RUN") == "1", "protected-userdata");
        _output = OS.GetEnvironment("URMAN_UI_SHOT_DIR");
        if (string.IsNullOrWhiteSpace(_output))
        {
            var source = System.IO.Directory.GetParent(ProjectSettings.GlobalizePath("res://").TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))!;
            Require(source.Name == "source", "station-isolated-source");
            _output = System.IO.Path.Combine(source.Parent!.FullName, "artifacts", "frames");
        }
        System.IO.Directory.CreateDirectory(_output);
        DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
        DisplayServer.WindowMoveToForeground();
        var loading = LoadingScreenUi.Show(this, "Кара-Урман", "Подготавливаем деревню и первую встречу");
        await Frames(3);
        Capture("01_loading");
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")!.Instantiate<Main>();
        main.EnableAct1ConnectedWorld = true;
        main.EnableZoneTransitionFade = true;
        AddChild(main);
        await Frames(3);
        loading.Hide();
        var bridge = main.GetNode<RuntimeBridge>("RuntimeBridge");
        var player = main.GetNode<FirstPersonController>("Player");
        var journal = main.GetNode<JournalUi>("JournalUi");
        Require(main.ConnectedWorld?.IsBuilt == true, "connected-world-ready");
        Require(await bridge.StartDebugSessionAsync(), "isolated-debug-session");
        player.SetPhysicsProcess(false);
        DisplayServer.WindowMoveToForeground();
        var registry = bridge.NotebookSettlement!;
        var address = registry.Addresses["ADR-BABAI"];
        var access = registry.AccessPoints[address.AccessId].Position;
        player.ApplyZoneSpawn(new Vector3((float)access.X, (float)access.Y + .05f, (float)access.Z), 180);
        await Frames(3);
        var feedback = bridge.GetNode<ExplorationFeedbackUi>("ExplorationFeedback");
        var title = feedback.GetChild<Control>(0).GetChildren().OfType<VBoxContainer>().Single();
        await WaitFor(() => title.Visible && title.Modulate.A > .95f, "location-title-visible");
        Require(title.GetChildren().OfType<Label>().Any(label => label.Text == "Дом бабая и әби"), "babai-title-from-entry-proximity");
        Capture("02_babai_location");
        await WaitFor(() => bridge.ExploredMapCells().Count > 0, "first-explored-cell-committed");
        var firstCells = bridge.ExploredMapCells().Count;
        journal._UnhandledInput(Action("inventory"));
        await Frames(3);
        var tabs = journal.GetNode<TabBar>("Screen/Book/Layout/Tabs");
        Require(tabs.CurrentTab == 6 && journal.GetNode<Control>("Screen/Book/Layout/Inventory").IsVisibleInTree(), "inventory-shortcut");
        Capture("03_inventory");
        journal._UnhandledInput(Action("map"));
        await Frames(3);
        Require(tabs.CurrentTab == 3 && journal.GetNode<Control>("Screen/Book/Layout/VillageMap").IsVisibleInTree(), "map-shortcut");
        Capture("04_map_first_visit");
        journal._UnhandledInput(Action("map"));
        await Frames(2);
        Require(!player.ModalOpen, "map-shortcut-closes-modal");
        var second = registry.AccessPoints[registry.Addresses["ADR-FAP"].AccessId].Position;
        player.ApplyZoneSpawn(new Vector3((float)second.X, (float)second.Y + .05f, (float)second.Z), 180);
        await WaitFor(() => bridge.ExploredMapCells().Count > firstCells, "map-expands-at-new-position");
        journal._UnhandledInput(Action("map"));
        await Frames(3);
        Capture("05_map_second_visit");
        journal._UnhandledInput(Action("map"));
        await Frames(2);
        Require(await bridge.SaveSlotAsync("exploration-ui-capture"), "real-save-written");
        await Frames(2);
        Require(feedback.FindChildren("*", nameof(Label), true, false).OfType<Label>()
            .Any(label => label.IsVisibleInTree() && label.Text == "Сохранено"), "save-success-visible");
        Capture("06_saved");
        var savedCells = bridge.ExploredMapCells().ToArray();
        var load = bridge.LoadSlotAsync("exploration-ui-capture");
        Require(bridge.GetChildren().OfType<LoadingScreenUi>().Any(), "load-overlay-created");
        Capture("07_save_loading");
        Require(await load, "real-save-restored");
        Require(bridge.ExploredMapCells().SequenceEqual(savedCells), "explored-map-survives-load");
        await Frames(3);
        Require(!player.ModalOpen, "load-restores-player-control");
        journal._UnhandledInput(Action("map"));
        await Frames(3);
        Capture("08_map_after_load");
        System.IO.File.WriteAllText(System.IO.Path.Combine(_output, "exploration-ui-checks.json"),
            JsonSerializer.Serialize(new { checks = _checks, savedCells, renderer = RenderingServer.GetRenderingDevice()?.GetDeviceName(), resolution = GetViewport().GetVisibleRect().Size },
                new JsonSerializerOptions { WriteIndented = true }));
        journal._UnhandledInput(Action("map"));
        await GodotSmokeCleanup.ReleaseAsync(main);
        GD.Print("exploration-ui-capture: PASS " + _checks.Count + " checks");
        GetTree().Quit(0);
    }

    private static InputEventAction Action(string action) => new() { Action = action, Pressed = true };

    private void Capture(string name)
    {
        RenderingServer.ForceDraw(false);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image is not null && !image.IsEmpty() && image.SavePng(System.IO.Path.Combine(_output, name + ".png")) == Error.Ok,
            "capture-" + name);
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task WaitFor(Func<bool> condition, string check)
    {
        for (var i = 0; i < 900 && !condition(); i++) await Frames(1);
        Require(condition(), check);
    }

    private void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
        _checks.Add(check);
    }
}
