using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    /// <summary>
    /// A01.1 pilot (review 2026-09-28): ambiguous eye-like details on yard
    /// objects and one deliberately menacing forest-edge fragment beyond the
    /// north fence. Pareidolia of real construction - knot holes, hollows and
    /// dark recesses - never literal eyeballs, stickers or a new creature.
    /// Presentation only: no collision, no runtime state, no story flags.
    /// </summary>
    private void BuildWatchingFormsPilot(Node3D core)
    {
        var forms = new Node3D { Name = "WatchingFormsPilot" };
        forms.SetMeta("presentationOnly", true);
        forms.SetMeta("visualOnly", true);
        forms.SetMeta("presentationOwner", nameof(Act1ConnectedWorld));
        forms.SetMeta("a01_1_pilot", "eye-like pareidolia details + menacing edge fragment; see docs/tasktracker/review_2026-09-28/05_art_characters_assets.md A01.1");
        core.AddChild(forms);

        BuildWorkshopKnotPair(forms);
        BuildYardSnagWithHollows(forms);
        BuildMenacingEdgeFragment(forms);
    }

    // The workshop's own back boards: two uneven knot holes at eye height.
    // Up close they read as honest wood defects; from the yard gate the pair
    // reads as something looking back. Sizes and tilt are deliberately uneven.
    private static void BuildWorkshopKnotPair(Node3D forms)
    {
        var yard = forms.GetParent().FindChild("YardRepairCorner", true, false) as Node3D;
        if (yard is null) return;
        Knot(yard, "WorkshopKnotLeft", new(-.88f, 1.52f, -.815f), .052f, "241d16", tiltDegrees: 7);
        Knot(yard, "WorkshopKnotRight", new(-.74f, 1.44f, -.815f), .039f, "2a221a", tiltDegrees: -11);
        // A single lower knot on the trestle crossbar: the same motif, alone,
        // so the family never repeats as one identical stamp.
        Knot(yard, "TrestleSingleKnot", new(1.82f, .623f, .623f), .026f, "262019", tiltDegrees: 4);
    }

    // A dead standing snag inside the north fence line: crooked, dark, with
    // two asymmetric hollows facing the yard. Real geometry for the hollows,
    // because this one is inspected from the EX13 route a few metres away.
    private static void BuildYardSnagWithHollows(Node3D forms)
    {
        var anchor = new Vector3(-34.55f, 0f, 5.85f);
        var snag = new Node3D { Name = "YardDeadSnag", RotationDegrees = new(0, 18, 0) };
        snag.Position = GroundedYardPoint(anchor);
        snag.SetMeta("visualOnly", true);
        forms.AddChild(snag);
        // Three chained trunk segments with alternating lean read as a broken,
        // watching figure; the crown stays bare - it is dead, not leafy.
        var lean = new Vector3(.06f, 0, .045f);
        var segmentHeight = new[] { 2.6f, 2.2f, 1.5f };
        var basePoint = Vector3.Zero;
        for (var index = 0; index < segmentHeight.Length; index++)
        {
            var height = segmentHeight[index];
            var radius = .155f - index * .042f;
            var segment = new MeshInstance3D
            {
                Name = $"SnagTrunk{index}",
                Position = basePoint + Vector3.Up * (height * .5f),
                RotationDegrees = new(index % 2 == 0 ? lean.Z : -lean.Z, 0, index % 2 == 0 ? lean.X : -lean.X),
                Mesh = new CylinderMesh { TopRadius = radius * .82f, BottomRadius = radius, Height = height, RadialSegments = 10, Rings = 1 },
                MaterialOverride = PainterlyMaterialLibrary.ForColor(index == 0 ? "463c31" : "3d352b", "wood")
            };
            snag.AddChild(segment);
            basePoint += Vector3.Up * height + (index % 2 == 0 ? lean : -lean) * .8f;
        }
        // Two crooked stub branches keep the silhouette off-balance.
        Stub(snag, basePoint with { Y = basePoint.Y - 1.1f }, 0.62f, 38, "SnagBranchHigh");
        Stub(snag, new Vector3(0, 1.35f, 0), 0.48f, -52, "SnagBranchLow");
        // The hollow pair: an oval low wound and a small round hole higher and
        // to the side. Both face the yard (the snag root faces +Z toward it).
        Hollow(snag, "SnagHollowLow", new(.07f, 1.62f, -.132f), .085f, .125f, "171310");
        Hollow(snag, "SnagHollowHigh", new(-.045f, 2.51f, -.128f), .048f, .052f, "1b1512");
    }

    // The edge fragment beyond the north fence, seen from the yard and from
    // the house path: three tall dark trunks leaning slightly over the
    // village, a sparse second row behind them, and a dark under-storey line
    // that deepens the base so no sky gap shows an outside edge.
    private static void BuildMenacingEdgeFragment(Node3D forms)
    {
        var fragment = new Node3D { Name = "MenacingEdgeFragment" };
        fragment.SetMeta("visualOnly", true);
        forms.AddChild(fragment);
        var trunkSpec = new (Vector3 at, float height, float leanX, float leanZ, float yaw)[]
        {
            (new(-29.6f, 0, 7.9f), 9.6f, .10f, -.06f, 24),
            (new(-31.9f, 0, 9.1f), 10.4f, -.07f, .09f, -18),
            (new(-33.4f, 0, 7.3f), 8.8f, .05f, .12f, 41),
            (new(-28.4f, 0, 10.6f), 7.2f, -.09f, -.10f, -35),
            (new(-32.2f, 0, 12.3f), 8.1f, .08f, .07f, 12),
        };
        MeshInstance3D? nearestTrunk = null;
        foreach (var (at, height, leanX, leanZ, yaw) in trunkSpec)
        {
            var trunk = new MeshInstance3D
            {
                Name = "EdgeTrunk_" + Mathf.RoundToInt(at.X * 10) + "_" + Mathf.RoundToInt(at.Z * 10),
                Position = GroundedYardPoint(at) + Vector3.Up * (height * .5f),
                RotationDegrees = new(Mathf.RadToDeg(leanZ), yaw, Mathf.RadToDeg(leanX)),
                Mesh = new CylinderMesh { TopRadius = .13f, BottomRadius = .30f, Height = height, RadialSegments = 9, Rings = 1 },
                MaterialOverride = PainterlyMaterialLibrary.ForColor("332c24", "wood")
            };
            trunk.SetMeta("visualOnly", true);
            fragment.AddChild(trunk);
            nearestTrunk ??= trunk;
        }
        // Crooked bare crowns: a few angled branches per trunk, uneven by
        // position hash so no two silhouettes repeat.
        for (var index = 0; index < trunkSpec.Length; index++)
        {
            var (at, height, _, _, yaw) = trunkSpec[index];
            var crown = new Node3D
            {
                Name = $"EdgeCrown_{index}",
                // Follow the trunk's own lean so the bare crown stays attached.
                Position = GroundedYardPoint(at) + Vector3.Up * (height * .74f)
                    + new Vector3(trunkSpec[index].leanX, 0, trunkSpec[index].leanZ) * (height * .38f),
                RotationDegrees = new(0, yaw, 0)
            };
            fragment.AddChild(crown);
            var branches = 3 + index % 2;
            for (var branch = 0; branch < branches; branch++)
            {
                var angle = (branch * Mathf.Tau / branches) + VegetationHash(at, branch + 1) * 2.4f;
                var arm = new MeshInstance3D
                {
                    Name = $"EdgeBranch{branch}",
                    Position = new(Mathf.Sin(angle) * .34f, .18f - branch * .22f, Mathf.Cos(angle) * .34f),
                    RotationDegrees = new(Mathf.RadToDeg(Mathf.Cos(angle) * .85f), Mathf.RadToDeg(-angle), Mathf.RadToDeg(Mathf.Sin(angle) * .85f)),
                    Mesh = new CylinderMesh { TopRadius = .018f, BottomRadius = .05f, Height = 1.5f + branch * .35f, RadialSegments = 6, Rings = 1 },
                    MaterialOverride = PainterlyMaterialLibrary.ForColor("2e2720", "wood")
                };
                crown.AddChild(arm);
            }
        }
        // One distant hollow pair in the nearest trunk, angled toward the yard:
        // far enough to stay ambiguous, close enough to be noticed once.
        if (nearestTrunk is not null)
        {
            Hollow(nearestTrunk, "EdgeHollowPairA", new(.06f, .35f, -.235f), .07f, .09f, "14100d");
            Hollow(nearestTrunk, "EdgeHollowPairB", new(-.07f, .57f, -.235f), .05f, .06f, "14100d");
        }
        // The dark under-storey line: low thin masses that ground the trunks
        // and keep a continuous dark band at the fence horizon.
        foreach (var (x, z, width) in new[] { (-28.6f, 7.2f, 2.6f), (-31.2f, 8.2f, 3.4f), (-33.8f, 7.0f, 2.8f) })
        {
            var mass = new MeshInstance3D
            {
                Name = $"EdgeUnderstorey_{x:0.0}",
                Position = GroundedYardPoint(new(x, 0, z)) + Vector3.Up * .42f,
                RotationDegrees = new(0, VegetationHash(new(x, 0, z), 3.3f) * 60f, 0),
                Mesh = new BoxMesh { Size = new(width, .84f, .9f) },
                MaterialOverride = PainterlyMaterialLibrary.ForColor("232019", "wood")
            };
            mass.SetMeta("visualOnly", true);
            fragment.AddChild(mass);
        }
    }

    // Slightly raised grain surrounds a dark, visually recessed knot. The
    // backing board stays intact, including its existing collision owner.
    private static void Knot(Node3D parent, string name, Vector3 localAt, float radius, string colour, float tiltDegrees)
    {
        var knot = new Node3D { Name = name, Position = localAt, RotationDegrees = new(0, 0, tiltDegrees) };
        knot.SetMeta("visualOnly", true);
        knot.SetMeta("a01_1_motif", "irregular recessed wood knot pareidolia");
        parent.AddChild(knot);
        Recess(knot, radius, radius * .86f, colour, "6b5a45", .18f);
    }

    // A bark lip and dark throat suggest depth without cutting the existing
    // structural trunk mesh or adding a collision seam at the route edge.
    private static void Hollow(Node3D parent, string name, Vector3 localAt, float width, float height, string colour)
    {
        var hollow = new Node3D { Name = name, Position = localAt };
        hollow.SetMeta("visualOnly", true);
        hollow.SetMeta("a01_1_motif", "irregular bark wound pareidolia");
        parent.AddChild(hollow);
        Recess(hollow, width * .5f, height * .5f, colour, "4c4034", name.Length * .37f);
    }

    private static void Recess(Node3D parent, float rx, float ry, string dark, string bark, float variation)
    {
        const int sides = 13;
        Vector3 Ring(int i, float radius, float depth)
        {
            var angle = Mathf.Tau * i / sides;
            var irregularity = 1f + .055f * Mathf.Sin(3f * angle + variation)
                + .035f * Mathf.Sin(7f * angle - variation);
            return new Vector3(Mathf.Cos(angle) * rx * radius * irregularity,
                Mathf.Sin(angle) * ry * radius * irregularity, -depth);
        }

        using var lip = new SurfaceTool();
        lip.Begin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < sides; i++)
        {
            var outer = Ring(i, 1.48f, .018f);
            var outerNext = Ring(i + 1, 1.48f, .018f);
            var crest = Ring(i, 1f, .051f);
            var crestNext = Ring(i + 1, 1f, .051f);
            var throat = Ring(i, .61f, .018f);
            var throatNext = Ring(i + 1, .61f, .018f);
            lip.AddVertex(outer); lip.AddVertex(crest); lip.AddVertex(outerNext);
            lip.AddVertex(crest); lip.AddVertex(crestNext); lip.AddVertex(outerNext);
            lip.AddVertex(crest); lip.AddVertex(throat); lip.AddVertex(crestNext);
            lip.AddVertex(throat); lip.AddVertex(throatNext); lip.AddVertex(crestNext);
        }
        lip.GenerateNormals();
        parent.AddChild(new MeshInstance3D
        {
            Name = "RaisedBarkLip", Mesh = lip.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor(bark, "wood")
        });

        using var bottom = new SurfaceTool();
        bottom.Begin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < sides; i++)
        {
            bottom.AddVertex(new Vector3(0, 0, -.019f));
            bottom.AddVertex(Ring(i + 1, .62f, .019f));
            bottom.AddVertex(Ring(i, .62f, .019f));
        }
        bottom.GenerateNormals();
        parent.AddChild(new MeshInstance3D
        {
            Name = "DarkRecess", Mesh = bottom.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor(dark, "wood")
        });
    }

    private static void Stub(Node3D parent, Vector3 at, float length, float yawDegrees, string name)
    {
        var stub = new MeshInstance3D
        {
            Name = name,
            Position = at,
            RotationDegrees = new(0, yawDegrees, 62),
            Mesh = new CylinderMesh { TopRadius = .02f, BottomRadius = .05f, Height = length, RadialSegments = 7, Rings = 1 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("3a3229", "wood")
        };
        parent.AddChild(stub);
    }
}
