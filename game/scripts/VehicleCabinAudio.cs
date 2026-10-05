using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>
/// The Niva cabin mix. While the player sits inside, the exterior buses are
/// gently muffled on the existing Ambience and Loudspeaker chains (a low-pass
/// plus a high-shelf cut; no bus volume or mute is ever written, so the user's
/// saved settings stay exact), the car radio keeps its clear dash sound by
/// riding a dedicated Cabin bus, and a quiet synthesised heater bed joins. On
/// exit the effects are removed by identity, the radio returns to the Ambience
/// bus and the bed fades out. The bed is project-original synthesis (the same
/// approach as VehicleMechanicalAudio), so there are no new recordings to
/// license; one player, about 172 KiB of PCM, no per-frame allocations.
/// </summary>
public partial class VehicleCabinAudio : Node3D
{
    /// <summary>Interior sources (radio, heater bed) ride this bus while seated.
    /// It mirrors the Ambience volume so the existing slider still works.</summary>
    public const string CabinBus = "Cabin";

    // --- mix numbers ----------------------------------------------------------
    // Through the glass the high band dies first: a 12 dB/octave low-pass at
    // 2.1 kHz is about -3 dB at 2.1 kHz and -19 dB at 8 kHz, and the high-shelf
    // cut (950 Hz, x0.45 = -6.9 dB) takes another ~7 dB off the hiss that makes
    // the weather bed read as "outside". The PA cones are further away and
    // cheaper: 1.3 kHz low-pass and -6 dB overall.
    private const float AmbienceLowPassHz = 2100f;
    private const float AmbienceLowPassResonance = .18f;
    private const float AmbienceShelfHz = 950f;
    private const float AmbienceShelfGain = .45f; // linear gain, ~ -6.9 dB
    private const float ShelfResonance = .5f;
    private const AudioEffectFilter.FilterDB FilterOrder = AudioEffectFilter.FilterDB.Filter12Db;
    private const float PaLowPassHz = 1300f;
    private const float PaLowPassResonance = .12f;
    private const float PaAmplifyDb = -6f;
    // --- heater bed -----------------------------------------------------------
    private const int BedRate = 22050;
    private const int BedSeconds = 4;
    private const float BedRunningDb = -24f; // engine running: vent fan and block rumble
    private const float BedIdleDb = -30f;    // seated, ignition off: quiet cabin tone
    private const float BedSilentDb = -60f;
    private const float BedFadeDbPerSecond = 90f; // ~0.4 s from silence to -24 dB

    private readonly List<(int BusIndex, AudioEffect Effect)> _applied = new(4);
    private readonly List<AudioStreamPlayer3D> _movedRadioSpeakers = new(2);

    private VehicleController? _controller;
    private AudioStreamPlayer? _bedPlayer;
    private AudioStreamWav? _bedStream;
    private bool _inside;
    private bool _cabinMute;
    private float _cabinDb = float.PositiveInfinity;

    public override void _Ready()
    {
        // The mix must complete even when the tree is paused for a menu.
        ProcessMode = ProcessModeEnum.Always;
        SetMeta("presentationOwnership", "presentation-only; no collision, no save state");
        SetMeta("cabinMixOwner", "VehicleCabinAudio.cs; effects added on entry, removed on exit");
        SetMeta("ambienceLowPassHz", AmbienceLowPassHz);
        SetMeta("ambienceShelf", $"hz={AmbienceShelfHz};gain={AmbienceShelfGain}");
        SetMeta("paMix", $"lowPassHz={PaLowPassHz};amplifyDb={PaAmplifyDb}");
        _controller = GetParent() is { } visual ? visual.GetParent() as VehicleController : null;
    }

    public override void _Process(double delta)
    {
        if (_controller is not null && !GodotObject.IsInstanceValid(_controller))
        {
            _controller = null;
        }

        var dt = Mathf.Min((float)delta, .05f);
        var inside = _controller is not null && _controller.Driver is not null;
        if (inside != _inside)
        {
            _inside = inside;
            if (inside) ApplyCabinMix();
            else ReleaseCabinMix();
        }

        if (_inside)
        {
            MirrorCabinVolume();
            EnsureBedPlayer();
            if (_bedPlayer is not null && !_bedPlayer.Playing)
            {
                _bedPlayer.VolumeDb = BedSilentDb;
                _bedPlayer.Play();
            }
        }

        FadeBed(dt);
    }

    public override void _ExitTree()
    {
        RemoveAppliedEffects();
        RestoreRadioBus();
        if (_bedPlayer is not null && GodotObject.IsInstanceValid(_bedPlayer))
        {
            _bedPlayer.Stop();
            _bedPlayer.Stream = null;
        }

        _bedPlayer = null;
        _bedStream = null;
    }

    private void ApplyCabinMix()
    {
        AudioSettingsService.EnsureBuses();
        EnsureCabinBus();
        RemoveAppliedEffects();
        var ambience = AudioServer.GetBusIndex(AudioSettingsService.AmbienceBus);
        if (ambience != -1)
        {
            // No AudioEffectAmplify on Ambience: AmbientAudioDirector's voice
            // duck is the one Amplify owner on that bus, and its index is cached.
            AppendEffect(ambience, new AudioEffectLowPassFilter
            {
                CutoffHz = AmbienceLowPassHz, Resonance = AmbienceLowPassResonance, Db = FilterOrder
            });
            AppendEffect(ambience, new AudioEffectHighShelfFilter
            {
                CutoffHz = AmbienceShelfHz, Gain = AmbienceShelfGain, Resonance = ShelfResonance
            });
        }

        var loudspeaker = AudioServer.GetBusIndex(AudioSettingsService.LoudspeakerBus);
        if (loudspeaker != -1)
        {
            AppendEffect(loudspeaker, new AudioEffectLowPassFilter
            {
                CutoffHz = PaLowPassHz, Resonance = PaLowPassResonance, Db = FilterOrder
            });
            AppendEffect(loudspeaker, new AudioEffectAmplify { VolumeDb = PaAmplifyDb });
        }

        MoveRadioToCabin();
        _cabinDb = float.PositiveInfinity;
        MirrorCabinVolume();
        SetMeta("cabinMix", "inside");
        SetMeta("appliedCabinEffects", _applied.Count);
    }

    private void ReleaseCabinMix()
    {
        RemoveAppliedEffects();
        RestoreRadioBus();
        SetMeta("cabinMix", "outside");
        SetMeta("appliedCabinEffects", _applied.Count);
    }

    private void AppendEffect(int busIndex, AudioEffect effect)
    {
        AudioServer.AddBusEffect(busIndex, effect);
        _applied.Add((busIndex, effect));
    }

    /// <summary>Removes exactly the effects this node appended, newest first so
    /// earlier owners keep their cached indices.</summary>
    private void RemoveAppliedEffects()
    {
        for (var applied = _applied.Count - 1; applied >= 0; applied--)
        {
            var (busIndex, effect) = _applied[applied];
            for (var index = AudioServer.GetBusEffectCount(busIndex) - 1; index >= 0; index--)
            {
                if (AudioServer.GetBusEffect(busIndex, index) == effect)
                {
                    AudioServer.RemoveBusEffect(busIndex, index);
                    break;
                }
            }
        }

        _applied.Clear();
    }

    private void EnsureCabinBus()
    {
        if (AudioServer.GetBusIndex(CabinBus) != -1)
        {
            return;
        }

        var index = AudioServer.BusCount;
        AudioServer.AddBus(index);
        AudioServer.SetBusName(index, CabinBus);
        AudioServer.SetBusSend(index, AudioSettingsService.MasterBus);
    }

    /// <summary>Keeps the interior bus on the user's Ambience volume, mute included.</summary>
    private void MirrorCabinVolume()
    {
        var cabin = AudioServer.GetBusIndex(CabinBus);
        var ambience = AudioServer.GetBusIndex(AudioSettingsService.AmbienceBus);
        if (cabin == -1 || ambience == -1)
        {
            return;
        }

        var db = AudioServer.GetBusVolumeDb(ambience);
        if (Mathf.Abs(db - _cabinDb) > .05f)
        {
            _cabinDb = db;
            AudioServer.SetBusVolumeDb(cabin, db);
        }

        var mute = AudioServer.IsBusMute(ambience);
        if (mute != _cabinMute)
        {
            _cabinMute = mute;
            AudioServer.SetBusMute(cabin, mute);
        }
    }

    /// <summary>
    /// The radio speakers are inside the cabin, so they must not pass through
    /// the muffling chain: only this vehicle's own radio players are moved, and
    /// only those still on the Ambience bus (a fixture or a rebus by the radio
    /// lane is left alone).
    /// </summary>
    private void MoveRadioToCabin()
    {
        _movedRadioSpeakers.Clear();
        if (_controller?.Radio is not { } radio)
        {
            return;
        }

        for (var index = 0; index < radio.GetChildCount(); index++)
        {
            if (radio.GetChild(index) is not AudioStreamPlayer3D speaker) continue;
            if (!speaker.Name.ToString().StartsWith("RadioSpeaker", StringComparison.Ordinal)) continue;
            if (speaker.Bus != AudioSettingsService.AmbienceBus) continue;
            speaker.Bus = CabinBus;
            _movedRadioSpeakers.Add(speaker);
        }
    }

    private void RestoreRadioBus()
    {
        foreach (var speaker in _movedRadioSpeakers)
        {
            if (GodotObject.IsInstanceValid(speaker) && speaker.Bus == CabinBus)
            {
                speaker.Bus = AudioSettingsService.AmbienceBus;
            }
        }

        _movedRadioSpeakers.Clear();
    }

    private void FadeBed(float dt)
    {
        if (_bedPlayer is null || !GodotObject.IsInstanceValid(_bedPlayer) || !_bedPlayer.Playing)
        {
            return;
        }

        var target = !_inside ? BedSilentDb : (_controller?.EngineRunning == true ? BedRunningDb : BedIdleDb);
        if (Mathf.Abs(_bedPlayer.VolumeDb - target) > .05f)
        {
            _bedPlayer.VolumeDb = Mathf.MoveToward(_bedPlayer.VolumeDb, target, dt * BedFadeDbPerSecond);
        }
        else if (_bedPlayer.VolumeDb != target)
        {
            _bedPlayer.VolumeDb = target;
        }

        if (!_inside && _bedPlayer.VolumeDb <= BedSilentDb + .05f)
        {
            _bedPlayer.Stop();
        }
    }

    private void EnsureBedPlayer()
    {
        if (_bedPlayer is not null || DisplayServer.GetName() == "headless")
        {
            return;
        }

        _bedStream = BuildHeaterBed();
        _bedPlayer = new AudioStreamPlayer
        {
            Name = "CabinHeaterBed",
            Bus = CabinBus,
            Stream = _bedStream,
            VolumeDb = BedSilentDb,
            Autoplay = false
        };
        AddChild(_bedPlayer);
    }

    // ------------------------------------------------------------------------
    // Heater bed: project-original synthesis. A fan hiss (one-pole low-passed
    // white noise) over a body rumble (a second, darker low-pass) with a faint
    // 118 Hz / 59.5 Hz motor hum; both filters run several laps around the
    // ring buffer, so the loop is exactly periodic at the seam and there is
    // nothing to crossfade. 4 s of 22.05 kHz mono PCM16 is 172 KiB.
    // ------------------------------------------------------------------------
    private static AudioStreamWav BuildHeaterBed()
    {
        const int frames = BedRate * BedSeconds;
        const int laps = 4;
        var fanCoefficient = 1f - Mathf.Exp(-Mathf.Tau * 1500f / BedRate);
        var bodyCoefficient = 1f - Mathf.Exp(-Mathf.Tau * 260f / BedRate);
        var peak = MeasureBedPeak(frames, laps, fanCoefficient, bodyCoefficient);
        var gain = peak > .0001f ? .58f / peak : 0f;
        var data = new byte[frames * 2];
        WriteBedSamples(frames, laps, fanCoefficient, bodyCoefficient, gain, data);
        return new AudioStreamWav
        {
            Data = data,
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = BedRate,
            Stereo = false,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward,
            LoopBegin = 0,
            LoopEnd = frames
        };
    }

    private static float MeasureBedPeak(int frames, int laps, float fanCoefficient, float bodyCoefficient)
    {
        var random = new System.Random(0x5A1A);
        var fan = 0f;
        var body = 0f;
        var peak = 0f;
        for (var lap = 0; lap < laps; lap++)
        {
            for (var index = 0; index < frames; index++)
            {
                var white = (float)(random.NextDouble() * 2.0 - 1.0);
                fan += fanCoefficient * (white - fan);
                body += bodyCoefficient * (white - body);
                if (lap != laps - 1) continue;
                var magnitude = Mathf.Abs(BedSample(fan, body, index));
                if (magnitude > peak) peak = magnitude;
            }
        }

        return peak;
    }

    private static void WriteBedSamples(int frames, int laps, float fanCoefficient, float bodyCoefficient,
        float gain, byte[] data)
    {
        var random = new System.Random(0x5A1A);
        var fan = 0f;
        var body = 0f;
        for (var lap = 0; lap < laps; lap++)
        {
            for (var index = 0; index < frames; index++)
            {
                var white = (float)(random.NextDouble() * 2.0 - 1.0);
                fan += fanCoefficient * (white - fan);
                body += bodyCoefficient * (white - body);
                if (lap != laps - 1) continue;
                var value = Mathf.Clamp(BedSample(fan, body, index) * gain, -.98f, .98f);
                var sample = (short)Math.Round(value * 32000f);
                data[index * 2] = (byte)(sample & 0xff);
                data[index * 2 + 1] = (byte)((sample >> 8) & 0xff);
            }
        }
    }

    private static float BedSample(float fan, float body, int index)
    {
        var seconds = index / (float)BedRate;
        var hum = .16f * Mathf.Sin(Mathf.Tau * 118f * seconds)
            + .07f * Mathf.Sin(Mathf.Tau * 59.5f * seconds + .8f);
        return fan * .55f + body * 1.15f + hum * .10f;
    }
}
