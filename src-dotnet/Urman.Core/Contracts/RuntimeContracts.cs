using System.Text.Json;

namespace Urman.Core.Contracts;

public sealed record GameCommand(
    string ActionOccurrenceId,
    string Type,
    JsonElement Payload);

public sealed record GameEvent(
    long Sequence,
    string TransactionId,
    string Type,
    JsonElement Payload);

public enum CommandStatus
{
    Committed,
    Rejected
}

public sealed record RuntimeError(string Code, string Message, JsonElement? Details = null);

public sealed record CommandDispatchResult(
    CommandStatus Status,
    string? TransactionId,
    JsonElement Value,
    RuntimeError? Error,
    IReadOnlyList<GameEvent> Events);

public sealed record ResourceClaim(
    string ResourceId,
    string OwnerId,
    string LifecycleScope,
    ResourceClaimMode Mode);

public enum ResourceClaimMode
{
    Exclusive,
    Shared
}

public enum StateEffectOperation
{
    Set,
    Delete,
    Increment
}

public sealed record StateEffect(
    StateEffectOperation Operation,
    string Key,
    JsonElement Value = default,
    double Delta = 0);

public sealed record EventDraft(string Type, JsonElement Payload);

public sealed record CommandRejection(string Code, string Message, JsonElement? Details = null);

public sealed record CommandPlan(
    IReadOnlyList<StateEffect>? Effects = null,
    IReadOnlyList<EventDraft>? Events = null,
    IReadOnlyList<ResourceClaim>? Claims = null,
    IReadOnlyList<string>? ReleaseClaimOwnerIds = null,
    IReadOnlyList<string>? ReleaseClaimLifecycleScopes = null,
    CommandRejection? Rejection = null,
    JsonElement Value = default);

public sealed record RuntimeCommandContext(JsonElement State, IReadOnlyList<ResourceClaim> Claims);

public delegate CommandPlan RuntimeCommandHandler(GameCommand command, RuntimeCommandContext context);

public sealed record OccurrenceRecord(
    string ActionOccurrenceId,
    string Fingerprint,
    JsonElement Result);

public sealed record RuntimeSnapshot(
    int SchemaVersion,
    JsonElement State,
    IReadOnlyList<ResourceClaim> Claims,
    IReadOnlyList<OccurrenceRecord> Occurrences,
    long EventSequence);

public interface IRuntimeKernel
{
    ValueTask<CommandDispatchResult> DispatchAsync(GameCommand command, CancellationToken cancellationToken = default);
    RuntimeSnapshot CaptureSnapshot();
}

public interface ICapabilityProvider
{
    string ProtocolId { get; }
    string ExactVersion { get; }
    int StateSchemaVersion { get; }
    bool ValidateConfig(JsonElement config);
    ICapabilitySession CreateSession(string capabilityInstanceId, JsonElement config);
}

public interface ICapabilitySession : IDisposable
{
    void Restore(JsonElement state);
    void Start();
    JsonElement Handle(JsonElement input);
    JsonElement CaptureState();
    void Stop();
}

public sealed record CapabilitySessionSnapshot(
    string CapabilityInstanceId,
    string ProtocolId,
    string ExactVersion,
    int StateSchemaVersion,
    JsonElement State);
