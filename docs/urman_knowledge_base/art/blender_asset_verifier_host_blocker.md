# Blender asset-verifier host blocker

Дата проверки: 2026-08-14.

## Симптом

В managed/sandboxed запуске pinned Blender `4.5.12 LTS` завершает работу с
`SIGSEGV (139)` до загрузки `.blend` и до запуска Python verifier. Поэтому
этот контекст не может объявить полный `./eng/verify-assets.sh` PASS. В
elevated real-driver запуске того же pinned бинаря оба Blender verifier сейчас
проходят; это отдельная host-dependent проверка, а не отмена воспроизводимого
sandbox blocker.

## Воспроизведение

```text
BLENDER=.tools/blender/Blender.app/Contents/MacOS/Blender

$BLENDER --factory-startup --background --python-expr "print('startup-python-ok')"
exit=139

$BLENDER --factory-startup --gpu-backend metal --background \
  assets/source/blender/urman_modular_kit.blend \
  --python tools/blender/verify_modular_environment.py
exit=139

$BLENDER --factory-startup --gpu-backend metal --background \
  assets/source/blender/urman_character_kit.blend \
  --python tools/blender/verify_character_kit.py
exit=139
```

Проверенные логи:

- `/private/tmp/urman-blender-startup-repro.log`;
- `/private/tmp/urman-blender-metal-retry.log`;
- `/private/tmp/urman-blender-character-metal-retry.log`;
- crash report Blender в `$TMPDIR/blender.crash.txt`.

В backtrace повторяется один и тот же путь:

```text
blender::gpu::supports_barycentric_whitelist
blender::gpu::MTLBackend::metal_is_supported
GPU_backend_type_selection_detect
WM_init
main
```

Это указывает на падение при Metal backend detection в pinned Blender, а не на
плохую геометрию, текстуру, `.blend` или verifier script. `--factory-startup`
и явный `--gpu-backend metal` не меняют результат.

## Текущий статус

- Texture PNG/image gates, Godot import, v5↔v6 A/B и 27-cell Metal/Forward+
  motion sweeps остаются валидными и не затронуты.
- Blender source/GLB assets не изменялись этим расследованием.
- Environment/character Blender verification: **OPEN / host-toolchain
- Host-independent `./eng/verify-asset-registry.sh`: **PASS** (36 derived
  files, 10 explicit local sources, source/derived hashes checked).
- Environment/character Blender verification: **PASS (elevated real-driver) /
  OPEN (managed sandbox host-toolchain blocked)**.
- Art lock и production asset acceptance не объявляются.

## Следующее безопасное действие

Повторить `./eng/verify-assets.sh` на release host и после обновления pinned
Blender до версии с исправленным Metal startup crash. До этого не менять
runtime, shader, `.blend` или generated GLB только ради обхода падения.
`eng/verify-asset-registry.sh` остаётся обязательным первым gate и не является
fallback для Blender. Если инструмент обновляется, требуется отдельное
решение по toolchain и полный asset/Godot regression pass.

## Rebuilt modular-kit receipt — 2026-08-14

The elevated real-driver rerun after the authored village-module rebuild passes
the current contract: `verify_modular_environment.py` reports 47 deterministic
environment LOD1 meshes, including WellA/WoodpileA/GateA, and the character
verifier remains 143 LOD0/LOD1 pairs. The independent registry preflight now
checks 36 derived files and 10 explicit local-source records. These are fresh
elevated-host results; the managed-sandbox Metal startup blocker is unchanged
and still requires release-host revalidation. No fallback or stale generated
asset was used.
