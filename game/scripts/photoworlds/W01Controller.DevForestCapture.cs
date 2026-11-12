using System;
using Godot;

namespace Urman.Godot;

// Development only: URMAN_FOREST_CAPTURE=<output dir> walks the prologue trail
// by itself, saves stills along it and through the finale, then quits.
// URMAN_FOREST_SPEED (m/s, default 3) sets the pace, URMAN_FOREST_FROM (0..1)
// the starting share of the trail. Unset, nothing here runs.
public partial class W01Controller
{
    private static readonly string? DevForestCaptureDir = System.Environment.GetEnvironmentVariable("URMAN_FOREST_CAPTURE");
    internal static bool DevForestCaptureActive => !string.IsNullOrEmpty(DevForestCaptureDir);
    private float _devProgress = -1f;
    private double _devNextShot;
    private double _devClock;
    private double _devClimaxClock;
    private int _devShot;

    private string[]? _devStills;
    private int _devStill;
    private double _devStillClock;

    private void DevForestCaptureTick(FirstPersonController player, double delta)
    {
        if (!DevForestCaptureActive || MainMenuUi.AnyUndismissed(GetTree())) return;
        _devClock += delta;
        if (_devProgress < 0f)
        {
            System.IO.Directory.CreateDirectory(DevForestCaptureDir!);
            _devProgress = float.TryParse(System.Environment.GetEnvironmentVariable("URMAN_FOREST_FROM"),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var from) ? from : 1f;
            // URMAN_FOREST_STILLS="20,60:25,104": stand at those metres (optional :pitch degrees), shoot, quit.
            _devStills = System.Environment.GetEnvironmentVariable("URMAN_FOREST_STILLS")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        }
        void Place(float progress, float pitch)
        {
            var at = ToGlobal(PrologueRoutePoint(progress, 0f, .05f));
            var ahead = ToGlobal(PrologueRoutePoint(Mathf.Min(progress + 3f, PrologueRouteLength), 0f, .05f));
            player.GlobalPosition = at;
            player.Velocity = Vector3.Zero;
            var flat = new Vector3(ahead.X - at.X, 0f, ahead.Z - at.Z);
            if (flat.LengthSquared() > .01f) player.Rotation = new Vector3(0f, Mathf.Atan2(-flat.X, -flat.Z), 0f);
            if (player.GetNodeOrNull<Node3D>("Head") is { } head) head.RotationDegrees = new Vector3(pitch, 0f, 0f);
        }
        void Save(string label)
        {
            var name = $"{DevForestCaptureDir}/f{_devShot++:000}_{label}.png";
            GetViewport().GetTexture().GetImage().SavePng(name);
            GD.Print($"forest-capture: {name}");
        }
        float Number(string text) => float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        if (_devStills is { Length: > 0 } stills)
        {
            if (_devStill >= stills.Length) { GetTree().Quit(); return; }
            var parts = stills[_devStill].Split(':');
            Place(Number(parts[0]), parts.Length > 1 ? Number(parts[1]) : 0f);
            _devStillClock += delta;
            if (_devStillClock < (_devStill == 0 ? 4.0 : 1.6)) return;
            Save("p" + stills[_devStill].Replace(':', '_'));
            _devStill++;
            _devStillClock = 0;
            return;
        }
        if (!_nightmareClimax)
        {
            var speed = float.TryParse(System.Environment.GetEnvironmentVariable("URMAN_FOREST_SPEED"),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pace) ? pace : 3f;
            _devProgress = Mathf.Min(_devProgress + speed * (float)delta, PrologueRouteLength - .5f);
            Place(_devProgress, 0f);
        }
        else
        {
            _devClimaxClock += delta;
            if (_devClimaxClock > 19 && string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("URMAN_SHOT_DIR"))) { GetTree().Quit(); return; }
        }
        if (_devClock < _devNextShot) return;
        _devNextShot = _devClock + (_nightmareClimax ? .25 : float.TryParse(System.Environment.GetEnvironmentVariable("URMAN_FOREST_EVERY"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var every) ? every : 3.0);
        Save(_nightmareClimax ? "climax" : "p" + Mathf.RoundToInt(_devProgress));
    }
}
