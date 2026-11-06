namespace Urman.Godot;

internal enum PlayerBookAction { None, Mount, ReadBack, Enter }

/// <summary>A read-only presentation of the next earned campaign action.</summary>
internal sealed record PlayerMission(
    string Id,
    string Title,
    string Action,
    string? InteractionId = null,
    string? AnchorPath = null,
    string? TargetZoneId = null,
    string? TargetLabel = null,
    string? BookPhotoId = null,
    PlayerBookAction BookAction = PlayerBookAction.None);
