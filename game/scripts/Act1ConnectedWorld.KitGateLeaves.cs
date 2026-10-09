using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// ACT1-DEPTH.10 / author photo references T1-T2 (09.10.2026): Agent B's village
/// kit gives every yard gate (houses A1-A8, the FAP, Babai) a solid 1.3 m slab as
/// its leaf, so each gate reads as a door-sized plank. The kit's own latch rail
/// and diagonal brace already exist as separate members; only the slab is
/// replaced here, in its own plane and bounds, by sawn uprights with drying gaps
/// and uneven tops. The slab's orientation is measured from its vertices because
/// several leaves are authored standing open at an angle. Presentation only: the
/// gate's collision and any mechanism stay with their owners.
/// </summary>
public partial class Act1ConnectedWorld
{
    private static readonly string[] KitGateBoardTones = ["6f5440", "7a5d45", "65503d", "735842", "6a523e", "76593f"];

    private static int ReboardKitGateLeaves(Node3D root)
    {
        var reboarded = 0;
        foreach (var leaf in FindDescendants<MeshInstance3D>(root).ToArray())
        {
            var name = leaf.Name.ToString();
            if (!name.Contains("Gate", StringComparison.Ordinal) || !name.EndsWith("_Leaf", StringComparison.Ordinal)) continue;
            // Suppressed duplicates (e.g. Agent B's hidden Babai family) stay hidden.
            if (!leaf.Visible || leaf.HasMeta("leafReboarded") || leaf.Mesh is not ArrayMesh mesh || mesh.GetSurfaceCount() == 0) continue;
            if (leaf.GetParent() is not Node3D parent) continue;

            var points = mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (points.Length < 8) continue;
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);
            var bottom = points.Where(p => p.Y < minY + .02f).Select(p => new Vector2(p.X, p.Z)).Distinct().ToArray();
            if (bottom.Length < 4) continue;
            // The farthest pair of bottom corners spans the leaf; the spread across
            // that line is its thickness.
            var (from, to) = (bottom[0], bottom[1]);
            foreach (var a in bottom)
            foreach (var b in bottom)
                if (a.DistanceSquaredTo(b) > from.DistanceSquaredTo(to)) (from, to) = (a, b);
            var span = to - from;
            var length = span.Length();
            var height = maxY - minY;
            if (length is < .6f or > 2.6f || height is < .5f or > 2.2f) continue;
            var along = span / length;
            var across = new Vector2(-along.Y, along.X);
            var offsets = bottom.Select(p => (p - from).Dot(across)).ToArray();
            var mid = from + span * .5f + across * ((offsets.Min() + offsets.Max()) * .5f);
            var yaw = Mathf.RadToDeg(Mathf.Atan2(-along.Y, along.X));

            var boards = new Node3D { Name = name + "_Boards", Transform = leaf.Transform };
            boards.SetMeta("presentationOnly", true);
            boards.SetMeta("replaces", name);
            parent.AddChild(boards);
            var count = Mathf.Clamp(Mathf.RoundToInt(length / .15f), 5, 12);
            var pitch = length / count;
            var seed = (uint)name.Hash();
            for (var k = 0; k < count; k++)
            {
                seed = seed * 1664525u + 1013904223u;
                var top = height - .045f * ((seed >> 8) % 3);
                var centre = from + along * (pitch * (k + .5f)) + across * ((offsets.Min() + offsets.Max()) * .5f);
                AddVisualBox(boards, $"Board{k}", new(pitch - .045f, top, .028f),
                    new(centre.X, minY + top * .5f, centre.Y),
                    KitGateBoardTones[(int)((seed >> 16) % (uint)KitGateBoardTones.Length)], "wood", yawDegrees: yaw);
            }
            leaf.Visible = false;
            leaf.SetMeta("leafReboarded", true);
            leaf.SetMeta("suppressionReason", "solid kit slab replaced by sawn uprights in the same plane (ACT1-DEPTH.10/T1-T2)");
            boards.SetMeta("leafCentre", new Vector3(mid.X, minY, mid.Y));
            reboarded++;
        }
        if (reboarded > 0) GD.Print($"act1-gate-leaves: reboarded={reboarded}");
        return reboarded;
    }
}
