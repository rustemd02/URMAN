import { BaseScene } from './BaseScene';

export class IntroScene extends BaseScene {
    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `
            <div class="intro-overlay" style="
                background: #000; 
                color: #fff; 
                height: 100vh; 
                display: flex; 
                flex-direction: column; 
                justify-content: center; 
                align-items: center;
                font-family: 'Tahoma', sans-serif;
                padding: 40px;
                text-align: center;
            ">
                <div class="chat-bubble-container" style="max-width: 600px; margin-bottom: 40px;">
                    <div class="chat-msg" style="color: #888; margin-bottom: 10px; font-size: 14px;">[Групповой чат: КФУ ИТИС 2026]</div>
                    <div class="chat-msg" style="margin-bottom: 20px;">
                        <b style="color: #00ff00;">Тимур:</b> Айдар, ты реально в деревню? К коровам? 😂
                    </div>
                    <div class="chat-msg" style="margin-bottom: 20px;">
                        <b style="color: #00ff00;">Диана:</b> Мы в Анталью на всё лето, а ты будешь картошку копать? Жесть.
                    </div>
                    <div class="chat-msg" style="margin-bottom: 20px;">
                        <b style="color: #00ff00;">Руслан:</b> Скинь фотку трактора, айтишник сельский! Лол.
                    </div>
                </div>
                
                <div class="fade-text" style="font-style: italic; color: #aaa; margin-top: 20px;">
                    Казань провожала жарой и насмешками... <br>
                    Впереди — лето в Урмане. У Эби и Бабая.
                </div>

                <button class="win98-btn start-game-btn" style="margin-top: 40px; padding: 10px 20px;">
                    Ехать в деревню
                </button>
            </div>
        `;

        container.querySelector('.start-game-btn')?.addEventListener('click', () => {
            this.game.scenes.switchScene('computer');
        });
    }
}
