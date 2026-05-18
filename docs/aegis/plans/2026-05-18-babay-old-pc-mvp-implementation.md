# Goal

Implement бабай's old PC MVP as an interactive, mouse-driven, data-driven investigation hub.

# Architecture

The old PC shell remains a lightweight Win98-like scene inside `ComputerScene`, but the investigation content moves out of hardcoded registry entries into a runtime content module generated from `content/old_pc/` authoring files. The PC should open windows, folders and apps with mouse interactions, while the main app becomes the archive/search/document hub.

# Tech Stack

- Vite + TypeScript.
- Plain DOM rendering in `src/os/`.
- Markdown + frontmatter authoring under `content/old_pc/`.
- Node validation script for content contract checks.

# Baseline/Authority Refs

- `AGENTS.md`
- `docs/urman_knowledge_base/old_pc.md`
- `docs/urman_knowledge_base/technical_architecture.md`
- `docs/urman_knowledge_base/decision_log.md`
- `docs/aegis/specs/2026-05-18-babay-old-pc-brief.md`
- `content/old_pc/README.md`
- `src/scenes/ComputerScene.ts`
- `src/os/core/OS.ts`
- `src/os/core/registry.ts`

# Compatibility Boundary

- The PC must stay an MVP document hub, not a full programmable OS.
- The shell is unnamed Win98-like; do not revive `Тәрәзәләр 98` as canon UI branding.
- Technical metadata is not the main clue type.
- Content additions should normally require adding/editing Markdown under `content/old_pc/`, not rewriting UI code.
- Existing scene switching through `ComputerScene` and `SceneManager` must keep working.

# Verification

- `node scripts/validate-old-pc-content.mjs`
- `npm run build`
- Local browser smoke: open the app, enter the computer scene, open Archive/Search, search `Марат`, open a gated item, confirm visible UI state changes and no console errors from the new PC app.

# Tasks

1. Add content validation and runtime content model.
   - Files: `scripts/validate-old-pc-content.mjs`, `src/os/data/oldPcContent.ts`.
   - Verify RED first with the validator/test expecting runtime-ready content structure before implementation.
   - Green when content IDs, required fields, statuses, `requires` and `unlocks` validate.

2. Replace the old browser surface with the old PC investigation hub.
   - Files: `src/os/apps/oldPcHub.ts`, `src/os/apps/browser.ts`.
   - Implement sections, search, suggested terms, gated/corrupted item states, clue collection, and document reading.
   - Keep clues as local UI state for MVP; future journal integration remains explicit backlog.

3. Upgrade the OS shell.
   - Files: `src/os/core/OS.ts`, `src/os/core/registry.ts`, `src/os/os.css`.
   - Remove canon-breaking OS branding, wire app initializers, support folders/doc windows, taskbar entries, minimize/close and better sizing.

4. Fix canonical character drift touched by the PC.
   - Files: `src/data/characters.ts`, `src/data/chat_data.ts`.
   - Make `babay` display as Мансур and `abi` as Гөлсинә; do not silently introduce new lore beyond already accepted decisions.

5. Update docs after implementation.
   - Files: `docs/urman_knowledge_base/old_pc.md`, `technical_architecture.md`, `backlog.md`, `mindmap.md`, `weak_points.md`, `decision_log.md`.
   - Record that runtime implementation now exists and what remains post-MVP.

# Risks

- `npm run build` already fails before implementation because of unused old code and missing OS methods. Treat this as repair inside the PC slice where local; do not mask unrelated failures unless they are tiny and safe.
- Replacing `browser.ts` is a retirement of the older experimental browser surface. Keep the exported `renderBrowser` API as compatibility.
- The first version can collect clues locally in PC state; full journal/dialogue integration is a separate owner and should remain documented.

# Retirement

- Retire hardcoded investigation documents in `SYSTEM_REGISTRY`.
- Retire canon-branded `Тәрәзәләр 98` shell text.
- Retire the old experimental `browser.ts` implementation if it blocks build and duplicates the new old PC hub.
