using Godot;

namespace Urman.Godot;

/// <summary>
/// ACT1-VILLAGE-COMPOSITION, babay's yard (author 2026-09-22: the plot reads
/// cluttered; 2026-09-25: "убери лишнее"). Four generation passes each laid
/// their own fence across the yard's street side: within three metres of the
/// real boundary with the playable side gate stood a front palisade, a fence
/// stub, two pale rail "street boundaries" and a kit gate planted on the entry
/// path. Only those duplicates go; the real boundary, its gate and collision,
/// the well, sled, haystack, woodpile and every interaction stay.
/// </summary>
public partial class Act1ConnectedWorld
{
    internal static readonly (string Name, string Reason)[] BabaiYardDeclutter =
    [
        ("BabaiYardFrontPalisade", "second front line 2 m inside the real yard boundary"),
        ("BabaiEbiHouseApproachFenceEast", "2 m fence stub standing alone inside the entry"),
        ("BabaiYardStreetWestBoundary", "pale rail run duplicating the yard's street boundary"),
        ("BabaiYardStreetEastBoundary", "pale rail run duplicating the yard's street boundary")
    ];

    private void ComposeBabaiYard()
    {
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var hidden = 0;
        void Hide(Node node, string reason)
        {
            var gameplay = new[] { node }.Concat(FindDescendants<Node>(node)).Where(IsProtectedGameplayNode).ToArray();
            if (gameplay.Length > 0)
                throw new InvalidOperationException($"Yard composition target carries gameplay: {node.GetPath()}");
            node.SetMeta("connectedWorldSuppressionReason", "yard composition 2026-09-25: " + reason);
            HidePresentationNode(node);
            hidden++;
        }
        foreach (var (name, reason) in BabaiYardDeclutter)
        {
            // Fence runs and palisades are loose members named after the run
            // (rails, posts, snow caps), not one node; take the outermost ones.
            var members = FindDescendants<Node3D>(core).Where(n => n.Name.ToString().StartsWith(name, StringComparison.Ordinal)
                && !(n.GetParent()?.Name.ToString().StartsWith(name, StringComparison.Ordinal) ?? false)).ToArray();
            if (members.Length == 0) throw new InvalidOperationException("Yard composition target is missing: " + name);
            foreach (var member in members) Hide(member, reason);
        }
        // The kit gate planted on the entry path, two metres inside the real gate.
        var entryGate = new Vector2(-22.5f, 2.6f);
        foreach (var placement in FindDescendants<Node3D>(core).Where(n => n.IsVisibleInTree() && n.GetChildren()
                     .Any(c => c.Name.ToString().StartsWith("Gate_CrookedTimber", StringComparison.Ordinal))).ToArray())
            if (new Vector2(placement.GlobalPosition.X, placement.GlobalPosition.Z).DistanceTo(entryGate) < 1.4f)
                Hide(placement, "kit gate standing on the entry path inside the real gate");
        SetMeta("babaiYardDeclutterHidden", hidden);
        GD.Print($"act1-yard-composition: hidden={hidden}");
    }
}
