export type OldPcSection = string;

export interface OldPcHubDocument {
    id: string;
    title: string;
    bodyMarkdown: string;
    oldPc: {
        pcSection: string;
        type: string;
        reliability: string;
        suggestedTerms: readonly string[];
    };
}

export interface OldPcHubModel {
    activeDocumentId: string | null;
    activeSection: string;
    query: string;
    savedDocumentIds: readonly string[];
    results: readonly OldPcHubDocument[];
    suggestedTerms: readonly string[];
}

export interface OldPcHubController {
    render(): OldPcHubModel;
    handle(input: { type: 'search' | 'open' | 'save' | 'section'; query?: string; documentId?: string; section?: string }): unknown;
    subscribe?(listener: (model: OldPcHubModel) => void): { dispose(): void };
}

function escapeHtml(value = '') {
    return String(value)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}

function markdownToHtml(body: string) {
    let inTable = false;
    const lines = body.split('\n').map((line) => {
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
            const row = `<tr>${cells.map((cell) => `<td>${escapeHtml(cell)}</td>`).join('')}</tr>`;
            if (!inTable) {
                inTable = true;
                return `<table class="oldpc-doc-table">${row}`;
            }
            return row;
        }
        if (inTable) {
            inTable = false;
            return `</table>${renderBlock(trimmed)}`;
        }
        return renderBlock(trimmed);
    });
    if (inTable) lines.push('</table>');
    return lines.join('');
}

function renderBlock(line: string) {
    if (line.startsWith('# ')) return `<h2>${escapeHtml(line.slice(2))}</h2>`;
    if (line.startsWith('- ')) return `<div class="oldpc-bullet">■ ${escapeHtml(line.slice(2))}</div>`;
    return `<p>${escapeHtml(line)}</p>`;
}

function sectionLabel(section: string) {
    return section.replaceAll('_', ' ');
}

function reliabilityLabel(reliability: string) {
    return reliability.replaceAll('_', ' ');
}

function allSections(model: OldPcHubModel) {
    return [...new Set(model.results.map((item) => item.oldPc.pcSection).concat([model.activeSection]))].sort();
}

function renderResults(model: OldPcHubModel) {
    if (!model.results.length) {
        return '<div class="oldpc-empty">Ничего не найдено. Попробуй имя, место или термин из документа.</div>';
    }
    return model.results.map((item) => {
        const active = item.id === model.activeDocumentId;
        return `
            <button class="oldpc-result ${active ? 'active' : ''}" data-item-id="${escapeHtml(item.id)}">
                <span class="oldpc-result-top"><strong>${escapeHtml(item.title)}</strong><em>${escapeHtml(item.oldPc.type)}</em></span>
                <span class="oldpc-result-meta">${escapeHtml(sectionLabel(item.oldPc.pcSection))} · ${escapeHtml(reliabilityLabel(item.oldPc.reliability))}</span>
            </button>
        `;
    }).join('');
}

function renderReader(model: OldPcHubModel) {
    const document = model.results.find((item) => item.id === model.activeDocumentId);
    if (!document) {
        return '<div class="oldpc-doc-placeholder"><div class="oldpc-crt-mark">C:\\KYRLAY\\ARCHIVE</div><h2>Выбери файл</h2><p>Архив отображает только content records, разрешённые runtime-ядром.</p></div>';
    }
    const saved = model.savedDocumentIds.includes(document.id);
    return `
        <article class="oldpc-document">
            <header>
                <div><div class="oldpc-path">C:\\KYRLAY\\${escapeHtml(document.oldPc.pcSection)}\\${escapeHtml(document.id)}.txt</div><h2>${escapeHtml(document.title)}</h2></div>
                <button class="oldpc-save-clue ${saved ? 'saved' : ''}" data-save-clue="${escapeHtml(document.id)}">${saved ? 'Улика сохранена' : 'Сохранить улику'}</button>
            </header>
            <div class="oldpc-stamps"><span>${escapeHtml(reliabilityLabel(document.oldPc.reliability))}</span></div>
            <div class="oldpc-doc-body">${markdownToHtml(document.bodyMarkdown)}</div>
            <footer class="oldpc-doc-footer">${document.oldPc.suggestedTerms.map((term) => `<button class="oldpc-term" data-term="${escapeHtml(term)}">${escapeHtml(term)}</button>`).join('')}</footer>
        </article>
    `;
}

function renderShell(root: HTMLElement, model: OldPcHubModel) {
    const sections = allSections(model);
    root.innerHTML = `
        <div class="oldpc-hub">
            <aside class="oldpc-sidebar">
                <div class="oldpc-brand"><strong>Кырлай архив</strong><span>локальная копия</span></div>
                <nav>${sections.map((section) => `<button class="oldpc-section ${section === model.activeSection ? 'active' : ''}" data-section="${escapeHtml(section)}"><span>${escapeHtml(sectionLabel(section))}</span></button>`).join('')}</nav>
                <div class="oldpc-clues"><h3>Сохранённые улики</h3><span>${model.savedDocumentIds.length}</span></div>
            </aside>
            <main class="oldpc-main">
                <section class="oldpc-searchbar"><input class="oldpc-search-input" value="${escapeHtml(model.query)}" placeholder="Поиск: Марат, реестр, урман, граница..." /><button class="oldpc-search-btn">Искать</button></section>
                <section class="oldpc-suggested">${model.suggestedTerms.map((term) => `<button class="oldpc-term" data-term="${escapeHtml(term)}">${escapeHtml(term)}</button>`).join('')}</section>
                <div class="oldpc-workspace"><section class="oldpc-results"><div class="oldpc-panel-title"><strong>${escapeHtml(sectionLabel(model.activeSection))}</strong><span>${model.results.length} файлов</span></div>${renderResults(model)}</section><section class="oldpc-reader">${renderReader(model)}</section></div>
            </main>
        </div>
    `;
}

export const renderOldPcHub = () => '<div class="oldpc-hub-root"></div>';

/** Browser-only renderer. The controller is a capability adapter; it owns neither records nor progression. */
export const initOldPcHub = (root: HTMLElement, controller: OldPcHubController) => {
    const app = root.querySelector('.oldpc-hub-root') as HTMLElement | null;
    if (!app) return () => undefined;
    const render = (model = controller.render()) => renderShell(app, model);
    const click = (event: Event) => {
        const target = event.target as HTMLElement;
        const section = target.closest<HTMLButtonElement>('[data-section]')?.dataset.section;
        const term = target.closest<HTMLButtonElement>('[data-term]')?.dataset.term;
        const itemId = target.closest<HTMLButtonElement>('[data-item-id]')?.dataset.itemId;
        const saveId = target.closest<HTMLButtonElement>('[data-save-clue]')?.dataset.saveClue;
        const searchButton = target.closest<HTMLButtonElement>('.oldpc-search-btn');
        if (section) controller.handle({ type: 'section', section });
        else if (term) controller.handle({ type: 'search', query: term });
        else if (itemId) controller.handle({ type: 'open', documentId: itemId });
        else if (saveId) controller.handle({ type: 'save', documentId: saveId });
        else if (searchButton) controller.handle({ type: 'search', query: (app.querySelector('.oldpc-search-input') as HTMLInputElement | null)?.value ?? '' });
        render();
    };
    const keydown = (event: KeyboardEvent) => {
        if (event.key !== 'Enter' || !(event.target instanceof HTMLInputElement) || !event.target.classList.contains('oldpc-search-input')) return;
        controller.handle({ type: 'search', query: event.target.value });
        render();
    };
    app.addEventListener('click', click);
    app.addEventListener('keydown', keydown);
    const subscription = controller.subscribe?.((model) => render(model));
    render();
    return () => {
        subscription?.dispose();
        app.removeEventListener('click', click);
        app.removeEventListener('keydown', keydown);
    };
};
