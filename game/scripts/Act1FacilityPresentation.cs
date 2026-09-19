using System;
using Godot;

namespace Urman.Godot;

/// <summary>Ticks local room presentation; RuntimeBridge owns every saved state.</summary>
public partial class Act1FacilityPresentation : Node
{
    internal Action<double>? Tick { get; set; }
    internal Action? Detached { get; set; }
    public override void _PhysicsProcess(double delta) => Tick?.Invoke(delta);
    public override void _ExitTree()
    {
        Detached?.Invoke();
        Tick = null;
        Detached = null;
    }
}
