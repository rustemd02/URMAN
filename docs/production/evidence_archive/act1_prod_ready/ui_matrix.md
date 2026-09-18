# UI-матрица Фазы 9 (10 экранов × 2 разрешения)

Дата: 2026-09-10. Исполнительский верификационный источник: `act1_ui_readability`
PASS 2 разрешения × 5 критичных UI (лог: /tmp копия readability_final.log),
плюс код-аудит темы (единые PanelStyle/ButtonStyle с 5 состояниями в
MainMenuUi/PauseMenuUi/SettingsUi; общий шрифтовой оверрайд в tscn).

| Экран | 1280×720 | 1920×1080 | Источник |
|---|---|---|---|
| Настройки | PASS fit/close | PASS fit/close | readability smoke |
| Журнал | PASS fit/close | PASS fit/close | readability smoke |
| Старый ПК | PASS fit/close | PASS fit/close | readability smoke |
| Документ | PASS fit/close | PASS fit/close | readability smoke |
| Диалог | PASS fit/close | PASS fit/close | readability smoke |
| Главное меню | код-аудит: та же PanelStyle | код-аудит | MainMenuUi.cs:249 |
| Пауза | код-аудит: та же PanelStyle | код-аудит | PauseMenuUi.cs:278 |
| Субтитры | safe-area через textScale | safe-area | FirstPersonController |
| Interact prompt | high-contrast + outline 2/3px | то же | FirstPersonController:70 |
| Экран загрузки | статичная заставка main.tscn | то же | scenes/main.tscn |

Пиксельные скриншоты 5 критичных UI: evidence/act1_prod_ready/ui_matrix/
(10 PNG, реальный рендер, act1_ui_readability + opt-in URMAN_UI_SHOT_DIR).
Меню/пауза: код-аудит единой PanelStyle; пиксельные скрины меню и глубокий
художественный рескин — человеческий art-gate (документировано в ledger).
