# HANDOFF CHAR — код для чужих владельцев (visual reset 2026-10-07, дорожка персонажей)

Дорожка CHAR не вправе править эти файлы. Ниже — готовые, самодостаточные куски: copying
без изменений даёт ровно то, на что ссылается `ledger_CHAR.md`.

---

## H-1 · `game/scripts/Act1DemoRoot.DevViewCapture.cs` — цифры каста в сайдкаре кадра

Без этого пункта все новые меты (`clothAnchor`, `kitStartupMicroseconds`,
`alsuMaxSupportSoleSlideMetres`, `carryGripSeatedGapMetres`, `kitWorkGatedSeconds`) живут на
нодах и не попадают в receipt, а значит карточки 044–048/102/103 нельзя закрыть кадром.
Вписать в сборку одного кадра (в `new JsonObject { ["point"] = … }`, рядом с
`["atmosphereProfile"]`), используя уже существующие `ViewText`/`ViewNumber`/`ViewMeta` и
`_main.ConnectedWorld`:

```csharp
            // VIS-031/044–048/102/103: what the character kit actually did for this frame.
            // Read-only; never creates, moves or frees a node.
            ["characters"] = ViewCharacterEvidence(camera),
```

и метод рядом с остальными `View…`-хелперами:

```csharp
    /// <summary>Every kit person visible to this camera, with the numbers their cards ask
    /// for. Distances are measured from this camera, so a far-NPC card cannot be closed by
    /// a frame in which nobody was far.</summary>
    private global::Godot.Collections.Array ViewCharacterEvidence(Camera3D camera)
    {
        var rows = new global::Godot.Collections.Array();
        var owners = new List<Node3D>();
        if (_main.ConnectedWorld is { } world) owners.Add(world);
        if (GetTree().GetFirstNodeInGroup("player_controller") is Node3D controller) owners.Add(controller);
        foreach (var root in owners)
            foreach (var instance in FindDescendants(root).OfType<Node3D>()
                         .Where(node => node.HasMeta("characterPrefix"))
                         .OrderBy(node => node.GetPath().ToString()))
            {
                var distance = camera.GlobalPosition.DistanceTo(instance.GlobalPosition);
                var row = new JsonObject
                {
                    ["path"] = ViewText(instance.GetPath().ToString()),
                    ["characterId"] = ViewText(ViewMeta(instance, "characterId")),
                    ["characterPrefix"] = ViewText(ViewMeta(instance, "characterPrefix")),
                    ["characterKit"] = ViewText(ViewMeta(instance, "characterKit")),
                    ["characterFocus"] = ViewText(ViewMeta(instance, "characterFocus")),
                    ["distanceMetres"] = ViewNumber(distance),
                    ["animationClip"] = ViewText(ViewMeta(instance, "animationClip")),
                    ["animationStatus"] = ViewText(ViewMeta(instance, "animationStatus")),
                    ["animationMotionScale"] = ViewNumber(instance.GetMeta("animationMotionScale", 1f).AsSingle()),
                    ["kitStartupMicroseconds"] = ViewNumber(instance.GetMeta("kitStartupMicroseconds", -1L).AsInt64()),
                    ["kitLoadMicroseconds"] = ViewNumber(instance.GetMeta("kitLoadMicroseconds", -1L).AsInt64()),
                    ["kitClipSelectionMicroseconds"] = ViewNumber(instance.GetMeta("kitClipSelectionMicroseconds", -1L).AsInt64()),
                    ["kitMaterialMicroseconds"] = ViewNumber(instance.GetMeta("kitMaterialMicroseconds", -1L).AsInt64()),
                    ["kitWorkGated"] = ViewText(instance.GetMeta("kitWorkGated", false).AsBool().ToString()),
                    ["kitWorkGatedSeconds"] = ViewNumber(instance.GetMeta("kitWorkGatedSeconds", 0f).AsSingle()),
                    ["kitWorkDisarmed"] = ViewText(instance.GetMeta("kitWorkDisarmed", false).AsBool().ToString()),
                    ["kitWorkResumePhaseSeconds"] = ViewNumber(instance.GetMeta("kitWorkResumePhaseSeconds", -1f).AsSingle())
                };
                // Per-mesh proof of the anchor and the soft response (VIS-031/102/104).
                var meshes = new JsonArray();
                foreach (var mesh in FindDescendants(instance).OfType<MeshInstance3D>()
                             .Where(mesh => mesh.HasMeta("clothAnchor") || mesh.HasMeta("softSkinResponse"))
                             .OrderBy(mesh => mesh.Name.ToString()))
                    meshes.Add(new JsonObject
                    {
                        ["mesh"] = ViewText(mesh.Name.ToString()),
                        ["visible"] = ViewText(mesh.IsVisibleInTree().ToString()),
                        ["clothAnchor"] = ViewText(ViewMeta(mesh, "clothAnchor")),
                        ["painterlyMaterial"] = ViewText(ViewMeta(mesh, "painterlyMaterial")),
                        ["hasMetricClothUv"] = ViewText(GeneratedCharacterKitDressing
                            .HasMetricClothUv(mesh).ToString())
                    });
                row["meshes"] = meshes;
                // The carried thing, if this frame has one (VIS-047).
                var held = GetTree().GetNodesInGroup("carry_coordinator").OfType<CarryCoordinator>()
                    .FirstOrDefault(coordinator => coordinator?.HeldItem is not null);
                if (held?.HeldItem is { } prop)
                    row["carryGrip"] = new JsonObject
                    {
                        ["itemId"] = ViewText(prop.ItemId),
                        ["gapMetres"] = ViewNumber(prop.GetMeta("carryGripGapMetres", -1f).AsSingle()),
                        ["seatedGapMetres"] = ViewNumber(prop.GetMeta("carryGripSeatedGapMetres", -1f).AsSingle()),
                        ["seatMetres"] = ViewNumber(prop.GetMeta("carryGripSeatMetres", -1f).AsSingle()),
                        ["toleranceMetres"] = ViewNumber(prop.GetMeta("carryGripContactToleranceMetres",
                            AccessibilityPresentation.CarryGripContactTolerance).AsSingle()),
                        ["reducedMotion"] = ViewText(ViewMeta(prop, "carryGripReducedMotion")),
                        ["status"] = ViewText(ViewMeta(prop, "carryGripStatus")),
                        ["stepRiseMetres"] = ViewNumber(prop.GetMeta("carryStepRiseMetres", -1f).AsSingle())
                    };
                rows.Add(Godot.Json.ParseString(row.ToJsonString()).As<Godot.Collections.Dictionary>());
            }
        return rows;
    }
```

Если в этом файле уже есть `FindDescendants`, использовать его; иначе — существующий
`Descendants`-обход из `RinatPresencePresentation.cs:416` (тот же паттерн, без LINQ-рекурсии
над всем деревом сцены). `ViewMeta` возвращает `string`, поэтому числа читаются через
`GetMeta(key, default)` — так кадр не падает на NPC, у которого меты ещё не проставлены.

---

## H-2 · `game/scripts/PainterlyMaterialLibrary.cs` — режим A как публичный контракт

Контракт опоры (`material_anchor_contract_RU.md` §4, режим A) требует один публичный вход для
деформируемой поверхности; сейчас его роль выполняет `ForMovingCloth`, и это работает, но
название обещает только ткань. Предложенный метод — тонкая обёртка над уже существующим
кэшированным материалом, без нового шейдера и без изменения параметров:

```csharp
    /// <summary>VIS-007 §4 mode A: a surface that deforms with a skinned carrier.
    /// Albedo and pigment both live in the authored UV, the snow blanket and the 6 m
    /// world-cell tint are off. <see cref="ForMovingCloth"/> is this factory's cloth
    /// case; new families enter here instead of calling <see cref="ForColor"/>.</summary>
    public static ShaderMaterial ForDeformingSurface(string htmlColor) => ForMovingCloth(htmlColor);
```

Зачем это CHAR-дорожке: правки `GeneratedCharacterKitDressing.ClothFor` и обоих путей кита
ссылаются на режим A как на имя. После добавления метода konsumers (`ClothFor`,
`ApplyHumanMaterials`, `FirstPersonController.Footwear.cs`) переименуются в один проход,
**когда** появится семейство ткани, отличное от `cloth`; сейчас поведение идентично, и
менять вызовы ради имени нет основания. Ничего из VIS-102/031/104 этой правкой не
блокируется.

---

## H-3 · `game/scripts/PainterlyMaterialLibrary.cs` — семейства отклика одежды (пре-реквизит CHAR-C01/C02)

Слоты ответа в библиотеке уже есть (`private sealed record FamilyResponse`: `Relief`,
`ReliefFreq`, `ReliefRoughness`, `NormalMap`, `NormalScale`, `RoughnessMap`,
`RoughnessDelta`, `TilesPerMetre`), и `["cloth"]` уже получает процедурный relief
`{ Relief = .22f, ReliefFreq = new(16f, 16f), ReliefRoughness = .05f }`. Проблема не в
слоте, а в том, что **весь гардероб кита попадает в одно семейство**:
`ApplyHumanMaterials` теряет авторское имя из имени материала `<hex>__<surface>`
(`fur`, `knit`, `felt`, `wool`, `leather`) и всегда просит `"cloth"`. Войлок valenki,
вязаная шапка, овчинный воротник и суконное пальто имеют поэтому буквально одинаковый
рельеф и одинаковый шум.

Готовые записи — по образцу соседей, с явным периодом (значения авторские, правятся по
паре кадров P05b/C1):

```csharp
        // VIS-102: the cast's garment families must answer light differently. Same cloth
        // albedo, own period and finish: knitted wool reads as loops, felt as a matte
        // mass, sheepskin fur as a directional pile. Deforming cloth never takes the
        // 6 m world-cell tint (material_anchor_contract_RU.md §5.8).
        ["cloth_knit"] = new() { Relief = .34f, ReliefFreq = new(11f, 11f), ReliefRoughness = .07f,
            NormalMap = ResponseRoot + "garment_knit_v1_normal.png", NormalScale = .30f,
            TilesPerMetre = 2.0f },
        ["cloth_felt"] = new() { Relief = .18f, ReliefFreq = new(7f, 7f), ReliefRoughness = .04f,
            NormalMap = ResponseRoot + "garment_felt_v1_normal.png", NormalScale = .22f,
            TilesPerMetre = 1.4f },
        ["cloth_wool"] = new() { Relief = .26f, ReliefFreq = new(13f, 13f), ReliefRoughness = .06f,
            TilesPerMetre = 1.8f },
        ["fur_animal"] = new() { Relief = .40f, ReliefFreq = new(4f, 34f), ReliefRoughness = .08f,
            NormalMap = ResponseRoot + "fur_pile_v1_normal.png", NormalScale = .35f,
            TilesPerMetre = 4.0f },
```

Три обязательных условия, иначе правка развалит другие карточки:

1. Имена нужно внести в тот же реестр режимов, который проверяет VIS-007 шаг 3
   (`SurfaceTextures` / `TunedWithoutMap` / `FlatByDesign`): недекларированное имя сейчас
   громко предупреждает и молча получает мировой дефолт.
2. Альбедо-файл во всех четырёх — существующая `old_fabric_v3_albedo.png` из
   `SurfaceTextures["cloth"]`; новую альбедо CHAR не просит (см. `asset_requests/CHAR.md`).
3. `CellJitter`/снежное одеяло для живых персонажей остаются нулём: режим A
   (`ForMovingCloth` → `ForDeformingSurface`, H-2) их уже обнуляет, новые семейства обязаны
   пройти через тот же путь, а не через `ForColor(..., "cloth")`.

После этого CHAR-дорожка в одном следующем проходе перестаёт терять семейство в
`game/scripts/GeneratedCharacterKitDressing.cs`:

```csharp
                    var surfaceKind = parts[1] is "hair" ? string.Empty
                        : parts[1] is "fur" or "sheepskin" ? "fur_animal"
                        : parts[1] is "knit" ? "cloth_knit"
                        : parts[1] is "felt" ? "cloth_felt"
                        : parts[1] is "wool" ? "cloth_wool"
                        : parts[1];   // "cloth", "leather", "cloth_clinic" уже объявлены
```

и `uvCloth`-проверка расширяется на эти имена, чтобы метка `cloth_uv_units` осталась
единственным прогоном в UV-режим для любой деформируемой ткани.

---

## H-4 · Чего CHAR-дорожка не просила и почему

- `Act1ConnectedWorld.cs` / `MosqueImamDress.cs` / `PublicBuildings.cs`: адресные
  перетекстуры уже идут через `GeneratedCharacterKitDressing.ClothFor`, то есть через то же
  правило метки `cloth_uv_units`, и теперь сами ставят `clothAnchor`-мету. Дополнительный
  handoff не нужен.
- `eng/*`: перегенерация кита и capture — команда в `ledger_CHAR.md` §3, станция её уже
  принимает (`capture --phase/--fov` из итерации 02/03).
