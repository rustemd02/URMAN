using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Environment = Godot.Environment;

namespace Urman.Godot;

/// <summary>
/// A familiar voice leads Aidar through the photograph. The player opens the
/// path and examines the last footprints; the unanswered call becomes the
/// voice waking him in the Niva. Story effects stay in the authored interactions.
/// </summary>
public partial class W01Controller
{
    private const string NightmareAirStream = "res://assets/audio/kara_urman_edge_ambience.wav";
    private const string NightmareStepsStream = "res://assets/audio/act1/sound_mood/boots_snow.wav";

    private static readonly float PrologueRouteLength = ComputePrologueRouteLength();

    private readonly bool[] _nightmareFired = new bool[20];
    private object? _nightmareSession;
    private double _nightmareElapsed;
    private double _nightmareRecognitionAt;
    private double _nightmareNextStep;
    private AudioStreamPlayer3D? _nightmareParallelSteps;
    private double _nightmareLastGain;
    // The dream walk is a little heavier than the village walk. Tune here.
    private const float NightmarePaceScale = .74f;
    private float _nightmareBestProgress;
    private bool _nightmareSilence;
    private bool _nightmareClimax;
    private bool _prologueCaptionThought;
    private AudioStreamPlayer? _nightmareAir;
    private OmniLight3D? _nightmareEyeLight;

    // Daylight resources of the photograph, restored for the later W01 episode.
    private Environment? _daylightEnvironment;
    private Environment? _nightmareEnvironment;
    private float _daylightEnergy = -1f;
    private Color _daylightColor;
    private bool _nightmareMoodApplied;

    // The trodden trail. It wanders a few metres either side of the straight
    // authored route (which still owns the terrain) and comes back to it at the
    // branch and for the last stretch, where the story beats are anchored.
    private const float TrailWander = 3.3f;
    private static Vector3[]? _prologueTrailNodes;
    private static Vector3[] PrologueTrailNodes => _prologueTrailNodes ??= BuildPrologueTrail();

    private static Vector3[] BuildPrologueTrail()
    {
        // Literals on purpose: static initialisation order across partial files is not guaranteed.
        var route = new[] { new Vector2(0f, 0f), new Vector2(12f, 50f), new Vector2(45f, 95f), new Vector2(80f, 140f) };
        var lengths = new float[route.Length];
        for (var index = 1; index < route.Length; index++) lengths[index] = lengths[index - 1] + route[index - 1].DistanceTo(route[index]);
        var total = lengths[^1];
        var branchAt = lengths[1];
        float Window(float s)
        {
            var start = Mathf.SmoothStep(3f, 13f, s);
            var branch = Mathf.SmoothStep(3.5f, 12f, Mathf.Abs(s - branchAt));
            var end = 1f - Mathf.SmoothStep(total - 22f, total - 11f, s);
            return start * branch * end;
        }
        var nodes = new List<Vector3>();
        for (var s = 0f; ; s += 2.4f)
        {
            var at = Mathf.Min(s, total);
            var segment = 1;
            while (segment < route.Length - 1 && lengths[segment] < at) segment++;
            var a = route[segment - 1];
            var b = route[segment];
            var direction = (b - a).Normalized();
            var point = a + direction * (at - lengths[segment - 1]);
            var wander = (Mathf.Sin(at * .236f + .8f) * .72f + Mathf.Sin(at * .531f + 2.1f) * .28f) * TrailWander * Window(at);
            point += new Vector2(-direction.Y, direction.X) * wander;
            nodes.Add(new Vector3(point.X, 0f, point.Y));
            if (at >= total) break;
        }
        return nodes.ToArray();
    }

    private static float ComputePrologueRouteLength()
    {
        var total = 0f;
        for (var index = 1; index < PrologueTrailNodes.Length; index++)
            total += new Vector2(PrologueTrailNodes[index].X - PrologueTrailNodes[index - 1].X,
                PrologueTrailNodes[index].Z - PrologueTrailNodes[index - 1].Z).Length();
        return total;
    }

    /// <summary>Metres walked along the authored prologue route and distance from it.</summary>
    private static (float Progress, float Lateral) PrologueRouteProgress(Vector2 feet)
    {
        var bestLateral = float.PositiveInfinity;
        var bestProgress = 0f;
        var walked = 0f;
        for (var index = 1; index < PrologueTrailNodes.Length; index++)
        {
            var a = new Vector2(PrologueTrailNodes[index - 1].X, PrologueTrailNodes[index - 1].Z);
            var b = new Vector2(PrologueTrailNodes[index].X, PrologueTrailNodes[index].Z);
            var segment = b - a;
            var length = segment.Length();
            var t = Mathf.Clamp((feet - a).Dot(segment) / Mathf.Max(length * length, .0001f), 0f, 1f);
            var lateral = feet.DistanceTo(a + segment * t);
            if (lateral < bestLateral)
            {
                bestLateral = lateral;
                bestProgress = walked + t * length;
            }
            walked += length;
        }
        return (bestProgress, bestLateral);
    }

    /// <summary>Local ground point at a distance along the route, pushed sideways.</summary>
    private static Vector3 PrologueRoutePoint(float progress, float side, float lift = 0f)
    {
        var walked = 0f;
        for (var index = 1; index < PrologueTrailNodes.Length; index++)
        {
            var a = new Vector2(PrologueTrailNodes[index - 1].X, PrologueTrailNodes[index - 1].Z);
            var b = new Vector2(PrologueTrailNodes[index].X, PrologueTrailNodes[index].Z);
            var length = a.DistanceTo(b);
            if (walked + length >= progress || index == PrologueTrailNodes.Length - 1)
            {
                var direction = (b - a).Normalized();
                var point = a + direction * Mathf.Clamp(progress - walked, 0f, length)
                    + new Vector2(-direction.Y, direction.X) * side;
                return new Vector3(point.X, ForestGroundHeight(point.X, point.Y) + lift, point.Y);
            }
            walked += length;
        }
        return Vector3.Zero;
    }

    // ---- Mood: the photograph at dusk going to night -------------------------------------

    private void ApplyPrologueMood(bool nightmare)
    {
        if (nightmare == _nightmareMoodApplied) return;
        var world = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
        var sun = GetNodeOrNull<DirectionalLight3D>("Daylight");
        var moon = GetNodeOrNull<OmniLight3D>("PrologueClearingMoonlight");
        if (world?.Environment is null) return;
        _nightmareMoodApplied = nightmare;
        SetPrologueThicketVisible(nightmare);
        _daylightEnvironment ??= world.Environment;
        if (sun is not null && _daylightEnergy < 0f)
        {
            _daylightEnergy = sun.LightEnergy;
            _daylightColor = sun.LightColor;
        }

        if (!nightmare)
        {
            world.Environment = _daylightEnvironment;
            if (sun is not null && _daylightEnergy >= 0f)
            {
                sun.LightEnergy = _daylightEnergy;
                sun.LightColor = _daylightColor;
            }
            if (moon is not null) moon.Visible = false;
            SetDreadActive(false);
            if (_nightmareEyeLight is { } eyeLight && GodotObject.IsInstanceValid(eyeLight)) eyeLight.Visible = false;
            if (_nightMoon is { } disc && GodotObject.IsInstanceValid(disc)) disc.Visible = false;
            if (_forestVisual is { } forest && GodotObject.IsInstanceValid(forest) && _groundVisual is { Visible: true }) forest.Visible = true;
            return;
        }

        _nightmareEnvironment ??= BuildNightmareEnvironment(_daylightEnvironment);
        EnforceNightmareLook();
        EnsureNightmareEyeLight();
        SetDreadActive(true);
    }

    // ---- Night look: tune here ------------------------------------------------------------
    // About three metres of sight around the walker, the torch beam, and the
    // sky: a moon and the black crowns against it. Everything else is dark.
    private const float NightAmbient = .05f;
    private const float NightMoonlight = .13f;
    private const float NightSightRadius = 3.6f;
    private const float NightSightEnergy = .55f;
    private const float NightFogEnd = 34f;
    private MeshInstance3D? _nightMoon;

    /// <summary>
    /// Two owners write this scene's atmosphere (the W01 profile projection and
    /// this nightmare). While the nightmare is on, its look is re-asserted every
    /// frame so the order of those writers cannot brighten the forest again.
    /// </summary>
    private void EnforceNightmareLook()
    {
        if (!_nightmareMoodApplied || _nightmareEnvironment is null) return;
        if (GetNodeOrNull<WorldEnvironment>("WorldEnvironment") is { } world && world.Environment != _nightmareEnvironment)
            world.Environment = _nightmareEnvironment;
        var sun = GetNodeOrNull<DirectionalLight3D>("Daylight");
        if (sun is not null && !Mathf.IsEqualApprox(sun.LightEnergy, NightMoonlight))
        {
            sun.LightEnergy = NightMoonlight;
            sun.LightColor = new Color(.5f, .62f, 1f);
        }
        if (GetNodeOrNull<OmniLight3D>("PrologueClearingMoonlight") is { } clearing && clearing.Visible)
            clearing.Visible = false;
        // The photograph's daytime trees are replaced by the night stand.
        if (_forestVisual is { } forest && GodotObject.IsInstanceValid(forest) && forest.Visible) forest.Visible = false;

        // The moon rides with the viewer, so it never gets nearer.
        if (GetViewport().GetCamera3D() is not { } camera) return;
        if (_nightMoon is null || !GodotObject.IsInstanceValid(_nightMoon))
        {
            var disc = new Gradient();
            disc.Offsets = new[] { 0f, .3f, .34f, 1f };
            disc.Colors = new[]
            {
                new Color(.95f, .96f, 1f, 1f), new Color(.86f, .9f, 1f, 1f),
                new Color(.5f, .62f, .9f, .22f), new Color(.4f, .5f, .8f, 0f)
            };
            _nightMoon = new MeshInstance3D
            {
                Name = "PrologueMoon",
                Mesh = new QuadMesh { Size = new Vector2(46f, 46f) },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                    DisableFog = true,
                    AlbedoTexture = new GradientTexture2D
                    {
                        Gradient = disc, Width = 128, Height = 128,
                        Fill = GradientTexture2D.FillEnum.Radial,
                        FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1f, .5f)
                    }
                }
            };
            _nightMoon.SetMeta("presentationOnly", true);
            AddChild(_nightMoon);
        }
        var toMoon = sun is not null && sun.GlobalBasis.Z.Y > .2f
            ? sun.GlobalBasis.Z.Normalized() : new Vector3(.45f, .55f, .7f).Normalized();
        _nightMoon.Visible = true;
        _nightMoon.GlobalPosition = camera.GlobalPosition + toMoon * 420f;
    }

    private static Environment BuildNightmareEnvironment(Environment daylight)
    {
        var night = (Environment)daylight.Duplicate(true);
        if (night.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
        {
            // A sky that is lighter than the forest, so crowns read against it.
            sky.SkyTopColor = new Color(.035f, .055f, .12f);
            sky.SkyHorizonColor = new Color(.1f, .14f, .24f);
            sky.GroundBottomColor = new Color(.01f, .012f, .02f);
            sky.GroundHorizonColor = new Color(.06f, .08f, .14f);
            sky.SunAngleMax = 0f;
        }
        night.AmbientLightSource = Environment.AmbientSource.Color;
        night.AmbientLightColor = new Color(.3f, .38f, .6f);
        night.AmbientLightEnergy = NightAmbient;
        night.AmbientLightSkyContribution = 0f;
        // The dark swallows the torch a few trunks away; the sky stays clear.
        night.FogEnabled = true;
        night.FogMode = Environment.FogModeEnum.Depth;
        night.FogLightColor = new Color(.008f, .011f, .02f);
        night.FogLightEnergy = 1f;
        night.FogDensity = 1f;
        night.FogDepthBegin = 3f;
        night.FogDepthEnd = NightFogEnd;
        night.FogDepthCurve = .85f;
        night.FogSkyAffect = 0f;
        night.FogSunScatter = 0f;
        night.FogAerialPerspective = 0f;
        night.AdjustmentEnabled = true;
        night.AdjustmentSaturation = .8f;
        night.AdjustmentContrast = 1.1f;
        night.AdjustmentBrightness = 1f;
        // Only eyes, the moon and the torch's hot spot are bright enough to bloom.
        night.GlowEnabled = true;
        night.GlowIntensity = .35f;
        night.GlowStrength = .9f;
        night.GlowHdrThreshold = .95f;
        return night;
    }

    // Eyes adapted to the dark: a faint cold light that follows the walker so the
    // prints stay readable. No shadows, short range.
    private void EnsureNightmareEyeLight()
    {
        if (_nightmareEyeLight is { } existing && GodotObject.IsInstanceValid(existing))
        {
            existing.Visible = true;
            return;
        }
        _nightmareEyeLight = new OmniLight3D
        {
            Name = "PrologueAdaptedEyes",
            LightColor = new Color(.74f, .8f, 1f),
            LightEnergy = NightSightEnergy,
            OmniRange = NightSightRadius,
            OmniAttenuation = 1.6f,
            LightSpecular = 0f,
            ShadowEnabled = false
        };
        AddChild(_nightmareEyeLight);
    }

    // ---- The walk ------------------------------------------------------------------------

    private void ResetNightmare(object? session)
    {
        _nightmareSession = session;
        Array.Clear(_nightmareFired);
        _nightmareElapsed = 0;
        _nightmareRecognitionAt = 0;
        _nightmareNextStep = 0;
        _nightmareLastGain = 0;
        _nightmareBestProgress = 0;
        _nightmareSilence = false;
        _nightmareClimax = false;
        _prologueCaptionThought = false;
        StopParallelSteps();
        StopNightmareSound(ref _nightmareAir, 0);
        ResetDread();
    }

    private void ProcessPrologueNightmare(FirstPersonController player, Vector2 feet, bool branchMoved, double delta)
    {
        if (_nightmareClimax) return;
        _nightmareElapsed += delta;
        var t = _nightmareElapsed;
        player.HoldPaceScale(NightmarePaceScale);
        var (p, _) = PrologueRouteProgress(feet);
        if (p > _nightmareBestProgress + .5f)
        {
            _nightmareBestProgress = p;
            _nightmareLastGain = t;
        }
        if (_nightmareEyeLight is { } eyeLight && GodotObject.IsInstanceValid(eyeLight))
            eyeLight.GlobalPosition = player.GlobalPosition + Vector3.Up * 1.9f - player.GlobalBasis.Z * .6f;

        if (Fire(0, t > .7))
        {
            var ahead = PrologueRoutePoint(Math.Min(p + 20f, PrologueRouteLength), 1.5f, 1.4f);
            var length = PlayPrologueForestVoice(new Vector2(ahead.X, ahead.Z), "forest-name-call", "«Айдар…»", -3f);
            _nightmareRecognitionAt = t + Math.Max(2.4, length) + .25;
        }
        if (Fire(1, _nightmareRecognitionAt > 0 && t >= _nightmareRecognitionAt))
            ShowNightmareThought("Марат?.. Нет. Не может быть.", 4);
        if (Fire(9, t > .4)) StartNightmareSound(ref _nightmareAir, NightmareAirStream, -24f);

        ProcessDread(player, p, t, (float)delta);
        // The branch across the trail is lifted away by nothing anyone can see:
        // the walk never stops on a chore.
        if (!branchMoved && !_branchMoveInProgress
            && feet.DistanceTo(new Vector2(BranchF01.X, BranchF01.Z)) <= 7.5f
            && BeginBranchMove(PrologueBranchInteractionId) && Fire(5, true))
            UiFoley.PlayWorld(this, ToGlobal(BranchF01 + Vector3.Up * 1.2f), "forest/branch_crack", -2f, 30f, 6f);

        if (Fire(4, branchMoved))
        {
            if (p < 80f)
            {
                var ahead = PrologueRoutePoint(p + 23f, 1.5f);
                PlayPrologueForestVoice(new Vector2(ahead.X, ahead.Z), "forest-call-hey", "«Эй… эй… эй…»", -5f);
            }
        }
        if (branchMoved && p >= 76f && p < PrologueRouteLength - 18f && !_nightmareSilence)
        {
            // One consistent presence: it walks when Aidar walks and stops with him.
            var walking = new Vector2(player.Velocity.X, player.Velocity.Z).Length() > .4f;
            if (!walking) StopParallelSteps();
            else if (t >= _nightmareNextStep)
            {
                StopParallelSteps();
                _nightmareNextStep = t + 1.25;
                _nightmareParallelSteps = PlayNightmareSample(NightmareStepsStream,
                    ToGlobal(PrologueRoutePoint(p + 1f, -8f, .2f)), -15f, 24f, 4f);
            }
        }
        else StopParallelSteps();
        if (Fire(8, p >= PrologueRouteLength - 18f))
        {
            _nightmareSilence = true;
            StopParallelSteps();
            StopNightmareSound(ref _nightmareAir, 2);
        }
        if (t - _nightmareLastGain > 24 && t > 20 && !_nightmareSilence)
        {
            _nightmareLastGain = t;
            ShowNightmareThought("Следы уходят дальше по тропе, в чащу.", 4);
        }
    }

    private bool Fire(int index, bool condition)
    {
        if (_nightmareFired[index] || !condition) return false;
        _nightmareFired[index] = true;
        return true;
    }

    private void ShowNightmareThought(string text, double seconds)
    {
        if (_prologueVoiceCaption is not { } caption) return;
        caption.Text = text;
        _prologueCaptionRemaining = seconds;
        _prologueCaptionThought = true;
        caption.Visible = true;
    }

    private AudioStreamPlayer3D? PlayNightmareSample(string path, Vector3 at, float volumeDb, float maxDistance, float unitSize)
    {
        if (DisplayServer.GetName() == "headless" || !ResourceLoader.Exists(path)
            || ResourceLoader.Load<AudioStream>(path) is not { } stream) return null;
        AudioSettingsService.EnsureBuses();
        var player = new AudioStreamPlayer3D
        {
            Stream = stream, Bus = AudioSettingsService.SfxBus, VolumeDb = volumeDb,
            MaxDistance = maxDistance, UnitSize = unitSize
        };
        AddChild(player);
        player.GlobalPosition = at;
        player.Finished += player.QueueFree;
        player.Play();
        return player;
    }

    private void StopParallelSteps()
    {
        var steps = _nightmareParallelSteps;
        _nightmareParallelSteps = null;
        if (steps is null || !GodotObject.IsInstanceValid(steps)) return;
        steps.Stop();
        steps.QueueFree();
    }

    // ---- The end of the prints -----------------------------------------------------------

    /// <summary>The player has examined the last print. The answer comes from behind.</summary>
    private async void RunNightmareClimaxAsync(RuntimeBridge bridge, object session, FirstPersonController player)
    {
        if (_nightmareClimax) return;
        _nightmareClimax = true;
        _prologueExitDispatching = true;
        _prologueEndTarget?.SetPresentationEnabled(false);
        var cut = false;
        var transitionHeld = false;
        var committed = false;
        try
        {
            StopParallelSteps();
            StopNightmareSound(ref _nightmareAir, 1.5);
            PrologueVoice.StopSpatial(this);
            ShowNightmareThought("Следы обрываются. Дальше — чистый снег.", 3);
            if (!await NightmareWaitAsync(2.6, session)) return;

            // He never gets to answer.
            player.SetSessionTransition(true);
            transitionHeld = true;
            if (_prologueVoiceCaption is { } spoken) spoken.Hide();
            _prologueCaptionRemaining = 0;
            if (!await RunDreadAttackAsync(player, session)) return;
            PrologueDreamCut.Show(GetTree(), fadeSeconds: .1);
            cut = true;
            if (!await NightmareWaitAsync(.9, session)) return;
            ReleaseFallCamera(player);
            PrologueVoice.StopSpatial(this);
            PrologueDreamCut.PlayVoiceMorph(GetTree());
            if (_prologueVoiceCaption is { } caption) caption.Hide();
            _prologueCaptionRemaining = 0;
            if (!await NightmareWaitAsync(.9, session)) return;

            // RuntimeBridge owns the actual scene transition and its checkpoint.
            player.SetSessionTransition(false);
            transitionHeld = false;
            committed = await bridge.DispatchInteractionAsync(PrologueEndInteractionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W01 prologue exit failed: {error.Message}");
        }
        finally
        {
            var sameSession = GodotObject.IsInstanceValid(bridge) && ReferenceEquals(bridge.SessionIdentity, session);
            if (transitionHeld && sameSession && GodotObject.IsInstanceValid(player))
                player.SetSessionTransition(false);
            if (GodotObject.IsInstanceValid(this)) ReleaseFallCamera(GodotObject.IsInstanceValid(player) ? player : null);
            if (!committed && cut) PrologueDreamCut.Release(fadeSeconds: .4);
            if (!committed && GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
                && sameSession && bridge.ActiveSceneId == PrologueSceneId)
            {
                _nightmareClimax = false;
                _torchOverride = null;
                _prologueExitDispatching = false;
                _prologueEndTarget?.SetPresentationEnabled(true);
            }
        }
    }

    private async Task<bool> NightmareWaitAsync(double seconds, object session)
    {
        var elapsed = 0d;
        while (elapsed < seconds)
        {
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()) return false;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
                || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
                || !ReferenceEquals(bridge.SessionIdentity, session) || bridge.ActiveSceneId != PrologueSceneId)
                return false;
            if (ProloguePresentationPaused()
                || GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: true, SessionTransitionActive: false })
                continue;
            elapsed += GetProcessDeltaTime();
        }
        return true;
    }

    private bool ProloguePresentationPaused() => GetTree().Paused || !DisplayServer.WindowIsFocused()
        || MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true };

    // ---- Sound beds ----------------------------------------------------------------------

    private void StartNightmareSound(ref AudioStreamPlayer? slot, string path, float volumeDb)
    {
        if (DisplayServer.GetName() == "headless" || !ResourceLoader.Exists(path)) return;
        if (slot is { } existing && GodotObject.IsInstanceValid(existing)) return;
        if (ResourceLoader.Load<AudioStream>(path) is not { } stream) return;
        AudioSettingsService.EnsureBuses();
        var player = new AudioStreamPlayer
        {
            Name = "Nightmare_" + System.IO.Path.GetFileNameWithoutExtension(path),
            Stream = stream,
            Bus = AudioSettingsService.AmbienceBus,
            VolumeDb = -50f
        };
        AddChild(player);
        // The one-shot files become beds: each restarts until it is stopped.
        player.Finished += () => { if (GodotObject.IsInstanceValid(player) && player.IsInsideTree()) player.Play(); };
        player.Play();
        player.CreateTween().TweenProperty(player, "volume_db", volumeDb, 3.0);
        slot = player;
    }

    private static void StopNightmareSound(ref AudioStreamPlayer? slot, double fadeSeconds)
    {
        var player = slot;
        slot = null;
        if (player is null || !GodotObject.IsInstanceValid(player)) return;
        if (fadeSeconds <= 0 || !player.IsInsideTree())
        {
            player.QueueFree();
            return;
        }
        var fade = player.CreateTween();
        fade.TweenProperty(player, "volume_db", -60f, fadeSeconds);
        fade.TweenCallback(Callable.From(player.QueueFree));
    }
}
