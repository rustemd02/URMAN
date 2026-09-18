using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Ordinary New Game plus explicit local camera fixtures. Each attempt uses the
/// real input/ray/custody owner. Saves are made only from attained states. This
/// is a regression check, not an unfamiliar player's exploration or duration.
/// </summary>
public partial class Act1YardMechanismsSmokeTest : Node
{
    private Act1DemoRoot _demo = null!;
    private FirstPersonController _player = null!;
    private Camera3D _camera = null!;
    private CarryCoordinator _carry = null!;
    private RuntimeBridge _bridge = null!;
    private bool _inside;
    private float _standingBodyHeight;
    private int _checks;
    private readonly List<object> _receipt = new();

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(10);
            Require(await this.StartThroughMainMenuAsync(_demo), "ordinary New Game");
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(5);
            _player = _demo.DemoMain.GetNode<FirstPersonController>("Player");
            _standingBodyHeight = _player.BodyHeight;
            _camera = _player.GetNode<Camera3D>("Head/Camera3D");
            _carry = (CarryCoordinator)GetTree().GetFirstNodeInGroup("carry_coordinator");
            _bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            await Frames(5);
            Require(!_player.ModalOpen && !_player.IsCrouching && _carry.Mechanisms.Count == 11,
                "eleven actual local mechanisms, ordinary standing controls");
            Require(await _bridge.SaveSlotAsync("yard-mechanisms-pristine"), "pristine reachable save");
            var boardOnly = System.Environment.GetEnvironmentVariable("URMAN_YARD_FOCUS") == "board-rest";
            if (!boardOnly)
            {
            Act1CarryRestProof.Check(_player, _carry, Require);
            if (System.Environment.GetEnvironmentVariable("URMAN_MECHANISM_CAPTURE") == "1")
            {
                var gate = _demo.DemoMain.FindChildren("*", "", true, false).OfType<InteractionTarget>()
                    .Single(target => target.InteractionId
                        == "urman.chapter1:interaction/discover-babai-yard-loose-side-gate-board");
                await Approach(gate);
                Require(gate.IsAvailable(), "the side-gate view precedes its first interaction");
                await Capture("00_side_gate_before_interaction");
            }
            var frost = Mechanism("window-thaw");
            await Approach(frost);
            await Press("interact");
            Require(!frost.Solved, "empty hand does not thaw the window");
            await Take("carry-cloth");
            await Approach(frost);
            await Press("interact");
            Require(!frost.Solved && !Item("carry-cloth").IsWarm, "cold cloth explains refusal without an effect");
            await Capture("01_cold_cloth_refusal");

            await Act1ArrivalFlowProof.CompleteAsync(this, _bridge);
            Require(await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/arrival-enter-house"), "ordinary house entry gate");
            _demo.DemoMain.SwitchZone("house_old_pc", "entry");
            _inside = true;
            await Frames(8);
            Require(_carry.HeldItem == Item("carry-cloth") && _carry.HeldItem.IsVisibleInTree(), "carried cloth crosses actual presentation boundary");
            Require(Item("carry-cloth").GlobalPosition.DistanceTo(_camera.GlobalPosition) < 1.6f,
                "held cloth follows the player through the actual doorway");
            await EstablishOrdinaryHouseExit();
            var warm = Mechanism("warm-water");
            await Approach(warm);
            await Press("interact");
            Require(Item("carry-cloth").IsWarm, "warm water affects the cloth only after the action");
            Require(await _bridge.SaveSlotAsync("yard-warm-cloth"), "warm held cloth saved");
            await Capture("02_warm_water_in_house");
            Require(await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/house-to-route"),
                "ordinary first house exit is earned before the outdoor heat attempt");
            _demo.DemoMain.SwitchZone("village_day", "arrival");
            _inside = false;
            await Frames(8);
            await Approach(frost);
            await Press("interact");
            Require(frost.Solved && !Item("carry-cloth").IsWarm && frost.CollisionLayer == 0,
                "thaw consumes local warmth and clears the hidden frost ray skin");
            await Capture("03_cleared_window");
            await PutNear(new(-29.8f, 0, 5.9f));
            await Take("carry-lantern");
            var glass = Mechanism("window-light");
            await Approach(glass);
            await Press("interact");
            Require(!glass.Solved, "unlit lamp grants no observation");
            _player.SetModalOpen(true);
            await Press("carry_use");
            Require(!Item("carry-lantern").LightOn, "modal blocks lamp input");
            EnsureNoPauseShell("release the intentional light modal fixture");
            _player.SetModalOpen(false);
            await Press("carry_use");
            Require(!Item("carry-lantern").LightOn,
                "the modal-close physics frame cannot also toggle the held lamp");
            // Press released the action and let ordinary neutral frames pass.
            // Only this new, distinct press may act in the world again.
            await Press("carry_use");
            Require(Item("carry-lantern").LightOn, "mapped lamp switch works while aiming at the glass");
            await Press("interact");
            Require(glass.Solved, "actual lit unobstructed lamp reveals the repair detail");
            var cupboard = Mechanism("cabinet-thaw");
            await Approach(cupboard);
            await Press("interact");
            Require(!cupboard.Solved, "light does not melt the frozen latch");
            await Capture("04_lamp_is_not_heat");
            await PutNear(new(-29.3f, 0, 5.8f));
            Require(await _bridge.SaveSlotAsync("yard-thawed-window"), "window and placed light save together");
            var lampPose = Item("carry-lantern").GlobalPosition;
            await Load("yard-warm-cloth", true);
            Require(Item("carry-cloth").IsWarm && !frost.Solved && !glass.Solved, "older warm save resets future observations");
            Require(await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/house-to-route"),
                "alternate branch also uses the earned house exit");
            _demo.DemoMain.SwitchZone("village_day", "arrival");
            _inside = false;
            await Frames(8);
            await Approach(cupboard);
            await Press("interact");
            Require(cupboard.Solved && !Item("carry-cloth").IsWarm, "same heat tool opens a second physical latch");
            await PutNear(new(-29.8f, 0, 5.9f));
            await Take("carry-lantern");
            await Press("carry_use");
            var card = Mechanism("cabinet-light");
            await Approach(card);
            Require(Item("carry-lantern").LightOn
                && Item("carry-lantern").GlobalPosition.DistanceTo(_camera.GlobalPosition) < 1.6f,
                "relocated held lamp stays lit and physically beside the player");
            await Press("interact");
            Require(!card.Solved, "shelf blocks light from above even when the eye can see the card");
            var beforeLowering = _bridge.SelectWorldProps().GetRawText();
            var beforeLoweringPlayer = _player.GlobalPosition;
            var beforeLoweringLamp = Item("carry-lantern").GlobalPosition;
            await Press("crouch");
            if (!_player.IsCrouching)
            {
                GD.Print("act1-yard-mechanisms: blocked stance " + _carry.LastBlockedStanceProbe);
                Require(_carry.HeldItem == Item("carry-lantern") && Item("carry-lantern").LightOn
                    && _player.GlobalPosition.DistanceTo(beforeLoweringPlayer) < .01f
                    && Item("carry-lantern").GlobalPosition.DistanceTo(beforeLoweringLamp) < .035f
                    && _bridge.SelectWorldProps().GetRawText() == beforeLowering,
                    $"blocked lowering preserves stance, lamp and saved props: player {beforeLoweringPlayer} -> {_player.GlobalPosition}, lamp {beforeLoweringLamp} -> {Item("carry-lantern").GlobalPosition}");
                await BackAwayWithLamp();
                await Press("crouch");
            }
            Require(_player.IsCrouching, "the clear approach permits the actual crouch input");
            Aim(card.GlobalPosition);
            await Frames(4);
            Require(_carry.HasValidHeldPose && Math.Abs(Item("carry-lantern").GlobalPosition.Y
                - _camera.GlobalPosition.Y - Item("carry-lantern").HoldDrop) < .035f,
                $"lamp follows the lowered eye: before {beforeLoweringLamp}, after {Item("carry-lantern").GlobalPosition}, eye {_camera.GlobalPosition}");
            await Press("interact");
            Require(card.Solved, "opened cabinet can be inspected with the same lamp");
            await CaptureCabinetPromptAtBothResolutions(card);
            await Load("yard-thawed-window", false);
            Require(frost.Solved && glass.Solved && !cupboard.Solved && !card.Solved
                && Item("carry-lantern").LightOn && Item("carry-lantern").GlobalPosition.DistanceTo(lampPose) < .02f,
                "branch load restores light pose and correct independent mechanisms");

            await Load("yard-mechanisms-pristine", false);
            var shelfBox = Mechanism("high-shelf-box");
            var shelfSource = shelfBox.MovingPart!;
            var rearShelfPose = shelfSource.GlobalPosition;
            await Approach(shelfBox);
            await Press("interact");
            Require(!shelfBox.Solved && shelfSource.GlobalPosition.IsEqualApprox(rearShelfPose),
                "bare hand cannot move the far shelf box; refusal keeps its real pose");
            await Take("carry-tool-pole");
            var side = Mechanism("side-panel");
            await Approach(side);
            await ObserveWindCue(side, solved: false, "panel-before-pole");
            await Press("interact");
            Require(side.Solved && !Item("carry-tool-pole").HasHook, "plain pole secures the loose panel");
            await ObserveWindCue(side, solved: true, "panel-after-pole");
            await Approach(shelfBox);
            await Press("interact");
            Require(shelfBox.Solved && !Item("carry-tool-pole").HasHook
                && shelfSource.GlobalPosition.DistanceTo(rearShelfPose) > .45f,
                "same plain pole moves the light box along the upper shelf to its edge");
            await Capture("05b_plain_pole_second_use");
            Require(await _bridge.SaveSlotAsync("yard-plain-pole-results"), "both plain-pole uses saved together");
            await Load("yard-mechanisms-pristine", false);
            Require(!side.Solved && !shelfBox.Solved && shelfSource.GlobalPosition.IsEqualApprox(rearShelfPose),
                "earlier save restores both independent physical pole results");
            await Load("yard-plain-pole-results", false);
            Require(side.Solved && shelfBox.Solved && _carry.HeldItem == Item("carry-tool-pole"),
                "both pole uses and the reusable held tool restore together");
            await Approach(side);
            await ObserveWindCue(side, solved: true, "panel-after-load");
            var upper = Mechanism("upper-latch");
            await Approach(upper);
            await ObserveWindCue(upper, solved: false, "latch-before-hook");
            await Press("interact");
            Require(!upper.Solved, "plain pole cannot pull the hook latch");
            await Approach(Item("carry-hook"));
            await CheckHookAssemblyClearance();
            Require(AimedAt(Item("carry-hook")), "the unchanged clear shelf approach still aims at the real loose hook");
            await Press("interact");
            GD.Print("act1-yard-mechanisms: clear shelf assembly " + HookAssemblyState());
            Require(_carry.HeldItem == Item("carry-tool-pole") && Item("carry-tool-pole").HasHook
                && Item("carry-hook").IsConcealed, "two real parts become one carried assembly atomically; " + HookAssemblyState());
            Require(CustodyOwner("carry-hook") == "attachment/carry-tool-pole" && CustodyOwner("carry-tool-pole") == "player",
                "hook is not duplicated in world or held custody");
            Require(await _bridge.SaveSlotAsync("yard-hooked-pole"), "combined tool saved");
            await Approach(upper);
            await Press("interact");
            Require(upper.Solved, "hooked pole secures the high shutter");
            await ObserveWindCue(upper, solved: true, "latch-after-hook");
            Require(await _bridge.SaveSlotAsync("yard-quiet-hook-latch"), "repaired sound source saved");
            await Load("yard-quiet-hook-latch", false);
            await ObserveWindCue(upper, solved: true, "latch-after-load");
            await Capture("06_hooked_pole_and_shutter");
            await Approach(side);
            await Press("interact");
            Require(side.Solved, "pole retains its second practical use");
            var parkedLampTransform = Item("carry-lantern").GlobalTransform;
            var parkedLampCustody = CustodyOwner("carry-lantern");
            var hookRest = Mechanism("hook-rest");
            await Approach(hookRest);
            var hookRestProbe = _carry.DescribeRestProbe(Item("carry-hook"), hookRest.RestPoint, 0);
            GD.Print("act1-yard-mechanisms: " + hookRestProbe);
            await Press("interact");
            Require(!Item("carry-tool-pole").HasHook && Item("carry-hook").State == CarryableProp.CarryState.Placed
                && CustodyOwner("carry-hook") == "world", "hook detaches back to a supported shelf without consuming it; " + hookRestProbe);
            Require(Item("carry-lantern").GlobalTransform.IsEqualApprox(parkedLampTransform)
                && CustodyOwner("carry-lantern") == parkedLampCustody && Item("carry-lantern").CollisionLayer != 0,
                "the dedicated hook rest remains accessible beside the unchanged physical parked lamp");
            await Load("yard-hooked-pole", false);
            Require(Item("carry-tool-pole").HasHook && Item("carry-hook").IsConcealed && !upper.Solved,
                "combined save restores exact assembly, not later shutter state");

            await Load("yard-mechanisms-pristine", false);
            var handApproach = side.AlternativeApproach!.Value;
            var handCorner = handApproach + Vector3.Right * .77f;
            // The northern fixture intersected the existing pole's raised end.
            // Approach from the open southern aisle, leaving the tool in place.
            var handStart = handCorner + Vector3.Forward * 1.6f;
            var untouchedPolePose = Item("carry-tool-pole").GlobalTransform;
            await BeginLocalWalk(handStart);
            await WalkTo(handCorner, "walk around the work stand without crossing it");
            await WalkTo(handApproach, "walk around the workshop to the side-panel hand access");
            Aim(side.GlobalPosition);
            await Frames(3);
            await Press("interact");
            Require(side.Solved && _carry.HeldItem is null, "walking around gives the genuine bare-hand alternative");
            await WalkTo(handCorner, "leave the hand access between the crossbars");
            await WalkTo(handStart, "return from the hand access");
            Require(Item("carry-tool-pole").GlobalTransform.IsEqualApprox(untouchedPolePose)
                && CustodyOwner("carry-tool-pole") == "world",
                "the alternate hand route returns without moving or using the pole");
            var footboard = Mechanism("loose-footboard");
            await CheckUnderdeckSourceAndRepair(footboard);
            Require(await _bridge.SaveSlotAsync("yard-quiet-corner"), "quiet corner saved");
            await Load("yard-mechanisms-pristine", false);
            Require(!side.Solved && !footboard.Solved, "earlier save restores sound causes");
            await Load("yard-quiet-corner", false);
            Require(side.Solved && footboard.Solved && !upper.Solved
                && Knows("discovery-underdeck-rattle") && Knows("clue_underdeck_rattle_quiet"),
                "return restores the inspected and repaired board without another sound source's repair");
            await CheckQuietFootboardReturn(footboard);
            await Act1ArrivalFlowProof.CompleteAsync(this, _bridge);
            Require(await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/arrival-enter-house"),
                "the completed ordinary arrival permits reporting the repaired board inside the house");
            _demo.DemoMain.SwitchZone("house_old_pc", "entry");
            _inside = true;
            await Frames(8);
            if (!await Act1LocalReactionProof.CompleteAsync(this, _bridge, "underdeck",
                interactPhysically: InteractWithSourceHolder)) return;
            }

            await Load("yard-mechanisms-pristine", false);
            await Take("carry-board");
            var rest = Mechanism("board-rest");
            var occupied = rest.RestPoint;
            occupied.Y = AgentBAct1HeightField.CollisionGround(occupied.X, occupied.Z) + .04f;
            await Approach(rest, occupied);
            var beforeOccupied = _bridge.SelectWorldProps().GetRawText();
            RecordBoardRestProbe("occupied-before-attempt", rest);
            await Press("interact");
            Require(!rest.Solved && _carry.HeldItem == Item("carry-board")
                && beforeOccupied == _bridge.SelectWorldProps().GetRawText(),
                "board assembly cannot be placed through the player's body between the trestles");
            await Approach(rest);
            RecordBoardRestProbe("free-before-attempt", rest);
            await Press("interact");
            Require(rest.Solved && _carry.HeldItem is null && Item("carry-board").State == CarryableProp.CarryState.Combined,
                "same board rests physically on both trestles");
            await Capture("07_supported_board");
            await CheckBoardUseAndStep(rest);
            await Take("carry-board");
            Require(!rest.Solved && Item("carry-board").AssemblyKey.Length == 0, "unoccupied board removes the assembly atomically");
            await Load("yard-mechanisms-pristine", false);
            Require(_carry.Mechanisms.All(item => !item.Solved) && Item("carry-cloth").IsWarm == false
                && Item("carry-tool-pole").HasHook == false, "all local consequences reset on pristine restore");
            if (!boardOnly)
            {
            await CheckFixedLadder();
            Require(await _bridge.StartNewGameAsync(), "new kernel resets yard mechanics");
            await Frames(8);
            Require(_carry.Mechanisms.All(item => !item.Solved) && _carry.HeldItem is null, "New Game re-registers clean ownership");
            }
            exit = 0;
            GD.Print($"act1-yard-mechanisms: PASS {_checks} local checks; human/art/listening/duration not measured");
        }
        catch (Exception error)
        {
            GD.PrintErr($"act1-yard-mechanisms: FAIL {error}\n{_carry?.DescribeAim()}");
            _receipt.Add(new { kind = "failure", error = error.ToString(), aim = _carry?.DescribeAim() });
            if (_camera is not null)
            {
                try { await Capture("failure"); }
                catch (Exception captureError)
                {
                    _receipt.Add(new { kind = "capture-not-run", reason = captureError.Message });
                    GD.PrintErr("act1-yard-mechanisms: failure capture not-run: " + captureError.Message);
                }
            }
        }
        finally
        {
            foreach (var action in new[] { "interact", "carry_use", "carry_place", "carry_rotate", "crouch",
                         "move_forward", "move_backward", "pause" }) Input.ActionRelease(action);
            try
            {
                var directory = EvidenceDirectory();
                Directory.CreateDirectory(directory);
                using var receipt = new FileStream(Path.Combine(directory, "mechanisms-receipt.json"),
                    FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(receipt, new
                { passed = exit == 0, checks = _checks, fixture = "ordinary NewGame plus recorded supported local camera fixtures",
                    humanPlaytime = "external/not-run", events = _receipt }, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception evidenceError)
            {
                exit = 1;
                GD.PrintErr($"act1-yard-mechanisms: cannot create new evidence without overwriting: {evidenceError.Message}");
            }
            if (_demo is not null) await GodotSmokeCleanup.ReleaseAsync(_demo);
        }
        GetTree().Quit(exit);
    }

    private CarryableProp Item(string id) => _carry.Items.Single(item => item.ItemId == id);

    private void RecordBoardRestProbe(string attempt, YardMechanism rest)
    {
        var probe = _carry.DescribeRestProbe(Item("carry-board"), rest.RestPoint, 0);
        GD.Print($"act1-yard-board-rest: {attempt}; {probe}");
        _receipt.Add(new { kind = "board-rest-physical-probe", attempt,
            playerFeet = _player.GlobalPosition.ToString(), desiredRest = rest.RestPoint.ToString(), probe });
    }

    private async Task CaptureCabinetPromptAtBothResolutions(YardMechanism card)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_MECHANISM_CAPTURE") != "1") return;
        var originalSize = DisplayServer.WindowGetSize();
        var originalLook = _player.CapturePortableTransform().RotationDegrees;
        var prompt = _player.GetNode<Label>("Hud/InteractionPrompt");
        var route = _demo.GetNode<Label>("Act1DemoRouteCue/RoutePrompt");
        try
        {
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                DisplayServer.WindowSetSize(size);
                await Frames(8);
                Aim(card.GlobalPosition);
                await Frames(3);
                var rect = prompt.GetGlobalRect();
                var viewport = prompt.GetViewportRect().Size;
                Require(DisplayServer.WindowGetSize() == size && rect.Position.X >= 0 && rect.Position.Y >= 0
                    && rect.End.X <= viewport.X && rect.End.Y <= viewport.Y - 24f,
                    $"the full action label stays above the bottom margin at {size}: {rect}, viewport {viewport}");
                Require(prompt.Text.Contains(card.ResultText, StringComparison.Ordinal)
                    && prompt.Text.Contains(InputBindingService.ActionHint("carry_use",
                        _player.CurrentInputDevice == "gamepad"), StringComparison.Ordinal),
                    "wrapped cabinet result retains the complete text and lamp button");
                Require(string.IsNullOrEmpty(route.Text) || !rect.Intersects(route.GetGlobalRect()),
                    "the action label and ordinary objective occupy separate screen areas");
                _receipt.Add(new { kind = "actual-hud-layout", window = size.ToString(),
                    viewport = viewport.ToString(), bounds = rect.ToString(), text = prompt.Text,
                    humanReadability = "external/not-run" });
                await Capture(size.Y == 720 ? "05_cabinet_open_and_readable" : "05_cabinet_open_and_readable_1080");
            }
        }
        finally
        {
            DisplayServer.WindowSetSize(originalSize);
            _player.ApplySmokeLook((float)originalLook.X, (float)originalLook.Y);
            await Frames(6);
        }
    }

    private bool Knows(string id) => _bridge.SelectRuntimeState().GetProperty("knowledge")
        .GetProperty("urman.chapter1:knowledge/" + id).GetProperty("status").GetString() == "confirmed";

    private async Task ObserveWindCue(YardMechanism mechanism, bool solved, string phase)
    {
        Require(mechanism.CueCause == YardMechanism.SoundCause.Wind && mechanism.Solved == solved
            && mechanism.MovingPart?.IsVisibleInTree() == true && AimedAt(mechanism),
            phase + ": actual visible wind source and ray are available");
        var count = mechanism.EmittedCueCount;
        var rotation = mechanism.MovingPart!.Rotation;
        var knowledge = _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var scene = _bridge.ActiveSceneId;
        var zone = _bridge.CurrentZoneId;
        var started = Time.GetTicksMsec();
        var pulse = false;
        var caption = false;
        var nativePlayback = false;
        var playheads = new Dictionary<ulong, double>();
        var playbackEvidence = string.Empty;
        var existingPlayers = mechanism.GetChildren().OfType<WorldFoleyPlayer>().Select(player => player.GetInstanceId()).ToHashSet();
        using var cancellation = new System.Threading.CancellationTokenSource();
        var watchdog = Task.Delay(10000, cancellation.Token);
        try
        {
            while (Time.GetTicksMsec() - started < (solved ? 7600u : 9000u))
            {
                var next = NextProcessFrame();
                if (await Task.WhenAny(next, watchdog) != next || watchdog.IsCompleted)
                    throw new TimeoutException("No actual process frame while observing " + phase);
                await next;
                if (_player.ModalOpen || _bridge.CurrentZoneId != zone || !mechanism.MovingPart.IsVisibleInTree()
                    || mechanism.CollisionLayer != 4u || _player.GlobalPosition.DistanceSquaredTo(mechanism.GlobalPosition) > 121f)
                    throw new InvalidOperationException(phase + ": the real cue observation lost its active nearby gameplay context");
                var prompt = _player.GetNode<Label>("Hud/InteractionPrompt");
                caption |= prompt.IsVisibleInTree() && prompt.Text.Contains(
                    solved ? mechanism.ResultText : mechanism.SoundCaption, StringComparison.Ordinal);
                pulse |= mechanism.MovingPart.Rotation.DistanceTo(rotation) > .0001f;
                nativePlayback |= ObserveNativeCue(mechanism, existingPlayers, playheads, out var playback);
                if (!string.IsNullOrEmpty(playback)) playbackEvidence = playback;
                if (solved) RequireCueUnchanged();
                else if (mechanism.EmittedCueCount > count && nativePlayback && caption && (pulse || _player.ReducedMotion)) break;
            }
        }
        finally { cancellation.Cancel(); }
        _receipt.Add(new { kind = "sound-cause", phase, mechanism = mechanism.StateKey,
            sample = mechanism.SoundSample, before = count, after = mechanism.EmittedCueCount,
            elapsedMsec = Time.GetTicksMsec() - started, solved, caption, pulse, nativePlayback, playbackEvidence,
            observedNewNativePlayers = playheads.Count, reducedMotion = _player.ReducedMotion, listening = "external/not-run" });
        Require(Time.GetTicksMsec() - started < 10000 && caption && mechanism.Solved == solved
            && (solved ? mechanism.EmittedCueCount == count
                : mechanism.EmittedCueCount > count && nativePlayback && (pulse || _player.ReducedMotion))
            && _bridge.ActiveSceneId == scene
            && knowledge == _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText(),
            phase + ": native cue/visible equivalent respect the actual unsolved or repaired cause, without knowledge");
        await Capture("sound_" + phase);
        return;

        async Task NextProcessFrame() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        void RequireCueUnchanged()
        {
            if (mechanism.EmittedCueCount != count || mechanism.MovingPart.Rotation.DistanceTo(rotation) > .0001f
                || mechanism.GetChildren().OfType<WorldFoleyPlayer>().Any(player => !existingPlayers.Contains(player.GetInstanceId())))
                throw new InvalidOperationException(phase + ": a repaired source emitted again or visibly rattled");
        }
    }

    private static bool ObserveNativeCue(YardMechanism mechanism, HashSet<ulong> existingPlayers,
        Dictionary<ulong, double> playheads, out string evidence)
    {
        evidence = string.Empty;
        var advanced = false;
        foreach (var player in mechanism.GetChildren().OfType<WorldFoleyPlayer>())
        {
            if (existingPlayers.Contains(player.GetInstanceId()) || player.IsQueuedForDeletion() || player.Stream is not { } stream
                || stream.ResourcePath != "res://assets/audio/act1/foley/" + mechanism.SoundSample + ".wav"
                || !player.Playing || !player.HasStreamPlayback() || player.StreamPaused || player.Bus != AudioSettingsService.SfxBus
                || player.GlobalPosition.DistanceTo(mechanism.GlobalPosition) >= .08f) continue;
            var id = player.GetInstanceId();
            var now = player.GetPlaybackPosition();
            if (!playheads.TryGetValue(id, out var first)) playheads[id] = first = now;
            if (now <= first + .001) continue;
            advanced = true;
            evidence = $"id={id}; player={player.GetPath()}; stream={stream.ResourcePath}; playing={player.Playing}; native={player.HasStreamPlayback()}; from={first}; to={now}; source={player.GlobalPosition}";
        }
        return advanced;
    }

    private async Task WalkAcrossFootboard(YardMechanism board, Node3D shed, Vector3 inspection,
        bool shouldEmit, string phase)
    {
        Require(board.CueCause == YardMechanism.SoundCause.FootContact && board.Solved != shouldEmit
            && board.MovingPart?.IsVisibleInTree() == true && _player.IsCrouching,
            phase + ": the actual board has its expected cause and crouching approach");
        var count = board.EmittedCueCount;
        var rotation = board.MovingPart!.Rotation;
        var knowledge = _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var pulse = false;
        var contact = false;
        var nativePlayback = false;
        var playheads = new Dictionary<ulong, double>();
        var playbackEvidence = string.Empty;
        var existingPlayers = board.GetChildren().OfType<WorldFoleyPlayer>().Select(player => player.GetInstanceId()).ToHashSet();
        var centre = shed.ToLocal(board.MovingPart.GlobalPosition);
        var far = shed.ToGlobal(centre + new Vector3(0, 0, -.60f));
        await WalkTo(far, phase + ": step across the real hollow board", ObserveContact);
        await WalkTo(inspection, phase + ": return across the same board to the inspection view", ObserveContact);
        _receipt.Add(new { kind = "foot-contact-sound", phase, before = count, after = board.EmittedCueCount,
            contact, pulse, nativePlayback, playbackEvidence, observedNewNativePlayers = playheads.Count,
            shouldEmit, physicalOwner = board.MovingPart.GetPath().ToString(), listening = "external/not-run" });
        Require(contact && (shouldEmit ? board.EmittedCueCount > count : board.EmittedCueCount == count)
            && (shouldEmit ? nativePlayback && (pulse || _player.ReducedMotion) : !pulse)
            && knowledge == _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText(),
            phase + ": moving feet contacted the board; cue and visible pulse follow its repaired state");
        return;

        void ObserveContact()
        {
            pulse |= board.MovingPart.Rotation.DistanceTo(rotation) > .0001f;
            nativePlayback |= ObserveNativeCue(board, existingPlayers, playheads, out var playback);
            if (!string.IsNullOrEmpty(playback)) playbackEvidence = playback;
            if (!shouldEmit && board.GetChildren().OfType<WorldFoleyPlayer>().Any(player => !existingPlayers.Contains(player.GetInstanceId())))
                throw new InvalidOperationException(phase + ": a repaired board created a new sound player");
            for (var index = 0; index < _player.GetSlideCollisionCount(); index++)
            {
                var hit = _player.GetSlideCollision(index);
                contact |= hit.GetNormal().Y > .5f && hit.GetCollider() is Node body
                    && board.MovingPart.IsAncestorOf(body)
                    && new Vector2(_player.Velocity.X, _player.Velocity.Z).LengthSquared() > .04f;
            }
        }
    }

    private async Task CheckQuietFootboardReturn(YardMechanism footboard)
    {
        var shed = (Node3D)_demo.DemoMain.FindChild("BabaiYardAuthoredShed", true, false);
        var outside = shed.ToGlobal(new(-1.15f, 0, 3.18f));
        var entrance = shed.ToGlobal(new(-1.15f, .075f, 1.40f));
        var inspection = shed.ToGlobal(new(-1.15f, .075f, .30f));
        Require(_player.GlobalPosition.DistanceTo(outside) < .25f,
            "quiet-source save restores the actually reached outdoor return position");
        await Press("crouch");
        await WalkTo(entrance, "re-enter the small space after loading the repaired source");
        await WalkTo(inspection, "return to the same repaired board after loading");
        await WalkAcrossFootboard(footboard, shed, inspection, shouldEmit: false, "repaired-load");
        Aim(footboard.GlobalPosition);
        await Frames(3);
        Require(AimedAt(footboard), "loaded quiet board remains an actual aimed object");
        await Press("interact");
        Require(_bridge.JournalEntries().Count(entry => entry.EntryId
            == "urman.chapter1:knowledge/clue_underdeck_rattle_quiet") == 1,
            "rechecking the quiet board after load cannot duplicate its result");
        await WalkTo(entrance, "leave the loaded repaired board");
        await WalkTo(outside, "return to the yard after the quiet-source recheck");
        await Press("crouch");
    }

    private async Task CheckUnderdeckSourceAndRepair(YardMechanism footboard)
    {
        const string observe = "urman.chapter1:interaction/discover-underdeck-rattle";
        const string mark = "urman.chapter1:interaction/mark-underdeck-quiet";
        var shed = _demo.DemoMain.FindChild("BabaiYardAuthoredShed", true, false) as Node3D
            ?? throw new InvalidOperationException("The loose board has no actual loft shed.");
        var discovery = _demo.DemoMain.FindChildren("*", "", true, false).OfType<InteractionTarget>()
            .Single(target => target.InteractionId == observe);
        var journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        var outside = shed.ToGlobal(new(-1.15f, 0, 3.18f));
        var entrance = shed.ToGlobal(new(-1.15f, .075f, 1.40f));
        var inspection = shed.ToGlobal(new(-1.15f, .075f, .30f));
        // The start outside the shed is the only local reproduction fixture.
        // Entry, inspection and return use the existing physical passage and input.
        await BeginLocalWalk(outside);
        Require(!footboard.Solved && !Knows("discovery-underdeck-rattle")
            && !Knows("clue_underdeck_rattle_quiet") && !discovery.IsAvailable()
            && !await _bridge.DispatchInteractionAsync(observe),
            "standing outside cannot inspect the concealed underdeck source");
        await Press("crouch");
        Require(_player.IsCrouching && _carry.HeldItem is null, "ordinary crouch prepares the small passage with free hands");
        await WalkTo(entrance, "crouch through the supported front underdeck entrance");
        await WalkTo(inspection, "approach the rattling board within the real small space");
        await WalkAcrossFootboard(footboard, shed, inspection, shouldEmit: true, "before-repair");
        Aim(discovery.GlobalPosition);
        await Frames(3);
        Require(_player.IsOnFloor() && discovery.IsAvailable() && AimedAt(discovery),
            "the reached crouching view aims at the actual underdeck observation target");
        await Press("interact");
        for (var frame = 0; frame < 180 && !journal.GetNode<Control>("Screen").Visible; frame++) await Frames(1);
        Require(Knows("discovery-underdeck-rattle") && !footboard.Solved
            && !Knows("clue_underdeck_rattle_quiet") && _player.ModalOpen
            && journal.GetNode<Control>("Screen").Visible && journal.ActiveEntryId == discovery.JournalEntryId,
            "finding the sound opens its actual journal source without repairing the board");
        // The default gamepad accept button is also the world interact action.
        // Hold that action across the real source close: the world must wait
        // for release and a new press, even if the one-shot source disappears.
        var pressesBeforeClose = _carry.InteractionPresses;
        Input.ActionPress("interact");
        try
        {
            journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            await Frames(3);
            Require(!_player.ModalOpen && !footboard.Solved
                && _carry.InteractionPresses == pressesBeforeClose,
                "a held accept/interact across source close cannot repair the board; " + _carry.DescribeAim());
        }
        finally { Input.ActionRelease("interact"); }
        await Frames(2);
        GD.Print("underdeck-after-source-close: solved=" + footboard.Solved
            + " worldFlag=" + YardMechanism.Flag(_bridge.SelectWorldProps(), "yard/loose-footboard")
            + " semantic=" + _bridge.IsInteractionAvailable(mark)
            + " physical=" + _bridge.CanPhysicallyUseInteraction(mark)
            + " quietKnown=" + Knows("clue_underdeck_rattle_quiet")
            + " interactionPresses=" + _carry.InteractionPresses + "/" + pressesBeforeClose);
        Require(!_player.ModalOpen && !footboard.Solved && !_bridge.IsInteractionAvailable(mark)
            && !_bridge.CanPhysicallyUseInteraction(mark) && !await _bridge.DispatchInteractionAsync(mark)
            && !Knows("clue_underdeck_rattle_quiet"),
            "released source controls leave the unrepaired board and quiet observation unchanged; " + _carry.DescribeAim());
        Require(await _bridge.SaveSlotAsync("yard-underdeck-observed"), "an observed but unrepaired source can be saved");
        await WalkTo(entrance, "leave the unrepaired underdeck source");
        await WalkTo(outside, "return to open ground from the inspected source");
        await Load("yard-underdeck-observed", false);
        Require(_player.IsCrouching && _player.IsOnFloor() && Knows("discovery-underdeck-rattle")
            && !footboard.Solved && !Knows("clue_underdeck_rattle_quiet"),
            "returning to the reached saved pose preserves the unfinished physical repair");
        await WalkAcrossFootboard(footboard, shed, inspection, shouldEmit: true, "unfinished-load");
        Aim(footboard.GlobalPosition);
        await Frames(3);
        Require(AimedAt(footboard), "the one-shot source exposes the actual loose board beneath it");
        Require(_player.GetNode<Label>("Hud/InteractionPrompt").Text.Contains(footboard.SoundCaption, StringComparison.Ordinal),
            "the aimed hollow-board caption remains usable without hearing its sound");
        await Press("interact");
        Require(footboard.Solved && Knows("clue_underdeck_rattle_quiet")
            && _bridge.JournalEntries().Count(entry => entry.EntryId
                == "urman.chapter1:knowledge/clue_underdeck_rattle_quiet") == 1,
            "the physical repair and local recheck record the quiet consequence exactly once");
        await WalkAcrossFootboard(footboard, shed, inspection, shouldEmit: false, "after-repair");
        await Capture("06b_underdeck_repaired");
        await WalkTo(entrance, "leave the repaired board without crossing its geometry");
        await WalkTo(outside, "return from the repaired underdeck to the yard");
        await Press("crouch");
        Require(!_player.IsCrouching && _player.IsOnFloor(), "the open approach allows the player to stand again");
    }

    private async Task<bool> InteractWithSourceHolder(string action)
    {
        var target = _demo.DemoMain.FindChildren("*", "", true, false).OfType<InteractionTarget>()
            .Single(candidate => candidate.InteractionId == "urman.chapter1:interaction/" + action
                && candidate.IsAvailable());
        await Approach(target);
        Require(AimedAt(target), "the local source-holder fixture uses the actual aimed NPC interaction");
        await Press("interact");
        return true;
    }

    private async Task CheckBoardUseAndStep(YardMechanism rest)
    {
        var board = Item("carry-board");
        var lamp = Item("carry-lantern");
        var upper = Mechanism("upper-latch");
        await Take(lamp.ItemId);
        await Press("carry_use");
        Require(lamp.LightOn, "the lamp is lit before placing it on the work board");
        await Approach(board);
        Aim(board.GlobalPosition + Vector3.Up * board.Height - board.GlobalBasis.Z * .45f);
        await Frames(3);
        Require(_carry.TryPlacement(lamp, out var lampPose, out var reason), "board supports actual lamp placement: " + reason);
        await Press("carry_place");
        Require(_carry.HeldItem is null && lamp.GlobalPosition.DistanceTo(lampPose) < .03f
            && Math.Abs(lamp.GlobalPosition.Y - board.GlobalPosition.Y - board.Height) < .025f,
            "assembled board holds the lit lamp and frees the player's hands");
        await Capture("07a_board_holds_lamp");
        await Approach(board);
        var beforeLampRemoval = _bridge.SelectWorldProps().GetRawText();
        await Press("interact");
        Require(_carry.HeldItem is null && rest.Solved && board.State == CarryableProp.CarryState.Combined
            && beforeLampRemoval == _bridge.SelectWorldProps().GetRawText(),
            "the board cannot be removed from under its lamp");
        Require(await _bridge.SaveSlotAsync("yard-board-with-lamp"), "board, light and assembly saved together");
        await Load("yard-board-with-lamp", false);
        Require(rest.Solved && lamp.LightOn && lamp.GlobalPosition.DistanceTo(lampPose) < .03f
            && _carry.IsSupportingSomething(board), "loaded board retains its lit supported object");
        await Take(lamp.ItemId);
        await PutNear(new(-27.1f, 0, 5.9f));

        // Only this starting ground pose is a local reproduction fixture. Every
        // ascent, step on the board and return below uses ordinary walking input.
        var approach = rest.RestPoint + Vector3.Back * 2.75f;
        await BeginLocalWalk(approach);
        var front = board.GlobalPosition + Vector3.Back * .50f + Vector3.Up * board.Height;
        var working = board.GlobalPosition + Vector3.Forward * .42f + Vector3.Up * board.Height;
        await WalkTo(front, "walk up the visible gangway onto the assembled board");
        await WalkTo(working, "walk along the board to the upper latch");
        await Frames(5);
        Require(_player.IsOnFloor() && Math.Abs(_player.GlobalPosition.Y - working.Y) < .065f
            && _carry.IsSupportingSomething(board), "ordinary ascent ends standing on the real board collider");
        Aim(board.GlobalPosition + Vector3.Forward * .78f + Vector3.Up * board.Height * .5f);
        await Frames(3);
        var beforePlayerRemoval = _bridge.SelectWorldProps().GetRawText();
        await Press("interact");
        Require(_carry.HeldItem is null && rest.Solved && board.State == CarryableProp.CarryState.Combined
            && beforePlayerRemoval == _bridge.SelectWorldProps().GetRawText(),
            "the player cannot pull the board out from under their own feet");
        Require(!upper.Solved, "the plain-hand branch starts with the same unsolved latch");
        Aim(upper.GlobalPosition);
        await Frames(3);
        await Press("interact");
        Require(upper.Solved && _carry.HeldItem is null && _player.IsOnFloor(),
            "the reached stable work stand gives the real bare-hand alternative to the hooked pole");
        await Capture("07b_board_upper_latch_alternative");
        Require(await _bridge.SaveSlotAsync("yard-board-player-above"), "supported player and repaired latch saved");
        await WalkTo(front, "return along the board");
        await WalkTo(approach, "walk down the visible gangway to the yard");
        await Frames(5);
        Require(_player.IsOnFloor() && Math.Abs(_player.GlobalPosition.Y
            - AgentBAct1HeightField.CollisionGround(approach.X, approach.Z)) < .10f,
            "return from the work stand reaches ordinary ground");
        await Load("yard-board-with-lamp", false);
        Require(!upper.Solved, "earlier board save removes the later hand repair");
        await Load("yard-board-player-above", false);
        Require(upper.Solved && rest.Solved && _player.IsOnFloor() && _carry.IsSupportingSomething(board),
            "save above the yard restores the same real support and consequence");
        await WalkTo(front, "loaded player returns along the board");
        await WalkTo(approach, "loaded player walks down to the yard");
    }

    private async Task BeginLocalWalk(Vector3 point)
    {
        point.Y = AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .06f;
        var clear = _player.CanStandAt(point);
        if (!clear) GD.Print("act1-yard-mechanisms: refused walk fixture " + DescribeStandingClearance(point));
        Require(clear, $"standing clearance at the local walk start {point}");
        _player.ApplyZoneSpawn(point, 0);
        await Frames(6);
        _receipt.Add(new { kind = "local-walk-start-fixture", feet = _player.GlobalPosition.ToString(),
            isTraversalMeasurement = false });
    }

    private string DescribeStandingClearance(Vector3 point)
    {
        // This is a failure-only view of CanStandAt's actual capsule query.
        // It neither chooses a different fixture nor changes any body or mask.
        var capsule = (CapsuleShape3D)_player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        using var shape = new CapsuleShape3D { Radius = capsule.Radius, Height = _standingBodyHeight - .015f };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape, CollisionMask = _player.CollisionMask, Margin = .002f,
            Transform = new(Basis.Identity, point + Vector3.Up * (_standingBodyHeight * .5f + .01f)),
            Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() }
        };
        var space = _player.GetWorld3D().DirectSpaceState;
        var contacts = space.IntersectShape(query, 16).Select(hit =>
        {
            var body = hit["collider"].AsGodotObject() as CollisionObject3D;
            var owner = body?.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
            return $"{body?.GetPath()} / {owner?.GetPath()}";
        });
        using var ray = PhysicsRayQueryParameters3D.Create(point + Vector3.Up * .20f,
            point + Vector3.Down * 2f, _player.CollisionMask,
            new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var support = space.IntersectRay(ray);
        var underFeet = support.Count == 0 ? "none" : $"{(support["collider"].AsGodotObject() as Node)?.GetPath()}"
            + $" at {support["position"].AsVector3()} normal {support["normal"].AsVector3()}";
        return $"feet={point} height={_standingBodyHeight} radius={capsule.Radius} "
            + $"mask={_player.CollisionMask} contacts=[{string.Join("; ", contacts)}] underFeet={underFeet}";
    }

    private async Task BackAwayWithLamp()
    {
        var start = _player.GlobalPosition;
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        var clamps = _player.EdgeClamps;
        try
        {
            Input.ActionPress("move_backward");
            for (var frame = 0; frame < 60
                && new Vector2(_player.GlobalPosition.X - start.X, _player.GlobalPosition.Z - start.Z).Length() < .45f; frame++)
                await Frames(1);
        }
        finally { Input.ActionRelease("move_backward"); }
        await Frames(5);
        var travelled = new Vector2(_player.GlobalPosition.X - start.X, _player.GlobalPosition.Z - start.Z).Length();
        Require(travelled >= .40f && travelled < .75f && _player.IsOnFloor()
            && _player.PresentationTransformRevision == revision && _player.FallRecoveries == recoveries
            && _player.EdgeClamps == clamps,
            $"ordinary backward input gives the held lamp clearance, without relocation or recovery: {start} -> {_player.GlobalPosition}");
    }

    private async Task WalkTo(Vector3 point, string label, Action? observe = null)
    {
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        var clamps = _player.EdgeClamps;
        var start = _player.GlobalPosition;
        var arrived = false;
        try
        {
            for (var frame = 0; frame < 420; frame++)
            {
                var delta = point - _player.GlobalPosition;
                if (new Vector2(delta.X, delta.Z).Length() < .09f) { arrived = true; break; }
                _player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
                Input.ActionPress("move_forward");
                await Frames(1);
                observe?.Invoke();
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        await Frames(4);
        observe?.Invoke();
        Require(arrived && _player.PresentationTransformRevision == revision
            && _player.FallRecoveries == recoveries && _player.EdgeClamps == clamps,
            label + $" by ordinary input, without placement or recovery; at {_player.GlobalPosition}, expected {point}");
        _receipt.Add(new { kind = "physical-local-walk", label, from = start.ToString(),
            to = _player.GlobalPosition.ToString(), playerTeleports = 0, isHumanPlaytime = false });
    }

    private async Task CheckFixedLadder()
    {
        var ladder = GetTree().GetNodesInGroup(LadderTraversal3D.GroupName).OfType<LadderTraversal3D>().Single();
        var lower = ladder.ToGlobal(ladder.LowerLanding);
        var upper = ladder.ToGlobal(ladder.UpperLanding);
        await Take("carry-log");
        await LadderApproach(ladder, fromTop: false);
        await Press("interact");
        Require(!_player.IsClimbingLadder && _carry.HeldItem == Item("carry-log"),
            "ladder refuses occupied hands without dropping or duplicating the carried thing");
        await Load("yard-mechanisms-pristine", false);
        await LadderApproach(ladder, fromTop: false);
        var blockedLanding = new StaticBody3D { Name = "OccupiedLadderLandingFixture", CollisionLayer = 2u,
            CollisionMask = 0u, Position = upper + Vector3.Up * .40f };
        blockedLanding.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(.80f, .80f, .80f) } });
        AddChild(blockedLanding);
        try
        {
            await Frames(3);
            await Press("interact");
            Require(!_player.IsClimbingLadder && _player.GlobalPosition.DistanceTo(lower) < .15f,
                "occupied upper landing refuses entry without moving the player");
        }
        finally { blockedLanding.QueueFree(); await Frames(3); }

        await Press("interact");
        Require(_player.IsClimbingLadder, "ordinary interact takes the actual fixed ladder");
        Input.ActionPress("move_forward");
        await Frames(35);
        Input.ActionRelease("move_forward");
        await Frames(2);
        Require(_player.IsClimbingLadder && ladder.DistanceTravelled > .20f,
            "forward input physically advances the capsule up the ladder");
        var pausePoint = _player.GlobalPosition;
        _player.SetModalOpen(true);
        await Frames(5);
            Require(_player.GlobalPosition.DistanceTo(pausePoint) < .005f, "modal suspends ladder motion");
            EnsureNoPauseShell("release the intentional ladder modal fixture");
            _player.SetModalOpen(false);
        Require(!await _bridge.SaveSlotAsync("yard-ladder-denied"), "mid-ladder save is explicitly refused");
        await Press("pause");
        await WaitForLadderExit();
        Require(_player.GlobalPosition.DistanceTo(lower) < .15f,
            "cancel physically returns to the nearby supported landing");

        await LadderApproach(ladder, fromTop: false);
        await Press("interact");
        Require(_player.IsClimbingLadder, "returned ladder can be entered again");
        Input.ActionPress("move_forward");
        await WaitForLadderExit();
        Input.ActionRelease("move_forward");
        await Frames(4);
        Require(_player.GlobalPosition.DistanceTo(upper) < .15f && _player.CanStandAt(_player.GlobalPosition),
            "complete ascent exits onto the real upper floor with head clearance");
        await Capture("08_loft_landing");
        var roofline = _demo.DemoMain.FindChildren("*", "", true, false).OfType<InteractionTarget>()
            .Single(target => target.InteractionId == "urman.chapter1:interaction/discover-shed-loft-roofline");
        var sceneBeforeRoof = _bridge.ActiveSceneId;
        var roofView = roofline.GetMeta("accessAnchor").AsVector3();
        Require(!Knows("discovery-shed-loft-roofline"), "earned ascent alone does not inspect the roof repair");
        await WalkTo(roofView, "walk from the earned loft landing to the bolted roof repair");
        Aim(roofline.GlobalPosition);
        await Frames(3);
        Require(roofline.IsAvailable() && AimedAt(roofline) && _player.CanStandAt(_player.GlobalPosition),
            "earned standing loft view reaches the actual roof repair ray");
        await Press("interact");
        var roofJournal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        for (var frame = 0; frame < 180 && !roofJournal.GetNode<Control>("Screen").Visible; frame++) await Frames(1);
        Require(Knows("discovery-shed-loft-roofline") && roofJournal.ActiveEntryId == roofline.JournalEntryId
            && roofJournal.GetNode<Control>("Screen").Visible && _bridge.ActiveSceneId == sceneBeforeRoof
            && _bridge.JournalEntries().Count(entry => entry.EntryId == roofline.JournalEntryId) == 1,
            "manual roof inspection gives its visible source once without advancing the investigation");
        await Capture("08a_loft_roof_repair_source");
        roofJournal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        await WalkTo(upper, "return from the roof repair to the earned upper ladder landing");
        Require(await _bridge.SaveSlotAsync("yard-ladder-upper"), "supported upper landing can be saved");
        await Load("yard-mechanisms-pristine", false);
        await Load("yard-ladder-upper", false);
        Require(!_player.IsClimbingLadder && _player.GlobalPosition.DistanceTo(upper) < .15f
            && Knows("discovery-shed-loft-roofline") && !roofline.IsAvailable()
            && _bridge.JournalEntries().Count(entry => entry.EntryId == roofline.JournalEntryId) == 1,
            "upper save restores a supported ordinary player, not an orphaned traversal");
        Aim(ladder.ToGlobal(ladder.UpperGrip) + Vector3.Up * .22f);
        await Frames(3);
        Require(ladder.CanOffer(_player), "loaded earned upper landing offers the real ladder without a position fixture");
        await Press("interact");
        Require(_player.IsClimbingLadder, "upper landing offers the return descent");
        Input.ActionPress("move_backward");
        await WaitForLadderExit();
        Input.ActionRelease("move_backward");
        await Frames(4);
        Require(_player.GlobalPosition.DistanceTo(lower) < .15f,
            "backward input descends the same physical route to the lower landing");
    }

    private async Task LadderApproach(LadderTraversal3D ladder, bool fromTop)
    {
        var at = ladder.ToGlobal(fromTop ? ladder.UpperLanding : ladder.LowerLanding);
        _player.ApplyZoneSpawn(at, 0);
        await Frames(6);
        Aim(ladder.ToGlobal(fromTop ? ladder.UpperGrip : ladder.LowerGrip)
            + Vector3.Up * (fromTop ? .22f : .85f));
        await Frames(3);
        Require(ladder.CanOffer(_player), "supported local approach to " + (fromTop ? "upper" : "lower") + " ladder landing");
    }

    private async Task WaitForLadderExit()
    {
        for (var frame = 0; frame < 600 && _player.IsClimbingLadder; frame++) await Frames(1);
        Require(!_player.IsClimbingLadder, "ladder movement finishes without a blocked or orphaned capsule");
    }

    private YardMechanism Mechanism(string id) => _carry.Mechanisms.Single(item => item.StateKey == "yard/" + id);
    private string? CustodyOwner(string id)
    {
        // The check reads the same persisted custody projection, not the view.
        var custody = _bridge.SelectRuntimeState().GetProperty("world.custody");
        return custody.EnumerateArray().Single(item => item.GetProperty("itemId").GetString() == id)
            .GetProperty("custodyOwnerId").GetString();
    }
    private async Task EstablishOrdinaryHouseExit()
    {
        const string prefix = "urman.chapter1:";
        Require(await _bridge.EnterDialogueNodeAsync(prefix + "dialogue/mansur_pc_request", "ask-for-help")
            && await _bridge.ChooseDialogueAsync(prefix + "dialogue/mansur_pc_request", "ask-for-help", "offer-help"),
            "Mansur answers the actual household request");
        await _bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
        { type = "open", documentId = "urman.oldpc:document/doc_marat_official_death_notice" }));
        Require(await _bridge.EnterDialogueNodeAsync(prefix + "dialogue/gulsina_yaramyy", "home-warning")
            && await _bridge.ChooseDialogueAsync(prefix + "dialogue/gulsina_yaramyy", "home-warning", "ask-marat")
            && await Act1FamilyMealProof.CompleteAsync(this, _bridge)
            && _bridge.IsInteractionAvailable(prefix + "interaction/house-to-route"),
            "read notice, actual family question and home pause make outdoor return attainable");
    }
    private async Task Load(string slot, bool inside)
    {
        Require(await _bridge.LoadSlotAsync(slot), "restore " + slot);
        _inside = inside;
        await Frames(8);
    }
    private async Task Take(string id)
    {
        var prop = Item(id);
        await Approach(prop);
        await Press("interact");
        Require(_carry.HeldItem == prop, "actual aimed take " + id);
    }
    private string HookAssemblyState() => $"poleHasHook={Item("carry-tool-pole").HasHook} "
        + $"hookConcealed={Item("carry-hook").IsConcealed} hookState={Item("carry-hook").State} "
        + $"poleCustody={CustodyOwner("carry-tool-pole")} hookCustody={CustodyOwner("carry-hook")}";

    private async Task CheckHookAssemblyClearance()
    {
        // This explicit physical fixture tests the enlarged tool's clearance.
        // The authored shelf approach is already clear; it must not be called
        // a negative case merely because an earlier camera fixture was tighter.
        var pole = Item("carry-tool-pole");
        var hook = Item("carry-hook");
        var before = _bridge.SelectWorldProps().GetRawText();
        var poleOwner = CustodyOwner(pole.ItemId);
        var hookOwner = CustodyOwner(hook.ItemId);
        var forward = (-_camera.GlobalBasis.Z) with { Y = 0 };
        forward = forward.Normalized();
        var right = new Vector3(-forward.Z, 0, forward.X);
        var fixture = new StaticBody3D
        {
            Name = "HookAssemblyClearanceFixture", CollisionLayer = 1u, CollisionMask = 0u,
            Transform = new(new Basis(right, Vector3.Up, -forward),
                _camera.GlobalPosition + forward * 1.325f + Vector3.Up * (pole.HoldDrop + .1175f))
        };
        var size = new Vector3(1.6f, .025f, .85f);
        fixture.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        fixture.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("cf9b42") } });
        AddChild(fixture);
        try
        {
            await Frames(5);
            bool TouchesFixture(Vector3 envelope)
            {
                using var shape = new BoxShape3D { Size = envelope };
                using var query = new PhysicsShapeQueryParameters3D
                {
                    Shape = shape, CollisionMask = 3u, Margin = .008f,
                    Transform = new(pole.GlobalBasis, pole.GlobalPosition + Vector3.Up * envelope.Y * .5f),
                    Exclude = new global::Godot.Collections.Array<Rid> { pole.GetRid(), _player.GetRid() }
                };
                return _camera.GetWorld3D().DirectSpaceState.IntersectShape(query, 32)
                    .Any(hit => hit["collider"].AsGodotObject() == fixture);
            }
            Require(_player.IsOnFloor() && _player.CanStandAt(_player.GlobalPosition)
                && _carry.HasValidHeldPose && !TouchesFixture(pole.Size)
                && TouchesFixture(new(.16f, .14f, 1.90f)),
                "visible local clearance fixture clears the player and plain pole but intersects the full hooked tool");
            Require(AimedAt(hook), "the clearance fixture leaves the actual hook interaction ray open");
            await Press("interact");
            GD.Print("act1-yard-mechanisms: blocked assembly " + HookAssemblyState()
                + " propsChanged=" + (_bridge.SelectWorldProps().GetRawText() != before));
            Require(!pole.HasHook && !hook.IsConcealed && _carry.HeldItem == pole
                && CustodyOwner(pole.ItemId) == poleOwner && CustodyOwner(hook.ItemId) == hookOwner
                && _bridge.SelectWorldProps().GetRawText() == before,
                "a physically blocked enlarged assembly preserves both parts and custody; " + HookAssemblyState());
        }
        finally
        {
            fixture.QueueFree();
            await Frames(3);
        }
    }

    private bool AimedAt(Node3D target)
    {
        using var ray = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition,
            _camera.GlobalPosition - _camera.GlobalBasis.Z * 2.7f, 7u);
        ray.Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
        var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        return hit.Count > 0 && hit["collider"].AsGodotObject() == target;
    }

    private async Task Approach(Node3D target, Vector3? requested = null)
    {
        var point = target.GlobalPosition + (target is CarryableProp prop ? Vector3.Up * prop.Height * .5f : Vector3.Zero);
        foreach (var radius in new[] { 1.05f, 1.35f, 1.65f })
        for (var i = 0; i < (requested is null ? 16 : 1); i++)
        {
            var candidate = requested ?? point + new Vector3(Mathf.Sin(i * Mathf.Tau / 16), 0, Mathf.Cos(i * Mathf.Tau / 16)) * radius;
            var floorY = _inside
                ? _demo.DemoMain.ConnectedWorld!.GetZoneInstance("house_old_pc")!.GlobalPosition.Y
                : AgentBAct1HeightField.CollisionGround(candidate.X, candidate.Z);
            var feet = new Vector3(candidate.X, requested?.Y ?? floorY + .06f, candidate.Z);
            if (!_player.CanStandAt(feet) && !_player.CanCrouchAt(feet)) continue;
            _player.ApplyZoneSpawn(feet, 0);
            await Frames(5);
            Aim(point);
            await Frames(3);
            // A local camera fixture must also fit the actual carried shape.
            // Otherwise a long tool may remain at the previous fixture while
            // the new ray alone appears to make the interaction reachable.
            if (!_carry.HasValidHeldPose) continue;
            if (!AimedAt(target)) continue;
            _receipt.Add(new { kind = "local-approach", target = target.GetPath().ToString(), feet = _player.GlobalPosition.ToString(),
                aim = _carry.DescribeAim(), isTraversalMeasurement = false });
            return;
        }
        throw new InvalidOperationException($"No ordinary supported local approach to {target.GetPath()} at {point}: {_carry.DescribeAim()}");
    }
    private void Aim(Vector3 point)
    {
        for (var iteration = 0; iteration < 6; iteration++)
        {
            var delta = point - _camera.GlobalPosition;
            var pitch = Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length()));
            var yaw = Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z));
            _player.ApplySmokeLook(pitch, yaw);
        }
    }
    private async Task PutNear(Vector3 requested)
    {
        var held = _carry.HeldItem ?? throw new InvalidOperationException("No held prop to put down.");
        foreach (var dx in new[] { 0, -.5f, .5f, -1f, 1f })
        foreach (var dz in new[] { 0, -.5f, .5f })
        for (var side = 0; side < 8; side++)
        {
            var at = requested + new Vector3(dx, 0, dz);
            at.Y = AgentBAct1HeightField.CollisionGround(at.X, at.Z) + .04f;
            var feet = at + new Vector3(Mathf.Sin(side * Mathf.Tau / 8), 0, Mathf.Cos(side * Mathf.Tau / 8)) * 1.35f;
            feet.Y = AgentBAct1HeightField.CollisionGround(feet.X, feet.Z) + .06f;
            if (!_player.CanStandAt(feet)) continue;
            _player.ApplyZoneSpawn(feet, 0);
            await Frames(3);
            Aim(at);
            await Frames(3);
            if (!_carry.TryPlacement(held, out var pose, out _)) continue;
            await Press("carry_place");
            Require(_carry.HeldItem is null && held.GlobalPosition.DistanceTo(pose) < .03f, "supported put " + held.ItemId);
            return;
        }
        throw new InvalidOperationException($"No supported rest near {requested}: {_carry.DescribeAim()}");
    }
    private async Task Press(string action)
    {
        Input.ActionPress(action); await Frames(2);
        Input.ActionRelease(action); await Frames(2);
        await _carry.PendingAction; await Frames(3);
    }
    private void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        _checks++;
        _receipt.Add(new { kind = "check", label, passed = true });
        GD.Print("act1-yard-mechanisms: " + label);
    }
    private static string EvidenceDirectory()
    {
        var output = System.Environment.GetEnvironmentVariable("URMAN_MECHANISM_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
            return ProjectSettings.GlobalizePath("res://../docs/production/act1_yard_mechanisms_evidence_2026-09-15");
        if (!Path.IsPathFullyQualified(output)) throw new InvalidOperationException("URMAN_MECHANISM_OUTPUT must be an absolute new evidence path.");
        return output;
    }
    private async Task Capture(string name)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_MECHANISM_CAPTURE") != "1") return;
        if (RenderingServer.GetRenderingDevice() is null) throw new InvalidOperationException("No render device for capture.");
        Directory.CreateDirectory(EvidenceDirectory());
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "yard/" + name);
        EnsureNoPauseShell("capture the actual game view");
        var path = Path.Combine(EvidenceDirectory(), name + ".png");
        if (File.Exists(path)) throw new IOException($"Refusing to overwrite previous evidence: {path}");
        GD.Print("act1-yard-capture-save-begin: " + path);
        if (GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok) throw new IOException(path);
        GD.Print("act1-yard-capture-save-end: " + path);
        _receipt.Add(new { kind = "game-camera", file = path });
    }
    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            EnsureNoPauseShell("continue the local mechanism proof");
        }
    }

    private void EnsureNoPauseShell(string operation)
    {
        if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true })
            throw new InvalidOperationException($"Cannot {operation}: the real pause menu is open. " + _carry?.DescribeAim());
    }
}
