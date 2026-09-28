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
        _prologueElapsed = 0;
        _prologueStartFeet = _player.GlobalPosition;
        _player.SetModalOpen(false);
        BuildPrologueOverlay();

        // The walk itself: ordinary movement through the existing dark edge
        // until the player has covered the authored stretch, lingered long
        // enough, or asked to skip. There is nothing to collect and no gate.
        while (IsInsideTree())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _prologueElapsed += GetProcessDeltaTime();
            if (_prologueSkipRequested) break;
            var walked = _player.GlobalPosition.DistanceTo(_prologueStartFeet);
            // The authored stretch ends before the tree wall ahead; the
            // timeout covers a player who stops to look around instead.
            if (walked >= 5.5f || _prologueElapsed >= 12d) break;
        }

        if (!IsInsideTree()) return false;

        if (!_prologueSkipRequested)
        {
            await PlayPrologueCueAsync();
        }
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
        var behind = _player.GlobalPosition
            + (_player.GlobalBasis.Z with { Y = 0 }).Normalized() * 2.2f;
        UiFoley.PlayWorld(this, behind, "wood_tap");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_prologueSkipRequested) return;
        await PrologueWaitAsync(.65);
        if (_prologueSkipRequested) return;
        UiFoley.PlayWorld(this, behind, "metal_rattle");
        UiFoley.PlayWorld(this, _player.GlobalPosition, "hollow_board");
        FadePrologueBlackout(visible: true);
        await PrologueWaitAsync(.35);
        if (_prologueCaption is not null)
        {
            _prologueCaption.Text = "Мансур бабай: «Әй, уян. Приехали почти, внучек.»";
            _prologueCaption.Visible = true;
        }
        await PrologueWaitAsync(2.6);
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
