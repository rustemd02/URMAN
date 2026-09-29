using System.Text.Json;
using Godot;
using Urman.Core.Narrative;

namespace Urman.Godot;

/// <summary>
/// Daily routines of authored characters (URMAN Studio, spec NPC02, NPC03, A13).
/// Act I has no free-running clock: a routine block's "time" is a story
/// condition over the one runtime state (a story moment, a quest), so the
/// first block whose condition holds is where the character is. The character
/// walks its drawn route there and then does the block's activity. A route
/// that is blocked never leaves the story waiting: the block's own policy
/// decides (go straight to the place, or stay and say why). A scene or quest
/// step may claim the character; the routine yields and resumes from wherever
/// the scene left them (CINE09: a second claimant is refused with the holder's name).
/// </summary>
public partial class AuthoredWorldDirector
{
    private const float RouteProbeHeight = .9f;
    private const uint RouteProbeMask = 1u;
    private readonly Dictionary<string, RoutineRun> _routines = new(StringComparer.Ordinal);

    /// <summary>A route point: where, how long to stand there, and what to look at while standing.</summary>
    private readonly record struct Waypoint(Vector3 At, float Wait, Vector3? Look, bool LookAtPlayer);

    private sealed class RoutineRun
    {
        public required AuthoredObject Item;
        public required Node3D Character;
        public string? BlockId;
        public JsonElement Block;
        public bool NeedsPlan;
        public readonly List<Waypoint> Route = [];
        public int Next;
        public float WaitLeft;
        public bool Following;
        public string? Claim;
        public string Status = "";
    }

    /// <summary>The routine block the character is in (null = no routine), and what happened on the way.</summary>
    public (string? Block, string Status, bool Walking) RoutineStatus(string id) =>
        _routines.TryGetValue(id, out var run) ? (run.BlockId, run.Status, run.Next < run.Route.Count || run.Following) : (null, "", false);

    public string? ClaimedBy(string npc) => Routine(npc)?.Claim;

    /// <summary>Studio's still editor world can let routines run so the author sees them (the game always runs them).</summary>
    public bool RoutinesLive
    {
        get => ProcessMode == ProcessModeEnum.Always;
        set => ProcessMode = value ? ProcessModeEnum.Always : ProcessModeEnum.Inherit;
    }

    /// <summary>A scene or quest step takes the character; false with the holder when another owner already has them.</summary>
    public bool Claim(string npc, string owner, out string? heldBy)
    {
        heldBy = null;
        if (Routine(npc) is not { } run) return true; // no routine: nothing to yield
        if (run.Claim is { } holder && holder != owner)
        {
            heldBy = holder;
            GD.PushWarning($"authored-world: {run.Item.Id} is already taken by {holder}; {owner} was refused.");
            return false;
        }

        run.Claim = owner;
        run.Route.Clear();
        run.Next = 0;
        run.Following = false;
        run.Status = $"занят: {owner}";
        return true;
    }

    /// <summary>The scene is over: the routine resumes from where the scene left the character.</summary>
    public void Release(string npc, string owner)
    {
        if (Routine(npc) is not { } run || run.Claim != owner) return;
        run.Claim = null;
        run.BlockId = null; // re-plan the current block from the new position
        ApplyRoutines();
    }

    // Accepts the placed object's ID or the character ID it shows.
    private RoutineRun? Routine(string npc) =>
        _routines.TryGetValue(npc, out var direct) ? direct
        : _routines.Values.FirstOrDefault(run => run.Item.Params.TryGetProperty("characterId", out var character) && character.GetString() == npc);

    private void RegisterRoutine(AuthoredObject item, Node3D character)
    {
        _routines.Remove(item.Id);
        if (item.Params.TryGetProperty("schedule", out var schedule) && schedule.ValueKind == JsonValueKind.Array && schedule.GetArrayLength() > 0)
        {
            _routines[item.Id] = new RoutineRun { Item = item, Character = character };
        }
    }

    private void ApplyRoutines()
    {
        if (_bridge?.SelectRuntimeState() is not { ValueKind: JsonValueKind.Object } state) return;
        foreach (var run in _routines.Values)
        {
            if (run.Claim is not null || !run.Item.Root.IsInsideTree()) continue;
            JsonElement? chosen = null;
            foreach (var block in run.Item.Params.GetProperty("schedule").EnumerateArray())
            {
                if (!block.TryGetProperty("when", out var when) || ContentRuleEngine.EvaluateAll(when, state))
                {
                    chosen = block;
                    break;
                }
            }

            var blockId = chosen?.GetProperty("id").GetString();
            if (blockId == run.BlockId) continue;
            run.BlockId = blockId;
            run.Route.Clear();
            run.Next = 0;
            run.Following = false;
            if (chosen is not { } next)
            {
                run.Status = "ни один блок распорядка не подходит — стоит на исходном месте";
                continue;
            }

            run.Block = next;
            run.NeedsPlan = true; // routes are probed inside the physics step
        }
    }

    private void StepRoutines(float delta)
    {
        Node3D? player = null;
        foreach (var run in _routines.Values)
        {
            if (run.Claim is not null) continue;
            if (run.NeedsPlan)
            {
                run.NeedsPlan = false;
                Plan(run);
            }

            var speed = run.Block.ValueKind == JsonValueKind.Object && run.Block.TryGetProperty("speed", out var speedValue) ? speedValue.GetSingle() : 1.3f;
            if (run.Following)
            {
                player ??= GetTree().GetFirstNodeInGroup("player_controller") as Node3D;
                Follow(run, player, speed * 1.4f, delta);
                continue;
            }

            if (run.Next >= run.Route.Count) continue;
            var root = run.Item.Root;
            var waypoint = run.Route[run.Next];
            if (run.WaitLeft > 0)
            {
                // Standing at a route point: face what the author chose.
                run.WaitLeft -= delta;
                player ??= GetTree().GetFirstNodeInGroup("player_controller") as Node3D;
                if ((waypoint.LookAtPlayer ? player?.GlobalPosition : waypoint.Look) is { } look) Face(root, look);
                if (run.WaitLeft <= 0) Advance(run);
                continue;
            }

            if (MoveTowards(root, waypoint.At, speed * delta))
            {
                if (waypoint.Wait > 0)
                {
                    run.WaitLeft = waypoint.Wait;
                    run.Status = $"ждёт {waypoint.Wait:0.#} с";
                    AnimationCatalog.Play(run.Character, "urman.anim:idle");
                }
                else
                {
                    Advance(run);
                }
            }
        }
    }

    private void Advance(RoutineRun run)
    {
        if (++run.Next >= run.Route.Count)
        {
            Arrive(run);
            return;
        }

        run.Status = "идёт";
        AnimationCatalog.Play(run.Character, Text(run.Block, "walkMotion", "urman.anim:walk"));
    }

    // Escort: keep near the player; a player who runs far away is waited for, never chased across the map.
    private void Follow(RoutineRun run, Node3D? player, float speed, float delta)
    {
        if (player is null) return;
        var follow = run.Block.GetProperty("follow");
        var distance = follow.TryGetProperty("distance", out var d) ? d.GetSingle() : 2.5f;
        var lose = follow.TryGetProperty("loseMetres", out var l) ? l.GetSingle() : 30f;
        var root = run.Item.Root;
        var gap = new Vector2(player.GlobalPosition.X - root.GlobalPosition.X, player.GlobalPosition.Z - root.GlobalPosition.Z).Length();
        if (gap > lose)
        {
            if (run.Status.StartsWith("отстал", StringComparison.Ordinal)) return;
            run.Status = $"отстал от игрока ({gap:0} м) — ждёт на месте";
            AnimationCatalog.Play(run.Character, "urman.anim:idle");
            return;
        }

        if (gap <= distance)
        {
            Face(root, player.GlobalPosition);
            if (run.Status != "рядом с игроком") AnimationCatalog.Play(run.Character, Text(run.Block, "motion", "urman.anim:idle"));
            run.Status = "рядом с игроком";
            return;
        }

        if (run.Status != "идёт за игроком") AnimationCatalog.Play(run.Character, Text(run.Block, "walkMotion", "urman.anim:walk"));
        run.Status = "идёт за игроком";
        var target = player.GlobalPosition - (player.GlobalPosition - root.GlobalPosition).Normalized() * distance;
        MoveTowards(root, root.GetParent<Node3D>().ToLocal(target), speed * delta);
    }

    /// <summary>One walking step on the ground; true when the point is reached.</summary>
    private static bool MoveTowards(Node3D root, Vector3 target, float step)
    {
        var flat = new Vector3(target.X - root.Position.X, 0, target.Z - root.Position.Z);
        if (flat.Length() <= step)
        {
            root.Position = Ground(new Vector3(target.X, 0, target.Z));
            return true;
        }

        var direction = flat.Normalized();
        var at = root.Position + direction * step;
        root.Position = Ground(new Vector3(at.X, 0, at.Z));
        root.Rotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0);
        return false;
    }

    private static void Face(Node3D root, Vector3 look)
    {
        var flat = new Vector2(look.X - root.GlobalPosition.X, look.Z - root.GlobalPosition.Z);
        if (flat.Length() > .05f) root.Rotation = new Vector3(0, Mathf.Atan2(flat.X, flat.Y), 0);
    }

    private void Plan(RoutineRun run)
    {
        var block = run.Block;
        if (block.TryGetProperty("follow", out _))
        {
            run.Following = true;
            run.Status = "идёт за игроком";
            return;
        }

        var place = Ground(ReadVector(block, "place"));
        var points = new List<Waypoint>();
        if (block.TryGetProperty("route", out var route))
        {
            points.AddRange(route.EnumerateArray().Select(ReadWaypoint));
        }

        points.Add(new Waypoint(place, 0, null, false));
        var from = run.Item.Root.Position;
        var blockedAt = (Vector3?)null;
        foreach (var point in points)
        {
            if (FirstObstacle(from, point.At) is { } hit)
            {
                blockedAt = hit;
                break;
            }

            from = point.At;
        }

        if (blockedAt is { } obstacle)
        {
            var policy = Text(block, "onBlocked", "go-directly");
            var where = $"x {obstacle.X:0.#}, z {obstacle.Z:0.#}";
            GD.PushWarning($"authored-world: {run.Item.Id} route to {run.BlockId} blocked at {where}; policy {policy}.");
            switch (policy)
            {
                case "stay":
                    run.Status = $"путь перекрыт у {where} — остался на месте";
                    return;
                case "safe-point" when block.TryGetProperty("safePoint", out _):
                    run.Item.Root.Position = Ground(ReadVector(block, "safePoint"));
                    run.Status = $"путь перекрыт у {where} — ушёл в безопасную точку";
                    Arrive(run, faceBlock: false);
                    return;
                default:
                    // The story must not wait on a path: the character is simply there.
                    run.Item.Root.Position = place;
                    run.Status = $"путь перекрыт у {where} — сразу на месте";
                    Arrive(run);
                    return;
            }
        }

        run.Route.AddRange(points.SkipWhile(point => point.Wait <= 0 && point.At.DistanceTo(run.Item.Root.Position) < .05f));
        run.Next = 0;
        run.WaitLeft = 0;
        if (run.Route.Count == 0)
        {
            Arrive(run);
            return;
        }

        run.Status = "идёт";
        AnimationCatalog.Play(run.Character, Text(block, "walkMotion", "urman.anim:walk"));
    }

    // A route point is [x, y, z] or { "at": [x, y, z], "waitSeconds": 3, "look": [x, y, z] | "player" }.
    private static Waypoint ReadWaypoint(JsonElement point)
    {
        if (point.ValueKind == JsonValueKind.Array) return new(Ground(AuthoredWorldPlot.ToVector3(point)), 0, null, false);
        var wait = point.TryGetProperty("waitSeconds", out var seconds) ? seconds.GetSingle() : 0f;
        var hasLook = point.TryGetProperty("look", out var look);
        var atPlayer = hasLook && look.ValueKind == JsonValueKind.String && look.GetString() == "player";
        Vector3? lookAt = hasLook && look.ValueKind == JsonValueKind.Array ? Ground(AuthoredWorldPlot.ToVector3(look)) : null;
        return new(Ground(ReadVector(point, "at")), wait, lookAt, atPlayer);
    }

    private void Arrive(RoutineRun run, bool faceBlock = true)
    {
        run.Route.Clear();
        run.Next = 0;
        run.WaitLeft = 0;
        var block = run.Block;
        if (faceBlock && block.TryGetProperty("yawDegrees", out var yaw)) run.Item.Root.RotationDegrees = new Vector3(0, yaw.GetSingle(), 0);
        var motion = Text(block, "motion", "urman.anim:idle");
        var played = AnimationCatalog.Play(run.Character, motion);
        if (!run.Status.StartsWith("путь перекрыт", StringComparison.Ordinal)) run.Status = "на месте";
        if (!played.Played) run.Status += $"; занятие не проиграно: {played.Problem}";
        run.Item.Root.SetMeta("routineBlock", run.BlockId ?? "");
    }

    /// <summary>First solid thing between two ground points at knee-to-chest height, sampled along the ground.</summary>
    private Vector3? FirstObstacle(Vector3 from, Vector3 to)
    {
        var space = GetWorld3D().DirectSpaceState;
        var flat = new Vector2(to.X - from.X, to.Z - from.Z);
        var steps = Math.Max(1, (int)Mathf.Ceil(flat.Length()));
        var previous = from + Vector3.Up * RouteProbeHeight;
        for (var index = 1; index <= steps; index++)
        {
            var t = index / (float)steps;
            var ground = Ground(new Vector3(Mathf.Lerp(from.X, to.X, t), 0, Mathf.Lerp(from.Z, to.Z, t)));
            var point = ground + Vector3.Up * RouteProbeHeight;
            var query = PhysicsRayQueryParameters3D.Create(previous, point, RouteProbeMask);
            query.CollideWithAreas = false;
            var hit = space.IntersectRay(query);
            if (hit.Count > 0) return (Vector3)hit["position"];
            previous = point;
        }

        return null;
    }
}
