import type { RuntimePresentationPort, RuntimeSceneSession } from '../runtime/bootstrap/runtime-bootstrap.mjs';
import type { ProductionPersistenceGateway } from '../runtime/persistence/production-persistence-gateway.mjs';
import type { OldPcHubController, OldPcHubModel } from '../os/apps/oldPcHub';
import { presentationInputFor } from './presentation-input.mjs';

type InputHandler = (input: Readonly<Record<string, unknown>>) => Promise<unknown> | unknown;

type PresentationModel = {
    readonly kind?: string;
    readonly title?: { readonly text?: string } | null;
    readonly texts?: readonly { readonly text?: string }[];
    readonly text?: { readonly text?: string } | null;
    readonly speaker?: {
        readonly characterId?: string;
        readonly portrait?: PresentationAsset | null;
    } | null;
    readonly assets?: readonly { readonly kind?: string; readonly url?: string; readonly accessibility?: { readonly decorative?: boolean } }[];
    readonly interactions?: readonly { readonly id: string; readonly label?: { readonly text?: string } }[];
    readonly actions?: readonly { readonly id: string; readonly label?: { readonly text?: string } }[];
    readonly choices?: readonly { readonly id: string; readonly label?: { readonly text?: string } }[];
};

type PresentationAsset = {
    readonly kind?: string;
    readonly url?: string;
    readonly accessibility?: { readonly decorative?: boolean };
};

type NavigationRequest = {
    readonly targetSceneId: string;
    readonly entryAlreadyCommitted?: boolean;
};

type DialogueHandoffRequest = {
    readonly fromSceneId?: string;
    readonly targetDialogueId: string;
    readonly entryAlreadyCommitted?: boolean;
};

type RuntimeHandoffRequest = NavigationRequest | DialogueHandoffRequest;

type GameOptions = {
    readonly runtime: RuntimePresentationPort;
    readonly persistence: ProductionPersistenceGateway;
};

function isDialogueHandoff(request: RuntimeHandoffRequest): request is DialogueHandoffRequest {
    return 'targetDialogueId' in request;
}

/**
 * Browser composition host. It knows only a compiled campaign, resolved
 * view-models and input events; the runtime facade remains the sole state owner.
 */
export class Game {
    public readonly audio = Object.freeze({
        startAmbience: (_assetId: string) => undefined,
        stopAmbience: (_assetId: string) => undefined,
    });

    private readonly container: HTMLElement;
    private readonly runtime: RuntimePresentationPort;
    private readonly persistence: ProductionPersistenceGateway;
    private activeScene: RuntimeSceneSession | null = null;
    private activeComputer: { destroy(): void } | null = null;
    private activeInput: InputHandler | null = null;
    private activeSceneId: string | null = null;
    private activeDialogueReturnSceneId: string | null = null;
    private navigationTail: Promise<void> = Promise.resolve();

    constructor(container: HTMLElement, { runtime, persistence }: GameOptions) {
        this.container = container;
        this.runtime = runtime;
        this.persistence = persistence;
    }

    public start(): void {
        void this.boot();
    }

    public snapshot(): object {
        return this.runtime.snapshot();
    }

    public async restore(snapshot: object): Promise<void> {
        await this.disposeActiveScene();
        await this.runtime.restore(snapshot);
        await this.openScene(this.runtime.campaign.entrypoint, true);
    }

    public dispose(): void {
        void this.disposeActiveScene();
        this.activeComputer?.destroy();
        this.activeComputer = null;
    }

    /**
     * The old PC is an explicitly launched development module, never a
     * production campaign scene. It receives only the host presentation port.
     */
    public async openDevelopmentOldPc(): Promise<void> {
        if (!import.meta.env.DEV) throw new Error('The isolated old-PC module is available only in development.');
        await this.disposeActiveScene();
        this.activeComputer?.destroy();
        const { ComputerScene } = await import('../scenes/ComputerScene');
        const presentation = this.runtime.oldPcPresentation();
        const oldPcHub: OldPcHubController = {
            render: () => presentation.render() as OldPcHubModel,
            handle: (input) => presentation.handle(input),
            subscribe: (listener) => presentation.subscribe((model) => listener(model as OldPcHubModel)),
        };
        const computer = new ComputerScene(this, { oldPcHub });
        computer.init(this.container);
        this.activeComputer = computer;
    }

    private async boot(): Promise<void> {
        const legacyReset = this.persistence.applyLegacyReset();
        if (legacyReset.status === 'reset-failed') {
            this.renderFailure(new Error(legacyReset.message));
            return;
        }
        const notice = legacyReset.notice ?? undefined;
        try {
            const restored = await this.persistence.restore();
            if (restored.status === 'restored') {
                await this.openScene(this.runtime.campaign.entrypoint, true);
                if (notice) this.announce(notice);
                return;
            }
            if (restored.status === 'reset-required') {
                this.renderCurrentV2ResetConfirmation(restored.message, restored.reset);
                return;
            }
            this.renderCampaignMenu(notice);
        } catch (error) {
            this.renderFailure(error);
        }
    }

    private renderCampaignMenu(announcement?: string): void {
        this.container.innerHTML = `
            <main class="urman-campaign-menu" aria-labelledby="urman-title">
                <h1 id="urman-title">УРМАН</h1>
                <p>Мистический детектив</p>
                <button type="button" data-launch-campaign="${this.escape(this.runtime.campaign.id)}">Начать главу</button>
                <p class="urman-runtime-announcement" aria-live="polite">${this.escape(announcement ?? '')}</p>
            </main>
        `;
        const launch = this.container.querySelector<HTMLButtonElement>('[data-launch-campaign]');
        launch?.addEventListener('click', () => {
            void this.launchCampaign(launch.dataset.launchCampaign ?? '');
        });
    }

    private async launchCampaign(campaignId: string): Promise<void> {
        if (campaignId !== this.runtime.campaign.id) {
            throw new Error(`Campaign ${campaignId} is not part of this production composition.`);
        }
        await this.openScene(this.runtime.campaign.entrypoint, false);
    }

    private renderCurrentV2ResetConfirmation(message: string, proposal: Parameters<ProductionPersistenceGateway['discardCurrentV2Save']>[0]): void {
        this.container.innerHTML = `
            <main class="urman-persistence-reset" aria-labelledby="urman-persistence-reset-title">
                <h1 id="urman-persistence-reset-title">Сохранение нельзя безопасно продолжить</h1>
                <p>${this.escape(message)}</p>
                <p>Подтвердите удаление только текущего сохранения версии 2. Имя, настройки и Content Lab останутся без изменений.</p>
                <button type="button" data-persistence-reset-current-save="true">Удалить текущее сохранение</button>
                <button type="button" data-persistence-return-menu="true">Вернуться в меню</button>
                <p class="urman-runtime-announcement" aria-live="polite"></p>
            </main>
        `;
        const confirm = this.container.querySelector<HTMLButtonElement>('[data-persistence-reset-current-save]');
        confirm?.addEventListener('click', () => {
            const discarded = this.persistence.discardCurrentV2Save(proposal);
            if (discarded.status === 'current-v2-save-discarded') {
                this.renderCampaignMenu('Текущее сохранение удалено. Можно начать новую главу.');
            } else {
                this.announce(`Не удалось удалить текущее сохранение: ${discarded.message}`);
            }
        });
        const menu = this.container.querySelector<HTMLButtonElement>('[data-persistence-return-menu]');
        menu?.addEventListener('click', () => this.renderCampaignMenu());
    }

    private queueNavigation(request: NavigationRequest): void {
        this.navigationTail = this.navigationTail
            .then(() => this.openScene(request.targetSceneId, request.entryAlreadyCommitted === true))
            .catch((error) => this.renderFailure(error));
    }

    private queueHandoff(request: RuntimeHandoffRequest): void {
        const sourceSceneId = isDialogueHandoff(request)
            ? request.fromSceneId ?? this.activeSceneId
            : this.activeSceneId;
        this.navigationTail = this.navigationTail
            .then(() => {
                if (isDialogueHandoff(request)) {
                    return this.openDialogue(request.targetDialogueId, sourceSceneId, request.entryAlreadyCommitted === true);
                }
                return this.openScene(request.targetSceneId, request.entryAlreadyCommitted === true);
            })
            .catch((error) => this.renderFailure(error));
    }

    private async openScene(sceneId: string, entryAlreadyCommitted: boolean): Promise<void> {
        await this.disposeActiveScene();
        this.activeSceneId = sceneId;
        this.activeDialogueReturnSceneId = null;
        const scene = this.runtime.createScene(sceneId, this.presentationHost(), { entryAlreadyCommitted });
        this.activeScene = scene;
        await scene.init();
        this.persistCommittedRun();
    }

    private async openDialogue(dialogueId: string, returnSceneId: string | null, entryAlreadyCommitted: boolean): Promise<void> {
        if (!returnSceneId) throw new Error(`Dialogue ${dialogueId} has no originating scene for a generic return handoff.`);
        await this.disposeActiveScene();
        this.activeSceneId = null;
        this.activeDialogueReturnSceneId = returnSceneId;
        const dialogue = this.runtime.createDialogue(dialogueId, this.presentationHost(), { entryAlreadyCommitted });
        this.activeScene = dialogue;
        await dialogue.init();
    }

    private presentationHost() {
        return {
            present: (model: PresentationModel) => this.renderModel(model),
            subscribeInput: (handler: InputHandler) => {
                const autosavingHandler: InputHandler = async (input) => {
                    const result = await handler(input);
                    if (this.isCommitted(result)) this.persistCommittedRun();
                    return result;
                };
                this.activeInput = autosavingHandler;
                return {
                    dispose: () => {
                        if (this.activeInput === autosavingHandler) this.activeInput = null;
                    },
                };
            },
            navigate: (request: NavigationRequest) => this.queueNavigation(request),
            handoff: (request: RuntimeHandoffRequest) => this.queueHandoff(request),
            startMedia: (resource: { readonly transcript?: { readonly text?: string } }) => {
                if (resource.transcript?.text) this.announce(resource.transcript.text);
                return { dispose: () => undefined };
            },
            reportFailure: (error: unknown) => this.renderFailure(error),
        };
    }

    private async disposeActiveScene(): Promise<void> {
        const previous = this.activeScene;
        this.activeScene = null;
        this.activeInput = null;
        this.activeSceneId = null;
        if (previous) await previous.dispose();
    }

    private renderModel(model: PresentationModel): void {
        const actions = model.interactions ?? model.actions ?? model.choices ?? [];
        const title = model.title?.text ?? model.speaker?.characterId ?? model.kind ?? 'УРМАН';
        const textEntries = model.texts ?? (model.text ? [model.text] : []);
        const paragraphs = textEntries.map((entry) => `<p>${this.escape(entry.text ?? '')}</p>`).join('');
        const visualAssets = [...(model.assets ?? []), ...(model.speaker?.portrait ? [model.speaker.portrait] : [])];
        const assets = visualAssets
            .filter((asset) => asset.kind === 'image' && asset.url)
            .map((asset) => `<img src="${this.escape(asset.url ?? '')}" alt="${asset.accessibility?.decorative ? '' : this.escape(title)}">`)
            .join('');
        const terminalDialogueAction = model.kind === 'dialogue' && actions.length === 0
            ? '<button type="button" data-runtime-dialogue-continue="true">Продолжить</button>'
            : '';
        this.container.innerHTML = `
            <main class="urman-runtime-scene" data-scene-kind="${this.escape(model.kind ?? 'scene')}">
                <h1>${this.escape(title)}</h1>
                <section class="urman-runtime-assets">${assets}</section>
                <section class="urman-runtime-text">${paragraphs}</section>
                <nav class="urman-runtime-actions">${actions.map((action) => `<button type="button" data-runtime-interaction="${this.escape(action.id)}">${this.escape(action.label?.text ?? 'Продолжить')}</button>`).join('')}${terminalDialogueAction}</nav>
                <p class="urman-runtime-announcement" aria-live="polite"></p>
            </main>
        `;
        const dialogueContinue = this.container.querySelector<HTMLButtonElement>('[data-runtime-dialogue-continue]');
        dialogueContinue?.addEventListener('click', () => this.returnFromDialogue());
        for (const button of this.container.querySelectorAll<HTMLButtonElement>('[data-runtime-interaction]')) {
            button.addEventListener('click', () => {
                const interactionId = button.dataset.runtimeInteraction;
                if (!interactionId || !this.activeInput) return;
                void Promise.resolve(this.activeInput(presentationInputFor(model.kind, interactionId))).catch((error) => this.renderFailure(error));
            });
        }
    }

    private returnFromDialogue(): void {
        const sceneId = this.activeDialogueReturnSceneId;
        if (!sceneId) {
            this.renderFailure(new Error('Generic dialogue continuation has no originating scene.'));
            return;
        }
        this.queueNavigation({ targetSceneId: sceneId, entryAlreadyCommitted: true });
    }

    private announce(message: string): void {
        const region = this.container.querySelector<HTMLElement>('.urman-runtime-announcement');
        if (region) region.textContent = message;
    }

    private persistCommittedRun(): void {
        try {
            this.persistence.save();
        } catch (error) {
            const message = error instanceof Error ? error.message : String(error);
            // Saving is outside the already committed kernel transaction.
            this.announce(`Не удалось сохранить прогресс: ${message}`);
        }
    }

    private isCommitted(value: unknown): value is { readonly status: 'committed' } {
        return Boolean(value && typeof value === 'object' && (value as { readonly status?: unknown }).status === 'committed');
    }

    private renderFailure(error: unknown): void {
        const message = error instanceof Error ? error.message : String(error);
        this.container.innerHTML = `<main class="urman-runtime-failure"><h1>Невозможно продолжить</h1><p>${this.escape(message)}</p></main>`;
    }

    private escape(value: string): string {
        return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    }
}
