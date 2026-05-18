import { Game } from '../game/Game';

export class AudioDebugPanel {
    private game: Game;
    private element: HTMLElement;

    constructor(game: Game) {
        this.game = game;
        this.element = document.createElement('div');
        this.element.id = 'audio-debug-panel';
        this.element.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            z-index: 11000;
            background: rgba(0, 0, 0, 0.85);
            border: 2px solid #ff4d4d;
            border-radius: 8px;
            padding: 15px;
            color: #ff4d4d;
            font-family: 'Courier New', Courier, monospace;
            font-size: 11px;
            display: flex;
            flex-direction: column;
            gap: 10px;
            box-shadow: 0 0 20px rgba(255, 77, 77, 0.3);
            pointer-events: auto;
        `;

        this.element.innerHTML = `
            <div style="font-weight:bold; border-bottom:1px solid #ff4d4d; padding-bottom:5px; margin-bottom:5px; display:flex; justify-content:space-between;">
                <span>🔊 AUDIO DEBUG</span>
                <span id="ad-close" style="cursor:pointer;">[X]</span>
            </div>
            <button id="ad-wind-on" style="background:#222; border:1px solid #ff4d4d; color:white; padding:5px; cursor:pointer;">Ветер: ВКЛ</button>
            <button id="ad-wind-off" style="background:#222; border:1px solid #ff4d4d; color:white; padding:5px; cursor:pointer;">Ветер: ВЫКЛ</button>
            <div style="height:1px; background:#ff4d4d; opacity:0.3;"></div>
            <button class="ad-play" data-s="footsteps" style="background:#222; border:1px solid #4db8ff; color:white; padding:5px; cursor:pointer;">👣 Шаги (footsteps.mp3)</button>
            <button class="ad-play" data-s="forest_presence" style="background:#222; border:1px solid #4db8ff; color:white; padding:5px; cursor:pointer;">Лесное присутствие</button>
            <button class="ad-play" data-s="home_ambience" style="background:#222; border:1px solid #4db8ff; color:white; padding:5px; cursor:pointer;">Домашняя тишина</button>
            <button class="ad-play" data-s="door_creak" style="background:#222; border:1px solid #4db8ff; color:white; padding:5px; cursor:pointer;">🚪 Дверь (door_creak.mp3)</button>
        `;

        document.body.appendChild(this.element);
        this.bindEvents();
    }

    private bindEvents() {
        this.element.querySelector('#ad-close')?.addEventListener('click', () => {
            this.element.style.display = 'none';
        });

        this.element.querySelector('#ad-wind-on')?.addEventListener('click', () => {
            (this.game as any).audio?.startWind();
        });

        this.element.querySelector('#ad-wind-off')?.addEventListener('click', () => {
            (this.game as any).audio?.stopWind();
        });

        this.element.querySelectorAll('.ad-play').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const s = (e.target as HTMLElement).getAttribute('data-s');
                if (s) (this.game as any).audio?.play(s);
            });
        });
    }

    public toggle() {
        const d = this.element.style.display;
        this.element.style.display = d === 'none' ? 'flex' : 'none';
    }
}
