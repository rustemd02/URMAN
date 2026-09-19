using Godot;

namespace Urman.Godot;

public partial class CarryCoordinator
{
    // Held props are not rigid bodies: their entire volume is swept explicitly.
    // A single camera ray cannot keep the far end of a board out of a wall.
    private bool ClearVolume(CarryableProp prop, Vector3 feet, float yaw, Vector3 motion = default,
        float? sweepYaw = null, Vector3? envelope = null, bool includePlayer = false, float margin = .008f)
    {
        if (_camera is null || _player is null) return false;
        var size = envelope ?? prop.Size;
        using var shape = new BoxShape3D { Size = size };
        var basis = Basis.FromEuler(new(0, Mathf.DegToRad(sweepYaw ?? yaw), 0));
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new(basis, feet + Vector3.Up * size.Y * .5f),
            CollisionMask = 3u,
            Margin = margin,
            Exclude = includePlayer ? new global::Godot.Collections.Array<Rid> { prop.GetRid() }
                : new global::Godot.Collections.Array<Rid> { prop.GetRid(), _player.GetRid() }
        };
        var space = _camera.GetWorld3D().DirectSpaceState;
        if (space.IntersectShape(query, 1).Count > 0) return false;
        if (motion.LengthSquared() < .000001f) return true;
        query.Motion = motion;
        var fractions = space.CastMotion(query);
        return fractions.Length >= 2 && fractions[0] >= .999f;
    }

    private Vector3 DesiredHoldPoint(CarryableProp prop, Vector3 displacement)
    {
        var forward = -_camera!.GlobalBasis.Z;
        var horizontal = new Vector3(forward.X, 0, forward.Z).Normalized();
        if (horizontal.LengthSquared() < .01f) horizontal = -_player!.GlobalBasis.Z;
        return _camera.GlobalPosition + displacement + horizontal * prop.HoldDistance
            + Vector3.Up * (prop.HoldDrop - Mathf.Max(0, prop.Height - .45f) * .55f);
    }

    private bool TryHeldPose(CarryableProp prop, Vector3 displacement, float yaw, out Vector3 point,
        Vector3? resultingEnvelope = null, bool fromWorld = false)
    {
        point = Vector3.Zero;
        if (_camera is null || _player is null) return false;
        if (fromWorld) LastPickupProbe = $"item={prop.ItemId} source={prop.GlobalTransform} size={prop.Size};";
        // Small retreats preserve the requested world rotation. We never
        // shrink the object, draw it through a wall, or teleport it behind one.
        foreach (var candidate in HeldPoseCandidates(prop, displacement))
        {
            if (!ClearVolume(prop, candidate, yaw, envelope: resultingEnvelope))
            {
                if (fromWorld) LastPickupProbe += $" to={candidate} endpoint-blocked;";
                continue;
            }
            if (fromWorld)
            {
                if (!CanPullFromRest(prop, candidate, yaw, out var pickupReason))
                {
                    LastPickupProbe += $" to={candidate} {pickupReason};";
                    continue;
                }
                LastPickupProbe += $" path={pickupReason};";
            }
            if (_heldPoseValid && _heldPoseItem == prop.ItemId
                && !ClearVolume(prop, prop.GlobalPosition, yaw, candidate - prop.GlobalPosition)) continue;
            point = candidate;
            if (fromWorld) LastPickupProbe += $" accepted={candidate};";
            return true;
        }
        return !fromWorld && resultingEnvelope is null
            && TryRetainedHeldTranslation(prop, displacement, yaw, out point, out _);
    }

    // Turning the camera can put every preferred pose across an obstacle.
    // Keep the last approved pose attached to the owner's actual movement,
    // allowing a bounded retreat while still sweeping the whole object.
    // This query never advances the owner anchor: only an accepted HoldAt does.
    private bool TryRetainedHeldTranslation(CarryableProp prop, Vector3 displacement, float yaw, out Vector3 point,
        out string reason)
    {
        point = Vector3.Zero;
        reason = "owner-or-yaw-unavailable";
        if (_player is null || _camera is null || _held != prop || !HasValidHeldPose
            || Mathf.Abs(Mathf.Wrap(yaw - prop.YawDegrees, -180f, 180f)) > .01f) return false;
        var candidate = prop.GlobalPosition + (_player.GlobalPosition - _heldOwnerFeetAtPose) + displacement;
        var ownerFeet = _player.GlobalPosition + displacement;
        var offset = candidate - ownerFeet;
        if (new Vector2(offset.X, offset.Z).Length() > prop.HoldDistance + .25f)
        { reason = "horizontal-reach-limit"; return false; }
        if (Mathf.Abs(candidate.Y - DesiredHoldPoint(prop, displacement).Y) > .25f)
        { reason = "height-follow-limit"; return false; }
        if (!ClearVolume(prop, prop.GlobalPosition, yaw, candidate - prop.GlobalPosition))
        { reason = "sweep-blocked"; return false; }
        if (!ClearVolume(prop, candidate, yaw))
        { reason = "endpoint-blocked"; return false; }
        point = candidate;
        reason = "accepted";
        return true;
    }

    // Seeing a thing through an opening does not mean its body fits through it.
    // Sweep backwards from the free held pose: a forward cast would ignore the
    // ground with which an authored resting prop already has contact. Only the
    // final shallow contact beneath its original base is permitted here.
    internal string LastPickupProbe { get; private set; } = string.Empty;

    private bool CanPullFromRest(CarryableProp prop, Vector3 heldFeet, float yaw, out string reason)
    {
        reason = string.Empty;
        if (_camera is null || _player is null) { reason = "no physical owner"; return false; }
        var sourceBasis = prop.GlobalBasis.Orthonormalized();
        var heldBasis = Basis.FromEuler(new(0, Mathf.DegToRad(yaw), 0));
        var sourceCentre = prop.GlobalTransform * (Vector3.Up * prop.Height * .5f);
        var liftedCentre = heldFeet + sourceBasis * (Vector3.Up * prop.Height * .5f);
        using var shape = new BoxShape3D { Size = prop.Size };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape, Transform = new(sourceBasis, liftedCentre), CollisionMask = 3u, Margin = .001f,
            Exclude = new global::Godot.Collections.Array<Rid> { prop.GetRid(), _player.GetRid() }
        };
        var space = _camera.GetWorld3D().DirectSpaceState;
        string Contacts() => string.Join(" | ", space.IntersectShape(query, 4).Select(hit =>
        {
            var body = hit["collider"].AsGodotObject() as Node;
            var owner = body is CollisionObject3D physical
                ? physical.ShapeOwnerGetOwner(physical.ShapeFindOwner(hit["shape"].AsInt32())) as Node : null;
            return $"{body?.GetPath()}/{owner?.Name}";
        }));
        bool SweepSegment(Vector3 start, Vector3 end, bool terminalSource, out string refusal)
        {
            refusal = string.Empty;
            query.Transform = new(sourceBasis, start);
            query.Motion = Vector3.Zero;
            if (space.IntersectShape(query, 1).Count != 0)
            { refusal = "segment start overlaps " + Contacts(); return false; }
            var motion = end - start;
            if (motion.LengthSquared() < .000001f) return true;
            query.Motion = motion;
            var fractions = space.CastMotion(query);
            if (fractions.Length < 2) { refusal = "cast returned no safe/unsafe fractions"; return false; }
            if (fractions[0] < .999f)
            {
                // The unchanged authored source may have millimetres of foot
                // contact, including the existing tilted service-path crate.
                // Only the final segment can end on that original support.
                var remaining = (1f - fractions[0]) * motion.Length();
                // Sample a bounded physical depth, not a fraction that becomes
                // microscopic on the four-centimetre unseating segment. This
                // reads the contact normal; it does not move the actual item.
                var contactFraction = Math.Min(1f, fractions[1] + .005f / motion.Length());
                query.Transform = new(sourceBasis, start + motion * contactFraction);
                query.Motion = Vector3.Zero;
                var contact = space.GetRestInfo(query);
                if (!terminalSource || remaining > .055f || contact.Count == 0 || contact["normal"].AsVector3().Y < SlopeLimit
                    || contact["point"].AsVector3().Y > sourceCentre.Y - prop.Height * .35f)
                {
                    var rest = contact.Count == 0 ? "none" : $"point={contact["point"].AsVector3()} normal={contact["normal"].AsVector3()}";
                    refusal = $"sweep safe={fractions[0]:F5} unsafe={fractions[1]:F5} remaining={remaining:F5}m rest={rest} overlaps=[{Contacts()}]";
                    return false;
                }
            }
            if (!terminalSource)
            {
                query.Transform = new(sourceBasis, end);
                query.Motion = Vector3.Zero;
                if (space.IntersectShape(query, 1).Count != 0)
                { refusal = "intermediate pose overlaps " + Contacts(); return false; }
            }
            return true;
        }
        reason = "direct";
        if (!SweepSegment(liftedCentre, sourceCentre, true, out var directRefusal))
        {
            // A loose log can lie below the projecting end of a stacked log.
            // Unseat its underside, draw it near its rest height, then lift.
            // Every full-volume segment retains every world collider; only the
            // short return to the original rest permits shallow underside contact.
            var releaseCentre = sourceCentre + Vector3.Up * .04f;
            var pullFeet = new Vector3(heldFeet.X, prop.GlobalPosition.Y + .04f, heldFeet.Z);
            var pullCentre = pullFeet + sourceBasis * (Vector3.Up * prop.Height * .5f);
            if (!SweepSegment(liftedCentre, pullCentre, false, out var liftRefusal))
            { reason = $"direct=[{directRefusal}] low-lift=[{liftRefusal}]"; return false; }
            if (!SweepSegment(pullCentre, releaseCentre, false, out var drawRefusal))
            { reason = $"direct=[{directRefusal}] low-draw=[{drawRefusal}]"; return false; }
            if (!SweepSegment(releaseCentre, sourceCentre, true, out var restRefusal))
            { reason = $"direct=[{directRefusal}] unseat=[{restRefusal}]"; return false; }
            reason = $"draw then lift via {pullFeet}";
        }
        // The untouched crate can follow a gentle authored slope. Check its
        // full turn upright at the free end, instead of flattening its source.
        var from = sourceBasis.GetRotationQuaternion();
        var to = heldBasis.GetRotationQuaternion();
        var steps = Math.Max(1, Mathf.CeilToInt(from.AngleTo(to) / Mathf.DegToRad(5f)));
        query.Motion = Vector3.Zero;
        for (var step = 1; step <= steps; step++)
        {
            var basis = new Basis(from.Slerp(to, (float)step / steps));
            query.Transform = new(basis, heldFeet + basis * (Vector3.Up * prop.Height * .5f));
            if (space.IntersectShape(query, 1).Count != 0)
            { reason = $"turn {step}/{steps} overlaps {Contacts()}"; return false; }
        }
        return true;
    }

    private Vector3[] HeldPoseCandidates(CarryableProp prop, Vector3 displacement)
    {
        var desired = DesiredHoldPoint(prop, displacement);
        var forward = (-_camera!.GlobalBasis.Z) with { Y = 0 };
        forward = forward.Normalized();
        var right = new Vector3(-forward.Z, 0, forward.X);
        return new[] { desired, desired - forward * .18f, desired - forward * .35f,
            desired + right * .22f, desired - right * .22f };
    }

    internal string LastBlockedStanceProbe { get; private set; } = string.Empty;

    internal bool TryMoveHeldForStanceChange(Vector3 cameraDisplacement)
    {
        LastBlockedStanceProbe = string.Empty;
        if (_held is null) return true;
        if (HasValidHeldPose && TryHeldPose(_held, cameraDisplacement, _held.YawDegrees, out var pose))
        {
            // Accept the swept pose in this same synchronous stance change.
            // Later movement/bob may need a different candidate; retaining
            // this approved height then keeps the object with the new stance.
            _held.HoldAt(pose);
            _heldOwnerFeetAtPose = _player!.GlobalPosition;
            return true;
        }
        LastBlockedStanceProbe = DescribeHeldSweep(cameraDisplacement);
        Feedback("Предмет задевает преграду. Сначала отойдите от неё, затем смените положение.");
        return false;
    }

    // Called for a refused stance or by the explicit movement proof. Record
    // every production candidate, including the retained pose, without moving
    // anything or creating progress.
    private string DescribeHeldSweep(Vector3 cameraDisplacement)
    {
        if (_held is null || _camera is null || _player is null) return "held sweep has no owner";
        var prop = _held;
        using var shape = new BoxShape3D { Size = prop.Size };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape, CollisionMask = 3u, Margin = .008f,
            Exclude = new global::Godot.Collections.Array<Rid> { prop.GetRid(), _player.GetRid() }
        };
        var space = _camera.GetWorld3D().DirectSpaceState;
        string Contacts(Vector3 feet)
        {
            query.Motion = Vector3.Zero;
            query.Transform = new(prop.GlobalBasis, feet + Vector3.Up * prop.Height * .5f);
            return string.Join(" | ", space.IntersectShape(query, 8).Select(hit =>
            {
                var body = hit["collider"].AsGodotObject() as Node;
                var owner = body is CollisionObject3D physical
                    ? physical.ShapeOwnerGetOwner(physical.ShapeFindOwner(hit["shape"].AsInt32())) as Node : null;
                return $"{body?.GetPath()}/{owner?.Name}";
            }));
        }
        var results = new List<string> { $"item={prop.ItemId} from={prop.GlobalPosition} cameraDelta={cameraDisplacement} start=[{Contacts(prop.GlobalPosition)}]" };
        void DescribeCandidate(string kind, Vector3 candidate)
        {
            var endpoint = Contacts(candidate);
            query.Transform = new(prop.GlobalBasis, prop.GlobalPosition + Vector3.Up * prop.Height * .5f);
            query.Motion = candidate - prop.GlobalPosition;
            var fractions = space.CastMotion(query);
            var fraction = fractions.Length >= 2 ? fractions[0] : 0f;
            var length = (candidate - prop.GlobalPosition).Length();
            var contactFraction = fractions.Length >= 2
                ? Math.Min(1f, fractions[1] + .005f / Math.Max(.000001f, length)) : fraction;
            var contact = fraction < .999f
                ? Contacts(prop.GlobalPosition.Lerp(candidate, contactFraction)) : string.Empty;
            results.Add($"kind={kind} to={candidate} endpoint=[{endpoint}] swept={fraction:F4} contact=[{contact}]");
        }
        foreach (var candidate in HeldPoseCandidates(prop, cameraDisplacement))
            DescribeCandidate("camera", candidate);
        var retained = prop.GlobalPosition + (_player.GlobalPosition - _heldOwnerFeetAtPose) + cameraDisplacement;
        var offset = retained - (_player.GlobalPosition + cameraDisplacement);
        var retainedAccepted = TryRetainedHeldTranslation(prop, cameraDisplacement, prop.YawDegrees, out _, out var retainedReason);
        results.Add($"retained accepted={retainedAccepted} reason={retainedReason} ownerAnchor={_heldOwnerFeetAtPose} "
            + $"ownerFeet={_player.GlobalPosition} horizontalReach={new Vector2(offset.X, offset.Z).Length():F5}/{prop.HoldDistance + .25f:F5} "
            + $"heightDifference={Mathf.Abs(retained.Y - DesiredHoldPoint(prop, cameraDisplacement).Y):F5}/0.25000");
        DescribeCandidate("retained", retained);
        return string.Join("; ", results);
    }

    internal string DescribeBlockedHeldMovement(Vector3 requestedVelocity, float physicsDelta)
    {
        if (_held is null || _camera is null || _player is null) return "held movement has no owner";
        if (!float.IsFinite(physicsDelta) || physicsDelta <= 0
            || !float.IsFinite(requestedVelocity.X) || !float.IsFinite(requestedVelocity.Z))
            return "held movement probe received invalid motion";
        var move = new Vector3(requestedVelocity.X * physicsDelta, 0, requestedVelocity.Z * physicsDelta);
        var constrained = ConstrainCarriedMovement(requestedVelocity, physicsDelta);
        return $"requested={requestedVelocity} constrained={constrained} valid={_heldPoseValid} "
            + $"revision={_heldPlayerTransformRevision}/{_player.PresentationTransformRevision}; "
            + "full=[" + DescribeHeldSweep(move) + "]; "
            + "x=[" + DescribeHeldSweep(new(move.X, 0, 0)) + "]; "
            + "z=[" + DescribeHeldSweep(new(0, 0, move.Z)) + "]";
    }

    internal bool CanCarryThroughStep(Vector3 up, Vector3 forward, Vector3 down)
    {
        if (_held is null) return true;
        if (!_heldPoseValid || _player is null
            || _heldPlayerTransformRevision != _player.PresentationTransformRevision) return false;
        var start = _held.GlobalPosition;
        return ClearVolume(_held, start, _held.YawDegrees, up)
            && ClearVolume(_held, start + up, _held.YawDegrees, forward)
            && ClearVolume(_held, start + up + forward, _held.YawDegrees, down);
    }

    private bool CanRotateHeld(CarryableProp prop, float yaw)
    {
        if (!TryHeldPose(prop, Vector3.Zero, yaw, out var candidate)) return false;
        // Check intermediate orientations as well as the final 15-degree step.
        for (var fraction = 1; fraction <= 5; fraction++)
        {
            var angle = prop.YawDegrees + 15f * fraction / 5f;
            if (!ClearVolume(prop, candidate, angle)) return false;
        }
        return true;
    }

    internal Vector3 ConstrainCarriedMovement(Vector3 velocity, float delta)
    {
        if (_held is null || !_heldPoseValid || _camera is null || _player is null
            || _heldPlayerTransformRevision != _player.PresentationTransformRevision) return velocity;
        var move = new Vector3(velocity.X * delta, 0, velocity.Z * delta);
        if (move.LengthSquared() < .000001f || TryHeldPose(_held, move, _held.YawDegrees, out _)) return velocity;
        // Slide along the obstacle where one horizontal axis is still clear.
        var x = TryHeldPose(_held, new(move.X, 0, 0), _held.YawDegrees, out _) ? velocity.X : 0;
        var z = TryHeldPose(_held, new(0, 0, move.Z), _held.YawDegrees, out _) ? velocity.Z : 0;
        if (x != 0 && z != 0) z = 0;
        return new(x, velocity.Y, z);
    }

    internal bool IsSupportingSomething(CarryableProp support)
    {
        if (_player is null || _camera is null) return false;
        for (var i = 0; i < _player.GetSlideCollisionCount(); i++)
        {
            var collision = _player.GetSlideCollision(i);
            if (collision.GetCollider() == support && collision.GetNormal().Y > .5f) return true;
        }
        var underFeet = Trace(_player.GlobalPosition + Vector3.Up * .04f,
            _player.GlobalPosition - Vector3.Up * .10f);
        if (underFeet.Count > 0 && underFeet["collider"].AsGodotObject() == support) return true;
        foreach (var item in _props)
        {
            if (item == support || !item.IsVisibleInTree()
                || item.State is not (CarryableProp.CarryState.World or CarryableProp.CarryState.Placed or CarryableProp.CarryState.Combined)) continue;
            // A board rests on its ends, so a ray under its centre misses a
            // support that the player can otherwise remove from underneath it.
            // Inspect the shallow footprint below the whole physical base;
            // the inset excludes mere side contact with a neighbouring thing.
            using var footprint = new BoxShape3D
            { Size = new(Math.Max(.01f, item.Size.X - .014f), .087f, Math.Max(.01f, item.Size.Z - .014f)) };
            using var query = new PhysicsShapeQueryParameters3D
            {
                Shape = footprint,
                Transform = new(Basis.FromEuler(new(0, Mathf.DegToRad(item.YawDegrees), 0)),
                    item.GlobalPosition - Vector3.Up * .0475f),
                CollisionMask = 3u, Margin = .001f,
                Exclude = new global::Godot.Collections.Array<Rid> { item.GetRid(), _player.GetRid() }
            };
            foreach (var hit in _camera.GetWorld3D().DirectSpaceState.IntersectShape(query, 32))
                if (hit["collider"].AsGodotObject() == support) return true;
        }
        return false;
    }
}
