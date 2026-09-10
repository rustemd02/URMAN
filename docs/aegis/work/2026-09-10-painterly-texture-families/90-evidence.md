# Evidence

- 19 final PNGs: 1 024 × 1 024, RGB, SHA-256 recorded in the painterly README.
- Seam review: 19 2×2 mosaics plus `overview.png`, `tile_review.png` and
  `seam_metrics.tsv`; delivered edge MAE is at or below 9.852/255.
- Runtime visual review: six before/after pairs and one contact sheet; roofs,
  birch bark/leaves, house interiors and FAP wall read as distinct surfaces;
  cultural decoration stays locally scoped. Human moving art review remains.
- Build: `0 warnings / 0 errors`.
- Physical walkthrough: `PASS`, `335.27 m`, final zone `kara_urman_night`.
- Capture: `48/48` unique PNGs; 10 waypoints, 242.50 horizontal metres;
  `164` shader materials, `159` textured, `0` low-quality.
- Benchmark: 119.90–120.18 FPS across day street, old-PC interior and Kara
  night; 30 FPS floor `PASS`.
- Modular-kit material contract: focused smoke `PASS` after updating the
  existing expected owners for the intentionally textured HouseA/PineA paths.
- Aggregate verification: `./eng/verify-godot.sh` exit 0 after the focused
  HouseA/PineA/full-game material-owner expectations were rebound.
- Graph: `graphify update .` exit 0, 10,980 nodes / 15,014 edges / 764
  communities; HTML visualization skipped by Graphify's 5,000-node limit.
- Final hygiene: all 19 README hashes match delivered PNGs; all files are
  1 024 × 1 024 RGB and have Godot imports; 19 tile receipts are present;
  `git diff --check` exit 0.
