using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One step of a trial run, with what was actually found for it.</summary>
public sealed record TrialStep(string Order, string Step, string State, string Detail, bool Blocking);

/// <summary>
/// The first slice of the explaining-trial-run half of the spec AI-40 slice. A trial run
/// is not a black box: it resolves a campaign, reads that campaign's manifest and its
/// modules, compiles them into that campaign's pack, loads the chosen world scene, and
/// starts the run there. This projection walks exactly those inputs and reports each one
/// as a step with the state it really has, so an author sees WHICH input is missing
/// instead of a launch that fails for an unexplained reason.
///
/// Two limits are stated rather than hidden: whether a pack is STALE can only be decided
/// by compiling it, and whether the world actually loads can only be decided by the game.
/// </summary>
public static class TrialRunPlan
{
    public const string CampaignsFolder = "content/campaigns";

    public static IReadOnlyList<TrialStep> Steps(string root, string? campaignSelection, string? worldScenePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var steps = new List<TrialStep>();

        // 1. The campaign the author picked has to be resolved first; an unresolved
        // selection is not substituted with a default.
        if (string.IsNullOrWhiteSpace(campaignSelection))
        {
            steps.Add(new("1", "Выбор кампании", "не разрешён", "Кампания не выбрана: запуск отменяется, Act I по умолчанию не подставляется.", true));
            return steps;
        }

        var selection = campaignSelection.Trim();
        steps.Add(new("1", "Выбор кампании", "готово", $"Выбрана кампания {selection}.", false));

        // 2. The manifest names the modules the campaign is made of.
        var manifestPath = Path.Combine(full, CampaignsFolder.Replace('/', Path.DirectorySeparatorChar), selection, "campaign.json");
        if (!File.Exists(manifestPath))
        {
            steps.Add(new("2", "Манифест кампании", "нет файла", $"Не найден {CampaignsFolder}/{selection}/campaign.json: компилировать нечего.", true));
            return steps;
        }

        var modules = new List<string>();
        if (JsonNode.Parse(File.ReadAllText(manifestPath)) is JsonObject manifest && manifest["modules"] is JsonArray declared)
        {
            modules.AddRange(declared.OfType<JsonObject>().Select(module => AuthoredJson.Text(module, "moduleId")).OfType<string>());
        }

        if (modules.Count == 0)
        {
            steps.Add(new("2", "Манифест кампании", "пустой", "Манифест не перечисляет модулей: пак соберётся пустым.", true));
        }
        else
        {
            var missing = modules
                .Where(module => !Directory.Exists(Path.Combine(full, "content", "modules", RuleCatalogCoverage.ModuleFolder(module))))
                .ToArray();
            steps.Add(missing.Length == 0
                ? new("2", "Манифест кампании", "готово", $"Модулей {modules.Count}, все каталоги на месте.", false)
                : new("2", "Манифест кампании", "не хватает модулей", $"Нет каталогов модулей: {string.Join(", ", missing)}.", true));
        }

        // 3. The pack the run loads must belong to THIS campaign.
        var packRelative = $"game/content/{selection}.compiled.v1.json";
        var packPath = Path.Combine(full, packRelative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(packPath))
        {
            steps.Add(new("3", "Скомпилированный пак", "нет файла", $"Нет {packRelative}: его создаст компиляция перед запуском.", false));
        }
        else
        {
            var fingerprint = JsonNode.Parse(File.ReadAllText(packPath))?["campaignFingerprint"]?.GetValue<string>() ?? "";
            steps.Add(new("3", "Скомпилированный пак", "есть",
                fingerprint.Length >= 8
                    ? $"{packRelative}, отпечаток {fingerprint[..8]}. Свежесть пакета решает только компиляция."
                    : $"{packRelative} без отпечатка кампании: пак собран другой версией и будет перезаписан.", false));
        }

        // 4. The world scene the run starts in.
        if (string.IsNullOrWhiteSpace(worldScenePath))
        {
            steps.Add(new("4", "Сцена мира", "не выбрана", "Мир не выбран: запускать нечего, даже если пак собран.", true));
            return steps;
        }

        var scene = worldScenePath.Trim();
        var scenePath = Path.Combine(full, scene.Replace('/', Path.DirectorySeparatorChar));
        steps.Add(File.Exists(scenePath)
            ? new("4", "Сцена мира", "готово", $"{scene} на месте.", false)
            : new("4", "Сцена мира", "нет файла", $"Не найден {scene}: запуск не сможет загрузить мир.", true));

        // 5. What remains is the game itself: the plan stops here honestly.
        steps.Add(new("5", "Запуск игры", "вне проверки",
            "Дальше идёт сам запуск: загрузку мира, спавн и кадр эта проекция не проверяет — это делает игра.", false));
        return steps;
    }

    public static IReadOnlyList<string> Notes(string root, string? campaignSelection, string? worldScenePath)
    {
        var steps = Steps(root, campaignSelection, worldScenePath);
        var blocking = steps.Count(step => step.Blocking);
        return
        [
            $"Шагов пробного запуска: {steps.Count}; из них упираются в отсутствующий вход: {blocking}.",
            "Шаги идут по реальному пути запуска: выбрать кампанию → прочитать её манифест и модули → собрать пак ИМЕННО этой кампании → загрузить выбранную сцену мира → запустить игру.",
            "Пак ищется по имени кампании, поэтому второй мир не подменится паком первого; пустой выбор кампании не подменяется значением по умолчанию.",
            "Свежесть пакета и то, как мир выглядит и играется, проекция не доказывает: это решает компиляция и игра соответственно."
        ];
    }
}
