using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>Ordinary New Game, physical import observations, then an explicitly
/// diagnostic local camera/read fixture. This is not an unfamiliar-player search.</summary>
public partial class AddressWorldSmokeTest : Node
{
    private Act1DemoRoot? _demo;
    private Act1ConnectedWorld _world=null!;
    private FirstPersonController _player=null!;
    private RuntimeBridge _bridge=null!;
    private string _output="";
    private readonly List<object> _checks=[];
    private readonly List<object> _mounts=[];
    private readonly List<object> _captures=[];
    private readonly List<string> _failures=[];
    private JsonElement? _registryEvidence;
    private bool _auditCompleted;
    private bool _auditRun=true;
    private string _scope="full";

    /// <summary>The game pauses itself when its window loses focus. These native walks run for minutes
    /// while other applications take focus, so the fixture resumes as a player returning to the window
    /// would; the pause is otherwise untouched and remains covered by its own tests.</summary>
    public override void _Process(double delta)
    {
        if(GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi{IsOpen:true} pause)
        {
            DisplayServer.WindowMoveToForeground();
            pause.Resume();
        }
    }

    public override async void _Ready()
    {
        var exit=1;
        AddressAccessVerifier? suspendedAudit=null;
        var auditWasProcessing=false;
        try
        {
            _scope=System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_SCOPE")??"full";
            if(_scope is not ("full" or "remediation-search" or "entrance-candidates" or "standalone-candidates" or "standalone-access" or "production-entrances" or "verge-contacts" or "h045-layout" or "frontage-sightlines" or "crowded-addresses" or "far-bank"))throw new InvalidOperationException("Unknown address smoke scope: "+_scope);
            _output=System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_OUTPUT")??"";
            if(!Path.IsPathFullyQualified(_output)||!Directory.Exists(_output)||Directory.EnumerateFileSystemEntries(_output).Any())
                throw new InvalidOperationException("URMAN_ADDRESS_OUTPUT must name an existing empty absolute evidence directory.");
            if(DisplayServer.GetName()=="headless")throw new InvalidOperationException("Address world acceptance requires native rendered frames.");
            _demo=ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(8);
            Require(await this.StartThroughMainMenuAsync(_demo),"ordinary New Game starts");
            // The ordinary game opens with the forest prologue; the production skip input
            // hands over to the village before any address fixture is placed.
            DisplayServer.WindowMoveToForeground();
            _demo._Input(new InputEventAction{Action="ui_cancel",Pressed=true});
            for(var frame=0;frame<600&&_demo.PrologueActive;frame++)await Frames(1);
            await Frames(8);
            Require(!_demo.PrologueActive&&!_demo.IntroVisible,"production skip input completes the prologue transition");
            for(var frame=0;frame<300&&_demo.MainMenuVisible;frame++)await Frames(1);
            await Frames(8);
            _world=_demo.DemoMain.ConnectedWorld??throw new InvalidOperationException("The ordinary world was not created.");
            _player=_demo.DemoMain.GetNode<FirstPersonController>("Player");
            _bridge=(RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            var registry=_world.AddressRegistry??throw new InvalidOperationException("No imported address registry.");
            Check(!_bridge.SelectWorldProps().EnumerateObject().Any(p=>p.Name.StartsWith("address/",StringComparison.Ordinal)),"construction and proximity do not record a plate read");

            if(_scope is "remediation-search" or "entrance-candidates" or "standalone-candidates" or "standalone-access" or "production-entrances" or "verge-contacts" or "h045-layout" or "frontage-sightlines" or "crowded-addresses" or "far-bank")
            {
                // Scope is confined to this explicit smoke scene. The ordinary
                // runtime lifecycle and live address records are not modified.
                suspendedAudit=_world.GetNodeOrNull<AddressAccessVerifier>("AddressAccessVerification")
                    ??throw new InvalidOperationException("Missing actual address audit child.");
                auditWasProcessing=suspendedAudit.IsPhysicsProcessing();
                suspendedAudit.SetPhysicsProcess(false);
                _auditRun=false;
                _checks.Add(new{kind="audit-not-run-in-narrow-scope",auditRun=false,
                    constructionEntriesAlreadyObserved=registry.AccessPoints.Values.Count(a=>a.State!="pending-physics"),
                    previousPhysicsProcessing=auditWasProcessing,acceptance=false});
                if(_scope is "crowded-addresses" or "far-bank")
                {
                    var selectedIds=_scope=="far-bank" ? CheckFarBankBindings(registry) :
                        (System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_IDS")??"ADR-H013,ADR-H041")
                        .Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                    Require((_scope=="far-bank" ? selectedIds.Length==35 : selectedIds.Length is >0 and <=4)
                        && selectedIds.Distinct().Count()==selectedIds.Length && selectedIds.All(registry.Addresses.ContainsKey),
                        "selected existing, distinct address IDs match the requested diagnostic scope");
                    foreach(var id in selectedIds)
                    {
                        var sign=Descendants(_world).OfType<AddressSignVisualComponent>().Single(n=>n.AddressId==id);
                        GD.Print($"address-world: {id} start prologue={_demo.PrologueActive} intro={_demo.IntroVisible} menu={_demo.MainMenuVisible} modal={_player.ModalOpen} zone={_bridge.CurrentZoneId} at={_player.GlobalPosition}");
                        RecordMount(sign,registry);
                        var accessId=registry.Addresses[id].AccessId;
                        var advances=await ProbeSelectedAccess(suspendedAudit,accessId,Time.GetTicksMsec());
                        _world.AttachVerifiedAddressAccessPaths();
                        var access=registry.AccessPoints[accessId];
                        _checks.Add(new{kind="crowded-door-access",addressId=id,access,advances,
                            plateApproachIsSeparate=true,humanNavigationTest=false});
                        Check(access.State=="verified",id+": standing door route attached to the road graph");
                        try
                        {
                            Require(access.State=="verified",id+": a physical route is required before walking");
                            var road=registry.Graph.Roads["access/"+accessId];
                            var readPoint=Act1ConnectedWorld.AddressGround(sign.GlobalPosition+sign.GlobalBasis.Z.Normalized()*.75f);
                            // A plate above a raised portico is read from that floor, not from the terrain below it.
                            {
                                var above=sign.GlobalPosition+sign.GlobalBasis.Z.Normalized()*.75f;
                                using var floorRay=PhysicsRayQueryParameters3D.Create(above,above-Vector3.Up*3f,3);
                                floorRay.Exclude=new global::Godot.Collections.Array<Rid>{_player.GetRid()};
                                var floorHit=_player.GetWorld3D().DirectSpaceState.IntersectRay(floorRay);
                                if(floorHit.Count>0&&floorHit["normal"].AsVector3().Y>.7f)readPoint=floorHit["position"].AsVector3()+Vector3.Up*.035f;
                            }
                            var roadPoints=road.Points.Select(V).ToList();
                            var yardLeg=await PlanYardLegAsync(roadPoints[^1],readPoint);
                            _checks.Add(new{kind="yard-leg-plan",addressId=id,from=P(roadPoints[^1]),to=P(readPoint),found=yardLeg is not null,waypoints=yardLeg?.Count??0});
                            var points=roadPoints.Concat(yardLeg??[readPoint]).ToArray();
                            using(var support=new AddressWalkProbe(_world))
                            {
                                Require(support.TrySupport(points[0],out var roadStart),id+": road fixture has actual support");
                                _player.ApplyZoneSpawn(roadStart,0);
                            }
                            await Frames(6);
                            await RestoreControlsAsync();
                            var arrival=await Act1FirstPersonWalkthroughSmokeTest.FollowMosqueRouteAsync(this,_player,_bridge,
                                points,"address/"+id+"/read-approach",row=>_checks.Add(row));
                            Require(arrival.VisitedPoints==points.Length&&arrival.StableLanding,
                                id+": controller walks the published door path and final segment to the plate");
                            await ReadNotebookSaveLoad(id,id+"_");
                            await RestoreControlsAsync();
                            var returned=await Act1FirstPersonWalkthroughSmokeTest.FollowMosqueRouteAsync(this,_player,_bridge,
                                points.Reverse().ToArray(),"address/"+id+"/return-to-road",row=>_checks.Add(row));
                            Require(returned.VisitedPoints==points.Length&&returned.StableLanding,
                                id+": controller returns along the same route to the connected road");
                        }
                        catch(Exception error){_failures.Add(id+": "+error.Message);}
                    }
                    _registryEvidence=JsonSerializer.SerializeToElement(new{version=SettlementRegistry.RegistryVersion,
                        subsetOnly=true,addresses=registry.Addresses.Values.Where(a=>selectedIds.Contains(a.AddressId)),
                        accesses=registry.AccessPoints.Values.Where(a=>selectedIds.Any(id=>a.AccessId=="ACC-"+id))});
                }
                else if(_scope=="frontage-sightlines")
                {
                    foreach(var issue in _world.AddressImportIssues.Where(i=>i.Code.StartsWith("SIGN_MOUNT",StringComparison.Ordinal)))
                        _failures.Add(issue.Code+" "+issue.EntityId+": "+issue.Detail);
                    _registryEvidence=JsonSerializer.SerializeToElement(new{version=SettlementRegistry.RegistryVersion,
                        subsetOnly=true,addresses=registry.Addresses.Values,importIssues=_world.AddressImportIssues});
                    foreach(var sign in Descendants(_world).OfType<AddressSignVisualComponent>())RecordMount(sign,registry);
                    foreach(var id in new[]{"ADR-H009","ADR-H015","ADR-H019","ADR-H013","ADR-H041","ADR-BABAI","ADR-H017"})await CaptureHouse("frontage_"+id,id);
                }
                else if(_scope=="h045-layout")
                {
                    foreach(var sign in Descendants(_world).OfType<AddressSignVisualComponent>())RecordMount(sign,registry);
                    await CaptureSourceReview("ArrivalLeftHorizonDomesticFacade");
                    await CaptureHouse("h045_plate","ADR-H045");
                    var accessId=registry.Addresses["ADR-H045"].AccessId;
                    var advances=await ProbeSelectedAccess(suspendedAudit,accessId,Time.GetTicksMsec());
                    _world.AttachVerifiedAddressAccessPaths();
                    var access=registry.AccessPoints[accessId];
                    _checks.Add(new{kind="h045-selected-access",access,advances});
                    Check(access.State=="verified","H045 has an actual standing route attached to the road graph");
                    _registryEvidence=JsonSerializer.SerializeToElement(new{version=SettlementRegistry.RegistryVersion,
                        subsetOnly=true,buildings=registry.Buildings.Values,parcels=registry.Parcels.Values,
                        addresses=registry.Addresses.Values,access,importIssues=_world.AddressImportIssues});
                    await ReadNotebookSaveLoad("ADR-H045","h045_");
                }
                else if(_scope=="remediation-search")
                    AddressRemediationGeometryProof.Capture(_world,_player,_output,boundedSearchOnly:true);
                else if(_scope=="production-entrances")
                    await VerifyProductionEntrances(suspendedAudit);
                else if(_scope=="standalone-access")
                    await VerifyStandaloneShedAccess(suspendedAudit);
                else if(_scope=="verge-contacts")
                    VerifyInclinedVergeContacts();
                else
                {
                    var blockedViews=new List<AddressAccessGeometryProof.EntranceBlockedView>();
                    var previews=AddressAccessGeometryProof.CaptureEntranceCandidates(_world,_player,_output,standalone:_scope=="standalone-candidates",blockedViews:blockedViews);
                    await CaptureEntranceMountPreviews(previews);
                    if(_scope=="standalone-candidates")Require(blockedViews.Count==1,
                        "standalone comparison includes its one actually supported ReturnWest photograph");
                    await CaptureBlockedEntranceView(blockedViews);
                }
                exit=_failures.Count==0?0:1;
                return;
            }

            var seconds=180;
            if(int.TryParse(System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_AUDIT_SECONDS"),out var requested))seconds=Math.Clamp(requested,20,300);
            var start=Time.GetTicksMsec();var lastReport=start;
            while(!(_world.HasMeta("addressAccessAuditCompleted")&&_world.GetMeta("addressAccessAuditCompleted").AsBool()) && Time.GetTicksMsec()-start<(ulong)seconds*1000)
            {
                await Frames(1);
                if(Time.GetTicksMsec()-lastReport>=5000)
                {
                    lastReport=Time.GetTicksMsec();
                    GD.Print("address-world: auditing "+registry.AccessPoints.Values.Count(a=>a.State!="pending-physics")+"/"+registry.AccessPoints.Count);
                }
            }
            _auditCompleted=_world.HasMeta("addressAccessAuditCompleted")&&_world.GetMeta("addressAccessAuditCompleted").AsBool();
            Check(_auditCompleted,"bounded physical address audit completed");
            _checks.Add(new{kind="audit-duration",milliseconds=Time.GetTicksMsec()-start,deadlineSeconds=seconds,completed=_auditCompleted});
            foreach(var issue in _world.AddressImportIssues.Concat(registry.Validate()))
                _failures.Add(issue.Code+" "+issue.EntityId+": "+issue.Detail);
            _registryEvidence=JsonSerializer.SerializeToElement(new
            {
                version=SettlementRegistry.RegistryVersion,streets=registry.Streets.Values,addressAliases=registry.AddressAliases,
                buildings=registry.Buildings.Values,parcels=registry.Parcels.Values,addresses=registry.Addresses.Values,
                accessPoints=registry.AccessPoints.Values,constraints=registry.Constraints,
                graph=new{nodes=registry.Graph.Nodes.Values,edges=registry.Graph.Edges.Values,roads=registry.Graph.Roads.Values},
                importIssues=_world.AddressImportIssues,inactiveReservedBindings=_world.AddressInactiveBindings,validation=registry.Validate()
            });
            try{AddressAccessGeometryProof.Capture(_world,_player,_output);}
            catch(Exception error){_failures.Add("read-only access geometry: "+error);}
            try{AddressRemediationGeometryProof.Capture(_world,_player,_output);}
            catch(Exception error){_failures.Add("read-only remediation geometry: "+error);}
            foreach(var sign in Descendants(_world).OfType<AddressSignVisualComponent>())RecordMount(sign,registry);
            foreach(var address in registry.Addresses.Values.Where(a=>!Descendants(_world).OfType<AddressSignVisualComponent>().Any(s=>s.AddressId==a.AddressId)))
            {
                var source=Descendants(_world).OfType<Node3D>().FirstOrDefault(n=>n.HasMeta("building_id")&&n.GetMeta("building_id").AsString()==address.BuildingId);
                if(source is null)continue;
                _mounts.Add(new{addressId=address.AddressId,buildingId=address.BuildingId,plateAbsent=true,source=source.GetPath().ToString(),scale=P(source.GlobalBasis.Scale),
                    visibleFacadeMeshes=Descendants(source).OfType<MeshInstance3D>().Where(m=>m.Mesh is not null&&m.IsVisibleInTree()&&(AddressFacadeMount.Eligible(m.Name.ToString())||m.Name.ToString().Contains("Door",StringComparison.Ordinal)))
                        .Select(m=>new{name=m.GetPath().ToString(),center=P(m.GlobalTransform*m.Mesh!.GetAabb().GetCenter()),localSize=P(m.Mesh!.GetAabb().Size),basis=B(m.GlobalBasis)}).ToArray()});
            }
            try{await CaptureNotebookMap("00_empty_map_large",0,true);}
            catch(Exception error){_failures.Add("empty-map: "+error.Message);}

            var requestedIds=new List<(string Name,string Id)>{("01_babai","ADR-BABAI"),("02_fap","ADR-FAP")};
            if(registry.TryFind("usal","15",out var usal15))requestedIds.Add(("03_usal_15",usal15.AddressId));
            else _failures.Add("Usal 15 is absent from the actual imported world.");
            if(registry.TryFind("usal","52",out var usal52))requestedIds.Add(("04_usal_52",usal52.AddressId));
            else _failures.Add("Usal 52 is absent from the actual imported world.");
            foreach(var request in requestedIds)
            {
                try{await CaptureHouse(request.Name,request.Id);}
                catch(Exception error){_failures.Add(request.Name+": "+error.Message);}
            }
            foreach(var id in new[]{"ADR-H007","ADR-H024"})
                if(registry.Addresses.ContainsKey(id)&&!Descendants(_world).OfType<AddressSignVisualComponent>().Any(s=>s.AddressId==id))
                    try{await CaptureHouse("08_missing_"+id,id);}
                    catch(Exception error){_failures.Add("missing-mount source capture "+id+": "+error.Message);}
            foreach(var name in new[]{"ArrivalLeftHorizonDomesticFacade","ZiratVillageEdgeEastShed","PerimeterWestStreetShed","ArrivalReverseEastDomesticShed"})
                try{await CaptureSourceReview(name);}
                catch(Exception error){_failures.Add("source review capture "+name+": "+error.Message);}
            try{await ReadNotebookSaveLoad("ADR-BABAI");}
            catch(Exception error){_failures.Add("manual-read-notebook-save-load: "+error);}
            if(System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_PROPOSE_XZ")=="1")
                ProposeAccessCorrections(registry);
            exit=_failures.Count==0?0:1;
        }
        catch(Exception error){_failures.Add(error.ToString());}
        finally
        {
            Input.ActionRelease("interact");Input.ActionRelease("journal");
            try
            {
                if(_output.Length>0 && Directory.Exists(_output))
                {
                    var identity=AssemblyIdentity();
                    var payload=JsonSerializer.Serialize(new
                    {
                        schemaVersion=1,ordinaryNewGame=true,humanSearchPlaytest=false,diagnosticCameraFixtures=_captures.Count>0,
                        assembly=identity,scope=_scope,auditRun=_auditRun,fullAuditRun=_auditRun,
                        selectedAccessAuditRun=_scope is "production-entrances" or "standalone-access" or "h045-layout" or "crowded-addresses" or "far-bank",acceptance=_scope=="full"&&exit==0,
                        auditCompleted=_auditCompleted,registry=_registryEvidence,mounts=_mounts,captures=_captures,checks=_checks,failures=_failures
                    },new JsonSerializerOptions{WriteIndented=true});
                    var path=Path.Combine(_output,"address-world-receipt.json");
                    var temporary=path+".writing";
                    using(var file=new FileStream(temporary,FileMode.CreateNew,System.IO.FileAccess.Write,FileShare.Read))
                    using(var writer=new StreamWriter(file)){writer.Write(payload);writer.Flush();file.Flush(true);}
                    File.Move(temporary,path,false);
                }
            }
            catch(Exception error){exit=1;_failures.Add("receipt-write: "+error);GD.PrintErr("address-world receipt: "+error);}
            if(suspendedAudit is not null&&GodotObject.IsInstanceValid(suspendedAudit))
                suspendedAudit.SetPhysicsProcess(auditWasProcessing);
            if(_demo is not null)
            {
                try{await GodotSmokeCleanup.ReleaseAsync(_demo);}
                catch(Exception error){exit=1;GD.PrintErr("address-world cleanup: "+error);}
            }
            foreach(var failure in _failures)GD.PrintErr("address-world FAIL "+failure);
            if(_failures.Count>0)exit=1;
            GD.Print("address-world: "+(exit==0?"PASS":"FAIL")+"; scope="+_scope+"; "+_checks.Count+" checks; native diagnostics, not a human search or duration measurement");
            GetTree().Quit(exit);
        }
    }

    /// <summary>A long physical search can outlast the window's focus; the game then opens its own
    /// pause menu. That is ordinary behaviour, so the fixture closes it the way a player would
    /// (bring the window forward, press cancel) instead of walking a route under a modal.</summary>
    private async Task RestoreControlsAsync()
    {
        for(var attempt=0;attempt<4&&_player.ModalOpen;attempt++)
        {
            DisplayServer.WindowMoveToForeground();
            if(GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi{IsOpen:true} pause)pause.Resume();
            await Frames(20);
        }
        if(_player.ModalOpen)GD.Print("address-world: modal "+_player.ModalDiagnostic+" ui="+string.Join(",",_demo!.GetChildren().OfType<CanvasLayer>().Where(l=>l.Visible).Select(l=>l.Name))
            +" pause="+(GetTree().GetFirstNodeInGroup("pause_menu")?.Name??"none")+" blocks="+_bridge.CapturePlayTimeBlocks());
        Require(!_player.ModalOpen,"ordinary player controls are available before the route");
    }

    private object AssemblyIdentity()
    {
        var executing=Assembly.GetExecutingAssembly();
        var loadedModule=executing.ManifestModule.ModuleVersionId;
        var declared=System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_ASSEMBLY_PATH");
        var candidate=!string.IsNullOrWhiteSpace(declared)?declared:executing.Location;
        var source=!string.IsNullOrWhiteSpace(declared)?"URMAN_ADDRESS_ASSEMBLY_PATH":"Assembly.Location";
        if(string.IsNullOrWhiteSpace(candidate))
        {
            candidate=ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug/Urman.Game.dll");
            source="project-debug-candidate; module identity checked";
        }
        try
        {
            if(!Path.IsPathFullyQualified(candidate))throw new InvalidOperationException("Assembly evidence path must be absolute.");
            var bytes=File.ReadAllBytes(candidate);
            using var stream=new MemoryStream(bytes,false);using var pe=new PEReader(stream);
            var metadata=pe.GetMetadataReader();
            var diskModule=metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
            var matches=diskModule==loadedModule;
            Check(matches,"assembly candidate MVID matches the executing module");
            return new{path=candidate,pathSource=source,assemblyLocation=executing.Location,loadedModule,diskModule,
                status=matches?"module-match":"module-mismatch",sha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()};
        }
        catch(Exception error)
        {
            _failures.Add("assembly-identity: "+error.Message);
            return new{path=candidate,pathSource=source,assemblyLocation=executing.Location,loadedModule,status="not-verified",error=error.Message};
        }
    }

    private void RecordMount(AddressSignVisualComponent sign,SettlementRegistry registry)
    {
        var record=registry.Addresses[sign.AddressId];
        var source=Descendants(_world).OfType<Node3D>().FirstOrDefault(n=>n.HasMeta("building_id")&&n.GetMeta("building_id").AsString()==record.BuildingId);
        var normal=sign.GlobalBasis.Z.Normalized();
        string? explicitSurface=source?.HasMeta("addressSignSurfaceName")==true?source.GetMeta("addressSignSurfaceName").AsString():null;
        var samples=new List<object>();
        var assessment=source is null?new AddressFacadeMount.Coverage(false,0,"","missing source"):
            AddressFacadeMount.Inspect(source,sign.GlobalPosition,normal,_world,explicitSurface);
        var supportOkay=assessment.Supported;
        var surfaces=source is null?Array.Empty<(MeshInstance3D Mesh,Vector3[] Faces)>():Descendants(source).OfType<MeshInstance3D>()
            .Where(m=>m.Mesh is not null&&m.IsVisibleInTree()&&((explicitSurface is null?AddressFacadeMount.Eligible(m.Name.ToString()):m.Name==explicitSurface)
                ||AddressFacadeMount.StructuralTimber(m.Name.ToString()))).Select(m=>(Mesh:m,Faces:m.Mesh!.GetFaces())).ToArray();
        // Independent ray observations cover the physical outer rim and rivets;
        // the polygon assessment also detects openings between these samples.
        foreach(var offset2 in AddressFacadeMount.RequiredMountPoints(Vector2.Zero))
        {
            var offset=new Vector3(offset2.X,offset2.Y,0);
            var point=sign.GlobalTransform*offset;
            var visibleSupport=VisibleSupport(surfaces,point+normal*.45f,-normal,1.25f);
            var visualGap=visibleSupport is null?(float?)null:visibleSupport.Value.Distance-.45f;
            var fastener=Mathf.IsEqualApprox(Mathf.Abs(offset.X),AddressFacadeMount.RivetX)&&Mathf.IsEqualApprox(Mathf.Abs(offset.Y),AddressFacadeMount.RivetY);
            var boardJoint=visualGap is null&&!fastener&&assessment.Supported&&assessment.MissingArea>0
                &&assessment.Owner.Contains("BoardedGable",StringComparison.Ordinal);
            // The rigid rim may span a log groove only after the independent
            // polygon/fastener assessment proves the backing and all four screws.
            // Rivets retain the same 9 cm limit against actual visible timber.
            var timberJoint=!fastener&&assessment.Supported&&assessment.TimberCladding&&visualGap is >=-.02f and <=.321f;
            var mounted=visualGap is >=-.02f and <=.09f||boardJoint||timberJoint;
            if(!mounted)supportOkay=false;
            using var ray=PhysicsRayQueryParameters3D.Create(point+normal*.45f,point-normal*.8f,3);
            ray.Exclude=new global::Godot.Collections.Array<Rid>{_player.GetRid()};
            var hit=_player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            if(hit.Count==0)
            {
                samples.Add(new{sample=P(point),physicsHit=false,visualMesh=visibleSupport?.Mesh,visualGapMeters=visualGap,mounted,boardJoint,timberJoint});continue;
            }
            var surface=hit["position"].AsVector3();var gap=(point-surface).Dot(normal);
            samples.Add(new{sample=P(point),physicsHit=true,collider=(hit["collider"].AsGodotObject() as Node)?.GetPath().ToString(),point=P(surface),normal=P(hit["normal"].AsVector3()),physicsGapMeters=gap,
                visualMesh=visibleSupport?.Mesh,visualGapMeters=visualGap,mounted,boardJoint,timberJoint});
        }
        var leaf=source is null?Array.Empty<object>():Descendants(source).OfType<MeshInstance3D>()
            .Where(m=>m.Name.ToString().Contains("Door",StringComparison.Ordinal)&&m.Mesh is not null)
            .Select(m=>(object)new{name=m.GetPath().ToString(),visible=m.IsVisibleInTree(),position=P(m.GlobalTransform*m.Mesh!.GetAabb().GetCenter()),basis=B(m.GlobalBasis),aabbSize=P(m.Mesh.GetAabb().Size)}).ToArray();
        var access=registry.AccessPoints[record.AccessId];var at=V(access.Position);
        using var floorRay=PhysicsRayQueryParameters3D.Create(at+Vector3.Up*.55f,at-Vector3.Up*.75f,3);
        floorRay.Exclude=new global::Godot.Collections.Array<Rid>{_player.GetRid()};
        var floor=_player.GetWorld3D().DirectSpaceState.IntersectRay(floorRay);
        var labels=Descendants(sign).OfType<Label3D>().ToDictionary(l=>l.Name.ToString(),l=>new{text=l.Text,font=l.Font?.ResourcePath,pixelSize=l.PixelSize,fontSize=l.FontSize});
        _mounts.Add(new{addressId=record.AddressId,buildingId=record.BuildingId,source=source?.GetPath().ToString(),plate=sign.GetPath().ToString(),platePosition=P(sign.GlobalPosition),plateBasis=B(sign.GlobalBasis),plateWidth=2*AddressFacadeMount.HalfWidth,plateHeight=2*AddressFacadeMount.HalfHeight,labels,physicalMountSupported=supportOkay,mountAssessment=assessment,mountSamples=samples,doorMeshes=leaf,
            accessPosition=access.Position,accessState=access.State,standingCapsuleFits=_player.CanStandAt(at),floorHit=floor.Count>0,floorOwner=floor.Count>0?(floor["collider"].AsGodotObject() as Node)?.GetPath().ToString():null});
        Check(supportOkay,"mounted plate rests against actual facade triangles: "+record.AddressId);
        Check(labels.TryGetValue("TatarStreet",out var tt)&&tt.text==registry.Streets[record.StreetId].Tatar
            && labels.TryGetValue("RussianStreet",out var ru)&&ru.text==registry.Streets[record.StreetId].Russian
            && labels.TryGetValue("HouseNumber",out var num)&&num.text==record.HouseNumber,"sign text agrees with registry: "+record.AddressId);
    }

    /// <summary>Diagnostic-only accessXZ proposal. For every access point the
    /// live audit left blocked, spiral-search settled live physics for the
    /// nearest standable XZ and print it as manifest-ready JSON. The audit
    /// verdict is unchanged; a human checks the proposal into
    /// urman.settlement.addresses.v1.json as explicit accessXZ.</summary>
    private void ProposeAccessCorrections(SettlementRegistry registry)
    {
        var proposals=new List<object>();
        using var probe=new AddressWalkProbe(_world);
        foreach(var access in registry.AccessPoints.Values.Where(a=>a.State!="verified").OrderBy(a=>a.AccessId))
        {
            var current=Act1ConnectedWorld.AddressVector(access.Position);
            string? accepted=null;var acceptedAt="";
            for(var radius=.25f;radius<=4f&&accepted is null;radius+=.25f)
            {
                for(var angle=0;angle<16&&accepted is null;angle++)
                {
                    var candidate=current+new Vector3(Mathf.Cos(angle*Mathf.Tau/16)*radius,0,Mathf.Sin(angle*Mathf.Tau/16)*radius);
                    if(!probe.TrySupport(candidate,out var feet))continue;
                    acceptedAt=$"[{feet.X:0.###}, {feet.Z:0.###}]";
                    accepted=acceptedAt;
                }
            }
            proposals.Add(new{access=access.AccessId,building=access.BuildingId,state=access.State,
                currentXZ=$"[{current.X:0.###}, {current.Z:0.###}]",proposedAccessXZ=accepted});
        }
        GD.Print("address-access-proposals: "+JsonSerializer.Serialize(proposals));
        _checks.Add(new{kind="access-xz-proposals",proposals,acceptance=false,
            scope="diagnostic only; check accepted values into the manifest as accessXZ"});
    }
    private async Task WaitForOrdinaryPhotoBaseline()
    {
        var origin=_player.GlobalPosition;var initialPose=_player.CapturePortableTransform();
        var initialVelocity=_player.Velocity;var initialProps=_bridge.SelectWorldProps().GetRawText();
        var revision=_player.PresentationTransformRevision;var clamps=_player.EdgeClamps;var recoveries=_player.FallRecoveries;
        var start=Time.GetTicksMsec();var physicsStart=Engine.GetPhysicsFrames();var processStart=Engine.GetProcessFrames();
        (Rid Body,int Shape,Vector3 Point,Vector3 Normal,string? Owner)? Support()
        {
            var feet=_player.GlobalPosition;
            using var ray=PhysicsRayQueryParameters3D.Create(feet+Vector3.Up*.25f,feet+Vector3.Down*2f,
                3,new global::Godot.Collections.Array<Rid>{_player.GetRid()});
            var hit=_player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            return hit.Count==0?null:(hit["rid"].AsRid(),hit["shape"].AsInt32(),hit["position"].AsVector3(),
                hit["normal"].AsVector3(),(hit["collider"].AsGodotObject() as Node)?.GetPath().ToString());
        }
        var initialSupport=Support();
        Require(_player.IsPhysicsProcessing()&&!GetTree().Paused&&!_player.ModalOpen&&!_player.VehicleControlled&&!_bridge.NeedsPhysicalRecovery,
            "photo baseline allows ordinary pedestrian physics");
        Require(initialSupport.HasValue&&initialSupport.Value.Normal.Y>=Mathf.Cos(_player.FloorMaxAngle)
            &&origin.Y-initialSupport.Value.Point.Y>=-.03f&&origin.Y-initialSupport.Value.Point.Y<=2f,
            "photo baseline has measured nearby walkable support");
        var expected=initialSupport!.Value;var previousPose=initialPose;
        var stableSamples=0;var samples=0;var lastSupport=initialSupport;var observations=new List<object>();
        // A newly spawned player may still be falling when the synchronous
        // candidate queries end. Let the actual controller land before taking
        // the photograph's strict baseline; never freeze or relocate the actor.
        while(samples<180&&Time.GetTicksMsec()-start<5000&&stableSamples<6)
        {
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            samples++;lastSupport=Support();var pose=_player.CapturePortableTransform();
            var supported=_player.IsOnFloor()&&Mathf.Abs(_player.Velocity.Y)<.02f
                &&_player.CanStandAt(_player.GlobalPosition)&&lastSupport.HasValue
                &&lastSupport.Value.Body==expected.Body&&lastSupport.Value.Shape==expected.Shape
                &&lastSupport.Value.Normal.Y>=Mathf.Cos(_player.FloorMaxAngle)
                &&Mathf.Abs(lastSupport.Value.Point.Y-expected.Point.Y)<.02f
                &&Mathf.Abs(_player.GlobalPosition.Y-lastSupport.Value.Point.Y)<.03f;
            stableSamples=supported&&pose.Equals(previousPose)?stableSamples+1:0;previousPose=pose;
            if(samples<=4||stableSamples>0||samples==180||Time.GetTicksMsec()-start>=5000)
                observations.Add(new{sample=samples,pose,velocity=P(_player.Velocity),onFloor=_player.IsOnFloor(),supported,stableSamples,
                    supportOwner=lastSupport?.Owner,supportPoint=lastSupport.HasValue?P(lastSupport.Value.Point):null});
        }
        var displacement=_player.GlobalPosition-origin;
        var propsUnchanged=initialProps==_bridge.SelectWorldProps().GetRawText();
        var unchangedControl=_player.IsPhysicsProcessing()&&!GetTree().Paused&&!_player.ModalOpen&&!_player.VehicleControlled
            &&!_bridge.NeedsPhysicalRecovery&&_player.PresentationTransformRevision==revision
            &&_player.EdgeClamps==clamps&&_player.FallRecoveries==recoveries;
        var record=new{kind="blocked-entrance-natural-baseline",initialPose,initialVelocity=P(initialVelocity),finalPose=_player.CapturePortableTransform(),
            expectedSupportOwner=expected.Owner,expectedSupportPoint=P(expected.Point),expectedSupportNormal=P(expected.Normal),
            stableSamples,samples,observations,elapsedMilliseconds=Time.GetTicksMsec()-start,
            physicsFrames=Engine.GetPhysicsFrames()-physicsStart,processFrames=Engine.GetProcessFrames()-processStart,
            displacement=P(displacement),propsUnchanged,unchangedControl,actorTransformWritten=false,playerPhysicsSuspended=false,acceptance=false};
        _checks.Add(record);GD.Print(JsonSerializer.Serialize(record));
        Require(stableSamples>=6&&propsUnchanged&&unchangedControl
            &&new Vector2(displacement.X,displacement.Z).Length()<.04f&&displacement.Y<=.03f&&displacement.Y>=-2.03f,
            "photo baseline reaches stable real floor contact without relocation or progress");
    }

    private async Task CaptureBlockedEntranceView(IReadOnlyList<AddressAccessGeometryProof.EntranceBlockedView> views)
    {
        Require(views.Count<=1,"blocked entrance view is bounded to the measured ReturnWest candidate");
        if(views.Count==0)return;
        await WaitForOrdinaryPhotoBaseline();
        var view=views[0];var ordinary=_player.GetNode<Camera3D>("Head/Camera3D");
        var beforePose=_player.CapturePortableTransform();var beforeProps=_bridge.SelectWorldProps().GetRawText();
        var beforeRevision=_player.PresentationTransformRevision;var beforeClamps=_player.EdgeClamps;var beforeRecoveries=_player.FallRecoveries;
        var beforeVelocity=_player.Velocity;var beforeFloor=_player.IsOnFloor();
        var beforeTicks=Time.GetTicksMsec();var beforePhysics=Engine.GetPhysicsFrames();var beforeProcess=Engine.GetProcessFrames();
        var beforePlayerProcessing=_player.IsPhysicsProcessing();
        var name="13_ReturnWest_existing_seni_blocked_view";
        var camera=new Camera3D{Name="DiagnosticBlockedEntranceCamera",Fov=ordinary.Fov,Near=ordinary.Near,Far=ordinary.Far};
        AddChild(camera);
        try
        {
            camera.GlobalPosition=view.Eye;camera.LookAt(view.Target,Vector3.Up);camera.MakeCurrent();await Frames(3);
            await Capture(name);
            Act1VisibleSurfaceProbe.Log(_world,camera,name+"/actual-rendered-obstruction",new Vector2(.5f,.5f));
        }
        finally{ordinary.MakeCurrent();camera.QueueFree();await Frames(2);}
        var afterPose=_player.CapturePortableTransform();var afterProps=_bridge.SelectWorldProps().GetRawText();
        var poseUnchanged=beforePose.Equals(afterPose);var propsUnchanged=beforeProps==afterProps;
        using var beforeDocument=JsonDocument.Parse(beforeProps);using var afterDocument=JsonDocument.Parse(afterProps);
        var beforeFields=beforeDocument.RootElement.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetRawText(),StringComparer.Ordinal);
        var afterFields=afterDocument.RootElement.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetRawText(),StringComparer.Ordinal);
        var changedProps=beforeFields.Keys.Union(afterFields.Keys,StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Where(key=>!beforeFields.TryGetValue(key,out var before)||!afterFields.TryGetValue(key,out var after)||before!=after)
            .Select(key=>new{key,before=beforeFields.TryGetValue(key,out var before)?before:null,after=afterFields.TryGetValue(key,out var after)?after:null}).ToArray();
        var observation=new{kind="blocked-entrance-photograph-state",beforePose,afterPose,poseUnchanged,propsUnchanged,changedProps,
            beforeRevision,afterRevision=_player.PresentationTransformRevision,beforeClamps,afterClamps=_player.EdgeClamps,
            beforeRecoveries,afterRecoveries=_player.FallRecoveries,beforeVelocity=P(beforeVelocity),afterVelocity=P(_player.Velocity),
            beforeFloor,afterFloor=_player.IsOnFloor(),beforePlayerProcessing,afterPlayerProcessing=_player.IsPhysicsProcessing(),
            elapsedMilliseconds=Time.GetTicksMsec()-beforeTicks,physicsFrames=Engine.GetPhysicsFrames()-beforePhysics,processFrames=Engine.GetProcessFrames()-beforeProcess,
            ordinaryCameraRestored=ordinary.Current,diagnosticCameraRemoved=!IsInstanceValid(camera),
            cameraOnlyTransformWrites=true,acceptance=false};
        // Persist both sides before the strict assertion. A combined false did
        // not identify whether ordinary physics, recovery or state changed.
        _checks.Add(observation);GD.Print(JsonSerializer.Serialize(observation));
        _captures.Add(new{file=name+".png",view.SourceName,view.LeafPath,feet=P(view.Feet),camera=P(view.Eye),target=P(view.Target),
            view.PhysicsLeafMatched,view.KnownVisibleObstruction,
            diagnostic=true,physicalApproachSupported=true,actorMoved=!poseUnchanged,worldPropsUnchanged=propsUnchanged,
            geometryChanged=false,visibilityChanged=false,fieldOfView=ordinary.Fov,accepted=false,humanSearchPlaytest=false});
        Require(poseUnchanged&&propsUnchanged,
            "blocked entrance photograph leaves actor pose and world progress unchanged");
    }

    private async Task CaptureEntranceMountPreviews(IReadOnlyList<AddressAccessGeometryProof.EntranceMountPreview> proposals)
    {
        var ordinary=_player.GetNode<Camera3D>("Head/Camera3D");
        var eye=_player.ToLocal(ordinary.GlobalPosition);
        var beforeProps=_bridge.SelectWorldProps().GetRawText();
        foreach(var proposal in proposals)
        {
            var camera=new Camera3D{Name="DiagnosticEntranceMountCamera",Fov=ordinary.Fov,Near=ordinary.Near,Far=ordinary.Far};
            AddressSignVisualComponent? preview=null;
            AddChild(camera);
            try
            {
                if(proposal.Supported)
                {
                    // Render the real registry lettering and real plate dimensions
                    // outside the gameplay world. No read collider, interaction or
                    // knowledge callback is created for this temporary preview.
                    preview=new AddressSignVisualComponent();AddChild(preview);
                    preview.Bind(_world.AddressRegistry!,proposal.AddressId);
                    preview.SetMeta("diagnosticMountPreviewOnly",true);
                    preview.GlobalPosition=proposal.Point;
                    preview.GlobalBasis=new Basis(Vector3.Up,Mathf.Atan2(proposal.Outward.X,proposal.Outward.Z));
                    Require(!Descendants(preview).OfType<CollisionObject3D>().Any(),"diagnostic sign preview creates no collider or interaction");
                }
                foreach(var view in new[]{(Name:"road",Feet:proposal.RoadFeet,Supported:proposal.RoadSupported),
                    (Name:"approach",Feet:proposal.ApproachFeet,Supported:proposal.ApproachSupported)})
                {
                    if(!view.Supported)
                    {
                        _checks.Add(new{kind="mount-preview-view-not-run",proposal.AddressId,proposal.SurfaceName,view=view.Name,
                            reason="No physical standing support at requested view; no floating camera substituted.",accepted=false});
                        continue;
                    }
                    var right=new Vector3(proposal.Outward.Z,0,-proposal.Outward.X);
                    camera.GlobalPosition=view.Feet+Vector3.Up*eye.Y+proposal.Outward*eye.Z+right*eye.X;
                    camera.LookAt(proposal.Point,Vector3.Up);camera.MakeCurrent();await Frames(3);
                    var stem=proposal.SurfaceName.Replace("DwellingFacade_","").Replace("_Wall_LOD0","");
                    var name="10_"+proposal.AddressId+"_"+stem+"_"+view.Name;
                    await Capture(name);
                    var projected=preview is null?Array.Empty<object>():new[]{new Vector3(-AddressFacadeMount.HalfWidth,-AddressFacadeMount.HalfHeight,0),new(-AddressFacadeMount.HalfWidth,AddressFacadeMount.HalfHeight,0),new(AddressFacadeMount.HalfWidth,AddressFacadeMount.HalfHeight,0),new(AddressFacadeMount.HalfWidth,-AddressFacadeMount.HalfHeight,0)}
                        .Select(p=>{var pixel=camera.UnprojectPosition(preview.GlobalTransform*p);return (object)new{x=pixel.X,y=pixel.Y};}).ToArray();
                    _captures.Add(new{file=name+".png",proposal.AddressId,proposal.SurfaceName,view=view.Name,
                        diagnostic=true,previewOnly=true,actualProductionPlate=false,sourceGeometryChanged=false,
                        temporaryPlatePresent=preview is not null,point=P(proposal.Point),feet=P(view.Feet),camera=P(camera.GlobalPosition),
                        fieldOfView=camera.Fov,usesOrdinaryFieldOfView=true,standingEyeHeight=eye.Y,projectedPlateCorners=projected,
                        distance=camera.GlobalPosition.DistanceTo(proposal.Point),humanReadability="requires inspection",humanSearchPlaytest=false});
                    Act1VisibleSurfaceProbe.Log(_world,camera,name+"/existing-facade-owner",new Vector2(.5f,.5f));
                }
            }
            finally
            {
                ordinary.MakeCurrent();preview?.QueueFree();camera.QueueFree();await Frames(2);
                Require(preview is null||!GodotObject.IsInstanceValid(preview),"temporary mount preview removed in finally");
            }
        }
        Require(beforeProps==_bridge.SelectWorldProps().GetRawText(),"diagnostic plate views do not change discovered addresses or world state");
    }

    private async Task CaptureHouse(string name,string addressId)
    {
        var sign=Descendants(_world).OfType<AddressSignVisualComponent>().SingleOrDefault(s=>s.AddressId==addressId);
        if(sign is null){await CaptureUnmountedHouse(name,addressId);return;}
        var camera=new Camera3D{Name="DiagnosticAddressCamera",Fov=68,Near=.06f,Far=280};
        AddChild(camera);
        var ordinary=_player.GetNode<Camera3D>("Head/Camera3D");
        try
        {
            camera.GlobalPosition=sign.GlobalPosition+sign.GlobalBasis.Z.Normalized()*4.0f+Vector3.Up*.30f;
            camera.LookAt(sign.GlobalPosition,Vector3.Up);camera.MakeCurrent();
            await Frames(3);
            await Capture(name);
            _captures.Add(new{file=name+".png",addressId,camera=P(camera.GlobalPosition),basis=B(camera.GlobalBasis),fieldOfView=camera.Fov,diagnostic=true,controllerTraversal=false});
            Act1VisibleSurfaceProbe.Log(_world,camera,name+"/actual-visible-owner",new Vector2(.5f,.5f),new Vector2(.45f,.5f),new Vector2(.55f,.5f));
        }
        finally{ordinary.MakeCurrent();camera.QueueFree();await Frames(1);}
    }
    private async Task CaptureUnmountedHouse(string name,string addressId)
    {
        var address=_world.AddressRegistry!.Addresses[addressId];
        var source=Descendants(_world).OfType<Node3D>().Single(n=>n.HasMeta("building_id")&&n.GetMeta("building_id").AsString()==address.BuildingId);
        if(!_world.TryGetAddressSourceEntrance(source.Name.ToString(),out var door,out var outward))throw new InvalidOperationException("Missing source entrance.");
        var camera=new Camera3D{Name="DiagnosticMissingMountCamera",Fov=68,Near=.06f,Far=280};AddChild(camera);
        var ordinary=_player.GetNode<Camera3D>("Head/Camera3D");
        try
        {
            camera.GlobalPosition=door+outward*6+Vector3.Up*.5f;camera.LookAt(door+Vector3.Up*.3f,Vector3.Up);camera.MakeCurrent();
            await Frames(3);await Capture(name);
            _captures.Add(new{file=name+".png",addressId,plateAbsent=true,camera=P(camera.GlobalPosition),source=source.GetPath().ToString(),diagnostic=true,humanSearchPlaytest=false});
        }
        finally{ordinary.MakeCurrent();camera.QueueFree();await Frames(1);}
    }
    private async Task CaptureSourceReview(string sourceName)
    {
        var source=Descendants(_world).OfType<Node3D>().Single(n=>n.Name==sourceName&&n.IsVisibleInTree());
        var camera=new Camera3D{Name="DiagnosticAddressSourceReview",Fov=68,Near=.06f,Far=280};AddChild(camera);
        var ordinary=_player.GetNode<Camera3D>("Head/Camera3D");
        try
        {
            var point=source.GlobalPosition+Vector3.Up;
            var towardRoad=new Vector3(-source.GlobalPosition.X,0,0).Normalized();
            camera.GlobalPosition=point+towardRoad*7+Vector3.Up*2.5f;
            camera.LookAt(point,Vector3.Up);camera.MakeCurrent();await Frames(3);
            var name="09_review_"+sourceName;await Capture(name);
            _captures.Add(new{file=name+".png",source=source.GetPath().ToString(),camera=P(camera.GlobalPosition),
                diagnostic=true,ownershipAssignment=false,geometryChanged=false,humanSearchPlaytest=false});
        }
        finally{ordinary.MakeCurrent();camera.QueueFree();await Frames(1);}
    }
    private static (string Mesh,float Distance)? VisibleSupport((MeshInstance3D Mesh,Vector3[] Faces)[] surfaces,Vector3 origin,Vector3 direction,float limit)
    {
        (string Mesh,float Distance)? nearest=null;
        foreach(var surface in surfaces)
        {
            var inverse=surface.Mesh.GlobalTransform.AffineInverse();
            var localOrigin=inverse*origin;var localDirection=inverse.Basis*direction;
            var faces=surface.Faces;
            for(var i=0;i+2<faces.Length;i+=3)
            {
                var a=faces[i];var edge1=faces[i+1]-a;var edge2=faces[i+2]-a;
                var p=localDirection.Cross(edge2);var determinant=edge1.Dot(p);
                if(Mathf.Abs(determinant)<.0000001f)continue;
                var reciprocal=1f/determinant;var offset=localOrigin-a;var u=offset.Dot(p)*reciprocal;
                if(u<0||u>1)continue;
                var q=offset.Cross(edge1);var v=localDirection.Dot(q)*reciprocal;
                if(v<0||u+v>1)continue;
                var distance=edge2.Dot(q)*reciprocal;
                if(distance>.001f&&distance<=limit&&(nearest is null||distance<nearest.Value.Distance))
                    nearest=(surface.Mesh.GetPath().ToString(),distance);
            }
        }
        return nearest;
    }

    private async Task VerifyProductionEntrances(AddressAccessVerifier audit)
    {
        var registry=_world.AddressRegistry!;
        var started=Time.GetTicksMsec();
        foreach(var id in new[]{"ADR-H009","ADR-H016"})
        {
            var accessId=registry.Addresses[id].AccessId;
            var advances=await ProbeSelectedAccess(audit,accessId,started);
            Require(registry.AccessPoints[accessId].State=="pending-graph-attachment",id+": current physical search succeeded before graph attachment");
            _world.AttachVerifiedAddressAccessPaths();
            var access=registry.AccessPoints[accessId];
            var origin=registry.Graph.NodeAt(registry.Graph.Roads["authored/main-axis"].Points[0])!;
            var route=registry.DiagnosticRoute(origin,id,SettlementTravelMode.Foot);
            _checks.Add(new{kind="selected-production-access",addressId=id,access,advances,route=route.ToArray(),
                globalAuditCompleted=_world.HasMeta("addressAccessAuditCompleted")&&_world.GetMeta("addressAccessAuditCompleted").AsBool(),
                usesExistingSearch=true,usesSharedGraph=true,routeShownToPlayer=false});
            Require(access.State=="verified"&&route.Count>0&&route[^1].DistanceXZ(access.Position)<.001,
                id+": actual standing path is attached to the shared graph at the selected entrance");
            if(id=="ADR-H009")await VerifyAccessInvalidation(audit,accessId,started);
            var sign=Descendants(_world).OfType<AddressSignVisualComponent>().Single(s=>s.AddressId==id);
            RecordMount(sign,registry);
            await CaptureHouse("11_"+id+"_production_plate",id);
            await KnockSelectedDoor(id);
            await ReadNotebookSaveLoad(id,id+"_");
        }
        var ordinaryCompleted=_world.HasMeta("addressAccessAuditCompleted")&&_world.GetMeta("addressAccessAuditCompleted").AsBool();
        Require(!ordinaryCompleted,"selected entrance checks do not declare the complete settlement audit finished");
        _registryEvidence=JsonSerializer.SerializeToElement(new{version=SettlementRegistry.RegistryVersion,subsetOnly=true,
            addresses=registry.Addresses.Values.Where(a=>a.AddressId is "ADR-H009" or "ADR-H016").ToArray(),
            accesses=registry.AccessPoints.Values.Where(a=>a.AccessId is "ACC-ADR-H009" or "ACC-ADR-H016").ToArray(),
            ordinaryAuditWasNotCompleted=!ordinaryCompleted,milliseconds=Time.GetTicksMsec()-started});
    }

    private async Task<int> ProbeSelectedAccess(AddressAccessVerifier audit,string accessId,ulong started)
    {
        var advances=0;var more=true;
        using var search=audit.ProbeAccessForDiagnostic(accessId);
        while(more)
        {
            if(Time.GetTicksMsec()-started>=120000)throw new TimeoutException("Selected existing access audit exceeded its120second deadline.");
            var slice=Time.GetTicksUsec();
            for(var i=0;i<48&&Time.GetTicksUsec()-slice<2000;i++)
            {
                more=search.MoveNext();if(!more)break;advances++;
            }
            if(more)await Frames(1);
        }
        return advances;
    }

    private async Task VerifyAccessInvalidation(AddressAccessVerifier audit,string accessId,ulong started)
    {
        var registry=_world.AddressRegistry!;
        long Stamp(string name)=>_world.HasMeta(name)&&_world.GetMeta(name).AsGodotDictionary().TryGetValue(accessId,out var frame)?frame.AsInt64():-1;
        bool RoadAbsent()=>!registry.Graph.Roads.ContainsKey("access/"+accessId)
            &&registry.Graph.Edges.Values.All(edge=>edge.RoadId!="access/"+accessId);
        var original=registry.AccessPoints[accessId];
        var originalCommit=Stamp("addressAccessCommitFrames");var originalAttach=Stamp("addressAccessAttachFrames");
        var propsBefore=_bridge.SelectWorldProps().GetRawText();var sceneBefore=_bridge.ActiveSceneId;
        var fullAuditBefore=_world.GetMeta("addressAccessAuditCompleted",false).AsBool();
        var attachmentCountBefore=_world.GetMeta("addressGraphAttachmentCount",0L).AsInt64();
        Require(original.State=="verified"&&originalCommit>=0&&originalAttach>=originalCommit
            &&registry.Graph.Roads.ContainsKey("access/"+accessId)
            &&registry.Graph.Edges.Values.Any(edge=>edge.RoadId=="access/"+accessId),
            "retry begins with a real committed and attached path, graph edges and matching provenance");
        var fixture=new StaticBody3D{Name="DiagnosticStandingAccessRefusal",CollisionLayer=2,CollisionMask=0};
        var shape=new BoxShape3D{Size=new Vector3(1.4f,2.4f,1.4f)};
        fixture.AddChild(new CollisionShape3D{Shape=shape});_world.AddChild(fixture);
        fixture.GlobalPosition=V(registry.AccessPoints[accessId].Position)+Vector3.Up*1.1f;
        try
        {
            await Frames(2);
            await ProbeSelectedAccess(audit,accessId,started);
            var refused=registry.AccessPoints[accessId].State;
            Require(refused.StartsWith("blocked:",StringComparison.Ordinal),"real standing-body obstruction rejects the previously verified entrance");
            var immediateRoadAbsent=RoadAbsent();
            var immediateCommit=Stamp("addressAccessCommitFrames");var immediateAttach=Stamp("addressAccessAttachFrames");
            var propsUnchanged=_bridge.SelectWorldProps().GetRawText()==propsBefore&&_bridge.ActiveSceneId==sceneBefore;
            var fullAuditUnchanged=_world.GetMeta("addressAccessAuditCompleted",false).AsBool()==fullAuditBefore;
            var immediateAttachmentCount=_world.GetMeta("addressGraphAttachmentCount",0L).AsInt64();
            _checks.Add(new{kind="actual-access-refusal-before-manual-attachment",accessId,refused,originalCommit,originalAttach,
                immediateRoadAbsent,immediateCommit,immediateAttach,propsUnchanged,fullAuditUnchanged,
                attachmentCountBefore,immediateAttachmentCount,pendingPublication=_world.AddressGraphAttachmentPending});
            Require(immediateRoadAbsent&&immediateCommit<0&&immediateAttach<0&&!_world.AddressGraphAttachmentPending
                &&immediateAttachmentCount==attachmentCountBefore+1&&propsUnchanged&&fullAuditUnchanged
                &&registry.AccessPoints[accessId]==(original with{State=refused}),
                "physical refusal immediately removes the road, edges and stamps without moving the access or changing progress");
            _world.AttachVerifiedAddressAccessPaths();
            var origin=registry.Graph.NodeAt(registry.Graph.Roads["authored/main-axis"].Points[0])!;
            Require(registry.AccessPoints[accessId].State==refused&&RoadAbsent()
                &&Stamp("addressAccessCommitFrames")<0&&Stamp("addressAccessAttachFrames")<0
                &&registry.DiagnosticRoute(origin,"ADR-H009",SettlementTravelMode.Foot).Count==0,
                "graph attachment cannot restore a rejected cached access path");
            _checks.Add(new{kind="actual-access-retry-refusal",accessId,refused,fixture=fixture.GetPath().ToString(),
                physicalObstacleIntroduced=true,directStateMutation=false,cachedRouteInvalidated=true});
        }
        finally
        {
            fixture.QueueFree();await Frames(2);shape.Dispose();
        }
        Require(!GodotObject.IsInstanceValid(fixture),"temporary standing-body obstruction is removed before the restored path check");
        await ProbeSelectedAccess(audit,accessId,started);
        Require(registry.AccessPoints[accessId].State=="pending-graph-attachment","removing the actual obstruction permits a fresh physical access proof");
        var restoredCommit=Stamp("addressAccessCommitFrames");
        Require(restoredCommit>originalCommit&&Stamp("addressAccessAttachFrames")<0,
            "fresh physical retry has a new commit frame and no inherited attachment stamp");
        _world.AttachVerifiedAddressAccessPaths();
        Require(registry.AccessPoints[accessId].State=="verified","fresh physical proof restores the same access identity after a real refusal");
        Require(Stamp("addressAccessAttachFrames")>=restoredCommit&&Stamp("addressAccessAttachFrames")>originalAttach
            &&registry.Graph.Roads.ContainsKey("access/"+accessId)
            &&registry.Graph.Edges.Values.Any(edge=>edge.RoadId=="access/"+accessId)
            &&_world.GetMeta("addressAccessAuditCompleted",false).AsBool()==fullAuditBefore
            &&_bridge.SelectWorldProps().GetRawText()==propsBefore&&_bridge.ActiveSceneId==sceneBefore,
            "restored attachment has fresh provenance and graph edges while progress and full-audit status remain unchanged");
    }

    private async Task KnockSelectedDoor(string addressId)
    {
        var registry=_world.AddressRegistry!;
        var access=registry.AccessPoints[registry.Addresses[addressId].AccessId];
        var at=V(access.Position);
        Require(_player.CanStandAt(at),addressId+": selected door approach admits the real standing controller");
        var target=Descendants(_world).OfType<InteractionTarget>().Single(t=>t.InteractionId=="urman.address:knock/"+addressId);
        var action=target.PresentationRepeat??throw new InvalidOperationException("Selected residential door has no existing knock action.");
        var before=_bridge.SelectWorldProps().GetRawText();var observed=0;
        target.PresentationRepeat=()=>{observed++;action();};
        try
        {
            _player.ApplyZoneSpawn(at,0);await Frames(4);
            var camera=_player.GetNode<Camera3D>("Head/Camera3D");
            await AimFromCurrentCamera(camera,target.GlobalPosition);
            var ray=_player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay");ray.ForceRaycastUpdate();
            Require(ray.GetCollider()==target&&target.IsAvailable()&&_player.FocusedInteractionId==target.InteractionId,
                addressId+": ordinary interaction ray and passive focus reach the actual selected door");
            await PressInteract();
            Require(observed==1&&_player.InteractionNoticeActive,addressId+": one mapped input invokes one ordinary knock response");
            Require(before==_bridge.SelectWorldProps().GetRawText(),addressId+": unsuccessful knock invents no address or quest knowledge");
            _checks.Add(new{kind="actual-residential-knock",addressId,observed,feet=P(_player.GlobalPosition),target=target.GetPath().ToString(),
                diagnosticApproach=true,directActionInvocation=false,existingCallbackRetained=true,worldPropsUnchanged=true});
            await Capture("12_"+addressId+"_manual_knock");
        }
        finally{target.PresentationRepeat=action;}
    }

    private async Task ReadNotebookSaveLoad(string addressId,string capturePrefix="")
    {
        Require(!_player.ModalOpen,"ordinary player controls are available for the deliberate read");
        var target=Descendants(_world).OfType<InteractionTarget>().Single(t=>t.InteractionId=="urman.address:read/"+addressId);
        var sign=(AddressSignVisualComponent)target.GetParent();
        var camera=_player.GetNode<Camera3D>("Head/Camera3D");
        var approached=false;
        var fixtures=_scope=="crowded-addresses"?new[]{(Distance:0f,Lateral:0f)}:
            (from distance in new[]{.75f,.95f,1.15f,1.35f,1.75f,2.1f}
            from lateral in new[]{0f,-.40f,.40f,-.80f,.80f}
            select (Distance:distance,Lateral:lateral)).ToArray();
        foreach(var fixture in fixtures)
        {
            var distance=fixture.Distance;
            var candidate=_scope=="crowded-addresses"?_player.GlobalPosition:
                Act1ConnectedWorld.AddressGround(sign.GlobalPosition+sign.GlobalBasis.Z.Normalized()*distance+sign.GlobalBasis.X.Normalized()*fixture.Lateral);
            var fits=_player.CanStandAt(candidate);
            // Mirror CanStandAt's clearance probe so recorded owners explain
            // its actual refusal, rather than a differently sized test capsule.
            using var capsule=new CapsuleShape3D{Radius=.35f,Height=1.785f};
            using var standing=new PhysicsShapeQueryParameters3D{Shape=capsule,CollisionMask=_player.CollisionMask,Margin=.002f,
                Exclude=new global::Godot.Collections.Array<Rid>{_player.GetRid()},Transform=new(Basis.Identity,candidate+Vector3.Up*.91f)};
            var contacts=_player.GetWorld3D().DirectSpaceState.IntersectShape(standing,12).Select(h=>new
                {owner=(h["collider"].AsGodotObject() as Node)?.GetPath().ToString(),shape=h["shape"].AsInt64()}).ToArray();
            _checks.Add(new{kind="manual-read-position",addressId,distance,lateral=fixture.Lateral,candidate=P(candidate),canStand=fits,contacts,
                diagnosticApproach=true,humanNavigationTest=false});
            if(!fits)continue;
            if(_scope!="crowded-addresses")_player.ApplyZoneSpawn(candidate,0);
            await Frames(4);
            await AimFromCurrentCamera(camera,sign.GlobalPosition);
            using var ray=PhysicsRayQueryParameters3D.Create(camera.GlobalPosition,camera.GlobalPosition-camera.GlobalBasis.Z*2.4f,7);
            ray.Exclude=new global::Godot.Collections.Array<Rid>{_player.GetRid()};
            var hit=_player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var toward=(sign.GlobalPosition-camera.GlobalPosition).Normalized();
            _checks.Add(new{kind="manual-read-ray",addressId,distance,lateral=fixture.Lateral,player=P(_player.GlobalPosition),camera=P(camera.GlobalPosition),target=P(sign.GlobalPosition),
                rayDirection=P(-camera.GlobalBasis.Z),alignment=(-camera.GlobalBasis.Z).Dot(toward),targetDistance=camera.GlobalPosition.DistanceTo(sign.GlobalPosition),
                hit=hit.Count>0?(hit["collider"].AsGodotObject() as Node)?.GetPath().ToString():null,expected=target.GetPath().ToString()});
            if(hit.Count>0 && hit["collider"].AsGodotObject()==target){approached=true;break;}
        }
        Require(approached,"real controller ray reaches the plate from a supported local fixture");
        if(_scope=="h045-layout")await Capture("h045_controller_plate");
        if(_scope is "crowded-addresses" or "far-bank")await Capture(capturePrefix+"controller_plate");
        var before=_bridge.SelectWorldProps();
        Require(!before.TryGetProperty("address/"+addressId,out _),"plate starts unread before deliberate input");
        await PressInteract();
        for(var i=0;i<180 && !_bridge.SelectWorldProps().TryGetProperty("address/"+addressId,out _);i++)await Frames(1);
        Require(ReadRecorded(addressId),"one actual interact input records the read address");
        var entries=_bridge.NotebookAddresses();
        Require(entries.Count(e=>e.EntryId=="notebook/address/"+addressId)==1,"notebook contains exactly one matching address");
        var journal=(JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        journal.Open(_bridge);
        var section=Descendants(journal).OfType<OptionButton>().Single(b=>b.Name=="NotebookSection");
        section.Select(2);journal.RefreshProjection();
        Descendants(journal).OfType<TabBar>().Single(t=>t.Name=="Tabs").CurrentTab=0;
        await Frames(3);await Capture(capturePrefix+"05_manual_read_notebook");
        journal._UnhandledInput(new InputEventAction{Action="ui_cancel",Pressed=true});await Frames(5);
        Require(!_player.ModalOpen,"notebook closes back to ordinary controls");
        await CaptureNotebookMap(capturePrefix+"06_located_map_large",_bridge.LocatedAddressIds().Count,true);
        var slot=capturePrefix.Length==0?"address-world-smoke":"address-world-smoke-"+addressId.ToLowerInvariant();
        Require(await _bridge.SaveSlotAsync(slot),"save actual read state through existing snapshot owner");
        Require(await _bridge.LoadSlotAsync(slot),"load actual read state through existing snapshot owner");
        await Frames(8);
        Require(ReadRecorded(addressId)&&_bridge.NotebookAddresses().Count(e=>e.EntryId=="notebook/address/"+addressId)==1,"read knowledge and notebook survive load without duplicates");
        _checks.Add(new{kind="manual-read-save-load",addressId,props=_bridge.SelectWorldProps(),notebook=_bridge.NotebookAddresses(),diagnosticApproach=true,humanNavigationTest=false});
    }
    private async Task CaptureNotebookMap(string name,int expected,bool enlarged=false)
    {
        var journal=(JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        var original=_player.Accessibility;
        try
        {
            if(enlarged)AccessibilityPresentation.ApplyToTree(GetTree(),original with{TextScale=1.6,HighContrast=true});
            journal.Open(_bridge);
            Descendants(journal).OfType<TabBar>().Single(t=>t.Name=="Tabs").CurrentTab=3;
            await Frames(4);
            var map=Descendants(journal).OfType<SettlementMapControl>().Single();
            Require(map.DisplayedAddressCount==expected&&map.EmptyPageVisible==(expected==0),"notebook map shows only actually located houses: "+name);
            Check(map.GetGlobalRect().Size.Y>=200,"map retains usable drawing height: "+name);
            Check(journal.GetNode<Control>("Screen/Book").GetGlobalRect().Encloses(map.GetGlobalRect()),"map bounds stay inside the notebook: "+name);
            await Capture(name);
            _checks.Add(new{kind="notebook-map",name,located=map.DisplayedAddressCount,empty=map.EmptyPageVisible,enlarged,
                textScale=map.GetMeta("accessibilityTextScale").AsDouble(),size=new{x=map.Size.X,y=map.Size.Y},humanNavigationTest=false});
        }
        finally
        {
            journal._UnhandledInput(new InputEventAction{Action="ui_cancel",Pressed=true});
            if(enlarged)AccessibilityPresentation.ApplyToTree(GetTree(),original);
            await Frames(4);
        }
    }
    private bool ReadRecorded(string id)=>_bridge.SelectWorldProps().TryGetProperty("address/"+id,out var state)
        &&state.TryGetProperty("read",out var read)&&read.ValueKind==JsonValueKind.True;
    private async Task PressInteract(){Input.ActionPress("interact");await Frames(2);Input.ActionRelease("interact");await Frames(3);}
    private async Task AimFromCurrentCamera(Camera3D camera,Vector3 target)
    {
        // The production camera is offset from the head pivot. Each look changes
        // its origin; converge from the actual new pose rather than the old one.
        for(var i=0;i<6;i++)
        {
            var delta=target-camera.GlobalPosition;
            _player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y,new Vector2(delta.X,delta.Z).Length())),Mathf.RadToDeg(Mathf.Atan2(-delta.X,-delta.Z)));
            await Frames(1);
        }
    }
    private async Task Capture(string name)
    {
        var path=Path.Combine(_output,name+".png");
        if(File.Exists(path))throw new IOException("Refusing to overwrite historical capture: "+path);
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this,"address/"+name);
        using var shot=GetViewport().GetTexture().GetImage();
        if(shot.SavePng(path)!=Error.Ok)throw new IOException(path);
    }
    private void Check(bool value,string message){_checks.Add(new{kind="check",message,passed=value});if(!value)_failures.Add(message);}
    private void Require(bool value,string message){Check(value,message);if(!value)throw new InvalidOperationException(message);}
    private async Task Frames(int count){for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private static IEnumerable<Node> Descendants(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    private static object P(Vector3 p)=>new{x=p.X,y=p.Y,z=p.Z};
    private static object B(Basis b)=>new{x=P(b.X),y=P(b.Y),z=P(b.Z)};
    private static Vector3 V(SettlementPoint p)=>new((float)p.X,(float)p.Y,(float)p.Z);
}
