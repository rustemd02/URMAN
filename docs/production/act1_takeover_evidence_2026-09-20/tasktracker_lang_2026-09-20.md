# ACT1-LANG — словарный контур: реализация и evidence (2026-09-20)

Коммит: «feat(act1): tatar vocabulary auto-collection, starting-level seeding…»

Реализовано (ACT1-LANG.1/.2/.5(частично)/.6/.7):

- Разметка контента: `startingKnowledge` (none/family/everyday/literary) для
  12/12 слов главы; схема `vocabulary.schema.json` расширена; пакеты
  пересобраны каноническим `eng/compile-game-content.sh`; golden-фикстура
  паритет-теста и fingerprint обновлены (9349c1d6…).
- Ядро/настройки: `GameSettingsSnapshot.TatarLanguageLevel` (init, дефолт
  "none" — старые settings.json и сейвы без поля читаются безопасно);
  `FirstPersonController.TatarLanguageLevel` публичное свойство, входит в
  Capture/ApplySettings и UserSettingsStore.
- Сидирование: `RuntimeBridge.SeedStartingVocabulary` при создании новой
  сессии кладёт слова уровней family (some) / family+everyday+literary
  (fluent) как ГИПОТЕЗЫ с sourceId `urman.starting-knowledge:<уровень>`.
  Подтверждения не выдаются — все квесты на `confirmed` сохраняют гейт.
  Debug-сессии и уровень none ничего не сеют (историческое поведение приезда,
  включая уже принятый запрет преждевременного `tt_yul=guessed`).
- Автосбор: `RuntimeBridge.ObserveVocabularyTextAsync(text, sourceId)` —
  неизвестные термины из читаемого текста становятся `guessed` с источником;
  подключено к DocumentUi (находка документа), DialogueUi (реплика) и
  OldPcUi (открытие документа на ПК). Дубли исключены лесенкой ядра.
- Приёмка: `act1_vocabulary_cycle_smoke_test` PASS:
  сид some (бабай/әби guessed с source, юл unknown) → реплика с «юл» →
  guessed с source → физическая находка «обратная сторона указателя» →
  confirmed → журнал показывает термины и маркер «услышано» →
  save/load сохраняет статусы и источники.
- Регресс: verify-dotnet зелёный (Core 59, Content 14); act1_footstep,
  first_person_step, act1_player_movement, footstep-lag-probe зелёные.

Честные границы:

- ACT1-LANG.3 (окно словаря на ПК) — не начата: нужен 10-й app id в
  контракте «окон max 9» ACT1-OLDPC-SHELL (схема+провайдер+миграция) и окно
  со статусами/фильтрами; зависимость ACT1-OLDPC-SHELL выполнена, задача
  ready.
- ACT1-LANG.4 (калибровочный тест в прологе) — не начата: нужна UI-сцена
  вопросов (данные — из реестра слов) и развилка с ручным выбором.
- ACT1-LANG.5 — остался UI-ряд в настройках и отдельный различающий смок
  fluent-уровня.
- Человеческая проверка татарского (консультант) — external/not-run.

Найденные при работе предсуществующие дефекты (на чистом дереве HEAD
воспроизводятся, к этому коммиту не относятся):

1. `chapter_one_flow_smoke_test`: `Node not found: Npc_timur_hazrat`
   (Act1People) → NullReferenceException, смок зависает. Блокирует основной
   flow-смок.
2. `act1_checkpoint_smoke_test`: «Begin did not focus the actual source text
   in read-only selection mode» (редактор старого ПК после батча чата/поиска).
3. `act1_first_person_corridor_smoke_test`: «route transition house-entry
   declared spawn yaw 64,7°, expected 0,0°».
4. `npm run content:check`: MissingRoleBinding razilya (village_shop) —
   известно из аудита 2026-09-15, не чинено молча.
