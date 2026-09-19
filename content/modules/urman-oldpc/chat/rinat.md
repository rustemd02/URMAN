---
schemaVersion: 1
id: urman.oldpc:chat/rinat
title: {"default":"Rinat","translations":{"ru":"Ринат"}}
presence: offline
requires: [{"op":"npc.state","characterId":"urman.chapter1:character/rinat","stateKey":"alerted","value":true}]
startNodeId: warning
freeTextReply: urman.oldpc:text/chat-rinat-free-text
nodes: [ {"id":"warning","from":"npc","textId":"urman.oldpc:text/chat-rinat-warning","requires":[],"reveals":[],"choices":[ {"id":"why","textId":"urman.oldpc:text/chat-rinat-choice-why","requires":[],"reveals":[],"nextNodeId":"why"}, {"id":"accept","textId":"urman.oldpc:text/chat-rinat-choice-accept","requires":[],"reveals":[],"nextNodeId":null}]}, {"id":"why","from":"npc","textId":"urman.oldpc:text/chat-rinat-why","requires":[],"reveals":["Тимер"],"choices":[ {"id":"timur","textId":"urman.oldpc:text/chat-rinat-choice-timur","requires":[],"reveals":[],"nextNodeId":"timur"}, {"id":"silent","textId":"urman.oldpc:text/chat-rinat-choice-silent","requires":[],"reveals":[],"nextNodeId":null}]}, {"id":"timur","from":"npc","textId":"urman.oldpc:text/chat-rinat-timur","requires":[],"reveals":["зират"],"choices":[]} ]
---

# Ринат

Тред открывается тем же условием, что и сохранённое сообщение Марата:
`rinat.alerted = true`. Ринат не в сети — его присутствие честное («не в сети»),
фейкового «печатает…» нет.

Голос: коротко, без объяснений, как человек, который уже видел последствия.
Он не пугает и не раскрывает: он останавливает и отправляет к Тимуру хәзрәту.

Что тред даёт: термины «Тимер» и «зират», а не документы.
