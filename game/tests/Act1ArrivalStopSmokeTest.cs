using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>Ordinary arrival and manual timetable reading in the existing world.
/// Physical input covers the approach and both sides; this is not a human playtime test.</summary>
public partial class Act1ArrivalStopSmokeTest : Node
{
    private const string DocumentId = "urman.chapter1:document/arrival-stop-timetable";
    private Act1DemoRoot _demo = null!;
    private RuntimeBridge _bridge = null!;
    private Act1ConnectedWorld _world = null!;
    private FirstPersonController _player = null!;
    private Camera3D _camera = null!;
    private DocumentUi _documents = null!;
    private InteractionTarget _target = null!;
    private Node3D _stop = null!;
    private readonly List<object> _events = new();
    private JsonNode? _initialStory;
    private string _initialScene = string.Empty;
    private int _checks;
    private float _walked;
    private string Output => System.Environment.GetEnvironmentVariable("URMAN_ARRIVAL_OUTPUT")
        ?? throw new InvalidOperationException("Set URMAN_ARRIVAL_OUTPUT to a new absolute evidence folder.");

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            Require(Path.IsPathFullyQualified(Output), "evidence output is absolute");
            Require(!File.Exists(Path.Combine(Output, "arrival-stop-receipt.json")), "receipt path is unused");
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(10);
            Require(await this.StartThroughMainMenuAsync(_demo), "ordinary New Game through the actual menu");
            Require(_demo.IntroVisible, "ordinary arrival intro is presented");
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(8);
            RefreshOwners();
            Require(!_demo.IntroVisible && !_player.ModalOpen && _bridge.CurrentZoneId == "village_day",
                "intro releases the ordinary village player");
            Require(Horizontal(_player.GlobalPosition, AgentBAct1Layout.ArrivalSpawn) < .65f,
                "new game starts at the authored roadside stop");
            var road = _player.GlobalPosition;
            _initialStory = Story();
            _initialScene = _bridge.ActiveSceneId ?? throw new InvalidOperationException("Ordinary arrival has no active scene.");
            Require(!Opened() && Entries() == 0 && !_documents.IsOpen, "timetable starts unread and unrecorded");
            CheckContent();
            await Aim(_stop.GetNode<Label3D>("SettlementName").GlobalPosition);
            await Capture("01_ordinary_road");

            var front = At(0, 2.1f);
            await WalkTo(front, "road to timetable");
            await Aim(_target.GlobalPosition);
            Require(RayHitsTarget() && _target.IsAvailable(), "front camera ray reaches the manual timetable target");
            await Frames(20);
            CheckUnopened("standing near the readable timetable does not read it");
            await Capture("02_front_before_read");

            // Go around the real post on its west side, away from the existing bench.
            await WalkTo(At(-1.15f, 2.1f), "front to west side");
            await WalkTo(At(-1.15f, -1.45f), "walk around the actual post");
            await WalkTo(At(0, -1.45f), "reach the back of the case");
            await Aim(_target.GlobalPosition);
            var printed = _stop.GetNode<Label3D>("PrintedTimetable");
            Require(!printed.DoubleSided && !(_stop.GetNode<Label3D>("SettlementName").DoubleSided)
                && printed.GlobalBasis.Z.Dot(_camera.GlobalPosition - printed.GlobalPosition) < 0,
                "printed text faces the road and has no mirrored reverse face");
            var backHit = AimHit();
            Require(backHit is Node blocked && _stop.IsAncestorOf(blocked) && !RayHitsTarget(),
                "the real case or post occludes the timetable interaction from behind");
            await PressE();
            CheckUnopened("E against the opaque back cannot open the front paper");
            await Capture("03_opaque_back");

            await WalkTo(At(-1.15f, -1.45f), "leave the back");
            await WalkTo(At(-1.15f, 2.1f), "return around the post");
            await WalkTo(front, "return to the printed face");
            await ReadTimetable();
            Require(Entries() == 0, "opening a physical timetable does not automatically record it");
            await Capture("04_manual_reader");
            await RecordTimetable();
            await CloseReader();
            Require(Entries() == 1, "explicit journal action records one timetable");
            CheckStory("reading and recording timetable gives no story progress");

            await WalkTo(road, "walk back to the ordinary road before saving");
            Require(await _bridge.SaveSlotAsync("arrival-stop-manual-read"), "save the real read document and roadside pose");
            await WalkTo(front, "leave the saved roadside position");
            Require(await _bridge.LoadSlotAsync("arrival-stop-manual-read"), "load through the existing save owner");
            await Frames(10);
            RefreshOwners();
            Require(Horizontal(_player.GlobalPosition, road) < .18f && _player.IsOnFloor(),
                "load restores the actually saved roadside position with support");
            Require(Opened() && Entries() == 1 && !_documents.IsOpen && !_player.ModalOpen,
                "load preserves one read source and releases the reader");
            CheckContent();
            CheckStory("load does not grant arrival or investigation knowledge");
            await WalkTo(front, "walk back to the same timetable after load");
            await ReadTimetable();
            await RecordTimetable();
            Require(Entries() == 1, "recording a revisited timetable never duplicates it");
            await Capture("05_reader_after_load");
            await CloseReader();
            CheckStory("the repeat visit preserves story conditions");
            exit = 0;
        }
        catch (Exception error)
        {
            GD.PrintErr("act1-arrival-stop: " + error);
            _events.Add(new { kind = "failure", error = error.ToString(),
                feet = _player?.GlobalPosition.ToString(), aim = _camera is null ? null : DescribeAim() });
            if (_camera is not null)
                try { await Capture("failure"); } catch (Exception capture) { _events.Add(new { kind = "capture-failure", error = capture.Message }); }
        }
        finally
        {
            Release();
            try
            {
                Directory.CreateDirectory(Output);
                using var file = new FileStream(Path.Combine(Output, "arrival-stop-receipt.json"),
                    FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(file, new
                {
                    passed = exit == 0, checks = _checks, measuredWalkingMetres = _walked,
                    fixture = "existing act1_demo; actual menu and intro; no setup teleport; manual camera ray and physical E; ordinary save/load",
                    humanPlaytime = "external/not-run", visualReadability = "requires inspection of captured images",
                    buildModuleId = typeof(Act1ArrivalStopSmokeTest).Assembly.ManifestModule.ModuleVersionId,
                    events = _events
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception error) { exit = 1; GD.PrintErr("Cannot preserve arrival receipt: " + error); }
            if (_demo is not null) await GodotSmokeCleanup.ReleaseAsync(_demo);
        }
        GetTree().Quit(exit);
    }

    private void RefreshOwners()
    {
        _bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge ?? throw new InvalidOperationException("Missing runtime.");
        _world = _demo.DemoMain.ConnectedWorld ?? throw new InvalidOperationException("Missing existing world.");
        _player = _demo.DemoMain.GetNode<FirstPersonController>("Player");
        _camera = _player.GetNode<Camera3D>("Head/Camera3D");
        _documents = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi ?? throw new InvalidOperationException("Missing document reader.");
        _stop = _world.GetNode<Node3D>("Act1CoreWorldGreybox/Arrival/ArrivalBusStop");
        _target = _world.FindChild("ArrivalStopTimetable", true, false) as InteractionTarget
            ?? throw new InvalidOperationException("Missing authored arrival timetable target.");
        Require(_target.InteractionId == "urman.chapter1:interaction/arrival-stop-timetable",
            "the authored timetable target retains its stable interaction identity");
    }

    private void CheckContent()
    {
        var document = _bridge.RequireDocument(DocumentId);
        var name = _stop.GetNode<Label3D>("SettlementName");
        var printed = _stop.GetNode<Label3D>("PrintedTimetable");
        var expected = document.BodyMarkdown.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("#", string.Empty, StringComparison.Ordinal).Trim();
        Require(name.Text == SettlementRegistry.VillageName && expected.Contains(SettlementRegistry.VillageName, StringComparison.Ordinal),
            "the stop name and authored timetable agree with the current village registry");
        Require(printed.Text == expected && printed.GetMeta("contentOwner").AsString() == DocumentId
            && _target.DocumentId == DocumentId && _stop.GetMeta("documentId").AsString() == DocumentId,
            "physical paper, interaction and reader have one authored document owner");
        Require(!printed.Text.Contains("{address:", StringComparison.Ordinal),
            "the physical paper contains no unresolved address token");
    }

    private async Task ReadTimetable()
    {
        Release();
        await Aim(_target.GlobalPosition);
        Require(_target.IsAvailable() && RayHitsTarget(), "the current forward camera ray targets the visible paper");
        await PressE();
        for (var frame = 0; frame < 240 && !_documents.IsOpen; frame++) await Frames(1);
        Require(_documents.IsOpen && _documents.OpenDocumentId == DocumentId && _player.ModalOpen,
            "physical E opens exactly the aimed timetable through the existing interaction");
        var body = _documents.GetNode<RichTextLabel>("Screen/Document/Layout/Reader/Body");
        Require(body.IsVisibleInTree() && body.Text == SourceExcerptSelection.FormatSourceText(_bridge.RequireDocument(DocumentId).BodyMarkdown),
            "the actual reader displays the same authoritative timetable and announcements");
        Require(OpenedCount() == 1, "explicit reading records one document history entry");
        CheckStory("manual read does not advance the investigation");
    }

    private async Task RecordTimetable()
    {
        var save = _documents.GetNode<Button>("Screen/Document/Layout/Footer/Save");
        Require(!save.Disabled, "the current reader offers explicit recording");
        save.EmitSignal(Button.SignalName.Pressed);
        for (var frame = 0; frame < 240
            && !_documents.StatusText.StartsWith("Документ добавлен в журнал", StringComparison.Ordinal); frame++) await Frames(1);
        Require(Entries() == 1 && save.Disabled
            && _documents.StatusText.StartsWith("Документ добавлен в журнал", StringComparison.Ordinal),
            "the explicit journal transaction completes once, including on a repeat visit");
    }

    private async Task CloseReader()
    {
        _documents.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(6);
        Require(!_documents.IsOpen && !_player.ModalOpen, "closing the paper returns player input");
    }

    private bool Opened() => OpenedCount() > 0;
    private int OpenedCount()
    {
        var state = _bridge.SelectRuntimeState();
        return state.TryGetProperty("presentation", out var presentation)
            && presentation.TryGetProperty("openedDocumentIds", out var opened)
            ? opened.EnumerateArray().Count(id => id.GetString() == DocumentId) : 0;
    }
    private int Entries() => _bridge.JournalEntries().Count(entry => entry.EntryId == DocumentId);
    private JsonNode Story()
    {
        var state = _bridge.SelectRuntimeState();
        var result = new JsonObject();
        foreach (var key in new[] { "knowledge", "beats", "quests", "vocabulary" })
            if (state.TryGetProperty(key, out var value)) result[key] = JsonNode.Parse(value.GetRawText());
        return result;
    }
    private void CheckStory(string label) => Require(_bridge.ActiveSceneId == _initialScene
        && JsonNode.DeepEquals(_initialStory, Story()), label);
    private void CheckUnopened(string label)
    {
        Require(!Opened() && Entries() == 0 && !_documents.IsOpen && !_player.ModalOpen, label);
        CheckStory(label + ": no knowledge or scene change");
    }

    private Vector3 At(float x, float z) => _stop.ToGlobal(new Vector3(x, 0, z));
    private static float Horizontal(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
    private async Task WalkTo(Vector3 goal, string label)
    {
        var from = _player.GlobalPosition;
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        try
        {
            for (var frame = 0; frame < 240 && Horizontal(_player.GlobalPosition, goal) > .09f; frame++)
            {
                var delta = goal - _player.GlobalPosition;
                _player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
                Input.ActionPress("move_forward", Horizontal(_player.GlobalPosition, goal) < .3f ? .35f : 1);
                var previous = _player.GlobalPosition;
                await Frames(1);
                _walked += Horizontal(previous, _player.GlobalPosition);
            }
        }
        finally { Release(); }
        await Frames(4);
        _events.Add(new { kind = "walk", label, from = from.ToString(), goal = goal.ToString(), actual = _player.GlobalPosition.ToString() });
        Require(Horizontal(_player.GlobalPosition, goal) < .17f && _player.IsOnFloor() && _player.CanStandAt(_player.GlobalPosition),
            label + ": actual supported capsule reaches the destination");
        Require(_player.PresentationTransformRevision == revision && _player.FallRecoveries == recoveries,
            label + ": no teleport or fall recovery");
    }

    private async Task Aim(Vector3 point)
    {
        // Head.Z is offset from the character origin; each rotation moves the camera.
        for (var iteration = 0; iteration < 6; iteration++)
        {
            var delta = point - _camera.GlobalPosition;
            _player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length())),
                Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
        }
        await Frames(4);
    }
    private GodotObject? AimHit()
    {
        var ray = _player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
        ray.ForceRaycastUpdate();
        using var query = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition,
            _camera.GlobalPosition - _camera.GlobalBasis.Z * ray.TargetPosition.Length(), ray.CollisionMask,
            new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        var actual = ray.GetCollider();
        var calculated = hit.Count == 0 ? null : hit["collider"].AsGodotObject();
        Require(actual == calculated, "production interaction ray agrees with the current -camera.Basis.Z probe");
        return actual;
    }
    private bool RayHitsTarget() => AimHit() == _target;
    private string DescribeAim()
    {
        var ray = _player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
        ray.ForceRaycastUpdate();
        return (ray.GetCollider() as Node)?.GetPath().ToString() ?? "no hit";
    }
    private async Task PressE()
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = false });
        await Frames(5);
    }
    private async Task Capture(string name)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_ARRIVAL_CAPTURE") != "1") return;
        Directory.CreateDirectory(Output);
        var path = Path.Combine(Output, name + ".png");
        Require(!File.Exists(path), "capture path is unused: " + name);
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "arrival/" + name);
        using var image = GetViewport().GetTexture().GetImage();
        Require(!image.IsEmpty() && image.SavePng(path) == Error.Ok, "current gameplay viewport captured: " + name);
        _events.Add(new { kind = "capture", path, feet = _player.GlobalPosition.ToString(),
            camera = _camera.GlobalTransform.ToString(), focused = DisplayServer.WindowIsFocused() });
    }
    private static void Release()
    {
        foreach (var action in new[] { "move_forward", "move_backward", "move_left", "move_right", "sprint", "jump", "interact" })
            Input.ActionRelease(action);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = false });
    }
    private async Task Frames(int count) { for (var frame = 0; frame < count; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        _checks++;
        GD.Print("act1-arrival-stop: " + label);
        _events.Add(new { kind = "check", label, passed = true });
    }
}
