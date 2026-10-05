using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal const string ZiratFamilyInteractionId = "urman.chapter1:interaction/zirat-family-links";
    private const string ZiratFamilyDocumentId = "urman.chapter1:document/zirat-family-links";
    private readonly List<Label3D> _ziratFamilyInscriptions = [];
    private MeshInstance3D[] _ziratFamilyMarkers = [];
    private Vector3[] _ziratFamilyPathFaces = [];
    internal InteractionTarget? ZiratFamilyTarget { get; private set; }
    internal IReadOnlyList<Label3D> ZiratFamilyInscriptions => _ziratFamilyInscriptions;
    internal IReadOnlyList<MeshInstance3D> ZiratFamilyMarkers => _ziratFamilyMarkers;
    internal Vector3 ZiratFamilyReadingPoint { get; private set; }

    // Called once after the ordinary observation guards, interaction routing and
    // final contact work exist. The original authored markers remain their owners.
    private void BuildZiratFamily()
    {
        if (ZiratFamilyTarget is not null) return;
        var bridge = _runtimeBridge
            ?? throw new InvalidOperationException("Zirat inscriptions require the existing runtime.");
        var presentation = GetNode<Node3D>(
            "Act1CoreWorldGreybox/ZiratMemoryField/ZiratRoadsideAuthoredKitPresentation");
        var group = presentation.GetNode<Node3D>("ZiratAuthoredMarkerGroupLow/ZiratMarkerGroup_Low");
        _ziratFamilyMarkers = new[] { 0, 1 }.Select(index =>
            group.GetNode<MeshInstance3D>($"ZiratMarkerGroup_Low_Marker_{index:00}_LOD0")).ToArray();
        var sourceIds = new[]
        {
            "urman.chapter1:text/zirat-sabirov-inscription",
            "urman.chapter1:text/zirat-sabirova-inscription"
        };
        for (var index = 0; index < _ziratFamilyMarkers.Length; index++)
            MountZiratInscription(_ziratFamilyMarkers[index], sourceIds[index], bridge.ResolveText(sourceIds[index]));

        var path = FindDescendants<MeshInstance3D>(presentation).Single(mesh =>
            mesh.Name == "ZiratPathEdge_PathRibbon_00_LOD0");
        _ziratFamilyPathFaces = path.Mesh!.GetFaces().Select(vertex => path.GlobalTransform * vertex).ToArray();
        var stand = path.GlobalTransform * new Vector3(-.37f, 0, -1.67f);
        ZiratFamilyReadingPoint = new(stand.X, AgentBAct1HeightField.CollisionGround(stand.X, stand.Z) + .04f, stand.Z);

        var first = _ziratFamilyMarkers[0];
        var bounds = first.Mesh!.GetAabb();
        // The semantic ray surface sits just outside the unchanged physical
        // marker envelope. It cannot block the player or reveal a visible proxy.
        var localTarget = new Vector3(bounds.GetCenter().X, bounds.Position.Y + bounds.Size.Y * .49f,
            bounds.End.Z + .016f);
        var zone = (StyleBenchmarkZone)_zoneInstances["zirat_road"];
        ZiratFamilyTarget = zone.MakeInteractionBox("ReadZiratFamilyInscriptions",
            new Vector3(bounds.Size.X * .82f, bounds.Size.Y * .54f, .025f), Vector3.Zero, "554d43",
            ZiratFamilyInteractionId, bridge.ResolveText("urman.chapter1:text/zirat-family-links"),
            documentId: ZiratFamilyDocumentId, rayOnly: true);
        ZiratFamilyTarget.GlobalTransform = new Transform3D(first.GlobalBasis.Orthonormalized(), first.ToGlobal(localTarget));
        ZiratFamilyTarget.SetMeta("sourceMarkers", string.Join("|", _ziratFamilyMarkers.Select(mesh => mesh.GetPath())));
        ZiratFamilyTarget.SetMeta("observationReferenceEye", ZiratFamilyReadingPoint + Vector3.Up * 1.55f);
        ZiratFamilyTarget.SetMeta("observationLookAt", ZiratFamilyTarget.GlobalPosition);
        ZiratFamilyTarget.SetMeta("knowledgeOwner", "document.openEffects; no proximity effects");
        ObserveGuard(ZiratFamilyTarget, "Надписи читаются с дорожки, с лицевой стороны памятников.",
            ZiratFamilyCanRead);
        _observationRayExclusions.Add(ZiratFamilyTarget.GetRid());
        _interactionsByZone["zirat_road"] = _interactionsByZone["zirat_road"].Append(
            new InteractionBinding(ZiratFamilyTarget, ZiratFamilyTarget.ActiveCollisionLayer,
                ZiratFamilyTarget.CollisionMask)).ToArray();
        ApplyInteractionRouting();
    }

    private bool ZiratFamilyCanRead(Camera3D camera)
    {
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null || !ZiratFamilyOnPath(player.GlobalPosition)
            || ZiratFamilyTarget is null
            || !ObservationNear(camera, ZiratFamilyTarget.GlobalPosition, 3.1f, .72f))
            return false;
        foreach (var marker in _ziratFamilyMarkers)
        {
            if (!ObservationMeshPresent(marker, camera)) return false;
            var localEye = marker.ToLocal(camera.GlobalPosition);
            if (localEye.Z <= marker.Mesh!.GetAabb().End.Z + .08f) return false;
        }
        return _ziratFamilyInscriptions.All(label => label.IsVisibleInTree()
            && label.GlobalBasis.Z.Normalized().Dot((camera.GlobalPosition - label.GlobalPosition).Normalized()) > .15f
            && camera.GlobalPosition.DistanceTo(label.GlobalPosition) < 3.3f
            && ObservationFramed(camera, label.GlobalPosition));
    }

    internal bool ZiratFamilyOnPath(Vector3 point)
    {
        var p = new Vector2(point.X, point.Z);
        for (var index = 0; index + 2 < _ziratFamilyPathFaces.Length; index += 3)
        {
            var a = new Vector2(_ziratFamilyPathFaces[index].X, _ziratFamilyPathFaces[index].Z);
            var b = new Vector2(_ziratFamilyPathFaces[index + 1].X, _ziratFamilyPathFaces[index + 1].Z);
            var c = new Vector2(_ziratFamilyPathFaces[index + 2].X, _ziratFamilyPathFaces[index + 2].Z);
            var ab = (b - a).Cross(p - a);
            var bc = (c - b).Cross(p - b);
            var ca = (a - c).Cross(p - c);
            if (Math.Abs((b - a).Cross(c - a)) > .00001f
                && ((ab >= -.001f && bc >= -.001f && ca >= -.001f)
                    || (ab <= .001f && bc <= .001f && ca <= .001f)))
                return true;
        }
        return false;
    }

    private void MountZiratInscription(MeshInstance3D marker, string sourceId, string text)
    {
        var lines = text.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != 3 || text == sourceId)
            throw new InvalidOperationException("Zirat inscription must contain a given name, surname and date line: " + sourceId);
        var mesh = marker.Mesh ?? throw new InvalidOperationException("Zirat inscription has no authored stone surface.");
        var bounds = mesh.GetAabb();
        var faces = mesh.GetFaces();
        var font = ThemeDB.FallbackFont;
        for (var index = 0; index < lines.Length; index++)
        {
            const int fontSize = 48;
            var y = bounds.Position.Y + bounds.Size.Y * (.67f - index * .18f);
            var textSize = font.GetStringSize(lines[index], HorizontalAlignment.Left, -1, fontSize);
            var pixelSize = 0f;
            var surface = Vector3.Zero;
            var normal = Vector3.Back;
            var basis = Basis.Identity;
            // The imported stone facets are slightly twisted. Pick an actual
            // front-surface anchor near the centre that leaves the most readable
            // complete line, instead of shrinking it at the diagonal mesh seam.
            // Centre-first ordering keeps equal fits visually aligned.
            for (var sample = 0; sample < 13; sample++)
            {
                var offset = sample == 0 ? 0 : (sample + 1) / 2 * (sample % 2 == 1 ? -1 : 1);
                var ray = new Vector3(bounds.GetCenter().X + offset * bounds.Size.X * .025f, y, bounds.End.Z + .1f);
                if (!ZiratFrontSurface(faces, ray, Vector3.Forward, out var at, out var outward)) continue;
                var horizontal = Vector3.Up.Cross(outward).Normalized();
                var candidateBasis = new Basis(horizontal, outward.Cross(horizontal).Normalized(), outward);
                var candidateSize = Math.Min(.0013f, bounds.Size.X * .55f / Math.Max(textSize.X, 1));
                while (candidateSize > .00025f && !ZiratLabelFits(faces, at, candidateBasis,
                           textSize.X * candidateSize, font.GetHeight(fontSize) * candidateSize))
                    candidateSize *= .94f;
                if (candidateSize <= pixelSize + .000001f) continue;
                pixelSize = candidateSize;
                surface = at;
                normal = outward;
                basis = candidateBasis;
            }
            if (pixelSize <= .00025f)
                throw new InvalidOperationException("Existing zirat facet is too small for the compiled inscription.");
            var label = new Label3D
            {
                Name = $"FamilyInscription{index + 1}",
                Text = lines[index],
                Font = font,
                FontSize = fontSize,
                PixelSize = pixelSize,
                OutlineSize = 0,
                Modulate = new Color("25251f"),
                DoubleSided = false,
                NoDepthTest = false,
                Shaded = true,
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                Transform = new Transform3D(basis, surface + normal * .0015f)
            };
            label.SetMeta("compiledTextId", sourceId);
            label.SetMeta("surfaceOwner", marker.Name.ToString());
            label.SetMeta("mountOffsetMetres", .0015f);
            label.SetMeta("facetWidthMetres", textSize.X * pixelSize);
            label.SetMeta("facetHeightMetres", font.GetHeight(fontSize) * pixelSize);
            marker.AddChild(label);
            _ziratFamilyInscriptions.Add(label);
        }
    }

    private static bool ZiratLabelFits(Vector3[] faces, Vector3 center, Basis basis, float width, float height)
    {
        foreach (var x in new[] { -.5f, 0, .5f })
        foreach (var y in new[] { -.5f, .5f })
        {
            var corner = center + basis.X * (width * x) + basis.Y * (height * y);
            if (!ZiratFrontSurface(faces, corner + basis.Z * .025f, -basis.Z, out var hit, out var normal)
                || corner.DistanceTo(hit) > .0015f || normal.Dot(basis.Z) < .998f)
                return false;
        }
        return true;
    }

    private static bool ZiratFrontSurface(Vector3[] faces, Vector3 from, Vector3 direction,
        out Vector3 point, out Vector3 normal)
    {
        point = normal = Vector3.Zero;
        var nearest = float.PositiveInfinity;
        for (var index = 0; index + 2 < faces.Length; index += 3)
        {
            var a = faces[index];
            var b = faces[index + 1];
            var c = faces[index + 2];
            var hit = Geometry3D.RayIntersectsTriangle(from, direction, a, b, c);
            if (hit.VariantType == Variant.Type.Nil) continue;
            var at = hit.AsVector3();
            var distance = from.DistanceSquaredTo(at);
            if (distance >= nearest) continue;
            var outward = (b - a).Cross(c - a).Normalized();
            if (outward.Dot(direction) > 0) outward = -outward;
            nearest = distance;
            point = at;
            normal = outward;
        }
        return float.IsFinite(nearest);
    }

    /// <summary>
    /// Recomposes the relocated zīrat stones inside the garden-edge plot. The
    /// first two stones of the low group are the inscribed family pair: they
    /// keep the kit's path-relative reading offsets (the reading target is
    /// built from their final transforms), only seated on the real terrain.
    /// Every other stone is laid out in two quiet rows running west from the
    /// path, so the cemetery reads as an ordered plot rather than the old
    /// roadside cluster.
    /// </summary>
    private static void ComposeRelocatedZiratStones(Node3D lowMarkerPlacement, Node3D farMarkerPlacement)
    {
        foreach (var placement in new[] { lowMarkerPlacement, farMarkerPlacement })
        {
            var p = placement.GlobalPosition;
            placement.GlobalPosition = new Vector3(p.X, (float)AgentBAct1HeightField.CollisionGround(p.X, p.Z) - .02f, p.Z);
        }

        var lowStones = lowMarkerPlacement.FindChildren("*Marker_*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().OrderBy(stone => stone.Name.ToString(), StringComparer.Ordinal).ToArray();
        var farStones = farMarkerPlacement.FindChildren("*Marker_*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().OrderBy(stone => stone.Name.ToString(), StringComparer.Ordinal).ToArray();

        var family = lowStones.Take(2).ToArray();
        var quiet = lowStones.Skip(2).Concat(farStones).ToArray();
        var columns = Mathf.Max(1, (int)Mathf.Ceil(quiet.Length / 2f));
        for (var index = 0; index < quiet.Length; index++)
        {
            var row = index % 2;
            var column = index / 2;
            var x = Mathf.Lerp(-13.6f, -20.6f, columns == 1 ? 0f : column / (float)(columns - 1));
            var z = row == 0 ? -78.4f : -83.6f;
            var y = (float)AgentBAct1HeightField.CollisionGround(x, z) - .03f;
            quiet[index].GlobalPosition = new Vector3(x, y, z);
        }

        foreach (var stone in family)
        {
            var p = stone.GlobalPosition;
            stone.GlobalPosition = new Vector3(p.X, (float)AgentBAct1HeightField.CollisionGround(p.X, p.Z) - .03f, p.Z);
        }

        lowMarkerPlacement.SetMeta("ziratRelocatedStones", $"family={family.Length} quiet={quiet.Length} rows=2 plot=x[-22.4..-9.2] z[-74..-88]");
        GD.Print($"zirat-relocation: family stones={family.Length} quiet stones={quiet.Length} rows=2");
    }

}
