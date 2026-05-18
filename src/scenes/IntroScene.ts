import { BaseScene } from './BaseScene';

type DialogueMessage = Readonly<{
    side: 'left' | 'right';
    text: string;
    tr: string;
}>;

type BoundElements = {
    systemTime: HTMLElement;
    typingStatus: HTMLElement;
    chatMessages: HTMLElement;
    translationPopup: HTMLElement;
    ticketApp: HTMLElement;
    buyBtn: HTMLButtonElement;
    modalRoot: HTMLElement;
};

const DIALOGUE: readonly DialogueMessage[] = [
    { side: 'left', text: 'Улым, хәлләр ничек? Бабайга шалтыраттыңмы?', tr: 'Сынок, как дела? Дедушке звонил?' },
    { side: 'right', text: 'Салам, әни. Юк әле, хәзер шалтыратам.', tr: 'Привет, мам. Еще нет, сейчас позвоню.' },
    { side: 'left', text: 'Бабаең бик нык авырып китте, ярдәм кирәк аңа. Хуҗалыкта эшләр күп, көз җитте бит.', tr: 'Дедушка сильно приболел, помощь нужна ему. В хозяйстве много дел, осень уже пришла.' },
    { side: 'left', text: 'Хәзер баруың иң яхшы вакыт булыр, минемчә. Кит инде, ярдәм итәрсең.', tr: 'Сейчас, по-моему, самое время поехать. Поезжай уже, поможешь.' },
    { side: 'right', text: 'Әйе, аңладым. Бүген кич белән китәм. Билет алам.', tr: 'Да, понял. Сегодня вечером уеду. Куплю билет.' },
    { side: 'left', text: 'Ярар. Документларыңны онытма. Кем сораса да — бабайны карарга кайттым диярсең.', tr: 'Хорошо. Документы не забудь. Кто бы ни спросил — скажешь, что приехал ухаживать за дедушкой.' }
] as const;

export class IntroScene extends BaseScene {
    private clickCount = 0;
    private readonly SECRET_DATE = '21 сент 2022';

    private els!: BoundElements;
    private disposed = false;
    private timeouts = new Set<number>();
    private purchaseLocked = false;
    private translationHideTimer: number | null = null;

    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = this.renderMarkup();
        this.bindElements();
        this.bindEvents();
        this.startSequence();
    }

    destroy() {
        this.disposed = true;

        for (const id of this.timeouts) {
            window.clearTimeout(id);
        }
        this.timeouts.clear();

        if (this.translationHideTimer !== null) {
            window.clearTimeout(this.translationHideTimer);
            this.translationHideTimer = null;
        }

        super.destroy();
    }

    private renderMarkup(): string {
        return `
            <style>
                .intro-root {
                    min-height: 100vh;
                    display: grid;
                    place-items: center;
                    background:
                        radial-gradient(circle at 50% 20%, rgba(50,50,60,0.20), transparent 35%),
                        linear-gradient(180deg, #040404 0%, #090909 100%);
                    overflow: hidden;
                    user-select: none;
                }

                .intro-shell {
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    gap: 18px;
                    padding: 24px;
                }

                .iphone-frame {
                    position: relative;
                    width: 320px;
                    height: 650px;
                    background: linear-gradient(180deg, #050505 0%, #0c0c0d 100%);
                    border: 12px solid #181818;
                    border-radius: 50px;
                    box-shadow:
                        0 30px 60px rgba(0,0,0,0.7),
                        0 8px 20px rgba(255,255,255,0.04) inset;
                    overflow: hidden;
                    display: flex;
                    flex-direction: column;
                    font-family: -apple-system, BlinkMacSystemFont, "SF Pro Display", sans-serif;
                }

                .iphone-frame::before {
                    content: "";
                    position: absolute;
                    top: 10px;
                    left: 50%;
                    transform: translateX(-50%);
                    width: 115px;
                    height: 28px;
                    background: #000;
                    border-radius: 20px;
                    z-index: 5;
                }

                .status-bar {
                    position: relative;
                    z-index: 6;
                    padding: 14px 20px 6px;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    color: #fff;
                    font-size: 11px;
                    letter-spacing: 0.02em;
                }

                .chat-root {
                    position: relative;
                    display: flex;
                    flex-direction: column;
                    flex: 1;
                    min-height: 0;
                    background:
                        linear-gradient(180deg, rgba(255,255,255,0.02), transparent 18%),
                        #0d0d0f;
                }

                .chat-header {
                    padding: 15px 16px 12px;
                    border-bottom: 1px solid rgba(255,255,255,0.07);
                    display: flex;
                    align-items: center;
                    gap: 10px;
                    flex-shrink: 0;
                    backdrop-filter: blur(8px);
                }

                .avatar {
                    width: 38px;
                    height: 38px;
                    border-radius: 50%;
                    display: grid;
                    place-items: center;
                    color: white;
                    font-weight: 700;
                    background: linear-gradient(180deg, #8c8c92 0%, #636369 100%);
                    box-shadow: 0 3px 10px rgba(0,0,0,0.35);
                }

                .header-name {
                    color: #fff;
                    font-size: 14px;
                    font-weight: 700;
                }

                .typing-status {
                    color: #32d74b;
                    font-size: 10px;
                    opacity: 0;
                    transform: translateY(2px);
                    transition: opacity 180ms ease, transform 180ms ease;
                }

                .typing-status.visible {
                    opacity: 1;
                    transform: translateY(0);
                }

                .chat-messages {
                    flex: 1;
                    min-height: 0;
                    overflow-y: auto;
                    display: flex;
                    flex-direction: column;
                    gap: 8px;
                    padding: 14px 12px 92px;
                    scroll-behavior: smooth;
                    -webkit-overflow-scrolling: touch;
                }

                .chat-messages::-webkit-scrollbar {
                    width: 0;
                    height: 0;
                }

                .msg {
                    max-width: 82%;
                    padding: 10px 12px;
                    border-radius: 17px;
                    font-size: 13px;
                    line-height: 1.42;
                    cursor: pointer;
                    opacity: 0;
                    transform: translateY(6px) scale(0.985);
                    animation: msgIn 340ms cubic-bezier(0.2, 0.8, 0.2, 1) forwards;
                    will-change: transform, opacity;
                    box-shadow: 0 6px 18px rgba(0,0,0,0.18);
                }

                .msg:hover {
                    filter: brightness(1.04);
                }

                .msg.left {
                    background: linear-gradient(180deg, #262629 0%, #202023 100%);
                    color: #fff;
                    align-self: flex-start;
                    border-bottom-left-radius: 4px;
                }

                .msg.right {
                    background: linear-gradient(180deg, #1386ff 0%, #007aff 100%);
                    color: #fff;
                    align-self: flex-end;
                    border-bottom-right-radius: 4px;
                }

                @keyframes msgIn {
                    from {
                        opacity: 0;
                        transform: translateY(6px) scale(0.985);
                    }
                    to {
                        opacity: 1;
                        transform: translateY(0) scale(1);
                    }
                }

                .translation-bar {
                    position: absolute;
                    left: 50%;
                    bottom: 76px;
                    transform: translateX(-50%) translateY(10px);
                    min-width: 220px;
                    max-width: 86%;
                    background: rgba(12,12,14,0.92);
                    color: #d5d5da;
                    padding: 9px 14px;
                    border-radius: 18px;
                    font-size: 11px;
                    line-height: 1.35;
                    text-align: center;
                    border: 1px solid rgba(255,255,255,0.10);
                    backdrop-filter: blur(12px);
                    box-shadow: 0 10px 30px rgba(0,0,0,0.32);
                    opacity: 0;
                    pointer-events: none;
                    transition: opacity 180ms ease, transform 180ms ease;
                    z-index: 20;
                }

                .translation-bar.visible {
                    opacity: 1;
                    transform: translateX(-50%) translateY(0);
                }

                .ticket-app {
                    position: absolute;
                    left: 0;
                    right: 0;
                    bottom: 0;
                    height: 0;
                    background:
                        linear-gradient(180deg, #f7f7f7 0%, #efefef 100%);
                    transition: height 620ms cubic-bezier(0.22, 1, 0.36, 1);
                    z-index: 30;
                    display: flex;
                    flex-direction: column;
                    color: #2f2f33;
                    overflow: hidden;
                    border-top-left-radius: 26px;
                    border-top-right-radius: 26px;
                    box-shadow: 0 -18px 50px rgba(0,0,0,0.35);
                }

                .ticket-app.open {
                    height: 92%;
                }

                .ticket-top {
                    background: linear-gradient(180deg, #df2b22 0%, #c81e15 100%);
                    color: #fff;
                    padding: 16px 16px 14px;
                    font-weight: 700;
                    font-size: 14px;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    letter-spacing: 0.01em;
                }

                .ticket-body {
                    padding: 20px;
                }

                .ticket-label {
                    font-size: 11px;
                    color: #888;
                    letter-spacing: 0.05em;
                }

                .ticket-value {
                    border-bottom: 1px solid #ddd;
                    padding: 6px 0 8px;
                    margin-bottom: 15px;
                    font-weight: 700;
                }

                .ticket-card {
                    background: #fff;
                    border: 1px dashed #cfcfcf;
                    padding: 20px;
                    margin-top: 28px;
                    text-align: center;
                    border-radius: 14px;
                    box-shadow: 0 10px 24px rgba(0,0,0,0.06);
                }

                .ticket-title {
                    font-size: 10px;
                    color: #999;
                    letter-spacing: 0.08em;
                }

                .ticket-time {
                    font-size: 24px;
                    font-weight: 800;
                    margin: 10px 0 12px;
                    color: #1e1e22;
                }

                .buy-btn {
                    background: linear-gradient(180deg, #de2b21 0%, #c71f16 100%);
                    color: white;
                    border: none;
                    width: 100%;
                    padding: 14px;
                    border-radius: 10px;
                    font-weight: 800;
                    cursor: pointer;
                    margin-top: 10px;
                    transition: transform 120ms ease, filter 120ms ease, background 180ms ease;
                    box-shadow: 0 10px 20px rgba(201,31,22,0.22);
                }

                .buy-btn:hover {
                    filter: brightness(1.03);
                }

                .buy-btn:active {
                    transform: translateY(1px) scale(0.995);
                }

                .hint {
                    color: rgba(255,255,255,0.22);
                    font-size: 10px;
                    letter-spacing: 0.03em;
                }
            </style>

            <div class="intro-root">
                <div class="intro-shell">
                    <div class="iphone-frame">
                        <div class="status-bar">
                            <span id="system-time">14:32</span>
                            <span style="opacity:.28;font-size:9px;">LTE</span>
                            <span>📶</span>
                        </div>

                        <div class="chat-root" id="intro-modal-root">
                            <div class="chat-header">
                                <div class="avatar">Ә</div>
                                <div>
                                    <div class="header-name">Әнием</div>
                                    <div id="typing-status" class="typing-status">печатает...</div>
                                </div>
                            </div>

                            <div id="chat-messages" class="chat-messages"></div>
                            <div id="translation-popup" class="translation-bar"></div>

                            <div id="ticket-app" class="ticket-app">
                                <div class="ticket-top">
                                    <span>Автобусы.ру</span>
                                    <span style="opacity:.72;font-size:10px;">v4.2.1</span>
                                </div>

                                <div class="ticket-body">
                                    <div class="ticket-label">ОТКУДА</div>
                                    <div class="ticket-value">Казань, АВ "Столичный"</div>

                                    <div class="ticket-label">КУДА</div>
                                    <div class="ticket-value">Кырлай (поворот у старой лесной дороги)</div>

                                    <div class="ticket-card">
                                        <div class="ticket-title">БИЛЕТ НА АВТОБУС</div>
                                        <div class="ticket-time">Сегодня, 21:40</div>
                                        <button id="buy-btn" class="buy-btn">ОПЛАТИТЬ 450₽</button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="hint">Нажмите на сообщение, чтобы увидеть перевод</div>
                </div>
            </div>
        `;
    }

    private bindElements() {
        if (!this.container) throw new Error('IntroScene: container is missing');

        const q = <T extends Element>(selector: string): T => {
            const el = this.container!.querySelector(selector);
            if (!el) throw new Error(`IntroScene: missing element ${selector}`);
            return el as T;
        };

        this.els = {
            systemTime: q('#system-time'),
            typingStatus: q('#typing-status'),
            chatMessages: q('#chat-messages'),
            translationPopup: q('#translation-popup'),
            ticketApp: q('#ticket-app'),
            buyBtn: q('#buy-btn'),
            modalRoot: q('#intro-modal-root')
        };
    }

    private bindEvents() {
        this.els.buyBtn.addEventListener('click', this.handlePurchase);
    }

    private handlePurchase = (e: Event) => {
        if (this.purchaseLocked || this.disposed) return;
        this.purchaseLocked = true;

        const btn = e.currentTarget as HTMLButtonElement;
        btn.textContent = 'БИЛЕТ ОТПРАВЛЕН В SMS';
        btn.style.background = 'linear-gradient(180deg, #2ead4d 0%, #239241 100%)';
        btn.disabled = true;

        this.safeTimeout(() => {
            if (!this.disposed) {
                this.game.scenes.switchScene('village');
            }
        }, 1400);
    };

    private async startSequence() {
        for (let i = 0; i < DIALOGUE.length; i++) {
            if (this.disposed) return;

            const msg = DIALOGUE[i];

            if (msg.side === 'left') {
                this.setTyping(true);
                await this.wait(1200 + i * 140);
                this.setTyping(false);
            } else {
                await this.wait(520);
            }

            this.appendMessage(msg, i);
            this.scrollChatToBottom();
        }

        await this.wait(1100);
        if (this.disposed) return;

        this.els.ticketApp.classList.add('open');
    }

    private appendMessage(message: DialogueMessage, index: number) {
        const div = document.createElement('button');
        div.type = 'button';
        div.className = `msg ${message.side}`;
        div.textContent = message.text;
        div.style.animationDelay = `${Math.min(index * 25, 160)}ms`;
        div.style.border = 'none';
        div.style.textAlign = 'left';

        div.addEventListener('click', () => {
            this.showTranslation(message.tr);

            if (index === 0) {
                this.clickCount++;
                if (this.clickCount === 16) {
                    this.flashSecretDate();
                }
            }
        });

        this.els.chatMessages.appendChild(div);
    }

    private showTranslation(text: string) {
        const pop = this.els.translationPopup;
        pop.textContent = text;
        pop.classList.add('visible');

        if (this.translationHideTimer !== null) {
            window.clearTimeout(this.translationHideTimer);
        }

        this.translationHideTimer = window.setTimeout(() => {
            pop.classList.remove('visible');
            this.translationHideTimer = null;
        }, 2200);
    }

    private flashSecretDate() {
        const time = this.els.systemTime;
        const original = time.textContent ?? '14:32';

        time.textContent = this.SECRET_DATE;
        time.style.color = '#ff5a54';

        this.safeTimeout(() => {
            time.textContent = original;
            time.style.color = '#fff';
        }, 2400);
    }

    private setTyping(visible: boolean) {
        this.els.typingStatus.classList.toggle('visible', visible);
    }

    private scrollChatToBottom() {
        this.els.chatMessages.scrollTo({
            top: this.els.chatMessages.scrollHeight,
            behavior: 'smooth'
        });
    }

    private wait(ms: number): Promise<void> {
        return new Promise((resolve) => {
            const id = window.setTimeout(() => {
                this.timeouts.delete(id);
                resolve();
            }, ms);
            this.timeouts.add(id);
        });
    }

    private safeTimeout(fn: () => void, ms: number) {
        const id = window.setTimeout(() => {
            this.timeouts.delete(id);
            if (!this.disposed) fn();
        }, ms);
        this.timeouts.add(id);
    }
}
