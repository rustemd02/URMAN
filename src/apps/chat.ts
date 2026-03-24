export const renderChat = () => `
  <div class="chat-root" style="display:flex; height:100%; background:#cfcfcf; color:#000; font-family:Tahoma, sans-serif; font-size:13px;">
    
    <div class="chat-sidebar" style="width:160px; border-right:2px solid #8a8a8a; background:#dcdcdc; display:flex; flex-direction:column; flex-shrink: 0;">
      <div style="padding:6px; font-weight:bold; border-bottom:2px solid #8a8a8a; background:#efefef; font-size: 11px;">
        Чаты
      </div>

      <div class="chat-tab active" data-chat="selsovet"
        style="padding:6px; cursor:pointer; border-bottom:1px solid #aaa; background:#bcd3ff; font-size: 11px;">
        <div style="font-weight:bold;">Сельсовет</div>
      </div>

      <div class="chat-tab" data-chat="alsu"
        style="padding:6px; cursor:pointer; border-bottom:1px solid #aaa; background:#dcdcdc; font-size: 11px;">
        <div style="font-weight:bold;">Алсу</div>
      </div>

      <div class="chat-tab" data-chat="hazrat"
        style="padding:6px; cursor:pointer; border-bottom:1px solid #aaa; background:#dcdcdc; font-size: 11px;">
        <div style="font-weight:bold;">Хәзрәт / Бабай</div>
      </div>

      <div class="chat-tab" data-chat="empty1"
        style="padding:6px; cursor:pointer; border-bottom:1px solid #aaa; background:#dcdcdc; font-size: 11px;">
        <div style="font-weight:bold;">Марат_62</div>
      </div>

      <div class="chat-tab" data-chat="empty2"
        style="padding:6px; cursor:pointer; border-bottom:1px solid #aaa; background:#dcdcdc; font-size: 11px;">
        <div style="font-weight:bold;">Рафис</div>
      </div>

      <div style="padding:6px; border-top:2px solid #8a8a8a; margin-top:auto; font-size:10px; color:#333; line-height: 1.2;">
        <div><b>Подсказка:</b> нажимай на татарские слова</div>
        <div>и смотри перевод снизу.</div>
      </div>
    </div>

    <div class="chat-main" style="flex:1; display:flex; flex-direction:column; min-width:0;">
      
      <div class="chat-header" style="padding:8px 10px; border-bottom:2px solid #8a8a8a; background:#efefef; display:flex; justify-content:space-between; align-items:center;">
        <div>
          <div id="chat-title" style="font-weight:bold;">Сельсовет</div>
          <div id="chat-subtitle" style="font-size:11px; color:#444;">деревенский общий чат</div>
        </div>
      </div>

      <div id="chat-history" style="flex:1; overflow-y:auto; padding:10px; background:#fff; border-top:2px inset #fff; border-bottom:2px inset #fff;">
        
        <!-- СЕЛЬСОВЕТ -->
        <div class="chat-panel" data-chat-panel="selsovet" style="display:block;">
          <div style="margin-bottom:8px;"><span style="color:#ff0000; font-weight:bold;">[08:14] Gayaz_Admin:</span> Кто завтра в район едет?</div>
          <div style="margin-bottom:8px;"><span style="color:#0000ff; font-weight:bold;">[08:16] Ilfat_87:</span> блин завтра погода какая вообще</div>
          <div style="margin-bottom:8px;"><span style="color:#6a00aa; font-weight:bold;">[08:17] Alina_Kzn:</span> <span class="tt-word" data-translation="слушай / эй">әле</span> вы слышали что Айнур который сын Манур абый в Москву из Казани переехал</div>
          <div style="margin-bottom:8px;"><span style="color:#008000; font-weight:bold;">[08:18] Rafis:</span> да какой в госдуме он около госдумы в магазине на кассе работает лол</div>
          <div style="margin-bottom:8px;"><span style="color:#b85c00; font-weight:bold;">[08:21] Babay77:</span> ночью у леса опять собаки выли</div>
          <div style="margin-bottom:8px;"><span style="color:#ff0000; font-weight:bold;">[08:22] Gayaz_Admin:</span> не начинайте только опять</div>
          <div style="margin-bottom:8px;"><span style="color:#4444aa; font-weight:bold;">[08:25] Aidar:</span> а чо у Фарита свет в сарае до 3 утра горел</div>
          <div style="margin-bottom:8px;"><span style="color:#008080; font-weight:bold;">[08:26] Zuleiha:</span> у него корова отелилась может</div>
          <div style="margin-bottom:8px;"><span style="color:#990000; font-weight:bold;">[08:28] Rustem:</span> или опять кто-то в лес полез</div>
          <div class="chat-system-msg">Пользователь Marat_62 прочитал 138 сообщений и вышел.</div>
          <div style="margin-bottom:8px;"><span style="color:#ff00aa; font-weight:bold;">[08:31] Ilgizar:</span> Айдар сначала думал что тут коррупция замешана 😂</div>
          <div style="margin-bottom:8px;"><span style="color:#b85c00; font-weight:bold;">[08:33] Babay77:</span> я не про это. следы были. длинные. будто пальцы</div>
          <div style="margin-bottom:8px;"><span style="color:#0000ff; font-weight:bold;">[08:34] Ilfat_87:</span> опять началось</div>
          <div style="margin-bottom:8px;"><span style="color:#6a00aa; font-weight:bold;">[08:36] Aigul:</span> <span class="tt-word" data-translation="не говори / молчи">әйтмә</span> здесь такое вслух</div>
          <div class="chat-system-msg">Gayaz_Admin закрепил сообщение: “После заката детям в сторону леса не ходить”.</div>
        </div>

        <!-- АЛСУ -->
        <div class="chat-panel" data-chat-panel="alsu" style="display:none;">
          <div style="margin-bottom:8px;"><span style="color:#ff0000; font-weight:bold;">[21:02] You:</span> Ты вчера что хотела сказать про мост?</div>
          <div style="margin-bottom:8px;"><span style="color:#6a00aa; font-weight:bold;">[21:17] Alsu:</span> неважно уже</div>
          <div style="margin-bottom:8px;"><span style="color:#ff0000; font-weight:bold;">[21:19] You:</span> Это связано с лесом?</div>
          <div style="margin-bottom:8px;"><span style="color:#6a00aa; font-weight:bold;">[21:24] Alsu:</span> может да может нет</div>
          <div style="margin-bottom:8px;"><span style="color:#ff0000; font-weight:bold;">[21:25] You:</span> Ты видела кого-то?</div>
          <div style="margin-bottom:8px;"><span style="color:#6a00aa; font-weight:bold;">[21:29] Alsu:</span> <span class="tt-word" data-translation="не знаю">белмим</span></div>
          <div style="margin-bottom:8px;"><span style="color:#ff0000; font-weight:bold;">[21:31] You:</span> Алсу, нормально скажи.</div>
          <div style="margin-bottom:8px;"><span style="color:#6a00aa; font-weight:bold;">[21:38] Alsu:</span> если я скажу ты все равно не поверишь</div>
          <div style="margin-bottom:8px;"><span style="color:#ff0000; font-weight:bold;">[21:40] You:</span> Попробуй.</div>
          <div style="margin-bottom:8px;"><span style="color:#6a00aa; font-weight:bold;">[21:47] Alsu:</span> <span class="tt-word" data-translation="не ходи">барма</span> туда ночью</div>
          <div style="margin-bottom:8px;"><span style="color:#ff0000; font-weight:bold;">[21:48] You:</span> Куда именно?</div>
          <div style="margin-bottom:8px;"><span style="color:#6a00aa; font-weight:bold;">[21:56] Alsu:</span> ты сам поймешь</div>
          <div style="margin-bottom:8px;"><span style="color:#ff0000; font-weight:bold;">[21:58] You:</span> Это человек или нет?</div>
          <div style="margin-bottom:8px;"><span style="color:#6a00aa; font-weight:bold;">[22:12] Alsu:</span> <span class="tt-word" data-translation="иногда">кайчак</span> хуже человека</div>
          <div class="chat-system-msg">Alsu была в сети 18 сек. Сообщение не удалено.</div>
        </div>

        <!-- ХАЗРӘТ -->
        <div class="chat-panel" data-chat-panel="hazrat" style="display:none;">
          <div style="margin-bottom:8px;"><span style="color:#b85c00; font-weight:bold;">[18:03] Babay77:</span> Хәзрәт, может <span class="tt-word" data-translation="молитвы">догалар</span> укып чыгарга надо?</div>
          <div style="margin-bottom:8px;"><span style="color:#5555cc; font-weight:bold;">[18:11] Irek:</span> бабай у нас уже третий день не спит почти</div>
          <div style="margin-bottom:8px;"><span style="color:#b85c00; font-weight:bold;">[18:13] Babay77:</span> ночью опять кто-то под окном ходил</div>
          <div style="margin-bottom:8px;"><span style="color:#4444aa; font-weight:bold;">[18:18] Damir:</span> человек может был</div>
          <div style="margin-bottom:8px;"><span style="color:#b85c00; font-weight:bold;">[18:21] Babay77:</span> человек так не смеется</div>
          <div class="chat-system-msg">Хәзрәт прочитал сообщение.</div>
          <div style="margin-bottom:8px;"><span style="color:#b85c00; font-weight:bold;">[19:02] Babay77:</span> если читать, то что?</div>
          <div class="chat-system-msg">Хәзрәт печатает...</div>
          <div class="chat-system-msg">Хәзрәт перестал печатать.</div>
          <div style="margin-bottom:8px;"><span style="color:#5555cc; font-weight:bold;">[19:54] Irek:</span> это уже не смешно</div>
          <div class="chat-system-msg">Новых сообщений нет.</div>
        </div>

        <!-- EMPTY 1 -->
        <div class="chat-panel" data-chat-panel="empty1" style="display:none;">
          <div style="padding:20px; text-align:center; color:#888;">История сообщений очищена или еще не создана.</div>
        </div>

        <!-- EMPTY 2 -->
        <div class="chat-panel" data-chat-panel="empty2" style="display:none;">
          <div style="padding:20px; text-align:center; color:#888;">Пользователь не добавил вас в список контактов.</div>
        </div>

        <!-- STORY LOCKED CHAT (Example) -->
        <div class="chat-panel" data-chat-panel="locked" style="display:none;">
          <div style="padding:20px; text-align:center; color:#666;">
            <div style="font-size:24px; margin-bottom:10px;">🔒</div>
            Чат заблокирован. <br> Требуется расшифровка ключа ПАКТ_1999.
          </div>
        </div>
      </div>

      <div id="chat-translation-box" style="min-height:26px; padding:6px 10px; background:#efefef; border-top:1px solid #8a8a8a; font-size:12px; color:#333;">
        Нажми на татарское слово, чтобы увидеть перевод.
      </div>

      <div style="display:flex; gap:4px; padding:4px; background:#dcdcdc; border-top:2px solid #8a8a8a;">
        <input
          type="text"
          style="flex:1; border:1px inset #fff; padding:2px; font-family:Tahoma, sans-serif; font-size:10px;"
          placeholder="Сообщение..."
        />
        <button
          style="border:1px outset #fff; padding:2px 8px; background:#efefef; cursor:pointer; font-size:10px;"
        >
          Отправить
        </button>
      </div>
    </div>
  </div>
`;

export const initChatInteractions = (root: HTMLElement) => {
  const tabs = root.querySelectorAll('.chat-tab') as NodeListOf<HTMLElement>;
  const panels = root.querySelectorAll('.chat-panel') as NodeListOf<HTMLElement>;
  const title = root.querySelector('#chat-title');
  const subtitle = root.querySelector('#chat-subtitle');
  const translationBox = root.querySelector('#chat-translation-box');
  const chatHistory = root.querySelector('#chat-history') as HTMLElement;
  const input = root.querySelector('input[type="text"]') as HTMLInputElement;
  const sendBtn = root.querySelector('button') as HTMLButtonElement;

  const alsuReplies = [
    "Может быть... А ты сам как думаешь? 😉",
    "Белмим, может и видела что-то. А может это ты мне снишься?",
    "Урман всё слышит, даже наши мысли. Не говори об этом вслух.",
    "Кайчак мне кажется, что ты слишком много спрашиваешь... Но мне это нравится.",
    "Барма туда. Просто поверь мне на слово.",
    "Сенсация! Городской парень боится леса? Приходи вечером к мосту, я тебя защищу.",
    "У нас в деревне свои правила. Не нарушай пакт.",
    "Ты такой любопытный... Это тебя погубит. Или спасет.",
    "Әйтмә... Молчи. Просто слушай ветер.",
    "Если я отвечу, мне придется тебя украсть в лес.",
    "Хәзер не время для таких разговоров. Давай о чем-нибудь приятном?",
    "Ты мне нравишься. Но лес тебя не любит. Будь осторожен.",
    "Может, я и есть тот шепот, который ты слышишь?",
    "Ничего не бойся. Пока я с тобой в сети, ты в безопасности. Наверное."
  ];

  const meta: Record<string, {title: string, subtitle: string}> = {
    selsovet: { title: 'Сельсовет', subtitle: 'деревенский общий чат' },
    alsu: { title: 'Алсу', subtitle: 'личный чат' },
    hazrat: { title: 'Хәзрәт / Бабай', subtitle: 'тревога и догалар' },
    empty1: { title: 'Марат_62', subtitle: 'нет данных' },
    empty2: { title: 'Рафис', subtitle: 'нет данных' },
    locked: { title: '???', subtitle: 'зашифровано' }
  };

  const addMessage = (user: string, text: string, color: string = "#ff0000") => {
    const time = new Date().toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'});
    const msg = document.createElement('div');
    msg.style.marginBottom = "8px";
    msg.innerHTML = `<span style="color:${color}; font-weight:bold;">[${time}] ${user}:</span> ${text}`;
    
    // Находим активную панель
    const activePanel = Array.from(panels).find(p => p.style.display === 'block');
    if (activePanel) {
      activePanel.appendChild(msg);
      chatHistory.scrollTop = chatHistory.scrollHeight;
    }
  };

  const handleSend = () => {
    const text = input.value.trim();
    if (!text) return;

    const activeTab = Array.from(tabs).find(t => t.classList.contains('active'));
    const chatId = activeTab?.dataset.chat;

    addMessage("You", text, "#ff0000");
    input.value = "";

    if (chatId === 'alsu') {
      setTimeout(() => {
        const reply = alsuReplies[Math.floor(Math.random() * alsuReplies.length)];
        addMessage("Alsu", reply, "#6a00aa");
      }, 1000 + Math.random() * 2000);
    }
  };

  sendBtn.addEventListener('click', handleSend);
  input.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') handleSend();
  });

  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      const chatId = tab.dataset.chat;
      if (!chatId) return;

      tabs.forEach(t => {
        t.classList.remove('active');
        t.style.background = '#dcdcdc';
      });

      tab.classList.add('active');
      tab.style.background = '#bcd3ff';

      panels.forEach(panel => {
        panel.style.display = panel.dataset.chatPanel === chatId ? 'block' : 'none';
      });

      if (title && subtitle && meta[chatId]) {
        title.textContent = meta[chatId].title;
        subtitle.textContent = meta[chatId].subtitle;
      }
    });
  });

  root.querySelectorAll('.tt-word').forEach(wordNode => {
    const word = wordNode as HTMLElement;
    word.style.color = '#0044cc';
    word.style.textDecoration = 'underline';
    word.style.cursor = 'pointer';

    word.addEventListener('click', (e) => {
      e.stopPropagation();
      const original = word.textContent;
      const translation = word.dataset.translation || 'Перевод не найден';
      if (translationBox) {
        translationBox.innerHTML = '<b>' + original + '</b>: ' + translation;
      }
    });
  });
};
