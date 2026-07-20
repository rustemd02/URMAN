# Task Intent Draft — модульная миграция УРМАН

Date: 2026-07-17
Goal status: active
Branch: `codex/modular-migration`
ArchitectureReviewRequired: yes

## Запрошенный результат

Полностью выполнить `docs/modular_migration/MM-00–MM-80`: оркестрировать свежих implementer- и reviewer-субагентов по графу зависимостей, интегрировать portable content, transactional runtime kernel, capability providers, snapshot/cutover и retirement старых владельцев, затем провести независимую итоговую проверку.

## Успех и стоп-условие

Успех подтверждают все acceptance criteria `MM-80`, зелёные content/unit/architecture/e2e/build checks, отсутствие открытых P0/P1 review findings и синхронизированная knowledge base. Стоп-состояния: `done`, `blocked`, `needs-verification`, `scope-exceeded`. Статус MVP этим workstream не присваивается.

## Scope fence

Входит: весь runtime и legacy, перечисленные в task-пакетах; схемы, compiler, kernel, quest/capability/world services, resolvers, snapshot, current campaign data, active scenes, old PC/DedOS, MainMap quarantine, composition cutover, Content Lab, persistence retirement, tests и документация.

Не входит: live hot-swap, remote mods, full ECS, generic scripting engine, semver solver, Unity importer, новые канонические сюжетные ветки и production-реализация 12 stress mechanics.

Коммиты, push, merge и PR не разрешены текущим поручением и не входят в workstream.

## TDD Route

- Mode: off
- Decision: skipped
- Strict authority: not applicable
- Test posture: минимальная реализация плюс сфокусированная post-change regression
- Reason: пользователь не запрашивал строгий TDD; каждый task-пакет уже задаёт пропорциональную проверку
- Verification: task-local tests, два review-stage и итоговый MM-80 gate

## BaselineReadSetHint

- `AGENTS.md`
- `URMAN_Codex_Context.md`, разделы 1–17 при каноническом решении
- `docs/urman_knowledge_base/README.md`
- `docs/urman_knowledge_base/decision_log.md`
- `docs/urman_knowledge_base/open_questions.md`
- `docs/urman_knowledge_base/weak_points.md`
- `docs/urman_knowledge_base/chapter1_mvp_campaign.md`
- `docs/urman_knowledge_base/technical_architecture.md`
- `docs/aegis/baseline/2026-05-18-initial-baseline.md`
- task-файл и handoff всех его dependencies

## BaselineUsageDraft

- Required baseline refs: перечислены выше
- Acknowledged before execution: AGENTS, KB README, decision log, open questions, weak points, technical architecture, Chapter 1 reveal invariant, Aegis initial baseline
- Cited in plan refs: `docs/modular_migration/00_ORCHESTRATOR_README.md` и task-пакеты
- Missing refs: none
- Decision: continue

## ImpactStatementDraft

Изменение заменяет владельцев content/state/persistence и затрагивает весь runtime. Главные риски: второй источник истины, преждевременный cutover, nondeterministic save/restore, потеря текущего critical path и framework scope creep. Контроль: dependency gates, один implementer за раз, spec review до quality review, static architecture checks и явный retirement.

## Execution Readiness View

- Intent Lock: модульность ради быстрой замены арок/квестов/персонажей, не ради универсального движка
- Scope Fence: только MM-00–MM-80 и их acceptance criteria
- Baseline Lock: текущие `validate:content` и `build` зелёные; Chapter 1 reveal timing сохраняется
- Approved Behavior: data-only campaigns, kernel-only mutations, isolated custom capabilities
- Owner / Contract Constraints: frozen MM-01; MM-54 единственный composition-root owner
- Compatibility Boundary: clean reset pre-MVP saves; username/preferences сохраняются
- Retirement Boundary: один canonical owner, без permanent fallback
- Task Batches: freeze → baseline/schema/compiler/kernel → services/snapshot/canary → four migrations → cutover/closure → lab/retirement → verification
- Test Obligations: task-local tests, spec review, quality review, final independent review
- Review Gates: MM-03, MM-45, MM-55, MM-80
- Drift / Rewind Rules: finding возвращается владельцу пакета; downstream gate повторяется
- Evidence Required Before Completion: commands, exit status, static scans, review findings closed, KB updated
- Advisory Boundary: Aegis workflow не заменяет user authority или product MVP gate

## Workspace decision

Project-local worktree directory отсутствует, `.gitignore` не содержит worktree path, а исходный task package незакоммичен и не даёт commit authority. Чтобы не stash-ить и не коммитить документы, работа идёт на выделенной ветке `codex/modular-migration` в текущем shared workspace. Implementer-субагенты работают строго последовательно; reviewer не редактирует дерево одновременно с implementer.
