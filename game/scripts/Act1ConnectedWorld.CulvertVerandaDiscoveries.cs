using System;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const float ZiratCulvertZ = -67f;
    private const string ZiratOuterCulvertSlug = "zirat-outer-culvert-crossing";
    private const string HouseExteriorViewSlug = "house-exterior-rear-minaret-view";

    private Node3D? _ziratOuterCulvertSnow;
    private StaticBody3D? _ziratOuterCulvertCollision;
    private bool? _ziratOuterCulvertFound;

    private void BuildAct1CulvertVerandaDiscoveries()
    {
        var village = _zoneInstances["village_day"] as StyleBenchmarkZone
            ?? throw new InvalidOperationException(
                "Connected Act I layout is missing the village_day interaction zone.");
        var core = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox")
            ?? throw new InvalidOperationException(
                "Act I core world is missing its presentation root.");

        BuildZiratOuterCulvert(village, core);
        BuildHouseExteriorView(village, core);
    }

    private void BuildZiratOuterCulvert(StyleBenchmarkZone village, Node3D core)
    {
        var zirat = core.GetNodeOrNull<Node3D>("ZiratMemoryField")
            ?? throw new InvalidOperationException(
                "The zirat memory field is required for the outer culvert discovery.");
        var sourceBanks = zirat.GetNodeOrNull<Node3D>(
                "ZiratRoadsideAuthoredKitPresentation/ZiratCulvertStoneCluster")
            ?? throw new InvalidOperationException(
                "The active zirat roadside kit is missing its culvert stone cluster.");

        // This is on the outside edge of the road before the first graves. The
        // existing culvert at z=-72 is part of the grave-side composition, so it
        // is deliberately not reused as the crossing anchor.
        const float culvertZ = ZiratCulvertZ;
        const float westX = 2.35f;
        const float eastX = 4.95f;
        var westY = AgentBAct1HeightField.CollisionGround(westX, culvertZ);
        var eastY = AgentBAct1HeightField.CollisionGround(eastX, culvertZ);
        var deckLength = eastX - westX;
        var deckCenter = new Vector3(
            (westX + eastX) * .5f,
            (westY + eastY) * .5f,
            culvertZ);
        var deckSlope = Mathf.Atan2(eastY - westY, deckLength);

        var sourcePath = sourceBanks.GetPath().ToString();
        var outerBanks = new Node3D { Name = "ZiratOuterCulvertStoneBanks" };
        zirat.AddChild(outerBanks);
        outerBanks.SetMeta("presentationOnly", true);
        outerBanks.SetMeta("visualOnly", true);
        // Low stones support the sides, leaving the deck and both landings clear.
        foreach (var bankX in new[] { westX + .18f, eastX - .18f })
        foreach (var side in new[] { -1f, 1f })
            AddVisualStoneCluster(outerBanks, $"Bank{bankX}_{side}",
                new(bankX, AgentBAct1HeightField.CollisionGround(bankX, culvertZ + side * .68f) - .06f,
                    culvertZ + side * .68f), .27f, "707a72", organic: true);

        // Keep the deck and ditch as a small authored presentation, while the
        // permanent body below makes the shortcut a real walkable crossing.
        var bridge = new Node3D
        {
            Name = "ZiratOuterCulvertCrossing",
            Position = deckCenter,
            Rotation = new Vector3(0f, 0f, deckSlope)
        };
        bridge.SetMeta("presentationOnly", true);
        bridge.SetMeta("visualOnly", true);
        bridge.SetMeta("physicalAction", "check the outer ditch footbridge");
        bridge.SetMeta("assetSource", ZiratRoadsideKitScenePath);
        bridge.SetMeta("layoutAnchor", "zirat_road + (3.65, -67.0)");
        bridge.SetMeta("routeGeometry",
            "west bank (2.35,-67.0) -> 2.60m deck -> east shoulder (4.95,-67.0); short crossing over the ditch");
        bridge.SetMeta("plotGate", false);
        zirat.AddChild(bridge);

        AddVisualBox(bridge, "DitchWater", new(deckLength + .45f, .035f, 1.08f),
            new(0f, .02f, 0f), "394d52", "water");
        AddVisualBox(bridge, "FootbridgeDeck", new(deckLength, .12f, .90f),
            new(0f, .08f, 0f), "705238", "wood_furniture");
        for (var index = 1; index < 4; index++)
        {
            var plankX = Mathf.Lerp(-deckLength * .5f, deckLength * .5f, index / 4f);
            AddVisualBox(bridge, $"FootbridgePlankSeam{index}", new(.035f, .13f, .92f),
                new(plankX, .083f, 0f), "4d3829", "wood_furniture");
        }
        _ziratOuterCulvertSnow = new Node3D { Name = "FootbridgeSnowCap" };
        _ziratOuterCulvertSnow.SetMeta("presentationOnly", true);
        _ziratOuterCulvertSnow.SetMeta("visualOnly", true);
        bridge.AddChild(_ziratOuterCulvertSnow);
        AddVisualBox(_ziratOuterCulvertSnow, "DeckSnow", new(deckLength - .10f, .065f, .94f),
            new(0f, .175f, 0f), "e5edf0", "snow_ground");

        _ziratOuterCulvertCollision = new StaticBody3D
        {
            Name = "ZiratOuterCulvertCollision",
            Transform = village.GlobalTransform.AffineInverse() * bridge.GlobalTransform
        };
        _ziratOuterCulvertCollision.SetMeta("physicalShortcut", true);
        _ziratOuterCulvertCollision.SetMeta("plotGate", false);
        village.AddChild(_ziratOuterCulvertCollision);
        _ziratOuterCulvertCollision.AddChild(new CollisionShape3D
        {
            Name = "FootbridgeDeck",
            Position = new(0f, .08f, 0f),
            Shape = new BoxShape3D { Size = new(deckLength, .20f, .90f) }
        });

        // Seat tapered plank ends into the banks so walking does not hit
        // the vertical 18 cm edge of the deck's collision box.
        foreach (var side in new[] { -1f, 1f })
        {
            var inner = bridge.ToGlobal(new(side * (deckLength * .5f - .08f), .18f, 0f));
            var outerX = (side < 0 ? westX : eastX) + side * .7f;
            var outer = new Vector3(outerX, AgentBAct1HeightField.CollisionGround(outerX, culvertZ) - .01f, culvertZ);
            var delta = inner - outer;
            var center = (inner + outer) * .5f - Vector3.Up * .035f;
            var slope = Mathf.Atan2(delta.Y * -side, Mathf.Abs(delta.X));
            var ramp = AddVisualBox(zirat, side < 0 ? "CulvertWestLanding" : "CulvertEastLanding",
                new(delta.Length(), .06f, .9f), center, "705238", "wood_furniture",
                rollDegrees: Mathf.RadToDeg(slope));
            _ziratOuterCulvertCollision.AddChild(new CollisionShape3D
            {
                Name = ramp.Name + "Collision",
                Transform = _ziratOuterCulvertCollision.GlobalTransform.AffineInverse() * ramp.GlobalTransform,
                Shape = new BoxShape3D { Size = new(delta.Length(), .06f, .9f) }
            });
        }

        var target = DiscoveryTarget(
            village,
            ZiratOuterCulvertSlug,
            new(2.65f, 1.05f, 1.08f),
            village.ToLocal(bridge.GlobalPosition + Vector3.Up * .58f),
            journal: true);
        target.SetMeta("activePropPath", outerBanks.GetPath().ToString());
        target.SetMeta("sourceAsset", ZiratRoadsideKitScenePath);
        target.SetMeta("supportSourcePath", sourcePath);
        target.SetMeta("layoutAnchor", "zirat_road + (3.65, -67.0)");
        target.SetMeta("routeGeometry",
            "west bank (2.35,-67.0) -> 2.60m deck -> east shoulder (4.95,-67.0); short crossing over the ditch");
        target.SetMeta("physicalAction", "check the outer ditch footbridge");
        target.SetMeta("graveExclusion",
            "outer roadside crossing; not inside graves or the existing z=-72 culvert composition");
        target.SetMeta("plotGate", false);
    }

    private void BuildHouseExteriorView(StyleBenchmarkZone village, Node3D core)
    {
        var facade = core.GetNodeOrNull<Node3D>(
                "Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade")
            ?? throw new InvalidOperationException(
                "The active Babai approach dwelling facade is required for the rear-corner view discovery.");
        var minaret = core.GetNodeOrNull<Node3D>("DistantMinaretSilhouette")
            ?? throw new InvalidOperationException(
                "The authored distant minaret is required for the rear-corner view discovery.");

        // Use the actual rear corner posts of the authored dwelling. The
        // player walks around the house and looks toward the existing
        // silhouette; no window, glass override or extra post participates in
        // this discovery.
        var rearCorners = FindDescendants<MeshInstance3D>(facade)
            .Where(mesh => mesh.Mesh is not null
                && mesh.Name.ToString().StartsWith("DwellingFacade_Corner_", StringComparison.Ordinal)
                && mesh.Position.Z < -4f)
            .ToArray();
        if (rearCorners.Length < 2)
            throw new InvalidOperationException(
                "The authored dwelling is missing its two rear corner posts for the minaret view.");

        var rearCorner = rearCorners
            .OrderBy(mesh => mesh.GlobalPosition.DistanceSquaredTo(minaret.GlobalPosition))
            .First();
        var cornerMesh = rearCorner.Mesh
            ?? throw new InvalidOperationException(
                "The selected authored rear corner has no mesh for the minaret view.");
        var cornerBounds = rearCorner.GlobalTransform * cornerMesh.GetAabb();
        var awayFromHouse = HorizontalDirection(cornerBounds.GetCenter() - facade.GlobalPosition);
        var accessAnchor = cornerBounds.GetCenter() + awayFromHouse * 1.60f;
        accessAnchor.Y = AgentBAct1HeightField.CollisionGround(accessAnchor.X, accessAnchor.Z);

        var target = DiscoveryTarget(
            village,
            HouseExteriorViewSlug,
            new(1.05f, 1.25f, 1.05f),
            village.ToLocal(accessAnchor + Vector3.Up * .68f),
            journal: true);
        target.SetMeta("activePropPath", rearCorner.GetPath().ToString());
        target.SetMeta("sourceFacadePath", facade.GetPath().ToString());
        target.SetMeta("sourceCornerPath", rearCorner.GetPath().ToString());
        target.SetMeta("architectureMode",
            "existing-authored-house-rear-corner; no window sightline or added posts");
        target.SetMeta("viewTargetPath", minaret.GetPath().ToString());
        target.SetMeta("viewTargetWorldPosition", minaret.GlobalPosition);
        target.SetMeta("viewDirection",
            HorizontalDirection(minaret.GlobalPosition - accessAnchor));
        target.SetMeta("accessAnchor", accessAnchor);
        target.SetMeta("targetWorldPosition", accessAnchor);
        target.SetMeta("routeGeometry",
            "Babai yard -> around the selected real rear corner -> face the existing DistantMinaretSilhouette");
        target.SetMeta("requiresTeleport", false);
        target.SetMeta("requiresCrouch", false);
        target.SetMeta("physicalAction", "walk behind the house and look around the rear corner");
        target.SetMeta("routeReconnect", "arrival-to-house-yard / Babai dwelling rear corner");
        target.SetMeta("plotGate", false);
    }

    private void UpdateAct1CulvertVerandaDiscoveries()
    {
        if (_runtimeBridge?.ActiveSceneId is null) return;
        var knowledge = _runtimeBridge.SelectRuntimeState().GetProperty("knowledge");
        bool Found(string slug) => knowledge.TryGetProperty(DiscoveryPrefix + slug, out var entry)
            && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";
        var exterior = ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";

        if (_ziratOuterCulvertCollision is not null)
            _ziratOuterCulvertCollision.CollisionLayer = exterior ? 1u : 0u;
        var culvertFound = Found(ZiratOuterCulvertSlug);
        if (_ziratOuterCulvertSnow is not null && _ziratOuterCulvertFound != culvertFound)
        {
            _ziratOuterCulvertSnow.Visible = !culvertFound;
            _ziratOuterCulvertFound = culvertFound;
        }

    }
}
