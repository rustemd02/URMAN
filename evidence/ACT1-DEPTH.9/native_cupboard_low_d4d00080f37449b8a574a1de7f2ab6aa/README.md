# ACT1-DEPTH.9 protected Low cupboard capture

Native Windows capture `d4d00080f37449b8a574a1de7f2ab6aa` used sealed snapshot `f5501cd114cce146aeaffaa933b122a930d8cd424935e075720bc7fa79ef844e` from base `32264f34b933eda2cf015b4ff188e32cb126a31e`. The full 1,825-file manifest is preserved and matches the receipt. Thirty-two selected runtime source files and ten freshly copied local-only authoring/evidence files are preserved; the latter were excluded from station payload.

- The two exact existing checkpoint rows target `house_old_pc` at actual Low, FOV70, `village-winter-frost`; there was no phase or environment override. Sidecars confirm requested/effective Low, applied through player settings, no session override, `graphicsComparable=true`, and 0.70 render scale.
- Godot 4.7.1.stable.mono.official.a13da4feb, .NET SDK 10.0.302, Vulkan Forward+ on NVIDIA GeForce GTX 970. Receipt reports runner/import/game exit codes, verified snapshot hashes, and restored user data. The post-sequence doctor is ready.
- Original PNG IHDR dimensions are 1886×1061, while sidecars report a 1920×1080 viewport. This is static evidence only: it does not prove interaction, an ordinary player route, collision, or whole-interior acceptance. Root visual review is pending; no visual verdict is included here.
- `game.log` contains 13 warning blocks and no actual shader/error markers; `engine-errors.log` and `stderr.log` are empty. Raw log bytes and line endings are preserved.
- The current cupboard receipt is retained under `source/local-authoring-excluded/` and its source blend, generator, and derived GLB hashes were matched against the current local files. The raw result ZIP stays in ignored `.codex-captures/remote/d4d00080f37449b8a574a1de7f2ab6aa/result.zip`; its SHA-256 is recorded.
