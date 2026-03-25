import { EventEmitter } from './EventEmitter';

export interface InventoryItem {
    id: string;
    name: string;
    icon: string;
}

export interface VocabularyItem {
    id: string;
    tatar: string;
    russian: string;
    count: number;
    learned: boolean;
}

export class GameState {
    private events: EventEmitter;
    
    // Инвентарь
    public inventory: InventoryItem[] = [];
    public currentScene: string = 'intro';
    public flags: { [key: string]: boolean } = {};
    
    // Словарь
    public vocabulary: VocabularyItem[] = [];
    
    // --- Статистика героя (влияет на концовку) ---
    public money: number = 500;           // Рубли
    public tatarKnowledge: number = 5;    // Знание татарского (0-100%)
    public fear: number = 0;              // Уровень страха (0-100)
    public noise: number = 10;            // Шумность (0-100)
    public strength: number = 30;         // Сила/выносливость (0-100)
    public alsuRelation: number = 0;      // Отношения с Алсу (0-100)
    public flashlight: number = 84;       // Заряд фонарика

    // Сюжетные переменные
    public day: number = 1;
    public time: string = '20:00';

    constructor(events: EventEmitter) {
        this.events = events;
        this.initStarterInventory();
    }

    private initStarterInventory() {
        this.inventory = [
            { id: 'phone', name: 'Телефон', icon: '📱' },
            { id: 'flashlight', name: 'Фонарик', icon: '🔦' },
            { id: 'notepad', name: 'Блокнот', icon: '📓' },
            { id: 'candy', name: 'Конфеты от әби', icon: '🍬' },
            { id: 'key', name: 'Ключ от дома', icon: '🗝️' },
        ];
    }

    setFlag(flag: string, value: boolean) {
        this.flags[flag] = value;
        this.events.emit('flag_changed', { flag, value });
    }

    addItem(item: InventoryItem) {
        this.inventory.push(item);
        this.events.emit('inventory_updated', this.inventory);
    }

    removeItem(id: string) {
        this.inventory = this.inventory.filter(i => i.id !== id);
        this.events.emit('inventory_updated', this.inventory);
    }

    modifyStat(stat: 'money' | 'tatarKnowledge' | 'fear' | 'noise' | 'strength' | 'alsuRelation', delta: number) {
        (this as any)[stat] = Math.max(0, Math.min(100, (this as any)[stat] + delta));
        this.events.emit('stat_changed', { stat, value: (this as any)[stat] });
    }
}
