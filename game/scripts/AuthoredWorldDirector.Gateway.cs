using Godot;

namespace Urman.Godot;

public partial class AuthoredWorldDirector
{
    private const float GatewayHalfWidth = .62f;

    /// <summary>A household parcel's street gate stands open. The kit's gate is a 1 m closed
    /// assembly (leaves, brace, low header, planks) with continuous rails across it, which seals
    /// the yard from the street for a 0.7 m standing body. Everything wholly inside a 1.24 m
    /// opening centred on the gate is retired, the header is retired, and rails that cross the
    /// opening are split into two members ending at its edges. Posts, the sill and the fence on
    /// either side are untouched, and collision is baked afterwards from what remains visible.</summary>
    private static void OpenYardGateway(Node3D visual)
    {
        var meshes = visual.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .Where(mesh => mesh.Mesh is not null && mesh.Name.ToString().Contains("_Yard_", StringComparison.Ordinal)).ToList();
        if (meshes.FirstOrDefault(mesh => mesh.Name.ToString().Contains("_Yard_GateHeader", StringComparison.Ordinal)) is not { } header) return;

        var axis = header.GlobalBasis.X; axis.Y = 0;
        if (axis.LengthSquared() < .01f) return;
        axis = axis.Normalized();
        var across = new Vector3(-axis.Z, 0, axis.X);
        var headerBounds = WorldBounds(header);
        var centre = headerBounds.GetCenter();
        var from = centre.Dot(axis) - GatewayHalfWidth;
        var to = centre.Dot(axis) + GatewayHalfWidth;
        var line = centre.Dot(across);
        var retired = 0; var split = 0;

        foreach (var mesh in meshes)
        {
            var name = mesh.Name.ToString();
            if (name.Contains("Post", StringComparison.Ordinal) || name.Contains("Threshold", StringComparison.Ordinal)
                || name.Contains("WoodStack", StringComparison.Ordinal) || name.Contains("SideRail", StringComparison.Ordinal)
                || name.Contains("CornerRail", StringComparison.Ordinal)) continue;
            var bounds = WorldBounds(mesh);
            var middle = bounds.GetCenter();
            if (Mathf.Abs(middle.Dot(across) - line) > .8f) continue;
            var (lo, hi) = Extent(mesh, axis);
            if (name.Contains("_GateHeader", StringComparison.Ordinal))
            { mesh.Visible = false; retired++; continue; }
            if (lo >= from - .03f && hi <= to + .03f)
            { mesh.Visible = false; retired++; continue; }
            if (lo < from && hi > to)
            {
                SplitMember(mesh, axis, lo, hi, from, to);
                split++;
            }
            else if (middle.Dot(axis) > from && middle.Dot(axis) < to)
            { mesh.Visible = false; retired++; }
        }
        visual.GetParent()?.SetMeta("gatewayOpened", $"{retired} members retired, {split} rails split, {GatewayHalfWidth * 2:0.00} m clear");
    }

    private static Aabb WorldBounds(MeshInstance3D mesh)
    {
        var local = mesh.Mesh!.GetAabb();
        var first = mesh.GlobalTransform * local.GetEndpoint(0);
        var bounds = new Aabb(first, Vector3.Zero);
        for (var index = 1; index < 8; index++) bounds = bounds.Expand(mesh.GlobalTransform * local.GetEndpoint(index));
        return bounds;
    }

    private static (float Low, float High) Extent(MeshInstance3D mesh, Vector3 axis)
    {
        var local = mesh.Mesh!.GetAabb();
        var low = float.MaxValue; var high = float.MinValue;
        for (var index = 0; index < 8; index++)
        {
            var value = (mesh.GlobalTransform * local.GetEndpoint(index)).Dot(axis);
            low = Mathf.Min(low, value); high = Mathf.Max(high, value);
        }
        return (low, high);
    }

    /// <summary>Replace one rail by the parts of it outside [from, to] along the gate axis.
    /// Rails are boxes whose long side is the mesh's local X, so a part is the same mesh
    /// stretched and moved along that axis.</summary>
    private static void SplitMember(MeshInstance3D source, Vector3 axis, float lo, float hi, float from, float to)
    {
        var local = source.Mesh!.GetAabb();
        var global = source.GlobalTransform;
        var along = global.Basis.X;
        var slope = along.Dot(axis);
        if (Mathf.Abs(slope) < .01f || local.Size.X < .05f) return;
        var offset = (global * new Vector3(0, local.GetCenter().Y, local.GetCenter().Z)).Dot(axis);
        var parent = source.GetParent<Node3D>();
        var parts = new[] { ("L", lo, from), ("R", to, hi) };
        foreach (var (tag, start, end) in parts)
        {
            if (end - start < .05f) continue;
            var xa = (start - offset) / slope; var xb = (end - offset) / slope;
            if (xa > xb) (xa, xb) = (xb, xa);
            xa = Mathf.Max(xa, local.Position.X); xb = Mathf.Min(xb, local.End.X);
            var factor = (xb - xa) / local.Size.X;
            if (factor <= .001f) continue;
            var basis = new Basis(along * factor, global.Basis.Y, global.Basis.Z);
            var origin = global.Origin + along * (xa - factor * local.Position.X);
            var part = new MeshInstance3D
            {
                Name = source.Name + "_Gateway" + tag, Mesh = source.Mesh, MaterialOverride = source.MaterialOverride,
                CastShadow = source.CastShadow, GIMode = source.GIMode
            };
            parent.AddChild(part);
            part.GlobalTransform = new Transform3D(basis, origin);
        }
        source.Visible = false;
    }
}
