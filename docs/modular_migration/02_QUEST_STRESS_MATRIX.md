---
task_id: MM-02
title: Стресс-матрица двенадцати модульных квестов
status: proposal-fixture
kind: architecture-falsification
depends_on: [MM-01]
blocks: [MM-03]
parallel_group: null
owner_scope:
  - docs/modular_migration/02_QUEST_STRESS_MATRIX.md
forbidden_scope:
  - docs/urman_knowledge_base/canon.md
  - src/**
  - public/assets/**
deliverables:
  - Двенадцать неконанических architecture fixtures
  - Falsifier для data-only quest и custom capability
verification:
  - rg -n "^\| [0-9]+" docs/modular_migration/02_QUEST_STRESS_MATRIX.md
handoff: required
handoff_template: docs/modular_migration/TASK_TEMPLATE.md#формат-handoff
---

# MM-02 — Стресс-матрица квестов

## Статус материала

Все квесты ниже — `Proposal` и только architecture fixtures. Они не входят в канон, production campaign или asset backlog. Их механики и ассеты не нужно реализовывать: `MM-45` и `MM-60` используют схемы и mock providers.

## Матрица

| № | Квест | Что проверяет в данных | Capability / world primitive | Главный фальсификатор |
|---:|---|---|---|---|
| 1 | «Дом помнит места» | Состав объектов комнаты и правильные позиции задаются контентом | `spatial_placement`, item instances | Позиции или ID квеста зашиты в scene class |
| 2 | «Вода отвечает дважды» | Игрок сопоставляет два пространственных звуковых ответа | `spatial_audio_probe`, captions | Загадка нерешаема без слуха или знает ID квеста |
| 3 | «Защёлка на ночь» | Рецепт, варианты материалов и исходы описаны данными | `crafting`, atomic inventory transaction | Ресурс списан частично при неуспешной сборке |
| 4 | «До тени на мосту» | Окно доступности и последствия опоздания | logical clock, scheduler | `Date.now()` меняет сюжет или job срабатывает дважды после load |
| 5 | «Просьба Сарии» | Role binding, смешанная речь, словарь, предмет и расписание | dialogue, vocabulary, inventory, schedule | Свап персонажа требует переписать диалог или квест |
| 6 | «Объявление под дождём» | Варианты сцены и сравнение двух снимков | scene variants, photo/evidence compare | Renderer содержит сюжетные пути к конкретным изображениям |
| 7 | «Не глуши мотор» | Ограниченный по времени выбор и состояние транспорта | timed choice, vehicle state, audio alternative | Таймер переживает cancel или accessibility меняет исход |
| 8 | «Красный гребень» | Уникальный предмет, состояние, передача и место хранения | `ItemInstance`, custody, placement | Один предмет одновременно принадлежит двум владельцам |
| 9 | «Катушка № 6: голоса на линии» | Авторские фрагменты, метки и каноническое авторство | `audio_workbench`, seeded noise, subtitles | DSP попадает в DSL или load меняет интервалы и шум |
| 10 | «Вода помнит берег» | Состояния берега, вехи и безопасное окно | `environment_sim`, placement, resource claim | Два квеста независимо владеют рекой или load меняет воду |
| 11 | «Бичура не любит перестановок» | Room graph, набор предметов, checkpoint и мягкий fail | `stealth_space`, owner cleanup, accessible turn-based mode | После cancel остаются закрытые двери, AI знает quest ID |
| 12 | «Письма, которым не дали адреса» | Выбор 4 из 12 authored историй и runtime bindings | `evidence_workbench`, `content_instantiator`, provenance | Save перегенерирует адресата или procedural слой сочиняет канон |

## Обязательные свойства fixtures

- У каждой objective есть capability ID, валидируемый config, lifecycle и outcome schema.
- Quest lifecycle поддерживает параллельные objectives, `all/any/threshold`, optional/failure outcome, retry, checkpoint и cancel.
- Seed и отдельные RNG streams сохраняются; одинаковые seed и action log дают одинаковое состояние.
- Procedural выбор работает только по authored pool и constraints.
- Аудио, цвет, скорость реакции и мелкая моторика имеют эквивалентную доступную альтернативу.
- Культурно чувствительные детали Су Анасы, Бичуры, татарской речи и религиозного контекста не становятся каноном без отдельной проверки.

## Применение

`MM-03` использует матрицу для freeze. `MM-45` создаёт минимальные synthetic definitions и mock providers. `MM-60` проверяет компиляцию всех 12 fixtures, но не production gameplay.

Архитектура отвергается, если любой fixture требует упоминания своего ID в kernel, generic scene renderer, `GameState`, `SceneManager`, общем UI или provider другой механики.
