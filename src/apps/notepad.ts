const NOTEPAD_STORAGE_KEY = 'game.notepad.files.v1';
const NOTEPAD_ACTIVE_FILE_KEY = 'game.notepad.activeFile.v1';

function escapeHtml(value = '') {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function createDefaultNotepadState(initialContent = 'Новая заметка...') {
  const fileId = `note_${Date.now()}`;
  return {
    files: [
      {
        id: fileId,
        name: 'Заметка 1.txt',
        content: initialContent,
        createdAt: Date.now(),
        updatedAt: Date.now(),
      },
    ],
    activeFileId: fileId,
  };
}

function readNotepadState(initialContent = 'Новая заметка...') {
  try {
    const raw = localStorage.getItem(NOTEPAD_STORAGE_KEY);
    if (!raw) {
      const state = createDefaultNotepadState(initialContent);
      localStorage.setItem(NOTEPAD_STORAGE_KEY, JSON.stringify(state));
      localStorage.setItem(NOTEPAD_ACTIVE_FILE_KEY, state.activeFileId);
      return state;
    }

    const parsed = JSON.parse(raw);
    if (!parsed || !Array.isArray(parsed.files) || !parsed.files.length) {
      const fallback = createDefaultNotepadState(initialContent);
      localStorage.setItem(NOTEPAD_STORAGE_KEY, JSON.stringify(fallback));
      localStorage.setItem(NOTEPAD_ACTIVE_FILE_KEY, fallback.activeFileId);
      return fallback;
    }

    const savedActive = localStorage.getItem(NOTEPAD_ACTIVE_FILE_KEY);
    parsed.activeFileId = savedActive || parsed.activeFileId || parsed.files[0].id;

    return parsed;
  } catch (err) {
    const fallback = createDefaultNotepadState(initialContent);
    localStorage.setItem(NOTEPAD_STORAGE_KEY, JSON.stringify(fallback));
    localStorage.setItem(NOTEPAD_ACTIVE_FILE_KEY, fallback.activeFileId);
    return fallback;
  }
}

function writeNotepadState(state) {
  localStorage.setItem(NOTEPAD_STORAGE_KEY, JSON.stringify(state));
  localStorage.setItem(NOTEPAD_ACTIVE_FILE_KEY, state.activeFileId);
}

function formatTimestamp(ts) {
  const date = new Date(ts);
  const dd = String(date.getDate()).padStart(2, '0');
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const hh = String(date.getHours()).padStart(2, '0');
  const min = String(date.getMinutes()).padStart(2, '0');
  return `${dd}.${mm} ${hh}:${min}`;
}

export const renderNotepad = (content = 'Новая заметка...') => {
  const state = readNotepadState(content);
  const activeFile =
    state.files.find((file) => file.id === state.activeFileId) || state.files[0];

  return `
    <div class="notepad-app" style="
      display:flex;
      flex-direction:column;
      height:100%;
      background:#c0c0c0;
      font-family:Tahoma, sans-serif;
      color:#000;
      overflow:hidden;
    ">
      <div style="
        display:flex;
        align-items:center;
        gap:6px;
        padding:6px 8px;
        border-bottom:1px solid #808080;
        background:#d4d0c8;
        box-shadow: inset 1px 1px #fff;
        font-size:12px;
      ">
        <button class="notepad-new-btn" style="border:2px outset #fff; background:#c0c0c0; padding:2px 8px; cursor:pointer;">Новый</button>
        <button class="notepad-rename-btn" style="border:2px outset #fff; background:#c0c0c0; padding:2px 8px; cursor:pointer;">Переименовать</button>
        <button class="notepad-delete-btn" style="border:2px outset #fff; background:#c0c0c0; padding:2px 8px; cursor:pointer;">Удалить</button>
        <div style="width:1px; height:18px; background:#808080; border-right:1px solid #fff; margin:0 4px;"></div>
        <div class="notepad-file-title" style="font-weight:bold; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;">
          ${escapeHtml(activeFile.name)}
        </div>
      </div>

      <div style="display:flex; flex:1; min-height:0;">
        <div class="notepad-sidebar" style="
          width:220px;
          min-width:220px;
          border-right:1px solid #808080;
          background:#d4d0c8;
          display:flex;
          flex-direction:column;
        ">
          <div style="
            padding:6px 8px;
            font-size:11px;
            font-weight:bold;
            border-bottom:1px solid #9a9a9a;
            background:#e3e3e3;
          ">
            ФАЙЛЫ
          </div>

          <div class="notepad-files-list" style="flex:1; overflow:auto; padding:4px;">
            ${state.files
              .map((file) => {
                const isActive = file.id === activeFile.id;
                return `
                  <div
                    class="notepad-file-item"
                    data-file-id="${escapeHtml(file.id)}"
                    style="
                      border:1px solid ${isActive ? '#0a246a' : '#b0b0b0'};
                      background:${isActive ? '#0a246a' : '#f2f2f2'};
                      color:${isActive ? '#fff' : '#000'};
                      padding:6px;
                      margin-bottom:4px;
                      cursor:pointer;
                    "
                  >
                    <div style="
                      font-size:12px;
                      font-weight:bold;
                      white-space:nowrap;
                      overflow:hidden;
                      text-overflow:ellipsis;
                    ">${escapeHtml(file.name)}</div>
                    <div style="font-size:10px; opacity:0.85; margin-top:4px;">
                      ${formatTimestamp(file.updatedAt)}
                    </div>
                  </div>
                `;
              })
              .join('')}
          </div>
        </div>

        <div style="flex:1; display:flex; flex-direction:column; min-width:0; background:#c0c0c0;">
          <div style="
            padding:6px 8px;
            font-size:11px;
            background:#e9e9e9;
            border-bottom:1px solid #b5b5b5;
            display:flex;
            justify-content:space-between;
            gap:8px;
          ">
            <span class="notepad-path">C:\\Notes\\${escapeHtml(activeFile.name)}</span>
            <span class="notepad-save-status">Сохранено</span>
          </div>

          <textarea
            class="notepad-editor"
            spellcheck="false"
            style="
              width:100%;
              height:100%;
              flex:1;
              min-height:0;
              border:2px inset #fff;
              padding:10px 12px;
              font-family:'Courier New', monospace;
              font-size:14px;
              line-height:1.45;
              resize:none;
              background:#fff;
              color:#000;
              box-sizing:border-box;
              outline:none;
            "
          >${escapeHtml(activeFile.content)}</textarea>

          <div style="
            height:24px;
            background:#d4d0c8;
            border-top:1px solid #808080;
            display:flex;
            align-items:center;
            padding:0 8px;
            font-size:11px;
            gap:12px;
          ">
            <span class="notepad-char-count">Символов: ${activeFile.content.length}</span>
            <span class="notepad-line-count">Строк: ${Math.max(1, activeFile.content.split('\n').length)}</span>
          </div>
        </div>
      </div>
    </div>
  `;
};

export const initNotepad = (root: HTMLElement | Document = document, initialContent = 'Новая заметка...') => {
  const app = (root instanceof HTMLElement) ? root : root.querySelector('.notepad-app');
  if (!app) return;

  let state = readNotepadState(initialContent);

  const editor = app.querySelector('.notepad-editor') as HTMLTextAreaElement;
  const filesList = app.querySelector('.notepad-files-list') as HTMLElement;
  const fileTitle = app.querySelector('.notepad-file-title') as HTMLElement;
  const pathEl = app.querySelector('.notepad-path') as HTMLElement;
  const saveStatus = app.querySelector('.notepad-save-status') as HTMLElement;
  const charCount = app.querySelector('.notepad-char-count') as HTMLElement;
  const lineCount = app.querySelector('.notepad-line-count') as HTMLElement;
  const newBtn = app.querySelector('.notepad-new-btn');
  const renameBtn = app.querySelector('.notepad-rename-btn');
  const deleteBtn = app.querySelector('.notepad-delete-btn');

  if (!editor || !filesList || !fileTitle || !pathEl || !saveStatus || !charCount || !lineCount) {
    return;
  }

  let saveTimer: any = null;

  const getActiveFile = () =>
    state.files.find((file: any) => file.id === state.activeFileId) || state.files[0];

  const renderFilesList = () => {
    const activeFile = getActiveFile();

    filesList.innerHTML = state.files
      .map((file: any) => {
        const isActive = file.id === activeFile.id;
        return `
          <div
            class="notepad-file-item"
            data-file-id="${escapeHtml(file.id)}"
            style="
              border:1px solid ${isActive ? '#0a246a' : '#b0b0b0'};
              background:${isActive ? '#0a246a' : '#f2f2f2'};
              color:${isActive ? '#fff' : '#000'};
              padding:6px;
              margin-bottom:4px;
              cursor:pointer;
            "
          >
            <div style="
              font-size:12px;
              font-weight:bold;
              white-space:nowrap;
              overflow:hidden;
              text-overflow:ellipsis;
            ">${escapeHtml(file.name)}</div>
            <div style="font-size:10px; opacity:0.85; margin-top:4px;">
              ${formatTimestamp(file.updatedAt)}
            </div>
          </div>
        `;
      })
      .join('');

    filesList.querySelectorAll('.notepad-file-item').forEach((item) => {
      item.addEventListener('click', () => {
        const fileId = item.getAttribute('data-file-id');
        if (!fileId || fileId === state.activeFileId) return;
        forceSave();
        state.activeFileId = fileId;
        writeNotepadState(state);
        renderActiveFile();
        renderFilesList();
      });
    });
  };

  const renderActiveFile = () => {
    const activeFile = getActiveFile();
    editor.value = activeFile.content;
    fileTitle.textContent = activeFile.name;
    pathEl.textContent = `C:\\Notes\\${activeFile.name}`;
    charCount.textContent = `Символов: ${activeFile.content.length}`;
    lineCount.textContent = `Строк: ${Math.max(1, activeFile.content.split('\n').length)}`;
    saveStatus.textContent = 'Сохранено';
  };

  const updateCounters = () => {
    charCount.textContent = `Символов: ${editor.value.length}`;
    lineCount.textContent = `Строк: ${Math.max(1, editor.value.split('\n').length)}`;
  };

  const doSave = () => {
    const activeFile = getActiveFile();
    if (!activeFile) return;

    activeFile.content = editor.value;
    activeFile.updatedAt = Date.now();

    writeNotepadState(state);
    saveStatus.textContent = 'Сохранено';
    renderFilesList();
    updateCounters();
  };

  const scheduleSave = () => {
    saveStatus.textContent = 'Сохранение...';
    if (saveTimer) clearTimeout(saveTimer);
    saveTimer = setTimeout(() => {
      doSave();
      saveTimer = null;
    }, 250);
  };

  const forceSave = () => {
    if (saveTimer) {
      clearTimeout(saveTimer);
      saveTimer = null;
    }
    doSave();
  };

  const createFile = () => {
    forceSave();

    const nextNumber = state.files.length + 1;
    const id = `note_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;
    const file = {
      id,
      name: `Заметка ${nextNumber}.txt`,
      content: '',
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };

    state.files.unshift(file);
    state.activeFileId = file.id;
    writeNotepadState(state);
    renderFilesList();
    renderActiveFile();
    editor.focus();
  };

  const renameFile = () => {
    const activeFile = getActiveFile();
    if (!activeFile) return;

    const nextName = prompt('Новое имя файла:', activeFile.name);
    if (!nextName) return;

    const cleaned = nextName.trim();
    if (!cleaned) return;

    activeFile.name = cleaned.endsWith('.txt') ? cleaned : `${cleaned}.txt`;
    activeFile.updatedAt = Date.now();

    writeNotepadState(state);
    renderFilesList();
    renderActiveFile();
  };

  const deleteFile = () => {
    if (state.files.length === 1) {
      alert('Нельзя удалить последний файл.');
      return;
    }

    const activeFile = getActiveFile();
    if (!activeFile) return;

    const ok = confirm(`Удалить "${activeFile.name}"?`);
    if (!ok) return;

    state.files = state.files.filter((file: any) => file.id !== activeFile.id);
    state.activeFileId = state.files[0].id;

    writeNotepadState(state);
    renderFilesList();
    renderActiveFile();
  };

  editor.addEventListener('input', () => {
    updateCounters();
    scheduleSave();
  });

  editor.addEventListener('blur', forceSave);

  newBtn?.addEventListener('click', createFile);
  renameBtn?.addEventListener('click', renameFile);
  deleteBtn?.addEventListener('click', deleteFile);

  document.addEventListener('visibilitychange', () => {
    if (document.hidden) forceSave();
  });

  window.addEventListener('beforeunload', forceSave);

  renderFilesList();
  renderActiveFile();
};