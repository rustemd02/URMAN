# STATE-001 Audit Ledger — RuntimeBridge Sole-Writer Caller Audit (P0)

- Date: 2026-09-03
- Commit: `def4f43` (branch `main`)
- Method: repo-wide `rg` sweep over `game/scripts` and `src-dotnet`
  (`DispatchAsync|ApplySnapshot|RuntimeKernel|SaveGameV3|SetWorldLocation|
  CaptureSaveGame|HandleOldPcInputAsync|LoadSlot|QuickSave|QuickLoad|Reconcile|
  user://|FileAccess.Open|WriteAllText`) + per-caller code inspection; no
  production code change.

## Files touching the state surface and classification

| File | Surface used | Classification |
|---|---|---|
| `RuntimeBridge.cs` | owns `RuntimeKernel` (private `_kernel`), `SaveGameV3`, `AtomicSaveGameStore("user://savegames")`, `DispatchInteractionAsync`, `HandleOldPcInputAsync`, `SetWorldLocation`, save/load/restore, `ReplaceKernel` | sole writer (authorized owner) |
| `QuestRuntimeCoordinator.cs:90-150` | `kernel.DispatchAsync` for `quest.refresh` / `objective.complete` inside `ReconcileAsync(kernel, nextOccurrenceId)` — invoked only from `RuntimeBridge.cs:765`; deterministic occurrence IDs (`nextOccurrenceId`, `ObjectiveOccurrenceId(attempt)`) make re-dispatch idempotent; completions gated by content completion conditions | authorized reconciliation path (STATE-004 projection boundary held) |
| `Main.cs:121,167` | `bridge.SetWorldLocation(zoneId, spawnPointId)` on zone switch | authorized location write via bridge |
| `OldPcUi.cs:76,100,129` | `_bridge.HandleOldPcInputAsync(search/open/save)` | authorized; UI sends input events, never writes state |
| `FirstPersonController.cs` | comment-only reference ("transform owned by RuntimeBridge/SaveGameV3"); receives `ApplyPortableTransform` from bridge | reader |
| `AccessibilityPresentation.cs` | comment-only reference | reader |
| `src-dotnet/Urman.Core/*` (RuntimeKernel, SaveGameV3, AtomicSaveGameStore, RuntimeContracts) + `Urman.Content/CampaignSimulator` | kernel/persistence implementations and content simulation | owned implementations (bridge is the only Godot-side consumer) |

## Negative findings (no bypass)

- The only `user://` writer in the Godot tree is the bridge-owned
  `AtomicSaveGameStore` path (`RuntimeBridge.cs:54`). No UI/world/journal/
  dialogue file writes or story-file access.
- No `RuntimeKernel` instance, field, or internal accessor leaks outside
  `RuntimeBridge` (grep: zero hits).
- No `ApplySnapshot` callers outside the bridge/kernel restore path.
- No scene-local narrative flags or progress caches: UIs read projections and
  invalidate on `RuntimeStateChanged` (STATE-002 boundary unchanged).

## Verdict

Zero unauthorized write paths on commit `def4f43`. UI/world/capture remain
readers/presenters; quest reconciliation is kernel-mediated, content-gated and
deterministically idempotent. Reopen when a new caller, kernel accessor, or
save-file writer appears (TEST-007 adds the standing static guard).
