using Godot;

namespace Urman.Godot;

/// <summary>
/// The old family мунча as the author asked for it: dark boards scrubbed clean
/// and old, a stone каменка with a cast-iron door whose two draught holes glow
/// like eyes, babay's morning embers still smouldering and a thread of smoke
/// from the pipe, dry birch whisks in the changing room, a tiered полок in the
/// wet room, and in the dark corner behind the stove the мунча иясе, the bath's
/// owner, with a bit of soap and water left for it. Presentation only: every
/// door, target, collider and saved state of the bathhouse stays as it was.
/// </summary>
public partial class Act1ConnectedWorld
{
    private MeshInstance3D[] _bathStoveEyes = [];
    private OmniLight3D? _bathSmoulderLight;
    private CpuParticles3D? _bathSmoulderSmoke;
    private float _bathFlicker;

    private void BuildBathAtmosphere()
    {
        var bath = _bathhouse!;
        var rng = new RandomNumberGenerator { Seed = 7121 };

        // Каменка: rough stones laid round the iron firebox, soot on the top courses.
        for (var row = 0; row < 8; row++)
        {
            var y = .07f + row * .12f;
            var soot = row / 7f;
            void Stone(float x, float z, float sx, float sz)
            {
                var shade = Mathf.Lerp(.42f, .16f, soot) * rng.RandfRange(.8f, 1.15f);
                var colour = new Color(shade, shade * .97f, shade * .92f).ToHtml(false);
                var block = AddVisualBox(bath, $"BathKamenkaStone{row}_{x:0.00}_{z:0.00}",
                    new(sx * rng.RandfRange(.85f, 1.05f), .115f * rng.RandfRange(.85f, 1.1f), sz * rng.RandfRange(.85f, 1.05f)),
                    new(x + rng.RandfRange(-.01f, .01f), y, z), colour, "stone", rng.RandfRange(-6f, 6f));
                block.MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, "stone", sheltered: true);
            }
            for (var z = -1.40f; z <= -.80f; z += .2f) { Stone(-1.74f, z, .16f, .2f); Stone(-.92f, z, .16f, .2f); }
            for (var x = -1.62f; x <= -1.04f; x += .2f) Stone(x, -1.47f, .2f, .15f);
            // Front cheeks either side of the iron door.
            Stone(-1.65f, -.75f, .15f, .13f); Stone(-1.01f, -.75f, .15f, .13f);
            if (row >= 5) Stone(-1.33f, -.75f, .5f, .13f);
        }
        // A heap of heater stones piled high over the tray.
        for (var i = 0; i < 16; i++)
        {
            var a = i * 2.4f; var r = .1f + (i % 5) * .05f;
            var stone = new MeshInstance3D
            {
                Name = "BathKamenkaHeapStone" + i,
                Mesh = new SphereMesh { Radius = .09f + (i % 3) * .025f, Height = .13f + (i % 2) * .05f, RadialSegments = 7, Rings = 4 },
                Position = new(-1.33f + Mathf.Cos(a) * r, 1.09f + (5 - i % 5) * .035f, -1.09f + Mathf.Sin(a) * r * .9f),
                RotationDegrees = new(i * 17f, i * 41f, 0),
                MaterialOverride = PainterlyMaterialLibrary.ForColor(i % 3 == 0 ? "2f2d29" : i % 3 == 1 ? "4a4740" : "3b3934", "stone", sheltered: true)
            };
            bath.AddChild(stone);
        }
        // Cast-iron door: a plate with two round draught holes and a slot below.
        // It closes the old open grille, whose bars and frame now stand behind it.
        foreach (var child in bath.GetChildren().OfType<Node3D>())
            if (child.Name.ToString().StartsWith("BathFireboxGrille", StringComparison.Ordinal) || child.Name == "BathFireboxDoorFrame")
                HidePresentationNode(child);
        AddVisualBox(bath, "BathStoveIronDoor", new(.46f, .32f, .02f), new(-1.33f, .60f, -.698f), "1b1b19", "metal");
        AddVisualBox(bath, "BathStoveIronDoorRim", new(.5f, .025f, .025f), new(-1.33f, .77f, -.695f), "2a2926", "metal");
        if (_bathEmbers is not null) _bathEmbers.Position = _bathEmbers.Position with { Z = -.76f };
        var glow = new StandardMaterial3D
        {
            AlbedoColor = new Color("3a1a0c"), EmissionEnabled = true, Emission = new Color("ff7a26"),
            EmissionEnergyMultiplier = 1.4f, Roughness = 1f
        };
        var eyes = new List<MeshInstance3D>();
        foreach (var x in new[] { -1.44f, -1.22f })
            eyes.Add(new MeshInstance3D
            {
                Name = "BathStoveDraughtHole" + (x < -1.3f ? "Left" : "Right"),
                Mesh = new CylinderMesh { TopRadius = .042f, BottomRadius = .042f, Height = .008f, RadialSegments = 14 },
                Position = new(x, .66f, -.686f), RotationDegrees = new(90, 0, 0), MaterialOverride = glow
            });
        eyes.Add(new MeshInstance3D
        {
            Name = "BathStoveDraughtSlot", Mesh = new BoxMesh { Size = new(.22f, .026f, .008f) },
            Position = new(-1.33f, .51f, -.686f), MaterialOverride = glow
        });
        foreach (var eye in eyes) bath.AddChild(eye);
        _bathStoveEyes = eyes.ToArray();
        // Soot licks up the wall and across the ceiling above the stove.
        AddVisualBox(bath, "BathSootWall", new(.01f, 1.3f, .9f), new(-1.893f, 1.7f, -1.09f), "120f0d", "wood");
        AddVisualBox(bath, "BathSootCeiling", new(1.1f, .01f, 1.2f), new(-1.35f, 2.545f, -1.15f), "15120f", "wood");
        _bathSmoulderLight = new OmniLight3D
        {
            Name = "BathSmoulderLight", Position = new(-1.33f, .6f, -.6f), LightColor = new Color("ff8a3a"),
            LightEnergy = .08f, OmniRange = 1.6f, ShadowEnabled = false
        };
        bath.AddChild(_bathSmoulderLight);
        _bathSmoulderSmoke = BathMist("BathChimneySmoulder", new(-1.58f, 3.95f, -1.31f), 7, 6f, .26f, new Color(.5f, .5f, .5f, .14f));

        // Changing room: dry birch whisks on a pole, felt caps on the hooks.
        FacilityRod(bath, "BathWhiskPole", new(-1.86f, 2.22f, .55f), new(-1.86f, 2.22f, 1.95f), .018f, "4a3a2b");
        for (var i = 0; i < 5; i++)
        {
            var z = .7f + i * .28f;
            var top = new Vector3(-1.86f, 2.2f, z);
            FacilityRod(bath, $"BathWhiskStem{i}", top, top + new Vector3(0, -.2f, 0), .018f, "6b5537");
            // A whisk hangs head down: a fan of twigs opening from the tie, dry leaves thick on them.
            var tilt = (i % 3 - 1) * 6f;
            var twigs = new MeshInstance3D
            {
                Name = $"BathWhiskTwigs{i}",
                Mesh = new CylinderMesh { TopRadius = .03f, BottomRadius = .1f, Height = .42f, RadialSegments = 9, Rings = 2 },
                Position = top + new Vector3(0, -.42f, 0), RotationDegrees = new(tilt, i * 37f, 0), Scale = new(1, 1, .55f),
                MaterialOverride = PainterlyMaterialLibrary.ForColor(i % 2 == 0 ? "4c4526" : "574d2a", "foliage", sheltered: true)
            };
            bath.AddChild(twigs);
            for (var leaf = 0; leaf < 6; leaf++)
            {
                var a = leaf * 1.05f + i;
                var clump = new MeshInstance3D
                {
                    Name = $"BathWhiskLeaf{i}_{leaf}",
                    Mesh = new SphereMesh { Radius = .045f, Height = .07f, RadialSegments = 6, Rings = 3 },
                    Position = top + new Vector3(Mathf.Cos(a) * .06f, -.46f - (leaf % 3) * .06f, Mathf.Sin(a) * .035f),
                    MaterialOverride = PainterlyMaterialLibrary.ForColor(leaf % 2 == 0 ? "5a5330" : "6b6036", "foliage", sheltered: true)
                };
                bath.AddChild(clump);
            }
            FacilityRod(bath, $"BathWhiskTie{i}", top + new Vector3(-.03f, -.2f, 0), top + new Vector3(.03f, -.2f, 0), .026f, "8a2020");
        }
        foreach (var (x, colour) in new[] { (-1.15f, "8f8a7e"), (-.45f, "a09482") })
        {
            var cap = new MeshInstance3D
            {
                Name = "BathFeltCap" + x, Mesh = new CylinderMesh { TopRadius = .06f, BottomRadius = .12f, Height = .16f, RadialSegments = 12 },
                Position = new(x, 1.66f, 2.27f), RotationDegrees = new(-12, 0, 0), MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, "fabric", sheltered: true)
            };
            bath.AddChild(cap);
        }

        // Wet room: a two-tier полок of slats against the back wall, a tub, a whisk soaking.
        foreach (var (y, zMid, depth, tier) in new[] { (.95f, -2.13f, .5f, "Upper"), (.52f, -1.70f, .36f, "Lower") })
        {
            for (var slat = 0; slat < 5; slat++)
                AddVisualBox(bath, $"BathPolok{tier}Slat{slat}", new(1.7f, .04f, depth / 5f - .012f),
                    new(.12f, y, zMid - depth * .5f + (slat + .5f) * depth / 5f), "4e3d2d", "wood_furniture");
            foreach (var x in new[] { -.68f, .9f })
                AddVisualBox(bath, $"BathPolok{tier}Leg{x}", new(.06f, y, .06f), new(x, y * .5f, zMid), "33281f", "wood");
        }
        FacilityVessel(bath, "BathOakTub", new(1.62f, .23f, -.08f), .24f, .45f, "5b4633", true);
        for (var hoop = 0; hoop < 2; hoop++)
            AddVisualBox(bath, "BathOakTubHoop" + hoop, new(.5f, .02f, .5f), new(1.62f, .12f + hoop * .22f, -.08f), "2b2a27", "metal");
        var soaking = new MeshInstance3D
        {
            Name = "BathWhiskSoaking", Mesh = new SphereMesh { Radius = .09f, Height = .3f, RadialSegments = 8, Rings = 5 },
            Position = new(1.37f, .66f, -1.86f), RotationDegrees = new(0, 0, 70),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("4f5230", "foliage", sheltered: true)
        };
        bath.AddChild(soaking);

        // What is left for the bath's owner: soap on a saucer and a little water.
        FacilityVessel(bath, "BathOfferingSaucer", new(-1.52f, .012f, -1.72f), .075f, .015f, "cfc6b0", false);
        AddVisualBox(bath, "BathOfferingSoap", new(.07f, .028f, .045f), new(-1.52f, .036f, -1.72f), "d8c9a0", "paper");
        FacilityVessel(bath, "BathOfferingCup", new(-1.40f, .03f, -1.80f), .04f, .055f, "b9b3a3", true);

        BuildBathVibeProps();

        // Мунча иясе, redrawn (BathSpirit.cs): a tall, still silhouette in the
        // washing-room corner behind the stove. The node builds here; the
        // owner's presentation tick calls Initialize/Tick (ForestEdgePresence
        // contract). The vibe props above stay clear of its corner and the
        // local haze quads never cover it (BathhouseSteamAtmosphere).
        var spirit = new BathSpirit { Name = "MunchaIyase", Position = new(-1.66f, 0, -2.02f), RotationDegrees = new(0, 38, 0) };
        bath.AddChild(spirit);
        spirit.Initialize(bath);
        _bathSpirit = spirit;
        // Indoors nothing carries settled snow on its top faces.
        foreach (var mesh in FindDescendants<MeshInstance3D>(bath))
            if (mesh.MaterialOverride is { } material) mesh.MaterialOverride = PainterlyMaterialLibrary.Sheltered(material);
        bath.SetMeta("bathAtmosphere", "dark old boards, stone kamenka with glowing draught holes, whisks, polok, мунча иясе behind the stove; presentation only");
    }

    /// <summary>
    /// What sells the smell of an old steam bath: two soaked birch whisks
    /// (веники) hanging on the partition, a wooden ladle left in the oak tub,
    /// a full bucket of cold water by the stove, soap on a small shelf above
    /// the basin, a birch-bark туес on the bench, damp lower boards and a thin
    /// wet sheen on the wet-room floor. All primitives and existing painterly
    /// material families — no imagegen, no new textures. Presentation only; the
    /// only new contact is the bucket rest the changing-room bucket already
    /// uses, so the walking shapes stay the same.
    /// </summary>
    private void BuildBathVibeProps()
    {
        var bath = _bathhouse!;
        // Two soaked whisks, head down on the wet face of the partition.
        foreach (var (x, y, tilt, shade) in new[]
                 {
                     (-1.52f, 2.02f, -7f, "4a4a22"),
                     (-1.22f, 1.96f, 6f, "54562a"),
                 })
        {
            FacilityRod(bath, $"BathSoakedWhiskStem{x:0.00}", new(x, y, .245f), new(x, y - .34f, .245f), .017f, "6b5537");
            var twigs = new MeshInstance3D
            {
                Name = $"BathSoakedWhiskTwigs{x:0.00}",
                Mesh = new CylinderMesh { TopRadius = .028f, BottomRadius = .095f, Height = .44f, RadialSegments = 9, Rings = 2 },
                Position = new(x, y - .58f, .245f), RotationDegrees = new(tilt, 0, 0), Scale = new(1f, 1f, .6f),
                MaterialOverride = PainterlyMaterialLibrary.ForColor(shade, "foliage", sheltered: true)
            };
            bath.AddChild(twigs);
            for (var leaf = 0; leaf < 3; leaf++)
            {
                var a = leaf * 2.1f + (x < -1.4f ? 0f : 1f);
                var clump = new MeshInstance3D
                {
                    Name = $"BathSoakedWhiskLeaf{x:0.00}_{leaf}",
                    Mesh = new SphereMesh { Radius = .05f, Height = .08f, RadialSegments = 6, Rings = 3 },
                    Position = new(x + Mathf.Cos(a) * .065f, y - .62f - leaf % 2 * .07f, .245f + Mathf.Sin(a) * .04f),
                    MaterialOverride = PainterlyMaterialLibrary.ForColor(leaf % 2 == 0 ? shade : "5d6030", "foliage", sheltered: true)
                };
                bath.AddChild(clump);
            }
            FacilityRod(bath, $"BathSoakedWhiskTie{x:0.00}", new(x - .03f, y - .34f, .245f), new(x + .03f, y - .34f, .245f), .024f, "8a2020");
        }
        // Wooden ladle resting in the existing oak tub (rim at y .455).
        var ladleBowl = new MeshInstance3D
        {
            Name = "BathTubLadleBowl",
            Mesh = new CylinderMesh { TopRadius = .075f, BottomRadius = .055f, Height = .075f, RadialSegments = 12, CapTop = false, CapBottom = true },
            Position = new(1.52f, .42f, -.02f), RotationDegrees = new(14f, 0, -8f),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("a1723f", "wood_prop", sheltered: true)
        };
        bath.AddChild(ladleBowl);
        FacilityRod(bath, "BathTubLadleHandle", new(1.50f, .47f, -.02f), new(1.24f, .60f, .12f), .015f, "8a6236");
        // A full bucket of cold water stands by the stove.
        FacilityVessel(bath, "BathStoveWaterBucket", new(-.92f, .30f, -.16f), .22f, .40f, "8f9895", true);
        FacilityRod(bath, "BathStoveBucketHandleLeft", new(-1.135f, .11f, -.16f), new(-1.06f, .34f, -.16f), .009f, "696f65");
        FacilityRod(bath, "BathStoveBucketHandleTop", new(-1.06f, .34f, -.16f), new(-.78f, .34f, -.16f), .009f, "696f65");
        FacilityRod(bath, "BathStoveBucketHandleRight", new(-.78f, .34f, -.16f), new(-.705f, .11f, -.16f), .009f, "696f65");
        FacilitySolid(bath, "BathStoveWaterBucketRest", new(.55f, .085f, .50f), new(-.92f, .045f, -.16f), "45372a", "wood_furniture");
        // Soap and a small dish on a shelf above the wash basin.
        FacilitySolid(bath, "BathSoapShelf", new(.30f, .025f, .17f), new(1.76f, 1.02f, -1.62f), "4d3b2a", "wood_furniture");
        foreach (var z in new[] { -1.71f, -1.53f })
            AddVisualBox(bath, $"BathSoapShelfBracket{z:0.00}", new(.05f, .14f, .025f), new(1.865f, .94f, z), "3a2c20", "wood");
        AddVisualBox(bath, "BathSoapDish", new(.13f, .014f, .085f), new(1.76f, 1.040f, -1.62f), "b7b3a6", "paper");
        AddVisualBox(bath, "BathSoapBar", new(.085f, .028f, .055f), new(1.76f, 1.062f, -1.62f), "ddd0ae", "paper");
        // Birch-bark туес (box) on the changing bench (seat top y .4825).
        var tues = new MeshInstance3D
        {
            Name = "BathBirchBarkBox",
            Mesh = new CylinderMesh { TopRadius = .085f, BottomRadius = .075f, Height = .13f, RadialSegments = 12 },
            Position = new(-1.39f, .55f, 1.62f),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("d9d5c6", "bark_birch", sheltered: true)
        };
        bath.AddChild(tues);
        var lid = new MeshInstance3D
        {
            Name = "BathBirchBarkBoxLid",
            Mesh = new CylinderMesh { TopRadius = .092f, BottomRadius = .092f, Height = .018f, RadialSegments = 12 },
            Position = new(-1.39f, .624f, 1.62f),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("cfcaba", "bark_birch", sheltered: true)
        };
        bath.AddChild(lid);
        FacilityRod(bath, "BathBirchBarkBoxHandle", new(-1.45f, .636f, 1.62f), new(-1.33f, .636f, 1.62f), .012f, "8a6b45");
        // Damp lower boards: darker and glossier than the dry upper courses
        // (the wood family already carries the higher wet grade and gloss).
        AddVisualBox(bath, "BathDampBoardsRear", new(3.78f, .80f, .02f), new(0f, .42f, -2.389f), "2a211a", "wood");
        AddVisualBox(bath, "BathDampBoardsEast", new(.02f, .80f, 2.46f), new(1.884f, .42f, -1.18f), "2a211a", "wood");
        AddVisualBox(bath, "BathDampBoardsWetPartition", new(1.84f, .78f, .02f), new(-.98f, .40f, .272f), "2a211a", "wood");
        AddVisualBox(bath, "BathDampBoardsWetPartitionRight", new(.70f, .78f, .02f), new(1.52f, .40f, .272f), "2a211a", "wood");
        // Thin wet sheen on the wet-room walking floor; two triangles.
        var sheen = new MeshInstance3D
        {
            Name = "BathWetFloorSheen",
            Mesh = new PlaneMesh { Size = new(1.8f, 1.7f) },
            Position = new(.86f, .0045f, -.80f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(.40f, .46f, .45f, .10f),
                Roughness = .30f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        sheen.SetMeta("visualOnly", true);
        bath.AddChild(sheen);
    }

    /// <summary>Stove glow: embers always smoulder; a lit fire flickers bright and smokes hard.</summary>
    private void TickBathAtmosphere(bool burning, double delta)
    {
        if (_bathStoveEyes.Length == 0) return;
        _bathFlicker += (float)delta;
        var flicker = burning
            ? 2.2f + .9f * Mathf.Sin(_bathFlicker * 13f) + .5f * Mathf.Sin(_bathFlicker * 31f)
            : 1.3f + .35f * Mathf.Sin(_bathFlicker * 1.3f) + .15f * Mathf.Sin(_bathFlicker * 4.1f);
        if (_bathStoveEyes[0].MaterialOverride is StandardMaterial3D glow) glow.EmissionEnergyMultiplier = flicker;
        if (_bathSmoulderLight is not null)
        {
            _bathSmoulderLight.Visible = FacilityExteriorActive;
            _bathSmoulderLight.LightEnergy = burning ? .22f + .08f * Mathf.Sin(_bathFlicker * 17f) : .06f + .02f * Mathf.Sin(_bathFlicker * 1.3f);
        }
        if (_bathSmoulderSmoke is not null)
        {
            _bathSmoulderSmoke.Emitting = !burning && FacilityExteriorActive;
            _bathSmoulderSmoke.Visible = FacilityExteriorActive;
        }
    }
}
