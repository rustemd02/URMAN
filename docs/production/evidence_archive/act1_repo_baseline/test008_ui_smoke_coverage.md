# TEST-008 Coverage Receipt — Menu/Settings/Audio/Modal-Stack Focused Smokes

- Date: 2026-09-04
- Commit: `819f933` (branch `main`)
- Verification: `./eng/verify-godot.sh` exit 0 with all listed smokes in the
  aggregator (`evidence/act1_repo_baseline/uiux010-verify-godot-PASS.txt`,
  12 PASS lines); `./eng/verify-dotnet.sh` exit 0.

TEST-008 required focused coverage for: main menu, settings persistence,
audio volumes, modal stack, accessibility options. Delivered by the UIUX
slices (each drives production buttons/paths, no state injection):

| Required area | Smoke (aggregator) | Proof |
|---|---|---|
| Main menu | `act1_main_menu_smoke_test` | menu gates demo, hidden Continue on fresh profile, settings from menu, Continue restores session (`uiux001` evidence) |
| Main menu + session start | `act1_binding_conflict_smoke_test`, `act1_final_state_smoke_test`, `act1_interruption_smoke_test`, `act1_pause_menu_smoke_test` | all boot through the production New Game button via `Act1MainMenuTestSupport` |
| Settings persistence | `act1_user_settings_smoke_test` | versioned store cold-persists preferences, store/slot separation, safe reset (`uiux007` evidence) |
| Settings navigation/apply-rollback | `act1_settings_navigation_smoke_test` | menu-safe open, entry focus, rollback without apply, apply persists (`uiux006` evidence) |
| Audio volumes | `act1_audio_settings_smoke_test` | buses, routing, live volumes, versioned persistence, muted-voice captions, settings rows (`uiux011` evidence) |
| Modal stack | `act1_pause_menu_smoke_test` + dialogue-cancel in `act1_interruption_smoke_test` | pause shell opens/resumes, settings stacks over pause and returns, dialogue cancel atomicity |
| Accessibility options | `act1_reduced_motion_smoke_test` + accessibility rows in settings smoke paths | reduced-motion instant zone cut, normal fade intact; settings accessibility controls apply through the shared snapshot |

## Remaining scope honestly outside this task

Full-resolution/text-scale screenshot matrix (UIUX-009) and the human
first-time readability judgment (PLAYTEST-004) stay open — this receipt
covers the focused automated coverage TEST-008 asked for, not the human
review gates.
