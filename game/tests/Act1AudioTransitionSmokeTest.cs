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
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Audio transition smoke could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        await Frames(3);

        var director = GetTree().GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector;
        if (director is null)
        {
            Fail("Audio transition smoke could not find the ambience director.");
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
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private async Task<bool> VerifyWorldFoley(Main main)
    {
        const string worldFoleyGroup = "world_foley";
        // This is the interior HouseExit source after the house_old_pc origin
        // (-28, 0, 0). The village_day HouseDoorPortalCenter is an exterior
        // anchor and must not be reused after the player enters the interior.
        var source = new Vector3(-28f, 1.05f, 4.82f);

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

        // PlayWorld hands playback to the audio server, which starts a mix later
        // than the node appears in the group. Setting StreamPaused before the
        // stream is actually playing is a no-op in Godot, which made the pause
        // assertion race the audio thread: it failed once in a full 34-scene run
        // and passed on every re-run. Wait for playback to start within a bound
        // that sits safely inside the 1.21 s source, and fail if it never starts;
        // the pause contract itself is unchanged.
        var playDeadline = Time.GetTicksMsec() + 400;
        while (!player.Playing && Time.GetTicksMsec() < playDeadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!player.Playing)
        {
            Fail("World foley never started playing, so its pause contract cannot be checked.");
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
            Fail("StopWorld retained an in-flight source-positioned player.");
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
        return true;
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
