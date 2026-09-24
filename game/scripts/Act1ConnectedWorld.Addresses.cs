using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public sealed record AddressBuildingRegistration(Node3D Building,string SourceKey,string BuildingId,string ParcelId,string AddressId,string StreetId,string Number,string Cadastral,Vector3 AccessPoint,Vector3 SignPoint,Vector3 Outward,string Role="public",IReadOnlyList<Vector3>? ApproachPath=null,Vector3? SignOutward=null,string? SignSurfaceName=null);

public partial class Act1ConnectedWorld
{
    public SettlementRegistry? AddressRegistry { get; private set; }
    public event Action<string>? AddressRead;
    public IReadOnlyList<SettlementIssue> AddressImportIssues => _addressImportIssues;
    public IReadOnlyList<SettlementIssue> AddressInactiveBindings => _addressInactiveBindings;
    private readonly List<SettlementIssue> _addressImportIssues=[];
    private readonly List<SettlementIssue> _addressInactiveBindings=[];
    private readonly List<AddressBuildingRegistration> _addressExtraBuildings=[];
    private readonly List<(Node3D Building,string SourceKey,string PrimaryAddressId,string Role)> _addressInheritedBuildings=[];
    private readonly List<(string AccessId,Vector3 Point)> _addressPendingAccess=[];
    private readonly Dictionary<string,Vector3[]> _addressVerifiedPaths=new(StringComparer.Ordinal);
    private bool _addressGraphNeedsAttachment;
    private readonly global::Godot.Collections.Dictionary _addressAccessCommitFrames=new();
    private readonly global::Godot.Collections.Dictionary _addressAccessAttachFrames=new();
    private long _addressGraphAttachmentCount;
    internal bool AddressGraphAttachmentPending=>_addressGraphNeedsAttachment;
    internal ulong AddressGraphAttachmentFrame { get; private set; }=ulong.MaxValue;
    private readonly Dictionary<string,Vector3[]> _addressApproachPaths=new(StringComparer.Ordinal);
    private readonly List<AddressSignVisualComponent> _addressSigns=[];
    private Node3D? _addressRoot;

    /// <summary>Register an existing building before BuildAddressRegistry. Geometry,
    /// collision, narrative and saving remain with their current owners.</summary>
    public void RegisterAddressedBuilding(AddressBuildingRegistration registration)
    {
        if(AddressRegistry is not null)throw new InvalidOperationException("Register buildings before importing the address registry.");
        if(_addressExtraBuildings.Any(r=>r.SourceKey==registration.SourceKey||r.AddressId==registration.AddressId))
            throw new InvalidOperationException("Duplicate public-building registration.");
        _addressExtraBuildings.Add(registration);
    }
    /// <summary>Authored waypoints are requests to the physical verifier. They
    /// become graph geometry only after every segment has been walked by it.</summary>
    public IReadOnlyList<Vector3> AddressApproachPath(string accessId)
        =>_addressApproachPaths.TryGetValue(accessId,out var path)?Array.AsReadOnly(path):Array.Empty<Vector3>();
    public void RegisterAddressInheritedBuilding(Node3D building,string sourceKey,string primaryAddressId,string role)
    {
        if(AddressRegistry is not null)throw new InvalidOperationException("Register inherited buildings before importing addresses.");
        if(string.IsNullOrWhiteSpace(sourceKey)||_addressInheritedBuildings.Any(r=>r.SourceKey==sourceKey||r.Building==building))
            throw new InvalidOperationException("Inherited building requires a unique committed source key.");
        _addressInheritedBuildings.Add((building,sourceKey,primaryAddressId,role));
    }

    public void BuildAddressRegistry()
    {
        if(AddressRegistry is not null)return;
        RegisterAuthoredAddressAuxiliaries();
        var registry=new SettlementRegistry();
        using var manifest=JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString("res://content/urman.settlement.addresses.v1.json"));
        foreach(var row in manifest.RootElement.GetProperty("streets").EnumerateArray())
        {
            var origin=row.GetProperty("origin").EnumerateArray().Select(x=>x.GetDouble()).ToArray();
            registry.AddStreet(new(row.GetProperty("id").GetString()!,row.GetProperty("tatar").GetString()!,row.GetProperty("russian").GetString()!,
                new(origin[0],origin[1],origin[2]),row.GetProperty("oddSide").GetString()!,row.GetProperty("protected").GetBoolean()));
        }
        // A hidden/replaced authored building still owns its number. Reserve the
        // complete source manifest before visibility filtering and extra buildings.
        foreach(var row in manifest.RootElement.GetProperty("buildings").EnumerateArray())
            registry.ReserveAuthoredAddress(row.GetProperty("addressId").GetString()!,row.GetProperty("streetId").GetString()!,row.GetProperty("number").GetString()!);
        AddressRegistry=registry;
        _addressRoot=new Node3D{Name="SettlementAddressPresentation"};
        _addressRoot.SetMeta("dataSource","res://content/urman.settlement.addresses.v1.json");
        _addressRoot.SetMeta("stateOwner","RuntimeBridge");
        AddChild(_addressRoot);
        ImportAddressRoads(registry);
        var imported=new HashSet<Node3D>();
        foreach(var row in manifest.RootElement.GetProperty("buildings").EnumerateArray())
        {
            var name=row.GetProperty("sourceName").GetString()!;
            var replacement=_addressExtraBuildings.SingleOrDefault(e=>e.Building.Name==name);
            if(replacement is not null)
            {
                // A rebuilt room keeps its authored manifest position as well
                // as its registered IDs. Do not put it behind every background
                // house merely because its geometry has an explicit owner.
                if(imported.Add(replacement.Building))ImportAddressBuilding(replacement);
                continue;
            }
            var node=FindDescendants<Node3D>(this).FirstOrDefault(n=>n.Name==name && n.IsVisibleInTree());
            if(node is null)continue; // Retired/hidden source bindings stay reserved in data.
            // HidePresentationNode hides the meshes, not necessarily their root.
            // The old greybox root must not become a second inhabited house.
            if(node.HasMeta("authoredExteriorKitSuppressionReason") && !FindDescendants<MeshInstance3D>(node).Any(m=>m.Mesh is not null&&m.IsVisibleInTree()))
            {
                _addressInactiveBindings.Add(new("RESERVED_REPLACED_SOURCE",row.GetProperty("addressId").GetString()!,name+": "+node.GetMeta("authoredExteriorKitSuppressionReason").AsString()));
                node.SetMeta("addressBindingInactive",true);
                imported.Add(node);
                continue;
            }
            if(!TryAddressDoor(node,row.TryGetProperty("primaryDoor",out var doorName)?doorName.GetString():null,out var door,out var outward))
            {
                _addressImportIssues.Add(new("DOOR_NOT_IMPORTED",row.GetProperty("addressId").GetString()!,node.GetPath().ToString()));
                continue;
            }
            var access=AddressGround(door+outward*1.05f);
            if(row.TryGetProperty("accessXZ",out var accessRow))
            {
                var a=accessRow.EnumerateArray().Select(x=>x.GetSingle()).ToArray();access=AddressGround(new(a[0],0,a[1]));
            }
            // The authored door offset can land inside the house's own wall or
            // a porch volume: the audit then correctly reports that no human
            // can stand there. Explicit accessXZ overrides (see
            // URMAN_ADDRESS_PROPOSE_XZ diagnostics in AddressWorldSmokeTest)
            // carry the corrected, audited points as checked-in data; import
            // itself never drags points along stale transforms.
            var addressId=row.GetProperty("addressId").GetString()!;
            if(addressId=="ADR-BABAI")
            {
                var target=FindDescendants<InteractionTarget>(this).FirstOrDefault(t=>t.Name=="HouseDoor");
                if(target is not null)access=AddressGround(target.GlobalPosition+outward*.85f);
            }
            else if(addressId=="ADR-FAP")
            {
                var target=FindDescendants<InteractionTarget>(this).FirstOrDefault(t=>t.TargetZoneId=="fap_clinic");
                if(target is not null)access=AddressGround(target.GlobalPosition+outward*.85f);
            }
            string? signSurface=row.TryGetProperty("signSurface",out var surfaceRow)?surfaceRow.GetString():null;
            var signOutward=outward;
            if(signSurface is not null)
            {
                var surface=FindDescendants<MeshInstance3D>(node).Single(n=>n.Name==signSurface&&n.Mesh is not null&&n.IsVisibleInTree());
                // Explicit facade policy accepts only the two source-confirmed
                // exterior right-hand walls. It is separate from the door axis.
                signOutward=surface.GlobalBasis*Vector3.Right;signOutward.Y=0;signOutward=signOutward.Normalized();
            }
            var mounted=AddressFacadeMount.TryFind(node,door,signOutward,this,out var sign,out var mountOwner,out var mountFailure,signSurface);
            if(!mounted)
                _addressImportIssues.Add(new("SIGN_MOUNT_NOT_FOUND",addressId,node.GetPath()+": "+mountFailure));
            node.SetMeta("addressSignMountAvailable",mounted);
            node.SetMeta("addressSignMountOwner",mountOwner);
            var registration=new AddressBuildingRegistration(node,row.GetProperty("sourceKey").GetString()!,row.GetProperty("buildingId").GetString()!,
                row.GetProperty("parcelId").GetString()!,addressId,row.GetProperty("streetId").GetString()!,row.GetProperty("number").GetString()!,
                row.GetProperty("cadastral").GetString()!,access,sign,outward,row.GetProperty("role").GetString()!,SignOutward:signOutward,SignSurfaceName:signSurface);
            ImportAddressBuilding(registration);
            imported.Add(node);
        }
        // Only genuinely new source bindings remain after the manifest pass.
        foreach(var registration in _addressExtraBuildings)
            if(imported.Add(registration.Building))ImportAddressBuilding(registration);
        if(manifest.RootElement.TryGetProperty("addressAliases",out var aliases))
            foreach(var alias in aliases.EnumerateObject())
            {
                var canonical=alias.Value.GetString()!;
                if(registry.Addresses.ContainsKey(canonical))registry.AddAddressAlias(alias.Name,canonical);
                else _addressImportIssues.Add(new("ALIAS_TARGET_NOT_IMPORTED",alias.Name,canonical));
            }
        foreach(var inherited in _addressInheritedBuildings)
        {
            if(!registry.TryResolve(inherited.PrimaryAddressId,out var address))throw new InvalidOperationException("Inherited building has no imported primary address: "+inherited.PrimaryAddressId);
            ImportAddressAuxiliary(inherited.Building,inherited.SourceKey,registry.Buildings[address.BuildingId],inherited.Role);
            imported.Add(inherited.Building);
        }
        ImportStandaloneAddressParcels(imported);
        // Every visible dwelling source must have an explicit persistent binding.
        foreach(var node in FindDescendants<Node3D>(this).Where(n=>n.IsVisibleInTree() && n.HasMeta("logicalAnchor") && !n.HasMeta("presentationOnlyInstance")))
        {
            if(imported.Contains(node))continue;
            var dwelling=node.GetChildren().OfType<Node3D>().Any(child=>child.Name.ToString().Contains("_Dwelling",StringComparison.Ordinal)
                || child.Name=="DwellingFacade_TimberPlaster");
            if(dwelling)_addressImportIssues.Add(new("UNBOUND_DWELLING",node.Name.ToString(),node.GetMeta("logicalAnchor").AsString()));
        }
        ImportAddressAuxiliaries(imported);
        ImportAddressConstraints(registry);
        registry.Graph.Rebuild(registry.Streets);
        SetMeta("addressAccessCommitFrames",_addressAccessCommitFrames);
        SetMeta("addressAccessAttachFrames",_addressAccessAttachFrames);
        SetMeta("addressGraphAttachmentFrame",-1L);
        SetMeta("addressGraphAttachmentCount",0L);
        var audit=new AddressAccessVerifier{Name="AddressAccessVerification"};
        audit.Initialize(this,_addressPendingAccess.ToArray());
        AddChild(audit);
        SetMeta("addressRegistryVersion",SettlementRegistry.RegistryVersion);
        SetMeta("addressRegistryCount",registry.Addresses.Count);
        SetMeta("addressImportIssues",_addressImportIssues.Count);
        SetMeta("addressInactiveBindings",_addressInactiveBindings.Count);
    }

    private void RegisterAuthoredAddressAuxiliaries()
    {
        // These are committed import bindings, transcribed from the actual
        // AddAct1AuthoredExteriorParcel calls and named holding declarations in
        // Act1ConnectedWorld. A shared presentation parent contains many plots;
        // neither sibling count nor nearest-house distance establishes ownership.
        var bindings=new (string Source,string Primary,string Evidence)[]
        {
            ("ArrivalEastNearAuthoredShed","ADR-H009","ArrivalEastNearAuthoredFacade parcel call"),
            ("ArrivalForwardWestShed","ADR-H016","ArrivalForwardWestFacade parcel call"),
            ("ArrivalForwardEastShed","ADR-H017","ArrivalForwardEastFacade parcel call"),
            ("ArrivalFarWestShed","ADR-H003","ArrivalFarWestFacade parcel call"),
            ("PerimeterWestArrivalShed","ADR-H006","PerimeterWestArrivalFacade parcel call"),
            ("PerimeterEastArrivalShed","ADR-H007","PerimeterEastArrivalFacade parcel call"),
            ("EastStreetFarHoldingShed","ADR-H037","EastStreetFarHolding fenced plot and storage declaration"),
            ("ZiratWestHoldingShed","ADR-H027","ZiratWestLateralHouse enclosed holding and gate"),
            ("PerimeterEastStreetShed","ADR-H035","MainStreetEastNearMidHouse explicitly owns this holding's storage"),
            ("EastStreetSideClosureShed","ADR-H035","EastStreetSideClosureParcel explicitly belongs to the near-mid holding"),
            ("BabaiEastDepthServiceShed","ADR-BABAI","house_old_pc@east-depth-service-yard authored owner")
        };
        foreach(var binding in bindings)
        {
            var source=FindDescendants<Node3D>(this).SingleOrDefault(n=>n.Name==binding.Source&&n.IsVisibleInTree());
            if(source is null||_addressInheritedBuildings.Any(r=>r.Building==source))continue;
            source.SetMeta("addressParcelOwnershipEvidence",binding.Evidence);
            RegisterAddressInheritedBuilding(source,"act1/outbuilding/"+binding.Source,binding.Primary,"shed");
        }
    }

    private void ImportAddressBuilding(AddressBuildingRegistration r)
    {
        var registry=AddressRegistry!;
        var accessId="ACC-"+r.AddressId;
        if(r.ApproachPath is {Count:>0} approach)
        {
            if(approach.Any(p=>!p.IsFinite())||approach[^1].DistanceTo(r.AccessPoint)>.03f)
                throw new InvalidOperationException("Authored approach must contain finite world points and finish at its real access point: "+r.AddressId);
            _addressApproachPaths.Add(accessId,approach.ToArray());
        }
        var footprint=AddressFootprint(r.Building);
        var parcelPolygon=AddressParcelPolygon(footprint,AddressPoint(r.AccessPoint));
        var parcel=new SettlementParcel(r.ParcelId,r.Cadastral.Split('-')[1],r.Cadastral,r.BuildingId,accessId,parcelPolygon);
        var record=new AddressRecord(r.AddressId,r.BuildingId,r.ParcelId,r.StreetId,r.Number,accessId,[]);
        registry.Register(new(r.BuildingId,r.SourceKey,r.ParcelId,r.AddressId,r.Role,AddressPoint(r.Building.GlobalPosition),footprint),
            parcel,record,new(accessId,r.BuildingId,"entrance",AddressPoint(r.AccessPoint),"","pending-physics"));
        foreach(var (key,value) in new[]{("building_id",r.BuildingId),("parcel_id",r.ParcelId),("address_id",r.AddressId),("settlement_source_key",r.SourceKey)})
            r.Building.SetMeta(key,value);
        r.Building.SetMeta("addressAccessPoint",r.AccessPoint);
        if(r.SignSurfaceName is not null)r.Building.SetMeta("addressSignSurfaceName",r.SignSurfaceName);
        _addressPendingAccess.Add((accessId,r.AccessPoint));
        var doorBasis=new Basis(Vector3.Up,Mathf.Atan2(r.Outward.X,r.Outward.Z));
        if(!r.Building.HasMeta("addressSignMountAvailable") || r.Building.GetMeta("addressSignMountAvailable").AsBool())
        {
            var plate=new AddressSignVisualComponent();
            _addressRoot!.AddChild(plate);
            plate.Bind(registry,r.AddressId);
            plate.GlobalPosition=r.SignPoint;
            var signOutward=r.SignOutward??r.Outward;
            plate.GlobalBasis=new Basis(Vector3.Up,Mathf.Atan2(signOutward.X,signOutward.Z));
            _addressSigns.Add(plate);
            var read=new InteractionTarget{Name="Read_"+r.AddressId,InteractionId="urman.address:read/"+r.AddressId,Prompt="Прочитать табличку",
                CollisionLayer=4,CollisionMask=0,PresentationRepeatAvailable=()=>true};
            plate.AddChild(read);
            read.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(1.20f,.44f,.07f)}});
            read.PresentationRepeat=()=>
            {
                AddressRead?.Invoke(r.AddressId);
                (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.NotifyTraversal(registry.FormatAddress(r.AddressId));
            };
        }
        // Existing hero/public interactions continue to own their real doors.
        var hasDoorAction=FindDescendants<InteractionTarget>(this).Any(t=>t.GlobalPosition.DistanceTo(r.AccessPoint+Vector3.Up)<2.6f
            && (t.TargetZoneId.Length>0 || t.DialogueId.Length>0));
        if(r.Role=="residential" && r.AddressId!="ADR-BABAI" && !hasDoorAction)
        {
            var knock=new InteractionTarget{Name="Knock_"+r.AddressId,InteractionId="urman.address:knock/"+r.AddressId,
                Prompt="Постучать",CollisionLayer=4,CollisionMask=0,PresentationRepeatAvailable=()=>true};
            _addressRoot!.AddChild(knock);
            knock.GlobalPosition=r.AccessPoint-r.Outward*.8f+Vector3.Up;
            knock.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.85f,1.65f,.20f)}});
            knock.GlobalBasis=doorBasis;
            knock.PresentationRepeat=()=>
            {
                UiFoley.PlayWorld(knock,knock.GlobalPosition,"door_creak");
                (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.NotifyTraversal(
                    "Никто не открыл. "+registry.FormatAddress(r.AddressId)+". Можно свериться с записной книжкой.");
            };
        }
    }
    private void ImportAddressAuxiliaries(HashSet<Node3D> primary)
    {
        var registry=AddressRegistry!;
        foreach(var node in FindDescendants<Node3D>(this).Where(n=>n.IsVisibleInTree() && n.HasMeta("logicalAnchor") && !n.HasMeta("presentationOnlyInstance")))
        {
            if(primary.Contains(node))continue;
            var name=node.Name.ToString();
            if(!node.GetChildren().OfType<Node3D>().Any(n=>n.Name.ToString().StartsWith("OutbuildingShed_",StringComparison.Ordinal)))continue;
            // Shared authored parcel parent is ownership. Distance is not identity:
            // inserting or moving a neighbour must never transfer a shed to it.
            var owners=primary.Where(p=>p.GetParent()==node.GetParent() && p.HasMeta("building_id") && !p.HasMeta("addressInherited")).ToArray();
            if(owners.Length!=1){_addressImportIssues.Add(new("AUXILIARY_OWNER_UNBOUND",name,"Explicit authored parcel ownership is required."));continue;}
            var owner=owners[0];
            var ownerId=owner.GetMeta("building_id").AsString();
            var main=registry.Buildings[ownerId];
            var source="act1/outbuilding/"+name;
            ImportAddressAuxiliary(node,source,main,"shed");
        }
    }
    private void ImportAddressAuxiliary(Node3D node,string source,SettlementBuilding main,string role)
    {
        var id=SettlementRegistry.StableId("BLD",source);
        AddressRegistry!.RegisterAuxiliary(new(id,source,main.ParcelId,main.AddressId,role,AddressPoint(node.GlobalPosition),AddressFootprint(node)));
        node.SetMeta("building_id",id);node.SetMeta("parcel_id",main.ParcelId);node.SetMeta("address_id",main.AddressId!);
        node.SetMeta("addressInherited",true);node.SetMeta("settlement_source_key",source);
    }
    private void ImportAddressRoads(SettlementRegistry registry)
    {
        void Road(string id,string street,IReadOnlyList<Vector2> points,double width,SettlementTravelMode modes)
        {
            registry.Graph.AddRoad(new(id,street,points.Select(p=>AddressPoint(AddressGround(new(p.X,0,p.Y)))).ToArray(),width,"snow_trampled",modes));
        }
        Road("authored/main-axis","tukay",AgentBAct1Layout.MainRoadAxis,5.6,SettlementTravelMode.All);
        Road("authored/fap-axis","urman",AgentBAct1Layout.FapBranchAxis,4.6,SettlementTravelMode.All);
        Road("authored/zirat-axis","tukay",AgentBAct1Layout.ZiratRoadAxis,4.2,SettlementTravelMode.All);
        Road("authored/kara-axis","",AgentBAct1Layout.KaraRoadAxis,3.5,SettlementTravelMode.Foot|SettlementTravelMode.HorseCart);
        Road("authored/house-path","tukay",AgentBAct1Layout.HousePathAxis,1.15,SettlementTravelMode.Foot);
        foreach(var connector in Act1WorldLayout.Connectors)
            Road("connector/"+connector.ConnectorId,connector.ConnectorId.Contains("fap",StringComparison.Ordinal)?"urman":connector.ConnectorId.Contains("kara",StringComparison.Ordinal)?"":"tukay",
                [new(connector.Start.X,connector.Start.Z),new(connector.End.X,connector.End.Z)],connector.Width,
                connector.ConnectorId.Contains("kara",StringComparison.Ordinal)?SettlementTravelMode.Foot|SettlementTravelMode.HorseCart:SettlementTravelMode.All);
        Road("authored/ravine-bridge-approach","urman",RavineBridgeApproach,1.8,SettlementTravelMode.Foot);
        // The far bank's lane is its own piece of graph: the bridge span is gone.
        Road("authored/yar-lane","yar",RavineFarLane,3.2,SettlementTravelMode.All);
        Road("authored/yar-lane-south","yar",RavineFarLaneSouth,2.8,SettlementTravelMode.All);
        registry.Graph.Rebuild(registry.Streets);
        foreach(var (name,street) in new[]{("ZiratWestHoldingAccess","usal"),("EastStreetPlotAccessPath","urman"),("ConnectiveWestHouseDrive","tukay"),("ReturnEastFarmDrive","tukay"),("FapClinicEntryPath","urman")})
        {
            var mesh=FindDescendants<MeshInstance3D>(this).FirstOrDefault(m=>m.Name==name && m.Mesh is ArrayMesh && m.IsVisibleInTree());
            if(mesh?.Mesh is not ArrayMesh array)continue;
            var vertices=array.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if(vertices.Length<10 || vertices.Length%5!=0){_addressImportIssues.Add(new("PATH_MESH_FORMAT",name,"Expected existing five-column landform ribbon."));continue;}
            // Import the centre column of the actual rendered ribbon.
            var points=new List<SettlementPoint>();
            for(var i=2;i<vertices.Length;i+=20)points.Add(AddressPoint(AddressGround(mesh.GlobalTransform*vertices[i])));
            var last=AddressPoint(AddressGround(mesh.GlobalTransform*vertices[^3]));
            if(points[^1].DistanceXZ(last)>.01)points.Add(last);
            if(points.Count<2)continue;
            var nearest=registry.Graph.Nearest(points[0]);
            if(nearest is { } link && link.Distance>.01 && link.Distance<5)
                registry.Graph.AddRoad(new("surface-link/"+name,street,[link.Point,points[0]],1.1,"snow_trampled",SettlementTravelMode.Foot));
            registry.Graph.AddRoad(new("surface/"+name,street,points,1.1,"snow_trampled",SettlementTravelMode.Foot));
        }
        foreach(var node in FindDescendants<Node3D>(this).Where(n=>n.HasMeta("routePolyline")))
        {
            var points=node.GetMeta("routePolyline").AsString().Split('|').Select(x=>x.Split(','))
                .Where(x=>x.Length==2).Select(x=>AddressPoint(AddressGround(new(float.Parse(x[0],CultureInfo.InvariantCulture),0,float.Parse(x[1],CultureInfo.InvariantCulture))))).ToArray();
            if(points.Length<2)continue;
            var gate=node.Name.ToString().Contains("MainStreet",StringComparison.Ordinal)?"main-service-gate":"connective-service-gate";
            registry.Graph.AddRoad(new("route/"+node.Name,"",points,1.04,"snow_trampled",SettlementTravelMode.Foot,false,gate));
        }
        registry.Graph.GateIsOpen=key=>key switch{"main-service-gate"=>_mainStreetServiceGateCollision?.Disabled==true,
            "connective-service-gate"=>_connectiveShedBypassGateCollision?.Disabled==true,_=>false};
        registry.Graph.Rebuild(registry.Streets);
    }
    private void ImportAddressConstraints(SettlementRegistry registry)
    {
        var river=FindDescendants<Node3D>(this).FirstOrDefault(n=>n.Name=="VillageForestRiver");
        if(river is not null)
            foreach(var mesh in FindDescendants<MeshInstance3D>(river).Where(n=>n.Name.ToString().StartsWith("RiverIce_",StringComparison.Ordinal)))
                registry.AddConstraint(new("water/"+mesh.Name,"water",AddressFootprint(mesh),SettlementTravelMode.All));
        var ravineWest=new List<SettlementPoint>();var ravineEast=new List<SettlementPoint>();
        for(var z=-92f;z<=104f;z+=4f)
        {
            var centre=AgentBAct1HeightField.RavineCentre(z);
            ravineWest.Add(new(centre-AgentBAct1HeightField.RavineHalfWidth,0,z));
            ravineEast.Add(new(centre+AgentBAct1HeightField.RavineHalfWidth,0,z));
        }
        ravineEast.Reverse();
        registry.AddConstraint(new("water/ravine","water",[..ravineWest,..ravineEast],SettlementTravelMode.All));
        var min=AgentBAct1ExteriorLayer.ForestRingInnerMin;var max=AgentBAct1ExteriorLayer.ForestRingInnerMax;
        registry.AddConstraint(new("forest-ring","forest-edge",[new(min.X,0,min.Y),new(max.X,0,min.Y),new(max.X,0,max.Y),new(min.X,0,max.Y)],SettlementTravelMode.Car|SettlementTravelMode.Motorcycle));
    }
    public bool CanVehicleTraverse(Vector3 from,Vector3 to,SettlementTravelMode mode,out string reason)
    {
        if(AddressRegistry is null){reason="Дорога ещё загружается.";return false;}
        return AddressRegistry.Graph.CanTraverse(AddressPoint(from),AddressPoint(to),mode,out reason);
    }
    public void RefreshAddressSigns(){foreach(var sign in _addressSigns)if(GodotObject.IsInstanceValid(sign))sign.RefreshLabels();}
    internal void CommitAddressAccess(string accessId,Vector3[]? path,string failure,SettlementPoint? graphAnchor=null)
    {
        var registry=AddressRegistry!;
        if(path is null)
        {
            _addressVerifiedPaths.Remove(accessId);
            _addressAccessCommitFrames.Remove(accessId);
            _addressAccessAttachFrames.Remove(accessId);
            var removed=registry.Graph.RemoveVerifiedFootAccess(accessId);
            registry.SetAccessState(accessId,"blocked: "+failure);
            if(removed)AttachVerifiedAddressAccessPaths();
            return;
        }
        var access=registry.AccessPoints[accessId];
        var graphPath=path.Select(AddressPoint).ToArray();
        // Retain the exact double X/Z projection for graph connectivity, but keep
        // the supporting height verified by physics rather than the road's interpolated Y.
        if(graphAnchor is { } anchor)graphPath[0]=new(anchor.X,graphPath[0].Y,anchor.Z);
        if(path.Zip(path.Skip(1),(a,b)=>new Vector2(a.X-b.X,a.Z-b.Z).Length()).Any(length=>length>.00001f))
            registry.Graph.SetVerifiedFootAccess(accessId,graphPath);
        _addressVerifiedPaths[accessId]=path;
        _addressAccessCommitFrames[accessId]=(long)Engine.GetPhysicsFrames();
        _addressAccessAttachFrames.Remove(accessId);
        _addressGraphNeedsAttachment=true;
        // The physical verifier retains the actual supporting surface height,
        // including porch treads; do not put the registered entrance below it.
        registry.UpdateAccess(access with{Position=AddressPoint(path[^1]),State="pending-graph-attachment"});
    }
    internal void CompleteAddressAccessAudit()
    {
        if(_addressGraphNeedsAttachment)AttachVerifiedAddressAccessPaths();
        SetMeta("addressAccessAuditCompleted",true);
        SetMeta("addressAccessVerifiedCount",AddressRegistry!.AccessPoints.Values.Count(a=>a.State=="verified"));
    }
    // Attaching a verified subset does not declare the ordinary audit complete.
    internal void AttachVerifiedAddressAccessPaths()
    {
        var registry=AddressRegistry!;
        registry.Graph.Rebuild(registry.Streets);
        var start=registry.Graph.NodeAt(registry.Graph.Roads["authored/main-axis"].Points[0]);
        var connected=start is null?new HashSet<string>():registry.Graph.ReachableNodes(start,SettlementTravelMode.Foot);
        foreach(var (accessId,path) in _addressVerifiedPaths)
        {
            var nodeId=registry.Graph.NodeAt(AddressPoint(path[^1]));
            var access=registry.AccessPoints[accessId];
            var verified=nodeId is not null&&connected.Contains(nodeId);
            registry.UpdateAccess(access with{GraphNodeId=nodeId??"",State=verified?"verified":"graph-attachment-failed"});
            if(verified&&access.State=="pending-graph-attachment")
                _addressAccessAttachFrames[accessId]=(long)Engine.GetPhysicsFrames();
            else if(!verified)_addressAccessAttachFrames.Remove(accessId);
        }
        _addressGraphNeedsAttachment=false;
        AddressGraphAttachmentFrame=Engine.GetPhysicsFrames();
        SetMeta("addressGraphAttachmentFrame",(long)AddressGraphAttachmentFrame);
        SetMeta("addressGraphAttachmentCount",++_addressGraphAttachmentCount);
    }
    internal static Vector3 AddressGround(Vector3 p)=>new(p.X,AgentBAct1HeightField.CollisionGround(p.X,p.Z)+.035f,p.Z);
    internal static SettlementPoint AddressPoint(Vector3 p)=>new(p.X,p.Y,p.Z);
    internal static Vector3 AddressVector(SettlementPoint p)=>new((float)p.X,(float)p.Y,(float)p.Z);
    private static bool TryAddressDoor(Node3D building,string? preferred,out Vector3 center,out Vector3 outward)
    {
        center=default;outward=default;
        var meshes=FindDescendants<MeshInstance3D>(building).Where(m=>m.Mesh is not null).ToArray();
        var door=preferred is not null?meshes.FirstOrDefault(m=>m.Name==preferred):null;
        door??=meshes.Where(m=>m.IsVisibleInTree() && m.Name.ToString().Contains("Door",StringComparison.Ordinal)
            && !new[]{"Frame","Trim","Lintel","Handle","Step","Canopy","Awning","Hinge","Jamb","Rail","Recess"}.Any(s=>m.Name.ToString().Contains(s,StringComparison.Ordinal)))
            .OrderBy(m=>m.Name.ToString().Contains("StreetDoorClosed",StringComparison.Ordinal)?0:m.Name.ToString().Contains("SeniEntry_Door0_Leaf",StringComparison.Ordinal)?1:2)
            .ThenBy(m=>m.Name.ToString(),StringComparer.Ordinal).FirstOrDefault();
        door??=meshes.FirstOrDefault(m=>m.Name=="CoreFrontOpening");
        if(door?.Mesh is null)return false;
        var bounds=door.Mesh.GetAabb();center=door.GlobalTransform*(bounds.Position+bounds.Size*.5f);
        var axis=bounds.Size.X<bounds.Size.Z?Vector3.Right:Vector3.Back;
        outward=door.GlobalBasis*axis;outward.Y=0;outward=outward.Normalized();
        var fromHouse=center-building.GlobalPosition;fromHouse.Y=0;
        // The seni door faces the street even though its centre is behind the
        // dwelling origin. A centre-to-centre heuristic would point into the wall.
        var authoredLeaf=door.Name.ToString().Contains("SeniEntry_Door0_Leaf",StringComparison.Ordinal)
            || door.Name.ToString().Contains("StreetDoorClosed",StringComparison.Ordinal);
        if(!authoredLeaf && outward.Dot(fromHouse)<0)outward=-outward;
        if(outward.LengthSquared()<.1f)outward=Vector3.Back;
        return true;
    }
    public bool TryGetAddressSourceEntrance(string sourceName,out Vector3 center,out Vector3 outward)
    {
        var building=FindDescendants<Node3D>(this).FirstOrDefault(n=>n.Name==sourceName && n.IsVisibleInTree());
        center=default;outward=default;
        return building is not null && TryAddressDoor(building,null,out center,out outward);
    }
    private static IReadOnlyList<SettlementPoint> AddressFootprint(Node3D root)
    {
        var corners=new List<Vector3>();
        foreach(var mesh in new[]{root}.Concat(FindDescendants<Node3D>(root)).OfType<MeshInstance3D>().Where(m=>m.Mesh is not null && m.IsVisibleInTree()))
        {
            var b=mesh.Mesh!.GetAabb();
            for(var x=0;x<2;x++)for(var y=0;y<2;y++)for(var z=0;z<2;z++)
                corners.Add(mesh.GlobalTransform*(b.Position+new Vector3(b.Size.X*x,b.Size.Y*y,b.Size.Z*z)));
        }
        if(corners.Count==0)return [AddressPoint(root.GlobalPosition)];
        var minX=corners.Min(p=>p.X);var maxX=corners.Max(p=>p.X);var minZ=corners.Min(p=>p.Z);var maxZ=corners.Max(p=>p.Z);
        return [new(minX,0,minZ),new(maxX,0,minZ),new(maxX,0,maxZ),new(minX,0,maxZ)];
    }
    private static IReadOnlyList<SettlementPoint> AddressParcelPolygon(IReadOnlyList<SettlementPoint> footprint,SettlementPoint access)
    {
        var points=footprint.Append(access).ToArray();
        return [new(points.Min(p=>p.X)-.5,0,points.Min(p=>p.Z)-.5),new(points.Max(p=>p.X)+.5,0,points.Min(p=>p.Z)-.5),
            new(points.Max(p=>p.X)+.5,0,points.Max(p=>p.Z)+.5),new(points.Min(p=>p.X)-.5,0,points.Max(p=>p.Z)+.5)];
    }
}
