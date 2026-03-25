import { Game } from '../game/Game';

export abstract class BaseScene {
    protected game: Game;
    protected container: HTMLElement | null = null;

    constructor(game: Game) {
        this.game = game;
    }

    abstract init(container: HTMLElement): void;
    
    destroy(): void {
        if (this.container) {
            this.container.innerHTML = '';
        }
    }

    // Опциональный метод для системных уведомлений
    onOSMessage?(title: string, message: string): void;
}
