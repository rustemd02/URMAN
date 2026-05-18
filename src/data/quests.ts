import { QuestDefinition } from '../types/game.types';

export const QUESTS: QuestDefinition[] = [
    {
        id: 'quest_marat_first_contradiction',
        title: 'Найти первую трещину в версии Марата',
        requiredKeys: ['clue_marat_official_death_version', 'contradiction_marat_official_vs_internal'],
        completedBeat: 'beat_marat_first_contradiction',
    },
    {
        id: 'quest_language_reread',
        title: 'Использовать татарское слово как инструмент',
        requiredKeys: ['tt_urman', 'clue_folklore_as_survival_rule'],
        completedBeat: 'beat_language_word_used_as_tool',
    },
    {
        id: 'quest_kara_urman_cliffhanger',
        title: 'Дойти до кромки Кара-Урмана по уликам',
        requiredKeys: ['route_kara_urman_edge_hint', 'clue_do_not_answer_rule'],
        completedBeat: 'beat_kara_urman_cliffhanger_ready',
    },
];
