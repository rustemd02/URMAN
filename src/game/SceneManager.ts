import { Game } from './Game';
import { ComputerScene } from '../scenes/ComputerScene';
import { ForestScene } from '../scenes/ForestScene';
import { HouseScene } from '../scenes/HouseScene';
import { IntroScene } from '../scenes/IntroScene';
import { BaseScene } from '../scenes/BaseScene';

export class SceneManager {
    private game: Game;
    private currentScene: BaseScene | null = null;
    private container: HTMLElement;

    constructor(game: Game) {
        this.game = game;
        const app = document.getElementById('app');
        if (!app) throw new Error("#app not found");
        this.container = app;
    }

    public switchScene(sceneId: string) {
        if (this.currentScene) {
            this.currentScene.destroy();
        }

        this.container.innerHTML = '';

        switch (sceneId) {
            case 'intro':
                this.currentScene = new IntroScene(this.game);
                break;
            case 'computer':
                this.currentScene = new ComputerScene(this.game);
                break;
            case 'forest':
                this.currentScene = new ForestScene(this.game);
                break;
            case 'house':
                this.currentScene = new HouseScene(this.game);
                break;
            default:
                console.error(`Scene ${sceneId} not found`);
                return;
        }

        this.currentScene.init(this.container);
    }
}
