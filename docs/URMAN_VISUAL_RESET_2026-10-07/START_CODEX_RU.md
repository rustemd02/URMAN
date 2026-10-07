# START HERE — инструкция Codex

1. Прочитай `URMAN_FINAL_STYLE_RECIPE_RU.md`.
2. Прочитай `REFERENCES_RU.md`; запомни, что каждый код означает **свойство**, а не копирование всей игры.
   В Google Drive также открыт визуальный board с самими изображениями: `DRIVE_LINKS_RU.md`.
3. Основная очередь — `URMAN_VISUAL_RESTYLE_TASKS_RU.md`, **VIS-001…VIS-118**.
4. `HANDOVER_FOR_CODEX_VISUAL_RESET.md` — диагностическое обоснование и полный старый контекст.
5. До создания ассетов прочитай `ASSET_PASSPORTS_RU.md` и `AUTHOR_COMMENTS_COVERAGE_RU.md`.
6. Перед правкой каждого пути проверь актуальный checkout и `AGENTS.md`; пакет составлен по commit `69012da7029bb24c99826aa889b2b40fae875ef4`.
7. Если пользователь дал команду «выполни этот пакет», это разрешает visual-restyle implementation в пределах пакета, но **не отменяет** отдельные актуальные правила хоста о способе игровых прогонов/commit/push. Для runtime использовать разрешённую станцию/guard согласно свежему `AGENTS.md`.
8. Нельзя закрыть задачу потому, что код компилируется или PNG существует. Visual task закрывает before/after + критерии + human art acceptance там, где это указано.
9. VIS-112/113/114 — эталоны. После их принятия работа НЕ окончена: обязательны VIS-115–118.
