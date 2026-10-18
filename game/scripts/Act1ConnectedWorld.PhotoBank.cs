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
        var bankGround = entry.Y - .05f;
        var ravineCenter = (float)AgentBAct1HeightField.RavineCentre(entryZ);
        const float pileTopOffset = .38f;
        var pileTopY = bankGround + pileTopOffset;

        // Two old support rows remain in the stream cut. Their short edge
        // stringers survive, while the centre span is absent, so they read as
        // piles from this safe shore rather than as another usable bridge.
        foreach (var rowZ in new[] { entryZ - .95f, entryZ + .95f })
        foreach (var across in new[] { -3.3f, -1.1f, 1.1f, 3.3f })
        {
            var z = rowZ;
            var x = (float)AgentBAct1HeightField.RavineCentre(z) + across;
            var floor = AgentBAct1HeightField.CollisionGround(x, z);
            AddBankTimber(bank, $"OldApproachPile_{(rowZ < entryZ ? "North" : "South")}_{across:0.0}",
                new Vector3(x, floor - .28f, z), new Vector3(x, pileTopY, z), .145f, "514132");
        }

        foreach (var across in new[] { -3.3f, 3.3f })
        {
            AddBankTimber(bank, $"OldApproachEdgeStringer_{across:0.0}",
                new Vector3((float)AgentBAct1HeightField.RavineCentre(entryZ - .95f) + across, pileTopY - .08f, entryZ - .95f),
                new Vector3((float)AgentBAct1HeightField.RavineCentre(entryZ + .95f) + across, pileTopY - .08f, entryZ + .95f),
                .105f, "5b4938");
        }

        var entryMarker = new Marker3D
        {
            Name = "BankEntry",
            Position = entry,
            RotationDegrees = new Vector3(0f, PhotoBankApproachEntryYawDegrees, 0f)
        };
        bank.AddChild(entryMarker);

        var nearPileX = (float)AgentBAct1HeightField.RavineCentre(entryZ - .95f) - 3.3f;
        AddBankMarker(bank, "OldPilesWitness", new Vector3(nearPileX, pileTopY, entryZ - .95f));

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
