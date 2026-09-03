using Urman.Core.Contracts;

namespace Urman.Core.Capabilities;

public sealed class CapabilityRegistry
{
    private readonly IReadOnlyDictionary<string, ICapabilityProvider> _providers;

    public CapabilityRegistry(IEnumerable<ICapabilityProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var catalog = new Dictionary<string, ICapabilityProvider>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ValidateIdentity(provider.ProtocolId, provider.ExactVersion, provider.StateSchemaVersion);
            if (!catalog.TryAdd(provider.ProtocolId, provider))
            {
                throw new ArgumentException($"Duplicate capability provider {provider.ProtocolId}.", nameof(providers));
            }
        }

        _providers = catalog;
    }

    public ICapabilityProvider Require(string protocolId, string exactVersion)
    {
        if (!_providers.TryGetValue(protocolId, out var provider))
        {
            throw new KeyNotFoundException($"Capability provider {protocolId} is missing.");
        }

        if (!StringComparer.Ordinal.Equals(provider.ExactVersion, exactVersion))
        {
            throw new InvalidOperationException($"Capability {protocolId} requires {exactVersion}, registered {provider.ExactVersion}.");
        }

        return provider;
    }

    private static void ValidateIdentity(string protocolId, string exactVersion, int stateSchemaVersion)
    {
        _ = new ContentId(protocolId);
        var versionParts = exactVersion.Split('.');
        if (versionParts.Length != 3 || versionParts.Any(part => !int.TryParse(part, out _)) || stateSchemaVersion < 1)
        {
            throw new ArgumentException("Capability provider version metadata is invalid.");
        }
    }
}
