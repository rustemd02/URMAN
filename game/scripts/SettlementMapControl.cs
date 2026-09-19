using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>Notebook sketch of physically located addresses. Geometry and the
/// knowledge filter belong to SettlementRegistry; no player or quest route is drawn.</summary>
public partial class SettlementMapControl : Control, IAccessibilitySettingsTarget
{
    private SettlementRegistry? _registry;
    private SettlementMap? _map;
    private string[] _located = [];
    private float _textScale = 1;
    private float _screenScale = 1;
    private bool _highContrast;
    private VBoxContainer _layout = null!;
    private HBoxContainer _toolbar = null!;
    private Label _heading = null!, _hint = null!, _empty = null!, _legend = null!;
    private Control _canvas = null!;
    private Button _zoomIn = null!, _zoomOut = null!, _fit = null!;
    private Rect2 _fitBounds = new(-10, -10, 20, 20);
    private Vector2 _center;
    private float _zoom = 1;
    private bool _dragging;
    private readonly List<(string AddressId, Rect2 Bounds)> _drawnLabels = [];
    private readonly HashSet<string> _drawnRoads = new(StringComparer.Ordinal);
    private readonly HashSet<string> _drawnStreetLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _drawnStreetAnchors = new(StringComparer.Ordinal);
    public int DisplayedAddressCount { get; private set; }
    public bool EmptyPageVisible => DisplayedAddressCount == 0;
    internal float ViewZoom => _zoom;
    internal Vector2 ViewCenter => _center;
    internal IReadOnlyList<(string AddressId, Rect2 Bounds)> DrawnAddressLabels => _drawnLabels;
    internal IReadOnlyCollection<string> DrawnRoadIds => _drawnRoads;
    internal IReadOnlyCollection<string> DrawnStreetIds => _drawnStreetLabels;
    internal IReadOnlyDictionary<string, Vector2> DrawnStreetAnchors => _drawnStreetAnchors;
    internal int DrawingFontSize => MapFontSize(14);

    public override void _Ready()
    {
        AddToGroup(AccessibilityPresentation.TargetGroup);
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new(280, 260);
        _layout = new VBoxContainer { Name = "MapLayout", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_layout);
        _heading = new Label { Text = "Кара-Урман — схема улиц" };
        _layout.AddChild(_heading);
        _empty = new Label { Text = "Пока ни один дом не отмечен.\n\nПрочитай адресную табличку у найденного дома — его номер появится на этой странице.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = SizeFlags.ExpandFill };
        _layout.AddChild(_empty);
        _toolbar = new HBoxContainer { Name = "Tools" };
        _layout.AddChild(_toolbar);
        Button Tool(string name, string label, string tip, Action action)
        {
            var button = new Button { Name = name, Text = label, TooltipText = tip, FocusMode = FocusModeEnum.All };
            button.Pressed += action;
            _toolbar.AddChild(button);
            return button;
        }
        _zoomOut = Tool("ZoomOut", "−", "Уменьшить масштаб", () => ZoomAt(1 / 1.35f, _canvas.Size * .5f));
        _zoomIn = Tool("ZoomIn", "+", "Увеличить масштаб", () => ZoomAt(1.35f, _canvas.Size * .5f));
        _fit = Tool("Fit", "Все найденные", "Показать найденные дома (Home)", ResetView);
        _hint = new Label { Text = "Колесо — масштаб · перетащить — сдвиг",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _toolbar.AddChild(_hint);
        _canvas = new Control { Name = "Sketch", ClipContents = true, FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.Drag, CustomMinimumSize = new(0, 100),
            SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Stop,
            TooltipText = "Стрелки — сдвиг, + / − — масштаб, Home — все найденные дома" };
        _canvas.Draw += DrawSketch;
        _canvas.GuiInput += HandleMapInput;
        _canvas.Resized += () => _canvas.QueueRedraw();
        _canvas.VisibilityChanged += () => { _dragging = false; _canvas.QueueRedraw(); };
        _layout.AddChild(_canvas);
        _legend = new Label { Text = "Контуры — найденные дома · кружок — вход или ворота",
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _layout.AddChild(_legend);
        Resized += ResizeLayout;
        ResizeLayout();
        ApplyAccessibilitySettings((GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.Accessibility ?? AccessibilitySettingsSnapshot.Default);
        RefreshControls();
    }

    public void Bind(SettlementRegistry registry, IEnumerable<string> locatedAddressIds)
    {
        var located = locatedAddressIds.Select(registry.CanonicalAddressId).Where(id => id is not null)
            .Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var changed = !ReferenceEquals(_registry, registry) || !_located.SequenceEqual(located);
        _registry = registry;
        _located = located;
        _map = registry.MapForKnownAddresses(located);
        DisplayedAddressCount = located.Length;
        if (changed) { _fitBounds = FitKnownArea(_map); _center = _fitBounds.GetCenter(); _zoom = 1; _dragging = false; }
        RefreshControls();
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _textScale = Mathf.Clamp((float)settings.TextScale, .8f, 1.6f);
        _highContrast = settings.HighContrast;
        SetMeta("accessibilityTextScale", _textScale);
        SetMeta("accessibilityHighContrast", _highContrast);
        if (_layout is not null)
        {
            var ink = Ink();
            foreach (var label in new[] { _heading, _hint, _empty, _legend })
            {
                label.AddThemeColorOverride("font_color", ink);
                // The journal's dark theme applies offset black shadows to all
                // descendants. On this paper those shadows double the dark ink.
                label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
                label.AddThemeConstantOverride("shadow_offset_x", 0);
                label.AddThemeConstantOverride("shadow_offset_y", 0);
                label.AddThemeConstantOverride("outline_size", 0);
            }
            foreach (var button in new[] { _zoomIn, _zoomOut, _fit })
            {
                button.AddThemeColorOverride("font_color", ink);
                button.AddThemeColorOverride("font_hover_color", ink);
                button.AddThemeColorOverride("font_pressed_color", ink);
                button.AddThemeColorOverride("font_focus_color", ink);
                button.AddThemeConstantOverride("outline_size", 0);
                foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
                {
                    var box = new StyleBoxFlat { BgColor = state == "pressed" ? Paper().Darkened(.12f) : Paper(),
                        BorderColor = ink, BorderWidthLeft = 1, BorderWidthRight = 1,
                        BorderWidthTop = 1, BorderWidthBottom = 1, ContentMarginLeft = 9, ContentMarginRight = 9 };
                    if (state == "focus") { box.DrawCenter = false; box.BorderWidthLeft = box.BorderWidthRight = box.BorderWidthTop = box.BorderWidthBottom = 3; }
                    button.AddThemeStyleboxOverride(state, box);
                }
            }
        }
        RefreshScreenTypography(force: true);
        QueueRedraw();
        _canvas?.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        // Window stretch and parent presentation transforms can change without
        // a new accessibility setting. Measure the rendered scale, not DPI or
        // a guessed 720p ratio; only update font/layout values when it changes.
        if (IsVisibleInTree()) RefreshScreenTypography();
    }

    private int MapFontSize(int baseline) => Mathf.CeilToInt(baseline * _textScale / Math.Min(1f, _screenScale));

    private void RefreshScreenTypography(bool force = false)
    {
        if (_layout is null) return;
        // CanvasItem.GetScreenTransform uses popup_base_transform, which is
        // identity when the root embeds subwindows. Include the actual root
        // viewport stretch explicitly, as the normal pointer input path does.
        var transform = GetViewport().GetScreenTransform() * GetGlobalTransformWithCanvas();
        var scale = Math.Max(.01f, Math.Min(transform.X.Length(), transform.Y.Length()));
        if (!force && Math.Abs(scale - _screenScale) < .0001f) return;
        _screenScale = scale;
        foreach (var label in new[] { _heading, _hint, _empty, _legend })
            label.AddThemeFontSizeOverride("font_size", MapFontSize(label == _heading ? 18 : 14));
        foreach (var button in new[] { _zoomIn, _zoomOut, _fit })
        {
            button.AddThemeFontSizeOverride("font_size", MapFontSize(15));
            button.CustomMinimumSize = new Vector2(36, 30) * (_textScale / Math.Min(1f, _screenScale));
        }
        _canvas.QueueRedraw();
    }

    private void ResizeLayout()
    {
        _layout.Position = new(12, 10);
        _layout.Size = new(Math.Max(0, Size.X - 24), Math.Max(0, Size.Y - 20));
        QueueRedraw();
    }
    private void RefreshControls()
    {
        if (_layout is null) return;
        _empty.Visible = EmptyPageVisible;
        _toolbar.Visible = _canvas.Visible = _legend.Visible = !EmptyPageVisible;
        _zoomOut.Disabled = _zoom <= .6501f;
        _zoomIn.Disabled = _zoom >= 5.999f;
        QueueRedraw();
        _canvas.QueueRedraw();
    }
    private Color Ink() => _highContrast ? Colors.Black : Color.FromHtml("51422d");
    private Color Paper() => _highContrast ? Color.FromHtml("fff7dc") : Color.FromHtml("e5d9bc");
    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), Paper());
    private static Vector2 Xz(SettlementPoint p) => new((float)p.X, (float)p.Z);
    private bool IsPrimary(SettlementBuilding b) => b.AddressId is not null && _registry!.Addresses[b.AddressId].BuildingId == b.BuildingId;

    private Rect2 FitKnownArea(SettlementMap map)
    {
        var points = map.Buildings.SelectMany(b => b.Footprint.Count > 0 ? b.Footprint : new[] { b.Position })
            .Concat(map.AccessPoints.Select(a => a.Position)).Select(Xz).ToList();
        var nodes = map.Nodes.ToDictionary(n => n.Id);
        // Include the nearest real road as context, without drawing a new link
        // between the road and the building or consulting a computed route.
        foreach (var building in map.Buildings.Where(IsPrimary))
        {
            var p = Xz(building.Position);
            var distance = float.PositiveInfinity;
            var nearest = p;
            foreach (var edge in map.Edges)
            {
                if (!nodes.TryGetValue(edge.A, out var a) || !nodes.TryGetValue(edge.B, out var b)) continue;
                var start = Xz(a.Position); var delta = Xz(b.Position) - start;
                var candidate = start + delta * Mathf.Clamp(delta.LengthSquared() > .000001f ? (p - start).Dot(delta) / delta.LengthSquared() : 0, 0, 1);
                if (p.DistanceSquaredTo(candidate) < distance) { distance = p.DistanceSquaredTo(candidate); nearest = candidate; }
            }
            points.Add(nearest);
        }
        if (points.Count == 0) return new(-10, -10, 20, 20);
        var min = new Vector2(points.Min(p => p.X), points.Min(p => p.Y));
        var max = new Vector2(points.Max(p => p.X), points.Max(p => p.Y));
        return new Rect2(min, max - min).Grow(12);
    }
    private float PixelsPerMetre => Math.Max(.001f, Math.Min(_canvas.Size.X / Math.Max(1, _fitBounds.Size.X), _canvas.Size.Y / Math.Max(1, _fitBounds.Size.Y)) * _zoom);
    private Vector2 Screen(SettlementPoint p) => (Xz(p) - _center) * PixelsPerMetre + _canvas.Size * .5f;
    private void ResetView() { _center = _fitBounds.GetCenter(); _zoom = 1; RefreshControls(); }
    private void ZoomAt(float factor, Vector2 anchor)
    {
        var before = (anchor - _canvas.Size * .5f) / PixelsPerMetre + _center;
        _zoom = Mathf.Clamp(_zoom * factor, .65f, 6);
        _center = before - (anchor - _canvas.Size * .5f) / PixelsPerMetre;
        RefreshControls();
    }
    private void HandleMapInput(InputEvent input)
    {
        if (input is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Left) { _dragging = mouse.Pressed; if (mouse.Pressed) _canvas.GrabFocus(); }
            else if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelUp) ZoomAt(1.2f, mouse.Position);
            else if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelDown) ZoomAt(1 / 1.2f, mouse.Position);
            else return;
            _canvas.AcceptEvent();
        }
        else if (input is InputEventMouseMotion motion && _dragging)
        {
            _center -= motion.Relative / PixelsPerMetre;
            _canvas.QueueRedraw(); _canvas.AcceptEvent();
        }
        else if (input is InputEventKey { Pressed: true } key)
        {
            var pan = key.Keycode switch { Key.Left => Vector2.Left, Key.Right => Vector2.Right, Key.Up => Vector2.Up, Key.Down => Vector2.Down, _ => Vector2.Zero };
            if (pan != Vector2.Zero) { _center += pan * (45 * _textScale / PixelsPerMetre); _canvas.QueueRedraw(); }
            else if (key.Keycode == Key.Home) ResetView();
            else if (key.Keycode is Key.Plus or Key.Equal or Key.KpAdd) ZoomAt(1.2f, _canvas.Size * .5f);
            else if (key.Keycode is Key.Minus or Key.KpSubtract) ZoomAt(1 / 1.2f, _canvas.Size * .5f);
            else return;
            _canvas.AcceptEvent();
        }
    }

    private void DrawSketch()
    {
        _drawnLabels.Clear(); _drawnRoads.Clear(); _drawnStreetLabels.Clear(); _drawnStreetAnchors.Clear();
        if (_map is not { } map || _registry is null) return;
        var ink = Ink(); var paper = Paper(); var font = ThemeDB.FallbackFont;
        var text = DrawingFontSize;
        var viewport = new Rect2(Vector2.Zero, _canvas.Size);
        _canvas.DrawRect(viewport, ink.Lightened(.25f), false, 1);
        var nodes = map.Nodes.ToDictionary(n => n.Id);
        foreach (var edge in map.Edges)
        {
            if (!nodes.TryGetValue(edge.A, out var a) || !nodes.TryGetValue(edge.B, out var b)) continue;
            var p = Screen(a.Position); var q = Screen(b.Position);
            if (!ClipSegment(viewport.Grow(-1), ref p, ref q)) continue;
            _drawnRoads.Add(edge.RoadId);
            var width = edge.Modes.HasFlag(SettlementTravelMode.Car) ? Mathf.Clamp((float)edge.Width * PixelsPerMetre * .34f, 3, 12) : 2;
            _canvas.DrawLine(p, q, _highContrast ? Color.FromHtml("77736a") : Color.FromHtml("ada085"), width, true);
            _canvas.DrawLine(p, q, ink.Lightened(.32f), 1, true);
        }
        foreach (var building in map.Buildings)
        {
            var polygon = building.Footprint.Select(Screen).ToArray();
            if (polygon.Length >= 3)
            {
                _canvas.DrawColoredPolygon(polygon, IsPrimary(building) ? ink.Lightened(.55f) : ink.Lightened(.75f));
                _canvas.DrawPolyline(polygon.Append(polygon[0]).ToArray(), ink, IsPrimary(building) ? 2 : 1, true);
            }
            else _canvas.DrawCircle(Screen(building.Position), 4, ink);
        }
        foreach (var access in map.AccessPoints)
        {
            var p = Screen(access.Position);
            _canvas.DrawCircle(p, 5 * _textScale, paper);
            _canvas.DrawArc(p, 5 * _textScale, 0, Mathf.Tau, 20, ink, 2, true);
        }
        var occupied = new List<Rect2> { new(_canvas.Size.X - 38 * _textScale, 0, 38 * _textScale, 52 * _textScale),
            new(0, _canvas.Size.Y - 32 * _textScale, 125 * _textScale, 32 * _textScale) };
        foreach (var building in map.Buildings.Where(IsPrimary))
        {
            var p = Screen(building.Position);
            if (!viewport.Grow(-2).HasPoint(p)) continue;
            var address = _registry.Addresses[building.AddressId!];
            var street = map.KnownStreets.Single(s => s.Id == address.StreetId);
            var role = building.Role switch { "school" => "Школа", "shop" => "Магазин", "council" => "Сельсовет / ДК", "mosque" => "Мечеть", "clinic" => "ФАП", _ => "Дом" };
            var lines = new[] { role + " · " + address.HouseNumber, street.Tatar, street.Russian };
            var box = PlaceLabel(p, lines, occupied, text);
            if (box is null) continue;
            DrawLabel(lines, box.Value, text, ink, paper);
            occupied.Add(box.Value.Grow(3)); _drawnLabels.Add((address.AddressId, box.Value));
        }
        // Names come only from the registry's earned-name projection, never
        // from a context edge's internal StreetId or the global street catalog.
        foreach (var street in map.KnownStreets)
        {
            var anchor = StreetLabelAnchor(map, street.Id);
            if (anchor is null) continue;
            var point = (anchor.Value - _center) * PixelsPerMetre + _canvas.Size * .5f;
            if (!viewport.Grow(-2).HasPoint(point)) continue;
            var lines = new[] { street.Tatar + " / " + street.Russian };
            var box = PlaceLabel(point, lines, occupied, text);
            if (box is null) continue;
            DrawLabel(lines, box.Value, text, ink, paper); occupied.Add(box.Value.Grow(3)); _drawnStreetLabels.Add(street.Id);
            _drawnStreetAnchors[street.Id] = anchor.Value;
        }
        var north = new Vector2(_canvas.Size.X - 22 * _textScale, 25 * _textScale);
        _canvas.DrawLine(north + Vector2.Down * 15 * _textScale, north, ink, 2, true);
        _canvas.DrawLine(north, north + new Vector2(-4, 6) * _textScale, ink, 2, true);
        _canvas.DrawLine(north, north + new Vector2(4, 6) * _textScale, ink, 2, true);
        _canvas.DrawString(font, north + new Vector2(-5 * _textScale, -5 * _textScale), "С", fontSize: text, modulate: ink);
        var metres = new[] { 1f, 2f, 5f, 10f, 20f, 50f }.LastOrDefault(m => m * PixelsPerMetre <= 90 * _textScale);
        if (metres <= 0) metres = 1;
        var start = new Vector2(10, _canvas.Size.Y - 9); var end = start + Vector2.Right * metres * PixelsPerMetre;
        _canvas.DrawLine(start, end, ink, 2);
        _canvas.DrawLine(start + Vector2.Up * 4, start + Vector2.Down * 2, ink, 2);
        _canvas.DrawLine(end + Vector2.Up * 4, end + Vector2.Down * 2, ink, 2);
        _canvas.DrawString(font, start + Vector2.Up * 7, metres.ToString(System.Globalization.CultureInfo.InvariantCulture) + " м", fontSize: text, modulate: ink);
    }

    private Vector2? StreetLabelAnchor(SettlementMap map, string streetId)
    {
        // Access validation splits graph edges. The imported road polyline is
        // the stable geometry behind those edges, so its label must not depend
        // on which split happens to be the longest after loading a save.
        var known = map.Buildings.Where(b => IsPrimary(b) && _registry!.Addresses[b.AddressId!].StreetId == streetId)
            .OrderBy(b => b.BuildingId, StringComparer.Ordinal).Select(b => Xz(b.Position)).ToArray();
        if (known.Length == 0) return null;
        var reference = known.Aggregate(Vector2.Zero, (sum, point) => sum + point) / known.Length;
        var projectedRoadIds = map.Edges.Select(edge => edge.RoadId).ToHashSet(StringComparer.Ordinal);
        var candidates = new List<(double Distance, string RoadId, Vector2 Point)>();
        foreach (var road in _registry!.Graph.Roads.Values.Where(road => road.StreetId == streetId
            && projectedRoadIds.Contains(road.Id) && !road.Id.StartsWith("access/", StringComparison.Ordinal)
            && !road.Id.StartsWith("route/", StringComparison.Ordinal)))
        for (var i = 1; i < road.Points.Count; i++)
        {
            var a = road.Points[i - 1]; var b = road.Points[i];
            // Canonical endpoint order also keeps arithmetic independent of a
            // reversed input polyline, without changing the street direction.
            if (a.X > b.X || a.X == b.X && a.Z > b.Z) (a, b) = (b, a);
            var dx = b.X - a.X; var dz = b.Z - a.Z; var squared = dx * dx + dz * dz;
            if (squared < .00000001) continue;
            var t = Math.Clamp(((reference.X - a.X) * dx + (reference.Y - a.Z) * dz) / squared, 0, 1);
            var point = new Vector2((float)(a.X + dx * t), (float)(a.Z + dz * t));
            candidates.Add((reference.DistanceSquaredTo(point), road.Id, point));
        }
        if (candidates.Count == 0) return null;
        return candidates.OrderBy(candidate => candidate.Distance).ThenBy(candidate => candidate.RoadId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Point.X).ThenBy(candidate => candidate.Point.Y).First().Point;
    }

    private Rect2? PlaceLabel(Vector2 point, IReadOnlyList<string> lines, IReadOnlyList<Rect2> occupied, int fontSize)
    {
        var font = ThemeDB.FallbackFont;
        var size = new Vector2(lines.Max(s => font.GetStringSize(s, fontSize: fontSize).X) + 12, lines.Count * (font.GetHeight(fontSize) + 2) + 8);
        var area = new Rect2(new(4, 4), _canvas.Size - new Vector2(8, 8));
        if (size.X > area.Size.X || size.Y > area.Size.Y) return null;
        foreach (var offset in new[] { new Vector2(14, -size.Y - 8), new Vector2(-size.X - 14, -size.Y - 8),
            new Vector2(14, 12), new Vector2(-size.X - 14, 12), new Vector2(-size.X * .5f, -size.Y - 24), new Vector2(-size.X * .5f, 24) })
        {
            var at = point + offset;
            at = at.Clamp(area.Position, area.End - size);
            var box = new Rect2(at, size);
            if (!box.HasPoint(point) && !occupied.Any(other => other.Intersects(box))) return box;
        }
        return null;
    }
    private void DrawLabel(IReadOnlyList<string> lines, Rect2 box, int size, Color ink, Color paper)
    {
        _canvas.DrawRect(box, paper);
        var font = ThemeDB.FallbackFont; var y = box.Position.Y + 4 + font.GetAscent(size);
        foreach (var line in lines) { _canvas.DrawString(font, new(box.Position.X + 6, y), line, fontSize: size, modulate: ink); y += font.GetHeight(size) + 2; }
    }
    private static bool ClipSegment(Rect2 rect, ref Vector2 a, ref Vector2 b)
    {
        var start = a; var d = b - a; var lo = 0f; var hi = 1f;
        foreach (var (p, q) in new[] { (-d.X, start.X - rect.Position.X), (d.X, rect.End.X - start.X),
            (-d.Y, start.Y - rect.Position.Y), (d.Y, rect.End.Y - start.Y) })
        {
            if (Math.Abs(p) < .000001f) { if (q < 0) return false; continue; }
            var r = q / p;
            if (p < 0) lo = Math.Max(lo, r); else hi = Math.Min(hi, r);
            if (lo > hi) return false;
        }
        a = start + d * lo; b = start + d * hi; return true;
    }
}
