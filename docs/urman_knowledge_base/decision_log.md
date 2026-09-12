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

## 2026-08-14 — Narrow current delivery to an Act 1 atmospheric demo

Status: Accepted for the current product milestone

Context: Полный перенос и производство пятиактной игры создают слишком широкий
критерий, чтобы честно проверить атмосферу, темп и понятность первого часа.

Decision: Текущая активная цель — играбельная first-person демо-сборка первого
акта на Godot. `game/scenes/act1_demo.tscn` становится default launch;
маршрут проходит через пять компактных зон, 16 Chapter 1 beats и заканчивается
постановочным «НЕ ОТВЕЧАЙ» / «Конец демо». Демо использует существующие общие
runtime/content/save owners и не создаёт отдельный story state.

Boundary: Acts 2–5, полный 6–8-часовой playthrough, full-game asset production,
release-class Windows/M1 acceptance и web retirement deferred. Это изменение
продуктового объёма текущего этапа, а не изменение канона, финала или удаления
старых исходников и browser saves.

Linked files: `game/scripts/Act1DemoRoot.cs`, `game/scenes/act1_demo.tscn`,
`docs/urman_knowledge_base/execution_backlog.json`, `roadmap.md`,
`release_gate_matrix.md`, `playtest_plan.md`

## 2026-08-14 — Replace the active full-game goal with an Act 1 demo goal

Status: Accepted by the user for the current delivery cycle

Decision: Treat `urman-act1-atmospheric-demo` as the active goal, not merely as
an intermediate milestone. The current done-when boundary ends after a clean
first-person Act 1 launch, the 16-beat arrival-to-cliffhanger flow, persistence
and input smoke, and three Godot style-frame receipts. Do not continue this
goal into Acts 2–5 production, the full 6–8-hour playthrough, release-host
acceptance, web retirement or final art lock.

The complete Godot/C# migration remains documented as deferred long-term work;
existing full-game scenes, source assets, browser saves and the web oracle are
preserved. This is a scope change, not a story rewrite or a deletion request.

Linked files: `docs/aegis/work/2026-08-10-godot-full-migration/10-intent.md`,
`docs/urman_knowledge_base/execution_backlog.json`, `mvp_scope.md`,
`roadmap.md`, `playtest_plan.md`

## 2026-08-14 — Require screen-space proof for authored Act 5 GateA placement

Status: Accepted bounded production-evidence repair; art acceptance remains OPEN

Context: The runtime-backed boundary receipt proved GateA's world-space
clearance and both compiled interaction IDs, but a world-distance check alone
could not prove that the choice markers remained visible from a first-person
camera.

Decision: Keep the change test-only. `FullGameBoundaryRuntimeCapture` now
records a standard composition and a dedicated marker-camera projection. The
marker camera must keep both runtime interaction anchors inside the 1 920×1 080
viewport and must report no projected GateA overlap; the harness still performs
no story dispatch, save write, material/shader mutation or collision-owner
change. The standard camera remains diagnostic and is allowed to overlap the
marker projection because it is a distant composition check.

Evidence: `./eng/capture-fullgame-boundary-runtime.sh` passes on Metal 4.0 /
Forward+ with 2/2 PNGs, 8 visible GateA LOD meshes and 2.687 m minimum world
clearance. The receipt and wrapper retain the explicit OPEN gates for observed
traversal, repetition, cultural review, release hardware and art lock.

Linked files: `game/tests/FullGameBoundaryRuntimeCapture.cs`,
`eng/capture-fullgame-boundary-runtime.sh`,
`docs/urman_knowledge_base/art/fullgame_boundary_runtime_capture/`,
`docs/urman_knowledge_base/execution_backlog.json`
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

## 2026-08-10 — Fix the game presentation as walkable first-person 3D

Status: Accepted; exact stylized low-poly treatment remains Proposed pending a production art test.

Context: The static-node / discrete-route prototype reduced production cost, but it no longer matches the intended player experience. The target is a real three-dimensional world in which the player stands inside Кырлай, looks around continuously and moves through compact locations. Illustrated route frames that imply 3D movement are not an acceptable substitute.

Decision: Use a fully walkable 3D presentation with a first-person camera as the canonical target for the MVP and the full game. Preserve the detective loop, data-driven narrative architecture, deep 2D interfaces for the old PC, documents and journal, sound-first tension and compact location scope. Do not add combat, an open world or complex traversal by default.

Decision: Supersede the 2026-05-16 static-node hybrid and in-world discrete-route decisions as production direction. Existing route screens, graphs, manifests and painted locations remain historical prototype/provenance material and may inform composition, landmarks, textures or UI; they are not a parallel runtime owner or a fallback presentation target. This decision does not authorize deleting those artifacts.

Proposal: Test a polished stylized low-poly treatment rather than primitive PS1 minimalism: strong authored silhouettes, restrained geometry, hand-painted low-resolution materials, selective higher-detail hero props, controlled lighting, fog and a muted palette. The ink-wash principles «обычность сначала, неправильность потом», handmade texture and cultural specificity remain valid, but the exact shader, texture density and polygon budgets must be fixed only after an in-engine style frame.

Consequences: World assets must become modular 3D environment kits, props, materials, foliage and collision-ready scene pieces. Character presentation needs a separate first-person production decision; portraits may remain in dialogue/UI. Engine choice remains open until a separate technical decision.

Linked files: `gameplay.md`, `design_style.md`, `assets.md`, `technical_architecture.md`, `open_questions.md`, `weak_points.md`, `mindmap.md`

## 2026-08-10 — Accept Painterly Low-Poly 3D and the Godot/C# production stack

Status: Accepted

Context: The first-person target required a reproducible engine and art pipeline. Three visual tests separated primitive PS1 minimalism, production-safe polished low-poly and a more expensive painterly treatment. The intended result combines the geometry budget of the second direction with the light, fog and handmade material accents of the third.

Decision: Name the canonical art direction **Painterly Low-Poly 3D**. Use readable restrained geometry, hand-painted albedo/roughness, selective hero detail, muted natural colors, baked lighting, limited realtime shadows and localized fog. Do not use pixelation, photorealistic microdetail, permanent VHS/chromatic aberration, heavy outlines or generic monster-horror decoration. Concept images are targets only; art lock requires captures from a walkable Godot scene.

Decision: Use Godot 4.7.1 .NET with C# and .NET 10 LTS. macOS and Windows are release targets; Linux remains compatible but is not a release gate. Browser and mobile are out of scope. Blender 4.5 LTS is the source tool for scripted 3D asset production, with `.glb` as the derived engine import format.

Decision: Rewrite the content compiler, Content Lab, runtime kernel, capabilities and persistence in C#. `Urman.Core` and `Urman.Content` remain free of Godot dependencies; `Urman.Godot` is the only presentation/world/input/audio owner. The current web runtime stays a read-only parity oracle until final cutover. No JS/C# runtime bridge, permanent fallback or second state owner is permitted.

Decision: Introduce `SaveGameV3` without a V2 importer or browser-localStorage reader. Do not delete existing browser save data. New desktop saves are atomic under `user://` and include campaign lock, runtime/capabilities, world checkpoint, player transform, settings and playtime.

Consequences: All future technical and asset tasks target this stack. Existing route screens and Three.js runtime are historical/provisional until retirement, not alternative release paths. Tool upgrades require an explicit decision and regression pass.

Linked files: `design_style.md`, `technical_architecture.md`, `assets.md`, `gameplay.md`, `open_questions.md`, `weak_points.md`, `../aegis/plans/2026-08-10-godot-full-migration.md`

## 2026-08-10 — Fix the full-game scope and canonical tragic ending

Status: Accepted

Context: The knowledge base contained a five-act outline but treated the final choice as unresolved and described the complete game only as a future possibility. Full 3D production needs one release scope and one narrative target before acts 2–5 receive expensive scenes and assets.

Decision: The first complete release targets 6–8 hours across five acts. Act 1 remains the accepted Chapter 1 lock. Acts 2–4 reveal the double village system, the practical pact, Марат/Баранов/Су Анасы and the 1913/1552 historical layers. Act 5 ends with one canonical outcome: Айдар knowingly destroys the unjust pact after understanding both its protection and its inherited violence.

Decision: Treat the ending as tragic truth. Destroying the pact is neither uncomplicated liberation nor a foolish bad-ending mistake: Кырлай loses protection and enters an uncertain future, while the old system of fear and silence becomes impossible to preserve. Local choices alter relationships, evidence access and epilogue staging but do not create alternate endings.

Decision: The game has no combat and no systemic stealth loop. Rare authored threat sequences may require hiding, leaving a bounded zone or refusing a dangerous response. Release controls include keyboard/mouse and gamepad with remapping. Russian is the only required UI/subtitle language for the first release; татарский remains gameplay data and requires consultant review.

Consequences: Each act needs a production lock — beat sheet, clue graph, zones, NPCs, documents, language keys, world variants and threat beats — before final 3D asset production. The previous multi-ending outline is superseded.

Linked files: `narrative.md`, `gameplay.md`, `roadmap.md`, `playtest_plan.md`, `mindmap.md`, `open_questions.md`, `weak_points.md`

## 2026-08-10 — Store portable input bindings in unreleased SaveGameV3

Status: Accepted bounded amendment

Context: Desktop controls require keyboard/mouse and gamepad remapping, but `GameSettingsSnapshot` previously stored only the name of the last input device. Godot `InputMap` alone cannot restore a player's choices after loading a save.

Decision: Add a portable `InputBindingSnapshot` list to the current `GameSettingsSnapshot`: logical action, physical keyboard keycode and optional gamepad button or axis/sign pair. `Urman.Core` validates uniqueness and shape; `Urman.Godot.InputBindingService` is the sole adapter to and from Godot `InputMap`. Rebinding a key/button/axis replaces only the same action's corresponding Godot events and preserves mouse bindings and unrelated actions. Update SaveGameV3 in place because it has not shipped; do not add a compatibility fallback, V2 importer or second settings store.

Consequences: FOV, sensitivity, head bob, graphics preset, last device and remapped keyboard/gamepad button/axis bindings round-trip together. Existing development-only V3 files without `inputBindings` are intentionally incompatible and may be discarded; browser saves remain untouched. Settings UI captures a stable axis direction after a 0.45 dead-zone threshold; accessibility controls are covered by the separate SaveGameV3 contract below.

Linked files: `technical_architecture.md`, `playtest_plan.md`, `backlog.md`, `../aegis/work/2026-08-10-godot-full-migration/20-checkpoint.md`

## 2026-08-11 — Add the authored full-game C# campaign adapter without overstating production readiness

Status: Accepted implementation boundary; art/production lock remains open

Context: Acts 2–5 now have a compiled `urman.fullgame` campaign with 46 authored narrative beats, but Godot still needs a walkable presentation seam before production assets can replace procedural placeholders. A direct runtime smoke had also exposed that a missing namespaced interaction could fall through to a generic `world.interact` command and appear green.

Decision: Keep `content/modules/urman-fullgame` and `content/campaigns/urman.fullgame` as the authoritative data layer. `FullGameZone` may materialize compact procedural geometry and one `InteractionTarget` for every compiled interaction, while `RuntimeBridge` remains the only progression owner. Full-game smoke must assert both compiled interaction availability and the corresponding physical Godot target; missing authored IDs are failures, not generic progression commands. This adapter is a production foundation, not acceptance of final 6–8-hour art, sound or cultural content.

Consequences: Acts 2–5 can be traversed in Godot with the canonical tragic epilogue and one shared runtime state. The provisional zone presentation must be replaced by production-grade authored scenes, hero props, NPCs, audio and cultural review before release. The old web runtime remains a read-only oracle and no V2/browser save compatibility is added.

Linked files: `content/modules/urman-fullgame/definitions.json`, `content/campaigns/urman.fullgame/campaign.json`, `game/scripts/FullGameZone.cs`, `game/scripts/RuntimeBridge.cs`, `game/tests/FullGameFlowSmokeTest.cs`, `technical_architecture.md`, `roadmap.md`, `playtest_plan.md`

## 2026-08-11 — Isolate Acts 2–5 zone dressing from the narrative adapter

Status: Accepted bounded presentation implementation; final art/production lock remains open

Context: The authored full-game campaign could already traverse 12 zones, but one generic procedural forest composition made that evidence look like a route adapter rather than a production path. Adding more geometry directly to `FullGameZone` would mix presentation composition with the existing interaction bridge.

Decision: Keep `FullGameZone` responsible for environment shell, compiled interaction materialization and zone lifecycle. Put zone-specific modular composition in the presentation-only `FullGameZoneDressing` owner, keyed only by the logical `ZoneId`. It may add meshes, materials and local story lights, but it cannot read or mutate narrative state. Full-game smoke must require a `ProductionDressing` node for every traversed zone, and the capture script records representative renders separately from the three mandatory style-lock frames.

Consequences: Acts 2–5 now have distinct river, archive, Soviet file-room, pact-ledger and boundary silhouettes while preserving the single runtime owner. The pass remains greybox production evidence; final houses, hero props, NPCs, authored audio, LOD/performance and cultural review are still release gates. No web fallback, save compatibility path or content ID is introduced.

Linked files: `game/scripts/FullGameZoneDressing.cs`, `game/scripts/FullGameZone.cs`, `game/tests/FullGameFlowSmokeTest.cs`, `game/tests/FullGameDressingCapture.cs`, `eng/capture-fullgame-frames.sh`, `technical_architecture.md`, `assets.md`, `playtest_plan.md`

## 2026-08-11 — Give every full-game zone an authored Godot scene wrapper

Status: Accepted bounded presentation implementation; final art/level lock remains open

Context: The full-game adapter already keyed dressing and interactions by 12 logical zone IDs, but `Main` loaded one generic `fullgame_zone.tscn` for every transition. That weakened the compact-zone production boundary and made it too easy to miss a scene-specific import or future authored override.

Decision: Keep `FullGameZone.cs` as the single runtime/presentation adapter, but expose one authored `PackedScene` wrapper per Acts 2–5 zone under `game/scenes/zones/fullgame/`. Each wrapper serializes exactly one logical `ZoneId`; `Main` and capture tooling resolve those paths explicitly. The generic scene remains a development smoke fixture, not a transition fallback.

Consequences: Scene loading now proves the 12-zone boundary without duplicating narrative state or adapter logic. Full-game smoke must load every wrapper and still traverse the same 46 beats. Production geometry, NPCs, audio, LOD and cultural review remain separate gates.

## 2026-08-11 — Anchor full-game interactions to authored presentation props

Status: Accepted bounded presentation implementation; final level/art lock remains open

Context: Separate zone scenes still materialized interactions on one generic index-based grid. That made a dock, archive table, pact ledger and boundary marker visually unrelated to the action the player was being asked to perform.

Decision: Add `FullGameInteractionLayout` as a presentation-only logical-ID-to-position table. `FullGameZone` uses it only when placing physical `InteractionTarget` nodes and marks the result with `authored-zone-anchor`; unknown future interactions retain a deterministic fallback position but full-game smoke fails if the required authored marker is absent. The table cannot dispatch commands or mutate state.

Consequences: The same compiled interactions remain the progression owner, while the first-person affordances now sit beside the zone-specific props. Production meshes and final interaction animations can replace these anchors without changing IDs, saves or kernel contracts.

## 2026-08-11 — Generate deterministic LOD1 variants in the Blender source pipeline

Status: Accepted production-pipeline implementation; scene visibility and final art review remain open

Context: The modular Blender kit recorded LOD1 as pending, although the Godot production budget requires a repeatable lower-detail path for environment, foliage and hero-prop families.

Decision: `tools/blender/generate_modular_environment.py` duplicates every visible `_LOD0` mesh into a tagged `_LOD1` variant and applies deterministic Decimate ratios (0.35 for vegetation, 0.50 for general environment/props, 0.65 for the old-PC fallback). The `.blend`, `.glb` and `assets/asset_registry.json` record provenance and the remaining scene-specific visibility-range decision. Collision meshes are never duplicated as render LODs.

Consequences: Rebuilding the project-original kit now produces 21 inspectable LOD1 meshes without changing logical asset IDs or claiming final art quality. Godot scene visibility ranges, billboard treatment and final collision QA remain release gates; no runtime fallback or second asset owner is introduced.

## 2026-08-11 — Route full-game physical documents through the shared kernel and journal

Status: Accepted bounded gameplay/presentation implementation; authored content and cultural review remain open

Context: The full-game Markdown documents were already compiled and referenced by knowledge entries, but Acts 3–4 exposed only generic interaction anchors. A player could traverse the zones without reading the Baranov, Tukay, 1552 or pact documents in the 3D world, so the journal/evidence chain was still split between the old PC and the new presentation.

Decision: Allow one data-driven `targetDocumentId` on a scene interaction. `CompiledCampaignRepository` resolves the document and its `accessConditions`/`openEffects`; `RuntimeBridge` applies those effects plus `document.open` through `ContentApply`, and `DocumentUi` renders the compiled Markdown. «В журнал» dispatches the existing typed `journal.record` command; duplicate records remain idempotent. `ContentRuleEngine` also accepts `document.open` and `journal.record` effects for future authored content, while the old PC keeps its explicit save behavior.

Consequences: Four full-game documents are physically anchored beside their authored props and the full-game smoke proves open → presentation state → journal record. No second state owner, browser persistence path or V2 compatibility branch is introduced. Final typography, highlighted terms, richer document metadata, authored art and cultural review remain production gates.

Linked files: `content/schemas/scene.schema.json`, `content/schemas/condition-effect.schema.json`, `content/modules/urman-fullgame/definitions.json`, `game/scripts/CompiledCampaignRepository.cs`, `game/scripts/RuntimeBridge.cs`, `game/scripts/DocumentUi.cs`, `game/tests/FullGameFlowSmokeTest.cs`, `technical_architecture.md`, `gameplay.md`, `backlog.md`

## 2026-08-11 — Integrate the generated Blender kit with explicit Godot LOD ranges

Status: Accepted bounded production-pipeline implementation; collision, final art and release-hardware review remain open

Context: The project-original Blender `.blend` and derived `.glb` already contained deterministic LOD1 meshes, but no full-game zone consumed the import. The asset registry therefore could not prove that the lower-detail variants or their runtime visibility policy were usable in the first-person world.

Decision: Add the presentation-only `GeneratedModularKitDressing` adapter. It loads `res://assets/generated/urman_modular_kit.glb`, selects the `HouseA_`/`FenceA_` family for the Act 2 house, the `TableA_`/`OldPc_` family for the Act 3 Soviet archive and the `PineA_` family for the Act 5 forest boundary, aligns each kit to an authored world anchor, and applies self-fade ranges of `0–24m` for LOD0 and `18–72m` for LOD1. Blender `-col` objects are not treated as physics by the GLB import; ground/zone colliders remain authoritative and the adapter adds only provisional layer-2 proxy colliders until a dedicated collision test passes.

Consequences: Three walkable full-game zones now exercise the production `.glb` rather than only procedural placeholders, and the full-game smoke asserts selected meshes, exact visibility ranges, layer-2 proxy shape presence and collision policy without allowing the interaction ray to hit those proxies. Six Godot 1920×1080 dressing captures record the house, Soviet and forest-boundary kit frames alongside the existing zone evidence. This remains greybox production evidence: it does not accept final art, billboard treatment, imported collision or release-hardware performance.

Linked files: `game/scripts/GeneratedModularKitDressing.cs`, `game/scripts/FullGameZoneDressing.cs`, `game/tests/FullGameFlowSmokeTest.cs`, `game/tests/FullGameDressingCapture.cs`, `assets/asset_registry.json`, `docs/urman_knowledge_base/art/fullgame_frames/README.md`, `technical_architecture.md`, `assets.md`, `backlog.md`, `weak_points.md`

## 2026-08-11 — Add deterministic NPC presentation proxies and a collision contract smoke

Status: Accepted bounded production implementation; authored character and mesh-collision lock remain open

Context: The full-game zones had environment silhouettes and physical interaction anchors, but they still read as empty greybox spaces. Replacing them immediately with final character GLBs would conflate a useful composition test with an unreviewed asset pipeline. The imported Blender kit also needed a repeatable floor/proxy collision check before any procedural collider is retired.

Decision: Keep `FullGameNpcDressing` as a presentation-only owner that creates deterministic low-poly proxy silhouettes for the canonically present family, village, mosque, council, archive and pact characters. Proxies have no `CollisionObject3D`, no interaction ownership and no access to `RuntimeBridge`; their metadata explicitly marks them as temporary until original character GLBs, faces, clothing and animation are authored. Add `CollisionQaSmokeTest` to instantiate all 12 full-game zones, query a layer-1 floor and verify any generated kit proxy is layer 2 with mask 0. This smoke is a minimum physics contract, not acceptance of final authored mesh colliders.

Consequences: Full-game captures and internal movement tests now contain deterministic human scale/context without adding a second narrative owner. Basic floor/proxy regressions fail in CI-like Godot verification, while imported `-col` render meshes remain hidden from physics. Final NPC assets, animation, audio, cultural presentation and mesh-level collision review remain release gates; no web fallback or save compatibility path is introduced.

Linked files: `game/scripts/FullGameNpcDressing.cs`, `game/scripts/FullGameZoneDressing.cs`, `game/tests/FullGameFlowSmokeTest.cs`, `game/tests/CollisionQaSmokeTest.cs`, `game/tests/collision_qa_smoke_test.tscn`, `eng/verify-godot.sh`, `technical_architecture.md`, `assets.md`, `backlog.md`, `weak_points.md`, `playtest_plan.md`

## 2026-08-11 — Replace full-game NPC primitives with a project-original Blender character kit

Status: Accepted bounded production-pipeline implementation; face, clothing-detail, animation, audio and cultural presentation remain open

Context: Primitive NPC silhouettes were useful for scale and composition, but they were not a real 3D asset production path. The accepted target requires Blender-authored, project-original assets with provenance and LODs while keeping interaction and narrative ownership in the C# runtime.

Decision: Add `tools/blender/generate_character_kit.py` and `verify_character_kit.py`. The initial generator produced `assets/source/blender/urman_character_kit.blend` and `game/assets/generated/urman_character_kit.glb` with nine named character prefixes, ground anchors, 49 deterministic LOD0/LOD1 meshes, project-original materials and no collision meshes. `GeneratedCharacterKitDressing` selects one prefix per `FullGameNpcDressing` placement, aligns it to the authored ground anchor and applies Godot self-fade ranges of `0–18m` for LOD0 and `14–48m` for LOD1. Missing or malformed prefixes are hard failures; there is no primitive-character fallback in the production path. The 2026-08-11 detail pass supersedes the mesh count while preserving this ownership contract.

Consequences: Full-game zones now exercise a rebuildable character GLB rather than runtime-generated primitive bodies, and the Godot smoke asserts prefix selection, anchors, materials, LOD ranges, no character collision objects and shared interaction-layer boundaries. This is a production silhouette kit, not final facial or animation art; authored detail, animation/audio, cultural review and final capture acceptance remain gates. Environment kit and character kit remain separate assets with independent provenance; no web fallback or save compatibility path is introduced.

Linked files: `tools/blender/generate_character_kit.py`, `tools/blender/verify_character_kit.py`, `assets/source/blender/urman_character_kit.blend`, `game/assets/generated/urman_character_kit.glb`, `game/scripts/GeneratedCharacterKitDressing.cs`, `game/scripts/FullGameNpcDressing.cs`, `game/tests/FullGameFlowSmokeTest.cs`, `eng/verify-assets.sh`, `assets/asset_registry.json`, `technical_architecture.md`, `assets.md`, `backlog.md`, `roadmap.md`

## 2026-08-11 — Add face landmarks and layered clothing to the character kit

Status: Accepted bounded production-progress pass; facial expression, animation, audio and cultural review remain open

Context: The first generated character GLB proved prefix selection, anchors, LOD ranges and collision boundaries, but its five-piece silhouettes were too generic for a serious Acts 2–5 composition pass. Replacing them with external models would violate provenance and introduce a second asset owner.

Decision: Extend the project-original Blender generator with deterministic sleeves, trousers, boots, eye/nose/mouth landmarks and role-specific beard/hat details. Keep one prefix per canonically placed NPC, no collision meshes and the same Godot LOD ranges. The verifier records the matching generated LOD count from scene metadata and requires face landmarks plus the layered-clothing policy instead of hard-coding the previous 49-mesh count.

Consequences: The rebuilt GLB now contains 143 matching LOD0/LOD1 meshes across nine prefixes and Godot material routing keeps face landmarks readable without changing narrative or interaction ownership. This is a production-progress asset, not final facial expression or animation art; authored animation/audio, mesh-collision review, release hardware and cultural presentation remain separate gates.

Linked files: `tools/blender/generate_character_kit.py`, `tools/blender/verify_character_kit.py`, `assets/source/blender/urman_character_kit.blend`, `game/assets/generated/urman_character_kit.glb`, `game/scripts/GeneratedCharacterKitDressing.cs`, `game/scripts/FullGameNpcDressing.cs`, `game/tests/FullGameFlowSmokeTest.cs`, `assets/asset_registry.json`, `assets.md`, `backlog.md`, `weak_points.md`, `roadmap.md`

## 2026-08-11 — Use a repository execution backlog as the orchestrator queue

Status: Accepted workflow decision

Context: `set_goal` correctly stores the durable outcome and non-goals, but the full Godot migration also needs dependency ordering, bounded ownership, explicit verification and resumable evidence. A second external tracker would easily drift from the repository's canonical narrative and architecture documents.

Decision: Keep the active goal as the north-star contract and add `docs/urman_knowledge_base/execution_backlog.json` as the machine-readable execution queue. `backlog.md` remains the human-readable index; `roadmap.md` remains the milestone narrative; `decision_log.md`, `open_questions.md`, `technical_architecture.md` and `docs/aegis/work/` remain authorities/evidence. Every executable task has a stable ID, status, dependencies, owner, scope, deliverable, `done_when`, `verify` and evidence links. The orchestrator may claim only `ready` tasks whose dependencies are completed, and may not perform the final web retirement before the full release gate.

Consequences: Work can be resumed or delegated without turning the goal into a flat checklist, while canonical lore and architecture keep one source of truth. The queue is intentionally repository-local; an external task service may mirror it later only if it preserves these IDs and does not become a competing owner. Historical backlog entries remain audit material and are not automatically executable.

Linked files: `docs/aegis/work/2026-08-10-godot-full-migration/10-intent.md`, `docs/urman_knowledge_base/execution_backlog.json`, `docs/urman_knowledge_base/backlog.md`, `docs/urman_knowledge_base/roadmap.md`, `docs/urman_knowledge_base/README.md`, `docs/aegis/work/2026-08-10-godot-full-migration/20-checkpoint.md`

## 2026-08-11 — Make accessibility settings a shared SaveGameV3 and Godot presentation contract

Status: Accepted technical foundation; external accessibility and cultural review remain release gates

Context: The first-person slice already persisted FOV, sensitivity, graphics and input remapping, but accessibility behavior was not yet a single cross-layer contract. If each UI implemented its own flags, reduced motion and text/audio alternatives could drift from the settings saved with the player.

Decision: Add `AccessibilitySettingsSnapshot` to the unreleased `GameSettingsSnapshot` inside `SaveGameV3`. It stores reduced motion, high contrast, text scale (`0.8–1.6`), subtitles and audio descriptions with safe defaults. `AccessibilityPresentation` is the only Godot fan-out adapter: first-person head bob consumes reduced motion; dialogue, journal, document, old-PC and settings panels consume text scale/high contrast; `AudioCueUi` selects authored captions or the equivalent non-audio cue. No second settings store, browser importer or V2 compatibility path is introduced.

Consequences: C# codec tests prove roundtrip and reject unsupported text scales; Godot scene/player smoke proves the controls and reduced-motion override are wired through the real settings scene. This does not claim final accessibility quality: external motion-comfort, 1080p/M1/Windows readability, input-device parity and cultural review of text descriptions remain required before release.

Linked files: `src-dotnet/Urman.Core/Persistence/SaveGameV3.cs`, `game/scripts/AccessibilityPresentation.cs`, `game/scripts/FirstPersonController.cs`, `game/scripts/SettingsUi.cs`, `game/scripts/AudioCueUi.cs`, `game/scenes/ui/settings_ui.tscn`, `tests-dotnet/Urman.Core.Tests/DeterminismAndSaveTests.cs`, `game/tests/PlayerSettingsSmokeTest.cs`, `technical_architecture.md`, `playtest_plan.md`, `backlog.md`, `weak_points.md`

## 2026-08-11 — Lock the production narrative package for Acts 2–5

Status: Accepted narrative lock; language, cultural, art, audio and release gates remain open

Context: The compiled `urman.fullgame` campaign already traversed 46 beats and 12 zones, but the production rule required an explicit beat sheet, clue graph, zone/NPC/document list, language keys, world variants and threat beats for every act before expensive 3D work. `narrative.md` described the arc but did not yet provide that handoff package.

Decision: Accept `narrative_lock_acts_2_5.md` as the production narrative lock for Acts 2–5. It maps every existing runtime scene, dialogue, document, quest and knowledge ID to a five-act beat sheet, defines the clue graph and world-state variants, constrains threat beats to no-combat bounded presentation, and preserves one canonical tragic ending. Historical layers remain archival/staged rather than an unqualified literal time-travel claim; mythological beings remain part of an old order rather than enemy classes. Local choices alter evidence, relationships and epilogue staging only.

Consequences: `NARR-001` is complete and can feed style/asset production. `NARR-002` remains blocked until a татарский-speaking and cultural/religious consultant reviews the language keys, Timur's framing, folklore roles and historical presentation. This lock does not accept greybox art, authored animation/audio, mesh collision or release performance, and it does not authorize web-runtime retirement.

Linked files: `docs/urman_knowledge_base/narrative_lock_acts_2_5.md`, `narrative.md`, `content/campaigns/urman.fullgame/campaign.json`, `content/modules/urman-fullgame/definitions.json`, `docs/urman_knowledge_base/open_questions.md`, `docs/urman_knowledge_base/execution_backlog.json`

## 2026-08-11 — Add animation-safe authored clips to the project-original character kit

Status: Accepted production-pipeline pass; Godot playback, facial expression, audio and cultural review remain open

Context: The character kit had readable face landmarks, layered clothing and deterministic LODs, but its meshes were static and the Blender verification wrapper could accept a stale source after a Python traceback because Blender 4.5 returned exit code 0. A static silhouette cannot serve as an animation-safe production input for NPC staging.

Decision: Extend `generate_character_kit.py` with one project-original armature per canonical prefix, bone-parent the LOD0/LOD1 pieces without adding collision meshes, and export deterministic `Idle` and `Tension` clips in the GLB. `verify_character_kit.py` now requires nine armatures and both clips. `verify-assets.sh` and `rebuild-assets.sh` capture Blender output and fail closed on an unhandled Python traceback instead of trusting Blender's exit code. Godot remains the only playback/presentation owner; this pass does not add narrative or physics ownership.

Consequences: The rebuilt character source/GLB now provides nine animation-safe rigs and 143 matching LOD pairs. The asset gate proves the clips exist, while Godot playback, facial expression polish, authored voice/ambience, mesh collision, release hardware and cultural review remain separate gates. Existing browser/web runtime and save boundaries are unchanged.

Linked files: `tools/blender/generate_character_kit.py`, `tools/blender/verify_character_kit.py`, `eng/verify-assets.sh`, `eng/rebuild-assets.sh`, `assets/source/blender/urman_character_kit.blend`, `game/assets/generated/urman_character_kit.glb`, `assets/asset_registry.json`, `docs/urman_knowledge_base/assets.md`, `docs/urman_knowledge_base/technical_architecture.md`

## 2026-08-11 — Integrate Godot playback for the authored character clips

Status: Accepted presentation integration; facial expression, audio, collision and cultural review remain open

Context: The Blender source and GLB now contained nine `Idle`/`Tension` action pairs, but a static import check was not enough to prove that Godot could select the clips for the character prefix actually placed in a zone.

Decision: Keep `GeneratedCharacterKitDressing` as the presentation owner. On attach it must find the prefix-matching Godot `AnimationPlayer`, require both `<Prefix>_Idle` and `<Prefix>_Tension`, start `Idle`, and expose a presentation-only clip switcher. `FullGameFlowSmokeTest` must exercise a real NPC through `Tension` and back to `Idle`; the fail-closed Godot harness must reject runtime `ERROR:`/`SCRIPT ERROR:` log lines even when Godot returns exit code 0. No animation call may read or mutate narrative state.

Consequences: The current imported GLB exposes 18 animations (nine `Idle`, nine `Tension`), and the full-game smoke proves actual playback/switching in all NPC-bearing zones. This closes the import/playback wiring gate, but not final facial acting, authored voice/ambience, mesh collision, release hardware or cultural presentation acceptance. Browser/web runtime and save boundaries remain unchanged.

Linked files: `game/scripts/GeneratedCharacterKitDressing.cs`, `game/scripts/FullGameNpcDressing.cs`, `game/tests/FullGameFlowSmokeTest.cs`, `eng/verify-godot.sh`, `assets/asset_registry.json`, `assets/source/blender/urman_character_kit.blend`, `game/assets/generated/urman_character_kit.glb`

## 2026-08-11 — Select character presentation clips by authored threat zone

Status: Accepted presentation rule; no narrative branching or alternate ending introduced

Context: The imported `Idle`/`Tension` clips were playable, but every NPC still used the same neutral pose. The pact and boundary scenes are authored threat presentations and need a restrained visual escalation without a second gameplay state owner.

Decision: `FullGameNpcDressing` selects `Tension` only for `fullgame_act4_pact` and `fullgame_act5_boundary`; all other full-game NPC placements start `Idle`. The choice is a deterministic zone presentation property, stored as metadata for smoke/capture inspection, and remains outside `RuntimeBridge` and the clue graph. `FullGameFlowSmokeTest` asserts the expected clip for every NPC-bearing zone.

Consequences: Representative captures now exercise the same authored clip policy used by runtime, while the canonical story, interaction IDs, saves and ending remain unchanged. Final facial acting, audio, mesh collision and cultural review are still required.

Linked files: `game/scripts/FullGameNpcDressing.cs`, `game/scripts/GeneratedCharacterKitDressing.cs`, `game/tests/FullGameFlowSmokeTest.cs`, `docs/urman_knowledge_base/narrative_lock_acts_2_5.md`

## 2026-08-11 — Run a focused hero-prop pass without accepting the style lock

Status: Accepted bounded presentation pass; final art lock remains rejected

Context: The first reproducible Godot benchmark frames proved the renderer, camera, fog, lighting and painterly materials, but the house/old-PC frame still read as an empty greybox. Adding final art claims at this stage would hide the remaining density, cultural specificity and forest-silhouette risks.

Decision: Add only presentation-only detail to the existing style benchmark scenes: a framed CRT, keyboard key rows, document top page and red mark, tea cup, wall textile bands/cracks, and a small set of readable village well/firewood details. Keep all new nodes non-interactive and outside the kernel, then recapture the three frames from Godot. Retain the explicit `art-lock-pending` verdict until visual review and representative frame-time evidence accept the style bible.

Consequences: The house focal point is more legible and the capture remains deterministic, while the day street and Kara-Urman edge still expose the broader greybox/material-density gap. The new frame hashes are recorded in `art/style_frames/README.md`; no narrative, physics, save or web-retirement boundary changes.

Linked files: `game/scripts/StyleBenchmarkZone.cs`, `docs/urman_knowledge_base/art/style_frames/README.md`, `docs/aegis/work/2026-08-10-godot-full-migration/20-checkpoint.md`, `docs/aegis/work/2026-08-10-godot-full-migration/90-evidence.md`

## 2026-08-11 — Add bounded density and irregular-crown dressing without accepting art lock

Status: Accepted bounded presentation pass; final art lock remains rejected

Context: The first hero-prop pass made the house focal point legible, but the three Godot benchmark frames still read as a sparse kit. A full asset-production rewrite would be premature before the style language is accepted, so the next pass needed to test density and silhouette variety without adding interaction or narrative ownership.

Decision: Keep the existing compact zones and add only presentation-only dressing: laundry, crates, hay, signage, extra fences/shrubs and distant house mass to the street; shelf/calendar/radio/herbs and CRT scanline/vent/cable details to the house; irregular deterministic pine boughs, fallen logs, stumps, boundary charms, branches and restrained fireflies to Kara-Urman. Widen the painterly shader's albedo grade so the imported brush textures survive first-person distance. Re-capture through the same Godot Metal/Forward+ command and keep the verdict rejected until a final canopy family, hero PC, cultural specificity, authored environment modules and external review exist.

Consequences: The fresh frames (`06139d…`, `26a7e9…`, `3b8317…`) improve the street/house density and give the forest a less repeated silhouette while preserving deterministic rebuilds, collision ownership and the single runtime state. This is still a style-test production pass, not final art acceptance; performance, facial acting, authored sound, mesh collision and cultural review remain separate gates.

Linked files: `game/scripts/StyleBenchmarkZone.cs`, `game/scripts/PainterlyEnvironmentDetails.cs`, `game/scripts/PainterlyMaterialLibrary.cs`, `docs/urman_knowledge_base/art/style_frames/README.md`, `docs/urman_knowledge_base/assets.md`, `docs/aegis/work/2026-08-10-godot-full-migration/20-checkpoint.md`, `docs/aegis/work/2026-08-10-godot-full-migration/90-evidence.md`

## 2026-08-11 — Add a deterministic zone-ambience foundation without declaring final sound

Status: Accepted technical presentation pass; authored voice, final ambience mix and cultural listening review remain open

Context: The Godot runtime already exposed logical audio metadata and an accessibility fallback, but it had no imported physical-zone audio resources. Leaving the runtime silent would hide loading, zone-switching and shutdown regressions; treating generated tones as final sound would falsely close the audio-first cliffhanger gate.

Decision: Generate four deterministic project-original WAV stems with `tools/audio/generate_ambient_audio.py`, register them in `game/assets/audio/ambient_manifest.json`, and make `AmbientAudioDirector` the presentation-only owner that maps active zones to one stem. Desktop runs attach and loop the `AudioStreamPlayer`; headless smoke validates imported streams and switching without starting playback, so resource leaks cannot be mistaken for a passing test. Keep `AudioResolver`, captions, transcript and non-audio cues in the shared content/runtime boundary, and do not add voice or cultural claims.

Consequences: `AmbientAudioSmokeTest` now proves manifest closure, WAV import and zone routing, while `./eng/verify-godot.sh` fails closed on console/log errors and leak diagnostics. The four stems are a technical runtime foundation, not field recordings, final mix, Marat's voice, Rinat's «Не отвечай» or cultural approval; those remain explicit production gates. No narrative state, save schema, web fallback or browser persistence boundary changes.

Linked files: `tools/audio/generate_ambient_audio.py`, `game/assets/audio/ambient_manifest.json`, `game/assets/audio/*.wav`, `game/scripts/AmbientAudioDirector.cs`, `game/scripts/Main.cs`, `game/tests/AmbientAudioSmokeTest.cs`, `game/tests/GodotSmokeCleanup.cs`, `eng/verify-godot.sh`, `assets.md`, `technical_architecture.md`, `playtest_plan.md`, `backlog.md`

## 2026-08-11 — Add a non-destructive web-retirement preflight

Status: Accepted release-safety tooling; strict cutover remains blocked

Context: The old TypeScript/Vite/Three.js project must remain a read-only parity oracle until the Godot release path is accepted. Deleting it early would destroy the comparison owner, while leaving retirement criteria implicit makes accidental dual-runtime production possible.

Decision: Add `eng/check-web-retirement.sh` with a default `--report` mode and a strict `--assert-absent` mode. The gate inventories legacy production entrypoints, Node/Vite/Three.js commands, browser persistence references and the old `src/` tree. Report mode is non-destructive and currently exposes the expected findings; assert mode fails closed and never deletes source or browser data.

Consequences: The final cutover has a reproducible absence check that can only pass after macOS/Windows host acceptance, cultural/accessibility review and the complete 6–8-hour playthrough. Historical route documents and browser data remain untouched until the separate final decision. No runtime owner, save schema or parity behavior changes in this preflight.

Linked files: `eng/check-web-retirement.sh`, `eng/README.md`, `backlog.md`, `docs/aegis/work/2026-08-10-godot-full-migration/90-evidence.md`

## 2026-08-11 — Exercise authored environment modules in the style benchmarks without accepting art lock

Status: Accepted bounded presentation pass; final art lock remains rejected

Context: The three mandatory Godot captures used a dense procedural dressing layer, while the project-original Blender environment kit was only exercised by Acts 2–5. That left the style gate unable to judge imported modular silhouettes in the same first-person compositions used for review.

Decision: Add one `HouseA_` placement to the daytime street and one `PineA_` placement to the Kara-Urman edge through `GeneratedModularKitDressing`, reusing the existing visibility-range, painterly-material and layer-2 provisional-collider adapter. Keep the house/PC benchmark's authored hero-prop study unchanged, and keep the procedural floors, interaction targets and narrative state as their existing owners.

Consequences: Style captures now prove that project-original Blender modules load and render in the benchmark scenes without creating a second physics or narrative owner. The fresh frames remain a production-progress baseline, not final art acceptance: the complete village/forest module family, hero PC, cultural specificity, authored sound and external review are still required.

Linked files: `game/scripts/GeneratedModularKitDressing.cs`, `game/scripts/StyleBenchmarkZone.cs`, `docs/urman_knowledge_base/art/style_frames/README.md`, `docs/urman_knowledge_base/assets.md`, `docs/aegis/work/2026-08-10-godot-full-migration/20-checkpoint.md`, `docs/aegis/work/2026-08-10-godot-full-migration/90-evidence.md`

## 2026-08-11 — Improve environment-kit geometry without changing the import contract

Status: Accepted bounded asset pass; final art lock remains rejected

Context: The imported style modules were visible in Godot, but the first project-original kit was still dominated by literal boxes and a single cone crown. Replacing the whole environment family before visual acceptance would increase scope and invalidate existing LOD/collision evidence.

Decision: Keep the environment kit deterministic and add only bounded geometry improvements: small authored edge breaks on house/fence/road/furniture/PC pieces, a slightly overhanging gable, softened old-PC surfaces and a four-tier faceted `PineA` crown joined into one mesh. The later hero-detail pass extends the contract to 24 deterministic `_LOD1` meshes with named OldPc tower/panel/button parts. Preserve the existing asset IDs, imported `-col` policy, Godot visibility ranges and layer-2 provisional collider ownership.

Consequences: The same Blender→GLB→Godot pipeline now tests a more readable low-poly silhouette without changing scene selectors, interaction targets or physics boundaries. The rebuilt hashes and captures are recorded in the evidence bundle; the complete environment family, hero-PC art, cultural review and final style acceptance remain open.

Linked files: `tools/blender/generate_modular_environment.py`, `assets/source/blender/urman_modular_kit.blend`, `game/assets/generated/urman_modular_kit.glb`, `tools/blender/verify_modular_environment.py`, `docs/urman_knowledge_base/art/style_frames/README.md`, `docs/aegis/work/2026-08-10-godot-full-migration/90-evidence.md`

## 2026-08-11 — Replace procedural tree blobs with tapered foliage tiers without accepting art lock

Status: Accepted bounded presentation pass; final art lock remains rejected

Context: The environment-kit geometry pass improved the imported `PineA` crown, but the repeated procedural trees in the first-person style frames still read as inflated spheres and weakened the Painterly Low-Poly silhouette language.

Decision: Keep `PainterlyEnvironmentDetails` as the presentation owner and replace only the procedural crown primitives with deterministic seven-sided tapered `CylinderMesh` tiers plus three small tapered side boughs. Preserve tree placement, material ownership, collision shape, world-state boundaries and the existing environment asset/LOD contract. Do not add a new interaction or narrative owner.

Consequences: The day and Kara-Urman captures now have clearer faceted conifer silhouettes and remain deterministic; the M4 Pro benchmark still exceeds the 30 FPS floor and the full Godot smoke suite passes. The forest remains a provisional family, so final branch/canopy assets, hero props, cultural review and art acceptance are still open.

Linked files: `game/scripts/PainterlyEnvironmentDetails.cs`, `docs/urman_knowledge_base/art/style_frames/README.md`, `docs/urman_knowledge_base/assets.md`, `docs/aegis/work/2026-08-10-godot-full-migration/20-checkpoint.md`, `docs/aegis/work/2026-08-10-godot-full-migration/90-evidence.md`

## 2026-08-11 — Make desktop package verification part of the export wrapper

Status: Accepted release-evidence tooling; host execution and release signing remain open

Context: The debug export wrapper proved that Godot could produce macOS and Windows artifacts, but existence checks alone did not prove that the archives contained the expected native payloads, .NET assemblies or routed audio content. Windows embeds its PCK in `URMAN.exe`, while macOS ships a separate `URMAN.pck`.

Decision: Add `eng/verify-desktop-artifacts.sh` and invoke it from `eng/export-desktop-debug.sh`. The verifier extracts both archives, checks the universal Mach-O and PE32+ x86-64 payloads, architecture-specific/macOS and Windows `Urman.Game.dll` payloads, then searches the macOS PCK and Windows embedded PCK for the ambient manifest marker and all four routed WAV paths. Keep this as structural evidence only: it does not claim real-host execution, signing/notarization, accessibility, performance or full-playthrough acceptance.

Consequences: Every local export now fails closed if the package layout or embedded content boundary is incomplete, and the same check can be rerun without re-exporting. No runtime owner, save compatibility path, browser persistence behavior or web-retirement timing changes.

Linked files: `eng/export-desktop-debug.sh`, `eng/verify-desktop-artifacts.sh`, `eng/README.md`, `docs/aegis/work/2026-08-10-godot-full-migration/20-checkpoint.md`, `docs/aegis/work/2026-08-10-godot-full-migration/90-evidence.md`

## 2026-08-11 — Add a bounded macOS host smoke for the exported app

Status: Accepted release-evidence tooling; Windows host and release acceptance remain open

Context: Archive inspection proved that the macOS ZIP contained a universal Mach-O, PCK and both architecture-specific .NET payloads, but it did not prove that the packaged app could launch on an actual macOS host. The first probe also showed that a sandboxed run cannot write Godot's `user://` logs; that environment restriction must not be mistaken for an app defect.

Decision: Add `eng/verify-macos-host.sh`. It extracts the existing macOS ZIP, checks the universal Mach-O, launches the embedded app from its own bundle directory with the default Forward+ renderer, Dummy audio and a four-second headless bound, then requires the authored startup-zone and first-person bootstrap markers while failing closed on Godot errors or leak diagnostics. Do not pass `--path`: the exported binary is intentionally built without path overrides. Run this verifier with host filesystem access when the sandbox blocks `user://` writes; keep Windows host smoke as a separate required gate.

Consequences: The current macOS host now has reproducible evidence for the exported PCK/.NET bootstrap (`zone-loaded: village_day@arrival`, Painterly Low-Poly first-person ready, exit 0) for ZIP SHA-256 `04af0c2856897700a62902c4641d6031d30c2cb66e666facb10b3cee529e45d4`. This does not claim Windows execution, M1/Windows performance, signing/notarization, accessibility, cultural review or full playthrough acceptance.

Linked files: `eng/verify-macos-host.sh`, `eng/README.md`, `docs/urman_knowledge_base/execution_backlog.json`, `docs/aegis/work/2026-08-10-godot-full-migration/20-checkpoint.md`, `docs/aegis/work/2026-08-10-godot-full-migration/90-evidence.md`

## 2026-08-11 — Make audio authoring readiness explicit in Content Lab reports

Status: Accepted production-evidence tooling; authored audio and cultural listening remain open

Context: The runtime could prove that captions, transcripts and four technical ambience stems were wired, but a plain content report did not distinguish intentional logical voice references from missing physical files. Treating either state as equivalent would hide the actual production gate.

Decision: Extend `Urman.ContentCli report` with a deterministic audio readiness section. Compile every campaign, enumerate audio assets and variants, verify caption/transcript closure, resolve physical files under `game/`, and inspect the ambient manifest. Classify `.ref`/logical media as `logical-ref`, physical authored files as `authored-file`, and unexpected absent files as `missing-file`; keep the overall authoring status `OPEN` whenever logical voice or explicitly open mix work remains. Report diagnostics fail the command, while an intentional open authoring status does not.

Consequences: The repository now records an auditable boundary: current content has 8/8 campaign-level caption/transcript closures, 0 authored voice files, 8 logical voice references and 4/4 physical technical ambience stems. This improves handoff and prevents a generated tone from being mistaken for final sound without adding a runtime owner, fallback or save compatibility path.

Linked files: `src-dotnet/Urman.Content/Reporting/AudioProductionReporter.cs`, `tools-dotnet/Urman.ContentCli/Program.cs`, `tests-dotnet/Urman.Content.Tests/AudioProductionReporterTests.cs`, `eng/verify-dotnet.sh`, `eng/README.md`, `docs/urman_knowledge_base/assets.md`, `docs/urman_knowledge_base/backlog.md`

## 2026-08-11 — Add non-destructive Painterly texture production candidates

Status: Accepted bounded texture-production pass; runtime integration and final art lock remain open

Context: The existing four painterly albedos were already wired to the Godot material library, but the style gate needed a small candidate set that could be judged at first-person distance without risking the working runtime materials. The next material family also needs mossy stone and old fabric, while neither surface currently has a runtime owner.

Decision: Generate six versioned `_v2_albedo.png` siblings with the built-in ImageGen tool: weathered wood, aged plaster, damp earth, pine foliage, mossy stone and old fabric. Normalize each to 1 024 × 1 024 RGB, retain the v1 files, record exact prompts/source paths/hashes, and register the candidates as project-generated production candidates. Use a test-only Godot SubViewport harness that clones presentation `ShaderMaterial` instances in memory for the three mandatory style scenes plus a six-swatch frame. Do not modify `PainterlyMaterialLibrary`, shader code, gameplay, narrative state, collisions or saves in this pass; v1 remains the active runtime mapping.

Consequences: All six PNGs pass the deterministic format/seam/clipping/saturation gate. The four surfaces with existing owners render in the three Godot Forward+/Metal candidate frames (wood 232 meshes, plaster 15, earth 4, foliage 956); mossy stone and old fabric pass the pixel gate and swatch capture but remained owner/scene-coverage OPEN in this historical pre-owner record. README and asset registry now contain per-file provenance and SHA-256 without implying global runtime activation. Geometry, fog, lighting, cultural specificity, near/mid/far motion readability and final external art review remain separate production gates; no art lock is declared. The 2026-08-12 bounded owner decision below supersedes only the owner/coverage portion.

Linked files: `game/assets/textures/painterly/*_v2_albedo.png`, `game/assets/textures/painterly/README.md`, `assets/asset_registry.json`, `docs/urman_knowledge_base/art/texture_candidates_generation.md`, `docs/urman_knowledge_base/art/texture_candidates_technical.md`, `docs/urman_knowledge_base/art/texture_candidates_qa.md`, `eng/verify-painterly-textures.sh`, `eng/capture-texture-candidate-frames.sh`, `game/tests/TextureCandidateFrameCapture.cs`, `docs/urman_knowledge_base/art/texture_candidate_frames/`

## 2026-08-12 — Release managed painterly resources before Godot smoke shutdown

Status: Accepted test-only reliability repair; no production/runtime behavior change

Context: The full Godot headless harness intermittently reported `ObjectDB` and `RendererDummy` texture RID leaks after `ZoneFlowSmokeTest`, even though the zone transitions and direct bounded runs were correct. The leak was owned by managed `ShaderMaterial`/`ImageTexture` wrappers retained by the painterly cache until native shutdown, not by a second zone or narrative owner.

Decision: Keep `GodotSmokeCleanup.ReleaseAsync` as the single test cleanup owner and make `PainterlyMaterialLibrary.ClearCacheForHeadlessTests` clear its cache, force a bounded managed collection/finalizer pass and collect once more before the smoke process exits. Do not add a production fallback, renderer workaround, extra runtime owner or save compatibility path.

Consequences: Two consecutive full `./eng/verify-godot.sh` runs pass the complete scene/audio/zone/narrative/persistence/collision/Chapter 1/full-game suite without ObjectDB/RID diagnostics. The collection is reachable only through the explicitly test-only cleanup method; normal gameplay never forces it. No narrative state, shader contract, asset mapping, save schema or web-retirement boundary changes.

Linked files: `game/scripts/PainterlyMaterialLibrary.cs`, `game/tests/GodotSmokeCleanup.cs`, `game/tests/ZoneFlowSmokeTest.cs`, `eng/verify-godot.sh`, `docs/aegis/work/2026-08-10-godot-full-migration/20-checkpoint.md`, `docs/aegis/work/2026-08-10-godot-full-migration/90-evidence.md`

## 2026-08-12 — Add spatial style-readability sweep without accepting art lock

Status: Accepted test-only visual evidence; final art lock remains rejected

Context: The three mandatory Godot style frames were reproducible, but one
static composition could not show whether first-person scale, FOV and material
read survived near/mid/far camera changes. A production texture or geometry
change would be premature before that evidence existed.

Decision: Add a separate `StyleMotionSweepCapture` scene and capture wrapper.
Render the day street, house/old PC and Kara-Urman edge at near/mid/far camera
positions and FOV 65°/75°/90°, assemble real 640×360 SubViewport readbacks into
three 1 920×1 080 contact sheets, and record exact camera positions, hashes and
driver evidence in `art/style_motion_sweep/`. Keep the harness test-only: do not
activate v2 textures, change the PainterlyMaterialLibrary, alter gameplay,
narrative state, collisions or saves. Normalize SubViewport readback to RGBA8
before `Image.BlitRect`; a format mismatch must not silently produce a blank
evidence sheet.

Consequences: 27 Metal/Forward+ cells now provide spatial readability evidence.
The old PC focal read and forest foreground/midground separation are visible,
while greybox geometry, warm house light, fog compression, temporal motion
comfort, authored material ownership, cultural review and release-hardware
performance remain open. This is not a movement/head-bob test and does not
declare art lock.

Linked files: `game/tests/StyleMotionSweepCapture.cs`, `game/tests/style_motion_sweep_capture.tscn`, `eng/capture-style-motion-sweep.sh`, `docs/urman_knowledge_base/art/style_motion_sweep/`, `docs/urman_knowledge_base/art/style_frames/README.md`, `docs/aegis/work/2026-08-10-godot-full-migration/20-checkpoint.md`, `docs/aegis/work/2026-08-10-godot-full-migration/90-evidence.md`

## 2026-08-12 — Give stone and fabric candidates bounded v2-only presentation owners

Status: Accepted bounded material/scene decision; final art lock remains rejected

Context: The non-destructive texture pass produced technically valid mossy-stone
and old-fabric candidates, but the initial QA intentionally left them swatch-only
because no production owner or scene coverage existed. Keeping that state would
make the six-candidate production check incomplete; silently routing them through
wood, earth, plaster or foliage would be a worse ownership boundary.

Decision: Register `stone` and `fabric` in `PainterlyMaterialLibrary` with the
versioned candidates and explicit triplanar scales of 2.4 × 2.4 and 3.0 × 3.0.
Use them only on non-interactive presentation anchors in the benchmark scenes:
the village-well/Kara-edge stone markers and the house rug plus two woven stripe
meshes. Keep the four original v1 surface mappings active everywhere else. Do
not change the shader, collision ownership, gameplay, narrative state, runtime
save schema or persistence; do not add a fallback. The test-only capture helper
must fail closed when any one of the six candidates has zero scene coverage.

Consequences: The deterministic image/material-owner gate and real Metal/Forward+
capture now pass 6/6 candidates with coverage wood 232, plaster 15, earth 4,
foliage 956, stone 2 and fabric 3. This closes the former owner gap while keeping
the candidates explicitly production-only and art-lock-pending. Near/far motion
readability, mesh quality, fog/light calibration, cultural review and final art
acceptance remain separate gates.

Linked files: `game/scripts/PainterlyMaterialLibrary.cs`, `game/scripts/StyleBenchmarkZone.cs`, `game/tests/TextureCandidateFrameCapture.cs`, `eng/verify-painterly-textures.sh`, `eng/capture-texture-candidate-frames.sh`, `assets/asset_registry.json`, `game/assets/textures/painterly/README.md`, `docs/urman_knowledge_base/art/texture_candidates_{technical,qa}.md`

## 2026-08-12 — Add technical temporal/head-bob evidence without accepting comfort

Status: Accepted test-only visual evidence; external comfort review and art lock remain open

Context: The 27-cell near/mid/far spatial sweep proved readable still frames at
three FOV values, but it did not exercise the small first-person head bob or
the reduced-motion presentation flag. Claiming comfort from still images would
be too strong, while changing the controller before measurement would mix art
and accessibility decisions.

Decision: Add `StyleTemporalComfortCapture` and a dedicated wrapper. Render the
three mandatory style scenes at FOV 65°/75°/90° for 12 frames in two modes:
the current head-bob amplitude and a reduced-motion mode with the vertical
component disabled. Record frame-to-frame luminance metrics, black-pixel checks,
camera peaks, exact contact-sheet hashes and the real Metal/Forward+ driver in
`art/style_temporal_sweep/`. Keep the harness test-only: no gameplay commands,
narrative state, shader, material registry, collisions or SaveGame changes.

Consequences: 216 real frame samples now provide reproducible temporal evidence;
reduced-motion vertical displacement is measured as zero and all captures are
non-empty. This partially advances the temporal gate but does not establish a
medical comfort threshold or replace observed user traversal at different
speeds, look rates, displays and hardware. Greybox geometry, authored audio,
cultural/accessibility review, release hardware and final art lock remain open.

Linked files: `game/tests/StyleTemporalComfortCapture.cs`, `game/tests/style_temporal_comfort_capture.tscn`, `eng/capture-style-temporal-sweep.sh`, `docs/urman_knowledge_base/art/style_temporal_sweep/`, `docs/urman_knowledge_base/release_gate_matrix.md`

## 2026-08-12 — Add bounded OldPc hero-detail geometry without changing runtime ownership

Status: Accepted bounded asset-production pass; final art lock remains rejected

Context: The imported Blender kit was visible in the Soviet old-PC zone, but its CRT/table family still read as a softened block at the fixed first-person distance. The procedural benchmark already established the intended semantic cues (tower, vents and power control); replacing that with extra runtime dressing would create a second asset owner.

Decision: Extend the project-original `OldPc` Blender family with deterministic low-poly `OldPc_Tower`, `OldPc_TowerPanel` and `OldPc_PowerButton` LOD0 meshes, assign the explicit `OldPcVent` material, regenerate the `.blend`/`.glb` and deterministic LOD1 set (24 visible LOD1 meshes), and map the new names through `GeneratedModularKitDressing`. Preserve the existing `prop.oldpc.crt` asset ID, Godot layer-2 provisional collision proxy, first-person interaction target, narrative state, shader, v1/v2 texture ownership and save contract. Update provenance hashes and keep the result production-progress only.

Consequences: The imported old-PC family now has a readable tower/panel/button silhouette without expanding physics or gameplay ownership. Blender and Godot asset smoke must be rerun; the resulting frames remain a candidate art pass because mesh collision, near/far readability, documents, cultural specificity, audio and final art review are still open.

Linked files: `tools/blender/generate_modular_environment.py`, `tools/blender/verify_modular_environment.py`, `assets/source/blender/urman_modular_kit.blend`, `game/assets/generated/urman_modular_kit.glb`, `game/scripts/GeneratedModularKitDressing.cs`, `assets/asset_registry.json`, `docs/urman_knowledge_base/assets.md`, `docs/urman_knowledge_base/art/style_frames/README.md`

## 2026-08-12 — Keep temporal evidence cleanup render-only after Shape3D leak reproduction

Status: Accepted test-only reliability repair; no production/runtime behavior change

Context: The first temporal capture after the OldPc asset rebuild produced valid
frames but reported 842 leaked `Shape3D` RIDs at process exit. Awaiting a frame
after `SubViewport.Free()` and clearing individual shapes did not change the
count; the disposable benchmark clone still owned its `StaticBody3D` and
interaction collision trees.

Decision: In `StyleTemporalComfortCapture`, strip `CollisionObject3D` nodes from
each render-only scene clone before sampling and await one `SceneTree` frame
after viewport release. The harness does not move a player, query physics or
exercise interactions, so this removes no evidence-bearing geometry. Keep all
production collision owners, layer-2 kit proxies, interaction targets and
collision QA unchanged.

Consequences: The same 216-sample Metal/Forward+ capture now exits without
`ERROR`, `SCRIPT ERROR` or RID leak diagnostics. The fix is confined to a
test-only evidence path; observed motion comfort and final art lock remain open.

Linked files: `game/tests/StyleTemporalComfortCapture.cs`, `eng/capture-style-temporal-sweep.sh`, `docs/urman_knowledge_base/art/style_temporal_sweep/README.md`

## 2026-08-12 — Add a bounded HouseA facade pass without expanding runtime ownership

Status: Accepted bounded asset-production pass; final art lock remains rejected

Context: The imported `HouseA_` module made the day-street and Act 2 house
readable, but its front still read as a block at the fixed first-person
distance. The next useful improvement is a small set of authored facade cues,
not a second procedural dressing system or extra gameplay state.

Decision: Extend the project-original Blender environment kit with eight
deterministic low-poly HouseA meshes: foundation, door, door frame, window
trim top/bottom, porch, porch step and front eave. Regenerate the source and
derived `.glb`, producing the 32-mesh environment `_LOD1` contract, and reuse
the existing Godot presentation material adapter. Keep the existing layer-2
proxy collision owner, interaction IDs, narrative state, shader, texture
ownership and SaveGame contract unchanged; no new gameplay colliders are
introduced by this pass.

Consequences: HouseA now has authored silhouette breaks that can be assessed
in the day street and Act 2 house captures. The pass remains production
evidence only: authored village-family coverage, mesh-level collision review,
near/far readability, lighting/fog calibration, cultural review, release
hardware and final art lock remain open.

Linked files: `tools/blender/generate_modular_environment.py`,
`tools/blender/verify_modular_environment.py`,
`assets/source/blender/urman_modular_kit.blend`,
`game/assets/generated/urman_modular_kit.glb`,
`game/scripts/GeneratedModularKitDressing.cs`, `assets/asset_registry.json`,
`docs/urman_knowledge_base/execution_backlog.json`,
`docs/urman_knowledge_base/art/style_frames/README.md`

## 2026-08-13 — Add focused v3 Painterly candidates without runtime activation

Status: Accepted non-destructive texture comparison pass; final art lock remains rejected

Context: The v2 candidate set established six material owners, but the art review
identified broad-scale readability risks in earth and wood and requested a
quieter comparison family before any global material decision. Generating new
files is safer than overwriting the v1/v2 evidence or pretending a still image
is production acceptance.

Decision: Generate versioned `_v3_albedo.png` siblings for weathered wood, aged
plaster, damp earth, pine foliage, mossy stone and old fabric with ImageGen.
Normalize each to 1 024 × 1 024 RGB, record source paths/prompts/source and final
SHA-256, mirror provenance in the registry and texture README, and run the
existing deterministic image gate. Add a test-only six-material still capture
and a 27-cell near/mid/far × FOV 65°/75°/90° Metal/Forward+ sweep. The harness
may clone presentation materials in memory and strip disposable collision
shapes, but it must retain visible StaticBody3D presentation meshes and must not
change `PainterlyMaterialLibrary` runtime mappings, shader code, gameplay,
narrative state, collisions, saves or web-retirement boundaries.

Consequences: All 12 discovered v2/v3 PNG candidates pass the image gate. The
six v3 files pass still and spatial test captures with coverage wood 246,
plaster 15, earth 4, pine 956, stone 4 and fabric 3; the render-only sweep
exits without Godot/RID/resource-leak diagnostics. The candidates remain
preview-only: 20–30 m no-repeat, temporal/head-bob comfort, wet roughness,
authored geometry, fog/light calibration, cultural review, release hardware and
final art lock remain open. Existing v1 runtime textures and bounded v2 stone /
fabric presentation owners remain unchanged.

Linked files: `game/assets/textures/painterly/*_v3_albedo.png`,
`game/assets/textures/painterly/README.md`, `assets/asset_registry.json`,
`game/tests/TextureCandidateFrameCapture.cs`,
`game/tests/StyleMotionSweepCapture.cs`,
`eng/capture-texture-v3-frames.sh`,
`eng/capture-texture-v3-motion-sweep.sh`,
`docs/urman_knowledge_base/art/texture_candidates_{generation,technical,qa}.md`,
`docs/urman_knowledge_base/art/texture_candidate_frames/v3/`,
`docs/urman_knowledge_base/art/texture_candidate_motion_sweep/`

## 2026-08-14 — Keep wetness material review test-only after contact receipt

Status: Accepted test-only candidate evidence; wetness acceptance and art lock remain rejected

Context: The authored road-relief pass gives the benchmark roads readable
crown/rut geometry and bounded puddle silhouettes, but the shared Painterly
material intentionally remains matte (`roughness_value=0.90`). A direct runtime
roughness change would conflate a material experiment with production behavior.
The first contact audit also found that some fixed puddle origins float above
the relief, so a glossy candidate could mask a geometry defect.

Decision: Keep `PainterlyMaterialLibrary`, `PainterlyEnvironmentDetails`, the
shader, collision owners, SaveGameV3 and narrative state unchanged. Add a
separate Godot test-only harness that instantiates the three mandatory style
scenes plus `chapter1_zirat_road`, counts exactly five clusters/15 patches,
checks source materials remain at `roughness=0.90`, samples layer-1 relief
contact within ±0.010 m, and only after that gate passes clones the existing
ShaderMaterial in memory with `roughness_value=0.50`. Candidate captures are
limited to day street, Kara-Urman edge and Zirat; an OPEN contact gate suppresses
all PNG output.

Consequences: The first receipt caught 0.015–0.019 m day-far gaps and a
cross-scene collider alias, and was correctly fail-closed. A bounded
environment-only correction lowered `PuddleFar` by 0.010 m and
`BoundaryWetPatch` by 0.022 m from the previous candidate origin. The latest
real Metal/Forward+ receipt instantiates scenes one at a time, requires exact
relief instance ownership, passes 5/5 clusters and 15/15 patches within
±0.010 m, clones 15 materials at `roughness_value=0.50` in memory and writes
day/Kara/Zirat 1 920×1 080 candidate frames. Roughness/specular acceptance,
cultural review and art lock remain OPEN; no runtime shader/material activation
or gameplay state changed.

Linked files: `game/tests/WetnessCandidateCapture.cs`,
`game/tests/wetness_candidate_capture.tscn`,
`eng/capture-wetness-candidate.sh`,
`docs/urman_knowledge_base/art/wetness_candidate/`,
`docs/urman_knowledge_base/art/style_frames/README.md`,
`docs/urman_knowledge_base/release_gate_matrix.md`

## 2026-08-14 — Compare wetness roughness rows without activating production

Status: Accepted technical matrix evidence; wetness acceptance and art lock remain OPEN

Decision: Keep the source puddle owner at `roughness_value=0.90` and add only a
versioned test harness that records raw authored origins, verifies exact
layer-1 relief ownership, aligns temporary in-memory instances to a strict
±0.005 m contact gate, and compares `roughness_value` rows `0.40`, `0.50` and
`0.60`. Each row clones all 15 puddle materials in memory and restores source
overrides before shutdown; no runtime, shader, scene, GLB, SaveGameV3,
narrative or PainterlyMaterialLibrary owner changes.

Evidence: Metal/Forward+ wrapper pass with 45 clones and nine 1 920 × 1 080
PNGs; source scene hashes unchanged. Raw production origins remain OPEN because
8/15 exceed 5 mm (maximum 9.661 mm), while temporary candidate alignment passes
with maximum 4.205 mm. Visual review still finds flat/weak wetness response,
so no row is selected for production.

Linked files: `game/tests/WetnessCandidateMatrixV2Capture.cs`,
`game/tests/wetness_candidate_matrix_v2_capture.tscn`,
`eng/capture-wetness-candidate-matrix-v2.sh`,
`docs/urman_knowledge_base/art/wetness_candidate_matrix_v2/`.

## 2026-08-14 — Seat raw puddle origins on their authored relief without activating wetness

Status: Accepted narrow production-geometry correction; wetness acceptance and art lock remain OPEN

Context: Matrix v2 measured eight of fifteen production puddle patches more
than 5 mm above their own exact road/path relief collider. Its in-memory
alignment proved that these were local placement errors, but could not be used
as production evidence.

Decision: extend `AddPuddleCluster` only with optional three-entry local Y
corrections, then apply measured corrections to `PuddleNear[0]`,
`PuddleFar[0..2]`, `BoundaryWetPatch[0..2]` and `ZiratWetPatch[2]`. Do not
translate a whole cluster, change the 9×28 relief, collision owner, shader,
`PainterlyMaterialLibrary`, source `roughness_value=0.90`, textures, GLB,
runtime, SaveGameV3 or narrative state. The matrix wrapper now fails unless all
15 raw origins pass exact-owner ±0.005 m without candidate-local alignment.

Evidence: fresh real Metal/Forward+ receipt passes with all 15 raw samples,
45 in-memory row clones and nine 1 920 × 1 080 PNGs. Source scene hashes stay
unchanged during the test harness. The image review still finds flat/weak
wetness; no candidate roughness is selected.

Linked files: `game/scripts/PainterlyEnvironmentDetails.cs`,
`game/scripts/StyleBenchmarkZone.cs`,
`eng/capture-wetness-candidate-matrix-v2.sh`,
`docs/urman_knowledge_base/art/wetness_candidate_matrix_v2/`.

## 2026-08-14 — Hold focused v4 earth/wood texture candidates after in-engine review

Status: Accepted non-destructive comparison evidence; v4 replacement and final art lock remain rejected

Context: The v3 comparison reduced photographic noise, but the art review still
needed a focused answer for the two materials most visible in first person:
road earth and shared village wood. Two new v4 ImageGen siblings were generated
without overwriting any v1/v2/v3 asset and were run through the same deterministic
image gate, still capture and 27-cell Godot Metal/Forward+ spatial sweep.

Decision: Keep `damp_earth_v4_albedo.png` and `weathered_wood_boards_v4_albedo.png`
as test-only candidates with explicit HOLD/REWORK status. Do not activate them in
`PainterlyMaterialLibrary`, do not change shader, collision, SaveGameV3 or
narrative state. The earth candidate has stronger standalone broad value grouping
but its dark drawn ruts/stone shading duplicate the authored 9×28 road relief and
puddle overlays; the wood candidate has a calmer palette but dark continuous seams
and regular knots risk reading as baked geometry/striping, especially under the
shared wood owner for houses, fences, roofs and furniture. v3 remains the safer
comparison baseline until a material-owner split or a reworked albedo removes
those conflicts.

Consequences: v4 image gate is PASS (14/14 discovered v2/v3/v4 files overall),
still capture is PASS for day/house/Kara plus swatches, and the v4 motion sweep
is PASS with 27 real cells and no Godot/RID/resource-leak diagnostics. This is
spatial test evidence only. Next review must compare earth with relief-only vs
relief+wetness at day/Kara/Zirat and compare wood on facade/fence/furniture/end
grain under neutral and warm light; 20–30 m repetition, wet roughness, geometry,
lighting/fog, cultural review and final art lock remain open.

Linked files: `game/assets/textures/painterly/*_v4_albedo.png`,
`game/tests/TextureCandidateFrameCapture.cs`, `game/tests/StyleMotionSweepCapture.cs`,
`eng/capture-texture-v4-frames.sh`, `eng/capture-texture-v4-motion-sweep.sh`,
`docs/urman_knowledge_base/art/texture_candidate_frames/v4/`,
`docs/urman_knowledge_base/art/texture_candidate_motion_sweep_v4/`,
`docs/urman_knowledge_base/art/texture_candidates_generation.md`,
`docs/urman_knowledge_base/art/texture_candidates_qa.md`

## 2026-08-14 — Keep v5 earth/wood rework as production candidates only

Status: Accepted technical comparison evidence; runtime activation and final art lock remain rejected

Context: The focused v4 review rejected both replacements: earth embedded
painted ruts/stones that competed with authored road relief and puddles, while
wood retained long seams and knots that became regular stripes under the shared
triplanar owner. A narrower rework was needed without overwriting prior evidence
or changing the runtime material contract.

Decision: Generate only versioned `damp_earth_v5_albedo.png` and
`weathered_wood_boards_v5_albedo.png`. Keep the existing v1 runtime mappings,
shader, collision, SaveGameV3 and narrative state unchanged. Accept the two
files as technical candidates after the deterministic 1 024² RGB/sRGB gate and
an isolated real Metal/Forward+ v3↔v5 A/B harness. The harness must instantiate
each scene independently, retain visible relief, verify exact collider ownership
and ±0.010 m contact, and compare relief-only with relief+wetness; it must not
be interpreted as visual acceptance.

Consequences: Both v5 PNGs pass technical integrity/seam/saturation checks and
the A/B receipt produces nine non-black sheets with 120/120 contact samples.
The rendered earth relief-only cells do not yet demonstrate a reliable v3→v5
visual delta, and wood still needs facade/fence/furniture/end-grain review under
neutral and warm light. Therefore v5 remains production OPEN; no candidate is
registered as a runtime fallback or art-lock target. Remaining gates are
material-owner separation, 20–30 m repetition, near/mid/far motion, wetness and
lighting calibration, geometry, cultural review and release hardware.

Linked files: `game/assets/textures/painterly/damp_earth_v5_albedo.png`,
`game/assets/textures/painterly/weathered_wood_boards_v5_albedo.png`,
`game/tests/TextureCandidateAbDiagnostic.cs`,
`eng/capture-texture-ab-diagnostic.sh`,
`docs/urman_knowledge_base/art/texture_candidates_v5_technical.md`,
`docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v4/`,
`assets/asset_registry.json`

The harness manifest wording was then corrected to derive its acceptance keys
from the selected versions. A superseding v3↔v5 receipt was written to the
non-overwriting `art/texture_candidate_ab_diagnostic_v5/` directory (manifest
SHA `41cc4702fbca356b7a8e2035251205fc576ebd6f822dec834c159a25f7fbc9e0`),
with the same 9 sheets and 120/120 contact samples. This is a test-evidence
correction only and does not change the production-open decision.

The same v5 pair then completed a separate 27-cell Metal/Forward+ near/mid/far
× FOV 65°/75°/90° sweep in the non-overwriting
`art/texture_candidate_motion_sweep_v5/` directory. The day, house/old-PC and
Kara-Urman sheets are respectively SHA
`b9c7587602da28d8a62563c99c4e36deeef434725de870745fb52052ae69c2ae`,
`f891abeac5ded356014390bdd14ec452735a45b2348dd5e1f23c1c84bef73a5a` and
`ea56366bcc7ff2bb3013e2ecb9bf66b4d3c72f7eb89802429edc1543dff92f5f`;
the manifest is SHA
`003c0be712333fa0aaee5b1a596b119768a09cd0e2cd83b8b3376d014fea4053`.
This closes the spatial/technical capture gate only. Temporal comfort,
earth relief-vs-wetness separation, wood multi-owner/orientation, geometry,
lighting, cultural review and final art lock remain open.

## 2026-08-14 — Keep v6 earth/wood rework as production candidates only

Status: Accepted image and test-capture evidence; runtime activation and art lock remain rejected

Decision: Keep `damp_earth_v6_albedo.png` and
`weathered_wood_boards_v6_albedo.png` as non-destructive candidates. V6 was
generated to make earth broad and non-directional (the authored relief owns
the ruts) and wood abstract/low-frequency (mesh owns boards, seams and
end-grain). Extend only the deterministic verifier and test-only Godot
harnesses; do not modify `PainterlyMaterialLibrary`, shader, saves, collisions
or narrative state.

Evidence: the complete v2–v6 image gate is 18/18 PASS. The isolated v5↔v6
Metal/Forward+ A/B receipt is technical PASS with 9 sheets and 120/120
relief/contact samples under `art/texture_candidate_ab_diagnostic_v6/`; the
dedicated v6 motion receipt is technical PASS with three 27-cell sheets under
`art/texture_candidate_motion_sweep_v6/`. Earth relief-only separation remains
subtle and wood still needs multi-owner/orientation review, so production and
art-lock status stay OPEN.

Linked files: `game/assets/textures/painterly/damp_earth_v6_albedo.png`,
`game/assets/textures/painterly/weathered_wood_boards_v6_albedo.png`,
`eng/verify-painterly-textures.sh`, `game/tests/StyleMotionSweepCapture.cs`,
`game/tests/TextureCandidateAbDiagnostic.cs`,
`eng/capture-texture-v6-motion-sweep.sh`,
`docs/urman_knowledge_base/art/texture_candidates_v6_technical.md`,
`docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v6/`,
`docs/urman_knowledge_base/art/texture_candidate_motion_sweep_v6/`,
`assets/asset_registry.json`

## 2026-08-14 — Separate asset provenance from Blender host verification

Status: Accepted build/asset boundary; release host revalidation remains open

Decision: Keep `eng/verify-asset-registry.sh` as a mandatory, stdlib-only
preflight before Blender. It validates unique IDs, licenses, safe repository
paths, all derived SHA-256 values and every explicit local source hash. Correct
the stale project-original character source hash in `assets/asset_registry.json`.
If pinned Blender exits with a startup SIGSEGV, `eng/verify-assets.sh` must
return non-zero with `HOST_TOOLCHAIN_BLOCKED`; it must not fall back to stale
generated files or treat the registry preflight as a Blender PASS.

Evidence: positive preflight covers 33 assets/derived files and 7 explicit local
sources; missing-file, duplicate-ID and hash-mismatch fixtures fail closed.
The managed sandbox reproduces the typed Metal startup blocker, while an
elevated real-driver run passes both existing Blender verifiers. This split is
host evidence, not an art-lock or release acceptance.

Linked files: `eng/verify-asset-registry.sh`, `eng/verify-assets.sh`,
`assets/asset_registry.json`,
`docs/urman_knowledge_base/art/blender_asset_verifier_host_blocker.md`,
`docs/urman_knowledge_base/technical_architecture.md`

## 2026-08-14 — Keep lighting/fog calibration as a presentation candidate

Status: Accepted technical evidence; visual art lock remains rejected

Decision: Retune only `StyleBenchmarkZone.cs` ambient/fog and local warm
lights to test Painterly Low-Poly readability. Do not change geometry, shader,
material owners, collision, narrative state or SaveGameV3. Static, 27-cell
spatial and 216-sample temporal Metal/Forward+ captures are evidence for the
GODOT-003 review, not a runtime style switch.

Consequence: house/Kara wash is reduced, but day remains greybox, the house is
still warm/sparse and Kara needs authored canopy/ground geometry. The next
production slice is owner-specific geometry/material review, not another
universal texture version.

Linked files: `game/scripts/StyleBenchmarkZone.cs`,
`docs/urman_knowledge_base/art/style_frames/README.md`,
`docs/urman_knowledge_base/art/style_motion_sweep/README.md`,
`docs/urman_knowledge_base/art/style_temporal_sweep/README.md`

## 2026-08-14 — Split imported wood owners and close cloth fallback

Status: Accepted bounded material integration; visual art lock remains open

Decision: Keep the original weathered-wood albedo, shader and geometry intact,
but assign semantic descriptors `wood_facade`, `wood_fence`,
`wood_furniture` and `wood_bark` to the corresponding imported Blender meshes
with separate world scales. Add an explicit `cloth` descriptor pointing to the
bounded old-fabric presentation owner so generated character clothing cannot
silently render without albedo.

Evidence: Godot build and full smoke pass with zero warnings; the full-game
flow now asserts required imported surface owners and textured clothing. Fresh
Metal/Forward+ style, 27-cell spatial and 216-sample temporal captures pass
technical checks; local M4 Pro benchmark remains above the 30 FPS floor.
End-grain/orientation, clothing readability, 20–30 m repetition, cultural
review and art-lock acceptance remain open.

Non-goals: no shader, source PNG replacement, collision, save, narrative or
web-runtime change.

## 2026-08-14 — Integrate the bounded Chapter 1 lighting/fog baseline

Status: Accepted presentation calibration; visual art lock remains open

Context: The isolated `StyleCalibrationCandidateCapture` showed that the
original benchmark lighting over-warmed the old-PC house and compressed the
Kara-Urman edge into a blue fog mass. The day street still has a greybox
geometry problem, so lighting alone cannot close GODOT-003.

Decision: Promote the candidate values into `StyleBenchmarkZone.cs` for the
Chapter 1 benchmark/demo zones only: cooler neutral outdoor ambient/fog, lower
house lamp/fill energies, and a restrained warm-window/moon balance. Keep the
change presentation-only. Do not change geometry, PainterlyMaterialLibrary,
shader uniforms, collision ownership, SaveGameV3, narrative state or the
full-game zone lighting path.

Evidence: `./eng/capture-style-frames.sh` passes on Metal 4.0 / Forward+ at
1 920 × 1 080 with current hashes day
`d6887d013c08340ee6197b6126c4d55b2c10ab0b0e9637f2be2fccdd9700a3d8`, house
`00396d56324728735d479835b28d9309f902a523ae5c991e9751f7314e4ca2b8` and Kara
`3b9fdab9a7fc85f41c834ee6245e904005d22f52622576ec65c46ac711494674`.
`./eng/verify-godot.sh` passes the full smoke suite; the M4 Pro benchmark
remains above the 30 FPS floor. Visual/cultural review, authored geometry,
near/mid/far repetition, temporal comfort and art lock remain OPEN.

Linked files: `game/scripts/StyleBenchmarkZone.cs`,
`docs/urman_knowledge_base/art/style_frames/README.md`,
`docs/urman_knowledge_base/art/style_calibration_candidate/README.md`.

Linked files: `game/scripts/PainterlyMaterialLibrary.cs`,
`game/scripts/GeneratedModularKitDressing.cs`,
`game/tests/FullGameFlowSmokeTest.cs`,
`docs/urman_knowledge_base/art/texture_candidates_technical.md`

## 2026-08-14 — Use the authored HouseA module in the canonical epilogue

Status: Accepted bounded presentation integration; production art lock remains open

Decision: Add the project-original `HouseA_` environment module to the
`fullgame_act5_epilogue` wrapper under the explicit `act5-epilogue-house`
variant. Keep the distant procedural house as background dressing only. The
full-game smoke must assert the HouseA anchor, LOD ranges and `wood_facade`
owner, while layer-2 proxy collision remains provisional and separate from
gameplay progression.

Evidence: `.tools/dotnet/dotnet build game/Urman.Game.csproj`, fresh
`./eng/verify-godot.sh` and `./eng/capture-fullgame-frames.sh` pass. The capture
set now contains seven 1 920 × 1 080 frames, including
`godot_act5_epilogue_1080p.png` with SHA
`9ea07a1b1b50b71f7d92d97c8541698ff5b6255003a1bed32a48793c2367e348`.

Non-goals: no narrative/state/save change, no shader or global material switch,
no Blender source regeneration and no art-lock claim. Authored module family,
mesh-level collision, hero detail, cultural review and release-hardware gates
remain open.

## 2026-08-14 — Add authored yard and threshold modules without a new collision owner

Status: Accepted bounded presentation candidate; art lock and collision acceptance remain open

Decision: Rebuild the project-original modular Blender kit with `WellA_`,
`WoodpileA_` and `GateA_` families and expose them through the existing Godot
presentation adapter. Act 2 attaches the well/woodpile under the HouseA anchor;
Act 5 Kara-Urman attaches GateA under the PineA anchor and removes the old
procedural threshold posts/board/mark to avoid duplicate visual ownership. GateA
is not attached to the HouseA epilogue because its authored coordinates belong
to the forest composition.

Evidence boundary: the source/derived hashes are recorded in
`assets/asset_registry.json`, the Blender verifier reports 47 deterministic
LOD1 meshes, and semantic owners cover stone, prop wood, bark, fence and cloth.
The new meshes are explicitly decorative (`collision:none`); zone floor/path
and existing layer-2 proxies remain the only collision authorities. No shader,
SaveGameV3, narrative state, command, or web-runtime path changes.

Consequences: the village silhouette is more authored, but near/mid/far
traversal, 20–30 m repetition, marker readability, floating/occlusion checks,
authored family coverage, cultural review and M1/Windows performance remain
open. This decision does not declare a production art lock.

Linked files: `tools/blender/generate_modular_environment.py`,
`tools/blender/verify_modular_environment.py`, `assets/asset_registry.json`,
`game/scripts/GeneratedModularKitDressing.cs`,
`game/scripts/FullGameZoneDressing.cs`,
`docs/urman_knowledge_base/art/authored_module_candidates_2026-08-14.md`.

## 2026-08-14 — Harden desktop artifact publication without closing host gates

Status: Accepted bounded release-tooling integration; Windows host and release
acceptance remain open

Decision: Export macOS and Windows debug packages into a fresh staging directory,
serialize publication and verification with an explicit lock, replace the
Windows mirror wholesale, and build the Windows ZIP without host resource-fork
metadata. The structural verifier must reject unsafe or duplicate ZIP names,
Unicode-normalized/case-folded Windows collisions, reserved device aliases,
symlinks and traversal; it must validate exact macOS architectures, all three
.NET 10 dependency closures, embedded content markers and staged-versus-archive
executable identity. Publish the JSON receipt only after atomic SHA/size/path
rebinding to the three final artifacts; invalidate a previous receipt before a
new publication and leave no PASS receipt on failure.

Evidence: hardened `./eng/export-desktop-debug.sh` and
`./eng/verify-desktop-artifacts.sh` pass shell, positive, tamper, signal and
parallel-lock checks. Fresh export produced macOS ZIP
`131888d82a9b1be3362540506d6d3b5f1f131336b55a33569504dd108b739b6d`, Windows ZIP
`4462f271d4abd1452f3e511961ea2e799d4aa356c9f3fdba5235f37473e4a0f1` and
Windows EXE `d4985248230ef8f19b38ea0567a279242cd02bc579da0069d5bad436dbaa4750`.
The current Windows ZIP has zero `__MACOSX`, AppleDouble and `.DS_Store` entries;
the receipt records `windowsHostExecution: OPEN`. Elevated macOS host smoke
passes on the fresh universal package.

Non-goals: no gameplay, narrative state, SaveGameV3, shader, texture or web
retirement change; this decision does not claim Windows host execution, M1 or
medium-Windows performance, signing/notarization or full-playthrough acceptance.

Linked files: `eng/export-desktop-debug.sh`,
`eng/verify-desktop-artifacts.sh`, `eng/README.md`,
`build/desktop-artifact-receipt.json`,
`docs/urman_knowledge_base/release_gate_matrix.md`.

## 2026-08-14 — Diagnose puddle silhouette without activating production geometry

Status: Accepted test-only geometry evidence; puddle/wetness acceptance and art lock remain OPEN

Context: The existing puddle cluster uses shallow `CylinderMesh` patches. Even
after exact relief contact passed, the close view could read as a raised plate
with a visible cylindrical rim. Changing the production helper, roughness or
shader before comparing the silhouette would mix a geometry decision with a
wetness/material decision.

Decision: Add a versioned Godot harness that instantiates only the day street,
Kara-Urman edge and Zirat road independently. It retains each source
`ShaderMaterial` at `roughness_value=0.90`, validates the same exact layer-1
9×28 relief collider and ≤5 mm contact, then replaces only the `Mesh` property
of the fifteen existing `PuddlePatch` instances in memory with a shallow open
faceted `ArrayMesh`. The candidate has no bottom face or vertical side wall;
it adds no `CollisionObject3D` or material clone. A 1 920 × 1 080 sheet records
source/candidate overview and close views; source mesh references are restored
before teardown.

Evidence: fresh Metal/Forward+ capture passes source and candidate exact-owner
contact, 15/15 mesh replacements, unchanged collision-object counts and three
PNG/manifest SHA-256 receipts. The faceted candidate removes the obvious rim,
but is visually too quiet at source roughness to prove wet readability at a
distance. No production mesh, shader, material, texture, SaveGameV3, narrative
state or web-runtime path changes.

Linked files: `game/tests/PuddleSilhouetteCandidateCapture.cs`,
`game/tests/puddle_silhouette_candidate_capture.tscn`,
`eng/capture-puddle-silhouette-candidate.sh`,
`docs/urman_knowledge_base/art/puddle_silhouette_candidate/`,
`docs/urman_knowledge_base/assets.md`,
`docs/urman_knowledge_base/weak_points.md`,
`docs/urman_knowledge_base/release_gate_matrix.md`.

## 2026-08-14 — Lock modular GLB presentation to published LOD pairs

Status: Accepted bounded technical smoke; mesh-collision and art-lock gates remain OPEN

Context: provenance and selected-zone smoke established that the modular GLB is
project-original, but neither asserted every environment prefix in isolation
nor rejected a non-LOD render helper retained by Godot's import. The latter
appeared as an extra visible `HouseA` mesh beside its 12/12 published LOD pair.

Decision: add a host-independent smoke that loads the generated `.glb` through
the real `AttachPresentationOnly` adapter and verifies nine exact family pairs:
HouseA_ 12/12, FenceA_ 6/6, RoadDirt_ 1/1, PineA_ 2/2, TableA_ 5/5, OldPc_ 6/6,
WellA_ 7/7, WoodpileA_ 4/4 and GateA_ 4/4. All must use 0–24 m / 18–72 m
self-fade ranges, exact semantic `ShaderMaterial` owner sets, one-to-one LOD
names, source metadata, positive imported-collision removal counts and zero
`CollisionObject3D`/`CollisionShape3D` descendants. Restrict visible published
module meshes to explicit `_LOD0`/`_LOD1` names; the registry records FenceA as
presentation-only because this GLB has no `FenceA-col`; no GLB, Blender source,
registry, Painterly material, shader, gameplay collision, save or narrative
state changes.

Evidence: fresh C# build and headless Godot smoke pass all 9/9 families. The
derived artifact remains SHA-256
`a7647bc1156a3770433d7b8a2e8ba25df380cf94376a12737a54f12d058cf37a`.
This does not accept imported mesh collision, host Blender validation, visual
quality or release hardware.

## 2026-08-14 — Extend the authored OldPc hero-detail candidate

Status: Accepted bounded production candidate; visual art lock remains OPEN

Decision: Add two restrained, project-original low-poly parts to the existing
`OldPc_` family: `OldPc_DriveSlot` and `OldPc_LabelPlate`, each with deterministic
LOD0/LOD1 output. Keep both under `prop.oldpc.crt` with `collision=none`; the
existing Act 3 layer-2 table/OldPc proxy remains unchanged. Extend the modular
contract from 47 to 49 LOD1 meshes and require exact names, `lod_source`, and
`collision=none` in both Blender and Godot smoke tests. Do not activate a new
texture or shader owner.

Evidence: pinned Blender and `./eng/verify-assets.sh` pass; registry hashes are
source `4f3aa74be1d34e1cd956806e56fb57e084cbbfc385380042492bac5c66d48b48` and
derived `c9f9e8d9a036c3fc8ef20dfc736a164fb393eb8194eff0c0e55782547efa2c36`;
Godot build, full smoke and two Metal/Forward+ 1 920 × 1 080 close captures
pass. Mid-distance readability, movement/head-bob, standard Soviet recapture,
cultural review, release hardware and art lock remain OPEN.

Linked files: `tools/blender/generate_modular_environment.py`,
`tools/blender/verify_modular_environment.py`, `game/scripts/GeneratedModularKitDressing.cs`,
`game/tests/GeneratedModularKitContractSmokeTest.cs`,
`game/tests/OldPcHeroDetailCapture.cs`,
`docs/urman_knowledge_base/art/oldpc_hero_detail_candidate/`.

Linked files: `game/scripts/GeneratedModularKitDressing.cs`,
`game/tests/GeneratedModularKitContractSmokeTest.cs`,
`game/tests/generated_modular_kit_contract_smoke_test.tscn`,
`eng/verify-godot.sh`, `docs/urman_knowledge_base/assets.md`,
`docs/urman_knowledge_base/weak_points.md`,
`docs/urman_knowledge_base/release_gate_matrix.md`.

## 2026-08-14 — Preserve visual descendants in temporal evidence and isolate light calibration

Status: Accepted technical evidence repair; comfort, visual review and art lock remain OPEN

Context: The earlier temporal cleanup freed complete `CollisionObject3D` nodes.
Because benchmark meshes can be children of those bodies, the resulting contact
sheets were sparse despite passing non-empty checks. A lighting/fog decision also
needed an isolated A/B rather than a runtime change.

Decision: `StyleTemporalComfortCapture` now keeps `CollisionObject3D` and their
visual descendants, zeros collision layer/mask and frees only disposable
`CollisionShape3D` nodes in the render-only clone. `StyleCalibrationCandidateCapture`
uses a separate OwnWorld3D SubViewport, clones `WorldEnvironment` and overrides
only the named presentation lights/fog for six baseline/candidate frames. The
new sanitizer supersedes the earlier whole-`CollisionObject3D` cleanup decision;
the previous temporal receipt remains under `superseded_pre_visual_sanitization/`;
neither harness changes production scenes, materials, shader, GLB, collision,
SaveGameV3 or narrative state.

Evidence: the current temporal manifest is
`57ac2b4ae0b81883c30de7b54e41e24b6c9b8be620d7cef3c629e662fb17d66d` with
216 valid Metal/Forward+ samples and visual-mesh/shape-removal counts; the
calibration manifest is
`a98bbda675b5fa424d45b08d4972db44df212e5b6f66d3810aa6d01bd2ffc85a` with six
1 920×1 080 pairs. Both are technical candidate receipts only.

## 2026-08-14 — Release imported Shape3D resources before adapter teardown

Status: Accepted bounded reliability repair; no gameplay or physics-owner change

Context: The modular GLB contract smoke passed its ownership assertions, but
real Metal/Forward+ shutdown reported two leaked native `GodotShape3D` RIDs when
imported helper nodes were freed before entering the SceneTree.

Decision: `GeneratedModularKitDressing.RemoveImportedCollisionNodes` detaches
each imported `CollisionShape3D.Shape` resource before synchronous `Free()` and
zeros imported collision-object layers/masks. The controlled layer-2 proxy and
production floor/path remain the only gameplay owners; presentation-only
instances still expose zero physics descendants.

Evidence: fresh `.NET` build and elevated `./eng/verify-godot.sh` pass with no
RID/ObjectDB/resource-leak diagnostics. The GLB, registry, shader, saves,
narrative state and web-runtime boundary are unchanged.

## 2026-08-15 — Let Godot own shared imported Shape3D lifetimes

Status: Supersedes the 2026-08-14 disposal detail; accepted bounded reliability repair

Context: The previous cleanup note recommended manually disposing each detached
imported `Shape3D`. A fresh isolated modular-kit contract smoke reproduced two
`P12GodotShape3D` RID leaks at process exit even though all ownership checks
passed.

Decision: `GeneratedModularKitDressing.RemoveImportedCollisionNodes` detaches
each imported `CollisionShape3D.Shape`, frees the disposable node synchronously
and leaves the shared PackedScene subresource under Godot's reference-counted
ownership. It still zeros imported collision-object layers/masks and removes all
imported physics descendants before the presentation instance enters the tree.
The controlled layer-2 proxy and production floor/path remain the only gameplay
owners; presentation-only instances still expose zero physics descendants.

Evidence: the isolated contract smoke and fresh full `./eng/verify-godot.sh`
pass with exit 0 and no RID/ObjectDB/resource-leak diagnostics; the current
desktop packages were republished and receipt-bound. No GLB, registry, shader,
save, narrative or web-runtime boundary changed.

## 2026-08-14 — Keep OldPc motion evidence in one isolated render world

Status: Accepted bounded technical evidence repair; production/art acceptance remains OPEN

Context: The first OldPc near/mid/FOV/head-bob contact-sheet prototype created
and freed a `SubViewport` for every tile and retained readback images after
teardown. On Metal/Forward+ this produced cleared later tiles despite valid
scene nodes, so a still-only receipt could overstate motion coverage.

Decision: Reuse one `OwnWorld3D` SubViewport, `FullGameZone` clone and camera
for the complete near+mid 3×3×3 matrix. Normalize/copy each readback before
compositing, remove only disposable `CollisionShape3D` nodes and let Godot
reference counting own shared `Shape3D` resources. The wrapper fails closed on
missing renderer evidence, incomplete 18-frame manifests, blank luma spans,
wrong PNG dimensions, Godot errors or leaks. This is a test-only owner; no
runtime scene, material, shader, collision, save, narrative or web-retirement
path changes.

Evidence: `eng/capture-oldpc-hero-detail-motion.sh` passes on Metal 4.0 /
Forward+ / Apple M4 Pro with two 1 920 × 1 080 sheets, 18/18 frames, all four
hero-detail names and luma spans `0.545–0.601`. Receipt and previews are in
`docs/urman_knowledge_base/art/oldpc_hero_detail_motion_sweep/`.

Consequences: Technical motion/readability evidence is stronger and blank
capture regressions are fail-closed. This does not prove observed traversal,
external motion comfort, mid/far repetition, cultural/level-art review,
M1/Windows performance or final art lock.

## 2026-08-14 — Reconcile modular asset verifier with the published 49-mesh kit

Status: Accepted bounded verification repair; production art and release gates remain OPEN

Context: `verify-assets.sh` was failing before Blender verification because
`tools/blender/verify_modular_environment.py` still hard-coded the superseded
54-mesh expectation. The current project-original source, derived GLB,
registry and Godot contract all publish 49 deterministic environment LOD1
meshes (including OldPc DriveSlot/LabelPlate and WellA/WoodpileA/GateA).

Decision: Make the Blender verifier require the current exact 49-mesh contract
and keep the registry/source/derived assets unchanged. Treat the verifier as
the canonical contract owner; do not weaken it to accept arbitrary counts.

Evidence: elevated `./eng/verify-assets.sh` passes asset-registry 36/36,
environment 49/49 and character 143/143. Fresh desktop export and independent
package verification also pass; current macOS host smoke passes. Windows host,
M1/Windows performance, authored audio, cultural review, full playthrough and
web retirement remain open.

## 2026-08-14 — Make the read-only desktop receipt audit accept ZIP root directories

Status: Accepted bounded release-tooling repair; package contents and runtime unchanged

Context: The fresh Windows ZIP intentionally contains a directory entry named
`windows/`. The full package verifier accepted it, but
`./eng/verify-desktop-artifacts.sh --check-receipt` rejected the same published
receipt before comparing its 203-file payload manifest.

Decision: Allow exactly the published `windows/` root directory marker in the
read-only manifest parser, while continuing to reject every other empty,
traversal, normalized-separator or outside-root entry. Directory markers remain
excluded from the file digest manifest, so the archive/tree equality contract is
unchanged.

Evidence: shell syntax passes; full four-argument verifier passes into a
temporary receipt; `--check-receipt` passes with 203 payload files and the
published receipt SHA/mtime unchanged. No game, save, shader, narrative or
archive bytes changed.

## 2026-08-14 — Keep zone transitions atmospheric inside the Act 1 demo only

Status: Accepted bounded presentation change; full-game scenes remain unchanged

Context: The Act 1 route already loaded five compact zones and preserved one
runtime state, but every interaction exposed an instantaneous scene swap. That
made the walkable demo read like a debug menu and weakened the intended
slow-burn rhythm.

Decision: Enable a short, dark painterly fade only when `Act1DemoRoot` creates
its `Main` instance. The default `Main` contract remains transition-free for
other scenes until they receive their own presentation review. Add a compact
Russian control line to the existing intro card so a first-time player knows
how to walk, look, inspect and open the journal.

Evidence: `Act1DemoLaunchSmokeTest` now asserts the demo-only transition layer;
the clean .NET build and elevated `./eng/verify-godot.sh` pass, including the
16-beat Chapter 1 flow, persistence/input smoke and existing full-game
regressions. No narrative state, save schema, content, shader, asset or
Acts 2–5 launch path changed. The transition is presentation-only and the art,
audio, cultural and observed-playtest gates remain OPEN.

## 2026-08-14 — Make the old-PC interaction authored and fail closed

Status: Accepted Act 1 demo reliability repair; Acts 2–5 remain outside the current release

Context: The first-person smoke exposed that the physical old-PC target was
relying on `RuntimeBridge`'s generic `world.interact` fallback instead of an
authored compiled interaction. That made a missing content ID look playable
and allowed a scene-local target to bypass the data-driven campaign contract.

Decision: Add `urman.chapter1:interaction/oldpc-power` to the Chapter 1 house
scene, route the physical CRT target through that ID, and make unknown
interaction IDs unavailable and non-dispatchable. The Kara-Urman boundary
marker remains visual-only unless a future campaign definition owns its
interaction. RuntimeBridge remains the sole state owner; no generic fallback,
new save path or second narrative owner is introduced.

Evidence: regenerated Chapter 1/full-game compiled packs and golden fixture;
clean .NET build/tests pass; elevated `./eng/verify-godot.sh` passes the
first-person ray → keyboard E → OldPc UI smoke, the dedicated Act 1 launch and
the full 16-beat Chapter 1 flow. The old web runtime and Acts 2–5 production
scope are unchanged. Art, audio, cultural review and observed playtest remain
OPEN.

## 2026-08-14 — Keep deferred campaign transition overrides portable

Status: Accepted compiler-parity repair; no change to the Act 1 release scope

Context: The deferred `urman.fullgame` campaign intentionally adds its Act 2
transition to the Chapter 1 forest scene through a campaign-level
`transitionOverrides` record. The JS content compiler validated the target but
did not materialize the synthetic interaction, so its whole-pack closure test
reported the required Act 5 entry as unreachable or rejected the cross-module
reference.

Decision: Materialize each validated override as an authored, deterministic
scene interaction in the compiled JS pack, preserve the override in the pack
fingerprint, and exempt only that synthetic interaction's already-validated
references from module-local dependency checks. Do not add a runtime fallback,
change Chapter 1 content, or move Acts 2–5 into the current demo launch.

Evidence: the two Chapter 1/content-closure Node suites pass 9/9; C#/.NET and
Godot demo receipts remain green. The full-game campaign and its transition are
still deferred foundation, not current Act 1 acceptance.

## 2026-08-14 — Crossfade Act 1 zone ambience without adding a second runtime owner

Status: Accepted bounded presentation improvement; authored audio and cultural listening remain OPEN

Context: The Act 1 demo already used four project-original WAV beds, but
`AmbientAudioDirector` stopped the current player and replaced its stream at
each zone change. That made the visual fade feel disconnected from the sound
and exposed a hard cut between village, house, FAP, zirat and Kara-Urman.

Decision: Keep `AmbientAudioDirector` as the only continuous-ambience owner and
give it two in-memory `AudioStreamPlayer`s. The incoming stem fades in while
the previous stem fades out over 0.65 seconds; the old stream is stopped and
released after the tween. Headless mode remains deterministic and does not
start playback. No runtime state, story event, save format, shader, asset
license or Acts 2–5 launch path changes.

Evidence: `AmbientAudioSmokeTest` passes the two-player contract and all zone
mappings in `./eng/verify-godot.sh`; a real Metal 4.0 / Forward+ non-headless
smoke observes `village_day → house_old_pc → kara_urman_night` with the
crossfade path. Project-original stems, authored voice, final mix, cultural
listening and external playtest remain separate gates.

## 2026-08-14 — Preserve Act 1 audio captions when a logical voice has no recording

Status: Accepted bounded accessibility/presentation repair; external review remains OPEN

Context: The Chapter 1 voice assets are intentionally logical references until
recording. With audio descriptions disabled, `AudioCueUi` previously dropped a
cue completely when its physical stream was absent, even though an authored
caption existed.

Decision: Keep the existing preference order for playable audio and audio
descriptions. If the stream is unavailable, use the authored caption whenever
subtitles are enabled and audio descriptions are disabled. This keeps the
demo's Rinat/Marat cues understandable without pretending a placeholder WAV is
a final performance; no content IDs, runtime state or save data change.

Evidence: `ChapterOneFlowSmokeTest` reaches the forest cliffhanger with the
default non-audio transcript and then verifies the caption fallback under
`Subtitles=true, AudioDescriptions=false`; the full Godot smoke remains green.

## 2026-08-14 — Make the Act 1 opening discoverable for keyboard and gamepad

Status: Accepted bounded first-time usability repair; external parity review remains OPEN

Context: The demo already detected the last input device and changed nearby
interaction prompts between `[E]` and `[A]`, but the opening card only taught
keyboard controls and disappeared on a timer. A gamepad player could start
without knowing the left/right stick layout or the journal button.

Decision: Keep the Act 1 demo presentation wrapper as the only owner of the
opening card. Render keyboard or gamepad movement/look/journal hints from the
player's last detected device, allow the mapped `interact`/`ui_accept` action or
left mouse click to dismiss the card, and keep the player modal until dismissal.
The backing shade may soften after the opening beat, but the card never
auto-dismisses: a player must explicitly confirm before movement unlocks. This
does not add input actions, change remapping, mutate narrative state or make
Acts 2–5 reachable.

Evidence: `Act1DemoLaunchSmokeTest` now asserts the initial keyboard wording,
waits past the former timer window without input, switches the real controller
to gamepad wording through a joypad event and dismisses the intro through the
mapped gamepad interact action. `FirstPersonInteractionSmokeTest` verifies the
resulting `[A]` prompt on the authored HouseDoor. Elevated real Godot targeted
smoke and the full `./eng/verify-godot.sh` suite pass. This is internal
discovery evidence, not an observed multi-controller playtest or final
accessibility acceptance.

## 2026-08-15 — Add an explicit safe graphics launch for the Act 1 demo

Status: Accepted bounded performance diagnostic; normal profile and release gates unchanged

Context: The local Apple M4 Pro reproduces about 120 FPS in the real Act 1
entrypoint, but a report of roughly 1 FPS is still plausible on a weak,
unsupported or software GPU. The normal painterly material performs three
triplanar albedo reads per fragment, so simply lowering window scale may not
be enough to diagnose that path.

Decision: Keep the normal launch at `medium` with the full painterly shader. Add
the explicit `--urman-safe-mode` launch used by `eng/run-act1-demo-safe.sh`:
Godot Mobile renderer, low render scale/MSAA and a material branch that skips
triplanar albedo reads. The mode preserves the same zones, meshes, input,
runtime state, saves and narrative route; it is a presentation diagnostic, not
a second gameplay owner or an automatic renderer fallback.

Evidence: The default launch smoke still requires `medium`, 0.90 scale, 2×
MSAA and full-quality materials. The safe path is documented separately and
must be measured on the target OS/GPU before any M1/Windows performance gate is
closed. Art lock, visual comparison, first-time playtest and release hardware
acceptance remain OPEN.

Linked files: `game/scripts/PainterlyMaterialLibrary.cs`,
`game/scripts/FirstPersonController.cs`, `eng/run-act1-demo-safe.sh`,
`docs/urman_knowledge_base/performance/README.md`

## 2026-08-15 — Measure the packaged Act 1 entrypoint without scene overrides

Status: Accepted bounded diagnostic tooling; release and target-hardware gates unchanged

Context: Godot's exported binary is compiled without command-line scene-path
overrides, so the editor performance scene could not prove that the published
macOS demo package itself starts at the same frame time.

Decision: Add the presentation-only `--urman-perf-probe` branch to
`Act1DemoRoot` and the `eng/benchmark-act1-demo-package.sh` wrapper. The probe
uses the normal `act1_demo.tscn` main scene, records warm-up plus 60 frame
intervals, prints average/p95/max/FPS and exits. The wrapper extracts the
published macOS ZIP into a temporary directory, supports the existing
`--urman-safe-mode` diagnostic and removes the extraction after exit. It does
not mutate runtime state, saves, narrative owners or graphics defaults.

Evidence: fresh medium and safe package probes exit 0 at about 120 FPS in real
Metal windows (medium Forward+, safe Forward Mobile) on the local Apple M4 Pro.
M1/Windows performance, Windows host execution, human
playtest, authored audio, cultural review and art lock remain open.

Linked files: `game/scripts/Act1DemoRoot.cs`,
`eng/benchmark-act1-demo-package.sh`,
`docs/urman_knowledge_base/performance/README.md`

## 2026-08-15 — Add a physical traversability gate to the Act 1 demo

Status: Accepted bounded test-only evidence; human playtest and art lock remain open

The demo keeps the existing deterministic corridor smoke for exact narrative
parity, and adds `act1_first_person_walkthrough_smoke_test.tscn` as a separate
test-only gate. The new route drives `move_forward` through the production
`FirstPersonController` and `MoveAndSlide`, uses the camera ray for every
interaction, and crosses approximately 93 m from arrival to Kara-Urman. A
small internal look setter is limited to the smoke because headless Godot has
no captured mouse device; it does not change player position, runtime state,
saves, shaders or collision ownership. This evidence proves physical
traversability only; human wayfinding, comfort, cultural review and the art
lock remain open.

Linked files: `game/tests/Act1FirstPersonWalkthroughSmokeTest.cs`,
`game/tests/act1_first_person_walkthrough_smoke_test.tscn`,
`eng/verify-godot.sh`, `docs/urman_knowledge_base/playtest_plan.md`

## 2026-08-15 — Remove per-frame narrative polling from physical interaction targets

Status: Accepted bounded performance repair; target-host FPS and human playtest remain OPEN

Context: `InteractionTarget._Process` called `RuntimeBridge.IsInteractionAvailable`
every frame. That path serialized the full kernel state for each target even
when no narrative command had changed. The behavior was correct but created
avoidable CPU work in the first-person loop and was a plausible amplifier for a
hardware-specific single-digit-FPS report.

Decision: Keep `RuntimeBridge` as the only narrative-state owner and add a
coalesced main-thread `RuntimeStateChanged` invalidation. `InteractionTarget`
subscribes on entry, refreshes its cached availability/collision layer/mesh
visibility after committed state or zone changes, and unsubscribes on exit.
`FirstPersonController` now reads the cached target result; commands, scene
ownership, saves and content IDs are unchanged.

Evidence: fresh C# build has zero warnings/errors; elevated `./eng/verify-godot.sh`
passes scene/import, old-PC, journal, persistence, input/ray, physical 93 m
walkthrough, Chapter 1 and collision/full-game regressions. Real source-tree
Metal probes report 119.97 FPS medium Forward+ and 119.99 FPS safe Mobile/low
on the local M4 Pro. This is not M1/Windows evidence and does not claim the
user's reported 1 FPS is fixed until their target run is captured.

Linked files: `game/scripts/RuntimeBridge.cs`,
`game/scripts/InteractionTarget.cs`,
`docs/urman_knowledge_base/performance/README.md`

## 2026-08-15 — Remove frame-loop state polling from the Act 1 ending detector

Status: Accepted bounded performance repair; target-host FPS and human playtest remain OPEN

Context: After interaction targets were moved to notification-driven cache
refresh, `Act1DemoRoot._Process` still serialized the full runtime state every
frame to discover the completed «Не отвечай» beat. That was a second avoidable
CPU path in the demo's first-person loop.

Decision: Subscribe `Act1DemoRoot` to the same coalesced
`RuntimeBridge.RuntimeStateChanged` event. Evaluate the forest/cliffhanger rule
only after a committed state or zone change, then let `_Process` advance only
the already-authorized one-second fade delay. Narrative ownership, ending beat,
scene routing, saves and the demo boundary remain unchanged.

Evidence: Fresh C# build, full `./eng/verify-godot.sh`, fresh macOS/Windows
package export and read-only desktop receipt check pass. The latest real-window
package probes report 119.80 FPS medium Forward+ and 119.85 FPS safe Mobile/low
on the local Apple M4 Pro. This is not M1/Windows evidence and does not claim
the user's 1 FPS is fixed until the target machine is measured.

Linked files: `game/scripts/Act1DemoRoot.cs`,
`game/scripts/RuntimeBridge.cs`,
`docs/urman_knowledge_base/performance/README.md`

## 2026-08-15 — Use the authored OldPc GLB as the Act 1 house focal prop

Status: Accepted bounded presentation slice; art lock and target-host FPS remain OPEN

Decision: Replace the Act 1 house's duplicate procedural CRT, keyboard, tower
and scanline boxes with `GeneratedOldPcAct1`, an `OldPc_` 8/8 LOD module attached
through `GeneratedModularKitDressing.AttachPresentationOnly` at the existing
interaction anchor. Keep the procedural table, documents, mouse, lamp and the
layer-1 `InteractionTarget` with id
`urman.chapter1:interaction/oldpc-power`. The adapter's imported-collision
sanitation is the only GLB physics boundary; no narrative, save, shader or
Acts 2–5 owner changes.

Evidence: `SceneSmokeTest` and `StyleFrameCapture` require exact module
metadata, 8/8 LOD counts, positive imported-collision removal, zero module
physics descendants and the unchanged interaction shape. Fresh full Godot
smoke passes; the house Metal/Forward+ frame is recorded in
`art/style_frames/README.md`; fresh package probes remain about 119.85 FPS
medium and 119.75 FPS safe Mobile/low on the local M4 Pro. This is progress
toward the playable atmospheric demo, not final hero-prop approval or art lock.

Linked files: `game/scripts/StyleBenchmarkZone.cs`,
`game/scripts/GeneratedModularKitDressing.cs`,
`game/tests/SceneSmokeTest.cs`, `game/tests/StyleFrameCapture.cs`,
`docs/urman_knowledge_base/act1_demo_handoff.md`

## 2026-08-15 — Raise Kara-Urman night readability without changing gameplay owners

Status: Accepted bounded presentation calibration; visual art lock and target-host FPS remain OPEN

Context: The first-person Kara-Urman frame preserved the intended cold,
restrained horror mood, but its path and foreground trees collapsed into a
single blue-black value mass. That weakens both atmosphere and safe first-time
wayfinding at the Act 1 cliffhanger.

Decision: Adjust only the Chapter 1 benchmark environment: cold ambient
`7b9096 @ 0.64`, fog `52666d @ 0.0034` with height density `0.075`, directional
night energy `0.82`, and local `MoonFill` `8198a0 @ 1.10`. Do not add a gameplay
light, change materials/shaders, alter collision or narrative state, or
propagate the values to full-game zones. Keep the frame as production-progress
evidence, not an art-lock decision.

Evidence: `.NET build`, elevated `./eng/verify-godot.sh` and real Metal/Forward+
style capture pass. Current frames are recorded in
`art/style_frames/README.md` with day
`0891fdbc18f4119028b6de5c9147f0444e1ba04b74afc334cd95d8c000dc377f`, house
`bcb0a1d92c694161d84ef50051de578a54b6ab4c200435662d4160fea83428cb` and Kara
`c8d73ce00425fc791f12438d7c52195f544322f03f027bcb633e545c058fe9c3`. Visual,
cultural, motion and release-host review remain open.

Linked files: `game/scripts/StyleBenchmarkZone.cs`,
`docs/urman_knowledge_base/art/style_frames/README.md`,
`docs/urman_knowledge_base/weak_points.md`

## 2026-08-15 — Add a fail-soft startup guard for the reported 1 FPS path

Status: Accepted bounded presentation/performance diagnostic; release-host gate remains OPEN

Context: The local M4 Pro does not reproduce the report (fresh package:
`118.10 FPS` medium Forward+ and `119.33 FPS` safe Mobile/low), so the cause
cannot honestly be assigned to the demo runtime without the target OS/GPU and
launch path. A completely unusable first launch on a weak/software GPU is still
a valid UX risk.

Decision: `Act1DemoRoot` now samples a few post-warm-up frames and, only after a
median/average slow-start gate, asks `FirstPersonController` to switch the
current session to the existing low material/render-scale profile. The guard is
disabled for `--urman-perf-probe` and `--print-fps`; `--print-fps
--no-auto-performance-fallback` is the reproducible diagnostic path. This is
presentation-only: no zone, kernel, narrative, save, collision or renderer
architecture owner changes.

Evidence: C# build and Godot smoke pass; fresh desktop export and read-only
receipt pass. The exported package contains the guard and the diagnostic branch.
Target-host Windows/M1 performance, editor-vs-package behavior and human
playtest remain OPEN.

Linked files: `game/scripts/Act1DemoRoot.cs`,
`game/scripts/FirstPersonController.cs`,
`docs/urman_knowledge_base/performance/README.md`,
`docs/urman_knowledge_base/act1_demo_handoff.md`

## 2026-08-15 — Show the active Act 1 objective in the journal as a read-only projection

Status: Accepted bounded first-time-playtest UX repair; observed comprehension and art lock remain OPEN

Context: The old-PC flow already committed a journal entry and the runtime quest
kernel already derived the next active objective, but the journal UI displayed
only saved entries. After closing the document, a first-time player could lose
the immediate investigation step even though the narrative state was correct.

Decision: Add `RuntimeBridge.ActiveObjectives()` as a read-only presentation
projection and render the active objective title in the existing journal. The
journal does not dispatch commands, mutate quest state, add a second objective
owner, create a GPS marker or change SaveGameV3. If no objective is active, the
block remains neutral.

Evidence: `.NET` build, targeted `journal_flow_smoke_test.tscn` and full elevated
`./eng/verify-godot.sh` pass. The external first-time playtest must still check
whether the wording is noticed and whether diegetic landmarks remain sufficient.

Linked files: `game/scripts/RuntimeBridge.cs`, `game/scripts/JournalUi.cs`,
`game/scenes/ui/journal_ui.tscn`, `game/tests/JournalFlowSmokeTest.cs`,
`docs/urman_knowledge_base/act1_demo_handoff.md`

## 2026-08-15 — Point to the journal after an explicit clue save

Status: Accepted bounded first-time-playtest UX repair; no new navigation owner

Context: The read-only active-objective block is useful only if the player knows
that saving an old-PC or physical document has updated the journal. A silent
status line made that transition easy to miss, especially on gamepad.

Decision: Reuse the existing save-status label in `OldPcUi` and `DocumentUi` to
append the current journal shortcut (`[J]` or `[Y]`). This is a local
presentation message: it does not create a quest marker, dispatch a command,
change the runtime state or alter SaveGameV3.

Evidence: `.NET` build and the full Godot smoke suite remain required after the
UI change; wording and timing still need an observed first-time playtest.

Linked files: `game/scripts/OldPcUi.cs`, `game/scripts/DocumentUi.cs`,
`docs/urman_knowledge_base/act1_demo_handoff.md`

## 2026-08-15 — Preserve both Act 1 cliffhanger audio cues in presentation order

Status: Accepted bounded presentation repair; authored voice/mix and cultural listening review remain OPEN

Context: The forest scene emits Marat's voice trace and Rinat's interruption in
one runtime transition. Because both current assets are logical refs without
physical recordings, the old single-label `AudioCueUi` replaced Marat's text
with Rinat's text in the same frame; the player could miss the first half of
the audio-first cliffhanger entirely.

Decision: Keep the runtime event order and add a short presentation-only queue
inside `AudioCueUi`. The first cue remains visible, then the second cue appears,
and only after that does `Act1DemoRoot` show `НЕ ОТВЕЧАЙ`. `LastPresentedText`,
runtime events, beats, saves, accessibility settings and content IDs remain
unchanged; no authored audio file is fabricated by this change.

Evidence: `.NET` build and `chapter_one_flow_smoke_test.tscn` pass. The smoke
asserts Marat text is the visible first cue, Rinat is queued second, both are
recorded in presentation history and the final card still appears. The queue
is a technical fallback until voice recording, final mix, subtitle editing and
cultural listening review are complete.

Linked files: `game/scripts/AudioCueUi.cs`, `game/scripts/Act1DemoRoot.cs`,
`game/tests/ChapterOneFlowSmokeTest.cs`,
`docs/urman_knowledge_base/act1_demo_handoff.md`

## 2026-08-15 — Face Act 1 compact-zone spawns toward the next landmark

Status: Accepted bounded first-person wayfinding repair; observed first-time comprehension and art lock remain OPEN

Context: `Main.SwitchZone` previously preserved the yaw from the doorway or
interaction that triggered a transition. That left the player looking back at
the previous zone; the zirat spawn in particular faced the village while the
Kara-Urman route was behind the player. The existing corridor smoke hid this
because it aimed the camera before every interaction.

Decision: Add a destination-facing spawn transform for Act 1 compact zones.
House, street, FAP, zirat and forest arrivals face the next route landmark
along `-Z`; the optional forest-to-village return faces `+Z`. The transform is
presentation-only and is applied by `FirstPersonController.ApplyZoneSpawn`; it
does not add a GPS marker, command, quest owner, interaction ID, save field or
runtime-state mutation. Full-game zones retain their stable forward entry
contract for now.

Evidence: `act1_first_person_corridor_smoke_test.tscn` now asserts the declared
spawn yaw after every Act 1 transition; the physical first-person walkthrough
passes 93.05 m to Kara-Urman; full `./eng/verify-godot.sh` passes with zero build
warnings/errors. Human input/wayfinding, cultural review, release hardware and
final art lock remain separate gates.

Linked files: `game/scripts/Main.cs`, `game/scripts/FirstPersonController.cs`,
`game/tests/Act1FirstPersonCorridorSmokeTest.cs`,
`docs/urman_knowledge_base/art/first_person_corridor/README.md`

## 2026-08-15 — Recheck the Act 1 package after the spawn-facing repair

Status: Accepted as current technical evidence; user-reported ~1 FPS and
release-hardware performance remain OPEN

Context: The destination-facing spawn repair changed the source entrypoint, so
the debug packages and their receipt had to be rebuilt before treating the
previous performance numbers as current.

Evidence: `./eng/export-desktop-debug.sh`,
`./eng/verify-desktop-artifacts.sh --check-receipt` and
`./eng/verify-macos-host.sh` pass. The published macOS package measured
`79.36 FPS` in a serialized medium Forward+ window and `89.04 FPS` in the
safe Mobile/low window on the local Apple M4 Pro. This does not reproduce a
1 FPS stall and does not close M1/Windows or target-user reproduction.

Current artifacts: macOS ZIP
`5a213a569ce68dff683d77a876db62d23e7d0af07b7d0501730077eb513130ed`, Windows
ZIP `f9addda9847d810064280a2b4a8a15393166898318924c0521b07cf1590a0620`,
Windows EXE
`4e0b621de75c53635bd034ea5bebb5890e05e2eee02d5b1dfd56941921e5869a`.

Next evidence required: target OS/GPU, editor-versus-package launch path,
display scaling and complete `--print-fps` output. Do not change materials,
geometry or the kernel based only on the unscoped report.

Linked files: `eng/export-desktop-debug.sh`,
`eng/verify-desktop-artifacts.sh`, `eng/verify-macos-host.sh`,
`docs/urman_knowledge_base/performance/README.md`

## 2026-08-15 — Make the first Tatar vocabulary state visible in the Act 1 journal

Status: Accepted bounded presentation projection; language and cultural review remain OPEN

Context: The Act 1 runtime already committed the first vocabulary state when
the player entered the village, but the playable journal only exposed saved
entries and the active objective. A first-time player could therefore complete
the first language beat without seeing what had been learned.

Decision: Parse the campaign vocabulary registry into the compiled content
repository and expose learned `guessed`/`confirmed` entries through a read-only
`RuntimeBridge.LearnedVocabulary()` projection. The existing journal renders the
term and meaning (`урман — граница старых правил`) without dispatching a
command, changing kernel state, adding a second vocabulary owner or changing
SaveGameV3. The projection is intentionally available in the Act 1 demo only
through the existing journal surface; it does not make Acts 2–5 reachable.

Evidence: `.NET` build, targeted `journal_flow_smoke_test.tscn` and full
`./eng/verify-godot.sh` pass. The smoke checks the actual journal label for both
the term and its meaning. Human comprehension, native-speaker wording review
and final localization remain OPEN.

Linked files: `game/scripts/CompiledCampaignRepository.cs`,
`game/scripts/RuntimeBridge.cs`, `game/scripts/JournalUi.cs`,
`game/scenes/ui/journal_ui.tscn`, `game/tests/JournalFlowSmokeTest.cs`,
`docs/urman_knowledge_base/act1_demo_handoff.md`

## 2026-08-15 — Rebind the Act 1 demo package after the vocabulary UI change

Status: Accepted structural/package evidence; target-host performance remains OPEN

The desktop packages were rebuilt after the journal vocabulary projection so
the handoff describes the same bits that a player receives. Read-only receipt
verification and elevated macOS host smoke pass. Real-window probes on the
local Apple M4 Pro report `119.82 FPS` medium Metal/Forward+ and `120.04 FPS`
safe Forward Mobile/low; this still does not reproduce the external 1 FPS
report or close M1/Windows execution.

Current hashes: macOS ZIP
`e38176747d9eecaf3fc25207d34a138197200e18dfc90c984279f9cfd58b7751`, Windows
ZIP `b93fcc10ca090dca5ba3e780aa403a3e3e533226c2ed4a84d138379a3d8ea56c`,
Windows EXE
`ca2ccde016413ea88a77146676716643657bd0f343f39ecff8805e063582440e`, receipt
`567e45835cbdbcf7c1ffbeafbf0933d65ce82a19fe367014c3d4e309a30c47be`.

Linked files: `eng/export-desktop-debug.sh`,
`eng/verify-desktop-artifacts.sh`, `eng/verify-macos-host.sh`,
`eng/benchmark-act1-demo-package.sh`,
`docs/urman_knowledge_base/performance/README.md`,
`docs/urman_knowledge_base/act1_demo_handoff.md`

## 2026-08-15 — Supersede benchmark-demo framing with a connected Act I world gate

Status: Accepted priority change; connected greybox and visual continuity are now the active gate

Context: The Godot route, smoke tests and three style captures proved that
individual zones and transitions work, but `Main.SwitchZone` still frees the
current `ZoneHost` child and loads one benchmark scene at a time. A first-person
player can therefore see empty backsides, greybox edges and isolated rooms when
turning around. Local FPS and a clean package do not fix that product problem.

Decision: Stop treating the current build as an atmospheric player-ready demo.
Prioritize one compact, connected Act I territory containing the arrival,
village road, Babay/Äbi yard and house, populated street, FAP, return road,
zirat and Kara-Urman approach. The next implementation slice may compose the
existing five Chapter 1 zone builders in one presentation world and map logical
scene transitions to world-space spawns without changing RuntimeBridge,
SaveGameV3, compiled IDs or story beats. Benchmark frames, texture candidates,
FPS probes and desktop packages remain supporting evidence only until a real
first-person 360°/near-mid/far pass confirms continuity.

Consequences: No new texture variants, FPS optimization, packaging or Acts II–V
production work is prioritized before the connected greybox gate. The demo is
not called ready or art-locked while principal views still read as isolated
scenes, greybox boxes or repeated procedural cones. The existing one-zone path
remains the rollback path for tests and non-demo entrypoints.

Linked files: `docs/aegis/plans/2026-08-15-act1-connected-greybox.md`,
`game/scripts/Main.cs`, `game/scripts/Act1DemoRoot.cs`,
`game/scripts/StyleBenchmarkZone.cs`,
`docs/urman_knowledge_base/act1_demo_handoff.md`,
`docs/urman_knowledge_base/release_gate_matrix.md`

## 2026-08-15 — Record startup warm-up and compact-zone transition costs

Status: Accepted diagnostic evidence; no new runtime performance owner

Context: A report of approximately 1 FPS was not reproduced by the current
package or source probe. A steady-state average alone could still hide a first
launch hitch or a synchronous zone-load stall, so the diagnosis needed both
warm-up samples and transition timings.

Decision: Extend the diagnostic-only `--urman-perf-probe` output with the first
20-frame warm-up average/max and extend `ZoneFlowSmokeTest` with one-frame
transition timings. No gameplay loop, shader, renderer default, save format or
narrative state changes are made by this evidence slice.

Evidence: source medium warm-up `12.993/64.829 ms` average/max followed by
`120.34 FPS`; source Mobile/low warm-up `10.873/48.195 ms` followed by
`120.11 FPS`. Current compact-zone transitions measure `29.9 ms` house,
`16.8 ms` Kara-Urman and `25.8 ms` return-to-village. The freshly exported
package measures `119.96 FPS` medium and `120.02 FPS` safe on the local M4 Pro;
receipt and macOS host smoke pass. Target OS/GPU and editor-vs-package launch
remain the only evidence needed to classify the external 1 FPS report.

Linked files: `game/scripts/Act1DemoRoot.cs`,
`game/tests/ZoneFlowSmokeTest.cs`,
`docs/urman_knowledge_base/performance/README.md`

## 2026-08-15 — Separate Act 1 forest value planes with a deterministic pine palette

Status: Accepted bounded presentation improvement; authored canopy and art lock remain OPEN

Context: The Act 1 Kara-Urman frame read as a repeated cone family with weak
near/mid/far separation. A new texture or shader would have expanded the
production surface without solving the immediate value-grouping problem.

Decision: Keep the existing PineA geometry, collision ownership, materials,
renderer, route and narrative state. Assign five muted foliage colors from a
stable world-space phase in `StyleBenchmarkZone.MakePine`, so repeated trees
retain deterministic variation without adding meshes, physics, runtime state
or Acts II–V dependencies.

Evidence: fresh build and `./eng/verify-godot.sh` pass with zero build
warnings; physical Act 1 walkthrough reaches the Kara-Urman cliffhanger over
93.05 m. Real Metal/Forward+ style capture is 1920×1080 with day
`4c99a5483d8d9025503e320bb12dbd6b117a1e446fa78e22b6fa619de533bccb`, house
`115b617d91a0ea47bf65b3f46b200117f24ca5e921610845d68e124325af6903`, Kara
`ff3f37535d62f31bea4c2add821817a58fb1f3e901e105dbb1722f77e59e6826`.
Source probes remain around 120 FPS on the local M4 Pro; target hardware and
the reported external 1 FPS issue remain OPEN.

Linked files: `game/scripts/StyleBenchmarkZone.cs`,
`docs/urman_knowledge_base/art/style_frames/README.md`,
`docs/urman_knowledge_base/act1_demo_handoff.md`,
`docs/urman_knowledge_base/performance/README.md`

## 2026-08-15 — Rebind the Act 1 demo receipt after the pine palette export

Status: Accepted packaging evidence; target-host performance remains OPEN

The desktop packages were rebuilt after the deterministic foliage presentation
pass. Structural receipt check and elevated macOS host smoke pass. The fresh
macOS package measures `120.15 FPS` in medium Metal/Forward+ and `120.07 FPS`
in safe Forward Mobile/low on the local Apple M4 Pro. This does not reproduce
the reported 1 FPS and does not close Windows/M1 or target-driver evidence.

Current hashes: macOS ZIP
`d9eaa76e57f29fe4dec919d21f3384f1a7f5564efd1b88ce247bce002917d7f0`, Windows
ZIP `526aae396324e45504101ee9943824c3a1ed7ddd5c1a8a8daef06d56042391fe`,
Windows EXE `4e0b621de75c53635bd034ea5bebb5890e05e2eee02d5b1dfd56941921e5869a`,
receipt `2a9fa47531fcd4d01db7c2f00a73173d49794f9d0ea7df907de70562daad7a26`.

Linked files: `eng/export-desktop-debug.sh`,
`eng/verify-desktop-artifacts.sh`, `eng/verify-macos-host.sh`,
`docs/urman_knowledge_base/performance/README.md`

## 2026-08-15 — Зафиксировать Luna-only оркестрацию работы над УРМАНОМ

Status: Accepted workflow decision; process governance only, game canon unchanged

Context: Для работ над УРМАНОМ нужен единый пользовательский процесс, который
не допускает незаметной смены модели и сохраняет отдельные видимые задачи для
реализации, документации, тестов, захватов, итераций и делегированного
исследования.

Decision: Не использовать Sol-субагентов. Все перечисленные виды работы
выполнять только в отдельных пользовательских задачах на GPT-5.6 Luna.
Родительский Sol-чат остаётся только оркестратором, архитектором и владельцем
финальной приёмки, минимизирует собственную работу и может выполнять лишь
небольшие независимые проверки diff/test/frame, прямо требуемые
acceptance-контрактом, но не реализует
изменения и не запускает Sol-субагентов. Модель нельзя молча подменять, а
fallback запрещён: если GPT-5.6 Luna недоступен, работу нужно остановить и
сообщить о блокере.

Consequences: Это обязательное правило процесса и делегирования, а не решение
о сюжете, лоре, геймплее или технической архитектуре. Активный объект цели в
приложении этой записью не изменяется.

Linked files: `../../AGENTS.md`,
`docs/aegis/plans/2026-08-15-act1-connected-greybox.md`

## 2026-08-17 — Make the complete Act I connected world the mandatory next implementation unit

Status: Accepted production-priority and evidence correction; not a ready-demo or art-lock decision

Context: Review of the recent work showed that isolated Blender assets, style
frames, benchmarks, packaging and local beauty passes were receiving effort
before the first-person route existed as one shared world. The three style
frames and smoke receipts are insufficient for visual acceptance.

Decision: The next product milestone is one complete first-person, shared-world,
full-route Act I greybox/core-world in `game/scenes/act1_demo.tscn`: arrival →
village → двор/дом бабая и әби → FAP → return → zirat → Kara-Urman forest edge
→ «НЕ ОТВЕЧАЙ». It must use the existing route, collision, spawn and
`RuntimeBridge` owners; presentation changes may not add narrative-state
owners. The core-world must be traversable forward and back without a
180°/360° void and must receive real Godot first-person evidence across the
eight visual zones before authored material, light or detail work counts as
meaningful progress. The existing three-style-frame/smoke evidence is
insufficient for visual acceptance; this supersedes only its interpretation,
not the historical record or Act I scope.

Consequences: After full core-world passes, production order is
terrain/silhouettes → architecture/boundaries → foliage → materials →
light/rain → full 360° review. Do not call the current build a ready demo.
This is an execution-priority and evidence decision, not art lock, release
readiness or full-game scope expansion.

Linked files: `game/scenes/act1_demo.tscn`,
`docs/aegis/plans/2026-08-15-act1-connected-greybox.md`,
`docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md`

## 2026-08-21 — A/B verdict: Agent B experimental world vs main Act I line

Status: Accepted as evidence-based comparison; Agent B variant remains a
non-canonical experiment, not an integrated demo

Context: The isolated Agent B checkout
(`/Users/unterlantas/Documents/GitHub/URMAN__agent-b-act1`) delivered a
physically traversable authored world (5 GLB kits, 11/11 physics-walk
waypoints, 45 verified frames, 0 build warnings) on top of a copy of the main
checkout. The main line has the production route, RuntimeBridge ownership,
narrative flow, interior house with the old PC, and smoke/capture harnesses,
but its exterior presentation layer is still a documented PARTIAL greybox.

Verdict by part:

- Terrain/road: Agent B better — deterministic height field, road crown/ruts,
  ditches, puddles; main line is flat presentation envelopes.
- Village architecture: Agent B better for exteriors — authored houses, FAP,
  mosque landmark, fences, well; main line keeps benchmark boxes in key views.
- Foliage: roughly even — both avoid repeated cones; Agent B has 10 planted
  silhouette families, main line has authored landmark trees.
- Zirat: Agent B better — enclosure, gate, path, culturally neutral markers.
- Kara-Urman edge: main line slightly better in readability/lighting; Agent B
  night is very dark (mean luma ~0.04) and needs an exposure pass.
- Atmosphere: Agent B better — unified rain, fog, day-to-night gradient.
- Traversal evidence: Agent B strictly stronger — real CharacterBody3D walk
  receipt vs main-line waypoint audit without physical traversal.
- Narrative/gameplay: main line strictly stronger — Agent B has no runtime,
  interactions, dialogue, documents, PC, or narrative state by design.
- Verification discipline: comparable and strong on both sides.

Decision: Adopt Agent B's exterior-world approach (terrain + authored village
kits + atmosphere + physical-traversal capture harness pattern) as the
production direction for the Act I core world. Integration into
`game/scenes/act1_demo.tscn` must preserve RuntimeBridge as the only
narrative-state owner and must not import Agent B scene code wholesale.
Kara night readability and zirat marker cultural review remain open gates.
Neither variant is declared demo-ready.

Consequences: Next implementation unit is integration of authored exterior
kits into the production route with first-person visual review. The main
line's greybox document stays PARTIAL until that integration passes 360°
review. This decision does not constitute art lock or release readiness.

Linked files: `docs/experiments/agent_b_act1_world_report.md` (in
`URMAN__agent-b-act1`), `game/scenes/act1_demo.tscn`,
`docs/urman_knowledge_base/art/act1_core_world_greybox_2026-08-17.md`

## 2026-08-22 02:00 — Agent B exterior kits integrated into the production Act I connected world

Status: Implemented and verified; not an art lock and not release readiness

Context: The consolidated Agent B exterior world (five authored GLB kits,
deterministic terrain, unified rain/day-night atmosphere) lived only in the
experimental scene. The production route in `act1_demo.tscn` still used the
PARTIAL greybox presentation layer.

Decision:

- New production layer `AgentBExteriorWorld` (`AgentBAct1ExteriorLayer.cs`)
  mounts all five kits inside `Act1CoreWorldGreybox`, adds the deterministic
  terrain traversal collider (layer 1) and kit architecture collision with
  verified walk-through decor rules.
- Interior zone footprints (house_old_pc, fap_clinic), the Babai house kit
  volume and the zirat fence wall are excluded from exterior collision by
  AABB overlap so interior owners and the zirat_road route corridor stay
  authoritative. No InteractionTarget, spawn, RuntimeBridge or save owner
  changed.
- Rain follows the camera and thickens toward Kara-Urman (900→1600
  particles); night readability at the forest edge improved via fog color,
  ambient energy and sun energy lerp.
`project.godot` pins `physics/3d/default_gravity=21.6` so the production
player matches the verified Agent B traversal contract.

Evidence (all headless, no desktop window):

- `dotnet build game/Urman.Game.csproj`: 0 errors / 0 warnings.
- `eng/verify-dotnet.sh`: exit 0; content pack + campaign simulate valid.
- `eng/verify-godot.sh`: full smoke set green including
  `act1-first-person-corridor`.
- Physical walkthrough (`act1_first_person_walkthrough_smoke_test`): PASS,
  91,76 m on foot through arrival → house → old PC → FAP → documents →
  Rinat → evidence → zirat → Kara-Urman cliffhanger completed.

Consequences: The Act I exterior presentation is now the authored Agent B
world inside the production demo path. Remaining gates unchanged: cultural
review of zirat markers, Tatar language editing, motion comfort, M1/Windows
performance acceptance, final optimization pass. This record does not declare
art lock or release readiness.

Linked files: `game/scripts/Act1ConnectedWorld.cs`,
`game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs`,
`game/scripts/experiments/agent_b_act1/AgentBFoliagePlan.cs`,
`game/project.godot`

## 2026-08-24 — Bound the full-route visual capture watchdog

Status: Accepted QA-infrastructure correction; visual acceptance remains OPEN

Context: The first production full-route core-world capture remained alive
without writing a PNG. Its log reached the connected-world bootstrap and the
test-only harness then awaited `RenderingServer.FramePostDraw` without a
timeout. This blocked evidence collection and could steal the desktop focus
for hours.

Decision: `Act1FullRouteCoreWorldCapture` now uses a short `ProcessFrame` settle
window before root-viewport readback, while
`eng/capture-act1-full-route-core-world.sh` wraps the single Godot capture in a
300-second default watchdog configurable through
`URMAN_CAPTURE_TIMEOUT_SECONDS`. The harness still requires a real rendering
device, exact PNG/receipt checks and manual visual review; a timeout or a green
headless smoke test cannot be treated as art acceptance.

Consequences: Future captures fail closed and leave bounded logs instead of
running indefinitely. Runtime, scene layout, collision ownership,
`RuntimeBridge` and narrative state are unchanged.

Linked files: `game/tests/Act1FullRouteCoreWorldCapture.cs`,
`eng/capture-act1-full-route-core-world.sh`,
`docs/urman_knowledge_base/weak_points.md`

## 2026-08-24 — Close the declared reverse-arrival ground seam

Status: Implemented geometry-envelope correction; visual review remains OPEN

Context: The master layout identified that reverse-arrival framing reached
`z≈49–53`, while the shared ground ended at `z=37` and the Agent B heightfield
ended at `z=44`. That was a real first-person world boundary, not a material or
fog problem.

Decision: Extend the shared visual/traversal ground to `86 × 208 m` at
`(0, -.12, -48)` and extend the deterministic Agent B heightfield/layout cap to
`MaxZ=56`. The south/forest boundary stays unchanged at `z=-152`; route strips,
interiors, interactions and narrative state are untouched.

Evidence: Constants and box extents are aligned in `Act1ConnectedWorld`,
`AgentBAct1HeightField` and `AgentBAct1Layout`; the existing headless full smoke
remains the required regression. The change removes the known geometric seam,
but it does not close the eight-zone 360° capture or human visual gate.

Linked files: `game/scripts/Act1ConnectedWorld.cs`,
`game/scripts/experiments/agent_b_act1/AgentBAct1HeightField.cs`,
`game/scripts/experiments/agent_b_act1/AgentBAct1Layout.cs`,
`docs/urman_knowledge_base/art/act1_master_layout_2026-08-17.md`

## 2026-08-24 — Establish one production FAP exterior owner

Status: Accepted narrow production composition correction; visual first-person,
medical/local-context, wayfinding, cultural review and art lock remain OPEN

Context: `agentb_village_buildings_kit.glb` contains a complete `Fap_*` family
at the same world-space parcel where the connected world mounts the dedicated
`urman_fap_clinic_kit.glb`. Leaving both visible creates competing facades,
fences and entry cues.

Decision: Keep
`Act1ConnectedWorld/Act1CoreWorldGreybox/FapExterior/FapClinicAuthoredKitPresentation`
as the sole exterior FAP landmark owner. `AgentBAct1ExteriorLayer` suppresses
the Agent B `Fap_*` family before architecture-collision generation and records
the suppression in metadata. The local `chapter1_fap_clinic.tscn` remains the
interior floor, desk, interaction and RuntimeBridge-facing content owner. No
route, narrative, save or interaction owner changes are allowed by this
decision.

Evidence: `Act1DemoLaunchSmokeTest` now requires the dedicated FAP owner,
positive suppression metadata/count and no visible Agent B `Fap_*` meshes.

Follow-up correction: because the suppressed Agent B family also carried the
only visible `ФАП` text, the authored wayfinding-board placement now receives a
single presentation-only `Label3D` with the same acronym. The source board
remains neutral/blank; no quest marker, interaction target, narrative state or
second content owner is introduced. The label's language/local-context review
remains OPEN.

## 2026-08-24 — Hide foliage source library after deterministic extraction

Status: Accepted narrow production presentation correction; visual repetition,
foliage density, cultural review and art lock remain OPEN

Context: `agentb_foliage_kit.glb` is a source library of variant families used
by `AgentBFoliagePlan`. Before this correction its template root stayed visible
after `AgentB_PlantedFoliage` copies were created, producing a template-board
cluster and duplicate source/planted presentation in the connected world.

Decision: Treat `AgentB_FoliageKit` as extraction-only. Hide its root after
template collection, record `templateSourceHidden`/policy metadata and keep
only deterministic copies under `AgentB_PlantedFoliage`. Do not alter terrain,
collision, route, interactions, `RuntimeBridge`, saves or narrative state.

Evidence: `Act1DemoLaunchSmokeTest` now requires the source root to be hidden,
the planted layer to be non-empty, and the metadata entry/node counts to be
consistent with the complete foliage plan. This is structural QA, not visual
art acceptance.

## 2026-08-24 — Add low-contact authored foliage pass

Status: Implemented bounded presentation-density correction; visual review remains OPEN

Context: After hiding the foliage source library, the production route still
had only the verified 46-entry plan, with many entries being trees or distant
silhouettes. Wet shoulders, yard thresholds and the Kara transition could read
as bare even when the larger authored structures were present.

Decision: Extend `AgentBFoliagePlan` with a restrained low-contact layer of
authored `Sedge`, `Fern`, `GrassTuft`, `MossStone`, `FallenBranch` and `Stump`
variants along route edges, Babai/әби and FAP parcels, the zirat transition and
the Kara threshold. Positions remain outside the walkable road envelope; no
new collision, navigation, interaction, narrative or save owner is introduced.

Evidence: the dedicated Act I smoke now processes the expanded plan through
the same source-hidden/planted-count contract. `AgentBAct1ExteriorLayer` now
fails closed when any entry is closer than 0.25 m to the authored road envelope
and records the minimum clearance metadata; the existing plan corrections moved
three legacy entries off the house/FAP branches. Build and full Godot regression
remain required. This improves deterministic contact density but does not close
repetition, near/mid/far, cultural or human visual acceptance.

## 2026-08-24 — Route the single Act I atmosphere owner

Status: Accepted narrow presentation-ownership correction; lighting, rain/fog,
night readability and art lock remain OPEN

Context: The integrated Agent B layer added a persistent `AgentBEnvironment` and
`AgentBSun` after the logical zone scenes had already registered their own
WorldEnvironment/directional-light presentations. Both owners could remain
non-null/visible during a zone transition, violating the one-active-owner
layout contract and risking a cold exterior environment inside house/FAP.

Decision: `Act1ConnectedWorld.SetActiveLogicalZone` routes exactly one global
environment owner. Exterior `village_day`, `zirat_road` and `kara_urman_night`
use Agent B atmosphere/sun and disable local directional suns; interior
`house_old_pc` and `fap_clinic` disable Agent B atmosphere/sun and retain the
local environment; Agent B rain is also disabled in interiors. Local
interior/ambient point lights remain eligible. No
route, narrative, save, collision or interaction ownership changes.

Evidence: `Act1DemoLaunchSmokeTest` checks one active WorldEnvironment in the
exterior state, then switches to house and checks one local environment with
Agent B disabled before restoring village presentation.

## 2026-08-24 — Retire standalone Agent B Act I world variant

Status: Implemented internal code retirement; historical capture evidence remains
non-production

Context: The repository still contained `AgentBAct1World`, its standalone scene,
three probes and two launch/capture scripts after the production connected-world
integration had adopted `AgentBAct1ExteriorLayer`. Those files described a second
runnable exterior owner and the windowed launcher could bypass the user's
no-focus-steal workflow.

Decision: Keep `Act1ConnectedWorld/Act1CoreWorldGreybox/AgentBExteriorWorld` as
the only runnable Agent B exterior owner. Retire the old source, scenes, probes
and launch scripts. Preserve `agent_b_act1_world_report.md` and its receipt as
historical provenance, explicitly marked non-production. No route, collision,
interaction, narrative, save or `RuntimeBridge` ownership changes are part of
this retirement.

Evidence: repository search after deletion has no code or script references to
`AgentBAct1World`; production `Act1DemoLaunchSmokeTest` and the full Godot
regression exercise the remaining owner. The production layer also exposes
`variantStatus=production-canonical`, `retiredVariants=AgentBAct1World` and
the canonical `res://scenes/act1_demo.tscn` entrypoint; the dedicated smoke test
asserts those markers.

## 2026-08-24 — Add bounded Kara night value-separation cues

Status: Implemented narrow presentation correction; human night-art review remains OPEN

Context: The routed exterior atmosphere owner reached the Kara edge, but the
existing cool fog/sun endpoint could still collapse roots and foreground tree
masses into a low-value band. The correction must not introduce a second global
environment or turn a visual cue into a gameplay/light puzzle owner.

Decision: `AgentBAct1ExteriorLayer` now owns exactly three weak, shadowless
`OmniLight3D` cues under `KaraAccentLights`: threshold, root cluster and gesture
branch. Their base energies are deliberately low (`0.72`, `0.58`, `0.44`) and
their cool colors are applied only as the camera transitions into
`kara_urman_night`; they are hidden in village, house and FAP states. The nodes
are marked presentation/visual-only with no collision, navigation, interaction,
or narrative/save ownership. `RuntimeBridge` remains the only narrative-state
owner.

Evidence: `Act1DemoLaunchSmokeTest` moves only its test-owned first-person
player to the existing Kara and village spawns, asserts the cues become visible
with non-zero energy in Kara, then restores the village presentation. Build,
dedicated launch smoke and full Godot headless regression pass. This is not a
claim of final night lighting, authored forest density, or art lock.

## 2026-08-26 — Make Act I physical dialogue gates explicit

Status: Accepted internal route contract; technical evidence passes, while
visual, cultural and release gates remain OPEN

Context: The connected first-person route could traverse its zones, but the
important human beats needed an explicit physical interaction contract. A
player must earn access to the next investigative layer through the NPC or
document in front of them, rather than through a hidden progression shortcut.

Decision: Keep the Act I gate sequence as follows: Mansur grants old-PC access;
the player opens the official Marat death notice; Gulsina/әби delivers the
player-facing `ярамый` warning and unlocks the house exit; Alsu confirms the
conflicting accounts and unlocks the FAP route; Naila grants medical-record
access. The physical first-person ray and existing dialogue/document UIs are
the interaction path. `RuntimeBridge` remains the sole narrative-state owner;
the journal is only a read-only projection of shared runtime state, and its
stale smoke-test oracle is corrected.

Evidence: the exact physical walkthrough passes 135.49 m through the route to
`kara_urman_night` and completes the `НЕ ОТВЕЧАЙ` cliffhanger. Its assertions
check each gate before and after the physical interaction, including the
unavailable-before/available-after transitions; the journal flow now asserts
the current shared projection rather than the stale oracle.

Consequences: Internal smoke evidence now covers route gating, not only scene
transitions. This does not prove first-time wayfinding, visual quality, authored
characters, cultural/Tatar review or release readiness, and the project must
not be called prod-ready, art-locked or release-ready on this evidence alone.

Linked files: `game/tests/Act1FirstPersonWalkthroughSmokeTest.cs`,
`game/tests/JournalFlowSmokeTest.cs`,
`content/modules/urman-chapter1/definitions.json`,
`game/scripts/RuntimeBridge.cs`,
`docs/urman_knowledge_base/playtest_plan.md`

## 2026-09-03 — Fix the three existing art documents as the Act I acceptance authority

Status: Accepted

Context: Tracker task ART-001 requires one shared art acceptance target before
zone-by-zone production. Three authoritative documents already exist and fix
the Painterly Low-Poly full-volume direction, near/mid/far and 360-degree
review rules, cultural constraints and the "ordinary first, wrongness later"
rule: `design_style.md`, `art/act1_master_layout_2026-08-17.md` and
`art/act1_visual_reference_bible_2026-08-17.md`. Creating another style bible
would restart candidate churn.

Decision: Treat exactly these three documents as the acceptance reference for
every Act I art/zone task in
`URMAN_ACT_I_REPO_GROUNDED_PRODUCTION_TRACKER_RU.md`. Zone tasks must cite a
concrete target/gap from them; target PNGs inside these documents are
references, never runtime screenshots. No new style document is created; only
factual contradictions between these documents and the current runtime may be
edited, and each such edit requires a decision log entry.

Consequences: Art acceptance reviews are checked against the fixed documents
instead of per-task taste. The retired full task tracker and superseded route
documents are not art authority.

Linked files: `design_style.md`, `art/act1_master_layout_2026-08-17.md`,
`art/act1_visual_reference_bible_2026-08-17.md`

## 2026-09-03 — Stop painterly texture candidate churn; production set is the runtime-referenced six

Status: Accepted

Context: ART-009 requires one selected production material set instead of
endless candidate churn. The runtime material owner `PainterlyMaterialLibrary`
references exactly six albedo textures: `weathered_wood_boards_albedo`,
`damp_earth_albedo`, `aged_plaster_albedo`, `pine_foliage_albedo` (v1 family)
plus `mossy_stone_v2_albedo` and `old_fabric_v2_albedo`. The v2–v6 candidate
textures are already marked `candidate-provenance; exclude-from-act1-release`
in `assets/asset_registry.json` (BASE-007). STOP-DOING item 4 forbids new
texture variants before geometry/contact work.

Decision: Treat the six runtime-referenced families as the current production
set. No new painterly variants are authored; no candidate is promoted to the
runtime set without a recorded, proven material/scale blocker and a new
decision log entry. Candidate PNGs stay in the repository as provenance and
outside the Act I release package disposition.

Consequences: Candidate churn stops at v6. Final visual confirmation of the
production set (tiling, value separation, motion review at 65/75/90 FOV)
remains the open human art review owned by ART-009/CAPTURE-006; this decision
does not claim art lock.

Linked files: `game/scripts/PainterlyMaterialLibrary.cs`,
`assets/asset_registry.json`

## 2026-09-10 — Акт I переводится на зиму (аутентичный татарский авыл)

Status: Accepted (user decision 2026-09-10)

Context: Пользователь отклонил текущий летний/влажный вид деревни как
неаутентичный: массовые ели и сосны в деревенских зонах читаются как
«пальмы»/парк, а не как татарская деревня. Прежнее направление KB
(«недавно прошедший дождь, пасмурные сине-зелёные сумерки») зафиксировано
в `concept_image_pack/IMAGEGEN_SERIES_BRIEF_RU.md` и
`art/act1_visual_reference_bible_2026-08-17.md` §3.7. Канон
`village_lore.md` разрешает ель только как переход деревня→лес и как
тёмный слой у Кара-Урмана.

Decision:
1. Сезон Акта I — зима (жёстко, без сезонного переключателя). Лето
   остаётся возможной будущей опцией и не реализуется сейчас.
2. В деревенских зонах (Arrival, MainStreet, BabaiEbiYard,
   HouseExteriorApproach, ConnectiveStreetReturn, FapExterior,
   ZiratMemoryField) хвойные деревья не используются. Молодые ели
   допустимы только узкой полосой на переходе деревня→лес; тёмные ели
   со снегом — только у Кара-Урмана.
3. Деревенская растительность — лиственные виды авыла: берёза (каен),
   липа (юкә), клён, рябина (миләш), черёмуха (шомырт), ива (тал);
   зимняя форма — голые ветви со снегом.
4. «Пушистый продавливающийся снег» реализуется presentation-only слоем:
   динамическая маска проминания и следов + звук скрипа + снежные
   султанчики; состояние сессии, не сохраняется, без владения
   коллизией/навигацией/игровым состоянием.
5. Далёкий силуэт минарета добавляется как presentation-only ориентир
   (не входибельный); форма требует человеческого культурного ревью.

Consequences: Прежние «влажные» кадры Акта I становятся историческими;
все зимние кадры — новые evidence. Существующие контракты сохраняются:
один активный WorldEnvironment (ART-010), владелец погоды — Agent B
exterior layer, ходьба/маршрут/сейвы/культурный слой не меняются.
Смена сезона не закрывает human art/cultural gates.

Linked files: `docs/urman_knowledge_base/design_style.md`,
`docs/urman_knowledge_base/art/act1_visual_reference_bible_2026-08-17.md`,
`docs/production/URMAN_WINTER_TEXTURE_BRIEF_RU.md`,
`game/scripts/experiments/agent_b_act1/AgentBAct1ExteriorLayer.cs`,
`game/scripts/PainterlyMaterialLibrary.cs`

## 2026-09-10 — Проверяем зимний визуал на текущем production-рендере

Status: In progress, user-authorized visual plan; not art lock.

Новый baseline: `/Users/unterlantas/Documents/URMAN_visual_20260910/phase00` —
48 одинаковых контрольных камер в 1080p и 720p, 10 кадров реально открытых UI.
Существующий root capture теперь записывает FOV, допускает обе резолюции и
опционально измеряет 60 секунд после 12 секунд прогрева. UI capture проверяет
видимость панели и делает ForceDraw перед чтением; старые снимки закрытых UI
не являются доказательствами интерфейса.

На M4 Pro, medium, FOV 75, scale 0,9, MSAA 2× исходный hidden Metal capture:
avg 74,08 мс / p95 110,17 мс. Время включает ForceDraw; это сравнительный
capture benchmark, не обычный FPS игры в видимом окне. Отдельно зафиксированы
67 648 MeshInstance3D, 71 081 draw calls и 3 263 046 primitives.

Фаза 1 исправляет существующие AddDistantHouseRow/AddDistantForestBand:
ряды больше не пересекают деревню, каждый экземпляр получает свою высоту.
AddDistantRidge строит низкую нерегулярную поверхность; визуальный грунт
вне физического terrain rectangle поддерживает фон. Коллизии не меняются.
Приёмка фаз и остальные риски записываются по фактическим новым кадрам.

### Проверка зимнего снега: фазы 1–2, 2026-09-10

Фаза 1 принята по основному и боковому захвату; физический маршрут 335,27 м
завершён до клиффхэнгера. Фаза 2 использует существующий AddSnowBank /
AddVisualLandformSurface: округлые нерегулярные навалы вместо блоков,
понижение по дорожному контуру у пересечений и входов, целина 0,32–0,38 м
на эталонных 26 м улицы. Материал временно отключён для проверки формы.
Геометрия навалов проверяется существующим capture-путём; отдельного
collision owner не добавлено. Повторный физический маршрут прошёл.

Доказательства: `/Users/unterlantas/Documents/URMAN_visual_20260910/phase02/production1080`
и `phase02/final1080` (однотонный снег). Сравнительный замер обычного
материала: среднее 55,69 мс, p95 100,36 мс, максимум 116,66 мс.
Разброс принудительного захвата остаётся ограничением измерения.
Новые колея, материал снега, следы и формы деревьев этим решением не приняты.

### Связанный материал снега, 2026-09-10

Существующий генератор создаёт одну карту микровысоты, связанную шероховатость
и нормаль из производных высоты. PainterlyMaterialLibrary использует их в своём
единственном шейдере; снег неметаллический, редкое зерно меняет только BRDF,
без emission и временного шума. Микродеталь гасится при субпиксельном размере.
Фаза 4: 30 неподвижных кадров имеют одинаковый снежный участок; поворот
сохранён в `phase04/snow_material_sweep.mp4` внутри внешнего набора
`/Users/unterlantas/Documents/URMAN_visual_20260910`. Среднее время кадра
59,39 мс, p95 100,11 мс (M4 Pro, medium, 1080p, принудительный скрытый draw).
Физический маршрут 335 м прошёл. Следы и старые формы деревьев пока не приняты.

### Следы как локальное представление, 2026-09-10

SnowTrampleField остаётся единственным владельцем: окно 24 м, маска 1024
(512 на low), до 512 недавних отпечатков. Поле хранит высоту впадины, край,
высоту опоры и уплотнение; единый PainterlyMaterialLibrary получает высоту
и нормаль из этого поля. Только затронутые треугольники существующих
Terrain_Main/Road_* с supportOwner=AgentB_TerrainCollision уплотняются
до шага 6 см. Коллайдер, движение и формат сохранений не меняются.
Проверка реального пола, владельца коллизии и версии переносимого положения
отсекает воздух, интерьер, настил, крышу, воду и перенос/загрузку.

Два прохода подтверждены 104-кадровыми видео и одинаковыми камерами
в `/Users/unterlantas/Documents/URMAN_visual_20260910/phase05`: `road1024`,
`yard1024`, `footprints_road_1024.mp4`, `footprints_fresh_1024.mp4`.
Повтор углубляет след до 1 см на дороге и 3,5 см во дворе, край около 1,1 см.
Физический маршрут 335,27 м прошёл. Общий бюджет M4 Pro ещё открыт:
последний p95 скрытого принудительного draw — 97,86 мс; обновление шага
 в среднем 14,21 мс, максимум 21,34 мс. Это не подтверждение целевых 60 FPS.

### Фаза 6: bounded foliage/contact evidence, 2026-09-10

Фаза 6 принята как ограниченный технический и визуальный evidence-pass.
`contact1080` содержит 11 фактических кадров: 8 world и 3 reference forms.
Проверены 111 корней на рендерной apron-триангуляции и collider; в исходном
GLB проверены 36 native `WinterRoot` pivot-ов. Near/Light/Far используют
общие поверхности (2–3 на вариант), ground cover размещён через пространственные группы MultiMesh размером 12 м без тысяч branch Mesh nodes. Используется один существующий asset
kit и shader; gameplay changes отсутствуют.

Среда замера: M4 Pro, medium 1080p, FOV 75, scale 0,9, MSAA 2×.
Core contact benchmark: avg 14,057 мс, p95 15,396 мс, max 29,874 мс,
10 449 draws и 1 853 474 primitives; baseline phase 0 был avg 74,076 мс,
p95 110,173 мс и 71 081 draw. Эти значения включают скрытый `ForceDraw` и
его wall time не являются видимым gameplay FPS.

Post-trail `phase06/trample1024`: avg 12,952 мс, p95 14,032 мс, max 21,09 мс.
В 21 реальном шаге CPU mean 14,154 мс, max 19,657 мс; этот риск остаётся.
Окно следов 24 м и маска 1024 не менялись. Узкий demo и physical walkthrough
335,27 м прошли; `phase06/contact_walkthrough.log` восстановлен, build —
0 errors, diff — PASS. Evidence сохранён в
`/Users/unterlantas/Documents/URMAN_visual_20260910/phase06/contact1080`,
`/Users/unterlantas/Documents/URMAN_visual_20260910/phase06/trample1024/snapshots`
и `/Users/unterlantas/Documents/URMAN_visual_20260910/phase06/footprints_road_after_foliage.mp4`
(104 frames, 3,466667 s).

Это не закрывает общий production plan: финальные фазы 7–10 ещё не выполнены.


### Фаза 7: дворы и горизонт, 2026-09-11

В существующем `Act1ConnectedWorld` исправлены высоты калиток, столбов,
сараев, навесов и каменных групп. Низы стен сараев и отдельные столбы
следуют физическому грунту. У дома бабая и әби добавлены лопата, очищенный
подход и навес над существующей поленницей. Два уличных хозяйства получили
различающиеся по степени утоптанности подходы к дровам; подходы у ФАПа
и последнего западного двора переведены на зимний материал.

В существующем village exterior kit шесть брёвен уложены в устойчивые
ряды 3/2/1 на опорах. Сохранены корень, имена и материалы; экспорт
проверяет геометрию и контакт граней. Четыре декоративных ограждения,
пересекавших дорожную полосу, перенесены или укорочены без правки коллизии.

Дальний посёлок сокращён с 17 до шести разнесённых домов со скатными
снежными крышами. Древесные группы распределены нерегулярно; добавлен
дальний северный лес за грядой. Проверены 136 точек опоры на фактических
треугольниках фона, гряд и физического грунта. Второй восточный силуэт
минарета удалён; единственный западный ориентир сохраняет открытый
вопрос культурной проверки. Новых надписей или символов нет.

Материалы: `/Users/unterlantas/Documents/URMAN_visual_20260910/phase07/final1080`
(26 кадров), кадры до — `phase07/before1080` и `phase06/contact1080`.
Физический проход 335,27 м успешен, пользовательские сохранения восстановлены.
Сборка без ошибок; `git diff --check` успешен. M4 Pro medium 1080p:
среднее 14,008 мс, p95 14,960 мс, максимум 21,042 мс; 10 390 draw calls.
Режим замера прежний: скрытое окно с ForceDraw.

Аудит внешнего периметра не выявил вертикальных невидимых стен: границы
основного грунта объясняет поднятый рельеф. Обнаруженные локальные
невидимые препятствия исправлены без изменения физических тел:
существующий колодец совмещён с `VillageWellCollision`, исправленная
поленница — с `FirewoodCollision`. У Кара-Урмана возвращена видимость семи
мешей с действующими trimesh; их верхняя форма сохранена, нижняя доведена
до грунта, валежнику добавлены опоры. Визуальная опора не меняет коллайдеры.
Повторный физический проход успешен. Последние четыре кадра и замер:
`phase07/snowed_obstacles1080`, среднее 13,946 / p95 14,893 / максимум 26,287 мс,
10 406 draw calls. Фаза 7 закрыта; общий план ещё не завершён.


### Фаза 8: свет без глобального Glow, 2026-09-11

Единственный внешний профиль `TuneConnectedAct1Atmosphere` откалиброван
после геометрии. Glow и adjustments отключены; контрольный захват без них
сохранён в `phase08/neutral1080`. Дневная заливка 0,69, у зирата 0,64,
у Кара-Урмана 0,52; дневное солнце 31°, энергия 1,38. SSAO ограничен
радиусом 0,4 м и интенсивностью 0,75. FogDensity: 0,003 / 0,0038 / 0,0044.
Ночной переход, отдельные интерьерные профили и сценарная маршрутизация сохранены.

В `phase08/final1080` получены 11 кадров с фактическими параметрами
окружения в JSON. На каждом кадре ровно один активный WorldEnvironment;
дневной профиль до и после интерьера совпадает. Физический проход 335,27 м
успешен, сохранения восстановлены, сборка без предупреждений и ошибок,
`git diff --check` успешен. В выбранных нижних центральных участках четырёх
кадров нет белого и чёрного клиппинга; это локальная проверка, не оценка всего изображения.

`/Users/unterlantas/Documents/URMAN_visual_20260910/phase08/snow_light_sweep.mp4`:
90 кадров, 3 с, 1080p. Первые 30 кадров участка снега побайтно одинаковы;
далее медленный поворот на 5,16°. Погода и ветер отключены только для этой
проверки материала. Отдельный замер с обычной погодой: M4 Pro medium 1080p,
среднее 13,931 / p95 14,866 / максимум 19,627 мс, 10 409 draw calls.
Это тот же capture-режим ForceDraw, не замер обычного игрового окна.

### Фаза 9: редкие бытовые события, 2026-09-11 — принята

`Act1ConnectedWorld` размещает и обновляет две дымящие трубы, одного кота
на подходе во дворе, пролёт трёх ворон и одного дальнего жителя у поленницы.
Это `Proposal` фоновых сцен, а не новые сюжетные персонажи. Источник кота
и птицы — дополнительные компоненты существующего village exterior kit;
житель использует нейтральный силуэт `CouncilWitness` существующего
character kit через `GeneratedCharacterKitDressing`.

Одновременно выполняется только одно действие. Начальная пауза 24–38 с,
последующие — 45–120 с; подряд одинаковые события не выбираются. Нет NPC
расписаний, новых маршрутов, взаимодействий, коллизий и записей в сохранениях.
События останавливаются вне `village_day`, при модальном окне, сюжетной
аудиоподсказке и reduced motion. Звук остаётся у `AmbientAudioDirector`;
отдельные звуки животных не добавлены. Дым использует 36 CPU-частиц,
общий ветер и прозрачный материал без emission. Выход трубы проверяется
по реальным треугольникам крыш, а не только по `IsVisibleInTree`.

На проверочных кадрах исправлены пересечение котом забора, потеря исходного
поворота костей рук и летние зелёные посадки на зимних грядках. Грядки
используют `snow_ground`, физическую высоту грунта и редкие сухие остатки
растений. `FirstPersonController.ApplyAccessibilitySettings` теперь сохраняет
переданное значение accessibility до обновления оформления; прямой вызов
этого метода корректно меняет `ReducedMotion`.

Итоговый физический проход 335,27 м успешен, сохранения восстановлены.
`phase09/final_verified1080`: 14 контрольных кадров, три события до/во время,
проверка пауз, контакта кота с грунтом, тихих зон, модальности и reduced motion.
Хеши всех контрольных PNG совпадают с receipt; отдельные диагностические
снимки больше не перезаписывают контрольные виды труб.

В существующем character kit исправлены опора шеи/воротника и посадка лица
по реальной передней поверхности головы. Уши и шея используют материал кожи;
зимний шарф закрывает шею до нижней челюсти. Риги и игровые размеры сохранены.
Ближний кадр `resident_contact_detail.png` повторяет камеру исходного дефекта
`phase09/plumes1080/ChimneySmoke3_detail.png`.

Видео `/Users/unterlantas/Documents/URMAN_visual_20260910/phase09/yard_cat_verified.mp4`:
180 кадров, 30 fps, 6 с, 1080p. M4 Pro medium 1080p ForceDraw:
среднее 12,686 / p95 13,866 / max 22,473 мс, 10 405 draw calls.
Сборка: 0 предупреждений, 0 ошибок; `git diff --check` успешен.
Фаза 10 и общая приёмка остаются открытыми.

## 2026-09-11 — Фаза 10: bounded UI/readability и навигация настроек приняты

Status: Accepted bounded UI evidence; общая финальная приёмка Акта I ещё идёт.

Основание — исходники `game/scenes/ui/old_pc_ui.tscn`,
`game/scenes/ui/settings_ui.tscn`, `game/scripts/OldPcUi.cs`,
`game/scripts/SettingsUi.cs`, `game/scripts/AccessibilityPresentation.cs` и
evidence `/Users/unterlantas/Documents/URMAN_visual_20260910/phase10`.

Получены 34 реальных capture: 34 PNG и 34 JSON в `accepted_ui`, по 17 на
1280×720 и 1920×1080. Матрица покрывает пять критических экранов
(dialogue, document, journal, old PC, settings), baseline/filled/large
состояния, 19 записей журнала, реальные документы old PC и длинный документ
`urman.oldpc:document/tw_tavysh_boundary_stories` на 807 символов. Все
receipt содержат видимую панель, текст, Tatar glyphs
`ӘәӨөҮүҖҗҢңҺһ` и непустой `keyboard_focus`; длинный reader и settings
scroll-контейнеры представлены фактическими UI-узлами.

В old PC принят исправленный фактический reader: `ReaderArea/Reader` получает
`document.BodyMarkdown`, остаётся `scroll_active`, а filled capture показывает
реальный заголовок, тело документа, путь файла и действие «В журнал».
В settings принят `BodyScroll/Body` с обновлёнными node paths, динамическими
строками громкости и 44 px интерактивными строками; источник задаёт Theme с
основным размером 20 px. `navigation.log` подтверждает menu-safe open,
начальный focus, rollback без apply и сохранение после явного apply.

Масштаб 1.6 применяется к метрикам шрифтов (в receipt максимальный размер
48 px), при этом корневой Control принудительно остаётся без transform-scale;
это сохраняет геометрию панели, кнопок и scrollbars в 720p. Новый UI-owner не
введён, маршрут, сохранения, коллизии, навигация и narrative state не менялись.

Фаза 10 закрыта только в смысле этой bounded UI/readability-проверки. Human
first-time usability, языковое/культурное ревью, release-host проверки и
общая финальная приёмка остаются открытыми.


## 2026-09-11 — Итоговая техническая приёмка зимнего визуала Акта I

Выполнены фазы 0–10 плана `URMAN_VISUAL_ART_DIRECTION_PLAN_RU.md` в основном
checkout. Итоговые материалы: `/Users/unterlantas/Documents/URMAN_visual_20260910/final`.
`world1080_verified` и `world720_verified`: по 56 PNG; SHA-256 и размер каждого
файла проверены. Все 48 исходных камер совпадают с baseline; 56 камер между
разрешениями совпадают. Проверены восемь наружных зон и интерьеры дома/ФАПа.
`walkthrough_final.log`: физический маршрут 335,27 м, взаимодействия и
клиффхэнгер PASS. Пользовательские сохранения и настройки восстановлены побайтно.

Финальный осмотр выявил ещё два наследованных дефекта плоских benchmark-сцен:
парящие `puddleGeometry` в наружных зонах и `BoundaryStoneNear` у Кара-Урмана.
В существующем `ApplyLogicalZonePresentationSuppressions` декоративные лужи
убраны из зимнего connected world, камень посажен на физический грунт с
погружением 4 см. Коллизий у этих мешей нет. Исходные isolated studies сохранены.
До исправлений: `final/before_corrections`; после — итоговые наборы мира.

`trample_road` и `trample_fresh`: настоящие проходы по 5,83 м вперёд и назад,
10/21 отпечаток, 1024, два сдвига окна в каждом запуске, глубина 1/3,5 см.
По шесть неподвижных кадров и одному видео 104 кадра / 30 FPS / 3,466667 с.
`video_decode.json` подтверждает декодирование обоих видео следов, ролика кота
и проверки снега при медленном повороте. Исходные промежуточные последовательности
сокращены до четырёх ключевых кадров после успешного декодирования.

M4 Pro, medium, 1920×1080, FOV 75, scale 0,9, MSAA 2×; 12 с прогрева + 60 с
замера тем же существующим ForceDraw-путём: среднее 74,077 → 12,448 мс,
p95 110,173 → 13,592 мс, max 198,959 → 21,418 мс. Draw calls 71 081 → 10 391;
отрисованные примитивы 3 263 046 → 1 864 576. Это показатели скрытого Metal
capture, не обещание FPS обычного игрового окна. После прохода по следам:
среднее 12,885 / p95 14,191 / max 21,551 мс.

CPU штампа: накат mean 14,168 / max 19,397 мс; целина 11,399 / 15,894 мс.
Максимум двух сдвигов маски: 13,458 / 10,894 мс. При естественном запуске
события жителя CPU owner — 0,218 мс; первый видимый reference-кадр — 23,092 мс
(включает смену вида и ожидание рендера, исключает PNG). Три события, паузы,
quiet zones, modal и reduced motion: `life_verified.log` PASS.

Сборка: 0 ошибок / 0 предупреждений. Дорожные проверки: 75 600 точек,
расхождение 0,0036–0,0487 м; 1 432 110 низких вершин растительности вне
очищенной полосы; 136 фоновых опор. UI: 34 реальных кадра и navigation PASS,
описаны в записи фазы 10. Единственный WorldEnvironment и presentation-only
контракты проверены существующими smoke/capture-владельцами.

Технический объём этого зимнего плана принят локально. Это не культурный
sign-off, пользовательский first-time playtest и не проверка M1/Windows.
Остаются измеренные редкие CPU-пики штампов и отдельная художественная оценка
финальных форм; они не скрываются средним FPS. Разрешение маски не повышалось.

## 2026-09-11 — Хэндовер законченного Акта I вместо следующего изолированного polish-прохода

Status: Accepted scope by user; product design directions proposed for execution.
В этом запуске подготовлены документы, игровой код и ассеты не изменялись.

Автор после зимнего прохода запросил масштабный документ и промпт для другого
ИИ: закончить всю главу, включая интересную gameplay-петлю, сценарий,
интерьеры, людей, живые экстерьеры, звук, UI/меню и самостоятельную поставку.
Разрешена самостоятельная редактура и улучшение реализации. Акты II–V
остаются за пределами этого поручения; качество «AAA-like» определено через
постановку и согласованность конечного небольшого объёма.

Новый продуктовый brief —
`docs/production/URMAN_ACT_I_FINISHED_PRODUCT_HANDOVER_RU.md`, запуск —
`docs/production/URMAN_ACT_I_FINISHED_PRODUCT_PROMPT_RU.md`. Они заменяют
прежние частичные handover как текущее поручение, не создавая второй tracker,
style bible или runtime owner. Существующие art authority и канон сохраняются.
Прежнее ограничение зимней задачи «не менять gameplay/маршрут» не запрещает
осмысленное улучшение главы; сохранения, причинность и коллизии обязательны.

Направление: минимум три различных цикла осмысленной проверки и применения
знания; сопоставление в существующем journal/dialogue пути; явная граница
свидетельства, гипотезы и подтверждения. Общая художественная идея — тёплая
личная память и холодный деревенский учёт. Новые авторские решения ещё не
реализованы и не подтверждены плейтестом.

Read-only проверка выявила существенные исходные ограничения: первые
диалоги сохранялись без выбора ради старого walkthrough; длительность
34–38 минут была расчётом текста/пути; финальные Marat/Rinat cues не имеют
физических записей; AudioCueUi использует двухсекундный timer и обход очереди
без captions; шаги ещё используют летнюю zone mapping и отключаются при
ReducedMotion; release-export производит PCK, а не standalone app/exe.
Это задачи следующего исполнения, не результаты исправлений.

Технический зимний baseline сохранён в
`/Users/unterlantas/Documents/URMAN_visual_20260910`. Его hidden Metal frame
time не объявляется обычным packaged FPS. Полная приёмка требует реального
опыта, свежих кадров/аудиовидео, узких проверок, performance и запуска пакета;
внешние культурные, правовые и аппаратные подтверждения нельзя выдумывать.

## 2026-09-11 — Доступные ранние альтернативы в доме

В стартовых узлах `dialogue/gulsina_yaramyy` и `dialogue/mansur_pc_request`
добавлены реальные альтернативы без новых обязательных флагов или квестов.
«Ярамый» теперь доступен после первичного `guessed`, а вопрос Мансура
проверяет его фактическое состояние `pc_access_granted`. Ветка чая Гөлсинә и
ветка помощи Мансура меняют только ближайшую реплику и возвращаются в обычный
flow; доказательные вопросы сохраняют прежние условия. Intro Акта I приведён
к зимнему сеттингу: «Снег» вместо «Дождь».

## 2026-09-11 — Source-derived winter footsteps

Status: Accepted bounded technical integration; human listening, mix and
cultural review remain open.

Context: The existing footstep owner loaded five procedural families, selected
summer-like surfaces by zone, measured cadence from full velocity and muted
steps when reduced motion was enabled. The finished Act I winter route needs
the same presentation-only owner to agree with the real XZ walk and the
already authoritative snow-road mapping.

Decision: Keep `FootstepAudioController` as the only step owner. Load three
CC0-derived variants for each of `snow_packed`, `snow_soft`, `wood` and
`interior_floor`. Resolve outdoor packed/soft snow with
`AgentBAct1HeightField.RoadInfo` using the same `Distance < HalfWidth` rule as
`SnowTrampleField`; map `house_old_pc` to wood and `fap_clinic` to the interior
floor. Accumulate actual XZ displacement at `0.55 m`, preserve the fractional
remainder, and reset on `PresentationTransformRevision`, direct teleport,
modal state or loss of the floor. Reduced motion continues to suppress
nonessential visual motion but does not mute footsteps. The previous
wet-road/mud/grass files remain as inactive repository history and are not
deleted in this slice.

The selected WAVs are copied from the prepared CC0 conversions. Their source
files and license texts are retained at
`assets/source/audio/act1/footsteps/`; per-file source/output hashes and the
conversion record are in
`game/assets/audio/act1/footsteps/manifest.json`. The manifest deliberately
records listening review as open and does not claim an audio listening pass.

Linked files: `game/scripts/FootstepAudioController.cs`,
`tools/audio/generate_act1_footsteps.py`,
`game/assets/audio/act1/footsteps/manifest.json`,
`game/tests/Act1FootstepSmokeTest.cs`,
`assets/source/audio/act1/footsteps/`,
`docs/urman_knowledge_base/audio/act1_sound_map.md`.

## 2026-09-11 — Три ручных вывода в журнале Акта I

Accepted in the current finished-Act-I mandate. Вместо автоматических выводов при переходах игрок сопоставляет две находки и выбирает гипотезу. Существующие interaction/conditions/effects, журнал и RuntimeKernel сохраняют владение данными; `journalAction` — только ограниченная авторская метаинформация пары и ответа. Ключевые документы записываются при открытии, а не входе в сцену.

Старый ID `clue_marat_last_route_near_zirat` сохранён для внутренних ссылок, но видимый вывод уточнён: установлено место со схемы, а не доказан след Марата. Новая сырая улика `clue_zirat_roadside_marks` описывает бирку с двумя засечками у внешней тропы; отметки добавлены в существующее оформление бирки. Статья и черновик больше не выдают точную инструкцию финала.

Anti-Entropy: internal code/content responsibility retirement; obsolete authority — scene entry/document refs confirming the three deductions. Carrier retains observation/progression; canonical owner — explicit journal action through existing kernel. External boundary: none; source-of-truth deletion: none; no approval required within the authorized task. Main-path check: journal choices + chapter route. Negative check: missing source/wrong hypothesis cannot confirm. Boundary check: same source pair is checked in the kernel; save remains SaveGameV3. Final rule timing at Rinat's actual cue start remains a separate pending sound/staging change.

## 2026-09-11 — Audio cue lifecycle and transient voice duck

Status: Accepted bounded technical integration; physical voice recordings,
final mix and human listening remain open.

Context: `AudioCueUi` previously bypassed its queue when captions were disabled,
used a fixed two-second timer, and had no lifecycle contract for pause, load,
restart or return to menu. A physical voice stream could therefore overlap or
be cut by a later request, while a stale cue could survive a session change.

Decision: Keep `AudioCueUi` as the single voice presentation owner. Enqueue every
request, use the physical stream length when available, retain a readable
text-only fallback, and advance silent logical refs immediately. Pause freezes
the player and presentation clock. `RuntimeBridge` resets presentation only at
new-session and successful-load boundaries; `PauseMenuUi` pauses/resumes and
clears on return to the main menu. `Main.SwitchZone` remains outside this
contract because route audio is queued before the physical transition. Physical
voice uses a transient ambience-bus duck owned by `AmbientAudioDirector`; user
settings remain persistent and authoritative.

Linked files: `game/scripts/AudioCueUi.cs`, `game/scripts/RuntimeBridge.cs`,
`game/scripts/PauseMenuUi.cs`, `game/scripts/AmbientAudioDirector.cs`,
`game/tests/Act1AudioSettingsSmokeTest.cs`,
`game/tests/Act1PauseMenuSmokeTest.cs`,
`docs/urman_knowledge_base/audio/act1_sound_map.md`.

Physical Marat/Rinat recordings are still absent; no placeholder voice asset or
listening-pass claim is made.

## 2026-09-11 — Правило леса подтверждается при вмешательстве Рината

Accepted. Вход в `forest` запрашивает две реплики, но не подтверждает правило и не завершает акт. Фактический старт реплики Рината сообщает существующему RuntimeBridge о моменте вмешательства; bridge применяет авторское `forest-rinat-intervention` через обычные conditions/effects и сохраняет checkpoint. UI не хранит сюжетный результат. Это общий путь для Act I и fullgame adapter.

Загрузка незавершённого лесного эпизода очищает старую очередь и восстанавливает только audio.request из авторской сцены; state effects повторно не применяются. Завершённое сохранение не повторяет голоса. Protected Act1FinalState smoke подтверждает скрытое правило до вмешательства, загрузку внутри эпизода, повтор после pre-forest restore и отсутствие повторной очереди после завершения. Физические голоса и постановка персонажа пока открыты.

## 2026-09-11 — Профиль независим от сюжета; меню завершает цикл Акта I

Accepted. Продолжение использует последнее исправное quick/checkpoint, а не
постоянный приоритет quick. Настройки текущего профиля сохраняются при загрузке
старого сюжета. Это намеренное изменение прежнего контракта восстановления
settings из SaveGameV3; формат сохранения не меняется. Новая игра предупреждает
об автосохранении, титры содержат проверяемые источники CC0 и лицензии движка,
финал возвращает в главное меню. Надпись DedOS заменена нейтральным названием
локального архива; отдельная ОС не вводится в канон.

Read-only Luna review выявил протекание checkpoint guard между сеансами;
сброс перенесён в общий ReplaceKernel, а старое async-завершение проверяет
идентичность kernel. Existing checkpoint smoke подтверждает повторную запись
той же сцены после восстановления раннего слота. Чтение save приостанавливает
очередь реплик и не допускает параллельной записи старого сеанса.

## 2026-09-11 — Реестр зимних исходников приведён к фактическому экспорту

Accepted. Три устаревшие записи реестра приведены к source/GLB из `1f42f95`,
без повторной генерации. Добавлены 12 CC0 шагов с полным source/output SHA.
Release validator требует активные зимние семьи; проверка реестра PASS 67/67.
Это закрывает целостность происхождения, а не финальную визуальную/звуковую приёмку.

## 2026-09-11 — BOTW/TOTK как обязательный художественный ориентир

Accepted по прямому уточнению пользователя. Текущий Act I должен быть красивым
в обычном игровом ракурсе: композиция, силуэты, живописные поверхности, цветовые
массы и выразительное освещение оцениваются по BOTW/TOTK. Сохраняются зимний
Кырлай, существующий сюжет и татарская культура. Ранее датированные заметки
о «вторичном ориентире» описывают старые концепт-пакеты, а не снижают текущую
планку приёмки.

## 2026-09-11 — Общий anchor персонажа должен переживать поворот

Accepted. Исправлен runtime rebasing общего GLB character kit до применения
yaw: root children смещаются к локальному foot anchor. Это устраняет
отрыв модели от сюжетного target без ручных компенсаций каждого NPC;
исходный GLB, rigs и содержимое актов II–V не меняются.

## 2026-09-11 — Исследование открытого мира обязательно для завершения

Accepted по прямому запросу пользователя. Цель включает множество секретов,
тайные/обходные проходы, осмысленное исследование мест вдоль и поперёк и
уместные взаимодействия с окружением, возможно разрушение. Старый тезис
«это не open world» и исключение находок без сюжетной улики больше не
ограничивают Act I. Существующий связный Кырлай получает дополнительную
плотность и нелинейность. Для текущего производства принят нижний ориентир
24 находки, 3 проходимые петли и 1 локальное раскрывающее доступ действие;
интерес и красота отдельно подтверждаются игровыми кадрами/прохождением.
Массовое добавление одинаковых collectibles не закрывает требование.

## 2026-09-11 — physical discoveries and Act I people
Accepted within the user's autonomous finished-Act-I scope: authored
`worldLocations` extends existing scene interactions for persistent optional
exploration, without a second state registry or source-scene exit effects.
Selected journal presentation reuses `journal.record` and JournalUi. The
archive uses this same location contract. Optional finds remain production
work until physical paths/reveals and ordinary traversal are verified.

Accepted staging repair: show the existing Alsu host outside the suppressed
legacy street, place Timur's conversation on the street, and project the
existing Rinat alert into his earlier/later physical position. Timur's three
responses concern evidence, silence and family; no new forest rule or cultural
claim is introduced. The closing card stops the continuous ambience bed.

### 2026-09-11 — физический результат домашних находок

Принято: необязательная находка записывает существующие `knowledge` / `journal` и принудительный checkpoint через `worldLocations`; презентация предмета восстанавливается из этих же данных. Первый набор — фото, коробка, отметки роста, лампа. «Өй» подтверждается явной двуязычной карточкой, не догадкой. Новые бытовые воспоминания не меняют пакт и судьбу Марата. Семейное фото — ImageGen artwork с сохранённым исходным промптом и provenance; не сторонняя фотография. Арт-приёмка мира и оставшиеся наружные находки открыты.

### 2026-09-11 — необязательные наружные действия

Принято: наружные наблюдения не перемещают сюжетную сцену и не открывают обязательные clue автоматически. Их физический результат проецируется из общей записи knowledge, включая загрузку и новую игру. Коллизии новых неподвижных предметов и ограды отключаются во внутренних зонах. Служебная калитка ФАПа открывается из двора; восточный обход остаётся доступным, чтобы игрок не оказался заперт. Новые детские воспоминания остаются локальными бытовыми Proposal для narrative/cultural review, без изменения основного канона.


### 2026-09-11 — бытовые действия между сюжетными узлами

Принято: полка для сумки, салфетка под кружкой, детский рисунок за ставней и починка рукояти дают маленький физический результат без новой системы inventory/crafting. Створка открывается полностью; чашка после поднятия возвращается на поверхность рядом. Краткие воспоминания остаются локальным narrative Proposal, без утверждений о судьбе Марата или фольклорных существах. Непроверенное обещание вида на минарет у скамейки убрано из записи.


### 2026-09-11 — место финала и дверь дома

Принято в рамках самостоятельного завершения Act I: forest-approach отделяет
пешее исследование лесной кромки от постановочного forest.onEnter. Владельцы
очереди звука и незавершённого replay не меняются. Ринат заранее ожидает
у конца дороги. Вход и выход дома используют реальный портал существующего
фасада; прежний выход сразу на главную улицу больше не пропускает двор.
Это изменение маршрута и постановки, не новый факт лора.

### 2026-09-11 — отделить выбор находки от препятствия

Принято в текущей самостоятельной реализации. Широкий interaction proxy
верстака блокировал реальный обход сарая. Все необязательные находки теперь
используют ray-only слой; постоянная физика предмета/створки принадлежит
существующим наружным collision owners. InteractionBinding восстанавливает
исходный слой из InteractionTarget, в том числе после недоступного состояния.
Новые четыре находки расширяют исследуемую улицу, двор, канаву и задний
угол дома; двор за сараем — боковой карман с обратным выходом, не петля.
Количество находок и проверка физики не означают завершения art/gameplay goal.

### 2026-09-11 — исходники лиц и волос персонажей

Принято: головы, глаза, брови и причёски используют производные Quaternius Universal Base Characters Standard (CC0). Исходные тела и скелеты не импортируются в игровой набор. Одежда, роли, девять семикостных скелетов и Idle/Tension остаются собственными; канон персонажей не меняется. Упакованный локальный Blender-шаблон позволяет пересобирать набор без сети. Карты лица сохраняются в Godot с нативным toon diffuse; это улучшение основы, а не закрытие художественной и культурной приёмки.

### 2026-09-12 — внешний исходник зимнего дерева

Принято в рамках авторизованной художественной доводки: одна форма сухого дерева использует Quaternius Stylized Nature MegaKit Standard (CC0) с локальными исходниками и воспроизводимыми LOD. Это замена формы существующего объекта, не расширение карты и не новый факт лора. Общие материалы, проверки расположения и физика остаются у текущего владельца окружения.


### 2026-09-12 — сохранение хвои при упрощении сосен

Принято для локального художественного прохода: использовать Pine_3 из того же CC0 Standard MegaKit в трёх существующих местах Кара-Урмана. В LOD упрощается только кора; вырез хвои и UV сохраняются. Для зимней палитры расширен действующий PainterlyMaterialLibrary alpha-scissor вариантом, зарегистрированным в общем кеше графических настроек и ветра. Это не новый художественный стандарт и не подтверждение законченности леса. Источники и проверенные native кадры указаны в assets.md.

### 2026-09-12 — награда за указатель и граница сохранений

Исправлена преждевременная выдача перевода «юл» при приезде. Источником слова остаётся обратная сторона указателя; дополнительный вопрос Алсу требует подтверждённого слова. Подтверждённое социальное наблюдение `clue_marat_versions_conflict` в реплике Алсу не меняется: это сообщение о расходящихся версиях жителей, а не ручная архивная дедукция `contradiction_marat_official_vs_internal` и не знание о пакте.

Семантическая правка данных создаёт явную границу R5 → R6: chapter fingerprint меняется с `589643db90f5b18a8a8f8d986511dca9405c5f8b962f25f14fcb7a4619f9067c` на `774cf31e2d254b94e9319dfbf75fe4f97db980d52feac33d23334dceef08bbf1`. Миграция старых сохранений не реализована. Новое меню объясняет, что неподходящие файлы оставлены без изменений, и подтверждает начало новой игры даже при наличии только неподходящих файлов. Следующее автосохранение или ручное сохранение может перезаписать соответствующий слот; бессрочное хранение старого прохождения не обещается.


### 2026-09-12 — актуализация режима текущего запуска

В AGENTS.md снято противоречие с текущим прямым поручением: запрет Luna от 2026-09-05 заменён режимом native GPT-5.6 Luna max для субагентов текущего запуска. Доступный priority tier не назван отдельным переключателем Fast. Основной агент вправе реализовывать и проверять работу; старый режим отдельных пользовательских задач не возвращается. Повторно закреплены обязательные критерии красоты и интереса исследования из уточнений 2026-09-11. Это синхронизация инструкции, не изменение канона или признание приёмки завершённой.


### 2026-09-12 — слова финала и граница R7 → R8

Обычные captions двух финальных голосов показывают уже авторские произнесённые слова, а не только описание события. Новый сюжетный текст или правило пакта не вводится; подробные аудиоописания сохранены. При записи native-ролика обнаружено, что временная проверка ошибочно ждала transcript при выключенных аудиоописаниях; ошибка самой проверки отделена от полезной редакторской правки captions.

Правка authored content намеренно меняет chapter fingerprint с `774cf31e2d254b94e9319dfbf75fe4f97db980d52feac33d23334dceef08bbf1` на `769d61b3407319fbb28e5024da79105d13bfca299eefca05614ed28e930434f6`, fullgame — с `6a300fb9f6cd425f8279843cb408721480034e71af99744906bd05ae2761e403` на `c230d5d896ce64ea003faf7c465c698b2e443098d37bd18931ef48f14178c50d`. Сохранения R6/R7 отклоняются существующей проверкой совместимости; скрытой миграции или сброса нет. Файлы сохраняются до явного начала новой игры и последующей записи соответствующего слота.


### 2026-09-12 — присутствие Рината и граница R8 → R9

Разговор о внутреннем реестре поставлен лицом к лицу в доме. Положение единственного NPC проецируется из существующих `naila.record_access_granted`, `rinat.alerted` и подтверждения маршрута к Кара-Урману; новых сюжетных флагов и второго персонажа нет. Уход к лесу привязан к сцене `zirat-road`, поэтому начало домашней реплики не телепортирует говорящего из комнаты. Это уточнение постановки, а не изменение роли Рината или раскрытие пакта.

Условия раннего уличного разговора намеренно меняют chapter fingerprint с `769d61b3407319fbb28e5024da79105d13bfca299eefca05614ed28e930434f6` на `fba703a1ee8b9df898454c61c21b36471779c66835ff0dcab248d6b76ae31018`; fullgame — с `c230d5d896ce64ea003faf7c465c698b2e443098d37bd18931ef48f14178c50d` на `8c90245abbd4f027768ab997ef308ba74162c9fb506cc1a6ccecc231b1628fdc`. Миграции нет. Несовместимые файлы не сбрасываются скрыто; явная новая игра и последующая запись слота сохраняют уже документированное поведение.

### 2026-09-12 — домашняя речь и открытая находка на санках, R10 → R11

Три необязательных ответа бабая и әби заменяют отвлечённые афоризмы заботой,
памятью и уклонением от болезненной темы. У санок запись о синей ремонтной
перекладине сохраняется, но журнал больше не закрывает результат снятия
снежного чехла. Удалён только `targetJournalEntryId` этого interaction;
`journal.record`, knowledge и одноразовость сохранены.

Authored content меняет chapter fingerprint с
`fba703a1ee8b9df898454c61c21b36471779c66835ff0dcab248d6b76ae31018` на
`4d6cbd5646e7991be873f8fbdcf572836ceb24449204791e5e1f0b2742dc6805`, fullgame с
`8c90245abbd4f027768ab997ef308ba74162c9fb506cc1a6ccecc231b1628fdc` на
`8b04fa82dcd9b523fa949dd2f5a2ba09a793dbc32d15bc00fc2c5db672c8d74f`.
Миграции нет: существующий отказ совместимости и явное подтверждение новой
игры сохраняются. R10-пакет и его доказательства остаются привязаны к d1d20d4.


### 2026-09-12 — санки возвращаются в разговор, R12 → R13

После подтверждения `discovery-babai-yard-sled-repair` в существующем разговоре
Мансура о ПК появляется вопрос о перепачканных рукавицах. Короткий ответ
развивает уже записанную память о синей перекладине; новых сведений о Марате
или пакте нет. Ветка необязательна, не даёт сюжетных ключей и не меняет маршрут.

Изменение authored content меняет chapter fingerprint с
`4d6cbd5646e7991be873f8fbdcf572836ceb24449204791e5e1f0b2742dc6805` на
`4d8029f7fed5fd8875d95238621c05921bfcc6f743c5386a37eee1189ee5c5ee`, fullgame с
`8b04fa82dcd9b523fa949dd2f5a2ba09a793dbc32d15bc00fc2c5db672c8d74f` на
`9d4dc1ce75847120fb47a503850a8bbcbdf0a4d60694e80a7adba3a258cde9bd`. Сохранения R11/R12 несовместимы с R13.
Скрытой миграции или сброса нет; существующее явное подтверждение новой игры
и сохранение файлов до последующей записи слота остаются в силе.


### 2026-09-12 — вопрос Мансура после сопоставления, R19 → R20

Подтверждение «җавап» даёт необязательный вопрос в существующем домашнем
разговоре. `talk-mansur` использует существующую привязку `worldLocations`,
чтобы оставаться доступным после входа в архивные сцены. Новых флагов,
обязательного возвращения домой и раннего раскрытия правила нет.

Authored content меняет chapter fingerprint с
`4d8029f7fed5fd8875d95238621c05921bfcc6f743c5386a37eee1189ee5c5ee` на
`5ad0b581e82306a85921f7070d73bd2b40176b2097def049b4ebd65c50d37ffa`, fullgame с
`9d4dc1ce75847120fb47a503850a8bbcbdf0a4d60694e80a7adba3a258cde9bd` на
`2b329aab4ef99cfe448931f8bd7f3c32cf0d56ed84a61f41711461e7c5d71384`.
Сохранения R13–R19 несовместимы с R20. Скрытой миграции и сброса нет:
существующая проверка совместимости и явное подтверждение новой игры
сохраняются; файлы остаются до последующей записи соответствующего слота.


### 2026-09-12 — доступ Наили после конкретного вопроса, R20 → R21

Исправлен монолог при первой встрече: два вопроса открывают разные ответы
по существующей справке и дают прежний доступ к фрагменту о Марате.
Приветствие доступа не даёт. Сравнение документов остаётся действием игрока;
автоматическое подтверждение противоречия в разговор не переносится.
Наиля доступна в физическом ФАПе после смены логической сцены.

Изменение authored content меняет chapter fingerprint с
`5ad0b581e82306a85921f7070d73bd2b40176b2097def049b4ebd65c50d37ffa` на
`9a0c172d5324db110ba48560353c78d529ee8cdc1065021208745338ef854b8b`, fullgame с
`2b329aab4ef99cfe448931f8bd7f3c32cf0d56ed84a61f41711461e7c5d71384` на
`52056c1904a0cd81dd82def1a0716b4f54b834b2cf0a1d1cc98c43617bd6637a`.
R20 и более ранние сохранения несовместимы с R21. Действует прежний безопасный
отказ; скрытой миграции и сброса нет. Новая игра требует явного подтверждения,
последующая запись соответствующего слота заменяет его прежнее содержимое.


### 2026-09-13 — условный вход в разговор, R25 → R26

Для повторного Мансура добавлены optional ordered entryRoutes в существующий
контентный диалог. Причина: UI всегда начинал с первой просьбы; проверка
pc_access_granted внутри choices была бы слишком поздней, поскольку первый
узел уже выставляет этот флаг. Выбор entry выполняется до его effects;
остальные диалоги продолжают использовать startNodeId. Нейтральная замена
первой реплики отклонена, чтобы сохранить первую просьбу о помощи.
Нового persisted visited-state и hardcoded Mansur-ветки в UI нет.

Новый chapter fingerprint: `c6440c3167ee8c8cdf824921e98a36f268ffc3312cfdeec16a5a525a6e487c70`;
fullgame: `3dd6b49bc511c60e2130ab03859f836613561d5ee33fc9a59791f0d8ce1b5ab2`. R25 и более ранние сохранения несовместимы с R26.
Скрытой миграции/сброса нет: действует прежний явный отказ и подтверждение
новой игры, запись соответствующего слота впоследствии заменит его файл.
