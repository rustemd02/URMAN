using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal const string PlayerFootwearKey = "mosque/player-footwear";
    internal const string PlayerFootwearInteraction = "urman.chapter1:local/mosque/player-footwear";
    private InteractionTarget? _playerFootwearTarget;
    private Node3D? _playerFootwearOnShelf;
    private readonly List<MeshInstance3D> _playerStoredBoots = new();
    internal IReadOnlyList<MeshInstance3D> StoredPlayerBoots => _playerStoredBoots;
    internal bool PlayerFootwearRemoved => YardMechanism.Flag(_facilityProps, PlayerFootwearKey, "removed");

    private void BuildPlayerFootwearPlace()
    {
        // The front of the existing seat is below the separate observation ray
        // volume. The lower shoe shelf is presently empty; reserve only one pair.
        _playerFootwearOnShelf = new Node3D { Name = "AidarShoesOnMosqueShelf", Visible = false };
        _mosqueRoom!.AddChild(_playerFootwearOnShelf);
        _playerFootwearOnShelf.SetMeta("worldPropId", PlayerFootwearKey);
        _playerFootwearTarget = FacilityTarget("MosquePlayerFootwear", PlayerFootwearInteraction,
            "Снять обувь и поставить на полку", _mosqueRoom, new(3.65f, .445f, 2.638f), new(1.70f, .05f, .035f));
        _playerFootwearTarget.PresentationRepeatAvailable = PlayerAtFootwearVestibule;
        _playerFootwearTarget.PresentationRepeat = () => _ = TogglePlayerFootwear();
        _playerFootwearTarget.ConfigureRayOnly();
    }

    private bool PlayerAtFootwearVestibule() => FacilityExteriorActive && _mosqueRoom is not null
        && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player
        && FacilityInteriorAt(player.GlobalPosition) == "mosque" && _mosqueRoom.ToLocal(player.GlobalPosition).X > 2.20f;

    private bool CanChangePlayerFootwear(out string reason)
    {
        reason = "Нужно подойти к скамье в прихожей.";
        if (_facilityBusy || _runtimeBridge?.SessionIdentity is null || !PlayerAtFootwearVestibule()
            || _runtimeBridge.CapturePlayTimeBlocks() != RuntimeBridge.PlayTimeBlock.None
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !player.FootwearPresentationReady || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor())
            return false;
        if (GetTree().GetFirstNodeInGroup("carry_coordinator") is not CarryCoordinator { HeldItem: null, ActionInProgress: false })
        { reason = "Сначала поставьте вещь и освободите руки."; return false; }
        var camera = GetViewport().GetCamera3D();
        if (camera is null || camera != player.GetNode<Camera3D>("Head/Camera3D") || _playerFootwearTarget is null) return false;
        using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition,
            camera.GlobalPosition - camera.GlobalBasis.Z * 2.7f, 7, new global::Godot.Collections.Array<Rid> { player.GetRid() });
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() != _playerFootwearTarget) return false;
        reason = string.Empty;
        return true;
    }

    private async Task TogglePlayerFootwear()
    {
        if (_playerFootwearTarget is not { } target) return;
        target.SetMeta("footwearActionNumber", target.GetMeta("footwearActionNumber", 0).AsInt32() + 1);
        if (!CanChangePlayerFootwear(out var reason))
        {
            target.SetMeta("footwearActionResult", _facilityBusy ? "busy" : reason);
            if (!_facilityBusy && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
                player.NotifyTraversal(reason);
            return;
        }
        var removed = !YardMechanism.Flag(_runtimeBridge!.SelectWorldProps(), PlayerFootwearKey, "removed");
        try
        {
            var committed = await CommitFacilityProps(new JsonArray {
                new JsonObject { ["propId"] = PlayerFootwearKey, ["removed"] = removed } },
                removed ? "Ботинки стоят на нижней полке. Вы остались в носках." : "Вы надели свои ботинки.",
                target.GlobalPosition);
            if (IsInstanceValid(target)) target.SetMeta("footwearActionResult", committed ? "committed" : "session-or-commit-refusal");
        }
        catch (Exception error)
        {
            if (IsInstanceValid(target)) target.SetMeta("footwearActionResult", "exception: " + error.Message);
            GD.PushError("Player footwear action failed: " + error.Message);
        }
    }

    private void ApplyPlayerFootwearState()
    {
        if (_playerFootwearOnShelf is null || _playerFootwearTarget is null
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player) return;
        var removed = PlayerFootwearRemoved;
        player.ProjectIndoorFootwear(removed);
        if (_playerStoredBoots.Count == 0 && player.FootwearPresentationReady)
        {
            var pair = new List<MeshInstance3D>();
            try
            {
                for (var foot = 0; foot < player.FootwearPresentation.Count; foot++)
                {
                    var original = player.FootwearPresentation[foot];
                    var bounds = original.Boots.GetAabb();
                    var anchor = new Vector3(bounds.GetCenter().X, bounds.Position.Y, bounds.GetCenter().Z);
                    var mesh = BuildStoredPlayerBoot(original.Boots, anchor);
                    pair.Add(new MeshInstance3D { Name = foot == 0 ? "AidarBootLeftOnShelf" : "AidarBootRightOnShelf",
                        Mesh = mesh, MaterialOverride = original.BootMaterial, Position = new(3.54f + foot * .22f, .155f, 3.60f),
                        RotationDegrees = new(0, 180, 0) });
                }
            }
            catch { foreach (var item in pair) { item.Mesh.Dispose(); item.Free(); } throw; }
            foreach (var item in pair)
            {
                item.SetMeta("worldPropId", PlayerFootwearKey);
                item.SetMeta("presentationRole", "the player's existing boots, resting on the original lower shelf");
                _playerFootwearOnShelf.AddChild(item);
            }
            _playerStoredBoots.AddRange(pair);
        }
        _playerFootwearOnShelf.Visible = removed;
        _playerFootwearTarget.Prompt = removed ? "Надеть свои ботинки" : "Снять обувь и поставить на полку";
    }

    private void TickPlayerFootwear()
    {
        // Deferred player construction is the only reason to retry the initial
        // projection. Walking between zones never toggles the saved choice.
        if (_playerStoredBoots.Count == 0) ApplyPlayerFootwearState();
        _playerFootwearTarget?.SetPresentationEnabled(FacilityExteriorActive);
    }

    private static ArrayMesh BuildStoredPlayerBoot(ArrayMesh source, Vector3 anchor, bool shadow = false)
    {
        var result = new ArrayMesh { ResourceName = source.ResourceName + "_StoredOnMosqueShelf" };
        try
        {
            for (var surface = 0; surface < source.GetSurfaceCount(); surface++)
            {
                var arrays = source.SurfaceGetArrays(surface);
                arrays[(int)Mesh.ArrayType.Vertex] = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(point => point - anchor).ToArray();
                // Static resting geometry keeps the original silhouette, UVs,
                // normals, tangents and indices; only its unused skin is removed.
                arrays[(int)Mesh.ArrayType.Bones] = default;
                arrays[(int)Mesh.ArrayType.Weights] = default;
                using var lods = MosqueSockSurfaceLods(source, surface);
                result.AddSurfaceFromArrays(source.SurfaceGetPrimitiveType(surface), arrays, lods: lods);
                result.SurfaceSetMaterial(surface, source.SurfaceGetMaterial(surface));
                result.SurfaceSetName(surface, source.SurfaceGetName(surface));
            }
            if (!shadow && source.ShadowMesh is { } originalShadow)
                result.ShadowMesh = BuildStoredPlayerBoot(originalShadow, anchor, shadow: true);
            return result;
        }
        catch { result.Dispose(); throw; }
    }
}
