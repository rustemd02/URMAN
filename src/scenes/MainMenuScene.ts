import { BaseScene } from './BaseScene';

export class MainMenuScene extends BaseScene {
    init(container: HTMLElement) {
        this.container = container;

        const savedUsername = localStorage.getItem('urman_username') || '';

        container.innerHTML = `
            <style>
                @keyframes flicker {
                    0% { opacity: 0.8; text-shadow: 0 0 10px #ff0000; }
                    50% { opacity: 1; text-shadow: 0 0 30px #ff0000; }
                    100% { opacity: 0.8; text-shadow: 0 0 10px #ff0000; }
                }

                @keyframes fadeIn {
                    from { opacity: 0; transform: translateY(20px); }
                    to { opacity: 1; transform: translateY(0); }
                }

                .main-menu-bg {
                    position: absolute;
                    top: 0; left: 0; width: 100%; height: 100%;
                    background: url('/assets/background_house.jpg') center/cover no-repeat;
                    filter: saturate(0.1) brightness(0.2);
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
                    font-family: 'Philosopher', sans-serif;
                    color: #fff;
                    text-align: center;
                    animation: fadeIn 1.5s ease-out;
                }

                .main-title {
                    font-size: 84px;
                    font-weight: bold;
                    letter-spacing: 20px;
                    margin-bottom: 5px;
                    animation: flicker 4s infinite alternate;
                }

                .sub-title {
                    font-size: 12px;
                    letter-spacing: 8px;
                    color: #666;
                    margin-bottom: 60px;
                    text-transform: uppercase;
                }

                .menu-form {
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    gap: 15px;
                    background: rgba(0, 0, 0, 0.4);
                    padding: 40px;
                    border-radius: 4px;
                    border: 1px solid rgba(255, 255, 255, 0.05);
                    backdrop-filter: blur(4px);
                }

                .menu-input {
                    background: transparent;
                    border: none;
                    border-bottom: 1px solid #444;
                    color: #fff;
                    font-size: 18px;
                    text-align: center;
                    padding: 12px;
                    width: 280px;
                    outline: none;
                    transition: all 0.4s;
                    margin-bottom: 10px;
                }

                .menu-input:focus {
                    border-bottom: 1px solid #ff4444;
                    background: rgba(255, 68, 68, 0.05);
                }

                .menu-btn {
                    background: transparent;
                    border: 1px solid rgba(255, 255, 255, 0.2);
                    color: #ccc;
                    padding: 14px 50px;
                    font-size: 14px;
                    cursor: pointer;
                    text-transform: uppercase;
                    letter-spacing: 4px;
                    transition: all 0.3s;
                    width: 100%;
                }

                .menu-btn:hover {
                    background: #fff;
                    color: #000;
                    border-color: #fff;
                    box-shadow: 0 0 20px rgba(255,255,255,0.2);
                }

                .btn-secondary {
                    font-size: 10px;
                    border-color: transparent;
                    color: #444;
                    margin-top: 10px;
                }

                .btn-secondary:hover {
                    background: transparent;
                    color: #888;
                    border-color: transparent;
                }
            </style>
            
            <div class="main-menu-bg"></div>
            <div class="main-menu-content">
                <div class="main-title">URMAN</div>
                <div class="sub-title">Horror Quest</div>
                
                <div class="menu-form">
                    <input type="text" id="username-input" class="menu-input" 
                           placeholder="КЕМ СИН?" value="${savedUsername}" 
                           maxlength="20" autocomplete="off">
                    
                    <button id="play-btn" class="menu-btn">Начать игру</button>
                    
                    <button id="quick-play-btn" class="menu-btn btn-secondary">
                        Пропустить вступление
                    </button>
                </div>
            </div>
        `;

        const playBtn = container.querySelector('#play-btn');
        const quickBtn = container.querySelector('#quick-play-btn');
        const input = container.querySelector('#username-input') as HTMLInputElement;

        const startGame = (targetScene: string) => {
            const name = input.value.trim() || 'Айдар';
            localStorage.setItem('urman_username', name);
            this.game.scenes.switchScene(targetScene);
        };

        playBtn?.addEventListener('click', () => startGame('chapter1'));
        quickBtn?.addEventListener('click', () => startGame('village'));

        // Запуск по Enter
        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') startGame('chapter1');
        });
    }
}