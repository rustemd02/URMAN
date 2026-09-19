using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class FirstPersonController
{
    internal sealed record BodyFootwear(MeshInstance3D Node, ArrayMesh Boots, ArrayMesh Indoor,
        Material? BootMaterial, Skin Skin, NodePath Skeleton, Transform3D LocalPose);
    private readonly List<BodyFootwear> _bodyFootwear = new();
    private bool _indoorFootwear;
    internal IReadOnlyList<BodyFootwear> FootwearPresentation => _bodyFootwear;
    internal bool FootwearPresentationReady => _bodyFootwear.Count == 2;
    internal bool IndoorFootwearActive => _indoorFootwear;

    private void InitializeFootwearPresentation(IEnumerable<MeshInstance3D> selected)
    {
        if (_bodyFootwear.Count != 0) throw new InvalidOperationException("Player footwear is initialized once with the existing body.");
        var staged = new List<BodyFootwear>();
        try
        {
            foreach (var side in new[] { "Left", "Right" })
            {
                var node = selected.Single(mesh => mesh.Name == $"{BodyPrefix}_Boot{side}_LOD0");
                if (node.Mesh is not ArrayMesh original || node.Skin is not { } skin)
                    throw new InvalidOperationException("The final player ankle must have its existing mesh and skin.");
                staged.Add(new(node, original, Act1ConnectedWorld.CreateIndoorFootwearMesh(original),
                    node.MaterialOverride, skin, node.Skeleton, node.Transform));
            }
        }
        catch { foreach (var item in staged) item.Indoor.Dispose(); throw; }
        _bodyFootwear.AddRange(staged);
        ProjectIndoorFootwear(_indoorFootwear);
    }

    // Idempotent projection, including a load before InitializeVisibleBody has
    // run. No save, interaction, inventory or location policy lives in the body.
    internal void ProjectIndoorFootwear(bool removed)
    {
        _indoorFootwear = removed;
        foreach (var item in _bodyFootwear)
        {
            item.Node.Mesh = removed ? item.Indoor : item.Boots;
            item.Node.MaterialOverride = removed
                ? PainterlyMaterialLibrary.ForColor("6d6c64", "cloth", sheltered: true) : item.BootMaterial;
        }
    }
}
