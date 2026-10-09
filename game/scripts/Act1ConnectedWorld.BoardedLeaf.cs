using System;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    /// <summary>
    /// A village gate leaf built the way a carpenter would: vertical sawn boards
    /// with 40 mm drying gaps and uneven tops, two battens and a Z-brace on the
    /// yard (back) face. Replaces single-box slabs. The leaf is centred on
    /// <paramref name="position"/> and fits inside <paramref name="size"/>; its
    /// thickness axis is the smaller of size.X / size.Z, its width axis the other.
    /// The hinge side is the negative end of the width axis and the brace rises
    /// away from it.
    /// </summary>
    private static Node3D AddBoardedLeaf(
        Node3D parent, string name, Vector3 size, Vector3 position, string colour, float yawDegrees = 0f)
    {
        var leaf = new Node3D
        {
            Name = name,
            Position = position,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        leaf.SetMeta("presentationOnly", true);
        leaf.SetMeta("replacedSlab", true);
        parent.AddChild(leaf);

        var thicknessIsX = size.X <= size.Z;
        var thickness = thicknessIsX ? size.X : size.Z;
        var width = thicknessIsX ? size.Z : size.X;
        var height = size.Y;

        // Deterministic per-name hash (string.GetHashCode is randomised per run).
        uint hash = 2166136261u;
        foreach (var ch in name)
            hash = (hash ^ ch) * 16777619u;
        float Next()
        {
            hash = hash * 1664525u + 1013904223u;
            return ((hash >> 8) & 0xFFFF) / 65535f;
        }

        const float gap = .04f;
        const float boardThickness = .028f;
        const float battenThickness = .035f;
        const float battenHeight = .09f;
        var total = MathF.Min(boardThickness + battenThickness, thickness);
        var boardT = MathF.Min(boardThickness, total * .6f);
        var battenT = MathF.Max(.01f, total - boardT);
        var boardCentre = total * .5f - boardT * .5f;
        var battenCentre = -total * .5f + battenT * .5f;

        var count = Math.Clamp((int)MathF.Round(width / .15f), 4, 14);
        var boardWidth = (width - (count - 1) * gap) / count;

        var baseColour = Color.FromHtml(colour);
        Color[] tones =
        [
            baseColour,
            baseColour.Lightened(.08f),
            baseColour.Darkened(.07f),
            baseColour.Lightened(.04f),
            baseColour.Darkened(.12f)
        ];

        // Local vector from (along-width, y, across-thickness) to leaf space.
        Vector3 Local(float along, float y, float across) =>
            thicknessIsX ? new Vector3(across, y, along) : new Vector3(along, y, across);
        Vector3 BoxSize(float along, float y, float across) => Local(along, y, across);

        for (var i = 0; i < count; i++)
        {
            var drop = Next() * .045f;
            var boardHeight = MathF.Max(.1f, height - drop);
            var along = -width * .5f + boardWidth * .5f + i * (boardWidth + gap);
            var tone = tones[(int)(Next() * tones.Length) % tones.Length];
            AddVisualBox(
                leaf,
                $"{name}Plank{i}",
                BoxSize(boardWidth, boardHeight, boardT),
                Local(along, -height * .5f + boardHeight * .5f, boardCentre),
                tone.ToHtml(false),
                "wood");
        }

        var lowY = -height * .5f + height * .22f;
        var highY = -height * .5f + height * .80f;
        var battenColour = baseColour.Lightened(.10f).ToHtml(false);
        AddVisualBox(leaf, $"{name}BattenLow", BoxSize(width, battenHeight, battenT), Local(0f, lowY, battenCentre), battenColour, "wood");
        AddVisualBox(leaf, $"{name}BattenHigh", BoxSize(width, battenHeight, battenT), Local(0f, highY, battenCentre), battenColour, "wood");

        // Z-brace between the batten centres, climbing away from the hinge.
        var rise = highY - lowY;
        var run = width * .86f;
        var length = MathF.Sqrt(run * run + rise * rise);
        var angle = Mathf.RadToDeg(MathF.Atan2(rise, run));
        var brace = AddVisualBox(
            leaf,
            $"{name}Brace",
            BoxSize(length, .08f, MathF.Max(.01f, battenT - .006f)),
            Local(0f, (lowY + highY) * .5f, battenCentre),
            baseColour.Darkened(.05f).ToHtml(false),
            "wood");
        brace.RotationDegrees = thicknessIsX ? new Vector3(-angle, 0f, 0f) : new Vector3(0f, 0f, angle);
        return leaf;
    }
}
