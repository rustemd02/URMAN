using Godot;
using Urman.Godot;

namespace Urman.Godot.Tests;

public partial class Act1DemoLaunchSmokeTest : Node
{
    public override async void _Ready()
    {
        // The launch smoke asserts the declared default preset, and the demo
        // correctly lets a stored profile override it (a player may have chosen
        // high). Start from a deleted settings store so the assertion measures the
        // build, not whatever profile happens to sit in local userdata.
        UserSettingsStore.Delete();
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn");
        var demo = packed?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Act 1 demo scene could not be instantiated.");
            return;
        }

        AddChild(demo);
        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var menuPlayer = demo.DemoMain.GetNodeOrNull<FirstPersonController>("Player");
        if (!demo.MainMenuVisible
            || demo.MainMenu?.NewGameButton is null
            || demo.MainMenu.ContinueButton is null
            || demo.MainMenu.ReplayIntroButton is not { Visible: true } replayButton
            || demo.IntroVisible
            || menuPlayer?.ModalOpen != true)
        {
            Fail("Act 1 main menu did not gate the demo start before any intro or gameplay input.");
            return;
        }

        var quickBeforeReplay = bridge?.IsPlayerSlotAvailable(MainMenuUi.ContinueSlot) ?? false;
        var checkpointBeforeReplay = bridge?.IsPlayerSlotAvailable(MainMenuUi.CheckpointSlot) ?? false;
        replayButton.EmitSignal(BaseButton.SignalName.Pressed);
        for (var frame = 0; frame < 900 && !demo.IntroVisible; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!demo.IntroVisible || bridge?.IsDebugSession != true)
        {
            Fail("Main menu did not start an isolated arrival-intro replay.");
            return;
        }
        demo._UnhandledInput(new InputEventAction { Action = "interact", Pressed = true });
        for (var frame = 0; frame < 900 && !demo.MainMenuVisible; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (demo.IntroVisible || !demo.MainMenuVisible || menuPlayer.ModalOpen != true
            || bridge.IsPlayerSlotAvailable(MainMenuUi.ContinueSlot) != quickBeforeReplay
            || bridge.IsPlayerSlotAvailable(MainMenuUi.CheckpointSlot) != checkpointBeforeReplay
            || bridge.SelectRuntimeState().GetProperty("knowledge")
                .GetProperty("urman.chapter1:knowledge/memory_marat_childhood_photo")
                .GetProperty("status").GetString() != "hidden")
        {
            Fail("Skipping intro replay did not restore the menu or preserved player saves.");
            return;
        }

        var reducedMotionIntro = System.Environment.GetEnvironmentVariable("URMAN_INTRO_REDUCED_MOTION") == "1";
        if (reducedMotionIntro)
            menuPlayer.ApplySettings(menuPlayer.CaptureSettings() with
            {
                Accessibility = menuPlayer.Accessibility with { ReducedMotion = true }
            });

        for (var frame = 0; frame < 900 && demo.MainMenu?.NewGameButton?.Disabled == true; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Act 1 demo did not reach the intro through the main menu New Game button.");
            return;
        }

        if (bridge?.IsDebugSession != false)
        {
            Fail("New Game inherited the isolated intro replay session.");
            return;
        }

        if (demo.DemoMain is null
            || demo.DemoMain.ActiveZoneScenePath != "res://scenes/zones/style_benchmark_day_street.tscn"
            || !demo.DemoMain.EnableZoneTransitionFade
            || GetTree().GetFirstNodeInGroup("act1_demo_transition") is null
            || bridge?.ActiveSceneId != "urman.chapter1:scene/arrival_vehicle_dusk"
            || demo.DemoEnded
            || !demo.IntroVisible
            || !demo.IntroControlsText!.Contains("WASD", StringComparison.Ordinal)
            || !demo.IntroControlsText.Contains("E — начать", StringComparison.Ordinal))
        {
            Fail("Act 1 demo did not start in the first-person Chapter 1 arrival state.");
            return;
        }

        if (reducedMotionIntro)
        {
            var playerCamera = menuPlayer.GetNode<Camera3D>("Head/Camera3D");
            var hud = menuPlayer.GetNode<CanvasLayer>("Hud");
            if (GetViewport().GetCamera3D() != playerCamera || hud.Visible)
            {
                Fail("Reduced motion did not keep the arrival camera still and hide gameplay HUD.");
                return;
            }
            demo._UnhandledInput(new InputEventAction { Action = "interact", Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (demo.IntroVisible || menuPlayer.ModalOpen || !hud.Visible)
            {
                Fail("Reduced-motion intro did not restore HUD and input after confirmation.");
                return;
            }
            GD.Print("act1-demo-launch-smoke: reduced-motion intro -> still player camera -> HUD/input restored");
            await GodotSmokeCleanup.ReleaseAsync(demo);
            GetTree().Quit(0);
            return;
        }

        var introCamera = GetViewport().GetCamera3D();
        if (introCamera?.Name.ToString() != "Act1ArrivalFlyoverCamera")
        {
            Fail("New Game did not start the authored arrival camera before input.");
            return;
        }
        var firstIntroPosition = introCamera.GlobalPosition;
        var introCaptureDir = System.Environment.GetEnvironmentVariable("URMAN_INTRO_CAPTURE_DIR");
        if (!string.IsNullOrEmpty(introCaptureDir))
        {
            if (!Path.IsPathFullyQualified(introCaptureDir) || !Directory.Exists(introCaptureDir)
                || Directory.EnumerateFileSystemEntries(introCaptureDir).Any())
            {
                Fail("Intro capture needs an existing empty absolute directory.");
                return;
            }
            if (System.Environment.GetEnvironmentVariable("URMAN_INTRO_CAPTURE_SCALE") == "large")
                demo.ApplyAccessibilitySettings(menuPlayer.Accessibility with { TextScale = 1.6 });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CaptureIntro(introCaptureDir, "01_departure");
        }

        var connectedWorld = demo.DemoMain.ConnectedWorld;
        var coreLayer = connectedWorld?.GetNodeOrNull<Node3D>("Act1CoreWorldGreybox");
        var exteriorLayer = coreLayer?.GetNodeOrNull<Node3D>("AgentBExteriorWorld");
        var fapZone = coreLayer?.GetNodeOrNull<Node3D>("FapExterior");
        var fapOwner = fapZone?.GetNodeOrNull<Node3D>("FapClinicAuthoredKitPresentation");
        var fapWayfindingLabel = fapOwner?.GetNodeOrNull<Label3D>(
            "FapAuthoredWayfindingBoard/FapWayfindingLabel");
        var agentBBuildings = exteriorLayer?.GetNodeOrNull<Node3D>("AgentB_VillageBuildingsKit");
        var agentBFoliage = exteriorLayer?.GetNodeOrNull<Node3D>("AgentB_FoliageKit");
        var plantedFoliage = exteriorLayer?.GetNodeOrNull<Node3D>("AgentB_PlantedFoliage");
        var agentBArchitecture = exteriorLayer?.GetNodeOrNull<StaticBody3D>("AgentB_ArchitectureCollision");
        var groundBody = exteriorLayer?.GetNodeOrNull<StaticBody3D>("AgentB_TerrainCollision");
        var groundShape = groundBody?.GetNodeOrNull<CollisionShape3D>("AgentB_TerrainFaces");
        var plantedChildCount = plantedFoliage?.GetChildCount() ?? -1;
        var plannedEntryCount = exteriorLayer?.GetMeta("plannedFoliageEntryCount").AsInt32() ?? -1;
        var plantedEntryCount = exteriorLayer?.GetMeta("plantedFoliageEntryCount").AsInt32() ?? -1;
        var plantedNodeCount = exteriorLayer?.GetMeta("plantedFoliageNodeCount").AsInt32() ?? -1;
        var minimumFoliageRoadClearance = exteriorLayer?.GetMeta("minimumFoliageRoadClearance").AsDouble() ?? -1d;
        var foliageRebasePolicy = exteriorLayer?.GetMeta("foliageRebasePolicy").AsString() ?? string.Empty;
        var genericFacetedMassSuppressionCount = coreLayer?.GetMeta("genericFacetedMassSuppressionCount").AsInt32() ?? -1;
        var agentBEnvironment = exteriorLayer?.GetNodeOrNull<WorldEnvironment>("AgentBEnvironment");
        var agentBRain = exteriorLayer?.GetNodeOrNull<CpuParticles3D>("AgentBSnow");
        var karaAccentLights = exteriorLayer?.GetNodeOrNull<Node3D>("KaraAccentLights");
        var villageEnvironment = connectedWorld?.GetNodeOrNull<WorldEnvironment>(
            "village-main-road/WorldEnvironment");
        if (connectedWorld is null
            || !connectedWorld.IsBuilt
            || exteriorLayer is null
            || exteriorLayer.GetMeta("variantStatus").AsString() != "production-canonical"
            || exteriorLayer.GetMeta("retiredVariants").AsString() != "AgentBAct1World"
            || fapOwner is null
            || !fapOwner.HasMeta("assetSource")
            || fapWayfindingLabel is null
            || fapWayfindingLabel.Text != "ФАП"
            || agentBBuildings is null
            || agentBBuildings.GetMeta("suppressedPresentationFamilies").AsString() != "Fap_*"
            || agentBBuildings.GetMeta("suppressedFapMeshCount").AsInt32() <= 0
            || HasVisibleFapMesh(agentBBuildings)
            || agentBFoliage is null
            || agentBFoliage.Visible
            || !agentBFoliage.GetMeta("templateSourceHidden").AsBool()
            || plantedFoliage is null
            || plantedFoliage.GetChildCount() <= 0
            || plannedEntryCount <= 0
            || plantedEntryCount != plannedEntryCount
            || plantedNodeCount != plantedChildCount
            || minimumFoliageRoadClearance < 0.25d
            || foliageRebasePolicy.Length == 0
            || genericFacetedMassSuppressionCount != 11
            || karaAccentLights is null
            || karaAccentLights.GetChildCount() != 3
            || CountActiveWorldEnvironments(connectedWorld) != 1
            || agentBEnvironment?.Environment is null
            || agentBRain?.Emitting != true
            || villageEnvironment?.Environment is not null
            || agentBArchitecture is null
            || HasFapCollision(agentBArchitecture)
            || groundBody is null
            || groundShape?.Shape is not ConcavePolygonShape3D
            || groundShape.Disabled
            || connectedWorld.HasNode("Act1Connectors/SharedVillageGroundTraversalCollision")
            || connectedWorld.ConnectorTraversalCollisionCount != 0)
        {
            Fail("Act 1 connected world did not expose the production exterior layer, single labeled FAP owner, and reverse-arrival ground envelope.");
            return;
        }

        demo._UnhandledInput(new InputEventKey
        {
            Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true, Echo = true
        });
        if (!demo.IntroVisible)
        {
            Fail("A repeated held interaction key dismissed the New Game intro.");
            return;
        }
        await ToSignal(GetTree().CreateTimer(3.6), SceneTreeTimer.SignalName.Timeout);
        if (!demo.IntroVisible || !menuPlayer.ModalOpen
            || GetViewport().GetCamera3D() != introCamera
            || introCamera.GlobalPosition.DistanceTo(firstIntroPosition) < 1f)
        {
            Fail($"Ordinary New Game lost its moving intro and input gate before confirmation: intro={demo.IntroVisible}, modal={menuPlayer.ModalOpen}.");
            return;
        }
        if (!string.IsNullOrEmpty(introCaptureDir))
        {
            CaptureIntro(introCaptureDir, "02_village");
            await ToSignal(GetTree().CreateTimer(5.0), SceneTreeTimer.SignalName.Timeout);
            if (!demo.IntroVisible || !menuPlayer.ModalOpen
                || GetViewport().GetCamera3D() != menuPlayer.GetNode<Camera3D>("Head/Camera3D"))
            {
                Fail("Arrival flyover did not return to the player camera while preserving the intro input gate.");
                return;
            }
            CaptureIntro(introCaptureDir, "03_handoff");
        }

        connectedWorld.SetActiveLogicalZone("house_old_pc");
        var houseEnvironment = connectedWorld.GetNodeOrNull<WorldEnvironment>(
            "babay-abi-house/WorldEnvironment");
        if (CountActiveWorldEnvironments(connectedWorld) != 1
            || agentBEnvironment.Environment is not null
            || agentBRain?.Emitting == true
            || CountVisibleLights(karaAccentLights) != 0
            || houseEnvironment?.Environment is null)
        {
            Fail("Act 1 connected world did not route the single atmosphere owner to the house interior.");
            return;
        }

        var karaPlayer = demo.DemoMain.GetNodeOrNull<FirstPersonController>("Player");
        if (karaPlayer is null
            || !Act1WorldLayout.TryGetWorldSpawn("kara_urman_night", "village_path", out var karaSpawn)
            || !Act1WorldLayout.TryGetWorldSpawn("village_day", "arrival", out var villageSpawn))
        {
            Fail("Act 1 Kara presentation smoke could not resolve its connected-world spawns.");
            return;
        }

        // Keep this a presentation-only probe: move the test-owned player to
        // the Kara camera envelope without dispatching a narrative transition
        // or changing RuntimeBridge location state.
        karaPlayer.ApplyZoneSpawn(karaSpawn.Position, karaSpawn.YawDegrees);
        connectedWorld.SetActiveLogicalZone("kara_urman_night");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (CountVisibleLights(karaAccentLights) <= 0
            || !HasVisibleLightEnergy(karaAccentLights))
        {
            Fail("Act 1 Kara presentation did not enable a readable exterior accent-light cue.");
            return;
        }

        connectedWorld.SetActiveLogicalZone("village_day");
        karaPlayer.ApplyZoneSpawn(villageSpawn.Position, villageSpawn.YawDegrees);

        var player = demo.DemoMain.GetNodeOrNull<FirstPersonController>("Player");
        if (player is null)
        {
            Fail("Act 1 demo did not expose its first-person player for input discovery.");
            return;
        }

        if (player.GraphicsPreset != "medium"
            || Math.Abs(GetViewport().Scaling3DScale - 0.9f) > 0.0001f
            || GetViewport().Msaa3D != Viewport.Msaa.Msaa2X
            || PainterlyMaterialLibrary.LowQualityMaterials)
        {
            Fail("Act 1 demo did not apply its declared medium graphics preset at startup.");
            return;
        }

        if (!demo.IntroVisible || !player.ModalOpen)
        {
            Fail($"Presentation placement changed the intro gate before confirmation: intro={demo.IntroVisible}, modal={player.ModalOpen}.");
            return;
        }

        var originalBindings = InputBindingService.Capture();
        InputBindingService.RebindKeyboard("interact", Key.F);
        InputBindingService.RebindKeyboard("journal", Key.K);
        InputBindingService.RebindKeyboard("move_forward", Key.Up);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!demo.IntroControlsText!.Contains("F — начать", StringComparison.Ordinal)
            || !demo.IntroControlsText.Contains("K — журнал", StringComparison.Ordinal)
            || !demo.IntroControlsText.Contains(OS.GetKeycodeString(Key.Up), StringComparison.Ordinal))
        {
            Fail("Arrival intro ignored the player's remapped keyboard controls.");
            return;
        }
        demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
        if (!demo.IntroVisible)
        {
            Fail("The old interaction key dismissed the remapped arrival intro.");
            return;
        }
        InputBindingService.Apply(originalBindings);

        var gamepadPress = new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true };
        player._UnhandledInput(gamepadPress);
        for (var frame = 0; frame < 2; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (player.CurrentInputDevice != "gamepad"
            || !demo.IntroControlsText!.Contains("Левый стик", StringComparison.Ordinal)
            || !demo.IntroControlsText.Contains("A — начать", StringComparison.Ordinal))
        {
            Fail("Act 1 intro did not switch its controls hint to gamepad wording.");
            return;
        }

        demo._UnhandledInput(gamepadPress);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (demo.IntroVisible || player.ModalOpen
            || GetViewport().GetCamera3D() != player.GetNode<Camera3D>("Head/Camera3D"))
        {
            Fail("Act 1 intro did not return the camera and input on the mapped gamepad interact action.");
            return;
        }

        if (!await CheckArrivalReach(demo, bridge!, player)) return;
        GD.Print("act1-demo-launch-smoke: dedicated entrypoint -> remapped intro -> ordinary arrival walk -> physical phone and photo readers; first-player duration not measured");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private void CaptureIntro(string directory, string name)
    {
        var error = GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, name + ".png"));
        if (error != Error.Ok) Fail($"Could not capture arrival intro {name}: {error}.");
    }

    private async Task<bool> CheckArrivalReach(Act1DemoRoot demo, RuntimeBridge bridge, FirstPersonController player)
    {
        var destination = new Vector3(3.60f, player.GlobalPosition.Y, 5.05f);
        var start = player.GlobalPosition;
        try
        {
            for (var frame = 0; frame < 420; frame++)
            {
                var delta = destination - player.GlobalPosition;
                delta.Y = 0;
                if (delta.Length() < .16f) break;
                player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
                Input.ActionPress("move_forward");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var error = new Vector2(player.GlobalPosition.X - destination.X, player.GlobalPosition.Z - destination.Z).Length();
        if (error > .24f || player.EdgeClamps > 0 || player.FallRecoveries > 0)
        {
            Fail($"Ordinary arrival walk could not reach the bench: start={start}, actual={player.GlobalPosition}, error={error:F3}.");
            return false;
        }
        var reader = (DocumentUi)GetTree().GetFirstNodeInGroup("document_ui");
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var ray = camera.GetNode<RayCast3D>("InteractionRay");
        foreach (var (targetName, document) in new[] { ("ArrivalPhoneTarget", "arrival-mother-message"), ("ArrivalPhotoTarget", "arrival-photo-evidence") })
        {
            var target = demo.DemoMain.ConnectedWorld!.FindChild(targetName, true, false) as InteractionTarget;
            if (target is null) { Fail("Arrival source has no physical target: " + targetName); return false; }
            var toward = target.GlobalPosition - camera.GlobalPosition;
            player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(toward.Y, new Vector2(toward.X, toward.Z).Length())),
                Mathf.RadToDeg(Mathf.Atan2(-toward.X, -toward.Z)));
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            ray.ForceRaycastUpdate();
            if (ray.GetCollider() != target || !target.IsAvailable())
            {
                Fail($"Standing arrival ray cannot inspect {targetName}: collider={(ray.GetCollider() as Node)?.GetPath().ToString() ?? "none"}, target={target.GlobalPosition}, available={target.IsAvailable()}, player={player.GlobalPosition}.");
                return false;
            }
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            for (var frame = 0; frame < 180 && !reader.IsOpen; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = false });
            if (!reader.IsOpen || reader.OpenDocumentId != "urman.chapter1:document/" + document)
            {
                Fail("Mapped interaction did not open the arrival source reader: " + document);
                return false;
            }
            reader._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (!bridge.IsInteractionAvailable("urman.chapter1:interaction/arrival-answer-mother")
            || bridge.IsInteractionAvailable("urman.chapter1:interaction/arrival-enter-house") || player.ModalOpen)
        {
            Fail("Reading both physical arrival sources skipped the personal choice or retained a modal.");
            return false;
        }
        return true;
    }

    private static bool HasVisibleFapMesh(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is MeshInstance3D mesh
                && mesh.Name.ToString().StartsWith("Fap_", StringComparison.Ordinal)
                && mesh.Visible)
            {
                return true;
            }

            if (HasVisibleFapMesh(child))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFapCollision(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is CollisionShape3D shape
                && shape.Name.ToString().Contains("Fap_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (HasFapCollision(child))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountActiveWorldEnvironments(Node root)
    {
        var count = 0;
        foreach (var child in root.GetChildren())
        {
            if (child is WorldEnvironment environment && environment.Environment is not null)
            {
                count++;
            }

            count += CountActiveWorldEnvironments(child);
        }

        return count;
    }

    private static int CountVisibleLights(Node root) =>
        root.GetChildren().OfType<Light3D>().Count(light => light.Visible)
        + root.GetChildren().OfType<Node>().Sum(CountVisibleLights);

    private static bool HasVisibleLightEnergy(Node root) =>
        root.GetChildren().OfType<Light3D>().Any(light => light.Visible && light.LightEnergy > 0.01f)
        || root.GetChildren().OfType<Node>().Any(HasVisibleLightEnergy);

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
