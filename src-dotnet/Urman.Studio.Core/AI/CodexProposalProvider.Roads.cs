namespace Urman.Studio.Core.AI;

public sealed partial class CodexProposalProvider
{
    /// <summary>Runs one restricted, structured road-smoothing proposal from a frozen request.</summary>
    public Task<RoadSegmentSmoothProposal> GenerateRoadSegmentSmoothAsync(
        RoadSegmentSmoothRequest request,
        string workingSnapshotDirectory,
        CancellationToken cancellationToken = default,
        IProgress<CodexProposalProgress>? progress = null)
    {
        return RunStructuredAsync(
            RoadSegmentSmoothProtocol.BuildPrompt(request),
            RoadSegmentSmoothProtocol.BuildOutputSchema(request),
            text => RoadSegmentSmoothProtocol.ParseProposal(text, request),
            workingSnapshotDirectory,
            cancellationToken,
            progress,
            "Generating one structured road-smoothing proposal.",
            "Validating the frozen road identity and smoothing bounds.",
            "Codex did not complete a valid road-smoothing proposal.",
            "urman-studio-road-segment-smooth-proposal",
            RoadSegmentSmoothProtocol.MaximumProposalBytes);
    }

    public static string SerializeRoadSegmentSmoothRequest(RoadSegmentSmoothRequest request) =>
        RoadSegmentSmoothProtocol.SerializeRequest(request);

    public static string SerializeRoadSegmentSmoothProposal(
        RoadSegmentSmoothRequest request,
        RoadSegmentSmoothProposal proposal) =>
        RoadSegmentSmoothProtocol.SerializeProposal(request, proposal);

    public static RoadSegmentSmoothProposal ParseRoadSegmentSmoothProposal(
        string json,
        RoadSegmentSmoothRequest request) =>
        RoadSegmentSmoothProtocol.ParseProposal(json, request);

    public static string CreateRoadSegmentSmoothClipboardPrompt(RoadSegmentSmoothRequest request) =>
        RoadSegmentSmoothProtocol.CreateClipboardPrompt(request);
}
