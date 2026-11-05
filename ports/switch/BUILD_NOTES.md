# URMAN Switch build progress — 2026-11-05

First Linux CI run 42014867867 built coreclr_static successfully, then failed at managed-framework restore because host build tasks pulled System.Security.Cryptography.Pkcs 9.0.6. The pinned build-only patch directly references 9.0.21 in Crossgen2Tasks and installer.tasks; NuGet auditing stays enabled. See GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx.

The next run builds runtime, engine, matching Mono editor and complete PhotoWorlds game payload. The source patch preserves the local police-art changes and adds Nintendo controls/tutorial/haptics/loading. Generated artwork stays local and is combined at final SD packaging. The Switch loader reads the 16 hash-pinned PNGs from /switch/urman/art. Desktop C# compilation passed with zero warnings/errors after the loader change; 14 package tests passed.

No working Switch game package or on-device acceptance is claimed. Main is unchanged. This scoped log substitutes for the 1.8 MB shared journal, whose full update exceeded automatic approval review input limits.
Native controller follow-up: implement libnx rumble from Godot strengths with finite-time stop, style/connection refresh and focus/exit/resume cancellation; add focus notifications and neutral-input resync after HOME/resume. Physical rumble and suspend/resume remain hardware-unverified. Fix the export-hook System.Environment qualification and stream/export detailed diagnostics. Engine patch hash is updated; local whitespace and Python syntax checks passed.
