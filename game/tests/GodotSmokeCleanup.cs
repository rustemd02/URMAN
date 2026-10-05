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
            // Stopped players hand their AudioStreamPlayback back only on the
            // audio server's next mix, which headless frames can outrun; give
            // it real time so a playing ambience or click is not reported as a
            // leaked resource at exit.
            await tree.ToSignal(tree.CreateTimer(.15, processAlways: true, ignoreTimeScale: true), SceneTreeTimer.SignalName.Timeout);
        }

        AnimationCatalog.ClearCacheForTests();
        UiFoley.ClearCacheForHeadlessTests();
        GeneratedCharacterKitDressing.ClearCacheForHeadlessTests();
        VehicleVisualFactory.ClearCacheForHeadlessTests();
        PolicePostAssets.ClearCacheForTests();
        WinterParticleSurfaces.ClearCacheForHeadlessTests();
        VillageWindowMaterials.ClearCacheForHeadlessTests();
        PainterlyMaterialLibrary.ClearCacheForHeadlessTests();
    }
}
