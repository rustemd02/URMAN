using System.Text.Json.Nodes;
using Godot;
using Urman.Experiments.AgentBAct1;
using Urman.Godot;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.App;

/// <summary>Where "Play from here" puts the player.</summary>
public sealed record StudioPlayStart(string Zone, string Spawn, Vector3? Position, float Yaw);

/// <summary>
/// The world section: the real Act I village, built by the game's own code in
/// an isolated debug session inside a SubViewport, with an editor camera,
/// markers for authored entities and the object catalogue. Every change is a
/// typed edit of a world plot file (one undo step per drag or placement) and
/// is shown at once on the game's own nodes — bespoke plots by authoredId,
/// generic plots rebuilt by the game's AuthoredWorldDirector.
/// </summary>
public sealed partial class StudioWorldSection(StudioRoot studio) : IStudioSection
{
    private const float PickRadiusPixels = 22f;
    public const string StreetPlotPath = "game/content/world/studio-street.world.v1.json";
    private readonly Dictionary<string, Node3D> _markers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _previewed = new(StringComparer.Ordinal);
    private Control? _view;
    private SubViewport _viewport = null!;
    private SubViewportContainer _container = null!;
    private Camera3D _camera = null!;
    private Node3D _markerRoot = null!;
    private MeshInstance3D _ghost = null!;
    private Label _hint = null!;
    private ItemList _categories = null!;
    private ItemList _catalogList = null!;
    private LineEdit _catalogSearch = null!;
    private Act1DemoRoot? _demo;
    private Vector3 _pivot = new(4f, 0f, -40f);
    private float _yaw = -35f;
    private float _pitch = -38f;
    private float _distance = 34f;
    private bool _topView;
    private bool _orbiting;
    private bool _panning;
    private IDisposable? _drag;
    private string? _dragId;
    private string? _placing;
    private JsonArray _catalog = [];
    private string? _catalogState;
    private readonly HashSet<string> _group = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _layers = new(StringComparer.Ordinal)
    {
        ["kit-placement"] = true, ["prop"] = true, ["scatter"] = true, ["npc"] = true, ["pickup"] = true, ["item-spawn"] = true,
        ["trigger"] = true, ["marker"] = true, ["fence-run"] = true, ["road-path"] = true
    };
    private bool _measuring;
    private bool _sculpting;
    private Button _terrainButton = null!;
    private OptionButton _terrainMode = null!;
    private SpinBox _terrainStrength = null!;
    public const string TerrainPath = "game/content/world/terrain.v1.json";
    private string? _terrainShown;
    private Vector3? _measureStart;
    private Label _measureLabel = null!;
    private Dictionary<string, Vector3> _dragOffsets = [];
    private Button _brushButton = null!;
    private SpinBox _brushRadius = null!;
    private SpinBox _brushDensity = null!;
    private bool _brushing;
    public static readonly string[] DefaultBrushMix =
    [
        "urman.catalog:agentb_foliage_kit/winterspruce-3", "urman.catalog:agentb_foliage_kit/winterspruce-4",
        "urman.catalog:agentb_foliage_kit/winterbirch-1", "urman.catalog:agentb_foliage_kit/winterbirch-2", "urman.catalog:agentb_foliage_kit/winterrowan-1"
    ];

    public string Key => "world";
    public string Title => "Мир · деревня";
    public Control View => _view ??= Build();
    public Vector3 Pivot => _pivot;
    public bool WorldReady => _demo is not null && _hint is { Visible: false };

    // ---- view -------------------------------------------------------------------

    private Control Build()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 0);
        var toolbarPanel = new PanelContainer();
        var toolbar = new HBoxContainer();
        toolbarPanel.AddChild(toolbar);
        root.AddChild(toolbarPanel);
        StudioRoot.Button(toolbar, "3D", () => { _topView = false; UpdateCamera(); });
        StudioRoot.Button(toolbar, "План сверху", () => { _topView = true; UpdateCamera(); });
        StudioRoot.Button(toolbar, "◎ К выделенному", FocusSelection).TooltipText = "Навести камеру на выделенный объект (F)";
        StudioRoot.Button(toolbar, "👁 С уровня глаз", EyeLevel).TooltipText = "Посмотреть на выделенное с высоты глаз игрока (1,7 м)";
        var layers = new MenuButton { Text = "Слои ▾", Flat = false };
        var layerNames = new (string Kind, string Label)[] { ("kit-placement", "Постройки деревни"), ("prop", "Поставленные объекты"), ("scatter", "Посадки"), ("npc", "Персонажи"),
            ("pickup", "Предметы"), ("item-spawn", "Предметы квестов"), ("trigger", "Области-триггеры"), ("marker", "Точки"), ("fence-run", "Заборы"), ("road-path", "Дороги") };
        for (var index = 0; index < layerNames.Length; index++)
        {
            layers.GetPopup().AddCheckItem(layerNames[index].Label, index);
            layers.GetPopup().SetItemChecked(index, true);
        }

        layers.GetPopup().HideOnCheckableItemSelection = false;
        layers.GetPopup().IdPressed += itemId =>
        {
            var index = (int)itemId;
            var popup = layers.GetPopup();
            popup.SetItemChecked(index, !popup.IsItemChecked(index));
            _layers[layerNames[index].Kind] = popup.IsItemChecked(index);
            BuildMarkers();
        };
        toolbar.AddChild(layers);
        StudioRoot.Button(toolbar, "📏 Линейка", () => { _measuring = !_measuring; _measureStart = null; studio.RefreshStatus(_measuring ? "Линейка: щёлкните две точки на земле" : "Линейка выключена"); });
        StudioRoot.Button(toolbar, "☀ Свет и погода", OpenAtmosphere).TooltipText = "Небо, солнце, туман; предпросмотр на деревне";
        _terrainButton = StudioRoot.Button(toolbar, "⛰ Рельеф", () =>
        {
            _sculpting = !_sculpting;
            _terrainButton.ThemeTypeVariation = _sculpting ? "PrimaryButton" : "";
            studio.RefreshStatus(_sculpting ? "Рельеф: щелчок по земле — один штрих (радиус как у кисти), ⌘Z отменяет" : "Рельеф выключен");
        });
        _terrainMode = new OptionButton();
        foreach (var mode in new[] { "поднять", "опустить", "сгладить", "выровнять" }) _terrainMode.AddItem(mode);
        toolbar.AddChild(_terrainMode);
        _terrainStrength = new SpinBox { MinValue = .05, MaxValue = 5, Step = .05, Value = .5, Suffix = "м" };
        toolbar.AddChild(_terrainStrength);
        _brushButton = StudioRoot.Button(toolbar, "🌲 Кисть растительности", ToggleBrush);
        _brushButton.TooltipText = "Щелчок по земле сажает деревья и кусты. Порода — выбранная в каталоге или смесь ели, берёзы и рябины";
        toolbar.AddChild(new Label { Text = "радиус", ThemeTypeVariation = "MutedLabel" });
        _brushRadius = new SpinBox { MinValue = 1, MaxValue = 40, Value = 8, Suffix = "м" };
        toolbar.AddChild(_brushRadius);
        toolbar.AddChild(new Label { Text = "густота", ThemeTypeVariation = "MutedLabel" });
        _brushDensity = new SpinBox { MinValue = .005, MaxValue = .5, Step = .005, Value = .05, Suffix = "/м²" };
        toolbar.AddChild(_brushDensity);
        toolbar.AddChild(new Label { Text = "Правая кнопка или Alt+левая — вращать · два пальца или Shift — сдвигать · колесо или щипок — приблизить", ThemeTypeVariation = "MutedLabel" });

        _container = new SubViewportContainer { Stretch = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Stop, FocusMode = Control.FocusModeEnum.All };
        _viewport = new SubViewport { OwnWorld3D = true, HandleInputLocally = false, GuiDisableInput = true, Msaa3D = Viewport.Msaa.Msaa2X };
        _container.AddChild(_viewport);
        _container.GuiInput += OnViewportInput;
        root.AddChild(_container);
        _hint = new Label { Text = "Загружается деревня… Это та же сборка мира, что и в игре.", Position = new Vector2(16, 60), ThemeTypeVariation = "MutedLabel" };
        _container.AddChild(_hint);
        _measureLabel = new Label { Position = new Vector2(16, 16), Visible = false };
        _container.AddChild(_measureLabel);

        _camera = new Camera3D { Far = 900f, Fov = 60f };
        _viewport.AddChild(_camera);
        _markerRoot = new Node3D { Name = "StudioMarkers" };
        _viewport.AddChild(_markerRoot);
        _ghost = new MeshInstance3D { Visible = false, Mesh = new BoxMesh(), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.55f, .85f, .6f, .45f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } };
        _viewport.AddChild(_ghost);
        root.AddChild(BuildCatalogDrawer());
        _ = LoadWorldAsync();
        UpdateCamera();
        return root;
    }

    private Control BuildCatalogDrawer()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 230) };
        var row = new HBoxContainer();
        panel.AddChild(row);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(210, 0) };
        left.AddChild(new Label { Text = "Каталог", ThemeTypeVariation = "HeaderLabel" });
        _categories = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _categories.ItemSelected += _ => FillCatalog();
        left.AddChild(_categories);
        row.AddChild(left);
        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var searchRow = new HBoxContainer();
        _catalogSearch = new LineEdit { PlaceholderText = "Найти в каталоге: дом, забор, берёза…", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _catalogSearch.TextChanged += _ => FillCatalog();
        searchRow.AddChild(_catalogSearch);
        StudioRoot.Button(searchRow, "+ Импорт модели, картинки, звука…", OpenImportDialog).TooltipText = "glTF/GLB, PNG/JPEG, WAV/OGG. Файлы можно просто перетащить в окно Studio";
        right.AddChild(searchRow);
        _catalogList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, MaxColumns = 0, IconMode = ItemList.IconModeEnum.Top,
            FixedIconSize = new Vector2I(112, 84), FixedColumnWidth = 150, SameColumnWidth = true, SelectMode = ItemList.SelectModeEnum.Multi
        };
        _catalogList.ItemSelected += index => StartPlacing((string)_catalogList.GetItemMetadata((int)index));
        right.AddChild(_catalogList);
        row.AddChild(right);

        ReloadCatalog();
        return panel;
    }

    /// <summary>The catalogue is workspace data: imports and renames show up at once and undo like any edit.</summary>
    private void ReloadCatalog()
    {
        var selectedCategory = _categories.GetSelectedItems() is { Length: > 0 } picked && picked[0] > 0 ? (string)_categories.GetItemMetadata(picked[0]) : null;
        _catalog = new JsonArray(studio.Workspace.EntityIds.Where(id => id.StartsWith("urman.catalog:", StringComparison.Ordinal))
            .Select(id => studio.Workspace.Get(id)).Where(entry => entry is not null && (bool?)entry["hidden"] != true).ToArray());
        _categories.Clear();
        _categories.AddItem("Все");
        foreach (var category in _catalog.Select(entry => (string)entry!["category"]!).Distinct().Order(StringComparer.CurrentCulture))
        {
            _categories.AddItem($"{category}  ({_catalog.Count(entry => (string)entry!["category"]! == category)})");
            _categories.SetItemMetadata(_categories.ItemCount - 1, category);
        }

        _categories.Select(0);
        for (var index = 1; index < _categories.ItemCount; index++)
        {
            if ((string)_categories.GetItemMetadata(index) == selectedCategory) _categories.Select(index);
        }

        FillCatalog();
    }

    private void FillCatalog()
    {
        _catalogList.Clear();
        var selected = _categories.GetSelectedItems();
        var category = selected.Length > 0 && selected[0] > 0 ? (string)_categories.GetItemMetadata(selected[0]) : null;
        var query = _catalogSearch.Text.Trim().ToLowerInvariant();
        foreach (var entry in _catalog.OfType<JsonObject>())
        {
            var name = (string)entry["name"]!;
            if (category is not null && (string)entry["category"]! != category) continue;
            if (query.Length > 0 && !name.ToLowerInvariant().Contains(query, StringComparison.Ordinal)) continue;
            var size = entry["size"]!.AsArray();
            _catalogList.AddItem($"{name}\n{(double)size[0]!:0.#}×{(double)size[2]!:0.#}×{(double)size[1]!:0.#} м", LoadPreview((string)entry["id"]!));
            _catalogList.SetItemMetadata(_catalogList.ItemCount - 1, (string)entry["id"]!);
            _catalogList.SetItemTooltip(_catalogList.ItemCount - 1, $"{name}\n{entry["category"]} · набор «{entry["kit"]}» · коллизия: {((string)entry["collision"]! == "box" ? "есть" : "нет")}\n{entry["license"]}\n{entry["id"]}");
        }
    }

    private Texture2D? LoadPreview(string catalogId)
    {
        var file = StudioCatalogBuilder.PreviewPath(studio.Workspace.Root, catalogId);
        return File.Exists(file) && Image.LoadFromFile(file) is { } image ? ImageTexture.CreateFromImage(image) : null;
    }

    private async Task LoadWorldAsync()
    {
        _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
        _viewport.AddChild(_demo);
        for (var frame = 0; frame < 900 && !_demo.MainMenuVisible; frame++)
        {
            await studio.ToSignal(studio.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!await _demo.StartDebugZoneAsync("village_day", "arrival"))
        {
            _hint.Text = "Не удалось поднять мир игры. Подробности — в журнале Studio.";
            return;
        }

        for (var frame = 0; frame < 20; frame++)
        {
            await studio.ToSignal(studio.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        // An editor view: the world stands still, the game's HUD and player are off.
        foreach (var layer in _demo.FindChildren("*", nameof(CanvasLayer), true, false).OfType<CanvasLayer>())
        {
            layer.Visible = false;
        }

        if (_demo.GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            // The preview player is parked: modal, so the game's own focus-loss
            // pause never covers the editor view when Studio's window loses focus.
            player.SetModalOpen(true);
            player.ProcessMode = Node.ProcessModeEnum.Disabled;
        }

        _demo.ProcessMode = Node.ProcessModeEnum.Disabled;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _camera.MakeCurrent();
        _hint.Visible = false;
        foreach (var (id, entity) in WorldEntities().Where(item => IsGeneric(item.Id)))
        {
            _previewed[id] = entity.ToJsonString();
        }

        Refresh();
    }

    public StudioAtmospherePanel? Atmosphere { get; private set; }

    public void OpenAtmosphere()
    {
        if (Atmosphere is null)
        {
            Atmosphere = new StudioAtmospherePanel(studio, () => _demo?.GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld);
            studio.AddChild(Atmosphere);
        }

        Atmosphere.Reload();
        Atmosphere.Popup(new Rect2I(new Vector2I(760, 90), new Vector2I(470, 660)));
    }

    private string? _atmosphereShown;

    public void Refresh()
    {
        if (_view is null || _demo is null)
        {
            return;
        }

        // Atmosphere edits, undo and redo show on the village at once.
        if (studio.Workspace.HasFile(StudioAtmospherePanel.FilePath) && studio.Workspace.File(StudioAtmospherePanel.FilePath).Text is var atmosphere && atmosphere != _atmosphereShown)
        {
            if (_atmosphereShown is not null)
            {
                AtmosphereProfiles.Reload(atmosphere);
                (_demo.GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld)?.RefreshAtmosphereForStudio();
            }

            _atmosphereShown = atmosphere;
        }

        var catalogState = string.Join("|", studio.Workspace.EntityIds.Where(id => id.StartsWith("urman.catalog:", StringComparison.Ordinal)).Select(id => studio.Workspace.Get(id)?.ToJsonString().GetHashCode()));
        if (catalogState != _catalogState) { _catalogState = catalogState; ReloadCatalog(); }
        SyncTerrain();
        SyncPreview();
        BuildMarkers();
    }

    private AuthoredWorldDirector? Director => _demo is null ? null : AuthoredWorldDirector.Current(_demo.GetTree());

    /// <summary>Show edited, undone or redone generic objects on the game's own nodes.</summary>
    private void SyncPreview()
    {
        if (Director is not { } director) return;
        var current = WorldEntities().Where(item => IsGeneric(item.Id)).ToDictionary(item => item.Id, item => item.Entity.ToJsonString(), StringComparer.Ordinal);
        foreach (var (id, json) in current)
        {
            if (!_previewed.TryGetValue(id, out var shown) || shown != json)
            {
                director.PreviewUpsert(json);
                _previewed[id] = json;
            }
        }

        foreach (var id in _previewed.Keys.Except(current.Keys).Where(id => !id.StartsWith("bespoke:", StringComparison.Ordinal)).ToArray())
        {
            director.PreviewRemove(id);
            _previewed.Remove(id);
        }

        // Bespoke plots (the village kit, Tamara's yard): reapply changed
        // entities to the game nodes stamped with their authoredId (edits, undo, redo).
        foreach (var (id, entity) in WorldEntities().Where(item => !IsGeneric(item.Id)))
        {
            var key = "bespoke:" + id;
            var json = entity.ToJsonString();
            if (_previewed.TryGetValue(key, out var shown) && shown != json)
            {
                ApplyEntity(id, entity);
            }

            _previewed[key] = json;
        }
    }

    private void ApplyEntity(string id, JsonObject entity)
    {
        if (_demo is null || entity["params"] is not JsonObject parameters || parameters["position"] is not JsonArray position) return;
        var x = (float)(double)position[0]!;
        var z = (float)(double)position[2]!;
        foreach (var node in _demo.FindChildren("*", nameof(Node3D), true, false).OfType<Node3D>())
        {
            if (!node.HasMeta(AuthoredWorldPlot.AuthoredIdMeta) || (string)node.GetMeta(AuthoredWorldPlot.AuthoredIdMeta) != id) continue;
            if ((string?)entity["kind"] == "kit-placement")
            {
                node.GlobalPosition = new Vector3(x, node.GlobalPosition.Y, z);
                node.RotationDegrees = new Vector3(0, (float)(double)parameters["yawDegrees"]!, 0);
                var scale = parameters["scale"]!.AsArray();
                node.Scale = new Vector3((float)(double)scale[0]!, (float)(double)scale[1]!, (float)(double)scale[2]!);
                node.Visible = !((bool?)parameters["hidden"] ?? false);
            }
            else
            {
                node.GlobalPosition = new Vector3(x, AgentBAct1HeightField.CollisionGround(x, z), z);
            }
        }
    }

    public Node3D? PreviewNode(string id) => Director?.ObjectRoot(id);

    private bool IsGeneric(string id) =>
        studio.Workspace.Locate(id) is { } address && (string?)studio.Workspace.File(address.RelativePath).Header("executor") == "generic";

    private IEnumerable<(string Id, JsonObject Entity)> WorldEntities() =>
        studio.Workspace.EntityIds
            .Where(id => id.StartsWith("urman.world:", StringComparison.Ordinal))
            .Select(id => (id, studio.Workspace.Get(id) as JsonObject))
            .Where(item => item.Item2 is not null)
            .Select(item => (item.id, item.Item2!));

    private void BuildMarkers()
    {
        foreach (var child in _markerRoot.GetChildren())
        {
            child.QueueFree();
        }

        _markers.Clear();
        foreach (var (id, entity) in WorldEntities())
        {
            if (Anchor(entity) is not { } at)
            {
                continue;
            }

            var selected = id == studio.Selection || _group.Contains(id);
            var kind = (string?)entity["kind"];
            if (kind is not null && _layers.TryGetValue(kind, out var shown) && !shown && !selected) continue;
            var color = kind switch
            {
                "item-spawn" or "pickup" => new Color("d7a66b"),
                "marker" or "trigger" => StudioTheme.Violet,
                "npc" => new Color("9fd3a7"),
                "fence-run" => new Color("c9826f"),
                "kit-placement" => new Color("8fa7c2"),
                _ => StudioTheme.Accent
            };
            if (selected) color = new Color("ffe27a");
            var marker = new Node3D { Name = "Marker", Position = at };
            var material = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true, RenderPriority = 10 };
            if (kind == "trigger" && entity["params"]?["size"] is JsonArray size)
            {
                var box = new Vector3((float)(double)size[0]!, (float)(double)size[1]!, (float)(double)size[2]!);
                material.AlbedoColor = new Color(color, selected ? .35f : .2f);
                material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                marker.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = box, Material = material }, Position = new Vector3(0, box.Y / 2, 0) });
            }
            else
            {
                marker.AddChild(new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = selected ? .45f : kind == "kit-placement" ? .2f : .3f, Height = selected ? .9f : kind == "kit-placement" ? .4f : .6f, Material = material },
                    Position = new Vector3(0, .6f, 0)
                });
            }

            if (kind == "kit-placement" && !selected)
            {
                _markerRoot.AddChild(marker);
                _markers[id] = marker;
                continue;
            }

            marker.AddChild(new Label3D
            {
                Text = (string?)entity["name"] ?? id,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                FontSize = selected ? 56 : 34,
                OutlineSize = 10,
                Modulate = color,
                Position = new Vector3(0, kind == "npc" ? 2.2f : 1.45f, 0),
                PixelSize = .006f
            });
            _markerRoot.AddChild(marker);
            _markers[id] = marker;
        }

        DrawRoutine();
    }

    private static Vector3? Anchor(JsonObject entity)
    {
        var parameters = entity["params"] as JsonObject;
        JsonArray? point = parameters?["position"] as JsonArray ?? parameters?["center"] as JsonArray;
        if (point is null && parameters?["points"] is JsonArray points && points.Count > 0 && points[0] is JsonArray first)
        {
            point = new JsonArray((double)first[0]!, 0.0, (double)first[1]!);
        }

        if (point is null)
        {
            return null;
        }

        var x = (float)(double)point[0]!;
        var z = (float)(double)point[2]!;
        return new Vector3(x, AgentBAct1HeightField.CollisionGround(x, z), z);
    }

    // ---- camera and input ----------------------------------------------------------------

    /// <summary>Put the editor camera exactly where a cutscene shot stands (preview only).</summary>
    public void ShowShot(Vector3 position, Vector3 look, float fov)
    {
        _topView = false;
        _camera.Projection = Camera3D.ProjectionType.Perspective;
        _camera.GlobalPosition = position;
        _camera.LookAt(look, Vector3.Up);
        _camera.Fov = fov;
        _pivot = look;
        _distance = position.DistanceTo(look);
    }

    /// <summary>The current editor view as a shot: position, what it looks at, field of view.</summary>
    public (Vector3 Position, Vector3 Look, float Fov)? CameraShot() =>
        _camera is null || !_camera.IsInsideTree() ? null : (_camera.GlobalPosition, _pivot, _camera.Fov);

    public void LookAt(Vector3 point, float distance)
    {
        _pivot = point;
        _distance = distance;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (_camera is null)
        {
            return;
        }

        if (_topView)
        {
            _camera.Projection = Camera3D.ProjectionType.Orthogonal;
            _camera.Size = _distance * 1.4f;
            _camera.GlobalPosition = _pivot + new Vector3(0, 120f, 0);
            _camera.RotationDegrees = new Vector3(-90f, 0f, 0f);
            return;
        }

        _camera.Projection = Camera3D.ProjectionType.Perspective;
        var rotation = Basis.FromEuler(new Vector3(Mathf.DegToRad(_pitch), Mathf.DegToRad(_yaw), 0f));
        _camera.GlobalPosition = _pivot + rotation * new Vector3(0, 0, _distance);
        _camera.LookAt(_pivot, Vector3.Up);
    }

    private void OnViewportInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                Zoom(.88f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                Zoom(1.14f);
                break;
            case InputEventMagnifyGesture magnify:
                Zoom(1f / magnify.Factor);
                break;
            case InputEventPanGesture pan:
                Pan(pan.Delta * 6f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } right:
                _orbiting = right.Pressed && !right.ShiftPressed;
                _panning = right.Pressed && right.ShiftPressed;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left when left.AltPressed:
                _orbiting = left.Pressed;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when _measuring:
                if (GroundHit(press.Position) is { } point)
                {
                    if (_measureStart is null) { _measureStart = point; _measureLabel.Text = "Линейка: вторая точка…"; }
                    else { _measureLabel.Text = $"Расстояние: {_measureStart.Value.DistanceTo(point):0.00} м (по земле {new Vector2(_measureStart.Value.X - point.X, _measureStart.Value.Z - point.Z).Length():0.00} м)"; _measureStart = null; }
                    _measureLabel.Visible = true;
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true, ShiftPressed: true } press:
                AddToGroup(press.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when _sculpting:
                if (GroundHit(press.Position) is { } sculptAt)
                {
                    var mode = new[] { "raise", "lower", "smooth", "flatten" }[_terrainMode.Selected];
                    SculptStroke(sculptAt, mode, (float)_brushRadius.Value, mode is "smooth" or "flatten" ? 1f : (float)_terrainStrength.Value);
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when _brushing:
                if (GroundHit(press.Position) is { } stroke) PaintStroke(stroke, BrushModels(), (float)_brushRadius.Value, (float)_brushDensity.Value);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when _pointPick is not null:
                if (GroundHit(press.Position) is { } picked) _pointPick(picked);
                break;
            case InputEventKey { Pressed: true, Keycode: global::Godot.Key.Escape or global::Godot.Key.Enter } when _pointPick is not null:
                EndPointPick();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when _placing is not null:
                if (GroundHit(press.Position) is { } spot) Place(_placing, spot);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                _container.GrabFocus();
                var keepGroup = studio.Selection is { } before && (_group.Contains(Pick(press.Position) ?? "") || before == Pick(press.Position)) && _group.Count > 0;
                if (!keepGroup) { _group.Clear(); PickAt(press.Position); }
                if (studio.Selection is { } id && _markers.ContainsKey(id) && Movable(id) && !Locked(id))
                {
                    _dragId = id;
                    var anchor = Anchor((JsonObject)studio.Workspace.Get(id)!) ?? Vector3.Zero;
                    _dragOffsets = _group.Append(id).Distinct().Where(member => Movable(member) && !Locked(member))
                        .ToDictionary(member => member, member => (Anchor((JsonObject)studio.Workspace.Get(member)!) ?? anchor) - anchor);
                    _drag = studio.Session.Begin(_dragOffsets.Count > 1 ? $"переместить {_dragOffsets.Count} объекта" : $"переместить «{studio.Catalog.NameOf(id)}»");
                }

                break;
            case InputEventKey { Pressed: true, Keycode: global::Godot.Key.D } duplicate when duplicate.MetaPressed || duplicate.CtrlPressed:
                DuplicateSelection();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                EndDrag();
                break;
            case InputEventMouseMotion motion when _orbiting:
                _yaw -= motion.Relative.X * .3f;
                _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * .3f, -88f, -5f);
                UpdateCamera();
                break;
            case InputEventMouseMotion motion when _panning:
                Pan(motion.Relative);
                break;
            case InputEventMouseMotion motion when _placing is not null:
                MoveGhost(motion.Position);
                break;
            case InputEventMouseMotion motion when _dragId is not null:
                DragTo(motion.Position);
                break;
            case InputEventKey { Pressed: true, Keycode: global::Godot.Key.F }:
                FocusSelection();
                break;
            case InputEventKey { Pressed: true, Keycode: global::Godot.Key.Escape } when _placing is not null:
                CancelPlacing();
                break;
            case InputEventKey { Pressed: true, Keycode: global::Godot.Key.Escape } when _dragId is not null:
                EndDrag();
                studio.Undo();
                break;
        }
    }

    private void Zoom(float factor)
    {
        _distance = Mathf.Clamp(_distance * factor, 3f, 260f);
        UpdateCamera();
    }

    private void Pan(Vector2 delta)
    {
        var right = _camera.GlobalBasis.X with { Y = 0 };
        var forward = _topView ? -_camera.GlobalBasis.Y with { Y = 0 } : -_camera.GlobalBasis.Z with { Y = 0 };
        var scale = _distance * .0025f;
        _pivot += (-right.Normalized() * delta.X + forward.Normalized() * delta.Y) * scale;
        UpdateCamera();
    }

    private Vector3? GroundHit(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen);
        // March along the ray down to the terrain surface the game itself uses.
        for (var t = 0f; t < 600f; t += .25f)
        {
            var point = origin + direction * t;
            var ground = AgentBAct1HeightField.CollisionGround(point.X, point.Z);
            if (point.Y <= ground)
            {
                return new Vector3(point.X, ground, point.Z);
            }
        }

        return null;
    }

    private string? Pick(Vector2 screen)
    {
        string? best = null;
        var bestDistance = PickRadiusPixels;
        foreach (var (id, marker) in _markers)
        {
            var world = marker.GlobalPosition + new Vector3(0, .6f, 0);
            if (_camera.IsPositionBehind(world) || Locked(id)) continue;
            var distance = _camera.UnprojectPosition(world).DistanceTo(screen);
            if (distance < bestDistance) { bestDistance = distance; best = id; }
        }

        return best;
    }

    private void AddToGroup(Vector2 screen)
    {
        if (Pick(screen) is not { } id) return;
        if (studio.Selection is { } current) _group.Add(current);
        if (!_group.Add(id)) _group.Remove(id);
        studio.Select(id);
        BuildMarkers();
        studio.RefreshStatus($"Выбрано объектов: {_group.Count}. Перетаскивание двигает их вместе");
    }

    public IReadOnlyCollection<string> Group => _group;

    public void SelectGroup(IEnumerable<string> ids)
    {
        _group.Clear();
        foreach (var id in ids) _group.Add(id);
        studio.Select(_group.LastOrDefault());
        BuildMarkers();
    }

    public void MoveGroup(Vector3 delta)
    {
        using var _ = studio.Session.Begin($"переместить {_group.Count} объекта");
        foreach (var id in _group.Where(member => Movable(member) && !Locked(member)).ToArray())
        {
            var at = Anchor((JsonObject)studio.Workspace.Get(id)!)!.Value + delta;
            MoveEntity(id, at.X, at.Z, "перемещение группы");
        }
    }

    private bool Locked(string id) => (bool?)studio.Workspace.Get(id)?["locked"] ?? false;

    /// <summary>Duplicate the selection: new IDs, a metre aside; one undo step (spec DATA04, WORLD07).</summary>
    public IReadOnlyList<string> DuplicateSelection()
    {
        var sources = _group.Count > 0 ? _group.ToArray() : studio.Selection is { } one ? [one] : [];
        var copies = new List<string>();
        using (studio.Session.Begin(sources.Length > 1 ? $"дублировать {sources.Length} объекта" : "дублировать"))
        {
            foreach (var id in sources.Where(IsGeneric))
            {
                var address = studio.Workspace.Locate(id)!;
                var copy = studio.Workspace.Get(id)!.DeepClone().AsObject();
                var copyId = id[..(id.LastIndexOf('/') + 1)] + (string?)copy["kind"] + "-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4));
                copy["id"] = copyId;
                copy["name"] = (string?)copy["name"] + " (копия)";
                if (copy["params"]?["position"] is JsonArray position) position[0] = Math.Round((double)position[0]! + 1.0, 3);
                if (copy["params"]?["seed"] is not null) copy["params"]!["seed"] = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue);
                studio.Session.Set(address.RelativePath, copyId, copy, "дублировать");
                copies.Add(copyId);
            }
        }

        if (copies.Count == 0 && sources.Length > 0) studio.RefreshStatus("Дублировать можно поставленные в Studio объекты; постройки деревни — только перемещать");
        else SelectGroup(copies);
        return copies;
    }

    private void EyeLevel()
    {
        if (studio.Selection is not { } id || studio.Workspace.Get(id) is not JsonObject entity || Anchor(entity) is not { } at) return;
        _topView = false;
        _pitch = -4f;
        _pivot = at + new Vector3(0, 1.7f, 0);
        _distance = 6f;
        UpdateCamera();
    }

    private void PickAt(Vector2 screen)
    {
        studio.Select(Pick(screen));
        BuildMarkers();
    }

    private void DragTo(Vector2 screen)
    {
        if (_dragId is null || GroundHit(screen) is not { } hit)
        {
            return;
        }

        foreach (var (member, offset) in _dragOffsets.Count > 0 ? _dragOffsets : new() { [_dragId] = Vector3.Zero })
        {
            MoveEntity(member, Mathf.Snapped(hit.X + offset.X, .05f), Mathf.Snapped(hit.Z + offset.Z, .05f), "перемещение");
        }
    }

    private void EndDrag()
    {
        _drag?.Dispose();
        _drag = null;
        _dragId = null;
    }

    private bool Movable(string id) => studio.Workspace.Get(id)?["params"]?["position"] is JsonArray;

    public void MoveEntity(string id, float x, float z, string label)
    {
        var entity = (JsonObject)studio.Workspace.Get(id)!;
        var position = entity["params"]!["position"]!.AsArray();
        var y = (double)position[1]!;
        studio.Session.SetField(id, ["params", "position"], new JsonArray(Math.Round(x, 3), y, Math.Round(z, 3)), label);
        ApplyToWorld(id, x, z);
    }

    /// <summary>Move the game nodes built from a bespoke plot entity (presentation only; the file is the source).</summary>
    private void ApplyToWorld(string id, float x, float z)
    {
        if (_demo is null || IsGeneric(id) || studio.Workspace.Get(id) is not JsonObject entity) return;
        ApplyEntity(id, entity);
        _previewed["bespoke:" + id] = entity.ToJsonString();
    }

    private void FocusSelection()
    {
        if (studio.Selection is { } id && _markers.TryGetValue(id, out var marker))
        {
            _pivot = marker.GlobalPosition;
            _distance = Mathf.Min(_distance, 18f);
            UpdateCamera();
        }
    }

    // ---- placing from the catalogue (WORLD03) -------------------------------------------------

    public void StartPlacing(string catalogId)
    {
        _placing = catalogId;
        var entry = _catalog.OfType<JsonObject>().First(item => (string)item["id"]! == catalogId);
        var size = entry["size"]!.AsArray();
        ((BoxMesh)_ghost.Mesh).Size = new Vector3((float)(double)size[0]!, (float)(double)size[1]!, (float)(double)size[2]!);
        _ghost.Visible = false;
        _container.GrabFocus();
        studio.RefreshStatus($"Щёлкните по земле, чтобы поставить «{entry["name"]}». Escape — отмена");
    }

    public void MoveGhost(Vector2 screen)
    {
        if (GroundHit(screen) is not { } spot) { _ghost.Visible = false; return; }
        var height = ((BoxMesh)_ghost.Mesh).Size.Y;
        _ghost.GlobalPosition = spot + new Vector3(0, height / 2, 0);
        _ghost.Visible = true;
    }

    public Vector2 ScreenOf(Vector3 world) => _camera.UnprojectPosition(world);

    private void CancelPlacing()
    {
        _placing = null;
        _ghost.Visible = false;
        _catalogList.DeselectAll();
        studio.RefreshStatus("Размещение отменено");
    }

    public string Place(string catalogId, Vector3 at)
    {
        var entry = _catalog.OfType<JsonObject>().First(item => (string)item["id"]! == catalogId);
        var id = $"urman.world:studio/street/prop-{Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4))}";
        using (studio.Session.Begin($"поставить «{entry["name"]}»"))
        {
            EnsureStreetPlot();
            studio.Session.Set(StreetPlotPath, id, new JsonObject
            {
                ["id"] = id,
                ["kind"] = "prop",
                ["name"] = (string)entry["name"]!,
                ["params"] = new JsonObject
                {
                    ["catalogId"] = catalogId,
                    ["position"] = new JsonArray(Math.Round(at.X, 2), 0.0, Math.Round(at.Z, 2)),
                    ["yawDegrees"] = 0.0,
                    ["scale"] = 1.0,
                    ["collision"] = (string)entry["collision"]! == "box" ? "box" : "none"
                }
            }, "поставить");
        }

        _placing = null;
        _ghost.Visible = false;
        _catalogList.DeselectAll();
        studio.Select(id);
        return id;
    }

    private void EnsureStreetPlot()
    {
        if (studio.Workspace.HasFile(StreetPlotPath)) return;
        studio.Workspace.CreateFile(StreetPlotPath,
            "{\n  \"schemaVersion\": 1,\n  \"kind\": \"urman.world-plot\",\n  \"id\": \"urman.world:studio/street\",\n  \"name\": \"Объекты, поставленные в Studio\",\n  \"executor\": \"generic\",\n  \"entities\": [\n  ]\n}\n");
    }

    // ---- import (WORLD05, WORLD06) ----------------------------------------------------------

    private void OpenImportDialog()
    {
        var dialog = new FileDialog { FileMode = FileDialog.FileModeEnum.OpenFiles, Access = FileDialog.AccessEnum.Filesystem, Title = "Импорт в каталог URMAN", UseNativeDialog = true };
        dialog.Filters = ["*.glb, *.gltf ; Модели glTF", "*.png, *.jpg, *.jpeg ; Изображения", "*.wav, *.ogg ; Звук"];
        dialog.FilesSelected += files => { foreach (var file in files) ShowImport(file); dialog.QueueFree(); };
        dialog.Canceled += dialog.QueueFree;
        studio.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 600));
    }

    public void ShowImport(string file)
    {
        var report = StudioImporter.Check(studio, file);
        var dialog = new ConfirmationDialog { Title = "Импорт: " + Path.GetFileName(file), OkButtonText = report.CanImport ? "Добавить" : "Закрыть", CancelButtonText = "Отмена", MinSize = new Vector2I(560, 0) };
        var box = new VBoxContainer();
        dialog.AddChild(box);
        var name = new LineEdit { Text = report.SuggestedName };
        var category = new OptionButton();
        foreach (var option in new[] { "Бытовой реквизит", "Дома и постройки", "Заборы и ворота", "Деревья и кусты", "Камни и снег", "Мебель", "Инструменты", "Игровые предметы", "Источники света", "Транспорт" }) category.AddItem(option);
        if (report.Kind == "model")
        {
            box.AddChild(new Label { Text = "Название в каталоге", ThemeTypeVariation = "MutedLabel" });
            box.AddChild(name);
            box.AddChild(new Label { Text = "Категория", ThemeTypeVariation = "MutedLabel" });
            box.AddChild(category);
            box.AddChild(new Label { Text = $"Размер: {report.Size.X:0.##} × {report.Size.Z:0.##} м, высота {report.Size.Y:0.##} м · частей: {report.Meshes} · материалов: {report.Materials}" + (report.Bones > 0 ? $" · скелет: {report.Bones} костей" : "") });
        }
        else
        {
            box.AddChild(new Label { Text = report.Kind == "image" ? "Изображение для документов и материалов." : report.Kind == "sound" ? "Звук для атмосферы и сцен." : "" });
        }

        foreach (var problem in report.Problems) box.AddChild(new Label { Text = "✕ " + problem, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = StudioTheme.Bad });
        foreach (var warning in report.Warnings) box.AddChild(new Label { Text = "! " + warning, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = StudioTheme.Warn });
        if (report.CanImport && report.Problems.Count == 0 && report.Warnings.Count == 0) box.AddChild(new Label { Text = "✓ Проверка пройдена.", Modulate = StudioTheme.Ok });
        box.AddChild(new Label { Text = $"Будет сохранён как {report.TargetPath}", ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.Arbitrary });
        dialog.Confirmed += () =>
        {
            if (report.CanImport)
            {
                var result = StudioImporter.Apply(studio, report, name.Text.Trim().Length > 0 ? name.Text.Trim() : report.SuggestedName, category.GetItemText(category.Selected));
                if (result is not null && report.Kind == "model") _ = RenderImportPreview(result, Path.Combine(studio.Workspace.Root, report.TargetPath));
                studio.RefreshStatus($"Импортировано: {Path.GetFileName(file)}");
            }

            dialog.QueueFree();
        };
        dialog.Canceled += dialog.QueueFree;
        studio.AddChild(dialog);
        dialog.PopupCentered();
        LastImportReport = report;
    }

    public ImportReport? LastImportReport { get; private set; }

    private async Task RenderImportPreview(string catalogId, string file)
    {
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromFile(file, state) != Error.Ok || document.GenerateScene(state) is not Node3D scene) return;
        await StudioCatalogBuilder.RenderSingle(studio, scene, StudioCatalogBuilder.PreviewPath(studio.Workspace.Root, catalogId));
        scene.QueueFree();
        _catalogState = null;
        ReloadCatalog();
    }

    // ---- terrain (WORLD10) ------------------------------------------------------------------

    /// <summary>One terrain stroke as one entity (one undo step): raise, lower, smooth, or flatten to the clicked height.</summary>
    public string SculptStroke(Vector3 at, string mode, float radius, float strength)
    {
        var id = $"urman.world:terrain/stroke-{Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4))}";
        var label = mode switch { "raise" => "поднять", "lower" => "опустить", "smooth" => "сгладить", _ => "выровнять" };
        using (studio.Session.Begin($"рельеф: {label}"))
        {
            if (!studio.Workspace.HasFile(TerrainPath))
            {
                studio.Workspace.CreateFile(TerrainPath, "{\n  \"schemaVersion\": 1,\n  \"kind\": \"urman.world-plot\",\n  \"id\": \"urman.world:terrain\",\n  \"name\": \"Правки рельефа\",\n  \"executor\": \"terrain\",\n  \"entities\": [\n  ]\n}\n");
            }

            studio.Session.Set(TerrainPath, id, new JsonObject
            {
                ["id"] = id,
                ["kind"] = "terrain-stroke",
                ["name"] = $"Рельеф: {label} (радиус {radius:0.#} м)",
                ["params"] = new JsonObject
                {
                    ["mode"] = mode,
                    ["position"] = new JsonArray(Math.Round(at.X, 2), 0.0, Math.Round(at.Z, 2)),
                    ["radius"] = Math.Round(radius, 2),
                    ["strength"] = Math.Round(strength, 3),
                    ["target"] = Math.Round(at.Y, 3)
                }
            }, "штрих рельефа");
        }

        return id;
    }

    /// <summary>Show terrain edits (and their undo) on the Studio's village: surface and collider together.</summary>
    private void SyncTerrain()
    {
        var text = studio.Workspace.HasFile(TerrainPath) ? studio.Workspace.File(TerrainPath).Text : "";
        if (text == _terrainShown) return;
        var first = _terrainShown is null;
        _terrainShown = text;
        if (first && text.Length == 0) return;
        Urman.Experiments.AgentBAct1.AgentBAct1HeightField.ReloadStrokes(text.Length == 0 ? "{\"entities\":[]}" : text);
        (_demo?.FindChild("AgentBExteriorWorld", true, false) as Urman.Godot.AgentBAct1ExteriorLayer)?.RebuildTerrainForStudio();
    }

    // ---- vegetation brush (WORLD11) ----------------------------------------------------------

    private void ToggleBrush()
    {
        _brushing = !_brushing;
        _brushButton.ThemeTypeVariation = _brushing ? "PrimaryButton" : "";
        studio.RefreshStatus(_brushing ? "Кисть: щёлкайте по земле. Каждый щелчок — один штрих, отменяется ⌘Z" : "Кисть выключена");
    }

    private string[] BrushModels()
    {
        var selected = _catalogList.GetSelectedItems().Select(index => (string)_catalogList.GetItemMetadata(index))
            .Where(id => _catalog.OfType<JsonObject>().Any(entry => (string)entry["id"]! == id && (string)entry["category"]! == "Деревья и кусты"))
            .ToArray();
        return selected.Length > 0 ? selected : DefaultBrushMix;
    }

    /// <summary>One brush stroke as one entity: centre, radius, density, models and a seed (one undo step, reproducible).</summary>
    public string PaintStroke(Vector3 at, string[] models, float radius, float density)
    {
        var id = $"urman.world:studio/street/plants-{Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4))}";
        using (studio.Session.Begin("штрих кисти растительности"))
        {
            EnsureStreetPlot();
            studio.Session.Set(StreetPlotPath, id, new JsonObject
            {
                ["id"] = id,
                ["kind"] = "scatter",
                ["name"] = $"Посадки ({models.Length} пород, радиус {radius:0.#} м)",
                ["params"] = new JsonObject
                {
                    ["position"] = new JsonArray(Math.Round(at.X, 2), 0.0, Math.Round(at.Z, 2)),
                    ["radius"] = Math.Round(radius, 2),
                    ["density"] = Math.Round(density, 4),
                    ["seed"] = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue),
                    ["catalogIds"] = new JsonArray(models.Select(model => (JsonNode?)model).ToArray()),
                    ["scaleMin"] = 0.8,
                    ["scaleMax"] = 1.2
                }
            }, "штрих");
        }

        if (PreviewNode(id) is { } node && node.HasMeta("scatterKept"))
        {
            studio.RefreshStatus($"Посажено {(int)node.GetMeta("scatterKept")}; на дорогах и проходах пропущено {(int)node.GetMeta("scatterSkippedOnPassages")}");
        }

        return id;
    }

    // ---- section contract --------------------------------------------------------------------

    public bool Reveal(string id)
    {
        if (!id.StartsWith("urman.world:", StringComparison.Ordinal) || studio.Workspace.Get(id) is null)
        {
            return false;
        }

        studio.Select(id);
        if (_view is not null)
        {
            BuildMarkers();
            FocusSelection();
        }

        return true;
    }

    public StudioPlayStart? PlayStart()
    {
        if (studio.Selection is not { } id || studio.Workspace.Get(id) is not JsonObject entity || Anchor(entity) is not { } at)
        {
            return null;
        }

        // Stand a step toward the camera from the object, facing it, so the
        // player never spawns inside what they came to check.
        var toward = (_camera.GlobalPosition - at) with { Y = 0 };
        toward = toward.LengthSquared() < .01f ? Vector3.Back : toward.Normalized();
        var spawn = at + toward * 2.2f;
        spawn.Y = AgentBAct1HeightField.CollisionGround(spawn.X, spawn.Z) + .1f;
        var yaw = Mathf.RadToDeg(Mathf.Atan2(-toward.X, -toward.Z));
        return new("village_day", "arrival", spawn, yaw);
    }

    public void FillProperties(StudioProperties panel)
    {
        if (studio.Selection is not { } id || studio.Workspace.Get(id) is not JsonObject entity)
        {
            panel.Header("Ничего не выбрано", "Мир");
            panel.Text("Щёлкните маркер на карте или выберите объект в каталоге внизу и щёлкните по земле. Двор Тамары и объекты, поставленные в Studio, редактируются здесь; остальная деревня пока собирается кодом и появится по мере переноса (этап P2).", muted: true);
            return;
        }

        var summary = studio.Catalog.Describe(id);
        panel.Header(summary.Name, summary.KindLabel + " · экземпляр в мире");
        panel.IdLine(id);
        var parameters = entity["params"] as JsonObject ?? [];
        panel.TextField("Название", (string?)entity["name"] ?? "", text => studio.Session.SetField(id, ["name"], text, "переименование"));
        var lockBox = new CheckBox { Text = "Заблокировать от случайного выбора", ButtonPressed = (bool?)entity["locked"] ?? false };
        lockBox.Toggled += on => studio.Session.SetField(id, ["locked"], on, on ? "заблокировать" : "разблокировать");
        panel.AddChild(lockBox);
        if (_group.Count > 1) panel.Text($"Выбрано объектов: {_group.Count}. ⌘D дублирует все, перетаскивание двигает все.", muted: true);
        if (parameters["catalogId"] is JsonValue catalogId)
        {
            var entry = _catalog.OfType<JsonObject>().FirstOrDefault(item => (string)item["id"]! == (string)catalogId!);
            panel.Text($"Модель: {(string?)entry?["name"] ?? (string)catalogId!}", muted: true);
        }

        if (parameters["position"] is JsonArray position)
        {
            var x = (float)(double)position[0]!;
            var z = (float)(double)position[2]!;
            panel.NumberField("Положение X, м", x, value => MoveEntity(id, value, z, "положение X"));
            panel.NumberField("Положение Z, м", z, value => MoveEntity(id, x, value, "положение Z"));
        }

        if (parameters["yawDegrees"] is JsonValue yaw)
        {
            panel.NumberField("Поворот, °", (float)(double)yaw, value => studio.Session.SetField(id, ["params", "yawDegrees"], Math.Round(value, 1), "поворот"));
        }

        if (parameters["scale"] is JsonValue scale)
        {
            panel.NumberField("Масштаб", (float)(double)scale, value => studio.Session.SetField(id, ["params", "scale"], Math.Round(Math.Clamp(value, .1, 20), 3), "масштаб"));
        }

        if ((string?)entity["kind"] == "scatter")
        {
            panel.NumberField("Радиус, м", (float)(double)parameters["radius"]!, value => studio.Session.SetField(id, ["params", "radius"], Math.Round(Math.Clamp(value, .5, 60), 2), "радиус посадок"));
            panel.NumberField("Густота, на м²", (float)(double)parameters["density"]!, value => studio.Session.SetField(id, ["params", "density"], Math.Round(Math.Clamp(value, .001, 1), 4), "густота посадок"));
            panel.NumberField("Размер от", (float)(double)parameters["scaleMin"]!, value => studio.Session.SetField(id, ["params", "scaleMin"], Math.Round(value, 2), "размер посадок"));
            panel.NumberField("Размер до", (float)(double)parameters["scaleMax"]!, value => studio.Session.SetField(id, ["params", "scaleMax"], Math.Round(value, 2), "размер посадок"));
            panel.Text("Породы: " + string.Join(", ", parameters["catalogIds"]!.AsArray().Select(model => _catalog.OfType<JsonObject>().FirstOrDefault(entry => (string)entry["id"]! == (string)model!)?["name"]?.ToString() ?? (string)model!)), muted: true);
            panel.Buttons(("Перемешать", () => studio.Session.SetField(id, ["params", "seed"], System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue), "перемешать посадки")));
            if (PreviewNode(id) is { } planted && planted.HasMeta("scatterKept"))
            {
                panel.Text($"Посажено: {(int)planted.GetMeta("scatterKept")}. На дорогах и проходах не посажено: {(int)planted.GetMeta("scatterSkippedOnPassages")}.", muted: true);
            }
        }

        if ((string?)entity["kind"] == "kit-placement")
        {
            panel.Text("Это место в деревне раньше задавал код сборки. Теперь положение, поворот, масштаб и видимость хранятся в данных и принадлежат вам; код только предлагает исходный вариант.", muted: true);
            if (parameters["scale"] is JsonArray kitScale && (double)kitScale[0]! == (double)kitScale[1]! && (double)kitScale[1]! == (double)kitScale[2]!)
            {
                panel.NumberField("Масштаб", (float)(double)kitScale[0]!, value =>
                {
                    var v = Math.Round(Math.Clamp(value, .1, 20), 3);
                    studio.Session.SetField(id, ["params", "scale"], new JsonArray(v, v, v), "масштаб");
                });
            }

            var hidden = new CheckBox { Text = "Убрать из деревни (скрыть)", ButtonPressed = (bool?)parameters["hidden"] ?? false };
            hidden.Toggled += on => studio.Session.SetField(id, ["params", "hidden"], on, on ? "убрать из деревни" : "вернуть в деревню");
            panel.AddChild(hidden);
            panel.Text("Стены и твёрдость объекта едут вместе с ним. Срезы рельефа, дверные проходы и дорожки, которые код строил под исходное место, за объектом пока не следуют — после перемещения проверьте проход в игре.", muted: true);
        }

        if (parameters["collision"] is JsonValue collision)
        {
            var solid = new CheckBox { Text = "Твёрдый (с коллизией)", ButtonPressed = (string)collision! != "none" };
            solid.Toggled += on => studio.Session.SetField(id, ["params", "collision"], on ? "box" : "none", "коллизия");
            panel.AddChild(solid);
        }

        if ((string?)entity["kind"] == "trigger" && parameters["size"] is JsonArray size)
        {
            panel.Text("Область срабатывает только на игрока.", muted: true);
            for (var axis = 0; axis < 3; axis++)
            {
                var index = axis;
                panel.NumberField(new[] { "Ширина, м", "Высота, м", "Глубина, м" }[axis], (float)(double)size[axis]!, value =>
                {
                    var next = (JsonArray)size.DeepClone();
                    next[index] = Math.Round(Math.Max(.2, value), 2);
                    studio.Session.SetField(id, ["params", "size"], next, "размер области");
                });
            }

            var inside = new CheckBox { Text = "Если игрок уже внутри, когда это стало важно, — считать сразу", ButtonPressed = (bool?)parameters["countIfInside"] ?? false };
            inside.Toggled += on => studio.Session.SetField(id, ["params", "countIfInside"], on, "учёт уже вошедшего");
            panel.AddChild(inside);
        }

        if ((string?)entity["note"] is { Length: > 0 } note)
        {
            panel.Text(note, muted: true);
        }

        panel.Buttons(("◎ На карте", FocusSelection), ("▶ Играть отсюда", studio.PlayFromHere));
        StoryLinks(panel, id, parameters);
        if (IsGeneric(id))
        {
            StatesEditor(panel, id, parameters);
            if ((string?)entity["kind"] == "npc") RoutineEditor(panel, id, parameters);
            panel.Buttons(("Удалить объект…", () => ConfirmDelete(id)));
        }

        var advanced = panel.Advanced();
        advanced.AddChild(new Label { Text = $"Файл: {summary.RelativePath}", ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.Arbitrary });
        foreach (var interaction in InteractionIds(parameters))
        {
            advanced.AddChild(new Label { Text = $"Взаимодействие: {interaction}", ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.Arbitrary });
        }
    }

    private static IEnumerable<string> InteractionIds(JsonObject parameters)
    {
        if (parameters["interactionId"] is JsonValue direct) yield return (string)direct!;
        if (parameters["talk"]?["interactionId"] is JsonValue talk) yield return (string)talk!;
    }

    /// <summary>What this object changes in the story and which quest steps wait for it (spec TRIG03, A06).</summary>
    public IReadOnlyList<(string Text, string Id)> StoryLinkList(string id)
    {
        var entity = studio.Workspace.Get(id) as JsonObject;
        var parameters = entity?["params"] as JsonObject ?? [];
        var facts = new FactIndex(studio.Workspace);
        var phrases = new ConditionPhrases(studio.Catalog);
        var written = InteractionIds(parameters).SelectMany(facts.WrittenBy).ToList();
        if (parameters["talk"]?["dialogueId"] is JsonValue dialogue)
        {
            written.AddRange(FactsWrittenByEntity(facts, (string)dialogue!));
        }

        var links = new List<(string, string)>();
        foreach (var fact in written.Distinct())
        {
            var label = phrases.Describe(new JsonObject { ["op"] = "npc.state", ["characterId"] = fact.CharacterId, ["stateKey"] = fact.Key, ["value"] = true }).Replace(" = да", "");
            foreach (var reader in facts.Uses(fact).Where(use => !use.Writes).Select(use => use.EntityId).Distinct())
            {
                links.Add(($"{studio.Catalog.Describe(reader).KindLabel} «{studio.Catalog.NameOf(reader)}» ждёт: {label}", reader));
            }
        }

        if (parameters["states"] is JsonArray states)
        {
            foreach (var state in states.OfType<JsonObject>())
            {
                foreach (var fact in FactsIn(state["when"]))
                {
                    foreach (var writer in facts.Uses(fact).Where(use => use.Writes).Select(use => use.EntityId).Distinct())
                    {
                        links.Add(($"Состояние «{state["name"]}» наступает из: {studio.Catalog.Describe(writer).KindLabel} «{studio.Catalog.NameOf(writer)}»", writer));
                    }
                }
            }
        }

        var references = new ReferenceIndex(studio.Workspace);
        foreach (var interaction in InteractionIds(parameters))
        {
            links.AddRange(references.UsedBy(interaction).Where(user => user != id).Select(user => ($"{studio.Catalog.Describe(user).KindLabel} «{studio.Catalog.NameOf(user)}»", user)));
        }

        return links.Distinct().ToArray();
    }

    private static IEnumerable<StoryFact> FactsWrittenByEntity(FactIndex facts, string entityId) =>
        facts.WrittenBy(entityId);

    private void StoryLinks(StudioProperties panel, string id, JsonObject parameters) =>
        panel.Links("Связи с историей", StoryLinkList(id));

    private static IEnumerable<StoryFact> FactsIn(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj when (string?)(obj["op"] as JsonValue) is "npc.state":
                yield return new StoryFact((string)obj["characterId"]!, (string)obj["stateKey"]!);
                break;
            case JsonObject obj:
                foreach (var (_, child) in obj) foreach (var fact in FactsIn(child)) yield return fact;
                break;
            case JsonArray array:
                foreach (var child in array) foreach (var fact in FactsIn(child)) yield return fact;
                break;
        }
    }

    /// <summary>Named story states of a placed object (spec STATE01): when, visible, solid.</summary>
    private void StatesEditor(StudioProperties panel, string id, JsonObject parameters)
    {
        panel.AddChild(new Label { Text = "Сюжетные состояния", ThemeTypeVariation = "HeaderLabel" });
        panel.Text("Сверху вниз: применяется первое состояние, условие которого выполнено; иначе — обычный вид.", muted: true);
        var states = parameters["states"] as JsonArray ?? [];
        void Save(JsonArray next, string label) => studio.Session.SetField(id, ["params", "states"], next, label);
        for (var index = 0; index < states.Count; index++)
        {
            var at = index;
            var state = states[index]!.AsObject();
            var card = new PanelContainer { ThemeTypeVariation = "CardPanel" };
            var box = new VBoxContainer();
            card.AddChild(box);
            var name = new LineEdit { Text = (string?)state["name"] ?? "" };
            void Rename(string text)
            {
                if (text == (string?)state["name"]) return;
                var next = (JsonArray)states.DeepClone();
                next[at]!["name"] = text;
                Save(next, "название состояния");
            }

            name.TextSubmitted += Rename;
            name.FocusExited += () => Rename(name.Text);
            box.AddChild(name);
            box.AddChild(new Label { Text = "Когда:", ThemeTypeVariation = "MutedLabel" });
            box.AddChild(new StudioRuleEditor(studio, state["when"] as JsonArray ?? [], effects: false, rules =>
            {
                var next = (JsonArray)states.DeepClone();
                next[at]!["when"] = rules;
                Save(next, "условие состояния");
            }));
            var visible = new CheckBox { Text = "Виден", ButtonPressed = (bool?)state["visible"] ?? true };
            visible.Toggled += on => { var next = (JsonArray)states.DeepClone(); next[at]!["visible"] = on; Save(next, "видимость"); };
            var solid = new CheckBox { Text = "Твёрдый", ButtonPressed = (bool?)state["collision"] ?? true };
            solid.Toggled += on => { var next = (JsonArray)states.DeepClone(); next[at]!["collision"] = on; Save(next, "коллизия состояния"); };
            var row = new HBoxContainer();
            row.AddChild(visible);
            row.AddChild(solid);
            StudioRoot.Button(row, "Убрать", () => { var next = (JsonArray)states.DeepClone(); next.RemoveAt(at); Save(next, "убрать состояние"); });
            box.AddChild(row);
            panel.AddChild(card);
        }

        panel.Buttons(("+ Состояние", () =>
        {
            var next = (JsonArray)states.DeepClone();
            next.Add(new JsonObject
            {
                ["id"] = $"s-{Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(3))}",
                ["name"] = "Новое состояние", ["when"] = new JsonArray(), ["visible"] = true, ["collision"] = true
            });
            Save(next, "новое состояние");
        }));
    }

    /// <summary>Deleting shows every link it affects first (spec UX10).</summary>
    private void ConfirmDelete(string id)
    {
        var references = new ReferenceIndex(studio.Workspace).UsedBy(id).Select(user => $"• {studio.Catalog.Describe(user).KindLabel} «{studio.Catalog.NameOf(user)}»").ToArray();
        var dialog = new ConfirmationDialog
        {
            Title = "Удалить объект",
            DialogText = $"Удалить «{studio.Catalog.NameOf(id)}»?\n" + (references.Length == 0 ? "Связей нет." : "Затронутые связи:\n" + string.Join("\n", references)) + "\n\nДействие можно отменить (⌘Z).",
            OkButtonText = "Удалить",
            CancelButtonText = "Оставить"
        };
        dialog.Confirmed += () =>
        {
            var address = studio.Workspace.Locate(id)!;
            studio.Session.Delete(address.RelativePath, address.Key, $"удалить «{studio.Catalog.NameOf(id)}»");
            studio.Select(null);
        };
        studio.AddChild(dialog);
        dialog.PopupCentered();
    }
}
