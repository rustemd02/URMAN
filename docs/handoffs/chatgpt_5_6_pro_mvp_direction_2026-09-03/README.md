# Handoff для GPT-5.6 Pro: направление MVP УРМАНА

Дата сборки: **2026-09-03**. Этот каталог и ZIP рядом с корнем репозитория — evidence-first пакет для одного аналитического ответа GPT-5.6 Pro. Он не является релизом, art lock, готовым демо или разрешением на изменения в репозитории.

## Важное предупреждение

Канонический checkout `/Users/unterlantas/Documents/GitHub/URMAN` сейчас сильно dirty: есть изменённые и неотслеживаемые файлы. `HEAD` (`718daefcea7de791de16e29ce0db86deb477e8d3`, ветка `main`) не описывает текущий продукт. Для оценки нужно читать working-tree исходники и этот пакет, а не делать вывод по одному commit.

Текущий first-person capture из `/private/tmp/urman-gpt56-pro-current-capture-20260903/` содержит 44 PNG и receipt, но только **25 уникальных SHA-256**. В receipt хэши всех 44 файлов совпадают, viewport настоящий root viewport, subviewport не использован, один capture-процесс, 8 визуальных зон и 10/10 waypoint. Однако направления сериализовали один и тот же кадр: 16 файлов совпадают в группе `connective_street_return` + `fap_exterior` + `fap_interior` + четыре направления `zirat`, ещё 5 — в `kara_approach`. Поэтому capture-gate **провален**; поздние зоны нельзя считать прошедшими 360°/lateral review. Все 44 файла сохранены в архиве под `visual_evidence/current_act1_360_failed_gate/` именно для диагностики. Это не повод подменять их старым benchmark.

## Как использовать

1. Загрузите `URMAN_GPT_5_6_PRO_HANDOFF_2026-09-03.zip` в ChatGPT GPT-5.6 Pro.
2. Вставьте текст из `PROMPT_FOR_GPT_5_6_PRO.md` как единственный рабочий prompt после загрузки.
3. Попросите Pro сначала прочитать **весь** распакованный bundle и `SOURCE_MANIFEST.md`, затем ответить по контракту `EXPECTED_RESPONSE_SCHEMA.md`.
4. В ответе Pro должен ссылаться на пути внутри bundle, отделять доказанное от гипотез и вернуть конкретный critical-path tracker для Luna-only исполнения.

Не просите Pro «оценить по картинке» без receipt и исходников. Техническая проходимость маршрута и наличие mesh не означают production visual quality, human playtest или release readiness.

## Порядок авторитетности

1. Нормализованный канон и MVP-бриф в разделах 1–17 `URMAN_Codex_Context.md`.
2. Ограничения работы в `AGENTS.md`.
3. Рабочие решения и нерешённые противоречия: `docs/urman_knowledge_base/canon.md`, `decision_log.md`, `open_questions.md`, `weak_points.md`.
4. Остальные KB-документы для соответствующей области: продукт, нарратив, язык, арт, техническая архитектура, playtest и release gates.
5. Working-tree runtime/content: `game/`, `content/`, `eng/` и исходные тесты. Это текущая реализация, не автоматически утверждённый продуктовый контракт.
6. `visual_evidence/current_act1_360_failed_gate/` и receipt — наблюдаемое текущее evidence с явно зафиксированным failed gate. Оно выше исторических benchmark-кадров только для описания текущего визуального состояния, но не доказывает качество.
7. `docs/experiments/agent_b_act1_world_report.md` и исторические handoff/route-документы — provenance, benchmark или эксперимент; они не могут переопределять канон или текущего runtime owner.

Если источники расходятся, не исправляйте их молча: назовите конфликт, укажите уровень авторитета и предложите решение только как решение Pro/архитектора.

## Результат graphify-навигации

Перед синтезом были выполнены запросы `graphify query` для Act I MVP/current owners, связки `RuntimeBridge`/`Act1WorldLayout`/`Act1ConnectedWorld` и release gates/evidence gaps. Результаты оказались широким BFS с повторяющимися и историческими узлами (включая Agent B), местами обрезанными; они полезны для навигации, но слабы как доказательство текущего owner или release state. Поэтому authoritative reading исходников, KB и current receipt имеет приоритет. `graphify-out` и его служебные изменения в handoff не включены.

## Что находится в архиве

- семь authored handoff-файлов из этого каталога;
- curated canon/product/art/technical/release документы;
- текущие owners runtime, контентный pack, tests и capture script;
- пять asset manifest-файлов без GLB/Blend бинарников;
- две target-картинки как визуальные цели, не как доказательство результата;
- текущие 44 PNG и receipt под `visual_evidence/current_act1_360_failed_gate/`.

Секреты, `.git`, кэши, `.tools`, `.godot`, build output и бинарные GLB/Blend в архив не включаются. `SOURCE_MANIFEST.md` — полный перечень и назначение каждого вложения.

## Что Pro должен вернуть

Ответ нужен на русском, в виде решительного production direction: точный вердикт, зафиксированное MVP, dependency graph, план всех восьми визуальных зон, системы/нарратив, **полный исчерпывающий tracker всего remaining Act I scope до tested MVP/release-candidate**, coverage matrix с `unmapped gaps = 0`, отдельно выделенная первая Luna-only волна из 5–10 tracker ID, stop-doing список, release gates, реальные риски/вопросы и одностраничная директива родительскому архитектору. Нельзя объявлять текущий demo готовым, оценивать календарные сроки, выдумывать невиденные факты или уходить в Acts II–V.
