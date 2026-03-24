export const renderTerminal = () => `
    <div class="terminal-app" style="height: 100%; background: #000; color: #0f0; font-family: 'Courier New', monospace; padding: 10px; overflow-y: auto;">
        <div>C:\\> system_check.exe</div>
        <div>Scanning local drives...</div>
        <div style="color: yellow;">WARNING: Sector 666 corrupted.</div>
        <div style="color: yellow;">WARNING: Unauthorized presence detected in 'Urman_Core'.</div>
        <div>C:\\> _</div>
    </div>
`;
