import { SceneManager } from './SceneManager';
import { GameState } from './GameState';
import { EventEmitter } from './EventEmitter';
import { VocabularySystem } from '../systems/VocabularySystem';
import { HUD } from '../ui/HUD';
import { InventoryUI } from '../ui/InventoryUI';
import { AudioSystem } from '../systems/AudioSystem';
import { InvestigationSystem } from '../systems/InvestigationSystem';
import { DialogueSystem } from '../systems/DialogueSystem';
import { SaveSystem } from '../systems/SaveSystem';

export class Game {
    public state: GameState;
    public scenes: SceneManager;
    public events: EventEmitter;
    public hud!: HUD;
    public inventoryUI!: InventoryUI;
    public vocabularySystem!: VocabularySystem;
    public investigation!: InvestigationSystem;
    public dialogue!: DialogueSystem;
    public saveSystem!: SaveSystem;
    public audio!: AudioSystem;

    constructor() {
        this.events = new EventEmitter();
        this.state = new GameState(this.events);
        this.scenes = new SceneManager(this);
        this.hud = new HUD(this);
        this.investigation = new InvestigationSystem(this);
        this.dialogue = new DialogueSystem(this);
        this.saveSystem = new SaveSystem(this);
        const loadedSnapshot = this.saveSystem.load();
        this.vocabularySystem = new VocabularySystem(this);
        this.inventoryUI = new InventoryUI(this);
        this.audio = new AudioSystem(this);
        
        // Initial items
        this.state.addItem({
            id: 'notepad',
            name: 'Блокнот Айдара',
            icon: '📔'
        });
        
        (window as any).URMAN = this;
        if (loadedSnapshot) {
            this.state.currentScene = loadedSnapshot.currentScene;
        }
    }

    public start() {
        const sceneFromUrl = new URLSearchParams(window.location.search).get('scene');
        const allowedScenes = new Set(['mainMenu', 'chapter1', 'intro', 'computer', 'forest', 'house', 'village', 'villageGreybox', 'mosque', 'zirat']);
        const savedScene = this.saveSystem.lastLoadedSnapshot?.currentScene;
        const targetScene = sceneFromUrl && allowedScenes.has(sceneFromUrl)
            ? sceneFromUrl
            : savedScene && allowedScenes.has(savedScene)
                ? savedScene
                : 'mainMenu';
        this.scenes.switchScene(targetScene);

    }
}
