using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// The attic room of the babai house, made readable from outside. The hero
/// house model is a log house with a low gable roof and nothing that says
/// "someone lives under it". Aidar's bed is up there, so the roof gets what
/// such a house really has: a boarded shed dormer on the yard slope and a
/// small lit window in the street gable. Everything is measured from the
/// model's own roof mesh, built from its own materials, and is presentation
/// only (no collision, no ids). The house footprint with its porch and seni
/// is also published to the snow exclusions so no drift is laid into it.
/// </summary>
public partial class Act1ConnectedWorld
{
    // Tuning, in metres, in the model's own frame (ridge runs along Z).
    private const float MansardDormerSetback = 1.3f;   // front wall, up-slope from the eave
    private const float MansardDormerWall = .95f;      // height of the dormer front
    private const float MansardDormerHalfWidth = 1.2f;
    private const float MansardDormerFromStreetGable = 3.2f;
    private const float MansardDormerRoofRise = .1f;   // rise per metre of the dormer roof
    private static readonly Color MansardLampGlow = new(1f, .72f, .4f);

    private MeshInstance3D? FindHeroHouseMesh(string prefix)
    {
        var facade = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox/Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade");
        return facade?.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .FirstOrDefault(mesh => mesh.Mesh is not null && mesh.Name.ToString().StartsWith(prefix, StringComparison.Ordinal)
                && mesh.Name.ToString().Contains("LOD0", StringComparison.Ordinal));
    }

    private static Material HeroHouseMaterial(MeshInstance3D? source, string fallbackColour, string fallbackSurface) =>
        source?.MaterialOverride ?? (source?.Mesh is { } mesh && mesh.GetSurfaceCount() > 0 ? mesh.SurfaceGetMaterial(0) : null)
        ?? PainterlyMaterialLibrary.ForColor(fallbackColour, fallbackSurface);

    private void DressBabaiMansard()
    {
        if (FindHeroHouseMesh("HeroHouse_Roof_") is not { } roof || roof.GetParent() is not Node3D frame
            || frame.HasNode("BabaiMansard")) return;
        var bounds = roof.Transform * roof.Mesh.GetAabb();
        var eaveY = bounds.Position.Y;
        var ridgeY = bounds.End.Y;
        var eaveX = bounds.End.X;
        var ridgeX = bounds.GetCenter().X;
        if (ridgeY - eaveY < 1f || eaveX - ridgeX < 2.5f) return; // not the roof this was measured for
        var slope = (ridgeY - eaveY) / (eaveX - ridgeX);
        var roofMaterial = HeroHouseMaterial(roof, "6f3f35", "metal");
        var boardMaterial = HeroHouseMaterial(FindHeroHouseMesh("HeroHouse_Front_BoardedGable"), "8a7458", "wood");
        var snowMaterial = HeroHouseMaterial(FindHeroHouseMesh("HeroHouse_RoofSnow"), "eef2f6", "snow_ground");
        var trimMaterial = PainterlyMaterialLibrary.ForColor("e6e0cf", "wood_painted_trim");
        var glass = new StandardMaterial3D
        {
            AlbedoColor = new Color(.68f, .76f, .80f, .18f), Roughness = .16f, MetallicSpecular = .55f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        var root = new Node3D { Name = "BabaiMansard" };
        root.SetMeta("presentationOnly", true);
        root.SetMeta("collisionOwner", "none");
        frame.AddChild(root);

        MeshInstance3D Piece(string name, Vector3 size, Vector3 at, Material material)
        {
            var piece = new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size }, Position = at, MaterialOverride = material };
            root.AddChild(piece);
            return piece;
        }
        // A window with a plain surround: jambs, head, sill, one mullion and one transom.
        void Window(string name, Vector3 centre, float width, float height, Vector3 across, Vector3 outward)
        {
            var basis = new Basis(across, Vector3.Up, outward);
            var holder = new Node3D { Name = name, Transform = new Transform3D(basis, centre) };
            root.AddChild(holder);
            void Bar(string part, Vector3 size, Vector3 at, Material material) =>
                holder.AddChild(new MeshInstance3D { Name = part, Mesh = new BoxMesh { Size = size }, Position = at, MaterialOverride = material });
            Bar("Glass", new(width, height, .012f), new(0, 0, -.025f), glass);
            holder.GetNode<MeshInstance3D>("Glass").CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            foreach (var side in new[] { -1f, 1f })
                Bar("Reveal" + side, new(.045f, height, .22f), new(side * (width * .5f + .0225f), 0, -.08f), boardMaterial);
            holder.AddChild(new SpotLight3D { Name = "RoomWindowLight", Position = new(0, .04f, -.55f),
                RotationDegrees = new(0, 180, 0), LightColor = MansardLampGlow, LightEnergy = .45f,
                SpotRange = 4.5f, SpotAngle = 48f, ShadowEnabled = true, ShadowBias = .015f, LightSize = .08f });
            foreach (var side in new[] { -1f, 1f })
                Bar("Jamb" + side, new(.09f, height + .18f, .07f), new(side * (width * .5f + .045f), 0, .045f), trimMaterial);
            Bar("Head", new(width + .34f, .11f, .09f), new(0, height * .5f + .075f, .05f), trimMaterial);
            Bar("Sill", new(width + .3f, .06f, .13f), new(0, -height * .5f - .05f, .07f), trimMaterial);
            Bar("Mullion", new(.035f, height, .035f), new(0, 0, .04f), trimMaterial);
            Bar("Transom", new(width, .035f, .035f), new(0, height * .18f, .04f), trimMaterial);
            Bar("SillSnow", new(width + .26f, .04f, .1f), new(0, -height * .5f - .0f, .08f), snowMaterial);
        }

        // ---- Shed dormer on the yard-side (+X) slope ----------------------------------
        var frontX = eaveX - MansardDormerSetback;
        var footY = eaveY + (eaveX - frontX) * slope;
        var topY = footY + MansardDormerWall;
        var streetGableZ = bounds.End.Z - .6f;
        var centreZ = streetGableZ - MansardDormerFromStreetGable;
        var half = MansardDormerHalfWidth;
        // Where the nearly flat dormer roof runs back into the main slope.
        var lipX = frontX + .32f;
        var lipY = topY + .05f;
        var meetX = (lipY + lipX * MansardDormerRoofRise - eaveY - eaveX * slope) / (MansardDormerRoofRise - slope);
        meetX = Mathf.Clamp(meetX, ridgeX + .1f, frontX - .6f);
        var meetY = eaveY + (eaveX - meetX) * slope + .03f;

        var front = Piece("DormerFront", new(.1f, MansardDormerWall + .3f, half * 2f), new(frontX, footY + MansardDormerWall * .5f - .15f, centreZ), boardMaterial);
        CutBabaiMeshOpening(front, root, new Aabb(new(frontX-.20f, footY+MansardDormerWall*.52f-.31f, centreZ-.525f), new(.40f,.62f,1.05f)));
        // The main sheet and settled snow must be open behind the new dormer,
        // otherwise its glass simply reveals another opaque roof surface.
        foreach (var roofPart in frame.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().Where(m =>
                     m.Name.ToString().StartsWith("HeroHouse_Roof", StringComparison.Ordinal)).ToArray())
            CutBabaiMeshOpening(roofPart, frame, new Aabb(new(meetX, footY-.35f, centreZ-half+.05f), new(frontX-meetX+.06f, topY-footY+.75f, half*2f-.10f)));
        foreach (var side in new[] { -1f, 1f })
            Piece("DormerCornerBoard" + side, new(.13f, MansardDormerWall + .1f, .12f), new(frontX + .02f, footY + MansardDormerWall * .5f, centreZ + side * half), trimMaterial);
        Window("DormerWindow", new Vector3(frontX + .05f, footY + MansardDormerWall * .52f, centreZ), 1.05f, .62f, Vector3.Forward, Vector3.Right);

        using (var surface = new SurfaceTool())
        {
            // Cheeks: the triangle between the dormer roof and the main slope, both sides visible.
            surface.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var side in new[] { -1f, 1f })
            {
                var z = centreZ + side * half;
                var a = new Vector3(frontX, footY - .05f, z);
                var b = new Vector3(frontX, topY, z);
                var c = new Vector3(meetX, meetY, z);
                var normal = new Vector3(0, 0, side);
                foreach (var point in side > 0 ? new[] { a, b, c, a, c, b } : new[] { a, c, b, a, b, c })
                {
                    surface.SetNormal(normal);
                    surface.SetUV(new Vector2(point.X * .5f, point.Y * .5f));
                    surface.AddVertex(point);
                }
            }
            root.AddChild(new MeshInstance3D { Name = "DormerCheeks", Mesh = surface.Commit(), MaterialOverride = boardMaterial });
        }
        // Roof sheet and the snow lying on it, as two thin slabs along the same line.
        var run = new Vector2(lipX - meetX, lipY - meetY);
        var pitch = Mathf.Atan2(run.Y, run.X);
        var middle = new Vector3((lipX + meetX) * .5f, (lipY + meetY) * .5f, centreZ);
        var sheet = Piece("DormerRoof", new(run.Length() + .1f, .07f, half * 2f + .5f), middle, roofMaterial);
        sheet.Rotation = new Vector3(0, 0, pitch);
        var snow = Piece("DormerRoofSnow", new(run.Length() - .1f, .09f, half * 2f + .3f), middle + Vector3.Up * .075f, snowMaterial);
        snow.Rotation = new Vector3(0, 0, pitch);
        Piece("DormerFascia", new(.05f, .16f, half * 2f + .5f), new(lipX + .02f, lipY - .05f, centreZ), trimMaterial);

        // ---- Attic window in the street gable ----------------------------------------
        if (FindHeroHouseMesh("HeroHouse_Front_BoardedGable") is { } gable)
        {
            var plane = (gable.Transform * gable.Mesh.GetAabb()).End.Z;
            var windowY = eaveY + .88f;
            foreach (var part in frame.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                         .Where(m => m.Name.ToString().StartsWith("HeroHouse_Front_BoardedGable", StringComparison.Ordinal)).ToArray())
                CutBabaiMeshOpening(part, frame, new Aabb(new(ridgeX-.39f, windowY-.41f, plane-.60f), new(.78f,.82f,.80f)));
            Window("GableAtticWindow", new Vector3(ridgeX, windowY, plane + .01f), .78f, .82f, Vector3.Right, Vector3.Back);
        }
        foreach (var contact in FindDescendants<CollisionShape3D>(this).Where(c => c.HasMeta("authoredSourceMesh")))
            if (GetNodeOrNull<MeshInstance3D>(contact.GetMeta("authoredSourceMesh").AsString()) is { } source && source.HasMeta("realWindowOpening"))
                contact.Shape = source.Mesh.CreateTrimeshShape();
        root.SetMeta("presentationRole", "pierced dormer and gable windows with recessed glass and physical frame shadows");
        GD.Print($"act1-babai-mansard: dormer frontX={frontX:0.00} foot={footY:0.00} top={topY:0.00} meetX={meetX:0.00} ridgeX={ridgeX:0.00} slope={slope:0.000}");
    }

    /// <summary>The whole house plot of the hero house (walls, porch, seni, steps, eaves) as one snow exclusion.</summary>
    private bool PublishBabaiHouseFootprint()
    {
        if (FindHeroHouseMesh("HeroHouse_Roof_") is not { } roof || roof.GetParent() is not Node3D frame) return false;
        var bounds = roof.Transform * roof.Mesh.GetAabb();
        // The seni lean-to reaches 1.6 m past the yard eave and the porch 0.6 m past the street verge.
        var min = new Vector2(bounds.Position.X - .5f, bounds.Position.Z - .5f);
        var max = new Vector2(bounds.End.X + 2.2f, bounds.End.Z + 1.3f);
        var polygon = new[] { new Vector2(min.X, min.Y), new Vector2(max.X, min.Y), new Vector2(max.X, max.Y), new Vector2(min.X, max.Y) }
            .Select(corner => frame.ToGlobal(new Vector3(corner.X, 0f, corner.Y))).Select(world => new Vector2(world.X, world.Z)).ToArray();
        return SnowReliefStandard.PublishFootprint("footprint:babai-hero-house-plot", polygon);
    }
}
