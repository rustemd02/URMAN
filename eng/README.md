# URMAN toolchain

The pinned macOS tools live under ignored `.tools/` and are described by `toolchain.json`.

Expected executable paths:

```text
.tools/dotnet/dotnet
.tools/godot/Godot_mono.app/Contents/MacOS/Godot
.tools/blender/Blender.app/Contents/MacOS/Blender
```

The matching Mono export templates are downloaded under `.tools/downloads/` and installed into Godot's versioned user template directory. Their URL, size and checksum are part of `toolchain.json`.

Verification:

```bash
.tools/dotnet/dotnet --version
.tools/godot/Godot_mono.app/Contents/MacOS/Godot --headless --version
.tools/blender/Blender.app/Contents/MacOS/Blender --background --version
```

Do not commit downloaded archives or application bundles. When a pinned version changes, update the architecture decision, this manifest, checksums and the full regression evidence together.

Repository helpers:

```bash
./eng/compile-game-content.sh
./eng/verify-dotnet.sh
./eng/verify-godot.sh
./eng/verify-assets.sh
./eng/export-desktop-debug.sh
./eng/verify-desktop-artifacts.sh
./eng/verify-desktop-artifacts.sh --check-receipt
./eng/verify-macos-host.sh
./eng/run-act1-demo.sh
./eng/run-act1-demo-safe.sh
./eng/run-smoke-guarded.sh act1_audio_settings_smoke_test
./eng/benchmark-act1-demo-package.sh
./eng/check-web-retirement.sh --report
```

The first command regenerates both Godot runtime packs (`urman.chapter1` and `urman.fullgame`) from authoritative `content/`. The content test suite fails when either derived pack is stale. The second command builds the full solution without compiler servers, runs Core and Content tests, validates every campaign, emits the audio production-readiness report and runs the deterministic Chapter 1 simulation. The third runs a fresh Godot `--import` and then the headless scene, zone, narrative, dialogue, old-PC, collision and persistence smoke suite. The asset verifier reopens both generated Blender sources, checks the 47 environment LOD1 variants (including HouseA, WellA, WoodpileA, GateA and the bounded facade pass) plus the nine-prefix/143-mesh face-and-clothing character kit and validates the registry JSON.

`run-smoke-guarded.sh` runs one or more smoke scenes with `eng/protected_run.py` around each child. Headless checkpoint, save-lifecycle and walkthrough tests write real `SaveGameV3` files. Each smoke receives clean userdata; the guard keeps the original directory outside the child's path, restores it by rename, and verifies file contents, links and permissions. It also restores after child failure or SIGINT/SIGTERM and refuses concurrent protected checks or an unfinished recovery. Backups stay beside Godot userdata, outside git. The runner accepts bare scene names or `res://` paths; `URMAN_GUARD` overrides the script location, and a missing guard stops the run. For a capture that needs an existing save, invoke `python3 eng/protected_run.py <command>` without `--clean` to start with a disposable copy. Build the pinned C# project before running scenes.

`sh eng/run-act1-demo.sh` refreshes campaign content, checks the current C# build,
imports assets, then opens the game. It stops if any refresh step fails. The
launcher enters `protected_run.py` automatically, using a disposable copy of
the player's existing userdata; after closing the game, the original saves
and preferences are restored.

The desktop export command produces unsigned local debug packages in an isolated fresh staging directory, verifies them before publication and replaces `build/windows` wholesale so previous payloads cannot leak into a new archive. Export/publication and standalone verification share the atomic `build/.desktop-artifacts.lock`; contention fails closed, and an interrupted owner must be confirmed dead before its stale lock is removed manually. Nested staged/final checks use the export owner's exact token rather than opening a second lock. HUP, INT and TERM always clean task-owned temporary files/locks and exit non-zero.

The Windows ZIP is built without macOS resource-fork metadata. The verifier canonicalizes all input/receipt paths before mutation, rejects a receipt that aliases any artifact, and fails closed on corrupt, unsafe, duplicate, NFKC/case-fold-colliding, Windows-reserved or host-metadata ZIP entries. Path traversal and separator checks are repeated after Unicode normalization. It also requires an exact macOS `arm64` + `x86_64` executable, checks the three self-contained .NET dependency closures, compares the archived and staged Windows executables and scans each packaged payload once for required content markers. Any previous receipt is invalidated before publication; `build/desktop-artifact-receipt.json` appears atomically only after its schema, paths, sizes, SHA-256 values and full Windows payload manifest are rebound to all three final artifacts. The standalone form uses current `build/` paths, while a staged caller may pass `mac_zip windows_exe windows_zip receipt` as four positional arguments. The read-only `--check-receipt` form never removes or rewrites the receipt and compares the published ZIP, artifact digests, archive entry count and every regular file in `build/windows` against the retained manifest. This is structural package evidence only. `verify-macos-host.sh` separately launches the extracted macOS app with Forward+, Dummy audio and a bounded headless quit; Windows host execution, signing, notarization and release-hardware performance remain separate open gates.

`package-macos-dmg.sh <macos-zip> <output.dmg>` turns the verified macOS export into the delivery image for people outside the repository. It refuses to overwrite an existing image or to run while a volume named `URMAN` is already mounted, re-extracts the ZIP instead of reading the source tree, repeats the universal-binary and startup-marker probe, and then re-signs the bundle. Re-signing is required because Godot wraps the export around a prebuilt template whose Developer ID signature no longer matches the payload Godot appended, and that stale third-party signature is what makes macOS report a damaged app. The image is signed ad-hoc and deliberately without hardened runtime, because the .NET runtime JITs and would need entitlements an ad-hoc signature cannot legitimately carry. The script finally mounts the image it just wrote and launches the app from that read-only volume, so a PASS covers the path a recipient actually takes. Developer ID signing and notarization stay separate open gates, and the `START_HERE_RU.txt` written into the image explains the right-click Open an unsigned build needs on first launch.

`Urman.ContentCli report --root .` adds the non-destructive production boundary report: it compiles all campaigns, enumerates audio assets and variants, checks caption/transcript closure, resolves physical files under `game/` and inspects the Godot ambient manifest. Logical `.ref` voice assets are reported as `logical-ref` and keep the report at `OPEN`; they are not treated as missing files or final authored audio. A non-zero exit is reserved for invalid content/manifest diagnostics, not for the intentionally open authoring status.

`benchmark-act1-demo-package.sh` is a diagnostic-only packaged-entrypoint
probe. It extracts `build/macos/URMAN.zip` into a temporary directory when an
explicit `URMAN_ACT1_PACKAGE_BINARY` is not supplied, starts the real main
scene with `--urman-perf-probe`, prints average/p95/max frame time plus the
first 20-frame warm-up average/max and removes the temporary extraction after
exit. Pass `--urman-safe-mode` to measure the
weak/software-GPU presentation branch; the default run uses the real desktop
renderer, while `URMAN_ACT1_HEADLESS=1` is an explicit CPU/startup-only check.
It does not alter saves, narrative state or release gates.

`run-act1-demo.sh` is the canonical source-tree launch wrapper. It sources the
pinned `.tools/dotnet` environment before starting the Godot .NET binary and
passes all extra arguments through unchanged. Running the Godot binary directly
without `eng/dotnet-env.sh` is unsupported: on a clean shell it can fail to
locate `hostfxr`/`coreclr`, which is a launch-path failure rather than an FPS
measurement. `run-act1-demo-safe.sh` additionally selects the Mobile renderer
and the session-only low material profile for weak or software GPUs.

`check-web-retirement.sh --report` is a non-destructive cutover preflight. It inventories runnable TypeScript/Vite/Three.js entrypoints and browser persistence references while the old web project is still the read-only parity oracle. `--assert-absent` is the strict release gate and must remain failing until macOS/Windows host acceptance, cultural/accessibility review and the complete playthrough are recorded; it never deletes browser data or source files.

Rebuild the project-original 3D source assets with pinned Blender 4.5 LTS when their generators change:

```bash
./eng/rebuild-assets.sh
```

The wrapper is equivalent to:

```bash
.tools/blender/Blender.app/Contents/MacOS/Blender --background --python tools/blender/generate_modular_environment.py -- --root "$PWD"
.tools/blender/Blender.app/Contents/MacOS/Blender --background --python tools/blender/generate_character_kit.py -- --root "$PWD"
./eng/verify-assets.sh
```

## Shell notes for these scripts

The scripts here are POSIX `sh` and run under `/bin/sh`, which in this environment
is bash 3.2 (arm64-apple-darwin26). That build rejects a `case` statement inside a
command substitution:

```sh
# fails: syntax error near unexpected token ';;'
x=$(case a in a) echo b ;; esac)
```

Top-level `case` is fine, and `zsh`/`dash` accept the same line, so this is easy to
misread as a quoting problem. Prefer a plain `for`/`while` loop at the top level, or
compare two sorted lists with `diff` instead of building a filtered string inside
`$( )`. `eng/build-act1-review-packet.sh` does the latter.

Two other habits these scripts rely on: run them with `sh eng/<script>.sh` so
`eng/dotnet-env.sh` is sourced in the same shell that builds, and treat any native
launch (package probe, demo run, smoke scene) as something that writes the user's
Godot userdata — wrap it in `protected_run.py` or the guard-aware wrapper.
