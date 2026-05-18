import { Game } from '../game/Game';
import { KNOWLEDGE_KEY_BY_ID } from '../data/knowledge_keys';
import { VOCABULARY_BY_ID } from '../data/vocabulary_data';

export interface InvestigationSourcePayload {
    id: string;
    title: string;
    dangerLevel: number;
    reveals: string[];
    contradicts: string[];
    unlocks: string[];
    vocabulary: string[];
}

export class InvestigationSystem {
    constructor(private readonly game: Game) {}

    public registerOldPcOpen(item: InvestigationSourcePayload): void {
        this.game.state.markOldPcItemOpened(item.id);
        const keys = this.keysFromItem(item);
        this.game.state.addKnowledgeKeys(keys, item.id);

        for (const vocabularyId of item.vocabulary) {
            this.game.state.setVocabularyState(vocabularyId, 'confirmed');
            this.game.state.addKnowledgeKey(vocabularyId, item.id);
        }

        if (item.contradicts.includes('clue_marat_official_death_version')) {
            this.game.state.addKnowledgeKey('contradiction_marat_official_vs_internal', item.id);
        }

        if (item.id === 'tw_shurale_urman_boundary') {
            this.game.state.addKnowledgeKey('clue_do_not_answer_rule', item.id);
            this.game.state.setVocabularyState('tt_shurale', 'confirmed');
        }

        this.applyPressureFromItem(item);
    }

    public saveOldPcEvidence(item: InvestigationSourcePayload): void {
        const keyIds = this.keysFromItem(item);
        this.game.state.saveEvidence({
            id: item.id,
            title: item.title,
            sourceId: item.id,
            keyIds,
        });
    }

    public confirmVocabulary(entryId: string, sourceId = 'runtime'): void {
        if (!VOCABULARY_BY_ID.has(entryId)) return;
        this.game.state.setVocabularyState(entryId, 'confirmed');
        this.game.state.addKnowledgeKey(entryId, sourceId);
    }

    public canUseKey(keyId: string): boolean {
        return this.game.state.hasKnowledgeKey(keyId) || this.game.state.vocabularyStates[keyId] === 'confirmed';
    }

    private keysFromItem(item: InvestigationSourcePayload): string[] {
        const keys = new Set<string>();
        for (const keyId of [...item.reveals, ...item.unlocks, ...item.vocabulary]) {
            if (KNOWLEDGE_KEY_BY_ID.has(keyId) || keyId.startsWith('tt_') || keyId.startsWith('route_')) {
                keys.add(keyId);
            }
        }
        if (item.contradicts.length > 0 && item.reveals.includes('clue_marat_case_boundary_marker')) {
            keys.add('contradiction_marat_official_vs_internal');
        }
        return [...keys];
    }

    private applyPressureFromItem(item: InvestigationSourcePayload): void {
        if (item.dangerLevel >= 3) {
            this.game.state.raisePressure(2, `oldpc_${item.id}`);
            this.game.state.addPressureFlag('council_notified');
        } else if (item.dangerLevel >= 2) {
            this.game.state.raisePressure(1, `oldpc_${item.id}`);
        }

        if (item.unlocks.includes('route_kara_urman_edge_hint')) {
            this.game.state.addPressureFlag('forest_pressure_flag');
            this.game.state.completeBeat('beat_kara_urman_route_unlocked');
        }
    }
}
