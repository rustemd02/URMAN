---
schemaVersion: 1
id: urman.oldpc:chat/alsu
title: {"default":"Alsu","translations":{"ru":"Алсу"}}
presence: recently
requires: []
startNodeId: hello
freeTextReply: urman.oldpc:text/chat-alsu-free-text
nodes: [ {"id":"hello","from":"npc","textId":"urman.oldpc:text/chat-alsu-hello","requires":[],"reveals":[],"choices":[ {"id":"village","textId":"urman.oldpc:text/chat-alsu-choice-village","requires":[],"reveals":[],"nextNodeId":"village"}, {"id":"marat","textId":"urman.oldpc:text/chat-alsu-choice-marat","requires":[],"reveals":[],"nextNodeId":"marat"}]}, {"id":"village","from":"npc","textId":"urman.oldpc:text/chat-alsu-village","requires":[],"reveals":["школа"],"choices":[ {"id":"school","textId":"urman.oldpc:text/chat-alsu-choice-school","requires":[],"reveals":[],"nextNodeId":"school"}, {"id":"enough","textId":"urman.oldpc:text/chat-alsu-choice-enough","requires":[],"reveals":[],"nextNodeId":null}]}, {"id":"school","from":"npc","textId":"urman.oldpc:text/chat-alsu-school","requires":[],"reveals":["Фәридә"],"choices":[]}, {"id":"marat","from":"npc","textId":"urman.oldpc:text/chat-alsu-marat","requires":[],"reveals":["Ринат"],"choices":[ {"id":"rinat","textId":"urman.oldpc:text/chat-alsu-choice-rinat","requires":[],"reveals":[],"nextNodeId":"rinat"}, {"id":"stop","textId":"urman.oldpc:text/chat-alsu-choice-stop","requires":[],"reveals":[],"nextNodeId":null}]}, {"id":"rinat","from":"npc","textId":"urman.oldpc:text/chat-alsu-rinat","requires":[],"reveals":[],"choices":[]} ]
---

# Алсу

Алсу — человеческий проводник по деревне, а не мистическая разгадка. В треде она
отвечает на бытовые вопросы, называет закрытую школу и отправляет к Ринату, если
речь заходит о Марате.

Голос: живой, короткие шутки, татарские обращения. Никаких выводов за игрока:
она даёт направление («спроси Рината»), а не ответ.

Что тред даёт: термины «школа», «Фәридә», «Ринат» и мост к миру, а не к уликам.
