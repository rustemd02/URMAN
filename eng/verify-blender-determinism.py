#!/usr/bin/env python3
"""VIS-108 static gate: Blender generators draw randomness only from seeded generators.

Module-level random.random()/uniform()/choice()/randint()/shuffle() and numpy's
global RNG depend on process state, so the same input could give another GLB.
Generators must use random.Random(<stable seed>) or numpy default_rng(<seed>).
"""
import ast
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SEEDED_CONSTRUCTORS = {"random.Random", "numpy.random.default_rng", "numpy.random.RandomState"}

failures = []
files = sorted(list((ROOT / "tools/blender").rglob("*.py")) + list((ROOT / "assets/source/blender").rglob("*.py")))
for path in files:
    source = path.read_text(encoding="utf-8")
    try:
        tree = ast.parse(source, filename=str(path))
    except SyntaxError as exc:
        failures.append(f"{path.relative_to(ROOT)}:{exc.lineno}: invalid Python: {exc.msg}")
        continue
    aliases = {}
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            for alias in node.names:
                aliases[alias.asname or alias.name.split(".")[0]] = alias.name if alias.asname else alias.name.split(".")[0]
        elif isinstance(node, ast.ImportFrom) and node.module:
            for alias in node.names:
                if alias.name == "*" and node.module in ("random", "numpy", "numpy.random"):
                    failures.append(f"{path.relative_to(ROOT)}:{node.lineno}: wildcard RNG import cannot be audited")
                else:
                    aliases[alias.asname or alias.name] = node.module + "." + alias.name

    def qualified(node):
        if isinstance(node, ast.Name):
            return aliases.get(node.id, node.id)
        if isinstance(node, ast.Attribute):
            parent = qualified(node.value)
            return parent + "." + node.attr if parent else ""
        return ""

    for node in ast.walk(tree):
        if not isinstance(node, ast.Call):
            continue
        name = qualified(node.func)
        reason = None
        if name in SEEDED_CONSTRUCTORS:
            seed = node.args[0] if node.args else next((kw.value for kw in node.keywords if kw.arg in ("seed", "x")), None)
            if seed is None or isinstance(seed, ast.Constant) and seed.value is None or isinstance(seed, ast.Starred):
                reason = "RNG constructor needs an explicit seed"
        elif name == "random.SystemRandom":
            reason = "SystemRandom is nondeterministic even with a seed"
        elif name.startswith("random.") or name.startswith("numpy.random."):
            reason = "global RNG call: use an explicitly seeded private generator"
        if reason:
            failures.append(f"{path.relative_to(ROOT)}:{node.lineno}: {reason}: {ast.get_source_segment(source, node)}")
if failures:
    print("FAIL: unseeded randomness in Blender generators")
    print("\n".join(" - " + f for f in failures))
    sys.exit(1)
print(f"PASS: {len(files)} generator scripts have no detected global RNG calls or unseeded constructors (AST audit; stable seed expressions still require review)")
