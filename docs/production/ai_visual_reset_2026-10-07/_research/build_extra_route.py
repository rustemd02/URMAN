#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Дополнительный набор: по одному кадру на каждый момент игрового маршрута.

Берём каталог `images-walk-13` (144 кадра вдоль маршрута Акта I) и выбираем по одному
серединному кадру на каждый уникальный префикс — получается 44 недублирующихся момента:
приезд, дом, старый ПК, ФАП, мечеть, улица, лес, зиратская дорога, диалоги с NPC.
Складывается в отдельную папку, чтобы основной пак не раздувался.
"""
import os
import re
import shutil
import subprocess
import sys

ROOT = "/Users/unterlantas/Documents/GitHub/URMAN"
SRC = os.path.join(ROOT, "docs/production/act1_takeover_evidence_2026-09-16/images-walk-13")
OUT = os.path.join(ROOT, "docs/production/ai_visual_reset_2026-10-07/screenshots_extra/08_route_moments")

DESC = {
    "alsu-message-outward": "Дорога к Алсу: отправка сообщения (внешний путь).",
    "alsu-message-return": "Дорога к Алсу: возврат после сообщения.",
    "approach-urman.chapter1_interaction_arrival-enter-house": "Подход ко входу в дом бабая — первый вход.",
    "approach-urman.chapter1_interaction_edge-sketch-to-zirat-road": "Переход от наброска кромки леса к зиратской дороге.",
    "approach-urman.chapter1_interaction_fap-document-desk-to-official-record": "ФАП: от стола документов к официальной записи.",
    "approach-urman.chapter1_interaction_fap-to-document-desk": "ФАП: подход к столу документов.",
    "approach-urman.chapter1_interaction_internal-register-to-rinat": "ФАП: от внутреннего журнала к Ринату.",
    "approach-urman.chapter1_interaction_observe-rinat-roadside": "Наблюдение за Ринатом у дороги.",
    "approach-urman.chapter1_interaction_observe-sketch-landmarks": "Наблюдение ориентиров по наброску.",
    "approach-urman.chapter1_interaction_official-leave-clinic": "Выход из ФАПа после официальной части.",
    "approach-urman.chapter1_interaction_official-to-internal-register": "ФАП: от официальной части к внутреннему журналу.",
    "approach-urman.chapter1_interaction_route-to-fap": "Маршрут к ФАПу по улице.",
    "approach-urman.chapter1_interaction_talk-alsu": "Разговор с Алсу (точка подхода).",
    "approach-urman.chapter1_interaction_talk-gulsina": "Разговор с Гөлсинә (точка подхода).",
    "approach-urman.chapter1_interaction_talk-mansur": "Разговор с Мансуром (точка подхода).",
    "approach-urman.chapter1_interaction_view-arrival-message": "Просмотр приветственного сообщения по приезде.",
    "approach-urman.chapter1_interaction_zirat-road-to-forest": "От зиратской дороги к лесу.",
    "approach-urman.chapter1_interaction_zirat-roadside-clue": "Находка у зиратской дороги.",
    "arrival-house-axis": "Ось подхода к дому бабая.",
    "arrival-house-path": "Тропа от дороги к дому.",
    "computer-aisle": "Проход к старому ПК в доме.",
    "fap-alsu-bypass": "ФАП: обход Аlсу / боковой проход.",
    "fap-branch": "ФАП: развилка коридоров.",
    "fap-return": "ФАП: обратный путь.",
    "forest-approach": "Подход к лесу.",
    "house-door-approach": "Подход к двери дома изнутри.",
    "house-exit-aisle": "Выход из дома: проход.",
    "house-exit-street": "Выход из дома на улицу.",
    "house-return": "Возврат к дому.",
    "house-return-yard": "Возврат во двор дома.",
    "house-yard-entry": "Вход во двор дома.",
    "mosque-access-landing": "Мечеть: площадка подхода.",
    "mosque-enter-doorway": "Мечеть: входной проём.",
    "mosque-enter-timur-hall": "Мечеть: вход в зал, где Тимур хәзрәт.",
    "mosque-enter-vestibule": "Мечеть: вход в вестибюль.",
    "mosque-leave-doorway": "Мечеть: выход через проём.",
    "mosque-leave-landing": "Мечеть: выход на площадку.",
    "mosque-leave-vestibule": "Мечеть: выход из вестибюля.",
    "mosque-return-landing": "Мечеть: возврат на площадку.",
    "naila-question-outward": "ФАП: путь с вопросом к Наиле.",
    "naila-question-return": "ФАП: возврат после разговора с Наилей.",
    "naila-reception-aisle": "ФАП: ресепшен, проход к Наиле.",
    "timur-route-outward": "Путь к мечети (к Тимуру).",
    "timur-to-zirat-street": "От мечети к зиратской улице.",
}


def main():
    if not os.path.isdir(SRC):
        print("нет каталога-источника: " + SRC)
        return 1
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        os.remove(os.path.join(OUT, f))

    groups = {}
    for name in sorted(os.listdir(SRC)):
        if not name.endswith(".png"):
            continue
        prefix = re.sub(r"(-[-0-9.,]+)?\.png$", "", name)
        groups.setdefault(prefix, []).append(name)

    missing_desc = []
    n = 0
    for prefix in sorted(groups):
        files = sorted(groups[prefix])
        pick = files[len(files) // 2]
        n += 1
        slug = re.sub(r"[^a-z0-9]+", "_", prefix.split("_interaction_")[-1].lower()).strip("_")[:60]
        dst = os.path.join(OUT, f"{n:03d}_{slug}.jpg")
        r = subprocess.run(["sips", "-s", "format", "jpeg", "-s", "formatOptions", "88",
                            os.path.join(SRC, pick), "--out", dst],
                           capture_output=True, text=True)
        if r.returncode != 0:
            print("sips failed: " + pick)
            return 1
        if prefix not in DESC:
            missing_desc.append(prefix)

    print(f"собрано {n} кадров в {OUT}")
    if missing_desc:
        print("нет описания для:")
        for p in missing_desc:
            print("  " + p)
    return 0


if __name__ == "__main__":
    sys.exit(main())
