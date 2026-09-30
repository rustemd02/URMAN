using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // N2.1 prologue (review 2026-09-28, beat sheet P1-P2): the first game
    // image is a short walk in the night forest at the Kara-Urman edge - a
    // flash-forward of a future episode, cut short by an unseen movement and
    // a quiet loss of focus, where Mansur babai's voice wakes Aidar in the Niva.
    // The teaser grants no story progress. Its owner also restores the camera,
    // input and overlays when a later part of the opening is skipped.
    private bool _prologueForestActive;
    private bool _prologueSkipRequested;
    private Vector3 _prologueStartFeet;
    private double _prologueElapsed;
    private CanvasLayer? _prologueOverlay;
    private ColorRect? _prologueBlackout;
    private Label? _prologueCaption;
    private Tween? _prologueFade;

    private const string PrologueForestZone = "kara_urman_night";
    private const string PrologueForestSpawn = "forest-approach";

    private async Task<bool> RunPrologueForestAsync()
    {
        if (_main is null || _player is null)
        {
            return false;
        }

        _main.SwitchZone(PrologueForestZone, PrologueForestSpawn);
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge?.CurrentZoneId != PrologueForestZone)
        {
            // Without the night zone there is no teaser to show; the arrival
            // keeps its previous opening rather than a broken black screen.
            GD.PushError("act1-prologue: night forest zone unavailable; keeping the arrival opening.");
            return false;
        }

        _prologueForestActive = true;
        _prologueSkipRequested = false;
        _forestSilence = false;
        _prologueElapsed = 0;
        // Author feedback 2026-09-29: the flash-forward happens in its own deep
        // forest, not at the village's Kara-Urman edge. The logical zone keeps
        // the night atmosphere; the location owns ground and walkable window.
        _deepForest = new PrologueDeepForest();
        _main.AddChild(_deepForest);
        FirstPersonController.DetachedWorldGuard = PrologueDeepForest.Guard;
        _player.ApplyZoneSpawn(_deepForest.StartPosition, 0f);
        _prologueStartFeet = _player.GlobalPosition;
        _player.SetModalOpen(false);
        BuildPrologueOverlay();

        var walkStarted = Time.GetTicksMsec();
        await RunDeepForestWalkAsync(_deepForest);
        GD.Print($"act1-prologue: deep-forest walk {(Time.GetTicksMsec() - walkStarted) / 1000.0:0.0}s skipped={_prologueSkipRequested}");
        if (!IsInsideTree()) return false;

        if (!_prologueSkipRequested)
        {
            await PlayPrologueCueAsync();
        }
        ReleaseDeepForest();
        if (_prologueSkipRequested)
        {
            _main.SwitchZone("village_day", "arrival");
        }
        else
        {
            // The blackout caption hands straight into the Niva ride (P3/P4);
            // it ends on its own fade, still inside this prologue block.
            await RunPrologueNivaRideAsync();
            FadePrologueBlackout(visible: false);
            await PrologueWaitAsync(.3);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ReleasePrologueOverlay();
        _prologueForestActive = false;
        return !_prologueSkipRequested;
    }

    // The image loses coherence before consciousness fades. Nothing strikes the
    // player: the treeline interrupts itself at the edge of his vision, while a
    // familiar voice slowly becomes the voice inside the car.
    private async Task PlayPrologueCueAsync()
    {
        if (_player is null || _prologueOverlay is null) return;
        _player.SetModalOpen(true);
        var head = _player.GetNodeOrNull<Node3D>("Head");
        var headTransform = head?.Transform ?? Transform3D.Identity;
        var bodyYaw = _player.RotationDegrees.Y;
        ProloguePresence? presence = null;
        var cueTweens = new List<Tween>();
        try
        {
            _forestSilence = true;
            StartNameCalls();
            UiFoley.PlayWorld(this, _player.GlobalPosition + Vector3.Up * .3f, "forest/heartbeat", -22f, 10f, 3f);
            // A small listening turn; no forced spin to present a monster.
            if (!_player.ReducedMotion)
            {
                var turn = CreateTween();
                cueTweens.Add(turn);
                turn.TweenProperty(_player, "rotation_degrees:y", bodyYaw + 24f, 1.7).SetTrans(Tween.TransitionType.Sine);
            }
            await PrologueWaitAsync(1.9);
            if (_prologueSkipRequested) return;
            var forward = -_player.GlobalBasis.Z;
            var side = _player.GlobalBasis.X;
            var at = _player.GlobalPosition + forward * 6.8f + side * 5.3f + Vector3.Up * 1.1f;
            presence = new ProloguePresence { Name = "PeripheralForestPresence" };
            _deepForest?.AddChild(presence);
            if (!presence.IsInsideTree()) return;
            presence.GlobalPosition = at;
            presence.LookAt(_player.GlobalPosition + Vector3.Up * 1.2f);
            var elapsed = 0d;
            while (elapsed < 4.1 && !_prologueSkipRequested && IsInsideTree())
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                elapsed += GetProcessDeltaTime();
                // Irregular slow occlusion, never a flashing alpha or a charge.
                var envelope = Mathf.Sin(Mathf.Clamp((float)elapsed / 4.1f, 0, 1) * Mathf.Pi);
                presence.SetVeil(envelope * .55f, (float)elapsed);
                if (elapsed > 1.5 && _prologueDesaturate is null) DrainPrologueColour(.75f, 2.4);
            }
            if (_prologueSkipRequested) return;
            _nameCallsFade = true;
            // A shallow involuntary loss of focus, with reduced motion honoured.
            if (head is not null && !_player.ReducedMotion)
            {
                var drift = CreateTween();
                cueTweens.Add(drift);
                drift.TweenProperty(head, "rotation_degrees:z", head.RotationDegrees.Z + 1.4f, 2.8)
                    .SetTrans(Tween.TransitionType.Sine);
            }
            SlowPrologueBlackout(1f, 3.0);
            // The soft name arrives before complete black: sound bridges the
            // subjective forest and the passenger seat without a death sting.
            var babai = PrologueVoice.PlayClip(this, "forest-babai-name-v2");
            ShowPrologueCaption("…Айдар… Айдар…", Math.Max(3, babai));
            await PrologueWaitAsync(Math.Max(3.1, babai + .15));
            StopNameCalls();
            if (_prologueSkipRequested) return;
            var waking = PrologueVoice.PlayClip(this, "forest-wake-v2");
            ShowPrologueCaption("Мансур бабай: «Айдар… Айдар, уян. Килеп җитәбез.»\n(Айдар… Айдар, проснись. Уже подъезжаем.)", Math.Max(5, waking + .3));
            await PrologueWaitAsync(Math.Max(2.4, waking + .3));
        }
        finally
        {
            foreach (var tween in cueTweens) if (IsInstanceValid(tween)) tween.Kill();
            if (presence is not null && IsInstanceValid(presence)) presence.QueueFree();
            if (head is not null && IsInstanceValid(head)) head.Transform = headTransform;
            if (_player is not null && IsInstanceValid(_player)) _player.RotationDegrees = _player.RotationDegrees with { Y = bodyYaw };
            StopNameCalls();
            DrainPrologueColour(0f, 0);
            if (_deepForest?.Silhouette is { } figure && IsInstanceValid(figure)) figure.Visible = false;
        }
    }

    // "Айдар..." over and over from behind, each call from a slightly different place.
    // When the colour drains it thins out under babai's voice instead of stopping.
    private bool _nameCallsActive;
    private bool _nameCallsFade;
    private int _nameCallsRevision;

    private async void StartNameCalls()
    {
        if (_nameCallsActive) return;
        _nameCallsActive = true;
        _nameCallsFade = false;
        var revision = ++_nameCallsRevision;
        var volume = -10f;
        var index = 0;
        while (_nameCallsActive && revision == _nameCallsRevision && IsInsideTree() && _player is not null && !_prologueSkipRequested)
        {
            if (_nameCallsFade) volume -= 3.5f;
            if (volume < -30f) break;
            var behind = _player.GlobalBasis.Z.Normalized();
            var side = _player.GlobalBasis.X.Normalized() * (index % 2 == 0 ? -1f : 1f) * 3f;
            PrologueVoice.PlayAt(this, _player.GlobalPosition + behind * (9f - Mathf.Min(index, 5)) + side + Vector3.Up * 1.6f,
                "forest-name-call", volume, 60f);
            index++;
            await PrologueWaitAsync(_nameCallsFade ? 2.8 : 2.7 + (index % 3) * .6);
        }
        if (revision == _nameCallsRevision) _nameCallsActive = false;
    }

    private void StopNameCalls()
    {
        _nameCallsActive = false;
        _nameCallsRevision++;
        PrologueVoice.StopSpatial(this);
    }

    // Screen-space desaturation above the world and under the blackout (0 = full colour).
    private ColorRect? _prologueDesaturate;
    private Tween? _prologueFocusTween;

    private void DrainPrologueColour(float amount, double seconds)
    {
        if (_prologueOverlay is null || !IsInstanceValid(_prologueOverlay)) return;
        if (_prologueDesaturate is null || !IsInstanceValid(_prologueDesaturate))
        {
            if (amount <= 0f) return;
            _prologueDesaturate = new ColorRect
            {
                Name = "PrologueDesaturate",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = new ShaderMaterial
                {
                    Shader = new Shader
                    {
                        Code = """
                            shader_type canvas_item;
                            uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
                            uniform float amount = 0.0;
                            void fragment() {
                                vec2 d = SCREEN_PIXEL_SIZE * amount * 1.6;
                                vec3 c = texture(screen_tex, SCREEN_UV).rgb * 0.4;
                                c += texture(screen_tex, SCREEN_UV + vec2(d.x,0)).rgb * 0.15;
                                c += texture(screen_tex, SCREEN_UV - vec2(d.x,0)).rgb * 0.15;
                                c += texture(screen_tex, SCREEN_UV + vec2(0,d.y)).rgb * 0.15;
                                c += texture(screen_tex, SCREEN_UV - vec2(0,d.y)).rgb * 0.15;
                                float g = dot(c, vec3(0.299,0.587,0.114));
                                float edge = smoothstep(0.19,0.66,length((SCREEN_UV-0.5)*vec2(1.25,1.0)));
                                COLOR = vec4(mix(c,vec3(g)*0.86,amount*.7)*(1.0-edge*amount*.48),1.0);
                            }
                            """
                    }
                }
            };
            StretchFullScreen(_prologueDesaturate);
            _prologueOverlay.AddChild(_prologueDesaturate);
            _prologueOverlay.MoveChild(_prologueDesaturate, 0);
        }
        _prologueFocusTween?.Kill();
        var material = (ShaderMaterial)_prologueDesaturate.Material;
        if (seconds <= 0)
        {
            material.SetShaderParameter("amount", amount);
            if (amount <= 0f) { _prologueDesaturate.QueueFree(); _prologueDesaturate = null; }
            return;
        }
        _prologueFocusTween = CreateTween();
        _prologueFocusTween.TweenMethod(Callable.From<float>(value => { if (IsInstanceValid(material)) material.SetShaderParameter("amount", value); }),
            (float)material.GetShaderParameter("amount"), amount, seconds);
    }

    private void FlashPrologueBlackout(float alpha, double seconds)
    {
        if (_prologueBlackout is null) return;
        _prologueFade?.Kill();
        _prologueFade = CreateTween();
        _prologueFade.TweenProperty(_prologueBlackout, "color:a", alpha, .04);
        _prologueFade.TweenProperty(_prologueBlackout, "color:a", alpha * .35f, seconds);
    }

    private void SlowPrologueBlackout(float alpha, double seconds)
    {
        if (_prologueBlackout is null) return;
        _prologueFade?.Kill();
        _prologueFade = CreateTween();
        _prologueFade.TweenProperty(_prologueBlackout, "color:a", alpha, seconds).SetTrans(Tween.TransitionType.Sine);
    }

    private PrologueDeepForest? _deepForest;
    private bool _forestSilence;
    private ulong _prologueCaptionToken;

    internal PrologueDeepForest? DeepForest => _deepForest;

    // The walk through the deep forest: fresh prints lead along an old track;
    // each staged event fires by distance walked, with timed fallbacks for a
    // player who stands still. Nothing is collected; there is no gate.
    private async Task RunDeepForestWalkAsync(PrologueDeepForest forest)
    {
        var fired = new bool[24];
        var nextAmbient = 4.0;
        var best = 0f;
        var lastGain = 0d;
        var silhouetteAt = -1d;
        var glintsAt = -1d;
        var clearingAt = -1d;
        while (IsInsideTree() && !_prologueSkipRequested && _player is not null)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var delta = GetProcessDeltaTime();
            _prologueElapsed += delta;
            var t = _prologueElapsed;
            var feet = _player.GlobalPosition;
            forest.FollowViewer(feet);
            var p = forest.Progress(feet);
            if (p > best + .5f) { best = p; lastGain = t; }

            if (!fired[0] && t > 1.2) { fired[0] = true; ShowPrologueCaption("Урман. Несколько дней спустя.", 4.5); }
            if (!fired[14] && t > 2.2)
            {
                fired[14] = true;
                // A cold low bed under the whole walk: the forest is not empty.
                UiFoley.PlayWorld(this, feet + Vector3.Up * 1.5f, "forest/dread_drone", -20f, 60f, 30f);
            }
            if (!fired[15] && p >= 24f) { fired[15] = true; ForestVoiceCall(feet, "forest-call-hey", 38f, -55f, -3f); }
            if (!fired[16] && p >= 78f) { fired[16] = true; ForestVoiceCall(feet, "forest-call-far", 46f, 62f, 0f); }
            if (!fired[17] && p >= 138f) { fired[17] = true; ForestVoiceCall(feet, "forest-call-hey", 30f, 165f, 2f); }
            if (!fired[1] && p >= 14f) { fired[1] = true; ShowPrologueCaption("Свежие следы. Кто-то прошёл здесь совсем недавно.", 4.5); }
            if (!fired[2] && p >= 32f)
            {
                fired[2] = true;
                KnockInTheDark(forest.ToWorld(PrologueDeepForest.TrackX(-40f) - 22f, -40f, 2f));
            }
            if (!fired[3] && p >= 50f) { fired[3] = true; ShowPrologueCaption("Высоко на ветке — синяя детская варежка. Рукой туда не достать.", 5); }
            if (!fired[7] && p >= -PrologueDeepForest.FallenSpruceZ - 9f)
            {
                fired[7] = true;
                ShowPrologueCaption("Поперёк просеки лежит ель. Следы уходят в обход, через сугроб справа.", 5);
            }
            if (!fired[4] && p >= 106f && forest.Silhouette is { } figure)
            {
                fired[4] = true;
                figure.Visible = true;
                silhouetteAt = t;
                UiFoley.PlayWorld(this, figure.GlobalPosition + Vector3.Up * 2f, "hollow_board");
                ShowPrologueCaption("«Айдар…»", Math.Max(2.5, PrologueVoice.PlayClip(this, "forest-name-call") + .5));
            }
            if (silhouetteAt >= 0 && forest.Silhouette is { Visible: true } shown)
            {
                var toFigure = (shown.GlobalPosition + Vector3.Up * 2f - _player.GlobalPosition).Normalized();
                var facing = -(_player.GetNodeOrNull<Camera3D>("Head/Camera3D")?.GlobalBasis.Z ?? _player.GlobalBasis.Z);
                // Gone the moment it is looked at, or after a breath if not.
                if (t - silhouetteAt > 2.8 || (t - silhouetteAt > .45 && facing.Dot(toFigure) > .95f))
                {
                    shown.Visible = false;
                    UiFoley.PlayWorld(this, shown.GlobalPosition + Vector3.Up, "wood_tap");
                }
            }
            if (!fired[8] && p >= -PrologueDeepForest.HutZ - 22f)
            {
                fired[8] = true;
                ShowPrologueCaption("В стороне — избушка. В окне горит свет.", 4.5);
            }
            if (!fired[9] && fired[8] && p >= -PrologueDeepForest.HutZ - 8f)
            {
                fired[9] = true;
                HutScare(forest);
            }
            if (!fired[5] && p >= 138f) { fired[5] = true; glintsAt = t; _glintLookedAt.Clear(); }
            if (glintsAt >= 0) UpdateEyeGlints(forest, t - glintsAt);
            if (!fired[6] && p >= -PrologueDeepForest.ClearingZ - 7f)
            {
                fired[6] = true;
                clearingAt = t;
                ShowPrologueCaption("Следы обрываются посреди поляны. Дальше — ни одного.", 5);
            }
            if (fired[6] && (t - clearingAt > 6.5 || p >= -PrologueDeepForest.ClearingZ + 4f))
            {
                var callLength = PrologueVoice.PlayClip(this, "forest-come-here");
                ShowPrologueCaption("«Айдар. Кил монда…»", Math.Max(2.5, callLength + .5));
                await PrologueWaitAsync(Math.Max(1.9, Math.Min(callLength, 8)));
                break;
            }
            // The living night: owls high in the crowns, cracks, rustles close
            // by, and later sounds that do not belong to any animal.
            if (t >= nextAmbient && !_forestSilence)
            {
                nextAmbient = t + 4.5 + _forestRng.Randf() * 6.5;
                var owl = ForestAmbientEvent(forest, p);
                _ = owl;
            }
            if (!fired[10] && p >= 20f)
            {
                fired[10] = true;
                ForestSound(forest, feet, "forest/wolf_far", 95f, 4f, -4f, 260f, 40f);
            }
            if (!fired[11] && p >= 88f)
            {
                fired[11] = true;
                RunAcrossTrack(forest, feet);
            }
            if (!fired[12] && p >= 128f)
            {
                fired[12] = true;
                var screamAt = ForestSoundAt(feet, "forest/fox_scream", 38f, 1f, -3f, 140f, 18f);
                ShowScreamSource(forest, screamAt);
            }
            if (!fired[13] && p >= 184f)
            {
                fired[13] = true;
                ForestSound(forest, feet, "forest/low_moan", 22f, .5f, -6f, 80f, 10f);
            }
            if (t - lastGain > 22 && t > 20) { lastGain = t; ShowPrologueCaption("Следы ведут дальше по просеке.", 4); }
            if (t > 300) break;
        }
    }

    private readonly RandomNumberGenerator _forestRng = new() { Seed = 0x55524d34 };

    // One randomly chosen night sound around the walker. Returns true for an owl.
    private bool ForestAmbientEvent(PrologueDeepForest forest, float progress)
    {
        if (_player is null) return false;
        var feet = _player.GlobalPosition;
        var roll = _forestRng.Randf();
        if (roll < .3f)
        {
            ForestSound(forest, feet, _forestRng.Randf() < .5f ? "forest/owl_eagle" : "forest/owl_tawny",
                _forestRng.RandfRange(30f, 70f), _forestRng.RandfRange(12f, 22f), -5f, 150f, 16f);
            return true;
        }
        if (roll < .55f)
        {
            // Rustle in a real bush close by, and the bush moves.
            var near = forest.Bushes
                .Where(bush => bush.GlobalPosition.DistanceTo(feet) is > 4f and < 16f)
                .OrderBy(_ => _forestRng.Randf()).FirstOrDefault();
            if (near is not null)
            {
                forest.Shake(near, .8f);
                UiFoley.PlayWorld(this, near.GlobalPosition + Vector3.Up * .5f, "forest/brush_rustle", -6f, 30f, 4f);
                return false;
            }
        }
        if (roll < .72f)
        {
            ForestSound(forest, feet, "forest/branch_crack", _forestRng.RandfRange(12f, 35f), 2f, -6f, 70f, 7f);
            return false;
        }
        if (roll < .86f && progress > 95f)
        {
            ForestSound(forest, feet, "forest/strange_clicks", _forestRng.RandfRange(10f, 20f), 1.5f, -9f, 45f, 5f);
            return false;
        }
        UiFoley.PlayWorld(this, feet + Vector3.Up * 6f, "forest/wind_gust", -12f, 60f, 20f);
        return false;
    }

    // A sound at a random bearing and distance from the walker.
    private void ForestSound(PrologueDeepForest forest, Vector3 from, string sample, float distance, float height,
        float volumeDb, float maxDistance, float unitSize) =>
        ForestSoundAt(from, sample, distance, height, volumeDb, maxDistance, unitSize);

    private Vector3 ForestSoundAt(Vector3 from, string sample, float distance, float height,
        float volumeDb, float maxDistance, float unitSize)
    {
        var angle = _forestRng.RandfRange(0, Mathf.Tau);
        var at = from + new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance);
        UiFoley.PlayWorld(this, at, sample, volumeDb, maxDistance, unitSize);
        return at;
    }

    // The thing that screamed has a life of about a second: the tall figure
    // stands where the cry came from and is gone, leaving only a snow puff.
    private async void ShowScreamSource(PrologueDeepForest forest, Vector3 cryAt)
    {
        if (forest.Silhouette is not { } figure || _player is null) return;
        var toward = (cryAt - _player.GlobalPosition) with { Y = 0 };
        var spot = _player.GlobalPosition + toward.Normalized() * Math.Min(toward.Length(), 26f);
        var local = spot - PrologueDeepForest.Origin;
        figure.GlobalPosition = forest.ToWorld(local.X, local.Z);
        figure.LookAt(new Vector3(_player.GlobalPosition.X, figure.GlobalPosition.Y, _player.GlobalPosition.Z), Vector3.Up, true);
        figure.Visible = true;
        await ToSignal(GetTree().CreateTimer(.9), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(figure)) return;
        figure.Visible = false;
        forest.SnowPuff(figure.GlobalPosition + Vector3.Up * .4f);
    }

    // Something crosses the track a few metres ahead, left to right, through
    // the bushes: heard moving, seen only as shaking snow.
    private async void RunAcrossTrack(PrologueDeepForest forest, Vector3 feet)
    {
        var local = feet - PrologueDeepForest.Origin;
        // Far ahead and quick: a glimpse, not a show (author feedback 2026-09-29).
        var z = local.Z - 21f;
        var cx = PrologueDeepForest.TrackX(z);
        var from = forest.ToWorld(cx - 16f, z, .5f);
        var to = forest.ToWorld(cx + 16f, z - 4f, .5f);
        var runner = UiFoley.PlayWorld(this, from, "forest/runner_past", -9f, 45f, 6f);
        const double duration = 1.15;
        var elapsed = 0d;
        var puffAt = 0d;
        while (elapsed < duration && IsInsideTree() && _prologueForestActive)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += GetProcessDeltaTime();
            var position = from.Lerp(to, (float)(elapsed / duration));
            if (runner is not null && IsInstanceValid(runner)) runner.GlobalPosition = position;
            if (elapsed >= puffAt)
            {
                puffAt += .5;
                if (Mathf.Abs(position.X - forest.ToWorld(cx, z).X) < 5f) forest.SnowPuff(position with { Y = position.Y - .2f });
                foreach (var bush in forest.Bushes.Where(bush => bush.GlobalPosition.DistanceTo(position) < 2.6f))
                    forest.Shake(bush, 1.2f);
            }
        }
    }

    private async void KnockInTheDark(Vector3 at)
    {
        foreach (var wait in new[] { 0d, .45, 1.2 })
        {
            if (wait > 0) await PrologueWaitAsync(wait);
            if (!_prologueForestActive || _prologueSkipRequested) return;
            UiFoley.PlayWorld(this, at, "wood_tap");
        }
    }

    /// <summary>A voice from among the trees: <paramref name="bearing"/> degrees off the way Aidar faces.</summary>
    private void ForestVoiceCall(Vector3 feet, string clip, float distance, float bearing, float volumeDb)
    {
        if (_player is null) return;
        var facing = -_player.GlobalBasis.Z;
        facing.Y = 0;
        var direction = facing.Normalized().Rotated(Vector3.Up, Mathf.DegToRad(bearing));
        PrologueVoice.PlayAt(this, feet + direction * distance + Vector3.Up * 1.7f, clip, volumeDb);
    }

    // The lit hut answers a step too close: the window flares twice, something
    // inside strikes the wall, the light dies. A short jolt, nothing shown.
    private async void HutScare(PrologueDeepForest forest)
    {
        var hutAt = forest.ToWorld(PrologueDeepForest.TrackX(PrologueDeepForest.HutZ) - 13f, PrologueDeepForest.HutZ, 1.4f);
        for (var flicker = 0; flicker < 2; flicker++)
        {
            forest.HutLight!.Visible = false;
            await ToSignal(GetTree().CreateTimer(.11), SceneTreeTimer.SignalName.Timeout);
            forest.HutLight.Visible = true;
            forest.HutLight.LightEnergy = 3.2f;
            await ToSignal(GetTree().CreateTimer(.14), SceneTreeTimer.SignalName.Timeout);
        }
        UiFoley.PlayWorld(this, hutAt, "hollow_board", 2f, 40f, 8f);
        UiFoley.PlayWorld(this, hutAt, "forest/scare_sting", -10f, 40f, 10f);
        forest.PutOutHutLight();
        FlashPrologueBlackout(.3f, .35);
    }

    // Eyes in the trees. They open one by one, follow the walker and blink; each
    // shuts for good the moment it is looked at, or after a few seconds.
    private readonly Dictionary<Node3D, double> _glintLookedAt = new();

    private void UpdateEyeGlints(PrologueDeepForest forest, double sinceStart)
    {
        if (_player is null) return;
        var camera = _player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        var facing = camera is null ? -_player.GlobalBasis.Z : -camera.GlobalBasis.Z;
        for (var index = 0; index < forest.EyeGlints.Count; index++)
        {
            var glint = forest.EyeGlints[index];
            var opensAt = 0.3 + index * .9;
            var alive = sinceStart > opensAt && sinceStart < opensAt + 6.5 && !_glintLookedAt.ContainsKey(glint);
            if (!alive)
            {
                if (glint.Visible)
                {
                    glint.Visible = false;
                    UiFoley.PlayWorld(this, glint.GlobalPosition, "forest/strange_clicks", -16f, 30f, 4f);
                }
                continue;
            }
            if (!glint.Visible)
            {
                glint.Visible = true;
                UiFoley.PlayWorld(this, glint.GlobalPosition, "forest/strange_clicks", -18f, 30f, 4f);
            }
            var target = _player.GlobalPosition + Vector3.Up * 1.5f;
            glint.LookAt(target, Vector3.Up, true);
            var blink = Math.Sin(sinceStart * 2.1 + index * 1.7) > .93;
            glint.Scale = new Vector3(1f, blink ? .08f : 1f, 1f);
            var toGlint = (glint.GlobalPosition - target).Normalized();
            var looked = facing.Dot(toGlint) > .965f;
            if (looked)
            {
                _glintLookedAt[glint] = sinceStart;
            }
        }
    }

    private async void ShowPrologueCaption(string text, double seconds)
    {
        if (_prologueCaption is null) return;
        var token = ++_prologueCaptionToken;
        _prologueCaption.Text = text;
        _prologueCaption.Visible = true;
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        if (token == _prologueCaptionToken && _prologueCaption is not null && IsInstanceValid(_prologueCaption))
            _prologueCaption.Visible = false;
    }

    private void ReleaseDeepForest()
    {
        PrologueVoice.Stop();
        FirstPersonController.DetachedWorldGuard = null;
        if (_deepForest is not null && IsInstanceValid(_deepForest)) _deepForest.QueueFree();
        _deepForest = null;
    }

    private async Task PrologueWaitAsync(double seconds)
    {
        while (seconds > 0 && IsInsideTree() && _prologueForestActive && !_prologueSkipRequested)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            seconds -= GetProcessDeltaTime();
        }
    }

    private void BuildPrologueOverlay(string skipText = "пропустить вступление")
    {
        _prologueOverlay = new CanvasLayer { Layer = 99, Name = "Act1PrologueOverlay" };
        AddChild(_prologueOverlay);
        _prologueBlackout = new ColorRect
        {
            Name = "PrologueBlackout",
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        StretchFullScreen(_prologueBlackout);
        _prologueOverlay.AddChild(_prologueBlackout);
        _prologueCaption = new Label
        {
            Name = "PrologueCaption",
            Text = string.Empty,
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _prologueCaption.AnchorRight = 1;
        _prologueCaption.AnchorTop = _prologueCaption.AnchorBottom = 1;
        _prologueCaption.OffsetTop = -120;
        _prologueCaption.OffsetBottom = -72;
        _prologueCaption.OffsetLeft = 64;
        _prologueCaption.OffsetRight = -64;
        _prologueCaption.AddThemeFontSizeOverride("font_size", (int)(21 * (_player?.Accessibility.TextScale ?? 1)));
        _prologueCaption.AddThemeColorOverride("font_color", new Color(0.92f, 0.9f, 0.82f));
        _prologueCaption.AddThemeColorOverride("font_outline_color", Colors.Black);
        _prologueCaption.AddThemeConstantOverride("outline_size", 6);
        _prologueOverlay.AddChild(_prologueCaption);
        var skip = new Label
        {
            Text = InputBindingService.ActionHint("ui_cancel", _player?.CurrentInputDevice == "gamepad") + " — " + skipText,
            AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -410, OffsetRight = -28,
            OffsetTop = 24, OffsetBottom = 64, HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        skip.AddThemeFontSizeOverride("font_size", 14);
        skip.AddThemeColorOverride("font_outline_color", Colors.Black);
        skip.AddThemeConstantOverride("outline_size", 4);
        skip.Modulate = new Color(1, 1, 1, .6f);
        _prologueOverlay.AddChild(skip);
        // Shown at the start, then it fades away instead of sitting on the cinematic frame like debug UI.
        var hintFade = CreateTween();
        hintFade.TweenInterval(6.0);
        hintFade.TweenProperty(skip, "modulate:a", 0f, 1.5);
    }

    private void FadePrologueBlackout(bool visible)
    {
        if (_prologueBlackout is null) return;
        _prologueFade?.Kill();
        _prologueFade = CreateTween();
        _prologueFade.TweenProperty(_prologueBlackout, "color:a", visible ? 1f : 0f,
            _player?.Accessibility.ReducedMotion == true ? 0f : .3f);
    }

    private void ReleasePrologueOverlay()
    {
        _prologueFade?.Kill();
        _prologueFade = null;
        if (_prologueOverlay is not null && IsInstanceValid(_prologueOverlay)) _prologueOverlay.QueueFree();
        _prologueOverlay = null;
        _prologueBlackout = null;
        _prologueCaption = null;
        StopNameCalls();
        PrologueVoice.Stop();
        _prologueFocusTween?.Kill();
        _prologueFocusTween = null;
        _prologueDesaturate = null;
    }

    private async Task ShowIntroAfterMenuAsync()
    {
        if (!MainMenuVisible)
        {
            return;
        }

        _mainMenu?.Dismiss();
        _mainMenu = null;
        // The menu press must finish before the prologue accepts skip input.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        try
        {
            if (_introReplay && _bridge is not null) await _bridge.SetFirstNightPassedAsync(false);
            await RunPrologueForestAsync();
        }
        catch (Exception error)
        {
            GD.PushError($"Act I prologue failed: {error}");
            if (IsInsideTree()) BuildMainMenu();
        }
        finally
        {
            _prologueForestActive = false;
            ReleaseDeepForest();
            ReleasePrologueRide();
            ReleasePrologueOverlay();
            if (IsInsideTree())
            {
                if (_introReplay)
                {
                    _introReplay = false;
                    BuildMainMenu();
                }
                _player?.SetModalOpen(MainMenuVisible);
                RefreshGameplayAudioPauseState();
                UpdateRouteCue();
                RecordM10Timing("intro-dismissed", force: true);
            }
        }
    }
}
