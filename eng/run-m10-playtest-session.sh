#!/bin/sh

set -eu

# M10 session kit: launch the exported candidate under the userdata guard and
# leave the observer a frame trail plus an answer sheet. It never plays the game
# itself and never fabricates answers; a human sits at the keyboard.

URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$URMAN_ROOT"

if [ "$#" -ne 1 ]; then
  echo "usage: $0 <output-directory-outside-repository>" >&2
  exit 2
fi

# Candidate resolution: an explicit URMAN_M10_CANDIDATE wins, otherwise take the
# newest exported candidate so this kit cannot rot into pointing at a build that
# has been superseded and deleted. Candidate directories live next to the guard.
EVIDENCE_ROOT=${URMAN_M10_EVIDENCE_ROOT:-/Users/unterlantas/Documents/URMAN_ActI_Finish_20260911}
GUARD=${URMAN_GUARD:-$EVIDENCE_ROOT/protected_run.py}
if [ -n "${URMAN_M10_CANDIDATE:-}" ]; then
  CANDIDATE=$URMAN_M10_CANDIDATE
else
  CANDIDATE=
  for dir in $(ls -d "$EVIDENCE_ROOT"/native_*_candidate 2>/dev/null | sort -r); do
    if [ -x "$dir/launch/URMAN.app/Contents/MacOS/URMAN" ]; then CANDIDATE=$dir; break; fi
  done
  [ -n "$CANDIDATE" ] || {
    echo "$0: no unpacked candidate found under $EVIDENCE_ROOT" >&2
    echo "$0: export one with eng/export-desktop-release.sh and unpack macos/URMAN.zip into <dir>/launch/" >&2
    echo "$0: or pass URMAN_M10_CANDIDATE=<dir>" >&2
    exit 1
  }
fi
BINARY="$CANDIDATE/launch/URMAN.app/Contents/MacOS/URMAN"
INTERVAL=${URMAN_M10_SHOT_SECONDS:-20}
MAX_MINUTES=${URMAN_M10_MAX_MINUTES:-90}

echo "act1-m10-session: candidate=$CANDIDATE"
[ -x "$BINARY" ] || { echo "candidate binary not found: $BINARY" >&2; exit 1; }
[ -f "$GUARD" ] || { echo "userdata guard not found: $GUARD" >&2; exit 1; }
command -v screencapture >/dev/null 2>&1 || { echo "screencapture is required" >&2; exit 1; }

OUTPUT_INPUT=$1
mkdir -p "$OUTPUT_INPUT"
OUTPUT_DIR=$(CDPATH= cd -- "$OUTPUT_INPUT" && pwd -P)
case "$OUTPUT_DIR" in
  "$URMAN_ROOT"|"$URMAN_ROOT"/*) echo "output must be outside the repository: $OUTPUT_DIR" >&2; exit 1 ;;
esac
[ "$OUTPUT_DIR" != "/" ] || { echo "refusing to use the filesystem root" >&2; exit 1; }

STAMP=$(date +%Y%m%d-%H%M%S)
mkdir -p "$OUTPUT_DIR/frames"
QUESTIONS="$OUTPUT_DIR/session_notes_$STAMP.md"

cat >"$QUESTIONS" <<'MD'
# M10 session — ответы заполняет человек

Протокол: `docs/production/act1_m10_handoff_package_2026-09-14.md`, раздел 8.
Чистое состояние истории: начать с «Новая игра» (она сбрасывает прогресс и
сохраняет настройки). Подсказок не давать, играть вслух.

Все девять пунктов протокола, чтобы ответы ложились в раздел 8 один к одному:
вопросы 1–8 задаются в указанный момент, пункт 9 — наблюдение за весь сеанс.

| # | Когда | Что спросить / на что смотреть | Ответ дословно | Время / место |
|---|---|---|---|---|
| 1 | первые 2 минуты | Куда ты приехал и к кому? | | |
| 2 | после первой находки во дворе | Что ты нашёл и почему тебе это важно? | | |
| 3 | после первого разговора с Мансуром | Что ты решил сделать и почему он не сделал это сам? | | |
| 4 | после журнала | Что теперь кажется странным? | | |
| 5 | конец первых 8–12 минут | Что ты хочешь проверить дальше и где? | | |
| 6 | любой момент | Покажи, как открыл журнал и что искал | | |
| 7 | финал | Что произошло и что тебе запретили? | | |
| 8 | финал | Ты вернулся в меню — продолжил бы играть? | | |
| 9 | весь сеанс | Где застрял, где смеялся или испугался, что перечитывал | | |

Дополнительно:

- длительность сеанса;
- что хотел сделать, но игра не позволила;
- произношение татарских слов (что прозвучало неверно);
- кадры-подсказки лежат в `frames/` с отметкой времени.

Результат переносится в `docs/urman_knowledge_base/playtest_plan.md`; при
противоречии канона — также в `open_questions.md`.
MD

echo "session output: $OUTPUT_DIR"
echo "answer sheet:   $QUESTIONS"
echo "frames every ${INTERVAL}s, hard stop after ${MAX_MINUTES} minutes"
echo "starting the candidate under the userdata guard; the guard restores saves and settings afterwards"

python3 "$GUARD" "$BINARY" >"$OUTPUT_DIR/game.log" 2>&1 &
GUARD_PID=$!
sleep 8

deadline=$((MAX_MINUTES * 60))
elapsed=0
while kill -0 "$GUARD_PID" 2>/dev/null && [ "$elapsed" -lt "$deadline" ]; do
  name=$(date +%03dm%02ds | tr -d ' ')
  screencapture -x "$OUTPUT_DIR/frames/frame_${name}.png" >/dev/null 2>&1 || true
  sleep "$INTERVAL"
  elapsed=$((elapsed + INTERVAL))
done

if kill -0 "$GUARD_PID" 2>/dev/null; then
  echo "time limit reached; stopping the session"
  kill "$GUARD_PID" 2>/dev/null || true
fi
wait "$GUARD_PID" 2>/dev/null || true

# Safety net: if the guard exited without taking the game down, stop the exact
# candidate binary so no instance keeps writing userdata outside the guard.
for pid in $(pgrep -f "$BINARY" 2>/dev/null || true); do
  echo "stopping leftover candidate process $pid"
  kill "$pid" 2>/dev/null || true
done

frames=$(find "$OUTPUT_DIR/frames" -type f -name '*.png' | wc -l | tr -d ' ')
echo "session finished: $frames frames in $OUTPUT_DIR/frames"
echo "if the guard printed 'userdata restored byte-for-byte', saves and settings are back to their prior state"
