using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only anchors for the authored full-game interactions.
/// Logical IDs remain owned by compiled content; this table only places the
/// corresponding physical affordance beside the prop that gives it meaning.
/// </summary>
public static class FullGameInteractionLayout
{
    private static readonly IReadOnlyDictionary<string, Vector3> Positions = new Dictionary<string, Vector3>(StringComparer.Ordinal)
    {
        ["urman.fullgame:interaction/act2-house-to-river"] = new(0, 0.92f, -3.75f),
        ["urman.fullgame:interaction/act2-river-to-mosque"] = new(-2.1f, 0.92f, -5.0f),
        ["urman.fullgame:interaction/act2-timur"] = new(0, 1.02f, -6.65f),
        ["urman.fullgame:interaction/act2-mosque-to-council"] = new(0, 0.92f, -4.15f),
        ["urman.fullgame:interaction/act2-alsu"] = new(-1.7f, 1.08f, -5.8f),
        ["urman.fullgame:interaction/act2-council-voice"] = new(1.7f, 1.08f, -5.8f),
        ["urman.fullgame:interaction/act2-council-to-archive"] = new(0, 0.92f, -3.35f),
        ["urman.fullgame:interaction/act3-naila"] = new(-1.2f, 1.1f, -3.4f),
        ["urman.fullgame:interaction/act3-archive-to-soviet"] = new(0, 0.92f, -3.35f),
        ["urman.fullgame:interaction/act3-soviet-document"] = new(-0.65f, 1.12f, -3.65f),
        ["urman.fullgame:interaction/act3-soviet-to-water"] = new(0, 0.92f, -3.55f),
        ["urman.fullgame:interaction/act3-water-voice"] = new(1.8f, 1.0f, -2.1f),
        ["urman.fullgame:interaction/act3-water-to-tukay"] = new(0, 0.92f, -2.9f),
        ["urman.fullgame:interaction/act4-tukay-voice"] = new(-0.55f, 1.08f, -5.6f),
        ["urman.fullgame:interaction/act4-tukay-document"] = new(0.75f, 1.12f, -5.6f),
        ["urman.fullgame:interaction/act4-tukay-to-1552"] = new(0, 0.92f, -3.45f),
        ["urman.fullgame:interaction/act4-1552-document"] = new(-1.25f, 1.16f, -5.75f),
        ["urman.fullgame:interaction/act4-1552-to-pact"] = new(0, 0.92f, -3.55f),
        ["urman.fullgame:interaction/act4-keeper"] = new(2.7f, 1.14f, -6.2f),
        ["urman.fullgame:interaction/act4-pact-document"] = new(0, 1.45f, -5.58f),
        ["urman.fullgame:interaction/act4-pact-to-boundary"] = new(0, 0.92f, -3.45f),
        ["urman.fullgame:interaction/act5-aidar-choice"] = new(0, 1.08f, -7.0f),
        ["urman.fullgame:interaction/act5-boundary-to-epilogue"] = new(0, 0.92f, -5.4f)
    };

    public static Vector3 PositionFor(string zoneId, string interactionId, int fallbackIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(interactionId);
        return Positions.TryGetValue(interactionId, out var position)
            ? position
            : new Vector3((fallbackIndex % 3 - 1) * 1.6f, 0.85f, -3.8f - fallbackIndex * 2.6f);
    }
}
