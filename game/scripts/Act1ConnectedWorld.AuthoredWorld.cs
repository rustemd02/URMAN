using Godot;
using System.Text.Json;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
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
