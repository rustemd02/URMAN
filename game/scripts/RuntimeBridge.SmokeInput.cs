using Godot;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    // The guarded harness explicitly replays input while its native window can
    // be behind the host. Normal sessions still stop on actual focus loss.
    private static readonly bool ProtectedBackgroundInputReplay =
        OS.GetEnvironment("URMAN_PROTECTED_RUN") == "1"
        && OS.GetCmdlineArgs().Contains("--urman-smoke-background-input", StringComparer.Ordinal)
        && OS.GetCmdlineArgs().Any(arg => arg.StartsWith("res://tests/", StringComparison.Ordinal)
            && arg.EndsWith(".tscn", StringComparison.Ordinal));
}
