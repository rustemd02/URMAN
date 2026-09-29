using System.Text.Json.Nodes;
using Godot;
using Urman.Godot;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.App;

/// <summary>
/// Characters and the animation catalogue (spec NPC01, ANIM01–ANIM05): a
/// character card with its model, where it stands on the map and which
/// dialogues use it; a live 3D preview of the real kit character — clothes
/// included — playing the chosen motion; the motion catalogue by category
/// with compatibility, speed and loop. Motions come from the prepared library;
/// composing bone keys by hand is not part of Studio (U08).
/// </summary>
public sealed class StudioCharacterSection(StudioRoot studio) : IStudioSection
{
    public const string CatalogPath = "game/content/animations/catalog.v1.json";
    public static readonly string[] KitPrefixes = ["Resident", "Alsu", "Gulsina", "Mansur", "Naila", "Rinat", "TimurHazrat"];
    private Control? _view;
    private ItemList _characters = null!;
    private OptionButton _model = null!;
    private ItemList _motions = null!;
    private SubViewport _viewport = null!;
    private Node3D _stage = null!;
    private Node3D? _figure;
    private Label _status = null!;
    private string? _characterId;
    private string _prefix = "Resident";
    private string? _motionId;
    private float _turn;

    public string Key => "characters";
    public string Title => "Персонажи";
    public Control View => _view ??= Build();

    private Control Build()
    {
        var split = new HSplitContainer { SplitOffsets = [300] };
        var left = new VBoxContainer();
        left.AddChild(new Label { Text = "Персонажи", ThemeTypeVariation = "HeaderLabel" });
        _characters = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _characters.ItemSelected += index => SelectCharacter((string)_characters.GetItemMetadata((int)index));
        left.AddChild(_characters);
        split.AddChild(left);

        var centre = new HSplitContainer { SplitOffsets = [520] };
        var previewBox = new VBoxContainer();
        var bar = new HBoxContainer();
        bar.AddChild(new Label { Text = "Модель:" });
        _model = new OptionButton();
        foreach (var prefix in KitPrefixes) _model.AddItem(prefix);
        _model.ItemSelected += index => { _prefix = KitPrefixes[index]; SpawnFigure(); };
        bar.AddChild(_model);
        StudioRoot.Button(bar, "⟲", () => { _turn += 45; if (_figure is not null) _figure.RotationDegrees = new Vector3(0, _turn, 0); }).TooltipText = "Повернуть предпросмотр";
        previewBox.AddChild(bar);
        var container = new SubViewportContainer { Stretch = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _viewport = new SubViewport { OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa2X };
        container.AddChild(_viewport);
        previewBox.AddChild(container);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        previewBox.AddChild(_status);
        centre.AddChild(previewBox);

        var motionsBox = new VBoxContainer();
        motionsBox.AddChild(new Label { Text = "Каталог движений", ThemeTypeVariation = "HeaderLabel" });
        motionsBox.AddChild(new Label { Text = "Выберите движение — персонаж слева проиграет его в одежде.", ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _motions = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _motions.ItemSelected += index => PlayMotion((string)_motions.GetItemMetadata((int)index));
        motionsBox.AddChild(_motions);
        centre.AddChild(motionsBox);
        split.AddChild(centre);

        BuildStage();
        Refresh();
        SpawnFigure();
        return split;
    }

    private void BuildStage()
    {
        var environment = new global::Godot.Environment
        {
            BackgroundMode = global::Godot.Environment.BGMode.Color, BackgroundColor = new Color("2a3038"),
            AmbientLightSource = global::Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("c9d3de"), AmbientLightEnergy = .8f
        };
        _viewport.AddChild(new WorldEnvironment { Environment = environment });
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40, 30, 0), LightEnergy = 1.3f });
        _viewport.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(6, 6) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("dfe6ee") } });
        var camera = new Camera3D { Position = new Vector3(0, 1.2f, 3.6f), Fov = 40 };
        _viewport.AddChild(camera);
        camera.LookAtFromPosition(camera.Position, new Vector3(0, .95f, 0));
        camera.MakeCurrent();
        _stage = new Node3D { Name = "Stage" };
        _viewport.AddChild(_stage);
    }

    private void SpawnFigure()
    {
        if (_stage is null) return;
        _figure?.QueueFree();
        _figure = GeneratedCharacterKitDressing.Attach(_stage, _characterId ?? "studio-preview", _prefix, Vector3.Zero);
        _figure.RotationDegrees = new Vector3(0, _turn, 0);
        if (_motionId is not null) PlayMotion(_motionId); else _status.Text = $"Модель {_prefix}: стойка.";
    }

    public AnimationPlayResult PlayMotion(string motionId)
    {
        _motionId = motionId;
        AnimationCatalog.Reload(studio.Workspace.File(CatalogPath).Text);
        if (_figure is null) return new(false, "", "Нет персонажа для предпросмотра.");
        var result = AnimationCatalog.Play(_figure, motionId);
        var p = studio.Workspace.Get(motionId)?["params"];
        var clip = (string?)p?["source"]?["clip"];
        var compatibility = clip is null ? "клип кита персонажа" : AnimationCatalog.Compatibility(_figure, clip);
        _status.Text = result.Played
            ? $"«{studio.Catalog.NameOf(motionId)}» на модели {_prefix}: {compatibility}." + (result.Problem.Length > 0 ? " " + result.Problem : "")
            : $"Не проигрывается: {result.Problem}";
        _status.Modulate = result.Played ? StudioTheme.Text : StudioTheme.Bad;
        studio.Select(motionId);
        return result;
    }

    public Node3D? Figure => _figure;

    public void Refresh()
    {
        if (_characters is null) return;
        _characters.Clear();
        foreach (var id in studio.Workspace.EntityIds.Where(id => EntityCatalog.KindOf(id, null) == "character").OrderBy(id => studio.Catalog.NameOf(id), StringComparer.CurrentCulture))
        {
            _characters.AddItem(studio.Catalog.NameOf(id));
            _characters.SetItemMetadata(_characters.ItemCount - 1, id);
            if (id == _characterId) _characters.Select(_characters.ItemCount - 1);
        }

        _motions.Clear();
        var motions = studio.Workspace.EntityIds.Where(id => id.StartsWith("urman.anim:", StringComparison.Ordinal))
            .Select(id => (id, entity: studio.Workspace.Get(id)!)).OrderBy(item => (string?)item.entity["params"]?["category"]).ThenBy(item => (string?)item.entity["name"]);
        string? category = null;
        foreach (var (id, entity) in motions)
        {
            var current = (string?)entity["params"]?["category"] ?? "";
            if (current != category)
            {
                _motions.AddItem($"— {current} —");
                _motions.SetItemDisabled(_motions.ItemCount - 1, true);
                _motions.SetItemMetadata(_motions.ItemCount - 1, "");
                category = current;
            }

            var procedural = entity["params"]?["source"]?["procedural"] is not null;
            var variant = entity["params"]?["note"] is not null;
            _motions.AddItem($"  {entity["name"]}{(procedural ? "  (без клипа)" : variant ? "  (вариант)" : "")}");
            _motions.SetItemMetadata(_motions.ItemCount - 1, id);
            if (id == _motionId) _motions.Select(_motions.ItemCount - 1);
        }
    }

    private void SelectCharacter(string id)
    {
        _characterId = id;
        // A character placed in the world names its kit model; use it for the preview.
        var placed = studio.Workspace.EntityIds.Select(entity => studio.Workspace.Get(entity)?["params"])
            .FirstOrDefault(p => (string?)p?["characterId"] == id);
        if ((string?)placed?["kitPrefix"] is { } prefix && KitPrefixes.Contains(prefix))
        {
            _prefix = prefix;
            _model.Selected = Array.IndexOf(KitPrefixes, prefix);
        }

        SpawnFigure();
        studio.Select(id);
    }

    public bool Reveal(string id)
    {
        if (EntityCatalog.KindOf(id, null) != "character" && !id.StartsWith("urman.anim:", StringComparison.Ordinal)) return false;
        if (_view is not null)
        {
            if (id.StartsWith("urman.anim:", StringComparison.Ordinal)) PlayMotion(id); else SelectCharacter(id);
        }
        else if (id.StartsWith("urman.anim:", StringComparison.Ordinal)) _motionId = id;
        else _characterId = id;

        return true;
    }

    public void FillProperties(StudioProperties panel)
    {
        if (studio.Selection is { } motion && motion.StartsWith("urman.anim:", StringComparison.Ordinal) && studio.Workspace.Get(motion) is JsonObject entity)
        {
            var p = entity["params"]!.AsObject();
            panel.Header((string?)entity["name"] ?? motion, $"Движение · {p["category"]}");
            panel.IdLine(motion);
            panel.TextField("Название", (string?)entity["name"] ?? "", text => studio.Session.SetField(motion, ["name"], text, "название движения"));
            panel.NumberField("Скорость", (float)(double)p["speed"]!, value => studio.Session.SetField(motion, ["params", "speed"], Math.Round(Math.Clamp(value, .2, 3), 2), "скорость движения"));
            var loop = new CheckBox { Text = "Повторять по кругу", ButtonPressed = (bool?)p["loop"] ?? false };
            loop.Toggled += on => studio.Session.SetField(motion, ["params", "loop"], on, "петля движения");
            panel.AddChild(loop);
            var source = p["source"]!;
            panel.Text(source["kit"] is not null ? $"Клип кита персонажа «{source["kit"]}»." : source["clip"] is not null ? $"Клип библиотеки Quaternius UAL (CC0): {source["clip"]}." : "Процедурное движение без клипа.", muted: true);
            if ((string?)p["note"] is { } note) panel.Text(note, muted: true);
            panel.Text("Перемещение по миру задаёт владелец движения (маршрут, сцена); корневое смещение клипа отключено, чтобы не было двойного хода и скольжения.", muted: true);
            panel.Links("Где используется", new ReferenceIndex(studio.Workspace).UsedBy(motion).Select(user => ($"{studio.Catalog.Describe(user).KindLabel} «{studio.Catalog.NameOf(user)}»", user)));
            return;
        }

        if (_characterId is not { } id || studio.Workspace.Get(id) is not JsonObject character)
        {
            panel.Header("Персонаж не выбран", "Персонажи");
            panel.Text("Слева — персонажи истории. Справа — каталог движений: выберите движение, чтобы увидеть его на модели.", muted: true);
            return;
        }

        panel.Header(studio.Catalog.NameOf(id), "Персонаж");
        panel.IdLine(id);
        panel.TextField("Имя", studio.Catalog.NameOf(id), text =>
        {
            var address = studio.Workspace.Locate(id)!;
            var edited = studio.Workspace.Get(id)!.AsObject();
            edited["displayName"] = new JsonObject { ["default"] = text, ["translations"] = new JsonObject { ["ru"] = text } };
            studio.Session.Set(address.RelativePath, address.Key, edited, "имя персонажа");
        });
        if (character["accessibilityDescription"]?["default"] is JsonValue description) panel.Text((string)description!, muted: true);
        panel.Text("Роль: " + string.Join(", ", (character["roleTags"] as JsonArray ?? []).Select(tag => (string?)tag)), muted: true);
        var onMap = studio.Workspace.EntityIds.Where(entity => (string?)studio.Workspace.Get(entity)?["params"]?["characterId"] == id)
            .Select(entity => ($"На карте: «{studio.Catalog.NameOf(entity)}»", entity));
        var users = new ReferenceIndex(studio.Workspace).UsedBy(id).Select(user => ($"{studio.Catalog.Describe(user).KindLabel} «{studio.Catalog.NameOf(user)}»", user));
        panel.Links("Где встречается", onMap.Concat(users).Distinct());
        panel.Text("Расписание, маршруты и реакции персонажа задаются у его точки на карте (этап P3, в работе).", muted: true);
    }
}
