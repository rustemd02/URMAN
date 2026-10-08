import sys, re, collections

raw = sys.stdin.read()
prefix = "C:" + chr(92) + "Users" + chr(92) + "ruste" + chr(92) + "Documents" + chr(92) + "GitHub" + chr(92) + "URMAN" + chr(92)
out, seen = [], set()
for line in raw.splitlines():
    line = line.strip()
    if ": error CS" not in line:
        continue
    line = line.replace(prefix, "")
    line = re.sub(r"^\[[^\]]+\]$", "", line)
    key = line.split("] ")[-1] if "]" in line else line
    if line in seen:
        continue
    seen.add(line)
    out.append(line)

with open("docs/production/visual_restyle_2026-10-07/build_errors_round1.txt", "w", encoding="utf-8") as fh:
    fh.write("\n".join(out) + "\n")

counts = collections.Counter(re.split(r"\(", o)[0] for o in out)
for f, n in counts.most_common():
    print(f"{n:3d}  {f}")
print("TOTAL UNIQUE:", len(out))
