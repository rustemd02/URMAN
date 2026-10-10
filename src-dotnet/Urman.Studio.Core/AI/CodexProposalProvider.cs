using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Urman.Studio.Core.AI;

public enum CodexLoginStatus
{
    Unknown,
    LoggedIn,
    NotLoggedIn
}

/// <summary>A secret-free summary of the installed Codex CLI and its login state.</summary>
public sealed record CodexDoctorSummary(bool ExecutableFound, string? Version, CodexLoginStatus LoginStatus);

public enum CodexProposalStage
{
    Preparing,
    Generating,
    Validating
}

public sealed record CodexProposalProgress(CodexProposalStage Stage, string Message);

/// <summary>
/// Runs one structured, proposal-only Codex turn. It never receives a StudioWorkspace,
/// reads snapshot files, or applies edits to a game document.
/// </summary>
public sealed class CodexProposalProvider
{
    private const string Model = "gpt-6-luna";
    private const string ReasoningEffort = "max";
    private const int MaximumLineBytes = 512 * 1024;
    private const int MaximumDoctorOutputCharacters = 16 * 1024;
    private static readonly TimeSpan DoctorTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProposalTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan InterruptTimeout = TimeSpan.FromSeconds(3);

    // Keep these explicit and fail closed if a newer app-server stops honoring them.
    private static readonly string[] DisabledFeatures =
    [
        "shell_tool",
        "unified_exec",
        "unified_exec_tty",
        "shell_snapshot",
        "shell_snapshot_v2",
        "apps",
        "hooks",
        "multi_agent",
        "multi_agent_v2",
        "multi_agent_v2_dynamic_tools",
        "plugins",
        "remote_plugin",
        "browser_use",
        "browser_use_external",
        "browser_use_full_cdp_access",
        "computer_use",
        "image_generation",
        "view_image",
        "sleep_tool",
        "skill_search",
        "workspace_dependencies",
        "tool_suggest",
        "in_app_browser",
        "in_app_local_automation",
        "in_app_chat",
        "skill_mcp_dependency_install",
        "tool_call_mcp_elicitation",
        "auth_elicitation",
        "worktrees",
        "code_mode",
        "code_mode_host",
        "code_mode_tool_search",
        "browser_annotation_api",
        "incremental_tools",
        "standalone_web_search"
    ];

    // The installed CLI currently reports these enabled features. The names below
    // are the complete, reviewed set that is harmless for proposal-only operation.
    // unified_exec is handled separately because this CLI reports it enabled even
    // when disabled; the request supplies no execution environments and any
    // command/tool item remains a protocol failure.
    private static readonly HashSet<string> KnownEnabledFeatures = new(StringComparer.Ordinal)
    {
        "unified_exec_zsh_fork",
        "tool_search_always_defer_mcp_tools",
        "plugin_sharing",
        "resize_all_images",
        "compaction_image_budget"
    };

    private static readonly JsonSerializerOptions WireJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string? _executablePath;
    private readonly string? _codexHome;
    private readonly bool _allowGlobalInstructions;

    /// <param name="codexHome">Optional isolated Codex profile directory. Credentials are never copied or read by Studio.</param>
    /// <param name="allowGlobalInstructions">Explicitly allow the single AGENTS.md file at this profile's Codex home.</param>
    public CodexProposalProvider(
        string? executablePath = null,
        string? codexHome = null,
        bool allowGlobalInstructions = false)
    {
        _executablePath = ResolveExecutable(executablePath ?? FindExecutable());
        if (!string.IsNullOrWhiteSpace(codexHome) && !Path.IsPathRooted(codexHome))
        {
            throw new ArgumentException("Codex home must be an absolute path.", nameof(codexHome));
        }

        _codexHome = string.IsNullOrWhiteSpace(codexHome) ? null : Path.GetFullPath(codexHome);
        _allowGlobalInstructions = allowGlobalInstructions;
    }

    /// <summary>Checks the executable, version, and sign-in state without starting a model turn.</summary>
    public async Task<CodexDoctorSummary> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_executablePath is null || !File.Exists(_executablePath))
        {
            return new CodexDoctorSummary(false, null, CodexLoginStatus.Unknown);
        }

        string? version = null;
        var versionResult = await RunShortCommandAsync(["--version"], cancellationToken).ConfigureAwait(false);
        if (versionResult is { ExitCode: 0 }) version = ParseVersion(versionResult.Output);

        var login = CodexLoginStatus.Unknown;
        var loginResult = await RunShortCommandAsync(["login", "status"], cancellationToken).ConfigureAwait(false);
        if (loginResult is { ExitCode: 0 }) login = ParseLoginStatus(loginResult.Output);

        return new CodexDoctorSummary(true, version, login);
    }

    /// <summary>
    /// Returns a validated <c>house.windows.resize</c> proposal from the immutable input.
    /// The snapshot directory must be an isolated child of the OS temp directory.
    /// </summary>
    public async Task<HouseWindowProposal> RunAsync(
        HouseWindowRequest request,
        string workingSnapshotDirectory,
        CancellationToken cancellationToken = default,
        IProgress<CodexProposalProgress>? progress = null)
    {
        HouseWindowProposalValidator.ValidateRequest(request);
        var workingDirectory = ValidateWorkingDirectory(workingSnapshotDirectory);
        if (_executablePath is null || !File.Exists(_executablePath))
        {
            throw new CodexProposalException("Codex CLI was not found. Use Doctor to check its installation.");
        }

        Report(progress, CodexProposalStage.Preparing, "Preparing a restricted Codex session.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProposalTimeout);

        using var process = new Process { StartInfo = BuildAppServerStartInfo(workingDirectory) };
        try
        {
            if (!process.Start()) throw new CodexProposalException("Codex app-server could not be started.");
        }
        catch (CodexProposalException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            throw new CodexProposalException("Codex app-server could not be started.");
        }

        var errorDrain = DrainAsync(process.StandardError);
        var client = new AppServerClient(process, MaximumLineBytes);
        var threadId = (string?)null;
        var turnId = (string?)null;
        var turnWasStarted = false;
        try
        {
            var token = timeout.Token;
            await client.RequestAsync("initialize", new Dictionary<string, object?>
            {
                ["clientInfo"] = new Dictionary<string, object?>
                {
                    ["name"] = "urman-studio-house-window-proposal",
                    ["title"] = "URMAN Studio",
                    ["version"] = "1.0"
                },
                ["capabilities"] = new Dictionary<string, object?> { ["experimentalApi"] = true }
            }, 1, null, token).ConfigureAwait(false);
            await client.NotifyAsync("initialized", new Dictionary<string, object?>(), token).ConfigureAwait(false);

            var accountResponse = await client.RequestAsync("account/read", new Dictionary<string, object?>
            {
                ["refreshToken"] = false
            }, 2, null, token).ConfigureAwait(false);
            VerifyChatGptAccount(accountResponse);
            await VerifyEffectiveFeaturesAsync(client, token).ConfigureAwait(false);

            var threadResponse = await client.RequestAsync("thread/start", new Dictionary<string, object?>
            {
                ["ephemeral"] = true,
                ["cwd"] = workingDirectory,
                ["sandbox"] = "read-only",
                ["approvalPolicy"] = "never",
                ["modelProvider"] = "openai",
                ["environments"] = Array.Empty<object>(),
                ["runtimeWorkspaceRoots"] = Array.Empty<string>()
            }, 100, HandleStartupNotificationAsync, token).ConfigureAwait(false);

            var thread = GetRequiredObject(threadResponse, "thread");
            threadId = GetRequiredString(thread, "id");
            if (!string.Equals(GetRequiredString(threadResponse, "modelProvider"), "openai", StringComparison.Ordinal))
            {
                throw new CodexProposalException("Codex did not select the approved ChatGPT provider.");
            }

            VerifyThreadBoundary(threadResponse, thread, workingDirectory);

            Report(progress, CodexProposalStage.Generating, "Generating one structured resize proposal.");
            var terminalTurn = (JsonElement?)null;
            var turnStart = new Dictionary<string, object?>
            {
                ["threadId"] = threadId,
                ["model"] = Model,
                ["effort"] = ReasoningEffort,
                ["approvalPolicy"] = "never",
                ["cwd"] = workingDirectory,
                ["environments"] = Array.Empty<object>(),
                ["runtimeWorkspaceRoots"] = Array.Empty<string>(),
                ["sandboxPolicy"] = new Dictionary<string, object?> { ["type"] = "readOnly", ["networkAccess"] = false },
                ["input"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["type"] = "text",
                        ["text"] = BuildPrompt(request)
                    }
                },
                ["outputSchema"] = BuildOutputSchema(request)
            };

            turnWasStarted = true;
            var turnResponse = await client.RequestAsync("turn/start", turnStart, 101,
                message => HandleTurnNotificationAsync(message, threadId, () => turnId, value => turnId = value, value => terminalTurn = value), token).ConfigureAwait(false);
            var startedTurn = GetRequiredObject(turnResponse, "turn");
            var responseTurnId = GetRequiredString(startedTurn, "id");
            if (turnId is not null && !string.Equals(turnId, responseTurnId, StringComparison.Ordinal))
            {
                throw new ProtocolBoundaryException();
            }

            turnId = responseTurnId;
            if (terminalTurn is null)
            {
                terminalTurn = await WaitForTerminalTurnAsync(client, threadId, turnId, HandleTurnNotificationAsync, () => turnId, value => turnId = value, token).ConfigureAwait(false);
            }

            Report(progress, CodexProposalStage.Validating, "Validating proposal identity and window width.");
            var proposalText = ExtractFinalAgentMessage(terminalTurn.Value);
            var proposal = ParseProposal(proposalText, request);
            return proposal;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (turnWasStarted && threadId is not null && turnId is not null)
            {
                await TryInterruptAsync(client, threadId, turnId).ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            throw new CodexProposalException("Codex proposal timed out.");
        }
        catch (ProtocolBoundaryException)
        {
            if (turnWasStarted && threadId is not null && turnId is not null)
            {
                await TryInterruptAsync(client, threadId, turnId).ConfigureAwait(false);
            }

            throw new CodexProposalException("Codex attempted an unavailable tool or unexpected action; the proposal was discarded.");
        }
        catch (CodexProposalException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (turnWasStarted && threadId is not null && turnId is not null)
            {
                await TryInterruptAsync(client, threadId, turnId).ConfigureAwait(false);
            }

            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            if (turnWasStarted && threadId is not null && turnId is not null)
            {
                await TryInterruptAsync(client, threadId, turnId).ConfigureAwait(false);
            }

            throw new CodexProposalException("Codex did not complete a valid house-window proposal.");
        }
        finally
        {
            KillProcessTree(process);
            try { await errorDrain.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException) { }
        }
    }

    private ProcessStartInfo BuildAppServerStartInfo(string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath!,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Every tool family stays explicitly disabled.
        startInfo.ArgumentList.Add("app-server");
        startInfo.ArgumentList.Add("--listen");
        startInfo.ArgumentList.Add("stdio://");
        startInfo.ArgumentList.Add("--strict-config");
        foreach (var feature in DisabledFeatures)
        {
            startInfo.ArgumentList.Add("--disable");
            startInfo.ArgumentList.Add(feature);
        }

        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("mcp_servers={}");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("web_search=\"disabled\"");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("project_doc_max_bytes=0");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("model_provider=\"openai\"");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("forced_login_method=\"chatgpt\"");
        ApplyCodexHome(startInfo);
        return startInfo;
    }

    private static void VerifyChatGptAccount(JsonElement response)
    {
        if (!response.TryGetProperty("account", out var account) || account.ValueKind != JsonValueKind.Object ||
            !account.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
            !string.Equals(type.GetString(), "chatgpt", StringComparison.Ordinal))
        {
            throw new CodexProposalException("Codex must be signed in with a ChatGPT account before proposal mode is available.");
        }
    }

    private async Task VerifyEffectiveFeaturesAsync(AppServerClient client, CancellationToken cancellationToken)
    {
        var enabledFeatures = new HashSet<string>(StringComparer.Ordinal);
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        var featureNames = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        var pages = 0;
        do
        {
            if (++pages > 8) throw new CodexProposalException("Codex exposes an unsupported feature configuration.");
            var listParameters = new Dictionary<string, object?> { ["limit"] = 200 };
            if (cursor is not null) listParameters["cursor"] = cursor;
            var response = await client.RequestAsync("experimentalFeature/list", listParameters, 10 + pages, null, cancellationToken).ConfigureAwait(false);

            if (!response.TryGetProperty("data", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                throw new CodexProposalException("Codex feature configuration could not be verified.");
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object ||
                    !row.TryGetProperty("name", out var nameValue) || nameValue.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(nameValue.GetString()) ||
                    !row.TryGetProperty("enabled", out var enabledValue) || enabledValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                    !row.TryGetProperty("defaultEnabled", out var defaultEnabledValue) || defaultEnabledValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                    !row.TryGetProperty("stage", out var stageValue) || stageValue.ValueKind != JsonValueKind.String)
                {
                    throw new CodexProposalException("Codex feature configuration could not be verified.");
                }

                var name = nameValue.GetString()!;
                if (!featureNames.Add(name)) throw new CodexProposalException("Codex feature configuration could not be verified.");
                var stage = stageValue.GetString();
                if (stage is not ("beta" or "underDevelopment" or "stable" or "deprecated" or "removed"))
                {
                    throw new CodexProposalException("Codex feature configuration could not be verified.");
                }

                if (enabledValue.ValueKind == JsonValueKind.True && stage is not ("removed" or "deprecated"))
                {
                    enabledFeatures.Add(name);
                }
            }

            if (!response.TryGetProperty("nextCursor", out var nextValue) || nextValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
            {
                throw new CodexProposalException("Codex feature configuration could not be verified.");
            }

            cursor = nextValue.ValueKind == JsonValueKind.String ? nextValue.GetString() : null;
            if (cursor is not null && (string.IsNullOrWhiteSpace(cursor) || !cursors.Add(cursor)))
            {
                throw new CodexProposalException("Codex feature configuration could not be verified.");
            }
        } while (cursor is not null);

        // unified_exec is the only exception: this CLI reports it enabled even when
        // --disable is set. Empty per-turn environments and rejection of all command
        // items are required before the provider will accept that state.
        if (enabledFeatures.Any(feature =>
                !string.Equals(feature, "unified_exec", StringComparison.Ordinal) &&
                !KnownEnabledFeatures.Contains(feature)))
        {
            throw new CodexProposalException("Codex still has an enabled tool feature; proposal mode is unavailable.");
        }
    }

    private static Task HandleStartupNotificationAsync(JsonElement message)
    {
        var method = GetMethod(message);
        if (method == "thread/started") return Task.CompletedTask;
        if (method.StartsWith("item/", StringComparison.Ordinal) || method.Contains("diff", StringComparison.OrdinalIgnoreCase))
        {
            throw new ProtocolBoundaryException();
        }

        return Task.CompletedTask;
    }

    private static async Task<JsonElement> WaitForTerminalTurnAsync(
        AppServerClient client,
        string threadId,
        string expectedTurnId,
        Func<JsonElement, string, Func<string?>, Action<string>, Action<JsonElement>, Task> notificationHandler,
        Func<string?> getTurnId,
        Action<string> setTurnId,
        CancellationToken cancellationToken)
    {
        var terminal = (JsonElement?)null;
        while (terminal is null)
        {
            var message = await client.ReadMessageAsync(cancellationToken).ConfigureAwait(false);
            if (message.TryGetProperty("method", out _))
            {
                await notificationHandler(message, threadId, getTurnId, setTurnId, value => terminal = value).ConfigureAwait(false);
                continue;
            }

            throw new ProtocolBoundaryException();
        }

        var turn = GetRequiredObject(terminal.Value, "turn");
        if (!string.Equals(GetRequiredString(turn, "id"), expectedTurnId, StringComparison.Ordinal)) throw new ProtocolBoundaryException();
        return terminal.Value;
    }

    private static Task HandleTurnNotificationAsync(
        JsonElement message,
        string threadId,
        Func<string?> getTurnId,
        Action<string> setTurnId,
        Action<JsonElement> setTerminalTurn)
    {
        var method = GetMethod(message);
        var parameters = message.TryGetProperty("params", out var parameterValue) && parameterValue.ValueKind == JsonValueKind.Object
            ? parameterValue
            : throw new ProtocolBoundaryException();

        if (method == "thread/started") return Task.CompletedTask;
        if (method == "turn/started")
        {
            VerifyThreadId(parameters, threadId);
            var turn = GetRequiredObject(parameters, "turn");
            setTurnId(GetRequiredString(turn, "id"));
            VerifySupportedTurnItems(turn);
            return Task.CompletedTask;
        }

        if (method == "item/started" || method == "item/completed")
        {
            VerifyThreadId(parameters, threadId);
            var itemTurnId = GetRequiredString(parameters, "turnId");
            if (getTurnId() is { } currentTurnId && !string.Equals(itemTurnId, currentTurnId, StringComparison.Ordinal)) throw new ProtocolBoundaryException();
            setTurnId(itemTurnId);
            EnsureNonToolItem(GetRequiredObject(parameters, "item"));
            return Task.CompletedTask;
        }

        if (method == "item/agentMessage/delta")
        {
            VerifyThreadId(parameters, threadId);
            var delta = GetRequiredString(parameters, "delta");
            if (delta.Length > 16_384) throw new ProtocolBoundaryException();
            var deltaTurnId = GetRequiredString(parameters, "turnId");
            if (getTurnId() is { } currentTurnId && !string.Equals(deltaTurnId, currentTurnId, StringComparison.Ordinal)) throw new ProtocolBoundaryException();
            setTurnId(deltaTurnId);
            return Task.CompletedTask;
        }

        if (method == "turn/completed")
        {
            VerifyThreadId(parameters, threadId);
            var turn = GetRequiredObject(parameters, "turn");
            var finishedTurnId = GetRequiredString(turn, "id");
            if (getTurnId() is { } currentTurnId && !string.Equals(finishedTurnId, currentTurnId, StringComparison.Ordinal)) throw new ProtocolBoundaryException();
            setTurnId(finishedTurnId);
            VerifySupportedTurnItems(turn);
            setTerminalTurn(parameters.Clone());
            return Task.CompletedTask;
        }

        if (method.StartsWith("item/", StringComparison.Ordinal) || method.Contains("diff", StringComparison.OrdinalIgnoreCase) || method.Contains("tool", StringComparison.OrdinalIgnoreCase))
        {
            throw new ProtocolBoundaryException();
        }

        return Task.CompletedTask;
    }

    private void VerifyThreadBoundary(JsonElement response, JsonElement thread, string workingDirectory)
    {
        if (!string.Equals(GetRequiredString(thread, "cwd"), workingDirectory, PathComparison)) throw new ProtocolBoundaryException();
        if (!thread.TryGetProperty("ephemeral", out var ephemeral) || ephemeral.ValueKind != JsonValueKind.True) throw new ProtocolBoundaryException();
        if (!thread.TryGetProperty("environments", out var environments) || environments.ValueKind != JsonValueKind.Array || environments.GetArrayLength() != 0)
        {
            throw new ProtocolBoundaryException();
        }

        if (!response.TryGetProperty("instructionSources", out var instructionSources) || instructionSources.ValueKind != JsonValueKind.Array)
        {
            throw new ProtocolBoundaryException();
        }

        var sources = instructionSources.EnumerateArray().ToArray();
        if (sources.Length == 0) return;

        if (!_allowGlobalInstructions || sources.Length != 1 || !IsAllowedGlobalInstructionSource(sources[0]))
        {
            throw new CodexProposalException("Codex loaded instructions outside the isolated proposal input; the proposal was not sent.");
        }
    }

    private bool IsAllowedGlobalInstructionSource(JsonElement source)
    {
        if (source.ValueKind != JsonValueKind.String || source.GetString() is not { } sourcePath) return false;
        if (sourcePath.StartsWith("~/", StringComparison.Ordinal))
        {
            sourcePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), sourcePath[2..]);
        }

        if (!Path.IsPathRooted(sourcePath)) return false;
        var expected = Path.GetFullPath(Path.Combine(GetEffectiveCodexHome(), "AGENTS.md"));
        return string.Equals(Path.GetFullPath(sourcePath), expected, PathComparison);
    }

    private static void VerifySupportedTurnItems(JsonElement turn)
    {
        if (!turn.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) throw new ProtocolBoundaryException();
        foreach (var item in items.EnumerateArray()) EnsureNonToolItem(item);
    }

    private static void EnsureNonToolItem(JsonElement item)
    {
        var type = GetRequiredString(item, "type");
        if (type is not ("userMessage" or "agentMessage" or "reasoning")) throw new ProtocolBoundaryException();
    }

    private static void VerifyThreadId(JsonElement parameters, string expectedThreadId)
    {
        if (!string.Equals(GetRequiredString(parameters, "threadId"), expectedThreadId, StringComparison.Ordinal)) throw new ProtocolBoundaryException();
    }

    private static string BuildPrompt(HouseWindowRequest request)
    {
        var input = JsonSerializer.Serialize(request, WireJsonOptions);
        return "Propose exactly one operation of type house.windows.resize using only the immutable request JSON below. " +
               "Window widths are metres. Keep expectedWidth exactly equal to currentWidth. Choose a different windowWidth in the inclusive range 0.65–1.35 metres. " +
               "Do not use tools, read files, execute commands, change files, or perform any other action. Return only the structured result required by the schema.\n\n" +
               "Immutable request JSON:\n" + input;
    }

    private static Dictionary<string, object?> BuildOutputSchema(HouseWindowRequest request) => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = new[] { "operation", "requestId", "snapshotHash", "entityId", "expectedWidth", "windowWidth", "summary" },
        ["properties"] = new Dictionary<string, object?>
        {
            ["operation"] = new Dictionary<string, object?> { ["type"] = "string", ["const"] = HouseWindowProposal.Operation },
            ["requestId"] = new Dictionary<string, object?> { ["type"] = "string", ["const"] = request.RequestId },
            ["snapshotHash"] = new Dictionary<string, object?> { ["type"] = "string", ["const"] = request.SnapshotHash },
            ["entityId"] = new Dictionary<string, object?> { ["type"] = "string", ["const"] = request.EntityId },
            ["expectedWidth"] = new Dictionary<string, object?> { ["type"] = "number", ["const"] = request.CurrentWidth },
            ["windowWidth"] = new Dictionary<string, object?>
            {
                ["type"] = "number",
                ["minimum"] = HouseWindowProposalValidator.MinimumWidthMeters,
                ["maximum"] = HouseWindowProposalValidator.MaximumWidthMeters
            },
            ["summary"] = new Dictionary<string, object?> { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 500 }
        }
    };

    private static HouseWindowProposal ParseProposal(string json, HouseWindowRequest request)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new CodexProposalException("Codex returned an invalid structured proposal.");

        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!values.TryAdd(property.Name, property.Value)) throw new CodexProposalException("Codex returned an invalid structured proposal.");
        }

        string[] expectedProperties = ["operation", "requestId", "snapshotHash", "entityId", "expectedWidth", "windowWidth", "summary"];
        if (values.Count != expectedProperties.Length || expectedProperties.Any(property => !values.ContainsKey(property)))
        {
            throw new CodexProposalException("Codex returned an invalid structured proposal.");
        }

        if (ReadString(values["operation"]) != HouseWindowProposal.Operation)
        {
            throw new CodexProposalException("Codex returned an unsupported operation.");
        }

        var proposal = new HouseWindowProposal(
            ReadString(values["requestId"]),
            ReadString(values["snapshotHash"]),
            ReadString(values["entityId"]),
            ReadDecimal(values["expectedWidth"]),
            ReadDecimal(values["windowWidth"]),
            ReadString(values["summary"]));
        try
        {
            HouseWindowProposalValidator.Validate(request, proposal);
        }
        catch (ArgumentException)
        {
            throw new CodexProposalException("Codex returned a proposal outside the approved window-resize bounds.");
        }

        return proposal;
    }

    private static string ExtractFinalAgentMessage(JsonElement terminalNotification)
    {
        var turn = GetRequiredObject(terminalNotification, "turn");
        if (!string.Equals(GetRequiredString(turn, "status"), "completed", StringComparison.Ordinal))
        {
            throw new CodexProposalException("Codex did not complete the proposal turn.");
        }

        VerifySupportedTurnItems(turn);
        string? finalMessage = null;
        foreach (var item in turn.GetProperty("items").EnumerateArray())
        {
            if (GetRequiredString(item, "type") == "agentMessage") finalMessage = GetRequiredString(item, "text");
        }

        if (string.IsNullOrWhiteSpace(finalMessage) || finalMessage.Length > 8_192)
        {
            throw new CodexProposalException("Codex returned no bounded proposal message.");
        }

        return finalMessage;
    }

    private static JsonElement GetRequiredObject(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object) throw new ProtocolBoundaryException();
        return value;
    }

    private static string GetRequiredString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String) throw new ProtocolBoundaryException();
        return value.GetString() ?? throw new ProtocolBoundaryException();
    }

    private static string ReadString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text) throw new CodexProposalException("Codex returned an invalid structured proposal.");
        return text;
    }

    private static decimal ReadDecimal(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number)) throw new CodexProposalException("Codex returned an invalid structured proposal.");
        return number;
    }

    private static string GetMethod(JsonElement message)
    {
        if (!message.TryGetProperty("method", out var method) || method.ValueKind != JsonValueKind.String) throw new ProtocolBoundaryException();
        return method.GetString()!;
    }

    private async Task TryInterruptAsync(AppServerClient client, string threadId, string turnId)
    {
        try { await client.InterruptAndDrainAsync(threadId, turnId, InterruptTimeout).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException) { }
    }

    private static string ValidateWorkingDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A snapshot directory is required.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath)) throw new ArgumentException("The snapshot directory does not exist.", nameof(path));

        var resolvedPath = ResolveLinks(fullPath);
        var resolvedTemp = ResolveLinks(Path.GetTempPath());
        var tempPrefix = Path.EndsInDirectorySeparator(resolvedTemp) ? resolvedTemp : resolvedTemp + Path.DirectorySeparatorChar;
        if (string.Equals(resolvedPath, resolvedTemp, PathComparison) || !resolvedPath.StartsWith(tempPrefix, PathComparison))
        {
            throw new ArgumentException("The snapshot must be a child of the OS temp directory.", nameof(path));
        }

        for (var directory = new DirectoryInfo(resolvedPath); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) ||
                File.Exists(Path.Combine(directory.FullName, ".git")) ||
                Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                throw new ArgumentException("The snapshot directory cannot inherit repository or AGENTS.md context.", nameof(path));
            }
        }

        return resolvedPath;
    }

    private static string ResolveLinks(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? throw new ArgumentException("Path must be absolute.", nameof(path));
        var parts = full[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            var info = new DirectoryInfo(current);
            if (info.LinkTarget is not null && info.ResolveLinkTarget(true) is { } target)
            {
                current = Path.GetFullPath(target.FullName);
            }
        }

        return Path.GetFullPath(current);
    }

    private async Task<ShortCommandResult?> RunShortCommandAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo.FileName = _executablePath!;
        process.StartInfo.WorkingDirectory = Path.GetTempPath();
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        ApplyCodexHome(process.StartInfo);
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);

        try
        {
            if (!process.Start()) return null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DoctorTimeout);
        var stdout = ReadBoundedTextAsync(process.StandardOutput, MaximumDoctorOutputCharacters, timeout.Token);
        var stderr = DrainAsync(process.StandardError);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var output = await stdout.ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            return new ShortCommandResult(process.ExitCode, output);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            await ObserveAfterKillAsync(stdout, stderr).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            KillProcessTree(process);
            await ObserveAfterKillAsync(stdout, stderr).ConfigureAwait(false);
            return null;
        }
    }

    private void ApplyCodexHome(ProcessStartInfo startInfo)
    {
        if (_codexHome is not null) startInfo.Environment["CODEX_HOME"] = _codexHome;
    }

    private string GetEffectiveCodexHome()
    {
        if (_codexHome is not null) return _codexHome;
        var environmentHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(environmentHome)) return Path.GetFullPath(environmentHome);
        return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"));
    }

    private static async Task ObserveAfterKillAsync(Task stdout, Task stderr)
    {
        try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException) { }
    }

    private static async Task<string> ReadBoundedTextAsync(StreamReader reader, int maximumCharacters, CancellationToken cancellationToken)
    {
        var buffer = new char[2_048];
        var output = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) return output.ToString();
            if (output.Length + read > maximumCharacters) throw new InvalidDataException("Command output exceeded its bound.");
            output.Append(buffer, 0, read);
        }
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        var buffer = new char[2_048];
        while (await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) != 0) { }
    }

    private static string? ParseVersion(string output)
    {
        var line = output.Trim();
        const string prefix = "codex-cli ";
        if (!line.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var version = line[prefix.Length..];
        return version.Length is > 0 and <= 80 && version.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '+')
            ? version
            : null;
    }

    private static CodexLoginStatus ParseLoginStatus(string output)
    {
        var normalized = output.Trim().ToLowerInvariant();
        if (normalized.Contains("not logged in", StringComparison.Ordinal) || normalized.Contains("not signed in", StringComparison.Ordinal)) return CodexLoginStatus.NotLoggedIn;
        if (normalized.StartsWith("logged in", StringComparison.Ordinal) || normalized.StartsWith("signed in", StringComparison.Ordinal)) return CodexLoginStatus.LoggedIn;
        return CodexLoginStatus.Unknown;
    }

    private static void Report(IProgress<CodexProposalProgress>? progress, CodexProposalStage stage, string message)
    {
        try { progress?.Report(new CodexProposalProgress(stage, message)); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException) { }
    }

    private static string? FindExecutable()
    {
        var candidates = new List<string>();
        if (OperatingSystem.IsMacOS())
        {
            candidates.Add("/Applications/ChatGPT.app/Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex");
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local)) candidates.Add(Path.Combine(local, "Programs", "Codex", "codex.exe"));
        candidates.Add("codex");

        foreach (var candidate in candidates)
        {
            var resolved = ResolveExecutable(candidate);
            if (resolved is not null && File.Exists(resolved)) return resolved;
        }

        return null;
    }

    private static string? ResolveExecutable(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        if (Path.IsPathRooted(candidate)) return Path.GetFullPath(candidate);

        if (candidate.Contains(Path.DirectorySeparatorChar) || candidate.Contains(Path.AltDirectorySeparatorChar))
        {
            var relative = Path.GetFullPath(candidate);
            if (File.Exists(relative)) return relative;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathValue)) return null;
        var fileNames = OperatingSystem.IsWindows() && Path.GetExtension(candidate).Length == 0
            ? new[] { candidate + ".exe", candidate + ".cmd", candidate + ".bat", candidate }
            : new[] { candidate };
        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var fileName in fileNames)
            {
                var full = Path.Combine(directory, fileName);
                if (File.Exists(full)) return Path.GetFullPath(full);
            }
        }

        return null;
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException) { }
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed record ShortCommandResult(int ExitCode, string Output);

    private sealed class ProtocolBoundaryException : Exception;

    private sealed class AppServerClient(Process process, int maximumLineBytes)
    {
        private readonly BoundedLineReader _reader = new(process.StandardOutput, maximumLineBytes);
        private int _nextRequestId = 1_000;

        public async Task<JsonElement> RequestAsync(
            string method,
            IReadOnlyDictionary<string, object?> parameters,
            int requestId,
            Func<JsonElement, Task>? notificationHandler,
            CancellationToken cancellationToken)
        {
            await WriteMessageAsync(new Dictionary<string, object?>
            {
                ["method"] = method,
                ["id"] = requestId,
                ["params"] = parameters
            }, cancellationToken).ConfigureAwait(false);

            while (true)
            {
                var message = await ReadMessageAsync(cancellationToken).ConfigureAwait(false);
                if (message.TryGetProperty("method", out _))
                {
                    if (message.TryGetProperty("id", out _)) throw new ProtocolBoundaryException();
                    if (notificationHandler is not null) await notificationHandler(message).ConfigureAwait(false);
                    else if (GetMethod(message) != "thread/started") throw new ProtocolBoundaryException();
                    continue;
                }

                if (!message.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var responseId) || responseId != requestId)
                {
                    throw new ProtocolBoundaryException();
                }

                if (message.TryGetProperty("error", out _)) throw new CodexProposalException("Codex app-server rejected a restricted proposal request.");
                if (!message.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object) throw new ProtocolBoundaryException();
                return result.Clone();
            }
        }

        public Task NotifyAsync(string method, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken) =>
            WriteMessageAsync(new Dictionary<string, object?> { ["method"] = method, ["params"] = parameters }, cancellationToken);

        public async Task<JsonElement> ReadMessageAsync(CancellationToken cancellationToken)
        {
            var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null) throw new CodexProposalException("Codex app-server stopped before completing the proposal.");
            try
            {
                using var document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 64, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
                if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ProtocolBoundaryException();
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                throw new ProtocolBoundaryException();
            }
        }

        public async Task InterruptAndDrainAsync(string threadId, string turnId, TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            var requestId = Interlocked.Increment(ref _nextRequestId);
            await WriteMessageAsync(new Dictionary<string, object?>
            {
                ["method"] = "turn/interrupt",
                ["id"] = requestId,
                ["params"] = new Dictionary<string, object?> { ["threadId"] = threadId, ["turnId"] = turnId }
            }, cancellation.Token).ConfigureAwait(false);

            while (!cancellation.IsCancellationRequested)
            {
                JsonElement message;
                try { message = await ReadMessageAsync(cancellation.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
                catch (CodexProposalException) { return; }
                if (message.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String && method.GetString() == "turn/completed" &&
                    message.TryGetProperty("params", out var parameters) && parameters.ValueKind == JsonValueKind.Object &&
                    parameters.TryGetProperty("threadId", out var completedThread) && completedThread.GetString() == threadId &&
                    parameters.TryGetProperty("turn", out var turn) && turn.ValueKind == JsonValueKind.Object &&
                    turn.TryGetProperty("id", out var completedTurn) && completedTurn.GetString() == turnId)
                {
                    return;
                }

                if (message.TryGetProperty("id", out var responseId) && responseId.ValueKind == JsonValueKind.Number && responseId.TryGetInt32(out var id) && id == requestId)
                {
                    if (!message.TryGetProperty("result", out _)) return;
                }
            }
        }

        private async Task WriteMessageAsync(IReadOnlyDictionary<string, object?> message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = JsonSerializer.Serialize(message, WireJsonOptions);
            if (Encoding.UTF8.GetByteCount(json) > maximumLineBytes) throw new ProtocolBoundaryException();
            await process.StandardInput.WriteLineAsync(json).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class BoundedLineReader(StreamReader reader, int maximumBytes)
    {
        private readonly char[] _buffer = new char[4_096];
        private readonly StringBuilder _line = new();
        private int _offset;
        private int _available;
        private int _lineBytes;

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (_offset < _available)
                {
                    var newline = Array.IndexOf(_buffer, '\n', _offset, _available - _offset);
                    var count = newline >= 0 ? newline - _offset : _available - _offset;
                    AppendBounded(_buffer, _offset, count);
                    _offset += count;
                    if (newline >= 0)
                    {
                        _offset++;
                        var line = _line.ToString();
                        _line.Clear();
                        _lineBytes = 0;
                        return line.EndsWith('\r') ? line[..^1] : line;
                    }
                }

                _available = await reader.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                _offset = 0;
                if (_available == 0)
                {
                    if (_line.Length == 0) return null;
                    var lastLine = _line.ToString();
                    _line.Clear();
                    _lineBytes = 0;
                    return lastLine;
                }
            }
        }

        private void AppendBounded(char[] buffer, int offset, int count)
        {
            _lineBytes += Encoding.UTF8.GetByteCount(buffer, offset, count);
            if (_lineBytes > maximumBytes)
            {
                throw new ProtocolBoundaryException();
            }

            _line.Append(buffer, offset, count);
        }
    }

    public sealed class CodexProposalException(string message) : Exception(message);
}
