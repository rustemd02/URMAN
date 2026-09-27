using System.Text.Json;
using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private Camera3D? _introFlyoverCamera;
    private Camera3D? _introPlayerCamera;
    private CanvasLayer? _introPlayerHud;
    private bool _introPlayerHudWasVisible;
    private Vector3[] _introPositionControls = [];
    private Vector3[] _introLookControls = [];
    private double _introFlyoverDuration;
    private double _introFlyoverElapsed;

    private void StartIntroFlyover()
    {
        _introPlayerHud = _player?.GetNodeOrNull<CanvasLayer>("Hud");
        if (_introPlayerHud is not null)
        {
            _introPlayerHudWasVisible = _introPlayerHud.Visible;
            _introPlayerHud.Visible = false;
        }
        if (_player?.Accessibility.ReducedMotion == true) return;
        var playerCamera = _player?.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (playerCamera is null) return;

        try
        {
            const string path = "res://content/act1_arrival_flyover.v1.json";
            using var document = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(path));
            var track = document.RootElement;
            var duration = track.GetProperty("durationSeconds").GetDouble();
            var positions = ReadControls(track.GetProperty("positionControls"));
            var targets = ReadControls(track.GetProperty("lookControls"));
            if (!double.IsFinite(duration) || duration <= 0)
                throw new InvalidOperationException("Arrival flyover duration must be positive.");

            _introPositionControls = positions;
            _introLookControls = targets;
            _introFlyoverDuration = duration;
            _introFlyoverElapsed = 0;
            _introPlayerCamera = playerCamera;
            var camera = new Camera3D
            {
                Name = "Act1ArrivalFlyoverCamera",
                Fov = playerCamera.Fov,
                Near = playerCamera.Near,
                Far = playerCamera.Far
            };
            _main.AddChild(camera);
            _introFlyoverCamera = camera;
            UpdateIntroFlyover(0);
            camera.MakeCurrent();
        }
        catch (Exception error)
        {
            StopIntroFlyover();
            GD.PushError($"Arrival flyover could not load its authored track: {error}");
        }
    }

    private void UpdateIntroFlyover(double delta)
    {
        var camera = _introFlyoverCamera;
        var playerCamera = _introPlayerCamera;
        if (camera is null || playerCamera is null) return;

        _introFlyoverElapsed = Math.Min(_introFlyoverElapsed + Math.Max(0, delta), _introFlyoverDuration);
        var progress = (float)(_introFlyoverElapsed / _introFlyoverDuration);
        var eased = progress * progress * (3f - 2f * progress);
        var eye = Bezier(_introPositionControls, playerCamera.GlobalPosition, eased);
        var target = Bezier(_introLookControls,
            playerCamera.GlobalPosition - playerCamera.GlobalBasis.Z * 12f, eased);
        camera.GlobalPosition = eye;
        camera.LookAt(target, Vector3.Up);
        if (_introFlyoverElapsed >= _introFlyoverDuration) StopIntroFlyover();
    }

    private void StopIntroFlyover()
    {
        if (_introPlayerCamera is { } playerCamera && GodotObject.IsInstanceValid(playerCamera))
            playerCamera.MakeCurrent();
        if (_introFlyoverCamera is { } camera && GodotObject.IsInstanceValid(camera))
            camera.QueueFree();
        if (_introPlayerHud is { } hud && GodotObject.IsInstanceValid(hud))
            hud.Visible = _introPlayerHudWasVisible;
        _introFlyoverCamera = null;
        _introPlayerCamera = null;
        _introPlayerHud = null;
    }

    private static Vector3[] ReadControls(JsonElement elements)
    {
        var points = elements.EnumerateArray().Select(point =>
        {
            var values = point.EnumerateArray().Select(value => value.GetSingle()).ToArray();
            if (values.Length != 3 || values.Any(value => !float.IsFinite(value)))
                throw new InvalidOperationException("Arrival flyover controls need finite 3D points.");
            return new Vector3(values[0], values[1], values[2]);
        }).ToArray();
        if (points.Length != 3)
            throw new InvalidOperationException("Arrival flyover needs three authored controls per curve.");
        return points;
    }

    private static Vector3 Bezier(Vector3[] controls, Vector3 destination, float t)
    {
        var inverse = 1f - t;
        return controls[0] * (inverse * inverse * inverse)
            + controls[1] * (3f * inverse * inverse * t)
            + controls[2] * (3f * inverse * t * t)
            + destination * (t * t * t);
    }
}
