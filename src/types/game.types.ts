export type KnowledgeKeyType =
    | 'name'
    | 'date'
    | 'place'
    | 'document'
    | 'word'
    | 'photo'
    | 'contradiction'
    | 'testimony'
    | 'route';

export type PressureLevel = 0 | 1 | 2 | 3;
export type VocabularyState = 'unknown' | 'guessed' | 'confirmed';

export interface KnowledgeKey {
    id: string;
    type: KnowledgeKeyType;
    title: string;
    summary: string;
    sourceIds: string[];
    tags: string[];
    dangerLevel: PressureLevel;
    relatedCharacters: string[];
    relatedLocations: string[];
    reveals: string[];
    contradicts: string[];
    unlocks: string[];
    usableInDialogue: boolean;
    mvp: boolean;
}

export interface VocabularyEntry {
    id: string;
    tatar: string;
    russian: string;
    context: string;
    stateDefault: VocabularyState;
    searchTerms: string[];
    reReadTargets: string[];
    gameplayUse: 'search' | 'dialogue' | 'document_reread' | 'route' | 'journal_link';
    dialogueKeyId?: string;
    needsConsultantReview: boolean;
}

export interface NpcState {
    reactionLevel?: number;
    flags: string[];
}

export interface EvidenceRecord {
    id: string;
    title: string;
    sourceId: string;
    keyIds: string[];
    savedAt: number;
}

export interface OldPcProgress {
    openedItemIds: string[];
    savedItemIds: string[];
    unlockedKeys: string[];
    readItemIds: string[];
}

export interface DialogueLine {
    id: string;
    npcId: string;
    label: string;
    text: string;
    requiredKeys: string[];
    dangerous?: boolean;
    pressureDelta?: PressureLevel;
    unlocks?: string[];
    flags?: string[];
}

export interface QuestDefinition {
    id: string;
    title: string;
    requiredKeys: string[];
    completedBeat: string;
}

export interface GameSnapshot {
    version: 1;
    savedAt: string;
    currentScene: string;
    flags: Record<string, boolean>;
    routeCurrentNodeId: string;
    routeDiscoveredNodeIds: string[];
    routeJournalUpdateIds: string[];
    routeJournalPressure: boolean;
    knownKeys: string[];
    savedEvidenceIds: string[];
    evidenceRecords: EvidenceRecord[];
    vocabularyStates: Record<string, VocabularyState>;
    npcStates: Record<string, NpcState>;
    pressureLevel: PressureLevel;
    pressureFlags: string[];
    completedBeats: string[];
    oldPcProgress: OldPcProgress;
}
