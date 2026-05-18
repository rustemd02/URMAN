export const OLD_PC_SECTION_LABELS = {
    archive_search: 'Архивный поиск',
    documents_marat: 'Документы Марата',
    tatarwiki: 'Татарвики',
    saved_messages: 'Сохранённые сообщения',
    internal_accounting: 'Внутренний учёт Кырлая',
    household_registry: 'Реестр домов / семей',
    violations_compensation: 'Нарушения / компенсации',
    kara_urman: 'Кара-Урман',
    damaged_hidden: 'Повреждённые / скрытые файлы',
    household_misc: 'Бытовые папки',
} as const;

export type OldPcSection = keyof typeof OLD_PC_SECTION_LABELS;

export type OldPcItemType =
    | 'document'
    | 'record'
    | 'message'
    | 'tatarwiki_article'
    | 'folder_note'
    | 'corrupted_fragment';

export type OldPcReliability =
    | 'official_lie'
    | 'partial_truth'
    | 'personal_memory'
    | 'village_record'
    | 'pact_record'
    | 'folklore_mask'
    | 'corrupted'
    | 'unverified';

export type OldPcCanonStatus =
    | 'canon'
    | 'soft_canon'
    | 'hypothesis'
    | 'proposal'
    | 'in_world_lie';

export interface OldPcItem {
    id: string;
    type: OldPcItemType;
    title: string;
    pcSection: OldPcSection;
    canonStatus: OldPcCanonStatus;
    reliability: OldPcReliability;
    sourceKind?: string;
    inWorldSource?: string;
    mvp: boolean;
    dangerLevel: number;
    visibleFromStart: boolean;
    requires: string[];
    searchTerms: string[];
    suggestedTerms: string[];
    reveals: string[];
    contradicts: string[];
    unlocks: string[];
    relatedCharacters: string[];
    relatedLocations: string[];
    vocabulary: string[];
    notesForLLM?: string;
    body: string;
    path: string;
}

type RawModuleMap = Record<string, string>;

const rawItems = import.meta.glob('../../../content/old_pc/**/*.md', {
    eager: true,
    query: '?raw',
    import: 'default',
}) as RawModuleMap;

const arrayFields = new Set([
    'requires',
    'searchTerms',
    'suggestedTerms',
    'reveals',
    'contradicts',
    'unlocks',
    'relatedCharacters',
    'relatedLocations',
    'vocabulary',
]);

function parseScalar(key: string, rawValue: string): string | boolean | number | string[] {
    const value = rawValue.trim();

    if (arrayFields.has(key)) {
        if (!value.startsWith('[') || !value.endsWith(']')) return [];
        const inner = value.slice(1, -1).trim();
        if (!inner) return [];
        return inner
            .split(',')
            .map((part) => part.trim().replace(/^["']|["']$/g, ''))
            .filter(Boolean);
    }

    if (value === 'true') return true;
    if (value === 'false') return false;
    if (/^-?\d+$/.test(value)) return Number(value);

    return value.replace(/^["']|["']$/g, '');
}

function parseMarkdownItem(path: string, raw: string): OldPcItem | null {
    if (path.endsWith('/README.md')) return null;

    const match = raw.match(/^---\n([\s\S]*?)\n---\n([\s\S]*)$/);
    if (!match) return null;

    const meta: Record<string, string | boolean | number | string[]> = {};
    for (const line of match[1].split('\n')) {
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('#')) continue;

        const sep = trimmed.indexOf(':');
        if (sep === -1) continue;

        const key = trimmed.slice(0, sep).trim();
        const value = trimmed.slice(sep + 1).trim();
        meta[key] = parseScalar(key, value);
    }

    return {
        id: String(meta.id),
        type: meta.type as OldPcItemType,
        title: String(meta.title),
        pcSection: meta.pcSection as OldPcSection,
        canonStatus: meta.canonStatus as OldPcCanonStatus,
        reliability: meta.reliability as OldPcReliability,
        sourceKind: typeof meta.sourceKind === 'string' ? meta.sourceKind : undefined,
        inWorldSource: typeof meta.inWorldSource === 'string' ? meta.inWorldSource : undefined,
        mvp: Boolean(meta.mvp),
        dangerLevel: Number(meta.dangerLevel ?? 0),
        visibleFromStart: Boolean(meta.visibleFromStart),
        requires: Array.isArray(meta.requires) ? meta.requires : [],
        searchTerms: Array.isArray(meta.searchTerms) ? meta.searchTerms : [],
        suggestedTerms: Array.isArray(meta.suggestedTerms) ? meta.suggestedTerms : [],
        reveals: Array.isArray(meta.reveals) ? meta.reveals : [],
        contradicts: Array.isArray(meta.contradicts) ? meta.contradicts : [],
        unlocks: Array.isArray(meta.unlocks) ? meta.unlocks : [],
        relatedCharacters: Array.isArray(meta.relatedCharacters) ? meta.relatedCharacters : [],
        relatedLocations: Array.isArray(meta.relatedLocations) ? meta.relatedLocations : [],
        vocabulary: Array.isArray(meta.vocabulary) ? meta.vocabulary : [],
        notesForLLM: typeof meta.notesForLLM === 'string' ? meta.notesForLLM : undefined,
        body: match[2].trim(),
        path,
    };
}

export const OLD_PC_ITEMS: OldPcItem[] = Object.entries(rawItems)
    .map(([path, raw]) => parseMarkdownItem(path, raw))
    .filter((item): item is OldPcItem => Boolean(item))
    .sort((a, b) => {
        if (a.visibleFromStart !== b.visibleFromStart) return a.visibleFromStart ? -1 : 1;
        return a.title.localeCompare(b.title, 'ru');
    });

export const OLD_PC_ITEMS_BY_ID = new Map(OLD_PC_ITEMS.map((item) => [item.id, item]));

export const OLD_PC_BOOT_KEYS = [
    'query_марат',
    'term_марат',
    'query_шүрәле',
    'query_урман',
    'tt_urman',
];

export function normalizeOldPcTerm(value: string): string {
    return value.trim().toLocaleLowerCase('ru');
}

export function makeQueryKey(value: string): string {
    return `query_${normalizeOldPcTerm(value)}`;
}

export function isOldPcItemUnlocked(item: OldPcItem, unlockedKeys: Set<string>): boolean {
    if (item.visibleFromStart) return true;
    return item.requires.every((key) => unlockedKeys.has(key));
}

export function searchOldPcItems(query: string, unlockedKeys: Set<string>, section?: OldPcSection): OldPcItem[] {
    const normalized = normalizeOldPcTerm(query);

    return OLD_PC_ITEMS.filter((item) => {
        if (section && section !== 'archive_search' && item.pcSection !== section) return false;

        const haystack = [
            item.title,
            item.body,
            item.pcSection,
            item.type,
            item.inWorldSource ?? '',
            ...item.searchTerms,
            ...item.suggestedTerms,
            ...item.reveals,
            ...item.unlocks,
            ...item.vocabulary,
        ].join(' ').toLocaleLowerCase('ru');

        const queryMatches = !normalized || haystack.includes(normalized);
        const unlocked = isOldPcItemUnlocked(item, unlockedKeys);

        return queryMatches && (unlocked || normalized.length >= 3);
    });
}

export function collectSuggestedTerms(items: OldPcItem[], unlockedKeys: Set<string>): string[] {
    const terms = new Set<string>();

    for (const item of items) {
        if (!isOldPcItemUnlocked(item, unlockedKeys)) continue;
        for (const term of item.suggestedTerms) terms.add(term);
        for (const term of item.searchTerms.slice(0, 2)) terms.add(term);
    }

    return [...terms].slice(0, 18);
}
