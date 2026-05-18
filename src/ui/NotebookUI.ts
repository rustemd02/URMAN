import { KNOWLEDGE_KEY_BY_ID } from '../data/knowledge_keys';
import { VOCABULARY_ENTRIES } from '../data/vocabulary_data';
import { Game } from '../game/Game';

type JournalTab = 'clues' | 'contradictions' | 'marat' | 'vocabulary' | 'route';

const TAB_LABELS: Record<JournalTab, string> = {
    clues: 'Улики',
    contradictions: 'Противоречия',
    marat: 'Марат',
    vocabulary: 'Словарь',
    route: 'Маршрут',
};

export class NotebookUI {
    private element: HTMLElement;
    private isOpen = false;
    private activeTab: JournalTab = 'clues';

    constructor(private readonly game: Game) {
        this.element = document.createElement('div');
        this.element.id = 'notebook-ui';
        this.element.style.display = 'none';
        document.body.appendChild(this.element);
        window.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && this.isOpen) this.close();
        });
        this.element.addEventListener('click', (event) => this.handleClick(event));
    }

    public open(): void {
        this.isOpen = true;
        this.element.style.display = 'block';
        this.render();
        requestAnimationFrame(() => this.element.classList.add('open'));
    }

    public close(): void {
        this.isOpen = false;
        this.element.classList.remove('open');
        window.setTimeout(() => {
            if (!this.isOpen) this.element.style.display = 'none';
        }, 180);
    }

    private handleClick(event: MouseEvent): void {
        const target = event.target;
        if (!(target instanceof HTMLElement)) return;
        const close = target.closest('[data-journal-close]');
        if (close) {
            this.close();
            return;
        }
        const tab = target.closest<HTMLElement>('[data-journal-tab]')?.dataset.journalTab as JournalTab | undefined;
        if (tab) {
            this.activeTab = tab;
            this.render();
        }
    }

    private render(): void {
        const state = this.game.state;
        this.element.innerHTML = `
            ${this.renderStyles()}
            <section class="journal-shell" role="dialog" aria-label="Журнал расследования">
                <button class="journal-close" data-journal-close>×</button>
                <header class="journal-header">
                    <div>
                        <h1>Журнал Айдара</h1>
                        <p>Кырлай · давление ${state.pressureLevel}/3 · улик ${state.knownKeys.length}</p>
                    </div>
                    <button class="journal-save" data-journal-close onclick="window.URMAN?.saveSystem?.save()">Сохранить</button>
                </header>
                <nav class="journal-tabs">
                    ${(Object.keys(TAB_LABELS) as JournalTab[]).map((tab) => `
                        <button class="${this.activeTab === tab ? 'active' : ''}" data-journal-tab="${tab}">${TAB_LABELS[tab]}</button>
                    `).join('')}
                </nav>
                <main class="journal-content">
                    ${this.renderTab()}
                </main>
            </section>
        `;
    }

    private renderTab(): string {
        if (this.activeTab === 'clues') return this.renderClues();
        if (this.activeTab === 'contradictions') return this.renderContradictions();
        if (this.activeTab === 'marat') return this.renderMaratTimeline();
        if (this.activeTab === 'vocabulary') return this.renderVocabulary();
        return this.renderRoute();
    }

    private renderClues(): string {
        const known = this.game.state.knownKeys
            .map((id) => KNOWLEDGE_KEY_BY_ID.get(id))
            .filter((key) => key && key.type !== 'word');
        if (!known.length) return this.empty('Пока нет внешних улик. Старый ПК должен дать первую справку о Марате.');
        return `<div class="journal-grid">${known.map((key) => key ? this.renderKeyCard(key.id) : '').join('')}</div>`;
    }

    private renderContradictions(): string {
        const hasContradiction = this.game.state.hasKnowledgeKey('contradiction_marat_official_vs_internal');
        if (!hasContradiction) {
            return this.empty('Нужна пара источников: официальная версия и внутренняя строка реестра.');
        }
        return `
            <article class="journal-card danger-2">
                <span>противоречие</span>
                <h2>Марат: медицина против внутреннего учёта</h2>
                <p>Официально дело закрыто как несчастный случай, но внутренний реестр использует категорию «граница / ответил».</p>
                <small>Проверить у Рината. Это опасный ключ, а не обычный вопрос.</small>
            </article>
        `;
    }

    private renderMaratTimeline(): string {
        const hasOfficial = this.game.state.hasKnowledgeKey('clue_marat_official_death_version');
        const hasBoundary = this.game.state.hasKnowledgeKey('clue_marat_case_boundary_marker');
        const hasFear = this.game.state.hasKnowledgeKey('clue_marat_was_afraid_before_death');
        return `
            <ol class="timeline">
                <li class="done"><b>До приезда</b><span>Марат остаётся эмоциональным крючком Айдара, а не просто именем в архиве.</span></li>
                <li class="${hasOfficial ? 'done' : ''}"><b>Официальная версия</b><span>${hasOfficial ? 'ФАП закрыл дело удобной формулировкой.' : 'Найти справку на ПК.'}</span></li>
                <li class="${hasBoundary ? 'done' : ''}"><b>Внутренняя трещина</b><span>${hasBoundary ? 'Появилась отметка «граница / ответил».' : 'Искать реестр после справки.'}</span></li>
                <li class="${hasFear ? 'done' : ''}"><b>Личный след</b><span>${hasFear ? 'Марат боялся до смерти.' : 'Найти сохранённое сообщение.'}</span></li>
            </ol>
        `;
    }

    private renderVocabulary(): string {
        return `
            <div class="journal-grid">
                ${VOCABULARY_ENTRIES.map((entry) => {
                    const state = this.game.state.vocabularyStates[entry.id] ?? entry.stateDefault;
                    return `
                        <article class="journal-card word ${state}">
                            <span>${state}</span>
                            <h2>${entry.tatar}</h2>
                            <p>${state === 'unknown' ? 'Значение ещё не подтверждено.' : `${entry.russian}. ${entry.context}`}</p>
                            <small>Использование: ${entry.gameplayUse}; re-read: ${entry.reReadTargets.join(', ')}</small>
                        </article>
                    `;
                }).join('')}
            </div>
        `;
    }

    private renderRoute(): string {
        const routeHint = this.game.state.hasKnowledgeKey('route_kara_urman_edge_hint');
        return `
            <article class="journal-card route-card ${routeHint ? 'danger-2' : ''}">
                <span>схема</span>
                <h2>${routeHint ? 'Тропа за зиратом отмечена' : 'Маршрут ещё неполный'}</h2>
                <p>${routeHint ? 'Старый ПК связал зират, голос и кромку Кара-Урмана. Это причина идти к границе, а не случайный маркер.' : 'Открывай маршрутные узлы и документы, чтобы схема стала полезной.'}</p>
                <small>Открыто узлов: ${this.game.state.routeDiscoveredNodeIds.length}; обновлений схемы: ${this.game.state.routeJournalUpdateIds.length}</small>
            </article>
        `;
    }

    private renderKeyCard(keyId: string): string {
        const key = KNOWLEDGE_KEY_BY_ID.get(keyId);
        if (!key) return '';
        return `
            <article class="journal-card danger-${key.dangerLevel}">
                <span>${key.type} · риск ${key.dangerLevel}/3</span>
                <h2>${key.title}</h2>
                <p>${key.summary}</p>
                <small>${key.tags.join(' · ')}</small>
            </article>
        `;
    }

    private empty(text: string): string {
        return `<div class="journal-empty">${text}</div>`;
    }

    private renderStyles(): string {
        return `
            <style>
                #notebook-ui {
                    position: fixed;
                    inset: 0;
                    z-index: 10005;
                    background: rgba(5, 4, 3, 0.62);
                    opacity: 0;
                    transition: opacity 180ms ease;
                    font-family: "Philosopher", Georgia, serif;
                    color: #241c13;
                }

                #notebook-ui.open { opacity: 1; }

                .journal-shell {
                    position: absolute;
                    top: 50%;
                    left: 50%;
                    width: min(980px, calc(100vw - 28px));
                    height: min(720px, calc(100vh - 28px));
                    transform: translate(-50%, -50%) scale(0.98);
                    padding: 24px;
                    border-radius: 8px;
                    background: #efe4c8;
                    box-shadow: 0 30px 80px rgba(0,0,0,0.46);
                    overflow: hidden;
                    display: grid;
                    grid-template-rows: auto auto 1fr;
                    gap: 16px;
                }

                .journal-close {
                    position: absolute;
                    top: 10px;
                    right: 12px;
                    width: 34px;
                    height: 34px;
                    border: 0;
                    background: transparent;
                    font-size: 30px;
                    cursor: pointer;
                    color: #5b251e;
                }

                .journal-header {
                    display: flex;
                    justify-content: space-between;
                    gap: 16px;
                    padding-right: 34px;
                    border-bottom: 1px solid rgba(67, 45, 26, 0.18);
                }

                .journal-header h1 {
                    margin: 0;
                    font-size: 34px;
                }

                .journal-header p {
                    margin: 4px 0 14px;
                    color: rgba(36, 28, 19, 0.62);
                }

                .journal-save,
                .journal-tabs button {
                    height: 36px;
                    padding: 0 14px;
                    border: 1px solid rgba(67, 45, 26, 0.24);
                    border-radius: 4px;
                    background: rgba(255,255,255,0.28);
                    color: #241c13;
                    cursor: pointer;
                    font: inherit;
                }

                .journal-tabs {
                    display: flex;
                    gap: 8px;
                    flex-wrap: wrap;
                }

                .journal-tabs button.active {
                    background: #3f3020;
                    color: #efe4c8;
                }

                .journal-content {
                    min-height: 0;
                    overflow: auto;
                    padding-right: 6px;
                }

                .journal-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
                    gap: 12px;
                }

                .journal-card {
                    min-height: 150px;
                    padding: 16px;
                    border: 1px solid rgba(67, 45, 26, 0.18);
                    border-radius: 6px;
                    background: rgba(255,255,255,0.34);
                }

                .journal-card span,
                .journal-card small {
                    display: block;
                    color: rgba(36, 28, 19, 0.55);
                    font-size: 12px;
                    letter-spacing: 0.04em;
                }

                .journal-card h2 {
                    margin: 8px 0;
                    font-size: 22px;
                    line-height: 1.08;
                }

                .journal-card p {
                    margin: 0 0 12px;
                    line-height: 1.42;
                }

                .danger-2,
                .danger-3 {
                    border-color: rgba(110, 42, 27, 0.42);
                    background: rgba(136, 70, 38, 0.16);
                }

                .word.unknown {
                    opacity: 0.58;
                }

                .timeline {
                    display: grid;
                    gap: 10px;
                    margin: 0;
                    padding: 0;
                    list-style: none;
                }

                .timeline li {
                    padding: 14px 16px;
                    border-radius: 6px;
                    background: rgba(255,255,255,0.26);
                    border-left: 4px solid rgba(67,45,26,0.2);
                }

                .timeline li.done {
                    border-left-color: #6d5b2f;
                }

                .timeline b,
                .timeline span {
                    display: block;
                }

                .timeline span {
                    margin-top: 4px;
                    color: rgba(36, 28, 19, 0.68);
                }

                .journal-empty {
                    padding: 22px;
                    border-radius: 6px;
                    background: rgba(255,255,255,0.28);
                    color: rgba(36, 28, 19, 0.62);
                }
            </style>
        `;
    }
}
