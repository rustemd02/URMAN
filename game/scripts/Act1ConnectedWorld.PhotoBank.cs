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
        var leafCenter = twigTip + new Vector3(-.12f, .10f, .03f);
        var leaf = new MeshInstance3D
        {
            Name = "PersistentWinterLeaf",
            Position = leafCenter,
            RotationDegrees = new Vector3(12f, 18f, -27f),
            Scale = new Vector3(.09f, .20f, .028f),
            Mesh = new SphereMesh { Radius = .5f, Height = 1f, RadialSegments = 10, Rings = 5 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("5d7351", "foliage")
        };
        leaf.SetMeta("physicalSubject", "single green leaf held through winter on the bank approach twig");
        bank.AddChild(leaf);
        var leafBody = new StaticBody3D
        {
            Name = "PersistentWinterLeafBody",
            Position = leafCenter,
            CollisionLayer = 0u,
            CollisionMask = 0u
        };
        leafBody.AddToGroup("photo_bank_approach_collision");
        leafBody.AddChild(new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = .075f }
        });
        bank.AddChild(leafBody);
        AddBankMarker(bank, "LeafWitness", leafCenter);

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
