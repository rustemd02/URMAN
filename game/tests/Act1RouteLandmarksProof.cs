using Godot;

namespace Urman.Godot.Tests;

/// <summary>Observe the authored roadside landmarks through the ordinary live-view target.</summary>
public static class Act1RouteLandmarksProof
{
    private const string Prefix = "urman.chapter1:";
    private const string Action = Prefix + "interaction/observe-sketch-landmarks";
    private const string Source = Prefix + "knowledge/clue_sketch_field_landmarks";

    public static async Task ObserveAsync(Node host, RuntimeBridge bridge)
    {
        var tree = host.GetTree();
        var main = (Main)tree.GetFirstNodeInGroup("zone_manager");
        var player = (FirstPersonController)tree.GetFirstNodeInGroup("player_controller");
        var world = main.ConnectedWorld ?? throw new InvalidOperationException("route-landmarks-proof: connected world is missing");
        var target = world.FindChild("Observation_observe-sketch-landmarks", true, false) as InteractionTarget
            ?? throw new InvalidOperationException("route-landmarks-proof: physical observation target is missing");
        for (var frame = 0; frame < 240 && player.ModalOpen; frame++) await Frames(1);
        if (player.ModalOpen || bridge.CurrentZoneId != "zirat_road" || Known())
            throw new InvalidOperationException("route-landmarks-proof: requires the real unobserved roadside scene without a modal");
        if (!target.HasMeta("observationReferenceEye") || !target.HasMeta("observationLookAt")
            || !target.GetMeta("observationRequiresLiveView").AsBool())
            throw new InvalidOperationException("route-landmarks-proof: the target lacks its authored view contract");

        var original = player.CapturePortableTransform();
        var scene = bridge.ActiveSceneId;
        try
        {
            var reference = target.GetMeta("observationReferenceEye").AsVector3();
            using var groundRay = PhysicsRayQueryParameters3D.Create(reference + Vector3.Up * 3f,
                reference + Vector3.Down * 8f, 1u, new global::Godot.Collections.Array<Rid> { player.GetRid() });
            var ground = player.GetWorld3D().DirectSpaceState.IntersectRay(groundRay);
            if (ground.Count == 0 || ground["normal"].AsVector3().Dot(Vector3.Up) <= .7f)
                throw new InvalidOperationException("route-landmarks-proof: reference has no walkable support: " + reference);
            var feet = ground["position"].AsVector3() + Vector3.Up * .05f;
            if (!player.CanStandAt(feet))
                throw new InvalidOperationException("route-landmarks-proof: reference does not fit a standing capsule: " + feet);
            player.ApplyZoneSpawn(feet, 0f);
            for (var frame = 0; frame < 12; frame++) await host.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            if (!player.IsOnFloor() || player.IsCrouching || player.ModalOpen || !player.CanStandAt(player.GlobalPosition))
                throw new InvalidOperationException("route-landmarks-proof: observation is not a supported standing view");

            var camera = player.GetNode<Camera3D>("Head/Camera3D");
            var point = target.GetMeta("observationLookAt").AsVector3();
            var direction = point - camera.GlobalPosition;
            var pitch = Mathf.RadToDeg(Mathf.Atan2(direction.Y, new Vector2(direction.X, direction.Z).Length()));
            var yaw = Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Z));
            var before = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
            player.ApplySmokeLook(pitch, yaw + 180f);
            if (target.IsAvailable() || await bridge.DispatchInteractionAsync(Action)
                || bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() != before)
                throw new InvalidOperationException("route-landmarks-proof: looking away supplied a field observation");
            player.ApplySmokeLook(pitch, yaw);
            await host.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            var ray = player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
            ray.ForceRaycastUpdate();
            var hit = ray.IsColliding() ? ray.GetCollider() as Node : null;
            var liveView = world.CanUseObservationInteraction(Action);
            var available = target.IsAvailable();
            GD.Print("route-landmarks-view: feet=" + player.GlobalPosition + " eye=" + camera.GlobalPosition
                + " lookAt=" + point + " hit=" + hit?.GetPath() + " semantic=" + bridge.IsInteractionAvailable(Action)
                + " liveView=" + liveView + " targetAvailable=" + available);
            await Capture(available && hit == target);
            if (!available || !liveView || hit != target)
                throw new InvalidOperationException("route-landmarks-proof: actual camera ray cannot use the field target");

            target.Interact();
            for (var frame = 0; frame < 180 && !Known(); frame++) await Frames(1);
            if (!Known() || bridge.ActiveSceneId != scene
                || bridge.JournalEntries().Count(entry => entry.EntryId == Source) != 1)
                throw new InvalidOperationException("route-landmarks-proof: actual observation did not retain exactly one local source");
            var journal = (JournalUi)tree.GetFirstNodeInGroup("journal_ui");
            // Knowledge commits before InteractionTarget finishes its awaited
            // dispatch and opens the reader. Close the actual resulting view.
            for (var frame = 0; frame < 180 && (!journal.GetNode<Control>("Screen").Visible
                    || journal.ActiveEntryId != Source); frame++) await Frames(1);
            if (!journal.GetNode<Control>("Screen").Visible || journal.ActiveEntryId != Source)
                throw new InvalidOperationException("route-landmarks-proof: actual source reader did not open");
            journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            for (var frame = 0; frame < 180 && player.ModalOpen; frame++) await Frames(1);
            if (player.ModalOpen)
                throw new InvalidOperationException("route-landmarks-proof: the observed source retained input");
            GD.Print("route-landmarks-proof: ordinary standing camera; look-away refused; ray -> actual target -> one field source");
        }
        finally { player.ApplyPortableTransform(original); }

        bool Known() => bridge.SelectRuntimeState().GetProperty("knowledge")
            .GetProperty(Source).GetProperty("status").GetString() == "confirmed";
        async Task Frames(int count)
        {
            for (var frame = 0; frame < count; frame++) await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        async Task Capture(bool ready)
        {
            var output = System.Environment.GetEnvironmentVariable("URMAN_IMAGE_UI_OUTPUT");
            if (string.IsNullOrEmpty(output) || DisplayServer.GetName() == "headless")
            {
                GD.Print("route-landmarks-frame: capture=not-run; no image output or rendering device");
                return;
            }
            await Act1StateFlowProof.WaitForRenderedFrameAsync(host, "route-landmarks/" + host.Name);
            using var picture = host.GetViewport().GetTexture().GetImage();
            System.IO.Directory.CreateDirectory(output);
            var path = System.IO.Path.Combine(output, "route_landmarks_" + host.Name + (ready ? "_ready.png" : "_blocked.png"));
            if (System.IO.File.Exists(path) || picture.IsEmpty() || picture.SavePng(path) != Error.Ok)
                throw new InvalidOperationException("route-landmarks-proof: failed to retain a fresh source-view frame: " + path);
            GD.Print("route-landmarks-frame: " + path);
        }
    }
}
