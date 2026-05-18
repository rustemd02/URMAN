# Baseline Governance

## 1. Architecture Defect

A confirmed error, gap, or contradiction in the baseline itself.

- Fix baseline first, then align implementation to corrected baseline.
- Do not patch implementation around a defective baseline.

## 2. Architecture Drift

Implementation has deviated from a confirmed, correct baseline.

- Return to baseline via the simplest path.
- Do not update baseline to match drift without explicit review.

## 3. Baseline Check Protocol

Before non-trivial changes:

1. Read the latest baseline snapshot in `baseline/`.
2. Compare current code structure against ownership map.
3. Compare current contracts against contract inventory.
4. Check for new anti-patterns not recorded in known list.
5. Report: aligned / minor drift / material drift.

## 4. Architecture Review

After each non-trivial change, check:

1. Ownership integrity.
2. Module boundaries.
3. Contract changes.
4. Cascade proliferation.
5. Dependency direction.
6. Retirement completeness.
7. Entropy flow.

## 5. Hard Boundaries

- `BASELINE-GOVERNANCE.md` is governance for this Aegis workspace.
- Baseline snapshots are evidence, not game canon.
- ADRs record decisions; they do not replace project knowledge base.
- This file is not auto-updated without explicit review.
