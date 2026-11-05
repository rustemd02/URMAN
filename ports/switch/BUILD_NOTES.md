# URMAN Switch native build — 2026-11-05

User requested a full homebrew port for Switch OLED and asked to arrange the Linux build environment independently. This branch adds the pinned Godot 4.7 / Horizon .NET 10 static host, source-build scripts, resource export and strict package validation. Nine packager tests and Python syntax checks pass. Native compilation and hardware acceptance are not yet demonstrated.

The first CI run builds the runtime and engine. An engine-only NRO is not a game package: the full PhotoWorlds PCK and matching managed application/framework payload are required. No changes are merged into main.

The normal shared AGENT_CHAT.md is 1.8 MB. Automatic approval review could not process its full-file update due to review input limits, so the build record is kept here without replacing that shared history.
