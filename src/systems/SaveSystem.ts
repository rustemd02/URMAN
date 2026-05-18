import { Game } from '../game/Game';
import { GameSnapshot, VocabularyState } from '../types/game.types';

const SAVE_KEY = 'urman.mvp.save.v1';
const OLD_PC_UI_SAVE_KEY = 'urman.oldPcHub.state.v1';

export class SaveSystem {
    public lastLoadedSnapshot: GameSnapshot | null = null;

    constructor(private readonly game: Game) {}

    public save(): GameSnapshot {
        const state = this.game.state;
        const snapshot: GameSnapshot = {
            version: 1,
            savedAt: new Date().toISOString(),
            currentScene: state.currentScene,
            flags: { ...state.flags },
            routeCurrentNodeId: state.routeCurrentNodeId,
            routeDiscoveredNodeIds: [...state.routeDiscoveredNodeIds],
            routeJournalUpdateIds: [...state.routeJournalUpdateIds],
            routeJournalPressure: state.routeJournalPressure,
            knownKeys: [...state.knownKeys],
            savedEvidenceIds: [...state.savedEvidenceIds],
            evidenceRecords: [...state.evidenceRecords],
            vocabularyStates: { ...state.vocabularyStates },
            npcStates: { ...state.npcStates },
            pressureLevel: state.pressureLevel,
            pressureFlags: [...state.pressureFlags],
            completedBeats: [...state.completedBeats],
            oldPcProgress: {
                openedItemIds: [...state.oldPcOpenedItemIds],
                savedItemIds: [...state.savedEvidenceIds],
                unlockedKeys: [...state.knownKeys],
                readItemIds: [...state.oldPcOpenedItemIds],
            },
        };
        localStorage.setItem(SAVE_KEY, JSON.stringify(snapshot));
        return snapshot;
    }

    public load(): GameSnapshot | null {
        const raw = localStorage.getItem(SAVE_KEY);
        if (!raw) return null;
        try {
            const snapshot = JSON.parse(raw) as GameSnapshot;
            if (snapshot.version !== 1) return null;
            this.apply(snapshot);
            this.lastLoadedSnapshot = snapshot;
            return snapshot;
        } catch {
            return null;
        }
    }

    public clear(): void {
        localStorage.removeItem(SAVE_KEY);
    }

    private apply(snapshot: GameSnapshot): void {
        const state = this.game.state;
        state.flags = { ...state.flags, ...snapshot.flags, debug_bypass: false };
        state.currentScene = snapshot.currentScene;
        state.routeCurrentNodeId = snapshot.routeCurrentNodeId;
        state.routeDiscoveredNodeIds = [...snapshot.routeDiscoveredNodeIds];
        state.routeJournalUpdateIds = [...snapshot.routeJournalUpdateIds];
        state.routeJournalPressure = snapshot.routeJournalPressure;
        state.knownKeys = [...snapshot.knownKeys];
        state.savedEvidenceIds = [...snapshot.savedEvidenceIds];
        state.evidenceRecords = [...snapshot.evidenceRecords];
        state.vocabularyStates = { ...snapshot.vocabularyStates } as Record<string, VocabularyState>;
        state.npcStates = { ...snapshot.npcStates };
        state.pressureLevel = snapshot.pressureLevel;
        state.pressureFlags = [...snapshot.pressureFlags];
        state.completedBeats = [...snapshot.completedBeats];
        state.oldPcOpenedItemIds = [...snapshot.oldPcProgress.openedItemIds];
        this.syncOldPcUiState(snapshot);
    }

    private syncOldPcUiState(snapshot: GameSnapshot): void {
        const openedItemIds = snapshot.oldPcProgress.openedItemIds ?? [];
        const savedItemIds = snapshot.oldPcProgress.savedItemIds ?? snapshot.savedEvidenceIds;
        const lastOpenedItemId = openedItemIds[openedItemIds.length - 1] ?? savedItemIds[savedItemIds.length - 1] ?? null;
        const recentEvents = [
            'Состояние восстановлено из общего сохранения.',
            ...savedItemIds.slice(-3).map((id) => `Улика сохранена: ${id}`),
        ];

        localStorage.setItem(OLD_PC_UI_SAVE_KEY, JSON.stringify({
            activeSection: 'archive_search',
            activeItemId: lastOpenedItemId,
            query: '',
            unlockedKeys: [...new Set([
                ...snapshot.knownKeys,
                ...openedItemIds.map((id) => `saved_${id}`),
                ...savedItemIds.map((id) => `saved_${id}`),
            ])],
            savedClues: savedItemIds,
            recentEvents,
        }));
    }
}
