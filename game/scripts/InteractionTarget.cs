using System;
using Godot;

namespace Urman.Godot;

public partial class InteractionTarget : StaticBody3D
{
    [Export]
    public string InteractionId { get; set; } = "unknown";

    [Export]
    public string Prompt { get; set; } = "Осмотреть";

    [Export]
    public string TargetZoneId { get; set; } = string.Empty;

    [Export]
    public string TargetSpawnPointId { get; set; } = "entry";

    [Export]
    public string DialogueId { get; set; } = string.Empty;

    [Export]
    public string DocumentId { get; set; } = string.Empty;

    [Export]
    public string JournalEntryId { get; set; } = string.Empty;

    // Empty by default so ordinary targets remain silent. Owners of physical
    // doors/gates opt in at construction without coupling RuntimeBridge to foley.
    internal string WorldFoleySample { get; set; } = string.Empty;

    private uint _activeCollisionLayer;
    internal uint ActiveCollisionLayer => _activeCollisionLayer;
    private bool? _available;
    private RuntimeBridge? _bridge;
    private bool _presentationEnabled = true;

    // The existing world router also uses IsSemanticallyAvailable, so an owner
    // waiting for a physical projection cannot be re-enabled by a later refresh.
    internal void SetPresentationEnabled(bool enabled)
    {
        _presentationEnabled = enabled;
        CollisionLayer = IsSemanticallyAvailable() ? _activeCollisionLayer : 0u;
    }

    // A moving character owns its physical body separately. Keep this stable
    // interaction target on the ray layer, including future routing refreshes.
    internal void ConfigureRayOnly()
    {
        _activeCollisionLayer = 4u;
        CollisionMask = 0u;
        CollisionLayer = _available is null || IsSemanticallyAvailable() ? 4u : 0u;
    }

    // Presentation-only repeat actions stay outside RuntimeBridge state. The
    // one-shot interaction still owns the journal/knowledge commit; a caller
    // may expose a local repeat after that commit without creating another
    // journal entry or persistence owner.
    internal Func<bool>? PresentationRepeatAvailable { get; set; }
    internal Action? PresentationRepeat { get; set; }

    // A physical prerequisite owned by the world, not by authored knowledge:
    // the target stays authored-available while the gate is shut, so the
    // ordinary interaction simply offers no reading, and the gate hint explains
    // what is missing. Copying no knowledge and writing no state by itself.
    internal Func<bool>? PresentationGate { get; set; }
    internal string PresentationGateHint { get; set; } = string.Empty;

    /// <summary>Authored conditions pass, but the world gate is still shut.</summary>
    internal bool HeldByGate => _available == true && PresentationGate is not null && !PresentationGate();

    public override void _Ready()
    {
        _activeCollisionLayer = CollisionLayer;
        AttachRuntimeBridge();
        RefreshAvailability();
    }

    public override void _ExitTree()
    {
        if (_bridge is not null && GodotObject.IsInstanceValid(_bridge))
        {
            _bridge.RuntimeStateChanged -= OnRuntimeStateChanged;
        }

        _bridge = null;
        PresentationRepeatAvailable = null;
        PresentationRepeat = null;
        PresentationGate = null;
        PresentationGateHint = string.Empty;
    }

    internal bool IsSemanticallyAvailable() => _presentationEnabled
        && (_available == true
            || (_available == false && PresentationRepeatAvailable?.Invoke() == true));

    public bool IsAvailable() => IsSemanticallyAvailable()
        && (PresentationGate?.Invoke() ?? true)
        && (_bridge?.CanPhysicallyUseInteraction(InteractionId) ?? true);

    public async void Interact()
    {
        AttachRuntimeBridge();
        if (!IsAvailable())
        {
            return;
        }

        // Once the authored interaction has been committed, the target can
        // perform a local presentation repeat. It deliberately bypasses the
        // narrative dispatcher, so New Game/load retain one journal record.
        if (_available != true)
        {
            if (PresentationRepeatAvailable?.Invoke() != true || PresentationRepeat is null)
            {
                return;
            }

            PresentationRepeat();
            return;
        }

        var bridge = _bridge;
        if (bridge is null || bridge.SessionIdentity is not { } session)
        {
            return;
        }

        if (!await bridge.DispatchInteractionAsync(InteractionId)
            || !IsPresentationCurrent(bridge, session))
        {
            return;
        }

        if (InteractionId == "urman.chapter1:interaction/oldpc-power")
        {
            bridge.OpenOldPcUi();
        }

        if (!string.IsNullOrWhiteSpace(DialogueId))
        {
            bridge.OpenDialogueUi(DialogueId);
        }

        if (!string.IsNullOrWhiteSpace(DocumentId))
        {
            var opened = await bridge.OpenDocumentAsync(DocumentId);
            if (!IsPresentationCurrent(bridge, session)) return;
            if (opened) bridge.OpenDocumentUi(DocumentId);
        }

        if (!string.IsNullOrWhiteSpace(JournalEntryId)
            && bridge.JournalEntries().Any(entry => entry.EntryId == JournalEntryId))
            (GetTree().GetFirstNodeInGroup("journal_ui") as JournalUi)?.Open(bridge, JournalEntryId);

        // Capture the source before a non-connected Main queues this target
        // for deletion during SwitchZone. World foley must be hosted by the
        // stable zone manager whenever the interaction changes zones.
        var sourcePosition = GlobalPosition;
        Main? main = null;
        if (!string.IsNullOrWhiteSpace(TargetZoneId))
        {
            main = GetTree().GetFirstNodeInGroup("zone_manager") as Main;
            if (main is not null && !await main.SwitchZoneAsync(TargetZoneId, TargetSpawnPointId)) return;
        }

        if (!string.IsNullOrWhiteSpace(WorldFoleySample))
        {
            // Entering the house changes logical zone, so resolve the source
            // from the active interior portal after the switch. Leaving the
            // house resolves the exterior portal where the player arrives.
            var source = sourcePosition;
            if (TargetZoneId == "house_old_pc"
                && main?.FindChild("HouseExit", true, false) is Node3D houseExit)
            {
                source = houseExit.GlobalPosition;
            }
            else if (InteractionId == "urman.chapter1:interaction/house-to-route"
                && main?.FindChild("HouseDoor", true, false) is Node3D houseDoor)
            {
                source = houseDoor.GlobalPosition;
            }

            Node host = main is not null ? main : this;
            UiFoley.PlayWorld(host, source, WorldFoleySample);
        }
    }

    // Persistence can finish after load/restart, menu return, or zone disposal.
    // Reuse the actual session and menu state instead of a separate lifecycle clock.
    internal bool IsPresentationCurrent(RuntimeBridge bridge, Urman.Core.Runtime.RuntimeKernel session) =>
        GodotObject.IsInstanceValid(this) && !IsQueuedForDeletion() && IsInsideTree()
        && GodotObject.IsInstanceValid(bridge) && !bridge.IsQueuedForDeletion() && bridge.IsInsideTree()
        && ReferenceEquals(session, bridge.SessionIdentity)
        && !GetTree().GetNodesInGroup("main_menu").OfType<MainMenuUi>().Any(menu => !menu.IsDismissed);

    private void RefreshAvailability()
    {
        AttachRuntimeBridge();
        var available = _bridge?.IsInteractionAvailable(InteractionId) == true;
        if (_available == available)
        {
            return;
        }

        _available = available;
        // RuntimeStateChanged subscribers are ordered by attachment. If this
        // target refreshes after Act1ConnectedWorld.ApplyInteractionRouting,
        // preserve the ray layer for an authored local repeat instead of
        // clobbering the routing pass with zero.
        var repeatAvailable = !available && PresentationRepeatAvailable?.Invoke() == true;
        CollisionLayer = _presentationEnabled && (available || repeatAvailable) ? _activeCollisionLayer : 0;
        foreach (var child in GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                mesh.Visible = available;
            }
        }
    }

    private void AttachRuntimeBridge()
    {
        if (_bridge is not null && GodotObject.IsInstanceValid(_bridge))
        {
            return;
        }

        _bridge = null;

        if (GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge bridge)
        {
            _bridge = bridge;
            _bridge.RuntimeStateChanged += OnRuntimeStateChanged;
        }
    }

    private void OnRuntimeStateChanged() => RefreshAvailability();
}
