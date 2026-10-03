using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>Read-only standing-body motion queries against the live world.
/// The invisible server body never moves and has no collision layer, input,
/// save state or interaction. All hypothetical poses are passed to BodyTestMotion.</summary>
internal sealed class AddressWalkProbe : IDisposable
{
    private const float Clearance = FirstPersonController.StepClearance;
    private readonly PhysicsDirectSpaceState3D _space;
    private readonly FirstPersonController _player;
    private readonly CapsuleShape3D _capsule;
    private readonly Vector3 _bodyCentre;
    private readonly float _floorNormalY;
    private readonly PhysicsShapeQueryParameters3D _shape;
    private readonly PhysicsTestMotionParameters3D _motion;
    private readonly PhysicsTestMotionResult3D _obstacle = new();
    private readonly PhysicsTestMotionResult3D _projectedObstacle = new();
    private readonly PhysicsTestMotionResult3D _downObstacle = new();
    private readonly PhysicsTestMotionResult3D _sweep = new();
    private readonly PhysicsTestMotionResult3D _landing = new();
    private readonly global::Godot.Collections.Array<Rid> _exclude = new();
    private Rid _body;
    // Same-frame memo of the standing query; see Clear.
    private readonly bool _memoizeSameFrameClear;
    private ulong _clearFrame = ulong.MaxValue;
    private bool _clearControlled;
    private float _clearStanding;
    private float _clearRadius;
    private Vector3 _clearPose;
    private bool _clearResult;
    internal string LastRejection { get; private set; } = string.Empty;
    internal string LastSupportProbe { get; private set; } = string.Empty;
    internal int StepsClimbed { get; private set; }

    internal AddressWalkProbe(Node3D world,bool memoizeSameFrameClear=false)
    {
        _memoizeSameFrameClear=memoizeSameFrameClear;
        _space = world.GetWorld3D().DirectSpaceState;
        var playerNodes = world.GetTree().GetNodesInGroup("player_controller");
        using var playerNodesOwner = (global::Godot.Collections.Array)playerNodes;
        var actors = playerNodes.OfType<CollisionObject3D>().ToArray();
        var player = actors.OfType<FirstPersonController>().FirstOrDefault()
            ?? throw new InvalidOperationException("Address motion audit requires the existing player body's dimensions.");
        _player = player;
        _capsule = new() { Radius = player.BodyRadius, Height = player.StandingBodyHeight };
        _bodyCentre = Vector3.Up * (_capsule.Height * .5f);
        _floorNormalY = Mathf.Cos(player.FloorMaxAngle);
        foreach (var actor in actors)
            _exclude.Add(actor.GetRid());
        _shape = new() { Shape = _capsule, CollisionMask = 3, Margin = .001f, Exclude = _exclude };
        _motion = new() { Margin = .001f, MaxCollisions = 4, ExcludeBodies = _exclude, RecoveryAsCollision = false };
        _body = PhysicsServer3D.BodyCreate();
        PhysicsServer3D.BodySetMode(_body, PhysicsServer3D.BodyMode.Kinematic);
        PhysicsServer3D.BodySetCollisionLayer(_body, 0);
        PhysicsServer3D.BodySetCollisionMask(_body, 3);
        PhysicsServer3D.BodyAddShape(_body, _capsule.GetRid(), new Transform3D(Basis.Identity, _bodyCentre));
        PhysicsServer3D.BodySetSpace(_body, world.GetWorld3D().Space);
    }

    // Use the controller's actual standing-clearance contract. A full motion
    // capsule has just landed on its real support; querying that same contact
    // again as an inflated static overlap wrongly rejects ordinary floors.
    // The full capsule, swept motion, floor normals and step checks stay intact.
    //
    // Within one physics frame the audit walks several 8 cm steps without ever
    // returning to the engine, so consecutive steps ask the same question about
    // the same pose (a step's start pose is the previous step's verified landing).
    // Nothing can move between those calls: the whole probe loop is synchronous
    // inside one _PhysicsProcess, and CanFitAt has no side effects, so the answer
    // for a bitwise-identical pose in the same frame is the same boolean. Reusing
    // it removes one shape query per step; a different pose, a different frame or
    // a NaN pose is still queried fresh.
    private bool Clear(Vector3 feet)
    {
        // Opt-in: the address audit drives the probe with a fully synchronous loop
        // inside one _PhysicsProcess, so no world state can change between two of
        // its steps. Other drivers (the walk smoke tests) interleave their own
        // frame waits and mutations, so they keep the unmemoised call and its
        // bit-identical behaviour.
        if (!_memoizeSameFrameClear) return _player.CanStandAt(feet);
        var frame = Engine.GetPhysicsFrames();
        // Everything the query reads belongs in the key: the capsule radius and
        // standing height (CanStandAt asks CanFitAt for StandingBodyHeight and the
        // query shape is keyed by radius) and VehicleControlled, on which CanFitAt
        // switches the collision mask. The world, the exclusion set and the masks
        // cannot change inside one tick for a caller that never returns to the
        // engine, so the frame plus these three inputs is the whole key.
        //
        // Named explicitly, because a future edit must not weaken this: CanFitAt
        // also reads the live CollisionMask (FirstPersonController.Movement.cs
        // switches it to 0 and back), the exclusion array (built once from the
        // player's own rid) and IsInsideTree(), and none of those three is part of
        // the key. They are covered only because the audit driver runs its whole
        // probe loop synchronously inside one _PhysicsProcess, so no other callback
        // can touch them between two steps. A driver that awaits frames must not
        // pass memoizeSameFrameClear: true.
        var standing = _player.StandingBodyHeight;
        var radius = _player.BodyRadius;
        var controlled = _player.VehicleControlled;
        if (frame == _clearFrame && controlled == _clearControlled && standing == _clearStanding && radius == _clearRadius
            && SameFeet(feet, _clearPose))
            return _clearResult;
        var result = _player.CanStandAt(feet);
        _clearFrame = frame; _clearControlled = controlled; _clearStanding = standing; _clearRadius = radius;
        _clearPose = feet; _clearResult = result;
        return result;
    }

    private bool Motion(Vector3 feet, Vector3 direction, PhysicsTestMotionResult3D result)
    {
        _motion.From = new(Basis.Identity, feet);
        _motion.Motion = direction;
        return PhysicsServer3D.BodyTestMotion(_body, _motion, result);
    }

    // IEEE-exact component comparison: no tolerance, so poses that differ by even
    // one ULP count as different, and NaN compares false (no skip). +0.0 and -0.0
    // do compare equal here; in the only reachable case that still hands CanFitAt
    // bitwise-identical arguments, because it adds strictly positive offsets to the
    // pose, so the skipped query would have returned the same answer.
    private static bool SameFeet(Vector3 a, Vector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    private bool Floor(Vector3 from, Vector3 to, out Vector3 point)
    {
        using var ray = PhysicsRayQueryParameters3D.Create(from, to, 3, _exclude);
        using var hit = _space.IntersectRay(ray);
        point = hit.Count == 0 ? default : hit["position"].AsVector3();
        return hit.Count != 0 && hit["normal"].AsVector3().Y >= _floorNormalY;
    }

    /// <summary>Resolve a nearby real floor, then land the full capsule on it.
    /// Supplied altitude remains meaningful: a roof or terrain metres below an
    /// authored entrance cannot silently substitute for its intended approach.</summary>
    internal bool TrySupport(Vector3 supplied, out Vector3 feet)
    {
        feet = default;
        LastSupportProbe = "supplied=" + supplied;
        LastRejection = "no walkable floor near supplied approach";
        if (!Floor(supplied + Vector3.Up * .55f, supplied - Vector3.Up * .75f, out var floor)) return false;
        var diagnose = System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_SUPPORT_DIAGNOSTICS") == "1";
        var attempts = new System.Collections.Generic.List<string>();
        LastRejection = "standing body blocked over approach";
        // Start close to the observed floor so a usable low canopy does not
        // require an extra 22 cm of headroom. Higher starts only resolve the
        // rounded capsule's normal resting height on an ordinary slope.
        foreach (var lift in new[] { .035f, .08f, .15f, FirstPersonController.MaximumStepHeight + Clearance })
        {
            var raised = floor + Vector3.Up * lift;
            if (!Clear(raised))
            {
                LastRejection = "standing body blocked before landing probe";
                if (diagnose) attempts.Add($"lift={lift} initial={raised} contacts=[{Contacts(raised)}] "
                    + $"playerCanStandAtInitial={_player.CanStandAt(raised)}");
                continue;
            }
            var hit = Motion(raised, Vector3.Down * (lift + .08f), _landing);
            if (!hit || _landing.GetCollisionNormal().Y < _floorNormalY)
            {
                LastRejection = hit ? "landing collision is not a walkable floor" : "body motion found no landing";
                if (diagnose) attempts.Add($"lift={lift} initialFree=true hit={hit} travel={_landing.GetTravel()} "
                    + (hit ? $"normal={_landing.GetCollisionNormal()} depth={_landing.GetCollisionDepth()} owner={Contact("landing", _landing)}" : ""));
                continue;
            }
            var landed = raised + _landing.GetTravel();
            // Same reasoning as TryAdvance: Clear(raised) above already passed this
            // exact query in this tick, so a zero-travel landing repeats nothing.
            if (!SameFeet(landed, raised) && !Clear(landed))
            {
                LastRejection = "standing capsule overlaps its returned landing contact";
                if (diagnose) attempts.Add($"lift={lift} initialFree=true hit=true travel={_landing.GetTravel()} "
                    + $"normal={_landing.GetCollisionNormal()} depth={_landing.GetCollisionDepth()} owner={Contact("landing", _landing)} "
                    + $"landed={landed} finalContacts=[{Contacts(landed)}] playerCanStandAtLanded={_player.CanStandAt(landed)}");
                continue;
            }
            feet = landed;
            LastRejection = string.Empty;
            LastSupportProbe = $"supplied={supplied} floor={floor} acceptedFeet={landed} lift={lift}; "
                + (diagnose ? $"normal={_landing.GetCollisionNormal()} depth={_landing.GetCollisionDepth()} owner={Contact("landing", _landing)} "
                    + $"playerCanStandAtLanded={_player.CanStandAt(landed)}; " : "")
                + string.Join("; ", attempts);
            return true;
        }
        LastSupportProbe = $"supplied={supplied} floor={floor} bodyCentre={_bodyCentre} radius={_capsule.Radius} height={_capsule.Height} margin=.001; "
            + string.Join("; ", attempts);
        return false;
    }

    private string Contacts(Vector3 feet)
    {
        _shape.Transform = new(Basis.Identity, feet + _bodyCentre);
        var hits = _space.IntersectShape(_shape, 6);
        using var hitsOwner = (global::Godot.Collections.Array)hits;
        var contacts = new System.Collections.Generic.List<string>(hits.Count);
        foreach (var hit in hits)
        {
            using (hit)
            {
                var owner = hit["collider"].AsGodotObject() as Node;
                contacts.Add((owner?.GetPath().ToString() ?? "unattached body") + " shape " + hit["shape"].AsInt32());
            }
        }
        return string.Join(" | ", contacts);
    }

    /// <summary>At most 8 cm of ordinary walking, preserving actual support Y.
    /// Step probes use the controller's six tread samples and the same three
    /// swept motions; a static capsule at the toe of a riser is not a wall test.</summary>
    internal bool TryAdvance(Vector3 from, Vector3 horizontal, out Vector3 feet)
    {
        feet = from;
        horizontal.Y = 0;
        var requested = horizontal;
        LastRejection = "initial standing capsule overlaps a collider";
        if (!Clear(from)) return false;
        var blocked = Motion(from, horizontal, _obstacle);
        if (blocked)
        {
            var direction = horizontal.Normalized();
            var riser = FindRiser(_obstacle, direction);
            if (riser >= 0) return TryStep(from, horizontal, _obstacle, riser, out feet);
            // Ordinary uphill ground follows its actual walkable plane. This
            // keeps the requested X/Z travel and does not turn a wall into a ramp.
            var floorNormal = _obstacle.GetCollisionNormal();
            LastRejection = Contact("non-walkable obstruction", _obstacle);
            if (floorNormal.Y < _floorNormalY)
                return DescribeAdvanceRefusal("initial-nonwalkable-without-riser", from, horizontal, null);
            horizontal.Y = -(floorNormal.X * horizontal.X + floorNormal.Z * horizontal.Z) / floorNormal.Y;
            if (Motion(from, horizontal, _projectedObstacle))
            {
                // A rounded contact with the current tread can first look like
                // walkable floor, while its tangent reaches the next riser. The
                // real controller continues moving and then tests that riser.
                // Apply the same six floor samples and three full-body sweeps
                // from this pose, keeping the original requested X/Z distance.
                riser = FindRiser(_projectedObstacle, direction);
                if (riser >= 0)
                {
                    if (TryStep(from, requested, _projectedObstacle, riser, out feet)) return true;
                    return DescribeAdvanceRefusal("projected-floor-step-rejected", from, requested, horizontal);
                }
                LastRejection = Contact("projected floor motion blocked", _projectedObstacle);
                return DescribeAdvanceRefusal("projected-floor-motion-blocked", from, requested, horizontal);
            }
        }
        var advanced = from + horizontal;
        if (!Clear(advanced))
        {
            LastRejection = "standing body blocked before ordinary step-down probe";
            return DescribeLandingRefusal("advanced-standing-blocked", from, horizontal, advanced, null, false, null, null);
        }
        if (!Motion(advanced, Vector3.Down * (FirstPersonController.MaximumStepHeight + .035f), _downObstacle))
        {
            LastRejection = "no physical landing within ordinary step-down height";
            return DescribeLandingRefusal("step-down-motion-no-hit", from, horizontal, advanced, null, true, false, null);
        }
        if (_downObstacle.GetCollisionNormal().Y < _floorNormalY)
        {
            // Mosque04: the floor-following sweep can clear, then the downward
            // capsule meets the rounded edge of the next ordinary tread. Test
            // that riser from the original feet with the original single X/Z
            // move. Its six tread rays and three full-body sweeps must all pass;
            // a steep landing contact alone never becomes a walkable floor.
            var riser = FindRiser(_downObstacle, requested.Normalized());
            if (riser >= 0)
            {
                if (TryStep(from, requested, _downObstacle, riser, out feet)) return true;
                return DescribeLandingRefusal("step-down-riser-step-rejected", from, horizontal, advanced, null, true, true, null);
            }
            LastRejection = Contact("ordinary step-down contact is not walkable", _downObstacle);
            return DescribeLandingRefusal("step-down-normal-rejected", from, horizontal, advanced, null, true, true, null);
        }
        feet = advanced + _downObstacle.GetTravel();
        // Only an exactly zero travel reuses a query: the standing check above for
        // `advanced` already ran and passed in this tick, so the identical pose
        // cannot answer differently, while a one-ULP difference is still queried.
        if (!SameFeet(feet, advanced) && !Clear(feet))
        {
            LastRejection = "standing body blocked at ordinary step-down landing";
            return DescribeLandingRefusal("landed-standing-blocked", from, horizontal, advanced, feet, true, true, false);
        }
        LastRejection = string.Empty;
        return true;
    }

    // Diagnose the same short-circuit decisions separately. Never perform a
    // landing motion when advanced clearance failed, or report an old landing
    // result from the previous route sample as this sample's collision.
    private bool DescribeLandingRefusal(string phase, Vector3 from, Vector3 advance,
        Vector3 advanced, Vector3? landed, bool advancedClear, bool? downHit, bool? landedClear)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_SUPPORT_DIAGNOSTICS") != "1") return false;
        LastRejection += " landingProbe=" + System.Text.Json.JsonSerializer.Serialize(new
        {
            phase, from = from.ToString(), advance = advance.ToString(), advanced = advanced.ToString(),
            landed = landed?.ToString(), advancedClear, downHit, landedClear,
            down = (Vector3.Down * (FirstPersonController.MaximumStepHeight + .035f)).ToString(),
            floorNormalY = _floorNormalY, radius = _capsule.Radius, height = _capsule.Height,
            margin = _motion.Margin, collisionMask = 3, recoveryAsCollision = _motion.RecoveryAsCollision,
            standingClearanceOwner = "FirstPersonController.CanStandAt",
            rawMotionCapsuleAdvancedContacts = advancedClear ? null : Contacts(advanced),
            rawMotionCapsuleLandedContacts = landed.HasValue ? Contacts(landed.Value) : null,
            travel = downHit.HasValue ? _downObstacle.GetTravel().ToString() : null,
            remainder = downHit.HasValue ? _downObstacle.GetRemainder().ToString() : null,
            candidateFeet = downHit == true ? (advanced + _downObstacle.GetTravel()).ToString() : null,
            landingNormalY = downHit == true ? (float?)_downObstacle.GetCollisionNormal().Y : null,
            safeFraction = downHit.HasValue ? (float?)_downObstacle.GetCollisionSafeFraction() : null,
            unsafeFraction = downHit.HasValue ? (float?)_downObstacle.GetCollisionUnsafeFraction() : null,
            contacts = downHit == true
                ? Enumerable.Range(0, _downObstacle.GetCollisionCount()).Select(index => new
                {
                    owner = (_downObstacle.GetCollider(index) as Node)?.GetPath().ToString() ?? _downObstacle.GetColliderRid(index).ToString(),
                    shape = _downObstacle.GetColliderShape(index), point = _downObstacle.GetCollisionPoint(index).ToString(),
                    normal = _downObstacle.GetCollisionNormal(index).ToString(), depth = _downObstacle.GetCollisionDepth(index)
                }).ToArray() : null
        });
        return false;
    }

    private int FindRiser(PhysicsTestMotionResult3D collision, Vector3 direction)
    {
        for (var index = 0; index < collision.GetCollisionCount(); index++)
        {
            var normal = collision.GetCollisionNormal(index);
            if (normal.Y < _floorNormalY && normal.Dot(direction) < -.05f) return index;
        }
        return -1;
    }

    // Keep the initial and tangent contacts separate from the subsequent step
    // sweeps, so a failed step still reports the actual obstacle that triggered it.
    private bool DescribeAdvanceRefusal(string phase, Vector3 from, Vector3 requested, Vector3? projected)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_ADDRESS_SUPPORT_DIAGNOSTICS") != "1") return false;
        object Sweep(PhysicsTestMotionResult3D result) => new
        {
            travel = result.GetTravel().ToString(), remainder = result.GetRemainder().ToString(),
            safeFraction = result.GetCollisionSafeFraction(), unsafeFraction = result.GetCollisionUnsafeFraction(),
            contacts = Enumerable.Range(0, result.GetCollisionCount()).Select(index => new
            {
                owner = (result.GetCollider(index) as Node)?.GetPath().ToString() ?? result.GetColliderRid(index).ToString(),
                shape = result.GetColliderShape(index), point = result.GetCollisionPoint(index).ToString(),
                normal = result.GetCollisionNormal(index).ToString(),
                directionDot = result.GetCollisionNormal(index).Dot(requested.Normalized()),
                depth = result.GetCollisionDepth(index)
            }).ToArray()
        };
        LastRejection += " advanceProbe=" + System.Text.Json.JsonSerializer.Serialize(new
        {
            phase, from = from.ToString(), requested = requested.ToString(), projected = projected?.ToString(),
            floorNormalY = _floorNormalY, radius = _capsule.Radius, height = _capsule.Height,
            margin = _motion.Margin, collisionMask = 3, recoveryAsCollision = _motion.RecoveryAsCollision,
            initial = Sweep(_obstacle), secondary = projected.HasValue ? Sweep(_projectedObstacle) : null
        });
        return false;
    }

    private bool TryStep(Vector3 from, Vector3 forward, PhysicsTestMotionResult3D obstacle, int riser, out Vector3 feet)
    {
        feet = from;
        var edge = obstacle.GetCollisionPoint(riser);
        var direction = forward.Normalized();
        var side = new Vector3(-direction.Z, 0, direction.X) * Math.Min(.10f, _capsule.Radius * .3f);
        var highest = float.NegativeInfinity;
        var lowest = float.PositiveInfinity;
        LastRejection = Contact("no walkable tread within controller step height", obstacle);
        foreach (var depth in new[] { .04f, .18f })
        foreach (var offset in new[] { -side, Vector3.Zero, side })
        {
            var at = edge + direction * depth + offset;
            if (!Floor(new(at.X, from.Y + FirstPersonController.MaximumStepHeight + Clearance, at.Z),
                new(at.X, from.Y + .015f, at.Z), out var tread)) return false;
            var height = tread.Y - from.Y;
            highest = Math.Max(highest, height); lowest = Math.Min(lowest, height);
        }
        if (highest > FirstPersonController.MaximumStepHeight || lowest < .015f || highest - lowest > .025f) return false;
        var up = Vector3.Up * (highest + Clearance);
        LastRejection = "ceiling above ordinary step";
        if (Motion(from, up, _sweep)) return false;
        var raised = from + up;
        LastRejection = "full body blocked beyond ordinary riser";
        if (Motion(raised, forward, _sweep)) return false;
        raised += forward;
        LastRejection = "no physical step landing";
        if (!Motion(raised, Vector3.Down * (up.Y + .025f), _landing)) return false;
        feet = raised + _landing.GetTravel();
        var rise = feet.Y - from.Y;
        if (rise <= .001f || rise > FirstPersonController.MaximumStepHeight + Clearance
            || _landing.GetCollisionNormal().Y <= .05f || !Clear(feet)) return false;
        StepsClimbed++;
        LastRejection = string.Empty;
        return true;
    }

    private static string Contact(string reason, PhysicsTestMotionResult3D collision)
    {
        var owner = collision.GetCollider() as Node;
        return reason + ": " + (owner?.GetPath().ToString() ?? collision.GetColliderRid().ToString())
            + " shape " + collision.GetColliderShape();
    }

    public void Dispose()
    {
        if (_body.IsValid) { PhysicsServer3D.FreeRid(_body); _body = default; }
        _motion.Dispose(); _shape.Dispose(); _obstacle.Dispose(); _projectedObstacle.Dispose(); _downObstacle.Dispose();
        _sweep.Dispose(); _landing.Dispose(); _capsule.Dispose(); ((global::Godot.Collections.Array)_exclude).Dispose();
    }
}
