---
schemaVersion: 1
id: urman.oldpc:chat/self
title: {"default": "Aidar's notes", "translations": {"ru": "Заметки Айдара"}}
presence: oneself
requires: []
startNodeId: first
freeTextReply: urman.oldpc:text/chat-self-free-text
nodes: [{"id": "first", "from": "player", "textId": "urman.oldpc:text/chat-self-first", "requires": [], "reveals": [], "choices": [{"id": "mark", "textId": "urman.oldpc:text/chat-self-choice-mark", "requires": [], "reveals": [], "nextNodeId": "mark"}, {"id": "nothing", "textId": "urman.oldpc:text/chat-self-choice-nothing", "requires": [], "reveals": [], "nextNodeId": null}], "nextNodeId": null}, {"id": "mark", "from": "player", "textId": "urman.oldpc:text/chat-self-mark", "requires": [], "reveals": ["Марат"], "choices": [], "nextNodeId": null}]
---

# Заметки Айдара

Системный тред разговора с собой. Внутримирово честно: Айдар записывает мысли,
никто ему не отвечает. Здесь же позже появятся контекстные подсказки (ACT1-OLDPC-HINTS),
поэтому тред существует с первого открытия компьютера.

Голос: первое лицо, коротко, без выводов за игрока.
