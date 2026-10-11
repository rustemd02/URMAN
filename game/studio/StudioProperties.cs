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

    public void IdLine(string id, IReadOnlyList<string>? scopeEntityIds = null)
    {
        var frozenScopeEntityIds = (scopeEntityIds ?? new[] { id }).ToArray();
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = id, ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.Arbitrary, SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Постоянный ID: не меняется при переименовании" });
        StudioRoot.Button(row, "Копировать", () =>
        {
            DisplayServer.ClipboardSet(id);
            _studio.RefreshStatus("ID скопирован");
        }).TooltipText = "Скопировать ID для разработчика";
        StudioRoot.Button(row, "Контекст…", () => OpenContext(frozenScopeEntityIds)).TooltipText = "Передать разработчику контекст выбранной области, в том числе ещё не сохранённый черновик";
        AddChild(row);
    }

    private void OpenContext(IReadOnlyList<string> scopeEntityIds)
    {
        var context = new Urman.Studio.Core.Handoff.DeveloperContext(_studio.Workspace, _studio.Catalog);
        var dialog = new AcceptDialog { Title = "Контекст для разработчика", OkButtonText = "Закрыть", MinSize = new Vector2I(640, 0) };
        var box = new VBoxContainer();
        dialog.AddChild(box);
        box.AddChild(new Label { Text = "Что не так (необязательно):", ThemeTypeVariation = "MutedLabel" });
        var problem = new TextEdit { CustomMinimumSize = new Vector2(600, 60), PlaceholderText = "Например: после передачи досок калитка осталась закрыта, ожидалось «починена»." };
        box.AddChild(problem);
        bool TryContextText(out string text)
        {
            try
            {
                text = context.Text(scopeEntityIds, problem: problem.Text);
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or InvalidDataException)
            {
                text = "Не удалось подготовить контекст выбранной области: " + error.Message + " Проект не изменён.";
                return false;
            }
        }

        _ = TryContextText(out var initialContextText);
        var preview = new Label { Text = initialContextText, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
        problem.TextChanged += () =>
        {
            _ = TryContextText(out var currentContextText);
            preview.Text = currentContextText;
        };
        box.AddChild(preview);
        var row = new HBoxContainer();
        StudioRoot.Button(row, "Скопировать текст", () =>
        {
            if (!TryContextText(out var text))
            {
                preview.Text = text + " Текст не скопирован.";
                _studio.RefreshStatus("Контекст выбранной области не скопирован");
                return;
            }
            DisplayServer.ClipboardSet(text);
            _studio.RefreshStatus("Контекст скопирован");
        });
        StudioRoot.Button(row, "Сохранить пакет с черновиком", () =>
        {
            try
            {
                var (path, entities, bytes) = context.WritePackage(scopeEntityIds, Path.Combine(_studio.Workspace.Root, ".urman-studio", "handoff"), problem.Text);
                _ = TryContextText(out var savedContextText);
                preview.Text = $"Пакет сохранён: {Path.GetFileName(path)} — {entities} объектов, {bytes / 1024.0:0.#} КБ. Без ваших путей и сохранений.\n\nТекущий контекст:\n" + savedContextText;
                OS.ShellShowInFileManager(path);
            }
            catch (InvalidOperationException error)
            {
                preview.Text = error.Message + " Пакет не записан; проект не изменён.";
                _studio.RefreshStatus("Пакет контекста не сохранён");
            }
            catch (ArgumentException error)
            {
                preview.Text = error.Message + " Пакет не записан; проект не изменён.";
                _studio.RefreshStatus("Пакет контекста не сохранён");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                preview.Text = "Не удалось сохранить пакет. Проверьте, что объект существует и папка доступна для записи; неполный архив использовать нельзя.";
                _studio.RefreshStatus("Пакет контекста не сохранён");
            }
        });
        StudioRoot.Button(row, "Открыть пакет…", OpenContextPackage).TooltipText = "Посмотреть контекст с другого компьютера без изменения проекта";
        box.AddChild(row);
        _studio.AddChild(dialog);
        dialog.PopupCentered();
    }

    private void OpenContextPackage()
    {
        var picker = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem,
            Title = "Открыть пакет контекста", UseNativeDialog = true, Filters = ["*.zip ; Контекст URMAN Studio"]
        };
        picker.FileSelected += path =>
        {
            try
            {
                var package = Urman.Studio.Core.Handoff.DeveloperContext.ReadPackage(path);
                var schemaVersion = package.Manifest["schemaVersion"]?.GetValue<int>();
                var integrity = package.HasIntegrityReceipts ? "Контрольные суммы вложений совпадают." : "Старый пакет без контрольных сумм: целостность не подтверждена.";
                var completeness = package.Manifest["truncated"]?.GetValue<bool>() switch
                {
                    null => "У старого формата нет отдельного признака усечения списка зависимостей.",
                    true => "Манифест сообщает, что часть обнаруженных зависимостей не вошла в пакет.",
                    false => "Явное усечение обнаруженного списка не отмечено; это не подтверждает полный охват ссылок."
                };
                var discovery = package.Manifest["dependencyDiscovery"]?.GetValue<string>();
                var referenceCoverageComplete = package.Manifest["referenceCoverageComplete"]?.GetValue<bool>();
                var partialTypedCoverageV1 = discovery == "partial-typed-module-fields-v1";
                var partialTypedCoverageV2 = discovery == "partial-typed-module-fields-v2";
                var partialTypedCoverageV3 = discovery == "partial-typed-module-fields-v3";
                var incompleteCoverage = partialTypedCoverageV1 || partialTypedCoverageV2 || partialTypedCoverageV3 || referenceCoverageComplete == false;
                var coverageNote = partialTypedCoverageV3
                    ? "Обход v3 включает ссылки v2 (ресурсы, тексты, подписи и цели взаимодействий сцен, knowledge в документах) и схемные массивы ContentId: knowledge.sourceIds/reveals/contradicts/unlocks, vocabulary.rereadTargets/applicationTargets. Категория цели определяется каноническим ID; допустимость цели в конкретном поле не проверяется. Условия, эффекты, игровые и физические ссылки не охвачены; граф остаётся частичным."
                    : partialTypedCoverageV2
                        ? "Обход v2 читает известные типизированные поля ресурсов, персонажей, сцен, документов, возможностей, диалогов и квестов. Он включает ссылки на ресурсы и тексты; для взаимодействий сцен — labelTextId и цели targetSceneId/targetDialogueId/targetDocumentId/targetJournalEntryId (сцены, диалоги, документы и knowledge); для документов — assetRefs и ссылки на knowledge (knowledgeRefs). Более глубокие ссылки, условия, эффекты, другие поля, игровые и физические зависимости не охвачены; это не полный граф зависимостей."
                        : partialTypedCoverageV1
                            ? "Обнаружены только типизированные ссылки на ресурсы и тексты в полях модулей. Другие домены ссылок не покрыты; это не полный граф зависимостей."
                            : incompleteCoverage
                                ? "Манифест явно сообщает, что охват ссылок неполон; другие зависимости могут отсутствовать."
                                : "Полнота ресурсов и применимость к этому проекту не проверены.";
                var viewer = new AcceptDialog { Title = "Контекст из пакета", OkButtonText = "Закрыть" };
                var content = new VBoxContainer();
                viewer.AddChild(content);
                var scopeSummary = string.Empty;
                var scopeDetails = string.Empty;
                var entitySummary = $"{package.Entities.Count} объектов.";
                if (schemaVersion == 5)
                {
                    var scopeIds = FormatManifestIds(package.Manifest["scopeEntityIds"], out var scopeCount);
                    scopeSummary = $" Корневые ID области ({scopeCount}): {scopeIds}.";
                    scopeDetails = $"Корни области (manifest.scopeEntityIds; не список зависимостей): {scopeIds}\nОбъектов в entities.json вместе с зависимостями: {package.Entities.Count}.";
                    entitySummary = $"{package.Entities.Count} объектов в entities.json.";
                }
                content.AddChild(new Label
                {
                    Text = $"{entitySummary}{scopeSummary} {integrity}\n{completeness}\n{coverageNote}\nТолько просмотр: проект не изменён.",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                });
                var previewText = new System.Text.StringBuilder(package.Text).AppendLine().AppendLine();
                if (schemaVersion == 5) previewText.AppendLine(scopeDetails).AppendLine();
                previewText.AppendLine("Данные объектов:")
                    .Append(package.Entities.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                if (package.Sources is null)
                {
                    previewText.AppendLine().AppendLine()
                        .AppendLine("Снимки исходников не приложены: это старый формат пакета. Показан только контекст и данные объектов.");
                }
                else
                {
                    previewText.AppendLine().AppendLine()
                        .AppendLine("Снимки исходников (только просмотр):");
                    if (package.Sources.Count == 0)
                        previewText.AppendLine("В пакете нет снимков исходников.");

                    foreach (var source in package.Sources)
                    {
                        previewText.AppendLine().AppendLine($"=== {source.RelativePath} ===")
                            .AppendLine($"Git-ревизия проекта при экспорте: {package.Manifest["baseRevision"]?.GetValue<string>() ?? "не указана"}")
                            .AppendLine($"SHA-256 сохранённого текста при загрузке в Studio: {source.BaseHash ?? "нет (новый файл)"}")
                            .AppendLine($"SHA-256 текущего содержимого: {source.CurrentHash}");

                        if (source.BaseText is null || source.IsNew)
                        {
                            previewText.AppendLine("Сохранённого текста при загрузке в Studio нет: новый исходник.")
                                .AppendLine("--- Текущий черновик ---")
                                .AppendLine(source.CurrentText);
                        }
                        else if (source.BaseText == source.CurrentText)
                        {
                            previewText.AppendLine("Сохранённый текст при загрузке в Studio и текущий черновик совпадают, текст показан один раз.")
                                .AppendLine("--- Текущий черновик ---")
                                .AppendLine(source.CurrentText);
                        }
                        else
                        {
                            previewText.AppendLine("--- Сохранённый текст при загрузке в Studio ---")
                                .AppendLine(source.BaseText)
                                .AppendLine("--- Текущий черновик ---")
                                .AppendLine(source.CurrentText);
                        }
                    }
                }
                if (incompleteCoverage)
                {
                    var unsupportedEntityIds = FormatManifestIds(package.Manifest["unsupportedEntityIds"], out var unsupportedCount);
                    var missingDependencyIds = FormatManifestIds(package.Manifest["missingDependencyIds"], out var missingCount);
                    previewText.AppendLine().AppendLine()
                        .AppendLine("Ограничения обнаружения зависимостей:")
                        .AppendLine($"Неподдерживаемые сущности ({unsupportedCount}): {unsupportedEntityIds}")
                        .AppendLine($"Недостающие зависимости ({missingCount}): {missingDependencyIds}");
                }
                content.AddChild(new TextEdit
                {
                    Text = previewText.ToString(), Editable = false, WrapMode = TextEdit.LineWrappingMode.Boundary,
                    CustomMinimumSize = new Vector2(720, 440)
                });
                _studio.AddChild(viewer);
                viewer.PopupCentered(new Vector2I(800, 600));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                _studio.RefreshStatus("Пакет не открыт: недоступен, повреждён или имеет неподдерживаемый формат. Проект не изменён.");
            }
            finally { picker.QueueFree(); }
        };
        picker.Canceled += picker.QueueFree;
        _studio.AddChild(picker);
        picker.PopupCentered(new Vector2I(900, 600));
    }

    private static string FormatManifestIds(System.Text.Json.Nodes.JsonNode? value, out int count)
    {
        if (value is not System.Text.Json.Nodes.JsonArray ids)
        {
            count = 0;
            return "список не указан";
        }

        var values = ids.Select(item => item?.GetValue<string>() ?? "(ID не указан)").ToArray();
        count = values.Length;
        return count == 0 ? "нет" : string.Join(", ", values);
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
