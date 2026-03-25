import { BaseScene } from './BaseScene';

export class MainMenuScene extends BaseScene {
    init(container: HTMLElement) {
        this.container = container;
        
        const savedUsername = localStorage.getItem('urman_username') || '';

        container.innerHTML = `
            <style>
                @keyframes flicker {
                    0% { opacity: 0.8; }
                    50% { opacity: 1; }
                    100% { opacity: 0.8; }
                }
                .main-menu-bg {
                    position: absolute;
                    top: 0; left: 0; width: 100%; height: 100%;
                    background: url('/assets/background_house.jpg') center/cover no-repeat;
                    filter: saturate(0.2) brightness(0.3);
                    z-index: 1;
                }
                .main-menu-content {
                    position: relative;
                    z-index: 10;
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    justify-content: center;
                    height: 100vh;
                    font-family: 'Verdana', sans-serif;
                    color: #fff;
                    text-align: center;
                }
                .main-title {
                    font-size: 72px;
                    font-weight: bold;
                    letter-spacing: 15px;
                    margin-bottom: 10px;
                    text-shadow: 0 0 20px #ff0000;
                    animation: flicker 4s infinite alternate;
                }
                .sub-title {
                    font-size: 14px;
                    letter-spacing: 5px;
                    color: #aaa;
                    margin-bottom: 50px;
                    text-transform: uppercase;
                }
                .menu-form {
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    gap: 15px;
                    background: rgba(0, 0, 0, 0.6);
                    padding: 30px;
                    border-radius: 10px;
                    border: 1px solid #333;
                }
                .menu-input {
                    background: transparent;
                    border: none;
                    border-bottom: 2px solid #555;
                    color: #fff;
                    font-size: 18px;
                    text-align: center;
                    padding: 10px;
                    width: 250px;
                    outline: none;
                    transition: border-color 0.3s;
                }
                .menu-input:focus {
                    border-bottom: 2px solid #ff4444;
                }
                .menu-btn {
                    background: transparent;
                    border: 1px solid #555;
                    color: #fff;
                    padding: 12px 40px;
                    font-size: 16px;
                    cursor: pointer;
                    text-transform: uppercase;
                    letter-spacing: 3px;
                    transition: all 0.3s;
                }
                .menu-btn:hover {
                    background: #fff;
                    color: #000;
                    border-color: #fff;
                }
            </style>
            
            <div class="main-menu-bg"></div>
            <div class="main-menu-content">
                <div class="main-title">URMAN</div>
                <div class="sub-title">Horror Quest</div>
                
                <div class="menu-form">
                    <input type="text" id="username-input" class="menu-input" placeholder="Введите ваше имя" value="${savedUsername}" autocomplete="off">
                    <button id="play-btn" class="menu-btn">Начать игру</button>
                    <button id="quick-play-btn" class="menu-btn" style="font-size: 10px; border-color: #444; color: #888;">Пропустить вступление</button>
                    <button id="settings-btn" class="menu-btn" style="border-color: transparent;">Настройки</button>
                </div>
            </div>
        `;

        const playBtn = container.querySelector('#play-btn');
        const quickBtn = container.querySelector('#quick-play-btn');
        const input = container.querySelector('#username-input') as HTMLInputElement;

        playBtn?.addEventListener('click', () => {
            const name = input.value.trim() || 'Игрок';
            localStorage.setItem('urman_username', name);
            this.game.scenes.switchScene('chapter1');
        });

        quickBtn?.addEventListener('click', () => {
            const name = input.value.trim() || 'Тестер';
            localStorage.setItem('urman_username', name);
            this.game.scenes.switchScene('village');
        });
    }
}
