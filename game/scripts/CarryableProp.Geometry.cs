using Godot;

namespace Urman.Godot;

public partial class CarryableProp
{
    // Small, intentionally modelled household silhouettes use the same surface
    // library as their surroundings. No imported photograph stands in for volume.
    private void BuildGeometry(string colour, string surface)
    {
        var wood = PainterlyMaterialLibrary.ForColor(colour, surface);
        var timber = PainterlyMaterialLibrary.ForColor("92745a", "wood");
        var metal = PainterlyMaterialLibrary.ForColor("575b57", "metal");
        void Box(string name, Vector3 size, Vector3 at, Material material) => AddChild(new MeshInstance3D
            { Name = name, Mesh = new BoxMesh { Size = size }, Position = at, MaterialOverride = material });
        void Rod(string name, float radius, float length, Vector3 at, Vector3 rotation, Material material) => AddChild(new MeshInstance3D
        {
            Name = name, Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius * 1.05f,
                Height = length, RadialSegments = 10, Rings = 1 }, Position = at,
            RotationDegrees = rotation, MaterialOverride = material
        });
        switch (Kind)
        {
            case ItemKind.Log:
                Rod("Bark", .062f, Size.X - .014f, new(0, .065f, 0), new(0, 0, 90), wood);
                for (var side = -1; side <= 1; side += 2)
                    Rod($"CutEnd{side}", .060f, .009f, new(side * (Size.X * .5f - .005f), .065f, 0), new(0, 0, 90), timber);
                break;
            case ItemKind.Crate:
                for (var row = 0; row < 3; row++)
                {
                    var y = .06f + row * (Size.Y - .1f) / 3f;
                    Box($"FrontPlank{row}", new(Size.X, .074f, .025f), new(0, y, Size.Z * .5f - .0125f), wood);
                    Box($"BackPlank{row}", new(Size.X, .074f, .025f), new(0, y, -Size.Z * .5f + .0125f), wood);
                    Box($"LeftPlank{row}", new(.025f, .074f, Size.Z), new(-Size.X * .5f + .0125f, y, 0), wood);
                    Box($"RightPlank{row}", new(.025f, .074f, Size.Z), new(Size.X * .5f - .0125f, y, 0), wood);
                }
                for (var x = -1; x <= 1; x += 2)
                    for (var z = -1; z <= 1; z += 2)
                        Box($"Corner{x}_{z}", new(.036f, Size.Y, .036f), new(x * (Size.X * .5f - .025f), Size.Y * .5f, z * (Size.Z * .5f - .025f)), timber);
                for (var plank = 0; plank < 5; plank++)
                    Box($"Bottom{plank}", new(Size.X / 5f - .006f, .025f, Size.Z), new(-Size.X * .4f + plank * Size.X / 5f, .013f, 0), timber);
                Box("HandleRailFront", new(Size.X, .038f, .028f), new(0, Size.Y - .022f, Size.Z * .5f - .014f), timber);
                Box("HandleRailBack", new(Size.X, .038f, .028f), new(0, Size.Y - .022f, -Size.Z * .5f + .014f), timber);
                break;
            case ItemKind.Bucket:
                // A hollow vessel: separate thin staves, rim and raised handle.
                for (var i = 0; i < 16; i++)
                {
                    var a = i * Mathf.Tau / 16;
                    var panel = new MeshInstance3D { Name = $"MetalSide{i}",
                        Mesh = new BoxMesh { Size = new(.051f, .275f, .009f) },
                        Position = new(Mathf.Sin(a) * .125f, .145f, Mathf.Cos(a) * .125f),
                        Rotation = new(0, a, 0), MaterialOverride = metal };
                    AddChild(panel);
                }
                Rod("BucketBottom", .12f, .012f, new(0, .009f, 0), Vector3.Zero, metal);
                for (var side = -1; side <= 1; side += 2)
                    Rod($"HandleSide{side}", .006f, .15f, new(side * .12f, .337f, 0), Vector3.Zero, metal);
                Rod("HandleGrip", .013f, .24f, new(0, .411f, 0), new(0, 0, 90), timber);
                break;
            case ItemKind.Axe:
                Rod("AxeHandle", .014f, .37f, new(0, .025f, 0), new(0, 0, 90), timber);
                AddChild(new MeshInstance3D { Name = "AxeHead", Mesh = WedgeMesh(.115f, .064f, .18f),
                    Position = new(.105f, .005f, 0), MaterialOverride = metal });
                break;
            case ItemKind.Shovel:
                Box("ShovelBlade", new(.28f, .25f, .016f), new(0, .13f, .018f), metal);
                for (var side = -1; side <= 1; side += 2)
                    Box($"BladeLip{side}", new(.012f, .25f, .065f), new(side * .134f, .13f, .042f), metal);
                Rod("ShovelShaft", .017f, .9f, new(0, .685f, 0), Vector3.Zero, timber);
                Rod("ShovelCrossGrip", .025f, .18f, new(0, 1.153f, 0), new(0, 0, 90), timber);
                break;
            case ItemKind.Pole:
                Rod("WoodenPole", .021f, Size.Z, new(0, .025f, 0), new(90, 0, 0), timber);
                Box("PoleNotch", new(.052f, .034f, .09f), new(0, .051f, -Size.Z * .5f + .065f), metal);
                break;
            case ItemKind.Board:
                Box("Board", Size, Vector3.Up * Size.Y * .5f, wood);
                for (var i = -1; i <= 1; i += 2)
                    Box($"WornEnd{i}", new(Size.X, .004f, .04f), new(0, Size.Y + .002f, i * (Size.Z * .5f - .04f)), timber);
                break;
            case ItemKind.Ladder:
                for (var side = -1; side <= 1; side += 2)
                    Box($"Rail{side}", new(.055f, Size.Y, Size.Z), new(side * (Size.X * .5f - .0275f), Size.Y * .5f, 0), timber);
                for (var rung = 0; rung < 7; rung++)
                    Rod($"Rung{rung}", .025f, Size.X - .06f, new(0, Size.Y * .5f, -Size.Z * .42f + rung * Size.Z * .14f), new(0, 0, 90), wood);
                break;
            case ItemKind.Cloth:
                for (var fold = 0; fold < 4; fold++)
                    Box($"Fold{fold}", new(Size.X - fold * .018f, .016f, Size.Z - fold * .01f), new(.003f * fold, .008f + fold * .015f, 0), wood);
                break;
            case ItemKind.Lantern:
                Box("BatteryBase", new(.21f, .085f, .19f), new(0, .044f, 0), metal);
                Box("LampCap", new(.21f, .037f, .19f), new(0, .282f, 0), metal);
                for (var x = -1; x <= 1; x += 2)
                    for (var z = -1; z <= 1; z += 2)
                        Rod($"Cage{x}_{z}", .008f, .20f, new(x * .085f, .178f, z * .075f), Vector3.Zero, metal);
                Rod("Diffuser", .052f, .16f, new(0, .176f, 0), Vector3.Zero,
                    PainterlyMaterialLibrary.ForColor("c3c6bb", "painted"));
                for (var side = -1; side <= 1; side += 2)
                    Rod($"CarryHandle{side}", .007f, .09f, new(side * .07f, .343f, 0), Vector3.Zero, metal);
                Rod("Grip", .012f, .14f, new(0, .381f, 0), new(0, 0, 90), timber);
                _lamp = new OmniLight3D { Name = "PortableLight", Position = new(0, .185f, 0),
                    LightColor = new Color("ffddaa"), LightEnergy = 1.3f, OmniRange = 4.8f,
                    ShadowEnabled = true, Visible = false };
                AddChild(_lamp);
                _lampGlow = new MeshInstance3D { Name = "LitDiffuser", Position = new(0, .176f, 0),
                    Mesh = new CylinderMesh { TopRadius = .053f, BottomRadius = .053f, Height = .161f, RadialSegments = 10 },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("ffe4b2"),
                        EmissionEnabled = true, Emission = new Color("ffd49b"), EmissionEnergyMultiplier = .9f }, Visible = false };
                AddChild(_lampGlow);
                break;
        }
    }

    private static ArrayMesh WedgeMesh(float width, float height, float depth)
    {
        var builder = new SurfaceTool();
        builder.Begin(Mesh.PrimitiveType.Triangles);
        var p = new[] { new Vector3(-width*.5f, 0, -depth*.5f), new Vector3(width*.5f, 0, -depth*.5f),
            new Vector3(-width*.5f, height, -depth*.5f), new Vector3(width*.5f, height, -depth*.5f),
            new Vector3(-width*.35f, height*.4f, depth*.5f), new Vector3(width*.35f, height*.4f, depth*.5f) };
        foreach (var i in new[] { 0,2,3,0,3,1,0,4,2,1,3,5,0,1,5,0,5,4,2,4,5,2,5,3 }) builder.AddVertex(p[i]);
        builder.GenerateNormals();
        return builder.Commit();
    }
}
