using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class W04Controller
{
    private const string KaraForestKitPath = "res://assets/models/act1/urman_kara_forest_edge_kit.glb";
    private const string HouseKitPath = "res://assets/models/act1/urman_village_exterior_kit.glb";
    private const float GroundMinX = -90f;
    private const float GroundMaxX = 90f;
    private const float GroundMinZ = -10f;
    private const float GroundMaxZ = 190f;
    private const float GroundStep = 1f;
    private const float GroundAnchorSigma = 22f;

    private static readonly Vector3[] GroundAnchors =
    [
        new(0f, 0f, 0f), new(-25f, 3f, 25f), new(24f, 2f, 30f),
        new(0f, 4f, 54f), new(0f, 5f, 83f), new(0f, 7f, 112f),
        new(-22f, 6f, 140f), new(25f, 6f, 143f), new(0f, 5f, 170f)
    ];

    private static readonly Vector2[][] CommonWalkRoutes =
    [
        [new(0f, 0f), new(-25f, 25f)], [new(0f, 0f), new(24f, 30f)],
        [new(-25f, 25f), new(0f, 54f)], [new(24f, 30f), new(0f, 54f)],
        [new(0f, 54f), new(0f, 83f)], [new(0f, 83f), new(0f, 112f)],
        [new(0f, 112f), new(-22f, 140f)], [new(0f, 112f), new(25f, 143f)],
        [new(-22f, 140f), new(0f, 170f)], [new(25f, 143f), new(0f, 170f)]
    ];

    private static readonly float[] GroundAnchorWeights = SolveGroundAnchorWeights();
    private Node3D _world = null!;

    private void BuildCommonWorld()
    {
        _world = new Node3D { Name = "W04World", Visible = true };
        _world.SetMeta("presentationRole", "180 by 200 metre common landscape with one inaccessible shared panorama");
        AddChild(_world);

        BuildCommonGround();
        BuildCommonHorizon();
        BuildCommonFoliage();
        BuildCommonHouse();
        BuildCommonFrames();
        BuildCommonClothAndSupport();
        BuildCommonCaptionDesk();
    }

    private void BuildCommonGround()
    {
        var columns = Mathf.RoundToInt((GroundMaxX - GroundMinX) / GroundStep) + 1;
        var rows = Mathf.RoundToInt((GroundMaxZ - GroundMinZ) / GroundStep) + 1;
        var positions = new Vector3[columns * rows];
        var normals = new Vector3[positions.Length];
        var colors = new Color[positions.Length];

        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
        {
            var index = row * columns + column;
            var x = GroundMinX + column * GroundStep;
            var z = GroundMinZ + row * GroundStep;
            positions[index] = new Vector3(x, CommonGroundHeightAt(x, z), z);
            const float normalDelta = .2f;
            var dx = (CommonGroundHeightAt(x + normalDelta, z) - CommonGroundHeightAt(x - normalDelta, z))
                / (2f * normalDelta);
            var dz = (CommonGroundHeightAt(x, z + normalDelta) - CommonGroundHeightAt(x, z - normalDelta))
                / (2f * normalDelta);
            normals[index] = new Vector3(-dx, 1f, -dz).Normalized();
            colors[index] = CommonGroundColor(x, z);
        }

        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var faces = new List<Vector3>((columns - 1) * (rows - 1) * 6);
        void Emit(int index)
        {
            var point = positions[index];
            surface.SetNormal(normals[index]);
            surface.SetColor(colors[index]);
            surface.SetUV(new Vector2(point.X, point.Z) * .12f);
            surface.AddVertex(point);
            faces.Add(point);
        }

        for (var row = 0; row + 1 < rows; row++)
        for (var column = 0; column + 1 < columns; column++)
        {
            var a = row * columns + column;
            var b = a + columns;
            var c = a + 1;
            var d = b + 1;
            Emit(a); Emit(d); Emit(b);
            Emit(a); Emit(c); Emit(d);
        }

        var ground = new MeshInstance3D
        {
            Name = "W04GroundSurface",
            Mesh = surface.Commit(),
            MaterialOverride = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                Roughness = .96f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        ground.SetMeta("presentationRole", "continuous shaped meadow and all ten broad walking links");
        ground.SetMeta("footstepSurface", "grass");
        ground.SetMeta("passportBoundsMetres", "180x200; x[-90,90], z[-10,190]");
        _world.AddChild(ground);

        var body = new StaticBody3D { Name = "W04GroundBody", CollisionLayer = 1u, CollisionMask = 1u };
        body.SetMeta("contactPolicy", "concave triangles generated from the same continuous passport height field");
        body.SetMeta("footstepSurface", "grass");
        _world.AddChild(body);
        body.AddChild(new CollisionShape3D
        {
            Name = "W04GroundTriangles",
            Shape = new ConcavePolygonShape3D { BackfaceCollision = true, Data = faces.ToArray() }
        });
    }

    private static float CommonGroundHeightAt(float x, float z)
    {
        var height = z * (5f / 170f);
        var denominator = 2f * GroundAnchorSigma * GroundAnchorSigma;
        for (var index = 0; index < GroundAnchors.Length; index++)
        {
            var anchor = GroundAnchors[index];
            var dx = x - anchor.X;
            var dz = z - anchor.Z;
            height += GroundAnchorWeights[index] * Mathf.Exp(-(dx * dx + dz * dz) / denominator);
        }
        return height;
    }

    private static float[] SolveGroundAnchorWeights()
    {
        var count = GroundAnchors.Length;
        var matrix = new double[count, count + 1];
        var denominator = 2d * GroundAnchorSigma * GroundAnchorSigma;
        for (var row = 0; row < count; row++)
        {
            var source = GroundAnchors[row];
            for (var column = 0; column < count; column++)
            {
                var target = GroundAnchors[column];
                var dx = source.X - target.X;
                var dz = source.Z - target.Z;
                matrix[row, column] = Math.Exp(-(dx * dx + dz * dz) / denominator);
            }
            matrix[row, count] = source.Y - source.Z * (5d / 170d);
        }

        for (var pivot = 0; pivot < count; pivot++)
        {
            var best = pivot;
            for (var row = pivot + 1; row < count; row++)
                if (Math.Abs(matrix[row, pivot]) > Math.Abs(matrix[best, pivot])) best = row;
            if (Math.Abs(matrix[best, pivot]) < 1e-10)
                throw new InvalidOperationException("W04 passport ground anchors do not define a stable height field.");
            if (best != pivot)
                for (var column = pivot; column <= count; column++)
                    (matrix[pivot, column], matrix[best, column]) = (matrix[best, column], matrix[pivot, column]);

            var scale = matrix[pivot, pivot];
            for (var column = pivot; column <= count; column++) matrix[pivot, column] /= scale;
            for (var row = 0; row < count; row++)
            {
                if (row == pivot) continue;
                var factor = matrix[row, pivot];
                for (var column = pivot; column <= count; column++)
                    matrix[row, column] -= factor * matrix[pivot, column];
            }
        }

        var weights = new float[count];
        for (var index = 0; index < count; index++) weights[index] = (float)matrix[index, count];
        return weights;
    }

    private static Color CommonGroundColor(float x, float z)
    {
        var distance = CommonRouteDistance(new Vector2(x, z));
        var track = 1f - Mathf.SmoothStep(1.2f, 3.2f, distance);
        var brush = .5f + .5f * Mathf.Sin(x * .083f + z * .061f) * Mathf.Sin(z * .097f - x * .039f);
        var meadow = new Color(.38f + brush * .035f, .48f + brush * .035f, .36f + brush * .03f);
        var path = new Color(.56f + brush * .045f, .48f + brush * .035f, .37f + brush * .03f);
        return meadow.Lerp(path, track * .78f);
    }

    private static float CommonRouteDistance(Vector2 point)
    {
        var closest = float.MaxValue;
        foreach (var route in CommonWalkRoutes)
        {
            var start = route[0];
            var run = route[1] - start;
            var t = Mathf.Clamp((point - start).Dot(run) / run.LengthSquared(), 0f, 1f);
            closest = Mathf.Min(closest, point.DistanceTo(start + run * t));
        }
        return closest;
    }

    private void BuildCommonFrames()
    {
        BuildCommonFrame("C01ForestFrame", "C01", "765b42", "60777a");
        BuildCommonFrame("C02RiverFrame", "C02", "6e5740", "55747b");
        BuildCommonFrame("C03HouseFrame", "C03", "765843", "697586");
    }

    private void BuildCommonFrame(string name, string anchorName, string woodTint, string blueTint)
    {
        var anchor = GetNodeOrNull<Node3D>($"Anchors/{anchorName}")
            ?? throw new InvalidOperationException($"W04 is missing its fixed passport anchor: {anchorName}");
        var frame = new StaticBody3D
        {
            Name = name,
            Position = anchor.GlobalPosition,
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        frame.SetMeta("presentationRole", "full-scale timber sight frame with an unobstructed 3.2 metre passage");
        frame.SetMeta("footstepSurface", "grass");
        frame.SetMeta("apertureWidthMetres", 3.2f);
        frame.SetMeta("apertureHeadroomMetres", 3.8f);
        _world.AddChild(frame);

        var timber = RuralPropMaterials.Surface("wood", woodTint);
        var blue = RuralPropMaterials.Surface("wood", blueTint);
        var stone = RuralPropMaterials.Surface("concrete", "c9beaa");
        const float postX = 1.76f;
        const float postHeight = 3.9f;
        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * postX;
            var groundDelta = CommonGroundHeightAt(anchor.Position.X + x, anchor.Position.Z) - anchor.Position.Y;
            AddCommonSolidBlock(frame, side < 0 ? "WestFoot" : "EastFoot",
                new Vector3(.52f, .18f, .58f), new Vector3(x, groundDelta + .09f, 0f), stone, .025f);
            AddCommonSolidBlock(frame, side < 0 ? "WestPost" : "EastPost",
                new Vector3(.28f, postHeight, .32f), new Vector3(x, groundDelta + postHeight * .5f + .12f, 0f), timber, .022f);
            AddCommonSolidBlock(frame, side < 0 ? "WestBlueCollar" : "EastBlueCollar",
                new Vector3(.34f, .20f, .36f), new Vector3(x, groundDelta + .38f, .015f), blue, .014f);
        }

        AddCommonSolidBlock(frame, "DeepTimberLintel", new Vector3(3.98f, .38f, .48f),
            new Vector3(0f, 4.06f, 0f), timber, .028f);
        AddCommonSolidBlock(frame, "BlueLintelInlay", new Vector3(2.32f, .09f, .51f),
            new Vector3(0f, 4.08f, .015f), blue, .015f);
        AddCommonSolidTube(frame, "WestShoulderBrace", new Vector3(-1.55f, 2.95f, .08f),
            new Vector3(-.92f, 3.82f, .08f), .065f, timber);
        AddCommonSolidTube(frame, "EastShoulderBrace", new Vector3(1.55f, 2.95f, .08f),
            new Vector3(.92f, 3.82f, .08f), .065f, timber);
    }

    private void BuildCommonHorizon()
    {
        var horizon = new MeshInstance3D
        {
            Name = "CommonDistantPanorama",
            Mesh = CreateCommonPanoramaMesh(),
            MaterialOverride = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                Roughness = .98f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        horizon.SetMeta("presentationRole", "one continuous inaccessible ridgeline shared by the three C05 apertures");
        horizon.SetMeta("accessPolicy", "visual-only beyond the north edge of the continuous W04 ground");
        _world.AddChild(horizon);

        AddCommonWitness("C01FrameWitness", new Vector3(-100.5f, 4.12f, -62.58f),
            "upper surface of the actual far forest mass visible through C01 from C03",
            "C03 eye ray crosses the centre of the C01 aperture and ends on the shared forest subject");
        AddCommonWitness("C02FrameWitness", new Vector3(117f, 1.55f, -63f),
            "actual distant water surface visible through C02 from C03",
            "C03 eye ray crosses the centre of the C02 aperture and ends on the shared water subject");
        AddCommonWitness("C03FrameWitness", new Vector3(-4.2f, 1f, -62f),
            "front-door surface of the actual shared hero house beyond C03",
            "C03 eye ray begins in the centre of the C03 aperture and ends on the shared house");
        AddCommonWitness("CommonHorizonWitness", new Vector3(-16.5f, -2.1272123f, -56f),
            "actual lower-slope surface of the continuous shared panorama, viewed from C05",
            "C05 eye ray intersects the hanging C04 cloth before reaching this real panorama surface");

        BuildCommonDistantWater();
        BuildCommonDistantSubjects();
    }

    private static ArrayMesh CreateCommonPanoramaMesh()
    {
        const int columns = 161;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var ridge = new Vector3[columns, 4];
        var color = new Color[columns, 4];
        for (var column = 0; column < columns; column++)
        {
            var x = -120f + column * 1.5f;
            var profile = 10.6f + 2.6f * Mathf.Sin(x * .028f + .4f)
                + 1.8f * Mathf.Sin(x * .071f - .8f) + 1.1f * Mathf.Cos(x * .013f);
            ridge[column, 0] = new Vector3(x, -3f, -55f);
            ridge[column, 1] = new Vector3(x, profile * .72f, -66f + Mathf.Sin(x * .022f) * 1.6f);
            ridge[column, 2] = new Vector3(x, profile, -81f + Mathf.Cos(x * .018f) * 1.5f);
            ridge[column, 3] = new Vector3(x, -3f, -94f);
            var variation = .5f + .5f * Mathf.Sin(x * .11f) * Mathf.Sin(x * .037f + 1.2f);
            color[column, 0] = new Color(.26f + variation * .035f, .36f + variation * .04f, .34f + variation * .035f);
            color[column, 1] = new Color(.34f + variation * .04f, .44f + variation * .04f, .44f + variation * .045f);
            color[column, 2] = new Color(.46f + variation * .06f, .49f + variation * .05f, .59f + variation * .07f);
            color[column, 3] = new Color(.25f + variation * .035f, .33f + variation * .035f, .38f + variation * .04f);
        }

        void Triangle(int a, int layerA, int b, int layerB, int c, int layerC)
        {
            foreach (var (column, layer) in new[] { (a, layerA), (b, layerB), (c, layerC) })
            {
                surface.SetColor(color[column, layer]);
                surface.SetUV(new Vector2(ridge[column, layer].X, ridge[column, layer].Z) * .08f);
                surface.AddVertex(ridge[column, layer]);
            }
        }

        for (var column = 0; column + 1 < columns; column++)
        for (var layer = 0; layer < 3; layer++)
        {
            Triangle(column, layer, column + 1, layer, column + 1, layer + 1);
            Triangle(column, layer, column + 1, layer + 1, column, layer + 1);
        }
        surface.GenerateNormals();
        return surface.Commit();
    }

    private void BuildCommonDistantWater()
    {
        var water = new MeshInstance3D
        {
            Name = "CommonDistantWater",
            Mesh = new PlaneMesh { Size = new Vector2(17f, 8f), SubdivideWidth = 12, SubdivideDepth = 6 },
            Position = new Vector3(117f, 1.55f, -63f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml("#547b85"),
                Roughness = .32f,
                Metallic = 0f,
                MetallicSpecular = .42f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        water.SetMeta("presentationRole", "quiet blue water behind the C02 frame, within the inaccessible distant view");
        _world.AddChild(water);
    }

    private void BuildCommonDistantSubjects()
    {
        BuildKaraComponents(
        [
            ("DistantForestMass_Tall", "CommonForestMassTall", new Vector3(-48f, 2f, -62f), -4f, Vector3.One * .90f),
            // The source-local top centre of DeepCanopy_00 lands exactly on the C01 witness.
            ("DistantForestMass_Low", "CommonForestMassLow", new Vector3(-90.292f, 2.3424f, -63.5656f), 0f, Vector3.One * .88f)
        ]);
    }

    private void BuildCommonFoliage()
    {
        BuildKaraComponents(
        [
            ("MixedTreeCluster_Left", "C01NearMixedTrees", new Vector3(-39f, CommonGroundHeightAt(-39f, 24f), 24f), -12f, Vector3.One * .92f),
            ("BirchEdgeMass", "C02NearBirchEdge", new Vector3(39f, CommonGroundHeightAt(39f, 35f), 35f), 15f, Vector3.One * .95f),
            ("CrookedPineMass", "C06FarCrookedPines", new Vector3(-57f, CommonGroundHeightAt(-57f, 123f), 123f), 9f, Vector3.One * .82f)
        ]);
    }

    private void BuildKaraComponents((string Source, string Name, Vector3 Position, float Yaw, Vector3 Scale)[] placements)
    {
        if (!ResourceLoader.Exists(KaraForestKitPath))
            throw new InvalidOperationException($"The authored W04 forest-edge kit is missing: {KaraForestKitPath}");
        var packed = ResourceLoader.Load<PackedScene>(KaraForestKitPath)
            ?? throw new InvalidOperationException($"The authored W04 forest-edge kit could not load: {KaraForestKitPath}");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException($"The authored W04 forest-edge kit has no Node3D root: {KaraForestKitPath}");

        foreach (var placementData in placements)
        {
            var source = FindCommonNode<Node3D>(importedRoot, placementData.Source)
                ?? throw new InvalidOperationException($"The W04 forest-edge kit is missing {placementData.Source}.");
            var clone = source.Duplicate() as Node3D
                ?? throw new InvalidOperationException($"The W04 forest-edge component could not be duplicated: {placementData.Source}");
            var basis = Act1ConnectedWorld.ComposeImportedAncestorBasis(source);
            Act1ConnectedWorld.ClearExtractedSceneOwnership(clone);
            clone.Transform = new Transform3D(basis, Vector3.Zero);
            clone.SetMeta("assetSource", KaraForestKitPath);
            clone.SetMeta("authoredComponent", placementData.Source);
            clone.SetMeta("collisionOwner", "none; imported kit is documented presentation-only");

            var placement = new Node3D
            {
                Name = placementData.Name,
                Position = placementData.Position,
                RotationDegrees = new Vector3(0f, placementData.Yaw, 0f),
                Scale = placementData.Scale
            };
            placement.SetMeta("assetSource", KaraForestKitPath);
            _world.AddChild(placement);
            placement.AddChild(clone);
        }
        importedRoot.Free();
    }

    private void BuildCommonHouse()
    {
        if (!ResourceLoader.Exists(HouseKitPath))
            throw new InvalidOperationException($"The authored W04 house kit is missing: {HouseKitPath}");
        var packed = ResourceLoader.Load<PackedScene>(HouseKitPath)
            ?? throw new InvalidOperationException($"The authored W04 house kit could not load: {HouseKitPath}");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException($"The authored W04 house kit has no Node3D root: {HouseKitPath}");
        var component = FindCommonNode<Node3D>(importedRoot, "HeroHouse_TimberPlaster")
            ?? throw new InvalidOperationException("The W04 house kit is missing HeroHouse_TimberPlaster.");
        var sourceParent = component.GetParent()
            ?? throw new InvalidOperationException("The W04 HeroHouse component has no imported parent.");
        var importedBasis = Act1ConnectedWorld.ComposeImportedAncestorBasis(component);
        sourceParent.RemoveChild(component);
        Act1ConnectedWorld.ClearExtractedSceneOwnership(component);
        importedRoot.Free();

        var door = new Vector3(-4.2f, 1.0f, -62f);
        var placement = new Node3D
        {
            Name = "CommonHousePlacement",
            Position = new Vector3(door.X - 1.1644f, door.Y - .215f, door.Z + 2.3f),
            RotationDegrees = new Vector3(0f, 180f, 0f)
        };
        placement.SetMeta("assetSource", HouseKitPath);
        placement.SetMeta("componentSource", "HeroHouse_TimberPlaster");
        _world.AddChild(placement);
        component.Transform = new Transform3D(importedBasis, Vector3.Zero);
        placement.AddChild(component);

        foreach (var mesh in FindCommonNodes<MeshInstance3D>(component))
        {
            if (!IsCommonWinterDecoration(mesh.Name.ToString())) continue;
            mesh.Visible = false;
            mesh.SetMeta("seasonalFilter", "only confirmed snow caps and icicles removed from the summer copy");
        }

        var body = new StaticBody3D { Name = "CommonHouseBody", CollisionLayer = 1u, CollisionMask = 1u };
        body.SetMeta("contactPolicy", "actual imported HeroHouse LOD0 surfaces; door leaf and street glazing excluded");
        body.SetMeta("footstepSurface", "wood");
        _world.AddChild(body);
        var glass = FindCommonNode<MeshInstance3D>(component, "HeroHouse_Street_Window1_Glass_LOD0");
        var contacts = 0;
        foreach (var mesh in FindCommonNodes<MeshInstance3D>(component))
        {
            var name = mesh.Name.ToString();
            if (mesh.Mesh is null || !name.EndsWith("_LOD0", StringComparison.Ordinal)
                || name.Contains("DoorClosed", StringComparison.Ordinal)
                || IsCommonWinterDecoration(name) || ReferenceEquals(mesh, glass)) continue;
            var faces = mesh.Mesh.GetFaces();
            if (faces.Length < 3) continue;
            body.AddChild(Act1ConnectedWorld.AuthoredSurfaceContact(body, mesh));
            contacts++;
        }
        if (contacts == 0) throw new InvalidOperationException("The W04 imported hero house produced no authored collision surfaces.");
        body.SetMeta("authoredContactSurfaceCount", contacts);
    }

    private void BuildCommonClothAndSupport()
    {
        const float clothX = -2.85f;
        const float clothZ = 81f;
        var ground = CommonGroundHeightAt(clothX, clothZ);
        var rack = new StaticBody3D
        {
            Name = "C04ClothRackBody",
            Position = new Vector3(clothX, ground, clothZ),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        rack.SetMeta("footstepSurface", "wood");
        rack.SetMeta("presentationRole", "physical drying rail beside the clear C03-C04-C05 route");
        _world.AddChild(rack);
        var wood = RuralPropMaterials.Surface("wood", "765b43");
        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * 1.0f;
            var supportHeight = CommonGroundHeightAt(clothX + x, clothZ) - ground;
            AddCommonSolidBlock(rack, "ClothRackPost" + (side < 0 ? "Left" : "Right"),
                new Vector3(.14f, 2.45f, .14f), new Vector3(x, supportHeight + 1.225f, 0f), wood, .014f);
        }
        AddCommonSolidBlock(rack, "ClothHangingRail", new Vector3(2.2f, .13f, .16f),
            new Vector3(0f, 2.34f, 0f), wood, .012f);

        var panel = new StaticBody3D
        {
            Name = "C04ClothPanel",
            Position = new Vector3(clothX, ground + 1.58f, clothZ),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        panel.SetMeta("presentationRole", "single supported cloth that interrupts the C05 horizon ray and folds onto the nearby table");
        panel.SetMeta("contactPolicy", "physical sight blocker on collision layer 1; fully off the C03-C05 walking corridor");
        _world.AddChild(panel);
        RuralPropGeometry.Part(panel, "WovenCloth", RuralPropGeometry.BowedPanel(1.62f, 1.36f, .026f, .06f, .085f),
            Vector3.Zero, RuralPropMaterials.Surface("cloth", "73838a"));
        RuralPropGeometry.Tube(panel, "UpperClothHem", new Vector3(-.78f, .68f, .02f), new Vector3(.78f, .68f, .02f),
            .024f, RuralPropMaterials.Surface("wood", "8f775b"), 10);
        panel.AddChild(new CollisionShape3D
        {
            Name = "ClothSightContact",
            Shape = new BoxShape3D { Size = new Vector3(1.62f, 1.36f, .052f) }
        });
        var clothWitness = new Marker3D { Name = "ClothInteractionWitness" };
        clothWitness.SetMeta("presentationRole", "actual woven-cloth interaction surface");
        panel.AddChild(clothWitness);

        const float tableX = -4.55f;
        const float tableZ = 84.7f;
        var tableGround = CommonGroundHeightAt(tableX, tableZ);
        var table = new StaticBody3D
        {
            Name = "C04ClothFoldTable",
            Position = new Vector3(tableX, tableGround, tableZ),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        table.SetMeta("presentationRole", "accessible solid support for the folded family cloth");
        table.SetMeta("footstepSurface", "wood");
        _world.AddChild(table);
        AddCommonSolidBlock(table, "FoldTableTop", new Vector3(1.82f, .08f, 1.52f),
            new Vector3(0f, .84f, 0f), wood, .012f);
        foreach (var x in new[] { -.76f, .76f })
        foreach (var z in new[] { -.61f, .61f })
            AddCommonSolidBlock(table, "FoldTableLeg", new Vector3(.12f, .80f, .12f),
                new Vector3(x, .40f, z), wood, .01f);

        var foldSupport = new Marker3D
        {
            Name = "C04ClothFoldSupport",
            Position = new Vector3(tableX, tableGround + .893f, tableZ),
            RotationDegrees = new Vector3(90f, 0f, 0f)
        };
        foldSupport.SetMeta("presentationRole", "flat folded-cloth pose resting on the physical table top");
        _world.AddChild(foldSupport);
    }

    private void BuildCommonCaptionDesk()
    {
        const float x = 28f;
        const float z = 143f;
        var desk = new StaticBody3D
        {
            Name = "C07CaptionDesk",
            Position = new Vector3(x, CommonGroundHeightAt(x, z), z),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        desk.SetMeta("presentationRole", "physical writing table set clear of both C05-C07 and C07-C08 links");
        desk.SetMeta("footstepSurface", "wood");
        _world.AddChild(desk);
        var wood = RuralPropMaterials.Surface("wood", "765b43");
        var paper = RuralPropMaterials.Surface("cloth", "e3d7bd");
        AddCommonSolidBlock(desk, "CaptionDeskTop", new Vector3(1.72f, .09f, 1.12f),
            new Vector3(0f, .84f, 0f), wood, .012f);
        foreach (var legX in new[] { -.72f, .72f })
        foreach (var legZ in new[] { -.43f, .43f })
            AddCommonSolidBlock(desk, "CaptionDeskLeg", new Vector3(.12f, .80f, .12f),
                new Vector3(legX, .40f, legZ), wood, .01f);
        var note = RuralPropGeometry.Block(desk, "BlankCaptionPaper", new Vector3(.72f, .018f, .48f),
            new Vector3(-.10f, .894f, .02f), paper, .006f, new Vector3(0f, -4f, 0f));
        desk.AddChild(new CollisionShape3D
        {
            Name = "BlankCaptionPaperContact",
            Shape = new BoxShape3D { Size = new Vector3(.72f, .018f, .48f) },
            Transform = note.Transform
        });

        var paperWitness = new Marker3D
        {
            Name = "CaptionPaperInteractionWitness",
            Position = new Vector3(-.10f, .916f, .02f)
        };
        paperWitness.SetMeta("presentationRole", "actual blank-caption paper surface, 1.3 cm above the paper top");
        desk.AddChild(paperWitness);
    }

    private void AddCommonWitness(string name, Vector3 position, string role, string viewContract)
    {
        var witness = new Marker3D { Name = name, Position = position };
        witness.SetMeta("presentationRole", role);
        witness.SetMeta("viewContract", viewContract);
        _world.AddChild(witness);
    }

    private static MeshInstance3D AddCommonSolidBlock(
        Node3D parent, string name, Vector3 size, Vector3 at, Material material, float bevel = .01f,
        Vector3? rotationDegrees = null)
    {
        var rotation = rotationDegrees ?? Vector3.Zero;
        var mesh = RuralPropGeometry.Block(parent, name, size, at, material, bevel, rotation);
        if (parent is StaticBody3D body)
            body.AddChild(new CollisionShape3D
            {
                Name = name + "Contact",
                Shape = new BoxShape3D { Size = size },
                Position = at,
                RotationDegrees = rotation
            });
        return mesh;
    }

    private static MeshInstance3D AddCommonSolidTube(
        Node3D parent, string name, Vector3 start, Vector3 end, float radius, Material material)
    {
        var mesh = RuralPropGeometry.Tube(parent, name, start, end, radius, material, 14);
        if (parent is StaticBody3D body)
            body.AddChild(new CollisionShape3D
            {
                Name = name + "Contact",
                Shape = new CylinderShape3D { Radius = radius + .012f, Height = start.DistanceTo(end) },
                Transform = mesh.Transform
            });
        return mesh;
    }

    private static bool IsCommonWinterDecoration(string name) =>
        name.Contains("RoofSnow", StringComparison.Ordinal)
        || name.Contains("PorchRoofSnow", StringComparison.Ordinal)
        || name.Contains("Icicle", StringComparison.Ordinal);

    private static T? FindCommonNode<T>(Node root, string name) where T : Node
    {
        if (root is T typed && root.Name.ToString() == name) return typed;
        foreach (var child in root.GetChildren())
        {
            var found = FindCommonNode<T>(child, name);
            if (found is not null) return found;
        }
        return null;
    }

    private static IEnumerable<T> FindCommonNodes<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T typed) yield return typed;
            foreach (var nested in FindCommonNodes<T>(child)) yield return nested;
        }
    }
}
