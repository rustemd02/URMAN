using System.Text.Json;
using Urman.Content.Compilation;
using Urman.Content.Reporting;
using Urman.Content.Simulation;
using Urman.Content.Validation;

var command = args.FirstOrDefault() ?? "validate";
var root = ResolveOption(args, "--root") ?? Directory.GetCurrentDirectory();

return command switch
{
    "validate" => await ValidateAsync(root),
    "inspect" => await InspectAsync(root, ResolveOption(args, "--campaign") ?? "urman.chapter1"),
    "report" => await ReportAsync(root),
    "compile" => await CompileAsync(
        root,
        ResolveOption(args, "--campaign") ?? "urman.chapter1",
        ResolveOptions(args, "--module"),
        ResolveOption(args, "--out")),
    "simulate" => await SimulateAsync(
        root,
        ResolveOption(args, "--campaign") ?? "urman.chapter1",
        ResolveOptions(args, "--module")),
    _ => Usage(command)
};

static async Task<int> ValidateAsync(string root)
{
    var result = await new ContentWorkspaceValidator().ValidateAsync(root);
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.SourcePath}{diagnostic.JsonPointer}: {diagnostic.Message}");
    }

    if (!result.IsValid)
    {
        return 1;
    }

    Console.WriteLine($"checked {result.CheckedModules} modules, {result.CheckedCampaigns} campaigns");
    return 0;
}

static async Task<int> InspectAsync(string root, string campaignId)
{
    var path = Path.Combine(root, "content", "campaigns", campaignId, "campaign.json");
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Campaign not found: {campaignId}");
        return 1;
    }

    await using var stream = File.OpenRead(path);
    using var document = await JsonDocument.ParseAsync(stream);
    var campaign = document.RootElement;
    Console.WriteLine($"campaign: {campaign.GetProperty("id").GetString()}@{campaign.GetProperty("exactVersion").GetString()}");
    Console.WriteLine($"entrypoint: {campaign.GetProperty("entrypoint").GetString()}");
    Console.WriteLine($"modules: {campaign.GetProperty("modules").GetArrayLength()}");
    Console.WriteLine($"narrative beats: {campaign.GetProperty("narrativeOrder").GetArrayLength()}");
    return 0;
}

static async Task<int> ReportAsync(string root)
{
    var result = await new ContentWorkspaceValidator().ValidateAsync(root);
    var audio = await new AudioProductionReporter().InspectAsync(root);
    Console.WriteLine($"valid: {result.IsValid.ToString().ToLowerInvariant()}");
    Console.WriteLine($"modules: {result.CheckedModules}");
    Console.WriteLine($"campaigns: {result.CheckedCampaigns}");
    Console.WriteLine($"diagnostics: {result.Diagnostics.Count}");
    Console.WriteLine($"audio campaigns inspected: {audio.CampaignsInspected}");
    Console.WriteLine($"audio assets: {audio.Assets.Count}");
    Console.WriteLine($"audio authored files: {audio.AuthoredFileCount}");
    Console.WriteLine($"audio logical refs: {audio.LogicalReferenceCount}");
    Console.WriteLine($"audio missing files: {audio.MissingFileCount}");
    Console.WriteLine($"audio caption closure: {audio.Assets.Count(asset => asset.CaptionClosed)}/{audio.Assets.Count}");
    Console.WriteLine($"audio transcript closure: {audio.Assets.Count(asset => asset.TranscriptClosed)}/{audio.Assets.Count}");
    Console.WriteLine($"audio ambient stems: {audio.AmbientStems.Count} ({audio.AmbientStems.Count(stem => stem.PhysicalFileExists)} physical)");
    Console.WriteLine($"audio authoring status: {(audio.HasOpenAuthoring ? "OPEN" : "READY")}");
    foreach (var asset in audio.Assets.OrderBy(asset => asset.CampaignId, StringComparer.Ordinal)
                 .ThenBy(asset => asset.AssetId, StringComparer.Ordinal)
                 .ThenBy(asset => asset.VariantId, StringComparer.Ordinal))
    {
        var variant = asset.VariantId is null ? string.Empty : $"[{asset.VariantId}]";
        Console.WriteLine(
            $"audio asset: {asset.CampaignId}/{asset.AssetId}{variant} status={asset.Status} captions={(asset.CaptionClosed ? "closed" : "open")} transcript={(asset.TranscriptClosed ? "closed" : "open")}");
    }

    foreach (var diagnostic in audio.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.SourcePath}{diagnostic.JsonPointer}: {diagnostic.Message}");
    }

    return result.IsValid && audio.Diagnostics.Count == 0 ? 0 : 1;
}

static async Task<int> CompileAsync(string root, string campaign, IReadOnlyCollection<string> modules, string? outputPath)
{
    var result = await new ContentCompiler().CompileAsync(root, campaign, modules);
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.SourcePath}{diagnostic.JsonPointer}: {diagnostic.Message}");
    }

    if (!result.IsSuccess)
    {
        return 1;
    }

    var output = CompiledPackText.Serialize(result.Pack!);
    if (outputPath is null)
    {
        Console.Write(output);
    }
    else
    {
        var absoluteOutputPath = Path.GetFullPath(Path.Combine(root, outputPath));
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath)!);
        await File.WriteAllTextAsync(absoluteOutputPath, output);
    }

    return 0;
}

static async Task<int> SimulateAsync(string root, string campaign, IReadOnlyCollection<string> modules)
{
    var compilation = await new ContentCompiler().CompileAsync(root, campaign, modules);
    foreach (var diagnostic in compilation.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.SourcePath}{diagnostic.JsonPointer}: {diagnostic.Message}");
    }

    if (!compilation.IsSuccess)
    {
        return 1;
    }

    var report = await new CampaignSimulator().SimulateNarrativeOrderAsync(compilation.Pack!);
    Console.WriteLine($"campaign: {report.CampaignId}");
    Console.WriteLine($"steps: {report.Steps}");
    Console.WriteLine($"event sequence: {report.EventSequence}");
    Console.WriteLine($"state sha256: {report.StateSha256}");
    return 0;
}

static string? ResolveOption(IReadOnlyList<string> arguments, string name)
{
    for (var index = 0; index < arguments.Count - 1; index++)
    {
        if (arguments[index] == name)
        {
            return arguments[index + 1];
        }
    }

    return null;
}

static IReadOnlyList<string> ResolveOptions(IReadOnlyList<string> arguments, string name)
{
    var values = new List<string>();
    for (var index = 0; index < arguments.Count - 1; index++)
    {
        if (arguments[index] == name)
        {
            values.Add(arguments[index + 1]);
        }
    }

    return values;
}

static int Usage(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Console.Error.WriteLine("Usage: urman-content <validate|compile|inspect|simulate|report> [--root path] [--campaign id] [--module id] [--out path]");
    return 2;
}
