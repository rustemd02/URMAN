# Пробелы и release gates

Срез: **2026-09-03**. Статусы ниже — рабочая матрица для Pro. `OPEN` означает отсутствие достаточного доказательства, а не обязательно отсутствие кода. Текущий capture с 25 уникальными кадрами — отдельный P0 blocker: receipt integrity есть, но 360° coverage нет.

## MVP blocker: восемь визуальных зон

Direct visual zones receipt-а — восемь; `house_interior` и `fap_interior` являются отдельными interior checkpoints и проверяются внутри соответствующих logical owners, но не увеличивают число direct zones.

| Зона | Что уже есть | Что блокирует красивый MVP | Минимальный gate |
|---|---|---|---|
| `Arrival` | `village_day@arrival`, road envelope, первые authored parcels; five requested views имеют разные hashes | Устойчивый reverse-arrival read, рельеф и lived-in near/mid/far ещё не приняты человеком | Новый capture без serialization failure + human 360: дорога, ворота/дома, дальнее продолжение Кырлая, контакт с землёй и отсутствие поля/skybox |
| `MainStreet` | Основная улица и branch intent к FAP, five frames с разными hashes | Повторяемость растительности, боковые parcel backs и визуальная иерархия не закрыты | Вперёд/назад/влево/вправо/depth различимы; читаются обе стороны улицы, branch/landmark, near/mid/far и непрерывность с arrival |
| `BabaiEbiYard` | Yard anchor, gate/path/house composition и safe-zone intent | Уют, бытовые props, side/back house volume, grade/threshold ещё greybox/candidate | 360° human review: порог и путь в near, дом/двор в mid, street/roofs в far; проход без seam/void и без потери обратного пути |
| `HouseExteriorApproach` | Подход к дому в persistent envelope; три requested frames с разными hashes | Цельность полного дома, задняя сторона, ступени/грунт и связь со street | Near contact/steps, полный объём и side/back, readable return to village; collision не создаёт обходной путь вне route |
| `ConnectiveStreetReturn` | Узел и connectors существуют; technical waypoint достижим | Все 3 requested views побайтно одинаковы (`4923…`): текущий 360° gate недействителен; переход читается как flat/procedural | Устранить причину, затем доказать distinct forward/back/depth, near/mid/far, живую lateral framing и связь FAP→return→zirat; не принимать по одному кадру |
| `FapExterior` | Один заявленный exterior owner, kit/porch/board/fence intent | Все 5 requested views одинаковы (`4923…`); FAP не прошёл ни 360°, ни landmark/wayfinding review | Distinct 5-view capture + human check: facade/porch/board, branch/yard, fence/gate, readable return, no duplicate owner/trees; cultural medical details reviewed |
| `ZiratMemoryField` | Roadside kit, boundary/gate/marker candidates и logical `zirat_road` | Четыре направления (`forward/back/left/right`) совпали (`4923…`), лишь depth отличается; плоскость, horizon/repeated trees и religious safety открыты | Полный distinct 360°/near-mid-far capture; restrained cemetery boundary/markers, respectful composition, no invented inscriptions, village return and forest direction readable |
| `KaraForestEdge` | `kara_urman_night`, cliffhanger route и recent crown reduction | Все 5 requested views одинаковы (`d23c…`); боковые стороны sparse/primitive; capture не доказывает edge composition | Distinct views + human review: near roots/ground, mid boundary/forest mass, far darkness/horizon, lateral density and safe sightline; no literal monster/bestiary, Marat voice/Rinat interruption remains readable |

### Особый статус interior checkpoints

`house_interior` (4 frames) и `fap_interior` (4 frames) входят в текущие 44 файла. У `house_interior` четыре разных hashes; все четыре `fap_interior` входят в текущую duplicate group. Ни один набор не является acceptance: нужны 360° human review, interactable old PC/document flow, readable UI, collision/comfort и отсутствие скрытой narrative mutation. FAP interior нельзя объявлять принятым только потому, что receipt структурно содержит четыре направления; точный receipt/source и человеческий осмотр остаются обязательными.

## MVP blocker: система и содержание

| Область | Текущий разрыв | Gate до playable/beautiful Act I |
|---|---|---|
| Capture/evidence | Receipt структурно валиден, но wrapper failed на 25 unique/44; late-zone views serialized | Исправить camera/settle/output root cause; повторить только после code fix и получить 44/44 expected distinctness, затем human review всех зон |
| Canonical presentation owner | Connected root, AgentB exterior layer, legacy procedural methods и default loader сосуществуют | Архитектор фиксирует один map/presentation owner, explicit retirement/rollback boundaries и отсутствие второго narrative/interaction/audio owner |
| Narrative/dramaturgy | Content order и invariants заданы; текущий handoff не запускал полный first-time human flow | Arrival → house/old PC → official/internal Marat evidence → Gulsina/Alsu → FAP/Naila → return/zirat → Kara; `Не отвечай` появляется только после Rinat; cliffhanger эмоционально понятен |
| Interaction | `InteractionTarget` и physical gates присутствуют в коде; usability не доказана | Игрок без debug/marker dependency находит и использует old PC, документы, NPC/dialogue, journal и next route; unavailable targets не дают ложного прогресса |
| Language | 5–7 татарских слов и mixed speech заданы data-driven; quality/consultant review открыты | Лексика понятна из контекста, optional reread работает, тексты/транслитерация/переводы одобрены носителем/консультантом |
| Audio | `AmbientAudioDirector` маршрутизирует stems с crossfade; это technical/placeholder layer | Voice of Marat and Rinat, ambience, silence and cue timing support route and cliffhanger; captions/levels pass comfort review |
| Art/material/light | Candidate kits и procedural greybox дают envelope, но visual target не достигнут | Восемь зон проходят master-layout near/mid/far, reverse/lateral, contact, no repeated horizon, Painterly Low-Poly coherence и human wow/clarity review |
| Cultural/religious safety | KB требует татарского кода и уважительной исламской рамки; consultation не закрыта | No caricature/anti-folklore reading, корректные зират/Тимур хәзрәт/слова/быт; formal cultural and religious review has zero blockers |
| Accessibility/motion comfort | FOV/settings/input hooks есть; first-time comfort не проверен | FOV/sensitivity, subtitles, contrast, readability, audio captions, no harmful flash/forced motion pass baseline and human comfort check |
| Save/persistence | `RuntimeBridge` owns SaveGameV3 and sources include smoke contract | Start, quick save/load, restart and recovery preserve zone/spawn/settings/knowledge without duplicate state or sequence break |
| Onboarding/settings | Input/settings/UI nodes exist in `main.tscn`; discoverability not evidenced | New player understands movement/look/interact, can alter comfort/subtitles/settings, can recover from route ambiguity without debug instructions |

## Polish gates (после закрытия P0/P1 основы)

- Replace greybox primitives only where they affect first-person read: authored parcels, threshold/ground contact, prop clusters, vegetation silhouette and coherent horizon.
- Tune Painterly Low-Poly material scale, wetness, fog/light/rain and palette per time/place without losing readable route.
- Make FAP, zirat and Kara transitions visually consequential but restrained; remove exact repeated crowns and accidental occluders while preserving canonical collision.
- Complete ambience/voice mix, silence windows, cue ducking, captions and localization pass.
- Refine UI hierarchy, crosshair/interaction affordance, journal and document typography after a human route test.
- Measure performance after geometry/visual ownership stabilizes; do not pre-optimise by hiding evidence or adding a parallel low-fidelity world.

## Post-MVP and public-demo gates

These are not excuses to expand Act I before its blockers close:

- Acts II–V, full pact reveal, all creatures, combat, romance, open-world simulation and full KFU intro remain out of the Act I MVP.
- Public demo packaging, clean checkout/build artifact, target-platform matrix (including M1 and Windows), installer/distribution, crash/recovery and telemetry policy block a public release even if internal MVP is playable.
- External human playtest must happen after art/audio/accessibility/cultural baseline. Record route completion, old-PC discovery, language comprehension, narrative/cliffhanger response and cultural blockers; no aggregate “PASS” from smoke alone.
- Final legal/credit/licensing and asset provenance review remains required for public distribution; this bundle contains manifests, not binary assets or a license decision.

## Gate precedence

1. Fix evidence and ownership ambiguity; otherwise visual decisions are based on serialized frames.
2. Lock one canonical connected Act I map and complete eight-zone first-person acceptance.
3. Prove the gated narrative/interaction/audio loop in a clean human run.
4. Close cultural/language/religious and accessibility review.
5. Verify target platform, packaging and public-demo playtest.

Any task that cannot name its owner, acceptance artifact and stop/rollback condition should not enter the Luna execution wave.
