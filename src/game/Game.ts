import { SceneManager } from './SceneManager';
import { GameState } from './GameState';
import { EventEmitter } from './EventEmitter';
import { VocabularySystem } from '../systems/VocabularySystem';
import { HUD } from '../ui/HUD';
import { InventoryUI } from '../ui/InventoryUI';

export class Game {
    public state: GameState;
    public scenes: SceneManager;
    public events: EventEmitter;
    public hud!: HUD;
    public inventoryUI!: InventoryUI;
    public vocabularySystem!: VocabularySystem;

    constructor() {
        this.events = new EventEmitter();
        this.state = new GameState(this.events);
        this.scenes = new SceneManager(this);
        this.hud = new HUD(this);
        this.vocabularySystem = new VocabularySystem(this);
        this.inventoryUI = new InventoryUI(this);
        
        // Initial items
        this.state.addItem({
            id: 'notepad',
            name: 'Блокнот Айдара',
            icon: '📔'
        });
        
        (window as any).URMAN = this;
    }

    public start() {
        // Начинаем с интро
        this.scenes.switchScene('mainMenu');

        // Для теста добавим сразу пару слов
        setTimeout(() => {
            this.vocabularySystem.discoverWord('Тэрэзе', 'Окно');
            this.vocabularySystem.discoverWord('Өй', 'Дом');
        }, 3000);
    }
}
