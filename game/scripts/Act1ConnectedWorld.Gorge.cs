using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Relayout v3 (author, 2026-10-01): no river. The village ends in the south at a wide,
/// deep gorge; the only way to the Kara-Urman forest is an old suspension bridge that
/// barely holds. Aidar crosses it; on his way back after the night of the blizzard it
/// gives way behind him (world prop <see cref="SuspensionBridgeStateId"/>). The terrain
/// cut lives in <see cref="AgentBAct1HeightField.RiverChannel"/>; this part dresses the
/// gorge, fences both rims and builds the bridge.
/// </summary>
public partial class Act1ConnectedWorld
{
    internal const string SuspensionBridgeStateId = "act1/suspension-bridge";
    internal const float SuspensionBridgeX = 0f;
    /// <summary>VIS-015: the vertical gap a bank-to-deck joint is closed to, in metres.
    /// The card's own target; measured per bearing and printed as
    /// <c>act1-bridge-joint … gapAfter</c>, never assumed.</summary>
    internal const float RimJointTargetGap = .01f;
    /// <summary>Deck and collision strip are cut into this many rigid sections that
    /// <see cref="SuspensionBridgeDynamics"/> moves as one; both stay glued together.</summary>
    internal const int SuspensionDeckSections = 14;
    private Node3D? _suspensionIntact;
    private Node3D? _suspensionBroken;
    private StaticBody3D? _suspensionDeckBody;
    private StaticBody3D? _suspensionGapBlocker;
    private bool _suspensionVisitedFar;
    private bool _suspensionCollapsing;
    private int _suspensionDeckPlanks;
    private int _suspensionPatchBoards;
    private SuspensionBridgeDynamics? _suspensionDynamics;
    private readonly Dictionary<Node3D, Transform3D> _suspensionRest = new();

    internal static float GorgeCentreZ(float x) => (float)AgentBAct1HeightField.RiverMeander(x);
    internal static float GorgeNearRim(float x) => GorgeCentreZ(x) + (float)AgentBAct1HeightField.GorgeHalfWidth;
    internal static float GorgeFarRim(float x) => GorgeCentreZ(x) - (float)AgentBAct1HeightField.GorgeHalfWidth;

    /// <summary>A low faceted boulder: a coarse sphere with every corner pushed in or out by a
    /// hash of its position (so seams stay closed), flat-shaded like the rest of the kit.</summary>
    private static ArrayMesh GorgeRockMesh(int seed)
    {
        var arrays = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 7, Rings = 4 }.GetMeshArrays();
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        for (var i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            var h = Mathf.Sin(Mathf.Snapped(v.X, .001f) * 12.9898f + Mathf.Snapped(v.Y, .001f) * 78.233f
                + Mathf.Snapped(v.Z, .001f) * 37.719f + seed * 1.618f) * 43758.547f;
            h -= Mathf.Floor(h);
            vertices[i] = v * (.72f + .5f * h);
        }
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = default;
        arrays[(int)Mesh.ArrayType.Tangent] = default;
        using var raw = new ArrayMesh();
        raw.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        using var surface = new SurfaceTool();
        surface.CreateFrom(raw, 0);
        surface.Deindex();
        surface.GenerateNormals();
        return surface.Commit();
    }

    private void AddVillageGorge(Node3D core)
    {
        var gorge = new Node3D { Name = "VillageForestGorge" };
        gorge.SetMeta("presentationRole", "deep gorge between the village and the Kara-Urman forest; one suspension bridge");
        gorge.SetMeta("collisionOwner", "terrain walls; rim fences on layer 2");
        core.AddChild(gorge);
        var rock = PainterlyMaterialLibrary.ForColor("4a4641", "stone");
        // One frozen stream winds along the bottom, following the bed; rock shelves break the walls.
        using (var bed = new Curve3D { BakeInterval = .5f })
        {
            for (var x = AgentBAct1HeightField.MinX + 2f; x <= AgentBAct1HeightField.MaxX - 2f; x += 2.5f)
                bed.AddPoint(new Vector3(x, 0f, GorgeCentreZ(x) + .8f * Mathf.Sin(x / 7f)));
            var stream = AddVisualLandformSurface(gorge, "GorgeStream", 1.5f, .03f, bed.GetBakedLength(),
                new(0f, .04f, 0f), "5c6f74", "ice", 0f, true, bed);
            stream.SetMeta("visualOnly", true);
        }
        for (var x = AgentBAct1HeightField.MinX + 2f; x <= AgentBAct1HeightField.MaxX - 2f; x += 5f)
        {
            var z = GorgeCentreZ(x);
            foreach (var side in new[] { -1f, 1f })
            {
                if (Mathf.Abs(Mathf.Sin(x * .37f + side)) < .45f) continue;
                // Rock breaks out of the wall partway down: a faceted outcrop half buried in the
                // slope and tilted with it, not a box laid on top of the snow.
                var depth = .55f + .25f * Mathf.Sin(x * 1.7f + side);
                var wallZ = z + side * (float)AgentBAct1HeightField.GorgeHalfWidth * depth;
                var size = new Vector3(1.5f + .5f * Mathf.Sin(x * 2.3f), .8f + .25f * Mathf.Cos(x), 1.1f + .3f * Mathf.Sin(x * .9f));
                var shelf = new MeshInstance3D
                {
                    Name = $"GorgeRock_{x:0}_{(side < 0 ? "far" : "near")}",
                    Position = new Vector3(x, (float)AgentBAct1HeightField.Ground(x, wallZ) - size.Y * .35f, wallZ),
                    RotationDegrees = new Vector3(side * 34f, 47f * Mathf.Sin(x), 0),
                    Scale = size, Mesh = GorgeRockMesh((int)(x * 7 + side * 3)), MaterialOverride = rock
                };
                shelf.SetMeta("visualOnly", true);
                gorge.AddChild(shelf);
            }
        }
        // Rim fences keep the edge readable and keep a walker out of the cut. Both rims
        // leave a gap only where the bridge deck actually passes: the rope-wall
        // collision sits at ±0.8 m, so the fence is clipped at the deck edge and tied
        // into it with a short return leg. Skipping the whole 4 m segment (the old
        // behaviour) left a ~4.0 m hole in the fence — a ~1.1 m open strip on each side
        // of the 1.4 m deck where a walker could step around the bridge and fall in.
        const float deckHalfWidth = .8f;
        var nearDeckZ = GorgeNearRim(SuspensionBridgeX) + .8f;   // first deck section
        var farDeckZ = GorgeFarRim(SuspensionBridgeX) - .8f;
        var fence = new StaticBody3D { Name = "GorgeRimFenceBody", CollisionLayer = 2, CollisionMask = 0 };
        fence.SetMeta("collisionOwner", "gorge-rim-fence");
        gorge.AddChild(fence);
        foreach (var near in new[] { true, false })
        {
            var offset = near ? 1.6f : -1.6f;
            var start = AgentBAct1HeightField.MinX + 2f;
            for (var x = start; x < AgentBAct1HeightField.MaxX - 2f; x += 4f)
            {
                var x1 = Mathf.Min(x + 4f, AgentBAct1HeightField.MaxX - 2f);
                // Run pieces outside the deck gap; a run fully inside it yields none.
                foreach (var (from, to, suffix) in ClipSuspensionFenceGap(x, x1, deckHalfWidth))
                {
                    var a = new Vector3(from, 0, (near ? GorgeNearRim(from) : GorgeFarRim(from)) + offset);
                    var b = new Vector3(to, 0, (near ? GorgeNearRim(to) : GorgeFarRim(to)) + offset);
                    var end = near ? "near" : "far";
                    AddVisualFenceRun(gorge, $"GorgeRimFence_{end}_{x:0}{suffix}", a, b, false);
                    var mid = (a + b) * .5f; mid.Y = AgentBAct1HeightField.CollisionGround(mid.X, mid.Z) + .6f;
                    fence.AddChild(new CollisionShape3D
                    {
                        Name = $"GorgeRimFenceShape_{end}_{x:0}{suffix}", Position = mid,
                        Rotation = new Vector3(0, Mathf.Atan2(b.X - a.X, b.Z - a.Z), 0),
                        Shape = new BoxShape3D { Size = new Vector3(.18f, 1.2f, a.DistanceTo(b) + .1f) }
                    });
                }
            }
            // Return legs close the corner between the fence line and the bridge:
            // each runs in Z at x = ±0.8 from the fence line to 0.3 m past the deck
            // end, overlapping the first rope-wall box, and reads as village fencing
            // joined to the bridge anchor posts.
            for (var side = -1; side <= 1; side += 2)
            {
                var x = SuspensionBridgeX + side * deckHalfWidth;
                var fenceZ = (near ? GorgeNearRim(x) : GorgeFarRim(x)) + offset;
                var deckZ = near ? nearDeckZ - .3f : farDeckZ + .3f;
                var a = new Vector3(x, 0, fenceZ);
                var b = new Vector3(x, 0, deckZ);
                var end = near ? "near" : "far";
                AddVisualFenceRun(gorge, $"GorgeRimFenceReturn_{end}_{(side < 0 ? "w" : "e")}", a, b, false);
                var mid = (a + b) * .5f; mid.Y = AgentBAct1HeightField.CollisionGround(mid.X, mid.Z) + .6f;
                fence.AddChild(new CollisionShape3D
                {
                    Name = $"GorgeRimFenceReturnShape_{end}_{(side < 0 ? "w" : "e")}", Position = mid,
                    Rotation = new Vector3(0, Mathf.Atan2(b.X - a.X, b.Z - a.Z), 0),
                    Shape = new BoxShape3D { Size = new Vector3(.18f, 1.2f, a.DistanceTo(b) + .1f) }
                });
            }
        }
        BuildSuspensionBridge(gorge);
        GD.Print($"act1-gorge: halfWidth={AgentBAct1HeightField.GorgeHalfWidth} depth={AgentBAct1HeightField.GorgeDepth} bridge=suspension@({SuspensionBridgeX},{GorgeCentreZ(SuspensionBridgeX):0.0})");
        GD.Print($"act1-gorge-bridge: planks={_suspensionDeckPlanks} patchBoards={_suspensionPatchBoards} apronBoards=10 sections={SuspensionDeckSections} deckHoles=0");
    }

    /// <summary>
    /// Splits one rim-fence run around the suspension-deck opening
    /// (SuspensionBridgeX ± <paramref name="halfWidth"/>). Runs that do not reach
    /// the opening are returned whole with no suffix; a run that straddles it is
    /// returned as its outside pieces, so the fence ends flush with the deck
    /// instead of leaving the whole 4 m segment open. A run fully inside the
    /// opening yields nothing.
    /// </summary>
    private static IEnumerable<(float From, float To, string Suffix)> ClipSuspensionFenceGap(
        float from, float to, float halfWidth)
    {
        var gapMin = SuspensionBridgeX - halfWidth;
        var gapMax = SuspensionBridgeX + halfWidth;
        if (to <= gapMin || from >= gapMax)
        {
            yield return (from, to, string.Empty);
            yield break;
        }
        var split = from < gapMin && to > gapMax;
        if (from < gapMin) yield return (from, gapMin, split ? "_w" : string.Empty);
        if (to > gapMax) yield return (gapMax, to, split ? "_e" : string.Empty);
    }

    private static float SuspensionDeckHeight(float t, float nearY, float farY) =>
        Mathf.Lerp(nearY, farY, t) - .75f * Mathf.Sin(Mathf.Pi * t);

    /// <summary>A board or beam that follows the deck slope (AddVisualBox has no pitch).</summary>
    private static MeshInstance3D AddTiltedBoard(Node3D parent, string name, Vector3 size, Vector3 position,
        Vector3 rotation, string colour, string surface)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Mesh = RuralPropGeometry.Box(size),
            MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, surface)
        };
        mesh.SetMeta("visualOnly", true);
        parent.AddChild(mesh);
        return mesh;
    }

    private void BuildSuspensionBridge(Node3D gorge)
    {
        var x = SuspensionBridgeX;
        var nearZ = GorgeNearRim(x) + .8f;
        var farZ = GorgeFarRim(x) - .8f;
        var nearY = (float)AgentBAct1HeightField.CollisionGround(x, nearZ) + .12f;
        var farY = (float)AgentBAct1HeightField.CollisionGround(x, farZ) + .12f;
        var wood = PainterlyMaterialLibrary.ForColor("5c4a38", "wood");
        var old = PainterlyMaterialLibrary.ForColor("4a3d30", "wood");
        var rope = PainterlyMaterialLibrary.ForColor("9c8a6a", "fabric");

        var root = new Node3D { Name = "SuspensionBridge" };
        root.SetMeta("presentationRole", "old rope-and-plank suspension bridge over the gorge; barely holds");
        gorge.AddChild(root);
        // Anchor posts on both rims stay after the collapse.
        foreach (var (z, y, end) in new[] { (nearZ, nearY, "near"), (farZ, farY, "far") })
            foreach (var side in new[] { -1f, 1f })
                AddVisualBox(root, $"SuspensionPost_{end}_{(side < 0 ? "w" : "e")}", new(.24f, 2.0f, .24f), new(x + side * .95f, y + .9f, z), "574d3d", "wood",
                    rollDegrees: side * 3f);
        // Anchor furniture: a sill beam sits under the deck end (its top at the board
        // underside), rope ties run from the post heads to the hand-rope eyes and knee
        // braces reach into the first boards. They close the notch between the towers
        // and the deck and stay on the rims if the span falls.
        foreach (var (z, y, end, inward) in new[] { (nearZ, nearY, "near", -1f), (farZ, farY, "far", 1f) })
        {
            AddVisualBox(root, $"SuspensionAnchorSill_{end}", new(2.4f, .18f, .34f), new(x, y - .12f, z), "4a3d30", "wood");
            foreach (var side in new[] { -1f, 1f })
            {
                AddRavineLog(root, $"SuspensionAnchorTie_{end}_{(side < 0 ? "w" : "e")}",
                    new Vector3(x + side * .95f, y + 1.58f, z), new Vector3(x + side * .78f, y + .98f, z), .045f, "9c8a6a");
                AddRavineLog(root, $"SuspensionAnchorBrace_{end}_{(side < 0 ? "w" : "e")}",
                    new Vector3(x + side * .95f, y + .60f, z), new Vector3(x + side * .55f, y + .01f, z + inward * .55f), .07f, "574d3d");
            }
        }

        _suspensionIntact = new Node3D { Name = "SuspensionBridgeIntact" };
        root.AddChild(_suspensionIntact);
        _suspensionDeckBody = new StaticBody3D { Name = "SuspensionDeckBody", CollisionLayer = 1, CollisionMask = 0 };
        _suspensionDeckBody.SetMeta("footstepSurface", "wood");
        _suspensionDeckBody.SetMeta("collisionContract",
            "one continuous strip: 14 animated section boxes overlapping 0.14 m, two static apron ramps overlapping the rims by 0.35 m; " +
            "every box is 0.10 m thick and set so its top equals the 0.06 m plank top (VIS-015)");
        _suspensionIntact.AddChild(_suspensionDeckBody);
        var length = nearZ - farZ;
        var spacing = .36f;
        var planks = Mathf.CeilToInt(length / spacing);
        var patches = 0;
        for (var i = 0; i < planks; i++)
        {
            var t = (i + .5f) / planks;
            var z = Mathf.Lerp(nearZ, farZ, t);
            var y = SuspensionDeckHeight(t, nearY, farY);
            var loose = i % 7 == 3;
            var patch = i % 11 == 7;   // reads as a replaced board, not as a hole
            if (patch) patches++;
            _suspensionIntact.AddChild(new MeshInstance3D
            {
                Name = patch ? $"SuspensionPatch_{i:00}" : $"SuspensionPlank_{i:00}",
                Position = new Vector3(x + (loose ? .10f : 0f), patch ? y - .012f : y, z),
                RotationDegrees = new Vector3(0, loose ? 6f : (i % 3 - 1) * 2f, loose ? 2f : 0f),
                // 0.38 m deep on a 0.36 m rhythm: neighbouring boards overlap, so the
                // walking surface never shows a slit to the gorge or to the sky.
                Mesh = new BoxMesh { Size = new Vector3(1.4f, .06f, .38f) },
                MaterialOverride = patch || i % 4 == 0 ? old : wood
            });
            if (!patch) continue;
            // The repair board is narrower and pinned by two cross battens: it keeps
            // the "held together by habit" reading while staying fully walkable.
            AddVisualBox(_suspensionIntact, $"SuspensionBatten_{i:00}_a", new(1.42f, .045f, .13f),
                new(x, y + .012f, z - .17f), "3f3428", "wood", yawDegrees: 3f * Mathf.Sin(i));
            AddVisualBox(_suspensionIntact, $"SuspensionBatten_{i:00}_b", new(1.42f, .045f, .13f),
                new(x, y + .012f, z + .17f), "3f3428", "wood", yawDegrees: -3f * Mathf.Cos(i));
        }
        const int segments = SuspensionDeckSections;
        for (var i = 0; i < segments; i++)
        {
            float T(int k) => k / (float)segments;
            var a = new Vector3(x, SuspensionDeckHeight(T(i), nearY, farY), Mathf.Lerp(nearZ, farZ, T(i)));
            var b = new Vector3(x, SuspensionDeckHeight(T(i + 1), nearY, farY), Mathf.Lerp(nearZ, farZ, T(i + 1)));
            var mid = (a + b) * .5f;
            var tilt = Mathf.Atan2(b.Y - a.Y, a.Z - b.Z);
            var span = a.DistanceTo(b) + .14f;   // moving boxes overlap, so the strip never opens
            // Three stringers under the boards: the visible structure that carries the
            // deck and fills what used to read as holes between slats.
            foreach (var stringer in new[] { -.62f, 0f, .62f })
                AddTiltedBoard(_suspensionIntact,
                    $"SuspensionStringer_{i:00}_{(stringer < 0 ? "w" : stringer > 0 ? "e" : "c")}",
                    new Vector3(.13f, .09f, span), mid - Vector3.Up * .072f + new Vector3(stringer, 0f, 0f),
                    new Vector3(tilt, 0, 0), "4a3d30", "wood");
            _suspensionDeckBody.AddChild(new CollisionShape3D
            {
                Name = $"SuspensionDeckShape_{i:00}", Position = mid - Vector3.Up * .02f, Rotation = new Vector3(tilt, 0, 0),
                // VIS-015: the strip is 10 cm thick and centred 2 cm under the deck line, so
                // its top is exactly the top of the 6 cm boards (mid + 3 cm). The old 8 cm box
                // centred 3 cm under the line walked 2 cm inside the planks: the feet sank into
                // the very surface the eye was told to step on, which reads as a broken joint.
                Shape = new BoxShape3D { Size = new Vector3(1.4f, .10f, span) }
            });
            foreach (var side in new[] { -1f, 1f })
            {
                // Hand ropes double as the walls of the walk: nobody steps off the side.
                AddRavineLog(_suspensionIntact, $"SuspensionHandRope_{(side < 0 ? "w" : "e")}_{i:00}",
                    a + new Vector3(side * .78f, .95f, 0), b + new Vector3(side * .78f, .95f, 0), .025f, "9c8a6a");
                _suspensionDeckBody.AddChild(new CollisionShape3D
                {
                    Name = $"SuspensionRopeWall_{(side < 0 ? "w" : "e")}_{i:00}", Position = mid + new Vector3(side * .8f, .5f, 0),
                    Rotation = new Vector3(tilt, 0, 0), Shape = new BoxShape3D { Size = new Vector3(.08f, 1.0f, span) }
                });
                if (i % 2 == 0)
                    AddRavineLog(_suspensionIntact, $"SuspensionHanger_{(side < 0 ? "w" : "e")}_{i:00}",
                        a + new Vector3(side * .7f, 0, 0), a + new Vector3(side * .78f, .95f, 0), .012f, "9c8a6a");
            }
        }
        foreach (var child in _suspensionIntact.GetChildren().OfType<MeshInstance3D>().Where(m => m.Name.ToString().StartsWith("SuspensionHand", StringComparison.Ordinal)))
            child.MaterialOverride = rope;

        // The ends: five boards per rim on a ramp from the deck line down to the real
        // ground, a sleeper on the ground under the outermost board and a collision
        // ramp that reaches 0.35 m under the first/last moving deck box. The old code
        // stopped the boards 3 cm inside each rim and the first slat holes showed there.
        const float apronLength = 1.5f;
        const int apronBoards = 5;
        foreach (var (rimZ, endY, end, sign) in new[] { (nearZ, nearY, "near", 1f), (farZ, farY, "far", -1f) })
        {
            var outerZ = rimZ + sign * apronLength;
            var outerY = (float)AgentBAct1HeightField.CollisionGround(x, outerZ) + .03f;
            var rimPoint = new Vector3(x, endY, rimZ);
            var outerPoint = new Vector3(x, outerY, outerZ);
            var a = rimPoint.Z > outerPoint.Z ? rimPoint : outerPoint;
            var b = rimPoint.Z > outerPoint.Z ? outerPoint : rimPoint;
            var rampTilt = Mathf.Atan2(b.Y - a.Y, a.Z - b.Z);
            for (var k = 1; k <= apronBoards; k++)
            {
                var f = Mathf.Lerp(.07f, .95f, (k - 1) / (float)(apronBoards - 1));
                AddTiltedBoard(_suspensionIntact, $"SuspensionApronBoard_{end}_{k}",
                    new Vector3(1.4f, .06f, .38f), new Vector3(x, Mathf.Lerp(endY, outerY, f), Mathf.Lerp(rimZ, outerZ, f)),
                    new Vector3(rampTilt, 0, 0), k % 2 == 0 ? "5c4a38" : "4a3d30", "wood");
            }
            AddVisualBox(_suspensionIntact, $"SuspensionApronSleeper_{end}", new(1.62f, .16f, .26f),
                new(x, outerY - .09f, outerZ + sign * .10f), "4a3d30", "wood", yawDegrees: sign * 3f);
            // VIS-015: the joint between the bank and the deck. The boards carry the walker,
            // but their straight line leaves a slot of 9 cm at the rim and closes to nothing
            // at the outer end; seen from the street that slot is the technical step the card
            // is about. One conformed snow bearing — edge tier, never more than 12 cm — fills
            // it from under the boards and feathers to the real ground at both ends, so the
            // gorge keeps its depth and the walk onto the planks is continuous. It hangs off
            // the bridge root, not off the span: the bank stays when the deck falls, and the
            // dynamics never collects it (its prefixes are planks, ropes and deck shapes).
            AddDeckLandBearing(root, $"SuspensionRimBearing_{end}",
                new Vector2(x, rimZ), new Vector2(x, outerZ), Vector2.Right, endY, outerY, .95f);
            var from = rimPoint + new Vector3(0, 0, -sign * .35f);   // under the first moving box
            var to = outerPoint + new Vector3(0, 0, sign * .15f);    // over the rim
            var rampA = from.Z > to.Z ? from : to;
            var rampB = from.Z > to.Z ? to : from;
            var rampMid = (rampA + rampB) * .5f;
            _suspensionDeckBody.AddChild(new CollisionShape3D
            {
                Name = $"SuspensionApronShape_{end}", Position = rampMid - Vector3.Up * .02f,
                Rotation = new Vector3(Mathf.Atan2(rampB.Y - rampA.Y, rampA.Z - rampB.Z), 0, 0),
                // VIS-015: same top as the apron boards it carries (see SuspensionDeckShape).
                Shape = new BoxShape3D { Size = new Vector3(1.5f, .10f, rampA.DistanceTo(rampB) + .10f) }
            });
        }

        // After the collapse: two short ends hang from the posts, the rest is gone.
        _suspensionBroken = new Node3D { Name = "SuspensionBridgeBroken", Visible = false };
        root.AddChild(_suspensionBroken);
        foreach (var (z, y, dir, end) in new[] { (nearZ, nearY, -1f, "near"), (farZ, farY, 1f, "far") })
            for (var k = 0; k < 6; k++)
                AddVisualBox(_suspensionBroken, $"SuspensionHangingPlank_{end}_{k}", new(1.3f, .06f, .3f),
                    new(x + (k % 2 == 0 ? .1f : -.1f), y - .45f - k * .5f, z + dir * .25f), "4a3d30", "wood", rollDegrees: k * 7f);
        // Once it is gone, the two posts with the torn hand ropes close the gap in each rim fence.
        _suspensionGapBlocker = new StaticBody3D { Name = "SuspensionGapBlocker", CollisionLayer = 0, CollisionMask = 0 };
        _suspensionBroken.AddChild(_suspensionGapBlocker);
        foreach (var (z, y, end) in new[] { (nearZ - .1f, nearY, "near"), (farZ + .1f, farY, "far") })
        {
            AddRavineLog(_suspensionBroken, $"SuspensionTornRope_{end}", new Vector3(x - .95f, y + .95f, z), new Vector3(x + .95f, y + .7f, z), .03f, "9c8a6a");
            _suspensionGapBlocker.AddChild(new CollisionShape3D
            {
                Name = $"SuspensionGapShape_{end}", Position = new Vector3(x, y + .6f, z),
                Shape = new BoxShape3D { Size = new Vector3(3.4f, 1.2f, .3f) }
            });
        }
        foreach (var part in _suspensionIntact.GetChildren().OfType<Node3D>()) _suspensionRest[part] = part.Transform;
        _suspensionDeckPlanks = planks;
        _suspensionPatchBoards = patches;
        root.SetMeta("deckPlanks", planks);
        root.SetMeta("deckPatchBoards", patches);
        root.SetMeta("deckApronBoards", apronBoards * 2);
        root.SetMeta("deckSections", segments);
        root.SetMeta("deckHoles", 0);
        root.SetMeta("swayContract", "SuspensionBridgeDynamics moves visuals and collision together; owner calls Tick");
        // VIS-015: the collision strip must agree with the planks the eye sees. A section box
        // is a chord of the sagging deck curve, so the walking surface and the board top differ
        // by the sag across one section — measured here instead of assumed, and reported.
        var deckSurfaceError = 0f;
        for (var i = 0; i < planks; i++)
        {
            var t = (i + .5f) / planks;
            var section = Mathf.Min(segments - 1, (int)(t * segments));
            var chord = (SuspensionDeckHeight(section / (float)segments, nearY, farY)
                + SuspensionDeckHeight((section + 1) / (float)segments, nearY, farY)) * .5f;
            deckSurfaceError = Mathf.Max(deckSurfaceError, Mathf.Abs(chord - SuspensionDeckHeight(t, nearY, farY)));
        }
        GD.Print($"act1-gorge-joint: deckCollisionTopError={deckSurfaceError:0.####} planks={planks} " +
                 $"sections={segments} apronBoards={apronBoards * 2}");

        // The dynamics pre-collects every animated node (planks, repairs, stringers,
        // ropes, hangers, deck/rope-wall collision shapes) and its three audio voices.
        // Initialize is idempotent; the owner only has to wire Tick.
        _suspensionDynamics = new SuspensionBridgeDynamics();
        _suspensionDynamics.Initialize(this);
    }

    /// <summary>
    /// VIS-015: the snow bearing that closes one bridge-to-bank joint, for either bridge.
    /// Five lateral samples × nine stations along the deck's landward run, every vertex seated
    /// on the real collision ground and rising only as far as the straight underside of the
    /// boards allows, minus the joint gap. Where the bank already reaches the boards the crest
    /// is zero, so the ribbon never becomes a white block stuffed under a span and never
    /// reaches into a cut: it spans exactly the apron it is given. Presentation only — the
    /// walkable surface stays the planks and their collision strip.
    /// <paramref name="rim"/> and <paramref name="outer"/> are XZ points at the deck end and at
    /// the landward end of its approach; <paramref name="lateral"/> is the unit vector across
    /// the deck; the deck heights are its walking tops.
    /// </summary>
    private static MeshInstance3D AddDeckLandBearing(Node3D parent, string name, Vector2 rim, Vector2 outer,
        Vector2 lateral, float rimDeckY, float outerDeckY, float halfWidth,
        float jointGap = RimJointTargetGap, float crestCap = SnowReliefStandard.EdgeHeightMax)
    {
        const int stations = 9;
        const int columns = 5;
        var points = new Vector3[stations, columns];
        var crestMax = 0f;
        var jointRemaining = 0f;
        var jointSlot = 0f;
        for (var s = 0; s < stations; s++)
        {
            var t = s / (float)(stations - 1);              // 0 at the deck end, 1 landward
            var centre = rim.Lerp(outer, t);
            var underside = Mathf.Lerp(rimDeckY, outerDeckY, t) - .03f;
            for (var c = 0; c < columns; c++)
            {
                var offset = (c / (float)(columns - 1) * 2 - 1) * halfWidth;
                var px = centre.X + lateral.X * offset;
                var pz = centre.Y + lateral.Y * offset;
                var ground = AgentBAct1HeightField.CollisionGround(px, pz);
                var crest = Mathf.Clamp(underside - ground - jointGap, 0f, crestCap)
                    * (1f - .35f * Mathf.Pow(Mathf.Abs(offset) / halfWidth, 2f));
                crestMax = Mathf.Max(crestMax, crest);
                points[s, c] = new Vector3(px, ground + crest, pz);
                // The walking line is what the joint is measured on: the open slot before the
                // bearing, and what of it is left after, must both be reported honestly.
                if (c != columns / 2) continue;
                if (t < .01f) jointSlot = underside - ground;
                else if (crest > 0f) jointRemaining = Mathf.Max(jointRemaining, underside - ground - crest);
            }
        }
        using var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        void Vertex(int s, int c)
        {
            var p = points[s, c];
            tool.SetUV(new Vector2(p.X * .25f, p.Z * .25f));
            tool.AddVertex(parent.ToLocal(p));
        }
        for (var s = 0; s < stations - 1; s++)
        for (var c = 0; c < columns - 1; c++)
        {
            // Both windings: the bearing is read from the bank and from under the deck edge.
            Vertex(s, c); Vertex(s, c + 1); Vertex(s + 1, c);
            Vertex(s, c + 1); Vertex(s + 1, c + 1); Vertex(s + 1, c);
        }
        tool.GenerateNormals();
        var bearing = new MeshInstance3D
        {
            Name = name,
            Mesh = tool.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("e4eaee", "snow_ground"),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        bearing.SetMeta("visualOnly", true);
        bearing.SetMeta("presentationOnly", true);
        bearing.SetMeta("collisionOwner", "none");
        bearing.SetMeta("snowTier", SnowReliefStandard.TierEdge);
        bearing.SetMeta("snowBankHeight", crestMax);
        bearing.SetMeta("bridgeJointSlotBefore", jointSlot);
        bearing.SetMeta("bridgeJointGapAfter", jointRemaining);
        bearing.SetMeta("presentationRole",
            "VIS-015 snow bearing closing the bank-to-deck joint of a bridge approach");
        parent.AddChild(bearing);
        GD.Print($"act1-bridge-joint: name={name} slotBefore={jointSlot:0.###} crestMax={crestMax:0.###} " +
                 $"gapAfter={jointRemaining:0.####} target={jointGap:0.###} cap={crestCap:0.###}");
        return bearing;
    }

    /// <summary>The bridge dynamics created with the bridge; its Tick is wired by the owner.</summary>
    internal SuspensionBridgeDynamics? SuspensionDynamics => _suspensionDynamics;

    /// <summary>
    /// Optional one-line driver for the owner's presentation tick: resolves the cached
    /// player, tests the deck span and calls <see cref="SuspensionBridgeDynamics.Tick"/>.
    /// Cheap: the player node is cached and only re-resolved after a zone rebuild, and
    /// the dynamics puts itself to sleep beyond 84 m. Either call this, or call
    /// SuspensionDynamics.Tick with the owner's own position/on-deck pair.
    /// </summary>
    internal void TickSuspensionDynamics(double delta)
    {
        if (_suspensionDynamics is not { IsReady: true } dynamics) return;
        if (LifePlayer() is not { } player) return;
        var at = player.GlobalPosition;
        var onDeck = Mathf.Abs(at.X - SuspensionBridgeX) < 1.2f
            && at.Z < GorgeNearRim(at.X) && at.Z > GorgeFarRim(at.X);
        dynamics.Tick(delta, at, onDeck);
    }

    /// <summary>Applies the saved state: intact (walkable) or broken (gone, collision off).</summary>
    private void UpdateSuspensionBridge()
    {
        if (_suspensionIntact is null || _suspensionBroken is null || _suspensionDeckBody is null || _suspensionCollapsing) return;
        var broken = SuspensionBridgeBroken;
        if (!broken)
            foreach (var (part, rest) in _suspensionRest) part.Transform = rest;   // a new session restores the deck
        _suspensionIntact.Visible = !broken;
        _suspensionBroken.Visible = broken;
        _suspensionDeckBody.CollisionLayer = broken ? 0u : 1u;
        if (_suspensionGapBlocker is not null) _suspensionGapBlocker.CollisionLayer = broken ? 2u : 0u;
    }

    internal bool SuspensionBridgeBroken => _runtimeBridge?.SelectWorldProps() is { } props
        && props.TryGetProperty(SuspensionBridgeStateId, out var state) && state.ValueKind == JsonValueKind.Object
        && state.TryGetProperty("broken", out var broken) && broken.ValueKind == JsonValueKind.True;

    /// <summary>Per frame: once Aidar has stood on the forest side after the night of the
    /// blizzard, stepping back onto the village rim brings the bridge down behind him.</summary>
    private void WatchSuspensionBridge()
    {
        if (_suspensionIntact is null || _suspensionCollapsing || _runtimeBridge is null) return;
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player || ActiveZoneId is not ("village_day" or "zirat_road" or "kara_urman_night")) return;
        // Ordered cheap-to-expensive: both properties read the same immutable kernel
        // snapshot, but SuspensionBridgeBroken additionally walks into the nested
        // "broken" object. All four conditions are conjunctive guards with no side
        // effects, so the set of frames that reaches the collapse code is identical.
        if (!_runtimeBridge.FirstNightPassed) return;
        if (SuspensionBridgeBroken) return;
        var z = player.GlobalPosition.Z;
        var x = player.GlobalPosition.X;
        if (z < GorgeFarRim(x) - 1f) _suspensionVisitedFar = true;
        else if (_suspensionVisitedFar && z > GorgeNearRim(x) + 1.5f && Mathf.Abs(x - SuspensionBridgeX) < 6f)
            CollapseSuspensionBridge();
    }

    private async void CollapseSuspensionBridge()
    {
        _suspensionCollapsing = true;
        _suspensionDeckBody!.CollisionLayer = 0u;
        var centre = new Vector3(SuspensionBridgeX, (float)AgentBAct1HeightField.CollisionGround(SuspensionBridgeX, GorgeNearRim(0)), GorgeCentreZ(0));
        UiFoley.PlayWorld(this, centre, "forest/branch_crack", -6f, 60f, 1f);
        var fall = CreateTween().SetParallel();
        var index = 0;
        foreach (var part in _suspensionIntact!.GetChildren().OfType<Node3D>())
        {
            var delay = .04f * (index++ % 30);
            fall.TweenProperty(part, "position:y", part.Position.Y - 11f, 1.6f).SetDelay(delay)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            fall.TweenProperty(part, "rotation:z", part.Rotation.Z + (index % 2 == 0 ? .9f : -.7f), 1.6f).SetDelay(delay);
        }
        await ToSignal(fall, Tween.SignalName.Finished);
        UiFoley.PlayWorld(this, centre + Vector3.Down * 8f, "forest/wind_gust", -10f, 60f, 1f);
        if (_runtimeBridge is not null)
            await _runtimeBridge.DispatchWorldPropsAsync(new JsonArray { new JsonObject { ["propId"] = SuspensionBridgeStateId, ["broken"] = true } });
        _suspensionCollapsing = false;
        UpdateSuspensionBridge();
    }

    /// <summary>Presentation and props authored when this was a 9 m river channel (trees, banks,
    /// rocks, the old kara-side dressing) can now stand inside the gorge. Hide everything whose
    /// centre lies within the cut, except the gorge's own dressing, the bridge and the terrain;
    /// disable any collision shape there for the same reason. Foliage multimeshes lose only the
    /// instances that stand in the cut.</summary>
    private void ClearGorgeOfLegacyPresentation()
    {
        static bool Inside(Vector3 p) =>
            Mathf.Abs(p.Z - GorgeCentreZ(p.X)) < (float)AgentBAct1HeightField.GorgeHalfWidth - .6f;
        bool Keep(Node node)
        {
            for (var n = node; n is not null && n != this; n = n.GetParent())
            {
                var name = n.Name.ToString();
                if (name is "VillageForestGorge" or "AgentB_TerrainCollision" or "AgentB_TerrainRoadKit" || n is InteractionTarget) return true;
            }
            return false;
        }
        int hidden = 0, shapes = 0, instances = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
        {
            if (mesh.Mesh is null || !mesh.IsVisibleInTree() || Keep(mesh)) continue;
            var centre = mesh.GlobalTransform * mesh.Mesh.GetAabb().GetCenter();
            if (mesh.Mesh.GetAabb().Size.Length() > 60f)
            {
                var box = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                if (box.Position.Z < GorgeCentreZ(box.GetCenter().X) && box.End.Z > GorgeCentreZ(box.GetCenter().X)
                    && box.Position.X < 60 && box.End.X > -60 && mesh.Name != "Terrain_Main")
                    GD.Print($"act1-gorge-clear: large mesh spans the gorge: {mesh.GetPath()} box={box.Position}..{box.End}");
                continue;
            }
            if (!Inside(centre))
            {
                var b2 = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                var cz = GorgeCentreZ(b2.GetCenter().X);
                if (System.Environment.GetEnvironmentVariable("URMAN_GORGE_DEBUG") == "1" && b2.Position.Z < cz + 4 && b2.End.Z > cz - 4
                    && b2.End.X > -50 && b2.Position.X < 50 && b2.End.Y > -2f)
                    GD.Print($"act1-gorge-clear: crosses the cut: {mesh.GetPath()} box={b2.Position}..{b2.End}");
                continue;
            }
            mesh.Visible = false; hidden++;
        }
        // The authored terrain/road kit predates the gorge: any of its pieces that are not the
        // rebuilt terrain or a conformed road ribbon and reach into the cut are hidden too.
        if (FindChild("AgentB_TerrainRoadKit", true, false) is Node3D roadKit)
            foreach (var mesh in FindDescendants<MeshInstance3D>(roadKit).ToArray())
            {
                if (mesh.Mesh is null || !mesh.IsVisibleInTree() || mesh.Name == "Terrain_Main") continue;
                var box = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                var cz = GorgeCentreZ(box.GetCenter().X);
                if (box.Position.Z > cz + (float)AgentBAct1HeightField.GorgeHalfWidth - .6f
                    || box.End.Z < cz - (float)AgentBAct1HeightField.GorgeHalfWidth + .6f) continue;
                if (mesh.Name.ToString().StartsWith("Road_", StringComparison.Ordinal)
                    || mesh.Name.ToString().StartsWith("Grade_", StringComparison.Ordinal)) continue;   // conformed down into the cut
                GD.Print($"act1-gorge-clear: road kit piece in the cut hidden: {mesh.Name} box={box.Position}..{box.End}");
                mesh.Visible = false; hidden++;
            }
        foreach (var shape in FindDescendants<CollisionShape3D>(this).ToArray())
        {
            if (shape.Disabled || Keep(shape) || !Inside(shape.GlobalPosition)) continue;
            if (shape.Shape is ConcavePolygonShape3D or HeightMapShape3D) continue;
            shape.Disabled = true; shapes++;
        }
        foreach (var multi in FindDescendants<MultiMeshInstance3D>(this).ToArray())
        {
            if (multi.Multimesh is not { } mm || Keep(multi)) continue;
            var count = mm.VisibleInstanceCount < 0 ? mm.InstanceCount : mm.VisibleInstanceCount;
            for (var i = 0; i < count; i++)
            {
                var t = mm.GetInstanceTransform(i);
                if (!Inside(multi.GlobalTransform * t.Origin)) continue;
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * .0001f), t.Origin + Vector3.Down * 40f));
                instances++;
            }
        }
        if (System.Environment.GetEnvironmentVariable("URMAN_GORGE_DEBUG") == "1")
            foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
            {
                if (mesh.Mesh is null || !mesh.IsVisibleInTree() || mesh.Name == "Terrain_Main") continue;
                var box = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                if (box.End.X < -40 || box.Position.X > 40) continue;
                var cz = GorgeCentreZ(box.GetCenter().X);
                if (box.Position.Z > cz + 7.4f || box.End.Z < cz - 7.4f) continue;
                for (var t = 0f; t <= 1f; t += .25f)
                {
                    var p = box.Position + box.Size * new Vector3(t, 0, t);
                    if (Inside(p) && box.Position.Y > AgentBAct1HeightField.CollisionGround(p.X, p.Z) + .4f)
                    { GD.Print($"act1-gorge-float: {mesh.GetPath()} box={box.Position}..{box.End}"); break; }
                }
            }
        SetMeta("gorgeClearedMeshes", hidden);
        GD.Print($"act1-gorge-clear: hidden={hidden} shapesDisabled={shapes} foliageInstances={instances}");
    }
}
