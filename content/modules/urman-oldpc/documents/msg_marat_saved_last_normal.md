---
schemaVersion: 1
id: urman.oldpc:document/msg_marat_saved_last_normal
title: {"default":"Marat's saved message","translations":{"ru":"Сохранённое сообщение Марата"}}
format: markdown
sourceFile: documents/msg_marat_saved_last_normal.md
assetRefs: ["urman.oldpc:asset/ui-yalkyn-messenger-saved-marat-log"]
knowledgeRefs: ["urman.chapter1:knowledge/clue_marat_message_read"]
accessConditions: [{"op":"knowledge.status","knowledgeId":"urman.chapter1:knowledge/contradiction_marat_official_vs_internal","status":"confirmed"},{"op":"npc.state","characterId":"urman.chapter1:character/rinat","stateKey":"alerted","value":true}]
openEffects: [{"op":"scene.request","sceneId":"urman.chapter1:scene/evidence-saved-message"},{"op":"journal.record","entryId":"urman.oldpc:document/msg_marat_saved_last_normal","sourceId":"urman.oldpc:document/msg_marat_saved_last_normal"}]
oldPc: {"type":"message","pcSection":"saved_messages","canonStatus":"canon","reliability":"personal_memory","searchTerms":["Марат","сообщение","урман","бабай","ночь"],"suggestedTerms":["урман","кромка"]}
---

# Экспорт сообщения

Марат:

Айдар, если когда-нибудь это найдёшь, не начинай сразу спорить со взрослыми.

Они не просто боятся. Они как будто считают, что бояться правильно.

Сегодня опять слышал из урмана голос. Не как эхо. Как будто человек стоит рядом с деревьями и ждёт, когда ты ответишь.

Бабай сказал бы: не ходи. Но если никто не пойдёт, они так и будут всё закрывать.
