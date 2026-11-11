using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

/// <summary>
/// The dread layer of the prologue: a dying hand torch, eyes that hold in the
/// trees until they are looked at, sounds that have no owner, and the thing at
/// the end of the prints. Presentation only: nothing here writes story state,
/// and every node lives under this scene so it goes away with the photograph.
/// </summary>
public partial class W01Controller
{
    // ---- Tuning ---------------------------------------------------------------------------
    private const float TorchEnergy = 3.4f;
    private const float TorchRange = 26f;
    private const float TorchAngle = 27f;
    private const float TorchLag = 9f;
    private const int EyePoolSize = 7;
    private const float EyeNearClose = 11f;
    private const float EyeStareSeconds = .55f;
    private const float EyeMaxAge = 16f;
    private const double DreadScareSpacing = 8;
    private const string DreadDroneStream = "res://assets/audio/act1/foley/forest/dread_drone.wav";
    private const string DreadHeartStream = "res://assets/audio/act1/foley/forest/heartbeat.wav";

    private sealed class EyePair
    {
        public Node3D Root = null!;
        public MeshInstance3D Left = null!;
        public MeshInstance3D Right = null!;
        public bool Active;
        public float Open;
        public float Age;
        public float Stare;
        public float NextBlink;
        public float Blink;
        public bool Closing;
        public bool Pinned;
    }

    private SpotLight3D? _torch;
    private float _torchDip;
    private float _torchNextDip;
    private float _torchFailure;
    private bool _torchDead;
    private readonly List<EyePair> _eyes = new();
    private StandardMaterial3D? _eyeMaterial;
    private double _eyesNextSpawn;
    private double _dreadNextSound;
    private double _dreadLastScare = -100;
    private readonly bool[] _dreadFired = new bool[12];
    private AudioStreamPlayer? _dreadDrone;
    private AudioStreamPlayer? _dreadHeart;
    private Camera3D? _fallCamera;
    private ProloguePresence? _dreadRunner;
    private readonly RandomNumberGenerator _dreadRng = new() { Seed = 0x44524541 };

    // ---- Lifetime -------------------------------------------------------------------------

    private void SetDreadActive(bool active)
    {
        if (active)
        {
            EnsureTorch();
            EnsureEyes();
            return;
        }
        ReleaseFallCamera(null);
        if (_torch is { } torch && GodotObject.IsInstanceValid(torch)) torch.Visible = false;
        foreach (var pair in _eyes)
        {
            pair.Active = false;
            if (GodotObject.IsInstanceValid(pair.Root)) pair.Root.Visible = false;
        }
        StopNightmareSound(ref _dreadDrone, 0);
        StopNightmareSound(ref _dreadHeart, 0);
    }

    private void ResetDread()
    {
        Array.Clear(_dreadFired);
        _eyesNextSpawn = 9;
        _dreadNextSound = 6;
        _dreadLastScare = -100;
        _torchDip = 0;
        _torchNextDip = 12;
        _torchFailure = 0;
        _torchDead = false;
        ReleaseFallCamera(null);
        foreach (var pair in _eyes)
        {
            pair.Active = false;
            if (GodotObject.IsInstanceValid(pair.Root)) pair.Root.Visible = false;
        }
        if (_torch is { } torch && GodotObject.IsInstanceValid(torch))
        {
            torch.Visible = _nightmareMoodApplied;
            torch.LightEnergy = TorchEnergy;
        }
        if (_dreadRunner is { } runner && GodotObject.IsInstanceValid(runner)) runner.Visible = false;
        StopNightmareSound(ref _dreadDrone, 0);
        StopNightmareSound(ref _dreadHeart, 0);
    }

    private void EnsureTorch()
    {
        if (_torch is { } existing && GodotObject.IsInstanceValid(existing))
        {
            existing.Visible = true;
            return;
        }
        _torch = new SpotLight3D
        {
            Name = "PrologueHandTorch",
            LightColor = new Color(1f, .93f, .8f),
            LightEnergy = TorchEnergy,
            SpotRange = TorchRange,
            SpotAngle = TorchAngle,
            SpotAngleAttenuation = .75f,
            SpotAttenuation = 1.15f,
            LightSpecular = .25f,
            ShadowEnabled = true,
            ShadowBias = .06f,
            ShadowBlur = 1.4f
        };
        _torch.SetMeta("presentationOnly", true);
        AddChild(_torch);
    }

    private void EnsureEyes()
    {
        if (_eyes.Count > 0) return;
        _eyeMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(.05f, .04f, .01f),
            EmissionEnabled = true,
            Emission = new Color(1f, .74f, .16f),
            EmissionEnergyMultiplier = 5f,
            Roughness = .3f,
            DisableFog = true,
            DisableReceiveShadows = true
        };
        var mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 5, Material = _eyeMaterial };
        for (var index = 0; index < EyePoolSize; index++)
        {
            var pair = new EyePair { Root = new Node3D { Name = "PrologueEyes" + index, Visible = false } };
            pair.Root.SetMeta("presentationOnly", true);
            pair.Left = new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            pair.Right = new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            pair.Root.AddChild(pair.Left);
            pair.Root.AddChild(pair.Right);
            AddChild(pair.Root);
            _eyes.Add(pair);
        }
    }

    // ---- Per-frame ------------------------------------------------------------------------

    private void ProcessDread(FirstPersonController player, float progress, double time, float delta)
    {
        var fraction = Mathf.Clamp(progress / Mathf.Max(PrologueRouteLength, 1f), 0f, 1f);
        TickDreadPresentation(player, fraction, time, delta);
        if (_nightmareSilence) return;

        // The bed under everything, rising with the walk.
        if (DreadFire(0, fraction > .3f)) StartNightmareSound(ref _dreadDrone, DreadDroneStream, -22f);

        // Sounds without an owner. Sparse at first, closer and stranger later.
        if (time >= _dreadNextSound)
        {
            _dreadNextSound = time + Mathf.Lerp(9f, 4.5f, fraction) + _dreadRng.Randf() * 5f;
            DreadAmbient(player, fraction);
        }

        // Set pieces, never closer together than DreadScareSpacing.
        if (DreadScare(1, fraction > .2f, time))
        {
            // Behind him: a branch gives way under a weight.
            var behind = player.GlobalPosition + player.GlobalBasis.Z.Normalized() * 9f + Vector3.Up;
            UiFoley.PlayWorld(this, behind, "forest/branch_crack", -3f, 40f, 6f);
            UiFoley.PlayWorld(this, behind + Vector3.Right * 2f, "forest/brush_rustle", -6f, 30f, 4f);
        }
        if (DreadScare(2, fraction > .38f, time))
            UiFoley.PlayWorld(this, DreadAround(player, 60f, 80f, 3f), "forest/fox_scream", -7f, 220f, 30f);
        if (DreadScare(3, fraction > .52f, time)) RunAcrossTrail(player, progress);
        if (DreadScare(4, fraction > .68f, time))
        {
            // The torch nearly goes; every pair of eyes shuts at once.
            _torchFailure = 1.3f;
            foreach (var pair in _eyes) pair.Closing = true;
            UiFoley.PlayWorld(this, DreadAround(player, 5f, 8f, 1.2f), "forest/strange_clicks", -5f, 30f, 4f);
        }
        if (DreadScare(5, fraction > .8f, time))
        {
            StartNightmareSound(ref _dreadHeart, DreadHeartStream, -15f);
            UiFoley.PlayWorld(this, DreadAround(player, 18f, 24f, .6f), "forest/low_moan", -6f, 80f, 10f);
        }
    }

    /// <summary>Torch and eyes only; also runs through the climax, when the walk logic is parked.</summary>
    private void TickDreadPresentation(FirstPersonController player, float fraction, double time, float delta)
    {
        var camera = _fallCamera is { } fall && GodotObject.IsInstanceValid(fall) ? fall : GetViewport().GetCamera3D();
        if (camera is null) return;
        UpdateTorch(player, camera, fraction, delta);
        UpdateEyes(player, camera, fraction, time, delta);
    }

    private bool DreadFire(int index, bool condition)
    {
        if (_dreadFired[index] || !condition) return false;
        _dreadFired[index] = true;
        return true;
    }

    private bool DreadScare(int index, bool condition, double time)
    {
        if (_dreadFired[index] || !condition || time - _dreadLastScare < DreadScareSpacing) return false;
        _dreadLastScare = time;
        _dreadNextSound = Math.Max(_dreadNextSound, time + 4);
        _dreadFired[index] = true;
        return true;
    }

    private Vector3 DreadAround(Node3D player, float minDistance, float maxDistance, float height)
    {
        var angle = _dreadRng.RandfRange(0f, Mathf.Tau);
        var distance = _dreadRng.RandfRange(minDistance, maxDistance);
        return player.GlobalPosition + new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance);
    }

    private void DreadAmbient(Node3D player, float fraction)
    {
        var roll = _dreadRng.Randf();
        if (roll < .26f)
            UiFoley.PlayWorld(this, DreadAround(player, 30f, 60f, 14f),
                _dreadRng.Randf() < .5f ? "forest/owl_eagle" : "forest/owl_tawny", -8f, 140f, 16f);
        else if (roll < .5f)
            UiFoley.PlayWorld(this, DreadAround(player, 5f, 12f, .4f), "forest/brush_rustle", -8f, 30f, 4f);
        else if (roll < .66f)
            UiFoley.PlayWorld(this, DreadAround(player, 12f, 30f, 2f), "forest/branch_crack", -8f, 70f, 7f);
        else if (roll < .76f && fraction > .25f)
            UiFoley.PlayWorld(this, DreadAround(player, 80f, 110f, 4f), "forest/wolf_far", -7f, 260f, 40f);
        else if (roll < .88f && fraction > .45f)
            UiFoley.PlayWorld(this, DreadAround(player, 8f, 16f, 1.5f), "forest/strange_clicks", -10f, 45f, 5f);
        else if (roll < .94f && fraction > .6f)
            UiFoley.PlayWorld(this, DreadAround(player, 6f, 10f, 1.6f), "forest/radio_evp", -14f, 30f, 4f);
        else
            UiFoley.PlayWorld(this, player.GlobalPosition + Vector3.Up * 6f, "forest/wind_gust", -13f, 60f, 20f);
    }

    // ---- Torch ----------------------------------------------------------------------------

    private void UpdateTorch(FirstPersonController player, Camera3D camera, float fraction, float delta)
    {
        if (_torch is not { } torch || !GodotObject.IsInstanceValid(torch)) return;
        // Held low in the right hand; the beam trails the look a little.
        var eye = camera.GlobalTransform;
        var hand = eye.Origin + eye.Basis.X * .24f - eye.Basis.Y * .3f - eye.Basis.Z * .1f;
        var walk = new Vector2(player.Velocity.X, player.Velocity.Z).Length();
        var sway = _accessibility.ReducedMotion ? 0f : Mathf.Min(walk, 3f) * .006f;
        var wobble = new Basis(Vector3.Right, Mathf.Sin((float)_nightmareElapsed * 6.1f) * sway)
            * new Basis(Vector3.Up, Mathf.Cos((float)_nightmareElapsed * 3.05f) * sway * 1.4f);
        var target = eye.Basis.Orthonormalized() * wobble;
        var current = torch.GlobalBasis.Orthonormalized();
        var basis = _accessibility.ReducedMotion
            ? target
            : new Basis(current.GetRotationQuaternion().Slerp(target.GetRotationQuaternion(), Mathf.Clamp(delta * TorchLag, 0f, 1f)));
        torch.GlobalTransform = new Transform3D(basis, hand);

        if (_torchDead)
        {
            torch.LightEnergy = 0f;
            return;
        }
        // A tired battery: short dropouts that come more often deeper in.
        _torchNextDip -= delta;
        if (_torchNextDip <= 0f)
        {
            _torchNextDip = Mathf.Lerp(14f, 3.5f, fraction) * _dreadRng.RandfRange(.6f, 1.4f);
            _torchDip = _dreadRng.RandfRange(.06f, .22f) + fraction * .12f;
        }
        _torchDip = Mathf.Max(0f, _torchDip - delta);
        _torchFailure = Mathf.Max(0f, _torchFailure - delta);
        var level = 1f - fraction * .22f;
        if (_torchDip > 0f) level *= _accessibility.ReducedMotion ? .7f : _dreadRng.RandfRange(.15f, .6f);
        if (_torchFailure > 0f) level *= _accessibility.ReducedMotion ? .25f : _dreadRng.RandfRange(0f, .35f);
        torch.LightEnergy = TorchEnergy * level;
    }

    // ---- Eyes -----------------------------------------------------------------------------

    private void UpdateEyes(FirstPersonController player, Camera3D camera, float fraction, double time, float delta)
    {
        var eye = camera.GlobalPosition;
        var look = -camera.GlobalBasis.Z;
        var active = 0;
        foreach (var pair in _eyes)
        {
            if (!pair.Active) continue;
            active++;
            pair.Age += delta;
            var to = pair.Root.GlobalPosition - eye;
            var distance = to.Length();
            // They hold while he does not look. Looked at, or approached, they shut.
            pair.Stare = look.Dot(to / Mathf.Max(distance, .01f)) > .972f ? pair.Stare + delta : 0f;
            if (!pair.Closing && !pair.Pinned && (pair.Stare > EyeStareSeconds || distance < EyeNearClose || pair.Age > EyeMaxAge))
            {
                pair.Closing = true;
                if (pair.Stare > EyeStareSeconds)
                    UiFoley.PlayWorld(this, pair.Root.GlobalPosition, "forest/brush_rustle", -9f, 40f, 5f);
            }
            pair.Open = Mathf.MoveToward(pair.Open, pair.Closing ? 0f : 1f, delta * (pair.Closing ? 7f : 2.2f));
            if (pair.Closing && pair.Open <= 0f)
            {
                pair.Active = false;
                pair.Root.Visible = false;
                continue;
            }
            pair.NextBlink -= delta;
            if (pair.NextBlink <= 0f)
            {
                pair.NextBlink = _dreadRng.RandfRange(2.2f, 6f);
                pair.Blink = .13f;
            }
            pair.Blink = Mathf.Max(0f, pair.Blink - delta);
            var lid = pair.Open * (pair.Blink > 0f ? .08f : 1f);
            var scale = pair.Root.GetMeta("eyeSize", .04f).AsSingle();
            pair.Left.Scale = pair.Right.Scale = new Vector3(scale, scale * Mathf.Max(lid, .02f), scale * .5f);
        }
        if (_nightmareClimax || time < _eyesNextSpawn || fraction < .1f) return;
        _eyesNextSpawn = time + Mathf.Lerp(8f, 2.4f, fraction) + _dreadRng.Randf() * 3f;
        var limit = fraction < .3f ? 1 : fraction < .55f ? 2 : fraction < .8f ? 4 : 6;
        if (active >= limit) return;
        foreach (var pair in _eyes)
        {
            if (pair.Active) continue;
            // Off to the side of where he is looking, between the trunks.
            var side = _dreadRng.Randf() < .5f ? -1f : 1f;
            var angle = side * Mathf.DegToRad(_dreadRng.RandfRange(22f, 78f));
            var flat = new Vector3(look.X, 0f, look.Z).Normalized();
            if (flat == Vector3.Zero) flat = Vector3.Forward;
            var direction = flat.Rotated(Vector3.Up, angle);
            var distance = _dreadRng.RandfRange(17f, 32f);
            var at = ToLocal(player.GlobalPosition + direction * distance);
            var kind = _dreadRng.Randf();
            var height = kind < .6f ? _dreadRng.RandfRange(.4f, .95f) : kind < .9f ? _dreadRng.RandfRange(1.5f, 2f) : _dreadRng.RandfRange(3.1f, 4.2f);
            at.Y = ForestGroundHeight(at.X, at.Z) + height;
            SpawnEyes(pair, ToGlobal(at), eye, kind < .6f ? .085f : .13f, kind < .6f ? .034f : .042f);
            break;
        }
    }

    private void SpawnEyes(EyePair pair, Vector3 at, Vector3 watcher, float separation, float size)
    {
        pair.Root.GlobalPosition = at;
        var toWatcher = watcher - at;
        if (toWatcher.LengthSquared() > .01f)
            pair.Root.LookAt(at - new Vector3(toWatcher.X, 0f, toWatcher.Z), Vector3.Up);
        pair.Left.Position = new Vector3(-separation * .5f, 0f, 0f);
        pair.Right.Position = new Vector3(separation * .5f, 0f, 0f);
        pair.Root.SetMeta("eyeSize", size);
        pair.Left.Scale = pair.Right.Scale = new Vector3(size, .001f, size * .5f);
        pair.Active = true;
        pair.Pinned = false;
        pair.Closing = false;
        pair.Open = 0f;
        pair.Age = 0f;
        pair.Stare = 0f;
        pair.Blink = 0f;
        pair.NextBlink = _dreadRng.RandfRange(1.5f, 4f);
        pair.Root.Visible = true;
    }

    // ---- Set pieces -----------------------------------------------------------------------

    /// <summary>Something tall crosses the trail at the edge of the torch beam.</summary>
    private async void RunAcrossTrail(FirstPersonController player, float progress)
    {
        var from = ToGlobal(PrologueRoutePoint(Math.Min(progress + 15f, PrologueRouteLength - 6f), -7f, 0f));
        var to = ToGlobal(PrologueRoutePoint(Math.Min(progress + 17f, PrologueRouteLength - 4f), 7f, 0f));
        UiFoley.PlayWorld(this, (from + to) * .5f + Vector3.Up, "forest/runner_past", -2f, 50f, 8f);
        if (_accessibility.ReducedMotion) return;
        if (_dreadRunner is null || !GodotObject.IsInstanceValid(_dreadRunner))
        {
            _dreadRunner = new ProloguePresence { Name = "PrologueTrailRunner" };
            AddChild(_dreadRunner);
        }
        var runner = _dreadRunner;
        runner.Visible = true;
        runner.Scale = Vector3.One * 1.25f;
        var session = _nightmareSession;
        var elapsed = 0f;
        const float seconds = .8f;
        while (elapsed < seconds && GodotObject.IsInstanceValid(this) && IsInsideTree()
               && GodotObject.IsInstanceValid(runner) && ReferenceEquals(session, _nightmareSession))
        {
            var t = elapsed / seconds;
            runner.GlobalPosition = from.Lerp(to, t);
            runner.SetVeil(.9f * Mathf.Sin(t * Mathf.Pi), elapsed * 9f);
            if (GodotObject.IsInstanceValid(player))
                runner.LookAt(new Vector3(player.GlobalPosition.X, runner.GlobalPosition.Y, player.GlobalPosition.Z), Vector3.Up, true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += (float)GetProcessDeltaTime();
        }
        if (GodotObject.IsInstanceValid(runner)) runner.Visible = false;
    }

    /// <summary>
    /// The end of the prints. The torch dies; in the dark something opens its
    /// eyes an arm's length away, and he is on his back in the snow before he
    /// understands. Returns false when the session went away mid-sequence.
    /// </summary>
    private async Task<bool> RunDreadAttackAsync(FirstPersonController player, object session)
    {
        // 1. The torch gives up.
        _torchFailure = 1.1f;
        foreach (var pair in _eyes) pair.Closing = true;
        if (!await NightmareWaitAsync(1.1, session)) return false;
        _torchDead = true;
        UiFoley.PlayWorld(this, player.GlobalPosition + Vector3.Up, "ui_click", -8f, 6f, 1f);
        StartNightmareSound(ref _dreadHeart, DreadHeartStream, -9f);
        if (!await NightmareWaitAsync(1.9, session)) return false;

        // 2. Breath behind the ear, then eyes — far too close and far too high.
        var camera = GetViewport().GetCamera3D();
        if (camera is null) return true;
        var eye = camera.GlobalPosition;
        var forward = -camera.GlobalBasis.Z;
        forward = new Vector3(forward.X, 0f, forward.Z).Normalized();
        if (forward == Vector3.Zero) forward = Vector3.Forward;
        UiFoley.PlayWorld(this, eye - forward * .8f, "forest/low_moan", -4f, 12f, 3f);
        if (!await NightmareWaitAsync(1.3, session)) return false;
        if (_eyes.Count > 0)
        {
            var pair = _eyes[0];
            SpawnEyes(pair, eye + forward * 1.5f + Vector3.Up * .55f, eye, .3f, .075f);
            pair.Open = 1f;
            pair.Pinned = true;
            pair.NextBlink = 100f;
        }
        UiFoley.PlayWorld(this, eye + forward, "forest/scare_sting", 2f, 20f, 8f);
        if (!await NightmareWaitAsync(.4, session)) return false;

        // 3. The blow. The world tips over and the snow comes up.
        UiFoley.PlayWorld(this, eye + forward * .6f, "forest/rush_close", 3f, 20f, 8f);
        UiFoley.PlayWorld(this, eye, "forest/gasp", 0f, 10f, 4f);
        if (!_accessibility.ReducedMotion)
        {
            var fall = new Camera3D
            {
                Name = "PrologueFallCamera", Fov = camera.Fov, Near = camera.Near, Far = camera.Far,
                Environment = camera.Environment, Attributes = camera.Attributes
            };
            AddChild(fall);
            fall.GlobalTransform = camera.GlobalTransform;
            fall.MakeCurrent();
            _fallCamera = fall;
            // The first-person body belongs to the walker's own camera only.
            player.SetCaptureBodyHidden(true);
            var ground = ToLocal(eye - forward * 1.1f);
            ground.Y = ForestGroundHeight(ground.X, ground.Z) + .3f;
            var tween = fall.CreateTween().SetParallel();
            tween.TweenProperty(fall, "global_position", ToGlobal(ground), .5)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.TweenProperty(fall, "rotation_degrees:x", fall.RotationDegrees.X + 52f, .5)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(fall, "rotation_degrees:z", fall.RotationDegrees.Z + (_dreadRng.Randf() < .5f ? -34f : 34f), .5)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(fall, "fov", camera.Fov + 14f, .5);
            if (!await NightmareWaitAsync(.5, session)) return false;
            UiFoley.PlayWorld(this, fall.GlobalPosition, "forest/body_fall", 2f, 10f, 4f);
            // On his back: the eyes lean in over him.
            if (_eyes.Count > 0 && _eyes[0].Active)
            {
                var over = fall.GlobalPosition - fall.GlobalBasis.Z * .9f;
                _eyes[0].Root.CreateTween().TweenProperty(_eyes[0].Root, "global_position", over, .7)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            }
            if (!await NightmareWaitAsync(.55, session)) return false;
        }
        else
        {
            UiFoley.PlayWorld(this, eye, "forest/body_fall", 2f, 10f, 4f);
            if (!await NightmareWaitAsync(.3, session)) return false;
        }
        UiFoley.PlayWorld(this, eye, "forest/knockout_ring", -2f, 10f, 6f);
        StopNightmareSound(ref _dreadDrone, .6);
        StopNightmareSound(ref _dreadHeart, .9);
        return true;
    }

    /// <summary>Hands the view back to the walker's own camera.</summary>
    private void ReleaseFallCamera(FirstPersonController? player)
    {
        var fall = _fallCamera;
        _fallCamera = null;
        if (fall is null || !GodotObject.IsInstanceValid(fall)) return;
        var wasCurrent = fall.Current;
        fall.QueueFree();
        player ??= IsInsideTree() ? GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController : null;
        if (player is null || !GodotObject.IsInstanceValid(player)) return;
        player.SetCaptureBodyHidden(false);
        if (wasCurrent) player.GetNodeOrNull<Camera3D>("Head/Camera3D")?.MakeCurrent();
    }
}
