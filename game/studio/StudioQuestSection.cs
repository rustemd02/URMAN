using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Quests;

namespace Urman.Studio.App;

/// <summary>
/// Quests: the campaign's quests on the left, the selected quest as a list or
/// a graph in the centre. Both views are projections of the one stage and
/// transition model the game executes (QuestFlow); every structural edit goes
/// through QuestFlowEditor, so switching views never flattens a branch.
/// Block positions in the graph are the author's personal layout and are kept
/// out of the quest data (spec DATA07).
/// </summary>
public sealed class StudioQuestSection(StudioRoot studio) : IStudioSection
{
    private Control? _view;
    private ItemList _quests = null!;
    private Tree _tree = null!;
    private GraphEdit _graph = null!;
    private Button _listButton = null!;
    private Button _graphButton = null!;
    private Label _title = null!;
    private string? _questId;
    private string? _stageId;
    private bool _showGraph;
    private readonly StudioLayout _layout = new(studio.Workspace.Root);

    public string Key => "quests";
    public string Title => "Квесты";
    public Control View => _view ??= Build();

    private Control Build()
    {
        var split = new HSplitContainer { SplitOffsets = [320] };
        var left = new VBoxContainer();
        left.AddChild(new Label { Text = "Квесты кампании", ThemeTypeVariation = "HeaderLabel" });
        left.AddChild(new Label { Text = "Порядок в этом списке не влияет на игру.", ThemeTypeVariation = "MutedLabel" });
        StudioRoot.Button(left, "+ Новый квест из шаблона…", OpenTemplateForm).ThemeTypeVariation = "PrimaryButton";
        _quests = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _quests.ItemSelected += index => OpenQuest((string)_quests.GetItemMetadata((int)index));
        left.AddChild(_quests);
        split.AddChild(left);

        var right = new VBoxContainer();
        var bar = new HBoxContainer();
        _title = new Label { ThemeTypeVariation = "HeaderLabel", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        bar.AddChild(_title);
        _listButton = StudioRoot.Button(bar, "☰ Список", () => { _showGraph = false; ShowView(); });
        _graphButton = StudioRoot.Button(bar, "◇ Граф", () => { _showGraph = true; ShowView(); });
        right.AddChild(bar);
        right.AddChild(new Label { Text = "Список и граф показывают одну и ту же логику. Переключение не меняет ветки, группы и объединения.", ThemeTypeVariation = "MutedLabel" });

        _tree = new Tree { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HideRoot = true };
        _tree.ItemSelected += () => SelectStage(_tree.GetSelected()?.GetMetadata(0).AsString());
        right.AddChild(_tree);
        _graph = new GraphEdit { SizeFlagsVertical = Control.SizeFlags.ExpandFill, Visible = false, RightDisconnects = false, ShowGrid = true };
        _graph.NodeSelected += node => SelectStage(node.Name.ToString().Replace("·", "/"));
        _graph.EndNodeMove += SaveLayout;
        right.AddChild(_graph);
        split.AddChild(right);
        Refresh();
        return split;
    }

    public void Refresh()
    {
        if (_view is null && _quests is null)
        {
            return;
        }

        var selectedQuest = _questId;
        _quests.Clear();
        foreach (var id in QuestIds())
        {
            _quests.AddItem(studio.Catalog.NameOf(id));
            _quests.SetItemMetadata(_quests.ItemCount - 1, id);
            _quests.SetItemTooltip(_quests.ItemCount - 1, id);
            if (id == selectedQuest)
            {
                _quests.Select(_quests.ItemCount - 1);
            }
        }

        if (selectedQuest is null && _quests.ItemCount > 0)
        {
            var tamara = QuestIds().FirstOrDefault(id => id.Contains("tamara", StringComparison.Ordinal)) ?? QuestIds().First();
            OpenQuest(tamara);
            return;
        }

        ShowView();
    }

    private IEnumerable<string> QuestIds() =>
        studio.Workspace.EntityIds.Where(id => EntityCatalog.KindOf(id, null) == "quest").Order(StringComparer.Ordinal);

    private void OpenQuest(string id)
    {
        _questId = id;
        _stageId = null;
        studio.Select(id);
        ShowView();
    }

    private JsonObject? Quest() => _questId is null ? null : studio.Workspace.Get(_questId) as JsonObject;

    public void ShowGraph(bool graph)
    {
        _showGraph = graph;
        ShowView();
    }

    private void ShowView()
    {
        _tree.Visible = !_showGraph;
        _graph.Visible = _showGraph;
        _listButton.ThemeTypeVariation = _showGraph ? "" : "PrimaryButton";
        _graphButton.ThemeTypeVariation = _showGraph ? "PrimaryButton" : "";
        if (Quest() is not { } quest)
        {
            _title.Text = "Выберите квест";
            return;
        }

        _title.Text = studio.Catalog.NameOf(_questId!);
        var flow = new QuestFlow(quest);
        if (_showGraph) BuildGraph(flow); else BuildTree(flow);
    }

    // ---- list view --------------------------------------------------------------------

    private void BuildTree(QuestFlow flow)
    {
        _tree.Clear();
        var root = _tree.CreateItem();
        AddItems(root, flow, QuestOutline.Build(flow));
    }

    private void AddItems(TreeItem parent, QuestFlow flow, IReadOnlyList<OutlineItem> items)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case StageItem stage:
                    Row(parent, $"{StageKind(flow, stage.StageId)}  {StageName(flow, stage.StageId)}", stage.StageId);
                    break;
                case BranchItem branch:
                {
                    var row = Row(parent, $"◆ Выбор: {StageName(flow, branch.StageId)}", branch.StageId);
                    foreach (var arm in branch.Arms)
                    {
                        var armRow = Row(row, $"↳ {EdgeLabel(flow, arm.Edge)}", branch.StageId);
                        armRow.SetCustomColor(0, StudioTheme.Accent);
                        AddItems(armRow, flow, arm.Items);
                    }

                    if (branch.JoinStageId is not null)
                    {
                        Row(row, $"⤵ Ветки сходятся: {StageName(flow, branch.JoinStageId)}", branch.JoinStageId).SetCustomColor(0, StudioTheme.Muted);
                    }

                    break;
                }
                case EndItem end:
                    Row(parent, EndLabel(end.Target), _stageId ?? "").SetCustomColor(0, end.Target.End == "failed" ? StudioTheme.Warn : StudioTheme.Ok);
                    break;
                case JumpItem jump:
                    Row(parent, $"↺ Вернуться к «{StageName(flow, jump.StageId)}»", jump.StageId).SetCustomColor(0, StudioTheme.Violet);
                    break;
            }
        }
    }

    private TreeItem Row(TreeItem parent, string text, string stageId)
    {
        var item = _tree.CreateItem(parent);
        item.SetText(0, text);
        item.SetMetadata(0, stageId);
        if (stageId == _stageId) item.Select(0);
        return item;
    }

    // ---- graph view -------------------------------------------------------------------

    private void BuildGraph(QuestFlow flow)
    {
        _graph.ClearConnections();
        foreach (var child in _graph.GetChildren())
        {
            if (child is GraphNode node)
            {
                _graph.RemoveChild(node);
                node.QueueFree();
            }
        }

        var depth = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (stage, distance) in flow.Reachable(flow.StartStageId)) depth[stage] = distance;
        var rows = new Dictionary<int, int>();
        foreach (var stageId in flow.StageOrder)
        {
            var column = depth.GetValueOrDefault(stageId, flow.StageOrder.ToList().IndexOf(stageId));
            var row = rows[column] = rows.GetValueOrDefault(column) + 1;
            var node = new GraphNode { Name = NodeName(stageId), Title = StageName(flow, stageId), Resizable = false, CustomMinimumSize = new Vector2(280, 0) };
            node.AddChild(new Label { Text = StageKind(flow, stageId), ThemeTypeVariation = "MutedLabel" });
            node.SetSlot(0, true, 0, StudioTheme.Accent, true, 0, StudioTheme.Accent);
            node.PositionOffset = _layout.Get(_questId!, stageId) ?? new Vector2(40 + column * 340, 90 + (row - 1) * 150);
            node.Selected = stageId == _stageId;
            _graph.AddChild(node);
        }

        var ends = 0;
        foreach (var edge in flow.AllEdges())
        {
            var target = edge.Target.StageId;
            if (target is null)
            {
                var name = $"end·{edge.Target.End}·{edge.Target.OutcomeId ?? "main"}";
                if (_graph.GetNodeOrNull(name) is null)
                {
                    var endNode = new GraphNode { Name = name, Title = EndLabel(edge.Target) };
                    endNode.AddChild(new Label { Text = edge.Target.End == "failed" ? "провал" : "успех", ThemeTypeVariation = "MutedLabel" });
                    endNode.SetSlot(0, true, 0, StudioTheme.Ok, false, 0, StudioTheme.Ok);
                    var from = (GraphNode)_graph.GetNode(NodeName(edge.FromStageId));
                    endNode.PositionOffset = from.PositionOffset + new Vector2(from.Size.X + 80, 150 * ends++);
                    _graph.AddChild(endNode);
                }

                _graph.ConnectNode(NodeName(edge.FromStageId), 0, name, 0);
            }
            else
            {
                _graph.ConnectNode(NodeName(edge.FromStageId), 0, NodeName(target), 0);
            }
        }
    }

    private static string NodeName(string stageId) => stageId.Replace("/", "·");

    private void SaveLayout()
    {
        foreach (var node in _graph.GetChildren().OfType<GraphNode>())
        {
            if (!node.Name.ToString().StartsWith("end·", StringComparison.Ordinal))
            {
                _layout.Set(_questId!, node.Name.ToString().Replace("·", "/"), node.PositionOffset);
            }
        }

        _layout.Save();
    }

    // ---- selection and editing -------------------------------------------------------------

    private void SelectStage(string? stageId)
    {
        if (string.IsNullOrEmpty(stageId) || stageId.StartsWith("end", StringComparison.Ordinal))
        {
            return;
        }

        _stageId = stageId;
        studio.Select(_questId);
    }

    public bool Reveal(string id)
    {
        if (EntityCatalog.KindOf(id, null) != "quest" || studio.Workspace.Get(id) is null)
        {
            return false;
        }

        if (_view is not null) OpenQuest(id); else _questId = id;
        return true;
    }

    public void FillProperties(StudioProperties panel)
    {
        if (Quest() is not { } quest)
        {
            panel.Header("Квест не выбран", "Квесты");
            return;
        }

        var flow = new QuestFlow(quest);
        if (_stageId is null || !flow.HasStage(_stageId))
        {
            panel.Header(studio.Catalog.NameOf(_questId!), "Квест");
            panel.IdLine(_questId!);
            panel.Text($"Этапов: {flow.StageOrder.Count}. Выберите этап в списке или на графе, чтобы изменить его.", muted: true);
            panel.Links("Где используется", new ReferenceIndex(studio.Workspace).UsedBy(_questId!).Select(user => ($"{studio.Catalog.Describe(user).KindLabel} «{studio.Catalog.NameOf(user)}»", user)));
            return;
        }

        var stage = flow.Stage(_stageId);
        panel.Header(StageName(flow, _stageId), "Этап квеста");
        panel.IdLine($"{_questId}/{_stageId}");
        var objectives = stage["objectives"]!.AsArray().OfType<JsonObject>().ToArray();
        panel.AddChild(new Label { Text = "Цели этапа", ThemeTypeVariation = "HeaderLabel" });
        foreach (var objective in objectives)
        {
            var objectiveId = (string)objective["id"]!;
            var titleId = (string?)objective["titleTextId"];
            panel.TextField("Цель (как её видит игрок)", studio.Catalog.ResolveText(titleId), text => SetText(titleId, text));
            panel.Text("Выполнена, когда:", muted: true);
            panel.AddChild(new StudioRuleEditor(studio, objective["completionConditions"] as JsonArray ?? [], effects: false,
                rules => MutateObjective(objectiveId, "completionConditions", rules, "условие цели")));
            panel.Text("После выполнения:", muted: true);
            panel.AddChild(new StudioRuleEditor(studio, objective["completionEffects"] as JsonArray ?? [], effects: true,
                rules => MutateObjective(objectiveId, "completionEffects", rules, "последствия цели")));
        }

        var composition = stage["composition"]!.AsObject();
        var mode = new OptionButton();
        mode.AddItem("Все цели");
        mode.AddItem("Любая цель");
        mode.AddItem("N из них");
        mode.Selected = (string)composition["mode"]! switch { "any" => 1, "threshold" => 2, _ => 0 };
        panel.Field("Этап выполнен, когда выполнены", mode);
        var threshold = new SpinBox { MinValue = 1, MaxValue = Math.Max(1, objectives.Length), Value = (int?)composition["threshold"] ?? 1, Editable = mode.Selected == 2 };
        panel.Field("Сколько целей (для «N из них»)", threshold);
        mode.ItemSelected += index => SetComposition((int)index, (int)threshold.Value);
        threshold.ValueChanged += value => SetComposition(mode.Selected, (int)value);

        panel.AddChild(new Label { Text = "Куда ведёт этап", ThemeTypeVariation = "HeaderLabel" });
        foreach (var edge in flow.Outgoing(_stageId))
        {
            panel.Text($"→ {EdgeLabel(flow, edge)}: {(edge.Target.StageId is { } next ? StageName(flow, next) : EndLabel(edge.Target))}");
            if (edge.TransitionId is { } transitionId)
            {
                var transition = stage["transitions"]!.AsArray().OfType<JsonObject>().First(item => (string)item["id"]! == transitionId);
                panel.Text("Что меняется в мире при этом переходе:", muted: true);
                panel.AddChild(new StudioRuleEditor(studio, transition["effects"] as JsonArray ?? [], effects: true,
                    rules => MutateTransition(transitionId, rules)));
            }
        }

        panel.Links("Где на карте", MapLinks(stage));

        panel.Buttons(("+ Этап после", InsertAfter), ("+ Добавить выбор", AddChoice));
        panel.Buttons(("Удалить этап", RemoveStage));
    }

    private void SetComposition(int mode, int threshold)
    {
        Mutate("правило этапа", quest =>
        {
            var stage = quest["stages"]!.AsArray().OfType<JsonObject>().First(item => (string)item["id"]! == _stageId);
            var composition = stage["composition"]!.AsObject();
            composition["mode"] = mode switch { 1 => "any", 2 => "threshold", _ => "all" };
            if (mode == 2) composition["threshold"] = threshold; else composition.Remove("threshold");
            return quest;
        });
    }

    private void InsertAfter()
    {
        var flow = new QuestFlow(Quest()!);
        var edge = flow.Outgoing(_stageId!).FirstOrDefault();
        using var _ = studio.Session.Begin("новый этап");
        var textId = NewObjectiveText("Новая цель");
        var stageId = $"s-{Hex()}";
        Mutate("новый этап", quest => QuestFlowEditor.InsertStageOnEdge(quest, _stageId!, edge?.TransitionId, stageId, $"o-{Hex()}", textId));
        _stageId = stageId;
    }

    private void AddChoice()
    {
        var flow = new QuestFlow(Quest()!);
        using var _ = studio.Session.Begin("новый выбор");
        var textId = NewObjectiveText("Другой вариант");
        var target = flow.Outgoing(_stageId!).FirstOrDefault()?.Target ?? FlowTarget.Ending("completed");
        Mutate("новый выбор", quest => QuestFlowEditor.AddBranchArm(quest, _stageId!, $"o-{Hex()}", textId, $"t-{Hex()}", target));
    }

    private void RemoveStage()
    {
        try
        {
            Mutate("удалить этап", quest => QuestFlowEditor.RemoveStage(quest, _stageId!));
            _stageId = null;
        }
        catch (QuestEditException error)
        {
            studio.ShowBanner(error.Message, error: false);
        }
    }

    private void Mutate(string label, Func<JsonObject, JsonObject> change)
    {
        var address = studio.Workspace.Locate(_questId!)!;
        var edited = change((JsonObject)Quest()!.DeepClone());
        studio.Session.Set(address.RelativePath, address.Key, edited, label);
    }

    /// <summary>A new objective needs a title text entity in the quest's own module; it gets a fresh ID too.</summary>
    private string NewObjectiveText(string text)
    {
        var address = studio.Workspace.Locate(_questId!)!;
        var ns = _questId![.._questId!.IndexOf(':')];
        var id = studio.Workspace.NewId(ns, "text", "objective");
        studio.Session.Set(address.RelativePath, id, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["id"] = id,
            ["value"] = new JsonObject { ["default"] = text, ["translations"] = new JsonObject { ["ru"] = text } },
            ["purpose"] = "ui"
        }, "текст цели");
        return id;
    }

    private static string Hex() => Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4));

    private void SetText(string? textId, string text)
    {
        if (textId is null || studio.Workspace.Locate(textId) is not { } address) return;
        var entity = studio.Workspace.Get(textId)!.AsObject();
        entity["value"] = new JsonObject { ["default"] = text, ["translations"] = new JsonObject { ["ru"] = text } };
        studio.Session.Set(address.RelativePath, address.Key, entity, "текст цели");
    }

    private void MutateObjective(string objectiveId, string field, JsonArray rules, string label) =>
        Mutate(label, quest =>
        {
            var stage = quest["stages"]!.AsArray().OfType<JsonObject>().First(item => (string)item["id"]! == _stageId);
            stage["objectives"]!.AsArray().OfType<JsonObject>().First(item => (string)item["id"]! == objectiveId)[field] = rules;
            return quest;
        });

    private void MutateTransition(string transitionId, JsonArray rules) =>
        Mutate("последствия перехода", quest =>
        {
            var stage = quest["stages"]!.AsArray().OfType<JsonObject>().First(item => (string)item["id"]! == _stageId);
            stage["transitions"]!.AsArray().OfType<JsonObject>().First(item => (string)item["id"]! == transitionId)["effects"] = rules;
            return quest;
        });

    /// <summary>World objects whose interactions produce the facts this stage waits for (spec TRIG03).</summary>
    private IEnumerable<(string, string)> MapLinks(JsonObject stage)
    {
        var facts = new FactIndex(studio.Workspace);
        var read = new List<StoryFact>();
        void Collect(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj when (string?)(obj["op"] as JsonValue) == "npc.state":
                    read.Add(new StoryFact((string)obj["characterId"]!, (string)obj["stateKey"]!));
                    break;
                case JsonObject obj:
                    foreach (var (_, child) in obj) Collect(child);
                    break;
                case JsonArray array:
                    foreach (var child in array) Collect(child);
                    break;
            }
        }

        foreach (var objective in stage["objectives"]!.AsArray()) Collect(objective?["completionConditions"]);
        var sources = read.SelectMany(facts.WritingSources).ToHashSet(StringComparer.Ordinal);
        foreach (var id in studio.Workspace.EntityIds.Where(id => id.StartsWith("urman.world:", StringComparison.Ordinal)))
        {
            var parameters = studio.Workspace.Get(id)?["params"];
            var used = new[] { (string?)(parameters?["interactionId"] as JsonValue), (string?)(parameters?["talk"]?["interactionId"] as JsonValue), (string?)(parameters?["talk"]?["dialogueId"] as JsonValue) };
            if (used.Any(value => value is not null && sources.Contains(value)))
            {
                yield return ($"На карте: «{studio.Catalog.NameOf(id)}»", id);
            }
        }

        foreach (var source in sources.Where(source => EntityCatalog.KindOf(source, null) == "dialogue"))
        {
            yield return ($"Ответ в диалоге «{studio.Catalog.NameOf(source)}»", source);
        }
    }

    public void SelectStageForTest(string stageId) => SelectStage(stageId);

    public IReadOnlyList<(string, string)> MapLinksForTest(string stageId) =>
        MapLinks(new QuestFlow(Quest()!).Stage(stageId)).ToArray();

    // ---- template -----------------------------------------------------------------------------

    private void OpenTemplateForm()
    {
        var dialog = new ConfirmationDialog { Title = "Новый квест: выбор с двумя исходами", OkButtonText = "Создать", CancelButtonText = "Отмена", MinSize = new Vector2I(560, 0) };
        var box = new VBoxContainer();
        dialog.AddChild(box);
        LineEdit Field(string label, string value)
        {
            box.AddChild(new Label { Text = label, ThemeTypeVariation = "MutedLabel" });
            var edit = new LineEdit { Text = value };
            box.AddChild(edit);
            return edit;
        }

        var title = Field("Название квеста", "Калитка соседа");
        var npc = Field("Кто просит о помощи", "Сосед");
        var request = Field("Что он говорит", "Поможешь с калиткой? Нужна проволока.");
        var accept = Field("Ответ «согласиться»", "Помогу.");
        var refuse = Field("Ответ «отказаться»", "Некогда.");
        var item = Field("Что нужно найти", "Моток проволоки");
        box.AddChild(new Label
        {
            Text = "Персонаж, предмет и калитка появятся рядом с центром текущего вида в разделе «Мир»; их можно сразу передвинуть. Всё созданное — обычные данные, их можно менять и удалить; создание отменяется одной командой.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, ThemeTypeVariation = "MutedLabel", CustomMinimumSize = new Vector2(520, 0)
        });
        dialog.Confirmed += () =>
        {
            var world = (StudioWorldSection)studio.Section("world");
            var at = world.Pivot;
            double[] P(float dx, float dz) => [Math.Round(at.X + dx, 2), 0.0, Math.Round(at.Z + dz, 2)];
            var ids = Urman.Studio.Core.Templates.TwoOutcomeQuestTemplate.Create(studio.Session, new Urman.Studio.Core.Templates.TwoOutcomeQuestRequest(
                _questId is { } current ? current[..current.IndexOf(':')] : "urman.chapter1",
                "content/modules/urman-chapter1", "content/campaigns/urman.chapter1/campaign.json",
                title.Text.Trim(), npc.Text.Trim(), "Resident", P(0, 0), 180, request.Text.Trim(), accept.Text.Trim(), refuse.Text.Trim(),
                item.Text.Trim(), P(4, 3), "urman.catalog:urman_village_exterior_kit/woodpile-stackedlogs",
                "urman.catalog:urman_village_exterior_kit/gate-crookedtimber", P(-3, 2), 90, P(0, 0), [4.0, 2.5, 4.0]));
            OpenQuest(ids.QuestId);
            Refresh();
            dialog.QueueFree();
        };
        dialog.Canceled += dialog.QueueFree;
        studio.AddChild(dialog);
        dialog.PopupCentered();
    }

    // ---- wording ------------------------------------------------------------------------------

    private string StageName(QuestFlow flow, string stageId)
    {
        var first = flow.Stage(stageId)["objectives"]!.AsArray().OfType<JsonObject>().FirstOrDefault();
        var title = studio.Catalog.ResolveText((string?)first?["titleTextId"]);
        return string.IsNullOrEmpty(title) ? stageId : title;
    }

    private static string StageKind(QuestFlow flow, string stageId)
    {
        var composition = flow.Stage(stageId)["composition"]!;
        var count = flow.Stage(stageId)["objectives"]!.AsArray().Count;
        return (string)composition["mode"]! switch
        {
            "any" when count > 1 => "◇ Любая из целей",
            "threshold" => $"⇉ {(int)composition["threshold"]!} из {count}",
            _ when count > 1 => $"⇉ Все {count} цели",
            _ => "● Цель"
        };
    }

    private string EdgeLabel(QuestFlow flow, FlowEdge edge)
    {
        string Objective() =>
            studio.Catalog.ResolveText((string?)flow.Stage(edge.FromStageId)["objectives"]!.AsArray().OfType<JsonObject>()
                .FirstOrDefault(objective => (string)objective["id"]! == edge.ObjectiveId)?["titleTextId"]);
        return edge.Trigger switch
        {
            "objective-complete" => $"если «{Objective()}»",
            "objective-fail" => $"если провалено «{Objective()}»",
            _ => "дальше"
        };
    }

    private string EndLabel(FlowTarget target)
    {
        var outcome = target.OutcomeId is null ? "" : $" · исход «{studio.Catalog.OutcomeName(_questId!, target.OutcomeId)}»";
        return target.End == "failed" ? $"■ Квест провален{outcome}" : $"■ Квест завершён{outcome}";
    }
}

/// <summary>The author's own block positions in quest graphs: personal layout, not quest data (DATA07).</summary>
public sealed class StudioLayout(string root)
{
    private readonly string _path = Path.Combine(root, ".urman-studio", "layout.json");
    private JsonObject? _data;

    private JsonObject Data => _data ??= File.Exists(_path) ? JsonNode.Parse(File.ReadAllText(_path))!.AsObject() : new JsonObject();

    public Vector2? Get(string questId, string stageId) =>
        Data[questId]?[stageId] is JsonArray point ? new Vector2((float)(double)point[0]!, (float)(double)point[1]!) : null;

    public void Set(string questId, string stageId, Vector2 position)
    {
        if (Data[questId] is not JsonObject quest)
        {
            Data[questId] = quest = new JsonObject();
        }

        quest[stageId] = new JsonArray(Math.Round(position.X, 1), Math.Round(position.Y, 1));
    }

    public void Save() => Urman.Studio.Core.Storage.AtomicFile.WriteAllText(_path, Data.ToJsonString());
}
