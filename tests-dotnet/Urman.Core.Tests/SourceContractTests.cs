using System.Text.RegularExpressions;
using Xunit;

namespace Urman.Core.Tests;

/// <summary>
/// TEST-007 static source-contract guard for the STATE-001 audit: only
/// RuntimeBridge may construct or restore the runtime kernel and only
/// RuntimeBridge may own the save store. Engine-independent by design (plain
/// source scan), so it runs in the dotnet suite without a Godot log gate.
/// </summary>
public sealed class SourceContractTests
{
    private static readonly (string RelativePath, string Source)[] GameScriptFiles = EnumerateGameScriptFiles().ToArray();

    [Fact]
    public void OnlyRuntimeBridgeConstructsOrRestoresTheKernel()
    {
        AssertExtensionMethodsNotCountedAsViolations();
        foreach (var (relativePath, source) in GameScriptFiles)
        {
            if (relativePath.EndsWith("RuntimeBridge.cs", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(
                !_kernelConstructionPattern.IsMatch(source),
                $"{relativePath}: kernel construction/restore outside RuntimeBridge");
        }
    }

    [Fact]
    public void OnlyRuntimeBridgeOwnsTheSaveStore()
    {
        foreach (var (relativePath, source) in GameScriptFiles)
        {
            if (relativePath.EndsWith("RuntimeBridge.cs", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(
                !_saveStorePattern.IsMatch(source),
                $"{relativePath}: save-store ownership outside RuntimeBridge");
        }
    }

    private static readonly Regex _kernelConstructionPattern = new(
        @"new\s+RuntimeKernel\s*\(|RuntimeKernel\s*\.\s*Restore\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex _saveStorePattern = new(
        @"new\s+AtomicSaveGameStore\s*\(|user://savegames",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static void AssertExtensionMethodsNotCountedAsViolations()
    {
        // Self-check: the guard patterns must not flag ordinary mentions.
        Assert.DoesNotMatch(_kernelConstructionPattern, "// RuntimeBridge/SaveGameV3 owns state");
        Assert.DoesNotMatch(_saveStorePattern, "/// SaveGameV3 persists settings");
    }

    private static IEnumerable<(string RelativePath, string Source)> EnumerateGameScriptFiles()
    {
        var directory = FindRepoRoot(AppContext.BaseDirectory);
        var scriptsRoot = Path.Combine(directory, "game", "scripts");
        foreach (var file in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (Path.GetRelativePath(directory, file), File.ReadAllText(file));
        }
    }

    private static string FindRepoRoot(string start)
    {
        var current = new DirectoryInfo(start);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "game", "scripts")))
            {
                return current.FullName;
            }

            current = current.Parent!;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
