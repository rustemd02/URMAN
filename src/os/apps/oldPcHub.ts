import {
    collectSuggestedTerms,
    isOldPcItemUnlocked,
    makeQueryKey,
    normalizeOldPcTerm,
    OLD_PC_BOOT_KEYS,
    OLD_PC_ITEMS,
    OLD_PC_SECTION_LABELS,
    OldPcItem,
    OldPcSection,
    searchOldPcItems,
} from '../data/oldPcContent';

const STORAGE_KEY = 'urman.oldPcHub.state.v1';

interface OldPcState {
    activeSection: OldPcSection;
    activeItemId: string | null;
    query: string;
    unlockedKeys: string[];
    savedClues: string[];
    recentEvents: string[];
}

const SECTION_ORDER = Object.keys(OLD_PC_SECTION_LABELS) as OldPcSection[];

const TYPE_LABELS: Record<OldPcItem['type'], string> = {
    document: 'документ',
    record: 'реестр',
    message: 'сообщение',
    tatarwiki_article: 'Татарвики',
    folder_note: 'заметка',
    corrupted_fragment: 'повреждено',
};

const RELIABILITY_HINTS: Record<OldPcItem['reliability'], string> = {
    official_lie: 'официальная версия',
    partial_truth: 'частичная запись',
    personal_memory: 'личная память',
    village_record: 'деревенский учет',
    pact_record: 'внутренний учет',
    folklore_mask: 'фольклорная маска',
    corrupted: 'поврежденный источник',
    unverified: 'непроверено',
};

function escapeHtml(value = '') {
    return String(value)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}

function normalizeState(raw?: Partial<OldPcState>): OldPcState {
    const unlocked = new Set([...(raw?.unlockedKeys ?? []), ...OLD_PC_BOOT_KEYS]);
    return {
        activeSection: raw?.activeSection && SECTION_ORDER.includes(raw.activeSection) ? raw.activeSection : 'archive_search',
        activeItemId: raw?.activeItemId ?? null,
        query: raw?.query ?? '',
        unlockedKeys: [...unlocked],
        savedClues: raw?.savedClues ?? [],
        recentEvents: raw?.recentEvents ?? ['Компьютер включился. Архив доступен из дома Мансура.'],
    };
}

function readState(initialSection: OldPcSection): OldPcState {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        const parsed = raw ? JSON.parse(raw) : {};
        const state = normalizeState(parsed);
        state.activeSection = initialSection;
        return state;
    } catch {
        return normalizeState({ activeSection: initialSection });
    }
}

function writeState(state: OldPcState) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}

function markdownToHtml(body: string, unlockedKeys: Set<string>) {
    const lines = body.split('\n');
    let inTable = false;

    const html = lines.map((line) => {
        const trimmed = line.trim();
        if (!trimmed) {
            if (inTable) {
                inTable = false;
                return '</table>';
            }
            return '';
        }

        if (trimmed.startsWith('|')) {
            const cells = trimmed.split('|').slice(1, -1).map((cell) => cell.trim());
            if (cells.every((cell) => /^-+$/.test(cell.replaceAll(' ', '')))) return '';
            const row = `<tr>${cells.map((cell) => `<td>${renderInline(cell, unlockedKeys)}</td>`).join('')}</tr>`;
            if (!inTable) {
                inTable = true;
                return `<table class="oldpc-doc-table">${row}`;
            }
            return row;
        }

        if (inTable) {
            inTable = false;
            return `</table>${renderBlock(trimmed, unlockedKeys)}`;
        }

        return renderBlock(trimmed, unlockedKeys);
    });

    if (inTable) html.push('</table>');
    return html.join('');
}

function renderBlock(line: string, unlockedKeys: Set<string>) {
    if (line.startsWith('# ')) return `<h2>${renderInline(line.slice(2), unlockedKeys)}</h2>`;
    if (line.startsWith('- ')) return `<div class="oldpc-bullet">■ ${renderInline(line.slice(2), unlockedKeys)}</div>`;
    return `<p>${renderInline(line, unlockedKeys)}</p>`;
}

function renderInline(text: string, unlockedKeys: Set<string>) {
    const escaped = escapeHtml(text);
    return escaped
        .replace(/`([^`]+)`/g, '<code>$1</code>')
        .replace(/\b(урман|тавыш|җавап|Шүрәле|Кара-Урман|Марат|Ринат)\b/giu, (match) => {
            const key = makeQueryKey(match);
            const knownClass = unlockedKeys.has(key) ? ' known' : '';
            return `<button class="oldpc-inline-term${knownClass}" data-term="${escapeHtml(match)}">${escapeHtml(match)}</button>`;
        });
}

function itemUnlockText(item: OldPcItem, unlockedKeys: Set<string>) {
    const missing = item.requires.filter((key) => !unlockedKeys.has(key));
    if (!missing.length) return '';
    return `Требуется: ${missing.map((key) => key.replace(/^clue_/, 'улика: ').replace(/^tt_/, 'слово: ').replace(/^query_/, 'поиск: ')).join(', ')}`;
}

function addEvent(state: OldPcState, text: string) {
    state.recentEvents = [text, ...state.recentEvents.filter((event) => event !== text)].slice(0, 6);
}

function unlockFromItem(state: OldPcState, item: OldPcItem) {
    const keys = new Set(state.unlockedKeys);
    const before = keys.size;
    keys.add(item.id);
    item.reveals.forEach((key) => keys.add(key));
    item.unlocks.forEach((key) => keys.add(key));
    item.vocabulary.forEach((key) => keys.add(key));
    item.searchTerms.forEach((term) => keys.add(makeQueryKey(term)));
    item.suggestedTerms.forEach((term) => keys.add(makeQueryKey(term)));
    state.unlockedKeys = [...keys];

    if (keys.size > before) {
        addEvent(state, `Новые ключи из файла: ${item.title}`);
    }
}

function syncSharedOpen(item: OldPcItem) {
    const game = (window as any).URMAN;
    game?.investigation?.registerOldPcOpen({
        id: item.id,
        title: item.title,
        dangerLevel: item.dangerLevel,
        reveals: item.reveals,
        contradicts: item.contradicts,
        unlocks: item.unlocks,
        vocabulary: item.vocabulary,
    });
    game?.saveSystem?.save();
}

function syncSharedSave(item: OldPcItem) {
    const game = (window as any).URMAN;
    game?.investigation?.saveOldPcEvidence({
        id: item.id,
        title: item.title,
        dangerLevel: item.dangerLevel,
        reveals: item.reveals,
        contradicts: item.contradicts,
        unlocks: item.unlocks,
        vocabulary: item.vocabulary,
    });
    game?.saveSystem?.save();
}

function renderSectionTabs(activeSection: OldPcSection) {
    return SECTION_ORDER.map((section) => `
        <button class="oldpc-section ${section === activeSection ? 'active' : ''}" data-section="${section}">
            <span>${escapeHtml(OLD_PC_SECTION_LABELS[section])}</span>
            <strong>${OLD_PC_ITEMS.filter((item) => item.pcSection === section).length}</strong>
        </button>
    `).join('');
}

function renderResults(items: OldPcItem[], state: OldPcState, unlockedKeys: Set<string>) {
    if (!items.length) {
        return `<div class="oldpc-empty">Ничего не найдено. Попробуй имя, место, татарское слово или термин из документа.</div>`;
    }

    return items.map((item) => {
        const unlocked = isOldPcItemUnlocked(item, unlockedKeys);
        const active = state.activeItemId === item.id;
        const danger = '●'.repeat(item.dangerLevel) || '○';
        const lockText = unlocked ? '' : itemUnlockText(item, unlockedKeys);

        return `
            <button class="oldpc-result ${active ? 'active' : ''} ${unlocked ? '' : 'locked'}" data-item-id="${escapeHtml(item.id)}">
                <span class="oldpc-result-top">
                    <strong>${escapeHtml(item.title)}</strong>
                    <em>${escapeHtml(TYPE_LABELS[item.type])}</em>
                </span>
                <span class="oldpc-result-meta">
                    ${escapeHtml(OLD_PC_SECTION_LABELS[item.pcSection])} · ${escapeHtml(RELIABILITY_HINTS[item.reliability])} · риск ${danger}
                </span>
                ${lockText ? `<span class="oldpc-lock">${escapeHtml(lockText)}</span>` : ''}
            </button>
        `;
    }).join('');
}

function renderDocPanel(item: OldPcItem | undefined, unlockedKeys: Set<string>, savedClues: Set<string>) {
    if (!item) {
        return `
            <div class="oldpc-doc-placeholder">
                <div class="oldpc-crt-mark">C:\\KYRLAY\\ARCHIVE</div>
                <h2>Выбери файл</h2>
                <p>ПК Мансура хранит бытовые папки, старые сообщения и внутренний учет деревни. Начни с поиска по имени Марата или открой видимый раздел слева.</p>
            </div>
        `;
    }

    const unlocked = isOldPcItemUnlocked(item, unlockedKeys);
    if (!unlocked) {
        return `
            <div class="oldpc-doc-locked">
                <div class="oldpc-lock-icon">▣</div>
                <h2>${escapeHtml(item.title)}</h2>
                <p>${escapeHtml(itemUnlockText(item, unlockedKeys))}</p>
                <p>Файл есть в индексе, но старый архив не показывает содержимое без нужного слова, документа или разговора.</p>
            </div>
        `;
    }

    return `
        <article class="oldpc-document">
            <header>
                <div>
                    <div class="oldpc-path">C:\\KYRLAY\\${escapeHtml(item.pcSection)}\\${escapeHtml(item.id)}.txt</div>
                    <h2>${escapeHtml(item.title)}</h2>
                </div>
                <button class="oldpc-save-clue ${savedClues.has(item.id) ? 'saved' : ''}" data-save-clue="${escapeHtml(item.id)}">
                    ${savedClues.has(item.id) ? 'Улика сохранена' : 'Сохранить улику'}
                </button>
            </header>

            <div class="oldpc-stamps">
                <span>${escapeHtml(item.inWorldSource ?? 'локальный архив')}</span>
                <span>${escapeHtml(RELIABILITY_HINTS[item.reliability])}</span>
                <span>опасность ${item.dangerLevel}/3</span>
            </div>

            <div class="oldpc-doc-body">
                ${markdownToHtml(item.body, unlockedKeys)}
            </div>

            <footer class="oldpc-doc-footer">
                ${item.reveals.map((key) => `<button class="oldpc-key" data-key="${escapeHtml(key)}">${escapeHtml(key.replace(/^clue_/, 'улика: '))}</button>`).join('')}
                ${item.suggestedTerms.map((term) => `<button class="oldpc-term" data-term="${escapeHtml(term)}">${escapeHtml(term)}</button>`).join('')}
            </footer>
        </article>
    `;
}

function renderSavedClues(state: OldPcState) {
    if (!state.savedClues.length) return '<div class="oldpc-muted">Пока пусто. Сохраняй сильные документы как улики.</div>';

    return state.savedClues.map((id) => {
        const item = OLD_PC_ITEMS.find((candidate) => candidate.id === id);
        return `<button class="oldpc-saved-clue" data-item-id="${escapeHtml(id)}">${escapeHtml(item?.title ?? id)}</button>`;
    }).join('');
}

function renderShell(root: HTMLElement, state: OldPcState) {
    const unlockedKeys = new Set(state.unlockedKeys);
    const results = searchOldPcItems(state.query, unlockedKeys, state.activeSection);
    const activeItem = OLD_PC_ITEMS.find((item) => item.id === state.activeItemId);
    const suggestedTerms = collectSuggestedTerms(OLD_PC_ITEMS, unlockedKeys);

    root.innerHTML = `
        <div class="oldpc-hub">
            <aside class="oldpc-sidebar">
                <div class="oldpc-brand">
                    <strong>Кырлай архив</strong>
                    <span>дом Мансура · локальная копия</span>
                </div>
                <nav>${renderSectionTabs(state.activeSection)}</nav>
                <div class="oldpc-clues">
                    <h3>Сохраненные улики</h3>
                    ${renderSavedClues(state)}
                </div>
            </aside>

            <main class="oldpc-main">
                <section class="oldpc-searchbar">
                    <input class="oldpc-search-input" value="${escapeHtml(state.query)}" placeholder="Поиск: Марат, реестр, урман, граница..." />
                    <button class="oldpc-search-btn">Искать</button>
                    <button class="oldpc-reset-btn">Сбросить сессию</button>
                </section>

                <section class="oldpc-suggested">
                    ${suggestedTerms.map((term) => `<button class="oldpc-term" data-term="${escapeHtml(term)}">${escapeHtml(term)}</button>`).join('')}
                </section>

                <div class="oldpc-workspace">
                    <section class="oldpc-results">
                        <div class="oldpc-panel-title">
                            <strong>${escapeHtml(OLD_PC_SECTION_LABELS[state.activeSection])}</strong>
                            <span>${results.length} файлов</span>
                        </div>
                        ${renderResults(results, state, unlockedKeys)}
                    </section>

                    <section class="oldpc-reader">
                        ${renderDocPanel(activeItem, unlockedKeys, new Set(state.savedClues))}
                    </section>
                </div>

                <section class="oldpc-events">
                    ${state.recentEvents.map((event) => `<span>${escapeHtml(event)}</span>`).join('')}
                </section>
            </main>
        </div>
    `;
}

export const renderOldPcHub = (initialSection: OldPcSection = 'archive_search') => `
    <div class="oldpc-hub-root" data-initial-section="${initialSection}"></div>
`;

export const initOldPcHub = (root: HTMLElement | Document = document, initialSection?: OldPcSection) => {
    const app = root instanceof HTMLElement
        ? root.querySelector('.oldpc-hub-root') as HTMLElement
        : document.querySelector('.oldpc-hub-root') as HTMLElement;

    if (!app) return;

    const startSection = initialSection ?? (app.dataset.initialSection as OldPcSection | undefined) ?? 'archive_search';
    let state = readState(startSection);

    const saveAndRender = () => {
        writeState(state);
        renderShell(app, state);
    };

    app.addEventListener('click', (event) => {
        const target = event.target as HTMLElement;
        const sectionButton = target.closest<HTMLButtonElement>('.oldpc-section');
        const termButton = target.closest<HTMLButtonElement>('[data-term]');
        const resultButton = target.closest<HTMLButtonElement>('[data-item-id]');
        const saveButton = target.closest<HTMLButtonElement>('[data-save-clue]');
        const resetButton = target.closest<HTMLButtonElement>('.oldpc-reset-btn');
        const searchButton = target.closest<HTMLButtonElement>('.oldpc-search-btn');

        if (sectionButton?.dataset.section) {
            state.activeSection = sectionButton.dataset.section as OldPcSection;
            state.query = '';
            state.activeItemId = null;
            addEvent(state, `Открыт раздел: ${OLD_PC_SECTION_LABELS[state.activeSection]}`);
            saveAndRender();
            return;
        }

        if (termButton?.dataset.term) {
            const term = termButton.dataset.term;
            state.query = term;
            state.activeSection = 'archive_search';
            state.unlockedKeys = [...new Set([...state.unlockedKeys, makeQueryKey(term), `term_${normalizeOldPcTerm(term)}`])];
            addEvent(state, `Поиск по термину: ${term}`);
            saveAndRender();
            return;
        }

        if (saveButton?.dataset.saveClue) {
            const id = saveButton.dataset.saveClue;
            const item = OLD_PC_ITEMS.find((candidate) => candidate.id === id);
            state.savedClues = [...new Set([...state.savedClues, id])];
            state.unlockedKeys = [...new Set([...state.unlockedKeys, `saved_${id}`])];
            if (item) syncSharedSave(item);
            addEvent(state, `Улика сохранена: ${item?.title ?? id}`);
            saveAndRender();
            return;
        }

        if (resultButton?.dataset.itemId) {
            const item = OLD_PC_ITEMS.find((candidate) => candidate.id === resultButton.dataset.itemId);
            if (!item) return;
            state.activeItemId = item.id;
            if (isOldPcItemUnlocked(item, new Set(state.unlockedKeys))) {
                unlockFromItem(state, item);
                syncSharedOpen(item);
            } else {
                addEvent(state, `Файл пока закрыт: ${item.title}`);
            }
            saveAndRender();
            return;
        }

        if (resetButton) {
            localStorage.removeItem(STORAGE_KEY);
            state = normalizeState({ activeSection: startSection });
            saveAndRender();
            return;
        }

        if (searchButton) {
            const input = app.querySelector<HTMLInputElement>('.oldpc-search-input');
            state.query = input?.value ?? '';
            state.activeSection = 'archive_search';
            state.unlockedKeys = [...new Set([...state.unlockedKeys, makeQueryKey(state.query)])];
            addEvent(state, `Поиск: ${state.query || 'все файлы'}`);
            saveAndRender();
        }
    });

    app.addEventListener('keydown', (event) => {
        const target = event.target as HTMLElement;
        if (!target.classList.contains('oldpc-search-input') || event.key !== 'Enter') return;
        state.query = (target as HTMLInputElement).value;
        state.activeSection = 'archive_search';
        state.unlockedKeys = [...new Set([...state.unlockedKeys, makeQueryKey(state.query)])];
        addEvent(state, `Поиск: ${state.query || 'все файлы'}`);
        saveAndRender();
    });

    saveAndRender();
};
