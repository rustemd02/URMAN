using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// ACT1-DEPTH.3 / P2: the village feldsher-midwife post lit and furnished like a cared-for
/// clinic. Presentation only: working ceiling fixtures (soft emission plus real lamps), a
/// coat stand by the door, a height/weight scale with a cabinet, and a health poster.
/// Everything is visual; no collision, interaction, navigation or path-blocking is added.
/// </summary>
public partial class StyleBenchmarkZone
{
    private const string FapDressingName = "FapComfortDressing";
    private const string FapCeilingLensName = "FapInteriorShell_CeilingFixtureLens_LOD0";

    // Called after ClinicSurfacePresentation.Attach: its reflection-light registry (and the
    // five-light clinic contract) is already closed, so these lamps stay on layer 1 and never
    // enter the cached static mirror capture.
    private void DressFapInterior()
    {
        var root = new Node3D { Name = FapDressingName };
        root.SetMeta("presentationOnly", true);
        root.SetMeta("visualOnly", true);
        root.SetMeta("collisionOwner", "none");
        root.SetMeta("interactionOwner", "none");
        AddChild(root);

        // 1. Ceiling fixtures: kit fixture (found by name), legacy far fixture, one extra over the cot.
        var lensMaterial = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml("eef4ff"),
            Roughness = .55f,
            EmissionEnabled = true,
            Emission = Color.FromHtml("eef4ff"),
            EmissionEnergyMultiplier = 1.5f
        };
        var kitLensCentre = new Vector3(0f, 3.05f, 0.2f);
        var kitLens = Descendants(this).OfType<MeshInstance3D>()
            .FirstOrDefault(mesh => mesh.Name == FapCeilingLensName && mesh.Mesh is not null);
        if (kitLens is not null)
        {
            kitLens.MaterialOverride = lensMaterial;
            kitLens.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            kitLens.SetMeta("materialRole", "ceiling diffuser with soft cool-white emission");
            kitLensCentre = ZoneLocalPoint(kitLens, kitLens.Mesh!.GetAabb().GetCenter());
        }

        Vector3? farLensCentre = null;
        if (GetNodeOrNull<Node3D>("ClinicCeilingFixtureLensFar") is { } farLens)
        {
            foreach (var mesh in farLens.GetChildren().OfType<MeshInstance3D>())
            {
                mesh.MaterialOverride = lensMaterial;
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
            farLensCentre = farLens.Position;
        }

        // Extra fixture above the cot bay so the whole room, not one strip, is lit.
        var cotFixture = new Vector3(-3.6f, kitLensCentre.Y, -0.6f);
        AddFapPart(root, "FapCotBayFixtureHousing", RuralPropGeometry.Box(new(1.2f, .12f, .42f)),
            new(cotFixture.X, cotFixture.Y + .12f, cotFixture.Z),
            PainterlyMaterialLibrary.ForColor("65736d", "iron", sheltered: true));
        AddFapPart(root, "FapCotBayFixtureLens", RuralPropGeometry.Box(new(.84f, .025f, .2f)),
            new(cotFixture.X, cotFixture.Y - .005f, cotFixture.Z), lensMaterial);

        AddFapCeilingLight(root, "FapCeilingLightNear", kitLensCentre + Vector3.Down * .35f, 1.25f, 8.2f);
        AddFapCeilingLight(root, "FapCeilingLightFar",
            (farLensCentre ?? new Vector3(0f, 3.145f, 2.4f)) + Vector3.Down * .35f, 1.1f, 8.2f);
        AddFapCeilingLight(root, "FapCeilingLightCotBay", cotFixture + Vector3.Down * .35f, 1.0f, 6.6f);

        // 2. Coat stand next to the entrance (front wall inner face z~5.52, door x +-0.68).
        var wood = PainterlyMaterialLibrary.ForColor("6a5642", "wood_furniture_interior", sheltered: true);
        AddFapPart(root, "FapEntryCoatStandPole", new CylinderMesh
        {
            TopRadius = .022f, BottomRadius = .03f, Height = 1.75f, RadialSegments = 8
        }, new(-1.95f, .875f, 5.18f), wood);
        AddFapPart(root, "FapEntryCoatStandCoat", RuralPropGeometry.Box(new(.36f, .85f, .09f)),
            new(-1.95f, 1.18f, 5.12f), PainterlyMaterialLibrary.ForColor("5d6a62", "cloth_clinic", sheltered: true),
            rotationDegrees: new(0f, 8f, -2f));

        // 3. Wall cabinet with a column scale beside it (left wall inner face x~-5.56, below the exam chart).
        AddFapPart(root, "FapSupplyCabinetLeftWall", RuralPropGeometry.Box(new(.42f, .88f, .9f)),
            new(-5.33f, .44f, 4.3f), PainterlyMaterialLibrary.ForColor("e6e9e2", "plaster", sheltered: true));
        var scaleMetal = PainterlyMaterialLibrary.ForColor("b9c1bb", "iron", sheltered: true);
        AddFapPart(root, "FapMedicalScalePlatform", RuralPropGeometry.Box(new(.5f, .07f, .42f)),
            new(-5.1f, .035f, 5.12f), scaleMetal);
        AddFapPart(root, "FapMedicalScaleColumn", RuralPropGeometry.Box(new(.07f, 1.35f, .07f)),
            new(-5.3f, .745f, 5.28f), scaleMetal);

        // 4. Health-education poster on the free back-wall stretch between the notice board and the cabinets.
        var posterX = 2.45f;
        AddFapPart(root, "FapHealthPosterFrame", RuralPropGeometry.Box(new(.98f, 1.28f, .035f)),
            new(posterX, 1.92f, -5.605f), PainterlyMaterialLibrary.ForColor("7fa498", "plaster", sheltered: true));
        AddFapPart(root, "FapHealthPosterPaper", RuralPropGeometry.Box(new(.86f, 1.16f, .012f)),
            new(posterX, 1.92f, -5.583f), PainterlyMaterialLibrary.ForColor("efeadb", sheltered: true));
        var cross = PainterlyMaterialLibrary.ForColor("b5473f", sheltered: true);
        AddFapPart(root, "FapHealthPosterCrossBar", RuralPropGeometry.Box(new(.36f, .1f, .008f)),
            new(posterX, 2.12f, -5.574f), cross);
        AddFapPart(root, "FapHealthPosterCrossStem", RuralPropGeometry.Box(new(.1f, .36f, .008f)),
            new(posterX, 2.12f, -5.574f), cross);

        SetMeta("fapComfortDressing",
            "cool-white working ceiling lamps with emissive diffusers; entry coat stand; supply cabinet and column scale; health poster; presentation only");
    }

    private static void AddFapPart(
        Node3D parent, string name, Mesh mesh, Vector3 position, Material material, Vector3? rotationDegrees = null)
    {
        var node = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = position,
            RotationDegrees = rotationDegrees ?? Vector3.Zero,
            MaterialOverride = material
        };
        node.SetMeta("presentationOwnership", "presentation-only");
        node.SetMeta("collisionPolicy", "no physics body; visual mesh only");
        parent.AddChild(node);
    }

    private static void AddFapCeilingLight(Node3D parent, string name, Vector3 at, float energy, float range)
    {
        var light = new OmniLight3D
        {
            Name = name,
            Position = at,
            LightColor = Color.FromHtml("eef4ff"),
            LightEnergy = energy,
            OmniRange = range,
            LightSize = .25f,
            ShadowEnabled = GraphicsQuality.Preset == "high"
        };
        // Same soft-lamp convention as the other interiors; the quality preset toggles the group.
        light.AddToGroup(GraphicsQuality.SoftShadowLampGroup);
        parent.AddChild(light);
    }

    // The clinic zone may not be in the tree yet during build; compose transforms by hand.
    private Vector3 ZoneLocalPoint(Node3D node, Vector3 point)
    {
        for (Node? current = node; current is Node3D spatial && current != this; current = current.GetParent())
            point = spatial.Transform * point;
        return point;
    }
}
