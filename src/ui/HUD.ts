import { Game } from '../game/Game';

export class HUD {
    private game: Game;
    private element: HTMLElement;

    constructor(game: Game) {
        this.game = game;
        this.element = document.createElement('div');
        this.element.id = 'hud-container';
        this.element.style.cssText = `
            position: fixed;
            bottom: 100px;
            left: 20px;
            z-index: 10002;
            pointer-events: none;
            color: #fff;
            font-family: 'Tahoma', sans-serif;
            font-size: 11px;
            text-shadow: 1px 1px 3px rgba(0,0,0,0.9);
            display: none;
        `;
        document.body.appendChild(this.element);
        this.element.style.display = 'block';
        this.update();
        setInterval(() => this.update(), 500);
    }

    public update() {
        const s = this.game.state;
        const bar = (val: number, color: string) => {
            const pct = Math.min(100, Math.max(0, val));
            return `<span style="display:inline-block;vertical-align:middle;width:50px;height:5px;background:rgba(0,0,0,0.3);border-radius:3px;overflow:hidden;margin-left:3px;">
                <span style="display:block;width:${pct}%;height:100%;background:${color};"></span></span>`;
        };
        this.element.innerHTML = `
            <div style="background:rgba(0,0,0,0.45);backdrop-filter:blur(4px);padding:8px 12px;border-radius:8px;display:flex;flex-direction:column;gap:3px;min-width:180px;">
                <div>💰 <b>${s.money}₽</b></div>
                <div>📖 Татарча ${bar(s.tatarKnowledge, '#4db8ff')} ${s.tatarKnowledge}%</div>
                <div>😨 Страх ${bar(s.fear, '#ff4444')}  💪 Сила ${bar(s.strength, '#88cc44')}</div>
                <div>❤️ Алсу ${bar(s.alsuRelation, '#ff69b4')}  🔦 ${s.flashlight}%</div>
            </div>
        `;
    }
}
