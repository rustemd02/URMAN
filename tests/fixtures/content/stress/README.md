# MM-60 stress fixtures

Все материалы в этой папке имеют статус `Proposal`: это не канон, не
production campaign и не список ассетов. Один portable pack кодирует все
двенадцать строк MM-02, а `tests/e2e/stress-fixtures.test.mjs` исполняет их
через headless mock providers.

| № | Fixture ID | Матрица MM-02 |
|---:|---|---|
| 1 | `spatial-placement` | Дом помнит места |
| 2 | `spatial-audio-probe` | Вода отвечает дважды |
| 3 | `crafting` | Защёлка на ночь |
| 4 | `scheduler-window` | До тени на мосту |
| 5 | `role-binding` | Просьба Сарии |
| 6 | `evidence-compare` | Объявление под дождём |
| 7 | `timed-choice` | Не глуши мотор |
| 8 | `custody-transfer` | Красный гребень |
| 9 | `audio-workbench` | Катушка № 6: голоса на линии |
| 10 | `environment-sim` | Вода помнит берег |
| 11 | `stealth-space` | Бичура не любит перестановок |
| 12 | `content-instantiator` | Письма, которым не дали адреса |

Ни графика, ни DSP, ни stealth AI, ни симуляция воды здесь не реализуются.
`contracts.json` и `contracts/stress-contracts.schema.json` задают для каждой
строки отдельные config/event/outcome данные и schema fragments. Provider mocks
проверяют их до создания session, отклоняют invalid config и затем проверяют
lifecycle, snapshot/restore, accessibility-equivalent outcome, cleanup и
resource claim.
