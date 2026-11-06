using Godot;

namespace Urman.Godot;

/// <summary>
/// The hard cut that ends the prologue nightmare (author request 2026-11-06):
/// the forest is cut to black on the scare, a ringing silence holds while the
/// village is prepared, and Mansur's voice wakes Aidar in the Niva. The black
/// lives on the scene root so it survives the photo-world unmount and the
/// arrival loading; the Niva ride takes it over once its own blackout covers
/// the frame. It writes no story or save state and frees itself if nothing
/// takes it over.
/// </summary>
internal static class PrologueDreamCut
{
    private const string RingStream = "res://assets/audio/act1/foley/forest/knockout_ring.wav";
    private const double SafetyReleaseSeconds = 25.0;
    private static CanvasLayer? _layer;
    private static ulong _shownAtMsec;

    internal static bool IsActive(SceneTree? tree) =>
        tree is not null && _layer is { } layer && GodotObject.IsInstanceValid(layer) && layer.IsInsideTree();

    /// <summary>Covers the frame in black at once and starts the ringing silence.</summary>
    internal static void Show(SceneTree tree)
    {
        if (IsActive(tree)) return;
        _layer = new CanvasLayer { Name = "PrologueDreamCut", Layer = 120 };
        var black = new ColorRect
        {
            Name = "Black",
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1f,
            AnchorBottom = 1f
        };
        _layer.AddChild(black);
        tree.Root.AddChild(_layer);
        _shownAtMsec = Time.GetTicksMsec();

        if (DisplayServer.GetName() != "headless" && ResourceLoader.Exists(RingStream)
            && ResourceLoader.Load<AudioStream>(RingStream) is { } ring)
        {
            AudioSettingsService.EnsureBuses();
            var player = new AudioStreamPlayer
            {
                Name = "Ring",
                Stream = ring,
                Bus = AudioSettingsService.SfxBus,
                VolumeDb = -9f
            };
            _layer.AddChild(player);
            player.Play();
            // The ring thins out under the coming voice instead of stopping on a cut.
            var fade = player.CreateTween();
            fade.TweenInterval(1.2);
            fade.TweenProperty(player, "volume_db", -40f, 4.5);
        }

        var safety = tree.CreateTimer(SafetyReleaseSeconds);
        var shownAt = _shownAtMsec;
        safety.Timeout += () =>
        {
            if (shownAt == _shownAtMsec) Release(fadeSeconds: .6);
        };
    }

    /// <summary>
    /// Removes the black. The caller has already covered the frame with its
    /// own blackout, so the default is an instant hand-over with no flash.
    /// </summary>
    internal static void Release(double fadeSeconds = 0)
    {
        var layer = _layer;
        _layer = null;
        if (layer is null || !GodotObject.IsInstanceValid(layer)) return;
        if (fadeSeconds <= 0 || !layer.IsInsideTree() || layer.GetNodeOrNull<ColorRect>("Black") is not { } black)
        {
            layer.QueueFree();
            return;
        }
        var fade = layer.CreateTween();
        fade.TweenProperty(black, "color:a", 0f, fadeSeconds);
        fade.TweenCallback(Callable.From(layer.QueueFree));
    }
}
