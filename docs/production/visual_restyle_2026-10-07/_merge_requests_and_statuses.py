import json, re, pathlib, datetime

root = pathlib.Path(".")
req_dir = root / "docs/urman_knowledge_base/art/asset_requests"
lanes = ["MAT", "TREES", "CHAR", "YARD", "ATMOS"]

frag = []
counts = {}
wanted = {}
name_re = re.compile(r"`([A-Za-z0-9_./-]+\.(?:png|jpg|webp|glb|blend|exr|tga))`")
for lane in lanes:
    p = req_dir / f"{lane}.md"
    if not p.exists():
        continue
    text = p.read_text(encoding="utf-8")
    counts[lane] = len(re.findall(r"(?m)^#{2,4}\s", text))
    for line in text.splitlines():
        for m in name_re.finditer(line):
            n = m.group(1)
            if n not in wanted and ("VIS-" in line or "albedo" in n or "normal" in n or "mask" in n or "rough" in n):
                wanted[n] = (lane, line.strip()[:150])
    frag.append(f"\n\n---\n\n# ЛАНА: {lane}\n\n{text.strip()}\n")

target = root / "docs/urman_knowledge_base/art/VISUAL_RESET_GENERATION_REQUESTS_2026-10-08.md"
asset_roots = [root / "game/assets", root / "assets", root / "content", root / "public"]
existing = set()
for base in asset_roots:
    if base.exists():
        for p in base.rglob("*"):
            if p.is_file() and p.suffix.lower() in (".png", ".jpg", ".webp", ".glb", ".blend", ".exr", ".tga"):
                existing.add(p.name)
present = {n: w for n, w in wanted.items() if n.split("/")[-1] in existing}
absent = {n: w for n, w in wanted.items() if n.split("/")[-1] not in present}


def rows(d):
    return "".join(f"| `{n}` | {w[0]} | {w[1]} |\n" for n, w in sorted(d.items()))


wanted_table = (
    "## Что именно сгенерировать (сводка имён)\n\n"
    "Полные спецификации — в разделах лан ниже (consumer / контекст / формат / UV / размер / "
    "назначение / референс / проверка). Здесь только перечень имён, чтобы ничего не потерялось.\n\n"
    "### Нужны новые файлы\n\n"
    "| Файл | Лана | Где описан |\n|---|---|---|\n" + rows(absent) +
    f"\nВсего к генерации: **{len(absent)}**.\n"
    "\n### Уже лежат в репозитории — их не генерировать, а подключать/сверять\n\n"
    "Эти имена упомянуты в заявках, но файл уже существует в дереве: заказывать его заново "
    "нельзя (правило переиспользования из VIS-005 и реестра текстур).\n\n"
    "| Файл | Лана | Где упомянут |\n|---|---|---|\n" + rows(present) +
    f"\nВсего уже на диске: **{len(present)}**.\n"
)
header = (
    "# УРМАН — сводный запрос на генерацию текстур и ассетов (visual reset, 2026-10-08)\n\n"
    "**Зачем этот файл.** Переделка визуального языка Акта I (VIS-001…VIS-118) требует фактур и мешиков, "
    "которых нет в репозитории. Исполнитель их не генерирует: ниже то, что нужно произвести GPT-Image "
    "или Blender. Имена уже имеют потребителя в коде, поэтому после генерации файл кладётся по указанному "
    "пути и включается существующей привязкой.\n\n"
    "**Как сдавать.** Для каждого блока: положить файл(ы) в указанный `destination`, соблюсти "
    "`format`/`size`/`colorspace`, не менять имя. Проверка — в блоке `verify`. Если кадр после подстановки "
    "не стал лучше, блок возвращается в работу, а не принимается «потому что файл существует».\n\n"
    f"**Собрано из фрагментов лан:** {', '.join(l for l in lanes if l in counts)}\n\n"
    "**Правила, которые ограничивают список (зафиксированы пакетом):**\n"
    "- сначала переиспользование: часть карт уже лежит на диске и не подключена — новые albedo для той же "
    "функции не заказываются, пока кандидат не сверен и не отвергнут с причиной;\n"
    "- ни одна фактура не является однотонной заглушкой;\n"
    "- нет чехарды v7/v8 вариантов: запрос на замену принятой карты помечен `edit` и объяснён;\n"
    "- нет запечённого света и бликов в albedo, нет «плесени на всё», нет chrome-look;\n"
    "- видимое low-poly не является целью; красный не является базовым цветом леса;\n"
    "- у каждого ассета есть consumer, provenance и лицензия (гейт `eng/verify-asset-registry.sh`).\n\n"
    + wanted_table +
    "\n## Разделы по ланам\n\n"
    "| Лана | Разделов во фрагменте | Фрагмент |\n|---|---|---|\n"
    + "".join(f"| {k} | {v} | `docs/urman_knowledge_base/art/asset_requests/{k}.md` |\n" for k, v in counts.items())
)
target.write_text(header + "".join(frag), encoding="utf-8")
print("WROTE", target, target.stat().st_size, "bytes; lane block counts:", counts)

# --- statuses: conservative, code never closes a visual card ---
sp = root / "docs/production/URMAN_VISUAL_RESET_EXECUTION_2026-10-07_status.json"
st = json.loads(sp.read_text(encoding="utf-8"))
led_dir = root / "docs/production/visual_restyle_2026-10-07"
mentioned = set()
for p in led_dir.glob("ledger_*.md"):
    body = p.read_text(encoding="utf-8", errors="replace")
    for m in re.finditer(r"VIS-(\d{3})", body):
        mentioned.add("VIS-" + m.group(1))

moved = []
for t in st["tasks"]:
    if t["id"] in mentioned and t["status"] in ("OPEN", "PARTIAL"):
        t["status"] = "CODE_DONE_VERIFY_PENDING"
        moved.append(t["id"])

st["updated"] = "2026-10-08"
st["iteration"] = "04"
sp.write_text(json.dumps(st, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
import collections
c = collections.Counter(t["status"] for t in st["tasks"])
print("moved to CODE_DONE_VERIFY_PENDING:", len(moved), sorted(moved))
print("status distribution:", dict(c))
