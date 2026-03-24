import { SceneManager } from './SceneManager';
import { GameState } from './GameState';
import { EventEmitter } from './EventEmitter';

export class Game {
    public state: GameState;
    public scenes: SceneManager;
    public events: EventEmitter;

    constructor() {
        this.events = new EventEmitter();
        this.state = new GameState(this.events);
        this.scenes = new SceneManager(this);
        
        (window as any).URMAN = this;
    }

    public start() {
        // Начинаем с компьютерной сцены (или интро)
        this.scenes.switchScene('computer');
    }
}
