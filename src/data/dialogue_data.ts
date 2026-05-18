import { DialogueLine } from '../types/game.types';

export const DIALOGUE_LINES: DialogueLine[] = [
    {
        id: 'rinat_no_key',
        npcId: 'char_rinat',
        label: 'Спросить Рината о Марате',
        text: 'Ринат смотрит не на Айдара, а на дорогу за его плечом: «Не начинай с этого. Ты приехал к семье — вот и будь с семьёй».',
        requiredKeys: [],
        pressureDelta: 0,
    },
    {
        id: 'rinat_official_death',
        npcId: 'char_rinat',
        label: 'Показать официальную справку',
        text: '«Формально там всё закрыто», — говорит Ринат. Слово “формально” он произносит слишком аккуратно.',
        requiredKeys: ['clue_marat_official_death_version'],
        pressureDelta: 1,
        flags: ['rinat_knows_aidar_has_official_doc'],
    },
    {
        id: 'rinat_internal_register',
        npcId: 'char_rinat',
        label: 'Спросить про “граница / ответил”',
        text: 'Ринат резко гасит голос: «Где ты это видел? Айдар, такие строки не для разговоров у дороги».',
        requiredKeys: ['contradiction_marat_official_vs_internal'],
        dangerous: true,
        pressureDelta: 2,
        unlocks: ['clue_do_not_answer_rule'],
        flags: ['rinat_alerted', 'medical_contradiction_key_used'],
    },
    {
        id: 'rinat_do_not_answer',
        npcId: 'char_rinat',
        label: 'Назвать правило “не отвечай”',
        text: 'Он уже не спорит. «Если услышишь Марата у кромки, не проверяй. Не отвечай. Потом объясню, если успеем».',
        requiredKeys: ['clue_do_not_answer_rule'],
        dangerous: true,
        pressureDelta: 2,
        unlocks: ['route_kara_urman_edge_hint'],
        flags: ['rinat_practical_warning_given', 'forest_pressure_flag'],
    },
    {
        id: 'mansur_pc_access',
        npcId: 'char_babay',
        label: 'Спросить, почему ПК открыт',
        text: 'Мансур долго поправляет чашку. «Иногда человеку надо самому найти бумагу. Чужой ответ легче не становится».',
        requiredKeys: ['clue_mansur_allowed_pc_access_deliberately'],
        pressureDelta: 1,
        flags: ['babay_warned_aidar'],
    },
    {
        id: 'gulsina_yaramyy',
        npcId: 'char_abi',
        label: 'Спросить, что значит “ярамый”',
        text: 'Гөлсинә отвечает мягко, но без улыбки: «Не всё, что можно услышать, можно звать обратно».',
        requiredKeys: ['tt_yaramyy'],
        pressureDelta: 1,
        unlocks: ['clue_do_not_answer_rule'],
        flags: ['gulsina_warned_about_answer'],
    },
];

export const DIALOGUE_BY_ID = new Map(DIALOGUE_LINES.map((line) => [line.id, line]));
