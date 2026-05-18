import { OldPcSection } from '../data/oldPcContent';

export type FileType = 'app' | 'folder' | 'doc' | 'image';

export interface SystemItem {
    id: string;
    name: string;
    type: FileType;
    icon: string;
    parentId: string | null;
    content?: string | string[];
    launchSection?: OldPcSection;
}

export const SYSTEM_REGISTRY: SystemItem[] = [
    // Apps
    { id: 'archive_search', name: 'Архивный поиск', type: 'app', icon: '🔎', parentId: null, launchSection: 'archive_search' },
    { id: 'documents_marat', name: 'Документы Марата', type: 'app', icon: '📁', parentId: null, launchSection: 'documents_marat' },
    { 
        id: 'browser', 
        name: 'Кырлай архив', 
        type: 'app', 
        icon: `<div style="width: 32px; height: 32px; margin: 0 auto 4px; background: #c0c0c0; position: relative; border: 1px solid transparent;">
                <div style="width: 24px; height: 24px; background: #000080; border-radius: 50%; margin: 4px; border: 2px solid #fff;"></div>
                <div style="position: absolute; top: 8px; left: 10px; color: #fff; font-weight: bold; font-size: 16px; font-family: 'MS Sans Serif'; line-height: 1;">К</div>
            </div>`, 
        parentId: null,
        launchSection: 'archive_search'
    },
    { id: 'tatarwiki', name: 'Татарвики', type: 'app', icon: '📚', parentId: null, launchSection: 'tatarwiki' },
    { id: 'saved_messages', name: 'Сохранённые сообщения', type: 'app', icon: '💬', parentId: null, launchSection: 'saved_messages' },
    { id: 'internal_accounting', name: 'Учёт Кырлая', type: 'app', icon: '📇', parentId: null, launchSection: 'internal_accounting' },
    { id: 'damaged_hidden', name: 'Повреждённые файлы', type: 'app', icon: '▣', parentId: null, launchSection: 'damaged_hidden' },
    { id: 'chat', name: 'Старый мессенджер', type: 'app', icon: '💬', parentId: null },
    { id: 'notepad', name: 'Заметки Айдара', type: 'app', icon: '📝', parentId: null },
    { id: 'player', name: 'Проигрыватель', type: 'app', icon: '📻', parentId: null },
    { id: 'terminal', name: 'Командная строка', type: 'app', icon: '💻', parentId: null },
    { 
        id: 'calendar', 
        name: 'Календарь', 
        type: 'app', 
        icon: `<div style="width: 32px; height: 32px; margin: 0 auto 4px; display: flex; align-items: center; justify-content: center;"><span style="font-size: 24px;">⏳</span></div>`, 
        parentId: null 
    },
    { id: 'task_manager', name: 'Состояние', type: 'app', icon: '📋', parentId: null },
    { id: 'village', name: 'Отойти от компьютера', type: 'app', icon: '🚪', parentId: null },

    // Folders
    { id: 'registry_folder', name: 'Реестр домов', type: 'app', icon: '📁', parentId: null, launchSection: 'household_registry' },
    { id: 'compensation_folder', name: 'Компенсации', type: 'app', icon: '📁', parentId: null, launchSection: 'violations_compensation' },
    { id: 'kara_urman_folder', name: 'Кара-Урман', type: 'app', icon: '📁', parentId: null, launchSection: 'kara_urman' },
    { id: 'household_folder', name: 'Хозяйство', type: 'app', icon: '📁', parentId: null, launchSection: 'household_misc' },

    { id: 'trash', name: 'Чүплек', type: 'app', icon: '🗑️', parentId: null }
];
