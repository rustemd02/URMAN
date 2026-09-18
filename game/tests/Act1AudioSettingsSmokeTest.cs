using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// UIUX-011 focused smoke: the Act I bus set exists (Master/Ambience/Voice/
/// SFX), ambient and voice players are routed to their buses, volumes apply
/// live to the AudioServer and persist, muting the voice bus keeps captions
/// working, and the settings panel exposes the volume rows.
/// </summary>
public partial class Act1AudioSettingsSmokeTest : Node
{
    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Audio settings smoke could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        await Frames(3);

        AudioSettingsService.DeleteFile();
        AudioSettingsService.EnsureBuses();

        foreach (var busName in new[]
                 {
                     AudioSettingsService.MasterBus,
                     AudioSettingsService.AmbienceBus,
                     AudioSettingsService.VoiceBus,
                     AudioSettingsService.SfxBus
                 })
        {
            if (AudioServer.GetBusIndex(busName) == -1)
            {
                Fail($"Audio bus is missing: {busName}.");
                return;
            }
        }

        var ambientDirector = GetTree().GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector;
        var ambientBus = ambientDirector?.GetNode<AudioStreamPlayer>("AmbientPlayer1").Bus;
        var audioCue = GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi;
        var voiceBus = audioCue?.GetNode<AudioStreamPlayer>("AudioPlayer").Bus;
        if (ambientBus != AudioSettingsService.AmbienceBus || voiceBus != AudioSettingsService.VoiceBus)
        {
            Fail($"Player bus routing drifted: ambience={ambientBus} voice={voiceBus}.");
            return;
        }

        // Voice muted: bus mutes, captions still present the authored line.
        AudioSettingsService.SetVolume(AudioSettingsService.VoiceBus, 0f);
        if (!AudioServer.IsBusMute(AudioServer.GetBusIndex(AudioSettingsService.VoiceBus)))
        {
            Fail("Voice bus did not mute at zero volume.");
            return;
        }

        var audioCueCaptions = CompiledCampaignRepository.Load().ResolveAudio(
            "urman.chapter1:asset/audio-marat-voice",
            "runtime-test:caption-fallback");
        audioCue?.Present(audioCueCaptions);
        await Frames(2);
        if (string.IsNullOrEmpty(audioCue?.VisibleText))
        {
            Fail("Muted voice lost the synchronous caption fallback.");
            return;
        }

        var rinatCue = CompiledCampaignRepository.Load().ResolveAudio(
            "urman.chapter1:asset/audio-rinat-interruption", "runtime-test:queue");
        var voicePlayer = audioCue.GetNode<AudioStreamPlayer>("AudioPlayer");
        var playableCount = new[] { audioCueCaptions, rinatCue }.Count(item =>
            item.Asset.MediaType.StartsWith("audio/", StringComparison.Ordinal));
        foreach (var item in new[] { audioCueCaptions, rinatCue })
        {
            var declaredAudio = item.Asset.MediaType.StartsWith("audio/", StringComparison.Ordinal);
            if (declaredAudio && (!ResourceLoader.Exists(item.Asset.Url)
                    || ResourceLoader.Load<AudioStream>(item.Asset.Url) is not { } stream || stream.GetLength() <= 0d)
                || !declaredAudio && item.Asset.Url.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            { Fail("A finale WAV is missing, unreadable, or still declared as a logical reference: " + item.Asset.AssetId); return; }
        }
        var nativeVoice = DisplayServer.GetName() != "headless" && playableCount == 2;
        if (nativeVoice && !await WaitUntil(() => voicePlayer.Playing && voicePlayer.HasStreamPlayback()
                && voicePlayer.GetPlaybackPosition() > .02f, "The declared Marat recording never reached native playback.", 3000)) return;
        audioCue.Present(rinatCue);
        var firstText = audioCue.VisibleText;
        audioCue.SetPaused(true);
        await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
        var pausedCursor = voicePlayer.GetPlaybackPosition();
        await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
        if (audioCue.VisibleText != firstText || !audioCue.IsPresenting)
        { Fail("Pause advanced the active cue."); return; }
        if (nativeVoice && (!voicePlayer.StreamPaused
                || Mathf.Abs(voicePlayer.GetPlaybackPosition() - pausedCursor) > .015f))
        { Fail("Pause advanced the real voice playback cursor."); return; }
        audioCue.SetPaused(false);
        if (!await WaitUntil(() => audioCue.LastStartedAssetId == rinatCue.Asset.AssetId,
                "The second cue did not follow the first after resume.")) return;
        audioCue.ResetPresentation();
        await Frames(2);
        if (audioCue.IsPresenting || voicePlayer.Playing || voicePlayer.Stream is not null
            || audioCue.PresentedHistory.Count != 0 || audioCue.LastStartedAssetId is not null)
        { Fail("Reset retained a cue from the old session."); return; }
        audioCue.ApplyAccessibilitySettings(new(Subtitles: false, AudioDescriptions: false));
        var naturalFinishes = 0;
        void VoiceFinished() => naturalFinishes++;
        voicePlayer.Finished += VoiceFinished;
        try
        {
            audioCue.Present(audioCueCaptions);
            audioCue.Present(rinatCue);
            if (!await WaitUntil(() => audioCue.LastStartedAssetId == rinatCue.Asset.AssetId,
                    "Caption-disabled cues did not retain their ordered queue.")) return;
            if (!await WaitUntil(() => !audioCue.IsPresenting,
                    "The caption-disabled voice queue did not finish.")) return;
            if (audioCue.VisibleText is not null || nativeVoice && naturalFinishes != 2)
            { Fail($"Caption-disabled playback leaked text or stopped before both native Finished signals: finished={naturalFinishes}."); return; }
        }
        finally { voicePlayer.Finished -= VoiceFinished; }
        GD.Print(nativeVoice
            ? $"act1-voice-native: PASS queued recordings, paused cursor={pausedCursor:0.000}, abort/reset, captions-off native Finished={naturalFinishes}; listening remains external/not-run"
            : "act1-voice-native: external/not-run; current assets are logical references or the renderer is headless");
        audioCue.ResetPresentation();

        // Audio descriptions: with captions off but descriptions on, a cue that has no
        // recording must still tell the player what is happening. This is the whole
        // point of the setting, and until now only its disabled path was exercised.
        // This explicit presentation-only missing-recording case remains valid
        // after the real finale assets are installed. It never dispatches an
        // interaction or claims that synthetic test input is a performed voice.
        var unavailableCue = rinatCue with { Asset = rinatCue.Asset with
        {
            AssetId = "smoke:asset/unavailable-rinat-recording",
            MediaType = "application/vnd.urman.logical-asset-ref",
            Url = "res://assets/logical/audio-rinat-interruption.ref",
            Sha256 = null
        } };
        var descriptionText = unavailableCue.NonAudioCue?.Text;
        if (string.IsNullOrWhiteSpace(descriptionText))
        { Fail("The finale cue carries no audio description to fall back on."); return; }
        audioCue.ApplyAccessibilitySettings(new(Subtitles: false, AudioDescriptions: true));
        audioCue.Present(unavailableCue);
        // Observe real frames while the missing-recording text is still visible;
        // never advance the presentation clock by calling _Process manually.
        await Frames(2);
        if (audioCue.VisibleText != descriptionText || !audioCue.IsPresenting)
        {
            Fail($"Audio descriptions did not present the authored description: visible='{audioCue.VisibleText ?? "<null>"}' presenting={audioCue.IsPresenting} expected='{descriptionText}'");
            return;
        }
        audioCue.ResetPresentation();

        // And with descriptions off as well, the cue must stay silent and empty rather
        // than leak a description nobody asked for.
        audioCue.ApplyAccessibilitySettings(new(Subtitles: false, AudioDescriptions: false));
        audioCue.Present(unavailableCue);
        await Frames(2);
        if (audioCue.VisibleText is not null)
        { Fail("A cue leaked text with both accessibility channels off."); return; }
        audioCue.ResetPresentation();

        // Live volume application + persistence round trip.
        AudioSettingsService.SetVolume(AudioSettingsService.MasterBus, 0.5f);
        var masterDb = AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(AudioSettingsService.MasterBus));
        if (Mathf.Abs(masterDb - Mathf.LinearToDb(0.5f)) > 0.01f)
        {
            Fail($"Master volume did not apply ({masterDb} dB).");
            return;
        }

        AudioSettingsService.SetVolume(AudioSettingsService.AmbienceBus, 0.75f);
        ambientDirector!.SetVoiceDuck(true);
        _ = AudioSettingsService.GetVolume(AudioSettingsService.AmbienceBus);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        var ambienceIndex = AudioServer.GetBusIndex(AudioSettingsService.AmbienceBus);
        var duck = Enumerable.Range(0, AudioServer.GetBusEffectCount(ambienceIndex))
            .Select(index => AudioServer.GetBusEffect(ambienceIndex, index)).OfType<AudioEffectAmplify>().Single();
        if (duck.VolumeDb > -5.9f || Math.Abs(AudioServer.GetBusVolumeDb(ambienceIndex) - Mathf.LinearToDb(.75f)) > .01f)
        { Fail("Transient duck lost its attenuation or changed the user bus volume."); return; }
        ambientDirector.SetVoiceDuck(false);
        var persisted = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(
            System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath("user://audio-settings.json")));
        var persistedVolumes = persisted?["Volumes"]?.AsObject();
        if (persistedVolumes?["Master"]?.GetValue<float>() is not 0.5f
            || persistedVolumes["Ambience"]?.GetValue<float>() is not 0.75f)
        {
            Fail("Audio volumes did not persist to the versioned store.");
            return;
        }

        // Settings panel exposes the four volume rows and applies live.
        var settings = main.GetNodeOrNull<SettingsUi>("SettingsUi");
        var player = main.GetNodeOrNull<FirstPersonController>("Player");
        if (settings is null || player is null)
        {
            Fail("Audio settings smoke could not find settings UI or player.");
            return;
        }

        settings.Open(player);
        var voiceSlider = settings.GetNode<HSlider>("Screen/Panel/Layout/BodyScroll/Body/VoiceVolumeRow/VoiceVolume");
        voiceSlider.EmitSignal(HSlider.SignalName.ValueChanged, 1.0);
        await Frames(2);
        if (AudioServer.IsBusMute(AudioServer.GetBusIndex(AudioSettingsService.VoiceBus))
            || Math.Abs(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(AudioSettingsService.VoiceBus))
                - Mathf.LinearToDb(1f)) > 0.01f)
        {
            Fail("Settings volume row did not apply to the voice bus.");
            return;
        }

        settings.Close();
        GD.Print("act1-audio-settings: PASS buses + routing + live volumes + persistence + muted-voice captions + audio-description fallback + settings rows");
        AudioSettingsService.DeleteFile();
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private async Task<bool> WaitUntil(Func<bool> condition, string failure, ulong timeoutMs = 20000)
    {
        var deadline = Time.GetTicksMsec() + timeoutMs;
        while (Time.GetTicksMsec() < deadline)
        {
            if (condition()) return true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Fail(failure);
        return false;
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
