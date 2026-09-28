using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // N2.2 prologue (beat sheet P3-P4): after the forest teaser cuts to
    // black, Aidar rides the last stretch to Kara-Urman on the Niva's front
    // passenger seat. The car follows the entry road to the stop where the
    // world's parked Niva stands; during the ride Mansur's teasing talk
    // calibrates the Tatar profile for this new session (L1): household
    // answers seed guessed vocabulary through ordinary kernel effects -
    // no story flags, no confirmed words, and a skipped ride leaves the
    // manual setting/none untouched.
    private Node3D? _rideNiva;
    private Camera3D? _rideCamera;
    private CanvasLayer? _playerHudForRide;
    private bool _playerHudWasVisibleForRide;

    private const string PrologueRideDialogue = "urman.chapter1:dialogue/prologue-niva-language";

    private static readonly Vector3[] PrologueRidePath =
    {
        new(0f, 0f, 46f), new(0f, 0f, 30f), new(0f, 0f, 18f),
        new(-.6f, 0f, 12f), new(-1.6f, 0f, 9.6f), new(-1.65f, 0f, 9.2f)
    };

    private async Task RunPrologueNivaRideAsync()
    {
        if (_main is null || _player is null)
        {
            return;
        }

        _main.SwitchZone("village_day", "arrival");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

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
            Position = new(.42f, 1.22f, .18f),
            Fov = _player.GetNodeOrNull<Camera3D>("Head/Camera3D")?.Fov ?? 75f
        };
        _rideNiva.AddChild(_rideCamera);

        PlaceRideAt(0);
        _rideCamera.MakeCurrent();

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var calibrationOpened = false;
        var elapsed = 0d;
        const double rideSeconds = 21d;
        const double calibrationAt = 6.5d;
        while (elapsed < rideSeconds)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_prologueSkipRequested) break;
            elapsed += GetProcessDeltaTime();
            PlaceRideAt(MathF.Min((float)(elapsed / rideSeconds), 1f));
            if (!calibrationOpened && elapsed >= calibrationAt && bridge is not null)
            {
                calibrationOpened = true;
                bridge.OpenDialogueUi(PrologueRideDialogue);
            }
        }

        FadePrologueBlackout(visible: true);
        await PrologueRideFrames(24);
        ReleasePrologueRide();
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
        var yaw = Mathf.RadToDeg(Mathf.Atan2(-forward.X, -forward.Z));
        var previous = _rideNiva.RotationDegrees with { X = 0, Z = 0 };
        _rideNiva.RotationDegrees = previous.Lerp(new Vector3(0, yaw, 0), .12f);
        _rideNiva.GlobalPosition = new(position.X,
            AgentBAct1HeightField.CollisionGround(position.X, position.Z) + .02f,
            position.Z);
    }

    private async Task PrologueRideFrames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            if (_prologueSkipRequested) return;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void ReleasePrologueRide()
    {
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
        _rideNiva = null;
        _rideCamera = null;
        _playerHudForRide = null;
    }
}
