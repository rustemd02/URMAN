using Godot;

namespace Urman.Studio.App;

/// <summary>
/// The right-hand properties panel: human name first, type in words, the ID in
/// one click, the main fields visible and technical ones under "Дополнительно"
/// (spec UX07). Sections fill it; it never closes while the author types.
/// </summary>
public partial class StudioProperties : VBoxContainer
{
    private readonly StudioRoot _studio;

    public StudioProperties(StudioRoot studio)
    {
        _studio = studio;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);
    }

    public void Clear()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
    }

    public void Header(string name, string kind)
    {
        AddChild(new Label { Text = name, ThemeTypeVariation = "HeaderLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        AddChild(new Label { Text = kind, ThemeTypeVariation = "MutedLabel" });
    }

    public void IdLine(string id)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = id, ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.Arbitrary, SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Постоянный ID: не меняется при переименовании" });
        StudioRoot.Button(row, "Копировать", () =>
        {
            DisplayServer.ClipboardSet(id);
            _studio.RefreshStatus("ID скопирован");
        }).TooltipText = "Скопировать ID для разработчика";
        StudioRoot.Button(row, "Контекст…", () => OpenContext(id)).TooltipText = "Передать разработчику точный контекст, в том числе ещё не сохранённый черновик";
        AddChild(row);
    }

    private void OpenContext(string id)
    {
        var context = new Urman.Studio.Core.Handoff.DeveloperContext(_studio.Workspace, _studio.Catalog);
        var dialog = new AcceptDialog { Title = "Контекст для разработчика", OkButtonText = "Закрыть", MinSize = new Vector2I(640, 0) };
        var box = new VBoxContainer();
        dialog.AddChild(box);
        box.AddChild(new Label { Text = "Что не так (необязательно):", ThemeTypeVariation = "MutedLabel" });
        var problem = new TextEdit { CustomMinimumSize = new Vector2(600, 60), PlaceholderText = "Например: после передачи досок калитка осталась закрыта, ожидалось «починена»." };
        box.AddChild(problem);
        var preview = new Label { Text = context.Text(id), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
        problem.TextChanged += () => preview.Text = context.Text(id, problem: problem.Text);
        box.AddChild(preview);
        var row = new HBoxContainer();
        StudioRoot.Button(row, "Скопировать текст", () =>
        {
            DisplayServer.ClipboardSet(context.Text(id, problem: problem.Text));
            _studio.RefreshStatus("Контекст скопирован");
        });
        StudioRoot.Button(row, "Сохранить пакет с черновиком", () =>
        {
            var (path, entities, bytes) = context.WritePackage(id, Path.Combine(_studio.Workspace.Root, ".urman-studio", "handoff"), problem.Text);
            preview.Text = $"Пакет сохранён: {Path.GetFileName(path)} — {entities} объектов, {bytes / 1024.0:0.#} КБ. Без ваших путей и сохранений.";
            OS.ShellShowInFileManager(path);
        });
        box.AddChild(row);
        _studio.AddChild(dialog);
        dialog.PopupCentered();
    }

    public void Text(string text, bool muted = false) =>
        AddChild(new Label { Text = text, ThemeTypeVariation = muted ? "MutedLabel" : "", AutowrapMode = TextServer.AutowrapMode.WordSmart });

    public T Field<T>(string label, T control) where T : Control
    {
        AddChild(new Label { Text = label, ThemeTypeVariation = "MutedLabel" });
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(control);
        return control;
    }

    /// <summary>A text field that commits on Enter or when focus leaves; unfinished text is never dropped (UX03).</summary>
    public LineEdit TextField(string label, string value, Action<string> commit)
    {
        var edit = Field(label, new LineEdit { Text = value });
        void Commit(string text)
        {
            if (text != value)
            {
                commit(text);
            }
        }

        edit.TextSubmitted += Commit;
        edit.FocusExited += () => Commit(edit.Text);
        return edit;
    }

    /// <summary>A number field in metres or degrees; a wrong entry is marked, the typed text stays.</summary>
    public LineEdit NumberField(string label, float value, Action<float> commit)
    {
        var edit = Field(label, new LineEdit { Text = value.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) });
        void Commit(string text)
        {
            if (float.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                edit.RemoveThemeColorOverride("font_color");
                edit.TooltipText = "";
                if (!Mathf.IsEqualApprox(number, value))
                {
                    commit(number);
                }
            }
            else
            {
                edit.AddThemeColorOverride("font_color", StudioTheme.Bad);
                edit.TooltipText = "Нужно число, например 12,5";
            }
        }

        edit.TextSubmitted += Commit;
        edit.FocusExited += () => Commit(edit.Text);
        return edit;
    }

    public HBoxContainer Buttons(params (string Text, Action Action)[] buttons)
    {
        var row = new HBoxContainer();
        foreach (var (text, action) in buttons)
        {
            StudioRoot.Button(row, text, action);
        }

        AddChild(row);
        return row;
    }

    public void Links(string title, IEnumerable<(string Text, string Id)> links)
    {
        AddChild(new Label { Text = title, ThemeTypeVariation = "MutedLabel" });
        var any = false;
        foreach (var (text, id) in links)
        {
            any = true;
            var button = new LinkButton { Text = text, TooltipText = id, Underline = LinkButton.UnderlineMode.OnHover };
            button.AddThemeColorOverride("font_color", StudioTheme.Accent);
            button.Pressed += () => _studio.Navigate(id);
            AddChild(button);
        }

        if (!any)
        {
            AddChild(new Label { Text = "Нет связей", ThemeTypeVariation = "MutedLabel" });
        }
    }

    public VBoxContainer Advanced()
    {
        var toggle = new Button { Text = "▸ Дополнительно", ThemeTypeVariation = "NavButton", Alignment = HorizontalAlignment.Left };
        var box = new VBoxContainer { Visible = false };
        toggle.Pressed += () =>
        {
            box.Visible = !box.Visible;
            toggle.Text = (box.Visible ? "▾" : "▸") + " Дополнительно";
        };
        AddChild(toggle);
        AddChild(box);
        return box;
    }
}

/// <summary>A section that is part of the spec but not built yet; it says so instead of pretending to be an editor.</summary>
public sealed class StudioPendingSection(string key, string title) : IStudioSection
{
    private Control? _view;

    public string Key => key;
    public string Title => title;

    public Control View => _view ??= Build();

    public void FillProperties(StudioProperties panel) => panel.Text("Раздел ещё не готов.", muted: true);
    public bool Reveal(string id) => false;
    public void Refresh() { }

    private Control Build()
    {
        var center = new CenterContainer();
        var box = new VBoxContainer { CustomMinimumSize = new(520, 0) };
        box.AddChild(new Label { Text = $"«{title}» — ещё в разработке", ThemeTypeVariation = "HeaderLabel", HorizontalAlignment = HorizontalAlignment.Center });
        box.AddChild(new Label
        {
            Text = "Этот раздел входит в URMAN Studio по ТЗ, но в текущей сборке его ещё нет. Работают «Мир», «Квесты», «Диалоги и сцены» и «Совместная работа». План и статус — в трекере проекта, задачи STUDIO.P2–P6.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = "MutedLabel"
        });
        center.AddChild(box);
        return center;
    }
}
