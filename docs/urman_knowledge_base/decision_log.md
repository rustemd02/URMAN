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
