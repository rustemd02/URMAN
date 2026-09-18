---
task_id: MM-50
title: Перенос текущей кампании в portable content
status: complete
kind: migration
depends_on: [MM-45]
blocks: [MM-54]
parallel_group: migration-leaves
owner_scope:
  - content/modules/urman-core/**
  - content/modules/urman-chapter1/**
  - tests/content/urman-chapter1/**
forbidden_scope:
  - content/modules/urman-oldpc/**
  - content/campaigns/**
  - src/**
  - public/assets/**
deliverables:
  - Portable core и Chapter 1 content modules
  - Canonical ID migration без aliases
  - Narrative invariant tests
verification:
  - npm run content:check -- --module urman.chapter1
  - npm run test:content
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-50 — Campaign data migration

## Цель

Перенести текущие inline/TypeScript данные кампании в engine-neutral JSON/Markdown. Пакет не создаёт финальный `CampaignManifest`: композиция core, Chapter 1, old PC и runtime providers принадлежит `MM-54`.

## Источники

Прочитать `chapter1_mvp_campaign.md`, `canon.md`, `characters.md`, `language_learning.md`, `route_navigation_graph.md`, `decision_log.md` и handoff `MM-10`. При конфликте использовать зафиксированный приоритет канона; новый конфликт записать и остановить соответствующий entity, а не выбирать молча.

## Перенос

Создать manifests и data files для:

- characters и role slots;
- locations/scenes и route definitions;
- beats, quests, objectives, entrypoints и handoffs;
- dialogues, reactions и NPC states;
- knowledge, evidence, documents и vocabulary;
- pressure rules и narrative invariants;
- logical asset, audio и text refs.

Модуль `urman.core` содержит только переиспользуемые доменные определения. Всё, что связано с маршрутом Марата или порядком Chapter 1, остаётся в `urman.chapter1`.

## Обязательные инварианты

- Игра начинается у приезда в Кырлай, а не отдельной сценой КФУ.
- Кара-Урман — урочище, не название деревни.
- Ринат не превращается в generic antagonist.
- Мифологические существа не становятся мобами.
- `clue_voice_answer_is_dangerous_hint` остаётся hypothesis до финала.
- `clue_do_not_answer_rule` появляется confirmed только после реплики «Не отвечай».
- Татарские слова имеют состояния unknown/guessed/confirmed и ведут к повторному чтению улик.

## Запреты

Не копировать TS-массивы как второй источник истины и не добавлять aliases для старых ID. Старые `src/data/**` удаляет `MM-54` после переключения всех consumers. Assets не перемещаются: data содержит только logical refs.

## Handoff

Передать `MM-54` список migrated entities, unresolved external refs на old PC/runtime capabilities, canonical ID mapping и результаты invariant tests.

## Handoff

```text
Task: MM-50
Status: complete
Worktree / branch: shared workspace / codex/modular-migration
Changed paths: content/modules/urman-core/module.json; content/modules/urman-chapter1/**; tests/content/urman-chapter1/chapter1-data.test.mjs
Behavior / contracts delivered: `urman.core` is a schema-valid no-data future shell because it has fewer than two current production consumers; `urman.chapter1` owns canonical cast, quests, dialogues, logical assets, route/evidence presentations, knowledge and vocabulary. The canonical route is arrival → house → crossroad → FAP waiting → FAP document desk → official → register → Rinat dialogue → saved message → Татарвики first read → Rinat-gated reread → Mansur sketch → zirat → forest. The register-to-Rinat edge is data-only `targetDialogueId`; its generic atomic handoff sets `alerted` before saved-message access. Only the reread confirms tt_tavysh/tt_javap, and only the forest interruption confirms the «Не отвечай» rule.
Verification commands: npm run content:check -- --module urman.chapter1; node --test tests/content/urman-chapter1/chapter1-data.test.mjs; npm run content:check; npm run test:content; npm run content:types:check; npm run test:runtime; npm run test:architecture; npm run build; git diff --check
Verification results: PASS — module check 1/0; chapter data 5/5; content 63/63; runtime 65/65; architecture 2/2; generated types, production build and diff check pass. Independent SPEC PASS and QUALITY PASS; final independent SPEC PASS after reviewer-incident reconciliation.
Retired owners / remaining callers: No legacy data owner retired before MM-54. `urman.oldpc` must retain full Markdown/search for doc_marat_official_death_notice, rec_marat_case_register_conflict, msg_marat_saved_last_normal, tw_shurale_urman_boundary and doc_kara_urman_edge_sketch; it maps them one-way to the named Chapter 1 evidence presentations without copying bodies or effects.
Risks or drift: A quality reviewer made a scoped FAP edit despite a read-only instruction. Root reconciled it to MM-10 row 197, kept the canonical FAP document-desk scene/asset, reran full verification and obtained quality reconfirmation. No composition, legacy source, old-PC content, campaign manifest, aliases or direct asset paths changed.
Required next task: MM-51; then MM-52, MM-53, MM-54.
```
