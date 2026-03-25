import { BaseScene } from './BaseScene';

export class IntroScene extends BaseScene {
    private profiles: Record<string, any> = {
        "Тимур (ИТИС)": {
            avatar: "https://images.unsplash.com/photo-1531427186611-ecfd6d936c79?w=200&h=200&fit=crop",
            status: "Senior Pomidor 🍅",
            bio: "ИТИС 552. В поисках идеального стака.",
            posts: ["Купил мак на м3, теперь доширак мой лучший друг", "Кто пойдет в качалку после пар?"]
        },
        "Диана": {
            avatar: "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=200&h=200&fit=crop",
            status: "Beach mode: ON 🏖️",
            bio: "Life is too short for bad code. Анталья, жди!",
            posts: ["Чемоданы собраны, билеты в кармане!", "Почему в аэропорту такой дорогой кофе?"]
        },
        "Руслан": {
            avatar: "https://images.unsplash.com/photo-1528892952291-009c663ce843?w=200&h=200&fit=crop",
            status: "Тракторист на полставки 🚜",
            bio: "JS is my passion. Сельское хозяйство — моё призвание (нет).",
            posts: ["Вчера кодил до 4 утра, сегодня не чувствую ног", "Где лучшие тусовки? У нас в общаге!"]
        },
        "Булат (Староста)": {
            avatar: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=200&h=200&fit=crop",
            status: "Сдаем лабы вовремя! 📋",
            bio: "Главный по дедлайнам. Не пишите мне после 22:00.",
            posts: ["Список должников обновлен. Спойлер: там все.", "Методичка по курсачу в закрепе."]
        }
    };

    private messages = [
        { sender: "Тимур (ИТИС)", text: "Айдар, ты реально в деревню? К коровам? 😂", side: 'left' },
        { sender: "Диана", text: "Мы в Анталью на всё лето, а ты будешь картошку копать? Жесть.", side: 'left' },
        { sender: "Руслан", text: "Скинь фотку трактора, айтишник сельский! Лол.", side: 'left' },
        { sender: "Булат (Староста)", text: "Че по курсачу? В деревне инета нет, забей короче.", side: 'left' },
        { sender: "Тимур (ИТИС)", text: "Там хоть 2G ловит? Или только на крыше сельсовета? 📶", side: 'left' },
        { sender: "Вы", text: "Отстаньте. У бабушки там спокойно. Отдохну от ваших фреймворков.", side: 'right' }
    ];

    init(container: HTMLElement) {
        this.container = container;
        container.innerHTML = `
            <style>
                @keyframes messageAppear {
                    from { opacity: 0; transform: translateY(10px) scale(0.95); }
                    to { opacity: 1; transform: translateY(0) scale(1); }
                }
                @keyframes slideIn {
                    from { transform: translateX(100%); }
                    to { transform: translateX(0); }
                }
                .kfu-background {
                    position: absolute; top: 0; left: 0; width: 100vw; height: 100vh;
                    background: url('https://images.unsplash.com/photo-1541339907198-e08756dedf3f?w=1600&h=900&fit=crop') center/cover no-repeat;
                    filter: brightness(0.4) blur(3px);
                    z-index: 1;
                }
                .iphone-frame {
                    position: relative; 
                    width: 320px; 
                    height: 650px;
                    background: #000; 
                    border: 12px solid #1a1a1a; 
                    border-radius: 50px;
                    box-shadow: 0 30px 60px rgba(0,0,0,0.8), inset 0 0 2px 2px rgba(255,255,255,0.1); 
                    overflow: hidden;
                    display: flex; 
                    flex-direction: column; 
                    z-index: 10;
                    box-sizing: border-box;
                }
                .iphone-notch {
                    position: absolute; top: 0; left: 50%; transform: translateX(-50%);
                    width: 150px; height: 30px; background: #1a1a1a; 
                    border-bottom-left-radius: 20px; border-bottom-right-radius: 20px; 
                    z-index: 100;
                }
                .screen { position: absolute; top: 0; left: 0; width: 100%; height: 100%; display: none; flex-direction: column; background: #000; z-index: 10; }
                .screen.active { display: flex; }
                .profile-screen { animation: slideIn 0.3s ease-out; z-index: 20; background: #111; }
                .post-card { background: #1c1c1e; border-radius: 10px; padding: 10px; margin-bottom: 10px; font-size: 13px; color: #eee; }
                #chat-flow::-webkit-scrollbar { width: 0; }
                .avi-img {
                    width: 32px; height: 32px; 
                    border-radius: 50%; 
                    object-fit: cover; 
                    flex-shrink: 0; /* Чтобы не сплющило */
                    cursor: pointer;
                }
            </style>

            <div class="ios-intro" style="height: 100vh; display: flex; justify-content: center; align-items: center; font-family: -apple-system, sans-serif; position: relative; overflow: hidden; background: #000;">
                <div class="kfu-background"></div>

                <div class="iphone-frame">
                    <div class="iphone-notch"></div>
                    
                    <div id="screen-chat" class="screen active">
                        <div style="padding: 40px 10px 10px; background: #111; border-bottom: 1px solid #333; text-align: center;">
                            <div style="color: #666; font-size: 10px;">ВКонтакте</div>
                            <div style="color: #fff; font-weight: 600; font-size: 14px;">ИТИС группа 552 🎓</div>
                        </div>
                        <div id="chat-flow" style="flex: 1; padding: 15px; display: flex; flex-direction: column; gap: 12px; overflow-y: auto;"></div>
                        <div style="padding: 10px; background: #111; border-top: 1px solid #333;">
                            <div style="background: #222; border-radius: 20px; padding: 8px 15px; color: #555; font-size: 13px;">Сообщение...</div>
                        </div>
                    </div>

                    <div id="screen-profile" class="screen profile-screen">
                        <div style="padding: 40px 15px 15px; background: #1c1c1e; display: flex; align-items: center; gap: 10px;">
                            <button id="back-to-chat" style="background: none; border: none; color: #007AFF; font-size: 18px; cursor: pointer;">✕</button>
                            <div style="color: #fff; font-weight: 600;" id="prof-name">Профиль</div>
                        </div>
                        <div style="flex: 1; overflow-y: auto; padding: 20px; text-align: center;">
                            <img class="prof-avatar" src="" style="width: 100px; height: 100px; border-radius: 50%; object-fit: cover; border: 3px solid #007AFF; margin-bottom: 15px;">
                            <div id="prof-status" style="color: #007AFF; font-size: 13px; font-weight: bold; margin-bottom: 5px;"></div>
                            <div id="prof-bio" style="color: #888; font-size: 13px; margin-bottom: 20px; line-height: 1.4;"></div>
                            <div style="text-align: left; border-top: 1px solid #333; pt: 15px;">
                                <div style="color: #fff; font-size: 14px; font-weight: bold; margin-bottom: 10px; margin-top: 15px;">Посты</div>
                                <div id="prof-posts"></div>
                            </div>
                        </div>
                    </div>
                </div>

                <div id="narrative-footer" style="position: absolute; bottom: 40px; text-align: center; opacity: 0; transition: opacity 1s; z-index: 100;">
                    <button class="start-game-btn" style="background: #007AFF; color: #fff; border: none; padding: 12px 35px; border-radius: 25px; font-weight: 600; cursor: pointer; font-size: 16px;">
                        Ехать в деревню
                    </button>
                </div>
                
                <!-- Skip button -->
                <button id="skip-intro-btn" style="
                    position: absolute; top: 14px; right: 14px; z-index: 200;
                    background: rgba(255,255,255,0.12); color: rgba(255,255,255,0.7);
                    border: 1px solid rgba(255,255,255,0.2); border-radius: 20px;
                    padding: 6px 16px; font-size: 12px; cursor: pointer;
                    backdrop-filter: blur(4px); transition: all 0.2s;
                " onmouseover="this.style.background='rgba(255,255,255,0.22)'" onmouseout="this.style.background='rgba(255,255,255,0.12)'">Пропустить ↩</button>
            </div>
        `;

        this.startChatLogic(container);
        this.initEvents(container);
    }

    private initEvents(container: HTMLElement) {
        container.querySelector('#back-to-chat')?.addEventListener('click', () => {
            container.querySelector('#screen-profile')?.classList.remove('active');
            container.querySelector('#screen-chat')?.classList.add('active');
        });

        container.querySelector('.start-game-btn')?.addEventListener('click', () => {
            this.game.scenes.switchScene('village');
        });

        container.querySelector('#skip-intro-btn')?.addEventListener('click', () => {
            this.game.scenes.switchScene('village');
        });
    }

    private showProfile(name: string) {
        const data = this.profiles[name];
        if (!data || !this.container) return;
        
        const chatScreen = this.container.querySelector('#screen-chat');
        const profScreen = this.container.querySelector('#screen-profile');
        const profName = this.container.querySelector('#prof-name');
        const profStatus = this.container.querySelector('#prof-status');
        const profBio = this.container.querySelector('#prof-bio');
        const postsCont = this.container.querySelector('#prof-posts');
        const avatarImg = profScreen?.querySelector('.prof-avatar') as HTMLImageElement;

        if (chatScreen && profScreen && profName && profStatus && profBio && postsCont && avatarImg) {
            avatarImg.src = data.avatar;
            profName.textContent = name;
            profStatus.textContent = data.status;
            profBio.textContent = data.bio;
            postsCont.innerHTML = data.posts.map((p: string) => `<div class="post-card">${p}</div>`).join('');
            
            chatScreen.classList.remove('active');
            profScreen.classList.add('active');
        }
    }

    private async startChatLogic(container: HTMLElement) {
        const flow = container.querySelector('#chat-flow') as HTMLElement;
        const footer = container.querySelector('#narrative-footer') as HTMLElement;
        if (!flow || !footer) return;

        for (const msg of this.messages) {
            await this.delay(1000 + Math.random() * 800);
            this.appendMessage(flow, msg);
            flow.scrollTo({ top: flow.scrollHeight, behavior: 'smooth' });
        }
        setTimeout(() => { footer.style.opacity = "1"; }, 500);
    }

    private appendMessage(container: HTMLElement, msg: any) {
        const isRight = msg.side === 'right';
        const profile = this.profiles[msg.sender];
        const row = document.createElement('div');
        row.style.cssText = `display: flex; gap: 10px; align-self: ${isRight ? 'flex-end' : 'flex-start'}; max-width: 85%;`;

        if (!isRight && profile) {
            const avi = document.createElement('img');
            avi.src = profile.avatar;
            avi.className = 'avi-img';
            avi.onclick = () => this.showProfile(msg.sender);
            row.appendChild(avi);
        }

        const bubble = document.createElement('div');
        bubble.style.cssText = `
            background: ${isRight ? '#007AFF' : '#262629'}; 
            color: white; padding: 10px 14px; border-radius: 18px; font-size: 14px;
            animation: messageAppear 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
        `;

        if (!isRight) {
            const name = document.createElement('div');
            name.style.cssText = `font-size: 10px; color: #007AFF; font-weight: bold; margin-bottom: 4px; cursor: pointer;`;
            name.textContent = msg.sender;
            name.onclick = () => this.showProfile(msg.sender);
            bubble.appendChild(name);
        }

        const txt = document.createElement('div');
        txt.textContent = msg.text;
        bubble.appendChild(txt);
        row.appendChild(bubble);
        container.appendChild(row);
    }

    private delay(ms: number) { return new Promise(resolve => setTimeout(resolve, ms)); }
}