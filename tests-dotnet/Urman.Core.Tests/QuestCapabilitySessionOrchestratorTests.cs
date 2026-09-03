using System.Text.Json;
using Urman.Core.Capabilities;
using Urman.Core.Contracts;
using Xunit;

namespace Urman.Core.Tests;

public sealed class QuestCapabilitySessionOrchestratorTests
{
    private const string ProtocolId = "test:capability/session";
    private const string InstanceId = "capability:instance/quest:stage:objective";

    [Fact]
    public void ReconcileCreatesAndStartsDesiredSessionThenWaitsForClaimReleaseBeforeDisposal()
    {
        var provider = new FakeProvider();
        using var host = Host(provider);
        var orchestrator = new QuestCapabilitySessionOrchestrator(host);

        orchestrator.Reconcile(State(Binding(InstanceId)), []);

        Assert.True(host.Contains(InstanceId));
        Assert.Equal([InstanceId], orchestrator.OwnedSessionIds);
        Assert.Equal(1, host.Handle(InstanceId, JsonSerializer.SerializeToElement(new { })).GetProperty("count").GetInt32());

        var claim = new ResourceClaim("test:resource/console", InstanceId, "capability-session", ResourceClaimMode.Exclusive);
        Assert.Throws<InvalidOperationException>(() => orchestrator.Reconcile(State(), [claim]));
        Assert.True(host.Contains(InstanceId));

        orchestrator.Reconcile(State(), []);

        Assert.False(host.Contains(InstanceId));
        Assert.Empty(orchestrator.OwnedSessionIds);
        Assert.Equal(1, provider.DisposedSessions);
    }

    [Fact]
    public void ReconcileRestoresQuestOwnedSnapshotBeforeStartingSession()
    {
        CapabilitySessionSnapshot snapshot;
        var firstProvider = new FakeProvider();
        using (var firstHost = Host(firstProvider))
        {
            var first = new QuestCapabilitySessionOrchestrator(firstHost);
            first.Reconcile(State(Binding(InstanceId)), []);
            _ = firstHost.Handle(InstanceId, JsonSerializer.SerializeToElement(new { }));
            _ = firstHost.Handle(InstanceId, JsonSerializer.SerializeToElement(new { }));
            snapshot = firstHost.Capture(InstanceId);
        }

        var restoredProvider = new FakeProvider();
        using var restoredHost = Host(restoredProvider);
        var restored = new QuestCapabilitySessionOrchestrator(restoredHost);
        restored.Reconcile(State(Binding(InstanceId)), [], [snapshot]);

        var output = restoredHost.Handle(InstanceId, JsonSerializer.SerializeToElement(new { }));
        Assert.Equal(3, output.GetProperty("count").GetInt32());
    }

    [Fact]
    public void FailedProviderCreationRollsBackSessionsCreatedByCurrentReconciliation()
    {
        var provider = new FakeProvider();
        using var host = Host(provider);
        var orchestrator = new QuestCapabilitySessionOrchestrator(host);
        var firstId = "capability:instance/quest:stage/a";
        var failingId = "capability:instance/quest:stage/b";

        Assert.Throws<InvalidOperationException>(() => orchestrator.Reconcile(
            State(Binding(firstId), Binding(failingId, failCreate: true)),
            []));

        Assert.Empty(host.SessionIds());
        Assert.Empty(orchestrator.OwnedSessionIds);
        Assert.Equal(1, provider.DisposedSessions);
    }

    private static CapabilityHost Host(FakeProvider provider) => new(new CapabilityRegistry([provider]));

    private static object Binding(string instanceId, bool failCreate = false) => new
    {
        capabilityInstanceId = instanceId,
        protocolId = ProtocolId,
        exactVersion = "1.0.0",
        configRef = "test:config/session",
        config = new { failCreate },
        outcomeSchemaRef = "schemas/test-outcome.schema.json"
    };

    private static JsonElement State(params object[] bindings)
    {
        var active = bindings.ToDictionary(
            binding => JsonSerializer.SerializeToElement(binding).GetProperty("capabilityInstanceId").GetString()!,
            binding => binding,
            StringComparer.Ordinal);
        return JsonSerializer.SerializeToElement(new
        {
            quests = new Dictionary<string, object>
            {
                ["test:quest/session"] = new { activeCapabilities = active }
            }
        });
    }

    private sealed class FakeProvider : ICapabilityProvider
    {
        public string ProtocolId => QuestCapabilitySessionOrchestratorTests.ProtocolId;
        public string ExactVersion => "1.0.0";
        public int StateSchemaVersion => 1;
        public int DisposedSessions { get; private set; }

        public bool ValidateConfig(JsonElement config) => config.ValueKind == JsonValueKind.Object;

        public ICapabilitySession CreateSession(string capabilityInstanceId, JsonElement config)
        {
            if (config.GetProperty("failCreate").GetBoolean())
            {
                throw new InvalidOperationException("Requested provider creation failure.");
            }

            return new FakeSession(() => DisposedSessions++);
        }
    }

    private sealed class FakeSession(Action disposed) : ICapabilitySession
    {
        private int _count;
        private bool _started;

        public void Restore(JsonElement state) => _count = state.GetProperty("count").GetInt32();

        public void Start() => _started = true;

        public JsonElement Handle(JsonElement input)
        {
            if (!_started)
            {
                throw new InvalidOperationException("Session is not started.");
            }

            return JsonSerializer.SerializeToElement(new { count = ++_count });
        }

        public JsonElement CaptureState() => JsonSerializer.SerializeToElement(new { count = _count });

        public void Stop() => _started = false;

        public void Dispose() => disposed();
    }
}
