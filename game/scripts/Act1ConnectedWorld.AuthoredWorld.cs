using Godot;
using System.Text.Json;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string PrivateVariantACatalogId =
        "urman.catalog:urman_village_exterior_kit/villageparcel-varianta-timbergable";

    private static readonly (string NodeName, int TriangleCount)[] PrivateVariantAOuterWalls =
    [
        ("VariantA_TimberGable_Dwelling_Left_Wall_LOD0", 68),
        ("VariantA_TimberGable_Dwelling_Rear_Wall_LOD0", 68),
        ("VariantA_TimberGable_Dwelling_Right_Wall_LOD0", 40),
        ("VariantA_TimberGable_Dwelling_Street_Wall_LOD0", 96)
    ];

    public AuthoredWorldDirector? AuthoredWorld { get; private set; }

    /// <summary>
    /// Authored world content from URMAN Studio (generic plots under
    /// res://content/world). Built last, so authored objects sit on top of the
    /// finished street and their interactions join the village zone.
    /// </summary>
    private IEnumerable<Action> BuildAuthoredWorldJobs()
    {
        if (AuthoredWorld is not null) yield break;
        yield return () => AuthoredWorld = AuthoredWorldDirector.CreateForBuild(
            GetNode<Node3D>("Act1CoreWorldGreybox"), _zoneInstances["village_day"]);
        foreach (var job in ProfileAuthoredBuildJobs("authored-content", AuthoredWorld!.BuildJobs()))
            yield return job;
        // Generic plots are attached after the core kit's material pass. Bind
        // their existing named source surfaces too, so the new quarter does
        // not silently retain plain preview materials.
        var rebound = 0;
        IEnumerable<Action> MaterialJobs()
        {
            // Every authored object remains under the same parent/transform.
            // Regrade its complete subtree together so geometry helpers still
            // see all of that dwelling's walls, openings and gate members.
            foreach (var root in AuthoredWorld!.GetChildren().OfType<Node3D>().ToArray())
                yield return () => rebound += RegradeAct1DaylightKitMaterials(root);
            yield return () => AuthoredWorld!.SetMeta("revisedSurfaceBindings", rebound);
        }
        foreach (var job in ProfileAuthoredBuildJobs("authored-materials", MaterialJobs()))
            yield return job;
        if (!_studioPreview)
        {
            IEnumerable<Action> OccluderJobs()
            {
                yield return () => AttachPrivateVariantAOuterWallOccluders(AuthoredWorld!);
            }
            foreach (var job in ProfileAuthoredBuildJobs("authored-wall-occluders", OccluderJobs()))
                yield return job;
        }
        // Use the same plot discovery contract as the director. A household
        // keeps its address when it moves between quarters; no second list of
        // addressed plots should silently omit the far bank.
        IEnumerable<Action> AddressJobs()
        {
            AddressFacadeMount.PreparedWorldFaces? preparedWorldFaces = null;
            yield return () => preparedWorldFaces = AddressFacadeMount.PrepareWorldFaces(this);
            foreach (var file in DirAccess.GetFilesAt(AuthoredWorldDirector.WorldDirectory)
                .Where(file => file.EndsWith(".world.v1.json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
            {
                using var layout = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString($"{AuthoredWorldDirector.WorldDirectory}/{file}"));
                if (!layout.RootElement.TryGetProperty("executor", out var executor) || executor.GetString() != "generic") continue;
                foreach (var row in layout.RootElement.GetProperty("entities").EnumerateArray())
                {
                    if (!row.GetProperty("params").TryGetProperty("address", out _)) continue;
                    var captured = row.Clone();
                    yield return () => RegisterAuthoredPlotAddress(captured, preparedWorldFaces!);
                }
            }
            yield return () => preparedWorldFaces!.ReportProfile();
        }
        foreach (var job in ProfileAuthoredBuildJobs("authored-addresses", AddressJobs()))
            yield return job;
        yield return () =>
        {
            foreach (var anchor in KitPlacementTakeover.Unmatched())
                GD.PushError($"authored-world: the plot owns placement {anchor}, but the village builder no longer places it; decide in URMAN Studio.");
        };
    }

    private static int AttachPrivateVariantAOuterWallOccluders(AuthoredWorldDirector world)
    {
        var attached = 0;
        var skipped = 0;
        var totalTriangles = 0;
        var requiredNames = PrivateVariantAOuterWalls.Select(wall => wall.NodeName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var id in world.ObjectIds.Order(StringComparer.Ordinal))
        {
            var root = world.ObjectRoot(id);
            if (root is null || !root.HasMeta("catalogId")
                || root.GetMeta("catalogId").AsString() != PrivateVariantACatalogId)
                continue;

            if (world.HasHouseModelOverride(id)
                || root.GetNodeOrNull<OccluderInstance3D>("PrivateOuterWallOccluder") is not null)
            {
                skipped++;
                continue;
            }

            var meshes = FindDescendants<MeshInstance3D>(root)
                .Where(mesh => requiredNames.Contains(mesh.Name.ToString()))
                .GroupBy(mesh => mesh.Name.ToString(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            if (PrivateVariantAOuterWalls.Any(wall =>
                    !meshes.TryGetValue(wall.NodeName, out var matches) || matches.Length != 1))
            {
                skipped++;
                continue;
            }

            var points = new List<Vector3>(PrivateVariantAOuterWalls.Sum(wall => wall.TriangleCount * 3));
            var safe = true;
            foreach (var source in PrivateVariantAOuterWalls)
            {
                var mesh = meshes[source.NodeName][0];
                var sourceMesh = mesh.Mesh;
                if (!mesh.Visible || sourceMesh is null || sourceMesh.GetSurfaceCount() != 1
                    || !IsOpaqueStaticOccluderSurface(mesh)
                    || !TryGetRelativeTransform(mesh, root, out var meshToRoot))
                {
                    safe = false;
                    break;
                }

                var faces = sourceMesh.GetFaces();
                if (faces.Length != source.TriangleCount * 3)
                {
                    safe = false;
                    break;
                }

                foreach (var vertex in faces)
                    points.Add(meshToRoot * vertex);
            }

            var triangleCount = points.Count / 3;
            if (!safe || triangleCount != 272)
            {
                skipped++;
                continue;
            }

            var occluder = new ArrayOccluder3D();
            occluder.SetArrays(points.ToArray(), Enumerable.Range(0, points.Count).ToArray());
            var instance = new OccluderInstance3D
            {
                Name = "PrivateOuterWallOccluder",
                Occluder = occluder
            };
            instance.SetMeta("sourceFamily", "VariantA_TimberGable_Dwelling outer wall LOD0");
            instance.SetMeta("sourceTriangles", triangleCount);
            root.AddChild(instance);
            attached++;
            totalTriangles += triangleCount;
        }

        world.SetMeta("privateVariantAOuterWallOccluderCount", attached);
        world.SetMeta("privateVariantAOuterWallOccluderTriangles", totalTriangles);
        GD.Print($"act1-occlusion|private-variant-a-walls|instances={attached}|triangles={totalTriangles}|skipped={skipped}");
        return attached;
    }

    private static bool IsOpaqueStaticOccluderSurface(MeshInstance3D mesh)
    {
        if (mesh.MaterialOverride is not null || mesh.MaterialOverlay is not null || mesh.Transparency > 0f)
            return false;

        var sourceMesh = mesh.Mesh;
        if (sourceMesh is null || sourceMesh.GetSurfaceCount() != 1
            || mesh.GetSurfaceOverrideMaterial(0) is not null
            || sourceMesh.SurfaceGetMaterial(0) is not ShaderMaterial material
            || material.NextPass is not null)
            return false;

        var shader = material.Shader;
        if (shader is null)
            return false;
        var code = shader.Code;
        return code.Contains("shader_type spatial;", StringComparison.Ordinal)
            && code.Contains("render_mode diffuse_burley, specular_schlick_ggx;", StringComparison.Ordinal)
            && !code.Contains("blend_", StringComparison.Ordinal)
            && !code.Contains("ALPHA =", StringComparison.Ordinal);
    }

    private static bool TryGetRelativeTransform(Node3D node, Node3D ancestor, out Transform3D transform)
    {
        transform = Transform3D.Identity;
        Node? cursor = node;
        while (cursor is not null && !ReferenceEquals(cursor, ancestor))
        {
            if (cursor is not Node3D node3D)
                return false;

            transform = node3D.Transform * transform;
            cursor = cursor.GetParent();
        }

        return ReferenceEquals(cursor, ancestor);
    }

    private static IEnumerable<Action> ProfileAuthoredBuildJobs(string stage, IEnumerable<Action> jobs)
    {
        // Count construction CPU time, not the frames spent waiting for the
        // main-thread budget. Keep the existing phase log useful for native A/B.
        var elapsed = new System.Diagnostics.Stopwatch();
        using var iterator = jobs.GetEnumerator();
        while (true)
        {
            bool more;
            elapsed.Start();
            try { more = iterator.MoveNext(); }
            finally { elapsed.Stop(); }
            if (!more) break;
            var job = iterator.Current;
            yield return () =>
            {
                elapsed.Start();
                try { job(); }
                finally { elapsed.Stop(); }
            };
        }
        yield return () => GD.Print($"world-build-profile|{stage}|{elapsed.Elapsed.TotalSeconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}s");
    }

    private void RegisterAuthoredPlotAddress(JsonElement row, AddressFacadeMount.PreparedWorldFaces preparedWorldFaces)
    {
        var address = row.GetProperty("params").GetProperty("address");
        var id = row.GetProperty("id").GetString()!;
        var root = AuthoredWorld!.ObjectRoot(id) ?? throw new InvalidOperationException("Missing addressed household: " + id);
        if (!TryAddressDoor(root, null, out var door, out var outward))
            throw new InvalidOperationException("New household has no actual door: " + id);
        var addressId = address.GetProperty("id").GetString()!;
        var gates = FindDescendants<MeshInstance3D>(root).Where(mesh => mesh.Mesh is not null
            && (mesh.Name.ToString().Contains("_Yard_GateLeft", StringComparison.Ordinal)
                || mesh.Name.ToString().Contains("_Yard_GateRight", StringComparison.Ordinal)
                || mesh.Name.ToString().Contains("_Yard_GatePostLeft", StringComparison.Ordinal)
                || mesh.Name.ToString().Contains("_Yard_GatePostRight", StringComparison.Ordinal))).ToArray();
        var accessOwner = door;
        if (gates.Length == 2)
        {
            accessOwner = gates.Select(mesh => mesh.ToGlobal(mesh.Mesh.GetAabb().GetCenter())).Aggregate(Vector3.Zero, (a, b) => a + b) / 2;
            outward = root.GlobalBasis.Z.Normalized();
        }
        var access = AddressGround(accessOwner + outward * .72f);
        root.SetMeta("addressAccessOwner", gates.Length == 2 ? "actual street gate" : "actual dwelling door");
        Vector3 sign = default, signOutward = default; string owner = "", failure = "";
        // Behind a street gate the plate belongs on the gate itself, readable from the street;
        // the house facade stays the fallback when the gate offers no free rail or post.
        var mount = false;
        if (gates.Length == 2 && AddressFacadeMount.TryFindOnOwnYardFence(root, accessOwner, outward, this, out sign, out owner, out failure, preparedWorldFaces)) { mount = true; signOutward = outward; }
        if (!mount) mount = AddressFacadeMount.TryFind(root, door, outward, this, out sign, out signOutward, out owner, out failure,
            preparedWorldFaces: preparedWorldFaces);
        root.SetMeta("addressSignMountAvailable", mount);
        root.SetMeta("addressSignMountOwner", owner);
        root.SetMeta("addressSignMountFailure", failure);
        var slug = addressId[4..];
        RegisterAddressedBuilding(new(root, id, "BLD-" + slug, "PAR-" + slug, addressId,
            address.GetProperty("street").GetString()!, address.GetProperty("number").GetString()!,
            address.GetProperty("cadastral").GetString()!, access, sign, outward, "residential", SignOutward: signOutward));
    }
}
