using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Godot;
using FileAccess = System.IO.FileAccess;
using Urman.Godot;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Handoff;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

public partial class StudioRoot
{
    private const string AiPilotCommandLabel = "изменить окна дома через Codex";
    private const int AiActionLimit = 32;
    private const int AiActionJsonLimit = 8 * 1024;
    private const int AiActionPngLimit = 16 * 1024 * 1024;
    private const int AiRequestLimit = HouseWindowProposalValidator.MaximumSerializedRequestBytes;
    private const int AiProposalLimit = 32 * 1024;
    private const int AiHouseRecipeLimit = 32 * 1024;
    private const long AiPilotTargetHashLimit = 128L * 1024 * 1024;
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private bool _aiEvidenceCaptureBusy;

    internal sealed record AiActionIdentity(string RequestId, string RequestHash, string ProposalHash, string SnapshotHash);
    internal enum AiEvidenceAction { Preview, Apply, Undo, Redo }

    internal bool IsAiActionBusy => IsWorkspaceActionBusy || _aiEvidenceCaptureBusy
        || _sections.TryGetValue("world", out var section) && section is StudioWorldSection world
            && world.HasTransientFencePreview
        || _aiPanel is not null && GodotObject.IsInstanceValid(_aiPanel)
            && (_aiPanel.IsActionBusy || _aiPanel.HasTransientFencePreview);

    internal AiActionIdentity FreezeAiActionIdentity(
        HouseWindowRequest request,
        HouseWindowProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(proposal);
        return ReadAiActionIdentity(request.RequestId, request, proposal);
    }

    private AiActionIdentity ReadAiActionIdentity(
        string requestId,
        HouseWindowRequest? expectedRequest = null,
        HouseWindowProposal? expectedProposal = null)
    {
        if (!Regex.IsMatch(requestId, @"\A[a-f0-9]{32}\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException("The pilot request ID is not canonical.");

        var requestRelative = $".urman-studio/ai-jobs/{requestId}/request.json";
        var proposalRelative = $".urman-studio/ai-jobs/{requestId}/proposal.json";
        var requestBytes = ReadBoundedAiArtifact(requestRelative, AiRequestLimit);
        var proposalBytes = ReadBoundedAiArtifact(proposalRelative, AiProposalLimit);
        var request = AuthoringRequestV2.Parse(StrictUtf8.GetString(requestBytes));
        HouseWindowProposalValidator.ValidateRequest(request);
        var proposal = CodexProposalProvider.ParseProposal(StrictUtf8.GetString(proposalBytes), request);
        HouseWindowProposalValidator.Validate(request, proposal);

        if (!string.Equals(request.RequestId, requestId, StringComparison.Ordinal)
            || !requestBytes.AsSpan().SequenceEqual(StrictUtf8.GetBytes(CodexProposalProvider.SerializeRequest(request)))
            || !proposalBytes.AsSpan().SequenceEqual(StrictUtf8.GetBytes(CodexProposalProvider.SerializeProposal(request, proposal))))
            throw new InvalidDataException("The stored pilot request or proposal is not the canonical frozen artifact.");

        if (expectedRequest is not null && !requestBytes.AsSpan().SequenceEqual(
                StrictUtf8.GetBytes(CodexProposalProvider.SerializeRequest(expectedRequest))))
            throw new InvalidDataException("The stored request differs from the active Studio request.");
        if (expectedProposal is not null && !proposalBytes.AsSpan().SequenceEqual(
                StrictUtf8.GetBytes(CodexProposalProvider.SerializeProposal(expectedRequest ?? request, expectedProposal))))
            throw new InvalidDataException("The stored proposal differs from the active Studio proposal.");

        return new(requestId, AtomicFile.Sha256(requestBytes), AtomicFile.Sha256(proposalBytes), request.SnapshotHash);
    }

    private byte[] ReadBoundedAiArtifact(string relativePath, int maximumBytes) =>
        ReadBoundedAuthoringFile(Workspace.Root, relativePath, maximumBytes, allowStudioMetadata: true);

    private static byte[] ReadBoundedAuthoringFile(string root, string relativePath, int maximumBytes,
        bool allowStudioMetadata = false)
    {
        StudioFileTransaction.EnsureNoLinks(root, relativePath, allowStudioMetadata);
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length < 0 || stream.Length > maximumBytes)
            throw new InvalidDataException("A stored authoring file exceeds its size limit.");

        var bytes = new byte[(int)stream.Length];
        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) throw new EndOfStreamException("A stored pilot artifact changed while being read.");
            offset += read;
        }
        if (stream.ReadByte() != -1)
            throw new InvalidDataException("A stored authoring file grew while being read.");
        return bytes;
    }

    internal bool TryGetPilotActionIdentity(bool redo, out AiActionIdentity? identity, out string? transactionId, out string? requestId)
    {
        identity = null;
        transactionId = null;
        requestId = null;
        var step = Session.History(redo).FirstOrDefault();
        if (step is null || !TryGetPilotRequestId(step.Paths, out var selectedRequestId)
            || !IsPilotStep(step, selectedRequestId, step.TransactionId))
            return false;

        requestId = selectedRequestId;
        transactionId = step.TransactionId;
        identity = ReadAiActionIdentity(selectedRequestId);
        return true;
    }

    internal bool IsPilotStep(EditHistoryStep? step, string requestId, string? transactionId)
    {
        if (step is null || !string.Equals(step.Label, AiPilotCommandLabel, StringComparison.Ordinal)
            || !string.Equals(step.TransactionId, transactionId, StringComparison.Ordinal)
            || transactionId is null || !Regex.IsMatch(transactionId, @"\A[a-f0-9]{32}\z", RegexOptions.CultureInvariant))
            return false;

        var expected = PilotPaths(requestId);
        return step.Paths.Count == expected.Length
            && expected.All(path => step.Paths.Contains(path, StringComparer.Ordinal));
    }

    private static bool TryGetPilotRequestId(IReadOnlyList<string> paths, out string requestId)
    {
        requestId = string.Empty;
        var model = paths.FirstOrDefault(path => path.EndsWith(".glb", StringComparison.Ordinal));
        if (model is null) return false;
        var match = Regex.Match(model,
            @"\Agame/assets/models/studio/hero-house-(?<id>[a-f0-9]{32})\.glb\z", RegexOptions.CultureInvariant);
        if (!match.Success) return false;
        requestId = match.Groups["id"].Value;
        return true;
    }

    private static string[] PilotPaths(string requestId) =>
    [
        "game/content/studio/hero_house.recipe.json",
        $"game/assets/models/studio/hero-house-{requestId}.glb",
        $"assets/source/blender/studio/hero-house-{requestId}.blend",
        $"game/assets/models/studio/hero-house-{requestId}.glb.manifest.json"
    ];

    // shortcut: receipts bind individual Studio actions; game and byte-exact proof require the matched native walkthrough.
    internal async Task RecordAiActionEvidenceAsync(
        AiActionIdentity identity,
        AiEvidenceAction action,
        string? transactionId,
        Func<bool> isStillCurrent)
    {
        if (_aiEvidenceCaptureBusy || Session.IsBusy || _checkpointBusy)
        {
            ShowAiEvidenceBanner("Studio занята другим действием; кадр этого действия не записан.", error: true);
            return;
        }

        _aiEvidenceCaptureBusy = true;
        try
        {
            if (_aiPanel is not null && GodotObject.IsInstanceValid(_aiPanel)) _aiPanel.SetEvidenceBusy(true);
            RefreshStatus();
        }
        catch (Exception) { }
        var workspaceRoot = string.Empty;
        var folderRelative = $".urman-studio/ai-jobs/{identity.RequestId}/actions";
        var actionId = Guid.NewGuid().ToString("N");
        var pngRelative = $"{folderRelative}/{actionId}.png";
        var jsonRelative = $"{folderRelative}/{actionId}.json";
        var imageWritten = false;
        IReadOnlyList<string?>? frozenTargetHashes = null;
        try
        {
            workspaceRoot = Workspace.Root;
            if (action == AiEvidenceAction.Preview ? transactionId is not null :
                transactionId is null || !Regex.IsMatch(transactionId, @"\A[a-f0-9]{32}\z", RegexOptions.CultureInvariant))
                throw new InvalidDataException("The action receipt has an invalid transaction binding.");

            StudioFileTransaction.EnsureNoLinks(workspaceRoot, folderRelative, allowStudioMetadata: true);
            var folder = Path.Combine(workspaceRoot, folderRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(folder);
            StudioFileTransaction.EnsureNoLinks(workspaceRoot, folderRelative, allowStudioMetadata: true);
            if (Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly).Take(AiActionLimit).Count() >= AiActionLimit)
                throw new InvalidOperationException("This request already has the maximum 32 Studio action receipts.");

            byte[]? png = null;
            string? captureError = null;
            if (action != AiEvidenceAction.Preview)
            {
                try { frozenTargetHashes = CapturePilotTargetHashes(workspaceRoot, identity.RequestId); }
                catch (Exception) { captureError = "Pilot target files could not be fingerprinted before capture."; }
            }
            try
            {
                if (captureError is not null)
                {
                    // Preserve the action receipt with an explicit no-frame reason.
                }
                else if (!await WaitForAiEvidenceFrameAsync())
                    captureError = "Studio viewport did not draw within 3 seconds.";
                else if (!IsInstanceValid(this) || Session.IsBusy || _checkpointBusy || !isStillCurrent()
                    || frozenTargetHashes is not null && !PilotTargetHashesMatch(workspaceRoot, identity.RequestId, frozenTargetHashes))
                    captureError = "Studio action state or target files changed during frame capture.";
                else
                    png = CaptureAiViewportPng(out captureError);
            }
            catch (Exception)
            {
                captureError = "Studio viewport frame could not be captured.";
            }

            if (png is not null)
            {
                StudioFileTransaction.EnsureNoLinks(workspaceRoot, pngRelative, allowStudioMetadata: true);
                AtomicFile.WriteAllBytes(Path.Combine(workspaceRoot, pngRelative.Replace('/', Path.DirectorySeparatorChar)), png, overwrite: false);
                imageWritten = true;
            }

            var json = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                actionId,
                action = action.ToString().ToLowerInvariant(),
                requestId = identity.RequestId,
                requestHash = identity.RequestHash,
                proposalHash = identity.ProposalHash,
                snapshotHash = identity.SnapshotHash,
                transactionId,
                frameHash = png is null ? null : AtomicFile.Sha256(png),
                captureError
            });
            if (json.Length > AiActionJsonLimit) throw new InvalidDataException("The Studio action receipt exceeds 8 KiB.");
            StudioFileTransaction.EnsureNoLinks(workspaceRoot, jsonRelative, allowStudioMetadata: true);
            AtomicFile.WriteAllBytes(Path.Combine(workspaceRoot, jsonRelative.Replace('/', Path.DirectorySeparatorChar)), json, overwrite: false);
            imageWritten = false;
            if (captureError is not null)
                ShowAiEvidenceBanner("Действие завершено. Studio receipt сохранён без кадра: " + captureError, error: true);
        }
        catch (Exception error)
        {
            if (imageWritten)
            {
                try
                {
                    StudioFileTransaction.EnsureNoLinks(workspaceRoot, pngRelative, allowStudioMetadata: true);
                    File.Delete(Path.Combine(workspaceRoot, pngRelative.Replace('/', Path.DirectorySeparatorChar)));
                }
                catch (Exception) { }
            }
            ShowAiEvidenceBanner($"Действие завершено, но Studio receipt не записан ({error.GetType().Name}).", error: true);
        }
        finally
        {
            _aiEvidenceCaptureBusy = false;
            try
            {
                if (IsInstanceValid(this))
                {
                    if (_aiPanel is not null && GodotObject.IsInstanceValid(_aiPanel)) _aiPanel.SetEvidenceBusy(false);
                    RefreshStatus();
                }
            }
            catch (Exception) { }
        }
    }

    internal void ShowAiEvidenceBanner(string text, bool error)
    {
        try
        {
            if (IsInstanceValid(this)) ShowBanner(text, error);
        }
        catch (Exception) { }
    }

    private async Task<bool> WaitForAiEvidenceFrameAsync()
    {
        var server = RenderingServer.Singleton;
        var drawn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = Callable.From(() => drawn.TrySetResult(true));
        server.Connect(RenderingServer.SignalName.FramePostDraw, callback);
        try
        {
            if (await Task.WhenAny(drawn.Task, Task.Delay(TimeSpan.FromSeconds(3))) != drawn.Task)
                return false;
            await drawn.Task;
            return true;
        }
        finally
        {
            if (server.IsConnected(RenderingServer.SignalName.FramePostDraw, callback))
                server.Disconnect(RenderingServer.SignalName.FramePostDraw, callback);
        }
    }

    private static IReadOnlyDictionary<string, string> CapturePlayHouseSources(string root)
    {
        // shortcut: hashes cover house geometry/placement inputs; include other rendered resources when accepting broader scene provenance.
        var paths = new[]
        {
            "game/content/studio/hero_house.recipe.json",
            "game/content/world/act1_village_kit.world.v1.json",
            "game/assets/models/act1/urman_village_exterior_kit.glb",
            "game/assets/generated/urman_modular_kit.glb"
        };
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var relativePath in paths)
            sources.Add(relativePath, CaptureAuthoringFileHash(root, relativePath)
                ?? throw new FileNotFoundException("A required Play source is missing.", relativePath));

        var recipePath = paths[0];
        var recipeBytes = ReadBoundedAuthoringFile(root, recipePath, AiHouseRecipeLimit);
        if (!string.Equals(sources[recipePath], Convert.ToHexStringLower(SHA256.HashData(recipeBytes)), StringComparison.Ordinal))
            throw new InvalidDataException("The Play recipe changed while its sources were being captured.");
        var recipe = HeroHouseRecipe.Parse(StrictUtf8.GetString(recipeBytes));
        if (recipe.ModelPath is { } modelPath)
        {
            var relativePath = "game/" + modelPath["res://".Length..];
            sources.Add(relativePath, CaptureAuthoringFileHash(root, relativePath)
                ?? throw new FileNotFoundException("The Play recipe model is missing.", relativePath));
        }
        return sources;
    }

    private static bool PlayHouseSourcesMatch(string root, IReadOnlyDictionary<string, string> expected)
    {
        var current = CapturePlayHouseSources(root);
        return current.Count == expected.Count
            && current.OrderBy(source => source.Key, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(source => source.Key, StringComparer.Ordinal));
    }

    private static IReadOnlyList<string?> CapturePilotTargetHashes(string root, string requestId) =>
        PilotPaths(requestId).Select(relativePath => CaptureAuthoringFileHash(root, relativePath)).ToArray();

    private static string? CaptureAuthoringFileHash(string root, string relativePath)
    {
        StudioFileTransaction.EnsureNoLinks(root, relativePath);
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var expectedLength = stream.Length;
        if (expectedLength < 0 || expectedLength > AiPilotTargetHashLimit)
            throw new InvalidDataException("An authored file exceeds the bounded fingerprint size.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > AiPilotTargetHashLimit)
                throw new InvalidDataException("An authored file grew beyond the bounded fingerprint size.");
            hash.AppendData(buffer, 0, read);
        }
        if (total != expectedLength || stream.Length != expectedLength || stream.ReadByte() != -1)
            throw new InvalidDataException("An authored file changed while its fingerprint was read.");
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static bool PilotTargetHashesMatch(string root, string requestId, IReadOnlyList<string?> expected) =>
        CapturePilotTargetHashes(root, requestId).SequenceEqual(expected, StringComparer.Ordinal);

    private byte[]? CaptureAiViewportPng(out string? error)
    {
        error = null;
        using var image = GetViewport().GetTexture().GetImage();
        if (image.IsEmpty() || image.GetWidth() <= 0 || image.GetHeight() <= 0)
        {
            error = "Studio viewport returned no pixels.";
            return null;
        }
        var png = image.SavePngToBuffer();
        if (png.Length < PngSignature.Length || !png.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
        {
            error = "Studio viewport did not produce a valid PNG.";
            return null;
        }
        if (png.Length > AiActionPngLimit)
        {
            error = "Studio viewport PNG exceeds 16 MiB.";
            return null;
        }
        return png;
    }
}
