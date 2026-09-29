using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // N2.1 prologue (review 2026-09-28, beat sheet P1-P2): the first game
    // image is a short walk in the night forest at the Kara-Urman edge - a
    // flash-forward of a future episode, cut short by an unseen movement and
    // a sting into black, where Mansur babai's voice wakes Aidar in the Niva.
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

    // The unseen movement and the cut: close wooden steps beside the player,
    // a beat, then a sharp sting straight to black with babai's waking line.
    // Samples are existing foley placeholders until real voice/audio arrives
    // (external gate); the caption is the audible line's text alternative.
    private async Task PlayPrologueCueAsync()
    {
        if (_player is null || _prologueOverlay is null) return;
        _player.SetModalOpen(true);
        var head = _player.GetNodeOrNull<Node3D>("Head");
        var headTransform = head?.Transform ?? Transform3D.Identity;
        var bodyYaw = _player.RotationDegrees.Y;
        try
        {
            // The voice was behind. Aidar turns slowly - and there is nobody.
            var turn = CreateTween();
            turn.TweenProperty(_player, "rotation_degrees:y", bodyYaw + 180f, 1.1).SetTrans(Tween.TransitionType.Sine);
            _forestSilence = true;
            await PrologueWaitAsync(1.2);
            if (_prologueSkipRequested) return;
            // The forest goes quiet; only breath and the heart.
            UiFoley.PlayWorld(this, _player.GlobalPosition + Vector3.Up * .3f, "forest/heartbeat", -8f, 10f, 3f);
            ShowPrologueCaption("[тишина. никого.]", 2.2);
            await PrologueWaitAsync(2.3);
            if (_prologueSkipRequested) return;
            // Aidar turns back to the clearing...
            var back = CreateTween();
            back.TweenProperty(_player, "rotation_degrees:y", bodyYaw, .9).SetTrans(Tween.TransitionType.Sine);
            await PrologueWaitAsync(1.0);
            if (_prologueSkipRequested) return;

            // ...and something unseen seizes him from under the snow and drags
            // him down: the sting, a burst of snow, black. No creature is shown.
            UiFoley.PlayWorld(this, _player.GlobalPosition + Vector3.Up * 1.2f, "forest/scare_sting", 4f, 30f, 12f);
            UiFoley.PlayWorld(this, _player.GlobalPosition, "forest/body_fall", 0f, 20f, 6f);
            _deepForest?.SnowPuff(_player.GlobalPosition + Vector3.Up * 1.1f);
            if (head is not null)
            {
                var grab = CreateTween().SetParallel();
                grab.TweenProperty(head, "position:y", headTransform.Origin.Y - 1.55f, .16).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                grab.TweenProperty(head, "rotation_degrees", new Vector3(-62f, 0, 18f), .16).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            }
            await PrologueWaitAsync(.14);
            if (_prologueBlackout is not null)
            {
                _prologueFade?.Kill();
                _prologueBlackout.Color = new Color(.9f, .93f, 1f, .95f);
                _prologueFade = CreateTween();
                _prologueFade.TweenProperty(_prologueBlackout, "color", new Color(0, 0, 0, 1), .12);
            }
            await PrologueWaitAsync(1.3);
            if (_prologueSkipRequested) return;

            // Waking in the Niva: babai's voice first, in the dark.
            UiFoley.PlayWorld(this, _player.GlobalPosition + Vector3.Up * 1.5f, "forest/gasp", 0f, 10f, 4f);
            if (_prologueCaption is not null)
            {
                _prologueCaptionToken++;
                _prologueCaption.Text = "Мансур бабай: «Әй! Әй, улым, уян! Уян дим!»\n(Эй! Эй, сынок, проснись! Проснись, говорю!)";
                _prologueCaption.Visible = true;
            }
            await PrologueWaitAsync(2.4);
        }
        finally
        {
            if (head is not null && IsInstanceValid(head)) head.Transform = headTransform;
            if (_deepForest?.Silhouette is { } figure && IsInstanceValid(figure)) figure.Visible = false;
        }
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
        var fired = new bool[14];
        var nextAmbient = 4.0;
        var owlCaptioned = false;
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
            if (!fired[1] && p >= 14f) { fired[1] = true; ShowPrologueCaption("Свежие следы. Кто-то прошёл здесь совсем недавно.", 4.5); }
            if (!fired[2] && p >= 32f)
            {
                fired[2] = true;
                KnockInTheDark(forest.ToWorld(PrologueDeepForest.TrackX(-40f) - 22f, -40f, 2f));
                ShowPrologueCaption("[где-то слева, в глубине, стучат по дереву]", 4);
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
                ShowPrologueCaption("[между деревьями, тихо: «Айдар…»]", 4);
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
                forest.PutOutHutLight();
                UiFoley.PlayWorld(this, forest.ToWorld(PrologueDeepForest.TrackX(PrologueDeepForest.HutZ) - 13f, PrologueDeepForest.HutZ, 1.4f), "hollow_board");
                ShowPrologueCaption("Свет в окне погас.", 3.5);
            }
            if (!fired[5] && p >= 160f) { fired[5] = true; glintsAt = t; foreach (var glint in forest.EyeGlints) glint.Visible = true; }
            if (glintsAt >= 0)
                for (var index = 0; index < forest.EyeGlints.Count; index++)
                    if (t - glintsAt > 2.6 + index * .75) forest.EyeGlints[index].Visible = false;
            if (!fired[6] && p >= -PrologueDeepForest.ClearingZ - 7f)
            {
                fired[6] = true;
                clearingAt = t;
                ShowPrologueCaption("Следы обрываются посреди поляны. Дальше — ни одного.", 5);
            }
            if (fired[6] && (t - clearingAt > 6.5 || p >= -PrologueDeepForest.ClearingZ + 4f))
            {
                ShowPrologueCaption("[за спиной, совсем близко: «Айдар. Кил монда…»]", 3);
                await PrologueWaitAsync(1.9);
                break;
            }
            // The living night: owls high in the crowns, cracks, rustles close
            // by, and later sounds that do not belong to any animal.
            if (t >= nextAmbient && !_forestSilence)
            {
                nextAmbient = t + 4.5 + _forestRng.Randf() * 6.5;
                var owl = ForestAmbientEvent(forest, p);
                if (owl && !owlCaptioned) { owlCaptioned = true; ShowPrologueCaption("[где-то высоко, в кронах, ухает сова]", 3.5); }
            }
            if (!fired[10] && p >= 20f)
            {
                fired[10] = true;
                ForestSound(forest, feet, "forest/wolf_far", 95f, 4f, -4f, 260f, 40f);
                ShowPrologueCaption("[очень далеко воет волк]", 4);
            }
            if (!fired[11] && p >= 88f)
            {
                fired[11] = true;
                RunAcrossTrack(forest, feet);
                ShowPrologueCaption("[что-то быстро пробежало через просеку]", 4);
            }
            if (!fired[12] && p >= 128f)
            {
                fired[12] = true;
                ForestSound(forest, feet, "forest/fox_scream", 38f, 1f, -3f, 140f, 18f);
                ShowPrologueCaption("[короткий истошный крик в чаще]", 4);
            }
            if (!fired[13] && p >= 184f)
            {
                fired[13] = true;
                ForestSound(forest, feet, "forest/low_moan", 22f, .5f, -6f, 80f, 10f);
                ShowPrologueCaption("[низкий протяжный звук — не зверь и не ветер]", 4.5);
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
        float volumeDb, float maxDistance, float unitSize)
    {
        var angle = _forestRng.RandfRange(0, Mathf.Tau);
        var at = from + new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance);
        UiFoley.PlayWorld(this, at, sample, volumeDb, maxDistance, unitSize);
    }

    // Something crosses the track a few metres ahead, left to right, through
    // the bushes: heard moving, seen only as shaking snow.
    private async void RunAcrossTrack(PrologueDeepForest forest, Vector3 feet)
    {
        var local = feet - PrologueDeepForest.Origin;
        var z = local.Z - 9f;
        var cx = PrologueDeepForest.TrackX(z);
        var from = forest.ToWorld(cx - 14f, z, .5f);
        var to = forest.ToWorld(cx + 14f, z - 3f, .5f);
        var runner = UiFoley.PlayWorld(this, from, "forest/runner_past", -2f, 45f, 6f);
        const double duration = 1.7;
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
                puffAt += .22;
                forest.SnowPuff(position with { Y = position.Y - .2f });
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
        skip.AddThemeFontSizeOverride("font_size", 18);
        skip.AddThemeColorOverride("font_outline_color", Colors.Black);
        skip.AddThemeConstantOverride("outline_size", 5);
        _prologueOverlay.AddChild(skip);
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
