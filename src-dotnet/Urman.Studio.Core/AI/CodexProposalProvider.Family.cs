namespace Urman.Studio.Core.AI;

public sealed partial class CodexProposalProvider
{
    /// <summary>Runs the shared restricted app-server lifecycle for a frozen A/B/C family proposal.</summary>
    public Task<HouseFamilyWindowProposal> GenerateFamilyAsync(
        HouseFamilyWindowRequest request,
        string workingSnapshotDirectory,
        CancellationToken cancellationToken = default,
        IProgress<CodexProposalProgress>? progress = null)
    {
        return RunStructuredAsync(
            HouseFamilyWindowProtocol.BuildPrompt(request),
            HouseFamilyWindowProtocol.BuildOutputSchema(request),
            text => HouseFamilyWindowProtocol.ParseProposal(text, request),
            workingSnapshotDirectory,
            cancellationToken,
            progress,
            "Generating one structured family-window proposal.",
            "Validating the frozen target set and common width.",
            "Codex did not complete a valid family-window proposal.",
            "urman-studio-house-family-window-proposal",
            HouseFamilyWindowProtocol.MaximumProposalBytes);
    }

    /// <summary>Serializes the canonical frozen family request for disclosure or manual handoff.</summary>
    public static string SerializeRequest(HouseFamilyWindowRequest request) =>
        HouseFamilyWindowProtocol.SerializeRequest(request);

    /// <summary>Serializes a family proposal only after it matches the frozen request.</summary>
    public static string SerializeProposal(HouseFamilyWindowRequest request, HouseFamilyWindowProposal proposal) =>
        HouseFamilyWindowProtocol.SerializeProposal(request, proposal);

    /// <summary>Parses a manual proposal through the same request-bound validator used by direct generation.</summary>
    public static HouseFamilyWindowProposal ParseProposal(string json, HouseFamilyWindowRequest request) =>
        HouseFamilyWindowProtocol.ParseProposal(json, request);

    /// <summary>Creates the exact bounded prompt and schema used by direct family proposal generation.</summary>
    public static string CreateClipboardPrompt(HouseFamilyWindowRequest request) =>
        HouseFamilyWindowProtocol.CreateClipboardPrompt(request);
}
