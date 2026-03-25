import { getActiveChats, getMessagesForChat } from "../data/ChatProvider";

export const renderChat = () => {
  const contacts = getActiveChats();
  
  return `
    <div class="chat-root" style="display:flex; height:100%; background:#cfcfcf; color:#000; font-family:'Tahoma', 'MS Sans Serif', sans-serif; font-size:13px;">
      
      <div class="chat-sidebar" style="width:160px; border-right:2px solid #8a8a8a; background:#dcdcdc; display:flex; flex-direction:column; flex-shrink: 0; overflow-y:auto;">
        <div style="padding:6px; font-weight:bold; border-bottom:2px solid #8a8a8a; background:#efefef; font-size: 11px;">ICQ Contacts</div>
        <div id="chat-tabs-container">
          ${contacts.map((c: any, i: number) => `
            <div class="chat-tab ${i === 0 ? 'active' : ''}" data-chat="${c.id}" 
                 style="padding:6px; cursor:pointer; border-bottom:1px solid #aaa; background:${i === 0 ? '#bcd3ff' : '#dcdcdc'}; font-size: 11px;">
              ${c.icon || ''} <b>${c.name}</b>
            </div>
          `).join('')}
        </div>
        <div style="padding:6px; border-top:2px solid #8a8a8a; margin-top:auto; font-size:10px; color:#333; background:#efefef;">
          <b>Словарь:</b> жми на синие слова
        </div>
      </div>

      <div class="chat-main" style="flex:1; display:flex; flex-direction:column; min-width:0;">
        <div class="chat-header" style="padding:8px 10px; border-bottom:2px solid #8a8a8a; background:#efefef;">
          <div id="chat-title" style="font-weight:bold;">${contacts[0]?.name || 'Chat'}</div>
          <div id="chat-subtitle" style="font-size:11px; color:#444;">${contacts[0]?.sub || ''}</div>
        </div>

        <div id="chat-history" style="flex:1; overflow-y:auto; padding:10px; background:#fff;"></div>

        <div id="chat-translation-box" style="min-height:26px; padding:6px 10px; background:#efefef; border-top:1px solid #8a8a8a; font-size:12px; color:#333;">
          Нажми на синее татарское слово
        </div>

        <div style="display:flex; gap:4px; padding:4px; background:#dcdcdc; border-top:2px solid #8a8a8a;">
          <input type="text" style="flex:1; border:1px inset #fff; padding:2px; font-size:10px;" placeholder="Message..." />
          <button style="border:1px outset #fff; padding:2px 8px; background:#efefef; cursor:pointer; font-size:10px;">Send</button>
        </div>
      </div>
    </div>
  `;
};

export const initChatInteractions = (root: HTMLElement) => {
  const history = root.querySelector('#chat-history') as HTMLElement;
  const title = root.querySelector('#chat-title') as HTMLElement;
  const subtitle = root.querySelector('#chat-subtitle') as HTMLElement;
  const translationBox = root.querySelector('#chat-translation-box') as HTMLElement;
  const input = root.querySelector('input') as HTMLInputElement;

  const renderMessages = (chatId: string) => {
    const messages = getMessagesForChat(chatId);
    history.innerHTML = messages.map((m: any) => {
      let text = m.text;
      // Если есть данные для перевода, оборачиваем слово в спан
      if (m.trans) {
        const regex = new RegExp(`(${m.trans.word})`, 'gi');
        text = text.replace(regex, `<span class="tt-word" style="color:blue;text-decoration:underline;cursor:pointer" data-translation="${m.trans.mean}">$1</span>`);
      }
      return `
        <div style="margin-bottom:6px;">
          <span style="color:${m.color || '#ff0000'}; font-weight:bold;">[${m.time}] ${m.user}:</span> ${text}
        </div>
      `;
    }).join('');
    history.scrollTop = history.scrollHeight;
  };

  root.addEventListener('click', (e) => {
    const target = e.target as HTMLElement;
    
    // Переключение вкладок
    const tab = target.closest('.chat-tab') as HTMLElement;
    if (tab) {
      const id = tab.dataset.chat!;
      const contacts = getActiveChats();
      const current = contacts.find((c: any) => c.id === id);

      root.querySelectorAll('.chat-tab').forEach(t => (t as HTMLElement).style.background = '#dcdcdc');
      tab.style.background = '#bcd3ff';

      if (current) {
        title.textContent = current.name;
        subtitle.textContent = current.sub;
        renderMessages(id);
      }
    }

    // Обработка клика по татарскому слову
    if (target.classList.contains('tt-word')) {
      translationBox.innerHTML = `<b>${target.textContent}</b>: ${target.dataset.translation}`;
    }
  });

  // Отправка (просто визуальная)
  const send = () => {
    if (!input.value.trim()) return;
    const msg = document.createElement('div');
    msg.style.marginBottom = '6px';
    msg.innerHTML = `<span style="color:#ff0000; font-weight:bold;">[${new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}] You:</span> ${input.value}`;
    history.appendChild(msg);
    input.value = '';
    history.scrollTop = history.scrollHeight;
  };

  root.querySelector('button')!.onclick = send;
  input.onkeydown = (e) => { if (e.key === 'Enter') send(); };

  // Рендерим первый чат при открытии
  const firstChat = getActiveChats()[0];
  if (firstChat) renderMessages(firstChat.id);
};