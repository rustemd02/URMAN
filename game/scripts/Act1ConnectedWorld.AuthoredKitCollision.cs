using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    /// <summary>
    /// Authored kit components arrive presentation-only, so walls, fences and
    /// outbuildings used to be walk-through. Pierced walls use their actual
    /// triangle surfaces, including reveals. Separate solid members keep their
    /// own contact shape; no interaction carves a hole in a visible wall.
    /// </summary>
    private void BuildAuthoredKitBlockers()
    {
        if (_zoneInstances.GetValueOrDefault("village_day") is StyleBenchmarkZone village)
            village.RetireBenchmarkHouseAssemblies();
        GroundZiratRoadsideContacts();
        BuildAuthoredWellContacts();
        var placements = 0;
        var blocked = 0;
        var skippedHidden = 0;
        var skippedRoadClear = 0;
        var skippedFlatSlab = 0;
        var exactArchitecture = 0;
        var carved = 0;
        var existingSourceContacts = FindDescendants<CollisionShape3D>(this)
            .Where(shape => shape.HasMeta("authoredSourceMesh"))
            .Select(shape => shape.GetMeta("authoredSourceMesh").AsString()).ToHashSet(StringComparer.Ordinal);
        // Authored doors that carry an interaction target must stay reachable: a
        // wall box around a doorway would seal the entrance the route uses. The
        // carve targets are the live interaction approaches themselves.
        var doorTargets = FindDescendants<InteractionTarget>(this)
            .Where(target => target.IsInsideTree())
            .Select(target => target.GlobalPosition)
            .ToArray();
        foreach (var placement in FindDescendants<Node3D>(this)
            .Where(node => HasTrueMeta(node, "authoredKitBlockerCandidate"))
            .ToArray())
        {
            if (!placement.IsVisibleInTree())
            {
                skippedHidden++;
                placement.SetMeta("authoredKitBlockerSkipReason", "presentation suppressed before blockers were built");
                continue;
            }

            // Created lazily: a StaticBody3D that never enters the tree is a leaked
            // physics body at exit, and most placements end up with no blocker.
            StaticBody3D? proxy = null;
            var componentBlocked = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(placement))
        {
            if (mesh.Name.ToString() == "AuthoredKitCollisionProxy" || mesh.Mesh is null || !mesh.IsVisibleInTree())
            {
                continue;
            }

            var meshName = mesh.Name.ToString();
            // A mesh already assigned to an explicit physical owner (service
            // shed, usable porch or local support) must not receive a second body.
            if (existingSourceContacts.Contains(mesh.GetPath().ToString()))
                continue;
            var building = meshName.StartsWith("HeroHouse_", StringComparison.Ordinal)
                || AuthoredKitBlockerFamilies.Any(family => meshName.StartsWith(family, StringComparison.Ordinal));
            // Use the authored opening topology on every house, not only its
            // street face. The old global DwellingFacade side/rear exception
            // also removed walls from non-enterable neighboring houses.
            var piercedWall = building && meshName.Contains("_Wall", StringComparison.Ordinal)
                && meshName.EndsWith("_LOD0", StringComparison.Ordinal);
            var clinicShell = meshName is "FapFacade_Body_LOD0" or "FapServiceShed_Body_LOD0";
            var closedOpening = building && meshName.EndsWith("_LOD0", StringComparison.Ordinal)
                && (meshName.Contains("_Glass_", StringComparison.Ordinal)
                    || meshName.Contains("_Leaf_", StringComparison.Ordinal)
                    || meshName.EndsWith("StreetDoorClosed_LOD0", StringComparison.Ordinal)
                    || meshName is "FapFacade_DoorPanel_LOD0" or "FapFacade_ServiceDoor_LOD0");
            if (piercedWall || clinicShell || closedOpening)
            {
                proxy ??= NewKitBlockerProxy();
                proxy.AddChild(AuthoredSurfaceContact(placement, mesh));
                componentBlocked++;
                blocked++;
                exactArchitecture++;
                continue;
            }
            // Each log and transverse beam is a solid of its own. The generic
            // wall filter intentionally rejects low slabs and previously also
            // rejected every log here. Preserve the empty spaces between them.
            if (meshName.StartsWith("Woodpile_Log_", StringComparison.Ordinal)
                || meshName.StartsWith("Woodpile_Support", StringComparison.Ordinal))
            {
                // This one presentation explicitly wraps village_day's existing
                // FirewoodCollision; do not create a second physical owner.
                if (placement.Name == "MainStreetPhysicalFirewood") continue;
                proxy ??= NewKitBlockerProxy();
                proxy.AddChild(AuthoredSolidContact(placement, mesh, $"{meshName}_Contact"));
                componentBlocked++;
                blocked++;
                continue;
            }
            if (!building)
            {
                continue;
            }

            if (AuthoredKitClearanceParts.Any(part => meshName.Contains(part, StringComparison.Ordinal)))
            {
                continue;
            }

            var (frame, bounds) = AuthoredKitMeshBounds(mesh);
            var size = bounds.Size;
            if (size.X < .25f && size.Z < .25f)
            {
                continue;
            }

            // Bands and flat slabs (footing caps, plinths, floors, roof planes)
            // span the whole footprint but are not walls: blocking them would seal
            // the walkable interior of the hero house and turn every roof into a
            // ceiling. Walls are tall and thin, sheds are tall volumes; those stay.
            if (size.Y < .8f || Mathf.Min(size.X, size.Z) > 2.5f * size.Y)
            {
                skippedFlatSlab++;
                continue;
            }

            var centre = frame * bounds.GetCenter();
            // Never let a blocker reach into the walkable road envelope: the
            // route, the interaction approaches and the bypasses all run there.
            var road = AgentBAct1HeightField.RoadInfo(centre.X, centre.Z);
            if (road.Distance < road.HalfWidth - .15f)
            {
                skippedRoadClear++;
                continue;
            }

            // A raking verge is one inclined solid, not an upright wall. The
            // generic five-slab envelope filled the air below its slope. Keep
            // the existing eligibility filters and bake the actual beam instead.
            if (meshName.EndsWith("_VergeLeft_LOD0", StringComparison.Ordinal)
                || meshName.EndsWith("_VergeRight_LOD0", StringComparison.Ordinal))
            {
                proxy ??= NewKitBlockerProxy();
                var contact = AuthoredSolidContact(placement, mesh, $"{meshName}_Contact");
                contact.SetMeta("contactPolicy", "authored-solid-inclined-verge");
                proxy.AddChild(contact);
                componentBlocked++;
                blocked++;
                continue;
            }

            var shapes = AuthoredKitMeshBlockers(placement, mesh.Name, frame, bounds, doorTargets, out var meshCarved);
            carved += meshCarved;
            foreach (var shape in shapes)
            {
                proxy ??= NewKitBlockerProxy();
                shape.SetMeta("authoredSourceMesh", mesh.GetPath().ToString());
                proxy.AddChild(shape);
                componentBlocked++;
                blocked++;
            }
        }

            if (proxy is null)
            {
                continue;
            }

            placement.AddChild(proxy);
            placement.SetMeta("authoredKitBlockerCount", componentBlocked);
            placements++;
        }

        GD.Print(
            $"act1-kit-blockers: placements={placements} shapes={blocked} carved_door_slabs={carved} "
            + $"flat_slab_skipped={skippedFlatSlab} exact_architecture_surfaces={exactArchitecture} "
            + $"hidden_skipped={skippedHidden} road_clearance_skipped={skippedRoadClear}");
        GD.Print(
            $"act1-dwelling-threshold: placements={_dwellingThresholdPlacements} "
            + $"worst_analytic_vs_collision_delta={_dwellingThresholdWorst:F3}m "
            + $"worst_placement={_dwellingThresholdWorstPlacement}");
        SetMeta("authoredKitBlockerPlacementCount", placements);
        SetMeta("authoredKitBlockerShapeCount", blocked);
        SetMeta("authoredKitBlockerHiddenSkipCount", skippedHidden);
        SetMeta("authoredKitBlockerRoadSkipCount", skippedRoadClear);
        SetMeta("authoredArchitectureSurfaceCount", exactArchitecture);
        GroundAuthoredBuildingSupports();
        BuildYardSupportContacts();
        RebuildMosqueSurfaceContacts();
        SetAuthoredKitCollisionEnabled(!Act1WorldLayout.TryGetPlacement(ActiveZoneId, out var activePlacement)
            || !activePlacement.Interior, ActiveZoneId);
    }

    private void SetAuthoredKitCollisionEnabled(bool exteriorEnabled, string activeZoneId)
    {
        // The inner room uses the same seven real openings. Exterior dark
        // backing cards are useful only while that room is not presented;
        // retaining them inside made otherwise transparent glazing opaque.
        var heroFacade = GetNodeOrNull<Node3D>(
            "Act1CoreWorldGreybox/Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade");
        var roomWindowPrefixes = new[] { "Street_Window1", "Street_Window2", "Street_Window3",
            "Rear_Window0", "Rear_Window1", "Left_Window0", "Right_Window0" };
        if (heroFacade is not null)
        foreach (var mesh in FindDescendants<MeshInstance3D>(heroFacade).Where(mesh =>
            roomWindowPrefixes.Any(prefix => mesh.Name == "HeroHouse_" + prefix + "_Glass_LOD0"
                || mesh.Name == "HeroHouse_" + prefix + "_Recess_LOD0")))
        {
            if (!mesh.HasMeta("heroWindowExteriorOriginalVisibility"))
                mesh.SetMeta("heroWindowExteriorOriginalVisibility", mesh.Visible);
            mesh.Visible = activeZoneId != "house_old_pc" && mesh.GetMeta("heroWindowExteriorOriginalVisibility").AsBool();
        }
        if (GetNodeOrNull<Node3D>(
            "Act1CoreWorldGreybox/FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredFacade") is { } clinicFacade)
            ClinicSurfacePresentation.SetExteriorGlassVisible(clinicFacade, activeZoneId != "fap_clinic");
        // Visibility alone leaves the outdoor terrain and previous architecture
        // physical inside a separate room. Disable only these explicit exterior
        // owners; local room/NPC/interaction contacts keep their own lifecycle.
        foreach (var body in FindDescendants<StaticBody3D>(this)
            .Where(body => body.Name == "AuthoredKitCollisionProxy"
                || body.HasMeta("collisionOwner") && body.GetMeta("collisionOwner").AsString()
                    is "act1-exterior-terrain" or "act1-exterior-architecture" or "mosque-blocker"))
        {
            if (!body.HasMeta("exteriorOriginalCollisionLayer"))
            {
                body.SetMeta("exteriorOriginalCollisionLayer", (int)body.CollisionLayer);
                body.SetMeta("exteriorOriginalCollisionMask", (int)body.CollisionMask);
            }
            body.CollisionLayer = exteriorEnabled ? (uint)body.GetMeta("exteriorOriginalCollisionLayer").AsInt32() : 0;
            body.CollisionMask = exteriorEnabled ? (uint)body.GetMeta("exteriorOriginalCollisionMask").AsInt32() : 0;
        }
    }

    private void RebuildMosqueSurfaceContacts()
    {
        var mosque = GetNode<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex");
        var body = mosque.GetNode<StaticBody3D>("MosqueCollisionProxy");
        foreach (var old in body.GetChildren().OfType<CollisionShape3D>().ToArray())
        {
            // The old height cap shortened a 5.2m wall around its midpoint,
            // leaving its bottom half-metre unsupported. Use the actual wall.
            var name = old.Name.ToString();
            if (!name.EndsWith("_Blocker", StringComparison.Ordinal)) continue;
            var mesh = mosque.GetNode<MeshInstance3D>(name[..^8]);
            old.Free();
            body.AddChild(AuthoredSurfaceContact(body, mesh));
        }
        foreach (var name in new[] { "MosqueEntranceDoor", "MosqueEntranceStep" })
            body.AddChild(AuthoredSurfaceContact(body, mosque.GetNode<MeshInstance3D>(name)));
        var count = body.GetChildCount();
        body.SetMeta("mosqueBlockerShapeCount", count);
        mosque.SetMeta("mosqueBlockerShapeCount", count);
        mosque.SetMeta("contactPolicy", "full visible wall/closed-door/step triangles; no height cap or floating lower edge");
    }

    internal static CollisionShape3D AuthoredSurfaceContact(Node3D owner, MeshInstance3D mesh)
    {
        var surface = new ConcavePolygonShape3D { BackfaceCollision = true };
        surface.SetFaces(mesh.Mesh.GetFaces().Select(vertex => mesh.GlobalBasis * vertex).ToArray());
        var shape = new CollisionShape3D
        {
            Name = mesh.Name + "_SurfaceContact", Shape = surface,
            Transform = owner.GlobalTransform.AffineInverse() * new Transform3D(Basis.Identity, mesh.GlobalPosition)
        };
        shape.SetMeta("authoredSourceMesh", mesh.GetPath().ToString());
        shape.SetMeta("contactPolicy", "authored-triangles-preserve-openings");
        return shape;
    }

    private void BuildYardSupportContacts()
    {
        var architecture = GetNode<StaticBody3D>("Act1CoreWorldGreybox/AgentBExteriorWorld/AgentB_ArchitectureCollision");
        var count = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
        {
            if (!mesh.IsVisibleInTree() || mesh.Mesh is null || mesh.GetParent() is not Node3D parent) continue;
            var role = parent.HasMeta("presentationRole") ? parent.GetMeta("presentationRole").AsString() : string.Empty;
            var canopyPost = role == "open yard canopy / shelter" && mesh.Mesh is BoxMesh box
                && box.Size.Y > 1f && box.Size.X <= .25f && box.Size.Z <= .25f;
            var utilityShaft = mesh.Name == "PoleShaft" && mesh.Mesh is CylinderMesh
                && parent.Name.ToString().StartsWith("VillageUtilityPole", StringComparison.Ordinal);
            if (!canopyPost && !utilityShaft) continue;
            // These exact solid meshes already exist after all final visibility
            // suppressions. Their individual hulls stop bodies and interaction
            // rays without closing the canopy's open doorway or its whole yard.
            architecture.AddChild(AuthoredSolidContact(architecture, mesh, $"YardSupport_{count}_{mesh.Name}"));
            mesh.SetMeta("physicalYardSupport", true);
            count++;
        }
        SetMeta("physicalYardSupportCount", count);
    }

    private static CollisionShape3D AuthoredSolidContact(Node3D owner, MeshInstance3D mesh, string name)
    {
        // Bake rotation/scale into one small convex solid, preserving the log's
        // bevels and the pole's tapered polygon. No aggregate object AABB and no
        // shape inherits non-uniform scaling from an authored placement.
        var points = mesh.Mesh.GetFaces().Select(vertex => mesh.GlobalBasis * vertex).Distinct().ToArray();
        var shape = new CollisionShape3D
        {
            Name = name,
            Shape = new ConvexPolygonShape3D { Points = points },
            Transform = owner.GlobalTransform.AffineInverse() * new Transform3D(Basis.Identity, mesh.GlobalPosition)
        };
        shape.SetMeta("authoredSourceMesh", mesh.GetPath().ToString());
        return shape;
    }

    private static StaticBody3D NewKitBlockerProxy()
    {
        var proxy = new StaticBody3D
        {
            Name = "AuthoredKitCollisionProxy",
            CollisionLayer = 2,
            CollisionMask = 0
        };
        proxy.SetMeta("collisionOwner", "authored-kit-blocker");
        proxy.SetMeta("collisionStatus", "authored-blocker-layer-2");
        return proxy;
    }

    // Metric, upright frame aligned with the source wall, including rotations
    // inside its imported hierarchy. World AABBs inflate diagonal walls; using
    // those dimensions under a rotated placement then rotates the box again.
    // The longest source-box edge projected onto the ground also handles kits
    // whose native vertical coordinate was Z, not Y.
    internal static (Transform3D Frame, Aabb Bounds) AuthoredKitMeshBounds(MeshInstance3D mesh)
    {
        var local = mesh.Mesh.GetAabb();
        var basis = mesh.GlobalTransform.Basis;
        var edges = new[] { basis.X * local.Size.X, basis.Y * local.Size.Y, basis.Z * local.Size.Z };
        var horizontal = edges.Select(edge => new Vector3(edge.X, 0, edge.Z))
            .MaxBy(edge => edge.LengthSquared());
        var side = horizontal.LengthSquared() > 1e-8f ? horizontal.Normalized() : Vector3.Right;
        var frame = new Transform3D(new Basis(side, Vector3.Up, side.Cross(Vector3.Up)),
            mesh.GlobalTransform * local.GetCenter());
        return (frame, (frame.AffineInverse() * mesh.GlobalTransform) * local);
    }

    // Internal so the physics diagnostic can exercise the production builder
    // on rotated/scaled fixtures as well as checking actual village facades.
    internal static IReadOnlyList<CollisionShape3D> AuthoredKitMeshBlockers(Node3D placement, StringName meshName,
        Transform3D frame, Aabb bounds, IReadOnlyList<Vector3> approachTargets, out int carved)
    {
        var size = bounds.Size;
        var centre = bounds.GetCenter();
        var boxSize = new Vector3(Mathf.Max(size.X - .06f, .12f), Mathf.Min(size.Y, 3.4f),
            Mathf.Max(size.Z - .06f, .12f));
        var boxCentre = centre;
        // Clip tall wall tops without lifting the lower contact face above the
        // visible base. Ordinary wall heights keep their original centre.
        boxCentre.Y = bounds.Position.Y + boxSize.Y * .5f;
        var toFrame = frame.AffineInverse();
        var localTargets = approachTargets.Select(target => toFrame * target).ToArray();
        var shapes = new List<CollisionShape3D>();
        carved = 0;
        if (!localTargets.Any(target => Mathf.Abs(target.Y - centre.Y) < 3.4f
                && Mathf.Abs(target.X - centre.X) <= size.X * .5f + .9f
                && Mathf.Abs(target.Z - centre.Z) <= size.Z * .5f + .9f))
        {
            shapes.Add(BlockerShape($"{meshName}_Blocker", placement,
                new Transform3D(frame.Basis, frame * boxCentre), boxSize));
            return shapes;
        }

        // Preserve the existing five-slab approach policy and clearance values,
        // but measure the target and every slab in the same unscaled wall frame.
        var alongX = size.X >= size.Z;
        const int slabs = 5;
        var slice = (alongX ? size.X : size.Z) / slabs;
        for (var index = 0; index < slabs; index++)
        {
            var offset = -.5f * (alongX ? size.X : size.Z) + (index + .5f) * slice;
            var slabCentre = boxCentre + (alongX ? new Vector3(offset, 0, 0) : new Vector3(0, 0, offset));
            if (localTargets.Any(target => Mathf.Abs(target.Y - centre.Y) <= 3.4f
                    && Mathf.Abs(target.X - slabCentre.X) <= slice * .5f + .45f
                    && Mathf.Abs(target.Z - slabCentre.Z) <= slice * .5f + .45f))
            {
                carved++;
                continue;
            }
            var slabSize = alongX
                ? new Vector3(Mathf.Max(slice - .04f, .1f), boxSize.Y, boxSize.Z)
                : new Vector3(boxSize.X, boxSize.Y, Mathf.Max(slice - .04f, .1f));
            shapes.Add(BlockerShape($"{meshName}_Blocker{index}", placement,
                new Transform3D(frame.Basis, frame * slabCentre), slabSize));
        }
        return shapes;
    }

    private static CollisionShape3D BlockerShape(string name, Node3D placement, Transform3D worldBox, Vector3 size)
    {
        return new CollisionShape3D
        {
            Name = name,
            // Cancel the full parent basis, not just its scale. The desired
            // physics box then has a unit world basis and exactly one yaw.
            Transform = placement.GlobalTransform.AffineInverse() * worldBox,
            Shape = new BoxShape3D { Size = size }
        };
    }
}
