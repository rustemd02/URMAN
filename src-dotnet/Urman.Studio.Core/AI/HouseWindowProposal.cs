using System.Text.Json.Serialization;

namespace Urman.Studio.Core.AI;

/// <summary>The exact, immutable input the proposal model is allowed to see.</summary>
public sealed record HouseWindowRequest(
    [property: JsonPropertyName("requestId"), JsonPropertyOrder(0)] string RequestId,
    [property: JsonPropertyName("snapshotHash"), JsonPropertyOrder(1)] string SnapshotHash,
    [property: JsonPropertyName("entityId"), JsonPropertyOrder(2)] string EntityId,
    [property: JsonPropertyName("currentWidth"), JsonPropertyOrder(3)] decimal CurrentWidth,
    [property: JsonPropertyName("prompt"), JsonPropertyOrder(4)] string Prompt);

/// <summary>
/// A validated proposal for the single supported operation. Widths are in metres.
/// The operation is static because this type cannot represent any other operation.
/// </summary>
public sealed record HouseWindowProposal(
    [property: JsonPropertyName("requestId"), JsonPropertyOrder(0)] string RequestId,
    [property: JsonPropertyName("snapshotHash"), JsonPropertyOrder(1)] string SnapshotHash,
    [property: JsonPropertyName("entityId"), JsonPropertyOrder(2)] string EntityId,
    [property: JsonPropertyName("expectedWidth"), JsonPropertyOrder(3)] decimal ExpectedWidth,
    [property: JsonPropertyName("windowWidth"), JsonPropertyOrder(4)] decimal WindowWidth,
    [property: JsonPropertyName("summary"), JsonPropertyOrder(5)] string Summary)
{
    public const string Operation = "house.windows.resize";
}

/// <summary>Shared validation for proposals before they can reach Studio's edit/undo path.</summary>
public static class HouseWindowProposalValidator
{
    public const decimal MinimumWidthMeters = 0.65m;
    public const decimal MaximumWidthMeters = 1.35m;

    public static void ValidateRequest(HouseWindowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Guid.TryParseExact(request.RequestId, "N", out var requestId)
            || !string.Equals(requestId.ToString("N"), request.RequestId, StringComparison.Ordinal))
        {
            throw new ArgumentException("RequestId must be a canonical 32-character lowercase hexadecimal GUID.", nameof(request.RequestId));
        }

        ValidateSnapshotHash(request.SnapshotHash);
        ValidateIdentifier(request.EntityId, nameof(request.EntityId), maximumLength: 256);
        ValidateWidth(request.CurrentWidth, nameof(request.CurrentWidth));

        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt.Length > 8_000 || ContainsUnsupportedPromptControl(request.Prompt))
        {
            throw new ArgumentException("The prompt must contain 1–8000 printable characters.", nameof(request));
        }
    }

    public static void Validate(HouseWindowRequest request, HouseWindowProposal proposal)
    {
        ValidateRequest(request);
        ArgumentNullException.ThrowIfNull(proposal);

        if (!string.Equals(proposal.RequestId, request.RequestId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The proposal request ID does not match its input.", nameof(proposal));
        }

        if (!string.Equals(proposal.SnapshotHash, request.SnapshotHash, StringComparison.Ordinal))
        {
            throw new ArgumentException("The proposal snapshot hash does not match its input.", nameof(proposal));
        }

        if (!string.Equals(proposal.EntityId, request.EntityId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The proposal entity ID does not match its input.", nameof(proposal));
        }

        if (proposal.ExpectedWidth != request.CurrentWidth)
        {
            throw new ArgumentException("The expected width must equal the input width.", nameof(proposal));
        }

        ValidateWidth(proposal.WindowWidth, nameof(proposal.WindowWidth));
        if (proposal.WindowWidth == request.CurrentWidth)
        {
            throw new ArgumentException("The proposed width must change the current width.", nameof(proposal));
        }

        if (string.IsNullOrWhiteSpace(proposal.Summary) || proposal.Summary.Length > 500 || ContainsControl(proposal.Summary))
        {
            throw new ArgumentException("The summary must contain 1–500 printable characters.", nameof(proposal));
        }
    }

    private static void ValidateWidth(decimal width, string name)
    {
        if (width < MinimumWidthMeters || width > MaximumWidthMeters)
        {
            throw new ArgumentOutOfRangeException(name, $"Window width must be between {MinimumWidthMeters}m and {MaximumWidthMeters}m.");
        }
    }

    private static void ValidateIdentifier(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || ContainsControl(value))
        {
            throw new ArgumentException($"{name} must contain 1–{maximumLength} printable characters.", name);
        }
    }

    private static void ValidateSnapshotHash(string value)
    {
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("SnapshotHash must be a 64-character SHA-256 hex digest.", nameof(HouseWindowRequest.SnapshotHash));
        }
    }

    private static bool ContainsControl(string value) => value.Any(char.IsControl);

    private static bool ContainsUnsupportedPromptControl(string value) =>
        value.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t'));
}
