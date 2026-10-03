using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildYardMechanisms(Node3D core, CarryCoordinator carry)
    {
        SeatGroundedYardItems(carry);
        SeatServicePathCrate(carry.Items.Single(item => item.ItemId == "carry-gap-crate"));
        var yard = new Node3D { Name = "YardRepairCorner", Position = YardGround(-30.9f, 1.8f) };
        core.AddChild(yard);
        for (var x = -1; x <= 1; x += 2)
        for (var z = -1; z <= 1; z += 2)
            MechanismSolid(yard, $"RoofPost{x}_{z}", new(.11f, 2.65f, .11f), new(x * 1.20f, 1.325f, z * .78f), "685342");
        for (var strip = 0; strip < 14; strip++)
        {
            var roof = MechanismSolid(yard, $"RoofBoard{strip}", new(.192f, .075f, 1.92f),
                new(-1.27f + strip * .195f, 2.68f, 0), "565b55");
            roof.RotationDegrees = new(-5, 0, 0);
            MechanismVisual(roof, "SettledSnow", new(.18f, .018f, 1.74f), new(0, .047f, -.04f), "c5ced0", "snow");
        }
        for (var board = 0; board < 17; board++)
        {
            var x = -1.16f + board * .145f;
            if (x > -.47f && x < .47f)
            {
                MechanismSolid(yard, $"WindowLowerBoard{board}", new(.14f, 1.04f, .045f), new(x, .52f, -.79f), "756550");
                MechanismSolid(yard, $"WindowUpperBoard{board}", new(.14f, .72f, .045f), new(x, 2.22f, -.79f), "756550");
            }
            else MechanismSolid(yard, $"BackBoard{board}", new(.14f, 2.58f, .045f), new(x, 1.29f, -.79f), "756550");
        }
        MechanismSolid(yard, "RepairShelf", new(1.02f, .065f, .52f), new(-.63f, .77f, -.28f), "967b5b");
        foreach (var x in new[] { -1.05f, -.21f })
        foreach (var z in new[] { -.47f, -.09f })
            MechanismSolid(yard, $"ShelfLeg{x}_{z}", new(.055f, .74f, .055f), new(x, .37f, z), "66513c");
        carry.Register(CarryableProp.Create("carry-cloth", "Чистая хлопковая ткань", CarryableProp.ItemClass.Light,
            yard.ToGlobal(new(-.91f, .81f, -.26f)), 8, "c6b79a", "fabric", CarryableProp.ItemKind.Cloth));
        carry.Register(CarryableProp.Create("carry-hook", "Съёмный крючок", CarryableProp.ItemClass.Light,
            yard.ToGlobal(new(-.32f, .81f, -.26f)), 0, "646c68", "metal", CarryableProp.ItemKind.Hook));
        // The coordinator has not entered the tree yet: this is the authored
        // rest of the existing lamp, captured once by CarryableProp._Ready.
        // Saved held/placed deviations still take precedence after restoration.
        carry.Items.Single(item => item.ItemId == "carry-lantern").Position = yard.ToGlobal(new(-.63f, .81f, -.26f));
        // The worn rest is under the hook itself. Binding the whole shelf made
        // its default aim point coincide with the parked lamp in the middle.
        // This thin surface mark rests on the existing physical shelf; it adds
        // no support volume and leaves the lamp's real occlusion intact.
        var hookSeat = MechanismVisual(yard, "HookRestWornSeat", new(.18f, .0015f, .16f),
            new(-.32f, .80325f, -.26f), "b09a78", "wood");
        var hookRest = YardMechanism.Create("hook-rest", YardMechanism.Operation.DetachHook, hookSeat,
            "Снять крючок на полку", "Крючок снимается обратно", "Крючок лежит на полке. Шест снова можно использовать отдельно.");
        hookRest.RestPoint = yard.ToGlobal(new(-.32f, .81f, -.26f));
        carry.Register(hookRest);

        var glass = MechanismVisual(yard, "WorkshopWindowGlass", new(.80f, .78f, .014f), new(0, 1.45f, -.765f), "727f7d", "glass");
        glass.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(.55f, .65f, .63f, .24f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = .36f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        glass.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var glassBody = new StaticBody3D { Name = "GlassContact", CollisionLayer = 1, CollisionMask = 0 };
        glassBody.SetMeta("collisionOwner", "yard-mechanism-solid");
        glass.AddChild(glassBody);
        var glassContact = new CollisionShape3D { Shape = new BoxShape3D { Size = new(.80f, .78f, .014f) } };
        glassContact.SetMeta("authoredSourceMesh", glass.GetPath().ToString());
        glassBody.AddChild(glassContact);
        foreach (var x in new[] { -.45f, .45f })
            MechanismSolid(yard, $"WindowJamb{x}", new(.065f, .91f, .085f), new(x, 1.45f, -.745f), "83928b");
        foreach (var y in new[] { 1.005f, 1.895f })
            MechanismSolid(yard, $"WindowRail{y}", new(.96f, .065f, .085f), new(0, y, -.745f), "83928b");
        var frost = MechanismVisual(yard, "WindowFrost", new(.79f, .77f, .006f), new(0, 1.45f, -.752f), "c3ced0", "snow");
        var windowThaw = YardMechanism.Create("window-thaw", YardMechanism.Operation.Thaw, frost,
            "Протереть обмерзшее стекло", "Нужна тёплая ткань", "Отошёл иней. За стеклом видна отогнутая жестяная полоска.",
            solved => frost.Visible = !solved);
        carry.Register(windowThaw);
        var repairStrip = MechanismVisual(yard, "OldWindowRepairStrip", new(.032f, .49f, .018f), new(.21f, 1.39f, -.787f), "aaa18d", "metal");
        repairStrip.RotationDegrees = new(0, -18, -9);
        var windowRead = YardMechanism.Create("window-light", YardMechanism.Operation.ReadByLight, glass,
            "Рассмотреть старый ремонт на просвет", "Сначала очистите стекло",
            "Свет отражается от жести. Узкая светлая черта — след ремонта; за ней обычная оконная рама.");
        windowRead.PrerequisiteKey = windowThaw.StateKey;
        carry.Register(windowRead);

        var cabinet = new Node3D { Name = "YardToolCabinet", Position = new(.80f, 0, -.12f) };
        yard.AddChild(cabinet);
        MechanismSolid(cabinet, "LeftSide", new(.045f, 1.17f, .50f), new(-.29f, .585f, 0), "6a5745");
        MechanismSolid(cabinet, "RightSide", new(.045f, 1.17f, .50f), new(.29f, .585f, 0), "6a5745");
        MechanismSolid(cabinet, "Back", new(.62f, 1.17f, .035f), new(0, .585f, -.24f), "6a5745");
        foreach (var y in new[] { .12f, .61f, 1.19f })
            MechanismSolid(cabinet, $"Shelf{y}", new(.62f, .055f, .50f), new(0, y, 0), "907759");
        var hinge = new Node3D { Name = "DoorHinge", Position = new(-.29f, 0, .25f) };
        cabinet.AddChild(hinge);
        MechanismSolid(hinge, "Door", new(.58f, 1.14f, .035f), new(.29f, .585f, 0), "6c7066");
        var latch = MechanismVisual(hinge, "IcedLatch", new(.12f, .085f, .048f), new(.49f, .80f, .04f), "b9c8c9", "snow");
        carry.Register(YardMechanism.Create("cabinet-thaw", YardMechanism.Operation.Thaw, latch,
            "Отогреть примерзшую защёлку", "Нужна тёплая ткань", "Защёлка освободилась. Дверца шкафа открывается без усилия.",
            solved => hinge.RotationDegrees = new(0, solved ? -110 : 0, 0)));
        var card = MechanismVisual(cabinet, "RepairCard", new(.25f, .12f, .008f), new(.02f, .83f, -.216f), "a79574", "paper");
        for (var i = 0; i < 3; i++)
            MechanismVisual(cabinet, $"PencilNotch{i}", new(.09f - i * .018f, .006f, .003f), new(-.02f, .85f - i * .026f, -.209f), "4d4336", "wood");
        var cabinetRead = YardMechanism.Create("cabinet-light", YardMechanism.Operation.ReadByLight, card,
            "Осветить затёртую карточку", "Нужен фонарь",
            "Три длины для ремонта: полка, оконный запор, перекладина. На шесте сохранились такие же зарубки.");
        cabinetRead.PrerequisiteKey = "yard/cabinet-thaw";
        carry.Register(cabinetRead);

        BuildHighRepairShelf(yard, carry);

        var loosePanel = MechanismSolid(yard, "LooseSidePanel", new(.065f, .72f, .58f), new(1.20f, 1.32f, .31f), "7b705b");
        var panel = YardMechanism.Create("side-panel", YardMechanism.Operation.Nudge, loosePanel,
            "Подправить стучащую доску", "Можно поддеть шестом или обойти навес и прижать доску рукой",
            "Доска встала под перекладину. Стук прекратился; на кромке видно, что её поправляли и раньше.",
            solved => loosePanel.RotationDegrees = new(0, solved ? 0 : -5, 0));
        panel.AlternativeApproach = yard.ToGlobal(new(1.93f, .02f, 0));
        panel.SoundSample = "wood_tap"; panel.SoundCaption = "У боковой доски: сухой стук"; panel.MovingPart = loosePanel;
        panel.CueCause = YardMechanism.SoundCause.Wind;
        carry.Register(panel);
        var shutter = MechanismSolid(yard, "LooseUpperShutter", new(.66f, .26f, .035f), new(.71f, 2.35f, -.72f), "7b6c57");
        var highLatch = MechanismVisual(yard, "UpperShutterLatch", new(.16f, .08f, .04f), new(.78f, 2.22f, -.682f), "5f6866", "metal");
        var latchAction = YardMechanism.Create("upper-latch", YardMechanism.Operation.HookLatch, highLatch,
            "Подцепить запор верхней створки", "Нужен крючок или устойчивая ступень",
            "Створка прижата. Металлический дребезг стих; щель стала частью окна, а не отдельным звуком.",
            solved => shutter.RotationDegrees = new(solved ? 0 : -12, 0, 0));
        latchAction.SoundSample = "metal_rattle"; latchAction.SoundCaption = "Сверху: дребезжит железный запор"; latchAction.MovingPart = shutter;
        latchAction.CueCause = YardMechanism.SoundCause.Wind;
        carry.Register(latchAction);
        // The tapping board actually rests on two little spacers; the dark
        // pocket between them is the hollow that gives the knock its sound.
        var footboardParent = _underdeckRepairRoot
            ?? throw new InvalidOperationException("The loft underdeck support must be built before its repair board.");
        foreach (var side in new[] { -.16f, .16f })
            MechanismSolid(footboardParent, $"FootBoardSupportLeg{side}", new(.065f, .0875f, .31f),
                new(.36f + side, .04375f, .62f), "65533f", terrainFeet: false);
        var bottomBoard = MechanismSolid(footboardParent, "LooseFootBoard", new(.44f, .045f, .31f), new(.36f, .11f, .62f), "877257");
        var footAction = YardMechanism.Create("loose-footboard", YardMechanism.Operation.QuietRattle, bottomBoard,
            "Уложить дощечку на место", "Освободите руку", "Под дощечкой пустота между опорами. Это дерево отдавалось глухим стуком.",
            solved => bottomBoard.RotationDegrees = new(0, 0, solved ? 0 : 4));
        footAction.SoundSample = "hollow_board"; footAction.SoundCaption = "Под ногами: глухой перестук"; footAction.MovingPart = bottomBoard;
        footAction.CueCause = YardMechanism.SoundCause.FootContact;
        carry.Register(footAction);
        BuildBoardRest(yard, carry);
        BuildBoardCrossing(core, carry);

        var house = _zoneInstances["house_old_pc"];
        var bowl = MechanismBowl(house, StyleBenchmarkInteriorFactory.WarmWaterAnchor);
        var warm = YardMechanism.Create("warm-water", YardMechanism.Operation.WarmCloth, bowl,
            "Смочить ткань тёплой водой", "Нужна чистая ткань", "Ткань тёплая и влажная. Можно размягчить иней или освободить небольшую защёлку.");
        warm.ZoneId = "house_old_pc";
        carry.Register(warm);
        yard.SetMeta("sourceBasis", "fictional household repair arrangement; existing research and material library; winter 2026");
    }

    private static void BuildHighRepairShelf(Node3D yard, CarryCoordinator carry)
    {
        // The shelf projects from the existing workshop wall. Its box moves
        // along a real bearing surface; nothing drops from above the player.
        MechanismSolid(yard, "HighRepairShelf", new(1.05f, .055f, .90f), new(-.62f, 1.92f, -.15f), "8b7357");
        foreach (var x in new[] { -1.02f, -.23f })
        {
            MechanismSolid(yard, $"ShelfWallBracket{x}", new(.052f, .35f, .075f), new(x, 1.73f, -.75f), "5d615b");
            MechanismSolid(yard, $"ShelfBearingBracket{x}", new(.052f, .052f, .90f), new(x, 1.8665f, -.275f), "5d615b");
        }
        var rear = new Vector3(-.62f, 1.9495f, -.42f);
        var near = rear with { Z = .11f };
        var tray = new Node3D { Name = "HighShelfRepairBox", Position = rear };
        yard.AddChild(tray);
        MechanismSolid(tray, "BoxBase", new(.36f, .014f, .29f), new(0, .007f, 0), "8d795a");
        var front = MechanismSolid(tray, "BoxFront", new(.36f, .17f, .014f), new(0, .099f, .138f), "998261");
        MechanismSolid(tray, "BoxBack", new(.36f, .17f, .014f), new(0, .099f, -.138f), "998261");
        foreach (var side in new[] { -.173f, .173f })
            MechanismSolid(tray, $"BoxSide{side}", new(.014f, .17f, .29f), new(side, .099f, 0), "887354");
        for (var shim = 0; shim < 4; shim++)
        {
            // Ends project above the rim: a standing player can inspect them
            // from below the shelf without being told about invisible contents.
            var length = .29f - shim * .018f;
            var piece = MechanismVisual(tray, $"RepairShim{shim}", new(.055f, length, .018f),
                new(-.105f + shim * .065f, .014f + length * .5f, .015f), "b39a72");
            piece.RotationDegrees = new(0, -12 + shim * 8, 0);
        }
        MechanismVisual(front, "WornPushMark", new(.12f, .027f, .004f), new(.04f, -.018f, .009f), "b6a382");
        var shelf = YardMechanism.Create("high-shelf-box", YardMechanism.Operation.Nudge, front,
            "Подвинуть коробку к краю полки", "До дальней стороны коробки дотянется длинный шест",
            "Коробка теперь у края полки. Над бортом видны тонкие деревянные прокладки — такими выравнивают полки и настил.",
            solved => tray.Position = solved ? near : rear);
        shelf.MovingPart = tray;
        shelf.SoundSample = "wood_tap";
        carry.Register(shelf);
    }

    private static void BuildBoardRest(Node3D yard, CarryCoordinator carry)
    {
        // Keep the original assembly anchor, including old saved board poses.
        // The hand approach uses the clear centre between the two crossbars.
        var at = new Vector3(1.95f, .66f, 0);
        MeshInstance3D? crossbar = null;
        for (var end = -1; end <= 1; end += 2)
        {
            crossbar = MechanismSolid(yard, $"TrestleCrossbar{end}", new(.48f, .075f, .23f), at + new Vector3(0, -.0375f, end * .74f), "937754");
            foreach (var side in new[] { -.18f, .18f })
                MechanismSolid(yard, $"TrestleLeg{end}_{side}", new(.075f, .585f, .075f), new(at.X + side, .2925f, end * .74f), "705943");
        }
        var rest = YardMechanism.Create("board-rest", YardMechanism.Operation.RestBoard, crossbar!,
            "Уложить доску на козлы", "Нужна доска", "Доска лежит обоими концами на опорах. Можно поставить фонарь и освободить руки. Доску можно забрать, когда сверху никого и ничего нет.");
        rest.RestPoint = yard.ToGlobal(at + Vector3.Up * .012f);
        BuildBoardRestApproach(yard, at);
        carry.Register(rest);
    }

    private static void BuildBoardRestApproach(Node3D yard, Vector3 rest)
    {
        // A short, supported wooden gangway belongs to this work stand. Its
        // visible sloping surface is its collider; normal walking owns ascent.
        // Native board-rest-15: with the player clear, GangwayContact alone
        // intersected the board's full body at the existing 2mm query margin.
        // Keep a natural 15mm joint; both supports and all save IDs stay put.
        const float nearZ = .915f;
        const float farZ = 2.40f;
        const float halfWidth = .25f;
        const float thickness = .075f;
        var top = rest.Y + .008f + .075f;
        float GroundAt(float x, float z)
        {
            var point = yard.ToGlobal(new(x, 0, z));
            return yard.ToLocal(new(point.X, AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .025f, point.Z)).Y;
        }
        var low = Math.Max(GroundAt(rest.X - halfWidth, farZ), GroundAt(rest.X + halfWidth, farZ));
        var vertices = new[]
        {
            new Vector3(rest.X - halfWidth, top, nearZ), new Vector3(rest.X + halfWidth, top, nearZ),
            new Vector3(rest.X - halfWidth, low, farZ), new Vector3(rest.X + halfWidth, low, farZ),
            new Vector3(rest.X - halfWidth, top - thickness, nearZ), new Vector3(rest.X + halfWidth, top - thickness, nearZ),
            new Vector3(rest.X - halfWidth, low - thickness, farZ), new Vector3(rest.X + halfWidth, low - thickness, farZ)
        };
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        // Godot uses clockwise front faces, including the concave contact.
        foreach (var index in new[] { 0,3,2,0,1,3, 4,7,5,4,6,7, 0,5,1,0,4,5,
                     2,7,6,2,3,7, 0,6,4,0,2,6, 1,7,3,1,5,7 })
        {
            surface.SetUV(new(vertices[index].X * 2, vertices[index].Z * 1.2f));
            surface.AddVertex(vertices[index]);
        }
        surface.GenerateNormals();
        var mesh = surface.Commit();
        var ramp = new MeshInstance3D { Name = "WorkStandGangway", Mesh = mesh,
            MaterialOverride = PainterlyMaterialLibrary.ForColor("897052", "wood") };
        yard.AddChild(ramp);
        var body = new StaticBody3D { Name = "GangwayContact", CollisionLayer = 1, CollisionMask = 0 };
        body.SetMeta("collisionOwner", "yard-mechanism-solid");
        body.SetMeta("footstepSurface", "wood");
        ramp.AddChild(body);
        var shape = new CollisionShape3D { Shape = mesh.CreateTrimeshShape() };
        shape.SetMeta("authoredSourceMesh", ramp.GetPath().ToString());
        body.AddChild(shape);
        var supportTop = Mathf.Lerp(top, low, (.99f - nearZ) / (farZ - nearZ)) - thickness;
        foreach (var side in new[] { -.18f, .18f })
            MechanismSolid(yard, $"GangwaySupportLeg{side}", new(.07f, supportTop, .07f),
                new(rest.X + side, supportTop * .5f, .99f), "6e583e");
    }

    private static Vector3 YardGround(float x, float z) => new(x, AgentBAct1HeightField.CollisionGround(x, z) + .008f, z);

    private static MeshInstance3D AddYardSnowDetail(Node3D parent, string name, Vector3 size,
        Vector3 groundAnchor, string colour, bool swept = false)
    {
        // Reuse the authored snow-bank profile at enough length samples for a
        // small household mound. The general landform's .4m spacing otherwise
        // leaves a .26m pile with only its two buried end rows and no crown.
        var profileLength = Math.Max(1.6f, size.Z);
        var detail = AddVisualLandformSurface(parent, name, size.X, size.Y + .025f, profileLength,
            groundAnchor with { Y = 0 }, colour, "snow_ground", 0, conformToTerrain: true);
        using var arrays = ((ArrayMesh)detail.Mesh).SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        for (var index = 0; index < vertices.Length; index++)
        {
            var before = detail.ToGlobal(vertices[index]);
            var rise = before.Y - AgentBAct1HeightField.CollisionGround(before.X, before.Z);
            vertices[index].Z *= size.Z / profileLength;
            var world = detail.ToGlobal(vertices[index]);
            if (swept)
            {
                // Only millimetres of uneven scraped snow remain. Its feathered
                // perimeter meets the real ground instead of outlining a slab.
                var profile = Mathf.Clamp((rise + .025f) / (size.Y + .060f), 0, 1);
                rise = Mathf.Lerp(-.004f, .0045f, Mathf.SmoothStep(0, .28f, profile));
                rise += .001f * Mathf.Sin(vertices[index].X * 13f + vertices[index].Z * 7f);
            }
            world.Y = AgentBAct1HeightField.CollisionGround(world.X, world.Z) + rise;
            vertices[index] = detail.ToLocal(world);
        }
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        using var resized = new ArrayMesh();
        resized.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        using var normals = new SurfaceTool();
        normals.CreateFrom(resized, 0);
        normals.GenerateNormals();
        detail.Mesh = normals.Commit();
        detail.SetMeta("terrainRole", swept ? "ground-conformed shallow shovel trace; visual only"
            : "ground-conformed household snow mound; visual only");
        // ToolSnowPileWood still owns only its existing interaction ray target.
        // Neither this cosmetic mound nor the cleared trace blocks traversal.
        return detail;
    }

    private static MeshInstance3D MechanismVisual(Node3D parent, string name, Vector3 size, Vector3 position, string colour, string surface = "wood")
    {
        var mesh = new MeshInstance3D { Name = name, Mesh = RuralPropGeometry.Box(size), Position = position,
            MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, surface) };
        parent.AddChild(mesh);
        return mesh;
    }
    private static void SeatGroundedYardItems(CarryCoordinator carry)
    {
        // The loose log was partly underneath a real protruding woodpile end.
        // Leave its full body outside that end, with room to lift it normally.
        // The offset follows the authored end-face normal, not a collision exception.
        var log = carry.Items.Single(item => item.ItemId == "carry-log");
        log.Position = new(-32.173938f, log.Position.Y, 4.706752f);
        foreach (var id in new[] { "carry-log", "carry-crate", "carry-bucket", "carry-axe",
                     "carry-tool-shovel", "carry-tool-pole" })
        {
            var prop = carry.Items.Single(item => item.ItemId == id);
            SeatAuthoredCarryOnSurface(prop, .0f);
            prop.SetMeta("authoredPlacementOwner", "yard terrain footprint");
        }
    }

    private static void SeatServicePathCrate(CarryableProp crate)
    {
        // The FAP path has its own visible plank surface above the terrain.
        SeatAuthoredCarryOnSurface(crate, .030f);
        crate.SetMeta("authoredPlacementOwner", "FAP east service timber path");
    }

    private static void SeatAuthoredCarryOnSurface(CarryableProp prop, float surfaceOffset)
    {
        // The unparented coordinator and tool roots use world axes here. Fit
        // the full authored yaw and footprint to the real terrain triangles:
        // a centre-only Y left the far end of the 1.85m pole below the ground.
        // This runs before Ready captures the authored pose; normal saved
        // held/placed deviations remain authoritative after restoration.
        var centre = prop.Position;
        var half = prop.Size * .5f;
        var xAxis = (prop.Basis.X with { Y = 0 }).Normalized();
        var zAxis = (prop.Basis.Z with { Y = 0 }).Normalized();
        float Top(Vector3 at) => AgentBAct1HeightField.CollisionGround(at.X, at.Z) + surfaceOffset;
        var xGrade = (Top(centre + xAxis * half.X) - Top(centre - xAxis * half.X)) / prop.Size.X;
        var zGrade = (Top(centre + zAxis * half.Z) - Top(centre - zAxis * half.Z)) / prop.Size.Z;
        var right = new Vector3(xAxis.X, xGrade, xAxis.Z).Normalized();
        var forward = new Vector3(zAxis.X, zGrade, zAxis.Z).Normalized();
        var up = forward.Cross(right).Normalized();
        var basis = new Basis(right, up, right.Cross(up).Normalized());
        var bottom = Top(centre);
        var xSteps = Math.Max(2, Mathf.CeilToInt(prop.Size.X / .10f));
        var zSteps = Math.Max(2, Mathf.CeilToInt(prop.Size.Z / .10f));
        for (var ix = 0; ix <= xSteps; ix++)
        for (var iz = 0; iz <= zSteps; iz++)
        {
            var local = new Vector3(Mathf.Lerp(-half.X, half.X, ix / (float)xSteps), 0,
                Mathf.Lerp(-half.Z, half.Z, iz / (float)zSteps));
            var offset = basis * local;
            bottom = Mathf.Max(bottom, Top(centre + offset) - offset.Y);
        }
        prop.Transform = new(basis, new(centre.X, bottom + .003f, centre.Z));
        prop.SetMeta("placementPolicy", "untouched saves use authored pose; held/placed deviations retain full saved pose and custody");
    }

    private static MeshInstance3D MechanismSolid(Node3D parent, string name, Vector3 size, Vector3 position, string colour,
        bool terrainFeet = true)
    {
        if (terrainFeet && (name.Contains("Leg", StringComparison.Ordinal) || name.StartsWith("RoofPost", StringComparison.Ordinal)))
        {
            var world = parent.ToGlobal(position);
            var bottom = AgentBAct1HeightField.CollisionGround(world.X, world.Z) + .004f;
            var top = parent.ToGlobal(position + Vector3.Up * size.Y * .5f).Y;
            size.Y = Mathf.Max(.03f, top - bottom);
            position.Y = parent.ToLocal(new Vector3(world.X, bottom + size.Y * .5f, world.Z)).Y;
        }
        var mesh = MechanismVisual(parent, name, size, position, colour);
        var body = new StaticBody3D { Name = "SolidContact", CollisionLayer = 1, CollisionMask = 0 };
        body.SetMeta("collisionOwner", "yard-mechanism-solid");
        body.SetMeta("footstepSurface", "wood");
        mesh.AddChild(body);
        var shape = new CollisionShape3D { Shape = new BoxShape3D { Size = size } };
        shape.SetMeta("authoredSourceMesh", mesh.GetPath().ToString());
        body.AddChild(shape);
        return mesh;
    }
    private static MeshInstance3D MechanismBowl(Node3D parent, Vector3 at)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < 20; i++)
        {
            var a = i * Mathf.Tau / 20; var b = (i + 1) * Mathf.Tau / 20;
            Vector3 Ring(float angle, float r, float y) => new(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            var p = new[] { Ring(a, .09f, .006f), Ring(b, .09f, .006f), Ring(a, .132f, .09f), Ring(b, .132f, .09f),
                Ring(a, .086f, .013f), Ring(b, .086f, .013f), Ring(a, .125f, .09f), Ring(b, .125f, .09f) };
            foreach (var index in new[] { 0,2,3,0,3,1,4,5,7,4,7,6,2,6,7,2,7,3 }) surface.AddVertex(p[index]);
            surface.AddVertex(new Vector3(0, .013f, 0)); surface.AddVertex(p[5]); surface.AddVertex(p[4]);
        }
        surface.GenerateNormals();
        var bowl = new MeshInstance3D { Name = "HearthWarmWaterBowl", Mesh = surface.Commit(), Position = at,
            MaterialOverride = PainterlyMaterialLibrary.ForColor("90978b", "metal") };
        parent.AddChild(bowl);
        bowl.AddChild(new MeshInstance3D { Name = "WarmWaterSurface", Position = new(0, .065f, 0),
            Mesh = new CylinderMesh { TopRadius = .110f, BottomRadius = .110f, Height = .003f, RadialSegments = 20 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("8a9a97", "glass") });
        return bowl;
    }
}
