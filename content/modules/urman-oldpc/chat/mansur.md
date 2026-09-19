---
schemaVersion: 1
id: urman.oldpc:chat/mansur
title: {"default":"Mansur","translations":{"ru":"Бабай Мансур"}}
presence: recently
requires: []
startNodeId: hello
freeTextReply: urman.oldpc:text/chat-mansur-free-text
nodes: [ {"id":"hello","from":"npc","textId":"urman.oldpc:text/chat-mansur-hello","requires":[],"reveals":[],"choices":[ {"id":"what","textId":"urman.oldpc:text/chat-mansur-choice-what","requires":[],"reveals":[],"nextNodeId":"what-shows"}, {"id":"photos","textId":"urman.oldpc:text/chat-mansur-choice-photos","requires":[],"reveals":[],"nextNodeId":"photos"}, {"id":"later","textId":"urman.oldpc:text/chat-mansur-choice-later","requires":[],"reveals":[],"nextNodeId":null}]}, {"id":"what-shows","from":"npc","textId":"urman.oldpc:text/chat-mansur-what","requires":[],"reveals":[],"choices":[ {"id":"password","textId":"urman.oldpc:text/chat-mansur-choice-password","requires":[],"reveals":[],"nextNodeId":"password"}, {"id":"ok","textId":"urman.oldpc:text/chat-mansur-choice-ok","requires":[],"reveals":[],"nextNodeId":null}]}, {"id":"password","from":"npc","textId":"urman.oldpc:text/chat-mansur-password","requires":[],"reveals":["Гөлсинә"],"choices":[ {"id":"ask-ebi","textId":"urman.oldpc:text/chat-mansur-choice-ask-ebi","requires":[],"reveals":[],"nextNodeId":"ask-ebi"}, {"id":"enough","textId":"urman.oldpc:text/chat-mansur-choice-enough","requires":[],"reveals":[],"nextNodeId":null}]}, {"id":"ask-ebi","from":"npc","textId":"urman.oldpc:text/chat-mansur-ask-ebi","requires":[],"reveals":[],"choices":[]}, {"id":"photos","from":"npc","textId":"urman.oldpc:text/chat-mansur-photos","requires":[],"reveals":["Сабантуй"],"choices":[ {"id":"album","textId":"urman.oldpc:text/chat-mansur-choice-album","requires":[],"reveals":[],"nextNodeId":"album"}, {"id":"thanks","textId":"urman.oldpc:text/chat-mansur-choice-thanks","requires":[],"reveals":[],"nextNodeId":null}]}, {"id":"album","from":"npc","textId":"urman.oldpc:text/chat-mansur-album","requires":[],"reveals":[],"choices":[]} ]
---

# Бабай Мансур

Первый тред, который видит Айдар на старом компьютере: бабай просит посмотреть
машину. Это та же просьба, что раньше была короткой сценой у стола; теперь она
приходит сообщением и остаётся в треде.

Голос: сдержанный, короткие фразы, без объяснений. Про Марата — ничего: бабай
хранит память, но не отдаёт её первому вопросу.

Что тред даёт игроку: бытовой повод открыть компьютер, два поисковых термина
(«компьютер», «Гөлсинә») и путь к әби, а не к уликам.
