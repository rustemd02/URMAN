using Godot;
using Urman.Godot;

namespace Urman.Godot.Tests;

internal static class GodotSmokeCleanup
{
    public static async Task ReleaseAsync(Node scene)
    {
        if (GodotObject.IsInstanceValid(scene))
        {
            var tree = scene.GetTree();
            scene.QueueFree();
            // Await on the still-live SceneTree rather than on the node that is
            // being freed. Waiting through the queued node can never resume on
            // headless Godot once its native instance has been released. The
            // physics barrier lets Shape3D RIDs leave the physics server before
            // the render-frame cleanup and shutdown leak checks run.
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        PainterlyMaterialLibrary.ClearCacheForHeadlessTests();
    }
}
