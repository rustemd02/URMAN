# Проверка целостности пакета

**Результат:** PASS. **Проверок:** 77.

Это проверка спецификации: не запуск игры, не измерение FPS, не доказательство визуального качества и не приёмка автора. Проверка порядка глав использует абстрактные предпосылки, а не исполняемый контент URMAN.

| Проверка | Результат | Примечание |
|---|---|---|
| worlds: count and unique IDs | PASS | 5 |
| photos: count and unique IDs | PASS | 13 |
| assets: count and unique IDs | PASS | 48 |
| tasks: count and unique IDs | PASS | 100 |
| migration: count and unique IDs | PASS | 118 |
| references: count and unique IDs | PASS | 24 |
| dialogues: count and unique IDs | PASS | 29 |
| coverage: count and unique IDs | PASS | 10 |
| Every task dependency exists | PASS |  |
| Task DAG is acyclic | PASS |  |
| Tasks have actionable steps and acceptance | PASS |  |
| All task asset/reference links resolve | PASS |  |
| All asset families have at least one producing/integrating task | PASS |  |
| No fabricated implementation status | PASS |  |
| Five passage photos and eight supporting photos | PASS |  |
| One passage photo per world | PASS |  |
| W01: valid local graph | PASS |  |
| W01: finite positions | PASS |  |
| W01: main spawn can reach action nodes and exit | PASS |  |
| W01: exactly three reachable linger nodes | PASS |  |
| W01: completion flags are supplied by actions | PASS |  |
| W01: assets exist | PASS |  |
| W01: six required capture views | PASS |  |
| W02: valid local graph | PASS |  |
| W02: finite positions | PASS |  |
| W02: main spawn can reach action nodes and exit | PASS |  |
| W02: exactly three reachable linger nodes | PASS |  |
| W02: completion flags are supplied by actions | PASS |  |
| W02: assets exist | PASS |  |
| W02: six required capture views | PASS |  |
| W03: valid local graph | PASS |  |
| W03: finite positions | PASS |  |
| W03: main spawn can reach action nodes and exit | PASS |  |
| W03: exactly three reachable linger nodes | PASS |  |
| W03: completion flags are supplied by actions | PASS |  |
| W03: assets exist | PASS |  |
| W03: six required capture views | PASS |  |
| W04: valid local graph | PASS |  |
| W04: finite positions | PASS |  |
| W04: main spawn can reach action nodes and exit | PASS |  |
| W04: exactly three reachable linger nodes | PASS |  |
| W04: completion flags are supplied by actions | PASS |  |
| W04: assets exist | PASS |  |
| W04: six required capture views | PASS |  |
| W05: valid local graph | PASS |  |
| W05: finite positions | PASS |  |
| W05: main spawn can reach action nodes and exit | PASS |  |
| W05: exactly three reachable linger nodes | PASS |  |
| W05: completion flags are supplied by actions | PASS |  |
| W05: assets exist | PASS |  |
| W05: six required capture views | PASS |  |
| Thirty distinct capture IDs | PASS |  |
| Every historical VIS task maps to current PW tasks | PASS |  |
| All author requirements have valid task coverage | PASS |  |
| Eighty-one unique non-empty dialogue lines | PASS |  |
| Tatar translation is not falsely marked reviewed | PASS |  |
| W05 remains optional for player but present in production | PASS |  |
| Final chapter requires both outside evidence chains | PASS |  |
| Abstract chapter order W01 → W02 → W03 → W04 | PASS | Content/anchor prerequisites are supplied as an abstract fixture; not an end-to-end game test. |
| Abstract chapter order W02 → W03 → W01 → W04 | PASS | Content/anchor prerequisites are supplied as an abstract fixture; not an end-to-end game test. |
| Abstract chapter order W02 → W01 → W03 → W04 | PASS | Content/anchor prerequisites are supplied as an abstract fixture; not an end-to-end game test. |
| Abstract chapter order W01 → W03 → W02 → W04 | PASS | Content/anchor prerequisites are supplied as an abstract fixture; not an end-to-end game test. |
| W04 cannot unlock without river evidence | PASS |  |
| W04 cannot unlock without family evidence | PASS |  |
| Sixteen specification chapters | PASS |  |
| Five detailed level passports | PASS |  |
| Coverage document paths exist | PASS |  |
| Local reference LEGACY01 | PASS | legacy/URMAN_VISUAL_RESET_PACKAGE.zip |
| Local reference REF-AMINOV | PASS | references/ART_Aminov_SuAnasy_1978_reference.jpeg |
| Local reference REF-KARAMYSHEV | PASS | references/ART_Karamyshev_SuAnasy_1985_reference.jpeg |
| Local reference REF-FATKHUTDINOV | PASS | references/ART_Fatkhutdinov_SuAnasy_1995_1997_reference.jpeg |
| Local reference REF-P1 | PASS | references/P1_user_selected_painterly.png |
| No redistributable font files in package or historical ZIP | PASS |  |
| Readable task cards match registry IDs | PASS |  |
| Negative self-test: cyclic dependency | PASS | Damaged copy rejected; source unchanged. |
| Negative self-test: missing asset | PASS | Damaged copy rejected; source unchanged. |
| Negative self-test: optional chapter made mandatory | PASS | Damaged copy rejected; source unchanged. |
