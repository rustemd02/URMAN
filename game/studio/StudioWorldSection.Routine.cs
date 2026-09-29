using System.Text.Json.Nodes;
using Godot;
using Urman.Godot;
using Urman.Studio.Core.Scenes;

namespace Urman.Studio.App;

/// <summary>
/// A character's routine (spec NPC02, NPC03): blocks top to bottom, each with a
/// story condition ("когда"), a place and facing, an activity from the
/// animation library, a route drawn by clicking points in the world, and what
/// happens if the route is blocked. The selected character's routes are drawn
/// on the map; problems found without running the game are listed in words.
/// </summary>
public sealed partial class StudioWorldSection
{
    private Action<Vector3>? _pointPick;
    private Node3D? _routineOverlay;

    private static readonly (string Value, string Text)[] BlockedPolicies =
    [
        ("go-directly", "сразу оказаться на месте (история не ждёт)"),
        ("stay", "остаться, где стоит, и сообщить"),
        ("safe-point", "уйти в безопасную точку")
    ];

    private void RoutineEditor(StudioProperties panel, string id, JsonObject parameters)
    {
        panel.AddChild(new Label { Text = "Распорядок", ThemeTypeVariation = "HeaderLabel" });
        panel.Text("Часов в Акте I нет: «время» блока — состояние истории (сюжетный момент, квест). Сверху вниз: персонаж там, где первый блок с выполненным условием. Сцена может временно занять персонажа — распорядок уступит и продолжится после неё.", muted: true);
        var live = new CheckBox { Text = "Оживить распорядки в предпросмотре", ButtonPressed = Director?.RoutinesLive ?? false, TooltipText = "Персонажи пойдут по своим путям прямо в окне Studio; в игре распорядок работает всегда" };
        live.Toggled += SetRoutinesLive;
        panel.AddChild(live);
        var blocks = parameters["schedule"] as JsonArray ?? [];
        void Save(JsonArray next, string label) => studio.Session.SetField(id, ["params", "schedule"], next, label);
        JsonArray Copy() => (JsonArray)blocks.DeepClone();
        var motions = AnimationCatalog.Entries.Keys.Order(StringComparer.Ordinal).ToArray();
        for (var index = 0; index < blocks.Count; index++)
        {
            var at = index;
            var block = blocks[index]!.AsObject();
            var card = new PanelContainer { ThemeTypeVariation = "CardPanel" };
            var box = new VBoxContainer();
            card.AddChild(box);
            var name = new LineEdit { Text = (string?)block["name"] ?? "" };
            void Rename(string text)
            {
                if (text == (string?)block["name"]) return;
                var next = Copy();
                next[at]!["name"] = text;
                Save(next, "название блока");
            }

            name.TextSubmitted += Rename;
            name.FocusExited += () => Rename(name.Text);
            box.AddChild(name);
            box.AddChild(new Label { Text = "Когда (пусто — всегда):", ThemeTypeVariation = "MutedLabel" });
            box.AddChild(new StudioRuleEditor(studio, block["when"] as JsonArray ?? [], effects: false, rules =>
            {
                var next = Copy();
                next[at]!["when"] = rules;
                Save(next, "условие блока");
            }));

            var place = block["place"] as JsonArray;
            box.AddChild(new Label
            {
                Text = place is { Count: 3 } ? $"Место: x {(double)place[0]!:0.#}, z {(double)place[2]!:0.#}" : "Место не задано",
                ThemeTypeVariation = "MutedLabel"
            });
            var placeRow = new HBoxContainer();
            StudioRoot.Button(placeRow, "Место — щёлкнуть на карте", () => StartPointPick("Щёлкните место блока на земле. Esc — отмена.", point =>
            {
                var next = Copy();
                next[at]!["place"] = Point(point);
                EndPointPick();
                Save(next, "место блока");
            }));
            StudioRoot.Button(placeRow, "Здесь, где стоит", () =>
            {
                if (studio.Workspace.Get(id)?["params"]?["position"] is not JsonArray position) return;
                var next = Copy();
                next[at]!["place"] = position.DeepClone();
                Save(next, "место блока");
            });
            box.AddChild(placeRow);

            var yaw = new SpinBox { MinValue = -180, MaxValue = 180, Step = 5, Value = (double?)block["yawDegrees"] ?? 0, Suffix = "°", TooltipText = "Куда смотрит на месте" };
            yaw.ValueChanged += value => { var next = Copy(); next[at]!["yawDegrees"] = value; Save(next, "поворот на месте"); };
            box.AddChild(Row("Смотрит:", yaw));

            var activity = new OptionButton();
            var current = (string?)block["motion"] ?? "urman.anim:idle";
            foreach (var motion in motions)
            {
                activity.AddItem(AnimationName(motion));
                activity.SetItemMetadata(activity.ItemCount - 1, motion);
                if (motion == current) activity.Select(activity.ItemCount - 1);
            }

            if (!motions.Contains(current))
            {
                activity.AddItem($"⚠ нет в библиотеке: {current}");
                activity.Select(activity.ItemCount - 1);
            }

            activity.ItemSelected += item =>
            {
                if (activity.GetItemMetadata((int)item).AsString() is not { Length: > 0 } motion) return;
                var next = Copy();
                next[at]!["motion"] = motion;
                Save(next, "занятие");
            };
            box.AddChild(Row("Занятие:", activity));

            var escort = new CheckBox { Text = "Сопровождать игрока (вместо места)", ButtonPressed = block["follow"] is JsonObject };
            escort.Toggled += on =>
            {
                var next = Copy();
                if (on) next[at]!["follow"] = new JsonObject { ["distance"] = 2.5, ["loseMetres"] = 30.0 };
                else next[at]!.AsObject().Remove("follow");
                Save(next, on ? "сопровождать игрока" : "не сопровождать");
            };
            box.AddChild(escort);
            if (block["follow"] is JsonObject follow)
            {
                var keep = new SpinBox { MinValue = 1, MaxValue = 10, Step = .5, Value = (double?)follow["distance"] ?? 2.5, Suffix = "м" };
                keep.ValueChanged += value => { var next = Copy(); next[at]!["follow"]!["distance"] = value; Save(next, "дистанция сопровождения"); };
                box.AddChild(Row("Держаться в:", keep));
                var lose = new SpinBox { MinValue = 5, MaxValue = 200, Step = 5, Value = (double?)follow["loseMetres"] ?? 30, Suffix = "м" };
                lose.ValueChanged += value => { var next = Copy(); next[at]!["follow"]!["loseMetres"] = value; Save(next, "дальность отставания"); };
                box.AddChild(Row("Дальше — ждёт на месте:", lose));
            }

            var route = block["route"] as JsonArray ?? [];
            box.AddChild(new Label { Text = route.Count == 0 ? "Путь: напрямую" : $"Путь: {route.Count} точ. — рисуется на карте", ThemeTypeVariation = "MutedLabel" });
            for (var pointIndex = 0; pointIndex < route.Count; pointIndex++)
            {
                var pi = pointIndex;
                var point = route[pi] as JsonObject; // null for a bare [x, y, z] point
                var waitBox = new SpinBox { MinValue = 0, MaxValue = 120, Step = .5, Value = (double?)point?["waitSeconds"] ?? 0, Suffix = "с", TooltipText = "Сколько стоять в этой точке" };
                var look = new OptionButton { TooltipText = "Куда смотреть, пока стоит" };
                look.AddItem("по ходу");
                look.AddItem("на игрока");
                if ((string?)(point?["look"] as JsonValue) == "player") look.Select(1);
                void Update(double seconds, bool atPlayer)
                {
                    var next = Copy();
                    var old = next[at]!["route"]![pi]!;
                    var coordinates = old is JsonArray bare ? bare.DeepClone() : old["at"]!.DeepClone();
                    next[at]!["route"]![pi] = seconds <= 0 && !atPlayer ? coordinates
                        : new JsonObject { ["at"] = coordinates, ["waitSeconds"] = seconds, ["look"] = atPlayer ? "player" : null };
                    if (next[at]!["route"]![pi] is JsonObject written && written["look"] is null) written.Remove("look");
                    Save(next, "действие в точке пути");
                }

                waitBox.ValueChanged += value => Update(value, look.Selected == 1);
                look.ItemSelected += item => Update(waitBox.Value, item == 1);
                var pointRow = new HBoxContainer();
                pointRow.AddChild(new Label { Text = $"Точка {pi + 1}: ждать", ThemeTypeVariation = "MutedLabel" });
                pointRow.AddChild(waitBox);
                pointRow.AddChild(look);
                box.AddChild(pointRow);
            }

            var routeRow = new HBoxContainer();
            StudioRoot.Button(routeRow, route.Count == 0 ? "Нарисовать путь" : "Добавить точки", () =>
            {
                var drawn = (JsonArray)route.DeepClone();
                StartPointPick("Щёлкайте точки пути по порядку. Enter или Esc — готово.", point =>
                {
                    drawn.Add(Point(point));
                    var next = Copy();
                    next[at]!["route"] = drawn.DeepClone();
                    Save(next, "точка пути");
                });
            });
            if (route.Count > 0)
            {
                StudioRoot.Button(routeRow, "Убрать последнюю", () => { var next = Copy(); next[at]!["route"]!.AsArray().RemoveAt(route.Count - 1); Save(next, "убрать точку пути"); });
                StudioRoot.Button(routeRow, "Напрямую", () => { var next = Copy(); next[at]!.AsObject().Remove("route"); Save(next, "путь напрямую"); });
            }

            box.AddChild(routeRow);
            var blocked = new OptionButton();
            var policy = (string?)block["onBlocked"] ?? "go-directly";
            foreach (var (value, text) in BlockedPolicies)
            {
                blocked.AddItem(text);
                if (value == policy) blocked.Select(blocked.ItemCount - 1);
            }

            blocked.ItemSelected += item => { var next = Copy(); next[at]!["onBlocked"] = BlockedPolicies[(int)item].Value; Save(next, "если путь перекрыт"); };
            box.AddChild(Row("Если путь перекрыт:", blocked));
            if (policy == "safe-point")
            {
                var safe = block["safePoint"] as JsonArray;
                box.AddChild(new Label { Text = safe is { Count: 3 } ? $"Безопасная точка: x {(double)safe[0]!:0.#}, z {(double)safe[2]!:0.#}" : "⚠ Безопасная точка не задана — персонаж окажется сразу на месте", ThemeTypeVariation = "MutedLabel" });
                StudioRoot.Button(box, "Безопасная точка — щёлкнуть на карте", () => StartPointPick("Щёлкните безопасную точку. Esc — отмена.", point =>
                {
                    var next = Copy();
                    next[at]!["safePoint"] = Point(point);
                    EndPointPick();
                    Save(next, "безопасная точка");
                }));
            }

            var row = new HBoxContainer();
            if (at > 0) StudioRoot.Button(row, "↑", () => { var next = Copy(); var moved = next[at]!; next.RemoveAt(at); next.Insert(at - 1, moved); Save(next, "порядок блоков"); });
            StudioRoot.Button(row, "Убрать блок", () => { var next = Copy(); next.RemoveAt(at); Save(next, "убрать блок распорядка"); });
            box.AddChild(row);
            panel.AddChild(card);
        }

        panel.Buttons(("+ Блок распорядка", () =>
        {
            var next = Copy();
            var position = studio.Workspace.Get(id)?["params"]?["position"]?.DeepClone() ?? new JsonArray(0.0, 0.0, 0.0);
            next.Add(new JsonObject
            {
                ["id"] = $"r-{Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(3))}",
                ["name"] = blocks.Count == 0 ? "Обычно" : "Новый блок", ["when"] = new JsonArray(), ["place"] = position,
                ["motion"] = "urman.anim:idle", ["onBlocked"] = "go-directly"
            });
            Save(next, "новый блок распорядка");
        }));

        foreach (var note in RoutineCheck.Notes(studio.Workspace, studio.Catalog).Where(note => note.EntityId == id))
        {
            panel.Text((note.Problem ? "⚠ " : "• ") + note.Text, muted: !note.Problem);
        }

        if (Director?.RoutineStatus(id) is { Block: { } active } status)
        {
            var activeName = blocks.OfType<JsonObject>().FirstOrDefault(block => (string?)block["id"] == active)?["name"];
            panel.Text($"В предпросмотре сейчас: «{activeName}» — {(status.Walking ? "идёт" : status.Status)}", muted: true);
        }
    }

    public void SetRoutinesLive(bool on)
    {
        if (Director is { } director) director.RoutinesLive = on;
        studio.RefreshStatus(on ? "Распорядки идут в предпросмотре" : "Мир снова стоит");
    }

    private static HBoxContainer Row(string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, ThemeTypeVariation = "MutedLabel" });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(control);
        return row;
    }

    private static string AnimationName(string motion) =>
        AnimationCatalog.Entries.TryGetValue(motion, out var entry) && entry.TryGetProperty("name", out var name) ? name.GetString() ?? motion : motion;

    private static JsonArray Point(Vector3 point) => new(Math.Round(point.X, 2), 0.0, Math.Round(point.Z, 2));

    public void StartPointPick(string hint, Action<Vector3> pick)
    {
        if (_placing is not null) CancelPlacing();
        _pointPick = pick;
        _measureLabel.Text = hint;
        _measureLabel.Visible = true;
        _container.GrabFocus();
    }

    public void EndPointPick()
    {
        _pointPick = null;
        _measureLabel.Visible = false;
    }

    /// <summary>Test hook: what a click on the ground at this point would do in the current pick mode.</summary>
    public void PickPointForTest(Vector3 point) => _pointPick?.Invoke(point);

    // Routes of the selected character: one line per block, numbered places.
    private void DrawRoutine()
    {
        _routineOverlay?.QueueFree();
        _routineOverlay = null;
        if (studio.Selection is not { } id || studio.Workspace.Get(id) is not JsonObject { } entity || entity["params"]?["schedule"] is not JsonArray blocks) return;
        _routineOverlay = new Node3D { Name = "RoutineOverlay" };
        _markerRoot.AddChild(_routineOverlay);
        var start = Anchor(entity) ?? Vector3.Zero;
        var colors = new[] { new Color("ffe27a"), new Color("9fd3a7"), new Color("8fb8ff"), new Color("f0a3c8"), new Color("d7a66b") };
        for (var index = 0; index < blocks.Count; index++)
        {
            if (blocks[index] is not JsonObject block || block["place"] is not JsonArray place) continue;
            var color = colors[index % colors.Length];
            var material = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true, RenderPriority = 11 };
            var points = new List<Vector3> { start };
            points.AddRange((block["route"] as JsonArray ?? []).Select(point => point is JsonArray bare ? bare : point?["at"] as JsonArray).OfType<JsonArray>().Select(Lift));
            points.Add(Lift(place));
            for (var segment = 1; segment < points.Count; segment++)
            {
                var from = points[segment - 1] + Vector3.Up * .2f;
                var to = points[segment] + Vector3.Up * .2f;
                if (from.DistanceTo(to) < .05f) continue;
                // A flat band along the ground, readable from above and at eye level.
                _routineOverlay.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(.18f, .06f, from.DistanceTo(to)), Material = material },
                    Transform = new Transform3D(Basis.LookingAt(to - from, Vector3.Up), (from + to) / 2)
                });
                _routineOverlay.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = .18f, Height = .36f, Material = material }, Position = to });
            }

            _routineOverlay.AddChild(new Label3D
            {
                Text = $"{index + 1} · {(string?)block["name"]}",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true, FontSize = 30, OutlineSize = 8, Modulate = color, PixelSize = .006f,
                Position = points[^1] + Vector3.Up * 1.1f
            });
        }
    }

    private static Vector3 Lift(JsonArray point)
    {
        var x = (float)(double)point[0]!;
        var z = (float)(double)point[2]!;
        return new Vector3(x, Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(x, z), z);
    }
}
