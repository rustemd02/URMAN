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

    private static readonly Vector3[] PrologueRidePath =
    {
        new(69f, 0, 8f), new(69f, 0, -18f), new(68f, 0, -21f), new(63f, 0, -24.4f),
        new(58f, 0, -25f), new(41.5f, 0, -25f), new(38f, 0, -23.8f), new(32f, 0, -24.2f),
        new(28f, 0, -26.2f), new(23f, 0, -25f), new(17f, 0, -21.5f), new(10f, 0, -17f),
        new(4.5f, 0, -12.5f), new(0f, 0, -10f), new(-.6f, 0, -1.5f), new(-1.65f, 0, 1f)
    };

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

        PlaceRideAt(0);
        _rideCamera.MakeCurrent();
        if (_prologueCaption is not null) _prologueCaption.Visible = false;
        FadePrologueBlackout(visible: false);

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var calibrationOpened = false;
        var elapsed = 0d;
        const double rideSeconds = 28d;
        const double calibrationAt = 12d;
        var dialogueUi = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        while (IsInsideTree() && (elapsed < rideSeconds || dialogueUi?.IsOpen == true))
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_prologueSkipRequested) break;
            elapsed += GetProcessDeltaTime();
            if (dialogueUi?.IsOpen != true)
            {
                var look = Input.GetVector("look_left", "look_right", "look_up", "look_down");
                _rideLook -= look * (float)GetProcessDeltaTime() * 105f;
                ApplyPrologueRideLook();
            }
            var previousPosition = _rideNiva.GlobalPosition;
            PlaceRideAt(MathF.Min((float)(elapsed / rideSeconds), 1f));
            var wheelTurn = previousPosition.DistanceTo(_rideNiva.GlobalPosition) / .345f;
            foreach (var wheel in visual.Wheels) wheel.RotateX(-wheelTurn);
            if (!calibrationOpened && elapsed >= calibrationAt && bridge is not null)
            {
                calibrationOpened = true;
                bridge.OpenDialogueUi(PrologueRideDialogue);
            }
        }

        FadePrologueBlackout(visible: true);
        await PrologueWaitAsync(.3);
        ReleasePrologueRide();
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
    private void PlaceRideAt(float progress)
    {
        if (_rideNiva is null) return;
        var eased = progress * progress * (3f - 2f * progress);
        var total = 0f;
        for (var index = 1; index < PrologueRidePath.Length; index++)
        {
            total += PrologueRidePath[index - 1].DistanceTo(PrologueRidePath[index]);
        }
        var target = eased * total;
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

    private void ReleasePrologueRide()
    {
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
