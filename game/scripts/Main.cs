using Godot;

namespace Urman.Godot;

public partial class Main : Node3D
{
    private static readonly IReadOnlyDictionary<string, string> ZoneScenes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["village_day"] = "res://scenes/zones/style_benchmark_day_street.tscn",
        ["house_old_pc"] = "res://scenes/zones/style_benchmark_house_pc.tscn",
        ["fap_clinic"] = "res://scenes/zones/chapter1_fap_clinic.tscn",
        ["zirat_road"] = "res://scenes/zones/chapter1_zirat_road.tscn",
        ["kara_urman_night"] = "res://scenes/zones/style_benchmark_kara_urman_night.tscn",
        ["fullgame_act2_house"] = "res://scenes/zones/fullgame/act2_house.tscn",
        ["fullgame_act2_river"] = "res://scenes/zones/fullgame/act2_river.tscn",
        ["fullgame_act2_mosque"] = "res://scenes/zones/fullgame/act2_mosque.tscn",
        ["fullgame_act2_council"] = "res://scenes/zones/fullgame/act2_council.tscn",
        ["fullgame_act3_archive"] = "res://scenes/zones/fullgame/act3_archive.tscn",
        ["fullgame_act3_soviet"] = "res://scenes/zones/fullgame/act3_soviet.tscn",
        ["fullgame_act3_water"] = "res://scenes/zones/fullgame/act3_water.tscn",
        ["fullgame_act4_tukay"] = "res://scenes/zones/fullgame/act4_tukay.tscn",
        ["fullgame_act4_1552"] = "res://scenes/zones/fullgame/act4_1552.tscn",
        ["fullgame_act4_pact"] = "res://scenes/zones/fullgame/act4_pact.tscn",
        ["fullgame_act5_boundary"] = "res://scenes/zones/fullgame/act5_boundary.tscn",
        ["fullgame_act5_epilogue"] = "res://scenes/zones/fullgame/act5_epilogue.tscn"
    };

    private Node3D _zoneHost = null!;
    private ColorRect? _zoneTransition;
    private Tween? _zoneTransitionTween;
    private bool _hasLoadedInitialZone;
    private Act1ConnectedWorld? _connectedWorld;

    public string ActiveZoneScenePath { get; private set; } = string.Empty;

    /// <summary>
    /// Read-only presentation diagnostic for the last compact-zone placement.
    /// It lets smoke tests prove the destination-facing contract without
    /// treating a player's subsequent mouse/gamepad look as a failure.
    /// </summary>
    public float LastSpawnYawDegrees { get; private set; }

    public Act1ConnectedWorld? ConnectedWorld => _connectedWorld;

    [Export]
    public string InitialZoneId { get; set; } = "village_day";

    [Export]
    public string InitialSpawnPointId { get; set; } = "arrival";

    /// <summary>
    /// Presentation-only transition used by the Act 1 demo. Full-game scenes
    /// keep the default false value until their own transition contract exists.
    /// </summary>
    [Export]
    public bool EnableZoneTransitionFade { get; set; }

    /// <summary>
    /// Opt-in presentation mode for the dedicated Act I demo. The default
    /// remains the existing one-zone loader used by ordinary tests and the
    /// full-game entrypoint.
    /// </summary>
    [Export]
    public bool EnableAct1ConnectedWorld { get; set; }

    public override void _Ready()
    {
        AddToGroup("zone_manager");
        _zoneHost = GetNode<Node3D>("ZoneHost");
        if (EnableZoneTransitionFade)
        {
            BuildZoneTransitionOverlay();
        }

        if (EnableAct1ConnectedWorld)
        {
            _connectedWorld = new Act1ConnectedWorld
            {
                Name = Act1WorldLayout.ConnectedWorldRootName
            };
            _zoneHost.AddChild(_connectedWorld);
        }

        SwitchZone(InitialZoneId, InitialSpawnPointId);
        _hasLoadedInitialZone = true;
        GD.Print("УРМАН Godot vertical slice ready: Painterly Low-Poly / first person");
    }

    public void SwitchZone(string zoneId, string spawnPointId)
    {
        if (!ZoneScenes.TryGetValue(zoneId, out var scenePath))
        {
            GD.PushError($"Unknown zone {zoneId}.");
            return;
        }

        if (_connectedWorld is not null)
        {
            if (!Act1WorldLayout.ContainsZone(zoneId))
            {
                GD.PushError($"Connected Act I world cannot enter non-Act-I zone {zoneId}.");
                return;
            }

            if (!_connectedWorld.TryGetWorldSpawn(zoneId, spawnPointId, out var connectedSpawn))
            {
                GD.PushError($"Connected Act I world has no mapped spawn '{zoneId}@{spawnPointId}'; transition aborted.");
                return;
            }

            _connectedWorld.SetActiveLogicalZone(zoneId);
            ActiveZoneScenePath = scenePath;
            if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController connectedPlayer)
            {
                connectedPlayer.ApplyZoneSpawn(connectedSpawn.Position, connectedSpawn.YawDegrees);
                LastSpawnYawDegrees = connectedSpawn.YawDegrees;
            }

            if (GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge connectedBridge)
            {
                connectedBridge.SetWorldLocation(zoneId, spawnPointId);
            }

            if (GetTree().GetFirstNodeInGroup("ambient_audio") is AmbientAudioDirector connectedAmbience)
            {
                connectedAmbience.SetZone(zoneId, spawnPointId);
            }

            if (_hasLoadedInitialZone)
            {
                PlayZoneTransition();
            }

            GD.Print($"zone-loaded: {zoneId}@{spawnPointId} connected-world={_connectedWorld.PersistentInstanceIdentity}");
            return;
        }

        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed is null)
        {
            GD.PushError($"Zone scene is missing: {scenePath}.");
            return;
        }

        foreach (var child in _zoneHost.GetChildren())
        {
            child.QueueFree();
        }

        var zone = packed.Instantiate<Node3D>();
        if (zone is FullGameZone fullGameZone)
        {
            fullGameZone.ZoneId = zoneId;
        }

        _zoneHost.AddChild(zone);
        ActiveZoneScenePath = scenePath;
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            var spawn = SpawnTransform(zoneId, spawnPointId);
            player.ApplyZoneSpawn(spawn.Position, spawn.YawDegrees);
            LastSpawnYawDegrees = spawn.YawDegrees;
        }

        if (GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge bridge)
        {
            bridge.SetWorldLocation(zoneId, spawnPointId);
        }

        if (GetTree().GetFirstNodeInGroup("ambient_audio") is AmbientAudioDirector ambience)
        {
            ambience.SetZone(zoneId, spawnPointId);
        }

        if (_hasLoadedInitialZone)
        {
            PlayZoneTransition();
        }

        GD.Print($"zone-loaded: {zoneId}@{spawnPointId}");
    }

    private void BuildZoneTransitionOverlay()
    {
        var layer = new CanvasLayer
        {
            Name = "Act1ZoneTransition",
            Layer = 80
        };
        layer.AddToGroup("act1_demo_transition");
        AddChild(layer);
        _zoneTransition = new ColorRect
        {
            Name = "Fade",
            Color = new Color(0.008f, 0.012f, 0.011f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        layer.AddChild(_zoneTransition);
    }

    private void PlayZoneTransition()
    {
        if (_zoneTransition is null || !GodotObject.IsInstanceValid(_zoneTransition))
        {
            return;
        }

        // UIUX-010: reduced motion replaces the animated transition with an
        // instant cut; the overlay clears immediately.
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ReducedMotion: true })
        {
            _zoneTransitionTween?.Kill();
            _zoneTransitionTween = null;
            _zoneTransition.Color = new Color(0.008f, 0.012f, 0.011f, 0f);
            return;
        }

        _zoneTransitionTween?.Kill();
        _zoneTransition.Color = new Color(0.008f, 0.012f, 0.011f, 0.68f);
        _zoneTransitionTween = CreateTween();
        _zoneTransitionTween.TweenProperty(
            _zoneTransition,
            "color",
            new Color(0.008f, 0.012f, 0.011f, 0.18f),
            0.12f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _zoneTransitionTween.TweenProperty(
            _zoneTransition,
            "color",
            new Color(0.008f, 0.012f, 0.011f, 0f),
            0.42f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    private readonly record struct SpawnTransformData(Vector3 Position, float YawDegrees);

    private static SpawnTransformData SpawnTransform(string zoneId, string spawnPointId) => (zoneId, spawnPointId) switch
    {
        // Godot yaw 0 faces -Z. These Act 1 destinations deliberately face
        // the next route landmark rather than preserving the doorway-facing
        // direction from the previous compact zone.
        ("village_day", "from_house") => new(new Vector3(-2.2f, 0.05f, 2.4f), 0f),
        ("village_day", "from_forest") => new(new Vector3(1.8f, 0.05f, -12.5f), 180f),
        ("house_old_pc", _) => new(new Vector3(0, 0.05f, 3.8f), 0f),
        ("fap_clinic", _) => new(new Vector3(0, 0.05f, 4.8f), 0f),
        ("zirat_road", _) => new(new Vector3(0, 0.05f, 16.5f), 0f),
        ("kara_urman_night", _) => new(new Vector3(0, 0.05f, 12), 0f),
        ("fullgame_act2_house", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act2_river", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act2_mosque", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act2_council", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act3_archive", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act3_soviet", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act3_water", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act4_tukay", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act4_1552", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act4_pact", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act5_boundary", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act5_epilogue", _) => new(new Vector3(0, 0.05f, 8), 0f),
        _ => new(new Vector3(0, 0.05f, 9), 0f)
    };
}
