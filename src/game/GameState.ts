import { EventEmitter } from './EventEmitter';
import { KNOWLEDGE_KEY_BY_ID } from '../data/knowledge_keys';
import { VOCABULARY_ENTRIES } from '../data/vocabulary_data';
import { EvidenceRecord, NpcState, VocabularyState } from '../types/game.types';

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
    public flags: { [key: string]: boolean } = {
        'debug_bypass': false
    };
    
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
    public timeOfDay: string = 'вечер';
    public locationName: string = 'Кырлай';

    // Route navigation state
    public routeCurrentNodeId: string = 'arrival_vehicle_dusk';
    public routeDiscoveredNodeIds: string[] = [];
    public routeJournalUpdateIds: string[] = [];
    public routeJournalPressure: boolean = false;

    // Языковая система (LAS)
    public langLevel: 'beginner' | 'intermediate' | 'native' = 'beginner';
    public learnedWords: Record<string, { count: number, discovered: boolean, dictionaryUnlocked: boolean }> = {};

    // Shared MVP investigation state
    public knownKeys: string[] = [];
    public savedEvidenceIds: string[] = [];
    public evidenceRecords: EvidenceRecord[] = [];
    public vocabularyStates: Record<string, VocabularyState> = {};
    public npcStates: Record<string, NpcState> = {};
    public pressureLevel: 0 | 1 | 2 | 3 = 0;
    public pressureFlags: string[] = [];
    public completedBeats: string[] = [];
    public oldPcOpenedItemIds: string[] = [];

    constructor(events: EventEmitter) {
        this.events = events;
        this.initStarterInventory();
        this.initVocabularyStates();
    }

    private initStarterInventory() {
        this.inventory = [
            { id: 'phone', name: 'Телефон', icon: '📱' },
            { id: 'flashlight', name: 'Фонарик', icon: '🔦' },
            { id: 'notepad', name: 'Блокнот Айдара', icon: '📔' },
            { id: 'candy', name: 'Конфеты от әби', icon: '🍬' },
            { id: 'key', name: 'Ключ от дома', icon: '🗝️' },
        ];
    }

    setFlag(flag: string, value: boolean) {
        this.flags[flag] = value;
        this.events.emit('flag_changed', { flag, value });
    }

    addKnowledgeKey(keyId: string, sourceId = 'runtime'): boolean {
        if (!keyId) return false;
        if (!this.knownKeys.includes(keyId)) {
            this.knownKeys.push(keyId);
            const key = KNOWLEDGE_KEY_BY_ID.get(keyId);
            if (key?.type === 'word' || keyId.startsWith('tt_')) {
                this.setVocabularyState(keyId, 'confirmed');
            }
            for (const unlock of key?.unlocks ?? []) {
                if (unlock.startsWith('tt_')) this.setVocabularyState(unlock, 'guessed');
                if (unlock.startsWith('clue_') || unlock.startsWith('route_')) this.addKnowledgeKey(unlock, keyId);
                if (unlock.startsWith('pressure_')) this.addPressureFlag(unlock);
            }
            this.events.emit('knowledge_updated', { keyId, sourceId, knownKeys: this.knownKeys });
            return true;
        }
        return false;
    }

    addKnowledgeKeys(keyIds: string[], sourceId = 'runtime'): string[] {
        const added: string[] = [];
        for (const keyId of keyIds) {
            if (this.addKnowledgeKey(keyId, sourceId)) added.push(keyId);
        }
        return added;
    }

    hasKnowledgeKey(keyId: string): boolean {
        return this.knownKeys.includes(keyId);
    }

    saveEvidence(record: Omit<EvidenceRecord, 'savedAt'>): void {
        if (!this.savedEvidenceIds.includes(record.id)) {
            this.savedEvidenceIds.push(record.id);
        }
        const nextRecord: EvidenceRecord = { ...record, savedAt: Date.now() };
        this.evidenceRecords = [
            nextRecord,
            ...this.evidenceRecords.filter((candidate) => candidate.id !== record.id),
        ];
        this.addKnowledgeKeys(record.keyIds, record.sourceId);
        this.events.emit('evidence_saved', nextRecord);
    }

    markOldPcItemOpened(itemId: string): void {
        if (!this.oldPcOpenedItemIds.includes(itemId)) {
            this.oldPcOpenedItemIds.push(itemId);
            this.events.emit('oldpc_item_opened', { itemId });
        }
    }

    setVocabularyState(entryId: string, state: VocabularyState): void {
        const previous = this.vocabularyStates[entryId] ?? 'unknown';
        if (this.vocabularyRank(state) < this.vocabularyRank(previous)) return;
        this.vocabularyStates[entryId] = state;
        if (!this.knownKeys.includes(entryId) && state === 'confirmed') {
            this.knownKeys.push(entryId);
        }
        this.events.emit('vocabulary_unlocked', { entryId, state });
    }

    updateNpcState(npcId: string, patch: Partial<NpcState>): void {
        const current = this.npcStates[npcId] ?? { flags: [] };
        const flags = new Set([...(current.flags ?? []), ...(patch.flags ?? [])]);
        this.npcStates[npcId] = {
            ...current,
            ...patch,
            flags: [...flags],
        };
        this.events.emit('npc_state_updated', { npcId, state: this.npcStates[npcId] });
    }

    raisePressure(delta: number, reason: string): void {
        if (delta <= 0) return;
        const previous = this.pressureLevel;
        this.pressureLevel = Math.min(3, this.pressureLevel + delta) as 0 | 1 | 2 | 3;
        this.addPressureFlag(reason);
        if (this.pressureLevel !== previous) {
            this.routeJournalPressure = this.pressureLevel >= 2;
            this.events.emit('pressure_level_changed', { previous, current: this.pressureLevel, reason });
        }
    }

    addPressureFlag(flag: string): void {
        if (!flag || this.pressureFlags.includes(flag)) return;
        this.pressureFlags.push(flag);
        if (flag === 'forest_pressure_flag') this.addKnowledgeKey('route_kara_urman_edge_hint', flag);
        this.events.emit('pressure_flag_added', { flag });
    }

    completeBeat(beatId: string): void {
        if (!this.completedBeats.includes(beatId)) {
            this.completedBeats.push(beatId);
            this.events.emit('beat_completed', { beatId });
        }
    }

    addItem(item: InventoryItem) {
        if (this.inventory.some((candidate) => candidate.id === item.id)) return;
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

    rememberRouteVisit(nodeId: string, journalUpdateId?: string, pressureVariant?: string) {
        this.routeCurrentNodeId = nodeId;
        if (!this.routeDiscoveredNodeIds.includes(nodeId)) {
            this.routeDiscoveredNodeIds.push(nodeId);
        }
        if (journalUpdateId && !this.routeJournalUpdateIds.includes(journalUpdateId)) {
            this.routeJournalUpdateIds.push(journalUpdateId);
        }
        if (pressureVariant && !['normal', 'evening'].includes(pressureVariant)) {
            this.routeJournalPressure = true;
        }
        this.events.emit('route_updated', {
            currentNodeId: this.routeCurrentNodeId,
            discoveredNodeIds: this.routeDiscoveredNodeIds,
            journalUpdateIds: this.routeJournalUpdateIds,
            journalPressure: this.routeJournalPressure,
        });
    }

    private initVocabularyStates(): void {
        for (const entry of VOCABULARY_ENTRIES) {
            this.vocabularyStates[entry.id] = entry.stateDefault;
        }
    }

    private vocabularyRank(state: VocabularyState): number {
        if (state === 'confirmed') return 2;
        if (state === 'guessed') return 1;
        return 0;
    }
}
