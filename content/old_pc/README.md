# Old PC Content Authoring Guide

Status: first authoring contract, 2026-05-18.

This folder is the source of truth for content shown through бабай's old PC. It is intentionally human-readable so writers and LLM-assisted sessions can add or change content without editing UI code.

Canonical product rules live in `docs/urman_knowledge_base/old_pc.md`.

## Folder Layout

```text
content/old_pc/
  documents/   official documents, scanned notes, letters
  records/     village records, pact-adjacent accounting, registries
  messages/    saved chats, exported logs, direct messages
  tatarwiki/   local article pages
  schema/      machine-checkable contracts
```

## Required Frontmatter

Every content file must start with YAML frontmatter.

```yaml
---
id: doc_unique_id
type: document
title: "Readable title"
pcSection: documents_marat
canonStatus: canon
reliability: partial_truth
sourceKind: official_document
inWorldSource: "ФАП / сельсовет"
mvp: true
dangerLevel: 1
visibleFromStart: true
requires: []
searchTerms: ["Марат", "зират"]
suggestedTerms: ["Кара-Урман"]
reveals: ["clue_example"]
contradicts: []
unlocks: []
relatedCharacters: ["char_marat"]
relatedLocations: ["loc_zirat"]
vocabulary: []
notesForLLM: "Short instruction for future edits."
---
```

## Allowed Values

`type`:

- `document`
- `record`
- `message`
- `tatarwiki_article`
- `folder_note`
- `corrupted_fragment`

`pcSection`:

- `archive_search`
- `documents_marat`
- `tatarwiki`
- `saved_messages`
- `internal_accounting`
- `household_registry`
- `violations_compensation`
- `kara_urman`
- `damaged_hidden`
- `household_misc`

`canonStatus`:

- `canon`
- `soft_canon`
- `hypothesis`
- `proposal`
- `in_world_lie`

`reliability`:

- `official_lie`
- `partial_truth`
- `personal_memory`
- `village_record`
- `pact_record`
- `folklore_mask`
- `corrupted`
- `unverified`

## Writing Rules

- Do not make technical metadata the main clue.
- Use document marks: stamps, case numbers, registry dates, crossed-out lines, repeated categories.
- Each MVP file must either reveal, contradict, unlock or reframe something.
- Do not explain the pact fully in MVP content.
- Keep Татарвики articles readable as folklore first and practical instructions second.
- Mark lies as lies in frontmatter; do not rely on future readers remembering author intent.
- Add useful `searchTerms`, but make sure each useful term can be discovered in-game.
- Keep meaningful text editable and separate from generated PNG backgrounds.

## First Prototype Success

The first PC content pack currently contains 10 MVP files and should let the player:

- search a term independently;
- find an official Marat document;
- find a contradictory registry or internal record;
- read one saved Marat message;
- open or reinterpret one gated/corrupted fragment;
- use one clue in a later NPC conversation.
