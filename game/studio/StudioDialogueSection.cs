using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.App;

/// <summary>
/// Dialogues as a script (spec DLG01, first part): each line with its speaker
/// and text, the player's answers with their conditions and consequences in
/// words, and the entry rules that pick where a conversation starts. Texts are
/// the existing localized text entities, so edits reach the game unchanged in
/// format. Cutscene timelines belong to a later stage (P3).
/// </summary>
public sealed class StudioDialogueSection(StudioRoot studio) : IStudioSection
{
    private Control? _view;
    private ItemList _list = null!;
    private VBoxContainer _script = null!;
    private string? _dialogueId;

    public string Key => "scenes";
    public string Title => "Диалоги и сцены";
    public Control View => _view ??= Build();

    public StudioCutscenePanel? Cutscenes { get; private set; }

    private Control Build()
    {
        var tabs = new TabContainer();
        var split = new HSplitContainer { SplitOffsets = [320], Name = "Диалоги" };
        tabs.AddChild(split);
        Cutscenes = new StudioCutscenePanel(studio) { Name = "Катсцены" };
        tabs.AddChild(Cutscenes);
        var left = new VBoxContainer();
        left.AddChild(new Label { Text = "Диалоги", ThemeTypeVariation = "HeaderLabel" });
        _list = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _list.ItemSelected += index => Open((string)_list.GetItemMetadata((int)index));
        left.AddChild(_list);
        split.AddChild(left);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _script = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _script.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_script);
        split.AddChild(scroll);
        Refresh();
        return tabs;
    }

    public void ShowCutscenes()
    {
        _ = View;
        ((TabContainer)View).CurrentTab = 1;
    }

    public void Refresh()
    {
        Cutscenes?.Refresh();
        if (_list is null) return;
        _list.Clear();
        foreach (var id in studio.Workspace.EntityIds.Where(id => EntityCatalog.KindOf(id, null) == "dialogue").OrderBy(id => studio.Catalog.NameOf(id), StringComparer.CurrentCulture))
        {
            _list.AddItem(studio.Catalog.NameOf(id));
            _list.SetItemMetadata(_list.ItemCount - 1, id);
            if (id == _dialogueId) _list.Select(_list.ItemCount - 1);
        }

        ShowScript();
    }

    private void Open(string id)
    {
        _dialogueId = id;
        studio.Select(id);
        ShowScript();
    }

    private JsonObject? Dialogue() => _dialogueId is null ? null : studio.Workspace.Get(_dialogueId) as JsonObject;

    private void ShowScript()
    {
        foreach (var child in _script.GetChildren()) child.QueueFree();
        if (Dialogue() is not { } dialogue)
        {
            _script.AddChild(new Label { Text = "Выберите диалог слева.", ThemeTypeVariation = "MutedLabel" });
            return;
        }

        var phrases = new ConditionPhrases(studio.Catalog);
        _script.AddChild(new Label { Text = studio.Catalog.NameOf(_dialogueId!), ThemeTypeVariation = "HeaderLabel" });
        if (dialogue["entryRoutes"] is JsonArray routes && routes.Count > 0)
        {
            _script.AddChild(new Label { Text = "С чего начинается разговор (сверху вниз, первое подходящее):", ThemeTypeVariation = "MutedLabel" });
            foreach (var route in routes.OfType<JsonObject>())
            {
                _script.AddChild(new Label { Text = $"• Если {phrases.DescribeAll(route["conditions"] as JsonArray)} — с реплики «{NodeText(dialogue, (string)route["nodeId"]!)}»", AutowrapMode = TextServer.AutowrapMode.WordSmart });
            }

            _script.AddChild(new Label { Text = $"• Иначе — с реплики «{NodeText(dialogue, (string)dialogue["startNodeId"]!)}»", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        }

        var nodes = dialogue["nodes"]!.AsArray();
        for (var index = 0; index < nodes.Count; index++)
        {
            var nodeIndex = index;
            var node = nodes[index]!.AsObject();
            var card = new PanelContainer { ThemeTypeVariation = "CardPanel" };
            var box = new VBoxContainer();
            card.AddChild(box);
            box.AddChild(new Label { Text = $"{Speaker(dialogue, (string)node["speakerRole"]!)} говорит:", ThemeTypeVariation = "MutedLabel" });
            box.AddChild(TextEdit((string)node["textId"]!, "реплика"));
            var choices = node["choices"]!.AsArray();
            for (var choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
            {
                var at = choiceIndex;
                var choice = choices[choiceIndex]!.AsObject();
                var answer = new VBoxContainer();
                var margin = new MarginContainer();
                margin.AddThemeConstantOverride("margin_left", 24);
                margin.AddChild(answer);
                answer.AddChild(new Label { Text = "Ответ Айдара:", ThemeTypeVariation = "MutedLabel" });
                answer.AddChild(TextEdit((string)choice["textId"]!, "ответ"));
                answer.AddChild(new Label { Text = "Доступен, если:", ThemeTypeVariation = "MutedLabel" });
                answer.AddChild(new StudioRuleEditor(studio, choice["conditions"] as JsonArray ?? [], effects: false,
                    rules => MutateChoice(nodeIndex, at, "conditions", rules)));
                answer.AddChild(new Label { Text = "Что запоминается после ответа:", ThemeTypeVariation = "MutedLabel" });
                answer.AddChild(new StudioRuleEditor(studio, choice["effects"] as JsonArray ?? [], effects: true,
                    rules => MutateChoice(nodeIndex, at, "effects", rules)));
                var next = (string?)choice["nextNodeId"];
                answer.AddChild(new Label { Text = next is null ? "→ разговор заканчивается" : $"→ дальше: «{NodeText(dialogue, next)}»", ThemeTypeVariation = "MutedLabel" });
                box.AddChild(margin);
            }

            if (choices.Count == 0)
            {
                box.AddChild(new Label { Text = "Ответов нет — после реплики разговор заканчивается.", ThemeTypeVariation = "MutedLabel" });
            }

            _script.AddChild(card);
        }
    }

    private LineEdit TextEdit(string textId, string label)
    {
        var edit = new LineEdit { Text = studio.Catalog.ResolveText(textId), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var original = edit.Text;
        void Commit(string text)
        {
            if (text == original || studio.Workspace.Locate(textId) is not { } address) return;
            var entity = studio.Workspace.Get(textId)!.AsObject();
            var translations = entity["value"]?["translations"] as JsonObject ?? [];
            translations["ru"] = text;
            entity["value"] = new JsonObject { ["default"] = text, ["translations"] = translations.DeepClone() };
            studio.Session.Set(address.RelativePath, address.Key, entity, label);
            original = text;
        }

        edit.TextSubmitted += Commit;
        edit.FocusExited += () => Commit(edit.Text);
        return edit;
    }

    private void MutateChoice(int node, int choice, string field, JsonArray rules)
    {
        var address = studio.Workspace.Locate(_dialogueId!)!;
        var dialogue = Dialogue()!.DeepClone().AsObject();
        dialogue["nodes"]![node]!["choices"]![choice]![field] = rules;
        studio.Session.Set(address.RelativePath, address.Key, dialogue, field == "effects" ? "последствия ответа" : "условие ответа");
    }

    private string NodeText(JsonObject dialogue, string nodeId)
    {
        var node = dialogue["nodes"]!.AsArray().OfType<JsonObject>().FirstOrDefault(item => (string)item["id"]! == nodeId);
        var text = studio.Catalog.ResolveText((string?)node?["textId"]);
        return text.Length > 50 ? text[..49] + "…" : text;
    }

    private string Speaker(JsonObject dialogue, string role)
    {
        var bindings = studio.Workspace.Files.FirstOrDefault(file => file.RelativePath.EndsWith("urman.chapter1/campaign.json", StringComparison.Ordinal))?.Get("roleBindings");
        return bindings?[role] is JsonValue character ? studio.Catalog.NameOf((string)character!) : role;
    }

    private void CutsceneActionProperties(StudioProperties panel, string id, JsonObject action)
    {
        var p = action["params"]!.AsObject();
        var kind = (string?)p["action"] ?? "";
        panel.Header((string?)action["name"] ?? id, "Действие сцены");
        panel.IdLine(id);
        panel.TextField("Название действия", (string?)action["name"] ?? "", text => studio.Session.SetField(id, ["name"], text, "название действия"));
        void Number(string label, string field, double min, double max) =>
            panel.NumberField(label, (float)(double)p[field]!, value => studio.Session.SetField(id, ["params", field], Math.Round(Math.Clamp(value, min, max), 3), label.ToLowerInvariant()));
        switch (kind)
        {
            case "wait":
                Number("Длительность, с", "seconds", 0, 30);
                break;
            case "say":
                var textId = "urman.chapter1:text/" + (string)p["text"]!;
                panel.TextField("Реплика", studio.Catalog.ResolveText(textId), text =>
                {
                    if (studio.Workspace.Locate(textId) is not { } address) return;
                    var entity = studio.Workspace.Get(textId)!.AsObject();
                    var translations = entity["value"]?["translations"] as JsonObject ?? [];
                    translations["ru"] = text;
                    entity["value"] = new JsonObject { ["default"] = text, ["translations"] = translations.DeepClone() };
                    studio.Session.Set(address.RelativePath, address.Key, entity, "реплика сцены");
                });
                panel.TextField("Кто говорит (подпись)", (string?)p["speaker"] ?? "", text => studio.Session.SetField(id, ["params", "speaker"], text, "говорящий"));
                Number("Минимум на экране, с", "seconds", .3, 15);
                panel.Text("Субтитр держится не меньше, чем нужно, чтобы его прочитать.", muted: true);
                break;
            case "cut":
                Number("Угол обзора, °", "fov", 20, 100);
                Number("Длительность движения камеры, с", "seconds", 0, 20);
                panel.Text(p["position"]?["car"] is not null ? "План привязан к машине: стоит у её борта со стороны улицы." : "Точку камеры можно заменить кнопкой «+ Кадр из текущего вида».", muted: true);
                break;
            case "move":
                Number("Скорость, м/с", "speed", .1, 6);
                panel.Text("Длительность ходьбы зависит от земли — на шкале она показана как оценка (⧗).", muted: true);
                break;
            default:
                panel.Text($"Тип действия: {kind}", muted: true);
                break;
        }
    }

    public bool Reveal(string id)
    {
        if (EntityCatalog.KindOf(id, null) != "dialogue" || studio.Workspace.Get(id) is null) return false;
        _dialogueId = id;
        if (_view is not null) Refresh();
        return true;
    }

    public void FillProperties(StudioProperties panel)
    {
        if (studio.Selection is { } selected && selected.StartsWith("urman.cutscene:", StringComparison.Ordinal) && studio.Workspace.Get(selected) is JsonObject action)
        {
            CutsceneActionProperties(panel, selected, action);
            return;
        }

        if (_dialogueId is null)
        {
            panel.Header("Диалог не выбран", "Диалоги");
            return;
        }

        panel.Header(studio.Catalog.NameOf(_dialogueId), "Диалог");
        panel.IdLine(_dialogueId);
        panel.Links("Где используется", new ReferenceIndex(studio.Workspace).UsedBy(_dialogueId)
            .Select(user => ($"{studio.Catalog.Describe(user).KindLabel} «{studio.Catalog.NameOf(user)}»", user)));
        panel.Text("Монтажная шкала сцен и камеры появятся на этапе P3.", muted: true);
    }
}
