using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;
using Urman.Core.Serialization;

namespace Urman.Core.Runtime;

public sealed class RuntimeKernel : IRuntimeKernel, IDisposable
{
    public const int SnapshotSchemaVersion = 1;

    private readonly IReadOnlyDictionary<string, RuntimeCommandHandler> _handlers;
    private readonly SemaphoreSlim _dispatchLock = new(1, 1);
    private readonly Dictionary<string, LedgerEntry> _ledger = new(StringComparer.Ordinal);
    private JsonObject _state;
    private JsonElement? _selectedState;
    private List<ResourceClaim> _claims = [];
    private long _eventSequence;
    private bool _disposed;

    public RuntimeKernel(JsonElement initialState, IReadOnlyDictionary<string, RuntimeCommandHandler> handlers)
    {
        if (initialState.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Runtime initial state must be a JSON object.", nameof(initialState));
        }

        ArgumentNullException.ThrowIfNull(handlers);
        _state = JsonNode.Parse(initialState.GetRawText())!.AsObject();
        _handlers = new Dictionary<string, RuntimeCommandHandler>(handlers, StringComparer.Ordinal);
        if (_handlers.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null))
        {
            throw new ArgumentException("Runtime handlers require non-empty command types and handlers.", nameof(handlers));
        }
    }

    public static RuntimeKernel Restore(RuntimeSnapshot snapshot, IReadOnlyDictionary<string, RuntimeCommandHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != SnapshotSchemaVersion || snapshot.EventSequence < 0 || snapshot.State.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Runtime snapshot is incompatible or malformed.", nameof(snapshot));
        }

        var kernel = new RuntimeKernel(snapshot.State, handlers)
        {
            _eventSequence = snapshot.EventSequence,
            _claims = snapshot.Claims.Select(ValidateClaim).ToList()
        };
        ValidateClaimSet(kernel._claims);

        foreach (var occurrence in snapshot.Occurrences)
        {
            ValidateKey(occurrence.ActionOccurrenceId, "Occurrence ID");
            ValidateKey(occurrence.Fingerprint, "Occurrence fingerprint");
            if (!kernel._ledger.TryAdd(
                    occurrence.ActionOccurrenceId,
                    new(occurrence.Fingerprint, occurrence.Result.Clone())))
            {
                throw new ArgumentException($"Runtime snapshot has duplicate occurrence {occurrence.ActionOccurrenceId}.", nameof(snapshot));
            }
        }

        return kernel;
    }

    public async ValueTask<CommandDispatchResult> DispatchAsync(GameCommand command, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _dispatchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return DispatchSerial(command);
        }
        finally
        {
            _dispatchLock.Release();
        }
    }

    public RuntimeSnapshot CaptureSnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _dispatchLock.Wait();
        try
        {
            return new(
                SnapshotSchemaVersion,
                ToElement(_state),
                _claims.OrderBy(claim => claim.ResourceId, StringComparer.Ordinal)
                    .ThenBy(claim => claim.OwnerId, StringComparer.Ordinal)
                    .ToArray(),
                _ledger.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => new OccurrenceRecord(entry.Key, entry.Value.Fingerprint, entry.Value.Result.Clone()))
                    .ToArray(),
                _eventSequence);
        }
        finally
        {
            _dispatchLock.Release();
        }
    }

    public JsonElement SelectState()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _dispatchLock.Wait();
        try
        {
            // Immutable reads share one snapshot until a transaction commits.
            return _selectedState ??= ToElement(_state);
        }
        finally
        {
            _dispatchLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispatchLock.Dispose();
    }

    private CommandDispatchResult DispatchSerial(GameCommand command)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.ActionOccurrenceId) || string.IsNullOrWhiteSpace(command.Type))
        {
            return Reject("PreflightError", "Command type and actionOccurrenceId are required.");
        }

        var fingerprintNode = new JsonObject
        {
            ["type"] = command.Type,
            ["payload"] = JsonNode.Parse(command.Payload.GetRawText())
        };
        var fingerprint = CanonicalJson.Serialize(fingerprintNode);
        if (_ledger.TryGetValue(command.ActionOccurrenceId, out var prior))
        {
            return StringComparer.Ordinal.Equals(prior.Fingerprint, fingerprint)
                ? Reject("DuplicateOccurrence", $"Occurrence {command.ActionOccurrenceId} was already processed.")
                : Reject("OccurrenceConflict", $"Occurrence {command.ActionOccurrenceId} has a different command fingerprint.");
        }

        if (!_handlers.TryGetValue(command.Type, out var handler))
        {
            return Record(command.ActionOccurrenceId, fingerprint, Reject("UnknownCommand", $"Unknown command {command.Type}."));
        }

        CommandPlan plan;
        try
        {
            plan = handler(command with { Payload = command.Payload.Clone() }, new(ToElement(_state), SnapshotClaims())) ?? new();
        }
        catch (Exception exception)
        {
            return Record(command.ActionOccurrenceId, fingerprint, Reject("PreflightError", exception.Message));
        }

        if (plan.Rejection is not null)
        {
            if (string.IsNullOrWhiteSpace(plan.Rejection.Code) || string.IsNullOrWhiteSpace(plan.Rejection.Message))
            {
                return Record(command.ActionOccurrenceId, fingerprint, Reject("PreflightError", "Command rejection is invalid."));
            }

            return Record(command.ActionOccurrenceId, fingerprint, Reject(
                plan.Rejection.Code,
                plan.Rejection.Message,
                plan.Rejection.Details));
        }

        JsonObject draftState;
        List<ResourceClaim> draftClaims;
        try
        {
            draftClaims = ApplyClaims(
                _claims,
                plan.ReleaseClaimOwnerIds ?? [],
                plan.ReleaseClaimLifecycleScopes ?? [],
                plan.Claims ?? []);
            draftState = (JsonObject)_state.DeepClone();
            ApplyEffects(draftState, plan.Effects ?? []);
        }
        catch (ResourceConflictException exception)
        {
            return Record(command.ActionOccurrenceId, fingerprint, Reject("ResourceConflict", exception.Message));
        }
        catch (Exception exception)
        {
            return Record(command.ActionOccurrenceId, fingerprint, Reject("PreflightError", exception.Message));
        }

        var drafts = plan.Events ?? [];
        if (drafts.Count > long.MaxValue - _eventSequence)
        {
            return Record(command.ActionOccurrenceId, fingerprint, Reject("PreflightError", "Event sequence would overflow."));
        }

        var transactionId = $"tx:{command.ActionOccurrenceId}";
        var events = new List<GameEvent>(drafts.Count);
        var nextSequence = _eventSequence;
        try
        {
            foreach (var draft in drafts)
            {
                ValidateKey(draft.Type, "Event type");
                if (draft.Payload.ValueKind == JsonValueKind.Undefined)
                {
                    throw new ArgumentException("Event payload is required.");
                }

                events.Add(new(++nextSequence, transactionId, draft.Type, draft.Payload.Clone()));
            }
        }
        catch (Exception exception)
        {
            return Record(command.ActionOccurrenceId, fingerprint, Reject("PreflightError", exception.Message));
        }

        _state = draftState;
        _selectedState = null;
        _claims = draftClaims;
        _eventSequence = nextSequence;
        var value = plan.Value.ValueKind == JsonValueKind.Undefined ? JsonSerializer.SerializeToElement<object?>(null) : plan.Value.Clone();
        return Record(command.ActionOccurrenceId, fingerprint, new(CommandStatus.Committed, transactionId, value, null, events));
    }

    private CommandDispatchResult Record(string occurrenceId, string fingerprint, CommandDispatchResult result)
    {
        _ledger.Add(occurrenceId, new(fingerprint, JsonSerializer.SerializeToElement(result)));
        return result;
    }

    private static CommandDispatchResult Reject(string code, string message, JsonElement? details = null) => new(
        CommandStatus.Rejected,
        null,
        JsonSerializer.SerializeToElement<object?>(null),
        new(code, message, details?.Clone()),
        []);

    private static void ApplyEffects(JsonObject state, IReadOnlyList<StateEffect> effects)
    {
        foreach (var effect in effects)
        {
            ValidateKey(effect.Key, "State effect key");
            switch (effect.Operation)
            {
                case StateEffectOperation.Set:
                    if (effect.Value.ValueKind == JsonValueKind.Undefined)
                    {
                        throw new ArgumentException("State set effect requires a value.");
                    }

                    state[effect.Key] = JsonNode.Parse(effect.Value.GetRawText());
                    break;
                case StateEffectOperation.Delete:
                    state.Remove(effect.Key);
                    break;
                case StateEffectOperation.Increment:
                    if (!double.IsFinite(effect.Delta))
                    {
                        throw new ArgumentException("State increment delta must be finite.");
                    }

                    var current = state[effect.Key] is null ? 0d : state[effect.Key]!.GetValue<double>();
                    var next = current + effect.Delta;
                    if (!double.IsFinite(next))
                    {
                        throw new ArgumentException("State increment result must be finite.");
                    }

                    state[effect.Key] = next;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), "Unknown state effect operation.");
            }
        }
    }

    private static List<ResourceClaim> ApplyClaims(
        IReadOnlyList<ResourceClaim> current,
        IReadOnlyList<string> releaseOwnerIds,
        IReadOnlyList<string> releaseLifecycleScopes,
        IReadOnlyList<ResourceClaim> requested)
    {
        var ownerReleases = releaseOwnerIds.ToHashSet(StringComparer.Ordinal);
        var scopeReleases = releaseLifecycleScopes.ToHashSet(StringComparer.Ordinal);
        var next = current
            .Where(claim => !ownerReleases.Contains(claim.OwnerId) && !scopeReleases.Contains(claim.LifecycleScope))
            .ToList();

        foreach (var rawClaim in requested)
        {
            var claim = ValidateClaim(rawClaim);
            var existing = next.Where(item => item.ResourceId == claim.ResourceId && item.OwnerId != claim.OwnerId).ToArray();
            if (existing.Length > 0 && (claim.Mode == ResourceClaimMode.Exclusive || existing.Any(item => item.Mode == ResourceClaimMode.Exclusive)))
            {
                throw new ResourceConflictException($"Resource {claim.ResourceId} is already claimed.");
            }

            next.RemoveAll(item => item.ResourceId == claim.ResourceId && item.OwnerId == claim.OwnerId);
            next.Add(claim);
        }

        ValidateClaimSet(next);
        return next;
    }

    private static ResourceClaim ValidateClaim(ResourceClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ValidateKey(claim.ResourceId, "Resource ID");
        ValidateKey(claim.OwnerId, "Resource owner ID");
        ValidateKey(claim.LifecycleScope, "Resource lifecycle scope");
        return claim;
    }

    private static void ValidateClaimSet(IReadOnlyCollection<ResourceClaim> claims)
    {
        foreach (var group in claims.GroupBy(claim => claim.ResourceId, StringComparer.Ordinal))
        {
            if (group.GroupBy(claim => claim.OwnerId, StringComparer.Ordinal).Any(owner => owner.Count() > 1))
            {
                throw new ArgumentException($"Duplicate resource claim owner for {group.Key}.");
            }

            if (group.Count() > 1 && group.Any(claim => claim.Mode == ResourceClaimMode.Exclusive))
            {
                throw new ArgumentException($"Conflicting resource claims for {group.Key}.");
            }
        }
    }

    private IReadOnlyList<ResourceClaim> SnapshotClaims() => _claims
        .OrderBy(claim => claim.ResourceId, StringComparer.Ordinal)
        .ThenBy(claim => claim.OwnerId, StringComparer.Ordinal)
        .ToArray();

    private static JsonElement ToElement(JsonNode node) => JsonSerializer.SerializeToElement(node);

    private static void ValidateKey(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "__proto__" or "constructor" or "prototype")
        {
            throw new ArgumentException($"{label} is invalid.");
        }
    }

    private sealed record LedgerEntry(string Fingerprint, JsonElement Result);

    private sealed class ResourceConflictException(string message) : Exception(message);
}
