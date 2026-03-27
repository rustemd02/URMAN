export type FileType = 'app' | 'folder' | 'doc' | 'image';

export interface SystemItem {
    id: string;
    name: string;
    type: FileType;
    icon: string;
    parentId: string | null;
    content?: string | string[]; 
}

export const SYSTEM_REGISTRY: SystemItem[] = [
    // Apps
    { id: 'chat', name: 'ICQ (Сельсовет)', type: 'app', icon: '💬', parentId: null },
    { 
        id: 'browser', 
        name: 'Yangir', 
        type: 'app', 
        icon: `<div style="width: 32px; height: 32px; margin: 0 auto 4px; background: #c0c0c0; position: relative; border: 1px solid transparent;">
                <div style="width: 24px; height: 24px; background: #000080; border-radius: 50%; margin: 4px; border: 2px solid #fff;"></div>
                <div style="position: absolute; top: 8px; left: 10px; color: #fff; font-weight: bold; font-size: 16px; font-family: 'MS Sans Serif'; line-height: 1;">Я</div>
            </div>`, 
        parentId: null 
    },
    { id: 'minesweeper', name: 'Тетрис (Урман)', type: 'app', icon: '🧩', parentId: null },
    { id: 'notepad', name: 'Заметки Айдара', type: 'app', icon: '📝', parentId: null },
    { id: 'player', name: 'Winamp', type: 'app', icon: '📻', parentId: null },
    { id: 'terminal', name: 'MS-DOS', type: 'app', icon: '💻', parentId: null },
    { 
        id: 'calendar', 
        name: 'Календарь', 
        type: 'app', 
        icon: `<div style="width: 32px; height: 32px; margin: 0 auto 4px; display: flex; align-items: center; justify-content: center;"><span style="font-size: 24px;">⏳</span></div>`, 
        parentId: null 
    },
    { id: 'task_manager', name: 'Task Manager', type: 'app', icon: '📋', parentId: null },
    { id: 'village', name: 'Отойти от компьютера', type: 'app', icon: '🚪', parentId: null },

    // Folders
    { id: 'my_docs', name: 'Документлар', type: 'folder', icon: '📁', parentId: null, content: ['pact_1999', 'forest_plan', 'missing_report'] },
    { id: 'photos', name: 'Фото_Архив', type: 'folder', icon: '📁', parentId: null, content: ['photo_forest', 'photo_old_man'] },

    // Files
    { id: 'pact_1999', name: 'ПАКТ_1999.doc', type: 'doc', icon: '📄', parentId: 'my_docs', content: 'ДОГОВОР (ПАКТ) ОТ 1999 ГОДА...' },
    { id: 'forest_plan', name: 'ПЛАН_ВЫРУБКИ.doc', type: 'doc', icon: '📄', parentId: 'my_docs', content: 'ОБЪЕКТ: Участок 44-Б...' },
    { id: 'trash', name: 'Чүплек', type: 'app', icon: '🗑️', parentId: null }
];
