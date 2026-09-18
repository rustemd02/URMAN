# Evidence Bundle Draft — модульная миграция УРМАН

## Baseline

| Evidence | Result | Scope |
|---|---|---|
| `npm run validate:content` | pass | 10 old PC files, 20 shared keys, 32 route nodes, 41 route assets, 11 handoffs |
| `npm run build` | pass | TypeScript + Vite production build, 72 modules |
| task package structural check | pass | 22 Markdown files, 21 unique task IDs, reciprocal acyclic dependencies |
| source diff before execution | empty | `src/`, `content/`, `public/`, `package.json` unchanged by planning slice |

## Implementation evidence

### MM-01–MM-03 — architecture freeze

- Changed paths: `docs/modular_migration/01_ARCHITECTURE_CONTRACT.md`, `03_ARCHITECTURE_FREEZE_GATE.md` и downstream task contracts `20`, `30`, `31`, `42`, `51`, `52`, `60`.
- Frozen seams: fatal `ContentCompilationResult`, immutable `CompiledContentPack`, `ContentRegistry.fromPack`, occurrence fingerprint/result ledger, stable `capabilityInstanceId`, staged `GameSnapshotV2` restore и owner-scoped cleanup.
- Stress evidence: 12/12 fixtures прошли полный путь definition → pack → registries → transaction/events → snapshot/restore → cleanup/dispose.
- Spec review: initial 5 P1; все исправлены; re-review `SPEC PASS`.
- Quality review: initial 3 P1 + 1 P2, затем 1 P1; все исправлены; final `QUALITY PASS`.
- Verification: frozen/status scans pass, fixture counts `12/12`, `git diff --check` exit 0, trailing-whitespace scans pass.
- Host note: новый третий child thread был недоступен из-за thread limit; quality review выполнен отдельным quality-only turn независимого от implementer reviewer-а по указанию root.
- Drift: intent/scope/compatibility aligned; runtime и canon не менялись; decision `continue`.

### MM-10 — authority, IDs and retirement baseline

- Changed paths: `docs/modular_migration/10_AUTHORITY_IDS_RETIREMENT_BASELINE.md`, task ownership contracts `51`/`54`, and allowed KB `decision_log.md`, `open_questions.md`, `weak_points.md`.
- Delivered: complete mandatory retirement matrix; authority table; all 11 route target dispositions; no-alias canonical ID map; exact character/dialogue/asset mappings; Chapter 1 reveal invariants; exact authorized four-key reset policy.
- Retirement: internal owners are delete-first in assigned packets; `ChapterScene`, `MainMenuScene` and `ZiratMiniGame` have atomic caller/file retirement in MM-54; MM-51 owns only generic replacements.
- Reviews: initial spec and quality findings were repaired; final six-area `SPEC PASS`; final `QUALITY PASS`; no open P0/P1.
- Fresh baseline before MM-20: `npm run validate:content` exit 0; `npm run build` exit 0 (72 modules); `git diff --check` exit 0.
- Drift: no source/content/public mutation in MM-10; accepted canon unchanged; newly recorded open questions are explicitly deferred, not silent decisions.

### MM-20 — portable content schemas

- Changed paths: 17 files under `content/schemas/`, `scripts/validate-content-schemas.mjs`, `tests/content/schema/**`; only verification-contract wording changed in MM-20/MM-30/MM-60.
- Delivered: hierarchical stable schema IDs; closed portable manifests/registries/AST; exact-version contract; dedicated text schema/registry; capability state version; fatal `ContentCompilationResult` as sole diagnostics owner.
- Validator hardening: schema-position-only traversal; supported-keyword shape/cross-bound audit; canonical JSON equality; own-property-safe refs and closure; boolean schemas; union sibling/discriminator diagnostics; Unicode code-point lengths.
- Regression history: review loops found and fixed key-order equality, schema annotation traversal, malformed keyword shapes, union semantics/diagnostics, boolean-schema truthiness, prototype-chain lookup and Unicode length defects.
- Final reviews: bounded `SPEC PASS` and `QUALITY PASS`; no P0/P1/P2; forbidden runtime/package scope clean.
- Verification: `node scripts/validate-content-schemas.mjs` — 17 schemas; `node --test 'tests/content/schema/**/*.test.mjs'` — 23/23; `npm run build` — pass, 72 modules; `git diff --check` — pass.
- Drift: exact supported JSON Schema subset is deliberate; no general-purpose validator claim and no runtime cutover. Decision `continue` to MM-30.
- Bounded re-freeze after MM-30 review: added required `narrativeOrder`, closed reachable/reveal invariants, required static capability `resourceClaims`, and optional non-empty portable `bodyMarkdown`; generated types updated. Independent `AMENDMENT SPEC PASS` and `AMENDMENT QUALITY PASS`, 17 schemas, 26/26 schema tests, type drift/diff pass. No new ECS, script DSL or canon change.

### MM-30 — semantic compiler, types and Vite adapter

- Changed paths: `scripts/content/**`, `src/content/generated/**`, `src/content/web/**`, `tests/content/compiler/**`, `package.json`, `vite.config.ts`.
- Delivered: explicit campaign/module compiler with no empty pack or production discovery; schema-derived references; deterministic registries/dependency graph/canonical SHA-256; narrative/reachability/resource/capability/role preflight; normalized retained Markdown; dev-only 0/0 audit and typed ID/path selection.
- Web/types: schema-derived deterministic `content-types.ts` with non-writing drift check; Vite adapter invokes the same compiler with explicit paths and emits only a deep-frozen virtual pack. Production wiring remains MM-54.
- Review history: initial spec found missing reference, invariant, claim, role/provider, audit/CLI/Markdown gates and triggered bounded schema re-freeze; later spec/quality loops fixed schema-context declarations, effect reachability, dependency closure, typed CLI diagnostics, Markdown whitespace, nullable-pattern types and module ownership.
- Final reviews: `SPEC PASS` and `QUALITY PASS`; no P0/P1/P2; forbidden runtime/campaign paths clean.
- Verification: `content:check` reports `checked 0 modules, 0 campaigns`; `content:types:check` pass; `test:content` 57/57; legacy+new `validate:content`, build (72 modules) and diff check pass.
- Drift: no production composition cutover and no legacy parser retirement; decision `continue` to MM-31.

### MM-31 — runtime kernel and registries

- Changed paths: `src/runtime/contracts/**`, `src/runtime/kernel/**`, `src/runtime/registries/**`, `tests/runtime/kernel/**`, `tests/architecture/**`.
- Delivered: immutable `RuntimeContext`; single-writer transactional `RuntimeKernel`; immutable state selectors and read-only state/claim query; ordered disposable events; exact content/scene/condition/effect/capability registries; occurrence ledger, claims and JSON-only kernel snapshot/restore.
- Atomicity hardening: command and output boundaries are descriptor-snapshotted without invoking accessors; conflicts preflight before effect evaluation; non-JSON, bad promise/thenable, sequence-overflow and invalid rejection paths ledger a preflight failure without a partial state, claim or event commit.
- Persistence hardening: persisted JSON canonicalizes negative zero; restored ledger entries retain duplicate/conflict semantics; opaque invalid-command fallback is side-effect free and stable through JSON snapshot restore; safe-integer event-sequence capacity is preflighted.
- Registry hardening: descriptors require own data fields, exact capability versions and no implicit fallback/provider override.
- Final reviews: repeated `SPEC PASS`, bounded `MM-03 REFREEZE PASS` and final `QUALITY PASS`; no open P0/P1/P2.
- Verification: `npm run test:content` — 58/58; `npm run content:types:check` — pass; `npm run test:runtime` — 54/54; `npm run test:architecture` — 2/2; `npm run build` — pass, 72 modules; `git diff --check` — pass. `graphify update .` completed extraction but its watch rebuild reported host `Operation not permitted`; this did not alter source verification.
- Drift: additive runtime only; no production composition, campaign data, legacy owner or narrative identifier changed. Decision `continue` to MM-40/MM-41.

### MM-40 — quest, capability and deterministic world services

- Changed paths: `src/runtime/quests/**`, `src/runtime/capabilities/**`, `src/runtime/world/**` and matching focused tests/declarations.
- Delivered: portable quest instance reducer for parallel/all-any-threshold objectives, optional/failure/retry/checkpoint/cancel; exact capability host lifecycle with namespaced JSON snapshots; logical clock, owner RNG, lease/ack scheduler, custody and evidence provenance.
- Cleanup proof: quest plans release every active child `capabilityInstanceId` in the same kernel transaction. Host verifies immutable kernel claim query before stop/dispose, rejects pre-start/post-stop claims, cleans all children after callback errors and is safe against stop/dispose/disposer re-entry.
- Determinism: an unacknowledged scheduler lease is deliberately non-persistent and can be re-offered after restore; acknowledgement writes the durable fire ledger. Custody emits exclusive claims before its invalid/effect path, so competing consume gets `ResourceConflict` and no partial effect.
- Reviews: initial reviews found lifecycle, custody and scheduler P1s; all were repaired. Final `SPEC PASS`, `MM-03 REFREEZE PASS` and `QUALITY PASS`.
- Verification: full suite at this checkpoint — content 58/58, generated type drift pass, runtime 54/54, architecture 2/2, production build 72 modules and diff check pass.
- Drift: additive generic foundation only; no production composition, campaign/legacy import, narrative ID, wall clock or second source of truth. Decision `continue` to MM-41.

### MM-41 — asset, audio and text resolvers

- Changed paths: `src/runtime/resolvers/**`, `src/runtime/media/**` and `tests/runtime/resolvers/content-resolvers.test.mjs`.
- Delivered: campaign-ordered resolver catalog with duplicate logical-ID rejection; host-owned URLs for `AssetRef`; explicit-state/seed deterministic variants; immutable safe localized text and vocabulary tokens; captions, transcript and equivalent non-audio cue for non-decorative audio; generic manifest-closure diagnostics with module ID and source pointer.
- Reviews: independent `SPEC PASS` and `QUALITY PASS`; no open P0/P1/P2. The production host must provide `resolveFileUrl` only at MM-54; identity resolution is foundation/test behavior, not production wiring.
- Verification: `npm run test:content` — 58/58; `npm run test:runtime` — 59/59; `npm run test:architecture` — 2/2; `npm run content:types:check`, `npm run build` and `git diff --check` — pass.
- Drift: no production composition, campaign/scene/legacy/persistence import, raw `/assets/` path or renderer coupling. Decision `continue` to MM-42.

### MM-42 — GameSnapshotV2 and campaign-locked persistence

- Changed paths: `src/runtime/persistence/**` and `tests/runtime/persistence/game-snapshot-v2.test.mjs`.
- Delivered: exact `CampaignLock`, strict JSON-only V2 decode/encode, typed incompatibility errors, staged kernel/world/capability restoration, restore-before-start lifecycle, failed-stage cleanup and persistence of occurrence ledger, claims, custody/provenance domain data, scheduler and owner RNG streams.
- Review history: independent quality review passed. Independent spec review found one P1: an unknown nested RNG member would have been silently dropped. The codec now exact-validates `{ masterSeed, streams }`, a regression asserts the rejection, and repeat review returned `SPEC PASS`.
- Verification: `npm run test:content` — 58/58; `npm run content:types:check` — pass; `npm run test:runtime` — 65/65; `npm run test:architecture` — 2/2; `npm run build` and `git diff --check` — pass.
- Drift: additive persistence contract only; no Save v1, scene, OS, campaign or composition-root coupling. Decision `continue` to MM-45 synthetic canary.

### MM-45 — foundation canary

- Changed paths: `tests/fixtures/content/canary/**`, `tests/helpers/mock-capabilities/canary-signal-provider.mjs` and `tests/integration/foundation-canary.test.mjs`.
- Delivered: two neutral campaign variants and modules, role-bound NPC/text/asset swap, reordered independent quests, full compiler-to-dispose path, scheduled provider, custody/provenance, deterministic V2 restore and five deterministic negative preflight diagnostics.
- Reviews: independent `SPEC PASS` and `QUALITY PASS`; review repairs closed two P1 canary coverage gaps before acceptance. Fixture and mock-provider content is explicitly scanned for URMAN identifiers.
- Verification: `npm run content:check` — expected explicit 0/0 workspace audit; `npm run test:content` — 58/58; `npm run test:runtime` — 65/65; `npm run test:architecture` — 2/2; `node --test tests/integration/foundation-canary.test.mjs` — 5/5; build and diff check pass.
- Drift: test-only additive fixture; no production composition, campaign/legacy/scene/OS or asset-tree mutation. Decision `continue` to MM-50.

### MM-50 — portable Chapter 1 data

- Changed paths: `content/modules/urman-core/module.json`, `content/modules/urman-chapter1/**` and `tests/content/urman-chapter1/chapter1-data.test.mjs`.
- Delivered: canonical cast, route/evidence projections, quests, dialogue, knowledge, vocabulary and logical assets. The only `tt_tavysh`/`tt_javap` confirmation is a Rinat-gated reread; the final rule is confirmed only by Rinat's forest interruption. Full old-PC authored bodies remain outside Chapter 1.
- Review history: early reviews removed a false core owner, duplicate old-PC Markdown bodies and early language confirmation; final review aligned the FAP document-desk scene/asset with MM-10 row 197. A quality reviewer made that last scoped edit despite read-only instructions; root reconciled it to the already accepted baseline and required full re-verification plus quality reconfirmation and fresh independent SPEC PASS.
- Verification: module preflight 1/0; chapter data 5/5; content 63/63; runtime 65/65; architecture 2/2; generated type drift, build and diff check pass.
- Drift: no final campaign manifest, old-PC body, legacy source, composition or direct asset-path mutation. Decision `continue` to MM-51.

Оркестратор продолжает дополнять раздел после каждого принятого task: changed paths, exact commands, exit statuses, spec review, quality review, retirement evidence и drift decision.

### MM-20 amendment and MM-52 — portable old PC / DedOS

- Changed paths: closed `DocumentDefinition.oldPc` schema/types/compiler retention, `content/modules/urman-oldpc/**`, `src/os/**`, `src/runtime/modules/oldpc/**`, the injected `ComputerScene` seam and focused tests.
- Delivered: the canonical old-PC document body and metadata are portable data; application routing is a descriptor registry; `OldPcCapabilitySession` owns search/open/save projection and lifecycle. Opening a record emits kernel commands for `knowledgeRefs` and presentation IDs; a bookmark is UI-only and never gates the route.
- Contract: the bounded `oldPc` object is optional, closed and has only `type`, `pcSection`, `canonStatus`, `reliability`, `searchTerms`, `suggestedTerms`; canonical IDs have no aliases.
- Reviews: old-PC schema amendment received `MM-03 OLDPC REFREEZE PASS`; MM-52 received final `SPEC PASS` and `QUALITY PASS`.
- Verification: 17 schemas; 45 focused amendment checks; content 66/66; runtime 90/90; architecture 2/2; production build and diff check pass.
- Deferred: legacy `validate-old-pc-content`/`validate-clue-graph` and old storage reset remain intentionally red and are exclusively MM-70-owned.

### MM-53 — dev-only MainMap isolation

- Changed paths: `src/MainMap/**`, `src/runtime/modules/legacy-mainmap/**`, `tests/runtime/modules/legacy-mainmap/**` and the MM-53 handoff.
- Delivered: MainMap accepts explicit role/layout/action bindings, emits opaque typed requests, owns no production navigation/state and has no automatic registration. Its descriptor is development-only; DOM/listeners/RAF/interval/canvas resources are disposed deterministically.
- Review history: independent reviews found and repaired P1 dynamic action-listener disposal, config validation after acquisition, and non-atomic descriptor init failure. The final regression preserves the original init error while calling destroy once and permanently disposing the session.
- Verification: focused 6/6; runtime 96/96; architecture 2/2; production build and diff check pass; final independent `SPEC PASS` and `QUALITY PASS`.
- Deferred: the remaining production `villageGreybox` callers are explicitly MM-54-owned; descriptor remains unregistered until a dev manifest chooses it.

### Bounded re-freeze and MM-54 — composition-root cutover

- Re-freeze: the optional capability-presentation port now has both independent `SPEC PASS` and `QUALITY PASS`; no P0/P1/P2. The portable scene-to-dialogue seam likewise has independent `MM-03 REFREEZE SPEC PASS` and `QUALITY PASS`: exact compiler pointers, generated interaction types, atomic target entry, zero replay and forest-only final-rule timing are proven.
- Changed paths: explicit production/dev campaigns, Vite virtual catalog selection, `RuntimeBootstrap`, browser `Game` host/input mapping, integration tests, and the MM-10-authorized legacy `GameState`/`SceneManager`/scene/system/UI/data closure.
- Delivered: one compiled production campaign starts through preflight; `RuntimeKernel` remains the sole state owner; terminal dialogue return and source-to-dialogue start effects remain generic; old-PC web access receives only the bounded presentation port in development; MainMap remains dev-only.
- Determinism: session occurrence scopes derive from campaign fingerprint, kind/content ID and immutable creation state. Changed state creates a new interaction namespace; identical restored state returns `DuplicateOccurrence` without another commit. The default seed derives from the selected campaign fingerprint.
- Verification: `content:check` 3 modules/3 campaigns; generated types current; content 68/68; runtime 102/102; architecture 2/2; default-campaign integration 4/4; production build 55 modules; `git diff --check` pass. Static no-story/no-legacy scan for bootstrap/Game/main returned no result.
- Graph: `graphify update .` completed static extraction but the host watch rebuild remains blocked by `Operation not permitted`; source/build verification is otherwise green.

### MM-55 — whole-pack closure

- Delivered: an exact campaign enumerator compiles all authored campaigns independently, resolves declared modules/roles/capability versions/entrypoints, validates the resolver/accessibility closure and preserves compiler reachability/narrative gates. A separate production-source checker covers dev/lab imports, raw storage/global access, direct asset paths, campaign narrative IDs, hardcoded scene control and all retired owner paths.
- Review history: first independent spec review found incomplete narrative-kind, `AudioSystem` retirement and scene-control scans. A follow-up quality review found dotted scene identifiers could still evade detection. All repairs remained inside MM-55 checker/test/doc scope and gained regressions; no content or runtime owner was changed.
- Final verification: static gate 86 files; closure 3/3; content 3 modules/3 campaigns; runtime 102/102; architecture 2/2; production build 55 modules; clean diff. Graphify extraction again reached only the host watch `Operation not permitted` limitation.

### MM-55 amendment and MM-60 — dev-only Lab, stress fixtures and browser integration

- MM-55 amendment: `src/lab/**` is excluded only as a dev-only implementation path; production files importing Lab or MainMap still fail the gate. Verification: static gate 86 files; closure 4/4; content 3 modules/3 campaigns; runtime 102/102; architecture 2/2; build 55 modules; diff check pass.
- Changed MM-60 paths: `src/lab/**`, `tests/e2e/**`, `tests/fixtures/content/stress/**`, `scripts/playtest-mvp-smoke.mjs`, `vite.config.ts`, `package.json`, task handoff.
- Delivered: serve-only `?lab=1`; runtime runs only separately compiler-produced immutable variant packs with real SHA-256 locks; module/quest/scene selectors are explicitly non-mutating inspection selections; preset feeds the real old-PC provider; a failed cleanup retains and blocks the prior run. The production build contains neither Lab marker nor virtual Lab catalog.
- Stress evidence: 12/12 Proposal fixtures have exact per-provider JSON config/event/outcome schema fragments; objective and provider outcome schema refs match; mocks reject invalid config, preserve accessibility outcome, snapshot/restore, cancellation cleanup and typed conflict.
- Browser evidence: zero-dependency CDP validates executable errors, focus failure, default journey/save/restore/final clue timing, Lab swap/ARIA, failed cleanup retention and production preview exclusion. `npm run test:e2e` passed 8/8 using approved local loopback; unapproved sandbox listener failure remains a host permission constraint, not a skipped test.
- Reviews: initial spec found static-gate scope, immutable-pack and CDP diagnostics P1s; initial quality found functional controls, mock contract, smoke-source and failed-cleanup gaps; all repairs were re-reviewed. Final `MM60 REMEDIATION SPEC PASS` and `MM60 QUALITY PASS`, no P0/P1/P2.
- Final verification: `npm run content:check` 3 modules/3 campaigns; `npm run test:unit` content 68/68 + runtime 102/102; architecture 2/2; static gate 86; closure 4/4; build 55 modules; `git diff --check` pass. Drift: no main/kernel/content-module mutation and no live hot-swap/fallback. Decision: continue to MM-70.

### MM-70 — persistence/validator retirement (implementation complete, acceptance blocked)

- Changed paths: `src/runtime/persistence/production-persistence-gateway.{mjs,d.mts}`, `tests/integration/persistence-cutover/**`, `package.json`, deleted legacy validators. The exact four-key reset runs only against an injected storage port in tests; no actual user/browser storage was touched during this work.
- Delivered in isolation: strict V2 save/load/restore, typed `reset-required` for malformed data or campaign mismatch with no partial restore, exact one-time reset marker/notice and preservation of username/Lab/v2/unknown keys. The legacy regex validators and their package callers are deleted; semantic compiler `content:check` remains.
- Evidence: focused persistence 5/5; content 3 modules/3 campaigns; unit content 68/68 + runtime 102/102; architecture 2/2; static gate 88; E2E 8/8 with approved loopback; build 55 modules; diff check pass.
- Independent spec review: `NOT PASS`, P1. Production boot still only constructs `Game → RuntimeBootstrap`; it never creates the gateway, applies reset, renders notice or loads a snapshot. MM-70 scope forbids `Game`/`main`, while MM-54 owns the composition root. This is a requirements/architecture owner conflict, not a local gateway defect. Decision: pause for an explicitly scoped MM-54 amendment; do not start MM-80.

### Coordinated MM-54P/MM-55P/MM-70 amendment — implementation awaiting review

- Scope: exactly `Game`/`main`, RuntimeBootstrap closed persistence port, named gateway, static allowlist and focused Node/browser regressions. No content module, kernel, capability protocol/version, campaign or snapshot shape changed.
- Delivered: a single production storage owner uses injected browser host + RuntimePersistencePort (`capture`/`decode`/`restore`); only the port can replace RuntimeBootstrap live state. Boot runs exact v1 retirement once, saves valid V2 after entry/commits, strictly restores valid V2 to the entry without replay, and renders a typed explicit current-V2 discard flow for mismatch. Storage failure reports after commit through aria-live.
- Retirement: current-V2 discard removes only `urman.mvp.save.v2`; v1 reset remains exactly four keys and preserves marker, username, Content Lab and unknown keys. No reader, broad clear, alias or fallback was introduced.
- Verification: content 3 modules/3 campaigns; content 68/68; runtime 102/102; architecture 2/2; static 88 files; focused persistence 8/8; real Chrome/Vite E2E 10/10; production build 56 modules; clean diff. The added failing-storage E2E proves an already committed transition is not rolled back and receives an aria-live error. Sandbox loopback listener EPERM was expected; the approved local loopback run passed.
- Historical decision at this checkpoint: await independent SPEC/QUALITY review. It later passed; MM-80 is complete.

### Coordinated amendment remediation — composition/presentation/reset authority

- `main` is now the sole browser composition root: it owns compiled-pack import, RuntimeBootstrap construction and named browser-gateway factory. Game receives only `RuntimePresentationPort` plus a ready gateway; a regression forbids Game bootstrap/capability/registry/persistence internals or browser factory imports.
- The presentation facade is closed to campaign/presentation sessions/old-PC presentation and typed snapshot restore. It excludes runtimeContext, contentRegistry, capabilityHost and persistence.
- Current-V2 discard is now proposal-gated: direct invocation rejects, only the exact proposal from a reset-required result succeeds, replay after success rejects, and a storage failure keeps the proposal for retry. Focused persistence 8/8 and real main-entry browser persistence 1/1 pass; independent review remains pending.

### Coordinated amendment P2 hardening — browser factory caller

- Static closure now permits `createBrowserProductionPersistenceGateway` import/call only in `src/main.ts`; the named gateway file is exempt only for its own export definition. A temporary-tree focused regression proves a sibling production caller fails alongside the existing raw-storage escape diagnostic. Status remains `amendment-review`.

### Coordinated MM-54P/MM-55P/MM-70 — independent acceptance

- Result: `QUALITY PASS`; no P0/P1/P2. Review found and closed three P1 boundaries (main-only assembly, presentation facade and proposal-gated V2 deletion) plus the P2 static factory-caller hardening.
- Evidence: content 3 modules/3 campaigns; unit content 68/68 + runtime 102/102; architecture 2/2; focused persistence/static 8/8; static source gate 88 files; real Vite+Chrome E2E 10/10; build 56 modules; `git diff --check` pass.
- Decision: accept MM-54P, MM-55P and MM-70; start MM-80 final verification/KB handoff.

## Completion candidate

Сформирован и принят 2026-07-18.

### MM-80 — final verification и KB handoff

- Final independent architecture review: `PASS`, P0/P1/P2 — none. Reviewer independently confirmed the sole compiled-content/kernel/gateway owners, campaign lock, atomic duplicate-safe restore, generic-owner bans, final «Не отвечай» timing and accessibility-equivalent providers.
- Final MM-80 finding: unreachable internal legacy `src/os/data/web_content.json` carried noncanonical HTML/JSON narrative data. A bounded scan found no import, Vite glob, runtime lookup, user state or external boundary. It was deleted under delete-first retirement, and `RETIRED_OWNER_PATHS` plus a temporary-tree regression now require its exact absence.
- Final fresh commands: `npm run content:check` — 3 modules/3 campaigns; `npm run test:unit` — content 68/68, runtime 102/102; `npm run test:architecture` — 2/2; `node scripts/check-runtime-architecture.mjs` — 88 production files; `node --test tests/integration/persistence-cutover/production-persistence-gateway.test.mjs` — 8/8; whole-pack closure — 4/4; approved-loopback `npm run test:e2e` — 10/10; `npm run build` — 56 modules; `git diff --check` — pass.
- `graphify update .` reran source extraction but its host watch rebuild returned `Operation not permitted`; no code/content test or build failed because of this host limitation.
- KB handoff: technical architecture, decision log, mindmap, backlog, weak points, open questions, README, glossary and MVP handoff are synchronized. The architecture is ready for MVP construction but is not a declaration that the game is an MVP release.
- Residual P2: `GameSnapshotV2` restores an equivalent narrative state, not the exact presentation/UI position. This has an explicit future owner and does not change the MVP-critical narrative outcome.
