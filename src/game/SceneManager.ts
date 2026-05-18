import { Game } from './Game';
import { ComputerScene } from '../scenes/ComputerScene';
import { ForestScene } from '../scenes/ForestScene';
import { HouseScene } from '../scenes/HouseScene';
import { IntroScene } from '../scenes/IntroScene';
import { MainMenuScene } from '../scenes/MainMenuScene';
import { ChapterScene } from '../scenes/ChapterScene';
import { MainMapScene } from '../MainMap/MainMapScene';
import { BaseScene } from '../scenes/BaseScene';
import { MosqueScene } from '../scenes/MosqueScene';
import { ZiratMiniGame } from '../scenes/ZiratMiniGame';
import { RouteNavigationScene } from '../scenes/RouteNavigationScene';

export class SceneManager {
    private game: Game;
    public currentScene: BaseScene | null = null;
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
            case 'mainMenu':
                this.currentScene = new MainMenuScene(this.game);
                break;
            case 'chapter1':
                this.currentScene = new ChapterScene(this.game);
                break;
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
            case 'village':
                this.currentScene = new RouteNavigationScene(this.game);
                break;
            case 'villageGreybox':
                this.currentScene = new MainMapScene(this.game);
                break;
            case 'mosque':
                this.currentScene = new MosqueScene(this.game);
                break;
            case 'zirat':
                this.currentScene = new ZiratMiniGame(this.game);
                break;
            default:
                console.error(`Scene ${sceneId} not found`);
                return;
        }

        const isGameplay = ['village', 'villageGreybox', 'forest', 'house', 'mosque', 'zirat'].includes(sceneId);
        this.game.state.currentScene = sceneId;

        if (this.game.inventoryUI) {
            this.game.inventoryUI.setVisible(isGameplay);
        }
        if (this.game.hud) {
            this.game.hud.setVisible(isGameplay);
        }

        if (this.currentScene) {
            this.currentScene.init(this.container);
        }
    }
}
