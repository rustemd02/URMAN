using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private static void SetInteriorStaticCollisionEnabled(IEnumerable<StaticBody3D> bodies, bool isActive)
    {
        foreach (var body in bodies.Where(candidate => candidate is not InteractionTarget))
        {
            if (!body.HasMeta("connectedWorldOriginalCollisionLayer"))
            {
                body.SetMeta("connectedWorldOriginalCollisionLayer", (int)body.CollisionLayer);
                body.SetMeta("connectedWorldOriginalCollisionMask", (int)body.CollisionMask);
            }
            body.CollisionLayer = isActive
                ? (uint)body.GetMeta("connectedWorldOriginalCollisionLayer").AsInt32() : 0u;
            body.CollisionMask = isActive
                ? (uint)body.GetMeta("connectedWorldOriginalCollisionMask").AsInt32() : 0u;
        }
    }

    /// <summary>Refresh only the hero window shell; incompatible geometry uses the caller's full reload.</summary>
    public bool TryRefreshHeroHouseWindowGeometry(HeroHouseRecipe previousRecipe, HeroHouseRecipe nextRecipe)
    {
        bool Reject(string reason)
        {
            GD.Print($"hero-window-refresh: skipped={reason}");
            return false;
        }

        // A recipe-only edit cannot supply matching exterior geometry; use the existing reload path.
        if (previousRecipe.ModelPath == nextRecipe.ModelPath
            && HeroHouseRecipe.WindowIds.Any(id => previousRecipe.WidthFor(id) != nextRecipe.WidthFor(id)))
            return Reject("same-model-path-width-edit");
        var facade = GetNodeOrNull<Node3D>(
            "Act1CoreWorldGreybox/Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade");
        if (!IsInsideTree()) return Reject("outside-tree");
        if (facade is null) return Reject("facade-missing");
        if (!facade.IsVisibleInTree()) return Reject("facade-hidden");
        if (!_zoneInstances.TryGetValue("house_old_pc", out var room) || room is null)
            return Reject("room-missing");
        if (!room.HasMeta("heroHouseWindowClearWidth")) return Reject("width-meta-missing");
        var roomWidth = room.GetMeta("heroHouseWindowClearWidth");
        if (roomWidth.VariantType != Variant.Type.Float) return Reject("width-meta-type");
        var roomWidthValue = roomWidth.AsSingle();
        if (!float.IsFinite(roomWidthValue)) return Reject("width-meta-value");
        if (Math.Abs(roomWidthValue - previousRecipe.WindowClearWidth) > .0001f)
            return Reject("width-baseline-mismatch");
        var oldComponent = facade.GetNodeOrNull<Node3D>(HeroHouseRecipe.Component);
        if (oldComponent is null) return Reject("component-missing");
        if (!facade.HasMeta("assetSource")) return Reject("asset-meta-missing");
        if (facade.GetMeta("assetSource").AsString() != (previousRecipe.ModelPath ?? VillageExteriorKitScenePath))
            return Reject("asset-baseline-mismatch");

        Node3D? imported = null;
        var stagingOwner = new Node3D { Name = "HeroWindowRefreshStaging" };
        Node3D? freshComponent = null;
        HeroExteriorCollisionStage collision = default;
        var componentIndex = oldComponent.GetIndex();
        var oldName = oldComponent.Name;
        var oldAssetSource = facade.GetMeta("assetSource");
        var hadCount = facade.HasMeta("authoredKitBlockerCount");
        var oldCount = hadCount ? facade.GetMeta("authoredKitBlockerCount") : default;
        var swapped = false;
        var committed = false;
        var retainedContacts = new List<(Node Node, int Index)>();
        var rearIndices = new List<(CollisionShape3D Node, int Index)>();
        var proxyIndex = -1;
        try
        {
            var currentRecipe = HeroHouseRecipe.Load();
            if (currentRecipe.ModelPath != nextRecipe.ModelPath
                || currentRecipe.WindowClearHeight != nextRecipe.WindowClearHeight
                || HeroHouseRecipe.WindowIds.Any(id => currentRecipe.WidthFor(id) != nextRecipe.WidthFor(id)))
                return Reject("recipe-no-longer-current");
            var packed = ResourceLoader.Load<PackedScene>(VillageExteriorKitScenePath);
            if (packed is null) return Reject("kit-scene-missing");
            var root = packed.Instantiate();
            if (root is not Node3D imported3D) { root.Free(); return Reject("kit-root-not-node3d"); }
            imported = imported3D;
            var kit = imported.Name == VillageExteriorKitRootName ? imported
                : imported.GetNodeOrNull<Node3D>(VillageExteriorKitRootName);
            var baseline = kit?.GetNodeOrNull<Node3D>(VillageExteriorKitComponentNames[9]);
            if (baseline is null || FindDescendants<Node>(baseline).Any(node => node is CollisionObject3D or CollisionShape3D))
                return Reject("kit-baseline-invalid");
            var basis = ComposeImportedAncestorBasis(baseline);
            facade.GetParent().AddChild(stagingOwner);
            var stagedFacade = new Node3D { Name = "BabaiApproachDwellingFacade", Transform = facade.Transform };
            stagingOwner.AddChild(stagedFacade);
            baseline.GetParent().RemoveChild(baseline);
            ClearExtractedSceneOwnership(baseline);
            baseline.Transform = new Transform3D(basis, Vector3.Zero);
            stagedFacade.AddChild(baseline);
            KitPlacementTakeover.Apply(stagedFacade, HeroHouseRecipe.Component, VillageExteriorKitScenePath,
                "house_old_pc@babai-approach");
            // Replace window geometry while preserving the live placement and terrain alignment.
            stagedFacade.GlobalTransform = facade.GlobalTransform;
            freshComponent = stagedFacade.GetNodeOrNull<Node3D>(HeroHouseRecipe.Component);
            if (freshComponent is null || !HeroRefreshTransformsEqual(stagedFacade.GlobalTransform, facade.GlobalTransform)
                || !stagedFacade.Visible
                || FindDescendants<Node>(freshComponent).Any(node => node is CollisionObject3D or CollisionShape3D))
                return Reject("staged-component-invalid");
            RegradeAct1DaylightKitMaterials(stagedFacade);
            ApplyHeroWarmWindow(stagedFacade);
            var door = FindDescendants<MeshInstance3D>(freshComponent)
                .Where(mesh => mesh.Name == "HeroHouse_StreetDoorClosed_LOD0").ToArray();
            if (door.Length != 1) return Reject("door-anchor-count");
            if (!HeroRefreshAnchorsCompatible(facade, oldComponent, freshComponent, out var incompatibleReason))
            {
                GD.Print($"hero-window-refresh: incompatible={incompatibleReason}; using full world reload");
                return false;
            }
            door[0].Visible = true;

            // Keep the old subtree alive but remove its canonical paths before collision capture.
            oldComponent.Name = "HeroWindowRefreshPreviousComponent";
            swapped = true;
            stagedFacade.RemoveChild(freshComponent);
            facade.AddChild(freshComponent);
            facade.MoveChild(freshComponent, componentIndex);
            collision = PrepareHeroExteriorCollisionStage(facade, freshComponent);
            if (collision.PreviousKitProxy is null || collision.ReplacementKitProxy is null)
                return Reject("collision-proxies-invalid");
            var prefix = freshComponent.GetPath().ToString() + "/";
            proxyIndex = collision.PreviousKitProxy.GetIndex();
            foreach (var contact in collision.PreviousKitProxy.GetChildren())
            {
                if (!contact.HasMeta("authoredSourceMesh")) return Reject("retained-contact-source-missing");
                var source = contact.GetMeta("authoredSourceMesh").AsString();
                if (source.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (contact is not CollisionShape3D || facade.GetNodeOrNull<MeshInstance3D>(source) is null)
                    return Reject("retained-contact-owner-invalid");
                retainedContacts.Add((contact, contact.GetIndex()));
            }
            foreach (var (contact, _) in retainedContacts)
            {
                collision.PreviousKitProxy.RemoveChild(contact);
                collision.ReplacementKitProxy.AddChild(contact);
            }
            facade.RemoveChild(collision.PreviousKitProxy);
            facade.AddChild(collision.ReplacementKitProxy);
            facade.MoveChild(collision.ReplacementKitProxy, proxyIndex);
            foreach (var shape in collision.PreviousRearBoundaryShapes)
            {
                rearIndices.Add((shape, shape.GetIndex()));
                shape.GetParent().RemoveChild(shape);
            }
            foreach (var shape in collision.ReplacementRearBoundaryShapes) _act1BypassCollision!.AddChild(shape);
            facade.RemoveChild(oldComponent);
            facade.SetMeta("assetSource", nextRecipe.ModelPath ?? VillageExteriorKitScenePath);
            facade.SetMeta("authoredKitBlockerCount", collision.ReplacementKitShapeCount + retainedContacts.Count);
            SetAuthoredKitCollisionEnabled(!Act1WorldLayout.TryGetPlacement(ActiveZoneId, out var activePlacement)
                || !activePlacement.Interior, ActiveZoneId);
            // Leave the internally atomic interior replacement until exterior preparation/routing succeeds.
            StyleBenchmarkInteriorFactory.RefreshWindowShell(room, nextRecipe,
                bodies => SetInteriorStaticCollisionEnabled(bodies, ActiveZoneId == "house_old_pc"));
            committed = true;
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"hero-window-refresh: failed={exception.GetType().Name}; using full world reload");
            return false;
        }
        finally
        {
            if (!committed)
            {
                foreach (var shape in collision.ReplacementRearBoundaryShapes ?? [])
                {
                    shape.GetParent()?.RemoveChild(shape);
                    shape.Free();
                }
                foreach (var (shape, index) in rearIndices.OrderBy(item => item.Index))
                {
                    _act1BypassCollision!.AddChild(shape);
                    _act1BypassCollision.MoveChild(shape, Math.Min(index, _act1BypassCollision.GetChildCount() - 1));
                }
                foreach (var (contact, _) in retainedContacts)
                    if (contact.GetParent() == collision.ReplacementKitProxy)
                    {
                        collision.ReplacementKitProxy!.RemoveChild(contact);
                        collision.PreviousKitProxy!.AddChild(contact);
                    }
                foreach (var (contact, index) in retainedContacts.OrderBy(item => item.Index))
                    collision.PreviousKitProxy!.MoveChild(contact, Math.Min(index, collision.PreviousKitProxy.GetChildCount() - 1));
                if (collision.ReplacementKitProxy is { } replacement)
                {
                    replacement.GetParent()?.RemoveChild(replacement);
                    replacement.Free();
                }
                if (collision.PreviousKitProxy is { } previous && previous.GetParent() is null)
                {
                    facade.AddChild(previous);
                    facade.MoveChild(previous, Math.Min(proxyIndex, facade.GetChildCount() - 1));
                }
                if (swapped)
                {
                    freshComponent!.GetParent()?.RemoveChild(freshComponent);
                    freshComponent.Free();
                    if (oldComponent.GetParent() is null) facade.AddChild(oldComponent);
                    oldComponent.Name = oldName;
                    facade.MoveChild(oldComponent, Math.Min(componentIndex, facade.GetChildCount() - 1));
                }
                facade.SetMeta("assetSource", oldAssetSource);
                if (hadCount) facade.SetMeta("authoredKitBlockerCount", oldCount);
                else facade.RemoveMeta("authoredKitBlockerCount");
            }
            else
            {
                oldComponent.Free();
                collision.PreviousKitProxy!.Free();
                foreach (var shape in collision.PreviousRearBoundaryShapes) shape.Free();
            }
            stagingOwner.Free();
            imported?.Free();
        }
    }

    private static bool HeroRefreshTransformsEqual(Transform3D a, Transform3D b) =>
        HeroRefreshVectorsEqual(a.Origin, b.Origin) && HeroRefreshVectorsEqual(a.Basis.X, b.Basis.X)
        && HeroRefreshVectorsEqual(a.Basis.Y, b.Basis.Y) && HeroRefreshVectorsEqual(a.Basis.Z, b.Basis.Z);

    private static bool HeroRefreshVectorsEqual(Vector3 a, Vector3 b) =>
        a.IsFinite() && b.IsFinite() && a.DistanceSquaredTo(b) <= .000001f;

    private static bool HeroRefreshAnchorsCompatible(Node3D facade, Node3D previous, Node3D next,
        out string reason)
    {
        static bool Anchored(MeshInstance3D mesh) => mesh.Name.ToString().Contains("Foundation", StringComparison.Ordinal)
            || mesh.Name.ToString().Contains("Footing", StringComparison.Ordinal)
            || mesh.Name.ToString().Contains("Step", StringComparison.Ordinal)
            || HasTrueMeta(mesh, "requiresTerrainSupport");
        // Opening widths may change wall topology and generated log segments. Only
        // support/threshold geometry has an exact per-mesh preservation contract.
        var oldMeshes = FindDescendants<MeshInstance3D>(previous).Where(Anchored).ToArray();
        var newMeshes = FindDescendants<MeshInstance3D>(next).Where(Anchored).ToArray();
        reason = "support-membership";
        if (oldMeshes.Length == 0 || oldMeshes.Length != newMeshes.Length
            || oldMeshes.Select(mesh => mesh.Name.ToString()).Distinct().Count() != oldMeshes.Length
            || newMeshes.Select(mesh => mesh.Name.ToString()).Distinct().Count() != newMeshes.Length) return false;
        foreach (var old in oldMeshes)
        {
            var fresh = newMeshes.SingleOrDefault(mesh => mesh.Name == old.Name);
            reason = "support-transform";
            if (fresh?.Mesh is null || old.Mesh is null
                || !HeroRefreshTransformsEqual(old.GlobalTransform, fresh.GlobalTransform)) return false;
            var a = old.Mesh.GetAabb();
            var b = fresh.Mesh.GetAabb();
            reason = "support-bounds";
            if (!HeroRefreshVectorsEqual(a.Position, b.Position) || !HeroRefreshVectorsEqual(a.Size, b.Size)) return false;
            var oldFaces = old.Mesh.GetFaces();
            var newFaces = fresh.Mesh.GetFaces();
            reason = "support-topology";
            if (oldFaces.Length == 0 || oldFaces.Length != newFaces.Length
                || oldFaces.Where((vertex, index) => !HeroRefreshVectorsEqual(vertex, newFaces[index])).Any()) return false;
            if (!old.HasMeta("foundationTerrainSupport")) continue;
            var supportPath = old.GetMeta("foundationTerrainSupport").AsString();
            var support = facade.GetNodeOrNull<MeshInstance3D>(supportPath);
            reason = "terrain-support-owner";
            if (support is null || support.GetParent() != facade || support.Mesh is null
                || !support.HasMeta("foundationSourceMesh")
                || support.GetMeta("foundationSourceMesh").AsString() != old.GetPath().ToString()) return false;
            fresh.SetMeta("foundationTerrainSupport", supportPath);
        }
        reason = "wall-bounds-unavailable";
        if (!TryHeroRefreshWallBounds(previous, out var previousBounds)
            || !TryHeroRefreshWallBounds(next, out var nextBounds)) return false;
        reason = "outer-wall-bounds";
        if (!HeroRefreshVectorsEqual(previousBounds.Position, nextBounds.Position)
            || !HeroRefreshVectorsEqual(previousBounds.Size, nextBounds.Size)) return false;
        reason = string.Empty;
        return true;
    }

    private static bool TryHeroRefreshWallBounds(Node3D component, out Aabb bounds)
    {
        bounds = default;
        var found = false;
        foreach (var mesh in FindDescendants<MeshInstance3D>(component).Where(mesh =>
            mesh.Name.ToString().StartsWith("HeroHouse_", StringComparison.Ordinal)
            && (mesh.Name.ToString().Contains("_Wall", StringComparison.Ordinal)
                || mesh.Name.ToString().Contains("_LogCrown", StringComparison.Ordinal))))
        {
            if (mesh.Mesh is null) return false;
            var local = mesh.Mesh.GetAabb();
            if (!local.Position.IsFinite() || !local.Size.IsFinite()
                || local.Size.X < 0 || local.Size.Y < 0 || local.Size.Z < 0) return false;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = mesh.GlobalTransform * (local.Position + new Vector3(
                    (corner & 1) == 0 ? 0 : local.Size.X,
                    (corner & 2) == 0 ? 0 : local.Size.Y,
                    (corner & 4) == 0 ? 0 : local.Size.Z));
                if (!point.IsFinite()) return false;
                bounds = found ? bounds.Expand(point) : new Aabb(point, Vector3.Zero);
                found = true;
            }
        }
        return found && bounds.Size.LengthSquared() > 0;
    }
}
