using Godot;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private void CheckSavedRadioPosition(VehicleRadioPlayer radio,JsonElement saved,JsonObject? beforeSave)
    {
        // Read the actual playable order; never compare unrelated per-file
        // offsets when normal playback has crossed an EOF during save/load.
        var field=typeof(VehicleRadioPlayer).GetField("_timeline",BindingFlags.Instance|BindingFlags.NonPublic)
            ??throw new MissingFieldException(nameof(VehicleRadioPlayer),"_timeline");
        var timeline=field.GetValue(radio) as VehicleRadioTimeline
            ??throw new InvalidOperationException("The restored radio has no active programme.");
        var after=radio.Capture();
        var savedId=saved.GetProperty("segmentId").GetString();
        var currentId=after["segmentId"]!.GetValue<string>();
        var savedOffset=saved.GetProperty("offsetSeconds").GetDouble();
        var currentOffset=after["offsetSeconds"]!.GetValue<double>();
        double Phase(string? id,double offset)
        {
            double elapsed=0;
            foreach(var segment in timeline.Segments)
            {
                if(segment.Id==id)
                {
                    if(!double.IsFinite(offset)||offset<0||offset>=segment.DurationSeconds)
                        throw new InvalidOperationException("Saved/current radio offset is outside its actual segment.");
                    return elapsed+offset;
                }
                elapsed+=segment.DurationSeconds;
            }
            throw new InvalidOperationException("Saved/current radio segment is absent from the actual playable programme: "+id);
        }
        var savedPhase=Phase(savedId,savedOffset);var currentPhase=Phase(currentId,currentOffset);
        var duration=timeline.Segments.Sum(segment=>segment.DurationSeconds);
        var phaseDelta=Math.IEEERemainder(currentPhase-savedPhase,duration);
        _records.Add(new{kind="occupied-radio-save-load",beforeSave,saved=saved.Clone(),after,
            savedPhase,currentPhase,phaseDelta,programmeDuration=duration,
            crossedSegmentBoundary=savedId!=currentId,
            limit="actual committed segment and offset; unchanged one-second phase tolerance; no listening claim"});
        Require(radio.Enabled&&saved.GetProperty("enabled").GetBoolean()
            &&Math.Abs(after["volume"]!.GetValue<float>()-saved.GetProperty("volume").GetSingle())<.000001f
            &&Math.Abs(phaseDelta)<1.0,"radio resumes at the saved segment position");
    }

    private async Task DeliveredRadioChecks(VehicleRadioPlayer radio)
    {
        var preview=new VehicleRadioSegment("eligibility-fixture","ident","tt","Fixture","Fixture","",1,
            "licensed-synthetic-preview","fixture-provenance","synthetic-preview",
            "preview-pending-listening-and-language-review","MIT");
        Require(preview.CanPlay&&preview.IsSyntheticPreview,"licensed speech preview has an explicit playback contract distinct from recorded approval");
        Require(!(preview with{Origin="human-recording"}).CanPlay
            &&!(preview with{Acceptance="approved"}).CanPlay
            &&!(preview with{ModelLicense="unknown"}).CanPlay
            &&!(preview with{Provenance=""}).CanPlay
            &&!(preview with{Kind="music"}).CanPlay,
            "preview playback rejects wrong origin, acceptance, licence, absent provenance and synthetic music");
        using var file=global::Godot.FileAccess.Open(VehicleRadioPlayer.StationPath,global::Godot.FileAccess.ModeFlags.Read)
            ??throw new InvalidDataException("The actual station programme is absent.");
        using var document=JsonDocument.Parse(file.GetAsText());
        var rows=document.RootElement.GetProperty("segments").EnumerateArray().ToArray();
        var approved=rows.Where(row=>VehicleRadioSegment.FromJson(row).CanPlay).ToArray();
        var recorded=approved.Count(row=>row.GetProperty("rightsStatus").GetString()=="approved");
        var previews=approved.Count(row=>VehicleRadioSegment.FromJson(row).IsSyntheticPreview);
        var repeats=approved.Count(row=>VehicleRadioSegment.FromJson(row).IsRecordingRepeat);
        var missing=rows.Where(row=>!VehicleRadioSegment.FromJson(row).CanPlay)
            .Select(row=>row.GetProperty("id").GetString()).ToArray();
        Require(radio.PlayableSegmentCount==approved.Length&&radio.MissingRecordingCount==missing.Length
            &&radio.ProgrammedSegmentCount==rows.Length&&radio.RecordedSegmentCount==recorded
            &&radio.SyntheticPreviewCount==previews&&radio.RepeatedSegmentCount==repeats,
            "recorded segments, declared repeats, synthetic previews and missing station audio remain separately accounted");
        var programme=rows.Select(VehicleRadioSegment.FromJson).ToArray();
        foreach(var repeated in programme.Where(segment=>segment.IsRecordingRepeat))
        {
            Require(repeated.HasValidRepeatSource(programme),"declared archive repeat uses the same approved recording bytes and provenance");
            Require(!(repeated with{Sha256=new string('0',64)}).HasValidRepeatSource(programme)
                &&!(repeated with{RepeatsSegmentId=repeated.Id}).HasValidRepeatSource(programme),
                "recording repeats reject byte substitution and cyclic source IDs");
        }
        foreach(var row in approved)
        {
            var resource=row.GetProperty("streamPath").GetString()!;
            using var input=File.OpenRead(ProjectSettings.GlobalizePath(resource));
            Require(Convert.ToHexString(SHA256.HashData(input)).Equals(row.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),
                "delivered station bytes match their provenance digest: "+row.GetProperty("id").GetString());
            var stream=ResourceLoader.Load<AudioStream>(resource);
            Require(stream is not null&&Math.Abs(stream.GetLength()-row.GetProperty("durationSeconds").GetDouble())<.025,
                "delivered station recording decodes with its actual timeline length");
        }
        _records.Add(new{kind="actual-station-delivery",programmed=rows.Length,playable=approved.Length,recorded,repeats,syntheticPreviews=previews,missing,
            interpretation="actual bytes/native playback only; previews are not original recordings; listening and cultural acceptance remain separate"});
        if(approved.Length==0)return;
        var speakers=radio.GetChildren().OfType<AudioStreamPlayer3D>().ToArray();
        Require(approved.Any(row=>row.GetProperty("id").GetString()==radio.CurrentSegmentId),
            "turning on the radio selects an actual recording without waiting through pending scripts");
        await WaitRadioUntil(()=>speakers.Any(speaker=>speaker.Playing&&speaker.HasStreamPlayback()),
            "the actual delivered programme starts native audio immediately");
        var original=JsonSerializer.SerializeToElement(radio.Capture());
        var rowToTest=approved[0];var id=rowToTest.GetProperty("id").GetString()!;
        var duration=rowToTest.GetProperty("durationSeconds").GetDouble();
        try
        {
            RestoreRadioFixture(radio,id,Math.Min(15,duration*.25),.43f);
            await RadioSeconds(.35);
            var playing=speakers.Single(speaker=>speaker.Playing);
            Require(Math.Abs(playing.GetPlaybackPosition()-radio.SegmentOffset)<.20,
                "actual delivered audio seeks and follows its saved segment position");
            RestoreRadioFixture(radio,id,duration-.65,.43f);
            await WaitRadioUntil(()=>radio.SegmentOffset<1&&speakers.Any(speaker=>speaker.Playing),
                "the delivered recording crosses its real end into the available programme");
            _records.Add(new{kind="actual-station-native-playback",id,duration,
                source=rowToTest.GetProperty("provenance").GetString(),
                origin=VehicleRadioSegment.FromJson(rowToTest).Origin,seek=true,fileBoundary=true,listening="not-run"});
        }
        finally{radio.Restore(original);}
    }

    /// <summary>
    /// Real CC0 ambience recordings exercise only the playback plumbing. They are
    /// not station music, performed speech or either missing finale voice. The
    /// temporary player is outside VehicleFleet capture and never replaces its radio.
    /// </summary>
    private async Task NativeRadioChecks(VehicleController vehicle)
    {
        if(DisplayServer.GetName()=="headless")
            throw new InvalidOperationException("The vehicle radio proof requires native audio playback.");
        const string firstPath="res://assets/audio/village_arrival.wav";
        const string secondPath="res://assets/audio/village_yard.wav";
        const string provenance="game/assets/audio/ambient_manifest.json; CC0 wind recording by lwdickens, Freesound 261226";
        VerifyRadioFixtureHash(firstPath,"5b39aba16584cafc2b0e05de75c37f47950587d3f3ecfe9473794edcb898989a");
        VerifyRadioFixtureHash(secondPath,"7b73602eef16b131c876b62fcd8c38be78f09e5fdceb8355e5f6b18523aee0d2");
        var sharedFirst=ResourceLoader.Load<AudioStreamWav>(firstPath)
            ??throw new InvalidOperationException("First CC0 fixture did not decode.");
        var sharedSecond=ResourceLoader.Load<AudioStreamWav>(secondPath)
            ??throw new InvalidOperationException("Second CC0 fixture did not decode.");
        var firstLoop=sharedFirst.LoopMode;var secondLoop=sharedSecond.LoopMode;
        using var first=(AudioStreamWav)sharedFirst.Duplicate();
        using var second=(AudioStreamWav)sharedSecond.Duplicate();
        first.LoopMode=AudioStreamWav.LoopModeEnum.Disabled;
        second.LoopMode=AudioStreamWav.LoopModeEnum.Disabled;
        Require(first.GetLength()>8&&second.GetLength()>8,"CC0 playback fixtures contain real decoded samples");
        var fixture=new VehicleRadioPlayer{Name="RadioPlaybackFixture"};vehicle.AddChild(fixture);
        fixture.Position=Vector3.Up;
        var cue=GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi
            ??throw new InvalidOperationException("Actual AudioCueUi is missing.");
        var ambience=GetTree().GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector
            ??throw new InvalidOperationException("Actual ambience owner is missing.");
        Require(!cue.IsPresenting,"radio fixture starts outside an authored voice cue");
        var firstId="smoke:cc0-radio-wind-a";var secondId="smoke:cc0-radio-wind-b";
        try
        {
            // Test-only injection avoids a second production station loader or
            // a mutable fixture path exposed to ordinary runtime and save files.
            var timeline=new VehicleRadioTimeline(new[]{
                new VehicleRadioSegment(firstId,"test-fixture","none","CC0 wind fixture A","",firstPath,
                    first.GetLength(),"approved",provenance),
                new VehicleRadioSegment(secondId,"test-fixture","none","CC0 wind fixture B","",secondPath,
                    second.GetLength(),"approved",provenance)});
            var fields=BindingFlags.Instance|BindingFlags.NonPublic;
            var timelineField=typeof(VehicleRadioPlayer).GetField("_timeline",fields)
                ??throw new MissingFieldException(nameof(VehicleRadioPlayer),"_timeline");
            var streamsField=typeof(VehicleRadioPlayer).GetField("_streams",fields)
                ??throw new MissingFieldException(nameof(VehicleRadioPlayer),"_streams");
            timelineField.SetValue(fixture,timeline);
            var streams=streamsField.GetValue(fixture) as Dictionary<string,AudioStream?>
                ??throw new InvalidOperationException("Radio stream fixture hook changed.");
            streams[firstId]=first;streams[secondId]=second;
            var speakers=new[]{fixture.GetNode<AudioStreamPlayer3D>("RadioSpeaker0"),
                fixture.GetNode<AudioStreamPlayer3D>("RadioSpeaker1")};
            Require(speakers.All(speaker=>speaker.Bus==AudioSettingsService.AmbienceBus),
                "radio speakers use the existing ambience bus");
            RestoreRadioFixture(fixture,firstId,3.25,.4f);
            await WaitRadioUntil(()=>speakers.Any(speaker=>speaker.Playing&&speaker.HasStreamPlayback()
                &&speaker.GetPlaybackPosition()>3.27),"native radio starts at the restored sample position");
            await RadioSeconds(.35);
            var active=speakers.Single(speaker=>speaker.Playing);
            var position=active.GetPlaybackPosition();
            Require(Math.Abs(position-fixture.SegmentOffset)<.20,
                "saved programme offset and real native stream cursor agree after seek");

            fixture.SetPaused(true);await RadioSeconds(.12);
            var pausedPosition=active.GetPlaybackPosition();var pausedOffset=fixture.SegmentOffset;
            await RadioSeconds(.22);
            Require(active.StreamPaused&&Math.Abs(active.GetPlaybackPosition()-pausedPosition)<.025
                &&Math.Abs(fixture.SegmentOffset-pausedOffset)<.001,
                "radio pause freezes both the mixer cursor and saved timeline");
            fixture.SetPaused(false);
            await WaitRadioUntil(()=>active.GetPlaybackPosition()>pausedPosition+.05,
                "radio resumes the paused native stream");
            fixture.SetVolume(.23f);await Frames(3);
            Require(Math.Abs(active.VolumeDb-Mathf.LinearToDb(.23f*.42f))<.03f,
                "radio volume changes the local source gain");
            var saved=JsonSerializer.SerializeToElement(fixture.Capture());
            var savedOffset=fixture.SegmentOffset;
            fixture.SetEnabled(false);await RadioSeconds(.08);
            Require(speakers.All(speaker=>!speaker.Playing),"radio off stops both real sources");
            await RadioSeconds(.08);
            Require(Math.Abs(fixture.SegmentOffset-savedOffset)<.001,"radio off preserves its saved position");
            fixture.Restore(saved);
            await WaitRadioUntil(()=>speakers.Any(speaker=>speaker.Playing&&speaker.GetPlaybackPosition()>savedOffset),
                "saved radio power and position restart real playback");
            await RadioSeconds(.35);active=speakers.Single(speaker=>speaker.Playing);
            Require(Math.Abs(active.GetPlaybackPosition()-fixture.SegmentOffset)<.20
                &&Math.Abs(active.VolumeDb-Mathf.LinearToDb(.23f*.42f))<.03f,
                "radio restore retains both native seek and volume");

            var bus=AudioServer.GetBusIndex(AudioSettingsService.AmbienceBus);
            var effects=Enumerable.Range(0,AudioServer.GetBusEffectCount(bus))
                .Select(index=>AudioServer.GetBusEffect(bus,index)).OfType<AudioEffectAmplify>().ToArray();
            Require(effects.Length==1&&!ambience.VoiceDuckActive,"one ambience owner supplies the voice duck effect");
            var effect=effects.Single();var localGain=active.VolumeDb;var userGain=AudioServer.GetBusVolumeDb(bus);
            var voiceTemplate=CompiledCampaignRepository.Load().ResolveAudio(
                "urman.chapter1:asset/audio-marat-voice","smoke:radio-bus-fixture");
            // This explicitly labelled CC0 noise exercises the actual cue queue;
            // its distinct asset ID cannot trigger a finale voice story event.
            var testCue=voiceTemplate with { Asset=voiceTemplate.Asset with {
                AssetId="smoke:asset/cc0-radio-duck-noise",MediaType="audio/wav",Url=firstPath,Sha256=null},
                Captions=null,Transcript=null,NonAudioCue=null };
            cue.Present(testCue);await RadioSeconds(.18);
            Require(cue.IsPresenting&&ambience.VoiceDuckActive&&Math.Abs(effect.VolumeDb+6)<.05f,
                "the actual cue applies one minus-six-decibel ambience bus effect");
            Require(Math.Abs(active.VolumeDb-localGain)<.03f&&Math.Abs(AudioServer.GetBusVolumeDb(bus)-userGain)<.01f,
                "voice duck preserves radio local gain and the user's bus volume");
            cue.ResetPresentation();await RadioSeconds(.18);
            Require(!ambience.VoiceDuckActive&&Math.Abs(effect.VolumeDb)<.05f,
                "ending the cue releases the actual bus effect");

            RestoreRadioFixture(fixture,firstId,first.GetLength()-.65,.23f);
            await WaitRadioUntil(()=>speakers.Any(speaker=>speaker.Playing&&speaker.Stream==first),
                "EOF transition starts from the first real recording");
            var gapStart=0UL;var maxGap=0UL;var deadline=Time.GetTicksMsec()+3000;
            while(fixture.CurrentSegmentId!=secondId||speakers.All(speaker=>!speaker.Playing||speaker.Stream!=second))
            {
                if(Time.GetTicksMsec()>deadline)throw new InvalidOperationException("Native radio did not cross the real file boundary.");
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var now=Time.GetTicksMsec();
                if(speakers.All(speaker=>!speaker.Playing)){if(gapStart==0)gapStart=now;maxGap=Math.Max(maxGap,now-gapStart);}
                else if(gapStart!=0){maxGap=Math.Max(maxGap,now-gapStart);gapStart=0;}
            }
            await RadioSeconds(.35);
            var incoming=speakers.Single(speaker=>speaker.Playing);
            Require(incoming.Stream==second&&incoming.GetPlaybackPosition()>.1f
                &&Math.Abs(incoming.GetPlaybackPosition()-fixture.SegmentOffset)<.20,
                "the native file boundary selects the next recording and preserves fractional time");
            Require(maxGap<200,"recording transition has no sustained loss of both native players");
            _records.Add(new{kind="native-radio-fixtures",firstPath,secondPath,provenance,
                firstDuration=first.GetLength(),secondDuration=second.GetLength(),maxObservedNoPlayerMilliseconds=maxGap,
                limitations="CC0 noise only; no programme delivery, listening, sample-continuity or overlap-crossfade acceptance"});
        }
        finally
        {
            cue.ResetPresentation();fixture.SetEnabled(false);
            foreach(var speaker in fixture.GetChildren().OfType<AudioStreamPlayer3D>())speaker.Stream=null;
            fixture.QueueFree();await Frames(3);
            Require(sharedFirst.LoopMode==firstLoop&&sharedSecond.LoopMode==secondLoop,
                "radio fixtures preserve the ambience owner's shared resource loop settings");
        }
    }

    private static void RestoreRadioFixture(VehicleRadioPlayer player,string id,double offset,float volume)=>
        player.Restore(JsonSerializer.SerializeToElement(new{enabled=true,segmentId=id,offsetSeconds=offset,volume}));

    private void VerifyRadioFixtureHash(string resource,string expected)
    {
        using var input=File.OpenRead(ProjectSettings.GlobalizePath(resource));
        var actual=Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        Require(actual==expected,"CC0 radio fixture matches its recorded provenance hash: "+resource);
    }

    private async Task WaitRadioUntil(Func<bool> condition,string label)
    {
        var deadline=Time.GetTicksMsec()+3000;
        while(!condition())
        {
            if(Time.GetTicksMsec()>deadline)throw new InvalidOperationException(label);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        Require(true,label);
    }

    private async Task RadioSeconds(double seconds)=>
        await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
}
