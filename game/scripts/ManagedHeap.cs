using System;

namespace Urman.Godot;

/// <summary>
/// Crash guard (2026-11-12 report: abort on the .NET finalizer thread while the
/// main thread was freeing native memory right after the prologue handed over
/// to the village). A scene hand-over drops thousands of Godot wrappers at once;
/// left alone, the finalizer thread releases them while the main thread is
/// tearing the old scene down. Draining them here, with the main thread parked,
/// removes that overlap. Call only at hand-over points that are already covered
/// by a blackout or a loading frame.
/// </summary>
public static class ManagedHeap
{
    public static void Quiesce()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
