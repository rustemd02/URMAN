---
schemaVersion: 1
id: urman.oldpc:document/rec_marat_case_register_conflict
title: {"default":"Marat case register line","translations":{"ru":"Строка реестра: дело Марата"}}
format: markdown
sourceFile: documents/rec_marat_case_register_conflict.md
assetRefs: ["urman.oldpc:asset/ui-document-viewer-template"]
knowledgeRefs: ["urman.chapter1:knowledge/clue_marat_case_boundary_marker"]
accessConditions: [{"op":"knowledge.status","knowledgeId":"urman.chapter1:knowledge/clue_marat_official_death_version","status":"confirmed"},{"op":"knowledge.status","knowledgeId":"urman.chapter1:knowledge/clue_marat_versions_conflict","status":"confirmed"},{"op":"npc.state","characterId":"urman.chapter1:character/naila","stateKey":"record_access_granted","value":true}]
openEffects: [{"op":"scene.request","sceneId":"urman.chapter1:scene/evidence-internal-register"},{"op":"journal.record","entryId":"urman.oldpc:document/rec_marat_case_register_conflict","sourceId":"urman.oldpc:document/rec_marat_case_register_conflict"}]
oldPc: {"type":"record","pcSection":"internal_accounting","canonStatus":"canon","reliability":"partial_truth","searchTerms":["Марат","реестр","граница","Кара-Урман","закрыто","ответил"],"suggestedTerms":["ответил","граница","компенсация"]}
---

# Реестр закрытых случаев

Строка 17.

Фамилия: Н.

Имя: Марат.

Категория: граница / ответил.

Внешняя формулировка: несчастный случай.

Внутренняя отметка: компенсация закрыта.

Ответственный: М.

Примечание: не выносить в общий архив.
