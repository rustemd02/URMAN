using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// AUDIO-009 focused smoke: switching through every Act I zone keeps exactly
/// the manifest-mapped bed audible (one active player after the crossfade
/// settles), streams follow the manifest routing, and no zone ends with two
/// looping beds.
/// </summary>
public partial class Act1AudioTransitionSmokeTest : Node
{
    public override async void _Ready()
    {
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Audio transition smoke could not load the ordinary Act I entrypoint.");
            return;
        }

        // Act1DemoRoot owns the actual pause menu. The internal Main scene
        // alone cannot exercise the product's pause and resume lifecycle.
        AddChild(demo);
        await Frames(1);
        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Audio transition smoke could not start an ordinary New Game.");
            return;
        }
        demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
        await Frames(2);
        var main = demo.DemoMain;

        var director = GetTree().GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector;
        if (director is null)
        {
            Fail("Audio transition smoke could not find the ambience director.");
            return;
        }

        if (!await VerifyRinatLandingStep(main))
        {
            return;
        }

        if (!await VerifyWorldFoley(main))
        {
            return;
        }

        if (!await VerifyHouseDoorInteraction(main))
        {
            return;
        }

        // Zone -> manifest stem file, read from the same manifest the
        // director uses (single routing source).
        var manifest = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(
            System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/audio/ambient_manifest.json")));
        var expectedFileByZone = new Dictionary<string, string>();
        foreach (var stem in manifest?["stems"]?.AsArray() ?? [])
        {
            var file = stem?["file"]?.GetValue<string>();
            foreach (var zone in stem?["zones"]?.AsArray() ?? [])
            {
                expectedFileByZone[zone!.GetValue<string>()] = file!;
            }
        }

        var expectations = new (string Zone, string Spawn)[]
        {
            ("village_day", "arrival"),
            ("village_day", "from_house"),
            ("village_day", "from_forest"),
            ("house_old_pc", "entry"),
            ("fap_clinic", "waiting_room"),
            ("zirat_road", "village_side"),
            ("kara_urman_night", "village_path")
        };

        foreach (var zone in expectations)
        {
            // AUDIO-003: village sub-zone beds key on zone@spawn with the
            // plain zone bed as fallback.
            var bedKey = $"{zone.Zone}@{zone.Spawn}";
            var expectedFile = expectedFileByZone.TryGetValue(bedKey, out var subBed)
                ? subBed
                : expectedFileByZone[zone.Zone];

            main.SwitchZone(zone.Zone, zone.Spawn);
            // Let the 0.65 s crossfade finish (time-based, not frame-based:
            // headless frames are not vsync-capped).
            await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
            await Frames(2);

            if (director.CurrentZoneId != zone.Zone)
            {
                Fail($"Zone {zone.Zone}: director stayed on {director.CurrentZoneId}.");
                return;
            }

            // Headless runs intentionally never start playback; the contract
            // is the routed stream: exactly one player holds a stream and it
            // is the manifest stem for this zone.
            var streamHolders = 0;
            string? holderPath = null;
            for (var index = 0; index < director.PlayerCount; index++)
            {
                var player = director.GetNode<AudioStreamPlayer>($"AmbientPlayer{index + 1}");
                if (player.Stream is not null)
                {
                    streamHolders++;
                    holderPath = player.Stream.ResourcePath;
                }
            }

            if (streamHolders != 1
                || director.CurrentStreamPath != holderPath
                || holderPath != expectedFile)
            {
                Fail($"Zone {zone.Zone}: stream routing drifted (holders={streamHolders}, path={holderPath ?? "<null>"}, expected={expectedFile}).");
                return;
            }
        }

        GD.Print("act1-audio-transitions: PASS 5 zones -> manifest beds + world foley spatial lifecycle");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private async Task<bool> VerifyWorldFoley(Main main)
    {
        const string worldFoleyGroup = "world_foley";
        // The physical interior exit follows the current house contract. Do
        // not retain the old room's numeric anchor after the shell is resized.
        var exit = main.ConnectedWorld?.FindChild("HouseExit", true, false) as Node3D;
        if (exit is null)
        {
            Fail("World foley smoke could not find the current interior HouseExit source.");
            return false;
        }
        var source = exit.GlobalPosition;
        var listener = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var previousListener = listener?.CapturePortableTransform();
        if (DisplayServer.GetName() != "headless" && listener is not null)
        {
            // Keep this source-positioned sound inside its authored 14 m
            // range. A listener at village arrival is about 30 m from the
            // actual house door and cannot prove native audible pause.
            listener.ApplyZoneSpawn(source + new Vector3(0f, 0f, 2f), 0f);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        UiFoley.StopWorld(GetTree());
        UiFoley.PlayWorld(main, source, "door_creak");
        await Frames(1);

        var nodes = GetTree().GetNodesInGroup(worldFoleyGroup);
        if (DisplayServer.GetName() == "headless")
        {
            // PlayWorld intentionally has no native player under the headless
            // display. Still exercise the lifecycle clear used by load/menu.
            if (nodes.Count != 0)
            {
                Fail($"Headless world foley guard left {nodes.Count} native players.");
                return false;
            }

            UiFoley.SetWorldPaused(GetTree(), true);
            UiFoley.SetWorldPaused(GetTree(), false);
            UiFoley.StopWorld(GetTree());
            await Frames(1);
            if (GetTree().GetNodesInGroup(worldFoleyGroup).Count != 0)
            {
                Fail("Headless world foley lifecycle clear retained a player.");
                return false;
            }

            GD.Print("act1-world-foley: headless guard/lifecycle clear PASS; native spatial assertions require a window");
            return true;
        }

        if (nodes.Count != 1)
        {
            Fail($"World foley did not create exactly one native player (count={nodes.Count}).");
            return false;
        }

        if (nodes[0] is not AudioStreamPlayer3D player)
        {
            Fail($"World foley group member is not AudioStreamPlayer3D ({nodes[0].GetType().Name}).");
            return false;
        }

        if (player.Stream is null
            || player.Bus != AudioSettingsService.SfxBus
            || player.GlobalPosition.DistanceTo(source) > 0.001f
            || !Mathf.IsEqualApprox(player.UnitSize, 2f)
            || !Mathf.IsEqualApprox(player.MaxDistance, 14f))
        {
            Fail($"World foley spatial contract drifted (stream={player.Stream is not null}, bus={player.Bus}, position={player.GlobalPosition}, unit={player.UnitSize}, max={player.MaxDistance}).");
            return false;
        }

        // AudioStreamPlayer3D.Play queues the start for the next physics frame.
        // Playing alone also describes that queued start; prove an actual
        // mixer cursor before exercising pause, inside the 1.21 s source.
        var playDeadline = Time.GetTicksMsec() + 400;
        while ((!player.Playing || !player.HasStreamPlayback() || player.GetPlaybackPosition() < .02f)
            && Time.GetTicksMsec() < playDeadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        if (!player.Playing || !player.HasStreamPlayback() || player.GetPlaybackPosition() < .02f)
        {
            Fail($"World foley did not reach native playback (playing={player.Playing}, playback={player.HasStreamPlayback()}, cursor={player.GetPlaybackPosition():0.000}, paused={player.StreamPaused}); its pause contract is unverified.");
            return false;
        }

        UiFoley.SetWorldPaused(GetTree(), true);
        if (!player.StreamPaused)
        {
            Fail("World foley pause did not pause the native stream.");
            return false;
        }

        UiFoley.SetWorldPaused(GetTree(), false);
        if (player.StreamPaused)
        {
            Fail("World foley resume left the native stream paused.");
            return false;
        }

        // This is the explicit clear used by RuntimeBridge load/reset and the
        // pause-menu return-to-menu path; it must free an in-flight one-shot.
        UiFoley.StopWorld(GetTree());
        await Frames(1);
        if (GetTree().GetNodesInGroup(worldFoleyGroup).Count != 0)
        {
            var retained = GetTree().GetNodesInGroup(worldFoleyGroup)
                .Select(node => $"{node.GetPath()} queued={node.IsQueuedForDeletion()} type={node.GetType().Name}");
            Fail("StopWorld retained an in-flight source-positioned player: " + string.Join(" | ", retained));
            return false;
        }

        var pause = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi;
        if (pause is null)
        {
            Fail("World foley paused-start check has no real pause menu.");
            return false;
        }
        if (!await OpenActualPause(pause)) return false;
        UiFoley.PlayWorld(main, source, "door_creak");
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await Frames(1);
        var pausedSource = GetTree().GetNodesInGroup(worldFoleyGroup).OfType<AudioStreamPlayer3D>().Single();
        var pausedStart = pausedSource.GetPlaybackPosition();
        await ToSignal(GetTree().CreateTimer(.20), SceneTreeTimer.SignalName.Timeout);
        var pausedEnd = pausedSource.GetPlaybackPosition();
        var stillPaused = pause.IsOpen;
        GD.Print($"act1-world-foley-paused-start: menuOpen={stillPaused} queuedPlaying={pausedSource.Playing} nativePlayback={pausedSource.HasStreamPlayback()} nativePaused={pausedSource.StreamPaused} cursor={pausedStart:0.000}->{pausedEnd:0.000}");
        // A queued Playing flag is not a failure. A correct implementation may
        // retain a paused playback or defer its native start until Resume.
        var advancedWhilePaused = pausedEnd > .04f || pausedEnd - pausedStart > .025f;
        if (!await ResumeActualPause(pause)) return false;
        if (!stillPaused || advancedWhilePaused)
        {
            UiFoley.StopWorld(GetTree());
            Fail(!stillPaused
                ? "Paused-start fixture lost its real pause state before the native cursor sample."
                : $"A newly requested world one-shot advanced while pause was open ({pausedStart:0.000}->{pausedEnd:0.000}s).");
            return false;
        }
        var resumeDeadline = Time.GetTicksMsec() + 400;
        while (pausedSource.GetPlaybackPosition() <= pausedEnd + .02f && Time.GetTicksMsec() < resumeDeadline)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (pausedSource.GetPlaybackPosition() <= pausedEnd + .02f)
        {
            UiFoley.StopWorld(GetTree());
            Fail("The world one-shot requested during pause did not start/resume after Resume.");
            return false;
        }
        UiFoley.StopWorld(GetTree());
        await Frames(1);

        // Cancelling a source requested during the real pause must also remove
        // its deferred start, so Resume cannot resurrect a door sound after a
        // load/menu cleanup. This is the same owner reset used by those flows.
        if (!await OpenActualPause(pause)) return false;
        UiFoley.PlayWorld(main, source, "door_creak");
        await Frames(1);
        UiFoley.StopWorld(GetTree());
        await Frames(2);
        if (!await ResumeActualPause(pause)) return false;
        await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
        if (GetTree().GetNodesInGroup(worldFoleyGroup).Count != 0)
        {
            Fail("A world one-shot cancelled during pause reappeared after Resume.");
            return false;
        }

        // Play again without StopWorld and let the real stream duration drive
        // Finished cleanup. The checked-in source-backed candidate is 1.21 s;
        // the margin keeps this check independent of the render frame rate.
        UiFoley.PlayWorld(main, source, "door_creak");
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        await Frames(2);
        if (GetTree().GetNodesInGroup(worldFoleyGroup).Count != 0)
        {
            Fail("Finished did not clean up the completed world foley one-shot.");
            return false;
        }

        GD.Print("act1-world-foley: PASS source position + SFX routing + unit/max distance + pause/resume + StopWorld + Finished cleanup");
        if (listener is not null && previousListener is { } previous) listener.ApplyPortableTransform(previous);
        return true;
    }

    private async Task<bool> VerifyRinatLandingStep(Main main)
    {
        var presentation = RinatPresencePresentation.Current(GetTree());
        var sound = main.ConnectedWorld?.FindChild("RinatLandingStep", true, false) as AudioStreamPlayer3D;
        var pause = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi;
        var listener = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (presentation is null || sound is null || pause is null || listener is null || bridge is null)
        {
            Fail("Rinat landing audio has no actual retained player, presentation, listener or pause shell.");
            return false;
        }
        var identity = sound.GetInstanceId();
        var previous = listener.CapturePortableTransform();
        var state = bridge.SelectRuntimeState().GetRawText();
        var finishedLandings = 0;
        void RecordFinishedLanding() { finishedLandings++; }
        sound.Finished += RecordFinishedLanding;
        try
        {
            if (DisplayServer.GetName() == "headless")
            {
                presentation.RequestLandingStep();
                presentation.CancelIntervention();
                if (sound.Playing || sound.Stream is null || sound.GetInstanceId() != identity)
                {
                    Fail("Headless Rinat landing replay started native sound or destroyed its retained player.");
                    return false;
                }
                GD.Print("act1-rinat-audio: headless replay/cancel guard PASS; native pause external to this display");
                return true;
            }

            listener.ApplyZoneSpawn(sound.GlobalPosition + new Vector3(0f, 0f, 2f), 0f);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            presentation.RequestLandingStep();
            if (!await NativeCursorAfter(sound, .02f))
            {
                Fail("Rinat's actual landing stream did not reach native playback before pause.");
                return false;
            }
            if (!await OpenActualPause(pause)) return false;
            var start = sound.GetPlaybackPosition();
            await ToSignal(GetTree().CreateTimer(.20), SceneTreeTimer.SignalName.Timeout);
            var end = sound.GetPlaybackPosition();
            GD.Print($"act1-rinat-audio-pause: menu={pause.IsOpen} nativePaused={sound.StreamPaused} cursor={start:0.000}->{end:0.000} retained={sound.GetInstanceId() == identity}");
            if (!pause.IsOpen || !sound.StreamPaused || Math.Abs(end - start) > .025f || sound.IsQueuedForDeletion())
            {
                Fail("Rinat's retained landing sound continued or was destroyed while the real pause menu was open.");
                return false;
            }
            if (!await ResumeActualPause(pause) || !await NativeCursorAfter(sound, end + .02f))
            {
                Fail("Rinat's retained landing sound did not resume its native cursor after the Resume button.");
                return false;
            }

            presentation.CancelIntervention();
            await Frames(2);
            if (sound.Playing || sound.Stream is null || sound.GetInstanceId() != identity)
            {
                Fail("Rinat's cancellation did not stop and retain his reusable landing player.");
                return false;
            }

            // A landing can request Play during the render frame in which
            // Escape opens pause, before AudioStreamPlayer3D creates its native
            // playback on the next physics tick. Exercise that third ordering
            // through the production pause owner and the normal presentation
            // update; neither the already-playing nor paused-request checks
            // above/below covers it.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var finishesBeforeQueuedStart = finishedLandings;
            presentation.RequestLandingStep();
            var playingBeforePause = sound.Playing;
            var playbackBeforePause = sound.HasStreamPlayback();
            var cursorBeforePause = sound.GetPlaybackPosition();
            pause.Open();
            GD.Print($"act1-rinat-audio-request-before-pause: playing={playingBeforePause} retainedPlayback={playbackBeforePause} cursor={cursorBeforePause:0.000000} menu={pause.IsOpen} controlsPaused={listener.ModalOpen}");
            // A reused AudioStreamPlayer3D may retain its previous playback
            // object after Stop. The synchronous request -> Open ordering
            // establishes this case; HasStreamPlayback cannot identify a new
            // native start on a retained player.
            if (!playingBeforePause || !pause.IsOpen || !listener.ModalOpen)
            {
                Fail("Rinat queued-start fixture did not open the actual pause before native playback.");
                return false;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
            var queuedPausedCursor = sound.GetPlaybackPosition();
            GD.Print($"act1-rinat-audio-queued-pause: menu={pause.IsOpen} nativePlayback={sound.HasStreamPlayback()} nativePaused={sound.StreamPaused} cursor={cursorBeforePause:0.000000}->{queuedPausedCursor:0.000000}");
            if (!pause.IsOpen || queuedPausedCursor > .001f)
            {
                Fail($"Rinat's queued landing advanced before Resume ({queuedPausedCursor:0.000000}s).");
                return false;
            }
            if (!await ResumeActualPause(pause) || !await NativeCursorAfter(sound, .02f))
            {
                Fail("Rinat's landing queued immediately before pause did not play after Resume.");
                return false;
            }

            // Let this retained player finish naturally, without cancelling it
            // or opening another menu while its native playback is active.
            // A stale queued marker must not resurrect that completed landing.
            var finishDeadline = Time.GetTicksMsec() + 1500;
            while ((sound.Playing || finishedLandings == finishesBeforeQueuedStart)
                && Time.GetTicksMsec() < finishDeadline)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (sound.Playing || finishedLandings != finishesBeforeQueuedStart + 1)
            {
                Fail("Rinat's resumed landing did not finish naturally exactly once.");
                return false;
            }
            var naturalFinishes = finishedLandings;
            if (!await OpenActualPause(pause)) return false;
            await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
            if (!await ResumeActualPause(pause)) return false;
            await ToSignal(GetTree().CreateTimer(.20), SceneTreeTimer.SignalName.Timeout);
            GD.Print($"act1-rinat-audio-after-natural-end: playing={sound.Playing} cursor={sound.GetPlaybackPosition():0.000000} finishes={naturalFinishes}->{finishedLandings} retained={sound.GetInstanceId() == identity}");
            if (sound.Playing || finishedLandings != naturalFinishes || sound.GetInstanceId() != identity)
            {
                Fail("A late Pause/Resume replayed a Rinat landing that had already finished naturally.");
                return false;
            }
            presentation.CancelIntervention();
            await Frames(2);

            if (!await OpenActualPause(pause)) return false;
            presentation.RequestLandingStep();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
            if (!pause.IsOpen || sound.GetPlaybackPosition() > .025f)
            {
                Fail("A Rinat landing requested during pause started its native cursor before Resume.");
                return false;
            }
            presentation.CancelIntervention();
            if (!await ResumeActualPause(pause)) return false;
            await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
            if (sound.Playing || sound.GetInstanceId() != identity || sound.Stream is null
                || bridge.SelectRuntimeState().GetRawText() != state)
            {
                Fail("Rinat's cancelled paused request replayed later, destroyed its player, or changed narrative state.");
                return false;
            }
            GD.Print("act1-rinat-audio: PASS native landing replay + Escape pause + Resume + natural-end no replay + pending cancel + retained player; physical step is a separate finale check");
            return true;
        }
        finally
        {
            sound.Finished -= RecordFinishedLanding;
            presentation.CancelIntervention();
            if (pause.IsOpen) pause.Resume();
            listener.ApplyPortableTransform(previous);
        }
    }

    private async Task<bool> NativeCursorAfter(AudioStreamPlayer3D player, double minimum)
    {
        var deadline = Time.GetTicksMsec() + 400;
        do
        {
            if (player.Playing && player.HasStreamPlayback() && player.GetPlaybackPosition() > minimum) return true;
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        } while (Time.GetTicksMsec() < deadline);
        return false;
    }

    private async Task<bool> OpenActualPause(PauseMenuUi pause)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
        await Frames(2);
        if (pause.IsOpen && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: true }) return true;
        Fail("Real Escape did not open the production pause menu and gate player controls.");
        return false;
    }

    private async Task<bool> ResumeActualPause(PauseMenuUi pause)
    {
        if (pause.ResumeButton is not { Disabled: false } button)
        {
            Fail("The production pause menu has no enabled Resume button.");
            return false;
        }
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (!pause.IsOpen && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: false }) return true;
        Fail("The production Resume button did not close pause and restore player controls.");
        return false;
    }

    private async Task<bool> VerifyHouseDoorInteraction(Main main)
    {
        const string slot = "act1-audio-house-interaction";
        const string worldFoleyGroup = "world_foley";
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var connected = main.ConnectedWorld;
        var houseDoor = connected?.FindChild("HouseDoor", true, false) as InteractionTarget;
        var houseExit = connected?.FindChild("HouseExit", true, false) as Node3D;
        if (bridge is null || connected is null || houseDoor is null || houseExit is null)
        {
            Fail("Audio transition smoke could not find the connected-world HouseDoor/HouseExit path.");
            return false;
        }

        // Ordinary arrival now requires reading both personal sources and an
        // actual reply. Reuse the same reader/choice proof as the family flow;
        // the audio fixture must not bypass those gates with synthetic flags.
        await Act1ArrivalFlowProof.CompleteAsync(this, bridge);
        DeleteSlot(slot);
        if (!await bridge.SaveSlotAsync(slot))
        {
            Fail("Audio transition smoke could not save the pre-interaction load fixture.");
            return false;
        }

        for (var attempt = 0; attempt < 60 && !houseDoor.IsAvailable(); attempt++)
        {
            await Frames(1);
        }

        if (!houseDoor.IsAvailable() || !bridge.IsInteractionAvailable(houseDoor.InteractionId))
        {
            Fail("HouseDoor was not available for the actual InteractionTarget.Interact path.");
            return false;
        }

        var interactionSession = bridge.SessionIdentity;
        if (interactionSession is null || !houseDoor.IsPresentationCurrent(bridge, interactionSession))
        {
            Fail("Fresh house interaction did not own its presentation session.");
            return false;
        }
        var menu = new MainMenuUi();
        AddChild(menu);
        var rejectedInMenu = !houseDoor.IsPresentationCurrent(bridge, interactionSession);
        menu.Dismiss();
        await Frames(1);
        if (!rejectedInMenu || !houseDoor.IsPresentationCurrent(bridge, interactionSession))
        {
            Fail("Pending interaction presentation did not follow the real main-menu state.");
            return false;
        }

        UiFoley.StopWorld(GetTree());
        houseDoor.Interact();
        var enteredHouse = false;
        for (var attempt = 0; attempt < 120; attempt++)
        {
            await Frames(1);
            if (connected.ActiveZoneId == "house_old_pc" && bridge.CurrentZoneId == "house_old_pc")
            {
                enteredHouse = true;
                break;
            }
        }

        if (!enteredHouse)
        {
            Fail($"HouseDoor.Interact did not enter house_old_pc (world={connected.ActiveZoneId}, bridge={bridge.CurrentZoneId}).");
            return false;
        }

        var nodes = GetTree().GetNodesInGroup(worldFoleyGroup);
        if (DisplayServer.GetName() == "headless")
        {
            if (nodes.Count != 0)
            {
                Fail($"Headless HouseDoor.Interact left {nodes.Count} world foley players.");
                return false;
            }
        }
        else
        {
            if (nodes.Count != 1 || nodes[0] is not AudioStreamPlayer3D player)
            {
                Fail($"HouseDoor.Interact did not create one world foley player (count={nodes.Count}).");
                return false;
            }

            if (player.GetParent() != main
                || player.GlobalPosition.DistanceTo(houseExit.GlobalPosition) > 0.001f)
            {
                Fail($"HouseDoor.Interact used the wrong source host/position (parent={player.GetParent()?.Name}, position={player.GlobalPosition}, expected={houseExit.GlobalPosition}).");
                return false;
            }
        }

        if (!await bridge.LoadSlotAsync(slot))
        {
            Fail("Audio transition smoke could not load the pre-interaction fixture.");
            return false;
        }

        await Frames(1);
        DeleteSlot(slot);
        if (houseDoor.IsPresentationCurrent(bridge, interactionSession))
        {
            Fail("Load retained presentation ownership for the old interaction session.");
            return false;
        }
        if (bridge.CurrentZoneId != "village_day"
            || connected.ActiveZoneId != "village_day"
            || GetTree().GetNodesInGroup(worldFoleyGroup).Count != 0)
        {
            Fail($"Load did not restore village silently (bridge={bridge.CurrentZoneId}, world={connected.ActiveZoneId}, foley={GetTree().GetNodesInGroup(worldFoleyGroup).Count}).");
            return false;
        }

        GD.Print("act1-world-foley: PASS actual HouseDoor.Interact -> HouseExit source + load silence");
        return true;
    }

    private static void DeleteSlot(string slot)
    {
        foreach (var suffix in new[] { ".savegame-v3.json", ".savegame-v3.backup.json" })
        {
            var path = ProjectSettings.GlobalizePath($"user://savegames/{slot}{suffix}");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

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
