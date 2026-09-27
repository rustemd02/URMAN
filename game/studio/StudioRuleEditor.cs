using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.App;

/// <summary>
/// Conditions and effects as phrases (spec QUEST08): each rule is one row in
/// words with "НЕ" and remove buttons; "ИЛИ" groups nest. New rules are built
/// from the runtime's closed catalogue with typed pickers — a character slot
/// lists only characters, a quest slot only quests — so there is no syntax to
/// learn and no free-form key to invent. Every change hands the whole list
/// back to the owner, which records it as one undoable edit.
/// </summary>
public partial class StudioRuleEditor : VBoxContainer
{
    private readonly StudioRoot _studio;
    private readonly bool _effects;
    private readonly Action<JsonArray> _changed;
    private JsonArray _rules;

    public StudioRuleEditor(StudioRoot studio, JsonArray rules, bool effects, Action<JsonArray> changed)
    {
        _studio = studio;
        _rules = (JsonArray)rules.DeepClone();
        _effects = effects;
        _changed = changed;
        AddThemeConstantOverride("separation", 4);
        Rebuild();
    }

    private ConditionPhrases Phrases => new(_studio.Catalog);

    private void Rebuild()
    {
        foreach (var child in GetChildren()) child.QueueFree();
        if (_rules.Count == 0)
        {
            AddChild(new Label { Text = _effects ? "Ничего не меняет" : "Всегда (без условий)", ThemeTypeVariation = "MutedLabel" });
        }

        for (var index = 0; index < _rules.Count; index++)
        {
            AddChild(Row(_rules, index, () => Commit()));
        }

        var add = new HBoxContainer();
        StudioRoot.Button(add, _effects ? "+ Действие" : "+ Условие", () => OpenPicker(rule => { _rules.Add(rule); Commit(); }));
        if (!_effects)
        {
            StudioRoot.Button(add, "+ Группа «ИЛИ»", () =>
            {
                _rules.Add(new JsonObject { ["op"] = "any", ["conditions"] = new JsonArray() });
                Commit();
            });
        }

        AddChild(add);
    }

    private Control Row(JsonArray owner, int index, Action commit)
    {
        var rule = owner[index]!.AsObject();
        var op = (string?)rule["op"];
        if (op is "any" or "all")
        {
            var group = new PanelContainer { ThemeTypeVariation = "CardPanel" };
            var box = new VBoxContainer();
            group.AddChild(box);
            var head = new HBoxContainer();
            head.AddChild(new Label { Text = op == "any" ? "Любое из:" : "Все из:", SizeFlagsHorizontal = SizeFlags.ExpandFill });
            StudioRoot.Button(head, "✕", () => { owner.RemoveAt(index); commit(); }).TooltipText = "Убрать группу";
            box.AddChild(head);
            var inner = rule["conditions"]!.AsArray();
            for (var child = 0; child < inner.Count; child++)
            {
                box.AddChild(Row(inner, child, commit));
            }

            StudioRoot.Button(box, "+ Вариант", () => OpenPicker(added => { inner.Add(added); commit(); }));
            return group;
        }

        var row = new HBoxContainer();
        var negated = op == "not";
        var shown = negated ? rule["condition"]!.AsObject() : rule;
        var text = new Label
        {
            Text = (negated ? "НЕ: " : "") + Phrases.Describe(shown),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        if (ConditionPhrases.Kind((string?)shown["op"] ?? "") is null)
        {
            text.AddThemeColorOverride("font_color", StudioTheme.Bad);
        }

        row.AddChild(text);
        if (!_effects)
        {
            StudioRoot.Button(row, negated ? "НЕ ✓" : "НЕ", () =>
            {
                owner[index] = negated ? shown.DeepClone() : new JsonObject { ["op"] = "not", ["condition"] = rule.DeepClone() };
                commit();
            }).TooltipText = "Перевернуть условие";
        }

        StudioRoot.Button(row, "✕", () => { owner.RemoveAt(index); commit(); }).TooltipText = "Убрать";
        return row;
    }

    private void Commit()
    {
        _changed((JsonArray)_rules.DeepClone());
        Rebuild();
    }

    // ---- picker ------------------------------------------------------------------

    private void OpenPicker(Action<JsonObject> done)
    {
        var dialog = new ConfirmationDialog { Title = _effects ? "Новое действие" : "Новое условие", OkButtonText = "Добавить", CancelButtonText = "Отмена", MinSize = new Vector2I(520, 0) };
        var box = new VBoxContainer();
        dialog.AddChild(box);
        var kinds = _effects ? ConditionPhrases.Effects : ConditionPhrases.Conditions;
        var kind = new OptionButton();
        foreach (var item in kinds) kind.AddItem(item.Label);
        box.AddChild(new Label { Text = "Что проверить", ThemeTypeVariation = "MutedLabel" });
        box.AddChild(kind);
        var slots = new VBoxContainer();
        box.AddChild(slots);
        var readers = new List<Func<(string Field, JsonNode? Value)>>();
        void ShowSlots()
        {
            foreach (var child in slots.GetChildren()) child.QueueFree();
            readers.Clear();
            foreach (var slot in kinds[kind.Selected].Slots)
            {
                slots.AddChild(new Label { Text = slot.Label, ThemeTypeVariation = "MutedLabel" });
                readers.Add(SlotControl(slots, slot, () => readers.Count > 0 ? readers[0]().Value as JsonValue : null));
            }
        }

        kind.ItemSelected += _ => ShowSlots();
        ShowSlots();
        dialog.Confirmed += () =>
        {
            var rule = new JsonObject { ["op"] = kinds[kind.Selected].Op };
            foreach (var read in readers)
            {
                var (field, value) = read();
                rule[field] = value;
            }

            done(rule);
            dialog.QueueFree();
        };
        dialog.Canceled += dialog.QueueFree;
        _studio.AddChild(dialog);
        dialog.PopupCentered();
    }

    private Func<(string, JsonNode?)> SlotControl(Container parent, RuleSlot slot, Func<JsonValue?> firstSlot)
    {
        switch (slot.ValueKind)
        {
            case "enum":
            {
                var option = new OptionButton();
                foreach (var (_, label) in slot.Choices!) option.AddItem(label);
                parent.AddChild(option);
                return () => (slot.Field, JsonValue.Create(slot.Choices![option.Selected].Value));
            }
            case "value":
            {
                var option = new OptionButton();
                option.AddItem("да");
                option.AddItem("нет");
                option.AddItem("число…");
                var number = new SpinBox { MinValue = -1000, MaxValue = 1000, Visible = false };
                option.ItemSelected += index => number.Visible = index == 2;
                parent.AddChild(option);
                parent.AddChild(number);
                return () => (slot.Field, option.Selected switch { 0 => JsonValue.Create(true), 1 => JsonValue.Create(false), _ => JsonValue.Create((int)number.Value) });
            }
            case "integer":
            {
                var number = new SpinBox { MinValue = 1, MaxValue = 99, Value = 1 };
                parent.AddChild(number);
                return () => (slot.Field, JsonValue.Create((int)number.Value));
            }
            case "fact":
            {
                // Facts already used for this character are offered by name; a new one gets a readable name too.
                var option = new OptionButton();
                var known = new List<string>();
                var facts = new FactIndex(_studio.Workspace);
                var character = firstSlot() is { } id ? (string)id! : "";
                foreach (var fact in _studio.Workspace.EntityIds.Where(entityId => entityId.StartsWith(character + "#", StringComparison.Ordinal)))
                {
                    known.Add(fact[(character.Length + 1)..]);
                    option.AddItem((string)_studio.Workspace.Get(fact)!["label"]!);
                }

                option.AddItem("Новый факт…");
                var name = new LineEdit { PlaceholderText = "Как назвать факт (например: «согласился помочь»)", Visible = known.Count == 0 };
                option.ItemSelected += index => name.Visible = index == known.Count;
                parent.AddChild(option);
                parent.AddChild(name);
                _ = facts;
                return () =>
                {
                    if (option.Selected < known.Count) return (slot.Field, JsonValue.Create(known[option.Selected]));
                    var label = name.Text.Trim().Length == 0 ? "новый факт" : name.Text.Trim();
                    var key = "f-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(3));
                    if (character.Length > 0) ConditionPhrases.NameFact(_studio.Session, character, key, label);
                    return (slot.Field, JsonValue.Create(key));
                };
            }
            case "text":
            {
                var edit = new LineEdit();
                parent.AddChild(edit);
                return () => (slot.Field, JsonValue.Create(edit.Text));
            }
            default:
            {
                // Entity pickers: only entities of the slot's kind are offered.
                var option = new OptionButton();
                var ids = _studio.Workspace.EntityIds
                    .Where(id => EntityCatalog.KindOf(id, null) == slot.ValueKind)
                    .OrderBy(id => _studio.Catalog.NameOf(id), StringComparer.CurrentCulture)
                    .ToArray();
                foreach (var id in ids) option.AddItem(_studio.Catalog.NameOf(id));
                if (ids.Length == 0) option.AddItem("— пока нет ни одного —");
                parent.AddChild(option);
                return () => (slot.Field, ids.Length == 0 ? null : JsonValue.Create(ids[option.Selected]));
            }
        }
    }
}
