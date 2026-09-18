using System.Reflection;
using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>Measures proposed repairs without instantiating or moving a house.
/// Query results are review evidence, not approval or traversal acceptance.</summary>
public static class AddressRemediationGeometryProof
{
    // Review candidates, not runtime placement data. Native runs measure these
    // together before an author is asked to approve a settlement repair.
    private static readonly Dictionary<string,Vector2> Candidates=new(StringComparer.Ordinal)
    {
        ["ADR-H006"]=new(-22,38),["ADR-H007"]=new(22,40),
        ["ADR-H010"]=new(-22.5f,24),["ADR-H011"]=new(27,28.5f),
        ["ADR-H012"]=new(-18.5f,13.5f),["ADR-H013"]=new(18,17),
        ["ADR-H014"]=new(-31.5f,17),["ADR-H015"]=new(9.5f,17),
        ["ADR-H019"]=new(9.8f,4),["ADR-H021"]=new(-11.5f,-29.5f),
        ["ADR-H024"]=new(-24,-47),["ADR-H032"]=new(20,-47),
        ["ADR-H041"]=new(-38.5f,-6),["ADR-H044"]=new(-11.5f,35.5f),
        ["ADR-H045"]=new(-31.5f,29.5f),["ADR-H046"]=new(6.2f,-49.6f)
    };
    // One normal-height model with the same local front reference Z=1.3.
    // This is a conservative envelope query, never a visible runtime blockout.
    private static readonly Aabb CompactLocalBounds=new(new(-2.4f,.04f,-4.3f),new(4.8f,4.06f,5.6f));
    private static readonly Dictionary<string,Vector2> CompactCandidates=new(StringComparer.Ordinal)
    {
        ["ADR-H014"]=new(-11,12.9f),["ADR-H019"]=new(15.1f,-6.1f),
        ["ADR-H024"]=new(-16.9f,-45.7f),["ADR-H044"]=new(-11.5f,33.5f),
        ["ADR-H045"]=new(-30.6f,27.5f),["ADR-H046"]=new(7.5f,-49)
    };
    public static void Capture(Act1ConnectedWorld world,FirstPersonController player,string directory,bool boundedSearchOnly=false)
    {
        var registry=world.AddressRegistry??throw new InvalidOperationException("Missing settlement registry.");
        var path=Path.Combine(directory,"address-remediation-geometry.json");
        if(File.Exists(path))throw new IOException("Refusing to overwrite "+path);
        var nodes=Descendants(world).OfType<Node3D>().ToArray();
        var bridge=world.GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var beforeState=bridge?.SelectRuntimeState().GetRawText();
        var beforePlayer=player.GlobalTransform;
        var sourcePoses=new Dictionary<Node3D,Transform3D>();
        var proposals=new List<object>();
        var finalBounds=new Dictionary<string,Aabb>(StringComparer.Ordinal);
        var preferredCompactBounds=new Dictionary<string,Aabb>(StringComparer.Ordinal);
        var revisedBounds=new Dictionary<string,Aabb>(StringComparer.Ordinal);
        var actualRoofs=AgentBAct1ExteriorLayer.BuildingRoofBounds(world);
        var space=world.GetWorld3D().DirectSpaceState;
        foreach(var id in Candidates.Keys)
        {
            var record=registry.Addresses[id];
            var source=nodes.Single(n=>n.HasMeta("building_id")&&n.GetMeta("building_id").AsString()==record.BuildingId);
            sourcePoses[source]=source.GlobalTransform;
            // H021 is a whole compound. Only its dwelling changes size; its
            // fence, garage and bath are not stretched with the house.
            var geometry=id=="ADR-H021"?Descendants(source).OfType<Node3D>().Single(n=>n.Name=="VillageParcel_VariantC_BanyaYard_Dwelling"):source;
            sourcePoses[geometry]=geometry.GlobalTransform;
            var sourceInverse=geometry.GlobalTransform.AffineInverse();
            var rigidBasis=geometry.GlobalBasis.Orthonormalized();
            var offset=geometry.GlobalPosition.Y-AgentBAct1HeightField.CollisionGround(geometry.GlobalPosition.X,geometry.GlobalPosition.Z);
            var moveTo=Candidates[id];
            var proposedOrigin=new Vector3(moveTo.X,AgentBAct1HeightField.CollisionGround(moveTo.X,moveTo.Y)+offset,moveTo.Y);
            Vector3 At(Vector2 xz)=>new(xz.X,AgentBAct1HeightField.CollisionGround(xz.X,xz.Y)+offset,xz.Y);
            var shapes=Descendants(source).OfType<CollisionShape3D>().Where(s=>!s.Disabled&&s.Shape is not null&&s.GetParent() is CollisionObject3D body&&(body.CollisionLayer&3)!=0
                &&(geometry==source||s.Name.ToString().Contains("VariantC_BanyaYard_Dwelling",StringComparison.Ordinal))).ToArray();
            var excluded=new global::Godot.Collections.Array<Rid>{player.GetRid()};
            // A compound proxy can contain both the changing dwelling and the
            // retained fence/bath/garage. Excluding its RID would hide all of
            // those obstacles. Filter only the exact old shape owners below.
            var replacedShapes=shapes.ToHashSet();
            if(geometry==source)
                foreach(var body in Descendants(source).OfType<CollisionObject3D>())excluded.Add(body.GetRid());
            bool IsReplacedShape(CollisionObject3D? body,int index)
            {
                if(body is null)return false;
                return body.ShapeOwnerGetOwner(body.ShapeFindOwner(index)) is CollisionShape3D oldShape&&replacedShapes.Contains(oldShape);
            }
            var visible=Descendants(geometry).OfType<MeshInstance3D>().Where(m=>m.IsVisibleInTree()&&m.Mesh is not null).ToArray();
            var localCorners=visible.SelectMany(m=>Enumerable.Range(0,8).Select(i=>sourceInverse*m.GlobalTransform*m.Mesh!.GetAabb().GetEndpoint(i))).ToArray();
            var candidates=new List<(string Name,Transform3D Transform,bool Compact,bool LegacyWorldEnvelope,bool OriginalProposal,bool PreferredCompact)>
            {
                ("current-observed",geometry.GlobalTransform,false,false,false,false),
                ("full-scale-same-anchor",new(rigidBasis,geometry.GlobalPosition),false,false,false,false),
                (id=="ADR-H046"?"compact-house-envelope-proposal":"coordinated-full-scale-proposal",
                    new(rigidBasis,proposedOrigin),id=="ADR-H046",id=="ADR-H046",true,false)
            };
            if(CompactCandidates.TryGetValue(id,out var compactAt))
            {
                var compactBasis=id=="ADR-H019"?new Basis(Vector3.Up,Mathf.DegToRad(-90)):rigidBasis;
                candidates.Add(("compact-shared-model-current-anchor",new(rigidBasis,geometry.GlobalPosition),true,false,false,false));
                candidates.Add(("compact-shared-model-preferred-proposal",new(compactBasis,At(compactAt)),true,false,false,true));
                if(id=="ADR-H019")
                {
                    candidates.Add(("compact-shared-model-original-yaw-wide-gap",new(rigidBasis,At(new(4.4f,-21.4f))),true,false,false,false));
                    candidates.Add(("compact-shared-model-original-yaw-nearby",new(rigidBasis,At(new(15.2f,-5.8f))),true,false,false,false));
                }
            }
            // B30 exposed the actual well, drain and EX repair-bench owners.
            // Keep every historical case; these extra envelopes change no node.
            if(id=="ADR-H012")
            {
                candidates.Add(("compact-preserve-well-current-anchor",new(rigidBasis,geometry.GlobalPosition),true,false,false,false));
                candidates.Add(("compact-preserve-well-and-drain-proposal",new(rigidBasis,At(new(-21.4f,8.8f))),true,false,false,false));
            }
            if(id=="ADR-H014")
                candidates.Add(("compact-preserve-drain-proposal",new(rigidBasis,At(new(-26.2f,14.9f))),true,false,false,false));
            if(id=="ADR-H021")
                candidates.Add(("compact-preserve-ex-yard-current-anchor",new(rigidBasis,geometry.GlobalPosition),true,false,false,false));
            var replacedExternalProxy=id=="ADR-H021"
                ? world.GetNodeOrNull<CollisionShape3D>("village-main-road/Act1BypassCollision/ConnectiveServiceDwelling")
                : null;
            foreach(var candidate in candidates)
            {
                var compact=candidate.Compact;
                var contacts=new Dictionary<(string Source,string Owner,long Shape),(int Count,Vector3 Origin,Vector3 Scale,uint Layer,int Triangle,string ShapeOwner)>();
                var unsupported=new List<string>();
                var hitLimit=false;
                var queryHitLimit=compact?256:32;
                var queryCount=0;var triangleCount=0;var degenerate=0;var ignoredOldShapes=0;
                void Query(Shape3D queryShape,Transform3D at,string sourceShape,int triangle=-1)
                {
                    using var query=new PhysicsShapeQueryParameters3D{Shape=queryShape,Transform=at,CollisionMask=3,Margin=.002f,Exclude=excluded};
                    queryCount++;
                    var hits=space.IntersectShape(query,queryHitLimit);hitLimit|=hits.Count==queryHitLimit;
                    foreach(var hit in hits)
                    {
                        var body=hit["collider"].AsGodotObject() as CollisionObject3D;
                        var shapeIndex=hit["shape"].AsInt32();
                        if(geometry!=source&&IsReplacedShape(body,shapeIndex)){ignoredOldShapes++;continue;}
                        var shapeOwner=body?.ShapeOwnerGetOwner(body.ShapeFindOwner(shapeIndex)) as Node;
                        var key=(sourceShape,body?.GetPath().ToString()??"unknown",(long)shapeIndex);
                        if(contacts.TryGetValue(key,out var old))contacts[key]=(old.Count+1,old.Origin,old.Scale,old.Layer,old.Triangle,old.ShapeOwner);
                        else contacts.Add(key,(1,at.Origin,at.Basis.Scale,body?.CollisionLayer??0,triangle,shapeOwner?.GetPath().ToString()??"unresolved"));
                    }
                }
                var bounds=candidate.LegacyWorldEnvelope?new Aabb(proposedOrigin+new Vector3(-2.4f,.04f,-2.8f),new(4.8f,4.06f,5.6f)):
                    compact?Bounds(Enumerable.Range(0,8).Select(i=>candidate.Transform*CompactLocalBounds.GetEndpoint(i))):
                        Bounds(localCorners.Select(p=>candidate.Transform*p));
                if(candidate.OriginalProposal)finalBounds.Add(id,bounds);
                if(candidate.PreferredCompact)preferredCompactBounds.Add(id,bounds);
                if(candidate.Name is "compact-preserve-well-and-drain-proposal" or "compact-preserve-drain-proposal" or "compact-preserve-ex-yard-current-anchor")
                    revisedBounds.Add(id,bounds);
                // The narrow scope still derives the complete planned layout
                // from this actual world, but performs no historical 66-case query.
                if(boundedSearchOnly)continue;
                if(compact)
                {
                    using var envelope=new BoxShape3D{Size=CompactLocalBounds.Size};
                    var envelopeTransform=candidate.LegacyWorldEnvelope?new Transform3D(Basis.Identity,bounds.GetCenter()):
                        candidate.Transform*new Transform3D(Basis.Identity,CompactLocalBounds.GetCenter());
                    Query(envelope,envelopeTransform,"PROPOSED_COMPACT_ENVELOPE_NOT_A_RUNTIME_MESH");
                }
                else foreach(var shape in shapes)
                {
                    if(shape.Shape is HeightMapShape3D or WorldBoundaryShape3D)
                    {unsupported.Add(shape.GetPath().ToString());continue;}
                    var at=candidate.Transform*sourceInverse*shape.GlobalTransform;
                    if(shape.Shape is not ConcavePolygonShape3D concave)
                    {Query(shape.Shape!,at,shape.GetPath().ToString());continue;}
                    var faces=concave.GetFaces();
                    if(faces.Length>12288){unsupported.Add(shape.GetPath().ToString()+": more than 4096 triangles; coverage incomplete");continue;}
                    for(var i=0;i+2<faces.Length;i+=3)
                    {
                        var a=at*faces[i];var b=at*faces[i+1];var c=at*faces[i+2];
                        var n=(b-a).Cross(c-a);
                        if(n.LengthSquared()<.0000000001f){degenerate++;continue;}
                        // Four millimetres total thickness makes a queryable
                        // convex prism of this exact triangle, preserving holes.
                        n=n.Normalized()*.002f;var center=(a+b+c)/3;
                        using var prism=new ConvexPolygonShape3D{Points=[a-center+n,b-center+n,c-center+n,a-center-n,b-center-n,c-center-n]};
                        Query(prism,new(Basis.Identity,center),shape.GetPath().ToString(),i/3);triangleCount++;
                    }
                }
                var candidateRoofs=compact
                    ? new[]{candidate.LegacyWorldEnvelope
                        ? new Aabb(new(bounds.Position.X,candidate.Transform.Origin.Y+2.7f,bounds.Position.Z),new(bounds.Size.X,1.4f,bounds.Size.Z))
                        : candidate.Transform*new Aabb(new(-2.4f,2.7f,-4.3f),new(4.8f,1.4f,5.6f))}
                    : visible.Where(m=>m.Name.ToString().Contains("Roof",StringComparison.OrdinalIgnoreCase))
                        .Select(m=>candidate.Transform*sourceInverse*m.GlobalTransform*m.Mesh!.GetAabb())
                        .Where(b=>b.Size.X>1&&b.Size.Z>1).ToArray();
                var floors=new List<object>();
                for(var ix=0;ix<=4;ix++)for(var iz=0;iz<=4;iz++)
                {
                    var x=bounds.Position.X+bounds.Size.X*ix/4;var z=bounds.Position.Z+bounds.Size.Z*iz/4;
                    var terrain=AgentBAct1HeightField.CollisionGround(x,z);
                    using var ray=PhysicsRayQueryParameters3D.Create(new(x,terrain+6,z),new(x,terrain-3,z),3,excluded);
                    var hit=space.IntersectRay(ray);
                    var floorBody=hit.Count>0?hit["collider"].AsGodotObject() as CollisionObject3D:null;
                    floors.Add(new{x,z,terrainY=terrain,deltaFromCandidateBase=terrain-candidate.Transform.Origin.Y,
                        insideCompactFootprint=!compact||candidate.LegacyWorldEnvelope||CompactLocalBounds.HasPoint(
                            candidate.Transform.AffineInverse()*new Vector3(x,candidate.Transform.Origin.Y+CompactLocalBounds.GetCenter().Y,z)),
                        hit=hit.Count>0,point=hit.Count>0?P(hit["position"].AsVector3()):null,
                        normal=hit.Count>0?P(hit["normal"].AsVector3()):null,
                        owner=floorBody?.GetPath().ToString(),
                        shapeOwner=floorBody?.ShapeOwnerGetOwner(floorBody.ShapeFindOwner(hit["shape"].AsInt32())) is Node floorShape?floorShape.GetPath().ToString():null,
                        intersectsOldDwelling=hit.Count>0&&IsReplacedShape(floorBody,hit["shape"].AsInt32())});
                }
                proposals.Add(new{addressId=id,record.BuildingId,record.ParcelId,record.HouseNumber,source=source.GetPath().ToString(),
                    candidate=candidate.Name,origin=P(candidate.Transform.Origin),basis=B(candidate.Transform.Basis),footprint=Describe(bounds),
                    geometryScope=geometry.GetPath().ToString(),wholeParcelScaled=false,compactEnvelopeOnly=compact,
                    legacyWorldEnvelope=candidate.LegacyWorldEnvelope,
                    compactLocalBounds=compact&&!candidate.LegacyWorldEnvelope?Describe(CompactLocalBounds):null,
                    compactWorldCorners=compact&&!candidate.LegacyWorldEnvelope?Enumerable.Range(0,8).Select(i=>P(candidate.Transform*CompactLocalBounds.GetEndpoint(i))).ToArray():null,
                    shiftMetres=geometry.GlobalPosition.DistanceTo(candidate.Transform.Origin),queryCount,triangleCount,trianglePrismThickness=.004f,degenerateTriangles=degenerate,ignoredOldShapes,
                    shapeCoverageComplete=queryCount>0&&unsupported.Count==0&&degenerate==0&&!hitLimit,
                    unsupportedShapes=unsupported,contacts=contacts.Select(h=>new{proposedShape=h.Key.Source,owner=h.Key.Owner,shape=h.Key.Shape,
                        shapeOwner=h.Value.ShapeOwner,hitCount=h.Value.Count,queryOrigin=P(h.Value.Origin),queryScale=P(h.Value.Scale),layer=h.Value.Layer,firstTriangle=h.Value.Triangle,
                        sameDwellingExternalProxy=replacedExternalProxy is not null&&h.Value.ShapeOwner==replacedExternalProxy.GetPath().ToString()}),
                    replacedExternalProxy=replacedExternalProxy?.GetPath().ToString(),externalProxyStillIncludedInQuery=true,
                    treeRoofProbe=TreeRoofProbe(world,nodes,id,actualRoofs,candidateRoofs,compact,sourcePoses),
                    contactListReachedLimit=hitLimit,queryHitLimit,contactOwnershipComplete=contacts.All(h=>h.Value.ShapeOwner!="unresolved"),floors,
                    nearby=Neighborhood(nodes,bounds,source),finalPlacementOnly=true,relocationSweepTest=false,geometryChanged=false,accepted=false});
            }
        }
        var auxiliaries=new List<object>();
        foreach(var name in boundedSearchOnly?Array.Empty<string>():new[]{"ZiratVillageEdgeEastShed","PerimeterWestStreetShed","ArrivalReverseEastDomesticShed","ReturnWestBanyaSilhouette"})
        {
            var source=nodes.SingleOrDefault(n=>n.Name==name&&n.IsVisibleInTree());
            if(source is null){auxiliaries.Add(new{name,missing=true});continue;}
            sourcePoses[source]=source.GlobalTransform;
            var bounds=Bounds(Descendants(source).OfType<MeshInstance3D>().Where(m=>m.Mesh is not null&&m.IsVisibleInTree())
                .SelectMany(m=>Enumerable.Range(0,8).Select(i=>m.GlobalTransform*m.Mesh!.GetAabb().GetEndpoint(i))));
            auxiliaries.Add(new{name,source=source.GetPath().ToString(),origin=P(source.GlobalPosition),bounds=Describe(bounds),
                logicalAnchor=source.HasMeta("logicalAnchor")?source.GetMeta("logicalAnchor").AsString():null,
                nearby=Neighborhood(nodes,bounds,source),ownershipAssigned=false});
        }
        var pairs=finalBounds.SelectMany((a,index)=>finalBounds.Skip(index+1).Select(b=>new{first=a.Key,second=b.Key,
            separationXZ=Separation(a.Value,b.Value),aabbOverlap=Separation(a.Value,b.Value)==0})).Where(p=>p.separationXZ<1.1f).ToArray();
        var compactLayout=finalBounds.ToDictionary(p=>p.Key,p=>preferredCompactBounds.GetValueOrDefault(p.Key,p.Value),StringComparer.Ordinal);
        var compactPairs=compactLayout.SelectMany((a,index)=>compactLayout.Skip(index+1).Select(b=>new{first=a.Key,second=b.Key,
            separationXZ=Separation(a.Value,b.Value),aabbOverlap=Separation(a.Value,b.Value)==0})).Where(p=>p.separationXZ<1.1f).ToArray();
        var revisedLayout=compactLayout.ToDictionary(p=>p.Key,p=>revisedBounds.GetValueOrDefault(p.Key,p.Value),StringComparer.Ordinal);
        var revisedPairs=revisedLayout.SelectMany((a,index)=>revisedLayout.Skip(index+1).Select(b=>new{first=a.Key,second=b.Key,
            separationXZ=Separation(a.Value,b.Value),aabbOverlap=Separation(a.Value,b.Value)==0})).Where(p=>p.separationXZ<1.1f).ToArray();
        var boundedCompactSearch=BoundedCompactSearch(world,player,nodes,registry,revisedLayout,sourcePoses);
        var unchanged=beforePlayer==player.GlobalTransform&&sourcePoses.All(p=>p.Key.GlobalTransform==p.Value)
            &&beforeState==bridge?.SelectRuntimeState().GetRawText();
        var payload=JsonSerializer.Serialize(new{schemaVersion=5,module=Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId,
            scope=boundedSearchOnly?"remediation-search":"full-remediation-observation",historicalCasesRun=!boundedSearchOnly,acceptance=false,
            plannedBounds=revisedLayout.Select(p=>new{addressId=p.Key,bounds=Describe(p.Value)}).ToArray(),
            proposals,proposedPairClearancesBelow1_1m=pairs,compactProposedAabbClearancesBelow1_1m=compactPairs,
            revisedPreservingExAabbClearancesBelow1_1m=revisedPairs,boundedCompactSearch,
            compactPairMetric="Conservative AABB only; rotated envelope query is stored per candidate.",
            auxiliaries,poseAndStateUnchanged=unchanged,geometryChanged=false,authorApproval=false},new JsonSerializerOptions{WriteIndented=true});
        var temporary=path+".writing";
        using(var stream=new FileStream(temporary,FileMode.CreateNew,System.IO.FileAccess.Write,FileShare.None))
        {using var writer=new StreamWriter(stream);writer.Write(payload);writer.Flush();stream.Flush(true);}
        File.Move(temporary,path,false);
        if(!unchanged)throw new InvalidOperationException("Read-only remediation evidence changed pose or runtime state.");
    }
    private static object[] BoundedCompactSearch(Act1ConnectedWorld world,FirstPersonController player,Node3D[] nodes,
        SettlementRegistry registry,Dictionary<string,Aabb> planned,Dictionary<Node3D,Transform3D> poses)
    {
        var results=new List<object>();
        var space=world.GetWorld3D().DirectSpaceState;
        var roads=registry.Graph.Roads.Values.Where(r=>!r.Id.StartsWith("access/",StringComparison.Ordinal)).ToArray();
        foreach(var id in new[]{"ADR-H014","ADR-H021"})
        {
            var record=registry.Addresses[id];
            var source=nodes.Single(n=>n.HasMeta("building_id")&&n.GetMeta("building_id").AsString()==record.BuildingId);
            var geometry=id=="ADR-H021"?Descendants(source).OfType<Node3D>().Single(n=>n.Name=="VillageParcel_VariantC_BanyaYard_Dwelling"):source;
            poses.TryAdd(source,source.GlobalTransform);poses.TryAdd(geometry,geometry.GlobalTransform);
            var basis=geometry.GlobalBasis.Orthonormalized();
            var old=geometry.GlobalPosition;var offset=old.Y-AgentBAct1HeightField.CollisionGround(old.X,old.Z);
            var region=id=="ADR-H014"?new Rect2(-35,4,28,21):new Rect2(-24,-38,18,16);
            var oldShapes=Descendants(source).OfType<CollisionShape3D>().Where(s=>!s.Disabled&&s.Shape is not null
                &&(geometry==source||s.Name.ToString().Contains("VariantC_BanyaYard_Dwelling",StringComparison.Ordinal))).ToHashSet();
            var oldProxy=id=="ADR-H021"?world.GetNodeOrNull<CollisionShape3D>("village-main-road/Act1BypassCollision/ConnectiveServiceDwelling"):null;
            var excluded=new global::Godot.Collections.Array<Rid>{player.GetRid()};
            if(geometry==source)foreach(var body in Descendants(source).OfType<CollisionObject3D>())excluded.Add(body.GetRid());
            var positions=new List<Vector2>();
            for(var x=region.Position.X;x<=region.End.X+.001f;x+=.5f)
            for(var z=region.Position.Y;z<=region.End.Y+.001f;z+=.5f)positions.Add(new(x,z));
            positions=positions.OrderBy(p=>p.DistanceSquaredTo(new(old.X,old.Z))).ThenBy(p=>p.X).ThenBy(p=>p.Y).ToList();
            var rejects=new Dictionary<string,int>(StringComparer.Ordinal);
            void Reject(string reason){rejects[reason]=rejects.GetValueOrDefault(reason)+1;}
            var found=new List<object>();var selected=new List<Vector2>();var examined=0;var queries=0;var saturated=0;var unresolved=0;
            using var envelope=new BoxShape3D{Size=CompactLocalBounds.Size};
            foreach(var at in positions)
            {
                examined++;
                if(selected.Any(p=>p.DistanceTo(at)<1.5f)){Reject("less than 1.5 metres from an already reported alternative");continue;}
                var origin=new Vector3(at.X,AgentBAct1HeightField.CollisionGround(at.X,at.Y)+offset,at.Y);
                var transform=new Transform3D(basis,origin);
                var corners=Enumerable.Range(0,8).Select(i=>transform*CompactLocalBounds.GetEndpoint(i)).ToArray();
                var bounds=Bounds(corners);
                var polygon=Footprint(transform,CompactLocalBounds);
                var clash=planned.FirstOrDefault(p=>p.Key!=id&&Separation(bounds,p.Value)<1.1f);
                if(clash.Key is not null){Reject("planned building clearance: "+clash.Key);continue;}
                // Existing public roads and authored EX paths are constraints even
                // where the snow surface has no separate collision body.
                string? routeBlock=null;
                foreach(var road in roads)
                {
                    for(var i=1;i<road.Points.Count;i++)
                        if(SegmentPolygonDistance(new((float)road.Points[i-1].X,(float)road.Points[i-1].Z),
                            new((float)road.Points[i].X,(float)road.Points[i].Z),polygon)<road.Width*.5+.4)
                        {routeBlock=road.Id;break;}
                    if(routeBlock is not null)break;
                }
                if(routeBlock is not null){Reject("retained road/path: "+routeBlock);continue;}
                // Preserve the authored drain's approach and board manipulation
                // space; these are usable floor, not necessarily solid obstacles.
                var drainTransform=new Transform3D(new Basis(Vector3.Up,Mathf.DegToRad(-45)),new(-15.5f,0,8.8f));
                var drainApproach=Footprint(drainTransform,new(new(-.4f,0,-3.1f),new(2.95f,1,6.2f)));
                if(PolygonDistance(polygon,drainApproach)<.4f){Reject("retained drain interaction/approach space");continue;}
                var water=registry.Constraints.FirstOrDefault(c=>c.Kind=="water"&&PolygonDistance(polygon,
                    c.Boundary.Select(p=>new Vector2((float)p.X,(float)p.Z)).ToArray())<.4f);
                if(water is not null){Reject("water boundary: "+water.Id);continue;}
                using var query=new PhysicsShapeQueryParameters3D{Shape=envelope,
                    Transform=transform*new Transform3D(Basis.Identity,CompactLocalBounds.GetCenter()),
                    CollisionMask=3,Margin=.002f,Exclude=excluded};
                var hits=space.IntersectShape(query,256);queries++;
                if(hits.Count==256){saturated++;Reject("native hit limit: coverage incomplete");continue;}
                var contacts=new List<object>();var blocked=false;var owners=new HashSet<string>(StringComparer.Ordinal);
                foreach(var hit in hits)
                {
                    var body=hit["collider"].AsGodotObject() as CollisionObject3D;var index=hit["shape"].AsInt32();
                    var owner=body?.ShapeOwnerGetOwner(body.ShapeFindOwner(index)) as Node;
                    var oldDwelling=owner is CollisionShape3D shape&&(oldShapes.Contains(shape)||shape==oldProxy);
                    var terrain=body?.Name=="AgentB_TerrainCollision"&&owner?.Name=="AgentB_TerrainFaces";
                    var ownerPath=owner?.GetPath().ToString()??"unresolved";
                    contacts.Add(new{owner=body?.GetPath().ToString(),shape=index,shapeOwner=ownerPath,replacedDwellingOnly=oldDwelling,terrain});
                    if(owner is null){unresolved++;blocked=true;}
                    if(!oldDwelling&&!terrain){blocked=true;owners.Add(ownerPath);}
                }
                if(blocked){foreach(var owner in owners)Reject("native retained shape: "+owner);continue;}
                var floors=new List<object>();
                for(var ix=0;ix<=4;ix++)for(var iz=0;iz<=4;iz++)
                {
                    var local=new Vector3(CompactLocalBounds.Position.X+CompactLocalBounds.Size.X*ix/4,0,
                        CompactLocalBounds.Position.Z+CompactLocalBounds.Size.Z*iz/4);
                    var point=transform*local;var terrain=AgentBAct1HeightField.CollisionGround(point.X,point.Z);
                    using var ray=PhysicsRayQueryParameters3D.Create(new(point.X,terrain+6,point.Z),new(point.X,terrain-3,point.Z),3,excluded);
                    var hit=space.IntersectRay(ray);var body=hit.Count>0?hit["collider"].AsGodotObject() as CollisionObject3D:null;
                    var owner=body?.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
                    floors.Add(new{x=point.X,z=point.Z,terrainY=terrain,deltaFromCandidateBase=terrain-origin.Y,hit=hit.Count>0,
                        point=hit.Count>0?P(hit["position"].AsVector3()):null,normal=hit.Count>0?P(hit["normal"].AsVector3()):null,
                        owner=body?.GetPath().ToString(),shapeOwner=owner?.GetPath().ToString(),
                        replacedDwellingOnly=owner is CollisionShape3D shape&&(oldShapes.Contains(shape)||shape==oldProxy)});
                }
                found.Add(new{origin=P(origin),basis=B(basis),footprint=Describe(bounds),compactWorldCorners=corners.Select(P).ToArray(),
                    shiftXZ=at.DistanceTo(new(old.X,old.Z)),contacts,floors,shapeCoverageComplete=true,contactOwnershipComplete=true,
                    futureBuildingClearances=planned.Where(p=>p.Key!=id)
                        .Select(p=>new{addressId=p.Key,separationXZ=Separation(bounds,p.Value)}).OrderBy(p=>p.separationXZ).ToArray(),
                    clearEnvelopeOnly=true,foundationVerified=false,entranceVerified=false,authorParcelReviewRequired=true,accepted=false});
                selected.Add(at);if(found.Count==3)break;
            }
            results.Add(new{addressId=id,record.BuildingId,record.ParcelId,record.HouseNumber,source=source.GetPath().ToString(),
                currentOrigin=P(old),fixedBasis=B(basis),normalHeightEnvelope=Describe(CompactLocalBounds),
                searchRegionXZ=new{minX=region.Position.X,minZ=region.Position.Y,maxX=region.End.X,maxZ=region.End.Y},
                gridMetres=.5f,maximumGridPositions=positions.Count,examined,queries,alternatives=found,
                candidateSeparationMetres=1.5f,plannedBuildingClearanceMetres=1.1f,roadEdgeClearanceMetres=.4f,
                boundedSearchExhausted=examined==positions.Count,searchCoverageComplete=saturated==0&&unresolved==0,
                saturationCount=saturated,unresolvedContactCount=unresolved,
                rejectionCounts=rejects.OrderByDescending(p=>p.Value).Select(p=>new{reason=p.Key,count=p.Value}).ToArray(),
                actualParcelBoundaryKnown=false,newParcelOwnershipAssigned=false,geometryChanged=false,accepted=false});
        }
        return results.ToArray();
    }
    private static Vector2[] Footprint(Transform3D transform,Aabb local)
    {
        var a=local.Position;var b=local.End;
        return new[]{new Vector3(a.X,0,a.Z),new Vector3(b.X,0,a.Z),new Vector3(b.X,0,b.Z),new Vector3(a.X,0,b.Z)}
            .Select(p=>transform*p).Select(p=>new Vector2(p.X,p.Z)).ToArray();
    }
    private static float PolygonDistance(Vector2[] a,Vector2[] b)
    {
        if(a.Length<3||b.Length<3)return float.PositiveInfinity;
        if(Geometry2D.IsPointInPolygon(a[0],b)||Geometry2D.IsPointInPolygon(b[0],a))return 0;
        return Enumerable.Range(0,a.Length).Min(i=>SegmentPolygonDistance(a[i],a[(i+1)%a.Length],b));
    }
    private static float SegmentPolygonDistance(Vector2 a,Vector2 b,Vector2[] polygon)
    {
        if(Geometry2D.IsPointInPolygon(a,polygon)||Geometry2D.IsPointInPolygon(b,polygon))return 0;
        var best=float.PositiveInfinity;
        for(var i=0;i<polygon.Length;i++)
        {
            var c=polygon[i];var d=polygon[(i+1)%polygon.Length];
            if(Geometry2D.SegmentIntersectsSegment(a,b,c,d).VariantType!=Variant.Type.Nil)return 0;
            best=Math.Min(best,Math.Min(Math.Min(a.DistanceTo(Geometry2D.GetClosestPointToSegment(a,c,d)),b.DistanceTo(Geometry2D.GetClosestPointToSegment(b,c,d))),
                Math.Min(c.DistanceTo(Geometry2D.GetClosestPointToSegment(c,a,b)),d.DistanceTo(Geometry2D.GetClosestPointToSegment(d,a,b)))));
        }
        return best;
    }
    private static object? TreeRoofProbe(Act1ConnectedWorld world,Node3D[] nodes,string addressId,Aabb[] currentRoofs,
        Aabb[] proposedRoofs,bool compact,Dictionary<Node3D,Transform3D> poses)
    {
        // Names identify these exact observed B30 contacts only. Runtime planting
        // remains spatial and must never suppress a generated Plant number.
        var name=addressId switch
        {
            "ADR-H006"=>"PlantedStem_WinterLinden_1_Plant33",
            "ADR-H024"=>"PlantedStem_WinterBirch_2_Plant14",
            "ADR-H032"=>"PlantedStem_WinterBirch_2_Plant15",
            "ADR-H046"=>"PlantedStem_WinterMaple_1_Plant70",
            _=>null
        };
        if(name is null)return null;
        var stem=nodes.OfType<CollisionShape3D>().SingleOrDefault(n=>n.Name==name);
        if(stem is null)return new{expectedObservedStem=name,found=false,accepted=false};
        poses.TryAdd(stem,stem.GlobalTransform);
        var ownerPath=stem.HasMeta("geometryOwner")?stem.GetMeta("geometryOwner").AsString():null;
        var geometry=ownerPath is null?null:world.GetNodeOrNull<Node3D>(ownerPath);
        if(geometry is not null)poses.TryAdd(geometry,geometry.GlobalTransform);
        var root=stem.GlobalPosition;
        return new{expectedObservedStem=name,found=true,stem=stem.GetPath().ToString(),shapeType=stem.Shape?.GetClass().ToString(),
            geometryOwner=ownerPath,geometryResolved=geometry is not null,geometryVisible=geometry?.IsVisibleInTree(),
            rootWorld=P(root),geometryRootWorld=geometry is null?null:P(geometry.GlobalPosition),
            plantPosition=stem.HasMeta("plantPosition")?P(stem.GetMeta("plantPosition").AsVector3()):null,
            plantVariant=stem.HasMeta("plantVariant")?stem.GetMeta("plantVariant").AsString():null,
            actualCurrentRoofSuppresses=AgentBAct1ExteriorLayer.UnderBuildingRoof(root,currentRoofs),
            proposedRoofSuppresses=AgentBAct1ExteriorLayer.UnderBuildingRoof(root,proposedRoofs),
            proposedRoofBounds=proposedRoofs.Select(Describe).ToArray(),
            roofSource=compact?"planned compact eave/ridge envelope; new mesh not yet authored":"transformed existing visible Roof mesh bounds",
            usesExistingRoofGeometry=!compact,futureCompactRoofAuthored=false,
            requiresRoofBeforePlantFoliage=true,suppressionApplied=false,accepted=false};
    }
    private static object[] Neighborhood(Node3D[] nodes,Aabb bounds,Node3D source)
    {
        var area=bounds.Grow(8);
        return nodes.Where(n=>n!=source&&!source.IsAncestorOf(n)&&n.IsVisibleInTree()&&(n.HasMeta("building_id")||n.HasMeta("logicalAnchor")||
            n.Name.ToString().Contains("Fence",StringComparison.Ordinal)||n.Name.ToString().Contains("Gate",StringComparison.Ordinal)))
            .Where(n=>area.HasPoint(new(n.GlobalPosition.X,bounds.GetCenter().Y,n.GlobalPosition.Z)))
            .OrderBy(n=>new Vector2(n.GlobalPosition.X-bounds.GetCenter().X,n.GlobalPosition.Z-bounds.GetCenter().Z).LengthSquared()).Take(80)
            .Select(n=>(object)new{owner=n.GetPath().ToString(),position=P(n.GlobalPosition),
                buildingId=n.HasMeta("building_id")?n.GetMeta("building_id").AsString():null,
                addressId=n.HasMeta("address_id")?n.GetMeta("address_id").AsString():null}).ToArray();
    }
    private static Aabb Bounds(IEnumerable<Vector3> values)
    {var p=values.ToArray();if(p.Length==0)throw new InvalidOperationException("No visible source geometry.");var min=p[0];var max=p[0];foreach(var q in p){min=min.Min(q);max=max.Max(q);}return new(min,max-min);}
    private static object Describe(Aabb bounds)=>new{min=P(bounds.Position),max=P(bounds.End),size=P(bounds.Size)};
    private static float Separation(Aabb a,Aabb b)=>new Vector2(Math.Max(0,Math.Max(a.Position.X-b.End.X,b.Position.X-a.End.X)),Math.Max(0,Math.Max(a.Position.Z-b.End.Z,b.Position.Z-a.End.Z))).Length();
    private static object P(Vector3 p)=>new{x=p.X,y=p.Y,z=p.Z};
    private static object B(Basis b)=>new{x=P(b.X),y=P(b.Y),z=P(b.Z)};
    private static IEnumerable<Node> Descendants(Node node){foreach(var child in node.GetChildren()){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
}
