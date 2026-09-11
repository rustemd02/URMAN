# Godot performance evidence

The current baseline is measured on the local Apple M4 Pro with Godot 4.7.1
.NET, Metal 4.0, Forward+, 1920×1080 and 2× MSAA. The test renders the three
mandatory style scenes in real Godot viewports after a warm-up and records
average, p95 and maximum frame time in
[`godot_m4pro_baseline.tsv`](godot_m4pro_baseline.tsv).

All three scenes clear the internal 30 FPS low-preset floor on this host. This
does not prove the release target on Apple M1, integrated Windows graphics or a
real Windows build; those remain required external evidence.

Reproduce with:

```bash
./eng/benchmark-godot.sh
```

## Act 1 demo entrypoint probe (2026-08-15)

The test-only `Act1DemoPerformanceSmokeTest` instantiates the actual
`res://scenes/act1_demo.tscn` entrypoint, including `Main`, the arrival zone,
the first-person player, HUD, runtime bridge and intro card. It is distinct
from the isolated style-scene benchmark and is intended to diagnose reports
of abnormally low FPS without changing gameplay settings.

On the local Apple M4 Pro at 1920×1080, 60 measured frames after a 20-frame
warm-up produced the following results (`nodes=1982`). The demo now applies
its declared startup `medium` profile before the first frame: 0.90 3D scale
and 2× MSAA.

The current source probe also records that warm-up separately: medium
Forward+ `warmup_avg=12.993 ms`, `warmup_max=64.829 ms`, then
`avg=8.310 ms` / `120.34 FPS`; Mobile/low `warmup_avg=10.873 ms`,
`warmup_max=48.195 ms`, then `avg=8.326 ms` / `120.11 FPS`. The warm-up max is
startup shader/import pacing, not a persistent one-frame-per-second loop.

| Renderer | Average | p95 | Maximum | Average FPS |
| --- | ---: | ---: | ---: | ---: |
| Metal Forward+ | 8.301 ms | 8.635 ms | 16.730 ms | 120.46 |
| Metal Forward Mobile | 8.339 ms | 9.032 ms | 9.333 ms | 119.92 |
| Metal Compatibility | 8.235 ms | 13.292 ms | 13.892 ms | 121.43 |

This is local diagnostic evidence, not M1/Windows acceptance. A report of
approximately 1 FPS therefore still requires the target OS/GPU, whether the
export or editor is being used, and a capture from the game window itself.

### Explicit safe launch for weak or software GPUs

The normal demo launch remains `medium` and keeps the full painterly shader.
For a machine that reports single-digit FPS, use the bounded presentation-only
safe launch:

```bash
./eng/run-act1-demo-safe.sh
```

It starts Godot with the `mobile` renderer and `--urman-safe-mode`. The latter
keeps the same zones, meshes, interactions, narrative state and saves, but
skips the three triplanar albedo reads in the painterly fragment shader and
uses the existing low render scale/MSAA profile. This is a diagnostic rescue
path, not a new runtime owner and not evidence that M1/Windows performance is
accepted. If this path is fast while the normal launch is not, the remaining
issue is renderer/material cost on the target GPU rather than the Act 1 state
machine.

Reproduce the full-entrypoint probe with:

```bash
. ./eng/dotnet-env.sh
./.tools/godot/Godot_mono.app/Contents/MacOS/Godot \
  --path game \
  res://tests/act1_demo_performance_smoke_test.tscn
```

### Exported-package probe

The editor probe does not prove that the packaged executable starts with the
same frame time. The main scene contains a diagnostic-only
`--urman-perf-probe` branch, so the published macOS package can be measured
without unsupported scene-path overrides:

```bash
./eng/benchmark-act1-demo-package.sh
./eng/benchmark-act1-demo-package.sh --urman-safe-mode
```

On the fresh 2026-08-15 macOS package, the real-window Metal/Forward+ probes
report `119.94 FPS` for medium and `120.05 FPS` for safe Mobile/low on the
local Apple M4 Pro. Set `URMAN_ACT1_HEADLESS=1` only for a non-GPU startup/CPU
check. This is package evidence for that host only; Windows host execution and
M1/Windows performance remain open.

### Slow-startup rescue and live diagnostic output (2026-08-15)

The Act 1 entrypoint now has a bounded presentation guard for the reported
single-digit-FPS case. After a short warm-up it requires several consistently
slow frames (not one shader-compilation hitch) and then switches the current
session to the existing low material/render-scale profile. It changes no zone,
kernel state, narrative data or SaveGameV3 fields, and it is disabled during
the benchmark probe so benchmark results remain honest.

To inspect a target machine without the rescue masking the problem, pass:

```text
URMAN.exe --print-fps --no-auto-performance-fallback
```

Godot's built-in `--print-fps` output prints live FPS and frame time
approximately once per second. The package probe additionally reports the
effective preset, render scale, MSAA and renderer. On a weak or software GPU,
the first diagnostic launch should instead use `--rendering-method mobile
--urman-safe-mode`; if that remains near 1 FPS, retain the result as a
platform/driver or launch-path blocker and capture the OS, GPU and complete
command line.

### Presentation state CPU guard (2026-08-15)

`InteractionTarget` no longer serializes the complete narrative state from
`_Process` every frame. The Act 1 ending detector also no longer polls the
serialized state from its frame loop. `RuntimeBridge` queues one main-thread
invalidation after a committed state/zone change; targets refresh their
collision layer/visibility and the demo evaluates its ending rule from that
notification. This keeps the kernel as the only state owner while removing
redundant JSON work from the first-person frame loop.

Fresh source-tree real-window probes after this change measured `119.97 FPS`
on Forward+ medium and `119.99 FPS` on Forward Mobile low on the local M4 Pro.
These numbers still do not substitute for a run on the user's OS/GPU.

### External 1 FPS report (2026-08-15, OPEN)

A manual report described the demo as running at approximately one frame per
second. It is not reproduced on the local M4 Pro: the current exported package
measures `119.94 FPS` in Metal/Forward+ and `120.05 FPS` in Metal/Forward
Mobile with `--urman-safe-mode`. The target OS/GPU and launch path are
therefore required before claiming a new runtime regression.

For a weak or software GPU, launch the current package with the bounded rescue
profile first:

```text
URMAN.exe --rendering-method mobile --urman-safe-mode --print-fps
```

On macOS the equivalent executable is
`build/macos/URMAN.app/Contents/MacOS/URMAN`. If safe/mobile is still near one
FPS, retain the result as a platform/driver blocker and record the OS, GPU,
whether the run came from the editor or package, and the `--print-fps` output;
do not activate another material or scene fallback based on a still-unscoped
report.

### Canonical source-tree launch path (2026-08-15)

The source project now has one supported launch wrapper:

```bash
./eng/run-act1-demo.sh
```

It sources the pinned `.tools/dotnet` environment before starting Godot. A
direct Godot binary launch from a clean shell was reproduced as a launch error
(`hostfxr/coreclr` dependent libraries missing), so it must not be interpreted
as a one-FPS runtime result. With the wrapper, the same source entrypoint
measured `120.00 FPS` in Metal Forward+; the safe wrapper measured `120.06 FPS`
in Forward Mobile/low on the local Apple M4 Pro. If the user's correctly
wrapped safe/mobile launch still reports about 1 FPS, the unresolved owner is
the target OS/GPU/driver or display path, not the launch environment documented
above; retain the full command and `--print-fps` output for the next gate.

After the Act 1 OldPc presentation and slow-startup guard slices, the freshly
exported package re-probe measures `119.94 FPS` in Metal/Forward+ medium and
`120.05 FPS` in Metal Forward Mobile/low on the local M4 Pro. Target-host
reproduction is still required.

### Direct-window diagnostic receipt (2026-08-15)

The published macOS ZIP was also launched as a normal window with
`--print-fps --no-auto-performance-fallback`, rather than through the 60-frame
probe. Godot reported `Project FPS: 120` in Forward+ and `Project FPS: 119` in
Mobile/low; a one-second macOS `sample` showed the main thread waiting on
`CAMetalLayer.nextDrawable`, which is the expected VSync pacing path, not a
one-frame-per-second CPU spin. A managed/sandboxed launch aborted while
opening `user://logs`/Metal state before the first frame, so that failure is
not accepted as game-performance evidence.

This does not close the external report: the user's OS/GPU, editor-versus-ZIP
launch path, display scaling and complete `--print-fps` output are still
needed. If the safe command below remains near 1 FPS on the target machine,
the next owner is platform/driver or launch configuration, not the Act 1
runtime state machine.

### Current source/package recheck after the Act 1 journal projection (2026-08-15)

The source entrypoint probe was rerun after the journal UI change: 1 983 nodes,
`8.331 ms` average and `120.03 FPS` in Metal Forward+ on the local Apple M4
Pro. A fresh desktop export was then measured in a real window: `120.08 FPS`
medium Forward+ and `120.03 FPS` safe Forward Mobile/low. The package receipt
is bound to macOS ZIP `e7c466c13c1c4a482d058bc2ffe3bc5040bd96ad41ad38c93c2d4c8448928818`,
Windows ZIP `508f49553a40a16b2d4e662094b72f036a3b3510088f11d3664fb45b388fab32`
and Windows EXE `4e0b621de75c53635bd034ea5bebb5890e05e2eee02d5b1dfd56941921e5869a`.
These are host-local facts; the reported ~1 FPS remains OPEN until the target
OS/GPU and exact editor/package command are captured.

The package was re-exported once more after the `[J]/[Y]` save-status hint was
added. The latest real-window probe measured `118.04 FPS` medium Forward+ and
`118.14 FPS` safe Mobile/low on the same M4 Pro; the small variation is within
the local run-to-run envelope and is not a regression. The receipt remains
structurally bound to the current ZIP/EXE set. Target-host reproduction is
still required.

### Fresh reproduction check (2026-08-15 16:09 MSK)

The source-tree benchmark was rerun after the current Act 1 demo changes. Real
Metal/Forward+ at 1920×1080 measured `118.10 FPS` on `day_street`, `120.01 FPS`
on `house_old_pc` and `118.91 FPS` on `kara_urman_edge`; the 30 FPS low-preset
floor passed for all three scenes. The exported macOS package also passed the
real-window probe (`119.86 FPS` medium and `117.97 FPS` Mobile/low). A direct
source safe launch with `--print-fps --no-auto-performance-fallback` printed
`Project FPS: 118–120` consistently.

Therefore the reported ~1 FPS is still an external reproduction gap, not an
observed Act 1 runtime regression on this host. Before changing geometry,
materials or the state loop, capture the target OS/GPU, editor-versus-package
path, complete command line and the `--print-fps` output. If Mobile/low is also
near 1 FPS, classify it as a renderer/driver or launch-environment blocker.

The package was re-exported after the ordered cliffhanger cue presentation and
AudioCueUi cleanup. The current real-window probe reports `120.04 FPS` medium
Forward+ and `119.86 FPS` safe Forward Mobile/low; receipt check remains PASS
and the Windows host execution gate is still OPEN.

### Fresh package recheck after destination-facing spawns (2026-08-15 17:16 MSK)

The debug packages were rebuilt after the Act 1 startup/transition diagnostic
fix. The retained receipt and read-only check both pass. A serialized
real-window probe of the published macOS package measured `119.96 FPS` medium
Forward+ and `120.02 FPS` safe Forward Mobile/low on the local Apple M4 Pro; both remain above the
internal 30 FPS floor. The run-to-run spread versus earlier 118–120 FPS
measurements is window/VSync/background pacing on this host, not a reproduced
single-frame-per-second stall. Fresh artifacts are macOS ZIP
`3bf0d162fee3cac3c4ca2d2c5084571b080e8c6b68fa8058d8cdbb9c42c5d038`, Windows
ZIP `11a7c0002114594cba5e0a49c36e6b22e7b44fbc1130c5d814b979cdc01d3a8c` and
Windows EXE `4e0b621de75c53635bd034ea5bebb5890e05e2eee02d5b1dfd56941921e5869a`.
The fresh exported app also passed `./eng/verify-macos-host.sh`; target OS/GPU,
Windows host execution and release-hardware performance remain OPEN.

The same current source build records Act 1 zone-transition frame costs of
`29.9 ms` (house), `16.8 ms` (Kara-Urman) and `25.8 ms` (return to village) in
`zone_flow_smoke_test.tscn`. These are synchronous transition diagnostics, not
a replacement for target-hardware traversal, but they do not support a
persistent 1 FPS stall caused by the compact-zone loader on this host.

### Live-window recheck after the external 1 FPS report (2026-08-15 20:49 MSK)

The current source entrypoint was launched as a real window with
`--print-fps --no-auto-performance-fallback`. On the local Apple M4 Pro,
Godot selected Metal 4.0 / Forward+ and printed four consecutive
`Project FPS: 120 (8.33 mspf)` lines. The published package probes in both
medium Forward+ and Mobile/low likewise measured `120.07` and `120.04` FPS.

A managed-sandbox attempt failed before the first frame because Godot could
not open `user://logs` and then aborted inside Metal/MoltenVK. That is a launch
environment failure, not a valid one-FPS measurement. The external report
therefore remains **OPEN**: reproduce the exact target OS/GPU and launch path
with the safe command below, then retain the complete `Project FPS` output.

```text
URMAN.exe --rendering-method mobile --urman-safe-mode --print-fps --no-auto-performance-fallback
```

On macOS, run the equivalent packaged executable from Terminal; for the
source tree use `./eng/run-act1-demo-safe.sh` with the same diagnostic flags.
If Mobile/low is still near 1 FPS, classify the next owner as the target
driver/display/launch environment rather than changing Act 1 materials,
geometry or narrative code.

### Current package recheck after deterministic pine palette (2026-08-15)

Fresh `./eng/export-desktop-debug.sh` completed after the Act 1 presentation
pass. The read-only receipt check and elevated macOS host smoke both pass. The
published macOS package measured `120.15 FPS` medium Metal/Forward+ and
`120.07 FPS` safe Forward Mobile/low on the local Apple M4 Pro; warm-up maxima
were `51.221 ms` and `35.527 ms`, respectively. This host still shows no
persistent one-frame-per-second stall.

Current package binding:

- macOS ZIP: `d9eaa76e57f29fe4dec919d21f3384f1a7f5564efd1b88ce247bce002917d7f0`
  (161,715,412 bytes);
- Windows ZIP: `526aae396324e45504101ee9943824c3a1ed7ddd5c1a8a8daef06d56042391fe`
  (96,886,935 bytes);
- Windows EXE: `4e0b621de75c53635bd034ea5bebb5890e05e2eee02d5b1dfd56941921e5869a`
  (127,508,816 bytes);
- receipt: `2a9fa47531fcd4d01db7c2f00a73173d49794f9d0ea7df907de70562daad7a26`.

These are local M4 measurements only. M1/Windows execution, display scaling,
and the user's exact 1 FPS reproduction remain OPEN; retain OS/GPU, launch
path, renderer preset and complete `--print-fps` output before any runtime
performance change.

### Fresh package recheck after the journal vocabulary projection (2026-08-15)

The demo was exported again after adding the read-only `ТАТАРСКИЕ СЛОВА`
projection to the existing journal. Structural receipt verification and the
elevated macOS host smoke both pass. A real-window probe of the published
package measured `119.82 FPS` medium Metal/Forward+ (`8.346 ms` average,
`8.666 ms` p95, `39.741 ms` warm-up maximum) and `120.04 FPS` safe Forward
Mobile/low (`8.331 ms` average, `8.609 ms` p95, `29.923 ms` warm-up maximum)
on the local Apple M4 Pro.

Current package binding:

- macOS ZIP: `e38176747d9eecaf3fc25207d34a138197200e18dfc90c984279f9cfd58b7751`
  (161,721,643 bytes);
- Windows ZIP: `b93fcc10ca090dca5ba3e780aa403a3e3e533226c2ed4a84d138379a3d8ea56c`
  (96,888,584 bytes);
- Windows EXE: `ca2ccde016413ea88a77146676716643657bd0f343f39ecff8805e063582440e`
  (127,508,992 bytes);
- receipt: `567e45835cbdbcf7c1ffbeafbf0933d65ce82a19fe367014c3d4e309a30c47be`.

The local host still does not reproduce the reported one-FPS loop. This is
not M1/Windows evidence; retain the target OS/GPU, exact editor/package launch
path and complete `--print-fps --no-auto-performance-fallback` output before
assigning the issue to runtime code.

## 2026-09-11 — журнал редких задержек

Существующий `Act1DemoRoot.RecordPerformanceProbeFrame` теперь выводит `act1-perf-stall` только для кадров >100 мс: UTC и монотонный timestamp, warmup/measurement, номер кадра, elapsed, дельты GC поколений и выделенных managed bytes, фокус окна, событие VillageLife и последние engine process/physics monitors. Счётчики собираются только при явно включённом probe. Эти признаки помогают найти причину; совпадение GC само по себе не доказывает причину задержки. Порог итоговой приёмки не изменён.

Проверка logger: C# build PASS; короткий реальный Metal запуск 720p/1с+5с — `DIAGNOSTIC_SHORT`, запись двух warmup-задержек 179,648/173,993 мс с полями. Внешняя диагностическая пауза собственного PID была до окончания построения мира, поэтому не приписывается конкретной записи logger. Это проверка инструмента, не финальный packaged FPS и не исправление прежнего скачка 529,721 мс. Доказательство: `/Users/unterlantas/Documents/URMAN_ActI_Finish_20260911/performance_stalls_diagnostic.log`; userdata восстановлены byte-for-byte. Полное измерение свежей самостоятельной сборки остаётся открытым.
