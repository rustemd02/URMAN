using System.Globalization;
using System.Text.Json.Nodes;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Tamara Gennadievna's fence: an optional street accident that grows into a
/// small side quest. The fence itself is a real street-frontage plot on the
/// return street: individual breakable panels stand on the road shoulder, the
/// Niva's hard stops decide whether this is a crash, and the crash commits the
/// quest gate, the broken fence and the checkpoint in one runtime transaction
/// before any presentation starts. RuntimeBridge stays the only state owner:
/// the crash/delivery counters live in npc state, physical fence state in
/// world.props, and this node only stages what those already say.
/// </summary>
public partial class TamaraFenceQuest : Node3D
{
    public const string QuestId = "urman.chapter1:quest/quest_tamara_fence";
    public const string HandInInteractionId = "urman.chapter1:interaction/tamara-fence-hand-in";

    /// <summary>The plot's positions and tuning live in authored world data (URMAN Studio); this node executes them.</summary>
    public const string PlotPath = "res://content/world/act1_tamara_plot.world.v1.json";
    private static readonly AuthoredWorldPlot Plot = AuthoredWorldPlot.Load(PlotPath);

    // The fence line hugs the return street shoulder, roadward of the aligned
    // street frontages: her old fence leans toward the road the way the plot
    // reads in the source video. The car's road-graph reach covers the face.
    private static readonly float FenceFaceX = Plot.Number("fence", "faceX");
    private static readonly float FenceZStart = Plot.Number("fence", "zStart");
    private static readonly float FenceZEnd = Plot.Number("fence", "zEnd");
    private static readonly int PanelCount = Plot.Params("fence").GetProperty("panelCount").GetInt32();
    private static readonly float WicketZ = Plot.Number("fence", "wicketZ");
    private static readonly float CrashSpeedThreshold = Plot.Number("fence", "crashSpeed");
    private static readonly float BreakRadius = Plot.Number("fence", "breakRadius");

    private static readonly Vector3 FenceCenter = Plot.Point("fence", "center");

    /// <summary>
    /// The driveway the road graph knows about: from the street axis to the
    /// breach. Without it any angled approach is refused as "no road here"
    /// before the hull can reach the boards, and the crash reads as an
    /// invisible wall instead of a real impact.
    /// </summary>
    public static readonly Vector2[] ApproachAxis = Plot.Params("approach").GetProperty("points")
        .EnumerateArray().Select(AuthoredWorldPlot.ToVector2).ToArray();

    // Board spots in authored order; their interaction IDs are part of the data.
    private static readonly (Vector3 At, float Yaw, string InteractionId, string EntityId)[] BoardSpots = Plot.EntitiesOfKind("item-spawn")
        .OrderBy(entity => entity.Id, StringComparer.Ordinal)
        .Select(entity => (
            AuthoredWorldPlot.ToVector3(entity.Params.GetProperty("position")),
            entity.Params.GetProperty("yawDegrees").GetSingle(),
            entity.Params.GetProperty("interactionId").GetString()!,
            entity.Id))
        .ToArray();

    private static readonly Vector3 TamaraEmergence = Plot.Point("tamara-emergence");
    private static readonly Vector3 TamaraRest = Plot.Point("tamara-rest");
    private static readonly Vector3 GuyEntry = Plot.Point("guy-entry");
    // He films from the street shoulder north of the wreck: the Niva is nose-in
    // at the breach and its body covers z -42.7..-44.5, so a bystander standing
    // any closer to the fence would be inside the car.
    private static readonly Vector3 GuyFilming = Plot.Point("guy-filming");
    internal static readonly Vector3 GuyFilmingSpot = GuyFilming;
    private static readonly Vector3 GuyRest = Plot.Point("guy-rest");

    private readonly List<FencePanel> _panels = [];
    private readonly List<Node3D> _pillars = [];
    private readonly List<InteractionTarget> _boardTargets = [];
    private readonly List<Node3D> _boardVisuals = [];
    private readonly List<MeshInstance3D> _debris = [];
    private Node3D _tamara = null!;
    private Node3D _guy = null!;
    private BoneAttachment3D? _phoneGrip;
    private MeshInstance3D? _phoneMesh;
    private AnimationPlayer? _guyAnimation;
    private Skeleton3D? _guySkeleton;
    private bool _guyFilming;
    private float _guyLaugh;
    private InteractionTarget? _handInTarget;
    private RuntimeBridge? _bridge;
    private Act1ConnectedWorld? _world;
    private bool _fleetHooked;
    private object? _session;
    private TamaraFenceCutscene? _cutscene;
    private bool _questBannerShown;
    private bool _remarkSaid;
    private bool _callbackDone;
    private bool _repairApplied;
    private CpuParticles3D? _snowPuff;

    internal sealed class FencePanel
    {
        public required Node3D Root;
        public required StaticBody3D Body;
        public required MeshInstance3D Mesh;
        public bool Broken;
    }

    internal IReadOnlyList<FencePanel> Panels => _panels;
    internal IReadOnlyList<InteractionTarget> BoardTargets => _boardTargets;
    internal InteractionTarget? HandInTarget => _handInTarget;
    internal TamaraFenceCutscene? ActiveCutscene => _cutscene;
    internal Node3D TamaraActor => _tamara;
    internal Node3D GuyActor => _guy;
    internal bool FenceBreakApplied => _panels.Any(panel => panel.Broken);
    internal bool FenceRepairApplied => _repairApplied;

    public static TamaraFenceQuest? Current(SceneTree tree) =>
        tree.GetFirstNodeInGroup("tamara_fence_quest") as TamaraFenceQuest;

    /// <summary>Called from Act1ConnectedWorld after the street frontages exist.</summary>
    public static TamaraFenceQuest Build(Act1ConnectedWorld world, Node3D coreRoot, Node interactionHost)
    {
        var quest = new TamaraFenceQuest { Name = "TamaraFenceQuest" };
        coreRoot.AddChild(quest);
        quest.BuildContent(world, interactionHost);
        return quest;
    }

    private void BuildContent(Act1ConnectedWorld world, Node interactionHost)
    {
        _world = world;
        AddToGroup("tamara_fence_quest");
        SetMeta("runtimeStateOwner", "RuntimeBridge npc/tamara + world.props tamara/fence");
        SetMeta("presentationRole", "breakable fence of Tamara Gennadievna's plot, six spare boards, two people");
        // Bone work (the raised phone arm) runs after the AnimationPlayer has
        // written this frame's idle, or the clip would overwrite the aims.
        ProcessPriority = 100;

        var root = new Node3D { Name = "TamaraFencePlot" };
        AddChild(root);
        BuildFence(root);
        BuildWicket(root);
        BuildBoards(root, interactionHost);
        BuildNpcs(root);
        _snowPuff = new CpuParticles3D
        {
            Name = "CrashSnowPuff",
            Emitting = false,
            OneShot = true,
            Amount = 26,
            Lifetime = .8f,
            Explosiveness = 1f,
            Direction = new(0, 1, 0),
            Spread = 65f,
            InitialVelocityMin = 1.1f,
            InitialVelocityMax = 2.6f,
            Gravity = new(0, -7.5f, 0),
            ScaleAmountMin = .05f,
            ScaleAmountMax = .11f,
            Mesh = new QuadMesh { Size = new(.16f, .16f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("eef2f6"),
                Roughness = 1f,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
            }
        };
        root.AddChild(_snowPuff);
    }

    // ---- fence ----------------------------------------------------------------

    /// <summary>
    /// The plot follows the source video: pale concrete pillars on a rubble
    /// footing, and between them wooden shields of weathered peach boards that
    /// lean when the car hits them. The pillars never fall — only the shields.
    /// </summary>
    private void BuildFence(Node3D root)
    {
        var fenceRoot = new Node3D { Name = "TamaraFence" };
        root.AddChild(fenceRoot);
        var span = FenceZEnd - FenceZStart;
        for (var index = 0; index < PanelCount; index++)
        {
            var z0 = FenceZStart + span * index / PanelCount;
            var z1 = FenceZStart + span * (index + 1) / PanelCount;
            var panel = BuildPanel(fenceRoot, index, z0, z1, weathered: true);
            _panels.Add(panel);
        }

        for (var index = 0; index <= PanelCount; index++)
        {
            var z = FenceZStart + span * index / PanelCount;
            BuildPillar(fenceRoot, index, z);
        }

        BuildFooting(fenceRoot);
    }

    private void BuildPillar(Node3D parent, int index, float z)
    {
        var ground = Ground(z);
        var pillar = new Node3D { Name = $"TamaraFencePillar_{index}", Position = new(FenceFaceX + .03f, ground, z) };
        parent.AddChild(pillar);
        pillar.AddChild(new MeshInstance3D
        {
            Name = "Shaft",
            Mesh = Box(.23f, 1.86f, .23f, Colors.White, ConcreteLight()),
            Position = new(0, .93f, 0)
        });
        pillar.AddChild(new MeshInstance3D
        {
            Name = "Cap",
            Mesh = Box(.3f, .09f, .3f, Colors.White, ConcreteDark()),
            Position = new(0, 1.9f, 0)
        });
        pillar.AddChild(new MeshInstance3D
        {
            Name = "CapSnow",
            Mesh = Box(.29f, .035f, .29f, Colors.White, SnowMaterial()),
            Position = new(0, 1.98f, 0)
        });
        var body = new StaticBody3D
        {
            Name = "PillarBody",
            CollisionLayer = 1u,
            CollisionMask = 0u
        };
        body.SetMeta("tamaraFencePillar", index);
        body.SetMeta("collisionOwner", "tamara-fence-quest");
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new(.23f, 1.95f, .23f) },
            Position = new(0, .97f, 0)
        });
        pillar.AddChild(body);
        _pillars.Add(pillar);
    }

    /// <summary>White field-stone rubble along the foot of the run, the way the
    /// video's fence stands on its own broken base.</summary>
    private void BuildFooting(Node3D parent)
    {
        var footing = new Node3D { Name = "TamaraFenceFooting" };
        parent.AddChild(footing);
        var stone = ConcreteLight();
        var stoneDark = ConcreteDark();
        var snow = SnowMaterial();
        var rng = new RandomNumberGenerator { Seed = 61207 };
        for (var z = FenceZStart; z <= FenceZEnd; z += .34f)
        {
            var ground = Ground(z);
            var width = rng.RandfRange(.34f, .52f);
            var height = rng.RandfRange(.14f, .26f);
            footing.AddChild(new MeshInstance3D
            {
                Name = "Stone",
                Mesh = Box(width, height, .28f, Colors.White, rng.Randf() > .45f ? stone : stoneDark),
                Position = new(FenceFaceX + .02f + rng.RandfRange(-.04f, .04f), ground + height * .5f - .04f, z),
                RotationDegrees = new(0, rng.RandfRange(-9f, 9f), 0)
            });
            footing.AddChild(new MeshInstance3D
            {
                Name = "StoneSnow",
                Mesh = Box(width * .86f, .03f, .22f, Colors.White, snow),
                Position = new(FenceFaceX + .02f, ground + height - .025f, z),
                RotationDegrees = new(0, rng.RandfRange(-9f, 9f), 0)
            });
        }
    }

    private static StandardMaterial3D? _concreteLight;
    private static StandardMaterial3D? _concreteDark;
    private static StandardMaterial3D? _snowMaterial;

    private static StandardMaterial3D ConcreteLight() => _concreteLight ??= FlatMaterial("b7b5aa");
    private static StandardMaterial3D ConcreteDark() => _concreteDark ??= FlatMaterial("9d9a90");
    private static StandardMaterial3D SnowMaterial() => _snowMaterial ??= FlatMaterial("e9edf2");

    private static StandardMaterial3D FlatMaterial(string hex) =>
        new() { AlbedoColor = new Color(hex), Roughness = .95f };


    private FencePanel BuildPanel(Node3D parent, int index, float z0, float z1, bool weathered)
    {
        // The line runs southward (z decreases), so the span itself is negative.
        // The shield sits between two pillars and never overlaps them.
        var length = MathF.Abs(z1 - z0) - .24f;
        var midZ = (z0 + z1) * .5f;
        var ground = Ground(midZ);
        var panelRoot = new Node3D
        {
            Name = $"TamaraFencePanel_{index}",
            Position = new(FenceFaceX, ground, midZ)
        };
        parent.AddChild(panelRoot);
        var mesh = new MeshInstance3D
        {
            Name = $"PanelBoards_{index}",
            Mesh = PanelMesh(index, length, weathered),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        };
        panelRoot.AddChild(mesh);
        var body = new StaticBody3D
        {
            Name = $"PanelBody_{index}",
            CollisionLayer = 1u,
            CollisionMask = 0u
        };
        body.SetMeta("tamaraFencePanel", index);
        body.SetMeta("collisionOwner", "tamara-fence-quest");
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new(.16f, 1.8f, length) },
            Position = new(0, .9f, 0)
        });
        panelRoot.AddChild(body);
        return new FencePanel { Root = panelRoot, Body = body, Mesh = mesh };
    }

    /// <summary>Vertex-coloured boards in one merged mesh, the way the street
    /// frontages draw: a handful of draw calls for the whole fence.</summary>
    private static ArrayMesh PanelMesh(int seed, float length, bool weathered)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(9173 + seed * 131) };
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colours = new List<Color>();

        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color colour)
        {
            if ((b - a).Cross(c - a).Dot(outward) > 0) (b, c) = (c, b);
            foreach (var v in new[] { a, b, c }) { vertices.Add(v); normals.Add(outward); colours.Add(colour); }
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color colour)
        {
            Tri(a, b, c, outward, colour);
            Tri(a, c, d, outward, colour);
        }

        void Box(Vector3 centre, Vector3 x, Vector3 y, Vector3 z, Color colour)
        {
            Vector3 P(int i, int j, int k) => centre + x * i + y * j + z * k;
            var nx = x.Normalized(); var ny = y.Normalized(); var nz = z.Normalized();
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), nx, colour);
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), -nx, colour);
            Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), ny, colour);
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), nz, colour);
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), -nz, colour);
        }

        // Wide horizontal boards in a frame: the shields of the source video
        // are plank panels, sun-bleached on top and greyer towards the foot.
        Color Board(int index, int count)
        {
            if (!weathered)
            {
                var fresh = new Color(index % 2 == 0 ? "cd9a89" : "c48f7e");
                return fresh;
            }

            // Saturated on purpose: the winter light of the street is cold and
            // washes a pale peach out to grey, so the boards carry more colour
            // than the paint they imitate.
            var faded = new Color(
                Mathf.Lerp(.93f, 1.00f, index / (float)Math.Max(count - 1, 1)),
                Mathf.Lerp(.46f, .60f, index / (float)Math.Max(count - 1, 1)),
                Mathf.Lerp(.35f, .46f, index / (float)Math.Max(count - 1, 1)));
            if (index % 5 == 1) faded = faded.Lerp(new Color("6d544a"), .4f);
            var tint = 1f + rng.RandfRange(-.06f, .05f);
            return new Color(Mathf.Min(faded.R * tint, 1f), faded.G * tint, faded.B * tint);
        }

        var height = 1.61f;
        var thickness = .022f;
        var boards = 6;
        var gap = .008f;
        for (var i = 0; i < boards; i++)
        {
            var boardHeight = height / boards - gap;
            var y = .11f + height * (i + .5f) / boards;
            var left = new Vector3(0, y, -length * .5f + .035f);
            var right = new Vector3(0, y, length * .5f - .035f);
            var centre = (left + right) * .5f;
            var half = (right - left) * .5f;
            var sag = weathered ? rng.RandfRange(-.004f, .004f) : 0f;
            Box(centre + new Vector3(sag, 0, 0), Vector3.Right * thickness,
                Vector3.Up * (boardHeight * .5f), half, Board(i, boards));
        }

        // The frame: a top rail, a low rail and two stiles on the inside face.
        var frame = weathered ? new Color("8f7566") : new Color("a87d6b");
        Box(new(.012f, 1.755f, 0), Vector3.Right * .03f, Vector3.Up * .045f,
            Vector3.Back * (length * .5f), frame);
        Box(new(.012f, .075f, 0), Vector3.Right * .03f, Vector3.Up * .045f,
            Vector3.Back * (length * .5f), frame);
        foreach (var z in new[] { -length * .5f + .022f, length * .5f - .022f })
        {
            Box(new(0, .9f, z), Vector3.Right * .03f, Vector3.Up * .83f, Vector3.Back * .022f, frame);
        }

        var snow = new Color("e9edf2");
        Box(new(.014f, 1.815f, 0), Vector3.Right * .034f, Vector3.Up * .014f,
            Vector3.Back * (length * .5f), snow);

        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colours.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, VehicleVisualFactory.FrontageWoodMaterial());
        return mesh;
    }

    /// <summary>An open wicket at the north end of the run: the yard keeps its
    /// own way in, and the hand-over happens beside it. It is built of the same
    /// concrete posts and peach shields as the run, swung open toward the yard.
    /// </summary>
    private void BuildWicket(Node3D root)
    {
        // The grain shader multiplies a white base by vertex colour, and these
        // boxes carry none: the leaf needs its own painted materials or it
        // comes out plain white.
        var peach = new StandardMaterial3D { AlbedoColor = new Color("c9826f"), Roughness = .9f };
        var frame = new StandardMaterial3D { AlbedoColor = new Color("8f7566"), Roughness = .92f };
        var wicket = new Node3D { Name = "TamaraFenceWicket" };
        root.AddChild(wicket);
        var ground = Ground(WicketZ);
        wicket.Position = new(FenceFaceX, ground, WicketZ);
        foreach (var z in new[] { -.62f, .62f })
        {
            wicket.AddChild(new MeshInstance3D
            {
                Mesh = Box(.19f, 1.8f, .19f, Colors.White, ConcreteLight()),
                Position = new(0, .9f, z)
            });
            wicket.AddChild(new MeshInstance3D
            {
                Mesh = Box(.25f, .08f, .25f, Colors.White, ConcreteDark()),
                Position = new(0, 1.84f, z)
            });
        }

        // The leaf swings in from the street hinge and rests at an angle toward
        // the yard, the way a village gate is left open all day.
        var leaf = new Node3D { Name = "WicketLeaf", Position = new(0, 0f, .62f) };
        wicket.AddChild(leaf);
        leaf.RotationDegrees = new(0, -62f, 0);
        for (var i = 0; i < 6; i++)
        {
            var y = .12f + i * .255f;
            leaf.AddChild(new MeshInstance3D
            {
                Mesh = Box(.045f, .235f, 1.05f, Colors.White, peach),
                Position = new(0, y, -.53f)
            });
        }

        leaf.AddChild(new MeshInstance3D
        {
            Mesh = Box(.05f, 1.5f, .05f, Colors.White, frame),
            Position = new(.03f, .8f, -.05f)
        });
        leaf.AddChild(new MeshInstance3D
        {
            Mesh = Box(.05f, 1.5f, .05f, Colors.White, frame),
            Position = new(.03f, .8f, -1.01f)
        });
    }

    private static BoxMesh Box(float x, float y, float z, Color colour, Material? material = null)
    {
        var mesh = new BoxMesh { Size = new(x, y, z) };
        mesh.Material = material ?? new StandardMaterial3D { AlbedoColor = colour, Roughness = .88f };
        return mesh;
    }

    private static float Ground(float z) => Ground(FenceFaceX, z);
    private static float Ground(float x, float z) => AgentBAct1HeightField.CollisionGround(x, z);

    // ---- boards ---------------------------------------------------------------

    private void BuildBoards(Node3D root, Node interactionHost)
    {
        var boardsRoot = new Node3D { Name = "TamaraFenceBoards" };
        root.AddChild(boardsRoot);
        var wood = new StandardMaterial3D { AlbedoColor = new Color("8a745c"), Roughness = .9f };
        var woodDark = new StandardMaterial3D { AlbedoColor = new Color("77644f"), Roughness = .92f };
        var snowMat = new StandardMaterial3D { AlbedoColor = new Color("e9edf2"), Roughness = 1f };
        for (var index = 0; index < BoardSpots.Length; index++)
        {
            var (at, yaw, interactionId, entityId) = BoardSpots[index];
            var ground = Ground(at.X, at.Z);
            var spot = new Node3D { Name = $"BoardSpot_{index + 1}", Position = new(at.X, ground, at.Z) };
            spot.SetMeta(AuthoredWorldPlot.AuthoredIdMeta, entityId);
            boardsRoot.AddChild(spot);

            // The takeable plank leans where a hand can reach it.
            var takeable = new Node3D { Name = "TakeablePlank", RotationDegrees = new(0, yaw, 0) };
            spot.AddChild(takeable);
            var plank = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new(.16f, 2.05f, .05f), Material = wood },
                Position = new(.16f, .78f, 0),
                RotationDegrees = new(0, 0, -12f)
            };
            takeable.AddChild(plank);
            takeable.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new(.5f, .05f, .1f), Material = snowMat },
                Position = new(.16f, 1.62f, .0f),
                RotationDegrees = new(0, 0, -12f)
            });

            // Neighbouring lumber is permanent village dressing, never taken.
            var dressing = new Node3D { Name = "LumberDressing" };
            spot.AddChild(dressing);
            var rng = new RandomNumberGenerator { Seed = (ulong)(311 + index * 57) };
            for (var extra = 0; extra < 2; extra++)
            {
                var lying = new MeshInstance3D
                {
                    Mesh = new BoxMesh
                    {
                        Size = new(.15f, .05f, 1.7f + rng.RandfRange(-.2f, .3f)),
                        Material = extra == 0 ? woodDark : wood
                    },
                    Position = new(-.45f - extra * .3f, .045f + extra * .055f, .1f + extra * .18f),
                    RotationDegrees = new(0, yaw + rng.RandfRange(-9f, 9f), 0)
                };
                dressing.AddChild(lying);
            }

            var target = new InteractionTarget
            {
                Name = $"TakeBoard_{index + 1}",
                InteractionId = interactionId,
                Prompt = "Взять целую доску",
                CollisionLayer = 4u,
                CollisionMask = 0u,
                Position = new(0, .8f, 0)
            };
            target.SetMeta("worldPropId", $"tamara/board/{index + 1}");
            target.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(.9f, 1.2f, 1.0f) } });
            spot.AddChild(target);
            var boardIndex = index;
            target.AfterDispatch = () => CommitBoardTakenAsync(boardIndex, takeable);
            _boardTargets.Add(target);
            _boardVisuals.Add(takeable);
        }

        _handInTarget = new InteractionTarget
        {
            Name = "TamaraHandIn",
            InteractionId = HandInInteractionId,
            Prompt = "Отдать доски Тамаре Геннадьевне",
            DialogueId = "urman.chapter1:dialogue/tamara_fence_hand_in",
            CollisionLayer = 4u,
            CollisionMask = 0u,
            Position = new(4.05f, Ground(4.05f, -44.2f) + .85f, -44.2f)
        };
        _handInTarget.SetMeta("physicalOwner", "Tamara Gennadievna at her wicket");
        _handInTarget.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(1.1f, 1.8f, 1.0f) } });
        root.AddChild(_handInTarget);
        _handInTarget.AfterDispatch = HandInTransactionAsync;
    }

    private async Task<bool> CommitBoardTakenAsync(int index, Node3D visual)
    {
        if (_bridge is null) return false;
        var taken = await _bridge.DispatchWorldPropsAsync(new JsonArray
        {
            new JsonObject { ["propId"] = $"tamara/board/{index + 1}", ["taken"] = true }
        });
        if (taken)
        {
            visual.Visible = false;
            PresentBoardProgress();
        }

        return taken;
    }

    /// <summary>The board count is the only progress this quest has, so the
    /// toast repeats with the new number instead of leaving the player to
    /// remember how many planks they are carrying.</summary>
    private void PresentBoardProgress()
    {
        if (TamaraFenceSnapshotNow() is not { Repaired: false } snapshot) return;
        PresentBanner("Забор Тамары Геннадьевны",
            snapshot.Carried >= 6 ? "Вернуться к Тамаре Геннадьевне" : "Целые доски во дворах деревни",
            $"{snapshot.Carried} / 6");
    }

    private async Task<bool> HandInTransactionAsync()
    {
        if (_bridge is null) return false;
        var committed = await _bridge.TamaraFenceDeliverAsync();
        if (committed && TamaraFenceSnapshotNow() is { Delivered: 6 })
        {
            PresentBanner("Забор Тамары Геннадьевны", "Забор отремонтирован", "6 / 6");
        }
        return true;
    }

    private SideQuestBannerUi? _banner;

    private void PresentBanner(string title, string goal, string counter)
    {
        if (!IsInsideTree()) return;
        if (_banner is not { } banner || !GodotObject.IsInstanceValid(banner))
        {
            if (GetTree().GetFirstNodeInGroup("side_quest_banner") is SideQuestBannerUi existing
                && GodotObject.IsInstanceValid(existing))
            {
                _banner = existing;
            }
            else
            {
                _banner = new SideQuestBannerUi { Name = "SideQuestBannerUi" };
                GetTree().Root.AddChild(_banner);
            }
        }
        _banner.CallDeferred(nameof(SideQuestBannerUi.Present), title, goal, counter);
    }

    private RuntimeBridge.TamaraFenceSnapshot? TamaraFenceSnapshotNow() => _bridge?.TamaraFenceSnapshotNow();

    // ---- people ---------------------------------------------------------------

    private void BuildNpcs(Node3D root)
    {
        var people = new Node3D { Name = "TamaraFencePeople" };
        people.SetMeta("presentationOnly", true);
        root.AddChild(people);

        _tamara = GeneratedCharacterKitDressing.Attach(people, "tamara", "Gulsina",
            new(TamaraRest.X, Ground(TamaraRest.X, TamaraRest.Z), TamaraRest.Z));
        _tamara.Name = "Npc_tamara";
        TintKitParts(_tamara, "Gulsina",
            ("Coat", "2f3a52"), ("Scarf", "cfc7b8"), ("Skirt", "3a4257"), ("Sash", "8c3f38"));
        FaceDirection(_tamara, Vector3.Left);
        _tamara.Visible = false;

        _guy = GeneratedCharacterKitDressing.Attach(people, "phone_guy", "Rinat",
            new(GuyRest.X, Ground(GuyRest.X, GuyRest.Z), GuyRest.Z));
        _guy.Name = "Npc_phone_guy";
        TintKitParts(_guy, "Rinat", ("Coat", "4c5566"), ("Sash", "2e333d"), ("Hat", "565f6e"));
        _guy.Visible = false;
        _guyAnimation = _guy.FindChildren("*", nameof(AnimationPlayer), true, false)
            .OfType<AnimationPlayer>()
            .FirstOrDefault(player => player.HasAnimation("Rinat_Idle"));
        foreach (var mesh in _guy.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (!mesh.Visible || !mesh.Name.ToString().EndsWith("_BootLeft_LOD0", StringComparison.Ordinal)) continue;
            if (mesh.GetNode<Skeleton3D>(mesh.Skeleton) is { } skeleton)
            {
                _guySkeleton = skeleton;
                var hand = skeleton.FindBone("hand_r");
                if (hand >= 0)
                {
                    _phoneGrip = new BoneAttachment3D { Name = "PhoneGrip", BoneIdx = hand };
                    skeleton.AddChild(_phoneGrip);
                    var phone = new MeshInstance3D
                    {
                        Name = "Phone",
                        Mesh = Box(.072f, .148f, .013f, new Color("20242b")),
                        Visible = false
                    };
                    var screen = new MeshInstance3D
                    {
                        Mesh = Box(.06f, .126f, .004f, new Color("3d4c5e")),
                        Position = new(0, 0, -.008f)
                    };
                    phone.AddChild(screen);
                    _phoneGrip.AddChild(phone);
                    phone.Position = new(0, .1f, .03f);
                    _phoneMesh = phone;
                }
            }
            break;
        }
    }

    private static void TintKitParts(Node3D actor, string prefix, params (string Part, string Hex)[] tints)
    {
        foreach (var mesh in actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = mesh.Name.ToString();
            foreach (var (part, hex) in tints)
            {
                if (!name.StartsWith($"{prefix}_{part}_", StringComparison.Ordinal)) continue;
                mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(hex), Roughness = .92f };
            }
        }
    }

    private static void FaceDirection(Node3D actor, Vector3 worldDirection)
    {
        var direction = worldDirection.Normalized();
        actor.RotationDegrees = new(0, Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z)), 0);
    }

    private static void FaceTowards(Node3D actor, Vector3 worldPoint)
    {
        var direction = worldPoint - actor.GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() < .0001f) return;
        FaceDirection(actor, direction);
    }

    // ---- runtime wiring -------------------------------------------------------

    public override void _Process(double delta)
    {
        // Runs at ProcessPriority 100: the idle clip has already written this
        // frame's bones, so the raised phone arm survives.
        HoldFilmingPose();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_bridge is null)
        {
            if (GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge bridge)
            {
                _bridge = bridge;
                bridge.RuntimeStateChanged += OnRuntimeStateChanged;
                OnRuntimeStateChanged();
            }
            else
            {
                return;
            }
        }

        HookFleet();
        UpdateRepairPresentation();
        UpdateAmbientBehaviour(delta);
    }

    private void HookFleet()
    {
        if (_fleetHooked) return;
        var fleet = GetTree().GetFirstNodeInGroup("vehicle_fleet") as VehicleFleet;
        if (fleet is null) return;
        foreach (var vehicle in fleet.Vehicles) vehicle.HardStop += OnVehicleHardStop;
        _fleetHooked = true;
    }

    private void OnVehicleHardStop(VehicleController vehicle, float speed, Node? collider)
    {
        if (speed < CrashSpeedThreshold || collider is not Node3D colliderNode) return;
        FencePanel? panel = null;
        var hitPillar = false;
        for (var node = collider; node is not null; node = node.GetParent())
        {
            if (node.GetMeta("tamaraFencePanel", -1).AsInt32() is >= 0)
            {
                panel = _panels.FirstOrDefault(candidate => candidate.Body == node);
                break;
            }

            if (node.GetMeta("tamaraFencePillar", -1).AsInt32() is >= 0)
            {
                hitPillar = true;
                break;
            }
        }

        if (panel is null && hitPillar)
        {
            // The concrete pillars stand between the shields and from some
            // angles the car reaches one first. Hitting a pillar is hitting the
            // fence: the nearest shield comes down with it.
            var nearest = _panels
                .Where(candidate => !candidate.Broken)
                .OrderBy(candidate => Mathf.Abs(candidate.Root.GlobalPosition.Z - colliderNode.GlobalPosition.Z))
                .FirstOrDefault();
            if (nearest is not null
                && Mathf.Abs(nearest.Root.GlobalPosition.Z - colliderNode.GlobalPosition.Z) <= BreakRadius + .8f)
            {
                panel = nearest;
            }
        }

        if (panel is null) return;
        HandleCrashAsync(vehicle, speed, panel, colliderNode.GlobalPosition).ContinueWith(_ => { });
    }

    internal async Task<bool> HandleCrashAsync(VehicleController? vehicle, float speed, FencePanel panel, Vector3 impactPoint)
    {
        if (_bridge?.SessionIdentity is not { } session) return false;
        if (_cutscene is not null) return false;
        if (TamaraFenceSnapshotNow()?.Crashed == true) return false;

        // The commit precedes every presentation: crash flag, quest gate and
        // the broken-fence world state are one transaction, then the scene.
        if (!await _bridge.TamaraFenceCrashAsync(impactPoint)) return false;
        if (!ReferenceEquals(session, _bridge.SessionIdentity)) return false;
        ApplyBreak(impactPoint);
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null) return false;

        _cutscene = new TamaraFenceCutscene(this, _bridge, session, player, vehicle, impactPoint);
        _cutscene.AccelerateForTest(_cutsceneTestSpeed);
        if (CutsceneShotListenerForTest is { } shotListener) _cutscene.SetShotListenerForTest(shotListener);
        AddChild(_cutscene);
        var watched = await _cutscene.RunAsync();
        if (_cutscene is { } scene && GodotObject.IsInstanceValid(scene))
        {
            scene.QueueFree();
        }
        _cutscene = null;
        if (watched && _bridge is { } bridge)
        {
            await bridge.DispatchWorldPropsAsync(new JsonArray
            {
                new JsonObject { ["propId"] = RuntimeBridge.TamaraFenceStateId, ["cutsceneWatched"] = true }
            });
        }

        StageRestState();
        if (player is { ModalOpen: true } livePlayer && GodotObject.IsInstanceValid(livePlayer))
        {
            // A load during the scene already restored its own controls.
            livePlayer.SetModalOpen(false);
        }
        PresentQuestBanner();
        await _bridge.SaveCheckpointAsync(force: true);
        return true;
    }

    /// <summary>
    /// Panel shields topple over their own bottom edge: the struck shield goes
    /// down fast into the yard, its neighbours are dragged over more slowly and
    /// come to rest leaning on the pillars. The concrete pillars stay upright,
    /// and loose boards are already on the ground where the car pushed them
    /// instead of dropping from the sky.
    /// </summary>
    private void ApplyBreak(Vector3 impactPoint, bool animate = true)
    {
        var fallTowardYard = true;
        foreach (var panel in _panels)
        {
            if (panel.Broken) continue;
            var centre = panel.Root.GlobalPosition;
            if (Mathf.Abs(centre.Z - impactPoint.Z) > BreakRadius) continue;
            panel.Broken = true;
            panel.Body.CollisionLayer = 0u;
            var distance = Mathf.Abs(centre.Z - impactPoint.Z);
            TopplePanel(panel, distance, fallTowardYard, animate);
            PeelBoards(panel, distance, fallTowardYard, animate);
            ScatterLooseBoards(centre, distance, impactPoint, animate);
        }

        if (animate)
        {
            _snowPuff?.Restart();
        }

        UiFoley.PlayWorld(this, FenceCenter + Vector3.Up, "wood_tap");
        UiFoley.PlayWorld(this, FenceCenter + Vector3.Up, "hollow_board");
        SetMeta("tamaraFenceBroken", true);
    }

    private void TopplePanel(FencePanel panel, float distance, bool towardYard, bool animate)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(panel.Root.GlobalPosition.Z * 977f) };
        var near = Mathf.Clamp(1f - distance / BreakRadius, 0f, 1f);
        var restAngle = Mathf.Lerp(58f, 66f, rng.Randf()) + near * 26f;
        var lean = towardYard ? -restAngle : restAngle;
        var skid = (towardYard ? 1f : -1f) * (0.06f + near * 0.16f);
        var yaw = rng.RandfRange(-4.5f, 4.5f);
        var settleSeconds = Mathf.Lerp(1.35f, .62f, near);
        var start = panel.Root.RotationDegrees;
        var startPosition = panel.Root.Position;
        if (!animate)
        {
            panel.Root.RotationDegrees = new(start.X, start.Y + yaw, lean);
            panel.Root.Position = startPosition + new Vector3(skid, 0f, 0f);
            return;
        }

        // Falling is a rotation about the footing, not a dropped prop: the
        // shield swings down, hits, and rocks back a touch before it rests.
        var tween = CreateTween();
        tween.SetParallel(false);
        tween.TweenMethod(Callable.From((float t) =>
        {
            var angle = Mathf.Lerp(0f, lean, t);
            panel.Root.RotationDegrees = new(start.X, start.Y + yaw * t, angle);
            panel.Root.Position = startPosition + new Vector3(skid * t, 0f, rng.RandfRange(-.03f, .03f) * t);
        }), 0f, 1f, settleSeconds).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenMethod(Callable.From((float t) =>
        {
            var angle = Mathf.Lerp(lean, lean * .96f, t);
            panel.Root.RotationDegrees = new(start.X, start.Y + yaw, angle);
        }), 0f, 1f, .42f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Two boards tear out of the shield while it is still going over:
    /// they start at its top edge and drop with it, so the fall reads as a
    /// breaking panel rather than a rigid plank turning about its foot.</summary>
    private void PeelBoards(FencePanel panel, float distance, bool towardYard, bool animate)
    {
        if (distance > BreakRadius * .8f) return;
        var centre = panel.Root.GlobalPosition;
        var ground = Ground(centre.X, centre.Z);
        var fallSign = towardYard ? 1f : -1f;
        var rng = new RandomNumberGenerator { Seed = (ulong)(centre.Z * 733f) };
        var peel = new StandardMaterial3D { AlbedoColor = new Color("c07a67"), Roughness = .9f };
        for (var board = 0; board < 2; board++)
        {
            var along = board == 0 ? -.42f : .38f;
            var start = centre + new Vector3(0f, 1.28f + board * .26f, along);
            var end = centre + new Vector3(
                fallSign * (1.32f + rng.RandfRange(-.12f, .3f)),
                ground + .045f + board * .05f,
                along * 1.25f + rng.RandfRange(-.12f, .12f));
            var debris = new MeshInstance3D
            {
                Name = $"CrashPeel_{_debris.Count}",
                Mesh = new BoxMesh
                {
                    Size = new(.05f, .23f, 1.15f + rng.RandfRange(-.12f, .18f)),
                    Material = peel
                },
                Position = start,
                RotationDegrees = new(0f, rng.RandfRange(-6f, 6f), -52f)
            };
            AddChild(debris);
            _debris.Add(debris);
            // A fence board lies flat once it is off: its 23 cm face turns up.
            var landing = new Vector3(2f, rng.RandfRange(-28f, 28f), -88f);
            if (!animate)
            {
                debris.Position = end;
                debris.RotationDegrees = landing;
                continue;
            }

            var drop = CreateTween();
            drop.SetTrans(Tween.TransitionType.Quad);
            drop.SetEase(Tween.EaseType.In);
            drop.TweenProperty(debris, "position", end, .62f + rng.RandfRange(0f, .2f))
                .SetDelay(.22f + board * .1f);
            drop.Parallel().TweenProperty(debris, "rotation_degrees", landing, .62f)
                .SetDelay(.22f + board * .1f);
        }
    }

    /// <summary>Boards that split out of the shield: they are on the ground from
    /// the first frame and only slide a little, the way a plank ends up when a
    /// car pushes a panel over.</summary>
    private void ScatterLooseBoards(Vector3 centre, float distance, Vector3 impactPoint, bool animate)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(centre.Z * 1013f) };
        // Loose boards keep the shield's own palette: a brighter red plank in a
        // heap of pale pink ones read as a different fence.
        var wood = new StandardMaterial3D { AlbedoColor = new Color("c9826f"), Roughness = .9f };
        var woodDark = new StandardMaterial3D { AlbedoColor = new Color("9c6a58"), Roughness = .92f };
        var count = distance < BreakRadius * .55f ? 5 : 3;
        for (var board = 0; board < count; board++)
        {
            var length = 1.15f + rng.RandfRange(-.25f, .35f);
            var towardRoad = board % 2 == 0;
            // Boards are spaced along the run instead of dropped on top of one
            // another: two planks crossing in an X was the tell that the pile
            // was random rather than where a car pushed them.
            var along = (board - (count - 1) * .5f) * (length + .22f);
            var at = new Vector3(
                centre.X + (towardRoad ? rng.RandfRange(-.62f, -.34f) : rng.RandfRange(.32f, 1.05f)),
                Ground(centre.X, centre.Z) + .03f + (board % 2) * .045f,
                centre.Z + along + rng.RandfRange(-.16f, .16f));
            if (at.DistanceTo(GuyFilmingSpot with { Y = at.Y }) < .85f) continue;
            var debris = new MeshInstance3D
            {
                Name = $"CrashDebris_{_debris.Count}",
                Mesh = new BoxMesh
                {
                    Size = new(length, .045f, .15f),
                    Material = board % 3 == 0 ? woodDark : wood
                },
                Position = at,
                RotationDegrees = new(rng.RandfRange(-3f, 3f), rng.RandfRange(0f, 360f), rng.RandfRange(-4f, 4f))
            };
            AddChild(debris);
            _debris.Add(debris);
            if (!animate) continue;
            var slide = CreateTween();
            slide.SetTrans(Tween.TransitionType.Cubic);
            slide.SetEase(Tween.EaseType.Out);
            slide.TweenProperty(debris, "position",
                at + new Vector3((at.X - impactPoint.X) * .12f, 0f, rng.RandfRange(-.22f, .22f)),
                .55f + rng.RandfRange(0f, .35f)).SetDelay(rng.RandfRange(0f, .25f));
        }
    }

    private void StageRestState()
    {
        _tamara.Visible = true;
        _tamara.GlobalPosition = new(TamaraRest.X, Ground(TamaraRest.X, TamaraRest.Z), TamaraRest.Z);
        FaceDirection(_tamara, Vector3.Left);
        GeneratedCharacterKitDressing.PlayClip(_tamara, "Idle");
        _guy.Visible = true;
        _guy.GlobalPosition = new(GuyRest.X, Ground(GuyRest.X, GuyRest.Z), GuyRest.Z);
        FaceTowards(_guy, BoardSpots[2].At with { Y = 0 });
        _guyAnimation?.Play("Rinat_Idle");
        SetGuyFilming(false);
    }

    internal void SetGuyFilming(bool filming)
    {
        if (_phoneMesh is { } phone) phone.Visible = filming;
        _guyFilming = filming;
        if (!filming) _guyLaugh = 0f;
    }

    /// <summary>Bone name -> where the bone should point, in the skeleton's own
    /// space. The human kit faces +Z with its right side at -X, so this reads
    /// as: upper arm hangs down and a little forward, the elbow folds, and the
    /// forearm and hand carry the phone out in front of the chest.</summary>
    private static readonly (string Bone, Vector3 Aim)[] FilmingAims =
    {
        ("upperarm_r", new(-.20f, -.92f, .34f)),
        ("lowerarm_r", new(.24f, .46f, .86f)),
        ("hand_r", new(.08f, .40f, .91f))
    };

    /// <summary>The cutscene hands over the laugh level; the pose itself is
    /// held every frame in _Process, after the idle clip has written its own.</summary>
    internal void UpdateGuyFilming(double delta, float laugh) => _guyLaugh = laugh;

    private void HoldFilmingPose()
    {
        if (!_guyFilming || _guySkeleton is null) return;
        var skeleton = _guySkeleton;
        foreach (var (bone, aim) in FilmingAims)
        {
            AimBone(skeleton, bone, aim);
        }

        if (_guyLaugh > 0)
        {
            var time = Time.GetTicksMsec() * .001f;
            var shake = Mathf.Sin(time * 13f) * .05f * _guyLaugh;
            NudgeBone(skeleton, "spine_03", new Basis(Vector3.Back, shake) * new Basis(Vector3.Right, shake * .35f));
            NudgeBone(skeleton, "neck_01", new Basis(Vector3.Right, .10f + Mathf.Sin(time * 9f) * .05f * _guyLaugh));
        }

        OrientPhoneTowardsFace();
    }

    /// <summary>Turns the phone so its screen faces the boy holding it, whatever
    /// the arm is doing: a slab glued to the wrist reads as a prop the moment
    /// its screen points at the ground.</summary>
    private void OrientPhoneTowardsFace()
    {
        if (_phoneMesh is not { } phone || _guySkeleton is null) return;
        if (!GodotObject.IsInstanceValid(phone) || !phone.IsInsideTree()) return;
        var neck = _guySkeleton.FindBone("neck_01");
        if (neck < 0) return;
        var face = _guy.ToGlobal(_guySkeleton.GetBoneGlobalPose(neck).Origin);
        var toFace = face - phone.GlobalPosition;
        if (toFace.LengthSquared() < .0001f) return;
        phone.GlobalBasis = Basis.LookingAt(toFace.Normalized(), Vector3.Up);
    }

    /// <summary>Rotates one bone so its length points along <paramref name="aim"/>,
    /// keeping the pose's own twist. Poses come from the animation clip, so the
    /// result stays a living idle with one arm raised, not a stiff mannequin.</summary>
    private static void AimBone(Skeleton3D skeleton, string bone, Vector3 aim)
    {
        var index = skeleton.FindBone(bone);
        if (index < 0) return;
        var global = GlobalBonePose(skeleton, index);
        var current = global.Basis.Y.Normalized();
        var wanted = aim.Normalized();
        var axis = current.Cross(wanted);
        if (axis.LengthSquared() < 1e-8f) return;
        var rotation = new Basis(axis.Normalized(), current.AngleTo(wanted));
        var parent = skeleton.GetBoneParent(index);
        var parentGlobal = parent >= 0 ? GlobalBonePose(skeleton, parent) : Transform3D.Identity;
        skeleton.SetBonePose(index,
            parentGlobal.AffineInverse() * new Transform3D(rotation * global.Basis, global.Origin));
    }

    private static void NudgeBone(Skeleton3D skeleton, string bone, Basis extra)
    {
        var index = skeleton.FindBone(bone);
        if (index < 0) return;
        var pose = skeleton.GetBonePose(index);
        skeleton.SetBonePose(index, new Transform3D(extra * pose.Basis, pose.Origin));
    }

    /// <summary>Walks the parent chain instead of trusting the skeleton's cached
    /// globals, because several bones are aimed in the same frame.</summary>
    private static Transform3D GlobalBonePose(Skeleton3D skeleton, int index)
    {
        var chain = new List<int>();
        for (var bone = index; bone >= 0; bone = skeleton.GetBoneParent(bone))
        {
            chain.Add(bone);
        }

        var result = Transform3D.Identity;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            result *= skeleton.GetBonePose(chain[i]);
        }

        return result;
    }

    // ---- projection -----------------------------------------------------------

    private void OnRuntimeStateChanged()
    {
        if (_bridge is null) return;
        if (_bridge.SessionIdentity is { } session && !ReferenceEquals(_session, session))
        {
            _session = session;
            _questBannerShown = false;
            _remarkSaid = false;
            _callbackDone = false;
        }

        if (_cutscene is not null) return;
        var snapshot = TamaraFenceSnapshotNow();
        if (snapshot is null) return;

        if (snapshot.Crashed && !snapshot.Repaired && !FenceBreakApplied)
        {
            // Loaded into a crashed world whose scene never played this
            // session: the breach simply exists, people at their rest spots.
            ApplyBreak(FenceCenter, animate: false);
        }

        if (snapshot.Repaired && !_repairApplied)
        {
            // A completed quest loads straight into the mended fence.
            ApplyRepair();
        }

        StageFromSnapshot(snapshot);
        ProjectBoards(snapshot);
    }

    private void StageFromSnapshot(RuntimeBridge.TamaraFenceSnapshot snapshot)
    {
        _tamara.Visible = snapshot.Crashed;
        _guy.Visible = snapshot.Crashed;
        if (!snapshot.Crashed) return;
        _tamara.GlobalPosition = new(TamaraRest.X, Ground(TamaraRest.X, TamaraRest.Z), TamaraRest.Z);
        FaceDirection(_tamara, Vector3.Left);
        GeneratedCharacterKitDressing.PlayClip(_tamara, "Idle");
        _guy.GlobalPosition = new(GuyRest.X, Ground(GuyRest.X, GuyRest.Z), GuyRest.Z);
        FaceTowards(_guy, BoardSpots[2].At with { Y = 0 });
    }

    private void ProjectBoards(RuntimeBridge.TamaraFenceSnapshot snapshot)
    {
        var props = _bridge is null ? default : _bridge.SelectWorldProps();
        for (var index = 0; index < _boardVisuals.Count; index++)
        {
            _boardVisuals[index].Visible = snapshot.Crashed && !snapshot.Repaired;
            if (props.ValueKind == System.Text.Json.JsonValueKind.Object
                && props.TryGetProperty($"tamara/board/{index + 1}", out var record)
                && record.ValueKind == System.Text.Json.JsonValueKind.Object
                && record.TryGetProperty("taken", out var taken)
                && taken.ValueKind == System.Text.Json.JsonValueKind.True)
            {
                _boardVisuals[index].Visible = false;
            }
        }
    }

    private void UpdateRepairPresentation()
    {
        if (_repairApplied || _cutscene is not null) return;
        var snapshot = TamaraFenceSnapshotNow();
        if (snapshot is not { Repaired: true }) return;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null) return;
        // The repair lands when the yard is unattended or after a fresh load;
        // the player never watches boards jump back into place.
        if (player.GlobalPosition.DistanceTo(FenceCenter) < 15f && !_repairDeferredByLoad) return;
        ApplyRepair();
    }

    private bool _repairDeferredByLoad;

    /// <summary>The whole run is repainted when Tamara fixes it: fresh boards
    /// replace the old peach-pink shields, every panel stands straight again
    /// and the debris is carted off. The pillars never moved.</summary>
    private void ApplyRepair()
    {
        _repairApplied = true;
        foreach (var debris in _debris.Where(debris => GodotObject.IsInstanceValid(debris)).ToArray())
        {
            debris.QueueFree();
        }
        _debris.Clear();
        var span = MathF.Abs(FenceZEnd - FenceZStart) / PanelCount;
        for (var index = 0; index < _panels.Count; index++)
        {
            var panel = _panels[index];
            panel.Mesh.QueueFree();
            panel.Root.RotationDegrees = Vector3.Zero;
            panel.Root.Position = new Vector3(FenceFaceX, panel.Root.Position.Y, panel.Root.Position.Z);
            panel.Mesh = new MeshInstance3D
            {
                Name = $"PanelBoards_{index}",
                Mesh = PanelMesh(50 + index, span - .24f, weathered: false),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On
            };
            panel.Root.AddChild(panel.Mesh);
            panel.Broken = false;
            panel.Body.CollisionLayer = 1u;
            panel.Body.SetMeta("tamaraFenceRepaired", true);
        }
        SetMeta("tamaraFenceRepaired", true);
    }

    // ---- ambient life ---------------------------------------------------------

    private void UpdateAmbientBehaviour(double delta)
    {
        if (_cutscene is not null || _bridge?.SessionIdentity is null) return;
        var snapshot = TamaraFenceSnapshotNow();
        if (snapshot is null) return;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null) return;

        if (!_remarkSaid && snapshot is { Crashed: true, Repaired: false, Delivered: < 6 }
            && !player.VehicleControlled
            && player.GlobalPosition.DistanceTo(BoardSpots[2].At with { Y = player.GlobalPosition.Y }) < 3.4f)
        {
            _remarkSaid = true;
            PlayAmbientRemark();
        }

        if (!_callbackDone && snapshot is { Repaired: true }
            && player.VehicleControlled
            && player.GlobalPosition.DistanceTo(FenceCenter) < 14f
            && player.Velocity.Length() < 3.2f)
        {
            _callbackDone = true;
            PlayQuietCallback();
        }

        if (_callbackFilming)
        {
            UpdateGuyFilming(delta, 0f);
        }
    }

    private bool _callbackFilming;

    /// <summary>The one quiet callback: the Niva rolls past the mended fence and
    /// the guy simply raises his phone again. No line, no UI, no new state.</summary>
    private async void PlayQuietCallback()
    {
        if (!_guy.Visible) return;
        _callbackFilming = true;
        var tween = CreateTween();
        var restYaw = _guy.RotationDegrees.Y;
        var toCar = (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.GlobalPosition ?? _guy.GlobalPosition;
        var direction = toCar - _guy.GlobalPosition;
        var targetYaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
        tween.TweenProperty(_guy, "rotation:y", Mathf.DegToRad(targetYaw), .5f);
        SetGuyFilming(true);
        await ToSignal(GetTree().CreateTimer(4.5), SceneTreeTimer.SignalName.Timeout);
        SetGuyFilming(false);
        var back = CreateTween();
        back.TweenProperty(_guy, "rotation:y", Mathf.DegToRad(restYaw), .7f);
        _callbackFilming = false;
    }

    private async void PlayAmbientRemark()
    {
        var strip = CaptionStrip.Ensure(GetTree());
        if (_bridge is null) return;
        var guy = _bridge.ResolveText("urman.chapter1:text/tamara-remark-guy-boards");
        var aidar = _bridge.ResolveText("urman.chapter1:text/tamara-remark-aidar-yes");
        var dry = _bridge.ResolveText("urman.chapter1:text/tamara-remark-guy-dry");
        FaceTowards(_guy, (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.GlobalPosition ?? _guy.GlobalPosition);
        await strip.ShowAsync("ПАРЕНЬ С ТЕЛЕФОНОМ", guy, 1.6f);
        await strip.ShowAsync("АЙДАР", aidar, 1.0f);
        await strip.ShowAsync("ПАРЕНЬ С ТЕЛЕФОНОМ", dry, 2.4f);
        FaceTowards(_guy, BoardSpots[2].At with { Y = 0 });
    }

    private void PresentQuestBanner()
    {
        if (_questBannerShown) return;
        var snapshot = TamaraFenceSnapshotNow();
        if (snapshot is not { Crashed: true }) return;
        _questBannerShown = true;
        PresentBanner("Забор Тамары Геннадьевны", "Принести Тамаре Геннадьевне 6 целых досок", "0 / 6");
    }

    public override void _ExitTree()
    {
        if (_bridge is not null && GodotObject.IsInstanceValid(_bridge))
        {
            _bridge.RuntimeStateChanged -= OnRuntimeStateChanged;
        }
    }

    internal void MarkRepairDeferredByLoadForTest()
    {
        _repairDeferredByLoad = true;
    }

    internal bool RemarkSaid => _remarkSaid;
    internal bool QuietCallbackDone => _callbackDone;

    /// <summary>Test-only entry into the same impact path the fleet's physics uses.</summary>
    internal void DebugHandleHardStop(VehicleController vehicle, float speed, Node collider) =>
        OnVehicleHardStop(vehicle, speed, collider);

    private double _cutsceneTestSpeed = 1.0;

    internal void AccelerateCutsceneForTest(double speed) => _cutsceneTestSpeed = speed;

    /// <summary>Capture hook: shots are listed from the moment the scene is
    /// created, so even the opening cut is photographed.</summary>
    internal Action<string>? CutsceneShotListenerForTest
    {
        get;
        set;
    }

    internal void DebugExposeGuyForTest(out Node3D guy, out Skeleton3D? skeleton,
        out AnimationPlayer? animation, out MeshInstance3D? phone)
    {
        guy = _guy;
        skeleton = _guySkeleton;
        animation = _guyAnimation;
        phone = _phoneMesh;
    }
}
