using System;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string YardSpinnerSlug = "babai-yard-childhood-spinner";
    private const string YardSledSlug = "babai-yard-sled-repair";
    private const string PorchNookSlug = "house-exterior-porch-nook";

    private Node3D? _yardSpinner;
    private MeshInstance3D? _yardSledBlueBrace;
    private Node3D? _yardSledRepairCover;
    private Node3D? _porchNookBox;
    private Node3D? _porchNookMitten;
    private MeshInstance3D? _porchNookRedPatch;
    private Vector3 _porchNookBoxAtRest;
    private Vector3 _porchNookBoxMoved;
    private Tween? _yardSpinnerSpin;
    private Tween? _porchNookMove;
    private bool? _yardSpinnerFound;
    private bool? _yardSledFound;
    private bool? _porchNookFound;

    private void BuildAct1YardDiscoveries()
    {
        var village = _zoneInstances["village_day"] as StyleBenchmarkZone
            ?? throw new InvalidOperationException(
                "Connected Act I layout is missing the village_day interaction zone.");
        var core = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox")
            ?? throw new InvalidOperationException(
                "Act I core world is missing its presentation root.");
        var yard = core.GetNodeOrNull<Node3D>("BabaiEbiYard")
            ?? throw new InvalidOperationException(
                "Act I core world is missing the active BabaiEbiYard presentation zone.");
        // The old primitive woodpile is suppressed by BuildAct1CoreWorldGreybox;
        // keep the discovery on the authored replacement instead.
        var activeWoodpile = core.GetNodeOrNull<Node3D>(
                "Act1AuthoredExteriorKitPresentation/BabaiYardAuthoredWoodpile")
            ?? throw new InvalidOperationException(
                "Act I authored yard is missing BabaiYardAuthoredWoodpile.");
        var activeSled = yard.GetNodeOrNull<Node3D>("BabaiYardSled")
            ?? throw new InvalidOperationException(
                "Act I active yard is missing BabaiYardSled.");
        var activeFacade = core.GetNodeOrNull<Node3D>(
                "Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade")
            ?? throw new InvalidOperationException(
                "Act I authored yard is missing BabaiApproachDwellingFacade.");

        activeWoodpile.SetMeta(
            "yardDiscoveryProp",
            "active authored replacement; legacy BabaiEbiYard/BabaiYardWoodpile is suppressed");
        // Find the rendered surface at the spinner's planned local XZ. A max
        // transformed AABB can belong to a neighboring log, leaving the prop
        // floating above the support point. Work in the authored placement's
        // local space so its child anchor, yaw, and scale stay in agreement.
        var woodpileTopLocalY = float.MinValue;
        foreach (var mesh in FindDescendants<MeshInstance3D>(activeWoodpile))
        {
            var meshData = mesh.Mesh;
            if (meshData is null) continue;
            var faces = meshData.GetFaces();
            for (var face = 0; face + 2 < faces.Length; face += 3)
            {
                var a = activeWoodpile.ToLocal(mesh.GlobalTransform * faces[face]);
                var b = activeWoodpile.ToLocal(mesh.GlobalTransform * faces[face + 1]);
                var c = activeWoodpile.ToLocal(mesh.GlobalTransform * faces[face + 2]);
                woodpileTopLocalY = Mathf.Max(
                    woodpileTopLocalY, Mathf.Max(a.Y, Mathf.Max(b.Y, c.Y)));
            }
        }
        if (woodpileTopLocalY == float.MinValue)
        {
            throw new InvalidOperationException(
                "Act I authored yard is missing mesh faces for the spinner support.");
        }

        // The planned support is the child origin (local XZ 0,0). Intersect a
        // downward ray with the actual triangles and choose the highest hit at
        // that point; this follows the upper log's sloped/cylindrical surface.
        var supportRayFrom = new Vector3(0f, woodpileTopLocalY + .05f, 0f);
        var spinnerSupportLocalY = float.MinValue;
        foreach (var mesh in FindDescendants<MeshInstance3D>(activeWoodpile))
        {
            var meshData = mesh.Mesh;
            if (meshData is null) continue;
            var faces = meshData.GetFaces();
            for (var face = 0; face + 2 < faces.Length; face += 3)
            {
                var a = activeWoodpile.ToLocal(mesh.GlobalTransform * faces[face]);
                var b = activeWoodpile.ToLocal(mesh.GlobalTransform * faces[face + 1]);
                var c = activeWoodpile.ToLocal(mesh.GlobalTransform * faces[face + 2]);
                var hit = Geometry3D.RayIntersectsTriangle(
                    supportRayFrom, Vector3.Down, a, b, c);
                if (hit.VariantType != Variant.Type.Nil)
                {
                    spinnerSupportLocalY = Mathf.Max(
                        spinnerSupportLocalY, hit.AsVector3().Y);
                }
            }
        }
        if (spinnerSupportLocalY == float.MinValue)
        {
            throw new InvalidOperationException(
                "Act I authored yard has no woodpile triangle under the spinner support.");
        }
        _yardSpinner = new Node3D
        {
            Name = "YardChildhoodSpinner",
            // SpinnerBody center .10 minus half-height .18/2 leaves a local
            // bottom of .01 m, so place that bottom on the triangle hit.
            Position = new Vector3(0f, spinnerSupportLocalY - .01f, 0f)
        };
        _yardSpinner.SetMeta("presentationOnly", true);
        _yardSpinner.SetMeta("physicalAction", "spin the worn wooden top");
        activeWoodpile.AddChild(_yardSpinner);
        DiscoveryCylinder(_yardSpinner, "SpinnerBody", .10f, .018f, .18f,
            new Vector3(0f, .10f, 0f), "8b573e");
        DiscoveryCylinder(_yardSpinner, "SpinnerRedBand", .105f, .105f, .025f,
            new Vector3(0f, .125f, 0f), "b13b3d");
        DiscoveryCylinder(_yardSpinner, "SpinnerHandle", .018f, .018f, .12f,
            new Vector3(0f, .25f, 0f), "6a4536");
        AddVisualBox(activeWoodpile, "SpinnerCord", new(.24f, .012f, .018f),
            _yardSpinner.Position + new Vector3(.18f, .014f, .02f), "c8b990", "fabric", yawDegrees: -10f);
        var spinnerTarget = DiscoveryTarget(
            village,
            YardSpinnerSlug,
            new(.75f, .85f, .75f),
            village.ToLocal(_yardSpinner.GlobalPosition),
            // The compiled interaction still records the journal entry. Keep
            // the target free of JournalUi auto-open so the first spin stays
            // visible in the world.
            journal: false);
        spinnerTarget.SetMeta("activePropPath",
            "Act1CoreWorldGreybox/Act1AuthoredExteriorKitPresentation/BabaiYardAuthoredWoodpile/YardChildhoodSpinner");
        spinnerTarget.SetMeta("physicalAction", "spin the worn wooden top");
        spinnerTarget.PresentationRepeatAvailable = () =>
            _yardSpinnerFound == true && IsYardSpinnerExteriorZone();
        spinnerTarget.PresentationRepeat = SpinYardSpinner;

        activeSled.SetMeta("yardDiscoveryProp", "active connected-world sled; no hidden legacy duplicate");
        // The original anchor sits behind the front palisade. Pull the sled
        // into the open yard lane using the same layout vectors as its owner,
        // then seat the actual mesh bounds on the traversal heightfield.
        if (Act1WorldLayout.TryGetPlacement("house_old_pc", out var housePlacement))
        {
            var approach = HorizontalDirection(
                GetConnector("arrival-to-house-yard").End - housePlacement.Origin);
            var side = new Vector3(approach.Z, 0f, -approach.X);
            var clearAnchor = housePlacement.Origin + side * -3.2f + approach * 2.4f;
            activeSled.GlobalPosition = new Vector3(
                clearAnchor.X, activeSled.GlobalPosition.Y, clearAnchor.Z);
        }
        var sledMinY = float.MaxValue;
        foreach (var mesh in FindDescendants<MeshInstance3D>(activeSled))
        {
            var meshData = mesh.Mesh;
            if (meshData is null) continue;
            sledMinY = Mathf.Min(
                sledMinY,
                (mesh.GlobalTransform * meshData.GetAabb()).Position.Y);
        }
        if (sledMinY != float.MaxValue)
        {
            var groundedSled = activeSled.GlobalPosition;
            groundedSled.Y += AgentBAct1HeightField.CollisionGround(
                groundedSled.X, groundedSled.Z) - sledMinY;
            activeSled.GlobalPosition = groundedSled;
        }
        _yardSledBlueBrace = AddVisualBox(
            activeSled,
            "SledBlueRepairBrace",
            new(.70f, .085f, .11f),
            new(0f, .49f, .06f),
            "3f6f91",
            "wood_furniture");
        _yardSledBlueBrace.SetMeta("physicalAction", "remove the cover from the blue repair brace");
        _yardSledRepairCover = new Node3D { Name = "SledBlueRepairCover" };
        _yardSledRepairCover.SetMeta("presentationOnly", true);
        _yardSledRepairCover.SetMeta("physicalAction", "remove snow and strap from the repair");
        activeSled.AddChild(_yardSledRepairCover);
        AddVisualBox(_yardSledRepairCover, "RepairSnowCover", new(.76f, .09f, .16f),
            new(0f, .59f, .06f), "eef2f6", "snow_ground");
        AddVisualBox(_yardSledRepairCover, "RepairStrap", new(.78f, .045f, .06f),
            new(0f, .65f, .06f), "6a5742", "fabric", rollDegrees: -3f);
        DiscoveryCylinder(activeSled, "SledBlueRepairBoltWest", .012f, .012f, .008f,
            new(-.22f, .537f, .10f), "4b5960");
        DiscoveryCylinder(activeSled, "SledBlueRepairBoltEast", .015f, .015f, .009f,
            new(.22f, .538f, .10f), "4b5960");
        var sledTarget = DiscoveryTarget(
            village,
            YardSledSlug,
            new(.82f, .82f, 1.10f),
            village.ToLocal(activeSled.GlobalPosition + Vector3.Up * .45f),
            journal: true);
        sledTarget.SetMeta("activePropPath", "Act1CoreWorldGreybox/BabaiEbiYard/BabaiYardSled");
        sledTarget.SetMeta("physicalAction", "inspect the two repairs on the runners");

        activeFacade.SetMeta(
            "yardDiscoveryProp",
            "active authored replacement; legacy HouseExteriorApproach/BabaiEbiHousePorchWarmth is suppressed");
        _porchNookBox = new Node3D
        {
            Name = "HouseExteriorPorchNookBox",
            // Imported GLB local +Z is the facade approach (source Blender
            // local -Y). Keep the crate outside the front wall at the visible
            // threshold; its body extends 0.30 m toward -Z.
            Position = new Vector3(-2.60f, .28f, 1.72f)
        };
        _porchNookBox.SetMeta("presentationOnly", true);
        _porchNookBox.SetMeta("physicalAction", "move the porch crate aside");
        activeFacade.AddChild(_porchNookBox);
        var crateGround = _porchNookBox.GlobalPosition;
        crateGround.Y = AgentBAct1HeightField.CollisionGround(crateGround.X, crateGround.Z);
        _porchNookBox.GlobalPosition = crateGround;
        _porchNookBoxAtRest = _porchNookBox.Position;
        _porchNookBoxMoved = _porchNookBoxAtRest - new Vector3(.72f, 0f, 0f);
        AddVisualBox(_porchNookBox, "CrateBody", new(.82f, .42f, .60f),
            new(0f, .21f, 0f), "80684f", "wood");
        AddVisualBox(_porchNookBox, "CrateLid", new(.88f, .075f, .64f),
            new(0f, .44f, 0f), "9a7958", "wood", rollDegrees: -2f);
        AddVisualBox(_porchNookBox, "CrateFrontSlat", new(.65f, .055f, .045f),
            new(0f, .24f, .32f), "6d5741", "wood");
        _porchNookMitten = new Node3D
        {
            Name = "StoredMendedMitten",
            Position = _porchNookBoxAtRest + new Vector3(.22f, .08f, .04f),
            Visible = false
        };
        _porchNookMitten.SetMeta("presentationOnly", true);
        _porchNookMitten.SetMeta("physicalAction", "reveal the red mended thumb");
        activeFacade.AddChild(_porchNookMitten);
        _porchNookMitten.AddChild(new MeshInstance3D
        {
            Name = "MittenPalm", Scale = new(1, 1, .8f),
            Mesh = new SphereMesh { Radius = .12f, Height = .13f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("76504a", "fabric", sheltered: true)
        });
        _porchNookMitten.AddChild(new MeshInstance3D
        {
            Name = "MittenThumb", Position = new(.095f, 0, .01f), Scale = new(1, 1, .7f),
            Mesh = new SphereMesh { Radius = .045f, Height = .10f, RadialSegments = 10, Rings = 5 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("76504a", "fabric", sheltered: true)
        });
        AddVisualBox(_porchNookMitten, "MittenCuff", new(.15f, .08f, .08f),
            new(0f, 0f, -.09f), "62515a", "fabric");
        _porchNookRedPatch = AddVisualBox(
            _porchNookMitten,
            "MittenRedPatch",
            new(.065f, .007f, .055f),
            new(.088f, .052f, .01f),
            "b13b3d",
            "fabric");
        _porchNookRedPatch.MaterialOverride = PainterlyMaterialLibrary.ForColor("b13b3d", "fabric", sheltered: true);
        _porchNookRedPatch.Visible = false;
        var porchTarget = DiscoveryTarget(
            village,
            PorchNookSlug,
            new(.92f, .78f, .92f),
            village.ToLocal(_porchNookBox.GlobalPosition + Vector3.Up * .28f),
            journal: false);
        porchTarget.SetMeta(
            "activePropPath",
            "Act1CoreWorldGreybox/Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade/HouseExteriorPorchNookBox");
        porchTarget.SetMeta("physicalAction", "move the porch crate aside");
    }

    private bool IsYardSpinnerExteriorZone() =>
        ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";

    private void SpinYardSpinner()
    {
        if (_yardSpinner is null
            || !GodotObject.IsInstanceValid(_yardSpinner)
            || !IsYardSpinnerExteriorZone())
        {
            return;
        }

        _yardSpinnerSpin?.Kill();
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ReducedMotion: true })
        {
            _yardSpinnerSpin = null;
            return;
        }

        _yardSpinnerSpin = CreateTween();
        _yardSpinnerSpin.TweenProperty(
            _yardSpinner,
            "rotation:y",
            _yardSpinner.Rotation.Y + Mathf.Tau * 5f,
            1.7f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private void UpdateAct1YardDiscoveries()
    {
        if (_runtimeBridge?.ActiveSceneId is null) return;
        var knowledge = _runtimeBridge.SelectRuntimeState().GetProperty("knowledge");
        bool Found(string slug) => knowledge.TryGetProperty(DiscoveryPrefix + slug, out var entry)
            && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";
        var exterior = IsYardSpinnerExteriorZone();

        var spinnerFound = Found(YardSpinnerSlug);
        if (_yardSpinner is not null && _yardSpinnerFound != spinnerFound)
        {
            _yardSpinnerSpin?.Kill();
            if (_yardSpinnerFound == false && spinnerFound && exterior)
            {
                SpinYardSpinner();
            }
            else if (!spinnerFound)
            {
                _yardSpinner.Rotation = Vector3.Zero;
            }
            _yardSpinnerFound = spinnerFound;
        }

        var sledFound = Found(YardSledSlug);
        if (_yardSledBlueBrace is not null && _yardSledFound != sledFound)
        {
            _yardSledBlueBrace.Visible = true;
            if (_yardSledRepairCover is not null) _yardSledRepairCover.Visible = !sledFound;
            _yardSledFound = sledFound;
        }

        var nookFound = Found(PorchNookSlug);
        if (_porchNookBox is not null && _porchNookFound != nookFound)
        {
            _porchNookMove?.Kill();
            if (_porchNookMitten is not null) _porchNookMitten.Visible = nookFound;
            if (_porchNookRedPatch is not null) _porchNookRedPatch.Visible = nookFound;
            if (_porchNookFound == false && nookFound && exterior)
            {
                _porchNookMove = CreateTween();
                _porchNookMove.TweenProperty(
                    _porchNookBox,
                    "position:x",
                    _porchNookBoxMoved.X,
                    .45f);
            }
            else
            {
                _porchNookBox.Position = nookFound ? _porchNookBoxMoved : _porchNookBoxAtRest;
            }
            _porchNookFound = nookFound;
        }
    }
}
