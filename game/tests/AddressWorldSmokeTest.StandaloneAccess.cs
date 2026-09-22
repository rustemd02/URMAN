using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

public partial class AddressWorldSmokeTest
{
    private bool _observeStandaloneMouse;
    private Vector2 _standaloneMouseExpected, _standaloneMouseDelivered;
    private int _standaloneMouseInput, _standaloneMouseUnhandled;
    private ulong _standaloneMouseProcess;

    public override void _Input(InputEvent inputEvent) => ObserveStandaloneMouse(inputEvent, false);
    public override void _UnhandledInput(InputEvent inputEvent) => ObserveStandaloneMouse(inputEvent, true);

    private void ObserveStandaloneMouse(InputEvent inputEvent, bool unhandled)
    {
        if (!_observeStandaloneMouse || inputEvent is not InputEventMouseMotion motion) return;
        if (!unhandled) _standaloneMouseDelivered += motion.ScreenRelative;
        if (!motion.ScreenRelative.IsEqualApprox(_standaloneMouseExpected)) return;
        if (unhandled) { _standaloneMouseUnhandled++; _standaloneMouseProcess = Engine.GetProcessFrames(); }
        else _standaloneMouseInput++;
        // Passive observation: FirstPersonController is the only input owner.
    }

    private async Task VerifyStandaloneShedAccess(AddressAccessVerifier audit)
    {
        var registry = _world.AddressRegistry!;
        var cases = new[]
        {
            (Name: "PerimeterWestStreetShed", Id: "CD709C39546561E0", Cadastre: "URM-Q01-P0202", Xz: new Vector2(-21.6f, -13.4f), Yaw: 84f),
            (Name: "ZiratVillageEdgeEastShed", Id: "3BE818BAD103E824", Cadastre: "URM-Q01-P0201", Xz: new Vector2(15.8f, -48.6f), Yaw: -90f),
            (Name: "ArrivalReverseEastDomesticShed", Id: "F0BEBEC3DE92DD30", Cadastre: "URM-Q01-P0203", Xz: new Vector2(18f, 30f), Yaw: -90f)
        };
        foreach (var sample in cases)
        {
            try
            {
                var source = Descendants(_world).OfType<Node3D>().Single(n => n.Name == sample.Name && n.IsVisibleInTree());
                var leaf = Descendants(source).OfType<MeshInstance3D>().Single(m => m.Name == "OutbuildingShed_Door_Panel_LOD0" && m.Mesh is not null);
                var id = "ACC-" + sample.Id;
                var building = registry.Buildings["BLD-" + sample.Id];
                var parcel = registry.Parcels["PAR-" + sample.Id];
                Require(building.AddressId is null && building.ParcelId == parcel.ParcelId
                    && registry.AccessPoints[id].BuildingId == building.BuildingId
                    && source.GetMeta("settlement_source_key").AsString() == Act1ConnectedWorld.StandaloneShedSourceKey(sample.Name),
                    sample.Name + ": original technical identity and null residential address are retained");
                Require(parcel.GameCadastralId == sample.Cadastre, sample.Name + ": original cadastral key is retained");
                var size = leaf.Mesh!.GetAabb().Size;
                var height = (leaf.GlobalBasis * Vector3.Up * size.Y).Length();
                var width = (leaf.GlobalBasis * Vector3.Right * size.X).Length();
                var sourcePose = source.GlobalTransform;
                var center = leaf.GlobalTransform * leaf.Mesh.GetAabb().GetCenter();
                // The authored central Plank_01 is a separate seam mesh. Aim at
                // the actual panel between seams while retaining its exact plane.
                var aimPoint = leaf.GlobalTransform * (leaf.Mesh.GetAabb().GetCenter() + Vector3.Right * size.X * .15f);
                var outward = leaf.GlobalBasis.Z.Normalized();
                Require(new Vector2(source.GlobalPosition.X, source.GlobalPosition.Z).DistanceTo(sample.Xz) < .001f
                    && Math.Abs(Mathf.Wrap(source.GlobalRotationDegrees.Y - sample.Yaw, -180, 180)) < .01f
                    && source.GlobalBasis.Scale.DistanceTo(Vector3.One * 1.15f) < .0001f,
                    sample.Name + ": the approved instance position, orientation and uniform scale are published");
                Require(Math.Abs(height - 1.932f) < .001f && Math.Abs(width - 1.334f) < .001f,
                    sample.Name + ": actual door geometry has the intended human dimensions");
                _checks.Add(new { kind = "standalone-source", sample.Name, building, parcel,
                    leaf = leaf.GetPath().ToString(), center = P(center), height, width, outward = P(outward),
                    sourcePosition = P(source.GlobalPosition), sourceBasis = B(source.GlobalBasis),
                    sourceBounds = StandaloneSourceBounds(source), registered = registry.AccessPoints[id],
                    ownershipAssignment = false, interiorOpeningClaim = false });
                if (sample.Name == "ZiratVillageEdgeEastShed") CheckStandaloneFenceJunction(source);
                await CaptureStandaloneShed(source, leaf, sample.Name);

                var advances = await ProbeSelectedAccess(audit, id, Time.GetTicksMsec());
                _checks.Add(new { kind = "standalone-search-completed", sample.Name, advances,
                    access = registry.AccessPoints[id], fullExistingSearch = true,
                    probe = audit.HasMeta("lastAccessProbe") ? audit.GetMeta("lastAccessProbe").AsString() : null });
                Require(registry.AccessPoints[id].State == "pending-graph-attachment",
                    sample.Name + ": full existing physical search committed a complete path");
                _world.AttachVerifiedAddressAccessPaths();
                var access = registry.AccessPoints[id];
                var road = registry.Graph.Roads["access/" + id];
                var origin = registry.Graph.NodeAt(registry.Graph.Roads["authored/main-axis"].Points[0])!;
                var graphRoute = registry.Graph.Route(origin, access.GraphNodeId, SettlementTravelMode.Foot);
                Require(access.State == "verified" && graphRoute.Count > 0
                    && road.Points[^1].DistanceXZ(access.Position) < .001,
                    sample.Name + ": the same physical path is attached to the shared reachable graph");
                var points = road.Points.Select(V).ToArray();
                _checks.Add(new { kind = "standalone-published-path", sample.Name, access, graphRoute,
                    points = points.Select(P).ToArray(), pointCount = points.Length,
                    routeShownToPlayer = false, localFixture = true, humanNavigationTest = false });

                using (var support = new AddressWalkProbe(_world))
                {
                    var supported = support.TrySupport(points[0], out var start);
                    _checks.Add(new { kind = "standalone-road-fixture-support", sample.Name, supported,
                        supplied = P(points[0]), start = P(start),
                        publishedHeightError = supported ? Math.Abs(points[0].Y - start.Y) : (float?)null,
                        support.LastRejection, support.LastSupportProbe });
                    Require(supported && _player.CanStandAt(start), sample.Name + ": local fixture begins on a real clear road support");
                    Require(Math.Abs(points[0].Y - start.Y) <= .002f,
                        sample.Name + ": published road anchor retains the verified physical support height before the fixture");
                    _player.ApplyZoneSpawn(start, 0);
                }
                await Frames(6);
                _checks.Add(new { kind = "standalone-road-fixture-settled", sample.Name,
                    position = P(_player.GlobalPosition), velocity = P(_player.Velocity),
                    onFloor = _player.IsOnFloor(), _player.IsCrouching, _player.ModalOpen,
                    _player.VehicleControlled, physicsProcessing = _player.IsPhysicsProcessing(),
                    canStand = _player.CanStandAt(_player.GlobalPosition),
                    introVisible = _demo?.IntroVisible, mainMenuVisible = _demo?.MainMenuVisible });
                Require(_player.IsOnFloor() && !_player.IsCrouching && _player.CanStandAt(_player.GlobalPosition),
                    sample.Name + ": local road fixture settles with the ordinary standing capsule");
                var props = _bridge.SelectWorldProps().GetRawText();
                var revision = _player.PresentationTransformRevision;
                var recoveries = _player.FallRecoveries;
                var clamps = _player.EdgeClamps;
                // This existing follower retains every published graph point and
                // controls heading explicitly; movement remains real W/physics.
                var arrival = await Act1FirstPersonWalkthroughSmokeTest.FollowMosqueRouteAsync(this, _player, _bridge,
                    points, "shed/" + sample.Name + "/approach", row => _checks.Add(row));
                Require(arrival.VisitedPoints == points.Length && arrival.StableLanding,
                    sample.Name + ": ordinary controller physically follows every committed point to the door approach");
                RecordStandaloneStanding(sample.Name + "/door", _player.GlobalPosition);
                await TurnStandaloneMouse(sample.Yaw, 0, sample.Name + "/face-door");
                await TurnStandaloneMouse(sample.Yaw - 45f, 0, sample.Name + "/look-left");
                await TurnStandaloneMouse(sample.Yaw + 45f, 0, sample.Name + "/look-right");
                await AimStandaloneLeaf(aimPoint, sample.Name);
                var ordinary = _player.GetNode<Camera3D>("Head/Camera3D");
                using (var query = PhysicsRayQueryParameters3D.Create(ordinary.GlobalPosition,
                    aimPoint + (aimPoint - ordinary.GlobalPosition).Normalized() * .30f, 3,
                    new global::Godot.Collections.Array<Rid> { _player.GetRid() }))
                {
                    var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
                    var owner = StandaloneRayOwner(hit);
                    var wall = Descendants(source).OfType<MeshInstance3D>().Single(m => m.Name == "OutbuildingShed_Wall_LOD0");
                    var body = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as CollisionObject3D;
                    var shape = body is null ? null : body.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
                    var physicalSource = shape is not null && shape.HasMeta("authoredSourceMesh")
                        ? shape.GetMeta("authoredSourceMesh").AsString() : null;
                    var pixel = ordinary.UnprojectPosition(aimPoint) / ordinary.GetViewport().GetVisibleRect().Size;
                    var visible = Act1VisibleSurfaceProbe.Nearest(_world, ordinary, pixel);
                    var direction = (aimPoint - ordinary.GlobalPosition).Normalized();
                    var planeDistance = (center - ordinary.GlobalPosition).Dot(outward) / direction.Dot(outward);
                    float? physicalDistance = hit.Count == 0 ? null : ordinary.GlobalPosition.DistanceTo(hit["position"].AsVector3());
                    _checks.Add(new { kind = "standalone-door-ray", sample.Name, eye = P(ordinary.GlobalPosition),
                        center = P(center), aimPoint = P(aimPoint), owner, hit = hit.Count > 0, physicalSource,
                        expectedPhysicalSource = wall.GetPath().ToString(), physicalDistance, leafPlaneDistance = planeDistance,
                        visibleMesh = visible?.Mesh.GetPath().ToString(), visibleDistance = visible?.Distance,
                        expectedVisibleMesh = leaf.GetPath().ToString(), pixel = pixel.ToString(),
                        interpretation = "nearest raw visible triangle and exact physical wall behind the existing closed leaf; rendered image reviewed separately" });
                    Require(physicalSource == wall.GetPath().ToString() && physicalDistance is { } distance
                        && distance >= planeDistance - .005f && distance <= planeDistance + .30f
                        && visible is { } first && first.Mesh == leaf,
                        sample.Name + ": nearest visible surface is the actual leaf and its exact physical wall is behind the leaf plane");
                }
                await Capture("15_" + sample.Name + "_reached_door");
                var detailPixels = sample.Name == "ZiratVillageEdgeEastShed"
                    ? new[] { new Vector2(.5f, .5f), new Vector2(.70f, .80f), new Vector2(.82f, .90f), new Vector2(.69f, .94f) }
                    : new[] { new Vector2(.5f, .5f) };
                Act1VisibleSurfaceProbe.Log(_world, ordinary, sample.Name + "/reached-visible-door", detailPixels);
                foreach (var detail in detailPixels.Skip(1))
                {
                    var screenPoint = detail * ordinary.GetViewport().GetVisibleRect().Size;
                    var from = ordinary.ProjectRayOrigin(screenPoint);
                    using var detailRay = PhysicsRayQueryParameters3D.Create(from,
                        from + ordinary.ProjectRayNormal(screenPoint) * 10f, 3,
                        new global::Godot.Collections.Array<Rid> { _player.GetRid() });
                    var visible = Act1VisibleSurfaceProbe.Nearest(_world, ordinary, detail);
                    _checks.Add(new { kind = "standalone-fence-pixel", sample.Name, pixel = detail.ToString(),
                        eye = P(ordinary.GlobalPosition), basis = B(ordinary.GlobalBasis),
                        visibleMesh = visible?.Mesh.GetPath().ToString(), visiblePoint = visible is { } first ? P(first.Point) : null,
                        visibleDistance = visible?.Distance,
                        physicalRay = StandaloneRayOwner(_world.GetWorld3D().DirectSpaceState.IntersectRay(detailRay)),
                        diagnosticOnly = true });
                }
                var returning = await Act1FirstPersonWalkthroughSmokeTest.FollowMosqueRouteAsync(this, _player, _bridge,
                    points.Reverse().ToArray(), "shed/" + sample.Name + "/return", row => _checks.Add(row));
                Require(returning.VisitedPoints == points.Length && returning.StableLanding
                    && revision == _player.PresentationTransformRevision && recoveries == _player.FallRecoveries
                    && clamps == _player.EdgeClamps && props == _bridge.SelectWorldProps().GetRawText()
                    && source.GlobalTransform.IsEqualApprox(sourcePose),
                    sample.Name + ": physical return preserves the source, control owner and all world props");
                _checks.Add(new { kind = "standalone-physical-result", sample.Name, passed = true,
                    arrival.WalkedMetres, returnMetres = returning.WalkedMetres, revision, recoveries, clamps,
                    ordinaryMouseAtDoor = true, routeHeadingFixture = true, noInteractionOrKnowledgeGranted = true });
            }
            catch (Exception error)
            {
                _failures.Add(sample.Name + ": " + error);
                GD.PrintErr("standalone-access failure: " + sample.Name + ": " + error);
            }
            finally
            {
                _observeStandaloneMouse = false;
                Input.ActionRelease("move_forward");
            }
        }
        _registryEvidence = JsonSerializer.SerializeToElement(new { subsetOnly = true,
            buildings = cases.Select(c => registry.Buildings["BLD-" + c.Id]).ToArray(),
            parcels = cases.Select(c => registry.Parcels["PAR-" + c.Id]).ToArray(),
            accesses = cases.Select(c => registry.AccessPoints["ACC-" + c.Id]).ToArray(),
            roads = registry.Graph.Roads.Values.Where(r => cases.Any(c => r.Id == "access/ACC-" + c.Id)).ToArray(),
            ordinaryAuditCompleted = _world.HasMeta("addressAccessAuditCompleted") && _world.GetMeta("addressAccessAuditCompleted").AsBool() });
        Require(!_world.HasMeta("addressAccessAuditCompleted") || !_world.GetMeta("addressAccessAuditCompleted").AsBool(),
            "three shed checks do not accept the complete settlement audit");
    }

    private readonly record struct ShedFencePoint(double X, double Y, double Z)
    {
        internal static ShedFencePoint From(Vector3 p) => new(p.X, p.Y, p.Z);
        internal ShedFencePoint Lerp(ShedFencePoint p, double t) => new(X + (p.X - X) * t, Y + (p.Y - Y) * t, Z + (p.Z - Z) * t);
        internal Vector3 Vector => new((float)X, (float)Y, (float)Z);
    }
    private readonly record struct ShedFencePlane(double X, double Y, double Z, double D)
    {
        internal double Distance(ShedFencePoint p) => X * p.X + Y * p.Y + Z * p.Z - D;
    }
    private sealed record ShedFenceSolid(Vector3[] Faces, ShedFencePlane[] Planes, bool Closed, bool Convex);
    private sealed record ShedFenceMeshData(Vector3[] Points, Vector3[] Normals, Plane[] Tangents, ulong Format,
        Vector3[] RawPoints, Vector3[] RawNormals, Plane[] RawTangents, int[] Indices);

    private void CheckStandaloneFenceJunction(Node3D shed)
    {
        var repair = _world.ZiratShedFenceJunction ?? throw new InvalidOperationException("Missing measured Zirat fence repair.");
        var before = _player.GlobalTransform; var props = _bridge.SelectWorldProps().GetRawText();
        var wall = ShedFenceSupport(repair.Wall); var footing = ShedFenceSupport(repair.Foundation);
        var recess = ShedFenceSupport(repair.Recess);
        var panelMesh = Descendants(shed).OfType<MeshInstance3D>().Single(m => m.Name == "OutbuildingShed_Door_Panel_LOD0");
        var panel = ShedFenceSupport(panelMesh);
        var records = new List<object>(); var capContacts = new List<object>();
        var allValid = repair.Shed == shed && wall.Closed && wall.Convex && footing.Closed && footing.Convex
            && recess.Closed && recess.Convex && panel.Closed && panel.Convex && repair.Rails.Length == 2 && repair.Posts.Length == 5;
        var footingOwners = Descendants(_world).OfType<CollisionShape3D>().Where(c => c.HasMeta("authoredSourceMesh")
            && c.GetMeta("authoredSourceMesh").AsString() == repair.Foundation.GetPath().ToString()).ToArray();
        var footingContactFaces = Array.Empty<Vector3>(); var footingContactError = 0f;
        var footingContactValid = footingOwners.Length == 1;
        var footingProxy = shed.GetNode<StaticBody3D>("AuthoredKitCollisionProxy");
        if (footingOwners.Length == 1 && footingOwners[0].Shape is ConcavePolygonShape3D footingShape)
        {
            var contact = footingOwners[0];
            var expected = repair.Foundation.Mesh!.GetFaces().Select(p => repair.Foundation.GlobalTransform * p).ToArray();
            footingContactFaces = footingShape.GetFaces().Select(p => contact.GlobalTransform * p).ToArray();
            footingContactValid &= !contact.Disabled && footingShape.BackfaceCollision && contact.GetParent() == footingProxy
                && footingProxy.CollisionLayer == 2 && expected.Length > 0 && footingContactFaces.Length == expected.Length;
            if (footingContactFaces.Length == expected.Length)
                for (var i = 0; i < expected.Length; i++) footingContactError = Math.Max(footingContactError, expected[i].DistanceTo(footingContactFaces[i]));
            footingContactValid &= footingContactError < .0002f;
        }
        else footingContactValid = false;
        allValid &= footingContactValid;
        foreach (var rail in repair.Rails)
        {
            var member = rail.Member; var mesh = member.Mesh;
            var original = ShedFenceArrays(member.Source); var result = ShedFenceArrays(mesh.Mesh!);
            // Republish the unchanged decoded source once through the same native
            // path as the repair. Oct16 directions need not survive another encode
            // bit-for-bit; the independent control, never the repaired output, is
            // the expected readback for retained source directions.
            using var controlMesh = new ArrayMesh();
            using var controlArrays = member.Source.SurfaceGetArrays(0);
            controlMesh.AddSurfaceFromArrays(member.Source.SurfaceGetPrimitiveType(0), controlArrays);
            controlMesh.SurfaceSetMaterial(0, member.Source.SurfaceGetMaterial(0));
            controlMesh.SurfaceSetName(0, member.Source.SurfaceGetName(0));
            var control = ShedFenceArrays(controlMesh);
            var sourceAfterControl = ShedFenceArrays(member.Source);
            var sourceIndices = original.Indices.Length == 0
                ? Enumerable.Range(0, original.RawPoints.Length).ToArray() : original.Indices;
            var controlPositionsSame = original.RawPoints.SequenceEqual(control.RawPoints) && original.Points.SequenceEqual(control.Points);
            var controlIndicesSame = original.Indices.SequenceEqual(control.Indices);
            var controlHandednessSame = original.RawTangents.Select(t => t.D).SequenceEqual(control.RawTangents.Select(t => t.D));
            var sourceUnchanged = original.Format == sourceAfterControl.Format && original.RawPoints.SequenceEqual(sourceAfterControl.RawPoints)
                && original.RawNormals.SequenceEqual(sourceAfterControl.RawNormals) && original.RawTangents.SequenceEqual(sourceAfterControl.RawTangents)
                && original.Indices.SequenceEqual(sourceAfterControl.Indices);
            var controlValid = controlPositionsSame && controlIndicesSame && controlHandednessSame && sourceUnchanged
                && original.Format == control.Format && control.RawNormals.Length == original.RawNormals.Length
                && control.RawTangents.Length == original.RawTangents.Length && controlMesh.SurfaceGetMaterial(0) == member.Source.SurfaceGetMaterial(0);
            var codecDrift = Enumerable.Range(0, original.RawPoints.Length).Select(index => new
            {
                sourceIndex = index,
                normalError = index < control.RawNormals.Length ? (float?)original.RawNormals[index].DistanceTo(control.RawNormals[index]) : null,
                tangentError = index < control.RawTangents.Length ? (float?)original.RawTangents[index].Normal.DistanceTo(control.RawTangents[index].Normal) : null,
                handednessSame = index < control.RawTangents.Length && original.RawTangents[index].D == control.RawTangents[index].D
            }).ToArray();
            var source = original.Points.Select(p => mesh.GlobalTransform * p).ToArray();
            var published = result.Points.Select(p => mesh.GlobalTransform * p).ToArray();
            var prefixPositionsSame = rail.PrefixVertices == 72 && original.Points.Take(72).SequenceEqual(result.Points.Take(72));
            var prefixDirectionsMatchControl = controlValid && original.Points.Length >= 72 && result.Points.Length >= 72
                && result.Normals.Length == result.Points.Length && result.Tangents.Length == result.Points.Length
                && Enumerable.Range(0, 72).All(i => result.Normals[i] == control.RawNormals[sourceIndices[i]]
                    && result.Tangents[i] == control.RawTangents[sourceIndices[i]]);
            var prefixHandednessSame = original.Tangents.Take(72).Select(t => t.D).SequenceEqual(result.Tangents.Take(72).Select(t => t.D));
            var prefixSame = prefixPositionsSame && prefixDirectionsMatchControl && prefixHandednessSame;
            var prefixBetweenActualPosts = Enumerable.Range(0, 2).All(span =>
            {
                var start = repair.Posts[span].OriginalTransform.Origin;
                var delta = repair.Posts[span + 1].OriginalTransform.Origin - start; delta.Y = 0;
                var along = delta.Normalized(); var length = delta.Length();
                return source.Skip(span * 36).Take(36).All(p =>
                    (p - start).Dot(along) >= -.00002f && (p - start).Dot(along) <= length + .00002f);
            });
            var farAttributeFailures = new List<object>();
            var farRetainedVertices = 0; var farInterpolatedVertices = 0;
            var maxFarNormalError = 0f; var maxFarTangentError = 0f;
            var sourceRecessArea = 0d; var recessArea = 0d; var panelArea = 0d; var unsupportedCaps = 0d;
            var outsideUnion = 0; var frontCaps = 0; var rearCaps = 0; var wrongCapWinding = 0; var offSource = 0;
            var parentAreas = new double[source.Length / 3];
            var capGroups = new[] { new List<Vector3>(), new List<Vector3>() };
            for (var i = 0; i < source.Length; i += 3)
                sourceRecessArea += ShedFenceArea(ShedFenceClip(source.Skip(i).Take(3).Select(ShedFencePoint.From).ToArray(), recess.Planes));
            for (var i = 0; i < published.Length; i += 3)
            {
                var triangle = published.Skip(i).Take(3).ToArray();
                var polygon = triangle.Select(ShedFencePoint.From).ToArray();
                recessArea += ShedFenceArea(ShedFenceClip(polygon, recess.Planes));
                panelArea += ShedFenceArea(ShedFenceClip(polygon, panel.Planes));
                if (!triangle.All(p => repair.FrontCut.DistanceTo(p) >= -.00002f)
                    && !triangle.All(p => repair.RearCut.DistanceTo(p) >= -.00002f)) outsideUnion++;
                var cap = triangle.All(p => Math.Abs(repair.FrontCut.DistanceTo(p)) < .00002f) ? 0
                    : triangle.All(p => Math.Abs(repair.RearCut.DistanceTo(p)) < .00002f) ? 1 : -1;
                if (cap >= 0)
                {
                    if (cap == 0) frontCaps++; else rearCaps++;
                    capGroups[cap].AddRange(triangle);
                    var outward = -(cap == 0 ? repair.FrontCut.Normal : repair.RearCut.Normal);
                    if ((triangle[1] - triangle[0]).Cross(triangle[2] - triangle[0]).Dot(outward) >= -1e-10f) wrongCapWinding++;
                    var inWall = ShedFenceClip(polygon, wall.Planes);
                    var inFooting = ShedFenceClip(polygon, footing.Planes);
                    var overlap = ShedFenceClip(inWall, footing.Planes);
                    unsupportedCaps += Math.Max(0, ShedFenceArea(polygon) - ShedFenceArea(inWall) - ShedFenceArea(inFooting) + ShedFenceArea(overlap));
                }
                else if (i >= rail.PrefixVertices + rail.LinkVertices)
                {
                    var parent = Enumerable.Range(0, source.Length / 3).FirstOrDefault(p => ShedFenceFragmentFits(triangle, source, p * 3), -1);
                    if (parent < 0) offSource++;
                    else
                    {
                        parentAreas[parent] += ShedFenceArea(polygon);
                        for (var vertex = 0; vertex < 3; vertex++)
                        {
                            var validAttributes = ShedFenceFragmentAttributes(original, control, result, parent * 3, i + vertex,
                                out var mode, out var originalIndex, out var normalError, out var tangentError, out var handednessSame);
                            if (mode == "retained-source-vertex") farRetainedVertices++;
                            if (mode == "interpolated-source-edge") farInterpolatedVertices++;
                            maxFarNormalError = Math.Max(maxFarNormalError, normalError);
                            maxFarTangentError = Math.Max(maxFarTangentError, tangentError);
                            if (!validAttributes) farAttributeFailures.Add(new { vertex = i + vertex, parent, mode, sourceIndex = originalIndex,
                                normalError, tangentError, handednessSame });
                        }
                    }
                }
            }
            var rear = repair.RearCut;
            var rearKeep = new ShedFencePlane(-rear.Normal.X, -rear.Normal.Y, -rear.Normal.Z, -rear.D);
            var maxParentAreaError = Enumerable.Range(0, parentAreas.Length).Max(i => Math.Abs(parentAreas[i]
                - ShedFenceArea(ShedFenceClip(source.Skip(i * 3).Take(3).Select(ShedFencePoint.From).ToArray(), new[] { rearKeep }))));
            var contactError = 0f;
            foreach (var contact in member.Contacts)
            {
                if (contact.Disabled || contact.Shape is not ConcavePolygonShape3D physical) { allValid = false; continue; }
                var actual = physical.GetFaces(); var cached = mesh.Mesh!.GetFaces();
                if (actual.Length != cached.Length) { allValid = false; continue; }
                for (var i = 0; i < actual.Length; i++) contactError = Math.Max(contactError,
                    (contact.GlobalTransform * actual[i]).DistanceTo(mesh.GlobalTransform * cached[i]));
            }
            for (var cap = 0; cap < 2; cap++)
            {
                var vertices = capGroups[cap].Distinct().ToArray();
                if (vertices.Length == 0) { allValid = false; continue; }
                var center = vertices.Aggregate(Vector3.Zero, (sum, p) => sum + p) / vertices.Length;
                var outward = cap == 0 ? repair.FrontCut.Normal : repair.RearCut.Normal;
                var rayStart = center + outward * .12f; var rayEnd = center - outward * .05f;
                var startWallDistances = wall.Planes.Select(p => p.Distance(ShedFencePoint.From(rayStart))).ToArray();
                var startFootingDistances = footing.Planes.Select(p => p.Distance(ShedFencePoint.From(rayStart))).ToArray();
                var startInsideWall = startWallDistances.All(d => d <= .000002);
                var startInsideFooting = startFootingDistances.All(d => d <= .000002);
                using var ray = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd, 3,
                    new global::Godot.Collections.Array<Rid> { _player.GetRid() });
                var hit = _world.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                var body = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as CollisionObject3D;
                var shape = body?.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
                var owner = shape is not null && shape.HasMeta("authoredSourceMesh") ? shape.GetMeta("authoredSourceMesh").AsString() : null;
                var physicalSupport = owner == repair.Wall.GetPath().ToString() || owner == repair.Foundation.GetPath().ToString();
                capContacts.Add(new { mesh = mesh.GetPath().ToString(), cap, center = P(center), physicalSupport,
                    rayStart = P(rayStart), rayEnd = P(rayEnd), startWallDistances, startFootingDistances,
                    startInsideWall, startInsideFooting, ray.HitFromInside, hit = StandaloneRayOwner(hit) });
                allValid &= physicalSupport && !startInsideWall && !startInsideFooting && !ray.HitFromInside;
            }
            var materialSame = mesh.Mesh!.SurfaceGetMaterial(0) == member.Source.SurfaceGetMaterial(0)
                && mesh.MaterialOverride == rail.OriginalOverride;
            var valid = controlValid && prefixSame && prefixBetweenActualPosts && farAttributeFailures.Count == 0 && farRetainedVertices > 0
                && mesh.GlobalTransform == member.OriginalTransform && mesh.Visible == member.OriginalVisible
                && sourceRecessArea > .00001 && recessArea < .0000002 && panelArea < .0000002 && outsideUnion == 0
                && frontCaps == rail.FrontCaps && rearCaps == rail.RearCaps && frontCaps > 0 && rearCaps > 0
                && wrongCapWinding == 0 && unsupportedCaps < .0000002 && offSource == 0 && maxParentAreaError < .00002
                && contactError < .0002f && materialSame && result.Points.Length == rail.PrefixVertices + rail.LinkVertices + rail.FarVertices
                && result.Normals.Length == result.Points.Length && original.Format == result.Format;
            allValid &= valid;
            records.Add(new { owner = mesh.GetPath().ToString(), valid, prefixSame, prefixPositionsSame, prefixDirectionsMatchControl,
                prefixHandednessSame, prefixBetweenActualPosts,
                publicationControl = new { valid = controlValid, detached = true, originalArraysUnmodified = sourceUnchanged,
                    path = "AddSurfaceFromArrays(original primitive, original arrays); default flags; one readback",
                    controlPositionsSame, controlIndicesSame, controlHandednessSame, format = control.Format,
                    rawVertices = control.RawPoints.Select(P).ToArray(), rawNormals = control.RawNormals.Select(P).ToArray(),
                    rawTangents = control.RawTangents.Select(t => new[] { t.Normal.X, t.Normal.Y, t.Normal.Z, t.D }).ToArray(),
                    indices = control.Indices, expandedSourceIndices = sourceIndices, codecDrift,
                    changedNormals = codecDrift.Count(d => d.normalError > 0), changedTangents = codecDrift.Count(d => d.tangentError > 0),
                    maximumNormalError = codecDrift.Max(d => d.normalError), maximumTangentError = codecDrift.Max(d => d.tangentError) },
                farRetainedVertices, farInterpolatedVertices, maxFarNormalError, maxFarTangentError, farAttributeFailures,
                sourceRecessArea, recessArea, panelArea,
                outsideUnion, frontCaps, rearCaps, wrongCapWinding, unsupportedCaps, offSource, maxParentAreaError, contactError, materialSame,
                rail.PrefixVertices, rail.LinkVertices, rail.FarVertices, sourceFormat = original.Format, publishedFormat = result.Format,
                original = source.Select(P).ToArray(), published = published.Select(P).ToArray(),
                sourceNormals = original.Normals.Select(P).ToArray(), publishedNormals = result.Normals.Select(P).ToArray(),
                sourceTangents = original.Tangents.Select(t => new[] { t.Normal.X, t.Normal.Y, t.Normal.Z, t.D }).ToArray(),
                publishedTangents = result.Tangents.Select(t => new[] { t.Normal.X, t.Normal.Y, t.Normal.Z, t.D }).ToArray(),
                sourceRawVertices = original.RawPoints.Select(P).ToArray(), sourceRawNormals = original.RawNormals.Select(P).ToArray(),
                sourceRawTangents = original.RawTangents.Select(t => new[] { t.Normal.X, t.Normal.Y, t.Normal.Z, t.D }).ToArray(),
                sourceIndices = original.Indices, publishedRawVertexCount = result.RawPoints.Length, publishedIndices = result.Indices,
                contacts = member.Contacts.Select(c => c.GetPath().ToString()).ToArray() });
        }
        var posts = repair.Posts.Select((post, index) =>
        {
            var points = ShedFenceArrays(post.Source).Points.Select(p => post.Mesh.GlobalTransform * p).ToArray();
            // Soil anchors extend below the wall: test the real side planes of
            // the occupied footprint, without declaring the soil a wall surface.
            var insideFootprint = points.All(p => wall.Planes.Where(q => Math.Abs(q.Y) < .2).All(q => q.Distance(ShedFencePoint.From(p)) <= .000002));
            var valid = post.Mesh.Mesh == post.Source && post.Mesh.GlobalTransform == post.OriginalTransform
                && post.Mesh.MaterialOverride == post.OriginalOverride
                && (index == 3 ? !post.Mesh.Visible && insideFootprint && post.Contacts.Length == 0 : post.Mesh.Visible == post.OriginalVisible);
            allValid &= valid;
            return new { name = post.Mesh.Name.ToString(), sourceMeshType = post.Source.GetClass().ToString(),
                valid, insideFootprint, post.Mesh.Visible, post.Action, materialSame = post.Mesh.MaterialOverride == post.OriginalOverride,
                original = points.Select(P).ToArray(), contacts = post.Contacts.Select(c => c.GetPath().ToString()).ToArray() };
        }).ToArray();
        var evidence = new { kind = "standalone-fence-junction", support = new { wall = repair.Wall.GetPath().ToString(), footing = repair.Foundation.GetPath().ToString(),
            wall.Closed, wall.Convex, footingClosed = footing.Closed, footingConvex = footing.Convex,
            wallFaces = wall.Faces.Select(P).ToArray(), footingFaces = footing.Faces.Select(P).ToArray(), recessFaces = recess.Faces.Select(P).ToArray() },
            footingContact = new { valid = footingContactValid, count = footingOwners.Length, error = footingContactError,
                owners = footingOwners.Select(c => c.GetPath().ToString()).ToArray(), physicalFaces = footingContactFaces.Select(P).ToArray() },
            records, posts, capContacts, allValid, scope = "actual published mesh and exact physical support; route and unchanged camera pixels follow" };
        var json = JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true });
        using (var file = new FileStream(Path.Combine(_output, "zirat-shed-fence-junction.json"), FileMode.CreateNew, System.IO.FileAccess.Write))
        using (var writer = new StreamWriter(file)) writer.Write(json);
        _checks.Add(new { kind = "standalone-fence-junction", allValid, rails = records.Count, posts, capContacts });
        Require(allValid, "Zirat: original external rails/posts survive; the door recess is clear and closed cut ends meet real wall/footing supports");
        Require(_player.GlobalTransform == before && _bridge.SelectWorldProps().GetRawText() == props,
            "Zirat: fence geometry inspection changes no player pose or persistent state");
    }

    private static ShedFenceMeshData ShedFenceArrays(Mesh mesh)
    {
        var faces = new List<Vector3>(); var allNormals = new List<Vector3>(); var allTangents = new List<Plane>();
        var rawPoints = new List<Vector3>(); var rawNormals = new List<Vector3>(); var rawTangents = new List<Plane>();
        var rawIndices = new List<int>();
        for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            var a = mesh.SurfaceGetArrays(surface); var points = a[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals = a[(int)Mesh.ArrayType.Normal].AsVector3Array(); var v = a[(int)Mesh.ArrayType.Index];
            var indices = v.VariantType == Variant.Type.Nil ? Array.Empty<int>() : v.AsInt32Array();
            var tangentValue = a[(int)Mesh.ArrayType.Tangent];
            var tangentValues = tangentValue.VariantType == Variant.Type.Nil ? Array.Empty<float>() : tangentValue.AsFloat32Array();
            if (normals.Length != points.Length) throw new InvalidOperationException("Incomplete authored shed/fence normals.");
            if (tangentValues.Length != 0 && tangentValues.Length != points.Length * 4)
                throw new InvalidOperationException("Incomplete published shed/fence tangent float4 channel.");
            if (indices.Any(i => i < 0 || i >= points.Length)) throw new InvalidOperationException("Invalid published shed/fence index.");
            var tangents = Enumerable.Range(0, tangentValues.Length / 4).Select(i => new Plane(
                new Vector3(tangentValues[i * 4], tangentValues[i * 4 + 1], tangentValues[i * 4 + 2]), tangentValues[i * 4 + 3])).ToArray();
            faces.AddRange(indices.Length == 0 ? points : indices.Select(i => points[i]));
            allNormals.AddRange(indices.Length == 0 ? normals : indices.Select(i => normals[i]));
            if (tangents.Length != 0) allTangents.AddRange(indices.Length == 0 ? tangents : indices.Select(i => tangents[i]));
            rawPoints.AddRange(points); rawNormals.AddRange(normals); rawTangents.AddRange(tangents); rawIndices.AddRange(indices);
        }
        using var rawSurface = RenderingServer.MeshGetSurface(mesh.GetRid(), 0);
        if (!rawSurface.TryGetValue("format", out var format) || format.VariantType != Variant.Type.Int)
            throw new InvalidOperationException("Missing actual shed/fence surface format.");
        return new(faces.ToArray(), allNormals.ToArray(), allTangents.ToArray(), (ulong)format.AsInt64(),
            rawPoints.ToArray(), rawNormals.ToArray(), rawTangents.ToArray(), rawIndices.ToArray());
    }

    private static bool ShedFenceFragmentAttributes(ShedFenceMeshData source, ShedFenceMeshData control, ShedFenceMeshData result, int parent, int vertex,
        out string mode, out int sourceIndex, out float normalError, out float tangentError, out bool handednessSame)
    {
        mode = "no-source-edge"; sourceIndex = -1; normalError = 0; tangentError = 0; handednessSame = false;
        if (source.Tangents.Length != source.Points.Length || result.Tangents.Length != result.Points.Length) return false;
        var point = result.Points[vertex];
        for (var corner = 0; corner < 3; corner++)
        {
            var index = parent + corner;
            if (point != source.Points[index]) continue;
            mode = "retained-source-vertex";
            sourceIndex = source.Indices.Length == 0 ? index : source.Indices[index];
            if (sourceIndex >= control.RawNormals.Length || sourceIndex >= control.RawTangents.Length) return false;
            normalError = result.Normals[vertex].DistanceTo(control.RawNormals[sourceIndex]);
            tangentError = result.Tangents[vertex].Normal.DistanceTo(control.RawTangents[sourceIndex].Normal);
            handednessSame = result.Tangents[vertex].D == source.Tangents[index].D
                && result.Tangents[vertex].D == control.RawTangents[sourceIndex].D;
            return normalError == 0 && tangentError == 0 && handednessSame;
        }
        for (var edge = 0; edge < 3; edge++)
        {
            var a = parent + edge; var b = parent + (edge + 1) % 3;
            var delta = source.Points[b] - source.Points[a];
            var t = (point - source.Points[a]).Dot(delta) / delta.LengthSquared();
            if (t < -.0001f || t > 1.0001f || point.DistanceTo(source.Points[a].Lerp(source.Points[b], t)) > .00003f) continue;
            mode = "interpolated-source-edge";
            var normal = source.Normals[a].Lerp(source.Normals[b], t).Normalized();
            var tangent = source.Tangents[a].Normal.Lerp(source.Tangents[b].Normal, t);
            tangent = (tangent - normal * tangent.Dot(normal)).Normalized();
            normalError = normal.DistanceTo(result.Normals[vertex]);
            tangentError = tangent.DistanceTo(result.Tangents[vertex].Normal);
            handednessSame = result.Tangents[vertex].D == (t < .5f ? source.Tangents[a].D : source.Tangents[b].D);
            // New interpolated directions have an allowance for the renderer's
            // 16-bit octahedral codec; retained source directions above require
            // exact equality with the independently republished original control.
            return normalError < .0002f && tangentError < .0002f && handednessSame;
        }
        return false;
    }
    private static ShedFenceSolid ShedFenceSupport(MeshInstance3D mesh)
    {
        var faces = ShedFenceArrays(mesh.Mesh!).Points.Select(p => mesh.GlobalTransform * p).ToArray();
        var center = faces.Aggregate(Vector3.Zero, (sum, p) => sum + p) / faces.Length;
        var planes = new List<ShedFencePlane>(); var edges = new Dictionary<(string, string), int>();
        string Key(Vector3 p) => $"{BitConverter.SingleToInt32Bits(p.X)},{BitConverter.SingleToInt32Bits(p.Y)},{BitConverter.SingleToInt32Bits(p.Z)}";
        for (var i = 0; i < faces.Length; i += 3)
        {
            var a = ShedFencePoint.From(faces[i]); var b = ShedFencePoint.From(faces[i + 1]); var c = ShedFencePoint.From(faces[i + 2]);
            var nx = (b.Y - a.Y) * (c.Z - a.Z) - (b.Z - a.Z) * (c.Y - a.Y);
            var ny = (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z);
            var nz = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length < 1e-12) throw new InvalidOperationException("Degenerate authored shed support face.");
            nx /= length; ny /= length; nz /= length;
            if (nx * (center.X - a.X) + ny * (center.Y - a.Y) + nz * (center.Z - a.Z) > 0) { nx = -nx; ny = -ny; nz = -nz; }
            planes.Add(new(nx, ny, nz, nx * a.X + ny * a.Y + nz * a.Z));
            for (var e = 0; e < 3; e++)
            {
                var left = Key(faces[i + e]); var right = Key(faces[i + (e + 1) % 3]);
                var edge = StringComparer.Ordinal.Compare(left, right) < 0 ? (left, right) : (right, left);
                edges[edge] = edges.GetValueOrDefault(edge) + 1;
            }
        }
        return new(faces, planes.ToArray(), edges.Count > 0 && edges.Values.All(n => n == 2),
            planes.All(plane => faces.All(p => plane.Distance(ShedFencePoint.From(p)) <= .000002)));
    }
    private static ShedFencePoint[] ShedFenceClip(ShedFencePoint[] input, IEnumerable<ShedFencePlane> planes)
    {
        var polygon = input;
        foreach (var plane in planes)
        {
            var next = new List<ShedFencePoint>();
            for (var i = 0; i < polygon.Length; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length]; var da = plane.Distance(a); var db = plane.Distance(b);
                if (da <= 0) next.Add(a);
                if ((da <= 0) != (db <= 0)) next.Add(a.Lerp(b, da / (da - db)));
            }
            polygon = next.ToArray(); if (polygon.Length == 0) break;
        }
        return polygon;
    }
    private static double ShedFenceArea(ShedFencePoint[] polygon)
    {
        double area = 0;
        for (var i = 1; i + 1 < polygon.Length; i++)
        {
            var a = polygon[0]; var b = polygon[i]; var c = polygon[i + 1];
            var nx = (b.Y - a.Y) * (c.Z - a.Z) - (b.Z - a.Z) * (c.Y - a.Y);
            var ny = (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z);
            var nz = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            area += Math.Sqrt(nx * nx + ny * ny + nz * nz) * .5;
        }
        return area;
    }
    private static bool ShedFenceFragmentFits(Vector3[] triangle, Vector3[] source, int start)
    {
        var a = source[start]; var ab = source[start + 1] - a; var ac = source[start + 2] - a;
        var normal = ab.Cross(ac).Normalized();
        if ((triangle[1] - triangle[0]).Cross(triangle[2] - triangle[0]).Dot(normal) <= 0) return false;
        var drop = Math.Abs(normal.X) >= Math.Abs(normal.Y) && Math.Abs(normal.X) >= Math.Abs(normal.Z) ? 0
            : Math.Abs(normal.Y) >= Math.Abs(normal.Z) ? 1 : 2;
        (double X, double Y) Project(Vector3 p) => drop == 0 ? (p.Y, p.Z) : drop == 1 ? (p.X, p.Z) : (p.X, p.Y);
        var pa = Project(a); var pb = Project(source[start + 1]); var pc = Project(source[start + 2]);
        var denominator = (pb.X - pa.X) * (pc.Y - pa.Y) - (pb.Y - pa.Y) * (pc.X - pa.X);
        if (Math.Abs(denominator) < 1e-12) return false;
        foreach (var point in triangle)
        {
            if (Math.Abs(normal.Dot(point - a)) > .00003f) return false;
            var p = Project(point);
            var u = ((p.X - pa.X) * (pc.Y - pa.Y) - (p.Y - pa.Y) * (pc.X - pa.X)) / denominator;
            var v = ((pb.X - pa.X) * (p.Y - pa.Y) - (pb.Y - pa.Y) * (p.X - pa.X)) / denominator;
            if (u < -.0001 || v < -.0001 || u + v > 1.0001) return false;
        }
        return true;
    }

    private object StandaloneSourceBounds(Node3D source)
    {
        var points = Descendants(source).OfType<MeshInstance3D>().Where(m => m.Mesh is not null && m.IsVisibleInTree())
            .SelectMany(m => Enumerable.Range(0, 8).Select(i => m.GlobalTransform * m.Mesh!.GetAabb().GetEndpoint(i))).ToArray();
        return new { min = P(new(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z))),
            max = P(new(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z))) };
    }

    private async Task CaptureStandaloneShed(Node3D source, MeshInstance3D leaf, string name)
    {
        var camera = new Camera3D { Name = "DiagnosticStandaloneShedCamera", Fov = 68, Near = .06f, Far = 280 };
        AddChild(camera);
        var ordinary = _player.GetNode<Camera3D>("Head/Camera3D");
        try
        {
            var center = leaf.GlobalTransform * leaf.Mesh!.GetAabb().GetCenter();
            // Keep these three local views in the known gaps beside the doors.
            // The former 7.5 m offset put West/Arrival inside neighbouring houses.
            var side = name == "ArrivalReverseEastDomesticShed" ? 2f : -1.2f;
            camera.GlobalPosition = center + leaf.GlobalBasis.Z.Normalized() * 3.1f
                + leaf.GlobalBasis.X.Normalized() * side + Vector3.Up * 1.4f;
            camera.LookAt(source.GlobalPosition + Vector3.Up * 1.75f, Vector3.Up);
            camera.MakeCurrent(); await Frames(3);
            await Capture("14_" + name + "_door_support_neighbours");
            var aim = leaf.GlobalTransform * (leaf.Mesh.GetAabb().GetCenter()
                + Vector3.Right * leaf.Mesh.GetAabb().Size.X * .15f);
            var pixel = camera.UnprojectPosition(aim) / camera.GetViewport().GetVisibleRect().Size;
            var firstVisible = Act1VisibleSurfaceProbe.Nearest(_world, camera, pixel);
            using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition,
                aim + (aim - camera.GlobalPosition).Normalized() * .30f, 3,
                new global::Godot.Collections.Array<Rid> { _player.GetRid() });
            _captures.Add(new { file = "14_" + name + "_door_support_neighbours.png", source = source.GetPath().ToString(),
                eye = P(camera.GlobalPosition), basis = B(camera.GlobalBasis), diagnostic = true,
                leafPixel = pixel.ToString(), visibleMesh = firstVisible?.Mesh.GetPath().ToString(),
                expectedVisibleMesh = leaf.GetPath().ToString(),
                physicalRay = StandaloneRayOwner(_world.GetWorld3D().DirectSpaceState.IntersectRay(ray)),
                intendedCoverage = "complete shed, door, foundation and adjacent parcels; actual image must be reviewed" });
            Act1VisibleSurfaceProbe.Log(_world, camera, name + "/overview-owner", pixel, new Vector2(.5f, .8f));
            Require(pixel.X > 0 && pixel.X < 1 && pixel.Y > 0 && pixel.Y < 1
                && firstVisible is { } visible && visible.Mesh == leaf,
                name + ": overview sees the actual door without an intervening mesh");
        }
        finally { ordinary.MakeCurrent(); camera.QueueFree(); await Frames(1); }
    }

    private static object StandaloneRayOwner(global::Godot.Collections.Dictionary hit)
    {
        var body = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as CollisionObject3D;
        var index = hit.Count == 0 ? -1 : hit["shape"].AsInt32();
        var shape = body is null ? null : body.ShapeOwnerGetOwner(body.ShapeFindOwner(index)) as Node;
        return new { collider = body?.GetPath().ToString(), shapeIndex = index, shapeOwner = shape?.GetPath().ToString(),
            source = shape is not null && shape.HasMeta("authoredSourceMesh") ? shape.GetMeta("authoredSourceMesh").AsString() : null,
            point = hit.Count == 0 ? null : P(hit["position"].AsVector3()),
            normal = hit.Count == 0 ? null : P(hit["normal"].AsVector3()) };
    }

    private void RecordStandaloneStanding(string label, Vector3 feet)
    {
        using var capsule = new CapsuleShape3D { Radius = _player.BodyRadius, Height = _player.StandingBodyHeight - .015f };
        using var standing = new PhysicsShapeQueryParameters3D { Shape = capsule, Margin = .002f,
            CollisionMask = _player.CollisionMask, Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() },
            Transform = new(Basis.Identity, feet + Vector3.Up * (_player.StandingBodyHeight * .5f + .01f)) };
        var contacts = _player.GetWorld3D().DirectSpaceState.IntersectShape(standing, 32).Select(h =>
        {
            var body = h["collider"].AsGodotObject() as CollisionObject3D;
            var index = h["shape"].AsInt32();
            var shape = body?.ShapeOwnerGetOwner(body.ShapeFindOwner(index)) as Node;
            return new { collider = body?.GetPath().ToString(), shapeIndex = index, shapeOwner = shape?.GetPath().ToString(),
                source = shape is not null && shape.HasMeta("authoredSourceMesh") ? shape.GetMeta("authoredSourceMesh").AsString() : null };
        }).ToArray();
        using var ray = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * .3f, feet - Vector3.Up * .5f, 3,
            new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        _checks.Add(new { kind = "standalone-standing", label, feet = P(feet), contacts, saturated = contacts.Length == 32,
            canStand = _player.CanStandAt(feet), grounded = _player.IsOnFloor(),
            support = StandaloneRayOwner(_player.GetWorld3D().DirectSpaceState.IntersectRay(ray)) });
        Require(contacts.Length == 0 && _player.CanStandAt(feet) && _player.IsOnFloor(), label + ": actual standing capsule and support remain valid");
    }

    private async Task AimStandaloneLeaf(Vector3 point, string label)
    {
        var camera = _player.GetNode<Camera3D>("Head/Camera3D");
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var delta = point - camera.GlobalPosition;
            var yaw = Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z));
            var pitch = Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length()));
            await TurnStandaloneMouse(yaw, pitch, label + "/aim-" + attempt);
            if ((-camera.GlobalBasis.Z).Dot((point - camera.GlobalPosition).Normalized()) > .9999f) break;
        }
        Require((-camera.GlobalBasis.Z).Dot((point - camera.GlobalPosition).Normalized()) > .9999f,
            label + ": ordinary mouse aiming converges on the actual door");
    }

    private async Task TurnStandaloneMouse(float yaw, float pitch, string label)
    {
        var before = _player.CapturePortableTransform();
        var yawDelta = Mathf.Wrap(yaw - _player.RotationDegrees.Y, -180, 180);
        var pitchDelta = pitch - (float)before.RotationDegrees.X;
        if (Math.Abs(yawDelta) < .0001f && Math.Abs(pitchDelta) < .0001f) return;
        _standaloneMouseExpected = new(-yawDelta / _player.MouseSensitivity, -pitchDelta / _player.MouseSensitivity);
        _standaloneMouseDelivered = Vector2.Zero; _standaloneMouseInput = 0; _standaloneMouseUnhandled = 0;
        _standaloneMouseProcess = 0; _observeStandaloneMouse = true;
        var started = Time.GetTicksMsec(); var frames = 0; var ready = false;
        try
        {
            var center = (Vector2)DisplayServer.WindowGetSize() * .5f;
            using var motion = new InputEventMouseMotion { Position = center, GlobalPosition = center,
                Relative = _standaloneMouseExpected, ScreenRelative = _standaloneMouseExpected };
            Input.ParseInputEvent(motion);
            do
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); frames++;
                ready = _standaloneMouseInput == 1 && _standaloneMouseUnhandled == 1
                    && Engine.GetProcessFrames() > _standaloneMouseProcess
                    && Math.Abs(Mathf.Wrap(_player.RotationDegrees.Y - yaw, -180, 180)) < .15f
                    && Math.Abs((float)_player.CapturePortableTransform().RotationDegrees.X - pitch) < .15f;
            } while (!ready && frames < 180 && Time.GetTicksMsec() - started < 2000);
            ready &= _standaloneMouseDelivered.IsEqualApprox(_standaloneMouseExpected);
        }
        finally
        {
            _observeStandaloneMouse = false;
            _checks.Add(new { kind = "standalone-ordinary-mouse", label, ready, yaw, pitch,
                expected = _standaloneMouseExpected.ToString(), delivered = _standaloneMouseDelivered.ToString(),
                inputCount = _standaloneMouseInput, unhandledCount = _standaloneMouseUnhandled,
                before, after = _player.CapturePortableTransform(), frames, milliseconds = Time.GetTicksMsec() - started });
        }
        Require(ready && !_player.ModalOpen && _player.CanStandAt(_player.GlobalPosition),
            label + ": one delivered ordinary mouse event turns a clear standing player");
    }
}
