using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>Opt-in, presentation-only coordinates for the author's walking QA.</summary>
public partial class DebugWorldGrid : Node3D
{
    public const string LaunchArgument = "--urman-qa-grid";
    public const float CellSize = 8f;
    private const int Radius = 2;
    private const string ColumnLetters = "АБВГДЕЖЗИКЛМНОПРСТУФХЦЧШЩЭЮЯ";
    private static readonly int ColumnCount = Mathf.CeilToInt(
        (AgentBAct1HeightField.MaxX - AgentBAct1HeightField.MinX) / CellSize);
    private static readonly int RowCount = Mathf.CeilToInt(
        (AgentBAct1HeightField.MaxZ - AgentBAct1HeightField.MinZ) / CellSize);

    private readonly Label3D[] _cellLabels = new Label3D[(Radius * 2 + 1) * (Radius * 2 + 1)];
    private Node3D _ground = null!;
    private MeshInstance3D _lines = null!;
    private CanvasLayer _hudLayer = null!;
    private PanelContainer _hudPlate = null!;
    private DebugVillageMinimap? _minimap;
    private Label _hud = null!;
    private Label _targetHud = null!;
    private FirstPersonController? _player;
    private bool _requested;
    private int _column = int.MinValue;
    private int _row = int.MinValue;

    public static void AttachIfEnabled(Node host)
    {
        var launchEnabled = OS.GetCmdlineArgs().Contains(LaunchArgument, StringComparer.Ordinal);
        if (!launchEnabled && !MainMenuUi.DebugZonesEnabled) return;
        host.AddChild(new DebugWorldGrid { Name = "DebugWorldGrid", _requested = launchEnabled });
    }

    public override void _Ready()
    {
        _ground = new Node3D { Name = "GroundCoordinates" };
        AddChild(_ground);
        _lines = new MeshInstance3D { Name = "CellBorders", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _ground.AddChild(_lines);
        for (var index = 0; index < _cellLabels.Length; index++)
        {
            var label = new Label3D
            {
                Name = $"Cell_{index}", Font = UrmanUiTheme.DisplayFont,
                FontSize = 64, PixelSize = .008f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color("ffd47c"), OutlineModulate = new Color("17282a"), OutlineSize = 9,
                Shaded = false, NoDepthTest = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            _ground.AddChild(label);
            _cellLabels[index] = label;
        }

        _hudLayer = new CanvasLayer { Name = "GridCoordinateHud", Layer = 80 };
        AddChild(_hudLayer);
        var plateStyle = new StyleBoxFlat
        {
            BgColor = new Color("11252ce6"), BorderColor = new Color("72c9dc")
        };
        plateStyle.SetBorderWidthAll(2);
        plateStyle.SetContentMarginAll(12);
        _hudPlate = new PanelContainer
        {
            Name = "CoordinatePlate", MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 1f, AnchorRight = 1f,
            OffsetLeft = -396f, OffsetRight = -24f, OffsetTop = 76f, OffsetBottom = 166f
        };
        _hudPlate.AddThemeStyleboxOverride("panel", plateStyle);
        _hudLayer.AddChild(_hudPlate);
        var stack = new VBoxContainer();
        _hudPlate.AddChild(stack);
        _hud = new Label
        {
            Name = "CurrentCell", MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right,
            Theme = UrmanUiTheme.For(false)
        };
        _hud.AddThemeFontSizeOverride("font_size", 28);
        _hud.AddThemeColorOverride("font_color", new Color("fff0c8"));
        stack.AddChild(_hud);
        _targetHud = new Label { Name = "MarkedCell", HorizontalAlignment = HorizontalAlignment.Right,
            Theme = UrmanUiTheme.For(false), Text = "F6 кадр  ·  F7 объект  ·  F8 сетка" };
        _targetHud.AddThemeFontSizeOverride("font_size", 18);
        _targetHud.AddThemeColorOverride("font_color", new Color("b9e5e4"));
        stack.AddChild(_targetHud);
        _minimap = GetParent().FindChild("DebugVillageMinimap", true, false) as DebugVillageMinimap;
        ShowOverlay(false);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.F8) _requested = !_requested;
        else if (key.Keycode == Key.F7 && _requested) MarkLookedAtCell();
        else if (key.Keycode == Key.F6 && _requested) CaptureQaFrame();
        else return;
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        var show = _requested && GetParent() is Act1DemoRoot { MainMenuVisible: false, IntroVisible: false };
        ShowOverlay(show);
        if (!show) return;
        var plateTop = _minimap?.Visible == true ? 484f : 76f;
        _hudPlate.OffsetTop = plateTop;
        _hudPlate.OffsetBottom = plateTop + 90f;
        _player ??= GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (_player is null) return;
        var at = _player.GlobalPosition;
        var column = Mathf.Clamp(Mathf.FloorToInt((at.X - AgentBAct1HeightField.MinX) / CellSize), 0, ColumnCount - 1);
        var row = Mathf.Clamp(Mathf.FloorToInt((AgentBAct1HeightField.MaxZ - at.Z) / CellSize), 0, RowCount - 1);
        _hud.Text = $"КЛЕТКА {CellName(column, row)}   x {at.X:0.0}  z {at.Z:0.0}";
        if (column == _column && row == _row) return;
        _column = column;
        _row = row;
        RebuildWindow();
    }

    private void ShowOverlay(bool visible)
    {
        _ground.Visible = visible;
        _hudLayer.Visible = visible;
    }

    private static string CellName(int column, int row) => $"{ColumnLetters[column]}-{row + 1}";

    private void MarkLookedAtCell()
    {
        _player ??= GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var camera = GetViewport().GetCamera3D();
        if (_player is null || camera is null) return;
        var gridExclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
        using var gridExcludeOwner = (global::Godot.Collections.Array)gridExclude;
        using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition,
            camera.GlobalPosition - camera.GlobalBasis.Z * 80f, 7u, gridExclude);
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (hit.Count == 0)
        {
            _targetHud.Text = "Цель не найдена   ·   F7 повторить";
            return;
        }
        var point = hit["position"].AsVector3();
        var column = Mathf.Clamp(Mathf.FloorToInt((point.X - AgentBAct1HeightField.MinX) / CellSize), 0, ColumnCount - 1);
        var row = Mathf.Clamp(Mathf.FloorToInt((AgentBAct1HeightField.MaxZ - point.Z) / CellSize), 0, RowCount - 1);
        _targetHud.Text = $"ОТМЕЧЕНО {CellName(column, row)}   ·   F6 кадр  ·  F7 новая цель";
    }

    private void CaptureQaFrame()
    {
        if (_column < 0 || _row < 0) return;
        try
        {
            var directory = OS.GetEnvironment("URMAN_QA_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(directory))
                directory = ProjectSettings.GlobalizePath("user://qa-captures");
            if (!Path.IsPathFullyQualified(directory))
                throw new InvalidOperationException("URMAN_QA_CAPTURE_DIR must be absolute.");
            Directory.CreateDirectory(directory);
            var name = $"urman_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{CellName(_column, _row)}.png";
            var path = Path.Combine(directory, name);
            using var frame = GetViewport().GetTexture().GetImage();
            if (frame.IsEmpty() || frame.SavePng(path) != Error.Ok)
                throw new IOException("Could not save the QA viewport frame.");
            GD.Print("qa-grid-capture: " + path);
        }
        catch (Exception error)
        {
            GD.PushWarning("QA frame was not saved: " + error.Message);
        }
    }

    private void RebuildWindow()
    {
        var index = 0;
        for (var row = _row - Radius; row <= _row + Radius; row++)
        for (var column = _column - Radius; column <= _column + Radius; column++)
        {
            var label = _cellLabels[index++];
            label.Visible = column >= 0 && column < ColumnCount && row >= 0 && row < RowCount;
            if (!label.Visible) continue;
            var x = AgentBAct1HeightField.MinX + (column + .5f) * CellSize;
            var z = AgentBAct1HeightField.MaxZ - (row + .5f) * CellSize;
            label.Text = CellName(column, row);
            label.GlobalPosition = new(x, AgentBAct1HeightField.CollisionGround(x, z) + .62f, z);
        }

        var mesh = new ImmediateMesh();
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(.06f, .62f, .85f, .52f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
        var firstColumn = Mathf.Max(0, _column - Radius);
        var lastColumn = Mathf.Min(ColumnCount, _column + Radius + 1);
        var firstRow = Mathf.Max(0, _row - Radius);
        var lastRow = Mathf.Min(RowCount, _row + Radius + 1);
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            var x = Mathf.Min(AgentBAct1HeightField.MaxX,
                AgentBAct1HeightField.MinX + column * CellSize);
            for (var row = firstRow; row < lastRow; row++)
                DrawBorder(mesh, new(x, AgentBAct1HeightField.MaxZ - row * CellSize),
                    new(x, AgentBAct1HeightField.MaxZ - (row + 1) * CellSize));
        }
        for (var row = firstRow; row <= lastRow; row++)
        {
            var z = Mathf.Max(AgentBAct1HeightField.MinZ,
                AgentBAct1HeightField.MaxZ - row * CellSize);
            for (var column = firstColumn; column < lastColumn; column++)
                DrawBorder(mesh, new(AgentBAct1HeightField.MinX + column * CellSize, z),
                    new(Mathf.Min(AgentBAct1HeightField.MaxX,
                        AgentBAct1HeightField.MinX + (column + 1) * CellSize), z));
        }
        mesh.SurfaceEnd();
        _lines.Mesh = mesh;
    }

    private static void DrawBorder(ImmediateMesh mesh, Vector2 from, Vector2 to)
    {
        const int steps = 8;
        var side = new Vector2(to.Y - from.Y, from.X - to.X).Normalized() * .035f;
        for (var step = 0; step < steps; step++)
        {
            var a = from.Lerp(to, step / (float)steps);
            var b = from.Lerp(to, (step + 1) / (float)steps);
            var aLeft = GroundPoint(a - side);
            var aRight = GroundPoint(a + side);
            var bLeft = GroundPoint(b - side);
            var bRight = GroundPoint(b + side);
            mesh.SurfaceAddVertex(aLeft);
            mesh.SurfaceAddVertex(bLeft);
            mesh.SurfaceAddVertex(bRight);
            mesh.SurfaceAddVertex(aLeft);
            mesh.SurfaceAddVertex(bRight);
            mesh.SurfaceAddVertex(aRight);
        }
    }

    private static Vector3 GroundPoint(Vector2 at) =>
        new(at.X, AgentBAct1HeightField.CollisionGround(at.X, at.Y) + .075f, at.Y);
}
