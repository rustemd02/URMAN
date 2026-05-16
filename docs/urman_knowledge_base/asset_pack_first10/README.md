# URMAN MVP First 10 Asset Pack

Status: Generated first test batch, 2026-05-16.

Workspace asset path: `public/assets/urman_mvp_first10/`

This is the first controlled production batch, not the full 50-unit inventory. It follows the required workflow:

1. Style bible locked.
2. Character identity sheets locked.
3. Test batch generated: Айдар portrait set, main street route screen, old PC frame, document template, Kara-Urman forest edge pressure screen.
4. Consistency checked against the style bible.
5. Controlled animation slots added for overlay-driven ambience and pressure states.

## Consistency Check

- Visual style: pass. Ink line, muted watercolor and paper grain are consistent across portraits, locations, UI and document.
- Айдар identity: pass. Face, age, haircut, hoodie/jacket silhouette and slim build stay stable across variants.
- Horror tone: pass. Unease is subtle; no gore, skulls, glowing eyes, demons, jump-scare framing or full creature reveal.
- Cultural framing: pass. Village details stay grounded; no costume-folklore overload.
- Text readiness: pass with sidecars. Route signs, PC labels and document content are intended for editable overlays, not baked unreadable text.
- Controlled animation readiness: pass via `animation_layers.json`. Current PNGs are static bases with specified overlay / mask / shader regions for light, CRT, birds, foreground drift, document focus and pressure states.
- Known limitation: generated source images are flattened PNGs, not layered PSDs. Layer intent and editable text coordinates are documented in sidecars.

## Final Asset Manifest

| asset_id | file_name | asset_type | dimensions | transparent | variants included | related | consistency anchors |
|---|---|---|---:|---|---|---|---|
| `char_aidar_portrait_neutral` | `char_aidar_portrait_neutral.png` | Character portrait | 1024x1024 | yes | neutral | Айдар | Same face, haircut, hoodie/jacket, slim young silhouette |
| `char_aidar_portrait_thinking` | `char_aidar_portrait_thinking.png` | Character portrait | 1024x1024 | yes | thinking | Айдар | Same identity; only hand pose and gaze change |
| `char_aidar_portrait_worried` | `char_aidar_portrait_worried.png` | Character portrait | 1024x1024 | yes | worried | Айдар | Same identity; expression and head angle change |
| `char_aidar_portrait_pressure` | `char_aidar_portrait_pressure.png` | Character portrait | 1024x1024 | yes | fear/pressure | Айдар | Same identity; pressure is facial tension, not monsterization |
| `char_aidar_avatar_small` | `char_aidar_avatar_small.png` | UI avatar | 512x512 | yes | small avatar | Айдар | Cropped from same controlled set |
| `char_aidar_portrait_contact_sheet` | `char_aidar_portrait_contact_sheet.png` | Character contact sheet | 1536x1024 | yes | neutral, thinking, worried, pressure, resolve, avatar source | Айдар | Single generated controlled set to prevent redesign drift |
| `route_main_street_entry_day` | `route_main_street_entry_day.png` | Route navigation screen | 2048x1152 | no | day | Главная улица Кырлая | First-person route segment, clear forward road and right turn, blank diegetic sign |
| `ui_old_pc_desktop_base` | `ui_old_pc_desktop_base.png` | Old PC UI / prop frame | 2048x1152 | no | desktop base | Старый ПК Мансура | Win98-like, restrained, blank overlay-ready labels |
| `doc_marat_medical_record_bg` | `doc_marat_medical_record_bg.png` | Document background | 1463x2048 | no | scan background | Медсправка Марата | Blank form background; evidence text stays editable |
| `route_kara_urman_forest_edge_pressure` | `route_kara_urman_forest_edge_pressure.png` | Pressure route/location screen | 2048x1152 | no | pressure | Кромка Кара-Урмана | Level 3 unease, branches almost form shapes, no creature reveal |

## Source Notes

- `public/assets/urman_mvp_first10/_source/char_aidar_portrait_contact_sheet_key.png` is the chroma-key source used to create the transparent Айдар files.
- `animation_layers.json` defines the first pass of controlled animation slots. It does not mean every slot should be active all the time.
- Generated images remain in the Codex generated image cache; project-consumable copies are stored in the workspace path above.
