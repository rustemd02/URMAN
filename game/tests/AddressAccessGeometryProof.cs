using System.Reflection;
using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>Read-only physical observations beside the live address audit. No
/// relocation, geometry repair, new graph, unlocked door or runtime effect.</summary>
public static class AddressAccessGeometryProof
{
    private sealed record Floor(Vector3 Point, Vector3 Normal, string Owner);
    private sealed record Contact(string Owner, long Shape, uint Layer);
    private sealed record Sample(string Label, object SuppliedPoint, object TerrainPoint,
        object? ShortFloor, bool ShortFloorPolicyAccepts, IReadOnlyList<Contact> VerifierCapsuleContacts,
        bool OriginalVerifierFree, bool PlayerFitsSuppliedPoint, IReadOnlyList<Contact> SuppliedCapsuleContacts,
        IReadOnlyList<object> VerticalSurfaces);

    public static void Capture(Act1ConnectedWorld world, FirstPersonController player, string directory)
    {
        if (!Path.IsPathFullyQualified(directory) || !Directory.Exists(directory))
            throw new InvalidOperationException("Address access evidence requires an existing absolute directory.");
        var destination = Path.Combine(directory, "address-access-geometry.json");
        if (File.Exists(destination)) throw new IOException("Refusing to overwrite " + destination);
        var registry = world.AddressRegistry ?? throw new InvalidOperationException("Missing live address registry.");
        var graph = registry.Graph;
        var origin = graph.NodeAt(graph.Roads["authored/main-axis"].Points[0]);
        var connected = origin is null ? new HashSet<string>() : graph.ReachableNodes(origin, SettlementTravelMode.Foot);
        var exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        var space = world.GetWorld3D().DirectSpaceState;
        var beforePose = player.CapturePortableTransform();
        var bridge = world.GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var beforeState = bridge?.SelectRuntimeState().GetRawText();
        using var capsule = new CapsuleShape3D { Radius = .35f, Height = 1.8f };

        Floor? Ray(Vector3 from, Vector3 to)
        {
            using var ray = PhysicsRayQueryParameters3D.Create(from, to, 3, exclude);
            var hit = space.IntersectRay(ray);
            return hit.Count == 0 ? null : new(hit["position"].AsVector3(), hit["normal"].AsVector3(),
                (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() ?? "unknown");
        }
        IReadOnlyList<Contact> Contacts(Vector3 centre)
        {
            using var query = new PhysicsShapeQueryParameters3D { Shape = capsule, CollisionMask = 3,
                Exclude = exclude, Margin = .001f, Transform = new(Basis.Identity, centre) };
            return space.IntersectShape(query, 16).Select(hit =>
            {
                var owner = hit["collider"].AsGodotObject() as CollisionObject3D;
                return new Contact(owner?.GetPath().ToString() ?? "unknown", hit["shape"].AsInt64(), owner?.CollisionLayer ?? 0);
            }).ToArray();
        }
        object DescribeFloor(Floor floor, Vector3 terrain) => new { point = P(floor.Point), normal = P(floor.Normal),
            floor.Owner, deltaFromTerrain = floor.Point.Y - terrain.Y,
            standingCapsuleContacts = Contacts(floor.Point + Vector3.Up * .955f),
            playerFitsAbove = player.CanStandAt(floor.Point + Vector3.Up * .035f) };
        Sample Observe(string label, Vector3 supplied)
        {
            var terrain = Act1ConnectedWorld.AddressGround(supplied);
            var floor = Ray(terrain + Vector3.Up * .55f, terrain - Vector3.Up * .75f);
            var policy = floor is not null && floor.Normal.Y >= .64f && Math.Abs(floor.Point.Y - terrain.Y) <= .42f;
            var contacts = floor is null ? Array.Empty<Contact>() : Contacts(floor.Point + Vector3.Up * .955f);
            var surfaces = new List<object>();
            // Record the entire vertical stack. A high observation is evidence,
            // not permission to choose a roof as a walkable entrance.
            var top = new Vector3(supplied.X, Math.Max(supplied.Y, terrain.Y) + 6f, supplied.Z);
            var bottom = new Vector3(supplied.X, Math.Min(supplied.Y, terrain.Y) - 2f, supplied.Z);
            for (var layer = 0; layer < 12 && top.Y > bottom.Y; layer++)
            {
                var surface = Ray(top, bottom);
                if (surface is null) break;
                surfaces.Add(DescribeFloor(surface, terrain));
                top = surface.Point - Vector3.Up * .015f;
            }
            return new(label, P(supplied), P(terrain), floor is null ? null : DescribeFloor(floor, terrain),
                policy, contacts, policy && contacts.Count == 0, player.CanStandAt(supplied),
                Contacts(supplied + Vector3.Up * .90f), surfaces);
        }

        var records = new List<object>();
        foreach (var id in new[] { "ADR-BABAI", "ADR-FAP", "ADR-MOSQUE", "ADR-H020", "ADR-H031", "ADR-H040", "ADR-H049", "ADR-H001" })
        {
            if (!registry.Addresses.TryGetValue(id, out var address))
            { records.Add(new { addressId = id, missing = true }); continue; }
            var access = registry.AccessPoints[address.AccessId];
            var target = Act1ConnectedWorld.AddressVector(access.Position);
            var nearest = graph.Nearest(access.Position, filter: edge => connected.Contains(edge.A) && connected.Contains(edge.B));
            var samples = new List<Sample> { Observe("registered access", target) };
            var line = new List<Sample>();
            object? road = null;
            if (nearest is { } found)
            {
                var anchor = found.Point;
                var start = Act1ConnectedWorld.AddressVector(anchor);
                if (new Vector2(start.X - target.X, start.Z - target.Z).Length() < .01f)
                {
                    anchor = new[] { graph.Nodes[found.Edge.A].Position, graph.Nodes[found.Edge.B].Position }
                        .OrderBy(point => point.DistanceXZ(access.Position)).First();
                    start = Act1ConnectedWorld.AddressVector(anchor);
                }
                road = new { found.Edge, exactAnchor = anchor, engineAnchor = P(start),
                    engineRoundTripErrorMetres = anchor.Distance(Act1ConnectedWorld.AddressPoint(start)) };
                samples.Add(Observe("nearest connected road start", start));
                var steps = Math.Max(1, (int)Math.Ceiling(start.DistanceTo(target) / .25f));
                for (var step = 0; step <= Math.Min(steps, 300); step++)
                {
                    var sample = Observe("direct segment " + step + "/" + steps, start.Lerp(target, (float)step / steps));
                    line.Add(sample);
                    // Preserve the first failed point plus its actual next two
                    // quarter-metre samples to expose risers versus tall walls.
                    if (!sample.OriginalVerifierFree)
                    {
                        for (var next = step + 1; next <= Math.Min(steps, step + 2); next++)
                            line.Add(Observe("after first direct refusal " + next, start.Lerp(target, (float)next / steps)));
                        break;
                    }
                }
            }
            if (id == "ADR-BABAI")
            {
                samples.Add(Observe("existing HouseDoorApproach; earlier physical walkthrough control", AgentBAct1Layout.HouseDoorApproach));
                foreach (var point in new[] { new Vector3(-25.2f, 0, 2.6f), new(-26.05f, 0, 2.6f), new(-26.05f, 0, .2f) })
                    samples.Add(Observe("existing house yard walk control", Act1ConnectedWorld.AddressGround(point)));
            }
            if (id == "ADR-FAP")
                samples.Add(Observe("existing FAP exterior walkthrough control", Act1ConnectedWorld.AddressGround(new(24, 0, -24))));
            var room = world.PublicBuildingRooms.FirstOrDefault(room => room.AddressId == id);
            if (room is not null)
            {
                samples.Add(Observe("public standing approach", room.Outside));
                samples.Add(Observe("public threshold; door stays in its actual state", room.Entrance));
                samples.Add(Observe("public vestibule; no door opening", room.Vestibule));
                var outward = (room.Outside - room.Entrance); outward.Y = 0; outward = outward.Normalized();
                for (var step = 0; step <= 8; step++)
                    samples.Add(Observe("exterior entrance profile " + step, room.Entrance + outward * (step * .25f)));
            }
            records.Add(new { addressId = id, address.BuildingId, access, road, samples, firstDirectSegment = line });
        }
        if (!beforePose.Equals(player.CapturePortableTransform()) || beforeState != bridge?.SelectRuntimeState().GetRawText())
            throw new InvalidOperationException("Read-only address geometry observation changed player or runtime state.");
        var json = JsonSerializer.Serialize(new { schemaVersion = 1, loadedModule = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId,
            diagnosticOnly = true, traversalAcceptance = false, mutation = "none", playerPosePreserved = true,
            originalVerifier = new { capsuleRadius = .35, capsuleHeight = 1.8, capsuleBottomAboveFloor = .055,
                floorHeightCutoff = .42, floorRayUp = .55, floorRayDown = .75, margin = .001 },
            actualControllerMaximumStepHeight = FirstPersonController.MaximumStepHeight, records }, new JsonSerializerOptions { WriteIndented = true });
        var temporary = destination + ".writing";
        using (var file = new FileStream(temporary, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(file)) { writer.Write(json); writer.Flush(); file.Flush(true); }
        File.Move(temporary, destination, false);
        GD.Print("address-access-geometry: recorded read-only floor and collider evidence: " + destination);
    }

    /// <summary>Native floor and facade queries for the repaired existing plinth.
    /// Records failures before returning false; it does not move the camera or
    /// take responsibility for the separate rendered appearance review.</summary>
    public static bool CaptureMosqueFoundation(Act1ConnectedWorld world, FirstPersonController player, string directory)
    {
        if (!Path.IsPathFullyQualified(directory) || !Directory.Exists(directory))
            throw new InvalidOperationException("Mosque foundation evidence requires an existing absolute directory.");
        var destination = Path.Combine(directory, "mosque-foundation-geometry.json");
        if (File.Exists(destination)) throw new IOException("Refusing to overwrite " + destination);
        var complex = world.GetNode<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex");
        var plinth = complex.GetNode<MeshInstance3D>("MosquePlinth");
        var room = complex.GetNode<Node3D>("MosqueInterior");
        if (!plinth.HasMeta("foundationTerrainSupportRepair") || plinth.Mesh is not BoxMesh box)
            throw new InvalidOperationException("Missing actual repaired mosque plinth.");
        var terrainNode = world.GetNode<CollisionShape3D>(
            "Act1CoreWorldGreybox/AgentBExteriorWorld/AgentB_TerrainCollision/AgentB_TerrainFaces");
        var terrainOwner = terrainNode.GetParent().GetPath().ToString();
        var expectedSource = plinth.GetPath().ToString();
        var contacts = Descendants(world).OfType<CollisionShape3D>().Where(n => !n.Disabled
            && n.HasMeta("authoredSourceMesh") && n.GetMeta("authoredSourceMesh").AsString() == expectedSource).ToArray();
        var bridge = world.GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var beforeState = bridge?.SelectRuntimeState().GetRawText();
        var beforePose = player.CapturePortableTransform();
        var beforePlinth = plinth.GlobalTransform;
        var beforeFloor = room.GlobalTransform;
        var size = box.Size;
        var oldSize = plinth.GetMeta("foundationOriginalSize").AsVector3();
        var oldTop = plinth.GetMeta("foundationOriginalTopWorld").AsVector3();
        var oldBottom = plinth.GetMeta("foundationOriginalBottomWorld").AsVector3();
        var oldFloor = plinth.GetMeta("foundationOriginalRoomFloorWorld").AsVector3();
        var top = plinth.GlobalTransform * new Vector3(0, size.Y * .5f, 0);
        var bottom = plinth.GlobalTransform * new Vector3(0, -size.Y * .5f, 0);
        var topUnchanged = top.DistanceTo(oldTop) <= .00001f;
        var footprintUnchanged = size.X == oldSize.X && size.Z == oldSize.Z
            && Math.Abs(top.X - oldTop.X) <= .00001f && Math.Abs(top.Z - oldTop.Z) <= .00001f;
        var floorUnchanged = room.GlobalPosition == oldFloor;
        var highestTerrain = plinth.GetMeta("foundationHighestTerrainWorldY").AsSingle();
        var space = world.GetWorld3D().DirectSpaceState;
        var exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        var samples = new List<object>();
        var failures = new List<string>();
        if (!topUnchanged) failures.Add("Original plinth top moved.");
        if (!footprintUnchanged) failures.Add("Original plinth footprint changed.");
        if (!floorUnchanged) failures.Add("Original room floor moved.");
        if (contacts.Length != 1) failures.Add("Expected exactly one active authored contact for the existing plinth; found " + contacts.Length);
        object Describe(global::Godot.Collections.Dictionary hit)
        {
            var body = hit["collider"].AsGodotObject() as CollisionObject3D;
            var index = hit["shape"].AsInt32();
            var owner = body?.ShapeOwnerGetOwner(body.ShapeFindOwner(index)) as Node;
            return new { collider = body?.GetPath().ToString(), shape = index, shapeOwner = owner?.GetPath().ToString(),
                authoredSourceMesh = owner?.HasMeta("authoredSourceMesh") == true ? owner.GetMeta("authoredSourceMesh").AsString() : "",
                point = P(hit["position"].AsVector3()), normal = P(hit["normal"].AsVector3()) };
        }
        void SamplePoint(string label, Vector3 local, Vector3 outward)
        {
            var at = plinth.ToGlobal(local);
            using var ray = PhysicsRayQueryParameters3D.Create(new(at.X, Math.Max(top.Y, highestTerrain) + .5f, at.Z),
                new(at.X, bottom.Y - .3f, at.Z), 1, exclude);
            var hit = space.IntersectRay(ray);
            var onTerrain = hit.Count > 0 && (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() == terrainOwner;
            var ground = hit.Count > 0 ? hit["position"].AsVector3().Y : float.NaN;
            var buried = onTerrain && bottom.Y <= ground - .045f;
            if (!buried) failures.Add(label + ": bottom is not embedded in actual terrain.");
            object? side = null;
            if (onTerrain && outward.LengthSquared() > .1f && oldBottom.Y - ground > .10f)
            {
                var center = new Vector3(at.X, (oldBottom.Y + ground) * .5f, at.Z);
                var normal = (plinth.GlobalBasis * outward).Normalized();
                using var sideRay = PhysicsRayQueryParameters3D.Create(center + normal * .12f, center - normal * .12f, 3, exclude);
                var sideHit = space.IntersectRay(sideRay);
                var sideBody = sideHit.Count == 0 ? null : sideHit["collider"].AsGodotObject() as CollisionObject3D;
                var owner = sideBody is null ? null : sideBody.ShapeOwnerGetOwner(sideBody.ShapeFindOwner(sideHit["shape"].AsInt32())) as Node;
                var matches = owner?.HasMeta("authoredSourceMesh") == true && owner.GetMeta("authoredSourceMesh").AsString() == expectedSource;
                side = new { midpointInFormerAirGap = P(center), matchesExistingPlinth = matches,
                    hit = sideHit.Count == 0 ? null : Describe(sideHit) };
                if (!matches) failures.Add(label + ": former air gap lacks the expected plinth surface contact.");
            }
            samples.Add(new { label, at = P(at), actualTerrain = hit.Count == 0 ? null : Describe(hit),
                bottomY = bottom.Y, embedded = buried, side });
        }
        foreach (var x in new[] { -size.X * .5f, size.X * .5f })
        foreach (var z in new[] { -size.Z * .5f, size.Z * .5f })
            SamplePoint($"corner {x}/{z}", new(x, 0, z), Vector3.Zero);
        for (var side = -1; side <= 1; side += 2)
        {
            var alongZ = Math.Max(1, Mathf.CeilToInt(size.Z / .5f));
            for (var i = 1; i < alongZ; i++)
                SamplePoint($"x-edge {side}/{i}", new(side * size.X * .5f, 0, Mathf.Lerp(-size.Z * .5f, size.Z * .5f, i / (float)alongZ)), new(side, 0, 0));
            var alongX = Math.Max(1, Mathf.CeilToInt(size.X / .5f));
            for (var i = 1; i < alongX; i++)
                SamplePoint($"z-edge {side}/{i}", new(Mathf.Lerp(-size.X * .5f, size.X * .5f, i / (float)alongX), 0, side * size.Z * .5f), new(0, 0, side));
        }
        if (!beforePose.Equals(player.CapturePortableTransform()) || beforeState != bridge?.SelectRuntimeState().GetRawText()
            || beforePlinth != plinth.GlobalTransform || beforeFloor != room.GlobalTransform)
            throw new InvalidOperationException("Read-only foundation observations changed actor, state or mosque transforms.");
        var json = JsonSerializer.Serialize(new { schemaVersion = 1, loadedModule = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId,
            passed = failures.Count == 0, diagnosticOnly = true, geometryChangedByObservation = false, renderedAppearance = "separate native frame review required",
            originalTop = P(oldTop), actualTop = P(top), originalBottom = P(oldBottom), actualBottom = P(bottom),
            oldFloor = P(oldFloor), actualFloor = P(room.GlobalPosition), topUnchanged, footprintUnchanged, floorUnchanged,
            originalSize = P(oldSize), actualSize = P(size), activeContacts = contacts.Select(n => n.GetPath().ToString()).ToArray(),
            coveredTriangles = plinth.GetMeta("foundationCoveredTerrainTriangles").AsInt32(),
            coveredArea = plinth.GetMeta("foundationCoveredTerrainArea").AsDouble(),
            footprintArea = plinth.GetMeta("foundationFootprintArea").AsDouble(),
            lowestTerrain = P(plinth.GetMeta("foundationLowestTerrainWorld").AsVector3()), samples, failures }, new JsonSerializerOptions { WriteIndented = true });
        var temporary = destination + ".writing";
        using (var file = new FileStream(temporary, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(file)) { writer.Write(json); writer.Flush(); file.Flush(true); }
        File.Move(temporary, destination, false);
        GD.Print("mosque-foundation: " + (failures.Count == 0 ? "PASS" : "FAIL") + "; " + samples.Count + " actual perimeter observations; " + destination);
        return failures.Count == 0;
    }

    /// <summary>Compare two existing physical leaves per house before changing
    /// primaryDoor. The live audit is suspended by the explicit smoke scope;
    /// this method does not relocate an actor or write an access/route/sign.</summary>
    public sealed record EntranceMountPreview(string AddressId,string SurfaceName,Vector3 Point,Vector3 Outward,
        bool Supported,Vector3 RoadFeet,bool RoadSupported,Vector3 ApproachFeet,bool ApproachSupported);
    public sealed record EntranceBlockedView(string SourceName,string LeafPath,Vector3 Feet,Vector3 Eye,Vector3 Target,
        bool PhysicsLeafMatched,bool KnownVisibleObstruction);

    public static IReadOnlyList<EntranceMountPreview> CaptureEntranceCandidates(Act1ConnectedWorld world, FirstPersonController player, string directory, bool standalone=false,
        ICollection<EntranceBlockedView>? blockedViews=null)
    {
        if (!Path.IsPathFullyQualified(directory) || !Directory.Exists(directory))
            throw new InvalidOperationException("Entrance evidence requires an existing absolute directory.");
        var destination = Path.Combine(directory, standalone ? "address-standalone-candidates.json" : "address-entrance-candidates.json");
        if (File.Exists(destination)) throw new IOException("Refusing to overwrite " + destination);
        var registry = world.AddressRegistry ?? throw new InvalidOperationException("Missing live address registry.");
        using var manifest = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString("res://content/urman.settlement.addresses.v1.json"));
        var graph = registry.Graph;
        var origin = graph.NodeAt(graph.Roads["authored/main-axis"].Points[0])
            ?? throw new InvalidOperationException("Missing known main-road graph origin.");
        var connected = graph.ReachableNodes(origin, SettlementTravelMode.Foot);
        var bridge = world.GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge
            ?? throw new InvalidOperationException("Missing runtime state owner.");
        var beforePose = player.CapturePortableTransform();
        var beforeState = bridge.SelectRuntimeState().GetRawText();
        string RegistryState() => JsonSerializer.Serialize(new { registry.Buildings, registry.Parcels, registry.Addresses, registry.AccessPoints,
            registry.AddressAliases, graph.Nodes, graph.Edges, graph.Roads });
        var beforeRegistry = RegistryState();
        var sources = new Dictionary<string, Node3D>();
        foreach (var id in standalone ? Array.Empty<string>() : new[] { "ADR-H009", "ADR-H016" })
        {
            var buildingId = registry.Addresses[id].BuildingId;
            sources.Add(id, Descendants(world).OfType<Node3D>().Single(n => n.HasMeta("building_id")
                && n.GetMeta("building_id").AsString() == buildingId));
        }
        if(standalone)
        {
            foreach(var binding in Act1ConnectedWorld.StandaloneShedSources)
            {
                var id=SettlementRegistry.StableId("BLD",Act1ConnectedWorld.StandaloneShedSourceKey(binding.SourceName));
                sources.Add(id,Descendants(world).OfType<Node3D>().Single(n=>n.Name==binding.SourceName&&n.IsVisibleInTree()));
            }
            sources.Add("candidate/ReturnWestBanyaSilhouette",Descendants(world).OfType<Node3D>()
                .Single(n=>n.Name=="ReturnWestBanyaSilhouette"&&n.IsVisibleInTree()));
        }
        var beforeTransforms = sources.Values.SelectMany(n => new[] { n }.Concat(Descendants(n).OfType<Node3D>()))
            .Distinct().ToDictionary(n => n, n => n.GlobalTransform);
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var localEye = player.ToLocal(camera.GlobalPosition);
        var rayLength = player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay").TargetPosition.Length();
        var exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        var space = world.GetWorld3D().DirectSpaceState;
        using var capsule = new CapsuleShape3D { Radius = player.BodyRadius, Height = player.StandingBodyHeight };
        using var walker = new AddressWalkProbe(world);

        (Node? Owner, string Source) ShapeOwner(global::Godot.Collections.Dictionary hit)
        {
            if (hit.Count == 0 || hit["collider"].AsGodotObject() is not CollisionObject3D body) return (null, "");
            var index = hit["shape"].AsInt32();
            var owner = body.ShapeOwnerGetOwner(body.ShapeFindOwner(index)) as Node;
            return (owner, owner?.HasMeta("authoredSourceMesh") == true ? owner.GetMeta("authoredSourceMesh").AsString() : "");
        }
        object DescribeHit(global::Godot.Collections.Dictionary hit)
        {
            var shape = ShapeOwner(hit);
            return new { collider = (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() ?? "unknown",
                shape = hit["shape"].AsInt32(), shapeOwner = shape.Owner?.GetPath().ToString() ?? "unresolved",
                authoredSourceMesh = shape.Source };
        }
        object SourceTriangleComparison(Vector3 from,Vector3 to,global::Godot.Collections.Dictionary? hit,
            out bool coverageComplete,out float? triangleDistance,MeshInstance3D? explicitSource=null)
        {
            coverageComplete=false;triangleDistance=null;
            var owner=hit is { Count: > 0 }?ShapeOwner(hit):(Owner:(Node?)null,Source:"");
            var mesh=explicitSource??(owner.Source.Length==0?null:world.GetNodeOrNull<MeshInstance3D>(owner.Source));
            if(mesh?.Mesh is not ArrayMesh array)
                return new{coverageComplete=false,reason="The actual hit has no inspectable authored ArrayMesh.",owner.Source};
            var tested=0;var nearest=float.PositiveInfinity;var surfaceHit=-1;var triangleHit=-1;
            Vector3 nearestPoint=default;var failures=new List<string>();
            var direction=to-from;var segmentLength=direction.Length();
            if(segmentLength<.00001f)return new{coverageComplete=false,reason="Empty diagnostic ray."};
            direction/=segmentLength;
            for(var surface=0;surface<array.GetSurfaceCount();surface++)
            {
                if(array.SurfaceGetPrimitiveType(surface)!=Mesh.PrimitiveType.Triangles)
                {failures.Add("Non-triangle surface "+surface);continue;}
                var arrays=array.SurfaceGetArrays(surface);
                var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var rawIndices=arrays[(int)Mesh.ArrayType.Index];
                var indices=rawIndices.VariantType==Variant.Type.Nil?Array.Empty<int>():rawIndices.AsInt32Array();
                var count=indices.Length==0?vertices.Length:indices.Length;
                if(count==0||count%3!=0){failures.Add("Incomplete surface "+surface);continue;}
                if(tested+count/3>50000){failures.Add("Bounded 50000-triangle limit exceeded");break;}
                for(var i=0;i<count;i+=3)
                {
                    var ia=indices.Length==0?i:indices[i];var ib=indices.Length==0?i+1:indices[i+1];var ic=indices.Length==0?i+2:indices[i+2];
                    if(ia<0||ib<0||ic<0||ia>=vertices.Length||ib>=vertices.Length||ic>=vertices.Length)
                    {failures.Add("Invalid index at surface "+surface+" triangle "+i/3);break;}
                    var a=mesh.GlobalTransform*vertices[ia];var b=mesh.GlobalTransform*vertices[ib];var c=mesh.GlobalTransform*vertices[ic];tested++;
                    // Same two-sided Moller-Trumbore arithmetic as the existing
                    // visible-surface probe, but retain published accessor vertices.
                    var edge1=b-a;var edge2=c-a;var cross=direction.Cross(edge2);var determinant=edge1.Dot(cross);
                    if(Math.Abs(determinant)<.0000001f)continue;
                    var delta=from-a;var inverse=1f/determinant;var u=delta.Dot(cross)*inverse;
                    if(u<0||u>1)continue;
                    var q=delta.Cross(edge1);var v=direction.Dot(q)*inverse;
                    if(v<0||u+v>1)continue;
                    var distance=edge2.Dot(q)*inverse;
                    if(distance>=0&&distance<=segmentLength&&distance<nearest)
                    {nearest=distance;surfaceHit=surface;triangleHit=i/3;nearestPoint=from+direction*distance;}
                }
            }
            var shape=owner.Owner as CollisionShape3D;
            var complete=failures.Count==0&&tested>0;
            coverageComplete=complete;triangleDistance=surfaceHit>=0?nearest:null;
            return new{coverageComplete=complete,testedTriangles=tested,triangleLimit=50000,failures,
                Source=mesh.GetPath().ToString(),sourceVisible=mesh.IsVisibleInTree(),sourceTransform=new{origin=P(mesh.GlobalPosition),x=P(mesh.GlobalBasis.X),y=P(mesh.GlobalBasis.Y),z=P(mesh.GlobalBasis.Z)},
                sourceAabb=new{position=P(array.GetAabb().Position),size=P(array.GetAabb().Size)},
                proxy=shape is null?null:new{path=shape.GetPath().ToString(),shape=shape.Shape?.GetClass().ToString(),
                    boxSize=shape.Shape is BoxShape3D box?P(box.Size):null,
                    origin=P(shape.GlobalPosition),x=P(shape.GlobalBasis.X),y=P(shape.GlobalBasis.Y),z=P(shape.GlobalBasis.Z)},
                physicsPoint=hit is { Count: > 0 }?P(hit["position"].AsVector3()):null,
                physicsDistance=hit is { Count: > 0 }?(float?)from.DistanceTo(hit["position"].AsVector3()):null,
                actualTriangleHit=surfaceHit>=0,actualTrianglePoint=surfaceHit>=0?P(nearestPoint):null,
                actualTriangleDistance=surfaceHit>=0?(float?)nearest:null,surfaceHit,triangleHit,
                rayMissesItsProxySource=hit is { Count: > 0 }&&complete&&surfaceHit<0,
                interpretation=explicitSource is null
                    ?"Only the physics hit's exact authored mesh is examined; no other obstruction is ignored or removed."
                    :"This explicitly identified visible source is examined independently of physics; not a whole-scene visibility audit."};
        }
        (object Evidence, bool Matches) Ray(Vector3 from, Vector3 to, MeshInstance3D? expected = null, IReadOnlySet<string>? expectedDoorParts = null,
            bool inspectProxySource=false)
        {
            using var query = PhysicsRayQueryParameters3D.Create(from, to, 3, exclude);
            var hit = space.IntersectRay(query);
            var actualSource=hit.Count>0?ShapeOwner(hit).Source:"";
            var matches = expected is not null && hit.Count > 0 && (actualSource == expected.GetPath().ToString()
                || expectedDoorParts?.Contains(actualSource)==true);
            return (new { from = P(from), to = P(to), hit = hit.Count > 0,
                owner = hit.Count == 0 ? null : DescribeHit(hit),
                point = hit.Count == 0 ? null : P(hit["position"].AsVector3()),
                normal = hit.Count == 0 ? null : P(hit["normal"].AsVector3()), expectedSourceMatched = matches,
                sourceTriangleComparison=inspectProxySource&&hit.Count>0?SourceTriangleComparison(from,to,hit,out _,out _):null }, matches);
        }
        object ObserveSupport(Vector3 supplied)
        {
            using var query = new PhysicsShapeQueryParameters3D { Shape = capsule, CollisionMask = 3,
                Exclude = exclude, Margin = .001f, Transform = new(Basis.Identity, supplied + Vector3.Up * capsule.Height * .5f) };
            var hits = space.IntersectShape(query, 64);
            return new { supplied = P(supplied), playerCanStand = player.CanStandAt(supplied),
                floor = Ray(supplied + Vector3.Up * .55f, supplied - Vector3.Up * .75f).Evidence,
                fullCapsuleContacts = hits.Select(DescribeHit).ToArray(), contactsSaturated = hits.Count == 64,
                interpretation = "Contacts include a resting floor; acceptance uses the existing controller and motion probe." };
        }
        object DirectPath(Vector3 target, out bool reached)
        {
            reached = false;
            var nearest = graph.Nearest(Act1ConnectedWorld.AddressPoint(target), filter: edge =>
                !edge.RoadId.StartsWith("access/", StringComparison.Ordinal) && connected.Contains(edge.A) && connected.Contains(edge.B));
            if (nearest is null) return new { reached = false, reason = "No existing connected open foot-road anchor." };
            var found = nearest.Value;
            var supplied = Act1ConnectedWorld.AddressVector(found.Point);
            var supported = walker.TrySupport(supplied, out var feet);
            var supportDiagnostic = walker.LastSupportProbe;
            var refusal = supported ? "" : walker.LastRejection;
            var path = new List<Vector3>();
            if (supported) path.Add(feet);
            var beforeSteps = walker.StepsClimbed;
            var advances = 0;
            while (supported && advances < 1000)
            {
                var remaining = new Vector3(target.X - feet.X, 0, target.Z - feet.Z);
                if (remaining.Length() <= .015f)
                {
                    reached = Math.Abs(feet.Y - target.Y) <= .08f;
                    if (!reached) refusal = "Reached XZ but physical support height differs from candidate by more than .08 m.";
                    break;
                }
                var motion = remaining.LimitLength(.08f);
                advances++;
                if (!walker.TryAdvance(feet, motion, out var next)) { refusal = walker.LastRejection; break; }
                feet = next; path.Add(feet);
            }
            if (advances == 1000 && !reached) refusal = "Bounded direct segment limit reached.";
            return new { reached, directOnly = true, routeDiscovery = false, maximumAdvances = 1000, advances,
                found.Edge, exactAnchor = found.Point, suppliedAnchor = P(supplied), supported, supportDiagnostic,
                anchorObservation = ObserveSupport(supplied), refusal, stepsClimbed = walker.StepsClimbed - beforeSteps,
                finalObservation = path.Count == 0 ? null : ObserveSupport(path[^1]), path = path.Select(P).ToArray(),
                interpretation = "A blocked straight segment does not establish that every route is blocked." };
        }

        var records = new List<object>();
        var previews = new List<EntranceMountPreview>();
        foreach (var (id, source) in sources)
        {
            registry.Addresses.TryGetValue(id,out var address);
            var buildingId=address?.BuildingId ?? (source.HasMeta("building_id")?source.GetMeta("building_id").AsString():null);
            var parcel=buildingId is not null&&registry.Buildings.TryGetValue(buildingId,out var registeredBuilding)
                ?registry.Parcels[registeredBuilding.ParcelId]:null;
            var access=parcel is not null?registry.AccessPoints[parcel.AccessId]:null;
            var leaves = Descendants(source).OfType<MeshInstance3D>().Where(m => m.Mesh is not null && m.IsVisibleInTree()).ToArray();
            var streetLeaf = leaves.SingleOrDefault(m => m.Name.ToString().Contains("StreetDoorClosed", StringComparison.Ordinal));
            var seniLeaf = leaves.SingleOrDefault(m => m.Name.ToString().Contains("SeniEntry_Door0_Leaf", StringComparison.Ordinal));
            var shedLeaf = leaves.SingleOrDefault(m => m.Name=="OutbuildingShed_Door_Panel_LOD0");
            var binding = manifest.RootElement.GetProperty("buildings").EnumerateArray()
                .SingleOrDefault(row => row.GetProperty("sourceName").GetString() == source.Name.ToString());
            var primaryName = binding.ValueKind==JsonValueKind.Object&&binding.TryGetProperty("primaryDoor", out var primaryProperty) ? primaryProperty.GetString() : null;
            var productionLeaf = standalone ? (buildingId is null?null:shedLeaf)
                : primaryName is null ? streetLeaf : leaves.Single(m => m.Name == primaryName);
            var physicalLeaves=new[]{streetLeaf,seniLeaf,shedLeaf}.Where(leaf=>leaf is not null).Cast<MeshInstance3D>().ToArray();
            if(physicalLeaves.Length==0)throw new InvalidOperationException("No actual authored door leaf on "+source.GetPath());
            var shedDoorParts=leaves.Where(m=>m.Name=="OutbuildingShed_Door_Panel_LOD0"
                ||m.Name.ToString().StartsWith("OutbuildingShed_Door_Plank_",StringComparison.Ordinal)
                ||m.Name=="OutbuildingShed_Door_Handle_LOD0").Select(m=>m.GetPath().ToString()).ToHashSet(StringComparer.Ordinal);
            var candidates = new List<object>();
            foreach (var leaf in physicalLeaves)
            {
                var label=leaf==shedLeaf?"existing-shed-door":leaf==seniLeaf?"existing-seni-door":"existing-street-door";
                var bounds = leaf.Mesh!.GetAabb();
                var centre = leaf.GlobalTransform * bounds.GetCenter();
                var outward = leaf.GlobalBasis * (bounds.Size.X < bounds.Size.Z ? Vector3.Right : Vector3.Back);
                outward.Y = 0; outward = outward.Normalized();
                var raw = Act1ConnectedWorld.AddressGround(centre + outward * 1.05f);
                var supported = walker.TrySupport(raw, out var feet);
                var supportDiagnostic = walker.LastSupportProbe;
                var supportRefusal = walker.LastRejection;
                var target = supported ? feet : raw;
                var direct = DirectPath(target, out var reached);
                var right = new Vector3(outward.Z, 0, -outward.X);
                var eye = target + Vector3.Up * localEye.Y + outward * localEye.Z + right * localEye.X;
                var inspectProxySource=standalone&&source.Name=="ReturnWestBanyaSilhouette";
                var view = Ray(eye, centre - outward * .08f, leaf,leaf==shedLeaf?shedDoorParts:null,inspectProxySource);
                var normalRay = Ray(centre + outward * .45f, centre - outward * .10f, leaf,leaf==shedLeaf?shedDoorParts:null,inspectProxySource);
                object? authoredVisibility=null;
                var passesKnownVisibleObstruction=true;
                if(inspectProxySource)
                {
                    // A physics ray can reach this leaf while the neighbouring
                    // H024 gable visibly covers it. Compare both published meshes
                    // with the same ray, independent of their collision policy.
                    var h024=Descendants(world).OfType<Node3D>().Single(n=>n.Name=="ReturnStreetDistantLowFacade");
                    var gable=Descendants(h024).OfType<MeshInstance3D>().Single(m=>m.Name=="DwellingFacade_Front_BoardedGable_LOD0");
                    beforeTransforms.TryAdd(gable,gable.GlobalTransform);
                    var end=centre-outward*.08f;
                    var gableEvidence=SourceTriangleComparison(eye,end,null,out var gableComplete,out var gableDistance,gable);
                    var leafEvidence=SourceTriangleComparison(eye,end,null,out var leafComplete,out var leafDistance,leaf);
                    var complete=gableComplete&&leafComplete;
                    var blocked=complete&&gable.IsVisibleInTree()&&gableDistance.HasValue&&leafDistance.HasValue
                        && gableDistance.Value<leafDistance.Value;
                    passesKnownVisibleObstruction=complete&&leaf.IsVisibleInTree()&&leafDistance.HasValue&&!blocked;
                    authoredVisibility=new{coverageComplete=complete,physicsLeafMatched=view.Matches,
                        knownObstruction=gable.GetPath().ToString(),knownVisibleObstructionBeforeLeaf=blocked,
                        gableDistance,leafDistance,gable=gableEvidence,leaf=leafEvidence,
                        scope="Exact H024 gable versus ReturnWest leaf; other visible surfaces require the real camera view.",
                        renderedAppearanceAccepted=false};
                    // Capture this one supported comparison even when physics
                    // matches the leaf. Never silently skip its photograph state
                    // assertions after a collider-only repair.
                    if(supported)blockedViews?.Add(new(source.Name.ToString(),leaf.GetPath().ToString(),feet,eye,centre,
                        view.Matches,blocked));
                }
                var canMount = AddressFacadeMount.TryFind(source, centre, outward, world, out var mount, out var mountedOutward, out var mountOwner, out var mountFailure);
                var coverage = canMount ? AddressFacadeMount.Inspect(source, mount, mountedOutward, world) : null;
                var worldCorners = (from x in new[] { 0, 1 } from y in new[] { 0, 1 } from z in new[] { 0, 1 }
                    select leaf.GlobalTransform * (bounds.Position + new Vector3(bounds.Size.X * x, bounds.Size.Y * y, bounds.Size.Z * z))).ToArray();
                candidates.Add(new { label, leaf = leaf.GetPath().ToString(), productionPrimary = leaf == productionLeaf,
                    approachDerivedFromOwnLeaf = true, centre = P(centre), outward = P(outward),
                    leafHeight = worldCorners.Max(p => p.Y) - worldCorners.Min(p => p.Y),
                    leafWidth = worldCorners.Max(p => p.Dot(right)) - worldCorners.Min(p => p.Dot(right)),
                    accessProposed = P(raw), support = new { supported, feet = supported ? P(feet) : null, supportRefusal,
                        supportDiagnostic, observations = ObserveSupport(raw) }, direct,
                    view = new { ray = view.Evidence, normalRay = normalRay.Evidence, cameraLocal = P(localEye),
                        physicsLeafMatched=view.Matches,authoredVisibility,
                        expectedDoorAssembly=leaf==shedLeaf?shedDoorParts.ToArray():new[]{leaf.GetPath().ToString()},
                        interactionRayLength = rayLength, eyeToLeaf = eye.DistanceTo(centre),
                        existingLeafVisibleAndInRange = supported && view.Matches && passesKnownVisibleObstruction && eye.DistanceTo(centre) <= rayLength },
                    mount = new { canMount, point = canMount ? P(mount) : null, outward = canMount ? P(mountedOutward) : null, mountOwner, mountFailure, coverage,
                        requiredForThisBuilding=shedLeaf is null,existingPlateMoved = false, plateWasCreated = false },
                    numberingProposal=standalone&&shedLeaf is null?StandaloneNumberingProposal(registry,target,manifest.RootElement):null,
                    comparisonConditionsSatisfied = supported && reached && view.Matches && passesKnownVisibleObstruction && eye.DistanceTo(centre) <= rayLength
                        && (shedLeaf is not null || canMount && coverage?.Supported == true),
                    interactionPerformed = false, accepted = false });
            }
            var alternateMounts = new List<object>();
            if(id=="ADR-H009")
            {
                var seniCentre=seniLeaf!.GlobalTransform*seniLeaf.Mesh!.GetAabb().GetCenter();
                foreach(var wallName in new[]{"DwellingFacade_SeniOuter_Wall_LOD0","DwellingFacade_Right_Wall_LOD0"})
                {
                    var wall=leaves.Single(m=>m.Name==wallName);
                    var wallCentre=wall.GlobalTransform*wall.Mesh!.GetAabb().GetCenter();
                    var outward=(wall.GlobalBasis*Vector3.Right).Normalized();
                    if(outward.Dot(wallCentre-source.GlobalPosition)<0)outward=-outward;
                    var found=AddressFacadeMount.TryFind(source,seniCentre,outward,world,out var at,out var mountedOutward,out var owner,out var reason,wallName);
                    var coverage=found?AddressFacadeMount.Inspect(source,at,mountedOutward,world,wallName):null;
                    var supported=found&&coverage?.Supported==true&&owner==wall.GetPath().ToString();
                    var focus=found?at:wallCentre;
                    var previewOutward=found?mountedOutward:outward;
                    var approachSupplied=Act1ConnectedWorld.AddressGround(focus+previewOutward*1.6f);
                    var approachSupported=walker.TrySupport(approachSupplied,out var approach);
                    var approachDiagnostic=walker.LastSupportProbe;
                    if(!approachSupported)approach=approachSupplied;
                    var path=DirectPath(approach,out var reached);
                    var nearest=graph.Nearest(Act1ConnectedWorld.AddressPoint(focus),filter:edge=>
                        edge.RoadId=="authored/main-axis"&&connected.Contains(edge.A)&&connected.Contains(edge.B));
                    var roadSupplied=nearest is null?Vector3.Zero:Act1ConnectedWorld.AddressVector(nearest.Value.Point);
                    var road=roadSupplied;
                    var roadSupported=nearest is not null&&walker.TrySupport(roadSupplied,out road);
                    var roadDiagnostic=walker.LastSupportProbe;
                    alternateMounts.Add(new{surface=wall.GetPath().ToString(),outward=P(previewOutward),found,supported,
                        at=found?P(at):null,owner,reason,coverage,maximumHeightAboveFacadeGround=3.0,
                        actualHeightAboveFacadeGround=found?(float?)(at.Y-Act1ConnectedWorld.AddressGround(at).Y):null,
                        approach=new{supported=approachSupported,point=P(approach),approachDiagnostic,path},
                        road=new{supported=roadSupported,point=P(road),roadDiagnostic,edge=nearest?.Edge,exactAnchor=nearest?.Point},
                        approachReachable=supported&&approachSupported&&reached,readingLineOfSightAndRangeVerified=false,
                        productionBindingChanged=false,accepted=false});
                    previews.Add(new(id,wallName,focus,previewOutward,supported,road,roadSupported,approach,approachSupported));
                }
            }
            records.Add(new { address, parcel, access, buildingId, source = source.GetPath().ToString(),
                logicalAnchor=source.GetMeta("logicalAnchor").AsString(),
                nonAddressableStandalone=shedLeaf is not null,technicalRegistrationPresent=buildingId is not null,
                productionPrimary = new { configuredLeafName = primaryName, leaf = productionLeaf?.GetPath().ToString(),
                    registryAccess = access?.Position, selectedBy = standalone?(productionLeaf is null?"not imported":"explicit standalone shed door panel")
                        :primaryName is null ? "existing importer StreetDoor preference" : "explicit primaryDoor binding" },
                aliases = registry.AddressAliases.Where(p => p.Value == id).ToArray(), candidates, alternateMounts });
        }
        var transformsUnchanged = beforeTransforms.All(p => p.Key.GlobalTransform == p.Value);
        if (!beforePose.Equals(player.CapturePortableTransform()) || beforeState != bridge.SelectRuntimeState().GetRawText()
            || beforeRegistry != RegistryState() || !transformsUnchanged)
            throw new InvalidOperationException("Read-only entrance comparison changed actor, runtime, registry or source transforms.");
        var json = JsonSerializer.Serialize(new { schemaVersion = 3, loadedModule = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId,
            scope = standalone?"standalone-candidates":"entrance-candidates", existingPhysicalEntranceComparison = true, auditRun = false, acceptance = false,
            primaryDoorUnchanged = true, geometryChanged = false, registryUnchanged = true, playerPoseAndStateUnchanged = true,
            sourceTransformsUnchanged = true, records }, new JsonSerializerOptions { WriteIndented = true });
        var temporary = destination + ".writing";
        using (var file = new FileStream(temporary, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(file)) { writer.Write(json); writer.Flush(); file.Flush(true); }
        File.Move(temporary, destination, false);
        GD.Print("address-entrance-candidates: recorded read-only physical comparisons for "+sources.Count+" sources: " + destination);
        return previews;
    }

    private static object StandaloneNumberingProposal(SettlementRegistry registry,Vector3 entrance,JsonElement authoredManifest)
    {
        var graph=registry.Graph;
        var nearest=graph.Nearest(Act1ConnectedWorld.AddressPoint(entrance),filter:e=>e.StreetId.Length>0);
        if(nearest is null)return new{assigned=false,reason="No existing street at the actual entrance."};
        var street=registry.Streets[nearest.Value.Edge.StreetId];
        var start=graph.Nearest(street.Origin,street.Id);
        if(start is null)return new{assigned=false,reason="Street origin has no imported segment."};
        var distances=new Dictionary<string,double>(StringComparer.Ordinal);
        var queue=new PriorityQueue<string,double>();
        foreach(var node in new[]{start.Value.Edge.A,start.Value.Edge.B})
        {
            var distance=graph.Nodes[node].Position.DistanceXZ(start.Value.Point);
            distances[node]=distance;queue.Enqueue(node,distance);
        }
        var streetEdges=graph.Edges.Values.Where(e=>e.StreetId==street.Id&&graph.Allowed(e,SettlementTravelMode.Foot,true)).ToArray();
        while(queue.TryDequeue(out var node,out var distance))
        {
            if(distance>distances[node])continue;
            foreach(var edge in streetEdges.Where(e=>e.A==node||e.B==node))
            {
                var other=edge.A==node?edge.B:edge.A;
                var next=distance+graph.Nodes[edge.A].Position.DistanceXZ(graph.Nodes[edge.B].Position);
                if(distances.TryGetValue(other,out var old)&&old<=next)continue;
                distances[other]=next;queue.Enqueue(other,next);
            }
        }
        (double Chainage,string Side) Measure(SettlementPoint point)
        {
            var projection=graph.Nearest(point,street.Id)!.Value;var edge=projection.Edge;
            var da=distances.GetValueOrDefault(edge.A,double.PositiveInfinity);
            var db=distances.GetValueOrDefault(edge.B,double.PositiveInfinity);
            var a=graph.Nodes[edge.A].Position;var b=graph.Nodes[edge.B].Position;
            var chainage=Math.Min(da+a.DistanceXZ(projection.Point),db+b.DistanceXZ(projection.Point));
            if(db<da)(a,b)=(b,a);
            var cross=(b.X-a.X)*(point.Z-projection.Point.Z)-(b.Z-a.Z)*(point.X-projection.Point.X);
            return(chainage,cross<0?"left":"right");
        }
        var measured=Measure(Act1ConnectedWorld.AddressPoint(entrance));
        if(!double.IsFinite(measured.Chainage))return new{assigned=false,reason="Street is disconnected from its fixed origin."};
        var odd=measured.Side==street.OddSide;
        var neighbours=registry.Addresses.Values.Where(a=>a.StreetId==street.Id)
            .Select(a=>new{address=a,station=Measure(registry.AccessPoints[a.AccessId].Position),
                numeric=int.Parse(new string(a.HouseNumber.TakeWhile(char.IsDigit).ToArray()),System.Globalization.CultureInfo.InvariantCulture)})
            .Where(n=>n.numeric%2==(odd?1:0)&&double.IsFinite(n.station.Chainage)).OrderBy(n=>n.station.Chainage).ToArray();
        var previous=neighbours.LastOrDefault(n=>n.station.Chainage<=measured.Chainage);
        var following=neighbours.FirstOrDefault(n=>n.station.Chainage>measured.Chainage);
        var reserved=authoredManifest.GetProperty("buildings").EnumerateArray().Where(row=>row.GetProperty("streetId").GetString()==street.Id)
            .Select(row=>row.GetProperty("number").GetString()!).ToArray();
        return new{assigned=false,street=street.Id,fixedOrigin=street.Origin,projectedOrigin=start.Value.Point,
            measured.Chainage,measured.Side,street.OddSide,parity=odd?"odd":"even",
            previous=previous is null?null:new{previous.address.AddressId,previous.address.HouseNumber,previous.station.Chainage},
            following=following is null?null:new{following.address.AddressId,following.address.HouseNumber,following.station.Chainage},
            reservedIncludingInactiveSources=reserved,
            proposedNumber=previous is null?null:registry.InfillNumber(street.Id,previous.address.HouseNumber),
            preservesEveryExistingAddress=true,usesActualEntrance=true,productionAssignmentPending=true};
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static object P(Vector3 value) => new { x = value.X, y = value.Y, z = value.Z };
}
