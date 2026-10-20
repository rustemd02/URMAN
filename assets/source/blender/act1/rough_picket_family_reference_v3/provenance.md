# Rough-picket: исходный образец семейства

Реконструкция по текущему `Act1ConnectedWorld.YardFences.cs`, а не выгрузка runtime и не игровой кадр. Частный участок-пример — ADR-H015, Бакча, 8. Один номинальный пролёт 2,35 м на 13 досок и текущие открытые двустворчатые ворота; отдельная калитка не выдумывается.

Примитивы взяты из существующего `urman_village_fence_kit.py`, единичная UV-упаковка — из существующего house V2 helper, студия — из hero-house helper. Исходные helper и runtime не изменяются. Права — Project-original. ImageGen, Meshy и внешние загрузки на этом этапе не используются.

Плоская опорная отметка заменяет terrain grounding, выбран номинальный максимальный пролёт и явная фаза трёх высот досок. Исходный Blender-примитив добавляет фаску 2 мм внутри исходного габарита. Все отличия, точные размеры, SHA зависимостей, узлы, pivots и коллизия runtime записываются в `source-receipt.json`.

Source pivots отделяют створки для дальнейшей подготовки материала; они не объявляют новую интерактивную механику. В runtime текущие створки ворот статичны и запечены в общий batch; существующие владельцы коллизии и прохода сохраняются. Снег, ландшафт и мировое освещение в образец не входят.

Результат одного Blender-запуска: 79 mesh, 2212 треугольников, пять actual source views; UV overlap selector — 0 до и после экспорта. Геометрический roundtrip сохранён. Это технический source-кандидат для отдельного просмотра root, а не художественная/native-приёмка.

Первый source-вариант повторял зазор ручки от полотна 57,5 мм из прежнего C#. Root после просмотра исправил только runtime normal offset ручки с 0,10 до 0,04 м. Отдельный разрешённый source parity run повторяет эту формулу: посадка ручки в плоскость доски 2,5 мм, тот же closing edge 1,28 м и высота 1,06 м. Прежние рецепт, сырой лог и receipt сохранены как `prepare_source_before_handle_parity.py`, `prepare-before-handle-parity.log`, `source-before-handle-parity-receipt.json`; прежняя картинка не объявляется прошедшей приёмку. Все остальные размеры, позиции, основы и роли деталей должны совпасть с первым source-вариантом.

Геометрия box-ring существующего fencekit повторена локально с безопасным обходом polygon.loop_indices, поскольку его исходный UV-код читает неподдерживаемый MeshLoop.polygon_index; общий helper не менялся.

## Один встроенный ImageGen material reference — 20.10.2026

Root лично принял исправленный source opening и source geometry как конечный вход для этой генерации; это не native/art-приёмка. Выполнен ровно один встроенный `image_gen.imagegen`, без CLI, API-ключа, Meshy, внешней загрузки и расхода Meshy-кредитов. Перед вызовом все три входа просмотрены через `view_image`.

Роли и SHA256 входов:

- `actual-source-front-right.png` — геометрия, полный состав и композиция семейства; `cf097f1474e79a26ef91358be8155e50b7c1033ddcccebcebd101f47d7c059fc`.
- `actual-source-cart-opening.png` — крупный проём, корректная посадка ручек, обвязка и открытые створки того же source; `742e86a76af8861146d4be98fe83ab8f2fbef3a53b6d58e804c3b4fb1e28fa77`.
- `evidence/ACT1-DEPTH.12/native_uv_v3_fdc5f5ef62fc4554ad107b9d9bef31ef/frames/babai_shape_front_high.png` — только игровая материальность/цвет/стилизация принятого дома; `146035efec8364cf654d773259d17a3dd538acb4cba3549e1a85204a50239adb`. Архитектура, персонаж, ландшафт и снег этого кадра не переносятся.

Результат: `imagegen-reference-v1.png`, 1536×1024, 1 668 738 байт, SHA256 `c293359efe7c3489bd6ec049a36abe4964cbb2a5700870f01f4a50adc50bb94c`.
Точный tool output: `/Users/unterlantas/.codex/generated_images/01a15661-354d-76f1-8270-5d14a959da37/exec-d4b43b53-285c-486d-892e-60778dc9040b.png`. Идентификатор артефакта `exec-d4b43b53-285c-486d-892e-60778dc9040b`; отдельного API task ID встроенный инструмент не вернул. Выбранный output скопирован в репозиторий без перекодирования; исходный generated image сохранён.

Результат лично просмотрен: бледная состаренная краска, сдержанная древесина, тёмное матовое железо; пролёт и две открытые створки остаются раздельными, проём свободен. Снега, ландшафта и дополнительных предметов нет. Небольшие изменения очертаний/счёта деталей в bitmap не являются новой геометрией: точные 13/9/9 досок, pivots, зазоры и габариты задаёт frozen donor. Более выраженная фактура древесины и её соответствие общему кадру требуют отдельной оценки root перед любыми платными картами. Это только material input, не модель/текстурный атлас/runtime/native/art PASS. `source-receipt.json` не менялся: SHA256 `703dd6d255878c30b27e5e03a9e51bd743f37d927a44dd500e571bb2798eb190`.

Погода исключена намеренно: снег, иней и направленный свет остаются runtime-слоями, а не запечённой базовой материальностью.

Точный prompt:

```text
Use case: stylized-concept.
Asset type: ONE material-direction reference image for URMAN's existing timber fence and cart-gate family, intended for a later retexture of a frozen semantic 3D donor. It is not a new geometry design or a finished game asset.
Reference roles: Image 1 is the accepted actual 3D source front-three-quarter view; it controls the complete geometry, proportions, member counts, module spacing and composition. Image 2 is the close view of the SAME accepted cart opening; it controls correct seated handle contact, iron straps, braces and the clear passage. Image 3 is ONLY the accepted game's material/color/stylization family, especially the house's weathered matte timber and quiet cream-painted trim. Do not copy that house's architecture, logs, people, village, forest, sky or snow.
Render the SAME complete source family in a clear front three-quarter view on a plain warm-grey neutral background/ground, fully framed with comfortable margins. Keep the same two source modules: one separate rough-picket fence bay with exactly THIRTEEN narrow pointed vertical boards, two square posts, two real bearing rails and clean daylight gaps; and one tall cart-gate post-and-header frame with the same restrained seven-bar fan, exactly TWO wooden cart leaves folded OPEN 90 degrees into the yard. EACH cart leaf has exactly NINE separate vertical boards, its two rails, one diagonal brace, iron straps and an attached handle. Keep the post spacing, opening, heights, board widths/thicknesses, braces, proportions and module arrangement from Image 1. Do not close the leaves, bridge the opening, add a wicket, more spans, more boards, more rails, new ornament or supports.
Change only the MATERIAL EXPRESSION: tactile but quiet painterly 3D game timber, the same broad readable forms and restrained finish as the accepted gameplay house. A thin aged ivory/ochre-grey paint wash over pine, warm grey-brown wood showing modestly at some worn edges; subtle broad pine grain with low contrast, not dense photographic splinters or noisy fiber detail. Slightly varied timber values so the members remain readable. The diagonal braces are modest weathered brown wood. Iron straps and handles are matte dark grey, softly worn, no shiny black plastic or excessive orange rust. Keep both handles physically attached to the closing-edge boards exactly as in Image 2, never floating.
Soft neutral daylight, gentle contact shadows, material color remains readable. No dramatic HDR, cinematic rim lighting, photoreal microdetail, glossy PBR showroom appearance, toy plastic, extreme peeling/grunge, mud smears, rot, broken/torn boards or accidental bevel swelling.
Absolutely NO snow, frost, icicles, landscape, vegetation, sky, houses, people, extra props, text, labels or watermark. Weather and world-light shadows are separate runtime layers and must not be baked into a future base-material reference. Keep every gap, joint and opening legible.
This is only a material reference: the accepted source 3D geometry and its semantic pivots remain authoritative even if a generated bitmap drifts. Preserve its construction rather than redesigning it.
```
