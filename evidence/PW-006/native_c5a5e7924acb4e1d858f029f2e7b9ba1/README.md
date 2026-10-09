# PW-006 full-game-flow smoke

- Protected native Windows job `c5a5e7924acb4e1d858f029f2e7b9ba1`, snapshot `0b53fa3cf3dce4788412eb27a67e87b0114bc644948a617bf5b3e418c030310a`, sealed from base commit `f2380e63bd5eabcb27ebdd9e5d8ef103f009560b`.
- Result: PASS, exit 0; Godot 4.7.1, .NET SDK 10.0.302, interactive Vulkan/GTX 970. Narrow compile and content/import/game steps all exited 0; elapsed 129.67 s.
- `game.log` reports the existing semantic full-game flow assertions and the SaveGameV3 isolation/disk round-trip PASS. It explicitly reports `PHYSICAL NOT_RUN` for file/search and printer consumers, the ordinary route, handover/anchors/art. Human playtest is not run.
- The expected smoke log includes warnings from its deliberate duplicate-occurrence and blocked-condition assertions; station `engine-errors.log` is empty. Do not interpret this run as general warning-free play or art acceptance.
- Userdata guard restored the project and user files; post-run doctor reports `ready=true`, no busy job, no orphan game process, and no pending recovery.
- `source_snapshot/` preserves the exact selected sources, scene, compiled full-game campaign resource, pre-run PW-006 task receipt, request and their source proof. `receipt.json` is the full native station receipt; `request.json` records the submitted snapshot.
- `result.zip` is kept locally as fetched raw transport evidence and ignored for commits; extracted logs remain available individually.

`.gitattributes` disables text normalization. `archive-sha256.json` indexes every archive file except itself.
