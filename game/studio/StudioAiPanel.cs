using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Godot;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>Frozen house request → typed proposal → staged geometry → one reversible apply.</summary>
public partial class StudioAiPanel : AcceptDialog
{
    private const string RecipePath = "game/content/studio/hero_house.recipe.json";
    private const string GeneratorPath = "assets/source/blender/act1/urman_village_exterior_kit.py";
    private readonly StudioRoot _studio;
    private readonly TextEdit _prompt = new() { CustomMinimumSize = new(560, 100), WrapMode = TextEdit.LineWrappingMode.Boundary, PlaceholderText = "Например: сделай окна этого дома шире" };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly CheckButton _globalInstructions = new() { Text = "Использовать мои глобальные инструкции Codex" };
    private readonly Button _ask;
    private readonly Button _apply;
    private readonly Button _beforeView;
    private readonly Button _afterView;
    private readonly SubViewport _preview = new() { Size = new(560, 260), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
    private readonly Node3D _previewModels = new() { Name = "HouseCompareModels" };
    private readonly Camera3D _previewCamera = new() { Position = new(11, 7, 13), Current = true };
    private Node3D? _beforeModel;
    private Node3D? _afterModel;
    private CancellationTokenSource? _cancel;
    private HouseWindowRequest? _request;
    private HouseWindowProposal? _proposal;
    private string? _stage;
    private string? _recipeText;
    private string? _snapshot;
    private string? _consentIoWarning;
    private bool _busy;

    public static bool OwnsFile(string path) => path == RecipePath ||
        System.Text.RegularExpressions.Regex.IsMatch(path,
            @"\A(?:game/assets/models/studio/hero-house-[a-f0-9]{32}\.glb(?:\.manifest\.json)?|assets/source/blender/studio/hero-house-[a-f0-9]{32}\.blend)\z",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public StudioAiPanel(StudioRoot studio)
    {
        _studio = studio;
        Title = "Изменить выбранный дом с Codex";
        OkButtonText = "Закрыть";
        MinSize = new(600, 560);
        WrapControls = true;
        var body = new VBoxContainer();
        AddChild(body);
        body.AddChild(_status);
        body.AddChild(_prompt);
        body.AddChild(_globalInstructions);
        var permissionFile = Path.Combine(studio.Workspace.Root, ".urman-studio/codex-pilot.json");
        LoadGlobalInstructionConsent(permissionFile);
        _globalInstructions.TooltipText = "Только ваши инструкции из ~/.codex/AGENTS.md. Команды, инструменты и запись модели остаются запрещены.";
        _globalInstructions.Toggled += allowed => SaveGlobalInstructionConsent(permissionFile, allowed);
        var row = new HBoxContainer();
        body.AddChild(row);
        _ask = StudioRoot.Button(row, "Предложить изменение", () => _ = AskAsync());
        StudioRoot.Button(row, "Проверить Codex", () => _ = DoctorAsync());
        StudioRoot.Button(row, "Остановить", () => _cancel?.Cancel());
        StudioRoot.Button(row, "Скопировать запрос", CopyRequest);
        _apply = StudioRoot.Button(row, "Применить", () => _ = ApplyAsync());
        _apply.Disabled = true;
        var compare = new HBoxContainer();
        body.AddChild(compare);
        _beforeView = StudioRoot.Button(compare, "До", () => ShowPreview(after: false));
        _afterView = StudioRoot.Button(compare, "После", () => ShowPreview(after: true));
        _beforeView.Disabled = true;
        _afterView.Disabled = true;
        var viewport = new SubViewportContainer { Stretch = true, CustomMinimumSize = new(560, 260) };
        viewport.AddChild(_preview);
        body.AddChild(viewport);
        _preview.AddChild(_previewModels);
        _preview.AddChild(new DirectionalLight3D { RotationDegrees = new(-40, -25, 0), LightEnergy = 1.5f });
        _previewCamera.Ready += () => _previewCamera.LookAt(new(0, 1.5f, 0));
        _preview.AddChild(_previewCamera);
        VisibilityChanged += () => { if (!Visible) _cancel?.Cancel(); };
    }

    private void LoadGlobalInstructionConsent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("allowGlobalInstructions", out var allowed)
                    && allowed.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    _globalInstructions.ButtonPressed = allowed.GetBoolean();
            }
        }
        catch (IOException)
        {
            _consentIoWarning = "Не удалось прочитать локальный выбор инструкций Codex; он выключен.";
        }
        catch (UnauthorizedAccessException)
        {
            _consentIoWarning = "Нет доступа к локальному выбору инструкций Codex; он выключен.";
        }
        catch (JsonException)
        {
            // A malformed consent file remains opt-out by default.
        }
    }

    private void SaveGlobalInstructionConsent(string path, bool allowed)
    {
        try
        {
            AtomicFile.WriteAllText(path, new JsonObject { ["allowGlobalInstructions"] = allowed }.ToJsonString());
            _consentIoWarning = null;
        }
        catch (IOException)
        {
            _globalInstructions.SetPressedNoSignal(!allowed);
            _consentIoWarning = "Не удалось сохранить выбор инструкций Codex; переключатель возвращён.";
            _status.Text = _consentIoWarning;
        }
        catch (UnauthorizedAccessException)
        {
            _globalInstructions.SetPressedNoSignal(!allowed);
            _consentIoWarning = "Нет доступа для сохранения выбора инструкций Codex; переключатель возвращён.";
            _status.Text = _consentIoWarning;
        }
    }

    public void Open(string? selected)
    {
        if (_busy) { PopupCenteredClamped(new(600, 560), 0.9f); return; }
        ClearProposal();
        _request = null;
        _ask.Disabled = selected != HeroHouseRecipe.EntityId;
        _status.Text = selected == HeroHouseRecipe.EntityId
            ? "Эталонный дом · все 7 окон. Codex предложит ширину, вы увидите новую модель перед применением. ⌘/Ctrl+Z полностью отменяет применение."
            : "Выберите эталонный дом Бабая в мире. Другие объекты будут подключены следующими этапами B20.";
        if (_consentIoWarning is not null) _status.Text += "\n" + _consentIoWarning;
        ResetSize();
        PopupCenteredClamped(new(600, 560), 0.9f);
        _prompt.GrabFocus();
    }

    private HouseWindowRequest Capture()
    {
        if (_studio.Selection != HeroHouseRecipe.EntityId) throw new InvalidOperationException("Выбор изменился: снова выберите эталонный дом.");
        if (string.IsNullOrWhiteSpace(_prompt.Text)) throw new InvalidOperationException("Опишите изменение окон.");
        _recipeText = File.ReadAllText(Path.Combine(_studio.Workspace.Root, RecipePath));
        var recipe = JsonNode.Parse(_recipeText)!.AsObject();
        _snapshot = Fingerprint(_recipeText);
        return new(Guid.NewGuid().ToString("N"), _snapshot, HeroHouseRecipe.EntityId,
            (decimal)recipe["windowClearWidth"]!, _prompt.Text);
    }

    private string Fingerprint(string recipe)
    {
        var world = _studio.Workspace.Locate(HeroHouseRecipe.EntityId)!;
        var source = File.ReadAllBytes(Path.Combine(_studio.Workspace.Root, GeneratorPath));
        return AtomicFile.Sha256(System.Text.Encoding.UTF8.GetBytes(recipe + "\n" +
            _studio.Workspace.File(world.RelativePath).Text + "\n" + AtomicFile.Sha256(source) + "\n" +
            AtomicFile.Sha256OfFile(Path.Combine(_studio.Workspace.Root, "assets/source/blender/act1/urman_village_exterior_kit.blend")) + "\n" + Revision()));
    }

    private string Revision()
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, WorkingDirectory = _studio.Workspace.Root };
        start.ArgumentList.Add("rev-parse"); start.ArgumentList.Add("HEAD");
        using var process = Process.Start(start) ?? throw new IOException("Не удалось определить Git-ревизию.");
        var result = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(5_000)) { process.Kill(entireProcessTree: true); throw new IOException("Git не ответил."); }
        if (process.ExitCode != 0 || result.Trim().Length != 40) throw new IOException("Нет полной Git-ревизии.");
        return result.Trim();
    }

    private void CopyRequest()
    {
        try { _request = Capture(); DisplayServer.ClipboardSet(JsonSerializer.Serialize(_request)); _status.Text = "Запрос скопирован. Применение остаётся только в Studio."; }
        catch (Exception error) { _status.Text = error.Message; }
    }

    private async Task AskAsync()
    {
        if (_busy) return;
        SetBusy(true);
        ClearProposal();
        _cancel = new();
        try
        {
            _request = Capture();
            _stage = Path.Combine(Path.GetTempPath(), "urman-house-" + _request.RequestId);
            Directory.CreateDirectory(_stage);
            File.WriteAllText(Path.Combine(_stage, "request.json"), JsonSerializer.Serialize(_request));
            AtomicFile.WriteAllText(Path.Combine(_studio.Workspace.Root, ".urman-studio/ai-jobs", _request.RequestId, "request.json"), JsonSerializer.Serialize(_request));
            _status.Text = "Codex готовит предложение…";
            _proposal = await new CodexProposalProvider(allowGlobalInstructions: _globalInstructions.ButtonPressed)
                .RunAsync(_request, _stage, _cancel.Token);
            if (!Visible) throw new OperationCanceledException();
            File.WriteAllText(Path.Combine(_stage, "proposal.json"), JsonSerializer.Serialize(_proposal));
            AtomicFile.WriteAllText(Path.Combine(_studio.Workspace.Root, ".urman-studio/ai-jobs", _request.RequestId, "proposal.json"), JsonSerializer.Serialize(_proposal));
            var recipe = JsonNode.Parse(_recipeText!)!.AsObject();
            recipe["windowClearWidth"] = _proposal.WindowWidth;
            recipe["modelPath"] = "res://assets/models/studio/hero-house-" + _request.RequestId + ".glb";
            File.WriteAllText(Path.Combine(_stage, "recipe.json"), recipe.ToJsonString(new() { WriteIndented = true }));
            _status.Text = "Создаю настоящий проём и согласованный интерьер…";
            await GenerateAsync(_stage, _cancel.Token);
            ValidateStage();
            ClearPreviewModels();
            _beforeModel = LoadCurrentHousePreview();
            _previewModels.AddChild(_beforeModel);
            _afterModel = LoadHousePreview(Path.Combine(_stage, "hero_house.glb"));
            _previewModels.AddChild(_afterModel);
            _beforeView.Disabled = false;
            _afterView.Disabled = false;
            ShowPreview(after: true);
            _status.Text = $"{_proposal.Summary}\nШирина: {_proposal.ExpectedWidth:0.00} → {_proposal.WindowWidth:0.00} м. Только выбранный дом; 7 реальных проёмов. Ещё не применено.";
            if (_consentIoWarning is not null) _status.Text += "\n" + _consentIoWarning;
            _apply.Disabled = false;
        }
        catch (OperationCanceledException) { ClearProposal(); _status.Text = "Остановлено. Проект не изменён."; }
        catch (Exception error) { ClearProposal(); _status.Text = "Не применено: " + error.Message; }
        finally { _cancel.Dispose(); _cancel = null; SetBusy(false); }
    }

    private async Task GenerateAsync(string stage, CancellationToken cancellation)
    {
        var blender = Path.Combine(_studio.Workspace.Root, ".tools/blender/Blender.app/Contents/MacOS/Blender");
        if (!File.Exists(blender)) throw new InvalidOperationException("Нет закреплённого Blender. Запрос и исходный дом сохранены.");
        var start = new ProcessStartInfo(blender) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = _studio.Workspace.Root };
        foreach (var argument in new[] { "--background", "--factory-startup", "--python", GeneratorPath, "--", "--root", _studio.Workspace.Root, "--component-only", "hero-house", "--recipe", Path.Combine(stage, "recipe.json"), "--output-dir", stage }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Blender не запустился.");
        var output = process.StandardOutput.ReadToEndAsync(cancellation);
        var errors = process.StandardError.ReadToEndAsync(cancellation);
        try { await process.WaitForExitAsync(cancellation); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        await output; await errors;
        if (process.ExitCode != 0) throw new IOException("Blender не завершил сборку; последняя принятая модель не изменена.");
    }

    private async Task DoctorAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var doctor = await new CodexProposalProvider().CheckAsync();
            var version = doctor.Version is { Length: > 0 } value ? $"Codex {value}" : "Codex";
            _status.Text = !doctor.ExecutableFound ? "Codex не найден. Установите приложение или CLI и войдите через ChatGPT."
                : doctor.LoginStatus switch
                {
                    CodexLoginStatus.LoggedIn => $"{version}: выполнен вход. Доступ к модели проверяется запросом; API-платежи не подключаются.",
                    CodexLoginStatus.NotLoggedIn => $"{version}: вход не выполнен. Войдите через ChatGPT в Codex.",
                    _ => $"{version}: статус входа неизвестен. Запрос проверит доступ к аккаунту и модели; API-платежи не подключаются."
                };
        }
        catch (Exception error) { _status.Text = "Codex не готов: " + error.Message; }
        finally { SetBusy(false); }
    }

    private async Task ApplyAsync()
    {
        if (_busy || _proposal is null || _request is null || _stage is null) return;
        SetBusy(true);
        try
        {
            if (_snapshot != Fingerprint(File.ReadAllText(Path.Combine(_studio.Workspace.Root, RecipePath))))
                throw new InvalidOperationException("Исходник изменился после запроса. Получите новое предложение.");
            ValidateStage();
            var model = "game/assets/models/studio/hero-house-" + _request.RequestId + ".glb";
            var blend = "assets/source/blender/studio/hero-house-" + _request.RequestId + ".blend";
            var manifest = model + ".manifest.json";
            var writes = new StudioFileWrite[] {
                new(RecipePath, AtomicFile.Sha256(System.Text.Encoding.UTF8.GetBytes(_recipeText!)), File.ReadAllBytes(Path.Combine(_stage, "recipe.json"))),
                new(model, null, File.ReadAllBytes(Path.Combine(_stage, "hero_house.glb"))),
                new(blend, null, File.ReadAllBytes(Path.Combine(_stage, "hero_house.blend"))),
                new(manifest, null, File.ReadAllBytes(Path.Combine(_stage, "manifest.json")))
            };
            await _studio.Session.ApplyFilesAsync("изменить окна дома через Codex", HeroHouseRecipe.EntityId, writes,
                new HashSet<string>(writes.Select(write => write.RelativePath), StringComparer.Ordinal), ImportAssets,
                () => ((StudioWorldSection)_studio.Section("world")).ReloadGeometryAsync());
            _status.Text = "Окна изменены. ⌘/Ctrl+Z вернёт исходные проёмы, рецепт и модель; повторить можно через Redo без Codex.";
            ClearProposal();
        }
        catch (Exception error)
        {
            _status.Text = "Не применено: " + error.Message;
        }
        finally { SetBusy(false); }
    }

    private void ImportAssets()
    {
        // The runtime uses the same native glTF parser; no partially imported cached resource is promoted.
        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(_studio.Workspace.Root, RecipePath)))!;
        if ((string?)recipe["modelPath"] is not { } modelPath) return;
        var fullPath = Path.Combine(_studio.Workspace.Root, "game", modelPath["res://".Length..]);
        if (new GltfDocument().AppendFromFile(fullPath, new GltfState()) != Error.Ok)
            throw new IOException("Импорт модели не прошёл; возвращаю предыдущую версию.");
    }

    private Node3D LoadCurrentHousePreview()
    {
        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(_studio.Workspace.Root, RecipePath)))?.AsObject()
            ?? throw new InvalidDataException("Рецепт эталонного дома не является JSON-объектом.");
        var modelPath = (string?)recipe["modelPath"];
        string fullPath;
        if (modelPath is null)
        {
            fullPath = Path.Combine(_studio.Workspace.Root, "game/assets/models/act1/urman_village_exterior_kit.glb");
        }
        else
        {
            const string prefix = "res://assets/models/studio/hero-house-";
            if (!modelPath.StartsWith(prefix, StringComparison.Ordinal)
                || !modelPath.EndsWith(".glb", StringComparison.Ordinal)
                || modelPath.AsSpan(prefix.Length).Contains('/')
                || modelPath.Contains("..", StringComparison.Ordinal)
                || modelPath.Contains('\\'))
                throw new InvalidDataException("В рецепте указан недопустимый путь текущей модели.");
            fullPath = Path.Combine(_studio.Workspace.Root, "game", modelPath["res://".Length..]);
        }

        return LoadHousePreview(fullPath);
    }

    private static Node3D LoadHousePreview(string path)
    {
        if (!File.Exists(path)) throw new IOException("Текущая или новая модель дома не найдена для предпросмотра.");
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromFile(path, state) != Error.Ok)
            throw new InvalidOperationException("Модель дома не прошла импорт предпросмотра.");
        if (document.GenerateScene(state) is not Node3D scene)
            throw new InvalidDataException("Модель дома не имеет 3D-корня.");

        var components = scene.Name == HeroHouseRecipe.Component
            ? new[] { scene }
            : scene.FindChildren(HeroHouseRecipe.Component, nameof(Node3D), true, false).OfType<Node3D>().ToArray();
        if (components.Length != 1)
        {
            scene.Free();
            throw new InvalidDataException("В модели не найден единственный компонент HeroHouse_TimberPlaster.");
        }

        var component = components[0];
        var importedBasis = ComposeImportedBasis(component);
        if (component != scene)
        {
            var parent = component.GetParent();
            if (parent is null)
            {
                scene.Free();
                throw new InvalidDataException("У компонента дома отсутствует импортированный родитель.");
            }
            parent.RemoveChild(component);
            scene.Free();
        }

        ClearSceneOwnership(component);
        component.Transform = new Transform3D(importedBasis, Vector3.Zero);
        component.Visible = false;
        return component;
    }

    private static Basis ComposeImportedBasis(Node3D component)
    {
        var basis = component.Transform.Basis;
        for (var ancestor = component.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (ancestor is Node3D node) basis = node.Transform.Basis * basis;
        }
        return basis;
    }

    private static void ClearSceneOwnership(Node node)
    {
        node.Owner = null;
        foreach (var child in node.GetChildren()) ClearSceneOwnership(child);
    }

    private void ShowPreview(bool after)
    {
        if (_beforeModel is null || _afterModel is null) return;
        _beforeModel.Visible = !after;
        _afterModel.Visible = after;
        _beforeView.Text = after ? "До" : "● До";
        _afterView.Text = after ? "● После" : "После";
        _previewCamera.Current = true;
    }

    private void ClearPreviewModels()
    {
        foreach (var child in _previewModels.GetChildren())
        {
            _previewModels.RemoveChild(child);
            child.QueueFree();
        }
        _beforeModel = null;
        _afterModel = null;
        _beforeView.Disabled = true;
        _afterView.Disabled = true;
        _beforeView.Text = "До";
        _afterView.Text = "После";
    }

    private void ValidateStage()
    {
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(_stage!, "manifest.json")))!;
        if ((string?)manifest["schema"] != "hero-house-build-manifest/v1"
            || (string?)manifest["entityId"] != HeroHouseRecipe.EntityId
            || (string?)manifest["component"] != HeroHouseRecipe.Component
            || (string?)manifest["modelPath"] != "res://assets/models/studio/hero-house-" + _request!.RequestId + ".glb"
            || (decimal?)manifest["windowClearHeight"] != 1.40m
            || (decimal?)manifest["windowClearWidth"] != _proposal!.WindowWidth)
            throw new InvalidDataException("Сборка не соответствует принятому предложению.");
        foreach (var (key, name) in new[] { ("glb", "hero_house.glb"), ("blend", "hero_house.blend") })
            if ((string?)manifest["outputs"]?[key]?["sha256"] != AtomicFile.Sha256OfFile(Path.Combine(_stage!, name)))
                throw new InvalidDataException("Артефакт изменился после сборки: " + name);
        if ((string?)manifest["source"]?["recipe"]?["sha256"] != AtomicFile.Sha256OfFile(Path.Combine(_stage!, "recipe.json")))
            throw new InvalidDataException("Рецепт изменился после сборки.");
        foreach (var (key, path) in new[] { ("generator", GeneratorPath), ("baselineBlend", "assets/source/blender/act1/urman_village_exterior_kit.blend") })
            if ((string?)manifest["source"]?[key]?["sha256"] != AtomicFile.Sha256OfFile(Path.Combine(_studio.Workspace.Root, path)))
                throw new InvalidDataException("Исходник изменился после сборки: " + path);
    }

    private void SetBusy(bool busy) { _busy = busy; _ask.Disabled = busy || _studio.Selection != HeroHouseRecipe.EntityId; if (busy) _apply.Disabled = true; }
    private void ClearProposal()
    {
        _proposal = null;
        _apply.Disabled = true;
        ClearPreviewModels();
        if (_stage is not null)
        {
            try { Directory.Delete(_stage, recursive: true); } catch (IOException) { }
            _stage = null;
        }
    }
}
