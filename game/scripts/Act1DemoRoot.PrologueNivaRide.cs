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
    private static readonly Vector3 RideCameraRest = new(.42f, 1.44f, .30f);

    private const string PrologueRideDialogue = "urman.chapter1:dialogue/prologue-niva-language";

    // The village leg: the far-bank lane, the bridge and the FAP street, north up the
    // main street, once round Мәйдан and back to the arrival stop. Built from the
    // street axes so it can never drift from the roads it drives on.
    private static readonly Vector3[] PrologueRidePath = BuildPrologueRidePath();

    private const float RideLane = 1.3f;

    /// <summary>The village-leg polyline, for route diagnostics.</summary>
    internal static IReadOnlyList<Vector3> RideRoute => PrologueRidePath;

    private static Vector3[] BuildPrologueRidePath()
    {
        // The old x=69 lane now runs through a moved yard. Follow the same
        // far-bank polyline that owns the visible street and the terrain.
        var path = Act1ConnectedWorld.FarBankPlot().Roads.Single(road => road.Id == "yar-north")
            .Points.Take(3).Reverse().Select(point => new Vector3(point.X, 0, point.Y)).ToList();
        path.AddRange(new Vector3[]
        {
            new(58f, 0, -25f), new(47f, 0, -25f), new(44f, 0, -25f), new(41.5f, 0, -25f), new(38.5f, 0, -25f), new(37f, 0, -24f), new(32f, 0, -24.2f),
        });
        // Offset the real axes to the vehicle's right-hand side.
        void Lane(IReadOnlyList<Vector2> axis, float lateral)
        {
            for (var i = 0; i < axis.Count; i++)
            {
                var incoming = (axis[i] - axis[Math.Max(0, i - 1)]).Normalized();
                var outgoing = (axis[Math.Min(axis.Count - 1, i + 1)] - axis[i]).Normalized();
                if (incoming == Vector2.Zero) incoming = outgoing;
                if (outgoing == Vector2.Zero) outgoing = incoming;
                var n0 = new Vector2(-incoming.Y, incoming.X);
                var n1 = new Vector2(-outgoing.Y, outgoing.X);
                var miter = (n0 + n1).Normalized();
                var at = axis[i] + miter * (lateral / Mathf.Max(.5f, miter.Dot(n1)));
                if (path.Count == 0 || path[^1].DistanceTo(new(at.X, 0, at.Y)) > .05f)
                    path.Add(new(at.X, 0, at.Y));
            }
        }
        // Relayout v3: the straight FAP street (Урман урамы) west to the main street, north
        // along Тукай урамы past the arrival stop to Мәйдан, once round the square's garden
        // (west of the main street, z 33-63) and back south to the stop by the house.
        Lane(AgentBAct1Layout.FapBranchAxis.Reverse().ToArray(), RideLane);
        Lane(new[] { new Vector2(-0.5f, -24f) }.Concat(AgentBAct1Layout.MainRoadAxis
            .Where(p => p.Y > -24f && p.Y <= -1.5f).Reverse()).ToArray(), RideLane);
        // Past the arrival stop on the east half: the sign and bench of the stop stand on the west verge.
        path.Add(new(1.3f, 0, 2f)); path.Add(new(1.3f, 0, 10f)); path.Add(new(-1.3f, 0, 25f));
        Lane(new[] { new Vector2(0f, 25f), new Vector2(0f, 40f), new Vector2(0f, 52f) }, RideLane);
        // Round Мәйдан on its paved carriageway, then south again.
        foreach (var point in AgentBAct1Layout.PlazaDriveAxis) path.Add(new(point.X, 0, point.Y));
        var mainSouth = new[] { new Vector2(0f, 30f) }
            .Concat(AgentBAct1Layout.MainRoadAxis.Where(p => p.Y < 30f && p.Y >= 0f)).Append(new Vector2(0f, 4f)).ToArray();
        Lane(mainSouth, RideLane);
        path.Add(new(-1.3f, 0, 1f));
        path.Add(new(-1.65f, 0, 1f));
        return path.ToArray();
    }

    /// <summary>First point of the actual route within reach, including the
    /// middle of long segments, searching forward from <paramref name="after"/>.</summary>
    private static float RideMark(float x, float z, float after = 0f, float reach = 6f)
    {
        var walked = 0f;
        var target = new Vector2(x, z);
        for (var index = 1; index < PrologueRidePath.Length; index++)
        {
            var a = new Vector2(PrologueRidePath[index - 1].X, PrologueRidePath[index - 1].Z);
            var b = new Vector2(PrologueRidePath[index].X, PrologueRidePath[index].Z);
            var direction = b - a; var length = direction.Length();
            if (length < .001f) continue;
            var minimum = Mathf.Max(0, (after - walked) / length);
            if (minimum <= 1)
            {
                if (a.Lerp(b, minimum).DistanceTo(target) <= reach) return walked + minimum * length;
                var from = a - target; var aa = direction.LengthSquared();
                var bb = 2 * from.Dot(direction); var cc = from.LengthSquared() - reach * reach;
                var discriminant = bb * bb - 4 * aa * cc;
                if (discriminant >= 0)
                {
                    var enter = (-bb - Mathf.Sqrt(discriminant)) / (2 * aa);
                    if (enter >= minimum && enter <= 1) return walked + enter * length;
                }
            }
            walked += length;
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
            // A little further back and a narrower lens: at 75+ degrees the cabin read as a
            // wide-angle action-cam shot (author feedback 2026-09-29).
            Position = RideCameraRest,
            Fov = Mathf.Min(_player.GetNodeOrNull<Camera3D>("Head/Camera3D")?.Fov ?? 75f, 62f)
        };
        _rideNiva.AddChild(_rideCamera);
        // A warm dome light: the cabin was a cold grey box against the white road.
        _rideNiva.AddChild(new OmniLight3D
        {
            Name = "PrologueCabinLight",
            Position = new(0f, 1.5f, .05f),
            LightColor = new Color(1f, .86f, .66f),
            LightEnergy = .5f,
            LightSpecular = 0f,
            OmniRange = 2.6f,
            ShadowEnabled = false
        });
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
        DriverShakeAwake();
        var driven = 0f;
        var speed = 0f;
        var calibrationOpened = false;
        var calibrationCaptioned = false;
        var barkIndex = 0;
        var barks = new (float At, string Id)[]
        {
            (1f, "prologue-ride-bark-nightmare"), (48f, "prologue-ride-bark-wake"), (95f, "prologue-ride-bark-bus"), (122f, "prologue-ride-bark-radio"), (160f, "prologue-ride-bark-kazan"),
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
            TickRideRoad(delta, speed);
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
        var northLeg = RideMark(0f - RideLane, 9f, 60f);
        // Мәйдан is on the right going north (west of the main street, z 33-63); the car goes
        // once round its garden, then back south past Яңа урам (on the right) to the house.
        var squareAt = RideMark(-12f, 58.8f, northLeg, 2f);
        var villageBarks = new (float At, string Id)[]
        {
            (3f, "prologue-ride-bark-bridge"),
            (RideMark(32f, -24.2f), "prologue-ride-bark-fap"),
            (RideMark(0f, -10f), "prologue-ride-bark-street"),
            (northLeg + 6f, "prologue-ride-bark-edge"),
            (RideMark(0f - RideLane, 26f, northLeg), "prologue-ride-bark-square-road"),
            (squareAt, "prologue-ride-bark-square"),
            (RideMark(-21.5f, 48f, squareAt, 2f), "prologue-ride-bark-square-year"),
            (RideMark(-12f, 37.4f, squareAt, 2f), "prologue-ride-bark-fields"),
            (RideMark(0f + RideLane, 30f, squareAt + 20f), "prologue-ride-bark-back"),
            (RideMark(0f + RideLane, 16f, squareAt + 20f), "prologue-ride-bark-lower-street"),
            (RideMark(0f + RideLane, 5f, squareAt + 20f), "prologue-ride-bark-home")
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
            // Slow past the square so it can be seen from the passenger seat.
            var onRing = along >= squareAt - 12f && along <= squareAt + 48f;
            speed = Mathf.MoveToward(speed, remaining < 12f ? 1.6f : onRing ? 3.2f : along > northLeg ? 5.2f : 4.2f, 1.8f * delta);
            along += speed * delta;
            var before = _rideNiva.GlobalPosition;
            PlaceRideAt(MathF.Min(along / villageLength, 1f), eased: false);
            SpinRideWheels(visual, before);
            TickRideRoad(delta, speed);
            _prologueElapsed += delta;
            if (villageBark < villageBarks.Length && along >= villageBarks[villageBark].At && _prologueElapsed >= nextBarkReady)
            {
                var id = villageBarks[villageBark++].Id;
                // The next remark waits for this one - its voice clip when there is one.
                nextBarkReady = _prologueElapsed + RideBark(bridge, id) + 1.5;
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
            var shake = (1f - Mathf.Clamp(elapsed / .55f, 0f, 1f)) * .5f;
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
    private double RideBark(RuntimeBridge? bridge, string localTextId)
    {
        if (bridge is null) return 0;
        var text = bridge.ResolveText($"urman.chapter1:text/{localTextId}");
        _ = bridge.ObserveVocabularyTextAsync(text, $"urman.chapter1:text/{localTextId}");
        if (localTextId == "prologue-ride-bark-radio") StartRideRadio();
        // The forest closes in: the music thins out and dies before the trees.
        else if (localTextId == "prologue-ride-bark-field") RadioCatchesName();
        var voiced = PrologueVoice.PlayText(this, $"urman.chapter1:text/{localTextId}", bridge.TatarLanguageLevel);
        var shown = Math.Max(Math.Clamp(text.Length / 15.0, 4.5, 11), voiced + .8);
        ShowPrologueCaption("Мансур бабай: " + text, shown);
        return shown;
    }

    private static string LanguageLevelLabel(string level) => level switch
    {
        "fluent" => "свободно — бабай говорит по-татарски",
        "some" => "немного понимаю — вперемешку с русским",
        _ => "почти не знаю — по-русски, татарские слова по одному"
    };

    private void ReleaseApproachRoad()
    {
        PrologueVoice.Stop();
        StopRideRadio(1.5);
        if (_approachRoad is not null && IsInstanceValid(_approachRoad)) _approachRoad.QueueFree();
        _approachRoad = null;
    }

    private void ApplyPrologueRideLook()
    {
        _rideLook.X = Mathf.Clamp(_rideLook.X, -95f, 105f);
        _rideLook.Y = Mathf.Clamp(_rideLook.Y, -45f, 40f);
        if (_rideCamera is not null) _rideCamera.RotationDegrees = new(_rideLook.Y + _rideShake.Y, _rideLook.X + _rideShake.X, _rideShake.Y * .6f);
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
        var bridgeX = (float)AgentBAct1HeightField.RavineCentre(Act1ConnectedWorld.RavineBridgeZ);
        float GroundAt(Vector3 point) => MathF.Abs(point.Z - Act1ConnectedWorld.RavineBridgeZ) < .2f && MathF.Abs(point.X - bridgeX) <= 8f
            ? Act1ConnectedWorld.OpeningBridgeRoadHeight(point.X)
            : AgentBAct1HeightField.CollisionGround(point.X, point.Z);
        // Axle-height samples make the cabin follow a road's real grade. The
        // same terrain/deck drives placement; this never bypasses collisions.
        var heading = forward.Normalized();
        var front = GroundAt(position + heading * 1.225f);
        var rear = GroundAt(position - heading * 1.225f);
        var pitch = Mathf.Atan2(front - rear, 2.45f);
        var smoothing = progress == 0 ? 1f : 1f - MathF.Exp(-7f * (float)GetProcessDeltaTime());
        _rideNiva.Rotation = new Vector3(Mathf.LerpAngle(_rideNiva.Rotation.X, pitch, smoothing),
            Mathf.LerpAngle(_rideNiva.Rotation.Y, yaw, smoothing), 0);
        _rideNiva.GlobalPosition = new(position.X, GroundAt(position) + .02f, position.Z);
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
