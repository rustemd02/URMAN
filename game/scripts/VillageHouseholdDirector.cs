using System.Text.Json;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Urman.Godot;

/// <summary>Presentation only: no per-house players, NPC simulation, or save state.</summary>
public partial class VillageHouseholdDirector : Node3D
{
    public const int SpillBudget = 6;
    public const int VoiceBudget = 4;
    public const int ClipBudget = 8;
    public const int PcmBudget = 2_000_000;
    public const int ExpectedMotifCount = 47;
    public const string CatalogPath = "res://content/world/act1_village_life.v1.json";
    private readonly List<Window> _windows = [];
    private readonly List<House> _houses = [];
    private readonly HashSet<ulong> _inhabitedOwners = new();
    private readonly SpotLight3D[] _lights = new SpotLight3D[SpillBudget];
    private readonly AudioStreamPlayer3D[] _voices = new AudioStreamPlayer3D[VoiceBudget];
    private readonly int[] _voiceHouses = [-1,-1,-1,-1];
    private readonly Dictionary<string, Clip> _clips = new(StringComparer.Ordinal);
    private Motif[] _motifs = [];
    private readonly RandomNumberGenerator _random = new();
    private readonly int[] _nearest = new int[SpillBudget];
    private readonly int[] _boundWindows = [-1,-1,-1,-1,-1,-1];
    private readonly float[] _distances = new float[SpillBudget];
    private double _tick, _now, _nextEvent;
    private long _access;
    private int _clipBytes;
    public int WindowCount => _windows.Count;
    public int HouseCount => _houses.Count;
    public int MotifCount => _motifs.Length;
    public int LoadedClipCount => _clips.Count;
    public int LoadedPcmBytes => _clipBytes;
    public int PlayingVoiceCount => _voices.Count(v => v is not null && v.Playing);
    public int VisibleSpillCount => _lights.Count(l => l is not null && l.Visible);
    public int EventsStarted { get; private set; }

    /// <summary>
    /// 1 = the authored ordinary living village; 0 = the village has gone
    /// quiet and hostile (fewer, softer household events; the sound-mood
    /// director raises its dread layer instead). Presentation only.
    /// </summary>
    public float Mood { get; set; } = 1f;

    public IReadOnlyList<string> MotifIds => _motifs.Select(m => m.Id).ToArray();

    public void Initialize(Node3D world)
    {
        if (_lights[0] is not null) return;
        _random.Randomize();
        var json = FileAccess.GetFileAsString(CatalogPath);
        _motifs = JsonSerializer.Deserialize<Catalog>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive=true })?.Events
            ?? throw new InvalidOperationException("Village household sound catalog missing");
        if (_motifs.Length != ExpectedMotifCount || _motifs.Select(m=>m.Id).Distinct().Count()!=ExpectedMotifCount
            || _motifs.Any(m=>!ResourceLoader.Exists(m.File) || m.Radius is <6 or >25 || m.MinWait<20 || m.MaxWait<m.MinWait))
            throw new InvalidOperationException("Invalid household event/catalog budgets");
        var owners = new Dictionary<ulong, House>();
        foreach (var pane in world.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>())
        {
            if (pane.Mesh is null || !pane.IsVisibleInTree()) continue;
            var name=pane.Name.ToString();
            var glass=name.Contains("Window",StringComparison.Ordinal) && name.Contains("Glass",StringComparison.Ordinal);
            if (!glass && !pane.HasMeta("occupiedWindow")) continue;
            var owner=ResidentialOwner(pane);
            if (owner is null) continue;
            // Bind after relocation/deferred suppression. Hidden kit copies do not
            // acquire sources; transparent glass in playable interiors stays intact.
            var identity=owner.GetPath().ToString();
            pane.MaterialOverride=VillageWindowMaterials.For(identity);
            pane.SetMeta("occupiedWindow",true);
            pane.SetMeta("lightingRole","inhabited residential glass; shared warm curtains and frost");
            var bounds=pane.Mesh.GetAabb();
            var centre=pane.GlobalTransform*bounds.GetCenter();
            var normal=(pane.GlobalBasis*(bounds.Size.X<bounds.Size.Z ? Vector3.Right:Vector3.Back));
            normal.Y=0; normal=normal.Normalized();
            if (normal.Dot(centre-owner.GlobalPosition)<0) normal=-normal;
            _windows.Add(new Window(pane, centre, normal, pane.GetPath().ToString()));
            if (!owners.TryGetValue(owner.GetInstanceId(),out var house))
            {
                house=new House(owner,identity,centre-normal*.65f,centre+normal*.7f);
                owners.Add(owner.GetInstanceId(),house);
            }
        }
        _houses.AddRange(owners.Values.OrderBy(h=>h.Identity,StringComparer.Ordinal));
        foreach(var owner in owners.Keys)_inhabitedOwners.Add(owner);
        for (var i=0;i<_houses.Count;i++)
        {
            // Stable complete distribution, with runtime-random pauses. No reroll
            // on approach and no timer-based off-screen playback.
            _houses[i].Motif=i%_motifs.Length;
            _houses[i].Wait=2+(TimberHomeStyle.StableHash(_houses[i].Identity)%7);
        }
        for(var i=0;i<SpillBudget;i++)
        {
            var light=new SpotLight3D {Name=$"WindowSpill{i}",Visible=false,LightColor=Color.FromHtml("ffd3a1"),
                LightEnergy=.75f,SpotRange=4.9f,SpotAngle=65f,SpotAttenuation=1.6f,
                ShadowEnabled=true,LightSize=.12f,ShadowBias=.035f};
            light.SetMeta("lightingRole","bounded nearest residential window spill");
            AddChild(light); _lights[i]=light;
        }
        AudioSettingsService.EnsureBuses();
        for(var i=0;i<VoiceBudget;i++)
        {
            var voice=new AudioStreamPlayer3D {Name=$"HouseholdVoice{i}",Autoplay=false,
                Bus=AudioSettingsService.AmbienceBus,MaxPolyphony=1,UnitSize=4,
                MaxDistance=25,AttenuationModel=AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                VolumeDb=-13,AttenuationFilterDb=-14};
            AddChild(voice); _voices[i]=voice;
        }
        SetMeta("windowCount",WindowCount); SetMeta("houseCount",HouseCount);
        SetMeta("eventTypes",MotifCount); SetMeta("presentationOnly",true);
        SetMeta("lightBudget",SpillBudget);SetMeta("voiceBudget",VoiceBudget);
        SetMeta("clipBudget",ClipBudget); SetMeta("pcmBudgetBytes",PcmBudget);
        GD.Print($"village-households: {HouseCount} homes, {WindowCount} warm panes, {MotifCount} events; budgets lights={SpillBudget}, voices={VoiceBudget}, clips={ClipBudget}, PCM={PcmBudget}");
    }

    /// <summary>The residential owner rule shared with the chimney smoke pool: a
    /// visible pane or chimney binds only to a dwelling, a facade or a neighbour
    /// facade, never to an outbuilding.</summary>
    public static Node3D? ResidentialOwner(Node node)
    {
        for(var parent=node.GetParent();parent is Node3D p;parent=p.GetParent())
        {
            var name=p.Name.ToString();
            if(name.EndsWith("_Dwelling",StringComparison.Ordinal)
                || name.Contains("DwellingFacade",StringComparison.Ordinal)
                || name.EndsWith("NeighborFacade",StringComparison.Ordinal)) return p;
            if(name.EndsWith("_Outbuilding",StringComparison.Ordinal)) return null;
        }
        return null;
    }

    /// <summary>True for the exact owner node the window pass counted as an
    /// inhabited home. Chimney smoke uses this house list, so a chimney on a
    /// building the director does not know as a home never smokes.</summary>
    public bool IsInhabitedOwner(Node3D owner) => _inhabitedOwners.Contains(owner.GetInstanceId());

    public void Tick(double delta,Vector3 viewer,Vector3 listener,bool audible,bool night)
    {
        _now+=Math.Min(delta,.5); _tick+=delta;
        if(!audible) StopVoices(); // Dialogue/prologue/pause/shelter/ending gate.
        if(_tick<.25) return;
        var elapsed=Math.Min(_tick,.5); _tick=0;
        VillageWindowMaterials.SetNight(night);
        UpdateSpills(viewer,night);
        if(!audible) return;
        // The deep-dread end of the sound-mood scale keeps the houses dark and
        // silent; only the sound-mood layers carry the village then.
        if(Mood<.15f) return;
        for(var slot=0;slot<VoiceBudget;slot++)
        {
            var index=_voiceHouses[slot];
            if(index<0) continue;
            var house=_houses[index]; var motif=_motifs[house.Motif];
            if(!_voices[slot].Playing || _voices[slot].GlobalPosition.DistanceSquaredTo(listener)>motif.Radius*motif.Radius
                || !house.Owner.IsVisibleInTree()) ReleaseVoice(slot);
        }
        int best=-1; float bestDistance=float.MaxValue;
        for(var i=0;i<_houses.Count;i++)
        {
            var house=_houses[i]; var motif=_motifs[house.Motif];
            if(!house.Owner.IsVisibleInTree() || night && motif.DayOnly || !night && motif.NightOnly) continue;
            var position=motif.Outdoor ? house.Outside:house.Inside;
            var distance=position.DistanceSquaredTo(listener);
            if(distance>motif.Radius*motif.Radius) continue;
            house.Wait-=elapsed;
            if(house.Wait<=0 && distance<bestDistance && !_voiceHouses.Contains(i)) {best=i;bestDistance=distance;}
        }
        if(best<0 || _now<_nextEvent) return;
        var available=Array.FindIndex(_voiceHouses,h=>h<0);
        if(available<0) return;
        StartEvent(best,available);
        _nextEvent=_now+2.5; // Prevent a chorus at a crossroads.
    }

    private void UpdateSpills(Vector3 viewer,bool night)
    {
        Array.Fill(_nearest,-1);Array.Fill(_distances,18f*18f);
        for(var i=0;i<_windows.Count;i++)
        {
            var w=_windows[i]; if(!w.Pane.IsVisibleInTree())continue;
            var d=viewer.DistanceSquaredTo(w.Centre);
            for(var slot=0;slot<SpillBudget;slot++)
            {
                if(d>=_distances[slot])continue;
                for(var j=SpillBudget-1;j>slot;j--) {_distances[j]=_distances[j-1];_nearest[j]=_nearest[j-1];}
                _distances[slot]=d;_nearest[slot]=i;break;
            }
        }
        for(var slot=0;slot<SpillBudget;slot++)
        {
            var light=_lights[slot]; var index=_nearest[slot];
            if(index<0) {light.Visible=false;continue;}
            var w=_windows[index]; light.GlobalPosition=w.Centre+w.Normal*.13f;
            light.LookAt(light.GlobalPosition+w.Normal+Vector3.Down*.22f);
            // Fade distant spill before the material-only handoff; no global glow.
            light.LightEnergy=(night?1.25f:.78f)*(1-Mathf.SmoothStep(12,18,Mathf.Sqrt(_distances[slot])));
            light.Visible=true;
            if(_boundWindows[slot]!=index) {light.SetMeta("sourcePane",w.Path);_boundWindows[slot]=index;}
        }
    }

    private void StartEvent(int houseIndex,int slot)
    {
        var house=_houses[houseIndex];var motif=_motifs[house.Motif];
        var stream=GetClip(motif.File);
        if(stream is null) {house.Wait=30;return;}
        var voice=_voices[slot]; voice.Stream=stream;voice.GlobalPosition=motif.Outdoor?house.Outside:house.Inside;
        // Indoor motifs sit 3 dB lower and roll off harder, so a sound behind a
        // wall never reads louder than the same event in the open street.
        voice.VolumeDb=motif.VolumeDb+(motif.Outdoor?0f:-3f)+(Mood-1f)*8f;voice.MaxDistance=motif.Radius;
        voice.AttenuationFilterCutoffHz=motif.Outdoor?9000:1200;
        voice.AttenuationFilterDb=motif.Outdoor?-6f:-20f;
        voice.PitchScale=_random.RandfRange(.97f,1.03f);
        voice.SetMeta("eventId",motif.Id);voice.SetMeta("house",house.Identity);
        _voiceHouses[slot]=houseIndex;voice.Play();EventsStarted++;
        house.Wait=_random.RandfRange(motif.MinWait,motif.MaxWait)*(1f+(1f-Mood)*1.5f);
    }

    private AudioStream? GetClip(string path)
    {
        if(_clips.TryGetValue(path,out var hit)) {hit.Access=++_access;return hit.Stream;}
        // WAV file size bounds PCM without loading an unbounded stream first.
        using var file=FileAccess.Open(path,FileAccess.ModeFlags.Read);
        var bytes=checked((int)file.GetLength());
        if(bytes>PcmBudget || bytes>600_000) throw new InvalidOperationException("Household clip exceeded budget: "+path);
        while(_clips.Count>=ClipBudget || _clipBytes+bytes>PcmBudget)
        {
            var oldest=_clips.Where(c=>!_voices.Any(v=>ReferenceEquals(v.Stream,c.Value.Stream)))
                .OrderBy(c=>c.Value.Access).FirstOrDefault();
            if(oldest.Value is null) return null;
            _clipBytes-=oldest.Value.Bytes;_clips.Remove(oldest.Key);oldest.Value.Stream.Dispose();
        }
        var stream=ResourceLoader.Load<AudioStreamWav>(path,cacheMode:ResourceLoader.CacheMode.Ignore);
        stream.LoopMode=AudioStreamWav.LoopModeEnum.Disabled;
        _clips.Add(path,new Clip(stream,bytes,++_access));_clipBytes+=bytes;return stream;
    }

    private void ReleaseVoice(int slot)
    {
        _voices[slot].Stop();_voices[slot].Stream=null;_voiceHouses[slot]=-1;
    }
    private void StopVoices()
    {
        for(var i=0;i<VoiceBudget;i++) if(_voiceHouses[i]>=0) ReleaseVoice(i);
    }
    public override void _ExitTree()
    {
        if(_voices[0] is null)return;
        StopVoices();foreach(var clip in _clips.Values)clip.Stream.Dispose();_clips.Clear();_clipBytes=0;
    }
    private sealed record Window(MeshInstance3D Pane,Vector3 Centre,Vector3 Normal,string Path);
    private sealed class House(Node3D owner,string identity,Vector3 inside,Vector3 outside)
    {
        public Node3D Owner=owner;public string Identity=identity;public Vector3 Inside=inside,Outside=outside;
        public int Motif;public double Wait;
    }
    private sealed class Clip(AudioStream stream,int bytes,long access)
    {
        public AudioStream Stream=stream;public int Bytes=bytes;public long Access=access;
    }
    private sealed record Catalog(Motif[] Events);
    private sealed record Motif(string Id,string Label,string File,bool Outdoor,bool DayOnly,float Radius,float VolumeDb,float MinWait,float MaxWait,bool NightOnly=false);
}
