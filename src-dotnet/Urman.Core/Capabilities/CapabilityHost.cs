using System.Text.Json;
using Urman.Core.Contracts;

namespace Urman.Core.Capabilities;

public sealed class CapabilityHost : IDisposable
{
    private readonly CapabilityRegistry _registry;
    private readonly Dictionary<string, SessionRecord> _sessions = new(StringComparer.Ordinal);
    private bool _disposed;

    public CapabilityHost(CapabilityRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public void Create(
        string capabilityInstanceId,
        string protocolId,
        string exactVersion,
        JsonElement config,
        CapabilitySessionSnapshot? snapshot = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(capabilityInstanceId);
        if (_sessions.ContainsKey(capabilityInstanceId))
        {
            throw new InvalidOperationException($"Capability instance {capabilityInstanceId} already exists.");
        }

        var provider = _registry.Require(protocolId, exactVersion);
        var configClone = config.Clone();
        if (!provider.ValidateConfig(configClone))
        {
            throw new ArgumentException($"Capability {capabilityInstanceId} config is invalid.", nameof(config));
        }

        var session = provider.CreateSession(capabilityInstanceId, configClone)
            ?? throw new InvalidOperationException($"Capability provider {protocolId} returned no session.");
        var record = new SessionRecord(capabilityInstanceId, provider, session);
        try
        {
            if (snapshot is not null)
            {
                ValidateSnapshot(snapshot, record);
                session.Restore(snapshot.State.Clone());
            }

            _sessions.Add(capabilityInstanceId, record);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public void Start(string capabilityInstanceId)
    {
        var record = Require(capabilityInstanceId);
        if (record.Started || record.Stopped)
        {
            throw new InvalidOperationException($"Capability {capabilityInstanceId} cannot be started in its current state.");
        }

        record.Session.Start();
        record.Started = true;
    }

    public JsonElement Handle(string capabilityInstanceId, JsonElement input)
    {
        var record = Require(capabilityInstanceId);
        if (!record.Started || record.Stopped)
        {
            throw new InvalidOperationException($"Capability {capabilityInstanceId} is not active.");
        }

        return record.Session.Handle(input.Clone()).Clone();
    }

    public CapabilitySessionSnapshot Capture(string capabilityInstanceId)
    {
        var record = Require(capabilityInstanceId);
        if (record.Stopped)
        {
            throw new InvalidOperationException($"Capability {capabilityInstanceId} is stopped.");
        }

        return new(
            record.InstanceId,
            record.Provider.ProtocolId,
            record.Provider.ExactVersion,
            record.Provider.StateSchemaVersion,
            record.Session.CaptureState().Clone());
    }

    public IReadOnlyList<CapabilitySessionSnapshot> CaptureAll() => _sessions.Values
        .Where(record => !record.Stopped)
        .OrderBy(record => record.InstanceId, StringComparer.Ordinal)
        .Select(record => Capture(record.InstanceId))
        .ToArray();

    public bool Contains(string capabilityInstanceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(capabilityInstanceId);
        return _sessions.ContainsKey(capabilityInstanceId);
    }

    public IReadOnlyList<string> SessionIds()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _sessions.Keys.Order(StringComparer.Ordinal).ToArray();
    }

    public void Stop(string capabilityInstanceId)
    {
        var record = Require(capabilityInstanceId);
        if (record.Stopped)
        {
            return;
        }

        if (record.Started)
        {
            record.Session.Stop();
        }

        record.Stopped = true;
    }

    public void DisposeSession(string capabilityInstanceId)
    {
        var record = Require(capabilityInstanceId);
        Stop(capabilityInstanceId);
        record.Session.Dispose();
        _sessions.Remove(capabilityInstanceId);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var instanceId in _sessions.Keys.ToArray())
        {
            DisposeSession(instanceId);
        }

        _disposed = true;
    }

    private SessionRecord Require(string capabilityInstanceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(capabilityInstanceId);
        return _sessions.TryGetValue(capabilityInstanceId, out var record)
            ? record
            : throw new KeyNotFoundException($"Unknown capability instance {capabilityInstanceId}.");
    }

    private static void ValidateSnapshot(CapabilitySessionSnapshot snapshot, SessionRecord record)
    {
        if (!StringComparer.Ordinal.Equals(snapshot.CapabilityInstanceId, record.InstanceId) ||
            !StringComparer.Ordinal.Equals(snapshot.ProtocolId, record.Provider.ProtocolId) ||
            !StringComparer.Ordinal.Equals(snapshot.ExactVersion, record.Provider.ExactVersion) ||
            snapshot.StateSchemaVersion != record.Provider.StateSchemaVersion)
        {
            throw new InvalidDataException($"Capability snapshot {snapshot.CapabilityInstanceId} is incompatible.");
        }
    }

    private sealed class SessionRecord(string instanceId, ICapabilityProvider provider, ICapabilitySession session)
    {
        public string InstanceId { get; } = instanceId;
        public ICapabilityProvider Provider { get; } = provider;
        public ICapabilitySession Session { get; } = session;
        public bool Started { get; set; }
        public bool Stopped { get; set; }
    }
}
