using Godot;
using System.Collections.Generic;

namespace Urman.Experiments.AgentBAct1;

/// <summary>
/// Deterministic foliage planting plan for the production exterior layer.
/// Ported 1:1 from the verified Agent B capture run (45/45 frames,
/// 11/11 waypoints): positions are world-space (x, z), variant keys map
/// to the AgentB_FoliageKit template families.
///
/// Visual reset (VIS-025/026/075/082/083/084). The plan is now written with
/// explicit <c>Winter*</c> geometry keys instead of the summer family names the
/// composer used to collapse: <c>AgentBAct1ExteriorLayer.BuildDensifiedPlan</c>
/// rewrites every <c>Birch_*</c> to two birch silhouettes and every conifer to
/// <c>WinterSpruce_1</c>, so the street could not contain anything else. Naming
/// the rooted variant here gives the village its own deciduous species mix
/// (VIS-083), keeps conifers off the residential rows (VIS-084) and makes the
/// forest edge composable: landmark, near group, gaps (VIS-026).
/// A <c>Winter*</c> authored entry is also never dropped by the deep-Kara
/// <c>karaSuppression</c> filter, which only matches the summer prefixes.
/// </summary>
public static class AgentBFoliagePlan
{
    /// <summary>
    /// VIS-075/VIS-083: the authored hero families (<c>WinterOldBranch_1..5</c>,
    /// the third silhouette of each village species) exist only in the Blender
    /// generator until <c>agentb_foliage_kit.glb</c> is regenerated with
    /// <c>agent_b_foliage.py</c>. The composer throws on an unknown variant, so
    /// this flag stays false until that export has run; the ledger records the
    /// exact command. Flipping it true adds the placements without moving any
    /// existing root.
    /// </summary>
    /// <remarks>
    /// Deliberately not a <c>const</c>: this project builds with
    /// <c>TreatWarningsAsErrors</c>, and a constant false condition turns the
    /// gated block in <see cref="BuildEntries"/> into unreachable code
    /// (CS0162 = error). A static readonly property is the same single obvious
    /// switch and keeps the placements it guards compiled and reachable once
    /// the kit export has run.
    /// </remarks>
    public static bool HeroFamilyGeometryAvailable { get; } = false;

    internal static Vector2 ResolveYardWorkTree(Vector2 position, string variant)
    {
        if (!variant.Contains("Birch", System.StringComparison.Ordinal)) return position;
        // Two deterministic satellites grew through the later authored repair
        // cabinet and its working aisle. Move the whole rooted tree before
        // geometry, all LODs and stem collision are created; retain the seed
        // and every other planting. The new roots have 0.9 m clearance in the
        // actual world (yard-plant-probe-12b), beyond the north yard fence.
        if (position.DistanceSquaredTo(new(-30.183186f, 1.480051f)) < .000001f)
            return new(-29f, 6.4f);
        if (position.DistanceSquaredTo(new(-30.125542f, 2.240947f)) < .000001f)
            return new(-33f, 8.2f);
        return position;
    }

    /// <summary>
    /// The planting plan. Read on first use rather than in a field initializer:
    /// the arrays it is assembled from are declared further down this type, and
    /// static initializers run in declaration order, so an eager initializer
    /// would build the plan from a still-null <see cref="BaseEntries"/>.
    /// </summary>
    private static IReadOnlyList<(Vector2 Position, string Variant)>? _entries;

    public static IReadOnlyList<(Vector2 Position, string Variant)> Entries => _entries ??= BuildEntries();

    private static IReadOnlyList<(Vector2 Position, string Variant)> BuildEntries()
    {
        var entries = new List<(Vector2 Position, string Variant)>(BaseEntries);
        if (HeroFamilyGeometryAvailable)
        {
            entries.AddRange(HeroEntries);
            // Count-neutral silhouette upgrade: the same authored roots get the
            // third, genuinely different variant of their species (VIS-083), so
            // the street stops reading as two birch meshes repeated.
            for (var index = 0; index < entries.Count; index++)
            {
                foreach (var (position, variant) in HeroSubstitutions)
                {
                    if (entries[index].Item1.DistanceSquaredTo(position) < .000001f)
                    {
                        entries[index] = (position, variant);
                        break;
                    }
                }
            }
        }

        return entries;
    }

    // An array cannot be created with a target-typed `new()` (CS8752); the
    // element type is spelled out, exactly as HeroEntries below does it.
    private static readonly (Vector2 Position, string Variant)[] BaseEntries =
        new (Vector2 Position, string Variant)[]
    {
        // The arrival/main-street near-camera window intentionally has no
        // AgentB tree or shrub; low contact planting farther down the
        // shoulders remains.
        // Main street.
        // Keep the main-street sightline open; the parcel already has
        // authored trees farther down the road and needs no near occluder.
        // The branch-to-FAP road bends east here; keep this birch on the
        // village-side shoulder instead of inside the branch centreline.
        (new Vector2(4.3f, -16f), "WinterBirch_2"),
        // VIS-083: the bend by the wet ditch is a broad linden, not another
        // birch. Its low wide forks explain the shoulder water and its crown
        // reads at a different height from the trees on either side of it.
        (new Vector2(-6.2f, -24f), "WinterLinden_2"),
        (new Vector2(6.0f, -22f), "Shrub_3"),
        // The original Pine_3 sat inside the return-street first-person
        // envelope and became a route-wide black occluder; keep the parcel
        // landmark as a narrower birch at the authored shoulder edge.
        (new Vector2(-10.5f, -36f), "WinterBirch_1"),
        // VIS-084/VIS-083: a rowan against a house wall is the domestic avyl
        // tree — berries for the әби, shade for the yard — never a fir.
        (new Vector2(6.2f, -38f), "WinterRowan_1"),
        // VIS-083: willow on the damp shoulder by the ditch, at the scale the
        // ground actually wants. Conifers stay off every residential row.
        (new Vector2(-5.6f, -48f), "WinterWillow_1"),
        (new Vector2(6.4f, -46f), "Shrub_1"),
        // Babai yard dressing.
        (new Vector2(-27.5f, 4.5f), "Shrub_2"),
        (new Vector2(-33f, 3.5f), "WinterLinden_1"),
        (new Vector2(-21f, -4f), "GrassTuft_1"),
        (new Vector2(-26f, -3.5f), "GrassTuft_2"),
        (new Vector2(-30f, 5.5f), "Fern_1"),
        // FAP yard.
        // Remove the near birch for the doorway/depth sightline; the farther
        // birch remains on the clinic's north parcel.
        (new Vector2(34.5f, -33f), "WinterBirch_2"),
        (new Vector2(30f, -24.5f), "Shrub_1"),
        (new Vector2(33f, -27.5f), "GrassTuft_1"),
        // Return street.
        (new Vector2(-11.5f, -38f), "Shrub_2"),
        (new Vector2(10.8f, -36f), "Fern_2"),
        (new Vector2(-6.4f, -50f), "GrassTuft_2"),
        // Zirat: sparse, respectful planting.
        (new Vector2(3.5f, -74f), "WinterBirch_1"),
        (new Vector2(9.5f, -66.5f), "WinterLinden_2"),
        (new Vector2(10.5f, -73.5f), "Shrub_2"),
        (new Vector2(6.5f, -64.5f), "GrassTuft_1"),
        (new Vector2(8.5f, -70f), "GrassTuft_2"),
        // Zirat road shoulders.
        (new Vector2(-3.8f, -60f), "Shrub_2"),
        (new Vector2(4.2f, -78f), "WinterBirch_2"),
        (new Vector2(-3.6f, -84f), "WinterLinden_1"),
        // Kara transition — VIS-026: the one authored forest-edge sector.
        // It runs from the threshold at z -92 out to the debris marks near
        // z -119, about 47 m along the approach the C5_forest_edge / P06
        // street-end camera looks down. Composition, not density:
        //   * a threshold pair that reads as the village letting go: a tall fir
        //     on the left, a broad dark linden on the right;
        //   * a near group of unequal rooted trees between z -92 and z -99,
        //     which is where the repeated comb used to stand;
        //   * one landmark to steer by: the 24.5 m dominant at (5.0, -100),
        //     taller than anything planted near it;
        //   * two просветы: nothing rooted on the left between z -100 and
        //     z -112, nothing rooted on the right between z -101 and z -112.
        //     Only the low debris marks survive there, so the view passes into
        //     depth instead of stopping on a repeated trunk interval. The belt
        //     rows behind the sector and the settlement-envelope thicket are
        //     untouched, so neither window is a hole in the world: the walkable
        //     edge stays physically closed and the far line of trunks stays
        //     readable from the reverse side.
        // The instance count does not grow: these are the same authored roots
        // the plan already carried, re-specced to explicit geometry.
        (new Vector2(-4.5f, -92f), "WinterSpruce_3"),
        (new Vector2(4.6f, -94f), "WinterLinden_1"),
        (new Vector2(-5.2f, -99f), "WinterBirch_1"),
        (new Vector2(5.0f, -100f), "WinterSpruce_5"),
        (new Vector2(3.8f, -112f), "Stump_0"),
        (new Vector2(2.8f, -119f), "MossStone_0"),
        // Wet ditch vegetation along the main street.
        // The house connector bends through this shoulder; keep the sedge
        // outside its narrow path envelope.
        (new Vector2(-9.0f, -6f), "Sedge_0"),
        (new Vector2(4.5f, -19f), "Sedge_1"),
        (new Vector2(-4.6f, -28f), "Sedge_2"),
        (new Vector2(4.4f, -44f), "Sedge_0"),
        (new Vector2(-4.3f, -52f), "Fern_0"),

        // Authored low planting pass. These are contact-scale silhouettes for
        // wet shoulders, parcel edges and yard thresholds; they stay outside
        // the walkable road envelope and deliberately avoid adding another
        // repeated tree/cone family.
        (new Vector2(-4.0f, 18f), "Sedge_1"),
        (new Vector2(4.3f, 16f), "GrassTuft_0"),
        (new Vector2(-5.1f, 10f), "Fern_2"),
        (new Vector2(5.0f, 8f), "Sedge_2"),
        (new Vector2(-4.8f, -10f), "GrassTuft_1"),
        (new Vector2(-4.8f, -12f), "Fern_1"),
        (new Vector2(-5.0f, -18f), "Sedge_0"),
        (new Vector2(5.1f, -20f), "GrassTuft_2"),
        (new Vector2(-4.8f, -26f), "Fern_0"),
        (new Vector2(5.0f, -30f), "Sedge_1"),
        (new Vector2(-5.1f, -34f), "GrassTuft_0"),
        (new Vector2(5.2f, -40f), "Fern_2"),
        (new Vector2(-5.0f, -43f), "Sedge_2"),
        (new Vector2(5.1f, -49f), "GrassTuft_1"),

        // Perimeter homes: a few distant vertical anchors close the new
        // authored parcels without narrowing the first-person road window.
        // VIS-084: this row used to ask for "Spruce_1" and relied on the
        // composer's core rewrite to turn it into a linden. The village side
        // now asks for the broadleaf it means, so no conifer can survive here
        // if that rewrite ever moves.
        (new Vector2(-21.0f, 33.0f), "WinterMaple_1"),
        (new Vector2(21.5f, 35.0f), "WinterBirch_2"),
        (new Vector2(-22.0f, -11.0f), "WinterBirch_1"),
        (new Vector2(-19.0f, -45.0f), "WinterLinden_2"),
        (new Vector2(19.0f, -47.0f), "WinterBirch_1"),

        // Babai/әби yard contact: small irregular growth around the fence
        // line and firewood parcel, not across the house path.
        (new Vector2(-35.5f, 5.0f), "Fern_1"),
        (new Vector2(-34.0f, -5.0f), "GrassTuft_0"),
        (new Vector2(-22.0f, 5.0f), "Sedge_2"),
        (new Vector2(-20.5f, -2.5f), "MossStone_0"),

        // FAP side parcel: keep the branch and doorway legible while adding
        // damp shoulder texture on both lateral edges.
        (new Vector2(24.5f, -22.0f), "Sedge_0"),
        (new Vector2(26.0f, -30.5f), "GrassTuft_2"),
        (new Vector2(35.8f, -25.0f), "Fern_0"),
        (new Vector2(36.0f, -34.5f), "Sedge_2"),

        // Return and zirat transition: restrained planting preserves the
        // cemetery's quiet value instead of turning it into a hedge wall.
        (new Vector2(-12.5f, -43.5f), "GrassTuft_2"),
        (new Vector2(11.5f, -45.5f), "Sedge_1"),
        (new Vector2(-4.6f, -57.5f), "Fern_1"),
        (new Vector2(4.3f, -61.5f), "GrassTuft_0"),
        (new Vector2(-4.2f, -68.5f), "MossStone_0"),
        (new Vector2(4.1f, -81.5f), "Sedge_2"),

        // Kara threshold debris marks the change from village maintenance to
        // forest neglect without placing blockers on the route centreline.
        (new Vector2(-3.4f, -96.0f), "Fern_2"),
        (new Vector2(3.3f, -101.5f), "GrassTuft_1"),
        (new Vector2(-3.2f, -105.5f), "MossStone_0"),
        (new Vector2(-3.0f, -114.5f), "Stump_0")
    };

    /// <summary>
    /// VIS-075 (H3-1): the rare old-growth accents. Four rooted trees with long
    /// unsettling limbs, placed only in the forest register (west wood mass and
    /// beyond the authored threshold sector) and never in the village core, so
    /// one silhouette breaks the rhythm without turning the whole wood into a
    /// fantasy organism. Against the near forest hero placements of the sector
    /// and the two rows the player can actually reach, four accents stay well
    /// under the ten percent ceiling the card sets, and the VIS-026 belt carve
    /// removes more tall stems than this adds.
    /// Gated by <see cref="HeroFamilyGeometryAvailable"/> because
    /// <c>WinterOldBranch_*</c> geometry does not exist until the kit export
    /// that accompanies this change has run.
    /// </summary>
    private static readonly (Vector2 Position, string Variant)[] HeroEntries = new (Vector2, string)[]
    {
        (new Vector2(-18.5f, -112.0f), "WinterOldBranch_2"),
        (new Vector2(19.0f, -121.0f), "WinterOldBranch_4"),
        (new Vector2(-52.0f, -30.0f), "WinterOldBranch_1"),
        (new Vector2(-47.0f, 12.0f), "WinterOldBranch_3"),
    };

    /// <summary>
    /// VIS-083: species-variant upgrades for roots that already exist, so the
    /// village families are reassembled without planting another tree anywhere.
    /// </summary>
    private static readonly (Vector2 Position, string Variant)[] HeroSubstitutions = new (Vector2, string)[]
    {
        (new Vector2(4.3f, -16f), "WinterBirch_3"),
        (new Vector2(-33f, 3.5f), "WinterLinden_3"),
        (new Vector2(6.2f, -38f), "WinterRowan_3"),
        (new Vector2(-5.6f, -48f), "WinterWillow_3"),
        (new Vector2(-5.2f, -99f), "WinterBirch_3"),
    };

    /// <summary>
    /// VIS-026: the two authored просветы on the south belt, expressed as data so
    /// the composer only has to ask one question. Rows 3-6, never rows 0-2 (the
    /// far line that closes the skyline) and never rows 7-8 (the row the player
    /// can walk up to, whose stems carry the boundary collision). Windows are
    /// 8 m wide inside the 46 m authored sector.
    /// </summary>
    public static bool IsForestWindow(Vector2 point, int row)
    {
        if (row < 3 || row > 6) return false;
        // Only the south edge of the ring, within the band the street-end
        // camera reads as the wall. The vertical east/west edges and the north
        // edge fall outside this window band.
        if (point.Y > -132f || point.Y < -150f) return false;
        return point.X >= -10f && point.X <= -2f
            || point.X >= 8f && point.X <= 16f;
    }

    /// <summary>
    /// What stands in a window instead of a tall stem: knee-high regrowth, one
    /// stone and one fallen line, so the gap stays forest floor and not a
    /// cleared parcel. Deterministic on the coordinate alone; it consumes no
    /// shared random stream, so no other planting moves.
    /// </summary>
    public static string WindowUnderstory(Vector2 point)
    {
        var phase = Mathf.FloorToInt(Mathf.Abs(point.X * 13.7f + point.Y * 5.3f)) % 5;
        return phase switch
        {
            0 => "Fern_0",
            1 => "Sedge_2",
            2 => "Shrub_2",
            3 => "MossStone_0",
            _ => "GrassTuft_1",
        };
    }
}
