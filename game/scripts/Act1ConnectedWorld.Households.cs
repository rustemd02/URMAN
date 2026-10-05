using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private VillageHouseholdDirector? _households;
    private VillageSoundMoodDirector? _soundMood;
    private ForestEdgePresence? _forestEdge;
    private ClubGramophone? _gramophone;
    private VillagePaSystem? _paSystem;
    private WindowSilhouettes? _silhouettes;
    private MosqueSanctuary? _mosqueSanctuary;
    private VillageChimneySmoke? _chimneySmokeSystem;
    private void BuildVillageHouseholds()
    {
        _households=new VillageHouseholdDirector {Name="InhabitedVillage"};
        AddChild(_households);_households.Initialize(this);
        _soundMood=new VillageSoundMoodDirector {Name="VillageSoundMood"};
        AddChild(_soundMood);
        _soundMood.Initialize(_households,GetTree()?.GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector);
        _forestEdge=new ForestEdgePresence {Name="ForestEdgePresence"};
        AddChild(_forestEdge);_forestEdge.Initialize(this);
        _gramophone=new ClubGramophone {Name="ClubGramophone"};
        AddChild(_gramophone);_gramophone.Initialize(this);
        _paSystem=new VillagePaSystem {Name="VillagePaSystem"};
        AddChild(_paSystem);_paSystem.Initialize(this);
        _silhouettes=new WindowSilhouettes {Name="WindowSilhouettes"};
        AddChild(_silhouettes);_silhouettes.Initialize(this);
        _mosqueSanctuary=new MosqueSanctuary {Name="MosqueSanctuary"};
        AddChild(_mosqueSanctuary);_mosqueSanctuary.Initialize(this);
        _chimneySmokeSystem=new VillageChimneySmoke {Name="VillageChimneySmoke"};
        AddChild(_chimneySmokeSystem);
        // The authored village-life plumes already smoke their own two chimneys;
        // handing that root over lets the pool skip those recorded source paths,
        // so no chimney ever carries two emitters.
        _chimneySmokeSystem.Initialize(this,_households,_villageLife,
            GetViewport().GetCamera3D()?.GlobalPosition??LifePlayer()?.GlobalPosition??Vector3.Zero);
    }
    private void UpdateVillageHouseholds(double delta)
    {
        if(_households is null || LifePlayer() is not { } player)return;
        var camera=GetViewport().GetCamera3D();
        var contextAudible=_physicalInterior.Length==0 && !player.ModalOpen
            && _lifeCue?.IsPresenting!=true && !GetTree().Paused && !ArrivalFlyoverCameraActive();
        // The Kara night zone is the authored night state of the same village,
        // so it gets its own audible path instead of the director going fully
        // silent there: night-only motifs and the VillageSoundMoodDirector
        // night levels become reachable, while interiors, the mosque, dialogue,
        // pause and the flyover still cut both zones. The day zone keeps
        // night=false, so dayOnly filtering stays honest.
        var dayAudible=ActiveZoneId=="village_day" && contextAudible;
        var night=ActiveZoneId=="kara_urman_night";
        var villageAudible=dayAudible || (night && contextAudible);
        _households.Tick(delta,camera?.GlobalPosition??player.GlobalPosition,player.GlobalPosition,villageAudible,night);
        _soundMood?.Tick(delta,ActiveZoneId,villageAudible);
        // Smoke follows the zone gate, not the audio gate: it stays alive during
        // dialogue and the arrival flyover, and freezes under reduced motion
        // exactly like the authored village-life plumes already do.
        _chimneySmokeSystem?.Tick(delta,camera?.GlobalPosition??player.GlobalPosition,
            ActiveZoneId=="village_day",!player.ReducedMotion);
        var forestAudible=(ActiveZoneId is "village_day" or "kara_urman_night")
            && _physicalInterior.Length==0 && !player.ModalOpen && _lifeCue?.IsPresenting!=true
            && !GetTree().Paused && !ArrivalFlyoverCameraActive();
        _forestEdge?.Tick(delta,player.GlobalPosition,forestAudible);
        var clubAudible=ActiveZoneId=="village_day" && !player.ModalOpen && _lifeCue?.IsPresenting!=true
            && !GetTree().Paused && !ArrivalFlyoverCameraActive();
        _gramophone?.Tick(delta,player.GlobalPosition,clubAudible);
        // Indoors goes to the PA separately: the square horns must stop in the
        // mosque/bath/school while the club's own speakers stay an indoor
        // exception (VillagePaSystem.Tick decides by mode).
        _paSystem?.Tick(delta,player.GlobalPosition,clubAudible,_physicalInterior.Length>0);
        _silhouettes?.Tick(delta,camera?.GlobalPosition??player.GlobalPosition,dayAudible);
        _mosqueSanctuary?.Tick(delta,player.GlobalPosition,FacilityInteriorAt(player.GlobalPosition)=="mosque");
    }
}
