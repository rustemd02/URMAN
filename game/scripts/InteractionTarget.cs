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

    private uint _activeCollisionLayer;
    private bool? _available;
    private RuntimeBridge? _bridge;

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
    }

    public bool IsAvailable() => _available == true;

    public async void Interact()
    {
        AttachRuntimeBridge();
        var bridge = _bridge;
        if (bridge is null || !IsAvailable() || !await bridge.DispatchInteractionAsync(InteractionId))
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

        if (!string.IsNullOrWhiteSpace(DocumentId) && await bridge.OpenDocumentAsync(DocumentId))
        {
            bridge.OpenDocumentUi(DocumentId);
        }

        if (!string.IsNullOrWhiteSpace(JournalEntryId)
            && bridge.JournalEntries().Any(entry => entry.EntryId == JournalEntryId))
            (GetTree().GetFirstNodeInGroup("journal_ui") as JournalUi)?.Open(bridge, JournalEntryId);

        if (!string.IsNullOrWhiteSpace(TargetZoneId))
        {
            var main = GetTree().GetFirstNodeInGroup("zone_manager") as Main;
            main?.SwitchZone(TargetZoneId, TargetSpawnPointId);
        }
    }

    private void RefreshAvailability()
    {
        AttachRuntimeBridge();
        var available = _bridge?.IsInteractionAvailable(InteractionId) == true;
        if (_available == available)
        {
            return;
        }

        _available = available;
        CollisionLayer = available ? _activeCollisionLayer : 0;
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
