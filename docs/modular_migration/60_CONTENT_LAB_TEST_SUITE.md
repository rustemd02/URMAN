---
task_id: MM-60
title: Dev-only Content Lab и интеграционная матрица
status: complete
kind: tooling-and-tests
depends_on: [MM-55]
blocks: [MM-70]
parallel_group: null
owner_scope:
  - src/lab/**
  - tests/e2e/**
  - tests/fixtures/content/stress/**
  - scripts/playtest-mvp-smoke.mjs
  - vite.config.ts
  - package.json
forbidden_scope:
  - src/main.ts
  - src/runtime/kernel/**
  - content/modules/urman-chapter1/**
deliverables:
  - Content Lab только для Vite serve
  - Двенадцать stress fixtures с mock providers
  - Zero-dependency Chrome CDP driver и Node test e2e smoke
verification:
  - npm run content:check
  - npm run test:unit
  - npm run test:architecture
  - npm run test:e2e
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-60 — Content Lab и tests

## Content Lab

Создать dev-only интерфейс, открываемый через `?lab=1` только при Vite `serve`. `viteContentLabPlugin` перехватывает запрос в dev и загружает `src/lab/main.ts`; production build не импортирует lab и не создаёт lab entry/chunk.

Lab позволяет:

- выбрать campaign/module/quest/scene/preset и seed;
- увидеть dependency DAG, diagnostics, role bindings и required capabilities;
- запустить новый изолированный run;
- перезапустить со swap персонажа, диалога, ассета или порядка независимых квестов;
- просмотреть state/event log и capability cleanup.

Lab использует отдельный storage namespace и никогда не читает/пишет production save.

## Stress suite

Закодировать все 12 `MM-02` как content fixtures. Сложные providers заменяются headless mocks с теми же manifest/config/event/outcome contracts. Каждый fixture имеет отдельные JSON config/event/outcome данные и schema fragments, а objective ссылается ровно на `outcomeSchemaRef` своего provider; invalid config обязан отклоняться до создания capability session. Проверить компиляцию, lifecycle, save/restore, cancel cleanup, accessibility outcome и resource conflict; графику, DSP, stealth AI и симуляцию воды не реализовывать.

## E2E runner

Перевести smoke на встроенный `node:test`; `MM-60` добавляет только script `test:e2e = node --test 'tests/e2e/**/*.test.mjs'` и не переопределяет созданные `MM-30` scripts `test:content`, `test:runtime`, `test:architecture` или `test:unit`. Category directory не используется как runner contract. Сторонние test framework dependencies не устанавливаются.

`MM-60` владеет `tests/e2e/support/chrome-cdp-driver.mjs`. Driver использует только Node built-ins: `node:child_process` для Chrome process, `fetch` для CDP discovery и global `WebSocket` для protocol messages. Никаких Playwright/Puppeteer imports, package dependencies или захардкоженного единственного host path.

Browser executable ищется в стабильном порядке:

1. явный `CHROME_PATH`;
2. macOS: Google Chrome, затем Chromium в стандартных `/Applications/...` paths;
3. Linux: `google-chrome`, `google-chrome-stable`, `chromium`, `chromium-browser` в стандартных `/usr/bin`/`/usr/local/bin` paths;
4. Windows: Chrome/Chromium под `PROGRAMFILES`, `PROGRAMFILES(X86)` и `LOCALAPPDATA`.

Driver запускает отдельный headless process с временным user-data-dir, `--remote-debugging-port=0`, `--no-first-run` и `--no-default-browser-check`, читает выданный DevTools endpoint и подключается по CDP. После каждого test process завершается, WebSocket закрывается, временный профиль удаляется. Если executable не найден или CDP не поднялся, test падает с actionable diagnostic: проверенные paths, текущая platform и инструкция задать `CHROME_PATH`; skip/pass в этом случае запрещён.

Driver экспортирует deterministic async API `launch`, `goto`, `evaluate`, `click`, `waitFor`, `text`, `focus`, `screenshot`, `close`. `click`/`focus` принимают CSS selector; `waitFor` использует явный timeout и polling interval; `screenshot` возвращает PNG bytes. Внутри CDP request IDs монотонны, responses сопоставляются по ID, page/runtime events обрабатываются ordered. Тесты общаются с приложением через публичный test harness, включаемый только в test mode, а не через production `window.URMAN`.

`package.json` получает ровно:

```text
test:e2e node --test 'tests/e2e/**/*.test.mjs'
```

Обязательные e2e:

- default arrival-to-cliffhanger path и save/load;
- `clue_do_not_answer_rule` отсутствует до финала и confirmed после cut;
- campaign swap создаёт новый run;
- Content Lab не попадает в production preview;
- keyboard/focus/ARIA для sample capability и основных Lab controls.

## Handoff

```text
Task: MM-60
Status: complete
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: src/lab/viteContentLabPlugin.ts; src/lab/main.ts; src/lab/e2e-harness.ts; src/lab/{node-runtime,virtual-content-lab-catalog}.d.ts; tests/fixtures/content/stress/**; tests/e2e/**; scripts/playtest-mvp-smoke.mjs; vite.config.ts; package.json; docs/modular_migration/60_CONTENT_LAB_TEST_SUITE.md
Behavior / contracts delivered: `?lab=1` is Vite-serve-only and loads a dev virtual catalog compiled from all authored campaigns. A Lab swap is not a post-compile mutation: the plugin copies authored module data to a temporary directory, changes one data-only concern, calls the existing compiler, then exposes only that immutable compiler result. A new run therefore receives the real separately compiled pack and its SHA-256 campaign fingerprint; a campaign without a launchable compiled variant is inspectable but cannot start. Campaign/swap/seed configure the isolated run; the capability preset is passed as observable old-PC input. Module/quest/scene are explicitly frozen-pack inspection selections: their values are resolved against the selected compiled pack, rendered in the inspection output and recorded with the run, but never mutate campaign data or claim to change its lock. An old run is replaced only after its capability cleanup succeeds; a cleanup failure retains the active run and blocks replacement. The Lab offers DAG/diagnostics/roles/capabilities, character/dialogue/asset/quest-order variants, state/event/cleanup output and keyboard/ARIA-observable controls. Sample capability output comes from the active old-PC provider handle and its declared accessible outcome, rather than a literal success string. The production catalog and production bundle never import Lab. Twelve MM-02 Proposal fixtures compile as test-only content and use exact headless mock capability contracts: each quest objective uses the matching provider `outcomeSchemaRef`, each has JSON config/event/outcome data and a distinct config/event/outcome schema fragment, rejects invalid config before a session starts, and verifies declared event/outcome, quest request/cancel cleanup, provider lifecycle, accessibility-equivalent outcome, V2 snapshot/restore and typed resource conflict. `test:e2e` consumes `DEFAULT_MVP_JOURNEY`, its dialogue boundary and `FINAL_RULE_ID` from `scripts/playtest-mvp-smoke.mjs`, so the smoke path has one source. The suite uses only `node:test`, Vite processes and a zero-dependency CDP driver; its test-only harness is gated by `URMAN_E2E=1` and is absent from production. The driver checks executable permission with `X_OK` where applicable, rejects unavailable executable/spawn/CDP states through one diagnostic naming platform, every checked path and `CHROME_PATH`, rejects failed focus, and waits for process exit after `SIGKILL` before deleting a Chrome profile or Vite test-server resources.
Verification commands: node --test tests/e2e/stress-fixtures.test.mjs; npm run content:check; npm run test:unit; npm run test:architecture; npm run test:e2e; npm run build; git diff --check; rg -n "src/lab|Content Lab|urman-content-lab|virtual:urman-content-lab" dist
Verification results: PASS after SPEC and quality remediation — focused stress contracts/lifecycle 2/2 plus failed-dispose retention 1/1; content audit 3 modules/3 campaigns; content 68/68; runtime 102/102; architecture 2/2; static gate 86 files; closure integration 4/4; real local Vite+Chrome CDP e2e 8/8 (non-executable Chrome diagnostic, imported arrival-to-cliffhanger/save-load/final-rule path, separately compiled campaign/data swap with real SHA-256 lock, functional inspection selections and capability preset input, failed-dispose retention, positive and negative focus, production preview exclusion); production build 55 modules; diff check clean; no Lab marker or virtual entry in dist. The default sandbox blocks local listener creation with EPERM, so the required CDP run was repeated with approved loopback-process permission and passed; unavailable Chrome/CDP remains a hard actionable failure, never a skip.
Retired owners / remaining callers: The old direct Playwright smoke runner is replaced by the declarative journey constants plus node:test/CDP suite. No production code imports `src/lab/**`; the `window.__URMAN_E2E__` harness is served only by the Vite test route and is not a production global.
Risks or drift: Shared worktree contains unrelated concurrent migration changes and was not cleaned or altered. `graphify update .` remains for the orchestrator's final graph pass because this task's owner scope excludes graph artifacts; preceding MM-54/MM-55 records the host watch permission limitation.
Required next task: MM-70 legacy persistence retirement/reset, then MM-80 final verification and knowledge-base handoff.
```
