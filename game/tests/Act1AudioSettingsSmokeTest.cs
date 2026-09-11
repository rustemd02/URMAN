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
        audioCue.Present(rinatCue);
        var firstText = audioCue.VisibleText;
        audioCue.SetPaused(true);
        audioCue._Process(30);
        if (audioCue.VisibleText != firstText || !audioCue.IsPresenting)
        { Fail("Pause advanced the active cue."); return; }
        audioCue.SetPaused(false);
        audioCue._Process(30);
        audioCue._Process(1);
        if (audioCue.LastStartedAssetId != rinatCue.Asset.AssetId)
        { Fail("The second cue did not follow the first after resume."); return; }
        audioCue.ResetPresentation();
        if (audioCue.IsPresenting || audioCue.PresentedHistory.Count != 0 || audioCue.LastStartedAssetId is not null)
        { Fail("Reset retained a cue from the old session."); return; }
        audioCue.ApplyAccessibilitySettings(new(Subtitles: false, AudioDescriptions: false));
        audioCue.Present(audioCueCaptions);
        audioCue.Present(rinatCue);
        audioCue._Process(1);
        audioCue._Process(1);
        if (audioCue.LastStartedAssetId != rinatCue.Asset.AssetId || audioCue.VisibleText is not null)
        { Fail("Caption-disabled cues bypassed the ordered queue."); return; }
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
        GD.Print("act1-audio-settings: PASS buses + routing + live volumes + persistence + muted-voice captions + settings rows");
        AudioSettingsService.DeleteFile();
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
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
