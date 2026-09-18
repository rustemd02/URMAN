---
task_id: MM-51
title: Модульные адаптеры активных сцен и UI
status: complete
kind: migration
depends_on: [MM-45]
blocks: [MM-54]
parallel_group: migration-leaves
owner_scope:
  - src/scenes/**
  - src/ui/**
  - src/runtime/modules/active/**
  - tests/runtime/modules/active/**
forbidden_scope:
  - src/scenes/ComputerScene.ts
  - src/scenes/ChapterScene.ts
  - src/scenes/MainMenuScene.ts
  - src/scenes/ZiratMiniGame.ts
  - src/game/Game.ts
  - src/game/GameState.ts
  - src/game/SceneManager.ts
  - src/main.ts
  - src/os/**
  - src/MainMap/**
  - content/**
deliverables:
  - Generic scene/dialogue/route providers
  - Тонкие UI adapters через RuntimeContext
  - Декомпозиция RouteNavigationScene
verification:
  - npm run test:runtime
  - npm run test:architecture
  - npm run build
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-51 — Active runtime migration

## Цель

Перевести активные сцены и UI на registries, resolvers и immutable `RuntimeContext`, не подключая их в composition root.

Четыре точечных исключения явно не входят в широкую маску `src/scenes/**`:

- `src/scenes/ComputerScene.ts`: единственный owner файла и old PC adapter — `MM-52`;
- `src/scenes/ChapterScene.ts`: единственный owner удаления файла вместе с `chapter1` token/callers — `MM-54`;
- `src/scenes/MainMenuScene.ts`: legacy production caller переключает только `MM-54`; `MM-51` создаёт replacement menu provider в `src/runtime/modules/active/**`, не меняя production entrypoint.
- `src/scenes/ZiratMiniGame.ts`: единственный owner безусловного удаления legacy файла вместе с `SceneManager` caller — `MM-54`; `MM-51` создаёт только независимый generic zirat provider.

## Модули

Создать host-specific providers для:

- static scene и scripted scene;
- route navigation и external handoff;
- dialogue/reaction UI;
- journal, vocabulary, HUD и inventory presentation;
- audio-first forest finale;
- house, mosque, forest, generic zirat provider, а также generic intro/chapter/menu presentation provider; legacy `ZiratMiniGame.ts`, `ChapterScene.ts` и `MainMenuScene.ts` не редактируются.

Provider получает definition/config и не знает конкретный quest/campaign ID. Текст и assets приходят через resolvers. Все действия UI отправляют typed commands; прямые изменения `game.state`, `window.URMAN` и `localStorage` запрещены.

## Декомпозиция route

Разделить `RouteNavigationScene.ts` на узкие владельцы: renderer, input/controller, graph navigation, hotspot/handoff adapter и journal projection. Requirements, labels, effects, asset paths и Ринат-specific поведение переезжают в content definitions или capability config.

## Lifecycle

Каждый scene/provider реализует create/init/render/handle/dispose через registry contract. Dispose удаляет DOM listeners, timers, media и subscriptions. Переход сцены — command/effect, а не вызов глобального manager.

Для `targetDialogueId` static/presentation provider resolves the portable dialogue start node, puts its entry conditions/effects into the originating atomic scene command, and only after commit calls typed host handoff with `entryAlreadyCommitted: true`. DialogueReactionSession accepts that flag and never re-dispatches start-node effects. No provider may branch on a Chapter 1/campaign ID.

## Проверки

- generic providers рендерят две synthetic campaigns из `MM-45`;
- swap role/text/asset не требует изменения provider;
- destroy/dispose оставляет zero listeners/timers;
- static scan не находит narrative IDs и прямых `/assets/` в generic provider;
- текущие production entrypoints пока не переключаются; `chapter1` token, `ChapterScene.ts` и его callers остаются без изменений для атомарного `MM-54` cutover.

Handoff перечисляет старые scene callers, которые должен отключить `MM-54`, и файлы, безопасные для удаления после cutover.

## Handoff

```text
Task: MM-51
Status: complete
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: src/runtime/modules/active/**; tests/runtime/modules/active/active-providers.test.mjs
Behavior / contracts delivered: Generic static/scripted/presentation/capability/route descriptors; resolver-only immutable scene, dialogue and UI models; decomposed route renderer/input/graph/handoff/journal; atomic target-entry preflight; `entryAlreadyCommitted` handoff; data-driven forest audio/accessibility; exact capability definition/provider/config preflight; serialized lifecycle and terminal post-dispose API.
Verification commands: node --test tests/runtime/modules/active/active-providers.test.mjs; npm run test:runtime; npm run test:content; npm run content:types:check; npm run test:architecture; npm run build; git diff --check
Verification results: PASS — focused 19/19; runtime 84/84; content 63/63; architecture 2/2; generated types, production build and diff check pass. Independent quality PASS; initial final SPEC review found capability-resolution and post-dispose P1s, both repaired; repeat independent SPEC PASS.
Retired owners / remaining callers: No legacy production owner retired. MM-54 navigation host must forward `entryAlreadyCommitted` to target providers; only MM-54 can switch callers and remove legacy RouteNavigationScene/ChapterScene/MainMenuScene/Zirat registrations atomically.
Risks or drift: No src/scenes, src/ui, Game, GameState, SceneManager, main, OS, MainMap or content mutation in this packet. New adapters have no campaign IDs, raw asset paths, browser globals, storage or state writers.
Required next task: MM-52
```
