export const renderPlayer = () => `
    <div class="player-app" style="height: 100%; background: #222; color: #0f0; font-family: monospace; padding: 10px; display: flex; flex-direction: column;">
        <div style="border: 1px solid #0f0; padding: 5px; margin-bottom: 10px;">
            <div style="font-size: 10px;">WINAMP 2.81</div>
            <div style="font-size: 14px; overflow: hidden; white-space: nowrap;">01. Urman_Ambient_Noise.mp3</div>
        </div>
        <div style="display: flex; gap: 5px; justify-content: center;">
            <button style="background: #444; color: #fff; border: 1px solid #888; width: 30px;">◀◀</button>
            <button style="background: #444; color: #fff; border: 1px solid #888; width: 30px;">▶</button>
            <button style="background: #444; color: #fff; border: 1px solid #888; width: 30px;">■</button>
            <button style="background: #444; color: #fff; border: 1px solid #888; width: 30px;">▶▶</button>
        </div>
        <div style="margin-top: 10px; font-size: 9px; color: #888;">
            * Слышишь? Это не ветер.
        </div>
    </div>
`;
