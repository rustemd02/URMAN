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
            top: 20px;
            left: 20px;
            z-index: 10002;
            pointer-events: none;
            color: #fff;
            font-family: 'Verdana', sans-serif;
            text-transform: uppercase;
            font-size: 11px;
            letter-spacing: 1px;
            text-shadow: 2px 2px 0px #000;
        `;
        document.body.appendChild(this.element);
        this.update();
        
        // Перехватываем изменения в GameState (можно в будущем добавить ивенты сюда)
        setInterval(() => this.update(), 1000);
    }

    public update() {
        const { flashlight, tatarKnowledge } = this.game.state;
        this.element.innerHTML = `
            <span style="display: flex; align-items: center; gap: 8px;">
                <span style="opacity: 0.8;">🔋 ФОНАРЬ:</span> 
                <span style="font-weight: bold; color: ${flashlight < 20 ? '#ff4d4d' : '#88ff88'}">${flashlight}%</span>
                <span style="margin: 0 15px; opacity: 0.3;">|</span>
                <span style="opacity: 0.8;">ЗНАНИЕ ТАТАРСКОГО:</span> 
                <span style="font-weight: bold; color: #4db8ff;">${tatarKnowledge}%</span>
            </span>
        `;
    }
}
