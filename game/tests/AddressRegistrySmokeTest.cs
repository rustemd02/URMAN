using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class AddressRegistrySmokeTest : Node
{
    public override void _Ready()
    {
        try{var checks=RunPureChecks();foreach(var check in checks)GD.Print("ADDRESS PASS "+check);GetTree().Quit(0);}
        catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}
    }
    public static IReadOnlyList<string> RunPureChecks()
    {
        var done=new List<string>();
        void Check(bool okay,string name){if(!okay)throw new InvalidOperationException("Address invariant failed: "+name);done.Add(name);}
        SettlementRegistry Registry(bool reverse=false,bool shift=false)
        {
            var registry=new SettlementRegistry();
            registry.AddStreet(new("tukay","Тукай ур.","ул. Тукая",new(0,0,0),"left",true));
            SettlementPoint P(double x,double z)=>new(x+(shift ? .02 : 0),0,z+(shift ? .02 : 0));
            var roads=new[]{new SettlementRoad("main","tukay",[P(0,0),P(0,20)],5.6,"snow_trampled",SettlementTravelMode.All),
                new SettlementRoad("gate-walk","tukay",[P(0,10),P(6,10)],1.2,"snow_trampled",SettlementTravelMode.Foot)};
            foreach(var road in reverse?roads.Reverse():roads)
                registry.Graph.AddRoad(reverse?road with{Points=road.Points.Reverse().ToArray()}:road);
            registry.Graph.Rebuild(registry.Streets);return registry;
        }
        var a=Registry();var reversed=Registry(true);var shifted=Registry(shift:true);
        Check(a.Graph.Nodes.Keys.Order().SequenceEqual(reversed.Graph.Nodes.Keys.Order()),"road reorder/reversal keeps graph nodes");
        Check(a.Graph.Edges.Keys.Order().SequenceEqual(reversed.Graph.Edges.Keys.Order()),"road reorder/reversal keeps graph edges");
        Check(a.Graph.Nodes.Keys.Order().SequenceEqual(shifted.Graph.Nodes.Keys.Order()),"small geometry movement keeps node references");
        var gate=new SettlementPoint(6,0,10);var gateNode=a.Graph.NodeAt(gate)!;
        var building=new SettlementBuilding("BLD-TEST","authored/test","PAR-TEST","ADR-TEST","residential",new(8,0,10),[]);
        var parcel=new SettlementParcel("PAR-TEST","Q01","URM-Q01-P0017","BLD-TEST","ACC-TEST",[]);
        var address=new AddressRecord("ADR-TEST","BLD-TEST","PAR-TEST","tukay","17","ACC-TEST",[]);
        var access=new SettlementAccess("ACC-TEST","BLD-TEST","gate",gate,gateNode,"verified");
        a.Register(building,parcel,address,access);a.Register(building,parcel,address,access);
        Check(a.Addresses.Count==1,"repeat import retains exactly one address");
        bool Reject(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
        bool MatchesExhaustive(SettlementRoadGraph graph,IReadOnlyDictionary<string,SettlementStreet>? streets=null)
        {
            var exhaustive=new SettlementRoadGraph();
            foreach(var road in graph.Roads.Values)exhaustive.AddRoad(road);
            exhaustive.RebuildExhaustiveForDiagnostics(streets??a.Streets);
            return graph.Nodes.Values.OrderBy(n=>n.Id,StringComparer.Ordinal).SequenceEqual(exhaustive.Nodes.Values.OrderBy(n=>n.Id,StringComparer.Ordinal))
                &&graph.Edges.Values.OrderBy(e=>e.Id,StringComparer.Ordinal).SequenceEqual(exhaustive.Edges.Values.OrderBy(e=>e.Id,StringComparer.Ordinal));
        }
        Check(Reject(()=>a.Register(building,parcel with{GameCadastralId="URM-Q01-P9000"},address,access)),"reimport cannot change committed cadastre");
        Check(Reject(()=>a.Register(building,parcel with{AccessId="ACC-CHANGED"},address with{AccessId="ACC-CHANGED"},access with{AccessId="ACC-CHANGED"})),"reimport cannot detach committed access ID");
        Check(a.Parcels["PAR-TEST"].GameCadastralId=="URM-Q01-P0017"&&a.AccessPoints.Count==1,"rejected import is atomic");
        a.AddAddressAlias("ADR-SHOP","ADR-TEST");
        Check(a.CanonicalAddressId("ADR-SHOP")=="ADR-TEST"&&a.MapForKnownAddresses(["ADR-SHOP"]).Buildings.Count==1,"semantic public alias keeps original address identity");
        var reference=address.BuildingId;
        a.RenameStreet("tukay","Тукай ур.","ул. Габдуллы Тукая");
        Check(a.TryResolve(reference,out var renamed)&&renamed.AddressId=="ADR-TEST"&&a.FormatAddress(reference).Contains("Габдуллы"),"saved building reference survives street rename");
        Check(a.InfillNumber("tukay","17")=="17А"&&a.Addresses["ADR-TEST"].HouseNumber=="17","infill keeps existing numbering");
        a.ChangeAddress("ADR-TEST","tukay","19","test-rename");
        Check(a.TryFind("tukay","17",out var legacy)&&legacy.AddressId=="ADR-TEST","old numbered alias resolves stable address");
        Check(a.Addresses["ADR-TEST"].BuildingId==reference,"renumbering preserves building identity");
        Check(Reject(()=>a.Register(building,parcel,address,access)),"stale reimport cannot erase migrated history");
        a.Register(building with{Position=new(8.02,0,10.02)},parcel,a.Addresses["ADR-TEST"],access);
        Check(a.Addresses["ADR-TEST"].History.Count==1&&a.TryFind("tukay","17",out _),"migrated reimport preserves historical alias");
        Check(SettlementRegistry.NormalizeNumber(" 017 A ")=="17А","address normalization merges keyboard aliases");
        var route=a.DiagnosticRoute(a.Graph.NodeAt(new(0,0,0))!,"ADR-TEST",SettlementTravelMode.Foot);
        Check(route.Count>0&&route[^1]==gate,"route ends at real gate instead of building centroid");
        Check(a.DiagnosticRoute(a.Graph.NodeAt(new(0,0,0))!,"ADR-TEST",SettlementTravelMode.Car).Count==0,"vehicle cannot use narrow foot access");
        Check(a.MapForKnownAddresses([]).Buildings.Count==0&&a.MapForKnownAddresses(["ADR-TEST"]).Buildings.Count==1,"notebook only labels learned addresses");
        Check(ReferenceEquals(a.MapForKnownAddresses(["ADR-TEST"]).Edges[0],a.Graph.Edges[a.MapForKnownAddresses(["ADR-TEST"]).Edges[0].Id]),"notebook and routing share graph edges");
        a.Graph.SetRoadBlocked("gate-walk",true);
        Check(a.DiagnosticRoute(a.Graph.NodeAt(new(0,0,0))!,"ADR-TEST",SettlementTravelMode.Foot).Count==0,"closed road removes route");
        a.Graph.SetRoadBlocked("gate-walk",false);
        var duplicate=false;
        try{a.Register(building with{BuildingId="BLD-OTHER",SourceKey="authored/other",ParcelId="PAR-OTHER",AddressId="ADR-OTHER"},
            parcel with{ParcelId="PAR-OTHER",PrimaryBuildingId="BLD-OTHER",AccessId="ACC-OTHER"},
            address with{AddressId="ADR-OTHER",BuildingId="BLD-OTHER",ParcelId="PAR-OTHER",AccessId="ACC-OTHER",HouseNumber="20"},
            access with{AccessId="ACC-OTHER",BuildingId="BLD-OTHER"});}
        catch(InvalidOperationException){duplicate=true;}
        Check(duplicate,"duplicate fictional cadastral identity rejected");
        a.SetAccessState("ACC-TEST","pending-physics");
        Check(a.DiagnosticRoute(a.Graph.NodeAt(new(0,0,0))!,"ADR-TEST",SettlementTravelMode.Foot).Count==0,"unverified access is never declared reachable");
        var collinear=new SettlementRoadGraph();
        collinear.AddRoad(new("long","tukay",[new(0,0,0),new(10,0,0)],4,"earth",SettlementTravelMode.Foot));
        collinear.AddRoad(new("overlap","tukay",[new(3,0,0),new(7,0,0)],4,"earth",SettlementTravelMode.Foot));
        collinear.Rebuild(a.Streets);
        Check(collinear.Nodes.Count==4&&collinear.Route(collinear.NodeAt(new(0,0,0))!,collinear.NodeAt(new(7,0,0))!,SettlementTravelMode.Foot).Count>0,"collinear overlap is split and connected");
        var collinearReversed=new SettlementRoadGraph();
        foreach(var road in collinear.Roads.Values.Reverse())
            collinearReversed.AddRoad(road with{Points=road.Points.Reverse().ToArray()});
        collinearReversed.Rebuild(a.Streets);
        Check(collinear.Nodes.Values.OrderBy(n=>n.Id,StringComparer.Ordinal).SequenceEqual(collinearReversed.Nodes.Values.OrderBy(n=>n.Id,StringComparer.Ordinal))
            &&collinear.Edges.Values.OrderBy(e=>e.Id,StringComparer.Ordinal).SequenceEqual(collinearReversed.Edges.Values.OrderBy(e=>e.Id,StringComparer.Ordinal)),
            "collinear reorder and reversal preserve complete node and edge records");
        foreach(var height in new[]{.5,.5001})
        {
            var crossing=new SettlementRoadGraph();
            crossing.AddRoad(new("ground","tukay",[new(0,0,0),new(10,0,0)],4,"earth",SettlementTravelMode.Foot));
            crossing.AddRoad(new("upper","tukay",[new(5,height,-5),new(5,height,5)],4,"earth",SettlementTravelMode.Foot));
            crossing.Rebuild(a.Streets);
            var connected=crossing.Route(crossing.NodeAt(new(0,0,0))!,crossing.NodeAt(new(5,height,5))!,SettlementTravelMode.Foot).Count>0;
            Check(connected==(height==.5),height==.5
                ? "crossing at the existing half-metre height boundary remains connected"
                : "crossing above the existing height boundary stays disconnected");
            Check(MatchesExhaustive(crossing),$"broadphase preserves all crossing records at height {height}");
        }
        var gap=new SettlementRoadGraph();
        gap.AddRoad(new("left","tukay",[new(0,0,0),new(1,0,0)],4,"earth",SettlementTravelMode.Foot));
        gap.AddRoad(new("right","tukay",[new(1.01,0,0),new(2,0,0)],4,"earth",SettlementTravelMode.Foot));
        gap.Rebuild(a.Streets);
        Check(gap.Route(gap.NodeAt(new(0,0,0))!,gap.NodeAt(new(2,0,0))!,SettlementTravelMode.Foot).Count==0,"small geometric gap is not rounded shut");
        foreach(var (name,roads) in new (string,SettlementRoad[])[]
        {
            ("t just beyond the endpoint",[
                new("a-long","tukay",[new(0,0,0),new(4000,0,0)],4,"earth",SettlementTravelMode.Foot),
                new("b-cross","tukay",[new(4000.00002,0,-1),new(4000.00002,0,1)],4,"earth",SettlementTravelMode.Foot)]),
            ("u just beyond the endpoint",[
                new("a-cross","tukay",[new(-1,0,-.00002),new(1,0,-.00002)],4,"earth",SettlementTravelMode.Foot),
                new("b-long","tukay",[new(0,0,0),new(0,0,4000)],4,"earth",SettlementTravelMode.Foot)]),
            ("outside the parameter allowance",[
                new("a-long","tukay",[new(0,0,0),new(4000,0,0)],4,"earth",SettlementTravelMode.Foot),
                new("b-cross","tukay",[new(4000.00008,0,-1),new(4000.00008,0,1)],4,"earth",SettlementTravelMode.Foot)]),
            ("collinear endpoint inside distance allowance",[
                new("a-left","tukay",[new(0,0,0),new(1,0,0)],4,"earth",SettlementTravelMode.Foot),
                new("b-right","tukay",[new(1.000009,0,0),new(2,0,0)],4,"earth",SettlementTravelMode.Foot)]),
            ("collinear endpoint outside distance allowance",[
                new("a-left","tukay",[new(0,0,0),new(1,0,0)],4,"earth",SettlementTravelMode.Foot),
                new("b-right","tukay",[new(1.000011,0,0),new(2,0,0)],4,"earth",SettlementTravelMode.Foot)]),
            ("ill-conditioned disjoint near-parallel pair",[
                new("a-skew","tukay",[new(0,0,0),new(1000,0,1000)],4,"earth",SettlementTravelMode.Foot),
                new("b-skew","tukay",[new(1002,0,1002),new(2002,0,2002.0000001)],4,"earth",SettlementTravelMode.Foot)]),
            ("large-coordinate exhaustive fallback",[
                new("a-large","tukay",[new(1e12,0,1e12),new(1e12+4,0,1e12+4)],4,"earth",SettlementTravelMode.Foot),
                new("b-large","tukay",[new(1e12+8,0,1e12+8),new(1e12+12,0,1e12+12.0001)],4,"earth",SettlementTravelMode.Foot)])
        })
        {
            var graph=new SettlementRoadGraph();
            foreach(var road in roads)graph.AddRoad(road);
            graph.Rebuild(a.Streets);
            Check(MatchesExhaustive(graph),"broadphase matches exhaustive records: "+name);
            var reverseGraph=new SettlementRoadGraph();
            foreach(var road in roads.Reverse())reverseGraph.AddRoad(road with{Points=road.Points.Reverse().ToArray()});
            reverseGraph.Rebuild(a.Streets);
            Check(MatchesExhaustive(reverseGraph)
                &&graph.Nodes.Values.OrderBy(n=>n.Id,StringComparer.Ordinal).SequenceEqual(reverseGraph.Nodes.Values.OrderBy(n=>n.Id,StringComparer.Ordinal))
                &&graph.Edges.Values.OrderBy(e=>e.Id,StringComparer.Ordinal).SequenceEqual(reverseGraph.Edges.Values.OrderBy(e=>e.Id,StringComparer.Ordinal)),
                "broadphase keeps reversal and import order: "+name);
            if(name=="t just beyond the endpoint")
                Check(graph.Nodes["ND-29FAD5A3B6A5DD28"].Position==new SettlementPoint(4000.00002,0,-1)
                    &&graph.Nodes["ND-A5BA17240D8F9C8E"].Position==new SettlementPoint(4000.00002,0,1)
                    &&reverseGraph.Nodes["ND-29FAD5A3B6A5DD28"].Position==new SettlementPoint(4000.00002,0,-1)
                    &&reverseGraph.Nodes["ND-A5BA17240D8F9C8E"].Position==new SettlementPoint(4000.00002,0,1),
                    "exact-distance tie retains the original committed endpoint IDs and positions after reversal");
        }
        var pinnedRoad=new SettlementRoad("pinned-symmetric","tukay",[new(5,0,-2),new(5,0,2)],4,"earth",SettlementTravelMode.Foot,
            PointKeys:["pin/south","pin/north"]);
        var pinned=new SettlementRoadGraph();pinned.AddRoad(pinnedRoad);pinned.Rebuild(a.Streets);
        var pinnedReverse=new SettlementRoadGraph();
        pinnedReverse.AddRoad(pinnedRoad with{Points=pinnedRoad.Points.Reverse().ToArray(),PointKeys=pinnedRoad.PointKeys!.Reverse().ToArray()});
        pinnedReverse.Rebuild(a.Streets);
        Check(MatchesExhaustive(pinned)&&MatchesExhaustive(pinnedReverse)
            &&pinned.Nodes.Values.OrderBy(n=>n.Id,StringComparer.Ordinal).SequenceEqual(pinnedReverse.Nodes.Values.OrderBy(n=>n.Id,StringComparer.Ordinal))
            &&pinned.Edges.Values.OrderBy(e=>e.Id,StringComparer.Ordinal).SequenceEqual(pinnedReverse.Edges.Values.OrderBy(e=>e.Id,StringComparer.Ordinal)),
            "equal-distance road reversal preserves complete graph records with pinned point keys");
        Check(pinned.Nodes[SettlementRegistry.StableId("ND","pin/south")].Position==new SettlementPoint(5,0,-2)
            &&pinned.Nodes[SettlementRegistry.StableId("ND","pin/north")].Position==new SettlementPoint(5,0,2),
            "canonical direction cannot transfer a pinned vertex identity to the other endpoint");
        var originPriority=new SettlementRoadGraph();
        originPriority.AddRoad(new("origin-priority","tukay",[new(0,0,0),new(0,0,5)],4,"earth",SettlementTravelMode.Foot,
            PointKeys:["z-near-origin","a-far-origin"]));
        originPriority.Rebuild(a.Streets);
        Check(originPriority.Edges.Values.Single().A==SettlementRegistry.StableId("ND","z-near-origin")&&MatchesExhaustive(originPriority),
            "unequal street-origin distance retains its previous priority over lexical point keys");
        var sparse=new SettlementRoadGraph();
        for(var i=0;i<128;i++)sparse.AddRoad(new("sparse/"+i,"tukay",[new(i*10,0,0),new(i*10+2,0,1)],4,"earth",SettlementTravelMode.Foot));
        sparse.Rebuild(a.Streets);
        Check(MatchesExhaustive(sparse),"sparse 128-segment graph preserves every node and edge while opt-in records expose allocation costs");
        foreach(var graph in new[]{a.Graph,reversed.Graph,shifted.Graph,collinear,collinearReversed,gap})
            Check(MatchesExhaustive(graph),"existing graph fixture matches exhaustive builder with "+graph.Roads.Count+" roads and "+graph.Nodes.Count+" nodes");
        List<AddressFacadeMount.Triangle> Rect(float x0,float y0,float x1,float y1)=>
            [new(new(x0,y0),new(x1,y0),new(x1,y1)),new(new(x0,y0),new(x1,y1),new(x0,y1))];
        var wall=Rect(-.8f,-.4f,.8f,.4f);
        Check(AddressFacadeMount.Cover(wall,Vector2.Zero).Supported,"complete real plate rectangle is supported");
        var hole=Rect(-.8f,-.4f,.11f,.4f).Concat(Rect(.15f,-.4f,.8f,.4f))
            .Concat(Rect(.11f,-.4f,.15f,.05f)).Concat(Rect(.11f,.09f,.15f,.4f)).ToArray();
        Check(!AddressFacadeMount.Cover(hole,Vector2.Zero).Supported,"exact footprint rejects a hole between probe samples");
        Check(!AddressFacadeMount.Cover(Rect(-.55f,-.4f,.8f,.4f),Vector2.Zero).Supported,"full outer rim rejects an eleven-centimetre unchecked overhang");
        var boards=Rect(-.8f,-.4f,.12f,.4f).Concat(Rect(.125f,-.4f,.8f,.4f)).ToArray();
        Check(!AddressFacadeMount.Cover(boards,Vector2.Zero).Supported&&AddressFacadeMount.Cover(boards,Vector2.Zero,true).Supported,"only explicitly boarded facade accepts a five-millimetre vertical joint");
        var centreJoint=Rect(-.8f,-.4f,-.004f,.4f).Concat(Rect(.004f,-.4f,.8f,.4f)).ToArray();
        Check(AddressFacadeMount.Cover(centreJoint,Vector2.Zero,true).Supported,"eight-millimetre board joint may cross the metal rim between supported fasteners");
        Check(!AddressFacadeMount.Cover(Rect(-.585f,-.4f,.8f,.4f),Vector2.Zero,true).Supported,"a five-millimetre exterior overhang cannot masquerade as a board joint");
        var fastenerJoint=Rect(-.8f,-.4f,.532f,.4f).Concat(Rect(.539f,-.4f,.8f,.4f)).ToArray();
        Check(!AddressFacadeMount.Cover(fastenerJoint,Vector2.Zero,true).Supported,"a fastener cannot be fixed into a narrow construction joint");
        Check(!AddressFacadeMount.Eligible("VariantA_Dwelling_SeniRear_Wall_LOD0")&&!AddressFacadeMount.Eligible("Window_Glass")
            &&AddressFacadeMount.Eligible("VariantA_Dwelling_Front_Wall_LOD0"),"compound rear wall and glass cannot own an exterior plate");
        Check(AddressFacadeMount.TriangleObstructs(new(.45f,-1,2),new(.58f,-1,2),new(.58f,1,2),Vector3.Zero,Vector3.Back),"foreground post two metres away blocks the outer rim");
        Check(!AddressFacadeMount.TriangleObstructs(new(.7f,-1,2),new(.9f,-1,2),new(.9f,1,2),Vector3.Zero,Vector3.Back),"post outside the full plate footprint does not block it");
        var nativeProjection=new SettlementRoadGraph();
        nativeProjection.AddRoad(new("native-spur","tukay",[new(-21.868152618408203,0,-63.62556838989258),new(-23.367734909057617,0,-64.13541412353516)],1.1,"snow_trampled",SettlementTravelMode.Foot));
        nativeProjection.Rebuild(a.Streets);
        var nativeTarget=new SettlementPoint(-31.254236221313477,0,-39.69572067260742);
        var exactProjection=nativeProjection.Nearest(nativeTarget)!.Value.Point;
        nativeProjection.AddRoad(new("native-access","",[exactProjection,nativeTarget],.75,"snow_trampled",SettlementTravelMode.Foot));
        nativeProjection.Rebuild(a.Streets);
        Check(nativeProjection.Route(nativeProjection.NodeAt(nativeProjection.Roads["native-spur"].Points[0])!,nativeProjection.NodeAt(nativeTarget)!,SettlementTravelMode.Foot).Count>0,"native H020 access retains its exact road projection after physics probing");
        var shed=new SettlementBuilding("BLD-SHED","authored/test/shed","PAR-TEST","ADR-TEST","shed",new(9,0,9),[]);
        a.RegisterAuxiliary(shed);a.RegisterAuxiliary(shed with{Position=new(9.02,0,9.02)});
        Check(a.Buildings["BLD-SHED"].ParcelId=="PAR-TEST"&&a.TryResolve("BLD-SHED",out var inherited)&&inherited.AddressId=="ADR-TEST","shed retains authored parcel and address through repeated geometry import");
        Check(Reject(()=>a.RegisterAuxiliary(building with{Role="shed"}))&&a.Buildings["BLD-TEST"].Role=="residential","auxiliary registration cannot overwrite the primary house");
        var utility=new SettlementBuilding("BLD-UTILITY","authored/independent-shed","PAR-UTILITY",null,"shed",new(5,0,10),[]);
        var utilityParcel=new SettlementParcel("PAR-UTILITY","Q01","URM-Q01-P0201","BLD-UTILITY","ACC-UTILITY",[]);
        var utilityAccess=new SettlementAccess("ACC-UTILITY","BLD-UTILITY","entrance",gate,gateNode,"pending-physics");
        var addressCount=a.Addresses.Count;
        a.Register(utility,utilityParcel,null,utilityAccess);
        a.Register(utility with{Position=new(5.02,0,10.02)},utilityParcel,null,utilityAccess);
        Check(a.Addresses.Count==addressCount&&!a.TryResolve("BLD-UTILITY",out _)
            &&a.Buildings["BLD-UTILITY"].ParcelId=="PAR-UTILITY","standalone shed repeat import preserves its own parcel without inventing a residential address");
        Check(a.Validate().Any(i=>i.EntityId=="PAR-UTILITY"&&i.Code=="ACCESS_NOT_VERIFIED"),"non-addressable parcel access is included in validation");
        a.UpdateAccess(utilityAccess with{State="verified"});
        Check(!a.Validate().Any(i=>i.EntityId=="PAR-UTILITY"),"verified utility access uses the same graph and parcel validation");
        Check(!a.MapForKnownAddresses(["ADR-TEST"]).Buildings.Any(b=>b.BuildingId=="BLD-UTILITY"),"independent technical parcel does not leak into notebook knowledge");
        Check(Reject(()=>a.Register(utility with{AddressId="ADR-INVENTED"},utilityParcel,null,utilityAccess)),"non-addressable import cannot invent an address reference without its record");
        var reservedRegistry=Registry();
        reservedRegistry.ReserveAuthoredAddress("ADR-TEST","tukay","25");
        reservedRegistry.ReserveAuthoredAddress("ADR-RETIRED","tukay","025 A");
        reservedRegistry.ReserveAuthoredAddress("ADR-RETIRED","tukay","25А");
        reservedRegistry.Register(building,parcel,address with{HouseNumber="25"},access);
        Check(reservedRegistry.InfillNumber("tukay","25")=="25Б"&&!reservedRegistry.TryFind("tukay","25А",out _),
            "retired authored 25А reserves its normalized number without a visible address record");
        var inserted=building with{BuildingId="BLD-INSERTED",SourceKey="authored/inserted",ParcelId="PAR-INSERTED",AddressId="ADR-INSERTED"};
        var insertedParcel=parcel with{ParcelId="PAR-INSERTED",GameCadastralId="URM-Q01-P0204",PrimaryBuildingId="BLD-INSERTED",AccessId="ACC-INSERTED"};
        var insertedAddress=new AddressRecord("ADR-INSERTED","BLD-INSERTED","PAR-INSERTED","tukay",reservedRegistry.InfillNumber("tukay","25"),"ACC-INSERTED",[]);
        var insertedAccess=access with{AccessId="ACC-INSERTED",BuildingId="BLD-INSERTED"};
        reservedRegistry.Register(inserted,insertedParcel,insertedAddress,insertedAccess);
        reservedRegistry.ReserveAuthoredAddress("ADR-INSERTED","tukay","25Б");
        reservedRegistry.Register(inserted,insertedParcel,insertedAddress,insertedAccess);
        Check(reservedRegistry.Addresses.Count==2&&reservedRegistry.Addresses["ADR-TEST"].HouseNumber=="25"
            &&reservedRegistry.Addresses["ADR-INSERTED"].HouseNumber=="25Б"&&reservedRegistry.InfillNumber("tukay","25")=="25В",
            "repeat enriched import retains infill and leaves the existing house and retired reservation unchanged");
        Check(Reject(()=>reservedRegistry.ReserveAuthoredAddress("ADR-WRONG","tukay","25А")),"another source cannot claim a retired authored number");
        var accessGraph=Registry();
        accessGraph.Graph.SetVerifiedFootAccess("ACC-REPEAT",[new(0,0,12),new(4,0,12)]);
        accessGraph.Graph.Rebuild(accessGraph.Streets);
        var accessNodes=accessGraph.Graph.Nodes.Keys.Order().ToArray();
        accessGraph.Graph.SetVerifiedFootAccess("ACC-REPEAT",[new(0,0,12),new(4,0,12)]);
        accessGraph.Graph.Rebuild(accessGraph.Streets);
        Check(accessNodes.SequenceEqual(accessGraph.Graph.Nodes.Keys.Order())&&accessGraph.Graph.Roads.Count==3,
            "repeat physical access attachment is idempotent and preserves the authored roads");
        var mapRegistry=Registry();
        mapRegistry.AddStreet(new("urman","Урман ур.","ул. Лесная",new(0,0,20)));
        mapRegistry.Graph.AddRoad(new("authored/east-street","urman",[new(0,0,20),new(30,0,20)],4,"snow_trampled",SettlementTravelMode.All));
        mapRegistry.Graph.AddRoad(new("route/undiscovered-gate","",[new(0,0,15),new(-30,0,15)],1,"earth",SettlementTravelMode.Foot));
        mapRegistry.Register(building,parcel,address,access with{State="pending-physics",GraphNodeId=""});
        mapRegistry.Register(inserted,insertedParcel,insertedAddress with{StreetId="urman"},insertedAccess);
        mapRegistry.Graph.SetVerifiedFootAccess(insertedAccess.AccessId,[new(30,0,20),new(35,0,20)]);
        mapRegistry.Graph.Rebuild(mapRegistry.Streets);
        var beforeAudit=mapRegistry.MapForKnownAddresses(["ADR-TEST"]);
        Check(beforeAudit.Edges.Any(e=>e.RoadId=="authored/east-street")&&beforeAudit.KnownStreets.Select(s=>s.Id).SequenceEqual(new[]{"tukay"}),
            "located house receives real road context without learning other street names");
        Check(beforeAudit.Buildings.Count==1&&beforeAudit.Parcels.Count==1&&beforeAudit.AccessPoints.Single().AccessId=="ACC-TEST"
            &&beforeAudit.AccessPoints[0].State=="pending-physics",
            "located house and real entrance remain on the map while access physics is pending");
        Check(beforeAudit.Edges.All(e=>!e.RoadId.StartsWith("route/",StringComparison.Ordinal)&&!e.RoadId.StartsWith("access/",StringComparison.Ordinal))
            &&beforeAudit.Nodes.All(n=>n.Position.X>=0&&n.Position.X<=30),
            "map cannot reveal a gated shortcut or another house's computed access path");
        mapRegistry.Graph.SetVerifiedFootAccess(access.AccessId,[new(0,0,12),new(6,0,12),gate]);
        mapRegistry.Graph.Rebuild(mapRegistry.Streets);
        mapRegistry.UpdateAccess(access with{GraphNodeId=mapRegistry.Graph.NodeAt(gate)!});
        var afterAudit=mapRegistry.MapForKnownAddresses(["ADR-TEST"]);
        var roadLengths=beforeAudit.Edges.GroupBy(e=>e.RoadId).ToDictionary(g=>g.Key,g=>g.Sum(e=>e.Length));
        Check(afterAudit.Edges.All(e=>roadLengths.ContainsKey(e.RoadId))
            &&afterAudit.Edges.GroupBy(e=>e.RoadId).All(g=>Math.Abs(g.Sum(e=>e.Length)-roadLengths[g.Key])<.00001)
            &&afterAudit.Edges.Select(e=>e.RoadId).Distinct().Count()==roadLengths.Count
            &&afterAudit.AccessPoints[0].Position==beforeAudit.AccessPoints[0].Position,
            "successful access audit adds no automatic route and preserves the map's real road geometry");
        var blankMap=mapRegistry.MapForKnownAddresses([]);
        Check(blankMap.Nodes.Count==0&&blankMap.Edges.Count==0&&blankMap.Buildings.Count==0&&blankMap.AccessPoints.Count==0&&blankMap.KnownStreets.Count==0,
            "empty notebook map contains no house, entrance or street-name discovery");
        Check(MatchesExhaustive(nativeProjection),"broadphase preserves the native double-precision access projection");
        Check(MatchesExhaustive(accessGraph.Graph,accessGraph.Streets),"broadphase preserves repeated physical-access graph records");
        Check(MatchesExhaustive(mapRegistry.Graph,mapRegistry.Streets),"broadphase preserves both street origins and notebook access graph records");
        return done;
    }
}
