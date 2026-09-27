using Godot;

namespace Urman.Godot;

/// <summary>
/// Internal village minimap for development: every road of the settlement
/// graph, every registered building with its street number, and the player's
/// position and heading. It exists only while user://debug-zones.enabled is
/// present (the same gate as the debug location menu), toggles with F9, and
/// never touches knowledge, saves or the player's notebook map.
/// </summary>
public partial class DebugVillageMinimap : Control
{
    private const float MetersPerPixelDefault = .42f;
    private float _metersPerPixel = MetersPerPixelDefault;
    private Node3D? _player;
    private Act1ConnectedWorld? _world;

    public static void AttachIfEnabled(Node host)
    {
        if (!MainMenuUi.DebugZonesEnabled
            && !OS.GetCmdlineArgs().Contains(DebugWorldGrid.LaunchArgument, StringComparer.Ordinal)) return;
        var layer = new CanvasLayer { Name = "DebugVillageMinimapLayer", Layer = 90 };
        host.AddChild(layer);
        layer.AddChild(new DebugVillageMinimap { Name = "DebugVillageMinimap", Visible = false });
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = AnchorRight = 1f;
        OffsetLeft = -472; OffsetRight = -12; OffsetTop = 12; OffsetBottom = 472;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.F9) { Visible = !Visible; GetViewport().SetInputAsHandled(); }
        else if (Visible && key.Keycode == Key.Equal) _metersPerPixel = Mathf.Max(.08f, _metersPerPixel * .8f);
        else if (Visible && key.Keycode == Key.Minus) _metersPerPixel = Mathf.Min(1.2f, _metersPerPixel * 1.25f);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _player ??= GetTree().GetFirstNodeInGroup("player_controller") as Node3D;
        if (_world is null || !IsInstanceValid(_world))
            _world = GetTree().Root.FindChild("Act1ConnectedWorld", true, false) as Act1ConnectedWorld;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(.06f, .08f, .1f, .82f));
        DrawRect(new Rect2(Vector2.Zero, size), new Color(.8f, .85f, .9f, .6f), false, 1.5f);
        var font = ThemeDB.FallbackFont;
        if (_player is null || _world?.AddressRegistry is not { } registry)
        {
            DrawString(font, new Vector2(10, 22), "Мини-карта: мир не загружен", fontSize: 14);
            return;
        }
        var centre = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
        Vector2 Map(double x, double z) => size / 2 + (new Vector2((float)x, (float)z) - centre) / _metersPerPixel;

        foreach (var road in registry.Graph.Roads.Values)
        {
            var width = Mathf.Clamp((float)road.Width / _metersPerPixel, 2.5f, 12f);
            var colour = road.WinterBlocked ? new Color("6b5b50") : new Color("9aa6b0");
            for (var i = 1; i < road.Points.Count; i++)
                DrawLine(Map(road.Points[i - 1].X, road.Points[i - 1].Z), Map(road.Points[i].X, road.Points[i].Z), colour, width);
        }
        var drawn = new HashSet<string>();
        foreach (var building in registry.Buildings.Values)
        {
            if (building.Footprint.Count >= 3)
            {
                var polygon = building.Footprint.Select(point => Map(point.X, point.Z)).ToArray();
                DrawColoredPolygon(polygon, building.AddressId is null ? new Color("5d6b74") : new Color("c9a86a"));
            }
            else DrawCircle(Map(building.Position.X, building.Position.Z), 3f, new Color("c9a86a"));
            if (building.AddressId is { } addressId && registry.Addresses.TryGetValue(addressId, out var record))
            {
                var at = Map(building.Position.X, building.Position.Z);
                var street = registry.Streets.TryGetValue(record.StreetId, out var s) ? s.Russian : record.StreetId;
                var label = _metersPerPixel < .2f ? $"{street} {record.HouseNumber}" : record.HouseNumber;
                if (!drawn.Add(addressId)) continue;
                DrawString(font, at + new Vector2(-7, 5), label, fontSize: 13, modulate: new Color(0, 0, 0, .85f));
                DrawString(font, at + new Vector2(-8, 4), label, fontSize: 13, modulate: Colors.White);
            }
        }
        DrawString(font, new Vector2(size.X - 28, 22), "−Z", fontSize: 12, modulate: new Color(.85f, .9f, .95f));
        DrawLine(new Vector2(size.X - 17, 28), new Vector2(size.X - 17, 44), new Color(.85f, .9f, .95f), 2f);
        var forward = -_player.GlobalBasis.Z;
        var heading = new Vector2(forward.X, forward.Z).Normalized();
        var tip = size / 2 + heading * 11f;
        var side = new Vector2(-heading.Y, heading.X) * 6f;
        DrawColoredPolygon([tip, size / 2 - heading * 5f + side, size / 2 - heading * 5f - side], new Color("ff5a4a"));
        DrawString(font, new Vector2(8, size.Y - 10),
            $"x {_player.GlobalPosition.X:0.0}  z {_player.GlobalPosition.Z:0.0}   F9 скрыть · +/− масштаб", fontSize: 12,
            modulate: new Color(.85f, .9f, .95f));
    }
}
