#!/usr/bin/env bash
set -euo pipefail

# Deterministic, runtime-independent gate for versioned painterly albedo candidates.
# The embedded Python decoder deliberately uses only the standard library so this
# check can run before Godot/Blender and does not alter the material/shader path.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEFAULT_DIR="$ROOT_DIR/game/assets/textures/painterly"
REPORT_PATH=""
ALLOW_MISSING=0
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

if ((${#INPUT_PATHS[@]} == 0)); then
    if [[ -d "$DISCOVERY_DIR" ]]; then
        while IFS= read -r candidate; do
            INPUT_PATHS+=("$candidate")
        done < <(find "$DISCOVERY_DIR" -maxdepth 1 -type f \( -name '*_v2_albedo.png' -o -name '*_v3_albedo.png' -o -name '*_v4_albedo.png' -o -name '*_v5_albedo.png' -o -name '*_v6_albedo.png' -o -name 'urman_*_v??_basecolor.png' \) -print | LC_ALL=C sort)
    fi
fi

PYTHON_BIN="${PYTHON_BIN:-python3}"

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
    "urman_w02_v02_basecolor.png": ("wood_fence_vertical / wood_fence_rail", 1.0, 1.0),
    "wallpaper_old_v1_albedo.png": ("wallpaper (T12 reuse)", 1.1, -1.1),
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
    "urman_b03_v01_basecolor.png": ("wall_institution", 1.0, 1.0),
    "urman_b04_v01_basecolor.png": ("floor_institution", 1.0, 1.0),
    "urman_m05_v01_basecolor.png": ("plastic_abs", 2.0, 2.0),
    "urman_t10_v02_basecolor.png": ("cloth_clinic", 2.0, 2.0),
    "urman_t08_v01_basecolor.png": ("fabric_upholstery", 2.0, 2.0),
    "urman_w08_v01_basecolor.png": ("wood_furniture_interior", 1.0 / 0.75, 1.0 / 0.75),
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
