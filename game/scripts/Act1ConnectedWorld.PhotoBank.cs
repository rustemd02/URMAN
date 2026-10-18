using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// A separate, older timber approach south of the collapsed village bridge.
/// It ends at the west bank; the surviving piles can be seen across the frozen
/// ravine, but they do not form a second crossing.
/// </summary>
public partial class Act1ConnectedWorld
{
    private const float PhotoBankApproachEntryZ = -52.5f;
    private const float PhotoBankApproachBankOffset = 7.4f;
    internal const float PhotoBankApproachEntryYawDegrees = -90f;

    internal static Vector3 PhotoBankApproachEntryPosition
    {
        get
        {
            var x = (float)AgentBAct1HeightField.RavineCentre(PhotoBankApproachEntryZ)
                - PhotoBankApproachBankOffset;
            return new Vector3(x,
                AgentBAct1HeightField.CollisionGround(x, PhotoBankApproachEntryZ) + .05f,
                PhotoBankApproachEntryZ);
        }
    }

    private static float PhotoBankTrailX(float z) =>
        (float)AgentBAct1HeightField.RavineCentre(z) - PhotoBankApproachBankOffset;

    private void AddPhotoBankApproach(Node3D core)
    {
        var bank = new Node3D { Name = "PhotoBankApproach", Visible = false };
        bank.SetMeta("presentationRole", "fullgame PhotoWorlds winter bank approach, separate from the collapsed village bridge");
        bank.SetMeta("routeId", "pw-bank-approach");
        bank.SetMeta("collisionOwner", "terrain plus authored approach surfaces and timber");
        core.AddChild(bank);

        AddPhotoBankTrail(bank);

        var entry = PhotoBankApproachEntryPosition;
        var entryX = entry.X;
        var entryZ = entry.Z;

        // E-BANK source relation, mapped from W02Controller.BuildOldApproach:
        // source origin (34.6, 63.5) maps to the near winter post at
        // (RavineCentre(entryZ)-4.3, entryZ). Reflect source X across that
        // anchor and preserve source Z offsets: x'=nearX-(x-34.6),
        // z'=entryZ+(z-63.5). The source (-.62,+.85) m post step therefore
        // becomes (+.62,+.85) m: seven single-file posts over a 6.3 m diagonal,
        // followed by the same four slanted broken boards. The near post is
        // 3.10 m in front of BankEntry (entry is centre-7.4; post is centre-4.3),
        // outside its 1.2 m safe-stance radius. At all seven posts the mapped
        // X offset stays within the ravine's 4.6 m half-width; the far post is
        // still 1.21 m west of the local centreline, so this is not a crossing.
        // Each post embeds 0.25 m into CollisionGround. With source tilts up to
        // 11 degrees and the minimum 0.28 m source exposure, its rotated top
        // remains at least 0.41 m above the anchor's CollisionGround sample.
        const float sourcePileOriginX = 34.6f;
        const float sourcePileOriginZ = 63.5f;
        var nearPileX = (float)AgentBAct1HeightField.RavineCentre(entryZ) - 4.3f;
        Vector2 MapOldApproach(float x, float z) => new(
            nearPileX - (x - sourcePileOriginX),
            entryZ + (z - sourcePileOriginZ));

        var sourceRandom = new Random(20261017);
        Vector3 nearPileTop = default;
        for (var pile = 0; pile < 7; pile++)
        {
            var sourceX = sourcePileOriginX - .62f * pile;
            var sourceZ = sourcePileOriginZ + .85f * pile;
            var mapped = MapOldApproach(sourceX, sourceZ);
            var floor = AgentBAct1HeightField.CollisionGround(mapped.X, mapped.Y);
            var sourceAboveWater = .28f + (float)sourceRandom.NextDouble() * .34f;
            var winterIceDatum = Mathf.Max(floor, (float)AgentBAct1HeightField.Ground(mapped.X, mapped.Y)) + .14f;
            var topY = winterIceDatum + sourceAboveWater;
            var sourceTilt = new Vector3(
                4f * ((pile % 3) - 1),
                20f * pile,
                5f + 4f * ((pile % 4) - 1.5f));
            var actualTop = AddPhotoBankPile(bank, $"OldApproachPile{pile + 1}",
                mapped.X, mapped.Y, floor - .25f, topY, sourceTilt);
            if (pile == 0) nearPileTop = actualTop;
        }

        for (var board = 0; board < 4; board++)
        {
            var sourceX = 33.2f - board * .55f;
            var sourceZ = 64.1f + .35f * board;
            var mapped = MapOldApproach(sourceX, sourceZ);
            var floor = AgentBAct1HeightField.CollisionGround(mapped.X, mapped.Y);
            var winterIceDatum = Mathf.Max(floor, (float)AgentBAct1HeightField.Ground(mapped.X, mapped.Y)) + .14f;
            var centerY = winterIceDatum + .10f + board * .04f;
            var sourceRotation = new Vector3(56f + board * 6f, 24f - board * 9f, 14f);
            AddPhotoBankBrokenBoard(bank, $"OldApproachBrokenBoard{board + 1}",
                new Vector3(mapped.X, centerY, mapped.Y), sourceRotation);
        }

        var entryMarker = new Marker3D
        {
            Name = "BankEntry",
            Position = entry,
            RotationDegrees = new Vector3(0f, PhotoBankApproachEntryYawDegrees, 0f)
        };
        bank.AddChild(entryMarker);

        AddBankMarker(bank, "OldPilesWitness", nearPileTop);

        var returnZ = entryZ + .95f;
        var returnX = PhotoBankTrailX(returnZ);
        AddBankMarker(bank, "BankReturnWitness", new Vector3(returnX,
            AgentBAct1HeightField.CollisionGround(returnX, returnZ) + .05f, returnZ));

        var leafGround = AgentBAct1HeightField.CollisionGround(entryX - 1.4f, entryZ);
        var twigBase = new Vector3(entryX - 1.4f, leafGround + .015f, entryZ);
        var twigTip = twigBase + new Vector3(-.11f, .55f, .08f);
        AddBankTimber(bank, "PersistentLeafTwig", twigBase, twigTip, .025f, "514336");
        var leafPivot = new Node3D
        {
            Name = "PersistentLeafRevealPivot",
            Position = twigTip,
            RotationDegrees = new Vector3(12f, 18f, -27f),
            Visible = false
        };
        leafPivot.SetMeta("physicalSubject", "single seasonal leaf revealed on a bare winter twig, then moved to its snow landing");
        bank.AddChild(leafPivot);

        var leaf = new MeshInstance3D
        {
            Name = "PersistentWinterLeaf",
            Mesh = BuildPersistentLeafMesh(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("5d7351", "foliage")
        };
        leaf.SetMeta("physicalSubject", "thin asymmetrical leaf blade with a tapered tip and folded midrib ridge");
        leafPivot.AddChild(leaf);
        leafPivot.AddChild(new MeshInstance3D
        {
            Name = "PersistentLeafMidrib",
            Mesh = BuildPersistentLeafMidribMesh(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("89926b", "foliage")
        });
        var leafWitnessLocal = new Vector3(.17f, .004f, .014f);
        var leafWitness = new Marker3D
        {
            Name = "LeafInteractionWitness",
            Position = leafWitnessLocal
        };
        leafPivot.AddChild(leafWitness);
        AddBankMarker(bank, "LeafWitness", leafPivot.Transform * leafWitnessLocal);

        var landingX = entryX - 2.0f;
        var landingZ = entryZ + .35f;
        var landing = AddBankMarker(bank, "LeafLanding", new Vector3(landingX,
            AgentBAct1HeightField.CollisionGround(landingX, landingZ) + .012f, landingZ));
        landing.RotationDegrees = new Vector3(-90f, 28f, 0f);

        var meetingX = entryX - 2.3f;
        var meetingZ = entryZ - 1.4f;
        var meetingPosition = new Vector3(meetingX,
            AgentBAct1HeightField.CollisionGround(meetingX, meetingZ) + .05f, meetingZ);
        var towardTwig = twigTip - meetingPosition;
        var meeting = AddBankMarker(bank, "AlsuMeeting", meetingPosition);
        // The existing NPC gait and conversation owner face local +Z.
        meeting.RotationDegrees = new Vector3(0f,
            Mathf.RadToDeg(Mathf.Atan2(towardTwig.X, towardTwig.Z)), 0f);

        // The presence owner is attached after its direct markers are stable.
        bank.AddChild(new PhotoBankApproachPresence { Name = "PhotoBankApproachPresence" });
    }

    private static void AddPhotoBankTrail(Node3D bank)
    {
        using var centerline = new Curve3D { BakeInterval = .3f };
        for (var z = -25.5f; z > PhotoBankApproachEntryZ; z -= 3f)
            centerline.AddPoint(new Vector3(PhotoBankTrailX(z), 0f, z));
        centerline.AddPoint(new Vector3(PhotoBankTrailX(PhotoBankApproachEntryZ), 0f, PhotoBankApproachEntryZ));

        var trail = AddVisualLandformSurface(bank, "PhotoBankApproachTrail", 1.8f, .018f,
            centerline.GetBakedLength(), Vector3.Zero, "cbd3d8", "snow_trampled", 0f,
            conformToTerrain: true, centerline: centerline);
        trail.SetMeta("visualOnly", false);
        trail.SetMeta("terrainRole", "packed-snow approach conformed to the shared terrain collision");
        trail.SetMeta("collisionOwner", "PhotoBankApproachTrailBody");
        var contact = new StaticBody3D { Name = "PhotoBankApproachTrailBody", CollisionLayer = 0u, CollisionMask = 0u };
        contact.SetMeta("collisionOwner", "PhotoBankApproachTrail");
        contact.SetMeta("footstepSurface", "snow_packed");
        contact.AddToGroup("photo_bank_approach_collision");
        trail.AddChild(contact);
        contact.AddChild(new CollisionShape3D
        {
            Name = "PhotoBankApproachTrailContact",
            Shape = trail.Mesh!.CreateTrimeshShape()
        });
    }

    private static Marker3D AddBankMarker(Node3D parent, string name, Vector3 position)
    {
        var marker = new Marker3D { Name = name, Position = position };
        marker.SetMeta("routeId", "pw-bank-approach");
        parent.AddChild(marker);
        return marker;
    }

    private static ArrayMesh BuildPersistentLeafMesh()
    {
        ReadOnlySpan<float> x = [0f, .052f, .118f, .190f, .258f, .320f];
        ReadOnlySpan<float> halfWidth = [0f, .036f, .061f, .057f, .031f, 0f];
        ReadOnlySpan<float> curve = [0f, .003f, -.004f, .006f, .001f, 0f];
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var section = 0; section < x.Length - 1; section++)
        {
            var left = new Vector3(x[section], curve[section] - halfWidth[section], 0f);
            var ridge = new Vector3(x[section], curve[section], .008f);
            var right = new Vector3(x[section], curve[section] + halfWidth[section], 0f);
            var nextLeft = new Vector3(x[section + 1], curve[section + 1] - halfWidth[section + 1], 0f);
            var nextRidge = new Vector3(x[section + 1], curve[section + 1], .008f);
            var nextRight = new Vector3(x[section + 1], curve[section + 1] + halfWidth[section + 1], 0f);
            var u = (float)section / (x.Length - 1);
            var nextU = (float)(section + 1) / (x.Length - 1);
            AddDoubleSidedLeafTriangle(surface, left, nextLeft, ridge, new(u, 0f), new(nextU, 0f), new(u, .5f));
            AddDoubleSidedLeafTriangle(surface, ridge, nextLeft, nextRidge, new(u, .5f), new(nextU, 0f), new(nextU, .5f));
            AddDoubleSidedLeafTriangle(surface, ridge, nextRidge, right, new(u, .5f), new(nextU, .5f), new(u, 1f));
            AddDoubleSidedLeafTriangle(surface, right, nextRidge, nextRight, new(u, 1f), new(nextU, .5f), new(nextU, 1f));
        }
        return surface.Commit();
    }

    private static ArrayMesh BuildPersistentLeafMidribMesh()
    {
        ReadOnlySpan<float> x = [0f, .052f, .118f, .190f, .258f, .320f];
        ReadOnlySpan<float> width = [.0035f, .0035f, .0032f, .0028f, .0019f, 0f];
        ReadOnlySpan<float> curve = [0f, .003f, -.004f, .006f, .001f, 0f];
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var section = 0; section < x.Length - 1; section++)
        {
            var left = new Vector3(x[section], curve[section] - width[section], .011f);
            var right = new Vector3(x[section], curve[section] + width[section], .011f);
            var nextLeft = new Vector3(x[section + 1], curve[section + 1] - width[section + 1], .011f);
            var nextRight = new Vector3(x[section + 1], curve[section + 1] + width[section + 1], .011f);
            var u = (float)section / (x.Length - 1);
            var nextU = (float)(section + 1) / (x.Length - 1);
            AddDoubleSidedLeafTriangle(surface, left, nextLeft, right, new(u, 0f), new(nextU, 0f), new(u, 1f));
            AddDoubleSidedLeafTriangle(surface, right, nextLeft, nextRight, new(u, 1f), new(nextU, 0f), new(nextU, 1f));
        }
        return surface.Commit();
    }

    private static void AddDoubleSidedLeafTriangle(SurfaceTool surface,
        Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC)
    {
        var normal = (b - a).Cross(c - a).Normalized();
        AddLeafVertex(surface, a, uvA, normal);
        AddLeafVertex(surface, b, uvB, normal);
        AddLeafVertex(surface, c, uvC, normal);
        AddLeafVertex(surface, c, uvC, -normal);
        AddLeafVertex(surface, b, uvB, -normal);
        AddLeafVertex(surface, a, uvA, -normal);
    }

    private static void AddLeafVertex(SurfaceTool surface, Vector3 position, Vector2 uv, Vector3 normal)
    {
        surface.SetNormal(normal);
        surface.SetUV(uv);
        surface.AddVertex(position);
    }

    private static Vector3 AddPhotoBankPile(Node3D parent, string name,
        float x, float z, float bottomY, float topY, Vector3 sourceRotationDegrees)
    {
        var height = topY - bottomY;
        var center = new Vector3(x, bottomY + height * .5f, z);
        var basis = ReflectOldApproachBasis(sourceRotationDegrees);
        var pose = new Transform3D(basis, center);
        var visual = new MeshInstance3D
        {
            Name = name,
            Transform = pose,
            Mesh = new CylinderMesh
            {
                TopRadius = .12f,
                BottomRadius = .155f,
                Height = height,
                RadialSegments = 6
            },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("514132", "wood")
        };
        visual.SetMeta("physicalSubject", "single salvaged timber pile from the old approach, embedded in the winter ravine ice");
        parent.AddChild(visual);

        var body = new StaticBody3D
        {
            Name = name + "Body",
            Transform = pose,
            CollisionLayer = 0u,
            CollisionMask = 0u
        };
        body.SetMeta("collisionOwner", name);
        body.SetMeta("footstepSurface", "wood");
        body.AddToGroup("photo_bank_approach_collision");
        body.AddChild(new CollisionShape3D
        {
            Name = name + "Contact",
            Shape = new CylinderShape3D { Radius = .155f, Height = height }
        });
        parent.AddChild(body);

        // The witness is on the rendered, rotated top end, not a nearby free-space marker.
        return center + basis.Y.Normalized() * (height * .5f);
    }

    private static void AddPhotoBankBrokenBoard(Node3D parent, string name,
        Vector3 center, Vector3 sourceRotationDegrees)
    {
        var basis = ReflectOldApproachBasis(sourceRotationDegrees);
        var pose = new Transform3D(basis, center);
        var size = new Vector3(.17f, .05f, 1.5f);
        var visual = new MeshInstance3D
        {
            Name = name,
            Transform = pose,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("514132", "wood")
        };
        visual.SetMeta("physicalSubject", "one of the four broken diagonal boards preserved from the old bank approach");
        parent.AddChild(visual);

        var body = new StaticBody3D
        {
            Name = name + "Body",
            Transform = pose,
            CollisionLayer = 0u,
            CollisionMask = 0u
        };
        body.SetMeta("collisionOwner", name);
        body.SetMeta("footstepSurface", "wood");
        body.AddToGroup("photo_bank_approach_collision");
        body.AddChild(new CollisionShape3D
        {
            Name = name + "Contact",
            Shape = new BoxShape3D { Size = size }
        });
        parent.AddChild(body);
    }

    private static Basis ReflectOldApproachBasis(Vector3 sourceRotationDegrees)
    {
        // Positions are reflected across X. Apply the same reflection to the source pose,
        // then reflect the symmetric local X axis so Godot receives a proper rotation basis.
        var source = Basis.FromEuler(sourceRotationDegrees * Mathf.DegToRad(1f));
        static Vector3 ReflectX(Vector3 value) => new(-value.X, value.Y, value.Z);
        return new Basis(-ReflectX(source.X), ReflectX(source.Y), ReflectX(source.Z));
    }

    private static void AddBankTimber(Node3D parent, string name, Vector3 from, Vector3 to, float radius, string colour)
    {
        var axis = to - from;
        if (axis.LengthSquared() <= .0001f) return;
        var up = axis.Normalized();
        var side = Mathf.Abs(up.Dot(Vector3.Forward)) > .9f ? Vector3.Right : Vector3.Forward;
        var x = side.Cross(up).Normalized();
        var z = x.Cross(up).Normalized();
        var transform = new Transform3D(new Basis(x, up, z), (from + to) * .5f);
        var mesh = new CylinderMesh
        {
            TopRadius = radius * .72f,
            BottomRadius = radius,
            Height = axis.Length(),
            RadialSegments = 10,
            Rings = 1
        };
        var visual = new MeshInstance3D
        {
            Name = name,
            Transform = transform,
            Mesh = mesh,
            MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, "wood")
        };
        visual.SetMeta("physicalSubject", "weathered timber support of the older bank approach");
        parent.AddChild(visual);

        var body = new StaticBody3D
        {
            Name = name + "Body",
            Transform = transform,
            CollisionLayer = 0u,
            CollisionMask = 0u
        };
        body.SetMeta("collisionOwner", visual.GetPath().ToString());
        body.SetMeta("footstepSurface", "wood");
        body.AddToGroup("photo_bank_approach_collision");
        body.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = radius * .9f, Height = axis.Length() }
        });
        parent.AddChild(body);
    }
}
