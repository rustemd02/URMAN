import { Game } from '../game/Game';

export class HUD {
    private game: Game;
    private element: HTMLElement;
    private intervalId: number | null = null;
    private isCollapsed: boolean = false; // Состояние сворачивания

    constructor(game: Game) {
        this.game = game;
        this.element = document.createElement('div');
        this.element.id = 'hud-container';
        this.element.style.cssText = `
            position: fixed;
            bottom: 20px; /* Прижали чуть ниже */
            left: 20px;
            z-index: 10002;
            pointer-events: auto; /* Включаем клики! */
            color: #fff;
            font-family: 'Philosopher', sans-serif;
            font-size: 13px;
            display: none;
        `;

        document.body.appendChild(this.element);

        // Вешаем событие клика на весь контейнер (или можно на заголовок)
        this.element.onclick = (e) => {
            const target = e.target as HTMLElement;
            if (target.closest('.hud-header')) {
                this.isCollapsed = !this.isCollapsed;
                this.update();
            }
        };

        this.update();
        this.intervalId = window.setInterval(() => this.update(), 500);
    }

    public destroy() {
        if (this.intervalId !== null) {
            clearInterval(this.intervalId);
            this.intervalId = null;
        }
        this.element.remove();
    }

    public setVisible(show: boolean) {
        this.element.style.display = show ? 'block' : 'none';
    }

    private bar(val: number, color: string, width = 50) {
        const pct = Math.min(100, Math.max(0, val));
        return `
            <span style="display:inline-block; vertical-align:middle; width:${width}px; height:4px; background:rgba(255,255,255,0.1); border-radius:4px; overflow:hidden; margin:0 4px;">
                <span style="display:block; width:${pct}%; height:100%; background:${color}; transition: width 0.3s;"></span>
            </span>
        `;
    }

    public update() {
        const s = this.game.state as any;

        // Данные (берем твои же)
        const money = s.money || 0;
        const health = s.health ?? 100;
        const fear = s.fear || 0;
        const hour = s.timeOfDay ?? s.timeLabel ?? '--:--';
        const day = s.day || 1;

        // Если свернуто — показываем только компактную полоску
        if (this.isCollapsed) {
            this.element.innerHTML = `
                <div class="hud-header" style="
                    background:rgba(8,10,14,0.8);
                    backdrop-filter:blur(10px);
                    padding:8px 12px;
                    border-radius:20px;
                    border:1px solid rgba(255,255,255,0.1);
                    cursor:pointer;
                    display:flex;
                    align-items:center;
                    gap:10px;
                    box-shadow: 0 4px 15px rgba(0,0,0,0.5);
                ">
                    <span style="font-size:16px;">🌲</span>
                    <b>День ${day}</b>
                    <span>${hour}</span>
                    <span>❤️ ${this.bar(health, '#ff6b6b', 40)}</span>
                    <span style="opacity:0.5; font-size:10px;">[развернуть]</span>
                </div>
            `;
            return;
        }

        // Если развернуто — твой полный дизайн, но с кнопкой сворачивания
        this.element.innerHTML = `
            <div style="
                background:rgba(8,10,14,0.7);
                backdrop-filter:blur(8px);
                border:1px solid rgba(255,255,255,0.1);
                padding:12px;
                border-radius:12px;
                display:flex;
                flex-direction:column;
                gap:6px;
                min-width:240px;
                box-shadow: 0 10px 30px rgba(0,0,0,0.5);
            ">
                <div class="hud-header" style="
                    display:flex;
                    justify-content:space-between;
                    align-items:center;
                    cursor:pointer;
                    border-bottom:1px solid rgba(255,255,255,0.1);
                    padding-bottom:6px;
                    margin-bottom:4px;
                ">
                    <div style="font-weight:bold; letter-spacing:1px; color:#4db8ff;">КАРА-УРМАН</div>
                    <div style="font-size:10px; opacity:0.5;">[свернуть]</div>
                </div>

                <div style="display:flex; justify-content:space-between;">
                    <span>💰 Деньги:</span> <b>${money}₽</b>
                </div>
                
                <div>❤️ Жизнь ${this.bar(health, '#ff6b6b')} ${health}%</div>
                <div>😨 Страх ${this.bar(fear, '#ff5a5a')} ${fear}%</div>
                <div>📖 Татарча ${this.bar(s.tatarKnowledge || 0, '#4db8ff')}</div>
                
                <div style="font-size:11px; margin-top:5px; padding-top:5px; border-top:1px solid rgba(255,255,255,0.05); display:grid; grid-template-columns: 1fr 1fr; gap:4px; opacity:0.8;">
                    <span>📍 ${s.locationName || 'Урман'}</span>
                    <span style="text-align:right;">🕰️ ${hour}</span>
                    <span>🌫️ ${s.weather || 'Ясно'}</span>
                    <span style="text-align:right;">📅 День ${day}</span>
                </div>
                
                </div>
        `;
    }
}