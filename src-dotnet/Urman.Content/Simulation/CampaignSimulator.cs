using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;
using Urman.Core.Runtime;
using Urman.Core.Serialization;

namespace Urman.Content.Simulation;

public sealed record CampaignSimulationReport(
    string CampaignId,
    int Steps,
    long EventSequence,
    string StateSha256);

public sealed class CampaignSimulator
{
    public async Task<CampaignSimulationReport> SimulateNarrativeOrderAsync(
        JsonObject compiledPack,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compiledPack);
        var campaign = compiledPack["campaign"]?.AsObject()
            ?? throw new ArgumentException("Compiled pack campaign is missing.", nameof(compiledPack));
        var campaignId = campaign["id"]!.GetValue<string>();
        var narrativeOrder = campaign["narrativeOrder"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .ToArray();

        using var kernel = new RuntimeKernel(
            JsonSerializer.SerializeToElement(new { campaignId, visited = Array.Empty<string>() }),
            new Dictionary<string, RuntimeCommandHandler>
            {
                ["simulation.record"] = RecordNarrativeEntry
            });
        for (var index = 0; index < narrativeOrder.Length; index++)
        {
            var targetId = narrativeOrder[index];
            var command = new GameCommand(
                $"simulation:{index:D4}:{targetId}",
                "simulation.record",
                JsonSerializer.SerializeToElement(new { targetId }));
            var result = await kernel.DispatchAsync(command, cancellationToken);
            if (result.Status != CommandStatus.Committed)
            {
                throw new InvalidOperationException($"Simulation rejected {targetId}: {result.Error?.Message}");
            }
        }

        var snapshot = kernel.CaptureSnapshot();
        var stateNode = JsonNode.Parse(snapshot.State.GetRawText())!;
        return new(campaignId, narrativeOrder.Length, snapshot.EventSequence, CanonicalJson.Sha256(stateNode));
    }

    private static CommandPlan RecordNarrativeEntry(GameCommand command, RuntimeCommandContext context)
    {
        var targetId = command.Payload.GetProperty("targetId").GetString()
            ?? throw new InvalidOperationException("Simulation target ID is missing.");
        var visited = context.State.GetProperty("visited").EnumerateArray()
            .Select(item => item.GetString()!)
            .Append(targetId)
            .ToArray();
        return new(
            Effects: [new(StateEffectOperation.Set, "visited", JsonSerializer.SerializeToElement(visited))],
            Events: [new("simulation.narrative.visited", JsonSerializer.SerializeToElement(new { targetId }))]);
    }
}
