using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Environment = Godot.Environment;

namespace Urman.Godot;

/// <summary>
/// Prologue direction pass (author request 2026-11-06). The first minutes are a
/// nightmare inside the forest photograph: dusk turning to night, fresh prints
/// leading on, a voice calling Aidar by name, something that is never fully
/// seen. Nothing is collected and nothing is solved: the only goal is to follow
/// the prints to whoever is calling. The branch that once had to be moved by
/// hand now opens by itself, and the walk ends on a hard cut that becomes
/// Mansur's voice in the Niva.
///
/// Everything here is presentation of the existing pw-prologue scene: the same
/// authored interactions commit the same facts, so saves, skip and the smoke
/// flow keep their contracts.
/// </summary>
public partial class W01Controller
{
    private const string NightmareBedStream = "res://assets/audio/act1/foley/forest/dread_drone.wav";
    private const string NightmareHeartStream = "res://assets/audio/act1/foley/forest/heartbeat.wav";
    private const float NightmareBranchOpenDistance = 8.5f;

    private static readonly float PrologueRouteLength = ComputePrologueRouteLength();

    private readonly bool[] _nightmareFired = new bool[20];
    private readonly RandomNumberGenerator _nightmareRng = new() { Seed = 0x55524d35 };
    private readonly List<Node3D> _nightmareGlints = new();
    private readonly Dictionary<Node3D, bool> _nightmareGlintClosed = new();
    private object? _nightmareSession;
    private double _nightmareElapsed;
    private double _nightmareNextAmbient;
    private double _nightmareGlintsAt = -1;
    private double _nightmareLastGain;
    private float _nightmareBestProgress;
    private bool _nightmareSilence;
    private bool _nightmareClimax;
    private bool _prologueCaptionThought;
    private AudioStreamPlayer? _nightmareBed;
    private AudioStreamPlayer? _nightmareHeart;
    private OmniLight3D? _nightmareEyeLight;

    // Daylight resources of the photograph, restored for the later W01 episode.
    private Environment? _daylightEnvironment;
    private Environment? _nightmareEnvironment;
    private float _daylightEnergy = -1f;
    private Color _daylightColor;
    private bool _nightmareMoodApplied;

    private static float ComputePrologueRouteLength()
    {
        var total = 0f;
        for (var index = 1; index < PrologueRouteNodes.Length; index++)
            total += new Vector2(PrologueRouteNodes[index].X - PrologueRouteNodes[index - 1].X,
                PrologueRouteNodes[index].Z - PrologueRouteNodes[index - 1].Z).Length();
        return total;
    }

    /// <summary>Metres walked along the authored prologue route and distance from it.</summary>
    private static (float Progress, float Lateral) PrologueRouteProgress(Vector2 feet)
    {
        var bestLateral = float.PositiveInfinity;
        var bestProgress = 0f;
        var walked = 0f;
        for (var index = 1; index < PrologueRouteNodes.Length; index++)
        {
            var a = new Vector2(PrologueRouteNodes[index - 1].X, PrologueRouteNodes[index - 1].Z);
            var b = new Vector2(PrologueRouteNodes[index].X, PrologueRouteNodes[index].Z);
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
        for (var index = 1; index < PrologueRouteNodes.Length; index++)
        {
            var a = new Vector2(PrologueRouteNodes[index - 1].X, PrologueRouteNodes[index - 1].Z);
            var b = new Vector2(PrologueRouteNodes[index].X, PrologueRouteNodes[index].Z);
            var length = a.DistanceTo(b);
            if (walked + length >= progress || index == PrologueRouteNodes.Length - 1)
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
            if (_nightmareEyeLight is { } eyeLight && GodotObject.IsInstanceValid(eyeLight)) eyeLight.Visible = false;
            return;
        }

        _nightmareEnvironment ??= BuildNightmareEnvironment(_daylightEnvironment);
        world.Environment = _nightmareEnvironment;
        if (sun is not null)
        {
            // A low cold moon: shapes and prints still read, colour does not.
            sun.LightEnergy = .24f;
            sun.LightColor = new Color(.62f, .7f, .94f);
        }
        // The authored clearing light becomes the pale gap the prints lead to.
        if (moon is not null)
        {
            moon.Visible = true;
            moon.LightEnergy = .9f;
            moon.OmniRange = 26f;
        }
        EnsureNightmareEyeLight();
    }

    private static Environment BuildNightmareEnvironment(Environment daylight)
    {
        var night = (Environment)daylight.Duplicate(true);
        if (night.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
        {
            sky.SkyTopColor = new Color(.05f, .07f, .11f);
            sky.SkyHorizonColor = new Color(.17f, .2f, .27f);
            sky.GroundBottomColor = new Color(.04f, .05f, .07f);
            sky.GroundHorizonColor = new Color(.17f, .2f, .27f);
        }
        night.AmbientLightColor = new Color(.32f, .38f, .5f);
        night.AmbientLightEnergy = .36f;
        night.AmbientLightSkyContribution = .3f;
        // Closer, darker fog: more dread and less to draw.
        night.FogLightColor = new Color(.09f, .11f, .16f);
        night.FogDensity = .93f;
        night.FogDepthBegin = 5f;
        night.FogDepthEnd = 64f;
        night.FogDepthCurve = 1.15f;
        night.FogSunScatter = 0f;
        night.FogAerialPerspective = .1f;
        night.AdjustmentEnabled = true;
        night.AdjustmentSaturation = .72f;
        night.AdjustmentContrast = 1.08f;
        night.AdjustmentBrightness = .96f;
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
            LightEnergy = .5f,
            OmniRange = 9.5f,
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
        _nightmareNextAmbient = 5;
        _nightmareGlintsAt = -1;
        _nightmareLastGain = 0;
        _nightmareBestProgress = 0;
        _nightmareSilence = false;
        _nightmareClimax = false;
        _prologueCaptionThought = false;
        _nightmareGlintClosed.Clear();
        foreach (var glint in _nightmareGlints)
            if (GodotObject.IsInstanceValid(glint)) glint.Visible = false;
        StopNightmareSound(ref _nightmareBed, 0);
        StopNightmareSound(ref _nightmareHeart, 0);
    }

    private void ProcessPrologueNightmare(FirstPersonController player, Vector2 feet, bool branchMoved, double delta)
    {
        if (_nightmareClimax) return;
        _nightmareElapsed += delta;
        var t = _nightmareElapsed;
        var (p, _) = PrologueRouteProgress(feet);
        if (p > _nightmareBestProgress + .5f)
        {
            _nightmareBestProgress = p;
            _nightmareLastGain = t;
        }
        if (_nightmareEyeLight is { } eyeLight && GodotObject.IsInstanceValid(eyeLight))
            eyeLight.GlobalPosition = player.GlobalPosition + Vector3.Up * 1.9f - player.GlobalBasis.Z * .6f;

        if (Fire(0, t > 1.6)) ShowNightmareThought("Следы. Совсем свежие — кто-то прошёл здесь только что.", 4.5);
        if (Fire(1, t > 2.4)) StartNightmareSound(ref _nightmareBed, NightmareBedStream, -19f);
        if (Fire(2, p >= 17f)) NightmareSoundAround(player, "forest/wolf_far", 95f, 4f, -5f, 260f, 40f);
        if (Fire(3, p >= 29f)) KnockInTheTrees(player);

        // The branch across the path is lifted away by nothing anyone can see.
        // A rejected or interrupted move is simply tried again: the hand prompt
        // is hidden in the prologue, so this is the only way on.
        if (!branchMoved && !_branchMoveInProgress
            && feet.DistanceTo(new Vector2(BranchF01.X, BranchF01.Z)) <= NightmareBranchOpenDistance)
        {
            BeginBranchMove(PrologueBranchInteractionId);
            if (_branchMoveInProgress && Fire(4, true))
            {
                var at = ToGlobal(BranchF01 + Vector3.Up * 1.2f);
                UiFoley.PlayWorld(this, at, "forest/branch_crack", -2f, 30f, 6f);
                var whisper = new Vector2(BranchF01.X, BranchF01.Z) + new Vector2(2.5f, 1.5f);
                PlayPrologueForestVoice(whisper, "forest-name-call", "«Айдар…»", -9f);
            }
        }

        if (branchMoved && Fire(5, p >= 64f)) RunAcrossThePath(player, p + 19f);
        if (Fire(6, p >= 92f)) _nightmareGlintsAt = t;
        if (_nightmareGlintsAt >= 0) UpdateNightmareGlints(player, t - _nightmareGlintsAt);
        if (Fire(7, p >= 127f)) GlimpseAtTheScream(player);
        if (Fire(8, p >= 141f))
        {
            // The forest holds its breath before the end of the prints.
            _nightmareSilence = true;
            StartNightmareSound(ref _nightmareHeart, NightmareHeartStream, -16f);
            NightmareSoundAround(player, "forest/low_moan", 24f, .5f, -7f, 80f, 10f);
        }
        if (t >= _nightmareNextAmbient && !_nightmareSilence)
        {
            _nightmareNextAmbient = t + 4.5 + _nightmareRng.Randf() * 6;
            NightmareAmbientEvent(player, p);
        }
        if (t - _nightmareLastGain > 24 && t > 20 && !_nightmareSilence)
        {
            _nightmareLastGain = t;
            ShowNightmareThought(branchMoved ? "Следы ведут дальше, к светлому просвету." : "Следы уходят вперёд, под низкие ветви.", 4);
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

    private void NightmareAmbientEvent(FirstPersonController player, float progress)
    {
        var roll = _nightmareRng.Randf();
        if (roll < .32f)
            NightmareSoundAround(player, _nightmareRng.Randf() < .5f ? "forest/owl_eagle" : "forest/owl_tawny",
                _nightmareRng.RandfRange(30f, 60f), _nightmareRng.RandfRange(12f, 20f), -6f, 140f, 16f);
        else if (roll < .55f)
            NightmareSoundAround(player, "forest/brush_rustle", _nightmareRng.RandfRange(5f, 12f), .4f, -7f, 30f, 4f);
        else if (roll < .74f)
            NightmareSoundAround(player, "forest/branch_crack", _nightmareRng.RandfRange(12f, 32f), 2f, -7f, 70f, 7f);
        else if (roll < .88f && progress > 80f)
            NightmareSoundAround(player, "forest/strange_clicks", _nightmareRng.RandfRange(9f, 18f), 1.5f, -10f, 45f, 5f);
        else
            UiFoley.PlayWorld(this, player.GlobalPosition + Vector3.Up * 6f, "forest/wind_gust", -13f, 60f, 20f);
    }

    private Vector3 NightmareSoundAround(FirstPersonController player, string sample, float distance, float height,
        float volumeDb, float maxDistance, float unitSize)
    {
        var angle = _nightmareRng.RandfRange(0, Mathf.Tau);
        var at = player.GlobalPosition + new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance);
        UiFoley.PlayWorld(this, at, sample, volumeDb, maxDistance, unitSize);
        return at;
    }

    private async void KnockInTheTrees(FirstPersonController player)
    {
        var left = -player.GlobalBasis.X.Normalized();
        var at = player.GlobalPosition + left * 18f - player.GlobalBasis.Z * 9f + Vector3.Up * 1.5f;
        foreach (var wait in new[] { 0d, .45, 1.2 })
        {
            if (wait > 0) await ToSignal(GetTree().CreateTimer(wait, false), SceneTreeTimer.SignalName.Timeout);
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || _nightmareClimax) return;
            UiFoley.PlayWorld(this, at, "wood_tap", -4f, 40f, 6f);
        }
    }

    // Something crosses the path a short way ahead, left to right: heard moving,
    // seen only as snow thrown up between the trunks.
    private async void RunAcrossThePath(FirstPersonController player, float aheadProgress)
    {
        var from = ToGlobal(PrologueRoutePoint(aheadProgress, -15f, .5f));
        var to = ToGlobal(PrologueRoutePoint(aheadProgress + 4f, 15f, .5f));
        var runner = UiFoley.PlayWorld(this, from, "forest/runner_past", -8f, 45f, 6f);
        const double duration = 1.1;
        var elapsed = 0d;
        var puffAt = 0d;
        while (elapsed < duration && GodotObject.IsInstanceValid(this) && IsInsideTree() && !_nightmareClimax)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(this) || GetTree().Paused) continue;
            elapsed += GetProcessDeltaTime();
            var position = from.Lerp(to, (float)(elapsed / duration));
            if (runner is not null && GodotObject.IsInstanceValid(runner)) runner.GlobalPosition = position;
            if (elapsed < puffAt) continue;
            puffAt += .28;
            NightmareSnowPuff(position with { Y = position.Y - .3f });
        }
    }

    private void NightmareSnowPuff(Vector3 at)
    {
        var puff = new CpuParticles3D
        {
            Emitting = false, OneShot = true, Amount = 40, Lifetime = 1.3f, Explosiveness = .9f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = .45f,
            Direction = Vector3.Up, Spread = 70f, InitialVelocityMin = .6f, InitialVelocityMax = 1.7f,
            Gravity = new Vector3(.3f, -1.6f, 0), LocalCoords = false,
            Mesh = WinterParticleSurfaces.Snow(.06f, .65f),
            ColorRamp = WinterParticleSurfaces.Fade(),
            ScaleAmountMin = .35f, ScaleAmountMax = 1.1f
        };
        AddChild(puff);
        puff.GlobalPosition = at;
        puff.Emitting = true;
        puff.Finished += puff.QueueFree;
    }

    // A scream deep in the trees; for a breath something stands where it came
    // from - an outline, never a body - and then there is only snow in the air.
    private async void GlimpseAtTheScream(FirstPersonController player)
    {
        var at = NightmareSoundAround(player, "forest/fox_scream", 26f, 1f, -4f, 140f, 18f);
        var presence = new ProloguePresence { Name = "ScreamGlimpse" };
        AddChild(presence);
        presence.GlobalPosition = at with { Y = player.GlobalPosition.Y + .9f };
        presence.LookAt(player.GlobalPosition + Vector3.Up * 1.4f, Vector3.Up, true);
        var elapsed = 0d;
        while (elapsed < .95 && GodotObject.IsInstanceValid(presence) && IsInsideTree() && !_nightmareClimax)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(presence)) return;
            elapsed += GetProcessDeltaTime();
            presence.SetVeil(.6f * Mathf.Sin(Mathf.Clamp((float)elapsed / .95f, 0f, 1f) * Mathf.Pi), (float)elapsed);
        }
        if (!GodotObject.IsInstanceValid(presence)) return;
        NightmareSnowPuff(presence.GlobalPosition);
        presence.QueueFree();
    }

    // Eyes between the trunks open one by one, follow the walker, blink, and
    // close for good the moment they are looked at.
    private void BuildNightmareGlints()
    {
        var iris = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(.98f, .78f, .16f),
            EmissionEnabled = true,
            Emission = new Color(.95f, .62f, .08f),
            EmissionEnergyMultiplier = 1.6f
        };
        var pupil = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(.02f, .015f, .01f) };
        var placements = new (float Progress, float Side, float Height)[]
        {
            (100f, -11f, 1.6f), (106f, 13f, 2.7f), (113f, -15f, 1.3f), (119f, 10f, 2.1f), (126f, -12f, 3.1f), (132f, 14f, 1.5f)
        };
        foreach (var (progress, side, height) in placements)
        {
            var pair = new Node3D { Name = $"NightmareGlint{_nightmareGlints.Count}", Visible = false };
            AddChild(pair);
            pair.Position = PrologueRoutePoint(progress, side, height);
            var spacing = _nightmareRng.RandfRange(.07f, .11f);
            foreach (var offset in new[] { -spacing * .5f, spacing * .5f })
            {
                pair.AddChild(new MeshInstance3D { Position = new Vector3(offset, 0, 0), Scale = new Vector3(1f, .78f, .5f),
                    Mesh = new SphereMesh { Radius = .026f, Height = .052f, RadialSegments = 10, Rings = 6 }, MaterialOverride = iris,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                pair.AddChild(new MeshInstance3D { Position = new Vector3(offset, 0, .014f),
                    Mesh = new BoxMesh { Size = new Vector3(.007f, .036f, .004f) }, MaterialOverride = pupil,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            }
            _nightmareGlints.Add(pair);
        }
    }

    private void UpdateNightmareGlints(FirstPersonController player, double sinceStart)
    {
        var camera = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        var facing = camera is null ? -player.GlobalBasis.Z : -camera.GlobalBasis.Z;
        var target = player.GlobalPosition + Vector3.Up * 1.5f;
        for (var index = 0; index < _nightmareGlints.Count; index++)
        {
            var glint = _nightmareGlints[index];
            if (!GodotObject.IsInstanceValid(glint)) continue;
            var opensAt = .3 + index * 1.1;
            var alive = sinceStart > opensAt && sinceStart < opensAt + 7 && !_nightmareGlintClosed.ContainsKey(glint);
            if (!alive)
            {
                if (glint.Visible)
                {
                    glint.Visible = false;
                    UiFoley.PlayWorld(this, glint.GlobalPosition, "forest/strange_clicks", -17f, 30f, 4f);
                }
                continue;
            }
            if (!glint.Visible)
            {
                glint.Visible = true;
                UiFoley.PlayWorld(this, glint.GlobalPosition, "forest/strange_clicks", -19f, 30f, 4f);
            }
            glint.LookAt(target, Vector3.Up, true);
            var blink = Math.Sin(sinceStart * 2.1 + index * 1.7) > .93;
            glint.Scale = new Vector3(1f, blink ? .08f : 1f, 1f);
            if (facing.Dot((glint.GlobalPosition - target).Normalized()) > .965f) _nightmareGlintClosed[glint] = true;
        }
    }

    // ---- The end of the prints -----------------------------------------------------------

    /// <summary>
    /// The prints stop. A silence, the voice right beside him, a rush from
    /// behind - and the frame is cut to black on the scare. The black is held
    /// through the arrival loading and becomes the moment Aidar wakes.
    /// </summary>
    private async void RunNightmareClimaxAsync(RuntimeBridge bridge, object session, FirstPersonController player, Vector2 feet, Vector2 direction)
    {
        if (_nightmareClimax) return;
        _nightmareClimax = true;
        _prologueExitDispatching = true;
        ProloguePresence? presence = null;
        Tween? turn = null;
        var cut = false;
        var reachedExit = false;
        try
        {
            StopNightmareSound(ref _nightmareBed, 1.2);
            PrologueVoice.StopSpatial(this);
            player.SetSessionTransition(true);
            ShowNightmareThought("Следы обрываются. Дальше — ни одного.", 3.4);
            if (!await NightmareWaitAsync(2.1, session)) return;

            var beside = feet + new Vector2(direction.Y, -direction.X) * 1.1f;
            var voice = PlayPrologueForestVoice(beside, "forest-babai-name-v2", "«Айдар… Айдар…»", -3f);
            if (!await NightmareWaitAsync(Math.Clamp(voice * .6, 1.2, 2.2), session)) return;

            var behind = player.GlobalPosition + player.GlobalBasis.Z.Normalized() * 3f + Vector3.Up * 1.4f;
            UiFoley.PlayWorld(this, behind, "forest/rush_close", 0f, 20f, 6f);
            if (!_accessibility.ReducedMotion)
            {
                turn = CreateTween();
                turn.TweenProperty(player, "rotation_degrees:y", player.RotationDegrees.Y + 165f, .3)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            }
            if (!await NightmareWaitAsync(_accessibility.ReducedMotion ? .15 : .3, session)) return;

            var camera = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
            var eye = camera?.GlobalPosition ?? player.GlobalPosition + Vector3.Up * 1.6f;
            var forward = camera is null ? -player.GlobalBasis.Z : -camera.GlobalBasis.Z;
            presence = new ProloguePresence { Name = "EndOfPrints" };
            AddChild(presence);
            presence.GlobalPosition = eye + forward.Normalized() * 1.5f + Vector3.Down * .7f;
            presence.LookAt(eye, Vector3.Up, true);
            presence.SetVeil(1f, 0f);
            UiFoley.PlayWorld(this, presence.GlobalPosition, "forest/scare_sting", 2f, 20f, 8f);
            if (!await NightmareWaitAsync(.42, session)) return;

            PrologueDreamCut.Show(GetTree());
            cut = true;
            if (_prologueVoiceCaption is { } caption) caption.Hide();
            _prologueCaptionRemaining = 0;
            StopNightmareSound(ref _nightmareHeart, 0);
            if (!await NightmareWaitAsync(.9, session)) return;
            reachedExit = true;
        }
        finally
        {
            if (turn is not null && GodotObject.IsInstanceValid(turn)) turn.Kill();
            if (presence is not null && GodotObject.IsInstanceValid(presence)) presence.QueueFree();
            // Only hand back what this sequence took: a load or menu return that
            // replaced the session owns the player's transition state now.
            var sameSession = _bridge is { } current && GodotObject.IsInstanceValid(current)
                && ReferenceEquals(current.SessionIdentity, session);
            if (GodotObject.IsInstanceValid(player) && sameSession) player.SetSessionTransition(false);
            if (cut && !reachedExit) PrologueDreamCut.Release(fadeSeconds: .4);
        }
        if (!reachedExit) return;

        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(PrologueEndInteractionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W01 prologue nightmare exit failed: {error.Message}");
        }
        if (committed) return;
        // Rejected: lift the black, and let the end of the prints be tried again.
        if (cut) PrologueDreamCut.Release(fadeSeconds: .4);
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()) return;
        _nightmareClimax = false;
        _prologueExitDispatching = false;
        _nightmareSilence = false;
    }

    private async Task<bool> NightmareWaitAsync(double seconds, object session)
    {
        await ToSignal(GetTree().CreateTimer(seconds, false), SceneTreeTimer.SignalName.Timeout);
        return GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
            && _bridge is { } bridge && GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(bridge.SessionIdentity, session);
    }

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
