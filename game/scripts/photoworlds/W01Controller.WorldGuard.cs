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
        profile.AmbientLightEnergy = night.AmbientEnergy;
        profile.AmbientLightSkyContribution = night.AmbientSky;
        profile.TonemapExposure = night.Exposure;
        profile.FogEnabled = true;
        profile.FogMode = global::Godot.Environment.FogModeEnum.Exponential;
        profile.FogLightColor = night.FogColor;
        profile.FogDensity = night.FogDensity;
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
}
