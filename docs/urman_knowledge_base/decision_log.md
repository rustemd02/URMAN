# Decision Log

## 2026-05-16 — Treat URMAN_Codex_Context.md as primary source

Status: Accepted

Context: Исходный файл найден в `/Users/unterlantas/Downloads/URMAN_Codex_Context.md`, но будущим Codex-сессиям нужен источник в репозитории.
Decision: Скопировать файл в корень проекта как `URMAN_Codex_Context.md`, не удаляя оригинал.
Consequences: Future sessions can read canonical source from repo root. Downloads copy remains untouched.
Linked files: `../../URMAN_Codex_Context.md`, `../../AGENTS.md`

## 2026-05-16 — Use Айдар as MVP protagonist name

Status: Accepted

Context: Ранний контекст иногда использует Айрат, нормализованный канон и поздний лор используют Айдар.
Decision: Использовать Айдар в документации, data IDs and MVP сценарии. Айрат оставить старым alias.
Consequences: Уменьшается путаница в тексте, UI и данных.
Linked files: `canon.md`, `characters.md`, `open_questions.md`

## 2026-05-16 — Use Кырлай as canonical village name

Status: Accepted

Context: Ранний синопсис использует Кара-Урман, поздний лор подробно разработал Кырлай.
Decision: Кырлай — каноническое название деревни для MVP. Кара-Урман — не название деревни, а старое локальное название запретного лесного урочища / тёмной части леса у границы Кырлая.
Consequences: Все MVP docs use Кырлай for the village. Кара-Урман can be used for warnings, maps, archival names and forest-boundary horror without competing with the main setting name.
Linked files: `canon.md`, `village_lore.md`, `open_questions.md`

## 2026-05-16 — Use Гөлсинә as canonical әби name

Status: Accepted

Context: Ранний список даёт Миннигуль, поздний лор фиксирует Гөлсинә Хәмит кызы.
Decision: Использовать Гөлсинә / Әби в MVP. Миннигуль оставить старым alias.
Consequences: Characters, canon and docs follow the detailed later lore.
Linked files: `canon.md`, `characters.md`

## 2026-05-16 — Use Мансур as бабай name

Status: Accepted

Context: Ранний список называет бабая Мансуром, но поздний лор подробно описывает роль без закрепления имени.
Decision: Использовать Мансура как каноническое имя бабая. В тексте допустимы «Бабай», «Мансур» и «Мансур бабай» в зависимости от точки зрения персонажа.
Consequences: `char_babay` can remain the stable data ID, but display name and production text should use Мансур / Мансур бабай. Фамилия пока не фиксируется.
Linked files: `characters.md`, `open_questions.md`

## 2026-05-16 — MVP centers on Marat, not the logging/corruption plot

Status: Accepted

Context: Ранний синопсис строился вокруг вырубки, поздний MVP сильнее фиксирует Марата как эмоциональный крючок.
Decision: MVP main arc is Marat mystery. Logging/corruption can be a false lead only if it does not dilute the first slice.
Consequences: Backlog and MVP docs prioritize grave, medpunkt, archive, Rinat and old PC.
Linked files: `mvp_scope.md`, `narrative.md`, `backlog.md`

## 2026-05-16 — Do not fully reveal creatures in MVP by default

Status: Proposed

Context: Источник рекомендует дать след, силуэт, звук, документ или несостыковку, а не полноценный показ существа.
Decision: MVP cliffhanger should prove the system, not show a complete monster encounter.
Consequences: Reduces asset cost and protects slow-burn tone.
Linked files: `mvp_scope.md`, `assets.md`, `mythology.md`

## 2026-05-16 — Keep engine choice open

Status: Accepted

Context: Движок не указан.
Decision: Technical architecture remains engine-neutral and data-driven.
Consequences: Avoid engine-specific commitments; use portable data models.
Linked files: `technical_architecture.md`

## 2026-05-16 — Use static-node hybrid gameplay for MVP direction

Status: Accepted

Context: Gameplay format was open between classic Zelda-like, isometric, interface-heavy and hybrid. The project needs tension, but also must stay cheap enough for fast prototyping / vibe coding.
Decision: Use a static-node hybrid direction for MVP: mostly static or lightly animated location scenes connected by a village map, with limited spatial movement and deep interface investigation inside key nodes.
Consequences: Avoid full Zelda-like/isometric movement scope for MVP. Prioritize tense pacing, readable interactions, sound, pauses, documents, journal, old PC and dialogue keys. Keep diegetic "real object" interfaces selective only where they are cheap and high-impact, not as a mandatory UI rule.
Linked files: `gameplay.md`, `assets.md`, `open_questions.md`, `mindmap.md`

## 2026-05-16 — MVP tension comes from pauses, sound and incomplete rules

Status: Accepted

Context: The desired horror tone is not constant jump scares or monster encounters. A useful reference direction is tension from withheld rules, pauses, sound and player uncertainty.
Decision: Build MVP tension through sound-first presence, silence, delayed reactions, ambiguous rules and the feeling that the world has an order the player only partly understands.
Consequences: Do not rely on frequent screamers or full creature reveals. Uncomfortable moments may exist, but base controls should stay readable; friction belongs in specific scenes or diegetic devices, not in the core UX.
Linked files: `gameplay.md`, `assets.md`, `mvp_scope.md`, `open_questions.md`

## 2026-05-16 — Use detective loop as the main minute-to-minute action

Status: Accepted

Context: After choosing static-node hybrid, the project still needed a clear repeatable player action. Exploration-only risks becoming wandering; document-only risks becoming reading without tension.
Decision: The core minute-to-minute loop is detective: notice an inconsistency, capture it as a clue, test it in dialogue / archive / document, get a changed reaction or new meaning, then return to old evidence with updated understanding.
Consequences: Spatial movement and documents support the detective loop; they are not the main loop by themselves. Every MVP document, NPC exchange and татарский unlock should either create, test, reframe or resolve a clue.
Linked files: `gameplay.md`, `open_questions.md`, `weak_points.md`, `mvp_scope.md`

## 2026-05-16 — Make бабай's old PC the MVP document hub

Status: Accepted

Context: Старый ПК бабая уже был самым выгодным интерфейсным ассетом MVP. Новое решение уточняет его роль: это не побочная «прикольная штука», а сюжетно важный центр доступа к документам, архивам, «Татарвики», сохранённым сообщениям и странному внутреннему учёту Кырлая.
Decision: В MVP старый ПК Мансура должен быть основным документальным хабом расследования. Через него игрок получает или переосмысляет ключевые материалы по Марату, первые записи о старой системе сосуществования и часть татарского language layer. ПК должен ощущаться абсурдно глубоким через плотность контента, поиск, переоткрытие старых файлов и странную систематизацию, но не обязан быть тьюринг-полной ОС в MVP.
Consequences: Prototype old PC / archive becomes one of the highest-priority MVP tasks. Most MVP documents should either live on the PC, be indexed from it, or have an explicit reason to exist outside it. Full programmability / Turing-complete behavior is a long-term ambition, not a first-slice requirement.
Linked files: `mvp_scope.md`, `gameplay.md`, `technical_architecture.md`, `assets.md`, `backlog.md`, `mindmap.md`

## 2026-05-16 — Use scripted village pressure flags for dangerous knowledge

Status: Accepted

Context: The village needs to react when Айдар uses dangerous clues, but a full village simulation would over-scope the MVP.
Decision: Use scripted pressure flags plus a light 0–3 village pressure level. Dangerous keys can change NPC lines, trigger family / council reactions, adjust node availability and alter ambience.
Consequences: The village feels like it remembers Айдар's investigation without requiring a complex systemic simulation. Pressure is a dramaturgical tool, not a punishment-heavy stealth or survival system.
Linked files: `gameplay.md`, `open_questions.md`, `technical_architecture.md`, `backlog.md`

## 2026-05-16 — MVP cliffhanger at the edge of Кара-Урман

Status: Accepted

Context: The MVP needs a concrete final scene that proves the old system without showing a full creature or turning into a generic monster reveal.
Decision: The working MVP cliffhanger: Айдар follows the detective trail from Марат / old PC / archive clues to the edge of Кара-Урман. At the boundary, he hears Марат's voice from the forest. Before he answers, Ринат appears and says: «Не отвечай». Ринат reveals knowledge through action, not a monologue.
Consequences: The finale uses one static forest-edge scene, sound, silence and a brief Ринат intervention. It proves that Марат's case is tied to real rules of the forest and reframes Ринат from «local cop blocking the investigation» into someone who knows practical survival rules.
Linked files: `mvp_scope.md`, `narrative.md`, `open_questions.md`, `assets.md`, `backlog.md`

## 2026-05-16 — Exclude KFU intro from MVP

Status: Accepted

Context: KFU / Kazan intro can establish contrast, but it delays the village, adds scene scope and risks weakening the start of the MVP.
Decision: Do not include a standalone KFU intro scene in MVP. Start the playable experience at or near Айдар's arrival in Кырлай.
Consequences: Казань / student-life contrast can remain as lightweight context through phone messages, dialogue or journal backstory, but not as a separate playable scene for the first slice.
Linked files: `mvp_scope.md`, `narrative.md`, `open_questions.md`, `backlog.md`

## 2026-05-16 — Use ink-wash storybook as visual style

Status: Accepted

Context: The project needed a reproducible art direction for sprites, portraits, locations, documents and UI. Hyperrealistic concept art looked impressive but was rejected because it would be hard to reproduce consistently for production assets.
Decision: Use an ink-wash storybook direction: hand-drawn ink line, muted watercolor fills, paper texture, grounded village detail and subtle wrongness. The visual rule is «обычность сначала, неправильность потом». This is not pixel art, not hyperrealism, not AAA concept art and not decorative folklore overload.
Consequences: MVP assets should prioritize static location nodes, expressive portraits, in-world village route screens, old PC/document UI and sound-linked visual states. Full uncanny horror and full creature reveals remain outside the baseline MVP style.
Linked files: `design_style.md`, `assets.md`, `open_questions.md`, `weak_points.md`, `mindmap.md`

## 2026-05-16 — Use in-world route navigation for the village

Status: Accepted

Context: A full visible map removes mystery, while free isometric movement is too expensive for the accepted ink-wash MVP style. The chosen village-map pitch variant A works because it lets the player stand inside the village instead of observing the whole layout from above.
Decision: Use in-world discrete route navigation for Кырлай. The player sees one route segment at a time, moves forward by fixed steps, turns left/right by 90 degrees and uses diegetic signs / landmarks for wayfinding. A journal sketch can show discovered routes, but it is incomplete and supportive rather than the main overworld.
Consequences: Village movement needs route segment assets, facing views, physical signs, landmarks and reusable turn/step transitions. Do not implement fake continuous 3D rotation of flat paintings. The current procedural / isometric `MainMap` remains greybox only, not final direction.
Linked files: `gameplay.md`, `design_style.md`, `asset_inventory_50.md`, `assets.md`, `technical_architecture.md`, `mindmap.md`

## 2026-05-16 — Lock first generated asset test batch

Status: Accepted

Context: The first production generation pass needed a controlled style bible, recurring character identity sheets and a small batch before expanding toward the full 50-asset inventory.
Decision: Store the first controlled batch in `public/assets/urman_mvp_first10/` and document the style/identity locks in `docs/urman_knowledge_base/asset_pack_first10/`.
Consequences: Future Айдар portraits should derive from this identity sheet and contact sheet rather than redesigning him. Route screens, old PC UI and document backgrounds should follow the same ink-wash / muted watercolor direction and keep important text editable via overlays.
Linked files: `asset_pack_first10/style_bible.md`, `asset_pack_first10/character_identity_sheets.md`, `asset_pack_first10/README.md`

## 2026-05-16 — Add controlled animation slots to visual assets

Status: Accepted

Context: Static-node hybrid scenes should not feel dead, but uncontrolled animation risks turning УРМАН into generic animated ambience or noisy horror UI.
Decision: Treat animation as controlled overlay slots on top of static PNG bases: light pulse, CRT glow, birds, dust, grass/branch drift, document highlights and pressure overlays. Do not bake loops into base images by default.
Consequences: Future asset prompts must reserve animation-safe regions. Runtime animation should be slow, sparse and tied to scene state / pressure flags. Full creature motion, glowing eyes, jump-scare movement and heavy glitch remain forbidden for MVP.
Linked files: `asset_pack_first10/controlled_animation.md`, `asset_pack_first10/animation_layers.json`, `asset_pack_first10/style_bible.md`

## 2026-05-18 — Treat the remaining MVP pack as visual-complete, with audio separated

Status: Accepted

Context: The remaining asset generation pass produced game-ready PNG bases, transparent character cutouts, route screens, document/UI backgrounds, icon sheets and visual overlay/VFX states for the 50-row inventory. Row #50 includes authored audio and voice assets, which should not be faked by empty files or generic placeholders.
Decision: Mark `public/assets/urman_mvp_remaining/` as the validated visual asset pack. Keep #50 partial for authored audio/voice production while counting the visual overlay/VFX PNGs as complete visual coverage.
Consequences: Runtime can start integrating the visual pack using `asset_manifest.json`, `animation_layers.json` and `editable_layers.json`. A separate audio pass is still required for home ambience, street silence, old PC hum, mosque calm, cemetery wind, water stillness, forest presence, Marat voice and Rinat's «Не отвечай».
Linked files: `asset_inventory_50.md`, `asset_pack_remaining/README.md`, `asset_pack_remaining/generation_log.md`, `public/assets/urman_mvp_remaining/asset_manifest.json`

## 2026-05-18 — Lock бабай old PC product concept and authoring model

Status: Accepted

Context: Старый ПК бабая уже принят как главный документальный хаб MVP, но его продуктовая граница, разделы, puzzle-box элементы и data-driven authoring model были не зафиксированы достаточно конкретно. Без lock ПК легко расползётся в отдельную ОС или начнёт противоречить линии Марата и пакта.
Decision: Делать ПК Мансура в MVP как документальный хаб расследования с ограниченными puzzle-box элементами. Основные разделы: архивный поиск, документы Марата, «Татарвики», сохранённые сообщения, внутренний учёт Кырлая, реестр домов / семей, нарушения / компенсации, Кара-Урман, повреждённые / скрытые файлы и ограниченные бытовые папки. ПК доступен всегда из дома; первый доступ мотивирован бытовой просьбой бабая, но позже может читаться как скрытое допущение. Оболочка безымянная Win98-like, не каноническая «ОС». Техническая метадата не является основной уликой; важны документальные отметки. Контент ПК пишется как Markdown + frontmatter в `content/old_pc/`, чтобы сценарист или LLM-ассистент могли безопасно добавлять документы.
Consequences: Реализация ПК должна идти от content graph и валидируемых authoring-файлов, а не от захардкоженных окон. Первый прототип должен включать 3-5 источников: официальный документ о смерти Марата, противоречащий реестр, сохранённое сообщение, статью «Татарвики» и повреждённую запись внутреннего учёта. Текущий кодовый drift с именами Гаяз / Зухра / отдельный Мансур надо исправить перед серьёзной интеграцией ПК.
Linked files: `old_pc.md`, `gameplay.md`, `technical_architecture.md`, `backlog.md`, `mindmap.md`, `content/old_pc/README.md`

## 2026-05-18 — Generate route-map graph and asset integration pack

Status: Accepted

Context: Route navigation was already accepted as the MVP village movement model, but the world map still needed a concrete node/facing graph, production questions, reusable transition rules and game-ready route imagery with controlled animation slots.
Decision: Create a separate route-map integration pack in `public/assets/urman_route_map/`, reuse the existing first10 / remaining route PNGs, generate only missing facing views and transition/support overlays, and keep the journal sketch as an incomplete support view rather than a full top-down map. The route graph uses discrete first-person nodes, `turnLeft = -90`, `turnRight = 90`, fixed step-forward movement and explicit external handoff nodes for interiors / special scenes.
Consequences: Runtime integration can start from `route_graph.json`, `asset_manifest.json`, `animation_layers.json` and `editable_layers.json`. The pack currently validates at 41 PNG assets, 32 route nodes, 10 route-node state transitions, 1 journal support transition, 34 editable-layer entries and 0 missing route-map assets. The next task is engine/runtime implementation, not more random route image generation.
Linked files: `route_navigation_product_lock.md`, `route_navigation_graph.md`, `../../public/assets/urman_route_map/route_graph.json`, `../../public/assets/urman_route_map/asset_manifest.json`

## 2026-05-18 — Implement бабай old PC MVP prototype

Status: Accepted

Context: Product lock for the old PC was accepted, but runtime still used a hardcoded experimental shell with canon drift, no content validator and no serious mouse-driven investigation loop.
Decision: Implement the old PC as a Win98-like interactive document hub backed by Markdown/frontmatter content from `content/old_pc/`. The prototype includes direct home access, desktop icons, draggable windows, taskbar, archive search, sections, suggested search terms, document reader, gated/corrupted unlocks, local clue saving and a validator script. The old experimental browser surface is retired behind the same `renderBrowser/initBrowser` API.
Consequences: ПК now proves the MVP archive loop without becoming a full programmable OS. `babay` / `abi` naming drift is fixed in code. Remaining production work is to connect PC clues into the shared journal/dialogue knowledge graph and to integrate the final route-navigation path to the house.
Linked files: `old_pc.md`, `technical_architecture.md`, `backlog.md`, `mindmap.md`, `../../src/os/apps/oldPcHub.ts`, `../../src/os/data/oldPcContent.ts`, `../../scripts/validate-old-pc-content.mjs`, `../../content/old_pc/`

## 2026-05-18 — Treat shared evidence integration as the next MVP blocker

Status: Proposed

Context: Audit after route navigation and old PC prototypes found that visual assets, route graph and old PC content are ahead of the integrated gameplay loop. The old PC can produce local unlocks, and route navigation can move through Кырлай, but journal, dialogue keys, vocabulary re-read, pressure, save/load and final cliffhanger are not connected through a shared source of truth.
Decision: Prioritize shared evidence integration before adding more lore branches or generating more visual assets. The required chain is: old PC clue -> shared knowledge key -> journal card -> dialogue reaction -> vocabulary re-read -> pressure change -> route/cliffhanger progression.
Consequences: Next implementation work should follow `mvp_completion_handoff.md` and validate through `playtest_plan.md`. Full external MVP playtest is premature until this chain works at least once with Марат / Ринат / `tt_urman` / Кара-Урман.
Linked files: `mvp_completion_handoff.md`, `playtest_plan.md`, `technical_architecture.md`, `old_pc.md`, `backlog.md`, `weak_points.md`

## 2026-05-23 — Use real татарские words through русско-татарская mixed speech

Status: Accepted

Context: The language mechanic was drifting toward broad «Chants of Sennaar»-style semantic deduction, which risks making татарский feel like a fictional code or a complex linguistic puzzle. The desired direction is simpler and more grounded: the player learns real татарские words that local NPCs naturally insert into Russian speech.
Decision: Use medium татарский density for MVP: early NPC lines are mostly Russian with individual татарские word insertions; later scenes can contain more татарский because the player has learned repeated words and short formulas. Do not make proverbs, complex grammar, large untranslated monologues, literary register or Иске Имля mandatory MVP mechanics. One mostly татароязычный NPC is allowed as a marker of comprehension progress, but must not fully block the main story path.
Consequences: Vocabulary entries must teach real words and connect each word to gameplay use: dialogue key, old PC / archive search, document re-read, route sign or pressure scene. Each new MVP word should appear in several contexts before it is required. Татарский text needs consultant review before production lock.
Linked files: `language_learning.md`, `gameplay.md`, `technical_architecture.md`, `open_questions.md`, `mindmap.md`

## 2026-05-23 — Lock Chapter 1 / MVP campaign structure

Status: Accepted

Context: The MVP needed a 40–60-minute first chapter that is more than a content list: a playable narrative spine with route navigation, old PC investigation, journal, dialogue keys, татарский re-read, pressure and an audio-first cliffhanger. A red-team pass found that early confirmation of «do not answer», a PC-only structure and an unexplained Rinat arrival would weaken the chapter.
Decision: Use `chapter1_mvp_campaign.md` as the canonical first-chapter scenario lock. The chapter starts near the road / arrival, not with playable Kazan or KFU. The spine is: road -> home -> Alsu / village route -> old PC official document -> FAP / Rinat / internal register contradiction -> Tatarwiki re-read -> Timur -> evening route past zirat -> edge of Kara-Urman -> Marat voice -> Rinat says «Не отвечай» -> hard cut. Before the finale the player may only infer `clue_voice_answer_is_dangerous_hint`; `clue_do_not_answer_rule` is confirmed only by Rinat's action at the cliffhanger.
Consequences: Future scene, dialogue, quest and data work should implement this chapter before expanding to broader full-game lore. Do not add a full creature reveal, pact exposition, playable Kazan intro, mystical Alsu reveal or direct «answer / do not answer» branch to this MVP ending without a new decision.
Linked files: `chapter1_mvp_campaign.md`, `narrative.md`, `mvp_scope.md`, `gameplay.md`, `old_pc.md`, `language_learning.md`, `playtest_plan.md`, `mindmap.md`, `weak_points.md`

## 2026-05-23 — Strengthen Chapter 1 handoff after subagent review

Status: Accepted

Context: Five independent subagent reviewers found no P0 canon drift, but raised P1 handoff risks: route unlock depended too much on journal inference, Rinat's arrival needed clearer causality, Marat lacked a concrete personal marker, terminology mixed clues/keys/cards/flags, and the chapter did not state a save/load state contract.
Decision: Update `chapter1_mvp_campaign.md` with a handoff glossary, location glossary, 40-minute critical path, concrete route source (`doc_kara_urman_edge_sketch` plus `rinat_alerted`), Rinat causality beat, temporary Marat personal marker «Казанский, не отставай», state/save table and stricter language that Татарвики gives only a hypothesis before the final «Не отвечай».
Consequences: Downstream LLMs should no longer invent route unlocks from the journal alone, treat side flags as journal clues, copy unchecked татарские phrases as production text, or leave Marat's final voice generic. The personal marker remains replaceable during dialogue polish.
Linked files: `chapter1_mvp_campaign.md`, `open_questions.md`

## 2026-07-17 — Use campaign modules, transactional kernel and capability providers

Status: Accepted

Context: Итоговый сценарий и путь игрока ещё не зафиксированы. Текущий prototype уже содержит полезные data-driven островки — Markdown старого ПК, route graph, asset manifests и shared knowledge keys, — но сцены, переходы, эффекты, состояние и сохранения распределены между hardcoded владельцами. При таком устройстве новая сюжетная арка или уникальный квест требуют менять общий runtime.

Decision: Провести поэтапную модульную миграцию по пакету `../modular_migration/`. Portable-граница — JSON/Markdown, JSON Schema, immutable namespaced IDs и capability protocols. `CampaignManifest` компонует заменяемые модули сценария, персонажей, диалогов, квестов и ассетов. Runtime kernel становится единственным владельцем транзакций состояния; уникальные механики подключаются через изолированные capability providers. Активный run фиксирует exact module versions и campaign fingerprint; live hot-swap, implicit overrides и silent save fallback запрещены. Текущая Chapter 1 остаётся production baseline, но не зашивается в архитектуру навсегда.

Consequences: Сначала замораживаются контракты и проходят synthetic canary tests, затем отдельно мигрируются campaign data, активные сцены, old PC/DedOS и legacy MainMap. Pre-MVP gameplay saves сбрасываются при cutover, а имя пользователя и классифицированные preferences сохраняются. Стресс-квесты из пакета имеют статус `Proposal` и не меняют канон. Завершение миграции означает готовность архитектуры к достройке MVP, но не статус MVP.

Linked files: `../modular_migration/00_ORCHESTRATOR_README.md`, `../modular_migration/01_ARCHITECTURE_CONTRACT.md`, `../modular_migration/02_QUEST_STRESS_MATRIX.md`, `mindmap.md`, `backlog.md`, `technical_architecture.md`

## 2026-07-17 — Freeze MM-10 authority, canonical IDs and retirement baseline

Status: Accepted

Context: Before portable schemas can be implemented, current content/state/persistence owners need an explicit one-owner target and retirement packet. The audit also found mixed character tokens (`babay`, `char_babay`, `abi`, `char_abi`, `char_gulsina`), missing Chapter 1 character records and runtime reveal-timing drift that must not be copied into portable content.

Decision: Use the authority and retirement matrix in `../modular_migration/10_AUTHORITY_IDS_RETIREMENT_BASELINE.md`. Final runtime IDs use `moduleId:kind/localId` and have no legacy aliases. The one-time character normalization includes `babay → char_babay → urman.chapter1:character/mansur` and `abi/char_abi → char_gulsina → urman.chapter1:character/gulsina`. MM-50 must create accepted records for Айдар, Марат, Ринат, Наиля and Тимур хәзрәт; `rushania` is not silently merged with Наиля. Named campaign characters stay outside `urman.core`.

Decision: Internal duplicate owners retire delete-first in their assigned packets. The old route graph and asset manifests may remain only as rebuildable dev/provenance artifacts after production imports are removed. No permanent parser, alias, state or persistence fallback is allowed.

Decision: Close the remaining ID families deterministically. Every current `externalLocationNodes` token is rewritten directly to the explicit namespaced `SceneRef` listed in MM-10; no parallel handoff entity or runtime alias is introduced. The launch behavior migrates to a direct host launch of campaign `urman.chapter1` at its declared arrival/road entrypoint, while the hardcoded `chapter1` scene token and `ChapterScene` splash are deleted without a scene alias. Stale knowledge refs `dialogue_rinat_internal_register` and `dialogue_gulsina_home_words` are one-time rewrites to the actual dialogue IDs `rinat_internal_register` and `gulsina_yaramyy`, with no aliases.

Decision: Close the three additional inline route targets without aliases: `selsmag_counter_interior` becomes optional scene `urman.chapter1:scene/selsmag_counter_interior`; `zirat_general_late_evening` resolves to typed scene `urman.chapter1:scene/zirat` with route transition context; `multiple` is semantically renamed to `urman.chapter1:scene/crossroad_signs_inspect`. MM-50 owns these definitions/reference rewrites and MM-51 owns only their generic providers. The registered `ZiratMiniGame` is deleted unconditionally by MM-54 and is not a source for accepted zirat data or behavior.

Decision: The route-map, first10 and remaining asset manifests are provenance inventories, not ownership boundaries. Logical ownership follows the authored consumer: old-PC-only app/document/UI assets belong to `urman.oldpc`; accepted campaign route/location/character/journal/dialogue/vocabulary/phone/evidence assets belong to `urman.chapter1`; MainMap-only assets remain dev-only under `urman.legacy.mainmap`. `urman.core` requires an explicit content-neutral shared definition rather than a filename inference. Cross-module consumers reference the single owner's ID.

Decision: MM-70 is authorized to remove exactly `urman.mvp.save.v1`, `urman.oldPcHub.state.v1`, `game.notepad.files.v1` and `game.notepad.activeFile.v1` once, while preserving `urman_username` and explicitly classified user preferences. Broad `localStorage.clear()` is forbidden for production reset.

Consequences: Current early unlocks of `clue_do_not_answer_rule` are implementation drift, not canon. Portable Chapter 1 must keep only `clue_voice_answer_is_dangerous_hint` before the cliffhanger and confirm the rule only after Ринат acts. MM-20 may start only after the MM-10 verification record is green.

Linked files: `../modular_migration/10_AUTHORITY_IDS_RETIREMENT_BASELINE.md`, `chapter1_mvp_campaign.md`, `technical_architecture.md`, `open_questions.md`, `weak_points.md`

## 2026-07-18 — Connect the V2 persistence gateway through the only composition root

Status: Accepted bounded amendment

Context: MM-70 proved strict `GameSnapshotV2` persistence, one-time retirement of the four audited v1 keys and typed mismatch handling in isolation. Independent acceptance found that production boot still created only `Game → RuntimeBootstrap`; it never instantiated the gateway, therefore never applied the reset, restored V2 or presented the player with the required reset choice. Adding a storage side effect to the gateway would violate the one-owner composition boundary.

Decision: Reopen only the MM-54 composition seam. `main` injects the browser global into a named persistence-boundary factory; `Game` receives the resulting gateway, not raw storage. At boot it applies the exact v1 reset and displays its one-time notice, then validates V2 through the gateway before replacing the active runtime. A malformed, fingerprint-mismatched or provider-incompatible V2 snapshot shows a typed reset proposal; deleting the current V2 save requires an explicit player action. RuntimeBootstrap may supply only the codec inputs needed by the gateway and remains the owner of live runtime assembly. Static architecture checking permits browser storage access only inside that named boundary and rejects it everywhere else.

Consequences: No v1 parser, silent partial load, broad clear, secondary store or story-specific persistence branch is introduced. Username, explicitly classified preferences and Content Lab storage stay outside gameplay reset. This is technical wiring only: campaign data, canon, snapshot schema and capability protocols do not change.

Linked files: `../modular_migration/54_COMPOSITION_ROOT_CUTOVER.md`, `../modular_migration/70_LEGACY_PERSISTENCE_RETIREMENT.md`, `../modular_migration/55_WHOLE_PACK_CLOSURE_GATE.md`

## 2026-07-18 — Accept the modular migration architecture baseline

Status: Accepted

Context: MM-80 final verification follows the completion of portable schemas/compiler, kernel/capabilities, campaign migration, composition cutover, Content Lab, persistence retirement and their independent reviews. The final review required main-only browser assembly, a closed presentation facade, proposal-gated V2 deletion and a static factory-caller closure before acceptance.

Decision: Treat the modular architecture as the current production baseline. Portable JSON/Markdown modules and `CampaignManifest` own campaign content; `RuntimeKernel` owns progression state; unique mechanics use exact-version capability providers; `GameSnapshotV2` locks campaign fingerprint; `main` alone assembles the browser runtime/persistence boundary. Content Lab and MainMap remain dev-only. Old state/storage/parser paths are retired without aliases or fallback.

Consequences: New arcs, side quests, NPCs, dialogue and assets on existing mechanics are data-only changes. A new mechanic requires a capability package and catalog entry, not a kernel change. This records architecture readiness to build the MVP; it does not change canon or assert product/MVP release readiness.

Linked files: `../modular_migration/00_ORCHESTRATOR_README.md`, `../modular_migration/80_FINAL_VERIFICATION_KB_HANDOFF.md`, `technical_architecture.md`, `mvp_completion_handoff.md`, `mindmap.md`, `backlog.md`

## 2026-07-17 — Amend portable compiler contracts after MM-30 review

Status: Accepted

Context: Independent MM-30 spec review found that the frozen schemas could not represent compile-time reveal order, composition-time capability resource conflicts, or the Markdown body that must reach the browser pack. Implementing these as compiler heuristics would create hidden narrative and mechanic hardcode.

Decision: Reopen only the affected MM-01/MM-20 seams. Campaigns require unique `narrativeOrder` and closed `required-reachable` / `reveal-not-before` invariants. Selected capability providers declare typed `resourceClaims`; any selected collision with an exclusive claim is fatal. Markdown definitions retain normalized non-empty `bodyMarkdown` in the compiled `DocumentDefinition`. Compiler reference traversal follows resolved schema `ContentId` fields with explicit declaration semantics, and missing explicit module selection is fatal. MM-30 remains blocked until an independent bounded MM-03 re-freeze passes.

Consequences: Chapter 1 will eventually encode beat `rinat_do_not_answer` before the final clue in `narrativeOrder`; this records existing canon timing and does not introduce a new plot decision. No general scripting, resource ECS, runtime glob, or separate ResourceDefinition is added.

Linked files: `../modular_migration/01_ARCHITECTURE_CONTRACT.md`, `../modular_migration/03_ARCHITECTURE_FREEZE_GATE.md`, `../modular_migration/20_PORTABLE_CONTENT_SCHEMAS.md`, `../modular_migration/30_CONTENT_COMPILER_WEB_ADAPTER.md`

## 2026-07-17 — Add immutable RuntimeContext claim query for capability cleanup proof

Status: Accepted

Context: MM-40 quality review proved that a capability host cannot treat a generic `CommandResult.status === committed` as evidence that its kernel-owned resource claims were actually released. A cleanup handler can commit a different plan; stopping the provider in that state would leave a dead capability as a resource owner. Creating a local claim registry would violate the single-writer and single-owner boundaries.

Decision: Amend the frozen runtime contract with exactly one read-only seam: `RuntimeContext.query(readModelSelector)`. It receives an immutable snapshot containing only `state` and the kernel-owned claim projection. `select` remains state-only; query exposes no mutable store, occurrence ledger, provider session, new command/effect type or direct state write. Capability cleanup must query that its `capabilityInstanceId` has no remaining claims after the cleanup transaction commits and before `stop/dispose`. The MM-03 12-fixture gate is rerun before the amended contract becomes frozen again.

Consequences: Capability cleanup can prove release without a second source of truth. The query seam is generic and content-neutral; it changes no Chapter 1 canon, campaign/module data, snapshot payload, composition root or legacy behavior. Future expansion of the runtime read model requires a new recorded decision and re-freeze.

Linked files: `../modular_migration/01_ARCHITECTURE_CONTRACT.md`, `../modular_migration/03_ARCHITECTURE_FREEZE_GATE.md`, `../modular_migration/31_RUNTIME_KERNEL_REGISTRIES.md`, `../modular_migration/40_QUEST_WORLD_CAPABILITIES.md`, `mindmap.md`

## 2026-07-17 — Portable old-PC document metadata

Status: Accepted bounded amendment; `MM-03 OLDPC REFREEZE PASS`.

Context: MM-52 must replace the old frontmatter parser with normal `ContentRegistry` records, but the portable document contract previously retained only authoring body and canonical conditions/effects.

Decision: `DocumentDefinition.oldPc` is an optional closed object containing exactly `type`, `pcSection`, `canonStatus`, `reliability`, `searchTerms`, `suggestedTerms`, derived from current authored old-PC records. `accessConditions` and `openEffects` remain the only canonical progression fields. Provenance/UI-only fields such as `sourceKind`, `inWorldSource`, `dangerLevel` and layout are not portable metadata. `urman.oldpc` is the sole full Markdown/search owner; Chapter 1 keeps only its existing evidence presentations.

Consequences: No new parser, legacy alias, state owner or narrative decision is introduced. MM-52 remains gated until independent schema/architecture review confirms the amendment and generic runtime stays browser-free.

Linked files: `../modular_migration/20_PORTABLE_CONTENT_SCHEMAS.md`, `../modular_migration/03_ARCHITECTURE_FREEZE_GATE.md`, `../modular_migration/52_OLD_PC_DEDOS_MIGRATION.md`

## 2026-07-17 — Close legacy compile graph and expose capability presentation through a bounded port

Status: Accepted bounded integration amendment; re-freeze required before MM-54 wires it.

Context: MM-54 inventory showed that dead legacy scenes, UI and systems still import the old `GameState`, `SceneManager` and `src/data/**`; excluding them from TypeScript would conceal a second implementation instead of retiring it. The same inventory showed that `CapabilityHost` correctly owns the old-PC session lifecycle but exposes only `handle` and snapshot APIs, while the web-only DedOS adapter requires a read model and a disposable subscription.

Decision: MM-54 deletes the explicit stale compile closure listed in its task contract together with the old owners; it does not add a `Game` façade or tsconfig exclusion. `MM-70` remains responsible for the reset/storage gateway and validators, even if some obsolete UI/system files no longer exist. Add one generic capability presentation port: only a started exact session that explicitly implements it may expose immutable render data, typed input forwarding through the host, and disposable subscriptions. The host never exposes a raw provider session, runtime context, kernel state or a direct state writer. The old-PC provider is the first consumer; the seam is content-neutral and optional for other capabilities.

Consequences: Production old PC can be injected from the composition root without a second UI/progression owner. A capability session stays terminal after host stop/dispose; presentation handles must then reject and subscriptions must not survive. This is an implementation-boundary change only: it changes no campaign data, canon timing, asset or quest mechanics.

Linked files: `../modular_migration/03_ARCHITECTURE_FREEZE_GATE.md`, `../modular_migration/40_QUEST_WORLD_CAPABILITIES.md`, `../modular_migration/52_OLD_PC_DEDOS_MIGRATION.md`, `../modular_migration/54_COMPOSITION_ROOT_CUTOVER.md`, `mindmap.md`

## 2026-07-18 — Retire unowned legacy DedOS chat

Status: Accepted bounded retirement.

Context: The old DedOS chat stored contacts, messages, татарский glosses and plot hints directly in `src/data/chat_data.ts` and a separate unused JSON payload. It was not represented by the portable schemas, had no selected module or campaign owner, and its hardcoded story/language text had not passed the canonical Chapter 1 route or language-review gates.

Decision: Delete the chat data, provider, renderer, unused JSON payload, exact DedOS launcher and exact app descriptor. Do not migrate, alias, hide or replace it. The compiled `urman.oldpc` archive capability remains the sole production old-PC narrative owner; generic communication UI can be introduced only later through a separately authored portable module and an explicit decision.

Consequences: This removes a second narrative/language source and lets MM-54 retire the remaining `src/data/**` closure without a chat consumer. It changes no Chapter 1 canon, clue timing, asset contract or persistent user data.

Linked files: `../modular_migration/52_OLD_PC_DEDOS_MIGRATION.md`, `../modular_migration/54_COMPOSITION_ROOT_CUTOVER.md`, `mindmap.md`

## 2026-07-18 — Atomic portable scene-to-dialogue handoff

Status: Accepted bounded amendment; independent MM-03 re-freeze pending.

Context: The Chapter 1 register defined Ринат's causal dialogue but had no portable, typed edge to start it. A direct host/campaign branch would split the scene transaction and make the dialogue start effects replayable.

Decision: A scene interaction may target exactly one `targetSceneId` or `targetDialogueId`. For a dialogue target, the generic scene provider resolves the dialogue start node and commits its conditions/effects in the originating atomic action; only a successful commit emits `{ fromSceneId, targetDialogueId, entryAlreadyCommitted: true }`. The generic dialogue provider then renders that node without a second entry dispatch. Chapter 1 uses this only for internal register → Ринат and gates the subsequent saved-message edge on `rinat.alerted`.

Consequences: The first register visit is allowed through typed `not npc.state(alerted)`; replay is blocked without an implicit hidden-state assumption. The early dialogue may establish only the existing hypothesis, never the final «Не отвечай» rule, which remains forest-only. This adds no canonical lore, campaign-ID branch, alias, direct state write or kernel change.

Linked files: `../modular_migration/01_ARCHITECTURE_CONTRACT.md`, `../modular_migration/03_ARCHITECTURE_FREEZE_GATE.md`, `../modular_migration/20_PORTABLE_CONTENT_SCHEMAS.md`, `../modular_migration/30_CONTENT_COMPILER_WEB_ADAPTER.md`, `../modular_migration/50_CAMPAIGN_DATA_MIGRATION.md`, `../modular_migration/51_ACTIVE_RUNTIME_MIGRATION.md`, `mindmap.md`, `backlog.md`
