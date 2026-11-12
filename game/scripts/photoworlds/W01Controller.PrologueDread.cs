using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

/// <summary>
/// The dread layer of the prologue. A dying hand torch; real eyes in the dark
/// that shine back when the beam crosses them, move when he looks away and
/// stand up when they should not; shapes at the edge of the light that will
/// not hold still long enough to be understood; and, where the prints end,
/// something tall with a spur over its brow and fingers that are far too long.
/// It is never named and never fully shown. Presentation only: nothing here
/// writes story state, and every node lives under this scene.
/// </summary>
public partial class W01Controller
{
    // ---- Tuning ---------------------------------------------------------------------------
    private const float TorchEnergy = 3.4f;
    private const float TorchRange = 32f;
    private const float TorchAngle = 25f;
    private const float TorchLag = 9f;
    private const int EyePoolSize = 10;
    private const float EyeNearClose = 9f;
    private const float EyeStareSeconds = .5f;
    private const float EyeMaxAge = 18f;
    private const float EyeRestGlow = .3f;
    private float EyeOwnGlow = EyeRestGlow;
    private const double DreadScareSpacing = 8;
    private const string DreadDroneStream = "res://assets/audio/act1/foley/forest/dread_drone.wav";
    private const string DreadHeartStream = "res://assets/audio/act1/foley/forest/heartbeat.wav";
    private const string DreadCrowsStream = "res://assets/audio/act1/sound_mood/crows.wav";

    // Eyeshine, not drawn eyes: a hot point with a soft bloom round it, the way an
    // animal's eyes come back out of the dark. `open` closes it like a lid.
    private const string EyeShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, fog_disabled, shadows_disabled, blend_add, depth_draw_never;
        uniform vec3 tint : source_color = vec3(1.0, 0.74, 0.16);
        uniform float open = 1.0;
        uniform float shine = 0.3;
        uniform float slit = 0.0;
        void fragment() {
            vec2 q = UV * 2.0 - 1.0;
            float lid = max(open, 0.03);
            float d = length(vec2(q.x, q.y / lid));
            float core = smoothstep(0.30, 0.04, d);
            float ring = smoothstep(0.42, 0.26, d) * 0.35;
            float halo = pow(max(0.0, 1.0 - d), 3.0) * 0.3;
            vec3 hot = mix(tint, vec3(1.0, 0.97, 0.86), core * 0.7);
            ALBEDO = hot * (core * (1.2 + shine * 3.6) + ring * (0.5 + shine)) + tint * halo * (0.25 + shine * 0.9) + vec3(slit) * 0.0;
            ALPHA = clamp(core + ring + halo, 0.0, 1.0) * clamp(open * 3.0, 0.0, 1.0);
        }
        """;

    private const float EyeQuadScale = 2.6f; // the quad carries the bloom; the bright point is a third of it

    private enum EyeKind { Watcher, Stalker, Riser }

    private sealed class EyePair
    {
        public Node3D Root = null!;
        public MeshInstance3D Left = null!;
        public MeshInstance3D Right = null!;
        public ShaderMaterial Material = null!;
        public EyeKind Kind;
        public bool Active;
        public float Open;
        public float Shine;
        public float Age;
        public float Stare;
        public float NextBlink;
        public float Blink;
        public bool Closing;
        public bool Pinned;
        public float Size;
        public float RiseTo;
        public float ReopenIn;
        public Vector3 ReopenAt;
        public Vector3 Drift;
    }

    private SpotLight3D? _torch;
    private float _torchDip;
    private float _torchNextDip;
    private float _torchFailure;
    private float? _torchOverride;
    private readonly List<EyePair> _eyes = new();
    private Shader? _eyeShader;
    private double _eyesNextSpawn;
    private double _dreadNextSound;
    private double _dreadLastScare = -100;
    private readonly bool[] _dreadFired = new bool[16];
    private AudioStreamPlayer? _dreadDrone;
    private AudioStreamPlayer? _dreadHeart;
    private Camera3D? _fallCamera;
    private Node3D? _attackRig;
    private readonly List<(Node3D Joint, float Direction, int Segment, float Phase)> _fingerJoints = new();
    private float _fingerCurl;
    private float _fingerCurlTarget;
    private MeshInstance3D? _tallOne;
    private ProloguePresence? _dreadRunner;
    private ProloguePresence? _lurker;
    private float _lurkerAge = -1f;
    private float _lurkerStare;
    private float _lurkerLeaving;
    private Vector3 _lurkerAway;
    private double _lurkerNext;
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
        HideDreadActors();
        StopNightmareSound(ref _dreadDrone, 0);
        StopNightmareSound(ref _dreadHeart, 0);
    }

    private void HideDreadActors()
    {
        foreach (var pair in _eyes)
        {
            pair.Active = false;
            pair.ReopenIn = 0f;
            if (GodotObject.IsInstanceValid(pair.Root)) pair.Root.Visible = false;
        }
        if (_dreadRunner is { } runner && GodotObject.IsInstanceValid(runner)) runner.Visible = false;
        if (_lurker is { } lurker && GodotObject.IsInstanceValid(lurker)) lurker.Visible = false;
        if (_tallOne is { } tall && GodotObject.IsInstanceValid(tall)) tall.Visible = false;
        _lurkerAge = -1f;
    }

    private void ResetDread()
    {
        Array.Clear(_dreadFired);
        _eyesNextSpawn = 8;
        _dreadNextSound = 6;
        _dreadLastScare = -100;
        _lurkerNext = 30;
        _torchDip = 0;
        _torchNextDip = 12;
        _torchFailure = 0;
        _torchOverride = null;
        ReleaseFallCamera(null);
        HideDreadActors();
        if (_torch is { } torch && GodotObject.IsInstanceValid(torch))
        {
            torch.Visible = _nightmareMoodApplied;
            torch.LightEnergy = TorchEnergy;
        }
        if (_nightmareEyeLight is { } sight && GodotObject.IsInstanceValid(sight)) sight.LightEnergy = NightSightEnergy;
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
            SpotAttenuation = .55f,
            LightSpecular = .25f,
            ShadowEnabled = true,
            ShadowBias = .06f,
            ShadowBlur = 1.4f
        };
        _torch.SetMeta("presentationOnly", true);
        AddChild(_torch);
    }

    private ShaderMaterial NewEyeMaterial()
    {
        _eyeShader ??= new Shader { Code = EyeShaderCode };
        return new ShaderMaterial { Shader = _eyeShader };
    }

    private static MeshInstance3D NewEyeQuad(ShaderMaterial material) => new()
    {
        Mesh = new QuadMesh { Size = Vector2.One },
        MaterialOverride = material,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    };

    private void EnsureEyes()
    {
        if (_eyes.Count > 0) return;
        for (var index = 0; index < EyePoolSize; index++)
        {
            var pair = new EyePair { Root = new Node3D { Name = "PrologueEyes" + index, Visible = false }, Material = NewEyeMaterial() };
            pair.Root.SetMeta("presentationOnly", true);
            pair.Left = NewEyeQuad(pair.Material);
            pair.Right = NewEyeQuad(pair.Material);
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
        if (DreadFire(0, fraction > .28f)) StartNightmareSound(ref _dreadDrone, DreadDroneStream, -22f);

        // Sounds without an owner. Sparse at first, closer and stranger later.
        if (time >= _dreadNextSound)
        {
            _dreadNextSound = time + Mathf.Lerp(9f, 4.5f, fraction) + _dreadRng.Randf() * 5f;
            DreadAmbient(player, fraction);
        }

        // What happens on the trail. Each is small and has no owner he can
        // see; they come closer together and stranger as the prints go on.
        if (DreadScare(1, fraction > .10f, time)) SnowFallsFromABough(progress);
        if (DreadScare(2, fraction > .22f, time))
        {
            // Behind him: a branch gives way under a weight.
            var behind = player.GlobalPosition + player.GlobalBasis.Z.Normalized() * 9f + Vector3.Up;
            UiFoley.PlayWorld(this, behind, "forest/branch_crack", -4f, 40f, 6f);
            UiFoley.PlayWorld(this, behind + Vector3.Right * 2f, "forest/brush_rustle", -7f, 30f, 4f);
        }
        if (DreadScare(3, fraction > .36f, time)) BirdsBreakCover(progress);
        if (DreadScare(4, fraction > .48f, time))
            UiFoley.PlayWorld(this, DreadAround(player, 55f, 75f, 1f), "forest/fox_scream", -10f, 180f, 24f);
        if (DreadScare(6, fraction > .68f, time))
        {
            // The torch nearly goes; every pair of eyes shuts at once.
            _torchFailure = 1.4f;
            foreach (var pair in _eyes) { pair.Closing = true; pair.ReopenIn = 0f; }
            UiFoley.PlayWorld(this, DreadAround(player, 5f, 8f, 1.2f), "forest/strange_clicks", -5f, 30f, 4f);
        }
        if (DreadScare(5, fraction > .75f, time)) RunAcrossTrail(player, progress);
        if (DreadScare(7, fraction > .83f, time))
        {
            // Too many of them, at heights that do not belong to one animal, on the trail itself.
            var heights = new[] { .5f, 1.3f, 2.4f, 3.6f };
            for (var index = 0; index < heights.Length; index++)
            {
                var at = PrologueRoutePoint(Math.Min(progress + 16f + index * 1.4f, PrologueRouteLength - 6f),
                    _dreadRng.RandfRange(-1.6f, 1.6f), heights[index]);
                if (FreeEyePair() is { } pair)
                    SpawnEyes(pair, ToGlobal(at), EyeKind.Watcher, index > 1 ? .13f : .09f, index > 1 ? .11f : .08f, index == 3);
            }
            UiFoley.PlayWorld(this, ToGlobal(PrologueRoutePoint(Math.Min(progress + 16f, PrologueRouteLength - 6f), 0f, 1.2f)),
                "forest/low_moan", -8f, 60f, 8f);
        }
        if (DreadScare(8, fraction > .9f, time))
            StartNightmareSound(ref _dreadHeart, DreadHeartStream, -18f);
        UpdateLurker(player, progress, fraction, time, delta);
    }

    /// <summary>Look, torch and eyes; also runs through the climax, when the walk logic is parked.</summary>
    private void TickDreadPresentation(FirstPersonController player, float fraction, double time, float delta)
    {
        EnforceNightmareLook();
        var camera = _fallCamera is { } fall && GodotObject.IsInstanceValid(fall) ? fall : GetViewport().GetCamera3D();
        if (camera is null) return;
        UpdateTorch(player, camera, fraction, delta);
        UpdateEyes(player, camera, fraction, time, delta);
        UpdateFingers(delta);
    }

    private static GradientTexture2D? _softDot;
    /// <summary>A soft round sprite for flakes and vapour.</summary>
    private static GradientTexture2D SoftDot()
    {
        if (_softDot is not null) return _softDot;
        var falloff = new Gradient();
        falloff.SetColor(0, Colors.White);
        falloff.SetColor(1, new Color(1f, 1f, 1f, 0f));
        return _softDot = new GradientTexture2D
        {
            Gradient = falloff, Width = 32, Height = 32, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1f, .5f)
        };
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
        if (roll < .24f)
            UiFoley.PlayWorld(this, DreadAround(player, 30f, 60f, 14f),
                _dreadRng.Randf() < .5f ? "forest/owl_eagle" : "forest/owl_tawny", -8f, 140f, 16f);
        else if (roll < .48f)
            UiFoley.PlayWorld(this, DreadAround(player, 5f, 12f, .4f), "forest/brush_rustle", -8f, 30f, 4f);
        else if (roll < .66f)
            UiFoley.PlayWorld(this, DreadAround(player, 10f, 28f, 2f), "forest/branch_crack", -8f, 70f, 7f);
        else if (roll < .76f && fraction > .25f)
            UiFoley.PlayWorld(this, DreadAround(player, 80f, 110f, 4f), "forest/wolf_far", -7f, 260f, 40f);
        else if (roll < .88f && fraction > .4f)
            UiFoley.PlayWorld(this, DreadAround(player, 7f, 15f, 1.5f), "forest/strange_clicks", -10f, 45f, 5f);
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
        // The beam is pointed where he looks, not parallel to it from the hip.
        var aim = eye.Origin - eye.Basis.Z * 10f + eye.Basis.Y * .9f - hand;
        var target = Basis.LookingAt(aim.Normalized(), eye.Basis.Y.Normalized()) * wobble;
        var current = torch.GlobalBasis.Orthonormalized();
        var basis = _accessibility.ReducedMotion
            ? target
            : new Basis(current.GetRotationQuaternion().Slerp(target.GetRotationQuaternion(), Mathf.Clamp(delta * TorchLag, 0f, 1f)));
        torch.GlobalTransform = new Transform3D(basis, hand);

        if (_torchOverride is { } forced)
        {
            torch.LightEnergy = TorchEnergy * forced;
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

    private bool TorchReaches(Vector3 point)
    {
        if (_torch is not { } torch || !GodotObject.IsInstanceValid(torch) || torch.LightEnergy < .4f) return false;
        var to = point - torch.GlobalPosition;
        var distance = to.Length();
        return distance < TorchRange + 8f
            && (-torch.GlobalBasis.Z).Dot(to / Mathf.Max(distance, .01f)) > Mathf.Cos(Mathf.DegToRad(TorchAngle * .9f));
    }

    // ---- Eyes -----------------------------------------------------------------------------

    private EyePair? FreeEyePair()
    {
        foreach (var pair in _eyes)
            if (!pair.Active && pair.ReopenIn <= 0f) return pair;
        return null;
    }

    private void UpdateEyes(FirstPersonController player, Camera3D camera, float fraction, double time, float delta)
    {
        var eye = camera.GlobalPosition;
        var look = -camera.GlobalBasis.Z;
        var active = 0;
        foreach (var pair in _eyes)
        {
            if (!pair.Active)
            {
                // Looked at, it shut. Now it opens again somewhere nearer.
                if (pair.ReopenIn > 0f && (pair.ReopenIn -= delta) <= 0f && !_nightmareClimax)
                    SpawnEyes(pair, pair.ReopenAt, EyeKind.Watcher, pair.Size * 1.25f, pair.Size, false);
                continue;
            }
            active++;
            pair.Age += delta;
            if (pair.Kind == EyeKind.Stalker && !pair.Closing)
            {
                // Keeps pace beside the trail, with the small bob of something walking.
                var pace = new Vector3(player.Velocity.X, 0f, player.Velocity.Z);
                pair.Root.GlobalPosition += pace * delta * .92f
                    + Vector3.Up * Mathf.Sin(pair.Age * 5.2f) * .0025f * Mathf.Min(pace.Length(), 2f);
            }
            else if (pair.Kind == EyeKind.Riser && !pair.Closing && pair.Age > 1.6f)
            {
                // It was low to the ground. It is not an animal: it stands up.
                var position = pair.Root.GlobalPosition;
                position.Y = Mathf.MoveToward(position.Y, pair.RiseTo, delta * .85f);
                pair.Root.GlobalPosition = position;
            }
            else if (pair.Drift != Vector3.Zero && !pair.Closing)
                pair.Root.GlobalPosition += pair.Drift * delta;

            var to = pair.Root.GlobalPosition - eye;
            var distance = to.Length();
            // They face whoever is looking, always.
            if (distance > .2f && Mathf.Abs(to.Normalized().Y) < .98f) pair.Root.LookAt(eye, Vector3.Up, true);
            pair.Stare = look.Dot(to / Mathf.Max(distance, .01f)) > .975f ? pair.Stare + delta : 0f;
            if (!pair.Closing && !pair.Pinned && (pair.Stare > EyeStareSeconds || distance < EyeNearClose || pair.Age > EyeMaxAge))
            {
                pair.Closing = true;
                if (pair.Stare > EyeStareSeconds)
                {
                    UiFoley.PlayWorld(this, pair.Root.GlobalPosition, "forest/brush_rustle", -9f, 40f, 5f);
                    if (_dreadRng.Randf() < .5f && distance > 12f)
                    {
                        var flat = new Vector3(to.X, 0f, to.Z);
                        var sideways = flat.Cross(Vector3.Up).Normalized() * (_dreadRng.Randf() < .5f ? -1f : 1f) * _dreadRng.RandfRange(2f, 4.5f);
                        pair.ReopenAt = pair.Root.GlobalPosition - flat * .38f + sideways;
                        pair.ReopenIn = _dreadRng.RandfRange(1f, 2f);
                    }
                }
            }
            pair.Open = Mathf.MoveToward(pair.Open, pair.Closing ? 0f : 1f, delta * (pair.Closing ? 8f : 2.2f));
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
            // Eyeshine: they flare when the beam crosses them and smoulder when it does not.
            pair.Shine = Mathf.MoveToward(pair.Shine, TorchReaches(pair.Root.GlobalPosition) ? 1f : EyeOwnGlow, delta * 6f);
            pair.Material.SetShaderParameter("open", pair.Open * (pair.Blink > 0f ? .06f : 1f));
            pair.Material.SetShaderParameter("shine", pair.Shine);
        }
        if (_nightmareClimax || time < _eyesNextSpawn || fraction < .08f) return;
        _eyesNextSpawn = time + Mathf.Lerp(7f, 2.2f, fraction) + _dreadRng.Randf() * 3f;
        var limit = fraction < .25f ? 1 : fraction < .5f ? 2 : fraction < .75f ? 4 : 6;
        if (active >= limit || FreeEyePair() is not { } free) return;
        // Off to the side of where he is looking, between the trunks.
        var side = _dreadRng.Randf() < .5f ? -1f : 1f;
        var angle = side * Mathf.DegToRad(_dreadRng.RandfRange(20f, 80f));
        var ahead = new Vector3(look.X, 0f, look.Z).Normalized();
        if (ahead == Vector3.Zero) ahead = Vector3.Forward;
        var direction = ahead.Rotated(Vector3.Up, angle);
        var roll = _dreadRng.Randf();
        var kind = fraction > .3f && roll < .2f ? EyeKind.Stalker : fraction > .45f && roll < .38f ? EyeKind.Riser : EyeKind.Watcher;
        var range = kind == EyeKind.Stalker ? _dreadRng.RandfRange(11f, 15f) : _dreadRng.RandfRange(14f, 30f);
        var at = ToLocal(player.GlobalPosition + direction * range);
        var shape = _dreadRng.Randf();
        var height = kind == EyeKind.Riser ? .45f
            : shape < .5f ? _dreadRng.RandfRange(.4f, .95f) : shape < .86f ? _dreadRng.RandfRange(1.5f, 2.1f) : _dreadRng.RandfRange(3f, 4.4f);
        var ground = ForestGroundHeight(at.X, at.Z);
        at.Y = ground + height;
        var tall = height > 1.3f;
        SpawnEyes(free, ToGlobal(at), kind, tall ? .13f : .085f, tall ? .1f : .075f, tall && _dreadRng.Randf() < .6f);
        if (kind == EyeKind.Riser) free.RiseTo = ToGlobal(new Vector3(at.X, ground + _dreadRng.RandfRange(2.5f, 3.3f), at.Z)).Y;
        else if (kind == EyeKind.Watcher && _dreadRng.Randf() < .3f)
            free.Drift = direction.Cross(Vector3.Up).Normalized() * _dreadRng.RandfRange(-.5f, .5f);
    }

    private void SpawnEyes(EyePair pair, Vector3 at, EyeKind kind, float separation, float size, bool levelPupil)
    {
        pair.Root.GlobalPosition = at;
        pair.Left.Position = new Vector3(-separation * .5f, 0f, 0f);
        pair.Right.Position = new Vector3(separation * .5f, 0f, 0f);
        pair.Left.Scale = pair.Right.Scale = Vector3.One * size * EyeQuadScale;
        pair.Size = size;
        pair.Kind = kind;
        pair.Active = true;
        pair.Pinned = false;
        pair.Closing = false;
        pair.Open = 0f;
        pair.Shine = EyeOwnGlow;
        pair.Age = 0f;
        pair.Stare = 0f;
        pair.Blink = 0f;
        pair.ReopenIn = 0f;
        pair.Drift = Vector3.Zero;
        pair.NextBlink = _dreadRng.RandfRange(1.5f, 4f);
        var tintRoll = _dreadRng.Randf();
        pair.Material.SetShaderParameter("tint", tintRoll < .55f ? new Color(1f, .74f, .16f)
            : tintRoll < .85f ? new Color(.62f, 1f, .42f) : new Color(1f, .93f, .7f));
        pair.Material.SetShaderParameter("slit", levelPupil ? 1f : 0f);
        pair.Material.SetShaderParameter("open", 0f);
        pair.Material.SetShaderParameter("shine", EyeOwnGlow);
        pair.Root.Visible = true;
    }

    // ---- Things at the edge of the light --------------------------------------------------

    /// <summary>A dark shape leaning out from behind a trunk ahead. It withdraws the moment the beam finds it.</summary>
    private void UpdateLurker(FirstPersonController player, float progress, float fraction, double time, float delta)
    {
        if (_accessibility.ReducedMotion || _nightmareSilence) return;
        var camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        if (_lurkerAge < 0f)
        {
            if (fraction < .3f || time < _lurkerNext || progress > PrologueRouteLength - 30f) return;
            _lurkerNext = time + _dreadRng.RandfRange(15f, 24f);
            if (_lurker is null || !GodotObject.IsInstanceValid(_lurker))
            {
                _lurker = new ProloguePresence { Name = "PrologueLurker" };
                AddChild(_lurker);
            }
            var side = _dreadRng.Randf() < .5f ? -1f : 1f;
            var at = PrologueRoutePoint(progress + _dreadRng.RandfRange(10f, 14f), side * _dreadRng.RandfRange(1.9f, 2.7f));
            _lurker.Position = at;
            _lurker.Scale = new Vector3(1.1f, 1.55f, 1.1f);
            _lurker.Visible = true;
            _lurker.SetVeil(0f, 0f);
            _lurkerAway = ToGlobal(at) - ToGlobal(PrologueRoutePoint(progress + 12f, 0f));
            _lurkerAway = new Vector3(_lurkerAway.X, 0f, _lurkerAway.Z).Normalized();
            _lurkerAge = 0f;
            _lurkerStare = 0f;
            _lurkerLeaving = 0f;
            return;
        }
        if (_lurker is not { } lurker || !GodotObject.IsInstanceValid(lurker)) { _lurkerAge = -1f; return; }
        _lurkerAge += delta;
        var to = lurker.GlobalPosition + Vector3.Up * 1.4f - camera.GlobalPosition;
        var distance = to.Length();
        var facing = new Vector3(camera.GlobalPosition.X, lurker.GlobalPosition.Y, camera.GlobalPosition.Z);
        if (facing.DistanceSquaredTo(lurker.GlobalPosition) > .05f) lurker.LookAt(facing, Vector3.Up, true);
        if (_lurkerLeaving <= 0f)
        {
            // It sways, a little out, a little back.
            lurker.SetVeil(Mathf.Min(_lurkerAge / .7f, 1f) * .8f, _lurkerAge * 2.5f);
            _lurkerStare = (-camera.GlobalBasis.Z).Dot(to / Mathf.Max(distance, .01f)) > .95f ? _lurkerStare + delta : 0f;
            if (_lurkerStare > .22f || distance < 5.5f || _lurkerAge > 10f)
            {
                _lurkerLeaving = .001f;
                UiFoley.PlayWorld(this, lurker.GlobalPosition + Vector3.Up, "forest/brush_rustle", -6f, 30f, 4f);
            }
            return;
        }
        _lurkerLeaving += delta;
        lurker.GlobalPosition += _lurkerAway * delta * 8f;
        lurker.SetVeil(Mathf.Max(0f, .8f * (1f - _lurkerLeaving / .4f)), _lurkerAge * 9f);
        if (_lurkerLeaving >= .4f)
        {
            lurker.Visible = false;
            _lurkerAge = -1f;
        }
    }

    /// <summary>Snow lets go of a bough just ahead: the first, harmless start.</summary>
    private void SnowFallsFromABough(float progress)
    {
        var at = ToGlobal(PrologueRoutePoint(progress + _dreadRng.RandfRange(6f, 8.5f), _dreadRng.RandfRange(-1.4f, 1.4f), 5.5f));
        UiFoley.PlayWorld(this, at, "forest/brush_rustle", -4f, 30f, 5f);
        if (_accessibility.ReducedMotion) return;
        var flakes = new CpuParticles3D
        {
            Name = "PrologueSnowFall",
            Amount = 90, Lifetime = 1.7f, OneShot = true, Explosiveness = .75f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(.7f, .25f, .7f),
            Direction = Vector3.Down, Spread = 25f,
            InitialVelocityMin = .6f, InitialVelocityMax = 2.4f,
            Gravity = new Vector3(0f, -5.5f, 0f),
            ScaleAmountMin = .5f, ScaleAmountMax = 1.4f,
            Mesh = new QuadMesh
            {
                Size = new Vector2(.09f, .09f),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(.9f, .94f, 1f),
                    AlbedoTexture = SoftDot(),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                    Roughness = 1f
                }
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(flakes);
        flakes.GlobalPosition = at;
        flakes.Emitting = true;
        GetTree().CreateTimer(3.5).Timeout += () => { if (GodotObject.IsInstanceValid(flakes)) flakes.QueueFree(); };
    }

    /// <summary>Birds go up out of a crown ahead, all at once, from something he cannot see.</summary>
    private void BirdsBreakCover(float progress)
    {
        var side = _dreadRng.Randf() < .5f ? -1f : 1f;
        var from = ToGlobal(PrologueRoutePoint(progress + 9f, side * 3.5f, 6f));
        PlayNightmareSample(DreadCrowsStream, from, -6f, 80f, 10f);
        UiFoley.PlayWorld(this, from, "forest/branch_crack", -6f, 50f, 6f);
        if (_accessibility.ReducedMotion) return;
        var wing = new StandardMaterial3D
        {
            AlbedoColor = new Color(.02f, .02f, .025f), Roughness = 1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        for (var index = 0; index < 7; index++)
        {
            var bird = new MeshInstance3D
            {
                Name = "PrologueBird" + index,
                Mesh = new QuadMesh { Size = new Vector2(.5f, .16f) },
                MaterialOverride = wing,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(bird);
            bird.GlobalPosition = from + new Vector3(_dreadRng.RandfRange(-1f, 1f), _dreadRng.RandfRange(-.6f, .6f), _dreadRng.RandfRange(-1f, 1f));
            bird.RotationDegrees = new Vector3(_dreadRng.RandfRange(-30f, 30f), _dreadRng.RandfRange(0f, 360f), _dreadRng.RandfRange(-25f, 25f));
            var away = new Vector3(-side * _dreadRng.RandfRange(4f, 9f), _dreadRng.RandfRange(5f, 9f), _dreadRng.RandfRange(-6f, 6f));
            var seconds = _dreadRng.RandfRange(1.3f, 2f);
            // The beat of the wings.
            var beat = bird.CreateTween().SetLoops(Mathf.CeilToInt(seconds / .16f));
            beat.TweenProperty(bird, "scale:y", .15f, .08);
            beat.TweenProperty(bird, "scale:y", 1f, .08);
            var flight = bird.CreateTween();
            flight.TweenProperty(bird, "global_position", bird.GlobalPosition + away, seconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            flight.TweenCallback(Callable.From(bird.QueueFree));
        }
    }

    /// <summary>Something tall crosses the trail at the edge of the torch beam.</summary>
    private async void RunAcrossTrail(FirstPersonController player, float progress)
    {
        var from = ToGlobal(PrologueRoutePoint(Math.Min(progress + 14f, PrologueRouteLength - 6f), -7f, 0f));
        var to = ToGlobal(PrologueRoutePoint(Math.Min(progress + 16f, PrologueRouteLength - 4f), 7f, 0f));
        UiFoley.PlayWorld(this, (from + to) * .5f + Vector3.Up, "forest/runner_past", -2f, 50f, 8f);
        if (_accessibility.ReducedMotion) return;
        if (_dreadRunner is null || !GodotObject.IsInstanceValid(_dreadRunner))
        {
            _dreadRunner = new ProloguePresence { Name = "PrologueTrailRunner" };
            AddChild(_dreadRunner);
        }
        var runner = _dreadRunner;
        runner.Visible = true;
        runner.Scale = new Vector3(1.2f, 1.7f, 1.2f);
        var session = _nightmareSession;
        var elapsed = 0f;
        const float seconds = .75f;
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

    // ---- The end of the prints ------------------------------------------------------------

    private async Task<bool> TorchFlashAsync(double seconds, object session)
    {
        if (_accessibility.ReducedMotion)
        {
            // No strobe: one steady, weak light for a little longer.
            _torchOverride = .4f;
            if (!await NightmareWaitAsync(seconds * 2.2, session)) return false;
        }
        else
        {
            _torchOverride = 1f;
            if (!await NightmareWaitAsync(seconds * .45, session)) return false;
            _torchOverride = .15f;
            if (!await NightmareWaitAsync(.04, session)) return false;
            _torchOverride = .9f;
            if (!await NightmareWaitAsync(seconds * .55, session)) return false;
        }
        _torchOverride = 0f;
        return true;
    }

    private MeshInstance3D EnsureTallOne()
    {
        if (_tallOne is { } existing && GodotObject.IsInstanceValid(existing)) return existing;
        _tallOne = new MeshInstance3D
        {
            Name = "PrologueTallOne",
            Mesh = PrologueTreeMeshes.TallOne(7),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            Visible = false
        };
        _tallOne.SetMeta("presentationOnly", true);
        // Two dull eyes under the spur, pupils lying level like a goat's.
        var material = NewEyeMaterial();
        material.SetShaderParameter("tint", new Color(1f, .8f, .3f));
        material.SetShaderParameter("slit", 1f);
        material.SetShaderParameter("shine", .55f);
        material.SetShaderParameter("open", .8f);
        foreach (var x in new[] { .025f, .115f })
        {
            var eye = NewEyeQuad(material);
            eye.Position = new Vector3(x, 3.13f, -.43f);
            eye.Scale = Vector3.One * .05f;
            _tallOne.AddChild(eye);
        }
        AddChild(_tallOne);
        return _tallOne;
    }

    private void StandTallOne(Vector3 eye, Vector3 forward, float distance, float tiltDegrees)
    {
        var tall = EnsureTallOne();
        var at = ToLocal(eye + forward * distance);
        at.Y = ForestGroundHeight(at.X, at.Z);
        tall.Position = at;
        tall.LookAt(new Vector3(eye.X, tall.GlobalPosition.Y, eye.Z), Vector3.Up);
        tall.RotateObjectLocal(Vector3.Back, Mathf.DegToRad(tiltDegrees));
        tall.Visible = true;
    }

    /// <summary>The hands that come in from both edges of sight: five jointed fingers each, far too long.</summary>
    private void BuildAttackRig(Camera3D camera)
    {
        ReleaseAttackRig();
        var rig = new Node3D { Name = "PrologueAttackRig" };
        rig.SetMeta("presentationOnly", true);
        AddChild(rig);
        rig.GlobalTransform = camera.GlobalTransform;
        _attackRig = rig;
        var skin = new StandardMaterial3D { AlbedoColor = new Color(.5f, .46f, .4f), Roughness = .85f, MetallicSpecular = .3f, AlbedoTexture = PrologueTextureSlot("finale_hand_skin_albedo", null) };
        var lengths = new[] { .17f, .15f, .13f, .11f };
        foreach (var side in new[] { -1f, 1f })
        {
            var direction = -side;
            for (var finger = 0; finger < 5; finger++)
            {
                var root = new Node3D
                {
                    Position = new Vector3(side * .5f, (finger - 2) * .085f + .03f, -.46f - Mathf.Abs(finger - 2) * .025f),
                    RotationDegrees = new Vector3(0f, 0f, (finger - 2) * 9f * direction)
                };
                rig.AddChild(root);
                var parent = root;
                for (var segment = 0; segment < lengths.Length; segment++)
                {
                    var joint = new Node3D { Position = segment == 0 ? Vector3.Zero : new Vector3(direction * lengths[segment - 1], 0f, 0f) };
                    parent.AddChild(joint);
                    var thick = Mathf.Lerp(.017f, .006f, segment / 3f);
                    joint.AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh
                        {
                            Height = lengths[segment], BottomRadius = thick, TopRadius = thick * .7f,
                            RadialSegments = 5, Rings = 1, Material = skin
                        },
                        Position = new Vector3(direction * lengths[segment] * .5f, 0f, 0f),
                        RotationDegrees = new Vector3(0f, 0f, direction > 0f ? -90f : 90f),
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                    });
                    // A knuckle at every joint.
                    joint.AddChild(new MeshInstance3D
                    {
                        Mesh = new SphereMesh { Radius = thick * 1.35f, Height = thick * 2.7f, RadialSegments = 6, Rings = 3, Material = skin },
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                    });
                    _fingerJoints.Add((joint, direction, segment, _dreadRng.RandfRange(0f, Mathf.Tau)));
                    parent = joint;
                }
            }
        }
        // Whatever it is stands between him and the forest: a mass with no edge.
        var shade = new Gradient();
        shade.Offsets = new[] { 0f, .55f, 1f };
        shade.Colors = new[] { new Color(0f, 0f, 0f, 1f), new Color(0f, 0f, 0f, .96f), new Color(0f, 0f, 0f, 0f) };
        rig.AddChild(new MeshInstance3D
        {
            Name = "LoomingMass",
            Mesh = new QuadMesh { Size = new Vector2(3.4f, 4.2f) },
            Position = new Vector3(0f, .25f, -1.25f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                DisableFog = true,
                AlbedoTexture = new GradientTexture2D
                {
                    Gradient = shade, Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
                    FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1f, .5f)
                }
            }
        });
        // The fingers catch the last of the torch whatever the beam is doing.
        rig.AddChild(new OmniLight3D
        {
            Name = "LastSpark", Position = new Vector3(0f, -.05f, -.2f), LightColor = new Color(1f, .9f, .74f),
            LightEnergy = .22f, OmniRange = 1.1f, OmniAttenuation = 1.2f, ShadowEnabled = false
        });
        _fingerCurl = .1f;
        _fingerCurlTarget = .1f;
        UpdateFingers(0f);
    }

    private void UpdateFingers(float delta)
    {
        if (_attackRig is null || !GodotObject.IsInstanceValid(_attackRig)) return;
        _fingerCurl = Mathf.MoveToward(_fingerCurl, _fingerCurlTarget, delta * .75f);
        var time = Time.GetTicksMsec() / 1000f;
        foreach (var (joint, direction, segment, phase) in _fingerJoints)
        {
            if (!GodotObject.IsInstanceValid(joint)) continue;
            // They never keep still: a small, quick working of every joint.
            var twitch = _accessibility.ReducedMotion ? 0f : Mathf.Sin(time * 9f + phase + segment) * 3.5f;
            var bend = 6f + _fingerCurl * (30f + segment * 6f) + twitch;
            joint.RotationDegrees = new Vector3(0f, -direction * bend, 0f);
        }
    }

    private void ReleaseAttackRig()
    {
        _fingerJoints.Clear();
        var rig = _attackRig;
        _attackRig = null;
        if (rig is not null && GodotObject.IsInstanceValid(rig)) rig.QueueFree();
    }

    /// <summary>
    /// The end of the prints. The torch dies. A voice he knows calls from ahead.
    /// The sound approaches outside the view; consciousness cuts out before
    /// its source is visible. Returns false if the session changes mid-sequence.
    /// </summary>
    private async Task<bool> RunDreadAttackAsync(FirstPersonController player, object session)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera is null) return true;
        var eye = camera.GlobalPosition;
        var calm = _accessibility.ReducedMotion;
        // The scene is staged along the line the prints were following, and his
        // head is brought round to it: he hears something and looks up.
        var forward = GlobalBasis * new Vector3(PrologueF03.X - PrologueF02.X, 0f, PrologueF03.Z - PrologueF02.Z);
        forward = new Vector3(forward.X, 0f, forward.Z).Normalized();
        if (forward == Vector3.Zero) forward = Vector3.Forward;
        var right = forward.Cross(Vector3.Up).Normalized();
        var yaw = Mathf.Atan2(-forward.X, -forward.Z);
        var turn = player.CreateTween().SetParallel();
        turn.TweenProperty(player, "rotation:y", player.Rotation.Y + Mathf.AngleDifference(player.Rotation.Y, yaw), calm ? .05 : .9)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        if (player.GetNodeOrNull<Node3D>("Head") is { } head)
            turn.TweenProperty(head, "rotation_degrees:x", 5f, calm ? .05 : .9)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        // 1. The torch stutters. While it is out, the tree is simply there.
        if (DevForestCaptureActive) GD.Print("finale: step 1 torch=" + (_torch?.LightEnergy ?? -1f) + " t=" + Time.GetTicksMsec());
        HideDreadActors();
        _torchFailure = 0f;
        _torchOverride = .1f;
        StartNightmareSound(ref _dreadHeart, DreadHeartStream, -14f);
        var trunkAt = ToLocal(eye + forward * FinaleTrunkDistance - right * .42f);
        trunkAt.Y = ForestGroundHeight(trunkAt.X, trunkAt.Z);
        var trunk = BuildFinaleTrunk(ToGlobal(trunkAt), eye);
        if (!await NightmareWaitAsync(.9, session)) return false;
        _torchOverride = .85f;
        UiFoley.PlayWorld(this, eye, "ui_click", -9f, 6f, 1f);
        if (!await NightmareWaitAsync(1.0, session)) return false;

        // 2. A voice he knows, from behind that tree.
        if (DevForestCaptureActive) GD.Print("finale: step 2 torch=" + (_torch?.LightEnergy ?? -1f) + " t=" + Time.GetTicksMsec());
        var voiceAt = ToLocal(trunk.GlobalPosition);
        var voice = PlayPrologueForestVoice(new Vector2(voiceAt.X, voiceAt.Z), "forest-come-here",
            "«Айдар. Кил монда…»\n(Айдар, иди сюда…)", -4f);
        if (!await NightmareWaitAsync(Math.Clamp(voice, 1.6, 3.2) + .3, session)) return false;
        if (_prologueVoiceCaption is { } caption) caption.Hide();
        _prologueCaptionRemaining = 0;

        // 3. Fingers come round the trunk, one after another. Far too long.
        if (DevForestCaptureActive) GD.Print("finale: step 3 torch=" + (_torch?.LightEnergy ?? -1f) + " t=" + Time.GetTicksMsec());
        var hand = trunk.GetNode<Node3D>("Hand");
        hand.Visible = true;
        UiFoley.PlayWorld(this, trunk.GlobalPosition + Vector3.Up * 2f, "forest/strange_clicks", -3f, 30f, 5f);
        hand.CreateTween().TweenProperty(hand, "rotation:y", hand.Rotation.Y + Mathf.DegToRad(FinaleHandCreepDegrees), calm ? .2 : 1.5)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        if (!await NightmareWaitAsync(1.9, session)) return false;

        // 4. Then the eyes lean out after them, above where a head should be.
        if (DevForestCaptureActive) GD.Print("finale: step 4 torch=" + (_torch?.LightEnergy ?? -1f) + " t=" + Time.GetTicksMsec());
        var eyesAt = trunk.GlobalPosition + right * .42f + Vector3.Up * FinaleEyeHeight - forward * .05f;
        EyeOwnGlow = .75f;
        if (FreeEyePair() is not { } watcher) return true;
        SpawnEyes(watcher, eyesAt - right * .3f, EyeKind.Watcher, .12f, .085f, true);
        watcher.Pinned = true;
        watcher.NextBlink = 100f;
        watcher.Material.SetShaderParameter("tint", new Color(1f, .78f, .25f));
        watcher.Root.CreateTween().TweenProperty(watcher.Root, "global_position", eyesAt, calm ? .2 : .9)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        UiFoley.PlayWorld(this, eye + forward * 3f, "forest/scare_sting", -6f, 20f, 8f);
        if (!await NightmareWaitAsync(1.5, session)) return false;

        // 5. The torch dies. The eyes do not. They come down to his height.
        if (DevForestCaptureActive) GD.Print("finale: step 5 torch=" + (_torch?.LightEnergy ?? -1f) + " t=" + Time.GetTicksMsec());
        _torchOverride = 0f;
        UiFoley.PlayWorld(this, eye, "ui_click", -8f, 6f, 1f);
        if (_nightmareEyeLight is { } sight && GodotObject.IsInstanceValid(sight))
            sight.CreateTween().TweenProperty(sight, "light_energy", 0f, .6);
        hand.Visible = false;
        UiFoley.PlayWorld(this, eye + forward * 5f, "forest/low_moan", -3f, 30f, 6f);
        watcher.Root.CreateTween().TweenProperty(watcher.Root, "global_position",
            eye + forward * (FinaleTrunkDistance - 2.2f) + Vector3.Up * .25f, calm ? .3 : 1.5)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        if (!await NightmareWaitAsync(1.9, session)) return false;

        // 6. And then they are here.
        if (DevForestCaptureActive) GD.Print("finale: step 6 torch=" + (_torch?.LightEnergy ?? -1f) + " t=" + Time.GetTicksMsec());
        var arrive = calm ? .9 : .42;
        UiFoley.PlayWorld(this, eye + forward * 2f, "forest/rush_close", 3f, 20f, 8f);
        var rush = watcher.Root.CreateTween().SetParallel();
        rush.TweenProperty(watcher.Root, "global_position", eye + forward * 1.05f + Vector3.Up * .46f, arrive)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        foreach (var quad in new[] { watcher.Left, watcher.Right })
            rush.TweenProperty(quad, "scale", Vector3.One * .085f, arrive);
        if (!await NightmareWaitAsync(arrive - .04, session)) return false;
        BuildAttackRig(camera);
        _fingerCurlTarget = 1f;
        _fingerCurl = .55f;
        // The last spark of the torch: what has him is all around the edge of sight.
        _torchOverride = calm ? .5f : 1.2f;
        UiFoley.PlayWorld(this, eye, "forest/scare_sting", 3f, 20f, 8f);
        UiFoley.PlayWorld(this, eye, "forest/gasp", 1f, 10f, 4f);
        if (!await NightmareWaitAsync(calm ? .5 : .22, session)) return false;
        _torchOverride = 0f;
        PrologueDreamCut.Show(GetTree(), fadeSeconds: .04);
        UiFoley.PlayWorld(this, eye, "forest/body_fall", 2f, 10f, 4f);
        if (!await NightmareWaitAsync(.5, session)) return false;
        UiFoley.PlayWorld(this, eye, "forest/knockout_ring", -3f, 10f, 6f);
        StopNightmareSound(ref _dreadDrone, .4);
        StopNightmareSound(ref _dreadHeart, .9);
        EyeOwnGlow = EyeRestGlow;
        if (GodotObject.IsInstanceValid(trunk)) trunk.QueueFree();
        return await NightmareWaitAsync(.6, session);
    }

    // Finale staging, in metres and degrees.
    private const float FinaleTrunkDistance = 5.4f;
    private const float FinaleTrunkRadius = .33f;
    private const float FinaleHandHeight = 2.1f;
    private const float FinaleEyeHeight = 2.75f;
    private const float FinaleHandStartDegrees = 190f;
    private const float FinaleHandCreepDegrees = -128f;

    /// <summary>
    /// A thick old trunk with something behind it. The hand is built on the
    /// trunk's own surface: four fingers of four joints each, lying round the
    /// bark from the far side, and turned into view when the scene calls for it.
    /// </summary>
    private Node3D BuildFinaleTrunk(Vector3 at, Vector3 watcher)
    {
        var root = new Node3D { Name = "PrologueFinaleTrunk" };
        root.SetMeta("presentationOnly", true);
        AddChild(root);
        root.GlobalPosition = at;
        var toWatcher = watcher - at;
        root.Rotation = new Vector3(0f, Mathf.Atan2(toWatcher.X, toWatcher.Z), 0f); // +Z faces him
        var normal = PrologueTextureSlot("finale_trunk_bark_normal", null);
        var bark = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, .96f, .9f), Roughness = 1f, MetallicSpecular = .1f,
            AlbedoTexture = PrologueTextureSlot("finale_trunk_bark_albedo", "res://assets/textures/painterly/bark_pine_v1_albedo.png"),
            NormalEnabled = normal is not null, NormalTexture = normal,
            Uv1Scale = new Vector3(2f, 5f, 1f)
        };
        root.AddChild(new MeshInstance3D
        {
            Name = "Trunk",
            Mesh = new CylinderMesh { BottomRadius = FinaleTrunkRadius + .05f, TopRadius = FinaleTrunkRadius - .05f, Height = 11f, RadialSegments = 18, Rings = 6 },
            Position = new Vector3(0f, 5.2f, 0f), MaterialOverride = bark
        });
        // A flare of root, so it grows out of the drift.
        root.AddChild(new MeshInstance3D
        {
            Name = "RootFlare",
            Mesh = new CylinderMesh { BottomRadius = FinaleTrunkRadius + .2f, TopRadius = FinaleTrunkRadius + .04f, Height = .7f, RadialSegments = 14 },
            Position = new Vector3(0f, .25f, 0f), MaterialOverride = bark
        });
        var skin = new StandardMaterial3D
        {
            AlbedoColor = new Color(.78f, .74f, .66f), Roughness = .8f, MetallicSpecular = .3f,
            AlbedoTexture = PrologueTextureSlot("finale_hand_skin_albedo", null)
        };
        // The fingers are authored reaching round the side that faces +X; the node is
        // first turned so they are wholly behind the trunk, then crept round into view.
        var hand = new Node3D { Name = "Hand", Position = new Vector3(0f, FinaleHandHeight, 0f), Visible = false };
        hand.Rotation = new Vector3(0f, Mathf.DegToRad(FinaleHandStartDegrees), 0f);
        root.AddChild(hand);
        var radius = FinaleTrunkRadius + .06f; // the trunk is thicker at hand height than at its mean
        for (var finger = 0; finger < 4; finger++)
        {
            var height = (1.5f - finger) * .17f + _dreadRng.RandfRange(-.02f, .02f);
            var length = new[] { .92f, 1.05f, 1f, .8f }[finger];
            Vector3 previous = default;
            for (var joint = 0; joint <= 4; joint++)
            {
                var angle = Mathf.DegToRad(joint * 36f * length);
                var lift = joint == 4 ? .035f : joint == 2 ? .012f : 0f; // the tip stands off the bark
                var point = new Vector3(Mathf.Cos(angle) * (radius + lift), height - joint * (.022f + finger * .012f), Mathf.Sin(angle) * (radius + lift));
                if (joint > 0)
                {
                    var thick = Mathf.Lerp(.055f, .02f, joint / 4f);
                    var along = point - previous;
                    var bone = new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { Height = along.Length() + .004f, BottomRadius = thick, TopRadius = thick * .8f, RadialSegments = 6, Rings = 1 },
                        MaterialOverride = skin, Position = (previous + point) * .5f
                    };
                    hand.AddChild(bone);
                    bone.Quaternion = new Quaternion(Vector3.Up, along.Normalized());
                    hand.AddChild(new MeshInstance3D
                    {
                        Mesh = new SphereMesh { Radius = thick * 1.3f, Height = thick * 2.6f, RadialSegments = 6, Rings = 3 },
                        MaterialOverride = skin, Position = previous
                    });
                }
                previous = point;
            }
        }
        return root;
    }

    /// <summary>
    /// An optional generated texture. Drop <c>name.png</c> into
    /// res://assets/textures/prologue/ and it is used; until then the fallback (or nothing) is.
    /// </summary>
    internal static Texture2D? PrologueTextureSlot(string name, string? fallback)
    {
        var path = $"res://assets/textures/prologue/{name}.png";
        if (ResourceLoader.Exists(path)) return ResourceLoader.Load<Texture2D>(path);
        return fallback is not null && ResourceLoader.Exists(fallback) ? ResourceLoader.Load<Texture2D>(fallback) : null;
    }

    /// <summary>Hands the view back to the walker's own camera and removes the hands.</summary>
    private void ReleaseFallCamera(FirstPersonController? player)
    {
        var lift = _fallCamera;
        _fallCamera = null;
        var wasCurrent = lift is not null && GodotObject.IsInstanceValid(lift) && lift.Current;
        var hadRig = _attackRig is not null;
        ReleaseAttackRig();
        if (_tallOne is { } tall && GodotObject.IsInstanceValid(tall)) tall.Visible = false;
        if (lift is null && !hadRig) return;
        player ??= IsInsideTree() ? GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController : null;
        if (player is null || !GodotObject.IsInstanceValid(player)) return;
        player.SetCaptureBodyHidden(false);
        if (wasCurrent) player.GetNodeOrNull<Camera3D>("Head/Camera3D")?.MakeCurrent();
    }
}
