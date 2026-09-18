# Recovered worktree candidates — 2026-08-24

Статус: **архив provenance, не production-источник и не runtime-owner**.

При очистке detached worktree `2502` и `c43f` были найдены две уникальные
пары authored-кандидатов, которых нет в текущем каноническом runtime:

- `assets/retired_candidates/act1-2026-08-17/urman_act1_kara_zirat_environment.{blend,glb}`
  — старый composite Kara/Zirat environment из worktree `2502`;
- `assets/retired_candidates/act1-2026-08-17/urman_act1_master_terrain.{blend,glb}`
  — ранний master-terrain candidate из worktree `c43f`;
- `act1_master_spatial_layout_2026-08-17.md` — ранняя spatial-layout запись
  из `c43f`, заменённая более новой authoritative записью
  `docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md`.

SHA-256 исходных файлов зафиксирован в истории операции очистки. Эти файлы
не подключены к `Act1ConnectedWorld`, не являются collision/navigation или
narrative owners и не должны включаться в production без отдельного решения.
Текущий canonical source-of-truth остаётся в `assets/source/blender/act1/`,
`game/assets/models/act1/` и `game/scripts/Act1ConnectedWorld.cs`.

Временный `Act1ConnectedWorldForestCapture` из worktree `e587`, PNG-captures,
Godot/.NET/NuGet/Graphify caches и `.blend1` backups не переносились: они
воспроизводимы либо уже заменены текущими full-route capture harnesses и
authoritative knowledge-base документами.
