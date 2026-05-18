# URMAN Remaining Asset Pack Generation Log

Status: Started, 2026-05-17.

## Production Rule

The first batch in `public/assets/urman_mvp_first10/` is locked as upstream material. Do not regenerate it without a concrete defect. New generated assets go to `public/assets/urman_mvp_remaining/`.

## Required Pre-Generation Reading

Read before generation:

- `AGENTS.md`
- `docs/urman_knowledge_base/README.md`
- `docs/urman_knowledge_base/asset_inventory_50.md`
- `docs/urman_knowledge_base/design_style.md`
- `docs/urman_knowledge_base/characters.md`
- `docs/urman_knowledge_base/village_route_art_spec.md`
- `docs/urman_knowledge_base/decision_log.md`
- `docs/urman_knowledge_base/open_questions.md`
- `docs/urman_knowledge_base/weak_points.md`
- `docs/urman_knowledge_base/asset_pack_first10/style_bible.md`
- `docs/urman_knowledge_base/asset_pack_first10/character_identity_sheets.md`
- `docs/urman_knowledge_base/asset_pack_first10/controlled_animation.md`
- `docs/urman_knowledge_base/asset_pack_first10/animation_layers.json`
- `docs/urman_knowledge_base/asset_pack_first10/editable_layers.json`
- `public/assets/urman_mvp_first10/asset_manifest.json`

## Audit Of Existing First10 Batch

Existing first10 workspace assets:

- `char_aidar_portrait_neutral.png` — covers inventory #1 variant `neutral`.
- `char_aidar_portrait_thinking.png` — covers inventory #1 variant `thinking`.
- `char_aidar_portrait_worried.png` — covers inventory #1 variant `worried`.
- `char_aidar_portrait_pressure.png` — covers inventory #1 variant `fear/pressure`.
- `char_aidar_avatar_small.png` — covers inventory #1 variant `small avatar`.
- `char_aidar_portrait_contact_sheet.png` — production reference for inventory #1.
- `route_main_street_entry_day.png` — partial coverage for inventory #22 and #23.
- `ui_old_pc_desktop_base.png` — partial coverage for inventory #33 and #34.
- `doc_marat_medical_record_bg.png` — partial coverage for inventory #45.
- `route_kara_urman_forest_edge_pressure.png` — partial coverage for inventory #31 and #23.

Validation:

- Character transparent PNGs are RGBA.
- Location / UI PNGs are 2048x1152.
- Medical document background is 1463x2048.
- `asset_manifest.json`, `editable_layers.json` and `animation_layers.json` are valid JSON.

## Inventory Coverage After Audit

| # | Inventory row | Status before remaining generation | Notes |
|---:|---|---|---|
| 1 | Айдар — портретный набор | complete | Locked in first10. Do not regenerate. |
| 2 | Айдар — малый игровой спрайт | missing | Generate in Batch 1. |
| 3 | Мансур бабай — портретный набор | missing | Generate controlled set in Batch 1. |
| 4 | Мансур бабай — позы / cutout | missing | Generate in Batch 1 or P1 support. |
| 5 | Гөлсинә / Әби — портретный набор | missing | Generate controlled set in Batch 1. |
| 6 | Гөлсинә / Әби — кухонная поза | missing | Generate in Batch 1 or P1 support. |
| 7 | Алсу — портретный набор | missing | Generate controlled set in Batch 1. |
| 8 | Алсу — малый спрайт / cutout | missing | Generate in Batch 1 or P1 support. |
| 9 | Марат — портрет / силуэт подростка | missing | Generate evidence assets in Batch 1. |
| 10 | Детское фото Айдара и Марата | missing | Generate evidence assets in Batch 1. |
| 11 | Тимур хәзрәт — портретный набор | missing | Generate in Batch 4. |
| 12 | Ринат — портретный набор | missing | Generate controlled set in Batch 1. |
| 13 | Ринат у кромки — силуэт / cutout | missing | Generate in Batch 1. |
| 14 | Наиля — портретный набор | missing | Generate in Batch 4. |
| 15 | Разиля — портретный набор | missing | Generate in Batch 4. |
| 16 | Ambient villagers group | missing | Generate in Batch 4. |
| 17 | Приезд / дорога к Кырлаю | missing | Generate in Batch 4. |
| 18 | Дом бабая и әби — экстерьер | missing | Generate in Batch 2. |
| 19 | Кухня / первый ужин | missing | Generate in Batch 2. |
| 20 | Комната / стол Мансура со старым ПК | missing | Generate in Batch 2. |
| 21 | Двор / сарай / старая Нива | missing | Generate in Batch 4. |
| 22 | Главная улица Кырлая | partial | Day route screen exists; evening / pressure missing. |
| 23 | Village route navigation kit | partial | Main street day and Kara-Urman pressure exist; most route segments and transition assets missing. |
| 24 | Сельмаг | missing | Generate in Batch 4. |
| 25 | Мечеть — экстерьер | missing | Generate in Batch 4. |
| 26 | Мечеть — чай / кабинет Тимура | missing | Generate in Batch 4. |
| 27 | Медпункт / ФАП | missing | Generate in Batch 2. |
| 28 | Кладбище / зират — общий вид | missing | Generate in Batch 2. |
| 29 | Могила Марата — close-up / interaction | missing | Generate in Batch 2. |
| 30 | Река / берег | missing | Generate in Batch 4. |
| 31 | Кромка Кара-Урмана | partial | Pressure screen exists; approach / silence / Rinat interruption missing. |
| 32 | Администрация / архивный угол | missing | Generate in Batch 4. |
| 33 | Старый CRT / monitor hardware | partial | Working desktop frame exists; off/boot/power-off/close crop missing. |
| 34 | Old PC desktop shell | partial | Desktop base exists; folder / active window / error-corrupt states missing. |
| 35 | Archive search app | missing | Generate in Batch 3. |
| 36 | «Татарвики» page template | missing | Generate in Batch 3. |
| 37 | «Ялкын» / ICQ messenger | missing | Generate in Batch 4. |
| 38 | Телефон Айдара / intro chat UI | missing | Generate in Batch 4. |
| 39 | Journal / clue graph | missing | Generate in Batch 3. |
| 40 | Dialogue key picker | missing | Generate in Batch 3. |
| 41 | Vocabulary / татарский карточки | missing | Generate in Batch 3. |
| 42 | Document viewer template | missing | Generate in Batch 3. |
| 43 | Clue type icon set | missing | Generate in Batch 3. |
| 44 | Inventory / evidence prop icon set | missing | Generate in Batch 4. |
| 45 | Медсправка / медзапись Марата | partial | Background exists; metadata / redacted / readable text layer missing. |
| 46 | Могильная запись / cemetery registry | missing | Generate in Batch 3. |
| 47 | Дневник Марата | missing | Generate in Batch 3. |
| 48 | Сохранённые сообщения Марата | missing | Generate in Batch 3. |
| 49 | Внутренний учёт / pact folder | missing | Generate in Batch 3. |
| 50 | Audio / VFX state pack | missing | Generate visual overlay pack metadata / PNG overlays in Batch 4; audio generation is not part of imagegen. |

## Batch 1 Plan — Remaining P0 Characters

Generate:

- `char_aidar_sprite_contact_sheet.png` and cropped transparent frames:
  - `char_aidar_sprite_idle.png`
  - `char_aidar_sprite_walk_01.png`
  - `char_aidar_sprite_walk_02.png`
  - `char_aidar_sprite_forest_tint.png`
- `char_mansur_portrait_contact_sheet.png` and cropped transparent portraits:
  - `char_mansur_portrait_warm.png`
  - `char_mansur_portrait_evasive.png`
  - `char_mansur_portrait_stern.png`
  - `char_mansur_portrait_almost_confession.png`
  - `char_mansur_portrait_pressure.png`
- `char_gulsina_portrait_contact_sheet.png` and cropped transparent portraits:
  - `char_gulsina_portrait_warm.png`
  - `char_gulsina_portrait_worried.png`
  - `char_gulsina_portrait_quiet_warning.png`
  - `char_gulsina_portrait_hidden_knowledge.png`
- `char_alsu_portrait_contact_sheet.png` and cropped transparent portraits:
  - `char_alsu_portrait_guarded.png`
  - `char_alsu_portrait_helpful.png`
  - `char_alsu_portrait_ironic.png`
  - `char_alsu_portrait_worried.png`
  - `char_alsu_portrait_withheld_truth.png`
- Mарат evidence:
  - `char_marat_evidence_old_photo.png`
  - `char_marat_evidence_diary_silhouette.png`
  - `char_marat_evidence_forest_memory_silhouette.png`
  - `photo_aidar_marat_childhood_clean.png`
  - `photo_aidar_marat_childhood_damaged.png`
  - `photo_aidar_marat_childhood_journal_thumb.png`
- Ринат:
  - `char_rinat_portrait_contact_sheet.png`
  - `char_rinat_portrait_official_deflect.png`
  - `char_rinat_portrait_irritated.png`
  - `char_rinat_portrait_fear.png`
  - `char_rinat_portrait_practical_warning.png`
  - `char_rinat_forest_edge_cutout_distant.png`
  - `char_rinat_forest_edge_cutout_interruption.png`
  - `char_rinat_forest_edge_cutout_hard_cut.png`

## Batch 1 Started

- 2026-05-17: Audit complete. No first10 regeneration needed.
- 2026-05-17: Generated Айдар small sprite set with 4 transparent frames.
- 2026-05-17: Generated Мансур бабай controlled portrait set.
- 2026-05-17: Generated Гөлсинә / әби controlled portrait set.
- 2026-05-17: Generated Алсу controlled portrait set.
- 2026-05-17: Generated Марат evidence / childhood photo set.
- 2026-05-17: Generated Ринат controlled portrait set and forest-edge cutouts.
- 2026-05-17: Validated Batch 1 PNG dimensions and alpha channels; updated `asset_manifest.json` and `animation_layers.json`.
- 2026-05-17: Corrected `transparent_background` from actual alpha pixels, not PNG mode alone; linked manifest entries to controlled animation slots and editable text regions.

## Batch 1 Result

Complete inventory rows:

- #2 Айдар — малый игровой спрайт.
- #3 Мансур бабай — портретный набор.
- #5 Гөлсинә / Әби — портретный набор.
- #7 Алсу — портретный набор.
- #9 Марат — портрет / силуэт подростка.
- #10 Детское фото Айдара и Марата.
- #12 Ринат — портретный набор.
- #13 Ринат у кромки — силуэт / cutout.

Still missing from character/crowd section:

- #4 Мансур бабай — позы / cutout.
- #6 Гөлсинә / Әби — кухонная поза.
- #8 Алсу — малый спрайт / cutout.
- #11 Тимур хәзрәт — портретный набор.
- #14 Наиля — портретный набор.
- #15 Разиля — портретный набор.
- #16 Ambient villagers group.

## Batch 2 Started — P0 Route / Location Screens

- 2026-05-17: Registered `loc_mansur_house_exterior_day.png` as partial coverage for #18 Дом бабая и әби — экстерьер.
- Source image was preserved as `_source/loc_mansur_house_exterior_day_raw_1672x941.png`; runtime file was resized to 2048x1152.
- The scene is ordinary daytime village exterior only: open gate, carved wooden windows, yard, fence, road and domestic clutter. No overt monster or Halloween framing.
- Controlled animation slots added for rare distant birds, tiny grass/branch drift, optional curtain/light micro-variation and pressure-flag-only wrong shadow.

Still missing for #18:

- Evening exterior variant.
- Pressure exterior variant.
- Optional separated pass notes for interactable gate/window/door hotspots.

## Batch 4 Local Slice — Icons / Visual VFX Overlays

- 2026-05-18: Generated deterministic ink-line UI icon sets:
  - `ui_clue_type_icon_set.png`
  - `ui_inventory_evidence_icon_set.png`
- 2026-05-18: Generated controlled visual overlay PNGs:
  - `overlay_pressure_edge_darkening.png`
  - `overlay_window_warm_glow.png`
  - `overlay_crt_scanline.png`
  - `overlay_document_focus_highlight.png`
  - `overlay_ink_smear_transition.png`
  - `overlay_water_near_static_shimmer.png`
  - `overlay_dust_motes.png`
  - `overlay_branch_grass_drift_mask.png`
  - `ui_icons_vfx_contact_sheet.png`

Coverage update:

- #43 Clue type icon set — complete as a first game-ready icon sheet.
- #44 Inventory / evidence prop icon set — complete as a first game-ready icon sheet.
- #50 Audio / VFX state pack — partial: visual overlay PNGs exist; authored audio ambience / voice assets remain separate production work.

Validation:

- Root `public/assets/urman_mvp_remaining/` has 68 PNG files.
- `asset_manifest.json` has 68 entries.
- `animation_layers.json` has 68 entries.
- `editable_layers.json` has 16 editable-layer entries.
- JSON validation passes and root PNG filenames match manifest filenames.

## Batch 3 Integrated — UI Systems

- 2026-05-18: Integrated worker package `batch3_ui_systems_worker`.
- Added:
  - `ui_archive_search_empty.png`
  - `ui_archive_search_results.png`
  - `ui_tatarwiki_article_template.png`
  - `ui_journal_clue_graph_base.png`
  - `ui_dialogue_key_picker_base.png`
  - `ui_vocabulary_cards_base.png`
  - `ui_document_viewer_template.png`
  - `ui_clue_type_icons_contact_sheet.png`
- Coverage update:
  - #35 Archive search app — complete first-pass base states.
  - #36 Татарвики page template — complete first-pass template.
  - #39 Journal / clue graph — complete first-pass base.
  - #40 Dialogue key picker — complete first-pass base.
  - #41 Vocabulary cards — complete first-pass base.
  - #42 Document viewer template — complete first-pass base.
  - #43 Clue icons — covered by worker contact sheet and local deterministic icon sheet.
- Editable UI text regions were merged into `editable_layers.json`; important text remains runtime-rendered.

## Batch 4 Integrated — P1 Portraits

- 2026-05-18: Integrated worker package `batch4_p1_portraits_worker`.
- Added transparent portrait sets:
  - Тимур хәзрәт: calm, direct, concerned, prayerful_restraint.
  - Наиля: professional, evasive, alarmed.
  - Разиля: gossip_friendly, cautious, pressure.
- Coverage update:
  - #11 Тимур хәзрәт — portrait set complete.
  - #14 Наиля — portrait set complete.
  - #15 Разиля — portrait set complete.
- Visual QA notes: Тимур remains safe-zone/supportive, Наиля remains practical medpunkt staff, Разиля remains grounded social/selsmag presence; no monster/gore/Halloween framing.

Validation after integration:

- Root `public/assets/urman_mvp_remaining/` has 89 PNG files.
- `asset_manifest.json` has 89 entries.
- `animation_layers.json` has 89 entries.
- `editable_layers.json` has 23 editable-layer entries.
- JSON validation passes and root PNG filenames match manifest filenames.

Validation checkpoint:

- 41 PNG files exist in `public/assets/urman_mvp_remaining/`.
- 41 manifest entries exist and match the workspace PNG filenames.
- `asset_manifest.json`, `animation_layers.json` and `editable_layers.json` pass JSON validation.
- Evidence/photo assets are marked non-transparent when their alpha channel is fully opaque.
- Runtime evidence/photo assets have editable metadata overlay regions; important labels must remain engine-rendered text.

## Integration Checkpoint — Batch 2/3 Documents, Old PC And Local Support Locations

- 2026-05-18: Integrated worker package `batch2_locations_completion_worker`.
- 2026-05-18: Integrated worker package `batch3_documents_worker`.
- 2026-05-18: Integrated worker package `batch3_old_pc_phone_worker`.
- 2026-05-18: Integrated local imagegen support-location bases for arrival road, yard/Niva, selsmag exterior, mosque exterior and river bank.

Coverage update:

- #19, #27, #28, #29, #33, #34, #45 and #48 are now marked complete for first-pass game-ready bitmap coverage.
- #17, #21, #24, #25, #30, #31, #37, #38, #46, #47 and #49 remain partial where variants or final readable narrative overlays are still missing.
- All newly integrated document/UI text remains runtime-editable; no important story text should be treated as baked into the PNG.

## Batch 3 Local Document Gap Completion

- 2026-05-18: Added deterministic document derivatives for remaining document variants:
  - `doc_cemetery_registry_journal_evidence_card.png`
  - `doc_marat_diary_damaged_page_bg.png`
  - `doc_pact_folder_pakt_1999_replacement_bg.png`
- #46 and #47 are now complete for first-pass visual bases.
- #49 is complete for visual bases; final readable pact wording belongs in runtime narrative/editable text data.

## Batch 2/4 Route And Support Location Integration

- 2026-05-18: Integrated route-kit incoming PNGs for Mansur house turn, mosque sign, selsmag/FAP lane, crossroad pressure and 90-degree ink-smear transition.
- 2026-05-18: Integrated support-location incoming PNGs for admin/archive corner, arrival bus-stop road, mosque tea office, river day, selsmag counter and yard/Niva evening.
- QA note: one Mansur cutout incoming file was rejected because it was cropped without a head; the cutout worker was asked to regenerate full silhouettes before integration.

## Batch 3 Comms UI Completion

- 2026-05-18: Integrated `batch3_comms_completion_worker` outputs for Yalkyn group chat, Alsu DM, unread danger-message state, phone mother chat, phone ticket/purchase and translation popup.
- #37 and #38 are now complete for first-pass visual bases.
- QA note: tiny UI chrome micro-marks are acceptable as non-story decoration; meaningful chat/title/timestamp text is covered by editable runtime regions.

## Batch 4 Cutouts And Ambient Crowd Integration

- 2026-05-18: Integrated transparent cutouts for Мансур, Гөлсинә/Әби and Алсу plus ambient villagers/crowd/chat-avatar sheets from `batch4_cutouts_crowd_worker`.
- QA correction: the first cropped Мансур standing cutout was rejected; regenerated version includes full head/feet silhouette and alpha channel.
- #4, #6, #8 and #16 are now complete for first-pass transparent PNG coverage.

## Batch 4 Route/Admin/Small Location Gap Integration

- 2026-05-18: Integrated small location gap worker outputs for arrival vehicle interior, Niva/tool close-up, mosque serious-talk office and admin denied-access state.
- 2026-05-18: Integrated route-pressure and route/admin gap PNGs for main street evening/watchers, bridge/river turn, Alsu meeting spot, forest voice-lock, Rinat interruption, unsafe path and journal route sketch.
- #17, #21, #22, #23, #26, #31 and #32 are now complete for first-pass visual coverage.
- Duplicate concepts from parallel workers were either given distinct alternate filenames or left in `_incoming` as source material rather than replacing integrated assets.

## Final Visual Validation Checkpoint

- 2026-05-18: Final validation pass completed for the remaining visual asset pack.
- Root `public/assets/urman_mvp_remaining/` has 179 PNG files.
- `asset_manifest.json` has 179 entries and matches root PNG filenames.
- `animation_layers.json` has 179 entries and matches manifest asset IDs.
- `editable_layers.json` has 92 editable-layer entries.
- All 50 rows in `asset_inventory_50.md` are represented in manifest coverage.
- Only #50 remains partial, because authored audio/voice assets require a separate audio production pass. Visual overlay/VFX PNGs for #50 are present.
- Dimension and transparency checks pass for all manifest entries.
