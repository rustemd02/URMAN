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

        var spirit = new BathSpirit { Name = "MunchaIyase", Position = new(-1.66f, 0, -2.02f), RotationDegrees = new(0, 38, 0) };
        bath.AddChild(spirit);
        spirit.Build();
        // Indoors nothing carries settled snow on its top faces.
        foreach (var mesh in FindDescendants<MeshInstance3D>(bath))
            if (mesh.MaterialOverride is { } material) mesh.MaterialOverride = PainterlyMaterialLibrary.Sheltered(material);
        bath.SetMeta("bathAtmosphere", "dark old boards, stone kamenka with glowing draught holes, whisks, polok, мунча иясе behind the stove; presentation only");
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

/// <summary>
/// Мунча иясе, the owner of the bathhouse: a small hunched shape with wet hair
/// in the dark corner behind the stove. It is never shown plainly: only the
/// eyes catch the light, it blinks, and it is gone when looked at straight or
/// approached, coming back once the player looks away. Presentation only; no
/// collision, no save state, no gameplay.
/// </summary>
public partial class BathSpirit : Node3D
{
    private Node3D _body = null!;
    private MeshInstance3D[] _eyes = [];
    private float _hiddenFor, _blinkIn = 3f, _blinkFor;

    public void Build()
    {
        _body = new Node3D { Name = "Body" };
        AddChild(_body);
        var skin = new StandardMaterial3D { AlbedoColor = new Color("16130f"), Roughness = .35f, MetallicSpecular = .6f };
        var hair = new StandardMaterial3D { AlbedoColor = new Color("0e0c0a"), Roughness = .25f, MetallicSpecular = .7f };
        MeshInstance3D Part(string name, Mesh mesh, Vector3 at, Vector3 scale, Material material, Vector3 rotation = default)
        {
            var part = new MeshInstance3D { Name = name, Mesh = mesh, Position = at, Scale = scale, RotationDegrees = rotation, MaterialOverride = material };
            part.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _body.AddChild(part);
            return part;
        }
        Part("Hunch", new SphereMesh { Radius = .2f, Height = .4f }, new(0, .34f, 0), new(1.1f, 1.05f, .9f), skin);
        Part("Head", new SphereMesh { Radius = .11f, Height = .22f }, new(.06f, .62f, .06f), Vector3.One, skin);
        for (var i = 0; i < 9; i++)
        {
            var a = -1.1f + i * .28f;
            Part($"Hair{i}", new BoxMesh { Size = new(.018f, .42f, .012f) },
                new(.06f + Mathf.Sin(a) * .09f, .47f, .06f + Mathf.Cos(a) * .07f - .03f), Vector3.One, hair, new(-8f + i, 0, Mathf.Sin(a) * 12f));
        }
        foreach (var side in new[] { -1f, 1f })
            Part($"Arm{side}", new BoxMesh { Size = new(.035f, .42f, .035f) }, new(side * .15f, .22f, .12f), Vector3.One, skin, new(18, 0, side * 10));
        var eyeGlow = new StandardMaterial3D
        {
            AlbedoColor = new Color("1d1f10"), EmissionEnabled = true, Emission = new Color("d8e07a"), EmissionEnergyMultiplier = 3.2f
        };
        _eyes = new[] { .035f, .085f }.Select(x => new MeshInstance3D
        {
            Name = "Eye" + x, Mesh = new SphereMesh { Radius = .017f, Height = .022f, RadialSegments = 8, Rings = 4 },
            Position = new(x, .64f, .155f), MaterialOverride = eyeGlow, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        }).ToArray();
        foreach (var eye in _eyes) AddChild(eye);
        SetMeta("presentationOnly", true);
        SetMeta("folklore", "мунча иясе: owner of the bathhouse, lives behind the stove (Proposal, mythology.md)");
    }

    public override void _Process(double delta)
    {
        if (_eyes.Length == 0) return;
        var camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        var eyes = GlobalTransform * new Vector3(.06f, .64f, .15f);
        var toSpirit = eyes - camera.GlobalPosition;
        var distance = toSpirit.Length();
        var facing = -camera.GlobalBasis.Z;
        var lookedAt = distance < 3.4f && facing.Dot(toSpirit / distance) > .975f;
        var tooClose = distance < 1.3f;
        if (lookedAt || tooClose || distance > 5.5f) _hiddenFor = 1.5f;
        else _hiddenFor = Math.Max(0, _hiddenFor - (float)delta);
        _blinkIn -= (float)delta;
        if (_blinkIn <= 0) { _blinkFor = .14f; _blinkIn = 2.5f + (float)GD.RandRange(0, 4.5); }
        _blinkFor = Math.Max(0, _blinkFor - (float)delta);
        var present = _hiddenFor <= 0;
        _body.Visible = present;
        foreach (var eye in _eyes) eye.Visible = present && _blinkFor <= 0;
    }
}
