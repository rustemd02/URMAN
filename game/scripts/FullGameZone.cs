using Godot;

namespace Urman.Godot;

/// <summary>
/// Compact 3D presentation adapter for the authored Acts 2–5 campaign.
/// Narrative ownership stays in RuntimeBridge; this node only turns compiled
/// scene interactions into walkable Godot targets and routes into the next
/// compact zone.
/// </summary>
public partial class FullGameZone : Node3D
{
    private static readonly IReadOnlyDictionary<string, string> SceneByZone = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["fullgame_act2_house"] = "urman.fullgame:scene/act2-house",
        ["fullgame_act2_river"] = "urman.fullgame:scene/act2-river",
        ["fullgame_act2_mosque"] = "urman.fullgame:scene/act2-mosque",
        ["fullgame_act2_council"] = "urman.fullgame:scene/act2-council",
        ["fullgame_act3_archive"] = "urman.fullgame:scene/act3-archive",
        ["fullgame_act3_soviet"] = "urman.fullgame:scene/act3-soviet",
        ["fullgame_act3_water"] = "urman.fullgame:scene/act3-suanasy",
        ["fullgame_act4_tukay"] = "urman.fullgame:scene/act4-tukay",
        ["fullgame_act4_1552"] = "urman.fullgame:scene/act4-1552",
        ["fullgame_act4_pact"] = "urman.fullgame:scene/act4-pact",
        ["fullgame_act5_boundary"] = "urman.fullgame:scene/act5-boundary",
        ["fullgame_act5_epilogue"] = "urman.fullgame:scene/act5-epilogue"
    };

    private static readonly IReadOnlyDictionary<string, string> ZoneByScene =
        SceneByZone.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    [Export]
    public string ZoneId { get; set; } = "fullgame_act2_house";

    public override void _Ready()
    {
        BuildEnvironment();
        CallDeferred(nameof(BuildCompiledInteractions));
    }

    private void BuildEnvironment()
    {
        var night = ZoneId.Contains("water", StringComparison.Ordinal)
            || ZoneId.Contains("boundary", StringComparison.Ordinal)
            || ZoneId.Contains("pact", StringComparison.Ordinal);
        var dusk = ZoneId.Contains("tukay", StringComparison.Ordinal)
            || ZoneId.Contains("1552", StringComparison.Ordinal)
            || ZoneId.Contains("soviet", StringComparison.Ordinal);
        var environment = new global::Godot.Environment
        {
            BackgroundMode = global::Godot.Environment.BGMode.Sky,
            Sky = new Sky
            {
                RadianceSize = Sky.RadianceSizeEnum.Size256,
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = night ? Color.FromHtml("111c26") : dusk ? Color.FromHtml("554e58") : Color.FromHtml("476572"),
                    SkyHorizonColor = night ? Color.FromHtml("52626a") : dusk ? Color.FromHtml("c08e72") : Color.FromHtml("c5c5af"),
                    GroundBottomColor = night ? Color.FromHtml("111719") : Color.FromHtml("40483c"),
                    GroundHorizonColor = night ? Color.FromHtml("384a4c") : Color.FromHtml("9c9b82"),
                    SunAngleMax = night ? 0.8f : 3.5f,
                    SunCurve = 0.12f
                }
            },
            AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
            AmbientLightColor = night ? Color.FromHtml("607681") : dusk ? Color.FromHtml("a78b83") : Color.FromHtml("a8a58f"),
            AmbientLightEnergy = night ? 0.42f : dusk ? 0.58f : 0.72f,
            FogEnabled = true,
            FogLightColor = night ? Color.FromHtml("3c5059") : Color.FromHtml("82918a"),
            FogDensity = night ? 0.012f : 0.005f,
            FogHeight = 1.1f,
            FogHeightDensity = night ? 0.26f : 0.08f,
            TonemapMode = global::Godot.Environment.ToneMapper.Filmic
        };
        AddChild(new WorldEnvironment { Name = "WorldEnvironment", Environment = environment });

        AddChild(new DirectionalLight3D
        {
            Name = "KeyLight",
            RotationDegrees = night ? new(-48, -34, 0) : new(-50, -28, 0),
            LightColor = night ? Color.FromHtml("8da8b7") : dusk ? Color.FromHtml("e0a07a") : Color.FromHtml("e7d5af"),
            LightEnergy = night ? 0.62f : dusk ? 0.84f : 1.08f,
            ShadowEnabled = true
        });

        var ground = new StaticBody3D { Name = "Ground" };
        ground.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(34, 0.25f, 34) },
            Position = new Vector3(0, -0.125f, 0),
            MaterialOverride = PainterlyMaterialLibrary.ForColor(night ? "2d3933" : "5c604b", "earth")
        });
        ground.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(34, 0.25f, 34) },
            Position = new Vector3(0, -0.125f, 0)
        });
        AddChild(ground);

        // Shared compact zones now use the same authored low-poly road relief
        // as the style benchmarks. It remains presentation-owned: the kernel
        // never sees this mesh, while the matching cell colliders keep the
        // existing first-person path walkable without a physics fallback.
        var path = PainterlyEnvironmentDetails.AddRoadRelief(
            this,
            "WalkablePath",
            3.6f,
            28f,
            new Vector3(0, 0, -1.5f),
            night ? "3c4038" : dusk ? "665147" : "75604b");
        path.SetMeta("ownership", "presentation-only");
        path.SetMeta("status", "authored-road-relief-production-candidate");

        var treeColor = night ? "263a35" : dusk ? "3d4b3b" : "40563e";
        for (var index = 0; index < 14; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            PainterlyEnvironmentDetails.AddPine(this,
                new Vector3(side * (4.4f + index % 3 * 1.4f), 0, 10 - index * 1.65f),
                4.8f + index % 4 * 0.45f);
        }

        for (var index = 0; index < 10; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            PainterlyEnvironmentDetails.AddShrub(this,
                new Vector3(side * (2.7f + index % 4 * 0.6f), 0, 9 - index * 2.0f),
                0.7f + index % 3 * 0.15f,
                treeColor);
        }

        AddChild(new OmniLight3D
        {
            Name = "LocalStoryLight",
            Position = new Vector3(-2.5f, 2.3f, -6.2f),
            LightColor = night ? Color.FromHtml("df9b63") : Color.FromHtml("d8a76b"),
            LightEnergy = night ? 3.3f : 1.4f,
            OmniRange = 7.0f,
            ShadowEnabled = true
        });

        FullGameZoneDressing.Build(this, ZoneId);
    }

    private void BuildCompiledInteractions()
    {
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null || !SceneByZone.TryGetValue(ZoneId, out var sceneId))
        {
            return;
        }

        var scene = bridge.RequireScene(sceneId);
        for (var index = 0; index < scene.Interactions.Count; index++)
        {
            var interaction = scene.Interactions[index];
            var target = new InteractionTarget
            {
                Name = $"AuthoredInteraction{index + 1}",
                Position = FullGameInteractionLayout.PositionFor(ZoneId, interaction.Id, index),
                InteractionId = interaction.Id,
                Prompt = bridge.ResolveText(interaction.LabelTextId),
                DialogueId = interaction.TargetDialogueId ?? string.Empty,
                DocumentId = interaction.TargetDocumentId ?? string.Empty,
                JournalEntryId = interaction.TargetJournalEntryId ?? string.Empty,
                TargetZoneId = interaction.TargetSceneId is not null && ZoneByScene.TryGetValue(interaction.TargetSceneId, out var nextZone)
                    ? nextZone
                    : string.Empty,
                TargetSpawnPointId = "entry"
            };
            target.SetMeta("presentationLayout", "authored-zone-anchor");
            target.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.0f, 1.35f, 0.58f) },
                MaterialOverride = PainterlyMaterialLibrary.ForColor(index % 2 == 0 ? "87664f" : "5c6c60", "wood")
            });
            target.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(1.0f, 1.35f, 0.58f) }
            });
            AddChild(target);
        }

        if (scene.Interactions.Count == 0)
        {
            AddChild(new Label3D
            {
                Text = bridge.ResolveText("urman.fullgame:text/scene-act5-epilogue"),
                Position = new Vector3(0, 1.7f, -4.0f),
                Modulate = Color.FromHtml("d8c59c"),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
            });
        }
    }
}
