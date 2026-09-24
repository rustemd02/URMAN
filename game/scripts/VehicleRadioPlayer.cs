using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urman.Godot;

public sealed record VehicleRadioSegment(string Id,string Kind,string Language,string Title,string Transcript,
    string StreamPath,double DurationSeconds,string RightsStatus,string Provenance,
    string Origin="",string Acceptance="",string ModelLicense="",string RepeatsSegmentId="",string Sha256="")
{
    public bool IsSyntheticPreview => RightsStatus=="licensed-synthetic-preview"
        && Origin=="synthetic-preview" && ModelLicense=="MIT"
        && Acceptance=="preview-pending-listening-and-language-review" && Kind!="music";
    public bool IsRecordingRepeat => RightsStatus=="licensed-recording-repeat" && Origin=="recorded-music-repeat"
        && Acceptance=="reuses-existing-approved-recording" && Kind=="music" && !string.IsNullOrWhiteSpace(RepeatsSegmentId);
    public bool CanPlay => !string.IsNullOrWhiteSpace(Provenance)
        && (RightsStatus=="approved" || IsSyntheticPreview || IsRecordingRepeat);

    public bool HasValidRepeatSource(IReadOnlyList<VehicleRadioSegment> programme)
        => !IsRecordingRepeat || programme.Any(source=>source.Id==RepeatsSegmentId&&source.Id!=Id
            &&source.RightsStatus=="approved"&&source.Kind=="music"&&source.CanPlay
            &&source.StreamPath==StreamPath&&source.Provenance==Provenance&&source.Sha256==Sha256
            &&Sha256.Length==64&&source.Language==Language&&source.DurationSeconds==DurationSeconds);

    public static VehicleRadioSegment FromJson(JsonElement row)
    {
        string Text(string key)=>row.TryGetProperty(key,out var value)?value.GetString()??string.Empty:string.Empty;
        return new(Text("id"),Text("kind"),Text("language"),Text("title"),Text("transcript"),
            Text("streamPath"),row.GetProperty("durationSeconds").GetDouble(),Text("rightsStatus"),Text("provenance"),
            Text("origin"),Text("acceptance"),Text("modelLicense"),Text("repeatsSegmentId"),Text("sha256"));
    }
}

/// <summary>Stable segment IDs preserve playback when station data is reordered.</summary>
public sealed class VehicleRadioTimeline
{
    public IReadOnlyList<VehicleRadioSegment> Segments { get; }
    public int Index { get; private set; }
    public double Offset { get; private set; }
    public VehicleRadioSegment Current => Segments[Index];
    public VehicleRadioTimeline(IReadOnlyList<VehicleRadioSegment> segments)
    {
        if(segments.Count==0 || segments.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=segments.Count
            || segments.Any(x=>string.IsNullOrWhiteSpace(x.Id)||!double.IsFinite(x.DurationSeconds)||x.DurationSeconds<=0))
            throw new InvalidDataException("Radio programme needs unique IDs and positive finite durations.");
        Segments=segments;
    }
    public bool Advance(double seconds)
    {
        if(!double.IsFinite(seconds)||seconds<=0)return false;
        var crossed=Offset+seconds>=Current.DurationSeconds;Offset+=seconds;
        var total=Segments.Sum(segment=>segment.DurationSeconds);
        if(Offset>total)Offset%=total;
        for(var count=0;Offset>=Current.DurationSeconds&&count<=Segments.Count;count++)
        {Offset-=Current.DurationSeconds;Index=(Index+1)%Segments.Count;}
        return crossed;
    }
    public void Restore(string? id,double offset)
    {
        Index=0;var found=false;
        for(var i=0;i<Segments.Count;i++)if(Segments[i].Id==id){Index=i;found=true;break;}
        Offset=found&&double.IsFinite(offset)?Math.Clamp(offset,0,Math.Max(0,Current.DurationSeconds-.001)):0;
    }
}

/// <summary>
/// Car-radio playback uses a data programme per station, two local sources and
/// one saved station/segment/offset. A transcript is never substituted for a
/// missing voice file. Present recordings and explicitly licensed synthetic
/// previews share playback; their delivery and acceptance counts remain
/// distinct and are reported for the station that is actually tuned in.
/// </summary>
public partial class VehicleRadioPlayer : Node3D
{
    public const string StationPath="res://content/vehicles/avyl_radio.v1.json";
    /// <summary>The dial. The first entry is the station a new session tunes to.</summary>
    public static readonly string[] StationPaths=[
        StationPath,
        "res://content/vehicles/kirlay_archive_radio.v1.json"
    ];

    /// <summary>One tuned frequency: its own programme, sources and phase.</summary>
    private sealed class Station
    {
        public required string Id{get;init;}
        public required string DisplayName{get;init;}
        public required string Frequency{get;init;}
        public required VehicleRadioTimeline Timeline{get;set;}
        public required Dictionary<string,AudioStream?> Streams{get;init;}
        public int ProgrammedSegmentCount{get;init;}
        public int PlayableSegmentCount{get;init;}
        public int RecordedSegmentCount{get;init;}
        public int SyntheticPreviewCount{get;init;}
        public int RepeatedSegmentCount{get;init;}
        // Phase kept per station so switching away and back resumes the same
        // point of the same programme instead of restarting it.
        public string? SavedSegmentId{get;set;}
        public double SavedOffset{get;set;}
    }

    private readonly AudioStreamPlayer3D[] _players=new AudioStreamPlayer3D[2];
    private readonly List<Station> _stations=[];
    private Station _station=null!;
    private int _active=-1;
    private bool _paused;
    private bool _native;
    private float _fade;
    private float _volume=.65f;
    public bool Enabled { get; private set; }
    public string Display => _station.DisplayName;
    /// <summary>Dial readout for the dashboard: the tuned frequency, or dashes when off.</summary>
    public string Tuning => Enabled ? _station.Frequency : "— —";
    public string CurrentStationId => _station.Id;
    public int StationIndex { get; private set; }
    public int StationCount => _stations.Count;
    public string CurrentSegmentId => _station.Timeline.Current.Id;
    public double SegmentOffset => _station.Timeline.Offset;
    /// <summary>The programme order actually being played on the tuned station.</summary>
    public IReadOnlyList<VehicleRadioSegment> ActiveProgramme => _station.Timeline.Segments;
    public int PlayableSegmentCount => _station.PlayableSegmentCount;
    public int RecordedSegmentCount => _station.RecordedSegmentCount;
    public int SyntheticPreviewCount => _station.SyntheticPreviewCount;
    public int RepeatedSegmentCount => _station.RepeatedSegmentCount;
    public int ProgrammedSegmentCount => _station.ProgrammedSegmentCount;
    public int MissingRecordingCount => ProgrammedSegmentCount-PlayableSegmentCount;

    public override void _Ready()
    {
        AudioSettingsService.EnsureBuses();_native=DisplayServer.GetName()!="headless";
        foreach(var path in StationPaths)_stations.Add(LoadStation(path));
        if(_stations.Count==0)throw new InvalidDataException("The dial has no stations.");
        _station=_stations[0];
        for(var i=0;i<2;i++)
        {
            _players[i]=new AudioStreamPlayer3D{Name="RadioSpeaker"+i,Bus=AudioSettingsService.AmbienceBus,
                UnitSize=2,MaxDistance=12,VolumeDb=-60,Autoplay=false};
            AddChild(_players[i]);
        }
        PublishStationMeta();
    }

    private static Station LoadStation(string path)
    {
        using var file=global::Godot.FileAccess.Open(path,global::Godot.FileAccess.ModeFlags.Read)
            ??throw new InvalidDataException("Radio programme is missing: "+path);
        using var document=JsonDocument.Parse(file.GetAsText());var root=document.RootElement;
        var segments=root.GetProperty("segments").EnumerateArray().Select(VehicleRadioSegment.FromJson).ToArray();
        // Validate the complete authored programme even while some deliveries
        // are pending. Its missing rows never become periods of silent playback.
        _=new VehicleRadioTimeline(segments);
        var streams=new Dictionary<string,AudioStream?>(StringComparer.Ordinal);
        var playable=0;var recorded=0;var previews=0;var repeats=0;
        foreach(var segment in segments)
        {
            if(!segment.HasValidRepeatSource(segments))
                throw new InvalidDataException("A repeated radio segment differs from its approved source: "
                    +path+":"+segment.Id);
            var stream=segment.CanPlay && !string.IsNullOrEmpty(segment.StreamPath)&&ResourceLoader.Exists(segment.StreamPath)
                ?ResourceLoader.Load<AudioStream>(segment.StreamPath):null;
            streams[segment.Id]=stream;
            if(stream is not null)
            {
                playable++;
                if(segment.IsSyntheticPreview)previews++;
                else if(segment.IsRecordingRepeat)repeats++;else recorded++;
            }
        }
        var audible=segments.Where(segment=>streams[segment.Id] is not null).ToArray();
        return new Station{
            Id=root.GetProperty("stationId").GetString()!,
            DisplayName=root.GetProperty("displayName").GetString()!,
            Frequency=root.TryGetProperty("frequencyMhz",out var mhz)&&mhz.TryGetDouble(out var value)
                ?value.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
                :root.GetProperty("displayName").GetString()!,
            Timeline=new(audible.Length>0?audible:segments),
            Streams=streams,
            ProgrammedSegmentCount=segments.Length,
            PlayableSegmentCount=playable,
            RecordedSegmentCount=recorded,
            SyntheticPreviewCount=previews,
            RepeatedSegmentCount=repeats,
        };
    }

    /// <summary>
    /// Moves to the next frequency on the dial, keeping the current station's
    /// position so a later switch back resumes it. Playback restarts only when
    /// the radio is on, so tuning while off does not start audio.
    /// </summary>
    public bool NextStation()
    {
        if(_stations.Count<2)return false;
        RememberStationPhase();
        StopPlayers();
        StationIndex=(StationIndex+1)%_stations.Count;
        _station=_stations[StationIndex];
        _station.Timeline.Restore(_station.SavedSegmentId,_station.SavedOffset);
        PublishStationMeta();
        if(Enabled)StartCurrent();
        return true;
    }

    private void RememberStationPhase()
    {
        _station.SavedSegmentId=_station.Timeline.Current.Id;
        _station.SavedOffset=_station.Timeline.Offset;
    }

    private void PublishStationMeta()
    {
        SetMeta("stationId",_station.Id);SetMeta("stationDisplay",_station.DisplayName);
        SetMeta("stationIndex",StationIndex);SetMeta("stationCount",_stations.Count);
        SetMeta("playableRecordings",PlayableSegmentCount);
        SetMeta("recordedSegments",RecordedSegmentCount);SetMeta("syntheticPreviews",SyntheticPreviewCount);
        SetMeta("repeatedSegments",RepeatedSegmentCount);
        SetMeta("missingRecordings",MissingRecordingCount);
        SetMeta("recordingStatus",$"station={_station.Id}; recorded={RecordedSegmentCount}; repeats={RepeatedSegmentCount}; synthetic-preview={SyntheticPreviewCount}; pending={MissingRecordingCount}; listening and language acceptance remain separate");
    }

    public void SetEnabled(bool enabled)
    {
        if(Enabled==enabled)return;Enabled=enabled;
        if(enabled)StartCurrent();else StopPlayers();
    }

    public void SetPaused(bool paused)
    {
        _paused=paused;
        foreach(var player in _players)if(player is not null)player.StreamPaused=paused;
    }

    public void SetVolume(float volume)=>_volume=float.IsFinite(volume)?Math.Clamp(volume,0,1):.65f;

    public override void _Process(double delta)
    {
        if(!Enabled||_paused)return;
        if(_station.Timeline.Advance(delta))StartCurrent();
        if(_active<0)return;
        _fade=Math.Min(1,_fade+(float)delta/.3f);
        // AmbientAudioDirector owns the transient voice attenuation on this bus.
        // Applying it here as well would double-duck the same radio signal.
        _players[_active].VolumeDb=Mathf.LinearToDb(Math.Max(.0001f,_volume*_fade*.42f));
        _players[1-_active].VolumeDb=Mathf.LinearToDb(Math.Max(.0001f,_volume*(1-_fade)*.42f));
        if(_fade>=1&&_players[1-_active].Playing)_players[1-_active].Stop();
    }

    private void StartCurrent()
    {
        var next=(_active+1)%2;_players[next].Stop();_players[next].Stream=_station.Streams[_station.Timeline.Current.Id];
        _active=next;_fade=0;
        if(_native&&_players[next].Stream is { } stream)
        {
            var seek=Math.Min(_station.Timeline.Offset,Math.Max(0,stream.GetLength()-.01));
            _players[next].Play((float)seek);_players[next].StreamPaused=_paused;
        }
        SetMeta("currentSegment",_station.Timeline.Current.Id);
        SetMeta("currentRecordingPresent",_players[next].Stream is not null);
    }

    private void StopPlayers()
    {foreach(var player in _players)if(player is not null)player.Stop();_active=-1;_fade=0;}

    public JsonObject Capture()=>new(){["enabled"]=Enabled,["stationId"]=_station.Id,
        ["segmentId"]=_station.Timeline.Current.Id,
        ["offsetSeconds"]=_station.Timeline.Offset,["volume"]=_volume};

    public void Restore(JsonElement? record)
    {
        if(_stations.Count==0)return;
        StopPlayers();Enabled=false;
        // A record without a station id is an older save: it belongs to the
        // first station, which is the one those sessions could tune to.
        var stationId=record is {} probe&&probe.ValueKind==JsonValueKind.Object
            &&probe.TryGetProperty("stationId",out var stored)&&stored.ValueKind==JsonValueKind.String
            ?stored.GetString():null;
        StationIndex=Math.Max(0,_stations.FindIndex(entry=>entry.Id==stationId));
        _station=_stations[StationIndex];
        foreach(var entry in _stations){entry.SavedSegmentId=null;entry.SavedOffset=0;entry.Timeline.Restore(null,0);}
        _volume=.65f;
        if(record is {} value&&value.ValueKind==JsonValueKind.Object)
        {
            var id=value.TryGetProperty("segmentId",out var segment)&&segment.ValueKind==JsonValueKind.String?segment.GetString():null;
            var offset=value.TryGetProperty("offsetSeconds",out var seconds)&&seconds.TryGetDouble(out var parsed)?parsed:0;
            _station.Timeline.Restore(id,offset);
            if(value.TryGetProperty("volume",out var volume)&&volume.TryGetSingle(out var level))SetVolume(level);
            Enabled=value.TryGetProperty("enabled",out var enabled)&&enabled.ValueKind==JsonValueKind.True;
        }
        RememberStationPhase();
        PublishStationMeta();
        if(Enabled)StartCurrent();
    }

    public override void _ExitTree(){StopPlayers();foreach(var player in _players)if(player is not null)player.Stream=null;_stations.Clear();}
}
