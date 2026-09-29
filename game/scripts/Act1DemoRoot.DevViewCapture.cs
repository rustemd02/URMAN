using Godot;

namespace Urman.Godot;

// Development only: URMAN_VIEW_CAPTURE=<output dir> with
// URMAN_VIEW_POINTS="name:x,y,z>tx,ty,tz;..." saves one still per viewpoint from
// a free camera in the village and quits. Unset, it does nothing.
public partial class Act1DemoRoot
{
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
        var camera = new Camera3D { Name = "DevViewCamera", Fov = 70f, Far = 900f };
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
            camera.GlobalPosition = V(parts[0]);
            camera.LookAt(V(parts[1]), Vector3.Up);
            for (var frame = 0; frame < 150; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");
            GD.Print($"view-capture: {name}");
        }
        GetTree().Quit();
    }
}
