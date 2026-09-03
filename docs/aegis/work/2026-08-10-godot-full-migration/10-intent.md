# Task Intent Draft — атмосферная демка первого акта «УРМАНА»

Date: 2026-08-14  
Goal status: active  
Goal key: `urman-act1-atmospheric-demo`  
Branch: `main`  
HEAD at start: `718daefcea7de791de16e29ce0db86deb477e8d3`  
ArchitectureReviewRequired: yes

## Goal change / DriftCheck — 2026-08-14

The previous active objective was the complete Godot migration and production
of Acts 1–5. The user explicitly changed the current objective: deliver and
validate one playable, atmospheric first-person Act 1 demo. The complete
migration, Acts 2–5, six-to-eight-hour playthrough, desktop release gates and
web retirement are retained as deferred long-term work, not as this goal's
`done_when` criteria. This drift does not change canon, runtime ownership,
SaveGameV3, browser data or the future roadmap.

## Current requested result

`res://scenes/act1_demo.tscn` must launch a fixed-first-person demo through the
five compact Chapter 1 zones and 16 authored beats: arrival, village, old PC,
FAP, zirat and the Kara-Urman edge. The demo must expose the investigation
loop, dialogue/document/journal state, a first татарский vocabulary unlock,
the old-PC clue and the final `НЕ ОТВЕЧАЙ` / `Конец демо` cliffhanger.

## Current Goal Contract

### Success evidence

- Clean Godot launch reaches the Act 1 entry overlay and fixed first-person
  controls without a web-runtime dependency.
- A deterministic Chapter 1 flow test reaches all 16 beats, preserves shared
  clues/journal/vocabulary/document state and ends on the demo overlay.
- Save/load and input/accessibility smoke remain green for the demo route.
- Three Godot style frames (day street, house with old PC, Kara-Urman edge)
  are captured and recorded as production-candidate evidence, not art lock.
- Remaining human-facing risks are written down: voice/mix, cultural review,
  observed playtest, motion/readability, release hardware and art acceptance.

### Stop condition

- `done`: the demo launch, full 16-beat flow, persistence/input checks and
  three style-frame receipts are all verified and documented.
- `needs-verification`: implementation exists but a required demo receipt is
  missing or stale.
- `blocked`: a required Godot/toolchain or content dependency cannot run and
  the blocker is recorded with a reproducible command.
- `scope-exceeded`: work starts producing Acts 2–5 content, full-game release
  acceptance or web-retirement changes without a new explicit goal.

## Deferred north-star contract — not current done_when

The full migration contract below is preserved as historical architecture and
long-term backlog. It must not pull this demo task into production of the last
act or into web cutover.

## Deferred north-star contract (historical, not this goal)

The original full-migration request is retained below as a reference for the
long-term architecture. It is not an active completion contract for this goal.
The machine-readable queue in `docs/urman_knowledge_base/execution_backlog.json`
is authoritative for the current Act 1 delivery boundary.

## Historical requested result

Выполнить `docs/aegis/plans/2026-08-10-godot-full-migration.md` и задачи из
`docs/urman_knowledge_base/execution_backlog.json`: перенести игру, pipeline и
runtime на Godot 4.7.1 .NET/C#, произвести Painterly Low-Poly 3D-версию на
6–8 часов и удалить старый web-runtime только после доказанной parity, desktop
acceptance и полного playthrough. Этот текст сохранён как долгосрочная
архитектурная справка и не расширяет текущий Act 1 goal.

## Historical Goal Contract

### Success evidence

- C# compiler/content pipeline воспроизводит текущую Chapter 1 без потери контента, fingerprint или narrative invariants; чистое C#-ядро проходит unit-, scenario- и save/load-тесты.
- Godot first-person slice и full-game path проходят от приезда Айдара через расследование Марата, пакт и исторические слои до разрушения несправедливого пакта и одной канонической трагической концовки.
- Все 12 Acts 2–5 зон получают production-grade Painterly Low-Poly presentation, authored hero assets, collision/LOD budgets, звук, субтитры и культурно проверенный контент; три обязательных style frames принимаются только из Godot.
- SaveGameV3, old PC, documents, journal, dialogues, татарский re-read, audio cues и world variants используют одно runtime state; accessibility settings проходят технический roundtrip и внешний readability/motion-comfort review.
- macOS и Windows exports запускаются на целевых host, а M1/Windows performance evidence подтверждает release budgets.
- После acceptance static absence gate подтверждает отсутствие запускаемого production TypeScript/Vite/Three.js/browser runtime; browser localStorage не удаляется и остаётся только историческими данными вне нового runtime.

### Stop condition

- `done`: все success evidence собраны и связаны с `docs/aegis/work/`.
- `blocked`: отсутствует обязательная зависимость, разрешение, hardware run или культурный reviewer; состояние и следующий task записаны в execution backlog.
- `needs-verification`: реализация есть, но evidence не покрывает критерий.
- `scope-exceeded`: продолжение требует browser/mobile/open-world/combat/multiplayer/alternative-ending или второго runtime owner.

### Execution boundary

Текущий исполняемый порядок определяется `docs/urman_knowledge_base/execution_backlog.json` (7 milestones, stable task IDs, dependencies, owners, `done_when`, `verify`, evidence). `backlog.md` — human index, `roadmap.md` — milestone narrative, `decision_log.md`, `open_questions.md`, `technical_architecture.md` и `docs/aegis/work/` — authority/evidence. Оркестратор может брать только `ready`-задачу с завершёнными зависимостями; `REL-004` нельзя выполнять до полного release acceptance.

## Historical scope fence

В долгосрочный north star входили knowledge base, story production lock, .NET
solution, content compiler/CLI, pure C# runtime, SaveGameV3, Godot presentation,
first-person controller, compact world scenes, UI/audio/input, Blender asset
pipeline, Chapters 1–5, desktop exports, verification и web retirement.

Для текущего goal это только сохранённая справка. В текущий delivery входят
только Act 1, его пять компактных зон, 16 authored beats, shared runtime state,
первое лицо, old PC/journal/dialogue/vocabulary, сохранение/ввод и demo
cliffhanger. Acts 2–5 production, полный playthrough, release-host acceptance и
web retirement находятся за scope fence.

## BaselineReadSetHint

- `AGENTS.md`
- `docs/urman_knowledge_base/README.md`
- `decision_log.md`, `open_questions.md`, `weak_points.md`
- `chapter1_mvp_campaign.md`, `narrative.md`, `design_style.md`, `technical_architecture.md`
- `docs/modular_migration/80_FINAL_VERIFICATION_KB_HANDOFF.md`
- `docs/aegis/baseline/2026-05-18-initial-baseline.md`
- `docs/aegis/work/2026-07-17-modular-migration/**`

## BaselineUsageDraft

- Required refs: перечислены выше.
- Acknowledged before first write: AGENTS, KB README, decision/open/weak, narrative, style/architecture diffs, MM-80 handoff, Aegis governance and previous work record.
- Missing refs: none for baseline slice.
- Decision: continue.

## TaskStartSnapshot

- Worktree: `/Users/unterlantas/Documents/GitHub/URMAN`
- Branch: `main`; upstream divergence `0/0`.
- Staged paths: none.
- Pre-existing modified paths: 15 files under `docs/urman_knowledge_base/`, all preserved as task context.
- Active merge/cherry-pick/rebase: none detected.
- Worktrees: only the shared root checkout.
- Installed tools: Node 25.2.1 and npm 11.6.2; `dotnet`, Godot and Blender absent.

## ImpactStatementDraft

Миграция меняет engine, language runtime, persistence schema, presentation owner и asset pipeline. Главные риски: второй runtime owner, неподтверждённая parity, преждевременное удаление web-кода, пустой дорогой 3D, культурная ошибка и невозможность воспроизвести toolchain. Контроль: read-only oracle, golden fixtures, owner boundaries, compact zones, narrative/art gates, pinned tools и delete-first только после полного acceptance.

## Change Necessity

- User-visible need: полноценная first-person 3D-игра вместо веб-прототипа.
- No-change option: не достигает выбранного engine/style/world experience.
- Why code change is necessary: текущий production boot, rendering, persistence и toolchain принадлежат TypeScript/Vite/Three.js.
- Minimum boundary: отдельный C#/.NET контур с portable data boundary и без runtime bridge; web-owner не удаляется до parity.
- Decision: code-change.

## Complexity Budget

- Artifact class: architecture/source/process.
- Target artifacts: новые owner-модули вместо роста `Game.ts` и browser composition root.
- Current pressure: миграция затрагивает cross-module contracts и persistent snapshot boundary.
- Projected pressure: at-risk при смешивании Godot types с core/content или при временном JS bridge.
- Budget result: within-budget только при отдельных `Core`, `Content`, `Godot`, `ContentCli` owners.
- Planned governance: package boundaries, architecture tests, per-slice checkpoints и explicit retirement gate.

## Execution Readiness View

- Intent Lock: настоящая first-person 3D-игра, не 3D-оболочка над плоским route runtime.
- Scope Fence: текущий slice — Act 1 atmospheric demo; полный перенос остаётся long-term deferred contour.
- Baseline Lock: Chapter 1 content 68/68, runtime 102/102, architecture 2/2, E2E 10/10, build 56 modules.
- Owner Constraints: portable content owns narrative; kernel owns progression; Godot owns presentation only.
- Compatibility Boundary: никаких V2/import/localStorage readers в новой игре.
- Retirement Boundary: web runtime остаётся oracle до полного acceptance, затем удаляется целиком без fallback.
- Test Obligations: golden parity, pure .NET tests, Godot headless, cross-platform export, full playthrough.
- Review Gates: content parity, runtime parity, Chapter 1 vertical slice, acts 2–5 locks, art lock, cutover.
- Drift Rule: любой второй owner или engine-specific dependency в Core возвращает slice на архитектурный review.
