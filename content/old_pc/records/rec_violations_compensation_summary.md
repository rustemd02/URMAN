---
id: rec_violations_compensation_summary
type: record
title: "Сводка нарушений и компенсаций"
pcSection: violations_compensation
canonStatus: canon
reliability: pact_record
sourceKind: pact_record
inWorldSource: "Внутренний учёт Кырлая"
mvp: true
dangerLevel: 3
visibleFromStart: false
requires: ["clue_marat_case_boundary_marker"]
searchTerms: ["нарушение", "компенсация", "Марат", "граница", "ответил", "закрыто"]
suggestedTerms: ["не закрыто", "линия М", "проверка"]
reveals: ["clue_compensation_system_exists"]
contradicts: ["clue_marat_official_death_version"]
unlocks: ["rec_internal_accounting_damaged", "pressure_council_attention_1"]
relatedCharacters: ["char_marat", "char_babay", "char_rinat"]
relatedLocations: ["loc_kara_urman_edge", "loc_admin_archive"]
vocabulary: ["tt_urman"]
notesForLLM: "Не объяснять, кому и чем платят. Документ должен доказать административную систему вокруг пакта."
---

# Сводка

Период: последние закрытые случаи.

Формулировка для внешнего архива: несчастные случаи, болезни, уход из дома.

Внутренняя классификация:

| Дом | Случай | Категория | Компенсация |
| --- | --- | --- | --- |
| Н. | Марат | граница / ответил | закрыта внешне |
| М. | приезжий внук | проверка | не начинать без старшего |
| Р. | ночной голос | предупреждён | без записи |

Отметка на полях: «Закрыта внешне» не равно «закрыта перед урманом».
