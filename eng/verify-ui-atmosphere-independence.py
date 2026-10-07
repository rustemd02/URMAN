#!/usr/bin/env python3
"""Static gate for VIS-110: Canvas UI never reads or applies the world atmosphere.

The journal, documents, settings and the Old PC must look the same in every colour
state (frost, golden hour, green night). This checks only source; the readability of
each state still needs a capture on the station.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
UI = re.compile(r"^(UrmanUiTheme|DocumentUi|JournalUi|OldPcUi|OldPcXpChrome|SettingsUi|PauseMenu|MainMenu|DialogueUi)\b.*\.cs$")
FORBIDDEN = re.compile(
    r"AtmosphereProfiles|unifiedAtmosphere|URMAN_ATMOSPHERE_PHASE|WorldEnvironment|SetSnowMood|CanvasModulate|TonemapMode")

files = sorted(p for p in (ROOT / "game/scripts").glob("*.cs") if UI.match(p.name))
hits = []
for path in files:
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if FORBIDDEN.search(line):
            hits.append(f"{path.relative_to(ROOT)}:{number}: {line.strip()}")
# A global CanvasModulate anywhere would tint every Canvas UI as well.
for path in (ROOT / "game/scripts").rglob("*.cs"):
    if "CanvasModulate" in path.read_text(encoding="utf-8"):
        hits.append(f"{path.relative_to(ROOT)}: CanvasModulate tints all canvas UI")
if not files:
    print("FAIL: no UI sources matched")
    sys.exit(1)
if hits:
    print("FAIL")
    print("\n".join(" - " + h for h in hits))
    sys.exit(1)
print(f"PASS: {len(files)} UI sources independent of the world atmosphere")
