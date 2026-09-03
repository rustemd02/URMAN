using System.Text.Json;
using Urman.Core.Contracts;

namespace Urman.Core.Capabilities;

public sealed record QuestCapabilityBinding(
    string CapabilityInstanceId,
    string ProtocolId,
    string ExactVersion,
    JsonElement Config);

public sealed class QuestCapabilitySessionOrchestrator
{
    private readonly CapabilityHost _host;
    private readonly HashSet<string> _ownedSessionIds = new(StringComparer.Ordinal);

    public QuestCapabilitySessionOrchestrator(CapabilityHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public IReadOnlyList<string> OwnedSessionIds => _ownedSessionIds.Order(StringComparer.Ordinal).ToArray();

    public void Reconcile(
        JsonElement runtimeState,
        IReadOnlyList<ResourceClaim> activeClaims,
        IReadOnlyList<CapabilitySessionSnapshot>? snapshots = null)
    {
        if (runtimeState.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Runtime state must be an object.", nameof(runtimeState));
        }

        ArgumentNullException.ThrowIfNull(activeClaims);
        var desired = ReadBindings(runtimeState);
        var snapshotById = SnapshotIndex(snapshots ?? []);
        var created = new List<string>();
        try
        {
            foreach (var binding in desired.Values.OrderBy(value => value.CapabilityInstanceId, StringComparer.Ordinal))
            {
                if (_host.Contains(binding.CapabilityInstanceId))
                {
                    continue;
                }

                snapshotById.TryGetValue(binding.CapabilityInstanceId, out var snapshot);
                _host.Create(
                    binding.CapabilityInstanceId,
                    binding.ProtocolId,
                    binding.ExactVersion,
                    binding.Config,
                    snapshot);
                try
                {
                    _host.Start(binding.CapabilityInstanceId);
                    created.Add(binding.CapabilityInstanceId);
                }
                catch
                {
                    _host.DisposeSession(binding.CapabilityInstanceId);
                    throw;
                }
            }
        }
        catch
        {
            foreach (var instanceId in created.AsEnumerable().Reverse())
            {
                _host.DisposeSession(instanceId);
            }

            throw;
        }

        var obsolete = _ownedSessionIds
            .Where(instanceId => !desired.ContainsKey(instanceId))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var claimedObsolete = obsolete
            .Where(instanceId => activeClaims.Any(claim => StringComparer.Ordinal.Equals(claim.OwnerId, instanceId)))
            .ToArray();
        if (claimedObsolete.Length > 0)
        {
            foreach (var instanceId in created.AsEnumerable().Reverse())
            {
                _host.DisposeSession(instanceId);
            }

            throw new InvalidOperationException(
                $"Quest capability claims must be released before teardown: {string.Join(", ", claimedObsolete)}.");
        }

        foreach (var instanceId in obsolete)
        {
            if (_host.Contains(instanceId))
            {
                _host.DisposeSession(instanceId);
            }
        }

        _ownedSessionIds.Clear();
        _ownedSessionIds.UnionWith(desired.Keys);
    }

    private static IReadOnlyDictionary<string, QuestCapabilityBinding> ReadBindings(JsonElement runtimeState)
    {
        if (!runtimeState.TryGetProperty("quests", out var quests) || quests.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Runtime state quests must be an object.");
        }

        var result = new Dictionary<string, QuestCapabilityBinding>(StringComparer.Ordinal);
        foreach (var quest in quests.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            if (quest.Value.ValueKind != JsonValueKind.Object
                || !quest.Value.TryGetProperty("activeCapabilities", out var active)
                || active.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"Quest {quest.Name} activeCapabilities must be an object.");
            }

            foreach (var capability in active.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                var value = capability.Value;
                if (value.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException($"Quest capability {capability.Name} must be an object.");
                }

                var instanceId = RequiredString(value, "capabilityInstanceId", capability.Name);
                if (!StringComparer.Ordinal.Equals(instanceId, capability.Name))
                {
                    throw new InvalidDataException($"Quest capability key {capability.Name} does not match its instance ID.");
                }

                if (!value.TryGetProperty("config", out var config) || config.ValueKind == JsonValueKind.Undefined)
                {
                    throw new InvalidDataException($"Quest capability {instanceId} config is missing.");
                }

                var binding = new QuestCapabilityBinding(
                    instanceId,
                    RequiredString(value, "protocolId", instanceId),
                    RequiredString(value, "exactVersion", instanceId),
                    config.Clone());
                if (!result.TryAdd(instanceId, binding))
                {
                    throw new InvalidDataException($"Quest capability {instanceId} is owned by multiple quest instances.");
                }
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, CapabilitySessionSnapshot> SnapshotIndex(
        IReadOnlyList<CapabilitySessionSnapshot> snapshots)
    {
        var result = new Dictionary<string, CapabilitySessionSnapshot>(StringComparer.Ordinal);
        foreach (var snapshot in snapshots)
        {
            if (!result.TryAdd(snapshot.CapabilityInstanceId, snapshot))
            {
                throw new InvalidDataException($"Duplicate capability snapshot {snapshot.CapabilityInstanceId}.");
            }
        }

        return result;
    }

    private static string RequiredString(JsonElement owner, string property, string label)
    {
        if (owner.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new InvalidDataException($"Quest capability {label} {property} must be a non-empty string.");
    }
}
