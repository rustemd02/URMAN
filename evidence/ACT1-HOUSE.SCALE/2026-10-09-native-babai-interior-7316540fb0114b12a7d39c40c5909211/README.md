# Babai house interior diagnostic capture

- Job `7316540fb0114b12a7d39c40c5909211`; snapshot `cce551b187b11d4e9b5cb372cfae956cd256777fd61df252e1ead97728beba67`; result **PASS** in native windowed `res://scenes/act1_demo.tscn` on UNTERPC, Godot 4.7.1.stable.mono.official.a13da4feb, .NET 10.0.302; result archive SHA-256 `fefa3c7172e195c226617decc9839a7625164174dbc51db56dd355bb64395e39`.
- Requested `babai_entry_door` and `babai_room_overview`, explicit FOV 70, timeout 300. The captured visual checkpoint manifest had 20 rows. Both sidecars record `requestedZoneId=house_old_pc` and `activeZoneId=house_old_pc`; atmosphere source is recorded in each sidecar. No zone/scope environment overrides were sent.
- Original PNGs and JSON sidecars are under `frames/`. This is visual diagnostic evidence, not an art acceptance.
- `source_snapshot/` contains exact byte copies of 27 selected runtime/zone/manifest/asset/runner files and the complete captured 1820-file station manifest; each selected copy was verified against it.
- Guard reports `userdataRestored=True`. Post-run doctor reports ready, no active job, no orphan game process, and no pending recovery.

Fetched station logs and `result.zip` are retained byte-for-byte. `.gitattributes` disables text normalization. `archive-sha256.json` records all archive files except itself.
