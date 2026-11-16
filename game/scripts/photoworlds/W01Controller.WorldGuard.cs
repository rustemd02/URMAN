using System;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private const float ForestWorldGuardInset = 0.5f;
    private const string ForestWorldGroundName = "F00F16GroundVisual";
    private const string PrologueNightAtmosphereId = "kara-winter-night-edge";

    private Aabb _forestWorldGroundBoundsLocal;
    private bool _forestWorldGroundBoundsCached;
    private Func<Vector3, (Vector3 Position, float Ground)>? _ownedForestWorldGuard;
    private object? _forestWorldGuardSessionIdentity;
    private bool _forestWorldGuardBoundsErrorReported;
    private WorldEnvironment? _w01EnvironmentOwner;
    private global::Godot.Environment? _authoredW01Environment;
    private global::Godot.Environment? _prologueW01Environment;
    private DirectionalLight3D? _w01Daylight;
    private OmniLight3D? _prologueClearingMoonlight;
    private (Color Color, float Energy, float ShadowOpacity, Vector3 Rotation) _authoredW01Sun;
    private bool _w01AtmosphereOwnerErrorReported;
    private bool _prologueW01EnvironmentCreationAttempted;
    private bool _prologueAtmosphereApplied;

    private void ApplyForestWorldGuardProjection(RuntimeBridge.W01BranchProjection? projection)
    {
        if (!IsCurrentForestWorldProjection(projection, out var bridge))
        {
            ApplyW01AtmosphereProjection(null);
            ReleaseForestWorldGuard();
            return;
        }

        ApplyW01AtmosphereProjection(projection);
        _forestWorldGuardSessionIdentity = projection!.SessionIdentity;
        EnsureForestWorldGuardInstalled(bridge);
    }

    private void ProcessForestWorldGuard()
    {
        if (!IsInsideTree() || IsQueuedForDeletion()
            || _bridge is not { } bridge || !IsValidCurrentBridge(bridge))
        {
            ApplyW01AtmosphereProjection(null);
            ReleaseForestWorldGuard();
            return;
        }

        var projection = bridge.CurrentW01BranchProjection();
        if (!IsCurrentForestWorldProjection(projection, out bridge))
        {
            ApplyW01AtmosphereProjection(null);
            ReleaseForestWorldGuard();
            return;
        }

        ApplyW01AtmosphereProjection(projection);
        _forestWorldGuardSessionIdentity = projection!.SessionIdentity;
        EnsureForestWorldGuardInstalled(bridge);
    }

    private void ApplyW01AtmosphereProjection(RuntimeBridge.W01BranchProjection? projection)
    {
        if (_w01EnvironmentOwner is null || _authoredW01Environment is null || _w01Daylight is null)
        {
            if (_w01AtmosphereOwnerErrorReported) return;
            var foundOwner = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
            var foundDaylight = GetNodeOrNull<DirectionalLight3D>("Daylight");
            var clearingMoonlight = GetNodeOrNull<OmniLight3D>("PrologueClearingMoonlight");
            if (foundOwner?.Environment is not { } environment || foundDaylight is null || clearingMoonlight is null)
            {
                GD.PushError("W01 atmosphere requires its authored environment and lights.");
                _w01AtmosphereOwnerErrorReported = true;
                return;
            }

            _w01EnvironmentOwner = foundOwner;
            _authoredW01Environment = environment;
            _w01Daylight = foundDaylight;
            _prologueClearingMoonlight = clearingMoonlight;
            _authoredW01Sun = (foundDaylight.LightColor, foundDaylight.LightEnergy,
                foundDaylight.ShadowOpacity, foundDaylight.RotationDegrees);
        }

        var owner = _w01EnvironmentOwner!;
        var authoredEnvironment = _authoredW01Environment!;
        var daylight = _w01Daylight!;

        if (projection is { ActiveSceneId: PrologueSceneId })
        {
            if (_prologueAtmosphereApplied) return;
            var night = AtmosphereProfiles.Get(PrologueNightAtmosphereId);
            if (!_prologueW01EnvironmentCreationAttempted)
            {
                _prologueW01EnvironmentCreationAttempted = true;
                _prologueW01Environment = CreatePrologueW01Environment(authoredEnvironment, night);
            }
            if (_prologueW01Environment is null) return;

            owner.Environment = _prologueW01Environment;
            // The scene-authored environment is also the full-forest look, so only the prologue uses this clone.
            daylight.LightColor = night.SunColor;
            daylight.LightEnergy = night.SunEnergy;
            daylight.ShadowOpacity = night.ShadowOpacity;
            daylight.RotationDegrees = night.SunRotation;
            _prologueClearingMoonlight!.Show();
            GraphicsQuality.ConfigureEnvironment(_prologueW01Environment);
            GraphicsQuality.ConfigureSun(daylight);
            _prologueAtmosphereApplied = true;
        }
        else if (_prologueAtmosphereApplied)
        {
            owner.Environment = authoredEnvironment;
            daylight.LightColor = _authoredW01Sun.Color;
            daylight.LightEnergy = _authoredW01Sun.Energy;
            daylight.ShadowOpacity = _authoredW01Sun.ShadowOpacity;
            daylight.RotationDegrees = _authoredW01Sun.Rotation;
            _prologueClearingMoonlight!.Hide();
            GraphicsQuality.ConfigureEnvironment(authoredEnvironment);
            GraphicsQuality.ConfigureSun(daylight);
            _prologueAtmosphereApplied = false;
        }
    }

    private static global::Godot.Environment? CreatePrologueW01Environment(
        global::Godot.Environment authoredEnvironment, AtmosphereProfile night)
    {
        if (authoredEnvironment.Duplicate(true) is not global::Godot.Environment profile
            || profile.Sky?.SkyMaterial is not ProceduralSkyMaterial sky)
        {
            GD.PushError("W01 prologue atmosphere could not clone its authored procedural-sky environment.");
            return null;
        }

        sky.SkyTopColor = night.SkyTop;
        sky.SkyHorizonColor = night.SkyHorizon;
        sky.GroundHorizonColor = night.GroundHorizon;
        sky.GroundBottomColor = night.GroundBottom;
        sky.SunAngleMax = night.SunAngleMax;
        sky.SunCurve = night.SunCurve;
        sky.SkyCoverModulate = night.Cover;
        profile.AmbientLightSource = global::Godot.Environment.AmbientSource.Color;
        profile.AmbientLightColor = night.AmbientColor;
        profile.AmbientLightEnergy = night.AmbientEnergy * .72f;
        profile.AmbientLightSkyContribution = night.AmbientSky;
        profile.TonemapExposure = night.Exposure;
        profile.FogEnabled = true;
        profile.FogMode = global::Godot.Environment.FogModeEnum.Exponential;
        // Hold the snow and tracks in the foreground while the tree line loses depth.
        profile.FogLightColor = night.FogColor.Darkened(.35f);
        profile.FogDensity = night.FogDensity * 1.32f;
        profile.FogHeight = night.FogHeight;
        profile.FogHeightDensity = night.FogHeightDensity;
        profile.FogAerialPerspective = night.FogAerial;
        profile.FogSkyAffect = night.FogSkyAffect;
        profile.FogSunScatter = night.FogSunScatter;
        profile.SsaoIntensity = night.SsaoIntensity;
        profile.SsaoRadius = night.SsaoRadius;
        // Record this variant's authored ambient once; reapplying a quality preset must not compound it.
        GraphicsQuality.ConfigureEnvironment(profile, authoredSsao: true);
        return profile;
    }

    private void ReleaseForestWorldGuard()
    {
        var ownedGuard = _ownedForestWorldGuard;
        if (ownedGuard is not null
            && ReferenceEquals(FirstPersonController.DetachedWorldGuard, ownedGuard))
        {
            FirstPersonController.DetachedWorldGuard = null;
        }

        _forestWorldGuardSessionIdentity = null;
    }

    private bool IsCurrentForestWorldProjection(
        RuntimeBridge.W01BranchProjection? projection,
        out RuntimeBridge bridge)
    {
        bridge = null!;
        if (!IsInsideTree() || IsQueuedForDeletion()
            || _bridge is not { } currentBridge || !IsValidCurrentBridge(currentBridge)
            || projection is null
            || !ReferenceEquals(projection.SessionIdentity, currentBridge.SessionIdentity)
            || projection.ActiveSceneId is not (PrologueSceneId or ForestSceneId))
        {
            return false;
        }

        var currentProjection = currentBridge.CurrentW01BranchProjection();
        if (currentProjection is null
            || !ReferenceEquals(currentProjection.SessionIdentity, projection.SessionIdentity)
            || currentProjection.ActiveSceneId != projection.ActiveSceneId)
        {
            return false;
        }

        bridge = currentBridge;
        return true;
    }

    private bool IsValidCurrentBridge(RuntimeBridge bridge) =>
        GodotObject.IsInstanceValid(this)
        && !IsQueuedForDeletion()
        && IsInsideTree()
        && GodotObject.IsInstanceValid(bridge)
        && !bridge.IsQueuedForDeletion()
        && bridge.IsInsideTree()
        && GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge treeBridge
        && ReferenceEquals(treeBridge, bridge);

    private void EnsureForestWorldGuardInstalled(RuntimeBridge bridge)
    {
        if (!IsForestGroundEnabled())
        {
            ReleaseForestWorldGuard();
            return;
        }

        if (!TryCacheForestWorldGroundBounds())
        {
            ReleaseForestWorldGuard();
            return;
        }

        _forestWorldGuardSessionIdentity = bridge.SessionIdentity;
        _ownedForestWorldGuard ??= ClampToForestWorld;
        // Another scene may clear its own guard during QueueFree after this
        // projection. Reassert only while this exact W01 presentation is active.
        FirstPersonController.DetachedWorldGuard = _ownedForestWorldGuard;
    }

    private bool IsForestGroundEnabled() =>
        _groundVisual is { } visual
        && GodotObject.IsInstanceValid(visual)
        && !visual.IsQueuedForDeletion()
        && visual.IsInsideTree()
        && visual.Name == ForestWorldGroundName
        && visual.Visible
        && _groundBody is { } body
        && GodotObject.IsInstanceValid(body)
        && !body.IsQueuedForDeletion()
        && body.IsInsideTree()
        && body.CollisionLayer != 0u;

    private bool TryCacheForestWorldGroundBounds()
    {
        if (_forestWorldGroundBoundsCached) return true;
        if (!IsForestGroundEnabled() || _groundVisual is not { } ground)
            return false;

        var toController = GlobalTransform.AffineInverse();
        var anyMesh = false;
        var bounds = new Aabb();
        foreach (var mesh in Descendants<MeshInstance3D>(ground))
        {
            if (!GodotObject.IsInstanceValid(mesh) || mesh.IsQueuedForDeletion()
                || mesh.Mesh is not { } resource)
                continue;

            var meshBounds = toController * mesh.GlobalTransform * resource.GetAabb();
            bounds = anyMesh ? bounds.Merge(meshBounds) : meshBounds;
            anyMesh = true;
        }

        if (!anyMesh
            || bounds.Size.X <= ForestWorldGuardInset * 2f
            || bounds.Size.Z <= ForestWorldGuardInset * 2f)
        {
            if (!_forestWorldGuardBoundsErrorReported)
            {
                GD.PushError("W01 world guard could not derive a usable bounds union from F00F16GroundVisual.");
                _forestWorldGuardBoundsErrorReported = true;
            }

            return false;
        }

        _forestWorldGroundBoundsLocal = bounds;
        _forestWorldGroundBoundsCached = true;
        return true;
    }

    private (Vector3 Position, float Ground) ClampToForestWorld(Vector3 worldPosition)
    {
        if (_forestWorldGuardSessionIdentity is not { } ownerSession
            || _bridge is not { } bridge || !IsValidCurrentBridge(bridge)
            || !ReferenceEquals(bridge.SessionIdentity, ownerSession)
            || bridge.CurrentW01BranchProjection() is not { } projection
            || !ReferenceEquals(projection.SessionIdentity, ownerSession)
            || projection.ActiveSceneId is not (PrologueSceneId or ForestSceneId)
            || !IsForestGroundEnabled()
            || !_forestWorldGroundBoundsCached)
        {
            return (worldPosition, worldPosition.Y);
        }

        var localPosition = ToLocal(worldPosition);
        var localX = Mathf.Clamp(
            localPosition.X,
            _forestWorldGroundBoundsLocal.Position.X + ForestWorldGuardInset,
            _forestWorldGroundBoundsLocal.End.X - ForestWorldGuardInset);
        var localZ = Mathf.Clamp(
            localPosition.Z,
            _forestWorldGroundBoundsLocal.Position.Z + ForestWorldGuardInset,
            _forestWorldGroundBoundsLocal.End.Z - ForestWorldGuardInset);

        var clampedWorld = ToGlobal(new Vector3(localX, localPosition.Y, localZ));
        var groundWorld = ToGlobal(new Vector3(localX, ForestGroundHeight(localX, localZ), localZ));
        return (new Vector3(clampedWorld.X, worldPosition.Y, clampedWorld.Z), groundWorld.Y);
    }

    internal bool TryResolveLegacyPrologueLandscapeFeet(FirstPersonController player, out Vector3 destination)
    {
        var oldFeet = player.GlobalPosition;
        destination = oldFeet;
        var projection = _bridge?.CurrentW01BranchProjection();
        if (player.VehicleControlled
            || !IsCurrentForestWorldProjection(projection, out _)
            || projection is not { ActiveSceneId: PrologueSceneId }
            || _prologueThicket is not { } thicket || !thicket.IsVisibleInTree()
            || _prologueLandscapeBounds.Size.X <= 0f || _prologueLandscapeBounds.Size.Z <= 0f)
            return false;

        var local = ToLocal(oldFeet);
        var bounds = _prologueLandscapeBounds;
        if (local.X < bounds.Position.X || local.X > bounds.End.X
            || local.Z < bounds.Position.Z || local.Z > bounds.End.Z)
            return false;
        var inset = Mathf.Min(Mathf.Min(local.X - bounds.Position.X, bounds.End.X - local.X),
            Mathf.Min(local.Z - bounds.Position.Z, bounds.End.Z - local.Z));
        if (inset > 44f) return false;

        var bank = thicket.GetNodeOrNull<StaticBody3D>("ForestBeyondTrail/SnowBankContact");
        if (bank is null || !bank.IsInsideTree() || !thicket.IsAncestorOf(bank)
            || !bank.IsVisibleInTree() || (bank.CollisionLayer & player.CollisionMask) == 0u
            || !bank.HasMeta("footstepSurface") || bank.GetMeta("footstepSurface").AsString() != "snow_soft")
            return false;

        var space = player.GetWorld3D().DirectSpaceState;
        var rayExclusions = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        using var rayExclusionsOwner = (global::Godot.Collections.Array)rayExclusions;
        using var ray = PhysicsRayQueryParameters3D.Create(
            oldFeet + Vector3.Up * 32f, oldFeet - Vector3.Up * .05f, player.CollisionMask, rayExclusions);
        using var hit = space.IntersectRay(ray);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() != bank
            || hit["normal"].AsVector3().Dot(Vector3.Up) < .7f)
            return false;

        var face = hit["position"].AsVector3();
        if (_groundBody is not { } ground || !ground.IsInsideTree() || !IsAncestorOf(ground)
            || (ground.CollisionLayer & player.CollisionMask) == 0u)
            return false;
        var groundExclusions = new global::Godot.Collections.Array<Rid> { player.GetRid(), bank.GetRid() };
        using var groundExclusionsOwner = (global::Godot.Collections.Array)groundExclusions;
        using var groundRay = PhysicsRayQueryParameters3D.Create(
            oldFeet + Vector3.Up * .5f, oldFeet - Vector3.Up * .5f, player.CollisionMask, groundExclusions);
        using var groundHit = space.IntersectRay(groundRay);
        if (groundHit.Count == 0 || groundHit["collider"].AsGodotObject() != ground
            || groundHit["normal"].AsVector3().Dot(Vector3.Up) < .7f)
            return false;
        var oldGroundFace = groundHit["position"].AsVector3();
        if (Mathf.Abs(oldFeet.Y - oldGroundFace.Y) > .35f || face.Y - oldGroundFace.Y <= .30f)
            return false;
        var candidate = new Vector3(oldFeet.X, face.Y + .003f, oldFeet.Z);
        var motion = candidate - oldFeet;
        if (motion.Y <= .02f || motion.Y > 20f
            || player.GetNodeOrNull<CollisionShape3D>("CollisionShape3D") is not { Shape: { } shape } capsule)
            return false;

        var moveExclusions = new global::Godot.Collections.Array<Rid> { player.GetRid(), bank.GetRid() };
        using var moveExclusionsOwner = (global::Godot.Collections.Array)moveExclusions;
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = capsule.GlobalTransform,
            CollisionMask = player.CollisionMask,
            Exclude = moveExclusions,
            Margin = .002f
        };
        var initialOverlap = space.IntersectShape(query, 1);
        using var initialOverlapOwner = (global::Godot.Collections.Array)initialOverlap;
        if (initialOverlap.Count != 0) return false;
        query.Motion = motion;
        var travel = space.CastMotion(query);
        if (travel.Length < 2 || travel[0] < .999999f || travel[1] < .999999f) return false;

        var finalExclusions = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        using var finalExclusionsOwner = (global::Godot.Collections.Array)finalExclusions;
        query.Motion = Vector3.Zero;
        query.Transform = new Transform3D(capsule.GlobalBasis, capsule.GlobalPosition + motion);
        query.Exclude = finalExclusions;
        var finalOverlap = space.IntersectShape(query, 1);
        using var finalOverlapOwner = (global::Godot.Collections.Array)finalOverlap;
        if (finalOverlap.Count != 0 || !player.CanCrouchAt(candidate)) return false;

        destination = candidate;
        return true;
    }
}
