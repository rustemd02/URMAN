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
    private const float TorchEnergy = 3.6f;
    private const float TorchRange = 27f;
    private const float TorchAngle = 26f;
    private const float TorchLag = 9f;
    private const int EyePoolSize = 10;
    private const float EyeNearClose = 9f;
    private const float EyeStareSeconds = .5f;
    private const float EyeMaxAge = 18f;
    private const float EyeOwnGlow = .26f;
    private const double DreadScareSpacing = 8;
    private const string DreadDroneStream = "res://assets/audio/act1/foley/forest/dread_drone.wav";
    private const string DreadHeartStream = "res://assets/audio/act1/foley/forest/heartbeat.wav";
    private const string DreadCrowsStream = "res://assets/audio/act1/sound_mood/crows.wav";

    private const string EyeShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, fog_disabled, shadows_disabled;
        uniform vec3 tint : source_color = vec3(1.0, 0.74, 0.16);
        uniform float open = 1.0;
        uniform float shine = 0.3;
        uniform float slit = 0.0;
        void fragment() {
            vec2 q = UV * 2.0 - 1.0;
            float lid = max(open, 0.02);
            // An almond, not a dot: wider than tall, closing like a lid.
            float d = length(vec2(q.x, q.y / (lid * 0.62)));
            float mask = smoothstep(1.0, 0.84, d);
            float upright = (1.0 - smoothstep(0.05, 0.11, abs(q.x))) * (1.0 - smoothstep(0.3, 0.56, abs(q.y)));
            float level = (1.0 - smoothstep(0.05, 0.11, abs(q.y))) * (1.0 - smoothstep(0.42, 0.74, abs(q.x)));
            float pupil = mix(upright, level, step(0.5, slit));
            float hot = 1.0 - smoothstep(0.0, 0.9, d);
            vec3 colour = tint * (0.45 + hot * 1.1);
            colour = mix(colour, vec3(0.0), pupil * 0.94);
            ALBEDO = colour * (0.2 + shine * 2.8);
            ALPHA = mask * clamp(open * 4.0, 0.0, 1.0);
        }
        """;

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
            SpotAttenuation = 1.15f,
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

        // A few things that can really happen in a winter forest. Silence
        // between them leaves the footsteps and the light at the hut legible.
        if (DreadScare(1, fraction > .14f, time)) SnowFallsFromABough(progress);
        if (DreadScare(2, fraction > .27f, time))
            UiFoley.PlayWorld(this, player.GlobalPosition + player.GlobalBasis.Z*10f + Vector3.Up,
                "forest/branch_crack", -9f, 35f, 5f);
        if (DreadScare(3, fraction > .40f, time)) BirdsBreakCover(progress);
        if (DreadScare(4, fraction > .76f, time))
            UiFoley.PlayWorld(this, DreadAround(player,55f,75f,1f),"forest/fox_scream",-14f,140f,20f);
        if (DreadScare(8, fraction > .92f, time))
            StartNightmareSound(ref _dreadHeart,DreadHeartStream,-22f);

    }

    /// <summary>Look, torch and eyes; also runs through the climax, when the walk logic is parked.</summary>
    private void TickDreadPresentation(FirstPersonController player, float fraction, double time, float delta)
    {
        EnforceNightmareLook();
        var camera = _fallCamera is { } fall && GodotObject.IsInstanceValid(fall) ? fall : GetViewport().GetCamera3D();
        if (camera is null) return;
        UpdateTorch(player, camera, fraction, delta);
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
        var target = eye.Basis.Orthonormalized() * wobble;
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
        pair.Left.Scale = pair.Right.Scale = Vector3.One * size;
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
                Size = new Vector2(.07f, .07f),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(.9f, .94f, 1f),
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
        var skin = new StandardMaterial3D { AlbedoColor = new Color(.2f, .185f, .165f), Roughness = .9f, MetallicSpecular = .2f };
        var lengths = new[] { .17f, .15f, .13f, .11f };
        foreach (var side in new[] { -1f, 1f })
        {
            var direction = -side;
            for (var finger = 0; finger < 5; finger++)
            {
                var root = new Node3D
                {
                    Position = new Vector3(side * .62f, (finger - 2) * .075f - .02f, -.42f - Mathf.Abs(finger - 2) * .02f),
                    RotationDegrees = new Vector3(0f, 0f, (finger - 2) * 9f * direction)
                };
                rig.AddChild(root);
                var parent = root;
                for (var segment = 0; segment < lengths.Length; segment++)
                {
                    var joint = new Node3D { Position = segment == 0 ? Vector3.Zero : new Vector3(direction * lengths[segment - 1], 0f, 0f) };
                    parent.AddChild(joint);
                    var thick = Mathf.Lerp(.014f, .005f, segment / 3f);
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
        // Its eyes, an arm's length from his.
        var material = NewEyeMaterial();
        material.SetShaderParameter("tint", new Color(1f, .8f, .3f));
        material.SetShaderParameter("slit", 1f);
        material.SetShaderParameter("shine", 1f);
        material.SetShaderParameter("open", 1f);
        foreach (var x in new[] { -.075f, .075f })
        {
            var eye = NewEyeQuad(material);
            eye.Position = new Vector3(x, .09f, -.52f);
            eye.Scale = Vector3.One * .085f;
            rig.AddChild(eye);
        }
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
        var camera=GetViewport().GetCamera3D();
        if(camera is null)return true;
        var eye=camera.GlobalPosition;
        var forward=-camera.GlobalBasis.Z;
        HideDreadActors();
        _torchOverride=0f;
        if(_nightmareEyeLight is { } light)
            light.CreateTween().TweenProperty(light,"light_energy",NightSightEnergy*.15f,1.8);
        StartNightmareSound(ref _dreadHeart,DreadHeartStream,-19f);
        if(!await NightmareWaitAsync(1.6,session))return false;
        var ahead=ToLocal(eye+forward*11f);
        var voice=PlayPrologueForestVoice(new(ahead.X,ahead.Z),"forest-come-here",
            "«Айдар. Кил монда…»\n(Айдар, иди сюда…)",-7f);
        if(!await NightmareWaitAsync(Math.Clamp(voice,1.6,3.4)+.4,session))return false;
        // The view contains only the forest. A sound closes from behind, then
        // consciousness cuts out before its source could be identified.
        UiFoley.PlayWorld(this,eye+camera.GlobalBasis.Z*4f+camera.GlobalBasis.X*2f,
            "forest/brush_rustle",-8f,15f,3f);
        if(!await NightmareWaitAsync(.6,session))return false;
        PrologueDreamCut.Show(GetTree(),fadeSeconds:_accessibility.ReducedMotion ? .6 : .32);
        UiFoley.PlayWorld(this,eye,"forest/gasp",-8f,10f,2f);
        if(!await NightmareWaitAsync(.35,session))return false;
        UiFoley.PlayWorld(this,eye+camera.GlobalBasis.Z,"forest/rush_close",-12f,12f,3f);
        StopNightmareSound(ref _dreadDrone,.4);
        StopNightmareSound(ref _dreadHeart,.6);
        return await NightmareWaitAsync(.45,session);
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
