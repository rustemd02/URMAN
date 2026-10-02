using Godot;

namespace Urman.Godot;

// Development only: URMAN_VIEW_CAPTURE=<output dir> with
// URMAN_VIEW_POINTS="name:x,y,z>tx,ty,tz;..." saves one still per viewpoint from
// a free camera in the village and quits. Unset, it does nothing.
public partial class Act1DemoRoot
{
    // Development only: URMAN_WALK_PROBE="Building;x,y,z;yawDegrees;seconds;action" puts the real
    // player controller at a building-local point, holds move_forward and prints where it ends up.
    private async void DevWalkProbeBoot()
    {
        var spec = System.Environment.GetEnvironmentVariable("URMAN_WALK_PROBE");
        if (string.IsNullOrEmpty(spec)) return;
        for (var frame = 0; frame < 900 && !(MainMenuVisible && _main is not null && _player is not null); frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _mainMenu?.Dismiss();
        _mainMenu = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        foreach (var leg in spec.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = leg.Split(';');
            var building = _main!.FindChild(parts[0], true, false) as Node3D;
            var n = parts[1].Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var yaw = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            var seconds = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
            var action = parts.Length > 4 ? parts[4] : "move_forward";
            _player!.SetSessionTransition(false);
            _player.SetModalOpen(false);
            var start = building!.ToGlobal(new Vector3(n[0], n[1], n[2]));
            _player.ApplyZoneSpawn(start, building.RotationDegrees.Y + yaw);
            DisplayServer.WindowMoveToForeground();
            for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (_pauseMenu?.IsOpen == true) _pauseMenu.Resume();
            Input.ActionPress(action);
            var elapsed = 0d;
            while (elapsed < seconds)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                elapsed += GetPhysicsProcessDeltaTime();
            }
            Input.ActionRelease(action);
            var local = building.ToLocal(_player.GlobalPosition);
            GD.Print($"walk-probe-view: focus={DisplayServer.WindowIsFocused()} pause={_pauseMenu?.IsOpen} preset={GraphicsQuality.Preset} scale={GetViewport().Scaling3DScale} fov={GetViewport().GetCamera3D()?.Fov}");
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            GD.Print(string.Format(ci, "walk-probe: {0} start=({1},{2},{3}) yaw={4} floorY={5:0.00} startGlobal={6} -> local=({7:0.00},{8:0.00},{9:0.00}) global={10}",
                parts[0], n[0], n[1], n[2], yaw, building.GlobalPosition.Y, start, local.X, local.Y, local.Z, _player.GlobalPosition));
        }
        GetTree().Quit();
    }

    private async void DevViewCaptureBoot()
    {
        var dir = System.Environment.GetEnvironmentVariable("URMAN_VIEW_CAPTURE");
        var points = System.Environment.GetEnvironmentVariable("URMAN_VIEW_POINTS");
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(points)) return;
        System.IO.Directory.CreateDirectory(dir);
        for (var frame = 0; frame < 900 && !(MainMenuVisible && _main is not null && _player is not null); frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _mainMenu?.Dismiss();
        _mainMenu = null;
        _player!.SetModalOpen(true);
        var camera = new Camera3D { Name = "DevViewCamera", Fov = 70f, Far = GetViewport().GetCamera3D()?.Far ?? 160f };
        _main!.AddChild(camera);
        camera.MakeCurrent();
        foreach (var entry in points.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = entry[..entry.IndexOf(':')];
            var parts = entry[(entry.IndexOf(':') + 1)..].Split('>');
            static Vector3 V(string text)
            {
                var n = text.Split(',').Select(value => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                return new Vector3(n[0], n[1], n[2]);
            }
            // "Building@x,y,z" points are in that building's local space (front = +Z).
            Vector3 P(string text)
            {
                var at = text.IndexOf('@');
                if (at < 0) return V(text);
                if (text[..at] == "Ground")
                {
                    var point = V(text[(at + 1)..]);
                    point.Y += Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(point.X, point.Z);
                    return point;
                }
                var node = _main!.FindChild(text[..at], true, false) as Node3D;
                return node is null ? V(text[(at + 1)..]) : node.ToGlobal(V(text[(at + 1)..]));
            }
            var owner = parts[0].Contains('@') ? parts[0][..parts[0].IndexOf('@')] : (parts[1].Contains('@') ? parts[1][..parts[1].IndexOf('@')] : "");
            var lookText = parts[1].Contains('@') || owner.Length == 0 ? parts[1] : owner + "@" + parts[1];
            camera.GlobalPosition = P(parts[0]);
            camera.LookAt(P(lookText), Vector3.Up);
            DisplayServer.WindowMoveToForeground();
            camera.MakeCurrent();
            for (var frame = 0; frame < 150; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");
            GD.Print($"view-capture: {name} camera={GetViewport().GetCamera3D()?.Name} at={camera.GlobalPosition} drawCalls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} frameMs={GetProcessDeltaTime()*1000:0.0}");
        }
        GetTree().Quit();
    }
}
