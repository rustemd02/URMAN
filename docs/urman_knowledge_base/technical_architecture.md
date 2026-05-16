# Technical Architecture

Архитектура должна оставаться engine-neutral, пока движок не выбран. Ни Unity, ни Godot, ни Unreal не фиксируются как решение. Главное требование: narrative content должен быть data-driven.

## Content Layout Proposal

```text
/data
  /characters
  /locations
  /routes
  /dialogues
  /clues
  /documents
  /vocabulary
  /timeline
  /quests
  /lore
  /assets
```

## Data-driven Quests

Квесты должны описывать не «иди в точку X», а условия знания:

- required clues;
- required vocabulary;
- required location access;
- NPC state;
- time phase;
- unlocked topics;
- consequences.

MVP quest example: `quest_marat_first_contradiction`.

## Dialogue System

Диалоги должны поддерживать:

- topics;
- NPC states;
- required knowledge keys;
- escalating reaction levels;
- effects: unlock clue, unlock topic, update suspicion, notify council;
- language fragments with partial translation.

Ответ NPC не должен быть единственным линейным узлом. Один topic может иметь несколько ответов в зависимости от того, что игрок уже знает.

## Knowledge Key System

Knowledge key — общий формат для фактов, слов, документов, фото, предметов и противоречий.

Поля:

- `id`
- `title`
- `type`
- `source`
- `tags`
- `reveals`
- `dangerLevel`
- `usableInDialogue`
- `relatedCharacters`
- `relatedLocations`

## Inventory / Evidence System

Inventory в УРМАНЕ — не только предметы. Это evidence inventory:

- physical items;
- documents;
- photos;
- names;
- dates;
- татарские words;
- contradictions;
- testimony.

Система должна позволять применить evidence в диалоге, архивном поиске и journal graph.

## Document Viewer

Document viewer должен поддерживать:

- body text;
- metadata: year, source, author, location;
- tags;
- highlighted terms;
- unknown татарские words;
- re-read after vocabulary unlock;
- links to clue graph.

Документы не должны быть статичными страницами лора. Они должны открывать действия.

## Old PC Interface

Accepted MVP direction: старый ПК бабая — основной document hub / документальный хаб расследования и отдельная оболочка поверх тех же data-driven сущностей, что journal, documents, clues and vocabulary.

Core surfaces:

- folders;
- archive search;
- «Татарвики»;
- Marat documents;
- local documents;
- saved messages;
- possibly corrupted files;
- file metadata clues;
- search by keywords;
- vocabulary-sensitive re-read.

Data requirements:

- PC documents should use the shared document model, not a separate lore-only format.
- Search index should connect documents, vocabulary, clues, dates, characters and locations.
- PC entries should be able to unlock `KnowledgeKey` ids and update the journal graph.
- Татарские terms discovered elsewhere should be usable as PC search terms.
- Some files can be gated by story flags, passwords, damaged filenames or recovered metadata, but MVP should avoid complex freeform hacking.

Он должен быть дешёвым production hub: много сюжета через UI вместо дорогих катсцен. Full programmability / Turing-complete behavior is explicitly post-MVP.

## Messenger Interface

«Ялкын»:

- direct messages;
- group chats;
- timestamps;
- search;
- message clues;
- partially understood татарские fragments;
- attachments: photo, audio, document.

Для MVP можно реализовать read-only версию.

## Татарский Language-learning System

Система должна поддерживать:

- vocabulary entries;
- known / guessed / confirmed states;
- contextual hints;
- unlock effects;
- partial translation states;
- old document re-render after unlock;
- words as dialogue keys.

Не делать language system отдельным «учебником». Она должна быть связана с evidence.

## Character State

Для NPC:

- trust;
- fear;
- suspicion;
- knows player has clue X;
- available topics;
- last reaction;
- council notified flag.

Состояние нужно для того, чтобы сильные ключи меняли не только одну реплику, но и поведение деревни.

## Village State

Village state:

- day phase;
- investigation pressure;
- council alert level;
- forest presence;
- safe zone availability;
- route restrictions;
- village rumour state.

Это позволит делать ощущение, что Кырлай реагирует на расследование.

For MVP, village state should stay lightweight:

- `pressureLevel`: 0–3;
- concrete flags such as `rinat_alerted`, `council_notified`, `night_route_blocked`;
- ambience state ids for location changes;
- dialogue state effects tied to dangerous knowledge keys.

Do not build a full village simulation for MVP. Pressure is scripted dramaturgy around the detective loop.

## Route Navigation

Accepted MVP direction: in-world discrete route navigation, not a full top-down map.

Routes should be data-driven and engine-neutral. A route graph should describe what the player can see and do from a fixed segment, without requiring free movement, pathfinding or a 3D camera.

Suggested route node fields:

- `id`
- `locationId`
- `timePhase`
- `pressureVariant`
- `facing`
- `backgroundAssetId`
- `foregroundAssetIds`
- `availableActions`: forward, turnLeft, turnRight, back, inspect
- `exits`: target route node ids plus optional requirements
- `diegeticSigns`: label, language, target, visibility rules
- `landmarks`: mosque, house, Niva, forest edge, bridge, zirat, notice board
- `ambienceId`
- `journalSketchUpdate`

Movement transitions should be reusable assets / configs:

- `turn_90_default`: 250–450 ms, foreground parallax, ink-smear edge darkening, sound cue;
- `step_forward_default`: 500–800 ms, footsteps, slight bob, foreground occlusion;
- special transitions only for major reveals such as first zirat approach or Кара-Урман boundary.

The journal map is a derived support view from route discovery and clue state. It must not become the primary overworld for MVP.

## Save / Load

Save state должен хранить:

- collected clues;
- vocabulary state;
- quest state;
- document read state;
- NPC states;
- village state;
- journal graph;
- current time phase;
- location access.

## Localization

Даже если основной язык проекта — русский, система должна нормально держать:

- русский текст;
- татарские слова;
- переводы;
- partial translation;
- glyph support: ә, ө, ү, җ, ң, һ.

Татарский должен быть не вторичным «локализационным языком», а gameplay data.

## Asset Registry

Реестр ассетов должен связывать:

- asset id;
- type;
- priority;
- used by quest / document / location / character;
- MVP status;
- placeholder status;
- source / license.

Это нужно, чтобы контролировать главный риск MVP — расползание ассетов.

## Content Pipeline

Proposal:

1. Писать narrative data в Markdown / JSON.
2. Валидировать IDs и ссылки.
3. Генерировать index для поиска.
4. Проверять, что все clues имеют source и use.
5. Проверять, что все татарские слова имеют translation, context и consultant status.

## Narrative Flags

Флаги должны быть именованы по смыслу:

- `marat_grave_date_found`
- `rinat_notified_council`
- `tt_urman_confirmed`
- `old_pc_archive_unlocked`
- `first_mystical_trace_seen`

Избегать флагов вида `quest1_done`, потому что они быстро ломают логику.

## Clue Graph

Clue graph — центральная структура расследования:

- nodes: character, clue, document, location, vocabulary, event;
- edges: reveals, contradicts, confirms, mentioned_by, found_at, unlocks;
- tags: MVP, language, danger, Marat, pact.

Graph нужен и для журнала игрока, и для внутренней проверки сценария.

## Engine Compatibility Boundary

Не использовать решения, завязанные на конкретный движок, до отдельного выбора. Все данные должны быть переносимы: JSON / Markdown / CSV-like structures, с валидируемыми IDs и ссылками.
