# Next 10 Actions to MVP

1. **Взять `mvp_completion_handoff.md` как главный P0-план.**
   Не начинать с новых ассетов или лорных веток. Главная недостача: единая сцепка old PC → knowledge keys → journal → dialogue → vocabulary → pressure → cliffhanger.

2. **Закрыть runtime canon/style drift.**
   Проверить `characters.ts`, `chat_data.ts`, `MainMenuScene.ts`, `IntroScene.ts`, `HUD.ts`: Кырлай — деревня, Кара-Урман — урочище; Шүрәле не generic monster; Алсу 1926 не active truth без пометки.

3. **Создать shared knowledge source of truth.**
   Добавить `src/data/knowledge_keys.ts`, `vocabulary_data.ts`, `dialogue_data.ts`, `quests.ts` and shared state in `GameState`.

4. **Подключить old PC clues к общей игре.**
   Открытие и сохранение PC-документа должно обновлять shared clues, vocabulary, contradictions, journal and dialogue availability, not only old PC localStorage.

5. **Сделать investigation journal.**
   Расширить/заменить `NotebookUI`: улики, противоречия, timeline Марата, vocabulary, route sketch support.

6. **Реализовать dialogue key prototype на Ринате.**
   Минимум: без ключа отвод темы; official death key; internal register contradiction; dangerous do-not-answer/key reaction; pressure effect.

7. **Сделать первый татарский re-read loop.**
   `урман`, `тавыш`, `җавап`, `ярамый`, `зират`, `шүрәле` должны менять поиск, документ, диалог или route interpretation.

8. **Заменить P0 scene stubs существующими ink-wash ассетами.**
   Дом, мечеть and forest/cliffhanger must stop looking like HTML/3D placeholders. Use `public/assets/urman_mvp_remaining/` and `public/assets/urman_route_map/`.

9. **Замкнуть route → Kara-Urman → cliffhanger path.**
   Добавить поддержку scripted/final route actions if needed; игрок должен пройти до `Марат voice -> Ринат: "Не отвечай" -> hard cut` without URL jumps.

10. **Добавить validators and run `playtest_plan.md`.**
   Keep old PC validator, add clue graph and route graph validators, then run internal smoke, route orientation, old PC, narrative and language tests before external MVP playtest.
