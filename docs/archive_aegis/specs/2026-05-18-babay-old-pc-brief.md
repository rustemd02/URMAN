# Babay Old PC Product Lock

Status: brief accepted from user answers, 2026-05-18.

Canonical game document: `docs/urman_knowledge_base/old_pc.md`.

## TaskIntentDraft

Outcome: fix the product, narrative and authoring contract for бабай's old PC before implementation.

Goal: make the PC a data-driven document hub that future Codex/LLM sessions can safely extend without contradicting canon.

Success evidence:

- old PC role and MVP boundary are documented;
- required sections are listed;
- authoring folder and schema exist;
- initial 3-5 content files exist;
- implementation drift is recorded.

Stop condition: product decisions are captured in canonical KB and authoring files, but code implementation is not started yet.

Non-goals:

- no full OS implementation;
- no terminal puzzle implementation;
- no runtime content loader yet;
- no final literary polish of documents.

## BaselineReadSetHint

- `AGENTS.md`
- `docs/urman_knowledge_base/README.md`
- `docs/urman_knowledge_base/gameplay.md`
- `docs/urman_knowledge_base/technical_architecture.md`
- `docs/urman_knowledge_base/decision_log.md`
- `docs/urman_knowledge_base/weak_points.md`
- `src/scenes/ComputerScene.ts`
- `src/os/core/OS.ts`
- `src/os/core/registry.ts`

## ImpactStatementDraft

Affected layers:

- narrative canon;
- gameplay loop;
- technical authoring model;
- old PC UI implementation;
- content validation;
- character naming cleanup.

Invariants:

- Марат remains the MVP emotional center;
- ПК proves the system but does not fully explain the pact;
- татарский words can become search terms and knowledge keys;
- technical metadata is not a main clue;
- PC shell is unnamed Win98-like.

Compatibility:

- old PC content must be portable and engine-neutral;
- future implementation should migrate from hardcoded registry content to content files;
- current code naming drift must be resolved before serious integration.

## Options Considered

1. Document hub only.
   Low scope, strong detective focus, less atmosphere.

2. Full pseudo-OS.
   Atmospheric, but too likely to swallow MVP.

3. Document hub with limited puzzle-box elements.
   Chosen. Keeps investigation primary while allowing gated documents, corrupted fragments and one possible password.

## Decision

Implement бабай's old PC as a data-driven document hub with limited puzzle-box elements. See `docs/urman_knowledge_base/old_pc.md`.

## Review Notes

No placeholders remain in this brief. The implementation plan should start from content loading / validation, not from visual OS features.
