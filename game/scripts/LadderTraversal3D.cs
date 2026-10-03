using Godot;

namespace Urman.Godot;

/// <summary>
/// A fixed, source-matched ladder. Progress is earned by actual capsule motion;
/// no pose assignment, gravity override outside traversal, or invisible lift.
/// The controller owns input and pauses ordinary walking while IsTraversing.
/// </summary>
public partial class LadderTraversal3D : Node3D
{
    public const string GroupName = "act1-fixed-ladders";
    public Vector3 LowerLanding { get; set; } = new(0, .04f, 3.92f);
    public Vector3 LowerGrip { get; set; } = new(0, .04f, 3.75f);
    public Vector3 UpperGrip { get; set; } = new(0, 1.50f, 2.65f);
    public Vector3 UpperLanding { get; set; } = new(0, 1.50f, 1.45f);
    public float ClimbSpeed { get; set; } = .72f;
    public FirstPersonController? Player { get; private set; }
    public bool IsTraversing => Player is not null;
    public bool IsReturning => _returnDirection != 0;
    public string LastFailure { get; private set; } = string.Empty;
    public string DisplayPrompt => IsReturning ? "Возвращаемся на площадку"
        : IsTraversing ? "Вверх / вниз — двигаться по лестнице; отмена — вернуться"
        : "Взяться за лестницу";
    public float DistanceTravelled { get; private set; }
    public Func<bool>? EntryAllowed { get; set; }

    private Vector3[] _path = [];
    private float[] _distances = [];
    private float _progress;
    private float _initialProgress;
    private bool _joining;
    private int _returnDirection;

    public override void _Ready() => AddToGroup(GroupName);

    public bool CanOffer(FirstPersonController player)
    {
        if (!IsInsideTree() || !IsVisibleInTree() || IsTraversing || EntryAllowed?.Invoke() == false) return false;
        return Near(player.GlobalPosition, ToGlobal(LowerLanding)) || Near(player.GlobalPosition, ToGlobal(UpperLanding));
    }

    public bool TryBegin(FirstPersonController player)
    {
        if (IsTraversing) return Player == player;
        if (!CanOffer(player)) return Fail("Подойдите к свободной площадке лестницы.");
        if (player.IsCrouching) return Fail("Перед лестницей нужно встать в полный рост.");
        if ((GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator)?.HeldItem is not null)
            return Fail("Поставьте предмет: на лестнице нужны свободные руки.");
        _path = [ToGlobal(LowerLanding), ToGlobal(LowerGrip), ToGlobal(UpperGrip), ToGlobal(UpperLanding)];
        _distances = new float[_path.Length];
        for (var i = 1; i < _path.Length; i++)
            _distances[i] = _distances[i - 1] + _path[i].DistanceTo(_path[i - 1]);
        if (_distances[^1] < .3f) return Fail("У лестницы нет безопасного пути между площадками.");
        var fromTop = player.GlobalPosition.DistanceSquaredTo(_path[^1]) < player.GlobalPosition.DistanceSquaredTo(_path[0]);
        var endpoint = fromTop ? _path[^1] : _path[0];
        if (!HasSupport(player, _path[0]) || !HasSupport(player, _path[^1]))
            return Fail("У одной из площадок нет устойчивой опоры.");
        if (!player.CanStandAt(_path[0]) || !player.CanStandAt(_path[^1]))
            return Fail("На площадке недостаточно места, чтобы сойти с лестницы.");
        if (!ClearSegment(player, player.GlobalPosition, endpoint))
            return Fail("Что-то мешает подойти к лестнице.");
        for (var i = 1; i < _path.Length; i++)
            if (!ClearSegment(player, _path[i - 1], _path[i]))
                return Fail("Проход по лестнице занят.");
        Player = player;
        _progress = _initialProgress = fromTop ? _distances[^1] : 0;
        _joining = player.GlobalPosition.DistanceTo(endpoint) > .005f;
        _returnDirection = 0;
        DistanceTravelled = 0;
        LastFailure = string.Empty;
        player.Velocity = Vector3.Zero;
        return true;
    }

    /// <returns>True while the controller must leave movement to this ladder.</returns>
    public bool PhysicsTick(double delta, float direction)
    {
        if (Player is not { } player) return false;
        if (!GodotObject.IsInstanceValid(player)) { Player = null; return false; }
        player.Velocity = Vector3.Zero;
        var budget = ClimbSpeed * (float)Math.Clamp(delta, 0, 1d / 15);
        if (budget <= 0) return true;
        if (_joining)
        {
            var endpoint = _initialProgress == 0 ? _path[0] : _path[^1];
            if (!MoveTowards(player, endpoint, budget, out _)) return true;
            _joining = false;
            if (_returnDirection != 0) FinishAtLanding(player);
            return IsTraversing;
        }
        var sign = _returnDirection != 0 ? _returnDirection : Math.Sign(Math.Abs(direction) > .15f ? direction : 0);
        if (sign == 0) return true;
        if (sign < 0 && _progress <= .006f || sign > 0 && _progress >= _distances[^1] - .006f)
        {
            FinishAtLanding(player);
            return IsTraversing;
        }
        var knot = sign > 0
            ? _distances.First(value => value > _progress + .001f)
            : _distances.Last(value => value < _progress - .001f);
        var next = sign > 0 ? Math.Min(knot, _progress + budget) : Math.Max(knot, _progress - budget);
        var point = PointAt(next);
        var before = player.GlobalPosition;
        MoveTowards(player, point, budget, out var arrived);
        var intended = point - before;
        if (intended.LengthSquared() > .000001f)
        {
            // Collision recovery is not forward progress: only motion along
            // this segment advances the traversal contract.
            var earned = Math.Max(0, (player.GlobalPosition - before).Dot(intended.Normalized()));
            _progress = arrived ? next : Mathf.Clamp(_progress + sign * earned, 0, _distances[^1]);
        }
        if (_progress <= .006f && sign < 0 || _progress >= _distances[^1] - .006f && sign > 0)
            FinishAtLanding(player);
        return IsTraversing;
    }

    /// <summary>Request a physical return; it completes in PhysicsTick.</summary>
    public bool Cancel()
    {
        if (Player is not { } player) return true;
        if (_joining)
        {
            _returnDirection = _initialProgress == 0 ? -1 : 1;
            return true;
        }
        var nearest = _progress <= _distances[^1] * .5f ? -1 : 1;
        foreach (var direction in new[] { nearest, -nearest })
        {
            var landing = direction < 0 ? _path[0] : _path[^1];
            if (!HasSupport(player, landing) || !player.CanStandAt(landing)) continue;
            var from = player.GlobalPosition;
            var points = direction < 0
                ? _path.Where((_, index) => _distances[index] < _progress).Reverse()
                : _path.Where((_, index) => _distances[index] > _progress);
            var clear = true;
            foreach (var point in points)
            {
                if (!ClearSegment(player, from, point)) { clear = false; break; }
                from = point;
            }
            if (!clear) continue;
            _returnDirection = direction;
            LastFailure = string.Empty;
            return true;
        }
        return Fail("Путь к площадкам занят. Держитесь за лестницу и освободите выход.");
    }

    /// <summary>Called only by an authorized load/new-game/zone placement.</summary>
    public void ReleaseForWorldPlacement(FirstPersonController player)
    {
        if (Player != player) return;
        Player = null;
        _joining = false;
        _returnDirection = 0;
        LastFailure = string.Empty;
    }

    private bool MoveTowards(FirstPersonController player, Vector3 target, float budget, out bool arrived)
    {
        var distance = target.DistanceTo(player.GlobalPosition);
        if (distance < .006f) { arrived = true; return true; }
        var motion = (target - player.GlobalPosition).Normalized() * Math.Min(distance, budget);
        var before = player.GlobalPosition;
        var collision = player.MoveAndCollide(motion);
        var moved = player.GlobalPosition.DistanceTo(before);
        DistanceTravelled += moved;
        arrived = player.GlobalPosition.DistanceTo(target) < .006f;
        if (collision is not null && moved < Math.Min(.003f, motion.Length() * .2f))
        {
            LastFailure = "Дальше мешает препятствие. Можно спуститься или вернуться.";
            return false;
        }
        LastFailure = string.Empty;
        return arrived;
    }

    private void FinishAtLanding(FirstPersonController player)
    {
        var endpoint = _progress < _distances[^1] * .5f ? _path[0] : _path[^1];
        if (player.GlobalPosition.DistanceTo(endpoint) > .025f || !HasSupport(player, endpoint)
            || !player.CanStandAt(player.GlobalPosition))
        { Fail("Площадка занята. Держитесь за лестницу или вернитесь."); return; }
        player.Velocity = Vector3.Zero;
        Player = null;
        _returnDirection = 0;
        _joining = false;
        LastFailure = string.Empty;
    }

    private Vector3 PointAt(float progress)
    {
        for (var i = 1; i < _path.Length; i++)
            if (progress <= _distances[i])
                return _path[i - 1].Lerp(_path[i], (progress - _distances[i - 1]) / (_distances[i] - _distances[i - 1]));
        return _path[^1];
    }

    private static bool ClearSegment(FirstPersonController player, Vector3 from, Vector3 to)
    {
        var capsule = (CapsuleShape3D)player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        using var queryCapsule = new CapsuleShape3D { Radius = capsule.Radius, Height = capsule.Height - .015f };
        var ladderExclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        using var ladderExcludeOwner = (global::Godot.Collections.Array)ladderExclude;
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = queryCapsule, Transform = new(Basis.Identity, from + Vector3.Up * (capsule.Height * .5f + .01f)),
            Motion = to - from, CollisionMask = player.CollisionMask,
            Exclude = ladderExclude, Margin = .002f
        };
        var space = player.GetWorld3D().DirectSpaceState;
        var startOverlap = space.IntersectShape(query, 1);
        using var startOverlapOwner = (global::Godot.Collections.Array)startOverlap;
        if (startOverlap.Count != 0) return false;
        var fractions = space.CastMotion(query);
        if (fractions.Length != 2 || fractions[0] < .999f) return false;
        query.Transform = new(Basis.Identity, to + Vector3.Up * (capsule.Height * .5f + .01f));
        query.Motion = Vector3.Zero;
        var endOverlap = space.IntersectShape(query, 1);
        using var endOverlapOwner = (global::Godot.Collections.Array)endOverlap;
        return endOverlap.Count == 0;
    }

    private static bool HasSupport(FirstPersonController player, Vector3 feet)
    {
        using var ray = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * .09f, feet - Vector3.Up * .18f, player.CollisionMask);
        var supportExclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        using var supportExcludeOwner = (global::Godot.Collections.Array)supportExclude;
        ray.Exclude = supportExclude;
        using var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        return hit.Count > 0 && hit["normal"].AsVector3().Y > .65f
            && Math.Abs(hit["position"].AsVector3().Y - feet.Y) < .13f;
    }

    private static bool Near(Vector3 player, Vector3 landing) => Math.Abs(player.Y - landing.Y) < .30f
        && new Vector2(player.X, player.Z).DistanceTo(new(landing.X, landing.Z)) <= 1.05f;
    private bool Fail(string message) { LastFailure = message; return false; }
}
