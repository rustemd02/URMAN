using Godot;
using System.Collections.Generic;

namespace Urman.Experiments.AgentBAct1;

/// <summary>
/// Deterministic foliage planting plan for the production exterior layer.
/// Ported 1:1 from the verified Agent B capture run (45/45 frames,
/// 11/11 waypoints): positions are world-space (x, z), variant keys map
/// to the AgentB_FoliageKit template families.
/// </summary>
public static class AgentBFoliagePlan
{
    public static IReadOnlyList<(Vector2 Position, string Variant)> Entries { get; } = new List<(Vector2, string)>
    {
        // The arrival/main-street near-camera window intentionally has no
        // AgentB tree or shrub; low contact planting farther down the
        // shoulders remains.
        // Main street.
        // Keep the main-street sightline open; the parcel already has
        // authored trees farther down the road and needs no near occluder.
        // The branch-to-FAP road bends east here; keep this birch on the
        // village-side shoulder instead of inside the branch centreline.
        (new Vector2(4.3f, -16f), "Birch_1"),
        (new Vector2(-6.2f, -24f), "Birch_2"),
        (new Vector2(6.0f, -22f), "Shrub_3"),
        // The original Pine_3 sat inside the return-street first-person
        // envelope and became a route-wide black occluder; keep the parcel
        // landmark as a narrower birch at the authored shoulder edge.
        (new Vector2(-10.5f, -36f), "Birch_3"),
        (new Vector2(6.2f, -38f), "Birch_3"),
        (new Vector2(-5.6f, -48f), "Birch_1"),
        (new Vector2(6.4f, -46f), "Shrub_1"),
        // Babai yard dressing.
        (new Vector2(-27.5f, 4.5f), "Shrub_2"),
        (new Vector2(-33f, 3.5f), "Birch_2"),
        (new Vector2(-21f, -4f), "GrassTuft_1"),
        (new Vector2(-26f, -3.5f), "GrassTuft_2"),
        (new Vector2(-30f, 5.5f), "Fern_1"),
        // FAP yard.
        // Remove the near birch for the doorway/depth sightline; the farther
        // birch remains on the clinic's north parcel.
        (new Vector2(34.5f, -33f), "Birch_1"),
        (new Vector2(30f, -24.5f), "Shrub_1"),
        (new Vector2(33f, -27.5f), "GrassTuft_1"),
        // Return street.
        (new Vector2(-11.5f, -38f), "Shrub_2"),
        (new Vector2(10.8f, -36f), "Fern_2"),
        (new Vector2(-6.4f, -50f), "GrassTuft_2"),
        // Zirat: sparse, respectful planting.
        (new Vector2(3.5f, -74f), "Birch_3"),
        (new Vector2(9.5f, -66.5f), "Birch_1"),
        (new Vector2(10.5f, -73.5f), "Shrub_2"),
        (new Vector2(6.5f, -64.5f), "GrassTuft_1"),
        (new Vector2(8.5f, -70f), "GrassTuft_2"),
        // Zirat road shoulders.
        (new Vector2(-3.8f, -60f), "Shrub_2"),
        (new Vector2(4.2f, -78f), "Birch_2"),
        (new Vector2(-3.6f, -84f), "Birch_2"),
        // Kara transition.
        (new Vector2(-4.5f, -92f), "Spruce_1"),
        (new Vector2(4.6f, -94f), "Spruce_2"),
        (new Vector2(-5.2f, -99f), "Birch_3"),
        (new Vector2(5.0f, -100f), "Birch_1"),
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
        (new Vector2(-21.0f, 33.0f), "Spruce_1"),
        (new Vector2(21.5f, 35.0f), "Birch_3"),
        (new Vector2(-22.0f, -11.0f), "Birch_2"),
        (new Vector2(-19.0f, -45.0f), "Birch_1"),
        (new Vector2(19.0f, -47.0f), "Birch_2"),

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
}
