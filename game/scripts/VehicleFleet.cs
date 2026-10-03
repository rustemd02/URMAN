using Godot;
using System.Text.Json.Nodes;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Presentation coordinator for the authored fleet. RuntimeBridge's existing
/// world.props and SaveGameV3 remain the sole persistence owner.
/// </summary>
public partial class VehicleFleet : Node3D
{
    public const string DefinitionPath="res://content/vehicles/act1_vehicles.v1.json";
    public const string FleetStateId="vehicle/fleet";
    private readonly List<VehicleController> _vehicles=new();
    private Act1ConnectedWorld _world = null!;
    private RuntimeBridge? _bridge;
    private FirstPersonController? _player;
    private PauseMenuUi? _pauseMenu;
    private SettingsUi? _settingsUi;
    private object? _session;
    private bool _dirty;
    private bool _saving;
    private bool _loading;
    private int _projectionFrames;
    private bool _sessionProjectionPending;
    private bool _exteriorGeometryEnabled;
    private bool _placementValidationDeferred=true;
    private string? _occupiedIdToRestore;
    private bool _restorePlayerHeld;
    private string? _projectionFailure;
    private Vector3? _lastStandingFeet;
    private double _commitDelay;
    private Task<bool> _pending=Task.FromResult(true);
    private Task<bool>? _flushTask;
    private float _textScale=-1;
    private Label _hint=null!;
    private CanvasLayer _hud=null!;
    public IReadOnlyList<VehicleController> Vehicles=>_vehicles;
    // Same first-match order as _vehicles.FirstOrDefault(vehicle=>vehicle.Driver is not null),
    // without the LINQ enumerator allocation on every HUD read.
    public VehicleController? Occupied
    {
        get
        {
            foreach(var vehicle in _vehicles)if(vehicle.Driver is not null)return vehicle;
            return null;
        }
    }
    public bool Suspended { get; private set; }
    public string? ProjectionFailure=>_projectionFailure;
    public bool PlacementValidationDeferred=>_placementValidationDeferred;

    public void Configure(Act1ConnectedWorld world){_world=world;Name="VillageVehicles";}

    /// <summary>The connected world calls this after changing its actual collision presentation.</summary>
    public void SetZonePresentation(string zoneId,bool exteriorEnabled)
    {
        if(_exteriorGeometryEnabled==exteriorEnabled)return;
        _exteriorGeometryEnabled=exteriorEnabled;
        _placementValidationDeferred=true;
        foreach(var vehicle in _vehicles)
        { vehicle.SetPlacementAvailability(false);vehicle.Radio?.SetPaused(true); }
        // Keep the live parking, power and radio state. A zone change is not a
        // saved-session projection and must never reset a vehicle to its snapshot.
        if(exteriorEnabled)
        {
            if(_session is not null&&_projectionFailure is null)_projectionFrames=2;
        }
        else if(!_sessionProjectionPending)_projectionFrames=0;
        SetMeta("placementPresentationZone",zoneId);
    }

    public override void _Ready()
    {
        AddToGroup("vehicle_fleet");
        _hud=new CanvasLayer{Name="VehicleHud",Layer=14};AddChild(_hud);
        _hint=new Label{Name="VehicleControls",HorizontalAlignment=HorizontalAlignment.Center,
            VerticalAlignment=VerticalAlignment.Bottom,MouseFilter=Control.MouseFilterEnum.Ignore,
            AutowrapMode=TextServer.AutowrapMode.WordSmart};
        _hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        _hint.OffsetLeft=24;_hint.OffsetRight=-24;_hint.OffsetTop=-160;_hint.OffsetBottom=-22;
        _hint.AddThemeFontSizeOverride("font_size",20);
        _hint.AddThemeColorOverride("font_color",new Color(.96f,.92f,.78f));
        _hint.AddThemeColorOverride("font_outline_color",Colors.Black);
        _hint.AddThemeConstantOverride("outline_size",4);_hud.AddChild(_hint);_hud.Visible=false;
        foreach(var definition in VehicleDefinition.Load(DefinitionPath))
        {
            // Geometry is prepared before its first tree entry. Parking uses its
            // actual wheel/hoof bindings, also consumed by restore validation.
            var vehicle=new VehicleController();vehicle.Configure(this,definition);
            vehicle.GroundAuthoredSpawn(point=>AgentBAct1HeightField.CollisionGround(point.X,point.Z));
            _vehicles.Add(vehicle);AddChild(vehicle);
        }
        AttachBridge();
    }

    private void AttachBridge()
    {
        if(_bridge is not null&&GodotObject.IsInstanceValid(_bridge))return;
        _bridge=GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if(_bridge is not null)_bridge.PlayTimeBoundary+=OnBoundary;
    }

    private void OnBoundary(string kind)
    {if(kind=="load-start")_loading=true;else if(kind is "load-restored" or "load-failed")_loading=false;}

    public override void _PhysicsProcess(double delta)
    {
        AttachBridge();
        // Cached tree lookups: each of these groups holds one authored node, so
        // a valid node still inside the tree is what GetFirstNodeInGroup would
        // return. The player body is rebuilt with the world on a session change,
        // so validity and tree membership are rechecked every tick.
        if(_player is null||!GodotObject.IsInstanceValid(_player)||!_player.IsInsideTree())
            _player=GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if(_pauseMenu is null||!GodotObject.IsInstanceValid(_pauseMenu)||!_pauseMenu.IsInsideTree())
            _pauseMenu=GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi;
        if(_settingsUi is null||!GodotObject.IsInstanceValid(_settingsUi)||!_settingsUi.IsInsideTree())
            _settingsUi=GetTree().GetFirstNodeInGroup("settings_ui") as SettingsUi;
        if(_bridge?.ProjectionSessionIdentity is {} session&&!ReferenceEquals(_session,session))
            ProjectSession(session);
        else if(_projectionFrames>0&&--_projectionFrames==0)
            FinishSessionProjection();
        // GetNodesInGroup allocated a native Array<Node> plus a LINQ iterator on
        // every physics tick. One MainMenuUi is authored and Dismiss() queue-frees
        // it, so an empty group or a live un-dismissed first node already decides
        // this. The full enumeration remains as the fallback for the frame where
        // a dismissed node is still in the tree or a non-menu node shares the
        // group, preserving "at least one un-dismissed MainMenuUi".
        var menu=MainMenuUi.AnyUndismissed(GetTree());
        var demo=_bridge?.GetParent()?.GetParent() as Act1DemoRoot;
        Suspended=_saving||_loading||_projectionFrames>0||_projectionFailure is not null||menu||demo?.DemoEnded==true
            ||_pauseMenu is {IsOpen:true}
            ||_settingsUi is {IsOpen:true};
        var hudVisible=!Suspended&&Occupied is not null&&_player?.ModalOpen==false;
        if(_hud.Visible!=hudVisible)_hud.Visible=hudVisible;
        if(_hud.Visible)
        {
            var scale=(float)(_player?.Accessibility.TextScale??1);
            if(!Mathf.IsEqualApprox(scale,_textScale))
            {
                _textScale=scale;_hint.AddThemeFontSizeOverride("font_size",Mathf.RoundToInt(20*scale));
                _hint.OffsetTop=-165*scale;
            }
            _hint.Text=Occupied!.ControlHint();
        }
        if(Suspended)return;
        _commitDelay+=delta;
        if(_dirty&&_pending.IsCompleted&&_commitDelay>=2)
        { _commitDelay=0;_dirty=false;_pending=CommitAsync(); }
    }

    private void ProjectSession(object session)
    {
        if(_restorePlayerHeld&&_player is not null)_player.SetVehicleControl(false);
        _projectionFailure=null;_restorePlayerHeld=false;
        _sessionProjectionPending=true;_placementValidationDeferred=true;
        foreach(var vehicle in _vehicles)
        { vehicle.Radio?.SetPaused(true);vehicle.ResetAuthored(); }
        _session=session;_dirty=false;_commitDelay=0;_pending=Task.FromResult(true);
        var props=_bridge!.SelectWorldProps();
        foreach(var vehicle in _vehicles)
            if(props.TryGetProperty(vehicle.Definition.StateId,out var record)&&!vehicle.Restore(record))
                GD.PushWarning("Invalid saved vehicle pose ignored for "+vehicle.Definition.Id+"; authored parking retained.");
        _occupiedIdToRestore=null;
        if(props.TryGetProperty(FleetStateId,out var fleet)
            &&fleet.TryGetProperty("occupiedId",out var occupied)&&occupied.ValueKind==System.Text.Json.JsonValueKind.String)
            _occupiedIdToRestore=occupied.GetString();
        if(_exteriorGeometryEnabled&&_occupiedIdToRestore is not null&&_player is not null)
        {
            // The portable save contains seated feet. Disable the pedestrian
            // capsule before allowing any physics frame at that temporary pose.
            _player.SetVehicleControl(true);_restorePlayerHeld=true;
        }
        // All saved transforms are applied before querying any of them. Waiting
        // for physics avoids testing against the previous session's body poses.
        _projectionFrames=2;
        // Re-projecting does not create a new runtime effect or duplicate any object.
        _dirty=false;
    }

    private void FinishSessionProjection()
    {
        if(!_exteriorGeometryEnabled)
        {
            // Exterior terrain is intentionally absent from the physics space
            // indoors. No support query or fallback parking is meaningful here.
            _placementValidationDeferred=true;
            if(_occupiedIdToRestore is not null)
            {
                _projectionFailure="An occupied vehicle cannot be restored while exterior collision geometry is disabled.";
                GD.PushError(_projectionFailure);
            }
            _occupiedIdToRestore=null;
            if(_sessionProjectionPending)_dirty=false;
            _sessionProjectionPending=false;
            return;
        }
        foreach(var vehicle in _vehicles)
        {
            if(vehicle.ValidatePhysicalPlacement(out var reason))
            { vehicle.SetPlacementAvailability(true);continue; }
            vehicle.SetMeta("rejectedPlacementProbe",vehicle.DescribePlacementProbe(vehicle.GlobalTransform).ToJsonString());
            var recovered=vehicle.TryRestoreAuthoredParking();
            vehicle.SetMeta("rejectedSavedPlacement",reason);
            vehicle.SetMeta("restoredParkingRecovery",recovered);
            vehicle.SetPlacementAvailability(recovered,reason);
            if(recovered&&!_sessionProjectionPending)MarkDirty();
            GD.PushWarning("Vehicle "+vehicle.Definition.Id+" placement rejected: "+reason
                +(recovered?"; recovered to physically checked parking.":"; no clear authored parking was found."));
        }
        _placementValidationDeferred=false;
        if(_player is not null&&_occupiedIdToRestore is {} id)
        {
            var requested=_vehicles.FirstOrDefault(vehicle=>vehicle.Definition.Id==id);
            var entered=requested is {PlacementAvailable:true}&&requested.Enter(_player,restore:true);
            if(!entered&&_restorePlayerHeld)
            {
                if(TryFindRecoveryStanding(_player,requested?.GlobalPosition??_player.GlobalPosition,out var feet))
                {
                    _player.ApplyZoneSpawn(feet,_player.RotationDegrees.Y);
                    _player.SetVehicleControl(false);_lastStandingFeet=feet;
                    _player.NotifyTraversal("Транспорт зажат. Вы стоите рядом в свободном месте.");
                }
                else
                {
                    // The save owner must reject and roll back this projection.
                    // Never turn collision back on inside the rejected chassis.
                    _projectionFailure="The saved vehicle and all checked standing recovery positions are blocked.";
                    GD.PushError(_projectionFailure);
                }
            }
        }
        if(_projectionFailure is null)_restorePlayerHeld=false;
        _occupiedIdToRestore=null;
        if(_sessionProjectionPending)_dirty=false;
        _sessionProjectionPending=false;
    }

    /// <summary>Await from the existing save owner after applying its portable transform.</summary>
    public async Task<bool> CompleteLoadedProjectionAsync()
    {
        if(_bridge?.ProjectionSessionIdentity is not {} session)return false;
        _player=GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        try
        {
            if(!ReferenceEquals(_session,session))ProjectSession(session);
            while(_projectionFrames>0&&ReferenceEquals(session,_bridge.ProjectionSessionIdentity))
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            return ReferenceEquals(session,_bridge.ProjectionSessionIdentity)&&_projectionFailure is null;
        }
        finally
        {
            // The save owner's awaiting continuation rolls back a false result
            // before the next physics frame. Never leak a projection-only hold
            // into a later new game, failed read, or independent load request.
            if(ReferenceEquals(_session,session)&&_restorePlayerHeld&&Occupied is null&&_player is not null)
            { _player.SetVehicleControl(false);_restorePlayerHeld=false; }
        }
    }

    internal bool TryFindRecoveryStanding(FirstPersonController player,Vector3 anchor,out Vector3 feet)
    {
        feet=default;
        foreach(var candidate in RecoveryStandingCandidates(anchor,_bridge?.CurrentZoneId))
            if(TryResolveRecoveryStandingCandidate(player,candidate,out feet))return true;
        return false;
    }

    // Shared diagnostic enumeration: tests place real obstacles at these same
    // authored candidates. This does not override a query or force an outcome.
    internal IReadOnlyList<Vector3> RecoveryStandingCandidates(Vector3 anchor,string? zone)
    {
        var candidates=new List<Vector3>();
        var anchors=new List<Vector3>{anchor};
        if(_lastStandingFeet is {} previous)anchors.Add(previous);
        if(zone is not null&&(_world.TryGetWorldSpawn(zone,"entry",out var entry)
            ||_world.TryGetWorldSpawn(zone,"default",out entry)))anchors.Add(entry.Position);
        foreach(var centre in anchors)
        foreach(var radius in new[]{0f,1f,1.5f,2f,3f,4f,6f,8f,12f,16f})
        for(var direction=0;direction<(radius==0?1:16);direction++)
        {
            var angle=direction*Mathf.Tau/16;
            var candidate=centre+new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius);
            // The shared Foot graph answers geometry, not story permission.
            // Recovery must stay on the already available side of the forest
            // approach, and must not advance beyond the vehicle stopping point.
            if(candidate.Z < -118.5f||(candidate.Z < -91.5f&&zone!="kara_urman_night"))continue;
            candidate.Y=AgentBAct1HeightField.CollisionGround(candidate.X,candidate.Z);
            if(!IsWithinTerrain(candidate))continue;
            if(!candidates.Any(existing=>existing.DistanceSquaredTo(candidate)<.0001f))candidates.Add(candidate);
        }
        return candidates;
    }

    internal bool TryResolveRecoveryStandingCandidate(FirstPersonController player,Vector3 candidate,out Vector3 feet)
    {
        feet=default;
        using var ray=PhysicsRayQueryParameters3D.Create(candidate+Vector3.Up*1.5f,candidate-Vector3.Up*.8f,3);
        var recoveryExclude=new global::Godot.Collections.Array<Rid>{player.GetRid()};
        using var recoveryExcludeOwner=(global::Godot.Collections.Array)recoveryExclude;
        ray.Exclude=recoveryExclude;
        using var hit=GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if(hit.Count==0||hit["normal"].AsVector3().Y<.82f)return false;
        candidate=hit["position"].AsVector3()+Vector3.Up*.035f;
        // Do not recover onto the roof of the blocked vehicle or fixture.
        if(Math.Abs(candidate.Y-AgentBAct1HeightField.CollisionGround(candidate.X,candidate.Z))>.45f)return false;
        if(!_world.CanVehicleTraverse(candidate,candidate,SettlementTravelMode.Foot,out _)
            ||!player.CanStandAt(candidate))return false;
        feet=candidate;return true;
    }

    private bool IsExterior=>_bridge?.CurrentZoneId is "village_day" or "zirat_road" or "kara_urman_night";

    public bool TryEnter(VehicleController vehicle)
    {
        if(_player is null||_bridge?.SessionIdentity is null||Suspended||!IsExterior||!_exteriorGeometryEnabled
            ||_projectionFrames>0||_placementValidationDeferred||Occupied is not null)return false;
        if(!_vehicles.Contains(vehicle)||_player.GlobalPosition.DistanceTo(vehicle.EntryTarget.GlobalPosition)>2.8f)return false;
        if(!vehicle.PlacementAvailable)
        {
            // A later removal of the actual obstruction can make the same saved
            // vehicle usable again; no item, progress or power state is recreated.
            if(!vehicle.ValidatePhysicalPlacement(out var reason))
            { _player.NotifyTraversal("Транспорт зажат. Сейчас сесть в него нельзя.");return false; }
            vehicle.SetPlacementAvailability(true);
        }
        if(_player.CanStandAt(_player.GlobalPosition))_lastStandingFeet=_player.GlobalPosition;
        return vehicle.Enter(_player);
    }

    public VehicleTravelDecision EvaluateTravel(VehicleController vehicle,Vector3 from,Vector3 to)
    {
        if(!IsWithinTerrain(to))return new(false,"Здесь нет проезда.");
        // Existing scene transitions stay explicit. Driving never dispatches a quest,
        // unlocks a gate, discovers a clue, or silently switches the active chapter.
        if(to.Z < -91.5f && to.Z < from.Z && _bridge?.CurrentZoneId!="kara_urman_night")
            return new(false,"Дальше лесная дорога. Остановитесь у указателя и осмотрите путь.");
        if(to.Z < -118.5f && to.Z < from.Z)
            return new(false,vehicle.Definition.Kind==VehicleKind.HorseCart
                ?"Лошадь дальше не идёт. Здесь можно развернуться или сойти."
                :"Дальше узкая тропа. Здесь можно оставить транспорт.");
        var mode=vehicle.Definition.Kind switch {VehicleKind.Niva=>SettlementTravelMode.Car,
            VehicleKind.Motorcycle=>SettlementTravelMode.Motorcycle,_=>SettlementTravelMode.HorseCart};
        if(!_world.CanVehicleTraverse(from,to,mode,out var reason))return new(false,reason);
        return new(true,string.Empty,to.Z < -89? .72f:1f);
    }

    public HorseDisposition HorseMoodAt(Vector3 position)
    {
        if(_bridge?.CurrentZoneId!="kara_urman_night")return HorseDisposition.Calm;
        return position.Z < -116 ? HorseDisposition.Refusing : position.Z < -110 ? HorseDisposition.Slowing
            : position.Z < -98 ? HorseDisposition.Wary : HorseDisposition.Calm;
    }

    public bool IsWithinTerrain(Vector3 position)=>float.IsFinite(position.X)&&float.IsFinite(position.Y)&&float.IsFinite(position.Z)
        &&position.X>AgentBAct1HeightField.MinX+3&&position.X<AgentBAct1HeightField.MaxX-3
        &&position.Z>AgentBAct1HeightField.MinZ+3&&position.Z<AgentBAct1HeightField.MaxZ-3
        &&Math.Abs(position.Y-AgentBAct1HeightField.CollisionGround(position.X,position.Z))<2.5f;

    public void MarkDirty()=>_dirty=true;

    private JsonArray Capture()
    {
        var records=new JsonArray();
        foreach(var vehicle in _vehicles)records.Add(vehicle.Capture());
        records.Add(new JsonObject{["propId"]=FleetStateId,["version"]=1,["occupiedId"]=Occupied?.Definition.Id});
        return records;
    }

    private async Task<bool> CommitAsync()
    {
        var bridge=_bridge;var session=bridge?.SessionIdentity;if(bridge is null||session is null)return false;
        try
        {
            var result=await bridge.DispatchWorldPropsAsync(Capture());
            return result&&ReferenceEquals(session,bridge.SessionIdentity);
        }
        catch(Exception exception)
        {
            if(ReferenceEquals(session,bridge.SessionIdentity))GD.PushError("Vehicle state commit failed: "+exception.Message);
            return false;
        }
    }

    /// <summary>Call from RuntimeBridge.SaveSlotAsync before capturing its one snapshot.</summary>
    public Task<bool> FlushForSaveAsync()
    {
        if(_flushTask is {IsCompleted:false})return _flushTask;
        return _flushTask=FlushForSaveInnerAsync();
    }

    private async Task<bool> FlushForSaveInnerAsync()
    {
        if(_bridge?.SessionIdentity is not {} session)return true;
        _saving=true;
        try
        {
            if(!ReferenceEquals(session,_session))
            {
                _player=GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
                ProjectSession(session);
            }
            while(_projectionFrames>0&&ReferenceEquals(session,_bridge.SessionIdentity))
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            if(!await _pending||!ReferenceEquals(session,_bridge.SessionIdentity)||_projectionFailure is not null)return false;
            var result=await CommitAsync();
            if(result)_dirty=false;
            return result;
        }
        finally{_saving=false;}
    }

    public override void _ExitTree()
    {
        if(_bridge is not null&&GodotObject.IsInstanceValid(_bridge))_bridge.PlayTimeBoundary-=OnBoundary;
        foreach(var vehicle in _vehicles)if(GodotObject.IsInstanceValid(vehicle))vehicle.ReleaseForSessionBoundary();
    }
}
