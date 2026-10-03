using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal sealed class PublicBuildingRoom
    {
        internal required string Id;
        internal required string AddressId;
        internal required Node3D Building;
        internal required Node3D Metric;
        internal required Node3D Room;
        internal required Vector2 HalfSize;
        internal required float Ceiling;
        internal required Aabb SeniVolume;
        internal required Vector3 Entrance;
        internal required Vector3 Outside;
        internal required Vector3 Vestibule;
        internal required Vector3 Inside;
        internal required InteractionTarget OuterDoor;
        internal required InteractionTarget InnerDoor;
    }

    private sealed record PublicUse(string RoomId, InteractionTarget Target, Node3D? SurfaceOwner, bool Outside = false);
    private readonly List<PublicBuildingRoom> _publicBuildings = new();
    private readonly Dictionary<string, PublicUse> _publicUses = new(StringComparer.Ordinal);
    private readonly List<(Label3D Label, string Id, bool DocumentTitle)> _publicText = new();
    private Node3D? _publicPlanSurface;
    internal IReadOnlyList<PublicBuildingRoom> PublicBuildingRooms => _publicBuildings;

    /// <summary>Existing parcels, source geometry and address IDs remain the owners.
    /// Called before importing addresses and collecting interaction bindings.</summary>
    private void BuildPublicBuildings()
    {
        if (_publicBuildings.Count != 0) throw new InvalidOperationException("Public buildings are already assembled.");
        var shop = PreparePublicBuilding("shop", "WestReturnMidFacade", "H020", "tukay", "14", "URM-Q01-P0119", "village-shop-sign");
        BuildVillageShop(shop);
        SquareWeatherShelter(shop.Room, new(0, shop.Ceiling * .5f, 0), new(shop.HalfSize.X, shop.Ceiling * .5f, shop.HalfSize.Y));
        BuildShopSign(FindDescendants<Node3D>(this).Single(node => node.Name == "WestReturnMidFacade"));
        BuildShopStorefront(shop);
        var school = PreparePublicBuilding("school", "EastReturnMidFacade", "H031", "urman", "3", "URM-Q02-P0130", "school-building-sign");
        BuildClosedSchool(school);
        SquareWeatherShelter(school.Room, new(0, school.Ceiling * .5f, 0), new(school.HalfSize.X, school.Ceiling * .5f, school.HalfSize.Y));
        ExcludeSchoolFloorBank(school);
        var council = PreparePublicBuilding("council", "EastStreetHorizonFacade", "H040", "bakcha", "9", "URM-Q01-P0139", "council-building-sign");
        BuildCouncilClub(council);
        SquareWeatherShelter(council.Room, new(0, council.Ceiling * .5f, 0), new(council.HalfSize.X, council.Ceiling * .5f, council.HalfSize.Y));
        RepairCouncilFenceJunction(council);
        foreach (var room in _publicBuildings) ShelterPublicMaterials(room.Metric);
        EnsureFacilityTick();
    }

    private void ExcludeSchoolFloorBank(PublicBuildingRoom school)
    {
        // Public08: this exact presentation-only bank rises 55–93 mm above
        // the school's timber floor. Preserve the bank outside the occupied
        // shell and its original material; neither terrain nor floor collision
        // needs moving. The shared clipper already preserves exterior faces.
        if (school.Id != "school" || school.Building.Name != "EastReturnMidFacade")
            throw new InvalidOperationException("The field-bank repair belongs only to the existing school.");
        var bank = GetNode<MeshInstance3D>("Act1CoreWorldGreybox/AgentBExteriorWorld/AgentB_TerrainRoadKit/URMAN_AgentB_TerrainRoadKit/Ditch_FieldBank_EastZirat");
        if (bank.Mesh is not ArrayMesh original || original.GetSurfaceCount() != 1
            || original.SurfaceGetMaterial(0)?.ResourceName != "AB_earth")
            throw new InvalidOperationException("The school field bank must retain its single authored AB_earth surface.");
        // The imported wall is 20 cm thick before the existing display scale.
        // End the cut halfway through it, so the retained bank cannot protrude
        // into the room or leave an exposed gap beyond the outside wall.
        var halfSize = school.HalfSize + Vector2.One * (.10f * school.Building.GlobalBasis.Scale.X);
        var (result, changed, vertices) = AgentBAct1ExteriorLayer.ClipGroundFootprint(bank, original, school.Room, halfSize);
        if (changed == 0)
        {
            // Relayout v3 moved the school to the FAP street; the bank no longer reaches its floor.
            result.Dispose();
            school.Room.SetMeta("exteriorBankFootprintExcluded", false);
            GD.Print("act1-school-bank-cut: the field bank does not meet the school footprint (relayout v3); no cut");
            return;
        }
        bank.SetMeta("occupiedRoomOriginalMesh", original);
        bank.Mesh = result;
        bank.SetMeta("occupiedRoomTerrainCut", school.Room.GetPath().ToString());
        bank.SetMeta("occupiedRoomTerrainHalfSize", halfSize);
        bank.SetMeta("supportOwner", "AgentB_TerrainCollision");
        school.Room.SetMeta("exteriorBankFootprintExcluded", true);
        school.Room.SetMeta("exteriorBankCutHalfSize", halfSize);
        school.Room.SetMeta("exteriorBankCutOwner", bank.GetPath().ToString());
        GD.Print($"act1-school-bank-cut: mesh={bank.GetPath()} room={school.Room.GetPath()} half={halfSize} affectedTriangles={changed} clippedVertices={vertices} publishedVertices={result.GetFaces().Length} floorY={school.Room.GlobalPosition.Y} collision=unchanged-existing-terrain-and-timber-floor");
    }

    private PublicBuildingRoom PreparePublicBuilding(string id, string sourceName, string suffix,
        string street, string number, string cadastral, string titleId)
    {
        var building = FindDescendants<Node3D>(this).Single(node => node.Name == sourceName);
        MeshInstance3D Wall(string suffixName) => FindDescendants<MeshInstance3D>(building)
            .Single(mesh => mesh.Name.ToString().EndsWith(suffixName + "_Wall_LOD0", StringComparison.Ordinal));
        var streetWall = Wall("_Street");
        var rearWall = Wall("_Rear");
        var rightWall = Wall("_Right");
        var entryWall = Wall("_SeniEntry");
        var streetBounds = PublicBuildingShell.Bounds(building, streetWall);
        var rearBounds = PublicBuildingShell.Bounds(building, rearWall);
        var entryBounds = PublicBuildingShell.Bounds(building, entryWall);
        var seniOuterBounds = PublicBuildingShell.Bounds(building, Wall("_SeniOuter"));
        var mainOuterX = PublicBuildingShell.Bounds(building, rightWall).End.X;
        var scale = building.GlobalBasis.Scale.X;
        if (Math.Abs(scale - building.GlobalBasis.Scale.Y) > .001f || Math.Abs(scale - building.GlobalBasis.Scale.Z) > .001f)
            throw new InvalidOperationException("Existing public-building footprint must keep its uniform authored scale.");
        var half = streetBounds.Size.X * .5f;
        var front = streetBounds.End.Z;
        var back = rearBounds.Position.Z;
        var midpoint = (front + back) * .5f;
        var depth = front - back;
        var entryX = entryBounds.GetCenter().X;
        var entryZ = entryBounds.End.Z;
        const float mainFloorSource = .365f;
        const float seniFloorSource = .31f;
        const float doorHalfSource = .54f;
        const float outerHeadSource = 2.46f;
        const float innerHeadSource = 2.50f;
        var innerZ = back + 1.27f;

        // Only these three existing annexes gain a slightly taller outer aperture
        // and a connecting doorway through the actual main wall. Other instances
        // continue using the imported shared mesh, unchanged.
        PublicBuildingShell.OpenDoor(building, entryWall, true,
            entryX - doorHalfSource, entryX + doorHalfSource, .299f, outerHeadSource);
        PublicBuildingShell.OpenDoor(building, rightWall, false,
            innerZ - .58f, innerZ + .58f, .299f, innerHeadSource);
        foreach (var mesh in FindDescendants<MeshInstance3D>(building).Where(mesh =>
            mesh.Name.ToString().Contains("_SeniEntry_Door0_", StringComparison.Ordinal)
            || mesh.Name.ToString().EndsWith("_SeniStep_LOD0", StringComparison.Ordinal)).ToArray())
        {
            mesh.SetMeta("retirementReason", "this existing public entrance now has an explicit full-height hinged leaf and matching threshold");
            HidePresentationNode(mesh);
        }

        // Furniture, people, capsules and doors are authored in physical metres.
        // This child cancels only the inherited display scale; the house is not moved.
        var metric = new Node3D { Name = "PublicInterior_" + id, Scale = Vector3.One / scale };
        building.AddChild(metric);
        var room = new Node3D { Name = "MainRoom", Position = new(0, mainFloorSource * scale, midpoint * scale) };
        metric.AddChild(room);
        var halfSize = new Vector2((half - .205f) * scale, (depth * .5f - .205f) * scale);
        var ceiling = (streetBounds.End.Y - mainFloorSource) * scale - .035f;
        var timber = PublicSolid(room, "TimberFloor", new(halfSize.X * 2, .06f, halfSize.Y * 2), new(0, -.03f, 0), "948064", "wood_furniture");
        if (id is "school" or "council")
        {
            timber.Visible = false;
            CivicSurfaceLibrary.Floor(room,"AnnexHerringbone",halfSize*2,new(0,.001f,0));
            room.GetNode<StaticBody3D>("TimberFloorBody").SetMeta("footstepSurface","herringbone_parquet");
        }
        PublicSolid(room, "Ceiling", new(halfSize.X * 2, .055f, halfSize.Y * 2), new(0, ceiling + .0275f, 0), "c6c0aa", "plaster");
        for (var i = 1; id == "shop" && i < 10; i++)
            PublicBox(room, "FloorboardJoint" + i, new(.009f, .002f, halfSize.Y * 2 - .03f),
                new(-halfSize.X + i * halfSize.X * .2f, .0015f, 0), "685845", "wood_furniture");
        // A thin interior finish shares the real wall plane; windows retain their
        // existing recesses, glass and mullions rather than becoming painted openings.
        FacilityLamp(room, id + "MainLamp", new(0, ceiling - .10f, 0), "ffdfa9", id == "school" ? .78f : 1.0f, 5.5f);
        FacilityRadiator(room, id + "Radiator", new(-halfSize.X + .55f, .48f, halfSize.Y - .12f), 180);
        // The main wall's thickness is inside the house; subtracting it again
        // from the seni left an unsupported strip at the connecting threshold.
        var seniWidth = (seniOuterBounds.Position.X - mainOuterX) * scale;
        var seniDepth = (entryZ - back - .205f) * scale;
        var seniCenter = new Vector3((mainOuterX + seniOuterBounds.Position.X) * .5f * scale,
            seniFloorSource * scale, (back + .10f + entryZ) * .5f * scale);
        PublicSolid(metric, "SeniFloor", new(seniWidth, .045f, seniDepth), seniCenter - Vector3.Up * .0225f, "8f7453", "wood_furniture");
        if (id is "school" or "council")
        {
            metric.GetNode<MeshInstance3D>("SeniFloor").Visible = false;
            CivicSurfaceLibrary.Floor(metric,"SeniHerringbone",new(seniWidth,seniDepth),seniCenter+Vector3.Up*.001f);
            metric.GetNode<StaticBody3D>("SeniFloorBody").SetMeta("footstepSurface","herringbone_parquet");
        }
        FacilityLamp(metric, id + "SeniLamp", new(entryX * scale, 2.34f * scale, (entryZ - .65f) * scale), "efcd91", .46f, 2.6f);
        var threshold = new Vector3(entryX * scale, seniFloorSource * scale, entryZ * scale);
        var approachDistance = BuildPublicEntrySteps(metric, id, threshold);
        PublicSolid(metric, "MainThreshold", new(.28f * scale, mainFloorSource * scale, 1.17f * scale),
            new((half - .075f) * scale, mainFloorSource * scale * .5f, innerZ * scale), "88735a", "wood_furniture");

        // The pivot belongs on the opening side of the frame. A pivot inside
        // the reveal lets the 65mm leaf cut across its hinge-side jamb at 70°
        // (Public02 observed the first contact at sample19, yaw69.49°).
        // Keep the full leaf and 2mm collision margin; allow 6mm beyond the
        // frame's 35mm half-depth and the leaf's 32.5mm half-thickness.
        const float frameToHinge = .035f + .065f * .5f + .006f;
        var outerDoor = FacilityManualDoor(metric, id + "Entrance", id + "/entrance",
            new((entryX - doorHalfSource) * scale + .013f, seniFloorSource * scale, entryZ * scale + .019f + frameToHinge),
            2 * doorHalfSource * scale - .026f, (outerHeadSource - seniFloorSource) * scale - .018f, 90, -95);
        var innerDoor = FacilityManualDoor(metric, id + "InnerDoor", id + "/inner-door",
            new((half - .205f) * scale - frameToHinge, mainFloorSource * scale, (innerZ - .58f) * scale + .014f),
            1.16f * scale - .028f, (innerHeadSource - mainFloorSource) * scale - .018f, 0, -95);
        RegisterPublicUse("", outerDoor.Target, outerDoor.Hinge, outside: true);
        RegisterPublicUse("", innerDoor.Target, innerDoor.Hinge, outside: true);
        PublicDoorFrame(metric, id + "OuterFrame",
            new(entryX * scale, seniFloorSource * scale, entryZ * scale + .019f),
            2 * doorHalfSource * scale, (outerHeadSource - seniFloorSource) * scale, 0);
        PublicDoorFrame(metric, id + "InnerFrame",
            new((half - .205f) * scale, mainFloorSource * scale, innerZ * scale),
            1.16f * scale, (innerHeadSource - mainFloorSource) * scale, -90);

        var entrance = metric.ToGlobal(threshold);
        var outward = metric.GlobalBasis.Z.Normalized();
        var access = entrance + outward * approachDistance;
        access.Y = AgentBAct1HeightField.CollisionGround(access.X, access.Z) + .035f;
        // A 1.18m plate does not fit the two narrow seni jambs. It is screwed to
        // the existing boarded street gable, where it can be read from the road.
        var sign = metric.ToGlobal(new(0, (streetBounds.End.Y + .38f) * scale, front * scale + .035f));
        RegisterAddressedBuilding(new(building, "act1/holding/" + sourceName, "BLD-" + suffix,
            "PAR-" + suffix, "ADR-" + suffix, street, number, cadastral, access, sign, outward, id));
        var fascia = new Node3D { Name = id + "BuildingSign", Position = new(0, (streetBounds.End.Y - .18f) * scale, front * scale + .026f) };
        metric.AddChild(fascia);
        // The council-club board is 1.48 m so the painted 2000s plate that
        // replaces its label keeps the generator's 6.5:1 letterforms instead of
        // being stretched along a longer board.
        PublicBox(fascia, "PaintedBoard", new(id == "council" ? 1.48f : 1.7f, .225f, .032f), Vector3.Zero, "35514d", "wood_furniture");
        PublicText(fascia, "Institution", "urman.chapter1:text/" + titleId, new(0, 0, .022f), .0022f, false);
        building.SetMeta("visualOnly", false);
        building.SetMeta("publicBuilding", id);
        building.SetMeta("collisionOwner", "existing AuthoredKitCollision and Facility contacts");
        building.SetMeta("runtimeStateOwner", "RuntimeBridge/world.props");
        building.SetMeta("entryRepairPolicy", "instance-local cut in existing outer and main walls; original parcel, roof, frontage and IDs retained");
        var result = new PublicBuildingRoom
        {
            Id = id, AddressId = "ADR-" + suffix, Building = building, Metric = metric, Room = room,
            HalfSize = halfSize, Ceiling = ceiling,
            SeniVolume = new Aabb(new((mainOuterX + .01f) * scale, .23f * scale, (back + .19f) * scale),
                new(seniWidth - .02f * scale, 2.25f * scale, (entryZ - back - .15f) * scale)),
            Entrance = entrance, Outside = access,
            Vestibule = metric.ToGlobal(new(entryX * scale, seniFloorSource * scale, (entryZ - .68f) * scale)),
            Inside = room.ToGlobal(new(halfSize.X - .47f, 0, (innerZ - midpoint) * scale)),
            OuterDoor = outerDoor.Target, InnerDoor = innerDoor.Target
        };
        _publicBuildings.Add(result);
        return result;
    }

    private float BuildPublicEntrySteps(Node3D parent, string name, Vector3 threshold)
    {
        var outside = parent.ToGlobal(threshold + Vector3.Back * .70f);
        var ground = AgentBAct1HeightField.CollisionGround(outside.X, outside.Z);
        var bottom = parent.ToLocal(new(outside.X, ground, outside.Z)).Y;
        var rise = threshold.Y - bottom;
        if (rise < -.02f || rise > .85f)
            throw new InvalidOperationException("Public entrance needs a measured terrain repair: " + name + " rise " + rise);
        var steps = Math.Max(1, Mathf.CeilToInt(Math.Max(rise, .03f) / .15f));
        for (var i = 0; i < steps; i++)
        {
            var top = bottom + rise * (i + 1) / steps;
            var depth = .34f;
            var z = threshold.Z + .045f + (steps - i - 1) * .32f;
            var baseY = Math.Min(bottom - .035f, top - .025f);
            PublicSolid(parent, name + "EntryTread" + i, new(1.13f, top - baseY, depth),
                new(threshold.X, (top + baseY) * .5f, z), "817a68", "stone_foundation");
            PublicBox(parent, name + "TreadNosing" + i, new(1.10f, .022f, .055f),
                new(threshold.X, top - .011f, z + depth * .5f - .025f), "696557", "stone_foundation");
        }
        // Address04 measured a standing capsule touching the first tread at
        // school/council: their three treads extend farther than the shop's two.
        // Put the approach beyond the actual leading face plus the full player
        // radius and 5 cm clearance. The steps and door geometry stay unchanged.
        return Math.Max(.93f, .045f + (steps - 1) * .32f + .17f + .35f + .05f);
    }

    private void BuildVillageShop(PublicBuildingRoom building)
    {
        var room = building.Room;
        var h = building.HalfSize;
        var counter = new Node3D { Name = "RazilyaCounter", Position = new(0, 0, .47f) };
        room.AddChild(counter);
        PublicSolid(counter, "CounterFront", new(2.48f, .84f, .085f), new(0, .42f, -.235f), "81724f", "wood_furniture");
        foreach (var x in new[] { -1.20f, 1.20f })
            PublicSolid(counter, "CounterSide" + x, new(.08f, .84f, .51f), new(x, .42f, 0), "81724f", "wood_furniture");
        PublicSolid(counter, "WornLaminateTop", new(2.58f, .055f, .63f), new(0, .87f, 0), "b4ae93", "wood_furniture");
        for (var i = 0; i < 6; i++)
            PublicBox(counter, "VerticalCounterJoint" + i, new(.015f, .76f, .008f),
                new(-1.02f + i * .4f, .43f, -.283f), "62573f", "wood_furniture");
        var buy = FacilityTarget("VillageShopCounter", "urman.chapter1:local/shop-counter", "Попросить нужные вещи",
            counter, new(.84f, 1.01f, -.12f), new(.61f, .25f, .36f));
        buy.AddToGroup("village_shop_counter");
        RegisterPublicUse("shop", buy, counter);
        buy.PresentationRepeatAvailable = () => FacilityExteriorActive && _runtimeBridge?.SessionIdentity is not null;
        buy.PresentationRepeat = () =>
        {
            if (_runtimeBridge is { } bridge && CanUsePublicBuildingInteraction(buy.InteractionId))
                ShopUi.OpenFor(this, bridge);
        };
        PublicDocument(counter, "shop-account-book", new(-.57f, .903f, -.12f), Vector3.Zero, new(.42f, .31f), flat: true);
        PublicGoods(counter, new(.85f, .91f, .05f), compact: true);
        PublicShelf(room, "ShopRearShelves", new(0, 0, h.Y - .27f), 2.95f, 1.83f, .39f, goods: true);
        BuildPublicFridge(room, new(-h.X + .44f, 0, .67f));
        FacilityTable(room, "EmptyCrateStand", new(h.X - .50f, 0, h.Y - .52f), new(.74f, .61f, .65f));
        PublicBox(room, "FoldedShoppingBags", new(.42f, .09f, .36f), new(h.X - .50f, .69f, h.Y - .52f), "9f9679", "paper");
        var seller = GeneratedCharacterKitDressing.Attach(room, "razilya", "Naila", new(0, 0, 1.47f), sheltered: true);
        seller.Name = "Npc_razilya";
        seller.RotationDegrees = new(0, 180, 0);
        foreach (var mesh in FindDescendants<MeshInstance3D>(seller).Where(mesh => mesh.Visible))
        {
            var name = mesh.Name.ToString();
            if (name.Contains("_Body_", StringComparison.Ordinal) || name.Contains("_Sleeve", StringComparison.Ordinal)
                || name.Contains("_Coat", StringComparison.Ordinal))
                mesh.MaterialOverride = PainterlyMaterialLibrary.ForColor("4f615e", "cloth", sheltered: true);
            if (name.Contains("Apron", StringComparison.Ordinal))
                mesh.MaterialOverride = PainterlyMaterialLibrary.ForColor("b8a78a", "cloth", sheltered: true);
        }
        GeneratedCharacterKitDressing.GroundSolesOnAnchor(seller);
        var actorContact = new StaticBody3D { Name = "RazilyaContact", Position = new(0, .82f, 1.47f), CollisionLayer = 2, CollisionMask = 0 };
        actorContact.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = .24f, Height = 1.64f } });
        room.AddChild(actorContact); _facilityBodies.Add(actorContact);
        var greeting = FacilityTarget("VillageShopGreeting", "urman.chapter1:interaction/village-shop-greeting", "Поговорить с Разилей",
            room, new(0, 1.32f, 1.31f), new(.62f, .62f, .39f));
        greeting.DialogueId = "urman.chapter1:dialogue/village_shop";
        RegisterPublicUse("shop", greeting, actorContact);
        PublicBox(room, "ShopEntranceMat", new(1.04f, .012f, .75f), new(h.X - .70f, .008f, -h.Y + 1.14f), "626350", "fabric");
        PublicNotice(room, "ShopNoticeBoard", new(-h.X + .045f, 1.55f, -1.18f), 90,
            "Магазин\nЕсли закрыто — постучите.\nОбъявления оставляют у Разили.", .78f);
    }

    private void BuildClosedSchool(PublicBuildingRoom building)
    {
        var room = building.Room;
        var h = building.HalfSize;
        const float partitionX = .73f;
        const float divideZ = -.15f;
        const float portalHeight = 2.02f;
        PublicSolid(room, "ClassOfficeDivider", new(h.X + partitionX, building.Ceiling, .12f),
            new((-h.X + partitionX) * .5f, building.Ceiling * .5f, divideZ), "b8b496", "plaster");
        // Corridor and two rooms fit the actual small existing shell.
        foreach (var (near, far) in new[] { (-h.Y, -1.80f), (-.72f, .22f), (1.30f, h.Y) })
            if (far > near)
                PublicSolid(room, "CorridorPier" + near, new(.12f, building.Ceiling, far - near),
                    new(partitionX, building.Ceiling * .5f, (near + far) * .5f), "aab095", "plaster");
        foreach (var center in new[] { -1.26f, .76f })
        {
            PublicSolid(room, "CorridorHeader" + center, new(.12f, building.Ceiling - portalHeight, 1.08f),
                new(partitionX, (building.Ceiling + portalHeight) * .5f, center), "b8b496", "plaster");
            var key = center > 0 ? "school/classroom" : "school/office";
            // Mount the hinge on the room face of the 12 cm partition. In its
            // mid-plane the near corner swept into its own pier at 26 degrees.
            var door = FacilityManualDoor(room, center > 0 ? "ClassroomDoor" : "SchoolOfficeDoor",
                key, new(partitionX - .10f, 0, center - .524f), 1.048f, portalHeight - .02f, 0, -95);
            RegisterPublicUse("school", door.Target, door.Hinge);
            PublicDoorFrame(room, "SchoolDoorFrame" + center, new(partitionX + .075f, 0, center), 1.08f, portalHeight, 90);
        }
        PublicSolid(room, "ChalkboardBacking", new(1.96f, .91f, .055f), new(-.84f, 1.31f, h.Y - .055f), "514c37", "wood_furniture");
        CivicSurfaceLibrary.FramedFace(room,"AnnexChalkboard",new(-.84f,1.31f,h.Y-.103f),180,new(1.84f,.80f),
            CivicSurfaceLibrary.Face("chalkboard_lessons_v1_atlas.png",1,2,0),paper:false);
        PublicSolid(room, "ChalkTray", new(1.95f, .045f, .13f), new(-.84f, .845f, h.Y - .145f), "8c7c61", "wood_furniture");
        for (var i = 0; i < 3; i++)
            PublicBox(room, "Chalk" + i, new(.052f, .012f, .013f), new(-1.12f + i * .10f, .88f, h.Y - .16f), "d5d2bd", "stone");
        for (var row = 0; row < 2; row++)
        {
            var z = .58f + row * .85f;
            FacilityTable(room, "PupilDesk" + row, new(-1.0f, 0, z), new(1.08f, .66f, .49f));
            FacilityBench(room, "PupilBench" + row, new(-1.0f, 0, z - .36f), 1.01f, 0);
            PublicBox(room, "ExerciseBook" + row, new(.20f, .018f, .26f), new(-.86f, .704f, z), "6d9780", "paper");
        }
        FacilityTable(room, "SchoolStaffDesk", new(-1.13f, 0, -1.14f), new(1.35f, .75f, .64f));
        PublicShelf(room, "SchoolArchiveShelves", new(-1.24f, 0, -h.Y + .21f), 1.81f, 1.83f, .31f, goods: false);
        CivicSurfaceLibrary.FramedFace(room,"SchoolDrawingDisplay",new(.35f,1.41f,h.Y-.035f),180,new(.49f,.43f),
            CivicSurfaceLibrary.Face("children_drawings_v1_atlas.png",2,2,0));
        PublicLockedSeniStore(building, "school/store", "Хозяйственная кладовая закрыта.");
        building.Building.SetMeta("schoolClosure", "few pupils; remaining children take school transport to another village; content owns exact notice");
    }

    private void BuildCouncilClub(PublicBuildingRoom building)
    {
        var room = building.Room;
        var h = building.HalfSize;
        const float officeX = -.06f;
        const float officeFront = -.59f;
        PublicSolid(room, "OfficeSidePartition", new(.12f, building.Ceiling, h.Y + officeFront),
            new(officeX, building.Ceiling * .5f, (-h.Y + officeFront) * .5f), "bdb7a2", "plaster");
        foreach (var (left, right) in new[] { (-h.X, -1.62f), (-.54f, officeX) })
            PublicSolid(room, "OfficeFrontPier" + left, new(right - left, building.Ceiling, .12f),
                new((left + right) * .5f, building.Ceiling * .5f, officeFront), "bdb7a2", "plaster");
        PublicSolid(room, "OfficeHeader", new(1.08f, building.Ceiling - 2.0f, .12f),
            new(-1.08f, (building.Ceiling + 2.0f) * .5f, officeFront), "bdb7a2", "plaster");
        // Keep the complete leaf beyond the existing 70 mm casing. Its pivot
        // needs the casing offset, half-depth, half-leaf thickness and 6 mm air.
        var office = FacilityManualDoor(room, "CouncilOfficeDoor", "council/office",
            new(-1.607f, 0, officeFront + .072f + .035f + .0325f + .006f), 1.054f, 1.98f, 90, -95);
        RegisterPublicUse("council", office.Target, office.Hinge);
        PublicDoorFrame(room, "CouncilOfficeFrame", new(-1.08f, 0, officeFront + .072f), 1.08f, 2.0f, 0);
        FacilityTable(room, "CouncilDesk", new(-1.24f, 0, -1.56f), new(1.40f, .74f, .61f));
        PublicDocument(room, "council-photo-album", new(-1.35f, .778f, -1.54f), Vector3.Zero,
            new(.45f, .32f), flat: true, image: "res://assets/images/council-sabantuy-2005.png");
        PublicShelf(room, "CouncilArchive", new(-h.X + .20f, 0, -1.61f), .98f, 1.80f, .31f, goods: false, yaw: 90);
        PublicBox(room, "DocumentStampPad", new(.10f, .035f, .075f), new(-.75f, .785f, -1.54f), "494b44", "metal");
        FacilityRod(room, "DeskPen", new(-.77f, .787f, -1.74f), new(-.69f, .791f, -1.68f), .005f, "38433f");
        var plan = PublicDocument(room, "council-village-plan", new(h.X - .035f, 1.49f, -.78f),
            new(0, -90, 0), new(1.24f, .94f));
        _publicPlanSurface = plan;
        // The full frame and its fixings fit the solid pier between both existing windows.
        PublicDocument(room, "council-sabantuy-poster", new(-h.X + .035f, 1.42f, .19f),
            new(0, 90, 0), new(.65f, .86f));
        PublicSolid(room, "ClubStageDeck", new(2.8f, .16f, .71f), new(-.59f, .08f, h.Y - .40f), "8e7251", "wood_furniture");
        room.GetNode<MeshInstance3D>("ClubStageDeck").Visible = false;
        room.GetNode<StaticBody3D>("ClubStageDeckBody").SetMeta("footstepSurface","herringbone_parquet");
        CivicSurfaceLibrary.Floor(room,"AnnexStageHerringbone",new(2.8f,.71f),new(-.59f,.161f,h.Y-.40f));
        foreach (var z in new[] { .74f, 1.38f })
            // The front bench leaves the full door swing clear; retain its row
            // and width, with the rear bench in its existing position.
            FacilityBench(room, "ClubAudienceBench" + z, new(z < 1f ? -.23f : -.53f, 0, z), 1.72f, 0);
        foreach (var x in new[] { -1.94f, .76f })
        {
            SCurtain(room,"AnnexStageCurtain"+x,new(x,1.10f,h.Y-.13f),.23f,1.90f,RuralPropMaterials.Surface("velvet"));
        }
        FacilityRod(room, "StageCurtainRod", new(-2.03f, 2.04f, h.Y - .12f), new(.89f, 2.04f, h.Y - .12f), .022f, "65615a");
        PublicShelf(room, "ClubStore", new(h.X - .32f, 0, h.Y - .55f), .66f, 1.56f, .41f, goods: false);
        PublicBox(room, "FoldedStageFabric", new(.52f, .17f, .33f), new(h.X - .32f, 1.38f, h.Y - .55f), "998665", "fabric");
        PublicLockedSeniStore(building, "council/store", "Кладовая реквизита закрыта.");
    }

    private void PublicLockedSeniStore(PublicBuildingRoom building, string key, string reason)
    {
        var volume = building.SeniVolume;
        var z = volume.Position.Z + .28f;
        var x = volume.GetCenter().X;
        var floor = .31f * building.Building.GlobalBasis.Scale.X;
        var width = volume.Size.X;
        var closed = PublicSolid(building.Metric, key.Replace('/', '_') + "ClosedDoor", new(width, 1.89f, .065f),
            new(x, floor + .945f, z), "70634d", "wood_furniture");
        var target = FacilityTarget(key.Replace('/', '_') + "LockedUse", "urman.chapter1:local/" + key,
            "Проверить дверь кладовой", building.Metric, new(x, floor + 1.0f, z + .08f), new(width - .08f, 1.6f, .10f));
        RegisterPublicUse(building.Id, target, building.Metric.GetNode<StaticBody3D>(closed.Name + "Body"));
        target.PresentationRepeatAvailable = () => FacilityExteriorActive;
        target.PresentationRepeat = () =>
        {
            if (CanUsePublicBuildingInteraction(target.InteractionId)
                && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            {
                UiFoley.PlayWorld(this, target.GlobalPosition, "door_creak");
                player.NotifyTraversal(reason);
            }
        };
        PublicNotice(building.Metric, "StoreLabel", new(x, floor + 1.53f, z + .045f), 0, "КЛАДОВАЯ\nЗАКРЫТО", .56f);
    }

    private void RegisterPublicUse(string roomId, InteractionTarget target, Node3D? surfaceOwner, bool outside = false)
    {
        _publicUses.Add(target.InteractionId, new(roomId, target, surfaceOwner, outside));
        target.SetMeta("publicBuilding", roomId);
        target.SetMeta("physicalUsePolicy", "explicit player action; reachable physical target and unobstructed sightline");
    }

    internal string PublicInteriorAt(Vector3 point)
    {
        if (!FacilityExteriorActive) return string.Empty;
        if (_squareSchool is { } centralSchool)
        {
            var schoolLocal = centralSchool.ToLocal(point);
            if (Math.Abs(schoolLocal.X) < 9.65f && Math.Abs(schoolLocal.Z) < 5.15f
                && schoolLocal.Y > -.20f && schoolLocal.Y < 3.4f)
                return "school";
        }
        foreach (var room in _publicBuildings)
        {
            var local = room.Room.ToLocal(point);
            if (Math.Abs(local.X) < room.HalfSize.X && Math.Abs(local.Z) < room.HalfSize.Y
                && local.Y > -.20f && local.Y < room.Ceiling)
                return room.Id;
            if (room.SeniVolume.HasPoint(room.Metric.ToLocal(point))) return room.Id;
        }
        return string.Empty;
    }

    internal bool CanUsePublicBuildingInteraction(string id)
    {
        if (!_publicUses.TryGetValue(id, out var use)) return true;
        if (!FacilityExteriorActive || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.VehicleControlled || !IsInstanceValid(use.Target)) return false;
        if (!use.Outside && PublicInteriorAt(player.GlobalPosition) != use.RoomId) return false;
        var camera = GetViewport().GetCamera3D();
        if (camera is null || camera.GlobalPosition.DistanceTo(use.Target.GlobalPosition) > 3.15f) return false;
        using var query = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, use.Target.GlobalPosition, 3);
        var useExclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        using var useExcludeOwner = (global::Godot.Collections.Array)useExclude;
        query.Exclude = useExclude;
        query.HitFromInside = true;
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return true;
        return hit.TryGetValue("collider", out var owner) && owner.AsGodotObject() is Node collider
            && use.SurfaceOwner is { } allowed && (collider == allowed || allowed.IsAncestorOf(collider));
    }

    /// <summary>Called after the existing contact builder and runtime attachment.
    /// Does not delete any ordinary parcel collision or create a save owner.</summary>
    private void FinalizePublicBuildingContacts()
    {
        if (_runtimeBridge is null) throw new InvalidOperationException("Public content must bind after the existing runtime bridge.");
        foreach (var (label, id, documentTitle) in _publicText)
            label.Text = documentTitle ? _runtimeBridge.RequireDocument(id).Title : _runtimeBridge.ResolveText(id);
        foreach (var use in _publicUses.Values)
        {
            if (use.Target.InteractionId.StartsWith("urman.chapter1:interaction/", StringComparison.Ordinal))
                use.Target.Prompt = _runtimeBridge.ResolveText(use.Target.InteractionId.Replace(":interaction/", ":text/"));
        }
        foreach (var room in _publicBuildings)
        {
            var repairedWalls = FindDescendants<MeshInstance3D>(room.Building).Where(mesh => mesh.HasMeta("publicEntranceRepair")).ToArray();
            if (repairedWalls.Length != 2)
                throw new InvalidOperationException("Public entrance lost one of its two real wall repairs: " + room.Id);
            var sources = FindDescendants<CollisionShape3D>(room.Building).Where(shape => shape.HasMeta("authoredSourceMesh"))
                .Select(shape => shape.GetMeta("authoredSourceMesh").AsString()).ToHashSet(StringComparer.Ordinal);
            if (repairedWalls.Any(mesh => !sources.Contains(mesh.GetPath().ToString())))
                throw new InvalidOperationException("Repaired visible wall has no matching authored triangle contact: " + room.Id);
            room.Building.SetMeta("publicContactProjection", "both repaired source walls have existing authored triangle contacts; native traversal pending");
        }
        FinalizeCouncilFenceJunctionContacts();
        BuildPublicVillagePlan();
        BuildCouncilPosterPresentation();
        RefreshFacilityState();
    }

    private MeshInstance3D PublicSolid(Node3D parent, string name, Vector3 size, Vector3 at, string color, string surface)
    {
        var mesh = FacilitySolid(parent, name, size, at, color, surface);
        mesh.MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface, sheltered: true);
        return mesh;
    }

    private static MeshInstance3D PublicBox(Node3D parent, string name, Vector3 size, Vector3 at, string color, string surface)
    {
        var mesh = AddVisualBox(parent, name, size, at, color, surface);
        mesh.MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface, sheltered: true);
        return mesh;
    }

    private void PublicDoorFrame(Node3D parent, string name, Vector3 baseAt, float width, float height, float yaw)
    {
        var frame = new Node3D { Name = name, Position = baseAt, RotationDegrees = new(0, yaw, 0) };
        parent.AddChild(frame);
        foreach (var x in new[] { -width * .5f - .035f, width * .5f + .035f })
            PublicSolid(frame, "Jamb" + x, new(.065f, height + .075f, .07f), new(x, height * .5f, 0), "a18b68", "wood_furniture");
        PublicSolid(frame, "Lintel", new(width + .135f, .067f, .07f), new(0, height + .037f, 0), "a18b68", "wood_furniture");
    }

    private Label3D PublicText(Node3D parent, string name, string id, Vector3 at, float pixelSize, bool documentTitle)
    {
        var label = new Label3D { Name = name, Position = at, FontSize = 40, PixelSize = pixelSize,
            OutlineSize = 0, DoubleSided = false, Shaded = true, NoDepthTest = false,
            Modulate = Color.FromHtml("eee6d0") };
        parent.AddChild(label);
        _publicText.Add((label, id, documentTitle));
        return label;
    }

    private Node3D PublicDocument(Node3D parent, string slug, Vector3 at, Vector3 rotation, Vector2 size,
        bool flat = false, string? image = null)
    {
        var mount = new Node3D { Name = slug.Replace('-', '_') + "_Source", Position = at,
            RotationDegrees = flat ? new(-90, 0, 0) : rotation };
        parent.AddChild(mount);
        PublicBox(mount, "PaperOrFrame", new(size.X + .045f, size.Y + .045f, .025f), Vector3.Zero,
            image is null ? "c5b99b" : "69563e", image is null ? "paper" : "wood_furniture");
        var paper = new MeshInstance3D { Name = "SourceFace", Position = new(0, 0, .016f),
            Mesh = new QuadMesh { Size = size } };
        if (image is not null)
        {
            var texture = ResourceLoader.Load<Texture2D>(image)
                ?? throw new InvalidOperationException("The actual published source photograph is missing: " + image);
            var aspect = texture.GetWidth() / (float)texture.GetHeight();
            ((QuadMesh)paper.Mesh).Size = aspect > size.X / size.Y
                ? new(size.X, size.X / aspect) : new(size.Y * aspect, size.Y);
            paper.MaterialOverride = new StandardMaterial3D { AlbedoTexture = texture, AlbedoColor = Colors.White,
                Roughness = 1, MetallicSpecular = 0, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic };
            paper.SetMeta("assetSource", image);
        }
        else if (slug == "school-transport-notice" || slug == "school-staff-note" || slug == "council-sabantuy-poster")
        {
            var cell = slug == "school-transport-notice" ? 0 : slug == "school-staff-note" ? 1 : 2;
            paper.MaterialOverride = CivicSurfaceLibrary.Face("quest_papers_v2_atlas.png",2,2,cell);
            paper.SetMeta("sourceDocumentId","urman.chapter1:document/"+slug);
        }
        else paper.MaterialOverride = CivicSurfaceLibrary.Face("quest_papers_v2_atlas.png",2,2,3);
        mount.AddChild(paper);
        if (image is null)
        {
            // The readable title uses the content record; ruled lines are merely
            // the physical paper surface. Opening it presents the complete source.
            var title = PublicText(mount, "DocumentTitle", "urman.chapter1:document/" + slug,
                new(0, size.Y * .30f, .020f), Math.Min(.0011f, size.X / 560f), true);
            title.Modulate = Color.FromHtml("3c443c");
            if (slug is "school-transport-notice" or "school-staff-note" or "council-sabantuy-poster") title.Visible = false;
            for (var row = 0; title.Visible && row < 5; row++)
                PublicBox(mount, "WrittenLine" + row, new(size.X * (.75f - row % 2 * .13f), .002f, .001f),
                    new(-size.X * .03f, size.Y * .08f - row * size.Y * .105f, .021f), "8d8977", "paper");
        }
        var target = FacilityTarget(slug.Replace('-', '_') + "_Use", "urman.chapter1:interaction/" + slug,
            "Прочитать", mount, new(0, 0, .052f), new(size.X + .035f, size.Y + .035f, .06f));
        target.DocumentId = "urman.chapter1:document/" + slug;
        var room = slug.StartsWith("shop-", StringComparison.Ordinal) ? "shop"
            : slug.StartsWith("school-", StringComparison.Ordinal) ? "school" : "council";
        RegisterPublicUse(room, target, mount);
        return mount;
    }

    private static void PublicNotice(Node3D parent, string name, Vector3 at, float yaw, string text, float width)
    {
        var board = new Node3D { Name = name, Position = at, RotationDegrees = new(0, yaw, 0) };
        parent.AddChild(board);
        PublicBox(board, "Frame", new(width + .06f, .48f, .027f), Vector3.Zero, "847050", "wood_furniture");
        PublicBox(board, "PinnedPaper", new(width, .43f, .014f), new(0, 0, .019f), "cec5a9", "paper");
        board.AddChild(new Label3D { Name = "Notice", Text = text, Position = new(0, 0, .030f), FontSize = 30,
            PixelSize = .0016f, OutlineSize = 0, Modulate = Color.FromHtml("394139"), Shaded = true, DoubleSided = false });
        foreach (var x in new[] { -width * .44f, width * .44f })
            PublicBox(board, "Pin" + x, new(.011f, .011f, .013f), new(x, .185f, .035f), "525c52", "metal");
    }

    private void PublicShelf(Node3D parent, string name, Vector3 at, float width, float height, float depth, bool goods, float yaw = 0)
    {
        var shelf = new Node3D { Name = name, Position = at, RotationDegrees = new(0, yaw, 0) };
        parent.AddChild(shelf);
        foreach (var x in new[] { -width * .5f + .027f, width * .5f - .027f })
            PublicSolid(shelf, "Upright" + x, new(.055f, height, depth), new(x, height * .5f, 0), "7d7054", "wood_furniture");
        PublicSolid(shelf, "Back", new(width, height, .033f), new(0, height * .5f, depth * .48f), "706249", "wood_furniture");
        for (var level = 0; level < 4; level++)
        {
            var y = .16f + level * (height - .20f) / 3f;
            PublicSolid(shelf, "Shelf" + level, new(width, .045f, depth), new(0, y, 0), "968465", "wood_furniture");
            if (level == 3) continue;
            for (var item = 0; item < Math.Max(2, (int)(width / .25f)); item++)
            {
                var x = -width * .5f + .15f + item * .245f;
                if (goods)
                {
                    var stock = level switch
                    {
                        0 => item < 4 ? 1 : item < 7 ? 2 : 4,
                        1 => item < 4 ? 0 : item < 7 ? 3 : 5,
                        _ => item < 4 ? 6 : item < 8 ? 0 : 3
                    };
                    PublicGoods(shelf, new(x, y + .025f, -.035f), compact: false, stock);
                }
                else
                {
                    var folderHeight = .25f + (item % 3) * .03f;
                    PublicBox(shelf, "ArchiveFolder" + level + "_" + item, new(.075f, folderHeight, depth * .77f),
                        new(x, y + .025f + folderHeight * .5f, -.035f), item % 2 == 0 ? "8e8266" : "6f8078", "paper");
                    PublicBox(shelf, "FolderSpineLabel" + level + "_" + item, new(.052f, .075f, .004f),
                        new(x, y + .14f, -depth * .425f), "d6caae", "paper");
                }
            }
        }
    }

    private void BuildPublicFridge(Node3D parent, Vector3 at)
    {
        var fridge = new Node3D { Name = "OldShopRefrigerator", Position = at };
        parent.AddChild(fridge);
        PublicSolid(fridge, "EnamelCabinet", new(.72f, 1.76f, .63f), new(0, .90f, 0), "c5c3af", "metal");
        foreach (var (center, height) in new[] { (.48f, .84f), (1.33f, .76f) })
        {
            PublicBox(fridge, "DoorSeal" + center, new(.69f, height + .02f, .025f), new(0, center, -.328f), "777b6e", "rubber");
            PublicBox(fridge, "EnamelDoor" + center, new(.65f, height - .018f, .047f), new(0, center, -.358f), "d2ceb7", "metal");
            FacilityRod(fridge, "PullHandle" + center, new(.24f, center - .16f, -.413f), new(.24f, center + .16f, -.413f), .016f, "969c8e");
        }
        foreach (var x in new[] { -.26f, .26f })
        foreach (var z in new[] { -.23f, .23f })
            PublicSolid(fridge, "Foot" + x + "_" + z, new(.065f, .055f, .065f), new(x, .0275f, z), "51554d", "metal");
        for (var i = 0; i < 7; i++)
            PublicBox(fridge, "KickplateVent" + i, new(.059f, .012f, .008f), new(-.25f + i * .083f, .08f, -.371f), "73796b", "metal");
    }

    private static void ShelterPublicMaterials(Node3D root)
    {
        foreach (var mesh in root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (mesh.MaterialOverride is not ShaderMaterial source) continue;
            mesh.MaterialOverride = PainterlyMaterialLibrary.WithoutSnow(source);
        }
    }

    private void BuildPublicVillagePlan()
    {
        if (_publicPlanSurface is null || AddressRegistry is not { } registry) return;
        var edges = registry.Graph.Edges.Values.Where(edge => registry.Graph.Nodes.ContainsKey(edge.A)
            && registry.Graph.Nodes.ContainsKey(edge.B)).OrderBy(edge => edge.Id, StringComparer.Ordinal).ToArray();
        if (edges.Length == 0) throw new InvalidOperationException("The physical village plan requires the shared imported road graph.");
        var points = registry.Graph.Nodes.Values.Select(node => node.Position).ToArray();
        var minX = points.Min(point => point.X); var maxX = points.Max(point => point.X);
        var minZ = points.Min(point => point.Z); var maxZ = points.Max(point => point.Z);
        var scale = Math.Min(1.10 / Math.Max(1, maxX - minX), .68 / Math.Max(1, maxZ - minZ));
        Vector3 Project(SettlementPoint point) => new((float)((point.X - (minX + maxX) * .5) * scale),
            (float)(-(point.Z - (minZ + maxZ) * .5) * scale - .055), .026f);
        using var builder = new SurfaceTool();
        builder.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var edge in edges)
        {
            var a = Project(registry.Graph.Nodes[edge.A].Position);
            var b = Project(registry.Graph.Nodes[edge.B].Position);
            var delta = b - a;
            if (delta.LengthSquared() < 1e-10f) continue;
            var side = new Vector3(-delta.Y, delta.X, 0).Normalized() * .003f;
            foreach (var vertex in new[] { a - side, b + side, b - side, a - side, a + side, b + side })
            { builder.SetNormal(Vector3.Back); builder.AddVertex(vertex); }
        }
        var lineMesh = builder.Commit();
        _publicPlanSurface.AddChild(new MeshInstance3D { Name = "SharedRegistryRoads", Mesh = lineMesh,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Color.FromHtml("45554c"), Roughness = 1,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled } });
        foreach (var building in registry.Buildings.Values.Where(building => building.AddressId is not null))
        {
            var point = Project(building.Position);
            PublicBox(_publicPlanSurface, "MapHouse_" + building.BuildingId, new(.012f, .009f, .002f),
                point + Vector3.Back * .003f, "826452", "paper");
        }
        foreach (var line in _publicPlanSurface.GetChildren().OfType<MeshInstance3D>()
            .Where(mesh => mesh.Name.ToString().StartsWith("WrittenLine", StringComparison.Ordinal)))
            line.Hide();
        _publicPlanSurface.SetMeta("mapDataOwner", "SettlementRegistry.Graph and Buildings");
        _publicPlanSurface.SetMeta("automaticQuestRoute", false);
    }
}
