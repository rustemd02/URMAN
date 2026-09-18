# Act I Repo Baseline (BASE-001)

- Repo: `rustemd02/URMAN`
- Branch: `main`
- SHA: `fc58c408326c0fff420641cdd82e712bb51b4f6c` (`changes`, 2026-09-03)
- Engine/runtime: Godot 4.7.1 .NET (toolchain.json: dotnet SDK 10.0.302, godot 4.7.1.stable.mono)
- Main launch: `game/project.godot` → `res://scenes/act1_demo.tscn`
- Canonical owners: `game/scripts/Act1DemoRoot.cs`, `game/scripts/Main.cs`,
  `game/scripts/RuntimeBridge.cs` → `src-dotnet/Urman.Core`,
  `game/scripts/Act1WorldLayout.cs`, `game/scripts/Act1ConnectedWorld.cs`,
  `game/scripts/StyleBenchmarkZone.cs`, `game/scripts/InteractionTarget.cs`,
  `game/scripts/FirstPersonController.cs`
- Dirty at baseline: `M .DS_Store`, `M AGENTS.md` (pre-existing exception clause),
  `?? docs/tasktracker/01_production_tracker.md` (tracker input, до переноса — корневой файл)
- Local checks required: `./eng/verify-dotnet.sh`, `./eng/verify-godot.sh`
  (logs in `evidence/act1_repo_baseline/`). No local PASS claimed until logs land.
- GitHub checks: none published for this SHA; local verification mandatory.
