using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Plank steps down the village bank of the river, where people go to the ice
/// hole for water. Both banks of the ravine are steeper than the controller's
/// floor limit, so a player who slid onto the ice had no way back up: the steps
/// are that way out. They exist on the village bank only, so the forest bank
/// stays the boundary and the culvert stays the only crossing.
/// </summary>
public partial class Act1ConnectedWorld
{
    // No exit at x 44: that is the mouth of the ravine (2026-09-25).
    internal static readonly float[] RiverStepXs = [-44f, -28f, -10f, 28f, 34f];

    /// <summary>Bottom and top of the walkable step surface at one exit.</summary>
    internal static (Vector3 Bottom, Vector3 Top) RiverStepLine(float x)
    {
        var centre = (float)AgentBAct1HeightField.RiverMeander(x);
        // The bed blocker's top is the surface a player stands on in the ravine.
        var bed = (float)AgentBAct1HeightField.Ground(x, centre) + .78f;
        var bottom = new Vector3(x, bed, centre + .8f);
        // The bank is not a clean curve: it steepens at the lip and the
        // village ground keeps rising past it. Land the flight on level ground
        // beyond the lip, lifted just enough to clear the lip all the way down;
        // from its end a player steps down, never up a slope.
        for (var reach = 4.8f; reach < 9f; reach += .1f)
        {
            var ground = AgentBAct1HeightField.CollisionGround(x, centre + reach);
            var ahead = AgentBAct1HeightField.CollisionGround(x, centre + reach + .6f);
            if (Math.Abs(ahead - ground) > .3f) continue;
            var landingZ = centre + reach;
            var landingY = ground;
            for (var t = .1f; t < 1f; t += .05f)
            {
                var z = Mathf.Lerp(bottom.Z, landingZ, t);
                var need = AgentBAct1HeightField.CollisionGround(x, z) + .04f;
                landingY = Math.Max(landingY, bottom.Y + (need - bottom.Y) / t);
            }
            if (landingY - ground < .42f && landingY - bottom.Y < (landingZ - bottom.Z) * .72f)
                return (bottom, new Vector3(x, landingY, landingZ));
        }
        var profile = string.Join(" ", Enumerable.Range(0, 19).Select(i =>
            $"{i * .5f:0.0}:{AgentBAct1HeightField.CollisionGround(x, centre + i * .5f):0.00}"));
        throw new InvalidOperationException($"No walkable flight fits the village bank at x={x}: bed={bed:0.00} {profile}");
    }

    /// <summary>
    /// Windfall along the forest lip: two spruce trunks with brushwood between
    /// them. Where the forest ground sits low the far bank alone could be
    /// climbed; the pile stands taller than a step from the slope, so the
    /// culvert stays the only crossing, and the reason is in view.
    /// </summary>
    private static int AddForestBankWindfall(Node3D river, StaticBody3D proxy, float x, float centre)
    {
        var lipZ = centre - 4.9f;
        var ground = AgentBAct1HeightField.CollisionGround(x, lipZ);
        var bark = PainterlyMaterialLibrary.ForColor("4a3b2e", "wood");
        var snow = PainterlyMaterialLibrary.ForColor("eef2f6", "snow_ground");
        var needles = PainterlyMaterialLibrary.ForColor("2f3d33", "foliage");
        var lean = 9f * Mathf.Sin(x * .37f);
        for (var i = 0; i < 2; i++)
        {
            var trunk = new MeshInstance3D
            {
                Name = $"RiverWindfallTrunk_{x:0}_{i}",
                Position = new Vector3(x + (i - .5f) * .3f, ground + .28f + i * .5f, lipZ - .25f * i),
                RotationDegrees = new Vector3(0f, lean + i * 11f, 90f),
                Mesh = new CylinderMesh { TopRadius = .2f, BottomRadius = .26f, Height = 4.6f, RadialSegments = 10 },
                MaterialOverride = bark
            };
            trunk.SetMeta("visualOnly", true);
            river.AddChild(trunk);
        }
        var brush = new MeshInstance3D
        {
            Name = $"RiverWindfallBrush_{x:0}",
            Position = new Vector3(x, ground + .95f, lipZ - .5f),
            Scale = new Vector3(2.1f, .7f, .8f),
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = needles
        };
        brush.SetMeta("visualOnly", true);
        river.AddChild(brush);
        var cap = new MeshInstance3D
        {
            Name = $"RiverWindfallSnow_{x:0}",
            Position = new Vector3(x, ground + 1.35f, lipZ - .4f),
            Scale = new Vector3(2f, .25f, .7f),
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = snow
        };
        cap.SetMeta("visualOnly", true);
        river.AddChild(cap);
        proxy.AddChild(new CollisionShape3D
        {
            Name = $"RiverWindfallBlocker_{x:0}",
            Position = new Vector3(x, ground + 1.1f, lipZ - .35f),
            Shape = new BoxShape3D { Size = new Vector3(4.4f, 2.6f, 1.1f) }
        });
        return 1;
    }

    private static int AddRiverBankSteps(Node3D river, StaticBody3D proxy)
    {
        var shapes = 0;
        foreach (var x in RiverStepXs)
        {
            var (bottom, top) = RiverStepLine(x);
            var run = top - bottom;
            var length = run.Length();
            var pitch = Mathf.Atan2(run.Y, run.Z);
            var basis = new Basis(Vector3.Right, -pitch);
            const float thickness = .24f;
            // The top face runs through both end points; the body hangs below.
            var centre = (bottom + top) * .5f - basis.Y * (thickness * .5f);

            proxy.AddChild(new CollisionShape3D
            {
                Name = $"RiverBankSteps_{x:0}",
                Transform = new Transform3D(basis, centre),
                Shape = new BoxShape3D { Size = new Vector3(1.6f, thickness, length + .5f) }
            });
            shapes++;

            var steps = new Node3D { Name = $"RiverBankStepsVisual_{x:0}", Transform = new Transform3D(basis, centre) };
            steps.SetMeta("visualOnly", true);
            steps.SetMeta("presentationRole", "plank steps down the village bank to the ice hole; the way back up for anyone on the ice");
            river.AddChild(steps);
            var wood = PainterlyMaterialLibrary.ForColor("5d4a38", "wood");
            var worn = PainterlyMaterialLibrary.ForColor("7a6450", "wood");
            foreach (var side in new[] { -1f, 1f })
                steps.AddChild(new MeshInstance3D
                {
                    Name = side < 0 ? "StringerLeft" : "StringerRight",
                    Position = new Vector3(side * .74f, .02f, 0f),
                    Mesh = new BoxMesh { Size = new Vector3(.12f, .22f, length + .4f) },
                    MaterialOverride = wood
                });
            var treads = Mathf.Max(4, Mathf.RoundToInt(length / .42f));
            for (var i = 0; i < treads; i++)
            {
                var along = -length * .5f + (i + .5f) * length / treads;
                steps.AddChild(new MeshInstance3D
                {
                    Name = $"Tread_{i}",
                    Position = new Vector3(0f, thickness * .5f + .03f, along),
                    RotationDegrees = new Vector3(0f, (i % 3 - 1) * 1.5f, 0f),
                    Mesh = new BoxMesh { Size = new Vector3(1.5f, .06f, .26f) },
                    MaterialOverride = i % 2 == 0 ? wood : worn
                });
            }
            // Two posts carry the middle of the flight where the bank falls away
            // under it; they end in the snow instead of floating.
            foreach (var t in new[] { .35f, .65f })
            {
                var anchor = bottom.Lerp(top, t);
                var ground = AgentBAct1HeightField.CollisionGround(anchor.X, anchor.Z);
                var height = Mathf.Max(.2f, anchor.Y - ground);
                foreach (var side in new[] { -1f, 1f })
                {
                    var post = new MeshInstance3D
                    {
                        Name = $"RiverBankStepPost_{x:0}_{t:0.00}_{(side < 0 ? "l" : "r")}",
                        Position = new Vector3(anchor.X + side * .74f, ground + height * .5f - .05f, anchor.Z),
                        Mesh = new BoxMesh { Size = new Vector3(.1f, height + .1f, .1f) },
                        MaterialOverride = wood
                    };
                    post.SetMeta("visualOnly", true);
                    river.AddChild(post);
                }
            }
        }

        return shapes;
    }
}
