using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// Ordinary New Game plus a declared, supported local fixture outside the shop.
/// Doors, the final approach, ray, purchase UI and custody rules remain active.
/// Disk failure is injected only by redirecting the existing store to an owned
/// temporary file; no user slot or permission is damaged.
/// </summary>
public partial class Act1NotebookShopSmokeTest : Node
{
    private Act1DemoRoot _demo = null!;
    private RuntimeBridge _bridge = null!;
    private FirstPersonController _player = null!;
    private Camera3D _camera = null!;
    private JournalUi _journal = null!;
    private readonly List<object> _checks = [];
    private readonly string _slot = "notebook-shop-" + Guid.NewGuid().ToString("N");
    private AccessibilitySettingsSnapshot? _originalAccessibility;
    private Dictionary<string, Vector2>? _schoolStreetAnchors;

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
            _journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
            Require(!_player.ModalOpen && !_bridge.IsDebugSession, "ordinary-session-controls");
            await WaitFor(() => !ReadPrivate<bool>(_bridge, "_checkpointBusy"), "initial checkpoint");
            var before = _bridge.SelectRuntimeState().GetRawText();
            Require(!await _bridge.PurchaseShopItemAsync("tea")
                && _bridge.SelectRuntimeState().GetRawText() == before,
                "purchase-from-arrival-refused-without-state-change");

            _originalAccessibility = _player.Accessibility;
            var enlarged = _originalAccessibility with { TextScale = 1.6, HighContrast = true };
            _player.ApplyAccessibilitySettings(enlarged);
            AccessibilityPresentation.ApplyToTree(GetTree(), enlarged);
            var savedNote = await CheckNotebookAsync();
            var notebookOnly = OS.GetEnvironment("URMAN_NOTEBOOK_ONLY") == "1";
            if (!notebookOnly) await CheckShopAsync(savedNote);
            GD.Print(JsonSerializer.Serialize(new { test = "act1-notebook-shop", status = "pass",
                checks = _checks, physicalShopUse = notebookOnly ? "not-run (notebook-only scope)" : "tea on the existing home tray; batteries and stove are separate checks",
                humanPlaytime = "not-run", visual = DisplayServer.GetName() == "headless" ? "not-run" : "captured-if-requested" }));
            exit = 0;
        }
        catch (Exception error)
        {
            GD.PushError("act1-notebook-shop-smoke: " + error);
            GD.Print(JsonSerializer.Serialize(new { test = "act1-notebook-shop", status = "fail", checks = _checks,
                error = error.Message }));
        }
        finally
        {
            foreach (var action in new[] { "move_forward", "interact" }) Input.ActionRelease(action);
            if (_originalAccessibility is not null && GodotObject.IsInstanceValid(_player))
            {
                _player.ApplyAccessibilitySettings(_originalAccessibility);
                AccessibilityPresentation.ApplyToTree(GetTree(), _originalAccessibility);
            }
            if (GodotObject.IsInstanceValid(_demo)) await GodotSmokeCleanup.ReleaseAsync(_demo);
            GetTree().Quit(exit);
        }
    }

    private async Task<string> CheckNotebookAsync()
    {
        _journal.Open(_bridge);
        var screen = _journal.GetNode<Control>("Screen");
        var tabs = _journal.GetNode<TabBar>("Screen/Book/Layout/Tabs");
        var notes = _journal.GetNode<Control>("Screen/Book/Layout/PersonalNotes");
        var editor = notes.GetNode<TextEdit>("Text");
        var save = notes.FindChildren("*", "Button", true, false).OfType<Button>().Single();
        var status = notes.FindChildren("*", "Label", true, false).OfType<Label>().Single(label => label.Name == "Status");
        var close = _journal.GetNode<Button>("Screen/Book/Layout/Header/Close");
        tabs.CurrentTab = 4;
        await Frames(2);
        Require(editor.HasFocus() && editor.GetThemeFontSize("font_size") == Mathf.RoundToInt(18 * 1.6f),
            "notebook-editor-focus-and-enlarged-font");
        var sections = _journal.GetNode<OptionButton>("Screen/Book/Layout/NotebookSection");
        Require(sections.GetPopup().GetThemeFontSize("font_size") == editor.GetThemeFontSize("font_size"),
            "notebook-section-popup-matches-text-scale");

        const string failedText = "Әби попросила купить чай. Проверить номер на доме.";
        var storeField = Field(typeof(RuntimeBridge), "_saveStore");
        var originalStore = storeField.GetValue(_bridge) as AtomicSaveGameStore
            ?? throw new InvalidOperationException("The ordinary session has no existing save-store owner.");
        var faultRoot = ProjectSettings.GlobalizePath("user://notebook-shop-fault-" + Guid.NewGuid().ToString("N"));
        var blocked = System.IO.Path.Combine(faultRoot, "not-a-directory");
        System.IO.Directory.CreateDirectory(faultRoot);
        try
        {
            using (var file = new System.IO.FileStream(blocked, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write))
                file.WriteByte(1);
            storeField.SetValue(_bridge, new AtomicSaveGameStore(blocked));
            // TextEdit.Text invokes set_text and intentionally does not emit
            // text_changed. Use the actual editing path, then press Save in the
            // same frame to cover the engine's deferred text_changed delivery.
            editor.SelectAll();
            editor.InsertTextAtCaret(failedText);
            RecordNotebookState("before-immediate-save", editor, save, status);
            save.EmitSignal(BaseButton.SignalName.Pressed);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
            await WaitFor(() => !save.Disabled, "expected disk failure");
            await Frames(2);
            RecordNotebookState("after-disk-failure", editor, save, status);
            Require(screen.Visible && editor.Text == failedText && status.Text.Contains("Не удалось", StringComparison.Ordinal),
                "disk-failure-keeps-visible-draft-and-reports-refusal");
            Require(_bridge.PersonalNotebookText() == failedText, "failed-disk-write-preserves-committed-runtime-text");
            var draftBeforeFailedLoad = failedText + " Черновик дороги ещё не сохранён.";
            await EditNotebookTextAsync(editor, draftBeforeFailedLoad);
            var sessionBeforeFailedLoad = _bridge.SessionIdentity;
            Require(!await _bridge.LoadSlotAsync("notebook-shop-intentionally-missing"), "expected-load-failure");
            await Frames(2);
            Require(ReferenceEquals(sessionBeforeFailedLoad, _bridge.SessionIdentity)
                && screen.Visible && _player.ModalOpen && Input.MouseMode == Input.MouseModeEnum.Visible
                && editor.Text == draftBeforeFailedLoad && ReadPrivate<bool>(_journal, "_notesDirty"),
                "failed-load-keeps-open-book-dirty-draft-and-modal-owner");
            await EditNotebookTextAsync(editor, string.Empty);
            save.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFor(() => !save.Disabled, "restore original text after failed disk write");
            await Frames(2);
            RecordNotebookState("failed-write-then-revert-original-text", editor, save, status);
            Require(screen.Visible && editor.Text.Length == 0 && _bridge.PersonalNotebookText().Length == 0
                && status.Text.Contains("Не удалось", StringComparison.Ordinal)
                && ReadPrivate<bool>(_journal, "_notesDirty"),
                "reverting-to-original-text-undoes-failed-write-runtime-value-and-keeps-disk-retry");
            await EditNotebookTextAsync(editor, failedText);
            _checks.Add(new { kind = "expected-fault", scope = "test-owned save directory is a regular file",
                modifiedUserFiles = false, modifiedPermissions = false });
        }
        finally
        {
            storeField.SetValue(_bridge, originalStore);
            if (System.IO.File.Exists(blocked)) System.IO.File.Delete(blocked);
            if (System.IO.Directory.Exists(faultRoot)) System.IO.Directory.Delete(faultRoot);
        }

        save.EmitSignal(BaseButton.SignalName.Pressed);
        Require(save.Disabled, "retry-save-is-in-flight-before-Escape");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
        await WaitFor(() => !save.Disabled, "same-text disk retry");
        RecordNotebookState("after-same-text-retry", editor, save, status);
        Require(status.Text == "Записано", "same-text-retry-reaches-real-save-store");
        await WaitFor(() => !screen.Visible, "one Escape during save closes after that save");
        Require(!_player.ModalOpen, "Escape-during-save-releases-player-control");
        Require(await _bridge.LoadSlotAsync(RuntimeBridge.CheckpointSlot), "load-notebook-retry-checkpoint");
        _journal.Open(_bridge);
        await Frames(2);
        Require(editor.Text == failedText && editor.HasFocus(), "disk-retry-survives-load-and-notes-reopen-focus");

        tabs.CurrentTab = 3;
        close.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFor(() => !screen.Visible, "close map");
        _journal.Open(_bridge);
        await Frames(2);
        Require(tabs.HasFocus(), "map-reopen-keeps-keyboard-focus-on-visible-tabs");
        tabs.CurrentTab = 4;
        var first = "Черновик А. " + new string('а', 11000);
        const string current = "Черновик Б. Әби попросила чай; уточнить дорогу у Разили.";
        await EditNotebookTextAsync(editor, first);
        close.EmitSignal(BaseButton.SignalName.Pressed);
        Require(save.Disabled, "real-async-save-yields-for-concurrent-edit-probe");
        await EditNotebookTextAsync(editor, current);
        await WaitFor(() => !save.Disabled, "concurrent draft save");
        Require(screen.Visible && editor.Text == current
            && status.Text.Contains("новые незаписанные", StringComparison.Ordinal),
            "newer-draft-keeps-book-open-after-older-save-completes");
        await Capture("01_notebook_newer_draft");
        close.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFor(() => !screen.Visible, "save current draft and close");
        Require(_bridge.PersonalNotebookText() == current, "close-commits-current-draft");
        _journal.Open(_bridge);
        await EditNotebookTextAsync(editor, "Несохранённый текст перед явной загрузкой.");
        Require(await _bridge.LoadSlotAsync(RuntimeBridge.CheckpointSlot), "load-while-notebook-is-open");
        RecordNotebookFeet("loaded-with-book-open");
        await Frames(3);
        Require(screen.Visible && _player.ModalOpen && Input.MouseMode == Input.MouseModeEnum.Visible
            && editor.Text == current && !ReadPrivate<bool>(_journal, "_notesDirty"),
            "successful-load-restores-saved-draft-and-retains-visible-book-modal");
        close.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFor(() => !screen.Visible, "close loaded notebook");
        RecordNotebookFeet("book-just-closed-after-load");
        Require(!_player.ModalOpen, "loaded-notebook-close-releases-modal-owner");
        await CheckNotebookPhysicalRollbackAsync(current);
        return current;
    }

    private async Task CheckNotebookPhysicalRollbackAsync(string savedText)
    {
        await WaitForNotebookSupportedFeetAsync();
        var slot = _slot + "-draft";
        Require(await _bridge.SaveSlotAsync(slot), "notebook-physical-load-saves-attained-state");
        var savedFeet = _player.GlobalPosition;
        var destination = FindNotebookRollbackWalk(savedFeet);
        await WalkTo(destination, "walk-away-from-notebook-saved-feet-without-placement-fixture");
        var freeFeet = _player.GlobalPosition;
        Require(freeFeet.DistanceTo(savedFeet) > 1.3f && _player.CanStandAt(freeFeet),
            "notebook-rollback-has-a-distinct-walked-to-free-position");

        _journal.Open(_bridge);
        _journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 4;
        var screen = _journal.GetNode<Control>("Screen");
        var notes = _journal.GetNode<Control>("Screen/Book/Layout/PersonalNotes");
        var editor = notes.GetNode<TextEdit>("Text");
        var save = notes.FindChildren("*", "Button", true, false).OfType<Button>().Single();
        var status = notes.FindChildren("*", "Label", true, false).OfType<Label>().Single(label => label.Name == "Status");
        var draft = savedText + " Незаконченная заметка перед отказом загрузки: проверить дорогу.";
        await EditNotebookTextAsync(editor, draft);
        var baseline = ReadPrivate<string>(_journal, "_notesBaseline");
        var draftStatus = status.Text;
        var previousSession = _bridge.SessionIdentity;
        var store = ReadPrivate<AtomicSaveGameStore>(_bridge, "_saveStore");
        var files = new[] { store.SlotPath(slot), store.BackupPath(slot), "user://settings.json" };
        var disk = files.ToDictionary(path => path, NotebookFileDigest);
        var blocker = new StaticBody3D { Name = "NotebookBlockedSavedFeet", CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(.85f, 2.2f, .85f) } });
        AddChild(blocker);
        blocker.GlobalPosition = savedFeet + Vector3.Up * 1.1f;
        try
        {
            await Frames(3);
            Require(!_player.CanCrouchAt(savedFeet) && _player.CanStandAt(freeFeet),
                "owned-obstacle-blocks-saved-capsule-and-leaves-rollback-feet-free");
            var runtimeBefore = JsonNode.Parse(_bridge.SelectRuntimeState().GetRawText());
            Require(!await _bridge.LoadSlotAsync(slot), "notebook-load-rejects-real-blocked-saved-position");
            await Frames(3);
            RecordNotebookState("after-physical-load-rollback", editor, save, status);
            Require(!_bridge.NeedsPhysicalRecovery && _bridge.SessionIdentity is not null
                && !ReferenceEquals(previousSession, _bridge.SessionIdentity)
                && _player.GlobalPosition.DistanceTo(freeFeet) < .08f && _player.CanStandAt(_player.GlobalPosition),
                "physical-load-really-recreates-previous-session-at-free-feet");
            Require(screen.Visible && _player.ModalOpen && Input.MouseMode == Input.MouseModeEnum.Visible
                && editor.Text == draft && ReadPrivate<bool>(_journal, "_notesDirty")
                && ReadPrivate<string>(_journal, "_notesBaseline") == baseline && status.Text == draftStatus
                && ReferenceEquals(ReadPrivate<object?>(_journal, "_notesSession"), _bridge.SessionIdentity),
                "physical-rollback-preserves-visible-draft-baseline-feedback-and-rebinds-session");
            Require(JsonNode.DeepEquals(runtimeBefore, JsonNode.Parse(_bridge.SelectRuntimeState().GetRawText()))
                && _bridge.PersonalNotebookText() == savedText
                && files.All(path => NotebookFileDigest(path) == disk[path]),
                "physical-rollback-writes-neither-draft-progress-slot-backup-nor-profile");
            await Capture("01b_notebook_physical_rollback");
        }
        finally { blocker.QueueFree(); await Frames(3); }

        Require(await _bridge.LoadSlotAsync(slot), "notebook-same-slot-loads-after-owned-obstacle-removal");
        await Frames(3);
        Require(screen.Visible && _player.ModalOpen && editor.Text == savedText
            && !ReadPrivate<bool>(_journal, "_notesDirty") && ReadPrivate<object?>(_journal, "_notesLoadDraft") is null
            && _player.GlobalPosition.DistanceTo(savedFeet) < .08f && _player.CanCrouchAt(_player.GlobalPosition)
            && files.All(path => NotebookFileDigest(path) == disk[path]),
            "successful-physical-retry-accepts-saved-text-and-discards-transient-draft-without-slot-rewrite");
        _journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFor(() => !screen.Visible, "close notebook after physical retry");
        Require(!_player.ModalOpen, "notebook-physical-retry-releases-modal-on-close");
        _checks.Add(new { kind = "notebook-physical-rollback", slot, savedFeet = savedFeet.ToString(),
            rollbackFeet = freeFeet.ToString(), movement = "ordinary walked path", actualObstacle = true,
            negativeLoad = false, positiveLoad = true, unsavedDraftRetainedOnlyForFailure = true });
    }

    private Vector3 FindNotebookRollbackWalk(Vector3 savedFeet)
    {
        var space = _player.GetWorld3D().DirectSpaceState;
        var exclusions = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
        using var ray = PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.Down, 3, exclusions);
        foreach (var direction in new[] { Vector3.Back, Vector3.Right, Vector3.Left, Vector3.Forward,
            new Vector3(1, 0, 1).Normalized(), new Vector3(-1, 0, 1).Normalized(),
            new Vector3(1, 0, -1).Normalized(), new Vector3(-1, 0, -1).Normalized() })
        {
            var clear = true;
            var end = savedFeet;
            for (var sample = 1; sample <= 18; sample++)
            {
                var point = savedFeet + direction * (.1f * sample);
                ray.From = point + Vector3.Up * .45f;
                ray.To = point + Vector3.Down * .6f;
                var hit = space.IntersectRay(ray);
                if (hit.Count == 0 || hit["normal"].AsVector3().Y < .85f) { clear = false; break; }
                end = hit["position"].AsVector3();
                if (Mathf.Abs(end.Y - savedFeet.Y) > .3f || !_player.CanStandAt(end + Vector3.Up * .02f))
                { clear = false; break; }
            }
            if (clear) return end;
        }
        throw new InvalidOperationException("No supported unobstructed short walking path from actual notebook save feet.");
    }

    private async Task WaitForNotebookSupportedFeetAsync()
    {
        var origin = _player.GlobalPosition;
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        var clamps = _player.EdgeClamps;
        var initialSupport = RecordNotebookFeet("before-natural-floor-settle");
        Require(!_player.ModalOpen && !_player.VehicleControlled && !_bridge.NeedsPhysicalRecovery,
            "notebook-floor-settle-has-ordinary-pedestrian-control");
        Require(initialSupport.HasValue && initialSupport.Value.Normal.Y >= Mathf.Cos(_player.FloorMaxAngle)
            && origin.Y - initialSupport.Value.Point.Y >= -.03f
            && origin.Y - initialSupport.Value.Point.Y <= 2f,
            "notebook-floor-settle-has-measured-nearby-walkable-support");
        var expectedSupport = initialSupport!.Value;
        // Loading with the book open preserves the saved feet exactly while the
        // modal suspends gravity. Once it closes, wait for real floor contact,
        // not a guessed number of frames or a fixed vertical displacement. The
        // original bounded ray defines the expected support; losing it, changing
        // its owner, crossing the ground or a stuck session still fails.
        var supportedFrames = 0;
        for (var frame = 0; frame < 60 && supportedFrames < 2; frame++)
        {
            await Frames(1);
            var supported = _player.IsOnFloor() && _player.CanStandAt(_player.GlobalPosition)
                && Mathf.Abs(_player.Velocity.Y) < .02f;
            supportedFrames = supported ? supportedFrames + 1 : 0;
            if (frame < 5 || supportedFrames == 1 || frame == 59)
                RecordNotebookFeet("natural-floor-settle-" + (frame + 1));
        }
        var displacement = _player.GlobalPosition - origin;
        var finalSupport = RecordNotebookFeet("after-natural-floor-settle");
        Require(supportedFrames >= 2 && new Vector2(displacement.X, displacement.Z).Length() < .04f
            && finalSupport.HasValue && finalSupport.Value.Body == expectedSupport.Body
            && Mathf.Abs(finalSupport.Value.Point.Y - expectedSupport.Point.Y) < .02f
            && Mathf.Abs(_player.GlobalPosition.Y - expectedSupport.Point.Y) < .03f
            && displacement.Y <= .03f && _player.PresentationTransformRevision == revision
            && _player.FallRecoveries == recoveries && _player.EdgeClamps == clamps,
            "notebook-physical-load-starts-at-supported-ordinary-feet-without-relocation");
    }

    private (Rid Body, Vector3 Point, Vector3 Normal)? RecordNotebookFeet(string phase)
    {
        var feet = _player.GlobalPosition;
        using var ray = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * .25f,
            feet + Vector3.Down * 2f, 3, new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        var record = new { kind = "notebook-physical-feet", phase, physicsFrame = Engine.GetPhysicsFrames(),
            feet = feet.ToString(), velocity = _player.Velocity.ToString(), onFloor = _player.IsOnFloor(),
            canStand = _player.CanStandAt(feet), canCrouch = _player.CanCrouchAt(feet), crouched = _player.IsCrouching,
            modal = _player.ModalOpen, sessionTransition = ReadPrivate<bool>(_player, "_sessionTransition"),
            modalOwner = ReadPrivate<bool>(_player, "_modalOpen"), stancePending = ReadPrivate<bool>(_player, "_restoreStancePending"),
            vehicle = _player.VehicleControlled, recovery = _bridge.NeedsPhysicalRecovery, treePaused = GetTree().Paused,
            supportOwner = hit.Count == 0 ? null : (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString(),
            supportPoint = hit.Count == 0 ? null : hit["position"].AsVector3().ToString(),
            supportNormal = hit.Count == 0 ? null : hit["normal"].AsVector3().ToString(),
            supportGap = hit.Count == 0 ? (float?)null : feet.Y - hit["position"].AsVector3().Y };
        _checks.Add(record);
        GD.Print(JsonSerializer.Serialize(record));
        return hit.Count == 0 ? null : (hit["rid"].AsRid(), hit["position"].AsVector3(), hit["normal"].AsVector3());
    }

    private static string NotebookFileDigest(string path)
    {
        var native = path.StartsWith("user://", StringComparison.Ordinal) ? ProjectSettings.GlobalizePath(path) : path;
        return System.IO.File.Exists(native)
            ? Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(native))) : "absent";
    }

    private async Task CheckShopAsync(string expectedNote)
    {
        var world = _demo.DemoMain.ConnectedWorld
            ?? throw new InvalidOperationException("The existing connected world is missing.");
        var room = world.PublicBuildingRooms.Single(building => building.Id == "shop");
        // Match the actual clear opening stance verified by PublicBuildings07.
        // The address access point is nearer the door's outward swept volume.
        var outside = room.Outside + room.Metric.GlobalBasis.Z.Normalized() * .55f;
        outside.Y = Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(outside.X, outside.Z) + .035f;
        Require(_player.CanStandAt(outside), "shop-outside-fixture-fits-real-capsule");
        _player.ApplyZoneSpawn(outside, 0);
        await Frames(5);
        Require(_player.IsOnFloor(), "shop-outside-fixture-has-real-support");
        _checks.Add(new { kind = "local-fixture", at = _player.GlobalPosition.ToString(),
            purpose = "start outside existing shop entrance", traversalMeasurement = false });
        await OpenPublicDoorAsync(room.OuterDoor, "shop/entrance",
            room.Metric.GetNode<Node3D>("shopEntranceHinge"), -5f);
        await WalkTo(room.Vestibule, "walk-through-outer-shop-door");
        await OpenPublicDoorAsync(room.InnerDoor, "shop/inner-door",
            room.Metric.GetNode<Node3D>("shopInnerDoorHinge"), -95f);
        var vestibule = room.Metric.ToLocal(room.Vestibule);
        var inside = room.Metric.ToLocal(room.Inside);
        await WalkTo(room.Metric.ToGlobal(new(vestibule.X, vestibule.Y, inside.Z)),
            "align-with-fixed-inner-shop-aperture");
        await WalkTo(room.Inside, "walk-through-inner-shop-door");
        await WalkTo(room.Room.ToGlobal(new Vector3(.65f, 0, -.58f)), "walk-to-real-counter");
        var counter = GetTree().GetNodesInGroup("village_shop_counter").OfType<InteractionTarget>().Single();
        Require(counter.InteractionId == "urman.chapter1:local/shop-counter", "shared-real-shop-counter");
        await AimAndPress(counter);
        await WaitFor(() => GetTree().GetFirstNodeInGroup("village_shop_ui") is ShopUi,
            "counter opens shop UI");
        var shop = (ShopUi)GetTree().GetFirstNodeInGroup("village_shop_ui");
        var screen = shop.GetNode<Control>("Screen");
        Require(screen.Visible && _player.ModalOpen, "physical-counter-opens-modal-shop");
        var stock = shop.FindChildren("*", "VBoxContainer", true, false).OfType<VBoxContainer>().Single(node => node.Name == "Stock");
        var close = shop.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "Закрыть");
        Button ProductButton(string sku)
        {
            var product = ShopCatalog.Find(sku) ?? throw new InvalidOperationException("Missing stock: " + sku);
            return stock.GetChildren().OfType<Button>().Single(button => button.Text.StartsWith(product.Title + " — ", StringComparison.Ordinal));
        }
        void CheckStockScale(string label)
        {
            Require(stock.GetChildren().OfType<Button>().All(button =>
                button.GetThemeFontSize("font_size") == close.GetThemeFontSize("font_size")
                && button.GetThemeFontSize("font_size") > 20
                && button.GetThemeColor("font_color") == Colors.White), label);
        }
        CheckStockScale("new-shop-buttons-use-current-scale-and-contrast");
        foreach (var sku in new[] { "tea", "matches" })
        {
            ProductButton(sku).EmitSignal(BaseButton.SignalName.Pressed);
            await WaitFor(() => !close.Disabled, "purchase " + sku);
            Require(_bridge.HasPocketShopItem(sku) && _bridge.ShopLedger().Count(entry => entry.Sku == sku) == 1,
                "purchase-atomically-receives-and-records-" + sku);
            Require(ProductButton(sku).Disabled, "received-product-disabled-" + sku);
            CheckStockScale("rebuilt-shop-buttons-retain-scale-" + sku);
        }
        Require(!await _bridge.PurchaseShopItemAsync("tea")
            && _bridge.ShopLedger().Count(entry => entry.Sku == "tea") == 1,
            "duplicate-purchase-refused-with-one-ledger-row");
        await Capture("02_shop_purchases");
        close.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        Require(!screen.Visible && !_player.ModalOpen, "shop-closes-and-releases-controls");
        var foundSchool = await CheckHeardAndLocatedAddressAsync(world);
        Require(!await _bridge.TryUsePocketShopItemAsync("tea", "bathhouse/stove") && _bridge.HasPocketShopItem("tea"),
            "wrong-use-refuses-without-consuming-item");
        Require(!await _bridge.TryUsePocketShopItemAsync("tea", "house/tea") && _bridge.HasPocketShopItem("tea"),
            "remote-house-use-from-shop-refuses-without-consuming");
        // Reuse the ordinary arrival documents and response to attain house
        // access. This declared local state fixture is not a navigation test.
        await Act1ArrivalFlowProof.CompleteAsync(this, _bridge);
        Require(await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/arrival-enter-house"),
            "ordinary-earned-house-entry");
        _demo.DemoMain.SwitchZone("house_old_pc", "entry");
        await Frames(8);
        _checks.Add(new { kind = "local-fixture", purpose = "ordinary attained house entry after arrival sources",
            traversalMeasurement = false });
        var teaTarget = world.FindChild("PutTeaOnHomeTray", true, false) as InteractionTarget
            ?? throw new InvalidOperationException("Physical home tea target is missing.");
        var house = teaTarget.GetParent<Node3D>();
        Require(teaTarget.InteractionId == "urman.chapter1:local/shop-tea", "shared-physical-tea-consumer");
        await WalkTo(house.ToGlobal(new Vector3(.69f, 0, -.75f)), "walk-to-home-table-approach");
        var teaStand = house.ToGlobal(new Vector3(.69f, 0, -1.48f));
        Require(_player.CanStandAt(teaStand + Vector3.Up * .06f), "tea-stand-clears-the-existing-table");
        await WalkTo(teaStand, "walk-to-home-tea-tray");
        var teaPackage = house.GetNode<Node3D>("TeaBroughtHome");
        Require(!teaPackage.Visible, "tea-package-absent-before-manual-use");
        await AimAndPress(teaTarget);
        await WaitFor(() => !_bridge.HasPocketShopItem("tea") && teaPackage.Visible, "physical tea use and package projection");
        Require(_bridge.ShopLedger().Count(entry => entry.Sku == "tea") == 1,
            "physical-tea-use-consumes-item-and-keeps-debt-row");
        await Capture("03_tea_on_home_tray");
        await CaptureHouseholdArtAsync(house);
        Require(!await _bridge.TryUsePocketShopItemAsync("tea", "house/tea"), "repeated-use-refused");
        Require(await _bridge.SaveSlotAsync(_slot), "save-attained-notebook-and-shop-state");
        Require(await _bridge.StartNewGameAsync(), "new-session-for-isolation-check");
        await Frames(3);
        Require(_bridge.PersonalNotebookText().Length == 0 && _bridge.ShopLedger().Count == 0 && !teaPackage.Visible,
            "new-session-does-not-inherit-notes-debt-or-placed-tea");
        Require(!_bridge.KnownAddressIds().Contains(foundSchool) && !_bridge.LocatedAddressIds().Contains(foundSchool),
            "new-session-does-not-inherit-heard-or-located-school");
        Require(await _bridge.LoadSlotAsync(_slot), "load-attained-notebook-and-shop-state");
        await Frames(3);
        Require(_bridge.PersonalNotebookText() == expectedNote && _bridge.ShopLedger().Count == 2
            && !_bridge.HasPocketShopItem("tea") && _bridge.HasPocketShopItem("matches") && teaPackage.Visible,
            "load-preserves-note-debt-and-consumed-versus-held-items");
        Require(_bridge.KnownAddressIds().Contains(foundSchool) && _bridge.LocatedAddressIds().Contains(foundSchool),
            "load-preserves-heard-and-actually-read-address");
        await CheckAddressPages(foundSchool, located: true, "loaded-read-school");
        Require(!await _bridge.PurchaseShopItemAsync("tea")
            && !await _bridge.TryUsePocketShopItemAsync("tea", "house/tea")
            && _bridge.ShopLedger().Count == 2, "load-does-not-regrant-purchased-or-consumed-item");
    }

    private async Task CaptureHouseholdArtAsync(Node3D house)
    {
        // The preceding tea capture uses the actual standing player after the
        // real approach. The following camera-only fixtures inspect published
        // meshes; they neither demonstrate an attainable leaning pose nor a
        // normal-distance LOD1 view or a performance sample.
        Require(!_player.ModalOpen && !_player.IsCrouching && _player.IsOnFloor(),
            "household-art-starts-after-real-standing-table-approach-without-ui");
        var cameraPose = _camera.Transform;
        var playerPose = _player.GlobalTransform;
        var standingEyeHeight = _camera.GlobalPosition.Y - _player.GlobalPosition.Y;
        var revision = _player.PresentationTransformRevision;
        var knowledge = _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var props = _bridge.SelectWorldProps().GetRawText();
        var all = house.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToArray();
        var families = new[] { "HouseInterior_TableKettle", "HouseInterior_TableKettleLid", "OldPc_Keyboard" };
        var meshes = families.SelectMany(name => new[] { "_LOD0", "_LOD1" }.Select(lod =>
            all.Single(mesh => mesh.Name == name + lod && mesh.Mesh is not null && mesh.IsVisibleInTree()))).ToArray();
        var saved = meshes.Select(mesh => (Mesh: mesh, mesh.Visible, Begin: mesh.VisibilityRangeBegin,
            End: mesh.VisibilityRangeEnd, BeginMargin: mesh.VisibilityRangeBeginMargin, EndMargin: mesh.VisibilityRangeEndMargin)).ToArray();
        object MeshRecord(MeshInstance3D mesh)
        {
            var aabb = mesh.GetAabb();
            var center = mesh.GlobalTransform * aabb.GetCenter();
            var distance = _camera.GlobalPosition.DistanceTo(center);
            return new
            {
                owner = mesh.GetPath().ToString(), name = mesh.Name.ToString(), mesh = mesh.Mesh!.ResourcePath,
                roomPosition = house.ToLocal(mesh.GlobalPosition).ToString(), localBounds = aabb.ToString(),
                roomBounds = PublicBuildingShell.Bounds(house, mesh).ToString(), worldTransform = mesh.GlobalTransform.ToString(),
                visible = mesh.Visible, visibleInTree = mesh.IsVisibleInTree(), distance,
                visibilityBegin = mesh.VisibilityRangeBegin, visibilityEnd = mesh.VisibilityRangeEnd,
                beginMargin = mesh.VisibilityRangeBeginMargin, endMargin = mesh.VisibilityRangeEndMargin,
                fadeMode = mesh.VisibilityRangeFadeMode.ToString(), triangles = mesh.Mesh.GetFaces().Length / 3,
                metadata = mesh.GetMetaList().ToDictionary(key => key.ToString(), key => mesh.GetMeta(key).ToString()),
                visibilityLimit = "tree visibility and configured distance; actual image remains the visual evidence"
            };
        }
        void Record(string frame, bool forcedLod1)
        {
            var entry = new { kind = "household-art-camera", frame, forcedLod1,
                fixture = forcedLod1 ? "close-up diagnostic with only paired LOD1 forced; not ordinary 18m presentation"
                    : "close-up camera-only inspection; ordinary visibility ranges",
                camera = _camera.GlobalTransform.ToString(), cameraRoomPosition = house.ToLocal(_camera.GlobalPosition).ToString(),
                originalStandingEyeHeight = standingEyeHeight,
                playerFeet = _player.GlobalPosition.ToString(), meshes = meshes.Select(MeshRecord).ToArray(),
                noInteraction = true, performanceSample = false };
            _checks.Add(entry); GD.Print(JsonSerializer.Serialize(entry));
        }
        try
        {
            foreach (var (name, prefix, offset) in new[]
            {
                ("household_kettle", "HouseInterior_TableKettle", new Vector3(.56f, .28f, .36f)),
                ("household_keyboard", "OldPc_Keyboard", new Vector3(.20f, .36f, .32f))
            })
            {
                var source = meshes.Single(mesh => mesh.Name == prefix + "_LOD0");
                var center = source.GlobalTransform * source.GetAabb().GetCenter();
                _camera.GlobalPosition = center + house.GlobalBasis * offset;
                _camera.LookAt(center, Vector3.Up);
                await Frames(2);
                var distance = _camera.GlobalPosition.DistanceTo(center);
                Require(source.Visible && source.IsVisibleInTree()
                    && distance >= source.VisibilityRangeBegin + source.VisibilityRangeBeginMargin
                    && (source.VisibilityRangeEnd == 0 || distance < source.VisibilityRangeEnd - source.VisibilityRangeEndMargin),
                    name + "-close-view-uses-existing-lod0-range");
                Record(name, forcedLod1: false);
                Act1VisibleSurfaceProbe.Log(house, _camera, name, new(.5f, .5f), new(.43f, .5f), new(.57f, .5f));
                await Capture("03a_" + name);
                var paired = meshes.Where(mesh => prefix == "OldPc_Keyboard"
                    ? mesh.Name.ToString().StartsWith(prefix, StringComparison.Ordinal)
                    : mesh.Name.ToString().StartsWith("HouseInterior_TableKettle", StringComparison.Ordinal)).ToArray();
                try
                {
                    foreach (var mesh in paired)
                    {
                        mesh.Visible = mesh.Name.ToString().EndsWith("_LOD1", StringComparison.Ordinal);
                        if (!mesh.Visible) continue;
                        mesh.VisibilityRangeBegin = mesh.VisibilityRangeEnd = 0;
                        mesh.VisibilityRangeBeginMargin = mesh.VisibilityRangeEndMargin = 0;
                    }
                    await Frames(2);
                    Record(name + "_forced_lod1", forcedLod1: true);
                    Act1VisibleSurfaceProbe.Log(house, _camera, name + "/forced-lod1", new(.5f, .5f), new(.43f, .5f), new(.57f, .5f));
                    await Capture("03b_" + name + "_forced_lod1");
                }
                finally { RestoreMeshes(); }
            }
        }
        finally
        {
            RestoreMeshes();
            _camera.Transform = cameraPose;
        }
        await Frames(2);
        Require(saved.All(item => item.Mesh.Visible == item.Visible && item.Mesh.VisibilityRangeBegin == item.Begin
            && item.Mesh.VisibilityRangeEnd == item.End && item.Mesh.VisibilityRangeBeginMargin == item.BeginMargin
            && item.Mesh.VisibilityRangeEndMargin == item.EndMargin)
            && _camera.Transform.IsEqualApprox(cameraPose) && _player.GlobalTransform.IsEqualApprox(playerPose)
            && _player.PresentationTransformRevision == revision
            && knowledge == _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText()
            && props == _bridge.SelectWorldProps().GetRawText(),
            "household-art-restores-camera-and-all-lod-ranges-without-moving-player-or-changing-progress");

        void RestoreMeshes()
        {
            foreach (var item in saved)
            {
                item.Mesh.Visible = item.Visible;
                item.Mesh.VisibilityRangeBegin = item.Begin; item.Mesh.VisibilityRangeEnd = item.End;
                item.Mesh.VisibilityRangeBeginMargin = item.BeginMargin; item.Mesh.VisibilityRangeEndMargin = item.EndMargin;
            }
        }
    }

    private async Task<string> CheckHeardAndLocatedAddressAsync(Act1ConnectedWorld world)
    {
        var registry = _bridge.NotebookSettlement ?? throw new InvalidOperationException("Shared address registry is missing.");
        var school = registry.CanonicalAddressId("ADR-SCHOOL") ?? throw new InvalidOperationException("Stable school alias is missing.");
        Require(!_bridge.KnownAddressIds().Contains(school) && !_bridge.LocatedAddressIds().Contains(school),
            "school-address-unknown-before-the-conversation");
        var greeting = world.FindChildren("*", "", true, false).OfType<InteractionTarget>().Single(target =>
            target.InteractionId == "urman.chapter1:interaction/village-shop-greeting");
        await AimAndPress(greeting);
        var dialogue = (DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui");
        await WaitFor(() => dialogue.IsOpen, "Razilya greeting");
        var choices = dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices");
        var directions = choices.GetChildren().OfType<Button>().Single(button =>
            button.Text == _bridge.ResolveText("urman.chapter1:text/shop-directions-choice"));
        directions.EmitSignal(BaseButton.SignalName.Pressed);
        var spoken = dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line");
        await WaitFor(() => _bridge.KnownAddressIds().Contains(school)
            && spoken.Text.Contains(registry.FormatAddress(school), StringComparison.Ordinal), "actual spoken school directions");
        dialogue._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
        await Frames(3);
        Require(!_bridge.LocatedAddressIds().Contains(school), "spoken-address-does-not-locate-the-house");
        Require(!await _bridge.RememberAddressAsync(school), "cannot-record-school-plate-from-the-shop");
        await CheckAddressPages(school, located: false, "heard-school");
        var heardSlot = "notebook-heard-" + Guid.NewGuid().ToString("N");
        Require(await _bridge.SaveSlotAsync(heardSlot) && await _bridge.LoadSlotAsync(heardSlot), "save-load-heard-address-only");
        Require(_bridge.KnownAddressIds().Contains(school) && !_bridge.LocatedAddressIds().Contains(school),
            "heard-address-load-does-not-invent-a-location");
        await CheckAddressPages(school, located: false, "loaded-heard-school");

        var sign = world.FindChildren("*", "", true, false).OfType<AddressSignVisualComponent>()
            .Single(plate => plate.AddressId == school);
        var stand = sign.GlobalPosition + sign.GlobalBasis.Z.Normalized() * 1.25f;
        stand.Y = Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(stand.X, stand.Z) + .06f;
        Require(_player.CanStandAt(stand), "school-plate-fixture-clears-real-capsule");
        _player.ApplyZoneSpawn(stand, 0);
        await Frames(6);
        Require(_player.IsOnFloor(), "school-plate-fixture-has-real-support");
        _checks.Add(new { kind = "local-fixture", purpose = "physical school plate after hearing the address",
            position = _player.GlobalPosition.ToString(), traversalMeasurement = false });
        Require(!_bridge.LocatedAddressIds().Contains(school), "approaching-school-sign-does-not-record-it");
        var target = sign.GetChildren().OfType<InteractionTarget>().Single();
        await AimAndPress(target);
        await WaitFor(() => _bridge.LocatedAddressIds().Contains(school), "manual plate read");
        Require(registry.TryResolve("ADR-SCHOOL", out var canonical) && canonical.AddressId == school,
            "spoken-semantic-alias-and-physical-plate-share-stable-identity");
        await CheckAddressPages(school, located: true, "read-school");
        return school;
    }

    private async Task CheckAddressPages(string addressId, bool located, string phase)
    {
        var registry = _bridge.NotebookSettlement!;
        _journal.Open(_bridge);
        var tabs = _journal.GetNode<TabBar>("Screen/Book/Layout/Tabs");
        tabs.CurrentTab = 0;
        var section = _journal.GetNode<OptionButton>("Screen/Book/Layout/NotebookSection");
        section.Select(2);
        section.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);
        await Frames(2);
        var entries = _journal.GetNode<ItemList>("Screen/Book/Layout/WorkArea/Entries");
        var title = registry.FormatAddress(addressId);
        var index = Enumerable.Range(0, entries.ItemCount).Single(at => entries.GetItemText(at).Contains(title, StringComparison.Ordinal));
        entries.Select(index);
        entries.EmitSignal(ItemList.SignalName.ItemSelected, (long)index);
        var body = _journal.GetNode<RichTextLabel>("Screen/Book/Layout/WorkArea/Reader/Body");
        Require(body.Text.Contains(title, StringComparison.Ordinal), phase + "-written-address-can-be-reread");
        tabs.CurrentTab = 3;
        await Frames(2);
        var map = _journal.GetNode<SettlementMapControl>("Screen/Book/Layout/VillageMap/Map");
        // The journal also applies settings directly during its own Ready,
        // outside the tree group's traversal order. Its paper child must own
        // the final styles after that same public caller.
        _journal.ApplyAccessibilitySettings(_player.Accessibility);
        await Frames(2);
        // The visible sketch binds only physically located addresses; heard
        // addresses belong to the notebook text, not to the map's draw input.
        var bound = ReadPrivate<string[]>(map, "_located");
        Require(map.DisplayedAddressCount == bound.Length
            && new HashSet<string>(bound, StringComparer.Ordinal).SetEquals(_bridge.LocatedAddressIds()),
            phase + "-map-draw-input-matches-actual-located-addresses");
        var projection = registry.MapForKnownAddresses(bound);
        Require(bound.Contains(addressId) == located
            && projection.Buildings.Any(building => building.AddressId == addressId) == located
            && projection.AccessPoints.Any(access => access.BuildingId == registry.Addresses[addressId].BuildingId) == located,
            phase + "-map-house-and-access-require-manual-plate");
        _checks.Add(new { kind = "notebook-address-map", phase, requestedAddress = addressId,
            located, boundLocatedAddresses = bound, displayedAddressCount = map.DisplayedAddressCount,
            displayedBuildings = projection.Buildings.Select(building => building.BuildingId).ToArray(),
            displayedAccessPoints = projection.AccessPoints.Select(access => access.BuildingId).ToArray() });
        await Capture("04_" + phase + "_map");
        CheckMapInk(map, phase);
        if (located)
        {
            Require(map.DrawnAddressLabels.Any(label => label.AddressId == addressId)
                && map.DrawnRoadIds.Count > 0
                && map.DrawnRoadIds.All(id => !id.StartsWith("access/", StringComparison.Ordinal)
                    && !id.StartsWith("route/", StringComparison.Ordinal))
                && map.DrawnStreetIds.All(id => projection.KnownStreets.Any(street => street.Id == id)),
                phase + "-actual-drawing-shows-located-house-and-context-without-computed-route-or-unearned-name");
            if (phase == "read-school") await CheckMapControlsAsync(map, addressId);
            if (phase == "read-school") _schoolStreetAnchors = map.DrawnStreetAnchors.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (phase == "loaded-read-school")
            {
                Require(_schoolStreetAnchors is { Count: > 0 }
                    && _schoolStreetAnchors.Count == map.DrawnStreetAnchors.Count
                    && _schoolStreetAnchors.All(pair => map.DrawnStreetAnchors.TryGetValue(pair.Key, out var anchor)
                        && anchor.DistanceTo(pair.Value) < .001f),
                    "loaded-map-street-labels-retain-the-same-canonical-road-anchors-after-access-edge-splitting");
                _checks.Add(new { kind = "notebook-map-stable-street-anchors", phase,
                    before = _schoolStreetAnchors!.ToDictionary(pair => pair.Key, pair => pair.Value.ToString()),
                    after = map.DrawnStreetAnchors.ToDictionary(pair => pair.Key, pair => pair.Value.ToString()) });
            }
        }
        _journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFor(() => !_journal.GetNode<Control>("Screen").Visible, "close address notebook");
    }

    private async Task CheckMapControlsAsync(SettlementMapControl map, string addressId)
    {
        var canvas = map.GetNode<Control>("MapLayout/Sketch");
        var zoom = map.GetNode<Button>("MapLayout/Tools/ZoomIn");
        var fit = map.GetNode<Button>("MapLayout/Tools/Fit");
        var knowledge = _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var located = _bridge.LocatedAddressIds().Order(StringComparer.Ordinal).ToArray();
        var feet = _player.GlobalPosition;
        var revision = _player.PresentationTransformRevision;
        map.ApplyAccessibilitySettings(_player.Accessibility with { TextScale = 1.6, HighContrast = true });
        await Frames(4);
        await Capture("04b_school_map_large_text");
        CheckMapInk(map, "large-text");
        var initialCenter = map.ViewCenter;
        var initialZoom = map.ViewZoom;
        var label = map.DrawnAddressLabels.Single(value => value.AddressId == addressId);
        Require(new Rect2(Vector2.Zero, canvas.Size).Encloses(label.Bounds)
            && canvas.GetGlobalRect().Size.Y >= 100
            && map.GetGlobalRect().Encloses(zoom.GetGlobalRect())
            && map.GetGlobalRect().Encloses(fit.GetGlobalRect())
            && zoom.GetThemeFontSize("font_size") >= 24,
            "map-large-text-keeps-actual-house-label-and-controls-inside-page");
        zoom.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        Require(map.ViewZoom > initialZoom, "map-visible-zoom-button-changes-scale");
        canvas.GrabFocus();
        try
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Right, PhysicalKeycode = Key.Right, Pressed = true });
            await Frames(2);
        }
        finally { Input.ParseInputEvent(new InputEventKey { Keycode = Key.Right, PhysicalKeycode = Key.Right, Pressed = false }); }
        Require(canvas.HasFocus() && map.ViewCenter.X > initialCenter.X, "focused-map-receives-real-arrow-key-pan");
        await Capture("04c_school_map_zoom_and_pan");
        fit.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        Require(map.ViewCenter.DistanceTo(initialCenter) < .001f && Math.Abs(map.ViewZoom - initialZoom) < .001f,
            "map-fit-button-restores-known-area-without-player-tracking");
        await CheckMapPointerAsync(map, canvas);
        fit.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        map.ApplyAccessibilitySettings(_player.Accessibility with { TextScale = 1.0, HighContrast = false });
        await Frames(4);
        await Capture("04d_school_map_standard_text");
        CheckMapInk(map, "standard-text");
        Require(map.DrawnAddressLabels.Any(value => value.AddressId == addressId), "map-standard-text-keeps-known-house-caption");
        await CaptureMapReadabilityAsync(map, addressId);
        map.ApplyAccessibilitySettings(_player.Accessibility);
        await Frames(2);
        Require(knowledge == _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText()
            && located.SequenceEqual(_bridge.LocatedAddressIds().Order(StringComparer.Ordinal))
            && _player.GlobalPosition.DistanceTo(feet) < .001f && _player.PresentationTransformRevision == revision,
            "map-controls-change-neither-knowledge-located-houses-nor-player-position");
        _checks.Add(new { kind = "notebook-map-ui", addressId, input = "visible buttons, actual focused key, delivered wheel and drag",
            contextRoads = map.DrawnRoadIds.ToArray(), streetLabels = map.DrawnStreetIds.ToArray(),
            visibleHouseLabels = map.DrawnAddressLabels.Select(value => new { value.AddressId, bounds = value.Bounds.ToString() }).ToArray(),
            computedQuestRoute = false, humanOrientationPlaytest = "not-run" });
    }

    private async Task CheckMapPointerAsync(SettlementMapControl map, Control canvas)
    {
        Require(Input.MouseMode == Input.MouseModeEnum.Visible && _player.ModalOpen,
            "map-pointer-input-uses-the-ordinary-visible-modal-cursor");
        var toInput = canvas.GetViewport().GetScreenTransform() * canvas.GetGlobalTransformWithCanvas();
        var local = canvas.Size * new Vector2(.52f, .55f);
        var end = local + new Vector2(48, -30);
        var startPosition = toInput * local;
        var endPosition = toInput * end;
        var originalMouse = canvas.GetViewport().GetScreenTransform() * canvas.GetViewport().GetMousePosition();
        async Task Deliver(InputEvent input, Func<InputEvent, bool> matches, Func<bool> applied, Vector2 expectedLocal, string name)
        {
            var count = 0; ulong deliveredFrame = 0; var receivedPosition = Vector2.Inf;
            void Observe(InputEvent value)
            {
                if (!matches(value)) return;
                count++; deliveredFrame = Engine.GetProcessFrames();
                if (value is InputEventMouse mouse) receivedPosition = mouse.Position;
            }
            canvas.GuiInput += Observe;
            var started = Time.GetTicksMsec(); var frames = 0; var ready = false;
            try
            {
                using (input)
                {
                    Input.ParseInputEvent(input);
                    do
                    {
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); frames++;
                        ready = count == 1 && Engine.GetProcessFrames() > deliveredFrame && applied()
                            && receivedPosition.DistanceTo(expectedLocal) < .5f;
                    } while (!ready && Time.GetTicksMsec() - started < 2000 && frames < 180);
                }
            }
            finally
            {
                canvas.GuiInput -= Observe;
                _checks.Add(new { kind = "notebook-map-pointer-delivery", name, count, deliveredFrame,
                    process = Engine.GetProcessFrames(), frames, milliseconds = Time.GetTicksMsec() - started,
                    expectedLocal = expectedLocal.ToString(), receivedLocal = receivedPosition.ToString(),
                    toInput = toInput.ToString(), center = map.ViewCenter.ToString(), zoom = map.ViewZoom, ready });
            }
            Require(ready, name + "-reaches-real-canvas-and-applies-before-the-next-input");
        }
        try
        {
            var zoomBefore = map.ViewZoom;
            await Deliver(new InputEventMouseButton { Position = startPosition, GlobalPosition = startPosition,
                    ButtonIndex = MouseButton.WheelUp, Pressed = true, Factor = 1 },
                value => value is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true },
                () => Math.Abs(map.ViewZoom - zoomBefore * 1.2f) < .001f, local, "map-wheel-up");
            await Deliver(new InputEventMouseButton { Position = startPosition, GlobalPosition = startPosition,
                    ButtonIndex = MouseButton.WheelUp, Pressed = false },
                value => value is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: false },
                () => Math.Abs(map.ViewZoom - zoomBefore * 1.2f) < .001f, local, "map-wheel-release");
            await Deliver(new InputEventMouseButton { Position = startPosition, GlobalPosition = startPosition,
                    ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left },
                value => value is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true },
                () => canvas.HasFocus() && ReadPrivate<bool>(map, "_dragging"), local, "map-drag-press");
            var centerBefore = map.ViewCenter; var dragZoom = map.ViewZoom;
            var fitBounds = ReadPrivate<Rect2>(map, "_fitBounds");
            var pixelsPerMetre = Math.Min(canvas.Size.X / Math.Max(1, fitBounds.Size.X),
                canvas.Size.Y / Math.Max(1, fitBounds.Size.Y)) * dragZoom;
            var expectedCenter = centerBefore - (end - local) / pixelsPerMetre;
            await Deliver(new InputEventMouseMotion { Position = endPosition, GlobalPosition = endPosition,
                    Relative = endPosition - startPosition, ScreenRelative = endPosition - startPosition, ButtonMask = MouseButtonMask.Left },
                value => value is InputEventMouseMotion,
                () => map.ViewCenter.DistanceTo(expectedCenter) < .002f && Math.Abs(map.ViewZoom - dragZoom) < .001f,
                end, "map-drag-motion");
            await Deliver(new InputEventMouseButton { Position = endPosition, GlobalPosition = endPosition,
                    ButtonIndex = MouseButton.Left, Pressed = false },
                value => value is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false },
                () => !ReadPrivate<bool>(map, "_dragging"), end, "map-drag-release");
            await Capture("04e_school_map_actual_wheel_and_drag");
        }
        finally
        {
            Input.ParseInputEvent(new InputEventMouseButton { Position = startPosition, GlobalPosition = startPosition,
                ButtonIndex = MouseButton.WheelUp, Pressed = false });
            Input.ParseInputEvent(new InputEventMouseButton { Position = endPosition, GlobalPosition = endPosition,
                ButtonIndex = MouseButton.Left, Pressed = false });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = originalMouse, GlobalPosition = originalMouse });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task CaptureMapReadabilityAsync(SettlementMapControl map, string addressId)
    {
        var windowSize = DisplayServer.WindowGetSize(); var windowPosition = DisplayServer.WindowGetPosition();
        var windowMode = DisplayServer.WindowGetMode();
        var center = map.ViewCenter; var zoom = map.ViewZoom;
        try
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                DisplayServer.WindowSetSize(size);
                await WaitFor(() => DisplayServer.WindowGetSize() == size, "map actual window resize");
                foreach (var textScale in new[] { 1.0, 1.6 })
                {
                    map.ApplyAccessibilitySettings(_player.Accessibility with { TextScale = textScale, HighContrast = textScale > 1 });
                    for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var name = $"04f_school_map_{size.X}x{size.Y}_text_{(textScale > 1 ? "large_hc" : "standard")}";
                    await Capture(name);
                    var canvas = map.GetNode<Control>("MapLayout/Sketch");
                    // Independent oracle: measure the image actually rendered,
                    // then map logical viewport coordinates into those pixels.
                    // Do not use the runtime's screen-transform API for the
                    // font assertion: it previously repeated the same omission.
                    using var raster = GetViewport().GetTexture().GetImage();
                    var rasterSize = new Vector2(raster.GetWidth(), raster.GetHeight());
                    var logical = canvas.GetViewport().GetVisibleRect();
                    Require(!raster.IsEmpty() && logical.Size.X > 0 && logical.Size.Y > 0
                        && rasterSize == (Vector2)size, name + "-uses-the-actual-requested-raster");
                    var ratio = rasterSize / logical.Size;
                    var logicalToRaster = new Transform2D(new Vector2(ratio.X, 0), new Vector2(0, ratio.Y), -logical.Position * ratio);
                    Transform2D RasterTransform(Control item) => logicalToRaster * item.GetGlobalTransformWithCanvas();
                    float RasterScale(Control item)
                    {
                        var measured = RasterTransform(item);
                        return Math.Min(measured.X.Length(), measured.Y.Length());
                    }
                    Rect2 Project(Transform2D transform, Rect2 rectangle)
                    {
                        var points = new[] { rectangle.Position, rectangle.End,
                            new Vector2(rectangle.Position.X, rectangle.End.Y), new Vector2(rectangle.End.X, rectangle.Position.Y) }
                            .Select(point => transform * point).ToArray();
                        var min = new Vector2(points.Min(point => point.X), points.Min(point => point.Y));
                        var max = new Vector2(points.Max(point => point.X), points.Max(point => point.Y));
                        return new Rect2(min, max - min);
                    }
                    var measuredTransform = RasterTransform(canvas);
                    var runtimeTransform = canvas.GetViewport().GetScreenTransform() * canvas.GetGlobalTransformWithCanvas();
                    var actualScale = RasterScale(canvas);
                    var labels = map.FindChildren("*", nameof(Label), recursive: true, owned: false).OfType<Label>().ToArray();
                    var buttons = map.FindChildren("*", nameof(Button), recursive: true, owned: false).OfType<Button>().ToArray();
                    var caption = map.DrawnAddressLabels.Single(value => value.AddressId == addressId);
                    var canvasRect = new Rect2(Vector2.Zero, canvas.Size);
                    var measuredCanvas = Project(measuredTransform, canvasRect);
                    var runtimeCanvas = Project(runtimeTransform, canvasRect);
                    var measuredMap = Project(RasterTransform(map), new Rect2(Vector2.Zero, map.Size));
                    var measuredCaption = Project(measuredTransform, caption.Bounds);
                    _checks.Add(new { kind = "notebook-map-screen-readability", size = size.ToString(), textScale,
                        rasterSize = rasterSize.ToString(), logicalViewport = logical.ToString(), rasterToLogicalRatio = ratio.ToString(),
                        measuredRasterTransform = measuredTransform.ToString(), runtimeCompositeTransform = runtimeTransform.ToString(),
                        canvasItemPopupTransform = canvas.GetScreenTransform().ToString(), actualScale,
                        drawingFontLogical = map.DrawingFontSize, drawingFontScreenPixels = map.DrawingFontSize * actualScale,
                        labelScreenFonts = labels.Select(label => new { label.Text, logical = label.GetThemeFontSize("font_size"),
                            pixels = label.GetThemeFontSize("font_size") * RasterScale(label), visible = label.IsVisibleInTree(),
                            boundsInRaster = Project(RasterTransform(label), new Rect2(Vector2.Zero, label.Size)).ToString() }).ToArray(),
                        mapBoundsInRaster = measuredMap.ToString(), canvasBoundsInRaster = measuredCanvas.ToString(),
                        runtimeCanvasBounds = runtimeCanvas.ToString(), captionBoundsInRaster = measuredCaption.ToString(),
                        houseCaption = caption.Bounds.ToString(), center = map.ViewCenter.ToString(), zoom = map.ViewZoom,
                        oracle = "actual rendered image dimensions divided by logical viewport extent; no screen-transform API in font measurement" });
                    Require(measuredCanvas.Position.DistanceTo(runtimeCanvas.Position) < .5f
                        && measuredCanvas.Size.DistanceTo(runtimeCanvas.Size) < .5f
                        && new Rect2(Vector2.Zero, rasterSize).Grow(1).Encloses(measuredMap)
                        && measuredMap.Grow(1).Encloses(measuredCanvas) && measuredCanvas.Grow(1).Encloses(measuredCaption)
                        && labels.Where(label => label.IsVisibleInTree()).All(label => measuredMap.Grow(1)
                            .Encloses(Project(RasterTransform(label), new Rect2(Vector2.Zero, label.Size))))
                        && buttons.All(button => measuredMap.Grow(1)
                            .Encloses(Project(RasterTransform(button), new Rect2(Vector2.Zero, button.Size)))),
                        name + "-independent-raster-bounds-match-runtime-and-keep-caption-and-toolbar-on-page");
                    Require(map.DrawingFontSize * actualScale >= 14 * textScale - .05
                        && labels.All(label => label.GetThemeFontSize("font_size")
                            * RasterScale(label) >= 14 * textScale - .05)
                        && new Rect2(Vector2.Zero, canvas.Size).Encloses(caption.Bounds)
                        && map.ViewCenter.DistanceTo(center) < .001f && Math.Abs(map.ViewZoom - zoom) < .001f,
                        name + "-keeps-readable-final-screen-fonts-caption-and-view");
                    CheckMapInk(map, name);
                }
            }
        }
        finally
        {
            DisplayServer.WindowSetSize(windowSize); DisplayServer.WindowSetMode(windowMode); DisplayServer.WindowSetPosition(windowPosition);
            map.ApplyAccessibilitySettings(_player.Accessibility);
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "notebook-map/restore-window");
        }
        Require(DisplayServer.WindowGetSize() == windowSize && map.ViewCenter.DistanceTo(center) < .001f
            && Math.Abs(map.ViewZoom - zoom) < .001f, "map-window-fixtures-restore-resolution-and-existing-view");
    }

    private void CheckMapInk(SettlementMapControl map, string phase)
    {
        // Inspect the actual styles after the ordinary journal/tree accessibility
        // pass as well as after the map's scaled variants. A black offset shadow
        // on dark paper ink was visibly duplicated even though bounds passed.
        var labels = map.FindChildren("*", nameof(Label), recursive: true, owned: false).OfType<Label>().ToArray();
        var buttons = map.FindChildren("*", nameof(Button), recursive: true, owned: false).OfType<Button>().ToArray();
        Require(labels.Length == 4 && buttons.Length == 3
            && labels.All(label => label.GetThemeColor("font_shadow_color").A == 0
                && label.GetThemeConstant("shadow_offset_x") == 0
                && label.GetThemeConstant("shadow_offset_y") == 0
                && label.GetThemeConstant("outline_size") == 0)
            && buttons.All(button => button.GetThemeConstant("outline_size") == 0),
            phase + "-paper-map-text-has-no-inherited-offset-shadow-or-dark-outline");
    }

    private async Task OpenPublicDoorAsync(InteractionTarget target, string key, Node3D hinge, float openDegrees)
    {
        bool Open()
        {
            var props = _bridge.SelectWorldProps();
            return props.TryGetProperty(key, out var record) && record.TryGetProperty("open", out var value)
                && value.ValueKind == JsonValueKind.True;
        }
        void Record(string phase)
        {
            var evidence = new { kind = "notebook-shop-door", phase, key,
                feet = _player.GlobalPosition.ToString(), target = target.GlobalPosition.ToString(),
                hinge = hinge.GetPath().ToString(), angleDegrees = hinge.RotationDegrees.Y, open = Open(),
                expectedAngleDegrees = openDegrees, modal = _player.ModalOpen, focus = DisplayServer.WindowIsFocused(),
                actionNumber = target.GetMeta("lastDoorActionNumber", 0).AsInt32(),
                result = target.GetMeta("lastDoorActionResult", "not-called").AsString(),
                sweep = target.GetMeta("doorSweepProbe", "not-called").AsString() };
            _checks.Add(evidence); GD.Print(JsonSerializer.Serialize(evidence));
        }
        Require(!Open(), key + "-starts-closed-before-deliberate-input");
        var action = target.GetMeta("lastDoorActionNumber", 0).AsInt32();
        Record("before-input");
        try
        {
            await AimAndPress(target);
            Record("after-input");
            Require(target.GetMeta("lastDoorActionNumber", 0).AsInt32() == action + 1,
                key + "-one-input-reaches-exactly-one-door-action");
            await WaitFor(() => target.GetMeta("lastDoorActionResult", "not-called").AsString() is not ("received" or "not-called"),
                key + " actual door transaction completes");
            Require(target.GetMeta("lastDoorActionResult", "not-called").AsString() == "committed" && Open(),
                key + "-ordinary-door-action-commits-open-state");
            await WaitFor(() => Math.Abs(Mathf.Wrap(hinge.RotationDegrees.Y - openDegrees, -180f, 180f)) < .1f,
                key + " actual leaf reaches its authored open angle");
        }
        finally { Record("finished"); }
    }

    private async Task AimAndPress(InteractionTarget target)
    {
        // The first-person head offset moves the camera when the body turns.
        // Recalculate from the actual camera until the look direction converges.
        for (var iteration = 0; iteration < 6; iteration++)
        {
            var delta = target.GlobalPosition - _camera.GlobalPosition;
            _player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length())),
                Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
        }
        await Frames(3);
        var ray = _player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
        ray.ForceRaycastUpdate();
        Require(ray.GetCollider() == target && target.IsAvailable(), "actual-ray-and-availability-" + target.Name);
        try { Input.ActionPress("interact"); await Frames(2); }
        finally { Input.ActionRelease("interact"); }
        await Frames(3);
    }

    private async Task WalkTo(Vector3 point, string label)
    {
        var from = _player.GlobalPosition;
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        var clamps = _player.EdgeClamps;
        var reached = false;
        try
        {
            for (var frame = 0; frame < 600; frame++)
            {
                var delta = point - _player.GlobalPosition;
                if (new Vector2(delta.X, delta.Z).Length() < .13f) { reached = true; break; }
                _player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
                Input.ActionPress("move_forward");
                await Frames(1);
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        await Frames(3);
        var evidence = new { kind = "notebook-shop-walk", label, from = from.ToString(), goal = point.ToString(),
            feet = _player.GlobalPosition.ToString(), reached, onFloor = _player.IsOnFloor(),
            canStand = _player.CanStandAt(_player.GlobalPosition),
            contacts = Enumerable.Range(0, _player.GetSlideCollisionCount()).Select(index =>
            {
                var contact = _player.GetSlideCollision(index);
                return new { owner = (contact.GetCollider() as Node)?.GetPath().ToString(),
                    point = contact.GetPosition().ToString(), normal = contact.GetNormal().ToString() };
            }).ToArray() };
        _checks.Add(evidence); GD.Print(JsonSerializer.Serialize(evidence));
        Require(reached && _player.IsOnFloor() && _player.PresentationTransformRevision == revision
            && _player.FallRecoveries == recoveries && _player.EdgeClamps == clamps, label);
    }

    private void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
        _checks.Add(new { check, status = "pass" });
    }

    private async Task EditNotebookTextAsync(TextEdit editor, string text)
    {
        var changed = false;
        void OnChanged() => changed = true;
        editor.TextChanged += OnChanged;
        try
        {
            editor.SelectAll();
            editor.InsertTextAtCaret(text);
            await WaitFor(() => changed, "actual notebook text_changed");
            Require(editor.Text == text && ReadPrivate<bool>(_journal, "_notesDirty"),
                "actual-editor-change-reaches-dirty-draft");
        }
        finally { editor.TextChanged -= OnChanged; }
    }

    private void RecordNotebookState(string phase, TextEdit editor, Button save, Label status)
    {
        var record = new { kind = "notebook-write-state", phase, editorLength = editor.Text.Length,
            runtimeLength = _bridge.PersonalNotebookText().Length, dirty = ReadPrivate<bool>(_journal, "_notesDirty"),
            saving = ReadPrivate<bool>(_journal, "_notesSaving"), saveDisabled = save.Disabled,
            visible = editor.IsVisibleInTree(), status = status.Text,
            sameSession = ReferenceEquals(ReadPrivate<object?>(_journal, "_notesSession"), _bridge.SessionIdentity) };
        _checks.Add(record);
        GD.Print(JsonSerializer.Serialize(record));
    }

    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing existing test inspection field: " + type.Name + "." + name);
    private static T ReadPrivate<T>(object owner, string name) => (T)Field(owner.GetType(), name).GetValue(owner)!;

    private async Task WaitFor(Func<bool> condition, string label)
    {
        var deadline = Time.GetTicksMsec() + 15000;
        while (!condition() && Time.GetTicksMsec() < deadline) await Frames(1);
        if (!condition()) throw new TimeoutException(label);
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task Capture(string name)
    {
        var directory = OS.GetEnvironment("URMAN_NOTEBOOK_SHOP_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory) || DisplayServer.GetName() == "headless") return;
        if (!System.IO.Path.IsPathFullyQualified(directory) || !System.IO.Directory.Exists(directory))
            throw new InvalidOperationException("Capture directory must be absolute and already exist.");
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "notebook-shop/" + name);
        var path = System.IO.Path.Combine(directory, name + ".png");
        if (System.IO.File.Exists(path)) throw new InvalidOperationException("Historical capture already exists: " + path);
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(path) != Error.Ok) throw new InvalidOperationException("Failed to save capture: " + path);
        GD.Print("notebook-shop-capture: " + path);
    }
}
