# Initial Baseline Snapshot

Date: 2026-05-18.

## Project Structure

- `src/` - current TypeScript game prototype.
- `src/scenes/` - scene entry points including `ComputerScene`.
- `src/os/` - current old-PC / DedOS prototype.
- `src/MainMap/` - current procedural isometric greybox map.
- `docs/urman_knowledge_base/` - canonical project knowledge base.
- `content/old_pc/` - old PC authoring content source.
- `public/assets/` - prototype and generated visual assets.

## Tech Stack

- TypeScript frontend prototype.
- Browser DOM / canvas UI.
- Vite-style npm project.
- Markdown + frontmatter planned for narrative authoring.

## Ownership Mapping

- Game state: `src/game/GameState.ts`.
- Scene switching: `src/game/SceneManager.ts`.
- Old PC scene: `src/scenes/ComputerScene.ts`.
- Old PC shell: `src/os/core/OS.ts`.
- Old PC registry prototype: `src/os/core/registry.ts`.
- Canon and product rules: `docs/urman_knowledge_base/`.
- Old PC content source: `content/old_pc/`.

## Contract Inventory

- `KnowledgeKey` concept is documented in `docs/urman_knowledge_base/technical_architecture.md`.
- Old PC authoring fields are documented in `docs/urman_knowledge_base/old_pc.md` and `content/old_pc/README.md`.
- Old PC item schema is in `content/old_pc/schema/old_pc_item.schema.json`.

## Dependency Direction

Content should drive UI. Runtime should render and index authoring data rather than hardcoding narrative content in application code.

## Test System

No dedicated automated content validator exists yet. Backlog now requires an old PC content validator.

## Build And Deploy

Current project uses npm scripts from `package.json`; no deployment baseline was reviewed for this snapshot.

## Known Anti-Patterns

- Hardcoded old PC content in UI/app code.
- Technical metadata as primary clue.
- Full OS simulation before detective loop is proven.
- Current code drift: бабай / әби / Мансур names do not match accepted canon.

## Compatibility Boundaries

- Do not break existing scene switching while replacing old PC content.
- Do not make engine-specific narrative data.
- Do not treat Aegis specs as game canon; canonical game rules live in knowledge base.
