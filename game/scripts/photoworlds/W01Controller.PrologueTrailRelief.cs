using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private const int PrologueTrailReliefVertexBudget = 4000;
    private const float PrologueTrailReliefLongitudinalStep = 1f;
    private const float PrologueTrailReliefFadeInLength = 6f;
    private const float PrologueTrailReliefFadeOutLength = 8f;
    private const float PrologueTrailReliefSurfaceOffset = .012f;
    private static readonly (float Offset, float Height)[] PrologueTrailReliefProfile =
    {
        (1.05f, 0f), (1.15f, .03f), (1.30f, .07f), (1.45f, .095f), (1.60f, .105f),
        (1.75f, .09f), (1.90f, .06f), (2.05f, .025f), (2.20f, 0f)
    };

    private MeshInstance3D? _prologueTrailReliefVisual;
    private ArrayMesh? _prologueTrailReliefMesh;

    private void BuildPrologueTrailRelief(Material material)
    {
        if (_prologueTrailReliefVisual is { } existing && GodotObject.IsInstanceValid(existing)) return;
        if (_forestVisual is not { } forest || !GodotObject.IsInstanceValid(forest))
            throw new InvalidOperationException("W01 prologue trail relief requires its forest presentation owner.");

        _prologueTrailReliefMesh ??= BuildPrologueTrailReliefMesh(material);
        _prologueTrailReliefVisual = new MeshInstance3D
        {
            Name = "F00F03SoftSnowShoulders",
            Mesh = _prologueTrailReliefMesh,
            MaterialOverride = material,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _prologueTrailReliefVisual.SetMeta("presentationOnly", true);
        _prologueTrailReliefVisual.SetMeta("collisionOwner", "none");
        _prologueTrailReliefVisual.SetMeta("routeClearance", "prologue route core and existing ground collision retained; relief begins at 1.05 m");
        _prologueTrailReliefVisual.SetMeta("vertexBudget", PrologueTrailReliefVertexBudget);
        forest.AddChild(_prologueTrailReliefVisual);
    }

    private void SetPrologueTrailReliefVisible(bool visible)
    {
        if (_prologueTrailReliefVisual is { } relief && GodotObject.IsInstanceValid(relief))
            relief.Visible = visible;
    }

    private static ArrayMesh BuildPrologueTrailReliefMesh(Material material)
    {
        var route = PrologueRouteNodes;
        var segmentDirections = new Vector2[route.Length - 1];
        var segmentLengths = new float[route.Length - 1];
        var totalLength = 0f;
        for (var index = 0; index < segmentDirections.Length; index++)
        {
            var start = new Vector2(route[index].X, route[index].Z);
            var end = new Vector2(route[index + 1].X, route[index + 1].Z);
            segmentLengths[index] = start.DistanceTo(end);
            segmentDirections[index] = (end - start).Normalized();
            totalLength += segmentLengths[index];
        }

        static Vector2 LeftNormal(Vector2 direction) => new(-direction.Y, direction.X);

        static (Vector2 Normal, float Scale) JointFrame(Vector2 incoming, Vector2 outgoing)
        {
            var incomingNormal = LeftNormal(incoming);
            var outgoingNormal = LeftNormal(outgoing);
            var miter = (incomingNormal + outgoingNormal).Normalized();
            var denominator = MathF.Max(.5f, miter.Dot(outgoingNormal));
            return (miter, MathF.Min(1.5f, 1f / denominator));
        }

        var rows = new List<(Vector2 Center, Vector2 LeftNormal, float MiterScale, float Distance)>();
        var traveled = 0f;
        for (var segmentIndex = 0; segmentIndex < segmentLengths.Length; segmentIndex++)
        {
            var start = new Vector2(route[segmentIndex].X, route[segmentIndex].Z);
            var end = new Vector2(route[segmentIndex + 1].X, route[segmentIndex + 1].Z);
            var segmentLength = segmentLengths[segmentIndex];
            var divisions = Mathf.Max(1, Mathf.CeilToInt(segmentLength / PrologueTrailReliefLongitudinalStep));
            for (var step = segmentIndex == 0 ? 0 : 1; step <= divisions; step++)
            {
                var t = step / (float)divisions;
                var tangent = segmentDirections[segmentIndex];
                var frame = (Normal: LeftNormal(tangent), Scale: 1f);
                if (step == divisions && segmentIndex < segmentLengths.Length - 1)
                    frame = JointFrame(tangent, segmentDirections[segmentIndex + 1]);
                var center = start.Lerp(end, t);
                rows.Add((center, frame.Normal, frame.Scale, traveled + segmentLength * t));
            }
            traveled += segmentLength;
        }

        var vertexCount = rows.Count * PrologueTrailReliefProfile.Length * 2;
        if (vertexCount > PrologueTrailReliefVertexBudget)
            throw new InvalidOperationException($"W01 prologue trail relief exceeds its {PrologueTrailReliefVertexBudget}-vertex budget: {vertexCount}.");

        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);
        surface.SetSmoothGroup(0);
        var freshSnowMask = new Color(0f, 0f, 0f, 0f);
        for (var side = 0; side < 2; side++)
        foreach (var row in rows)
        {
            var outward = row.LeftNormal * (side == 0 ? 1f : -1f);
            var fadeIn = TerrainSmoothStep(0f, PrologueTrailReliefFadeInLength, row.Distance);
            var fadeOut = 1f - TerrainSmoothStep(totalLength - PrologueTrailReliefFadeOutLength,
                totalLength, row.Distance);
            var endFade = fadeIn * fadeOut;
            foreach (var (offset, height) in PrologueTrailReliefProfile)
            {
                var point = row.Center + outward * (offset * row.MiterScale);
                var position = new Vector3(point.X, ForestGroundHeight(point.X, point.Y)
                    + height * endFade + PrologueTrailReliefSurfaceOffset, point.Y);
                surface.SetColor(freshSnowMask);
                surface.SetUV2(new Vector2(point.X / 8f, 1f - point.Y / 8f));
                surface.AddVertex(position);
            }
        }

        var columns = PrologueTrailReliefProfile.Length;
        var verticesPerSide = rows.Count * columns;
        for (var side = 0; side < 2; side++)
        {
            var sideOffset = side * verticesPerSide;
            for (var row = 0; row < rows.Count - 1; row++)
            for (var column = 0; column < columns - 1; column++)
            {
                var a = sideOffset + row * columns + column;
                var b = a + columns;
                var c = b + 1;
                var d = a + 1;
                if (side == 0)
                {
                    surface.AddIndex(a); surface.AddIndex(b); surface.AddIndex(c);
                    surface.AddIndex(a); surface.AddIndex(c); surface.AddIndex(d);
                }
                else
                {
                    surface.AddIndex(a); surface.AddIndex(c); surface.AddIndex(b);
                    surface.AddIndex(a); surface.AddIndex(d); surface.AddIndex(c);
                }
            }
        }

        surface.GenerateNormals();
        var mesh = surface.Commit();
        mesh.ResourceName = "W01_F00_F03_SoftSnowShoulders";
        return mesh;
    }
}
