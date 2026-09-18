---
task_id: MM-55
title: Whole-pack closure gate
status: complete
kind: integration-gate
depends_on: [MM-54]
blocks: [MM-60]
parallel_group: null
owner_scope:
  - tests/integration/content-closure/**
  - scripts/check-runtime-architecture.mjs
  - docs/modular_migration/55_WHOLE_PACK_CLOSURE_GATE.md
forbidden_scope:
  - content/modules/**
  - src/runtime/kernel/**
  - src/runtime/modules/**
deliverables:
  - Полная closure-проверка всех campaigns
  - Static architecture gate
  - Исправления возвращены владельцам, а не внесены в gate
verification:
  - node scripts/check-runtime-architecture.mjs
  - node --test 'tests/integration/content-closure/**/*.test.mjs'
  - npm run content:check
  - npm run test:architecture
  - npm run test:runtime
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-55 — Whole-pack closure gate

## Цель

Проверить интегрированный результат как единый продукт до добавления Content Lab. Gate не чинит leaf-модули: ошибку возвращают владельцу соответствующего пакета, затем повторяют интеграцию `MM-54` и closure.

## Closure checks

- Все campaign/module/file/entity/role/capability/asset/text refs разрешаются.
- Все required entrypoints достижимы; optional content явно помечен.
- Нет dependency cycles, duplicate IDs и implicit overrides.
- Capability exact versions и config schemas совпадают.
- Narrative reveal timing соблюдён.
- Все logical assets имеют runtime resolution и доступную альтернативу, где она обязательна.
- Production imports не включают lab и legacy MainMap.

## Static architecture gate

Скрипт проверяет generic kernel/renderers на narrative IDs, `/assets/`, raw `localStorage`, `window.URMAN`, hardcoded scene switch и imports из конкретной campaign. Также проверяется, что владельцы со статусом `deleted` из `MM-10` не имеют callers.

## Результат

Gate выпускает `MM-60` только при полном pass. Список warnings без владельца и срока считается failure, если warning относится к source of truth, persistence или production import graph.

## Реализация gate

`tests/integration/content-closure/whole-pack-closure.test.mjs` discovers every authored `campaign.json`, resolves exactly its declared module manifests, and compiles each pack independently. It verifies the resolved campaign lock, entrypoint, role bindings, capability exact versions, narrative/reachability compiler invariants and every logical asset/text through `AssetResolver`, `TextResolver` and `validateManifestClosure`.

`node scripts/check-runtime-architecture.mjs` is a separate production-source gate. It scans every non-dev-only `src/**` source file and rejects:

- imports of Content Lab (including `src/lab/**`) or dev-only MainMap;
- raw `localStorage`, `window.URMAN` and direct `/assets/` paths;
- MM-10 retired-owner callers and any deleted owner file still present;
- every namespaced narrative `urman.chapterN:<kind>/...`/legacy-MainMap ID, regardless of kind, and hardcoded scene comparisons or arbitrary string cases in a scene-ID switch, including common scene-ID fields on any dotted object path. Explicit `:capability/...` protocol IDs remain allowed composition contracts.

The checker also asserts absence of every production-retired owner listed by MM-10/MM-54, including `EventEmitter`, `MainMenuScene`, `AudioSystem`, legacy HUD/inventory/debug UI and the bounded deleted DedOS chat paths. `src/os/**` and `ComputerScene.ts` are intentionally not absence-gated: MM-52 has already converted them to the current descriptor-driven old-PC adapter; they remain valid only through explicit module/host registration, not as legacy state or narrative owners.

The checker intentionally does not scan `scripts/**`: legacy smoke replacement remains owned by MM-60/MM-70. `src/lab/**`, `src/MainMap/**` and `src/runtime/modules/legacy-mainmap/**` are excluded only as explicitly dev-only implementation paths; any production-source import into them is a failure.

## Handoff

```text
Task: MM-55
Status: complete
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: scripts/check-runtime-architecture.mjs; tests/integration/content-closure/whole-pack-closure.test.mjs; docs/modular_migration/55_WHOLE_PACK_CLOSURE_GATE.md
Behavior / contracts delivered: Every authored campaign is compiled from exactly its declared modules and checked through the compiled closure, registries and logical asset/text resolvers. A production-source gate rejects dev/lab imports, raw browser/storage escape hatches, direct asset paths, every non-capability narrative ID kind or hardcoded scene comparison/switch in shared code, and every MM-10/MM-54 retired production owner/caller (including AudioSystem). The gate has regressions for src/lab, all extended narrative-ID kinds, capability protocol allowance and scene-control forms.
Verification commands: node scripts/check-runtime-architecture.mjs; node --test 'tests/integration/content-closure/**/*.test.mjs'; npm run content:check; npm run test:architecture; npm run test:runtime; npm run build; git diff --check; graphify update .
Verification results: PASS after independent P1 remediation and the Content Lab-boundary amendment — static gate checked 86 production source files; closure integration 4/4, including regressions for a dev-only `src/lab/**` implementation, forbidden production imports of Lab/MainMap, location/route-node/item/beat/document IDs, allowed capability protocol IDs, bare and dotted scene comparisons/switches, type guards and AudioSystem retirement; content 3 modules/3 campaigns; architecture 2/2; runtime 102/102; production build (55 modules) and diff check pass. graphify re-extraction was started but its host watch rebuild ended Operation not permitted; no source verification was affected.
Retired owners / remaining callers: Gate requires absence/caller-free status for the MM-10/MM-54 deleted source owners, including EventEmitter, AudioSystem, menu/UI shells and bounded DedOS chat retirement. Current descriptor-driven src/os/** and ComputerScene adapter paths are explicitly retained, not legacy fallback owners.
Risks or drift: scripts/** is intentionally outside this static scan until MM-60/MM-70 replace the legacy smoke/validator tooling. The graphify host watch permission needs host remediation before graph artifacts can be refreshed.
Required next task: MM-60 Content Lab and integration suite, followed by MM-70 persistence retirement.
```

## Independent acceptance — 2026-07-18

Result: `SPEC PASS` and `QUALITY PASS`; no open P0/P1/P2.

The first spec review found incomplete narrative-kind, retired-owner and scene-control coverage. MM-55 repaired only its checker/tests: all namespaced campaign kinds except the explicit capability protocol composition contract are now checked, `AudioSystem` joins the deleted owner set, and scene literal comparisons/switches include dotted identifiers without rejecting a typed `typeof` guard. Root independently reran direct checker, closure integration, content/runtime/architecture suites, build and `git diff --check`; all passed.

## Amendment — 2026-07-18: explicit Content Lab implementation boundary

`src/lab/**` is an implementation-only dev path, matching the pre-existing `src/MainMap/**` and `src/runtime/modules/legacy-mainmap/**` treatment. It is excluded from the production-source scan so that its dev entrypoint is not judged as a production import. This exemption does not weaken the import rule: a non-dev `src/**` file importing either `src/lab/**` or a legacy MainMap path still receives `ProductionDevImport`.

Evidence: `node scripts/check-runtime-architecture.mjs` passed with 86 production source files; closure integration passed 4/4, including a temporary `src/lab/main.mjs` exclusion and synthetic production imports of both Lab and MainMap. `npm run content:check`, `npm run test:architecture`, `npm run test:runtime` (102/102), `npm run build` (55 modules) and `git diff --check` also passed.

## MM-55P implementation handoff — 2026-07-18

Status: implementation complete; independent SPEC/QUALITY acceptance pending.

The scanner names `src/runtime/persistence/production-persistence-gateway.mjs` as the sole injected browser-storage boundary. It continues to reject raw `localStorage` in every other production file, including `Game` and `main`, and still rejects `window.URMAN` everywhere. `createBrowserProductionPersistenceGateway` may be imported or called only from `src/main.ts`; its definition remains in the named gateway boundary. The focused regression creates a temporary production tree and proves the named gateway is allowed while sibling production files are diagnosed for raw storage or an unauthorized factory caller.

Verification: static gate 88 production source files; architecture 2/2; focused persistence/static regression 8/8; real Vite+Chrome E2E 10/10; production build 56 modules; clean diff. This amendment changes no kernel/content/runtime owner and requires the coordinated independent review before MM-80.

## MM-55P independent acceptance — 2026-07-18

Result: `QUALITY PASS`; no open P0/P1/P2.

The review verified that raw browser storage is limited to the named gateway boundary and that `createBrowserProductionPersistenceGateway` can be called only by `src/main.ts`. A temporary production-tree regression rejects an otherwise valid sibling factory caller, so this remains a future-proof source-closure rule rather than a one-time `rg` observation.

## Legacy web-corpus closure amendment — 2026-07-18

Final MM-80 review found `src/os/data/web_content.json`: an unreachable legacy HTML/JSON narrative corpus outside the portable module set. MM-52 removed the exact internal code-retirement path after a bounded no-consumer scan; it is not a migrated campaign asset, user data or compatibility boundary.

`RETIRED_OWNER_PATHS` now includes this exact filename. The existing temporary production-tree regression deliberately recreates `src/os/data/web_content.json` and requires `RetiredOwnerPresent` for that exact `sourcePath`, so a future hidden narrative owner cannot return silently. This is an amendment receipt only; it does not reopen production content, kernel or runtime ownership.
