using System;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Covers the handover from the forest dream to waking in the Niva while the
/// village is prepared. The black
/// lives on the scene root so it survives the photo-world unmount and the
/// arrival loading; the Niva ride takes it over once its own blackout covers
/// the frame. It writes no story or save state and frees itself if nothing
/// takes it over.
/// </summary>
internal static class PrologueDreamCut
{
    private const double SafetyReleaseSeconds = 25.0;
    private static CanvasLayer? _layer;
    private static ulong _shownAtMsec;

    internal static bool IsActive(SceneTree? tree) =>
        tree is not null && _layer is { } layer && GodotObject.IsInstanceValid(layer) && layer.IsInsideTree();

    /// <summary>Covers the frame before the world is replaced.</summary>
    internal static void Show(SceneTree tree, double fadeSeconds)
    {
        if (IsActive(tree)) return;
        _layer = new CanvasLayer { Name = "PrologueDreamCut", Layer = 120 };
        var black = new ColorRect
        {
            Name = "Black",
            Color = Colors.Black with { A = 0f },
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1f,
            AnchorBottom = 1f
        };
        _layer.AddChild(black);
        tree.Root.AddChild(_layer);
        _shownAtMsec = Time.GetTicksMsec();
        black.CreateTween().TweenProperty(black, "color:a", 1f, fadeSeconds);

        var safety = tree.CreateTimer(SafetyReleaseSeconds);
        var shownAt = _shownAtMsec;
        safety.Timeout += () =>
        {
            if (shownAt == _shownAtMsec) Release(fadeSeconds: .6);
        };
    }

    private const string VoiceDir = "res://assets/audio/act1/voice_preview/";
    private const string FriendBus = "PrologueDreamFriend";
    private const string BabaiBus = "PrologueDreamBabai";
    private static ulong _morphEndsAtMsec;

    /// <summary>Seconds left until the friend-to-babai voice blend has finished.</summary>
    internal static double MorphRemainingSeconds =>
        Math.Max(0, ((long)_morphEndsAtMsec - (long)Time.GetTicksMsec()) / 1000.0);

    /// <summary>
    /// In the black after the unanswered call, Marat's «Айдар…» sinks under a closing
    /// low-pass and a long echo while Mansur's «Айдар… Айдар…» comes up from
    /// muffled to clear: the same name, one voice turning into the other, the
    /// way a dream hands over to whoever is shaking you awake. The players live
    /// on the scene root so they outlast the black and the arrival loading.
    /// </summary>
    internal static void PlayVoiceMorph(SceneTree tree, string friendClip = "forest-name-call", string babaiClip = "forest-babai-name-v2")
    {
        if (DisplayServer.GetName() == "headless") return;
        if (ResourceLoader.Load<AudioStream>(VoiceDir + friendClip + ".wav") is not { } friend
            || ResourceLoader.Load<AudioStream>(VoiceDir + babaiClip + ".wav") is not { } babai) return;
        AudioSettingsService.EnsureBuses();
        var friendBus = EnsureDreamBus(FriendBus, withEcho: true);
        var babaiBus = EnsureDreamBus(BabaiBus, withEcho: false);
        var friendFilter = (AudioEffectLowPassFilter)AudioServer.GetBusEffect(friendBus, 0);
        var babaiFilter = (AudioEffectLowPassFilter)AudioServer.GetBusEffect(babaiBus, 0);
        friendFilter.CutoffHz = 9000f;
        babaiFilter.CutoffHz = 380f;

        var friendPlayer = new AudioStreamPlayer { Name = "DreamFriendVoice", Stream = friend, Bus = FriendBus, VolumeDb = -3f };
        var babaiPlayer = new AudioStreamPlayer { Name = "DreamBabaiVoice", Stream = babai, Bus = BabaiBus, VolumeDb = -34f };
        tree.Root.AddChild(friendPlayer);
        tree.Root.AddChild(babaiPlayer);
        friendPlayer.Finished += friendPlayer.QueueFree;
        babaiPlayer.Finished += babaiPlayer.QueueFree;

        const double friendAt = .25, babaiAt = 1.0, blend = 2.2;
        _morphEndsAtMsec = Time.GetTicksMsec() + (ulong)((babaiAt + babai.GetLength()) * 1000);
        var fade = friendPlayer.CreateTween().SetParallel();
        fade.TweenCallback(Callable.From(() => friendPlayer.Play())).SetDelay(friendAt);
        fade.TweenProperty(friendFilter, "cutoff_hz", 420f, blend).SetDelay(friendAt + .3).SetTrans(Tween.TransitionType.Sine);
        fade.TweenProperty(friendPlayer, "volume_db", -36f, blend).SetDelay(friendAt + .4).SetTrans(Tween.TransitionType.Sine);
        var rise = babaiPlayer.CreateTween().SetParallel();
        rise.TweenCallback(Callable.From(() => babaiPlayer.Play())).SetDelay(babaiAt);
        rise.TweenProperty(babaiPlayer, "volume_db", -2f, 1.6).SetDelay(babaiAt).SetTrans(Tween.TransitionType.Sine);
        rise.TweenProperty(babaiFilter, "cutoff_hz", 11000f, 2.2).SetDelay(babaiAt + .2).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
    }

    private static int EnsureDreamBus(string name, bool withEcho)
    {
        var index = AudioServer.GetBusIndex(name);
        if (index >= 0) return index;
        AudioServer.AddBus();
        index = AudioServer.BusCount - 1;
        AudioServer.SetBusName(index, name);
        AudioServer.SetBusSend(index, AudioSettingsService.VoiceBus);
        AudioServer.AddBusEffect(index, new AudioEffectLowPassFilter { CutoffHz = 9000f, Resonance = .6f }, 0);
        if (withEcho)
            AudioServer.AddBusEffect(index, new AudioEffectReverb { RoomSize = .85f, Damping = .35f, Wet = .42f, Dry = .8f, Spread = 1f });
        return index;
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
