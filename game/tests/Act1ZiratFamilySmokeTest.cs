using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// A declared local fixture on the existing zirat path, with ordinary runtime,
/// physical support, aim and manual input. It does not measure a human playthrough.
/// </summary>
public partial class Act1ZiratFamilySmokeTest : Node
{
    private const string Document = "urman.chapter1:document/zirat-family-links";
    private const string Entry = "urman.chapter1:knowledge/zirat-family-links-read";
    private Act1DemoRoot _demo = null!;
    private RuntimeBridge _bridge = null!;
    private FirstPersonController _player = null!;
    private Camera3D _camera = null!;
    private Act1ConnectedWorld _world = null!;
    private readonly List<object> _checks = [];

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")!.Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(10);
            Require(await this.StartThroughMainMenuAsync(_demo), "ordinary-new-game");
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(8);
            _player = _demo.DemoMain.GetNode<FirstPersonController>("Player");
            _camera = _player.GetNode<Camera3D>("Head/Camera3D");
            _bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            _world = _demo.DemoMain.ConnectedWorld ?? throw new InvalidOperationException("Connected world is missing.");
            var target = _world.ZiratFamilyTarget ?? throw new InvalidOperationException("BuildZiratFamily hook is missing.");
            Require(!_bridge.IsDebugSession && !_player.ModalOpen, "ordinary-runtime-without-debug-knowledge");
            Require(Knowledge("zirat-family-links-read") == "hidden"
                && Knowledge("sabirov-family-linked") == "hidden", "initial-family-knowledge-hidden");
            VerifyInscriptions();

            await SupportedFixture(_world.ZiratFamilyReadingPoint, "existing-path-before-stones");
            Require(_world.ZiratFamilyOnPath(_player.GlobalPosition), "reading-fixture-on-authored-path");
            await Aim(target.GlobalPosition);
            await Frames(15);
            Require(Knowledge("zirat-family-links-read") == "hidden"
                && _bridge.JournalEntries().All(entry => entry.EntryId != Entry), "approach-and-looking-do-not-grant-knowledge");
            var before = _bridge.SelectRuntimeState().GetRawText();
            _player.ApplySmokeLook(0, _player.RotationDegrees.Y + 180);
            await Frames(3);
            Require(!target.IsAvailable(), "looking-away-refuses-source");
            target.Interact();
            await Frames(4);
            Require(_bridge.SelectRuntimeState().GetRawText() == before, "wrong-look-does-not-dispatch-effects");

            var rear = _world.ZiratFamilyReadingPoint + new Vector3(0, 0, -2.5f);
            await SupportedFixture(rear, "existing-path-behind-stones");
            await Aim(target.GlobalPosition);
            Require(_world.ZiratFamilyMarkers.All(marker => marker.ToLocal(_camera.GlobalPosition).Z < 0)
                && !target.IsAvailable(), "back-of-marker-refuses-reading");
            target.Interact();
            await Frames(4);
            Require(Knowledge("zirat-family-links-read") == "hidden", "backside-does-not-open-source");

            await SupportedFixture(_world.ZiratFamilyReadingPoint, "return-to-front-path");
            await Aim(target.GlobalPosition);
            await Capture("01_inscriptions_from_path");
            await Press(target);
            var reader = (DocumentUi)GetTree().GetFirstNodeInGroup("document_ui");
            await WaitFor(() => reader.IsOpen && reader.OpenDocumentId == Document, "manual source document");
            Require(Knowledge("zirat-family-links-read") == "confirmed"
                && Knowledge("sabirov-family-linked") == "hidden",
                "manual-read-confirms-names-without-inferring-family");
            Require(_bridge.JournalEntries().Count(entry => entry.EntryId == Entry) == 1,
                "one-notebook-source-after-reading");
            await Capture("02_inscription_document");
            reader.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
            await Press(target);
            await WaitFor(() => reader.IsOpen, "repeat source document");
            Require(_bridge.JournalEntries().Count(entry => entry.EntryId == Entry) == 1,
                "repeat-reading-does-not-duplicate-source");
            reader.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
            var slot = "zirat-family-" + Guid.NewGuid().ToString("N");
            Require(await _bridge.SaveSlotAsync(slot), "save-actually-read-state");
            Require(await _bridge.StartNewGameAsync(), "new-session-resets-read-knowledge");
            Require(Knowledge("zirat-family-links-read") == "hidden"
                && _bridge.JournalEntries().All(entry => entry.EntryId != Entry), "no-new-session-leak");
            Require(await _bridge.LoadSlotAsync(slot), "load-actually-read-state");
            Require(Knowledge("zirat-family-links-read") == "confirmed"
                && Knowledge("sabirov-family-linked") == "hidden"
                && _bridge.JournalEntries().Count(entry => entry.EntryId == Entry) == 1, "load-restores-source-without-early-inference");
            await SupportedFixture(_world.ZiratFamilyReadingPoint, "loaded-source-return");
            await Aim(target.GlobalPosition);
            await Press(target);
            await WaitFor(() => reader.IsOpen, "read after load");
            Require(_bridge.JournalEntries().Count(entry => entry.EntryId == Entry) == 1,
                "reading-after-load-does-not-duplicate-source");
            reader.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print(JsonSerializer.Serialize(new { test = "act1-zirat-family", status = "pass", checks = _checks,
                geometryPolicy = "existing authored pair and path retained", culturalReview = "external/not-run",
                humanPlaytime = "not-run", visual = DisplayServer.GetName() == "headless" ? "not-run" : "capture-if-requested" }));
            exit = 0;
        }
        catch (Exception error)
        {
            GD.PushError("act1-zirat-family-smoke: " + error);
            GD.Print(JsonSerializer.Serialize(new { test = "act1-zirat-family", status = "fail", checks = _checks, error = error.Message }));
        }
        finally
        {
            Input.ActionRelease("interact");
            if (GodotObject.IsInstanceValid(_demo)) await GodotSmokeCleanup.ReleaseAsync(_demo);
            GetTree().Quit(exit);
        }
    }

    private void VerifyInscriptions()
    {
        Require(_world.ZiratFamilyMarkers.Select(marker => marker.Name.ToString()).SequenceEqual(new[]
        {
            "ZiratMarkerGroup_Low_Marker_00_LOD0", "ZiratMarkerGroup_Low_Marker_01_LOD0"
        }), "existing-marker-identities-retained");
        Require(_world.ZiratFamilyMarkers[0].GlobalPosition.DistanceTo(_world.ZiratFamilyMarkers[1].GlobalPosition) < 1.3f,
            "one-existing-neighbouring-pair");
        Require(_world.ZiratFamilyInscriptions.Count == 6, "six-compiled-name-and-date-lines");
        foreach (var marker in _world.ZiratFamilyMarkers)
        {
            var labels = marker.GetChildren().OfType<Label3D>().ToArray();
            var sourceId = labels[0].GetMeta("compiledTextId").AsString();
            var normalized = string.Join(" ", _bridge.ResolveText(sourceId).Split(
                new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            Require(string.Join(" ", labels.Select(label => label.Text)) == normalized,
                "compiled-inscription-matches-" + marker.Name);
            foreach (var label in labels)
            {
                Require(!label.DoubleSided && !label.NoDepthTest && label.Shaded
                    && label.Billboard == BaseMaterial3D.BillboardModeEnum.Disabled
                    && label.Text.All(character => label.Font.HasChar(character)), "real-surface-glyphs-" + marker.Name + "-" + label.Name);
                var from = label.Position + label.Transform.Basis.Z * .01f;
                var to = label.Position - label.Transform.Basis.Z * .01f;
                var faces = marker.Mesh!.GetFaces();
                var hits = new List<Vector3>();
                for (var index = 0; index + 2 < faces.Length; index += 3)
                {
                    var hit = Geometry3D.SegmentIntersectsTriangle(from, to, faces[index], faces[index + 1], faces[index + 2]);
                    if (hit.VariantType != Variant.Type.Nil) hits.Add(hit.AsVector3());
                }
                Require(hits.Any(hit => Math.Abs(label.Position.DistanceTo(hit) - .0015f) < .0001f),
                    "engraved-line-follows-real-facet-" + marker.Name + "-" + label.Name);
            }
            _checks.Add(new { kind = "marker", path = marker.GetPath().ToString(), position = marker.GlobalPosition.ToString(),
                bounds = marker.Mesh!.GetAabb().ToString(), pixelSizes = labels.Select(label => label.PixelSize).ToArray() });
        }
    }

    private async Task SupportedFixture(Vector3 point, string reason)
    {
        // Reuse the common existing ground support; a test fixture is explicitly
        // recorded and never represented as walking time or player wayfinding.
        point.Y = Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .06f;
        Require(_player.CanStandAt(point), "capsule-fits-" + reason);
        _player.ApplyZoneSpawn(point, 0);
        await Frames(6);
        Require(_player.IsOnFloor(), "real-support-" + reason);
        _checks.Add(new { kind = "local-fixture", reason, position = _player.GlobalPosition.ToString(), traversalMeasurement = false });
    }

    private async Task Aim(Vector3 point)
    {
        var direction = point - _camera.GlobalPosition;
        _player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(direction.Y, new Vector2(direction.X, direction.Z).Length())),
            Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Z)));
        await Frames(3);
    }

    private async Task Press(InteractionTarget target)
    {
        var ray = _player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
        ray.ForceRaycastUpdate();
        Require(ray.GetCollider() == target && target.IsAvailable(), "actual-ray-can-read-source");
        try { Input.ActionPress("interact"); await Frames(2); }
        finally { Input.ActionRelease("interact"); }
        await Frames(3);
    }

    private string Knowledge(string id)
    {
        var knowledge = _bridge.SelectRuntimeState().GetProperty("knowledge");
        return knowledge.TryGetProperty("urman.chapter1:knowledge/" + id, out var entry)
            ? entry.GetProperty("status").GetString()! : "hidden";
    }

    private void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        _checks.Add(new { check = label, status = "pass" });
    }

    private async Task WaitFor(Func<bool> condition, string label)
    {
        var deadline = Time.GetTicksMsec() + 15000;
        while (!condition() && Time.GetTicksMsec() < deadline) await Frames(1);
        if (!condition()) throw new TimeoutException(label);
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task Capture(string name)
    {
        var directory = OS.GetEnvironment("URMAN_ZIRAT_FAMILY_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory) || DisplayServer.GetName() == "headless") return;
        if (!System.IO.Path.IsPathFullyQualified(directory) || !System.IO.Directory.Exists(directory))
            throw new InvalidOperationException("Capture directory must be absolute and already exist.");
        var path = System.IO.Path.Combine(directory, name + ".png");
        if (System.IO.File.Exists(path)) throw new InvalidOperationException("Historical capture already exists: " + path);
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "zirat-family/" + name);
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(path) != Error.Ok) throw new InvalidOperationException("Capture failed: " + path);
    }
}
