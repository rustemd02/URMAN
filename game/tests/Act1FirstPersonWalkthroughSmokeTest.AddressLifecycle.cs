using System;
using System.Linq;
using Godot;

namespace Urman.Godot.Tests;

public partial class Act1FirstPersonWalkthroughSmokeTest
{
    private bool _addressLifecycleOnly;
    private bool _addressNaturalQueueOnly;
    private AddressAccessVerifier? _addressLifecycleAudit;
    private string _addressLifecycleAccess = "";
    private string _addressLifecycleInitialState = "";
    private long _addressLifecycleSearchSteps;
    private long _addressLifecycleCommits;
    private long _addressLifecycleInterrupted;
    private long _addressLifecycleEpoch;
    private string _addressNaturalIndoorCurrent = "";
    private long _addressNaturalIndoorAttachments;
    private long _addressNaturalIndoorCommitFrame;
    private long _addressNaturalIndoorAttachFrame;

    private bool PrepareAddressLifecycle(Act1ConnectedWorld world, FirstPersonController player)
    {
        var registry = world.AddressRegistry;
        if (registry is null || !registry.TryResolve("ADR-MOSQUE", out var mosque))
        { Fail("The ordinary house approach has no imported mosque address."); return false; }
        _addressLifecycleAudit = world.GetNode<AddressAccessVerifier>("AddressAccessVerification");
        _addressLifecycleAccess = mosque.AccessId;
        _addressLifecycleInitialState = registry.AccessPoints[mosque.AccessId].State;
        if (_addressNaturalQueueOnly)
        {
            var importedFirst = registry.AccessPoints.Keys.Take(3).ToArray();
            if (!importedFirst.SequenceEqual(new[] { "ACC-ADR-BABAI", "ACC-ADR-FAP", "ACC-ADR-MOSQUE" }))
            { Fail("Ordinary address import lost its authored first three slots: " + string.Join(",", importedFirst)); return false; }
            _addressLifecycleEpoch = AuditCount("lifecycleEpoch");
            PrintNaturalAddressQueue("before-ordinary-house-door", world, player);
            return true;
        }
        var revision = player.PresentationTransformRevision;
        var feet = player.GlobalPosition;
        var commits = AuditCount("lifecycleCommittedJobs");
        var steps = AuditCount("lifecycleSearchSteps");
        // Select scheduling only in this explicit smoke scope. The production
        // search must perform a real first support query and retain its partial
        // job while the ordinary camera interaction enters the house.
        _addressLifecycleAudit.SetPhysicsProcess(false);
        _addressLifecycleAudit.StartLifecycleDiagnostic(_addressLifecycleAccess);
        if (AuditCount("lifecycleSearchSteps") != steps + 1
            || AuditCount("lifecycleCommittedJobs") != commits
            || _addressLifecycleAudit.GetMeta("lifecycleCurrentAccess", "").AsString() != _addressLifecycleAccess
            || player.PresentationTransformRevision != revision || player.GlobalPosition != feet)
        { Fail("Selecting the mosque audit did not retain exactly one real, unfinished exterior query."); return false; }
        _addressLifecycleSearchSteps = AuditCount("lifecycleSearchSteps");
        _addressLifecycleCommits = AuditCount("lifecycleCommittedJobs");
        _addressLifecycleInterrupted = AuditCount("lifecycleInterruptedJobs");
        _addressLifecycleEpoch = AuditCount("lifecycleEpoch");
        PrintAddressLifecycle("before-ordinary-house-door", world, player);
        return true;
    }

    private async Task<bool> VerifyAddressLifecycleIndoors(Act1ConnectedWorld world,
        RuntimeBridge bridge, FirstPersonController player)
    {
        await PhysicsFrames(4);
        if (!AddressAuditStayedSuspended(world, bridge, player, "after-household-actions")) return false;
        var knowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var scene = bridge.ActiveSceneId;
        var feet = player.GlobalPosition;
        if (!await bridge.SaveSlotAsync("walk-address-indoor") || !await bridge.LoadSlotAsync("walk-address-indoor"))
        { Fail("The actually reached household state could not save and load indoors."); return false; }
        await PhysicsFrames(12);
        if (!AddressAuditStayedSuspended(world, bridge, player, "after-ordinary-indoor-load")) return false;
        if (player.GlobalPosition.DistanceTo(feet) > .08f || bridge.ActiveSceneId != scene
            || bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() != knowledge
            || player.ModalOpen || !player.CanStandAt(player.GlobalPosition))
        { Fail("Indoor lifecycle roundtrip changed the reachable story, standing pose or input state."); return false; }
        return true;
    }

    private bool AddressAuditStayedSuspended(Act1ConnectedWorld world, RuntimeBridge bridge,
        FirstPersonController player, string phase)
    {
        if (_addressNaturalQueueOnly)
            return NaturalAddressAuditStayedIndoors(world, bridge, player, phase);
        PrintAddressLifecycle(phase, world, player);
        var access = world.AddressRegistry!.AccessPoints[_addressLifecycleAccess];
        if (bridge.CurrentZoneId != "house_old_pc" || world.GetMeta("activeExteriorAtmosphere", true).AsBool()
            || AuditCount("lifecycleSearchSteps") != _addressLifecycleSearchSteps
            || AuditCount("lifecycleCommittedJobs") != _addressLifecycleCommits
            || AuditCount("lifecycleInterruptedJobs") != _addressLifecycleInterrupted + 1
            || AuditCount("lifecycleEpoch") <= _addressLifecycleEpoch
            || _addressLifecycleAudit!.GetMeta("lifecycleCurrentAccess", "").AsString() != _addressLifecycleAccess
            || access.State != _addressLifecycleInitialState)
        { Fail("An inactive exterior query survived, advanced, or committed during the actual household visit."); return false; }
        return true;
    }

    private async Task<bool> CompleteAddressLifecycleOutside(Act1ConnectedWorld world,
        RuntimeBridge bridge, FirstPersonController player)
    {
        if (_addressNaturalQueueOnly) return await CompleteNaturalAddressQueueOutside(world, bridge, player);
        var registry = world.AddressRegistry!;
        var knowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var scene = bridge.ActiveSceneId;
        var revision = player.PresentationTransformRevision;
        var recoveries = player.FallRecoveries;
        var clamps = player.EdgeClamps;
        PrintAddressLifecycle("after-ordinary-house-exit", world, player);
        for (var frame = 0; frame < 1800; frame++)
        {
            var state = registry.AccessPoints[_addressLifecycleAccess].State;
            if (AuditCount("lifecycleCommittedJobs") > _addressLifecycleCommits
                && (state is "pending-graph-attachment" or "verified")) break;
            if (AuditCount("lifecycleCommittedJobs") > _addressLifecycleCommits
                && !state.StartsWith("pending", StringComparison.Ordinal))
            { Fail("The resumed actual mosque access audit refused: " + state); return false; }
            await PhysicsFrames(1);
        }
        var access = registry.AccessPoints[_addressLifecycleAccess];
        var diagnosticAttachment = access.State == "pending-graph-attachment";
        if (diagnosticAttachment)
        {
            if (world.GetMeta("addressAccessAuditCompleted", false).AsBool())
            { Fail("An unattached selected path was incorrectly marked as a completed whole audit."); return false; }
            GD.Print($"walk-address-lifecycle-graph: mode=explicit-diagnostic-subset phase=before "
                + $"access={_addressLifecycleAccess} state={access.State} fullAuditCompleted=false nodes={registry.Graph.Nodes.Count}");
            world.AttachVerifiedAddressAccessPaths();
            access = registry.AccessPoints[_addressLifecycleAccess];
            GD.Print($"walk-address-lifecycle-graph: mode=explicit-diagnostic-subset phase=after "
                + $"access={_addressLifecycleAccess} state={access.State} "
                + $"fullAuditCompleted={world.GetMeta("addressAccessAuditCompleted", false).AsBool()} nodes={registry.Graph.Nodes.Count}");
            if (world.GetMeta("addressAccessAuditCompleted", false).AsBool())
            { Fail("Diagnostic subset attachment marked the unfinished ordinary queue complete."); return false; }
        }
        access = registry.AccessPoints[_addressLifecycleAccess];
        PrintAddressLifecycle("resumed-route-complete", world, player);
        var graph = registry.Graph;
        var origin = graph.NodeAt(graph.Roads["authored/main-axis"].Points[0]);
        var route = origin is null ? Array.Empty<SettlementPoint>()
            : registry.DiagnosticRoute(origin, "ADR-MOSQUE", SettlementTravelMode.Foot).ToArray();
        if (bridge.CurrentZoneId != "village_day" || !world.GetMeta("activeExteriorAtmosphere", false).AsBool()
            || access.State != "verified" || route.Length < 2
            || AuditCount("lifecycleSearchSteps") <= _addressLifecycleSearchSteps + 1
            || AuditCount("lifecycleCommittedJobs") <= _addressLifecycleCommits
            || AuditCount("lifecycleFirstSearchFrame") < AuditCount("lifecycleReadyAfterFrame")
            || player.PresentationTransformRevision != revision || player.FallRecoveries != recoveries
            || player.EdgeClamps != clamps || bridge.ActiveSceneId != scene
            || bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() != knowledge)
        { Fail("Ordinary exterior return did not restart and verify the same physical access without player/progress changes."); return false; }
        GD.Print($"walk-address-lifecycle: PASS ordinary arrival/household -> actual indoor save/load with no exterior probes/commits "
            + $"-> manual house exit -> fresh supported route; access={_addressLifecycleAccess}; graphPoints={route.Length}; "
            + $"graphAttachment={(diagnosticAttachment ? "explicit-diagnostic-subset" : "ordinary-completed-queue")}; "
            + $"fullAuditCompleted={world.GetMeta("addressAccessAuditCompleted", false).AsBool()}; "
            + "no target teleport/knowledge fixture; no human-duration claim");
        return true;
    }

    private bool BeginNaturalAddressIndoor(Act1ConnectedWorld world, RuntimeBridge bridge,
        FirstPersonController player)
    {
        // The ordinary queue may legitimately advance while the player aims and
        // presses the door. Capture its inactive baseline only after real entry.
        PrintNaturalAddressQueue("after-ordinary-house-door", world, player);
        if (bridge.CurrentZoneId != "house_old_pc" || world.GetMeta("activeExteriorAtmosphere", true).AsBool()
            || (!world.GetMeta("addressAccessAuditCompleted", false).AsBool()
                && AuditCount("lifecycleEpoch") <= _addressLifecycleEpoch))
        { Fail("The ordinary queue did not observe the actual indoor collision presentation."); return false; }
        _addressLifecycleSearchSteps = AuditCount("lifecycleSearchSteps");
        _addressLifecycleCommits = AuditCount("lifecycleCommittedJobs");
        _addressLifecycleInterrupted = AuditCount("lifecycleInterruptedJobs");
        _addressLifecycleInitialState = world.AddressRegistry!.AccessPoints[_addressLifecycleAccess].State;
        _addressNaturalIndoorCurrent = _addressLifecycleAudit!.GetMeta("lifecycleCurrentAccess", "").AsString();
        _addressNaturalIndoorAttachments = world.GetMeta("addressGraphAttachmentCount", 0L).AsInt64();
        _addressNaturalIndoorCommitFrame = NaturalAccessFrame(world, "addressAccessCommitFrames");
        _addressNaturalIndoorAttachFrame = NaturalAccessFrame(world, "addressAccessAttachFrames");
        return true;
    }

    private bool NaturalAddressAuditStayedIndoors(Act1ConnectedWorld world, RuntimeBridge bridge,
        FirstPersonController player, string phase)
    {
        PrintNaturalAddressQueue(phase, world, player);
        if (bridge.CurrentZoneId != "house_old_pc" || world.GetMeta("activeExteriorAtmosphere", true).AsBool()
            || AuditCount("lifecycleSearchSteps") != _addressLifecycleSearchSteps
            || AuditCount("lifecycleCommittedJobs") != _addressLifecycleCommits
            || AuditCount("lifecycleInterruptedJobs") != _addressLifecycleInterrupted
            || world.GetMeta("addressGraphAttachmentCount", 0L).AsInt64() != _addressNaturalIndoorAttachments
            || NaturalAccessFrame(world, "addressAccessCommitFrames") != _addressNaturalIndoorCommitFrame
            || NaturalAccessFrame(world, "addressAccessAttachFrames") != _addressNaturalIndoorAttachFrame
            || _addressLifecycleAudit!.GetMeta("lifecycleCurrentAccess", "").AsString() != _addressNaturalIndoorCurrent
            || world.AddressRegistry!.AccessPoints[_addressLifecycleAccess].State != _addressLifecycleInitialState)
        { Fail("The untouched ordinary queue probed, committed, or lost its retained address during the indoor visit/load."); return false; }
        return true;
    }

    private async Task<bool> CompleteNaturalAddressQueueOutside(Act1ConnectedWorld world,
        RuntimeBridge bridge, FirstPersonController player)
    {
        var registry = world.AddressRegistry!;
        var knowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var scene = bridge.ActiveSceneId;
        var heard = bridge.KnownAddressIds().ToHashSet(StringComparer.Ordinal);
        var located = bridge.LocatedAddressIds().ToHashSet(StringComparer.Ordinal);
        var revision = player.PresentationTransformRevision;
        var recoveries = player.FallRecoveries;
        var clamps = player.EdgeClamps;
        var startedFrame = Engine.GetPhysicsFrames();
        var startedMsec = Time.GetTicksMsec();
        using var watchdogCancellation = new System.Threading.CancellationTokenSource();
        var watchdog = Task.Delay(TimeSpan.FromSeconds(60), watchdogCancellation.Token);
        var feetBeforeSettling = player.GlobalPosition;
        Vector3? stableFeet = null;
        var lastBoundary = "";
        try
        {
            PrintNaturalAddressQueue("after-ordinary-house-exit", world, player);
            var settled = 0;
            var previousY = player.GlobalPosition.Y;
            for (var frame = 0; frame < 45 && settled < 3; frame++)
            {
                if (!CheckInvariants() || !await AwaitBudgetFrame("natural-floor-settle")) return false;
                var y = player.GlobalPosition.Y;
                settled = player.IsOnFloor() && Math.Abs(player.Velocity.Y) < .05f && Math.Abs(y - previousY) < .002f
                    ? settled + 1 : 0;
                previousY = y;
            }
            if (settled < 3 || player.IsCrouching || !player.CanStandAt(player.GlobalPosition))
            { Fail("The ordinary house exit did not settle on a real standing support before observing queue readiness."); return false; }
            stableFeet = player.GlobalPosition;
            GD.Print($"walk-address-natural-floor: before={feetBeforeSettling} settled={stableFeet.Value} "
                + $"frames={Engine.GetPhysicsFrames() - startedFrame}; no position writes");

            while (registry.AccessPoints[_addressLifecycleAccess].State.StartsWith("pending", StringComparison.Ordinal))
            {
                ObserveBoundary();
                if (!CheckInvariants() || !await AwaitBudgetFrame("target-route-pending")) return false;
            }
            ObserveBoundary();
            if (!CheckInvariants()) return false;
            var access = registry.AccessPoints[_addressLifecycleAccess];
            var commitFrame = NaturalAccessFrame(world, "addressAccessCommitFrames");
            var attachFrame = NaturalAccessFrame(world, "addressAccessAttachFrames");
            var graph = registry.Graph;
            var origin = graph.NodeAt(graph.Roads["authored/main-axis"].Points[0]);
            var route = origin is null ? Array.Empty<SettlementPoint>()
                : registry.DiagnosticRoute(origin, "ADR-MOSQUE", SettlementTravelMode.Foot).ToArray();
            var authored = world.AddressApproachPath(_addressLifecycleAccess);
            var finalPoint = authored.Count == 0 ? Act1ConnectedWorld.AddressVector(access.Position) : authored[^1];
            if (access.State != "verified" || route.Length < 2 || string.IsNullOrEmpty(access.GraphNodeId)
                || origin is null || !graph.ReachableNodes(origin, SettlementTravelMode.Foot).Contains(access.GraphNodeId)
                || route[^1].Distance(new(finalPoint.X, finalPoint.Y, finalPoint.Z)) > .12
                || commitFrame < 0 || attachFrame < commitFrame || attachFrame > (long)Engine.GetPhysicsFrames())
            {
                PrintNaturalAddressQueue("failed-common-route", world, player);
                Fail($"The ordinary queue did not publish its physically committed mosque path: state={access.State}; "
                    + $"routePoints={route.Length}; commitFrame={commitFrame}; attachFrame={attachFrame}.");
                return false;
            }
            PrintNaturalAddressQueue("ordinary-target-route-ready", world, player);
            if (BudgetExpired()) return BudgetFailed("final-common-route");
            GD.Print($"walk-address-natural-queue: PASS ordinary arrival/household/indoor save/load/manual exit; "
                + $"target={_addressLifecycleAccess}; routePoints={route.Length}; commitFrame={commitFrame}; attachFrame={attachFrame}; "
                + $"targetAlreadyVerifiedAtIndoorBaseline={_addressLifecycleInitialState == "verified"}; "
                + $"framesAfterExit={Engine.GetPhysicsFrames() - startedFrame}; elapsedMsec={Time.GetTicksMsec() - startedMsec}; "
                + $"fullAuditCompleted={world.GetMeta("addressAccessAuditCompleted", false).AsBool()}; "
                + "ordinary scheduling/attachment only; no target probe, reordering, graph fixture, knowledge or pose writes; "
                + "physical mosque walk and human duration are outside this scope");
            return true;
        }
        finally { watchdogCancellation.Cancel(); }

        bool CheckInvariants()
        {
            var first = AuditCount("lifecycleFirstSearchFrame");
            var ready = AuditCount("lifecycleReadyAfterFrame");
            if (bridge.CurrentZoneId != "village_day" || !world.GetMeta("activeExteriorAtmosphere", false).AsBool()
                || player.ModalOpen || GetTree().Paused || player.PresentationTransformRevision != revision
                || player.FallRecoveries != recoveries || player.EdgeClamps != clamps
                || (first >= 0 && first < ready) || bridge.ActiveSceneId != scene
                || bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() != knowledge
                || !heard.SetEquals(bridge.KnownAddressIds()) || !located.SetEquals(bridge.LocatedAddressIds())
                || (stableFeet is { } feet && (player.GlobalPosition.DistanceTo(feet) > .002f
                    || !player.IsOnFloor() || player.IsCrouching || !player.CanStandAt(player.GlobalPosition))))
            {
                PrintNaturalAddressQueue("failed-observation-invariant", world, player);
                Fail("Waiting for the ordinary address queue changed the player/story or searched before exterior readiness.");
                return false;
            }
            return true;
        }

        void ObserveBoundary()
        {
            var key = $"{AuditCount("lifecycleEpoch")}/{_addressLifecycleAudit!.GetMeta("lifecycleStage", "").AsString()}/"
                + $"{_addressLifecycleAudit.GetMeta("lifecycleCurrentAccess", "").AsString()}/{AuditCount("lifecycleCommittedJobs")}/"
                + registry.AccessPoints[_addressLifecycleAccess].State;
            if (key == lastBoundary) return;
            lastBoundary = key;
            PrintNaturalAddressQueue("ordinary-queue-boundary", world, player);
        }

        async Task<bool> AwaitBudgetFrame(string phase)
        {
            if (Engine.GetPhysicsFrames() - startedFrame >= 600 || BudgetExpired())
                return BudgetFailed(phase);
            var nextFrame = PhysicsFrames(1);
            if (await Task.WhenAny(nextFrame, watchdog) != nextFrame) return BudgetFailed(phase);
            await nextFrame;
            if (BudgetExpired()) return BudgetFailed(phase);
            return true;
        }

        bool BudgetExpired() => Engine.GetPhysicsFrames() - startedFrame > 600
            || Time.GetTicksMsec() - startedMsec >= 60000 || watchdog.IsCompleted;

        bool BudgetFailed(string phase)
        {
            PrintNaturalAddressQueue("failed-budget-" + phase, world, player);
            Fail($"The ordinary mosque queue exceeded its 600-physics-frame/60-second readiness budget: "
                + $"frames={Engine.GetPhysicsFrames() - startedFrame}; elapsedMsec={Time.GetTicksMsec() - startedMsec}.");
            return false;
        }
    }

    private long NaturalAccessFrame(Act1ConnectedWorld world, string metadata)
    {
        if (!world.HasMeta(metadata)) return -1;
        var frames = world.GetMeta(metadata).AsGodotDictionary();
        return frames.TryGetValue(_addressLifecycleAccess, out var frame) ? frame.AsInt64() : -1;
    }

    private void PrintNaturalAddressQueue(string phase, Act1ConnectedWorld world, FirstPersonController player)
    {
        PrintAddressLifecycle("ordinary-" + phase, world, player);
        var registry = world.AddressRegistry!;
        GD.Print($"walk-address-natural-observation: phase={phase}; target={_addressLifecycleAccess}; "
            + $"state={registry.AccessPoints[_addressLifecycleAccess].State}; physicsFrame={Engine.GetPhysicsFrames()}; "
            + $"targetCommitFrame={NaturalAccessFrame(world, "addressAccessCommitFrames")}; "
            + $"targetAttachFrame={NaturalAccessFrame(world, "addressAccessAttachFrames")}; "
            + $"attachmentFrame={world.GetMeta("addressGraphAttachmentFrame", -1L).AsInt64()}; "
            + $"attachmentCount={world.GetMeta("addressGraphAttachmentCount", 0L).AsInt64()}; "
            + $"pending={registry.AccessPoints.Values.Count(a => a.State.StartsWith("pending", StringComparison.Ordinal))}; "
            + $"verified={registry.AccessPoints.Values.Count(a => a.State == "verified")}; total={registry.AccessPoints.Count}; "
            + $"fullAuditCompleted={world.GetMeta("addressAccessAuditCompleted", false).AsBool()}; "
            + $"grounded={player.IsOnFloor()}; modal={player.ModalOpen}");
    }

    private long AuditCount(string name) => _addressLifecycleAudit!.GetMeta(name, 0L).AsInt64();

    private void PrintAddressLifecycle(string phase, Act1ConnectedWorld world, FirstPersonController player)
        => GD.Print($"walk-address-lifecycle-observation: phase={phase} zone={world.ActiveZoneId} "
            + $"exterior={world.GetMeta("activeExteriorAtmosphere", false).AsBool()} "
            + $"access={_addressLifecycleAccess} state={world.AddressRegistry!.AccessPoints[_addressLifecycleAccess].State} "
            + $"epoch={AuditCount("lifecycleEpoch")} stage={_addressLifecycleAudit!.GetMeta("lifecycleStage", "").AsString()} "
            + $"steps={AuditCount("lifecycleSearchSteps")} commits={AuditCount("lifecycleCommittedJobs")} "
            + $"interrupted={AuditCount("lifecycleInterruptedJobs")} readyAfter={AuditCount("lifecycleReadyAfterFrame")} "
            + $"firstSearch={AuditCount("lifecycleFirstSearchFrame")} lastSearch={AuditCount("lifecycleLastSearchFrame")} feet={player.GlobalPosition}");
}
