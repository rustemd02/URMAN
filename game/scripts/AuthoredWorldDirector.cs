using System.Text.Json;
using Godot;
using Urman.Core.Narrative;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Executes authored world plots whose <c>executor</c> is <c>generic</c>
/// (URMAN Studio's placeable content): props from the catalogue, characters,
/// pickups and trigger areas, each built from its stable ID. Story states of an
/// object ("gate closed / broken / repaired") are conditions over the one
/// runtime state; after every committed transaction the first matching state
/// is applied to visibility, collision and position together (spec STATE01,
/// STATE04). RuntimeBridge stays the only owner of narrative state: triggers
/// and pickups only dispatch compiled interactions.
/// </summary>
public partial class AuthoredWorldDirector : Node3D
{
    private static readonly System.Text.RegularExpressions.Regex YardFenceMesh =
        new(@"_Yard_(Post|\w*Rail|Gate|Picket|Fence|Paling|Board|Plank|Threshold|FoundationStone)", System.Text.RegularExpressions.RegexOptions.Compiled);

    public const string WorldDirectory = "res://content/world";
    public const string CatalogPath = "res://content/world/catalog.v1.json";

    /// <summary>Test-only: world plots from a disposable Studio workspace instead of res://content/world.</summary>
    internal static string? WorldDirectoryOverrideForTest { get; set; }

    private static string PlotDirectory => WorldDirectoryOverrideForTest ?? WorldDirectory;

    private readonly Dictionary<string, AuthoredObject> _objects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _catalog = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PackedScene> _scenes = new(StringComparer.Ordinal);
    private RuntimeBridge? _bridge;
    private Node _interactionHost = null!;
    // Number of objects whose PendingState is currently non-null. Rebuilt from
    // scratch on every ApplyStates pass, which is the only place PendingState is
    // written (set at the overlap guard, cleared when the state is committed).
    private int _pendingStates;

    private sealed class AuthoredObject
    {
        public required string Id;
        public required string Kind;
        public required Node3D Root;
        public required JsonElement Params;
        public CollisionObject3D? Body;
        public uint BodyLayer;
        public Area3D? Area;
        public InteractionTarget? Target;
        public Vector3 BasePosition;
        public float BaseYaw;
        public string? AppliedState;
        public string? PendingState;
        public readonly HashSet<ulong> Inside = [];
        public bool FiredThisRun;
    }

    public IReadOnlyCollection<string> ObjectIds => _objects.Keys;
    public string? AppliedState(string id) => _objects.TryGetValue(id, out var item) ? item.AppliedState : null;
    public Node3D? ObjectRoot(string id) => _objects.TryGetValue(id, out var item) ? item.Root : null;

    public static AuthoredWorldDirector? Current(SceneTree tree) => tree.GetFirstNodeInGroup("authored_world_director") as AuthoredWorldDirector;

    public static AuthoredWorldDirector Build(Node3D parent, Node interactionHost)
    {
        var director = new AuthoredWorldDirector { Name = "AuthoredWorldDirector" };
        parent.AddChild(director);
        director.BuildContent(interactionHost);
        return director;
    }

    private void BuildContent(Node interactionHost)
    {
        _interactionHost = interactionHost;
        AddToGroup("authored_world_director");
        SetMeta("runtimeStateOwner", "none: reads RuntimeBridge state, dispatches compiled interactions");
        if (global::Godot.FileAccess.FileExists(CatalogPath))
        {
            using var catalog = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(CatalogPath));
            foreach (var entry in catalog.RootElement.GetProperty("entities").EnumerateArray())
            {
                _catalog[entry.GetProperty("id").GetString()!] = entry.Clone();
            }
        }

        foreach (var file in DirAccess.GetFilesAt(PlotDirectory).Where(file => file.EndsWith(".world.v1.json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString($"{PlotDirectory}/{file}"));
            var root = document.RootElement;
            if (!root.TryGetProperty("executor", out var executor) || executor.GetString() != "generic")
            {
                continue; // bespoke plots (Tamara's fence) are executed by their own node
            }

            foreach (var entity in root.GetProperty("entities").EnumerateArray())
            {
                BuildObject(entity.Clone());
            }
        }

        GD.Print($"authored-world: {_objects.Count} objects from {PlotDirectory}");
    }

    public override void _Ready()
    {
        _bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (_bridge is not null)
        {
            _bridge.RuntimeStateChanged += ApplyStates;
        }

        CallDeferred(nameof(ApplyStates));
    }

    public override void _ExitTree()
    {
        if (_bridge is not null) _bridge.RuntimeStateChanged -= ApplyStates;
    }

    public override void _PhysicsProcess(double delta)
    {
        // A state held back because the player stood where its collision
        // would appear is applied as soon as they step away (STATE05).
        StepAmbientPresence(delta);
        StepGreetings(delta);
        StepRoutines((float)delta);
        // Same "is there at least one held-back state" test as the previous LINQ
        // scan; ApplyStates itself clears PendingState, so a stale non-zero count
        // can only keep re-running the pass, never hide a pending state.
        if (_pendingStates > 0) ApplyStates();
    }

    /// <summary>
    /// URMAN Studio preview: rebuild one object from edited (possibly unsaved)
    /// data, or remove it. Presentation only; the saved file stays the source
    /// the game loads.
    /// </summary>
    public void PreviewUpsert(string entityJson)
    {
        using var document = JsonDocument.Parse(entityJson);
        var entity = document.RootElement.Clone();
        PreviewRemove(entity.GetProperty("id").GetString()!);
        BuildObject(entity);
        _objects[entity.GetProperty("id").GetString()!].AppliedState = null;
        ApplyStates();
    }

    public void PreviewRemove(string id)
    {
        _routines.Remove(id);
        _ambientVisits.RemoveAll(visit => visit.Item.Id == id);
        if (!_objects.Remove(id, out var item)) return;
        item.Target?.QueueFree();
        item.Root.QueueFree();
    }

    // ---- building --------------------------------------------------------------

    private void BuildObject(JsonElement entity)
    {
        var id = entity.GetProperty("id").GetString()!;
        var kind = entity.GetProperty("kind").GetString()!;
        var parameters = entity.GetProperty("params");
        var position = ReadVector(parameters, "position");
        var yaw = parameters.TryGetProperty("yawDegrees", out var yawValue) ? yawValue.GetSingle() : 0f;
        var root = new Node3D { Name = SafeName(id), Position = Ground(position), RotationDegrees = new Vector3(0, yaw, 0) };
        root.SetMeta(AuthoredWorldPlot.AuthoredIdMeta, id);
        AddChild(root);
        var item = new AuthoredObject { Id = id, Kind = kind, Root = root, Params = parameters, BasePosition = root.Position, BaseYaw = yaw };
        switch (kind)
        {
            case "prop":
                BuildVisual(item, parameters.GetProperty("catalogId").GetString()!, parameters.TryGetProperty("scale", out var scale) ? scale.GetSingle() : 1f);
                if (!parameters.TryGetProperty("collision", out var collision) || collision.GetString() != "none")
                {
                    BuildCollision(item);
                }

                break;
            case "pickup":
                if (parameters.TryGetProperty("catalogId", out var visual)) BuildVisual(item, visual.GetString()!, 1f);
                item.Target = Target(item, parameters.GetProperty("interactionId").GetString()!, Text(parameters, "prompt", "Взять"), null, new Vector3(.9f, 1.2f, .9f));
                break;
            case "npc":
                var characterId = parameters.GetProperty("characterId").GetString()!;
                var character = GeneratedCharacterKitDressing.Attach(root, characterId, Text(parameters, "kitPrefix", "Resident"), Vector3.Zero);
                ConfigureResidentPresentation(character, parameters);
                RegisterAmbientVisit(item);
                GeneratedCharacterKitDressing.PlayClip(character, Text(parameters, "clip", "Idle"));
                RegisterRoutine(item, character);
                if (parameters.TryGetProperty("talk", out var talk))
                {
                    item.Target = Target(item, talk.GetProperty("interactionId").GetString()!, Text(talk, "prompt", "Поговорить"),
                        talk.TryGetProperty("dialogueId", out var dialogue) ? dialogue.GetString() : null, new Vector3(1f, 1.8f, 1f));
                }

                break;
            case "trigger":
                BuildTrigger(item);
                break;
            case "scatter":
                BuildScatter(item);
                break;
            default:
                GD.PushWarning($"authored-world: {id} has kind {kind} with no executor; it is shown only in Studio.");
                break;
        }

        _objects[id] = item;
    }

    private void BuildVisual(AuthoredObject item, string catalogId, float scale)
    {
        if (!_catalog.TryGetValue(catalogId, out var entry))
        {
            GD.PushError($"authored-world: {item.Id} uses unknown catalogue entry {catalogId}.");
            return;
        }

        var source = entry.GetProperty("source");
        var scenePath = source.GetProperty("scene").GetString()!;
        Node3D instance;
        if (ResourceLoader.Exists(scenePath))
        {
            if (!_scenes.TryGetValue(scenePath, out var packed))
            {
                _scenes[scenePath] = packed = ResourceLoader.Load<PackedScene>(scenePath);
            }

            instance = packed.Instantiate<Node3D>();
        }
        else
        {
            // A model the author just imported, before Godot's own import ran:
            // read the glTF directly (no code in the file is executed).
            var document = new GltfDocument();
            var state = new GltfState();
            var error = document.AppendFromFile(ProjectSettings.GlobalizePath(scenePath), state);
            if (error != Error.Ok || document.GenerateScene(state) is not Node3D generated)
            {
                GD.PushError($"authored-world: {item.Id} model {scenePath} could not be read ({error}).");
                return;
            }

            instance = generated;
        }

        if (source.TryGetProperty("whole", out var whole) && whole.GetBoolean())
        {
            var wholeHolder = new Node3D { Name = "Visual", Scale = Vector3.One * scale };
            wholeHolder.AddChild(instance);
            item.Root.AddChild(wholeHolder);
            item.Root.SetMeta("catalogId", catalogId);
            return;
        }

        var top = instance.GetChildCount() == 1 && instance.GetChild(0) is Node3D only && only.GetChildCount() > 1 ? only : instance;
        var holder = new Node3D { Name = "Visual", Scale = Vector3.One * scale };
        var corrected = source.TryGetProperty("axisCorrection", out var axis) && axis.GetString() == "x+90";
        var correction = corrected ? Basis.FromEuler(new Vector3(Mathf.Pi * .5f, 0, 0)) : Basis.Identity;
        var origin = ReadVector(entry, "origin");
        holder.Position = -origin * scale;
        foreach (var child in top.GetChildren().OfType<Node3D>())
        {
            var name = child.Name.ToString();
            var matches = source.TryGetProperty("node", out var node) ? name == node.GetString()
                : source.TryGetProperty("prefix", out var prefix) && GroupKey(name) == prefix.GetString()
                  && !name.Contains("LOD1", StringComparison.Ordinal) && !name.Contains("LOD2", StringComparison.Ordinal) && !name.EndsWith("-col", StringComparison.Ordinal);
            if (!matches) continue;
            var transform = new Transform3D(correction, Vector3.Zero) * (top == instance ? child.Transform : top.Transform * child.Transform);
            void ClearSourceOwner(Node node)
            {
                node.Owner = null;
                foreach (var member in node.GetChildren()) ClearSourceOwner(member);
            }
            ClearSourceOwner(child);
            top.RemoveChild(child);
            holder.AddChild(child);
            child.Transform = transform;
        }

        instance.QueueFree();
        item.Root.AddChild(holder);
        item.Root.SetMeta("catalogId", catalogId);
    }

    /// <summary>
    /// A vegetation brush stroke (spec WORLD11): instances are derived from the
    /// stroke's seed, so the same data always grows the same plants, and the
    /// whole stroke is one entity (one undo step). Roads and the authored
    /// exclusion circles stay clear; the count kept out is recorded.
    /// </summary>
    private void BuildScatter(AuthoredObject item)
    {
        var p = item.Params;
        var radius = p.GetProperty("radius").GetSingle();
        var roadClearance = p.TryGetProperty("roadClearance", out var roadMargin) ? roadMargin.GetSingle() : .6f;
        var density = p.GetProperty("density").GetSingle();
        var seed = p.GetProperty("seed").GetUInt64();
        var models = p.GetProperty("catalogIds").EnumerateArray().Select(model => model.GetString()!).ToArray();
        var minScale = p.TryGetProperty("scaleMin", out var min) ? min.GetSingle() : .8f;
        var maxScale = p.TryGetProperty("scaleMax", out var max) ? max.GetSingle() : 1.2f;
        var excludes = p.TryGetProperty("exclude", out var exclude)
            ? exclude.EnumerateArray().Select(circle => (Center: new Vector2(circle[0].GetSingle(), circle[1].GetSingle()), Radius: circle[2].GetSingle())).ToArray()
            : [];
        var rng = new RandomNumberGenerator { Seed = seed };
        var count = Mathf.RoundToInt(Mathf.Pi * radius * radius * density);
        var center = item.Root.GlobalPosition;
        var kept = 0;
        var skipped = 0;
        for (var index = 0; index < count; index++)
        {
            var angle = rng.Randf() * Mathf.Tau;
            var distance = Mathf.Sqrt(rng.Randf()) * radius;
            var yaw = rng.RandfRange(0, 360);
            var size = rng.RandfRange(minScale, maxScale);
            var model = models[rng.RandiRange(0, models.Length - 1)];
            var at = new Vector2(center.X + Mathf.Cos(angle) * distance, center.Z + Mathf.Sin(angle) * distance);
            var road = Urman.Experiments.AgentBAct1.AgentBAct1HeightField.RoadInfo(at.X, at.Y);
            if (road.Distance < road.HalfWidth + roadClearance || excludes.Any(circle => at.DistanceTo(circle.Center) < circle.Radius))
            {
                skipped++;
                continue; // passages stay clear of the brush
            }

            var plant = new Node3D { Name = $"Plant{index}" };
            item.Root.AddChild(plant);
            plant.GlobalPosition = Ground(new Vector3(at.X, 0, at.Y));
            plant.RotationDegrees = new Vector3(0, yaw, 0);
            var holder = new AuthoredObject { Id = item.Id, Kind = "prop", Root = plant, Params = item.Params };
            BuildVisual(holder, model, size);
            kept++;
        }

        item.Root.SetMeta("scatterKept", kept);
        item.Root.SetMeta("scatterSkippedOnPassages", skipped);
    }

    // The visible object and its physical twin share one parent transform, so
    // moving or hiding the object always moves or removes its collision (WORLD13).
    private void BuildCollision(AuthoredObject item)
    {
        if (item.Root.GetNodeOrNull<Node3D>("Visual") is not { } visual) return;
        var catalogId = (string)item.Root.GetMeta("catalogId");
        var size = ReadVector(_catalog[catalogId], "size") * visual.Scale.X;
        var body = new StaticBody3D { Name = "Collision", CollisionLayer = 1u, CollisionMask = 0u };
        if (item.Params.TryGetProperty("collision", out var policy) && policy.GetString() == "surfaces")
        {
            // A complete household parcel contains empty yard and gate space.
            // Its catalogue bounding box cannot serve as a solid collision.
            OpenYardGateway(visual);
            // Yard fences of every plot are rebuilt as one village-wide system along the real lot
            // lines (Act1ConnectedWorld.YardFences); the parcel's own short rails would double them.
            // A parcel squeezed into a narrow lot keeps its yard layout, but its house stays
            // full-size: the dwelling and outbuilding grow back about their own base to the
            // ordinary 0.9 kit scale (walls ~2.6 m, not a toy house).
            if (catalogId.Contains("villageparcel", StringComparison.Ordinal) && visual.Scale.X < .895f)
            {
                var toVisual = visual.GlobalTransform.AffineInverse();
                Aabb? Bounds(Node3D root)
                {
                    Aabb? result = null;
                    foreach (var mesh in root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
                    {
                        if (mesh.Mesh is null || !mesh.Visible) continue;
                        var box = toVisual * mesh.GlobalTransform * mesh.GetAabb();
                        result = result is { } r ? r.Merge(box) : box;
                    }
                    return result;
                }
                if (Bounds(visual) is { } parcel)
                    foreach (var piece in visual.FindChildren("*", "Node3D", true, false).OfType<Node3D>()
                                 .Where(n => n.Name.ToString().EndsWith("_Dwelling", StringComparison.Ordinal)
                                     || n.Name.ToString().EndsWith("_Outbuilding", StringComparison.Ordinal)).ToArray())
                    {
                        piece.Scale *= .9f / visual.Scale.X;
                        // Keep the grown house inside its own parcel: shift it back from any edge.
                        if (Bounds(piece) is not { } extent) continue;
                        float Push(float lo, float hi, float min, float max) =>
                            lo < min ? min - lo : hi > max ? max - hi : 0f;
                        var shift = new Vector3(Push(extent.Position.X, extent.End.X, parcel.Position.X + .3f, parcel.End.X - .3f), 0,
                            Push(extent.Position.Z, extent.End.Z, parcel.Position.Z + .3f, parcel.End.Z - .3f));
                        piece.Position += piece.GetParent<Node3D>().Basis.Inverse() * shift;
                    }
            }
            if (catalogId.Contains("villageparcel", StringComparison.Ordinal))
                TimberHomeStyle.DressParcel(visual, item.Id);
            if (catalogId.Contains("villageparcel", StringComparison.Ordinal))
                foreach (var rail in visual.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                             .Where(mesh => YardFenceMesh.IsMatch(mesh.Name.ToString())).ToArray())
                {
                    rail.Visible = false;
                    rail.SetMeta("suppressionReason", "yard fences rebuilt along lot lines");
                }
            var faces = new List<Vector3>();
            foreach (var mesh in visual.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            {
                if (mesh.Mesh is null || !mesh.IsVisibleInTree()) continue;
                var name = mesh.Name.ToString();
                if (new[] { "_Moss_", "_Sedge_", "_Shrub_", "_Branch_" }.Any(name.Contains)) continue;
                var relative = item.Root.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
                faces.AddRange(mesh.Mesh.GetFaces().Select(vertex => relative * vertex));
            }
            if (faces.Count == 0) throw new InvalidOperationException("Surface collision has no visible source faces: " + item.Id);
            body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = faces.ToArray() } });
            body.SetMeta("collisionPolicy", "actual static source triangles; open yard and gate spaces remain empty");
            body.SetMeta("sourceTriangleCount", faces.Count / 3);
        }
        else body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, size.Y / 2, 0) });
        item.Root.AddChild(body);
        item.Body = body;
        item.BodyLayer = body.CollisionLayer;
    }

    private InteractionTarget Target(AuthoredObject item, string interactionId, string prompt, string? dialogueId, Vector3 size)
    {
        var target = new InteractionTarget
        {
            Name = "Interaction",
            InteractionId = interactionId,
            Prompt = prompt,
            DialogueId = dialogueId ?? string.Empty,
            CollisionLayer = 4u,
            CollisionMask = 0u
        };
        target.SetMeta(AuthoredWorldPlot.AuthoredIdMeta, item.Id);
        target.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        // Interaction targets live under the zone host, which gates them by
        // zone; they follow the authored object through a remote transform.
        _interactionHost.AddChild(target);
        target.GlobalPosition = item.Root.GlobalPosition + new Vector3(0, size.Y / 2, 0);
        var follow = new RemoteTransform3D { Name = "InteractionFollow", Position = new Vector3(0, size.Y / 2, 0), UpdateRotation = false, UpdateScale = false };
        item.Root.AddChild(follow);
        follow.RemotePath = follow.GetPathTo(target);
        return target;
    }

    private void BuildTrigger(AuthoredObject item)
    {
        var size = ReadVector(item.Params, "size");
        var area = new Area3D { Name = "Trigger", CollisionLayer = 0u, CollisionMask = 2u | 1u, Monitoring = true };
        area.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, size.Y / 2, 0) });
        area.BodyEntered += body => OnTriggerEntered(item, body);
        area.BodyExited += body => item.Inside.Remove(body.GetInstanceId());
        item.Root.AddChild(area);
        item.Area = area;
    }

    private async void OnTriggerEntered(AuthoredObject item, Node3D body)
    {
        // Actor filter: only the player unless the author chose otherwise (TRIG02).
        if (Text(item.Params, "actor", "player") == "player" && !body.IsInGroup("player_controller")) return;
        if (!item.Inside.Add(body.GetInstanceId())) return;
        if (!item.Root.Visible || _bridge is null) return;
        var repeat = Text(item.Params, "repeat", "once-per-run");
        if (repeat == "once-per-run" && item.FiredThisRun) return;
        // The compiled interaction's own conditions decide whether it applies;
        // its occurrence is recorded by the kernel, so a repeat never pays twice.
        if (await _bridge.DispatchInteractionAsync(item.Params.GetProperty("interactionId").GetString()!))
        {
            item.FiredThisRun = true;
            GD.Print($"authored-world: trigger {item.Id} fired");
        }
    }

    /// <summary>Re-check a trigger when its step becomes active while the player already stands inside (TRIG05).</summary>
    private void RecheckInside(AuthoredObject item)
    {
        if (item.Area is null || !item.Params.TryGetProperty("countIfInside", out var inside) || !inside.GetBoolean()) return;
        foreach (var body in item.Area.GetOverlappingBodies())
        {
            item.Inside.Remove(body.GetInstanceId());
            OnTriggerEntered(item, body);
        }
    }

    // ---- story states ---------------------------------------------------------------

    private void ApplyStates()
    {
        if (_bridge?.SelectRuntimeState() is not { ValueKind: JsonValueKind.Object } state)
        {
            return;
        }

        var player = GetTree().GetFirstNodeInGroup("player_controller") as Node3D;
        // The pass below visits every object exactly once and writes PendingState on
        // every one of them, so counting assignments here reproduces the exact
        // "values.Where(PendingState is not null).Any()" answer for the next tick.
        _pendingStates = 0;
        foreach (var item in _objects.Values)
        {
            JsonElement? chosen = null;
            if (item.Params.TryGetProperty("states", out var states))
            {
                foreach (var candidate in states.EnumerateArray())
                {
                    if (ContentRuleEngine.EvaluateAll(candidate.GetProperty("when"), state))
                    {
                        chosen = candidate;
                        break;
                    }
                }
            }

            var stateId = chosen?.GetProperty("id").GetString() ?? "base";
            if (stateId == item.AppliedState)
            {
                // An object can still hold a PendingState from an earlier pass whose
                // desired state has since reverted to the applied one. The pass writes
                // nothing for it, but the old "is there any non-null PendingState" scan
                // still saw it, so the count keeps tracking that answer exactly.
                if (item.PendingState is not null) _pendingStates++;
                continue;
            }

            var visible = chosen is not { } visibleState || !visibleState.TryGetProperty("visible", out var visibleValue) || visibleValue.GetBoolean();
            var solid = visible && (chosen is not { } solidState || !solidState.TryGetProperty("collision", out var solidValue) || solidValue.GetBoolean());
            var position = chosen is { } placed && placed.TryGetProperty("position", out _) ? Ground(ReadVector(placed, "position")) : item.BasePosition;
            var yaw = chosen is { } turned && turned.TryGetProperty("yawDegrees", out var yawValue) ? yawValue.GetSingle() : item.BaseYaw;

            // Collision appearing on top of the player waits until they leave;
            // the player is never pushed or trapped by a story change (STATE05).
            if (solid && item.Body is not null && item.Body.CollisionLayer == 0 && player is not null
                && OverlapsBody(item, position, player.GlobalPosition))
            {
                item.PendingState = stateId;
                _pendingStates++;
                continue;
            }

            item.Root.Visible = visible;
            if (!_routines.ContainsKey(item.Id))
            {
                // A character with a routine is placed by the routine alone.
                item.Root.Position = position;
                item.Root.RotationDegrees = new Vector3(0, yaw, 0);
            }

            if (item.Body is not null) item.Body.CollisionLayer = solid ? item.BodyLayer : 0u;
            if (item.Target is not null)
            {
                item.Target.Visible = visible;
                item.Target.CollisionLayer = visible ? 4u : 0u;
            }

            if (item.Area is not null) item.Area.Monitoring = visible;
            item.AppliedState = stateId;
            item.PendingState = null;
            RecheckInside(item);
        }

        ApplyRoutines();
        ApplyAmbientPresence();
    }

    private bool OverlapsBody(AuthoredObject item, Vector3 position, Vector3 player)
    {
        var size = item.Root.GetNodeOrNull<StaticBody3D>("Collision")?.GetChild<CollisionShape3D>(0).Shape is BoxShape3D box ? box.Size : Vector3.One;
        var local = (player - position).Rotated(Vector3.Up, -Mathf.DegToRad(item.Root.RotationDegrees.Y));
        return Mathf.Abs(local.X) < size.X / 2 + .45f && Mathf.Abs(local.Z) < size.Z / 2 + .45f;
    }

    // ---- helpers ---------------------------------------------------------------------

    private static Vector3 Ground(Vector3 point) =>
        new(point.X, point.Y + AgentBAct1HeightField.CollisionGround(point.X, point.Z), point.Z);

    private static Vector3 ReadVector(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out var value) ? AuthoredWorldPlot.ToVector3(value) : Vector3.Zero;

    private static string Text(JsonElement owner, string property, string fallback) =>
        owner.TryGetProperty(property, out var value) ? value.GetString() ?? fallback : fallback;

    private static string GroupKey(string name)
    {
        var parts = name.Split('_');
        return parts.Length > 1 && parts[1].Length > 0 && parts[1].All(char.IsDigit) ? $"{parts[0]}_{parts[1]}" : parts[0];
    }

    private static string SafeName(string id) => id[(id.LastIndexOf('/') + 1)..].Replace(":", "_").Replace(".", "_");
}
