using Godot;
using Urman.Godot;

namespace Urman.Godot.Tests;

public partial class Act1DemoLaunchSmokeTest : Node
{
    public override async void _Ready()
    {
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
            || demo.IntroVisible
            || menuPlayer?.ModalOpen != true)
        {
            Fail("Act 1 main menu did not gate the demo start before any intro or gameplay input.");
            return;
        }

        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Act 1 demo did not reach the intro through the main menu New Game button.");
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

        await ToSignal(GetTree().CreateTimer(3.6), SceneTreeTimer.SignalName.Timeout);
        if (!demo.IntroVisible || !player.ModalOpen)
        {
            Fail("Act 1 intro dismissed or unlocked movement before the player confirmed the controls.");
            return;
        }

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
        if (demo.IntroVisible || player.ModalOpen)
        {
            Fail("Act 1 intro did not dismiss on the mapped gamepad interact action.");
            return;
        }

        GD.Print("act1-demo-launch-smoke: dedicated entrypoint -> first-person arrival -> dynamic keyboard/gamepad intro -> Chapter 1 campaign");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
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
