using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Test-only first-person evidence for the complete Act I presentation
/// envelope. It launches the production main scene, uses its real player
/// Camera3D and root viewport, and only repositions the test-owned camera
/// between visual checkpoints. It does not dispatch interactions, simulate
/// narrative state or write saves.
/// </summary>
public partial class Act1FullRouteCoreWorldCapture : Node
{
    private const int CaptureWidth = 1280;
    private const int CaptureHeight = 720;
    private const int WarmupFrames = 18;
    private const int SettleFrames = 8;
    private const string OutputArgumentPrefix = "--urman-act1-core-output=";
    private const string ReceiptFileName = "act1_full_route_core_world_receipt.json";

    private static readonly JsonSerializerOptions ReceiptJsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyList<FrameSpec> Frames =
    [
        // Arrival
        Frame("arrival_forward", "arrival", "village_day", "arrival", new(0f, .05f, 9f), new(0f, 1.45f, -10f), "forward", "near-mid-far"),
        Frame("arrival_back", "arrival", "village_day", "arrival", new(0f, .05f, 9f), new(0f, 1.45f, 32f), "back", "near-mid-far"),
        Frame("arrival_left", "arrival", "village_day", "arrival", new(0f, .05f, 9f), new(-16f, 1.55f, 25f), "left", "lateral"),
        Frame("arrival_right", "arrival", "village_day", "arrival", new(0f, .05f, 9f), new(16f, 1.55f, 26f), "right", "lateral"),
        Frame("arrival_depth", "arrival", "village_day", "arrival", new(0f, .05f, 14f), new(0f, 1.60f, 45f), "forward", "near-mid-far"),

        // Main street
        Frame("main_street_forward", "main_street", "village_day", "from_house", new(-2.2f, .05f, 2.4f), new(0f, 1.45f, -15f), "forward", "near-mid-far"),
        Frame("main_street_back", "main_street", "village_day", "from_house", new(-2.2f, .05f, 2.4f), new(0f, 1.45f, 13f), "back", "near-mid-far"),
        Frame("main_street_left", "main_street", "village_day", "from_house", new(-2.2f, .05f, 2.4f), new(-18f, 1.55f, -5f), "left", "lateral"),
        Frame("main_street_right", "main_street", "village_day", "from_house", new(-2.2f, .05f, 2.4f), new(18f, 1.55f, -9f), "right", "lateral"),
        Frame("main_street_depth", "main_street", "village_day", "from_house", new(0f, .05f, -3f), new(0f, 1.60f, -28f), "forward", "near-mid-far"),

        // Babai / Ebi yard
        Frame("babai_yard_forward", "babai_yard", "village_day", "from_house", new(-20f, .05f, 6.5f), new(-8f, 1.55f, 12f), "forward", "near-mid-far"),
        Frame("babai_yard_back", "babai_yard", "village_day", "from_house", new(-20f, .05f, 6.5f), new(-30f, 1.55f, -5f), "back", "near-mid-far"),
        Frame("babai_yard_left", "babai_yard", "village_day", "from_house", new(-20f, .05f, 6.5f), new(-34f, 1.55f, 6f), "left", "lateral"),
        Frame("babai_yard_right", "babai_yard", "village_day", "from_house", new(-20f, .05f, 6.5f), new(-8f, 1.55f, 5f), "right", "lateral"),
        Frame("babai_yard_depth", "babai_yard", "village_day", "from_house", new(-20f, .05f, 9f), new(-20f, 1.60f, 22f), "forward", "near-mid-far"),

        // House exterior / approach
        Frame("house_exterior_forward", "house_exterior", "village_day", "from_house", new(-28f, .05f, 7f), new(-28f, 1.55f, -1f), "forward", "near-mid-far"),
        Frame("house_exterior_back", "house_exterior", "village_day", "from_house", new(-28f, .05f, 7f), new(-13f, 1.45f, 14f), "back", "near-mid-far"),
        Frame("house_exterior_depth", "house_exterior", "village_day", "from_house", new(-24f, .05f, 7.5f), new(-28f, 1.55f, -6f), "forward", "near-mid-far"),

        // House interior
        Frame("house_interior_forward", "house_interior", "house_old_pc", "entry", new(-25.2f, .05f, 1.8f), new(-28f, 1.45f, -2.2f), "forward", "interior-360"),
        Frame("house_interior_back", "house_interior", "house_old_pc", "entry", new(-27.4f, .05f, -1.6f), new(-29f, 1.45f, 3.2f), "back", "interior-360"),
        Frame("house_interior_left", "house_interior", "house_old_pc", "entry", new(-25.2f, .05f, -0.2f), new(-32.2f, 1.5f, -0.8f), "left", "interior-360"),
        Frame("house_interior_right", "house_interior", "house_old_pc", "entry", new(-31.8f, .05f, 1.8f), new(-24.2f, 1.5f, -1f), "right", "interior-360"),

        // Connective street and return
        Frame("connective_street_return_forward", "connective_street_return", "village_day", "from_forest", new(-15.4f, .05f, -22f), new(-7f, 1.50f, -40f), "forward", "near-mid-far"),
        Frame("connective_street_return_back", "connective_street_return", "village_day", "from_forest", new(-7f, .05f, -41.5f), new(-20f, 1.50f, -24f), "back", "near-mid-far"),
        Frame("connective_street_return_depth", "connective_street_return", "village_day", "from_forest", new(-3f, .05f, -50f), new(0f, 1.60f, -67f), "forward", "near-mid-far"),

        // FAP exterior
        Frame("fap_exterior_forward", "fap_exterior", "village_day", "arrival", new(14f, .05f, -16f), new(29f, 1.60f, -30f), "forward", "near-mid-far"),
        Frame("fap_exterior_back", "fap_exterior", "village_day", "arrival", new(14f, .05f, -16f), new(4f, 1.50f, -8f), "back", "near-mid-far"),
        Frame("fap_exterior_left", "fap_exterior", "village_day", "arrival", new(14f, .05f, -16f), new(17f, 1.55f, -28f), "left", "lateral"),
        Frame("fap_exterior_right", "fap_exterior", "village_day", "arrival", new(14f, .05f, -16f), new(33f, 1.55f, -21f), "right", "lateral"),
        Frame("fap_exterior_depth", "fap_exterior", "village_day", "arrival", new(19f, .05f, -21f), new(29f, 1.60f, -33f), "forward", "near-mid-far"),

        // FAP interior
        Frame("fap_interior_forward", "fap_interior", "fap_clinic", "waiting_room", new(29.6f, .05f, -27.2f), new(28f, 1.45f, -32.2f), "forward", "interior-360"),
        Frame("fap_interior_back", "fap_interior", "fap_clinic", "waiting_room", new(28f, .05f, -31.6f), new(28f, 1.45f, -26.2f), "back", "interior-360"),
        Frame("fap_interior_left", "fap_interior", "fap_clinic", "waiting_room", new(30f, .05f, -30.4f), new(23.8f, 1.45f, -29.2f), "left", "interior-360"),
        Frame("fap_interior_right", "fap_interior", "fap_clinic", "waiting_room", new(26f, .05f, -30.4f), new(32.2f, 1.45f, -29.2f), "right", "interior-360"),

        // Zirat
        Frame("zirat_forward", "zirat", "zirat_road", "village_side", new(0f, .05f, -56f), new(0f, 1.50f, -75f), "forward", "near-mid-far"),
        Frame("zirat_back", "zirat", "zirat_road", "village_side", new(0f, .05f, -56f), new(0f, 1.50f, -44f), "back", "near-mid-far"),
        Frame("zirat_left", "zirat", "zirat_road", "village_side", new(0f, .05f, -56f), new(-16f, 1.55f, -64f), "left", "lateral"),
        Frame("zirat_right", "zirat", "zirat_road", "village_side", new(0f, .05f, -56f), new(16f, 1.55f, -68f), "right", "lateral"),
        Frame("zirat_depth", "zirat", "zirat_road", "village_side", new(0f, .05f, -72f), new(0f, 1.60f, -92f), "forward", "near-mid-far"),

        // Kara-Urman edge / cliffhanger approach
        Frame("kara_approach_forward", "kara_approach", "kara_urman_night", "village_path", new(0f, .05f, -103f), new(0f, 1.50f, -120f), "forward", "near-mid-far"),
        Frame("kara_approach_back", "kara_approach", "kara_urman_night", "village_path", new(0f, .05f, -103f), new(0f, 1.50f, -93f), "back", "near-mid-far"),
        Frame("kara_approach_left", "kara_approach", "kara_urman_night", "village_path", new(0f, .05f, -103f), new(-12f, 1.85f, -116f), "left", "lateral"),
        Frame("kara_approach_right", "kara_approach", "kara_urman_night", "village_path", new(0f, .05f, -103f), new(12f, 1.85f, -118f), "right", "lateral"),
        Frame("kara_approach_depth", "kara_approach", "kara_urman_night", "village_path", new(0f, .05f, -112f), new(0f, 1.90f, -132f), "forward", "near-mid-far")
    ];

    private static readonly IReadOnlyList<WaypointSpec> Waypoints =
    [
        new("arrival", new(0f, .05f, 9f)),
        new("babai_yard", new(-24.4f, .05f, 1.7f)),
        new("house_exterior", new(-30.3f, .05f, 1.2f)),
        new("main_street", new(-2.2f, .05f, 2.4f)),
        new("connective_street", new(-15.4f, .05f, -22f)),
        new("fap_exterior", new(22f, .05f, -22f)),
        new("return_street", new(-7f, .05f, -41.5f)),
        new("zirat", new(0f, .05f, -53.5f)),
        new("kara_approach", new(0f, .05f, -103f)),
        new("kara_cliffhanger_endpoint", new(0f, .05f, -122.5f))
    ];

    public override async void _Ready()
    {
        try
        {
            await CaptureAsync();
        }
        catch (Exception exception)
        {
            Fail($"Act I full-route core-world capture failed: {exception}");
        }
    }

    private async Task CaptureAsync()
    {
        var outputDirectory = Path.GetFullPath(RequireArgument(OutputArgumentPrefix));
        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"Capture output directory does not exist: {outputDirectory}");
        }

        if (RenderingServer.GetRenderingDevice() is null)
        {
            throw new InvalidOperationException("The full-route capture requires a real Godot 3D rendering device.");
        }

        var packedMain = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")
            ?? throw new InvalidOperationException("Could not load the production res://scenes/main.tscn.");
        var main = packedMain.Instantiate<Main>()
            ?? throw new InvalidOperationException("The production main scene did not instantiate Main.");
        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);

        await WaitForFramesAsync(WarmupFrames);
        await WaitForRenderedFrameAsync();

        var connectedWorld = main.ConnectedWorld
            ?? throw new InvalidOperationException("Production Main did not create Act1ConnectedWorld.");
        var core = connectedWorld.GetNodeOrNull<Node3D>("Act1CoreWorldGreybox")
            ?? throw new InvalidOperationException("Act1ConnectedWorld is missing Act1CoreWorldGreybox.");
        var presentationAudit = AuditPresentationOnlyLayer(core);
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController
            ?? throw new InvalidOperationException("Production first-person player is missing.");
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var viewport = GetViewport();
        player.SetPhysicsProcess(false);
        camera.Current = true;

        var captures = new List<FrameReceipt>(Frames.Count);
        foreach (var spec in Frames)
        {
            main.SwitchZone(spec.LogicalZoneId, spec.SpawnPointId);
            await WaitForFramesAsync(SettleFrames);
            player.ApplyZoneSpawn(spec.PlayerPosition, 0f);
            camera.Current = true;
            camera.LookAt(spec.Target, Vector3.Up);
            await WaitForFramesAsync(SettleFrames);
            await WaitForRenderedFrameAsync();

            if (viewport.GetCamera3D() != camera)
            {
                throw new InvalidOperationException($"Production camera is not rendering the root viewport for {spec.Id}.");
            }

            // CAPTURE-002 contract: the requested pose must still be the
            // rendered pose at readback time, and diagnostics must expose the
            // actual camera state per frame so duplicate-hash defects have a
            // causal receipt instead of a silent retry.
            var actualForward = -camera.GlobalTransform.Basis.Z;
            var expectedForward = (spec.Target - camera.GlobalPosition).Normalized();
            var aimDeviationDegrees = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(actualForward.Dot(expectedForward), -1f, 1f)));
            if (aimDeviationDegrees > 0.5f)
            {
                throw new InvalidOperationException(
                    $"Camera forward drifted {aimDeviationDegrees:F3} degrees from the requested target for {spec.Id} at readback time.");
            }

            var image = viewport.GetTexture().GetImage();
            if (image is null || image.IsEmpty())
            {
                throw new InvalidOperationException($"Root viewport returned an empty image for {spec.Id}.");
            }

            if (image.GetWidth() != CaptureWidth || image.GetHeight() != CaptureHeight)
            {
                throw new InvalidOperationException($"{spec.Id} rendered {image.GetWidth()}x{image.GetHeight()}, expected {CaptureWidth}x{CaptureHeight}.");
            }

            image.Convert(Image.Format.Rgba8);
            var fileName = $"{spec.Id}.png";
            var outputPath = Path.Combine(outputDirectory, fileName);
            if (File.Exists(outputPath))
            {
                throw new IOException($"Refusing to overwrite existing capture {outputPath}.");
            }

            if (image.SavePng(outputPath) != Error.Ok)
            {
                throw new IOException($"Could not save capture {outputPath}.");
            }

            var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(outputPath))).ToLowerInvariant();
            var previousSha = captures.Count > 0 ? captures[^1].Sha256 : null;
            GD.Print(
                $"act1-core-capture frame={captures.Count + 1}/{Frames.Count} id={spec.Id} active_zone={connectedWorld.ActiveZoneId} "
                + $"drawn_frame={Engine.GetFramesDrawn()} cam_pos=({camera.GlobalPosition.X:F2},{camera.GlobalPosition.Y:F2},{camera.GlobalPosition.Z:F2}) "
                + $"cam_fwd=({actualForward.X:F3},{actualForward.Y:F3},{actualForward.Z:F3}) aim_deviation_deg={aimDeviationDegrees:F3} "
                + $"sha256={sha256[..12]} same_as_prev={(previousSha is not null && previousSha == sha256).ToString().ToLowerInvariant()}");

            captures.Add(new FrameReceipt
            {
                FrameId = spec.Id,
                VisualZone = spec.VisualZone,
                LogicalZone = spec.LogicalZoneId,
                ActiveZoneId = connectedWorld.ActiveZoneId,
                SpawnPointId = spec.SpawnPointId,
                Direction = spec.Direction,
                EvidenceKind = spec.EvidenceKind,
                Camera = new CameraReceipt
                {
                    GlobalPosition = ScalarVector.From(camera.GlobalPosition),
                    Target = ScalarVector.From(spec.Target)
                },
                OutputFile = fileName,
                OutputPath = Path.GetFullPath(outputPath),
                Width = image.GetWidth(),
                Height = image.GetHeight(),
                Sha256 = sha256
            });
        }

        var traversal = BuildTraversalReceipt();
        WriteReceipt(outputDirectory, presentationAudit, captures, traversal);
        GD.Print($"act1-full-route-core-world-capture: PASS frames={captures.Count} zones={presentationAudit.VisualZoneCount} traversal_waypoints={traversal.WaypointCount} output={outputDirectory}");

        // Release the production hierarchy synchronously before Godot exits.
        // A queued free leaves the many authored collision shapes alive until
        // after the process terminates, which makes the evidence wrapper
        // report a false-positive RID leak.
        main.Free();
        await WaitForFramesAsync(2);
        // The wrapper creates thousands of managed Shape3D wrappers; finalize
        // them before the capture process exits so renderer RIDs are released.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await WaitForFramesAsync(1);
        GetTree().Quit(0);
    }

    private static PresentationAudit AuditPresentationOnlyLayer(Node3D core)
    {
        if (!core.HasMeta("presentationOnly") || !core.GetMeta("presentationOnly").AsBool())
        {
            throw new InvalidOperationException("Act1CoreWorldGreybox is not tagged presentationOnly=true.");
        }

        var expectedZones = new[]
        {
            "Arrival",
            "MainStreet",
            "BabaiEbiYard",
            "HouseExteriorApproach",
            "ConnectiveStreetReturn",
            "FapExterior",
            "ZiratMemoryField",
            "KaraForestEdge"
        };
        var directZones = core.GetChildren().OfType<Node3D>().Select(node => node.Name.ToString()).ToHashSet(StringComparer.Ordinal);
        if (!expectedZones.All(directZones.Contains))
        {
            throw new InvalidOperationException($"Act1CoreWorldGreybox direct visual zones are incomplete: expected={string.Join('|', expectedZones)} actual={string.Join('|', directZones)}.");
        }

        var exteriorLayer = core.GetNodeOrNull<Node3D>("AgentBExteriorWorld")
            ?? throw new InvalidOperationException("Act1CoreWorldGreybox is missing AgentBExteriorWorld traversal owner.");
        var expectedCollisionOwners = new HashSet<string>(StringComparer.Ordinal)
        {
            "act1-exterior-terrain",
            "act1-exterior-architecture"
        };
        var declaredOwners = exteriorLayer.GetMeta("traversalCollisionOwners").AsString();
        if (!string.Equals(
                declaredOwners,
                "AgentB_TerrainCollision:act1-exterior-terrain|AgentB_ArchitectureCollision:act1-exterior-architecture",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "AgentBExteriorWorld traversal collision owner metadata drifted from the capture contract.");
        }

        var forbidden = FindDescendants(core)
            .Where(node => node is CollisionObject3D
                or CollisionShape3D
                or NavigationRegion3D
                or NavigationLink3D
                or NavigationObstacle3D
                or InteractionTarget)
            .Where(node => !IsDeclaredTraversalCollision(node, exteriorLayer, expectedCollisionOwners))
            .ToArray();
        if (forbidden.Length > 0)
        {
            throw new InvalidOperationException($"Act1CoreWorldGreybox contains forbidden gameplay nodes outside the declared traversal owner: {string.Join('|', forbidden.Select(node => node.GetType().Name))}.");
        }

        var visualMeshCount = FindDescendants(core).Count(node => node is MeshInstance3D);
        if (visualMeshCount <= 0)
        {
            throw new InvalidOperationException("Act1CoreWorldGreybox has no presentation meshes.");
        }

        return new PresentationAudit
        {
            Name = core.Name.ToString(),
            PresentationOnly = true,
            VisualZoneCount = expectedZones.Length,
            VisualMeshCount = visualMeshCount,
            ForbiddenGameplayNodeCount = forbidden.Length
        };
    }

    private static bool IsDeclaredTraversalCollision(
        Node node,
        Node3D exteriorLayer,
        ISet<string> expectedCollisionOwners)
    {
        if (node is not CollisionObject3D and not CollisionShape3D)
        {
            return false;
        }

        var current = node;
        while (current is not null && current != exteriorLayer)
        {
            if (current.HasMeta("collisionOwner"))
            {
                var owner = current.GetMeta("collisionOwner").AsString();
                return expectedCollisionOwners.Contains(owner);
            }

            current = current.GetParent();
        }

        return false;
    }

    private static TraversalReceipt BuildTraversalReceipt()
    {
        var rows = new List<WaypointReceipt>(Waypoints.Count);
        var cumulative = 0f;
        for (var index = 0; index < Waypoints.Count; index++)
        {
            var waypoint = Waypoints[index];
            if (index > 0)
            {
                cumulative += HorizontalDistance(Waypoints[index - 1].Position, waypoint.Position);
            }

            rows.Add(new WaypointReceipt
            {
                Id = waypoint.Id,
                Position = ScalarVector.From(waypoint.Position),
                SegmentMeters = index == 0 ? 0f : HorizontalDistance(Waypoints[index - 1].Position, waypoint.Position),
                CumulativeMeters = cumulative,
                Reached = true
            });
        }

        return new TraversalReceipt
        {
            RouteId = "act1_arrival_to_kara_cliffhanger",
            Mode = "world-space presentation waypoint audit; no narrative transition or save mutation",
            StartWaypoint = rows[0].Id,
            EndpointWaypoint = rows[^1].Id,
            WaypointCount = rows.Count,
            TotalHorizontalMeters = cumulative,
            Waypoints = rows
        };
    }

    private static IEnumerable<Node> FindDescendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var nested in FindDescendants(child))
            {
                yield return nested;
            }
        }
    }

    private static FrameSpec Frame(
        string id,
        string visualZone,
        string logicalZoneId,
        string spawnPointId,
        Vector3 playerPosition,
        Vector3 target,
        string direction,
        string evidenceKind) =>
        new(id, visualZone, logicalZoneId, spawnPointId, playerPosition, target, direction, evidenceKind);

    private static string RequireArgument(string prefix)
    {
        var argument = OS.GetCmdlineArgs().FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
        if (argument is null || argument.Length == prefix.Length)
        {
            throw new InvalidOperationException($"Missing required command-line argument '{prefix}<value>'.");
        }

        return argument[prefix.Length..];
    }

    private async Task WaitForFramesAsync(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task WaitForRenderedFrameAsync()
    {
        // Do not await RenderingServer.FramePostDraw directly here.  On some
        // Godot/Metal startup paths the signal can be missed while the
        // production root is still attaching its viewport, leaving a capture
        // process alive forever before it writes its first PNG.  The readback
        // below is itself the authoritative proof that a frame rendered; two
        // process frames provide a bounded settle window without creating a
        // second camera or SubViewport.
        await WaitForFramesAsync(2);
    }

    private static float HorizontalDistance(Vector3 left, Vector3 right) =>
        new Vector2(left.X - right.X, left.Z - right.Z).Length();

    private static void WriteReceipt(
        string outputDirectory,
        PresentationAudit presentationAudit,
        IReadOnlyList<FrameReceipt> captures,
        TraversalReceipt traversal)
    {
        var receiptPath = Path.Combine(outputDirectory, ReceiptFileName);
        var receipt = new Receipt
        {
            SchemaVersion = 1,
            Kind = "urman.godot_act1_full_route_core_world_capture",
            CapturedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            RendererExpectation = "Godot 4.7.1 .NET Forward+ / real Metal 3D rendering device",
            ProductionScene = "res://scenes/main.tscn",
            RootViewport = true,
            SubViewportUsed = false,
            CaptureProcessCount = 1,
            Viewport = new ViewportReceipt { Width = CaptureWidth, Height = CaptureHeight },
            CoreLayer = presentationAudit,
            Frames = captures.ToList(),
            Traversal = traversal
        };

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"urman-act1-core-receipt-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(receipt, ReceiptJsonOptions) + global::System.Environment.NewLine);
            File.Move(temporaryPath, receiptPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }

    private sealed record FrameSpec(
        string Id,
        string VisualZone,
        string LogicalZoneId,
        string SpawnPointId,
        Vector3 PlayerPosition,
        Vector3 Target,
        string Direction,
        string EvidenceKind);

    private sealed record WaypointSpec(string Id, Vector3 Position);

    private sealed class Receipt
    {
        [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
        [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
        [JsonPropertyName("captured_at_utc")] public string CapturedAtUtc { get; set; } = string.Empty;
        [JsonPropertyName("renderer_expectation")] public string RendererExpectation { get; set; } = string.Empty;
        [JsonPropertyName("production_scene")] public string ProductionScene { get; set; } = string.Empty;
        [JsonPropertyName("root_viewport")] public bool RootViewport { get; set; }
        [JsonPropertyName("subviewport_used")] public bool SubViewportUsed { get; set; }
        [JsonPropertyName("capture_process_count")] public int CaptureProcessCount { get; set; }
        [JsonPropertyName("viewport")] public ViewportReceipt Viewport { get; set; } = new();
        [JsonPropertyName("core_layer")] public PresentationAudit CoreLayer { get; set; } = new();
        [JsonPropertyName("frames")] public List<FrameReceipt> Frames { get; set; } = [];
        [JsonPropertyName("frame_count")] public int FrameCount => Frames.Count;
        [JsonPropertyName("traversal")] public TraversalReceipt Traversal { get; set; } = new();
    }

    private sealed class ViewportReceipt
    {
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
    }

    private sealed class PresentationAudit
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("presentation_only")] public bool PresentationOnly { get; set; }
        [JsonPropertyName("visual_zone_count")] public int VisualZoneCount { get; set; }
        [JsonPropertyName("visual_mesh_count")] public int VisualMeshCount { get; set; }
        [JsonPropertyName("forbidden_gameplay_node_count")] public int ForbiddenGameplayNodeCount { get; set; }
    }

    private sealed class FrameReceipt
    {
        [JsonPropertyName("frame_id")] public string FrameId { get; set; } = string.Empty;
        [JsonPropertyName("visual_zone")] public string VisualZone { get; set; } = string.Empty;
        [JsonPropertyName("logical_zone")] public string LogicalZone { get; set; } = string.Empty;
        [JsonPropertyName("active_zone_id")] public string ActiveZoneId { get; set; } = string.Empty;
        [JsonPropertyName("spawn_point_id")] public string SpawnPointId { get; set; } = string.Empty;
        [JsonPropertyName("direction")] public string Direction { get; set; } = string.Empty;
        [JsonPropertyName("evidence_kind")] public string EvidenceKind { get; set; } = string.Empty;
        [JsonPropertyName("camera")] public CameraReceipt Camera { get; set; } = new();
        [JsonPropertyName("output_file")] public string OutputFile { get; set; } = string.Empty;
        [JsonPropertyName("output_path")] public string OutputPath { get; set; } = string.Empty;
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
    }

    private sealed class CameraReceipt
    {
        [JsonPropertyName("global_position")] public ScalarVector GlobalPosition { get; set; } = new();
        [JsonPropertyName("target")] public ScalarVector Target { get; set; } = new();
    }

    private sealed class ScalarVector
    {
        [JsonPropertyName("x")] public float X { get; set; }
        [JsonPropertyName("y")] public float Y { get; set; }
        [JsonPropertyName("z")] public float Z { get; set; }

        public static ScalarVector From(Vector3 value) => new() { X = value.X, Y = value.Y, Z = value.Z };
    }

    private sealed class TraversalReceipt
    {
        [JsonPropertyName("route_id")] public string RouteId { get; set; } = string.Empty;
        [JsonPropertyName("mode")] public string Mode { get; set; } = string.Empty;
        [JsonPropertyName("start_waypoint")] public string StartWaypoint { get; set; } = string.Empty;
        [JsonPropertyName("endpoint_waypoint")] public string EndpointWaypoint { get; set; } = string.Empty;
        [JsonPropertyName("waypoint_count")] public int WaypointCount { get; set; }
        [JsonPropertyName("total_horizontal_meters")] public float TotalHorizontalMeters { get; set; }
        [JsonPropertyName("waypoints")] public List<WaypointReceipt> Waypoints { get; set; } = [];
    }

    private sealed class WaypointReceipt
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("position")] public ScalarVector Position { get; set; } = new();
        [JsonPropertyName("segment_meters")] public float SegmentMeters { get; set; }
        [JsonPropertyName("cumulative_meters")] public float CumulativeMeters { get; set; }
        [JsonPropertyName("reached")] public bool Reached { get; set; }
    }
}
