using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Solid, hand-shaped winter trees for the night forest of the prologue. No
/// alpha cards: every crown is closed geometry with its own snow load, so a
/// torch beam finds clean silhouettes instead of ragged cut-outs. Each mesh is
/// one vertex-coloured surface, authored at unit scale with the root at y = 0.
/// </summary>
internal static class PrologueTreeMeshes
{
    private static readonly Color Needle = new(.05f, .1f, .08f);
    private static readonly Color NeedleDeep = new(.025f, .05f, .045f);
    private static readonly Color Snow = new(.8f, .86f, .96f);
    private static readonly Color SpruceBark = new(.2f, .15f, .12f);
    private static readonly Color PineBarkLow = new(.24f, .19f, .16f);
    private static readonly Color PineBarkHigh = new(.45f, .25f, .14f);
    private static readonly Color BirchWhite = new(.84f, .84f, .8f);
    private static readonly Color BirchMark = new(.07f, .07f, .07f);
    private static readonly Color Twig = new(.11f, .09f, .08f);

    private static StandardMaterial3D? _material;

    /// <summary>Shared matte material: vertex colour broken up by a soft world-space grain.</summary>
    public static StandardMaterial3D Material => _material ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true,
        Roughness = .96f,
        MetallicSpecular = .12f,
        AlbedoTexture = new NoiseTexture2D
        {
            Width = 256, Height = 256, Seamless = true,
            Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = .045f, FractalOctaves = 3 },
            ColorRamp = Ramp(new Color(.62f, .62f, .62f), Colors.White)
        },
        Uv1Triplanar = true,
        Uv1WorldTriplanar = true,
        Uv1Scale = new Vector3(.55f, .22f, .55f)
    };

    private static Gradient Ramp(Color from, Color to)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, from);
        gradient.SetColor(1, to);
        return gradient;
    }

    public sealed class Builder
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Color> _colours = new();
        public readonly RandomNumberGenerator Rng;

        public Builder(ulong seed) => Rng = new RandomNumberGenerator { Seed = seed };

        // Godot's front face is clockwise seen from outside, so the geometric
        // cross product of a front face points inward. The winding is chosen
        // from the outward hint, and the stored normal always points outward.
        private void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, Vector3 outward)
        {
            var cross = (b - a).Cross(c - a);
            if (cross.LengthSquared() < 1e-12f) return;
            if (cross.Dot(outward) > 0f)
            {
                (b, c) = (c, b);
                (cb, cc) = (cc, cb);
                cross = -cross;
            }
            var normal = -cross.Normalized();
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
            _normals.Add(normal); _normals.Add(normal); _normals.Add(normal);
            _colours.Add(ca); _colours.Add(cb); _colours.Add(cc);
        }

        /// <summary>
        /// A tapered limb from <paramref name="from"/> to <paramref name="to"/>. Flat faces on
        /// purpose: the facets read as bark planes and as snow-laden boughs.
        /// </summary>
        public void Limb(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int sides,
            Color colourFrom, Color colourTo, float ragged = 0f, bool capFrom = false, bool capTo = true)
        {
            var axis = to - from;
            if (axis.LengthSquared() < 1e-8f) return;
            var up = axis.Normalized();
            var side = Mathf.Abs(up.Y) > .95f ? Vector3.Right : up.Cross(Vector3.Up).Normalized();
            var forward = side.Cross(up).Normalized();
            var twist = Rng.RandfRange(0f, Mathf.Tau);
            var ringFrom = new Vector3[sides];
            var ringTo = new Vector3[sides];
            var arounds = new Vector3[sides];
            for (var index = 0; index < sides; index++)
            {
                var angle = twist + Mathf.Tau * index / sides;
                var around = side * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                arounds[index] = around;
                ringFrom[index] = from + around * radiusFrom * (1f + Rng.RandfRange(-ragged, ragged));
                ringTo[index] = to + around * radiusTo * (1f + Rng.RandfRange(-ragged, ragged) * .5f);
            }
            for (var index = 0; index < sides; index++)
            {
                var next = (index + 1) % sides;
                var outward = (arounds[index] + arounds[next]).Normalized();
                Tri(ringFrom[index], ringTo[index], ringFrom[next], colourFrom, colourTo, colourFrom, outward);
                Tri(ringFrom[next], ringTo[index], ringTo[next], colourFrom, colourTo, colourTo, outward);
                if (capFrom) Tri(from, ringFrom[index], ringFrom[next], colourFrom, colourFrom, colourFrom, -up);
                if (capTo && radiusTo > .001f) Tri(to, ringTo[next], ringTo[index], colourTo, colourTo, colourTo, up);
            }
        }

        public ArrayMesh Commit()
        {
            var arrays = new global::Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = _colours.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(0, Material);
            return mesh;
        }
    }

    /// <summary>Old spruce, 10 units tall: a bare lower trunk, then tiers of drooping, snow-loaded boughs.</summary>
    public static ArrayMesh Spruce(ulong seed, float bareTrunk = 2.4f, int tiers = 6)
    {
        var builder = new Builder(seed);
        var rng = builder.Rng;
        builder.Limb(new Vector3(0, -.3f, 0), new Vector3(0, 9.4f, 0), .2f, .03f, 7, SpruceBark, SpruceBark, .08f);
        // Dead lower twigs: what a torch actually catches at eye level.
        for (var twig = 0; twig < 5; twig++)
        {
            var y = rng.RandfRange(.9f, bareTrunk + .4f);
            var out2 = new Vector3(1, 0, 0).Rotated(Vector3.Up, rng.RandfRange(0f, Mathf.Tau));
            builder.Limb(new Vector3(0, y, 0) + out2 * .12f, new Vector3(0, y - rng.RandfRange(.1f, .35f), 0) + out2 * rng.RandfRange(.5f, 1.1f),
                .022f, .006f, 3, Twig, Twig);
        }
        for (var tier = 0; tier < tiers; tier++)
        {
            var t = tier / (float)(tiers - 1);
            var baseY = Mathf.Lerp(bareTrunk, 8.3f, t);
            var radius = Mathf.Lerp(2.05f, .42f, Mathf.Pow(t, .85f)) * rng.RandfRange(.88f, 1.1f);
            var height = Mathf.Lerp(2.3f, 1.7f, t);
            var lean = new Vector3(rng.RandfRange(-.12f, .12f), 0, rng.RandfRange(-.12f, .12f));
            // The skirt hangs below its own root: boughs bent by the load.
            var skirt = new Vector3(0, baseY - .32f, 0) + lean;
            var tip = new Vector3(0, baseY + height, 0);
            builder.Limb(skirt, tip, radius, radius * .06f, 8, NeedleDeep, Needle, .2f, capFrom: true, capTo: false);
            // Snow lies on the upper two thirds and slumps over the edge.
            var snowFrom = new Vector3(0, baseY + height * .22f, 0) + lean * .6f;
            builder.Limb(snowFrom, tip + Vector3.Up * .1f, radius * .82f, radius * .05f, 8, Snow, Snow, .16f, capTo: false);
        }
        return builder.Commit();
    }

    /// <summary>Young spruce, 10 units tall: boughs to the snow.</summary>
    public static ArrayMesh YoungSpruce(ulong seed) => Spruce(seed, .35f, 5);

    /// <summary>Scots pine, 10 units tall: a long clean stem going red towards a small, high crown.</summary>
    public static ArrayMesh Pine(ulong seed)
    {
        var builder = new Builder(seed);
        var rng = builder.Rng;
        var bend = new Vector3(rng.RandfRange(-.35f, .35f), 0, rng.RandfRange(-.35f, .35f));
        var mid = new Vector3(0, 4.6f, 0) + bend * .4f;
        var top = new Vector3(0, 8.6f, 0) + bend;
        builder.Limb(new Vector3(0, -.3f, 0), mid, .23f, .16f, 7, PineBarkLow, PineBarkHigh, .07f, capTo: false);
        builder.Limb(mid, top, .16f, .07f, 7, PineBarkHigh, PineBarkHigh, .05f);
        // A couple of snapped stubs on the stem.
        for (var stub = 0; stub < 3; stub++)
        {
            var y = rng.RandfRange(2.2f, 6f);
            var out2 = new Vector3(1, 0, 0).Rotated(Vector3.Up, rng.RandfRange(0f, Mathf.Tau));
            builder.Limb(new Vector3(0, y, 0) + out2 * .12f, new Vector3(0, y + .12f, 0) + out2 * rng.RandfRange(.3f, .6f), .035f, .015f, 4, PineBarkLow, Twig);
        }
        for (var clump = 0; clump < 5; clump++)
        {
            var out2 = new Vector3(1, 0, 0).Rotated(Vector3.Up, rng.RandfRange(0f, Mathf.Tau)) * rng.RandfRange(.3f, 1.5f);
            var root = top + new Vector3(0, rng.RandfRange(-1.9f, .1f), 0);
            var centre = root + out2 + Vector3.Up * rng.RandfRange(.2f, .7f);
            builder.Limb(root, centre, .06f, .03f, 4, PineBarkHigh, PineBarkHigh, capTo: false);
            var size = rng.RandfRange(.7f, 1.15f);
            // A flattened double cone reads as a pine pad from below.
            builder.Limb(centre - Vector3.Up * .25f * size, centre + Vector3.Up * .55f * size, size, size * .1f, 7, NeedleDeep, Needle, .22f, capFrom: true, capTo: false);
            builder.Limb(centre + Vector3.Up * .02f, centre + Vector3.Up * .66f * size, size * .78f, size * .08f, 7, Snow, Snow, .18f, capTo: false);
        }
        return builder.Commit();
    }

    /// <summary>Winter birch, 10 units tall: a pale banded stem and bare black twigs.</summary>
    public static ArrayMesh Birch(ulong seed)
    {
        var builder = new Builder(seed);
        var rng = builder.Rng;
        var sway = new Vector3(rng.RandfRange(-.5f, .5f), 0, rng.RandfRange(-.5f, .5f));
        const int rings = 14;
        Vector3 Stem(float t) => new Vector3(0, -.3f + t * 9.6f, 0) + sway * t * t;
        float Radius(float t) => Mathf.Lerp(.15f, .025f, Mathf.Pow(t, .8f));
        for (var ring = 0; ring < rings; ring++)
        {
            var t0 = ring / (float)rings;
            var t1 = (ring + 1) / (float)rings;
            // Black lenticel bands, denser near the ground.
            var dark = rng.Randf() < Mathf.Lerp(.55f, .2f, t0);
            var colour = dark ? BirchWhite.Lerp(BirchMark, rng.RandfRange(.55f, .95f)) : BirchWhite;
            builder.Limb(Stem(t0), Stem(t1), Radius(t0), Radius(t1), 6, colour, dark ? BirchWhite : colour, .04f, capTo: ring == rings - 1);
        }
        for (var branch = 0; branch < 9; branch++)
        {
            var t = rng.RandfRange(.38f, .95f);
            var out2 = new Vector3(1, 0, 0).Rotated(Vector3.Up, rng.RandfRange(0f, Mathf.Tau));
            var length = Mathf.Lerp(2.2f, .7f, t) * rng.RandfRange(.7f, 1.15f);
            var root = Stem(t);
            var elbow = root + out2 * length * .45f + Vector3.Up * length * .5f;
            var end = elbow + out2 * length * .5f + Vector3.Up * length * rng.RandfRange(-.25f, .2f);
            builder.Limb(root, elbow, Radius(t) * .5f, .018f, 4, Twig, Twig, capTo: false);
            // Birch twigs weep.
            builder.Limb(elbow, end, .018f, .004f, 3, Twig, Twig);
            builder.Limb(elbow.Lerp(end, .5f), elbow.Lerp(end, .5f) + new Vector3(0, -length * .35f, 0) + out2 * .1f, .008f, .003f, 3, Twig, Twig);
        }
        return builder.Commit();
    }

    /// <summary>
    /// The one at the end of the prints, 1 unit = 1 m, about 3.3 m tall. Built
    /// like the trees around it: a narrow bark body, a small tilted head with a
    /// single spur, and arms that end far too low in fingers that are far too long.
    /// </summary>
    public static ArrayMesh TallOne(ulong seed)
    {
        var builder = new Builder(seed);
        var rng = builder.Rng;
        var bark = new Color(.07f, .06f, .055f);
        var pale = new Color(.16f, .15f, .14f);
        // Legs that could be two thin trunks, a long body, a stoop.
        foreach (var x in new[] { -.13f, .13f })
            builder.Limb(new Vector3(x * 1.3f, -.2f, 0), new Vector3(x, 1.45f, .04f), .07f, .09f, 6, bark, bark, .12f, capTo: false);
        builder.Limb(new Vector3(0, 1.4f, .04f), new Vector3(0, 2.75f, -.16f), .21f, .16f, 7, bark, bark, .14f);
        var neck = new Vector3(0, 2.75f, -.16f);
        var head = neck + new Vector3(.07f, .3f, -.12f);
        builder.Limb(neck, head, .07f, .12f, 6, bark, bark, .1f, capTo: false);
        builder.Limb(head, head + new Vector3(.03f, .2f, -.03f), .13f, .05f, 6, bark, bark, .12f);
        // The spur over the brow.
        builder.Limb(head + new Vector3(.02f, .12f, -.11f), head + new Vector3(.05f, .37f, -.25f), .028f, .004f, 4, pale, pale);
        foreach (var side in new[] { -1f, 1f })
        {
            var shoulder = new Vector3(side * .22f, 2.62f, -.12f);
            var elbow = shoulder + new Vector3(side * .16f, -.95f, .1f);
            var wrist = elbow + new Vector3(side * -.04f, -.9f, -.16f);
            builder.Limb(shoulder, elbow, .05f, .04f, 5, bark, bark, .1f, capTo: false);
            builder.Limb(elbow, wrist, .04f, .03f, 5, bark, bark, .1f, capTo: false);
            for (var finger = 0; finger < 4; finger++)
            {
                var spread = (finger - 1.5f) * .045f;
                var knuckle = wrist + new Vector3(spread, -.3f - rng.RandfRange(0f, .08f), spread * .5f);
                var tip = knuckle + new Vector3(spread * 1.5f + side * rng.RandfRange(-.03f, .03f), -.45f - rng.RandfRange(0f, .16f), -.05f);
                builder.Limb(wrist, knuckle, .013f, .011f, 4, pale, pale, capTo: false);
                builder.Limb(knuckle, tip, .011f, .002f, 4, pale, pale);
            }
        }
        return builder.Commit();
    }
}
