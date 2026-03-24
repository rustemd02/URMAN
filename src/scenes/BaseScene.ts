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
}
