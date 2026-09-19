using System.Reflection;
using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Native local regression in the ordinary New Game. Only the outside starts
/// and the explicit carry fixture relocate the player; doorway legs use input.
/// Resource expiry advances the real runtime clock at a recorded time scale,
/// never by assigning flags. This does not measure human duration or listening.
/// </summary>
public partial class Act1FacilitiesSmokeTest : Node
{
    private const string BathObservation = "urman.chapter1:interaction/bathhouse-condensation-observation";
    private const string MosqueObservation = "urman.chapter1:interaction/mosque-visit";
    private static readonly string[] Actions = ["move_forward", "move_backward", "interact", "carry_place", "sprint", "crouch", "journal"];
    private static readonly FieldInfo BusyField = typeof(Act1ConnectedWorld).GetField("_facilityBusy", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Facility pending-action observer is missing.");
    private Act1DemoRoot _demo = null!;
    private FirstPersonController _player = null!;
    private Camera3D _camera = null!;
    private RuntimeBridge _bridge = null!;
    private CarryCoordinator _carry = null!;
    private Act1ConnectedWorld _world = null!;
    private Node3D _bath = null!;
    private Node3D _mosque = null!;
    private readonly List<object> _events = new();
    private int _checks;
    private double _walked;
    private string _initialBeats = string.Empty;
    private bool MosqueOnly => OS.GetCmdlineUserArgs().Contains("--urman-facilities-mosque-only", StringComparer.Ordinal);
    private bool MosqueContinuousRouteProof => OS.GetEnvironment("URMAN_MOSQUE_CONTINUOUS_ROUTE_PROOF") == "1";
    private string Output => System.Environment.GetEnvironmentVariable("URMAN_FACILITY_OUTPUT")
        ?? throw new InvalidOperationException("Set URMAN_FACILITY_OUTPUT to a new absolute evidence directory.");

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            if (!Path.IsPathFullyQualified(Output)) throw new InvalidOperationException("Facility evidence path must be absolute.");
            if (MosqueContinuousRouteProof && !MosqueOnly)
                throw new InvalidOperationException("The continuous route diagnostic requires the existing mosque-only scope.");
            if (File.Exists(Path.Combine(Output, "facilities-receipt.json"))) throw new IOException("This facility receipt already exists.");
            Check(DisplayServer.GetName() != "headless", "facility proof uses a real native game window");
            GetWindow().GrabFocus();
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(10);
            Check(await this.StartThroughMainMenuAsync(_demo), "production main menu starts an ordinary New Game");
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(8);
            _player = _demo.DemoMain.GetNode<FirstPersonController>("Player");
            _camera = _player.GetNode<Camera3D>("Head/Camera3D");
            _world = (Act1ConnectedWorld)GetTree().GetFirstNodeInGroup("act1_connected_world");
            _bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            _carry = (CarryCoordinator)GetTree().GetFirstNodeInGroup("carry_coordinator");
            await EnsureFocus();
            _bath = _world.GetNode<Node3D>("Act1CoreWorldGreybox/BabaiBathhouse");
            _mosque = _world.GetNode<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex/MosqueInterior");
            Check(!_player.ModalOpen && _world.ActiveZoneId == "village_day", "ordinary exterior controls are active");
            Check(!_world.CanUseFacilityInteraction(BathObservation) && !_world.CanUseFacilityInteraction(MosqueObservation),
                "arrival cannot inspect either distant interior");
            _initialBeats = _bridge.SelectRuntimeState().GetProperty("beats").GetRawText();
            Check(await _bridge.SaveSlotAsync("facilities-pristine"), "save the actual New Game before local fixtures");
            if (!MosqueOnly)
            {
                await CheckBathhouse();
                await Load("facilities-pristine");
                Check(Number("bathhouse/stove", "logsRemaining", 5) == 5 && Number("bathhouse/water", "ladles", 8) == 8
                    && !Flag("bathhouse/steam", "condensed") && JournalCount(BathObservation) == 0,
                    "pristine load resets consumed resources, condensation and future notebook entry");
            }
            await CheckMosque();
            if (!MosqueOnly)
            {
                await Load("facilities-pristine");
                await CheckOptionalMatches();
                await Load("facilities-pristine");
                await CheckHeldDoorObstacle();
            }
            Check(_bridge.SelectRuntimeState().GetProperty("beats").GetRawText() == _initialBeats,
                "local facility use does not advance the main story beats");
            Check(await _bridge.StartNewGameAsync(), "ordinary new session resets facility consequences");
            await Frames(10);
            Check(Number("bathhouse/stove", "logsRemaining", 5) == 5 && !Flag("mosque/entrance", "open")
                && _carry.HeldItem is null && JournalCount(MosqueObservation) == 0 && JournalCount(BathObservation) == 0,
                "new kernel contains no old door state, carried item or facility knowledge");
            exit = 0;
            GD.Print($"act1-facilities: PASS {_checks} checks, {_walked:0.00}m local input traversal; human/art/listening/performance not measured");
        }
        catch (Exception error)
        {
            _events.Add(new { kind = "failure", error = error.ToString(), aim = _carry?.DescribeAim(), feet = _player?.GlobalPosition.ToString() });
            GD.PrintErr("act1-facilities: FAIL " + error + "\n" + _carry?.DescribeAim());
            if (_camera is not null)
            {
                try { await Capture("failure"); }
                catch (Exception captureError) { _events.Add(new { kind = "capture-not-run", error = captureError.Message }); }
            }
        }
        finally
        {
            Engine.TimeScale = 1;
            ReleaseInputs();
            try
            {
                Directory.CreateDirectory(Output);
                using var receipt = new FileStream(Path.Combine(Output, "facilities-receipt.json"), FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(receipt, new { passed = exit == 0, scope = MosqueOnly ? "mosque-only" : "all-facilities", continuousRouteProof = MosqueContinuousRouteProof, checks = _checks, walkedMetres = _walked,
                    build = System.Environment.GetEnvironmentVariable("URMAN_FACILITY_BUILD_ID") ?? "not-supplied: use outer protected-run provenance",
                    engine = Engine.GetVersionInfo().ToString(), display = DisplayServer.GetName(),
                    fixture = "ordinary New Game, explicitly recorded supported local starts, input-only doorway legs; clock acceleration only while stationary",
                    humanDuration = "external/not-run", culturalReview = "external/not-run", listening = "external/not-run", performance = "not-measured",
                    events = _events }, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception error) { exit = 1; GD.PrintErr("Cannot preserve new facility evidence: " + error); }
            if (_demo is not null) await GodotSmokeCleanup.ReleaseAsync(_demo);
        }
        GetTree().Quit(exit);
    }

    private async Task CheckBathhouse()
    {
        CheckBathFoliageContactPair();
        Act1CarryRestProof.Check(_player, _carry, Check);
        var originalLog = _carry.Items.Single(item => item.ItemId == "carry-log");
        Check(Math.Abs(originalLog.GlobalPosition.X + 32.173938f) < .001f && Math.Abs(originalLog.GlobalPosition.Z - 4.706752f) < .001f,
            "new bath steps preserve the previously authored loose log position");
        await ApproachCarry(originalLog);
        Check(AimedAt(originalLog), "existing loose log retains a supported unobstructed pickup approach beside the repaired steps");
        var entrance = Target("urman.chapter1:local/bathhouse/entrance");
        var hinge = _bath.GetNode<Node3D>("BathEntranceHinge");
        var outside = _bath.GetMeta("entryApproach").AsVector3();
        var turn = _bath.GetMeta("entryTurn").AsVector3();
        var clearDoor = _bath.GetMeta("entryClearDoor").AsVector3();
        await LocalStart(outside, "outside existing Babai bathhouse footprint");
        await Capture("01_bathhouse_approach");
        await WalkTo(clearDoor, "climb the new lower flight along the clear yard side");
        await WalkTo(turn, "turn on the real upper landing toward the closed bath door");
        var before = Prop("bathhouse/entrance");
        await Use(entrance);
        Check(!Flag("bathhouse/entrance", "open") && Prop("bathhouse/entrance") == before && Math.Abs(hinge.Rotation.Y - Mathf.Pi) < .001f,
            "player in the opening sweep causes refusal without state or angle changes");
        using (var refusal = JsonDocument.Parse(entrance.GetMeta("doorSweepProbe", "{}").AsString()))
            Check(entrance.GetMeta("lastDoorActionResult", "not-called").AsString() == "blocked-by-full-shape-sweep"
                && refusal.RootElement.TryGetProperty("collider", out var blocker)
                && blocker.GetString() == _player.GetPath().ToString(),
                "the first door refusal is specifically the actual player collision; static obstructions cannot masquerade as this case");
        await Capture("02_bath_door_player_refusal");
        await WalkTo(clearDoor, "step out of the door sweep");
        await Use(entrance);
        await DoorAt(hinge, 82, "bath entrance opens its full 98-degree outward arc after clearing the player");
        Check(Target("urman.chapter1:local/bathhouse/stove").CollisionLayer == 4,
            "saving a door action keeps the stove interaction ray present");
        CheckBathEntryBypassSupport(turn);
        await WalkBathEntryBypass(turn, entering: true);
        Aim(_bath.ToGlobal(new(3.4f, .30f, 2.15f)));
        await Capture("02b_bath_open_door_bypass");
        Aim(hinge.ToGlobal(new(-.10f, .99f, 1.10f - .16f)));
        await Capture("02d_bath_handle_attachment");
        await WalkTo(_bath.ToGlobal(new(1.40f, 0, 1.57f)), "walk through the actual exterior bath doorway");
        Check(_world.FacilityInteriorAt(_player.GlobalPosition) == "bathhouse", "predbannik is recognized from the actual player position");
        await CheckShelter("bathhouse");
        await WalkTo(_bath.ToGlobal(new(.55f, 0, 1.32f)), "walk within the changing room");
        var wetDoor = Target("urman.chapter1:local/bathhouse/wet-door");
        var wetHinge = _bath.GetNode<Node3D>("BathWetRoomDoorHinge");
        Aim(wetDoor.GlobalPosition);
        await Capture("02c_bath_partition_before_open");
        await Use(wetDoor);
        await DoorAt(wetHinge, 185, "inner bath door opens from its real narrow room");
        await WalkTo(_bath.ToGlobal(new(.55f, 0, -.40f)), "walk through the second doorway into the wet room");
        await SetBathWetDoorFromInside(open: false);
        await WalkTo(_bath.ToGlobal(new(.55f, 0, -.40f)), "approach the heater after closing the real inner leaf");
        var stove = Target("urman.chapter1:local/bathhouse/stove");
        var water = Target("urman.chapter1:local/bathhouse/water");
        var observation = Target(BathObservation);
        var coldState = BathProps();
        await Use(water);
        Check(BathProps() == coldState && !Flag("bathhouse/steam", "condensed") && !observation.IsAvailable(),
            "water on cold stones gives a refusal with no resource or knowledge effect");
        RecordBathIgnitionState("before-first-ignition");
        await Use(stove);
        RecordBathIgnitionState("after-first-ignition");
        Check(Number("bathhouse/stove", "logsRemaining", 5) == 4 && VisibleLogs() == 4
            && Number("bathhouse/stove", "heatUntil") > _bridge.PlayTimeSeconds,
            "actual ignition consumes one of five visible logs and leaves physical heat");
        var litState = BathProps();
        await Use(stove);
        Check(BathProps() == litState && VisibleLogs() == 4, "repeat ignition while a log burns does not consume another log");
        await Use(water);
        Check(Number("bathhouse/water", "ladles", 8) == 7 && Flag("bathhouse/steam", "condensed")
            && _bath.GetNode<CpuParticles3D>("BathSteam").Emitting && _bath.GetNode<Node3D>("BathCondensation").Visible,
            "one real ladle atomically creates steam and visible condensation");
        await Capture("03_bath_stove_and_steam");
        var steamingState = BathProps();
        await Use(water);
        Check(BathProps() == steamingState, "repeat water during this steam pulse is refused without double consumption");
        await WalkTo(_bath.ToGlobal(new(.55f, 0, -1.55f)), "approach the actual wet window");
        Check(observation.IsAvailable(), "condensation makes the physical observation available inside the wet room");
        var paneAim = await CheckBathWindowSightlines(observation);
        await Capture("04_bath_condensation_before_observation", paneAim);
        await Use(observation, paneAim);
        await WaitUntil(() => JournalCount(BathObservation) == 1, "one authored bath observation enters the notebook");
        await ReadNotebookObservation(BathObservation, "04b_bath_observation_notebook");
        Check(!observation.IsAvailable() && JournalCount(BathObservation) == 1, "the same observation cannot make a second notebook entry");
        Check(await _bridge.SaveSlotAsync("facilities-bath-observed"), "save the attained steam, resources, position and notebook state");
        var savedFeet = _player.GlobalPosition;
        var savedWater = Number("bathhouse/water", "ladles", 8);
        var savedAngle = hinge.Rotation.Y;
        var savedWetAngle = wetHinge.Rotation.Y;
        await SetBathWetDoorFromInside(open: true);
        await WalkTo(_bath.ToGlobal(new(.55f, 0, 1.32f)), "leave the wet room through its actual doorway");
        await WalkTo(_bath.ToGlobal(new(1.40f, 0, 1.57f)), "cross the changing room toward the outside door");
        await WalkTo(turn, "exit onto the supported upper landing");
        await WalkBathEntryBypass(clearDoor, entering: false);
        await WalkTo(outside, "leave the bathhouse by the same physical steps");
        Check(_world.FacilityInteriorAt(_player.GlobalPosition) == string.Empty, "ordinary exit leaves the bath interior volume");
        await CheckShelter(string.Empty);
        await Load("facilities-bath-observed");
        Check(_player.GlobalPosition.DistanceTo(savedFeet) < .10f && _player.IsOnFloor()
            && Number("bathhouse/water", "ladles", 8) == savedWater && VisibleLogs() == 4
            && Flag("bathhouse/steam", "condensed") && JournalCount(BathObservation) == 1
            && Math.Abs(hinge.Rotation.Y - savedAngle) < .01f && Math.Abs(wetHinge.Rotation.Y - savedWetAngle) < .01f,
            "load restores supported indoor feet, exact exterior and wet-room door angles, consumed resources and single observation");
        await Capture("05_bath_saved_return");
        await CheckShelter("bathhouse");
        await CheckBathResources(stove, water);
        await SetBathWetDoorFromInside(open: true);
        await WalkTo(_bath.ToGlobal(new(.55f, 0, 1.32f)), "leave exhausted stove room");
        await WalkTo(_bath.ToGlobal(new(1.40f, 0, 1.57f)), "cross the changing room after resource checks");
        await WalkTo(turn, "cross the doorway onto the landing after resource checks");
        await WalkBathEntryBypass(clearDoor, entering: false);
        await WalkTo(outside, "physical return outside after resource checks");
        await WalkTo(clearDoor, "approach the door from outside its swept area");
        await Use(entrance);
        await DoorAt(hinge, 180, "close bath entrance from a clear position");
        await CheckLateDoorObstacle(entrance, hinge);
        await CheckBathDoorMigration(entrance, hinge);
    }

    private void RecordBathIgnitionState(string phase)
    {
        var target = Target("urman.chapter1:local/bathhouse/stove");
        var entry = new { kind = "bath-ignition-state", phase, playSeconds = _bridge.PlayTimeSeconds,
            playBlocks = _bridge.CapturePlayTimeBlocks().ToString(),
            logsRemaining = Number("bathhouse/stove", "logsRemaining", 5), visibleLogs = VisibleLogs(),
            burnUntil = Number("bathhouse/stove", "burnUntil"), heatUntil = Number("bathhouse/stove", "heatUntil"),
            props = BathProps(), physicalPredicate = _world.CanIgniteBathStove(),
            actualFireboxVisible = _bath.GetNode<MeshInstance3D>("BathStoveFirebox").IsVisibleInTree(),
            interactionCarrierVisible = target.IsVisibleInTree(), targetAvailable = target.IsAvailable(),
            actionNumber = target.GetMeta("lastIgnitionActionNumber", 0).AsInt32(),
            result = target.GetMeta("lastIgnitionResult", "not-called").AsString(), aim = _carry.DescribeAim() };
        _events.Add(entry);
        GD.Print("act1-facility-ignition: " + JsonSerializer.Serialize(entry));
    }

    private void CheckBathFoliageContactPair()
    {
        var owners = _bath.GetMeta("roofSuppressedFoliageOwners").AsStringArray();
        var contacts = _bath.GetMeta("roofSuppressedFoliageContacts").AsStringArray();
        Check(owners.Any(path => path.EndsWith("/WinterBirch_1_Plant25", StringComparison.Ordinal)),
            "the actual Facilities06 indoor birch is covered by the bath roof rule");
        foreach (var path in owners)
        {
            var tree = GetNode<Node3D>(path);
            Check(!tree.IsVisibleInTree() && tree.GetMeta("roofSuppressedBy").AsString() == _bath.GetPath().ToString(),
                "the roof-overlapping plant retains its identity with all LODs hidden");
            var stems = contacts.Select(contact => GetNode<CollisionShape3D>(contact))
                .Where(stem => stem.GetMeta("geometryOwner").AsString() == path).ToArray();
            Check(stems.All(stem => stem.Disabled), "every recorded stem belonging to that plant is disabled");
            if (path.EndsWith("/WinterBirch_1_Plant25", StringComparison.Ordinal))
                Check(stems.Length == 1 && stems[0].GetParent() is CollisionObject3D sharedBody
                    && (sharedBody.CollisionLayer & 3) != 0,
                    "the known indoor birch retains its exact paired stem while the shared scenery body remains active");
            _events.Add(new { kind = "bath-roof-foliage-contact-pair", tree = path,
                originalPlantPosition = tree.GetMeta("plantPosition").AsVector3().ToString(),
                worldRoot = tree.GlobalPosition.ToString(), retained = true,
                visible = tree.IsVisibleInTree(), stems = stems.Select(stem => new {
                    path = stem.GetPath().ToString(), stem.Disabled,
                    sharedBodyLayer = (stem.GetParent() as CollisionObject3D)?.CollisionLayer }).ToArray() });
        }
    }

    private async Task SetBathWetDoorFromInside(bool open)
    {
        if (Flag("bathhouse/wet-door", "open") == open) return;
        // Facilities06: the opened leaf correctly hid the stove from the first
        // stance after the threshold. Walk beyond its free end before using it;
        // closing frees the view to the heater and keeps the warm room shut.
        await WalkTo(_bath.ToGlobal(new(.55f, 0, -1.35f)), "stand beyond the wet door's complete swept arc");
        await Use(Target("urman.chapter1:local/bathhouse/wet-door"));
        await DoorAt(_bath.GetNode<Node3D>("BathWetRoomDoorHinge"), open ? 185 : 90,
            open ? "manually reopen the wet room before returning" : "manually close the wet room before tending the stove");
    }

    private async Task WalkBathEntryBypass(Vector3 destination, bool entering)
    {
        var path = _bath.GetMeta("entryDoorBypass").AsVector3Array();
        foreach (var point in entering ? path : path.Reverse())
            await WalkTo(point, entering
                ? "walk on the supported side of the fully open bath leaf"
                : "return around the same open bath leaf on the supported landing");
        await WalkTo(destination, entering
            ? "reach the original landing from the clear side of the open leaf"
            : "return to the original lower stair flight");
    }

    private void CheckBathEntryBypassSupport(Vector3 turn)
    {
        var path = _bath.GetMeta("entryDoorBypass").AsVector3Array();
        var space = _player.GetWorld3D().DirectSpaceState;
        var excluded = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
        using var ray = PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.Down, 3, excluded);
        using var capsule = new CapsuleShape3D { Radius = _player.BodyRadius, Height = _player.StandingBodyHeight };
        using var query = new PhysicsShapeQueryParameters3D { Shape = capsule, CollisionMask = 3,
            Margin = .002f, Exclude = excluded };
        // The lower-flight point is tested by actual walking/step-up. The two
        // flat side points and the original landing must fit a full standing
        // capsule before attempting the route around the already open leaf.
        foreach (var point in path.Skip(1).Append(turn))
        {
            ray.From = point + Vector3.Up * .30f;
            ray.To = point - Vector3.Up * .35f;
            var support = space.IntersectRay(ray);
            Check(support.Count > 0 && support["normal"].AsVector3().Y > .98f,
                "bath door bypass has a real flat landing support");
            var feet = support["position"].AsVector3();
            var owner = support["collider"].AsGodotObject() as Node;
            Check(Math.Abs(feet.Y - _bath.GlobalPosition.Y) < .02f
                && owner is not null && owner.Name.ToString().StartsWith("BathEntryLanding", StringComparison.Ordinal),
                "bath bypass stands on its authored upper landing, not terrain underneath it");
            // A standing capsule has a rounded bottom, not a .70 m flat sole.
            // Check room for both feet independently of its full body query:
            // nine real floor samples over .20 x .36 m, using the bath axes.
            foreach (var dx in new[] { -.10f, 0, .10f })
            foreach (var dz in new[] { -.18f, 0, .18f })
            {
                var sample = point + _bath.GlobalBasis * new Vector3(dx, 0, dz);
                ray.From = sample + Vector3.Up * .30f;
                ray.To = sample - Vector3.Up * .35f;
                var footSupport = space.IntersectRay(ray);
                Check(footSupport.Count > 0 && footSupport["normal"].AsVector3().Y > .98f
                    && Math.Abs(footSupport["position"].AsVector3().Y - feet.Y) < .01f,
                    "both feet retain a complete level support beside the trimmed axe clearance");
            }
            query.Transform = new(Basis.Identity, feet + Vector3.Up * (_player.StandingBodyHeight * .5f + .025f));
            var contacts = space.IntersectShape(query, 16);
            _events.Add(new { kind = "bath-open-door-bypass-standing", feet = feet.ToString(),
                support = owner!.GetPath().ToString(), radius = capsule.Radius, height = capsule.Height,
                margin = query.Margin, contacts = contacts.Select(hit =>
                    (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() ?? "unresolved").ToArray() });
            Check(contacts.Count == 0, "full standing capsule fits beside the open leaf and the preserved shelter");
        }
    }

    private async Task CheckBathResources(InteractionTarget stove, InteractionTarget water)
    {
        await WalkTo(_bath.ToGlobal(new(.55f, 0, -.40f)), "return to the physical heater controls");
        for (var remaining = 6; remaining >= 0; remaining--)
        {
            await AdvanceClockUntil(Number("bathhouse/steam", "until") + .10, "allow the previous steam pulse to dissipate");
            await Use(water);
            Check(Number("bathhouse/water", "ladles", 8) == remaining, "finite water falls to " + remaining + " ladles");
        }
        await AdvanceClockUntil(Number("bathhouse/steam", "until") + .10, "finish the last available ladle");
        var empty = BathProps();
        await Use(water);
        Check(BathProps() == empty && !_bath.GetNode<Node3D>("BathWaterBucket/Water").Visible,
            "empty bucket refuses another ladle without an invented water supply");
        await SetBathWetDoorFromInside(open: true);
        await WalkTo(_bath.ToGlobal(new(.55f, 0, 1.32f)), "walk back to the bucket and covered tank");
        var refill = Target("urman.chapter1:local/bathhouse/bucket");
        await Use(refill);
        Check(Number("bathhouse/water", "ladles", 8) == 8 && Number("bathhouse/water", "tankLadles", 40) == 32
            && _bath.GetNode<Node3D>("BathWaterBucket/Water").Visible, "visible tank refills the bucket by spending eight reserve ladles");
        var refilled = BathProps();
        await Use(refill);
        Check(BathProps() == refilled, "full bucket does not charge the tank again");
        await WalkTo(_bath.ToGlobal(new(.55f, 0, -.40f)), "return through the open wet-room door after refilling");
        await SetBathWetDoorFromInside(open: false);
        await WalkTo(_bath.ToGlobal(new(.55f, 0, -1.55f)), "reach the real ventilation slider");
        var vent = Target("urman.chapter1:local/bathhouse/vent");
        await Use(vent);
        Check(Flag("bathhouse/vent", "open") && !Flag("bathhouse/steam", "condensed")
            && !_bath.GetNode<Node3D>("BathCondensation").Visible && JournalCount(BathObservation) == 1,
            "ventilation clears physical moisture without removing the learned observation");
        await Use(vent);
        Check(!Flag("bathhouse/vent", "open"), "the same reachable slider closes the vent again");
        await WalkTo(_bath.ToGlobal(new(.55f, 0, -.40f)), "return to stoke the finite remaining logs");
        for (var remaining = 3; remaining >= 0; remaining--)
        {
            await AdvanceClockUntil(Number("bathhouse/stove", "burnUntil") + .10, "let the actual previous log burn out");
            await Use(stove);
            Check(Number("bathhouse/stove", "logsRemaining", 5) == remaining && VisibleLogs() == remaining,
                "another actual ignition consumes the visible stock to " + remaining);
        }
        await AdvanceClockUntil(Number("bathhouse/stove", "burnUntil") + .10, "complete the fifth log before the empty-rack attempt");
        var noLogs = BathProps();
        await Use(stove);
        Check(BathProps() == noLogs && VisibleLogs() == 0, "empty dry rack refuses a sixth ignition without creating a log");
        Check(await _bridge.SaveSlotAsync("facilities-bath-exhausted"), "save attained finite resource exhaustion");
        await Load("facilities-bath-observed");
        Check(VisibleLogs() == 4, "earlier attained save restores its earlier visible fuel supply");
        await Load("facilities-bath-exhausted");
        Check(VisibleLogs() == 0 && Number("bathhouse/water", "tankLadles", 40) == 32 && JournalCount(BathObservation) == 1,
            "later save restores exhaustion, reserve water consumption and a single notebook entry");
        await Capture("06_bath_empty_dry_rack");
    }

    private async Task CheckLateDoorObstacle(InteractionTarget entrance, Node3D hinge)
    {
        Aim(entrance.GlobalPosition);
        await Frames(3);
        Check(AimedAt(entrance), "late-obstacle case begins with a real aimed door input");
        Input.ActionPress("interact"); await Frames(1); Input.ActionRelease("interact");
        await WaitUntil(() => Mathf.Pi - hinge.Rotation.Y > .07f, "unobstructed door starts its normal opening animation");
        Check(Mathf.Pi - hinge.Rotation.Y < Mathf.DegToRad(60), "late obstacle is introduced before the leaf reaches it");
        var relative = new Vector3(Mathf.Sin(Mathf.DegToRad(105)) * .96f, 0, Mathf.Cos(Mathf.DegToRad(105)) * .96f);
        var center = _bath.ToGlobal(hinge.Position + relative);
        using (var support = PhysicsRayQueryParameters3D.Create(new(center.X, _bath.GlobalPosition.Y + 3, center.Z),
            new(center.X, _bath.GlobalPosition.Y - 2, center.Z), 3))
        {
            var hit = _world.GetWorld3D().DirectSpaceState.IntersectRay(support);
            Check(hit.Count > 0, "visible late-obstacle fixture has an actual support below it");
            center.Y = hit["position"].AsVector3().Y + .75f;
        }
        var obstacle = new StaticBody3D { Name = "ExplicitLateDoorObstacle", CollisionLayer = 1, CollisionMask = 0 };
        obstacle.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(.22f, 1.50f, .22f) } });
        obstacle.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new(.22f, 1.50f, .22f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("d19a44") } });
        obstacle.SetMeta("testFixture", "explicit late obstacle; excluded from world and artistic acceptance");
        AddChild(obstacle); obstacle.GlobalPosition = center;
        try
        {
            await WaitUntil(() => entrance.Prompt.Contains("упёрлась", StringComparison.Ordinal), "a newly introduced physical body pauses the moving leaf");
            var stopped = hinge.Rotation.Y;
            await Frames(12);
            Check(Mathf.Pi - stopped > .07f && Mathf.Pi - stopped < Mathf.DegToRad(98) && Math.Abs(hinge.Rotation.Y - stopped) < .002f
                && !LeafHits(hinge, "BathEntranceLeafBody", obstacle), "blocked animation remains still and never penetrates the new obstacle");
            Check(await _bridge.SaveSlotAsync("facilities-door-stopped"), "save while the physical door is partially open and stopped");
            Check(Math.Abs(Number("bathhouse/entrance", "angle") - stopped) < .001,
                "save flush records actual stopped angle rather than its desired endpoint");
            await Capture("07_bath_door_late_obstacle");
        }
        finally { obstacle.QueueFree(); await Frames(3); }
        await DoorAt(hinge, 82, "removing the obstacle resumes the same door without another grant");
        await WaitUntil(FacilityIdle, "pending door save completes");
        await Use(entrance);
        await DoorAt(hinge, 180, "door can be closed after a blocked and resumed opening");
    }

    private async Task CheckMosque()
    {
        var entrance = Target("urman.chapter1:local/mosque/entrance");
        var hinge = _mosque.GetNode<Node3D>("MosqueEntranceHinge");
        var approach = _mosque.GetMeta("entryApproachPath").AsVector3Array();
        var outside = _mosque.GetMeta("entryApproach").AsVector3();
        await LocalStart(outside, "outside the unchanged village mosque complex");
        Check(AddressAccessGeometryProof.CaptureMosqueFoundation(_world, _player, Output),
            "the existing mosque plinth reaches actual terrain with one matching contact and unchanged floor and footprint");
        CheckMosqueGrounding();
        await Capture("08_mosque_entrance", entrance.GlobalPosition);
        if (MosqueOnly) ObserveMosqueApproachProbe(approach);
        var stepsBefore = _player.StepsClimbed;
        try
        {
            if (MosqueContinuousRouteProof) await CheckContinuousMosqueRoute(approach);
            else for (var point = 1; point < approach.Length; point++)
                    await WalkTo(approach[point], "climb the actual mosque entrance profile: point " + point);
        }
        finally
        {
            var actual = new { kind = "mosque-actual-stairs", stepsClimbed = _player.StepsClimbed - stepsBefore,
                lastStepRejection = _player.LastStepRejection, feet = _player.GlobalPosition.ToString() };
            _events.Add(actual); GD.Print("act1-facility-stairs: " + JsonSerializer.Serialize(actual));
        }
        Check(Math.Abs(_player.GlobalPosition.Y - _mosque.GlobalPosition.Y) < .06f,
            "the full entrance stair reaches the unchanged mosque floor before the door action");
        await Capture("08b_mosque_upper_landing", entrance.GlobalPosition);
        var groundedGate = _mosque.GetParent().GetNode<MeshInstance3D>("MosqueYardGateLeft");
        await Capture("08c_mosque_courtyard_gate", groundedGate.ToGlobal(groundedGate.Mesh.GetAabb().GetCenter()));
        if (MosqueOnly) CheckClosedMosqueDoorProbe(hinge);
        await Use(entrance);
        await DoorAt(hinge, -95, "full inward mosque leaf opens by a real aimed action without hitting the landing guards");
        await WalkTo(_mosque.ToGlobal(new(4.65f, 0, -.25f)), "cross the actual mosque doorway from the upper landing");
        Check(_world.FacilityInteriorAt(_player.GlobalPosition) == "mosque", "physical mosque entry selects its indoor state");
        await CheckShelter("mosque");
        await WalkTo(_mosque.ToGlobal(new(4.30f, 0, 1.50f)), "approach the shoe bench inside the vestibule");
        await Capture("09_mosque_vestibule", _mosque.ToGlobal(new(3.65f, .85f, 2.90f)));
        await CheckMosqueCourtesyMount();
        await Use(Target(MosqueObservation));
        await WaitUntil(() => JournalCount(MosqueObservation) == 1, "actual vestibule observation enters the personal notebook once");
        await ReadNotebookObservation(MosqueObservation, "09a_mosque_observation_notebook");
        await CheckPlayerFootwear();
        await WalkTo(_mosque.ToGlobal(new(3.65f, 0, 0)), "return to the vestibule opening");
        await WalkTo(_mosque.ToGlobal(new(1.20f, 0, 0)), "enter the mosque hall through the partition opening");
        var timur = _mosque.GetNode<Node3D>("Npc_timur_hazrat");
        var conversation = Target("urman.chapter1:interaction/route-to-mosque");
        Check(conversation.GlobalPosition.DistanceTo(timur.GlobalPosition + Vector3.Up * .9f) < .03f
            && conversation.DialogueId == "urman.chapter1:dialogue/timur_restraint", "existing Timur and his existing conversation share the indoor physical anchor");
        Aim(timur.GlobalPosition + Vector3.Up * 1.40f);
        await Capture("09_mosque_hall_timur");
        Check(_player.IsOnFloor() && _player.CanStandAt(_player.GlobalPosition), "hall offers actual floor and head clearance");
        await CheckMosqueTimurFootwear(timur);
        await WalkMosqueWestHall();
        await WalkTo(_mosque.ToGlobal(new(3.65f, 0, 0)), "return through the hall opening");
        await WalkTo(_mosque.ToGlobal(new(6.65f, 0, -.25f)), "leave the real doorway onto its supported exterior landing within reach of the inward leaf");
        await CheckMosqueDoorMigration(entrance, hinge);
        await WalkTo(approach[^1], "return along the guarded upper landing to the stair");
        for (var point = approach.Length - 2; point >= 0; point--)
            await WalkTo(approach[point], "descend the actual mosque entrance profile: point " + point);
        await CheckShelter(string.Empty);
        Check(await _bridge.SaveSlotAsync("facilities-mosque-return"), "save an ordinary mosque visit after physically leaving");
        var angle = hinge.Rotation.Y;
        await Load("facilities-pristine");
        Check(JournalCount(MosqueObservation) == 0 && !Flag("mosque/entrance", "open"), "old save does not inherit a future mosque visit");
        await Load("facilities-mosque-return");
        Check(JournalCount(MosqueObservation) == 1 && Math.Abs(hinge.Rotation.Y - angle) < .01f && _player.IsOnFloor(),
            "saved mosque return restores one observation, physical door angle and supported player");
    }

    private async Task CheckContinuousMosqueRoute(Vector3[] approach)
    {
        var registry = _world.AddressRegistry ?? throw new InvalidOperationException("The existing address registry is absent.");
        Check(registry.TryResolve("ADR-MOSQUE", out var address), "the local route diagnostic resolves the existing mosque identity");
        var started = Time.GetTicksMsec();
        var startedFrame = Engine.GetPhysicsFrames();
        using var watchdogCancellation = new System.Threading.CancellationTokenSource();
        var watchdog = Task.Delay(60000, watchdogCancellation.Token);
        try
        {
            while (registry.AccessPoints[address.AccessId].State.StartsWith("pending", StringComparison.Ordinal)
                && Engine.GetPhysicsFrames() - startedFrame < 3600 && !PreparationExpired())
            {
                var nextFrame = NextPhysicsFrame();
                if (await Task.WhenAny(nextFrame, watchdog) != nextFrame)
                    throw new TimeoutException("The local mosque path preparation received no physics frame before its 60-second deadline.");
                await nextFrame;
                if (PreparationExpired()) break;
            }
            var preparedAccess = registry.AccessPoints[address.AccessId];
            _events.Add(new { kind = "natural-route-preparation", frames = Engine.GetPhysicsFrames() - startedFrame,
                elapsedMs = Time.GetTicksMsec() - started, accessId = address.AccessId, state = preparedAccess.State,
                diagnosticScheduling = false,
                note = "local fixture preparation, not a production latency or full queue acceptance criterion" });
            Check(preparedAccess.State == "verified" && !PreparationExpired(),
                "ordinary address processing publishes the actual mosque path before this bounded local diagnostic");
        }
        finally { watchdogCancellation.Cancel(); }

        bool PreparationExpired() => Engine.GetPhysicsFrames() - startedFrame > 3600
            || Time.GetTicksMsec() - started >= 60000 || watchdog.IsCompleted;
        async Task NextPhysicsFrame() { await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

        var access = registry.AccessPoints[address.AccessId];
        var origin = registry.Graph.NodeAt(registry.Graph.Roads["authored/main-axis"].Points[0]);
        Check(origin is not null, "the actual main street retains its common graph origin");
        var complete = registry.DiagnosticRoute(origin!, address.AddressId, SettlementTravelMode.Foot)
            .Select(p => new Vector3((float)p.X, (float)p.Y, (float)p.Z)).ToArray();
        Check(complete.Length > 2 && approach.Length > 2, "the published mosque route and authored landings exist");
        var first = Enumerable.Range(0, complete.Length)
            .OrderBy(index => complete[index].DistanceTo(approach[0])).First();
        Check(new Vector2(complete[first].X - approach[0].X, complete[first].Z - approach[0].Z).Length() < .02f
            && Math.Abs(complete[first].Y - approach[0].Y) < .08f
            && complete[^1].DistanceTo(approach[^1]) < .12f,
            "the unchanged published suffix begins at the actual exterior approach and ends at the authored upper landing");
        var suffix = complete.Skip(first).ToArray();
        // Locate the original B46 failing sample in the live published path.
        // This constant selects a diagnostic observation, never a player pose.
        var historicalSample = new Vector3(-44.94902f, 1.8013142f, -41.181847f);
        var selected = Enumerable.Range(0, suffix.Length).OrderBy(index => suffix[index].DistanceTo(historicalSample)).First();
        Check(selected > 0 && selected < suffix.Length - 1 && suffix[selected].DistanceTo(historicalSample) < .025f,
            "the actual published suffix contains the same intermediate capsule pose as Walk12 sample218");
        var original = suffix.ToArray();
        var knowledge = _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var beats = _bridge.SelectRuntimeState().GetProperty("beats").GetRawText();
        var scene = _bridge.ActiveSceneId;
        var addressState = access;
        var steps = _player.StepsClimbed;
        _events.Add(new { kind = "published-mosque-suffix", source = "ordinary registry DiagnosticRoute; no graph mutation",
            addressId = address.AddressId, accessId = address.AccessId, sourceFirstIndex = first,
            diagnosticIndex = selected, originalPointCount = suffix.Length,
            points = suffix.Select(point => point.ToString()).ToArray(), historicalSample = historicalSample.ToString() });
        await Capture("08r_continuous_route_start", _mosque.ToGlobal(new(8.05f, .25f, -5.60f)));
        var oldPrefix = suffix.Take(selected + 1).ToArray();
        var oldResult = await Act1FirstPersonWalkthroughSmokeTest.FollowMosqueRouteAsync(this, _player, _bridge,
            oldPrefix, "old-stop-at-walk12-218", row => _events.Add(row), observeOldStops: true);
        _walked += oldResult.WalkedMetres;
        Check(oldResult.VisitedPoints == oldPrefix.Length, "the old-stop diagnostic actually visits each sample before releasing input");
        _events.Add(new { kind = "old-stop-observed-outcome", oldResult.StableLanding,
            expectedFailureRequired = false, acceptance = false, note = "before/after physical trace determines whether this run reproduces sliding" });
        await Capture("08s_old_stop_observed");
        await Follow(oldPrefix.Reverse().ToArray(), "diagnostic-return-to-original-start");
        await Follow(suffix, "continuous-exact-suffix-up");
        await Capture("08t_continuous_upper_landing");
        await Follow(suffix.Reverse().ToArray(), "continuous-exact-suffix-down");
        await Capture("08u_continuous_lower_return");
        await Follow(suffix, "continuous-exact-suffix-second-up");
        Check(original.SequenceEqual(suffix) && registry.AccessPoints[address.AccessId] == addressState
            && _bridge.ActiveSceneId == scene
            && _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == knowledge
            && _bridge.SelectRuntimeState().GetProperty("beats").GetRawText() == beats
            && _player.StepsClimbed > steps,
            "both directions preserve every original graph sample, address identity and story while using actual steps");

        async Task Follow(Vector3[] points, string label)
        {
            var result = await Act1FirstPersonWalkthroughSmokeTest.FollowMosqueRouteAsync(this, _player, _bridge,
                points, label, row => _events.Add(row));
            _walked += result.WalkedMetres;
            Check(result.StableLanding && result.VisitedPoints == points.Length,
                label + ": every point visited by real input; only the authored endpoint requires stationary support");
        }
    }

    private void CheckClosedMosqueDoorProbe(Node3D hinge)
    {
        var leaf = hinge.GetNode<StaticBody3D>("MosqueEntranceLeafBody");
        var target = _mosque.ToGlobal(new(4.65f, 0, -.25f));
        var playerPose = _player.CapturePortableTransform();
        using var ray = PhysicsRayQueryParameters3D.Create(_player.GlobalPosition + Vector3.Up * .9f,
            target + Vector3.Up * .9f, 3, new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var hit = _world.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        Check(hit.Count > 0 && hit["collider"].AsGodotObject() == leaf,
            "negative motion case faces the actual closed mosque leaf above the landing support");
        using var probe = new AddressWalkProbe(_world);
        Check(probe.TrySupport(_player.GlobalPosition, out var feet),
            "negative closed-door motion starts on the actual reached upper landing");
        var rejected = false;
        for (var step = 0; step < 100; step++)
        {
            var remaining = new Vector3(target.X - feet.X, 0, target.Z - feet.Z);
            if (remaining.Length() <= .015f) break;
            if (!probe.TryAdvance(feet, remaining.LimitLength(.08f), out var next)) { rejected = true; break; }
            feet = next;
        }
        ray.From = feet + Vector3.Up * .9f;
        var finalHit = _world.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        var finalOwner = finalHit.Count > 0 ? finalHit["collider"].AsGodotObject() as Node : null;
        float? distanceToLeaf = finalHit.Count > 0 ? (finalHit["position"].AsVector3() - ray.From).Length() : null;
        _events.Add(new { kind = "mosque-closed-door-negative-motion", rejected,
            expectedOwner = leaf.GetPath().ToString(), rayPoint = hit["position"].AsVector3().ToString(),
            finalOwner = finalOwner?.GetPath().ToString(), distanceToLeaf,
            stoppedFeet = feet.ToString(), probe.LastRejection, probe.LastSupportProbe,
            probe.StepsClimbed, playerUnchanged = playerPose.Equals(_player.CapturePortableTransform()) });
        Check(rejected && finalOwner == leaf && distanceToLeaf is { } distance && distance <= _player.BodyRadius + .10f
            && playerPose.Equals(_player.CapturePortableTransform()),
            "address motion cannot step through the same real closed leaf that ordinary play must open");
    }

    private void ObserveMosqueApproachProbe(Vector3[] approach)
    {
        // Compare the address auditor with the real controller below. A probe
        // refusal is evidence, never a gate that prevents the input traversal.
        using var probe = new AddressWalkProbe(_world);
        var accepted = probe.TrySupport(approach[0], out var feet);
        var support = probe.LastSupportProbe;
        var rejection = probe.LastRejection;
        var failedSegment = -1;
        var failedStep = -1;
        for (var segment = 1; accepted && segment < approach.Length; segment++)
        {
            if (!probe.TrySupport(approach[segment], out var supportedTarget))
            {
                accepted = false; failedSegment = segment;
                support = probe.LastSupportProbe; rejection = probe.LastRejection; break;
            }
            var displacement = approach[segment] - feet; displacement.Y = 0;
            var steps = Math.Max(1, (int)Math.Ceiling(displacement.Length() / .08f));
            var advance = displacement / steps;
            for (var step = 0; step < steps; step++)
            {
                accepted = probe.TryAdvance(feet, advance, out var next);
                if (!accepted) { failedSegment = segment; failedStep = step; rejection = probe.LastRejection; break; }
                feet = next;
            }
            if (accepted && Math.Abs(feet.Y - supportedTarget.Y) >= .08f)
            {
                accepted = false; failedSegment = segment;
                rejection = "walked segment reached a different support height: " + feet + " vs " + supportedTarget;
            }
        }
        var result = new { kind = "mosque-read-only-address-probe", accepted, failedSegment, failedStep,
            support, rejection, stepsClimbed = probe.StepsClimbed,
            returnedFeet = feet.ToString(), suppliedFinal = approach[^1].ToString(),
            finalHeightError = feet.Y - approach[^1].Y, commitsAddressGraph = false, controlsActualWalk = false };
        _events.Add(result); GD.Print("act1-facility-address-probe: " + JsonSerializer.Serialize(result));
    }

    private async Task CheckHeldDoorObstacle()
    {
        var crate = _carry.Items.Single(item => item.ItemId == "carry-crate");
        await ApproachCarry(crate);
        await Press("interact"); await _carry.PendingAction;
        Check(_carry.HeldItem == crate, "existing yard crate is taken through the real ray and custody owner");
        var entrance = Target("urman.chapter1:local/mosque/entrance");
        var hinge = _mosque.GetNode<Node3D>("MosqueEntranceHinge");
        var feet = _mosque.ToGlobal(new(3.60f, .035f, .25f));
        await LocalStart(feet, "explicit paired inward-door fixture inside the vestibule with the normally acquired crate");
        Aim(entrance.GlobalPosition); await Frames(6);
        Check(_carry.HasValidHeldPose && _carry.HeldItem == crate && crate.CollisionLayer == 0 && AimedAt(entrance),
            "held crate has its valid full pose while the actual door ray remains unobstructed");
        var state = Prop("mosque/entrance");
        var owner = Custody(crate.ItemId);
        await Use(entrance);
        Check(!Flag("mosque/entrance", "open") && Prop("mosque/entrance") == state && _carry.HeldItem == crate
            && Custody(crate.ItemId) == owner && Math.Abs(hinge.Rotation.Y) < .001f,
            "held collision-disabled crate in the sweep refuses the door and preserves custody");
        using (var refusal = JsonDocument.Parse(entrance.GetMeta("doorSweepProbe", "{}").AsString()))
            Check(refusal.RootElement.TryGetProperty("heldItem", out var heldItem) && heldItem.GetString() == crate.ItemId,
                "the inward door refusal names the held crate rather than the player or stationary furniture");
        await Capture("10_mosque_door_held_crate_refusal");
        // Drop aside with the real placement input, without moving the player.
        var sameFeet = _player.GlobalPosition;
        var placed = false;
        foreach (var at in new[] { new Vector3(3.05f, .014f, 1.65f), new(3.10f, .014f, 1.65f), new(3.15f, .014f, 1.70f) })
        {
            Aim(_mosque.ToGlobal(at)); await Frames(4);
            if (!_carry.TryPlacement(crate, out _, out _)) continue;
            await Press("carry_place"); await _carry.PendingAction;
            if (_carry.HeldItem is null) { placed = true; break; }
        }
        Check(placed && _player.GlobalPosition.DistanceTo(sameFeet) < .04f, "crate is placed aside without changing the player's counterfactual stance");
        await Use(entrance);
        await DoorAt(hinge, -95, "same player position opens the full inward door after only the held obstruction is removed");
        Check(_carry.HeldItem is null && crate.State == CarryableProp.CarryState.Placed,
            "successful door action does not retake or duplicate the placed crate");
    }

    private async Task<Vector3> CheckBathWindowSightlines(InteractionTarget observation)
    {
        // Facilities08 aimed at the window's real central timber, not a pane.
        // Keep that obstruction and verify both visible panes with the same ray
        // used for interaction, from this attained indoor standing position.
        var leftPane = _bath.ToGlobal(new(.46f, 1.60f, -2.465f));
        var probes = new (string Label, Vector3 Aim, Node3D Expected)[]
        {
            ("central timber remains solid", observation.GlobalPosition, _bath.GetNode<StaticBody3D>("BathWindow/Mullion0Body")),
            ("left wet pane is readable", leftPane, observation),
            ("right wet pane is readable", _bath.ToGlobal(new(.84f, 1.60f, -2.465f)), observation)
        };
        foreach (var probe in probes)
        {
            Aim(probe.Aim); await Frames(3);
            using var ray = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition, _camera.GlobalPosition - _camera.GlobalBasis.Z * 2.7f, 7);
            ray.Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
            var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var owner = hit.Count > 0 ? hit["collider"].AsGodotObject() as Node3D : null;
            var evidence = new { kind = "bath-window-sightline", probe.Label, aim = probe.Aim.ToString(),
                camera = _camera.GlobalTransform.ToString(), feet = _player.GlobalPosition.ToString(),
                owner = owner?.GetPath().ToString(), shape = hit.Count > 0 ? hit["shape"].AsInt32() : -1,
                contact = hit.Count > 0 ? hit["position"].AsVector3().ToString() : null };
            _events.Add(evidence); GD.Print("act1-facility-window: " + JsonSerializer.Serialize(evidence));
            Check(owner == probe.Expected, probe.Label + "; " + _carry.DescribeAim());
        }
        return leftPane;
    }

    private async Task Use(InteractionTarget target, Vector3? aimPoint = null)
    {
        await EnsureFocus();
        await WaitUntil(FacilityIdle, "previous facility action finishes");
        Aim(aimPoint ?? target.GlobalPosition); await Frames(3);
        Check(target.IsAvailable() && AimedAt(target), $"actual available aim at {target.Name}; layer={target.CollisionLayer}; {_carry.DescribeAim()}");
        var hinge = DoorHinge(target);
        var actionBefore = target.GetMeta("lastDoorActionNumber", 0).AsInt32();
        if (hinge is not null)
        {
            RecordDoorInput(target, hinge, "before-input");
            Check(_player.FocusedInteractionId == target.InteractionId,
                target.Name + ": passive focus follows the actual door during a result notice");
        }
        await Press("interact");
        if (hinge is not null)
        {
            RecordDoorInput(target, hinge, "after-input");
            Check(target.GetMeta("lastDoorActionNumber", 0).AsInt32() == actionBefore + 1,
                target.Name + ": one deliberate mapped press reaches exactly one door callback");
        }
        await WaitUntil(FacilityIdle, "facility transaction and existing checkpoint save finish");
        await Frames(3);
        if (hinge is not null) RecordDoorInput(target, hinge, "after-transaction");
    }

    private Node3D? DoorHinge(InteractionTarget target) => target.Name.ToString() switch
    {
        "BathEntranceUse" => _bath.GetNode<Node3D>("BathEntranceHinge"),
        "BathWetRoomDoorUse" => _bath.GetNode<Node3D>("BathWetRoomDoorHinge"),
        "MosqueEntranceUse" => _mosque.GetNode<Node3D>("MosqueEntranceHinge"),
        _ => null
    };

    private void RecordDoorInput(InteractionTarget target, Node3D hinge, string phase)
    {
        var probe = new { kind = "door-input-probe", phase, target = target.InteractionId,
            feet = _player.GlobalPosition.ToString(), velocity = _player.Velocity.ToString(),
            forward = Input.GetActionStrength("move_forward"), backward = Input.GetActionStrength("move_backward"),
            left = Input.GetActionStrength("move_left"), right = Input.GetActionStrength("move_right"),
            interact = Input.GetActionStrength("interact"), focus = DisplayServer.WindowIsFocused(),
            modal = _player.ModalOpen, sessionReady = _bridge.SessionIdentity is not null,
            noticeActive = _player.InteractionNoticeActive, focusedInteraction = _player.FocusedInteractionId,
            transformRevision = _player.PresentationTransformRevision, angle = hinge.Rotation.Y,
            actionNumber = target.GetMeta("lastDoorActionNumber", 0).AsInt32(),
            action = target.GetMeta("lastDoorAction", "not-called").ToString(),
            result = target.GetMeta("lastDoorActionResult", "not-called").ToString(),
            sweep = target.GetMeta("doorSweepProbe", "not-called").ToString(), aim = _carry.DescribeAim() };
        _events.Add(probe);
        GD.Print("act1-facility-door: " + JsonSerializer.Serialize(probe));
    }

    private async Task DoorAt(Node3D hinge, float degrees, string label)
    {
        var target = _world.FindChildren("*", nameof(StaticBody3D), true, false).OfType<InteractionTarget>()
            .Single(candidate => DoorHinge(candidate) == hinge);
        var deadline = Time.GetTicksMsec() + 12000;
        var nextSample = Time.GetTicksMsec() + 500;
        while (Math.Abs(hinge.Rotation.Y - Mathf.DegToRad(degrees)) >= .005f && Time.GetTicksMsec() < deadline)
        {
            await Frames(1);
            if (Time.GetTicksMsec() >= nextSample)
            {
                RecordDoorInput(target, hinge, "waiting-angle");
                nextSample = Time.GetTicksMsec() + 500;
            }
        }
        RecordDoorInput(target, hinge, "finished-angle-wait");
        Check(Math.Abs(hinge.Rotation.Y - Mathf.DegToRad(degrees)) < .005f, label);
    }

    private async Task WalkTo(Vector3 goal, string label)
    {
        await EnsureFocus();
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        var clamps = _player.EdgeClamps;
        var start = _player.GlobalPosition;
        var previous = start;
        var arrived = false;
        try
        {
            for (var frame = 0; frame < 600; frame++)
            {
                var delta = goal - _player.GlobalPosition;
                if (new Vector2(delta.X, delta.Z).Length() < .09f) { arrived = true; break; }
                _player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
                Input.ActionPress("move_forward"); await Frames(1);
                _walked += _player.GlobalPosition.DistanceTo(previous); previous = _player.GlobalPosition;
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        // Facilities09/Mosque05 reached the original XZ radius while still
        // descending the final 18 cm to terrain. After four frames the failure
        // camera still descended another 9.56 mm during capture. Let the
        // ordinary controller finish that physical landing without moving it.
        var settleStart = _player.GlobalPosition;
        var settledFrames = 0;
        var waitedFrames = 0;
        var previousY = settleStart.Y;
        for (; waitedFrames < 30 && settledFrames < 3; waitedFrames++)
        {
            await Frames(1);
            var y = _player.GlobalPosition.Y;
            settledFrames = _player.IsOnFloor() && Math.Abs(_player.Velocity.Y) < .05f && Math.Abs(y - previousY) < .002f
                ? settledFrames + 1 : 0;
            previousY = y;
        }
        var finalDistance = new Vector2(goal.X - _player.GlobalPosition.X, goal.Z - _player.GlobalPosition.Z).Length();
        using var supportRay = PhysicsRayQueryParameters3D.Create(_player.GlobalPosition + Vector3.Up * .15f,
            _player.GlobalPosition - Vector3.Up * .35f, 3, new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var support = _world.GetWorld3D().DirectSpaceState.IntersectRay(supportRay);
        var supportBody = support.Count == 0 ? null : support["collider"].AsGodotObject() as CollisionObject3D;
        var supportShape = supportBody is null ? null : supportBody.ShapeOwnerGetOwner(supportBody.ShapeFindOwner(support["shape"].AsInt32())) as Node;
        _events.Add(new { kind = "physical-walk-settle", label, arrived, waitedFrames, settledFrames, finalDistance,
            from = settleStart.ToString(), feet = _player.GlobalPosition.ToString(), velocity = _player.Velocity.ToString(),
            onFloor = _player.IsOnFloor(), revisionBefore = revision, revisionAfter = _player.PresentationTransformRevision,
            recoveriesBefore = recoveries, recoveriesAfter = _player.FallRecoveries, clampsBefore = clamps, clampsAfter = _player.EdgeClamps,
            supportOwner = supportShape?.GetPath().ToString(), supportPoint = support.Count == 0 ? null : support["position"].AsVector3().ToString() });
        Check(arrived && finalDistance < .09f && settledFrames == 3 && _player.PresentationTransformRevision == revision
            && _player.FallRecoveries == recoveries && _player.EdgeClamps == clamps && _player.IsOnFloor(),
            label + $"; actual={_player.GlobalPosition}, expected={goal}, grounded={_player.IsOnFloor()}, settledFrames={settledFrames}, waitedFrames={waitedFrames}, distance={finalDistance}");
        _events.Add(new { kind = "physical-local-walk", label, from = start.ToString(), to = _player.GlobalPosition.ToString(), teleports = 0, recoveries = 0 });
    }

    private async Task CheckMosqueCourtesyMount()
    {
        var backing = _mosque.GetNode<MeshInstance3D>("MosqueEntranceCourtesyBacking");
        var paper = _mosque.GetNode<MeshInstance3D>("MosqueEntranceCourtesyPaper");
        var text = _mosque.GetNode<Label3D>("MosqueEntranceCourtesy");
        var pier = _mosque.GetNode<MeshInstance3D>("MosqueWindowPier1_4_4");
        var pierBody = _mosque.GetNode<StaticBody3D>("MosqueWindowPier1_4_4Body");
        var bounds = backing.Mesh.GetAabb();
        var paperBounds = paper.Mesh.GetAabb();
        var outward = text.GlobalBasis.Z.Normalized();
        var support = new List<object>();
        var supported = true;
        foreach (var x in new[] { bounds.Position.X, bounds.End.X })
        foreach (var y in new[] { bounds.Position.Y, bounds.End.Y })
        {
            var backCorner = backing.ToGlobal(new(x, y, bounds.End.Z));
            using var query = PhysicsRayQueryParameters3D.Create(backCorner + outward * .04f, backCorner - outward * .04f,
                3, new global::Godot.Collections.Array<Rid> { _player.GetRid() });
            var hit = _world.GetWorld3D().DirectSpaceState.IntersectRay(query);
            var body = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as CollisionObject3D;
            var point = hit.Count == 0 ? (Vector3?)null : hit["position"].AsVector3();
            var gap = point is { } p ? p.DistanceTo(backCorner) : (float?)null;
            supported &= body == pierBody && gap < .0002f;
            support.Add(new { corner = backCorner.ToString(), owner = body?.GetPath().ToString(), point = point?.ToString(), gap });
        }
        var glyphBounds = text.GetAabb();
        var textFits = glyphBounds.Size.X > .05f && glyphBounds.Size.Y > .05f;
        for (var corner = 0; corner < 8; corner++)
        {
            var p = paper.ToLocal(text.ToGlobal(glyphBounds.GetEndpoint(corner)));
            textFits &= p.X >= paperBounds.Position.X && p.X <= paperBounds.End.X
                && p.Y >= paperBounds.Position.Y && p.Y <= paperBounds.End.Y
                && p.Z < paperBounds.Position.Z && paperBounds.Position.Z - p.Z < .003f;
        }
        using var sight = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition, text.GlobalPosition - outward * .025f,
            3, new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var visibleWall = _world.GetWorld3D().DirectSpaceState.IntersectRay(sight);
        var sightOwner = visibleWall.Count == 0 ? null : visibleWall["collider"].AsGodotObject() as CollisionObject3D;
        var frontDot = outward.Dot((_camera.GlobalPosition - text.GlobalPosition).Normalized());
        _events.Add(new { kind = "mosque-courtesy-mount", backing = backing.GetPath().ToString(),
            surface = pier.GetPath().ToString(), support, supported, glyphBounds = glyphBounds.ToString(), textFits,
            outward = outward.ToString(), frontDot, sightOwner = sightOwner?.GetPath().ToString(),
            feet = _player.GlobalPosition.ToString(), sourceText = text.Text,
            proof = "actual wall attachment and unobstructed approach; readable appearance requires the native frame" });
        Check(supported, "all four notice corners are attached to the actual solid pier rather than the window glass");
        Check(textFits && text.Billboard == BaseMaterial3D.BillboardModeEnum.Disabled && !text.NoDepthTest
            && text.Text.Replace('\n', ' ') == "Пожалуйста, снимите обувь",
            "the unchanged courtesy wording fits the physical paper with fixed orientation and ordinary depth testing");
        Check(frontDot > .80f && sightOwner == pierBody && _player.IsOnFloor() && _player.CanStandAt(_player.GlobalPosition),
            "the existing reached shoe-bench stance sees the front of the mounted notice without a window, furniture or door obstruction");
        await Capture("09_mosque_courtesy_notice", text.GlobalPosition);
    }

    private async Task LocalStart(Vector3 feet, string label)
    {
        ReleaseInputs();
        Check(_player.CanStandAt(feet), "supported local start fits standing capsule: " + label);
        _player.ApplyZoneSpawn(feet, 0); await Frames(6);
        Check(_player.IsOnFloor(), "local start settles on real support: " + label);
        _events.Add(new { kind = "explicit-local-start", label, feet = _player.GlobalPosition.ToString(), isTraversal = false });
    }

    private async Task ApproachCarry(CarryableProp item)
    {
        ReleaseInputs();
        var aim = item.GlobalPosition + Vector3.Up * item.Height * .5f;
        foreach (var radius in new[] { 1.15f, 1.5f, 1.8f })
        for (var side = 0; side < 16; side++)
        {
            var point = Exterior(aim + new Vector3(Mathf.Sin(side * Mathf.Tau / 16), 0, Mathf.Cos(side * Mathf.Tau / 16)) * radius);
            if (!_player.CanStandAt(point)) continue;
            _player.ApplyZoneSpawn(point, 0); await Frames(5); Aim(aim); await Frames(3);
            if (!_player.IsOnFloor() || !AimedAt(item)) continue;
            _events.Add(new { kind = "explicit-carry-start", item = item.ItemId, feet = _player.GlobalPosition.ToString(), isTraversal = false }); return;
        }
        throw new InvalidOperationException("No supported real aim at existing " + item.ItemId);
    }

    private async Task AdvanceClockUntil(double until, string label)
    {
        if (_bridge.PlayTimeSeconds >= until) return;
        await EnsureFocus();
        ReleaseInputs();
        var before = _bridge.PlayTimeSeconds;
        var feet = _player.GlobalPosition;
        var started = Time.GetTicksMsec();
        try
        {
            Check(!_player.ModalOpen && _player.IsOnFloor() && _bridge.CapturePlayTimeBlocks() == RuntimeBridge.PlayTimeBlock.None,
                "clock expiry starts from active supported gameplay");
            Engine.TimeScale = 30;
            while (_bridge.PlayTimeSeconds < until && Time.GetTicksMsec() - started < 25000) await Frames(1);
        }
        finally { Engine.TimeScale = 1; }
        await Frames(3);
        Check(_bridge.PlayTimeSeconds >= until && _player.GlobalPosition.DistanceTo(feet) < .04f,
            label + " using the actual clock while stationary");
        _events.Add(new { kind = "accelerated-clock-expiry", label, timeScale = 30, before, after = _bridge.PlayTimeSeconds,
            wallMilliseconds = Time.GetTicksMsec() - started, isHumanDuration = false, injectedWorldFlags = 0 });
    }

    private bool LeafHits(Node3D hinge, string name, CollisionObject3D obstacle)
    {
        var body = hinge.GetNode<StaticBody3D>(name);
        var contact = body.GetNode<CollisionShape3D>("Contact");
        using var query = new PhysicsShapeQueryParameters3D { Shape = contact.Shape, Transform = contact.GlobalTransform,
            CollisionMask = 3, Margin = 0, Exclude = new global::Godot.Collections.Array<Rid> { body.GetRid() } };
        return _world.GetWorld3D().DirectSpaceState.IntersectShape(query, 32).Any(hit => hit["collider"].AsGodotObject() == obstacle);
    }

    private async Task Load(string slot)
    {
        await WaitUntil(FacilityIdle, "pending action is finished before loading " + slot);
        Check(await _bridge.LoadSlotAsync(slot), "load attained save " + slot); await Frames(10);
    }
    private async Task CheckShelter(string room)
    {
        await WaitUntil(() => _world.GetMeta("physicalInterior", "").AsString() == room, "physical room presentation follows the actual player");
        var sheltered = room.Length > 0;
        var weather = _world.GetNode<Node3D>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        var flakes = weather.GetNode<CpuParticles3D>("AgentBSnow");
        var audio = (AmbientAudioDirector)GetTree().GetFirstNodeInGroup("ambient_audio");
        Check(weather.GetMeta("physicalSheltered", false).AsBool() == sheltered && flakes.Visible == !sheltered && flakes.Emitting == !sheltered,
            "actual snow emission and visibility follow shelter: " + (sheltered ? room : "outside"));
        Check(audio.GetMeta("physicalSheltered", false).AsBool() == sheltered && audio.PlayerCount == 2
            && (audio.CurrentStreamPath == "res://assets/audio/house_room_tone.wav") == sheltered,
            "existing two-player ambience owner selects the indoor bed and returns to the village bed");
        if (DisplayServer.GetName() != "headless")
        {
            await WaitUntil(() => audio.GetChildren().OfType<AudioStreamPlayer>().Count(player => player.Playing) == 1,
                "ambient crossfade reaches one active native playback without an extra owner");
        }
        _events.Add(new { kind = "shelter-projection", room, snowVisible = flakes.Visible, snowEmitting = flakes.Emitting,
            audioStem = audio.CurrentStemId, audioStream = audio.CurrentStreamPath, listening = "external/not-run" });
    }
    private async Task ReadNotebookObservation(string interactionId, string captureName)
    {
        var journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
        var entryId = interactionId.Replace(":interaction/", ":knowledge/");
        var expected = _bridge.JournalEntries().Single(entry => entry.EntryId == entryId);
        // Facility observations append their entry without taking control away.
        // Open and select it through the normal notebook input and UI controls.
        Check(!journal.GetNode<Control>("Screen").Visible && !_player.ModalOpen,
            "the observation leaves ordinary control active until the player opens the notebook");
        // JournalUi listens to the input event; Input.ActionPress alone only
        // changes polled state and cannot open this UI.
        using (var pressed = new InputEventAction { Action = "journal", Pressed = true }) Input.ParseInputEvent(pressed);
        await Frames(2);
        using (var released = new InputEventAction { Action = "journal", Pressed = false }) Input.ParseInputEvent(released);
        await Frames(3);
        await WaitUntil(() => journal.GetNode<Control>("Screen").Visible && _player.ModalOpen,
            "mapped journal action opens the notebook");
        journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 0;
        var section = journal.GetNode<OptionButton>("Screen/Book/Layout/NotebookSection");
        section.Select(0); section.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
        await Frames(2);
        var entries = journal.GetNode<ItemList>("Screen/Book/Layout/WorkArea/Entries");
        var index = Enumerable.Range(0, entries.ItemCount).Single(at => entries.GetItemText(at).EndsWith(expected.Title, StringComparison.Ordinal));
        entries.Select(index); entries.EmitSignal(ItemList.SignalName.ItemSelected, (long)index);
        await Frames(2);
        var body = journal.GetNode<RichTextLabel>("Screen/Book/Layout/WorkArea/Reader/Body");
        Check(journal.ActiveEntryId == entryId && body.IsVisibleInTree()
            && body.Text == SourceExcerptSelection.FormatPlainSourceText(expected.Body) && !string.IsNullOrWhiteSpace(body.Text),
            "the selected actual notebook entry displays its authored observation");
        await Capture(captureName);
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntil(() => !_player.ModalOpen, "close the notebook through its production button"); await Frames(3);
        Check(JournalCount(interactionId) == 1, "reading the saved observation creates no duplicate notebook entry");
    }
    private InteractionTarget Target(string id) => _world.FindChildren("*", "", true, false).OfType<InteractionTarget>().Single(target => target.InteractionId == id);
    private int JournalCount(string interactionId) => _bridge.JournalEntries().Count(entry => entry.EntryId == interactionId.Replace(":interaction/", ":knowledge/"));
    private int VisibleLogs() => Enumerable.Range(0, 5).Count(index => _bath.GetNode<Node3D>("BathDryFirewood" + index).Visible);
    private bool FacilityIdle() => BusyField.GetValue(_world) is false && !_world.BathIgnitionInProgress;
    private bool Flag(string key, string field) => YardMechanism.Flag(_bridge.SelectWorldProps(), key, field);
    private double Number(string key, string field, double fallback = 0)
    {
        var props = _bridge.SelectWorldProps();
        return props.ValueKind == JsonValueKind.Object && props.TryGetProperty(key, out var item) && item.TryGetProperty(field, out var value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : fallback;
    }
    private string Prop(string key)
    {
        var props = _bridge.SelectWorldProps(); return props.TryGetProperty(key, out var item) ? item.GetRawText() : "absent";
    }
    private string BathProps() => JsonSerializer.Serialize(_bridge.SelectWorldProps().EnumerateObject().Where(item => item.Name.StartsWith("bathhouse/", StringComparison.Ordinal))
        .OrderBy(item => item.Name, StringComparer.Ordinal).ToDictionary(item => item.Name, item => item.Value.GetRawText()));
    private string? Custody(string id) => _bridge.SelectRuntimeState().GetProperty("world.custody").EnumerateArray()
        .Single(item => item.GetProperty("itemId").GetString() == id).GetProperty("custodyOwnerId").GetString();
    private static Vector3 Exterior(Vector3 point) => point with { Y = AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .04f };
    private void Aim(Vector3 point)
    {
        // The head's anatomical offset moves the camera when yaw changes.
        // Converge from the resulting pose; AimedAt still checks its actual ray.
        for (var iteration = 0; iteration < 6; iteration++)
        {
            var delta = point - _camera.GlobalPosition;
            _player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length())),
                Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
        }
    }
    private bool AimedAt(Node3D target)
    {
        using var ray = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition, _camera.GlobalPosition - _camera.GlobalBasis.Z * 2.7f, 7);
        ray.Exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
        var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        return hit.Count > 0 && hit["collider"].AsGodotObject() == target;
    }
    private async Task Capture(string name, Vector3? intendedAim = null)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_FACILITY_CAPTURE") != "1") return;
        if (Engine.TimeScale != 1) throw new InvalidOperationException("Screenshots require normal time scale.");
        if (intendedAim is { } point)
        {
            // Mosque03 captured an unintended view after an immediate Aim while
            // the native window lacked focus. Settle focus/input first, then
            // give the actual camera pose physics/process frames to render.
            await EnsureFocus();
            await Frames(3);
            Aim(point);
            await Frames(3);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var cameraBefore = _camera.GetCameraTransform();
        var frameBefore = Engine.GetFramesDrawn();
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "facility/" + name);
        var cameraAfter = _camera.GetCameraTransform();
        Directory.CreateDirectory(Output);
        var file = Path.Combine(Output, name + ".png");
        if (File.Exists(file)) throw new IOException("Refusing to overwrite " + file);
        using var pixels = GetViewport().GetTexture().GetImage();
        if (pixels.IsEmpty() || pixels.SavePng(file) != Error.Ok) throw new IOException(file);
        if (_mosque is not null && GodotObject.IsInstanceValid(_mosque)
            && (name.StartsWith("08", StringComparison.Ordinal) || name.StartsWith("09", StringComparison.Ordinal) || name == "failure"))
            RecordMosqueCaptureMaterials(name);
        if (name == "08_mosque_entrance")
        {
            // The low approach view shows a large plaster block. Resolve its
            // actual visible mesh before changing a header, gate or canopy.
            Act1VisibleSurfaceProbe.Log(GetTree().Root, _camera, "facility/" + name + "/plaster-block-owner",
                new Vector2(.55f, .40f), new Vector2(.55f, .50f));
            _events.Add(new { kind = "visible-surface-owner-probe", file,
                normalizedPixels = new[] { "(.55,.40)", "(.55,.50)" }, camera = _camera.GlobalTransform.ToString(),
                resultLocation = "act1-visible-surface lines in this run's log", meshVisibilityChanged = false });
        }
        if (name is "failure" or "02c_bath_partition_before_open")
        {
            Act1VisibleSurfaceProbe.Log(GetTree().Root, _camera, "facility/" + name + "/left-visible-owner",
                new Vector2(.03f, .85f), new Vector2(.08f, .80f));
            _events.Add(new { kind = "visible-surface-owner-probe", file,
                normalizedPixels = new[] { "(.03,.85)", "(.08,.80)" }, camera = _camera.GlobalTransform.ToString(),
                resultLocation = "act1-visible-surface lines in this run's log", meshVisibilityChanged = false });
        }
        var aimDot = intendedAim is { } destination
            ? (-cameraAfter.Basis.Z).Normalized().Dot((destination - cameraAfter.Origin).Normalized()) : (float?)null;
        var cameraTravel = cameraBefore.Origin.DistanceTo(cameraAfter.Origin);
        var cameraForwardDot = cameraBefore.Basis.Z.Normalized().Dot(cameraAfter.Basis.Z.Normalized());
        var cameraEvidence = new { kind = "native-game-camera", file, focus = GetWindow().HasFocus(),
            intendedAim = intendedAim?.ToString(), cameraBefore = cameraBefore.ToString(), cameraAfter = cameraAfter.ToString(),
            cameraUnchangedDuringRender = cameraBefore.IsEqualApprox(cameraAfter), cameraTravel, cameraForwardDot, frameBefore,
            frameAfter = Engine.GetFramesDrawn(), intendedAimDot = aimDot, humanArtReview = "not-run" };
        _events.Add(cameraEvidence);
        GD.Print("act1-facility-camera: " + JsonSerializer.Serialize(cameraEvidence));
        if (intendedAim.HasValue)
            Check(aimDot is > .9999f && cameraTravel < .015f && cameraForwardDot > .99999f,
                name + ": the actual camera retains the intended view across the rendered frame");
    }
    private void RecordMosqueCaptureMaterials(string capture)
    {
        // Mosque05 switched to the existing low-preset startup rescue after its
        // first frame. Record actual presentation state, without overriding it
        // or treating these instrumented frames as a performance measurement.
        var meshes = new List<MeshInstance3D>
        {
            _mosque.GetParent().GetNode<MeshInstance3D>("MosqueHallEastLeft"),
            _mosque.GetNode<MeshInstance3D>("MosqueTimberFloor")
        };
        meshes.AddRange(_mosque.GetNode<Node3D>("Npc_timur_hazrat")
            .FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .Where(mesh => mesh.IsVisibleInTree()).OrderByDescending(mesh => mesh.Name.ToString().Contains("Coat", StringComparison.OrdinalIgnoreCase))
            .ThenBy(mesh => mesh.GetPath().ToString(), StringComparer.Ordinal).Take(3));
        var materials = new List<object>();
        foreach (var mesh in meshes)
        for (var surface = 0; surface < (mesh.Mesh?.GetSurfaceCount() ?? 0); surface++)
        {
            var material = mesh.GetActiveMaterial(surface);
            var shader = material as ShaderMaterial;
            string? Parameter(string name) => shader?.Shader?.Code.Contains(name, StringComparison.Ordinal) == true
                ? shader.GetShaderParameter(name).ToString() : null;
            var texture = shader?.Shader?.Code.Contains("albedo_texture", StringComparison.Ordinal) == true
                ? shader.GetShaderParameter("albedo_texture").AsGodotObject() as Texture2D : null;
            materials.Add(new { mesh = mesh.GetPath().ToString(), surface, visible = mesh.IsVisibleInTree(),
                material = material?.GetClass(), materialPath = material?.ResourcePath, materialId = material?.GetInstanceId(),
                shaderPath = shader?.Shader?.ResourcePath, shaderId = shader?.Shader?.GetInstanceId(),
                lowQuality = Parameter("low_quality"), hasAlbedoTexture = Parameter("has_albedo_texture"),
                textureStrength = Parameter("texture_strength"), textureScale = Parameter("texture_scale"), texturePath = texture?.ResourcePath });
        }
        var evidence = new { kind = "mosque-capture-materials", capture, scope = MosqueOnly ? "mosque-only" : "all-facilities",
            graphicsPreset = _player.GraphicsPreset, savedPreferencesPreset = UserSettingsStore.TryLoad()?.GraphicsPreset,
            textureLoadsSuppressed = PainterlyMaterialLibrary.SuppressTextureLoadsForHeadlessTests, materials,
            stateChangedByObservation = false, foregroundPerformanceMeasured = false };
        _events.Add(evidence);
        GD.Print("act1-facility-materials: " + JsonSerializer.Serialize(evidence));
    }
    private async Task WaitUntil(Func<bool> done, string label)
    {
        var started = Time.GetTicksMsec();
        while (!done() && Time.GetTicksMsec() - started < 12000) await Frames(1);
        Check(done(), label + (_carry is null ? "" : "; " + _carry.DescribeAim()));
    }
    private async Task EnsureFocus()
    {
        if (DisplayServer.WindowIsFocused()) return;
        GetWindow().GrabFocus();
        await WaitUntil(() => DisplayServer.WindowIsFocused(),
            "native window has actual focus; background input alone does not advance facility time or door animation");
    }
    private static void ReleaseInputs() { foreach (var action in Actions) Input.ActionRelease(action); }
    private async Task Press(string action) { Input.ActionPress(action); await Frames(2); Input.ActionRelease(action); await Frames(3); }
    private async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true })
                throw new InvalidOperationException("The actual pause menu interrupted the facility proof.");
        }
    }
    private void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        _checks++; _events.Add(new { kind = "check", label, passed = true }); GD.Print("act1-facilities: " + label);
    }
}
