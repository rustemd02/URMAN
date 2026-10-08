#!/usr/bin/env bash
set -euo pipefail

# Deterministic, runtime-independent gate for versioned painterly albedo candidates.
# The embedded Python decoder deliberately uses only the standard library so this
# check can run before Godot/Blender and does not alter the material/shader path.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEFAULT_DIR="$ROOT_DIR/game/assets/textures/painterly"
REPORT_PATH=""
ALLOW_MISSING=0
CONTRACT=0
MAX_MEAN_SEAM="0.120000"
MAX_MAX_SEAM="0.400000"
MAX_CLIP_FRACTION="0.020000"
MAX_HIGH_SAT_FRACTION="0.080000"
MAX_MEAN_SATURATION="0.680000"

usage() {
    cat <<'EOF'
Usage: eng/verify-painterly-textures.sh [options] [candidate.png ...]

Checks legacy *_v2_albedo.png through *_v6_albedo.png and explicitly mapped
urman_*_vNN_basecolor.png TILE candidates without touching Godot/runtime files.
Legacy sources remain 1024 square; catalogue masters may be 512–2048 square RGB.
With no paths, candidates are discovered under game/assets/textures/painterly.

Options:
  --contract                 Static material-contract mode (VIS-035/038/081/092/
                             093/094/095): reads the C# material owners and the
                             asset-request document instead of decoding pixels.
  --dir DIR                  Discover candidates in DIR.
  --report FILE              Also write the markdown result to FILE.
  --allow-missing            Return success when no candidates exist (OPEN).
  --max-mean-seam VALUE      Mean opposite-edge seam threshold (default 0.12).
  --max-max-seam VALUE       Maximum opposite-edge seam threshold (default 0.40).
  --max-clip-fraction VALUE  Per-channel exact 0/255 threshold (default 0.02).
  --max-high-sat VALUE       Fraction with HSV saturation > 0.90 (default 0.08).
  --max-mean-sat VALUE       Mean HSV saturation threshold (default 0.68).
  -h, --help                 Show this help.

Exit status is non-zero when a candidate fails or when no candidate exists unless
--allow-missing is supplied. OPEN is never presented as PASS.
EOF
}

INPUT_PATHS=()
DISCOVERY_DIR="$DEFAULT_DIR"
while (($# > 0)); do
    case "$1" in
        --dir)
            (($# >= 2)) || { echo "--dir requires a value" >&2; exit 64; }
            DISCOVERY_DIR="$2"
            shift 2
            ;;
        --report)
            (($# >= 2)) || { echo "--report requires a value" >&2; exit 64; }
            REPORT_PATH="$2"
            shift 2
            ;;
        --allow-missing)
            ALLOW_MISSING=1
            shift
            ;;
        --contract)
            CONTRACT=1
            shift
            ;;
        --max-mean-seam|--max-max-seam|--max-clip-fraction|--max-high-sat|--max-mean-sat)
            (($# >= 2)) || { echo "$1 requires a value" >&2; exit 64; }
            case "$1" in
                --max-mean-seam) MAX_MEAN_SEAM="$2" ;;
                --max-max-seam) MAX_MAX_SEAM="$2" ;;
                --max-clip-fraction) MAX_CLIP_FRACTION="$2" ;;
                --max-high-sat) MAX_HIGH_SAT_FRACTION="$2" ;;
                --max-mean-sat) MAX_MEAN_SATURATION="$2" ;;
            esac
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        --)
            shift
            INPUT_PATHS+=("$@")
            break
            ;;
        -* )
            echo "Unknown option: $1" >&2
            usage >&2
            exit 64
            ;;
        *)
            INPUT_PATHS+=("$1")
            shift
            ;;
    esac
done

if ((${#INPUT_PATHS[@]} == 0)) && [[ "$CONTRACT" != "1" ]]; then
    if [[ -d "$DISCOVERY_DIR" ]]; then
        while IFS= read -r candidate; do
            INPUT_PATHS+=("$candidate")
        done < <(find "$DISCOVERY_DIR" -maxdepth 1 -type f \( -name '*_v2_albedo.png' -o -name '*_v3_albedo.png' -o -name '*_v4_albedo.png' -o -name '*_v5_albedo.png' -o -name '*_v6_albedo.png' -o -name 'urman_*_v??_basecolor.png' \) -print | LC_ALL=C sort)
    fi
fi

# Resolve a working interpreter. On a Windows station `python3` may be the Store
# execution alias, which never runs the gate, so each candidate is probed instead of
# trusted.
PYTHON_OK=0
if [[ -n "${PYTHON_BIN:-}" ]]; then
    if "$PYTHON_BIN" -c "import sys; raise SystemExit(0 if sys.version_info>=(3,8) else 1)" >/dev/null 2>&1; then
        PYTHON_OK=1
    else
        echo "PYTHON_BIN=$PYTHON_BIN is not a usable Python 3 interpreter" >&2
    fi
else
    for probe in python3 python py; do
        if command -v "$probe" >/dev/null 2>&1 \
            && "$probe" -c "import sys; raise SystemExit(0 if sys.version_info>=(3,8) else 1)" >/dev/null 2>&1; then
            PYTHON_BIN="$probe"
            PYTHON_OK=1
            break
        fi
    done
fi
if [[ "$PYTHON_OK" != "1" ]]; then
    echo "eng/verify-painterly-textures.sh: no usable Python 3 found (tried \$PYTHON_BIN, python3, python, py)." >&2
    echo "This gate does not run on the game host; install Python 3 or set PYTHON_BIN." >&2
    exit 127
fi

if [[ "$CONTRACT" == "1" ]]; then
    set +e
    "$PYTHON_BIN" - "$ROOT_DIR" "$REPORT_PATH" <<'CONTRACT_PY'
"""VIS-035/038/081/092/093/094/095 static material-contract gate.

This reads the material owners as source, so it runs before Godot, before import and
before any capture. Every check is a written project rule, not a taste judgement:

1. declared texture paths resolve on disk or are registered as production requests;
2. metal policy: no dielectric coating carries metallic > 0, and only the physically
   exposed metal families are allowed metallic at all (VIS-095);
3. no dead response row: a family that declares a drawn map must give it an effect
   (VIS-092 sampler discipline);
4. the snow micro vertex amplitude stays at the measured +/-0.002 m, so a material
   tweak can never silently become a substitute for snow geometry (VIS-034/081);
5. triplanar wood/plaster families keep an explicit repeat of at least 1 m per tile
   so a 10 m run does not read as a grid (VIS-035/093/094).

It does not prove any visible result; that stays with the paired frames.
"""
import re
import sys
from pathlib import Path

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

root = Path(sys.argv[1]).resolve()
report_path = sys.argv[2]
textures = root / "game" / "assets" / "textures"
requests_doc = root / "docs" / "urman_knowledge_base" / "art" / "asset_requests" / "MAT.md"
pml_path = root / "game" / "scripts" / "PainterlyMaterialLibrary.cs"
rural_path = root / "game" / "scripts" / "RuralPropMaterials.cs"

issues = []
notes = []
rows = []

pml = pml_path.read_text(encoding="utf-8")
rural = rural_path.read_text(encoding="utf-8")
requests_text = requests_doc.read_text(encoding="utf-8") if requests_doc.exists() else ""
requests_names = set(re.findall(r"([A-Za-z0-9_]+\.png)", requests_text))

# ── 1. every declared res:// path is on disk or registered as a request ───────────
declared = set()
for res_path in re.findall(r'"(res://assets/textures/[^"]+)"', pml + rural):
    # Skip a path built by interpolation ("...{textureName}.png") and the bare folder
    # constant: neither names a file this gate can resolve.
    if "{" in res_path or res_path.endswith("/"):
        continue
    declared.add(res_path)
# RuralProp and PML build some paths by joining a root constant with a file name.
response_root = ""
m = re.search(r'const string ResponseRoot = "res://assets/textures/([^"]+)"', pml)
if m:
    response_root = m.group(1).rstrip("/")
for name in re.findall(r'ResponseRoot \+ "([^"]+)"', pml):
    declared.add("res://assets/textures/" + response_root + "/" + name)
rural_dirs = {"realism_20260929": "realism_20260929"}
for name in re.findall(r'\["([a-z_]+)"\] = new\("([^"]+)"', rural):
    folder, file_name = name[1].split("/", 1) if "/" in name[1] else ("painterly", name[1])
    declared.add("res://assets/textures/" + folder + "/" + file_name)

bound = missing_registered = unexplained = 0
for res_path in sorted(declared):
    relative = res_path.replace("res://assets/textures/", "")
    on_disk = (textures / relative).exists()
    file_name = Path(relative).name
    if on_disk:
        bound += 1
    elif file_name in requests_names:
        missing_registered += 1
    else:
        unexplained += 1
        issues.append(f"declared texture is neither on disk nor registered in MAT.md: {res_path}")
rows.append(f"declared texture paths: {bound} on disk, {missing_registered} registered as pending requests, {unexplained} unexplained")

# ── 2. metal policy ───────────────────────────────────────────────────────────────
metallic_block = re.search(r'SetShaderParameter\("metallic_value", surface switch\s*\{(.*?)\}\);', pml, re.S)
if not metallic_block:
    issues.append("could not find the metallic_value switch in PainterlyMaterialLibrary")
else:
    arms = dict(re.findall(r'"([a-z_0-9]+)" => ([0-9.]+)f', metallic_block.group(1)))
    dielectric_names = ("metal", "enamel", "steel", "vehicle_paint", "plastic", "plastic_abs",
                        "rubber", "vehicle_rubber", "carpet", "cloth", "wood", "plaster")
    for family, value in arms.items():
        if family in dielectric_names and float(value) > 0:
            issues.append(f"VIS-095: dielectric family '{family}' declares metallic {value}")
    allowed_metal = {"iron", "zinc_sheet", "vehicle_bare_metal", "vehicle_trim_metal"}
    for family, value in arms.items():
        if float(value) > 0 and family not in allowed_metal:
            issues.append(f"VIS-095: unexpected metallic {value} on family '{family}'")
    notes.append("painterly metallic arms: " + ", ".join(f"{k}={v}" for k, v in sorted(arms.items())))

rural_arms = dict(re.findall(r'\["([a-z_]+)"\] = new\("[^"]+", (?:[0-9.]+f), (?:[0-9.]+f), ([0-9.]+f|1f),', rural))
coatings = {"wood", "plywood", "steel", "enamel", "laminate", "cloth", "upholstery", "velvet",
            "curtain", "plastic", "rubber", "ceramic", "earthenware", "concrete"}
for family, value in rural_arms.items():
    numeric = 1.0 if value == "1f" else float(value.rstrip("f"))
    if family in coatings and numeric > 0:
        issues.append(f"VIS-095: RuralProp coating '{family}' has metallic {numeric}")
rows.append(f"RuralProp metallic values read for {len(rural_arms)} finishes")

# ── 3. no dead response row ───────────────────────────────────────────────────────
response_body = re.search(r'FamilyResponses = new\(StringComparer\.Ordinal\)\s*\{(.*?)\n    \};', pml, re.S)
if not response_body:
    issues.append("could not find the FamilyResponses table")
else:
    entries = re.findall(r'\["([a-z_0-9]+)"\] = new\(\) \{(.*?)\},', response_body.group(1), re.S)
    dead = 0
    for family, body in entries:
        relief = re.search(r'Relief = [0-9.]+f', body)
        maps = re.findall(r'(?:NormalMap|RoughnessMap|WearMap) = ResponseRoot \+ "([^"]+)"', body)
        if not maps and not relief:
            dead += 1
            issues.append(f"VIS-092: family '{family}' has a response row with neither relief nor a drawn map")
        for drawn in maps:
            if drawn not in requests_names and not (textures / "response" / drawn).exists():
                issues.append(f"VIS-092: drawn map '{drawn}' is bound by '{family}' but not registered in MAT.md")
    if not entries:
        issues.append("VIS-092: FamilyResponses table parsed empty; the gate itself needs fixing")
    rows.append(f"FamilyResponses rows: {len(entries)}, dead rows: {dead}")

# ── 4. snow micro amplitude stays sub-centimetre ──────────────────────────────────
if "* 0.004 * snow_relief_scale" not in pml:
    issues.append("VIS-034: the snow micro vertex term no longer matches the measured +/-0.002 m amplitude")
else:
    rows.append("snow micro relief amplitude held at 0.004 * (r-0.5) = +/-0.002 m [K02]")

# ── 5. explicit repeat for the families this wave re-tiled ───────────────────────
# Two floors, not one blended claim:
#  * WAVE_FLOOR: the wood/plaster families VIS-035/093/094 deliberately re-tiled must
#    keep at least one metre per repeat, so a 10 m run cannot read as a grid.
#  * GLOBAL_FLOOR: every other triplanar family may not repeat faster than ~0.35 m.
#    Their authored values belong to other cards (cloth, carpet, foliage, snow), so the
#    gate does not silently re-tile them; it only refuses an accidental micro-tile.
# Metric-UV families (unit scale is their UV contract) and narrow mouldings are exempt.
wave_families = {"wood", "wood_fence", "wood_facade", "wood_prop", "wood_furniture",
                 "wood_furniture_interior", "log_wall", "plaster", "plaster_domestic",
                 "wall_institution", "wallpaper"}
tile_floor_exempt = {"wood_painted_trim", "wood_log_uv", "wood_fence_uv", "wood_fence_vertical",
                     "wood_fence_rail", "cloth_table", "cloth_curtain", "hay_fibers", "hay_bundle"}
surface_table = re.search(r'SurfaceTextures = new\(StringComparer\.Ordinal\)\s*\{(.*?)\n    \};', pml, re.S)
if not surface_table:
    issues.append("could not find the SurfaceTextures table")
else:
    checked = 0
    for line in surface_table.group(1).splitlines():
        entry = re.search(r'\["([a-z_0-9]+)"\] = \("res://[^"]+", (.*)\),\s*$', line)
        if not entry:
            continue
        family, expression = entry.group(1), entry.group(2)
        inner = re.search(r'new Vector2\(([^)]*)\)', expression)
        if inner is None:
            numbers = []              # Vector2.One
            body = ""
        else:
            body = inner.group(1)
            numbers = [float(value) for value in re.findall(r"-?\d*\.?\d+", body)]
        if not numbers:
            value = 1.0
        elif len(numbers) >= 2 and re.search(r"\d\s*/\s*\d", body):
            value = numbers[0] / numbers[1]  # 1f / 1.2f
        else:
            value = numbers[0]
        if value <= 0 or family in tile_floor_exempt:
            continue
        checked += 1
        tile = 1.0 / value
        floor_value = 0.999 if family in wave_families else 0.34
        if tile < floor_value:
            label = "VIS-035/093/094" if family in wave_families else "VIS-092"
            issues.append(f"{label}: triplanar family '{family}' repeats every {tile:.2f} m "
                          f"(< {floor_value:.2f} m floor)")
    rows.append(f"triplanar repeat checked for {checked} families "
                f"({len(wave_families)} re-tiled by this wave at >= 1 m, the rest at >= 0.35 m)")

status = "PASS" if not issues else "OPEN"
lines = [
    "# Painterly material contract verification",
    "",
    f"- Status: **{status}**",
    f"- Mode: static source contract (no engine, no import, no capture).",
    "",
]
lines += [f"- {row}" for row in rows]
if notes:
    lines += ["", "## Notes"] + [f"- {note}" for note in notes]
if issues:
    lines += ["", "## Issues"] + [f"- {issue}" for issue in issues]
else:
    lines += ["", "No contract violation found. Visible acceptance still requires the paired frames.", ""]

report = "\n".join(lines) + "\n"
print(report, end="")
if report_path:
    destination = Path(report_path)
    if not destination.is_absolute():
        destination = Path.cwd() / destination
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(report, encoding="utf-8")

raise SystemExit(0 if status == "PASS" else 2)
CONTRACT_PY
    PY_STATUS=$?
    set -e
    exit "$PY_STATUS"
fi

PY_ARGS=(
    "$ROOT_DIR"
    "$REPORT_PATH"
    "$ALLOW_MISSING"
    "$MAX_MEAN_SEAM"
    "$MAX_MAX_SEAM"
    "$MAX_CLIP_FRACTION"
    "$MAX_HIGH_SAT_FRACTION"
    "$MAX_MEAN_SATURATION"
)
if ((${#INPUT_PATHS[@]} > 0)); then
    PY_ARGS+=("${INPUT_PATHS[@]}")
fi

set +e
"$PYTHON_BIN" - "${PY_ARGS[@]}" <<'PY'
import base64
import hashlib
import math
import os
import re
import struct
import sys
import zlib
from pathlib import Path

# The report contains U+00D7 and Cyrillic headings. On a Windows console the default
# cp1251 stdout raises UnicodeEncodeError after the gate has already decoded every
# candidate, so the stream is pinned to UTF-8 with replacement instead of trusting the
# locale. (The contract block below does the same.)
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass


root = Path(sys.argv[1]).resolve()
report_path = sys.argv[2]
allow_missing = bool(int(sys.argv[3]))
max_mean_seam = float(sys.argv[4])
max_max_seam = float(sys.argv[5])
max_clip_fraction = float(sys.argv[6])
max_high_sat_fraction = float(sys.argv[7])
max_mean_sat = float(sys.argv[8])
input_paths = [Path(value).expanduser() for value in sys.argv[9:]]

EXPECTED = {
    "weathered_wood_boards": ("wood", 3.2, 3.2),
    "damp_earth": ("earth", 2.0, 5.0),
    "aged_plaster": ("plaster", 2.6, 1.8),
    "pine_foliage": ("foliage", 2.2, 2.2),
    "mossy_stone": ("stone", 2.4, 2.4),
    "old_fabric": ("fabric", 3.0, 3.0),
}
NAME_RE = re.compile(r"^[a-z0-9]+(?:_[a-z0-9]+)*_v[23456]_albedo\.png$")
CATALOG_NAME_RE = re.compile(r"^(urman_[a-z][0-9]{2})_v[0-9]{2}_basecolor\.png$")
# Only integrated TILE consumers belong here. Unique sheets, UV atlases and
# alpha decals need their own acceptance, not an automatic seam PASS.
CATALOG_EXPECTED = {
    "urman_w05_v02_basecolor.png": ("wood_painted_trim", 2.0, 2.0),
    "urman_b07_v02_basecolor.png": ("stone_foundation", 1.0, 1.0),
    "urman_w02_v02_basecolor.png": ("wood_fence_vertical / wood_fence_rail / wood_fence_uv", 1.0, 1.0),
    # VIS-035: the wallpaper repeat doubled in the material owner (0.91 m -> 1.82 m per
    # tile) so the ornament stops reading as a grid across a 5 m wall.
    "wallpaper_old_v1_albedo.png": ("wallpaper (T12 reuse)", 0.55, -0.55),
    "urman_t04_v01_basecolor.png": ("cloth_curtain", 2.0, 2.0),
    "urman_b01_v01_basecolor.png": ("plaster_domestic", 1.0, 1.0),
    "urman_t01_v02_basecolor.png": ("cloth_table", 2.0, 2.0),
    "urman_s01_v01_basecolor.png": ("snow_ground", 0.5, 0.5),
    "urman_s02_v02_basecolor.png": ("snow_road", 0.5, 0.5),
    "urman_f01_v03_basecolor.png": ("bark_birch_winter", 1.0 / 0.75, 1.0 / 1.5),
    "urman_f02_v02_basecolor.png": ("bark_pine", 1.0, 1.0),
    "urman_w01_v01_basecolor.png": ("wood_facade", 1.0, 1.0),
    "urman_w03_v01_basecolor.png": ("wood_painted_blue", 1.0, 1.0),
    "urman_w04_v02_basecolor.png": ("wood_painted_green", 1.0, 1.0),
    "urman_w09_v01_basecolor.png": ("wood_floor_painted", 1.0, 1.0),
    # VIS-094: civic plaster tile raised from 1.0 m to 1.8 m per repeat.
    "urman_b03_v01_basecolor.png": ("wall_institution", 0.55, 0.55),
    "urman_b04_v01_basecolor.png": ("floor_institution", 1.0, 1.0),
    "urman_m05_v01_basecolor.png": ("plastic_abs", 2.0, 2.0),
    "urman_t10_v02_basecolor.png": ("cloth_clinic", 2.0, 2.0),
    "urman_t08_v01_basecolor.png": ("fabric_upholstery", 2.0, 2.0),
    # VIS-035: the hero-room table moved from a 0.75 m to a 1.2 m tile so the pair
    # table/wallpaper reads at one scale instead of two competing ones.
    "urman_w08_v01_basecolor.png": ("wood_furniture_interior", 1.0 / 1.2, 1.0 / 1.2),
}
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def markdown_escape(value):
    return str(value).replace("|", "\\|").replace("\n", " ")


def normal_path(path):
    if not path.is_absolute():
        path = Path.cwd() / path
    return path.resolve()


def read_png(path):
    """Decode non-interlaced 8-bit RGB/RGBA PNGs using stdlib only."""
    payload = path.read_bytes()
    if not payload.startswith(PNG_SIGNATURE):
        raise ValueError("invalid PNG signature")
    offset = len(PNG_SIGNATURE)
    width = height = bit_depth = color_type = interlace = None
    idat = []
    saw_iend = False
    chunks = []
    while offset < len(payload):
        if offset + 12 > len(payload):
            raise ValueError("truncated PNG chunk")
        size = struct.unpack(">I", payload[offset : offset + 4])[0]
        kind = payload[offset + 4 : offset + 8]
        start = offset + 8
        end = start + size
        if end + 4 > len(payload):
            raise ValueError("truncated PNG chunk payload")
        body = payload[start:end]
        crc_expected = struct.unpack(">I", payload[end : end + 4])[0]
        crc_actual = zlib.crc32(kind)
        crc_actual = zlib.crc32(body, crc_actual) & 0xFFFFFFFF
        if crc_actual != crc_expected:
            raise ValueError(f"CRC mismatch in {kind.decode('latin1')}")
        chunks.append(kind.decode("latin1"))
        if kind == b"IHDR":
            if len(body) != 13:
                raise ValueError("invalid IHDR")
            width, height, bit_depth, color_type, compression, filtering, interlace = struct.unpack(
                ">IIBBBBB", body
            )
            if compression != 0 or filtering != 0:
                raise ValueError("unsupported PNG compression/filter method")
        elif kind == b"IDAT":
            idat.append(body)
        elif kind == b"IEND":
            saw_iend = True
            break
        offset = end + 4
    if width is None or height is None:
        raise ValueError("PNG has no IHDR")
    if not saw_iend:
        raise ValueError("PNG has no IEND")
    if bit_depth not in (8,) or color_type not in (2, 6):
        raise ValueError(
            f"requires 8-bit RGB/RGBA (bit_depth={bit_depth}, color_type={color_type})"
        )
    if interlace != 0:
        raise ValueError("interlaced PNG is not accepted by deterministic gate")
    if not idat:
        raise ValueError("PNG has no IDAT")
    channels = 3 if color_type == 2 else 4
    row_bytes = width * channels
    compressed = b"".join(idat)
    raw = zlib.decompress(compressed)
    expected_size = height * (row_bytes + 1)
    if len(raw) != expected_size:
        raise ValueError(f"decoded scanline size {len(raw)} != expected {expected_size}")

    rows = []
    cursor = 0
    previous = bytearray(row_bytes)
    for _ in range(height):
        filter_type = raw[cursor]
        cursor += 1
        encoded = raw[cursor : cursor + row_bytes]
        cursor += row_bytes
        decoded = bytearray(row_bytes)
        for index, value in enumerate(encoded):
            left = decoded[index - channels] if index >= channels else 0
            up = previous[index]
            up_left = previous[index - channels] if index >= channels else 0
            if filter_type == 0:
                result = value
            elif filter_type == 1:
                result = (value + left) & 0xFF
            elif filter_type == 2:
                result = (value + up) & 0xFF
            elif filter_type == 3:
                result = (value + ((left + up) // 2)) & 0xFF
            elif filter_type == 4:
                predictor = left + up - up_left
                distance_left = abs(predictor - left)
                distance_up = abs(predictor - up)
                distance_up_left = abs(predictor - up_left)
                if distance_left <= distance_up and distance_left <= distance_up_left:
                    nearest = left
                elif distance_up <= distance_up_left:
                    nearest = up
                else:
                    nearest = up_left
                result = (value + nearest) & 0xFF
            else:
                raise ValueError(f"unsupported PNG filter type {filter_type}")
            decoded[index] = result
        rows.append(decoded)
        previous = decoded
    return width, height, channels, rows, chunks


def hsv_saturation(r, g, b):
    high = max(r, g, b)
    low = min(r, g, b)
    return 0.0 if high == 0 else (high - low) / high


def analyse(path):
    result = {
        "path": path,
        "name": path.name,
        "status": "PASS",
        "issues": [],
    }
    catalog_match = CATALOG_NAME_RE.fullmatch(path.name)
    mapped_tile = path.name in CATALOG_EXPECTED
    if not NAME_RE.fullmatch(path.name) and catalog_match is None and not mapped_tile:
        result["issues"].append("name is not a supported legacy albedo or catalogue TILE candidate")
    if not path.exists():
        result["status"] = "OPEN"
        result["issues"].append("file is missing")
        return result
    if path.suffix.lower() != ".png":
        result["status"] = "OPEN"
        result["issues"].append("file extension is not .png")
        return result
    result["sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
    try:
        width, height, channels, rows, chunks = read_png(path)
    except Exception as error:
        result["status"] = "OPEN"
        result["issues"].append(str(error))
        return result
    result.update({"width": width, "height": height, "channels": channels, "color": "RGB" if channels == 3 else "RGBA"})
    result["chunks"] = ",".join(chunks)
    if catalog_match is not None or mapped_tile:
        if width != height or not 512 <= width <= 2048:
            result["issues"].append(f"size {width}x{height}; catalogue TILE master must be square, 512–2048 px")
        if channels != 3:
            result["issues"].append("catalogue base-color TILE requires opaque RGB; alpha needs separate acceptance")
    elif (width, height) != (1024, 1024):
        result["issues"].append(f"size {width}x{height}; expected 1024x1024")
    # Catalogue versions have explicit intended consumers; legacy scales are
    # historical comparison scales. This is a source-image gate, not runtime QA.
    stem = re.sub(r"_v[23456]_albedo\.png$", "", path.name)
    material = CATALOG_EXPECTED.get(path.name) if catalog_match or mapped_tile else EXPECTED.get(stem)
    if material is None:
        result["issues"].append("no PainterlyMaterialLibrary material/scale mapping")
    else:
        result["surface"], scale_x, scale_y = material
        result["scale_x"] = scale_x
        result["scale_y"] = scale_y
        result["texels_per_world_x"] = width * abs(scale_x)
        result["texels_per_world_y"] = height * abs(scale_y)
        # The candidate keeps the existing scale; no shader edit is required.
        if width <= 0 or height <= 0:
            result["issues"].append("non-positive dimensions cannot provide texel density")

    total_values = width * height * channels
    clipped_low = clipped_high = 0
    sat_sum = 0.0
    high_sat = 0
    vertical_sum = vertical_max = 0
    horizontal_sum = horizontal_max = 0
    vertical_count = height * channels
    horizontal_count = width * channels
    # Opposite-edge seam: mean/max normalized absolute channel delta in [0, 1].
    for y, row in enumerate(rows):
        for x in range(0, width * channels, channels):
            pixels = row[x : x + channels]
            rgb = pixels[:3]
            for value in pixels:
                clipped_low += value == 0
                clipped_high += value == 255
            saturation = hsv_saturation(*rgb)
            sat_sum += saturation
            high_sat += saturation > 0.90
            if x == 0:
                opposite = row[(width - 1) * channels : width * channels]
                for left_value, right_value in zip(pixels, opposite):
                    delta = abs(left_value - right_value)
                    vertical_sum += delta
                    vertical_max = max(vertical_max, delta)
        first = row[0:channels]
        last = row[(width - 1) * channels : width * channels]
        # Store horizontal seam values on the first/last scanline below.
        if y == 0:
            top = row
        if y == height - 1:
            bottom = row
    for top_value, bottom_value in zip(top, bottom):
        delta = abs(top_value - bottom_value)
        horizontal_sum += delta
        horizontal_max = max(horizontal_max, delta)

    result.update(
        {
            "clip_low": clipped_low / total_values,
            "clip_high": clipped_high / total_values,
            "mean_saturation": sat_sum / (width * height),
            "high_saturation": high_sat / (width * height),
            "seam_vertical_mean": vertical_sum / (vertical_count * 255.0),
            "seam_vertical_max": vertical_max / 255.0,
            "seam_horizontal_mean": horizontal_sum / (horizontal_count * 255.0),
            "seam_horizontal_max": horizontal_max / 255.0,
        }
    )
    if result["clip_low"] > max_clip_fraction or result["clip_high"] > max_clip_fraction:
        result["issues"].append(
            f"clipping fraction low={result['clip_low']:.4f} high={result['clip_high']:.4f} > {max_clip_fraction:.4f}"
        )
    if result["high_saturation"] > max_high_sat_fraction or result["mean_saturation"] > max_mean_sat:
        result["issues"].append(
            f"saturation high={result['high_saturation']:.4f} mean={result['mean_saturation']:.4f} exceeds thresholds"
        )
    seam_means = (result["seam_vertical_mean"], result["seam_horizontal_mean"])
    seam_maxes = (result["seam_vertical_max"], result["seam_horizontal_max"])
    if max(seam_means) > max_mean_seam or max(seam_maxes) > max_max_seam:
        result["issues"].append(
            f"edge seam mean={max(seam_means):.4f} max={max(seam_maxes):.4f} exceeds thresholds"
        )
    if result["issues"]:
        result["status"] = "OPEN"
    return result


results = [analyse(normal_path(path)) for path in input_paths]
if not results:
    status = "OPEN"
    summary = "No supported versioned painterly candidates found; waiting for generation agent."
else:
    status = "PASS" if all(result["status"] == "PASS" for result in results) else "OPEN"
    summary = f"{sum(result['status'] == 'PASS' for result in results)}/{len(results)} candidate(s) PASS."

lines = [
    "# Painterly texture candidate verification",
    "",
    f"- Status: **{status}**",
    f"- Summary: {summary}",
    f"- Gate: legacy 1 024 × 1 024 RGB/RGBA; mapped catalogue TILE masters square 512–2048 RGB; 8-bit non-indexed/non-gray, opposite-edge seam mean ≤ {max_mean_seam:g} and max ≤ {max_max_seam:g}, clipping fraction ≤ {max_clip_fraction:g}, HSV saturation mean ≤ {max_mean_sat:g} and high-saturation fraction ≤ {max_high_sat_fraction:g}.",
    "- Source-only density: exact catalogue filenames use declared consumer scales; legacy scales are historical previews. Texels/world below are source pixels, before import size caps. PASS does not prove runtime binding, imported density or art acceptance.",
    "",
]
if results:
    lines += [
        "| File | Status | Size/mode | Seam mean V/H | Seam max V/H | Clip low/high | Sat mean/high | Scale | Source texels/world | SHA-256 |",
        "|---|---|---|---:|---:|---:|---:|---|---:|---|",
    ]
    for result in results:
        issues = "; ".join(result["issues"]) if result["issues"] else "—"
        if "width" in result:
            size_mode = f"{result['width']}×{result['height']} {result['color']}"
            seam_mean = f"{result['seam_vertical_mean']:.4f}/{result['seam_horizontal_mean']:.4f}"
            seam_max = f"{result['seam_vertical_max']:.4f}/{result['seam_horizontal_max']:.4f}"
            clipping = f"{result['clip_low']:.4f}/{result['clip_high']:.4f}"
            saturation = f"{result['mean_saturation']:.4f}/{result['high_saturation']:.4f}"
            scale = f"{result.get('scale_x', '—')}×{result.get('scale_y', '—')}"
            texels = f"{result.get('texels_per_world_x', '—')}×{result.get('texels_per_world_y', '—')}"
        else:
            size_mode = seam_mean = seam_max = clipping = saturation = scale = texels = "—"
        lines.append(
            f"| `{markdown_escape(result['path'])}` | **{result['status']}** | {size_mode} | {seam_mean} | {seam_max} | {clipping} | {saturation} | {scale} | {texels} | `{result.get('sha256', '—')}` |"
        )
        if issues != "—":
            lines.append(f"| ↳ issue | {markdown_escape(issues)} |  |  |  |  |  |  |  |  |")
else:
    lines += [
        "No candidate files were present when this gate ran. Generation must produce supported versioned files named `*_v2_albedo.png` through `*_v6_albedo.png`; rerun the same command afterwards.",
    ]

report = "\n".join(lines) + "\n"
print(report, end="")
if report_path:
    destination = normal_path(Path(report_path))
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(report, encoding="utf-8")

if status == "PASS" or (status == "OPEN" and not results and allow_missing):
    raise SystemExit(0)
raise SystemExit(2)
PY
PY_STATUS=$?
set -e
exit "$PY_STATUS"
