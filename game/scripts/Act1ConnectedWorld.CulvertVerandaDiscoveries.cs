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
    private Node3D? _underdeckRepairRoot;
    private Node3D? _heroYardShed;

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
        BuildHeroYardLoft(village, core);
    }

    private void BuildHeroYardLoft(StyleBenchmarkZone village, Node3D core)
    {
        var presentation = core.GetNode<Node3D>("Act1AuthoredExteriorKitPresentation");
        var previous = presentation.GetNode<Node3D>("BabaiYardAuthoredShed");
        var anchor = previous.Position;
        var yaw = previous.RotationDegrees.Y;
        // This replaces the former closed low shed at the same authored yard
        // anchor. Retain its placement identity; there must be one building here.
        presentation.RemoveChild(previous);
        previous.Free();
        var imported = ResourceLoader.Load<PackedScene>(VillageExteriorKitScenePath).Instantiate<Node3D>();
        var source = imported.FindChild("HeroYardShed_Loft", true, false) as Node3D
            ?? throw new InvalidOperationException("The village kit has no authored HeroYardShed_Loft component.");
        var shed = AttachAct1ExteriorKitComponent(presentation, source, "BabaiYardAuthoredShed",
            anchor, yaw, Vector3.One, "house_old_pc@two-level-yard-shed");
        imported.Free();
        _heroYardShed = shed;
        shed.SetMeta("smallSpaceContract", "hero-yard-shed-two-level-v1");
        shed.SetMeta("replacedRuntimeComponent", "OutbuildingShed_Low; same yard anchor, one metric building");
        shed.SetMeta("collisionOwner", "act1-exterior-architecture");
        shed.SetMeta("presentationOnly", false);
        shed.SetMeta("visualOnly", false);

        // A level stone platform sits above the highest terrain triangle in its
        // footprint. The common foundation owner scribes the gap below it; the
        // three short sloped entrances below connect both levels to real ground.
        var terrainTop = float.NegativeInfinity;
        for (var ix = 0; ix <= 10; ix++)
        for (var iz = 0; iz <= 10; iz++)
        {
            var sample = shed.ToGlobal(new(Mathf.Lerp(-2.2f, 2.2f, ix / 10f), 0,
                Mathf.Lerp(-2.2f, 2.2f, iz / 10f)));
            terrainTop = Math.Max(terrainTop, AgentBAct1HeightField.CollisionGround(sample.X, sample.Z));
        }
        for (var iz = 0; iz <= 5; iz++)
        foreach (var x in new[] { -.49f, 0f, .49f })
        {
            var sample = shed.ToGlobal(new(x, 0, Mathf.Lerp(2.2f, 4.1f, iz / 5f)));
            terrainTop = Math.Max(terrainTop, AgentBAct1HeightField.CollisionGround(sample.X, sample.Z));
        }
        shed.GlobalPosition = new(shed.GlobalPosition.X, terrainTop + .045f, shed.GlobalPosition.Z);
        shed.SetMeta("groundContactPolicy", "level floor above sampled terrain; scribed stone plinth and three sloped approaches");
        var meshes = FindDescendants<MeshInstance3D>(shed).Where(mesh => mesh.Mesh is not null).ToArray();
        if (meshes.Length != 58)
            throw new InvalidOperationException($"HeroYardShed source contract expects 58 meshes, found {meshes.Length}.");
        foreach (var mesh in meshes)
        {
            var name = mesh.Name.ToString();
            if (name == "HeroYardShed_LadderApron_LOD0") mesh.SetMeta("requiresTerrainSupport", true);
            var wood = !name.Contains("Foundation", StringComparison.Ordinal)
                && !name.Contains("LadderApron", StringComparison.Ordinal)
                && !name.Contains("Hay", StringComparison.Ordinal);
            AddHeroYardSourceContact(shed, mesh, wood);
        }
        RegradeAct1DaylightKitMaterials(shed);
        AddHeroYardGroundApproach(shed, "LadderGroundApproach", 0, 4.06f, 5.08f, 1.02f);
        AddHeroYardGroundApproach(shed, "UnderdeckFrontApproach", -1.15f, 2.16f, 3.14f, .90f);
        AddHeroYardGroundApproach(shed, "UnderdeckRearApproach", -1.15f, -2.16f, -3.14f, .90f);

        _underdeckRepairRoot = new Node3D
        {
            Name = "UnderdeckRepairRoot", Position = new(-1.51f, .04f, -1.37f)
        };
        _underdeckRepairRoot.SetMeta("runtimeStateOwner", "yard/loose-footboard");
        _underdeckRepairRoot.SetMeta("platformFloorY", .04f);
        shed.AddChild(_underdeckRepairRoot);

        var ladder = new LadderTraversal3D
        {
            Name = "FixedLoftLadder",
            LowerLanding = new(0, .04f, 3.92f), LowerGrip = new(0, .04f, 3.75f),
            UpperGrip = new(0, 1.50f, 2.65f), UpperLanding = new(0, 1.50f, 1.45f),
            EntryAllowed = () => ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night"
        };
        shed.AddChild(ladder);
        shed.SetMeta("lowerLanding", ladder.ToGlobal(ladder.LowerLanding));
        shed.SetMeta("upperLanding", ladder.ToGlobal(ladder.UpperLanding));
        shed.SetMeta("underdeckFrontAccess", shed.ToGlobal(new(-1.15f, .08f, 2.80f)));
        shed.SetMeta("underdeckRearAccess", shed.ToGlobal(new(-1.15f, .08f, -2.80f)));
        shed.SetMeta("underdeckClearHeight", 1.22f);
        shed.SetMeta("loftFloorHeight", 1.46f);

        var roofRepair = BuildLoftRoofRepair(shed);
        var roofline = DiscoveryTarget(village, "shed-loft-roofline", new(.36f, .55f, .10f),
            village.ToLocal(roofRepair.GlobalPosition - roofRepair.GlobalBasis.Z * .045f), journal: true);
        roofline.GlobalBasis = roofRepair.GlobalBasis;
        roofline.SetMeta("activePropPath", roofRepair.GetPath().ToString());
        roofline.SetMeta("physicalAction", "climb the fixed ladder, inspect the bolted roof repair and look across the village from the upper landing");
        roofline.SetMeta("accessAnchor", shed.ToGlobal(new(0, 1.50f, 1.40f)));
        roofline.SetMeta("plotGate", false);
        var rattle = DiscoveryTarget(village, "underdeck-rattle", new(.34f, .20f, .26f),
            village.ToLocal(shed.ToGlobal(new(-1.15f, .32f, -.75f))), journal: true);
        rattle.SetMeta("activePropPath", _underdeckRepairRoot.GetPath().ToString());
        rattle.SetMeta("physicalAction", "crouch under the supported loft and trace the loose board; repair uses the existing yard mechanism");
        rattle.SetMeta("requiresCrouch", true);
        rattle.SetMeta("plotGate", false);
    }

    private static MeshInstance3D BuildLoftRoofRepair(Node3D shed)
    {
        var repair = new Node3D { Name = "LoftRoofRepair" };
        repair.SetMeta("presentationRole", "supported roof rafters with a visible local timber and bolted steel repair");
        shed.AddChild(repair);
        MeshInstance3D Box(string name, Vector3 size, Transform3D transform, string color, bool wood)
        {
            var mesh = new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size }, Transform = transform,
                MaterialOverride = PainterlyMaterialLibrary.ForColor(color, wood ? "wood" : "iron", sheltered: true) };
            repair.AddChild(mesh);
            AddHeroYardSourceContact(shed, mesh, wood);
            return mesh;
        }
        var peak = new Vector3(0, 4.16f, 2.04f);
        Basis rightBasis = Basis.Identity;
        var rightFoot = new Vector3(2.08f, 3.335f, 2.04f);
        foreach (var side in new[] { -1, 1 })
        {
            var foot = new Vector3(side * 2.08f, 3.335f, 2.04f);
            var delta = peak - foot;
            var basis = new Basis(new Quaternion(Vector3.Up, delta.Normalized()));
            var rafter = Box(side < 0 ? "LeftRafter" : "RightRafter", new(.20f, delta.Length(), .14f),
                new(basis, (foot + peak) * .5f), "685744", true);
            rafter.SetMeta("bearingPolicy", "lower end overlaps the existing front support post; upper edge follows the actual roof underside");
            if (side > 0) rightBasis = basis;
        }
        // A fresh timber scab sits on the old rafter's inner face. Steel crosses
        // the scab, and four visible bolt heads enter it. Every layer overlaps
        // its real support; the clue no longer points at an empty rear window.
        var patchAt = rightFoot.Lerp(peak, .32f);
        patchAt.Z = 1.962f;
        Box("FreshTimberScab", new(.24f, .60f, .024f), new(rightBasis, patchAt), "a18860", true);
        var plateAt = patchAt; plateAt.Z = 1.942f;
        var plate = Box("BoltedSteelSplice", new(.28f, .46f, .022f), new(rightBasis, plateAt), "626461", false);
        plate.SetMeta("physicalObservation", "four bolts through a steel splice over fresh timber on the supported roof rafter");
        foreach (var x in new[] { -.085f, .085f })
        foreach (var y in new[] { -.145f, .145f })
        {
            var bolt = new MeshInstance3D { Name = $"Bolt_{(x < 0 ? "L" : "R")}{(y < 0 ? "Lower" : "Upper")}",
                Mesh = new CylinderMesh { TopRadius = .018f, BottomRadius = .018f, Height = .026f, RadialSegments = 6, Rings = 1 },
                Transform = new(rightBasis * new Basis(Vector3.Right, Mathf.Pi * .5f), plateAt + rightBasis * new Vector3(x, y, -.019f)),
                MaterialOverride = PainterlyMaterialLibrary.ForColor("777b78", "iron", sheltered: true) };
            repair.AddChild(bolt);
            bolt.SetMeta("supportOwner", plate.GetPath().ToString());
        }
        repair.SetMeta("observationAnchor", shed.ToGlobal(new(0, 1.50f, 1.40f)));
        repair.SetMeta("villageViewAim", shed.ToGlobal(new(0, 3.22f, 8.0f)));
        return plate;
    }

    private static void AddHeroYardSourceContact(Node3D shed, MeshInstance3D mesh, bool wood)
    {
        var body = new StaticBody3D
        {
            Name = "HeroYardShedContact_" + mesh.Name, CollisionLayer = 1, CollisionMask = 1
        };
        body.SetMeta("collisionOwner", "act1-exterior-architecture");
        body.SetMeta("authoredSourceMesh", mesh.GetPath().ToString());
        if (wood) body.SetMeta("footstepSurface", "wood");
        shed.AddChild(body);
        body.AddChild(AuthoredSurfaceContact(shed, mesh));
    }

    private static void AddHeroYardGroundApproach(Node3D shed, string name, float x,
        float innerZ, float outerZ, float width)
    {
        var points = new Vector3[8];
        for (var side = 0; side < 2; side++)
        {
            var lateral = x + (side == 0 ? -1 : 1) * width * .5f;
            points[side] = new(lateral, .04f, innerZ);
            var outer = shed.ToGlobal(new(lateral, 0, outerZ));
            outer.Y = AgentBAct1HeightField.CollisionGround(outer.X, outer.Z) + .012f;
            points[3 - side] = shed.ToLocal(outer);
        }
        for (var index = 0; index < 4; index++) points[index + 4] = points[index] - Vector3.Up * .065f;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var triangles = new[] { 0,1,2, 0,2,3, 4,6,5, 4,7,6,
            0,4,5, 0,5,1, 1,5,6, 1,6,2, 2,6,7, 2,7,3, 3,7,4, 3,4,0 };
        for (var index = 0; index < triangles.Length; index += 3)
        {
            surface.AddVertex(points[triangles[index]]);
            surface.AddVertex(points[triangles[index + (outerZ > innerZ ? 1 : 2)]]);
            surface.AddVertex(points[triangles[index + (outerZ > innerZ ? 2 : 1)]]);
        }
        surface.GenerateNormals();
        var mesh = new MeshInstance3D
        {
            Name = name, Mesh = surface.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("705b45", "wood_furniture")
        };
        mesh.SetMeta("terrainSupportPolicy", "board ends embedded in terrain; upper edge seated on the stone platform");
        shed.AddChild(mesh);
        AddHeroYardSourceContact(shed, mesh, true);
    }

    internal bool CanUseSmallSpaceInteraction(string interactionId)
    {
        if (interactionId == "urman.chapter1:interaction/discover-fap-service-cabinet")
            return GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController cabinetPlayer
                && CanUseFapServiceCabinet(cabinetPlayer);
        var loft = interactionId == "urman.chapter1:interaction/discover-shed-loft-roofline";
        var underdeck = interactionId is "urman.chapter1:interaction/discover-underdeck-rattle"
            or "urman.chapter1:interaction/mark-underdeck-quiet";
        if (!loft && !underdeck) return true;
        if (_heroYardShed is null || ActiveZoneId is not ("village_day" or "zirat_road" or "kara_urman_night")) return false;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null) return false;
        var local = _heroYardShed.ToLocal(player.GlobalPosition);
        if (loft) return local.Y >= 1.38f && local.Y <= 1.70f && Mathf.Abs(local.X) <= .70f
            && local.Z >= .65f && local.Z <= 1.72f;
        return player.IsCrouching && local.Y >= -.06f && local.Y <= .20f
            && Mathf.Abs(local.X) < 1.92f && Mathf.Abs(local.Z) < 1.88f
            && new Vector2(local.X + 1.15f, local.Z + .75f).Length() <= 1.35f;
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
        _ziratOuterCulvertCollision.SetMeta("footstepSurface", "wood");
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

        // The journal mentions a fresh shovel scrape beyond the crossing.
        // Keep its cut and pushed snow on the outer bank, away from the graves.
        AddVisualLandformSurface(zirat, "CulvertFreshShovelScrape", .48f, .012f, 1.05f,
            new(6.02f, 0f, culvertZ - .08f), "bacbd4", "snow_trampled", 78f, true);
        AddVisualLandformSurface(zirat, "CulvertPushedSnowLip", .28f, .10f, .60f,
            new(6.57f, .025f, culvertZ - .18f), "e3ebef", "snow_ground", -12f, true);

        var target = DiscoveryTarget(
            village,
            ZiratOuterCulvertSlug,
            new(2.65f, 1.05f, 1.08f),
            village.ToLocal(bridge.GlobalPosition + Vector3.Up * .58f),
            journal: false);
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
                && (mesh.Name.ToString().StartsWith("HeroHouse_Corner_", StringComparison.Ordinal)
                    || mesh.Name.ToString().StartsWith("DwellingFacade_Corner_", StringComparison.Ordinal))
                && facade.ToLocal(mesh.GlobalTransform * mesh.Mesh.GetAabb().GetCenter()).Z < -4f)
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
        var houseCenter = facade.ToGlobal(new(StyleBenchmarkInteriorFactory.RoomOffset.X, 0,
            StyleBenchmarkInteriorFactory.RoomOffset.Z));
        var awayFromHouse = HorizontalDirection(cornerBounds.GetCenter() - houseCenter);
        var accessAnchor = cornerBounds.GetCenter() + awayFromHouse * 1.60f + Vector3.Left * 2.60f;
        accessAnchor.Y = AgentBAct1HeightField.CollisionGround(accessAnchor.X, accessAnchor.Z);

        // Let the player discover the landmark while looking up toward it,
        // rather than searching the snow for an invisible selection box.
        var eye = accessAnchor + Vector3.Up * 1.7f;
        var sightline = (minaret.GlobalPosition + Vector3.Up * 13f - eye).Normalized();
        var target = DiscoveryTarget(
            village,
            HouseExteriorViewSlug,
            new(.9f, .9f, .9f),
            village.ToLocal(eye + sightline * 1.6f),
            journal: false);
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
