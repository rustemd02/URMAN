using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// PW-037 / ACT1-PERF.PROFILE: a station render histogram of the arrival street
/// (09.10.2026, C1: 6.5k visible instances, 6.0k of them shadow casters, 19-26k
/// draw calls on the GTX 970) shows every village dwelling drawn as ~160 small
/// joinery members, each repeated in every directional shadow cascade. Window
/// glass, mullions, rails, jambs, sills, drip edges, rafter tails, gutters,
/// downpipes, handles, icicles and fretwork teeth add nothing a player can read
/// in the sun's shadow — the recess, wall and roof already cast it — so those
/// members of house kits stop casting. Their own shading, ambient occlusion and
/// the casings' contact are unchanged.
/// </summary>
public partial class Act1ConnectedWorld
{
    private static readonly string[] ShadowlessJoinery =
    [
        "_Glass", "_Mullion", "_Rail", "_Jamb", "_Sill", "SillDrip", "RafterTail", "Gutter",
        "Downpipe", "_Handle", "Icicle", "Tooth", "_LatchPlate", "_Hinge", "_Recess", "Spout"
    ];
    private static readonly string[] HouseKitOwners = ["VillageParcel_", "DwellingFacade", "HeroHouse", "Variant"];

    private static int TrimHouseJoineryShadows(Node3D root)
    {
        var trimmed = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(root))
        {
            if (mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off) continue;
            var name = mesh.Name.ToString();
            if (!HouseKitOwners.Any(owner => name.StartsWith(owner, StringComparison.Ordinal))) continue;
            if (!ShadowlessJoinery.Any(part => name.Contains(part, StringComparison.Ordinal))) continue;
            // Yard fence rails throw the long readable stripes on the snow; they stay.
            if (name.Contains("_Yard_", StringComparison.Ordinal)) continue;
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            trimmed++;
        }
        if (trimmed > 0) GD.Print($"act1-shadow-budget: joineryCastersOff={trimmed}");
        return trimmed;
    }
}
