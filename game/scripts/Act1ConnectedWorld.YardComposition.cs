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
///
/// 2026-09-26 note: the inventory's zone A/B duplicates all belonged to the
/// legacy "Act1AuthoredOutdoorBackbone" yard. That builder is not called on the
/// connected-world path any more (its root never materializes), so those
/// duplicates do not exist in the built world and nothing is hidden for them.
/// </summary>
public partial class Act1ConnectedWorld
{
    internal static readonly (string Name, string Reason)[] BabaiYardDeclutter =
    [
        ("BabaiYardFrontPalisade", "second front line 2 m inside the real yard boundary"),
        ("BabaiEbiHouseApproachFenceEast", "2 m fence stub standing alone inside the entry"),
        ("BabaiYardStreetWestBoundary", "pale rail run duplicating the yard's street boundary"),
        ("BabaiYardStreetEastBoundary", "pale rail run duplicating the yard's street boundary"),
        // These decorative runs belong to the old yard layout. Relocation
        // marks them as household members, which otherwise exempts them from
        // RebuildYardFences and leaves extra internal lines and orphan posts.
        // The playable side gate/RearYardGate and their real fence spans have
        // separate owners and are not part of these presentation prefixes.
        ("BabaiYardWestBoundary", "old decorative west line and its detached posts"),
        ("BabaiYardEastBoundary", "old decorative east line inside the working yard"),
        ("BabaiYardWattleRun", "two decorative internal fence runs bounding no current household plot"),
        ("BabaiEbiHouseBackFence", "old approach enclosure behind the relocated household"),
        ("BabaiEbiHouseApproachFenceWest", "old approach fence fragment beside the working yard")
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
        // The Agent B village kit laid its own west gate and fence run inside
        // the yard mouth, 2.5 m behind the authored gate the player actually
        // walks through, plus two bare saplings in the middle of the working
        // area. Those go; the real gate, the yard fence line and both yard
        // trees stay.
        foreach (var duplicate in new[]
                 {
                     ("GateBabai_PostW", "Agent B kit gate post inside the authored yard gate"),
                     ("GateBabai_PostCapW", "Agent B kit gate cap inside the authored yard gate"),
                     ("FenceBabaiW_N", "Agent B kit fence run duplicating the yard's west boundary")
                 })
        {
            var targets = FindDescendants<Node3D>(core)
                .Where(n => n.Name.ToString().StartsWith(duplicate.Item1, StringComparison.Ordinal)).ToArray();
            if (targets.Length == 0) throw new InvalidOperationException(
                "Yard composition target is missing: " + duplicate.Item1);
            foreach (var target in targets) Hide(target, duplicate.Item2);
        }

        // Generated Plant indices change when a street gains a real verge.
        // Match source-confirmed variant/root instead of suppressing whichever
        // unrelated sapling happens to inherit the old numerical name.
        foreach (var duplicate in new[]
        {
            (Variant: "WinterBirch_1", Root: new Vector2(-29f, 6.4f), Reason: "bare sapling standing in the yard's working area"),
            (Variant: "WinterWillow_1", Root: new Vector2(-25.704922f, 5.9302864f), Reason: "bare willow standing in the yard's working area")
        })
        {
            var targets = FindDescendants<Node3D>(core).Where(n => n.GetParent()?.Name == "AgentB_PlantedFoliage" && n.HasMeta("plantVariant")
                && n.GetMeta("plantVariant").AsString() == duplicate.Variant
                && new Vector2(n.GlobalPosition.X, n.GlobalPosition.Z).DistanceSquaredTo(duplicate.Root) < .0001f).ToArray();
            // The planting follows the street axes; after relayout v3 a sapling may no longer grow here.
            if (targets.Length > 1) throw new InvalidOperationException(
                $"Yard composition root match count {targets.Length}: {duplicate.Variant}@{duplicate.Root}");
            foreach (var target in targets) Hide(target, duplicate.Reason);
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

    // ── VIS-014: one prop contact on the deck ────────────────────────────────
    // The repair corner is stored inside the tool barn: the barn's own plank Floor
    // (a 10 cm deck) is what actually carries its legs, while every support member
    // was originally let down onto CollisionGround, the terrain *under* that deck.
    // After the store was built the four legs of the sawhorse, the four shelf legs
    // and the two gangway legs therefore ended either below the planks (piercing
    // the deck, duplicate support volume) or above them (floating).
    // The pass seats each member's own foot on the deck top with the same authored
    // 4 mm clearance the terrain feet use, keeps the top exactly where its load
    // sits, and trims away only what the deck already carries.

    /// <summary>VIS-014: a visible contact gap or penetration larger than this is a
    /// defect; the card allows at most 1 cm on visible contacts.</summary>
    internal const float DeckContactTolerance = .010f;

    /// <summary>VIS-014: the authored air the foot keeps above its support — the
    /// same 4 mm the terrain-seated yard feet use, so a deck foot and a ground foot
    /// read the same way.</summary>
    internal const float DeckContactClearance = .004f;

    /// <summary>VIS-014: member names that carry something.</summary>
    internal static readonly string[] DeckSupportMemberTokens = ["Leg", "RoofPost", "Support"];

    /// <summary>Seats every yard piece stored on a real deck onto that deck. Runs
    /// after the storage pass and before the kit blockers are baked.</summary>
    private void SeatYardPropsOnSupportDecks(Node3D core)
    {
        var barn = core.GetNodeOrNull<Node3D>("BabaiStorageBarn");
        if (barn?.GetNodeOrNull<MeshInstance3D>("Floor") is not { } deck || deck.Mesh is null) return;
        var planks = deck.GlobalTransform * deck.GetAabb();
        var deckTop = planks.End.Y;
        var seats = 0; var trimmed = 0; var letDown = 0; var worst = 0f;
        var edited = new HashSet<ulong>();
        var editedShapes = new HashSet<ulong>();
        foreach (var stored in FindDescendants<Node3D>(core).Where(n => n.HasMeta("storedIn")))
        {
            if (stored.GetMeta("storedIn").AsString() != barn.Name.ToString()) continue;
            if (FindDescendants<InteractionTarget>(stored).Any())
                throw new InvalidOperationException("Deck seating target carries gameplay: " + stored.GetPath());
            foreach (var member in RuralPropGeometry.SupportMembers(stored, DeckSupportMemberTokens))
            {
                var box = member.GlobalTransform * member.GetAabb();
                var centre = new Vector2(box.GetCenter().X, box.GetCenter().Z);
                if (centre.X < planks.Position.X - .06f || centre.X > planks.End.X + .06f
                    || centre.Y < planks.Position.Z - .06f || centre.Y > planks.End.Z + .06f) continue;
                var deviation = RuralPropGeometry.SeatSupportFoot(member, deckTop, DeckContactClearance,
                    DeckContactTolerance, editedShapes);
                if (Mathf.Abs(deviation) <= .00001f || !edited.Add((ulong)member.GetInstanceId())) continue;
                seats++;
                worst = Mathf.Max(worst, Mathf.Abs(deviation));
                if (deviation < 0f) trimmed++; else letDown++;
            }
        }
        // The four-legged sawhorse is the card's own criterion: four feet, not
        // three, not a single block under the whole stand.
        var trestleFeet = FindDescendants<MeshInstance3D>(core)
            .Count(m => m.Name.ToString().StartsWith("TrestleLeg", StringComparison.Ordinal) && m.IsVisibleInTree());
        var after = core.GetNodeOrNull<Node3D>("YardRepairCorner");
        var underDeck = after is null ? 0 : RuralPropGeometry.SupportMembers(after, DeckSupportMemberTokens)
            .Count(m => (m.GlobalTransform * m.GetAabb()).Position.Y < planks.Position.Y + .002f);
        if (trestleFeet != 4) GD.PushWarning($"VIS-014: the yard sawhorse carries {trestleFeet} visible legs, expected 4");
        SetMeta("deckSeatsCorrected", seats);
        SetMeta("deckTrimmedBuried", trimmed);
        SetMeta("deckLetDownFloating", letDown);
        SetMeta("deckWorstDeviationM", worst);
        SetMeta("deckUndersidePiercing", underDeck);
        SetMeta("deckContactToleranceM", DeckContactTolerance);
        SetMeta("sawhorseFootCount", trestleFeet);
        GD.Print($"act1-deck-seat: deck={deck.GetPath()} topY={deckTop:F3} seats={seats} trimmed={trimmed} "
            + $"letDown={letDown} worst={worst * 1000f:F1}mm sawhorseFeet={trestleFeet} undersidePiercing={underDeck}");
    }

    // ── VIS-024: one yard's biography through two groups ──────────────────────
    // The east holding is the passport's working household (H04). Two groups must
    // read from the gate: where you enter, and where wood is stored and work
    // happens. Nothing is added here and nothing is deleted — only existing pieces
    // of these two groups move, and only when they sit on the yard's own walking
    // line. A piece that already leaves the line clear keeps its authored pose.

    /// <summary>VIS-024: the free corridor the pass must preserve — a person plus
    /// an arm's width of clearance on either side.</summary>
    internal const float YardCorridorHalfWidth = .45f;

    /// <summary>VIS-024: the least clear width the corridor may keep after a move.</summary>
    internal const float YardCorridorMinWidth = .90f;

    private void ComposeEastHoldingWorkYard(Node3D core)
    {
        var parcel = FindDescendants<Node3D>(core)
            .FirstOrDefault(n => n.Name == "EastStreetMidParcel" && n.IsVisibleInTree());
        if (parcel?.GetNodeOrNull<Node3D>("EastStreetMidFacade") is not { } facade) return;
        var woodpile = parcel.GetNodeOrNull<Node3D>("EastStreetMidWoodpile");
        var yard = parcel.GetNodeOrNull<Node3D>("EastStreetMidYard");
        if (woodpile is null && yard is null) return;
        var registry = AddressRegistry;
        var lot = YardLots().Where(l => l.Kind != "keep")
            .OrderBy(l => l.Centre.DistanceSquaredTo(new Vector2(facade.GlobalPosition.X, facade.GlobalPosition.Z)))
            .FirstOrDefault();
        if (registry is null || lot is null
            || lot.Centre.DistanceSquaredTo(new Vector2(facade.GlobalPosition.X, facade.GlobalPosition.Z)) > 64f) return;
        if (!registry.Addresses.TryGetValue(lot.Id, out var address)
            || !registry.Buildings.TryGetValue(address.BuildingId, out var building)
            || !registry.AccessPoints.TryGetValue(address.AccessId, out var access)) return;

        var door = new Vector2((float)building.Position.X, (float)building.Position.Z);
        var gate = new Vector2((float)access.Position.X, (float)access.Position.Z);
        // The walking line: from the street through the yard gate to the door, and
        // two metres back out to the street approach, which is the same line.
        var towardStreet = gate - door;
        if (towardStreet.LengthSquared() < .01f) return;
        towardStreet = towardStreet.Normalized();
        var lane = (Start: gate + towardStreet * 2f, End: door);

        var before = CountYardInstances(core, parcel);
        var wallAxis = new Vector2(facade.GlobalBasis.Z.X, facade.GlobalBasis.Z.Z);
        var moved = 0;
        if (woodpile is not null && !FindDescendants<InteractionTarget>(woodpile).Any())
        {
            // Group 1 — storage: the stack is laid along the house's own wall axis
            // (the delivery line ends there), never at a stray angle, and it steps
            // off the walking line to the side it already stands on.
            woodpile.RotationDegrees = new Vector3(0f, YawOf(wallAxis), 0f);
            woodpile.SetMeta("yardGroup", "storage-wood");
            woodpile.SetMeta("yardGroupReason",
                "firewood stacked along the dwelling wall where the delivery line ends, off the pedestrian line");
            moved += StepOffLane(woodpile, lane, facade) ? 1 : 0;
        }
        if (yard is not null)
        {
            // Group 2 — work: the yard bench is the one seat in this yard. It
            // belongs to the working side; if it stands on the entering line it
            // steps off it, otherwise it stays exactly as authored.
            foreach (var bench in FindDescendants<Node3D>(yard)
                         .Where(n => n.Name.ToString().StartsWith("VariantA_Yard_Bench", StringComparison.Ordinal))
                         .Where(n => n.GetParent() is null
                             || !n.GetParent().Name.ToString().StartsWith("VariantA_Yard_Bench", StringComparison.Ordinal))
                         .ToArray())
            {
                if (FindDescendants<InteractionTarget>(bench).Any()) continue;
                bench.SetMeta("yardGroup", "work-seat");
                if (StepOffLane(bench, lane, facade)) moved++;
                else bench.SetMeta("yardGroupReason", "already off the entering line; kept its authored pose");
            }
        }
        var after = CountYardInstances(core, parcel);
        var corridor = CorridorClearWidth(core, parcel, lane);
        SetMeta("eastHoldingYardGroups", 2);
        SetMeta("eastHoldingYardMoves", moved);
        SetMeta("eastHoldingVisibleInstancesBefore", before);
        SetMeta("eastHoldingVisibleInstancesAfter", after);
        SetMeta("eastHoldingCorridorWidthM", corridor);
        GD.Print($"act1-yard-biography: lot={lot.Id} groups=2 moves={moved} instances={before}->{after} "
            + $"corridor={(corridor < 0f ? 0f : corridor):F2}m (min {YardCorridorMinWidth:F2}m)");
    }

    private static int CountYardInstances(Node3D core, Node3D parcel)
    {
        var inParcel = FindDescendants<MeshInstance3D>(parcel).Count(m => m.IsVisibleInTree() && m.Mesh is not null);
        var fences = core.GetNodeOrNull<Node3D>("YardFences");
        return inParcel + (fences is null ? 0 : FindDescendants<MeshInstance3D>(fences)
            .Count(m => m.IsVisibleInTree() && m.Mesh is not null));
    }

    /// <summary>Yaw that turns a model's local +X into the given world direction.</summary>
    private static float YawOf(Vector2 direction)
        => Mathf.RadToDeg(Mathf.Atan2(-direction.Y, direction.X));

    /// <summary>VIS-024: steps one existing piece off the yard's walking line, on
    /// the side it already stands on, far enough that a person plus an arm's width
    /// still passes. Returns false — and changes nothing — when the piece is
    /// already clear, when it is not needed, or when the only free direction would
    /// push it into the dwelling.</summary>
    private static bool StepOffLane(Node3D piece, (Vector2 Start, Vector2 End) lane, Node3D house)
    {
        var box = BoundsOf(piece);
        if (box.Size == Vector3.Zero) return false;
        var length = lane.Start.DistanceTo(lane.End);
        if (length < .01f) return false;
        var step = (lane.End - lane.Start) / length;
        var across = new Vector2(step.Y, -step.X);
        var centre = new Vector2(box.GetCenter().X, box.GetCenter().Z);
        var x0 = Mathf.Min(box.Position.X, box.End.X); var x1 = Mathf.Max(box.Position.X, box.End.X);
        var z0 = Mathf.Min(box.Position.Z, box.End.Z); var z1 = Mathf.Max(box.Position.Z, box.End.Z);
        var radius = Mathf.Max(x1 - x0, z1 - z0) * .5f + YardCorridorHalfWidth;
        for (var t = 0f; t <= length + .001f; t += .2f)
        {
            var p = lane.Start + step * Mathf.Min(t, length);
            var dx = Mathf.Max(0f, Mathf.Max(x0 - p.X, p.X - x1));
            var dz = Mathf.Max(0f, Mathf.Max(z0 - p.Y, p.Y - z1));
            if (Mathf.Sqrt(dx * dx + dz * dz) >= radius) continue;
            var signed = (centre - p).Dot(across);
            var side = signed >= 0f ? 1f : -1f;
            var offset = (radius - Mathf.Abs(signed)) * side;
            if (Mathf.Abs(offset) < .001f) continue;
            var moved = new Vector3(across.X * offset, 0f, across.Y * offset);
            if (IntersectsHouse(box, moved, house)) continue;
            piece.GlobalPosition += moved;
            piece.SetMeta("yardCorridorStepM", offset);
            return true;
        }
        return false;
    }

    private static bool IntersectsHouse(Aabb piece, Vector3 shift, Node3D house)
    {
        var shell = BoundsOf(house);
        if (shell.Size == Vector3.Zero) return false;
        var moved = new Aabb(piece.Position + shift - Vector3.One * .02f, piece.Size + Vector3.One * .04f);
        var grown = new Aabb(shell.Position - Vector3.One * .1f, shell.Size + Vector3.One * .2f);
        return moved.Position.X < grown.End.X && moved.End.X > grown.Position.X
            && moved.Position.Y < grown.End.Y && moved.End.Y > grown.Position.Y
            && moved.Position.Z < grown.End.Z && moved.End.Z > grown.Position.Z;
    }

    private static float CorridorClearWidth(Node3D core, Node3D parcel, (Vector2 Start, Vector2 End) lane)
    {
        var length = lane.Start.DistanceTo(lane.End);
        var step = (lane.End - lane.Start) / Mathf.Max(.01f, length);
        var narrowest = -1f;
        for (var t = 0f; t <= length; t += .2f)
        {
            var p = lane.Start + step * Mathf.Min(t, length);
            var nearest = float.MaxValue;
            foreach (var mesh in FindDescendants<MeshInstance3D>(parcel))
            {
                if (!mesh.IsVisibleInTree() || mesh.Mesh is null) continue;
                var box = mesh.GlobalTransform * mesh.GetAabb();
                if (box.Size.Y < .06f || box.Size.Length() > 60f) continue;
                var centre = new Vector2(box.GetCenter().X, box.GetCenter().Z);
                var half = new Vector2(box.Size.X * .5f, box.Size.Z * .5f);
                var d = new Vector2(Mathf.Max(0f, Mathf.Abs(centre.X - p.X) - half.X),
                    Mathf.Max(0f, Mathf.Abs(centre.Y - p.Y) - half.Y));
                nearest = Mathf.Min(nearest, d.Length());
            }
            if (nearest != float.MaxValue) narrowest = narrowest < 0f ? nearest : Mathf.Min(narrowest, nearest);
        }
        return narrowest < 0f ? 0f : narrowest * 2f;
    }

    private static Aabb BoundsOf(Node3D node)
    {
        var result = new Aabb();
        var any = false;
        foreach (var mesh in FindDescendants<MeshInstance3D>(node))
        {
            if (!mesh.IsVisibleInTree() || mesh.Mesh is null) continue;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            if (!any) { result = box; any = true; continue; }
            result = result.Merge(box);
        }
        return any ? result : new Aabb(node.GlobalPosition, Vector3.Zero);
    }
}
