using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // N2.1 prologue (review 2026-09-28, beat sheet P1-P2): the first game
    // image is a short walk in the night forest at the Kara-Urman edge - a
    // flash-forward of a future episode, cut short by an unseen movement and
    // a sting into black, where Mansur babai's voice wakes Aidar in the Niva.
    // No knowledge, vocabulary or story effects: watching and skipping leave
    // exactly the same state. One confirm press during the teaser skips the
    // whole prologue block, so the ordinary single-E flow of every existing
    // start contract keeps working.
    private bool _prologueForestActive;
    private bool _prologueSkipRequested;
    private Vector3 _prologueStartFeet;
    private double _prologueElapsed;
    private CanvasLayer? _prologueOverlay;
    private ColorRect? _prologueBlackout;
    private Label? _prologueCaption;

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
        // The teaser is walked, not read: the arrival card's modal comes
        // after it (or not at all on a full skip).
        _player.SetModalOpen(false);
        BuildPrologueOverlay();

        // The walk itself: ordinary movement through the existing dark edge
        // until the player has covered the authored stretch, lingered long
        // enough, or asked to skip. There is nothing to collect and no gate.
        while (true)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _prologueElapsed += GetProcessDeltaTime();
            if (_prologueSkipRequested) break;
            var walked = _player.GlobalPosition.DistanceTo(_prologueStartFeet);
            // The authored stretch ends before the tree wall ahead; the
            // timeout covers a player who stops to look around instead.
            if (walked >= 5.5f || _prologueElapsed >= 12d) break;
        }

        var skipped = _prologueSkipRequested;
        if (!skipped)
        {
            await PlayPrologueCueAsync();
        }
        if (skipped)
        {
            _main.SwitchZone("village_day", "arrival");
        }
        else
        {
            // The blackout caption hands straight into the Niva ride (P3/P4);
            // it ends on its own fade, still inside this prologue block.
            await RunPrologueNivaRideAsync();
            FadePrologueBlackout(visible: false);
            await PrologueFrames(18);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ReleasePrologueOverlay();
        _prologueForestActive = false;
        return !skipped;
    }

    // The unseen movement and the cut: close wooden steps beside the player,
    // a beat, then a sharp sting straight to black with babai's waking line.
    // Samples are existing foley placeholders until real voice/audio arrives
    // (external gate); the caption is the audible line's text alternative.
    private async Task PlayPrologueCueAsync()
    {
        if (_player is null || _prologueOverlay is null) return;
        var behind = _player.GlobalPosition
            - (_player.GlobalBasis.Z with { Y = 0 }).Normalized() * 2.2f;
        UiFoley.PlayWorld(this, behind, "wood_tap");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_prologueSkipRequested) return;
        await PrologueFrames(22);
        if (_prologueSkipRequested) return;
        UiFoley.PlayWorld(this, behind, "metal_rattle");
        UiFoley.PlayWorld(this, _player.GlobalPosition, "hollow_board");
        FadePrologueBlackout(visible: true);
        await PrologueFrames(20);
        if (_prologueCaption is not null)
        {
            _prologueCaption.Text = "Мансур бабай: «Әй, уян. Приехали почти, внучек.»";
            _prologueCaption.Visible = true;
        }
        await PrologueFrames(75);
    }

    private async Task PrologueFrames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            if (_prologueSkipRequested) return;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void BuildPrologueOverlay()
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
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _prologueCaption.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _prologueCaption.OffsetTop = -120;
        _prologueCaption.OffsetBottom = -72;
        _prologueCaption.OffsetLeft = 220;
        _prologueCaption.OffsetRight = -220;
        _prologueCaption.AddThemeFontSizeOverride("font_size", 21);
        _prologueCaption.AddThemeColorOverride("font_color", new Color(0.92f, 0.9f, 0.82f));
        _prologueOverlay.AddChild(_prologueCaption);
    }

    private void FadePrologueBlackout(bool visible)
    {
        if (_prologueBlackout is null) return;
        var tween = CreateTween();
        tween.TweenProperty(_prologueBlackout, "color:a", visible ? 1f : 0f,
            _player?.Accessibility.ReducedMotion == true ? 0f : .3f);
    }

    private void ReleasePrologueOverlay()
    {
        _prologueOverlay?.QueueFree();
        _prologueOverlay = null;
        _prologueBlackout = null;
        _prologueCaption = null;
    }

    private async void ShowIntroAfterMenu()
    {
        if (!MainMenuVisible)
        {
            return;
        }

        _mainMenu?.Dismiss();
        _mainMenu = null;
        var showArrivalCard = await RunPrologueForestAsync();
        if (showArrivalCard)
        {
            BuildIntro();
        }
        else if (_player is { } player)
        {
            // The whole prologue block was skipped with one press: hand the
            // controls straight to the arrival instead of a card for a scene
            // the player chose not to read.
            player.SetModalOpen(false);
        }
    }
}
