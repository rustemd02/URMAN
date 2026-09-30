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
    private void BuildAuthoredWorld()
    {
        if (AuthoredWorld is not null) return;
        AuthoredWorld = AuthoredWorldDirector.Build(GetNode<Node3D>("Act1CoreWorldGreybox"), _zoneInstances["village_day"]);
        // Generic plots are attached after the core kit's material pass. Bind
        // their existing named source surfaces too, so the new quarter does
        // not silently retain plain preview materials.
        var rebound = RegradeAct1DaylightKitMaterials(AuthoredWorld);
        AuthoredWorld.SetMeta("revisedSurfaceBindings", rebound);
        using var layout = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString("res://content/world/act1_north_street.world.v1.json"));
        foreach (var row in layout.RootElement.GetProperty("entities").EnumerateArray())
        {
            if (!row.GetProperty("params").TryGetProperty("address", out var address)) continue;
            var id = row.GetProperty("id").GetString()!;
            var root = AuthoredWorld.ObjectRoot(id) ?? throw new InvalidOperationException("Missing addressed household: " + id);
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
            var mount = AddressFacadeMount.TryFind(root, door, outward, this, out var sign, out var signOutward, out var owner, out var failure);
            root.SetMeta("addressSignMountAvailable", mount);
            root.SetMeta("addressSignMountOwner", owner);
            root.SetMeta("addressSignMountFailure", failure);
            var slug = addressId[4..];
            RegisterAddressedBuilding(new(root, id, "BLD-" + slug, "PAR-" + slug, addressId,
                address.GetProperty("street").GetString()!, address.GetProperty("number").GetString()!,
                address.GetProperty("cadastral").GetString()!, access, sign, outward, "residential", SignOutward: signOutward));
        }
        foreach (var anchor in KitPlacementTakeover.Unmatched())
        {
            GD.PushError($"authored-world: the plot owns placement {anchor}, but the village builder no longer places it; decide in URMAN Studio.");
        }
    }
}
