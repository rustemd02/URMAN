using System.Text.Json;
using Urman.Core.Contracts;

namespace Urman.Core.Narrative;

/// <summary>
/// Typed command entry points for the PhotoWorld reducer operations that
/// already have authored state owners. Composite authored interactions keep
/// one kernel transaction through InteractionApply; this class does not own a
/// second state store or physical visit lifecycle.
/// </summary>
public static class PhotoWorldCommandHandler
{
    public const string InteractionApply = "pw1.interaction.apply";
    public const string BookReceive = "pw1.book.receive";
    public const string PhotoAcquire = "pw1.photo.acquire";
    public const string PhotoMount = "pw1.photo.mount";
    public const string PhotoBackRead = "pw1.photo.back-read";
    public const string ContextObserve = "pw1.context.observe";
    public const string OutsideVerify = "pw1.outside.verify";
    public const string PrologueEnter = "pw1.prologue.enter";
    public const string PrologueComplete = "pw1.prologue.complete";
    public const string EpilogueFinish = "pw1.epilogue.finish";

    private static readonly IReadOnlyDictionary<string, string> CommandByOperation =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["photoworlds.book.receive"] = BookReceive,
            ["photoworlds.photo.acquire"] = PhotoAcquire,
            ["photoworlds.photo.mount"] = PhotoMount,
            ["photoworlds.photo.back-read"] = PhotoBackRead,
            ["photoworlds.fact.observe"] = ContextObserve,
            ["photoworlds.photo.context-known"] = ContextObserve,
            ["photoworlds.evidence.verify"] = OutsideVerify,
            ["photoworlds.prologue.enter"] = PrologueEnter,
            ["photoworlds.prologue.complete"] = PrologueComplete,
            ["photoworlds.epilogue.finish"] = EpilogueFinish
        };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> OperationsByCommand =
        CommandByOperation
            .GroupBy(entry => entry.Value, entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

    public static Dictionary<string, RuntimeCommandHandler> Create()
    {
        var handlers = new Dictionary<string, RuntimeCommandHandler>(StringComparer.Ordinal)
        {
            [InteractionApply] = HandleInteractionApply
        };
        foreach (var commandType in OperationsByCommand.Keys)
        {
            var expectedType = commandType;
            handlers.Add(commandType, (command, context) => HandleNamedCommand(expectedType, command, context));
        }

        return handlers;
    }

    public static bool IsCommandType(string commandType) =>
        commandType == InteractionApply || OperationsByCommand.ContainsKey(commandType);

    /// <summary>
    /// Chooses the explicit command name when an authored transaction contains
    /// one reducer intent. Mixed PhotoWorld intents use one typed transaction
    /// envelope so scene, dialogue, quest and PhotoWorld effects stay atomic.
    /// </summary>
    public static string? CommandTypeFor(JsonElement effects)
    {
        if (effects.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var commandTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var effect in effects.EnumerateArray())
        {
            if (effect.ValueKind != JsonValueKind.Object
                || !effect.TryGetProperty("op", out var operationElement)
                || operationElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var operation = operationElement.GetString()!;
            if (!operation.StartsWith("photoworlds.", StringComparison.Ordinal))
            {
                continue;
            }

            if (!PhotoWorldState.IsEffectOperation(operation))
            {
                return InteractionApply;
            }

            commandTypes.Add(CommandByOperation[operation]);
        }

        return commandTypes.Count switch
        {
            0 => null,
            1 => commandTypes.Single(),
            _ => InteractionApply
        };
    }

    private static CommandPlan HandleInteractionApply(GameCommand command, RuntimeCommandContext context) =>
        HandleEnvelope(command.Type, command, context);

    private static CommandPlan HandleNamedCommand(
        string expectedCommandType,
        GameCommand command,
        RuntimeCommandContext context)
    {
        return HandleEnvelope(expectedCommandType, command, context);
    }

    private static CommandPlan HandleEnvelope(
        string commandType,
        GameCommand command,
        RuntimeCommandContext context)
    {
        var payload = command.Payload;
        if (!IdentityMatches(payload, context.State, out var rejection))
        {
            return new(Rejection: rejection);
        }

        if (!payload.TryGetProperty("conditions", out var conditions) || conditions.ValueKind != JsonValueKind.Array
            || !payload.TryGetProperty("effects", out var effects) || effects.ValueKind != JsonValueKind.Array)
        {
            return Reject("InvalidPhotoWorldCommand", "A PhotoWorld command requires its authored conditions and effects arrays.");
        }

        var operationTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var effect in effects.EnumerateArray())
        {
            if (effect.ValueKind != JsonValueKind.Object
                || !effect.TryGetProperty("op", out var operationElement)
                || operationElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var operation = operationElement.GetString()!;
            if (!operation.StartsWith("photoworlds.", StringComparison.Ordinal))
            {
                continue;
            }
            if (!PhotoWorldState.IsEffectOperation(operation))
            {
                return Reject("UnsupportedPhotoWorldOperation", $"PhotoWorld operation {operation} has no current reducer owner.");
            }

            operationTypes.Add(CommandByOperation[operation]);
        }

        if (operationTypes.Count == 0)
        {
            return Reject("PhotoWorldOperationMissing", "A PhotoWorld command must contain a reducer-backed PhotoWorld operation.");
        }
        if (commandType != InteractionApply
            && (operationTypes.Count != 1 || !operationTypes.Contains(commandType)))
        {
            return Reject("PhotoWorldCommandMismatch", $"Command {commandType} does not match its single authored PhotoWorld intent.");
        }

        try
        {
            return NarrativeCommandHandlers.HandleContentApply(
                command with { Type = NarrativeCommandHandlers.ContentApply }, context);
        }
        catch (InvalidOperationException exception)
        {
            return Reject("PhotoWorldActionRejected", exception.Message);
        }
    }

    private static bool IdentityMatches(JsonElement payload, JsonElement state, out CommandRejection rejection)
    {
        rejection = new("PhotoWorldCampaignMismatch", "The PhotoWorld command does not match the active campaign namespace and version.");
        if (!state.TryGetProperty("photoworlds", out var world)
            || world.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("namespaceId", out var namespaceId)
            || namespaceId.ValueKind != JsonValueKind.String
            || namespaceId.GetString() != PhotoWorldState.NamespaceId
            || !payload.TryGetProperty("campaignId", out var campaignId)
            || campaignId.ValueKind != JsonValueKind.String
            || !payload.TryGetProperty("campaignExactVersion", out var campaignVersion)
            || campaignVersion.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var campaign = world.GetProperty("campaign");
        if (world.GetProperty("namespaceId").GetString() != namespaceId.GetString()
            || campaign.GetProperty("id").GetString() != campaignId.GetString()
            || campaign.GetProperty("exactVersion").GetString() != campaignVersion.GetString())
        {
            return false;
        }

        rejection = null!;
        return true;
    }

    private static CommandPlan Reject(string code, string message) => new(Rejection: new(code, message));
}
