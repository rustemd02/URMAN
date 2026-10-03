using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>Bounded read-only standing-body route audit against the real world.
/// It neither moves the player nor opens doors or advances runtime progress.</summary>
public partial class AddressAccessVerifier : Node3D
{
    private Act1ConnectedWorld _world = null!;
    private readonly Queue<(string AccessId, Vector3 Point)> _pending = new();
    private IEnumerator<bool>? _job;
    private (string AccessId, Vector3 Point)? _current;
    private RuntimeBridge? _bridge;
    // Ready stays the exact expression Context() used to store, but is derived
    // from the parts, so Refresh() re-evaluates it without re-reading them.
    private readonly record struct AuditContext(string Zone, object? Session, bool Exterior,
        RuntimeBridge.PlayTimeBlock Blocks, bool Paused, ulong PresentationRevision)
    {
        public bool Ready => Exterior && Session is not null && !Paused
            && (Blocks & (RuntimeBridge.PlayTimeBlock.Loading | RuntimeBridge.PlayTimeBlock.NotReady)) == 0;
    }
    private AuditContext _context;
    private ulong _epoch;
    private ulong _readyAfterFrame;
    private int _interruptedJobs;
    private int _committedJobs;
    private long _searchSteps;
    private ulong _lastSearchFrame;
    private long _firstSearchFrame=-1;
    private bool _diagnostics;
    private ulong _presentationRevision;

    internal void NotifyPresentationChanged()=>_presentationRevision++;
    /// <summary>Diagnostic summary for smokes waiting on one access point.</summary>
    internal string DescribeProgress()=>$"pending={_pending.Count} current={_current?.AccessId ?? "none"} "
        +$"job={(_job is not null)} processing={IsPhysicsProcessing()} completed={GetMeta("completed",false)}";

    private AuditContext Context()
    {
        _bridge ??= GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var exterior=_world.ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night"
            &&_world.HasMeta("activeExteriorAtmosphere")&&_world.GetMeta("activeExteriorAtmosphere").AsBool();
        var blocks=_bridge?.CapturePlayTimeBlocks()??RuntimeBridge.PlayTimeBlock.NotReady;
        var session=_bridge?.SessionIdentity;
        return new(_world.ActiveZoneId,session,exterior,blocks,GetTree().Paused,_presentationRevision);
    }
    // This callback is fully synchronous (no await/yield/CallDeferred here) and
    // every zone/exterior change bumps _presentationRevision from
    // Act1ConnectedWorld.SetActiveLogicalZone, the sole caller of
    // NotifyPresentationChanged, so refreshing these two fields matches Same().
    private AuditContext Refresh(in AuditContext basis)=>
        basis with { Paused=GetTree().Paused, PresentationRevision=_presentationRevision };
    private static bool Same(AuditContext a,AuditContext b)=>a.Zone==b.Zone&&a.Exterior==b.Exterior&&a.Ready==b.Ready
        &&a.PresentationRevision==b.PresentationRevision&&ReferenceEquals(a.Session,b.Session);
    private void CountSearchStep()
    {
        _searchSteps++;_lastSearchFrame=Engine.GetPhysicsFrames();
        if(_firstSearchFrame<0)
        {
            _firstSearchFrame=(long)_lastSearchFrame;
            if(_diagnostics)SetMeta("lifecycleFirstSearchFrame",_firstSearchFrame);
        }
        if(!_diagnostics)return;
        SetMeta("lifecycleSearchSteps",_searchSteps);
        SetMeta("lifecycleLastSearchFrame",(long)_lastSearchFrame);
    }
    private void ReportLifecycle(string stage,string detail="")
    {
        SetMeta("lifecycleEpoch",(long)_epoch);SetMeta("lifecycleStage",stage);
        SetMeta("lifecycleCurrentAccess",_current?.AccessId??"");SetMeta("lifecycleInterruptedJobs",_interruptedJobs);
        SetMeta("lifecycleCommittedJobs",_committedJobs);SetMeta("lifecycleReadyAfterFrame",(long)_readyAfterFrame);
        SetMeta("lifecycleSearchSteps",_searchSteps);
        SetMeta("lifecycleLastSearchFrame",(long)_lastSearchFrame);
        SetMeta("lifecycleFirstSearchFrame",_firstSearchFrame);
        if(_diagnostics)
        {
            var live=Context();
            GD.Print($"address-audit-lifecycle: epoch={_epoch} presentation={live.PresentationRevision} stage={stage} access={_current?.AccessId} zone={live.Zone} exterior={live.Exterior} ready={live.Ready} frame={Engine.GetPhysicsFrames()} {detail}");
        }
    }
    private void RestartCurrent()
    {
        if(_job is null)return;
        _job.Dispose();_job=null;_interruptedJobs++;
        // Keep _current ahead of the queue. Its uncommitted partial path belongs
        // to the old collision presentation and must never be resumed.
    }
    private bool ObservePresentation()
    {
        var next=Context();
        if(!Same(next,_context))
        {
            RestartCurrent();_context=next;_epoch++;
            _firstSearchFrame=-1;
            _readyAfterFrame=Engine.GetPhysicsFrames()+3;
            ReportLifecycle(next.Ready?"restoring-exterior":"waiting-for-exterior");
        }
        if(!_context.Ready)return false;
        if(Engine.GetPhysicsFrames()<_readyAfterFrame)return false;
        return true;
    }

    public void Initialize(Act1ConnectedWorld world, IEnumerable<(string AccessId, Vector3 Point)> points)
    {
        _world = world;
        _diagnostics=System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_SUPPORT_DIAGNOSTICS")=="1";
        foreach (var point in points) _pending.Enqueue(point);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!ObservePresentation()) return;
        // One context snapshot for this tick's loop. The loop re-checks it through
        // Refresh(tick) (paused flag + presentation revision), which is provably the
        // same predicate: see the comment on Refresh. The post-loop check below keeps
        // the full evaluation, so no exit path is relaxed. ObservePresentation and
        // that post-loop check remain the only other full evaluations.
        var tick = Context();
        var started = Time.GetTicksUsec();
        // One yield performs one bounded 8 cm body motion (including the six
        // tread rays when necessary), not a whole route or synchronous search.
        for (var probes = 0; probes < 48 && Time.GetTicksUsec() - started < 2000; probes++)
        {
            if (_job is null)
            {
                if (_current is null&&_pending.Count == 0)
                {
                    break;
                }
                _current??=_pending.Dequeue();var point=_current.Value;
                _job = Search(point.AccessId, point.Point).GetEnumerator();ReportLifecycle("search-start");
            }
            if(!Same(Refresh(tick),_context)){ObservePresentation();return;}
            try
            {
                CountSearchStep();
                if (!_job.MoveNext())
                {
                    _job.Dispose(); _job = null;_current=null;
                    // A real retry refusal removes its old graph immediately.
                    // That rebuild also publishes this tick's completed paths;
                    // do not start another batch in the same physics frame.
                    if(_world.AddressGraphAttachmentFrame==Engine.GetPhysicsFrames())break;
                }
            }
            catch(AuditPresentationChanged){RestartCurrent();ObservePresentation();return;}
        }
        if(!Same(Context(),_context)){ObservePresentation();return;}
        // Publish only whole physical results while this collision presentation
        // is still ready. No route is exposed by an unfinished iterator, and a
        // verified entrance no longer waits for all remaining village houses.
        if(_world.AddressGraphAttachmentPending
            &&_world.AddressGraphAttachmentFrame==Engine.GetPhysicsFrames())return;
        if(_job is null&&_current is null&&_pending.Count==0)
        {
            _world.CompleteAddressAccessAudit();SetPhysicsProcess(false);SetMeta("completed",true);
        }
        else if(_world.AddressGraphAttachmentPending)_world.AttachVerifiedAddressAccessPaths();
    }

    public override void _ExitTree() { _job?.Dispose(); _job = null; }

    // The explicit smoke scene suspends this component's normal queue while
    // stepping this same search. Disposing the returned enumerator releases its
    // physics probe on success, refusal, timeout or cancellation; _job is retained.
    internal IEnumerator<bool> ProbeAccessForDiagnostic(string accessId)
    {
        if(IsPhysicsProcessing())throw new InvalidOperationException("Suspend the ordinary audit before a selected diagnostic search.");
        var access=_world.AddressRegistry!.AccessPoints[accessId];
        return DiagnosticSearch(accessId,Act1ConnectedWorld.AddressVector(access.Position)).GetEnumerator();
    }

    private sealed class AuditPresentationChanged:Exception { }
    private IEnumerable<bool> DiagnosticSearch(string accessId,Vector3 point)
    {
        while(true)
        {
            var context=Context();var readyFrame=Engine.GetPhysicsFrames()+3;
            while(!context.Ready||Engine.GetPhysicsFrames()<readyFrame)
            {
                yield return true;
                var next=Context();if(!Same(next,context)){context=next;readyFrame=Engine.GetPhysicsFrames()+3;}
            }
            using var search=Search(accessId,point).GetEnumerator();
            while(Same(Context(),context))
            {
                bool more;
                try{CountSearchStep();more=search.MoveNext();}catch(AuditPresentationChanged){break;}
                if(!more)yield break;
                yield return true;
            }
            // A diagnostic load/zone change has the same dispose/restart rule.
        }
    }

    internal void StartLifecycleDiagnostic(string accessId)
    {
        if(IsPhysicsProcessing()||System.Environment.GetEnvironmentVariable("URMAN_WALK_ADDRESS_LIFECYCLE_ONLY")!="1"
            ||!GetTree().Root.GetChildren().Any(n=>n.GetType().Name=="Act1FirstPersonWalkthroughSmokeTest"))
            throw new InvalidOperationException("Selected lifecycle scheduling is confined to the explicit suspended walkthrough smoke scene.");
        if(!ObservePresentation())throw new InvalidOperationException("Wait for the real exterior collision presentation before selecting its lifecycle job.");
        RestartCurrent();
        var remaining=_pending.Where(p=>p.AccessId!=accessId).ToArray();_pending.Clear();
        if(_current is {} prior&&prior.AccessId!=accessId)_pending.Enqueue(prior);
        foreach(var pending in remaining)_pending.Enqueue(pending);
        var access=_world.AddressRegistry!.AccessPoints[accessId];
        _current=(accessId,Act1ConnectedWorld.AddressVector(access.Position));
        _job=Search(accessId,_current.Value.Point).GetEnumerator();
        _world.SetMeta("addressAccessAuditCompleted",false);SetMeta("completed",false);
        CountSearchStep();
        if(!_job.MoveNext())throw new InvalidOperationException("Selected access did not produce a real partial physics search.");
        ReportLifecycle("diagnostic-one-move-next");
    }

    private IEnumerable<bool> Search(string id, Vector3 target)
    {
        var context=Context();
        if(!context.Ready)throw new AuditPresentationChanged();
        void Commit(Vector3[]? path,string failure,SettlementPoint? anchor=null)
        {
            if(!Same(Context(),context))throw new AuditPresentationChanged();
            _world.CommitAddressAccess(id,path,failure,anchor);_committedJobs++;
            ReportLifecycle(path is null?"blocked":"path-committed",$"testedAccess={id} supplied={target} reason={failure}");
        }
        var finalTarget = target;
        var authoredApproach = _world.AddressApproachPath(id).ToArray();
        // Connect to the bottom of a real approach first. A nearest road beside
        // the upper landing is not evidence that its intervening stairs work.
        if (authoredApproach.Length > 0) target = authoredApproach[0];
        var graph = _world.AddressRegistry!.Graph;
        var origin = graph.NodeAt(graph.Roads["authored/main-axis"].Points[0]);
        var connected = origin is null ? new HashSet<string>() : graph.ReachableNodes(origin, SettlementTravelMode.Foot);
        var nearest = graph.Nearest(Act1ConnectedWorld.AddressPoint(target), filter: e => connected.Contains(e.A) && connected.Contains(e.B));
        if (nearest is null) { Commit(null, "no connected foot road"); yield break; }
        var graphAnchor = nearest.Value.Point;
        var suppliedStart = Act1ConnectedWorld.AddressVector(graphAnchor);
        if (new Vector2(suppliedStart.X - target.X, suppliedStart.Z - target.Z).Length() < .01f)
        {
            var edge = nearest.Value.Edge;
            graphAnchor = new[] { graph.Nodes[edge.A].Position, graph.Nodes[edge.B].Position }
                .OrderBy(p => p.DistanceXZ(Act1ConnectedWorld.AddressPoint(target))).First();
            suppliedStart = Act1ConnectedWorld.AddressVector(graphAnchor);
        }
        using var body = new AddressWalkProbe(this);
        void ReportSupport(string stage)
        {
            if (System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_SUPPORT_DIAGNOSTICS") == "1")
                GD.Print("address-support-probe: " + id + " " + stage + " result="
                    + (body.LastRejection.Length == 0 ? "accepted" : body.LastRejection) + " " + body.LastSupportProbe);
        }
        // NOTE (audit-21): snapping a fence-buried graph anchor to open ground
        // was tried and reverted: the snapped point floats off the graph edge,
        // so the committed route loses edge connectivity (graph-attachment
        // failed). The defect is the graph road crossing FapServiceFence at
        // (33,-28.4) — road/fence scene surgery, not an audit tweak. The
        // anchor is therefore used as authored; H033/H037 stay blocked with
        // their exact reason until the road or the fence moves.
        if (!body.TrySupport(target, out var supportedTarget))
        {
            ReportSupport("approach start");
            Commit(null, "approach start: " + body.LastRejection); yield break;
        }
        ReportSupport("approach start");
        if (authoredApproach.Length > 0 && Math.Abs(supportedTarget.Y - target.Y) >= .08f)
        {
            Commit(null, "authored approach start has a different actual support height"); yield break;
        }
        yield return true;
        if (!body.TrySupport(suppliedStart, out var start))
        {
            ReportSupport("road anchor");
            Commit(null, "connected road anchor: " + body.LastRejection); yield break;
        }
        ReportSupport("road anchor");
        yield return true;

        var segmentValid = false;
        var segmentPath = new List<Vector3>();
        var firstRejection = string.Empty;
        IEnumerable<bool> Segment(Vector3 from, Vector3 destination)
        {
            segmentValid = false; segmentPath.Clear(); segmentPath.Add(from);
            var displacement = destination - from; displacement.Y = 0;
            var steps = Math.Max(1, (int)Math.Ceiling(displacement.Length() / .08f));
            var advance = displacement / steps;
            var feet = from;
            for (var i = 0; i < steps; i++)
            {
                var free = body.TryAdvance(feet, advance, out var next);
                yield return true;
                if (!free)
                {
                    if (firstRejection.Length == 0) firstRejection = body.LastRejection;
                    yield break;
                }
                feet = next; segmentPath.Add(feet);
            }
            segmentValid = true;
        }

        bool AtTarget() => segmentValid && Math.Abs(segmentPath[^1].Y - supportedTarget.Y) < .08f;
        IEnumerable<bool> CommitCompleteRoute(IReadOnlyList<Vector3> connectedRoute)
        {
            // Copy before Segment reuses its working list. Nothing is committed
            // until every requested bend/landing and the real final access point
            // has been reached by the same capsule with continuous support.
            var complete = new List<Vector3>(connectedRoute);
            var checkedSegments = 0;
            var remaining = authoredApproach.Skip(1);
            if (authoredApproach.Length > 0 && authoredApproach[^1].DistanceTo(finalTarget) > .000001f)
                remaining = remaining.Append(finalTarget);
            foreach (var waypoint in remaining)
            {
                if (!body.TrySupport(waypoint, out var supported))
                {
                    ReportSupport("authored segment " + checkedSegments);
                    Commit(null, "authored approach segment " + checkedSegments + ": " + body.LastRejection);
                    yield break;
                }
                ReportSupport("authored segment " + checkedSegments);
                yield return true;
                if (authoredApproach.Length > 0 && Math.Abs(supported.Y - waypoint.Y) >= .08f)
                {
                    Commit(null, "authored approach segment " + checkedSegments + " has a different actual support height");
                    yield break;
                }
                foreach (var sample in Segment(complete[^1], supported)) yield return sample;
                if (!segmentValid || Math.Abs(segmentPath[^1].Y - supported.Y) >= .08f)
                {
                    Commit(null, "authored approach segment " + checkedSegments + " blocked: "
                        + (segmentValid ? "actual landing height differs" : body.LastRejection));
                    yield break;
                }
                complete.AddRange(segmentPath.Skip(1));
                checkedSegments++;
            }
            if (authoredApproach.Length > 0)
                SetMeta("lastAuthoredApproach", id + ": " + checkedSegments + " continuous segments; final feet " + complete[^1]);
            Commit(Compact(complete), "", graphAnchor);
        }
        foreach (var sample in Segment(start, supportedTarget)) yield return sample;
        if (AtTarget())
        {
            foreach (var sample in CommitCompleteRoute(segmentPath)) yield return sample;
            yield break;
        }

        // A 75 cm lattice could miss the existing human-sized gate opening.
        // This search samples physical space; it adds no street or regular grid
        // to the settlement. Keep height per reached node, not terrain projection.
        const float cell = .40f;
        const int limit = 5000;
        var goal = ((int)MathF.Round((target.X - start.X) / cell), (int)MathF.Round((target.Z - start.Z) / cell));
        var cost = new Dictionary<(int, int), float> { [(0, 0)] = 0 };
        var positions = new Dictionary<(int, int), Vector3> { [(0, 0)] = start };
        var previous = new Dictionary<(int, int), (int, int)>();
        var segments = new Dictionary<(int, int), Vector3[]>();
        var closed = new HashSet<(int, int)>();
        var queue = new PriorityQueue<(int, int), float>(); queue.Enqueue((0, 0), 0);
        (int, int)? found = null;
        var tail = Array.Empty<Vector3>();
        var expanded = 0;
        while (queue.TryDequeue(out var key, out _) && expanded < limit)
        {
            if (!closed.Add(key)) continue;
            expanded++;
            var here = positions[key];
            if (new Vector2(here.X - target.X, here.Z - target.Z).Length() < .75f)
            {
                foreach (var sample in Segment(here, supportedTarget)) yield return sample;
                if (AtTarget()) { found = key; tail = segmentPath.ToArray(); break; }
            }
            foreach (var dx in new[] { -1, 0, 1 }) foreach (var dz in new[] { -1, 0, 1 })
            {
                if (dx == 0 && dz == 0) continue;
                var next = (key.Item1 + dx, key.Item2 + dz);
                if (closed.Contains(next)) continue;
                if (next.Item1 < Math.Min(0, goal.Item1) - 20 || next.Item1 > Math.Max(0, goal.Item1) + 20
                    || next.Item2 < Math.Min(0, goal.Item2) - 20 || next.Item2 > Math.Max(0, goal.Item2) + 20) continue;
                var candidate = cost[key] + (dx == 0 || dz == 0 ? 1 : 1.414214f);
                if (cost.TryGetValue(next, out var old) && old <= candidate) continue;
                var intended = new Vector3(start.X + next.Item1 * cell, here.Y, start.Z + next.Item2 * cell);
                foreach (var sample in Segment(here, intended)) yield return sample;
                if (!segmentValid) continue;
                var reached = segmentPath[^1];
                cost[next] = candidate; positions[next] = reached; previous[next] = key; segments[next] = segmentPath.ToArray();
                queue.Enqueue(next, candidate + new Vector2(reached.X - target.X, reached.Z - target.Z).Length() / cell);
            }
        }
        SetMeta("lastAccessProbe", id + ": " + expanded + " cells; " + body.StepsClimbed + " verified step motions; " + firstRejection);
        if (found is null)
        {
            Commit(null, "standing body route not found; examined " + expanded + " cells; " + firstRejection); yield break;
        }
        var keys = new List<(int, int)> { found.Value };
        while (keys[^1] != (0, 0)) keys.Add(previous[keys[^1]]);
        keys.Reverse();
        var route = new List<Vector3> { start };
        foreach (var key in keys.Skip(1)) route.AddRange(segments[key].Skip(1));
        route.AddRange(tail.Skip(1));
        foreach (var sample in CommitCompleteRoute(route)) yield return sample;
    }

    private static Vector3[] Compact(IReadOnlyList<Vector3> route)
    {
        // Keep measured risers and slope changes. Flattening Y here would turn
        // a verified staircase into a path drawn through its supporting boards.
        var compact = new List<Vector3> { route[0] };
        for (var i = 1; i < route.Count - 1; i++)
        {
            var a = route[i] - compact[^1]; var b = route[i + 1] - route[i];
            if (a.LengthSquared() < .0000001f) continue;
            if (b.LengthSquared() < .0000001f || a.Normalized().Dot(b.Normalized()) < .99999f) compact.Add(route[i]);
        }
        if (route.Count > 1) compact.Add(route[^1]);
        return compact.ToArray();
    }
}
