using System;
using System.Text.Json;
using Godot;
using Urman.Core.Narrative;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private VillageHouseholdDirector? _households;
    private WeakReference<object>? _lastRestoredHouseholdAudioSession;
    private string? _lastRestoredHouseholdAudioTicketId;
    private JsonElement? _pendingHouseholdAudioSchedule;
    private WeakReference<object>? _pendingHouseholdAudioSession;
    private string? _pendingHouseholdAudioTicketId;
    private string? _pendingHouseholdAudioFailure;
    private ulong _nextPendingHouseholdAudioRetryUsec;

    private RuntimeBridge? HouseholdRuntimeBridge => _runtimeBridge is { } current && GodotObject.IsInstanceValid(current)
        ? current
        : GetTree()?.GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;

    internal bool TryCaptureHouseholdAudioSchedule(out JsonElement snapshot)
    {
        snapshot = default;
        if (!IsInsideTree() || IsQueuedForDeletion()) return false;
        if (_pendingHouseholdAudioSchedule is { } pending)
        {
            if (_pendingHouseholdAudioSession is { } pendingSession && pendingSession.TryGetTarget(out var owner)
                && ReferenceEquals(HouseholdRuntimeBridge?.SessionIdentity, owner))
            {
                snapshot = pending.Clone();
                return true;
            }
            ClearPendingHouseholdAudioSchedule();
        }
        if (_households is not { } households || !GodotObject.IsInstanceValid(households)
            || !households.IsInsideTree() || households.IsQueuedForDeletion()
            || !households.IsScheduleInitialized) return false;
        snapshot = households.CaptureScheduleSnapshot();
        return true;
    }

    internal bool TryRestoreHouseholdAudioSchedule(object session, string ticketId, JsonElement snapshot)
    {
        var bridge = HouseholdRuntimeBridge;
        if (session is null || !ReferenceEquals(bridge?.SessionIdentity, session)
            || string.IsNullOrWhiteSpace(ticketId) || !IsInsideTree() || IsQueuedForDeletion()) return false;
        PhotoWorldState.ValidateHouseholdAudioSnapshot(snapshot);
        if (_lastRestoredHouseholdAudioSession is { } restoredSessionReference
            && restoredSessionReference.TryGetTarget(out var restoredSession)
            && ReferenceEquals(restoredSession, session)
            && _lastRestoredHouseholdAudioTicketId == ticketId) return true;
        if (_pendingHouseholdAudioFailure is not null
            && _pendingHouseholdAudioSession is { } failedSession
            && failedSession.TryGetTarget(out var failedOwner) && ReferenceEquals(failedOwner, session)
            && _pendingHouseholdAudioTicketId == ticketId
            && Time.GetTicksUsec() < _nextPendingHouseholdAudioRetryUsec) return false;
        if (_households is { } households && GodotObject.IsInstanceValid(households)
            && households.IsInsideTree() && !households.IsQueuedForDeletion() && households.IsScheduleInitialized)
            return ApplyHouseholdAudioSchedule(session, ticketId, snapshot, households);

        _pendingHouseholdAudioSchedule = snapshot.Clone();
        _pendingHouseholdAudioSession = new WeakReference<object>(session);
        _pendingHouseholdAudioTicketId = ticketId;
        _pendingHouseholdAudioFailure = null;
        return true;
    }

    private bool ApplyHouseholdAudioSchedule(
        object session, string ticketId, JsonElement snapshot, VillageHouseholdDirector households)
    {
        try
        {
            households.RestoreScheduleSnapshot(snapshot);
            _lastRestoredHouseholdAudioSession = new WeakReference<object>(session);
            _lastRestoredHouseholdAudioTicketId = ticketId;
            if (_pendingHouseholdAudioSession is { } pendingSession && pendingSession.TryGetTarget(out var pendingOwner)
                && ReferenceEquals(pendingOwner, session))
                ClearPendingHouseholdAudioSchedule();
            _pendingHouseholdAudioFailure = null;
            return true;
        }
        catch (Exception error)
        {
            _pendingHouseholdAudioSchedule = snapshot.Clone();
            _pendingHouseholdAudioSession = new WeakReference<object>(session);
            _pendingHouseholdAudioTicketId = ticketId;
            if (_pendingHouseholdAudioFailure != error.Message)
                GD.PushError($"Household audio schedule restore remains pending: {error.Message}");
            _pendingHouseholdAudioFailure = error.Message;
            _nextPendingHouseholdAudioRetryUsec = Time.GetTicksUsec() + 1_000_000;
            return false;
        }
    }

    private void ApplyPendingHouseholdAudioSchedule()
    {
        if (_pendingHouseholdAudioSchedule is not { } snapshot
            || _pendingHouseholdAudioSession is not { } pendingSession
            || !pendingSession.TryGetTarget(out var session)
            || !ReferenceEquals(HouseholdRuntimeBridge?.SessionIdentity, session)
            || _households is not { } households || !households.IsScheduleInitialized) return;
        ApplyHouseholdAudioSchedule(session, _pendingHouseholdAudioTicketId ?? string.Empty, snapshot, households);
    }

    private void RetryPendingHouseholdAudioSchedule()
    {
        if (_pendingHouseholdAudioSchedule is null || Time.GetTicksUsec() < _nextPendingHouseholdAudioRetryUsec) return;
        _nextPendingHouseholdAudioRetryUsec = Time.GetTicksUsec() + 1_000_000;
        ApplyPendingHouseholdAudioSchedule();
    }

    private void ClearPendingHouseholdAudioSchedule()
    {
        _pendingHouseholdAudioSchedule = null;
        _pendingHouseholdAudioSession = null;
        _pendingHouseholdAudioTicketId = null;
        _pendingHouseholdAudioFailure = null;
    }

    internal bool TryTransferHouseholdAudioScheduleTo(Act1ConnectedWorld target, object session)
    {
        if (!TryCaptureHouseholdAudioSchedule(out var snapshot)) return true;
        return target.TryRestoreHouseholdAudioSchedule(session,
            $"home-retry:{_pendingHouseholdAudioTicketId ?? _lastRestoredHouseholdAudioTicketId ?? "snapshot"}", snapshot);
    }

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
        ApplyPendingHouseholdAudioSchedule();
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
        RetryPendingHouseholdAudioSchedule();
        if(_households is null || LifePlayer() is not { } player)return;
        var camera=GetViewport().GetCamera3D();
        var contextAudible=_physicalInterior.Length==0 && !player.ModalOpen && !player.SessionTransitionActive
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
        // Inaudible Tick still advances the director's clock. A transition
        // must also preserve its remaining deadline and fractional tick.
        _households.Tick(player.SessionTransitionActive ? 0d : delta,
            camera?.GlobalPosition??player.GlobalPosition,player.GlobalPosition,villageAudible,night);
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
