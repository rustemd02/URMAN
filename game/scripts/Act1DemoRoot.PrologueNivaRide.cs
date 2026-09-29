using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // N2.2 prologue (beat sheet P3-P4): after the forest teaser cuts to
    // black, Aidar rides the last stretch to Kara-Urman on the Niva's front
    // passenger seat. The car follows the entry road to the stop where the
    // world's parked Niva stands; during the ride Mansur's teasing talk
    // contains the introductory language conversation.
    private Node3D? _rideNiva;
    private Camera3D? _rideCamera;
    private CanvasLayer? _playerHudForRide;
    private bool _playerHudWasVisibleForRide;
    private VehicleController? _parkedNivaForRide;
    private bool _parkedNivaWasVisible;
    private Vector2 _rideLook;

    private const string PrologueRideDialogue = "urman.chapter1:dialogue/prologue-niva-language";

    // The village leg: the far-bank lane, the bridge and the FAP branch as before,
    // then the whole main street north (right-hand lane), once round the square's
    // ring road and back down the other lane to the arrival stop. Built from the
    // street axes so it can never drift from the roads it drives on.
    private static readonly Vector3[] PrologueRidePath = BuildPrologueRidePath();

    private const float RideLane = 1.3f;

    /// <summary>The village-leg polyline, for route diagnostics.</summary>
    internal static IReadOnlyList<Vector3> RideRoute => PrologueRidePath;

    private static Vector3[] BuildPrologueRidePath()
    {
        var path = new List<Vector3>
        {
            new(69f, 0, 8f), new(69f, 0, -18f), new(68f, 0, -21f), new(63f, 0, -24.4f),
            new(58f, 0, -25f), new(41.5f, 0, -25f), new(38f, 0, -23.8f), new(32f, 0, -24.2f),
            new(28f, 0, -26.2f), new(23f, 0, -25f), new(17f, 0, -21.5f), new(10f, 0, -17f),
            new(4.5f, 0, -12.5f), new(0f, 0, -10f), new(-.6f, 0, -1.5f)
        };
        // Main axis south to north from the village entrance to the ring's south entry.
        var north = AgentBAct1Layout.MainRoadAxis.Where(point => point.Y >= 9f).OrderBy(point => point.Y).ToArray();
        foreach (var point in north) path.Add(new(point.X + RideLane, 0, point.Y));
        // Once round the ring, counter-clockwise, on the outer (right-hand) side of the island.
        var ring = AgentBAct1Layout.SquareRingAxis;
        var centre = new Vector2(.5f, 186f);
        foreach (var point in ring.Skip(1))
        {
            var outward = (point - centre).Normalized();
            path.Add(new(point.X + outward.X * RideLane, 0, point.Y + outward.Y * RideLane));
        }
        // Down the other lane to the arrival stop.
        foreach (var point in north.Reverse().Skip(1)) path.Add(new(point.X - RideLane, 0, point.Y));
        path.Add(new(-1.65f, 0, 1f));
        return path.ToArray();
    }

    /// <summary>Distance along the ride path of the first vertex within reach of (x, z), searching forward from <paramref name="after"/>.</summary>
    private static float RideMark(float x, float z, float after = 0f, float reach = 6f)
    {
        var walked = 0f;
        for (var index = 0; index < PrologueRidePath.Length; index++)
        {
            if (index > 0) walked += PrologueRidePath[index - 1].DistanceTo(PrologueRidePath[index]);
            if (walked < after) continue;
            if (new Vector2(PrologueRidePath[index].X - x, PrologueRidePath[index].Z - z).Length() <= reach) return walked;
        }
        return after;
    }

    private async Task RunPrologueNivaRideAsync()
    {
        if (_main is null || _player is null)
        {
            return;
        }

        _main.SwitchZone("village_day", "arrival");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree() || _prologueSkipRequested) return;
        _player.SetSessionTransition(true);

        var definition = LoadBabayNivaDefinition();
        if (definition is null)
        {
            GD.PushError("act1-prologue-ride: the authored Niva definition is unavailable; keeping the arrival opening.");
            return;
        }

        _playerHudForRide = _player.GetNodeOrNull<CanvasLayer>("Hud");
        if (_playerHudForRide is not null)
        {
            _playerHudWasVisibleForRide = _playerHudForRide.Visible;
            _playerHudForRide.Visible = false;
        }

        var visual = VehicleVisualFactory.Build(definition);
        _rideNiva = visual.Root;
        _rideNiva.Name = "PrologueNivaRide";
        _rideNiva.SetMeta("presentationOnly", true);
        _rideNiva.SetMeta("visualOnly", true);
        _rideNiva.SetMeta("presentationOwner", nameof(Act1DemoRoot));
        _main.AddChild(_rideNiva);
        _rideCamera = new Camera3D
        {
            Name = "PrologueRideCamera",
            // The factory Niva is left-hand drive; the front passenger seat
            // sits on +X, eye height above the seat cushion.
            Position = new(.42f, 1.44f, .18f),
            Fov = _player.GetNodeOrNull<Camera3D>("Head/Camera3D")?.Fov ?? 75f
        };
        _rideNiva.AddChild(_rideCamera);
        BuildPrologueDriver();
        // Aidar wakes toward the person speaking, then can look out through the windshield.
        _rideLook = new(35f, 0f);
        ApplyPrologueRideLook();
        _player.SetModalOpen(false);

        _parkedNivaForRide = (GetTree().GetFirstNodeInGroup("vehicle_fleet") as VehicleFleet)
            ?.Vehicles.FirstOrDefault(vehicle => vehicle.Definition.Id == definition.Id);
        if (_parkedNivaForRide is not null)
        {
            _parkedNivaWasVisible = _parkedNivaForRide.Visible;
            _parkedNivaForRide.Visible = false;
        }

        _rideCamera.MakeCurrent();
        if (_prologueCaption is not null) _prologueCaption.Visible = false;
        DialogueUi.ChoiceAccepted += OnPrologueRideChoice;
        var rideStarted = Time.GetTicksMsec();
        var dialogueUi = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;

        // Leg A (author feedback 2026-09-29): the long winter road from the
        // district bus stop, through the fields and the forest the road cuts.
        _approachRoad = new PrologueApproachRoad();
        _main.AddChild(_approachRoad);
        var approach = _approachRoad;
        PlaceOnApproach(approach, 18f, snap: true);
        // Jolted awake: slumped against the window, then up with a start.
        _rideLook = new(62f, -32f);
        ApplyPrologueRideLook();
        FadePrologueBlackout(visible: false);
        WakeJolt();
        var driven = 0f;
        var speed = 0f;
        var calibrationOpened = false;
        var calibrationCaptioned = false;
        var barkIndex = 0;
        var barks = new (float At, string Id)[]
        {
            (1f, "prologue-ride-bark-nightmare"), (48f, "prologue-ride-bark-wake"), (95f, "prologue-ride-bark-bus"), (160f, "prologue-ride-bark-kazan"),
            (205f, "prologue-ride-bark-sign"), (ApproachForestAt - 40f, "prologue-ride-bark-field"),
            (ApproachForestAt + 10f, "prologue-ride-bark-forest"), (ApproachForestAt + 150f, "prologue-ride-bark-marat")
        };
        const float calibrationAt = 265f;
        var total = PrologueApproachRoad.RoadLength + 18f;
        while (IsInsideTree() && driven < total - 4f)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_prologueSkipRequested) break;
            var delta = (float)GetProcessDeltaTime();
            var talking = dialogueUi?.IsOpen == true;
            UpdateRideLook(talking, delta);
            var target = talking ? 3f : driven > ApproachForestAt - 20f ? 6.5f : 8.5f;
            speed = Mathf.MoveToward(speed, target, 2.2f * delta);
            driven += speed * delta;
            var before = _rideNiva.GlobalPosition;
            PlaceOnApproach(approach, 18f - driven, snap: false);
            SpinRideWheels(visual, before);
            while (barkIndex < barks.Length && driven >= barks[barkIndex].At)
                RideBark(bridge, barks[barkIndex++].Id);
            if (!calibrationOpened && driven >= calibrationAt && bridge is not null)
            {
                calibrationOpened = true;
                bridge.OpenDialogueUi(PrologueRideDialogue);
            }
            if (calibrationOpened && !calibrationCaptioned && dialogueUi?.IsOpen != true)
            {
                calibrationCaptioned = true;
                ShowPrologueCaption($"Татарский в разговорах: {LanguageLevelLabel(_player.TatarLanguageLevel)}. Это можно поменять в настройках.", 5);
            }
            if (approach.EdgeFigure is { Visible: true } figure && _rideNiva.GlobalPosition.Z < figure.GlobalPosition.Z + 12f)
                figure.Visible = false;
        }

        GD.Print($"act1-prologue: approach road {driven:0}m {(Time.GetTicksMsec() - rideStarted) / 1000.0:0.0}s skipped={_prologueSkipRequested}");
        var villageLegStarted = Time.GetTicksMsec();
        // Through the trees into the village: a short dip, not a loading screen.
        if (!_prologueSkipRequested && IsInsideTree())
        {
            FadePrologueBlackout(visible: true);
            await PrologueWaitAsync(.45);
        }
        ReleaseApproachRoad();

        // Leg B: the far bank, the whole bridge and the village street home.
        var villageLength = 0f;
        for (var index = 1; index < PrologueRidePath.Length; index++)
            villageLength += PrologueRidePath[index - 1].DistanceTo(PrologueRidePath[index]);
        PlaceRideAt(0);
        if (!_prologueSkipRequested) FadePrologueBlackout(visible: false);
        // Tour of the village: each remark waits for the previous one to finish
        // and for the car to reach its place, so nothing talks over anything.
        var northLeg = RideMark(0f + RideLane, 9f, 60f);
        var ringStart = RideMark(.5f, 179.5f, northLeg);
        var villageBarks = new (float At, string Id)[]
        {
            (3f, "prologue-ride-bark-bridge"),
            (RideMark(32f, -24.2f), "prologue-ride-bark-fap"),
            (RideMark(0f, -10f), "prologue-ride-bark-street"),
            (northLeg + 6f, "prologue-ride-bark-edge"),
            (RideMark(1.2f + RideLane, 62f, northLeg), "prologue-ride-bark-fields"),
            (RideMark(-3f + RideLane, 108f, northLeg), "prologue-ride-bark-lower-street"),
            (RideMark(-1.5f + RideLane, 150f, northLeg), "prologue-ride-bark-square-road"),
            (ringStart, "prologue-ride-bark-square"),
            (ringStart + 24f, "prologue-ride-bark-square-year"),
            (RideMark(-1.5f - RideLane, 148f, ringStart), "prologue-ride-bark-back"),
            (RideMark(0f - RideLane, 20f, ringStart), "prologue-ride-bark-home")
        };
        var villageBark = 0;
        var nextBarkReady = 0.0;
        var along = 0f;
        speed = 4.5f;
        while (IsInsideTree() && along < villageLength)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_prologueSkipRequested) break;
            var delta = (float)GetProcessDeltaTime();
            UpdateRideLook(dialogueUi?.IsOpen == true, delta);
            var remaining = villageLength - along;
            var onRing = along >= ringStart - 8f && along <= ringStart + 52f;
            speed = Mathf.MoveToward(speed, remaining < 12f ? 1.6f : onRing ? 3.2f : along > northLeg ? 5.2f : 4.2f, 1.8f * delta);
            along += speed * delta;
            var before = _rideNiva.GlobalPosition;
            PlaceRideAt(MathF.Min(along / villageLength, 1f), eased: false);
            SpinRideWheels(visual, before);
            _prologueElapsed += delta;
            if (villageBark < villageBarks.Length && along >= villageBarks[villageBark].At && _prologueElapsed >= nextBarkReady)
            {
                var id = villageBarks[villageBark++].Id;
                RideBark(bridge, id);
                var text = bridge?.ResolveText($"urman.chapter1:text/{id}") ?? "";
                nextBarkReady = _prologueElapsed + Math.Clamp(text.Length / 15.0, 4.5, 11) + 1.5;
            }
        }
        if (!_prologueSkipRequested) await PrologueWaitAsync(2.2);
        GD.Print($"act1-prologue: village leg {(Time.GetTicksMsec() - villageLegStarted) / 1000.0:0.0}s");

        FadePrologueBlackout(visible: true);
        await PrologueWaitAsync(.3);
        ReleasePrologueRide();
    }

    private PrologueApproachRoad? _approachRoad;
    private const float ApproachForestAt = 18f - PrologueApproachRoad.ForestStart;

    // Distance along the approach road (local Z runs toward -Z); the body
    // yaws smoothly into the road's direction and rests on its surface.
    private void PlaceOnApproach(PrologueApproachRoad road, float localZ, bool snap)
    {
        if (_rideNiva is null) return;
        var here = road.RoadPoint(localZ) + Vector3.Right * 1.35f;
        var ahead = road.RoadPoint(localZ - 3f) + Vector3.Right * 1.35f;
        var forward = ahead - here;
        var yaw = Mathf.Atan2(-forward.X, -forward.Z);
        var pitch = Mathf.Atan2(forward.Y, new Vector2(forward.X, forward.Z).Length());
        var smoothing = snap ? 1f : 1f - MathF.Exp(-6f * (float)GetProcessDeltaTime());
        _rideNiva.Rotation = new Vector3(Mathf.LerpAngle(_rideNiva.Rotation.X, pitch, smoothing), Mathf.LerpAngle(_rideNiva.Rotation.Y, yaw, smoothing), 0);
        _rideNiva.GlobalPosition = here;
    }

    private void SpinRideWheels(VehicleVisualFactory.Visual visual, Vector3 before)
    {
        if (_rideNiva is null) return;
        var turn = before.DistanceTo(_rideNiva.GlobalPosition) / .345f;
        foreach (var wheel in visual.Wheels) wheel.RotateX(-turn);
    }

    private async void WakeJolt()
    {
        if (_rideCamera is null) return;
        var from = _rideLook;
        var to = new Vector2(35f, -2f);
        var elapsed = 0f;
        while (elapsed < .55f && _rideCamera is not null && IsInstanceValid(_rideCamera))
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += (float)GetProcessDeltaTime();
            var k = Mathf.Clamp(elapsed / .35f, 0f, 1f);
            _rideLook = from.Lerp(to, 1f - (1f - k) * (1f - k) * (1f - k));
            ApplyPrologueRideLook();
            var shake = (1f - Mathf.Clamp(elapsed / .55f, 0f, 1f)) * 1.6f;
            _rideCamera.RotationDegrees += new Vector3(Mathf.Sin(elapsed * 71f) * shake, Mathf.Sin(elapsed * 53f) * shake, 0);
        }
        await PrologueWaitAsync(7.5);
        ShowPrologueCaption("Сон. Просто сон… Лес, следы на снегу, этот голос за спиной.", 5);
    }

    private void UpdateRideLook(bool talking, float delta)
    {
        if (talking) return;
        var look = Input.GetVector("look_left", "look_right", "look_up", "look_down");
        _rideLook -= look * delta * 105f;
        ApplyPrologueRideLook();
    }

    // Babai's talk on the road: authored lines with Tatar density variants,
    // shown as subtitles; the car keeps going.
    private void RideBark(RuntimeBridge? bridge, string localTextId)
    {
        if (bridge is null) return;
        var text = bridge.ResolveText($"urman.chapter1:text/{localTextId}");
        _ = bridge.ObserveVocabularyTextAsync(text, $"urman.chapter1:text/{localTextId}");
        ShowPrologueCaption("Мансур бабай: " + text, Math.Clamp(text.Length / 15.0, 4.5, 11));
    }

    private static string LanguageLevelLabel(string level) => level switch
    {
        "fluent" => "свободно — бабай говорит по-татарски",
        "some" => "немного понимаю — вперемешку с русским",
        _ => "почти не знаю — по-русски, татарские слова по одному"
    };

    private void ReleaseApproachRoad()
    {
        if (_approachRoad is not null && IsInstanceValid(_approachRoad)) _approachRoad.QueueFree();
        _approachRoad = null;
    }

    private void ApplyPrologueRideLook()
    {
        _rideLook.X = Mathf.Clamp(_rideLook.X, -95f, 105f);
        _rideLook.Y = Mathf.Clamp(_rideLook.Y, -45f, 40f);
        if (_rideCamera is not null) _rideCamera.RotationDegrees = new(_rideLook.Y, _rideLook.X, 0);
    }

    private void BuildPrologueDriver()
    {
        var driver = GeneratedCharacterKitDressing.Attach(_rideNiva!, "prologue-mansur", "Mansur", Vector3.Zero, sheltered: true);
        foreach (var animation in driver.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>())
            animation.Stop();
        driver.RotationDegrees = new(0, 180, 0);
        // The source body's crown is 1.82 m; Mansur is 1.72 m. He takes off his
        // ushanka in the heated cabin, keeping the roof clearance of the real model.
        driver.Scale = Vector3.One * (1.72f / 1.82f);
        foreach (var hat in driver.FindChildren("Mansur_Hat*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            hat.Visible = false;
        var skeleton = driver.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>()
            .First(item => item.Name.ToString().Contains("Mansur", StringComparison.Ordinal)
                || item.GetParent().Name.ToString().Contains("Mansur", StringComparison.Ordinal));
        skeleton.ResetBonePoses();
        var pelvis = skeleton.GetBoneGlobalPose(skeleton.FindBone("pelvis")).Origin;
        driver.GlobalPosition += _rideNiva!.ToGlobal(new(-.4f, .86f, .22f)) - skeleton.ToGlobal(pelvis);
        foreach (var (side, x) in new[] { ("l", -.23f), ("r", -.57f) })
        {
            AimBone("thigh_" + side, "calf_" + side, new(x, .72f, -.19f));
            AimBone("calf_" + side, "foot_" + side, new(x, .48f, -.53f));
            AimBone("upperarm_" + side, "lowerarm_" + side, new(x, 1.13f, .07f));
            AimBone("lowerarm_" + side, "hand_" + side, new(x, 1.08f, -.25f));
        }

        // Pose this existing rig against the authored seat and wheel; no second NPC state.
        void AimBone(string boneName, string childName, Vector3 target)
        {
            var bone = skeleton.FindBone(boneName);
            var pose = skeleton.GetBoneGlobalPose(bone);
            var current = skeleton.GetBoneGlobalPose(skeleton.FindBone(childName)).Origin - pose.Origin;
            var desired = skeleton.ToLocal(_rideNiva.ToGlobal(target)) - pose.Origin;
            pose.Basis = new Basis(new Quaternion(current.Normalized(), desired.Normalized())) * pose.Basis;
            skeleton.SetBoneGlobalPose(bone, pose);
        }
    }

    private static VehicleDefinition? LoadBabayNivaDefinition()
    {
        try
        {
            foreach (var definition in VehicleDefinition.Load("res://content/vehicles/act1_vehicles.v1.json"))
            {
                if (definition.Id == "babay-niva") return definition;
            }
        }
        catch (Exception error)
        {
            GD.PushError($"act1-prologue-ride: vehicle definitions failed to load: {error}");
        }
        return null;
    }

    // Distance-parameterised placement over the authored polyline with
    // smooth ends; the car banks its yaw toward the active segment and the
    // body rests on the terrain like the parked runtime vehicles.
    private void PlaceRideAt(float progress, bool eased = true)
    {
        if (_rideNiva is null) return;
        var shaped = eased ? progress * progress * (3f - 2f * progress) : progress;
        var total = 0f;
        for (var index = 1; index < PrologueRidePath.Length; index++)
        {
            total += PrologueRidePath[index - 1].DistanceTo(PrologueRidePath[index]);
        }
        var target = shaped * total;
        var walked = 0f;
        var position = PrologueRidePath[0];
        var forward = PrologueRidePath[1] - PrologueRidePath[0];
        for (var index = 1; index < PrologueRidePath.Length; index++)
        {
            var segment = PrologueRidePath[index] - PrologueRidePath[index - 1];
            var length = segment.Length();
            if (walked + length >= target || index == PrologueRidePath.Length - 1)
            {
                var local = Mathf.Clamp((target - walked) / MathF.Max(length, .0001f), 0f, 1f);
                position = PrologueRidePath[index - 1] + segment * local;
                forward = segment;
                break;
            }
            walked += length;
        }
        var yaw = Mathf.Atan2(-forward.X, -forward.Z);
        _rideNiva.Rotation = new Vector3(0, progress == 0 ? yaw
            : Mathf.LerpAngle(_rideNiva.Rotation.Y, yaw, 1f - MathF.Exp(-7f * (float)GetProcessDeltaTime())), 0);
        var bridgeX = (float)AgentBAct1HeightField.RavineCentre(Act1ConnectedWorld.RavineBridgeZ);
        var height = MathF.Abs(position.Z - Act1ConnectedWorld.RavineBridgeZ) < .2f && MathF.Abs(position.X - bridgeX) <= 8f
            ? Act1ConnectedWorld.OpeningBridgeRoadHeight(position.X)
            : AgentBAct1HeightField.CollisionGround(position.X, position.Z);
        _rideNiva.GlobalPosition = new(position.X, height + .02f, position.Z);
    }

    // The answer to Mansur sets the starting Tatar density; "skip" keeps
    // whatever the player's profile already says.
    private void OnPrologueRideChoice(string dialogueId, string choiceId)
    {
        if (dialogueId != PrologueRideDialogue) return;
        var current = _player?.TatarLanguageLevel ?? "none";
        var level = choiceId switch
        {
            "answer-none" => "none", "answer-some" => "some", "answer-fluent" => "fluent",
            "level-lower" => current == "fluent" ? "some" : "none",
            "level-raise" => current == "none" ? "some" : "fluent",
            _ => null
        };
        if (level is null || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge) return;
        _ = bridge.SetTatarLanguageLevelAsync(level);
    }

    private void ReleasePrologueRide()
    {
        DialogueUi.ChoiceAccepted -= OnPrologueRideChoice;
        ReleaseApproachRoad();
        if (IsInsideTree())
        {
            (GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi)?.CloseIfDialogue(PrologueRideDialogue);
        }
        if (_player is not null && IsInstanceValid(_player)) _player.SetSessionTransition(false);
        if (_player?.GetNodeOrNull<Camera3D>("Head/Camera3D") is { } playerCamera
            && GodotObject.IsInstanceValid(playerCamera))
        {
            playerCamera.MakeCurrent();
        }
        if (_rideNiva is not null && GodotObject.IsInstanceValid(_rideNiva))
        {
            _rideNiva.QueueFree();
        }
        if (_playerHudForRide is not null && GodotObject.IsInstanceValid(_playerHudForRide))
        {
            _playerHudForRide.Visible = _playerHudWasVisibleForRide;
        }
        if (_parkedNivaForRide is not null && IsInstanceValid(_parkedNivaForRide))
            _parkedNivaForRide.Visible = _parkedNivaWasVisible;
        _parkedNivaForRide = null;
        _rideNiva = null;
        _rideCamera = null;
        _playerHudForRide = null;
    }
}
