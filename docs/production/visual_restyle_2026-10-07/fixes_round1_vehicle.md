# FIXES ROUND 1 — кластер транспорта (VIS-049, VIS-050, VIS-105)

Каждый член проверен grep'ом по рабочим обращениям в репозитории.

| Ошибка | Было → Стало | Карточка | Подтверждение API |
| --- | --- | --- | --- |
| Materials.cs:109 | `SpecularAntiAliasing` → нет такого члена; ответ стекла = `Roughness .08` + `MetallicSpecular .52` | VIS-105: glass к семействам деревни | `PainterlyMaterialLibrary.cs:1697` glass→(.14,.52); `Cabin.cs:520` |
| Materials.cs:113 | `ShadowToOpaque` → удалён; кабину читает alpha `.34` | VIS-105 | idiom стекла: `MosqueInterior.cs:706` |
| Materials.cs:167/169 | `mesh.GetSurfaceCount/SurfaceGetMaterial` (MeshInstance3D) → `node.Mesh is not { } mesh` + то же на Mesh; запись `node.SetSurfaceOverrideMaterial` | VIS-105: rebind слотов «шестёрки» без правки источника | `NivaModel.cs:44,56`; `Act1ConnectedWorld.cs:10648` |
| Cabin.cs:257 | `instance.GetSurfaceCount()` → `instance.Mesh is not { } mesh` | VIS-105: тонировка снега кузова | `Cabin.cs:527` |
| Cabin.cs:287 | `Vector3.IsNaN()` → `float.IsNaN(_appliedSnowMood.X)` (sentinel X=NaN) | VIS-105 | `Act1ConnectedWorld.cs:3541` |
| NivaModel.cs:87 | `Uri.IsHexDigits` → приватный `IsHex6` | VIS-105: имя вне контракта = ошибка, не default grey | `string.IndexOf`, LINQ |
| VehicleSnowTracks.cs:289 | `var second = null` → `Vector2? second` | VIS-050 | — |
| SnowTrackObstruction.cs:47/51 | `new Rid(ulong)` + `World3D.IntersectRay` → `space.DirectSpaceState.IntersectRay`; обход своего тела: `hit["rid"].AsRid().Id == excludeRid`, перезапуск луча +4 мм. Сигнатура `ulong` не менялась | VIS-050/VIS-016: след не сквозь предмет, не теряется на корпусе | `VehicleHorsePose.cs:286,292`; `ContactMaterial.cs:65` |
| ContactMaterial.cs:272 | `mesh.GetSurfaceCount()` → `mesh.Mesh is not { } source` | VIS-105 | `ContactMaterial.cs:49` |
| ContactMaterial.cs:300 | семья бралась из `vehicleFinish` (`"glass"`) → из `surface` (`"vehicle_glass"`), fallback прежний | проверка :320 | `PainterlyMaterialLibrary.cs:1414`; `Materials.cs:121` |

## HANDOFF (вне кластера, не трогал)

Та же семья поломок: `Act1ConnectedWorld.SnowRelief.cs:349` (SurfaceSetMaterial на MeshInstance3D), `RuralPropMaterials.cs:110-119`, `GeneratedCharacterKitDressing.cs:304/615`, `AddressFacadeMount.cs:248`, `AtmosphereProfiles.cs:85/117`, `Act1ConnectedWorld.Ravine.cs:247`, `YardComposition.cs:194/288`, `experiments/agent_b_act1/*`, `GraphicsQuality.cs:43`.
Сборку не запускал — за основным агентом.
