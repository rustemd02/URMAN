export const renderTerminal = () => `
    <div class="terminal-app" style="
        position: relative;
        height: 100%;
        background:
            radial-gradient(circle at center, rgba(20,40,20,0.18) 0%, rgba(0,0,0,0) 55%),
            linear-gradient(to bottom, rgba(255,255,255,0.03) 0%, rgba(255,255,255,0.00) 100%),
            #020402;
        color: #7CFF7A;
        font-family: 'Courier New', monospace;
        padding: 14px;
        overflow-y: auto;
        text-shadow: 0 0 4px rgba(124,255,122,0.45);
        box-sizing: border-box;
    ">
        <div style="
            position:absolute;
            inset:0;
            pointer-events:none;
            background:
                repeating-linear-gradient(
                    to bottom,
                    rgba(255,255,255,0.03) 0px,
                    rgba(255,255,255,0.03) 1px,
                    rgba(0,0,0,0) 2px,
                    rgba(0,0,0,0) 4px
                );
            opacity:0.18;
        "></div>

        <div style="position:relative; z-index:1;">
            <div style="color:#b8ffb6; margin-bottom:8px;">URMAN/DOS v0.9.3 [build 14.10.1998]</div>
            <div style="color:#6cffc9;">KARA-URMAN LOCAL NODE / RECOVERY CONSOLE</div>
            <div style="margin:10px 0; color:#4eff4a;">====================================================</div>

            <div>C:\\> boot /diagnostic /ritual-layer=false</div>
            <div>Loading village sector map...</div>
            <div>[OK] Forest boundary mesh</div>
            <div>[OK] North river spline</div>
            <div>[OK] Bridge access node</div>
            <div>[OK] Household registry: 15 structures found</div>
            <div>[WARN] 2 structures have no valid ownership record</div>
            <div>[WARN] Audio source detected near sector N-13</div>
            <div>[FAIL] Urman_Core checksum mismatch</div>
            <div style="color:#ffd54a;">NOTICE: Memory fragments were moved to quarantine.</div>
            <div style="color:#ffd54a;">NOTICE: Civilian profile 'ALSU' is hidden by operator policy.</div>
            <div style="color:#ff6b6b;">ALERT: Unauthorized presence detected beyond northern bridge.</div>

            <div style="margin-top:10px;">C:\\> dir /b /s c:\\urman\\logs\\</div>
            <div>night_watch_01.log</div>
            <div>bridge_noise_02.log</div>
            <div>household_registry.old</div>
            <div>forest_edge_report.corrupt</div>
            <div>alsu_profile.locked</div>

            <div style="margin-top:10px;">C:\\> type forest_edge_report.corrupt</div>
            <div>...tree line expanded by 3 meters...</div>
            <div>...fog density above expected norm...</div>
            <div style="color:#ff6b6b;">...someone answered from the other side...</div>

            <div style="margin-top:12px; display:flex; align-items:center; gap:6px;">
                <span>C:\\></span>
                <span style="opacity:0.95;">_</span>
            </div>
        </div>
    </div>
`;