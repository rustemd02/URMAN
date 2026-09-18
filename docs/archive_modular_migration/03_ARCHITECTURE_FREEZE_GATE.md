---
task_id: MM-03
title: Заморозка архитектурного контракта
status: passed
kind: review-gate
depends_on: [MM-02]
blocks: [MM-10]
parallel_group: null
owner_scope:
  - docs/modular_migration/01_ARCHITECTURE_CONTRACT.md
  - docs/modular_migration/03_ARCHITECTURE_FREEZE_GATE.md
forbidden_scope:
  - src/**
  - content/**
  - public/**
deliverables:
  - Независимый review двенадцати fixtures
  - Frozen architecture contract или явный список блокеров
verification:
  - rg -n "status: frozen|Status: Frozen" docs/modular_migration/01_ARCHITECTURE_CONTRACT.md docs/modular_migration/03_ARCHITECTURE_FREEZE_GATE.md
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-03 — Architecture freeze gate

## Цель

Не разрешить кодовую миграцию, пока контракты `MM-01` не выдержат стресс-матрицу `MM-02` и не устранят разночтения между compiler, kernel, capability и snapshot.

## Порядок проверки

1. Назначить reviewer, который не будет исполнителем `MM-20` или `MM-31`.
2. Для каждого из 12 fixtures описать путь: content definition → compiled pack → registry → command/transaction → capability events → snapshot/restore → dispose.
3. Проверить swap: новый персонаж через role binding, новый порядок независимых квестов и новая side arc не меняют kernel.
4. Проверить custom mechanic: меняются только capability package, его schema/catalog registration и content config.
5. Проверить failure cases: duplicate/unresolved ID, unknown opcode, missing capability/asset, resource conflict и fingerprint mismatch дают typed preflight/load errors.
6. Проверить, что `CompiledContentPack`, diagnostics, loader/registry API и `GameSnapshotV2` названы одинаково во всех последующих пакетах.
7. Если блокеров нет, заменить `status: draft` на `status: frozen` в `MM-01` и записать ниже дату, reviewer и evidence. Если блокер есть, вернуть `MM-01` владельцу; `MM-10` не запускать.

## Freeze checklist

- [x] ID и exact-version policy однозначны.
- [x] Нет implicit override, alias или hot-swap активного run.
- [x] Kernel — единственный writer; transaction атомарна и идемпотентна.
- [x] Capability lifecycle, cleanup и accessibility определены.
- [x] Campaign lock/fingerprint и deterministic restore определены.
- [x] Browser discovery имеет конкретный путь через compiler и Vite catalog.
- [x] Legacy retirement запрещает двух владельцев.
- [x] Scope не разросся до ECS, scripting engine или реализации fixtures.

## Bounded amendment review — 2026-07-17

MM-30 spec review доказал три пробела первоначального portable handoff. Re-freeze ограничен следующими seam и не открывает другие решения: обязательный `narrativeOrder` плюс closed `required-reachable`/`reveal-not-before`; static capability `resourceClaims`; сохранение normalized Markdown body в `DocumentDefinition.bodyMarkdown`. Дополнительно compiler получает schema-derived ContentId reference traversal и fatal `MissingModuleSelection`. До независимого повторного review статус MM-01 — `amendment-review`, а MM-30 заблокирован.

Result: `AMENDMENT SPEC PASS` и отдельный `AMENDMENT QUALITY PASS`; P0/P1/P2 нет. Проверено 17 schemas, 26/26 schema tests, generated-type drift и `git diff --check`. Все 12 stress paths сохраняют прежние boundaries; MM-01 повторно frozen, MM-20 complete, MM-30 разблокирован.

## Bounded amendment review — immutable claim query, 2026-07-17

MM-40 quality review обнаружил, что результата со `status: committed` недостаточно, чтобы capability host доказал фактическое освобождение kernel-owned claim до `stop/dispose`: ошибочный cleanup handler мог закоммитить другой plan. Контракт уже запрещает второго владельца claims, поэтому решение не может быть локальным mutable registry capability host.

Re-freeze ограничен одним публичным seam: `RuntimeContext.query(readModelSelector)` возвращает immutable `{ state, claims }`. Он не отдаёт mutable store, occurrence ledger, capability session или новую command/effect API. `select` остаётся state-only. Capability host обязан проверить через query отсутствие каждого child `capabilityInstanceId` owner после committed cleanup transaction и только затем вызвать `stop/dispose`.

Повторно проверить все 12 stress fixtures: в каждом cleanup path query должен видеть ноль claims соответствующего owner после transaction и перед dispose; положительный claim запрещает teardown. Не меняются portable data, capability protocol/version, snapshot shape, composition root, narrative или v1 non-goals. До независимых spec и quality verdict статусы MM-01/MM-03 остаются `amendment-review`; downstream implementation не объявляется accepted.

Result: `MM-03 REFREEZE PASS` и отдельный final `QUALITY PASS`. Проверены все 12 существующих lifecycle paths; immutable query видит только kernel-owned claims и блокирует teardown при оставшемся owner. 54 runtime tests, architecture tests, build и diff check прошли. MM-01 повторно frozen; MM-31/MM-40 accepted, MM-41 разблокирован.

## Bounded old-PC metadata re-freeze — 2026-07-17

MM-52 обнаружил, что portable `DocumentDefinition` мог хранить body, но не нормализованные metadata, нужные для старого ПК. Re-freeze ограничен optional closed `oldPc` object с `type`, `pcSection`, `canonStatus`, `reliability`, `searchTerms`, `suggestedTerms`. Он не меняет runtime state/capability protocol/authoritative progression: source `requires` становятся existing `accessConditions`, outcomes — existing `openEffects`; `sourceKind`, `inWorldSource`, `dangerLevel`, UI layout и parser provenance не входят. Полный Markdown/search owner — только `urman.oldpc`.

Result: `MM-03 OLDPC REFREEZE PASS`. Проверены 17 schemas, 45 focused schema/compiler/architecture checks, content 66/66, type drift и diff check. `oldPc` не создаёт parser/state/canon seam; guard ловит targeted DOM APIs, не запрещая content-local `document`. Полный production build должен быть повторён после MM-52: его WIP временно удалил oldPcContent до переключения OS consumers.

## Bounded capability-presentation amendment — 2026-07-18

Decision log уже принял узкий integration seam для MM-54. Он не меняет portable content, campaign/canon, capability protocol/version, snapshot shape, `RuntimeContext`, kernel writer или composition root: это ровно web-specific port `CapabilityHost.presentation(capabilityInstanceId)` для active exact session с собственными callable `render` и `subscribe`.

Port возвращает только immutable JSON models, host-forwarded typed input и disposable listener. Он не раскрывает raw provider session, provider runtime context, kernel state или writer API. Получение port/subscription до `start`, после `stop/dispose`, либо у session без обеих methods запрещено; остановка/утилизация host-ом terminally закрывает все уже выданные subscriptions и handles. Old-PC descriptor — первый consumer; это не превращает другие capability в UI providers и не вводит fallback.

Initial independent spec review нашёл и implementation уже устранила P1: owner runtime facade теперь guarded-forwards immutable `query` из фактического `RuntimeContext`, а не только обещает его в declaration. Regression подтверждает, что valid provider читает immutable `{ state, claims }`, а retained context после teardown уже не может query. Targeted capability/old-PC suite снова `17/17` зелёная; ранние evidence также покрывают immutable render/update model, no-raw-session surface, host input forwarding, missing-port failure, stop/dispose cleanup и terminal retained handles.

Initial independent spec review found the missing `query` P1. After the repair, root performed a separate source-and-regression review: guarded query forwarding, active-only exact port, immutable JSON boundary, terminal retained handles and old-PC forwarding are all `SPEC PASS`; focused runtime is 99/99, architecture is 2/2 and diff check passes.

Result: separate independent `QUALITY PASS`, 2026-07-18. Reviewer found no P0/P1/P2: the port remains active-session-only, carries cloned/frozen JSON through render/handle/subscribe, isolates listener errors, terminally closes retained handles/subscriptions, and never exposes a raw provider session, state or writer. The guarded query facade and old-PC forwarding were rechecked. Focused runtime 102/102, architecture 2/2 and `git diff --check` passed. This amendment is fully frozen.

## Bounded scene-to-dialogue handoff amendment — 2026-07-18

Status: independent `MM-03 REFREEZE SPEC PASS` and `QUALITY PASS`, 2026-07-18.

The portable scene interaction gets a mutually exclusive `targetDialogueId` alongside `targetSceneId`. Its start-node conditions and effects are preflighted and committed inside the originating scene action, then the web host receives the typed `entryAlreadyCommitted` handoff. The generic dialogue session must skip a second start-node command in that case. The seam changes no kernel writer, snapshot, capability protocol, campaign alias or campaign-specific provider branch.

Result: independent reviewer found no P0/P1/P2. Schema collision and unknown-dialogue diagnostics retain exact pointers; generated `SceneDefinition` preserves its interaction shape through `allOf`; the active-provider regressions prove atomic rejection and exactly-once start effects; the Chapter 1 route is register → Ринат → saved message and the final «Не отвечай» rule remains forest-only. Focused review, content/type checks, active-provider checks and `git diff --check` passed. This amendment is fully frozen.

## Повторный stress-review после исправления контракта

Reviewer не исполняет `MM-20` или `MM-31`. Для всех строк ниже definitions остаются synthetic `Proposal`: freeze проверяет seam, а не разрешает production-механику или новый канон.

| № | Полный проверенный путь | Результат фальсификатора |
|---:|---|---|
| 1 | Room/object/placement definitions → `CompiledContentPack` → `ContentRegistry` + `CapabilityRegistry` (`spatial_placement`) → place/compare command → atomic kernel item/placement transaction → ordered outcome event → `GameSnapshotV2` positions/claims/session → restore session before `start` → owner cleanup transaction → `stop/dispose` | Pass: позиции и quest ID не нужны scene/kernel; cleanup не оставляет claims |
| 2 | Authored audio pair, config, captions/non-audio cue → `CompiledContentPack` → `ContentRegistry` + `CapabilityRegistry` + resolvers (`spatial_audio_probe`) → observation command → kernel outcome transaction → ordered observation/outcome events → snapshot selected refs/session → restore before `start` → owner cleanup transaction → media/listener `stop/dispose` | Pass: слуховая и доступная ветки дают один outcome и одинаковые effects |
| 3 | Recipe/material alternatives/outcomes → `CompiledContentPack` → `ContentRegistry` + `CapabilityRegistry` (`crafting`) → craft command → atomic claim/consume/create kernel transaction → ordered success/failure outcome event → snapshot custody/occurrence/session → restore before `start` → owner cleanup transaction → `stop/dispose` | Pass: failed batch даёт zero effects, частичное списание невозможно |
| 4 | Availability window/job definition → `CompiledContentPack` → `ContentRegistry` + `CapabilityRegistry` → schedule/advance command → scheduler-backed kernel transaction → ordered due/outcome event → snapshot queue/job/fire ledger/session → restore before `start` → owner cleanup transaction → job `stop/dispose` | Pass: wall clock не участвует, job не повторяется после restore |
| 5 | Role refs, mixed text, vocabulary, item и schedule → `CompiledContentPack` → `ContentRegistry` + `SceneRegistry` + `CapabilityRegistry` with resolved role binding → dialogue/schedule command → kernel NPC/item/vocabulary transaction → ordered reaction/outcome events → snapshot binding/state/session → restore before `start` → owner cleanup transaction → `stop/dispose` | Pass: swap меняет binding/text/assets, не quest/kernel/provider |
| 6 | Scene variants, photo refs и compare rules → `CompiledContentPack` → `ContentRegistry` + `SceneRegistry` + `CapabilityRegistry` + resolvers → compare command → kernel evidence/provenance transaction → ordered compare/outcome events → snapshot refs/provenance/session → restore before `start` → owner cleanup transaction → renderer/provider `stop/dispose` | Pass: renderer не знает сюжетных image paths |
| 7 | Timed-choice config, vehicle state и audio alternative → `CompiledContentPack` → `ContentRegistry` + `CapabilityRegistry` → choice/tick command → atomic clock/vehicle/outcome transaction → ordered choice/outcome events → snapshot timer/job/vehicle/session → restore before `start` → cancel cleanup transaction → timer/media/listener `stop/dispose` | Pass: accessibility меняет presentation/input, не outcome/effects |
| 8 | Unique item definition, condition и custody rules → `CompiledContentPack` → `ContentRegistry` + `CapabilityRegistry` → transfer/place command → atomic custody/claim kernel transaction → ordered transfer/conflict event → snapshot item/custody/claims/session → restore before `start` → owner cleanup transaction → `stop/dispose` | Pass: competing transfer получает typed conflict, двойного владельца нет |
| 9 | Authored fragments/marks/subtitles + noise config → `CompiledContentPack` → `ContentRegistry` + `CapabilityRegistry` (`audio_workbench`) → edit/mark command → RNG-backed kernel outcome transaction → ordered mark/outcome events → snapshot RNG position/marks/session → restore before `start` → owner cleanup transaction → audio `stop/dispose` | Pass: DSP остаётся provider code, restore не меняет noise/intervals |
| 10 | Shore states/milestones/window + river resource ID → `CompiledContentPack` → `ContentRegistry` + `CapabilityRegistry` (`environment_sim`) → claim/advance command → atomic resource/environment transaction → ordered state/conflict events → snapshot provider/RNG/clock/claim → restore before `start` → owner cleanup transaction → `stop/dispose` | Pass: второй quest получает typed conflict и не владеет рекой отдельно |
| 11 | Room graph/items/checkpoint/failure config → `CompiledContentPack` → `ContentRegistry` + `CapabilityRegistry` (`stealth_space`) → move/checkpoint/cancel command → atomic temporary world/outcome transaction → ordered checkpoint/failure events → snapshot quest/generated/capability state → restore before `start` → cancel/retry cleanup transaction → AI/listener `stop/dispose` | Pass: двери/AI/listeners не остаются; turn-based mode даёт тот же outcome |
| 12 | Authored pool 4/12, constraints и role refs → `CompiledContentPack` → `ContentRegistry` + `CapabilityRegistry` (`content_instantiator`, `evidence_workbench`) → instantiate/bind command → seeded binding/provenance transaction → ordered binding/outcome events → snapshot generated bindings/provenance/RNG/sessions → restore both before `start` → owner cleanup transaction → both providers `stop/dispose` | Pass: restore не выбирает адресатов заново и provider не сочиняет канон |

Вывод повторного прохода: ни один fixture не требует ID-кейс в kernel, `GameState`, `SceneManager`, общем UI или provider другой механики. Обычный side quest на существующих protocols меняет только content module/campaign composition. Новая custom mechanic меняет capability package, её schemas, static descriptor catalog и content config; host build/preflight подхватывает descriptor без изменения kernel.

## Swap, failure и naming evidence

Swap проверен в трёх формах: role binding хранится как resolved/generated binding; порядок независимых квестов не является identity; новая side arc подключается явным module dependency и campaign order. Active run не hot-swap-ится — любой новый состав создаёт новый `CampaignLock` и run.

Failure boundary после исправления однозначен:

| Case | Boundary | Typed result | Mutation |
|---|---|---|---|
| duplicate/unresolved ID, unknown opcode, missing capability/asset | compile/preflight | stable diagnostic code + module/path/pointer | pack/run не создаётся |
| static или dynamic resource conflict | preflight/dispatch | `ResourceClaimConflict` / `ResourceConflict` | zero partial effects |
| duplicate occurrence, same fingerprint | dispatch | `DuplicateOccurrence` + saved-result reference | no commit/events |
| duplicate occurrence, different fingerprint | dispatch | `OccurrenceConflict` | no commit/events |
| fingerprint/schema/capability snapshot mismatch | staged load | `CampaignMismatch`, `UnsupportedSnapshotVersion`, `CapabilitySnapshotIncompatible` или `SnapshotIntegrityError` | active state не меняется |

Нормативные имена и downstream usage:

| Seam | Frozen name | Downstream packets checked | Result |
|---|---|---|---|
| Compiler output | `ContentCompilationResult`, `CompiledContentPack` | `MM-20`, `MM-30`, `MM-31`, `MM-41`, `MM-45` | alternate name отсутствует; result wrapper нормативен для `MM-20`/`MM-30`, pack существует только без error diagnostics |
| Diagnostics | только `ContentCompilationResult.diagnostics`, stable code/severity/module/path/pointer | `MM-20`, `MM-30`, `MM-45`, `MM-60` | consistent; pack не содержит diagnostics, `scripts/content/diagnostics.mjs` только создаёт codes/records |
| Browser loader | `virtual:urman-content-catalog` → `CompiledContentPack` | `MM-30`, `MM-54` | consistent; runtime filesystem loader/glob отсутствует |
| Registries | `ContentRegistry`, `SceneRegistry`, `ConditionRegistry`, `EffectRegistry`, `CapabilityRegistry`; `ContentRegistry.fromPack(pack)` | `MM-31`, `MM-51`, `MM-52`, `MM-54` | consistent; registry не service locator и не второй parser |
| Snapshot | `GameSnapshotV2`, `CampaignLock` | `MM-42`, `MM-45`, `MM-54`, `MM-70` | consistent |

Semantic enforcement теперь записан прямо в downstream tasks: `MM-42` использует key `capabilityInstanceId + protocol ID + exact protocol version + state schema version`, а `MM-31` различает `DuplicateOccurrence` с saved-result reference и `OccurrenceConflict` без нового commit/events.

## Freeze record

При исполнении добавить фактический результат в формате:

```text
Status: Frozen | Blocked
Date:
Reviewer:
Evidence:
Contract changes made before freeze:
Downstream packets released:
```

Пустой freeze record означает, что gate не пройден.

```text
Status: Frozen
Date: 2026-07-17
Reviewer: MM-03 independent architecture reviewer (mm03_freeze); not assigned to MM-20/MM-31
Evidence: independent 12-fixture table above; fixture-count scan = MM-02 12 / MM-03 12; full-lifecycle regex confirms all 12 rows contain pack/registry/command/transaction/event/snapshot/restore/dispose; frozen-marker and downstream naming scans pass with no stale occurrence/snapshot names; diagnostics single-owner and ComputerScene exclusive-owner scans pass; git diff --check exit 0; owner-file trailing-whitespace scan exit 0
Contract changes made before freeze: added fatal compilation result semantics and one Vite/registry path; stable capabilityInstanceId and multi-session snapshot key; quest checkpoint/cancel cleanup transaction; occurrence fingerprint/result ledger; staged typed load errors. Post-freeze consistency repair closed five P1 findings in MM-20/MM-30/MM-31/MM-42/MM-51/MM-52 without changing the frozen architecture intent
Downstream packets released: MM-10 only; later packets remain dependency-gated
```
