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
/// Car-radio playback uses a data programme, two local sources and one saved
/// segment/offset. A transcript is never substituted for a missing voice file.
/// Present recordings and explicitly licensed synthetic previews share playback;
/// their delivery and acceptance counts remain distinct.
/// </summary>
public partial class VehicleRadioPlayer : Node3D
{
    public const string StationPath="res://content/vehicles/avyl_radio.v1.json";
    private readonly AudioStreamPlayer3D[] _players=new AudioStreamPlayer3D[2];
    private readonly Dictionary<string,AudioStream?> _streams=new(StringComparer.Ordinal);
    private VehicleRadioTimeline _timeline=null!;
    private int _active=-1;
    private bool _paused;
    private bool _native;
    private float _fade;
    private float _volume=.65f;
    private string _stationName=string.Empty;
    public bool Enabled { get; private set; }
    public string Display => _stationName;
    public string CurrentSegmentId => _timeline.Current.Id;
    public double SegmentOffset => _timeline.Offset;
    public int PlayableSegmentCount { get; private set; }
    public int RecordedSegmentCount { get; private set; }
    public int SyntheticPreviewCount { get; private set; }
    public int RepeatedSegmentCount { get; private set; }
    public int ProgrammedSegmentCount { get; private set; }
    public int MissingRecordingCount => ProgrammedSegmentCount-PlayableSegmentCount;

    public override void _Ready()
    {
        AudioSettingsService.EnsureBuses();_native=DisplayServer.GetName()!="headless";
        using var file=global::Godot.FileAccess.Open(StationPath,global::Godot.FileAccess.ModeFlags.Read)
            ??throw new InvalidDataException("Radio programme is missing.");
        using var document=JsonDocument.Parse(file.GetAsText());var root=document.RootElement;
        _stationName=root.GetProperty("displayName").GetString()!;
        var segments=root.GetProperty("segments").EnumerateArray().Select(VehicleRadioSegment.FromJson).ToArray();
        // Validate the complete authored programme even while some deliveries
        // are pending. Its missing rows never become periods of silent playback.
        _=new VehicleRadioTimeline(segments);ProgrammedSegmentCount=segments.Length;
        foreach(var segment in segments)
        {
            if(!segment.HasValidRepeatSource(segments))
                throw new InvalidDataException("A repeated radio segment differs from its approved source: "+segment.Id);
            var stream=segment.CanPlay && !string.IsNullOrEmpty(segment.StreamPath)&&ResourceLoader.Exists(segment.StreamPath)
                ?ResourceLoader.Load<AudioStream>(segment.StreamPath):null;
            _streams[segment.Id]=stream;
            if(stream is not null)
            {
                PlayableSegmentCount++;
                if(segment.IsSyntheticPreview)SyntheticPreviewCount++;
                else if(segment.IsRecordingRepeat)RepeatedSegmentCount++;else RecordedSegmentCount++;
            }
        }
        var playable=segments.Where(segment=>_streams[segment.Id] is not null).ToArray();
        _timeline=new(playable.Length>0?playable:segments);
        for(var i=0;i<2;i++)
        {
            _players[i]=new AudioStreamPlayer3D{Name="RadioSpeaker"+i,Bus=AudioSettingsService.AmbienceBus,
                UnitSize=2,MaxDistance=12,VolumeDb=-60,Autoplay=false};
            AddChild(_players[i]);
        }
        SetMeta("stationPath",StationPath);SetMeta("playableRecordings",PlayableSegmentCount);
        SetMeta("recordedSegments",RecordedSegmentCount);SetMeta("syntheticPreviews",SyntheticPreviewCount);
        SetMeta("repeatedSegments",RepeatedSegmentCount);
        SetMeta("missingRecordings",MissingRecordingCount);
        SetMeta("recordingStatus",$"recorded={RecordedSegmentCount}; repeats={RepeatedSegmentCount}; synthetic-preview={SyntheticPreviewCount}; pending={MissingRecordingCount}; listening and language acceptance remain separate");
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
        if(_timeline is null||!Enabled||_paused)return;
        if(_timeline.Advance(delta))StartCurrent();
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
        if(_timeline is null)return;
        var next=(_active+1)%2;_players[next].Stop();_players[next].Stream=_streams[_timeline.Current.Id];
        _active=next;_fade=0;
        if(_native&&_players[next].Stream is { } stream)
        {
            var seek=Math.Min(_timeline.Offset,Math.Max(0,stream.GetLength()-.01));
            _players[next].Play((float)seek);_players[next].StreamPaused=_paused;
        }
        SetMeta("currentSegment",_timeline.Current.Id);
        SetMeta("currentRecordingPresent",_players[next].Stream is not null);
    }

    private void StopPlayers()
    {foreach(var player in _players)if(player is not null)player.Stop();_active=-1;_fade=0;}

    public JsonObject Capture()=>new(){["enabled"]=Enabled,["segmentId"]=_timeline.Current.Id,
        ["offsetSeconds"]=_timeline.Offset,["volume"]=_volume};

    public void Restore(JsonElement? record)
    {
        if(_timeline is null)return;
        StopPlayers();Enabled=false;_timeline.Restore(null,0);_volume=.65f;
        if(record is {} value&&value.ValueKind==JsonValueKind.Object)
        {
            var id=value.TryGetProperty("segmentId",out var segment)&&segment.ValueKind==JsonValueKind.String?segment.GetString():null;
            var offset=value.TryGetProperty("offsetSeconds",out var seconds)&&seconds.TryGetDouble(out var parsed)?parsed:0;
            _timeline.Restore(id,offset);
            if(value.TryGetProperty("volume",out var volume)&&volume.TryGetSingle(out var level))SetVolume(level);
            Enabled=value.TryGetProperty("enabled",out var enabled)&&enabled.ValueKind==JsonValueKind.True;
        }
        if(Enabled)StartCurrent();
    }

    public override void _ExitTree(){StopPlayers();foreach(var player in _players)if(player is not null)player.Stream=null;_streams.Clear();}
}
