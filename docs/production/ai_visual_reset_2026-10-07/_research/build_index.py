#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Генерирует 02_SCREENSHOT_INDEX_RU.md из pack_manifest.json."""
import json
import os
from collections import OrderedDict

BASE = "/Users/unterlantas/Documents/GitHub/URMAN/docs/production/ai_visual_reset_2026-10-07"
MAN = os.path.join(BASE, "_research", "pack_manifest.json")

CAT_TITLES = OrderedDict([
    ("01_world_exterior", "01. Мир снаружи: улицы, дворы, деревня, горизонт"),
    ("02_materials_snow_trees", "02. Материалы, снег, деревья, калибровка стиля"),
    ("03_interiors", "03. Интерьеры и обжитость"),
    ("04_characters", "04. Персонажи, игрок, анимация"),
    ("05_ui", "05. Интерфейс, документы, старый ПК, журнал"),
    ("06_night_forest_atmosphere", "06. Ночь, лес, атмосфера, планировка"),
    ("07_vehicle_cutscene", "07. Машина, поездка, катсцены"),
])

CAT_INTRO = {
    "01_world_exterior": "Первое впечатление от мира. Именно здесь видно «пустоту», плоский снег, «палочные» деревья, отсутствие глубины и следов жизни.",
    "02_materials_snow_trees": "Материалы и их поведение в кадре, а также опорные кадры стиля и референсы направления.",
    "03_interiors": "Обжитость, свет, количество и качество реквизита, качество крупных планов.",
    "04_characters": "Лица, руки, пропорции, одежда, силуэт, ходьба, idle.",
    "05_ui": "Стиль интерфейса, читаемость, шрифты, взаимодействие UI с миром.",
    "06_night_forest_atmosphere": "Ночные и тревожные сцены, читаемость в темноте, композиция центра деревни.",
    "07_vehicle_cutscene": "Транспорт, салон, поездка, постановочные кадры.",
}


def main():
    with open(MAN, encoding="utf-8") as f:
        man = json.load(f)
    by_cat = OrderedDict()
    for m in man:
        by_cat.setdefault(m["category"], []).append(m)

    lines = []
    lines.append("# Индекс пака кадров УРМАН (для внешнего art-ревью)\n")
    lines.append("Дата сборки: 2026-10-07. Все кадры — снимки **реального игрового рендера** "
                 "Godot 4 (Forward+), сделанные проектными harness-скриптами, либо принятые "
                 "в проекте текстурные/стилевые диагностики. Внешних референсных фотографий в паке нет.\n")
    lines.append("Папка `00_contact_sheets/` — контактные листы по категориям (номер на листе = "
                 "трёхзначный номер файла в папке категории). Остальные папки — полноразмерные кадры.\n")
    total = len(man)
    size = sum(m["bytes"] for m in man) / 1024 / 1024
    lines.append(f"Всего кадров: **{total}**, суммарный размер **{size:.1f} MB**.\n")
    lines.append("Столбец «Смотреть» — на что именно обратить внимание. "
                 "Столбец «Источник» — путь в репозитории, откуда взят кадр.\n")

    for cat, items in by_cat.items():
        lines.append(f"\n## {CAT_TITLES.get(cat, cat)}\n")
        lines.append(CAT_INTRO.get(cat, "") + "\n")
        lines.append("| # | Файл | Что показывает | Смотреть | Источник |")
        lines.append("|---|---|---|---|---|")
        for m in sorted(items, key=lambda x: x["index"]):
            note = m["note"].replace("|", "\\|")
            short = os.path.basename(m["file"])[4:-4].replace("_", " ")
            lines.append(f"| {m['index']:03d} | `{m['file']}` | {short} | {note} | `{m['source']}` |")

    lines.append("\n## Чего в паке намеренно нет\n")
    lines.append("- Кадров прототипа Тамары (`docs/production/tamara_mvp_2026-09-26/`) — вне текущего объёма работ.")
    lines.append("- Кадров legacy веб-прототипа в `src/`/`public/` — это не игра (Three.js-прототип, признан устаревшим).")
    lines.append("- Полного 60-минутного прохождения — харнесс-кадры фиксированные, а не живой плейтест.\n")

    out = os.path.join(BASE, "02_SCREENSHOT_INDEX_RU.md")
    with open(out, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"{out}: {len(lines)} строк, {total} кадров")


if __name__ == "__main__":
    main()
